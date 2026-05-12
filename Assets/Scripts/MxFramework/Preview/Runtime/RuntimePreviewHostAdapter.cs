using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Preview
{
    public delegate IRuntimeModule RuntimePreviewHostModuleFactory();

    /// <summary>
    /// Preview adapter path backed by RuntimeHost, RuntimeCommandBuffer, RuntimeClock and RuntimeReplayRecorder.
    /// This is intentionally separate from ScenePreviewWorld so the host lifecycle can be tested in isolation.
    /// </summary>
    public sealed class RuntimePreviewHostAdapter : IDisposable
    {
        private const int PreviewSourceId = 4104;
        private const int ApplyBuffCommandId = 1;
        private const string CommandModuleId = "preview.host.commands";
        private const string WorldTickModuleId = "preview.host.worldTick";

        private readonly IPreviewWorld _world;
        private readonly PreviewLogBuffer _logs;
        private readonly IRuntimePreviewModeSource _modeSource;
        private readonly IRuntimePreviewConfigMetadataSource _configMetadataSource;
        private readonly IRuntimePreviewFailureSource _failureSource;
        private readonly List<RuntimePreviewHostModuleFactory> _moduleFactories = new List<RuntimePreviewHostModuleFactory>();
        private readonly Dictionary<long, PendingPreviewCommand> _pendingCommands = new Dictionary<long, PendingPreviewCommand>();
        private readonly Dictionary<string, int> _runtimeIds = new Dictionary<string, int>(StringComparer.Ordinal);

        private RuntimeHost _host;
        private RuntimeClock _clock;
        private RuntimeCommandBuffer _commandBuffer;
        private RuntimeReplayRecorder _replayRecorder;
        private IReadOnlyList<RuntimeCommand> _lastFrameCommands = Array.Empty<RuntimeCommand>();
        private RuntimePreviewHostCommandResult _lastCommandResult;
        private bool _tickWorldThisFrame;
        private bool _initialized;
        private bool _started;
        private bool _disposed;
        private int _nextRuntimeId = 1;

        public RuntimePreviewHostAdapter(
            IPreviewWorld world,
            PreviewLogBuffer logs = null,
            IEnumerable<RuntimePreviewHostModuleFactory> moduleFactories = null)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _logs = logs ?? new PreviewLogBuffer();
            _modeSource = world as IRuntimePreviewModeSource;
            _configMetadataSource = world as IRuntimePreviewConfigMetadataSource;
            _failureSource = world as IRuntimePreviewFailureSource;

            if (moduleFactories != null)
            {
                foreach (RuntimePreviewHostModuleFactory factory in moduleFactories)
                {
                    if (factory != null)
                        _moduleFactories.Add(factory);
                }
            }

            CreateRuntimeState();
        }

        public double SecondsPerFrame { get; set; } = 1d / 60d;

        public string PreviewMode => _modeSource != null && !string.IsNullOrEmpty(_modeSource.PreviewMode)
            ? _modeSource.PreviewMode
            : "runtime";

        public string FallbackReason => _modeSource != null ? _modeSource.FallbackReason ?? string.Empty : string.Empty;

        public RuntimePreviewConfigMetadata ConfigMetadata => _configMetadataSource != null
            ? (_configMetadataSource.CurrentConfigMetadata?.Clone() ?? new RuntimePreviewConfigMetadata())
            : new RuntimePreviewConfigMetadata();

        public RuntimeLifecycleState HostState => _host != null
            ? _host.State
            : _disposed ? RuntimeLifecycleState.Disposed : RuntimeLifecycleState.Created;
        public long HostTickCount => _host != null ? _host.TickCount : 0L;
        public RuntimeFrame CurrentFrame => _clock != null ? _clock.CurrentFrame : RuntimeFrame.Zero;
        public int PendingCommandCount => _commandBuffer != null ? _commandBuffer.PendingCount : 0;
        public int ReplayFrameCount => _replayRecorder != null ? _replayRecorder.Count : 0;
        public IReadOnlyList<RuntimeHostError> HostErrors => _host != null ? _host.Errors : Array.Empty<RuntimeHostError>();

        public RuntimeReplaySnapshot CreateReplaySnapshot()
        {
            return _replayRecorder != null
                ? _replayRecorder.CreateSnapshot()
                : new RuntimeReplaySnapshot(CreateReplayHeader(RuntimeFrame.Zero), Array.Empty<RuntimeReplayFrameRecord>());
        }

        public void Initialize()
        {
            EnsureNotDisposed();
            if (_initialized)
                return;

            _host.Initialize();
            _initialized = true;
            _logs.Append("info", "RuntimePreviewHostAdapter: host initialized.");
        }

        public void Start()
        {
            EnsureNotDisposed();
            if (!_initialized)
                Initialize();
            if (_started)
                return;

            _host.Start();
            _started = true;
            _logs.Append("info", "RuntimePreviewHostAdapter: host started.");
        }

        public void Stop()
        {
            if (_host == null || _host.State == RuntimeLifecycleState.Disposed)
                return;

            _host.Stop();
            _started = false;
            _logs.Append("info", "RuntimePreviewHostAdapter: host stopped.");
        }

        public RuntimePreviewAdapterResult ApplyBuff(RuntimePreviewApplyBuffRequest request)
        {
            request = request ?? new RuntimePreviewApplyBuffRequest();
            string previewMode = PreviewMode;
            string casterId = string.IsNullOrEmpty(request.CasterId) ? "TestCaster" : request.CasterId;
            string targetId = string.IsNullOrEmpty(request.TargetId) ? "TestTarget" : request.TargetId;
            int stack = request.Stack <= 0 ? 1 : request.Stack;

            if (!IsStarted(out RuntimePreviewAdapterResult lifecycleFailure, PreviewError.ApplyBuffFailed, request.BuffId, targetId))
                return lifecycleFailure;

            if (string.IsNullOrEmpty(request.BuffId))
                return Fail(PreviewError.ApplyBuffFailed, "missing_buff_id", "buffId is required.", previewMode, request.BuffId, targetId);

            if (!int.TryParse(request.BuffId, out int runtimeBuffId))
                return Fail(PreviewError.ApplyBuffFailed, "invalid_buff_id", "buffId must be a numeric runtime buff id.", previewMode, request.BuffId, targetId);

            if (request.WaitTicks < 0)
                return Fail(PreviewError.ApplyBuffFailed, "invalid_wait_ticks", "waitTicks must be greater than or equal to zero.", previewMode, request.BuffId, targetId);

            _lastCommandResult = null;
            RuntimeFrame commandFrame = CurrentFrame;
            var command = new RuntimeCommand(
                commandFrame,
                PreviewSourceId,
                ApplyBuffCommandId,
                GetRuntimeId(targetId),
                payload0: runtimeBuffId,
                payload1: stack,
                payload2: request.DurationOverrideMs.HasValue ? 1 : 0,
                traceId: "preview.applyBuff:" + request.BuffId);

            RuntimeCommandValidationResult enqueue = _commandBuffer.Enqueue(command);
            if (!enqueue.Success)
            {
                string message = "Runtime preview command rejected: " + enqueue.Error;
                _logs.Append("error", "RuntimePreviewHostAdapter: " + message);
                return Fail(PreviewError.ApplyBuffFailed, "command_rejected", message, previewMode, request.BuffId, targetId);
            }

            _pendingCommands[enqueue.Command.Sequence] = new PendingPreviewCommand(
                request.BuffId,
                casterId,
                targetId,
                stack,
                request.DurationOverrideMs);

            _logs.Append("info", $"RuntimePreviewHostAdapter: queued applyBuff frame={commandFrame.Value} buffId={request.BuffId} targetId={targetId} stack={stack} waitTicks={request.WaitTicks}");

            if (!AdvanceHostFrame(tickWorld: false, PreviewError.ApplyBuffFailed, request.BuffId, targetId, out RuntimePreviewAdapterResult hostFailure))
                return hostFailure;

            if (_lastCommandResult == null)
                return Fail(PreviewError.ApplyBuffFailed, "command_not_executed", "RuntimeHost tick completed without executing the preview command.", previewMode, request.BuffId, targetId);

            if (!_lastCommandResult.Success)
                return Fail(PreviewError.ApplyBuffFailed, _lastCommandResult.Reason, _lastCommandResult.Message, previewMode, request.BuffId, targetId);

            if (request.WaitTicks > 0 && !AdvanceWorldFrames(request.WaitTicks, PreviewError.ApplyBuffFailed, request.BuffId, targetId, out hostFailure))
                return hostFailure;

            RuntimePreviewSnapshot snapshot = Snapshot(targetId);
            _logs.Append("info", $"RuntimePreviewHostAdapter: applyBuff success previewMode={previewMode} buffId={request.BuffId} targetId={targetId} hostTicks={HostTickCount}");
            return RuntimePreviewAdapterResult.Ok(previewMode, request.BuffId, targetId, snapshot);
        }

        public RuntimePreviewAdapterResult Tick(int frames, string targetId)
        {
            string resolvedTargetId = string.IsNullOrEmpty(targetId) ? "TestTarget" : targetId;

            if (frames < 0)
                return Fail(PreviewError.InvalidParams, "invalid_tick_count", "frames must be greater than or equal to zero.", PreviewMode, string.Empty, resolvedTargetId);

            if (!IsStarted(out RuntimePreviewAdapterResult lifecycleFailure, PreviewError.InternalError, string.Empty, resolvedTargetId))
                return lifecycleFailure;

            if (frames > 0 && !AdvanceWorldFrames(frames, PreviewError.InternalError, string.Empty, resolvedTargetId, out RuntimePreviewAdapterResult hostFailure))
                return hostFailure;

            return RuntimePreviewAdapterResult.Ok(PreviewMode, string.Empty, resolvedTargetId, Snapshot(resolvedTargetId));
        }

        public RuntimePreviewAdapterResult Reset(bool reloadBase)
        {
            EnsureNotDisposed();

            string previewMode = PreviewMode;
            bool restart = _started;
            bool reinitialize = _initialized;

            try
            {
                _world.Reset(reloadBase);
            }
            catch (Exception ex)
            {
                _logs.Append("error", $"RuntimePreviewHostAdapter: reset exception previewMode={previewMode} message={ex.Message}");
                return Fail(PreviewError.InternalError, "world_reset_exception", ex.Message, previewMode, string.Empty, string.Empty);
            }

            DisposeRuntimeState();
            CreateRuntimeState();
            _initialized = false;
            _started = false;

            if (reinitialize)
                Initialize();
            if (restart)
                Start();

            _logs.Append("info", $"RuntimePreviewHostAdapter: reset previewMode={previewMode} reloadBase={reloadBase}");
            return RuntimePreviewAdapterResult.Ok(PreviewMode, string.Empty, string.Empty, RuntimePreviewSnapshot.Empty(PreviewMode));
        }

        public RuntimePreviewSnapshot Snapshot(string targetId)
        {
            string previewMode = PreviewMode;
            string resolvedTargetId = string.IsNullOrEmpty(targetId) ? "TestTarget" : targetId;
            var snapshot = new RuntimePreviewSnapshot(previewMode, resolvedTargetId);
            snapshot.ConfigMetadata = ConfigMetadata;

            IReadOnlyList<BuffSnapshot> buffs = _world.SnapshotBuffs(resolvedTargetId);
            for (int i = 0; i < buffs.Count; i++) snapshot.BuffSnapshots.Add(buffs[i]);

            IReadOnlyList<AttributeChange> attrs = _world.SnapshotAttributeChanges(resolvedTargetId);
            for (int i = 0; i < attrs.Count; i++) snapshot.AttributeChanges.Add(attrs[i]);

            IReadOnlyList<DamageTick> damage = _world.DrainDamageTicks();
            for (int i = 0; i < damage.Count; i++) snapshot.DamageTicks.Add(damage[i]);

            IReadOnlyList<StatusChange> status = _world.DrainStatusChanges();
            for (int i = 0; i < status.Count; i++) snapshot.StatusChanges.Add(status[i]);

            return snapshot;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            DisposeRuntimeState();
            _disposed = true;
            _initialized = false;
            _started = false;
        }

        private bool AdvanceWorldFrames(
            int frames,
            int failureCode,
            string buffId,
            string targetId,
            out RuntimePreviewAdapterResult failure)
        {
            failure = null;
            for (int i = 0; i < frames; i++)
            {
                if (!AdvanceHostFrame(tickWorld: true, failureCode, buffId, targetId, out failure))
                    return false;
            }

            return true;
        }

        private bool AdvanceHostFrame(
            bool tickWorld,
            int failureCode,
            string buffId,
            string targetId,
            out RuntimePreviewAdapterResult failure)
        {
            failure = null;
            RuntimeFrame frame = CurrentFrame;
            int errorCountBefore = _host.Errors.Count;
            _lastFrameCommands = Array.Empty<RuntimeCommand>();
            _tickWorldThisFrame = tickWorld;

            try
            {
                _host.Tick(frame.Value, SecondsPerFrame, frame.Value * SecondsPerFrame);
                RecordReplayFrame(frame);
                _clock.Step();
            }
            catch (RuntimeHostException ex)
            {
                RecordReplayFrame(frame);
                failure = Fail(failureCode, "host_exception", ex.Message, PreviewMode, buffId, targetId);
                _logs.Append("error", "RuntimePreviewHostAdapter: " + ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                failure = Fail(failureCode, "host_exception", ex.Message, PreviewMode, buffId, targetId);
                _logs.Append("error", "RuntimePreviewHostAdapter: host tick exception " + ex.Message);
                return false;
            }
            finally
            {
                _tickWorldThisFrame = false;
            }

            if (_host.Errors.Count > errorCountBefore)
            {
                RuntimeHostError error = _host.Errors[_host.Errors.Count - 1];
                string module = string.IsNullOrEmpty(error.ModuleId) ? "<host>" : error.ModuleId;
                string message = "RuntimeHost " + error.Operation + " failed for module '" + module + "': " + error.Message;
                failure = Fail(failureCode, "host_exception", message, PreviewMode, buffId, targetId);
                _logs.Append("error", "RuntimePreviewHostAdapter: " + message);
                return false;
            }

            return true;
        }

        private void DrainRuntimeCommands(RuntimeTickContext context)
        {
            RuntimeFrame frame = new RuntimeFrame(context.FrameIndex);
            IReadOnlyList<RuntimeCommand> commands = _commandBuffer.DrainForFrame(frame);
            _lastFrameCommands = commands;

            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                if (command.CommandId != ApplyBuffCommandId)
                    continue;

                if (!_pendingCommands.TryGetValue(command.Sequence, out PendingPreviewCommand pending))
                    continue;

                _pendingCommands.Remove(command.Sequence);
                ExecuteApplyBuffCommand(pending);
            }
        }

        private void ExecuteApplyBuffCommand(PendingPreviewCommand pending)
        {
            bool applied = _world.ApplyBuff(
                pending.BuffId,
                pending.CasterId,
                pending.TargetId,
                pending.Stack,
                pending.DurationOverrideMs);

            if (applied)
            {
                _lastCommandResult = RuntimePreviewHostCommandResult.Ok();
                return;
            }

            string reason = !string.IsNullOrEmpty(_failureSource?.LastFailureReason)
                ? _failureSource.LastFailureReason
                : PreviewMode == "dummy"
                    ? "dummy_missing_factory_or_config"
                    : "buff_factory_or_config_rejected";
            string message = !string.IsNullOrEmpty(_failureSource?.LastFailureMessage)
                ? _failureSource.LastFailureMessage
                : $"ApplyBuff failed for buffId={pending.BuffId}, targetId={pending.TargetId}, previewMode={PreviewMode}.";
            if (!string.IsNullOrEmpty(FallbackReason))
                message += " " + FallbackReason;

            _lastCommandResult = RuntimePreviewHostCommandResult.Fail(reason, message);
        }

        private void TickPreviewWorld(RuntimeTickContext context)
        {
            if (_tickWorldThisFrame)
                _world.Tick(1);
        }

        private void RecordReplayFrame(RuntimeFrame frame)
        {
            _replayRecorder.RecordFrame(
                frame,
                _lastFrameCommands,
                ComputeResultHash(frame, _lastFrameCommands),
                "hostTicks=" + HostTickCount + " commands=" + _lastFrameCommands.Count + " errors=" + _host.Errors.Count);
        }

        private long ComputeResultHash(RuntimeFrame frame, IReadOnlyList<RuntimeCommand> commands)
        {
            unchecked
            {
                long hash = 17L;
                hash = (hash * 31L) + frame.Value;
                hash = (hash * 31L) + HostTickCount;
                hash = (hash * 31L) + PendingCommandCount;
                hash = (hash * 31L) + _host.Errors.Count;
                for (int i = 0; i < commands.Count; i++)
                {
                    RuntimeCommand command = commands[i];
                    hash = (hash * 31L) + command.SourceId;
                    hash = (hash * 31L) + command.CommandId;
                    hash = (hash * 31L) + command.TargetId;
                    hash = (hash * 31L) + command.Payload0;
                    hash = (hash * 31L) + command.Payload1;
                    hash = (hash * 31L) + command.Payload2;
                    hash = (hash * 31L) + command.Sequence;
                }

                return hash;
            }
        }

        private bool IsStarted(out RuntimePreviewAdapterResult failure, int code, string buffId, string targetId)
        {
            failure = null;
            if (_started && _host != null && _host.State == RuntimeLifecycleState.Started)
                return true;

            failure = Fail(code, "host_not_started", "RuntimePreviewHostAdapter host is not started.", PreviewMode, buffId, targetId);
            return false;
        }

        private int GetRuntimeId(string value)
        {
            value = value ?? string.Empty;
            if (_runtimeIds.TryGetValue(value, out int id))
                return id;

            id = _nextRuntimeId++;
            _runtimeIds[value] = id;
            return id;
        }

        private void CreateRuntimeState()
        {
            _pendingCommands.Clear();
            _runtimeIds.Clear();
            _nextRuntimeId = 1;
            _lastFrameCommands = Array.Empty<RuntimeCommand>();
            _lastCommandResult = null;
            _tickWorldThisFrame = false;
            _clock = new RuntimeClock(RuntimeFrame.Zero);
            _commandBuffer = new RuntimeCommandBuffer(null, RuntimeFrame.Zero);
            _replayRecorder = new RuntimeReplayRecorder(CreateReplayHeader(RuntimeFrame.Zero));

            _host = new RuntimeHost(new RuntimeHostOptions
            {
                ErrorPolicy = RuntimeHostErrorPolicy.CollectAndStopFrame
            });
            _host.RegisterModule(new RuntimePreviewDelegateModule(
                CommandModuleId,
                RuntimeTickStage.PreSimulation,
                -1000,
                DrainRuntimeCommands));

            for (int i = 0; i < _moduleFactories.Count; i++)
            {
                IRuntimeModule module = _moduleFactories[i]();
                if (module != null)
                    _host.RegisterModule(module);
            }

            _host.RegisterModule(new RuntimePreviewDelegateModule(
                WorldTickModuleId,
                RuntimeTickStage.Simulation,
                0,
                TickPreviewWorld));
        }

        private void DisposeRuntimeState()
        {
            if (_host != null)
            {
                _host.Dispose();
                _host = null;
            }

            if (_commandBuffer != null)
                _commandBuffer.Clear();
            if (_replayRecorder != null)
                _replayRecorder.Clear();
            _pendingCommands.Clear();
            _lastFrameCommands = Array.Empty<RuntimeCommand>();
            _lastCommandResult = null;
            _tickWorldThisFrame = false;
        }

        private static RuntimeReplayHeader CreateReplayHeader(RuntimeFrame startFrame)
        {
            return new RuntimeReplayHeader(
                schemaVersion: 0,
                frameworkVersion: "MxFramework.Preview.RuntimeHostAdapter",
                configHash: "preview",
                resourceCatalogHash: "preview",
                startFrame: startFrame);
        }

        private static RuntimePreviewAdapterResult Fail(
            int code,
            string reason,
            string message,
            string previewMode,
            string buffId,
            string targetId)
        {
            return RuntimePreviewAdapterResult.Fail(code, reason, message, previewMode, buffId, targetId);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RuntimePreviewHostAdapter));
        }

        private sealed class PendingPreviewCommand
        {
            public PendingPreviewCommand(
                string buffId,
                string casterId,
                string targetId,
                int stack,
                long? durationOverrideMs)
            {
                BuffId = buffId ?? string.Empty;
                CasterId = casterId ?? string.Empty;
                TargetId = targetId ?? string.Empty;
                Stack = stack;
                DurationOverrideMs = durationOverrideMs;
            }

            public string BuffId { get; }
            public string CasterId { get; }
            public string TargetId { get; }
            public int Stack { get; }
            public long? DurationOverrideMs { get; }
        }

        private sealed class RuntimePreviewHostCommandResult
        {
            private RuntimePreviewHostCommandResult(bool success, string reason, string message)
            {
                Success = success;
                Reason = reason ?? string.Empty;
                Message = message ?? string.Empty;
            }

            public bool Success { get; }
            public string Reason { get; }
            public string Message { get; }

            public static RuntimePreviewHostCommandResult Ok()
            {
                return new RuntimePreviewHostCommandResult(true, string.Empty, string.Empty);
            }

            public static RuntimePreviewHostCommandResult Fail(string reason, string message)
            {
                return new RuntimePreviewHostCommandResult(false, reason, message);
            }
        }

        private sealed class RuntimePreviewDelegateModule : RuntimeModule
        {
            private readonly Action<RuntimeTickContext> _tick;

            public RuntimePreviewDelegateModule(
                string moduleId,
                RuntimeTickStage tickStage,
                int priority,
                Action<RuntimeTickContext> tick)
                : base(moduleId, tickStage, priority)
            {
                _tick = tick;
            }

            public override void Tick(RuntimeTickContext context)
            {
                _tick?.Invoke(context);
            }
        }
    }
}

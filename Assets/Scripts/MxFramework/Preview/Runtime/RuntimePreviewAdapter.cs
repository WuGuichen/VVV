using System;
using System.Collections.Generic;

namespace MxFramework.Preview
{
    /// <summary>
    /// Runtime boundary used by the preview protocol to drive apply / tick / snapshot / reset.
    /// The adapter keeps protocol error mapping and preview-mode logging out of world implementations.
    /// </summary>
    public sealed class RuntimePreviewAdapter
    {
        private readonly IPreviewWorld _world;
        private readonly PreviewLogBuffer _logs;
        private readonly IRuntimePreviewModeSource _modeSource;
        private readonly IRuntimePreviewConfigMetadataSource _configMetadataSource;
        private readonly IRuntimePreviewFailureSource _failureSource;

        public RuntimePreviewAdapter(IPreviewWorld world, PreviewLogBuffer logs = null)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _logs = logs ?? new PreviewLogBuffer();
            _modeSource = world as IRuntimePreviewModeSource;
            _configMetadataSource = world as IRuntimePreviewConfigMetadataSource;
            _failureSource = world as IRuntimePreviewFailureSource;
        }

        public string PreviewMode => _modeSource != null && !string.IsNullOrEmpty(_modeSource.PreviewMode)
            ? _modeSource.PreviewMode
            : "runtime";

        public string FallbackReason => _modeSource != null ? _modeSource.FallbackReason ?? string.Empty : string.Empty;

        public RuntimePreviewConfigMetadata ConfigMetadata => _configMetadataSource != null
            ? (_configMetadataSource.CurrentConfigMetadata?.Clone() ?? new RuntimePreviewConfigMetadata())
            : new RuntimePreviewConfigMetadata();

        public RuntimePreviewAdapterResult ApplyBuff(RuntimePreviewApplyBuffRequest request)
        {
            request = request ?? new RuntimePreviewApplyBuffRequest();
            string previewMode = PreviewMode;
            string casterId = string.IsNullOrEmpty(request.CasterId) ? "TestCaster" : request.CasterId;
            string targetId = string.IsNullOrEmpty(request.TargetId) ? "TestTarget" : request.TargetId;
            int stack = request.Stack <= 0 ? 1 : request.Stack;

            if (string.IsNullOrEmpty(request.BuffId))
                return Fail(PreviewError.ApplyBuffFailed, "missing_buff_id", "buffId is required.", previewMode, request.BuffId, targetId);

            if (!int.TryParse(request.BuffId, out _))
                return Fail(PreviewError.ApplyBuffFailed, "invalid_buff_id", "buffId must be a numeric runtime buff id.", previewMode, request.BuffId, targetId);

            if (request.WaitTicks < 0)
                return Fail(PreviewError.ApplyBuffFailed, "invalid_wait_ticks", "waitTicks must be greater than or equal to zero.", previewMode, request.BuffId, targetId);

            _logs.Append("info", $"RuntimePreviewAdapter: applyBuff begin previewMode={previewMode} buffId={request.BuffId} casterId={casterId} targetId={targetId} stack={stack} waitTicks={request.WaitTicks}");

            bool applied;
            try
            {
                applied = _world.ApplyBuff(request.BuffId, casterId, targetId, stack, request.DurationOverrideMs);
                if (applied && request.WaitTicks > 0)
                    _world.Tick(request.WaitTicks);
            }
            catch (Exception ex)
            {
                _logs.Append("error", $"RuntimePreviewAdapter: applyBuff exception previewMode={previewMode} reason=world_exception message={ex.Message}");
                return Fail(PreviewError.ApplyBuffFailed, "world_exception", ex.Message, previewMode, request.BuffId, targetId);
            }

            if (!applied)
            {
                string reason = !string.IsNullOrEmpty(_failureSource?.LastFailureReason)
                    ? _failureSource.LastFailureReason
                    : previewMode == "dummy"
                    ? "dummy_missing_factory_or_config"
                    : "buff_factory_or_config_rejected";
                string message = !string.IsNullOrEmpty(_failureSource?.LastFailureMessage)
                    ? _failureSource.LastFailureMessage
                    : $"ApplyBuff failed for buffId={request.BuffId}, targetId={targetId}, previewMode={previewMode}.";
                if (!string.IsNullOrEmpty(FallbackReason))
                    message += " " + FallbackReason;

                _logs.Append("error", $"RuntimePreviewAdapter: applyBuff failed previewMode={previewMode} reason={reason} buffId={request.BuffId} targetId={targetId}");
                return Fail(PreviewError.ApplyBuffFailed, reason, message, previewMode, request.BuffId, targetId);
            }

            RuntimePreviewSnapshot snapshot = Snapshot(targetId);
            _logs.Append("info", $"RuntimePreviewAdapter: applyBuff success previewMode={previewMode} buffId={request.BuffId} targetId={targetId}");
            return RuntimePreviewAdapterResult.Ok(previewMode, request.BuffId, targetId, snapshot);
        }

        public RuntimePreviewAdapterResult Tick(int frames, string targetId)
        {
            if (frames < 0)
                return Fail(PreviewError.InvalidParams, "invalid_tick_count", "frames must be greater than or equal to zero.", PreviewMode, string.Empty, targetId);

            if (frames > 0)
                _world.Tick(frames);

            string resolvedTargetId = string.IsNullOrEmpty(targetId) ? "TestTarget" : targetId;
            return RuntimePreviewAdapterResult.Ok(PreviewMode, string.Empty, resolvedTargetId, Snapshot(resolvedTargetId));
        }

        public RuntimePreviewAdapterResult Reset(bool reloadBase)
        {
            string previewMode = PreviewMode;
            _world.Reset(reloadBase);
            _logs.Append("info", $"RuntimePreviewAdapter: reset previewMode={previewMode} reloadBase={reloadBase}");
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
    }

    public interface IRuntimePreviewModeSource
    {
        string PreviewMode { get; }
        string FallbackReason { get; }
    }

    public interface IRuntimePreviewConfigMetadataSource
    {
        RuntimePreviewConfigMetadata CurrentConfigMetadata { get; }
    }

    public interface IRuntimePreviewFailureSource
    {
        string LastFailureReason { get; }
        string LastFailureMessage { get; }
    }

    public sealed class RuntimePreviewApplyBuffRequest
    {
        public string BuffId;
        public string CasterId;
        public string TargetId;
        public int Stack = 1;
        public long? DurationOverrideMs;
        public int WaitTicks;
    }

    public sealed class RuntimePreviewAdapterResult
    {
        public bool Success { get; private set; }
        public int ErrorCode { get; private set; }
        public string ErrorReason { get; private set; }
        public string ErrorMessage { get; private set; }
        public string PreviewMode { get; private set; }
        public string BuffId { get; private set; }
        public string TargetId { get; private set; }
        public RuntimePreviewSnapshot Snapshot { get; private set; }

        public static RuntimePreviewAdapterResult Ok(
            string previewMode,
            string buffId,
            string targetId,
            RuntimePreviewSnapshot snapshot)
        {
            return new RuntimePreviewAdapterResult
            {
                Success = true,
                PreviewMode = previewMode ?? string.Empty,
                BuffId = buffId ?? string.Empty,
                TargetId = targetId ?? string.Empty,
                Snapshot = snapshot ?? RuntimePreviewSnapshot.Empty(previewMode),
            };
        }

        public static RuntimePreviewAdapterResult Fail(
            int code,
            string reason,
            string message,
            string previewMode,
            string buffId,
            string targetId)
        {
            return new RuntimePreviewAdapterResult
            {
                Success = false,
                ErrorCode = code,
                ErrorReason = reason ?? string.Empty,
                ErrorMessage = message ?? string.Empty,
                PreviewMode = previewMode ?? string.Empty,
                BuffId = buffId ?? string.Empty,
                TargetId = targetId ?? string.Empty,
                Snapshot = RuntimePreviewSnapshot.Empty(previewMode),
            };
        }
    }

    public sealed class RuntimePreviewSnapshot
    {
        public RuntimePreviewSnapshot(string previewMode, string targetId)
        {
            PreviewMode = previewMode ?? string.Empty;
            TargetId = targetId ?? string.Empty;
        }

        public string PreviewMode { get; }
        public string TargetId { get; }
        public RuntimePreviewConfigMetadata ConfigMetadata { get; set; } = new RuntimePreviewConfigMetadata();
        public List<BuffSnapshot> BuffSnapshots { get; } = new List<BuffSnapshot>();
        public List<AttributeChange> AttributeChanges { get; } = new List<AttributeChange>();
        public List<DamageTick> DamageTicks { get; } = new List<DamageTick>();
        public List<StatusChange> StatusChanges { get; } = new List<StatusChange>();

        public static RuntimePreviewSnapshot Empty(string previewMode)
        {
            return new RuntimePreviewSnapshot(previewMode, string.Empty);
        }
    }
}

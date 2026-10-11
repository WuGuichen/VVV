using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MxFramework.Network;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestReplayCapture
    {
        private readonly RuntimeCustomState _bootstrapSnapshot;
        private readonly RuntimeReplaySnapshot _runtimeReplaySnapshot;
        private readonly RuntimeCommand[] _authoritativeCommands;
        private readonly ReadOnlyCollection<RuntimeCommand> _authoritativeCommandsView;
        private readonly Func<GameSlice> _replaySliceFactory;
        private readonly RuntimeCustomState _sessionAuthorityBootstrapSnapshot;
        private readonly Func<INetworkSnapshotParticipant> _sessionAuthorityParticipantFactory;
        private readonly CharacterTestReplaySessionAuthorityIdentity _sessionAuthorityIdentity;

        public CharacterTestReplayCapture(
            RuntimeReplaySnapshot runtimeReplaySnapshot,
            RuntimeCustomState bootstrapSnapshot,
            int tickRate,
            NetworkStateSyncPolicy stateSyncPolicy,
            IReadOnlyList<RuntimeCommand> authoritativeCommands,
            Func<GameSlice> replaySliceFactory = null,
            RuntimeCustomState sessionAuthorityBootstrapSnapshot = null,
            long sessionAuthorityHash = 0L)
            : this(
                runtimeReplaySnapshot,
                bootstrapSnapshot,
                tickRate,
                stateSyncPolicy,
                authoritativeCommands,
                replaySliceFactory,
                sessionAuthorityBootstrapSnapshot,
                sessionAuthorityHash,
                null,
                null,
                null)
        {
        }

        internal CharacterTestReplayCapture(
            RuntimeReplaySnapshot runtimeReplaySnapshot,
            RuntimeCustomState bootstrapSnapshot,
            int tickRate,
            NetworkStateSyncPolicy stateSyncPolicy,
            IReadOnlyList<RuntimeCommand> authoritativeCommands,
            Func<GameSlice> replaySliceFactory,
            RuntimeCustomState sessionAuthorityBootstrapSnapshot,
            long sessionAuthorityHash,
            Func<INetworkSnapshotParticipant> sessionAuthorityParticipantFactory,
            CharacterTestReplaySessionAuthorityIdentity sessionAuthorityIdentity = null,
            DateTime? recordedAtUtc = null)
        {
            _runtimeReplaySnapshot = runtimeReplaySnapshot
                ?? new RuntimeReplaySnapshot(
                    new RuntimeReplayHeader(
                        CharacterTestReplayRecorder.SchemaVersion,
                        CharacterTestReplayRecorder.FrameworkVersion,
                        string.Empty,
                        string.Empty,
                        RuntimeFrame.Zero),
                    Array.Empty<RuntimeReplayFrameRecord>());
            _bootstrapSnapshot = bootstrapSnapshot?.PreparePayloadDigest();
            FrameworkVersionDigest = RuntimeHashDigest.ComputeContentDigest(_runtimeReplaySnapshot.Header.FrameworkVersion);
            ConfigDigest = RuntimeHashDigest.ComputeContentDigest(_runtimeReplaySnapshot.Header.ConfigHash);
            ResourceCatalogDigest = RuntimePreparedDigest.PrepareExisting(_runtimeReplaySnapshot.Header.ResourceCatalogHash);
            GameplayBootstrapDigest = _bootstrapSnapshot != null
                ? _bootstrapSnapshot.PreparedPayloadDigest : RuntimeHashDigest.ComputeContentDigest(null);
            TickRate = Math.Max(1, tickRate);
            StateSyncPolicy = stateSyncPolicy;
            _authoritativeCommands = CopyCommands(authoritativeCommands);
            _authoritativeCommandsView = Array.AsReadOnly(_authoritativeCommands);
            _replaySliceFactory = replaySliceFactory;
            _sessionAuthorityBootstrapSnapshot = sessionAuthorityBootstrapSnapshot?.PreparePayloadDigest();
            AuthorityBootstrapDigest = _sessionAuthorityBootstrapSnapshot != null
                ? _sessionAuthorityBootstrapSnapshot.PreparedPayloadDigest : RuntimeHashDigest.ComputeContentDigest(null);
            SessionAuthorityHash = sessionAuthorityHash;
            _sessionAuthorityParticipantFactory = sessionAuthorityParticipantFactory;
            _sessionAuthorityIdentity = sessionAuthorityIdentity;
            RecordedAtUtc = recordedAtUtc;
        }

        public RuntimeReplaySnapshot RuntimeReplaySnapshot => _runtimeReplaySnapshot;
        internal RuntimeHashDigest FrameworkVersionDigest { get; }
        internal RuntimeHashDigest ConfigDigest { get; }
        internal RuntimePreparedDigest ResourceCatalogDigest { get; }
        internal RuntimeHashDigest GameplayBootstrapDigest { get; }
        internal RuntimeHashDigest AuthorityBootstrapDigest { get; }
        public RuntimeCustomState BootstrapSnapshot => _bootstrapSnapshot;
        public Func<GameSlice> ReplaySliceFactory => _replaySliceFactory;
        public int TickRate { get; }
        public NetworkStateSyncPolicy StateSyncPolicy { get; }
        public IReadOnlyList<RuntimeCommand> AuthoritativeCommands => _authoritativeCommandsView;
        public int FrameCount => _runtimeReplaySnapshot.Count;
        public int AuthoritativeCommandCount => _authoritativeCommands.Length;
        public bool HasBootstrapSnapshot => _bootstrapSnapshot != null;
        public RuntimeCustomState SessionAuthorityBootstrapSnapshot =>
            _sessionAuthorityBootstrapSnapshot;
        public long SessionAuthorityHash { get; }
        public bool HasSessionAuthorityBootstrap => _sessionAuthorityBootstrapSnapshot != null;
        internal CharacterTestReplaySessionAuthorityIdentity SessionAuthorityIdentity =>
            _sessionAuthorityIdentity;
        internal DateTime? RecordedAtUtc { get; }
        internal Func<INetworkSnapshotParticipant> SessionAuthorityParticipantFactory =>
            _sessionAuthorityParticipantFactory;

        public bool HasZeroControlIntentAfter(RuntimeFrame minFrameInclusive, int targetEntityId)
        {
            for (int i = 0; i < _authoritativeCommands.Length; i++)
            {
                RuntimeCommand command = _authoritativeCommands[i];
                if (command.Frame < minFrameInclusive)
                    continue;
                if (targetEntityId > 0 && command.TargetId != targetEntityId)
                    continue;
                if (CharacterTestReplayDiagnostics.IsZeroControlIntent(command))
                    return true;
            }

            return false;
        }

        private static RuntimeCommand[] CopyCommands(IReadOnlyList<RuntimeCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return Array.Empty<RuntimeCommand>();

            var copy = new RuntimeCommand[commands.Count];
            for (int i = 0; i < commands.Count; i++)
                copy[i] = commands[i];
            return copy;
        }

        private static RuntimeCustomState CloneCustomState(RuntimeCustomState state)
        {
            return state == null
                ? null
                : state.Copy();
        }
    }

    public sealed class CharacterTestReplayRecorder
    {
        public const int SchemaVersion = 7;
        public const int LegacyRootDistanceSchemaVersion = 3;
        public const int LegacyRemovedCancelSchemaVersion = 1;
        public const int LegacyCharacterActionSchemaVersion = 2;
        public const string FrameworkVersion = "character-test-replay-v0.7";

        private readonly NetworkSessionConfig _config;
        private readonly List<RuntimeCommand> _authoritativeCommands = new List<RuntimeCommand>();
        private RuntimeReplayRecorder _recorder;
        private RuntimeCustomState _bootstrapSnapshot;
        private RuntimeCustomState _sessionAuthorityBootstrapSnapshot;
        private long _sessionAuthorityHash;
        private Func<INetworkSnapshotParticipant> _sessionAuthorityParticipantFactory;
        private CharacterTestReplaySessionAuthorityIdentity _sessionAuthorityIdentity;
        private DateTime? _recordedAtUtc;
        private RuntimeFrame _startFrame;
        private bool _recording;

        public CharacterTestReplayRecorder(NetworkSessionConfig config)
        {
            _config = CharacterTestSessionHashContract.Normalize(config);
            _startFrame = RuntimeFrame.Zero;
            _recorder = CreateRecorder(_startFrame);
        }

        public bool IsRecording => _recording;
        public int FrameCount => _recorder.Count;
        public int AuthoritativeCommandCount => _authoritativeCommands.Count;
        public RuntimeFrame StartFrame => _startFrame;

        public CharacterTestReplayCapture Begin(
            RuntimeFrame startFrame,
            RuntimeCustomState bootstrapSnapshot,
            RuntimeCustomState sessionAuthorityBootstrapSnapshot = null,
            long sessionAuthorityHash = 0L)
        {
            return BeginInternal(
                startFrame,
                bootstrapSnapshot,
                sessionAuthorityBootstrapSnapshot,
                sessionAuthorityHash,
                null);
        }

        internal CharacterTestReplayCapture Begin(
            RuntimeFrame startFrame,
            RuntimeCustomState bootstrapSnapshot,
            RuntimeCustomState sessionAuthorityBootstrapSnapshot,
            long sessionAuthorityHash,
            Func<INetworkSnapshotParticipant> sessionAuthorityParticipantFactory,
            CharacterTestReplaySessionAuthorityIdentity sessionAuthorityIdentity = null)
        {
            return BeginInternal(
                startFrame,
                bootstrapSnapshot,
                sessionAuthorityBootstrapSnapshot,
                sessionAuthorityHash,
                sessionAuthorityParticipantFactory,
                sessionAuthorityIdentity);
        }

        private CharacterTestReplayCapture BeginInternal(
            RuntimeFrame startFrame,
            RuntimeCustomState bootstrapSnapshot,
            RuntimeCustomState sessionAuthorityBootstrapSnapshot,
            long sessionAuthorityHash,
            Func<INetworkSnapshotParticipant> sessionAuthorityParticipantFactory,
            CharacterTestReplaySessionAuthorityIdentity sessionAuthorityIdentity = null)
        {
            _startFrame = startFrame;
            _bootstrapSnapshot = CloneCustomState(bootstrapSnapshot);
            _sessionAuthorityBootstrapSnapshot = CloneCustomState(sessionAuthorityBootstrapSnapshot);
            _sessionAuthorityHash = sessionAuthorityHash;
            _sessionAuthorityParticipantFactory = sessionAuthorityParticipantFactory;
            _sessionAuthorityIdentity = sessionAuthorityIdentity;
            _recordedAtUtc = DateTime.UtcNow;
            _recorder = CreateRecorder(startFrame);
            _authoritativeCommands.Clear();
            _recording = true;
            return CreateCapture();
        }

        public void RecordAuthoritativeCommands(NetworkAuthoritativeCommandPacket packet)
        {
            if (!_recording || packet == null || packet.Commands == null || packet.Commands.Count == 0)
                return;

            for (int i = 0; i < packet.Commands.Count; i++)
            {
                RuntimeCommand command = packet.Commands[i];
                if (command.Frame >= _startFrame)
                    _authoritativeCommands.Add(command);
            }
        }

        private int _summaryCharacterCount = -1;
        private Func<RuntimeReplayFrameRecord, string> _summaryFormatter;

        internal void PrepareCaptureCapacity(int frameCapacity, int commandCapacity, int characterCount)
        {
            _recorder.PrepareCapacity(frameCapacity, commandCapacity);
            _summaryCharacterCount = characterCount;
            _summaryFormatter = CreateSummaryFormatter(characterCount);
        }

        private static Func<RuntimeReplayFrameRecord, string> CreateSummaryFormatter(int characterCount)
            => record => CharacterTestReplayDiagnostics.CreateFrameSummary(record.Frame, record.Commands.Count, characterCount, record.ResultHash);

        internal void RecordFrame(RuntimeFrame frame, IReadOnlyList<RuntimeCommand> commands, long resultHash, int characterCount)
        {
            if (!_recording || frame < _startFrame) return;
            if (_summaryFormatter == null || _summaryCharacterCount != characterCount)
            {
                _summaryCharacterCount = characterCount;
                _summaryFormatter = CreateSummaryFormatter(characterCount);
            }
            _recorder.RecordFrameDeferred(frame, commands, resultHash, _summaryFormatter);
        }

        public void RecordFrame(
            RuntimeFrame frame,
            IReadOnlyList<RuntimeCommand> drainedCommands,
            long resultHash,
            string diagnosticsSummary)
        {
            if (!_recording)
                return;
            if (frame < _startFrame)
                return;

            _recorder.RecordFrame(
                frame,
                drainedCommands,
                resultHash,
                diagnosticsSummary);
        }

        public CharacterTestReplayCapture CreateCapture(Func<GameSlice> replaySliceFactory = null)
        {
            RuntimeReplaySnapshot snapshot = _recorder.CreateSnapshot();
            return new CharacterTestReplayCapture(
                snapshot,
                CloneCustomState(_bootstrapSnapshot),
                _config.TickRate,
                _config.StateSyncPolicy,
                CreateAuthoritativeCommandSlice(snapshot),
                replaySliceFactory,
                CloneCustomState(_sessionAuthorityBootstrapSnapshot),
                _sessionAuthorityHash,
                _sessionAuthorityParticipantFactory,
                _sessionAuthorityIdentity,
                _recordedAtUtc);
        }

        private RuntimeCommand[] CreateAuthoritativeCommandSlice(RuntimeReplaySnapshot snapshot)
        {
            if (snapshot == null || snapshot.Count == 0 || _authoritativeCommands.Count == 0)
                return Array.Empty<RuntimeCommand>();
            RuntimeFrame endFrame = snapshot.Records[snapshot.Count - 1].Frame;
            int count = 0;
            for (int i = 0; i < _authoritativeCommands.Count; i++)
            {
                RuntimeCommand command = _authoritativeCommands[i];
                if (command.Frame >= _startFrame && command.Frame <= endFrame)
                    count++;
            }
            if (count == 0)
                return Array.Empty<RuntimeCommand>();
            var commands = new RuntimeCommand[count];
            int writeIndex = 0;
            for (int i = 0; i < _authoritativeCommands.Count; i++)
            {
                RuntimeCommand command = _authoritativeCommands[i];
                if (command.Frame >= _startFrame && command.Frame <= endFrame)
                    commands[writeIndex++] = command;
            }
            return commands;
        }

        private RuntimeReplayRecorder CreateRecorder(RuntimeFrame startFrame)
        {
            return new RuntimeReplayRecorder(new RuntimeReplayHeader(
                SchemaVersion,
                FrameworkVersion
                    + ";tickRate="
                    + _config.TickRate
                    + ";inputDelay="
                    + _config.InputDelayFrames
                    + ";stateSyncPolicy="
                    + _config.StateSyncPolicy
                    + ";bootstrap=InitialSceneBaseline"
                    + ";randomSeed=NoRandomSeed",
                _config.ConfigHash,
                _config.ResourceCatalogHash,
                startFrame));
        }

        private static RuntimeCustomState CloneCustomState(RuntimeCustomState state)
        {
            return state == null
                ? null
                : state.Copy();
        }
    }

    public sealed class CharacterTestReplayFrameDriver : IRuntimeReplayFrameDriver, IDisposable
    {
        private readonly RuntimeCustomState _bootstrapSnapshot;
        private readonly int _tickRate;
        private readonly Func<GameSlice> _sliceFactory;
        private readonly RuntimeCustomState _sessionAuthorityBootstrapSnapshot;
        private readonly Func<INetworkSnapshotParticipant> _sessionAuthorityParticipantFactory;
        private readonly long _expectedSessionAuthorityHash;
        private readonly CharacterTestReplaySessionAuthorityIdentity _expectedSessionAuthorityIdentity;
        private readonly bool _hasFrameRecords;
        private long _sessionAuthorityHash;
        private GameSlice _slice;
        private CharacterTestNetworkSnapshotParticipant _snapshotParticipant;
        private CharacterTestRuntimeAiPlannerSessionAuthorityParticipant _sessionAuthorityParticipant;
        private bool _disposed;

        public CharacterTestReplayFrameDriver(CharacterTestReplayCapture capture)
            : this(capture, null)
        {
        }

        public CharacterTestReplayFrameDriver(
            CharacterTestReplayCapture capture,
            Func<GameSlice> sliceFactory)
        {
            if (capture == null)
                throw new ArgumentNullException(nameof(capture));

            _bootstrapSnapshot = CloneCustomState(capture.BootstrapSnapshot);
            _tickRate = Math.Max(1, capture.TickRate);
            _sliceFactory = sliceFactory ?? capture.ReplaySliceFactory;
            _sessionAuthorityBootstrapSnapshot = CloneCustomState(
                capture.SessionAuthorityBootstrapSnapshot);
            _sessionAuthorityParticipantFactory = capture.SessionAuthorityParticipantFactory;
            _expectedSessionAuthorityHash = capture.SessionAuthorityHash;
            _expectedSessionAuthorityIdentity = capture.SessionAuthorityIdentity;
            _hasFrameRecords = capture.FrameCount > 0;
        }

        public GameSlice Slice => _slice;

        private static void ValidateReplaySchema(RuntimeReplayHeader header)
        {
            if (header.SchemaVersion != CharacterTestReplayRecorder.SchemaVersion)
            {
                throw new NotSupportedException(
                    header.SchemaVersion == CharacterTestReplayRecorder.LegacyRootDistanceSchemaVersion
                        ? "Replay schema v3 is unsupported because target.distance used legacy root-distance semantics."
                    : header.SchemaVersion == CharacterTestReplayRecorder.LegacyCharacterActionSchemaVersion
                        ? "Replay schema v2 is unsupported because it stores legacy string CharacterAction IDs."
                        : header.SchemaVersion == CharacterTestReplayRecorder.LegacyRemovedCancelSchemaVersion
                            ? "Replay schema v1 is unsupported because it contains the removed CharacterAction cancel semantics."
                        : "CharacterTest replay schema v" + header.SchemaVersion
                            + " is unsupported; expected v7 with the fixed session hash layout.");
            }
        }

        public void Reset(RuntimeReplayHeader header)
        {
            ValidateReplaySchema(header);
            // Preserve the legacy Runtime replay contract: an empty recording is a valid
            // no-op playback. The F3 validation kernel rejects it during Inspect instead.
            if (!_hasFrameRecords)
            {
                ThrowIfDisposed();
                DisposeSlice();
                return;
            }
            CharacterTestReplayBootstrapPhaseResult authority =
                BeginResetAndRestoreSessionAuthority(header);
            if (!authority.Success)
                throw new InvalidOperationException(authority.Code + ": " + authority.Message);
            CharacterTestReplayBootstrapPhaseResult gameplay = RestoreGameplayBootstrap(header);
            if (!gameplay.Success)
                throw new InvalidOperationException(gameplay.Code + ": " + gameplay.Message);
        }

        internal CharacterTestReplayBootstrapPhaseResult BeginResetAndRestoreSessionAuthority(
            RuntimeReplayHeader header)
        {
            ValidateReplaySchema(header);
            ThrowIfDisposed();
            DisposeSlice();
            try
            {
                _slice = _sliceFactory != null
                    ? _sliceFactory()
                    : new GameSlice(new CharacterTestSliceOptions
                    {
                        Logger = NullRuntimeLogger.Instance,
                        CreateCharacterViews = false,
                        EnableLocalInputCommands = false
                    });
                _slice.Services.Commands.SetRuntimeCommandSubmitter(AcceptDeferredReplayCommand);
                _snapshotParticipant = _slice.Services.Network.CreateSnapshotParticipant();
            }
            catch (Exception exception)
            {
                DisposeSlice();
                return CharacterTestReplayBootstrapPhaseResult.Failed(
                    CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap,
                    CharacterTestReplayValidationCodes.SessionAuthorityRestoreFailed,
                    exception.Message);
            }

            return TryRestoreSessionAuthorityBootstrap(header.StartFrame);
        }

        internal CharacterTestReplayBootstrapPhaseResult RestoreGameplayBootstrap(
            RuntimeReplayHeader header)
        {
            ValidateReplaySchema(header);
            ThrowIfDisposed();
            if (_slice == null || _snapshotParticipant == null)
            {
                return CharacterTestReplayBootstrapPhaseResult.Failed(
                    CharacterTestReplayValidationState.RestoringGameplayBootstrap,
                    CharacterTestReplayValidationCodes.GameplayBootstrapRestoreFailed,
                    "Replay session-authority bootstrap has not completed.");
            }
            if (_bootstrapSnapshot == null)
            {
                return CharacterTestReplayBootstrapPhaseResult.Failed(
                    CharacterTestReplayValidationState.RestoringGameplayBootstrap,
                    CharacterTestReplayValidationCodes.GameplayBootstrapMissing,
                    "CharacterTest replay gameplay bootstrap is missing.");
            }

            try
            {
                // Reject unsupported ledger/journal authority before replay catch-up can tick the world.
                NetworkSnapshotApplyResult preflight = _snapshotParticipant.PreflightFixedAssemblyRollback(_bootstrapSnapshot);
                if (!preflight.Success)
                    return CharacterTestReplayBootstrapPhaseResult.Failed(
                        CharacterTestReplayValidationState.RestoringGameplayBootstrap,
                        CharacterTestReplayValidationCodes.GameplayBootstrapRestoreFailed,
                        preflight.Message);
                AdvanceToFrame(header.StartFrame);
                NetworkSnapshotApplyResult result =
                    _snapshotParticipant.ApplyRollbackSnapshot(
                        header.StartFrame,
                        _bootstrapSnapshot,
                        preserveLocalPredictedEntity: false);
                return result.Success
                    ? CharacterTestReplayBootstrapPhaseResult.Passed(
                        CharacterTestReplayValidationState.RestoringGameplayBootstrap)
                    : CharacterTestReplayBootstrapPhaseResult.Failed(
                        CharacterTestReplayValidationState.RestoringGameplayBootstrap,
                        CharacterTestReplayValidationCodes.GameplayBootstrapRestoreFailed,
                        result.Message);
            }
            catch (Exception exception)
            {
                return CharacterTestReplayBootstrapPhaseResult.Failed(
                    CharacterTestReplayValidationState.RestoringGameplayBootstrap,
                    CharacterTestReplayValidationCodes.GameplayBootstrapRestoreFailed,
                    exception.Message);
            }
        }

        public RuntimeReplayPlaybackFrameResult RunFrame(RuntimeReplayFrameRecord record)
        {
            ThrowIfDisposed();
            if (_slice == null)
                Reset(record != null
                    ? new RuntimeReplayHeader(
                        CharacterTestReplayRecorder.SchemaVersion,
                        CharacterTestReplayRecorder.FrameworkVersion,
                        string.Empty,
                        string.Empty,
                        record.Frame)
                    : new RuntimeReplayHeader(
                        CharacterTestReplayRecorder.SchemaVersion,
                        CharacterTestReplayRecorder.FrameworkVersion,
                        string.Empty,
                        string.Empty,
                        RuntimeFrame.Zero));

            if (record == null)
            {
                return new RuntimeReplayPlaybackFrameResult(
                    RuntimeFrame.Zero,
                    0L,
                    "CharacterTest replay record is null.",
                    new[]
                    {
                        new RuntimeCommandError(
                            RuntimeCommandErrorCode.InvalidPayload,
                            default,
                            _slice.CurrentFrame,
                            "CharacterTest replay record is null.")
                    });
            }

            AdvanceToFrame(record.Frame);

            var errors = new List<RuntimeCommandError>();
            for (int i = 0; i < record.Commands.Count; i++)
            {
                RuntimeCommandValidationResult result =
                    _slice.Services.Modules.RuntimeCommands.Enqueue(record.Commands[i]);
                if (!result.Success)
                    errors.Add(result.Error);
            }

            _slice.Tick(ResolveDeltaTime());
            long hash = _snapshotParticipant.ComputeStateHash(record.Frame);
            if (_sessionAuthorityParticipant != null)
            {
                hash = CharacterTestRuntimeAiPlannerSessionAuthorityHash.CombineGameplayStateHash(
                    hash,
                    _sessionAuthorityHash);
            }
            string diagnostics = CharacterTestReplayDiagnostics.CreateFrameSummary(
                record.Frame,
                record.Commands.Count,
                _slice.Services.Modules.CharacterEntities.Count,
                hash);
            return new RuntimeReplayPlaybackFrameResult(record.Frame, hash, diagnostics, errors);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            DisposeSlice();
            _disposed = true;
        }

        private void AdvanceToFrame(RuntimeFrame frame)
        {
            while (_slice.CurrentFrame < frame)
                _slice.Tick(ResolveDeltaTime());
        }

        private double ResolveDeltaTime()
        {
            return 1d / Math.Max(1, _tickRate);
        }

        private static RuntimeCommandValidationResult AcceptDeferredReplayCommand(RuntimeCommand command)
        {
            // The authority recording already contains the command at the frame where the
            // network session accepted it. Re-enqueuing an autonomous planner submission here
            // would apply it one frame too early and then apply the recorded command again.
            return RuntimeCommandValidationResult.Accepted(command);
        }

        private void DisposeSlice()
        {
            _snapshotParticipant = null;
            _sessionAuthorityParticipant = null;
            _sessionAuthorityHash = 0L;
            _slice?.Dispose();
            _slice = null;
        }

        private CharacterTestReplayBootstrapPhaseResult TryRestoreSessionAuthorityBootstrap(
            RuntimeFrame frame)
        {
            _sessionAuthorityParticipant = null;
            _sessionAuthorityHash = 0L;
            if (_sessionAuthorityBootstrapSnapshot == null)
            {
                if (_expectedSessionAuthorityHash != 0L)
                {
                    return CharacterTestReplayBootstrapPhaseResult.Failed(
                        CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap,
                        CharacterTestReplayValidationCodes.SessionAuthorityBootstrapMissing,
                        "CharacterTest replay session authority bootstrap is missing.");
                }
                return CharacterTestReplayBootstrapPhaseResult.Passed(
                    CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap);
            }
            if (_sessionAuthorityParticipantFactory == null)
            {
                return CharacterTestReplayBootstrapPhaseResult.Failed(
                    CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap,
                    CharacterTestReplayValidationCodes.SessionAuthorityRestoreFailed,
                    "CharacterTest replay session authority participant factory is missing.");
            }

            INetworkSnapshotParticipant participant;
            try
            {
                participant = _sessionAuthorityParticipantFactory();
            }
            catch (Exception exception)
            {
                return CharacterTestReplayBootstrapPhaseResult.Failed(
                    CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap,
                    CharacterTestReplayValidationCodes.SessionAuthorityRestoreFailed,
                    exception.Message);
            }
            _sessionAuthorityParticipant =
                participant as CharacterTestRuntimeAiPlannerSessionAuthorityParticipant;
            if (_sessionAuthorityParticipant == null
                || !string.Equals(
                    participant.ParticipantId,
                    CharacterTestRuntimeAiPlannerSessionAuthorityParticipant.ParticipantIdValue,
                    StringComparison.Ordinal))
            {
                return CharacterTestReplayBootstrapPhaseResult.Failed(
                    CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap,
                    CharacterTestReplayValidationCodes.SessionAuthorityRestoreFailed,
                    "CharacterTest replay session authority participant factory returned an invalid participant.");
            }

            NetworkSnapshotApplyResult result = participant.ApplySnapshot(
                frame,
                _sessionAuthorityBootstrapSnapshot);
            if (!result.Success)
            {
                return CharacterTestReplayBootstrapPhaseResult.Failed(
                    CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap,
                    CharacterTestReplayValidationCodes.SessionAuthorityRestoreFailed,
                    result.Message);
            }

            _sessionAuthorityHash = _sessionAuthorityParticipant.AuthorityHash;
            if (_sessionAuthorityHash != _expectedSessionAuthorityHash)
            {
                return CharacterTestReplayBootstrapPhaseResult.Failed(
                    CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap,
                    CharacterTestReplayValidationCodes.SessionAuthorityHashMismatch,
                    "CharacterTest replay session authority bootstrap hash mismatch.");
            }

            if (_expectedSessionAuthorityIdentity == null)
            {
                return CharacterTestReplayBootstrapPhaseResult.Failed(
                    CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap,
                    CharacterTestReplayValidationCodes.CatalogIdentityMissing,
                    "CharacterTest replay catalog identity is missing.");
            }
            CharacterTestReplaySessionAuthorityIdentity restoredIdentity =
                _sessionAuthorityParticipant.CaptureReplayIdentity();
            if (!_expectedSessionAuthorityIdentity.Matches(restoredIdentity))
            {
                bool catalogMismatch = !string.Equals(
                        _expectedSessionAuthorityIdentity.CatalogId,
                        restoredIdentity.CatalogId,
                        StringComparison.Ordinal)
                    || _expectedSessionAuthorityIdentity.CatalogRevision != restoredIdentity.CatalogRevision
                    || _expectedSessionAuthorityIdentity.CatalogHash != restoredIdentity.CatalogHash
                    || _expectedSessionAuthorityIdentity.RuntimeContractVersion
                        != restoredIdentity.RuntimeContractVersion;
                return CharacterTestReplayBootstrapPhaseResult.Failed(
                    CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap,
                    catalogMismatch
                        ? CharacterTestReplayValidationCodes.CatalogHashMismatch
                        : CharacterTestReplayValidationCodes.SessionAuthorityHashMismatch,
                    catalogMismatch
                        ? "CharacterTest replay catalog identity mismatch."
                        : "CharacterTest replay pinned deck identity mismatch.");
            }
            return CharacterTestReplayBootstrapPhaseResult.Passed(
                CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterTestReplayFrameDriver));
        }

        private static RuntimeCustomState CloneCustomState(RuntimeCustomState state)
        {
            return state == null
                ? null
                : state.Copy();
        }
    }

    public sealed class CharacterTestReplayPlaybackStatus
    {
        public bool Available { get; set; }
        public bool Recording { get; set; }
        public bool PlaybackRan { get; set; }
        public bool Success { get; set; }
        public string ResultCode { get; set; } = "UNAVAILABLE";
        public string Summary { get; set; } = "CharacterTest replay unavailable.";
        public int FrameCount { get; set; }
        public int AuthoritativeCommandCount { get; set; }
        public long ExpectedResultHash { get; set; }
        public long ActualResultHash { get; set; }
        public RuntimeFrame FailureFrame { get; set; } = RuntimeFrame.Zero;

        public static CharacterTestReplayPlaybackStatus Unavailable(string summary)
        {
            return new CharacterTestReplayPlaybackStatus
            {
                Available = false,
                ResultCode = "UNAVAILABLE",
                Summary = string.IsNullOrWhiteSpace(summary)
                    ? "CharacterTest replay unavailable."
                    : summary.Trim()
            };
        }

        public static CharacterTestReplayPlaybackStatus RecordingStarted(CharacterTestReplayCapture capture)
        {
            return new CharacterTestReplayPlaybackStatus
            {
                Available = capture != null,
                Recording = true,
                ResultCode = capture != null ? "RECORDING" : "UNAVAILABLE",
                Summary = capture != null
                    ? "CharacterTest replay recording started. startFrame="
                        + capture.RuntimeReplaySnapshot.Header.StartFrame.Value
                    : "CharacterTest replay unavailable.",
                FrameCount = capture != null ? capture.FrameCount : 0,
                AuthoritativeCommandCount = capture != null ? capture.AuthoritativeCommandCount : 0
            };
        }

        public static CharacterTestReplayPlaybackStatus FromPlayback(
            CharacterTestReplayCapture capture,
            RuntimeReplayPlaybackResult result)
        {
            if (capture == null)
                return Unavailable("CharacterTest replay capture is unavailable.");
            if (result == null)
                return Unavailable("CharacterTest replay playback did not run.");

            return new CharacterTestReplayPlaybackStatus
            {
                Available = true,
                Recording = false,
                PlaybackRan = true,
                Success = result.Success,
                ResultCode = result.Success ? "PASS" : result.FailureCode.ToString(),
                Summary = "CharacterTest replay "
                    + (result.Success ? "PASS" : "FAIL")
                    + ": frames="
                    + capture.FrameCount
                    + " authCommands="
                    + capture.AuthoritativeCommandCount
                    + (result.Success ? string.Empty : " " + result.FailureMessage),
                FrameCount = capture.FrameCount,
                AuthoritativeCommandCount = capture.AuthoritativeCommandCount,
                ExpectedResultHash = result.ExpectedResultHash,
                ActualResultHash = result.ActualResultHash,
                FailureFrame = result.FailureFrame
            };
        }

        internal static CharacterTestReplayPlaybackStatus FromValidation(
            CharacterTestReplayCapture capture,
            CharacterTestReplayValidationResult result)
        {
            if (capture == null || result == null)
                return Unavailable("CharacterTest replay validation did not run.");
            return new CharacterTestReplayPlaybackStatus
            {
                Available = true,
                Recording = false,
                PlaybackRan = result.State != CharacterTestReplayValidationState.Empty,
                Success = result.Success,
                ResultCode = result.Code,
                Summary = result.Summary,
                FrameCount = capture.FrameCount,
                AuthoritativeCommandCount = capture.AuthoritativeCommandCount,
                ExpectedResultHash = result.ExpectedHash,
                ActualResultHash = result.ActualHash,
                FailureFrame = result.FailureFrame
            };
        }
    }

    public static class CharacterTestReplayPlayback
    {
        public static RuntimeReplayPlaybackResult Play(CharacterTestReplayCapture capture)
        {
            if (capture == null)
                throw new ArgumentNullException(nameof(capture));

            using (var driver = new CharacterTestReplayFrameDriver(capture))
            {
                return new RuntimeReplayPlaybackRunner()
                    .Play(capture.RuntimeReplaySnapshot, driver);
            }
        }

        public static CharacterTestReplayPlaybackStatus Validate(CharacterTestReplayCapture capture)
        {
            if (capture == null)
                return CharacterTestReplayPlaybackStatus.Unavailable("CharacterTest replay capture is unavailable.");
            using (var kernel = new CharacterTestReplayValidationComparisonKernel(capture))
            {
                return CharacterTestReplayPlaybackStatus.FromValidation(
                    capture,
                    kernel.RunToCompletion());
            }
        }
    }

    public static class CharacterTestReplayDiagnostics
    {
        public static bool IsZeroControlIntent(RuntimeCommand command)
        {
            if (command.CommandId != CharacterTestRuntimeCommandIds.SetControlIntent)
                return false;

            CharacterTestControlIntent intent = CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
            return ApproximatelyZero(intent.Move)
                && ApproximatelyZero(intent.WorldMove)
                && !intent.JumpPressed
                && !intent.JumpHeld
                && !intent.JumpReleased
                && !intent.AttackPrimaryPressed
                && !intent.AttackSecondaryPressed
                && !intent.DodgePressed
                && !intent.DebugPrimaryPressed
                && !intent.DebugSecondaryPressed
                && intent.SkillSlotPressedMask == 0
                && !intent.TurnInPlace;
        }

        public static string CreateFrameSummary(
            RuntimeFrame frame,
            int commandCount,
            int characterCount,
            long resultHash)
        {
            return "frame="
                + frame.Value
                + " commands="
                + commandCount
                + " characters="
                + characterCount
                + " hash="
                + resultHash;
        }

        private static bool ApproximatelyZero(Vector2 value)
        {
            return Math.Abs(value.x) <= 0.0001f && Math.Abs(value.y) <= 0.0001f;
        }
    }
}

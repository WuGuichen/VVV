using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Network
{
    public interface INetworkRollbackParticipant
    {
        string ParticipantId { get; }
        object CaptureRollbackState(RuntimeFrame frame);
        bool RestoreRollbackState(RuntimeFrame frame, object state, out string failure);
    }

    public delegate bool NetworkPredictionCommandSink(RuntimeCommand command, out string failure);

    public delegate bool NetworkPredictionReplayFrameHandler(RuntimeFrame frame, out string failure);

    public readonly struct NetworkPredictionRollbackResult
    {
        private NetworkPredictionRollbackResult(
            bool success,
            bool rolledBack,
            RuntimeFrame rollbackFrame,
            RuntimeFrame targetFrame,
            int replayedFrameCount,
            int appliedCommandCount,
            string message)
        {
            Success = success;
            RolledBack = rolledBack;
            RollbackFrame = rollbackFrame;
            TargetFrame = targetFrame;
            ReplayedFrameCount = replayedFrameCount;
            AppliedCommandCount = appliedCommandCount;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public bool RolledBack { get; }
        public RuntimeFrame RollbackFrame { get; }
        public RuntimeFrame TargetFrame { get; }
        public int ReplayedFrameCount { get; }
        public int AppliedCommandCount { get; }
        public string Message { get; }

        public static NetworkPredictionRollbackResult Succeeded(
            bool rolledBack,
            RuntimeFrame rollbackFrame,
            RuntimeFrame targetFrame,
            int replayedFrameCount,
            int appliedCommandCount,
            string message = "")
        {
            return new NetworkPredictionRollbackResult(
                true,
                rolledBack,
                rollbackFrame,
                targetFrame,
                replayedFrameCount,
                appliedCommandCount,
                message);
        }

        public static NetworkPredictionRollbackResult Failed(
            RuntimeFrame rollbackFrame,
            RuntimeFrame targetFrame,
            string message)
        {
            return new NetworkPredictionRollbackResult(
                false,
                false,
                rollbackFrame,
                targetFrame,
                0,
                0,
                message);
        }
    }

    public sealed class NetworkPredictionRollbackDiagnostics
    {
        public int HistoryCapacity { get; internal set; }
        public int HistoryCount { get; internal set; }
        public int RollbackCount { get; internal set; }
        public int RollbackFailureCount { get; internal set; }
        public int HistoryDropCount { get; internal set; }
        public int ReplayedFrameCount { get; internal set; }
        public int MaxRollbackFrames { get; internal set; }
        public RuntimeFrame LastRecordedFrame { get; internal set; } = RuntimeFrame.Zero;
        public RuntimeFrame LastRollbackFrame { get; internal set; } = RuntimeFrame.Zero;
        public RuntimeFrame LastTargetFrame { get; internal set; } = RuntimeFrame.Zero;
        public string LastFailure { get; internal set; } = string.Empty;
    }

    public sealed class NetworkSnapshotCorrectionResult
    {
        private NetworkSnapshotCorrectionResult(bool handled, bool applied, string message)
        {
            Handled = handled;
            Applied = applied;
            Message = message ?? string.Empty;
        }

        public bool Handled { get; }
        public bool Applied { get; }
        public string Message { get; }

        public static NetworkSnapshotCorrectionResult NotHandled()
        {
            return new NetworkSnapshotCorrectionResult(false, false, string.Empty);
        }

        public static NetworkSnapshotCorrectionResult HandledApplied(string message = "")
        {
            return new NetworkSnapshotCorrectionResult(true, true, message);
        }

        public static NetworkSnapshotCorrectionResult HandledFailed(string message)
        {
            return new NetworkSnapshotCorrectionResult(true, false, message);
        }
    }

    public sealed class NetworkPredictionRollbackCoordinator
    {
        public const int DefaultHistoryCapacity = 128;

        private readonly int _historyCapacity;
        private readonly FrameHistoryEntry[] _history;
        private readonly List<INetworkRollbackParticipant> _participants =
            new List<INetworkRollbackParticipant>();
        private readonly List<CommandRecord> _commands = new List<CommandRecord>();
        private readonly List<RuntimeCommand> _frameCommands = new List<RuntimeCommand>();
        private int _nextHistoryIndex;
        private int _historyCount;
        private NetworkBoundedRollbackHistory _boundedHistory;

        public NetworkPredictionRollbackCoordinator(int historyCapacity = DefaultHistoryCapacity)
        {
            _historyCapacity = historyCapacity > 0 ? historyCapacity : DefaultHistoryCapacity;
            _history = new FrameHistoryEntry[_historyCapacity];
            Diagnostics = new NetworkPredictionRollbackDiagnostics
            {
                HistoryCapacity = _historyCapacity
            };
        }

        public NetworkPredictionRollbackDiagnostics Diagnostics { get; }
        public int HistoryCount => _historyCount;
        /// <remarks>NoAlloc. Returns zero until bounded history is configured.</remarks>
        public int BoundedHistoryCount => _boundedHistory != null ? _boundedHistory.Count : 0;

        /// <summary>Creates the additive preallocated protocol-2 history owned by this coordinator.</summary>
        /// <remarks>AllocByDesign initialization operation. The legacy object history remains behavior-compatible.</remarks>
        public void ConfigureBoundedHistory(int historyCapacity = DefaultHistoryCapacity, int participantCapacity = 8)
        {
            _boundedHistory = new NetworkBoundedRollbackHistory(historyCapacity, participantCapacity);
        }

        /// <remarks>AllocByDesign initialization operation.</remarks>
        public void RegisterBoundedParticipant(INetworkBoundedRollbackParticipant participant)
        {
            if (_boundedHistory == null)
                throw new InvalidOperationException("Bounded rollback history is not configured.");
            _boundedHistory.Register(participant);
        }

        /// <remarks>AllocByDesign. Allocates one fixed history slab.</remarks>
        public void SealBoundedHistory()
        {
            if (_boundedHistory == null)
                throw new InvalidOperationException("Bounded rollback history is not configured.");
            _boundedHistory.Seal();
        }

        /// <remarks>NoAlloc after bounded history initialization.</remarks>
        public NetworkBoundedResult TryRecordBoundedFrame(RuntimeFrame frame)
        {
            return _boundedHistory != null
                ? _boundedHistory.TryRecord(frame)
                : NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidArgument);
        }

        /// <remarks>NoAlloc after bounded history initialization.</remarks>
        public NetworkBoundedResult TryRestoreBoundedFrame(RuntimeFrame frame)
        {
            return _boundedHistory != null
                ? _boundedHistory.TryRestore(frame)
                : NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidArgument);
        }

        public void RegisterParticipant(INetworkRollbackParticipant participant)
        {
            if (participant == null)
                throw new ArgumentNullException(nameof(participant));
            if (string.IsNullOrEmpty(participant.ParticipantId))
                throw new ArgumentException("Rollback participant id cannot be null or empty.", nameof(participant));

            for (int i = 0; i < _participants.Count; i++)
            {
                if (string.Equals(_participants[i].ParticipantId, participant.ParticipantId, StringComparison.Ordinal))
                    throw new ArgumentException("Duplicate rollback participant id: " + participant.ParticipantId, nameof(participant));
            }

            _participants.Add(participant);
            _participants.Sort(CompareParticipants);
        }

        public void Clear()
        {
            for (int i = 0; i < _history.Length; i++)
                _history[i] = null;
            _commands.Clear();
            _frameCommands.Clear();
            _nextHistoryIndex = 0;
            _historyCount = 0;
            Diagnostics.HistoryCount = 0;
        }

        public void RecordFrame(RuntimeFrame frame)
        {
            var states = new RollbackParticipantState[_participants.Count];
            for (int i = 0; i < _participants.Count; i++)
            {
                INetworkRollbackParticipant participant = _participants[i];
                states[i] = new RollbackParticipantState(
                    participant.ParticipantId,
                    participant.CaptureRollbackState(frame));
            }

            // Replay records a new checkpoint for an existing frame. Replace it rather than leaving
            // the stale copy first in lookup order and consuming another ring slot.
            for (int i = 0; i < _history.Length; i++)
            {
                if (_history[i] == null || _history[i].Frame != frame) continue;
                _history[i] = new FrameHistoryEntry(frame, states);
                Diagnostics.LastRecordedFrame = frame;
                return;
            }
            if (_historyCount == _historyCapacity)
                Diagnostics.HistoryDropCount++;
            else
                _historyCount++;

            _history[_nextHistoryIndex] = new FrameHistoryEntry(frame, states);
            _nextHistoryIndex = (_nextHistoryIndex + 1) % _historyCapacity;
            Diagnostics.HistoryCount = _historyCount;
            Diagnostics.LastRecordedFrame = frame;
            PruneCommandsBeforeOldestHistory();
        }

        public void RecordPredictedCommand(RuntimeCommand command)
        {
            AddCommand(command, authoritative: false);
        }

        /// <summary>Retains an authoritative command while simulation waits for a full restoration.</summary>
        public void RecordAuthoritativeCommand(RuntimeCommand command)
        {
            AddCommand(command, authoritative: true);
        }

        public NetworkPredictionRollbackResult ReconcileAuthoritativeCommands(
            IReadOnlyList<RuntimeCommand> commands,
            RuntimeFrame currentFrame,
            NetworkPredictionCommandSink commandSink,
            NetworkPredictionReplayFrameHandler replayFrame)
        {
            if (commands == null || commands.Count == 0)
            {
                return NetworkPredictionRollbackResult.Succeeded(
                    false,
                    currentFrame,
                    currentFrame,
                    0,
                    0);
            }
            if (commandSink == null)
                throw new ArgumentNullException(nameof(commandSink));
            if (replayFrame == null)
                throw new ArgumentNullException(nameof(replayFrame));

            RuntimeFrame earliest = commands[0].Frame;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                if (command.Frame < earliest)
                    earliest = command.Frame;
                AddCommand(command, authoritative: true);
            }

            if (earliest >= currentFrame)
            {
                int futureApplied = EnqueueCommandsFromFrame(currentFrame, commandSink, out string futureFailure);
                if (!string.IsNullOrEmpty(futureFailure))
                    return Fail(earliest, currentFrame, futureFailure);

                return NetworkPredictionRollbackResult.Succeeded(
                    false,
                    earliest,
                    currentFrame,
                    0,
                    futureApplied,
                    "authoritative commands queued without rollback");
            }

            return RollbackAndReplay(earliest, currentFrame, commandSink, replayFrame);
        }

        public NetworkPredictionRollbackResult ReconcileFromFrame(
            RuntimeFrame rollbackFrame,
            RuntimeFrame currentFrame,
            NetworkPredictionCommandSink commandSink,
            NetworkPredictionReplayFrameHandler replayFrame)
        {
            if (commandSink == null)
                throw new ArgumentNullException(nameof(commandSink));
            if (replayFrame == null)
                throw new ArgumentNullException(nameof(replayFrame));
            if (rollbackFrame >= currentFrame)
            {
                return NetworkPredictionRollbackResult.Succeeded(
                    false,
                    rollbackFrame,
                    currentFrame,
                    0,
                    0);
            }

            return RollbackAndReplay(rollbackFrame, currentFrame, commandSink, replayFrame);
        }

        public NetworkPredictionRollbackResult ReplayFromRestoredState(
            RuntimeFrame restoredFrame,
            RuntimeFrame currentFrame,
            NetworkPredictionCommandSink commandSink,
            NetworkPredictionReplayFrameHandler replayFrame)
        {
            if (commandSink == null)
                throw new ArgumentNullException(nameof(commandSink));
            if (replayFrame == null)
                throw new ArgumentNullException(nameof(replayFrame));

            // A full authoritative restore starts a new local history boundary. Earlier checkpoints
            // and unreplayed future checkpoints belong to the superseded prediction timeline.
            for (int i = 0; i < _history.Length; i++) _history[i] = null;
            _nextHistoryIndex = 0;
            _historyCount = 0;
            Diagnostics.HistoryCount = 0;
            for (int i = _commands.Count - 1; i >= 0; i--)
                if (_commands[i].Command.Frame < restoredFrame) _commands.RemoveAt(i);
            RecordFrame(restoredFrame);
            if (restoredFrame >= currentFrame)
            {
                RuntimeFrame commandFrame = restoredFrame > currentFrame ? restoredFrame : currentFrame;
                int futureApplied = EnqueueCommandsFromFrame(commandFrame, commandSink, out string futureFailure);
                if (!string.IsNullOrEmpty(futureFailure))
                    return Fail(restoredFrame, currentFrame, futureFailure);

                return NetworkPredictionRollbackResult.Succeeded(
                    false,
                    restoredFrame,
                    currentFrame,
                    0,
                    futureApplied);
            }

            return Replay(restoredFrame, currentFrame, commandSink, replayFrame);
        }

        public static bool AreEquivalentCommandIntents(RuntimeCommand left, RuntimeCommand right)
        {
            if (left.Frame != right.Frame
                || left.SourceId != right.SourceId
                || left.CommandId != right.CommandId
                || left.TargetId != right.TargetId
                || left.Envelope != right.Envelope)
            {
                return false;
            }

            if (!left.TraceId.IsNone
                && !right.TraceId.IsNone
                && left.TraceId == right.TraceId)
            {
                return true;
            }

            return left.Payload0 == right.Payload0
                && left.Payload1 == right.Payload1
                && left.Payload2 == right.Payload2;
        }

        private NetworkPredictionRollbackResult RollbackAndReplay(
            RuntimeFrame rollbackFrame,
            RuntimeFrame currentFrame,
            NetworkPredictionCommandSink commandSink,
            NetworkPredictionReplayFrameHandler replayFrame)
        {
            if (!TryGetHistory(rollbackFrame, out FrameHistoryEntry history))
                return Fail(rollbackFrame, currentFrame, "rollback history missing for frame " + rollbackFrame.Value);
            if (!RestoreHistory(history, out string restoreFailure))
                return Fail(rollbackFrame, currentFrame, restoreFailure);

            return Replay(rollbackFrame, currentFrame, commandSink, replayFrame);
        }

        private NetworkPredictionRollbackResult Replay(
            RuntimeFrame rollbackFrame,
            RuntimeFrame currentFrame,
            NetworkPredictionCommandSink commandSink,
            NetworkPredictionReplayFrameHandler replayFrame)
        {
            int appliedCommands = 0;
            int replayedFrames = 0;
            for (long value = rollbackFrame.Value; value < currentFrame.Value; value++)
            {
                var frame = new RuntimeFrame(value);
                appliedCommands += EnqueueCommandsForFrame(frame, commandSink, out string commandFailure);
                if (!string.IsNullOrEmpty(commandFailure))
                    return Fail(rollbackFrame, currentFrame, commandFailure);
                if (!replayFrame(frame, out string replayFailure))
                    return Fail(rollbackFrame, currentFrame, replayFailure);
                replayedFrames++;
            }

            appliedCommands += EnqueueCommandsFromFrame(currentFrame, commandSink, out string futureFailure);
            if (!string.IsNullOrEmpty(futureFailure))
                return Fail(rollbackFrame, currentFrame, futureFailure);

            Diagnostics.RollbackCount++;
            Diagnostics.ReplayedFrameCount += replayedFrames;
            Diagnostics.LastRollbackFrame = rollbackFrame;
            Diagnostics.LastTargetFrame = currentFrame;
            int rollbackFrames = (int)Math.Min(int.MaxValue, Math.Max(0L, currentFrame.Value - rollbackFrame.Value));
            if (rollbackFrames > Diagnostics.MaxRollbackFrames)
                Diagnostics.MaxRollbackFrames = rollbackFrames;

            return NetworkPredictionRollbackResult.Succeeded(
                true,
                rollbackFrame,
                currentFrame,
                replayedFrames,
                appliedCommands,
                "rollback replay completed");
        }

        private int EnqueueCommandsForFrame(
            RuntimeFrame frame,
            NetworkPredictionCommandSink commandSink,
            out string failure)
        {
            failure = string.Empty;
            _frameCommands.Clear();
            for (int i = 0; i < _commands.Count; i++)
            {
                RuntimeCommand command = _commands[i].Command;
                if (command.Frame == frame)
                    _frameCommands.Add(command);
            }

            _frameCommands.Sort(CompareCommands);
            for (int i = 0; i < _frameCommands.Count; i++)
            {
                if (!commandSink(_frameCommands[i], out failure))
                    return i;
            }

            int applied = _frameCommands.Count;
            _frameCommands.Clear();
            return applied;
        }

        private int EnqueueCommandsFromFrame(
            RuntimeFrame frame,
            NetworkPredictionCommandSink commandSink,
            out string failure)
        {
            failure = string.Empty;
            _frameCommands.Clear();
            for (int i = 0; i < _commands.Count; i++)
            {
                RuntimeCommand command = _commands[i].Command;
                if (command.Frame >= frame)
                    _frameCommands.Add(command);
            }

            _frameCommands.Sort(CompareCommands);
            for (int i = 0; i < _frameCommands.Count; i++)
            {
                if (!commandSink(_frameCommands[i], out failure))
                    return i;
            }

            int applied = _frameCommands.Count;
            _frameCommands.Clear();
            return applied;
        }

        private void AddCommand(RuntimeCommand command, bool authoritative)
        {
            if (authoritative)
            {
                for (int i = _commands.Count - 1; i >= 0; i--)
                {
                    CommandRecord record = _commands[i];
                    if (!record.Authoritative && AreEquivalentCommandIntents(record.Command, command))
                        _commands.RemoveAt(i);
                }
            }

            for (int i = _commands.Count - 1; i >= 0; i--)
            {
                CommandRecord record = _commands[i];
                if (record.Authoritative == authoritative && record.Command.Equals(command))
                    return;
            }

            _commands.Add(new CommandRecord(command, authoritative));
        }

        private bool TryGetHistory(RuntimeFrame frame, out FrameHistoryEntry history)
        {
            for (int i = 0; i < _historyCount; i++)
            {
                FrameHistoryEntry entry = _history[i];
                if (entry != null && entry.Frame == frame)
                {
                    history = entry;
                    return true;
                }
            }

            history = null;
            return false;
        }

        private bool RestoreHistory(FrameHistoryEntry history, out string failure)
        {
            failure = string.Empty;
            for (int i = 0; i < history.States.Length; i++)
            {
                RollbackParticipantState state = history.States[i];
                INetworkRollbackParticipant participant = FindParticipant(state.ParticipantId);
                if (participant == null)
                {
                    failure = "rollback participant missing: " + state.ParticipantId;
                    return false;
                }
                if (!participant.RestoreRollbackState(history.Frame, state.State, out failure))
                    return false;
            }

            return true;
        }

        private void PruneCommandsBeforeOldestHistory()
        {
            if (_historyCount == 0)
                return;

            RuntimeFrame oldest = RuntimeFrame.Zero;
            bool hasOldest = false;
            for (int i = 0; i < _historyCount; i++)
            {
                FrameHistoryEntry entry = _history[i];
                if (entry == null)
                    continue;
                if (!hasOldest || entry.Frame < oldest)
                {
                    oldest = entry.Frame;
                    hasOldest = true;
                }
            }

            if (!hasOldest)
                return;

            for (int i = _commands.Count - 1; i >= 0; i--)
            {
                if (_commands[i].Command.Frame < oldest)
                    _commands.RemoveAt(i);
            }
        }

        private INetworkRollbackParticipant FindParticipant(string participantId)
        {
            for (int i = 0; i < _participants.Count; i++)
            {
                if (string.Equals(_participants[i].ParticipantId, participantId, StringComparison.Ordinal))
                    return _participants[i];
            }

            return null;
        }

        private NetworkPredictionRollbackResult Fail(
            RuntimeFrame rollbackFrame,
            RuntimeFrame targetFrame,
            string failure)
        {
            Diagnostics.RollbackFailureCount++;
            Diagnostics.LastFailure = failure ?? string.Empty;
            return NetworkPredictionRollbackResult.Failed(rollbackFrame, targetFrame, failure);
        }

        private static int CompareParticipants(INetworkRollbackParticipant left, INetworkRollbackParticipant right)
        {
            return string.Compare(left.ParticipantId, right.ParticipantId, StringComparison.Ordinal);
        }

        private static int CompareCommands(RuntimeCommand left, RuntimeCommand right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            if (frame != 0)
                return frame;
            int source = left.SourceId.CompareTo(right.SourceId);
            if (source != 0)
                return source;
            int command = left.CommandId.CompareTo(right.CommandId);
            if (command != 0)
                return command;
            int target = left.TargetId.CompareTo(right.TargetId);
            if (target != 0)
                return target;
            return left.Sequence.CompareTo(right.Sequence);
        }

        private sealed class FrameHistoryEntry
        {
            public FrameHistoryEntry(RuntimeFrame frame, RollbackParticipantState[] states)
            {
                Frame = frame;
                States = states ?? Array.Empty<RollbackParticipantState>();
            }

            public RuntimeFrame Frame { get; }
            public RollbackParticipantState[] States { get; }
        }

        private readonly struct RollbackParticipantState
        {
            public RollbackParticipantState(string participantId, object state)
            {
                ParticipantId = participantId ?? string.Empty;
                State = state;
            }

            public string ParticipantId { get; }
            public object State { get; }
        }

        private readonly struct CommandRecord
        {
            public CommandRecord(RuntimeCommand command, bool authoritative)
            {
                Command = command;
                Authoritative = authoritative;
            }

            public RuntimeCommand Command { get; }
            public bool Authoritative { get; }
        }
    }
}

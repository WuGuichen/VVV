using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    /// <summary>Consumes trigger lifecycle commands from the Gameplay-owned command drain.</summary>
    public sealed class GameplayTriggerCommandSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.triggers.command";
        public const int DefaultPriority = 900;
        public const int DefaultMaxSignalCommandItems = 64;
        public const int DefaultMaxOccupancyCommandItems = 64;
        public const int DefaultMaxScalarCommandItems = 64;

        private readonly GameplayTriggerRuntime _runtime;
        private readonly GameplayTriggerSignal[] _signalScratch;
        private readonly bool[] _signalSeen;
        private readonly GameplayEntityId[] _occupancyScratch;
        private readonly bool[] _occupancySeen;
        private readonly GameplayTriggerScalarSample[] _scalarScratch;
        private readonly bool[] _scalarSeen;

        /// <summary>Creates the command consumer and its bounded Signal reconstruction scratch.</summary>
        /// <remarks>AllocByDesign at initialization; Tick is NoAlloc after warm-up.</remarks>
        public GameplayTriggerCommandSystem(
            GameplayTriggerRuntime runtime,
            string systemId = DefaultSystemId,
            int priority = DefaultPriority,
            int maxSignalCommandItems = DefaultMaxSignalCommandItems)
            : this(
                runtime,
                systemId,
                priority,
                maxSignalCommandItems,
                DefaultMaxOccupancyCommandItems,
                DefaultMaxScalarCommandItems)
        {
        }

        /// <summary>Creates the command consumer with explicit bounded reconstruction capacities.</summary>
        /// <remarks>AllocByDesign at initialization; Tick is NoAlloc after warm-up.</remarks>
        public GameplayTriggerCommandSystem(
            GameplayTriggerRuntime runtime,
            string systemId,
            int priority,
            int maxSignalCommandItems,
            int maxOccupancyCommandItems,
            int maxScalarCommandItems)
        {
            if (maxSignalCommandItems <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSignalCommandItems));
            if (maxOccupancyCommandItems <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxOccupancyCommandItems));
            if (maxScalarCommandItems <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxScalarCommandItems));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _signalScratch = new GameplayTriggerSignal[maxSignalCommandItems];
            _signalSeen = new bool[maxSignalCommandItems];
            _occupancyScratch = new GameplayEntityId[maxOccupancyCommandItems];
            _occupancySeen = new bool[maxOccupancyCommandItems];
            _scalarScratch = new GameplayTriggerScalarSample[maxScalarCommandItems];
            _scalarSeen = new bool[maxScalarCommandItems];
            SystemId = systemId ?? string.Empty;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase => GameplaySystemPhase.Command;
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;
        public int HandledCommandCount { get; private set; }
        public int RejectedCommandCount { get; private set; }
        public GameplayTriggerCommandExecutionResult LastResult { get; private set; }

        /// <summary>
        /// Gets the most recent rejected lifecycle command without being overwritten by success.
        /// </summary>
        /// <remarks>NoAlloc. The value remains default until the first rejection.</remarks>
        public GameplayTriggerCommandExecutionResult LastRejectedResult { get; private set; }

        /// <remarks>NoAlloc. Internal exact-composition identity.</remarks>
        internal GameplayTriggerRuntime RuntimeIdentity => _runtime;

        /// <remarks>NoAlloc. Internal exact-composition capacity observation.</remarks>
        internal int MaxSignalCommandItems => _signalScratch.Length;

        /// <remarks>NoAlloc. Internal exact-composition capacity observation.</remarks>
        internal int MaxOccupancyCommandItems => _occupancyScratch.Length;

        /// <remarks>NoAlloc. Internal exact-composition capacity observation.</remarks>
        internal int MaxScalarCommandItems => _scalarScratch.Length;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            IReadOnlyList<RuntimeCommand> commands = context.Commands;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                GameplayTriggerRuntimeCommandKind kind = GetCommandKind(command.CommandId);
                if (kind == GameplayTriggerRuntimeCommandKind.None)
                    continue;
                if (context.CommandState.IsHandled(command))
                    continue;
                if (kind == GameplayTriggerRuntimeCommandKind.SubmitSignalBatch)
                {
                    ProcessSignalBatch(context, commands, command);
                    continue;
                }
                if (kind == GameplayTriggerRuntimeCommandKind.SubmitOccupancyBatch)
                {
                    ProcessOccupancyBatch(context, commands, command);
                    continue;
                }
                if (kind == GameplayTriggerRuntimeCommandKind.SubmitScalarBatch)
                {
                    ProcessScalarBatch(context, commands, command);
                    continue;
                }

                GameplayTriggerResult result;
                GameplayTriggerInstanceId instanceId;
                if (!TryReadInstanceId(command, out instanceId) ||
                    !HasValidPayload(kind, command))
                {
                    result = new GameplayTriggerResult(
                        false,
                        GameplayTriggerErrorCode.InvalidArgument,
                        command.TargetId,
                        command.Payload0);
                }
                else if (kind == GameplayTriggerRuntimeCommandKind.SetEnabled)
                {
                    if (command.Payload1 == 0)
                        _runtime.ClearStagedSignalsForInstance(instanceId);
                    _runtime.TrySetEnabled(instanceId, command.Payload1 != 0, out result);
                }
                else
                {
                    _runtime.ClearStagedSignalsForInstance(instanceId);
                    _runtime.TryReset(instanceId, out result);
                }

                context.CommandState.MarkHandled(command);
                HandledCommandCount++;
                if (!result.Success)
                    RejectedCommandCount++;
                RecordResult(new GameplayTriggerCommandExecutionResult(
                    context.Frame,
                    command.Sequence,
                    kind,
                    instanceId,
                    result), in command);
            }
            FinalizeDeferredSignals(context.Frame);
        }

        private void ProcessSignalBatch(
            GameplaySystemContext context,
            IReadOnlyList<RuntimeCommand> commands,
            RuntimeCommand firstCommand)
        {
            Array.Clear(_signalSeen, 0, _signalSeen.Length);
            GameplayTriggerSignalCommandRecord first = default;
            bool hasDecodedRecord = false;
            int matched = 0;
            bool malformed = false;
            GameplayTriggerSignalCommandCodecStatus malformedStatus =
                GameplayTriggerSignalCommandCodecStatus.None;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand candidate = commands[i];
                if (context.CommandState.IsHandled(candidate) ||
                    !GameplayTriggerSignalCommandCodec.HasRawBatchIdentity(
                        candidate,
                        firstCommand))
                {
                    continue;
                }

                context.CommandState.MarkHandled(candidate);
                HandledCommandCount++;
                matched++;
                if (!GameplayTriggerSignalCommandCodec.TryDecodeRecord(
                        candidate,
                        out GameplayTriggerSignalCommandRecord decoded,
                        out GameplayTriggerSignalCommandCodecStatus status))
                {
                    malformed = true;
                    if (malformedStatus == GameplayTriggerSignalCommandCodecStatus.None)
                    {
                        malformedStatus = status;
                    }
                    continue;
                }

                if (!hasDecodedRecord)
                {
                    first = decoded;
                    hasDecodedRecord = true;
                }
                else if (decoded.Ordering != first.Ordering ||
                         decoded.ItemCount != first.ItemCount ||
                         decoded.Provenance != first.Provenance)
                {
                    malformed = true;
                    if (malformedStatus == GameplayTriggerSignalCommandCodecStatus.None)
                    {
                        malformedStatus = GameplayTriggerSignalCommandCodecStatus.InvalidBatch;
                    }
                    continue;
                }

                if (first.ItemCount == 0)
                {
                    if (matched > 1)
                    {
                        malformed = true;
                        malformedStatus = GameplayTriggerSignalCommandCodecStatus.InvalidBatch;
                    }
                    continue;
                }
                if (first.ItemCount > _signalScratch.Length)
                    continue;
                if (_signalSeen[decoded.ItemIndex])
                {
                    malformed = true;
                    malformedStatus = GameplayTriggerSignalCommandCodecStatus.InvalidItem;
                    continue;
                }

                _signalSeen[decoded.ItemIndex] = true;
                _signalScratch[decoded.ItemIndex] = decoded.Signal;
            }

            bool completeExternalBatch = hasDecodedRecord &&
                !malformed &&
                matched == (first.ItemCount == 0 ? 1 : first.ItemCount) &&
                (first.ItemCount > _signalScratch.Length ||
                    HasEverySignalItem(first.ItemCount));
            bool externalReconstructionOverflow = hasDecodedRecord &&
                first.ItemCount > _signalScratch.Length;
            GameplayTriggerResult result;
            if (externalReconstructionOverflow)
            {
                result = new GameplayTriggerResult(
                    false,
                    GameplayTriggerErrorCode.InputCapacityExceeded,
                    first.ItemCount,
                    _signalScratch.Length);
            }
            else if (!completeExternalBatch)
            {
                GameplayTriggerSignalCommandCodecStatus status = malformedStatus !=
                    GameplayTriggerSignalCommandCodecStatus.None
                    ? malformedStatus
                    : GameplayTriggerSignalCommandCodecStatus.InvalidBatch;
                result = new GameplayTriggerResult(
                    false,
                    GameplayTriggerErrorCode.InvalidArgument,
                    (int)status,
                    matched);
            }
            else
            {
                _runtime.TryFinalizeDeferredSignalsForSource(
                    context.Frame,
                    first.SourceBindingId,
                    _signalScratch,
                    first.ItemCount,
                    first.Ordering,
                    true,
                    out _,
                    out result);
            }

            if ((!completeExternalBatch || externalReconstructionOverflow) &&
                TryResolveRejectedSignalSource(
                    firstCommand,
                    hasDecodedRecord,
                    first,
                    out GameplayTriggerSourceBindingId rejectedSource,
                    out GameplayTriggerSignalOrdering rejectedOrdering))
            {
                _runtime.TryFinalizeDeferredSignalsForSource(
                    context.Frame,
                    rejectedSource,
                    _signalScratch,
                    0,
                    rejectedOrdering,
                    false,
                    out _,
                    out _);
                _runtime.BlockStagedSignalsForSource(rejectedSource);
            }
            else if (hasDecodedRecord)
            {
                // A valid external batch owns this source's only submission position for the frame.
                _runtime.BlockStagedSignalsForSource(first.SourceBindingId);
            }

            if (!result.Success)
                RejectedCommandCount += matched;
            RecordResult(new GameplayTriggerCommandExecutionResult(
                context.Frame,
                firstCommand.Sequence,
                GameplayTriggerRuntimeCommandKind.SubmitSignalBatch,
                default,
                result,
                hasDecodedRecord
                    ? first.SourceBindingId
                    : firstCommand.TargetId > 0
                        ? new GameplayTriggerSourceBindingId(firstCommand.TargetId)
                        : default,
                hasDecodedRecord ? first.BatchId : firstCommand.Envelope.Data0,
                hasDecodedRecord ? first.Provenance : firstCommand.Envelope.Provenance), in firstCommand);
        }

        private bool TryResolveRejectedSignalSource(
            RuntimeCommand firstCommand,
            bool hasDecodedRecord,
            GameplayTriggerSignalCommandRecord first,
            out GameplayTriggerSourceBindingId source,
            out GameplayTriggerSignalOrdering ordering)
        {
            if (hasDecodedRecord)
            {
                source = first.SourceBindingId;
                ordering = default;
                return source.IsValid &&
                    _runtime.TryGetSignalOrdering(source, out ordering);
            }
            if (firstCommand.TargetId <= 0)
            {
                source = default;
                ordering = default;
                return false;
            }
            source = new GameplayTriggerSourceBindingId(firstCommand.TargetId);
            return _runtime.TryGetSignalOrdering(source, out ordering);
        }

        private void FinalizeDeferredSignals(RuntimeFrame frame)
        {
            if (!_runtime.HasPendingDeferredSignalFinalization) return;
            int index = 0;
            while (index < _runtime.DeferredRecordCount)
            {
                if (!_runtime.TryGetStagedSourceAt(
                        index,
                        out GameplayTriggerSourceBindingId source,
                        out GameplayTriggerSignalOrdering ordering) ||
                    _runtime.WasStagedSourceSeenEarlier(index))
                {
                    index++;
                    continue;
                }

                _runtime.TryFinalizeDeferredSignalsForSource(
                    frame,
                    source,
                    _signalScratch,
                    0,
                    ordering,
                    false,
                    out _,
                    out _);
                _runtime.BlockStagedSignalsForSource(source);
                index = 0;
            }
            _runtime.CompleteDeferredSignalFinalization();
        }

        private void ProcessOccupancyBatch(
            GameplaySystemContext context,
            IReadOnlyList<RuntimeCommand> commands,
            RuntimeCommand firstCommand)
        {
            Array.Clear(_occupancySeen, 0, _occupancySeen.Length);
            GameplayTriggerOccupancyCommandRecord first = default;
            bool hasDecodedRecord = false;
            int matched = 0;
            bool malformed = false;
            GameplayTriggerOccupancyCommandCodecStatus malformedStatus =
                GameplayTriggerOccupancyCommandCodecStatus.None;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand candidate = commands[i];
                if (context.CommandState.IsHandled(candidate) ||
                    !GameplayTriggerOccupancyCommandCodec.HasRawBatchIdentity(
                        candidate,
                        firstCommand))
                {
                    continue;
                }

                context.CommandState.MarkHandled(candidate);
                HandledCommandCount++;
                matched++;
                if (!GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                        candidate,
                        out GameplayTriggerOccupancyCommandRecord decoded,
                        out GameplayTriggerOccupancyCommandCodecStatus status))
                {
                    malformed = true;
                    if (malformedStatus == GameplayTriggerOccupancyCommandCodecStatus.None)
                        malformedStatus = status;
                    continue;
                }

                if (!hasDecodedRecord)
                {
                    first = decoded;
                    hasDecodedRecord = true;
                }
                else if (decoded.ItemCount != first.ItemCount ||
                         decoded.Provenance != first.Provenance)
                {
                    malformed = true;
                    if (malformedStatus == GameplayTriggerOccupancyCommandCodecStatus.None)
                        malformedStatus = GameplayTriggerOccupancyCommandCodecStatus.InvalidBatch;
                    continue;
                }

                if (first.ItemCount == 0)
                {
                    if (matched > 1)
                    {
                        malformed = true;
                        malformedStatus = GameplayTriggerOccupancyCommandCodecStatus.InvalidBatch;
                    }
                    continue;
                }
                if (first.ItemCount > _occupancyScratch.Length)
                    continue;
                if (_occupancySeen[decoded.ItemIndex])
                {
                    malformed = true;
                    malformedStatus = GameplayTriggerOccupancyCommandCodecStatus.InvalidItem;
                    continue;
                }

                _occupancySeen[decoded.ItemIndex] = true;
                _occupancyScratch[decoded.ItemIndex] = decoded.Subject;
            }

            GameplayTriggerResult result;
            if (hasDecodedRecord && first.ItemCount > _occupancyScratch.Length)
            {
                result = new GameplayTriggerResult(
                    false,
                    GameplayTriggerErrorCode.InputCapacityExceeded,
                    first.ItemCount,
                    _occupancyScratch.Length);
            }
            else if (!hasDecodedRecord || malformed ||
                     matched != (first.ItemCount == 0 ? 1 : first.ItemCount) ||
                     !HasEveryItem(_occupancySeen, first.ItemCount))
            {
                GameplayTriggerOccupancyCommandCodecStatus status = malformedStatus !=
                    GameplayTriggerOccupancyCommandCodecStatus.None
                    ? malformedStatus
                    : GameplayTriggerOccupancyCommandCodecStatus.InvalidBatch;
                result = new GameplayTriggerResult(
                    false,
                    GameplayTriggerErrorCode.InvalidArgument,
                    (int)status,
                    matched);
            }
            else
            {
                _runtime.TrySubmitOccupancy(
                    context.Frame,
                    first.SourceBindingId,
                    _occupancyScratch.AsSpan(0, first.ItemCount),
                    out result);
            }

            if (!result.Success)
                RejectedCommandCount += matched;
            RecordResult(new GameplayTriggerCommandExecutionResult(
                context.Frame,
                firstCommand.Sequence,
                GameplayTriggerRuntimeCommandKind.SubmitOccupancyBatch,
                default,
                result,
                hasDecodedRecord
                    ? first.SourceBindingId
                    : firstCommand.TargetId > 0
                        ? new GameplayTriggerSourceBindingId(firstCommand.TargetId)
                        : default,
                hasDecodedRecord ? first.BatchId : firstCommand.Envelope.Data0,
                hasDecodedRecord ? first.Provenance : firstCommand.Envelope.Provenance), in firstCommand);
        }

        private void ProcessScalarBatch(
            GameplaySystemContext context,
            IReadOnlyList<RuntimeCommand> commands,
            RuntimeCommand firstCommand)
        {
            Array.Clear(_scalarSeen, 0, _scalarSeen.Length);
            GameplayTriggerScalarCommandRecord first = default;
            bool hasDecodedRecord = false;
            int matched = 0;
            bool malformed = false;
            GameplayTriggerScalarCommandCodecStatus malformedStatus =
                GameplayTriggerScalarCommandCodecStatus.None;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand candidate = commands[i];
                if (context.CommandState.IsHandled(candidate) ||
                    !GameplayTriggerScalarCommandCodec.HasRawBatchIdentity(
                        candidate,
                        firstCommand))
                {
                    continue;
                }

                context.CommandState.MarkHandled(candidate);
                HandledCommandCount++;
                matched++;
                if (!GameplayTriggerScalarCommandCodec.TryDecodeRecord(
                        candidate,
                        out GameplayTriggerScalarCommandRecord decoded,
                        out GameplayTriggerScalarCommandCodecStatus status))
                {
                    malformed = true;
                    if (malformedStatus == GameplayTriggerScalarCommandCodecStatus.None)
                        malformedStatus = status;
                    continue;
                }

                if (!hasDecodedRecord)
                {
                    first = decoded;
                    hasDecodedRecord = true;
                }
                else if (decoded.ItemCount != first.ItemCount ||
                         decoded.Provenance != first.Provenance)
                {
                    malformed = true;
                    if (malformedStatus == GameplayTriggerScalarCommandCodecStatus.None)
                        malformedStatus = GameplayTriggerScalarCommandCodecStatus.InvalidBatch;
                    continue;
                }

                if (first.ItemCount == 0)
                {
                    if (matched > 1)
                    {
                        malformed = true;
                        malformedStatus = GameplayTriggerScalarCommandCodecStatus.InvalidBatch;
                    }
                    continue;
                }
                if (first.ItemCount > _scalarScratch.Length)
                    continue;
                if (_scalarSeen[decoded.ItemIndex])
                {
                    malformed = true;
                    malformedStatus = GameplayTriggerScalarCommandCodecStatus.InvalidItem;
                    continue;
                }

                _scalarSeen[decoded.ItemIndex] = true;
                _scalarScratch[decoded.ItemIndex] = decoded.Sample;
            }

            GameplayTriggerResult result;
            if (hasDecodedRecord && first.ItemCount > _scalarScratch.Length)
            {
                result = new GameplayTriggerResult(
                    false,
                    GameplayTriggerErrorCode.InputCapacityExceeded,
                    first.ItemCount,
                    _scalarScratch.Length);
            }
            else if (!hasDecodedRecord || malformed ||
                     matched != (first.ItemCount == 0 ? 1 : first.ItemCount) ||
                     !HasEveryItem(_scalarSeen, first.ItemCount))
            {
                GameplayTriggerScalarCommandCodecStatus status = malformedStatus !=
                    GameplayTriggerScalarCommandCodecStatus.None
                    ? malformedStatus
                    : GameplayTriggerScalarCommandCodecStatus.InvalidBatch;
                result = new GameplayTriggerResult(
                    false,
                    GameplayTriggerErrorCode.InvalidArgument,
                    (int)status,
                    matched);
            }
            else
            {
                _runtime.TrySubmitScalarBatch(
                    context.Frame,
                    first.SourceBindingId,
                    _scalarScratch.AsSpan(0, first.ItemCount),
                    out result);
            }

            if (!result.Success)
                RejectedCommandCount += matched;
            RecordResult(new GameplayTriggerCommandExecutionResult(
                context.Frame,
                firstCommand.Sequence,
                GameplayTriggerRuntimeCommandKind.SubmitScalarBatch,
                default,
                result,
                hasDecodedRecord
                    ? first.SourceBindingId
                    : firstCommand.TargetId > 0
                        ? new GameplayTriggerSourceBindingId(firstCommand.TargetId)
                        : default,
                hasDecodedRecord ? first.BatchId : firstCommand.Envelope.Data0,
                hasDecodedRecord ? first.Provenance : firstCommand.Envelope.Provenance), in firstCommand);
        }

        private static bool HasEveryItem(bool[] seen, int itemCount)
        {
            for (int i = 0; i < itemCount; i++)
            {
                if (!seen[i])
                    return false;
            }
            return true;
        }

        private bool HasEverySignalItem(int itemCount)
        {
            for (int i = 0; i < itemCount; i++)
            {
                if (!_signalSeen[i])
                    return false;
            }
            return true;
        }

        private void RecordResult(
            GameplayTriggerCommandExecutionResult executionResult,
            in RuntimeCommand command)
        {
            LastResult = executionResult;
            if (!executionResult.Result.Success) LastRejectedResult = executionResult;
            _runtime.PublishCommandResult(in executionResult, in command);
        }

        private static GameplayTriggerRuntimeCommandKind GetCommandKind(int commandId)
        {
            if (commandId == GameplayTriggerRuntimeCommandIds.SetEnabled)
                return GameplayTriggerRuntimeCommandKind.SetEnabled;
            if (commandId == GameplayTriggerRuntimeCommandIds.Reset)
                return GameplayTriggerRuntimeCommandKind.Reset;
            if (commandId == GameplayTriggerRuntimeCommandIds.SubmitSignalBatch)
                return GameplayTriggerRuntimeCommandKind.SubmitSignalBatch;
            if (commandId == GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch)
                return GameplayTriggerRuntimeCommandKind.SubmitOccupancyBatch;
            if (commandId == GameplayTriggerRuntimeCommandIds.SubmitScalarBatch)
                return GameplayTriggerRuntimeCommandKind.SubmitScalarBatch;
            return GameplayTriggerRuntimeCommandKind.None;
        }

        private static bool HasValidPayload(
            GameplayTriggerRuntimeCommandKind kind,
            RuntimeCommand command)
        {
            if (command.Payload2 != 0 || command.Envelope.IsPresent)
                return false;
            if (kind == GameplayTriggerRuntimeCommandKind.SetEnabled)
                return command.Payload1 == 0 || command.Payload1 == 1;
            return command.Payload1 == 0;
        }

        private static bool TryReadInstanceId(
            RuntimeCommand command,
            out GameplayTriggerInstanceId instanceId)
        {
            if (command.TargetId <= 0 || command.Payload0 <= 0)
            {
                instanceId = default;
                return false;
            }

            instanceId = new GameplayTriggerInstanceId(
                command.TargetId,
                command.Payload0);
            return true;
        }
    }

    /// <summary>Evaluates the Trigger authority in the Gameplay Simulation phase.</summary>
    public sealed class GameplayTriggerEvaluationSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.triggers.evaluate";
        public const int DefaultPriority = 1000;

        private readonly GameplayTriggerRuntime _runtime;

        public GameplayTriggerEvaluationSystem(
            GameplayTriggerRuntime runtime,
            string systemId = DefaultSystemId,
            int priority = DefaultPriority)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            SystemId = systemId ?? string.Empty;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase => GameplaySystemPhase.Simulation;
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;
        public int EvaluationCount { get; private set; }
        public RuntimeFrame LastEvaluatedFrame { get; private set; }
        public GameplayTriggerResult LastResult { get; private set; }

        /// <remarks>NoAlloc. Internal exact-composition identity.</remarks>
        internal GameplayTriggerRuntime RuntimeIdentity => _runtime;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            LastResult = _runtime.Evaluate(context.Frame);
            LastEvaluatedFrame = context.Frame;
            EvaluationCount++;
        }
    }
}

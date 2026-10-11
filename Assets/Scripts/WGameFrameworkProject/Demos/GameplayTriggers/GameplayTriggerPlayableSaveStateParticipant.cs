using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Core.Math;
using MxFramework.Gameplay.Triggers.StoryBridge;
using MxFramework.Runtime;
using MxFramework.Story.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Demo.GameplayTriggers
{
    /// <summary>Owns Demo motion, command buffers, frame frontier, and Demo-only checkpoint readiness.</summary>
    internal sealed class GameplayTriggerPlayableSaveStateParticipant :
        IRuntimeSaveStateProvider,
        IRuntimeSaveStateRestorer,
        IRuntimeCheckpointQuiescenceChecker
    {
        internal const string ParticipantId =
            "mxframework.demo.gameplay-trigger-playable";
        internal const int ParticipantOrder = 400;
        private const int SchemaVersion = 1;
        private const int PendingBuffRequests = 1;
        private const int PendingPreparedBuffReservations = 2;
        private const int PendingCommittedBuffReservations = 3;
        private const int CandidateSwapInProgress = 4;

        private static readonly JsonSerializerSettings JsonSettings =
            new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
                MissingMemberHandling = MissingMemberHandling.Error
            };

        private readonly GameplayTriggerPlayableComposition _owner;

        internal GameplayTriggerPlayableSaveStateParticipant(
            GameplayTriggerPlayableComposition owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <remarks>NoAlloc. Trigger deferred/outbox records are intentionally not inspected here.</remarks>
        public RuntimeCheckpointQuiescenceResult CheckQuiescence(
            in RuntimeCheckpointContext context)
        {
            if (_owner.BuffRequests.PreparedReservationCount != 0)
                return RuntimeCheckpointQuiescenceResult.Pending(
                    PendingPreparedBuffReservations);
            if (_owner.BuffRequests.CommittedReservationCount != 0)
                return RuntimeCheckpointQuiescenceResult.Pending(
                    PendingCommittedBuffReservations);
            if (_owner.BuffRequests.Count != 0)
                return RuntimeCheckpointQuiescenceResult.Pending(PendingBuffRequests);
            if (_owner.IsCandidateSwapInProgress)
                return RuntimeCheckpointQuiescenceResult.Pending(CandidateSwapInProgress);
            return RuntimeCheckpointQuiescenceResult.Ready();
        }

        /// <remarks>AllocByDesign. Captures DTO arrays, JSON, and one module document.</remarks>
        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            CombatMotionState motion = _owner.Simulation.MotionState;
            var payload = new Payload
            {
                SchemaVersion = SchemaVersion,
                NextFrame = _owner.NextFrame,
                InputSequence = _owner.InputSequence,
                LastConsumedCommandSequence = _owner.LastConsumedCommandSequence,
                Motion = MotionPayload.From(in motion),
                StoryCommands = BufferPayload.From(_owner.StoryCommands.CaptureSnapshot()),
                GameplayCommands = BufferPayload.From(_owner.GameplayCommands.CaptureSnapshot()),
                DemoCommands = BufferPayload.From(_owner.DemoCommands.CaptureSnapshot())
            };
            string json = JsonConvert.SerializeObject(payload, JsonSettings);
            var module = new RuntimeModuleSaveState(
                ParticipantId,
                SchemaVersion,
                new RuntimeCustomState(3413, ParticipantId, SchemaVersion, json));
            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(
                new RuntimeSaveState(
                    RuntimeSaveState.CurrentSchemaVersion,
                    DateTime.UtcNow,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    _owner.NextFrame == 0L ? 0L : _owner.NextFrame - 1L,
                    null,
                    null,
                    new[] { module },
                    null));
        }

        /// <remarks>AllocByDesign. Parses and validates a candidate-owned payload before mutation.</remarks>
        public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            RuntimeSaveStateResult<Payload> load = Load(saveState);
            if (!load.Success)
                return RuntimeSaveStateResult<bool>.Failed(load.Error);

            Payload payload = load.Value;
            RuntimeSaveStateResult<PreparedRestore> prepare = Prepare(saveState, payload);
            if (!prepare.Success)
                return RuntimeSaveStateResult<bool>.Failed(prepare.Error);

            PreparedRestore restored = prepare.Value;
            CombatMotionState motion = restored.Motion;
            try
            {
                _owner.RestoreDemoState(
                    payload.NextFrame,
                    payload.InputSequence,
                    payload.LastConsumedCommandSequence,
                    in motion,
                    restored.StoryCommands,
                    restored.GameplayCommands,
                    restored.DemoCommands);
            }
            catch (Exception exception)
            {
                return Failed<bool>(
                    "$.moduleStates[" + ParticipantId + "]",
                    "Demo authority restore failed: " + exception.Message,
                    exception);
            }
            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private static RuntimeSaveStateResult<Payload> Load(RuntimeSaveState saveState)
        {
            if (saveState == null)
                return Failed<Payload>("$", "Save state is null.");

            RuntimeModuleSaveState module = null;
            for (int i = 0; i < saveState.ModuleStates.Count; i++)
            {
                RuntimeModuleSaveState candidate = saveState.ModuleStates[i];
                if (candidate != null && string.Equals(
                        candidate.ModuleId,
                        ParticipantId,
                        StringComparison.Ordinal))
                {
                    module = candidate;
                    break;
                }
            }
            string path = "$.moduleStates[" + ParticipantId + "]";
            if (module == null)
                return Failed<Payload>(path, "Demo module state is missing.");
            if (module.SchemaVersion != SchemaVersion ||
                module.CustomState == null ||
                module.CustomState.SchemaVersion != SchemaVersion ||
                !string.Equals(
                    module.CustomState.TypeId,
                    ParticipantId,
                    StringComparison.Ordinal))
            {
                return Failed<Payload>(path, "Demo module schema or type does not match.");
            }

            try
            {
                Payload payload = JsonConvert.DeserializeObject<Payload>(
                    module.CustomState.PayloadJson,
                    JsonSettings);
                return payload == null
                    ? Failed<Payload>(path + ".customState.payloadJson", "Demo payload parsed to null.")
                    : RuntimeSaveStateResult<Payload>.Succeeded(payload);
            }
            catch (Exception exception)
            {
                return Failed<Payload>(
                    path + ".customState.payloadJson",
                    "Demo payload could not be parsed: " + exception.Message,
                    exception);
            }
        }

        private RuntimeSaveStateResult<PreparedRestore> Prepare(
            RuntimeSaveState saveState,
            Payload payload)
        {
            if (payload.SchemaVersion != SchemaVersion)
                return Failed<PreparedRestore>("$.demo.schemaVersion", "Demo payload schema is unsupported.");
            if (payload.NextFrame <= 0L || payload.InputSequence < 0L ||
                payload.LastConsumedCommandSequence < -1L)
            {
                return Failed<PreparedRestore>("$.demo.frontier", "Demo command or frame frontier is invalid.");
            }

            long expectedNextFrame;
            try
            {
                expectedNextFrame = checked(saveState.Frame + 1L);
            }
            catch (OverflowException exception)
            {
                return Failed<PreparedRestore>("$.frame", "Save frame cannot resume.", exception);
            }
            if (payload.NextFrame != expectedNextFrame)
            {
                return Failed<PreparedRestore>(
                    "$.demo.nextFrame",
                    "Demo logical frontier does not resume at save frame plus one.");
            }
            if (payload.InputSequence != payload.NextFrame)
            {
                return Failed<PreparedRestore>(
                    "$.demo.inputSequence",
                    "Demo input sequence must equal the next logical frame.");
            }

            RuntimeSaveStateResult<CombatMotionState> motion = PrepareMotion(payload.Motion);
            if (!motion.Success)
                return RuntimeSaveStateResult<PreparedRestore>.Failed(motion.Error);
            if (motion.Value.Frame.Value != payload.NextFrame)
            {
                return Failed<PreparedRestore>(
                    "$.demo.motion.frame",
                    "Combat motion frame must equal the next logical frame.");
            }
            RuntimeSaveStateResult<RuntimeCommandBufferSnapshot> story = PrepareBuffer(
                payload.StoryCommands,
                payload.NextFrame,
                8,
                "storyCommands",
                PendingBufferContract.Story,
                _owner.StoryCommandValidator,
                payload.InputSequence);
            if (!story.Success)
                return RuntimeSaveStateResult<PreparedRestore>.Failed(story.Error);
            RuntimeSaveStateResult<RuntimeCommandBufferSnapshot> gameplay = PrepareBuffer(
                payload.GameplayCommands,
                payload.NextFrame,
                8,
                "gameplayCommands",
                PendingBufferContract.Gameplay,
                validator: null,
                payload.InputSequence);
            if (!gameplay.Success)
                return RuntimeSaveStateResult<PreparedRestore>.Failed(gameplay.Error);
            RuntimeSaveStateResult<RuntimeCommandBufferSnapshot> demo = PrepareBuffer(
                payload.DemoCommands,
                payload.NextFrame,
                16,
                "demoCommands",
                PendingBufferContract.Demo,
                _owner.DemoCommandValidator,
                payload.InputSequence);
            if (!demo.Success)
                return RuntimeSaveStateResult<PreparedRestore>.Failed(demo.Error);
            if (payload.LastConsumedCommandSequence >= demo.Value.NextSequence)
            {
                return Failed<PreparedRestore>(
                    "$.demo.lastConsumedCommandSequence",
                    "Last consumed Demo command lies outside the restored sequence frontier.");
            }

            return RuntimeSaveStateResult<PreparedRestore>.Succeeded(
                new PreparedRestore(
                    motion.Value,
                    story.Value,
                    gameplay.Value,
                    demo.Value));
        }

        private static RuntimeSaveStateResult<CombatMotionState> PrepareMotion(
            MotionPayload payload)
        {
            if (payload == null || payload.Frame < 0)
                return Failed<CombatMotionState>("$.demo.motion", "Combat motion payload is invalid.");
            const int validFlags = (1 << 6) - 1;
            if ((payload.CollisionFlags & ~validFlags) != 0)
            {
                return Failed<CombatMotionState>(
                    "$.demo.motion.collisionFlags",
                    "Combat motion collision flags are invalid.");
            }
            return RuntimeSaveStateResult<CombatMotionState>.Succeeded(
                new CombatMotionState(
                    new CombatFrame(payload.Frame),
                    Vector(payload.PositionX, payload.PositionY, payload.PositionZ),
                    Vector(payload.VelocityX, payload.VelocityY, payload.VelocityZ),
                    payload.Grounded,
                    Vector(payload.NormalX, payload.NormalY, payload.NormalZ),
                    (CombatMotionCollisionFlags)payload.CollisionFlags));
        }

        private static RuntimeSaveStateResult<RuntimeCommandBufferSnapshot> PrepareBuffer(
            BufferPayload payload,
            long nextFrame,
            int capacity,
            string field,
            PendingBufferContract contract,
            IRuntimeCommandValidator validator,
            long inputSequence)
        {
            string path = "$.demo." + field;
            if (payload == null || payload.CurrentFrame != nextFrame ||
                payload.NextSequence < 0L || payload.Pending == null ||
                payload.Pending.Length > capacity)
            {
                return Failed<RuntimeCommandBufferSnapshot>(
                    path,
                    "Runtime command buffer frontier or capacity is invalid.");
            }
            if (contract == PendingBufferContract.Gameplay && payload.Pending.Length != 0)
            {
                return Failed<RuntimeCommandBufferSnapshot>(
                    path + ".pending",
                    "Gameplay pending commands are unsupported by this playable Slice.");
            }

            var commands = new RuntimeCommand[payload.Pending.Length];
            var sequences = new HashSet<long>();
            for (int i = 0; i < payload.Pending.Length; i++)
            {
                CommandPayload command = payload.Pending[i];
                if (command == null || command.Frame < payload.CurrentFrame ||
                    command.Sequence < 0L || command.Sequence >= payload.NextSequence ||
                    !sequences.Add(command.Sequence))
                {
                    return Failed<RuntimeCommandBufferSnapshot>(
                        path + ".pending[" + i + "]",
                        "Pending command ordering frontier is invalid.");
                }
                RuntimeSaveStateResult<RuntimeCommand> prepared = command.Prepare(path, i);
                if (!prepared.Success)
                    return RuntimeSaveStateResult<RuntimeCommandBufferSnapshot>.Failed(prepared.Error);
                RuntimeCommand restored = prepared.Value;
                if (!ValidatePendingCommand(
                        in restored,
                        contract,
                        validator,
                        inputSequence))
                {
                    return Failed<RuntimeCommandBufferSnapshot>(
                        path + ".pending[" + i + "]",
                        "Pending command does not match its target buffer contract.");
                }
                commands[i] = restored;
            }
            return RuntimeSaveStateResult<RuntimeCommandBufferSnapshot>.Succeeded(
                new RuntimeCommandBufferSnapshot(
                    new RuntimeFrame(payload.CurrentFrame),
                    payload.NextSequence,
                    commands));
        }

        private static bool ValidatePendingCommand(
            in RuntimeCommand command,
            PendingBufferContract contract,
            IRuntimeCommandValidator validator,
            long inputSequence)
        {
            if (contract == PendingBufferContract.Gameplay || validator == null ||
                !validator.Validate(command).Success)
            {
                return false;
            }

            RuntimeCommandEnvelope envelope = command.Envelope;
            if (contract == PendingBufferContract.Demo)
            {
                return command.SourceId == GameplayTriggerPlayableIds.CommandSource &&
                       envelope.TypeId == GameplayTriggerPlayableIds.CommandEnvelopeType &&
                       envelope.SchemaVersion == GameplayTriggerPlayableIds.CommandEnvelopeSchema &&
                       envelope.Provenance == RuntimeCommandProvenanceKind.Exogenous &&
                       envelope.Data0 >= 0L && envelope.Data0 < inputSequence &&
                       envelope.Data1 == 0L && envelope.Data2 == 0L &&
                       envelope.Data3 == 0L && envelope.Data4 == 0L &&
                       envelope.Data5 == 0L && envelope.Data6 == 0L &&
                       envelope.Data7 == 0L;
            }

            if (command.SourceId != StoryRuntimeCommandSources.GameplayBridge ||
                command.CommandId != StoryRuntimeCommandIds.RaiseTrigger ||
                command.TargetId != GameplayTriggerPlayableIds.DoorSourceBinding ||
                command.Payload0 != GameplayTriggerPlayableIds.StoryOpenTrigger ||
                command.Payload1 != 0 || command.Payload2 != 0 ||
                envelope.TypeId != GameplayTriggerStoryCommandContracts.EnvelopeTypeId ||
                envelope.SchemaVersion != GameplayTriggerStoryCommandContracts.EnvelopeSchemaVersion ||
                envelope.Provenance != RuntimeCommandProvenanceKind.Derived ||
                envelope.Data0 <= 0L || envelope.Data1 < 0L ||
                envelope.Data2 != GameplayTriggerStoryCommandActionKinds.RaiseTriggerValue ||
                envelope.Data3 <= 0L || envelope.Data4 < 0L ||
                envelope.Data4 == long.MaxValue ||
                envelope.Data5 != envelope.Data4 + 1L ||
                envelope.Data5 > command.Frame.Value ||
                envelope.Data6 != 0L || envelope.Data7 != 0L)
            {
                return false;
            }
            return true;
        }

        private static FixVector3 Vector(long x, long y, long z)
        {
            return new FixVector3(Fix64.FromRaw(x), Fix64.FromRaw(y), Fix64.FromRaw(z));
        }

        private static RuntimeSaveStateResult<T> Failed<T>(
            string path,
            string message,
            Exception exception = null)
        {
            return RuntimeSaveStateResult<T>.Failed(new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.InvalidDocument,
                path,
                message,
                SchemaVersion,
                SchemaVersion,
                exception));
        }

        private sealed class Payload
        {
            public int SchemaVersion { get; set; }
            public long NextFrame { get; set; }
            public long InputSequence { get; set; }
            public long LastConsumedCommandSequence { get; set; }
            public MotionPayload Motion { get; set; }
            public BufferPayload StoryCommands { get; set; }
            public BufferPayload GameplayCommands { get; set; }
            public BufferPayload DemoCommands { get; set; }
        }

        private sealed class MotionPayload
        {
            public int Frame { get; set; }
            public long PositionX { get; set; }
            public long PositionY { get; set; }
            public long PositionZ { get; set; }
            public long VelocityX { get; set; }
            public long VelocityY { get; set; }
            public long VelocityZ { get; set; }
            public bool Grounded { get; set; }
            public long NormalX { get; set; }
            public long NormalY { get; set; }
            public long NormalZ { get; set; }
            public int CollisionFlags { get; set; }

            internal static MotionPayload From(in CombatMotionState state)
            {
                return new MotionPayload
                {
                    Frame = state.Frame.Value,
                    PositionX = state.Position.X.RawValue,
                    PositionY = state.Position.Y.RawValue,
                    PositionZ = state.Position.Z.RawValue,
                    VelocityX = state.Velocity.X.RawValue,
                    VelocityY = state.Velocity.Y.RawValue,
                    VelocityZ = state.Velocity.Z.RawValue,
                    Grounded = state.Grounded,
                    NormalX = state.LastCollisionNormal.X.RawValue,
                    NormalY = state.LastCollisionNormal.Y.RawValue,
                    NormalZ = state.LastCollisionNormal.Z.RawValue,
                    CollisionFlags = (int)state.CollisionFlags
                };
            }
        }

        private sealed class BufferPayload
        {
            public long CurrentFrame { get; set; }
            public long NextSequence { get; set; }
            public CommandPayload[] Pending { get; set; }

            internal static BufferPayload From(RuntimeCommandBufferSnapshot snapshot)
            {
                var pending = new CommandPayload[snapshot.PendingCount];
                for (int i = 0; i < pending.Length; i++)
                    pending[i] = CommandPayload.From(snapshot.PendingCommands[i]);
                return new BufferPayload
                {
                    CurrentFrame = snapshot.CurrentFrame.Value,
                    NextSequence = snapshot.NextSequence,
                    Pending = pending
                };
            }
        }

        private sealed class CommandPayload
        {
            public long Frame { get; set; }
            public int SourceId { get; set; }
            public int CommandId { get; set; }
            public int TargetId { get; set; }
            public int Payload0 { get; set; }
            public int Payload1 { get; set; }
            public int Payload2 { get; set; }
            public int TraceKindId { get; set; }
            public int TraceSourceId { get; set; }
            public long TraceExecutionId { get; set; }
            public long TraceSequence { get; set; }
            public long Sequence { get; set; }
            public int EnvelopeTypeId { get; set; }
            public int EnvelopeSchemaVersion { get; set; }
            public int EnvelopeProvenance { get; set; }
            public long[] EnvelopeData { get; set; }

            internal static CommandPayload From(RuntimeCommand command)
            {
                RuntimeCommandEnvelope envelope = command.Envelope;
                return new CommandPayload
                {
                    Frame = command.Frame.Value,
                    SourceId = command.SourceId,
                    CommandId = command.CommandId,
                    TargetId = command.TargetId,
                    Payload0 = command.Payload0,
                    Payload1 = command.Payload1,
                    Payload2 = command.Payload2,
                    TraceKindId = command.TraceId.KindId,
                    TraceSourceId = command.TraceId.SourceId,
                    TraceExecutionId = command.TraceId.ExecutionId,
                    TraceSequence = command.TraceId.Sequence,
                    Sequence = command.Sequence,
                    EnvelopeTypeId = envelope.TypeId,
                    EnvelopeSchemaVersion = envelope.SchemaVersion,
                    EnvelopeProvenance = (int)envelope.Provenance,
                    EnvelopeData = new[]
                    {
                        envelope.Data0,
                        envelope.Data1,
                        envelope.Data2,
                        envelope.Data3,
                        envelope.Data4,
                        envelope.Data5,
                        envelope.Data6,
                        envelope.Data7
                    }
                };
            }

            internal RuntimeSaveStateResult<RuntimeCommand> Prepare(
                string bufferPath,
                int index)
            {
                string path = bufferPath + ".pending[" + index + "]";
                if (Frame < 0L || SourceId < 0 || CommandId < 0 ||
                    TraceKindId < 0 || TraceSourceId < 0 || TraceExecutionId < 0L || TraceSequence < 0L ||
                    EnvelopeTypeId <= 0 || EnvelopeSchemaVersion <= 0 || EnvelopeData == null ||
                    EnvelopeData.Length != 8 ||
                    EnvelopeProvenance < (int)RuntimeCommandProvenanceKind.Unspecified ||
                    EnvelopeProvenance > (int)RuntimeCommandProvenanceKind.Derived)
                {
                    return Failed<RuntimeCommand>(path, "Pending command payload is invalid.");
                }
                var envelope = new RuntimeCommandEnvelope(
                    EnvelopeTypeId,
                    EnvelopeSchemaVersion,
                    (RuntimeCommandProvenanceKind)EnvelopeProvenance,
                    EnvelopeData[0],
                    EnvelopeData[1],
                    EnvelopeData[2],
                    EnvelopeData[3],
                    EnvelopeData[4],
                    EnvelopeData[5],
                    EnvelopeData[6],
                    EnvelopeData[7]);
                return RuntimeSaveStateResult<RuntimeCommand>.Succeeded(
                    new RuntimeCommand(
                        new RuntimeFrame(Frame),
                        SourceId,
                        CommandId,
                        TargetId,
                        Payload0,
                        Payload1,
                        Payload2,
                        new RuntimeTraceIdentity(TraceKindId, TraceSourceId, TraceExecutionId, TraceSequence),
                        Sequence,
                        envelope));
            }
        }

        private sealed class PreparedRestore
        {
            internal PreparedRestore(
                CombatMotionState motion,
                RuntimeCommandBufferSnapshot storyCommands,
                RuntimeCommandBufferSnapshot gameplayCommands,
                RuntimeCommandBufferSnapshot demoCommands)
            {
                Motion = motion;
                StoryCommands = storyCommands;
                GameplayCommands = gameplayCommands;
                DemoCommands = demoCommands;
            }

            internal CombatMotionState Motion { get; }
            internal RuntimeCommandBufferSnapshot StoryCommands { get; }
            internal RuntimeCommandBufferSnapshot GameplayCommands { get; }
            internal RuntimeCommandBufferSnapshot DemoCommands { get; }
        }

        private enum PendingBufferContract
        {
            Story = 1,
            Gameplay = 2,
            Demo = 3
        }
    }
}

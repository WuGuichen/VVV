using System;
using MxFramework.Combat.Motion;
using MxFramework.Runtime;

namespace MxFramework.Demo.GameplayTriggers
{
    /// <summary>
    /// Contributes Demo-owned motion and command frontiers without duplicating door authority.
    /// Implements the numeric-protocol contributor for the demo-owned state.
    /// </summary>
    internal sealed class GameplayTriggerPlayableHashContributor :
        IRuntimeHashContributor
    {
        internal const string StableContributorId =
            "mxframework.demo.gameplay-trigger-playable";
        private const int StaticTopologyVersion = 1;

        private readonly GameplayTriggerPlayableComposition _owner;

        // Non-frozen, demo-local V3 sub-schema ids and object field markers (must be &gt; 0).
        private const int snapshotSchemaId = 90101;
        private const int CommandObjectField = 1;
        private const int CommandSchemaId = 90102;
        private const int EnvelopeObjectField = 1;
        private const int EnvelopeSchemaId = 90103;

        /// <summary>V2 numeric field ids for the gameplay-trigger playable hash state.</summary>
        private static class Fields
        {
            public const int TopologyVersion = 1;
            public const int MotionFrame = 2;

            // Position vector (fields 3-5)
            public const int PositionX = 3, PositionY = 4, PositionZ = 5;
            // Velocity vector (fields 6-8)
            public const int VelocityX = 6, VelocityY = 7, VelocityZ = 8;

            public const int Grounded = 9;

            // Normal vector (fields 10-12)
            public const int NormalX = 10, NormalY = 11, NormalZ = 12;

            public const int CollisionFlags = 13;

            public const int StoryCommands = 14;
            public const int GameplayCommands = 15;
            public const int DemoCommands = 16;

            public const int LastConsumedSequence = 17;
        }

        /// <summary>V2 numeric field ids for a command-buffer snapshot object.</summary>
        private static class CommandBufferFields
        {
            public const int CurrentFrame = 1;
            public const int NextSequence = 2;
            public const int PendingCount = 3;
            public const int Pending = 4;
        }

        /// <summary>V2 numeric field ids for a single pending command object.</summary>
        private static class CommandFields
        {
            public const int Frame = 1;
            public const int SourceId = 2;
            public const int CommandId = 3;
            public const int TargetId = 4;
            public const int Payload0 = 5;
            public const int Payload1 = 6;
            public const int Payload2 = 7;
            public const int TraceId = 8;
            public const int Sequence = 9;
            public const int Envelope = 10;
        }

        /// <summary>V2 numeric field ids for a command envelope object.</summary>
        private static class EnvelopeFields
        {
            public const int TypeId = 1;
            public const int SchemaVersion = 2;
            public const int Provenance = 3;
            public const int Data0 = 4;
            public const int Data1 = 5;
            public const int Data2 = 6;
            public const int Data3 = 7;
            public const int Data4 = 8;
            public const int Data5 = 9;
            public const int Data6 = 10;
            public const int Data7 = 11;
        }

        internal GameplayTriggerPlayableHashContributor(
            GameplayTriggerPlayableComposition owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public int ContributorId => RuntimeHashContributorIds.DemoGameplayTriggerPlayable;
        public int HashSchemaId => RuntimeHashSchemaIds.DemoGameplayTriggerPlayable;
        public int HashSchemaVersion => RuntimeHashSchemaIds.Version1;
        /// <remarks>LowFreqAlloc because RuntimeCommandBuffer snapshots copy pending commands.</remarks>
        public void Contribute(
            RuntimeHashContext context,
            RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            accumulator.AddInt(Fields.TopologyVersion, StaticTopologyVersion);
            CombatMotionState motion = _owner.Simulation.MotionState;
            accumulator.AddInt(Fields.MotionFrame, motion.Frame.Value);
            AddVector(accumulator, Fields.PositionX, Fields.PositionY, Fields.PositionZ, motion.Position);
            AddVector(accumulator, Fields.VelocityX, Fields.VelocityY, Fields.VelocityZ, motion.Velocity);
            accumulator.AddInt(Fields.Grounded, motion.Grounded ? 1 : 0);
            AddVector(accumulator, Fields.NormalX, Fields.NormalY, Fields.NormalZ, motion.LastCollisionNormal);
            accumulator.AddInt(Fields.CollisionFlags, (int)motion.CollisionFlags);
            AddBuffer(accumulator, Fields.StoryCommands, _owner.StoryCommands.CaptureSnapshot());
            AddBuffer(accumulator, Fields.GameplayCommands, _owner.GameplayCommands.CaptureSnapshot());
            AddBuffer(accumulator, Fields.DemoCommands, _owner.DemoCommands.CaptureSnapshot());
            accumulator.AddLong(Fields.LastConsumedSequence, _owner.LastConsumedCommandSequence);
        }

        private static void AddVector(
            RuntimeHashAccumulator accumulator,
            int xKey,
            int yKey,
            int zKey,
            MxFramework.Core.Math.FixVector3 vector)
        {
            accumulator.AddLong(xKey, vector.X.RawValue);
            accumulator.AddLong(yKey, vector.Y.RawValue);
            accumulator.AddLong(zKey, vector.Z.RawValue);
        }

        private static void AddBuffer(
            RuntimeHashAccumulator accumulator,
            int field,
            RuntimeCommandBufferSnapshot snapshot)
        {
            accumulator.BeginObject(field, snapshotSchemaId, RuntimeHashSchemaIds.Version1);
            accumulator.AddLong(CommandBufferFields.CurrentFrame, snapshot.CurrentFrame.Value);
            accumulator.AddLong(CommandBufferFields.NextSequence, snapshot.NextSequence);
            accumulator.AddInt(CommandBufferFields.PendingCount, snapshot.PendingCount);
            accumulator.BeginArray(CommandBufferFields.Pending, snapshot.PendingCount);
            for (int i = 0; i < snapshot.PendingCount; i++)
            {
                RuntimeCommand command = snapshot.PendingCommands[i];
                accumulator.BeginElement(i);
                accumulator.BeginObject(
                    CommandObjectField, CommandSchemaId, RuntimeHashSchemaIds.Version1);
                accumulator.AddLong(CommandFields.Frame, command.Frame.Value);
                accumulator.AddInt(CommandFields.SourceId, command.SourceId);
                accumulator.AddInt(CommandFields.CommandId, command.CommandId);
                accumulator.AddInt(CommandFields.TargetId, command.TargetId);
                accumulator.AddInt(CommandFields.Payload0, command.Payload0);
                accumulator.AddInt(CommandFields.Payload1, command.Payload1);
                accumulator.AddInt(CommandFields.Payload2, command.Payload2);
                command.TraceId.Contribute(accumulator, CommandFields.TraceId);
                accumulator.AddLong(CommandFields.Sequence, command.Sequence);
                RuntimeCommandEnvelope envelope = command.Envelope;
                accumulator.BeginObject(
                    EnvelopeObjectField, EnvelopeSchemaId, RuntimeHashSchemaIds.Version1, envelope.IsPresent);
                if (envelope.IsPresent)
                {
                    accumulator.AddInt(EnvelopeFields.TypeId, envelope.TypeId);
                    accumulator.AddInt(EnvelopeFields.SchemaVersion, envelope.SchemaVersion);
                    accumulator.AddInt(EnvelopeFields.Provenance, (int)envelope.Provenance);
                    accumulator.AddLong(EnvelopeFields.Data0, envelope.Data0);
                    accumulator.AddLong(EnvelopeFields.Data1, envelope.Data1);
                    accumulator.AddLong(EnvelopeFields.Data2, envelope.Data2);
                    accumulator.AddLong(EnvelopeFields.Data3, envelope.Data3);
                    accumulator.AddLong(EnvelopeFields.Data4, envelope.Data4);
                    accumulator.AddLong(EnvelopeFields.Data5, envelope.Data5);
                    accumulator.AddLong(EnvelopeFields.Data6, envelope.Data6);
                    accumulator.AddLong(EnvelopeFields.Data7, envelope.Data7);
                }
                accumulator.EndObject();
                accumulator.EndObject();
                accumulator.EndElement();
            }
            accumulator.EndArray();
            accumulator.EndObject();
        }

    }
}

using System;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Authority input for independent facing and bounded basic approach movement.</summary>
    public readonly struct CharacterTestBehaviorControlInput
    {
        public bool IsPresent { get; }
        /// <summary>0 preserves legacy movement-facing, 1 releases facing, 2 faces the explicit world point.</summary>
        public int FacingMode { get; }
        public int TargetXMillimeters { get; }
        public int TargetZMillimeters { get; }
        public int TurnSpeedScaleMilli { get; }
        /// <summary>-1 means no approach bound; otherwise limits only basic control movement per fixed step.</summary>
        public int MaxMoveStepMillimeters { get; }
        public long OwnerToken { get; }
        public bool IsRelease { get; }
        public bool IsOwnerOnlyRelease { get; }
        public bool ShouldSerializeIsOwnerOnlyRelease() => IsOwnerOnlyRelease;
        public CharacterTestBehaviorControlInput(int facingMode, int targetXMillimeters, int targetZMillimeters,
            int turnSpeedScaleMilli = 1000, int maxMoveStepMillimeters = -1, bool isPresent = true,
            long ownerToken = 0, bool isRelease = false)
            : this(facingMode, targetXMillimeters, targetZMillimeters, false, turnSpeedScaleMilli, maxMoveStepMillimeters,
                isPresent, ownerToken, isRelease) { }

        [Newtonsoft.Json.JsonConstructor]
        public CharacterTestBehaviorControlInput(int facingMode, int targetXMillimeters, int targetZMillimeters, bool isOwnerOnlyRelease,
            int turnSpeedScaleMilli = 1000, int maxMoveStepMillimeters = -1, bool isPresent = true,
            long ownerToken = 0, bool isRelease = false)
        {
            if (!isPresent) { this = default; return; }
            if (isOwnerOnlyRelease && !isRelease) throw new ArgumentException("Owner-only release must be a release.");
            if (facingMode < 0 || facingMode > 2 || turnSpeedScaleMilli <= 0 || turnSpeedScaleMilli > 10000 || maxMoveStepMillimeters < -1)
                throw new ArgumentException("Behavior control input is invalid.");
            IsPresent = isPresent; FacingMode = facingMode; TargetXMillimeters = targetXMillimeters;
            TargetZMillimeters = targetZMillimeters; TurnSpeedScaleMilli = turnSpeedScaleMilli;
            MaxMoveStepMillimeters = maxMoveStepMillimeters;
            OwnerToken = ownerToken; IsRelease = isRelease; IsOwnerOnlyRelease = isOwnerOnlyRelease;
        }
        private static class HashFields
        {
            internal const int Facing = 1, X = 2, Z = 3, TurnScale = 4, StepLimit = 5, Owner = 6, Release = 7, OwnerOnlyRelease = 8;
        }

        internal void AddHash(ref RuntimeHashAccumulator hash, int key)
        {
            hash.BeginObject(key, RuntimeHashSchemaIds.CharacterTestBehaviorControlInput, IsOwnerOnlyRelease ? 2 : 1, IsPresent);
            if (IsPresent)
            {
                hash.AddInt(HashFields.Facing, FacingMode); hash.AddInt(HashFields.X, TargetXMillimeters);
                hash.AddInt(HashFields.Z, TargetZMillimeters); hash.AddInt(HashFields.TurnScale, TurnSpeedScaleMilli);
                hash.AddInt(HashFields.StepLimit, MaxMoveStepMillimeters);
                hash.AddLong(HashFields.Owner, OwnerToken); hash.AddInt(HashFields.Release, IsRelease ? 1 : 0);
                if (IsOwnerOnlyRelease) hash.AddInt(HashFields.OwnerOnlyRelease, 1);
            }
            hash.EndObject();
        }
    }

    public static partial class CharacterTestRuntimeCommandFactory
    {
        internal const int BehaviorControlEnvelopeTypeId = 901103;
        internal const int BehaviorStopEnvelopeTypeId = 901104;
        internal static RuntimeCommand SetBehaviorControlIntent(RuntimeFrame frame, int sourceId, int entityId,
            Vector2 move, float speed, in CharacterTestBehaviorControlInput control, RuntimeTraceIdentity traceId = default)
        {
            RuntimeCommand ordinary = SetControlIntent(frame, sourceId, entityId, move, move, false, false, false, false,
                attackSecondaryPressed: false, movementSpeedMultiplier: speed, traceId: traceId);
            int flags = CharacterTestRuntimeCommandPayloadCodec.UnpackControlIntentFlags(ordinary.Payload2);
            long target = unchecked((long)(uint)control.TargetXMillimeters | ((long)control.TargetZMillimeters << 32));
            return new RuntimeCommand(frame, sourceId, CharacterTestRuntimeCommandIds.SetControlIntent, entityId,
                ordinary.Payload0, ordinary.Payload1,
                CharacterTestRuntimeCommandPayloadCodec.PackControlIntentMetadata(flags, CharacterTestControlIntent.BehaviorControlSchemaVersion),
                traceId: traceId, envelope: new RuntimeCommandEnvelope(BehaviorControlEnvelopeTypeId, control.IsOwnerOnlyRelease ? 2 : 1,
                    RuntimeCommandProvenanceKind.Unspecified, data0: target,
                    data1: (uint)control.FacingMode | ((long)control.TurnSpeedScaleMilli << 8) | (control.IsRelease ? 1L << 32 : 0L) | (control.IsOwnerOnlyRelease ? 1L << 33 : 0L),
                    data2: control.MaxMoveStepMillimeters, data3: control.OwnerToken));
        }
        internal static bool IsBehaviorControlEnvelopeValid(in RuntimeCommandEnvelope envelope)
        {
            long mode = envelope.Data1 & 255;
            long speed = (envelope.Data1 >> 8) & 65535;
            bool ownerOnly = (envelope.Data1 & (1L << 33)) != 0;
            return envelope.TypeId == BehaviorControlEnvelopeTypeId
                && (envelope.SchemaVersion == 1 && !ownerOnly || envelope.SchemaVersion == 2 && ownerOnly && (envelope.Data1 & (1L << 32)) != 0)
                && envelope.Provenance == RuntimeCommandProvenanceKind.Unspecified && mode <= 2
                && speed > 0 && speed <= 10000 && envelope.Data2 >= -1 && envelope.Data2 <= int.MaxValue
                && (envelope.Data1 & ~((65535L << 8) | 255L | (1L << 32) | (1L << 33))) == 0;
        }
        internal static CharacterTestBehaviorControlInput DecodeBehaviorControl(in RuntimeCommandEnvelope envelope)
        {
            if (!IsBehaviorControlEnvelopeValid(envelope)) return default;
            return new CharacterTestBehaviorControlInput((int)(envelope.Data1 & 255), unchecked((int)envelope.Data0),
                unchecked((int)(envelope.Data0 >> 32)), (envelope.Data1 & (1L << 33)) != 0,
                (int)((envelope.Data1 >> 8) & 65535), (int)envelope.Data2,
                ownerToken: envelope.Data3, isRelease: (envelope.Data1 & (1L << 32)) != 0);
        }
    }
}

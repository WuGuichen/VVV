using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public enum CombatPhysicalContactKind
    {
        Unknown = 0,
        BodyPartContact = 1,
        Landing = 2,
        ObjectImpact = 3
    }

    public enum CombatBodyPartContactActivationMode
    {
        Disabled = 0,
        Always = 1,
        ActionWindow = 2,
        ExplicitState = 3,
        MotionState = 4
    }

    public enum CombatPhysicalContactPhase
    {
        None = 0,
        Started = 1,
        Stayed = 2,
        Ended = 3
    }

    /// <summary>
    /// Deterministic physical-contact fact emitted by Combat and consumed by presentation bridges.
    /// </summary>
    /// <remarks>NoAlloc: immutable value contract; hashing does not allocate.</remarks>
    public readonly struct CombatPhysicalContactEvent : IEquatable<CombatPhysicalContactEvent>
    {
        public CombatPhysicalContactEvent(
            CombatFrame frame,
            int sequence,
            CombatPhysicalContactKind kind,
            CombatPhysicalContactPhase phase,
            CombatEntityId sourceEntityId,
            CombatBodyId sourceBodyId,
            CombatColliderId sourceColliderId,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId,
            CombatPhysicsShapeKind targetShapeKind,
            int sourceSlotId,
            int contactPairKey,
            FixVector3 point,
            FixVector3 normal,
            CombatSurfaceMaterial surfaceMaterial,
            int relativeSpeedMillimetersPerSecond,
            int normalSpeedMillimetersPerSecond,
            int tangentialSpeedMillimetersPerSecond,
            int pressure,
            int intensity,
            int contactPointId = 0,
            int bodyPartId = 0,
            CombatBodyPartContactActivationMode activationMode = CombatBodyPartContactActivationMode.Always)
        {
            if (sequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence), "Contact event sequence cannot be negative.");
            }

            if (sourceSlotId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceSlotId), "Contact source slot id cannot be negative.");
            }

            if (contactPointId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contactPointId), "Contact point id cannot be negative.");
            }

            if (bodyPartId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bodyPartId), "Contact body part id cannot be negative.");
            }

            Frame = frame;
            Sequence = sequence;
            Kind = kind;
            Phase = phase;
            SourceEntityId = sourceEntityId;
            SourceBodyId = sourceBodyId;
            SourceColliderId = sourceColliderId;
            TargetBodyId = targetBodyId;
            TargetColliderId = targetColliderId;
            TargetShapeKind = targetShapeKind;
            SourceSlotId = sourceSlotId;
            ContactPairKey = contactPairKey;
            Point = point;
            Normal = normal;
            SurfaceMaterial = surfaceMaterial;
            RelativeSpeedMillimetersPerSecond = ClampNonNegative(relativeSpeedMillimetersPerSecond);
            NormalSpeedMillimetersPerSecond = ClampNonNegative(normalSpeedMillimetersPerSecond);
            TangentialSpeedMillimetersPerSecond = ClampNonNegative(tangentialSpeedMillimetersPerSecond);
            Pressure = ClampNonNegative(pressure);
            Intensity = ClampNonNegative(intensity);
            ContactPointId = contactPointId;
            BodyPartId = bodyPartId;
            ActivationMode = activationMode;
        }

        public CombatFrame Frame { get; }

        public int Sequence { get; }

        public CombatPhysicalContactKind Kind { get; }

        public CombatPhysicalContactPhase Phase { get; }

        public CombatEntityId SourceEntityId { get; }

        public CombatBodyId SourceBodyId { get; }

        public CombatColliderId SourceColliderId { get; }

        public CombatBodyId TargetBodyId { get; }

        public CombatColliderId TargetColliderId { get; }

        public CombatPhysicsShapeKind TargetShapeKind { get; }

        public int SourceSlotId { get; }

        public int ContactPairKey { get; }

        public FixVector3 Point { get; }

        public FixVector3 Normal { get; }

        public CombatSurfaceMaterial SurfaceMaterial { get; }

        public int RelativeSpeedMillimetersPerSecond { get; }

        public int NormalSpeedMillimetersPerSecond { get; }

        public int TangentialSpeedMillimetersPerSecond { get; }

        public int Pressure { get; }

        public int Intensity { get; }

        public int ContactPointId { get; }

        public int BodyPartId { get; }

        public CombatBodyPartContactActivationMode ActivationMode { get; }

        public CombatHash ContributeHash(CombatHash hash)
        {
            return hash
                .Add(Frame)
                .Add(Sequence)
                .Add((int)Kind)
                .Add((int)Phase)
                .Add(SourceEntityId)
                .Add(SourceBodyId)
                .Add(SourceColliderId)
                .Add(TargetBodyId)
                .Add(TargetColliderId)
                .Add((int)TargetShapeKind)
                .Add(SourceSlotId)
                .Add(ContactPairKey)
                .Add(unchecked((ulong)Point.X.RawValue))
                .Add(unchecked((ulong)Point.Y.RawValue))
                .Add(unchecked((ulong)Point.Z.RawValue))
                .Add(unchecked((ulong)Normal.X.RawValue))
                .Add(unchecked((ulong)Normal.Y.RawValue))
                .Add(unchecked((ulong)Normal.Z.RawValue))
                .Add((int)SurfaceMaterial.Kind)
                .Add(unchecked((ulong)SurfaceMaterial.Friction.RawValue))
                .Add(unchecked((ulong)SurfaceMaterial.Elasticity.RawValue))
                .Add(unchecked((ulong)SurfaceMaterial.FixedBounceHeight.RawValue))
                .Add(RelativeSpeedMillimetersPerSecond)
                .Add(NormalSpeedMillimetersPerSecond)
                .Add(TangentialSpeedMillimetersPerSecond)
                .Add(Pressure)
                .Add(Intensity)
                .Add(ContactPointId)
                .Add(BodyPartId)
                .Add((int)ActivationMode);
        }

        public bool Equals(CombatPhysicalContactEvent other)
        {
            return Frame.Equals(other.Frame)
                && Sequence == other.Sequence
                && Kind == other.Kind
                && Phase == other.Phase
                && SourceEntityId.Equals(other.SourceEntityId)
                && SourceBodyId.Equals(other.SourceBodyId)
                && SourceColliderId.Equals(other.SourceColliderId)
                && TargetBodyId.Equals(other.TargetBodyId)
                && TargetColliderId.Equals(other.TargetColliderId)
                && TargetShapeKind == other.TargetShapeKind
                && SourceSlotId == other.SourceSlotId
                && ContactPairKey == other.ContactPairKey
                && Point.Equals(other.Point)
                && Normal.Equals(other.Normal)
                && SurfaceMaterial.Equals(other.SurfaceMaterial)
                && RelativeSpeedMillimetersPerSecond == other.RelativeSpeedMillimetersPerSecond
                && NormalSpeedMillimetersPerSecond == other.NormalSpeedMillimetersPerSecond
                && TangentialSpeedMillimetersPerSecond == other.TangentialSpeedMillimetersPerSecond
                && Pressure == other.Pressure
                && Intensity == other.Intensity
                && ContactPointId == other.ContactPointId
                && BodyPartId == other.BodyPartId
                && ActivationMode == other.ActivationMode;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicalContactEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ Sequence;
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ (int)Phase;
                hash = (hash * 397) ^ SourceEntityId.GetHashCode();
                hash = (hash * 397) ^ SourceBodyId.GetHashCode();
                hash = (hash * 397) ^ SourceColliderId.GetHashCode();
                hash = (hash * 397) ^ TargetBodyId.GetHashCode();
                hash = (hash * 397) ^ TargetColliderId.GetHashCode();
                hash = (hash * 397) ^ (int)TargetShapeKind;
                hash = (hash * 397) ^ SourceSlotId;
                hash = (hash * 397) ^ ContactPairKey;
                hash = (hash * 397) ^ Point.GetHashCode();
                hash = (hash * 397) ^ Normal.GetHashCode();
                hash = (hash * 397) ^ SurfaceMaterial.GetHashCode();
                hash = (hash * 397) ^ RelativeSpeedMillimetersPerSecond;
                hash = (hash * 397) ^ NormalSpeedMillimetersPerSecond;
                hash = (hash * 397) ^ TangentialSpeedMillimetersPerSecond;
                hash = (hash * 397) ^ Pressure;
                hash = (hash * 397) ^ Intensity;
                hash = (hash * 397) ^ ContactPointId;
                hash = (hash * 397) ^ BodyPartId;
                hash = (hash * 397) ^ (int)ActivationMode;
                return hash;
            }
        }

        public static int CreateContactPairKey(
            CombatPhysicalContactKind kind,
            CombatEntityId sourceEntityId,
            CombatBodyId sourceBodyId,
            CombatColliderId sourceColliderId,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId,
            int sourceSlotId,
            int contactPointId = 0,
            int bodyPartId = 0)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (int)kind;
                hash = (hash * 31) + sourceEntityId.Value;
                hash = (hash * 31) + sourceBodyId.Value;
                hash = (hash * 31) + sourceColliderId.Value;
                hash = (hash * 31) + targetBodyId.Value;
                hash = (hash * 31) + targetColliderId.Value;
                hash = (hash * 31) + sourceSlotId;
                hash = (hash * 31) + contactPointId;
                hash = (hash * 31) + bodyPartId;
                hash &= int.MaxValue;
                return hash == 0 ? 1 : hash;
            }
        }

        private static int ClampNonNegative(int value)
        {
            return value < 0 ? 0 : value;
        }
    }
}

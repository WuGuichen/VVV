using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public enum CombatGroundProbeStatus
    {
        InvalidInput = 0,
        Success = 1,
        NoHit = 2,
        TooSteep = 3,
        OutOfRange = 4,
        UnsupportedShape = 5,
        SurfaceIdentityUnavailable = 6,
        SurfaceMaterialUnavailable = 7,
    }

    /// <summary>Fixed-point world-space input for one static ground probe.</summary>
    public readonly struct CombatGroundProbeQuery : IEquatable<CombatGroundProbeQuery>
    {
        public CombatGroundProbeQuery(
            int probeId,
            CombatEntityId sourceEntityId,
            int sourceOrder,
            FixVector3 origin,
            FixVector3 direction,
            Fix64 distance,
            Fix64 radius,
            CombatPhysicsLayerMask staticLayerMask,
            Fix64 minimumSlopeCosine)
        {
            ProbeId = probeId;
            SourceEntityId = sourceEntityId;
            SourceOrder = sourceOrder;
            Origin = origin;
            Direction = direction;
            Distance = distance;
            Radius = radius;
            StaticLayerMask = staticLayerMask;
            MinimumSlopeCosine = minimumSlopeCosine;
        }

        public int ProbeId { get; }

        public CombatEntityId SourceEntityId { get; }

        public int SourceOrder { get; }

        public FixVector3 Origin { get; }

        public FixVector3 Direction { get; }

        public Fix64 Distance { get; }

        public Fix64 Radius { get; }

        /// <summary>
        /// Caller-owned mask containing only static surface layers. Combat does not assign project layer semantics.
        /// </summary>
        public CombatPhysicsLayerMask StaticLayerMask { get; }

        public Fix64 MinimumSlopeCosine { get; }

        public bool Equals(CombatGroundProbeQuery other)
        {
            return ProbeId == other.ProbeId
                && SourceEntityId.Equals(other.SourceEntityId)
                && SourceOrder == other.SourceOrder
                && Origin.Equals(other.Origin)
                && Direction.Equals(other.Direction)
                && Distance.Equals(other.Distance)
                && Radius.Equals(other.Radius)
                && StaticLayerMask.Equals(other.StaticLayerMask)
                && MinimumSlopeCosine.Equals(other.MinimumSlopeCosine);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatGroundProbeQuery other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ProbeId;
                hash = (hash * 397) ^ SourceEntityId.Value;
                hash = (hash * 397) ^ SourceOrder;
                hash = (hash * 397) ^ Origin.GetHashCode();
                hash = (hash * 397) ^ Direction.GetHashCode();
                hash = (hash * 397) ^ Distance.GetHashCode();
                hash = (hash * 397) ^ Radius.GetHashCode();
                hash = (hash * 397) ^ StaticLayerMask.Value;
                hash = (hash * 397) ^ MinimumSlopeCosine.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>One quantized, stable static surface hit.</summary>
    public readonly struct CombatGroundProbeHit : IEquatable<CombatGroundProbeHit>
    {
        internal CombatGroundProbeHit(
            CombatEntityId surfaceEntityId,
            CombatBodyId surfaceBodyId,
            CombatColliderId surfaceColliderId,
            CombatColliderId surfaceLeafId,
            CombatPhysicsShapeKind surfaceShapeKind,
            Fix64 distance,
            FixVector3 point,
            FixVector3 normal,
            Fix64 slopeCosine,
            CombatSurfaceMaterial surfaceMaterial,
            int surfacePriority)
        {
            SurfaceEntityId = surfaceEntityId;
            SurfaceBodyId = surfaceBodyId;
            SurfaceColliderId = surfaceColliderId;
            SurfaceLeafId = surfaceLeafId;
            SurfaceShapeKind = surfaceShapeKind;
            Distance = distance;
            Point = point;
            Normal = normal;
            SlopeCosine = slopeCosine;
            SurfaceMaterial = surfaceMaterial;
            SurfacePriority = surfacePriority;
        }

        public CombatEntityId SurfaceEntityId { get; }

        public CombatBodyId SurfaceBodyId { get; }

        public CombatColliderId SurfaceColliderId { get; }

        /// <summary>Compound leaf id, or None for a primitive collider.</summary>
        public CombatColliderId SurfaceLeafId { get; }

        public bool IsCompoundLeaf => !SurfaceLeafId.IsNone;

        public CombatPhysicsShapeKind SurfaceShapeKind { get; }

        /// <summary>Distance quantized to one millimeter.</summary>
        public Fix64 Distance { get; }

        /// <summary>World point quantized to one millimeter per component.</summary>
        public FixVector3 Point { get; }

        /// <summary>World normal quantized to signed 1/32767 components and then normalized.</summary>
        public FixVector3 Normal { get; }

        /// <summary>Quantized dot product between the world normal and +Y.</summary>
        public Fix64 SlopeCosine { get; }

        public CombatSurfaceMaterial SurfaceMaterial { get; }

        public int SurfacePriority { get; }

        public bool Equals(CombatGroundProbeHit other)
        {
            return SurfaceEntityId.Equals(other.SurfaceEntityId)
                && SurfaceBodyId.Equals(other.SurfaceBodyId)
                && SurfaceColliderId.Equals(other.SurfaceColliderId)
                && SurfaceLeafId.Equals(other.SurfaceLeafId)
                && SurfaceShapeKind == other.SurfaceShapeKind
                && Distance.Equals(other.Distance)
                && Point.Equals(other.Point)
                && Normal.Equals(other.Normal)
                && SlopeCosine.Equals(other.SlopeCosine)
                && SurfaceMaterial.Equals(other.SurfaceMaterial)
                && SurfacePriority == other.SurfacePriority;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatGroundProbeHit other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SurfaceEntityId.Value;
                hash = (hash * 397) ^ SurfaceBodyId.Value;
                hash = (hash * 397) ^ SurfaceColliderId.Value;
                hash = (hash * 397) ^ SurfaceLeafId.Value;
                hash = (hash * 397) ^ (int)SurfaceShapeKind;
                hash = (hash * 397) ^ Distance.GetHashCode();
                hash = (hash * 397) ^ Point.GetHashCode();
                hash = (hash * 397) ^ Normal.GetHashCode();
                hash = (hash * 397) ^ SlopeCosine.GetHashCode();
                hash = (hash * 397) ^ SurfaceMaterial.GetHashCode();
                hash = (hash * 397) ^ SurfacePriority;
                return hash;
            }
        }
    }

    public readonly struct CombatGroundProbeResult : IEquatable<CombatGroundProbeResult>
    {
        private CombatGroundProbeResult(
            CombatGroundProbeStatus status,
            bool hasHit,
            CombatGroundProbeHit hit)
        {
            Status = status;
            HasHit = hasHit;
            Hit = hit;
        }

        public CombatGroundProbeStatus Status { get; }

        public bool Succeeded => Status == CombatGroundProbeStatus.Success;

        /// <summary>True only for Success; failures never expose a fabricated target.</summary>
        public bool HasHit { get; }

        public CombatGroundProbeHit Hit { get; }

        internal static CombatGroundProbeResult Success(in CombatGroundProbeHit hit)
        {
            return new CombatGroundProbeResult(CombatGroundProbeStatus.Success, true, hit);
        }

        internal static CombatGroundProbeResult Failure(CombatGroundProbeStatus status)
        {
            return new CombatGroundProbeResult(status, false, default);
        }

        public bool Equals(CombatGroundProbeResult other)
        {
            return Status == other.Status
                && HasHit == other.HasHit
                && Hit.Equals(other.Hit);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatGroundProbeResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (((int)Status * 397) ^ (HasHit ? 1 : 0)) * 397 ^ Hit.GetHashCode();
            }
        }
    }

    /// <summary>Complete stable order for quantized ground hits.</summary>
    public sealed class CombatGroundProbeHitComparer : IComparer<CombatGroundProbeHit>
    {
        public static readonly CombatGroundProbeHitComparer Instance = new CombatGroundProbeHitComparer();

        private CombatGroundProbeHitComparer()
        {
        }

        public int Compare(CombatGroundProbeHit left, CombatGroundProbeHit right)
        {
            int compare = left.Distance.RawValue.CompareTo(right.Distance.RawValue);
            if (compare != 0) return compare;

            compare = right.SurfacePriority.CompareTo(left.SurfacePriority);
            if (compare != 0) return compare;

            compare = left.SurfaceEntityId.Value.CompareTo(right.SurfaceEntityId.Value);
            if (compare != 0) return compare;

            compare = left.SurfaceBodyId.Value.CompareTo(right.SurfaceBodyId.Value);
            if (compare != 0) return compare;

            compare = left.SurfaceColliderId.Value.CompareTo(right.SurfaceColliderId.Value);
            if (compare != 0) return compare;

            compare = left.SurfaceLeafId.Value.CompareTo(right.SurfaceLeafId.Value);
            if (compare != 0) return compare;

            compare = ((int)left.SurfaceShapeKind).CompareTo((int)right.SurfaceShapeKind);
            if (compare != 0) return compare;

            compare = CompareVector(left.Point, right.Point);
            if (compare != 0) return compare;

            compare = CompareVector(left.Normal, right.Normal);
            if (compare != 0) return compare;

            compare = left.SlopeCosine.RawValue.CompareTo(right.SlopeCosine.RawValue);
            if (compare != 0) return compare;

            compare = ((int)left.SurfaceMaterial.Kind).CompareTo((int)right.SurfaceMaterial.Kind);
            if (compare != 0) return compare;

            compare = left.SurfaceMaterial.Friction.RawValue.CompareTo(right.SurfaceMaterial.Friction.RawValue);
            if (compare != 0) return compare;

            compare = left.SurfaceMaterial.Elasticity.RawValue.CompareTo(right.SurfaceMaterial.Elasticity.RawValue);
            if (compare != 0) return compare;

            return left.SurfaceMaterial.FixedBounceHeight.RawValue.CompareTo(
                right.SurfaceMaterial.FixedBounceHeight.RawValue);
        }

        private static int CompareVector(FixVector3 left, FixVector3 right)
        {
            int compare = left.X.RawValue.CompareTo(right.X.RawValue);
            if (compare != 0) return compare;
            compare = left.Y.RawValue.CompareTo(right.Y.RawValue);
            return compare != 0 ? compare : left.Z.RawValue.CompareTo(right.Z.RawValue);
        }
    }
}

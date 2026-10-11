using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Shields
{
    public readonly struct CombatShieldImpactRequest : IEquatable<CombatShieldImpactRequest>
    {
        public CombatShieldImpactRequest(
            CombatEntityId shieldEntityId,
            CombatEntityId sourceEntityId,
            CombatShieldImpactSourceKind sourceKind,
            CombatFrame frame,
            int actionId,
            int actionInstanceId,
            int traceId,
            int damage,
            int impactPower,
            CombatShieldPotencyTriplet delta,
            FixVector3 hitPointWorld,
            FixVector3 hitNormalWorld)
        {
            if (shieldEntityId.IsNone)
                throw new ArgumentException("Shield impact request requires a shield entity id.", nameof(shieldEntityId));
            if (!Enum.IsDefined(typeof(CombatShieldImpactSourceKind), sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind), "Shield impact source kind is not defined.");
            if (frame.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(frame), "Shield impact frame cannot be negative.");
            if (actionId < 0)
                throw new ArgumentOutOfRangeException(nameof(actionId), "Shield impact action id cannot be negative.");
            if (actionInstanceId < 0)
                throw new ArgumentOutOfRangeException(nameof(actionInstanceId), "Shield impact action instance id cannot be negative.");
            if (traceId < 0)
                throw new ArgumentOutOfRangeException(nameof(traceId), "Shield impact trace id cannot be negative.");
            if (damage < 0)
                throw new ArgumentOutOfRangeException(nameof(damage), "Shield impact damage cannot be negative.");
            if (impactPower < 0)
                throw new ArgumentOutOfRangeException(nameof(impactPower), "Shield impact power cannot be negative.");

            ShieldEntityId = shieldEntityId;
            SourceEntityId = sourceEntityId;
            SourceKind = sourceKind;
            Frame = frame;
            ActionId = actionId;
            ActionInstanceId = actionInstanceId;
            TraceId = traceId;
            Damage = damage;
            ImpactPower = impactPower;
            Delta = delta;
            HitPointWorld = hitPointWorld;
            HitNormalWorld = hitNormalWorld;
        }

        public CombatEntityId ShieldEntityId { get; }
        public CombatEntityId SourceEntityId { get; }
        public CombatShieldImpactSourceKind SourceKind { get; }
        public CombatFrame Frame { get; }
        public int ActionId { get; }
        public int ActionInstanceId { get; }
        public int TraceId { get; }
        public int Damage { get; }
        public int ImpactPower { get; }
        public CombatShieldPotencyTriplet Delta { get; }
        public FixVector3 HitPointWorld { get; }
        public FixVector3 HitNormalWorld { get; }

        public static CombatShieldImpactRequest FromQueryHit(
            CombatQueryResult hit,
            CombatShieldImpactSourceKind sourceKind,
            CombatFrame frame,
            int actionInstanceId,
            int damage,
            int impactPower,
            CombatShieldPotencyTriplet delta)
        {
            return new CombatShieldImpactRequest(
                hit.TargetEntityId,
                hit.Query.SourceEntityId,
                sourceKind,
                frame,
                hit.Query.ActionId,
                actionInstanceId,
                hit.Query.TraceId,
                damage,
                impactPower,
                delta,
                hit.Point,
                hit.Normal);
        }

        public bool Equals(CombatShieldImpactRequest other)
        {
            return ShieldEntityId.Equals(other.ShieldEntityId)
                && SourceEntityId.Equals(other.SourceEntityId)
                && SourceKind == other.SourceKind
                && Frame.Equals(other.Frame)
                && ActionId == other.ActionId
                && ActionInstanceId == other.ActionInstanceId
                && TraceId == other.TraceId
                && Damage == other.Damage
                && ImpactPower == other.ImpactPower
                && Delta.Equals(other.Delta)
                && HitPointWorld.Equals(other.HitPointWorld)
                && HitNormalWorld.Equals(other.HitNormalWorld);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatShieldImpactRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ShieldEntityId.Value;
                hash = (hash * 397) ^ SourceEntityId.Value;
                hash = (hash * 397) ^ (int)SourceKind;
                hash = (hash * 397) ^ Frame.Value;
                hash = (hash * 397) ^ ActionId;
                hash = (hash * 397) ^ ActionInstanceId;
                hash = (hash * 397) ^ TraceId;
                hash = (hash * 397) ^ Damage;
                hash = (hash * 397) ^ ImpactPower;
                hash = (hash * 397) ^ Delta.GetHashCode();
                hash = (hash * 397) ^ HitPointWorld.GetHashCode();
                hash = (hash * 397) ^ HitNormalWorld.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct CombatShieldBlockEvent : IEquatable<CombatShieldBlockEvent>
    {
        public CombatShieldBlockEvent(
            CombatShieldImpactRequest request,
            CombatEntityId ownerEntityId,
            CombatShieldPotencyTriplet potencyBefore,
            CombatShieldPotencyTriplet potencyAfter,
            int pressureBefore,
            int pressureAfter,
            int currentCohesionLimit,
            CombatShieldPressureBand bandBefore,
            CombatShieldPressureBand bandAfter,
            bool triggeredBreak)
        {
            if (ownerEntityId.IsNone)
                throw new ArgumentException("Shield block event requires an owner entity id.", nameof(ownerEntityId));
            if (pressureBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(pressureBefore), "Shield pressure cannot be negative.");
            if (pressureAfter < 0)
                throw new ArgumentOutOfRangeException(nameof(pressureAfter), "Shield pressure cannot be negative.");
            if (currentCohesionLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(currentCohesionLimit), "Shield cohesion limit must be positive.");
            if (!CombatShieldPotencyComponent.IsDefined(bandBefore))
                throw new ArgumentOutOfRangeException(nameof(bandBefore), "Shield pressure band is not defined.");
            if (!CombatShieldPotencyComponent.IsDefined(bandAfter))
                throw new ArgumentOutOfRangeException(nameof(bandAfter), "Shield pressure band is not defined.");

            Request = request;
            OwnerEntityId = ownerEntityId;
            PotencyBefore = potencyBefore;
            PotencyAfter = potencyAfter;
            PressureBefore = pressureBefore;
            PressureAfter = pressureAfter;
            CurrentCohesionLimit = currentCohesionLimit;
            BandBefore = bandBefore;
            BandAfter = bandAfter;
            TriggeredBreak = triggeredBreak;
        }

        public CombatShieldImpactRequest Request { get; }
        public CombatEntityId OwnerEntityId { get; }
        public CombatShieldPotencyTriplet PotencyBefore { get; }
        public CombatShieldPotencyTriplet PotencyAfter { get; }
        public int PressureBefore { get; }
        public int PressureAfter { get; }
        public int CurrentCohesionLimit { get; }
        public CombatShieldPressureBand BandBefore { get; }
        public CombatShieldPressureBand BandAfter { get; }
        public bool TriggeredBreak { get; }
        public CombatEntityId ShieldEntityId => Request.ShieldEntityId;
        public CombatEntityId SourceEntityId => Request.SourceEntityId;
        public CombatFrame Frame => Request.Frame;

        public bool Equals(CombatShieldBlockEvent other)
        {
            return Request.Equals(other.Request)
                && OwnerEntityId.Equals(other.OwnerEntityId)
                && PotencyBefore.Equals(other.PotencyBefore)
                && PotencyAfter.Equals(other.PotencyAfter)
                && PressureBefore == other.PressureBefore
                && PressureAfter == other.PressureAfter
                && CurrentCohesionLimit == other.CurrentCohesionLimit
                && BandBefore == other.BandBefore
                && BandAfter == other.BandAfter
                && TriggeredBreak == other.TriggeredBreak;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatShieldBlockEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Request.GetHashCode();
                hash = (hash * 397) ^ OwnerEntityId.Value;
                hash = (hash * 397) ^ PotencyBefore.GetHashCode();
                hash = (hash * 397) ^ PotencyAfter.GetHashCode();
                hash = (hash * 397) ^ PressureBefore;
                hash = (hash * 397) ^ PressureAfter;
                hash = (hash * 397) ^ CurrentCohesionLimit;
                hash = (hash * 397) ^ (int)BandBefore;
                hash = (hash * 397) ^ (int)BandAfter;
                hash = (hash * 397) ^ (TriggeredBreak ? 1 : 0);
                return hash;
            }
        }
    }

    public readonly struct CombatShieldPotencyChangedEvent : IEquatable<CombatShieldPotencyChangedEvent>
    {
        public CombatShieldPotencyChangedEvent(
            CombatEntityId shieldEntityId,
            CombatEntityId ownerEntityId,
            CombatFrame frame,
            CombatShieldPotencyChangeReason reason,
            CombatShieldPotencyTriplet potencyBefore,
            CombatShieldPotencyTriplet potencyAfter,
            int pressureBefore,
            int pressureAfter,
            CombatShieldPressureBand bandBefore,
            CombatShieldPressureBand bandAfter,
            int traceId = 0)
        {
            if (shieldEntityId.IsNone)
                throw new ArgumentException("Shield potency changed event requires a shield entity id.", nameof(shieldEntityId));
            if (ownerEntityId.IsNone)
                throw new ArgumentException("Shield potency changed event requires an owner entity id.", nameof(ownerEntityId));
            if (frame.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(frame), "Shield event frame cannot be negative.");
            if (!Enum.IsDefined(typeof(CombatShieldPotencyChangeReason), reason))
                throw new ArgumentOutOfRangeException(nameof(reason), "Shield potency change reason is not defined.");
            if (pressureBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(pressureBefore), "Shield pressure cannot be negative.");
            if (pressureAfter < 0)
                throw new ArgumentOutOfRangeException(nameof(pressureAfter), "Shield pressure cannot be negative.");
            if (!CombatShieldPotencyComponent.IsDefined(bandBefore))
                throw new ArgumentOutOfRangeException(nameof(bandBefore), "Shield pressure band is not defined.");
            if (!CombatShieldPotencyComponent.IsDefined(bandAfter))
                throw new ArgumentOutOfRangeException(nameof(bandAfter), "Shield pressure band is not defined.");
            if (traceId < 0)
                throw new ArgumentOutOfRangeException(nameof(traceId), "Shield event trace id cannot be negative.");

            ShieldEntityId = shieldEntityId;
            OwnerEntityId = ownerEntityId;
            Frame = frame;
            Reason = reason;
            PotencyBefore = potencyBefore;
            PotencyAfter = potencyAfter;
            PressureBefore = pressureBefore;
            PressureAfter = pressureAfter;
            BandBefore = bandBefore;
            BandAfter = bandAfter;
            TraceId = traceId;
        }

        public CombatEntityId ShieldEntityId { get; }
        public CombatEntityId OwnerEntityId { get; }
        public CombatFrame Frame { get; }
        public CombatShieldPotencyChangeReason Reason { get; }
        public CombatShieldPotencyTriplet PotencyBefore { get; }
        public CombatShieldPotencyTriplet PotencyAfter { get; }
        public int PressureBefore { get; }
        public int PressureAfter { get; }
        public CombatShieldPressureBand BandBefore { get; }
        public CombatShieldPressureBand BandAfter { get; }
        public int TraceId { get; }

        public bool Equals(CombatShieldPotencyChangedEvent other)
        {
            return ShieldEntityId.Equals(other.ShieldEntityId)
                && OwnerEntityId.Equals(other.OwnerEntityId)
                && Frame.Equals(other.Frame)
                && Reason == other.Reason
                && PotencyBefore.Equals(other.PotencyBefore)
                && PotencyAfter.Equals(other.PotencyAfter)
                && PressureBefore == other.PressureBefore
                && PressureAfter == other.PressureAfter
                && BandBefore == other.BandBefore
                && BandAfter == other.BandAfter
                && TraceId == other.TraceId;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatShieldPotencyChangedEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ShieldEntityId.Value;
                hash = (hash * 397) ^ OwnerEntityId.Value;
                hash = (hash * 397) ^ Frame.Value;
                hash = (hash * 397) ^ (int)Reason;
                hash = (hash * 397) ^ PotencyBefore.GetHashCode();
                hash = (hash * 397) ^ PotencyAfter.GetHashCode();
                hash = (hash * 397) ^ PressureBefore;
                hash = (hash * 397) ^ PressureAfter;
                hash = (hash * 397) ^ (int)BandBefore;
                hash = (hash * 397) ^ (int)BandAfter;
                hash = (hash * 397) ^ TraceId;
                return hash;
            }
        }
    }

    public readonly struct CombatShieldBreakEvent : IEquatable<CombatShieldBreakEvent>
    {
        public CombatShieldBreakEvent(
            CombatEntityId shieldEntityId,
            CombatEntityId ownerEntityId,
            CombatEntityId sourceEntityId,
            CombatFrame frame,
            int pressure,
            int currentCohesionLimit,
            int traceId)
        {
            if (shieldEntityId.IsNone)
                throw new ArgumentException("Shield break event requires a shield entity id.", nameof(shieldEntityId));
            if (ownerEntityId.IsNone)
                throw new ArgumentException("Shield break event requires an owner entity id.", nameof(ownerEntityId));
            if (frame.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(frame), "Shield break frame cannot be negative.");
            if (pressure < 0)
                throw new ArgumentOutOfRangeException(nameof(pressure), "Shield break pressure cannot be negative.");
            if (currentCohesionLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(currentCohesionLimit), "Shield cohesion limit must be positive.");
            if (traceId < 0)
                throw new ArgumentOutOfRangeException(nameof(traceId), "Shield break trace id cannot be negative.");

            ShieldEntityId = shieldEntityId;
            OwnerEntityId = ownerEntityId;
            SourceEntityId = sourceEntityId;
            Frame = frame;
            Pressure = pressure;
            CurrentCohesionLimit = currentCohesionLimit;
            TraceId = traceId;
        }

        public CombatEntityId ShieldEntityId { get; }
        public CombatEntityId OwnerEntityId { get; }
        public CombatEntityId SourceEntityId { get; }
        public CombatFrame Frame { get; }
        public int Pressure { get; }
        public int CurrentCohesionLimit { get; }
        public int TraceId { get; }

        public bool Equals(CombatShieldBreakEvent other)
        {
            return ShieldEntityId.Equals(other.ShieldEntityId)
                && OwnerEntityId.Equals(other.OwnerEntityId)
                && SourceEntityId.Equals(other.SourceEntityId)
                && Frame.Equals(other.Frame)
                && Pressure == other.Pressure
                && CurrentCohesionLimit == other.CurrentCohesionLimit
                && TraceId == other.TraceId;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatShieldBreakEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ShieldEntityId.Value;
                hash = (hash * 397) ^ OwnerEntityId.Value;
                hash = (hash * 397) ^ SourceEntityId.Value;
                hash = (hash * 397) ^ Frame.Value;
                hash = (hash * 397) ^ Pressure;
                hash = (hash * 397) ^ CurrentCohesionLimit;
                hash = (hash * 397) ^ TraceId;
                return hash;
            }
        }
    }
}

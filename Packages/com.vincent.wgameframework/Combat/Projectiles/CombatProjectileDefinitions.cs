using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Combat.Projectiles
{
    public enum CombatProjectileImpactPolicyKind
    {
        DirectHit = 0,
        AreaPulseOnImpact = 1,
    }

    public enum CombatProjectileHitSameTargetPolicy
    {
        AllowRepeat = 0,
        OncePerProjectile = 1,
    }

    public enum CombatProjectileMotionKind
    {
        Linear = 0,
        ArcToTarget = 1,
        BoomerangReturn = 2,
        SwayForward = 3,
        HomingToTarget = 4,
        BezierToTarget = 5,
        BallisticArcToTarget = 6,
    }

    public enum CombatProjectileSpeedProfileKind
    {
        Constant = 0,
        EaseInQuadratic = 1,
        EaseOutQuadratic = 2,
        EaseInOutSmoothstep = 3,
    }

    public sealed class CombatProjectileDefinitionRegistry
    {
        private readonly Dictionary<int, CombatProjectileDefinition> _byId =
            new Dictionary<int, CombatProjectileDefinition>();
        private readonly Dictionary<string, CombatProjectileDefinition> _byStableId =
            new Dictionary<string, CombatProjectileDefinition>(StringComparer.Ordinal);
        private bool _stableHashDirty = true;
        private long _stableHash;

        public CombatProjectileDefinitionRegistry()
        {
        }

        public CombatProjectileDefinitionRegistry(IEnumerable<CombatProjectileDefinition> definitions)
        {
            if (definitions == null)
                return;

            foreach (CombatProjectileDefinition definition in definitions)
            {
                if (!TryRegister(definition, out string failure))
                    throw new ArgumentException(failure, nameof(definitions));
            }
        }

        public int Count => _byId.Count;

        public bool TryRegister(CombatProjectileDefinition definition, out string failure)
        {
            failure = string.Empty;
            if (definition == null)
            {
                failure = "Projectile definition cannot be null.";
                return false;
            }

            if (_byId.ContainsKey(definition.DefinitionId))
            {
                failure = "Duplicate projectile definition id: " + definition.DefinitionId + ".";
                return false;
            }

            if (_byStableId.ContainsKey(definition.StableId))
            {
                failure = "Duplicate projectile stable id: " + definition.StableId + ".";
                return false;
            }

            _byId.Add(definition.DefinitionId, definition);
            _byStableId.Add(definition.StableId, definition);
            _stableHashDirty = true;
            return true;
        }

        public bool TryGet(int definitionId, out CombatProjectileDefinition definition)
        {
            return _byId.TryGetValue(definitionId, out definition);
        }

        public bool TryGetByStableId(string stableId, out CombatProjectileDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                definition = null;
                return false;
            }

            return _byStableId.TryGetValue(stableId, out definition);
        }

        public CombatProjectileDefinition[] CreateSnapshot()
        {
            var result = new CombatProjectileDefinition[_byId.Count];
            int index = 0;
            foreach (KeyValuePair<int, CombatProjectileDefinition> pair in _byId)
                result[index++] = pair.Value;

            Array.Sort(result, CombatProjectileDefinitionComparer.Instance);
            return result;
        }

        public long ComputeStableHash()
        {
            if (!_stableHashDirty)
                return _stableHash;

            var accumulator = new RuntimeContentHashAccumulator();
            CombatProjectileDefinition[] definitions = CreateSnapshot();
            accumulator.AddInt("projectile.definition.count", definitions.Length);
            for (int i = 0; i < definitions.Length; i++)
                definitions[i].Contribute(accumulator, "projectile.definition." + i);

            _stableHash = accumulator.ToHash();
            _stableHashDirty = false;
            return _stableHash;
        }
    }

    public sealed class CombatProjectileDefinition
    {
        public CombatProjectileDefinition(
            int definitionId,
            string stableId,
            CombatProjectileMotionConfig motion,
            CombatProjectileCollisionConfig collision,
            CombatProjectileImpactConfig impact,
            bool physicalEventsEnabled = false)
        {
            if (definitionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(definitionId), "Projectile definition id must be positive.");
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("Projectile stable id cannot be null or empty.", nameof(stableId));

            DefinitionId = definitionId;
            StableId = stableId;
            StableLabelDigest = RuntimeHashDigest.ComputeContentDigest(StableId);
            Motion = motion;
            Collision = collision;
            Impact = impact;
            PhysicalEventsEnabled = physicalEventsEnabled;
        }

        public int DefinitionId { get; }
        public string StableId { get; }
        public RuntimeHashDigest StableLabelDigest { get; }
        public CombatProjectileMotionConfig Motion { get; }
        public CombatProjectileCollisionConfig Collision { get; }
        public CombatProjectileImpactConfig Impact { get; }
        public bool PhysicalEventsEnabled { get; }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddInt(prefix + ".definitionId", DefinitionId);
            accumulator.AddStringStable(prefix + ".stableId", StableId);
            accumulator.AddInt(prefix + ".physicalEventsEnabled", PhysicalEventsEnabled ? 1 : 0);
            Motion.Contribute(accumulator, prefix + ".motion");
            Collision.Contribute(accumulator, prefix + ".collision");
            Impact.Contribute(accumulator, prefix + ".impact");
        }
    }

    public readonly struct CombatProjectileMotionConfig : IEquatable<CombatProjectileMotionConfig>
    {
        public CombatProjectileMotionConfig(Fix64 speedPerFrame, int lifetimeFrames)
            : this(
                CombatProjectileMotionKind.Linear,
                speedPerFrame,
                lifetimeFrames,
                Fix64.Zero,
                CombatProjectileSpeedProfileKind.Constant)
        {
        }

        public CombatProjectileMotionConfig(
            CombatProjectileMotionKind kind,
            Fix64 speedPerFrame,
            int lifetimeFrames,
            Fix64 arcHeight = default)
            : this(
                kind,
                speedPerFrame,
                lifetimeFrames,
                arcHeight,
                CombatProjectileSpeedProfileKind.Constant)
        {
        }

        public CombatProjectileMotionConfig(
            CombatProjectileMotionKind kind,
            Fix64 speedPerFrame,
            int lifetimeFrames,
            Fix64 arcHeight,
            CombatProjectileSpeedProfileKind speedProfileKind)
            : this(
                kind,
                speedPerFrame,
                lifetimeFrames,
                arcHeight,
                speedProfileKind,
                Fix64.Zero,
                0,
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.One,
                Fix64.Zero,
                Fix64.Zero)
        {
        }

        public CombatProjectileMotionConfig(
            CombatProjectileMotionKind kind,
            Fix64 speedPerFrame,
            int lifetimeFrames,
            Fix64 arcHeight,
            CombatProjectileSpeedProfileKind speedProfileKind,
            Fix64 lateralAmplitude,
            int lateralCycles,
            FixVector3 bezierControlOffsetA = default,
            FixVector3 bezierControlOffsetB = default,
            Fix64 homingTurnRatePerFrame = default,
            Fix64 gravityPerFrameSquared = default,
            Fix64 maxLaunchAngleTangent = default)
        {
            if (!Enum.IsDefined(typeof(CombatProjectileMotionKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Projectile motion kind is not supported.");
            if (!Enum.IsDefined(typeof(CombatProjectileSpeedProfileKind), speedProfileKind))
                throw new ArgumentOutOfRangeException(nameof(speedProfileKind), "Projectile speed profile kind is not supported.");
            if (speedPerFrame <= Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(speedPerFrame), "Projectile speed must be positive.");
            if (lifetimeFrames <= 0)
                throw new ArgumentOutOfRangeException(nameof(lifetimeFrames), "Projectile lifetime frames must be positive.");
            if (kind == CombatProjectileMotionKind.ArcToTarget && arcHeight <= Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(arcHeight), "Arc projectile height must be positive.");
            if (kind != CombatProjectileMotionKind.ArcToTarget && arcHeight < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(arcHeight), "Projectile arc height cannot be negative.");
            if (kind == CombatProjectileMotionKind.BallisticArcToTarget && gravityPerFrameSquared <= Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(gravityPerFrameSquared), "Ballistic projectile gravity must be positive.");
            if (kind != CombatProjectileMotionKind.BallisticArcToTarget && gravityPerFrameSquared < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(gravityPerFrameSquared), "Projectile gravity cannot be negative.");
            if (maxLaunchAngleTangent < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxLaunchAngleTangent), "Projectile max launch angle tangent cannot be negative.");
            if (kind == CombatProjectileMotionKind.SwayForward && lateralAmplitude <= Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(lateralAmplitude), "Sway projectile lateralAmplitude must be positive.");
            if (kind == CombatProjectileMotionKind.SwayForward && lateralCycles <= 0)
                throw new ArgumentOutOfRangeException(nameof(lateralCycles), "Sway projectile lateralCycles must be positive.");
            if (kind == CombatProjectileMotionKind.SwayForward && lateralCycles > 1000)
                throw new ArgumentOutOfRangeException(nameof(lateralCycles), "Sway projectile lateralCycles are too high.");
            if (kind != CombatProjectileMotionKind.SwayForward && lateralAmplitude < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(lateralAmplitude), "Projectile lateralAmplitude cannot be negative.");
            if (kind != CombatProjectileMotionKind.SwayForward && lateralCycles < 0)
                throw new ArgumentOutOfRangeException(nameof(lateralCycles), "Projectile lateralCycles cannot be negative.");
            if (kind == CombatProjectileMotionKind.BezierToTarget
                && bezierControlOffsetA.Equals(FixVector3.Zero)
                && bezierControlOffsetB.Equals(FixVector3.Zero))
            {
                throw new ArgumentOutOfRangeException(nameof(bezierControlOffsetA), "Bezier projectile requires at least one non-zero control offset.");
            }
            if (homingTurnRatePerFrame < Fix64.Zero || homingTurnRatePerFrame > Fix64.One)
                throw new ArgumentOutOfRangeException(nameof(homingTurnRatePerFrame), "Projectile homing turn rate must be between 0 and 1.");
            if (kind == CombatProjectileMotionKind.HomingToTarget && homingTurnRatePerFrame <= Fix64.Zero)
                homingTurnRatePerFrame = Fix64.One;

            Kind = kind;
            SpeedPerFrame = speedPerFrame;
            LifetimeFrames = lifetimeFrames;
            ArcHeight = kind == CombatProjectileMotionKind.ArcToTarget ? arcHeight : Fix64.Zero;
            SpeedProfileKind = speedProfileKind;
            LateralAmplitude = kind == CombatProjectileMotionKind.SwayForward ? lateralAmplitude : Fix64.Zero;
            LateralCycles = kind == CombatProjectileMotionKind.SwayForward ? lateralCycles : 0;
            BezierControlOffsetA = kind == CombatProjectileMotionKind.BezierToTarget ? bezierControlOffsetA : FixVector3.Zero;
            BezierControlOffsetB = kind == CombatProjectileMotionKind.BezierToTarget ? bezierControlOffsetB : FixVector3.Zero;
            HomingTurnRatePerFrame = kind == CombatProjectileMotionKind.HomingToTarget
                ? homingTurnRatePerFrame
                : Fix64.Zero;
            GravityPerFrameSquared = kind == CombatProjectileMotionKind.BallisticArcToTarget
                ? gravityPerFrameSquared
                : Fix64.Zero;
            MaxLaunchAngleTangent = kind == CombatProjectileMotionKind.BallisticArcToTarget
                ? maxLaunchAngleTangent
                : Fix64.Zero;
        }

        public CombatProjectileMotionKind Kind { get; }
        public Fix64 SpeedPerFrame { get; }
        public int LifetimeFrames { get; }
        public Fix64 ArcHeight { get; }
        public CombatProjectileSpeedProfileKind SpeedProfileKind { get; }
        public Fix64 LateralAmplitude { get; }
        public int LateralCycles { get; }
        public FixVector3 BezierControlOffsetA { get; }
        public FixVector3 BezierControlOffsetB { get; }
        public Fix64 HomingTurnRatePerFrame { get; }
        public Fix64 GravityPerFrameSquared { get; }
        public Fix64 MaxLaunchAngleTangent { get; }

        public Fix64 EvaluateProgress(int ageFrames)
        {
            return EvaluateProgress(ageFrames, LifetimeFrames);
        }

        public Fix64 EvaluateProgress(int ageFrames, int lifetimeFrames)
        {
            int life = Math.Max(1, lifetimeFrames);
            int clampedAge = Math.Max(0, Math.Min(ageFrames, life));
            Fix64 t = Fix64.FromRatio(clampedAge, life);
            switch (SpeedProfileKind)
            {
                case CombatProjectileSpeedProfileKind.EaseInQuadratic:
                    return t * t;
                case CombatProjectileSpeedProfileKind.EaseOutQuadratic:
                {
                    Fix64 inverse = Fix64.One - t;
                    return Fix64.One - inverse * inverse;
                }
                case CombatProjectileSpeedProfileKind.EaseInOutSmoothstep:
                    return t * t * (Fix64.FromInt(3) - Fix64.FromInt(2) * t);
                case CombatProjectileSpeedProfileKind.Constant:
                default:
                    return t;
            }
        }

        public bool Equals(CombatProjectileMotionConfig other)
        {
            return Kind == other.Kind
                && SpeedPerFrame.Equals(other.SpeedPerFrame)
                && LifetimeFrames == other.LifetimeFrames
                && ArcHeight.Equals(other.ArcHeight)
                && SpeedProfileKind == other.SpeedProfileKind
                && LateralAmplitude.Equals(other.LateralAmplitude)
                && LateralCycles == other.LateralCycles
                && BezierControlOffsetA.Equals(other.BezierControlOffsetA)
                && BezierControlOffsetB.Equals(other.BezierControlOffsetB)
                && HomingTurnRatePerFrame.Equals(other.HomingTurnRatePerFrame)
                && GravityPerFrameSquared.Equals(other.GravityPerFrameSquared)
                && MaxLaunchAngleTangent.Equals(other.MaxLaunchAngleTangent);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileMotionConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ SpeedPerFrame.GetHashCode();
                hash = (hash * 397) ^ LifetimeFrames;
                hash = (hash * 397) ^ ArcHeight.GetHashCode();
                hash = (hash * 397) ^ (int)SpeedProfileKind;
                hash = (hash * 397) ^ LateralAmplitude.GetHashCode();
                hash = (hash * 397) ^ LateralCycles;
                hash = (hash * 397) ^ BezierControlOffsetA.GetHashCode();
                hash = (hash * 397) ^ BezierControlOffsetB.GetHashCode();
                hash = (hash * 397) ^ HomingTurnRatePerFrame.GetHashCode();
                hash = (hash * 397) ^ GravityPerFrameSquared.GetHashCode();
                hash = (hash * 397) ^ MaxLaunchAngleTangent.GetHashCode();
                return hash;
            }
        }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddInt(prefix + ".kind", (int)Kind);
            accumulator.AddLong(prefix + ".speedPerFrame.raw", SpeedPerFrame.RawValue);
            accumulator.AddInt(prefix + ".lifetimeFrames", LifetimeFrames);
            accumulator.AddLong(prefix + ".arcHeight.raw", ArcHeight.RawValue);
            accumulator.AddInt(prefix + ".speedProfile.kind", (int)SpeedProfileKind);
            accumulator.AddLong(prefix + ".lateralAmplitude.raw", LateralAmplitude.RawValue);
            accumulator.AddInt(prefix + ".lateralCycles", LateralCycles);
            accumulator.AddLong(prefix + ".bezierControlOffsetA.x.raw", BezierControlOffsetA.X.RawValue);
            accumulator.AddLong(prefix + ".bezierControlOffsetA.y.raw", BezierControlOffsetA.Y.RawValue);
            accumulator.AddLong(prefix + ".bezierControlOffsetA.z.raw", BezierControlOffsetA.Z.RawValue);
            accumulator.AddLong(prefix + ".bezierControlOffsetB.x.raw", BezierControlOffsetB.X.RawValue);
            accumulator.AddLong(prefix + ".bezierControlOffsetB.y.raw", BezierControlOffsetB.Y.RawValue);
            accumulator.AddLong(prefix + ".bezierControlOffsetB.z.raw", BezierControlOffsetB.Z.RawValue);
            accumulator.AddLong(prefix + ".homing.turnRatePerFrame.raw", HomingTurnRatePerFrame.RawValue);
            accumulator.AddLong(prefix + ".gravityPerFrameSquared.raw", GravityPerFrameSquared.RawValue);
            accumulator.AddLong(prefix + ".maxLaunchAngleTangent.raw", MaxLaunchAngleTangent.RawValue);
        }
    }

    /// <summary>
    /// Deterministic fixed-point helpers for gameplay-gravity projectile arcs.
    /// </summary>
    public static class CombatProjectileBallistics
    {
        /// <summary>
        /// Calculates the same-height maximum range for a ballistic projectile.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public static Fix64 CalculateSameHeightMaxRange(
            Fix64 speedPerFrame,
            Fix64 gravityPerFrameSquared,
            Fix64 maxLaunchAngleTangent = default)
        {
            if (speedPerFrame <= Fix64.Zero || gravityPerFrameSquared <= Fix64.Zero)
                return Fix64.Zero;

            Fix64 unconstrained = speedPerFrame * speedPerFrame / gravityPerFrameSquared;
            if (maxLaunchAngleTangent <= Fix64.Zero || maxLaunchAngleTangent >= Fix64.One)
                return unconstrained;

            Fix64 tangentSquared = maxLaunchAngleTangent * maxLaunchAngleTangent;
            Fix64 factor = Fix64.FromInt(2) * maxLaunchAngleTangent / (Fix64.One + tangentSquared);
            return unconstrained * factor;
        }

        /// <summary>
        /// Calculates the max-speed angle tangent that maximizes horizontal reach for this height delta.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public static bool TryCalculateRangeOptimalLaunchAngleTangent(
            Fix64 maxSpeedPerFrame,
            Fix64 gravityPerFrameSquared,
            Fix64 heightDelta,
            out Fix64 tangent)
        {
            tangent = Fix64.Zero;
            if (maxSpeedPerFrame <= Fix64.Zero || gravityPerFrameSquared <= Fix64.Zero)
                return false;

            Fix64 speedSquared = maxSpeedPerFrame * maxSpeedPerFrame;
            Fix64 heightReach = speedSquared - Fix64.FromInt(2) * gravityPerFrameSquared * heightDelta;
            if (heightReach <= Fix64.Zero)
                return false;

            Fix64 heightReachRoot = heightReach.Sqrt();
            if (heightReachRoot <= Fix64.Zero)
                return false;

            tangent = maxSpeedPerFrame / heightReachRoot;
            return tangent > Fix64.Zero;
        }

        /// <summary>
        /// Solves a deterministic launch vector using max speed, gameplay gravity, and a range-optimal angle cap.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public static bool TrySolveLaunchArc(
            FixVector3 origin,
            FixVector3 targetPoint,
            Fix64 maxSpeedPerFrame,
            Fix64 gravityPerFrameSquared,
            Fix64 maxLaunchAngleTangent,
            int maxLifetimeFrames,
            out FixVector3 initialVelocity,
            out Fix64 launchSpeedPerFrame,
            out int effectiveLifetimeFrames)
        {
            initialVelocity = FixVector3.Zero;
            launchSpeedPerFrame = Fix64.Zero;
            effectiveLifetimeFrames = 0;
            if (maxSpeedPerFrame <= Fix64.Zero || gravityPerFrameSquared <= Fix64.Zero || maxLifetimeFrames <= 0)
                return false;

            FixVector3 displacement = targetPoint - origin;
            var horizontal = new FixVector3(displacement.X, Fix64.Zero, displacement.Z);
            Fix64 horizontalDistanceSquared = horizontal.LengthSquared();
            if (horizontalDistanceSquared <= Fix64.Zero)
                return false;

            Fix64 horizontalDistance = horizontalDistanceSquared.Sqrt();
            if (!TryCalculateRangeOptimalLaunchAngleTangent(
                    maxSpeedPerFrame,
                    gravityPerFrameSquared,
                    displacement.Y,
                    out Fix64 rangeOptimalTangent))
                return false;

            Fix64 tanTheta = maxLaunchAngleTangent > Fix64.Zero && maxLaunchAngleTangent < rangeOptimalTangent
                ? maxLaunchAngleTangent
                : rangeOptimalTangent;
            if (tanTheta <= Fix64.Zero)
                return false;

            Fix64 denominator = Fix64.FromInt(2) * (horizontalDistance * tanTheta - displacement.Y);
            if (denominator <= Fix64.Zero)
                return false;

            Fix64 tangentSquared = tanTheta * tanTheta;
            Fix64 requiredSpeedSquared =
                gravityPerFrameSquared
                * horizontalDistanceSquared
                * (Fix64.One + tangentSquared)
                / denominator;
            if (requiredSpeedSquared <= Fix64.Zero)
                return false;

            Fix64 maxSpeedSquared = maxSpeedPerFrame * maxSpeedPerFrame;
            if (requiredSpeedSquared > maxSpeedSquared)
                return false;

            launchSpeedPerFrame = requiredSpeedSquared.Sqrt();
            if (launchSpeedPerFrame <= Fix64.Zero || launchSpeedPerFrame > maxSpeedPerFrame)
                return false;

            Fix64 horizontalSpeed = launchSpeedPerFrame / (Fix64.One + tangentSquared).Sqrt();
            if (horizontalSpeed <= Fix64.Zero)
                return false;

            FixVector3 horizontalDirection = horizontal / horizontalDistance;
            Fix64 verticalSpeed = horizontalSpeed * tanTheta;
            initialVelocity = horizontalDirection * horizontalSpeed
                + new FixVector3(Fix64.Zero, verticalSpeed, Fix64.Zero);

            Fix64 flightTime = horizontalDistance / horizontalSpeed;
            effectiveLifetimeFrames = CeilPositiveFrames(flightTime);
            if (effectiveLifetimeFrames > maxLifetimeFrames)
            {
                effectiveLifetimeFrames = 0;
                launchSpeedPerFrame = Fix64.Zero;
                initialVelocity = FixVector3.Zero;
                return false;
            }

            return effectiveLifetimeFrames > 0;
        }

        /// <summary>
        /// Solves the high-arc initial velocity that reaches <paramref name="targetPoint"/> from
        /// <paramref name="origin"/> using a fixed initial speed and gameplay gravity.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public static bool TrySolveHighArc(
            FixVector3 origin,
            FixVector3 targetPoint,
            Fix64 speedPerFrame,
            Fix64 gravityPerFrameSquared,
            int maxLifetimeFrames,
            out FixVector3 initialVelocity,
            out int effectiveLifetimeFrames)
        {
            initialVelocity = FixVector3.Zero;
            effectiveLifetimeFrames = 0;
            if (speedPerFrame <= Fix64.Zero || gravityPerFrameSquared <= Fix64.Zero || maxLifetimeFrames <= 0)
                return false;

            FixVector3 displacement = targetPoint - origin;
            var horizontal = new FixVector3(displacement.X, Fix64.Zero, displacement.Z);
            Fix64 horizontalDistanceSquared = horizontal.LengthSquared();
            if (horizontalDistanceSquared <= Fix64.Zero)
                return false;

            Fix64 horizontalDistance = horizontalDistanceSquared.Sqrt();
            Fix64 speedSquared = speedPerFrame * speedPerFrame;
            Fix64 discriminant = speedSquared * speedSquared
                - gravityPerFrameSquared
                * (gravityPerFrameSquared * horizontalDistanceSquared
                    + Fix64.FromInt(2) * displacement.Y * speedSquared);
            if (discriminant < Fix64.Zero)
                return false;

            Fix64 denominator = gravityPerFrameSquared * horizontalDistance;
            if (denominator <= Fix64.Zero)
                return false;

            Fix64 tanTheta = (speedSquared + discriminant.Sqrt()) / denominator;
            Fix64 horizontalSpeed = speedPerFrame / (Fix64.One + tanTheta * tanTheta).Sqrt();
            if (horizontalSpeed <= Fix64.Zero)
                return false;

            FixVector3 horizontalDirection = horizontal / horizontalDistance;
            Fix64 verticalSpeed = horizontalSpeed * tanTheta;
            initialVelocity = horizontalDirection * horizontalSpeed
                + new FixVector3(Fix64.Zero, verticalSpeed, Fix64.Zero);

            Fix64 flightTime = horizontalDistance / horizontalSpeed;
            effectiveLifetimeFrames = CeilPositiveFrames(flightTime);
            if (effectiveLifetimeFrames > maxLifetimeFrames)
            {
                effectiveLifetimeFrames = 0;
                initialVelocity = FixVector3.Zero;
                return false;
            }

            return effectiveLifetimeFrames > 0;
        }

        private static int CeilPositiveFrames(Fix64 value)
        {
            if (value <= Fix64.Zero)
                return 1;

            long raw = value.RawValue;
            long frames = raw / Fix64.Scale;
            if (raw % Fix64.Scale != 0)
                frames++;
            if (frames < 1)
                return 1;
            if (frames > int.MaxValue)
                return int.MaxValue;
            return checked((int)frames);
        }
    }

    public readonly struct CombatProjectileCollisionConfig : IEquatable<CombatProjectileCollisionConfig>
    {
        public CombatProjectileCollisionConfig(
            Fix64 radius,
            CombatPhysicsLayerMask layerMask,
            bool stopOnFirstAcceptedImpact = true,
            bool stopOnBlockedImpact = false,
            int maxAcceptedImpacts = 1,
            CombatProjectileHitSameTargetPolicy hitSameTargetPolicy = CombatProjectileHitSameTargetPolicy.AllowRepeat)
        {
            if (radius < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(radius), "Projectile radius cannot be negative.");
            if (maxAcceptedImpacts <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxAcceptedImpacts), "Projectile max accepted impacts must be positive.");
            if (!Enum.IsDefined(typeof(CombatProjectileHitSameTargetPolicy), hitSameTargetPolicy))
                throw new ArgumentOutOfRangeException(nameof(hitSameTargetPolicy), "Projectile hit same target policy is not supported.");

            Radius = radius;
            LayerMask = layerMask;
            StopOnFirstAcceptedImpact = stopOnFirstAcceptedImpact;
            StopOnBlockedImpact = stopOnBlockedImpact;
            MaxAcceptedImpacts = maxAcceptedImpacts;
            HitSameTargetPolicy = hitSameTargetPolicy;
        }

        public Fix64 Radius { get; }
        public CombatPhysicsLayerMask LayerMask { get; }
        public bool StopOnFirstAcceptedImpact { get; }
        public bool StopOnBlockedImpact { get; }
        public int MaxAcceptedImpacts { get; }
        public CombatProjectileHitSameTargetPolicy HitSameTargetPolicy { get; }

        public bool Equals(CombatProjectileCollisionConfig other)
        {
            return Radius.Equals(other.Radius)
                && LayerMask.Equals(other.LayerMask)
                && StopOnFirstAcceptedImpact == other.StopOnFirstAcceptedImpact
                && StopOnBlockedImpact == other.StopOnBlockedImpact
                && MaxAcceptedImpacts == other.MaxAcceptedImpacts
                && HitSameTargetPolicy == other.HitSameTargetPolicy;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileCollisionConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Radius.GetHashCode();
                hash = (hash * 397) ^ LayerMask.Value;
                hash = (hash * 397) ^ (StopOnFirstAcceptedImpact ? 1 : 0);
                hash = (hash * 397) ^ (StopOnBlockedImpact ? 1 : 0);
                hash = (hash * 397) ^ MaxAcceptedImpacts;
                hash = (hash * 397) ^ (int)HitSameTargetPolicy;
                return hash;
            }
        }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddLong(prefix + ".radius.raw", Radius.RawValue);
            accumulator.AddInt(prefix + ".layerMask", LayerMask.Value);
            accumulator.AddInt(prefix + ".stopOnFirstAcceptedImpact", StopOnFirstAcceptedImpact ? 1 : 0);
            accumulator.AddInt(prefix + ".stopOnBlockedImpact", StopOnBlockedImpact ? 1 : 0);
            accumulator.AddInt(prefix + ".maxAcceptedImpacts", MaxAcceptedImpacts);
            accumulator.AddInt(prefix + ".hitSameTargetPolicy", (int)HitSameTargetPolicy);
        }
    }

    public readonly struct CombatProjectileImpactConfig : IEquatable<CombatProjectileImpactConfig>
    {
        public CombatProjectileImpactConfig(
            CombatHitEventConfig hitEvent,
            int stableEventId = 0,
            CombatProjectileImpactPolicyKind policyKind = CombatProjectileImpactPolicyKind.DirectHit,
            Fix64 areaRadius = default,
            CombatPhysicsLayerMask areaLayerMask = default,
            bool triggerOnExpire = false,
            CombatHitEventConfig areaHitEvent = default,
            string stableEventLabel = "")
            : this(
                0,
                stableEventId,
                policyKind,
                areaRadius,
                areaLayerMask,
                triggerOnExpire,
                hitEvent,
                areaHitEvent,
                0, stableEventLabel: stableEventLabel)
        {
        }

        public CombatProjectileImpactConfig(
            int presentationProfileId = 0,
            int stableEventId = 0,
            CombatProjectileImpactPolicyKind policyKind = CombatProjectileImpactPolicyKind.DirectHit,
            Fix64 areaRadius = default,
            CombatPhysicsLayerMask areaLayerMask = default,
            bool triggerOnExpire = false,
            CombatHitEventConfig hitEvent = default,
            CombatHitEventConfig areaHitEvent = default,
            int effectTimelineId = 0,
            string presentationProfileLabel = "",
            string stableEventLabel = "",
            string effectTimelineLabel = "")
        {
            if (!Enum.IsDefined(typeof(CombatProjectileImpactPolicyKind), policyKind))
                throw new ArgumentOutOfRangeException(nameof(policyKind), "Projectile impact policy kind is not supported.");
            if (areaRadius < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(areaRadius), "Projectile impact area radius cannot be negative.");
            if (policyKind == CombatProjectileImpactPolicyKind.AreaPulseOnImpact)
            {
                if (areaRadius <= Fix64.Zero)
                    throw new ArgumentOutOfRangeException(nameof(areaRadius), "Area projectile impact radius must be positive.");
                if (areaLayerMask.Value == 0)
                    throw new ArgumentOutOfRangeException(nameof(areaLayerMask), "Area projectile impact layer mask must be non-zero.");
            }

            PresentationProfileId = presentationProfileId;
            PresentationProfileLabel = presentationProfileLabel ?? string.Empty;
            StableEventId = stableEventId;
            StableEventLabel = stableEventLabel ?? string.Empty;
            PolicyKind = policyKind;
            AreaRadius = areaRadius;
            AreaLayerMask = areaLayerMask;
            TriggerOnExpire = triggerOnExpire;
            HitEvent = hitEvent.WithDefaults(StableEventId, StableEventLabel);
            AreaHitEvent = areaHitEvent.WithDefaults(StableEventId, StableEventLabel);
            EffectTimelineId = effectTimelineId;
            EffectTimelineLabel = effectTimelineLabel ?? string.Empty;
        }

        public int PresentationProfileId { get; }
        public string PresentationProfileLabel { get; }
        public int StableEventId { get; }
        public string StableEventLabel { get; }
        public CombatProjectileImpactPolicyKind PolicyKind { get; }
        public Fix64 AreaRadius { get; }
        public CombatPhysicsLayerMask AreaLayerMask { get; }
        public bool TriggerOnExpire { get; }
        public CombatHitEventConfig HitEvent { get; }
        public CombatHitEventConfig AreaHitEvent { get; }
        public int EffectTimelineId { get; }
        public string EffectTimelineLabel { get; }

        public bool Equals(CombatProjectileImpactConfig other)
        {
            if (PolicyKind != other.PolicyKind)
                return false;
            if (EffectTimelineId != other.EffectTimelineId)
                return false;
            if (PresentationProfileId != other.PresentationProfileId)
                return false;
            if (StableEventId != other.StableEventId)
                return false;
            if (!HitEvent.Equals(other.HitEvent))
                return false;
            if (PolicyKind != CombatProjectileImpactPolicyKind.AreaPulseOnImpact)
                return true;
            return AreaRadius.Equals(other.AreaRadius)
                && AreaLayerMask.Equals(other.AreaLayerMask)
                && TriggerOnExpire == other.TriggerOnExpire
                && AreaHitEvent.Equals(other.AreaHitEvent);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileImpactConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)PolicyKind;
                hash = (hash * 397) ^ PresentationProfileId;
                hash = (hash * 397) ^ StableEventId;
                hash = (hash * 397) ^ HitEvent.GetHashCode();
                hash = (hash * 397) ^ EffectTimelineId;
                if (PolicyKind != CombatProjectileImpactPolicyKind.AreaPulseOnImpact)
                    return hash;

                hash = (hash * 397) ^ AreaRadius.GetHashCode();
                hash = (hash * 397) ^ AreaLayerMask.Value;
                hash = (hash * 397) ^ (TriggerOnExpire ? 1 : 0);
                hash = (hash * 397) ^ AreaHitEvent.GetHashCode();
                return hash;
            }
        }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddStringStable(prefix + ".presentationProfileId", PresentationProfileLabel);
            accumulator.AddStringStable(prefix + ".stableEventId", StableEventLabel);
            accumulator.AddInt(prefix + ".policyKind", (int)PolicyKind);
            accumulator.AddStringStable(prefix + ".effectTimelineId", EffectTimelineLabel);
            HitEvent.Contribute(accumulator, prefix + ".hitEvent");
            if (PolicyKind != CombatProjectileImpactPolicyKind.AreaPulseOnImpact)
                return;

            accumulator.AddLong(prefix + ".areaRadius.raw", AreaRadius.RawValue);
            accumulator.AddInt(prefix + ".areaLayerMask", AreaLayerMask.Value);
            accumulator.AddInt(prefix + ".triggerOnExpire", TriggerOnExpire ? 1 : 0);
            AreaHitEvent.Contribute(accumulator, prefix + ".areaHitEvent");
        }
    }

    internal sealed class CombatProjectileDefinitionComparer : IComparer<CombatProjectileDefinition>
    {
        public static readonly CombatProjectileDefinitionComparer Instance =
            new CombatProjectileDefinitionComparer();

        public int Compare(CombatProjectileDefinition left, CombatProjectileDefinition right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int compare = left.DefinitionId.CompareTo(right.DefinitionId);
            if (compare != 0)
                return compare;
            return string.CompareOrdinal(left.StableId, right.StableId);
        }
    }

    internal static class CombatProjectileReadOnly
    {
        public static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
                return EmptyReadOnlyCollection<T>.Instance;

            var copy = new List<T>(source.Count);
            for (int i = 0; i < source.Count; i++)
                copy.Add(source[i]);

            return new ReadOnlyCollection<T>(copy);
        }

        public static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> source, int count)
        {
            if (source == null || count <= 0)
                return EmptyReadOnlyCollection<T>.Instance;
            if (count > source.Count)
                throw new ArgumentOutOfRangeException(nameof(count));

            var copy = new List<T>(count);
            for (int i = 0; i < count; i++)
                copy.Add(source[i]);

            return new ReadOnlyCollection<T>(copy);
        }

        private static class EmptyReadOnlyCollection<T>
        {
            public static readonly ReadOnlyCollection<T> Instance =
                new ReadOnlyCollection<T>(Array.Empty<T>());
        }
    }
}

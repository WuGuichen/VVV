using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestDirectHitTraceSampleKind
    {
        WeaponTraceBlade = 0,
        WeaponTraceTipSweep = 1,
        WeaponTraceProximal = 2,
        ManualCapsule = 3,
        CasterBody = 4,
        BakedSocketCapsule = 5
    }

    /// <summary>
    /// Explicit source pose input for a per-Active-frame trace sample.
    /// The pose is intentionally a value owned by CharacterTest, so a preview
    /// can provide a root-motion-derived pose without reading mutable objects.
    /// </summary>
    internal readonly struct CharacterTestDirectHitTraceSourcePose
    {
        public CharacterTestDirectHitTraceSourcePose(
            int localFrame,
            FixVector3 position,
            int yawMilliDegrees)
        {
            LocalFrame = localFrame;
            Position = position;
            YawMilliDegrees = yawMilliDegrees;
        }

        internal CharacterTestDirectHitTraceSourcePose(
            int localFrame,
            FixVector3 position,
            float yawDegrees)
            : this(
                localFrame,
                position,
                CharacterTestDeterministicUnits.DegreesToMilliDegrees(yawDegrees))
        {
        }

        public int LocalFrame { get; }

        public FixVector3 Position { get; }

        public int YawMilliDegrees { get; }

        public FixVector3 TransformLocal(FixVector3 local)
        {
            return Position + RotateLocal(local);
        }

        public FixVector3 RotateLocal(FixVector3 local)
        {
            const int QuarterTurnMilliDegrees = 90000;
            const int HalfTurnMilliDegrees = 180000;
            const int ThreeQuarterTurnMilliDegrees = 270000;
            const int FullTurnMilliDegrees = 360000;
            int normalizedYaw = YawMilliDegrees % FullTurnMilliDegrees;
            if (normalizedYaw < 0)
                normalizedYaw += FullTurnMilliDegrees;

            int sinScaled;
            int cosScaled;
            switch (normalizedYaw)
            {
                case 0:
                    sinScaled = 0;
                    cosScaled = CharacterTestDeterministicMath.TrigScale;
                    break;
                case QuarterTurnMilliDegrees:
                    sinScaled = CharacterTestDeterministicMath.TrigScale;
                    cosScaled = 0;
                    break;
                case HalfTurnMilliDegrees:
                    sinScaled = 0;
                    cosScaled = -CharacterTestDeterministicMath.TrigScale;
                    break;
                case ThreeQuarterTurnMilliDegrees:
                    sinScaled = -CharacterTestDeterministicMath.TrigScale;
                    cosScaled = 0;
                    break;
                default:
                    CharacterTestDeterministicMath.SinCosMilliDegrees(
                        normalizedYaw,
                        out sinScaled,
                        out cosScaled);
                    break;
            }
            Fix64 sin = Fix64.FromRatio(sinScaled, CharacterTestDeterministicMath.TrigScale);
            Fix64 cos = Fix64.FromRatio(cosScaled, CharacterTestDeterministicMath.TrigScale);
            return new FixVector3(
                local.X * cos + local.Z * sin,
                local.Y,
                -local.X * sin + local.Z * cos);
        }

        public static CharacterTestDirectHitTraceSourcePose FromTransform(
            int localFrame,
            CharacterTestCharacterTransformComponent transform)
        {
            return new CharacterTestDirectHitTraceSourcePose(
                localFrame,
                new FixVector3(
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(transform.XMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(transform.YMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(transform.ZMillimeters)),
                transform.YawMilliDegrees);
        }

    }

    /// <summary>
    /// Frozen root-motion/local-facing offset for one Active frame. Preview
    /// callers never provide an absolute world pose: the builder composes this
    /// value with the sealed plan PoseIdentity, making that identity the only
    /// world-space source-pose authority.
    /// </summary>
    internal readonly struct CharacterTestDirectHitTraceSourcePoseOffset
    {
        internal CharacterTestDirectHitTraceSourcePoseOffset(
            int localFrame,
            FixVector3 positionOffsetLocal,
            int yawDeltaMilliDegrees)
        {
            LocalFrame = localFrame;
            PositionOffsetLocal = positionOffsetLocal;
            YawDeltaMilliDegrees = yawDeltaMilliDegrees;
        }

        internal int LocalFrame { get; }
        internal FixVector3 PositionOffsetLocal { get; }
        internal int YawDeltaMilliDegrees { get; }

        internal CharacterTestDirectHitTraceSourcePose Resolve(
            CharacterTestDirectHitTracePlanPoseIdentity poseIdentity)
        {
            var basePose = new CharacterTestDirectHitTraceSourcePose(
                LocalFrame,
                poseIdentity.SourcePosition,
                poseIdentity.SourceYawMilliDegrees);
            return new CharacterTestDirectHitTraceSourcePose(
                LocalFrame,
                poseIdentity.SourcePosition + basePose.RotateLocal(PositionOffsetLocal),
                poseIdentity.SourceYawMilliDegrees + YawDeltaMilliDegrees);
        }
    }

    internal readonly struct CharacterTestDirectHitTracePlanSample
    {
        public CharacterTestDirectHitTracePlanSample(
            CombatCapsuleQuery query,
            CharacterTestDirectHitTraceSampleKind kind,
            int localFrame,
            int sampleOrder,
            int queryOffset,
            int hitWindowId,
            int actionId,
            int traceId,
            int resolvePriority,
            int stableSampleId)
            : this(
                query,
                kind,
                localFrame,
                sampleOrder,
                queryOffset,
                hitWindowId,
                actionId,
                traceId,
                traceId,
                resolvePriority,
                stableSampleId)
        {
        }

        public CharacterTestDirectHitTracePlanSample(
            CombatCapsuleQuery query,
            CharacterTestDirectHitTraceSampleKind kind,
            int localFrame,
            int sampleOrder,
            int queryOffset,
            int hitWindowId,
            int actionId,
            int traceId,
            int policyEventId,
            int resolvePriority,
            int stableSampleId,
            WeaponTraceFrame? weaponMotion = null)
        {
            Query = query;
            Kind = kind;
            LocalFrame = localFrame;
            SampleOrder = sampleOrder;
            QueryOffset = queryOffset;
            HitWindowId = hitWindowId;
            ActionId = actionId;
            TraceId = traceId;
            PolicyEventId = policyEventId;
            ResolvePriority = resolvePriority;
            StableSampleId = stableSampleId;
            WeaponMotion = weaponMotion;
        }

        public CombatCapsuleQuery Query { get; }

        public CharacterTestDirectHitTraceSampleKind Kind { get; }

        public int LocalFrame { get; }

        public int SampleOrder { get; }

        public int QueryOffset { get; }

        /// <summary>Compatibility action-instance value used by existing hit-once resolution.</summary>
        public int HitWindowId { get; }

        public int ActionId { get; }

        public int TraceId { get; }

        /// <summary>
        /// Stable policy-event identity independent of the physical query
        /// TraceId. Multiple HitTraceWindow events may share one weapon
        /// binding TraceId, while their StableEventId remains unique.
        /// </summary>
        public int PolicyEventId { get; }

        public int ResolvePriority { get; }

        /// <summary>
        /// Semantic sample identity. It deliberately excludes the order-allocated
        /// HitWindowId and query id, which are retained only for compatibility.
        /// </summary>
        public int StableSampleId { get; }

        private WeaponTraceFrame? WeaponMotion { get; }

        internal FixVector3 ResolveIncomingDisplacement(FixVector3 hitPoint)
            => Kind == CharacterTestDirectHitTraceSampleKind.WeaponTraceTipSweep
                ? WeaponMotion.HasValue ? WeaponMotion.Value.TipNow - WeaponMotion.Value.TipPrev : Query.PointB - Query.PointA
                : Kind == CharacterTestDirectHitTraceSampleKind.WeaponTraceBlade && WeaponMotion.HasValue
                    ? WeaponTraceQueryBuilder.ResolveBladeIncomingDisplacement(WeaponMotion.Value, hitPoint)
                    : Kind == CharacterTestDirectHitTraceSampleKind.WeaponTraceProximal && WeaponMotion.HasValue
                        ? WeaponTraceQueryBuilder.ResolveProximalIncomingDisplacement(WeaponMotion.Value, hitPoint)
                    : FixVector3.Zero;
    }

    internal struct CharacterTestDirectHitTraceBounds
    {
        public bool HasBounds { get; private set; }

        public FixVector3 Min { get; private set; }

        public FixVector3 Max { get; private set; }

        public void Reset()
        {
            HasBounds = false;
            Min = FixVector3.Zero;
            Max = FixVector3.Zero;
        }

        public void IncludeCapsule(FixVector3 pointA, FixVector3 pointB, Fix64 radius)
        {
            FixVector3 min = new FixVector3(
                MinValue(pointA.X, pointB.X) - radius,
                MinValue(pointA.Y, pointB.Y) - radius,
                MinValue(pointA.Z, pointB.Z) - radius);
            FixVector3 max = new FixVector3(
                MaxValue(pointA.X, pointB.X) + radius,
                MaxValue(pointA.Y, pointB.Y) + radius,
                MaxValue(pointA.Z, pointB.Z) + radius);

            if (!HasBounds)
            {
                HasBounds = true;
                Min = min;
                Max = max;
                return;
            }

            Min = new FixVector3(
                MinValue(Min.X, min.X),
                MinValue(Min.Y, min.Y),
                MinValue(Min.Z, min.Z));
            Max = new FixVector3(
                MaxValue(Max.X, max.X),
                MaxValue(Max.Y, max.Y),
                MaxValue(Max.Z, max.Z));
        }

        private static Fix64 MinValue(Fix64 left, Fix64 right)
        {
            return left < right ? left : right;
        }

        private static Fix64 MaxValue(Fix64 left, Fix64 right)
        {
            return left > right ? left : right;
        }
    }

    /// <summary>
    /// Preview-only local weapon trace input. The caller supplies the complete
    /// resolved Active-frame pose sequence separately; no runtime entity is read.
    /// </summary>
    internal readonly struct CharacterTestDirectHitTracePreviewWeaponFrame
    {
        public CharacterTestDirectHitTracePreviewWeaponFrame(
            int localFrame,
            int traceIndex,
            int traceId,
            int actionId,
            FixVector3 rootPrevLocal,
            FixVector3 tipPrevLocal,
            FixVector3 rootNowLocal,
            FixVector3 tipNowLocal,
            Fix64 radius,
            CombatPhysicsLayerMask targetMask,
            Fix64 proximalExtension,
            Fix64 proximalRadius)
            : this(
                localFrame,
                traceIndex,
                traceId,
                traceId,
                actionId,
                rootPrevLocal,
                tipPrevLocal,
                rootNowLocal,
                tipNowLocal,
                radius,
                targetMask,
                proximalExtension,
                proximalRadius)
        {
        }

        public CharacterTestDirectHitTracePreviewWeaponFrame(
            int localFrame,
            int traceIndex,
            int traceId,
            int policyEventId,
            int actionId,
            FixVector3 rootPrevLocal,
            FixVector3 tipPrevLocal,
            FixVector3 rootNowLocal,
            FixVector3 tipNowLocal,
            Fix64 radius,
            CombatPhysicsLayerMask targetMask,
            Fix64 proximalExtension,
            Fix64 proximalRadius)
        {
            LocalFrame = localFrame;
            TraceIndex = traceIndex;
            TraceId = traceId;
            PolicyEventId = policyEventId;
            ActionId = actionId;
            RootPrevLocal = rootPrevLocal;
            TipPrevLocal = tipPrevLocal;
            RootNowLocal = rootNowLocal;
            TipNowLocal = tipNowLocal;
            Radius = radius;
            TargetMask = targetMask;
            ProximalExtension = proximalExtension;
            ProximalRadius = proximalRadius;
        }

        public int LocalFrame { get; }
        public int TraceIndex { get; }
        public int TraceId { get; }
        public int PolicyEventId { get; }
        public int ActionId { get; }
        public FixVector3 RootPrevLocal { get; }
        public FixVector3 TipPrevLocal { get; }
        public FixVector3 RootNowLocal { get; }
        public FixVector3 TipNowLocal { get; }
        public Fix64 Radius { get; }
        public CombatPhysicsLayerMask TargetMask { get; }
        public Fix64 ProximalExtension { get; }
        public Fix64 ProximalRadius { get; }

        internal WeaponTraceFrame Resolve(CharacterTestDirectHitTraceSourcePose sourcePose)
        {
            return new WeaponTraceFrame(
                TraceId,
                sourcePose.TransformLocal(RootPrevLocal),
                sourcePose.TransformLocal(TipPrevLocal),
                sourcePose.TransformLocal(RootNowLocal),
                sourcePose.TransformLocal(TipNowLocal),
                Radius,
                TargetMask,
                ProximalExtension,
                ProximalRadius);
        }
    }

    /// <summary>Preview-only local capsule input for manual/baked/body samples.</summary>
    internal readonly struct CharacterTestDirectHitTracePreviewCapsuleFrame
    {
        public CharacterTestDirectHitTracePreviewCapsuleFrame(
            CharacterTestDirectHitTraceSampleKind kind,
            int localFrame,
            int sourceOrder,
            int queryOffset,
            int actionId,
            int traceId,
            int hitWindowId,
            CombatPhysicsLayerMask targetMask,
            FixVector3 pointALocal,
            FixVector3 pointBLocal,
            Fix64 radius)
            : this(
                kind,
                localFrame,
                sourceOrder,
                queryOffset,
                actionId,
                traceId,
                traceId,
                hitWindowId,
                targetMask,
                pointALocal,
                pointBLocal,
                radius)
        {
        }

        public CharacterTestDirectHitTracePreviewCapsuleFrame(
            CharacterTestDirectHitTraceSampleKind kind,
            int localFrame,
            int sourceOrder,
            int queryOffset,
            int actionId,
            int traceId,
            int policyEventId,
            int hitWindowId,
            CombatPhysicsLayerMask targetMask,
            FixVector3 pointALocal,
            FixVector3 pointBLocal,
            Fix64 radius)
        {
            Kind = kind;
            LocalFrame = localFrame;
            SourceOrder = sourceOrder;
            QueryOffset = queryOffset;
            ActionId = actionId;
            TraceId = traceId;
            PolicyEventId = policyEventId;
            HitWindowId = hitWindowId;
            TargetMask = targetMask;
            PointALocal = pointALocal;
            PointBLocal = pointBLocal;
            Radius = radius;
        }

        public CharacterTestDirectHitTraceSampleKind Kind { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public int QueryOffset { get; }
        public int ActionId { get; }
        public int TraceId { get; }
        public int PolicyEventId { get; }
        public int HitWindowId { get; }
        public CombatPhysicsLayerMask TargetMask { get; }
        public FixVector3 PointALocal { get; }
        public FixVector3 PointBLocal { get; }
        public Fix64 Radius { get; }
    }

    internal enum CharacterTestDirectHitTracePlanPoseKind
    {
        Unspecified = 0,
        CurrentPose = 1,
        TurnInPlace = 2,
        CandidatePose = 3,
        ActualExecution = 4
    }

    /// <summary>
    /// Frozen source-pose identity for a trace plan. Preview and actual plans
    /// carry CharacterTest's independent authority-pose/facing write
    /// revisions; a simulation frame is only the temporal boundary and is
    /// never used as a source identity token.
    /// </summary>
    internal readonly struct CharacterTestDirectHitTracePlanPoseIdentity :
        IEquatable<CharacterTestDirectHitTracePlanPoseIdentity>
    {
        internal CharacterTestDirectHitTracePlanPoseIdentity(
            CharacterTestDirectHitTracePlanPoseKind kind,
            FixVector3 sourcePosition,
            int sourceYawMilliDegrees,
            int sourcePoseRevision,
            int sourceFacingRevision)
        {
            Kind = kind;
            SourcePosition = sourcePosition;
            SourceYawMilliDegrees = NormalizeYaw(sourceYawMilliDegrees);
            SourcePoseRevision = sourcePoseRevision;
            SourceFacingRevision = sourceFacingRevision;
        }

        internal CharacterTestDirectHitTracePlanPoseKind Kind { get; }
        internal FixVector3 SourcePosition { get; }
        internal int SourceYawMilliDegrees { get; }
        internal int SourcePoseRevision { get; }
        internal int SourceFacingRevision { get; }
        internal bool IsValid =>
            Kind != CharacterTestDirectHitTracePlanPoseKind.Unspecified
            && SourcePoseRevision >= 0
            && SourceFacingRevision >= 0;
        internal bool IsPreviewBound =>
            IsValid
            && (Kind == CharacterTestDirectHitTracePlanPoseKind.CurrentPose
                || Kind == CharacterTestDirectHitTracePlanPoseKind.TurnInPlace
                || Kind == CharacterTestDirectHitTracePlanPoseKind.CandidatePose);

        internal static CharacterTestDirectHitTracePlanPoseIdentity CreateActualExecution(
            CharacterTestCharacterTransformComponent transform,
            int sourcePoseRevision,
            int sourceFacingRevision)
        {
            CharacterTestDirectHitTraceSourcePose pose =
                CharacterTestDirectHitTraceSourcePose.FromTransform(0, transform);
            return new CharacterTestDirectHitTracePlanPoseIdentity(
                CharacterTestDirectHitTracePlanPoseKind.ActualExecution,
                pose.Position,
                pose.YawMilliDegrees,
                sourcePoseRevision,
                sourceFacingRevision);
        }

        internal bool Matches(
            CharacterTestDirectHitTracePlanPoseKind kind,
            FixVector3 sourcePosition,
            int sourceYawMilliDegrees,
            int sourcePoseRevision,
            int sourceFacingRevision)
        {
            return Kind == kind
                && SourcePosition.Equals(sourcePosition)
                && SourceYawMilliDegrees == NormalizeYaw(sourceYawMilliDegrees)
                && SourcePoseRevision == sourcePoseRevision
                && SourceFacingRevision == sourceFacingRevision;
        }

        public bool Equals(CharacterTestDirectHitTracePlanPoseIdentity other)
        {
            return Kind == other.Kind
                && SourcePosition.Equals(other.SourcePosition)
                && SourceYawMilliDegrees == other.SourceYawMilliDegrees
                && SourcePoseRevision == other.SourcePoseRevision
                && SourceFacingRevision == other.SourceFacingRevision;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestDirectHitTracePlanPoseIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ SourcePosition.GetHashCode();
                hash = (hash * 397) ^ SourceYawMilliDegrees;
                hash = (hash * 397) ^ SourcePoseRevision;
                hash = (hash * 397) ^ SourceFacingRevision;
                return hash;
            }
        }

        private static int NormalizeYaw(int milliDegrees)
        {
            const int fullTurn = 360000;
            int normalized = milliDegrees % fullTurn;
            return normalized < 0 ? normalized + fullTurn : normalized;
        }
    }

    /// <summary>
    /// A value view over one sealed resolved action plan. The build lists are
    /// never read by a view; Seal copies their logical contents into a separate
    /// retained snapshot and advances generation. Consumers must use
    /// TryGetSample/TryGetBounds and IsCurrent.
    /// </summary>
    internal readonly struct CharacterTestDirectHitTracePlanView
    {
        private readonly CharacterTestDirectHitTracePlanBuffer _owner;
        private readonly int _generation;
        private readonly int _planIndex;
        private readonly bool _identityVerified;
        private readonly long _sealVersion;

        internal CharacterTestDirectHitTracePlanView(
            CharacterTestDirectHitTracePlanBuffer owner,
            int generation,
            int planIndex,
            RuntimeFrame runtimeFrame,
            int combatPhysicsRevision,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision,
            CharacterTestDirectHitTracePlanPoseIdentity poseIdentity)
        {
            _identityVerified = owner != null && owner.IsPlanCurrent(generation, planIndex, runtimeFrame,
                combatPhysicsRevision, sourceEntityId, resolvedActionRevision, poseIdentity);
            _sealVersion = owner?.SealVersion ?? -1;
            _owner = owner;
            _generation = generation;
            _planIndex = planIndex;
            RuntimeFrame = runtimeFrame;
            CombatPhysicsRevision = combatPhysicsRevision;
            SourceEntityId = sourceEntityId;
            ResolvedActionRevision = resolvedActionRevision;
            PoseIdentity = poseIdentity;
        }

        public RuntimeFrame RuntimeFrame { get; }

        public int CombatPhysicsRevision { get; }

        public CombatEntityId SourceEntityId { get; }

        public int ResolvedActionRevision { get; }

        internal CharacterTestDirectHitTracePlanPoseIdentity PoseIdentity { get; }

        internal int Count => IsBackingPlanCurrent()
            ? _owner.GetPlanCount(_planIndex)
            : 0;

        internal bool IsCurrent(
            RuntimeFrame runtimeFrame,
            int combatPhysicsRevision,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision)
        {
            return _owner != null
                && IsBackingPlanCurrent()
                && RuntimeFrame == runtimeFrame
                && CombatPhysicsRevision == combatPhysicsRevision
                && SourceEntityId.Equals(sourceEntityId)
                && ResolvedActionRevision == resolvedActionRevision;
        }

        internal bool IsStale(
            RuntimeFrame runtimeFrame,
            int combatPhysicsRevision,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision)
        {
            return !IsCurrent(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision);
        }

        internal bool IsCurrent(
            RuntimeFrame runtimeFrame,
            int combatPhysicsRevision,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision,
            CharacterTestDirectHitTracePlanPoseKind poseKind,
            FixVector3 sourcePosition,
            int sourceYawMilliDegrees,
            int sourcePoseRevision,
            int sourceFacingRevision)
        {
            return IsCurrent(
                    runtimeFrame,
                    combatPhysicsRevision,
                    sourceEntityId,
                    resolvedActionRevision)
                && PoseIdentity.Matches(
                    poseKind,
                    sourcePosition,
                    sourceYawMilliDegrees,
                    sourcePoseRevision,
                    sourceFacingRevision);
        }

        internal bool TryGetSample(int index, out CharacterTestDirectHitTracePlanSample sample)
        {
            if (_identityVerified && _owner.SealVersion == _sealVersion
                && _owner.TryGetPlanSample(_generation, _planIndex, index, out sample))
            {
                return true;
            }

            sample = default(CharacterTestDirectHitTracePlanSample);
            return false;
        }

        internal bool HasSameBackingPlan(in CharacterTestDirectHitTracePlanView other)
            => ReferenceEquals(_owner, other._owner) && _generation == other._generation
                && _sealVersion == other._sealVersion && _planIndex == other._planIndex;

        internal bool TryGetSampleInFrameOrder(int order, out int originalIndex, out CharacterTestDirectHitTracePlanSample sample)
        {
            originalIndex = -1;
            if (_identityVerified && _owner.SealVersion == _sealVersion)
                return _owner.TryGetPlanSampleInFrameOrder(_generation, _planIndex, order, out originalIndex, out sample);
            sample = default;
            return false;
        }

        internal bool TryGetBounds(out CharacterTestDirectHitTraceBounds bounds)
        {
            if (_identityVerified && _owner.SealVersion == _sealVersion
                && _owner.TryGetPlanBounds(_generation, _planIndex, out bounds))
            {
                return true;
            }

            bounds = default(CharacterTestDirectHitTraceBounds);
            return false;
        }

        private bool IsBackingPlanCurrent()
        {
            return _identityVerified && _owner.SealVersion == _sealVersion && _owner.IsPlanCurrent(_generation, _planIndex);
        }
    }

    /// <summary>Read-only, generation-bound collection of per-action plan views.</summary>
    internal readonly struct CharacterTestDirectHitTracePlanCollectionView
    {
        private readonly CharacterTestDirectHitTracePlanBuffer _owner;
        private readonly int _generation;

        internal CharacterTestDirectHitTracePlanCollectionView(
            CharacterTestDirectHitTracePlanBuffer owner,
            int generation,
            RuntimeFrame runtimeFrame,
            int combatPhysicsRevision)
        {
            _owner = owner;
            _generation = generation;
            RuntimeFrame = runtimeFrame;
            CombatPhysicsRevision = combatPhysicsRevision;
        }

        public RuntimeFrame RuntimeFrame { get; }

        public int CombatPhysicsRevision { get; }

        internal int Count => IsBackingCollectionCurrent()
            ? _owner.PlanCount
            : 0;

        internal bool IsCurrent(RuntimeFrame runtimeFrame, int combatPhysicsRevision)
        {
            return _owner != null
                && IsBackingCollectionCurrent()
                && RuntimeFrame == runtimeFrame
                && CombatPhysicsRevision == combatPhysicsRevision;
        }

        internal bool IsStale(RuntimeFrame runtimeFrame, int combatPhysicsRevision)
        {
            return !IsCurrent(runtimeFrame, combatPhysicsRevision);
        }

        internal bool TryGetPlan(int index, out CharacterTestDirectHitTracePlanView plan)
        {
            if (IsBackingCollectionCurrent()
                && _owner.TryGetPlan(_generation, index, out plan))
            {
                return true;
            }

            plan = default(CharacterTestDirectHitTracePlanView);
            return false;
        }

        private bool IsBackingCollectionCurrent()
        {
            return _owner != null
                && _owner.IsCollectionCurrent(
                    _generation,
                    RuntimeFrame,
                    CombatPhysicsRevision);
        }
    }

    /// <summary>
    /// Runtime-owned warmed plan storage. It deliberately exposes no list or
    /// enumerator; callers receive generation-bound value views instead.
    /// </summary>
    internal sealed class CharacterTestDirectHitTracePlanBuffer
    {
        private readonly List<CharacterTestDirectHitTracePlanEntry> _plans =
            new List<CharacterTestDirectHitTracePlanEntry>();
        private int _planCount;
        private int _generation;
        private long _sealVersion;
        internal long SealVersion => _sealVersion;
        private RuntimeFrame _runtimeFrame;
        private int _combatPhysicsRevision;
        private bool _isSealed;

        internal int PlanCount => _planCount;

        internal int Generation => _generation;

        internal bool IsSealed => _isSealed;

        internal void Reset(RuntimeFrame runtimeFrame, int combatPhysicsRevision)
        {
            for (int i = 0; i < _planCount; i++)
                _plans[i].Clear();
            _planCount = 0;
            _runtimeFrame = runtimeFrame;
            _combatPhysicsRevision = combatPhysicsRevision;
            _isSealed = false;
            _sealVersion = checked(_sealVersion + 1);
            _generation = _generation == int.MaxValue ? 1 : _generation + 1;
        }

        internal bool Seal(RuntimeFrame runtimeFrame, int combatPhysicsRevision)
        {
            if (_isSealed
                || runtimeFrame != _runtimeFrame
                || combatPhysicsRevision != _combatPhysicsRevision)
            {
                return false;
            }

            for (int i = 0; i < _planCount; i++)
            {
                if (!_plans[i].Seal())
                    return false;
            }

            _isSealed = true;
            _sealVersion = checked(_sealVersion + 1);
            _generation = _generation == int.MaxValue ? 1 : _generation + 1;
            return true;
        }

        internal int GetOrBeginPlan(
            CombatEntityId sourceEntityId,
            int resolvedActionRevision,
            CharacterTestDirectHitTracePlanPoseIdentity poseIdentity)
        {
            if (_isSealed || sourceEntityId.IsNone || resolvedActionRevision <= 0 || !poseIdentity.IsValid)
                return -1;

            for (int i = 0; i < _planCount; i++)
            {
                CharacterTestDirectHitTracePlanEntry existing = _plans[i];
                if (existing.SourceEntityId.Equals(sourceEntityId)
                    && existing.ResolvedActionRevision == resolvedActionRevision)
                {
                    return existing.PoseIdentity.Equals(poseIdentity) ? i : -1;
                }
            }

            int planIndex = _planCount++;
            if (planIndex == _plans.Count)
                _plans.Add(new CharacterTestDirectHitTracePlanEntry());
            _plans[planIndex].Prepare(sourceEntityId, resolvedActionRevision, poseIdentity);
            return planIndex;
        }

        internal List<CharacterTestDirectHitTracePlanSample> GetPlanSamplesBuffer(int planIndex)
        {
            if (!_isSealed && IsPlanIndexValid(planIndex))
                return _plans[planIndex].BuildSamples;

            return null;
        }

        internal int GetPlanCount(int planIndex)
        {
            return _isSealed && IsPlanIndexValid(planIndex) ? _plans[planIndex].SealedCount : 0;
        }

        internal CharacterTestDirectHitTraceBounds GetPlanBounds(int planIndex)
        {
            return IsPlanIndexValid(planIndex)
                ? _isSealed ? _plans[planIndex].SealedBounds : _plans[planIndex].BuildBounds
                : default(CharacterTestDirectHitTraceBounds);
        }

        internal bool UpdatePlan(
            int planIndex,
            int sampleCount,
            CharacterTestDirectHitTraceBounds bounds)
        {
            if (_isSealed || !IsPlanIndexValid(planIndex))
                return false;

            CharacterTestDirectHitTracePlanEntry plan = _plans[planIndex];
            plan.BuildCount = Math.Max(0, Math.Min(sampleCount, plan.BuildSamples.Count));
            plan.BuildBounds = bounds;
            return true;
        }

        internal CharacterTestDirectHitTracePlanCollectionView CreateView()
        {
            return new CharacterTestDirectHitTracePlanCollectionView(
                this,
                _generation,
                _runtimeFrame,
                _combatPhysicsRevision);
        }

        internal bool IsGenerationCurrent(int generation)
        {
            return _isSealed && generation == _generation;
        }

        internal bool IsCollectionCurrent(
            int generation,
            RuntimeFrame runtimeFrame,
            int combatPhysicsRevision)
        {
            return _isSealed
                && generation == _generation
                && runtimeFrame == _runtimeFrame
                && combatPhysicsRevision == _combatPhysicsRevision;
        }

        internal bool IsPlanCurrent(int generation, int planIndex)
        {
            return _isSealed && generation == _generation && IsPlanIndexValid(planIndex);
        }

        internal bool IsPlanCurrent(
            int generation,
            int planIndex,
            RuntimeFrame runtimeFrame,
            int combatPhysicsRevision,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision,
            CharacterTestDirectHitTracePlanPoseIdentity poseIdentity)
        {
            if (!IsPlanCurrent(generation, planIndex)
                || runtimeFrame != _runtimeFrame
                || combatPhysicsRevision != _combatPhysicsRevision)
            {
                return false;
            }

            CharacterTestDirectHitTracePlanEntry entry = _plans[planIndex];
            return entry.SourceEntityId.Equals(sourceEntityId)
                && entry.ResolvedActionRevision == resolvedActionRevision
                && entry.PoseIdentity.Equals(poseIdentity);
        }

        internal bool TryGetPlan(
            int generation,
            int planIndex,
            out CharacterTestDirectHitTracePlanView plan)
        {
            if (_isSealed && generation == _generation && IsPlanIndexValid(planIndex))
            {
                CharacterTestDirectHitTracePlanEntry entry = _plans[planIndex];
                plan = new CharacterTestDirectHitTracePlanView(
                    this,
                    generation,
                    planIndex,
                    _runtimeFrame,
                    _combatPhysicsRevision,
                    entry.SourceEntityId,
                    entry.ResolvedActionRevision,
                    entry.PoseIdentity);
                return true;
            }

            plan = default(CharacterTestDirectHitTracePlanView);
            return false;
        }

        internal bool TryGetPlanSampleInFrameOrder(int generation, int planIndex, int order,
            out int originalIndex, out CharacterTestDirectHitTracePlanSample sample)
        {
            originalIndex = -1;
            sample = default;
            if (!IsPlanCurrent(generation, planIndex)) return false;
            var plan = _plans[planIndex];
            if (order < 0 || order >= plan.SealedCount) return false;
            originalIndex = plan.FrameOrder[order];
            sample = plan.SealedSamples[originalIndex];
            return true;
        }

        internal bool TryGetPlanSample(
            int generation,
            int planIndex,
            int index,
            out CharacterTestDirectHitTracePlanSample sample)
        {
            sample = default(CharacterTestDirectHitTracePlanSample);
            if (!IsPlanCurrent(generation, planIndex) || index < 0)
                return false;

            CharacterTestDirectHitTracePlanEntry plan = _plans[planIndex];
            if (index >= plan.SealedCount)
                return false;

            if (index >= plan.SealedSamples.Count)
                return false;

            sample = plan.SealedSamples[index];
            return true;
        }

        internal bool TryGetPlanBounds(
            int generation,
            int planIndex,
            out CharacterTestDirectHitTraceBounds bounds)
        {
            if (IsPlanCurrent(generation, planIndex))
            {
                bounds = _plans[planIndex].SealedBounds;
                return true;
            }

            bounds = default(CharacterTestDirectHitTraceBounds);
            return false;
        }

        private bool IsPlanIndexValid(int planIndex)
        {
            return planIndex >= 0 && planIndex < _planCount;
        }

        private sealed class CharacterTestDirectHitTracePlanEntry : IComparer<int>
        {
            internal CharacterTestDirectHitTracePlanEntry()
            {
                BuildSamples = new List<CharacterTestDirectHitTracePlanSample>();
                SealedSamples = new List<CharacterTestDirectHitTracePlanSample>();
                _compareFrames = Compare;
            }

            internal void Prepare(
                CombatEntityId sourceEntityId,
                int resolvedActionRevision,
                CharacterTestDirectHitTracePlanPoseIdentity poseIdentity)
            {
                BuildCount = 0;
                SealedCount = 0;
                BuildBounds = default(CharacterTestDirectHitTraceBounds);
                SealedBounds = default(CharacterTestDirectHitTraceBounds);
                SourceEntityId = sourceEntityId;
                ResolvedActionRevision = resolvedActionRevision;
                PoseIdentity = poseIdentity;
                BuildSamples.Clear();
                SealedSamples.Clear();
            }

            internal void Clear()
            {
                BuildCount = 0;
                SealedCount = 0;
                BuildBounds = default(CharacterTestDirectHitTraceBounds);
                SealedBounds = default(CharacterTestDirectHitTraceBounds);
                BuildSamples.Clear();
                SealedSamples.Clear();
            }

            internal bool Seal()
            {
                if (!PoseIdentity.IsValid
                    || BuildCount < 0
                    || BuildCount > BuildSamples.Count)
                {
                    return false;
                }

                SealedSamples.Clear();
                for (int i = 0; i < BuildCount; i++)
                    SealedSamples.Add(BuildSamples[i]);
                FrameOrder.Clear();
                bool sorted = true;
                for (int i = 0; i < BuildCount; i++)
                {
                    FrameOrder.Add(i);
                    if (i > 0 && SealedSamples[i - 1].LocalFrame > SealedSamples[i].LocalFrame) sorted = false;
                }
                if (!sorted) FrameOrder.Sort(_compareFrames);
                SealedCount = BuildCount;
                SealedBounds = BuildBounds;
                return true;
            }

            internal readonly List<int> FrameOrder = new List<int>();
            private readonly Comparison<int> _compareFrames;
            public int Compare(int x, int y)
            {
                int frame = SealedSamples[x].LocalFrame.CompareTo(SealedSamples[y].LocalFrame);
                return frame != 0 ? frame : x.CompareTo(y);
            }

            internal int BuildCount;
            internal int SealedCount;
            internal CharacterTestDirectHitTraceBounds BuildBounds;
            internal CharacterTestDirectHitTraceBounds SealedBounds;
            internal CombatEntityId SourceEntityId;
            internal int ResolvedActionRevision;
            internal CharacterTestDirectHitTracePlanPoseIdentity PoseIdentity;
            internal readonly List<CharacterTestDirectHitTracePlanSample> BuildSamples;
            internal readonly List<CharacterTestDirectHitTracePlanSample> SealedSamples;
        }
    }
}

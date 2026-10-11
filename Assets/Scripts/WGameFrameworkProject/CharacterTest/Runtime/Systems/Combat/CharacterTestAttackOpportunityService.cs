using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Internal query status for CharacterTest attack-opportunity evaluation.</summary>
    internal enum CharacterTestAttackOpportunityStatus
    {
        Valid = 0,
        Unavailable = 1,
        Unsupported = 2,
        StaleSnapshot = 3
    }

    /// <summary>Internal evaluation modes. CandidatePose is reserved for CD-M3.</summary>
    internal enum CharacterTestAttackEvaluationMode
    {
        CurrentPose = 0,
        TurnInPlace = 1,
        CandidatePose = 2
    }

    internal enum CharacterTestAttackOpportunityLevel
    {
        NoOpportunity = 0,
        RequiresTranslation = 1,
        TurnInPlace = 2,
        CurrentPose = 3
    }

    internal enum CharacterTestAttackPredictionAssumption
    {
        Unknown = 0,
        StaticTarget = 1
    }

    /// <summary>Stable diagnostic identity and qualification evidence for one preview result.</summary>
    internal readonly struct CharacterTestAttackOpportunityEvidence
    {
        internal CharacterTestAttackOpportunityEvidence(
            CharacterTestAttackPredictionAssumption predictionAssumption,
            CombatEntityId sourceEntityId,
            CombatEntityId targetEntityId,
            int actionId,
            RuntimeFrame snapshotSimulationFrame,
            int snapshotCombatPhysicsRevision,
            int snapshotSourcePoseRevision,
            int snapshotSourceFacingRevision,
            int snapshotTargetHurtboxRevision,
            int snapshotResolvedActionRevision,
            int expectedSourceYawMilliDegrees,
            int evaluatedSourceYawMilliDegrees,
            bool orientationMatched,
            bool verticalOverlap,
            bool maskCompatible,
            bool qualificationAccepted,
            CombatBodyId blockerBodyId,
            CombatColliderId blockerColliderId,
            CharacterTestCombatWorldTargetKind blockerWorldTargetKind)
        {
            PredictionAssumption = predictionAssumption;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            ActionId = actionId;
            SnapshotSimulationFrame = snapshotSimulationFrame;
            SnapshotCombatPhysicsRevision = snapshotCombatPhysicsRevision;
            SnapshotSourcePoseRevision = snapshotSourcePoseRevision;
            SnapshotSourceFacingRevision = snapshotSourceFacingRevision;
            SnapshotTargetHurtboxRevision = snapshotTargetHurtboxRevision;
            SnapshotResolvedActionRevision = snapshotResolvedActionRevision;
            ExpectedSourceYawMilliDegrees = expectedSourceYawMilliDegrees;
            EvaluatedSourceYawMilliDegrees = evaluatedSourceYawMilliDegrees;
            OrientationMatched = orientationMatched;
            VerticalOverlap = verticalOverlap;
            MaskCompatible = maskCompatible;
            QualificationAccepted = qualificationAccepted;
            BlockerBodyId = blockerBodyId;
            BlockerColliderId = blockerColliderId;
            BlockerWorldTargetKind = blockerWorldTargetKind;
        }

        internal CharacterTestAttackPredictionAssumption PredictionAssumption { get; }
        internal CombatEntityId SourceEntityId { get; }
        internal CombatEntityId TargetEntityId { get; }
        internal int ActionId { get; }
        internal RuntimeFrame SnapshotSimulationFrame { get; }
        internal int SnapshotCombatPhysicsRevision { get; }
        internal int SnapshotSourcePoseRevision { get; }
        internal int SnapshotSourceFacingRevision { get; }
        internal int SnapshotTargetHurtboxRevision { get; }
        internal int SnapshotResolvedActionRevision { get; }
        internal int ExpectedSourceYawMilliDegrees { get; }
        internal int EvaluatedSourceYawMilliDegrees { get; }
        internal bool OrientationMatched { get; }
        internal bool VerticalOverlap { get; }
        internal bool MaskCompatible { get; }
        internal bool QualificationAccepted { get; }
        internal CombatBodyId BlockerBodyId { get; }
        internal CombatColliderId BlockerColliderId { get; }
        internal CharacterTestCombatWorldTargetKind BlockerWorldTargetKind { get; }

        internal static CharacterTestAttackOpportunityEvidence ForBlocker(
            CombatBodyId blockerBodyId,
            CombatColliderId blockerColliderId,
            CharacterTestCombatWorldTargetKind blockerWorldTargetKind)
        {
            return new CharacterTestAttackOpportunityEvidence(
                CharacterTestAttackPredictionAssumption.Unknown,
                default(CombatEntityId),
                default(CombatEntityId),
                0,
                default(RuntimeFrame),
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                false,
                false,
                false,
                false,
                blockerBodyId,
                blockerColliderId,
                blockerWorldTargetKind);
        }
    }

    internal enum CharacterTestAttackOpportunityTargetKind
    {
        Unknown = 0,
        HostileCharacter = 1,
        FriendlyCharacter = 2,
        Self = 3,
        ThirdPartyCharacter = 4,
        DamageableObject = 5
    }

    internal enum CharacterTestAttackOpportunityFailureReason
    {
        None = 0,
        SnapshotUnavailable = 1,
        StaleSnapshot = 2,
        TraceEvaluatorUnsupported = 3,
        TracePlanUnavailable = 4,
        TracePlanInvalid = 5,
        NoCompatibleHurtbox = 6,
        TargetNotEligible = 7,
        TargetStateRejected = 8,
        BlockedByEnvironment = 9,
        NoAcceptedHit = 10,
        CandidatePoseUnsupported = 11,
        NonTargetActorStateUnavailable = 12,
        DamagePayloadUnavailable = 13,
        DamagePayloadInvalid = 14,
        PartStateDependencyRejected = 15
    }

    internal readonly struct CharacterTestOpportunityFailure
    {
        private CharacterTestOpportunityFailure(
            CharacterTestAttackOpportunityStatus status,
            CharacterTestAttackOpportunityFailureReason reason,
            CharacterTestAttackOpportunityFailureFlags flags,
            bool hasCompatibleHurtbox,
            bool envelopeIntersectsTarget)
        {
            Status = status;
            Reason = reason;
            Flags = flags;
            HasCompatibleHurtbox = hasCompatibleHurtbox;
            EnvelopeIntersectsTarget = envelopeIntersectsTarget;
        }

        internal CharacterTestAttackOpportunityStatus Status { get; }
        internal CharacterTestAttackOpportunityFailureReason Reason { get; }
        internal CharacterTestAttackOpportunityFailureFlags Flags { get; }
        internal bool HasCompatibleHurtbox { get; }
        internal bool EnvelopeIntersectsTarget { get; }

        internal static CharacterTestOpportunityFailure Valid(
            CharacterTestAttackOpportunityFailureReason reason,
            CharacterTestAttackOpportunityFailureFlags flags,
            bool hasCompatibleHurtbox,
            bool envelopeIntersectsTarget)
        {
            return new CharacterTestOpportunityFailure(
                CharacterTestAttackOpportunityStatus.Valid,
                reason,
                flags,
                hasCompatibleHurtbox,
                envelopeIntersectsTarget);
        }

        internal static CharacterTestOpportunityFailure Unavailable(
            CharacterTestAttackOpportunityFailureReason reason,
            CharacterTestAttackOpportunityFailureFlags flags,
            bool hasCompatibleHurtbox = false,
            bool envelopeIntersectsTarget = false)
        {
            return new CharacterTestOpportunityFailure(
                CharacterTestAttackOpportunityStatus.Unavailable,
                reason,
                flags,
                hasCompatibleHurtbox,
                envelopeIntersectsTarget);
        }

        internal static CharacterTestOpportunityFailure Unsupported(
            CharacterTestAttackOpportunityFailureReason reason,
            CharacterTestAttackOpportunityFailureFlags flags)
        {
            return new CharacterTestOpportunityFailure(
                CharacterTestAttackOpportunityStatus.Unsupported,
                reason,
                flags,
                false,
                false);
        }

        internal static CharacterTestOpportunityFailure Stale(
            CharacterTestAttackOpportunityFailureReason reason,
            CharacterTestAttackOpportunityFailureFlags flags)
        {
            return new CharacterTestOpportunityFailure(
                CharacterTestAttackOpportunityStatus.StaleSnapshot,
                reason,
                flags,
                false,
                false);
        }
    }

    [Flags]
    internal enum CharacterTestAttackOpportunityFailureFlags
    {
        None = 0,
        SnapshotUnavailable = 1 << 0,
        SnapshotMismatch = 1 << 1,
        TraceEvaluatorUnsupported = 1 << 2,
        TracePlanUnavailable = 1 << 3,
        TracePlanMismatch = 1 << 4,
        TracePlanInvalid = 1 << 5,
        NoCompatibleHurtbox = 1 << 6,
        TargetNotEligible = 1 << 7,
        TargetStateRejected = 1 << 8,
        BlockedByEnvironment = 1 << 9,
        NoAcceptedHit = 1 << 10,
        CandidatePoseUnsupported = 1 << 11,
        NonTargetActorStateUnavailable = 1 << 12,
        DamagePayloadUnavailable = 1 << 13,
        DamagePayloadInvalid = 1 << 14,
        PartStateDependencyRejected = 1 << 15
    }

    /// <summary>Exact identity of one trace-plan sample.</summary>
    internal readonly struct CharacterTestAttackOpportunitySampleIdentity : IEquatable<CharacterTestAttackOpportunitySampleIdentity>
    {
        private CharacterTestAttackOpportunitySampleIdentity(
            int stableSampleId,
            int localFrame,
            int sampleOrder,
            int queryId,
            int queryOffset,
            int hitWindowId,
            int actionId,
            int traceId,
            int policyEventId)
        {
            StableSampleId = stableSampleId;
            LocalFrame = localFrame;
            SampleOrder = sampleOrder;
            QueryId = queryId;
            QueryOffset = queryOffset;
            HitWindowId = hitWindowId;
            ActionId = actionId;
            TraceId = traceId;
            PolicyEventId = policyEventId;
        }

        public int StableSampleId { get; }
        public int LocalFrame { get; }
        public int SampleOrder { get; }
        public int QueryId { get; }
        public int QueryOffset { get; }
        public int HitWindowId { get; }
        public int ActionId { get; }
        public int TraceId { get; }
        public int PolicyEventId { get; }

        internal static CharacterTestAttackOpportunitySampleIdentity From(
            CharacterTestDirectHitTracePlanSample sample)
        {
            return new CharacterTestAttackOpportunitySampleIdentity(
                sample.StableSampleId,
                sample.LocalFrame,
                sample.SampleOrder,
                sample.Query.Header.QueryId,
                sample.QueryOffset,
                sample.HitWindowId,
                sample.ActionId,
                sample.TraceId,
                sample.PolicyEventId);
        }

        public bool Equals(CharacterTestAttackOpportunitySampleIdentity other)
        {
            return StableSampleId == other.StableSampleId
                && LocalFrame == other.LocalFrame
                && SampleOrder == other.SampleOrder
                && QueryId == other.QueryId
                && QueryOffset == other.QueryOffset
                && HitWindowId == other.HitWindowId
                && ActionId == other.ActionId
                && TraceId == other.TraceId
                && PolicyEventId == other.PolicyEventId;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestAttackOpportunitySampleIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StableSampleId;
                hash = (hash * 397) ^ LocalFrame;
                hash = (hash * 397) ^ SampleOrder;
                hash = (hash * 397) ^ QueryId;
                hash = (hash * 397) ^ QueryOffset;
                hash = (hash * 397) ^ HitWindowId;
                hash = (hash * 397) ^ ActionId;
                hash = (hash * 397) ^ TraceId;
                return (hash * 397) ^ PolicyEventId;
            }
        }
    }

    /// <summary>
    /// Per-HitEvent frozen policy. Team relation, target state and the actual
    /// CombatHitEventConfig are captured before preview; evaluation never reads
    /// live team or battle state.
    /// </summary>
    internal readonly struct CharacterTestAttackOpportunitySamplePolicy
    {
        public CharacterTestAttackOpportunitySamplePolicy(
            CharacterTestAttackOpportunitySampleIdentity sampleIdentity,
            CombatEntityId sourceEntityId,
            CombatEntityId targetEntityId,
            int sourceTeamId,
            int targetTeamId,
            HitTargetStateFlags targetState,
            CombatHitEventConfig hitEvent,
            CharacterActionDamagePayload damagePayload)
        {
            SampleIdentity = sampleIdentity;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            SourceTeamId = sourceTeamId;
            TargetTeamId = targetTeamId;
            TargetKind = CharacterTestCombatHitEventResolver.ResolveCharacterTargetKind(
                sourceEntityId.Value,
                targetEntityId.Value,
                sourceTeamId,
                targetTeamId);
            TargetState = targetState;
            HitEvent = hitEvent;
            DamagePayload = damagePayload;
        }

        public CharacterTestAttackOpportunitySampleIdentity SampleIdentity { get; }
        public CombatEntityId SourceEntityId { get; }
        public CombatEntityId TargetEntityId { get; }
        public int SourceTeamId { get; }
        public int TargetTeamId { get; }
        public CharacterTestAttackOpportunityTargetKind TargetKind { get; }
        public HitTargetStateFlags TargetState { get; }
        public CombatHitEventConfig HitEvent { get; }
        public CharacterActionDamagePayload DamagePayload { get; }

        internal bool IsBoundTo(
            CharacterTestDirectHitTracePlanSample sample,
            CharacterTestAttackEvaluationSnapshot snapshot)
        {
            return SampleIdentity.Equals(CharacterTestAttackOpportunitySampleIdentity.From(sample))
                && SourceEntityId.Equals(snapshot.SourceEntityId)
                && TargetEntityId.Equals(snapshot.TargetEntityId);
        }
    }

    /// <summary>
    /// Logically immutable policy snapshot. It is built before evaluation and
    /// sealed once; warmed reads are allocation-free.
    /// </summary>
    internal sealed class CharacterTestAttackOpportunitySamplePolicySnapshot
    {
        private readonly List<CharacterTestAttackOpportunitySamplePolicy> _policies;
        private readonly Dictionary<CharacterTestAttackOpportunitySampleIdentity, int> _policyIndices;
        private bool _isSealed;

        public CharacterTestAttackOpportunitySamplePolicySnapshot(int initialCapacity = 0)
        {
            _policies = new List<CharacterTestAttackOpportunitySamplePolicy>(
                initialCapacity < 0 ? 0 : initialCapacity);
            _policyIndices = new Dictionary<CharacterTestAttackOpportunitySampleIdentity, int>(Math.Max(0, initialCapacity));
        }

        public int Count => _policies.Count;
        public bool IsSealed => _isSealed;

        internal bool TryAdd(CharacterTestAttackOpportunitySamplePolicy policy)
        {
            if (_isSealed)
                return false;

            if (_policyIndices.ContainsKey(policy.SampleIdentity))
                return false;
            _policyIndices.Add(policy.SampleIdentity, _policies.Count);
            _policies.Add(policy);
            return true;
        }

        internal void Seal()
        {
            _isSealed = true;
        }

        internal bool TryGet(
            CharacterTestDirectHitTracePlanSample sample,
            out CharacterTestAttackOpportunitySamplePolicy policy)
        {
            CharacterTestAttackOpportunitySampleIdentity identity =
                CharacterTestAttackOpportunitySampleIdentity.From(sample);
            if (_policyIndices.TryGetValue(identity, out int index))
            {
                policy = _policies[index];
                return true;
            }

            policy = default(CharacterTestAttackOpportunitySamplePolicy);
            return false;
        }
    }

    /// <summary>
    /// All mutable runtime inputs are supplied by the caller and are validated
    /// against the immutable decision snapshot before a trace is evaluated.
    /// </summary>
    internal interface ICharacterTestAttackTurnPlanProvider
    {
        bool TryGetTurnPlan(long generation, out CharacterTestDirectHitTracePlanView plan);
    }

    internal readonly struct CharacterTestAttackOpportunityQuery
    {
        public CharacterTestAttackOpportunityQuery(
            CharacterTestAttackEvaluationSnapshot snapshot,
            CharacterTestDamageGeometrySnapshot targetGeometry,
            CombatPhysicsWorld physicsWorld,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterTestDirectHitTracePlanView currentPosePlan,
            bool hasCurrentPosePlan,
            CharacterTestDirectHitTracePlanView turnInPlacePlan,
            bool hasTurnInPlacePlan,
            RuntimeFrame currentCompletedFrame,
            int currentCombatPhysicsRevision,
            int currentSourcePoseRevision,
            int currentSourceFacingRevision,
            int currentTargetHurtboxRevision,
            int currentResolvedActionRevision,
            CharacterTestCombatWorldTargetClassificationSnapshot worldTargets,
            CharacterTestAttackOpportunitySamplePolicySnapshot samplePolicies,
            bool traceEvaluatorSupported = true,
            CharacterTestDamageZoneSnapshot damageZoneSnapshot = null,
            int requiredDamageZoneId = 0,
            CharacterTestDirectHitTracePlanView candidatePosePlan = default,
            bool hasCandidatePosePlan = false,
            FixVector3 candidateSourcePosition = default,
            int candidateSourceYawMilliDegrees = 0,
            int requiredLogicalHurtboxId = 0,
            ICharacterTestAttackTurnPlanProvider turnPlanProvider = null,
            long turnPlanGeneration = 0)
        {
            Snapshot = snapshot;
            TargetGeometry = targetGeometry;
            PhysicsWorld = physicsWorld;
            Source = source;
            Target = target;
            CurrentPosePlan = currentPosePlan;
            HasCurrentPosePlan = hasCurrentPosePlan;
            _turnInPlacePlan = turnInPlacePlan;
            _turnPlanProvider = turnPlanProvider;
            _turnPlanGeneration = turnPlanGeneration;
            HasTurnInPlacePlan = hasTurnInPlacePlan;
            CurrentCompletedFrame = currentCompletedFrame;
            CurrentCombatPhysicsRevision = currentCombatPhysicsRevision;
            CurrentSourcePoseRevision = currentSourcePoseRevision;
            CurrentSourceFacingRevision = currentSourceFacingRevision;
            CurrentTargetHurtboxRevision = currentTargetHurtboxRevision;
            CurrentResolvedActionRevision = currentResolvedActionRevision;
            WorldTargets = worldTargets;
            SamplePolicies = samplePolicies;
            TraceEvaluatorSupported = traceEvaluatorSupported;
            DamageZoneSnapshot = damageZoneSnapshot;
            RequiredDamageZoneId = requiredDamageZoneId;
            CandidatePosePlan = candidatePosePlan;
            HasCandidatePosePlan = hasCandidatePosePlan;
            CandidateSourcePosition = candidateSourcePosition;
            CandidateSourceYawMilliDegrees = candidateSourceYawMilliDegrees;
            RequiredLogicalHurtboxId = requiredLogicalHurtboxId;
        }

        public CharacterTestAttackEvaluationSnapshot Snapshot { get; }
        public CharacterTestDamageGeometrySnapshot TargetGeometry { get; }
        public CombatPhysicsWorld PhysicsWorld { get; }
        public CharacterTestCharacterEntity Source { get; }
        public CharacterTestCharacterEntity Target { get; }
        public CharacterTestDirectHitTracePlanView CurrentPosePlan { get; }
        public bool HasCurrentPosePlan { get; }
        private readonly CharacterTestDirectHitTracePlanView _turnInPlacePlan;
        private readonly ICharacterTestAttackTurnPlanProvider _turnPlanProvider;
        private readonly long _turnPlanGeneration;
        public CharacterTestDirectHitTracePlanView TurnInPlacePlan => _turnPlanProvider == null
            ? _turnInPlacePlan : _turnPlanProvider.TryGetTurnPlan(_turnPlanGeneration, out var plan) ? plan : default;
        public bool HasTurnInPlacePlan { get; }
        public RuntimeFrame CurrentCompletedFrame { get; }
        public int CurrentCombatPhysicsRevision { get; }
        public int CurrentSourcePoseRevision { get; }
        public int CurrentSourceFacingRevision { get; }
        public int CurrentTargetHurtboxRevision { get; }
        public int CurrentResolvedActionRevision { get; }
        public CharacterTestCombatWorldTargetClassificationSnapshot WorldTargets { get; }
        public CharacterTestAttackOpportunitySamplePolicySnapshot SamplePolicies { get; }
        public bool TraceEvaluatorSupported { get; }
        public CharacterTestDamageZoneSnapshot DamageZoneSnapshot { get; }
        public int RequiredDamageZoneId { get; }
        public CharacterTestDirectHitTracePlanView CandidatePosePlan { get; }
        public bool HasCandidatePosePlan { get; }
        public FixVector3 CandidateSourcePosition { get; }
        public int CandidateSourceYawMilliDegrees { get; }
        public int RequiredLogicalHurtboxId { get; }

        internal CharacterTestAttackOpportunityQuery WithDamageZoneFilter(
            CharacterTestDamageZoneSnapshot damageZoneSnapshot,
            int requiredDamageZoneId,
            int requiredLogicalHurtboxId = 0)
        {
            return new CharacterTestAttackOpportunityQuery(
                Snapshot,
                TargetGeometry,
                PhysicsWorld,
                Source,
                Target,
                CurrentPosePlan,
                HasCurrentPosePlan,
                _turnInPlacePlan,
                HasTurnInPlacePlan,
                CurrentCompletedFrame,
                CurrentCombatPhysicsRevision,
                CurrentSourcePoseRevision,
                CurrentSourceFacingRevision,
                CurrentTargetHurtboxRevision,
                CurrentResolvedActionRevision,
                WorldTargets,
                SamplePolicies,
                TraceEvaluatorSupported,
                damageZoneSnapshot,
                requiredDamageZoneId,
                CandidatePosePlan,
                HasCandidatePosePlan,
                CandidateSourcePosition,
                CandidateSourceYawMilliDegrees,
                requiredLogicalHurtboxId, _turnPlanProvider, _turnPlanGeneration);
        }

        internal CharacterTestAttackOpportunityQuery WithCandidatePose(
            CharacterTestDirectHitTracePlanView candidatePosePlan,
            FixVector3 candidateSourcePosition,
            int candidateSourceYawMilliDegrees)
        {
            return new CharacterTestAttackOpportunityQuery(
                Snapshot,
                TargetGeometry,
                PhysicsWorld,
                Source,
                Target,
                CurrentPosePlan,
                HasCurrentPosePlan,
                _turnInPlacePlan,
                HasTurnInPlacePlan,
                CurrentCompletedFrame,
                CurrentCombatPhysicsRevision,
                CurrentSourcePoseRevision,
                CurrentSourceFacingRevision,
                CurrentTargetHurtboxRevision,
                CurrentResolvedActionRevision,
                WorldTargets,
                SamplePolicies,
                TraceEvaluatorSupported,
                DamageZoneSnapshot,
                RequiredDamageZoneId,
                candidatePosePlan,
                hasCandidatePosePlan: true,
                candidateSourcePosition,
                candidateSourceYawMilliDegrees,
                RequiredLogicalHurtboxId, _turnPlanProvider, _turnPlanGeneration);
        }
    }

    /// <summary>
    /// No-allocation result of static-target PreviewTrace. A valid result may be
    /// a negative answer; callers must inspect both Status and opportunity fields.
    /// </summary>
    internal readonly struct CharacterTestAttackOpportunityResult
    {
        internal CharacterTestAttackOpportunityResult(
            CharacterTestAttackOpportunityStatus status,
            CharacterTestAttackEvaluationMode mode,
            CharacterTestAttackOpportunityLevel level,
            bool hasCompatibleHurtbox,
            bool envelopeIntersectsTarget,
            bool traceCanResolveTargetHit,
            bool canHitFromCurrentPose,
            bool canHitAfterTurnInPlace,
            bool requiresTranslation,
            int requiredYawDeltaMilliDegrees,
            int earliestCandidateHitTick,
            int traceSampleIndex,
            int stableSampleId,
            int selectedHurtboxId,
            int selectedHitZoneId,
            int selectedDamageZoneId,
            FixVector3 witnessPoint,
            FixVector3 aimPoint,
            CharacterTestAttackOpportunityFailureReason failureReason,
            CharacterTestAttackOpportunityFailureFlags failureFlags,
            FixVector3 sourceWitnessPoint = default(FixVector3),
            FixVector3 targetWitnessPoint = default(FixVector3),
            int acceptedHitCount = 0,
            CombatBodyId selectedBodyId = default(CombatBodyId),
            CombatColliderId selectedColliderId = default(CombatColliderId),
            CharacterTestAttackOpportunityEvidence evidence = default(CharacterTestAttackOpportunityEvidence))
        {
            Status = status;
            Mode = mode;
            Level = level;
            HasCompatibleHurtbox = hasCompatibleHurtbox;
            EnvelopeIntersectsTarget = envelopeIntersectsTarget;
            TraceCanResolveTargetHit = traceCanResolveTargetHit;
            CanHitFromCurrentPose = canHitFromCurrentPose;
            CanHitAfterTurnInPlace = canHitAfterTurnInPlace;
            RequiresTranslation = requiresTranslation;
            RequiredYawDeltaMilliDegrees = requiredYawDeltaMilliDegrees;
            EarliestCandidateHitTick = earliestCandidateHitTick;
            TraceSampleIndex = traceSampleIndex;
            StableSampleId = stableSampleId;
            SelectedHurtboxId = selectedHurtboxId;
            SelectedHitZoneId = selectedHitZoneId;
            SelectedDamageZoneId = selectedDamageZoneId;
            WitnessPoint = witnessPoint;
            SourceWitnessPoint = sourceWitnessPoint;
            TargetWitnessPoint = targetWitnessPoint;
            AimPoint = aimPoint;
            FailureReason = failureReason;
            FailureFlags = failureFlags;
            AcceptedHitCount = acceptedHitCount;
            SelectedBodyId = selectedBodyId;
            SelectedColliderId = selectedColliderId;
            Evidence = evidence;
        }

        public CharacterTestAttackOpportunityStatus Status { get; }
        public CharacterTestAttackEvaluationMode Mode { get; }
        public CharacterTestAttackOpportunityLevel Level { get; }
        public bool HasCompatibleHurtbox { get; }
        public bool EnvelopeIntersectsTarget { get; }
        public bool TraceCanResolveTargetHit { get; }
        public bool CanHitFromCurrentPose { get; }
        public bool CanHitAfterTurnInPlace { get; }
        public bool RequiresTranslation { get; }
        public int RequiredYawDeltaMilliDegrees { get; }
        public int EarliestCandidateHitTick { get; }
        public int TraceSampleIndex { get; }
        public int StableSampleId { get; }
        public int SelectedHurtboxId { get; }
        public int SelectedHitZoneId { get; }
        public int SelectedDamageZoneId { get; }
        public CombatBodyId SelectedBodyId { get; }
        public CombatColliderId SelectedColliderId { get; }
        /// <summary>Legacy witness alias retained as the target contact point.</summary>
        public FixVector3 WitnessPoint { get; }
        /// <summary>
        /// Representative midpoint of the source capsule for the selected
        /// sample. This is not asserted to be the geometric contact point.
        /// </summary>
        public FixVector3 SourceWitnessPoint { get; }
        /// <summary>Actual target-side contact returned by CombatPhysics.</summary>
        public FixVector3 TargetWitnessPoint { get; }
        public FixVector3 AimPoint { get; }
        public CharacterTestAttackOpportunityFailureReason FailureReason { get; }
        public CharacterTestAttackOpportunityFailureFlags FailureFlags { get; }
        public int AcceptedHitCount { get; }
        internal CharacterTestAttackOpportunityEvidence Evidence { get; }
        public CharacterTestAttackPredictionAssumption PredictionAssumption => Evidence.PredictionAssumption;
        public CombatEntityId SourceEntityId => Evidence.SourceEntityId;
        public CombatEntityId TargetEntityId => Evidence.TargetEntityId;
        public int ActionId => Evidence.ActionId;
        public RuntimeFrame SnapshotSimulationFrame => Evidence.SnapshotSimulationFrame;
        public int SnapshotCombatPhysicsRevision => Evidence.SnapshotCombatPhysicsRevision;
        public int SnapshotSourcePoseRevision => Evidence.SnapshotSourcePoseRevision;
        public int SnapshotSourceFacingRevision => Evidence.SnapshotSourceFacingRevision;
        public int SnapshotTargetHurtboxRevision => Evidence.SnapshotTargetHurtboxRevision;
        public int SnapshotResolvedActionRevision => Evidence.SnapshotResolvedActionRevision;
        public int ExpectedSourceYawMilliDegrees => Evidence.ExpectedSourceYawMilliDegrees;
        public int EvaluatedSourceYawMilliDegrees => Evidence.EvaluatedSourceYawMilliDegrees;
        public bool OrientationMatched => Evidence.OrientationMatched;
        public bool VerticalOverlap => Evidence.VerticalOverlap;
        public bool MaskCompatible => Evidence.MaskCompatible;
        public bool QualificationAccepted => Evidence.QualificationAccepted;
        public CombatBodyId BlockerBodyId => Evidence.BlockerBodyId;
        public CombatColliderId BlockerColliderId => Evidence.BlockerColliderId;
        public CharacterTestCombatWorldTargetKind BlockerWorldTargetKind => Evidence.BlockerWorldTargetKind;

        public bool IsUsable => Status == CharacterTestAttackOpportunityStatus.Valid;

        internal CharacterTestAttackOpportunityResult WithEvidence(
            CharacterTestAttackOpportunityEvidence evidence)
        {
            return new CharacterTestAttackOpportunityResult(
                Status,
                Mode,
                Level,
                HasCompatibleHurtbox,
                EnvelopeIntersectsTarget,
                TraceCanResolveTargetHit,
                CanHitFromCurrentPose,
                CanHitAfterTurnInPlace,
                RequiresTranslation,
                RequiredYawDeltaMilliDegrees,
                EarliestCandidateHitTick,
                TraceSampleIndex,
                StableSampleId,
                SelectedHurtboxId,
                SelectedHitZoneId,
                SelectedDamageZoneId,
                WitnessPoint,
                AimPoint,
                FailureReason,
                FailureFlags,
                SourceWitnessPoint,
                TargetWitnessPoint,
                AcceptedHitCount,
                SelectedBodyId,
                SelectedColliderId,
                evidence);
        }

        internal static CharacterTestAttackOpportunityResult Failure(
            CharacterTestOpportunityFailure failure,
            CharacterTestAttackEvaluationMode mode,
            FixVector3 aimPoint = default(FixVector3))
        {
            return new CharacterTestAttackOpportunityResult(
                failure.Status,
                mode,
                CharacterTestAttackOpportunityLevel.NoOpportunity,
                failure.HasCompatibleHurtbox,
                failure.EnvelopeIntersectsTarget,
                false,
                false,
                false,
                false,
                0,
                -1,
                -1,
                0,
                0,
                0,
                0,
                FixVector3.Zero,
                aimPoint,
                failure.Reason,
                failure.Flags);
        }
    }

    internal interface ICharacterTestAttackOpportunityEvaluator
    {
        CharacterTestAttackOpportunityResult Evaluate(
            CharacterTestAttackOpportunityQuery query,
            int requiredYawDeltaMilliDegrees = 0);
    }

    /// <summary>Internal reusable evaluator for one preview request.</summary>
    internal sealed class CharacterTestAttackOpportunityService : ICharacterTestAttackOpportunityEvaluator
    {
        private readonly List<CombatQueryResult> _queryHits = new List<CombatQueryResult>(32);
        private readonly List<CharacterTestAttackOpportunityCandidateEvidence> _tickEvidence =
            new List<CharacterTestAttackOpportunityCandidateEvidence>(32);
        private readonly CharacterTestHitResolutionPolicyState _previewPolicyState =
            new CharacterTestHitResolutionPolicyState();
        private readonly CharacterTestHitResolutionEvaluator _previewHitEvaluator =
            new CharacterTestHitResolutionEvaluator();
        private readonly CharacterTestAttackOpportunityTeamRelationProvider _previewTeamRelations =
            new CharacterTestAttackOpportunityTeamRelationProvider();

        CharacterTestAttackOpportunityResult ICharacterTestAttackOpportunityEvaluator.Evaluate(
            CharacterTestAttackOpportunityQuery query,
            int requiredYawDeltaMilliDegrees)
        {
            return Evaluate(query, requiredYawDeltaMilliDegrees);
        }

        private readonly struct CharacterTestAttackOpportunityCandidateEvidence
        {
            internal CharacterTestAttackOpportunityCandidateEvidence(
                HitCandidate candidate,
                CharacterTestDirectHitTracePlanSample sample,
                int sampleIndex,
                CharacterTestDamageGeometryColliderMetadata metadata,
                CharacterTestHitZoneResolution hitZone,
                CharacterTestAttackOpportunitySamplePolicy policy,
                bool isEnvironment,
                CharacterTestCombatWorldTargetKind worldTargetKind)
            {
                Candidate = candidate;
                Sample = sample;
                SampleIndex = sampleIndex;
                Metadata = metadata;
                HitZone = hitZone;
                Policy = policy;
                IsEnvironment = isEnvironment;
                WorldTargetKind = worldTargetKind;
            }

            internal HitCandidate Candidate { get; }
            internal CharacterTestDirectHitTracePlanSample Sample { get; }
            internal int SampleIndex { get; }
            internal CharacterTestDamageGeometryColliderMetadata Metadata { get; }
            internal CharacterTestHitZoneResolution HitZone { get; }
            internal CharacterTestAttackOpportunitySamplePolicy Policy { get; }
            internal bool IsEnvironment { get; }
            internal CharacterTestCombatWorldTargetKind WorldTargetKind { get; }
        }

        private readonly struct CharacterTestAttackOpportunityCompatibility
        {
            internal CharacterTestAttackOpportunityCompatibility(
                bool hasTargetEligiblePolicy,
                bool hasTargetStateEligiblePolicy,
                bool hasCompatibleHurtbox)
            {
                HasTargetEligiblePolicy = hasTargetEligiblePolicy;
                HasTargetStateEligiblePolicy = hasTargetStateEligiblePolicy;
                HasCompatibleHurtbox = hasCompatibleHurtbox;
            }

            internal bool HasTargetEligiblePolicy { get; }
            internal bool HasTargetStateEligiblePolicy { get; }
            internal bool HasCompatibleHurtbox { get; }
        }

        private sealed class CharacterTestAttackOpportunityTeamRelationProvider : ITeamRelationProvider
        {
            private CombatEntityId _sourceEntityId;
            private CombatEntityId _targetEntityId;
            private CharacterTestAttackOpportunityTargetKind _targetKind;

            internal void Configure(CharacterTestAttackOpportunitySamplePolicy policy)
            {
                _sourceEntityId = policy.SourceEntityId;
                _targetEntityId = policy.TargetEntityId;
                _targetKind = policy.TargetKind;
            }

            public bool AreHostile(CombatEntityId a, CombatEntityId b)
            {
                return IsKnownPair(a, b)
                    && _targetKind == CharacterTestAttackOpportunityTargetKind.HostileCharacter;
            }

            public bool AreFriendly(CombatEntityId a, CombatEntityId b)
            {
                return IsKnownPair(a, b)
                    && _targetKind == CharacterTestAttackOpportunityTargetKind.FriendlyCharacter;
            }

            public bool IsSameTeam(CombatEntityId a, CombatEntityId b)
            {
                return AreFriendly(a, b);
            }

            private bool IsKnownPair(CombatEntityId a, CombatEntityId b)
            {
                return (a.Equals(_sourceEntityId) && b.Equals(_targetEntityId))
                    || (a.Equals(_targetEntityId) && b.Equals(_sourceEntityId));
            }
        }

        /// <summary>
        /// Evaluates CurrentPose and, only after a pure spatial miss, the
        /// supplied TurnInPlace plan. Eligibility, state and blocker failures
        /// remain failures and are never rewritten as RequiresTranslation.
        /// </summary>
        internal CharacterTestAttackOpportunityResult Evaluate(
            CharacterTestAttackOpportunityQuery query,
            int requiredYawDeltaMilliDegrees = 0)
        {
            CharacterTestAttackOpportunityResult current = Evaluate(
                query,
                CharacterTestAttackEvaluationMode.CurrentPose,
                requiredYawDeltaMilliDegrees);
            if (current.Status != CharacterTestAttackOpportunityStatus.Valid
                || current.CanHitFromCurrentPose
                || !IsPureSpatialMiss(current))
            {
                return current;
            }

            if (!query.HasTurnInPlacePlan)
            {
                return CharacterTestAttackOpportunityResult.Failure(
                    CharacterTestOpportunityFailure.Unavailable(
                        CharacterTestAttackOpportunityFailureReason.TracePlanUnavailable,
                        CharacterTestAttackOpportunityFailureFlags.TracePlanUnavailable,
                        current.HasCompatibleHurtbox,
                        current.EnvelopeIntersectsTarget),
                    CharacterTestAttackEvaluationMode.CurrentPose,
                    current.AimPoint).WithEvidence(current.Evidence);
            }

            CharacterTestAttackOpportunityResult turned = Evaluate(
                query,
                CharacterTestAttackEvaluationMode.TurnInPlace,
                requiredYawDeltaMilliDegrees);
            if (turned.Status != CharacterTestAttackOpportunityStatus.Valid)
                return turned;

            if (turned.CanHitAfterTurnInPlace)
            {
                return new CharacterTestAttackOpportunityResult(
                    CharacterTestAttackOpportunityStatus.Valid,
                    CharacterTestAttackEvaluationMode.TurnInPlace,
                    CharacterTestAttackOpportunityLevel.TurnInPlace,
                    true,
                    current.EnvelopeIntersectsTarget || turned.EnvelopeIntersectsTarget,
                    true,
                    false,
                    true,
                    false,
                    QuantizeYawDelta(query, requiredYawDeltaMilliDegrees),
                    turned.EarliestCandidateHitTick,
                    turned.TraceSampleIndex,
                    turned.StableSampleId,
                    turned.SelectedHurtboxId,
                    turned.SelectedHitZoneId,
                    0,
                    turned.WitnessPoint,
                    turned.AimPoint,
                    CharacterTestAttackOpportunityFailureReason.None,
                    CharacterTestAttackOpportunityFailureFlags.None,
                    turned.SourceWitnessPoint,
                    turned.TargetWitnessPoint,
                    turned.AcceptedHitCount,
                    turned.SelectedBodyId,
                    turned.SelectedColliderId,
                    turned.Evidence);
            }

            if (!IsPureSpatialMiss(turned))
                return turned;

            return new CharacterTestAttackOpportunityResult(
                CharacterTestAttackOpportunityStatus.Valid,
                CharacterTestAttackEvaluationMode.TurnInPlace,
                CharacterTestAttackOpportunityLevel.RequiresTranslation,
                true,
                current.EnvelopeIntersectsTarget || turned.EnvelopeIntersectsTarget,
                false,
                false,
                false,
                true,
                QuantizeYawDelta(query, requiredYawDeltaMilliDegrees),
                -1,
                -1,
                0,
                0,
                0,
                0,
                FixVector3.Zero,
                turned.AimPoint,
                CharacterTestAttackOpportunityFailureReason.NoAcceptedHit,
                CharacterTestAttackOpportunityFailureFlags.NoAcceptedHit,
                evidence: turned.Evidence);
        }

        private readonly ModeResult[] _modeResults = new ModeResult[3];
        internal long ModeQueryCount { get; private set; }
        internal long ModeCacheHitCount { get; private set; }
        internal long ExactModeEvaluationCount { get; private set; }
        internal long SampleQueryCount { get; private set; }
        internal void ClearQueryCache() => Array.Clear(_modeResults, 0, _modeResults.Length);

        private struct ModeResult
        {
            internal bool HasValue;
            internal CharacterTestAttackOpportunityQuery Query;
            internal CharacterTestDirectHitTracePlanView Plan;
            internal CharacterTestAttackOpportunityResult Result;
            internal int Yaw;
            internal bool Matches(in CharacterTestAttackOpportunityQuery query,
                in CharacterTestDirectHitTracePlanView plan, int yaw)
            {
                return HasValue && Yaw == yaw && Plan.HasSameBackingPlan(plan)
                    && ReferenceEquals(Query.Source, query.Source) && ReferenceEquals(Query.Target, query.Target)
                    && ReferenceEquals(Query.PhysicsWorld, query.PhysicsWorld)
                    && ReferenceEquals(Query.TargetGeometry, query.TargetGeometry)
                    && ReferenceEquals(Query.SamplePolicies, query.SamplePolicies)
                    && ReferenceEquals(Query.WorldTargets, query.WorldTargets)
                    && ReferenceEquals(Query.DamageZoneSnapshot, query.DamageZoneSnapshot)
                    && Query.CurrentCompletedFrame == query.CurrentCompletedFrame
                    && Query.CurrentCombatPhysicsRevision == query.CurrentCombatPhysicsRevision
                    && Query.CurrentSourcePoseRevision == query.CurrentSourcePoseRevision
                    && Query.CurrentSourceFacingRevision == query.CurrentSourceFacingRevision
                    && Query.CurrentTargetHurtboxRevision == query.CurrentTargetHurtboxRevision
                    && Query.CurrentResolvedActionRevision == query.CurrentResolvedActionRevision
                    && Query.Snapshot.SourceGameplayEntityId.Equals(query.Snapshot.SourceGameplayEntityId)
                    && Query.Snapshot.TargetEntityId.Equals(query.Snapshot.TargetEntityId)
                    && Query.Snapshot.TargetGameplayEntityId.Equals(query.Snapshot.TargetGameplayEntityId)
                    && (Query.RequiredDamageZoneId == query.RequiredDamageZoneId)
                    && (Query.RequiredLogicalHurtboxId == query.RequiredLogicalHurtboxId);
            }
        }

        /// <summary>Evaluates exactly one mode without changing runtime state.</summary>
        internal CharacterTestAttackOpportunityResult Evaluate(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestAttackEvaluationMode mode,
            int requiredYawDeltaMilliDegrees = 0)
        {
            ModeQueryCount++;
            var validation = ValidateQuery(query, mode, requiredYawDeltaMilliDegrees);
            bool valid = validation.Status == CharacterTestAttackOpportunityStatus.Valid
                && validation.Reason == CharacterTestAttackOpportunityFailureReason.None;
            int index = (int)mode;
            var plan = valid ? SelectPlan(query, mode) : default;
            if (valid && index >= 0 && index < _modeResults.Length
                && _modeResults[index].Matches(query, plan, requiredYawDeltaMilliDegrees))
            {
                ModeCacheHitCount++;
                return _modeResults[index].Result;
            }
            ExactModeEvaluationCount++;
            var result = EvaluateModeCore(query, mode, requiredYawDeltaMilliDegrees, validation);
            result = result.WithEvidence(CreateResultEvidence(query, mode, requiredYawDeltaMilliDegrees, result));
            if (valid && index >= 0 && index < _modeResults.Length)
                _modeResults[index] = new ModeResult { HasValue = true, Query = query, Plan = plan,
                    Result = result, Yaw = requiredYawDeltaMilliDegrees };
            return result;
        }

        private CharacterTestAttackOpportunityResult EvaluateModeCore(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestAttackEvaluationMode mode,
            int requiredYawDeltaMilliDegrees, CharacterTestOpportunityFailure validationFailure)
        {
            FixVector3 aimPoint = GetAimPoint(query.TargetGeometry);
            if (validationFailure.Status != CharacterTestAttackOpportunityStatus.Valid
                || validationFailure.Reason != CharacterTestAttackOpportunityFailureReason.None)
            {
                return CharacterTestAttackOpportunityResult.Failure(
                    validationFailure,
                    mode,
                    aimPoint);
            }

            CharacterTestDirectHitTracePlanView plan = SelectPlan(query, mode);
            if (!TryAnalyzeCompatibility(
                    query,
                    plan,
                    out CharacterTestAttackOpportunityCompatibility compatibility))
            {
                return CharacterTestAttackOpportunityResult.Failure(
                    CharacterTestOpportunityFailure.Unavailable(
                        CharacterTestAttackOpportunityFailureReason.TracePlanInvalid,
                        CharacterTestAttackOpportunityFailureFlags.TracePlanInvalid,
                        false,
                        false),
                    mode,
                    aimPoint);
            }

            if (!compatibility.HasTargetEligiblePolicy)
            {
                return CharacterTestAttackOpportunityResult.Failure(
                    CharacterTestOpportunityFailure.Valid(
                        CharacterTestAttackOpportunityFailureReason.TargetNotEligible,
                        CharacterTestAttackOpportunityFailureFlags.TargetNotEligible,
                        false,
                        false),
                    mode,
                    aimPoint);
            }

            if (!compatibility.HasTargetStateEligiblePolicy)
            {
                return CharacterTestAttackOpportunityResult.Failure(
                    CharacterTestOpportunityFailure.Valid(
                        CharacterTestAttackOpportunityFailureReason.TargetStateRejected,
                        CharacterTestAttackOpportunityFailureFlags.TargetStateRejected,
                        false,
                        false),
                    mode,
                    aimPoint);
            }

            if (!compatibility.HasCompatibleHurtbox)
            {
                return CharacterTestAttackOpportunityResult.Failure(
                    CharacterTestOpportunityFailure.Valid(
                        CharacterTestAttackOpportunityFailureReason.NoCompatibleHurtbox,
                        CharacterTestAttackOpportunityFailureFlags.NoCompatibleHurtbox,
                        false,
                        false),
                    mode,
                    aimPoint);
            }

            CharacterTestAttackOpportunityFailureFlags failureFlags =
                CharacterTestAttackOpportunityFailureFlags.None;
            CharacterTestAttackOpportunityFailureReason failureReason =
                CharacterTestAttackOpportunityFailureReason.NoAcceptedHit;
            bool envelopeIntersectsTarget = false;
            bool blockedAnySample = false;
            bool nonTargetActorStateUnavailable = false;
            bool damagePayloadUnavailable = false;
            bool damagePayloadInvalid = false;
            bool targetStateRejected = false;
            bool targetNotEligible = false;
            int acceptedHitCount = 0;
            bool hasAcceptedHit = false;
            CharacterTestAttackOpportunityCandidateEvidence bestAcceptedEvidence = default;
            bool hasBlockerEvidence = false;
            CharacterTestAttackOpportunityCandidateEvidence bestBlockerEvidence = default;
            _previewPolicyState.Clear();

            int planCount = plan.Count;
            int processedSampleCount = 0;
            while (processedSampleCount < planCount)
            {
                if (!plan.TryGetSampleInFrameOrder(processedSampleCount, out _, out var firstSample))
                    return InvalidPlanResult(mode, compatibility.HasCompatibleHurtbox, envelopeIntersectsTarget, aimPoint);
                int nextLocalFrame = firstSample.LocalFrame;
                if (nextLocalFrame < 0)
                {
                    return CharacterTestAttackOpportunityResult.Failure(
                        CharacterTestOpportunityFailure.Unavailable(
                            CharacterTestAttackOpportunityFailureReason.TracePlanInvalid,
                            CharacterTestAttackOpportunityFailureFlags.TracePlanInvalid,
                            compatibility.HasCompatibleHurtbox,
                            envelopeIntersectsTarget),
                        mode,
                        aimPoint);
                }

                _tickEvidence.Clear();
                while (processedSampleCount < planCount)
                {
                    if (!plan.TryGetSampleInFrameOrder(processedSampleCount, out int sampleIndex, out CharacterTestDirectHitTracePlanSample sample))
                        return InvalidPlanResult(mode, compatibility.HasCompatibleHurtbox, envelopeIntersectsTarget, aimPoint);
                    if (sample.LocalFrame != nextLocalFrame) break;
                    processedSampleCount++;
                    if (!query.SamplePolicies.TryGet(
                            sample,
                            out CharacterTestAttackOpportunitySamplePolicy policy)
                        || !policy.IsBoundTo(sample, query.Snapshot))
                    {
                        return InvalidPlanResult(mode, compatibility.HasCompatibleHurtbox, envelopeIntersectsTarget, aimPoint);
                    }

                    if (!IsStableTargetEligible(policy))
                        continue;

                    if (!QuerySample(
                            query,
                            sample,
                            sampleIndex,
                            policy,
                            ref envelopeIntersectsTarget,
                            ref aimPoint,
                            ref blockedAnySample,
                            ref nonTargetActorStateUnavailable))
                    {
                        return CharacterTestAttackOpportunityResult.Failure(
                            nonTargetActorStateUnavailable
                                ? CharacterTestOpportunityFailure.Unavailable(
                                    CharacterTestAttackOpportunityFailureReason.NonTargetActorStateUnavailable,
                                    CharacterTestAttackOpportunityFailureFlags.NonTargetActorStateUnavailable,
                                    compatibility.HasCompatibleHurtbox,
                                    envelopeIntersectsTarget)
                                : CharacterTestOpportunityFailure.Stale(
                                    CharacterTestAttackOpportunityFailureReason.StaleSnapshot,
                                    CharacterTestAttackOpportunityFailureFlags.SnapshotMismatch),
                            mode,
                            aimPoint);
                    }
                }

                SortTickEvidence();
                for (int evidenceIndex = 0; evidenceIndex < _tickEvidence.Count; evidenceIndex++)
                {
                    CharacterTestAttackOpportunityCandidateEvidence evidence = _tickEvidence[evidenceIndex];
                    if (evidence.IsEnvironment)
                    {
                        _previewTeamRelations.Configure(evidence.Policy);
                        HitResolveKind environmentKind = _previewHitEvaluator.Resolve(
                            evidence.Candidate,
                            _previewTeamRelations).Kind;
                        if (environmentKind == HitResolveKind.Blocked)
                        {
                            blockedAnySample = true;
                            if (!hasBlockerEvidence
                                || IsEarlierEvidence(evidence, bestBlockerEvidence))
                            {
                                bestBlockerEvidence = evidence;
                                hasBlockerEvidence = true;
                            }
                        }
                        continue;
                    }

                    // Actual direct-hit profiling resolves damage eligibility
                    // before a candidate reaches HitResolveSystem. Keep Preview
                    // on that same side-effect-free order so a missing/invalid
                    // action payload can never become a speculative hit.
                    CharacterTestDirectHitDamageEligibility damageEligibility =
                        CharacterTestCombatDamageResolver.EvaluateDirectHitDamageEligibility(
                            evidence.Policy.DamagePayload,
                            evidence.HitZone);
                    if (damageEligibility == CharacterTestDirectHitDamageEligibility.MissingPayload)
                    {
                        damagePayloadUnavailable = true;
                        continue;
                    }
                    if (damageEligibility == CharacterTestDirectHitDamageEligibility.InvalidPayload)
                    {
                        damagePayloadInvalid = true;
                        continue;
                    }

                    _previewTeamRelations.Configure(evidence.Policy);
                    HitResolveKind kind = _previewHitEvaluator.Resolve(
                        evidence.Candidate,
                        _previewTeamRelations).Kind;
                    if (kind == HitResolveKind.Damage || kind == HitResolveKind.Blocked)
                    {
                        if (!_previewPolicyState.TryAccept(
                                evidence.Candidate,
                                evidence.Sample.PolicyEventId,
                                evidence.Policy.HitEvent))
                        {
                            continue;
                        }

                        acceptedHitCount++;
                        if (!hasAcceptedHit || IsEarlierEvidence(evidence, bestAcceptedEvidence))
                        {
                            bestAcceptedEvidence = evidence;
                            hasAcceptedHit = true;
                        }
                    }
                    else if (kind == HitResolveKind.TargetDead
                        || kind == HitResolveKind.Invincible
                        || kind == HitResolveKind.Evaded
                        || kind == HitResolveKind.Parried)
                    {
                        targetStateRejected = true;
                    }
                    else if (kind == HitResolveKind.Friendly
                        || kind == HitResolveKind.SelfDamage)
                    {
                        targetNotEligible = true;
                    }
                }

            }

            if (hasAcceptedHit)
            {
                CharacterTestAttackOpportunityCandidateEvidence evidence = bestAcceptedEvidence;
                return new CharacterTestAttackOpportunityResult(
                    CharacterTestAttackOpportunityStatus.Valid,
                    mode,
                    mode == CharacterTestAttackEvaluationMode.CurrentPose
                        ? CharacterTestAttackOpportunityLevel.CurrentPose
                        : mode == CharacterTestAttackEvaluationMode.TurnInPlace
                            ? CharacterTestAttackOpportunityLevel.TurnInPlace
                            : CharacterTestAttackOpportunityLevel.RequiresTranslation,
                    true,
                    true,
                    true,
                    mode == CharacterTestAttackEvaluationMode.CurrentPose,
                    mode != CharacterTestAttackEvaluationMode.CandidatePose,
                    mode == CharacterTestAttackEvaluationMode.CandidatePose,
                    QuantizeYawDelta(query, requiredYawDeltaMilliDegrees),
                    evidence.Sample.LocalFrame,
                    evidence.SampleIndex,
                    evidence.Sample.StableSampleId,
                    evidence.Metadata.LogicalColliderId,
                    evidence.HitZone.HitZoneId,
                    query.RequiredDamageZoneId,
                    evidence.Candidate.PhysicsHit.Point,
                    evidence.Candidate.PhysicsHit.Point,
                    CharacterTestAttackOpportunityFailureReason.None,
                    CharacterTestAttackOpportunityFailureFlags.None,
                    GetRepresentativeSourceCapsuleMidpoint(evidence.Sample.Query),
                    evidence.Candidate.PhysicsHit.Point,
                    acceptedHitCount,
                    evidence.Candidate.PhysicsHit.TargetBodyId,
                    evidence.Candidate.PhysicsHit.TargetColliderId);
            }

            if (damagePayloadUnavailable || damagePayloadInvalid)
            {
                CharacterTestAttackOpportunityFailureReason damageFailureReason =
                    damagePayloadUnavailable
                        ? CharacterTestAttackOpportunityFailureReason.DamagePayloadUnavailable
                        : CharacterTestAttackOpportunityFailureReason.DamagePayloadInvalid;
                CharacterTestAttackOpportunityFailureFlags damageFailureFlag =
                    damagePayloadUnavailable
                        ? CharacterTestAttackOpportunityFailureFlags.DamagePayloadUnavailable
                        : CharacterTestAttackOpportunityFailureFlags.DamagePayloadInvalid;
                return CharacterTestAttackOpportunityResult.Failure(
                    CharacterTestOpportunityFailure.Unavailable(
                        damageFailureReason,
                        damageFailureFlag,
                        compatibility.HasCompatibleHurtbox,
                        envelopeIntersectsTarget),
                    mode,
                    aimPoint);
            }

            if (targetNotEligible)
            {
                failureReason = CharacterTestAttackOpportunityFailureReason.TargetNotEligible;
                failureFlags |= CharacterTestAttackOpportunityFailureFlags.TargetNotEligible;
            }
            else if (targetStateRejected)
            {
                failureReason = CharacterTestAttackOpportunityFailureReason.TargetStateRejected;
                failureFlags |= CharacterTestAttackOpportunityFailureFlags.TargetStateRejected;
            }
            else if (blockedAnySample)
            {
                failureReason = CharacterTestAttackOpportunityFailureReason.BlockedByEnvironment;
                failureFlags |= CharacterTestAttackOpportunityFailureFlags.BlockedByEnvironment;
            }
            else
            {
                failureReason = CharacterTestAttackOpportunityFailureReason.NoAcceptedHit;
                failureFlags |= CharacterTestAttackOpportunityFailureFlags.NoAcceptedHit;
            }

            return new CharacterTestAttackOpportunityResult(
                CharacterTestAttackOpportunityStatus.Valid,
                mode,
                CharacterTestAttackOpportunityLevel.NoOpportunity,
                compatibility.HasCompatibleHurtbox,
                envelopeIntersectsTarget,
                false,
                false,
                false,
                false,
                QuantizeYawDelta(query, requiredYawDeltaMilliDegrees),
                -1,
                -1,
                0,
                0,
                0,
                0,
                FixVector3.Zero,
                aimPoint,
                failureReason,
                failureFlags,
                evidence: hasBlockerEvidence
                    ? CharacterTestAttackOpportunityEvidence.ForBlocker(
                        bestBlockerEvidence.Candidate.PhysicsHit.TargetBodyId,
                        bestBlockerEvidence.Candidate.PhysicsHit.TargetColliderId,
                        bestBlockerEvidence.WorldTargetKind)
                    : default(CharacterTestAttackOpportunityEvidence));
        }

        private bool QuerySample(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestDirectHitTracePlanSample sample,
            int sampleIndex,
            CharacterTestAttackOpportunitySamplePolicy policy,
            ref bool envelopeIntersectsTarget,
            ref FixVector3 aimPoint,
            ref bool blockedAnySample,
            ref bool nonTargetActorStateUnavailable)
        {
            SampleQueryCount++;
            _queryHits.Clear();
            query.PhysicsWorld.QueryCapsule(sample.Query, _queryHits, includeSourceEntity: false);
            for (int hitIndex = 0; hitIndex < _queryHits.Count; hitIndex++)
            {
                CombatQueryResult hit = _queryHits[hitIndex];
                bool isEnvironment = CharacterTestCombatHitEventResolver.IsStaticEnvironmentTarget(
                    hit.TargetEntityId);
                if (isEnvironment)
                {
                    CharacterTestCombatWorldQualification qualification =
                        CharacterTestCombatHitEventResolver.QualifyWorldTarget(
                            policy.HitEvent,
                            hit,
                            query.CurrentCompletedFrame,
                            query.PhysicsWorld,
                            query.WorldTargets,
                            out CharacterTestCombatWorldTargetKind worldTargetKind);
                    if (qualification == CharacterTestCombatWorldQualification.StaleAuthority)
                        return false;
                    if (qualification == CharacterTestCombatWorldQualification.Rejected)
                        continue;

                    // Direct-hit actions currently expose no penetration count.
                    // Preview therefore has one explicit behavior: zero
                    // penetration. The first accepted BlockedPresentation world
                    // candidate ends this sample's ordered query.
                    HitCandidate environmentCandidate = CreateCandidate(
                        query,
                        sample,
                        hit,
                        HitTargetStateFlags.Alive | HitTargetStateFlags.Blocking);
                    _tickEvidence.Add(new CharacterTestAttackOpportunityCandidateEvidence(
                        environmentCandidate,
                        sample,
                        sampleIndex,
                        default(CharacterTestDamageGeometryColliderMetadata),
                        CharacterTestHitZoneResolution.Default,
                        policy,
                        isEnvironment: true,
                        worldTargetKind: worldTargetKind));
                    blockedAnySample = true;
                    break;
                }

                if (!hit.TargetEntityId.Equals(query.Snapshot.TargetEntityId))
                {
                    // M1 freezes only the selected target's hurtbox and policy.
                    // A preceding/following actor can consume MaxAcceptedHits
                    // in actual ordered resolution, but this query has no frozen
                    // team/state/policy snapshot for that actor. Do not claim a
                    // valid opportunity from an incomplete world contract.
                    nonTargetActorStateUnavailable = true;
                    return false;
                }

                if (!query.TargetGeometry.TryGetCollider(
                        hit.TargetColliderId,
                        out CharacterTestDamageGeometryColliderMetadata metadata))
                {
                    continue;
                }

                if ((query.RequiredDamageZoneId != 0)
                    && (query.DamageZoneSnapshot == null
                        || !query.DamageZoneSnapshot.ContainsHitZone(
                            query.RequiredDamageZoneId,
                            metadata.HitZoneId)))
                {
                    continue;
                }

                if ((query.RequiredLogicalHurtboxId != 0)
                    && !(query.RequiredLogicalHurtboxId == metadata.LogicalColliderId))
                {
                    continue;
                }

                envelopeIntersectsTarget = true;
                aimPoint = hit.Point;
                _tickEvidence.Add(new CharacterTestAttackOpportunityCandidateEvidence(
                    CreateCandidate(query, sample, hit, policy.TargetState),
                    sample,
                    sampleIndex,
                    metadata,
                    CharacterTestCombatDamageResolver.ResolveFrozenHitZone(metadata),
                    policy,
                    isEnvironment: false,
                    worldTargetKind: CharacterTestCombatWorldTargetKind.Unknown));
            }

            return true;
        }

        private static HitCandidate CreateCandidate(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestDirectHitTracePlanSample sample,
            CombatQueryResult hit,
            HitTargetStateFlags targetState)
        {
            return new HitCandidate(
                sample.Query.Header.SourceEntityId,
                hit.TargetEntityId,
                sample.ActionId,
                sample.HitWindowId,
                sample.TraceId,
                ToCombatFrame(query.CurrentCompletedFrame, sample.LocalFrame),
                hit,
                damage: 0,
                staggerFrames: 0,
                knockback: FixVector3.Zero,
                targetState,
                sample.ResolvePriority);
        }

        private static CharacterTestOpportunityFailure ValidateQuery(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestAttackEvaluationMode mode,
            int requiredYawDeltaMilliDegrees)
        {
            if (!query.TraceEvaluatorSupported)
            {
                return CharacterTestOpportunityFailure.Unsupported(
                    CharacterTestAttackOpportunityFailureReason.TraceEvaluatorUnsupported,
                    CharacterTestAttackOpportunityFailureFlags.TraceEvaluatorUnsupported);
            }

            if (query.PhysicsWorld == null
                || query.Source == null
                || query.Target == null
                || query.TargetGeometry == null
                || !query.TargetGeometry.IsValid
                || query.WorldTargets == null
                || query.SamplePolicies == null
                || !query.SamplePolicies.IsSealed)
            {
                return CharacterTestOpportunityFailure.Unavailable(
                    CharacterTestAttackOpportunityFailureReason.SnapshotUnavailable,
                    CharacterTestAttackOpportunityFailureFlags.SnapshotUnavailable);
            }

            if (query.CurrentCombatPhysicsRevision != query.PhysicsWorld.Revision
                || query.CurrentTargetHurtboxRevision != query.TargetGeometry.HurtboxRevision
                || !query.WorldTargets.IsCurrent(
                    query.CurrentCompletedFrame,
                    query.CurrentCombatPhysicsRevision)
                || !query.Snapshot.IsCurrent(
                    query.CurrentCompletedFrame,
                    query.PhysicsWorld,
                    query.Source,
                    query.Target,
                    query.TargetGeometry,
                    query.CurrentSourcePoseRevision,
                    query.CurrentSourceFacingRevision,
                    query.CurrentResolvedActionRevision))
            {
                return CharacterTestOpportunityFailure.Stale(
                    CharacterTestAttackOpportunityFailureReason.StaleSnapshot,
                    CharacterTestAttackOpportunityFailureFlags.SnapshotMismatch);
            }

            bool hasPlan = HasPlan(query, mode);
            if (!hasPlan)
            {
                return CharacterTestOpportunityFailure.Unavailable(
                    CharacterTestAttackOpportunityFailureReason.TracePlanUnavailable,
                    CharacterTestAttackOpportunityFailureFlags.TracePlanUnavailable);
            }

            CharacterTestDirectHitTracePlanView plan = SelectPlan(query, mode);
            CharacterTestDirectHitTracePlanPoseKind expectedPoseKind =
                mode == CharacterTestAttackEvaluationMode.CurrentPose
                    ? CharacterTestDirectHitTracePlanPoseKind.CurrentPose
                    : mode == CharacterTestAttackEvaluationMode.TurnInPlace
                        ? CharacterTestDirectHitTracePlanPoseKind.TurnInPlace
                        : CharacterTestDirectHitTracePlanPoseKind.CandidatePose;
            FixVector3 expectedSourcePosition = mode == CharacterTestAttackEvaluationMode.CandidatePose
                ? query.CandidateSourcePosition
                : query.Snapshot.SourcePosition;
            int expectedYawMilliDegrees = mode == CharacterTestAttackEvaluationMode.CurrentPose
                ? query.Snapshot.SourceYawMilliDegrees
                : mode == CharacterTestAttackEvaluationMode.TurnInPlace
                    ? query.Snapshot.SourceYawMilliDegrees
                        + QuantizeYawDelta(query, requiredYawDeltaMilliDegrees)
                    : query.CandidateSourceYawMilliDegrees;
            if (!plan.IsCurrent(
                    query.CurrentCompletedFrame,
                    query.CurrentCombatPhysicsRevision,
                    query.Snapshot.SourceEntityId,
                    query.CurrentResolvedActionRevision,
                    expectedPoseKind,
                    expectedSourcePosition,
                    expectedYawMilliDegrees,
                    query.CurrentSourcePoseRevision,
                    query.CurrentSourceFacingRevision))
            {
                return CharacterTestOpportunityFailure.Stale(
                    CharacterTestAttackOpportunityFailureReason.StaleSnapshot,
                    CharacterTestAttackOpportunityFailureFlags.TracePlanMismatch);
            }

            if (plan.Count <= 0)
            {
                return CharacterTestOpportunityFailure.Unavailable(
                    CharacterTestAttackOpportunityFailureReason.TracePlanInvalid,
                    CharacterTestAttackOpportunityFailureFlags.TracePlanInvalid,
                    false,
                    false);
            }

            return CharacterTestOpportunityFailure.Valid(
                CharacterTestAttackOpportunityFailureReason.None,
                CharacterTestAttackOpportunityFailureFlags.None,
                false,
                false);
        }

        private static bool TryAnalyzeCompatibility(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestDirectHitTracePlanView plan,
            out CharacterTestAttackOpportunityCompatibility compatibility)
        {
            bool hasTargetEligiblePolicy = false;
            bool hasTargetStateEligiblePolicy = false;
            bool hasCompatibleHurtbox = false;
            int homogeneousActionId = 0;
            for (int sampleIndex = 0; sampleIndex < plan.Count; sampleIndex++)
            {
                if (!plan.TryGetSample(sampleIndex, out CharacterTestDirectHitTracePlanSample sample)
                    || !IsSampleStructurallyValid(sample, query.Snapshot)
                    || (homogeneousActionId != 0 && sample.ActionId != homogeneousActionId)
                    || !query.SamplePolicies.TryGet(
                        sample,
                        out CharacterTestAttackOpportunitySamplePolicy policy)
                    || !policy.IsBoundTo(sample, query.Snapshot))
                {
                    compatibility = default(CharacterTestAttackOpportunityCompatibility);
                    return false;
                }

                homogeneousActionId = sample.ActionId;

                if (!IsStableTargetEligible(policy))
                    continue;

                hasTargetEligiblePolicy = true;
                if (!CanTargetStateResolve(policy.TargetState))
                    continue;

                hasTargetStateEligiblePolicy = true;
                if (!hasCompatibleHurtbox
                    && HasMaskCompatibleHurtbox(query, sample.Query.Header.LayerMask))
                {
                    hasCompatibleHurtbox = true;
                }
            }

            compatibility = new CharacterTestAttackOpportunityCompatibility(
                hasTargetEligiblePolicy,
                hasTargetStateEligiblePolicy,
                hasCompatibleHurtbox);
            return true;
        }

        private static bool IsStableTargetEligible(
            CharacterTestAttackOpportunitySamplePolicy policy)
        {
            if (!CharacterTestCombatHitEventResolver.AllowsTarget(
                    policy.HitEvent,
                    policy.TargetKind,
                    policy.TargetEntityId,
                    out bool environmentTarget)
                || environmentTarget)
            {
                return false;
            }

            // This is the same allowFriendlyFire=false boundary used by the
            // actual CharacterTest direct-hit resolver.
            return policy.TargetKind != CharacterTestAttackOpportunityTargetKind.FriendlyCharacter
                && policy.TargetKind != CharacterTestAttackOpportunityTargetKind.Self;
        }

        private static bool CanTargetStateResolve(HitTargetStateFlags state)
        {
            return (state & HitTargetStateFlags.Alive) != 0
                && (state & HitTargetStateFlags.Invincible) == 0
                && (state & HitTargetStateFlags.Unhittable) == 0
                && (state & HitTargetStateFlags.Parrying) == 0;
        }

        private static bool HasMaskCompatibleHurtbox(
            CharacterTestAttackOpportunityQuery query,
            CombatPhysicsLayerMask sampleMask)
        {
            CharacterTestDamageGeometrySnapshot geometry = query.TargetGeometry;
            if (geometry == null || !geometry.IsValid || sampleMask.Value == 0)
                return false;

            CombatBodyId bodyId = new CombatBodyId(
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxBodyIdStart
                + geometry.EntityId);
            for (int colliderIndex = 0; colliderIndex < geometry.ColliderCount; colliderIndex++)
            {
                CharacterTestDamageGeometryColliderMetadata metadata = geometry.GetCollider(colliderIndex);
                if (metadata.CombatColliderId.IsNone
                    || (metadata.LogicalColliderId == 0)
                    || !TryGetColliderLayer(
                        query.PhysicsWorld,
                        bodyId,
                        metadata,
                        out int colliderLayer))
                {
                    continue;
                }

                if (sampleMask.ContainsLayer(colliderLayer))
                    return true;
            }

            return false;
        }

        private static bool TryGetColliderLayer(
            CombatPhysicsWorld world,
            CombatBodyId bodyId,
            CharacterTestDamageGeometryColliderMetadata metadata,
            out int layer)
        {
            // Compound hurtbox leaves are not exposed by the standalone
            // collider accessors. The damage-geometry snapshot captures the
            // layer from the same authoritative registration, so prefer that
            // immutable metadata and retain the accessor path for legacy
            // non-compound geometry.
            if (metadata.HasLayer)
            {
                layer = metadata.Layer;
                return true;
            }

            layer = 0;
            switch (metadata.LocalShape.Kind)
            {
                case CombatPhysicsShapeKind.Aabb:
                    if (world.TryGetAabbCollider(bodyId, metadata.CombatColliderId, out CombatPhysicsAabbCollider aabb))
                    {
                        layer = aabb.Layer;
                        return true;
                    }
                    break;
                case CombatPhysicsShapeKind.Sphere:
                    if (world.TryGetSphereCollider(bodyId, metadata.CombatColliderId, out CombatPhysicsSphereCollider sphere))
                    {
                        layer = sphere.Layer;
                        return true;
                    }
                    break;
                case CombatPhysicsShapeKind.Capsule:
                    if (world.TryGetCapsuleCollider(bodyId, metadata.CombatColliderId, out CombatPhysicsCapsuleCollider capsule))
                    {
                        layer = capsule.Layer;
                        return true;
                    }
                    break;
                case CombatPhysicsShapeKind.Obb:
                    if (world.TryGetObbCollider(bodyId, metadata.CombatColliderId, out CombatPhysicsObbCollider obb))
                    {
                        layer = obb.Layer;
                        return true;
                    }
                    break;
            }

            return false;
        }

        private static bool IsSampleStructurallyValid(
            CharacterTestDirectHitTracePlanSample sample,
            CharacterTestAttackEvaluationSnapshot snapshot)
        {
            return sample.LocalFrame >= 0
                && sample.HitWindowId >= 0
                && sample.TraceId >= 0
                && sample.PolicyEventId > 0
                && sample.ActionId >= 0
                && sample.Query.Header.TraceId == sample.TraceId
                && sample.Query.Header.SourceEntityId.Equals(snapshot.SourceEntityId);
        }

        private void SortTickEvidence()
        {
            // Insertion sort is deterministic and allocation-free on the warmed
            // path. Candidate.CompareTo contains body/collider identity.
            for (int i = 1; i < _tickEvidence.Count; i++)
            {
                CharacterTestAttackOpportunityCandidateEvidence value = _tickEvidence[i];
                int j = i - 1;
                while (j >= 0 && CompareEvidence(_tickEvidence[j], value) > 0)
                {
                    _tickEvidence[j + 1] = _tickEvidence[j];
                    j--;
                }
                _tickEvidence[j + 1] = value;
            }
        }

        private static int CompareEvidence(
            CharacterTestAttackOpportunityCandidateEvidence left,
            CharacterTestAttackOpportunityCandidateEvidence right)
        {
            int compare = left.Candidate.CompareTo(right.Candidate);
            if (compare != 0)
                return compare;
            compare = left.SampleIndex.CompareTo(right.SampleIndex);
            if (compare != 0)
                return compare;
            return left.Sample.StableSampleId.CompareTo(right.Sample.StableSampleId);
        }

        private static bool IsEarlierEvidence(
            CharacterTestAttackOpportunityCandidateEvidence candidate,
            CharacterTestAttackOpportunityCandidateEvidence current)
        {
            int compare = candidate.Sample.LocalFrame.CompareTo(current.Sample.LocalFrame);
            if (compare != 0)
                return compare < 0;
            compare = candidate.Candidate.PhysicsHit.CompareTo(current.Candidate.PhysicsHit);
            if (compare != 0)
                return compare < 0;
            compare = candidate.SampleIndex.CompareTo(current.SampleIndex);
            if (compare != 0)
                return compare < 0;
            return candidate.Sample.StableSampleId.CompareTo(current.Sample.StableSampleId) < 0;
        }

        private static bool IsPureSpatialMiss(CharacterTestAttackOpportunityResult result)
        {
            return result.Status == CharacterTestAttackOpportunityStatus.Valid
                && result.HasCompatibleHurtbox
                && !result.TraceCanResolveTargetHit
                && result.FailureReason == CharacterTestAttackOpportunityFailureReason.NoAcceptedHit
                && result.FailureFlags == CharacterTestAttackOpportunityFailureFlags.NoAcceptedHit;
        }

        private static CharacterTestAttackOpportunityResult InvalidPlanResult(
            CharacterTestAttackEvaluationMode mode,
            bool hasCompatibleHurtbox,
            bool envelopeIntersectsTarget,
            FixVector3 aimPoint)
        {
            return CharacterTestAttackOpportunityResult.Failure(
                CharacterTestOpportunityFailure.Unavailable(
                    CharacterTestAttackOpportunityFailureReason.TracePlanInvalid,
                    CharacterTestAttackOpportunityFailureFlags.TracePlanInvalid,
                    hasCompatibleHurtbox,
                    envelopeIntersectsTarget),
                mode,
                aimPoint);
        }

        private static CharacterTestAttackOpportunityEvidence CreateResultEvidence(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestAttackEvaluationMode mode,
            int requiredYawDeltaMilliDegrees,
            CharacterTestAttackOpportunityResult result)
        {
            bool hasPlan = HasPlan(query, mode);
            CharacterTestDirectHitTracePlanView plan = SelectPlan(query, mode);
            CharacterTestDirectHitTracePlanPoseKind expectedPoseKind =
                mode == CharacterTestAttackEvaluationMode.CurrentPose
                    ? CharacterTestDirectHitTracePlanPoseKind.CurrentPose
                    : mode == CharacterTestAttackEvaluationMode.TurnInPlace
                        ? CharacterTestDirectHitTracePlanPoseKind.TurnInPlace
                        : CharacterTestDirectHitTracePlanPoseKind.CandidatePose;
            int expectedYaw = mode == CharacterTestAttackEvaluationMode.TurnInPlace
                ? query.Snapshot.SourceYawMilliDegrees
                    + QuantizeYawDelta(query, requiredYawDeltaMilliDegrees)
                : mode == CharacterTestAttackEvaluationMode.CandidatePose
                    ? query.CandidateSourceYawMilliDegrees
                    : query.Snapshot.SourceYawMilliDegrees;
            int evaluatedYaw = hasPlan ? plan.PoseIdentity.SourceYawMilliDegrees : 0;
            bool orientationMatched = hasPlan
                && plan.PoseIdentity.Kind == expectedPoseKind
                && evaluatedYaw == NormalizeYaw(expectedYaw);

            int actionId = 0;
            int evidenceSampleIndex = result.TraceSampleIndex >= 0
                ? result.TraceSampleIndex
                : 0;
            if (hasPlan
                && plan.TryGetSample(
                    evidenceSampleIndex,
                    out CharacterTestDirectHitTracePlanSample evidenceSample))
            {
                actionId = evidenceSample.ActionId;
            }

            bool verticalOverlap = false;
            if (hasPlan
                && query.TargetGeometry != null
                && query.TargetGeometry.IsValid
                && plan.TryGetBounds(out CharacterTestDirectHitTraceBounds bounds)
                && bounds.HasBounds)
            {
                verticalOverlap = bounds.Max.Y >= query.TargetGeometry.WorldMin.Y
                    && bounds.Min.Y <= query.TargetGeometry.WorldMax.Y;
            }

            bool qualificationAccepted = result.Status == CharacterTestAttackOpportunityStatus.Valid
                && result.FailureReason != CharacterTestAttackOpportunityFailureReason.TargetNotEligible
                && result.FailureReason != CharacterTestAttackOpportunityFailureReason.TargetStateRejected;
            return new CharacterTestAttackOpportunityEvidence(
                CharacterTestAttackPredictionAssumption.StaticTarget,
                query.Snapshot.SourceEntityId,
                query.Snapshot.TargetEntityId,
                actionId,
                query.Snapshot.SimulationFrame,
                query.Snapshot.CombatPhysicsRevision,
                query.Snapshot.SourcePoseRevision,
                query.Snapshot.SourceFacingRevision,
                query.Snapshot.TargetHurtboxRevision,
                query.Snapshot.ResolvedActionRevision,
                NormalizeYaw(expectedYaw),
                evaluatedYaw,
                orientationMatched,
                verticalOverlap,
                result.HasCompatibleHurtbox,
                qualificationAccepted,
                result.BlockerBodyId,
                result.BlockerColliderId,
                result.BlockerWorldTargetKind);
        }

        private static int NormalizeYaw(int milliDegrees)
        {
            const int fullTurn = 360000;
            int normalized = milliDegrees % fullTurn;
            return normalized < 0 ? normalized + fullTurn : normalized;
        }

        private static bool HasPlan(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestAttackEvaluationMode mode)
        {
            switch (mode)
            {
                case CharacterTestAttackEvaluationMode.CurrentPose:
                    return query.HasCurrentPosePlan;
                case CharacterTestAttackEvaluationMode.TurnInPlace:
                    return query.HasTurnInPlacePlan;
                case CharacterTestAttackEvaluationMode.CandidatePose:
                    return query.HasCandidatePosePlan;
                default:
                    return false;
            }
        }

        private static CharacterTestDirectHitTracePlanView SelectPlan(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestAttackEvaluationMode mode)
        {
            switch (mode)
            {
                case CharacterTestAttackEvaluationMode.CurrentPose:
                    return query.CurrentPosePlan;
                case CharacterTestAttackEvaluationMode.TurnInPlace:
                    return query.TurnInPlacePlan;
                case CharacterTestAttackEvaluationMode.CandidatePose:
                    return query.CandidatePosePlan;
                default:
                    return default;
            }
        }

        private static int QuantizeYawDelta(
            CharacterTestAttackOpportunityQuery query,
            int requiredYawDeltaMilliDegrees)
        {
            return CharacterTestDeterministicMath.ShortestAngleDelta(
                query.Snapshot.SourceYawMilliDegrees,
                query.Snapshot.SourceYawMilliDegrees + requiredYawDeltaMilliDegrees);
        }

        private static FixVector3 GetRepresentativeSourceCapsuleMidpoint(CombatCapsuleQuery query)
        {
            return new FixVector3(
                (query.PointA.X + query.PointB.X) / Fix64.FromInt(2),
                (query.PointA.Y + query.PointB.Y) / Fix64.FromInt(2),
                (query.PointA.Z + query.PointB.Z) / Fix64.FromInt(2));
        }

        private static CombatFrame ToCombatFrame(RuntimeFrame frame, int localFrame)
        {
            long value = frame.Value + Math.Max(0, localFrame);
            if (value <= 0)
                return CombatFrame.Zero;
            if (value >= int.MaxValue)
                return new CombatFrame(int.MaxValue);
            return new CombatFrame((int)value);
        }

        private static FixVector3 GetAimPoint(CharacterTestDamageGeometrySnapshot geometry)
        {
            if (geometry == null || !geometry.IsValid)
                return FixVector3.Zero;

            return new FixVector3(
                (geometry.WorldMin.X + geometry.WorldMax.X) / Fix64.FromInt(2),
                (geometry.WorldMin.Y + geometry.WorldMax.Y) / Fix64.FromInt(2),
                (geometry.WorldMin.Z + geometry.WorldMax.Z) / Fix64.FromInt(2));
        }
    }
}

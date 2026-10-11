using System;
using MxFramework.CharacterAction;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiJointEvaluationBudgetDiagnostics
    {
        internal CharacterTestRuntimeAiJointEvaluationBudgetDiagnostics(
            int candidatePosePreviewCount,
            int pathQueryCount,
            CharacterTestRuntimeAiJointTruncationPhase truncationPhase,
            int truncationObservedCount,
            int truncationAcceptedCount,
            int truncationLimit)
        {
            CandidatePosePreviewCount = candidatePosePreviewCount;
            PathQueryCount = pathQueryCount;
            TruncationPhase = truncationPhase;
            TruncationObservedCount = truncationObservedCount;
            TruncationAcceptedCount = truncationAcceptedCount;
            TruncationLimit = truncationLimit;
        }

        internal int CandidatePosePreviewCount { get; }
        internal int PathQueryCount { get; }
        internal CharacterTestRuntimeAiJointTruncationPhase TruncationPhase { get; }
        internal int TruncationObservedCount { get; }
        internal int TruncationAcceptedCount { get; }
        internal int TruncationLimit { get; }
    }

    internal static class CharacterTestRuntimeAiJointEvaluationBudgetCounter
    {
        internal static bool TryConsume(
            ref int count,
            int limit,
            CharacterTestRuntimeAiJointTruncationPhase phase,
            int companionCount,
            out CharacterTestRuntimeAiJointEvaluationBudgetDiagnostics diagnostics)
        {
            if (count >= limit)
            {
                diagnostics = phase == CharacterTestRuntimeAiJointTruncationPhase.CandidatePose
                    ? new CharacterTestRuntimeAiJointEvaluationBudgetDiagnostics(
                        count, companionCount, phase, count + 1, count, limit)
                    : new CharacterTestRuntimeAiJointEvaluationBudgetDiagnostics(
                        companionCount, count, phase, count + 1, count, limit);
                return false;
            }
            count++;
            diagnostics = default;
            return true;
        }
    }

    /// <summary>
    /// Immutable, frame-bound trace preparation shared by every Zone leaf for
    /// one (source, target, resolved action). It is derived state and is never
    /// serialized or used as command authority.
    /// </summary>
    internal readonly struct CharacterTestRuntimeAiJointPreparedAttack
    {
        internal CharacterTestRuntimeAiJointPreparedAttack(
            CharacterActionConfig action,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            RuntimeFrame plannerFrame,
            RuntimeFrame completedFrame,
            CharacterTestCharacterPhysicsRuntime physics,
            CharacterTestAttackEvaluationSnapshot snapshot,
            CharacterTestAttackOpportunityQuery query,
            CharacterTestDamageZoneSnapshot zones,
            CharacterTestDamageZoneLock previousLock,
            int requiredYawDeltaMilliDegrees)
        {
            Action = action;
            Source = source;
            Target = target;
            PlannerFrame = plannerFrame;
            CompletedFrame = completedFrame;
            Physics = physics;
            Snapshot = snapshot;
            Query = query;
            Zones = zones;
            PreviousLock = previousLock;
            RequiredYawDeltaMilliDegrees = requiredYawDeltaMilliDegrees;
        }

        internal CharacterActionConfig Action { get; }
        internal CharacterTestCharacterEntity Source { get; }
        internal CharacterTestCharacterEntity Target { get; }
        internal RuntimeFrame PlannerFrame { get; }
        internal RuntimeFrame CompletedFrame { get; }
        internal CharacterTestCharacterPhysicsRuntime Physics { get; }
        internal CharacterTestAttackEvaluationSnapshot Snapshot { get; }
        internal CharacterTestAttackOpportunityQuery Query { get; }
        internal CharacterTestDamageZoneSnapshot Zones { get; }
        internal CharacterTestDamageZoneLock PreviousLock { get; }
        internal int RequiredYawDeltaMilliDegrees { get; }
        internal bool IsValid => Action != null
            && Source != null
            && Target != null
            && Physics != null
            && Snapshot.ResolvedActionRevision > 0;
    }

    internal sealed partial class CharacterTestRuntimeAiPlannerAttackOpportunityAdapter
    {
        internal CharacterTestEngagementReservationAuthority ReservationAuthority =>
            _reservationAuthority;

        internal long PreparedFactReuseCount { get; private set; }
        internal bool TryGetPreparedPrimaryAttackFact(CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target, RuntimeFrame frame, in CharacterTestRuntimeAiJointDirective directive,
            out bool canHit)
        {
            canHit = false;
            var prepared = directive.PreparedAttack;
            if (!directive.HasSelection || directive.IsCompatibility || !prepared.IsValid
                || !ReferenceEquals(prepared.Source, source) || !ReferenceEquals(prepared.Target, target)
                || prepared.PlannerFrame != frame) return false;
            var runtime = _runtime.ActionRuntime;
            var resolved = ResolvePrimaryAttackIntent(source, runtime, frame);
            if (!resolved.IsSuccess || resolved.Plan == null || !resolved.Plan.ActionId.Equals(prepared.Action.ActionId)
                || !ReferenceEquals(ResolveAction(prepared.Action.ActionId), prepared.Action)
                || !runtime.EvaluatePartStateEligibility(source, prepared.Action).IsEligible
                || !_runtime.TryGetDamageGeometrySnapshot(target.Id.Value, out var geometry)
                || !prepared.Snapshot.IsCurrent(prepared.CompletedFrame, prepared.Physics.World, source, target,
                    geometry, source.SourcePoseRevision, source.SourceFacingRevision, prepared.Snapshot.ResolvedActionRevision))
                return false;
            var evaluation = directive.Selection.Evaluation;
            if (evaluation.Opportunity.Status != CharacterTestAttackOpportunityStatus.Valid) return false;
            PreparedFactReuseCount++;
            canHit = evaluation.CurrentPose && evaluation.Opportunity.CanHitFromCurrentPose;
            return true;
        }

        internal bool TryGetZoneLockWitness(
            int sourceEntityId,
            out int revision,
            out int zoneId)
        {
            if (_zoneLocksBySource.TryGetValue(
                    sourceEntityId,
                    out CharacterTestDamageZoneLock value)
                && value.HasLock)
            {
                unchecked
                {
                    revision = value.BindingRevision * 397 ^ value.ZoneRevision;
                    revision = revision * 397 ^ value.HurtboxRevision;
                    revision = revision * 397 ^ value.ResolvedActionRevision;
                    revision = revision * 397 ^ (int)value.BodyPartStateRevision;
                    revision = revision * 397 ^ (int)(value.BodyPartStateRevision >> 32);
                }
                zoneId = value.ZoneId;
                return true;
            }
            revision = 0;
            zoneId = 0;
            return false;
        }

        /// <summary>Projects the same dynamic Zone state used by the independent selector.</summary>
        /// <remarks>NoAlloc.</remarks>
        internal bool TryProjectJointZoneState(
            CharacterTestCharacterEntity target,
            CharacterTestDamageZoneSnapshot zones,
            in CharacterTestDamageZoneAggregate zone,
            out CharacterTestBodyPartDamageZoneSelectionState dynamicState)
        {
            if (!zone.IsAvailable)
            {
                dynamicState = default;
                return false;
            }
            return _zoneSelector.TryProjectDynamicState(target, zones, zone, out dynamicState)
                && dynamicState.IsUsable;
        }

        internal bool TryResolvePlannerPrimaryAttackAction(
            CharacterTestCharacterEntity source,
            RuntimeFrame plannerFrame,
            out CharacterActionConfig action)
        {
            action = null;
            CharacterTestCharacterActionRuntime actionRuntime = _runtime?.ActionRuntime;
            CharacterActionConfigRegistry registry = actionRuntime?.Registry;
            if (source == null || actionRuntime == null || registry == null)
                return false;
            CharacterActionResolveResult resolve = ResolvePrimaryAttackIntent(
                source,
                actionRuntime,
                plannerFrame);
            bool resolved = resolve.IsSuccess
                && resolve.Plan != null
                && resolve.Plan.Category == CharacterActionCategory.BasicAttack
                && resolve.Plan.ActionId.IsValid
                && registry.TryGetAction(resolve.Plan.ActionId, out action)
                && action != null
                && action.Category == CharacterActionCategory.BasicAttack;
            return resolved
                && actionRuntime.EvaluatePartStateEligibility(source, action).IsEligible;
        }

        /// <summary>Stable content witnesses used only to invalidate derived joint caches.</summary>
        /// <remarks>NoAlloc.</remarks>
        internal void GetJointActionCacheWitnesses(
            CharacterTestCharacterEntity source,
            CharacterActionConfig action,
            out int actionBindingRevision,
            out int animationRevision,
            out int rootMotionRevision,
            out int tracePolicyRevision)
        {
            actionBindingRevision = 0;
            CharacterTestCharacterActionRuntime actionRuntime = _runtime?.ActionRuntime;
            if (source != null
                && actionRuntime != null
                && actionRuntime.TryGetEntityActionSetNoAlloc(
                    source.Id,
                    out CharacterActionSetConfig actionSet))
            {
                actionBindingRevision = ComputeActionSetBindingSignature(actionSet);
            }
            animationRevision = ComputeAnimationWitness(action);
            rootMotionRevision = ComputeRootMotionWitness(action);
            tracePolicyRevision = ComputeCombatTraceWitness(action);
        }

        private static int ComputeAnimationWitness(CharacterActionConfig action)
        {
            if (action == null) return 0;
            unchecked
            {
                int hash = action.ActionId.Value;
                var events = action.AnimationTrack?.Events;
                for (int i = 0; events != null && i < events.Length; i++)
                    hash = hash * 397 ^ events[i].ContentWitnessHash.GetHashCode();
                return hash;
            }
        }

        private static int ComputeRootMotionWitness(CharacterActionConfig action)
        {
            MotionTrackConfig track = action?.MotionTrack;
            if (track == null)
                return 0;
            unchecked
            {
                int hash = track.RootMotion?.ContentWitnessHash.GetHashCode() ?? 0;
                hash = hash * 397 ^ (track.VerticalRootMotion?.ContentWitnessHash.GetHashCode() ?? 0);
                for (int i = 0; i < track.Events.Length; i++)
                {
                    MotionTrackEvent evt = track.Events[i];
                    if (evt.Kind != CharacterActionTrackEventKind.SampleRootMotionY)
                        continue;
                    hash = hash * 397 ^ evt.Frame;
                    hash = hash * 397 ^ evt.EndFrame;
                    hash = hash * 397 ^ evt.StableEventId;
                }
                return hash;
            }
        }

        private static int ComputeCombatTraceWitness(CharacterActionConfig action)
        {
            if (action == null)
                return 0;
            unchecked
            {
                CombatTrackConfig track = action.CombatTrack;
                int hash = track?.CombatActionId ?? 0;
                CombatTrackEvent[] events = track?.Events;
                for (int i = 0; events != null && i < events.Length; i++)
                {
                    CombatTrackEvent value = events[i];
                    hash = hash * 397 ^ value.Frame;
                    hash = hash * 397 ^ value.EndFrame;
                    hash = hash * 397 ^ (int)value.Kind;
                    hash = hash * 397 ^ value.CombatActionId;
                    hash = hash * 397 ^ value.TraceProfileId;
                    hash = hash * 397 ^ value.StableEventId;
                    hash = hash * 397 ^ value.EquipmentSlotId;
                    hash = hash * 397 ^ value.HitVolume.GetHashCode();
                }
                return hash;
            }
        }

        /// <summary>Builds the real Current/Turn trace bundle once per target/action.</summary>
        /// <remarks>NoAlloc after warm-up; read-only with respect to decision authority.</remarks>
        internal bool TryPrepareJointPrimaryAttack(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterActionId requestedActionId,
            RuntimeFrame plannerFrame,
            out CharacterTestRuntimeAiJointPreparedAttack prepared,
            out CharacterTestAttackOpportunityFailureReason failureReason)
        {
            prepared = default;
            failureReason = CharacterTestAttackOpportunityFailureReason.SnapshotUnavailable;
            if (source == null || target == null || _runtime == null
                || !_runtime.TryGetLastCompletedRuntimeFrame(out RuntimeFrame completedFrame))
            {
                return false;
            }

            CharacterTestCharacterPhysicsRuntime physics = _runtime.PhysicsRuntime;
            CombatPhysicsWorld world = physics?.World;
            CharacterTestCharacterCombatTraceSystem combat = _runtime.CombatTraceSystem;
            if (physics == null || world == null || combat == null)
                return false;
            if (!combat.WorldTargetClassifications.IsCurrent(completedFrame, world.Revision))
            {
                failureReason = CharacterTestAttackOpportunityFailureReason.StaleSnapshot;
                return false;
            }
            if (!_runtime.TryGetDamageGeometrySnapshot(
                    target.Id.Value,
                    out CharacterTestDamageGeometrySnapshot geometry)
                || geometry == null
                || !geometry.IsValid)
            {
                return false;
            }
            if (geometry.Frame != completedFrame || geometry.WorldRevision != world.Revision)
            {
                failureReason = CharacterTestAttackOpportunityFailureReason.StaleSnapshot;
                return false;
            }

            CharacterActionConfig action = ResolveAction(requestedActionId);
            if (action == null || action.Category != CharacterActionCategory.BasicAttack)
            {
                failureReason = CharacterTestAttackOpportunityFailureReason.TraceEvaluatorUnsupported;
                return false;
            }
            CharacterTestActionPartStateEligibility partStateEligibility =
                _runtime.ActionRuntime.EvaluatePartStateEligibility(source, action);
            if (!partStateEligibility.IsEligible)
            {
                failureReason = CharacterTestAttackOpportunityFailureReason.PartStateDependencyRejected;
                return false;
            }
            int actionRevision = action.ActionId.Value;
            if (actionRevision <= 0
                || !CharacterTestAttackEvaluationSnapshot.TryCapture(
                    completedFrame,
                    world,
                    source,
                    target,
                    geometry,
                    actionRevision,
                    out CharacterTestAttackEvaluationSnapshot snapshot))
            {
                return false;
            }

            _lookup.Configure(_runtime.World);
            FixVector3 targetCenter =
                (geometry.WorldMin + geometry.WorldMax) / Fix64.FromInt(2);
            int targetBearing = CharacterTestDeterministicMath.Atan2MilliDegrees(
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                    targetCenter.X - snapshot.SourcePosition.X),
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                    targetCenter.Z - snapshot.SourcePosition.Z));
            int requiredYawDelta = CharacterTestDeterministicMath.ShortestAngleDelta(
                snapshot.SourceYawMilliDegrees,
                targetBearing);
            if (!TryBuildPreviewPlans(
                    action,
                    source,
                    snapshot,
                    physics,
                    completedFrame,
                    actionRevision,
                    requiredYawDelta,
                    out CharacterTestDirectHitTracePlanView currentPlan,
                    out CharacterTestDirectHitTracePlanView turnPlan,
                    out CharacterTestAttackOpportunitySamplePolicySnapshot policies))
            {
                failureReason = CharacterTestAttackOpportunityFailureReason.TracePlanUnavailable;
                return false;
            }

            var query = new CharacterTestAttackOpportunityQuery(
                snapshot,
                geometry,
                world,
                source,
                target,
                currentPlan,
                hasCurrentPosePlan: true,
                turnPlan,
                hasTurnInPlacePlan: true,
                completedFrame,
                world.Revision,
                snapshot.SourcePoseRevision,
                snapshot.SourceFacingRevision,
                snapshot.TargetHurtboxRevision,
                snapshot.ResolvedActionRevision,
                combat.WorldTargetClassifications,
                policies,
                traceEvaluatorSupported: true, turnPlanProvider: this, turnPlanGeneration: _preparationGeneration);
            if (!_runtime.TryGetDamageZoneSnapshot(
                    target.Id.Value,
                    out CharacterTestDamageZoneSnapshot zones)
                || zones == null
                || !zones.IsValid)
            {
                // Legacy geometry remains evaluator-backed through one synthetic
                // empty Zone leaf; an invalid snapshot must not masquerade as authored data.
                zones = null;
            }
            _zoneLocksBySource.TryGetValue(source.Id.Value, out CharacterTestDamageZoneLock previousLock);
            prepared = new CharacterTestRuntimeAiJointPreparedAttack(
                action,
                source,
                target,
                plannerFrame,
                completedFrame,
                physics,
                snapshot,
                query,
                zones,
                previousLock,
                requiredYawDelta);
            failureReason = CharacterTestAttackOpportunityFailureReason.None;
            return true;
        }

        /// <summary>Evaluates one exact Zone leaf without writing locks, reservations or evidence.</summary>
        /// <remarks>NoAlloc after warm-up.</remarks>
        internal bool TryEvaluatePreparedJointPrimaryAttack(
            in CharacterTestRuntimeAiJointPreparedAttack prepared,
            int zoneId,
            out CharacterTestRuntimeAiJointEvaluation evaluation,
            out CharacterTestEngagementCandidateResult engagement,
            out CharacterTestEngagementCandidateStatus engagementStatus)
        {
            return TryEvaluatePreparedJointPrimaryAttack(
                prepared,
                zoneId,
                CharacterTestRuntimeAiJointGenerationBudget.HardCandidatePosePreviewLimit,
                CharacterTestRuntimeAiJointGenerationBudget.HardPathQueryLimit,
                out evaluation,
                out engagement,
                out engagementStatus,
                out _);
        }

        internal bool TryEvaluatePreparedJointPrimaryAttack(
            in CharacterTestRuntimeAiJointPreparedAttack prepared,
            int zoneId,
            int candidatePosePreviewLimit,
            int pathQueryLimit,
            out CharacterTestRuntimeAiJointEvaluation evaluation,
            out CharacterTestEngagementCandidateResult engagement,
            out CharacterTestEngagementCandidateStatus engagementStatus,
            out CharacterTestRuntimeAiJointEvaluationBudgetDiagnostics budgetDiagnostics)
        {
            evaluation = default;
            engagement = default;
            engagementStatus = CharacterTestEngagementCandidateStatus.None;
            budgetDiagnostics = default;
            if (!prepared.IsValid)
                return false;

            CharacterTestAttackOpportunityResult opportunity;
            CharacterTestAttackOpportunityQuery effectiveQuery = prepared.Query;
            CharacterTestDamageZoneLock effectiveLock = prepared.PreviousLock;
            if (prepared.Zones != null)
            {
                CharacterTestDamageZoneLock exactLock = prepared.PreviousLock;
                bool sameActor = exactLock.HasLock
                    && exactLock.TargetGameplayEntityId.Equals(
                        prepared.Target.GameplayEntityId);
                if (!sameActor)
                {
                    if (_runtime?.PerceptionFrameCache == null
                        || !_runtime.PerceptionFrameCache.TryAcquireNearestHurtbox(
                            prepared.PlannerFrame,
                            prepared.Source,
                            prepared.Target,
                            out CharacterTestCombatTargetHandle acquired,
                            out _))
                    {
                        return true;
                    }
                    exactLock = CreateInitialLockedHurtboxLock(
                        acquired,
                        prepared.Snapshot,
                        prepared.Zones,
                        prepared.Target);
                }
                if (!(
                        zoneId == exactLock.ZoneId))
                {
                    return true;
                }

                CharacterTestDamageZoneSelectionResult zone =
                    _zoneSelector.EvaluateLockedHurtbox(
                        prepared.Query,
                        prepared.Zones,
                        exactLock,
                        prepared.RequiredYawDeltaMilliDegrees);
                if (!zone.IsUsable)
                    return true;
                opportunity = zone.Opportunity;
                effectiveLock = exactLock;
                effectiveQuery = prepared.Query.WithDamageZoneFilter(
                    prepared.Zones,
                    exactLock.ZoneId,
                    exactLock.LogicalHurtboxId);
            }
            else
            {
                if (!(zoneId <= 0))
                    return true;
                CharacterTestDamageZoneLock exactLock = prepared.PreviousLock;
                bool sameActor = exactLock.HasLock
                    && exactLock.TargetGameplayEntityId.Equals(
                        prepared.Target.GameplayEntityId);
                if (!sameActor)
                {
                    if (_runtime?.PerceptionFrameCache == null
                        || !_runtime.PerceptionFrameCache.TryAcquireNearestHurtbox(
                            prepared.PlannerFrame,
                            prepared.Source,
                            prepared.Target,
                            out CharacterTestCombatTargetHandle acquired,
                            out _))
                    {
                        return true;
                    }
                    exactLock = CreateInitialLockedHurtboxLock(
                        acquired,
                        prepared.Snapshot,
                        null,
                        prepared.Target);
                }
                effectiveLock = exactLock;
                effectiveQuery = prepared.Query.WithDamageZoneFilter(
                    null,
                    0,
                    exactLock.LogicalHurtboxId);
                opportunity = _service.Evaluate(
                    effectiveQuery,
                    prepared.RequiredYawDeltaMilliDegrees);
            }

            bool current = opportunity.Status == CharacterTestAttackOpportunityStatus.Valid
                && opportunity.Level == CharacterTestAttackOpportunityLevel.CurrentPose
                && opportunity.CanHitFromCurrentPose;
            bool turn = opportunity.Status == CharacterTestAttackOpportunityStatus.Valid
                && opportunity.Level == CharacterTestAttackOpportunityLevel.TurnInPlace
                && opportunity.CanHitAfterTurnInPlace;
            bool approach = false;
            int pathCost = int.MaxValue;
            int anchorId = 0;
            if (opportunity.Status == CharacterTestAttackOpportunityStatus.Valid
                && opportunity.RequiresTranslation
                && prepared.Zones != null
                && TryEvaluateEngagementAnchors(
                    prepared.Action,
                    prepared.Source,
                    prepared.Target,
                    prepared.PlannerFrame,
                    prepared.CompletedFrame,
                    prepared.Physics,
                    prepared.Snapshot,
                    effectiveQuery,
                    prepared.Zones,
                    effectiveLock,
                    out engagement,
                    out engagementStatus,
                    out budgetDiagnostics,
                    candidatePosePreviewLimit,
                    pathQueryLimit,
                    zoneId)
                && _reservationAuthority.CanAcquire(
                    prepared.Source.Id.Value,
                    prepared.Target.Id.Value,
                    prepared.Snapshot.ResolvedActionRevision,
                    prepared.PlannerFrame,
                    engagement))
            {
                opportunity = engagement.Opportunity;
                approach = true;
                long totalCost = engagement.Path.TotalCostMillimeters;
                pathCost = totalCost >= int.MaxValue
                    ? int.MaxValue
                    : totalCost <= 0L ? 0 : (int)totalCost;
                anchorId = engagement.Pose.AnchorId;
            }

            evaluation = new CharacterTestRuntimeAiJointEvaluation(
                CharacterTestRuntimeAiJointEvaluationStatus.EvaluatorValid,
                current,
                turn,
                approach,
                pathCost,
                opportunity.EarliestCandidateHitTick,
                anchorId,
                opportunity);
            return true;
        }

        /// <summary>
        /// Re-evaluates the selected prepared leaf and commits only the existing
        /// Zone-lock/reservation/evidence authority immediately before command submission.
        /// </summary>
        internal bool TryCommitPreparedJointPrimaryAttack(
            in CharacterTestRuntimeAiJointPreparedAttack prepared,
            int zoneId,
            out CharacterTestAttackOpportunityResult result)
        {
            result = default;
            CharacterTestCharacterActionRuntime actionRuntime = _runtime?.ActionRuntime;
            if (actionRuntime == null
                || !actionRuntime.EvaluatePartStateEligibility(
                    prepared.Source,
                    prepared.Action).IsEligible)
            {
                return FailClosed(
                    prepared.Source,
                    prepared.Target,
                    prepared.PlannerFrame,
                    CharacterTestAttackOpportunityFailureReason.PartStateDependencyRejected,
                    out result);
            }
            if (!TryEvaluatePreparedJointPrimaryAttack(
                    prepared,
                    zoneId,
                    out CharacterTestRuntimeAiJointEvaluation evaluation,
                    out CharacterTestEngagementCandidateResult engagement,
                    out CharacterTestEngagementCandidateStatus engagementStatus)
                || !evaluation.IsSelectable)
            {
                return FailClosed(
                    prepared.Source,
                    prepared.Target,
                    prepared.PlannerFrame,
                    CharacterTestAttackOpportunityFailureReason.NoCompatibleHurtbox,
                    out result);
            }

            result = evaluation.Opportunity;
            if (evaluation.ApproachPose)
            {
                if (!_reservationAuthority.TryAcquire(
                        prepared.Source.Id.Value,
                        prepared.Target.Id.Value,
                        prepared.Snapshot.ResolvedActionRevision,
                        prepared.PlannerFrame,
                        engagement,
                        out _))
                {
                    return FailClosed(
                        prepared.Source,
                        prepared.Target,
                        prepared.PlannerFrame,
                        CharacterTestAttackOpportunityFailureReason.NoCompatibleHurtbox,
                        out result);
                }
                _engagementSelectionsBySource[prepared.Source.Id.Value] =
                    new CharacterTestEngagementSelectionState(prepared.PlannerFrame, engagement);
                _engagementDiagnosticsBySource[prepared.Source.Id.Value] =
                    new CharacterTestEngagementDiagnosticState(
                        prepared.Source.Id.Value,
                        prepared.Target.Id.Value,
                        prepared.Snapshot.ResolvedActionRevision,
                        prepared.PlannerFrame,
                        CharacterTestEngagementCandidateStatus.Success,
                        engagement);
                _zoneLocksBySource[prepared.Source.Id.Value] = CreateEngagementZoneLock(
                    prepared.Target,
                    prepared.Snapshot,
                    prepared.Zones,
                    engagement,
                    prepared.PlannerFrame);
            }
            else
            {
                _engagementSelectionsBySource.Remove(prepared.Source.Id.Value);
                _engagementDiagnosticsBySource.Remove(prepared.Source.Id.Value);
                _reservationAuthority.Release(
                    prepared.Source.Id.Value,
                    CharacterTestEngagementReservationReleaseReason.NotRenewed);
                if (prepared.Zones != null)
                {
                    CharacterTestDamageZoneLock exactLock = prepared.PreviousLock;
                    if (!exactLock.HasLock
                        || !exactLock.TargetGameplayEntityId.Equals(
                            prepared.Target.GameplayEntityId))
                    {
                        if (_runtime?.PerceptionFrameCache == null
                            || !_runtime.PerceptionFrameCache.TryAcquireNearestHurtbox(
                                prepared.PlannerFrame,
                                prepared.Source,
                                prepared.Target,
                                out CharacterTestCombatTargetHandle acquired,
                                out _))
                        {
                            return false;
                        }
                        exactLock = CreateInitialLockedHurtboxLock(
                            acquired,
                            prepared.Snapshot,
                            prepared.Zones,
                            prepared.Target);
                    }
                    CharacterTestDamageZoneSelectionResult zone =
                        _zoneSelector.EvaluateLockedHurtbox(
                            prepared.Query,
                            prepared.Zones,
                            exactLock,
                            prepared.RequiredYawDeltaMilliDegrees);
                    _zoneLocksBySource[prepared.Source.Id.Value] = zone.IsUsable
                        ? zone.ZoneLock
                        : exactLock;
                }
                else
                {
                    CharacterTestDamageZoneLock exactLock = prepared.PreviousLock;
                    if (!exactLock.HasLock
                        || !exactLock.TargetGameplayEntityId.Equals(
                            prepared.Target.GameplayEntityId))
                    {
                        if (_runtime?.PerceptionFrameCache == null
                            || !_runtime.PerceptionFrameCache.TryAcquireNearestHurtbox(
                                prepared.PlannerFrame,
                                prepared.Source,
                                prepared.Target,
                                out CharacterTestCombatTargetHandle acquired,
                                out _))
                        {
                            return false;
                        }
                        exactLock = CreateInitialLockedHurtboxLock(
                            acquired,
                            prepared.Snapshot,
                            null,
                            prepared.Target);
                    }
                    _zoneLocksBySource[prepared.Source.Id.Value] = exactLock;
                }
            }

            SetEvidence(
                prepared.Source.Id.Value,
                prepared.Target.Id.Value,
                prepared.PlannerFrame,
                CharacterTestRuntimeAiPlannerAttackDecisionSource.AttackOpportunity,
                result.Status,
                result.Level,
                result.FailureReason,
                result.RequiredYawDeltaMilliDegrees);
            CacheResult(
                prepared.Source,
                prepared.Target,
                prepared.PlannerFrame,
                result);
            return result.Status == CharacterTestAttackOpportunityStatus.Valid;
        }
    }
}

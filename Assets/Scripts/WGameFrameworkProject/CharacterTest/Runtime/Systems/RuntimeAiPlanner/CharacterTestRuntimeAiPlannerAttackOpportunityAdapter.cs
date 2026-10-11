using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiPlannerAttackDecisionSource
    {
        None = 0,
        AttackOpportunity = 1,
        CompatibilityLegacyRange = 2
    }

    /// <summary>
    /// CharacterTest-only composition adapter for the CD-M1 service. It owns
    /// plan construction and the planner-to-action cache, while the M1 core
    /// remains independent of Runtime AI and never reads the actual trace
    /// plan produced by Simulation.
    /// </summary>
    /// <remarks>
    /// All collections are allocated at construction/warm-up. The steady-state
    /// guard path is a dictionary lookup over the result produced in
    /// PreSimulation and performs no world or plan allocation.
    /// </remarks>
    internal sealed partial class CharacterTestRuntimeAiPlannerAttackOpportunityAdapter : ICharacterTestAttackTurnPlanProvider
    {
        private const int PreviewMaxSubsteps = 8;
        private static readonly Fix64 PreviewMaxTipDistancePerSubstep = Fix64.FromRatio(20, 100);

        private readonly CharacterTestCharacterRuntimeModule _runtime;
        private readonly IReadOnlyList<CharacterTestDirectHitTraceSourcePoseOffset> _nominalSourcePoseOffsets;
        private readonly CharacterTestNavigationSession _navigationSession;
        private readonly CharacterTestEngagementPlanner _engagementPlanner =
            new CharacterTestEngagementPlanner();
        private readonly CharacterTestEngagementReservationAuthority _reservationAuthority =
            new CharacterTestEngagementReservationAuthority();
        private readonly CharacterTestAttackOpportunityService _service =
            new CharacterTestAttackOpportunityService();
        private readonly CharacterTestDamageZoneSelector _zoneSelector;
        private readonly CharacterTestCombatEntityLookup _lookup =
            new CharacterTestCombatEntityLookup();
        private readonly CharacterTestDirectHitTracePlanBuffer _currentPlanBuffer =
            new CharacterTestDirectHitTracePlanBuffer();
        private readonly CharacterTestDirectHitTracePlanBuffer _turnPlanBuffer =
            new CharacterTestDirectHitTracePlanBuffer();
        private readonly CharacterTestDirectHitTracePlanBuffer _candidatePlanBuffer =
            new CharacterTestDirectHitTracePlanBuffer();
        private readonly List<CharacterTestDirectHitTraceSourcePoseOffset> _poseOffsets =
            new List<CharacterTestDirectHitTraceSourcePoseOffset>(96);
        private readonly List<CharacterTestDirectHitTraceSourcePoseOffset> _turnPoseOffsets =
            new List<CharacterTestDirectHitTraceSourcePoseOffset>(96);
        private readonly List<CharacterTestDirectHitTraceSourcePoseOffset> _candidatePoseOffsets =
            new List<CharacterTestDirectHitTraceSourcePoseOffset>(96);
        private readonly List<int> _activePreviewFrames = new List<int>(241);
        private readonly List<int> _candidateActiveFrames = new List<int>(241);
        private readonly List<CharacterTestDirectHitTracePreviewWeaponFrame> _weaponFrames =
            new List<CharacterTestDirectHitTracePreviewWeaponFrame>(96);
        private readonly List<CharacterTestDirectHitTracePreviewWeaponFrame> _turnWeaponFrames =
            new List<CharacterTestDirectHitTracePreviewWeaponFrame>(96);
        private readonly List<CharacterTestDirectHitTracePreviewWeaponFrame> _candidateWeaponFrames =
            new List<CharacterTestDirectHitTracePreviewWeaponFrame>(96);
        private readonly List<CharacterTestDirectHitTracePreviewCapsuleFrame> _capsuleFrames =
            new List<CharacterTestDirectHitTracePreviewCapsuleFrame>(96);
        private readonly List<CharacterTestDirectHitTracePreviewCapsuleFrame> _turnCapsuleFrames =
            new List<CharacterTestDirectHitTracePreviewCapsuleFrame>(96);
        private readonly List<CharacterTestDirectHitTracePreviewCapsuleFrame> _candidateCapsuleFrames =
            new List<CharacterTestDirectHitTracePreviewCapsuleFrame>(96);
        private readonly Dictionary<int, FixVector3> _previewSocketOffsets = new Dictionary<int, FixVector3>();
        private readonly Dictionary<int, int> _policyEventHeads = new Dictionary<int, int>();
        private readonly List<PreparedPolicyEvent> _preparedPolicyEvents = new List<PreparedPolicyEvent>();
        internal long PolicyEventPreparationCount { get; private set; }
        internal long PreviewPlanPreparationCount { get; private set; }
        internal long SocketOffsetReadCount { get; private set; }
        internal void MaintainDecisionAuthority(RuntimeFrame frame)
            => _reservationAuthority.SweepInvalid(_runtime, _navigationSession, frame);
        internal long TurnPlanPreparationCount { get; private set; }
        private long _preparationGeneration;
        private PendingTurnPlan _pendingTurn;
        private CharacterTestDirectHitTracePlanView _preparedTurnPlan;
        private bool _turnPlanAttempted;
        private readonly struct PendingTurnPlan
        {
            internal readonly CharacterActionConfig Action;
            internal readonly CharacterTestCharacterEntity Source;
            internal readonly CharacterTestAttackEvaluationSnapshot Snapshot;
            internal readonly CharacterTestCharacterPhysicsRuntime Physics;
            internal readonly RuntimeFrame Frame;
            internal readonly int Revision, YawDelta, First, Last;
            internal readonly long CurrentSeal;
            internal PendingTurnPlan(CharacterActionConfig action, CharacterTestCharacterEntity source,
                CharacterTestAttackEvaluationSnapshot snapshot, CharacterTestCharacterPhysicsRuntime physics,
                RuntimeFrame frame, int revision, int yawDelta, int first, int last, long currentSeal)
            { Action = action; Source = source; Snapshot = snapshot; Physics = physics; Frame = frame;
              Revision = revision; YawDelta = yawDelta; First = first; Last = last; CurrentSeal = currentSeal; }
        }

        private readonly List<CombatCapsuleQuery> _queryBuffer =
            new List<CombatCapsuleQuery>(192);
        private readonly Dictionary<int, CachedDecision> _cachedDecisions =
            new Dictionary<int, CachedDecision>(32);
        private readonly Dictionary<int, CachedIntentResolution> _resolvedIntentCache =
            new Dictionary<int, CachedIntentResolution>(32);
        private readonly Dictionary<PolicyCacheKey, CharacterTestAttackOpportunitySamplePolicySnapshot> _policyCache =
            new Dictionary<PolicyCacheKey, CharacterTestAttackOpportunitySamplePolicySnapshot>(16);
        private readonly Dictionary<int, CharacterTestAttackOpportunityDecisionEvidence> _evidenceBySource =
            new Dictionary<int, CharacterTestAttackOpportunityDecisionEvidence>(32);
        private readonly Dictionary<int, CharacterTestDamageZoneLock> _zoneLocksBySource =
            new Dictionary<int, CharacterTestDamageZoneLock>(32);
        private readonly Dictionary<int, CharacterTestEngagementSelectionState> _engagementSelectionsBySource =
            new Dictionary<int, CharacterTestEngagementSelectionState>(32);
        private readonly Dictionary<int, CharacterTestEngagementDiagnosticState> _engagementDiagnosticsBySource =
            new Dictionary<int, CharacterTestEngagementDiagnosticState>(32);
        private RuntimeFrame _policyAuthorityFrame;
        private int _policyAuthorityWorldRevision;
        private int _policyAuthorityActionRevision;
        private bool _hasPolicyAuthority;

        private readonly CharacterTestAttackOpportunityDecisionEvidence _lastEvidence =
            new CharacterTestAttackOpportunityDecisionEvidence();

        internal CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(
            CharacterTestCharacterRuntimeModule runtime,
            CharacterTestNavigationSession navigationSession = null,
            IReadOnlyList<CharacterTestDirectHitTraceSourcePoseOffset> nominalSourcePoseOffsets = null)
        {
            _runtime = runtime;
            _nominalSourcePoseOffsets = nominalSourcePoseOffsets;
            _navigationSession = navigationSession;
            _zoneSelector = new CharacterTestDamageZoneSelector(_service);
        }

        internal CharacterTestAttackOpportunityDecisionEvidence LastEvidence => _lastEvidence;

        /// <summary>Read-only projection for Game View/Runtime AI Lab diagnostics.</summary>
        internal bool TryGetDamageZoneLock(
            int sourceEntityId,
            out CharacterTestDamageZoneLock zoneLock)
        {
            return _zoneLocksBySource.TryGetValue(sourceEntityId, out zoneLock)
                && zoneLock.HasLock;
        }

        /// <summary>
        /// Resolves the actor's persistent Hurtbox target without evaluating or
        /// changing the current action. A same-actor call never scans alternatives.
        /// </summary>
        internal bool TryResolveCombatTarget(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            RuntimeFrame evaluationFrame,
            out CharacterTestCombatTargetHandle handle,
            out CharacterTestLockedHurtboxDistanceSample distance)
        {
            handle = default;
            distance = default;
            if (source == null || target == null || _runtime?.PerceptionFrameCache == null)
                return false;

            if (_zoneLocksBySource.TryGetValue(
                    source.Id.Value,
                    out CharacterTestDamageZoneLock existing)
                && existing.HasLock
                && existing.TargetGameplayEntityId.Equals(target.GameplayEntityId))
            {
                handle = existing.ToCombatTargetHandle(source.Id.Value);
                return _runtime.PerceptionFrameCache.TryGetLockedHurtboxDistance(
                    evaluationFrame,
                    handle,
                    out distance);
            }

            if (!_runtime.PerceptionFrameCache.TryAcquireNearestHurtbox(
                    evaluationFrame,
                    source,
                    target,
                    out handle,
                    out distance))
            {
                _zoneLocksBySource.Remove(source.Id.Value);
                handle = default;
                distance = default;
                return false;
            }

            _runtime.TryGetDamageZoneSnapshot(
                target.Id.Value,
                out CharacterTestDamageZoneSnapshot zones);
            _runtime.TryGetDamageGeometrySnapshot(
                target.Id.Value,
                out CharacterTestDamageGeometrySnapshot geometry);

            _zoneLocksBySource[source.Id.Value] = new CharacterTestDamageZoneLock(
                handle.TargetGameplayEntityId,
                handle.TargetCombatEntityId,
                1,
                handle.DamageZoneId,
                handle.HitZoneId,
                handle.LogicalHurtboxId,
                CharacterTestAttackOpportunityLevel.NoOpportunity,
                -1,
                zones?.BindingRevision ?? handle.BindingRevisionAtSelection,
                zones?.HurtboxRevision ?? distance.TargetHurtboxRevision,
                zones?.WorldRevision ?? geometry?.WorldRevision ?? 0,
                zones?.ZoneRevision ?? 0,
                handle.SelectedFrame,
                CharacterTestDamageZoneSwitchReason.InitialSelection,
                target.BodyPartState?.Revision ?? 0L,
                handle.SourceEntityId,
                handle.TargetEntityId);
            return true;
        }

        /// <summary>NoAlloc: returns the selected CandidatePose/path witness for this planner frame.</summary>
        internal bool TryGetEngagementSelection(
            int sourceEntityId,
            RuntimeFrame plannerFrame,
            out CharacterTestEngagementCandidateResult selection)
        {
            if (_engagementSelectionsBySource.TryGetValue(
                    sourceEntityId,
                    out CharacterTestEngagementSelectionState state)
                && state.PlannerFrame == plannerFrame
                && state.Selection.IsSelectable
                && _reservationAuthority.TryGetForSource(
                    sourceEntityId,
                    out CharacterTestEngagementReservation reservation)
                && (
                    reservation.AnchorId == state.Selection.Pose.AnchorId))
            {
                selection = state.Selection;
                return true;
            }
            selection = default;
            return false;
        }

        /// <summary>NoAlloc: projects the selected PF-M4 next waypoint to a world-space move.</summary>
        internal bool TryGetEngagementWorldMove(
            CharacterTestCharacterEntity source,
            RuntimeFrame plannerFrame,
            out UnityEngine.Vector2 worldMove)
        {
            worldMove = UnityEngine.Vector2.zero;
            if (source == null
                || !TryGetEngagementSelection(
                    source.Id.Value,
                    plannerFrame,
                    out CharacterTestEngagementCandidateResult selection))
            {
                return false;
            }
            FixVector3 waypoint = selection.Path.HasNextWaypoint
                ? selection.Path.NextWaypoint
                : selection.Pose.WorldPosition;
            int deltaX = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                waypoint.X) - source.Transform.XMillimeters;
            int deltaZ = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                waypoint.Z) - source.Transform.ZMillimeters;
            if (deltaX == 0 && deltaZ == 0)
                return true;
            worldMove = new UnityEngine.Vector2(deltaX, deltaZ).normalized;
            return true;
        }

        internal int ZoneLockCount => _zoneLocksBySource.Count;
        internal int EngagementReservationCount => _reservationAuthority.Count;

        internal void CaptureZoneLocksInto(CharacterTestDamageZoneLockStateSnapshot[] result, int[] sourceIds)
        {
            _zoneLocksBySource.Keys.CopyTo(sourceIds, 0);
            for (int i = 1; i < result.Length; i++)
            {
                int value = sourceIds[i]; int j = i - 1;
                while (j >= 0 && sourceIds[j] > value) { sourceIds[j + 1] = sourceIds[j]; j--; }
                sourceIds[j + 1] = value;
            }
            for (int i = 0; i < result.Length; i++)
            {
                int sourceId = sourceIds[i];
                CharacterTestDamageZoneLock value = _zoneLocksBySource[sourceId];
                result[i].SourceEntityId = sourceId;
                result[i].TargetGameplayEntityIndex = value.TargetGameplayEntityId.Index;
                result[i].TargetGameplayEntityGeneration = value.TargetGameplayEntityId.Generation;
                result[i].TargetCombatEntityId = value.TargetCombatEntityId.Value;
                result[i].ResolvedActionRevision = value.ResolvedActionRevision;
                result[i].ZoneId = value.ZoneId;
                result[i].HitZoneId = value.HitZoneId;
                result[i].LogicalHurtboxId = value.LogicalHurtboxId;
                result[i].OpportunityLevel = (int)value.OpportunityLevel;
                result[i].EarliestHitTick = value.EarliestHitTick;
                result[i].BindingRevision = value.BindingRevision;
                result[i].HurtboxRevision = value.HurtboxRevision;
                result[i].WorldRevision = value.WorldRevision;
                result[i].ZoneRevision = value.ZoneRevision;
                result[i].BodyPartStateRevision = value.BodyPartStateRevision;
                result[i].SelectedFrame = value.SelectedFrame.Value;
                result[i].SwitchReason = (int)value.SwitchReason;
            }
        }

        internal void CaptureEngagementReservationsInto(CharacterTestEngagementReservationStateSnapshot[] result)
            => _reservationAuthority.CaptureInto(result);

        internal CharacterTestDamageZoneLockStateSnapshot[] CaptureZoneLocks()
        {
            if (_zoneLocksBySource.Count == 0)
                return Array.Empty<CharacterTestDamageZoneLockStateSnapshot>();
            int[] sourceIds = new int[_zoneLocksBySource.Count];
            _zoneLocksBySource.Keys.CopyTo(sourceIds, 0);
            Array.Sort(sourceIds);
            var result = new CharacterTestDamageZoneLockStateSnapshot[sourceIds.Length];
            for (int i = 0; i < sourceIds.Length; i++)
            {
                int sourceId = sourceIds[i];
                CharacterTestDamageZoneLock value = _zoneLocksBySource[sourceId];
                result[i] = new CharacterTestDamageZoneLockStateSnapshot
                {
                    SourceEntityId = sourceId,
                    TargetGameplayEntityIndex = value.TargetGameplayEntityId.Index,
                    TargetGameplayEntityGeneration = value.TargetGameplayEntityId.Generation,
                    TargetCombatEntityId = value.TargetCombatEntityId.Value,
                    ResolvedActionRevision = value.ResolvedActionRevision,
                    ZoneId = value.ZoneId,
                    HitZoneId = value.HitZoneId,
                    LogicalHurtboxId = value.LogicalHurtboxId,
                    OpportunityLevel = (int)value.OpportunityLevel,
                    EarliestHitTick = value.EarliestHitTick,
                    BindingRevision = value.BindingRevision,
                    HurtboxRevision = value.HurtboxRevision,
                    WorldRevision = value.WorldRevision,
                    ZoneRevision = value.ZoneRevision,
                    BodyPartStateRevision = value.BodyPartStateRevision,
                    SelectedFrame = value.SelectedFrame.Value,
                    SwitchReason = (int)value.SwitchReason
                };
            }
            return result;
        }

        internal CharacterTestEngagementReservationStateSnapshot[] CaptureEngagementReservations()
        {
            return _reservationAuthority.Capture();
        }

        /// <remarks>AllocByDesign: Game View/editor diagnostics only; never called by simulation.</remarks>
        internal CharacterTestEngagementDiagnosticSnapshot[] CaptureEngagementDiagnostics()
        {
            if (_engagementDiagnosticsBySource.Count == 0)
                return Array.Empty<CharacterTestEngagementDiagnosticSnapshot>();
            int[] sourceIds = new int[_engagementDiagnosticsBySource.Count];
            _engagementDiagnosticsBySource.Keys.CopyTo(sourceIds, 0);
            Array.Sort(sourceIds);
            var result = new CharacterTestEngagementDiagnosticSnapshot[sourceIds.Length];
            for (int i = 0; i < sourceIds.Length; i++)
            {
                CharacterTestEngagementDiagnosticState state =
                    _engagementDiagnosticsBySource[sourceIds[i]];
                CharacterTestEngagementCandidateResult selection = state.Selection;
                bool hasReservation = _reservationAuthority.TryGetForSource(
                    state.SourceEntityId,
                    out CharacterTestEngagementReservation reservation);
                result[i] = CharacterTestEngagementDiagnosticSnapshot.From(
                    state,
                    hasReservation ? reservation : default);
            }
            return result;
        }

        internal bool TryRestoreZoneLocks(
            CharacterTestDamageZoneLockStateSnapshot[] snapshots,
            out string failure)
        {
            failure = string.Empty;
            _zoneLocksBySource.Clear();
            snapshots ??= Array.Empty<CharacterTestDamageZoneLockStateSnapshot>();
            for (int i = 0; i < snapshots.Length; i++)
            {
                CharacterTestDamageZoneLockStateSnapshot value = snapshots[i];
                if (value == null
                    || value.SourceEntityId <= 0
                    || value.TargetGameplayEntityIndex <= 0
                    || value.TargetGameplayEntityGeneration <= 0
                    || value.TargetCombatEntityId <= 0
                    || value.ResolvedActionRevision <= 0
                    || value.BodyPartStateRevision < 0L
                    || (value.LogicalHurtboxId <= 0)
                    || !Enum.IsDefined(typeof(CharacterTestAttackOpportunityLevel), value.OpportunityLevel)
                    || !Enum.IsDefined(typeof(CharacterTestDamageZoneSwitchReason), value.SwitchReason)
                    || _zoneLocksBySource.ContainsKey(value.SourceEntityId))
                {
                    _zoneLocksBySource.Clear();
                    failure = "Runtime AI Planner DamageZone lock snapshot is invalid.";
                    return false;
                }
                _zoneLocksBySource.Add(value.SourceEntityId, new CharacterTestDamageZoneLock(
                    new GameplayEntityId(
                        value.TargetGameplayEntityIndex,
                        value.TargetGameplayEntityGeneration),
                    new CombatEntityId(value.TargetCombatEntityId),
                    value.ResolvedActionRevision,
                    value.ZoneId,
                    value.HitZoneId,
                    value.LogicalHurtboxId,
                    (CharacterTestAttackOpportunityLevel)value.OpportunityLevel,
                    value.EarliestHitTick,
                    value.BindingRevision,
                    value.HurtboxRevision,
                    value.WorldRevision,
                    value.ZoneRevision,
                    new RuntimeFrame(value.SelectedFrame),
                    (CharacterTestDamageZoneSwitchReason)value.SwitchReason,
                    value.BodyPartStateRevision));
            }
            return true;
        }

        internal bool TryRestoreEngagementReservations(
            CharacterTestEngagementReservationStateSnapshot[] snapshots,
            out string failure)
        {
            return _reservationAuthority.TryRestore(snapshots, out failure);
        }

        /// <remarks>LowFreqAlloc: authoritative network/rollback restore only.</remarks>
        internal void SweepInvalidTopologyStateAfterAuthorityRestore(RuntimeFrame frame)
        {
            _reservationAuthority.SweepInvalid(_runtime, _navigationSession, frame);
            if (_runtime == null || _zoneLocksBySource.Count == 0)
                return;

            var sourceEntityIds = new int[_zoneLocksBySource.Count];
            _zoneLocksBySource.Keys.CopyTo(sourceEntityIds, 0);
            for (int i = 0; i < sourceEntityIds.Length; i++)
            {
                int sourceEntityId = sourceEntityIds[i];
                CharacterTestDamageZoneLock value = _zoneLocksBySource[sourceEntityId];
                int targetEntityId = value.TargetCombatEntityId.Value
                    - CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart;
                CharacterTestCharacterEntity target = _runtime.FindEntityById(targetEntityId);
                if (target != null
                    && value.TargetGameplayEntityId.Equals(target.GameplayEntityId))
                {
                    continue;
                }
                _zoneLocksBySource.Remove(sourceEntityId);
            }
        }

        /// <summary>
        /// Drops all frame-, geometry-, action-binding- and policy-derived state before
        /// rollback/replay applies an authoritative snapshot. A subsequent planner frame
        /// must rebuild its result from the restored world instead of reusing a same-frame
        /// decision from the discarded timeline.
        /// </summary>
        internal void ResetForRollbackReplay()
        {
            _preparationGeneration++;
            _service.ClearQueryCache();
            _cachedDecisions.Clear();
            _resolvedIntentCache.Clear();
            _policyCache.Clear();
            _evidenceBySource.Clear();
            _zoneLocksBySource.Clear();
            _engagementSelectionsBySource.Clear();
            _engagementDiagnosticsBySource.Clear();
            _reservationAuthority.Clear();
            _policyAuthorityFrame = default;
            _policyAuthorityWorldRevision = 0;
            _policyAuthorityActionRevision = 0;
            _hasPolicyAuthority = false;
            _lastEvidence.Set(
                0,
                0,
                default,
                CharacterTestRuntimeAiPlannerAttackDecisionSource.None,
                CharacterTestAttackOpportunityStatus.Unavailable,
                CharacterTestAttackOpportunityLevel.NoOpportunity,
                CharacterTestAttackOpportunityFailureReason.SnapshotUnavailable,
                0);
        }

        /// <summary>Gets the result captured during the current planner frame.</summary>
        internal bool CanSubmitPrimaryAttack(
            CharacterTestCharacterEntity entity,
            RuntimeFrame simulationFrame)
        {
            if (entity == null
                || entity.Control.Owner != CharacterTestCharacterControlOwner.AiControlled
                || !_cachedDecisions.TryGetValue(entity.Id.Value, out CachedDecision cached)
                || cached.PlannerFrame != simulationFrame
                || cached.Target == null
                || !cached.Matches(entity, _runtime?.ActionRuntime, _runtime))
            {
                return false;
            }

            CharacterTestAttackOpportunityResult result = cached.Result;
            return result.Status == CharacterTestAttackOpportunityStatus.Valid
                && result.Level == CharacterTestAttackOpportunityLevel.CurrentPose
                && result.CanHitFromCurrentPose;
        }

        /// <summary>
        /// Selection-time eligibility for the planner's PrimaryAttack action.
        /// The planner's compiled action id is intentionally not used here;
        /// it is a separate namespace from CharacterAction numeric ids.
        /// </summary>
        internal CharacterTestAttackOpportunityResult EvaluatePlannerPrimaryAttack(
            CharacterTestCharacterEntity source,
            int targetEntityId,
            RuntimeFrame plannerFrame)
        {
            CharacterTestCharacterEntity target = _runtime?.FindEntityById(targetEntityId);
            TryEvaluatePlannerPrimaryAttack(
                source,
                target,
                plannerFrame,
                out CharacterTestAttackOpportunityResult result);
            return result;
        }

        internal bool TryEvaluatePlannerPrimaryAttack(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            RuntimeFrame plannerFrame,
            out CharacterTestAttackOpportunityResult result)
        {
            result = default(CharacterTestAttackOpportunityResult);
            CharacterTestCharacterActionRuntime actionRuntime = _runtime?.ActionRuntime;
            if (source != null
                && target != null
                && _cachedDecisions.TryGetValue(source.Id.Value, out CachedDecision cached)
                && cached.PlannerFrame == plannerFrame
                && cached.Target != null
                && cached.Target.Id.Value == target.Id.Value
                && cached.Matches(source, actionRuntime, _runtime))
            {
                result = cached.Result;
                return result.Status == CharacterTestAttackOpportunityStatus.Valid;
            }

            CharacterActionConfigRegistry registry = actionRuntime?.Registry;
            if (source == null || target == null || actionRuntime == null || registry == null)
            {
                return FailClosed(
                    source,
                    target,
                    plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.SnapshotUnavailable,
                    out result);
            }

            CharacterActionResolveResult resolve = ResolvePrimaryAttackIntent(
                source,
                actionRuntime,
                plannerFrame);
            if (!resolve.IsSuccess
                || resolve.Plan == null
                || resolve.Plan.Category != CharacterActionCategory.BasicAttack
                || !resolve.Plan.ActionId.IsValid
                || !registry.TryGetAction(resolve.Plan.ActionId, out CharacterActionConfig resolvedAction)
                || resolvedAction == null
                || resolvedAction.Category != CharacterActionCategory.BasicAttack)
            {
                return FailClosed(
                    source,
                    target,
                    plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.TraceEvaluatorUnsupported,
                    out result);
            }

            return TryEvaluatePrimaryAttack(
                source,
                target,
                resolvedAction.ActionId,
                plannerFrame,
                out result);
        }

        /// <summary>
        /// Read-only M1-M3 preview for CD-M4 candidate reduction. It may use
        /// immutable/cached inputs but does not write evidence, decision cache,
        /// Zone locks, engagement selections or reservation authority.
        /// </summary>
        internal bool TryPreviewPlannerPrimaryAttack(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            RuntimeFrame plannerFrame,
            out CharacterTestAttackOpportunityResult result)
        {
            result = default;
            CharacterTestCharacterActionRuntime actionRuntime = _runtime?.ActionRuntime;
            CharacterActionConfigRegistry registry = actionRuntime?.Registry;
            if (source == null || target == null || actionRuntime == null || registry == null)
            {
                return FailClosed(
                    source,
                    target,
                    plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.SnapshotUnavailable,
                    out result,
                    commitState: false);
            }

            CharacterActionResolveResult resolve = ResolvePrimaryAttackIntent(
                source,
                actionRuntime,
                plannerFrame);
            if (!resolve.IsSuccess
                || resolve.Plan == null
                || resolve.Plan.Category != CharacterActionCategory.BasicAttack
                || !resolve.Plan.ActionId.IsValid
                || !registry.TryGetAction(resolve.Plan.ActionId, out CharacterActionConfig resolvedAction)
                || resolvedAction == null
                || resolvedAction.Category != CharacterActionCategory.BasicAttack)
            {
                return FailClosed(
                    source,
                    target,
                    plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.TraceEvaluatorUnsupported,
                    out result,
                    commitState: false);
            }

            return TryPreviewPrimaryAttack(
                source,
                target,
                resolvedAction.ActionId,
                plannerFrame,
                out result);
        }

        /// <summary>
        /// Resolves the final CharacterAction binding once per stable source
        /// action-set/equipment/binding identity. Only a successful static
        /// action identity is cached; dynamic rejects remain uncached.
        /// </summary>
        private CharacterActionResolveResult ResolvePrimaryAttackIntent(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame plannerFrame)
        {
            int characterStableId = source.Identity.CharacterId;
            string equipmentStateStableId = source.Equipment.EquipmentStateStableId ?? string.Empty;
            CharacterActionSetConfig actionSet = actionRuntime.TryGetEntityActionSetNoAlloc(
                source.Id,
                out CharacterActionSetConfig resolvedActionSet)
                ? resolvedActionSet
                : null;
            int actionSetBindingSignature = ComputeActionSetBindingSignature(actionSet);
            if (_resolvedIntentCache.TryGetValue(source.Id.Value, out CachedIntentResolution cached)
                && cached.Matches(
                    characterStableId,
                    equipmentStateStableId,
                    actionSet,
                    actionSetBindingSignature))
            {
                return cached.Result;
            }

            CharacterActionResolveResult result = actionRuntime.ResolveIntent(
                source,
                CharacterTestCharacterActionConfigSource.LightAttackIntentNumericId,
                plannerFrame,
                CharacterTestActionSlotIds.AttackPrimary);
            if (result.IsSuccess && result.Plan != null && actionSet != null)
            {
                _resolvedIntentCache[source.Id.Value] = new CachedIntentResolution(
                    result,
                    characterStableId,
                    equipmentStateStableId,
                    actionSet,
                    actionSetBindingSignature);
            }
            else
            {
                _resolvedIntentCache.Remove(source.Id.Value);
            }
            return result;
        }

        internal void CacheResult(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            RuntimeFrame plannerFrame,
            CharacterTestAttackOpportunityResult result)
        {
            if (source == null)
                return;
            CharacterActionSetConfig actionSet = null;
            int actionSetBindingSignature = 0;
            CharacterTestCharacterActionRuntime actionRuntime = _runtime?.ActionRuntime;
            if (actionRuntime != null
                && actionRuntime.TryGetEntityActionSetNoAlloc(source.Id, out CharacterActionSetConfig resolvedActionSet))
            {
                actionSet = resolvedActionSet;
                actionSetBindingSignature = ComputeActionSetBindingSignature(actionSet);
            }
            int worldRevision = _runtime?.PhysicsRuntime?.World?.Revision ?? 0;
            int targetHurtboxRevision = 0;
            if (target != null
                && _runtime != null
                && _runtime.TryGetDamageGeometrySnapshot(
                    target.Id.Value,
                    out CharacterTestDamageGeometrySnapshot targetGeometry)
                && targetGeometry != null)
            {
                targetHurtboxRevision = targetGeometry.HurtboxRevision;
            }
            int sourcePoseRevision = result.SnapshotSourcePoseRevision > 0
                ? result.SnapshotSourcePoseRevision
                : source.SourcePoseRevision;
            int sourceFacingRevision = result.SnapshotSourceFacingRevision > 0
                ? result.SnapshotSourceFacingRevision
                : source.SourceFacingRevision;
            _cachedDecisions[source.Id.Value] = new CachedDecision(
                plannerFrame,
                target,
                result,
                source.Identity.CharacterId,
                source.Equipment.EquipmentStateStableId,
                actionSet,
                actionSetBindingSignature,
                worldRevision,
                targetHurtboxRevision,
                sourcePoseRevision,
                sourceFacingRevision,
                source.Transform.XMillimeters,
                source.Transform.YMillimeters,
                source.Transform.ZMillimeters,
                source.Transform.YawMilliDegrees);
        }

        internal void MarkCompatibilityLegacyRange(
            CharacterTestCharacterEntity source,
            RuntimeFrame plannerFrame)
        {
            if (source == null)
                return;
            _cachedDecisions.Remove(source.Id.Value);
            SetEvidence(
                source.Id.Value,
                0,
                plannerFrame,
                CharacterTestRuntimeAiPlannerAttackDecisionSource.CompatibilityLegacyRange,
                CharacterTestAttackOpportunityStatus.Valid,
                CharacterTestAttackOpportunityLevel.NoOpportunity,
                CharacterTestAttackOpportunityFailureReason.None,
                0);
        }

        /// <summary>
        /// Captures the previous completed frame and independently builds both
        /// CurrentPose and TurnInPlace preview plans for one PrimaryAttack.
        /// </summary>
        internal bool TryEvaluatePrimaryAttack(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterActionId requestedActionId,
            RuntimeFrame plannerFrame,
            out CharacterTestAttackOpportunityResult result)
        {
            return TryEvaluatePrimaryAttackCore(
                source,
                target,
                requestedActionId,
                plannerFrame,
                commitState: true,
                out result);
        }

        internal bool TryPreviewPrimaryAttack(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterActionId requestedActionId,
            RuntimeFrame plannerFrame,
            out CharacterTestAttackOpportunityResult result)
        {
            return TryEvaluatePrimaryAttackCore(
                source,
                target,
                requestedActionId,
                plannerFrame,
                commitState: false,
                out result);
        }

        private bool TryEvaluatePrimaryAttackCore(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterActionId requestedActionId,
            RuntimeFrame plannerFrame,
            bool commitState,
            out CharacterTestAttackOpportunityResult result)
        {
            if (source == null || target == null || _runtime == null)
            {
                return FailClosed(
                    source,
                    target,
                    plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.SnapshotUnavailable,
                    out result,
                    commitState);
            }

            if (commitState)
            {
                _reservationAuthority.SweepInvalid(
                    _runtime,
                    _navigationSession,
                    plannerFrame);
            }

            if (!_runtime.TryGetLastCompletedRuntimeFrame(out RuntimeFrame completedFrame))
            {
                return FailClosed(source, target, plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.SnapshotUnavailable, out result, commitState);
            }

            CharacterTestCharacterPhysicsRuntime physics = _runtime.PhysicsRuntime;
            CombatPhysicsWorld world = physics?.World;
            CharacterTestCharacterCombatTraceSystem combat = _runtime.CombatTraceSystem;
            if (physics == null || world == null || combat == null)
            {
                return FailClosed(source, target, plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.SnapshotUnavailable, out result, commitState);
            }

            if (!combat.WorldTargetClassifications.IsCurrent(completedFrame, world.Revision))
            {
                return FailClosed(source, target, plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.StaleSnapshot, out result, commitState);
            }

            if (!_runtime.TryGetDamageGeometrySnapshot(
                    target.Id.Value,
                    out CharacterTestDamageGeometrySnapshot geometry)
                || geometry == null
                || !geometry.IsValid)
            {
                return FailClosed(source, target, plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.SnapshotUnavailable, out result, commitState);
            }

            if (geometry.Frame != completedFrame
                || geometry.WorldRevision != world.Revision)
            {
                return FailClosed(source, target, plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.StaleSnapshot, out result, commitState);
            }

            CharacterActionConfig action = ResolveAction(requestedActionId);
            if (action == null || action.Category != CharacterActionCategory.BasicAttack)
            {
                return FailClosed(source, target, plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.TraceEvaluatorUnsupported, out result, commitState);
            }
            CharacterTestActionPartStateEligibility partStateEligibility =
                _runtime.ActionRuntime.EvaluatePartStateEligibility(source, action);
            if (!partStateEligibility.IsEligible)
            {
                return FailClosed(source, target, plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.PartStateDependencyRejected, out result, commitState);
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
                return FailClosed(source, target, plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.SnapshotUnavailable, out result, commitState);
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
                return FailClosed(source, target, plannerFrame,
                    CharacterTestAttackOpportunityFailureReason.TracePlanUnavailable, out result, commitState);
            }

            CharacterTestAttackOpportunityQuery query = new CharacterTestAttackOpportunityQuery(
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

            CharacterTestDamageZoneSnapshot zones = null;
            CharacterTestDamageZoneLock previousLock = default;
            if (_runtime.TryGetDamageZoneSnapshot(target.Id.Value, out zones))
            {
                _zoneLocksBySource.TryGetValue(
                    source.Id.Value,
                    out previousLock);
                bool sameActor = previousLock.HasLock
                    && previousLock.TargetGameplayEntityId.Equals(target.GameplayEntityId);
                CharacterTestDamageZoneLock lockedHurtbox = previousLock;
                if (!sameActor)
                {
                    if (_runtime.PerceptionFrameCache == null
                        || !_runtime.PerceptionFrameCache.TryAcquireNearestHurtbox(
                            plannerFrame,
                            source,
                            target,
                            out CharacterTestCombatTargetHandle acquired,
                            out _))
                    {
                        if (commitState)
                            _zoneLocksBySource.Remove(source.Id.Value);
                        return FailClosed(
                            source,
                            target,
                            plannerFrame,
                            CharacterTestAttackOpportunityFailureReason.NoCompatibleHurtbox,
                            out result,
                            commitState);
                    }

                    lockedHurtbox = CreateInitialLockedHurtboxLock(
                        acquired,
                        snapshot,
                        zones,
                        target);
                }

                CharacterTestDamageZoneSelectionResult selection =
                    _zoneSelector.EvaluateLockedHurtbox(
                        query,
                        zones,
                        lockedHurtbox,
                        requiredYawDelta);
                if (selection.IsUsable)
                {
                    result = selection.Opportunity;
                    previousLock = selection.ZoneLock;
                    if (commitState)
                        _zoneLocksBySource[source.Id.Value] = selection.ZoneLock;
                }
                else
                {
                    if (commitState)
                        _zoneLocksBySource[source.Id.Value] = lockedHurtbox;
                    previousLock = lockedHurtbox;
                    CharacterTestAttackOpportunityFailureReason reason = selection.Status
                        == CharacterTestDamageZoneSelectionStatus.StaleSnapshot
                            ? CharacterTestAttackOpportunityFailureReason.StaleSnapshot
                            : CharacterTestAttackOpportunityFailureReason.NoCompatibleHurtbox;
                    return FailClosed(source, target, plannerFrame, reason, out result, commitState);
                }
            }
            else
            {
                // DamageZone is optional. Preserve the exact Hurtbox identity and
                // filter the opportunity directly by LogicalHurtboxId.
                _zoneLocksBySource.TryGetValue(source.Id.Value, out previousLock);
                bool sameActor = previousLock.HasLock
                    && previousLock.TargetGameplayEntityId.Equals(target.GameplayEntityId);
                if (!sameActor)
                {
                    if (_runtime.PerceptionFrameCache == null
                        || !_runtime.PerceptionFrameCache.TryAcquireNearestHurtbox(
                            plannerFrame,
                            source,
                            target,
                            out CharacterTestCombatTargetHandle acquired,
                            out CharacterTestLockedHurtboxDistanceSample distance))
                    {
                        if (commitState)
                            _zoneLocksBySource.Remove(source.Id.Value);
                        return FailClosed(
                            source,
                            target,
                            plannerFrame,
                            CharacterTestAttackOpportunityFailureReason.NoCompatibleHurtbox,
                            out result,
                            commitState);
                    }

                    previousLock = new CharacterTestDamageZoneLock(
                        acquired.TargetGameplayEntityId,
                        acquired.TargetCombatEntityId,
                        snapshot.ResolvedActionRevision,
                        0,
                        acquired.HitZoneId,
                        acquired.LogicalHurtboxId,
                        CharacterTestAttackOpportunityLevel.NoOpportunity,
                        -1,
                        acquired.BindingRevisionAtSelection,
                        distance.TargetHurtboxRevision,
                        world.Revision,
                        0,
                        acquired.SelectedFrame,
                        CharacterTestDamageZoneSwitchReason.InitialSelection,
                        target.BodyPartState?.Revision ?? 0L,
                        acquired.SourceEntityId,
                        acquired.TargetEntityId);
                }

                query = query.WithDamageZoneFilter(
                    null,
                    0,
                    previousLock.LogicalHurtboxId);
                result = _service.Evaluate(query, requiredYawDelta);
                if (commitState)
                    _zoneLocksBySource[source.Id.Value] = previousLock;
            }

            if (zones != null && previousLock.HasLock)
            {
                query = query.WithDamageZoneFilter(
                    zones,
                    previousLock.ZoneId,
                    previousLock.LogicalHurtboxId);
            }

            bool attemptedEngagement = zones != null
                && result.Status == CharacterTestAttackOpportunityStatus.Valid
                && result.RequiresTranslation;
            bool selectedEngagement = false;
            CharacterTestEngagementCandidateResult engagement = default;
            CharacterTestEngagementCandidateStatus engagementStatus =
                CharacterTestEngagementCandidateStatus.None;
            if (attemptedEngagement)
            {
                if (TryEvaluateEngagementAnchors(
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
                        out engagement,
                        out engagementStatus,
                        out _,
                        CharacterTestRuntimeAiJointGenerationBudget.HardCandidatePosePreviewLimit,
                        CharacterTestRuntimeAiJointGenerationBudget.HardPathQueryLimit))
                {
                    selectedEngagement = commitState
                        ? _reservationAuthority.TryAcquire(
                            source.Id.Value,
                            target.Id.Value,
                            snapshot.ResolvedActionRevision,
                            plannerFrame,
                            engagement,
                            out _)
                        : _reservationAuthority.CanAcquire(
                            source.Id.Value,
                            target.Id.Value,
                            snapshot.ResolvedActionRevision,
                            plannerFrame,
                            engagement);
                    if (!selectedEngagement)
                        engagementStatus = CharacterTestEngagementCandidateStatus.CapacityFull;
                }
                if (commitState)
                {
                    _engagementDiagnosticsBySource[source.Id.Value] =
                        new CharacterTestEngagementDiagnosticState(
                            source.Id.Value,
                            target.Id.Value,
                            snapshot.ResolvedActionRevision,
                            plannerFrame,
                            engagementStatus,
                            engagement);
                }
            }
            else
            {
                if (commitState)
                    _engagementDiagnosticsBySource.Remove(source.Id.Value);
            }

            if (selectedEngagement)
            {
                result = engagement.Opportunity;
                if (commitState)
                {
                    _engagementSelectionsBySource[source.Id.Value] =
                        new CharacterTestEngagementSelectionState(plannerFrame, engagement);
                    _zoneLocksBySource[source.Id.Value] = CreateEngagementZoneLock(
                        target,
                        snapshot,
                        zones,
                        engagement,
                        plannerFrame);
                }
            }
            else if (commitState)
            {
                _engagementSelectionsBySource.Remove(source.Id.Value);
                _reservationAuthority.Release(
                    source.Id.Value,
                    CharacterTestEngagementReservationReleaseReason.NotRenewed);
            }
            if (commitState)
            {
                SetEvidence(
                    source.Id.Value,
                    target.Id.Value,
                    plannerFrame,
                    CharacterTestRuntimeAiPlannerAttackDecisionSource.AttackOpportunity,
                    result.Status,
                    result.Level,
                    result.FailureReason,
                    result.RequiredYawDeltaMilliDegrees);
                CacheResult(source, target, plannerFrame, result);
            }
            return result.Status == CharacterTestAttackOpportunityStatus.Valid;
        }

        private bool TryEvaluateEngagementAnchors(
            CharacterActionConfig action,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            RuntimeFrame plannerFrame,
            RuntimeFrame completedFrame,
            CharacterTestCharacterPhysicsRuntime physics,
            CharacterTestAttackEvaluationSnapshot snapshot,
            CharacterTestAttackOpportunityQuery baseQuery,
            CharacterTestDamageZoneSnapshot zones,
            CharacterTestDamageZoneLock previousLock,
            out CharacterTestEngagementCandidateResult selection,
            out CharacterTestEngagementCandidateStatus rejectionStatus,
            out CharacterTestRuntimeAiJointEvaluationBudgetDiagnostics budgetDiagnostics,
            int candidatePosePreviewLimit,
            int pathQueryLimit,
            int requiredZoneId = 0)
        {
            selection = default;
            rejectionStatus = CharacterTestEngagementCandidateStatus.SnapshotUnavailable;
            budgetDiagnostics = default;
            int candidatePosePreviewCount = 0;
            int pathQueryCount = 0;
            if (_navigationSession == null
                || !_navigationSession.IsAvailable
                || !_runtime.TryGetRuntimeGeometryBinding(
                    target.Id.Value,
                    out CharacterImportedGeometryBinding geometryBinding)
                || geometryBinding == null
                || geometryBinding.EngagementAnchorMode
                    != CharacterEngagementAnchorMode.AuthoredAnchors
                || geometryBinding.EngagementAnchorRevision <= 0
                || geometryBinding.EngagementAnchors == null
                || geometryBinding.EngagementAnchors.Length == 0)
            {
                if (_navigationSession == null || !_navigationSession.IsAvailable)
                    rejectionStatus = CharacterTestEngagementCandidateStatus.NavigationUnavailable;
                return false;
            }

            bool hasSelection = false;
            CharacterEngagementAnchorRuntimeBinding[] anchors = geometryBinding.EngagementAnchors;
            for (int i = 0; i < anchors.Length; i++)
            {
                CharacterEngagementAnchorRuntimeBinding anchor = anchors[i];
                if (!(requiredZoneId <= 0)
                    && !(anchor.ZoneId == requiredZoneId))
                {
                    continue;
                }
                if (!CharacterTestRuntimeAiJointEvaluationBudgetCounter.TryConsume(
                        ref candidatePosePreviewCount,
                        candidatePosePreviewLimit,
                        CharacterTestRuntimeAiJointTruncationPhase.CandidatePose,
                        pathQueryCount,
                        out CharacterTestRuntimeAiJointEvaluationBudgetDiagnostics poseBudget))
                {
                    budgetDiagnostics = poseBudget;
                    rejectionStatus = CharacterTestEngagementCandidateStatus.PathBudgetExceeded;
                    break;
                }
                var candidateQuery = new CharacterTestEngagementCandidateQuery(
                    completedFrame,
                    source,
                    target,
                    snapshot.ResolvedActionRevision,
                    anchor,
                    geometryBinding.EngagementAnchorRevision,
                    geometryBinding.EngagementAnchorContentDigest,
                    zones,
                    physics,
                    _navigationSession,
                    phaseId: CharacterEngagementAnchorRuntimeBinding.DefaultPhaseId,
                    phaseRevision: 1);
                if (!_engagementPlanner.TryProjectAndValidate(
                        candidateQuery,
                        out CharacterTestEngagementCandidatePose pose,
                        out CharacterTestEngagementCandidateStatus candidateStatus))
                {
                    rejectionStatus = candidateStatus;
                    continue;
                }
                if (!TryBuildCandidatePreviewPlan(
                        action,
                        source,
                        snapshot,
                        physics,
                        completedFrame,
                        snapshot.ResolvedActionRevision,
                        pose.WorldPosition,
                        pose.WorldYawMilliDegrees,
                        out CharacterTestDirectHitTracePlanView candidatePlan))
                {
                    rejectionStatus = CharacterTestEngagementCandidateStatus.AttackUnavailable;
                    continue;
                }

                int candidateYawDelta = CharacterTestDeterministicMath.ShortestAngleDelta(
                    snapshot.SourceYawMilliDegrees,
                    pose.WorldYawMilliDegrees);
                CharacterTestAttackOpportunityQuery attackQuery = baseQuery
                    .WithCandidatePose(
                        candidatePlan,
                        pose.WorldPosition,
                        pose.WorldYawMilliDegrees)
                    .WithDamageZoneFilter(zones, pose.ZoneId);
                CharacterTestAttackOpportunityResult opportunity = _service.Evaluate(
                    attackQuery,
                    CharacterTestAttackEvaluationMode.CandidatePose,
                    candidateYawDelta);
                if (opportunity.Status != CharacterTestAttackOpportunityStatus.Valid
                    || !opportunity.TraceCanResolveTargetHit)
                {
                    rejectionStatus = CharacterTestEngagementCandidateStatus.AttackUnavailable;
                    continue;
                }
                if (!CharacterTestRuntimeAiJointEvaluationBudgetCounter.TryConsume(
                        ref pathQueryCount,
                        pathQueryLimit,
                        CharacterTestRuntimeAiJointTruncationPhase.PathQuery,
                        candidatePosePreviewCount,
                        out CharacterTestRuntimeAiJointEvaluationBudgetDiagnostics pathBudget))
                {
                    budgetDiagnostics = pathBudget;
                    rejectionStatus = CharacterTestEngagementCandidateStatus.PathBudgetExceeded;
                    break;
                }
                if (!_engagementPlanner.TryEvaluatePath(
                        candidateQuery,
                        pose,
                        out CharacterTestPathfindingSample path,
                        out candidateStatus))
                {
                    rejectionStatus = candidateStatus;
                    continue;
                }

                var candidate = new CharacterTestEngagementCandidateResult(
                    CharacterTestEngagementCandidateStatus.Success,
                    pose,
                    opportunity,
                    path,
                    anchor.AuthoredPriority,
                    anchor.Capacity,
                    reservationCount: _reservationAuthority.GetReservationCount(
                        pose.TargetEntityId,
                        pose.ZoneId,
                        pose.AnchorId,
                        source.Id.Value),
                    isLockedZone: previousLock.HasLock
                        && (
                            previousLock.ZoneId == anchor.ZoneId));
                if (!hasSelection || CharacterTestEngagementPlanner.IsBetter(candidate, selection))
                {
                    selection = candidate;
                    hasSelection = true;
                }
            }
            if (hasSelection)
                rejectionStatus = CharacterTestEngagementCandidateStatus.Success;
            if (budgetDiagnostics.TruncationPhase == CharacterTestRuntimeAiJointTruncationPhase.None)
            {
                budgetDiagnostics = new CharacterTestRuntimeAiJointEvaluationBudgetDiagnostics(
                    candidatePosePreviewCount,
                    pathQueryCount,
                    CharacterTestRuntimeAiJointTruncationPhase.None,
                    0,
                    0,
                    0);
            }
            return hasSelection;
        }

        private static CharacterTestDamageZoneLock CreateEngagementZoneLock(
            CharacterTestCharacterEntity target,
            CharacterTestAttackEvaluationSnapshot snapshot,
            CharacterTestDamageZoneSnapshot zones,
            CharacterTestEngagementCandidateResult engagement,
            RuntimeFrame plannerFrame)
        {
            CharacterTestAttackOpportunityResult opportunity = engagement.Opportunity;
            return new CharacterTestDamageZoneLock(
                target.GameplayEntityId,
                snapshot.TargetEntityId,
                snapshot.ResolvedActionRevision,
                engagement.Pose.ZoneId,
                opportunity.SelectedHitZoneId,
                opportunity.SelectedHurtboxId,
                opportunity.Level,
                opportunity.EarliestCandidateHitTick,
                zones.BindingRevision,
                zones.HurtboxRevision,
                zones.WorldRevision,
                zones.ZoneRevision,
                plannerFrame,
                CharacterTestDamageZoneSwitchReason.InitialSelection,
                target.BodyPartState?.Revision ?? 0L);
        }

        private static CharacterTestDamageZoneLock CreateInitialLockedHurtboxLock(
            in CharacterTestCombatTargetHandle handle,
            CharacterTestAttackEvaluationSnapshot snapshot,
            CharacterTestDamageZoneSnapshot zones,
            CharacterTestCharacterEntity target)
        {
            return new CharacterTestDamageZoneLock(
                handle.TargetGameplayEntityId,
                handle.TargetCombatEntityId,
                snapshot.ResolvedActionRevision,
                handle.DamageZoneId,
                handle.HitZoneId,
                handle.LogicalHurtboxId,
                CharacterTestAttackOpportunityLevel.NoOpportunity,
                -1,
                zones?.BindingRevision ?? handle.BindingRevisionAtSelection,
                zones?.HurtboxRevision ?? 0,
                zones?.WorldRevision ?? 0,
                zones?.ZoneRevision ?? 0,
                handle.SelectedFrame,
                CharacterTestDamageZoneSwitchReason.InitialSelection,
                target?.BodyPartState?.Revision ?? 0L,
                handle.SourceEntityId,
                handle.TargetEntityId);
        }

        private CharacterActionConfig ResolveAction(CharacterActionId requestedActionId)
        {
            CharacterActionConfigRegistry registry = _runtime.ActionRuntime?.Registry;
            if (registry == null)
                return null;
            CharacterActionId effective = requestedActionId.IsValid
                ? requestedActionId
                : CharacterTestCharacterActionConfigSource.LightAttackActionId;
            return registry.TryGetAction(effective, out CharacterActionConfig resolved)
                ? resolved
                : null;
        }

        private bool TryBuildPreviewPlans(
            CharacterActionConfig action,
            CharacterTestCharacterEntity source,
            CharacterTestAttackEvaluationSnapshot snapshot,
            CharacterTestCharacterPhysicsRuntime physics,
            RuntimeFrame completedFrame,
            int actionRevision,
            int requiredYawDelta,
            out CharacterTestDirectHitTracePlanView currentPlan,
            out CharacterTestDirectHitTracePlanView turnPlan,
            out CharacterTestAttackOpportunitySamplePolicySnapshot policies)
        {
            PreviewPlanPreparationCount++;
            _preparationGeneration++;
            _turnPlanAttempted = false;
            _preparedTurnPlan = default;
            _previewSocketOffsets.Clear();
            currentPlan = default(CharacterTestDirectHitTracePlanView);
            turnPlan = default(CharacterTestDirectHitTracePlanView);
            policies = null;
            CombatTrackEvent[] events = action.CombatTrack?.Events;
            if (events == null || events.Length == 0)
                return false;

            if (!_hasPolicyAuthority
                || _policyAuthorityFrame != completedFrame
                || _policyAuthorityWorldRevision != physics.World.Revision
                || _policyAuthorityActionRevision != actionRevision)
            {
                // The cache key and per-sample validation bind policy to the
                // complete source/target/action/team/state/event identity.
                // Keep equivalent frozen policies across completed frames so
                // the warmed PreSimulation path does not allocate every tick.
                _policyAuthorityFrame = completedFrame;
                _policyAuthorityWorldRevision = physics.World.Revision;
                _policyAuthorityActionRevision = actionRevision;
                _hasPolicyAuthority = true;
            }

            int firstFrame = int.MaxValue;
            int lastFrame = -1;
            for (int i = 0; i < events.Length; i++)
            {
                CombatTrackEvent evt = events[i];
                if (evt.Kind != CharacterActionTrackEventKind.HitTraceWindow)
                    continue;
                if (evt.EndFrame < evt.Frame)
                    continue;
                firstFrame = Math.Min(firstFrame, evt.Frame);
                lastFrame = Math.Max(lastFrame, evt.EndFrame);
            }
            if (firstFrame == int.MaxValue || lastFrame < firstFrame || lastFrame - firstFrame > 240)
                return false;

            CollectActiveFrames(events, firstFrame, lastFrame, _activePreviewFrames);
            BuildSourcePoseOffsets(
                action,
                snapshot.SourceYawMilliDegrees,
                firstFrame,
                lastFrame,
                _poseOffsets);
            _weaponFrames.Clear();
            _turnWeaponFrames.Clear();
            _capsuleFrames.Clear();
            _turnCapsuleFrames.Clear();
            if (!BuildPreviewFrames(
                    action,
                    source,
                    physics,
                    completedFrame,
                    actionRevision,
                    firstFrame,
                    lastFrame,
                    events,
                    _poseOffsets,
                    snapshot.SourceYawMilliDegrees,
                    _weaponFrames,
                    _capsuleFrames,
                    buildWeaponFrames: true))
            {
                return false;
            }
            FixVector3 sourcePosition = snapshot.SourcePosition;
            var currentIdentity = new CharacterTestDirectHitTracePlanPoseIdentity(
                CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
                sourcePosition,
                snapshot.SourceYawMilliDegrees,
                snapshot.SourcePoseRevision,
                snapshot.SourceFacingRevision);
            _queryBuffer.Clear();
            if (!CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(
                    completedFrame,
                    physics.World.Revision,
                    snapshot.SourceEntityId,
                    actionRevision,
                    currentIdentity,
                    _poseOffsets,
                    _weaponFrames,
                    _capsuleFrames,
                    useTipSweepCapsules: true,
                    PreviewMaxTipDistancePerSubstep,
                    PreviewMaxSubsteps,
                    _queryBuffer,
                    _currentPlanBuffer,
                    out currentPlan, requiredActiveFrames: _activePreviewFrames))
            {
                return false;
            }

            _pendingTurn = new PendingTurnPlan(action, source, snapshot, physics, completedFrame,
                actionRevision, requiredYawDelta, firstFrame, lastFrame, _currentPlanBuffer.SealVersion);
            policies = GetOrBuildPolicies(action, source, snapshot, currentPlan);
            return policies != null && policies.IsSealed;
        }

        bool ICharacterTestAttackTurnPlanProvider.TryGetTurnPlan(long generation, out CharacterTestDirectHitTracePlanView plan)
        {
            plan = default;
            var pending = _pendingTurn;
            if (generation != _preparationGeneration || pending.Action == null
                || pending.CurrentSeal != _currentPlanBuffer.SealVersion
                || pending.Physics.World.Revision != pending.Snapshot.CombatPhysicsRevision
                || pending.Source.SourcePoseRevision != pending.Snapshot.SourcePoseRevision
                || pending.Source.SourceFacingRevision != pending.Snapshot.SourceFacingRevision) return false;
            if (_turnPlanAttempted)
            {
                plan = _preparedTurnPlan;
                return plan.IsCurrent(pending.Frame, pending.Physics.World.Revision, pending.Snapshot.SourceEntityId, pending.Revision);
            }
            _turnPlanAttempted = true;
            TurnPlanPreparationCount++;
            int yaw = pending.Snapshot.SourceYawMilliDegrees + pending.YawDelta;
            BuildSourcePoseOffsets(pending.Action, yaw, pending.First, pending.Last, _turnPoseOffsets);
            _turnWeaponFrames.Clear();
            _turnWeaponFrames.AddRange(_weaponFrames);
            _turnCapsuleFrames.Clear();
            if (!BuildPreviewFrames(pending.Action, pending.Source, pending.Physics, pending.Frame, pending.Revision,
                pending.First, pending.Last, pending.Action.CombatTrack.Events, _turnPoseOffsets, yaw,
                _turnWeaponFrames, _turnCapsuleFrames, buildWeaponFrames: false)) return false;
            var identity = new CharacterTestDirectHitTracePlanPoseIdentity(CharacterTestDirectHitTracePlanPoseKind.TurnInPlace,
                pending.Snapshot.SourcePosition, yaw, pending.Snapshot.SourcePoseRevision, pending.Snapshot.SourceFacingRevision);
            _queryBuffer.Clear();
            if (!CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(pending.Frame, pending.Physics.World.Revision,
                pending.Snapshot.SourceEntityId, pending.Revision, identity, _turnPoseOffsets, _turnWeaponFrames,
                _turnCapsuleFrames, true, PreviewMaxTipDistancePerSubstep, PreviewMaxSubsteps, _queryBuffer,
                _turnPlanBuffer, out _preparedTurnPlan, requiredActiveFrames: _activePreviewFrames)) return false;
            plan = _preparedTurnPlan;
            return true;
        }

        private bool TryBuildCandidatePreviewPlan(
            CharacterActionConfig action,
            CharacterTestCharacterEntity source,
            CharacterTestAttackEvaluationSnapshot snapshot,
            CharacterTestCharacterPhysicsRuntime physics,
            RuntimeFrame completedFrame,
            int actionRevision,
            FixVector3 candidatePosition,
            int candidateYawMilliDegrees,
            out CharacterTestDirectHitTracePlanView candidatePlan)
        {
            _previewSocketOffsets.Clear();
            candidatePlan = default;
            CombatTrackEvent[] events = action.CombatTrack?.Events;
            if (events == null || events.Length == 0)
                return false;

            int firstFrame = int.MaxValue;
            int lastFrame = -1;
            for (int i = 0; i < events.Length; i++)
            {
                CombatTrackEvent evt = events[i];
                if (evt.Kind != CharacterActionTrackEventKind.HitTraceWindow
                    || evt.EndFrame < evt.Frame)
                {
                    continue;
                }
                firstFrame = Math.Min(firstFrame, evt.Frame);
                lastFrame = Math.Max(lastFrame, evt.EndFrame);
            }
            if (firstFrame == int.MaxValue || lastFrame < firstFrame || lastFrame - firstFrame > 240)
                return false;

            CollectActiveFrames(events, firstFrame, lastFrame, _candidateActiveFrames);
            BuildSourcePoseOffsets(
                action,
                candidateYawMilliDegrees,
                firstFrame,
                lastFrame,
                _candidatePoseOffsets);
            _candidateWeaponFrames.Clear();
            _candidateCapsuleFrames.Clear();
            if (!BuildPreviewFrames(
                    action,
                    source,
                    physics,
                    completedFrame,
                    actionRevision,
                    firstFrame,
                    lastFrame,
                    events,
                    _candidatePoseOffsets,
                    candidateYawMilliDegrees,
                    _candidateWeaponFrames,
                    _candidateCapsuleFrames,
                    buildWeaponFrames: true))
            {
                return false;
            }

            var identity = new CharacterTestDirectHitTracePlanPoseIdentity(
                CharacterTestDirectHitTracePlanPoseKind.CandidatePose,
                candidatePosition,
                candidateYawMilliDegrees,
                snapshot.SourcePoseRevision,
                snapshot.SourceFacingRevision);
            _queryBuffer.Clear();
            return CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(
                completedFrame,
                physics.World.Revision,
                snapshot.SourceEntityId,
                actionRevision,
                identity,
                _candidatePoseOffsets,
                _candidateWeaponFrames,
                _candidateCapsuleFrames,
                useTipSweepCapsules: true,
                PreviewMaxTipDistancePerSubstep,
                PreviewMaxSubsteps,
                _queryBuffer,
                _candidatePlanBuffer,
                out candidatePlan, requiredActiveFrames: _candidateActiveFrames);
        }

        private static void CollectActiveFrames(CombatTrackEvent[] events, int first, int last, List<int> frames)
        {
            frames.Clear();
            for (int frame = first; frame <= last; frame++)
                for (int i = 0; i < events.Length; i++)
                    if (events[i].Kind == CharacterActionTrackEventKind.HitTraceWindow
                        && frame >= events[i].Frame && frame <= events[i].EndFrame)
                    { frames.Add(frame); break; }
        }

        private void BuildSourcePoseOffsets(
            CharacterActionConfig action,
            int poseYawMilliDegrees,
            int firstFrame,
            int lastFrame,
            List<CharacterTestDirectHitTraceSourcePoseOffset> output)
        {
            output.Clear();
            if (_nominalSourcePoseOffsets != null)
            {
                // Offline reference sessions provide poses recorded from the formal movement simulation.
                // Missing frames remain missing so the trace sampler rejects an incomplete trajectory.
                for (int i = 0; i < _nominalSourcePoseOffsets.Count; i++)
                {
                    CharacterTestDirectHitTraceSourcePoseOffset pose = _nominalSourcePoseOffsets[i];
                    if (pose.LocalFrame >= firstFrame && pose.LocalFrame <= lastFrame) output.Add(pose);
                }
                return;
            }
            FixVector3 accumulatedLocal = FixVector3.Zero;
            FixVector3 accumulatedWorld = FixVector3.Zero;
            CharacterActionRootMotionConfig rootMotion = action.MotionTrack?.RootMotion;
            for (int frame = 0; frame <= lastFrame; frame++)
            {
                if (rootMotion != null && rootMotion.TryGetDelta(frame, out float x, out float y, out float z))
                {
                    FixVector3 delta = ToFixVector3(x, y, z);
                    if (rootMotion.Space == CharacterActionRootMotionSpace.World)
                    {
                        accumulatedWorld += delta;
                    }
                    else
                    {
                        accumulatedLocal += delta;
                    }
                }
                if (action.MotionTrack != null
                    && action.MotionTrack.TryGetVerticalRootMotionDelta(frame, out float verticalDelta))
                {
                    accumulatedWorld += ToFixVector3(0f, verticalDelta, 0f);
                }

                if (frame >= firstFrame)
                {
                    FixVector3 localOffset = rootMotion != null
                        && rootMotion.Space == CharacterActionRootMotionSpace.World
                        ? new CharacterTestDirectHitTraceSourcePose(0, FixVector3.Zero, -poseYawMilliDegrees)
                            .RotateLocal(accumulatedWorld)
                        : accumulatedLocal;
                    output.Add(new CharacterTestDirectHitTraceSourcePoseOffset(
                        frame,
                        localOffset,
                        0));
                }
            }
        }

        private bool BuildPreviewFrames(
            CharacterActionConfig action,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterPhysicsRuntime physics,
            RuntimeFrame completedFrame,
            int actionRevision,
            int firstFrame,
            int lastFrame,
            CombatTrackEvent[] events,
            IReadOnlyList<CharacterTestDirectHitTraceSourcePoseOffset> poseOffsets,
            int poseYawMilliDegrees,
            List<CharacterTestDirectHitTracePreviewWeaponFrame> weaponOutput,
            List<CharacterTestDirectHitTracePreviewCapsuleFrame> capsuleOutput,
            bool buildWeaponFrames)
        {
            for (int localFrame = firstFrame; localFrame <= lastFrame; localFrame++)
            {
                for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
                {
                    CombatTrackEvent evt = events[eventIndex];
                    if (evt.Kind != CharacterActionTrackEventKind.HitTraceWindow
                        || localFrame < evt.Frame
                        || localFrame > evt.EndFrame)
                    {
                        continue;
                    }

                    CombatPhysicsLayerMask targetMask =
                        CharacterTestCombatHitEventResolver.ResolveQueryLayerMask(evt.HitEvent, physics);
                    int actionId = action.ActionId.Value;
                    int policyEventId = evt.StableEventId;
                    int traceId = evt.StableEventId;

                    if (evt.HitVolume.HasVolume)
                    {
                        if (!TryBuildPreviewCapsule(
                                action,
                                evt,
                                source,
                                physics,
                                completedFrame,
                                localFrame,
                                eventIndex,
                                actionId,
                                traceId,
                                policyEventId,
                                actionRevision,
                                targetMask,
                            out CharacterTestDirectHitTracePreviewCapsuleFrame capsule))
                        {
                            return false;
                        }
                        capsuleOutput.Add(capsule);
                        if (evt.HitVolume.SourceKind == CharacterActionHitVolumeSourceKind.CasterBody
                            && !AppendCasterBodySweepCapsules(
                                evt,
                                source,
                                physics,
                                localFrame,
                                eventIndex,
                                actionId,
                                traceId,
                                policyEventId,
                                actionRevision,
                                targetMask,
                                poseOffsets,
                                poseYawMilliDegrees,
                                capsuleOutput))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (!buildWeaponFrames) continue;
                        if (!TryBuildPreviewWeapon(
                                action,
                                evt,
                                source,
                                physics,
                                localFrame,
                                eventIndex,
                                actionId,
                                targetMask,
                                out CharacterTestDirectHitTracePreviewWeaponFrame weapon))
                        {
                            return false;
                        }
                        if (buildWeaponFrames)
                            weaponOutput.Add(weapon);
                    }
                }

                // Frames between authored hit windows are part of the trajectory, but emit no hit samples.
            }

            return buildWeaponFrames
                ? weaponOutput.Count > 0 || capsuleOutput.Count > 0
                : capsuleOutput.Count > 0 || HasWeaponEvent(events, firstFrame, lastFrame);
        }

        private static bool HasWeaponEvent(
            CombatTrackEvent[] events,
            int firstFrame,
            int lastFrame)
        {
            for (int i = 0; i < events.Length; i++)
            {
                CombatTrackEvent evt = events[i];
                if (evt.Kind == CharacterActionTrackEventKind.HitTraceWindow
                    && !evt.HitVolume.HasVolume
                    && evt.EndFrame >= firstFrame
                    && evt.Frame <= lastFrame)
                    return true;
            }
            return false;
        }

        private bool AppendCasterBodySweepCapsules(
            CombatTrackEvent evt,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterPhysicsRuntime physics,
            int localFrame,
            int sourceOrder,
            int actionId,
            int traceId,
            int policyEventId,
            int actionRevision,
            CombatPhysicsLayerMask targetMask,
            IReadOnlyList<CharacterTestDirectHitTraceSourcePoseOffset> poseOffsets,
            int poseYawMilliDegrees,
            List<CharacterTestDirectHitTracePreviewCapsuleFrame> output)
        {
            // The actual trace cache has no previous center on the first
            // active frame. Match that behavior and only add sweeps when a
            // contiguous preview pose supplies a prior center.
            CharacterTestDirectHitTraceSourcePoseOffset currentOffset;
            CharacterTestDirectHitTraceSourcePoseOffset previousOffset;
            if (!TryFindPoseOffset(poseOffsets, localFrame, out currentOffset)
                || !TryFindPoseOffset(poseOffsets, localFrame - 1, out previousOffset))
            {
                return true;
            }

            CharacterTestDirectHitTraceSourcePose sourcePose =
                new CharacterTestDirectHitTraceSourcePose(localFrame, FixVector3.Zero, 0);
            FixVector3 bodyCenter = new FixVector3(
                Fix64.Zero,
                physics.GetCharacterCenterOffsetY(source),
                Fix64.Zero);
            if (!CharacterTestDirectHitTracePlanSampler.TryBuildCasterBodyCapsule(
                    sourcePose,
                    bodyCenter,
                    physics.GetCharacterHalfExtents(source),
                    evt.HitVolume,
                    out FixVector3 center,
                    out FixVector3 pointA,
                    out FixVector3 pointB,
                    out Fix64 radius,
                    out Fix64 segmentHalf))
            {
                return false;
            }

            // Both pose tracks use local offsets relative to their own
            // quantized yaw. Thus the prior center in the current sample's
            // local space is the same geometry plus the offset delta. This
            // is why current and turn plans use separate capsule lists.
            FixVector3 previousCenter = center
                + previousOffset.PositionOffsetLocal
                - currentOffset.PositionOffsetLocal;
            if (previousCenter.Equals(center))
                return true;

            FixVector3 vertical = new FixVector3(Fix64.Zero, segmentHalf, Fix64.Zero);
            if (segmentHalf > Fix64.Zero)
            {
                AppendCasterSweepCapsule(
                    output,
                    localFrame,
                    sourceOrder,
                    1,
                    actionId,
                    traceId,
                    policyEventId,
                    actionRevision,
                    targetMask,
                    previousCenter - vertical,
                    center - vertical,
                    radius);
                AppendCasterSweepCapsule(
                    output,
                    localFrame,
                    sourceOrder,
                    2,
                    actionId,
                    traceId,
                    policyEventId,
                    actionRevision,
                    targetMask,
                    previousCenter,
                    center,
                    radius);
                AppendCasterSweepCapsule(
                    output,
                    localFrame,
                    sourceOrder,
                    3,
                    actionId,
                    traceId,
                    policyEventId,
                    actionRevision,
                    targetMask,
                    previousCenter + vertical,
                    center + vertical,
                    radius);
            }
            else
            {
                AppendCasterSweepCapsule(
                    output,
                    localFrame,
                    sourceOrder,
                    1,
                    actionId,
                    traceId,
                    policyEventId,
                    actionRevision,
                    targetMask,
                    previousCenter,
                    center,
                    radius);
            }

            return true;
        }

        private static bool TryFindPoseOffset(
            IReadOnlyList<CharacterTestDirectHitTraceSourcePoseOffset> offsets,
            int localFrame,
            out CharacterTestDirectHitTraceSourcePoseOffset result)
        {
            for (int i = 0; i < offsets.Count; i++)
            {
                if (offsets[i].LocalFrame == localFrame)
                {
                    result = offsets[i];
                    return true;
                }
            }

            result = default(CharacterTestDirectHitTraceSourcePoseOffset);
            return false;
        }

        private static void AppendCasterSweepCapsule(
            List<CharacterTestDirectHitTracePreviewCapsuleFrame> output,
            int localFrame,
            int sourceOrder,
            int queryOffset,
            int actionId,
            int traceId,
            int policyEventId,
            int actionRevision,
            CombatPhysicsLayerMask targetMask,
            FixVector3 pointA,
            FixVector3 pointB,
            Fix64 radius)
        {
            output.Add(new CharacterTestDirectHitTracePreviewCapsuleFrame(
                CharacterTestDirectHitTraceSampleKind.CasterBody,
                localFrame,
                sourceOrder,
                queryOffset,
                actionId,
                traceId,
                policyEventId,
                actionRevision,
                targetMask,
                pointA,
                pointB,
                radius));
        }

        private bool TryBuildPreviewWeapon(
            CharacterActionConfig action,
            CombatTrackEvent evt,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterPhysicsRuntime physics,
            int localFrame,
            int traceIndex,
            int actionId,
            CombatPhysicsLayerMask targetMask,
            out CharacterTestDirectHitTracePreviewWeaponFrame weapon)
        {
            weapon = default(CharacterTestDirectHitTracePreviewWeaponFrame);
            if (!_runtime.CombatTraceSystem.WeaponTraceBindingResolver.TryResolve(
                    source,
                    evt.EquipmentSlotId,
                    evt.TraceProfileId,
                    out CharacterTestWeaponTraceBinding binding))
            {
                return false;
            }

            string clipKey = ResolveAnimationClipKey(action, localFrame);
            if (string.IsNullOrWhiteSpace(clipKey)
                || !_runtime.CombatTraceSystem.BakedTraceSource.TryGetFrameForEntity(
                    source,
                    clipKey,
                    binding.TraceProfileId,
                    localFrame,
                    out MxAnimationBakedWeaponTraceFrame baked))
            {
                return false;
            }

            int socketId = baked.SocketId;
            if (!_previewSocketOffsets.TryGetValue(socketId, out FixVector3 authorityOffset))
            {
                SocketOffsetReadCount++;
                authorityOffset = source.AuthorityPose != null
                    && source.AuthorityPose.TryGetSocketOffset(socketId, out FixVector3 resolvedOffset)
                    ? resolvedOffset : FixVector3.Zero;
                _previewSocketOffsets.Add(socketId, authorityOffset);
            }
            weapon = new CharacterTestDirectHitTracePreviewWeaponFrame(
                localFrame,
                traceIndex,
                binding.TraceProfileId,
                evt.StableEventId,
                actionId,
                ToFixVector3(baked.RootPrev) + authorityOffset,
                ToFixVector3(baked.TipPrev) + authorityOffset,
                ToFixVector3(baked.RootNow) + authorityOffset,
                ToFixVector3(baked.TipNow) + authorityOffset,
                binding.RadiusFix,
                targetMask,
                binding.ProximalExtensionFix,
                binding.ProximalRadiusFix);
            return weapon.Radius > Fix64.Zero;
        }

        private bool TryBuildPreviewCapsule(
            CharacterActionConfig action,
            CombatTrackEvent evt,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterPhysicsRuntime physics,
            RuntimeFrame completedFrame,
            int localFrame,
            int sourceOrder,
            int actionId,
            int traceId,
            int policyEventId,
            int actionRevision,
            CombatPhysicsLayerMask targetMask,
            out CharacterTestDirectHitTracePreviewCapsuleFrame capsule)
        {
            capsule = default(CharacterTestDirectHitTracePreviewCapsuleFrame);
            FixVector3 pointA;
            FixVector3 pointB;
            Fix64 radius;
            CharacterTestDirectHitTraceSampleKind kind;

            switch (evt.HitVolume.SourceKind)
            {
                case CharacterActionHitVolumeSourceKind.ManualCapsule:
                    pointA = new FixVector3(
                        ToFix64(evt.HitVolume.LocalPointAX),
                        ToFix64(evt.HitVolume.LocalPointAY),
                        ToFix64(evt.HitVolume.LocalPointAZ));
                    pointB = new FixVector3(
                        ToFix64(evt.HitVolume.LocalPointBX),
                        ToFix64(evt.HitVolume.LocalPointBY),
                        ToFix64(evt.HitVolume.LocalPointBZ));
                    radius = ToFix64(evt.HitVolume.Radius);
                    kind = CharacterTestDirectHitTraceSampleKind.ManualCapsule;
                    break;

                case CharacterActionHitVolumeSourceKind.BakedSocketCapsule:
                    string clipKey = ResolveAnimationClipKey(action, localFrame);
                    if (string.IsNullOrWhiteSpace(clipKey)
                        || !TryGetBakedSocketFrame(
                            source,
                            clipKey,
                            evt.HitVolume.StartSocketId,
                            localFrame,
                            out MxAnimationBakedSocketFrame start)
                        || !TryGetBakedSocketFrame(
                            source,
                            clipKey,
                            evt.HitVolume.EndSocketId,
                            localFrame,
                            out MxAnimationBakedSocketFrame end))
                    {
                        return false;
                    }
                    pointA = ToFixVector3(start.Position);
                    pointB = ToFixVector3(end.Position);
                    CharacterTestCharacterCombatTraceSystem.TryGetAuthorityPoseSocketInputs(
                        _runtime.CombatTraceSystem.BakedTraceSource,
                        source,
                        out CharacterAuthorityPoseRigRuntimeBinding authorityPoseRig,
                        out CharacterSocketBakeCatalog authorityPoseCatalog);
                    if (CharacterTestAuthorityPoseConsumerMath.TryResolveAttachmentDelta(
                            source,
                            authorityPoseRig,
                            authorityPoseCatalog,
                            CharacterAuthorityPoseRigAttachmentKind.Socket,
                            evt.HitVolume.StartSocketId,
                            clipKey,
                            localFrame,
                            loop: false,
                            completedFrame,
                            out CharacterTestAuthorityPoseAttachmentDelta startDelta))
                    {
                        pointA = startDelta.TransformPoint(pointA);
                    }
                    if (CharacterTestAuthorityPoseConsumerMath.TryResolveAttachmentDelta(
                            source,
                            authorityPoseRig,
                            authorityPoseCatalog,
                            CharacterAuthorityPoseRigAttachmentKind.Socket,
                            evt.HitVolume.EndSocketId,
                            clipKey,
                            localFrame,
                            loop: false,
                            completedFrame,
                            out CharacterTestAuthorityPoseAttachmentDelta endDelta))
                    {
                        pointB = endDelta.TransformPoint(pointB);
                    }
                    radius = ToFix64(evt.HitVolume.Radius);
                    kind = CharacterTestDirectHitTraceSampleKind.BakedSocketCapsule;
                    break;

                case CharacterActionHitVolumeSourceKind.CasterBody:
                    CharacterTestDirectHitTraceSourcePose sourcePose =
                        new CharacterTestDirectHitTraceSourcePose(localFrame, FixVector3.Zero, 0);
                    FixVector3 bodyCenter = new FixVector3(
                        Fix64.Zero,
                        physics.GetCharacterCenterOffsetY(source),
                        Fix64.Zero);
                    if (!CharacterTestDirectHitTracePlanSampler.TryBuildCasterBodyCapsule(
                            sourcePose,
                            bodyCenter,
                            physics.GetCharacterHalfExtents(source),
                            evt.HitVolume,
                            out _,
                            out pointA,
                            out pointB,
                            out radius,
                            out _))
                    {
                        return false;
                    }
                    kind = CharacterTestDirectHitTraceSampleKind.CasterBody;
                    break;

                default:
                    return false;
            }

            capsule = new CharacterTestDirectHitTracePreviewCapsuleFrame(
                kind,
                localFrame,
                sourceOrder,
                queryOffset: 0,
                actionId,
                traceId,
                policyEventId,
                actionRevision,
                targetMask,
                pointA,
                pointB,
                radius);
            return radius > Fix64.Zero;
        }

        private string ResolveAnimationClipKey(CharacterActionConfig action, int localFrame)
        {
            AnimationTrackEvent[] events = action?.AnimationTrack?.Events;
            if (events == null)
                return string.Empty;
            string key = string.Empty;
            for (int i = 0; i < events.Length; i++)
            {
                AnimationTrackEvent evt = events[i];
                if (evt.Frame <= localFrame && localFrame <= evt.EndFrame)
                    key = evt.EffectiveAnimationKey;
            }
            return key ?? string.Empty;
        }

        private bool TryGetBakedSocketFrame(
            CharacterTestCharacterEntity source,
            string clipKey,
            int socketId,
            int localFrame,
            out MxAnimationBakedSocketFrame frame)
        {
            ICharacterTestPackageBakedSocketSource packageSource =
                _runtime.CombatTraceSystem.BakedTraceSource as ICharacterTestPackageBakedSocketSource;
            if (packageSource != null)
            {
                if (source == null)
                {
                    frame = null;
                    return false;
                }

                return packageSource.TryGetSocketFrame(
                    source.Identity.GeneratedPackageRoot,
                    clipKey,
                    socketId,
                    localFrame,
                    out frame);
            }

            return _runtime.CombatTraceSystem.BakedTraceSource.TryGetSocketFrame(
                clipKey,
                socketId,
                localFrame,
                out frame);
        }

        private CharacterTestAttackOpportunitySamplePolicySnapshot GetOrBuildPolicies(
            CharacterActionConfig action,
            CharacterTestCharacterEntity source,
            CharacterTestAttackEvaluationSnapshot snapshot,
            CharacterTestDirectHitTracePlanView plan)
        {
            CharacterTestCharacterEntity target = _lookup.FindEntityByCombatId(snapshot.TargetEntityId);
            if (target == null)
                return null;
            HitTargetStateFlags targetState =
                CharacterTestCombatHitEventResolver.ResolveTargetState(snapshot.TargetEntityId, _lookup);
            PreparePolicyEvents(action, source);
            var key = new PolicyCacheKey(
                snapshot.SourceEntityId.Value,
                snapshot.TargetEntityId.Value,
                snapshot.ResolvedActionRevision,
                source.Team.TeamId,
                target.Team.TeamId,
                targetState);
            if (_policyCache.TryGetValue(key, out CharacterTestAttackOpportunitySamplePolicySnapshot cached)
                && cached != null
                && cached.IsSealed
                && cached.Count == plan.Count)
            {
                bool matches = true;
                for (int i = 0; i < plan.Count; i++)
                {
                    if (!plan.TryGetSample(i, out CharacterTestDirectHitTracePlanSample sample)
                        || !cached.TryGet(sample, out CharacterTestAttackOpportunitySamplePolicy policy)
                        || !TryResolvePolicyEvent(action, source, sample, out CombatTrackEvent liveEvent)
                        || !policy.HitEvent.Equals(liveEvent.HitEvent)
                        || !policy.DamagePayload.Equals(liveEvent.DamagePayload)
                        || !policy.IsBoundTo(sample, snapshot)
                        || policy.SourceTeamId != source.Team.TeamId
                        || policy.TargetTeamId != target.Team.TeamId
                        || policy.TargetState != targetState)
                    {
                        matches = false;
                        break;
                    }
                }
                if (matches)
                    return cached;
            }

            var policies = new CharacterTestAttackOpportunitySamplePolicySnapshot(plan.Count);
            for (int i = 0; i < plan.Count; i++)
            {
                if (!plan.TryGetSample(i, out CharacterTestDirectHitTracePlanSample sample))
                    return null;
                if (!TryResolvePolicyEvent(action, source, sample, out CombatTrackEvent evt))
                    return null;
                policies.TryAdd(new CharacterTestAttackOpportunitySamplePolicy(
                    CharacterTestAttackOpportunitySampleIdentity.From(sample),
                    snapshot.SourceEntityId,
                    snapshot.TargetEntityId,
                    source.Team.TeamId,
                    target.Team.TeamId,
                    CharacterTestCombatHitEventResolver.ResolveTargetState(snapshot.TargetEntityId, _lookup),
                    evt.HitEvent,
                    evt.DamagePayload));
            }
            policies.Seal();
            _policyCache[key] = policies;
            return policies;
        }

        private readonly struct PreparedPolicyEvent
        {
            internal readonly CombatTrackEvent Event;
            internal readonly int Next;
            internal readonly bool WeaponEligible;
            internal PreparedPolicyEvent(CombatTrackEvent evt, int next, bool weaponEligible)
            { Event = evt; Next = next; WeaponEligible = weaponEligible; }
        }

        // Bind expensive event ids and equipment once per preparation, not once per sample.
        // Read live events here so in-place content edits preserve the previous rejection semantics.
        private void PreparePolicyEvents(CharacterActionConfig action, CharacterTestCharacterEntity source)
        {
            _policyEventHeads.Clear();
            _preparedPolicyEvents.Clear();
            var events = action.CombatTrack?.Events;
            if (events == null) return;
            for (int i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Kind != CharacterActionTrackEventKind.HitTraceWindow) continue;
                PolicyEventPreparationCount++;
                int id = evt.StableEventId;
                int next = _policyEventHeads.TryGetValue(id, out int previous) ? previous : -1;
                bool eligible = _runtime.CombatTraceSystem.WeaponTraceBindingResolver.TryResolve(source, evt.EquipmentSlotId,
                        evt.TraceProfileId, out CharacterTestWeaponTraceBinding binding)
                    && binding.TraceProfileId > 0;
                _policyEventHeads[id] = _preparedPolicyEvents.Count;
                _preparedPolicyEvents.Add(new PreparedPolicyEvent(evt, next, eligible));
            }
        }

        private bool TryResolvePolicyEvent(CharacterActionConfig action, CharacterTestCharacterEntity source,
            CharacterTestDirectHitTracePlanSample sample, out CombatTrackEvent resolved)
        {
            resolved = default;
            if (!_policyEventHeads.TryGetValue(sample.PolicyEventId, out int index)) return false;
            bool volume = sample.Kind == CharacterTestDirectHitTraceSampleKind.ManualCapsule
                || sample.Kind == CharacterTestDirectHitTraceSampleKind.CasterBody
                || sample.Kind == CharacterTestDirectHitTraceSampleKind.BakedSocketCapsule;
            int matches = 0;
            while (index >= 0)
            {
                var prepared = _preparedPolicyEvents[index];
                var evt = prepared.Event;
                if (sample.LocalFrame >= evt.Frame && sample.LocalFrame <= evt.EndFrame && (volume || prepared.WeaponEligible))
                { resolved = evt; matches++; }
                index = prepared.Next;
            }
            return matches == 1;
        }

        private bool FailClosed(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            RuntimeFrame plannerFrame,
            CharacterTestAttackOpportunityFailureReason reason,
            out CharacterTestAttackOpportunityResult result,
            bool commitState = true)
        {
            if (commitState && source != null)
                _engagementSelectionsBySource.Remove(source.Id.Value);
            CharacterTestOpportunityFailure failure = reason
                == CharacterTestAttackOpportunityFailureReason.StaleSnapshot
                ? CharacterTestOpportunityFailure.Stale(
                    reason,
                    CharacterTestAttackOpportunityFailureFlags.SnapshotMismatch)
                : CharacterTestOpportunityFailure.Unavailable(
                    reason,
                    GetFailureFlags(reason));
            result = CharacterTestAttackOpportunityResult.Failure(
                failure,
                CharacterTestAttackEvaluationMode.CurrentPose);
            CharacterTestAttackOpportunityStatus status = reason
                == CharacterTestAttackOpportunityFailureReason.StaleSnapshot
                ? CharacterTestAttackOpportunityStatus.StaleSnapshot
                : CharacterTestAttackOpportunityStatus.Unavailable;
            if (commitState)
            {
                SetEvidence(
                    source != null ? source.Id.Value : 0,
                    target != null ? target.Id.Value : 0,
                    plannerFrame,
                    CharacterTestRuntimeAiPlannerAttackDecisionSource.AttackOpportunity,
                    status,
                    CharacterTestAttackOpportunityLevel.NoOpportunity,
                    reason,
                    0);
                CacheResult(source, target, plannerFrame, result);
            }
            return false;
        }

        private static CharacterTestAttackOpportunityFailureFlags GetFailureFlags(
            CharacterTestAttackOpportunityFailureReason reason)
        {
            switch (reason)
            {
                case CharacterTestAttackOpportunityFailureReason.SnapshotUnavailable:
                    return CharacterTestAttackOpportunityFailureFlags.SnapshotUnavailable;
                case CharacterTestAttackOpportunityFailureReason.StaleSnapshot:
                    return CharacterTestAttackOpportunityFailureFlags.SnapshotMismatch;
                case CharacterTestAttackOpportunityFailureReason.TraceEvaluatorUnsupported:
                    return CharacterTestAttackOpportunityFailureFlags.TraceEvaluatorUnsupported;
                case CharacterTestAttackOpportunityFailureReason.TracePlanUnavailable:
                    return CharacterTestAttackOpportunityFailureFlags.TracePlanUnavailable;
                case CharacterTestAttackOpportunityFailureReason.TracePlanInvalid:
                    return CharacterTestAttackOpportunityFailureFlags.TracePlanInvalid;
                case CharacterTestAttackOpportunityFailureReason.NoCompatibleHurtbox:
                    return CharacterTestAttackOpportunityFailureFlags.NoCompatibleHurtbox;
                case CharacterTestAttackOpportunityFailureReason.TargetNotEligible:
                    return CharacterTestAttackOpportunityFailureFlags.TargetNotEligible;
                case CharacterTestAttackOpportunityFailureReason.TargetStateRejected:
                    return CharacterTestAttackOpportunityFailureFlags.TargetStateRejected;
                case CharacterTestAttackOpportunityFailureReason.BlockedByEnvironment:
                    return CharacterTestAttackOpportunityFailureFlags.BlockedByEnvironment;
                case CharacterTestAttackOpportunityFailureReason.NoAcceptedHit:
                    return CharacterTestAttackOpportunityFailureFlags.NoAcceptedHit;
                case CharacterTestAttackOpportunityFailureReason.CandidatePoseUnsupported:
                    return CharacterTestAttackOpportunityFailureFlags.CandidatePoseUnsupported;
                case CharacterTestAttackOpportunityFailureReason.NonTargetActorStateUnavailable:
                    return CharacterTestAttackOpportunityFailureFlags.NonTargetActorStateUnavailable;
                case CharacterTestAttackOpportunityFailureReason.DamagePayloadUnavailable:
                    return CharacterTestAttackOpportunityFailureFlags.DamagePayloadUnavailable;
                case CharacterTestAttackOpportunityFailureReason.DamagePayloadInvalid:
                    return CharacterTestAttackOpportunityFailureFlags.DamagePayloadInvalid;
                case CharacterTestAttackOpportunityFailureReason.PartStateDependencyRejected:
                    return CharacterTestAttackOpportunityFailureFlags.PartStateDependencyRejected;
                default:
                    return CharacterTestAttackOpportunityFailureFlags.SnapshotUnavailable;
            }
        }

        internal bool TryGetEvidence(
            int sourceEntityId,
            out CharacterTestAttackOpportunityDecisionEvidence evidence)
        {
            return _evidenceBySource.TryGetValue(sourceEntityId, out evidence);
        }

        private void SetEvidence(
            int sourceEntityId,
            int targetEntityId,
            RuntimeFrame plannerFrame,
            CharacterTestRuntimeAiPlannerAttackDecisionSource source,
            CharacterTestAttackOpportunityStatus status,
            CharacterTestAttackOpportunityLevel level,
            CharacterTestAttackOpportunityFailureReason failure,
            int requiredYawDeltaMilliDegrees)
        {
            _lastEvidence.Set(
                sourceEntityId,
                targetEntityId,
                plannerFrame,
                source,
                status,
                level,
                failure,
                requiredYawDeltaMilliDegrees);
            if (!_evidenceBySource.TryGetValue(
                    sourceEntityId,
                    out CharacterTestAttackOpportunityDecisionEvidence evidence))
            {
                evidence = new CharacterTestAttackOpportunityDecisionEvidence();
                _evidenceBySource.Add(sourceEntityId, evidence);
            }
            evidence.Set(
                sourceEntityId,
                targetEntityId,
                plannerFrame,
                source,
                status,
                level,
                failure,
                requiredYawDeltaMilliDegrees);
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static FixVector3 ToFixVector3(float x, float y, float z)
        {
            return new FixVector3(ToFix64(x), ToFix64(y), ToFix64(z));
        }

        private static FixVector3 ToFixVector3(MxAnimationBakedVector3 value)
        {
            return new FixVector3(
                Fix64.FromRaw(value.X * Fix64.Scale / 1000000L),
                Fix64.FromRaw(value.Y * Fix64.Scale / 1000000L),
                Fix64.FromRaw(value.Z * Fix64.Scale / 1000000L));
        }

        private static int ComputeActionSetBindingSignature(CharacterActionSetConfig actionSet)
        {
            if (actionSet == null)
                return 0;

            unchecked
            {
                int hash = actionSet.Id;
                hash = hash * 397 ^ actionSet.StableLabelContentWitness.GetHashCode();
                hash = hash * 397 ^ actionSet.DefaultActionId.GetHashCode();
                hash = hash * 397 ^ actionSet.DefaultActionSlotMask;
                CharacterActionBinding[] bindings = actionSet.CommandBindings;
                for (int i = 0; i < bindings.Length; i++)
                {
                    CharacterActionBinding binding = bindings[i];
                    if (binding == null)
                        continue;
                    hash = hash * 397 ^ binding.IntentId;
                    hash = hash * 397 ^ binding.ActionId.GetHashCode();
                    hash = hash * 397 ^ binding.Priority;
                    hash = hash * 397 ^ (binding.AllowQueue ? 1 : 0);
                    hash = hash * 397 ^ binding.QueueWindowFrames;
                }

                CharacterActionCommandSequence[] sequences = actionSet.CommandSequences;
                for (int i = 0; i < sequences.Length; i++)
                {
                    CharacterActionCommandSequence sequence = sequences[i];
                    if (sequence == null)
                        continue;
                    hash = hash * 397 ^ sequence.IntentId;
                    hash = hash * 397 ^ sequence.Priority;
                    hash = hash * 397 ^ (sequence.AllowQueue ? 1 : 0);
                    hash = hash * 397 ^ sequence.QueueWindowFrames;
                    CharacterActionId[] actionIds = sequence.ActionIds;
                    for (int j = 0; j < actionIds.Length; j++)
                        hash = hash * 397 ^ actionIds[j].GetHashCode();
                }

                return hash;
            }
        }

        private readonly struct CharacterTestEngagementSelectionState
        {
            public CharacterTestEngagementSelectionState(
                RuntimeFrame plannerFrame,
                CharacterTestEngagementCandidateResult selection)
            {
                PlannerFrame = plannerFrame;
                Selection = selection;
            }

            public RuntimeFrame PlannerFrame { get; }
            public CharacterTestEngagementCandidateResult Selection { get; }
        }

        private readonly struct CachedDecision
        {
            public CachedDecision(
                RuntimeFrame plannerFrame,
                CharacterTestCharacterEntity target,
                CharacterTestAttackOpportunityResult result,
                int characterStableId,
                string equipmentStateStableId,
                CharacterActionSetConfig actionSet,
                int actionSetBindingSignature,
                int worldRevision,
                int targetHurtboxRevision,
                int sourcePoseRevision,
                int sourceFacingRevision,
                int sourceXMillimeters,
                int sourceYMillimeters,
                int sourceZMillimeters,
                int sourceYawMilliDegrees)
            {
                PlannerFrame = plannerFrame;
                Target = target;
                Result = result;
                CharacterStableId = characterStableId;
                EquipmentStateStableId = equipmentStateStableId ?? string.Empty;
                ActionSet = actionSet;
                ActionSetBindingSignature = actionSetBindingSignature;
                WorldRevision = worldRevision;
                TargetHurtboxRevision = targetHurtboxRevision;
                SourcePoseRevision = sourcePoseRevision;
                SourceFacingRevision = sourceFacingRevision;
                SourceXMillimeters = sourceXMillimeters;
                SourceYMillimeters = sourceYMillimeters;
                SourceZMillimeters = sourceZMillimeters;
                SourceYawMilliDegrees = sourceYawMilliDegrees;
            }

            public RuntimeFrame PlannerFrame { get; }
            public CharacterTestCharacterEntity Target { get; }
            public CharacterTestAttackOpportunityResult Result { get; }
            private int CharacterStableId { get; }
            private string EquipmentStateStableId { get; }
            private CharacterActionSetConfig ActionSet { get; }
            private int ActionSetBindingSignature { get; }
            private int WorldRevision { get; }
            private int TargetHurtboxRevision { get; }
            private int SourcePoseRevision { get; }
            private int SourceFacingRevision { get; }
            private int SourceXMillimeters { get; }
            private int SourceYMillimeters { get; }
            private int SourceZMillimeters { get; }
            private int SourceYawMilliDegrees { get; }

            public bool Matches(
                CharacterTestCharacterEntity source,
                CharacterTestCharacterActionRuntime actionRuntime,
                CharacterTestCharacterRuntimeModule runtime)
            {
                if (source == null || actionRuntime == null
                    || !(
                        CharacterStableId == source.Identity.CharacterId)
                    || !string.Equals(
                        EquipmentStateStableId,
                        source.Equipment.EquipmentStateStableId ?? string.Empty,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                if (source.Transform.XMillimeters != SourceXMillimeters
                    || source.Transform.YMillimeters != SourceYMillimeters
                    || source.Transform.ZMillimeters != SourceZMillimeters
                    || source.Transform.YawMilliDegrees != SourceYawMilliDegrees
                    || runtime == null
                    || runtime.PhysicsRuntime?.World == null
                    || runtime.PhysicsRuntime.World.Revision != WorldRevision
                    || !MatchesSourceRevisions(source)
                    || !runtime.TryGetDamageGeometrySnapshot(
                        Target?.Id.Value ?? 0,
                        out CharacterTestDamageGeometrySnapshot geometry)
                    || geometry == null
                    || !geometry.IsValid
                    || geometry.HurtboxRevision != TargetHurtboxRevision)
                {
                    return false;
                }

                CharacterActionSetConfig currentActionSet =
                    actionRuntime.TryGetEntityActionSetNoAlloc(
                        source.Id,
                        out CharacterActionSetConfig resolvedActionSet)
                        ? resolvedActionSet
                        : null;
                return ReferenceEquals(ActionSet, currentActionSet)
                    && ActionSetBindingSignature == ComputeActionSetBindingSignature(currentActionSet);
            }

            private bool MatchesSourceRevisions(CharacterTestCharacterEntity source)
            {
                if (SourcePoseRevision < 0 && SourceFacingRevision < 0)
                    return true;

                // Frame numbers describe when the witness was captured; they
                // are not source-pose identity. The independent write revisions
                // catch same-frame root/yaw and AuthorityPose/socket changes,
                // including changes that leave the physics world revision
                // untouched.
                return (SourcePoseRevision < 0
                        || SourcePoseRevision == source.SourcePoseRevision)
                    && (SourceFacingRevision < 0
                        || SourceFacingRevision == source.SourceFacingRevision);
            }
        }

        private readonly struct CachedIntentResolution
        {
            public CachedIntentResolution(
                CharacterActionResolveResult result,
                int characterStableId,
                string equipmentStateStableId,
                CharacterActionSetConfig actionSet,
                int actionSetBindingSignature)
            {
                Result = result;
                CharacterStableId = characterStableId;
                EquipmentStateStableId = equipmentStateStableId ?? string.Empty;
                ActionSet = actionSet;
                ActionSetStableId = actionSet?.StableId ?? string.Empty;
                ActionSetBindingSignature = actionSetBindingSignature;
            }

            public CharacterActionResolveResult Result { get; }
            private int CharacterStableId { get; }
            private string EquipmentStateStableId { get; }
            private CharacterActionSetConfig ActionSet { get; }
            private string ActionSetStableId { get; }
            private int ActionSetBindingSignature { get; }

            public bool Matches(
                int characterStableId,
                string equipmentStateStableId,
                CharacterActionSetConfig actionSet,
                int actionSetBindingSignature)
            {
                return (CharacterStableId == characterStableId)
                    && string.Equals(
                        EquipmentStateStableId,
                        equipmentStateStableId,
                        StringComparison.Ordinal)
                    && ReferenceEquals(ActionSet, actionSet)
                    && string.Equals(
                        ActionSetStableId,
                        actionSet?.StableId ?? string.Empty,
                        StringComparison.Ordinal)
                    && ActionSetBindingSignature == actionSetBindingSignature;
            }
        }

        private readonly struct PolicyCacheKey : IEquatable<PolicyCacheKey>
        {
            public PolicyCacheKey(
                int sourceEntityId,
                int targetEntityId,
                int actionRevision,
                int sourceTeamId,
                int targetTeamId,
                HitTargetStateFlags targetState)
            {
                SourceEntityId = sourceEntityId;
                TargetEntityId = targetEntityId;
                ActionRevision = actionRevision;
                SourceTeamId = sourceTeamId;
                TargetTeamId = targetTeamId;
                TargetState = targetState;
            }

            public int SourceEntityId { get; }
            public int TargetEntityId { get; }
            public int ActionRevision { get; }
            public int SourceTeamId { get; }
            public int TargetTeamId { get; }
            public HitTargetStateFlags TargetState { get; }

            public bool Equals(PolicyCacheKey other)
            {
                return SourceEntityId == other.SourceEntityId
                    && TargetEntityId == other.TargetEntityId
                    && ActionRevision == other.ActionRevision
                    && SourceTeamId == other.SourceTeamId
                    && TargetTeamId == other.TargetTeamId
                    && TargetState == other.TargetState;
            }

            public override bool Equals(object obj)
            {
                return obj is PolicyCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = SourceEntityId;
                    hash = hash * 397 ^ TargetEntityId;
                    hash = hash * 397 ^ ActionRevision;
                    hash = hash * 397 ^ SourceTeamId;
                    hash = hash * 397 ^ TargetTeamId;
                    hash = hash * 397 ^ (int)TargetState;
                    return hash;
                }
            }
        }
    }

    internal readonly struct CharacterTestEngagementDiagnosticState
    {
        internal CharacterTestEngagementDiagnosticState(
            int sourceEntityId,
            int targetEntityId,
            int resolvedActionRevision,
            RuntimeFrame plannerFrame,
            CharacterTestEngagementCandidateStatus status,
            CharacterTestEngagementCandidateResult selection)
        {
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            ResolvedActionRevision = resolvedActionRevision;
            PlannerFrame = plannerFrame;
            Status = status;
            Selection = selection;
        }

        internal int SourceEntityId { get; }
        internal int TargetEntityId { get; }
        internal int ResolvedActionRevision { get; }
        internal RuntimeFrame PlannerFrame { get; }
        internal CharacterTestEngagementCandidateStatus Status { get; }
        internal CharacterTestEngagementCandidateResult Selection { get; }
    }

    /// <summary>Read-only, allocation-boundary projection for the Game View debug overlay.</summary>
    internal sealed class CharacterTestEngagementDiagnosticSnapshot
    {
        internal int SourceEntityId { get; set; }
        internal int TargetEntityId { get; set; }
        internal int ResolvedActionRevision { get; set; }
        internal long PlannerFrame { get; set; }
        internal CharacterTestEngagementCandidateStatus Status { get; set; }
        internal int ZoneId { get; set; } = 0;
        internal int AnchorId { get; set; } = 0;
        internal int CandidateXMillimeters { get; set; }
        internal int CandidateYMillimeters { get; set; }
        internal int CandidateZMillimeters { get; set; }
        internal int CandidateYawMilliDegrees { get; set; }
        internal long PathCostMillimeters { get; set; }
        internal bool HasNextWaypoint { get; set; }
        internal int NextWaypointXMillimeters { get; set; }
        internal int NextWaypointYMillimeters { get; set; }
        internal int NextWaypointZMillimeters { get; set; }
        internal int ReservationCount { get; set; }
        internal int Capacity { get; set; }
        internal int CapacitySlot { get; set; } = -1;
        internal bool HasReservation { get; set; }

        internal static CharacterTestEngagementDiagnosticSnapshot From(
            CharacterTestEngagementDiagnosticState state,
            CharacterTestEngagementReservation reservation)
        {
            CharacterTestEngagementCandidateResult selection = state.Selection;
            CharacterTestEngagementCandidatePose pose = selection.Pose;
            CharacterTestPathfindingSample path = selection.Path;
            bool hasSelection = selection.Status == CharacterTestEngagementCandidateStatus.Success;
            bool hasReservation = reservation.IsActive;
            return new CharacterTestEngagementDiagnosticSnapshot
            {
                SourceEntityId = state.SourceEntityId,
                TargetEntityId = state.TargetEntityId,
                ResolvedActionRevision = state.ResolvedActionRevision,
                PlannerFrame = state.PlannerFrame.Value,
                Status = state.Status,
                ZoneId = hasSelection ? pose.ZoneId : 0,
                AnchorId = hasSelection ? pose.AnchorId : 0,
                CandidateXMillimeters = hasSelection
                    ? CharacterTestDeterministicUnits.Fix64MetersToMillimeters(pose.WorldPosition.X)
                    : 0,
                CandidateYMillimeters = hasSelection
                    ? CharacterTestDeterministicUnits.Fix64MetersToMillimeters(pose.WorldPosition.Y)
                    : 0,
                CandidateZMillimeters = hasSelection
                    ? CharacterTestDeterministicUnits.Fix64MetersToMillimeters(pose.WorldPosition.Z)
                    : 0,
                CandidateYawMilliDegrees = hasSelection ? pose.WorldYawMilliDegrees : 0,
                PathCostMillimeters = hasSelection ? path.TotalCostMillimeters : 0,
                HasNextWaypoint = hasSelection && path.HasNextWaypoint,
                NextWaypointXMillimeters = hasSelection && path.HasNextWaypoint
                    ? CharacterTestDeterministicUnits.Fix64MetersToMillimeters(path.NextWaypoint.X)
                    : 0,
                NextWaypointYMillimeters = hasSelection && path.HasNextWaypoint
                    ? CharacterTestDeterministicUnits.Fix64MetersToMillimeters(path.NextWaypoint.Y)
                    : 0,
                NextWaypointZMillimeters = hasSelection && path.HasNextWaypoint
                    ? CharacterTestDeterministicUnits.Fix64MetersToMillimeters(path.NextWaypoint.Z)
                    : 0,
                ReservationCount = hasSelection ? selection.ReservationCount + (hasReservation ? 1 : 0) : 0,
                Capacity = hasSelection ? selection.Capacity : 0,
                CapacitySlot = hasReservation ? reservation.CapacitySlot : -1,
                HasReservation = hasReservation
            };
        }
    }

    /// <summary>Stable, no-string decision evidence for Runtime AI diagnostics.</summary>
    internal sealed class CharacterTestAttackOpportunityDecisionEvidence
    {
        internal void Set(
            int sourceEntityId,
            int targetEntityId,
            RuntimeFrame plannerFrame,
            CharacterTestRuntimeAiPlannerAttackDecisionSource source,
            CharacterTestAttackOpportunityStatus status,
            CharacterTestAttackOpportunityLevel level,
            CharacterTestAttackOpportunityFailureReason failure,
            int requiredYawDeltaMilliDegrees)
        {
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            PlannerFrame = plannerFrame;
            Source = source;
            Status = status;
            Level = level;
            FailureReason = failure;
            RequiredYawDeltaMilliDegrees = requiredYawDeltaMilliDegrees;
        }

        internal int SourceEntityId { get; private set; }
        internal int TargetEntityId { get; private set; }
        internal RuntimeFrame PlannerFrame { get; private set; }
        internal CharacterTestRuntimeAiPlannerAttackDecisionSource Source { get; private set; }
        internal CharacterTestAttackOpportunityStatus Status { get; private set; }
        internal CharacterTestAttackOpportunityLevel Level { get; private set; }
        internal CharacterTestAttackOpportunityFailureReason FailureReason { get; private set; }
        internal int RequiredYawDeltaMilliDegrees { get; private set; }
    }
}

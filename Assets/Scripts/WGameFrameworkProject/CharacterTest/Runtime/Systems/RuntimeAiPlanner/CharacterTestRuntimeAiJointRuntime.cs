using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiJointDirective
    {
        internal CharacterTestRuntimeAiJointDirective(
            bool evaluated,
            bool hasSelection,
            int commandPlannerActionId,
            CharacterTestRuntimeAiJointSelectionCandidate selection,
            CharacterTestRuntimeAiJointSelectorDiagnostics diagnostics,
            CharacterTestRuntimeAiJointPreparedAttack preparedAttack)
        {
            Evaluated = evaluated;
            HasSelection = hasSelection;
            CommandPlannerActionId = commandPlannerActionId;
            Selection = selection;
            Diagnostics = diagnostics;
            PreparedAttack = preparedAttack;
        }

        internal bool Evaluated { get; }
        internal bool HasSelection { get; }
        internal int CommandPlannerActionId { get; }
        internal CharacterTestRuntimeAiJointSelectionCandidate Selection { get; }
        internal CharacterTestRuntimeAiJointSelectorDiagnostics Diagnostics { get; }
        internal CharacterTestRuntimeAiJointPreparedAttack PreparedAttack { get; }
        internal int TargetEntityId => HasSelection ? Selection.TargetEntityId : 0;
        internal CharacterTestAttackOpportunityResult Opportunity =>
            HasSelection ? Selection.Evaluation.Opportunity : default;
        internal bool IsCompatibility => HasSelection && Selection.Seed.IsCompatibility;
    }

    internal readonly struct CharacterTestRuntimeAiJointDiagnosticSnapshot
    {
        internal CharacterTestRuntimeAiJointDiagnosticSnapshot(
            int plannerFrame,
            int sourceEntityId,
            CharacterTestRuntimeAiJointDirective directive,
            int candidatePosePreviewCount,
            int pathQueryCount,
            CharacterTestAttackOpportunityFailureReason preparationFailureReason)
        {
            PlannerFrame = plannerFrame;
            SourceEntityId = sourceEntityId;
            HasSelection = directive.HasSelection;
            TargetEntityId = directive.TargetEntityId;
            PlannerActionId = directive.CommandPlannerActionId;
            ResolvedActionId = directive.HasSelection
                ? directive.Selection.ResolvedActionId
                : CharacterActionId.None;
            ZoneId = directive.HasSelection ? directive.Selection.ZoneId : 0;
            AnchorId = directive.HasSelection ? directive.Selection.AnchorId : 0;
            Status = directive.HasSelection
                ? directive.Selection.Evaluation.Status
                : CharacterTestRuntimeAiJointEvaluationStatus.Unselectable;
            CurrentPose = directive.HasSelection && directive.Selection.Evaluation.CurrentPose;
            TurnInPlace = directive.HasSelection && directive.Selection.Evaluation.TurnInPlace;
            ApproachPose = directive.HasSelection && directive.Selection.Evaluation.ApproachPose;
            PathCostMillimeters = directive.HasSelection
                ? directive.Selection.Evaluation.PathCostMillimeters
                : int.MaxValue;
            EarliestHitTick = directive.HasSelection
                ? directive.Selection.Evaluation.EarliestHitTick
                : int.MaxValue;
            IsCompatibility = directive.IsCompatibility;
            ActionUtilityMilli = directive.HasSelection
                ? directive.Selection.Seed.Action.ActionUtilityMilli
                : 0;
            ZonePriority = directive.HasSelection
                ? directive.Selection.Seed.ZonePriority
                : 0;
            FailureReason = directive.HasSelection
                ? CharacterTestAttackOpportunityFailureReason.None
                : preparationFailureReason != CharacterTestAttackOpportunityFailureReason.None
                    ? preparationFailureReason
                    : directive.Diagnostics.Generation.ReadyActionCount == 0
                        ? CharacterTestAttackOpportunityFailureReason.TraceEvaluatorUnsupported
                        : CharacterTestAttackOpportunityFailureReason.NoCompatibleHurtbox;
            Generation = directive.Diagnostics.Generation;
            EvaluationCount = directive.Diagnostics.EvaluationCount;
            TracePlanBuildCount = directive.Diagnostics.TracePlanBuildCount;
            ResultCacheHitCount = directive.Diagnostics.ResultCacheHitCount;
            CandidatePosePreviewCount = candidatePosePreviewCount;
            PathQueryCount = pathQueryCount;
            TruncationPhase = directive.Diagnostics.TruncationPhase;
            TruncationObservedCount = directive.Diagnostics.TruncationObservedCount;
            TruncationAcceptedCount = directive.Diagnostics.TruncationAcceptedCount;
            TruncationLimit = directive.Diagnostics.TruncationLimit;
        }

        internal int PlannerFrame { get; }
        internal int SourceEntityId { get; }
        internal bool HasSelection { get; }
        internal int TargetEntityId { get; }
        internal int PlannerActionId { get; }
        internal CharacterActionId ResolvedActionId { get; }
        internal int ZoneId { get; }
        internal int AnchorId { get; }
        internal CharacterTestRuntimeAiJointEvaluationStatus Status { get; }
        internal bool CurrentPose { get; }
        internal bool TurnInPlace { get; }
        internal bool ApproachPose { get; }
        internal int PathCostMillimeters { get; }
        internal int EarliestHitTick { get; }
        internal bool IsCompatibility { get; }
        internal int ActionUtilityMilli { get; }
        internal int ZonePriority { get; }
        internal CharacterTestAttackOpportunityFailureReason FailureReason { get; }
        internal CharacterTestRuntimeAiJointGenerationDiagnostics Generation { get; }
        internal int EvaluationCount { get; }
        internal int TracePlanBuildCount { get; }
        internal int ResultCacheHitCount { get; }
        internal int CandidatePosePreviewCount { get; }
        internal int PathQueryCount { get; }
        internal CharacterTestRuntimeAiJointTruncationPhase TruncationPhase { get; }
        internal int TruncationObservedCount { get; }
        internal int TruncationAcceptedCount { get; }
        internal int TruncationLimit { get; }
    }

    /// <summary>
    /// Runtime adapter from live CharacterTest snapshots to the bounded T1/T2
    /// generator/selector. All buffers are fixed and selection is recomputed
    /// every planner tick; only existing command/Zone/reservation authority is committed.
    /// </summary>
    internal sealed class CharacterTestRuntimeAiJointRuntime :
        ICharacterTestRuntimeAiJointEvaluationSource
    {
        private const int WorldExtentMillimeters = 100000000;
        private readonly CharacterTestCharacterRuntimeModule _runtime;
        private readonly CharacterTestNavigationSession _navigation;
        private readonly CharacterTestRuntimeAiPlannerAttackOpportunityAdapter _adapter;
        private readonly CharacterTestRuntimeAiJointCandidateGenerator _generator =
            new CharacterTestRuntimeAiJointCandidateGenerator();
        private readonly CharacterTestRuntimeAiJointSelector _selector =
            new CharacterTestRuntimeAiJointSelector();
        private readonly CharacterTestRuntimeAiJointGenerationBudget _budget =
            new CharacterTestRuntimeAiJointGenerationBudget().Normalize();
        private readonly CharacterTestRuntimeAiJointActorInput[] _actors =
            new CharacterTestRuntimeAiJointActorInput[
                CharacterTestRuntimeAiJointGenerationBudget.HardActorScanLimit];
        private readonly CharacterTestRuntimeAiJointActionInput[] _actions =
            new CharacterTestRuntimeAiJointActionInput[
                CharacterTestRuntimeAiJointGenerationBudget.HardReadyActionLimit];
        private readonly CharacterTestRuntimeAiJointZoneInput[] _zones =
            new CharacterTestRuntimeAiJointZoneInput[
                CharacterTestRuntimeAiJointGenerationBudget.HardActionZoneBroadphaseLimit];
        private readonly CharacterTestRuntimeAiJointDiagnosticSnapshot[] _diagnostics =
            new CharacterTestRuntimeAiJointDiagnosticSnapshot[
                CharacterTestRuntimeAiJointGenerationBudget.HardActorScanLimit];
        private int _diagnosticCount;
        private int _candidatePosePreviewCount;
        private int _pathQueryCount;
        private CharacterTestRuntimeAiJointTruncationPhase _evaluationTruncationPhase;
        private int _evaluationTruncationObservedCount;
        private int _evaluationTruncationAcceptedCount;
        private int _evaluationTruncationLimit;
        private CharacterTestAttackOpportunityFailureReason _lastPreparationFailureReason;
        private CharacterTestCharacterEntity _source;
        private RuntimeFrame _plannerFrame;

        internal CharacterTestRuntimeAiJointRuntime(
            CharacterTestCharacterRuntimeModule runtime,
            CharacterTestNavigationSession navigation,
            CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter)
        {
            _runtime = runtime;
            _navigation = navigation;
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        internal void BeginFrame()
        {
            _selector.BeginFrame();
            _diagnosticCount = 0;
        }

        internal void ResetForRollbackReplay()
        {
            _selector.ResetForRollbackReplay();
            _source = null;
            _plannerFrame = default;
            _diagnosticCount = 0;
        }

        /// <remarks>NoAlloc after warm-up.</remarks>
        internal CharacterTestRuntimeAiJointDirective Select(
            CharacterTestCharacterEntity source,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestRuntimeAiPlannerCompiledProfile profile,
            RuntimeFrame plannerFrame)
        {
            _source = source;
            _plannerFrame = plannerFrame;
            _candidatePosePreviewCount = 0;
            _pathQueryCount = 0;
            _evaluationTruncationPhase = CharacterTestRuntimeAiJointTruncationPhase.None;
            _evaluationTruncationObservedCount = 0;
            _evaluationTruncationAcceptedCount = 0;
            _evaluationTruncationLimit = 0;
            _lastPreparationFailureReason = CharacterTestAttackOpportunityFailureReason.None;
            if (source == null || entities == null || profile == null || _runtime == null)
                return new CharacterTestRuntimeAiJointDirective(true, false, 0, default, default, default);

            // Topology invalidation precedes candidate generation so stale
            // capacity never influences the joint reducer in this frame.
            _adapter.ReservationAuthority.SweepInvalid(
                _runtime,
                _navigation,
                plannerFrame);

            int actorCount = CollectActors(source, entities);
            int actionCount = CollectActions(source, profile, plannerFrame);
            int zoneCount = CollectZones(source, actorCount);
            CharacterTestRuntimeAiJointGenerationDiagnostics generation = _generator.Generate(
                source.Id.Value,
                source.Team.TeamId,
                _actors,
                actorCount,
                _actions,
                actionCount,
                _zones,
                zoneCount,
                _budget);
            CharacterTestRuntimeAiJointSelectionResult selected = _selector.Select(
                _generator,
                this,
                generation,
                out CharacterTestRuntimeAiJointSelectorDiagnostics diagnostics,
                _budget.LeafPreviewTraceLimit);
            if (generation.TruncationPhase == CharacterTestRuntimeAiJointTruncationPhase.None
                && _evaluationTruncationPhase != CharacterTestRuntimeAiJointTruncationPhase.None)
            {
                diagnostics = new CharacterTestRuntimeAiJointSelectorDiagnostics(
                    diagnostics.Generation,
                    diagnostics.EvaluationCount,
                    diagnostics.TracePlanBuildCount,
                    diagnostics.ResultCacheHitCount,
                    _evaluationTruncationPhase,
                    _evaluationTruncationObservedCount,
                    _evaluationTruncationAcceptedCount,
                    _evaluationTruncationLimit);
            }
            if (!selected.HasSelection)
            {
                // Joint selection owns the planner action only when it has a
                // selectable leaf.  Falling back to the ordinary planner here
                // keeps non-attack actions (notably MoveToTarget) alive for
                // legacy geometry without authored approach anchors.  Primary
                // attack remains fail-closed because the ordinary planner uses
                // the same evaluator-backed adapter before selecting/submitting
                // it; that evaluation also records the per-source failure
                // evidence when target authority is unavailable.
                CharacterTestRuntimeAiJointDirective empty = new CharacterTestRuntimeAiJointDirective(
                    false,
                    false,
                    0,
                    default,
                    diagnostics,
                    default);
                StoreDiagnostics(source.Id.Value, plannerFrame, empty);
                return empty;
            }

            CharacterTestRuntimeAiJointSelectionCandidate selection = selected.Selection;
            int commandActionId = ResolveCommandActionId(profile, selection);
            if (commandActionId <= 0)
            {
                CharacterTestRuntimeAiJointDirective unmapped = new CharacterTestRuntimeAiJointDirective(
                    true,
                    false,
                    0,
                    default,
                    diagnostics,
                    default);
                StoreDiagnostics(source.Id.Value, plannerFrame, unmapped);
                return unmapped;
            }

            CharacterTestRuntimeAiJointPreparedAttack prepared = default;
            if (!selection.Seed.IsCompatibility
                && TryCreateCacheKeys(selection.Seed, out CharacterTestRuntimeAiJointTracePlanCacheKey traceKey, out _)
                && _selector.Cache.TracePlans.TryGet(
                    traceKey,
                    out CharacterTestRuntimeAiJointTracePlanCacheValue traceValue))
            {
                prepared = traceValue.PreparedAttack;
            }
            if (!selection.Seed.IsCompatibility && !prepared.IsValid)
            {
                CharacterTestRuntimeAiJointDirective invalidPrepared = new CharacterTestRuntimeAiJointDirective(
                    true,
                    false,
                    0,
                    default,
                    diagnostics,
                    default);
                StoreDiagnostics(source.Id.Value, plannerFrame, invalidPrepared);
                return invalidPrepared;
            }
            CharacterTestRuntimeAiJointDirective directive = new CharacterTestRuntimeAiJointDirective(
                true,
                true,
                commandActionId,
                selection,
                diagnostics,
                prepared);
            StoreDiagnostics(source.Id.Value, plannerFrame, directive);
            return directive;
        }

        /// <remarks>NoAlloc. Returns the immutable state recorded by the latest planner tick.</remarks>
        internal bool TryGetDiagnostic(
            int sourceEntityId,
            out CharacterTestRuntimeAiJointDiagnosticSnapshot snapshot)
        {
            for (int i = 0; i < _diagnosticCount; i++)
            {
                if (_diagnostics[i].SourceEntityId != sourceEntityId)
                    continue;
                snapshot = _diagnostics[i];
                return true;
            }
            snapshot = default;
            return false;
        }

        /// <remarks>AllocByDesign: Game View/editor diagnostics only.</remarks>
        internal CharacterTestRuntimeAiJointDiagnosticSnapshot[] CaptureDiagnostics()
        {
            if (_diagnosticCount == 0)
                return Array.Empty<CharacterTestRuntimeAiJointDiagnosticSnapshot>();
            var snapshots = new CharacterTestRuntimeAiJointDiagnosticSnapshot[_diagnosticCount];
            Array.Copy(_diagnostics, snapshots, _diagnosticCount);
            Array.Sort(snapshots, CompareDiagnostics);
            return snapshots;
        }

        public bool TryCreateCacheKeys(
            CharacterTestRuntimeAiJointCandidateSeed seed,
            out CharacterTestRuntimeAiJointTracePlanCacheKey tracePlanKey,
            out CharacterTestRuntimeAiJointResultCacheKey resultKey)
        {
            tracePlanKey = default;
            resultKey = default;
            if (_evaluationTruncationPhase != CharacterTestRuntimeAiJointTruncationPhase.None)
                return false;
            CharacterTestCharacterEntity target = _runtime?.FindEntityById(seed.TargetEntityId);
            if (_source == null || target == null
                || !_runtime.TryGetLastCompletedRuntimeFrame(out RuntimeFrame completedFrame)
                || _runtime.PhysicsRuntime?.World == null
                || !_runtime.TryGetDamageGeometrySnapshot(
                    target.Id.Value,
                    out CharacterTestDamageGeometrySnapshot geometry)
                || geometry == null
                || !geometry.IsValid)
            {
                return false;
            }

            int centerX = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                (geometry.WorldMin.X + geometry.WorldMax.X) / Fix64.FromInt(2));
            int centerZ = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                (geometry.WorldMin.Z + geometry.WorldMax.Z) / Fix64.FromInt(2));
            int targetBearing = CharacterTestDeterministicMath.Atan2MilliDegrees(
                centerX - _source.Transform.XMillimeters,
                centerZ - _source.Transform.ZMillimeters);
            int requiredTurnYaw = CharacterTestDeterministicMath.ShortestAngleDelta(
                _source.Transform.YawMilliDegrees,
                targetBearing);
            int frameValue = completedFrame.Value >= int.MaxValue
                ? int.MaxValue
                : completedFrame.Value <= int.MinValue ? int.MinValue : (int)completedFrame.Value;
            tracePlanKey = new CharacterTestRuntimeAiJointTracePlanCacheKey(
                _source.Id.Value,
                seed.Action.ResolvedActionId,
                CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
                _source.Transform.XMillimeters,
                _source.Transform.YMillimeters,
                _source.Transform.ZMillimeters,
                _source.Transform.YawMilliDegrees,
                frameValue,
                _runtime.PhysicsRuntime.World.Revision,
                _source.SourcePoseRevision,
                _source.SourceFacingRevision,
                seed.Action.ActionBindingRevision,
                seed.Action.AnimationRevision,
                seed.Action.RootMotionRevision,
                seed.Action.TracePolicyRevision,
                target.Id.Value,
                requiredTurnYaw);

            _runtime.TryGetDamageZoneSnapshot(
                target.Id.Value,
                out CharacterTestDamageZoneSnapshot zones);
            _adapter.TryGetZoneLockWitness(
                _source.Id.Value,
                out int lockRevision,
                out _);
            CharacterTestNavigationGraph graph = _navigation?.Graph;
            int graphRevision = graph?.Revision ?? 0;
            long graphHash = graph?.ContentHash ?? 0L;
            int bindingRevision = zones?.BindingRevision ?? 0;
            RuntimePreparedDigest anchorContent = default;
            if (_runtime.TryGetRuntimeGeometryBinding(
                    target.Id.Value,
                    out CharacterImportedGeometryBinding binding)
                && binding != null)
            {
                bindingRevision = Combine(bindingRevision, binding.EngagementAnchorRevision);
                anchorContent = binding.EngagementAnchorContentDigest;
            }
            resultKey = new CharacterTestRuntimeAiJointResultCacheKey(
                tracePlanKey,
                target.Id.Value,
                geometry.HurtboxRevision,
                bindingRevision,
                CharacterTestAttackEvaluationMode.CurrentPose,
                seed.ZoneId,
                0,
                0,
                0,
                0,
                0,
                lockRevision,
                _adapter.ReservationAuthority.Revision,
                _adapter.ReservationAuthority.Count,
                graphRevision,
                graphHash,
                frameValue,
                _runtime.PhysicsRuntime.World.Revision,
                anchorContent);
            return true;
        }

        public bool TryBuildTracePlan(
            CharacterTestRuntimeAiJointCandidateSeed seed,
            out CharacterTestRuntimeAiJointTracePlanCacheValue tracePlan)
        {
            tracePlan = default;
            CharacterTestCharacterEntity target = _runtime?.FindEntityById(seed.TargetEntityId);
            if (!_adapter.TryPrepareJointPrimaryAttack(
                    _source,
                    target,
                    seed.Action.ResolvedActionId,
                    _plannerFrame,
                    out CharacterTestRuntimeAiJointPreparedAttack prepared,
                    out CharacterTestAttackOpportunityFailureReason failureReason))
            {
                if (_lastPreparationFailureReason
                    == CharacterTestAttackOpportunityFailureReason.None)
                {
                    _lastPreparationFailureReason = failureReason;
                }
                return false;
            }
            tracePlan = new CharacterTestRuntimeAiJointTracePlanCacheValue(prepared, sampleCount: 2);
            return true;
        }

        public bool TryEvaluate(
            CharacterTestRuntimeAiJointCandidateSeed seed,
            CharacterTestRuntimeAiJointTracePlanCacheValue tracePlan,
            out CharacterTestRuntimeAiJointEvaluation evaluation)
        {
            evaluation = default;
            if (!tracePlan.Valid)
                return false;
            int poseBefore = _candidatePosePreviewCount;
            int pathBefore = _pathQueryCount;
            bool success = _adapter.TryEvaluatePreparedJointPrimaryAttack(
                    tracePlan.PreparedAttack,
                    seed.ZoneId,
                    _budget.CandidatePosePreviewLimit - poseBefore,
                    _budget.PathQueryLimit - pathBefore,
                    out evaluation,
                    out _,
                    out _,
                    out CharacterTestRuntimeAiJointEvaluationBudgetDiagnostics budget);
            _candidatePosePreviewCount += budget.CandidatePosePreviewCount;
            _pathQueryCount += budget.PathQueryCount;
            if (_evaluationTruncationPhase == CharacterTestRuntimeAiJointTruncationPhase.None
                && budget.TruncationPhase != CharacterTestRuntimeAiJointTruncationPhase.None)
            {
                _evaluationTruncationPhase = budget.TruncationPhase;
                int prior = budget.TruncationPhase
                    == CharacterTestRuntimeAiJointTruncationPhase.CandidatePose
                        ? poseBefore
                        : pathBefore;
                _evaluationTruncationObservedCount = prior + budget.TruncationObservedCount;
                _evaluationTruncationAcceptedCount = prior + budget.TruncationAcceptedCount;
                _evaluationTruncationLimit = budget.TruncationLimit + prior;
            }
            return success;
        }

        private int CollectActors(
            CharacterTestCharacterEntity source,
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            int count = 0;
            for (int i = 0; i < entities.Count && count < _actors.Length; i++)
            {
                CharacterTestCharacterEntity target = entities[i];
                if (target == null)
                    continue;
                long squared;
                if (!_runtime.PerceptionFrameCache.TryGetSpatialDistanceSquaredMillimeters(
                        _plannerFrame,
                        source,
                        target,
                        out squared))
                {
                    long dx = (long)target.Transform.XMillimeters - source.Transform.XMillimeters;
                    long dy = (long)target.Transform.YMillimeters - source.Transform.YMillimeters;
                    long dz = (long)target.Transform.ZMillimeters - source.Transform.ZMillimeters;
                    squared = SaturatingSquaredDistance(dx, dy, dz);
                }
                bool snapshotCurrent = _runtime.TryGetDamageGeometrySnapshot(
                        target.Id.Value,
                        out CharacterTestDamageGeometrySnapshot geometry)
                    && geometry != null
                    && geometry.IsValid;
                _actors[count++] = new CharacterTestRuntimeAiJointActorInput(
                    target.Id.Value,
                    target.Team.TeamId,
                    target.CombatState.LifeState,
                    snapshotCurrent,
                    squared,
                    maskCompatible: true,
                    withinSoftSearchRadius: true);
            }
            return count;
        }

        private int CollectActions(
            CharacterTestCharacterEntity source,
            CharacterTestRuntimeAiPlannerCompiledProfile profile,
            RuntimeFrame plannerFrame)
        {
            int count = 0;
            bool resolved = _adapter.TryResolvePlannerPrimaryAttackAction(
                source,
                plannerFrame,
                out CharacterActionConfig primary);
            _adapter.GetJointActionCacheWitnesses(
                source,
                primary,
                out int actionBindingRevision,
                out int animationRevision,
                out int rootMotionRevision,
                out int tracePolicyRevision);
            IReadOnlyList<CharacterTestRuntimeAiPlannerCompiledAction> actions = profile.Actions;
            CharacterTestRuntimeAiJointAabb fullWorld = new CharacterTestRuntimeAiJointAabb(
                -WorldExtentMillimeters,
                -WorldExtentMillimeters,
                -WorldExtentMillimeters,
                WorldExtentMillimeters,
                WorldExtentMillimeters,
                WorldExtentMillimeters);
            bool actionRunning = (source.Action.State == CharacterTestActionStateId.Running);
            for (int i = 0; i < actions.Count && count < _actions.Length; i++)
            {
                CharacterTestRuntimeAiPlannerCompiledAction action = actions[i];
                if (action.Kind == CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack)
                {
                    _actions[count++] = new CharacterTestRuntimeAiJointActionInput(
                        action.ActionId,
                        primary?.ActionId ?? CharacterActionId.None,
                        CharacterTestRuntimeAiJointCandidateSource.EvaluatorBacked,
                        ready: !actionRunning,
                        resolvable: resolved,
                        ActionUtilityMilli(action),
                        fullWorld,
                        actionBindingRevision,
                        animationRevision,
                        rootMotionRevision,
                        tracePolicyRevision);
                }
                else if (action.Kind == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot)
                {
                    _actions[count++] = new CharacterTestRuntimeAiJointActionInput(
                        action.ActionId,
                        CharacterActionId.None,
                        CharacterTestRuntimeAiJointCandidateSource.CompatibilityLegacyRange,
                        ready: !actionRunning,
                        resolvable: action.AbilitySlot >= 0 || action.AbilityId > 0,
                        ActionUtilityMilli(action),
                        fullWorld);
                }
            }
            return count;
        }

        private int CollectZones(CharacterTestCharacterEntity source, int actorCount)
        {
            int count = 0;
            for (int actorIndex = 0; actorIndex < actorCount && count < _zones.Length; actorIndex++)
            {
                int targetId = _actors[actorIndex].TargetEntityId;
                CharacterTestRuntimeAiJointActorInput actor = _actors[actorIndex];
                if (targetId == source.Id.Value
                    || actor.TeamId == source.Team.TeamId
                    || actor.LifeState != CharacterTestCharacterLifeState.Alive
                    || !actor.SnapshotCurrent
                    || !actor.MaskCompatible
                    || !actor.WithinSoftSearchRadius)
                {
                    continue;
                }
                if (_runtime.TryGetDamageZoneSnapshot(
                        targetId,
                        out CharacterTestDamageZoneSnapshot zones)
                    && zones != null
                    && zones.IsValid)
                {
                    CharacterTestCharacterEntity target = _runtime.FindEntityById(targetId);
                    for (int i = 0; i < zones.ZoneCount && count < _zones.Length; i++)
                    {
                        CharacterTestDamageZoneAggregate zone = zones.GetZone(i);
                        if (_adapter.TryProjectJointZoneState(
                                target,
                                zones,
                                zone,
                                out CharacterTestBodyPartDamageZoneSelectionState dynamicState))
                        {
                            _zones[count++] = new CharacterTestRuntimeAiJointZoneInput(
                                targetId,
                                zone.ZoneId,
                                dynamicState.Hittable,
                                dynamicState.Approachable,
                                dynamicState.EffectivePriority,
                                Bounds(zone.WorldMin, zone.WorldMax));
                        }
                    }
                    continue;
                }
                if (_runtime.TryGetDamageGeometrySnapshot(
                        targetId,
                        out CharacterTestDamageGeometrySnapshot geometry)
                    && geometry != null
                    && geometry.IsValid)
                {
                    _zones[count++] = new CharacterTestRuntimeAiJointZoneInput(
                        targetId,
                        0,
                        true,
                        false,
                        0,
                        Bounds(geometry.WorldMin, geometry.WorldMax));
                }
            }
            return count;
        }

        private static int ResolveCommandActionId(
            CharacterTestRuntimeAiPlannerCompiledProfile profile,
            CharacterTestRuntimeAiJointSelectionCandidate selection)
        {
            if (selection.Seed.IsCompatibility || !selection.Evaluation.ApproachPose)
                return selection.Seed.Action.PlannerActionId;
            bool found = false;
            CharacterTestRuntimeAiPlannerCompiledAction best = default;
            IReadOnlyList<CharacterTestRuntimeAiPlannerCompiledAction> actions = profile.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                CharacterTestRuntimeAiPlannerCompiledAction action = actions[i];
                if (action.Kind != CharacterTestRuntimeAiPlannerActionKind.MoveToTarget)
                    continue;
                if (!found || BetterPlannerAction(action, best))
                {
                    best = action;
                    found = true;
                }
            }
            return found ? best.ActionId : 0;
        }

        private static bool BetterPlannerAction(
            CharacterTestRuntimeAiPlannerCompiledAction candidate,
            CharacterTestRuntimeAiPlannerCompiledAction current)
        {
            int score = candidate.Score.CompareTo(current.Score);
            if (score != 0)
                return score > 0;
            int cost = candidate.Cost.CompareTo(current.Cost);
            if (cost != 0)
                return cost < 0;
            return candidate.ActionId < current.ActionId;
        }

        private static int ActionUtilityMilli(CharacterTestRuntimeAiPlannerCompiledAction action)
        {
            double value = ((double)action.Score - action.Cost) * 1000d;
            if (value >= int.MaxValue)
                return int.MaxValue;
            if (value <= int.MinValue)
                return int.MinValue;
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static CharacterTestRuntimeAiJointAabb Bounds(FixVector3 min, FixVector3 max)
        {
            return new CharacterTestRuntimeAiJointAabb(
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(min.X),
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(min.Y),
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(min.Z),
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(max.X),
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(max.Y),
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(max.Z));
        }

        private static long SaturatingSquaredDistance(long x, long y, long z)
        {
            ulong ax = AbsAsUnsigned(x);
            ulong ay = AbsAsUnsigned(y);
            ulong az = AbsAsUnsigned(z);
            const ulong squareRootLongMax = 3037000499UL;
            if (ax > squareRootLongMax || ay > squareRootLongMax || az > squareRootLongMax)
                return long.MaxValue;
            ulong total = ax * ax;
            ulong ySquared = ay * ay;
            ulong zSquared = az * az;
            if (total > (ulong)long.MaxValue - ySquared)
                return long.MaxValue;
            total += ySquared;
            if (total > (ulong)long.MaxValue - zSquared)
                return long.MaxValue;
            return (long)(total + zSquared);
        }

        private void StoreDiagnostics(
            int sourceEntityId,
            RuntimeFrame plannerFrame,
            CharacterTestRuntimeAiJointDirective directive)
        {
            int frame = plannerFrame.Value >= int.MaxValue
                ? int.MaxValue
                : plannerFrame.Value <= int.MinValue ? int.MinValue : (int)plannerFrame.Value;
            CharacterTestRuntimeAiJointDiagnosticSnapshot snapshot =
                new CharacterTestRuntimeAiJointDiagnosticSnapshot(
                    frame,
                    sourceEntityId,
                    directive,
                    _candidatePosePreviewCount,
                    _pathQueryCount,
                    _lastPreparationFailureReason);
            for (int i = 0; i < _diagnosticCount; i++)
            {
                if (_diagnostics[i].SourceEntityId != sourceEntityId)
                    continue;
                _diagnostics[i] = snapshot;
                return;
            }
            if (_diagnosticCount < _diagnostics.Length)
                _diagnostics[_diagnosticCount++] = snapshot;
        }

        private static int CompareDiagnostics(
            CharacterTestRuntimeAiJointDiagnosticSnapshot left,
            CharacterTestRuntimeAiJointDiagnosticSnapshot right)
        {
            return left.SourceEntityId.CompareTo(right.SourceEntityId);
        }

        private static ulong AbsAsUnsigned(long value)
        {
            return value >= 0L ? (ulong)value : (ulong)(-(value + 1L)) + 1UL;
        }

        private static int Combine(int left, int right)
        {
            unchecked { return left * 397 ^ right; }
        }
    }
}

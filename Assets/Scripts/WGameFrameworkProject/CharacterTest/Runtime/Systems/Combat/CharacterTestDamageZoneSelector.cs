using System;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestDamageZoneSelectionStatus
    {
        Valid = 0,
        NoValidZone = 1,
        StaleSnapshot = 2,
        InvalidDynamicState = 3
    }

    internal enum CharacterTestDamageZoneSwitchReason
    {
        None = 0,
        InitialSelection = 1,
        PreviousLockKept = 2,
        PreviousLockInvalidated = 3,
        OpportunityLevelImproved = 4,
        EarliestHitImprovedByThreshold = 5,
        NoValidZone = 6,
        StaleSnapshot = 7
    }

    internal enum CharacterTestDamageZoneStateRevisionReason
    {
        None = 0,
        LegacyNoDefinitions = 1,
        RevisionUnchanged = 2,
        RevisionChangedLockKept = 3,
        RevisionChangedSelectionChanged = 4,
        RevisionChangedLockInvalidated = 5,
        InvalidState = 6
    }

    internal readonly struct CharacterTestDamageZoneLock
    {
        internal CharacterTestDamageZoneLock(
            GameplayEntityId targetGameplayEntityId,
            CombatEntityId targetCombatEntityId,
            int resolvedActionRevision,
            int zoneId,
            int hitZoneId,
            int logicalHurtboxId,
            CharacterTestAttackOpportunityLevel opportunityLevel,
            int earliestHitTick,
            int bindingRevision,
            int hurtboxRevision,
            int worldRevision,
            int zoneRevision,
            RuntimeFrame selectedFrame,
            CharacterTestDamageZoneSwitchReason switchReason,
            long bodyPartStateRevision = 0L,
            int sourceEntityId = 0,
            int targetEntityId = 0)
        {
            HasLock = logicalHurtboxId > 0;
            TargetGameplayEntityId = targetGameplayEntityId;
            TargetCombatEntityId = targetCombatEntityId;
            ResolvedActionRevision = resolvedActionRevision;
            ZoneId = zoneId;
            HitZoneId = hitZoneId;
            LogicalHurtboxId = logicalHurtboxId;
            OpportunityLevel = opportunityLevel;
            EarliestHitTick = earliestHitTick;
            BindingRevision = bindingRevision;
            HurtboxRevision = hurtboxRevision;
            WorldRevision = worldRevision;
            ZoneRevision = zoneRevision;
            SelectedFrame = selectedFrame;
            SwitchReason = switchReason;
            BodyPartStateRevision = bodyPartStateRevision;
            int resolvedTargetEntityId = targetEntityId > 0
                ? targetEntityId
                : targetCombatEntityId.Value
                    - CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart;
            TargetHandle = new CharacterTestCombatTargetHandle(
                sourceEntityId,
                resolvedTargetEntityId,
                targetGameplayEntityId,
                targetCombatEntityId,
                ZoneId,
                HitZoneId,
                LogicalHurtboxId,
                bindingRevision,
                selectedFrame);
        }

        public bool HasLock { get; }
        public GameplayEntityId TargetGameplayEntityId { get; }
        public CombatEntityId TargetCombatEntityId { get; }
        public int ResolvedActionRevision { get; }
        public int ZoneId { get; }
        public int HitZoneId { get; }
        public int LogicalHurtboxId { get; }
        public CharacterTestAttackOpportunityLevel OpportunityLevel { get; }
        public int EarliestHitTick { get; }
        public int BindingRevision { get; }
        public int HurtboxRevision { get; }
        public int WorldRevision { get; }
        public int ZoneRevision { get; }
        public RuntimeFrame SelectedFrame { get; }
        public CharacterTestDamageZoneSwitchReason SwitchReason { get; }
        public long BodyPartStateRevision { get; }
        internal CharacterTestCombatTargetHandle TargetHandle { get; }

        internal CharacterTestCombatTargetHandle ToCombatTargetHandle(int sourceEntityId)
        {
            return new CharacterTestCombatTargetHandle(
                sourceEntityId,
                TargetHandle.TargetEntityId,
                TargetGameplayEntityId,
                TargetCombatEntityId,
                ZoneId,
                HitZoneId,
                LogicalHurtboxId,
                BindingRevision,
                SelectedFrame);
        }
    }

    internal readonly struct CharacterTestDamageZoneSelectionResult
    {
        internal CharacterTestDamageZoneSelectionResult(
            CharacterTestDamageZoneSelectionStatus status,
            CharacterTestDamageZoneSwitchReason switchReason,
            CharacterTestDamageZoneAggregate zone,
            CharacterTestAttackOpportunityResult opportunity,
            CharacterTestDamageZoneLock zoneLock,
            CharacterTestBodyPartDamageZoneSelectionState dynamicState = default,
            CharacterTestDamageZoneStateRevisionReason stateRevisionReason =
                CharacterTestDamageZoneStateRevisionReason.None)
        {
            Status = status;
            SwitchReason = switchReason;
            Zone = zone;
            Opportunity = opportunity;
            ZoneLock = zoneLock;
            DynamicState = dynamicState;
            StateRevisionReason = stateRevisionReason;
        }

        public CharacterTestDamageZoneSelectionStatus Status { get; }
        public CharacterTestDamageZoneSwitchReason SwitchReason { get; }
        public CharacterTestDamageZoneAggregate Zone { get; }
        public CharacterTestAttackOpportunityResult Opportunity { get; }
        public CharacterTestDamageZoneLock ZoneLock { get; }
        public CharacterTestBodyPartDamageZoneSelectionState DynamicState { get; }
        public CharacterTestDamageZoneStateRevisionReason StateRevisionReason { get; }
        public bool IsUsable => Status == CharacterTestDamageZoneSelectionStatus.Valid;
    }

    /// <summary>Side-effect-free deterministic Zone reduction over one frozen evaluation snapshot.</summary>
    internal sealed class CharacterTestDamageZoneSelector
    {
        private const int EarliestHitSwitchThresholdTicks = 2;
        private readonly ICharacterTestAttackOpportunityEvaluator _opportunityService;
        private readonly CharacterTestBodyPartDamageZoneSelectionStateAdapter _dynamicStateAdapter;

        internal CharacterTestDamageZoneSelector(
            ICharacterTestAttackOpportunityEvaluator opportunityService,
            CharacterTestBodyPartDamageZoneSelectionStateAdapter dynamicStateAdapter = null)
        {
            _opportunityService = opportunityService ?? throw new ArgumentNullException(nameof(opportunityService));
            _dynamicStateAdapter = dynamicStateAdapter
                ?? new CharacterTestBodyPartDamageZoneSelectionStateAdapter();
        }

        internal CharacterTestDamageZoneSelectionResult Select(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestDamageZoneSnapshot zones,
            CharacterTestDamageZoneLock previousLock,
            int requiredYawDeltaMilliDegrees = 0)
        {
            if (!IsCurrent(query, zones))
                return Failure(CharacterTestDamageZoneSelectionStatus.StaleSnapshot, CharacterTestDamageZoneSwitchReason.StaleSnapshot);

            Candidate best = default;
            Candidate previous = default;
            for (int i = 0; i < zones.ZoneCount; i++)
            {
                CharacterTestDamageZoneAggregate zone = zones.GetZone(i);
                if (!zone.IsAvailable)
                    continue;
                if (!TryProjectDynamicState(query.Target, zones, zone, out CharacterTestBodyPartDamageZoneSelectionState dynamicState))
                {
                    return Failure(
                        CharacterTestDamageZoneSelectionStatus.InvalidDynamicState,
                        CharacterTestDamageZoneSwitchReason.NoValidZone,
                        CharacterTestDamageZoneStateRevisionReason.InvalidState);
                }
                if (!dynamicState.Hittable)
                    continue;
                CharacterTestAttackOpportunityResult opportunity = _opportunityService.Evaluate(
                    query.WithDamageZoneFilter(zones, zone.ZoneId),
                    requiredYawDeltaMilliDegrees);
                if (opportunity.Status != CharacterTestAttackOpportunityStatus.Valid)
                    continue;
                var candidate = new Candidate(zone, opportunity, dynamicState);
                if (!best.IsValid || Compare(candidate, best) < 0)
                    best = candidate;
                if (previousLock.HasLock
                    && previousLock.ZoneId == zone.ZoneId
                    && ((previousLock.LogicalHurtboxId <= 0)
                        || (previousLock.LogicalHurtboxId == opportunity.SelectedHurtboxId)))
                {
                    previous = candidate;
                }
            }

            if (!best.IsValid)
            {
                return Failure(
                    CharacterTestDamageZoneSelectionStatus.NoValidZone,
                    previousLock.HasLock
                        ? CharacterTestDamageZoneSwitchReason.PreviousLockInvalidated
                        : CharacterTestDamageZoneSwitchReason.NoValidZone);
            }

            Candidate selected = best;
            CharacterTestDamageZoneSwitchReason reason = CharacterTestDamageZoneSwitchReason.InitialSelection;
            if (previousLock.HasLock)
            {
                if (!IsPreviousWitnessCurrent(query, zones, previousLock) || !previous.IsValid)
                {
                    reason = CharacterTestDamageZoneSwitchReason.PreviousLockInvalidated;
                }
                else if (best.Opportunity.Level > previous.Opportunity.Level)
                {
                    reason = CharacterTestDamageZoneSwitchReason.OpportunityLevelImproved;
                }
                else if (best.Opportunity.Level == previous.Opportunity.Level
                    && IsTickImprovementAtLeast(previous.Opportunity.EarliestCandidateHitTick,
                        best.Opportunity.EarliestCandidateHitTick,
                        EarliestHitSwitchThresholdTicks))
                {
                    reason = CharacterTestDamageZoneSwitchReason.EarliestHitImprovedByThreshold;
                }
                else
                {
                    selected = previous;
                    reason = CharacterTestDamageZoneSwitchReason.PreviousLockKept;
                }
            }

            CharacterTestDamageZoneLock nextLock = CreateLock(query, zones, selected, reason);
            CharacterTestDamageZoneStateRevisionReason stateRevisionReason =
                ResolveStateRevisionReason(previousLock, selected.DynamicState, reason);
            return new CharacterTestDamageZoneSelectionResult(
                CharacterTestDamageZoneSelectionStatus.Valid,
                reason,
                selected.Zone,
                selected.Opportunity,
                nextLock,
                selected.DynamicState,
                stateRevisionReason);
        }

        /// <summary>
        /// Evaluates exactly one authored Zone for CD-M4 joint selection. The
        /// caller owns cross-Zone reduction, so this method deliberately does
        /// not apply the legacy selector's hysteresis against another Zone.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        internal CharacterTestDamageZoneSelectionResult EvaluateZone(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestDamageZoneSnapshot zones,
            int zoneId,
            int requiredYawDeltaMilliDegrees = 0)
        {
            if (!IsCurrent(query, zones))
            {
                return Failure(
                    CharacterTestDamageZoneSelectionStatus.StaleSnapshot,
                    CharacterTestDamageZoneSwitchReason.StaleSnapshot);
            }
            if (!zones.TryGetAvailableZone(zoneId, out CharacterTestDamageZoneAggregate zone))
            {
                return Failure(
                    CharacterTestDamageZoneSelectionStatus.NoValidZone,
                    CharacterTestDamageZoneSwitchReason.NoValidZone);
            }
            if (!TryProjectDynamicState(query.Target, zones, zone, out CharacterTestBodyPartDamageZoneSelectionState dynamicState))
            {
                return Failure(
                    CharacterTestDamageZoneSelectionStatus.InvalidDynamicState,
                    CharacterTestDamageZoneSwitchReason.NoValidZone,
                    CharacterTestDamageZoneStateRevisionReason.InvalidState);
            }
            if (!dynamicState.Hittable)
            {
                return Failure(
                    CharacterTestDamageZoneSelectionStatus.NoValidZone,
                    CharacterTestDamageZoneSwitchReason.NoValidZone);
            }

            CharacterTestAttackOpportunityResult opportunity = _opportunityService.Evaluate(
                query.WithDamageZoneFilter(zones, zone.ZoneId),
                requiredYawDeltaMilliDegrees);
            if (opportunity.Status != CharacterTestAttackOpportunityStatus.Valid)
            {
                return Failure(
                    CharacterTestDamageZoneSelectionStatus.NoValidZone,
                    CharacterTestDamageZoneSwitchReason.NoValidZone);
            }

            var candidate = new Candidate(zone, opportunity, dynamicState);
            CharacterTestDamageZoneLock nextLock = CreateLock(
                query,
                zones,
                candidate,
                CharacterTestDamageZoneSwitchReason.InitialSelection);
            return new CharacterTestDamageZoneSelectionResult(
                CharacterTestDamageZoneSelectionStatus.Valid,
                CharacterTestDamageZoneSwitchReason.InitialSelection,
                zone,
                opportunity,
                nextLock,
                dynamicState,
                dynamicState.Status == CharacterTestDamageZoneDynamicStateStatus.LegacyNoDefinitions
                    ? CharacterTestDamageZoneStateRevisionReason.LegacyNoDefinitions
                    : CharacterTestDamageZoneStateRevisionReason.None);
        }

        /// <summary>
        /// Evaluates only the exact Hurtbox already owned by the actor target.
        /// No other Zone or Hurtbox is considered when the witness is unavailable
        /// or cannot be hit by the current action.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        internal CharacterTestDamageZoneSelectionResult EvaluateLockedHurtbox(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestDamageZoneSnapshot zones,
            CharacterTestDamageZoneLock previousLock,
            int requiredYawDeltaMilliDegrees = 0)
        {
            if (!previousLock.HasLock
                || !IsCurrent(query, zones)
                || !previousLock.TargetGameplayEntityId.Equals(
                    query.Snapshot.TargetGameplayEntityId)
                || !previousLock.TargetCombatEntityId.Equals(
                    query.Snapshot.TargetEntityId)
                || !zones.ContainsMemberWitness(
                    previousLock.ZoneId,
                    previousLock.HitZoneId,
                    previousLock.LogicalHurtboxId)
                || !zones.TryGetAvailableZone(
                    previousLock.ZoneId,
                    out CharacterTestDamageZoneAggregate zone))
            {
                return Failure(
                    CharacterTestDamageZoneSelectionStatus.NoValidZone,
                    CharacterTestDamageZoneSwitchReason.PreviousLockInvalidated);
            }
            if (!TryProjectDynamicState(
                    query.Target,
                    zones,
                    zone,
                    out CharacterTestBodyPartDamageZoneSelectionState dynamicState)
                || !dynamicState.Hittable)
            {
                return Failure(
                    CharacterTestDamageZoneSelectionStatus.NoValidZone,
                    CharacterTestDamageZoneSwitchReason.PreviousLockInvalidated);
            }

            CharacterTestAttackOpportunityResult opportunity = _opportunityService.Evaluate(
                query.WithDamageZoneFilter(
                    zones,
                    previousLock.ZoneId,
                    previousLock.LogicalHurtboxId),
                requiredYawDeltaMilliDegrees);
            if (opportunity.Status != CharacterTestAttackOpportunityStatus.Valid)
            {
                return Failure(
                    CharacterTestDamageZoneSelectionStatus.NoValidZone,
                    CharacterTestDamageZoneSwitchReason.PreviousLockKept);
            }

            var candidate = new Candidate(zone, opportunity, dynamicState);
            CharacterTestDamageZoneLock refreshed = CreateLock(
                query,
                zones,
                candidate,
                CharacterTestDamageZoneSwitchReason.PreviousLockKept);
            return new CharacterTestDamageZoneSelectionResult(
                CharacterTestDamageZoneSelectionStatus.Valid,
                CharacterTestDamageZoneSwitchReason.PreviousLockKept,
                zone,
                opportunity,
                refreshed,
                dynamicState,
                CharacterTestDamageZoneStateRevisionReason.None);
        }

        /// <remarks>NoAlloc.</remarks>
        internal bool TryProjectDynamicState(
            CharacterTestCharacterEntity target,
            CharacterTestDamageZoneSnapshot zones,
            in CharacterTestDamageZoneAggregate zone,
            out CharacterTestBodyPartDamageZoneSelectionState projection)
        {
            return _dynamicStateAdapter.TryProject(target, zones, zone, out projection);
        }

        private static bool IsCurrent(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestDamageZoneSnapshot zones)
        {
            return zones != null
                && zones.IsValid
                && zones.Frame == query.CurrentCompletedFrame
                && zones.WorldRevision == query.CurrentCombatPhysicsRevision
                && zones.HurtboxRevision == query.CurrentTargetHurtboxRevision
                && zones.EntityId == query.Target?.Id.Value
                && zones.GameplayEntityId.Equals(query.Snapshot.TargetGameplayEntityId)
                && zones.CombatEntityId.Equals(query.Snapshot.TargetEntityId);
        }

        private static bool IsPreviousWitnessCurrent(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestDamageZoneSnapshot zones,
            CharacterTestDamageZoneLock previous)
        {
            return IsTopologyWitnessCurrent(
                zones,
                previous,
                query.Snapshot.TargetGameplayEntityId,
                query.Snapshot.TargetEntityId);
        }

        internal static bool IsTopologyWitnessCurrent(
            CharacterTestDamageZoneSnapshot zones,
            CharacterTestDamageZoneLock previous,
            GameplayEntityId targetGameplayEntityId,
            CombatEntityId targetCombatEntityId)
        {
            return zones != null
                && zones.IsValid
                && previous.TargetGameplayEntityId.Equals(targetGameplayEntityId)
                && previous.TargetCombatEntityId.Equals(targetCombatEntityId)
                && previous.BindingRevision == zones.BindingRevision
                && zones.ContainsMemberWitness(
                    previous.ZoneId,
                    previous.HitZoneId,
                    previous.LogicalHurtboxId);
        }

        private static bool IsTickImprovementAtLeast(int previousTick, int candidateTick, int threshold)
        {
            return previousTick >= 0 && candidateTick >= 0 && previousTick - candidateTick >= threshold;
        }

        private static int Compare(Candidate left, Candidate right)
        {
            int result = right.Opportunity.Level.CompareTo(left.Opportunity.Level);
            if (result != 0) return result;
            result = NormalizeTick(left.Opportunity.EarliestCandidateHitTick)
                .CompareTo(NormalizeTick(right.Opportunity.EarliestCandidateHitTick));
            if (result != 0) return result;
            result = right.DynamicState.EffectivePriority.CompareTo(
                left.DynamicState.EffectivePriority);
            if (result != 0) return result;
            result = CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, left.Zone.ZoneId).CompareTo(CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, right.Zone.ZoneId));
            if (result != 0) return result;
            return CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, left.Opportunity.SelectedHurtboxId).CompareTo(CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, right.Opportunity.SelectedHurtboxId));
        }

        private static int NormalizeTick(int tick) => tick < 0 ? int.MaxValue : tick;

        private static CharacterTestDamageZoneLock CreateLock(
            CharacterTestAttackOpportunityQuery query,
            CharacterTestDamageZoneSnapshot zones,
            Candidate selected,
            CharacterTestDamageZoneSwitchReason reason)
        {
            return new CharacterTestDamageZoneLock(
                query.Snapshot.TargetGameplayEntityId,
                query.Snapshot.TargetEntityId,
                query.CurrentResolvedActionRevision,
                selected.Zone.ZoneId,
                selected.Opportunity.SelectedHitZoneId,
                selected.Opportunity.SelectedHurtboxId,
                selected.Opportunity.Level,
                selected.Opportunity.EarliestCandidateHitTick,
                zones.BindingRevision,
                zones.HurtboxRevision,
                zones.WorldRevision,
                zones.ZoneRevision,
                query.CurrentCompletedFrame,
                reason,
                selected.DynamicState.StateRevision,
                query.Source?.Id.Value ?? 0,
                query.Target?.Id.Value ?? 0);
        }

        private static CharacterTestDamageZoneStateRevisionReason ResolveStateRevisionReason(
            CharacterTestDamageZoneLock previous,
            in CharacterTestBodyPartDamageZoneSelectionState selected,
            CharacterTestDamageZoneSwitchReason switchReason)
        {
            if (selected.Status == CharacterTestDamageZoneDynamicStateStatus.LegacyNoDefinitions)
                return CharacterTestDamageZoneStateRevisionReason.LegacyNoDefinitions;
            if (!previous.HasLock)
                return CharacterTestDamageZoneStateRevisionReason.None;
            if (previous.BodyPartStateRevision == selected.StateRevision)
                return CharacterTestDamageZoneStateRevisionReason.RevisionUnchanged;
            if (switchReason == CharacterTestDamageZoneSwitchReason.PreviousLockKept)
                return CharacterTestDamageZoneStateRevisionReason.RevisionChangedLockKept;
            if (switchReason == CharacterTestDamageZoneSwitchReason.PreviousLockInvalidated)
                return CharacterTestDamageZoneStateRevisionReason.RevisionChangedLockInvalidated;
            return CharacterTestDamageZoneStateRevisionReason.RevisionChangedSelectionChanged;
        }

        private static CharacterTestDamageZoneSelectionResult Failure(
            CharacterTestDamageZoneSelectionStatus status,
            CharacterTestDamageZoneSwitchReason reason,
            CharacterTestDamageZoneStateRevisionReason stateRevisionReason =
                CharacterTestDamageZoneStateRevisionReason.None)
        {
            return new CharacterTestDamageZoneSelectionResult(
                status,
                reason,
                default,
                default,
                default,
                default,
                stateRevisionReason);
        }

        private readonly struct Candidate
        {
            internal Candidate(
                CharacterTestDamageZoneAggregate zone,
                CharacterTestAttackOpportunityResult opportunity,
                CharacterTestBodyPartDamageZoneSelectionState dynamicState)
            {
                Zone = zone;
                Opportunity = opportunity;
                DynamicState = dynamicState;
                IsValid = true;
            }

            internal CharacterTestDamageZoneAggregate Zone { get; }
            internal CharacterTestAttackOpportunityResult Opportunity { get; }
            internal CharacterTestBodyPartDamageZoneSelectionState DynamicState { get; }
            internal bool IsValid { get; }
        }
    }
}

using System;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestDamageZoneDynamicStateStatus
    {
        None = 0,
        Ready = 1,
        LegacyNoDefinitions = 2
    }

    /// <summary>
    /// Immutable projection of one authored DamageZone against the target's current
    /// body-part authority state. It never changes the Zone's authored eligibility.
    /// </summary>
    internal readonly struct CharacterTestBodyPartDamageZoneSelectionState
    {
        internal CharacterTestBodyPartDamageZoneSelectionState(
            CharacterTestDamageZoneDynamicStateStatus status,
            int targetEntityId,
            int zoneId,
            long stateRevision,
            int definitionSourceRevision,
            int matchedDefinitionCount,
            int openDefinitionCount,
            int consumedDefinitionCount,
            int brokenDefinitionCount,
            bool focusVisible,
            bool hittable,
            bool aimable,
            bool approachable,
            int authoredPriority,
            int dynamicPriorityDelta,
            int effectivePriority)
        {
            Status = status;
            TargetEntityId = targetEntityId;
            ZoneId = zoneId;
            StateRevision = stateRevision;
            DefinitionSourceRevision = definitionSourceRevision;
            MatchedDefinitionCount = matchedDefinitionCount;
            OpenDefinitionCount = openDefinitionCount;
            ConsumedDefinitionCount = consumedDefinitionCount;
            BrokenDefinitionCount = brokenDefinitionCount;
            FocusVisible = focusVisible;
            Hittable = hittable;
            Aimable = aimable;
            Approachable = approachable;
            AuthoredPriority = authoredPriority;
            DynamicPriorityDelta = dynamicPriorityDelta;
            EffectivePriority = effectivePriority;
        }

        internal CharacterTestDamageZoneDynamicStateStatus Status { get; }
        internal int TargetEntityId { get; }
        internal int ZoneId { get; }
        internal long StateRevision { get; }
        internal int DefinitionSourceRevision { get; }
        internal int MatchedDefinitionCount { get; }
        internal int OpenDefinitionCount { get; }
        internal int ConsumedDefinitionCount { get; }
        internal int BrokenDefinitionCount { get; }
        internal bool FocusVisible { get; }
        internal bool Hittable { get; }
        internal bool Aimable { get; }
        internal bool Approachable { get; }
        internal int AuthoredPriority { get; }
        internal int DynamicPriorityDelta { get; }
        internal int EffectivePriority { get; }
        internal bool IsUsable => Status == CharacterTestDamageZoneDynamicStateStatus.Ready
            || Status == CharacterTestDamageZoneDynamicStateStatus.LegacyNoDefinitions;
    }

    /// <summary>
    /// CharacterTest-only, bounded adapter from body-part authority entries to the
    /// existing DamageZone priority comparison field.
    /// </summary>
    /// <remarks>NoAlloc. Reads at most eight compiled definitions.</remarks>
    internal sealed class CharacterTestBodyPartDamageZoneSelectionStateAdapter
    {
        internal const int MinimumDynamicPriorityDelta = -1_000;
        internal const int MaximumDynamicPriorityDelta = 1_000;

        internal bool TryProject(
            CharacterTestCharacterEntity target,
            CharacterTestDamageZoneSnapshot zones,
            in CharacterTestDamageZoneAggregate zone,
            out CharacterTestBodyPartDamageZoneSelectionState projection)
        {
            projection = default;
            if (target == null
                || zones == null
                || !zones.IsValid
                || target.Id.Value != zones.EntityId
                || zone.ZoneId <= 0)
            {
                return false;
            }

            CharacterTestBodyPartStateComponent state = target.BodyPartState;
            if (state == null || state.OwnerEntityId != target.Id.Value)
                return false;

            long stateRevision = state.Revision;
            if (state.InitializationStatus == CharacterTestBodyPartStateInitializationStatus.MissingDefinitions
                && state.DefinitionCount == 0)
            {
                projection = Create(
                    CharacterTestDamageZoneDynamicStateStatus.LegacyNoDefinitions,
                    target.Id.Value,
                    zone,
                    stateRevision,
                    state.DefinitionSourceRevision,
                    0,
                    0,
                    0,
                    0,
                    focusVisible: false,
                    dynamicPriorityDelta: 0);
                return true;
            }

            if (state.InitializationStatus != CharacterTestBodyPartStateInitializationStatus.Ready
                || state.DefinitionCount <= 0
                || state.DefinitionCount > CharacterTestBodyPartStateComponent.MaximumDefinitionCount)
            {
                return false;
            }

            long dynamicPriority = 0L;
            int matchedCount = 0;
            int openCount = 0;
            int consumedCount = 0;
            int brokenCount = 0;
            bool focusVisible = false;
            for (int i = 0; i < state.DefinitionCount; i++)
            {
                if (!state.TryGetDefinition(i, out CharacterTestBodyPartStateDefinition definition)
                    || !state.TryGetCurrentEntry(i, out CharacterTestBodyPartStateEntry entry))
                {
                    return false;
                }
                if (definition.DamageZoneId != zone.ZoneId)
                    continue;

                matchedCount++;
                if (entry.WoundState == CharacterTestBodyPartWoundState.Open)
                {
                    openCount++;
                    dynamicPriority += definition.AuthoredPriorityDeltaOpen;
                    focusVisible |= !string.IsNullOrEmpty(definition.FocusConsumeTag);
                }
                else if (entry.WoundState == CharacterTestBodyPartWoundState.Consumed)
                {
                    consumedCount++;
                }

                if (entry.StructureState == CharacterTestBodyPartStructureState.Broken)
                {
                    brokenCount++;
                    dynamicPriority += definition.AuthoredPriorityDeltaBroken;
                }
            }

            if (state.Revision != stateRevision)
                return false;

            int boundedDelta = ClampDynamicPriority(dynamicPriority);
            projection = Create(
                CharacterTestDamageZoneDynamicStateStatus.Ready,
                target.Id.Value,
                zone,
                stateRevision,
                state.DefinitionSourceRevision,
                matchedCount,
                openCount,
                consumedCount,
                brokenCount,
                focusVisible,
                boundedDelta);
            return true;
        }

        private static CharacterTestBodyPartDamageZoneSelectionState Create(
            CharacterTestDamageZoneDynamicStateStatus status,
            int targetEntityId,
            in CharacterTestDamageZoneAggregate zone,
            long stateRevision,
            int definitionSourceRevision,
            int matchedDefinitionCount,
            int openDefinitionCount,
            int consumedDefinitionCount,
            int brokenDefinitionCount,
            bool focusVisible,
            int dynamicPriorityDelta)
        {
            return new CharacterTestBodyPartDamageZoneSelectionState(
                status,
                targetEntityId,
                zone.ZoneId,
                stateRevision,
                definitionSourceRevision,
                matchedDefinitionCount,
                openDefinitionCount,
                consumedDefinitionCount,
                brokenDefinitionCount,
                focusVisible,
                zone.Hittable,
                zone.Aimable,
                zone.Approachable,
                zone.AuthoredPriority,
                dynamicPriorityDelta,
                SaturatingAdd(zone.AuthoredPriority, dynamicPriorityDelta));
        }

        private static int ClampDynamicPriority(long value)
        {
            if (value > MaximumDynamicPriorityDelta)
                return MaximumDynamicPriorityDelta;
            if (value < MinimumDynamicPriorityDelta)
                return MinimumDynamicPriorityDelta;
            return (int)value;
        }

        private static int SaturatingAdd(int left, int right)
        {
            long value = (long)left + right;
            if (value > int.MaxValue)
                return int.MaxValue;
            if (value < int.MinValue)
                return int.MinValue;
            return (int)value;
        }
    }
}

using System;
using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Modifiers;

namespace MxFramework.Gameplay
{
    /// <summary>Builds pure C# gameplay diagnostic snapshots from public runtime APIs.</summary>
    public sealed class GameplayDiagnosticSnapshotBuilder
    {
        public GameplayDiagnosticSnapshot Build(
            string sourceName,
            string abilitySource,
            IReadOnlyList<RuntimeEntity> entities,
            IReadOnlyList<int> attributeIds,
            AbilityCastResult lastCastResult,
            IReadOnlyList<AbilityEvent> abilityEvents,
            IReadOnlyList<AttributeChangedEvent> attributeEvents)
        {
            GameplayEntitySnapshot[] entitySnapshots = BuildEntities(entities, attributeIds);
            GameplayAbilityCastSnapshot lastCast = BuildLastCast(abilitySource, lastCastResult);
            GameplayAbilityEventSnapshot[] abilityEventSnapshots = BuildAbilityEvents(abilityEvents);
            GameplayAttributeEventSnapshot[] attributeEventSnapshots = BuildAttributeEvents(attributeEvents);

            return new GameplayDiagnosticSnapshot(
                sourceName,
                abilitySource,
                entitySnapshots,
                lastCast,
                abilityEventSnapshots,
                attributeEventSnapshots);
        }

        private static GameplayEntitySnapshot[] BuildEntities(
            IReadOnlyList<RuntimeEntity> entities,
            IReadOnlyList<int> attributeIds)
        {
            if (entities == null || entities.Count == 0)
                return Array.Empty<GameplayEntitySnapshot>();

            var snapshots = new GameplayEntitySnapshot[entities.Count];
            for (int i = 0; i < entities.Count; i++)
            {
                RuntimeEntity entity = entities[i];
                snapshots[i] = entity == null
                    ? new GameplayEntitySnapshot(
                        0,
                        0,
                        false,
                        Array.Empty<GameplayAttributeSnapshot>(),
                        Array.Empty<GameplayBuffSnapshot>(),
                        Array.Empty<GameplayModifierSnapshot>())
                    : new GameplayEntitySnapshot(
                        entity.EntityId,
                        entity.TeamId,
                        entity.IsAlive,
                        BuildAttributes(entity, attributeIds),
                        BuildBuffs(entity),
                        BuildModifiers(entity));
            }

            return snapshots;
        }

        private static GameplayAttributeSnapshot[] BuildAttributes(
            RuntimeEntity entity,
            IReadOnlyList<int> attributeIds)
        {
            if (entity == null || attributeIds == null || attributeIds.Count == 0)
                return Array.Empty<GameplayAttributeSnapshot>();

            var snapshots = new GameplayAttributeSnapshot[attributeIds.Count];
            for (int i = 0; i < attributeIds.Count; i++)
            {
                int attributeId = attributeIds[i];
                snapshots[i] = new GameplayAttributeSnapshot(attributeId, entity.Store.GetAttribute(attributeId));
            }

            return snapshots;
        }

        private static GameplayBuffSnapshot[] BuildBuffs(RuntimeEntity entity)
        {
            if (entity == null || entity.Buffs == null)
                return Array.Empty<GameplayBuffSnapshot>();

            BuffSnapshot[] buffs = entity.Buffs.CreateSnapshot();
            if (buffs == null || buffs.Length == 0)
                return Array.Empty<GameplayBuffSnapshot>();

            var snapshots = new GameplayBuffSnapshot[buffs.Length];
            for (int i = 0; i < buffs.Length; i++)
            {
                BuffSnapshot buff = buffs[i];
                bool isExpired = !buff.IsPermanent && buff.RemainingTime <= 0f;
                snapshots[i] = new GameplayBuffSnapshot(
                    buff.Id,
                    buff.Duration,
                    buff.RemainingTime,
                    buff.CurrentLayers,
                    buff.MaxLayers,
                    buff.IsPermanent,
                    isExpired);
            }

            return snapshots;
        }

        private static GameplayModifierSnapshot[] BuildModifiers(RuntimeEntity entity)
        {
            if (entity == null || entity.Modifiers == null)
                return Array.Empty<GameplayModifierSnapshot>();

            ModifierSnapshot[] modifiers = entity.Modifiers.CreateSnapshot();
            if (modifiers == null || modifiers.Length == 0)
                return Array.Empty<GameplayModifierSnapshot>();

            var snapshots = new GameplayModifierSnapshot[modifiers.Length];
            for (int i = 0; i < modifiers.Length; i++)
            {
                ModifierSnapshot modifier = modifiers[i];
                snapshots[i] = new GameplayModifierSnapshot(modifier.Id, modifier.ParamIndex);
            }

            return snapshots;
        }

        private static GameplayAbilityCastSnapshot BuildLastCast(
            string abilitySource,
            AbilityCastResult lastCastResult)
        {
            int[] targetIds = BuildTargetEntityIds(lastCastResult.Targets);
            return new GameplayAbilityCastSnapshot(
                abilitySource ?? string.Empty,
                lastCastResult.Success,
                lastCastResult.FailureReason,
                targetIds);
        }

        private static int[] BuildTargetEntityIds(IReadOnlyList<IRuntimeEntity> targets)
        {
            if (targets == null || targets.Count == 0)
                return Array.Empty<int>();

            var targetIds = new int[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                targetIds[i] = targets[i] == null ? 0 : targets[i].EntityId;

            return targetIds;
        }

        private static GameplayAbilityEventSnapshot[] BuildAbilityEvents(
            IReadOnlyList<AbilityEvent> abilityEvents)
        {
            if (abilityEvents == null || abilityEvents.Count == 0)
                return Array.Empty<GameplayAbilityEventSnapshot>();

            var snapshots = new GameplayAbilityEventSnapshot[abilityEvents.Count];
            for (int i = 0; i < abilityEvents.Count; i++)
            {
                AbilityEvent abilityEvent = abilityEvents[i];
                snapshots[i] = new GameplayAbilityEventSnapshot(
                    abilityEvent.Type.ToString(),
                    abilityEvent.AbilityId,
                    GetEntityId(abilityEvent.Caster),
                    GetEntityId(abilityEvent.Target),
                    abilityEvent.FailureReason);
            }

            return snapshots;
        }

        private static GameplayAttributeEventSnapshot[] BuildAttributeEvents(
            IReadOnlyList<AttributeChangedEvent> attributeEvents)
        {
            if (attributeEvents == null || attributeEvents.Count == 0)
                return Array.Empty<GameplayAttributeEventSnapshot>();

            var snapshots = new GameplayAttributeEventSnapshot[attributeEvents.Count];
            for (int i = 0; i < attributeEvents.Count; i++)
            {
                AttributeChangedEvent attributeEvent = attributeEvents[i];
                snapshots[i] = new GameplayAttributeEventSnapshot(
                    attributeEvent.AttributeId,
                    attributeEvent.BaseValue,
                    attributeEvent.OldValue,
                    attributeEvent.NewValue,
                    attributeEvent.Delta,
                    FormatSourceName(attributeEvent.Source));
            }

            return snapshots;
        }

        private static int? GetEntityId(IRuntimeEntity entity)
        {
            return entity == null ? (int?)null : entity.EntityId;
        }

        private static string FormatSourceName(object source)
        {
            if (source == null)
                return null;

            string name = source.ToString();
            return string.IsNullOrEmpty(name) ? source.GetType().Name : name;
        }
    }
}

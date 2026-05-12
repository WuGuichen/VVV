using System;
using System.Collections.Generic;
using MxFramework.Attributes;

namespace MxFramework.Gameplay
{
    /// <summary>Small diagnostics entry point for gameplay world/entity snapshots.</summary>
    public sealed class GameplayWorldDiagnostics
    {
        private readonly GameplayDiagnosticSnapshotBuilder _snapshotBuilder;

        public GameplayWorldDiagnostics(GameplayDiagnosticSnapshotBuilder snapshotBuilder = null)
        {
            _snapshotBuilder = snapshotBuilder ?? new GameplayDiagnosticSnapshotBuilder();
        }

        public GameplayDiagnosticSnapshot BuildSnapshot(
            string sourceName,
            string abilitySource,
            IReadOnlyList<RuntimeEntity> entities,
            IReadOnlyList<int> attributeIds,
            AbilityCastResult lastCastResult,
            IReadOnlyList<AbilityEvent> abilityEvents,
            IReadOnlyList<AttributeChangedEvent> attributeEvents)
        {
            return _snapshotBuilder.Build(
                sourceName,
                abilitySource,
                entities,
                attributeIds,
                lastCastResult,
                abilityEvents,
                attributeEvents);
        }

        public GameplayWorldDiagnosticsSummary BuildSummary(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot == null)
                return GameplayWorldDiagnosticsSummary.Empty;

            int aliveCount = 0;
            int attributeCount = 0;
            int buffCount = 0;
            int modifierCount = 0;

            IReadOnlyList<GameplayEntitySnapshot> entities = snapshot.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                GameplayEntitySnapshot entity = entities[i];
                if (entity.IsAlive)
                    aliveCount++;

                attributeCount += entity.Attributes.Count;
                buffCount += entity.Buffs.Count;
                modifierCount += entity.Modifiers.Count;
            }

            return new GameplayWorldDiagnosticsSummary(
                snapshot.SourceName,
                entities.Count,
                aliveCount,
                attributeCount,
                buffCount,
                modifierCount);
        }
    }

    public readonly struct GameplayWorldDiagnosticsSummary
    {
        public static readonly GameplayWorldDiagnosticsSummary Empty =
            new GameplayWorldDiagnosticsSummary(string.Empty, 0, 0, 0, 0, 0);

        public GameplayWorldDiagnosticsSummary(
            string sourceName,
            int entityCount,
            int aliveEntityCount,
            int attributeCount,
            int buffCount,
            int modifierCount)
        {
            SourceName = sourceName ?? string.Empty;
            EntityCount = entityCount;
            AliveEntityCount = aliveEntityCount;
            AttributeCount = attributeCount;
            BuffCount = buffCount;
            ModifierCount = modifierCount;
        }

        public string SourceName { get; }
        public int EntityCount { get; }
        public int AliveEntityCount { get; }
        public int AttributeCount { get; }
        public int BuffCount { get; }
        public int ModifierCount { get; }
    }
}

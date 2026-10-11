using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.StoryBridge;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestLevelGameplayTriggerAuthoringDocument
    {
        // Published project action kind; executed by Trigger's existing internal deferred owner.
        internal const int TacticalSetBindingEnabledActionId = 450502;

        /// <summary>Cold authoring of the indivisible outcome pair. Runtime binding ownership is supplied by the StageRun roster.</summary>
        internal void AddTacticalBattleOutcomeTemplates(GameplayEntityId owner = default)
        {
            for (int i = 0; i < (Entries?.Count ?? 0); i++)
                if (IsTacticalBattleOutcomeTemplate(Entries[i].TemplateId))
                    throw new InvalidOperationException("A stage has exactly one outcome pair.");
            var victory = AllocateBattleEntry(true);
            var defeat = AllocateBattleEntry(false);
            AddBattleEntry(victory, defeat, owner, true);
            AddBattleEntry(defeat, victory, owner, false);
        }

        private CharacterTestLevelGameplayTriggerEntry AllocateBattleEntry(bool victory)
        {
            int source = AllocateId(GameplayTriggerConfigIdKind.SourceBinding);
            return new CharacterTestLevelGameplayTriggerEntry
            {
                StableId = victory ? "trigger.tactical.victory" : "trigger.tactical.defeat",
                DisplayName = victory ? "战斗胜利" : "战斗失败",
                TemplateId = victory ? TacticalBattleVictoryStoryTemplateId : TacticalBattleDefeatStoryTemplateId,
                SourceDefinitionId = AllocateId(GameplayTriggerConfigIdKind.SourceDefinition),
                ConditionId = AllocateId(GameplayTriggerConfigIdKind.Condition),
                DefinitionId = AllocateId(GameplayTriggerConfigIdKind.Definition),
                BindingId = AllocateId(GameplayTriggerConfigIdKind.Binding),
                SourceBindingId = source,
                RouteIds = new List<int> { AllocateId(GameplayTriggerConfigIdKind.Route) }
            };
        }

        internal static GameplayTriggerConditionNodeAuthoring CreateBattleCondition(bool victory)
        {
            var signal = new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.SignalIdEquals,
                CharacterTestGameplayTriggerBattleVitalsCatalog.BattleVitalsSignalId);
            var player = new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.Payload0,
                comparison: victory ? GameplayTriggerComparison.Greater : GameplayTriggerComparison.LessOrEqual);
            var health = victory ? new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.All,
                children: new[] { player, new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.Payload1,
                    comparison: GameplayTriggerComparison.LessOrEqual) }) : player;
            return new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.All, children: new[] { signal, health });
        }

        private void AddBattleEntry(CharacterTestLevelGameplayTriggerEntry entry,
            CharacterTestLevelGameplayTriggerEntry other, GameplayEntityId owner, bool victory)
        {
            var source = Authoring ?? CreateEmptyAuthoring();
            var route = new GameplayTriggerRouteAuthoring(entry.RouteIds[0], GameplayTriggerOccurrenceKind.Pulsed,
                entry.ConditionId, new[]
                {
                    new GameplayTriggerActionAuthoring(GameplayTriggerStoryCommandActionKinds.RaiseTriggerValue,
                        GameplayTriggerTargetSelector.ExplicitBindingTarget, 1,
                        victory ? CharacterTestStoryIds.Triggers.TacticalBattleVictory : CharacterTestStoryIds.Triggers.TacticalBattleDefeat),
                    new GameplayTriggerActionAuthoring(TacticalSetBindingEnabledActionId,
                        GameplayTriggerTargetSelector.ExplicitBindingTarget, 1, payload0: 0),
                    new GameplayTriggerActionAuthoring(TacticalSetBindingEnabledActionId,
                        GameplayTriggerTargetSelector.ExplicitBindingTarget, 2, payload0: 0)
                }, stateScope: GameplayTriggerStateScope.PerInstance, maxActivations: 1, maxStateKeys: 1);
            Authoring = new GameplayTriggerAuthoringDocument(source.SchemaId, source.SchemaVersion,
                Append(source.Sources, new GameplayTriggerSourceAuthoring(entry.SourceDefinitionId, GameplayTriggerSourceKind.Signal)),
                Append(source.Conditions, new GameplayTriggerConditionAuthoring(entry.ConditionId, CreateBattleCondition(victory))),
                Append(source.Definitions, new GameplayTriggerDefinitionAuthoring(entry.DefinitionId, 1, entry.SourceDefinitionId, new[] { route })),
                Append(source.Bindings, new GameplayTriggerBindingAuthoring(entry.BindingId, entry.DefinitionId, entry.SourceBindingId,
                    owner.Index, owner.Generation, explicitTargets: new[]
                    {
                        new GameplayTriggerBindingTargetAuthoring(1, GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId, entry.SourceBindingId),
                        new GameplayTriggerBindingTargetAuthoring(2, GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId, other.SourceBindingId)
                    })));
            Entries ??= new List<CharacterTestLevelGameplayTriggerEntry>();
            Entries.Add(entry);
        }
    }
}

using System;
using System.Collections.Generic;
using MxFramework.Config.Levels;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.StoryBridge;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestLevelGameplayTriggerAuthoringAdapter
    {
        /// <summary>LowFreqAlloc. Bind a transient StageRun copy after Spawn, without saving live entity IDs into level authoring.</summary>
        internal static CharacterTestLevelGameplayTriggerAuthoringDocument BindBattleRosterOwner(
            CharacterTestLevelGameplayTriggerAuthoringDocument published, GameplayEntityId player)
        {
            if (published == null) throw new ArgumentNullException(nameof(published));
            if (!player.IsValid) throw new ArgumentOutOfRangeException(nameof(player));
            var candidate = published.Normalize();
            for (int i = 0; i < candidate.Authoring.Bindings.Length; i++)
            {
                var binding = candidate.Authoring.Bindings[i];
                bool battle = false;
                foreach (var entry in candidate.Entries)
                    battle |= entry.BindingId == binding.BindingId && CharacterTestLevelGameplayTriggerAuthoringDocument.IsTacticalBattleOutcomeTemplate(entry.TemplateId);
                if (battle) candidate.Authoring.Bindings[i] = new GameplayTriggerBindingAuthoring(binding.BindingId,
                    binding.DefinitionId, binding.SourceBindingId, player.Index, player.Generation,
                    binding.InitiallyEnabled, binding.ExplicitTargets);
            }
            return candidate;
        }

        private static void ValidateBattleTemplates(CharacterTestLevelGameplayTriggerAuthoringDocument document,
            string path, List<LevelWorldObjectAuthoringIssue> issues)
        {
            var entries = document.Entries;
            CharacterTestLevelGameplayTriggerEntry victory = null, defeat = null;
            int count = 0;
            for (int i = 0; i < (entries?.Count ?? 0); i++)
            {
                var entry = entries[i];
                if (!CharacterTestLevelGameplayTriggerAuthoringDocument.IsTacticalBattleOutcomeTemplate(entry?.TemplateId)) continue;
                count++;
                if (entry.TemplateId == CharacterTestLevelGameplayTriggerAuthoringDocument.TacticalBattleVictoryStoryTemplateId) victory = entry;
                else defeat = entry;
            }
            if (count == 0) return;
            if (count != 2 || victory == null || defeat == null ||
                !ValidBattleEntry(document, victory, defeat, true) || !ValidBattleEntry(document, defeat, victory, false))
                issues.Add(Error("CT_TRIGGER_BATTLE_CONTRACT_INVALID", path,
                    "Battle outcomes require one immutable mutually exclusive pair, one activation per instance, and transactional deferred disable of both bindings."));
        }

        private static bool ValidBattleEntry(CharacterTestLevelGameplayTriggerAuthoringDocument document,
            CharacterTestLevelGameplayTriggerEntry entry, CharacterTestLevelGameplayTriggerEntry other, bool victory)
        {
            GameplayTriggerConditionAuthoring condition = null;
            GameplayTriggerDefinitionAuthoring definition = null;
            GameplayTriggerBindingAuthoring binding = null;
            foreach (var row in document.Authoring.Conditions) if (row.ConditionId == entry.ConditionId) condition = row;
            foreach (var row in document.Authoring.Definitions) if (row.DefinitionId == entry.DefinitionId) definition = row;
            foreach (var row in document.Authoring.Bindings) if (row.BindingId == entry.BindingId) binding = row;
            if (!SameCondition(condition?.Root, CharacterTestLevelGameplayTriggerAuthoringDocument.CreateBattleCondition(victory)) ||
                definition?.Routes?.Length != 1 || binding == null || !binding.InitiallyEnabled || binding.ExplicitTargets.Length != 2)
                return false;
            var route = definition.Routes[0];
            if (route.ConditionId != entry.ConditionId || route.OccurrenceMask != GameplayTriggerOccurrenceKind.Pulsed ||
                route.StateScope != GameplayTriggerStateScope.PerInstance || route.MaxActivations != 1 ||
                route.CooldownFrames != 0 || route.PresenceLimit != GameplayTriggerPresenceLimit.Ignore ||
                route.ExclusiveGroupId != 0 || route.Actions.Length != 3) return false;
            for (int i = 0; i < 3; i++)
            {
                var action = route.Actions[i];
                int id = i == 0 ? GameplayTriggerStoryCommandActionKinds.RaiseTriggerValue
                    : CharacterTestLevelGameplayTriggerAuthoringDocument.TacticalSetBindingEnabledActionId;
                long payload = i == 0 ? (victory ? CharacterTestStoryIds.Triggers.TacticalBattleVictory : CharacterTestStoryIds.Triggers.TacticalBattleDefeat) : 0;
                if (action.ActionKindId != id || action.TargetSelector != GameplayTriggerTargetSelector.ExplicitBindingTarget ||
                    action.TargetSlotId != (i == 2 ? 2 : 1) || action.Payload0 != payload || action.Payload1 != 0 || action.Payload2 != 0) return false;
            }
            bool self = false, peer = false;
            foreach (var target in binding.ExplicitTargets)
            {
                if (target.DomainId != GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId || target.Generation != 0) return false;
                self |= target.SlotId == 1 && target.Value == entry.SourceBindingId;
                peer |= target.SlotId == 2 && target.Value == other.SourceBindingId;
            }
            return self && peer;
        }

        private static bool SameCondition(GameplayTriggerConditionNodeAuthoring left, GameplayTriggerConditionNodeAuthoring right)
        {
            if (left == null || left.Operation != right.Operation || left.Operand != right.Operand ||
                left.Comparison != right.Comparison || left.Children.Length != right.Children.Length) return false;
            for (int i = 0; i < left.Children.Length; i++) if (!SameCondition(left.Children[i], right.Children[i])) return false;
            return true;
        }
    }
}

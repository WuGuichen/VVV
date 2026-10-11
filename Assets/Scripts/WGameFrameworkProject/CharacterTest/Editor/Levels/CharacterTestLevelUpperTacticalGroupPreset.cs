using System;
using System.Linq;
using UnityEditor;

namespace MxFramework.Game.CharacterTest.Editor
{
    // Explicit authoring operation only. Existing formal groups and card revisions stay pinned.
    internal static class CharacterTestLevelUpperTacticalGroupPreset
    {
        internal const string Tag = "mx.level.upper-tactical.v1";
        private static readonly string[] CardIds = { "bc_fe8w87d52r", "bc_7hj69v6kd6", "bc_3205xjgcw8" };

        [MenuItem("MxFramework/Character/Runtime AI Planner/Prepare Playable Upper Tactical Group")]
        private static void PrepareFromMenu()
        {
            if (!TryGetOrCreate(out var group, out var issue))
                throw new InvalidOperationException(issue);
            UnityEngine.Debug.Log("已准备正式战术组：" + group.DisplayName + " / " + group.StableGroupId + " @" + group.Revision);
        }

        internal static bool TryGetOrCreate(out CharacterTestRuntimeAiPlannerBehaviorGroupRevision group, out string issue)
        {
            group = null; issue = string.Empty;
            var state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!state.Success) { issue = state.Diagnostic; return false; }
            group = state.GroupCatalog.Groups.FirstOrDefault(g => g.Tags.Contains(Tag) && IsExactUpperCandidate(g, state));
            if (group != null) return true;
            if (!TryBuildCandidate(state, out var candidate, out issue)) return false;
            return new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(state)
                .TryCreateGroupCopy(candidate, out group, out issue);
        }

        internal static bool TryBuildCandidate(CharacterTestRuntimeAiPlannerProjectCatalogState state,
            out CharacterTestRuntimeAiPlannerBehaviorGroupRevision candidate, out string issue)
        {
            candidate = null; issue = string.Empty;
            var source = state.GroupCatalog.Groups.SingleOrDefault(g => g.StableGroupId == "bg_06c30f0b24" && g.Revision == 2);
            if (source == null) { issue = "缺少精确来源组 bg_06c30f0b24 @2。"; return false; }
            var cards = source.Cards.Where(c => c.Enabled && CardIds.Contains(c.StableCardId)).ToArray();
            var entries = cards.Select(c => c.EntryId).ToHashSet(StringComparer.Ordinal);
            var profile = source.Profile.Clone();
            profile.Modules.RemoveAll(e => !entries.Contains(e.EntryId));
            candidate = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(source.StableGroupId, source.Revision,
                "单手剑 · 关卡战术组", "仅组织三张现有上层战术卡，沿用其条件、顺序与动作权威；无可选卡时沿用原 Profile 的 fallback，不接抽弃牌。",
                source.Tags.Concat(new[] { Tag }), source.SortOrder, profile, cards);
            if (!IsExactUpperCandidate(candidate, state))
            { candidate = null; issue = "来源卡版本、能力或基础卡费不满足当前关卡 AI 消费器。"; return false; }
            var compiled = CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(candidate.Profile);
            if (!compiled.Success) { candidate = null; issue = "候选 Profile 编译未通过。"; return false; }
            return true;
        }

        private static bool IsExactUpperCandidate(CharacterTestRuntimeAiPlannerBehaviorGroupRevision group,
            CharacterTestRuntimeAiPlannerProjectCatalogState state)
        {
            if (group.Cards.Count != CardIds.Length || group.Profile.Modules.Count != CardIds.Length) return false;
            foreach (var card in group.Cards)
            {
                if (!card.Enabled || !CardIds.Contains(card.StableCardId)
                    || !state.CardCatalog.TryGetExact(card.StableCardId, card.CardRevision, out var published)
                    || published.ContentHash != card.ContentHash || published.SemanticHash != card.SemanticHash
                    || published.Metadata.Cost != 0 || published.TacticalSource == null) return false;
                var source = published.TacticalSource;
                if (source.PointMove != null || source.PositionRecall != null || source.InteractionPreparation != null) return false;
            }
            return true;
        }
    }
}

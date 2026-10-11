using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabPlayerLegalityStatus
    {
        Unavailable = 0,
        PlayerLegal = 1,
        ExperimentOnly = 2
    }

    internal enum CharacterTestRuntimeAiLabPlayerLegalityReasonKind
    {
        EvaluationUnavailable = 0,
        FallbackOnly = 1,
        SlotLimitExceeded = 2,
        CostLimitExceeded = 3,
        DuplicateCard = 4,
        CardNotUnlocked = 5,
        UnpublishedCard = 6,
        PublishedRevisionUnavailable = 7,
        PublishedRevisionOutdated = 8,
        PublishedSourceMismatch = 9,
        UnpublishedSemanticChanges = 10,
        DraftInvalid = 11,
        CardReferenceMissing = 12,
        CharacterCapabilityMismatch = 13,
        CapabilityWarning = 14,
        DisabledCard = 15,
        GroupArbitrationOverride = 16
    }

    internal sealed class CharacterTestRuntimeAiLabPlayerLegalityReason
    {
        public CharacterTestRuntimeAiLabPlayerLegalityReason(
            CharacterTestRuntimeAiLabPlayerLegalityReasonKind kind,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity severity,
            string code,
            string cardStableId,
            string message)
        {
            Kind = kind;
            Severity = severity;
            Code = code ?? string.Empty;
            CardStableId = cardStableId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CharacterTestRuntimeAiLabPlayerLegalityReasonKind Kind { get; }
        public CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string CardStableId { get; }
        public string Message { get; }
    }

    internal sealed class CharacterTestRuntimeAiLabPlayerLegalityState
    {
        private static readonly IReadOnlyList<CharacterTestRuntimeAiLabPlayerLegalityReason> NoReasons =
            Array.AsReadOnly(Array.Empty<CharacterTestRuntimeAiLabPlayerLegalityReason>());

        private readonly IReadOnlyList<CharacterTestRuntimeAiLabPlayerLegalityReason> _reasons;

        public CharacterTestRuntimeAiLabPlayerLegalityState(
            CharacterTestRuntimeAiLabPlayerLegalityStatus status,
            int slotCount,
            int totalCost,
            CharacterTestRuntimeAiLabPlayerLegalityReason[] reasons)
        {
            Status = status;
            SlotCount = Math.Max(0, slotCount);
            TotalCost = Math.Max(0, totalCost);
            _reasons = reasons == null || reasons.Length == 0
                ? NoReasons
                : Array.AsReadOnly(reasons);
        }

        public CharacterTestRuntimeAiLabPlayerLegalityStatus Status { get; }
        public int SlotCount { get; }
        public int TotalCost { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabPlayerLegalityReason> Reasons => _reasons;
        public bool IsPlayerLegal => Status == CharacterTestRuntimeAiLabPlayerLegalityStatus.PlayerLegal;

        public static CharacterTestRuntimeAiLabPlayerLegalityState Unavailable(string message)
        {
            return new CharacterTestRuntimeAiLabPlayerLegalityState(
                CharacterTestRuntimeAiLabPlayerLegalityStatus.Unavailable,
                0,
                0,
                new[]
                {
                    new CharacterTestRuntimeAiLabPlayerLegalityReason(
                        CharacterTestRuntimeAiLabPlayerLegalityReasonKind.EvaluationUnavailable,
                        CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                        "LAB_PLAYER_LEGALITY_UNAVAILABLE",
                        string.Empty,
                        message)
                });
        }
    }

    internal interface ICharacterTestRuntimeAiLabPlayerLegalitySource
    {
        long ProjectionIdentity { get; }

        CharacterTestRuntimeAiLabPlayerLegalityState Evaluate(
            int characterId,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards);
    }

    internal static class CharacterTestRuntimeAiLabPlayerLegalityEvaluator
    {
        /// <remarks>LowFreqAlloc: explicit Lab authoring projection and publish-gate boundary only.</remarks>
        public static CharacterTestRuntimeAiLabPlayerLegalityState Evaluate(
            string characterStableId,
            int characterNumericId,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlockedCards,
            int runtimeContractVersion,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput = null)
        {
            if (group == null || cards == null || catalog == null || unlockedCards == null)
                return CharacterTestRuntimeAiLabPlayerLegalityState.Unavailable(
                    "玩家构筑目录或 Lab 草稿不可用。");

            var reasons = new List<CharacterTestRuntimeAiLabPlayerLegalityReason>();
            var stableIds = new string[group.Entries.Count];
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                string cardStableId = string.Empty;
                if (entry == null
                    || !cards.TryGetValue(entry.CardDraftId, out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    || card == null)
                {
                    AddError(
                        reasons,
                        CharacterTestRuntimeAiLabPlayerLegalityReasonKind.CardReferenceMissing,
                        "LAB_PLAYER_CARD_DRAFT_MISSING",
                        string.Empty,
                        "行为组引用的行为卡草稿不存在。");
                    stableIds[i] = string.Empty;
                    continue;
                }

                if (entry.Disabled || entry.RuntimeEntry == null || entry.RuntimeEntry.Disabled)
                {
                    AddError(
                        reasons,
                        CharacterTestRuntimeAiLabPlayerLegalityReasonKind.DisabledCard,
                        "LAB_PLAYER_CARD_DISABLED",
                        card.PublishedStableId,
                        "玩家卡组不支持禁用已安装卡；请移除卡片，或保留为 Lab 实验。");
                }
                if (HasPlayerInexpressibleArbitration(entry))
                {
                    AddError(
                        reasons,
                        CharacterTestRuntimeAiLabPlayerLegalityReasonKind.GroupArbitrationOverride,
                        "LAB_PLAYER_GROUP_ARBITRATION_OVERRIDE",
                        card.PublishedStableId,
                        "行为组包含玩家装配层无法表达的仲裁覆盖；当前组合仅属于 Lab 实验。");
                }

                cardStableId = card.PublishedStableId?.Trim() ?? string.Empty;
                stableIds[i] = cardStableId;
                if (!card.HasPublishedSource)
                {
                    AddError(
                        reasons,
                        CharacterTestRuntimeAiLabPlayerLegalityReasonKind.UnpublishedCard,
                        "LAB_PLAYER_CARD_UNPUBLISHED",
                        cardStableId,
                        "行为卡尚未建立 Published 来源，只能作为 Lab 实验运行。");
                }
                if (cardStableId.Length > 0 && !unlockedCards.Contains(cardStableId))
                {
                    AddError(
                        reasons,
                        CharacterTestRuntimeAiLabPlayerLegalityReasonKind.CardNotUnlocked,
                        "LAB_PLAYER_CARD_NOT_UNLOCKED",
                        cardStableId,
                        "当前角色收藏尚未解锁此行为卡。");
                }
                if (!card.HasPublishedSource)
                    continue;

                if (!catalog.TryGetExact(cardStableId, card.SourcePublishedRevision, out CharacterTestRuntimeAiPlannerPublishedBehaviorCard source))
                {
                    AddError(
                        reasons,
                        CharacterTestRuntimeAiLabPlayerLegalityReasonKind.PublishedRevisionUnavailable,
                        "LAB_PLAYER_SOURCE_REVISION_UNAVAILABLE",
                        cardStableId,
                        "草稿引用的 Published revision 已不在当前目录中。");
                    continue;
                }
                if (!card.MatchesPublishedSource(source))
                {
                    AddError(
                        reasons,
                        CharacterTestRuntimeAiLabPlayerLegalityReasonKind.PublishedSourceMismatch,
                        "LAB_PLAYER_SOURCE_IDENTITY_MISMATCH",
                        cardStableId,
                        "草稿保存的 Published 身份与当前目录不一致。");
                }
                if (!catalog.TryResolveLatestCompatible(cardStableId, runtimeContractVersion, out CharacterTestRuntimeAiPlannerPublishedBehaviorCard latest))
                {
                    AddError(
                        reasons,
                        CharacterTestRuntimeAiLabPlayerLegalityReasonKind.PublishedRevisionUnavailable,
                        "LAB_PLAYER_CARD_INCOMPATIBLE",
                        cardStableId,
                        "当前运行合约没有可用的 Published revision。");
                }
                else if (latest.Revision != source.Revision || latest.ContentHash != source.ContentHash)
                {
                    AddError(
                        reasons,
                        CharacterTestRuntimeAiLabPlayerLegalityReasonKind.PublishedRevisionOutdated,
                        "LAB_PLAYER_SOURCE_REVISION_OUTDATED",
                        cardStableId,
                        "草稿不是玩家卡组当前解析到的 Published revision。");
                }

                CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                    CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(card.LogicPayload);
                if (!compile.Success || compile.Program == null)
                {
                    AddError(
                        reasons,
                        CharacterTestRuntimeAiLabPlayerLegalityReasonKind.DraftInvalid,
                        "LAB_PLAYER_DRAFT_INVALID",
                        cardStableId,
                        "行为卡 Logic v3 草稿未通过编译。");
                }
                else if (compile.Program.SemanticHash != card.SourcePublishedSemanticHash)
                {
                    AddError(
                        reasons,
                        CharacterTestRuntimeAiLabPlayerLegalityReasonKind.UnpublishedSemanticChanges,
                        "LAB_PLAYER_UNPUBLISHED_SEMANTIC_CHANGES",
                        cardStableId,
                        "行为语义已偏离 Published 来源；可继续实验，但 Publish 前不属于玩家合法构筑。");
                }
            }

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult deckBuild =
                CharacterTestRuntimeAiPlannerInstalledDeckRules.ValidateAndCreate(
                    new CharacterTestRuntimeAiPlannerInstalledDeckCandidate(characterStableId, stableIds, characterNumericId),
                    catalog,
                    runtimeContractVersion,
                    diagnosticsInput);
            AddDeckDiagnostics(reasons, deckBuild.Diagnostics);
            if (deckBuild.Deck != null)
                AddCanonicalArbitrationDiagnostics(reasons, group, stableIds, deckBuild.Deck);
            int totalCost = deckBuild.Deck?.TotalCost ?? ResolveKnownCost(stableIds, catalog, runtimeContractVersion);
            return new CharacterTestRuntimeAiLabPlayerLegalityState(
                HasErrors(reasons)
                    ? CharacterTestRuntimeAiLabPlayerLegalityStatus.ExperimentOnly
                    : CharacterTestRuntimeAiLabPlayerLegalityStatus.PlayerLegal,
                group.Entries.Count,
                totalCost,
                reasons.ToArray());
        }

        private static void AddCanonicalArbitrationDiagnostics(
            List<CharacterTestRuntimeAiLabPlayerLegalityReason> reasons,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IReadOnlyList<string> stableIds,
            CharacterTestRuntimeAiPlannerInstalledDeck deck)
        {
            bool mismatch = stableIds.Count != deck.Cards.Count;
            for (int i = 0; !mismatch && i < stableIds.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                CharacterTestRuntimeAiPlannerInstalledDeckCard canonicalCard = deck.Cards[i];
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry runtimeEntry = entry?.RuntimeEntry;
                int expectedOrder = (i + 1) * 10;
                float expectedScore = CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ResolveCanonicalScore(
                    canonicalCard.PublishedCard.Metadata);
                mismatch = !string.Equals(stableIds[i], canonicalCard.StableCardId, StringComparison.Ordinal)
                    || entry == null
                    || entry.Order != expectedOrder
                    || runtimeEntry == null
                    || runtimeEntry.Order != expectedOrder
                    || runtimeEntry.Parameters.Count != 1
                    || !runtimeEntry.Parameters.TryGetValue(
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey,
                        out CharacterTestRuntimeAiPlannerBehaviorParameterValue score)
                    || score.Kind != CharacterTestRuntimeAiPlannerBehaviorParameterKind.Float
                    || Math.Abs(score.FloatValue - expectedScore) > 0.0001f;
            }
            if (!mismatch)
                return;

            AddError(
                reasons,
                CharacterTestRuntimeAiLabPlayerLegalityReasonKind.GroupArbitrationOverride,
                "LAB_PLAYER_GROUP_ARBITRATION_OVERRIDE",
                string.Empty,
                "行为卡顺序、优先级或仲裁参数与玩家卡组的规范化结果不同；当前组合仅属于 Lab 实验。");
        }

        private static bool HasPlayerInexpressibleArbitration(
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry)
        {
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry runtimeEntry = entry.RuntimeEntry;
            return runtimeEntry == null
                || !string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    StringComparison.Ordinal)
                || !string.Equals(
                    runtimeEntry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    StringComparison.Ordinal)
                || !string.Equals(entry.BehaviorId, runtimeEntry.BehaviorId, StringComparison.Ordinal)
                || entry.Disabled != runtimeEntry.Disabled
                || entry.Order != runtimeEntry.Order;
        }

        private static int ResolveKnownCost(
            IReadOnlyList<string> stableIds,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion)
        {
            int total = 0;
            for (int i = 0; i < stableIds.Count; i++)
            {
                if (catalog.TryResolveLatestCompatible(stableIds[i], runtimeContractVersion, out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card))
                    total += card.Metadata.Cost;
            }
            return total;
        }

        private static void AddDeckDiagnostics(
            List<CharacterTestRuntimeAiLabPlayerLegalityReason> reasons,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            for (int i = 0; diagnostics != null && i < diagnostics.Count; i++)
            {
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic diagnostic = diagnostics[i];
                reasons.Add(new CharacterTestRuntimeAiLabPlayerLegalityReason(
                    MapKind(diagnostic.Code),
                    diagnostic.Severity,
                    diagnostic.Code,
                    string.Empty,
                    diagnostic.Message));
            }
        }

        private static CharacterTestRuntimeAiLabPlayerLegalityReasonKind MapKind(string code)
        {
            switch (code)
            {
                case "INSTALLED_DECK_FALLBACK_ONLY":
                    return CharacterTestRuntimeAiLabPlayerLegalityReasonKind.FallbackOnly;
                case "INSTALLED_DECK_SLOT_LIMIT_EXCEEDED":
                    return CharacterTestRuntimeAiLabPlayerLegalityReasonKind.SlotLimitExceeded;
                case "INSTALLED_DECK_COST_LIMIT_EXCEEDED":
                    return CharacterTestRuntimeAiLabPlayerLegalityReasonKind.CostLimitExceeded;
                case "INSTALLED_DECK_CARD_DUPLICATE":
                    return CharacterTestRuntimeAiLabPlayerLegalityReasonKind.DuplicateCard;
                case "INSTALLED_DECK_CARD_UNAVAILABLE":
                case "INSTALLED_DECK_CARD_ID_MISSING":
                    return CharacterTestRuntimeAiLabPlayerLegalityReasonKind.PublishedRevisionUnavailable;
                case "INSTALLED_DECK_CHARACTER_CAPABILITY_MISMATCH":
                    return CharacterTestRuntimeAiLabPlayerLegalityReasonKind.CharacterCapabilityMismatch;
                default:
                    return CharacterTestRuntimeAiLabPlayerLegalityReasonKind.CapabilityWarning;
            }
        }

        private static bool HasErrors(IReadOnlyList<CharacterTestRuntimeAiLabPlayerLegalityReason> reasons)
        {
            for (int i = 0; i < reasons.Count; i++)
            {
                if (reasons[i].Severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error)
                    return true;
            }
            return false;
        }

        private static void AddError(
            List<CharacterTestRuntimeAiLabPlayerLegalityReason> reasons,
            CharacterTestRuntimeAiLabPlayerLegalityReasonKind kind,
            string code,
            string cardStableId,
            string message)
        {
            reasons.Add(new CharacterTestRuntimeAiLabPlayerLegalityReason(
                kind,
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                code,
                cardStableId,
                message));
        }
    }
}

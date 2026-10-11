using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        private static CharacterTestRuntimeAiLabLogicStage ResolvePrimaryStage(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                if (stage != null && stage.OwnsPrimaryAction)
                    return stage;
            }
            return payload.Stages[0];
        }

        private static void AddConfigurationField(
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel target,
            string label,
            string value)
        {
            if (target.ConfigurationFieldCount >= target.ConfigurationFields.Length)
                return;
            CharacterTestRuntimeAiLabBehaviorCardConfigurationFieldViewModel field =
                target.ConfigurationFields[target.ConfigurationFieldCount++];
            field.Label = label ?? string.Empty;
            field.Value = value ?? string.Empty;
        }

        private static string StageKindLabel(CharacterTestRuntimeAiLabCardStepKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                    return "移动";
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                    return "释放能力";
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    return "角色动作";
                case CharacterTestRuntimeAiLabCardStepKind.Wait:
                    return "等待";
                default:
                    return "逻辑";
            }
        }

        private static string MovementStrategyLabel(CharacterTestRuntimeAiLabMovementStrategy strategy)
        {
            switch (strategy)
            {
                case CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance:
                    return "保持距离";
                case CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard:
                    return "环境避险";
                case CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked:
                    return "受阻横移";
                case CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose:
                    return "过近后撤";
                case CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget:
                    return "环绕目标";
                default:
                    return "接近目标";
            }
        }

        private static string MovementTargetLabel(CharacterTestRuntimeAiLabLogicStage stage)
        {
            switch (stage.MovementTargetKind)
            {
                case CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition:
                    return "坐标 (" + stage.MoveTargetPositionX.ToString("0.##", CultureInfo.InvariantCulture)
                        + ", " + stage.MoveTargetPositionZ.ToString("0.##", CultureInfo.InvariantCulture) + ")";
                case CharacterTestRuntimeAiLabMovementTargetKind.Entity:
                    return stage.MoveTargetEntityId > 0
                        ? "实体 " + stage.MoveTargetEntityId.ToString(CultureInfo.InvariantCulture)
                        : "自动选择友方单位";
                default:
                    return "目标";
            }
        }

        private static bool UsesDistanceSetting(CharacterTestRuntimeAiLabMovementStrategy strategy)
        {
            return strategy == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget
                || strategy == CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance
                || strategy == CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose
                || strategy == CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget;
        }

        private static string FormatMovementDistance(CharacterTestRuntimeAiLabLogicStage stage)
        {
            float minimum = stage.MoveMinDistance;
            float maximum = stage.MoveMaxDistance;
            if (stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget)
            {
                float stopDistance = minimum > 0f ? minimum : maximum;
                return stopDistance.ToString("0.##", CultureInfo.InvariantCulture) + " m";
            }
            if (Math.Abs(maximum - minimum) < 0.01f)
                return minimum.ToString("0.##", CultureInfo.InvariantCulture) + " m";
            return minimum.ToString("0.##", CultureInfo.InvariantCulture) + "–"
                + maximum.ToString("0.##", CultureInfo.InvariantCulture) + " m";
        }

        private static string DurationModeLabel(
            CharacterTestRuntimeAiLabLogicDurationMode mode,
            int plannedDurationFrames)
        {
            switch (mode)
            {
                case CharacterTestRuntimeAiLabLogicDurationMode.Fixed:
                    return Math.Max(1, plannedDurationFrames).ToString(CultureInfo.InvariantCulture) + " 帧";
                case CharacterTestRuntimeAiLabLogicDurationMode.Infinite:
                    return "持续运行";
                default:
                    return "直到事件发生";
            }
        }

        private static void SortLibraryRows(
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel[] rows,
            int count)
        {
            for (int i = 1; i < count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorAssetCardViewModel value = rows[i];
                int target = i - 1;
                while (target >= 0 && CompareLibraryRows(value, rows[target]) < 0)
                {
                    rows[target + 1] = rows[target];
                    target--;
                }
                rows[target + 1] = value;
            }
        }

        private static int CompareLibraryRows(
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel left,
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel right)
        {
            int rank = LibraryRank(left).CompareTo(LibraryRank(right));
            if (rank != 0)
                return rank;
            int title = string.Compare(left?.Title, right?.Title, StringComparison.OrdinalIgnoreCase);
            return title != 0
                ? title
                : string.Compare(left?.StableCardId, right?.StableCardId, StringComparison.Ordinal);
        }

        private static int LibraryRank(CharacterTestRuntimeAiLabBehaviorAssetCardViewModel row)
        {
            if (row == null)
                return 6;
            if (row.CurrentEdit)
                return 0;
            if (row.RuntimeActive)
                return 1;
            if (row.Installed)
                return 2;
            if (row.Unlocked && row.Available)
                return 3;
            if (!row.Unlocked)
                return 4;
            return 5;
        }

        private static int ResolveLibraryRowIndex(
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel[] rows,
            int count,
            string stableCardId,
            int fallback)
        {
            for (int i = 0; i < count; i++)
            {
                if (string.Equals(rows[i]?.StableCardId, stableCardId, StringComparison.Ordinal))
                    return i;
            }
            return fallback;
        }

        private static void EnsureLibraryCapacity(
            CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel target,
            int required)
        {
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel[] rows = target.Cards;
            if (rows.Length < required)
            {
                int capacity = Math.Max(required, Math.Max(4, rows.Length * 2));
                Array.Resize(ref rows, capacity);
                target.Cards = rows;
            }
            for (int i = 0; i < required; i++)
            {
                if (rows[i] == null)
                    rows[i] = new CharacterTestRuntimeAiLabBehaviorAssetCardViewModel();
            }
        }

        private static long ComputeLibraryStructureKey(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView source,
            string query,
            CharacterTestRuntimeAiLabBehaviorCardLibraryFilter filter,
            string editStableId,
            string runtimeStableId,
            int draftRevision)
        {
            unchecked
            {
                long hash = source.DeckHash;
                hash = hash * 397L ^ StableTextHash(query);
                hash = hash * 397L ^ (int)filter;
                hash = hash * 397L ^ StableTextHash(editStableId);
                hash = hash * 397L ^ StableTextHash(runtimeStableId);
                hash = hash * 397L ^ draftRevision;
                hash = hash * 397L ^ source.SlotCount;
                hash = hash * 397L ^ source.MaximumSlots;
                hash = hash * 397L ^ source.KnownCost;
                hash = hash * 397L ^ source.MaximumCost;
                hash = hash * 397L ^ (source.IsSessionLocked ? 1 : 0);
                IReadOnlyList<CharacterTestRuntimeAiPlannerPlayerDeckCardView> cards = source.Cards;
                for (int i = 0; i < cards.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerPlayerDeckCardView card = cards[i];
                    hash = hash * 397L ^ StableTextHash(card.StableCardId);
                    hash = hash * 397L ^ StableTextHash(card.IconKey);
                    hash = hash * 397L ^ StableTextHash(card.DisplayName);
                    hash = hash * 397L ^ StableTextHash(card.ShortDescription);
                    hash = hash * 397L ^ StableTextHash(card.UseSummary);
                    hash = hash * 397L ^ StableTextHash(card.RiskSummary);
                    hash = hash * 397L ^ card.Revision;
                    hash = hash * 397L ^ card.PinnedRevision;
                    hash = hash * 397L ^ card.Cost;
                    hash = hash * 397L ^ (card.IsInstalled ? 1 : 0);
                    hash = hash * 397L ^ (card.IsUnlocked ? 1 : 0);
                    hash = hash * 397L ^ (int)card.Availability;
                    hash = hash * 397L ^ (int)card.BlockReason;
                    IReadOnlyList<string> tags = card.Tags;
                    for (int tagIndex = 0; tags != null && tagIndex < tags.Count; tagIndex++)
                        hash = hash * 397L ^ StableTextHash(tags[tagIndex]);
                    IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> capabilities =
                        card.RequiredCapabilities;
                    for (int capabilityIndex = 0;
                         capabilities != null && capabilityIndex < capabilities.Count;
                         capabilityIndex++)
                    {
                        hash = hash * 397L ^ (int)capabilities[capabilityIndex];
                    }
                }
                return hash;
            }
        }

        private static bool MatchesLibraryCard(
            CharacterTestRuntimeAiPlannerPlayerDeckCardView card,
            bool currentEdit,
            bool runtimeActive,
            string query,
            CharacterTestRuntimeAiLabBehaviorCardLibraryFilter filter)
        {
            if (filter != CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All)
            {
                bool matched = (filter & CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.Installed) != 0
                    && card.IsInstalled;
                matched |= (filter & CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.UnlockedAvailable) != 0
                    && card.IsUnlocked
                    && card.Availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available;
                matched |= (filter & CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.Locked) != 0
                    && !card.IsUnlocked;
                matched |= (filter & CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.UnavailableOrIncompatible) != 0
                    && (card.Availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Unavailable
                        || card.Availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Incompatible);
                matched |= (filter & CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.CurrentDraft) != 0
                    && currentEdit;
                matched |= (filter & CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.RuntimeActive) != 0
                    && runtimeActive;
                if (!matched)
                    return false;
            }

            int queryLength = query?.Length ?? 0;
            int tokenStart = 0;
            while (tokenStart < queryLength)
            {
                while (tokenStart < queryLength && char.IsWhiteSpace(query[tokenStart]))
                    tokenStart++;
                if (tokenStart >= queryLength)
                    break;
                int tokenEnd = tokenStart + 1;
                while (tokenEnd < queryLength && !char.IsWhiteSpace(query[tokenEnd]))
                    tokenEnd++;
                int tokenLength = tokenEnd - tokenStart;
                if (!ContainsLibraryToken(card.StableCardId, query, tokenStart, tokenLength)
                    && !ContainsLibraryToken(card.DisplayName, query, tokenStart, tokenLength)
                    && !ContainsLibraryToken(card.ShortDescription, query, tokenStart, tokenLength)
                    && !ContainsLibraryToken(card.UseSummary, query, tokenStart, tokenLength)
                    && !ContainsLibraryToken(card.RiskSummary, query, tokenStart, tokenLength)
                    && !ContainsLibraryToken(card.Tags, query, tokenStart, tokenLength)
                    && !ContainsCapabilityToken(card.RequiredCapabilities, query, tokenStart, tokenLength)
                    && !ContainsResponsibilityToken(card.RequiredCapabilities, query, tokenStart, tokenLength))
                {
                    return false;
                }
                tokenStart = tokenEnd;
            }
            return true;
        }

        private static bool ContainsCapabilityToken(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> capabilities,
            string query,
            int tokenStart,
            int tokenLength)
        {
            for (int i = 0; capabilities != null && i < capabilities.Count; i++)
            {
                string label = CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer
                    .FormatCapabilityLabel(capabilities[i]);
                if (ContainsLibraryToken(label, query, tokenStart, tokenLength)
                    || ContainsLibraryToken(capabilities[i].ToString(), query, tokenStart, tokenLength))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsResponsibilityToken(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> capabilities,
            string query,
            int tokenStart,
            int tokenLength)
        {
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] responsibilities =
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy
                    .ResolveCompatibleResponsibilities(capabilities);
            for (int i = 0; i < responsibilities.Length; i++)
            {
                if (ContainsLibraryToken(
                        CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.GetDisplayName(
                            responsibilities[i]),
                        query,
                        tokenStart,
                        tokenLength))
                {
                    return true;
                }
            }
            return false;
        }

        private static string BuildResponsibilitySummary(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities)
        {
            var builder = new StringBuilder("可承担：");
            for (int i = 0; responsibilities != null && i < responsibilities.Count; i++)
            {
                if (i > 0)
                    builder.Append('、');
                builder.Append(CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.GetDisplayName(
                    responsibilities[i]));
            }
            return builder.ToString();
        }

        private static bool ContainsLibraryToken(
            IReadOnlyList<string> values,
            string query,
            int tokenStart,
            int tokenLength)
        {
            for (int i = 0; values != null && i < values.Count; i++)
            {
                if (ContainsLibraryToken(values[i], query, tokenStart, tokenLength))
                    return true;
            }
            return false;
        }

        private static bool ContainsLibraryToken(
            string value,
            string query,
            int tokenStart,
            int tokenLength)
        {
            if (string.IsNullOrEmpty(value) || tokenLength <= 0 || value.Length < tokenLength)
                return false;
            for (int offset = 0; offset <= value.Length - tokenLength; offset++)
            {
                int index = 0;
                for (; index < tokenLength; index++)
                {
                    if (char.ToUpperInvariant(value[offset + index])
                        != char.ToUpperInvariant(query[tokenStart + index]))
                    {
                        break;
                    }
                }
                if (index == tokenLength)
                    return true;
            }
            return false;
        }

        private static long StableTextHash(string value)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 1099511628211UL;
                }
                return (long)hash;
            }
        }

        private static long StableTextHash(MxFramework.CharacterAction.CharacterActionId value)
        {
            return value.Value;
        }

    }
}

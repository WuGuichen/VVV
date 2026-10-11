using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModelBuilder
    {
        public static CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel Build(
            CharacterTestSliceServices services,
            int selectedEntityId,
            int selectedLoadoutId,
            int selectedVariantId,
            string statusText = "",
            IMxUiTextProvider textProvider = null)
        {
            var viewModel = new CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel
            {
                Title = L(textProvider, "ui.character_test.runtime_ai_behavior.title", "Runtime AI Planner Strategies"),
                StatusText = string.IsNullOrWhiteSpace(statusText)
                    ? L(textProvider, "ui.character_test.runtime_ai_behavior.ready", "Ready")
                    : statusText,
                Message = L(
                    textProvider,
                    "ui.character_test.runtime_ai_behavior.message",
                    "Choose an AI unit and a published strategy. This panel does not edit behavior groups."),
                PhaseText = L(
                    textProvider,
                    "ui.character_test.runtime_ai_behavior.phase.unavailable",
                    "Selection unavailable"),
                CanApply = false,
                ApplyLabel = L(textProvider, "ui.character_test.runtime_ai_behavior.apply", "Apply")
            };

            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadoutRuntime =
                services?.Modules.RuntimeAiBehaviorLoadouts;
            IReadOnlyList<CharacterTestCharacterEntity> entities = services?.CharacterEntities;
            if (services == null || loadoutRuntime == null)
            {
                viewModel.WarningText = L(
                    textProvider,
                    "ui.character_test.runtime_ai_behavior.warning.unavailable",
                    "Runtime AI Planner behavior loadouts are unavailable.");
                return viewModel;
            }

            bool phaseUnlocked = services.RuntimeCommands != null
                && services.RuntimeCommands.RuntimeAiBehaviorSelectionUnlocked;
            viewModel.PhaseUnlocked = phaseUnlocked;
            viewModel.PhaseText = phaseUnlocked
                ? L(textProvider, "ui.character_test.runtime_ai_behavior.phase.unlocked", "Selection phase unlocked")
                : L(textProvider, "ui.character_test.runtime_ai_behavior.phase.locked", "Selection phase locked");

            CharacterTestCharacterEntity selectedEntity =
                ResolveSelectedEntity(entities, selectedEntityId);
            viewModel.SelectedEntityId = selectedEntity != null ? selectedEntity.Id.Value : 0;
            viewModel.TotalEntityCount = CountSelectableEntities(entities);
            viewModel.SetEntities(CreateEntityRows(
                entities,
                viewModel.SelectedEntityId,
                CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.EntityRowCount));

            CharacterTestRuntimeAiBehaviorSelection activeSelection =
                selectedEntity != null
                    ? selectedEntity.Control.RuntimeAiBehaviorSelection
                    : CharacterTestRuntimeAiBehaviorSelection.None;
            ResolveSelectedLoadout(
                loadoutRuntime,
                activeSelection,
                selectedLoadoutId,
                selectedVariantId,
                out CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition selectedLoadout,
                out CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition selectedVariant,
                out CharacterTestRuntimeAiBehaviorSelection selectedSelection);
            viewModel.SelectedLoadoutId = selectedSelection.LoadoutId;
            viewModel.SelectedVariantId = selectedSelection.VariantId;
            viewModel.TotalLoadoutCount = CountAvailableLoadoutVariants(loadoutRuntime);
            viewModel.SetLoadouts(CreateLoadoutCards(
                loadoutRuntime,
                viewModel.SelectedLoadoutId,
                viewModel.SelectedVariantId,
                activeSelection,
                CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.LoadoutCardCount));

            FillSelectedStrategyDetails(
                viewModel,
                services,
                loadoutRuntime,
                selectedLoadout,
                selectedVariant,
                selectedSelection,
                activeSelection,
                phaseUnlocked,
                textProvider);
            return viewModel;
        }

        private static CharacterTestCharacterEntity ResolveSelectedEntity(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int selectedEntityId)
        {
            CharacterTestCharacterEntity first = null;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (!IsSelectableEntity(entity))
                    continue;

                if (first == null)
                    first = entity;
                if (entity.Id.Value == selectedEntityId)
                    return entity;
            }

            return first;
        }

        private static int CountSelectableEntities(IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            int count = 0;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                if (IsSelectableEntity(entities[i]))
                    count++;
            }

            return count;
        }

        private static bool IsSelectableEntity(CharacterTestCharacterEntity entity)
        {
            return entity != null
                && entity.Control.Owner == CharacterTestCharacterControlOwner.AiControlled;
        }

        private static CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel[] CreateEntityRows(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int selectedEntityId,
            int rowCount)
        {
            var rows = new CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel[rowCount];
            int row = 0;
            for (int i = 0; entities != null && i < entities.Count && row < rowCount; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (!IsSelectableEntity(entity))
                    continue;

                rows[row] = new CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel
                {
                    Index = row,
                    EntityId = entity.Id.Value,
                    Visible = true,
                    Selected = entity.Id.Value == selectedEntityId,
                    Label = "Agent "
                        + entity.Id.Value.ToString(CultureInfo.InvariantCulture)
                        + "  "
                        + EmptyAs(entity.Identity.DisplayName, "Unnamed"),
                    DetailText = "team="
                        + entity.Team.TeamId.ToString(CultureInfo.InvariantCulture)
                        + " owner="
                        + entity.Control.Owner
                        + " current="
                        + FormatSelection(entity.Control.RuntimeAiBehaviorSelection)
                };
                row++;
            }

            for (; row < rowCount; row++)
            {
                rows[row] = new CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel
                {
                    Index = row,
                    Visible = false
                };
            }

            return rows;
        }

        private static void ResolveSelectedLoadout(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadoutRuntime,
            CharacterTestRuntimeAiBehaviorSelection activeSelection,
            int selectedLoadoutId,
            int selectedVariantId,
            out CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition selectedLoadout,
            out CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition selectedVariant,
            out CharacterTestRuntimeAiBehaviorSelection selectedSelection)
        {
            selectedLoadout = null;
            selectedVariant = null;
            selectedSelection = CharacterTestRuntimeAiBehaviorSelection.None;

            if (TryFindLoadoutVariant(
                    loadoutRuntime,
                    selectedLoadoutId,
                    selectedVariantId,
                    out selectedLoadout,
                    out selectedVariant,
                    out selectedSelection))
            {
                return;
            }

            if (activeSelection.HasSelection
                && TryFindLoadoutVariant(
                    loadoutRuntime,
                    activeSelection.LoadoutId,
                    activeSelection.VariantId,
                    out selectedLoadout,
                    out selectedVariant,
                    out selectedSelection))
            {
                return;
            }

            CharacterTestRuntimeAiBehaviorSelection fallback = loadoutRuntime.DefaultSelection;
            if (fallback.HasSelection)
            {
                TryFindLoadoutVariant(
                    loadoutRuntime,
                    fallback.LoadoutId,
                    fallback.VariantId,
                    out selectedLoadout,
                    out selectedVariant,
                    out selectedSelection);
                return;
            }

            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition> loadouts =
                loadoutRuntime.Catalog?.Loadouts;
            for (int i = 0; loadouts != null && i < loadouts.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition loadout = loadouts[i];
                if (loadout == null)
                    continue;

                for (int j = 0; j < loadout.Variants.Count; j++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition variant =
                        loadout.Variants[j];
                    if (variant == null)
                        continue;

                    if (loadoutRuntime.TryResolveSelection(
                            loadout.LoadoutId,
                            variant.VariantId,
                            out selectedSelection,
                            out _))
                    {
                        selectedLoadout = loadout;
                        selectedVariant = variant;
                        return;
                    }
                }
            }
        }

        private static bool TryFindLoadoutVariant(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadoutRuntime,
            int loadoutId,
            int variantId,
            out CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition loadout,
            out CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition variant,
            out CharacterTestRuntimeAiBehaviorSelection selection)
        {
            loadout = null;
            variant = null;
            selection = CharacterTestRuntimeAiBehaviorSelection.None;
            if (loadoutRuntime == null || loadoutId <= 0 || variantId <= 0)
                return false;

            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition> loadouts =
                loadoutRuntime.Catalog?.Loadouts;
            for (int i = 0; loadouts != null && i < loadouts.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition candidateLoadout = loadouts[i];
                if (candidateLoadout == null || candidateLoadout.LoadoutId != loadoutId)
                    continue;

                for (int j = 0; j < candidateLoadout.Variants.Count; j++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition candidateVariant =
                        candidateLoadout.Variants[j];
                    if (candidateVariant == null || candidateVariant.VariantId != variantId)
                        continue;

                    if (!loadoutRuntime.TryResolveSelection(
                            loadoutId,
                            variantId,
                            out selection,
                            out _))
                    {
                        return false;
                    }

                    loadout = candidateLoadout;
                    variant = candidateVariant;
                    return true;
                }
            }

            return false;
        }

        private static int CountAvailableLoadoutVariants(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadoutRuntime)
        {
            int count = 0;
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition> loadouts =
                loadoutRuntime?.Catalog?.Loadouts;
            for (int i = 0; loadouts != null && i < loadouts.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition loadout = loadouts[i];
                if (loadout == null)
                    continue;

                for (int j = 0; j < loadout.Variants.Count; j++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition variant =
                        loadout.Variants[j];
                    if (variant != null
                        && loadoutRuntime.TryResolveSelection(
                            loadout.LoadoutId,
                            variant.VariantId,
                            out _,
                            out _))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel[] CreateLoadoutCards(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadoutRuntime,
            int selectedLoadoutId,
            int selectedVariantId,
            CharacterTestRuntimeAiBehaviorSelection activeSelection,
            int cardCount)
        {
            var cards = new CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel[cardCount];
            int row = 0;
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition> loadouts =
                loadoutRuntime?.Catalog?.Loadouts;
            for (int i = 0; loadouts != null && i < loadouts.Count && row < cardCount; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition loadout = loadouts[i];
                if (loadout == null)
                    continue;

                for (int j = 0; j < loadout.Variants.Count && row < cardCount; j++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition variant =
                        loadout.Variants[j];
                    if (variant == null
                        || !loadoutRuntime.TryResolveSelection(
                            loadout.LoadoutId,
                            variant.VariantId,
                            out CharacterTestRuntimeAiBehaviorSelection selection,
                            out _))
                    {
                        continue;
                    }

                    CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationViewModel presentation =
                        CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationCatalog.CreateLoadoutCard(
                            loadout,
                            variant);
                    cards[row] = new CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel
                    {
                        Index = row,
                        LoadoutId = loadout.LoadoutId,
                        VariantId = variant.VariantId,
                        Visible = true,
                        Selected = loadout.LoadoutId == selectedLoadoutId
                            && variant.VariantId == selectedVariantId,
                        ActiveOnSelectedEntity = activeSelection.HasSelection
                            && activeSelection.LoadoutId == loadout.LoadoutId
                            && activeSelection.VariantId == variant.VariantId
                            && activeSelection.CatalogHash == selection.CatalogHash,
                        Label = presentation.StrategyName
                            + " / "
                            + EmptyAs(variant.DisplayName, "Default"),
                        DetailText = presentation.ShortDescription
                    };
                    row++;
                }
            }

            for (; row < cardCount; row++)
            {
                cards[row] = new CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel
                {
                    Index = row,
                    Visible = false
                };
            }

            return cards;
        }

        private static void FillSelectedStrategyDetails(
            CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel viewModel,
            CharacterTestSliceServices services,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadoutRuntime,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition loadout,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition variant,
            CharacterTestRuntimeAiBehaviorSelection selectedSelection,
            CharacterTestRuntimeAiBehaviorSelection activeSelection,
            bool phaseUnlocked,
            IMxUiTextProvider textProvider)
        {
            if (loadout == null || variant == null || !selectedSelection.HasSelection)
            {
                viewModel.StrategyName = L(
                    textProvider,
                    "ui.character_test.runtime_ai_behavior.strategy.none",
                    "No strategy selected");
                viewModel.WarningText = L(
                    textProvider,
                    "ui.character_test.runtime_ai_behavior.warning.no_strategy",
                    "No published behavior loadout variant is available.");
                return;
            }

            CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationViewModel presentation =
                CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationCatalog.CreateLoadoutCard(loadout, variant);
            viewModel.StrategyName = presentation.StrategyName;
            viewModel.StrategyDescription = presentation.ShortDescription;
            viewModel.PrimaryActionSummary = "Main behavior: " + presentation.PrimaryActionSummary;
            viewModel.SuitableScenario = "Good for: " + presentation.SuitableScenario;
            viewModel.RiskText = "Limits: " + presentation.RiskText;
            viewModel.TagsText = "Tags: " + JoinTags(presentation.Tags);
            viewModel.BehaviorOrderText = BuildBehaviorOrderText(variant.BehaviorGroup);
            viewModel.ActionChainText = BuildActionChainText(
                services,
                loadoutRuntime,
                selectedSelection);
            viewModel.WarningText = BuildWarningText(
                viewModel,
                selectedSelection,
                activeSelection,
                phaseUnlocked,
                textProvider);
            viewModel.ApplyLabel = selectedSelection.HasSelection
                && activeSelection.HasSelection
                && activeSelection.LoadoutId == selectedSelection.LoadoutId
                && activeSelection.VariantId == selectedSelection.VariantId
                && activeSelection.CatalogHash == selectedSelection.CatalogHash
                    ? L(textProvider, "ui.character_test.runtime_ai_behavior.apply.current", "Apply current")
                    : L(textProvider, "ui.character_test.runtime_ai_behavior.apply", "Apply");
            viewModel.CanApply = phaseUnlocked
                && viewModel.SelectedEntityId > 0
                && selectedSelection.HasSelection;
        }

        private static string BuildBehaviorOrderText(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile group)
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries = group?.Entries;
            if (entries == null || entries.Count == 0)
                return "Behavior order: none";

            var active = new List<CharacterTestRuntimeAiPlannerBehaviorModuleEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && !entries[i].Disabled)
                    active.Add(entries[i]);
            }

            active.Sort(CompareEntryOrder);
            var builder = new StringBuilder(160);
            builder.Append("Behavior order: ");
            int limit = Math.Min(active.Count, 6);
            for (int i = 0; i < limit; i++)
            {
                if (i > 0)
                    builder.Append(" -> ");
                builder.Append(ResolveBehaviorDisplay(active[i]));
            }

            if (active.Count > limit)
                builder.Append(" -> +").Append(active.Count - limit).Append(" more");
            return builder.ToString();
        }

        private static int CompareEntryOrder(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry left,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry right)
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0
                ? order
                : string.Compare(left.EntryId, right.EntryId, StringComparison.Ordinal);
        }

        private static string ResolveBehaviorDisplay(CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
        {
            if (entry == null)
                return "unknown";

            if (CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.TryGetDefinition(
                    entry.BehaviorId,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
            {
                string action = definition.ActionPrimitive != CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.None
                    ? " (" + definition.ActionPrimitive + ")"
                    : string.Empty;
                return EmptyAs(definition.DisplayName, entry.BehaviorId) + action;
            }

            return EmptyAs(entry.BehaviorId, entry.EntryId);
        }

        private static string BuildActionChainText(
            CharacterTestSliceServices services,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadoutRuntime,
            CharacterTestRuntimeAiBehaviorSelection selection)
        {
            CharacterTestRuntimeAiPlannerProfile profile = FindProfile(loadoutRuntime, selection.ProfileSlot);
            if (profile == null)
                return "Action chain: no compiled profile";

            CharacterTestRuntimeAiPlannerBehaviorActionChainPreview preview =
                CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewBuilder.Build(
                    profile,
                    services?.AbilityHotbar,
                    services?.ActionRuntime);
            var builder = new StringBuilder(160);
            builder.Append("Action chain: ").Append(preview.SummaryText);
            int limit = Math.Min(preview.Items.Count, 4);
            for (int i = 0; i < limit; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem item = preview.Items[i];
                builder.Append(i == 0 ? " / " : " / ");
                builder.Append(item.PlayerSummary);
            }

            if (preview.Items.Count > limit)
                builder.Append(" / +").Append(preview.Items.Count - limit).Append(" more");
            return builder.ToString();
        }

        private static CharacterTestRuntimeAiPlannerProfile FindProfile(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadoutRuntime,
            int profileSlot)
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> bindings =
                loadoutRuntime?.ProfileBindings;
            for (int i = 0; bindings != null && i < bindings.Count; i++)
            {
                if (bindings[i].Slot == profileSlot)
                    return bindings[i].Profile;
            }

            return null;
        }

        private static string BuildWarningText(
            CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel viewModel,
            CharacterTestRuntimeAiBehaviorSelection selectedSelection,
            CharacterTestRuntimeAiBehaviorSelection activeSelection,
            bool phaseUnlocked,
            IMxUiTextProvider textProvider)
        {
            if (viewModel.SelectedEntityId <= 0)
            {
                return L(
                    textProvider,
                    "ui.character_test.runtime_ai_behavior.warning.no_agent",
                    "No AI-controlled unit is available.");
            }

            if (!phaseUnlocked)
            {
                return L(
                    textProvider,
                    "ui.character_test.runtime_ai_behavior.warning.locked",
                    "Selection phase is locked by the Runtime AI Planner editor controls.");
            }

            if (!selectedSelection.HasSelection)
            {
                return L(
                    textProvider,
                    "ui.character_test.runtime_ai_behavior.warning.no_selection",
                    "No compiled loadout variant is selected.");
            }

            if (activeSelection.HasSelection
                && activeSelection.LoadoutId == selectedSelection.LoadoutId
                && activeSelection.VariantId == selectedSelection.VariantId
                && activeSelection.CatalogHash == selectedSelection.CatalogHash)
            {
                return L(
                    textProvider,
                    "ui.character_test.runtime_ai_behavior.warning.current",
                    "This is already the unit's active strategy.");
            }

            return L(
                textProvider,
                "ui.character_test.runtime_ai_behavior.warning.ready",
                "Ready to submit an authoritative behavior selection command.");
        }

        private static string FormatSelection(CharacterTestRuntimeAiBehaviorSelection selection)
        {
            if (!selection.HasSelection)
                return "profile fallback";

            return "loadout="
                + selection.LoadoutId.ToString(CultureInfo.InvariantCulture)
                + "/variant="
                + selection.VariantId.ToString(CultureInfo.InvariantCulture);
        }

        private static string JoinTags(IReadOnlyList<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return "none";

            var builder = new StringBuilder(64);
            for (int i = 0; i < tags.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append(tags[i]);
            }

            return builder.ToString();
        }

        private static string EmptyAs(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string L(IMxUiTextProvider provider, string key, string fallback)
        {
            return CharacterTestUiText.Resolve(provider, key, fallback);
        }
    }
}

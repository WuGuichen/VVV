using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.UI;
using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    public static partial class CharacterTestFairyGuiCombatHudViewModelBuilder
    {
        private static readonly ProfilerMarker ResetMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHud.BuildViewModel.Reset");
        private static readonly ProfilerMarker NetworkMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHud.BuildViewModel.Network");
        private static readonly ProfilerMarker HotbarMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHud.BuildViewModel.Hotbar");
        private static readonly ProfilerMarker ResolveEntitiesMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHud.BuildViewModel.ResolveEntities");
        private static readonly ProfilerMarker FindLocalPlayerMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHud.BuildViewModel.FindLocalPlayer");
        private static readonly ProfilerMarker PlayerMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHud.BuildViewModel.Player");
        private static readonly ProfilerMarker ResolveTargetMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHud.BuildViewModel.ResolveTarget");
        private static readonly ProfilerMarker TargetMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHud.BuildViewModel.Target");
        private static readonly ProfilerMarker ClearTargetMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHud.BuildViewModel.ClearTarget");
        private static readonly ProfilerMarker FeedbackMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHud.BuildViewModel.Feedback");

        private const int FeedbackEventVisibleFrames = 180;
        private const int DefaultNetworkTickRate = 60;
        private const string DefaultActionNoneText = "Action: none";
        private const string PermanentBuffTimeText = "PERM";
        private const string TimedBuffTooltipText = "Timed";
        private const int FrameTextCacheLimit = 4096;
        private const int StackTextCacheLimit = 64;
        private const string AttributeTemplateToken = "{attribute}";
        private const string EffectIdTemplateToken = "{effectId}";
        private const string FramesTemplateToken = "{frames}";
        private const string KindTemplateToken = "{kind}";
        private const string MaxStackTemplateToken = "{maxStack}";
        private const string SecondsTemplateToken = "{seconds}";
        private const string SpaceTemplateToken = "{space}";
        private const string StatusTemplateToken = "{status}";
        private const string StatusIdTemplateToken = "{statusId}";
        private const string ValueTemplateToken = "{value}";
        private static readonly string[] AbilitySlotKeyTexts =
        {
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9"
        };
        private static readonly string[] FrameTextCache = CreateFrameTextCache();
        private static readonly string[] StackTextCache = CreateStackTextCache();

        public static CharacterTestFairyGuiCombatHudViewModel Build(
            GameSlice slice,
            IMxUiTextProvider textProvider = null,
            CharacterTestNetworkSessionStatus networkStatus = null)
        {
            var viewModel = new CharacterTestFairyGuiCombatHudViewModel();
            return BuildInto(viewModel, slice, textProvider, networkStatus);
        }

        public static CharacterTestFairyGuiCombatHudViewModel BuildInto(
            CharacterTestFairyGuiCombatHudViewModel viewModel,
            GameSlice slice,
            IMxUiTextProvider textProvider = null,
            CharacterTestNetworkSessionStatus networkStatus = null,
            CharacterTestAbilityHotbarSlotState[] hotbarSlotStates = null)
        {
            return BuildInto(
                viewModel,
                slice,
                textProvider,
                networkStatus,
                hotbarSlotStates,
                buffers: null);
        }

        internal static CharacterTestFairyGuiCombatHudViewModel BuildInto(
            CharacterTestFairyGuiCombatHudViewModel viewModel,
            GameSlice slice,
            IMxUiTextProvider textProvider,
            CharacterTestNetworkSessionStatus networkStatus,
            CharacterTestAbilityHotbarSlotState[] hotbarSlotStates,
            CharacterTestFairyGuiCombatHudBuildBuffers buffers,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench playerBehaviorDeckWorkbench = null,
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService playerBehaviorDeckService = null)
        {
            if (viewModel == null)
                viewModel = new CharacterTestFairyGuiCombatHudViewModel();

            using (ResetMarker.Auto())
            {
                viewModel.ResetForBuild();
            }

            using (NetworkMarker.Auto())
            {
                PopulateNetworkLatency(viewModel, networkStatus);
            }

            if (slice == null)
            {
                viewModel.ClearAbilitySlots();
                viewModel.ClearPlayerVitals();
                viewModel.ClearTargetVitals();
                SetFeedback(
                    viewModel,
                    L(textProvider, "ui.character_test.combat.runtime_unavailable", "CharacterTest runtime unavailable."));
                return viewModel;
            }

            IReadOnlyList<CharacterTestCharacterEntity> entities;
            using (ResolveEntitiesMarker.Auto())
            {
                entities = slice.CharacterEntities;
            }

            PopulateBehaviorCard(
                viewModel,
                slice.RuntimeAiPlannerCommands,
                entities,
                playerBehaviorDeckWorkbench,
                playerBehaviorDeckService,
                textProvider);

            CharacterTestCharacterEntity localPlayer;
            using (FindLocalPlayerMarker.Auto())
            {
                localPlayer = FindLocalPlayer(entities);
            }

            using (HotbarMarker.Auto())
            {
                PopulateHotbar(viewModel, slice, localPlayer, textProvider, hotbarSlotStates);
            }

            if (localPlayer == null)
            {
                viewModel.ClearPlayerVitals();
                viewModel.ClearTargetVitals();
                SetFeedback(
                    viewModel,
                    L(textProvider, "ui.character_test.combat.waiting_player", "Waiting for local player."));
                return viewModel;
            }

            using (PlayerMarker.Auto())
            {
                PopulatePlayer(viewModel, slice, localPlayer, textProvider, buffers);
            }

            CharacterTestCharacterEntity target;
            bool targetLocked;
            using (ResolveTargetMarker.Auto())
            {
                target = ResolveTarget(
                    slice,
                    entities,
                    localPlayer,
                    buffers,
                    out targetLocked);
            }

            if (target != null)
            {
                using (TargetMarker.Auto())
                {
                    PopulateTarget(viewModel, slice, target, targetLocked, textProvider, buffers);
                }
            }
            else
            {
                using (ClearTargetMarker.Auto())
                {
                    viewModel.ClearTargetVitals();
                }
            }

            using (FeedbackMarker.Auto())
            {
                if (TryBuildFeedback(slice, localPlayer, textProvider, out string feedback))
                    SetFeedback(viewModel, feedback);
            }

            return viewModel;
        }

        private static void PopulateBehaviorCard(
            CharacterTestFairyGuiCombatHudViewModel viewModel,
            CharacterTestRuntimeAiPlannerCommandModule commandModule,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench workbench,
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
            IMxUiTextProvider textProvider)
        {
            if (commandModule == null
                || service == null
                || !service.TryResolvePresentationEntityId(entities, out int presentationEntityId))
                return;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null
                    || entity.Id.Value != presentationEntityId
                    || !commandModule.TryGetBehaviorCardRuntimePresentation(
                        entity.Id.Value,
                        entity.Control.RuntimeAiPlannerProfileSlot,
                        out CharacterTestRuntimeAiPlannerBehaviorCardRuntimePresentation presentation)
                    || !presentation.HasActiveCard)
                {
                    continue;
                }

                TryResolveBehaviorCardMetadata(
                    workbench,
                    presentation.StableCardId,
                    out string displayName,
                    out string iconKey);
                viewModel.BehaviorCardVisible = true;
                viewModel.BehaviorCardIconKey = iconKey;
                viewModel.BehaviorCardIconText = ResolveBehaviorCardIcon(iconKey);
                viewModel.BehaviorCardName = ResolveBehaviorCardDisplayName(displayName, textProvider);
                viewModel.BehaviorCardReasonText = ResolveBehaviorCardReason(
                    presentation.SwitchReason,
                    textProvider);
                return;
            }
        }

        internal static bool TryResolveBehaviorCardMetadata(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench workbench,
            string stableCardId,
            out string displayName,
            out string iconKey)
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerPlayerDeckCardView> cards = workbench?.View?.Cards;
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPlayerDeckCardView card = cards[i];
                if (card != null && string.Equals(card.StableCardId, stableCardId, StringComparison.Ordinal))
                {
                    displayName = card.DisplayName;
                    iconKey = card.IconKey;
                    return true;
                }
            }
            displayName = string.Empty;
            iconKey = string.Empty;
            return false;
        }

        internal static string ResolveBehaviorCardDisplayName(
            string displayName,
            IMxUiTextProvider textProvider)
        {
            return !string.IsNullOrEmpty(displayName)
                ? displayName
                : L(
                    textProvider,
                    "ui.character_test.combat_hud.behavior_card.unknown",
                    "未知行为卡");
        }

        private static string ResolveBehaviorCardIcon(
            string iconKey)
        {
            return "◇";
        }

        private static string ResolveBehaviorCardReason(
            CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason reason,
            IMxUiTextProvider textProvider)
        {
            switch (reason)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason.IncomingThreat:
                    return L(textProvider, "ui.character_test.combat.behavior_card.reason.incoming_threat", "来袭威胁");
                case CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason.AbilityOpportunity:
                    return L(textProvider, "ui.character_test.combat.behavior_card.reason.ability_opportunity", "技能机会");
                case CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason.TargetInAttackRange:
                    return L(textProvider, "ui.character_test.combat.behavior_card.reason.target_in_range", "目标进入攻击范围");
                case CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason.TargetOutsideRange:
                    return L(textProvider, "ui.character_test.combat.behavior_card.reason.target_outside_range", "目标在攻击范围外");
                case CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason.TargetAcquired:
                    return L(textProvider, "ui.character_test.combat.behavior_card.reason.target_acquired", "发现目标");
                default:
                    return L(textProvider, "ui.character_test.combat.behavior_card.reason.conditions_satisfied", "条件满足");
            }
        }

        public static bool TryBuild(
            GameSlice slice,
            CharacterTestNetworkSessionStatus networkStatus,
            out CharacterTestFairyGuiCombatHudViewModel viewModel)
        {
            viewModel = Build(slice, null, networkStatus);
            return viewModel != null;
        }

        public static bool TryBuild(GameSlice slice, out CharacterTestFairyGuiCombatHudViewModel viewModel)
        {
            viewModel = Build(slice);
            return viewModel != null;
        }

        private static void PopulateHotbar(
            CharacterTestFairyGuiCombatHudViewModel viewModel,
            GameSlice slice,
            CharacterTestCharacterEntity localPlayer,
            IMxUiTextProvider textProvider,
            CharacterTestAbilityHotbarSlotState[] hotbarSlotStates)
        {
            int slotCount = CharacterTestFairyGuiCombatHudIds.AbilitySlotCount;
            CharacterTestFairyGuiCombatHudAbilitySlotViewModel[] slots =
                viewModel.EnsureAbilitySlots(slotCount);
            CharacterTestAbilityHotbarSlotState[] states = hotbarSlotStates;
            int stateCount = 0;
            if (slice?.AbilityHotbar != null)
            {
                if (states != null && states.Length >= slotCount)
                {
                    stateCount = slice.AbilityHotbar.FillSlotStates(
                        slice.AbilityRuntime,
                        localPlayer != null ? localPlayer.Id.Value : 0,
                        slice.CurrentFrame,
                        states);
                }
                else
                {
                    states = slice.AbilityHotbar.CreateSlotStates(
                        slice.AbilityRuntime,
                        localPlayer != null ? localPlayer.Id.Value : 0,
                        slice.CurrentFrame);
                    stateCount = states.Length;
                }
            }

            for (int i = 0; i < slots.Length; i++)
            {
                int slotNumber = i + 1;
                CharacterTestAbilityHotbarSlotState state =
                    states != null && i < stateCount
                        ? states[i]
                        : CreateEmptySlotState(slotNumber);
                string abilityText = state.IsEmpty
                    ? L(textProvider, "ui.character_test.ability.empty", "Empty")
                    : state.ShortName;
                bool selected = IsSlotSelected(slice, localPlayer, state.AbilityId);
                CharacterTestFairyGuiCombatHudAbilitySlotViewModel slot = slots[i];
                slot.SlotNumber = slotNumber;
                slot.KeyText = i < AbilitySlotKeyTexts.Length
                    ? AbilitySlotKeyTexts[i]
                    : slotNumber.ToString(CultureInfo.InvariantCulture);
                slot.AbilityText = abilityText;
                slot.IsBound = !state.IsEmpty && state.KnownAbility;
                slot.IsSelected = selected;
                slot.CooldownRatio = state.Cooldown01;
                slot.CooldownText = state.CooldownRemainingFrames > 0L
                    ? FormatFrameText(state.CooldownRemainingFrames)
                    : string.Empty;
            }
        }

        private static CharacterTestAbilityHotbarSlotState CreateEmptySlotState(int slotNumber)
        {
            return new CharacterTestAbilityHotbarSlotState(
                slotNumber,
                CharacterTestAbilityHotbarRuntime.EmptyAbilityId,
                false,
                string.Empty,
                string.Empty,
                0L,
                0L);
        }

        private static bool IsSlotSelected(
            GameSlice slice,
            CharacterTestCharacterEntity localPlayer,
            int abilityId)
        {
            if (slice?.AbilityRuntime?.Definitions == null
                || localPlayer == null
                || abilityId <= 0
                || !localPlayer.Action.ActiveActionId.IsValid)
            {
                return false;
            }

            return slice.AbilityRuntime.Definitions.TryGet(abilityId, out AbilityDefinitionConfig ability)
                && ability.ActionBinding.ActionId == localPlayer.Action.ActiveActionId;
        }

        private static void PopulatePlayer(
            CharacterTestFairyGuiCombatHudViewModel viewModel,
            GameSlice slice,
            CharacterTestCharacterEntity entity,
            IMxUiTextProvider textProvider,
            CharacterTestFairyGuiCombatHudBuildBuffers buffers)
        {
            CharacterTestCharacterCombatStateComponent combat = entity.CombatState;
            viewModel.HasLocalPlayer = true;
            viewModel.PlayerEntityId = entity.Id.Value;
            viewModel.PlayerName = ResolveDisplayName(
                entity,
                L(textProvider, "ui.character_test.combat.wanderer", "Wanderer"));
            viewModel.SetPlayerVitals(
                combat.Health,
                combat.MaxHealth,
                Ratio(combat.Health, combat.MaxHealth),
                combat.ScatterPressure,
                combat.MaxScatterPressure,
                Ratio(combat.ScatterPressure, combat.MaxScatterPressure));
            viewModel.PlayerScatterPressureBand = FormatPressureBand(combat.ScatterPressureBand);
            viewModel.PlayerActionText = FormatPlayerActionText(entity, textProvider, buffers);
            viewModel.SetPlayerBuffs(BuildBuffViewModels(
                slice,
                entity,
                GameplayBuffPresentationSurface.PlayerHud,
                textProvider,
                buffers,
                playerBuffs: true));
        }

        private static void PopulateTarget(
            CharacterTestFairyGuiCombatHudViewModel viewModel,
            GameSlice slice,
            CharacterTestCharacterEntity entity,
            bool targetLocked,
            IMxUiTextProvider textProvider,
            CharacterTestFairyGuiCombatHudBuildBuffers buffers)
        {
            CharacterTestCharacterCombatStateComponent combat = entity.CombatState;
            viewModel.TargetVisible = true;
            viewModel.TargetLocked = targetLocked;
            viewModel.TargetEntityId = entity.Id.Value;
            viewModel.TargetName = ResolveDisplayName(
                entity,
                L(textProvider, "ui.character_test.combat.target", "Target"));
            viewModel.SetTargetVitals(
                combat.Health,
                combat.MaxHealth,
                Ratio(combat.Health, combat.MaxHealth),
                combat.ScatterPressure,
                combat.MaxScatterPressure,
                Ratio(combat.ScatterPressure, combat.MaxScatterPressure));
            viewModel.TargetScatterPressureBand = FormatPressureBand(combat.ScatterPressureBand);
            viewModel.TargetStatusText = FormatTargetStatus(combat, targetLocked, textProvider);
            viewModel.SetTargetBuffs(BuildBuffViewModels(
                slice,
                entity,
                GameplayBuffPresentationSurface.TargetHud,
                textProvider,
                buffers,
                playerBuffs: false));
        }

        private static IReadOnlyList<CharacterTestFairyGuiCombatHudBuffViewModel> BuildBuffViewModels(
            GameSlice slice,
            CharacterTestCharacterEntity entity,
            GameplayBuffPresentationSurface surface,
            IMxUiTextProvider textProvider,
            CharacterTestFairyGuiCombatHudBuildBuffers buffers,
            bool playerBuffs)
        {
            if (slice?.CharacterRuntime?.World == null || entity == null)
                return Array.Empty<CharacterTestFairyGuiCombatHudBuffViewModel>();

            IReadOnlyList<GameplayBuffPresentationItem> items;
            if (buffers != null)
            {
                List<GameplayBuffPresentationItem> itemBuffer =
                    playerBuffs ? buffers.PlayerBuffItems : buffers.TargetBuffItems;
                GameplayBuffPresentationSnapshotBuilder.BuildInto(
                    slice.CharacterRuntime.World.GameplayComponentWorld,
                    entity.GameplayEntityId,
                    slice.BuffDefinitions,
                    slice.BuffPresentationDefinitions,
                    slice.CurrentFrame,
                    surface,
                    itemBuffer,
                    buffers.BuffEntryScratch,
                    CharacterTestFairyGuiCombatHudIds.BuffSlotCount);
                items = itemBuffer;
            }
            else
            {
                items = GameplayBuffPresentationSnapshotBuilder.Build(
                    slice.CharacterRuntime.World.GameplayComponentWorld,
                    entity.GameplayEntityId,
                    slice.BuffDefinitions,
                    slice.BuffPresentationDefinitions,
                    slice.CurrentFrame,
                    surface,
                    CharacterTestFairyGuiCombatHudIds.BuffSlotCount);
            }

            if (items.Count == 0)
                return Array.Empty<CharacterTestFairyGuiCombatHudBuffViewModel>();

            List<CharacterTestFairyGuiCombatHudBuffViewModel> reusableBuffs = null;
            if (buffers != null)
            {
                reusableBuffs = playerBuffs
                    ? buffers.PlayerBuffViewModels
                    : buffers.TargetBuffViewModels;
                EnsureReusableBuffViewModels(reusableBuffs, items.Count);
            }

            CharacterTestFairyGuiCombatHudBuffViewModel[] allocatedBuffs =
                reusableBuffs == null
                    ? new CharacterTestFairyGuiCombatHudBuffViewModel[items.Count]
                    : null;
            CharacterTestUiTextProviderSignature textProviderSignature =
                CharacterTestUiTextProviderSignature.Create(textProvider);
            for (int i = 0; i < items.Count; i++)
            {
                GameplayBuffPresentationItem item = items[i];
                CharacterTestFairyGuiCombatHudBuffViewModel buff =
                    reusableBuffs != null
                        ? reusableBuffs[i]
                        : new CharacterTestFairyGuiCombatHudBuffViewModel();
                bool staticPresentationReused =
                    reusableBuffs != null
                    && buff.CanReuseStaticPresentation(in item, textProviderSignature);
                if (!staticPresentationReused)
                {
                    string displayName = ResolveBuffDisplayName(item, textProvider);
                    string description = ResolveBuffDescription(item, slice.BuffDefinitions, textProvider, buffers);
                    buff.DisplayName = displayName;
                    buff.ShortName = item.ShortName;
                    buff.StackText = FormatBuffStackText(item.StackCount);
                    buff.TooltipText = BuildBuffTooltip(item, displayName, description, buffers?.TooltipBuilder);
                    buff.StoreStaticPresentation(in item, textProviderSignature);
                }

                buff.BuffId = item.BuffId;
                buff.Remaining01 = item.Remaining01;
                buff.IsPermanent = item.IsPermanent;
                buff.Polarity = item.Polarity;
                buff.TimeText = FormatBuffTimeText(item);
                if (allocatedBuffs != null)
                    allocatedBuffs[i] = buff;
            }

            return reusableBuffs != null
                ? reusableBuffs
                : (IReadOnlyList<CharacterTestFairyGuiCombatHudBuffViewModel>)allocatedBuffs;
        }

        private static string BuildBuffTooltip(
            GameplayBuffPresentationItem item,
            string displayName,
            string description,
            StringBuilder builder)
        {
            if (builder == null)
                builder = new StringBuilder(128);
            else
                builder.Clear();

            builder.Append(string.IsNullOrWhiteSpace(displayName)
                    ? item.BuffId.ToString(CultureInfo.InvariantCulture)
                    : displayName)
                .Append('\n')
                .Append("BuffId ")
                .Append(item.BuffId.ToString(CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(item.Polarity)
                .Append(" | ");
            builder.Append(item.IsPermanent ? "Permanent" : TimedBuffTooltipText);
            if (item.StackCount > 1)
            {
                builder.Append('\n')
                    .Append("Stacks ")
                    .Append(item.StackCount.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(description))
                builder.Append('\n').Append(description);
            if (!string.IsNullOrWhiteSpace(item.EffectSummary))
                builder.Append('\n').Append(item.EffectSummary);
            return builder.ToString();
        }

        private static string FormatBuffTimeText(GameplayBuffPresentationItem item)
        {
            return item.IsPermanent ? PermanentBuffTimeText : FormatFrameText(item.RemainingFrames);
        }

        private static string FormatFrameText(long frames)
        {
            if (frames >= 0L && frames <= FrameTextCacheLimit)
                return FrameTextCache[(int)frames];

            return frames.ToString(CultureInfo.InvariantCulture) + "f";
        }

        private static string FormatBuffStackText(int stackCount)
        {
            if (stackCount <= 1)
                return string.Empty;
            if (stackCount <= StackTextCacheLimit)
                return StackTextCache[stackCount];

            return stackCount.ToString(CultureInfo.InvariantCulture);
        }

        private static string[] CreateFrameTextCache()
        {
            var cache = new string[FrameTextCacheLimit + 1];
            for (int i = 0; i < cache.Length; i++)
                cache[i] = i.ToString(CultureInfo.InvariantCulture) + "f";
            return cache;
        }

        private static string[] CreateStackTextCache()
        {
            var cache = new string[StackTextCacheLimit + 1];
            cache[0] = string.Empty;
            cache[1] = string.Empty;
            for (int i = 2; i < cache.Length; i++)
                cache[i] = i.ToString(CultureInfo.InvariantCulture);
            return cache;
        }

    }

    internal sealed class CharacterTestFairyGuiCombatHudBuildBuffers
    {
        public readonly List<GameplayBuffPresentationItem> PlayerBuffItems =
            new List<GameplayBuffPresentationItem>(CharacterTestFairyGuiCombatHudIds.BuffSlotCount);
        public readonly List<GameplayBuffPresentationItem> TargetBuffItems =
            new List<GameplayBuffPresentationItem>(CharacterTestFairyGuiCombatHudIds.BuffSlotCount);
        public readonly List<GameplayBuffPresentationItem> RecentTargetBuffItems =
            new List<GameplayBuffPresentationItem>(1);
        public readonly List<GameplayComponentBuffEntry> BuffEntryScratch =
            new List<GameplayComponentBuffEntry>(CharacterTestFairyGuiCombatHudIds.BuffSlotCount);
        public readonly List<CharacterTestFairyGuiCombatHudBuffViewModel> PlayerBuffViewModels =
            new List<CharacterTestFairyGuiCombatHudBuffViewModel>(CharacterTestFairyGuiCombatHudIds.BuffSlotCount);
        public readonly List<CharacterTestFairyGuiCombatHudBuffViewModel> TargetBuffViewModels =
            new List<CharacterTestFairyGuiCombatHudBuffViewModel>(CharacterTestFairyGuiCombatHudIds.BuffSlotCount);
        public readonly List<string> AutoDescriptionParts = new List<string>(3);
        public readonly List<string> EffectDescriptionParts =
            new List<string>(CharacterTestFairyGuiCombatHudIds.BuffSlotCount);
        public readonly StringBuilder TooltipBuilder = new StringBuilder(128);
        public bool HasPlayerActionTextCache;
        public CharacterTestUiTextProviderSignature PlayerActionTextProviderSignature;
        public string PlayerActionTextActionId = string.Empty;
        public string PlayerActionTextPhase = string.Empty;
        public string PlayerActionText = string.Empty;
    }
}

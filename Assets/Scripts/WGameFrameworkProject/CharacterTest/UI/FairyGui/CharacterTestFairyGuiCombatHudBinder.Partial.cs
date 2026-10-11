using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Gameplay;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiCombatHudBinder
    {
        private const float PlayerHealthFillWidth = 356f;
        private const float PlayerScatterPressureFillWidth = 352f;
        private const float TargetHealthFillWidth = 356f;
        private const float TargetScatterPressureFillWidth = 352f;
        private const float AbilitySlotCooldownFillWidth = 72f;
        private const float BuffSlotSize = 36f;
        private const float BuffSlotCooldownFillHeight = 36f;
        private static readonly Color32 NetworkLatencyGoodColor = new Color32(111, 216, 181, 255);
        private static readonly Color32 NetworkLatencyWarnColor = new Color32(240, 180, 95, 255);
        private static readonly Color32 NetworkLatencyBadColor = new Color32(235, 118, 118, 255);

        private static readonly string[] PlayerChildNames =
        {
            CharacterTestFairyGuiCombatHudIds.PlayerPlate,
            CharacterTestFairyGuiCombatHudIds.PlayerName,
            CharacterTestFairyGuiCombatHudIds.PlayerHealthIcon,
            CharacterTestFairyGuiCombatHudIds.PlayerHealthTrack,
            CharacterTestFairyGuiCombatHudIds.PlayerHealthFill,
            CharacterTestFairyGuiCombatHudIds.PlayerHealthText,
            CharacterTestFairyGuiCombatHudIds.PlayerScatterPressureIcon,
            CharacterTestFairyGuiCombatHudIds.PlayerScatterPressureTrack,
            CharacterTestFairyGuiCombatHudIds.PlayerScatterPressureFill,
            CharacterTestFairyGuiCombatHudIds.PlayerCohesionBreakMarker,
            CharacterTestFairyGuiCombatHudIds.PlayerScatterPressureText,
            CharacterTestFairyGuiCombatHudIds.PlayerActionText
        };

        private static readonly string[] TargetChildNames =
        {
            CharacterTestFairyGuiCombatHudIds.TargetPlate,
            CharacterTestFairyGuiCombatHudIds.TargetName,
            CharacterTestFairyGuiCombatHudIds.TargetStatusText,
            CharacterTestFairyGuiCombatHudIds.TargetHealthTrack,
            CharacterTestFairyGuiCombatHudIds.TargetHealthFill,
            CharacterTestFairyGuiCombatHudIds.TargetHealthText,
            CharacterTestFairyGuiCombatHudIds.TargetScatterPressureTrack,
            CharacterTestFairyGuiCombatHudIds.TargetScatterPressureFill,
            CharacterTestFairyGuiCombatHudIds.TargetCohesionBreakMarker,
            CharacterTestFairyGuiCombatHudIds.TargetScatterPressureText
        };

        private static readonly string[] FeedbackChildNames =
        {
            CharacterTestFairyGuiCombatHudIds.FeedbackPanel,
            CharacterTestFairyGuiCombatHudIds.FeedbackTrim,
            CharacterTestFairyGuiCombatHudIds.FeedbackText
        };

        private static readonly string[] NetworkLatencyChildNames =
        {
            CharacterTestFairyGuiCombatHudIds.NetworkLatencyBadgeBackground,
            CharacterTestFairyGuiCombatHudIds.NetworkLatencyText
        };

        private static readonly string[] BehaviorCardChildNames =
        {
            CharacterTestFairyGuiCombatHudIds.BehaviorCardPanel,
            CharacterTestFairyGuiCombatHudIds.BehaviorCardIcon,
            CharacterTestFairyGuiCombatHudIds.BehaviorCardIconText,
            CharacterTestFairyGuiCombatHudIds.BehaviorCardName,
            CharacterTestFairyGuiCombatHudIds.BehaviorCardReason
        };

        private CombatHudControlCache _controlCache;

        public CharacterTestFairyGuiCombatHudBinder()
            : this(null, CharacterTestFairyGuiCombatHudIds.ViewId, null)
        {
        }

        partial void OnAfterGeneratedBind(Fgui.GComponent root, CharacterTestFairyGuiCombatHudViewModel viewModel)
        {
            root.SetSize(
                MxFairyGuiStageLayout.ResolveStageWidth(root, 1920f),
                MxFairyGuiStageLayout.ResolveStageHeight(root, 1080f));
            CombatHudControlCache cache = GetControlCache(root);

            SetVisible(cache.PlayerChildren, viewModel.HasLocalPlayer);
            if (viewModel.HasLocalPlayer)
            {
                SetText(cache.PlayerName, viewModel.PlayerName);
                SetText(cache.PlayerHealthText, viewModel.PlayerHealthText);
                SetBarWidth(
                    cache.PlayerHealthFill,
                    PlayerHealthFillWidth,
                    viewModel.PlayerHealthRatio);
                SetText(cache.PlayerScatterPressureText, viewModel.PlayerScatterPressureText);
                SetBarWidth(
                    cache.PlayerScatterPressureFill,
                    PlayerScatterPressureFillWidth,
                    viewModel.PlayerScatterPressureRatio);
                SetText(cache.PlayerActionText, viewModel.PlayerActionText);
            }

            SetVisible(cache.TargetChildren, viewModel.TargetVisible);
            SetVisible(cache.LockCrosshair, viewModel.TargetVisible && viewModel.TargetLocked);
            if (viewModel.TargetVisible)
            {
                SetText(cache.TargetName, viewModel.TargetName);
                SetText(cache.TargetStatusText, viewModel.TargetStatusText);
                SetText(cache.TargetHealthText, viewModel.TargetHealthText);
                SetBarWidth(
                    cache.TargetHealthFill,
                    TargetHealthFillWidth,
                    viewModel.TargetHealthRatio);
                SetText(cache.TargetScatterPressureText, viewModel.TargetScatterPressureText);
                SetBarWidth(
                    cache.TargetScatterPressureFill,
                    TargetScatterPressureFillWidth,
                    viewModel.TargetScatterPressureRatio);
            }

            SetVisible(cache.FeedbackChildren, viewModel.FeedbackVisible);
            SetText(
                cache.FeedbackText,
                viewModel.FeedbackVisible ? viewModel.FeedbackText : string.Empty);

            SetVisible(cache.NetworkLatencyChildren, viewModel.NetworkLatencyVisible);
            SetText(
                cache.NetworkLatencyText,
                viewModel.NetworkLatencyVisible ? viewModel.NetworkLatencyText : string.Empty);
            SetTextColor(
                cache.NetworkLatencyText,
                ResolveNetworkLatencyColor(viewModel.NetworkLatencyTone));

            SetVisible(cache.BehaviorCardChildren, viewModel.BehaviorCardVisible);
            SetText(cache.BehaviorCardIconText, viewModel.BehaviorCardIconText);
            SetText(cache.BehaviorCardName, viewModel.BehaviorCardName);
            SetText(cache.BehaviorCardReason, viewModel.BehaviorCardReasonText);

            BindHotbar(cache.AbilitySlots, viewModel.AbilitySlots);
            BindBuffSlots(cache.PlayerBuffSlots, viewModel.PlayerBuffs, viewModel.HasLocalPlayer);
            BindBuffSlots(cache.TargetBuffSlots, viewModel.TargetBuffs, viewModel.TargetVisible);
        }

        internal static void SyncLockCrosshair(
            Fgui.GComponent root,
            bool visible,
            Vector2 stagePosition)
        {
            if (root == null)
                return;

            Fgui.GObject crosshair = root.GetChild(CharacterTestFairyGuiCombatHudIds.LockCrosshair);
            if (crosshair == null)
                return;

            crosshair.visible = visible;
            if (!visible)
                return;

            crosshair.SetXY(
                Mathf.Round(stagePosition.x - crosshair.width * 0.5f),
                Mathf.Round(stagePosition.y - crosshair.height * 0.5f));
        }

        private CombatHudControlCache GetControlCache(Fgui.GComponent root)
        {
            if (_controlCache == null || !ReferenceEquals(_controlCache.Root, root))
                _controlCache = new CombatHudControlCache(root);
            return _controlCache;
        }

        private static void BindHotbar(
            AbilitySlotControls[] controls,
            IReadOnlyList<CharacterTestFairyGuiCombatHudAbilitySlotViewModel> slots)
        {
            for (int i = 0; i < controls.Length; i++)
            {
                AbilitySlotControls control = controls[i];
                CharacterTestFairyGuiCombatHudAbilitySlotViewModel slot =
                    slots != null && i < slots.Count
                        ? slots[i]
                        : null;

                SetVisible(control.Background, true);
                SetVisible(control.Selected, slot != null && slot.IsSelected);
                SetText(control.Key, slot != null ? slot.KeyText : control.DefaultKeyText);
                SetText(control.Name, slot != null ? slot.AbilityText : string.Empty);
                SetVisible(control.CooldownFill, slot != null && slot.CooldownRatio > 0f);
                SetBarWidth(
                    control.CooldownFill,
                    AbilitySlotCooldownFillWidth,
                    slot != null ? slot.CooldownRatio : 0f);
                SetText(control.CooldownText, slot != null ? slot.CooldownText : string.Empty);
            }
        }

        private static void BindBuffSlots(
            BuffSlotControls[] controls,
            IReadOnlyList<CharacterTestFairyGuiCombatHudBuffViewModel> buffs,
            bool ownerVisible)
        {
            for (int i = 0; i < controls.Length; i++)
            {
                BuffSlotControls control = controls[i];
                CharacterTestFairyGuiCombatHudBuffViewModel buff =
                    ownerVisible && buffs != null && i < buffs.Count
                        ? buffs[i]
                        : null;
                bool visible = buff != null;

                SetVisible(control.Background, visible);
                SetVisible(control.Border, visible);
                SetVisible(control.Label, visible);
                SetVisible(control.CooldownFill, visible && !buff.IsPermanent);
                SetVisible(control.Stack, visible && !string.IsNullOrEmpty(buff.StackText));
                SetVisible(control.Time, visible);
                SetText(control.Label, buff != null ? buff.ShortName : string.Empty);
                SetText(control.Stack, buff != null ? buff.StackText : string.Empty);
                SetText(control.Time, buff != null ? buff.TimeText : string.Empty);
                SetTooltip(control.Background, buff != null ? buff.TooltipText : string.Empty);
                SetTooltip(control.Border, buff != null ? buff.TooltipText : string.Empty);
                SetTooltip(control.Label, buff != null ? buff.TooltipText : string.Empty);

                if (buff == null)
                    continue;

                Color borderColor = ResolveBuffBorderColor(buff.Polarity);
                SetGraphRect(
                    control.BackgroundGraph,
                    BuffSlotSize,
                    BuffSlotSize,
                    fillColor: ResolveBuffBackgroundColor(buff.Polarity),
                    lineSize: 0,
                    lineColor: Color.clear);
                SetGraphRect(
                    control.BorderGraph,
                    BuffSlotSize,
                    BuffSlotSize,
                    fillColor: Color.clear,
                    lineSize: 1,
                    lineColor: borderColor);
                SetBarHeight(
                    control.CooldownFill,
                    BuffSlotCooldownFillHeight,
                    buff.IsPermanent ? 0f : 1f - buff.Remaining01);
            }
        }

        private static void SetText(Fgui.GTextField text, string value)
        {
            if (text == null)
                return;

            value ??= string.Empty;
            if (!string.Equals(text.text, value, StringComparison.Ordinal))
                text.text = value;
        }

        private static void SetVisible(Fgui.GObject[] children, bool visible)
        {
            for (int i = 0; i < children.Length; i++)
                SetVisible(children[i], visible);
        }

        private static void SetVisible(Fgui.GObject child, bool visible)
        {
            if (child != null)
                child.visible = visible;
        }

        private static void SetTextColor(Fgui.GTextField text, Color color)
        {
            if (text != null)
                text.color = color;
        }

        private static void SetTooltip(Fgui.GObject child, string value)
        {
            if (child == null)
                return;

            value ??= string.Empty;
            if (!string.Equals(child.tooltips, value, StringComparison.Ordinal))
                child.tooltips = value;
        }

        private static Color ResolveNetworkLatencyColor(string tone)
        {
            if (string.Equals(tone, "bad", StringComparison.OrdinalIgnoreCase))
                return NetworkLatencyBadColor;
            if (string.Equals(tone, "warn", StringComparison.OrdinalIgnoreCase))
                return NetworkLatencyWarnColor;
            return NetworkLatencyGoodColor;
        }

        private static Color ResolveBuffBorderColor(GameplayBuffPresentationPolarity polarity)
        {
            switch (polarity)
            {
                case GameplayBuffPresentationPolarity.Buff:
                    return new Color(0.28f, 0.82f, 0.98f, 0.96f);
                case GameplayBuffPresentationPolarity.Debuff:
                    return new Color(0.95f, 0.22f, 0.28f, 0.96f);
                case GameplayBuffPresentationPolarity.Aura:
                    return new Color(0.95f, 0.76f, 0.24f, 0.98f);
                default:
                    return new Color(0.7f, 0.74f, 0.76f, 0.86f);
            }
        }

        private static Color ResolveBuffBackgroundColor(GameplayBuffPresentationPolarity polarity)
        {
            switch (polarity)
            {
                case GameplayBuffPresentationPolarity.Buff:
                    return new Color(0.03f, 0.16f, 0.2f, 0.88f);
                case GameplayBuffPresentationPolarity.Debuff:
                    return new Color(0.18f, 0.035f, 0.05f, 0.9f);
                case GameplayBuffPresentationPolarity.Aura:
                    return new Color(0.18f, 0.13f, 0.03f, 0.9f);
                default:
                    return new Color(0.04f, 0.055f, 0.06f, 0.88f);
            }
        }

        private static void SetBarWidth(Fgui.GObject child, float maxWidth, float ratio)
        {
            if (child == null)
                return;

            float safeRatio = ratio <= 0f ? 0f : ratio >= 1f ? 1f : ratio;
            child.width = (float)Math.Round(maxWidth * safeRatio, 1);
        }

        private static void SetBarHeight(Fgui.GObject child, float maxHeight, float ratio)
        {
            if (child == null)
                return;

            float safeRatio = ratio <= 0f ? 0f : ratio >= 1f ? 1f : ratio;
            child.height = (float)Math.Round(maxHeight * safeRatio, 1);
        }

        private static void SetGraphRect(
            Fgui.GGraph graph,
            float width,
            float height,
            Color fillColor,
            int lineSize,
            Color lineColor)
        {
            if (graph == null)
                return;

            graph.DrawRect(width, height, lineSize, lineColor, fillColor);
        }

        private static Fgui.GObject[] CacheChildren(Fgui.GComponent root, IReadOnlyList<string> childNames)
        {
            var children = new Fgui.GObject[childNames.Count];
            for (int i = 0; i < childNames.Count; i++)
                children[i] = root.GetChild(childNames[i]);
            return children;
        }

        private static Fgui.GTextField Text(Fgui.GComponent root, string childName)
        {
            return root.GetChild(childName)?.asTextField;
        }

        private static Fgui.GObject Child(Fgui.GComponent root, string childName)
        {
            return root.GetChild(childName);
        }

        private static Fgui.GGraph Graph(Fgui.GObject child)
        {
            return child?.asGraph;
        }

        private sealed class CombatHudControlCache
        {
            public readonly Fgui.GComponent Root;
            public readonly Fgui.GObject[] PlayerChildren;
            public readonly Fgui.GObject[] TargetChildren;
            public readonly Fgui.GObject[] FeedbackChildren;
            public readonly Fgui.GObject[] NetworkLatencyChildren;
            public readonly Fgui.GTextField PlayerName;
            public readonly Fgui.GTextField PlayerHealthText;
            public readonly Fgui.GObject PlayerHealthFill;
            public readonly Fgui.GTextField PlayerScatterPressureText;
            public readonly Fgui.GObject PlayerScatterPressureFill;
            public readonly Fgui.GTextField PlayerActionText;
            public readonly Fgui.GObject LockCrosshair;
            public readonly Fgui.GTextField TargetName;
            public readonly Fgui.GTextField TargetStatusText;
            public readonly Fgui.GTextField TargetHealthText;
            public readonly Fgui.GObject TargetHealthFill;
            public readonly Fgui.GTextField TargetScatterPressureText;
            public readonly Fgui.GObject TargetScatterPressureFill;
            public readonly Fgui.GTextField FeedbackText;
            public readonly Fgui.GTextField NetworkLatencyText;
            public readonly Fgui.GObject[] BehaviorCardChildren;
            public readonly Fgui.GTextField BehaviorCardIconText;
            public readonly Fgui.GTextField BehaviorCardName;
            public readonly Fgui.GTextField BehaviorCardReason;
            public readonly AbilitySlotControls[] AbilitySlots;
            public readonly BuffSlotControls[] PlayerBuffSlots;
            public readonly BuffSlotControls[] TargetBuffSlots;

            public CombatHudControlCache(Fgui.GComponent root)
            {
                Root = root;
                PlayerChildren = CacheChildren(root, PlayerChildNames);
                TargetChildren = CacheChildren(root, TargetChildNames);
                FeedbackChildren = CacheChildren(root, FeedbackChildNames);
                NetworkLatencyChildren = CacheChildren(root, NetworkLatencyChildNames);
                PlayerName = Text(root, CharacterTestFairyGuiCombatHudIds.PlayerName);
                PlayerHealthText = Text(root, CharacterTestFairyGuiCombatHudIds.PlayerHealthText);
                PlayerHealthFill = Child(root, CharacterTestFairyGuiCombatHudIds.PlayerHealthFill);
                PlayerScatterPressureText = Text(root, CharacterTestFairyGuiCombatHudIds.PlayerScatterPressureText);
                PlayerScatterPressureFill = Child(root, CharacterTestFairyGuiCombatHudIds.PlayerScatterPressureFill);
                PlayerActionText = Text(root, CharacterTestFairyGuiCombatHudIds.PlayerActionText);
                LockCrosshair = Child(root, CharacterTestFairyGuiCombatHudIds.LockCrosshair);
                TargetName = Text(root, CharacterTestFairyGuiCombatHudIds.TargetName);
                TargetStatusText = Text(root, CharacterTestFairyGuiCombatHudIds.TargetStatusText);
                TargetHealthText = Text(root, CharacterTestFairyGuiCombatHudIds.TargetHealthText);
                TargetHealthFill = Child(root, CharacterTestFairyGuiCombatHudIds.TargetHealthFill);
                TargetScatterPressureText = Text(root, CharacterTestFairyGuiCombatHudIds.TargetScatterPressureText);
                TargetScatterPressureFill = Child(root, CharacterTestFairyGuiCombatHudIds.TargetScatterPressureFill);
                FeedbackText = Text(root, CharacterTestFairyGuiCombatHudIds.FeedbackText);
                NetworkLatencyText = Text(root, CharacterTestFairyGuiCombatHudIds.NetworkLatencyText);
                BehaviorCardChildren = CacheChildren(root, BehaviorCardChildNames);
                BehaviorCardIconText = Text(root, CharacterTestFairyGuiCombatHudIds.BehaviorCardIconText);
                BehaviorCardName = Text(root, CharacterTestFairyGuiCombatHudIds.BehaviorCardName);
                BehaviorCardReason = Text(root, CharacterTestFairyGuiCombatHudIds.BehaviorCardReason);
                AbilitySlots = CreateAbilitySlotControls(root);
                PlayerBuffSlots = CreateBuffSlotControls(root, isPlayer: true);
                TargetBuffSlots = CreateBuffSlotControls(root, isPlayer: false);
            }

            private static AbilitySlotControls[] CreateAbilitySlotControls(Fgui.GComponent root)
            {
                var controls = new AbilitySlotControls[CharacterTestFairyGuiCombatHudIds.AbilitySlotCount];
                for (int i = 0; i < controls.Length; i++)
                {
                    int slotNumber = i + 1;
                    controls[i] = new AbilitySlotControls(
                        slotNumber.ToString(CultureInfo.InvariantCulture),
                        Child(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotBackground(slotNumber)),
                        Child(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotSelected(slotNumber)),
                        Text(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotKey(slotNumber)),
                        Text(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotName(slotNumber)),
                        Child(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotCooldownFill(slotNumber)),
                        Text(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotCooldownText(slotNumber)));
                }

                return controls;
            }

            private static BuffSlotControls[] CreateBuffSlotControls(Fgui.GComponent root, bool isPlayer)
            {
                var controls = new BuffSlotControls[CharacterTestFairyGuiCombatHudIds.BuffSlotCount];
                for (int i = 0; i < controls.Length; i++)
                {
                    int slotNumber = i + 1;
                    Fgui.GObject background = Child(
                        root,
                        isPlayer
                            ? CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotBackground(slotNumber)
                            : CharacterTestFairyGuiCombatHudIds.TargetBuffSlotBackground(slotNumber));
                    Fgui.GObject border = Child(
                        root,
                        isPlayer
                            ? CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotBorder(slotNumber)
                            : CharacterTestFairyGuiCombatHudIds.TargetBuffSlotBorder(slotNumber));
                    controls[i] = new BuffSlotControls(
                        background,
                        border,
                        Text(
                            root,
                            isPlayer
                                ? CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotLabel(slotNumber)
                                : CharacterTestFairyGuiCombatHudIds.TargetBuffSlotLabel(slotNumber)),
                        Child(
                            root,
                            isPlayer
                                ? CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotCooldownFill(slotNumber)
                                : CharacterTestFairyGuiCombatHudIds.TargetBuffSlotCooldownFill(slotNumber)),
                        Text(
                            root,
                            isPlayer
                                ? CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotStack(slotNumber)
                                : CharacterTestFairyGuiCombatHudIds.TargetBuffSlotStack(slotNumber)),
                        Text(
                            root,
                            isPlayer
                                ? CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotTime(slotNumber)
                                : CharacterTestFairyGuiCombatHudIds.TargetBuffSlotTime(slotNumber)),
                        Graph(background),
                        Graph(border));
                }

                return controls;
            }
        }

        private readonly struct AbilitySlotControls
        {
            public readonly string DefaultKeyText;
            public readonly Fgui.GObject Background;
            public readonly Fgui.GObject Selected;
            public readonly Fgui.GTextField Key;
            public readonly Fgui.GTextField Name;
            public readonly Fgui.GObject CooldownFill;
            public readonly Fgui.GTextField CooldownText;

            public AbilitySlotControls(
                string defaultKeyText,
                Fgui.GObject background,
                Fgui.GObject selected,
                Fgui.GTextField key,
                Fgui.GTextField name,
                Fgui.GObject cooldownFill,
                Fgui.GTextField cooldownText)
            {
                DefaultKeyText = defaultKeyText;
                Background = background;
                Selected = selected;
                Key = key;
                Name = name;
                CooldownFill = cooldownFill;
                CooldownText = cooldownText;
            }
        }

        private readonly struct BuffSlotControls
        {
            public readonly Fgui.GObject Background;
            public readonly Fgui.GObject Border;
            public readonly Fgui.GTextField Label;
            public readonly Fgui.GObject CooldownFill;
            public readonly Fgui.GTextField Stack;
            public readonly Fgui.GTextField Time;
            public readonly Fgui.GGraph BackgroundGraph;
            public readonly Fgui.GGraph BorderGraph;

            public BuffSlotControls(
                Fgui.GObject background,
                Fgui.GObject border,
                Fgui.GTextField label,
                Fgui.GObject cooldownFill,
                Fgui.GTextField stack,
                Fgui.GTextField time,
                Fgui.GGraph backgroundGraph,
                Fgui.GGraph borderGraph)
            {
                Background = background;
                Border = border;
                Label = label;
                CooldownFill = cooldownFill;
                Stack = stack;
                Time = time;
                BackgroundGraph = backgroundGraph;
                BorderGraph = borderGraph;
            }
        }
    }
}

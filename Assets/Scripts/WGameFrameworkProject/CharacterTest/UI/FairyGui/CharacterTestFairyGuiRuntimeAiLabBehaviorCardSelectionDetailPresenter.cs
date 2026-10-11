using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Projects the selected assembly card into the upper-right detail surface.</summary>
    /// <remarks>LowFreqAlloc: rebuilt only when the explicit card-library selection changes.</remarks>
    internal static class CharacterTestFairyGuiRuntimeAiLabBehaviorCardSelectionDetailPresenter
    {
        internal const string RootName = "behaviorCardLibrarySelectionDetail";

        internal static void Bind(
            Fgui.GComponent host,
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card,
            bool visible,
            IMxUiCommandSink commandSink,
            MxUiViewId viewId,
            Action<Fgui.GObject, string> bindTooltip = null)
        {
            Fgui.GComponent root = EnsureRoot(host);
            if (root == null)
                return;
            root.visible = visible && card != null;
            if (!root.visible)
                return;

            ResetRootForMode(root, "summary");

            root.SetSize(
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelWidth,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionContentHeight);
            root.SetXY(
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelX,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionContentY);
            root.sortingOrder = 135;

            DrawPanel(root, "detailBackground", 0f, 0f, 520f, 446f,
                new Color32(6, 17, 24, 252), new Color32(24, 60, 72, 255), 2);
            DrawPanel(root, "identitySurface", 16f, 16f, 488f, 118f,
                new Color32(8, 24, 32, 255), new Color32(28, 72, 84, 255), 1);
            CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.Bind(
                root,
                card.IconKey,
                30f,
                32f,
                82f,
                card.InCurrentBehaviorGroup
                    ? new Color32(67, 224, 169, 255)
                    : new Color32(67, 184, 230, 255),
                true);

            HideChild(root, "detailTitle");
            BindTitleInput(root, card, commandSink, viewId);
            SetText(root, "detailCategory", ResolveCategory(card.IconKey),
                132f, 74f, 140f, 24f, 15,
                new Color32(108, 220, 207, 255), true, true);
            SetText(root, "detailState", card.ConfigurationSourceText,
                272f, 74f, 216f, 24f, 14,
                card.InCurrentBehaviorGroup
                    ? new Color32(82, 227, 156, 255)
                    : new Color32(111, 199, 240, 255),
                true,
                true,
                Fgui.AlignType.Right);

            DrawPanel(root, "purposeSurface", 16f, 146f, 488f, 100f,
                new Color32(6, 18, 25, 255), new Color32(22, 54, 66, 255), 1);
            BindResponsibilityButtons(root, card, commandSink, viewId);

            HideChild(root, "purposeDivider");
            HideChild(root, "purposeLabel");
            HideChild(root, "purposeText");
            HideChild(root, "riskLabel");
            HideChild(root, "riskText");
            HideChild(root, "statusText");
            HideChild(root, "logicLabel");
            HideChild(root, "logicStructureText");
            for (int i = 0; i < card.ConfigurationFields.Length; i++)
            {
                string suffix = i.ToString();
                HideChild(root, "configurationRow" + suffix);
                HideChild(root, "configurationLabel" + suffix);
                HideChild(root, "configurationValue" + suffix);
            }

            DrawPanel(root, "statusSurface", 16f, 258f, 488f, 140f,
                new Color32(8, 24, 32, 255), new Color32(28, 72, 84, 255), 1);
            SetText(root, "referenceLabel", "行为组引用情况", 32f, 270f, 240f, 24f, 15,
                new Color32(93, 213, 200, 255), true, true);
            DrawPanel(root, "referenceSummaryBackground", 28f, 300f, 464f, 80f,
                new Color32(9, 26, 34, 255), new Color32(20, 56, 68, 255), 1);
            SetText(root, "referenceSummaryText", card.ReferenceSummaryText,
                42f, 312f, 436f, 56f, 14,
                card.ReferenceCount > 0
                    ? new Color32(213, 227, 231, 255)
                    : new Color32(119, 148, 158, 255),
                false,
                false);
            Fgui.GObject referenceSummary = root.GetChild("referenceSummaryText");
            if (referenceSummary != null)
            {
                if (bindTooltip != null)
                    bindTooltip(referenceSummary, card.ReferenceSummaryText ?? string.Empty);
                else
                    referenceSummary.tooltips = string.Empty;
            }
            SetText(root, "stableIdText", "卡片唯一标识：" + card.StableCardId, 32f, 412f, 456f, 20f, 12,
                new Color32(90, 119, 130, 255), true, false);
        }

        internal static Fgui.GComponent EnsureRoot(Fgui.GComponent host)
        {
            if (host == null)
                return null;
            Fgui.GComponent root = host.GetChild(RootName)?.asCom;
            if (root != null)
                return root;
            root = new Fgui.GComponent
            {
                name = RootName,
                opaque = true,
                touchable = true
            };
            host.AddChild(root);
            return root;
        }

        internal static void ResetRootForMode(Fgui.GComponent root, string mode)
        {
            if (root == null || string.Equals(root.data as string, mode, StringComparison.Ordinal))
                return;
            root.RemoveChildren(0, -1, true);
            root.data = mode ?? string.Empty;
        }

        private static void DrawPanel(
            Fgui.GComponent root,
            string name,
            float x,
            float y,
            float width,
            float height,
            Color fill,
            Color border,
            int lineSize)
        {
            Fgui.GGraph graph = root.GetChild(name)?.asGraph;
            if (graph == null)
            {
                graph = new Fgui.GGraph { name = name, touchable = false };
                root.AddChild(graph);
            }
            graph.SetSize(width, height);
            graph.SetXY(x, y);
            graph.DrawRect(width, height, lineSize, border, fill);
            graph.visible = true;
        }

        private static void SetText(
            Fgui.GComponent root,
            string name,
            string value,
            float x,
            float y,
            float width,
            float height,
            int fontSize,
            Color color,
            bool singleLine,
            bool bold,
            Fgui.AlignType align = Fgui.AlignType.Left)
        {
            Fgui.GTextField text = root.GetChild(name)?.asTextField;
            if (text == null)
            {
                text = new Fgui.GTextField { name = name, touchable = false };
                root.AddChild(text);
            }
            text.SetSize(width, height);
            text.SetXY(x, y);
            text.text = value ?? string.Empty;
            text.textFormat.size = fontSize;
            text.color = color;
            text.singleLine = singleLine;
            text.textFormat.bold = bold;
            text.align = align;
            text.verticalAlign = Fgui.VertAlignType.Middle;
            text.visible = true;
        }

        private static void BindTitleInput(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card,
            IMxUiCommandSink commandSink,
            MxUiViewId viewId)
        {
            bool editable = card?.DisplayNameEditable == true && commandSink != null;
            DrawPanel(
                root,
                "detailTitleInputBackground",
                124f,
                24f,
                364f,
                44f,
                editable ? new Color32(10, 36, 44, 255) : new Color32(8, 24, 32, 255),
                editable ? new Color32(42, 130, 140, 255) : new Color32(22, 54, 64, 255),
                1);

            Fgui.GTextInput input = root.GetChild("detailTitleInput")?.asTextInput;
            if (input == null)
            {
                input = new Fgui.GTextInput
                {
                    name = "detailTitleInput",
                    singleLine = true,
                    maxLength = 96
                };
                root.AddChild(input);
            }
            input.SetSize(348f, 40f);
            input.SetXY(132f, 26f);
            if (!input.focused)
                input.text = card?.Title ?? string.Empty;
            input.data = card?.Title ?? string.Empty;
            input.promptText = "行为卡名称";
            input.textFormat.size = 22;
            input.textFormat.bold = true;
            input.color = new Color32(240, 248, 250, 255);
            input.verticalAlign = Fgui.VertAlignType.Middle;
            input.enabled = editable;
            input.grayed = !editable;
            input.touchable = editable;
            input.visible = true;
            input.onTouchBegin.Set(context => context.StopPropagation());
            input.onFocusOut.Set(() => CommitTitle(input, card, editable, commandSink, viewId));
            input.onSubmit.Set(() =>
            {
                CommitTitle(input, card, editable, commandSink, viewId);
                root.RequestFocus();
            });
        }

        private static void CommitTitle(
            Fgui.GTextInput input,
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card,
            bool editable,
            IMxUiCommandSink commandSink,
            MxUiViewId viewId)
        {
            if (!editable || input == null || commandSink == null)
                return;
            string value = input.text?.Trim() ?? string.Empty;
            string original = input.data as string ?? string.Empty;
            if (string.Equals(value, original.Trim(), StringComparison.Ordinal))
                return;
            input.data = value;
            commandSink.Enqueue(new MxUiCommand(
                viewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardDisplayName,
                new CharacterTestRuntimeAiLabProjectCardMetadataEditPayload(
                    card?.EntryIndex ?? -1,
                    card?.StableCardId ?? string.Empty,
                    value)));
        }

        private static void BindResponsibilityButtons(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card,
            IMxUiCommandSink commandSink,
            MxUiViewId viewId)
        {
            SetText(root, "responsibilityLabel", "行为职责", 32f, 156f, 120f, 24f, 15,
                new Color32(93, 213, 200, 255), true, true);
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities =
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.OrderedResponsibilities;
            for (int i = 0; i < responsibilities.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility = responsibilities[i];
                Fgui.GButton button = root.GetChild(
                    "responsibilityOption" + responsibility)?.asButton;
                if (button == null)
                {
                    button = Fgui.UIPackage.CreateObject(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                        "CompactNeutralButton")?.asButton;
                    if (button == null)
                        continue;
                    button.name = "responsibilityOption" + responsibility;
                    root.AddChild(button);
                }
                bool selected = HasResponsibility(card?.ResponsibilityKinds, responsibility);
                bool enabled = card?.ResponsibilityEditable == true && commandSink != null;
                button.SetXY(28f + i * 92f, 184f);
                button.SetSize(86f, 32f);
                button.titleFontSize = 13;
                button.title = (selected ? "✓ " : string.Empty)
                    + CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.GetDisplayName(responsibility);
                button.enabled = enabled;
                button.touchable = enabled;
                button.grayed = false;
                button.visible = true;
                button.onClick.Set(context =>
                {
                    context.StopPropagation();
                    CommitResponsibilityToggle(card, responsibility, commandSink, viewId);
                });
            }
        }

        private static void CommitResponsibilityToggle(
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility,
            IMxUiCommandSink commandSink,
            MxUiViewId viewId)
        {
            if (card?.ResponsibilityEditable != true || card.EntryIndex < 0 || commandSink == null)
                return;
            int responsibilityIndex = (int)responsibility;

            var selected = new bool[5];
            for (int i = 0; card.ResponsibilityKinds != null && i < card.ResponsibilityKinds.Length; i++)
            {
                int index = (int)card.ResponsibilityKinds[i];
                if (index >= 0 && index < selected.Length)
                    selected[index] = true;
            }
            if (responsibility == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other)
            {
                Array.Clear(selected, 0, selected.Length);
                selected[responsibilityIndex] = true;
            }
            else
            {
                selected[responsibilityIndex] = !selected[responsibilityIndex];
                selected[(int)CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other] = false;
                bool hasExplicitResponsibility = false;
                for (int i = 1; i < selected.Length; i++)
                    hasExplicitResponsibility |= selected[i];
                if (!hasExplicitResponsibility)
                    selected[(int)CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other] = true;
            }

            var payload = new StringBuilder();
            payload.Append(card.EntryIndex).Append('|');
            bool appended = false;
            for (int i = 0; i < selected.Length; i++)
            {
                if (!selected[i])
                    continue;
                if (appended)
                    payload.Append(',');
                payload.Append((CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind)i);
                appended = true;
            }
            commandSink.Enqueue(new MxUiCommand(
                viewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetEntryResponsibilities,
                payload.ToString()));
        }

        private static bool HasResponsibility(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] source,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility)
        {
            for (int i = 0; source != null && i < source.Length; i++)
            {
                if (source[i] == responsibility)
                    return true;
            }
            return false;
        }

        private static void HideChild(Fgui.GComponent root, string name)
        {
            Fgui.GObject child = root.GetChild(name);
            if (child != null)
                child.visible = false;
        }

        private static string ResolveCategory(string iconKey)
        {
            string key = iconKey ?? string.Empty;
            if (key.EndsWith("primary-attack", StringComparison.Ordinal))
                return "攻击行为";
            if (key.EndsWith("reactive-dodge", StringComparison.Ordinal)
                || key.EndsWith("avoid-hazard", StringComparison.Ordinal))
                return "防御行为";
            if (key.EndsWith("continuous-approach", StringComparison.Ordinal)
                || key.EndsWith("distance-band-approach", StringComparison.Ordinal)
                || key.EndsWith("follow-unit", StringComparison.Ordinal)
                || key.EndsWith("go-to-position", StringComparison.Ordinal)
                || key.EndsWith("keep-distance", StringComparison.Ordinal)
                || key.EndsWith("orbit-target", StringComparison.Ordinal)
                || key.EndsWith("strafe-when-blocked", StringComparison.Ordinal))
                return "移动行为";
            return "通用行为";
        }

        private static string NonEmpty(string preferred, string fallback) =>
            string.IsNullOrWhiteSpace(preferred) ? fallback ?? string.Empty : preferred;
    }
}

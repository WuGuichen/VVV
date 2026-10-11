using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestDeveloperDebugViewStyles
    {
        public static void StyleRoot(VisualElement root)
        {
            StyleRoot(root, CharacterTestDeveloperDebugPanelAnchor.TopRight);
        }

        public static void StyleRoot(VisualElement root, CharacterTestDeveloperDebugPanelAnchor anchor)
        {
            root.style.position = Position.Absolute;
            ApplyAnchor(root, anchor);
            root.style.width = 680f;
            root.style.maxWidth = 680f;
            root.style.maxHeight = 780f;
            root.style.flexDirection = FlexDirection.Column;
            root.style.overflow = Overflow.Hidden;
            root.style.backgroundColor = Palette.Surface;
            root.style.borderTopLeftRadius = 10f;
            root.style.borderTopRightRadius = 10f;
            root.style.borderBottomLeftRadius = 10f;
            root.style.borderBottomRightRadius = 10f;
            ApplyBorder(root, Palette.Accent, 1f);
        }

        public static void ApplyAnchor(VisualElement root, CharacterTestDeveloperDebugPanelAnchor anchor)
        {
            root.style.left = new StyleLength(StyleKeyword.Auto);
            root.style.right = new StyleLength(StyleKeyword.Auto);
            root.style.top = new StyleLength(StyleKeyword.Auto);
            root.style.bottom = new StyleLength(StyleKeyword.Auto);

            switch (anchor)
            {
                case CharacterTestDeveloperDebugPanelAnchor.TopLeft:
                    root.style.left = 12f;
                    root.style.top = 12f;
                    break;
                case CharacterTestDeveloperDebugPanelAnchor.BottomRight:
                    root.style.right = 12f;
                    root.style.bottom = 12f;
                    break;
                default:
                    root.style.right = 12f;
                    root.style.top = 12f;
                    break;
            }
        }

        public static void StyleCollapsed(VisualElement collapsed)
        {
            collapsed.style.flexDirection = FlexDirection.Row;
            collapsed.style.alignItems = Align.Center;
            collapsed.style.justifyContent = Justify.SpaceBetween;
            collapsed.style.paddingLeft = 14f;
            collapsed.style.paddingRight = 10f;
            collapsed.style.paddingTop = 10f;
            collapsed.style.paddingBottom = 10f;
            collapsed.style.borderBottomWidth = 1f;
            collapsed.style.borderBottomColor = Palette.BorderSubtle;
        }

        public static void StyleCollapsedSummary(Label summary)
        {
            summary.style.flexGrow = 1f;
            summary.style.flexShrink = 1f;
            summary.style.color = Palette.TextPrimary;
            summary.style.fontSize = 12f;
            summary.style.unityFontStyleAndWeight = FontStyle.Normal;
            summary.style.marginLeft = 8f;
        }

        public static void StyleStatusDot(VisualElement dot, Color color)
        {
            dot.style.width = 8f;
            dot.style.height = 8f;
            dot.style.flexShrink = 0f;
            dot.style.backgroundColor = color;
            dot.style.borderTopLeftRadius = 4f;
            dot.style.borderTopRightRadius = 4f;
            dot.style.borderBottomLeftRadius = 4f;
            dot.style.borderBottomRightRadius = 4f;
        }

        public static void StyleExpanded(VisualElement expanded)
        {
            expanded.style.flexGrow = 1f;
            expanded.style.flexShrink = 1f;
            expanded.style.minHeight = 0f;
            expanded.style.flexDirection = FlexDirection.Column;
            expanded.style.paddingLeft = 12f;
            expanded.style.paddingRight = 12f;
            expanded.style.paddingTop = 12f;
            expanded.style.paddingBottom = 12f;
        }

        public static void StyleHeader(VisualElement header)
        {
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.FlexStart;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingBottom = 10f;
            header.style.marginBottom = 2f;
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = Palette.BorderSubtle;
        }

        public static void StyleTitleBlock(VisualElement titleBlock)
        {
            titleBlock.style.flexGrow = 1f;
            titleBlock.style.flexShrink = 1f;
            titleBlock.style.marginRight = 12f;
        }

        public static void StyleTitle(Label title)
        {
            title.style.color = Palette.TextBright;
            title.style.fontSize = 16f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 2f;
        }

        public static void StyleSubtitle(Label subtitle)
        {
            subtitle.style.color = Palette.TextMuted;
            subtitle.style.fontSize = 11f;
            subtitle.style.unityFontStyleAndWeight = FontStyle.Normal;
        }

        public static void StyleBadgeGroup(VisualElement badgeGroup)
        {
            badgeGroup.style.flexDirection = FlexDirection.Row;
            badgeGroup.style.alignItems = Align.Center;
            badgeGroup.style.flexShrink = 0f;
        }

        public static void StyleToolbar(VisualElement toolbar)
        {
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginTop = 10f;
            toolbar.style.marginBottom = 10f;
        }

        public static void StyleTabsContainer(VisualElement tabs)
        {
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.flexWrap = Wrap.Wrap;
            tabs.style.alignItems = Align.Center;
            tabs.style.backgroundColor = Palette.SurfaceInset;
            tabs.style.paddingLeft = 6f;
            tabs.style.paddingRight = 6f;
            tabs.style.paddingTop = 6f;
            tabs.style.paddingBottom = 6f;
            tabs.style.borderTopLeftRadius = 8f;
            tabs.style.borderTopRightRadius = 8f;
            tabs.style.borderBottomLeftRadius = 8f;
            tabs.style.borderBottomRightRadius = 8f;
            tabs.style.marginBottom = 10f;
        }

        public static void StyleGizmoControls(VisualElement controls)
        {
            controls.style.backgroundColor = Palette.SurfaceInset;
            controls.style.paddingLeft = 10f;
            controls.style.paddingRight = 10f;
            controls.style.paddingTop = 8f;
            controls.style.paddingBottom = 8f;
            controls.style.marginBottom = 10f;
            controls.style.borderTopLeftRadius = 8f;
            controls.style.borderTopRightRadius = 8f;
            controls.style.borderBottomLeftRadius = 8f;
            controls.style.borderBottomRightRadius = 8f;
            ApplyBorder(controls, Palette.BorderSubtle, 1f);
        }

        public static void StyleGizmoTitle(Label title)
        {
            title.style.color = Palette.TextBright;
            title.style.fontSize = 12f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 5f;
        }

        public static void StyleGizmoToggleRow(VisualElement row)
        {
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
        }

        public static void StyleGizmoToggle(Toggle toggle)
        {
            toggle.style.minWidth = 104f;
            toggle.style.height = 28f;
            toggle.style.marginRight = 8f;
            toggle.style.color = Palette.TextSecondary;
            toggle.style.fontSize = 11f;
        }

        public static void StyleTabButton(Button tab, bool active)
        {
            tab.style.flexGrow = 1f;
            tab.style.flexShrink = 1f;
            tab.style.flexBasis = 90f;
            tab.style.minWidth = 72f;
            tab.style.height = 30f;
            tab.style.paddingLeft = 6f;
            tab.style.paddingRight = 6f;
            tab.style.paddingTop = 0f;
            tab.style.paddingBottom = 0f;
            tab.style.marginLeft = 2f;
            tab.style.marginRight = 2f;
            tab.style.marginTop = 2f;
            tab.style.marginBottom = 2f;
            tab.style.fontSize = 11f;
            tab.style.borderTopLeftRadius = 6f;
            tab.style.borderTopRightRadius = 6f;
            tab.style.borderBottomLeftRadius = 6f;
            tab.style.borderBottomRightRadius = 6f;
            tab.style.borderLeftWidth = 1f;
            tab.style.borderRightWidth = 1f;
            tab.style.borderTopWidth = 1f;
            tab.style.borderBottomWidth = 1f;
            tab.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;

            if (active)
            {
                tab.style.backgroundColor = Palette.TabActive;
                tab.style.color = Palette.TextBright;
                ApplyBorder(tab, Palette.AccentBright, 1f);
            }
            else
            {
                tab.style.backgroundColor = Palette.TabIdle;
                tab.style.color = Palette.TextSecondary;
                ApplyBorder(tab, Palette.BorderSubtle, 1f);
            }
        }

        public static void StyleContent(ScrollView content)
        {
            content.style.flexGrow = 1f;
            content.style.flexShrink = 1f;
            content.style.minHeight = 120f;
            content.style.maxHeight = 560f;
            content.style.backgroundColor = Palette.ContentBackground;
            content.style.borderTopLeftRadius = 8f;
            content.style.borderTopRightRadius = 8f;
            content.style.borderBottomLeftRadius = 8f;
            content.style.borderBottomRightRadius = 8f;
            content.style.paddingLeft = 10f;
            content.style.paddingRight = 10f;
            content.style.paddingTop = 10f;
            content.style.paddingBottom = 10f;
            ApplyBorder(content, Palette.BorderSubtle, 1f);
        }

        public static void StyleEmptyState(VisualElement empty)
        {
            empty.style.alignItems = Align.Center;
            empty.style.justifyContent = Justify.Center;
            empty.style.minHeight = 120f;
            empty.style.paddingTop = 24f;
            empty.style.paddingBottom = 24f;
        }

        public static void StyleEmptyLabel(Label label)
        {
            label.style.color = Palette.TextMuted;
            label.style.fontSize = 13f;
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        public static void StyleCard(VisualElement card)
        {
            card.style.backgroundColor = Palette.CardBackground;
            card.style.borderTopLeftRadius = 8f;
            card.style.borderTopRightRadius = 8f;
            card.style.borderBottomLeftRadius = 8f;
            card.style.borderBottomRightRadius = 8f;
            card.style.paddingLeft = 0f;
            card.style.paddingRight = 0f;
            card.style.paddingTop = 0f;
            card.style.paddingBottom = 0f;
            card.style.marginBottom = 10f;
            card.style.overflow = Overflow.Hidden;
            ApplyBorder(card, Palette.BorderCard, 1f);
        }

        public static void StyleCardHeader(VisualElement header)
        {
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 12f;
            header.style.paddingRight = 12f;
            header.style.paddingTop = 8f;
            header.style.paddingBottom = 8f;
            header.style.backgroundColor = Palette.CardHeader;
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = Palette.BorderSubtle;
        }

        public static void StyleCardHeaderAccent(VisualElement accent)
        {
            accent.style.width = 3f;
            accent.style.height = 14f;
            accent.style.marginRight = 8f;
            accent.style.flexShrink = 0f;
            accent.style.backgroundColor = Palette.Accent;
            accent.style.borderTopLeftRadius = 2f;
            accent.style.borderTopRightRadius = 2f;
            accent.style.borderBottomLeftRadius = 2f;
            accent.style.borderBottomRightRadius = 2f;
        }

        public static void StyleCardHeaderTitle(Label title)
        {
            title.style.fontSize = 13f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Palette.TextBright;
            title.style.flexGrow = 1f;
        }

        public static void StyleCardBody(VisualElement body)
        {
            body.style.paddingLeft = 12f;
            body.style.paddingRight = 12f;
            body.style.paddingTop = 8f;
            body.style.paddingBottom = 10f;
        }

        public static void StyleRow(VisualElement row, bool alternate)
        {
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.paddingLeft = 6f;
            row.style.paddingRight = 6f;
            row.style.paddingTop = 4f;
            row.style.paddingBottom = 4f;
            row.style.marginBottom = 1f;
            row.style.borderTopLeftRadius = 4f;
            row.style.borderTopRightRadius = 4f;
            row.style.borderBottomLeftRadius = 4f;
            row.style.borderBottomRightRadius = 4f;
            if (alternate)
                row.style.backgroundColor = Palette.RowAlternate;
        }

        public static void StyleRowKey(Label key)
        {
            key.style.width = 148f;
            key.style.minWidth = 148f;
            key.style.flexShrink = 0f;
            key.style.color = Palette.TextLabel;
            key.style.fontSize = 11f;
            key.style.unityFontStyleAndWeight = FontStyle.Bold;
            key.style.marginRight = 10f;
        }

        public static void StyleRowValue(Label value, bool warning)
        {
            value.style.flexGrow = 1f;
            value.style.flexShrink = 1f;
            value.style.fontSize = 12f;
            value.style.color = warning ? Palette.Warning : Palette.TextValue;
        }

        public static void StyleTextLine(Label label, bool warning)
        {
            label.style.marginBottom = 4f;
            label.style.paddingLeft = 6f;
            label.style.paddingRight = 6f;
            label.style.fontSize = 12f;
            label.style.color = warning ? Palette.Warning : Palette.TextBody;
        }

        public static void StyleButton(Button button, bool hot)
        {
            button.style.height = 28f;
            button.style.minWidth = 68f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 6f;
            button.style.marginTop = 2f;
            button.style.marginBottom = 2f;
            button.style.paddingLeft = 10f;
            button.style.paddingRight = 10f;
            button.style.fontSize = 11f;
            button.style.backgroundColor = hot ? Palette.ButtonHot : Palette.ButtonNormal;
            button.style.color = Palette.TextBright;
            button.style.borderTopLeftRadius = 6f;
            button.style.borderTopRightRadius = 6f;
            button.style.borderBottomLeftRadius = 6f;
            button.style.borderBottomRightRadius = 6f;
            ApplyBorder(button, hot ? Palette.AccentBright : Palette.BorderButton, 1f);
        }

        public static void StyleBadge(Label badge)
        {
            badge.style.height = 22f;
            badge.style.paddingLeft = 10f;
            badge.style.paddingRight = 10f;
            badge.style.paddingTop = 2f;
            badge.style.paddingBottom = 2f;
            badge.style.marginLeft = 4f;
            badge.style.fontSize = 10f;
            badge.style.borderTopLeftRadius = 999f;
            badge.style.borderTopRightRadius = 999f;
            badge.style.borderBottomLeftRadius = 999f;
            badge.style.borderBottomRightRadius = 999f;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        public static void StyleMetricGrid(VisualElement grid)
        {
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginBottom = 4f;
        }

        public static void StyleMetricTile(VisualElement tile)
        {
            tile.style.flexGrow = 1f;
            tile.style.flexShrink = 1f;
            tile.style.flexBasis = 120f;
            tile.style.minWidth = 100f;
            tile.style.maxWidth = 200f;
            tile.style.marginRight = 8f;
            tile.style.marginBottom = 8f;
            tile.style.paddingLeft = 10f;
            tile.style.paddingRight = 10f;
            tile.style.paddingTop = 8f;
            tile.style.paddingBottom = 8f;
            tile.style.backgroundColor = Palette.MetricTile;
            tile.style.borderTopLeftRadius = 6f;
            tile.style.borderTopRightRadius = 6f;
            tile.style.borderBottomLeftRadius = 6f;
            tile.style.borderBottomRightRadius = 6f;
            ApplyBorder(tile, Palette.BorderSubtle, 1f);
        }

        public static void StyleMetricLabel(Label label)
        {
            label.style.fontSize = 10f;
            label.style.color = Palette.TextMuted;
            label.style.marginBottom = 4f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        public static void StyleMetricValue(Label label, bool warning)
        {
            label.style.fontSize = 18f;
            label.style.color = warning ? Palette.Warning : Palette.TextBright;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        public static void StyleStatusGrid(VisualElement grid)
        {
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
        }

        public static void StyleStatusChip(VisualElement chip, bool warning)
        {
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.flexShrink = 0f;
            chip.style.marginRight = 6f;
            chip.style.marginBottom = 6f;
            chip.style.paddingLeft = 8f;
            chip.style.paddingRight = 10f;
            chip.style.paddingTop = 5f;
            chip.style.paddingBottom = 5f;
            chip.style.backgroundColor = warning ? Palette.ChipWarning : Palette.ChipOk;
            chip.style.borderTopLeftRadius = 6f;
            chip.style.borderTopRightRadius = 6f;
            chip.style.borderBottomLeftRadius = 6f;
            chip.style.borderBottomRightRadius = 6f;
            ApplyBorder(chip, warning ? Palette.Warning : Palette.Accent, 1f);
        }

        public static void StyleChipDot(VisualElement dot, bool warning)
        {
            dot.style.width = 6f;
            dot.style.height = 6f;
            dot.style.marginRight = 6f;
            dot.style.flexShrink = 0f;
            dot.style.backgroundColor = warning ? Palette.Warning : Palette.Positive;
            dot.style.borderTopLeftRadius = 3f;
            dot.style.borderTopRightRadius = 3f;
            dot.style.borderBottomLeftRadius = 3f;
            dot.style.borderBottomRightRadius = 3f;
        }

        public static void StyleChipLabel(Label label, bool warning)
        {
            label.style.fontSize = 11f;
            label.style.color = warning ? Palette.Warning : Palette.TextPrimary;
        }

        public static void StyleChipValue(Label label)
        {
            label.style.fontSize = 11f;
            label.style.color = Palette.TextValue;
            label.style.marginLeft = 4f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        public static void StyleFlowPipeline(VisualElement pipeline)
        {
            pipeline.style.flexDirection = FlexDirection.Row;
            pipeline.style.flexWrap = Wrap.Wrap;
            pipeline.style.alignItems = Align.Stretch;
        }

        public static void StyleFlowStep(VisualElement step, bool ok)
        {
            step.style.flexGrow = 1f;
            step.style.flexShrink = 1f;
            step.style.flexBasis = 100f;
            step.style.minWidth = 90f;
            step.style.marginRight = 4f;
            step.style.marginBottom = 4f;
            step.style.paddingLeft = 8f;
            step.style.paddingRight = 8f;
            step.style.paddingTop = 8f;
            step.style.paddingBottom = 8f;
            step.style.backgroundColor = ok ? Palette.FlowOk : Palette.FlowWarn;
            step.style.borderTopLeftRadius = 6f;
            step.style.borderTopRightRadius = 6f;
            step.style.borderBottomLeftRadius = 6f;
            step.style.borderBottomRightRadius = 6f;
            ApplyBorder(step, ok ? Palette.Positive : Palette.Warning, 1f);
        }

        public static void StyleFlowStepTitle(Label label, bool ok)
        {
            label.style.fontSize = 11f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = ok ? Palette.Positive : Palette.Warning;
            label.style.marginBottom = 3f;
        }

        public static void StyleFlowStepDetail(Label label)
        {
            label.style.fontSize = 10f;
            label.style.color = Palette.TextBody;
            label.style.whiteSpace = WhiteSpace.Normal;
        }

        public static void StyleFlowArrow(Label label)
        {
            label.style.width = 16f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.color = Palette.TextMuted;
            label.style.fontSize = 14f;
            label.style.alignSelf = Align.Center;
        }

        public static void StyleLogList(VisualElement list)
        {
            list.style.flexDirection = FlexDirection.Column;
        }

        public static void StyleLogEntry(VisualElement entry, bool warning)
        {
            entry.style.flexDirection = FlexDirection.Row;
            entry.style.alignItems = Align.FlexStart;
            entry.style.marginBottom = 6f;
            entry.style.paddingLeft = 4f;
            entry.style.paddingTop = 2f;
            entry.style.paddingBottom = 2f;
        }

        public static void StyleLogBar(VisualElement bar, bool warning)
        {
            bar.style.width = 3f;
            bar.style.alignSelf = Align.Stretch;
            bar.style.minHeight = 16f;
            bar.style.marginRight = 8f;
            bar.style.flexShrink = 0f;
            bar.style.backgroundColor = warning ? Palette.Warning : Palette.Accent;
            bar.style.borderTopLeftRadius = 2f;
            bar.style.borderTopRightRadius = 2f;
            bar.style.borderBottomLeftRadius = 2f;
            bar.style.borderBottomRightRadius = 2f;
        }

        public static void StyleLogText(Label label, bool warning)
        {
            label.style.flexGrow = 1f;
            label.style.fontSize = 11f;
            label.style.color = warning ? Palette.Warning : Palette.TextBody;
            label.style.whiteSpace = WhiteSpace.Normal;
        }

        public static void StyleBoolGrid(VisualElement grid)
        {
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginBottom = 8f;
        }

        public static void StyleBoolPill(VisualElement pill, bool on)
        {
            pill.style.flexDirection = FlexDirection.Column;
            pill.style.alignItems = Align.Center;
            pill.style.minWidth = 72f;
            pill.style.marginRight = 6f;
            pill.style.marginBottom = 6f;
            pill.style.paddingLeft = 8f;
            pill.style.paddingRight = 8f;
            pill.style.paddingTop = 6f;
            pill.style.paddingBottom = 6f;
            pill.style.backgroundColor = on ? Palette.BoolOn : Palette.BoolOff;
            pill.style.borderTopLeftRadius = 6f;
            pill.style.borderTopRightRadius = 6f;
            pill.style.borderBottomLeftRadius = 6f;
            pill.style.borderBottomRightRadius = 6f;
            ApplyBorder(pill, on ? Palette.Positive : Palette.BorderSubtle, 1f);
        }

        public static void StyleBoolLabel(Label label)
        {
            label.style.fontSize = 10f;
            label.style.color = Palette.TextMuted;
            label.style.marginBottom = 2f;
        }

        public static void StyleBoolValue(Label label, bool on)
        {
            label.style.fontSize = 12f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = on ? Palette.Positive : Palette.TextSecondary;
        }

        public static void StyleVectorGrid(VisualElement grid)
        {
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
        }

        public static void StyleVectorBlock(VisualElement block)
        {
            block.style.flexGrow = 1f;
            block.style.flexShrink = 1f;
            block.style.flexBasis = 140f;
            block.style.minWidth = 130f;
            block.style.marginRight = 8f;
            block.style.marginBottom = 8f;
            block.style.alignItems = Align.Center;
        }

        public static void StyleVectorLabel(Label label)
        {
            label.style.fontSize = 10f;
            label.style.color = Palette.TextMuted;
            label.style.marginBottom = 4f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        public static void StyleVectorCanvas(VisualElement canvas)
        {
            canvas.style.width = 72f;
            canvas.style.height = 72f;
            canvas.style.backgroundColor = Palette.VectorCanvas;
            canvas.style.borderTopLeftRadius = 6f;
            canvas.style.borderTopRightRadius = 6f;
            canvas.style.borderBottomLeftRadius = 6f;
            canvas.style.borderBottomRightRadius = 6f;
            ApplyBorder(canvas, Palette.BorderSubtle, 1f);
        }

        public static void StyleVectorCrosshairH(VisualElement line)
        {
            line.style.position = Position.Absolute;
            line.style.left = 4f;
            line.style.right = 4f;
            line.style.top = Length.Percent(50f);
            line.style.height = 1f;
            line.style.backgroundColor = Palette.BorderSubtle;
        }

        public static void StyleVectorCrosshairV(VisualElement line)
        {
            line.style.position = Position.Absolute;
            line.style.top = 4f;
            line.style.bottom = 4f;
            line.style.left = Length.Percent(50f);
            line.style.width = 1f;
            line.style.backgroundColor = Palette.BorderSubtle;
        }

        public static void StyleVectorDot(VisualElement dot)
        {
            dot.style.position = Position.Absolute;
            dot.style.width = 8f;
            dot.style.height = 8f;
            dot.style.marginLeft = -4f;
            dot.style.marginTop = -4f;
            dot.style.backgroundColor = Palette.AccentBright;
            dot.style.borderTopLeftRadius = 4f;
            dot.style.borderTopRightRadius = 4f;
            dot.style.borderBottomLeftRadius = 4f;
            dot.style.borderBottomRightRadius = 4f;
        }

        public static void StyleVectorValue(Label label)
        {
            label.style.fontSize = 10f;
            label.style.color = Palette.TextValue;
            label.style.marginTop = 4f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        public static void StyleProgressBlock(VisualElement block)
        {
            block.style.marginTop = 4f;
            block.style.marginBottom = 8f;
        }

        public static void StyleProgressHeader(VisualElement row)
        {
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 4f;
        }

        public static void StyleProgressTrack(VisualElement track)
        {
            track.style.height = 8f;
            track.style.backgroundColor = Palette.ProgressTrack;
            track.style.borderTopLeftRadius = 4f;
            track.style.borderTopRightRadius = 4f;
            track.style.borderBottomLeftRadius = 4f;
            track.style.borderBottomRightRadius = 4f;
            track.style.overflow = Overflow.Hidden;
        }

        public static void StyleProgressFill(VisualElement fill, bool warning)
        {
            fill.style.height = 8f;
            fill.style.backgroundColor = warning ? Palette.Warning : Palette.Accent;
        }

        public static void StyleEntityGrid(VisualElement grid)
        {
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginBottom = 8f;
        }

        private static void ApplyBorder(VisualElement element, Color color, float width)
        {
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
        }

        private static class Palette
        {
            public static readonly Color Surface = ColorFromRgba(0x08, 0x12, 0x20, 0xF5);
            public static readonly Color SurfaceInset = ColorFromRgba(0x05, 0x0D, 0x18, 0xEE);
            public static readonly Color ContentBackground = ColorFromRgba(0x03, 0x09, 0x13, 0xE8);
            public static readonly Color CardBackground = ColorFromRgba(0x0D, 0x19, 0x2A, 0xF8);
            public static readonly Color CardHeader = ColorFromRgba(0x11, 0x22, 0x36, 0xFF);
            public static readonly Color RowAlternate = ColorFromRgba(0x0A, 0x14, 0x22, 0x88);
            public static readonly Color Accent = ColorFromRgb(0x1F, 0xD6, 0xE8);
            public static readonly Color AccentBright = ColorFromRgb(0x20, 0xD9, 0xEE);
            public static readonly Color BorderSubtle = ColorFromRgb(0x1E, 0x2F, 0x45);
            public static readonly Color BorderCard = ColorFromRgb(0x23, 0x3A, 0x55);
            public static readonly Color BorderButton = ColorFromRgb(0x2A, 0xC8, 0xDD);
            public static readonly Color TabActive = ColorFromRgb(0x10, 0x4B, 0x5E);
            public static readonly Color TabIdle = ColorFromRgb(0x12, 0x1C, 0x2D);
            public static readonly Color ButtonNormal = ColorFromRgb(0x14, 0x22, 0x35);
            public static readonly Color ButtonHot = ColorFromRgb(0x0D, 0x57, 0x66);
            public static readonly Color TextBright = ColorFromRgb(0xF2, 0xFA, 0xFF);
            public static readonly Color TextPrimary = ColorFromRgb(0xE6, 0xF2, 0xFF);
            public static readonly Color TextSecondary = ColorFromRgb(0xA8, 0xB7, 0xC7);
            public static readonly Color TextMuted = ColorFromRgb(0x6E, 0x84, 0x9C);
            public static readonly Color TextLabel = ColorFromRgb(0x83, 0x98, 0xB3);
            public static readonly Color TextValue = ColorFromRgb(0xD7, 0xE7, 0xF5);
            public static readonly Color TextBody = ColorFromRgb(0xC7, 0xD5, 0xE4);
            public static readonly Color Warning = ColorFromRgb(0xFF, 0xCB, 0x6B);
            public static readonly Color Positive = ColorFromRgb(0x99, 0xF6, 0xBF);
            public static readonly Color MetricTile = ColorFromRgba(0x0F, 0x1E, 0x30, 0xFF);
            public static readonly Color ChipOk = ColorFromRgba(0x0C, 0x22, 0x1E, 0xEE);
            public static readonly Color ChipWarning = ColorFromRgba(0x2A, 0x1A, 0x0C, 0xEE);
            public static readonly Color FlowOk = ColorFromRgba(0x0C, 0x22, 0x1E, 0xDD);
            public static readonly Color FlowWarn = ColorFromRgba(0x2A, 0x1A, 0x0C, 0xDD);
            public static readonly Color BoolOn = ColorFromRgba(0x0C, 0x28, 0x30, 0xFF);
            public static readonly Color BoolOff = ColorFromRgba(0x14, 0x1C, 0x28, 0xFF);
            public static readonly Color VectorCanvas = ColorFromRgba(0x05, 0x0C, 0x16, 0xFF);
            public static readonly Color ProgressTrack = ColorFromRgba(0x0A, 0x14, 0x22, 0xFF);
        }

        private static Color ColorFromRgb(byte r, byte g, byte b)
        {
            return ColorFromRgba(r, g, b, 0xFF);
        }

        private static Color ColorFromRgba(byte r, byte g, byte b, byte a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }
    }
}

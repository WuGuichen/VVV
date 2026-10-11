using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;


namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineBinder
    {
        private void DrawGameplayPlanStaticTracks(
            int startFrame,
            int endFrame,
            float canvasLeft,
            float canvasWidth)
        {
            _gameplayPlanRulerBackground.visible = false;
            for (int i = 0; i < GameplayPlanTrackCount; i++)
            {
                Fgui.GGraph row = _gameplayPlanTrackBackgrounds[i];
                Fgui.GTextField label = _gameplayPlanTrackLabels[i];
                label.visible = false;
                row.SetXY(0f, GameplayPlanTrackY[i]);
                row.SetSize(_root.width, GameplayPlanTrackHeight[i]);
                row.visible = !(_gameplayPlanProjection.StageCount == 1 && i == 1);
                if (!row.visible)
                    continue;
                row.DrawRect(
                    _root.width,
                    GameplayPlanTrackHeight[i],
                    1,
                    new Color32(27, 49, 61, 180),
                    (i & 1) == 0
                        ? new Color32(6, 19, 28, 246)
                        : new Color32(7, 22, 31, 246));
            }
            for (int i = 0; i <= GameplayPlanRulerDivisionCount; i++)
            {
                Fgui.GGraph tick = _gameplayPlanRulerTicks[i];
                tick.visible = false;
                Fgui.GTextField label = _gameplayPlanRulerLabels[i];
                label.visible = false;
            }
        }

        private GameplayPlanBarSlot GetGameplayPlanBar(
            List<GameplayPlanBarSlot> pool,
            int index,
            string namePrefix)
        {
            while (pool.Count <= index)
            {
                var slot = new GameplayPlanBarSlot();
                var visual = new Fgui.GComponent
                {
                    name = namePrefix + pool.Count.ToString(CultureInfo.InvariantCulture),
                    touchable = true
                };
                var background = new Fgui.GGraph { touchable = false };
                var progressFill = new Fgui.GGraph
                {
                    name = "planProgressFill",
                    touchable = false,
                    visible = false
                };
                var indeterminateSweep = new Fgui.GGraph
                {
                    name = "planIndeterminateSweep",
                    touchable = false,
                    visible = false
                };
                Fgui.GTextField label = NewGameplayPlanText(
                    string.Empty,
                    string.Empty,
                    26,
                    new Color32(230, 240, 245, 255),
                    Fgui.AlignType.Center);
                label.singleLine = true;
                Fgui.GTextField stateBadge = NewGameplayPlanText(
                    "planStateBadge",
                    string.Empty,
                    24,
                    new Color32(234, 244, 247, 255),
                    Fgui.AlignType.Center);
                Fgui.GTextField metricTargetBadge = NewGameplayPlanText(
                    "planMetricTargetBadge",
                    string.Empty,
                    22,
                    new Color32(110, 230, 170, 255),
                    Fgui.AlignType.Left);
                Fgui.GTextField restartBadge = NewGameplayPlanText(
                    "planActionRestartBadge",
                    "↻",
                    28,
                    new Color32(255, 194, 75, 255),
                    Fgui.AlignType.Center);
                restartBadge.visible = false;
                visual.AddChild(background);
                visual.AddChild(progressFill);
                visual.AddChild(indeterminateSweep);
                visual.AddChild(label);
                visual.AddChild(stateBadge);
                visual.AddChild(metricTargetBadge);
                visual.AddChild(restartBadge);
                slot.Visual = visual;
                slot.Background = background;
                slot.ProgressFill = progressFill;
                slot.IndeterminateSweep = indeterminateSweep;
                slot.Label = label;
                slot.StateBadge = stateBadge;
                slot.MetricTargetBadge = metricTargetBadge;
                slot.RestartBadge = restartBadge;
                visual.onClick.Set(context =>
                {
                    context.StopPropagation();
                    OnGameplayPlanElementClicked(slot.DetailKind, slot.StableId);
                });
                pool.Add(slot);
                _gameplayPlanLayer.AddChild(visual);
            }
            GameplayPlanBarSlot result = pool[index];
            result.Visual.visible = true;
            return result;
        }

        private void BindGameplayPlanBar(
            GameplayPlanBarSlot slot,
            int sourceIndex,
            string stableId,
            GameplayReadablePlanDetailKind detailKind,
            string label,
            string tooltip,
            float x,
            float y,
            float width,
            float height,
            Color32 fill,
            Color32 border,
            bool selected,
            bool current,
            bool conflict)
        {
            if (!string.Equals(slot.StableId, stableId, StringComparison.Ordinal))
            {
                slot.ObservedActionInstanceId = 0L;
                slot.RestartActionInstanceId = 0L;
            }
            slot.SourceIndex = sourceIndex;
            slot.StableId = stableId ?? string.Empty;
            slot.DetailKind = detailKind;
            slot.Fill = fill;
            slot.Border = border;
            slot.Selected = selected;
            slot.Current = current;
            slot.Conflict = conflict;
            slot.Visual.SetXY(x, y);
            slot.Visual.SetSize(width, height);
            slot.Visual.touchable = !string.IsNullOrEmpty(slot.StableId);
            BindTooltip(slot.Visual, tooltip);
            slot.ProgressFill.visible = false;
            slot.IndeterminateSweep.visible = false;
            slot.StateBadge.visible = false;
            slot.MetricTargetBadge.visible = false;
            slot.RestartBadge.visible = false;
            slot.Label.SetXY(8f, 0f);
            slot.Label.SetSize(Mathf.Max(1f, width - 16f), height);
            slot.Label.text = label ?? string.Empty;
            RedrawGameplayPlanBar(slot);
        }

        private static void RedrawGameplayPlanBar(GameplayPlanBarSlot slot)
        {
            Color32 fill = slot.Fill;
            if (slot.Current)
            {
                fill.r = (byte)Mathf.Min(255, fill.r + 8);
                fill.g = (byte)Mathf.Min(255, fill.g + 30);
                fill.b = (byte)Mathf.Min(255, fill.b + 28);
                fill.a = 252;
            }
            slot.Background.SetXY(0f, 0f);
            slot.Background.DrawRect(
                slot.Visual.width,
                slot.Visual.height,
                slot.Selected ? 3 : slot.Current ? 2 : 1,
                slot.Conflict
                    ? new Color32(226, 73, 67, 255)
                    : slot.Selected
                        ? new Color32(244, 249, 251, 255)
                        : slot.Current
                            ? new Color32(159, 188, 201, 255)
                            : slot.Border,
                fill);
        }

        private GameplayPlanExitSlot GetGameplayPlanExit(int index)
        {
            while (_gameplayPlanExitCards.Count <= index)
            {
                var slot = new GameplayPlanExitSlot();
                var visual = new Fgui.GComponent
                {
                    name = "planExit" + _gameplayPlanExitCards.Count.ToString(CultureInfo.InvariantCulture),
                    touchable = true
                };
                var background = new Fgui.GGraph { touchable = false };
                var progressFill = new Fgui.GGraph
                {
                    name = "planProgressFill",
                    touchable = false,
                    visible = false
                };
                var indeterminateSweep = new Fgui.GGraph
                {
                    name = "planIndeterminateSweep",
                    touchable = false,
                    visible = false
                };
                var toneAccent = new Fgui.GGraph
                {
                    name = "planToneAccent",
                    touchable = false
                };
                Fgui.GTextField label = NewGameplayPlanText(
                    string.Empty,
                    string.Empty,
                    22,
                    new Color32(235, 241, 244, 255),
                    Fgui.AlignType.Left);
                label.singleLine = true;
                Fgui.GTextField stateBadge = NewGameplayPlanText(
                    "planStateBadge",
                    string.Empty,
                    20,
                    new Color32(241, 247, 249, 255),
                    Fgui.AlignType.Center);
                visual.AddChild(background);
                visual.AddChild(progressFill);
                visual.AddChild(indeterminateSweep);
                visual.AddChild(label);
                visual.AddChild(stateBadge);
                visual.AddChild(toneAccent);
                slot.Visual = visual;
                slot.Background = background;
                slot.ProgressFill = progressFill;
                slot.IndeterminateSweep = indeterminateSweep;
                slot.ToneAccent = toneAccent;
                slot.Label = label;
                slot.StateBadge = stateBadge;
                visual.onClick.Set(context =>
                {
                    context.StopPropagation();
                    GameplayReadablePlanExitProjection projection = slot.Projection;
                    if (projection != null)
                    {
                        OnGameplayPlanElementClicked(
                            GameplayReadablePlanDetailKind.Exit,
                            projection.StableId);
                    }
                });
                _gameplayPlanExitCards.Add(slot);
                _gameplayPlanLayer.AddChild(visual);
            }
            GameplayPlanExitSlot result = _gameplayPlanExitCards[index];
            result.Visual.visible = true;
            return result;
        }

        private void BindGameplayPlanExit(
            GameplayPlanExitSlot slot,
            GameplayReadablePlanExitProjection projection,
            float x,
            float y,
            float width,
            float height)
        {
            slot.Projection = projection;
            ResolveGameplayPlanExitPalette(projection.Tone, out slot.Fill, out slot.Border);
            slot.Visual.SetXY(x, y);
            slot.Visual.SetSize(width, height);
            slot.Visual.touchable = !string.IsNullOrEmpty(projection.StableId);
            BindTooltip(slot.Visual, projection.TooltipText);
            slot.RuntimeLabel = projection.Label;
            slot.TriggeredRuntimeLabel = projection.TriggeredLabel;
            slot.CommittedRuntimeLabel = projection.CommittedLabel;
            slot.Label.SetXY(18f, 0f);
            slot.Label.SetSize(width - 30f, height);
            slot.Label.align = Fgui.AlignType.Left;
            slot.Label.text = projection.Label;
            slot.ProgressFill.visible = false;
            slot.IndeterminateSweep.visible = false;
            slot.StateBadge.visible = false;
            RedrawGameplayPlanExit(slot);
        }

        private static void RedrawGameplayPlanExit(GameplayPlanExitSlot slot)
        {
            GameplayReadablePlanExitProjection projection = slot.Projection;
            if (projection == null)
                return;
            Color32 border = projection.Committed
                ? new Color32(244, 249, 252, 255)
                : projection.Pending
                    ? new Color32(246, 193, 74, 255)
                    : slot.Border;
            byte alpha = projection.DetectorActive || projection.Committed || projection.Pending
                ? (byte)252
                : (byte)224;
            Color32 fill = slot.Fill;
            fill.a = alpha;
            slot.Background.SetXY(0f, 0f);
            slot.Background.DrawRect(
                slot.Visual.width,
                slot.Visual.height,
                projection.Selected ? 3 : 1,
                border,
                fill);
            slot.ToneAccent.SetXY(0f, 0f);
            slot.ToneAccent.DrawRect(
                5f,
                slot.Visual.height,
                0,
                slot.Border,
                slot.Border);
            slot.Visual.alpha = projection.Selected
                || projection.DetectorActive
                || projection.DetectorTriggered
                || projection.Pending
                    ? 1f
                    : projection.Committed
                        ? 0.78f
                        : 0.68f;
        }

        private GameplayPlanBranchSlot GetGameplayPlanBranch(int index)
        {
            while (_gameplayPlanBranchNodes.Count <= index)
            {
                var slot = new GameplayPlanBranchSlot();
                var visual = new Fgui.GComponent
                {
                    name = "planBranchNode" + _gameplayPlanBranchNodes.Count
                        .ToString(CultureInfo.InvariantCulture),
                    touchable = false
                };
                var diamond = new Fgui.GGraph { touchable = false };
                Fgui.GTextField badge = NewGameplayPlanText(
                    "planBranchSourceLabel",
                    string.Empty,
                    18,
                    new Color32(129, 232, 226, 255),
                    Fgui.AlignType.Center);
                visual.AddChild(diamond);
                visual.AddChild(badge);
                slot.Visual = visual;
                slot.Diamond = diamond;
                slot.Badge = badge;
                _gameplayPlanBranchNodes.Add(slot);
                _gameplayPlanLayer.AddChild(visual);
            }
            GameplayPlanBranchSlot result = _gameplayPlanBranchNodes[index];
            result.Visual.visible = true;
            return result;
        }

        private void BindGameplayPlanBranch(
            GameplayPlanBranchSlot slot,
            GameplayReadablePlanExitGroupProjection group,
            float x,
            float y)
        {
            slot.Visual.SetXY(x - 16f, y - 16f);
            slot.Visual.SetSize(32f, 32f);
            BindTooltip(slot.Visual, group.TooltipText);
            slot.Diamond.SetXY(9f, 9f);
            slot.Diamond.rotation = 45f;
            slot.Diamond.DrawRect(
                14f,
                14f,
                1,
                group.AnchorOutsideViewport
                    ? new Color32(230, 157, 56, 255)
                    : new Color32(71, 225, 218, 255),
                new Color32(6, 34, 44, 255));
            slot.Badge.visible = false;
        }

        private Fgui.GGraph GetGameplayPlanConnector()
        {
            while (_gameplayPlanConnectorLines.Count <= _gameplayPlanConnectorCount)
            {
                Fgui.GGraph line = NewGraph(
                    "planConnector" + _gameplayPlanConnectorLines.Count
                        .ToString(CultureInfo.InvariantCulture),
                    false);
                _gameplayPlanConnectorLines.Add(line);
                _gameplayPlanLayer.AddChild(line);
            }
            Fgui.GGraph result = _gameplayPlanConnectorLines[_gameplayPlanConnectorCount++];
            result.visible = true;
            return result;
        }

        private void DrawGameplayPlanLine(float x, float y, float width, float height)
        {
            Fgui.GGraph line = GetGameplayPlanConnector();
            float drawY = height >= 0f ? y : y + height;
            line.SetXY(x, drawY);
            line.DrawRect(
                width,
                Mathf.Max(3f, Mathf.Abs(height)),
                0,
                Color.clear,
                new Color32(83, 139, 153, 82));
        }

        private void DrawGameplayPlanHorizontalLine(float fromX, float toX, float y, float height)
        {
            Fgui.GGraph line = GetGameplayPlanConnector();
            float x = Mathf.Min(fromX, toX);
            line.SetXY(x, y);
            line.DrawRect(
                Mathf.Max(3f, Mathf.Abs(toX - fromX)),
                height,
                0,
                Color.clear,
                new Color32(83, 139, 153, 82));
        }

        private Fgui.GComponent GetGameplayPlanGlobalBand(int index)
        {
            while (_gameplayPlanGlobalBands.Count <= index)
            {
                var band = new Fgui.GComponent
                {
                    name = "planGlobalListening" + _gameplayPlanGlobalBands.Count
                        .ToString(CultureInfo.InvariantCulture),
                    touchable = false
                };
                band.AddChild(new Fgui.GGraph { name = "background", touchable = false });
                band.AddChild(NewGameplayPlanText(
                    "label",
                    string.Empty,
                    18,
                    new Color32(243, 183, 80, 255),
                    Fgui.AlignType.Left));
                _gameplayPlanGlobalBands.Add(band);
                _gameplayPlanLayer.AddChild(band);
            }
            Fgui.GComponent result = _gameplayPlanGlobalBands[index];
            result.visible = true;
            return result;
        }

        private void BindGameplayPlanGlobalBand(
            Fgui.GComponent band,
            GameplayReadablePlanExitGroupProjection group,
            float x,
            float width)
        {
            float clampedWidth = Mathf.Max(180f, width);
            band.SetXY(x, GameplayPlanTrackY[2] + 4f);
            band.SetSize(clampedWidth, 28f);
            BindTooltip(band, group.TooltipText);
            Fgui.GGraph background = band.GetChild("background")?.asGraph;
            background?.DrawRect(
                clampedWidth,
                28f,
                1,
                new Color32(226, 144, 32, 255),
                new Color32(91, 53, 8, 154));
            Fgui.GTextField label = band.GetChild("label")?.asTextField;
            if (label != null)
            {
                label.SetXY(10f, 0f);
                label.SetSize(clampedWidth - 20f, 28f);
                label.text = group.ListeningLabel;
            }
        }

        /// <remarks>NoAlloc for runtime frames within the configured 36,000-frame lab limit.</remarks>
    }
}

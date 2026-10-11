using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;


namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private BehaviorRecipeTimelineViewModel FindRecipeTimeline(string stageId)
        {
            for (int i = 0; i < _recipeProjection.TimelineCount; i++)
            {
                BehaviorRecipeTimelineViewModel timeline = _recipeProjection.GetTimeline(i);
                if (string.Equals(timeline.StageId, stageId, StringComparison.Ordinal))
                    return timeline;
            }
            return null;
        }

        private BehaviorRecipeTimelineViewModel FindRecipeTimelineAtViewportY(float viewportY)
        {
            float y = -_recipeVerticalOffset;
            for (int i = 0; i < _recipeProjection.TimelineCount; i++)
            {
                BehaviorRecipeTimelineViewModel timeline = _recipeProjection.GetTimeline(i);
                float height = RecipeTimelineVisualHeight(timeline);
                if (viewportY >= y && viewportY < y + height)
                    return timeline;
                y += height;
            }
            return null;
        }

        private float RecipeTimelineVisualHeight(BehaviorRecipeTimelineViewModel timeline) =>
            Math.Max(1, timeline?.LaneCount ?? 0) * RecipeTrackHeight
            + RecipeTimelineGap + (ShowTacticalTimelineHeader ? TacticalTimelineHeaderHeight : 0f);

        private float TacticalTimelineHeaderHeight => _recipeSource?.TacticalPreview?.TimingPreview != null ? 48f : 24f;

        private bool ShowTacticalTimelineHeader => _recipeSource?.InspectCompiledExecution == false
            && (_recipeSource.TacticalPreview?.WholeCard != null || _recipeSource.CompositionEditor?.EditingSourceTimeline == true);

        private int ResolveRecipePointerFrame(Fgui.GComponent content)
        {
            Vector2 point = content.GlobalToLocal(Fgui.Stage.inst.touchPosition);
            return ResolveRecipePointerFrame(content, point.x);
        }

        private int ResolveRecipePointerFrame(Fgui.GComponent _, float localX)
        {
            if (_recipeSource == null)
                return 0;
            var viewport = new BehaviorTimelineViewport(
                _recipeSource.HorizontalStartFrame,
                _recipeSource.HorizontalEndFrame,
                _recipeSource.ZoomLevel,
                _recipeVerticalOffset,
                false,
                RecipeFrameSnap);
            float width = Math.Max(1f, _recipeLayer.width - RecipeContentInset - 20f);
            var transform = new BehaviorTimelineTransform(viewport, 0f, width);
            return transform.SnapRelativeFrame(transform.XToFrame(localX));
        }

        private void HideUnusedRecipeSlots()
        {
            for (int i = _recipeVisibleTrackCount; i < _recipeTrackPool.Count; i++)
                _recipeTrackPool[i].Visual.visible = false;
            for (int i = _recipeVisibleGridCount; i < _recipeGridPool.Count; i++)
                _recipeGridPool[i].visible = false;
        }

        private Fgui.GGraph GetRecipeGridLine(int index)
        {
            while (_recipeGridPool.Count <= index)
            {
                Fgui.GGraph line = NewRecipeGraph("recipeGrid" + _recipeGridPool.Count, false);
                _recipeGridPool.Add(line);
                // Guides sit below event rows, never over labels or selection outlines.
                _recipeLayer.AddChildAt(line, _recipeLayer.GetChildIndex(_recipeViewport));
            }
            Fgui.GGraph result = _recipeGridPool[index];
            result.visible = true;
            return result;
        }

        private Fgui.GTextField GetRecipeRulerLabel(int index)
        {
            while (_recipeRulerLabelPool.Count <= index)
            {
                Fgui.GTextField label = NewRecipeText(
                    "recipeRulerLabel" + _recipeRulerLabelPool.Count,
                    string.Empty,
                    12,
                    new Color32(105, 154, 166, 255),
                    Fgui.AlignType.Center,
                    false);
                _recipeRulerLabelPool.Add(label);
                _recipeLayer.AddChild(label);
            }
            Fgui.GTextField result = _recipeRulerLabelPool[index];
            result.visible = true;
            return result;
        }

        private Fgui.GGraph NewRecipeGraph(string name, bool touchable)
        {
            return new Fgui.GGraph { name = name, touchable = touchable };
        }

        private static Fgui.GTextField NewRecipeText(
            string name,
            string text,
            int fontSize,
            Color32 color,
            Fgui.AlignType align,
            bool bold)
        {
            var result = new Fgui.GTextField
            {
                name = name,
                text = text ?? string.Empty,
                color = color,
                align = align,
                verticalAlign = Fgui.VertAlignType.Middle,
                singleLine = true,
                autoSize = Fgui.AutoSizeType.None,
                touchable = false
            };
            Fgui.TextFormat format = result.textFormat;
            format.size = fontSize;
            format.bold = bold;
            result.textFormat = format;
            return result;
        }

        private static void DrawRecipeSpan(
            Fgui.GComponent span,
            BehaviorRecipeEventViewModel recipeEvent,
            float width)
        {
            Color32 color = RecipeEventAccent(recipeEvent);
            byte fillAlpha = recipeEvent.Running ? (byte)215 : (byte)175;
            Color32 fill = new Color32(
                (byte)Mathf.Clamp(color.r * 0.28f, 12f, 70f),
                (byte)Mathf.Clamp(color.g * 0.28f, 16f, 70f),
                (byte)Mathf.Clamp(color.b * 0.28f, 22f, 85f),
                fillAlpha);
            if (!string.IsNullOrEmpty(recipeEvent.TacticalOperationKind))
                fill = new Color32((byte)(color.r * 0.55f), (byte)(color.g * 0.55f), (byte)(color.b * 0.55f), 235);
            Fgui.GGraph continuation = span.GetChild("spanContinuation")?.asGraph;
            if (continuation != null)
                continuation.visible = false;

            Fgui.GGraph body = span.GetChild("spanFill")?.asGraph;
            if (body != null)
            {
                Color32 strokeColor = recipeEvent.Invalid
                    ? new Color32(244, 79, 88, 255)
                    : recipeEvent.SourceHighlighted ? new Color32(66, 217, 188, 255) : color;
                body.DrawRect(width, 32f, recipeEvent.Invalid || recipeEvent.SourceHighlighted ? 2 : 1, strokeColor, fill);
            }

            Fgui.GGraph selection = span.GetChild("selectionOutline")?.asGraph;
            if (selection != null)
            {
                selection.visible = recipeEvent.Selected;
                selection.DrawRect(width, RecipeEventHeight, recipeEvent.Selected ? 2 : 0,
                    recipeEvent.Invalid ? new Color32(244, 79, 88, 255) : new Color32(245, 250, 255, 255), Color.clear);
            }
        }

        private static void DrawRecipeMarker(
            Fgui.GComponent marker,
            BehaviorRecipeEventViewModel recipeEvent)
        {
            Fgui.GGraph glyph = marker.GetChild("markerGlyph")?.asGraph;
            if (glyph != null)
                glyph.DrawRect(2f, RecipeEventHeight, 0, Color.clear,
                    recipeEvent.Invalid
                        ? new Color32(244, 79, 88, 255)
                        : recipeEvent.Running
                            ? new Color32(76, 235, 221, 255)
                            : RecipeEventAccent(recipeEvent));
            Fgui.GGraph selection = marker.GetChild("selectionOutline")?.asGraph;
            if (selection != null)
            {
                selection.visible = false;
            }
        }

        private static Color32 EventColor(BehaviorRecipeEventKind kind) => kind switch
        {
            BehaviorRecipeEventKind.StageEntry => new Color32(52, 211, 198, 255),
            BehaviorRecipeEventKind.PrimaryAction => new Color32(48, 142, 238, 255),
            BehaviorRecipeEventKind.LegacySharedBranch => new Color32(235, 162, 78, 255),
            _ => new Color32(168, 102, 236, 255)
        };

        private static Color32 RecipeEventAccent(BehaviorRecipeEventViewModel value) => value.TacticalOperationKind switch
        {
            "CharacterAction" => new Color32(164, 112, 224, 255),
            "ApproachOptimalRange" => new Color32(64, 205, 176, 255),
            "TurnToTarget" => new Color32(72, 153, 225, 255),
            "WaitForCondition" => new Color32(228, 179, 71, 255),
            "RetreatFromTarget" => new Color32(82, 178, 178, 255),
            "ReturnToPlanner" => new Color32(217, 132, 158, 255),
            _ => value.CharacterActionEvent ? new Color32(56, 168, 248, 255) : EventColor(value.Kind)
        };

        private static Color32 ResultColor(string result)
        {
            if (!string.IsNullOrEmpty(result)
                && (result.IndexOf("重新", StringComparison.Ordinal) >= 0
                    || result.IndexOf("Replan", StringComparison.OrdinalIgnoreCase) >= 0))
                return new Color32(244, 168, 75, 255);
            if (!string.IsNullOrEmpty(result)
                && result.IndexOf("结束", StringComparison.Ordinal) >= 0)
                return new Color32(242, 99, 106, 255);
            return new Color32(110, 232, 196, 255);
        }

        private static string[] CreateRecipePlayheadFrameText()
        {
            var result = new string[RecipePlayheadFrameTextCacheLimit];
            for (int i = 0; i < result.Length; i++)
                result[i] = i.ToString(CultureInfo.InvariantCulture) + "f";
            return result;
        }

        private static string ResolveRecipePlayheadFrameText(int frame) =>
            frame >= 0 && frame < RecipePlayheadFrameText.Length
                ? RecipePlayheadFrameText[frame]
                : Math.Max(0, frame).ToString(CultureInfo.InvariantCulture) + "f";

        private static int ResolveRecipeRulerStep(int span)
        {
            if (span <= 60) return 5;
            if (span <= 100) return 10;
            if (span <= 150) return 15;
            if (span <= 360) return 30;
            if (span <= 900) return 60;
            return 120;
        }
    }
}

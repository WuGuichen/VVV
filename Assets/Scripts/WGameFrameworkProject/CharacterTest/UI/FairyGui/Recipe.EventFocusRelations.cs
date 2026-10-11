using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private readonly List<Fgui.GGraph> _focusRelationLines = new();
        private readonly List<Fgui.GTextField> _focusRelationLabels = new();
        private void DrawEventFocusRelations(in BehaviorTimelineTransform transform)
        {
            _focusRelationLines.RemoveAll(g => g.isDisposed); _focusRelationLabels.RemoveAll(g => g.isDisposed);
            foreach (var line in _focusRelationLines) line.visible = false;
            foreach (var label in _focusRelationLabels) label.visible = false;
            var preview = _recipeSource?.TacticalPreview;
            if (preview?.FocusBlockId == null) return;
            var selection = _recipeSource.Presentation;
            var ev = preview.FindEvent(selection.SelectedRecipeTimelineId, selection.SelectedRecipeEventId);
            if (ev == null) return;
            var slot = _recipeTrackPool.FirstOrDefault(s => s.Visual.visible && s.Event?.EventId == ev.EventId);
            if (slot == null) return;
            int lineIndex = 0, labelIndex = 0;
            var scale = transform;
            Fgui.GGraph Graph()
            {
                if (lineIndex == _focusRelationLines.Count)
                { var graph = NewRecipeGraph("eventFocusRelation" + lineIndex, false); _recipeViewport.AddChild(graph); _focusRelationLines.Add(graph); }
                var line = _focusRelationLines[lineIndex++]; line.visible = true;
                line.rotation = 0; return line;
            }
            void Stroke(float ax, float ay, float bx, float by, Color32 color)
            {
                float dx = bx - ax, dy = by - ay;
                if (dx * dx + dy * dy < 0.25f) return;
                var line = Graph(); line.SetXY(ax, ay);
                line.DrawRect(Mathf.Sqrt(dx * dx + dy * dy), 2, 0, Color.clear, color);
                line.rotation = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            }
            void Endpoint(float x, float y, Color32 color)
            {
                var point = Graph(); point.SetXY(x - 3, y - 3);
                point.DrawRect(6, 6, 1, new Color32(231, 245, 242, 255), color);
            }
            void Relation(CharacterTestTacticalFrameReference reference, bool end, bool origin = false)
            {
                if (reference?.Kind != "Anchor" && reference?.Kind != "Ratio") return;
                var target = reference.TargetKind == "Event" ? _recipeTrackPool.Take(_recipeVisibleTrackCount).FirstOrDefault(s => s.Event?.EventId == reference.SourceEventId) : null;
                float x = RecipeContentInset + scale.FrameToX(origin ? 0 : slot.Event.StartFrame + (end ? slot.Event.DurationFrames : 0));
                float y = slot.Visual.y + RecipeEventY;
                float targetX, targetY;
                bool builtin = reference.TargetKind == "Builtin";
                bool outside = target == null && !builtin;
                bool missing = false;
                Color32 color = reference.Kind == "Ratio" ? new Color32(188, 155, 246, 255)
                    : end ? new Color32(102, 225, 207, 255) : new Color32(240, 192, 105, 255);
                if (builtin)
                {
                    missing = reference.SourceEventId != preview.FocusBlockId || preview.FocusBlock.Timing == null;
                    targetX = RecipeContentInset + scale.FrameToX(reference.Anchor == "Minimum" ? preview.MinimumFrames : preview.SimulationEnd);
                    targetY = 44;
                }
                else if (target != null)
                {
                    bool resolved = preview.TryGetFrames(reference.SourceEventId, out int a, out int b);
                    int frame = reference.Anchor == "Start" ? a : b;
                    if (reference.Anchor == "ActionStartupEnd")
                    {
                        if (preview.TimingPreview != null)
                        {
                            try { frame = preview.TimingPreview.Position(reference.SourceEventId, reference.Anchor); }
                            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is OverflowException)
                            { resolved = false; }
                        }
                        else if (reference.Kind == "Anchor")
                        { resolved = preview.TryGetFrames(ev.EventId, out int s, out int e); frame = (origin ? preview.DisplayOrigin : end ? e : s) - reference.OffsetFrames; }
                        else resolved = false;
                    }
                    missing = !resolved;
                    targetX = RecipeContentInset + scale.FrameToX(frame - preview.DisplayOrigin);
                    targetY = target.Visual.y + RecipeEventY;
                    if (targetY < y && target != slot) targetY += RecipeEventHeight;
                }
                else { targetX = RecipeContentInset + 8; targetY = Math.Max(24, _recipeViewport.height - 52 + labelIndex * 24); }
                var source = preview.FindEvent(selection.SelectedRecipeTimelineId, reference.SourceEventId);
                missing |= !preview.TryGetFrames(ev.EventId, out _, out _);
                if (!builtin && source == null && preview.FindBlock(reference.SourceEventId) == null) missing = true;
                if (missing || preview.EventTimeInvalid(ev.EventId)) color = new Color32(248, 116, 132, 255);
                // A bracket leaves the source anchor and ends with an arrow at the dependent
                // endpoint. Keep the direction legible even when both frames coincide.
                if (!missing)
                {
                    float bend = Math.Max(4, y - (end ? 9 : 17));
                    Stroke(targetX, targetY, targetX, bend, color);
                    Stroke(targetX, bend, x, bend, color);
                    Stroke(x, bend, x, y - 1, color);
                    Stroke(x - 5, y - 7, x, y - 1, color);
                    Stroke(x + 5, y - 7, x, y - 1, color);
                    Endpoint(targetX, targetY, color);
                    if (target != null && target.Visual.visible && target != slot)
                    {
                        var outline = Graph();
                        outline.SetXY(RecipeContentInset + scale.FrameToX(target.Event.StartFrame), target.Visual.y + RecipeEventY);
                        outline.DrawRect(Math.Max(6, scale.FrameToX(target.Event.StartFrame + target.Event.DurationFrames) - scale.FrameToX(target.Event.StartFrame)),
                            RecipeEventHeight, 1, color, Color.clear);
                    }
                }
                if (labelIndex == _focusRelationLabels.Count)
                {
                    var label = NewRecipeText("eventFocusSource" + labelIndex, "", 14, new Color32(180, 239, 226, 255), Fgui.AlignType.Left, true);
                    _recipeViewport.AddChild(label); _focusRelationLabels.Add(label);
                }
                var text = _focusRelationLabels[labelIndex++];
                string title = preview.FindBlock(reference.SourceEventId)?.Title ?? source?.Title ?? reference.SourceEventId;
                if (builtin) title = reference.Anchor == "Minimum" ? "最小持续帧" : "模拟结束帧";
                string anchor = reference.Anchor == "Start" ? source?.TimingKind == "Instant" ? "触发" : "开始"
                    : reference.Anchor == "ActionStartupEnd" ? "前摇结束" : "结束";
                text.text = (origin ? "段起点" : end ? "结束" : ev.TimingKind == "Instant" ? "触发" : "开始") + " ← "
                    + (missing ? "不可解析：" : outside ? "外部：" : "") + title + (builtin ? "" : "." + anchor)
                    + (reference.Kind == "Ratio" ? " × " + (reference.Ratio * 100).ToString("0.##") + "%" : " " + (reference.OffsetFrames >= 0 ? "+" : "") + reference.OffsetFrames + "f");
                float right = RecipeContentInset + scale.FrameToX(slot.Event.StartFrame + slot.Event.DurationFrames) + 12;
                if (slot.Event.Primitive == BehaviorRecipeEventPrimitive.Marker)
                    right = Math.Max(right, RecipeContentInset + slot.MarkerCardBackground.x + slot.MarkerCardBackground.width + 12);
                float room = _recipeViewport.width - right - 12;
                float labelWidth = Math.Min(330, Math.Max(140, text.text.Length * 13));
                bool footer = outside || room < 140;
                float labelX = footer ? RecipeContentInset + 8 : right;
                float labelY = footer ? Math.Max(4, _recipeViewport.height - 52 + (labelIndex - 1) * 24)
                    : y - 3 + (labelIndex - 1) * 22;
                labelWidth = Math.Min(labelWidth, footer ? _recipeViewport.width - labelX - 12 : room);
                var background = Graph(); background.SetXY(labelX - 4, labelY);
                background.DrawRect(labelWidth + 8, 22, 1, color, new Color32(8, 24, 32, 245));
                text.color = color; text.SetXY(labelX, labelY); text.SetSize(labelWidth, 22);
                _recipeViewport.SetChildIndex(text, _recipeViewport.numChildren - 1);
                text.visible = true; text.tooltips = text.text; text.touchable = true;
                text.onClick.Set(context => {
                    if (builtin || missing) { context.StopPropagation(); return; }
                    if (outside) preview.Editor.OpenEventFocus(selection.SelectedRecipeTimelineId, reference.SourceEventId);
                    else _commands?.SelectRecipeInspector(BehaviorRecipeInspectorTargetKind.Event, selection.SelectedRecipeTimelineId, reference.SourceEventId);
                    context.StopPropagation();
                });
            }
            if (ev.Start?.Kind == "Absolute" && ev.Start.Space == "Block") Relation(preview.FocusBlock?.Start, false, true);
            else Relation(ev.Start, false);
            Relation(ev.End, true);
        }
    }
}

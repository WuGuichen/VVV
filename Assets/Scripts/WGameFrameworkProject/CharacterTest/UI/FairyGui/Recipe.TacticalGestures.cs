using System;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private sealed class TacticalGesture
        {
            internal CharacterTestTacticalAuthoringPreview Original;
            internal CharacterTestTacticalEditToken Token;
            internal string Timeline, Event;
            internal string[] Selection;
            internal bool Resize, StartResize, Valid;
            internal int Delta;
            internal string BlockId;
            internal int BlockDuration, MinimumDuration;
            internal System.Collections.Generic.Dictionary<string, int> InstanceDurations;
            internal Fgui.GObject Capture;
        }
        private TacticalGesture _tacticalGesture;

        private void BeginTacticalGesture(BehaviorRecipeEventViewModel ev, bool resize, Fgui.EventContext context)
        {
            var preview = _recipeSource?.TacticalPreview;
            if (preview?.Editor?.CanEdit != true || !preview.HasFramePositions) return;
            string[] selected = preview.Editor.Selected(ev.EventId);
            if (resize && selected.Length != 1) { preview.Editor.Feedback("多选时不能调整单个结束边界。"); return; }
            _tacticalGesture = new TacticalGesture { Original = preview, Token = preview.Editor.Token,
                Timeline = ev.StageId, Event = ev.EventId, Selection = selected, Resize = resize,
                Capture = context.sender as Fgui.GObject };
            var block = preview.FindBlock(ev.EventId);
            if (block?.Timing != null && resize)
            {
                _tacticalGesture.BlockId = block.BlockId;
                _tacticalGesture.BlockDuration = preview.Editor.BlockDuration(block);
                _tacticalGesture.MinimumDuration = block.Timing.MinimumFrames;
                var durations = preview.Editor.Composition?.SelectedNode?.BlockDurations;
                if (durations != null)
                    _tacticalGesture.InstanceDurations = new System.Collections.Generic.Dictionary<string, int>(durations);
            }
            Fgui.Stage.inst.onKeyDown.Add(OnTacticalGestureKey);
            Application.focusChanged += OnTacticalApplicationFocus;
            _root.onRemovedFromStage.Add(OnTacticalGestureInterrupted);
            _root.onSizeChanged.Add(OnTacticalGestureInterrupted);
        }

        private static void ApplyTacticalDelta(TacticalGesture gesture, CharacterTestTacticalAuthoringSource source)
        {
            if (gesture.StartResize)
            {
                var ev = CharacterTestTacticalAuthoringEdits.Event(source, gesture.Timeline, gesture.Event);
                gesture.Original.TryGetFrames(gesture.Event, out int start, out _);
                CharacterTestTacticalEditingSession.SetResolvedPoint(ev.Start, checked(start + gesture.Delta), gesture.Original, 0);
                return;
            }
            if (!gesture.Resize && gesture.Original.TimingPreview != null)
                CharacterTestTacticalEditingSession.MoveResolved(source, gesture.Timeline, gesture.Selection, gesture.Delta, gesture.Original);
            else if (!gesture.Resize) CharacterTestTacticalAuthoringEdits.Move(source, gesture.Timeline, gesture.Selection, gesture.Delta);
            else
            {
                var ev = CharacterTestTacticalAuthoringEdits.Event(source, gesture.Timeline, gesture.Event);
                if (ev.End == null || ev.Operation.Kind == "CharacterAction" || ev.TimingKind == "Instant")
                    throw new ArgumentException("该事件的结束边界不可调整。");
                if (gesture.Original.TimingPreview != null)
                {
                    gesture.Original.TryGetFrames(gesture.Event, out _, out int end);
                    CharacterTestTacticalEditingSession.SetResolvedPoint(ev.End, checked(end + gesture.Delta), gesture.Original, 0);
                }
                else if (ev.End.Kind == "Anchor") ev.End.OffsetFrames = checked(ev.End.OffsetFrames + gesture.Delta);
                else ev.End.Frame = checked(ev.End.Frame + gesture.Delta);
            }
        }

        private void UpdateTacticalGesture()
        {
            var gesture = _tacticalGesture;
            if (gesture == null) return;
            gesture.Delta = gesture.StartResize ? _recipeGesturePreviewStartFrame - _recipeGestureBaseStartFrame
                : gesture.Resize ? _recipeGesturePreviewDurationFrames - _recipeGestureBaseDurationFrames
                : _recipeGesturePreviewStartFrame - _recipeGestureBaseStartFrame;
            try
            {
                if (gesture.BlockId != null)
                    gesture.Delta = Math.Max(gesture.MinimumDuration - gesture.BlockDuration, gesture.Delta);
                var preview = gesture.BlockId != null
                    ? gesture.Original.WithBlockDuration(gesture.BlockId, checked(gesture.BlockDuration + gesture.Delta), gesture.InstanceDurations)
                    : gesture.Original.WithEdits(s => ApplyTacticalDelta(gesture, s));
                gesture.Valid = preview.HasFramePositions;
                if (gesture.Valid)
                {
                    _recipeSource.TacticalPreview = preview;
                    _recipeSource.RecipePayload = preview.DisplayPayload;
                    RedrawRecipeTimeline();
                }
                _recipeFeedbackText.text = gesture.Valid ? "预览偏移 " + gesture.Delta + "f · 松开提交一次 · Esc取消" : preview.Diagnostic;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is OverflowException)
            { gesture.Valid = false; _recipeFeedbackText.text = error.Message; }
            _recipeFeedbackText.visible = true;
        }

        private void EndTacticalGesture()
        {
            var gesture = _tacticalGesture;
            bool moved = _recipeGestureMoved;
            if (!moved || gesture.BlockId != null && gesture.Delta == 0)
            { CancelRecipeGesture(); return; }
            if (!gesture.Valid || gesture.Original.Compilation?.DependencyFingerprint != gesture.Original.Editor.Preview.Compilation?.DependencyFingerprint)
            { CancelRecipeGesture(); gesture.Original.Editor.Feedback("拖动无效或装配已变化；源未修改。"); return; }
            // Keep the last derived preview visible until the committed source replaces it.
            // Release capture before committing, since the commit can rebind this timeline.
            ClearRecipeGesture(restorePreview: false);
            bool committed = false;
            try
            {
                committed = gesture.BlockId != null
                    ? gesture.Original.Editor.SetBlockDuration(gesture.Token, gesture.BlockId, checked(gesture.BlockDuration + gesture.Delta))
                    : gesture.Original.Editor.Commit(gesture.Token, gesture.Resize ? "调整结束边界" : "移动事件",
                        s => ApplyTacticalDelta(gesture, s));
            }
            finally
            {
                if (!committed)
                {
                    RestoreTacticalPreview(gesture);
                    RedrawRecipeTimeline();
                }
            }
        }

        private void RestoreTacticalPreview(TacticalGesture gesture)
        {
            if (_recipeSource?.TacticalPreview?.Editor == gesture.Original.Editor)
            {
                var current = gesture.Original.Editor.Preview;
                _recipeSource.TacticalPreview = current;
                _recipeSource.RecipePayload = current.DisplayPayload;
            }
        }

        private void RestoreTacticalGesture(bool restorePreview)
        {
            if (_tacticalGesture == null) return;
            if (restorePreview) RestoreTacticalPreview(_tacticalGesture);
            Application.focusChanged -= OnTacticalApplicationFocus;
            if (_root != null && !_root.isDisposed) { _root.onRemovedFromStage.Remove(OnTacticalGestureInterrupted); _root.onSizeChanged.Remove(OnTacticalGestureInterrupted); }
            if (Application.isPlaying)
            {
                Fgui.Stage.inst.onKeyDown.Remove(OnTacticalGestureKey);
                if (_tacticalGesture.Capture != null) Fgui.Stage.inst.RemoveTouchMonitor(_tacticalGesture.Capture);
            }
            _tacticalGesture = null;
        }
        private void OnTacticalGestureInterrupted() { if (_tacticalGesture != null) CancelRecipeGesture(); }
        private void OnTacticalApplicationFocus(bool focused) { if (!focused) OnTacticalGestureInterrupted(); }
        private void OnTacticalGestureKey(Fgui.EventContext context)
        { if (context.inputEvent?.keyCode == KeyCode.Escape) { CancelRecipeGesture(); context.StopPropagation(); } }
    }
}

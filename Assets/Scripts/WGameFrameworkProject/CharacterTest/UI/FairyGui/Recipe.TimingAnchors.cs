using System;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private Fgui.GTextField[] _timingAnchorHandles;
        private Fgui.GGraph[] _timingAnchorLines;
        private CharacterTestTacticalAuthoringPreview _anchorOriginal;
        private CharacterTestTacticalEditToken _anchorToken;
        private int _anchorKind = -1, _anchorFrame;
        private int _anchorInitialEnd;
        private int _anchorInitialFrame, _anchorPointerFrame;

        private void DrawTimingAnchors(in BehaviorTimelineTransform transform)
        {
            var preview = _recipeSource?.TacticalPreview;
            bool visible = preview?.TimingPreview != null && preview.FocusBlock != null;
            if (_timingAnchorHandles == null && !visible) return;
            if (_timingAnchorHandles == null)
            {
                _timingAnchorHandles = new Fgui.GTextField[2]; _timingAnchorLines = new Fgui.GGraph[2];
                for (int i = 0; i < 2; i++)
                {
                    int index = i;
                    Color color = i == 0 ? new Color32(244, 189, 89, 255) : new Color32(70, 219, 196, 255);
                    var handle = NewRecipeText("timingAnchor" + i, "", 14, color, Fgui.AlignType.Center, true);
                    handle.touchable = true; handle.tooltips = i == 0 ? "拖动修改最小持续帧；松开提交，Esc取消" : "拖动模拟结束帧，只重算预览";
                    _recipeLayer.AddChild(handle); _timingAnchorHandles[i] = handle;
                    var line = NewRecipeGraph("timingAnchorLine" + i, false); _recipeViewport.AddChild(line); _timingAnchorLines[i] = line;
                    handle.onTouchBegin.Set(context => {
                        var current = _recipeSource?.TacticalPreview;
                        if (current?.TimingPreview == null || current.IsFrozen || index == 0 && current.Editor?.CanEdit != true) return;
                        _anchorOriginal = current; _anchorToken = current.Editor.Token; _anchorKind = index;
                        _anchorInitialEnd = current.SimulationEnd;
                        _anchorFrame = index == 0 ? current.MinimumFrames : current.SimulationEnd;
                        _anchorInitialFrame = _anchorFrame;
                        _anchorPointerFrame = ResolveRecipePointerFrame(_recipeViewport,
                            _recipeViewport.GlobalToLocal(Fgui.Stage.inst.touchPosition).x - RecipeContentInset);
                        Fgui.Stage.inst.onKeyDown.Add(OnTimingAnchorKey);
                        Application.focusChanged += OnTimingAnchorFocus;
                        context.CaptureTouch(); context.StopPropagation();
                    });
                    handle.onTouchMove.Set(UpdateTimingAnchor);
                    handle.onTouchEnd.Set(context => { EndTimingAnchor(false); context.StopPropagation(); });
                    handle.onRemovedFromStage.Add(() => EndTimingAnchor(true));
                }
            }
            for (int i = 0; i < 2; i++)
            {
                var handle = _timingAnchorHandles[i]; var line = _timingAnchorLines[i];
                handle.visible = line.visible = visible;
                if (!visible) continue;
                int frame = i == 0 ? preview.MinimumFrames : preview.SimulationEnd;
                float x = RecipeContentInset + transform.FrameToX(frame);
                bool inView = x >= RecipeContentInset && x <= _recipeLayer.width - 20;
                handle.visible = line.visible = inView;
                handle.text = (i == 0 ? "最小持续 " : "模拟结束 ") + frame + "f";
                handle.SetSize(132, 22);
                handle.SetXY(Mathf.Clamp(x - (i == 0 ? 130 : 0), RecipeContentInset, Math.Max(RecipeContentInset, _recipeLayer.width - 150)), RecipeHeaderHeight + RecipeRulerHeight + 22);
                line.SetXY(x, 44); line.DrawRect(2, Math.Max(1, _recipeViewport.height - 44), 0, Color.clear, handle.color);
                _recipeLayer.SetChildIndex(handle, _recipeLayer.numChildren - 1);
            }
        }
        private void UpdateTimingAnchor(Fgui.EventContext context)
        {
            if (_anchorKind < 0 || _anchorOriginal == null) return;
            float local = _recipeViewport.GlobalToLocal(Fgui.Stage.inst.touchPosition).x - RecipeContentInset;
            _anchorFrame = Math.Max(0, _anchorInitialFrame + ResolveRecipePointerFrame(_recipeViewport, local) - _anchorPointerFrame);
            try
            {
                if (_anchorKind == 1)
                {
                    _anchorFrame = Math.Max(_anchorOriginal.MinimumFrames, _anchorFrame);
                    _anchorOriginal.Editor.SetSimulationEnd(_anchorFrame);
                    _anchorOriginal.EnableTimingSimulation(_anchorFrame);
                    _recipeSource.TacticalPreview = _anchorOriginal;
                }
                else
                    _recipeSource.TacticalPreview = _anchorOriginal.WithEdits(source => {
                        var block = CharacterTestTacticalAuthoringEdits.Timeline(source, _anchorOriginal.Editor.Composition.FocusTimelineId)
                            .EventBlocks.Find(b => b.BlockId == _anchorOriginal.FocusBlockId);
                        block.Timing.MinimumFrames = _anchorFrame;
                        block.Timing.DurationFrames = Math.Max(_anchorFrame, block.Timing.DurationFrames);
                    });
                _recipeSource.RecipePayload = _recipeSource.TacticalPreview.DisplayPayload;
                RedrawRecipeTimeline();
                _recipeFeedbackText.text = _recipeSource.TacticalPreview.Diagnostic;
                _recipeFeedbackText.visible = !string.IsNullOrEmpty(_recipeFeedbackText.text);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is OverflowException)
            { _recipeFeedbackText.text = e.Message; _recipeFeedbackText.visible = true; }
            context.StopPropagation();
        }
        private void EndTimingAnchor(bool cancel)
        {
            if (_anchorKind < 0) return;
            var original = _anchorOriginal; int kind = _anchorKind, frame = _anchorFrame;
            _anchorKind = -1; _anchorOriginal = null;
            if (Fgui.Stage.inst != null) Fgui.Stage.inst.onKeyDown.Remove(OnTimingAnchorKey);
            Application.focusChanged -= OnTimingAnchorFocus;
            if (cancel) { original.Editor.SetSimulationEnd(_anchorInitialEnd); return; }
            if (kind == 0) original.Editor.SetMinimum(_anchorToken, frame);
        }
        private void OnTimingAnchorKey(Fgui.EventContext context)
        { if (context.inputEvent?.keyCode == KeyCode.Escape) { EndTimingAnchor(true); context.StopPropagation(); } }
        private void OnTimingAnchorFocus(bool focused) { if (!focused) EndTimingAnchor(true); }
    }
}

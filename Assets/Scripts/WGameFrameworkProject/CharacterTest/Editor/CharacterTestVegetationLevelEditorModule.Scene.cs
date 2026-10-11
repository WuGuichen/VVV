using System;
using System.Diagnostics;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestVegetationLevelEditorModule
    {
        private enum BrushMode { Off, Point, Paint, Erase, Select }
        private BrushMode _mode, _gestureMode;
        private CharacterTestVegetationBrushStroke _stroke;
        private CharacterTestVegetationSurfaceSampler _surface;
        private Vector3 _cursor, _lastStamp;
        private bool _cursorValid, _gestureActive, _subtractSelection;
        private long _gestureStarted;
        private int _hotControl;

        private void SetMode(BrushMode mode)
        {
            FinishGesture(false); _mode = mode;
            _status = mode == BrushMode.Off ? "已结束植被编辑。" : "在指定地面上点放／拖刷；Esc 取消当前整笔。";
            _window?.RefreshWorkspaceState(); SceneView.RepaintAll();
        }
        public void CancelScenePlacement(MxLevelEditorContext context) { FinishGesture(true); _mode = BrushMode.Off; SceneView.RepaintAll(); }

        public void HandleScenePlacement(MxLevelEditorPlacementContext context, SceneView sceneView)
        {
            if (_mode == BrushMode.Off || !SupportsScenePlacement || Event.current == null) return;
            Event current = Event.current; int control = GUIUtility.GetControlID(FocusType.Passive);
            if (current.type == EventType.Layout) { HandleUtility.AddDefaultControl(control); return; }
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            { CancelScenePlacement(context?.EditorContext); current.Use(); return; }
            if (current.alt || current.button > 0) return;
            if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag || current.type == EventType.MouseDown)
            {
                if (!_gestureActive) _surface = new CharacterTestVegetationSurfaceSampler(_owner.SurfaceRoot, _settings.MaximumSlope);
                _cursorValid = _surface.TryRay(HandleUtility.GUIPointToWorldRay(current.mousePosition), out var hit);
                if (_cursorValid) _cursor = hit.Position;
                if (!_cursorValid && current.type == EventType.MouseDown)
                { _status = string.IsNullOrEmpty(_surface.Issue) ? "未命中地面范围，或坡度超限；没有修改场景。" : _surface.Issue; _window?.RefreshWorkspaceState(); current.Use(); }
                sceneView?.Repaint();
            }
            if (current.type == EventType.MouseDown && current.button == 0 && _cursorValid)
            {
                _gestureActive = true; _gestureMode = current.shift && (_mode == BrushMode.Point || _mode == BrushMode.Paint) ? BrushMode.Erase : _mode;
                _subtractSelection = current.control || current.command;
                if (_gestureMode == BrushMode.Select && !current.shift && !_subtractSelection) _selected.Clear();
                else if (_gestureMode != BrushMode.Select)
                    _stroke = new CharacterTestVegetationBrushStroke(_owner, _settings, _surface, PaintType != null ? PaintType.Id : 1);
                _gestureStarted = Stopwatch.GetTimestamp(); _hotControl = control; GUIUtility.hotControl = control;
                _lastStamp = _cursor; Stamp(_cursor); current.Use(); return;
            }
            if (current.type == EventType.MouseDrag && _gestureActive && GUIUtility.hotControl == _hotControl)
            {
                if (_cursorValid && _gestureMode != BrushMode.Point)
                {
                    float step = Mathf.Max(0.05f, _settings.Radius * 0.35f); Vector3 segment = _cursor - _lastStamp;
                    float distance = segment.magnitude;
                    int samples = Mathf.Min(256, Mathf.FloorToInt(distance / step));
                    for (int i = 1; i <= samples; i++) Stamp(_lastStamp + segment * (i * step / distance));
                    if (samples > 0) _lastStamp += segment * (samples * step / distance);
                }
                current.Use(); return;
            }
            if (current.type == EventType.MouseUp && _gestureActive)
            { FinishGesture(false); current.Use(); }
        }
        private void Stamp(Vector3 center)
        {
            if (_gestureMode == BrushMode.Select)
            {
                foreach (var item in _owner.Instances)
                    if (CharacterTestVegetationBrushStroke.WithinBrush(_owner.transform.TransformPoint(item.Position), center, _settings.Radius))
                    { if (_subtractSelection) _selected.Remove(item.Id); else _selected.Add(item.Id); }
                UpdateCount(); SceneView.RepaintAll(); return;
            }
            if (_gestureMode == BrushMode.Erase) _stroke?.Erase(center);
            else _stroke?.Paint(center, _gestureMode == BrushMode.Point);
            UpdateCount(); _window?.RefreshWorkspaceState(); SceneView.RepaintAll();
        }
        private void FinishGesture(bool cancel)
        {
            if (!_gestureActive && _stroke == null) return;
            int count = _stroke?.ChangedCount ?? 0; _stroke?.Complete(cancel); _stroke = null; _gestureActive = false;
            if (GUIUtility.hotControl == _hotControl) GUIUtility.hotControl = 0;
            _lastStrokeMs = (Stopwatch.GetTimestamp() - _gestureStarted) * 1000d / Stopwatch.Frequency;
            _status = cancel ? "已取消当前整笔。" : "本笔处理 " + count + " 簇草；点击顶部保存。";
            PruneSelection(); _window?.RefreshWorkspaceState(); _context?.RequestRefresh?.Invoke(); SceneView.RepaintAll();
        }
        public void DrawScenePlacement(MxLevelEditorPlacementContext context, SceneView sceneView)
        {
            if (_mode == BrushMode.Off || !_cursorValid || Event.current?.type != EventType.Repaint) return;
            Color old = Handles.color;
            BrushMode effective = _gestureActive ? _gestureMode : Event.current.shift && (_mode == BrushMode.Paint || _mode == BrushMode.Point) ? BrushMode.Erase : _mode;
            Handles.color = effective == BrushMode.Erase ? new Color(1, 0.35f, 0.2f, 0.9f) : effective == BrushMode.Select ? Color.cyan : new Color(0.3f, 1, 0.35f, 0.9f);
            Handles.DrawWireDisc(_cursor, Vector3.up, effective == BrushMode.Point ? 0.15f : _settings.Radius);
            Handles.color = old;
        }
        public void DrawSceneView(MxLevelEditorSceneViewContext context, SceneView sceneView)
        {
            if (_owner == null || _selected.Count == 0 || Event.current?.type != EventType.Repaint) return;
            Color old = Handles.color; Handles.color = Color.cyan; int drawn = 0;
            foreach (var item in _owner.Instances)
            {
                if (!_selected.Contains(item.Id)) continue;
                Vector3 p = _owner.transform.TransformPoint(item.Position);
                Handles.DrawWireDisc(p + Vector3.up * 0.03f, Vector3.up, 0.18f);
                if (++drawn >= 500) break; // Keep large selection feedback bounded; authority still contains every selected ID.
            }
            Handles.color = old;
        }
    }
}

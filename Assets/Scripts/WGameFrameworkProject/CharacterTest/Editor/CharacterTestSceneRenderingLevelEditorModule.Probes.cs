using System;
using System.Collections.Generic;
using MxFramework.Rendering;
using MxFramework.Rendering.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestSceneRenderingLevelEditorModule
    {
        private readonly MxSceneLightingProbePreview _probes = new MxSceneLightingProbePreview();
        private readonly List<int> _visibleProbes = new List<int>(512);
        private bool _probeAttached, _probeEnabled, _probeCached = true, _probeSlice = true, _probeLocal = true;
        private float _probeHeight, _probeHalfThickness = 2, _probeRadius = 24;
        private int _probeLimit = 512, _probeValidity, _selectedProbe = -1, _visibilityBin;
        private bool _probeFilterInitialized;
        private double _nextProbeRefresh;
        private Label _probeSummary, _probeSelected, _probeDisplay;
        private IntegerField _probeIndex;

        private void ResetProbeOwner()
        { _selectedProbe = -1; _probeFilterInitialized = false; _rangeEditing=false; _rangeChanged=false; _probes.Dispose(); _visibleProbes.Clear(); }

        private void AttachProbePreview()
        {
            if (_probeAttached) return;
            _probeAttached = true;
            SceneView.duringSceneGui += DrawProbePreview;
            EditorApplication.update += UpdateProbePreview;
            Undo.undoRedoPerformed += ProbeUndo;
            EditorApplication.playModeStateChanged += ProbePlayState;
            if (_probeEnabled) RefreshProbePreview(true);
        }
        private void DetachProbePreview()
        {
            _probeAttached = false;
            SceneView.duringSceneGui -= DrawProbePreview;
            EditorApplication.update -= UpdateProbePreview;
            Undo.undoRedoPerformed -= ProbeUndo;
            EditorApplication.playModeStateChanged -= ProbePlayState;
            _probes.Dispose(); _visibleProbes.Clear();
            SceneView.RepaintAll();
        }
        private void ProbeUndo() { if (_probeEnabled) RefreshProbePreview(true); RebuildBody(); }
        private void ProbePlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) { _probes.Dispose(); _visibleProbes.Clear(); SceneView.RepaintAll(); }
            if (state == PlayModeStateChange.EnteredEditMode && _probeEnabled) { OnEnable(); RefreshProbePreview(true); }
        }
        private void UpdateProbePreview()
        {
            if (!_probeEnabled || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < _nextProbeRefresh) return;
            if (_rangeEditing && GUIUtility.hotControl != 0) return;
            _nextProbeRefresh = EditorApplication.timeSinceStartup + 2;
            if (_owner == null || _owner.gameObject.scene != SceneManager.GetActiveScene())
            { OnEnable(); _selectedProbe = -1; _probeFilterInitialized = false; RebuildBody(); }
            RefreshProbePreview();
        }
        private void RefreshProbePreview(bool force = false)
        {
            if (!_probeEnabled || EditorApplication.isPlayingOrWillChangePlaymode) return;
            _probes.Refresh(_owner?.SceneRendering, force);
            if (!_probeFilterInitialized && (_probes.HasCache || _probes.HasPlan))
            {
                var grid = _probes.HasCache ? _probes.CachedGrid : _probes.PlannedGrid;
                _probeHeight = grid.GridBounds.center.y; _probeHalfThickness = grid.Spacing * .5f;
                _probeFilterInitialized = true;
                RebuildBody();
            }
            UpdateProbeLabels(); SceneView.RepaintAll();
        }

        private void BuildProbePreviewPanel(MxSceneRenderingConfiguration c, bool readOnly)
        {
            var section = Section("探针布置预览");
            section.name = "scene-lighting-probe-preview";
            var coverage = new Toggle("独立探针覆盖范围（世界坐标）") { value = c.UseProbeCoverageBounds, name = "scene-lighting-probe-coverage" };
            coverage.SetEnabled(!readOnly && !MxSceneLightingBuilder.IsBuilding);
            coverage.RegisterValueChangedCallback(evt => { EditScene("调整探针覆盖模式", x => x.UseProbeCoverageBounds=evt.newValue); RefreshProbePreview(true); RebuildBody(); });
            section.Add(coverage);
            BuildProbeQualityControls(section,c,readOnly);
            BuildProbeRangeControls(section,c,readOnly);
            if (c.UseProbeCoverageBounds)
            {
                var center=new Vector3Field("覆盖中心") { value=c.ProbeCoverageBounds.center };
                var size=new Vector3Field("覆盖尺寸") { value=c.ProbeCoverageBounds.size };
                center.SetEnabled(!readOnly && !MxSceneLightingBuilder.IsBuilding);size.SetEnabled(!readOnly && !MxSceneLightingBuilder.IsBuilding);
                center.RegisterValueChangedCallback(evt =>
                {
                    if (!Finite(evt.newValue.x)||!Finite(evt.newValue.y)||!Finite(evt.newValue.z)) { center.SetValueWithoutNotify(c.ProbeCoverageBounds.center);return; }
                    EditScene("调整探针覆盖中心",x=>x.ProbeCoverageBounds=new Bounds(evt.newValue,x.ProbeCoverageBounds.size));RefreshProbePreview(true);
                });
                size.RegisterValueChangedCallback(evt =>
                {
                    Vector3 s=evt.newValue;
                    if (!Finite(s.x)||!Finite(s.y)||!Finite(s.z)||s.x<=0||s.y<=0||s.z<=0) {size.SetValueWithoutNotify(c.ProbeCoverageBounds.size);return;}
                    EditScene("调整探针覆盖尺寸",x=>x.ProbeCoverageBounds=new Bounds(x.ProbeCoverageBounds.center,s));RefreshProbePreview(true);
                });
                section.Add(center);section.Add(size);
                section.Add(new HelpBox("覆盖体积独立于静态捕获源，网格外侧另保留一格插值余量。修改后需要显式重建，不自动烘焙。",HelpBoxMessageType.Info));
            }
            var spacing = new FloatField("构建间距（默认 4）") { value = c.ProbeSpacing, isDelayed = true };
            spacing.name = "scene-lighting-probe-spacing";
            spacing.SetEnabled(!readOnly && !MxSceneLightingBuilder.IsBuilding);
            spacing.RegisterValueChangedCallback(evt =>
            {
                if (float.IsNaN(evt.newValue) || float.IsInfinity(evt.newValue)) { spacing.SetValueWithoutNotify(c.ProbeSpacing); return; }
                EditScene("调整探针构建间距", x => x.ProbeSpacing = Mathf.Max(1, evt.newValue));
                spacing.SetValueWithoutNotify(c.ProbeSpacing); RefreshProbePreview(true);
            });
            section.Add(spacing);
            section.Add(new HelpBox("改间距不会自动构建；旧缓存仍按实际点位展示，并标为过期。青色：计划点（有效性未知）；绿色／红色：缓存有效／无效标记。", HelpBoxMessageType.Info));
            AddToggle(section, "在 Scene 视图显示探针", _probeEnabled, value =>
            {
                _probeEnabled = value;
                if (value) RefreshProbePreview(true);
                else { _probes.Dispose(); _visibleProbes.Clear(); UpdateProbeLabels(); SceneView.RepaintAll(); }
            });
            var mode = new PopupField<string>("显示来源", new List<string> { "已构建缓存（真实点位）", "构建计划（未知有效性）" }, _probeCached ? 0 : 1);
            mode.RegisterValueChangedCallback(evt => { _probeCached = evt.newValue == "已构建缓存（真实点位）"; _selectedProbe = -1; UpdateProbeLabels(); SceneView.RepaintAll(); });
            section.Add(mode);
            AddToggle(section, "按世界高度切片", _probeSlice, value => { _probeSlice = value; SceneView.RepaintAll(); });
            AddFloat(section, "切片中心 Y", _probeHeight, value => { if (Finite(value)) _probeHeight = value; SceneView.RepaintAll(); });
            AddFloat(section, "切片半厚度", _probeHalfThickness, value => { if (Finite(value)) _probeHalfThickness = Mathf.Max(0, value); SceneView.RepaintAll(); });
            AddToggle(section, "仅显示 Scene 视图焦点附近", _probeLocal, value => { _probeLocal = value; SceneView.RepaintAll(); });
            AddFloat(section, "局部半径", _probeRadius, value => { if (Finite(value)) _probeRadius = Mathf.Max(0, value); SceneView.RepaintAll(); });
            var limit = new IntegerField("绘制上限（1–2000）") { value = _probeLimit, isDelayed = true };
            limit.RegisterValueChangedCallback(evt => { _probeLimit = Mathf.Clamp(evt.newValue, 1, 2000); limit.SetValueWithoutNotify(_probeLimit); SceneView.RepaintAll(); });
            section.Add(limit);
            var validity = new PopupField<string>("缓存有效性筛选", new List<string> { "全部", "有效", "无效" }, _probeValidity);
            validity.RegisterValueChangedCallback(evt => { _probeValidity = evt.newValue == "有效" ? 1 : evt.newValue == "无效" ? 2 : 0; SceneView.RepaintAll(); });
            section.Add(validity);
            section.Add(new Button(() => RefreshProbePreview(true)) { text = "刷新范围和缓存状态" });
            section.Add(new Button(() =>
            {
                var view = SceneView.lastActiveSceneView;
                if (view == null || !(_probeCached ? _probes.HasCache : _probes.HasPlan)) return;
                view.Frame((_probeCached ? _probes.CachedGrid : _probes.PlannedGrid).GridBounds, false);
            }) { text = "定位网格范围" });
            _probeSummary = new Label { name = "scene-lighting-probe-summary" };
            _probeSummary.style.whiteSpace = WhiteSpace.Normal; section.Add(_probeSummary);
            _probeDisplay = new Label { name = "scene-lighting-probe-display-count" }; section.Add(_probeDisplay);
            _probeIndex = new IntegerField("选中探针 ID（-1 取消）") { value = _selectedProbe, isDelayed = true };
            _probeIndex.name = "scene-lighting-probe-index";
            _probeIndex.RegisterValueChangedCallback(evt => SelectProbe(evt.newValue)); section.Add(_probeIndex);
            var bin = new IntegerField("方向距离分箱（0–95）") { value = _visibilityBin, isDelayed = true };
            bin.RegisterValueChangedCallback(evt => { _visibilityBin = Mathf.Clamp(evt.newValue, 0, 95); bin.SetValueWithoutNotify(_visibilityBin); UpdateProbeLabels(); SceneView.RepaintAll(); });
            section.Add(bin);
            _probeSelected = new Label { name = "scene-lighting-probe-selected" };
            _probeSelected.style.whiteSpace = WhiteSpace.Normal; section.Add(_probeSelected);
            // Preview state is transient. Play Mode remains read-only and does not allocate or draw an authoring overlay.
            section.SetEnabled(!readOnly);
            UpdateProbeLabels();
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void BuildProbeQualityControls(VisualElement section,MxSceneRenderingConfiguration c,bool readOnly)
        {
            var quality=c.ProbeQuality??new MxSceneLightingProbeQuality();
            bool editable=!readOnly && !MxSceneLightingBuilder.IsBuilding;
            var enabled=new Toggle("启用探针质量优化（需重建 v2 缓存）") {value=quality.Enabled,name="scene-lighting-probe-quality"};
            enabled.SetEnabled(editable);section.Add(enabled);
            enabled.RegisterValueChangedCallback(evt=>{EditScene("调整探针质量模式",x=>{x.ProbeQuality??=new MxSceneLightingProbeQuality();x.ProbeQuality.Enabled=evt.newValue;});RefreshProbePreview(true);RebuildBody();});
            if(!quality.Enabled)return;
            BuildAutomaticProbeControls(section,c,editable);
            var relocation=new Toggle("修正近表面或内部点位并重新捕获") {value=quality.RelocateNearGeometry};
            relocation.SetEnabled(editable);section.Add(relocation);
            relocation.RegisterValueChangedCallback(evt=>{EditScene("调整探针点位修正",x=>x.ProbeQuality.RelocateNearGeometry=evt.newValue);RefreshProbePreview(true);});
            var clearance=new FloatField("最小表面距离") {value=quality.MinimumClearance,isDelayed=true};
            clearance.SetEnabled(editable);section.Add(clearance);
            clearance.RegisterValueChangedCallback(evt=>{if(!Finite(evt.newValue))return;EditScene("调整探针表面距离",x=>x.ProbeQuality.MinimumClearance=Mathf.Max(.025f,evt.newValue));clearance.SetValueWithoutNotify(quality.MinimumClearance);RefreshProbePreview(true);});
            var dilation=new Toggle("可见性约束补偿（不穿墙）") {value=quality.VisibilityConstrainedDilation};
            dilation.SetEnabled(editable);section.Add(dilation);
            dilation.RegisterValueChangedCallback(evt=>{EditScene("调整探针补偿",x=>x.ProbeQuality.VisibilityConstrainedDilation=evt.newValue);RefreshProbePreview(true);});
            var budget=new IntegerField("探针总数预算") {value=quality.MaximumProbeCount,isDelayed=true};
            budget.SetEnabled(editable);section.Add(budget);
            budget.RegisterValueChangedCallback(evt=>{EditScene("调整探针预算",x=>x.ProbeQuality.MaximumProbeCount=Mathf.Clamp(evt.newValue,100,120000));budget.SetValueWithoutNotify(quality.MaximumProbeCount);RefreshProbePreview(true);});
            section.Add(new HelpBox("基础间距不变；仅在以下世界坐标体积内细分为半间距。细分砖共享点位，边界平滑混合。有效标记、点位修正和补偿均显示真实缓存结果。",HelpBoxMessageType.Info));
            var volumes=quality.LocalRefinementVolumes??Array.Empty<Bounds>();
            for(int index=0;index<volumes.Length;index++)
            {
                int i=index;
                var center=new Vector3Field($"加密范围 {i+1} 中心") {value=volumes[i].center};
                var size=new Vector3Field($"加密范围 {i+1} 尺寸") {value=volumes[i].size};
                center.SetEnabled(editable);size.SetEnabled(editable);section.Add(center);section.Add(size);
                center.RegisterValueChangedCallback(evt=>{Vector3 p=evt.newValue;if(!Finite(p.x)||!Finite(p.y)||!Finite(p.z))return;EditScene("调整探针加密中心",x=>x.ProbeQuality.LocalRefinementVolumes[i]=new Bounds(p,x.ProbeQuality.LocalRefinementVolumes[i].size));RefreshProbePreview(true);});
                size.RegisterValueChangedCallback(evt=>{Vector3 p=evt.newValue;if(!Finite(p.x)||!Finite(p.y)||!Finite(p.z)||p.x<=0||p.y<=0||p.z<=0)return;EditScene("调整探针加密尺寸",x=>x.ProbeQuality.LocalRefinementVolumes[i]=new Bounds(x.ProbeQuality.LocalRefinementVolumes[i].center,p));RefreshProbePreview(true);});
                var remove=new Button(()=>{EditScene("移除探针局部加密",x=>{var list=new List<Bounds>(x.ProbeQuality.LocalRefinementVolumes);list.RemoveAt(i);x.ProbeQuality.LocalRefinementVolumes=list.ToArray();});RefreshProbePreview(true);RebuildBody();}) {text=$"移除加密范围 {i+1}"};
                remove.SetEnabled(editable);section.Add(remove);
            }
            var add=new Button(()=>{EditScene("添加探针局部加密",x=>{var list=new List<Bounds>(x.ProbeQuality.LocalRefinementVolumes??Array.Empty<Bounds>());list.Add(new Bounds(SceneView.lastActiveSceneView?.pivot??Vector3.zero,Vector3.one*8));x.ProbeQuality.LocalRefinementVolumes=list.ToArray();});RefreshProbePreview(true);RebuildBody();}) {text="在 Scene 焦点添加 8m 加密范围"};
            add.SetEnabled(editable);section.Add(add);
        }
        private void SelectProbe(int index)
        {
            int count = _probes.PointCount(_probeCached);
            _selectedProbe = index >= 0 && index < count ? index : -1;
            _probeIndex?.SetValueWithoutNotify(_selectedProbe);
            UpdateProbeLabels(); SceneView.RepaintAll();
        }
        private void UpdateProbeLabels()
        {
            if (_probeSummary == null) return;
            if (!_probeEnabled) { _probeSummary.text = "开启后只读预览；不会烘焙或创建场景物件。"; _probeSelected.text = string.Empty; _probeDisplay.text = string.Empty; return; }
            string plan = _probes.HasPlan ? $"计划：{_probes.PlannedGrid.Dimensions}，间距 {_probes.PlannedGrid.Spacing:g}，{_probes.PlannedPointCount} 点；有效性未知。" : "计划不可用：" + _probes.PlanIssue;
            if (_probes.HasPlan && _probes.PlannedGrid.Count > 120000) plan += " 超出构建上限 120000；仅显示范围。";
            if (_probes.HasPlan && !string.IsNullOrEmpty(_probes.PlanIssue)) plan += " " + _probes.PlanIssue;
            string cache = _probes.HasCache ? $"缓存：{_probes.Payload.Probes.Length} 点，有效 {_probes.ValidCount}／无效 {_probes.Payload.Probes.Length - _probes.ValidCount}；" +
                (_probes.CacheCurrent ? "源和间距匹配。" : "已过期／源不可核对：" + _probes.CacheIssue) : "缓存不可用：" + _probes.CacheIssue;
            _probeSummary.text = plan + "\n" + cache + "\n" + _probes.CacheDiagnostics + "\n橙框：采集几何；青／绿框：计划／缓存网格。显示上限会均匀抽取筛选结果；周期核对约 2 秒，内存网格编辑后请手动刷新。";
            int count = _probes.PointCount(_probeCached);
            if (_selectedProbe < 0 || _selectedProbe >= count) { _probeSelected.text = "点击点或输入 ID 查看；筛选外的选中点仍单独标黄。"; return; }
            Vector3 position = _probes.Position(_probeCached, _selectedProbe);
            if (!_probeCached) { _probeSelected.text = $"计划点 #{_selectedProbe}：{position.ToString("F3")}；有效性未知，尚无方向距离数据。"; return; }
            var payload = _probes.Payload;
            Vector2 moment = payload.ProbeVisibility[_selectedProbe * 96 + _visibilityBin];
            float variance = Mathf.Max(0, moment.y - moment.x * moment.x), min = float.PositiveInfinity, max = 0;
            for (int i = 0; i < 96; i++) { float d = payload.ProbeVisibility[_selectedProbe * 96 + i].x; min = Mathf.Min(min, d); max = Mathf.Max(max, d); }
            _probeSelected.text = $"缓存点 #{_selectedProbe}：{position.ToString("F3")}，valid={payload.Probes[_selectedProbe].PositionValid.w:g}（构建启发式标记）。\n" +
                $"96 分箱均值范围 {min:F3}–{max:F3}；bin {_visibilityBin} 代表方向 {MxSceneLightingProbePreview.VisibilityDirection(_visibilityBin).ToString("F3")}，距离均值 {moment.x:F3}，二阶矩 {moment.y:F3}，标准差 {Mathf.Sqrt(variance):F3}。\n" +
                $"传输链接 {payload.ProbeRows[_selectedProbe].Count}；不是逐射线距离或运行时 RGB。方向线显示长度限制为两格。";
            _probeSelected.text += "\n" + _probes.QualityDescription(_selectedProbe);
        }

        private void DrawProbePreview(SceneView view)
        {
            if (!_probeAttached || !_probeEnabled || EditorApplication.isPlayingOrWillChangePlaymode || _owner == null || _owner.gameObject.scene != SceneManager.GetActiveScene()) return;
            Color color = Handles.color; CompareFunction depth = Handles.zTest;
            try
            {
                Handles.zTest = CompareFunction.Always;
                if (_probes.HasPlan)
                {
                    Handles.color = new Color(1, .6f, .15f, .8f); Handles.DrawWireCube(_probes.PlannedGrid.CaptureBounds.center, _probes.PlannedGrid.CaptureBounds.size);
                    Handles.color = Color.cyan; Handles.DrawWireCube(_probes.PlannedGrid.GridBounds.center, _probes.PlannedGrid.GridBounds.size);
                }
                if (_probes.HasPlan && _owner.SceneRendering.ProbeQuality?.Enabled == true)
                    foreach(var volume in _owner.SceneRendering.ProbeQuality.LocalRefinementVolumes ?? Array.Empty<Bounds>())
                    { Handles.color=new Color(.7f,.3f,1,.8f);Handles.DrawWireCube(volume.center,volume.size); }
                if (_probes.HasCache)
                {
                    Handles.color = Color.green; Handles.DrawWireCube(_probes.CachedGrid.GridBounds.center, _probes.CachedGrid.GridBounds.size);
                    if (!_probes.CacheCurrent) Handles.Label(_probes.CachedGrid.Origin, "已过期／源不可核对的缓存");
                }
                DrawAutomaticProbeCells();
                int matching = _probes.CollectVisible(_probeCached, _probeSlice, _probeHeight, _probeHalfThickness,
                    _probeLocal, view.pivot, _probeRadius, _probeValidity, _probeLimit, _visibleProbes);
                if (_probeDisplay != null) _probeDisplay.text = _probes.FilterDiagnostics;
                foreach (int id in _visibleProbes)
                {
                    Vector3 p = _probes.Position(_probeCached, id);
                    Handles.color = id == _selectedProbe ? Color.yellow : !_probeCached ? Color.cyan : _probes.IsValid(id) ? Color.green : Color.red;
                    float size = HandleUtility.GetHandleSize(p) * .035f;
                    if (!_rangeEditing && Handles.Button(p, Quaternion.identity, size, size * 1.5f, Handles.SphereHandleCap)) SelectProbe(id);
                    if (_rangeEditing && Event.current.type==EventType.Repaint) Handles.SphereHandleCap(0,p,Quaternion.identity,size,EventType.Repaint);
                }
                if (_selectedProbe >= 0 && _selectedProbe < _probes.PointCount(_probeCached))
                {
                    Vector3 p = _probes.Position(_probeCached, _selectedProbe);
                    Handles.color = Color.yellow; Handles.DrawWireDisc(p, view.camera.transform.forward, HandleUtility.GetHandleSize(p) * .08f);
                    Handles.Label(p, "Probe #" + _selectedProbe + (_probeCached && !_probes.CacheCurrent ? " [过期缓存]" : ""));
                    if (_probeCached)
                    {
                        float distance = _probes.Payload.ProbeVisibility[_selectedProbe * 96 + _visibilityBin].x;
                        Handles.DrawLine(p, p + MxSceneLightingProbePreview.VisibilityDirection(_visibilityBin) * Mathf.Min(distance, _probes.CachedGrid.Spacing * 2));
                    }
                }
                DrawProbeRangeHandle();
            }
            finally { Handles.color = color; Handles.zTest = depth; }
        }
    }
}

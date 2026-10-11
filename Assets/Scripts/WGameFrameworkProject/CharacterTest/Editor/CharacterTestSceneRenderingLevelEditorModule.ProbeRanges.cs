using System;
using System.Collections.Generic;
using MxFramework.Rendering;
using MxFramework.Rendering.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestSceneRenderingLevelEditorModule
    {
        private enum ProbeRangeKind { Coverage, Manual, Exclusion, Activity }
        private readonly BoxBoundsHandle _probeBoundsHandle = new BoxBoundsHandle();
        private bool _rangeEditing, _rangeChanged, _showAutomaticCells = true;
        private ProbeRangeKind _rangeKind;
        private int _rangeIndex;
        private Vector3Field _rangeCenter, _rangeSize;

        private void BuildAutomaticProbeControls(VisualElement section, MxSceneRenderingConfiguration configuration, bool editable)
        {
            var quality = configuration.ProbeQuality;
            var manual = new Toggle("采用人工加密覆盖（关闭时保留范围定义）") {value=quality.UseManualRefinement,name="sceneLightingUseManual"};
            manual.SetEnabled(editable);section.Add(manual);
            manual.RegisterValueChangedCallback(evt=>{EditScene("调整人工探针加密",x=>x.ProbeQuality.UseManualRefinement=evt.newValue);RefreshProbePreview(true);});
            var automatic = new Toggle("自动选择加密格（无需逐区画框）") { value = quality.AutomaticRefinement, name = "sceneLightingAutomatic" };
            automatic.SetEnabled(editable); section.Add(automatic);
            automatic.RegisterValueChangedCallback(evt => { EditScene("调整探针自动布局", x => x.ProbeQuality.AutomaticRefinement=evt.newValue); RefreshProbePreview(true); RebuildBody(); });
            if (quality.AutomaticRefinement)
            {
                var fraction = new FloatField("自动使用剩余预算比例") { value=quality.AutomaticBudgetFraction, isDelayed=true, name="sceneLightingAutomaticBudget" };
                fraction.SetEnabled(editable); section.Add(fraction);
                fraction.RegisterValueChangedCallback(evt => { if (!Finite(evt.newValue)) return; EditScene("调整自动探针预算", x=>x.ProbeQuality.AutomaticBudgetFraction=Mathf.Clamp01(evt.newValue)); RefreshProbePreview(true); fraction.SetValueWithoutNotify(quality.AutomaticBudgetFraction); });
                var threshold = new FloatField("自动加密最低评分") { value=quality.MinimumAutomaticScore, isDelayed=true, name="sceneLightingAutomaticThreshold" };
                threshold.SetEnabled(editable); section.Add(threshold);
                threshold.RegisterValueChangedCallback(evt => { if (!Finite(evt.newValue)) return; EditScene("调整自动探针评分", x=>x.ProbeQuality.MinimumAutomaticScore=Mathf.Max(.01f,evt.newValue)); RefreshProbePreview(true); threshold.SetValueWithoutNotify(quality.MinimumAutomaticScore); });
                var receivers = new Toggle("按静态接收面和遮挡变化分配") {value=quality.AutomaticReceivers}; receivers.SetEnabled(editable); section.Add(receivers);
                receivers.RegisterValueChangedCallback(evt=>{EditScene("调整探针接收需求",x=>x.ProbeQuality.AutomaticReceivers=evt.newValue);RefreshProbePreview(true);});
                var navigation = new Toggle("按已烘焙 NavMesh 活动范围分配") {value=quality.AutomaticNavigation}; navigation.SetEnabled(editable); section.Add(navigation);
                navigation.RegisterValueChangedCallback(evt=>{EditScene("调整探针活动需求",x=>x.ProbeQuality.AutomaticNavigation=evt.newValue);RefreshProbePreview(true);});
                section.Add(new HelpBox("自动评分使用世界几何、已烘焙导航、活动范围及雾/尘埃需求；不读取当前相机或亮度。人工加密优先，排除范围优先级最高且只排除加密。预算不足会保留较粗的完整基础网格并给出提示。修改后须主动重建，自动结果框为只读。",HelpBoxMessageType.Info));
            }
            BuildDemandVolumes(section,configuration,ProbeRangeKind.Exclusion,"排除加密",editable);
            BuildDemandVolumes(section,configuration,ProbeRangeKind.Activity,"活动需求",editable);
            var show = new Toggle("显示自动选择的格子（橙色，只读）") {value=_showAutomaticCells}; section.Add(show);
            show.RegisterValueChangedCallback(evt=>{_showAutomaticCells=evt.newValue;SceneView.RepaintAll();});
        }

        private static Bounds[] ProbeVolumes(MxSceneRenderingConfiguration configuration, ProbeRangeKind kind)
        {
            switch(kind)
            {
                case ProbeRangeKind.Manual: return configuration.ProbeQuality.LocalRefinementVolumes??Array.Empty<Bounds>();
                case ProbeRangeKind.Exclusion: return configuration.ProbeQuality.RefinementExclusionVolumes??Array.Empty<Bounds>();
                case ProbeRangeKind.Activity: return configuration.ProbeQuality.ActivityVolumes??Array.Empty<Bounds>();
                default: return Array.Empty<Bounds>();
            }
        }
        private static void SetProbeVolumes(MxSceneRenderingConfiguration configuration, ProbeRangeKind kind, Bounds[] volumes)
        {
            if(kind==ProbeRangeKind.Exclusion) configuration.ProbeQuality.RefinementExclusionVolumes=volumes;
            else if(kind==ProbeRangeKind.Activity) configuration.ProbeQuality.ActivityVolumes=volumes;
            else configuration.ProbeQuality.LocalRefinementVolumes=volumes;
        }
        private void BuildDemandVolumes(VisualElement section,MxSceneRenderingConfiguration configuration,ProbeRangeKind kind,string label,bool editable)
        {
            var volumes=ProbeVolumes(configuration,kind);
            for(int index=0;index<volumes.Length;index++)
            {
                int id=index;
                var center=new Vector3Field($"{label} {id+1} 中心") {value=volumes[id].center};
                var size=new Vector3Field($"{label} {id+1} 尺寸") {value=volumes[id].size};
                center.SetEnabled(editable);size.SetEnabled(editable);section.Add(center);section.Add(size);
                center.RegisterValueChangedCallback(evt=>{if(!Finite(evt.newValue.x)||!Finite(evt.newValue.y)||!Finite(evt.newValue.z))return;EditScene("调整探针需求中心",x=>{var list=ProbeVolumes(x,kind);list[id]=new Bounds(evt.newValue,list[id].size);});RefreshProbePreview(true);});
                size.RegisterValueChangedCallback(evt=>{Vector3 v=evt.newValue;if(!ValidRangeSize(v))return;EditScene("调整探针需求尺寸",x=>{var list=ProbeVolumes(x,kind);list[id]=new Bounds(list[id].center,v);});RefreshProbePreview(true);});
                var remove=new Button(()=>{EditScene("移除探针需求范围",x=>{var list=new List<Bounds>(ProbeVolumes(x,kind));list.RemoveAt(id);SetProbeVolumes(x,kind,list.ToArray());});RefreshProbePreview(true);RebuildBody();}) {text=$"移除{label} {id+1}"};
                remove.SetEnabled(editable);section.Add(remove);
            }
            var add=new Button(()=>{EditScene("添加探针需求范围",x=>{var list=new List<Bounds>(ProbeVolumes(x,kind));list.Add(new Bounds(SceneView.lastActiveSceneView?.pivot??Vector3.zero,Vector3.one*8));SetProbeVolumes(x,kind,list.ToArray());});RefreshProbePreview(true);RebuildBody();}) {text=$"在 Scene 焦点添加{label}范围"};
            add.SetEnabled(editable);section.Add(add);
        }

        private void BuildProbeRangeControls(VisualElement section,MxSceneRenderingConfiguration configuration,bool readOnly)
        {
            var labels=new List<string>();var kinds=new List<ProbeRangeKind>();var indices=new List<int>();
            if(configuration.UseProbeCoverageBounds) {labels.Add("总覆盖（白色）");kinds.Add(ProbeRangeKind.Coverage);indices.Add(0);}
            if(configuration.ProbeQuality?.Enabled==true) foreach(ProbeRangeKind kind in new[]{ProbeRangeKind.Manual,ProbeRangeKind.Exclusion,ProbeRangeKind.Activity})
            {
                var volumes=ProbeVolumes(configuration,kind);
                for(int i=0;i<volumes.Length;i++) {labels.Add((kind==ProbeRangeKind.Manual?"人工加密（紫色）":kind==ProbeRangeKind.Exclusion?"排除加密（红色）":"活动需求（黄色）")+$" {i+1}");kinds.Add(kind);indices.Add(i);}
            }
            if(labels.Count==0)return;
            int selected=0;for(int i=0;i<labels.Count;i++)if(kinds[i]==_rangeKind&&indices[i]==_rangeIndex)selected=i;
            _rangeKind=kinds[selected];_rangeIndex=indices[selected];
            var toggle=new Toggle("在 Scene 拖拽范围（移动/缩放，支持 Undo）") {value=_rangeEditing,name="sceneLightingEditRange"};
            toggle.SetEnabled(!readOnly&&!MxSceneLightingBuilder.IsBuilding);section.Add(toggle);
            toggle.RegisterValueChangedCallback(evt=>{_rangeEditing=evt.newValue;if(_rangeEditing){_probeEnabled=true;RefreshProbePreview(true);}SceneView.RepaintAll();});
            var target=new PopupField<string>("编辑范围",labels,selected) {name="sceneLightingRangeTarget"};
            target.SetEnabled(!readOnly&&!MxSceneLightingBuilder.IsBuilding);section.Add(target);
            target.RegisterValueChangedCallback(evt=>{int index=labels.IndexOf(evt.newValue);_rangeKind=kinds[index];_rangeIndex=indices[index];SyncProbeRangeFields();SceneView.RepaintAll();});
            _rangeCenter=new Vector3Field("选中范围中心"){name="sceneLightingRangeCenter"};
            _rangeSize=new Vector3Field("选中范围尺寸"){name="sceneLightingRangeSize"};
            _rangeCenter.SetEnabled(!readOnly&&!MxSceneLightingBuilder.IsBuilding);_rangeSize.SetEnabled(!readOnly&&!MxSceneLightingBuilder.IsBuilding);
            section.Add(_rangeCenter);section.Add(_rangeSize);SyncProbeRangeFields();
            _rangeCenter.RegisterValueChangedCallback(evt=>{if(TryProbeRange(out var bounds))ApplyProbeRange(new Bounds(evt.newValue,bounds.size));});
            _rangeSize.RegisterValueChangedCallback(evt=>{if(TryProbeRange(out var bounds))ApplyProbeRange(new Bounds(bounds.center,evt.newValue));});
        }
        private bool TryProbeRange(out Bounds bounds)
        {
            bounds=default;var configuration=_owner?.SceneRendering;if(configuration==null)return false;
            if(_rangeKind==ProbeRangeKind.Coverage) {bounds=configuration.ProbeCoverageBounds;return configuration.UseProbeCoverageBounds;}
            if(configuration.ProbeQuality==null)return false;
            var volumes=ProbeVolumes(configuration,_rangeKind);if(_rangeIndex<0||_rangeIndex>=volumes.Length)return false;
            bounds=volumes[_rangeIndex];return true;
        }
        private static bool ValidRangeSize(Vector3 size)=>Finite(size.x)&&Finite(size.y)&&Finite(size.z)&&size.x>0&&size.y>0&&size.z>0;
        private void ApplyProbeRange(Bounds bounds)
        {
            if(!Finite(bounds.center.x)||!Finite(bounds.center.y)||!Finite(bounds.center.z)||!ValidRangeSize(bounds.size)||MxSceneLightingBuilder.IsBuilding){SyncProbeRangeFields();return;}
            if(!TryProbeRange(out var previous)||previous==bounds)return;
            EditScene("移动或缩放探针范围",configuration=>{if(_rangeKind==ProbeRangeKind.Coverage)configuration.ProbeCoverageBounds=bounds;else ProbeVolumes(configuration,_rangeKind)[_rangeIndex]=bounds;});
            _rangeChanged=true;SyncProbeRangeFields();SceneView.RepaintAll();
            if(GUIUtility.hotControl==0){RefreshProbePreview(true);RebuildBody();_rangeChanged=false;}
        }
        private void SyncProbeRangeFields()
        {if(TryProbeRange(out var bounds)){_rangeCenter?.SetValueWithoutNotify(bounds.center);_rangeSize?.SetValueWithoutNotify(bounds.size);}}
        private void DrawProbeRangeHandle()
        {
            if(!_rangeEditing||MxSceneLightingBuilder.IsBuilding||!TryProbeRange(out var bounds))return;
            Handles.color=_rangeKind==ProbeRangeKind.Coverage?Color.white:_rangeKind==ProbeRangeKind.Manual?new Color(.7f,.3f,1):_rangeKind==ProbeRangeKind.Exclusion?Color.red:Color.yellow;
            _probeBoundsHandle.center=bounds.center;_probeBoundsHandle.size=bounds.size;
            EditorGUI.BeginChangeCheck();Vector3 center=Handles.PositionHandle(bounds.center,Quaternion.identity);_probeBoundsHandle.DrawHandle();
            if(EditorGUI.EndChangeCheck())ApplyProbeRange(new Bounds(center==bounds.center?_probeBoundsHandle.center:center,_probeBoundsHandle.size));
            if(_rangeChanged&&Event.current.rawType==EventType.MouseUp){_rangeChanged=false;RefreshProbePreview(true);RebuildBody();}
        }
        private void DrawAutomaticProbeCells()
        {
            var quality=_owner.SceneRendering.ProbeQuality;
            if(quality?.Enabled!=true)return;
            foreach(var bounds in quality.RefinementExclusionVolumes??Array.Empty<Bounds>()){Handles.color=Color.red;Handles.DrawWireCube(bounds.center,bounds.size);}
            foreach(var bounds in quality.ActivityVolumes??Array.Empty<Bounds>()){Handles.color=Color.yellow;Handles.DrawWireCube(bounds.center,bounds.size);}
            var layout=_probes.PlannedLayout;if(!_showAutomaticCells||layout?.CellKinds==null)return;
            var dims=layout.Topology.CellDimensions;int step=Mathf.Max(1,Mathf.CeilToInt(layout.AutomaticBricks/512f)),ordinal=0;
            Handles.color=new Color(1,.5f,.1f,.55f);
            for(int id=0;id<layout.CellKinds.Length;id++)if(layout.CellKinds[id]==2&&ordinal++%step==0)
            {
                Vector3 center=layout.Grid.Origin+new Vector3(id%dims.x+.5f,(id/dims.x)%dims.y+.5f,id/(dims.x*dims.y)+.5f)*layout.Grid.Spacing;
                Handles.DrawWireCube(center,Vector3.one*layout.Grid.Spacing);
            }
        }
    }
}

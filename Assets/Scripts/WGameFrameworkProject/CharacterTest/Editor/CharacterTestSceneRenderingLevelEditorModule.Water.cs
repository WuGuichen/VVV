using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestSceneRenderingLevelEditorModule
    {
        private readonly HashSet<CharacterTestShallowWaterSettings> _editedWater = new HashSet<CharacterTestShallowWaterSettings>();
        private readonly Dictionary<CharacterTestShallowWaterSettings, string> _preparedWater = new Dictionary<CharacterTestShallowWaterSettings, string>();
        private bool IsWaterAssetDirty => _editedWater.Count > 0 ||
            (_owner?.ShallowWater?.Settings != null && EditorUtility.IsDirty(_owner.ShallowWater.Settings));

        private void BuildWaterPanel(bool readOnly)
        {
            var water = Section("浅水"); water.name = "scene-rendering-shallow-water";
            var c = _owner.ShallowWater;
            if (c == null)
            {
                bool legacy = CharacterTestShallowWaterAuthoring.FindLegacy(_owner) != null;
                var create = new Button(() =>
                {
                    try { CharacterTestShallowWaterAuthoring.Configure(_owner); Changed(); RebuildBody(); }
                    catch (Exception e) { _status = e.Message; RebuildBody(); }
                }) { text = legacy ? "迁入现有水面配置" : "添加浅水" };
                create.SetEnabled(!readOnly); water.Add(create); return;
            }
            AddToggle(water, "启用浅水", c.SurfaceEnabled, value => EditWater("切换浅水", x => x.SurfaceEnabled = value));
            AddToggle(water, "镜面倒影", c.ReflectionEnabled, value => EditWater("切换水面倒影", x => x.ReflectionEnabled = value));
            AddToggle(water, "接触与移动涟漪", c.RipplesEnabled, value => EditWater("切换水面涟漪", x => x.RipplesEnabled = value));
            var position = new Vector3Field("水面位置") { value = c.Position, isDelayed = true };
            position.RegisterValueChangedCallback(evt => EditWater("水面位置", x => x.Position = evt.newValue)); water.Add(position);
            AddFloat(water, "平面朝向", c.Yaw, value => EditWater("水面朝向", x => x.Yaw = value));
            AddObject<CharacterTestShallowWaterSettings>(water, "水面参数", c.Settings, value =>
            { EditWater("绑定水面参数", x => x.Settings = value); RebuildBody(); });
            var settings = c.Settings;
            if (settings != null)
            {
                var size = new Vector2Field("覆盖尺寸（米）") { value = settings.Size, isDelayed = true };
                size.RegisterValueChangedCallback(evt => EditWaterSettings("水面范围", x =>
                    x.Size = new Vector2(Mathf.Max(.5f, evt.newValue.x), Mathf.Max(.5f, evt.newValue.y)))); water.Add(size);
                var appearance = CreateFoldout("水面外观", "water-appearance", true); water.Add(appearance);
                AddColor(appearance, "水色", settings.Tint, value => EditWaterSettings("水色", x => x.Tint = value));
                AddFloat(appearance, "水色浓度", settings.TintOpacity, value => EditWaterSettings("水色浓度", x => x.TintOpacity = Mathf.Clamp(value, 0, .3f)));
                AddFloat(appearance, "基础反射率", settings.BaseReflectivity, value => EditWaterSettings("基础反射率", x => x.BaseReflectivity = Mathf.Clamp(value, 0, .15f)));
                AddFloat(appearance, "背景水纹", settings.BackgroundSlope, value => EditWaterSettings("背景水纹", x => x.BackgroundSlope = Mathf.Clamp(value, 0, .05f)));
                AddFloat(appearance, "折射强度", settings.RefractionStrength, value => EditWaterSettings("折射强度", x => x.RefractionStrength = Mathf.Clamp(value, 0, .04f)));
                AddFloat(appearance, "接触柔化", settings.ContactSoftness, value => EditWaterSettings("接触柔化", x => x.ContactSoftness = Mathf.Max(.001f, value)));
                AddFloat(appearance, "最大水深", settings.MaximumWaterDepth, value => EditWaterSettings("最大水深", x => x.MaximumWaterDepth = Mathf.Clamp(value, .04f, .3f)));
                AddFloat(appearance, "边缘过渡", settings.EdgeSoftness, value => EditWaterSettings("边缘过渡", x => x.EdgeSoftness = Mathf.Max(.01f, value)));
                AddFloat(appearance, "高光强度", settings.HighlightStrength, value => EditWaterSettings("高光强度", x => x.HighlightStrength = Mathf.Clamp(value, 0, .15f)));
                var reflection = CreateFoldout("倒影", "water-reflection", true); water.Add(reflection);
                AddFloat(reflection, "倒影强度", settings.ReflectionStrength, value => EditWaterSettings("倒影强度", x => x.ReflectionStrength = Mathf.Clamp01(value)));
                AddFloat(reflection, "倒影可见下限", settings.MinimumReflection, value => EditWaterSettings("倒影可见下限", x => x.MinimumReflection = Mathf.Clamp(value, 0, .75f)));
                AddFloat(reflection, "倒影扰动", settings.ReflectionDistortion, value => EditWaterSettings("倒影扰动", x => x.ReflectionDistortion = Mathf.Clamp(value, 0, .08f)));
                AddFloat(reflection, "分辨率比例", settings.ReflectionScale, value => EditWaterSettings("分辨率比例", x => x.ReflectionScale = Mathf.Clamp(value, .1f, 1)));
                AddFloat(reflection, "最长边像素", settings.ReflectionMaximumEdge, value => EditWaterSettings("最长边像素", x => x.ReflectionMaximumEdge = Mathf.Clamp(Mathf.RoundToInt(value), 128, 2048)));
                AddFloat(reflection, "倒影距离（米）", settings.ReflectionDistance, value => EditWaterSettings("倒影距离（米）", x => x.ReflectionDistance = Mathf.Max(5, value)));
                var ripples = CreateFoldout("涟漪", "water-ripples", true); water.Add(ripples);
                AddFloat(ripples, "涟漪亮度", settings.RippleSheen, value => EditWaterSettings("涟漪亮度", x => x.RippleSheen = Mathf.Clamp(value, 0, .15f)));
                AddFloat(ripples, "波环宽度", settings.RippleWidth, value => EditWaterSettings("波环宽度", x => x.RippleWidth = Mathf.Clamp(value, .1f, .6f)));
                AddFloat(ripples, "持续时间（秒）", settings.RippleLifetime, value => EditWaterSettings("持续时间（秒）", x => x.RippleLifetime = Mathf.Clamp(value, .1f, 3)));
                AddFloat(ripples, "扩散速度", settings.RippleSpeed, value => EditWaterSettings("扩散速度", x => x.RippleSpeed = Mathf.Clamp(value, .2f, 4)));
                AddFloat(ripples, "接触振幅", settings.FootstepAmplitude, value => EditWaterSettings("接触振幅", x => x.FootstepAmplitude = Mathf.Clamp(value, 0, .06f)));
                AddFloat(ripples, "落地振幅", settings.LandingAmplitude, value => EditWaterSettings("落地振幅", x => x.LandingAmplitude = Mathf.Clamp(value, 0, .08f)));
                AddFloat(ripples, "移动波纹间距", settings.WakeSpacing, value => EditWaterSettings("移动波纹间距", x => x.WakeSpacing = Mathf.Max(.1f, value)));
                var advanced = CreateFoldout("水面高级绑定", "water-bindings", false); water.Add(advanced);
                AddObject<Shader>(advanced, "水面 Shader", settings.SurfaceShader, value => EditWaterSettings("水面 Shader", x => x.SurfaceShader = value));
                var layers = new UnityEditor.UIElements.LayerMaskField("倒影层") { value = settings.ReflectionLayers.value };
                layers.RegisterValueChangedCallback(evt => EditWaterSettings("倒影层", x => x.ReflectionLayers = evt.newValue)); advanced.Add(layers);
                advanced.Add(new Label("参数资产可复用，修改会影响引用它的场景。"));
            }
            water.SetEnabled(!readOnly);
            var preview = _owner.ShallowWaterSession;
            if (preview != null && !string.IsNullOrEmpty(preview.Issue)) water.Add(new HelpBox(preview.Issue, HelpBoxMessageType.Warning));
        }

        private void EditWater(string label, Action<CharacterTestShallowWaterConfiguration> edit)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || _owner?.ShallowWater == null) return;
            Undo.RecordObject(_owner, label); edit(_owner.ShallowWater);
            _owner.SynchronizeShallowWater()?.PrepareView(); Changed();
        }
        private void EditWaterSettings(string label, Action<CharacterTestShallowWaterSettings> edit)
        {
            var settings = _owner?.ShallowWater?.Settings;
            if (EditorApplication.isPlayingOrWillChangePlaymode || settings == null) return;
            Undo.RecordObject(settings, label); edit(settings); EditorUtility.SetDirty(settings);
            _editedWater.Add(settings); Changed();
        }
        private void CollectWaterSavePaths(List<string> paths)
        {
            foreach (var settings in _editedWater) AddWaterSavePath(settings, paths);
            var current = _owner?.ShallowWater?.Settings;
            if (current != null && EditorUtility.IsDirty(current)) AddWaterSavePath(current, paths);
        }
        private static void AddWaterSavePath(CharacterTestShallowWaterSettings settings, List<string> paths)
        {
            if (settings == null) return;
            string path = AssetDatabase.GetAssetPath(settings);
            if (!string.IsNullOrEmpty(path)) paths.Add(path);
        }
        private void PrepareWaterSave()
        {
            _preparedWater.Clear();
            foreach (var settings in _editedWater)
                if (settings != null) _preparedWater[settings] = JsonUtility.ToJson(settings);
            var current = _owner?.ShallowWater?.Settings;
            if (current != null && EditorUtility.IsDirty(current)) _preparedWater[current] = JsonUtility.ToJson(current);
        }
        private void SavePreparedWater()
        {
            foreach (var settings in _preparedWater.Keys)
                if (settings != null) AssetDatabase.SaveAssetIfDirty(settings);
        }
        private void CompleteWaterSave(bool succeeded)
        {
            if (succeeded) _editedWater.Clear();
            else foreach (var entry in _preparedWater)
                if (entry.Key != null)
                { JsonUtility.FromJsonOverwrite(entry.Value, entry.Key); EditorUtility.SetDirty(entry.Key); _editedWater.Add(entry.Key); }
            _preparedWater.Clear();
        }
    }
}

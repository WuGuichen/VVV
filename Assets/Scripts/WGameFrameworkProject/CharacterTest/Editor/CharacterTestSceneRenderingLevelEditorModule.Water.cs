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
            AddToggle(water, "反光与倒影", c.ReflectionEnabled, value => EditWater("切换水面倒影", x => x.ReflectionEnabled = value));
            AddToggle(water, "接触与移动涟漪", c.RipplesEnabled, value => EditWater("切换水面涟漪", x => x.RipplesEnabled = value));
            AddToggle(water, "脚踩水花", c.SplashesEnabled, value => EditWater("切换脚踩水花", x => x.SplashesEnabled = value));
            var position = new Vector3Field("水面位置") { value = c.Position, isDelayed = true };
            position.RegisterValueChangedCallback(evt => EditWater("水面位置", x => x.Position = evt.newValue)); water.Add(position);
            AddFloat(water, "平面朝向", c.Yaw, value => EditWater("水面朝向", x => x.Yaw = value));
            AddObject<CharacterTestShallowWaterSettings>(water, "水面参数", c.Settings, value =>
            { EditWater("绑定水面参数", x => x.Settings = value); RebuildBody(); });
            var settings = c.Settings;
            if (settings != null)
            {
                var quality = new DropdownField("性能档位", new List<string> { "自定义", "低（SSPR）", "中（SSPR）", "镜像反射", "高（SSPR）" }, (int)settings.QualityTier);
                quality.name = "water-quality-tier";
                quality.RegisterValueChangedCallback(evt =>
                {
                    EditWaterSettings("水面性能档位", x => x.QualityTier = (CharacterTestWaterQuality)quality.index);
                    _owner.SynchronizeShallowWater()?.PrepareView(); RebuildBody();
                });
                water.Add(quality);
                water.Add(new HelpBox(settings.QualityTier == CharacterTestWaterQuality.Performance
                    ? "性能优先：SSPR 动态倒影，最长边 256，无折射。涟漪使用独立档位。"
                    : settings.QualityTier == CharacterTestWaterQuality.Balanced
                    ? "均衡：SSPR 动态倒影，最长边 512，增强补洞，无折射。涟漪使用独立档位。"
                    : settings.QualityTier == CharacterTestWaterQuality.Quality
                    ? "质量优先：倒影 1/2 分辨率、最长边 1024、逐帧刷新，保留折射。涟漪使用独立档位。"
                    : settings.QualityTier == CharacterTestWaterQuality.SsprHigh
                    ? "高档 SSPR：最长边 1024，深度加权重建，轻度颜色柔化。涟漪使用独立档位。"
                    : "自定义：使用下方预算。刷新率 0 表示逐帧；切换预设不会覆盖自定义值。", HelpBoxMessageType.Info));
                var budget = CreateFoldout("自定义性能预算", "water-budget", settings.QualityTier == CharacterTestWaterQuality.Custom);
                water.Add(budget); budget.SetEnabled(settings.QualityTier == CharacterTestWaterQuality.Custom);
                var reflectionMode = new DropdownField("反射算法", new List<string> { "镜像相机", "SSPR", "仅环境反光" }, (int)settings.CustomReflection);
                reflectionMode.RegisterValueChangedCallback(evt => EditWaterSettings("水面反射算法", x => x.CustomReflection = (CharacterTestWaterReflection)reflectionMode.index)); budget.Add(reflectionMode);
                AddFloat(budget, "SSPR 补洞半径", settings.SsprHoleRadius, value => EditWaterSettings("SSPR 补洞", x => x.SsprHoleRadius = Mathf.Clamp(Mathf.RoundToInt(value), 0, 2)));
                AddToggle(budget, "启用折射", settings.RefractionEnabled, value => EditWaterSettings("水面折射", x => x.RefractionEnabled = value));
                AddToggle(budget, "倒影相机阴影", settings.ReflectionShadows, value => EditWaterSettings("倒影阴影", x => x.ReflectionShadows = value));
                AddFloat(budget, "镜像相机刷新率（0 为逐帧）", settings.ReflectionFramesPerSecond, value => EditWaterSettings("倒影刷新率", x => x.ReflectionFramesPerSecond = Mathf.Clamp(Mathf.RoundToInt(value), 0, 60)));
                AddFloat(budget, "分辨率比例", settings.ReflectionScale, value => EditWaterSettings("分辨率比例", x => x.ReflectionScale = Mathf.Clamp(value, .1f, 1)));
                AddFloat(budget, "最长边像素", settings.ReflectionMaximumEdge, value => EditWaterSettings("最长边像素", x => x.ReflectionMaximumEdge = Mathf.Clamp(Mathf.RoundToInt(value), 128, 2048)));
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
                AddFloat(reflection, "倒影距离（米）", settings.ReflectionDistance, value => EditWaterSettings("倒影距离（米）", x => x.ReflectionDistance = Mathf.Max(5, value)));
                var ripples = CreateFoldout("涟漪", "water-ripples", true); water.Add(ripples);
                var rippleQuality = new DropdownField("交互涟漪质量", new List<string> { "自定义", "关闭", "低", "中", "高" }, (int)settings.RippleQuality);
                rippleQuality.RegisterValueChangedCallback(evt => { EditWaterSettings("交互涟漪质量", x => x.RippleQuality = (CharacterTestRippleQuality)rippleQuality.index); RebuildBody(); }); ripples.Add(rippleQuality);
                ripples.Add(new Label("低：256 / 24 米 / 30 Hz；中：512 / 32 米 / 30 Hz；高：1024 / 48 米 / 60 Hz。新接触优先刷新。"));
                var rippleBudget = CreateFoldout("涟漪自定义预算", "water-ripple-budget", false); ripples.Add(rippleBudget);
                rippleBudget.SetEnabled(settings.RippleQuality == CharacterTestRippleQuality.Custom);
                AddFloat(rippleBudget, "纹理边长", settings.RippleFieldResolution, value => EditWaterSettings("涟漪纹理", x => x.RippleFieldResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(value)), 128, 1024)));
                AddFloat(rippleBudget, "覆盖边长（米）", settings.RippleFieldExtent, value => EditWaterSettings("涟漪范围", x => x.RippleFieldExtent = Mathf.Clamp(value, 8, 64)));
                AddFloat(rippleBudget, "刷新率", settings.RippleFieldRate, value => EditWaterSettings("涟漪刷新率", x => x.RippleFieldRate = Mathf.Clamp(Mathf.RoundToInt(value), 15, 60)));
                AddFloat(ripples, "内部波包安全上限", settings.MaximumRipples, value => EditWaterSettings("波包安全上限", x => x.MaximumRipples = Mathf.Clamp(Mathf.RoundToInt(value), 0, 32)));
                AddFloat(ripples, "涟漪亮度", settings.RippleSheen, value => EditWaterSettings("涟漪亮度", x => x.RippleSheen = Mathf.Clamp(value, 0, .15f)));
                AddFloat(ripples, "波环宽度", settings.RippleWidth, value => EditWaterSettings("波环宽度", x => x.RippleWidth = Mathf.Clamp(value, .1f, .6f)));
                AddFloat(ripples, "持续时间（秒）", settings.RippleLifetime, value => EditWaterSettings("持续时间（秒）", x => x.RippleLifetime = Mathf.Clamp(value, .1f, 3)));
                AddFloat(ripples, "扩散速度", settings.RippleSpeed, value => EditWaterSettings("扩散速度", x => x.RippleSpeed = Mathf.Clamp(value, .2f, 4)));
                AddFloat(ripples, "接触振幅", settings.FootstepAmplitude, value => EditWaterSettings("接触振幅", x => x.FootstepAmplitude = Mathf.Clamp(value, 0, .06f)));
                AddFloat(ripples, "落地振幅", settings.LandingAmplitude, value => EditWaterSettings("落地振幅", x => x.LandingAmplitude = Mathf.Clamp(value, 0, .08f)));
                AddFloat(ripples, "移动波纹间距", settings.WakeSpacing, value => EditWaterSettings("移动波纹间距", x => x.WakeSpacing = Mathf.Max(.1f, value)));
                var splashes = CreateFoldout("脚踩水花", "water-splashes", true); water.Add(splashes);
                AddObject<GameObject>(splashes, "水花资源", settings.SplashPrefab, value => EditWaterSettings("水花资源", x => x.SplashPrefab = value));
                if (settings.SplashPrefab == null)
                {
                    splashes.Add(new Button(() =>
                    {
                        try
                        {
                            var prefab = CharacterTestShallowWaterSplashAssetBuilder.EnsurePrefab();
                            EditWaterSettings("绑定水花资源", x => x.SplashPrefab = prefab); RebuildBody();
                        }
                        catch (Exception e) { _status = e.Message; RebuildBody(); }
                    }) { text = "准备水花资源" });
                }
                AddFloat(splashes, "水花大小", settings.SplashScale, value => EditWaterSettings("水花大小", x => x.SplashScale = Mathf.Clamp(value, .2f, 1.5f)));
                AddFloat(splashes, "落地增强", settings.SplashLandingMultiplier, value => EditWaterSettings("落地水花", x => x.SplashLandingMultiplier = Mathf.Clamp(value, 1, 2)));
                AddFloat(splashes, "最小浸水深度（米）", settings.SplashMinimumDepth, value => EditWaterSettings("水花浸水深度", x => x.SplashMinimumDepth = Mathf.Clamp(value, .001f, .03f)));
                AddFloat(splashes, "最小移动速度（米/秒）", settings.SplashMinimumSpeed, value => EditWaterSettings("水花触发速度", x => x.SplashMinimumSpeed = Mathf.Clamp(value, .1f, 2)));
                AddFloat(splashes, "每脚冷却（秒）", settings.SplashCooldown, value => EditWaterSettings("水花冷却", x => x.SplashCooldown = Mathf.Clamp(value, .08f, 1)));
                AddFloat(splashes, "发射距离（米）", settings.SplashDistance, value => EditWaterSettings("水花距离", x => x.SplashDistance = Mathf.Clamp(value, 5, 60)));
                AddFloat(splashes, "同时水花上限", settings.SplashMaximumActive, value => EditWaterSettings("水花池上限", x => x.SplashMaximumActive = Mathf.Clamp(Mathf.RoundToInt(value), 1, 16)));
                AddFloat(splashes, "每秒发射上限", settings.SplashBurstsPerSecond, value => EditWaterSettings("水花发射预算", x => x.SplashBurstsPerSecond = Mathf.Clamp(Mathf.RoundToInt(value), 1, 32)));
                AddFloat(splashes, "单步水滴数量", settings.SplashStepParticles, value => EditWaterSettings("水滴数量", x => x.SplashStepParticles = Mathf.Clamp(Mathf.RoundToInt(value), 4, 16)));
                splashes.Add(new Label("运行时由真实足部接触触发；干地、静止及超出发射预算时跳过。"));
                var advanced = CreateFoldout("水面高级绑定", "water-bindings", false); water.Add(advanced);
                AddObject<Shader>(advanced, "水面 Shader", settings.SurfaceShader, value => EditWaterSettings("水面 Shader", x => x.SurfaceShader = value));
                AddObject<ComputeShader>(advanced, "SSPR 计算资源", settings.SsprCompute, value => EditWaterSettings("SSPR 计算资源", x => x.SsprCompute = value));
                AddObject<ComputeShader>(advanced, "涟漪计算资源", settings.RippleCompute, value => EditWaterSettings("涟漪计算资源", x => x.RippleCompute = value));
                var layers = new UnityEditor.UIElements.LayerMaskField("镜像相机倒影层") { value = settings.ReflectionLayers.value };
                layers.RegisterValueChangedCallback(evt => EditWaterSettings("倒影层", x => x.ReflectionLayers = evt.newValue)); advanced.Add(layers);
                advanced.Add(new Label("参数资产可复用，修改会影响引用它的场景。"));
            }
            water.SetEnabled(!readOnly);
            var preview = _owner.ShallowWaterSession;
            if (preview != null && !string.IsNullOrEmpty(preview.Issue)) water.Add(new HelpBox(preview.Issue, HelpBoxMessageType.Warning));
            if (preview != null && !string.IsNullOrEmpty(preview.SplashIssue)) water.Add(new HelpBox(preview.SplashIssue, HelpBoxMessageType.Warning));
            if (preview != null && !string.IsNullOrEmpty(preview.SsprIssue)) water.Add(new HelpBox(preview.SsprIssue, HelpBoxMessageType.Warning));
            if (preview != null && !string.IsNullOrEmpty(preview.RippleFieldIssue)) water.Add(new HelpBox(preview.RippleFieldIssue, HelpBoxMessageType.Warning));
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

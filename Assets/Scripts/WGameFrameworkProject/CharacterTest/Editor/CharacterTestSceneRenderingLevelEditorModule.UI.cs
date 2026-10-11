using System;
using MxFramework.Editor.Levels;
using MxFramework.Rendering;
using MxFramework.Rendering.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestSceneRenderingLevelEditorModule
    {
        private VisualElement _body;
        private readonly System.Collections.Generic.Dictionary<string, bool> _foldoutStates =
            new System.Collections.Generic.Dictionary<string, bool>();
        private Vector2 _bodyScrollOffset;
        private IVisualElementScheduledItem _restoreBodyScroll;

        public void BuildModuleToolbar(MxLevelEditorWindow window, VisualElement toolbar) { _window = window; }

        public VisualElement CreateContent(MxLevelEditorWindow window)
        {
            _window = window;
            OnEnable();
            var scroll = _body as ScrollView;
            if (scroll == null)
            {
                scroll = new ScrollView { name = "scene-rendering-workspace" };
                _body = scroll;
                scroll.RegisterCallback<AttachToPanelEvent>(_ => AttachProbePreview());
                scroll.RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    if (_restoreBodyScroll == null) _bodyScrollOffset = scroll.scrollOffset;
                    _restoreBodyScroll?.Pause();
                    DetachProbePreview();
                });
            }
            RebuildBody();
            return scroll;
        }

        private void RebuildBody()
        {
            if (_body == null) return;
            var scroll = _body as ScrollView;
            if (scroll != null && scroll.panel != null && _restoreBodyScroll == null)
                _bodyScrollOffset = scroll.scrollOffset;
            _restoreBodyScroll?.Pause();
            PopulateBody();
            if (scroll != null)
            {
                // RefreshChrome detaches/recreates module content after parameter edits.
                // Restore after layout, when the rebuilt content has a valid scroll range.
                _restoreBodyScroll = scroll.schedule.Execute(() =>
                {
                    scroll.scrollOffset = _bodyScrollOffset;
                    _restoreBodyScroll = null;
                });
            }
        }

        private void PopulateBody()
        {
            _body.Clear();
            if (_owner == null)
            {
                _body.Add(new HelpBox("当前活动场景需要唯一的 GameManager；请检查场景层级。", HelpBoxMessageType.Warning));
                return;
            }
            var c = _owner.SceneRendering;
            bool readOnly = EditorApplication.isPlaying;
            if (IsUnconfigured(c))
            {
                _body.Add(new HelpBox("渲染配置由当前 GameManager 保存。", HelpBoxMessageType.Info));
                int oldLights = 0, oldFogs = 0;
                foreach (var legacy in UnityEngine.Object.FindObjectsByType<MxSceneLightingScene>(FindObjectsInactive.Include))
                    if (legacy.gameObject.scene == _owner.gameObject.scene) oldLights++;
                foreach (var legacy in UnityEngine.Object.FindObjectsByType<MxHeightFogScene>(FindObjectsInactive.Include))
                    if (legacy.gameObject.scene == _owner.gameObject.scene) oldFogs++;
                if (oldLights == 1 && oldFogs == 1)
                {
                    var migrate = new Button(() => CharacterTestSceneRenderingMigration.Migrate(_owner, this))
                        { text = "导入当前光照与雾配置" };
                    migrate.SetEnabled(!readOnly);
                    _body.Add(migrate);
                }
                else
                {
                    var create = new Button(CreateAndScan) { text = "创建并自动扫描场景渲染配置" };
                    create.SetEnabled(!readOnly);
                    _body.Add(create);
                }
                return;
            }
            if (readOnly) _body.Add(new HelpBox("Play 模式只显示状态；退出后可继续编辑。", HelpBoxMessageType.Info));
            var scanButton = new Button(AutoScanMissing) { text = "自动扫描缺失参数" };
            scanButton.SetEnabled(!readOnly);
            _body.Add(scanButton);
            _body.Add(new HelpBox("只补空绑定和可识别的材质映射；已有设置、GI／雾开关不会被覆盖。", HelpBoxMessageType.Info));
            if (!string.IsNullOrEmpty(_scanMessage))
                _body.Add(new HelpBox(_scanMessage,
                    _scanHasWarnings ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info));
            var bindings = Section("场景绑定");
            AddToggle(bindings, "启用间接光", c.IndirectLightingEnabled, value => EditScene("切换间接光", x => x.IndirectLightingEnabled = value));
            AddToggle(bindings, "启用高度雾", c.HeightFogEnabled, value => EditScene("切换高度雾", x => x.HeightFogEnabled = value));
            AddObject<Transform>(bindings, "静态场景根", c.StaticRoot, value =>
            {
                EditScene("绑定静态场景", x =>
                { x.StaticRoot = value; x.StaticSourceRenderers = null; });
                _scanMessage = string.Empty;
                RebuildBody();
            });
            if (c.StaticSourceRenderers != null && c.StaticSourceRenderers.Length > 0)
                bindings.Add(new HelpBox("GI 将仅预处理显式选择的 " + c.StaticSourceRenderers.Length +
                    " 个网格，不复制场景几何。", HelpBoxMessageType.Info));
            AddObject<Camera>(bindings, "目标相机", c.TargetCamera, value => EditScene("绑定渲染相机", x => x.TargetCamera = value));
            AddObject<Light>(bindings, "太阳", c.Sun, value => EditScene("绑定太阳", x => x.Sun = value));
            AddObject<MxSceneLightingData>(bindings, "光传输缓存", c.Data, value => EditScene("绑定光传输缓存", x => x.Data = value));
            BuildProbePreviewPanel(c, readOnly);

            var sun = Section("太阳与天光");
            if (c.Sun != null)
            {
                AddColor(sun, "太阳颜色", c.Sun.color, value => EditSun("太阳颜色", false, x => x.color = value));
                AddFloat(sun, "太阳强度", c.Sun.intensity, value => EditSun("太阳强度", false, x => x.intensity = Mathf.Max(0, value)));
                var rotation = new Vector3Field("太阳方向") { value = c.Sun.transform.eulerAngles };
                rotation.RegisterValueChangedCallback(evt => EditSun("太阳方向", true,
                    x => x.transform.eulerAngles = evt.newValue));
                sun.Add(rotation);
            }
            if (c.Look != null)
            {
                AddColor(sun, "天光", c.Look.Sky, value => EditLook("天光", x => x.Sky = value));
                AddColor(sun, "地平线环境光", c.Look.Equator, value => EditLook("地平线环境光", x => x.Equator = value));
                AddColor(sun, "地面环境光", c.Look.Ground, value => EditLook("地面环境光", x => x.Ground = value));
            }

            var look = Section("共享 Look");
            AddObject<MxSceneLightingLook>(look, "Look 资产", c.Look, value => EditScene("绑定共享 Look", x => x.Look = value));
            look.Add(new HelpBox("Look 可被多个场景引用。这里的修改会影响所有引用它的场景。", HelpBoxMessageType.Info));
            if (c.Look != null)
            {
                AddFloat(look, "直接光", c.Look.DirectStrength, value => EditLook("直接光风格", x => x.DirectStrength = Mathf.Max(0, value)));
                AddFloat(look, "间接光", c.Look.IndirectStrength, value => EditLook("间接光风格", x => x.IndirectStrength = Mathf.Max(0, value)));
                AddFloat(look, "微观遮蔽", c.Look.MicroOcclusion, value => EditLook("微观遮蔽", x => x.MicroOcclusion = Mathf.Clamp01(value)));
                AddFloat(look, "角色接地遮蔽", c.Look.CapsuleOcclusion, value => EditLook("角色接地遮蔽", x => x.CapsuleOcclusion = Mathf.Clamp01(value)));
                AddFloat(look, "明暗分区", c.Look.BandStrength, value => EditLook("明暗分区", x => x.BandStrength = Mathf.Clamp01(value)));
                AddColor(look, "阴影冷暖", c.Look.ShadowTint, value => EditLook("阴影冷暖", x => x.ShadowTint = value));
            }

            var fog = Section("高度雾");
            AddColor(fog, "雾颜色", c.FogColor, value => EditScene("雾颜色", x => x.FogColor = value));
            AddFloat(fog, "雾密度", c.FogDensity, value => EditScene("雾密度", x => x.FogDensity = Mathf.Clamp(value, 0, 5)));
            AddFloat(fog, "基准高度", c.FogBaseHeight, value => EditScene("雾基准高度", x => x.FogBaseHeight = value));
            AddFloat(fog, "高度衰减", c.FogHeightFalloff, value => EditScene("雾高度衰减", x => x.FogHeightFalloff = Mathf.Clamp(value, 0, 8)));
            AddFloat(fog, "作用距离", c.FogMaxDistance, value => EditScene("雾作用距离", x => x.FogMaxDistance = Mathf.Max(1, value)));
            AddFloat(fog, "散射强度", c.FogScatteringIntensity, value => EditScene("雾散射强度", x => x.FogScatteringIntensity = Mathf.Clamp(value, 0, 8)));
            AddFloat(fog, "中心清晰半径", c.FogFocusClearRadius, value => EditScene("中心清晰半径", x => x.FogFocusClearRadius = Mathf.Max(0, value)));
            AddFloat(fog, "中心过渡宽度", c.FogFocusClearSoftness, value => EditScene("中心过渡宽度", x => x.FogFocusClearSoftness = Mathf.Max(.001f, value)));

            BuildWaterPanel(readOnly);

            var dust = Section("空气微尘");
            AddToggle(dust, "启用空气微尘", c.DustEnabled, value => EditScene("切换空气微尘", x => x.DustEnabled = value));
            if (c.Dust != null)
            {
                AddColor(dust, "微尘颜色", c.Dust.Color, value => EditScene("微尘颜色", x => x.Dust.Color = value));
                AddFloat(dust, "粒子数量", c.Dust.ParticleCount, value => EditScene("微尘数量", x => x.Dust.ParticleCount = Mathf.Clamp(Mathf.RoundToInt(value), 0, 8192)));
                AddFloat(dust, "覆盖半径", c.Dust.Radius, value => EditScene("微尘范围", x => x.Dust.Radius = Mathf.Clamp(value, 1, 200)));
                AddFloat(dust, "基准高度", c.Dust.BaseHeight, value => EditScene("微尘高度", x => x.Dust.BaseHeight = value));
                AddFloat(dust, "分布高度", c.Dust.Height, value => EditScene("微尘厚度", x => x.Dust.Height = Mathf.Clamp(value, .1f, 100)));
                AddFloat(dust, "颗粒大小", c.Dust.Size, value => EditScene("微尘大小", x => x.Dust.Size = Mathf.Clamp(value, .001f, .3f)));
                AddFloat(dust, "不透明度", c.Dust.Opacity, value => EditScene("微尘透明度", x => x.Dust.Opacity = Mathf.Clamp01(value)));
                AddFloat(dust, "漂移速度", c.Dust.DriftSpeed, value => EditScene("微尘漂移", x => x.Dust.DriftSpeed = Mathf.Clamp(value, 0, 10)));
                AddFloat(dust, "前向散射", c.Dust.Anisotropy, value => EditScene("微尘散射", x => x.Dust.Anisotropy = Mathf.Clamp(value, -.85f, .85f)));
                AddFloat(dust, "受光强度", c.Dust.LightResponse, value => EditScene("微尘受光", x => x.Dust.LightResponse = Mathf.Clamp(value, 0, 20)));
            }
            dust.SetEnabled(!readOnly);

            var directional = Section("固定光向真实轮廓阴影");
            directional.Add(new Label("中心战斗区使用高精度阴影，周边使用独立覆盖；交界平滑过渡。"));
            AddToggle(directional, "启用固定阴影", c.DirectionalShadowsEnabled, value => EditScene("切换固定阴影", x => x.DirectionalShadowsEnabled = value));
            AddToggle(directional, "动态角色三角形阴影", c.DynamicDirectionalShadows, value => EditScene("切换动态阴影", x => x.DynamicDirectionalShadows = value));
            AddFloat(directional, "深度偏移（米）", c.DirectionalShadowBias, value => EditScene("阴影偏移", x => x.DirectionalShadowBias = Mathf.Max(0,value)));
            AddFloat(directional, "软化半径（米，0 为基础抗锯齿）", c.DirectionalShadowFilterRadius, value => EditScene("阴影过滤", x => x.DirectionalShadowFilterRadius = Mathf.Max(0,value)));
            var bakeDirectional = new Button(() => { try { CharacterTestDirectionalShadowAuthoring.Bake(_owner); Changed(); RebuildBody(); } catch(Exception exception) { _status=exception.Message; RebuildBody(); } }) { text = "烘焙静态物体与草的真实轮廓" };
            bakeDirectional.SetEnabled(!readOnly && c.StaticRoot != null && c.Sun != null); directional.Add(bakeDirectional);
            if (CharacterTestSceneRenderingPreview.Session != null)
                directional.Add(new Label(CharacterTestSceneRenderingPreview.Session.DirectionalShadowIssue));
            var build = Section("构建与诊断");
            _diagnosticLabel = new Label(Status); build.Add(_diagnosticLabel);
            build.Add(new Label("构建状态：" + MxSceneLightingBuilder.Status));
            var preview = CharacterTestSceneRenderingPreview.Session;
            if (preview != null)
                build.Add(new Label("空气微尘绘制 " + preview.DustDrawsRecorded + " 次"));
            if (preview != null)
                build.Add(new Label("世界光照更新 " + preview.LightingUpdates + " 次；太阳可见性更新 " + preview.VisibilityUpdates + " 次"));
            var debug = new PopupField<string>("预览数据", new System.Collections.Generic.List<string>
                { "正常画面", "间接光", "法线", "运动矢量" }, 0);
            debug.RegisterValueChangedCallback(evt => CharacterTestSceneRenderingPreview.SetDebugView(
                evt.newValue == "间接光" ? 1 : evt.newValue == "法线" ? 2 : evt.newValue == "运动矢量" ? 3 : 0));
            debug.SetEnabled(!readOnly);
            build.Add(debug);
            var controls = new VisualElement(); build.Add(controls);
            var buildButton = new Button(() => BuildTransport()) { text = "构建光传输" };
            var occluderButton = new Button(() => RebuildOccluders()) { text = "仅重建太阳遮挡" };
            var cancelButton = new Button(CancelBuild) { text = "取消构建" };
            buildButton.SetEnabled(!readOnly && !MxSceneLightingBuilder.IsBuilding && c.StaticRoot != null);
            occluderButton.SetEnabled(!readOnly && !MxSceneLightingBuilder.IsBuilding && c.Data != null);
            cancelButton.SetEnabled(!readOnly && MxSceneLightingBuilder.IsBuilding);
            controls.Add(buildButton); controls.Add(occluderButton); controls.Add(cancelButton);
            var advanced = CreateFoldout("高级绑定与材质映射", "advanced-bindings", false);
            _body.Add(advanced);
            AddObject<MxRenderingPipelineFeature>(advanced, "URP 渲染入口", c.Feature, value => EditScene("绑定渲染入口", x => x.Feature = value));
            AddObject<ComputeShader>(advanced, "光照计算", c.Compute, value => EditScene("绑定光照计算", x => x.Compute = value));
            AddObject<Shader>(advanced, "遮挡捕获", c.CaptureShader, value => EditScene("绑定遮挡 Shader", x => x.CaptureShader = value));
            AddObject<Shader>(advanced, "光照解析", c.ResolveShader, value => EditScene("绑定光照解析", x => x.ResolveShader = value));
            AddObject<Shader>(advanced, "高度雾合成", c.FogCompositeShader, value => EditScene("绑定高度雾 Shader", x => x.FogCompositeShader = value));
            AddObject<Shader>(advanced, "空气微尘绘制", c.DustShader, value => EditScene("绑定微尘 Shader", x => x.DustShader = value));
            AddToggle(advanced, "编辑器预览微尘", c.PreviewDustInSceneView, value => EditScene("预览空气微尘", x => x.PreviewDustInSceneView = value));
            if (c.StaticSourceRenderers != null && c.StaticSourceRenderers.Length > 0)
            {
                var sources = CreateFoldout("GI 静态源（" + c.StaticSourceRenderers.Length + "）", "static-sources", false);
                advanced.Add(sources);
                for (int i = 0; i < c.StaticSourceRenderers.Length; i++)
                {
                    var field = new ObjectField("网格 " + (i + 1))
                    { objectType = typeof(MeshRenderer), allowSceneObjects = true, value = c.StaticSourceRenderers[i] };
                    field.SetEnabled(false);
                    sources.Add(field);
                }
            }
            AddToggle(advanced, "编辑器预览间接光", c.PreviewIndirectInSceneView, value => EditScene("预览间接光", x => x.PreviewIndirectInSceneView = value));
            AddToggle(advanced, "编辑器预览高度雾", c.PreviewFogInSceneView, value => EditScene("预览高度雾", x => x.PreviewFogInSceneView = value));
            AddToggle(advanced, "请求运动矢量", c.RequireMotionVectors, value => EditScene("运动矢量", x => x.RequireMotionVectors = value));
            var layout = c.SourceLayout ?? new MxSceneLightingSourceLayout();
            AddText(advanced, "底色属性", layout.BaseColorProperty, value => EditScene("底色映射", x => x.SourceLayout.BaseColorProperty = value));
            AddText(advanced, "表面数据属性", layout.SurfaceDataProperty, value => EditScene("表面数据映射", x => x.SourceLayout.SurfaceDataProperty = value));
            AddText(advanced, "发光强度属性", layout.EmissionIntensityProperty, value => EditScene("发光强度映射", x => x.SourceLayout.EmissionIntensityProperty = value));
            AddText(advanced, "纯色属性", layout.SolidColorProperty, value => EditScene("纯色映射", x => x.SourceLayout.SolidColorProperty = value));
            AddText(advanced, "次级颜色属性", layout.SecondaryColorProperty, value => EditScene("次级颜色映射", x => x.SourceLayout.SecondaryColorProperty = value));
            AddText(advanced, "次级混合属性", layout.SecondaryBlendProperty, value => EditScene("次级混合映射", x => x.SourceLayout.SecondaryBlendProperty = value));
            if (layout.DiffuseFractions != null && layout.DiffuseFractions.Length == 16)
                for (int i = 0; i < 16; i++)
                {
                    int index = i;
                    AddFloat(advanced, "漫反射率 " + i, layout.DiffuseFractions[i], value => EditScene("漫反射率", x => x.SourceLayout.DiffuseFractions[index] = Mathf.Clamp01(value)));
                }
            advanced.SetEnabled(!readOnly);
            bindings.SetEnabled(!readOnly); sun.SetEnabled(!readOnly); look.SetEnabled(!readOnly); fog.SetEnabled(!readOnly);
        }

        private Foldout Section(string label)
        {
            var fold = CreateFoldout(label, label, true);
            _body.Add(fold); return fold;
        }

        private Foldout CreateFoldout(string label, string key, bool expanded)
        {
            if (_foldoutStates.TryGetValue(key, out bool saved)) expanded = saved;
            var fold = new Foldout { text = label, value = expanded };
            fold.RegisterValueChangedCallback(evt =>
            {
                // Child toggles also bubble ChangeEvent<bool> through a Foldout.
                if (evt.target == fold) _foldoutStates[key] = evt.newValue;
            });
            return fold;
        }
        private static void AddToggle(VisualElement parent, string label, bool value, Action<bool> edit)
        {
            var field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt => edit(evt.newValue)); parent.Add(field);
        }
        private static void AddFloat(VisualElement parent, string label, float value, Action<float> edit)
        {
            var field = new FloatField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt => edit(evt.newValue)); parent.Add(field);
        }
        private static void AddText(VisualElement parent, string label, string value, Action<string> edit)
        {
            var field = new TextField(label) { value = value ?? string.Empty, isDelayed = true };
            field.RegisterValueChangedCallback(evt => edit(evt.newValue)); parent.Add(field);
        }
        private static void AddColor(VisualElement parent, string label, Color value, Action<Color> edit)
        {
            var field = new ColorField(label) { value = value };
            field.RegisterValueChangedCallback(evt => edit(evt.newValue)); parent.Add(field);
        }
        private static void AddObject<T>(VisualElement parent, string label, T value, Action<T> edit) where T : UnityEngine.Object
        {
            var field = new ObjectField(label) { objectType = typeof(T), allowSceneObjects = true, value = value };
            field.RegisterValueChangedCallback(evt => edit(evt.newValue as T)); parent.Add(field);
        }
    }
}

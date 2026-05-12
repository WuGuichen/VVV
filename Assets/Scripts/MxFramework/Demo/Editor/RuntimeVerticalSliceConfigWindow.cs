using MxFramework.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    public sealed class RuntimeVerticalSliceConfigWindow : EditorWindow
    {
        private const string MenuPath = "MxFramework/Runtime Showcase/Scene Config";
        private const string ScenePath = "Assets/Scenes/RuntimeVerticalSlice.unity";
        private const string RuntimeConfigAssetPath = "Assets/Config/MxFramework/Demo/RuntimeVerticalSliceSceneConfig.asset";
        private const string PreviewProfileAssetPath = "Assets/Config/MxFramework/Preview/RuntimeVerticalSlicePreviewTargets.asset";

        private RuntimeVerticalSliceSceneConfig _runtimeConfig;
        private MxPreviewSceneTargetProfile _previewProfile;
        private SerializedObject _runtimeSerialized;
        private SerializedObject _previewSerialized;
        private HelpBox _validationBox;

        [MenuItem(MenuPath, priority = 120)]
        public static void Open()
        {
            var window = GetWindow<RuntimeVerticalSliceConfigWindow>();
            window.titleContent = new GUIContent("Runtime Showcase");
            window.minSize = new Vector2(520, 640);
        }

        public void CreateGUI()
        {
            LoadOrCreateAssets();

            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12;
            rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            var title = new Label("Runtime Vertical Slice 场景配置");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 18;
            rootVisualElement.Add(title);

            rootVisualElement.Add(new HelpBox(
                "这里是测试场景的唯一预配置入口。场景中不需要预挂 RuntimeVerticalSliceRunner、PreviewCaster 或 PreviewTarget；Play 时会按这里的配置动态生成运行时对象。",
                HelpBoxMessageType.Info));

            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8, marginBottom = 8 } };
            toolbar.Add(CreateButton("打开测试场景", OpenScene));
            toolbar.Add(CreateButton("定位配置资产", PingAssets));
            toolbar.Add(CreateButton("校验配置", ValidateConfig));
            rootVisualElement.Add(toolbar);

            _validationBox = new HelpBox("", HelpBoxMessageType.None);
            _validationBox.style.display = DisplayStyle.None;
            rootVisualElement.Add(_validationBox);

            var scroll = new ScrollView();
            rootVisualElement.Add(scroll);

            AddRuntimeConfig(scroll);
            AddPreviewProfile(scroll);

            ValidateConfig();
        }

        private void AddRuntimeConfig(VisualElement root)
        {
            root.Add(SectionTitle("运行入口"));
            root.Add(new HelpBox(
                "Auto Start 开启后，进入 RuntimeVerticalSlice 场景 Play 时会自动创建 RuntimeVerticalSliceRuntime，并动态挂载 Runner / HUD / Ability Slice。",
                HelpBoxMessageType.None));

            AddProperty(root, _runtimeSerialized, "_autoStartInScene", "自动启动", "关闭后 Play 不会自动生成 Runner，适合只查看空场景。");
            AddProperty(root, _runtimeSerialized, "_sceneName", "目标场景名", "只有当前场景名匹配时才自动启动，避免污染其他测试场景。");

            root.Add(SectionTitle("Showcase 模式"));
            AddProperty(root, _runtimeSerialized, "_useAbilitySlice", "启用 Ability Showcase", "体验 Entity -> Ability -> Target -> Effect -> Buff -> Event 闭环。");
            AddProperty(root, _runtimeSerialized, "_useConfigDrivenAbility", "Ability 使用配置驱动", "Strike / Ignite 由 BasicAbilityConfig 创建。");
            AddProperty(root, _runtimeSerialized, "_enableShowcaseUi", "启用 UI Toolkit HUD", "制作人手测推荐开启。关闭后可用 legacy OnGUI 兜底。");
            AddProperty(root, _runtimeSerialized, "_showLegacyOnGui", "显示 Legacy OnGUI", "调试兜底显示；HUD 开启时通常不需要。");

            root.Add(SectionTitle("旧 Runtime Slice 模式"));
            root.Add(new HelpBox("这些模式只在未启用 Ability Showcase 时作为底层功能验证入口。后续会逐步接入 UI Toolkit。", HelpBoxMessageType.None));
            AddProperty(root, _runtimeSerialized, "_useConfigDriven", "配置驱动 Buff / Modifier", "从 Demo 配置创建 Buff 和 Modifier。");
            AddProperty(root, _runtimeSerialized, "_usePatchFile", "加载 Patch 文件", "从 StreamingAssets 读取 runtime_config_patch.json。");
            AddProperty(root, _runtimeSerialized, "_patchFilePath", "Patch 文件路径", "相对 StreamingAssets 的路径。");
            AddProperty(root, _runtimeSerialized, "_useModPackage", "加载单个 Mod Package", "读取一个 Runtime Mod Package。");
            AddProperty(root, _runtimeSerialized, "_modPackagePath", "单包目录", "相对 StreamingAssets 的目录。");
            AddProperty(root, _runtimeSerialized, "_useModPackageLoadPlanMerge", "多包 LoadPlan 合并", "发现多个包并按 LoadPlan 合并。");
            AddProperty(root, _runtimeSerialized, "_showCatalog", "只展示 Package Catalog", "只扫描并显示包目录，不运行闭环。");
            AddProperty(root, _runtimeSerialized, "_useModPackageLoadout", "使用 Loadout", "多包合并时使用固定启用列表。");
            AddProperty(root, _runtimeSerialized, "_loadoutFilePath", "Loadout 文件路径", "相对 StreamingAssets 的路径。");

            root.Add(SectionTitle("初始数值 / 诊断"));
            AddProperty(root, _runtimeSerialized, "_initialHp", "初始 HP", "Runtime Slice 目标初始生命。");
            AddProperty(root, _runtimeSerialized, "_initialAttack", "初始 Attack", "Runtime Slice 目标初始攻击。");
            AddProperty(root, _runtimeSerialized, "_initialDefense", "初始 Defense", "Runtime Slice 目标初始防御。");
            AddProperty(root, _runtimeSerialized, "_writeModDiagnosticSnapshot", "写出 Mod 诊断 Snapshot", "写入 Application.persistentDataPath/MxFramework/Diagnostics。");
            AddProperty(root, _runtimeSerialized, "_modDiagnosticSnapshotFileName", "Snapshot 文件名", "只填写文件名，不建议填绝对路径。");
        }

        private void AddPreviewProfile(VisualElement root)
        {
            root.Add(SectionTitle("Preview Targets"));
            root.Add(new HelpBox(
                "这里编辑的是 Preview 目标定义，不是场景组件。Preview Server 启动时会按列表动态生成 MxPreviewSceneTarget；运行前场景里不会常驻 SceneTargetConfig。",
                HelpBoxMessageType.Info));

            AddProperty(root, _previewSerialized, "_enabled", "启用场景预览目标", "关闭后 Preview Server 会回退 dummy world。");
            AddProperty(root, _previewSerialized, "_targets", "目标列表", "至少保留 TestTarget 和 TestCaster，外部预览默认会使用这两个 ID。");
        }

        private static Label SectionTitle(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 14;
            label.style.marginBottom = 4;
            return label;
        }

        private static void AddProperty(VisualElement root, SerializedObject serialized, string propertyName, string label, string tooltip)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                root.Add(new HelpBox("缺少配置项：" + propertyName, HelpBoxMessageType.Error));
                return;
            }

            var field = new PropertyField(property, label) { tooltip = tooltip };
            field.Bind(serialized);
            root.Add(field);
        }

        private static Button CreateButton(string text, System.Action action)
        {
            var button = new Button(action) { text = text };
            button.style.marginRight = 6;
            return button;
        }

        private void LoadOrCreateAssets()
        {
            EnsureFolder("Assets/Config");
            EnsureFolder("Assets/Config/MxFramework");
            EnsureFolder("Assets/Config/MxFramework/Demo");
            EnsureFolder("Assets/Config/MxFramework/Preview");

            _runtimeConfig = AssetDatabase.LoadAssetAtPath<RuntimeVerticalSliceSceneConfig>(RuntimeConfigAssetPath);
            if (_runtimeConfig == null)
            {
                _runtimeConfig = CreateInstance<RuntimeVerticalSliceSceneConfig>();
                AssetDatabase.CreateAsset(_runtimeConfig, RuntimeConfigAssetPath);
            }

            _previewProfile = AssetDatabase.LoadAssetAtPath<MxPreviewSceneTargetProfile>(PreviewProfileAssetPath);
            if (_previewProfile == null)
            {
                _previewProfile = CreateInstance<MxPreviewSceneTargetProfile>();
                AssetDatabase.CreateAsset(_previewProfile, PreviewProfileAssetPath);
            }

            AssetDatabase.SaveAssets();
            _runtimeSerialized = new SerializedObject(_runtimeConfig);
            _previewSerialized = new SerializedObject(_previewProfile);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        private void PingAssets()
        {
            EditorGUIUtility.PingObject(_runtimeConfig);
            Selection.activeObject = _runtimeConfig;
        }

        private void ValidateConfig()
        {
            _runtimeSerialized.ApplyModifiedProperties();
            _previewSerialized.ApplyModifiedProperties();

            string message = "";
            HelpBoxMessageType type = HelpBoxMessageType.Info;

            if (_runtimeConfig == null || _previewProfile == null)
            {
                message = "配置资产缺失。请关闭并重新打开本窗口。";
                type = HelpBoxMessageType.Error;
            }
            else if (string.IsNullOrEmpty(_runtimeConfig.SceneName))
            {
                message = "目标场景名为空，自动启动不会生效。";
                type = HelpBoxMessageType.Warning;
            }
            else if (_previewProfile.Enabled && _previewProfile.Targets.Length == 0)
            {
                message = "Preview Target 列表为空，Preview Server 会回退 dummy world。";
                type = HelpBoxMessageType.Warning;
            }
            else
            {
                message = "配置可用：场景保持轻量，Play / Preview Server 会按资产动态生成运行时对象。";
            }

            if (_validationBox != null)
            {
                _validationBox.text = message;
                _validationBox.messageType = type;
                _validationBox.style.display = DisplayStyle.Flex;
            }
        }
    }
}

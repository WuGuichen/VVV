using UnityEditor;
using UnityEngine;

namespace MxFramework.Demo
{
    [CustomEditor(typeof(RuntimeVerticalSliceRunner))]
    public sealed class RuntimeVerticalSliceRunnerEditor : UnityEditor.Editor
    {
        private SerializedProperty _useAbilitySlice;
        private SerializedProperty _useConfigDrivenAbility;
        private SerializedProperty _enableShowcaseUi;
        private SerializedProperty _showLegacyOnGui;
        private SerializedProperty _useResourceCatalogWarmup;
        private SerializedProperty _useConfigDriven;
        private SerializedProperty _usePatchFile;
        private SerializedProperty _patchFilePath;
        private SerializedProperty _useModPackage;
        private SerializedProperty _modPackagePath;
        private SerializedProperty _useModPackageLoadPlanMerge;
        private SerializedProperty _showCatalog;
        private SerializedProperty _useModPackageLoadout;
        private SerializedProperty _loadoutFilePath;
        private SerializedProperty _writeModDiagnosticSnapshot;
        private SerializedProperty _modDiagnosticSnapshotFileName;
        private SerializedProperty _initialHp;
        private SerializedProperty _initialAttack;
        private SerializedProperty _initialDefense;

        private void OnEnable()
        {
            _useAbilitySlice = serializedObject.FindProperty("_useAbilitySlice");
            _useConfigDrivenAbility = serializedObject.FindProperty("_useConfigDrivenAbility");
            _enableShowcaseUi = serializedObject.FindProperty("_enableShowcaseUi");
            _showLegacyOnGui = serializedObject.FindProperty("_showLegacyOnGui");
            _useResourceCatalogWarmup = serializedObject.FindProperty("_useResourceCatalogWarmup");
            _useConfigDriven = serializedObject.FindProperty("_useConfigDriven");
            _usePatchFile = serializedObject.FindProperty("_usePatchFile");
            _patchFilePath = serializedObject.FindProperty("_patchFilePath");
            _useModPackage = serializedObject.FindProperty("_useModPackage");
            _modPackagePath = serializedObject.FindProperty("_modPackagePath");
            _useModPackageLoadPlanMerge = serializedObject.FindProperty("_useModPackageLoadPlanMerge");
            _showCatalog = serializedObject.FindProperty("_showCatalog");
            _useModPackageLoadout = serializedObject.FindProperty("_useModPackageLoadout");
            _loadoutFilePath = serializedObject.FindProperty("_loadoutFilePath");
            _writeModDiagnosticSnapshot = serializedObject.FindProperty("_writeModDiagnosticSnapshot");
            _modDiagnosticSnapshotFileName = serializedObject.FindProperty("_modDiagnosticSnapshotFileName");
            _initialHp = serializedObject.FindProperty("_initialHp");
            _initialAttack = serializedObject.FindProperty("_initialAttack");
            _initialDefense = serializedObject.FindProperty("_initialDefense");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MxFramework Runtime Showcase", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "这个组件是场景中的统一入口。Play 时会按下面的配置动态挂载 Ability Runner、UI Toolkit HUD 和相关适配器，场景里不需要手动堆多个 MonoBehaviour。",
                MessageType.Info);

            DrawShowcaseMode();
            DrawRuntimeMode();
            DrawPatchAndMod();
            DrawInitialValues();
            DrawDiagnostics();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawShowcaseMode()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Showcase 入口", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useAbilitySlice, new GUIContent("启用 Ability Showcase", "动态挂载 RuntimeAbilitySliceRunner。用于体验 Entity -> Ability -> Target -> Effect -> Buff -> Event 闭环。"));

            using (new EditorGUI.DisabledScope(!_useAbilitySlice.boolValue))
            {
                EditorGUILayout.PropertyField(_useConfigDrivenAbility, new GUIContent("Ability 使用配置驱动", "Strike / Ignite 由 BasicAbilityConfig 创建；关闭时使用硬编码 SimpleAbility。"));
                EditorGUILayout.PropertyField(_enableShowcaseUi, new GUIContent("启用 UI Toolkit HUD", "动态挂载 MxRuntimeHudController 和 RuntimeAbilitySliceShowcaseUi。建议保持开启，这是制作人手测入口。"));
            }

            EditorGUILayout.PropertyField(_showLegacyOnGui, new GUIContent("显示 Legacy OnGUI", "临时后备调试文本。Ability Showcase 且 HUD 开启时会自动隐藏，其他旧模式默认依赖它显示状态。"));
            EditorGUILayout.PropertyField(_useResourceCatalogWarmup, new GUIContent("启用资源 Catalog Warmup", "用 ResourceManager / ResourcePreloadService 预热 Runtime Showcase 资源。"));

            if (_useAbilitySlice.boolValue && !_enableShowcaseUi.boolValue && !_showLegacyOnGui.boolValue)
                EditorGUILayout.HelpBox("当前 Ability Showcase 没有任何可见 UI。建议开启 UI Toolkit HUD。", MessageType.Warning);
        }

        private void DrawRuntimeMode()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("非 Ability 运行模式", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("这些模式只在未启用 Ability Showcase 时生效。当前仍使用 legacy OnGUI 展示，后续会迁移到同一套 UI Toolkit HUD。", MessageType.None);

            using (new EditorGUI.DisabledScope(_useAbilitySlice.boolValue))
            {
                EditorGUILayout.PropertyField(_useConfigDriven, new GUIContent("配置驱动 Buff / Modifier", "从 RuntimeConfigSliceDemoData 创建 Buff 和 Modifier。"));
                EditorGUILayout.PropertyField(_usePatchFile, new GUIContent("加载 Patch 文件", "从 StreamingAssets 读取 mx.runtimeConfigPatch.v1 JSON，并覆盖基础配置。"));
                EditorGUILayout.PropertyField(_useModPackage, new GUIContent("加载单个 Mod Package", "读取一个 Runtime Mod Package 并应用其中的 Runtime Patch。"));
                EditorGUILayout.PropertyField(_useModPackageLoadPlanMerge, new GUIContent("多包 LoadPlan 合并", "发现多个包，构建 LoadPlan 后按顺序合并 Patch。"));
                EditorGUILayout.PropertyField(_showCatalog, new GUIContent("只展示 Package Catalog", "只扫描并显示包目录和 LoadPlan，不运行 Buff / Modifier 闭环。"));
            }
        }

        private void DrawPatchAndMod()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Patch / Mod 路径", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("路径相对 StreamingAssets。保持相对路径可以让项目在不同机器上稳定运行。", MessageType.None);

            EditorGUILayout.PropertyField(_patchFilePath, new GUIContent("Patch 文件路径", "例：MxFramework/Demo/runtime_config_patch.json"));
            EditorGUILayout.PropertyField(_modPackagePath, new GUIContent("单包目录", "例：MxFramework/Demo/runtime-patch-mod"));
            EditorGUILayout.PropertyField(_useModPackageLoadout, new GUIContent("使用 Loadout 文件", "为多包合并提供启用列表和可复现 profile。只在 LoadPlan Merge 模式下有意义。"));

            using (new EditorGUI.DisabledScope(!_useModPackageLoadout.boolValue))
            {
                EditorGUILayout.PropertyField(_loadoutFilePath, new GUIContent("Loadout 文件路径", "例：MxFramework/Demo/mod_loadout.json"));
            }
        }

        private void DrawInitialValues()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("初始数值", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_initialHp, new GUIContent("初始 HP", "Runtime Slice 目标初始生命值。"));
            EditorGUILayout.PropertyField(_initialAttack, new GUIContent("初始 Attack", "Runtime Slice 目标初始攻击力。"));
            EditorGUILayout.PropertyField(_initialDefense, new GUIContent("初始 Defense", "Runtime Slice 目标初始防御力。"));
        }

        private void DrawDiagnostics()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("诊断输出", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_writeModDiagnosticSnapshot, new GUIContent("写出 Mod 诊断 Snapshot", "LoadPlan Merge 模式下把诊断 JSON 写到 Application.persistentDataPath/MxFramework/Diagnostics。"));

            using (new EditorGUI.DisabledScope(!_writeModDiagnosticSnapshot.boolValue))
            {
                EditorGUILayout.PropertyField(_modDiagnosticSnapshotFileName, new GUIContent("Snapshot 文件名", "只填写文件名，不建议填写绝对路径。"));
            }
        }
    }
}

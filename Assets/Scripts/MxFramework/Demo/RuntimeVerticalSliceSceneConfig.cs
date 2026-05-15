using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    [CreateAssetMenu(menuName = "MxFramework/Demo/Runtime Vertical Slice Scene Config")]
    public sealed class RuntimeVerticalSliceSceneConfig : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Config/MxFramework/Demo/RuntimeVerticalSliceSceneConfig.asset";
        public const string DefaultSceneName = "RuntimeVerticalSlice";

        [SerializeField] private bool _autoStartInScene = true;
        [SerializeField] private string _sceneName = DefaultSceneName;

        [Header("Showcase")]
        [SerializeField] private bool _useAbilitySlice = true;
        [SerializeField] private bool _useConfigDrivenAbility = true;
        [SerializeField] private bool _enableShowcaseUi = true;
        [SerializeField] private bool _showLegacyOnGui;
        [SerializeField] private PanelSettings _hudPanelSettings;
        [SerializeField] private VisualTreeAsset _hudVisualTree;
        [SerializeField] private StyleSheet _hudStyleSheet;
        [SerializeField] private Font _hudFont;

        [Header("Samples Resource Test")]
        [SerializeField] private bool _useResourceCatalogWarmup = true;

        [Header("Runtime Slice")]
        [SerializeField] private bool _useConfigDriven;
        [SerializeField] private bool _usePatchFile;
        [SerializeField] private string _patchFilePath = "MxFramework/Demo/runtime_config_patch.json";
        [SerializeField] private bool _useModPackage;
        [SerializeField] private string _modPackagePath = "MxFramework/Demo/runtime-patch-mod";
        [SerializeField] private bool _useModPackageLoadPlanMerge;
        [SerializeField] private bool _showCatalog;
        [SerializeField] private bool _useModPackageLoadout;
        [SerializeField] private string _loadoutFilePath = "MxFramework/Demo/mod_loadout.json";

        [Header("Diagnostics")]
        [SerializeField] private bool _writeModDiagnosticSnapshot = true;
        [SerializeField] private string _modDiagnosticSnapshotFileName = "mod_diagnostic_snapshot.json";

        [Header("Initial Values")]
        [SerializeField] private int _initialHp = 1000;
        [SerializeField] private int _initialAttack = 100;
        [SerializeField] private int _initialDefense = 20;

        public bool AutoStartInScene => _autoStartInScene;
        public string SceneName => string.IsNullOrEmpty(_sceneName) ? DefaultSceneName : _sceneName;
        public bool UseAbilitySlice => _useAbilitySlice;
        public bool UseConfigDrivenAbility => _useConfigDrivenAbility;
        public bool EnableShowcaseUi => _enableShowcaseUi;
        public bool ShowLegacyOnGui => _showLegacyOnGui;
        public PanelSettings HudPanelSettings => _hudPanelSettings;
        public VisualTreeAsset HudVisualTree => _hudVisualTree;
        public StyleSheet HudStyleSheet => _hudStyleSheet;
        public Font HudFont => _hudFont;
        public bool UseResourceCatalogWarmup => _useResourceCatalogWarmup;
        public bool UseConfigDriven => _useConfigDriven;
        public bool UsePatchFile => _usePatchFile;
        public string PatchFilePath => _patchFilePath;
        public bool UseModPackage => _useModPackage;
        public string ModPackagePath => _modPackagePath;
        public bool UseModPackageLoadPlanMerge => _useModPackageLoadPlanMerge;
        public bool ShowCatalog => _showCatalog;
        public bool UseModPackageLoadout => _useModPackageLoadout;
        public string LoadoutFilePath => _loadoutFilePath;
        public bool WriteModDiagnosticSnapshot => _writeModDiagnosticSnapshot;
        public string ModDiagnosticSnapshotFileName => _modDiagnosticSnapshotFileName;
        public int InitialHp => _initialHp;
        public int InitialAttack => _initialAttack;
        public int InitialDefense => _initialDefense;

        public static RuntimeVerticalSliceSceneConfig LoadDefault()
        {
            var config = CreateInstance<RuntimeVerticalSliceSceneConfig>();
            config.name = "RuntimeVerticalSliceSceneConfigRuntimeDefault";
            return config;
        }
    }
}

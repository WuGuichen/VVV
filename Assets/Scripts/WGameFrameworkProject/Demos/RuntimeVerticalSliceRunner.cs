using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.DebugUI.Adapters;
using MxFramework.Modifiers;
using MxFramework.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    internal sealed class RuntimeSliceTarget
    {
        public RuntimeSliceTarget()
        {
            Store = new AttributeStore();
            Modifiers = new ModifierPipeline(Store, counters: new CounterStore());
        }

        public AttributeStore Store { get; }
        public ModifierPipeline Modifiers { get; set; }
    }

    internal sealed class FlatAddModifier : IAttributeModifier
    {
        public FlatAddModifier(int id, int attributeId, int value)
        {
            Id = id;
            AttributeId = attributeId;
            Value = value;
        }

        public int Id { get; }
        public int AttributeId { get; }
        public int Value { get; }
        public AttributeModifierPhase Phase => AttributeModifierPhase.Add;
        public int Priority => 0;
        public int Modify(int currentValue, IAttributeOwner owner) => currentValue + Value;
    }

    internal sealed class HealEffect : IModifierEffect
    {
        private readonly int _amount;
        public HealEffect(int amount) { _amount = amount; }
        /// <remarks>LowFreqAlloc: demo and validation ticks may update presentation/debug state; core runtime paths document stricter contracts separately.</remarks>
        public void Execute(ModifierContext context) { context.Target.AddAttribute(Const.AttrHp, _amount, this); }
    }

    internal sealed class AlwaysCondition : IModifierCondition
    {
        public bool Evaluate(ModifierContext context) => true;
    }

    internal static class Const
    {
        public const int AttrHp = 1;
        public const int AttrAttack = 2;
        public const int AttrDefense = 3;
        public const int ModAttackBoost = 200001;
        public const int ModHeal = 200003;
    }

    [AddComponentMenu("MxFramework/Demo/Runtime Vertical Slice Runner")]
    public sealed class RuntimeVerticalSliceRunner : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private bool _useAbilitySlice;
        [SerializeField] private bool _useConfigDrivenAbility;
        [SerializeField] private bool _enableShowcaseUi = true;
        [SerializeField] private bool _showLegacyOnGui = true;
        [SerializeField] private PanelSettings _hudPanelSettings;
        [SerializeField] private VisualTreeAsset _hudVisualTree;
        [SerializeField] private StyleSheet _hudStyleSheet;
        [SerializeField] private Font _hudFont;
        [SerializeField] private bool _useResourceCatalogWarmup = true;
        [SerializeField] private bool _useConfigDriven;
        [SerializeField] private bool _usePatchFile;
        [SerializeField] private string _patchFilePath = "MxFramework/Demo/runtime_config_patch.json";
        [SerializeField] private bool _useModPackage;
        [SerializeField] private string _modPackagePath = "MxFramework/Demo/runtime-patch-mod";
        [SerializeField] private bool _useModPackageLoadPlanMerge;
        [SerializeField] private bool _showCatalog;
        [SerializeField] private bool _useModPackageLoadout;
        [SerializeField] private string _loadoutFilePath = "MxFramework/Demo/mod_loadout.json";
        [SerializeField] private bool _writeModDiagnosticSnapshot = true;
        [SerializeField] private string _modDiagnosticSnapshotFileName = "mod_diagnostic_snapshot.json";

        [Header("Initial Values")]
        [SerializeField] private int _initialHp = 1000;
        [SerializeField] private int _initialAttack = 100;
        [SerializeField] private int _initialDefense = 20;

        private RuntimeSliceTarget _target;
        private float _elapsed;
        private int _totalDamageTaken;
        private IConfigProvider _configRegistry;
        private bool _configReady;
        private ConfigChangeSet _changeSet;
        private RuntimeConfigHotReloadResult _lastHotReloadResult;
        private string _patchSourceInfo = string.Empty;
        private string _patchError = string.Empty;
        private string _catalogSummary = string.Empty;
        private string _mergeSummary = string.Empty;
        private string _loadoutSummary = string.Empty;
        private string _snapshotSummary = string.Empty;
        private string _snapshotFilePath = string.Empty;
        private string _resourceWarmupSummary = string.Empty;
        private RuntimeVerticalSliceSampleResourceTest _resourceSampleTest;
        private RuntimeVerticalSlicePlayerResourceTest _playerResourceTest;
        private readonly List<string> _loadoutWarnings = new List<string>();
        private readonly List<string> _orderedPackageKeys = new List<string>();
        private readonly List<string> _skippedPackageKeys = new List<string>();
        private readonly List<string> _resourceTestLines = new List<string>();
        private readonly List<string> _eventLog = new List<string>();

        public bool ConfigReady => _configReady;
        public RuntimeConfigHotReloadResult LastConfigHotReloadResult => _lastHotReloadResult;
        public string ResourceWarmupSummary => _resourceWarmupSummary;
        public IReadOnlyList<string> ResourceBindingLogLines => _resourceTestLines;

        public RuntimeConfigHotReloadDebugSource CreateConfigHotReloadDebugSource()
        {
            return new RuntimeConfigHotReloadDebugSource(() => _lastHotReloadResult);
        }

        private void Awake()
        {
            InitializeTarget();
        }

        public void ApplyConfig(RuntimeVerticalSliceSceneConfig config)
        {
            if (config == null)
                return;

            _useAbilitySlice = config.UseAbilitySlice;
            _useConfigDrivenAbility = config.UseConfigDrivenAbility;
            _enableShowcaseUi = config.EnableShowcaseUi;
            _showLegacyOnGui = config.ShowLegacyOnGui;
            ConfigureHudAssets(config.HudPanelSettings, config.HudVisualTree, config.HudStyleSheet, config.HudFont);
            _useResourceCatalogWarmup = config.UseResourceCatalogWarmup;
            _useConfigDriven = config.UseConfigDriven;
            _usePatchFile = config.UsePatchFile;
            _patchFilePath = config.PatchFilePath;
            _useModPackage = config.UseModPackage;
            _modPackagePath = config.ModPackagePath;
            _useModPackageLoadPlanMerge = config.UseModPackageLoadPlanMerge;
            _showCatalog = config.ShowCatalog;
            _useModPackageLoadout = config.UseModPackageLoadout;
            _loadoutFilePath = config.LoadoutFilePath;
            _writeModDiagnosticSnapshot = config.WriteModDiagnosticSnapshot;
            _modDiagnosticSnapshotFileName = config.ModDiagnosticSnapshotFileName;
            _initialHp = config.InitialHp;
            _initialAttack = config.InitialAttack;
            _initialDefense = config.InitialDefense;
            InitializeTarget();
        }

        public void ConfigureHudAssets(PanelSettings panelSettings, VisualTreeAsset visualTree, StyleSheet styleSheet, Font font = null)
        {
            _hudPanelSettings = panelSettings;
            _hudVisualTree = visualTree;
            _hudStyleSheet = styleSheet;
            if (font != null)
                _hudFont = font;

            MxRuntimeHudController hud = GetComponent<MxRuntimeHudController>();
            if (hud != null)
                hud.ConfigureAssets(_hudPanelSettings, _hudVisualTree, _hudStyleSheet, _hudFont);
        }

        private void InitializeTarget()
        {
            _target = new RuntimeSliceTarget();
            _target.Store.RegisterAttribute(Const.AttrHp, _initialHp);
            _target.Store.RegisterAttribute(Const.AttrAttack, _initialAttack);
            _target.Store.RegisterAttribute(Const.AttrDefense, _initialDefense);
            _target.Store.OnAttributeChanged.Subscribe(OnAttributeChanged);
        }

        private void Start()
        {
            WarmupRuntimeResources();

            if (_useAbilitySlice)
            {
                StartAbilitySlice();
                return;
            }

            if (_showCatalog)
            {
                StartCatalogDemo();
                return;
            }

            if (_useModPackageLoadPlanMerge || _useModPackage || _usePatchFile || _useConfigDriven)
                StartConfigDriven();
            else
                StartHardcoded();
        }

        private void OnDestroy()
        {
            ReleaseRuntimeResources();
        }

        private void StartAbilitySlice()
        {
            var abilityRunner = GetComponent<RuntimeAbilitySliceRunner>();
            if (abilityRunner == null)
                abilityRunner = gameObject.AddComponent<RuntimeAbilitySliceRunner>();

            abilityRunner.UseConfigDriven = _useConfigDrivenAbility;
            abilityRunner.ShowLegacyOnGui = _showLegacyOnGui && !_enableShowcaseUi;
            AppendResourceTestEvents(abilityRunner);

            if (!_enableShowcaseUi)
                return;

            MxRuntimeHudController hud = GetComponent<MxRuntimeHudController>();
            if (hud == null)
                hud = gameObject.AddComponent<MxRuntimeHudController>();
            hud.ConfigureAssets(_hudPanelSettings, _hudVisualTree, _hudStyleSheet, _hudFont);
            if (GetComponent<RuntimeAbilitySliceShowcaseUi>() == null)
                gameObject.AddComponent<RuntimeAbilitySliceShowcaseUi>();
        }

        private void WarmupRuntimeResources()
        {
            _resourceTestLines.Clear();
            if (!_useResourceCatalogWarmup)
                return;

            try
            {
                _resourceSampleTest?.Release();
                _resourceSampleTest = new RuntimeVerticalSliceSampleResourceTest();
                RuntimeVerticalSliceSampleResourceTestResult result = _resourceSampleTest.Run();
                _resourceWarmupSummary = result.Summary;
                LogEvent(_resourceWarmupSummary);
                for (int i = 0; i < result.LogLines.Count; i++)
                {
                    _resourceTestLines.Add(result.LogLines[i]);
                    LogEvent(result.LogLines[i]);
                }

                if (!result.Success && !string.IsNullOrEmpty(result.FailureMessage))
                {
                    _resourceTestLines.Add(result.FailureMessage);
                    LogEvent(result.FailureMessage);
                }

                RunPlayerResourceSmoke();
            }
            catch (System.Exception ex)
            {
                _resourceWarmupSummary = "Samples resource test error: " + ex.Message;
                LogEvent(_resourceWarmupSummary);
            }
        }

        private void RunPlayerResourceSmoke()
        {
            _playerResourceTest?.Release();
            _playerResourceTest = new RuntimeVerticalSlicePlayerResourceTest();
            RuntimeVerticalSlicePlayerResourceTestResult result = _playerResourceTest.Run();
            _resourceWarmupSummary += " | " + result.Summary;
            LogEvent(result.Summary);

            for (int i = 0; i < result.LogLines.Count; i++)
            {
                _resourceTestLines.Add(result.LogLines[i]);
                LogEvent(result.LogLines[i]);
            }

            if (!result.Success && !string.IsNullOrEmpty(result.FailureMessage))
            {
                _resourceTestLines.Add(result.FailureMessage);
                LogEvent(result.FailureMessage);
            }
        }

        private void ReleaseRuntimeResources()
        {
            _resourceSampleTest?.Release();
            _resourceSampleTest = null;
            _playerResourceTest?.Release();
            _playerResourceTest = null;
        }

        private void AppendResourceTestEvents(RuntimeAbilitySliceRunner abilityRunner)
        {
            if (abilityRunner == null)
                return;

            if (!string.IsNullOrEmpty(_resourceWarmupSummary))
                abilityRunner.AppendExternalEvent(_resourceWarmupSummary);

            for (int i = 0; i < _resourceTestLines.Count; i++)
                abilityRunner.AppendExternalEvent(_resourceTestLines[i]);
        }

        private void StartHardcoded()
        {
            LogEvent("=== Hardcoded Slice ===");
            LogEvent("HP=" + _initialHp + " Attack=" + _initialAttack + " Defense=" + _initialDefense);
            var attackMod = new FlatAddModifier(Const.ModAttackBoost, Const.AttrAttack, 50);
            _target.Store.AddModifier(attackMod);
            int atkAfter = _target.Store.GetAttribute(Const.AttrAttack);
            LogEvent("Direct Modifier " + Const.ModAttackBoost + ": Attack " + _initialAttack + " -> " + atkAfter);
            var healMod = new ModifierBase(Const.ModHeal, new IModifierCondition[] { new AlwaysCondition() }, new IModifierEffect[] { new HealEffect(20) });
            _target.Modifiers.AddModifier(healMod);
            ApplyPipeline();
            int hpAfterHeal = _target.Store.GetAttribute(Const.AttrHp);
            LogEvent("ModifierPipeline Heal: HP " + _initialHp + " -> " + hpAfterHeal);
            LogEvent("Legacy OOP Buff demo removed; component Buff v2 is covered by CharacterTest.");
        }

        private void StartConfigDriven()
        {
            LogEvent("=== Config Driven Slice ===");
            _configRegistry = RuntimeConfigSliceDemoData.CreateRegistry();
            _configReady = true;
            LogEvent("Config registry ready");
            BuildAndRunFromRegistry();
        }

        private void StartCatalogDemo()
        {
            LogEvent("=== Catalog / LoadPlan Demo ===");
            _catalogSummary = "Catalog display retained; Buff patch loading is retired.";
            LogEvent(_catalogSummary);
        }

        public bool ReloadPatchFile()
        {
            string fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, _patchFilePath);
            var service = new RuntimeConfigPatchHotReloadService(RuntimeConfigSliceDemoData.CreateRegistry());
            RuntimeConfigHotReloadResult result = service.Reload(new RuntimeConfigHotReloadRequest(fullPath, _patchFilePath));
            _lastHotReloadResult = result;

            if (!result.Success)
            {
                _patchError = result.ErrorSummary;
                LogEvent("HOT RELOAD ERROR: " + _patchError);
                return false;
            }

            _patchError = string.Empty;
            _patchSourceInfo = result.SourceName + " (HotReload)";
            _configRegistry = result.Provider;
            _changeSet = result.ChangeSet;
            _configReady = true;
            LogEvent("Hot reload applied: " + result.ChangedTables.Count + " changed tables hash=" + result.ContentHash);
            BuildAndRunFromRegistry();
            return true;
        }

        private void BuildAndRunFromRegistry()
        {
            var modFactory = new ConfigModifierFactory<BasicModifierConfig>(_configRegistry);
            _target.Modifiers = new ModifierPipeline(_target.Store, factory: modFactory);

            bool modCreated = _target.Modifiers.TryAddModifier(Const.ModAttackBoost, out _);
            if (modCreated)
            {
                LogEvent("Modifier " + Const.ModAttackBoost + " created from config");
                ApplyPipeline();
                int atkAfter = _target.Store.GetAttribute(Const.AttrAttack);
                LogEvent("Config Modifier: Attack " + _initialAttack + " -> " + atkAfter);
            }
            else
            {
                LogEvent("Modifier " + Const.ModAttackBoost + " creation FAILED");
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            ModifierContext ctx = ModifierContext.Get();
            ctx.Target = _target.Store;
            ctx.Counters = _target.Modifiers.Counters;
            _target.Modifiers.UpdateAll(Time.deltaTime, ctx);
            ModifierContext.Push(ctx);
        }

        private void ApplyPipeline()
        {
            ModifierContext ctx = ModifierContext.Get();
            ctx.Target = _target.Store;
            ctx.Counters = _target.Modifiers.Counters;
            _target.Modifiers.ApplyAll(ctx);
            ModifierContext.Push(ctx);
        }

        private void OnAttributeChanged(AttributeChangedEvent e)
        {
            if (e.AttributeId == Const.AttrHp)
            {
                int delta = e.NewValue - e.OldValue;
                if (delta < 0)
                    _totalDamageTaken -= delta;
                LogEvent("Attr HP: " + e.OldValue + " -> " + e.NewValue + " (" + (delta >= 0 ? "+" : string.Empty) + delta + ")");
            }
            else if (e.AttributeId == Const.AttrAttack)
            {
                LogEvent("Attr Attack: " + e.OldValue + " -> " + e.NewValue);
            }
        }

        private void LogEvent(string msg)
        {
            _eventLog.Add("[" + _elapsed.ToString("F1") + "s] " + msg);
            if (_eventLog.Count > 20)
                _eventLog.RemoveAt(0);
        }

        private void OnGUI()
        {
            if (!_showLegacyOnGui || _target == null)
                return;

            float x = 10, y = 10, w = 620, h = 20, gap = 4;
            GUI.Label(new Rect(x, y, w, h), "=== Runtime Slice (Buff v2 migration) ===");
            y += h + gap;
            if (!string.IsNullOrEmpty(_resourceWarmupSummary))
            {
                GUI.Label(new Rect(x, y, w, h), _resourceWarmupSummary);
                y += h + gap;
            }

            GUI.Label(new Rect(x, y, w, h), "HP:   " + _target.Store.GetAttribute(Const.AttrHp));
            y += h + gap;
            GUI.Label(new Rect(x, y, w, h), "ATK:  " + _target.Store.GetAttribute(Const.AttrAttack));
            y += h + gap;
            GUI.Label(new Rect(x, y, w, h), "DEF:  " + _target.Store.GetAttribute(Const.AttrDefense));
            y += h + gap;
            GUI.Label(new Rect(x, y, w, h), "Dmg Taken: " + _totalDamageTaken);
            y += h + gap * 2;
            GUI.Label(new Rect(x, y, w, h), "Legacy OOP Buff demo removed. CharacterTest validates Buff v2.");
            y += h + gap;

            if (!string.IsNullOrEmpty(_patchSourceInfo))
            {
                GUI.Label(new Rect(x, y, w, h), "Source: " + _patchSourceInfo);
                y += h + gap;
            }
            if (!string.IsNullOrEmpty(_patchError))
            {
                GUI.Label(new Rect(x, y, w, h), "ERROR: " + _patchError);
                y += h + gap;
            }
            if (!string.IsNullOrEmpty(_mergeSummary) || !string.IsNullOrEmpty(_loadoutSummary) || !string.IsNullOrEmpty(_snapshotSummary) || !string.IsNullOrEmpty(_snapshotFilePath))
                y += h + gap;
            if (_loadoutWarnings.Count > 0 || _orderedPackageKeys.Count > 0 || _skippedPackageKeys.Count > 0)
                y += h + gap;

            for (int i = Mathf.Max(0, _eventLog.Count - 12); i < _eventLog.Count; i++)
            {
                GUI.Label(new Rect(x, y, w, h), _eventLog[i]);
                y += h + gap;
            }
        }
    }
}

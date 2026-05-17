using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.Events;
using MxFramework.Modifiers;
using MxFramework.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    internal sealed class RuntimeSliceTarget : IBuffTarget
    {
        private readonly EventBus<BuffEvent> _buffEvents = new EventBus<BuffEvent>();

        public RuntimeSliceTarget()
        {
            Store = new AttributeStore();
            Buffs = new BuffPipeline();
            Modifiers = new ModifierPipeline(Store, buffs: Buffs, counters: new CounterStore());
        }

        public AttributeStore Store { get; }
        public BuffPipeline Buffs { get; set; }
        public ModifierPipeline Modifiers { get; set; }

        IAttributeOwner IBuffTarget.Attributes => Store;
        IAttributeModifierOwner IBuffTarget.AttributeModifiers => Store;
        IEventBus<BuffEvent> IBuffTarget.BuffEvents => _buffEvents;
        public IEventBus<BuffEvent> BuffEvents => _buffEvents;
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

    internal sealed class BurningBuff : BuffBase
    {
        private const float TickInterval = 1f;
        private float _tickAccumulator;
        public BurningBuff() : base(id: Const.BuffBurning, duration: 5f, maxLayers: 3) { }
        public override void OnTick(float deltaTime, IBuffTarget target)
        {
            base.OnTick(deltaTime, target);
            _tickAccumulator += deltaTime;
            while (_tickAccumulator >= TickInterval)
            {
                _tickAccumulator -= TickInterval;
                target.Attributes.AddAttribute(Const.AttrHp, -35 * CurrentLayers, this);
            }
        }
        public override void OnAttach(IBuffTarget target) { _tickAccumulator = 0f; }
    }

    internal sealed class RageBuff : BuffBase
    {
        private readonly FlatAddModifier _attackMod;
        public RageBuff() : base(id: Const.BuffRage, duration: 3f, maxLayers: 1)
        {
            _attackMod = new FlatAddModifier(Const.ModRageAttack, Const.AttrAttack, 50);
        }
        public override void OnAttach(IBuffTarget target) { target.AttributeModifiers.AddModifier(_attackMod); }
        public override void OnDetach(IBuffTarget target) { target.AttributeModifiers.RemoveModifier(_attackMod.Id); }
    }

    internal sealed class HealEffect : IModifierEffect
    {
        private readonly int _amount;
        public HealEffect(int amount) { _amount = amount; }
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
        public const int BuffBurning = 100001;
        public const int BuffRage = 100002;
        public const int ModAttackBoost = 200001;
        public const int ModRageAttack = 200002;
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
        private string _patchSourceInfo = "";
        private string _patchError = "";
        private string _catalogSummary = "";
        private string _mergeSummary = "";
        private string _loadoutSummary = "";
        private string _snapshotSummary = "";
        private string _snapshotFilePath = "";
        private string _resourceWarmupSummary = string.Empty;
        private RuntimeVerticalSliceSampleResourceTest _resourceSampleTest;
        private RuntimeVerticalSlicePlayerResourceTest _playerResourceTest;
        private readonly List<string> _loadoutWarnings = new List<string>();
        private readonly List<string> _orderedPackageKeys = new List<string>();
        private readonly List<string> _skippedPackageKeys = new List<string>();
        private readonly List<string> _resourceTestLines = new List<string>();

        private readonly List<string> _eventLog = new List<string>();
        private const int MaxEventLog = 20;

        public string ResourceWarmupSummary => _resourceWarmupSummary;
        public IReadOnlyList<string> ResourceBindingLogLines => _resourceTestLines;

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
            _target.BuffEvents.Subscribe(OnBuffEvent);
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

            if (_useModPackageLoadPlanMerge)
                StartModPackageLoadPlanMerge();
            else if (_useModPackage)
                StartModPackageDriven();
            else if (_usePatchFile)
                StartPatchDriven();
            else if (_useConfigDriven)
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

            if (_enableShowcaseUi)
            {
                MxRuntimeHudController hud = GetComponent<MxRuntimeHudController>();
                if (hud == null)
                    hud = gameObject.AddComponent<MxRuntimeHudController>();
                hud.ConfigureAssets(_hudPanelSettings, _hudVisualTree, _hudStyleSheet, _hudFont);
                if (GetComponent<RuntimeAbilitySliceShowcaseUi>() == null)
                    gameObject.AddComponent<RuntimeAbilitySliceShowcaseUi>();
            }
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
            if (_resourceSampleTest != null)
            {
                _resourceSampleTest.Release();
                _resourceSampleTest = null;
            }

            if (_playerResourceTest != null)
            {
                _playerResourceTest.Release();
                _playerResourceTest = null;
            }
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

        private void StartHardcoded() { /* unchanged from previous implementation */ 
            LogEvent("=== Hardcoded Slice ===");
            LogEvent($"HP={_initialHp} Attack={_initialAttack} Defense={_initialDefense}");
            var attackMod = new FlatAddModifier(Const.ModAttackBoost, Const.AttrAttack, 50);
            _target.Store.AddModifier(attackMod);
            int atkAfter = _target.Store.GetAttribute(Const.AttrAttack);
            LogEvent($"Direct Modifier {Const.ModAttackBoost}: Attack {_initialAttack} -> {atkAfter}");
            var healMod = new ModifierBase(Const.ModHeal, new IModifierCondition[] { new AlwaysCondition() }, new IModifierEffect[] { new HealEffect(20) });
            _target.Modifiers.AddModifier(healMod);
            ApplyPipeline();
            int hpAfterHeal = _target.Store.GetAttribute(Const.AttrHp);
            LogEvent($"ModifierPipeline Heal: HP {_initialHp} -> {hpAfterHeal}");
            _target.Buffs.AddBuff(new BurningBuff(), _target);
            LogEvent($"Buff {Const.BuffBurning} Burning added (5s, 35 dmg/s, max 3 layers)");
            _target.Buffs.AddBuff(new RageBuff(), _target);
            int atkWithRage = _target.Store.GetAttribute(Const.AttrAttack);
            LogEvent($"Buff {Const.BuffRage} Rage added: Attack -> {atkWithRage}");
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

            string streamingAssets = Application.streamingAssetsPath + "/MxFramework/Demo";
            var containers = new string[] { streamingAssets };

            RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(containers);
            LogEvent($"Discovered: {catalog.Items.Count} packages");

            for (int i = 0; i < catalog.Items.Count; i++)
            {
                var item = catalog.Items[i];
                string id = item.DisplayIdentity;
                if (item.IsValid)
                    LogEvent($"  [{i}] {id} — valid ({item.Manifest?.Kind ?? "?"}) key={item.PackageKey}");
                else
                    LogEvent($"  [{i}] {id} — INVALID key={item.PackageKey}");
                foreach (var e in item.Errors)
                    LogEvent($"    error: {e}");
                foreach (var w in item.Warnings)
                    LogEvent($"    warn: {w}");
            }

            RuntimeModPackageLoadPlan plan = RuntimeModPackageLoadPlanBuilder.Build(catalog);
            LogEvent($"LoadPlan: {plan.OrderedItems.Count} ordered, {plan.SkippedItems.Count} skipped");
            for (int i = 0; i < plan.OrderedItems.Count; i++)
            {
                var item = plan.OrderedItems[i];
                LogEvent($"  [{i}] {item.DisplayIdentity} ({item.Manifest?.Kind ?? "?"})");
            }
            for (int i = 0; i < plan.SkippedItems.Count; i++)
            {
                var item = plan.SkippedItems[i];
                string reason = item.IsValid ? "disabled" : "invalid";
                LogEvent($"  skip[{i}] {item.DisplayIdentity} — {reason}");
            }

            _catalogSummary = $"Catalog: {catalog.Items.Count} items | " +
                $"LoadPlan: {plan.OrderedItems.Count} ordered, {plan.SkippedItems.Count} skipped";
        }

        private void StartModPackageLoadPlanMerge()
        {
            LogEvent("=== Mod Package LoadPlan Merge Slice ===");
            string container = System.IO.Path.Combine(Application.streamingAssetsPath, "MxFramework/Demo");
            RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
            RuntimeModPackageLoadPlan plan;
            RuntimeModPackageLoadout loadout = null;

            if (_useModPackageLoadout)
            {
                string loadoutPath = System.IO.Path.Combine(Application.streamingAssetsPath, _loadoutFilePath);
                try
                {
                    loadout = RuntimeModPackageLoadoutJson.LoadFromFile(loadoutPath);
                    plan = RuntimeModPackageLoadPlanBuilder.Build(catalog, loadout);
                    _loadoutSummary = $"profile={loadout.ProfileId}, enabledKeys={loadout.EnabledPackageKeys.Count}, planOrdered={plan.OrderedItems.Count}";
                    LogEvent($"Loadout profile: {loadout.ProfileId}");
                }
                catch (System.Exception ex)
                {
                    _patchError = ex.Message;
                    _loadoutSummary = "loadout error";
                    LogEvent($"LOADOUT ERROR: {ex.Message}");
                    return;
                }
            }
            else
            {
                plan = RuntimeModPackageLoadPlanBuilder.Build(catalog);
                _loadoutSummary = "";
            }

            RuntimeModPackageMergeResult mergeResult = RuntimeModPackagePatchMerger.Merge(
                plan,
                RuntimeConfigSliceDemoData.CreateRegistry());
            _loadoutWarnings.Clear();
            _orderedPackageKeys.Clear();
            _skippedPackageKeys.Clear();

            _patchSourceInfo = "LoadPlan Merge (StreamingAssets/MxFramework/Demo)";
            _mergeSummary = $"packages={mergeResult.Report.AppliedPackageCount}, skipped={mergeResult.Report.SkippedPackageCount}, overrides={mergeResult.Report.Overrides.Count}";

            if (!mergeResult.Success)
            {
                _patchError = mergeResult.Report.Errors.Count > 0 ? mergeResult.Report.Errors[0] : "Unknown merge error";
                LogEvent($"MERGE ERROR: {_patchError}");
                BuildAndReportDiagnosticSnapshot(catalog, loadout, plan, mergeResult);
                return;
            }

            _patchError = "";
            _configRegistry = mergeResult.Registry;
            _changeSet = mergeResult.ChangeSet;
            _configReady = true;

            LogEvent("LoadPlan merge success");
            LogEvent(_mergeSummary);
            if (!string.IsNullOrEmpty(_loadoutSummary))
                LogEvent($"Loadout: {_loadoutSummary}");
            for (int i = 0; i < plan.Warnings.Count; i++)
            {
                _loadoutWarnings.Add(plan.Warnings[i]);
                LogEvent($"loadout warn: {plan.Warnings[i]}");
            }
            for (int i = 0; i < plan.OrderedItems.Count; i++)
            {
                _orderedPackageKeys.Add(plan.OrderedItems[i].PackageKey);
                LogEvent($"ordered key: {plan.OrderedItems[i].PackageKey}");
            }
            for (int i = 0; i < plan.SkippedItems.Count; i++)
            {
                _skippedPackageKeys.Add(plan.SkippedItems[i].PackageKey);
                LogEvent($"skipped key: {plan.SkippedItems[i].PackageKey}");
            }
            for (int i = 0; i < mergeResult.Report.Overrides.Count; i++)
            {
                RuntimeModPackageOverrideRecord record = mergeResult.Report.Overrides[i];
                LogEvent($"override: {record.ConfigTypeName}:{record.Id} winner={record.WinnerPackageId}");
            }

            BuildAndReportDiagnosticSnapshot(catalog, loadout, plan, mergeResult);

            BuildAndRunFromRegistry();
        }

        private void BuildAndReportDiagnosticSnapshot(
            RuntimeModPackageCatalog catalog,
            RuntimeModPackageLoadout loadout,
            RuntimeModPackageLoadPlan plan,
            RuntimeModPackageMergeResult mergeResult)
        {
            RuntimeModDiagnosticSnapshot snapshot = RuntimeModDiagnosticSnapshotBuilder.Build(
                catalog,
                loadout,
                plan,
                mergeResult,
                includeAbsolutePaths: true);

            _snapshotSummary = $"success={snapshot.Success}, discovered={snapshot.Summary.Discovered}, " +
                $"ordered={snapshot.Summary.Ordered}, overrides={snapshot.Summary.Overrides}, " +
                $"errors={snapshot.Summary.Errors}, warnings={snapshot.Summary.Warnings}";
            LogEvent($"Snapshot: {_snapshotSummary}");

            if (!_writeModDiagnosticSnapshot)
                return;

            try
            {
                string directory = System.IO.Path.Combine(Application.persistentDataPath, "MxFramework/Diagnostics");
                System.IO.Directory.CreateDirectory(directory);
                _snapshotFilePath = System.IO.Path.Combine(directory, _modDiagnosticSnapshotFileName);
                System.IO.File.WriteAllText(_snapshotFilePath, RuntimeModDiagnosticSnapshotJson.SaveToJson(snapshot));
                LogEvent($"Snapshot written: {_snapshotFilePath}");
            }
            catch (System.Exception ex)
            {
                LogEvent($"Snapshot write failed: {ex.Message}");
            }
        }

        private void StartModPackageDriven()
        {
            LogEvent("=== Mod Package Driven Slice ===");

            string fullPackagePath = System.IO.Path.Combine(Application.streamingAssetsPath, _modPackagePath);
            _patchSourceInfo = $"{_modPackagePath} (ModPackage)";

            RuntimeModPackageLoadResult loadResult;
            try
            {
                loadResult = RuntimeModPackageLoader.LoadFromDirectory(fullPackagePath);
                _patchError = "";
                LogEvent($"Package loaded: {loadResult.Manifest.PackageId} v{loadResult.Manifest.Version} ({loadResult.Manifest.Kind})");
                LogEvent($"  runtimePatch: {loadResult.RuntimePatchFilePath}");
            }
            catch (System.Exception ex)
            {
                _patchError = ex.Message;
                LogEvent($"MOD PACKAGE ERROR: {ex.Message}");
                return;
            }

            BuildRegistryFromPatchBundle(loadResult.PatchBundle);
            _patchSourceInfo += $" / {loadResult.Manifest.PackageId} v{loadResult.Manifest.Version}";
        }

        private void StartPatchDriven()
        {
            LogEvent("=== Config Patch Slice ===");

            // Load patch file from StreamingAssets
            string fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, _patchFilePath);
            _patchSourceInfo = $"{_patchFilePath} (Patch)";

            RuntimeConfigPatchBundle bundle;
            try
            {
                string json = System.IO.File.ReadAllText(fullPath);
                bundle = RuntimeConfigPatchJsonLoader.Load(json);
                _patchError = "";
                LogEvent($"Patch loaded: {bundle.ModifierPatches.Count} modifiers, {bundle.BuffPatches.Count} buffs");
            }
            catch (System.Exception ex)
            {
                _patchError = ex.Message;
                LogEvent($"PATCH ERROR: {ex.Message}");
                return;
            }

            BuildRegistryFromPatchBundle(bundle);
        }

        private void BuildRegistryFromPatchBundle(RuntimeConfigPatchBundle bundle)
        {
            // Build base config
            IConfigProvider baseRegistry = RuntimeConfigSliceDemoData.CreateRegistry();

            // Get base rows for merge
            IReadOnlyCollection<BasicBuffConfig> baseBuffs = baseRegistry.GetAllConfigs<BasicBuffConfig>();
            IReadOnlyCollection<BasicModifierConfig> baseMods = baseRegistry.GetAllConfigs<BasicModifierConfig>();

            var mergedBuffResult = RuntimeConfigPatchMerger.Merge(
                BasicBuffConfig.CreateSchema(),
                baseBuffs,
                bundle.BuffPatches);

            var mergedModResult = RuntimeConfigPatchMerger.Merge(
                BasicModifierConfig.CreateSchema(),
                baseMods,
                bundle.ModifierPatches);

            // Combine change sets
            _changeSet = new ConfigChangeSet();
            foreach (var c in mergedBuffResult.ChangeSet.Changes)
                _changeSet.Add(c);
            foreach (var c in mergedModResult.ChangeSet.Changes)
                _changeSet.Add(c);

            var registry = new ConfigRegistry();
            registry.RegisterProvider<BasicModifierConfig>(mergedModResult.Table);
            registry.RegisterProvider<BasicBuffConfig>(mergedBuffResult.Table);
            _configRegistry = registry;
            _configReady = true;

            LogEvent("Config registry ready (Base + Patch merged)");
            LogEvent($"ChangeSet: {_changeSet.Count} changes");

            BuildAndRunFromRegistry();
        }

        private void BuildAndRunFromRegistry()
        {
            var modFactory = new ConfigModifierFactory<BasicModifierConfig>(_configRegistry);
            _target.Modifiers = new ModifierPipeline(_target.Store, factory: modFactory, buffs: _target.Buffs);
            var buffFactory = new ConfigBuffFactory<BasicBuffConfig>(_configRegistry);
            _target.Buffs = new BuffPipeline(factory: buffFactory);

            bool modCreated = _target.Modifiers.TryAddModifier(Const.ModAttackBoost, out _);
            if (modCreated)
            {
                LogEvent($"Modifier {Const.ModAttackBoost} created from config");
                ApplyPipeline();
                int atkAfter = _target.Store.GetAttribute(Const.AttrAttack);
                LogEvent($"Config Modifier: Attack {_initialAttack} -> {atkAfter}");
            }
            else
            {
                LogEvent($"Modifier {Const.ModAttackBoost} creation FAILED");
            }

            bool buffCreated = _target.Buffs.TryAddBuff(Const.BuffBurning, _target, out IBuff configBuff);
            if (buffCreated)
            {
                LogEvent($"Buff {Const.BuffBurning} created from config (duration={configBuff.Duration}s maxLayers={configBuff.MaxLayers})");
            }
            else
            {
                LogEvent($"Buff {Const.BuffBurning} creation FAILED");
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _elapsed += dt;
            _target.Buffs.TickAll(dt);
            ModifierContext ctx = ModifierContext.Get();
            ctx.Target = _target.Store;
            ctx.Buffs = _target.Buffs;
            ctx.Counters = _target.Modifiers.Counters;
            _target.Modifiers.UpdateAll(dt, ctx);
            ModifierContext.Push(ctx);
        }

        private void ApplyPipeline()
        {
            ModifierContext ctx = ModifierContext.Get();
            ctx.Target = _target.Store;
            ctx.Buffs = _target.Buffs;
            ctx.Counters = _target.Modifiers.Counters;
            _target.Modifiers.ApplyAll(ctx);
            ModifierContext.Push(ctx);
        }

        private void OnAttributeChanged(AttributeChangedEvent e)
        {
            if (e.AttributeId == Const.AttrHp)
            {
                int dmg = e.NewValue - e.OldValue;
                if (dmg < 0) _totalDamageTaken -= dmg;
                LogEvent($"Attr HP: {e.OldValue} -> {e.NewValue} ({(dmg >= 0 ? "+" : "")}{dmg}) source={e.Source?.GetType().Name ?? "?"}");
            }
            else if (e.AttributeId == Const.AttrAttack)
                LogEvent($"Attr Attack: {e.OldValue} -> {e.NewValue} source={e.Source?.GetType().Name ?? "?"}");
        }

        private void OnBuffEvent(BuffEvent e)
        {
            string msg = e.Type switch
            {
                BuffEventType.Added => $"Buff {e.BuffId} added (layers={e.LayerDelta})",
                BuffEventType.Removed => $"Buff {e.BuffId} removed",
                BuffEventType.Tick => $"Buff {e.BuffId} tick",
                BuffEventType.LayerChanged => $"Buff {e.BuffId} layer +{e.LayerDelta}",
                BuffEventType.DurationRefreshed => $"Buff {e.BuffId} duration refreshed",
                _ => $"Buff {e.BuffId} event={e.Type}"
            };
            LogEvent(msg);
        }

        private void LogEvent(string msg)
        {
            _eventLog.Add($"[{_elapsed:F1}s] {msg}");
            if (_eventLog.Count > MaxEventLog)
                _eventLog.RemoveAt(0);
        }

        private void OnGUI()
        {
            if (!_showLegacyOnGui || (_useAbilitySlice && _enableShowcaseUi))
                return;

            float x = 10, y = 10, w = 520, h = 20, gap = 4;
            if (_titleStyle == null)
                _titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            var warnStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.yellow } };

            // Title
            string mode;
            if (_useModPackageLoadPlanMerge) mode = "LoadPlan Merge Driven";
            else if (_useModPackage) mode = "Mod Package Driven";
            else if (_usePatchFile) mode = "Patch Driven";
            else if (_useConfigDriven) mode = "Config Driven";
            else mode = "Hardcoded";
            GUI.Label(new Rect(x, y, w, h), $"=== Runtime {mode} Slice ===", _titleStyle);
            y += h + gap;

            // Catalog summary
            if (_showCatalog && !string.IsNullOrEmpty(_catalogSummary))
            {
                GUI.Label(new Rect(x, y, w, h), _catalogSummary);
                y += h + gap;
            }
            if (!string.IsNullOrEmpty(_resourceWarmupSummary))
            {
                GUI.Label(new Rect(x, y, w, h), _resourceWarmupSummary);
                y += h + gap;
            }

            GUI.Label(new Rect(x, y, w, h), $"Elapsed: {_elapsed:F2}s");
            y += h + gap;

            // Patch info
            if (_usePatchFile || _useModPackage || _useModPackageLoadPlanMerge)
            {
                string src = string.IsNullOrEmpty(_patchSourceInfo) ? "(no patch loaded)" : _patchSourceInfo;
                GUI.Label(new Rect(x, y, w, h), $"Source: {src}");
                y += h + gap;
            }
            if (!string.IsNullOrEmpty(_mergeSummary))
            {
                GUI.Label(new Rect(x, y, w, h), $"Merge: {_mergeSummary}");
                y += h + gap;
            }
            if (!string.IsNullOrEmpty(_loadoutSummary))
            {
                GUI.Label(new Rect(x, y, w, h), $"Loadout: {_loadoutSummary}");
                y += h + gap;
            }
            if (!string.IsNullOrEmpty(_snapshotSummary))
            {
                GUI.Label(new Rect(x, y, w, h), $"Snapshot: {_snapshotSummary}");
                y += h + gap;
            }
            if (!string.IsNullOrEmpty(_snapshotFilePath))
            {
                GUI.Label(new Rect(x, y, w, h), $"Snapshot file: {_snapshotFilePath}");
                y += h + gap;
            }
            if (_loadoutWarnings.Count > 0)
            {
                GUI.Label(new Rect(x, y, w, h), $"Loadout warnings: {_loadoutWarnings.Count}", warnStyle);
                y += h + gap;
                for (int i = 0; i < _loadoutWarnings.Count; i++)
                {
                    GUI.Label(new Rect(x, y, w, h), $"  {_loadoutWarnings[i]}", warnStyle);
                    y += h + gap;
                }
            }
            if (_orderedPackageKeys.Count > 0)
            {
                GUI.Label(new Rect(x, y, w, h), $"Ordered package keys: {_orderedPackageKeys.Count}");
                y += h + gap;
                for (int i = 0; i < _orderedPackageKeys.Count; i++)
                {
                    GUI.Label(new Rect(x, y, w, h), $"  {_orderedPackageKeys[i]}");
                    y += h + gap;
                }
            }
            if (_skippedPackageKeys.Count > 0)
            {
                GUI.Label(new Rect(x, y, w, h), $"Skipped package keys: {_skippedPackageKeys.Count}");
                y += h + gap;
                for (int i = 0; i < _skippedPackageKeys.Count; i++)
                {
                    GUI.Label(new Rect(x, y, w, h), $"  {_skippedPackageKeys[i]}");
                    y += h + gap;
                }
            }

            // Patch error
            if (!string.IsNullOrEmpty(_patchError))
            {
                GUI.Label(new Rect(x, y, w, h), $"ERROR: {_patchError}", warnStyle);
                y += h + gap;
            }

            // ChangeSet
            if (_changeSet != null && _changeSet.Count > 0)
            {
                GUI.Label(new Rect(x, y, w, h), "--- ChangeSet ---", _titleStyle);
                y += h + gap;
                for (int i = 0; i < _changeSet.Count; i++)
                {
                    var c = _changeSet.Changes[i];
                    string typeName = c.ConfigType != null ? c.ConfigType.Name : "?";
                    GUI.Label(new Rect(x, y, w, h), $"  {typeName} id={c.Id} {c.ChangeKind} source={c.SourceId}");
                    y += h + gap;
                }
                y += gap;
            }

            // Attributes
            string hpBase = _target.Store.TryGetAttributeValue(Const.AttrHp, out var hpVal) ? hpVal.BaseValue.ToString() : "?";
            string atkBase = _target.Store.TryGetAttributeValue(Const.AttrAttack, out var atkVal) ? atkVal.BaseValue.ToString() : "?";
            GUI.Label(new Rect(x, y, w, h), $"HP:   {_target.Store.GetAttribute(Const.AttrHp)} / {hpBase}");
            y += h + gap;
            GUI.Label(new Rect(x, y, w, h), $"ATK:  {_target.Store.GetAttribute(Const.AttrAttack)} (base={atkBase})");
            y += h + gap;
            GUI.Label(new Rect(x, y, w, h), $"DEF:  {_target.Store.GetAttribute(Const.AttrDefense)}");
            y += h + gap;
            GUI.Label(new Rect(x, y, w, h), $"Dmg Taken: {_totalDamageTaken}");
            y += h + gap * 2;

            // Active Buffs
            BuffSnapshot[] buffs = _target.Buffs.CreateSnapshot();
            GUI.Label(new Rect(x, y, w, h), "--- Active Buffs ---", _titleStyle);
            y += h + gap;
            if (buffs.Length > 0)
            {
                foreach (BuffSnapshot b in buffs)
                {
                    string expire = b.IsPermanent ? "permanent" : $"{b.RemainingTime:F1}s";
                    GUI.Label(new Rect(x, y, w, h), $"  [{b.Id}] layers={b.CurrentLayers}/{b.MaxLayers} remaining={expire}");
                    y += h + gap;
                }
            }
            else
            {
                GUI.Label(new Rect(x, y, w, h), "  (none)");
                y += h + gap;
            }
            y += gap;

            // Event Log
            GUI.Label(new Rect(x, y, w, h), $"--- Event Log (last {_eventLog.Count}) ---", _titleStyle);
            y += h + gap;
            for (int i = Mathf.Max(0, _eventLog.Count - 12); i < _eventLog.Count; i++)
            {
                GUI.Label(new Rect(x, y, w, h), _eventLog[i]);
                y += h + gap;
            }
        }

        private GUIStyle _titleStyle;
    }
}

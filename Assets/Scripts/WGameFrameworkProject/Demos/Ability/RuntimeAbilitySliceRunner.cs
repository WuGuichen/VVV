using System;
using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.DebugUI;
using MxFramework.DebugUI.Adapters;
using MxFramework.Events;
using MxFramework.Gameplay;
using MxFramework.Modifiers;
using MxFramework.Runtime;
using MxFramework.UI.Toolkit;
using UnityEngine;

namespace MxFramework.Demo
{
    internal static class AbilityConst
    {
        public const int AttrHp = 1;
        public const int AttrAttack = 2;
        public const int AttrDefense = 3;

        public const int ModifierManualAttribute = 200001;

        public const int AbilityStrike = 300001;
        public const int AbilityIgnite = 300002;
    }

    internal sealed class AbilityFlatAttributeModifier : IAttributeModifier
    {
        public AbilityFlatAttributeModifier(int id, int attributeId, int value)
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
        public override string ToString() => "FlatAttributeModifier#" + Id;
    }

    internal sealed class AbilityFlatAttributeRuntimeModifier : ModifierBase
    {
        private readonly int _attributeId;
        private readonly int _value;

        public AbilityFlatAttributeRuntimeModifier(int id, int attributeId, int value)
            : base(id, paramIndex: attributeId)
        {
            _attributeId = attributeId;
            _value = value;
        }

        public override void Apply(ModifierContext context)
        {
            if (context?.Target is IAttributeModifierOwner owner)
                owner.AddModifier(new AbilityFlatAttributeModifier(Id, _attributeId, _value));
        }

        public override void Remove(ModifierContext context)
        {
            if (context?.Target is IAttributeModifierOwner owner)
                owner.RemoveModifier(Id);
        }
    }

    [AddComponentMenu("MxFramework/Demo/Runtime Ability Slice Runner")]
    public sealed class RuntimeAbilitySliceRunner : MonoBehaviour,
        IRuntimeAbilitySliceHudCommandTarget,
        IRuntimeSaveStateProvider,
        IRuntimeSaveStateRestorer
    {
        private const int RuntimeCommandSourceManual = 100;
        private const int RuntimeCommandSourceAuto = 101;

        private static class RuntimeAbilityCommandId
        {
            public const int Strike = 1;
            public const int Ignite = 2;
            public const int ApplyAttackModifier = 5;
            public const int ResetDemo = 6;
        }

        private static readonly int[] SnapshotAttributeIds =
        {
            AbilityConst.AttrHp,
            AbilityConst.AttrAttack,
            AbilityConst.AttrDefense
        };

        [Header("Mode")]
        [SerializeField] private bool _useConfigDriven;
        [SerializeField] private bool _showLegacyOnGui;
        [SerializeField] private bool _logConsoleSummary = true;
        [SerializeField] private bool _autoSequenceEnabled = true;
        [SerializeField] private bool _liveTickEnabled = true;
        [SerializeField] private bool _useRuntimeFoundation = true;

        [Header("Player")]
        [SerializeField] private int _playerHp = 1000;
        [SerializeField] private int _playerAttack = 120;
        [SerializeField] private int _playerDefense = 20;

        [Header("Enemy")]
        [SerializeField] private int _enemyHp = 600;
        [SerializeField] private int _enemyAttack = 80;
        [SerializeField] private int _enemyDefense = 10;

        private RuntimeEntity _player;
        private RuntimeEntity _enemy;
        private readonly List<string> _eventLog = new List<string>();
        private readonly List<AbilityEvent> _abilityEvents = new List<AbilityEvent>();
        private readonly List<AttributeChangedEvent> _attributeEvents = new List<AttributeChangedEvent>();
        private readonly GameplayDiagnosticSnapshotBuilder _snapshotBuilder = new GameplayDiagnosticSnapshotBuilder();
        private RuntimeAbilityConfigResolver _abilityConfigResolver;
        private IConfigProvider _abilityConfigProvider;
        private string _abilityConfigSourceName = "RuntimeAbilitySliceDemoData";
        private IAbility _comparisonOldAbility;
        private string _comparisonOldSourceName = "none";
        private string _abilityRebuildSummary = "Rebuild not requested";
        private string _configComparisonSummary = "Load Patch or Mod Package, then compare.";
        private AbilityCastResult _lastCastResult;
        private GameplayDiagnosticSnapshot _lastSnapshot;
        private RuntimeClock _runtimeClock;
        private RuntimeCommandBuffer _runtimeCommandBuffer;
        private RuntimeReplayRecorder _runtimeReplayRecorder;
        private RuntimeSaveState _lastRuntimeSaveState;
        private IReadOnlyList<RuntimeCommand> _lastRuntimeFrameCommands = Array.Empty<RuntimeCommand>();
        private long _lastRuntimeResultHash;
        private string _runtimeFoundationSummary = "RuntimeHost not initialized";
        private float _presentationTime;
        private float _nextActionTime;
        private int _actionStep;
        private bool _actionDone;

        public bool UseConfigDriven
        {
            get => _useConfigDriven;
            set => _useConfigDriven = value;
        }

        public bool ShowLegacyOnGui
        {
            get => _showLegacyOnGui;
            set => _showLegacyOnGui = value;
        }

        public bool LogConsoleSummary
        {
            get => _logConsoleSummary;
            set => _logConsoleSummary = value;
        }

        public bool AutoSequenceEnabled
        {
            get => _autoSequenceEnabled;
            set => SetAutoSequenceEnabled(value);
        }

        public bool LiveTickEnabled
        {
            get => _liveTickEnabled;
            set => SetLiveTickEnabled(value);
        }

        public bool UseRuntimeFoundation
        {
            get => _useRuntimeFoundation;
            set => _useRuntimeFoundation = value;
        }

        public bool IsInitialized => _player != null && _enemy != null;
        public RuntimeEntity Player => _player;
        public RuntimeEntity Enemy => _enemy;
        public int PlayerMaxHp => _playerHp;
        public int EnemyMaxHp => _enemyHp;
        public IReadOnlyList<string> EventLog => _eventLog;
        public AbilityCastResult LastCastResult => _lastCastResult;
        public GameplayDiagnosticSnapshot LastSnapshot => _lastSnapshot;
        public string AbilitySourceText => CreateAbilitySourceText();
        public string ConfigSummary => _abilityConfigResolver != null ? _abilityConfigResolver.CreateSummary() : "source=(none), policy=Hardcoded";
        public string ConfigModeStatus => _useConfigDriven
            ? "Config Driven, source=" + _abilityConfigSourceName
            : "Hardcoded, config resolver idle";
        public string RuntimeConfigChangeSummaryText => _useConfigDriven && _abilityConfigResolver != null
            ? _abilityConfigResolver.ChangeSummary.ToSummaryText()
            : "Hardcoded mode: no RuntimeConfigChangeSummary";
        public RuntimeConfigChangeSummary RuntimeConfigChangeSummary => _useConfigDriven && _abilityConfigResolver != null
            ? _abilityConfigResolver.ChangeSummary
            : null;
        public string AbilityRebuildSummary => _abilityRebuildSummary;
        public string ConfigComparisonSummary => _configComparisonSummary;
        public int AutoSequenceStep => _actionStep;
        public bool IsAutoSequenceComplete => _actionDone;
        public float NextAutoActionTime => _nextActionTime;
        public int PendingRuntimeCommandCount => _runtimeCommandBuffer != null ? _runtimeCommandBuffer.PendingCount : 0;
        public long CurrentRuntimeFrame => _runtimeClock != null ? _runtimeClock.CurrentFrame.Value : 0L;
        public long LastRuntimeResultHash => _lastRuntimeResultHash;
        public string RuntimeFoundationSummary => _runtimeFoundationSummary;
        public RuntimeSaveState LastRuntimeSaveState => _lastRuntimeSaveState;
        public RuntimeReplaySnapshot RuntimeReplaySnapshot => _runtimeReplayRecorder != null ? _runtimeReplayRecorder.CreateSnapshot() : null;

        public FrameworkDebugSourceRegistry CreateDebugSourceRegistry()
        {
            ResetRuntimeFoundationIfNeeded();
            UpdateSnapshot();

            var registry = new FrameworkDebugSourceRegistry();
            registry.Register(new GameplayDiagnosticSnapshotDebugSource(() => _lastSnapshot, "Gameplay"));
            return registry;
        }

        public void AppendExternalEvent(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                LogEvent(message);
        }

        private void Awake()
        {
            ResetDemo();
        }

        private void Start()
        {
            if (_useConfigDriven)
                EnsureConfigResolver();

            LogStartupSummaryToConsole();
            UpdateSnapshot();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _presentationTime += Mathf.Max(0f, deltaTime);
            if (_autoSequenceEnabled && !_actionDone && _presentationTime >= _nextActionTime)
                ExecuteNextAction(_presentationTime);

            if (_useRuntimeFoundation)
                RunRuntimeFrame(deltaTime);
            else
                UpdateSnapshot();
        }

        public void RunRuntimeFrame(float deltaTime)
        {
            if (!EnsureInitialized(nameof(RunRuntimeFrame)))
                return;

            ResetRuntimeFoundationIfNeeded();
            RuntimeFrame frame = _runtimeClock.CurrentFrame;
            IReadOnlyList<RuntimeCommand> commands = _runtimeCommandBuffer.DrainForFrame(frame);
            _lastRuntimeFrameCommands = commands;

            for (int i = 0; i < commands.Count; i++)
                ExecuteRuntimeCommand(commands[i]);

            _lastRuntimeResultHash = ComputeRuntimeHash();
            _runtimeReplayRecorder.RecordFrame(
                frame,
                commands,
                _lastRuntimeResultHash);
            _runtimeClock.Step();
            _runtimeFoundationSummary =
                "RuntimeHost frame=" + frame.Value
                + " commands=" + commands.Count
                + " hash=" + _lastRuntimeResultHash
                + " replayFrames=" + _runtimeReplayRecorder.Count;

            UpdateSnapshot();
        }

        public RuntimeCommandValidationResult EnqueueManualCommand(MxRuntimeHudManualCommand command)
        {
            if (!EnsureInitialized(nameof(EnqueueManualCommand)))
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidPayload,
                    default(RuntimeCommand),
                    RuntimeFrame.Zero,
                    "RuntimeAbilitySliceRunner is not initialized."));
            }

            switch (command)
            {
                case MxRuntimeHudManualCommand.Strike:
                    return EnqueueRuntimeCommand(RuntimeAbilityCommandId.Strike, RuntimeCommandSourceManual, _enemy.EntityId, traceId: "ui.strike");
                case MxRuntimeHudManualCommand.Ignite:
                case MxRuntimeHudManualCommand.ApplyBuff:
                case MxRuntimeHudManualCommand.Tick:
                    return EnqueueRuntimeCommand(RuntimeAbilityCommandId.Ignite, RuntimeCommandSourceManual, _enemy.EntityId, traceId: "ui.ignite-retired-buff");
                case MxRuntimeHudManualCommand.ApplyModifier:
                    return EnqueueRuntimeCommand(RuntimeAbilityCommandId.ApplyAttackModifier, RuntimeCommandSourceManual, _player.EntityId, payload0: 50, traceId: "ui.apply-modifier");
                case MxRuntimeHudManualCommand.Reset:
                    return EnqueueRuntimeCommand(RuntimeAbilityCommandId.ResetDemo, RuntimeCommandSourceManual, 0, traceId: "ui.reset");
                default:
                    return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                        RuntimeCommandErrorCode.UnregisteredCommandId,
                        default(RuntimeCommand),
                        _runtimeCommandBuffer != null ? _runtimeCommandBuffer.CurrentFrame : RuntimeFrame.Zero,
                        "RuntimeAbilitySlice HUD command is not supported: " + command));
            }
        }

        public RuntimeCommandValidationResult EnqueueHudCommand(RuntimeAbilitySliceHudManualCommand command)
        {
            switch (command)
            {
                case RuntimeAbilitySliceHudManualCommand.Strike:
                    return EnqueueManualCommand(MxRuntimeHudManualCommand.Strike);
                case RuntimeAbilitySliceHudManualCommand.Reset:
                    return EnqueueManualCommand(MxRuntimeHudManualCommand.Reset);
                default:
                    return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                        RuntimeCommandErrorCode.InvalidCommandId,
                        default(RuntimeCommand),
                        _runtimeCommandBuffer != null ? _runtimeCommandBuffer.CurrentFrame : RuntimeFrame.Zero,
                        "RuntimeAbilitySlice HUD command is not supported: " + command + "."));
            }
        }

        public void SetAutoSequenceEnabled(bool enabled)
        {
            _autoSequenceEnabled = enabled;
            if (enabled)
            {
                _actionDone = false;
                _nextActionTime = _presentationTime;
            }

            LogEvent(enabled ? "Auto sequence enabled" : "Auto sequence paused");
        }

        public void SetLiveTickEnabled(bool enabled)
        {
            _liveTickEnabled = enabled;
            LogEvent(enabled ? "Live tick enabled" : "Live tick paused");
        }

        public void SetConfigDrivenMode(bool enabled)
        {
            _useConfigDriven = enabled;
            if (enabled)
                EnsureConfigResolver();
            UpdateSnapshot();
        }

        public bool LoadPatchConfig()
        {
            CaptureOldAbilityForComparison("before patch");
            ApplyAbilityConfigProvider(
                RuntimeAbilitySliceDemoData.CreatePatchRegistry(),
                RuntimeAbilitySliceDemoData.CreatePatchChangeSet(),
                "Showcase Patch",
                "Patch config loaded");
            return true;
        }

        public bool LoadModPackageConfig()
        {
            CaptureOldAbilityForComparison("before mod package");
            ApplyAbilityConfigProvider(
                RuntimeAbilitySliceDemoData.CreateModPackageRegistry(),
                RuntimeAbilitySliceDemoData.CreateModPackageChangeSet(),
                "Showcase Mod Package",
                "Mod package config loaded");
            return true;
        }

        public bool RebuildConfiguredAbilities()
        {
            EnsureConfigResolver();
            bool strikeOk = _abilityConfigResolver.TryCreate(AbilityConst.AbilityStrike, out _, out string strikeError);
            bool igniteOk = _abilityConfigResolver.TryCreate(AbilityConst.AbilityIgnite, out _, out string igniteError);
            _abilityRebuildSummary = "Rebuild strike=" + strikeOk + " ignite=" + igniteOk;
            if (!strikeOk)
                LogEvent(strikeError);
            if (!igniteOk)
                LogEvent(igniteError);
            return strikeOk;
        }

        public bool CompareOldAndNewConfig()
        {
            IConfigProvider provider = EnsureAbilityConfigProvider();
            if (!TryCreateAbilityFromProvider(provider, AbilityConst.AbilityStrike, out IAbility newAbility, out string error))
            {
                _configComparisonSummary = "New ability create failed: " + error;
                return false;
            }

            int oldDamage = _comparisonOldAbility != null ? EvaluateStrikeDamage(_comparisonOldAbility) : 0;
            int newDamage = EvaluateStrikeDamage(newAbility);
            _configComparisonSummary =
                "Old object " + _comparisonOldSourceName
                + " damage=" + oldDamage
                + " | New object " + _abilityConfigSourceName
                + " damage=" + newDamage
                + " | old object unchanged";
            LogEvent(_configComparisonSummary);
            return true;
        }

        public bool CastStrike()
        {
            return CastAbility(AbilityConst.AbilityStrike, "Strike").Success;
        }

        public bool ApplyBurningBuffToEnemy(int layers = 1)
        {
            _ = layers;
            LogEvent("Burning Buff demo retired; Buff v2 lives in Gameplay component stores.");
            UpdateSnapshot();
            return false;
        }

        public bool ApplyRageBuffToPlayer()
        {
            LogEvent("Rage Buff demo retired; use component Buff v2 for runtime Buff state.");
            UpdateSnapshot();
            return false;
        }

        public bool ApplyBuffToPlayer(int buffId, int layers = 1)
        {
            _ = buffId;
            _ = layers;
            return ApplyRageBuffToPlayer();
        }

        public bool ApplyBuffToEnemy(int buffId, int layers = 1)
        {
            _ = buffId;
            _ = layers;
            return ApplyBurningBuffToEnemy(layers);
        }

        public void TickBuffs(float seconds = 1f)
        {
            _ = seconds;
            LogEvent("Legacy Buff tick retired.");
            UpdateSnapshot();
        }

        public bool ApplyAttackModifierToPlayer(int value = 50)
        {
            return ApplyModifier(_player, "Player", AbilityConst.ModifierManualAttribute, AbilityConst.AttrAttack, value);
        }

        public void ResetDemo()
        {
            CreateRuntimeEntities();
            _eventLog.Clear();
            _abilityEvents.Clear();
            _attributeEvents.Clear();
            _lastCastResult = default(AbilityCastResult);
            _abilityRebuildSummary = "Rebuild not requested";
            _configComparisonSummary = "Load Patch or Mod Package, then compare.";
            _actionStep = 0;
            _actionDone = false;
            _nextActionTime = _presentationTime;
            ResetRuntimeFoundation(RuntimeFrame.Zero);
            LogStartupEvents();
            UpdateSnapshot();
        }

        public RuntimeSaveStateResult<RuntimeSaveState> SaveRuntimeState()
        {
            RuntimeSaveStateResult<RuntimeSaveState> result = CaptureSaveState();
            if (result.Success)
                _lastRuntimeSaveState = result.Value;
            return result;
        }

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            if (!EnsureInitialized(nameof(CaptureSaveState)))
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "RuntimeAbilitySliceRunner is not initialized."));

            var save = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                "RuntimeAbilitySlice",
                _abilityConfigSourceName,
                string.Empty,
                CurrentRuntimeFrame,
                new[] { CaptureEntity(_player), CaptureEntity(_enemy) },
                Array.Empty<RuntimeCounterSaveState>(),
                Array.Empty<RuntimeModuleSaveState>());
            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(save);
        }

        public RuntimeSaveStateResult<bool> RestoreLastSaveState()
        {
            if (_lastRuntimeSaveState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "No RuntimeAbilitySlice save state has been captured."));
            }

            return RestoreSaveState(_lastRuntimeSaveState);
        }

        public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            if (saveState == null || saveState.Entities.Count < 2)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "RuntimeAbilitySlice save state is missing entity data."));
            }

            CreateRuntimeEntities();
            RestoreEntity(_player, saveState.Entities[0]);
            RestoreEntity(_enemy, saveState.Entities[1]);
            ResetRuntimeFoundation(new RuntimeFrame(saveState.Frame));
            UpdateSnapshot();
            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        public static string FormatBuffsForUi(RuntimeEntity entity)
        {
            _ = entity;
            return "(component Buff v2 diagnostics)";
        }

        private RuntimeCommandValidationResult EnqueueRuntimeCommand(
            int commandId,
            int sourceId,
            int targetId,
            int payload0 = 0,
            int payload1 = 0,
            int payload2 = 0,
            string traceId = "")
        {
            ResetRuntimeFoundationIfNeeded();
            RuntimeFrame frame = _runtimeClock.CurrentFrame;
            var command = new RuntimeCommand(
                frame,
                sourceId,
                commandId,
                targetId,
                payload0,
                payload1,
                payload2,
                new RuntimeTraceIdentity(commandId, sourceId));
            return _runtimeCommandBuffer.Enqueue(command);
        }

        private void ExecuteRuntimeCommand(RuntimeCommand command)
        {
            switch (command.CommandId)
            {
                case RuntimeAbilityCommandId.Strike:
                    CastStrike();
                    break;
                case RuntimeAbilityCommandId.Ignite:
                    LogEvent("Ignite retired: old OOP Buff demo removed.");
                    break;
                case RuntimeAbilityCommandId.ApplyAttackModifier:
                    ApplyAttackModifierToPlayer(command.Payload0 != 0 ? command.Payload0 : 50);
                    break;
                case RuntimeAbilityCommandId.ResetDemo:
                    ResetDemo();
                    break;
            }
        }

        private void ExecuteNextAction(float currentTime)
        {
            if (_actionStep == 0)
            {
                EnqueueRuntimeCommand(RuntimeAbilityCommandId.Strike, RuntimeCommandSourceAuto, _enemy.EntityId, traceId: "auto.strike");
                _nextActionTime = currentTime + 3f;
                _actionStep++;
                return;
            }

            if (_actionStep == 1)
            {
                EnqueueRuntimeCommand(RuntimeAbilityCommandId.Ignite, RuntimeCommandSourceAuto, _enemy.EntityId, traceId: "auto.ignite-retired");
                _nextActionTime = currentTime + 3f;
                _actionStep++;
                return;
            }

            _actionDone = true;
        }

        private AbilityCastResult CastAbility(int abilityId, string abilityName)
        {
            if (!EnsureInitialized(nameof(CastAbility)))
                return AbilityCastResult.Fail("NotInitialized");

            LogEvent("-- Player casts " + abilityName + " on Enemy --");
            var candidates = new IRuntimeEntity[] { _player, _enemy };
            var context = new AbilityContext(_player, candidates);
            IAbility ability = CreateAbility(abilityId);

            _lastCastResult = ability != null ? ability.Cast(context) : AbilityCastResult.Fail("AbilityCreateFailed");
            UpdateSnapshot();
            return _lastCastResult;
        }

        private IAbility CreateAbility(int abilityId)
        {
            if (_useConfigDriven)
                return CreateConfiguredAbility(abilityId);

            switch (abilityId)
            {
                case AbilityConst.AbilityStrike:
                    return CreateStrikeAbility(110);
                case AbilityConst.AbilityIgnite:
                    return CreateStrikeAbility(10);
                default:
                    LogFailureWarning("Unknown ability id=" + abilityId);
                    return null;
            }
        }

        private static IAbility CreateStrikeAbility(int damage)
        {
            return new SimpleAbility(
                AbilityConst.AbilityStrike,
                new SingleEnemyTargetSelector(),
                new IAbilityEffect[]
                {
                    new FixedDamageEffect(AbilityConst.AttrHp, damage)
                });
        }

        private IAbility CreateConfiguredAbility(int abilityId)
        {
            EnsureConfigResolver();

            if (_abilityConfigResolver.TryCreate(abilityId, out IAbility ability, out string error))
                return ability;

            LogEvent("Config ability create failed: id=" + abilityId + " error=" + error);
            Debug.LogWarning("[AbilitySlice] Config ability create failed: id=" + abilityId + " error=" + error);
            return null;
        }

        private bool ApplyModifier(RuntimeEntity target, string label, int modifierId, int attributeId, int value)
        {
            if (!EnsureInitialized(nameof(ApplyModifier)) || target == null)
                return false;

            target.Modifiers.AddModifier(new AbilityFlatAttributeRuntimeModifier(modifierId, attributeId, value));
            ModifierContext ctx = ModifierContext.Get();
            ctx.Target = target.Store;
            ctx.Counters = target.Modifiers.Counters;
            target.Modifiers.ApplyAll(ctx);
            ModifierContext.Push(ctx);
            LogEvent(label + " Modifier applied: id=" + modifierId + " value=" + value);
            UpdateSnapshot();
            return true;
        }

        private void ApplyAbilityConfigProvider(
            IConfigProvider provider,
            ConfigChangeSet changeSet,
            string sourceName,
            string status)
        {
            string previousSourceName = _abilityConfigSourceName;
            _abilityConfigProvider = provider;
            _abilityConfigSourceName = string.IsNullOrEmpty(sourceName) ? "RuntimeAbilitySliceDemoData" : sourceName;
            _abilityConfigResolver = new RuntimeAbilityConfigResolver(
                _abilityConfigProvider,
                _abilityConfigSourceName,
                changeSet,
                previousSourceName);
            _useConfigDriven = true;
            _abilityRebuildSummary = status;
            _configComparisonSummary = "Old Ability captured from " + _comparisonOldSourceName + ". Click Compare Old/New.";
            LogEvent(status);
            LogEvent("RuntimeConfigChangeSummary: " + _abilityConfigResolver.CreateSummary());
            UpdateSnapshot();
        }

        private void EnsureConfigResolver()
        {
            IConfigProvider provider = EnsureAbilityConfigProvider();
            if (_abilityConfigResolver != null)
                return;

            _abilityConfigResolver = new RuntimeAbilityConfigResolver(provider, _abilityConfigSourceName);
        }

        private IConfigProvider EnsureAbilityConfigProvider()
        {
            if (_abilityConfigProvider == null)
            {
                _abilityConfigProvider = RuntimeAbilitySliceDemoData.CreateRegistry();
                _abilityConfigSourceName = "RuntimeAbilitySliceDemoData";
            }

            return _abilityConfigProvider;
        }

        private void CaptureOldAbilityForComparison(string reason)
        {
            IConfigProvider provider = EnsureAbilityConfigProvider();
            if (TryCreateAbilityFromProvider(provider, AbilityConst.AbilityStrike, out IAbility ability, out string error))
            {
                _comparisonOldAbility = ability;
                _comparisonOldSourceName = _abilityConfigSourceName + " (" + reason + ")";
                return;
            }

            _comparisonOldAbility = null;
            _comparisonOldSourceName = "capture failed: " + error;
        }

        private static bool TryCreateAbilityFromProvider(
            IConfigProvider provider,
            int abilityId,
            out IAbility ability,
            out string error)
        {
            var factory = new ConfigAbilityFactory(provider);
            return factory.TryCreate(abilityId, out ability, out error);
        }

        private static int EvaluateStrikeDamage(IAbility ability)
        {
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity enemy = CreateEntity(2, 2, 600, 80, 10);
            ability.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));
            return 600 - enemy.Store.GetAttribute(AbilityConst.AttrHp);
        }

        private void CreateRuntimeEntities()
        {
            _player = CreateEntity(1, 1, _playerHp, _playerAttack, _playerDefense);
            _enemy = CreateEntity(2, 2, _enemyHp, _enemyAttack, _enemyDefense);

            _player.Store.OnAttributeChanged.Subscribe(e =>
            {
                _attributeEvents.Add(e);
                LogAttrEvent("Player", e);
            });
            _enemy.Store.OnAttributeChanged.Subscribe(e =>
            {
                _attributeEvents.Add(e);
                LogAttrEvent("Enemy", e);
            });

            _player.AbilityEvents.Subscribe(e =>
            {
                _abilityEvents.Add(e);
                LogAbilityEvent("Player", e);
            });
            _enemy.AbilityEvents.Subscribe(e =>
            {
                _abilityEvents.Add(e);
                LogAbilityEvent("Enemy", e);
            });
            _player.Modifiers.OnModifierEvent.Subscribe(e => LogModifierEvent("Player", e));
            _enemy.Modifiers.OnModifierEvent.Subscribe(e => LogModifierEvent("Enemy", e));
        }

        private static RuntimeEntity CreateEntity(int entityId, int teamId, int hp, int attack, int defense)
        {
            var entity = new RuntimeEntity(entityId, teamId, AbilityConst.AttrHp);
            entity.Store.RegisterAttribute(AbilityConst.AttrHp, hp);
            entity.Store.RegisterAttribute(AbilityConst.AttrAttack, attack);
            entity.Store.RegisterAttribute(AbilityConst.AttrDefense, defense);
            return entity;
        }

        private RuntimeEntitySaveState CaptureEntity(RuntimeEntity entity)
        {
            return new RuntimeEntitySaveState(
                entity.EntityId,
                entity.EntityId,
                entity.TeamId,
                entity.IsAlive,
                new[]
                {
                    new RuntimeAttributeSaveState(AbilityConst.AttrHp, entity.Store.GetAttribute(AbilityConst.AttrHp), "base"),
                    new RuntimeAttributeSaveState(AbilityConst.AttrAttack, entity.Store.GetAttribute(AbilityConst.AttrAttack), "base"),
                    new RuntimeAttributeSaveState(AbilityConst.AttrDefense, entity.Store.GetAttribute(AbilityConst.AttrDefense), "base")
                },
                Array.Empty<RuntimeModifierSaveState>(),
                Array.Empty<RuntimeAbilitySaveState>(),
                Array.Empty<RuntimeCounterSaveState>());
        }

        private static void RestoreEntity(RuntimeEntity entity, RuntimeEntitySaveState state)
        {
            for (int i = 0; i < state.Attributes.Count; i++)
            {
                RuntimeAttributeSaveState attr = state.Attributes[i];
                entity.Store.SetAttribute(attr.AttributeId, (int)Math.Round(attr.BaseValue), "restore");
            }
        }

        private void ResetRuntimeFoundationIfNeeded()
        {
            if (_runtimeClock == null || _runtimeCommandBuffer == null || _runtimeReplayRecorder == null)
                ResetRuntimeFoundation(RuntimeFrame.Zero);
        }

        private void ResetRuntimeFoundation(RuntimeFrame startFrame)
        {
            _runtimeClock = new RuntimeClock(startFrame);
            _runtimeCommandBuffer = new RuntimeCommandBuffer(new RuntimeAbilityCommandValidator(), startFrame);
            _runtimeReplayRecorder = new RuntimeReplayRecorder(new RuntimeReplayHeader(
                RuntimeSaveState.CurrentSchemaVersion,
                "RuntimeAbilitySlice",
                _abilityConfigSourceName,
                string.Empty,
                startFrame));
            _lastRuntimeFrameCommands = Array.Empty<RuntimeCommand>();
            _lastRuntimeResultHash = ComputeRuntimeHash();
            _runtimeFoundationSummary = "RuntimeHost frame=" + startFrame.Value + " commands=0 hash=" + _lastRuntimeResultHash;
        }

        private long ComputeRuntimeHash()
        {
            unchecked
            {
                long hash = 1469598103934665603L;
                AppendEntityHash(ref hash, _player);
                AppendEntityHash(ref hash, _enemy);
                AppendHash(ref hash, _actionStep);
                AppendHash(ref hash, _actionDone ? 1 : 0);
                return hash;
            }
        }

        private static void AppendEntityHash(ref long hash, RuntimeEntity entity)
        {
            if (entity == null)
            {
                AppendHash(ref hash, 0);
                return;
            }

            AppendHash(ref hash, entity.EntityId);
            AppendHash(ref hash, entity.TeamId);
            AppendHash(ref hash, entity.IsAlive ? 1 : 0);
            AppendHash(ref hash, entity.Store.GetAttribute(AbilityConst.AttrHp));
            AppendHash(ref hash, entity.Store.GetAttribute(AbilityConst.AttrAttack));
            AppendHash(ref hash, entity.Store.GetAttribute(AbilityConst.AttrDefense));

            ModifierSnapshot[] modifiers = entity.Modifiers.CreateSnapshot();
            AppendHash(ref hash, modifiers.Length);
            for (int i = 0; i < modifiers.Length; i++)
            {
                AppendHash(ref hash, modifiers[i].Id);
                AppendHash(ref hash, modifiers[i].ParamIndex);
            }
        }

        private static void AppendHash(ref long hash, long value)
        {
            unchecked
            {
                hash ^= value;
                hash *= 1099511628211L;
            }
        }

        private void LogStartupEvents()
        {
            LogEvent(_useConfigDriven ? "=== Ability Slice Started (Config Driven) ===" : "=== Ability Slice Started (Hardcoded) ===");
            LogEvent("Player: HP=" + _playerHp + " ATK=" + _playerAttack + " DEF=" + _playerDefense + " Team=1");
            LogEvent("Enemy:  HP=" + _enemyHp + " ATK=" + _enemyAttack + " DEF=" + _enemyDefense + " Team=2");
            LogEvent("Actions: Strike -> retired Ignite/Buff demo");
        }

        private bool EnsureInitialized(string operation)
        {
            if (IsInitialized)
                return true;

            LogFailureWarning(operation + " failed: runner is not initialized");
            return false;
        }

        private void UpdateSnapshot()
        {
            _lastSnapshot = _snapshotBuilder.Build(
                "RuntimeAbilitySlice",
                CreateAbilitySourceText(),
                new[] { _player, _enemy },
                SnapshotAttributeIds,
                _lastCastResult,
                _abilityEvents,
                _attributeEvents);
        }

        private string CreateAbilitySourceText()
        {
            if (!_useConfigDriven)
                return "hardcoded SimpleAbility";

            return _abilityConfigResolver != null
                ? "BasicAbilityConfig -> RuntimeAbilityConfigResolver (" + _abilityConfigResolver.CreateSummary() + ")"
                : "BasicAbilityConfig -> RuntimeAbilityConfigResolver";
        }

        private void LogEvent(string message)
        {
            _eventLog.Add(message);
            if (_eventLog.Count > 30)
                _eventLog.RemoveAt(0);
        }

        private void LogStartupSummaryToConsole()
        {
            if (!_logConsoleSummary)
                return;

            string mode = _useConfigDriven ? "Config Driven" : "Hardcoded";
            Debug.Log(
                "[AbilitySlice] Runtime showcase started"
                + "\n  mode: " + mode
                + "\n  player: HP=" + _playerHp + " ATK=" + _playerAttack + " DEF=" + _playerDefense + " Team=1"
                + "\n  enemy: HP=" + _enemyHp + " ATK=" + _enemyAttack + " DEF=" + _enemyDefense + " Team=2"
                + "\n  note: legacy OOP Buff demo has been removed; component Buff v2 is validated by CharacterTest.");
        }

        private static void LogFailureWarning(string message)
        {
            Debug.LogWarning("[AbilitySlice] " + message);
        }

        private void LogAttrEvent(string label, AttributeChangedEvent e)
        {
            LogEvent(label + " Attr#" + e.AttributeId + ": " + e.OldValue + " -> " + e.NewValue + " (delta=" + e.Delta + ")");
        }

        private void LogAbilityEvent(string label, AbilityEvent e)
        {
            string targetInfo = e.Target != null ? " target=Entity#" + e.Target.EntityId : "";
            string failInfo = e.FailureReason != null ? " reason=" + e.FailureReason : "";
            LogEvent(label + " Ability: " + e.Type + " id=" + e.AbilityId + targetInfo + failInfo);
        }

        private void LogModifierEvent(string label, ModifierEvent e)
        {
            LogEvent(label + " Modifier: id=" + e.ModifierId + " type=" + e.Type);
        }

        private sealed class FixedDamageEffect : IAbilityEffect
        {
            private readonly int _hpAttributeId;
            private readonly int _damage;

            public FixedDamageEffect(int hpAttributeId, int damage)
            {
                _hpAttributeId = hpAttributeId;
                _damage = Math.Max(0, damage);
            }

            public void Apply(AbilityContext context, IRuntimeEntity target)
            {
                if (target == null)
                    return;

                target.Attributes.AddAttribute(_hpAttributeId, -_damage, this);
            }
        }

        private sealed class RuntimeAbilityCommandValidator : IRuntimeCommandValidator
        {
            public RuntimeCommandValidationResult Validate(RuntimeCommand command)
            {
                switch (command.CommandId)
                {
                    case RuntimeAbilityCommandId.Strike:
                    case RuntimeAbilityCommandId.Ignite:
                    case RuntimeAbilityCommandId.ApplyAttackModifier:
                    case RuntimeAbilityCommandId.ResetDemo:
                        return RuntimeCommandValidationResult.Accepted(command);
                    default:
                        return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                            RuntimeCommandErrorCode.UnregisteredCommandId,
                            command,
                            RuntimeFrame.Zero,
                            "RuntimeAbilitySlice command id is not registered."));
                }
            }
        }

#pragma warning disable CS8361
        private void OnGUI()
        {
            if (!_showLegacyOnGui || !IsInitialized)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 600, 500));
            GUILayout.Label(_useConfigDriven ? "=== Ability Slice (Config Driven) ===" : "=== Ability Slice (Hardcoded) ===");
            GUILayout.Label("Player: HP=" + _player.Store.GetAttribute(AbilityConst.AttrHp)
                + " ATK=" + _player.Store.GetAttribute(AbilityConst.AttrAttack)
                + " DEF=" + _player.Store.GetAttribute(AbilityConst.AttrDefense));
            GUILayout.Label("Enemy: HP=" + _enemy.Store.GetAttribute(AbilityConst.AttrHp)
                + " ATK=" + _enemy.Store.GetAttribute(AbilityConst.AttrAttack)
                + " DEF=" + _enemy.Store.GetAttribute(AbilityConst.AttrDefense)
                + " Alive=" + _enemy.IsAlive);
            GUILayout.Label("Buffs: component Buff v2 diagnostics are not shown in this legacy demo.");
            GUILayout.Label("Event Log:");
            for (int i = _eventLog.Count - 1; i >= 0; i--)
                GUILayout.Label(_eventLog[i]);
            GUILayout.EndArea();
        }
#pragma warning restore CS8361
    }
}

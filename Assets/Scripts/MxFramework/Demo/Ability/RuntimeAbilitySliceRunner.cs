using System;
using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.Gameplay;
using MxFramework.Events;
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
        public const int BuffBurning = 100001;
        public const int BuffRage = 100002;

        public const int ModifierManualAttribute = 200001;

        public const int AbilityStrike = 300001;
        public const int AbilityIgnite = 300002;
    }

    /// <summary>
    /// Burning buff for the ability slice: ticks 35 * layers damage per second for 5s.
    /// </summary>
    internal sealed class AbilityBurningBuff : BuffBase
    {
        private const float TickInterval = 1f;
        private readonly float? _remainingTimeOverride;
        private float _tickAccumulator;

        public AbilityBurningBuff(int layers = 1)
            : this(layers, null)
        {
        }

        public AbilityBurningBuff(int layers, float? remainingTime)
            : base(id: AbilityConst.BuffBurning, duration: 5f, maxLayers: 3)
        {
            _remainingTimeOverride = remainingTime;
            for (int i = 1; i < layers; i++)
                AddLayer(1);
        }

        public override void OnTick(float deltaTime, IBuffTarget target)
        {
            base.OnTick(deltaTime, target);
            _tickAccumulator += deltaTime;
            while (_tickAccumulator >= TickInterval)
            {
                _tickAccumulator -= TickInterval;
                target.Attributes.AddAttribute(AbilityConst.AttrHp, -35 * CurrentLayers, this);
            }
        }

        public override void OnAttach(IBuffTarget target)
        {
            if (_remainingTimeOverride.HasValue)
                RestoreRemainingTime(_remainingTimeOverride.Value, target);
            _tickAccumulator = 0f;
        }

        private void RestoreRemainingTime(float remainingTime, IBuffTarget target)
        {
            float elapsed = Mathf.Max(0f, Duration - Mathf.Clamp(remainingTime, 0f, Duration));
            if (elapsed > 0f)
                base.OnTick(elapsed, target);
        }
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
        public override string ToString() => $"FlatAttributeModifier#{Id}";
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

        public override string ToString() => $"RuntimeFlatAttributeModifier#{Id}";
    }

    internal sealed class AbilityRageBuff : BuffBase
    {
        private const int RageAttackBonus = 50;
        private readonly AbilityFlatAttributeModifier _attackModifier;
        private readonly float? _remainingTimeOverride;

        public AbilityRageBuff()
            : this(null)
        {
        }

        public AbilityRageBuff(float? remainingTime)
            : base(id: AbilityConst.BuffRage, duration: 3f, maxLayers: 1)
        {
            _remainingTimeOverride = remainingTime;
            _attackModifier = new AbilityFlatAttributeModifier(AbilityConst.BuffRage, AbilityConst.AttrAttack, RageAttackBonus);
        }

        public override void OnAttach(IBuffTarget target)
        {
            if (_remainingTimeOverride.HasValue)
            {
                float elapsed = Mathf.Max(0f, Duration - Mathf.Clamp(_remainingTimeOverride.Value, 0f, Duration));
                if (elapsed > 0f)
                    base.OnTick(elapsed, target);
            }
            target.AttributeModifiers.AddModifier(_attackModifier);
        }

        public override void OnDetach(IBuffTarget target)
        {
            target.AttributeModifiers.RemoveModifier(_attackModifier.Id);
        }
    }

    [AddComponentMenu("MxFramework/Demo/Runtime Ability Slice Runner")]
    public sealed class RuntimeAbilitySliceRunner : MonoBehaviour
        , IRuntimeSaveStateProvider
        , IRuntimeSaveStateRestorer
    {
        private const int RuntimeCommandSourceManual = 100;
        private const int RuntimeCommandSourceAuto = 101;

        private static class RuntimeAbilityCommandId
        {
            public const int Strike = 1;
            public const int Ignite = 2;
            public const int ApplyBurningBuff = 3;
            public const int TickBuffs = 4;
            public const int ApplyAttackModifier = 5;
            public const int ResetDemo = 6;
        }

        private static class RuntimeSaveCounterId
        {
            public const int ActionStep = 1;
            public const int ActionDone = 2;
            public const int UseConfigDriven = 3;
            public const int AutoSequenceEnabled = 4;
            public const int LiveTickEnabled = 5;
        }

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
        private static readonly int[] SnapshotAttributeIds =
        {
            AbilityConst.AttrHp,
            AbilityConst.AttrAttack,
            AbilityConst.AttrDefense
        };

        private const int MaxLog = 30;

        private float _nextActionTime;
        private int _actionStep;
        private bool _actionDone;
        private RuntimeAbilityConfigResolver _abilityConfigResolver;
        private IConfigProvider _abilityConfigProvider;
        private string _abilityConfigSourceName = "RuntimeAbilitySliceDemoData";
        private IAbility _comparisonOldAbility;
        private string _comparisonOldSourceName = "none";
        private string _abilityRebuildSummary = "Rebuild not requested";
        private string _configComparisonSummary = "Load Patch or Mod Package, then compare.";
        private AbilityCastResult _lastCastResult;
        private GameplayDiagnosticSnapshot _lastSnapshot;
        private RuntimeHost _runtimeHost;
        private RuntimeClock _runtimeClock;
        private RuntimeCommandBuffer _runtimeCommandBuffer;
        private RuntimeReplayRecorder _runtimeReplayRecorder;
        private RuntimeSaveState _lastRuntimeSaveState;
        private IReadOnlyList<RuntimeCommand> _lastRuntimeFrameCommands = new List<RuntimeCommand>(0);
        private long _lastRuntimeResultHash;
        private string _runtimeFoundationSummary = "RuntimeHost not initialized";

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

        public void AppendExternalEvent(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            LogEvent(message);
        }

        private void Awake()
        {
            CreateRuntimeEntities();
            ResetAutoSequenceState();
            ResetRuntimeFoundation(RuntimeFrame.Zero);
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

            // Subscribe to buff events
            ((IBuffTarget)_player).BuffEvents.Subscribe(e => LogBuffEvent("Player", e));
            ((IBuffTarget)_enemy).BuffEvents.Subscribe(e => LogBuffEvent("Enemy", e));

            // Subscribe to ability events
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

        private void Start()
        {
            if (_useConfigDriven)
                EnsureConfigResolver();

            LogStartupEvents();
            LogStartupSummaryToConsole();
            UpdateSnapshot();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Scheduled ability casts
            if (_autoSequenceEnabled && !_actionDone && Time.time >= _nextActionTime)
            {
                ExecuteNextAction();
            }

            if (_useRuntimeFoundation)
            {
                RunRuntimeFrame(dt);
                return;
            }

            if (_liveTickEnabled)
            {
                _player.Buffs.TickAll(dt);
                _enemy.Buffs.TickAll(dt);
            }

            UpdateSnapshot();
        }

        private void OnDestroy()
        {
            DisposeRuntimeFoundation();
        }

        public void RunRuntimeFrame(float deltaTime)
        {
            if (!EnsureInitialized(nameof(RunRuntimeFrame)))
                return;

            ResetRuntimeFoundationIfNeeded();
            RuntimeFrame frame = _runtimeClock.CurrentFrame;
            double dt = Math.Max(0d, deltaTime);
            _runtimeHost.Tick(frame.Value, dt, frame.Value);
            _runtimeClock.Step();
            _runtimeFoundationSummary =
                $"RuntimeHost frame={frame.Value} commands={_lastRuntimeFrameCommands.Count} hash={_lastRuntimeResultHash} replayFrames={_runtimeReplayRecorder.Count}";
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
                    return EnqueueRuntimeCommand(RuntimeAbilityCommandId.Strike, RuntimeCommandSourceManual, targetId: _enemy.EntityId, traceId: "ui.strike");
                case MxRuntimeHudManualCommand.Ignite:
                    return EnqueueRuntimeCommand(RuntimeAbilityCommandId.Ignite, RuntimeCommandSourceManual, targetId: _enemy.EntityId, traceId: "ui.ignite");
                case MxRuntimeHudManualCommand.ApplyBuff:
                    return EnqueueRuntimeCommand(RuntimeAbilityCommandId.ApplyBurningBuff, RuntimeCommandSourceManual, targetId: _enemy.EntityId, payload0: 1, traceId: "ui.apply-buff");
                case MxRuntimeHudManualCommand.Tick:
                    return EnqueueRuntimeCommand(RuntimeAbilityCommandId.TickBuffs, RuntimeCommandSourceManual, targetId: 0, payload0: 1000, traceId: "ui.tick");
                case MxRuntimeHudManualCommand.ApplyModifier:
                    return EnqueueRuntimeCommand(RuntimeAbilityCommandId.ApplyAttackModifier, RuntimeCommandSourceManual, targetId: _player.EntityId, payload0: 50, traceId: "ui.apply-modifier");
                case MxRuntimeHudManualCommand.Reset:
                    return EnqueueRuntimeCommand(RuntimeAbilityCommandId.ResetDemo, RuntimeCommandSourceManual, targetId: 0, traceId: "ui.reset");
                default:
                    return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                        RuntimeCommandErrorCode.UnregisteredCommandId,
                        default(RuntimeCommand),
                        _runtimeCommandBuffer != null ? _runtimeCommandBuffer.CurrentFrame : RuntimeFrame.Zero,
                        "HUD command is not a runtime simulation command: " + command));
            }
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
                traceId);
            RuntimeCommandValidationResult result = _runtimeCommandBuffer.Enqueue(command);
            if (result.Success)
                LogEvent($"RuntimeCommand queued: frame={result.Command.Frame.Value} id={result.Command.CommandId} trace={result.Command.TraceId}");
            else
                LogEvent("RuntimeCommand rejected: " + result.Error);

            UpdateSnapshot();
            return result;
        }

        private void ResetRuntimeFoundationIfNeeded()
        {
            if (_runtimeHost == null || _runtimeClock == null || _runtimeCommandBuffer == null || _runtimeReplayRecorder == null)
                ResetRuntimeFoundation(RuntimeFrame.Zero);
        }

        private void ResetRuntimeFoundation(RuntimeFrame startFrame)
        {
            DisposeRuntimeFoundation();

            _runtimeClock = new RuntimeClock(startFrame);
            _runtimeCommandBuffer = new RuntimeCommandBuffer(new RuntimeAbilityCommandValidator(), startFrame);
            _runtimeReplayRecorder = new RuntimeReplayRecorder(new RuntimeReplayHeader(
                schemaVersion: 0,
                frameworkVersion: "MxFramework.RuntimeFoundation",
                configHash: _abilityConfigSourceName,
                resourceCatalogHash: "RuntimeAbilitySlice",
                startFrame: startFrame));
            _lastRuntimeFrameCommands = new List<RuntimeCommand>(0);
            _lastRuntimeResultHash = 0L;

            _runtimeHost = new RuntimeHost(new RuntimeHostOptions
            {
                ErrorPolicy = RuntimeHostErrorPolicy.CollectAndContinue
            });
            _runtimeHost.RegisterModule(new RuntimeAbilityDelegateModule(
                "demo.ability.commands",
                RuntimeTickStage.PreSimulation,
                0,
                DrainRuntimeCommands));
            _runtimeHost.RegisterModule(new RuntimeAbilityDelegateModule(
                "demo.ability.simulation",
                RuntimeTickStage.Simulation,
                0,
                TickRuntimeSimulation));
            _runtimeHost.RegisterModule(new RuntimeAbilityDelegateModule(
                "demo.ability.diagnostics",
                RuntimeTickStage.Diagnostics,
                0,
                CaptureRuntimeDiagnostics));
            _runtimeHost.Initialize();
            _runtimeHost.Start();
            _runtimeFoundationSummary = $"RuntimeHost ready frame={startFrame.Value}";
        }

        private void DisposeRuntimeFoundation()
        {
            if (_runtimeHost == null)
                return;

            _runtimeHost.Dispose();
            _runtimeHost = null;
        }

        private void DrainRuntimeCommands(RuntimeTickContext context)
        {
            RuntimeFrame frame = new RuntimeFrame(context.FrameIndex);
            IReadOnlyList<RuntimeCommand> commands = _runtimeCommandBuffer.DrainForFrame(frame);
            _lastRuntimeFrameCommands = commands;

            for (int i = 0; i < commands.Count; i++)
                DispatchRuntimeCommand(commands[i]);
        }

        private void TickRuntimeSimulation(RuntimeTickContext context)
        {
            if (!_liveTickEnabled)
                return;

            float dt = (float)Math.Max(0d, context.DeltaTime);
            _player.Buffs.TickAll(dt);
            _enemy.Buffs.TickAll(dt);
        }

        private void CaptureRuntimeDiagnostics(RuntimeTickContext context)
        {
            UpdateSnapshot();
            _lastRuntimeResultHash = ComputeRuntimeResultHash();
            _runtimeReplayRecorder.RecordFrame(
                new RuntimeFrame(context.FrameIndex),
                _lastRuntimeFrameCommands,
                _lastRuntimeResultHash,
                $"events={_eventLog.Count} abilityEvents={_abilityEvents.Count} attributeEvents={_attributeEvents.Count}");
        }

        private void DispatchRuntimeCommand(RuntimeCommand command)
        {
            switch (command.CommandId)
            {
                case RuntimeAbilityCommandId.Strike:
                    CastStrike();
                    break;
                case RuntimeAbilityCommandId.Ignite:
                    CastIgnite();
                    break;
                case RuntimeAbilityCommandId.ApplyBurningBuff:
                    ApplyBurningBuffToEnemy(Mathf.Max(1, command.Payload0));
                    break;
                case RuntimeAbilityCommandId.TickBuffs:
                    TickBuffs(command.Payload0 > 0 ? command.Payload0 / 1000f : 1f);
                    break;
                case RuntimeAbilityCommandId.ApplyAttackModifier:
                    ApplyAttackModifierToPlayer(command.Payload0 != 0 ? command.Payload0 : 50);
                    break;
                case RuntimeAbilityCommandId.ResetDemo:
                    ResetDemoInternal(resetRuntimeFoundation: false);
                    break;
                default:
                    LogEvent("RuntimeCommand ignored: id=" + command.CommandId);
                    break;
            }
        }

        private void CastStrikeFromSchedule()
        {
            if (_useRuntimeFoundation)
                EnqueueRuntimeCommand(RuntimeAbilityCommandId.Strike, RuntimeCommandSourceAuto, _enemy.EntityId, traceId: "auto.strike");
            else
                CastStrike();
        }

        private void CastIgniteFromSchedule()
        {
            if (_useRuntimeFoundation)
                EnqueueRuntimeCommand(RuntimeAbilityCommandId.Ignite, RuntimeCommandSourceAuto, _enemy.EntityId, traceId: "auto.ignite");
            else
                CastIgnite();
        }

        private void ExecuteNextAction()
        {
            switch (_actionStep)
            {
                case 0:
                    CastStrikeFromSchedule();
                    _nextActionTime = Time.time + 3f;
                    break;
                case 1:
                    CastIgniteFromSchedule();
                    _nextActionTime = Time.time + 6f;
                    break;
                case 2:
                    LogEvent("=== Action sequence complete. Observing Burning ticks... ===");
                    _nextActionTime = Time.time + 10f;
                    break;
                case 3:
                    LogEvent("=== Ability Slice Demo Complete ===");
                    _actionDone = true;
                    break;
            }
            _actionStep++;
        }

        public AbilityCastResult CastStrike()
        {
            return CastAbility(AbilityConst.AbilityStrike, "Strike");
        }

        public AbilityCastResult CastIgnite()
        {
            return CastAbility(AbilityConst.AbilityIgnite, "Ignite");
        }

        public bool ApplyBurningBuffToEnemy(int layers = 1)
        {
            return ApplyBuffToEnemy(AbilityConst.BuffBurning, layers);
        }

        public bool ApplyRageBuffToPlayer()
        {
            return ApplyBuffToPlayer(AbilityConst.BuffRage);
        }

        public bool ApplyBuffToPlayer(int buffId, int layers = 1)
        {
            return ApplyBuff(_player, "Player", buffId, layers);
        }

        public bool ApplyBuffToEnemy(int buffId, int layers = 1)
        {
            return ApplyBuff(_enemy, "Enemy", buffId, layers);
        }

        public bool ApplyAttackModifierToPlayer(int value = 50)
        {
            return ApplyModifierToPlayer(AbilityConst.ModifierManualAttribute, AbilityConst.AttrAttack, value);
        }

        public bool ApplyAttackModifierToEnemy(int value = 50)
        {
            return ApplyModifierToEnemy(AbilityConst.ModifierManualAttribute, AbilityConst.AttrAttack, value);
        }

        public bool ApplyModifierToPlayer(int modifierId, int attributeId, int value)
        {
            return ApplyModifier(_player, "Player", modifierId, attributeId, value);
        }

        public bool ApplyModifierToEnemy(int modifierId, int attributeId, int value)
        {
            return ApplyModifier(_enemy, "Enemy", modifierId, attributeId, value);
        }

        public void ApplyPlayerModifiers()
        {
            ApplyModifiers(_player, "Player");
        }

        public void ApplyEnemyModifiers()
        {
            ApplyModifiers(_enemy, "Enemy");
        }

        public void TickBuffs(float seconds = 1f)
        {
            if (!EnsureInitialized(nameof(TickBuffs)))
                return;

            float duration = Mathf.Max(0f, seconds);
            _player.Buffs.TickAll(duration);
            _enemy.Buffs.TickAll(duration);
            LogEvent($"Manual buff tick: {duration:F1}s");
            UpdateSnapshot();
        }

        public void SetLiveTickEnabled(bool enabled)
        {
            if (_liveTickEnabled == enabled)
                return;

            _liveTickEnabled = enabled;
            if (IsInitialized)
            {
                LogEvent(_liveTickEnabled ? "Live buff tick enabled" : "Live buff tick paused");
                UpdateSnapshot();
            }
        }

        public void SetAutoSequenceEnabled(bool enabled)
        {
            if (_autoSequenceEnabled == enabled)
                return;

            _autoSequenceEnabled = enabled;
            if (IsInitialized)
            {
                LogEvent(_autoSequenceEnabled ? "Auto sequence enabled" : "Auto sequence paused");
                UpdateSnapshot();
            }
        }

        public void SetConfigDrivenMode(bool enabled)
        {
            if (_useConfigDriven == enabled)
                return;

            _useConfigDriven = enabled;
            if (_useConfigDriven)
                EnsureConfigResolver();

            LogEvent(_useConfigDriven ? "Mode switched: Config Driven Ability" : "Mode switched: Hardcoded Ability");
            UpdateSnapshot();
        }

        public void LoadPatchConfig()
        {
            if (!EnsureInitialized(nameof(LoadPatchConfig)))
                return;

            CaptureOldAbilityForComparison("before patch");

            IConfigProvider baseProvider = EnsureAbilityConfigProvider();
            var patch = ConfigPatchEntry<BasicAbilityConfig>.Upsert(
                CreateStrikeAbilityConfig(
                    "ability.strike.patch.name",
                    "ability.strike.patch.desc",
                    new[]
                    {
                        AbilityEffectConfig.DamageByAttackDefense(
                            AbilityConst.AttrDefense,
                            AbilityConst.AttrDefense,
                            AbilityConst.AttrHp)
                    }),
                ConfigLayerKind.Patch,
                "showcase.patch.strike-defense");

            ConfigPatchMergeResult<BasicAbilityConfig> merged = RuntimeConfigPatchMerger.Merge(
                BasicAbilityConfig.CreateSchema(),
                baseProvider.GetAllConfigs<BasicAbilityConfig>(),
                new[] { patch });

            ApplyAbilityConfigProvider(
                merged.Table,
                merged.ChangeSet,
                "Showcase Patch: Strike DEF-vs-DEF",
                "Patch loaded: Strike now resolves through patched config. Use Rebuild Ability, then Compare Old/New.");
        }

        public void LoadModPackageConfig()
        {
            if (!EnsureInitialized(nameof(LoadModPackageConfig)))
                return;

            CaptureOldAbilityForComparison("before mod package");

            IConfigProvider baseProvider = EnsureAbilityConfigProvider();
            var patch = ConfigPatchEntry<BasicAbilityConfig>.Upsert(
                CreateStrikeAbilityConfig(
                    "ability.strike.mod.name",
                    "ability.strike.mod.desc",
                    new[]
                    {
                        AbilityEffectConfig.DamageByAttackDefense(
                            AbilityConst.AttrAttack,
                            AbilityConst.AttrDefense,
                            AbilityConst.AttrHp),
                        AbilityEffectConfig.DamageByAttackDefense(
                            AbilityConst.AttrAttack,
                            AbilityConst.AttrDefense,
                            AbilityConst.AttrHp)
                    }),
                ConfigLayerKind.Mod,
                "showcase.mod.double-strike");

            ConfigPatchMergeResult<BasicAbilityConfig> merged = RuntimeConfigPatchMerger.Merge(
                BasicAbilityConfig.CreateSchema(),
                baseProvider.GetAllConfigs<BasicAbilityConfig>(),
                new[] { patch });

            ApplyAbilityConfigProvider(
                merged.Table,
                merged.ChangeSet,
                "Showcase Mod Package: Double Strike",
                "Mod package loaded: Strike has two damage effects. Use Rebuild Ability, then Compare Old/New.");
        }

        public bool RebuildConfiguredAbilities()
        {
            if (!_useConfigDriven)
            {
                _abilityRebuildSummary = "Hardcoded mode: no runtime config resolver to rebuild.";
                LogEvent(_abilityRebuildSummary);
                UpdateSnapshot();
                return false;
            }

            EnsureConfigResolver();
            bool strikeCreated = _abilityConfigResolver.TryCreate(AbilityConst.AbilityStrike, out _, out string strikeError);
            bool igniteCreated = _abilityConfigResolver.TryCreate(AbilityConst.AbilityIgnite, out _, out string igniteError);
            _abilityRebuildSummary = $"Rebuild Ability: Strike={(strikeCreated ? "ok" : "failed")}, Ignite={(igniteCreated ? "ok" : "failed")}";
            if (!strikeCreated)
                _abilityRebuildSummary += ", StrikeError=" + strikeError;
            if (!igniteCreated)
                _abilityRebuildSummary += ", IgniteError=" + igniteError;

            LogEvent(_abilityRebuildSummary);
            UpdateSnapshot();
            return strikeCreated && igniteCreated;
        }

        public bool CompareOldAndNewConfig()
        {
            if (!_useConfigDriven)
            {
                _configComparisonSummary = "Hardcoded mode: load Patch or Mod Package to compare config rebuilds.";
                LogEvent(_configComparisonSummary);
                UpdateSnapshot();
                return false;
            }

            EnsureConfigResolver();
            if (_comparisonOldAbility == null)
                CaptureOldAbilityForComparison("current config");

            if (_comparisonOldAbility == null)
            {
                _configComparisonSummary = "Old/New compare failed: old Ability could not be captured.";
                LogEvent(_configComparisonSummary);
                UpdateSnapshot();
                return false;
            }

            if (!_abilityConfigResolver.TryCreate(AbilityConst.AbilityStrike, out IAbility newAbility, out string error))
            {
                _configComparisonSummary = "Old/New compare failed: " + error;
                LogEvent(_configComparisonSummary);
                UpdateSnapshot();
                return false;
            }

            int oldDamage = EvaluateStrikeDamage(_comparisonOldAbility);
            int newDamage = EvaluateStrikeDamage(newAbility);
            _configComparisonSummary =
                $"Old object ({_comparisonOldSourceName}) damage={oldDamage}; New rebuild ({_abilityConfigSourceName}) damage={newDamage}; old object unchanged.";
            LogEvent(_configComparisonSummary);
            UpdateSnapshot();
            return true;
        }

        public void RestartAutoSequence(bool resetDemoState = false)
        {
            if (resetDemoState)
                ResetDemo();

            ResetAutoSequenceState();
            AutoSequenceEnabled = true;
            LogEvent("=== Auto sequence restarted ===");
            UpdateSnapshot();
        }

        public void ResetDemo()
        {
            ResetDemoInternal(resetRuntimeFoundation: true);
        }

        private void ResetDemoInternal(bool resetRuntimeFoundation)
        {
            _eventLog.Clear();
            _abilityEvents.Clear();
            _attributeEvents.Clear();
            _lastCastResult = default;
            CreateRuntimeEntities();
            ResetAutoSequenceState();
            if (resetRuntimeFoundation)
                ResetRuntimeFoundation(RuntimeFrame.Zero);
            LogEvent("=== Ability Slice Reset ===");
            LogStartupEvents();
            UpdateSnapshot();
        }

        public RuntimeSaveStateResult<RuntimeSaveState> SaveRuntimeState()
        {
            RuntimeSaveStateResult<RuntimeSaveState> result = CaptureSaveState();
            if (result.Success)
            {
                _lastRuntimeSaveState = result.Value;
                LogEvent($"Runtime save captured: frame={result.Value.Frame} entities={result.Value.Entities.Count}");
                UpdateSnapshot();
            }

            return result;
        }

        public RuntimeSaveStateResult<bool> RestoreLastRuntimeSaveState()
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

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            if (!IsInitialized)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "RuntimeAbilitySliceRunner is not initialized."));
            }

            var entities = new List<RuntimeEntitySaveState>
            {
                CaptureEntitySaveState(_player),
                CaptureEntitySaveState(_enemy)
            };
            var counters = new List<RuntimeCounterSaveState>
            {
                new RuntimeCounterSaveState(RuntimeSaveCounterId.ActionStep, _actionStep),
                new RuntimeCounterSaveState(RuntimeSaveCounterId.ActionDone, _actionDone ? 1 : 0),
                new RuntimeCounterSaveState(RuntimeSaveCounterId.UseConfigDriven, _useConfigDriven ? 1 : 0),
                new RuntimeCounterSaveState(RuntimeSaveCounterId.AutoSequenceEnabled, _autoSequenceEnabled ? 1 : 0),
                new RuntimeCounterSaveState(RuntimeSaveCounterId.LiveTickEnabled, _liveTickEnabled ? 1 : 0)
            };
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "showcase", "RuntimeAbilitySlice" },
                { "abilitySource", CreateAbilitySourceText() },
                { "runtimeFoundation", _runtimeFoundationSummary }
            };

            var saveState = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                "MxFramework.RuntimeFoundation",
                _abilityConfigSourceName,
                "RuntimeAbilitySlice",
                _runtimeClock != null ? _runtimeClock.CurrentFrame.Value : 0L,
                entities,
                counters,
                new List<RuntimeModuleSaveState>(0),
                metadata);
            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(saveState);
        }

        public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            if (saveState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Runtime save state is null."));
            }

            if (saveState.SchemaVersion != RuntimeSaveState.CurrentSchemaVersion)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnsupportedVersion,
                    "$.schemaVersion",
                    "Runtime save state schema is not supported.",
                    saveState.SchemaVersion,
                    RuntimeSaveState.CurrentSchemaVersion));
            }

            _useConfigDriven = ReadCounter(saveState.GlobalCounters, RuntimeSaveCounterId.UseConfigDriven, _useConfigDriven ? 1 : 0) != 0;
            _autoSequenceEnabled = ReadCounter(saveState.GlobalCounters, RuntimeSaveCounterId.AutoSequenceEnabled, _autoSequenceEnabled ? 1 : 0) != 0;
            _liveTickEnabled = ReadCounter(saveState.GlobalCounters, RuntimeSaveCounterId.LiveTickEnabled, _liveTickEnabled ? 1 : 0) != 0;
            _actionStep = ReadCounter(saveState.GlobalCounters, RuntimeSaveCounterId.ActionStep, 0);
            _actionDone = ReadCounter(saveState.GlobalCounters, RuntimeSaveCounterId.ActionDone, 0) != 0;
            if (_useConfigDriven)
                EnsureConfigResolver();

            _eventLog.Clear();
            _abilityEvents.Clear();
            _attributeEvents.Clear();
            _lastCastResult = default;
            CreateRuntimeEntities();
            for (int i = 0; i < saveState.Entities.Count; i++)
            {
                RuntimeSaveStateResult<bool> entityResult = RestoreEntitySaveState(saveState.Entities[i]);
                if (!entityResult.Success)
                    return entityResult;
            }

            _lastRuntimeSaveState = saveState;
            ResetRuntimeFoundation(new RuntimeFrame(Math.Max(0L, saveState.Frame)));
            LogEvent($"Runtime save restored: frame={saveState.Frame} entities={saveState.Entities.Count}");
            UpdateSnapshot();
            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private RuntimeEntitySaveState CaptureEntitySaveState(RuntimeEntity entity)
        {
            var attributes = new List<RuntimeAttributeSaveState>(SnapshotAttributeIds.Length);
            for (int i = 0; i < SnapshotAttributeIds.Length; i++)
            {
                int attributeId = SnapshotAttributeIds[i];
                AttributeValue value;
                if (entity.Store.TryGetAttributeValue(attributeId, out value))
                    attributes.Add(new RuntimeAttributeSaveState(attributeId, value.BaseValue, "base"));
            }

            BuffSnapshot[] buffSnapshots = entity.Buffs.CreateSnapshot();
            var buffs = new List<RuntimeBuffSaveState>(buffSnapshots.Length);
            for (int i = 0; i < buffSnapshots.Length; i++)
            {
                ref BuffSnapshot buff = ref buffSnapshots[i];
                buffs.Add(new RuntimeBuffSaveState(
                    buff.Id,
                    instanceId: i + 1,
                    layer: buff.CurrentLayers,
                    remainingTime: buff.RemainingTime,
                    duration: buff.Duration,
                    sourceId: entity.EntityId,
                    configVersion: _abilityConfigSourceName));
            }

            ModifierSnapshot[] modifierSnapshots = entity.Modifiers.CreateSnapshot();
            var modifiers = new List<RuntimeModifierSaveState>(modifierSnapshots.Length);
            for (int i = 0; i < modifierSnapshots.Length; i++)
            {
                ref ModifierSnapshot modifier = ref modifierSnapshots[i];
                modifiers.Add(new RuntimeModifierSaveState(
                    modifier.Id,
                    instanceId: i + 1,
                    sourceId: entity.EntityId,
                    paramIndex: modifier.ParamIndex,
                    counters: new List<RuntimeCounterSaveState>(0)));
            }

            return new RuntimeEntitySaveState(
                entity.EntityId,
                definitionId: entity.EntityId,
                teamId: entity.TeamId,
                isAlive: entity.IsAlive,
                attributes: attributes,
                buffs: buffs,
                modifiers: modifiers,
                abilities: new List<RuntimeAbilitySaveState>(0),
                counters: new List<RuntimeCounterSaveState>(0));
        }

        private RuntimeSaveStateResult<bool> RestoreEntitySaveState(RuntimeEntitySaveState entityState)
        {
            RuntimeEntity entity = FindEntity(entityState.EntityId);
            if (entity == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnknownEntity,
                    "$.entities[" + entityState.EntityId + "]",
                    "RuntimeAbilitySlice save contains an unknown entity."));
            }

            for (int i = 0; i < entityState.Attributes.Count; i++)
            {
                RuntimeAttributeSaveState attribute = entityState.Attributes[i];
                entity.Store.SetAttribute(attribute.AttributeId, (int)Math.Round(attribute.BaseValue), this);
            }

            for (int i = 0; i < entityState.Buffs.Count; i++)
            {
                RuntimeBuffSaveState buffState = entityState.Buffs[i];
                if (buffState.RemainingTime <= 0d && buffState.Duration > 0d)
                    continue;

                IBuff buff = CreateManualBuff(buffState.BuffId, Math.Max(1, buffState.Layer), (float)buffState.RemainingTime);
                if (buff == null)
                {
                    return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.UnknownBuff,
                        "$.entities[" + entityState.EntityId + "].buffs[" + i + "]",
                        "RuntimeAbilitySlice save contains an unknown buff id=" + buffState.BuffId + "."));
                }

                entity.Buffs.AddBuff(buff, entity);
            }

            for (int i = 0; i < entityState.Modifiers.Count; i++)
            {
                RuntimeModifierSaveState modifier = entityState.Modifiers[i];
                if (modifier.ModifierId != AbilityConst.ModifierManualAttribute)
                {
                    return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.UnknownModifier,
                        "$.entities[" + entityState.EntityId + "].modifiers[" + i + "]",
                        "RuntimeAbilitySlice save contains an unknown modifier id=" + modifier.ModifierId + "."));
                }

                entity.Modifiers.AddModifier(new AbilityFlatAttributeRuntimeModifier(
                    modifier.ModifierId,
                    modifier.ParamIndex,
                    50));
                ApplyModifiers(entity, entity.EntityId == _player.EntityId ? "Player" : "Enemy");
            }

            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private RuntimeEntity FindEntity(int entityId)
        {
            if (_player != null && _player.EntityId == entityId)
                return _player;
            if (_enemy != null && _enemy.EntityId == entityId)
                return _enemy;
            return null;
        }

        private static int ReadCounter(IReadOnlyList<RuntimeCounterSaveState> counters, int counterId, int fallback)
        {
            if (counters == null)
                return fallback;

            for (int i = 0; i < counters.Count; i++)
            {
                if (counters[i].CounterId == counterId)
                    return counters[i].Value;
            }

            return fallback;
        }

        private long ComputeRuntimeResultHash()
        {
            unchecked
            {
                long hash = 1469598103934665603L;
                AppendHash(ref hash, _runtimeClock != null ? _runtimeClock.CurrentFrame.Value : 0L);
                AppendEntityHash(ref hash, _player);
                AppendEntityHash(ref hash, _enemy);
                AppendHash(ref hash, _abilityEvents.Count);
                AppendHash(ref hash, _attributeEvents.Count);
                AppendHash(ref hash, _lastCastResult.Success ? 1 : 0);
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

            BuffSnapshot[] buffs = entity.Buffs.CreateSnapshot();
            AppendHash(ref hash, buffs.Length);
            for (int i = 0; i < buffs.Length; i++)
            {
                ref BuffSnapshot buff = ref buffs[i];
                AppendHash(ref hash, buff.Id);
                AppendHash(ref hash, buff.CurrentLayers);
                AppendHash(ref hash, (int)Math.Round(buff.RemainingTime * 1000f));
            }

            ModifierSnapshot[] modifiers = entity.Modifiers.CreateSnapshot();
            AppendHash(ref hash, modifiers.Length);
            for (int i = 0; i < modifiers.Length; i++)
            {
                ref ModifierSnapshot modifier = ref modifiers[i];
                AppendHash(ref hash, modifier.Id);
                AppendHash(ref hash, modifier.ParamIndex);
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

        private AbilityCastResult CastAbility(int abilityId, string abilityName)
        {
            if (!EnsureInitialized(nameof(CastAbility)))
                return AbilityCastResult.Fail("NotInitialized");

            LogEvent($"-- Player casts {abilityName} on Enemy --");
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
                    return new SimpleAbility(
                        AbilityConst.AbilityStrike,
                        new SingleEnemyTargetSelector(),
                        new IAbilityEffect[]
                        {
                            new DamageEffect(AbilityConst.AttrAttack, AbilityConst.AttrDefense, AbilityConst.AttrHp)
                        });
                case AbilityConst.AbilityIgnite:
                    return new SimpleAbility(
                        AbilityConst.AbilityIgnite,
                        new SingleEnemyTargetSelector(),
                        new IAbilityEffect[]
                        {
                            new ApplyBuffEffect(() => new AbilityBurningBuff())
                        });
            }

            LogFailureWarning($"Unknown ability id={abilityId}");
            return null;
        }

        private bool ApplyBuff(RuntimeEntity target, string label, int buffId, int layers)
        {
            if (!EnsureInitialized(nameof(ApplyBuff)) || target == null)
                return false;

            IBuff buff = CreateManualBuff(buffId, layers);
            if (buff == null)
            {
                LogEvent($"{label} Buff apply failed: id={buffId}");
                LogFailureWarning($"Buff apply failed: unknown id={buffId}");
                UpdateSnapshot();
                return false;
            }

            target.Buffs.AddBuff(buff, target);
            LogEvent($"{label} Buff applied: id={buffId} layers={Mathf.Max(1, layers)}");
            UpdateSnapshot();
            return true;
        }

        private bool ApplyModifier(RuntimeEntity target, string label, int modifierId, int attributeId, int value)
        {
            if (!EnsureInitialized(nameof(ApplyModifier)) || target == null)
                return false;

            target.Modifiers.AddModifier(new AbilityFlatAttributeRuntimeModifier(modifierId, attributeId, value));
            ApplyModifiers(target, label);
            LogEvent($"{label} Modifier applied: id={modifierId} attr={FormatAttributeName(attributeId)} value={value}");
            UpdateSnapshot();
            return true;
        }

        private void ApplyModifiers(RuntimeEntity target, string label)
        {
            if (target == null)
                return;

            ModifierContext ctx = ModifierContext.Get();
            ctx.Target = target.Store;
            ctx.Buffs = target.Buffs;
            ctx.Counters = target.Modifiers.Counters;
            target.Modifiers.ApplyAll(ctx);
            ModifierContext.Push(ctx);
            LogEvent($"{label} modifiers applied");
            UpdateSnapshot();
        }

        private static IBuff CreateManualBuff(int buffId, int layers)
        {
            return CreateManualBuff(buffId, layers, null);
        }

        private static IBuff CreateManualBuff(int buffId, int layers, float? remainingTime)
        {
            switch (buffId)
            {
                case AbilityConst.BuffBurning:
                    return new AbilityBurningBuff(Mathf.Max(1, layers), remainingTime);
                case AbilityConst.BuffRage:
                    return new AbilityRageBuff(remainingTime);
                default:
                    return null;
            }
        }

        private IAbility CreateConfiguredAbility(int abilityId)
        {
            EnsureConfigResolver();

            if (_abilityConfigResolver.TryCreate(abilityId, out IAbility ability, out string error))
                return ability;

            LogEvent($"Config ability create failed: id={abilityId} error={error}");
            Debug.LogWarning($"[AbilitySlice] Config ability create failed: id={abilityId} error={error}");
            return null;
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
                new RuntimeAbilitySliceBuffFactory(),
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

            _abilityConfigResolver = new RuntimeAbilityConfigResolver(
                provider,
                new RuntimeAbilitySliceBuffFactory(),
                _abilityConfigSourceName);
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
            var factory = new ConfigAbilityFactory(provider, new RuntimeAbilitySliceBuffFactory());
            return factory.TryCreate(abilityId, out ability, out error);
        }

        private static BasicAbilityConfig CreateStrikeAbilityConfig(
            string nameKey,
            string descriptionKey,
            AbilityEffectConfig[] effects)
        {
            return new BasicAbilityConfig(
                AbilityConst.AbilityStrike,
                new LocalizedTextKey(nameKey),
                new LocalizedTextKey(descriptionKey),
                AbilityTargetSelectorKind.SingleEnemy,
                effects);
        }

        private static int EvaluateStrikeDamage(IAbility ability)
        {
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity enemy = CreateEntity(2, 2, 600, 80, 10);
            ability.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));
            return 600 - enemy.Store.GetAttribute(AbilityConst.AttrHp);
        }

        private void ResetAutoSequenceState()
        {
            _nextActionTime = Time.time;
            _actionStep = 0;
            _actionDone = false;
        }

        private void LogStartupEvents()
        {
            LogEvent(_useConfigDriven ? "=== Ability Slice Started (Config Driven) ===" : "=== Ability Slice Started (Hardcoded) ===");
            LogEvent($"Player: HP={_playerHp} ATK={_playerAttack} DEF={_playerDefense} Team=1");
            LogEvent($"Enemy:  HP={_enemyHp} ATK={_enemyAttack} DEF={_enemyDefense} Team=2");
            LogEvent("Actions: Strike(3s) -> Ignite(6s) -> observe Burning ticks");
            LogEvent(_useConfigDriven ? "Ability source: BasicAbilityConfig -> ConfigAbilityFactory" : "Ability source: hardcoded SimpleAbility");
            if (_useConfigDriven && _abilityConfigResolver != null)
                LogEvent("Ability config: " + _abilityConfigResolver.CreateSummary());
        }

        private bool EnsureInitialized(string operation)
        {
            if (IsInitialized)
                return true;

            LogFailureWarning($"{operation} failed: runner is not initialized");
            return false;
        }

        private static string FormatAttributeName(int attributeId)
        {
            switch (attributeId)
            {
                case AbilityConst.AttrHp:
                    return "HP";
                case AbilityConst.AttrAttack:
                    return "ATK";
                case AbilityConst.AttrDefense:
                    return "DEF";
                default:
                    return $"Attr#{attributeId}";
            }
        }

        private static void LogFailureWarning(string message)
        {
            Debug.LogWarning("[AbilitySlice] " + message);
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

        private static RuntimeEntity CreateEntity(int entityId, int teamId, int hp, int attack, int defense)
        {
            var entity = new RuntimeEntity(entityId, teamId, AbilityConst.AttrHp);
            entity.Store.RegisterAttribute(AbilityConst.AttrHp, hp);
            entity.Store.RegisterAttribute(AbilityConst.AttrAttack, attack);
            entity.Store.RegisterAttribute(AbilityConst.AttrDefense, defense);
            return entity;
        }

        private void LogEvent(string message)
        {
            _eventLog.Add(message);
            if (_eventLog.Count > MaxLog)
                _eventLog.RemoveAt(0);
        }

        private void LogStartupSummaryToConsole()
        {
            if (!_logConsoleSummary)
                return;

            string mode = _useConfigDriven ? "Config Driven" : "Hardcoded";
            string source = _useConfigDriven ? "BasicAbilityConfig -> ConfigAbilityFactory" : "hardcoded SimpleAbility";
            string configSummary = _useConfigDriven && _abilityConfigResolver != null
                ? "\n  config: " + _abilityConfigResolver.CreateSummary()
                : string.Empty;

            Debug.Log(
                "[AbilitySlice] Runtime showcase started"
                + $"\n  mode: {mode}"
                + $"\n  player: HP={_playerHp} ATK={_playerAttack} DEF={_playerDefense} Team=1"
                + $"\n  enemy: HP={_enemyHp} ATK={_enemyAttack} DEF={_enemyDefense} Team=2"
                + "\n  sequence: Strike(3s) -> Ignite(6s) -> Burning ticks"
                + $"\n  source: {source}"
                + configSummary
                + "\n  detail: open the UI Toolkit HUD Event Log for per-event output.");
        }

        private void LogAttrEvent(string label, AttributeChangedEvent e)
        {
            string attrName = FormatAttributeName(e.AttributeId);
            LogEvent($"{label} {attrName}: {e.OldValue} -> {e.NewValue} (delta={e.Delta})");
        }

        private void LogBuffEvent(string label, BuffEvent e)
        {
            LogEvent($"{label} Buff: id={e.BuffId} type={e.Type} layers={e.LayerDelta}");
        }

        private void LogAbilityEvent(string label, AbilityEvent e)
        {
            string targetInfo = e.Target != null ? $" target=Entity#{e.Target.EntityId}" : "";
            string failInfo = e.FailureReason != null ? $" reason={e.FailureReason}" : "";
            LogEvent($"{label} Ability: {e.Type} id={e.AbilityId}{targetInfo}{failInfo}");
        }

        private void LogModifierEvent(string label, ModifierEvent e)
        {
            LogEvent($"{label} Modifier: id={e.ModifierId} type={e.Type}");
        }

        private sealed class RuntimeAbilityDelegateModule : RuntimeModule
        {
            private readonly Action<RuntimeTickContext> _tick;

            public RuntimeAbilityDelegateModule(
                string moduleId,
                RuntimeTickStage tickStage,
                int priority,
                Action<RuntimeTickContext> tick)
                : base(moduleId, tickStage, priority)
            {
                _tick = tick;
            }

            public override void Tick(RuntimeTickContext context)
            {
                if (_tick != null)
                    _tick(context);
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
                    case RuntimeAbilityCommandId.ApplyBurningBuff:
                    case RuntimeAbilityCommandId.TickBuffs:
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
            if (!_showLegacyOnGui)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 600, 800));

            GUILayout.Label(_useConfigDriven ? "<b>=== Ability Slice (Config Driven) ===</b>" : "<b>=== Ability Slice (Hardcoded) ===</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Space(5);

            // Player stats
            GUILayout.Label($"Player (Team 1): HP={_player.Store.GetAttribute(AbilityConst.AttrHp)} " +
                $"ATK={_player.Store.GetAttribute(AbilityConst.AttrAttack)} " +
                $"DEF={_player.Store.GetAttribute(AbilityConst.AttrDefense)} " +
                $"Alive={_player.IsAlive}");
            GUILayout.Label($"  Buffs: {FormatBuffs(_player)}");

            GUILayout.Space(5);

            // Enemy stats
            GUILayout.Label($"Enemy (Team 2): HP={_enemy.Store.GetAttribute(AbilityConst.AttrHp)} " +
                $"ATK={_enemy.Store.GetAttribute(AbilityConst.AttrAttack)} " +
                $"DEF={_enemy.Store.GetAttribute(AbilityConst.AttrDefense)} " +
                $"Alive={_enemy.IsAlive}");
            GUILayout.Label($"  Buffs: {FormatBuffs(_enemy)}");

            GUILayout.Space(10);
            GameplayDiagnosticSnapshot snapshot = _lastSnapshot ?? _snapshotBuilder.Build(
                "RuntimeAbilitySlice",
                CreateAbilitySourceText(),
                new[] { _player, _enemy },
                SnapshotAttributeIds,
                _lastCastResult,
                _abilityEvents,
                _attributeEvents);
            GUILayout.Label($"Snapshot: entities={snapshot.Entities.Count}, abilityEvents={snapshot.AbilityEvents.Count}, attributeEvents={snapshot.AttributeEvents.Count}, source={snapshot.AbilitySource}");
            if (_useConfigDriven && _abilityConfigResolver != null)
                GUILayout.Label($"Ability Config: {_abilityConfigResolver.CreateSummary()}");
            GUILayout.Space(10);
            GUILayout.Label("<b>Event Log:</b>", new GUIStyle(GUI.skin.label) { richText = true });

            for (int i = _eventLog.Count - 1; i >= 0; i--)
                GUILayout.Label(_eventLog[i]);

            GUILayout.EndArea();
        }
#pragma warning restore CS8361

        public static string FormatBuffsForUi(RuntimeEntity entity)
        {
            return FormatBuffs(entity);
        }

        private static string FormatBuffs(RuntimeEntity entity)
        {
            var snapshot = entity.Buffs.CreateSnapshot();
            if (snapshot == null || snapshot.Length == 0)
                return "(none)";

            var parts = new System.Text.StringBuilder();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (i > 0) parts.Append(", ");
                ref var b = ref snapshot[i];
                parts.Append($"#{b.Id}({b.CurrentLayers}L {b.RemainingTime:F1}s)");
            }
            return parts.ToString();
        }
    }
}

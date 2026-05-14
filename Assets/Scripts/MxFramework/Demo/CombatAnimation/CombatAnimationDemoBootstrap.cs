using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Input;
using MxFramework.Runtime;
using MxFramework.Runtime.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.CombatAnimation
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Demo/Combat Animation Demo Bootstrap")]
    public sealed class CombatAnimationDemoBootstrap : MonoBehaviour
    {
        // Combat timelines are authored in action frames; 30 Hz keeps the demo attack durations readable in Play Mode.
        private const float FixedDeltaTime = 1f / 30f;
        private const float MoveSpeed = 3.2f;
        private const int LightDamage = 15;
        private const int HeavyDamage = 30;
        private const int DodgeDamage = 0;
        private static readonly PropertyInfo KeyboardCurrentProperty =
            System.Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem")?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);

        [SerializeField] private DefaultInputService _inputService;
        [SerializeField] private UIDocument _document;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private StyleSheet _styleSheet;
        [SerializeField] private CombatAnimationHudController _hud;
        [SerializeField] private Transform _player;
        [SerializeField] private Transform _dummy;

        private readonly HashSet<WeaponHitOnceKey> _consumedHitOnceKeys = new HashSet<WeaponHitOnceKey>();
        private readonly List<HitResolveResult> _hitResults = new List<HitResolveResult>();
        private readonly List<RuntimeCommand> _bridgeOutputCommands = new List<RuntimeCommand>();
        private readonly List<GameplayRuntimeEvent> _gameplayEvents = new List<GameplayRuntimeEvent>();
        private readonly List<string> _eventLog = new List<string>();
        private readonly CombatAnimationHudModel _hudModel = new CombatAnimationHudModel();

        private RuntimeHost _host;
        private GameplayWorld _gameplayWorld;
        private GameplayComponentWorld _componentWorld;
        private CombatEntityGameplayMap _entityMap;
        private CombatTargetStateProvider _targetStateProvider;
        private GameplayBridgeTargetStateResolver _targetStateResolver;
        private GameplaySystemPipeline _bridgePipeline;
        private GameplaySystemPipeline _attributeCommandPipeline;
        private GameplayEntityId _playerGameplayId;
        private GameplayEntityId _dummyGameplayId;
        private ICombatAnimationContext _animationContext;
        private CombatPhysicsWorld _physicsWorld;
        private CombatActionRegistry _actionRegistry;
        private CombatActionTimelineTraceProvider _traceProvider;
        private CombatAnimationUnityModule _unityModule;
        private CombatDemoPoseSource _poseSource;
        private DemoInputToActionAdapter _inputAdapter;
        private HitResolveSystem _hitResolve;
        private double _elapsed;

        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            InitializeDemo();
        }

        private void Update()
        {
            if (!IsInitialized || _host == null || _inputService == null)
            {
                return;
            }

            long frame = _inputService.Commands.CurrentFrame;
            EnqueueDemoKeyboardCommands(frame);
            _inputAdapter.Tick(frame, _inputService.Snapshot, Time.deltaTime, MoveSpeed);
            SyncPhysicsWorld();
            _host.Tick(frame, FixedDeltaTime, _elapsed);
            _elapsed += FixedDeltaTime;
            ResolveHits();
            RefreshHud();
        }

        private void LateUpdate()
        {
            RefreshHud();
        }

        private void OnDestroy()
        {
            _unityModule?.Shutdown();
            _host?.Dispose();
            _unityModule = null;
            _host = null;
            IsInitialized = false;
        }

        public void ConfigureSceneReferences(
            DefaultInputService inputService,
            UIDocument document,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            CombatAnimationHudController hud,
            Transform player,
            Transform dummy)
        {
            _inputService = inputService;
            _document = document;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
            _hud = hud;
            _player = player;
            _dummy = dummy;
        }

        private void InitializeDemo()
        {
            _inputService = _inputService != null ? _inputService : GetComponent<DefaultInputService>();
            _document = _document != null ? _document : GetComponent<UIDocument>();
            _hud = _hud != null ? _hud : GetComponent<CombatAnimationHudController>();

            _animationContext = new CombatAnimationContext();
            _physicsWorld = new CombatPhysicsWorld();
            _actionRegistry = new CombatActionRegistry();
            _traceProvider = new CombatActionTimelineTraceProvider();
            _poseSource = new CombatDemoPoseSource();
            _hitResolve = new HitResolveSystem();
            _gameplayWorld = new GameplayWorld();
            _componentWorld = new GameplayComponentWorld();
            _entityMap = new CombatEntityGameplayMap();
            _targetStateProvider = new CombatTargetStateProvider();
            _targetStateResolver = new GameplayBridgeTargetStateResolver(_entityMap, _componentWorld, _targetStateProvider);

            RegisterActions(_actionRegistry, _traceProvider);
            ConfigureGameplayBridge();
            ConfigureInitialPoses();
            RegisterPhysicsBodies();

            var options = new RuntimeHostOptions();
            options.Services.Register(_animationContext);
            options.Services.Register(_physicsWorld);
            options.Services.Register(_actionRegistry);
            options.Services.Register<ICombatActionTraceProvider>(_traceProvider);

            _host = new RuntimeHost(options);
            _host.RegisterModule(new CombatActionRuntimeModule());
            _host.RegisterModule(new CombatWeaponTraceRuntimeModule());
            _host.RegisterModule(new CombatAnimationDiagnosticsModule());
            _host.Initialize();
            _host.Start();

            _unityModule = new CombatAnimationUnityModule(_animationContext);
            RegisterUnityDrivers();
            _unityModule.Initialize();

            _inputAdapter = new DemoInputToActionAdapter(
                _inputService != null ? _inputService.Commands : new InputCommandQueue(),
                _animationContext.ActionRunner,
                _poseSource,
                CombatAnimationDemoIds.PlayerEntityId);

            _hud?.ConfigureAssets(_document, _visualTree, _styleSheet);
            IsInitialized = true;
            AddEvent("Combat Animation bridge demo initialized.");
            RefreshHud();
        }

        private void ConfigureGameplayBridge()
        {
            _playerGameplayId = _componentWorld.CreateEntity();
            _dummyGameplayId = _componentWorld.CreateEntity();

            _entityMap.Register(CombatAnimationDemoIds.PlayerEntityId, _playerGameplayId);
            _entityMap.Register(CombatAnimationDemoIds.DummyEntityId, _dummyGameplayId);

            GameplayComponentStore<GameplayIdentityComponent> identityStore = _componentWorld.GetOrCreateStore<GameplayIdentityComponent>();
            identityStore.Set(_playerGameplayId, new GameplayIdentityComponent(CombatAnimationDemoIds.PlayerDefinitionId));
            identityStore.Set(_dummyGameplayId, new GameplayIdentityComponent(CombatAnimationDemoIds.DummyDefinitionId));

            GameplayComponentStore<GameplayTeamComponent> teamStore = _componentWorld.GetOrCreateStore<GameplayTeamComponent>();
            teamStore.Set(_playerGameplayId, new GameplayTeamComponent(CombatAnimationDemoIds.PlayerTeamId));
            teamStore.Set(_dummyGameplayId, new GameplayTeamComponent(CombatAnimationDemoIds.DummyTeamId));

            GameplayComponentStore<GameplayLifecycleComponent> lifecycleStore = _componentWorld.GetOrCreateStore<GameplayLifecycleComponent>();
            lifecycleStore.Set(_playerGameplayId, GameplayLifecycleComponent.Alive);
            lifecycleStore.Set(_dummyGameplayId, GameplayLifecycleComponent.Alive);

            GameplayComponentStore<GameplayStatusComponent> statusStore = _componentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statusStore.Set(_playerGameplayId, new GameplayStatusComponent());
            statusStore.Set(_dummyGameplayId, new GameplayStatusComponent());

            GameplayComponentStore<GameplayAttributeSetComponent> attributeStore = _componentWorld.GetOrCreateStore<GameplayAttributeSetComponent>();
            attributeStore.Set(
                _playerGameplayId,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(
                    CombatAnimationDemoIds.HpAttributeId,
                    CombatAnimationDemoIds.PlayerMaxHp,
                    CombatAnimationDemoIds.PlayerMaxHp)));
            attributeStore.Set(
                _dummyGameplayId,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(
                    CombatAnimationDemoIds.HpAttributeId,
                    CombatAnimationDemoIds.DummyMaxHp,
                    CombatAnimationDemoIds.DummyMaxHp)));

            _bridgePipeline = new GameplaySystemPipeline();
            _bridgePipeline.Add(new CombatActionStateSyncSystem(
                _entityMap,
                _componentWorld,
                QueryCombatActionState));
            _bridgePipeline.Add(new CombatHitApplicationSystem(
                _entityMap,
                _componentWorld,
                () => _hitResults,
                CombatAnimationDemoIds.HpAttributeId,
                _bridgeOutputCommands));

            _attributeCommandPipeline = new GameplaySystemPipeline();
            _attributeCommandPipeline.Add(new GameplayAttributeCommandSystem());

            AddEvent($"Bridge map: player {_playerGameplayId} dummy {_dummyGameplayId}.");
        }

        private void ConfigureInitialPoses()
        {
            Vector3 playerPosition = _player != null ? _player.position : new Vector3(0f, 0.5f, 0f);
            Vector3 dummyPosition = _dummy != null ? _dummy.position : new Vector3(1.8f, 0.5f, 0f);
            _poseSource.SetPose(CombatAnimationDemoIds.PlayerEntityId, playerPosition, Quaternion.LookRotation(Vector3.right, Vector3.up));
            _poseSource.SetPose(CombatAnimationDemoIds.DummyEntityId, dummyPosition, Quaternion.LookRotation(Vector3.left, Vector3.up));
        }

        private void RegisterUnityDrivers()
        {
            DemoCombatAnimatorDriver[] animators = FindObjectsByType<DemoCombatAnimatorDriver>(FindObjectsSortMode.None);
            for (int i = 0; i < animators.Length; i++)
            {
                if (!animators[i].EntityId.IsNone)
                {
                    _unityModule.RegisterDriver(animators[i].EntityId, animators[i]);
                }
            }

            CombatTransformDriver[] transforms = FindObjectsByType<CombatTransformDriver>(FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].SetPoseSource(_poseSource);
                if (_poseSource.TryGetPose(transforms[i].EntityId, out Vector3 position, out Quaternion rotation))
                {
                    transforms[i].TeleportTo(position, rotation);
                }
            }
        }

        private void EnqueueDemoKeyboardCommands(long frame)
        {
            if (_inputService == null || !IsKeyboardKeyPressedThisFrame("kKey"))
            {
                return;
            }

            _inputService.Commands.TryEnqueue(new InputCommand(
                frame,
                sourceId: 23,
                InputIntent.AttackSecondary,
                InputCommandPhase.Pressed,
                traceId: "CombatAnimationDemo.K"), out _);
        }

        private static bool IsKeyboardKeyPressedThisFrame(string keyPropertyName)
        {
            object keyboard = KeyboardCurrentProperty?.GetValue(null);
            if (keyboard == null)
            {
                return false;
            }

            object keyControl = keyboard.GetType().GetProperty(keyPropertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(keyboard);
            object pressed = keyControl?.GetType().GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance)?.GetValue(keyControl);
            return pressed is bool value && value;
        }

        private void SyncPhysicsWorld()
        {
            if (_poseSource.TryGetPose(CombatAnimationDemoIds.PlayerEntityId, out Vector3 playerPosition, out _))
            {
                _physicsWorld.SetBodyPosition(CombatAnimationDemoIds.PlayerBodyId, ToFix(playerPosition));
            }

            if (_poseSource.TryGetPose(CombatAnimationDemoIds.DummyEntityId, out Vector3 dummyPosition, out _))
            {
                _physicsWorld.SetBodyPosition(CombatAnimationDemoIds.DummyBodyId, ToFix(dummyPosition));
            }
        }

        private void ResolveHits()
        {
            _hitResults.Clear();
            _hitResolve.Resolve(_animationContext.LastFrameHitCandidates, _consumedHitOnceKeys, _hitResults, _targetStateResolver);
            ApplyDemoActionDamage(_hitResults);
            TickGameplayBridge(_inputService.Commands.CurrentFrame);

            for (int i = 0; i < _hitResults.Count; i++)
            {
                HitResolveResult result = _hitResults[i];
                string damage = result.IsAcceptedDamage ? $" damage={result.Damage}" : string.Empty;
                AddEvent($"Hit {result.Kind}: action={ActionName(result.ActionId)} target={result.TargetId.Value}{damage}");
            }
        }

        private void TickGameplayBridge(long frame)
        {
            _bridgeOutputCommands.Clear();
            GameplaySystemContext bridgeContext = CreateGameplayContext(frame, Array.Empty<RuntimeCommand>());
            _bridgePipeline.Tick(bridgeContext);

            if (_bridgeOutputCommands.Count > 0)
            {
                GameplaySystemContext commandContext = CreateGameplayContext(frame, _bridgeOutputCommands);
                _attributeCommandPipeline.Tick(commandContext);
            }

            DrainGameplayEvents(frame);
        }

        private GameplaySystemContext CreateGameplayContext(long frame, IReadOnlyList<RuntimeCommand> commands)
        {
            return new GameplaySystemContext(
                new RuntimeFrame(frame),
                FixedDeltaTime,
                _elapsed,
                _gameplayWorld,
                commands,
                _componentWorld.Events,
                new GameplayCommandExecutionState(),
                _componentWorld);
        }

        private void DrainGameplayEvents(long frame)
        {
            _gameplayEvents.Clear();
            _componentWorld.DrainEvents(new RuntimeFrame(frame), _gameplayEvents);
            for (int i = 0; i < _gameplayEvents.Count; i++)
            {
                GameplayRuntimeEvent evt = _gameplayEvents[i];
                if (evt.Type == GameplayRuntimeEventType.ComponentAttributeChanged
                    && evt.AttributeId == CombatAnimationDemoIds.HpAttributeId)
                {
                    string entityName = ResolveGameplayName(evt.ComponentEntityId);
                    AddEvent($"Bridge HP: {entityName} {evt.OldAttributeValue}->{evt.NewAttributeValue} ({evt.AttributeDelta})");
                    continue;
                }

                if (evt.Type == GameplayRuntimeEventType.CommandRejected)
                {
                    AddEvent($"Bridge rejected: {evt.Reason} trace={evt.TraceId}");
                }
            }
        }

        private void ApplyDemoActionDamage(List<HitResolveResult> results)
        {
            for (int i = 0; i < results.Count; i++)
            {
                HitResolveResult result = results[i];
                if (result.Kind != HitResolveKind.Damage || result.Damage > 0)
                {
                    continue;
                }

                int damage = GetDamage(result.ActionId);
                if (damage <= 0)
                {
                    continue;
                }

                results[i] = new HitResolveResult(
                    result.AttackerId,
                    result.TargetId,
                    result.ActionId,
                    result.ActionInstanceId,
                    result.TraceId,
                    result.Frame,
                    result.Kind,
                    damage,
                    result.StaggerFrames,
                    result.Knockback);
            }
        }

        private void RefreshHud()
        {
            if (_hud == null)
            {
                return;
            }

            CombatAnimationSnapshot? snapshot = _animationContext?.LastSnapshot;
            CombatActionStateComponent actionState = ReadActionState(_playerGameplayId);
            _hudModel.PlayerAction = actionState.IsActive ? ActionName(actionState.ActionId) : "Idle";
            _hudModel.PlayerPhase = actionState.IsActive ? actionState.Phase.ToString() : "None";
            _hudModel.PlayerLocalFrame = actionState.IsActive ? actionState.LocalFrame.ToString() : "-";
            _hudModel.PlayerHp = FormatHp(_playerGameplayId, CombatAnimationDemoIds.PlayerMaxHp);
            _hudModel.DummyHp = FormatHp(_dummyGameplayId, CombatAnimationDemoIds.DummyMaxHp);
            _hudModel.WeaponTrace = snapshot.HasValue
                ? $"active={snapshot.Value.ActivePhaseCount} candidates={snapshot.Value.HitCandidateCount} frame={snapshot.Value.FrameIndex}"
                : "waiting";
            _hudModel.Instructions = "WASD move | J light | K heavy | Space dodge";
            _hudModel.RecentEvents = _eventLog;
            _hud.Refresh(_hudModel);
        }

        private CombatActionState? QueryCombatActionState(CombatEntityId combatId)
        {
            if (_animationContext == null)
            {
                return null;
            }

            return _animationContext.ActionRunner.GetActionState(combatId);
        }

        private CombatActionStateComponent ReadActionState(GameplayEntityId entityId)
        {
            if (_componentWorld != null
                && _componentWorld.TryGetStore(out GameplayComponentStore<CombatActionStateComponent> store)
                && store.TryGet(entityId, out CombatActionStateComponent component))
            {
                return component;
            }

            return CombatActionStateComponent.Inactive();
        }

        private string FormatHp(GameplayEntityId entityId, int maxValue)
        {
            int currentValue = 0;
            if (_componentWorld != null
                && _componentWorld.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store)
                && store.TryGet(entityId, out GameplayAttributeSetComponent attributes))
            {
                currentValue = attributes.GetCurrentValueOrDefault(CombatAnimationDemoIds.HpAttributeId);
            }

            return $"{currentValue}/{maxValue}";
        }

        private void RegisterPhysicsBodies()
        {
            _physicsWorld.UpsertBody(new CombatPhysicsBody(CombatAnimationDemoIds.PlayerEntityId, CombatAnimationDemoIds.PlayerBodyId, FixVector3.Zero));
            _physicsWorld.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                CombatAnimationDemoIds.PlayerBodyId,
                CombatAnimationDemoIds.HurtboxColliderId,
                CombatAnimationDemoIds.HurtboxLayer,
                new FixVector3(Fix64.FromRatio(-4, 10), Fix64.Zero, Fix64.FromRatio(-4, 10)),
                new FixVector3(Fix64.FromRatio(4, 10), Fix64.FromRatio(18, 10), Fix64.FromRatio(4, 10))));

            _physicsWorld.UpsertBody(new CombatPhysicsBody(CombatAnimationDemoIds.DummyEntityId, CombatAnimationDemoIds.DummyBodyId, FixVector3.Zero));
            _physicsWorld.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                CombatAnimationDemoIds.DummyBodyId,
                CombatAnimationDemoIds.HurtboxColliderId,
                CombatAnimationDemoIds.HurtboxLayer,
                new FixVector3(Fix64.FromRatio(-45, 100), Fix64.Zero, Fix64.FromRatio(-45, 100)),
                new FixVector3(Fix64.FromRatio(45, 100), Fix64.FromRatio(18, 10), Fix64.FromRatio(45, 100))));
            SyncPhysicsWorld();
        }

        private static void RegisterActions(CombatActionRegistry registry, CombatActionTimelineTraceProvider traceProvider)
        {
            registry.RegisterTimeline(CombatAnimationDemoIds.LightAttackActionId, new CombatActionTimeline(
                CombatAnimationDemoIds.LightAttackActionId,
                totalFrames: 23,
                startup: new CombatFrameRange(0, 9),
                active: new CombatFrameRange(10, 14),
                recovery: new CombatFrameRange(15, 22),
                windows: null,
                events: null));
            registry.RegisterTimeline(CombatAnimationDemoIds.HeavyAttackActionId, new CombatActionTimeline(
                CombatAnimationDemoIds.HeavyAttackActionId,
                totalFrames: 43,
                startup: new CombatFrameRange(0, 19),
                active: new CombatFrameRange(20, 27),
                recovery: new CombatFrameRange(28, 42),
                windows: null,
                events: null));
            registry.RegisterTimeline(CombatAnimationDemoIds.DodgeRollActionId, new CombatActionTimeline(
                CombatAnimationDemoIds.DodgeRollActionId,
                totalFrames: 15,
                startup: new CombatFrameRange(0, 4),
                active: CombatFrameRange.Empty,
                recovery: new CombatFrameRange(5, 14),
                windows: new[]
                {
                    new CombatActionWindow(CombatActionWindowKind.Invincible, new CombatFrameRange(0, 2)),
                },
                events: null));

            RegisterTraceFrames(traceProvider, CombatAnimationDemoIds.LightAttackActionId, 10, 14, traceId: 101, reach: Fix64.FromRatio(23, 10));
            RegisterTraceFrames(traceProvider, CombatAnimationDemoIds.HeavyAttackActionId, 20, 27, traceId: 201, reach: Fix64.FromRatio(27, 10));
        }

        private static void RegisterTraceFrames(
            CombatActionTimelineTraceProvider traceProvider,
            int actionId,
            int firstFrame,
            int lastFrame,
            int traceId,
            Fix64 reach)
        {
            for (int frame = firstFrame; frame <= lastFrame; frame++)
            {
                traceProvider.RegisterTrace(actionId, frame, new WeaponTraceFrame(
                    traceId,
                    new FixVector3(Fix64.Half, Fix64.One, Fix64.Zero),
                    new FixVector3(reach, Fix64.One, Fix64.Zero),
                    new FixVector3(Fix64.Half, Fix64.One, Fix64.Zero),
                    new FixVector3(reach, Fix64.One, Fix64.Zero),
                    Fix64.FromRatio(35, 100),
                    CombatPhysicsLayerMask.FromLayer(CombatAnimationDemoIds.HurtboxLayer)));
            }
        }

        private void AddEvent(string value)
        {
            _eventLog.Add(value);
            while (_eventLog.Count > 20)
            {
                _eventLog.RemoveAt(0);
            }
        }

        private static int GetDamage(int actionId)
        {
            switch (actionId)
            {
                case CombatAnimationDemoIds.LightAttackActionId:
                    return LightDamage;
                case CombatAnimationDemoIds.HeavyAttackActionId:
                    return HeavyDamage;
                case CombatAnimationDemoIds.DodgeRollActionId:
                    return DodgeDamage;
                default:
                    return 0;
            }
        }

        private static string ActionName(int actionId)
        {
            switch (actionId)
            {
                case CombatAnimationDemoIds.LightAttackActionId:
                    return "LightAttack";
                case CombatAnimationDemoIds.HeavyAttackActionId:
                    return "HeavyAttack";
                case CombatAnimationDemoIds.DodgeRollActionId:
                    return "DodgeRoll";
                default:
                    return "Action " + actionId;
            }
        }

        private static FixVector3 ToFix(Vector3 value)
        {
            return new FixVector3(ToFix(value.x), ToFix(value.y), ToFix(value.z));
        }

        private static Fix64 ToFix(float value)
        {
            return Fix64.FromRatio(Mathf.RoundToInt(value * 1000f), 1000);
        }

        private string ResolveGameplayName(GameplayEntityId entityId)
        {
            if (_entityMap != null && _entityMap.TryGetCombatId(entityId, out CombatEntityId combatId))
            {
                if (combatId.Equals(CombatAnimationDemoIds.PlayerEntityId))
                {
                    return "Player";
                }

                if (combatId.Equals(CombatAnimationDemoIds.DummyEntityId))
                {
                    return "Dummy";
                }

                return "Combat " + combatId.Value;
            }

            return "Gameplay " + entityId;
        }

        private sealed class GameplayBridgeTargetStateResolver : IHitTargetStateResolver
        {
            private readonly CombatEntityGameplayMap _entityMap;
            private readonly GameplayComponentWorld _componentWorld;
            private readonly CombatTargetStateProvider _stateProvider;

            public GameplayBridgeTargetStateResolver(
                CombatEntityGameplayMap entityMap,
                GameplayComponentWorld componentWorld,
                CombatTargetStateProvider stateProvider)
            {
                _entityMap = entityMap ?? throw new ArgumentNullException(nameof(entityMap));
                _componentWorld = componentWorld ?? throw new ArgumentNullException(nameof(componentWorld));
                _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
            }

            public HitTargetStateFlags ResolveTargetState(CombatEntityId targetId)
            {
                return _entityMap.TryGetGameplayId(targetId, out GameplayEntityId gameplayId)
                    ? _stateProvider.Evaluate(_componentWorld, gameplayId)
                    : HitTargetStateFlags.None;
            }
        }
    }
}

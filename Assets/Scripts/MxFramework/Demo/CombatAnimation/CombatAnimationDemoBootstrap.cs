using System.Collections.Generic;
using System.Reflection;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
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
        private const float FixedDeltaTime = 1f / 30f;
        private const float MoveSpeed = 3.2f;
        private const int PlayerMaxHp = 100;
        private const int DummyMaxHp = 100;
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
        private readonly List<string> _eventLog = new List<string>();
        private readonly CombatAnimationHudModel _hudModel = new CombatAnimationHudModel();

        private RuntimeHost _host;
        private ICombatAnimationContext _animationContext;
        private CombatPhysicsWorld _physicsWorld;
        private CombatActionRegistry _actionRegistry;
        private CombatActionTimelineTraceProvider _traceProvider;
        private CombatAnimationUnityModule _unityModule;
        private CombatDemoPoseSource _poseSource;
        private DemoInputToActionAdapter _inputAdapter;
        private HitResolveSystem _hitResolve;
        private int _playerHp;
        private int _dummyHp;
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
            _playerHp = PlayerMaxHp;
            _dummyHp = DummyMaxHp;

            RegisterActions(_actionRegistry, _traceProvider);
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
            AddEvent("Combat Animation demo initialized.");
            RefreshHud();
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
            _hitResolve.Resolve(_animationContext.LastFrameHitCandidates, _consumedHitOnceKeys, _hitResults);
            for (int i = 0; i < _hitResults.Count; i++)
            {
                HitResolveResult result = _hitResults[i];
                if (result.Kind != HitResolveKind.Damage || !result.TargetId.Equals(CombatAnimationDemoIds.DummyEntityId))
                {
                    AddEvent($"Hit {result.Kind}: action={ActionName(result.ActionId)} target={result.TargetId.Value}");
                    continue;
                }

                int damage = GetDamage(result.ActionId);
                _dummyHp = Mathf.Max(0, _dummyHp - damage);
                AddEvent($"Hit Damage: {ActionName(result.ActionId)} dealt {damage}. Dummy HP {_dummyHp}/{DummyMaxHp}");
            }
        }

        private void RefreshHud()
        {
            if (_hud == null)
            {
                return;
            }

            CombatActionState? state = _animationContext?.ActionRunner.GetActionState(CombatAnimationDemoIds.PlayerEntityId);
            CombatAnimationSnapshot? snapshot = _animationContext?.LastSnapshot;
            _hudModel.PlayerAction = state.HasValue ? ActionName(state.Value.ActionId) : "Idle";
            _hudModel.PlayerPhase = state.HasValue ? state.Value.Phase.ToString() : "None";
            _hudModel.PlayerLocalFrame = state.HasValue ? state.Value.LocalFrame.ToString() : "-";
            _hudModel.PlayerHp = $"{_playerHp}/{PlayerMaxHp}";
            _hudModel.DummyHp = $"{_dummyHp}/{DummyMaxHp}";
            _hudModel.WeaponTrace = snapshot.HasValue
                ? $"active={snapshot.Value.ActivePhaseCount} candidates={snapshot.Value.HitCandidateCount} frame={snapshot.Value.FrameIndex}"
                : "waiting";
            _hudModel.Instructions = "WASD move | J light | K heavy | Space dodge";
            _hudModel.RecentEvents = _eventLog;
            _hud.Refresh(_hudModel);
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
    }
}

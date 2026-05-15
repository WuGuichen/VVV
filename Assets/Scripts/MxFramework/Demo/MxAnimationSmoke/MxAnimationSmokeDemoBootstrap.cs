using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Animation.Unity;
using MxFramework.Combat.Core;
using MxFramework.Input;
using MxFramework.Resources;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.MxAnimationSmoke
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Demo/MxAnimation Smoke Demo Bootstrap")]
    public sealed class MxAnimationSmokeDemoBootstrap : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/MxAnimationPlayModeSmoke.unity";
        public const int IdleActionId = 9101;
        public const int WalkActionId = 9102;
        public const int RunActionId = 9103;
        public const int JumpActionId = 9104;

        private const int TicksPerSecond = 30;
        private const float FixedDeltaTime = 1f / TicksPerSecond;
        private const int MaxEvents = 6;
        private const string ActorId = "mxanimation.smoke.skeleton";

        private static readonly CombatEntityId ActorEntityId = new CombatEntityId(1);
        private static readonly PropertyInfo KeyboardCurrentProperty =
            Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem")?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);

        [SerializeField] private DefaultInputService _inputService;
        [SerializeField] private UIDocument _document;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private StyleSheet _styleSheet;
        [SerializeField] private Transform _actorParent;
        [SerializeField] private GameObject _skeletonModel;
        [SerializeField] private AnimationClip _idleClip;
        [SerializeField] private AnimationClip _walkForwardClip;
        [SerializeField] private AnimationClip _runForwardClip;
        [SerializeField] private AnimationClip _jumpClip;

        private readonly List<InputCommand> _drainedCommands = new List<InputCommand>();
        private readonly List<string> _events = new List<string>();

        private ResourceManager _resourceManager;
        private MemoryResourceProvider _provider;
        private ResourceHandle<GameObject> _modelHandle;
        private GameObject _modelInstance;
        private Animator _animator;
        private MxAnimationSetDefinition _animationSet;
        private UnityPlayablesAnimationBackend _backend;
        private CombatAnimationContext _combatContext;
        private CombatActionRegistry _actionRegistry;
        private CombatActionRunner _runner;
        private CombatMxAnimationUnityBridge _bridge;
        private CombatFrame _worldFrame = CombatFrame.Zero;
        private float _accumulator;
        private string _currentAction = "Idle";
        private string _lastRequest = "Initializing";
        private string _initializationError = string.Empty;

        private Label _title;
        private Label _instructions;
        private Label _actionLabel;
        private Label _clipLabel;
        private Label _backendLabel;
        private Label _resourceLabel;
        private Label _bridgeLabel;
        private Label _errorLabel;
        private VisualElement _eventList;

        public bool IsInitialized { get; private set; }
        public bool HasInitializationError => !string.IsNullOrEmpty(_initializationError);
        public UnityPlayablesAnimationBackend Backend => _backend;
        public ResourceManager ResourceManager => _resourceManager;
        public GameObject ModelInstance => _modelInstance;
        public Animator Animator => _animator;

        private void Awake()
        {
            BindHud();
            InitializeSmoke();
        }

        private void Update()
        {
            if (!IsInitialized)
            {
                RefreshHud();
                return;
            }

            long inputFrame = _inputService != null ? _inputService.Commands.CurrentFrame : _worldFrame.Value;
            EnqueueKeyboardCommands(inputFrame);
            DrainInput(inputFrame);
            TickCombat();
            _backend.Tick(Time.deltaTime);
            RefreshHud();
        }

        private void OnDestroy()
        {
            _bridge?.Dispose();
            _backend?.Release();
            if (_modelInstance != null)
                Destroy(_modelInstance);
            if (_modelHandle != null)
                _resourceManager?.Release(_modelHandle);

            _bridge = null;
            _backend = null;
            _modelInstance = null;
            _modelHandle = null;
            _resourceManager = null;
            IsInitialized = false;
        }

        public void ConfigureSceneReferences(
            DefaultInputService inputService,
            UIDocument document,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Transform actorParent,
            GameObject skeletonModel,
            AnimationClip idleClip,
            AnimationClip walkForwardClip,
            AnimationClip runForwardClip,
            AnimationClip jumpClip)
        {
            _inputService = inputService;
            _document = document;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
            _actorParent = actorParent;
            _skeletonModel = skeletonModel;
            _idleClip = idleClip;
            _walkForwardClip = walkForwardClip;
            _runForwardClip = runForwardClip;
            _jumpClip = jumpClip;
        }

        private void InitializeSmoke()
        {
            try
            {
                _inputService = _inputService != null ? _inputService : GetComponent<DefaultInputService>();
                _document = _document != null ? _document : GetComponent<UIDocument>();

                ResourceCatalog catalog = TempImportedResourceCatalog.CreateCatalog();
                _provider = new MemoryResourceProvider();
                RegisterSerializedAsset(catalog, TempImportedResourceCatalog.SkeletonModelId, ResourceTypeIds.GameObject, _skeletonModel);
                RegisterSerializedAsset(catalog, TempImportedResourceCatalog.SkeletonIdleAnimationId, ResourceTypeIds.AnimationClip, _idleClip);
                RegisterSerializedAsset(catalog, TempImportedResourceCatalog.SkeletonWalkForwardAnimationId, ResourceTypeIds.AnimationClip, _walkForwardClip);
                RegisterSerializedAsset(catalog, TempImportedResourceCatalog.SkeletonRunForwardAnimationId, ResourceTypeIds.AnimationClip, _runForwardClip);
                RegisterSerializedAsset(catalog, TempImportedResourceCatalog.SkeletonJumpAnimationId, ResourceTypeIds.AnimationClip, _jumpClip);

                _resourceManager = new ResourceManager();
                _resourceManager.RegisterProvider(_provider);
                _resourceManager.AddCatalog(catalog);
                _resourceManager.ValidateCatalogs();

                LoadModel();
                _animationSet = CreateAnimationSet();
                _backend = new UnityPlayablesAnimationBackend(_animator, _resourceManager, _animationSet, ActorId);

                _combatContext = new CombatAnimationContext();
                _actionRegistry = new CombatActionRegistry();
                RegisterTimelines(_actionRegistry);
                _runner = new CombatActionRunner(_actionRegistry);
                _combatContext.SetActionRunner(_runner);

                var options = new CombatMxAnimationBridgeOptions
                {
                    StartRequestKind = CombatMxAnimationStartRequestKind.CrossFade,
                    FinishedRequestKind = CombatMxAnimationEndRequestKind.CrossFade,
                    CanceledRequestKind = CombatMxAnimationEndRequestKind.Stop,
                    FinishedCrossFadeBindingId = "idle",
                    StartCrossFadeDurationSeconds = 0.18f,
                    StopFadeOutDurationSeconds = 0.08f,
                    EndCrossFadeDurationSeconds = 0.18f
                };
                _bridge = new CombatMxAnimationUnityBridge(_combatContext, options);
                _bridge.RegisterActor(ActorEntityId, _backend, _animationSet, ActorId);
                _bridge.Initialize();

                ForceAction(IdleActionId, "Idle");
                IsInitialized = true;
                AddEvent("MxAnimation smoke initialized through ResourceManager catalog.");
            }
            catch (Exception ex)
            {
                _initializationError = ex.Message;
                AddEvent("Initialization failed: " + ex.Message);
                Debug.LogError("MxAnimation smoke initialization failed: " + ex);
            }

            RefreshHud();
        }

        private void RegisterSerializedAsset(ResourceCatalog catalog, string id, string typeId, UnityEngine.Object asset)
        {
            if (asset == null)
                throw new InvalidOperationException("Smoke scene missing serialized resource asset for " + id + ".");

            ResourceCatalogEntry entry = FindEntry(catalog, id, typeId);
            _provider.Register(entry.Address, asset);
        }

        private void LoadModel()
        {
            ResourceLoadResult<ResourceHandle<GameObject>> result = _resourceManager.Load<GameObject>(Key(TempImportedResourceCatalog.SkeletonModelId, ResourceTypeIds.GameObject));
            if (!result.Success)
                throw new InvalidOperationException("Skeleton model load failed: " + result.Error.Message);

            _modelHandle = result.Value;
            _modelInstance = Instantiate(_modelHandle.Value, _actorParent != null ? _actorParent : transform);
            _modelInstance.name = "Skeleton_Model_Instance";
            _modelInstance.transform.localPosition = Vector3.zero;
            _modelInstance.transform.localRotation = Quaternion.identity;
            _modelInstance.transform.localScale = Vector3.one;

            _animator = _modelInstance.GetComponentInChildren<Animator>();
            if (_animator == null)
                _animator = _modelInstance.AddComponent<Animator>();

            _animator.applyRootMotion = false;
        }

        private static MxAnimationSetDefinition CreateAnimationSet()
        {
            return new MxAnimationSetDefinition(
                "mxanimation.smoke.skeleton",
                1,
                Key(TempImportedResourceCatalog.SkeletonIdleAnimationId, ResourceTypeIds.AnimationClip),
                Key(TempImportedResourceCatalog.SkeletonIdleAnimationId, ResourceTypeIds.AnimationClip),
                new[]
                {
                    Binding("idle", IdleActionId, TempImportedResourceCatalog.SkeletonIdleAnimationId, loop: true),
                    Binding("walk", WalkActionId, TempImportedResourceCatalog.SkeletonWalkForwardAnimationId, loop: true),
                    Binding("run", RunActionId, TempImportedResourceCatalog.SkeletonRunForwardAnimationId, loop: true),
                    Binding("jump", JumpActionId, TempImportedResourceCatalog.SkeletonJumpAnimationId, loop: false)
                });
        }

        private static MxAnimationActionBinding Binding(string bindingId, int actionId, string clipId, bool loop)
        {
            return new MxAnimationActionBinding(
                bindingId,
                "action:" + actionId,
                Key(clipId, ResourceTypeIds.AnimationClip),
                MxAnimationLayerId.Base,
                playbackSpeed: 1f,
                loop: loop,
                alignmentPolicy: MxAnimationAlignmentPolicy.StartAtZero);
        }

        private static void RegisterTimelines(CombatActionRegistry registry)
        {
            registry.RegisterTimeline(IdleActionId, Timeline(IdleActionId, 600));
            registry.RegisterTimeline(WalkActionId, Timeline(WalkActionId, 600));
            registry.RegisterTimeline(RunActionId, Timeline(RunActionId, 600));
            registry.RegisterTimeline(JumpActionId, Timeline(JumpActionId, 42));
        }

        private static CombatActionTimeline Timeline(int actionId, int totalFrames)
        {
            return new CombatActionTimeline(
                actionId,
                totalFrames,
                new CombatFrameRange(0, 0),
                totalFrames > 2 ? new CombatFrameRange(1, totalFrames - 2) : CombatFrameRange.Empty,
                new CombatFrameRange(totalFrames - 1, totalFrames - 1),
                null,
                null);
        }

        private void EnqueueKeyboardCommands(long frame)
        {
            TryEnqueue(frame, "iKey", InputIntent.DebugPrimary, "MxAnimationSmoke.I");
            TryEnqueue(frame, "oKey", InputIntent.AttackPrimary, "MxAnimationSmoke.O");
            TryEnqueue(frame, "pKey", InputIntent.AttackSecondary, "MxAnimationSmoke.P");
            TryEnqueue(frame, "spaceKey", InputIntent.Jump, "MxAnimationSmoke.Space");
        }

        private void TryEnqueue(long frame, string keyPropertyName, InputIntent intent, string traceId)
        {
            if (_inputService == null || !IsKeyboardKeyPressedThisFrame(keyPropertyName))
                return;

            _inputService.Commands.TryEnqueue(new InputCommand(
                frame,
                sourceId: 103,
                intent,
                InputCommandPhase.Pressed,
                traceId: traceId), out _);
        }

        private void DrainInput(long frame)
        {
            if (_inputService == null)
                return;

            _drainedCommands.Clear();
            _inputService.Commands.DrainForFrame(frame, _drainedCommands);
            for (int i = 0; i < _drainedCommands.Count; i++)
            {
                InputCommand command = _drainedCommands[i];
                if (command.Phase != InputCommandPhase.Pressed && command.Phase != InputCommandPhase.Performed)
                    continue;

                int actionId = ResolveActionId(command.Intent, out string displayName);
                if (actionId <= 0)
                    continue;

                ForceAction(actionId, displayName);
            }
        }

        private void ForceAction(int actionId, string displayName)
        {
            ActionResult result = _runner.ForceStartAction(ActorEntityId, actionId, _worldFrame);
            if (!result.Success)
            {
                AddEvent("Action rejected: " + displayName + " " + result.Reason);
                return;
            }

            _currentAction = displayName;
            _lastRequest = displayName + " instance " + result.ActionInstanceId;
            AddEvent("Action -> " + displayName);
        }

        private void TickCombat()
        {
            _accumulator += Time.deltaTime;
            int guard = 0;
            while (_accumulator >= FixedDeltaTime && guard < 4)
            {
                _worldFrame = _worldFrame.Next();
                _runner.TickActions(_worldFrame);
                _accumulator -= FixedDeltaTime;
                guard++;
            }
        }

        private void BindHud()
        {
            _document = _document != null ? _document : GetComponent<UIDocument>();
            VisualElement root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
                return;

            if (_styleSheet != null && !root.styleSheets.Contains(_styleSheet))
                root.styleSheets.Add(_styleSheet);

            _title = root.Q<Label>("title");
            _instructions = root.Q<Label>("instructions");
            _actionLabel = root.Q<Label>("action");
            _clipLabel = root.Q<Label>("clip");
            _backendLabel = root.Q<Label>("backend");
            _resourceLabel = root.Q<Label>("resources");
            _bridgeLabel = root.Q<Label>("bridge");
            _errorLabel = root.Q<Label>("error");
            _eventList = root.Q<VisualElement>("events");

            ApplyLabelFallback(_title, 20f, new Color(0.95f, 0.98f, 1f, 1f), FontStyle.Bold);
            ApplyLabelFallback(_instructions, 13f, new Color(0.72f, 0.80f, 0.86f, 1f), FontStyle.Normal);
            ApplyLabelFallback(_actionLabel, 14f, Color.white, FontStyle.Bold);
            ApplyLabelFallback(_clipLabel, 13f, Color.white, FontStyle.Normal);
            ApplyLabelFallback(_backendLabel, 13f, Color.white, FontStyle.Normal);
            ApplyLabelFallback(_resourceLabel, 13f, Color.white, FontStyle.Normal);
            ApplyLabelFallback(_bridgeLabel, 13f, Color.white, FontStyle.Normal);
            ApplyLabelFallback(_errorLabel, 13f, new Color(1f, 0.58f, 0.48f, 1f), FontStyle.Bold);
        }

        private void RefreshHud()
        {
            if (_actionLabel == null)
                BindHud();

            MxAnimationDiagnosticSnapshot animation = _backend?.CreateSnapshot();
            MxAnimationLayerDiagnostic layer = FindBaseLayer(animation);
            ResourceDebugSnapshot resources = _resourceManager?.CreateDebugSnapshot();
            CombatMxAnimationBridgeDiagnosticSnapshot bridge = _bridge?.CreateSnapshot();

            SetText(_title, "MxAnimation Play Mode Smoke");
            SetText(_instructions, "I idle | O walk | P run | Space jump");
            SetText(_actionLabel, "Action: " + _currentAction + " | frame " + _worldFrame.Value + " | " + _lastRequest);
            SetText(_clipLabel, layer != null
                ? "Clip: " + layer.CurrentClipKey.Id + " | " + layer.Status + " | weight " + layer.CurrentWeight.ToString("0.00")
                : "Clip: loading");
            SetText(_backendLabel, animation != null
                ? "Backend: " + animation.BackendName + " graph=" + animation.GraphIsValid + " set=" + animation.SetId
                : "Backend: unavailable");
            SetText(_resourceLabel, resources != null
                ? "Resources: loaded=" + resources.LoadedCount + " refs=" + resources.TotalRefCount + " failed=" + resources.FailedCount
                : "Resources: unavailable");
            SetText(_bridgeLabel, bridge != null
                ? "Bridge: initialized=" + bridge.IsInitialized + " actors=" + bridge.ActorCount + " events=" + bridge.RecentEntries.Count
                : "Bridge: unavailable");
            SetText(_errorLabel, string.IsNullOrEmpty(_initializationError) ? string.Empty : "Error: " + _initializationError);
            RefreshEvents();
        }

        private void RefreshEvents()
        {
            if (_eventList == null)
                return;

            _eventList.Clear();
            if (_events.Count == 0)
            {
                _eventList.Add(new Label("waiting"));
                return;
            }

            for (int i = 0; i < _events.Count; i++)
                _eventList.Add(new Label(_events[i]));
        }

        private void AddEvent(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            _events.Insert(0, message);
            while (_events.Count > MaxEvents)
                _events.RemoveAt(_events.Count - 1);
        }

        private static int ResolveActionId(InputIntent intent, out string displayName)
        {
            switch (intent)
            {
                case InputIntent.DebugPrimary:
                    displayName = "Idle";
                    return IdleActionId;
                case InputIntent.AttackPrimary:
                    displayName = "Walk";
                    return WalkActionId;
                case InputIntent.AttackSecondary:
                    displayName = "Run";
                    return RunActionId;
                case InputIntent.Jump:
                    displayName = "Jump";
                    return JumpActionId;
                default:
                    displayName = string.Empty;
                    return 0;
            }
        }

        private static bool IsKeyboardKeyPressedThisFrame(string keyPropertyName)
        {
            object keyboard = KeyboardCurrentProperty?.GetValue(null);
            if (keyboard == null)
                return false;

            object keyControl = keyboard.GetType().GetProperty(keyPropertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(keyboard);
            object pressed = keyControl?.GetType().GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance)?.GetValue(keyControl);
            return pressed is bool value && value;
        }

        private static ResourceCatalogEntry FindEntry(ResourceCatalog catalog, string id, string typeId)
        {
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry.Id == id && entry.TypeId == typeId)
                    return entry;
            }

            throw new InvalidOperationException("Sample catalog entry missing: " + id + ":" + typeId + ".");
        }

        private static MxAnimationLayerDiagnostic FindBaseLayer(MxAnimationDiagnosticSnapshot snapshot)
        {
            if (snapshot == null)
                return null;

            for (int i = 0; i < snapshot.LayerStates.Count; i++)
            {
                if (snapshot.LayerStates[i].LayerId == MxAnimationLayerId.Base)
                    return snapshot.LayerStates[i];
            }

            return null;
        }

        private static ResourceKey Key(string id, string typeId)
        {
            return new ResourceKey(id, typeId, string.Empty, TempImportedResourceCatalog.PackageId);
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
                label.text = text ?? string.Empty;
        }

        private static void ApplyLabelFallback(Label label, float fontSize, Color color, FontStyle fontStyle)
        {
            if (label == null)
                return;

            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = fontStyle;
            label.style.whiteSpace = WhiteSpace.Normal;
        }
    }
}

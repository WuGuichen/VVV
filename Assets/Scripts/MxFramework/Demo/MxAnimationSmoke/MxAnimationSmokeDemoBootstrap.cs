using System;
using System.Collections.Generic;
using System.IO;
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
        public const int UpperAttackActionId = 9105;
        public const string LocomotionBlendId = "locomotion";
        public const string SpeedParameterId = "locomotion.speed";

        private const int TicksPerSecond = 30;
        private const float FixedDeltaTime = 1f / TicksPerSecond;
        private const int MaxEvents = 6;
        private const string ActorId = "mxanimation.smoke.skeleton";
        private const string UpperBodyLayerId = "upper_body";

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
        [SerializeField] private AnimationClip[] _warmupAnimationClips = Array.Empty<AnimationClip>();
        [SerializeField] private AvatarMask _upperBodyMask;

        private readonly List<InputCommand> _drainedCommands = new List<InputCommand>();
        private readonly List<string> _events = new List<string>();

        private ResourceManager _resourceManager;
        private MemoryResourceProvider _provider;
        private ResourceHandle<GameObject> _modelHandle;
        private GameObject _modelInstance;
        private Animator _animator;
        private MxAnimationSetDefinition _animationSet;
        private MxAnimationClipRegistry _clipRegistry;
        private MxAnimationWarmupService _warmupService;
        private MxAnimationWarmupResult _warmupResult;
        private UnityPlayablesAnimationBackend _backend;
        private CombatAnimationContext _combatContext;
        private CombatActionRegistry _actionRegistry;
        private CombatActionRunner _runner;
        private CombatMxAnimationUnityBridge _bridge;
        private CombatFrame _worldFrame = CombatFrame.Zero;
        private float _accumulator;
        private MxAnimationQuantizedParameter _speedParameter = new MxAnimationQuantizedParameter(SpeedParameterId, 0);
        private string _speedName = "Idle";
        private string _currentAction = "Locomotion";
        private string _lastRequest = "Initializing";
        private string _initializationError = string.Empty;
        private string _warmupSummary = "Warmup: not started";

        private Label _title;
        private Label _instructions;
        private Label _actionLabel;
        private Label _speedLabel;
        private Label _clipLabel;
        private Label _layerLabel;
        private Label _warmupLabel;
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
        public MxAnimationWarmupResult WarmupResult => _warmupResult;
        public MxAnimationQuantizedParameter SpeedParameter => _speedParameter;

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
            if (_runner != null)
                _runner.ActionFinished -= OnActionFinished;
            _backend?.Release();
            if (_modelInstance != null)
                Destroy(_modelInstance);
            _warmupService?.Release(_warmupResult);
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
            AnimationClip jumpClip,
            IEnumerable<AnimationClip> warmupAnimationClips,
            AvatarMask upperBodyMask)
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
            _warmupAnimationClips = warmupAnimationClips != null
                ? new List<AnimationClip>(warmupAnimationClips).ToArray()
                : Array.Empty<AnimationClip>();
            _upperBodyMask = upperBodyMask;
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
                RegisterSerializedAsset(catalog, TempImportedResourceCatalog.SkeletonUpperBodyMaskId, ResourceTypeIds.AvatarMask, _upperBodyMask);
                RegisterWarmupAnimationClips(catalog);

                _resourceManager = new ResourceManager();
                _resourceManager.RegisterProvider(_provider);
                _resourceManager.AddCatalog(catalog);
                _resourceManager.ValidateCatalogs();

                LoadModel();
                _animationSet = CreateAnimationSet();
                _clipRegistry = MxAnimationClipRegistryBuilder.FromCatalog(catalog, version: 1, catalogHash: "mxanimation.smoke.catalog");
                Warmup(catalog);
                _backend = new UnityPlayablesAnimationBackend(_animator, _resourceManager, _animationSet, ActorId);

                _combatContext = new CombatAnimationContext();
                _actionRegistry = new CombatActionRegistry();
                RegisterTimelines(_actionRegistry);
                _runner = new CombatActionRunner(_actionRegistry);
                _combatContext.SetActionRunner(_runner);

                var options = new CombatMxAnimationBridgeOptions
                {
                    StartRequestKind = CombatMxAnimationStartRequestKind.CrossFade,
                    FinishedRequestKind = CombatMxAnimationEndRequestKind.Stop,
                    CanceledRequestKind = CombatMxAnimationEndRequestKind.Stop,
                    StartCrossFadeDurationSeconds = 0.18f,
                    StopFadeOutDurationSeconds = 0.08f,
                    EndCrossFadeDurationSeconds = 0.18f
                };
                _bridge = new CombatMxAnimationUnityBridge(_combatContext, options);
                _bridge.RegisterActor(ActorEntityId, _backend, _animationSet, ActorId);
                _bridge.Initialize();
                _runner.ActionFinished += OnActionFinished;

                SetSpeedParameter(0, "Idle", false);
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

        private void RegisterWarmupAnimationClips(ResourceCatalog catalog)
        {
            if (catalog == null || _warmupAnimationClips == null || _warmupAnimationClips.Length == 0)
                return;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null
                    || entry.TypeId != ResourceTypeIds.AnimationClip
                    || !HasLabel(entry, TempImportedResourceCatalog.WarmupMxAnimationLabel))
                {
                    continue;
                }

                if (!entry.ProviderData.TryGetValue(TempImportedResourceCatalog.ProviderDataAssetPathKey, out string assetPath))
                    continue;

                AnimationClip clip = FindWarmupAnimationClip(Path.GetFileNameWithoutExtension(assetPath));
                if (clip != null)
                    _provider.Register(entry.Address, clip);
            }
        }

        private AnimationClip FindWarmupAnimationClip(string clipName)
        {
            if (string.IsNullOrWhiteSpace(clipName) || _warmupAnimationClips == null)
                return null;

            for (int i = 0; i < _warmupAnimationClips.Length; i++)
            {
                AnimationClip clip = _warmupAnimationClips[i];
                if (clip != null && string.Equals(clip.name, clipName, StringComparison.Ordinal))
                    return clip;
            }

            return null;
        }

        private static bool HasLabel(ResourceCatalogEntry entry, string label)
        {
            if (entry == null || string.IsNullOrEmpty(label))
                return false;

            for (int i = 0; i < entry.Labels.Count; i++)
            {
                if (string.Equals(entry.Labels[i], label, StringComparison.Ordinal))
                    return true;
            }

            return false;
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

        private void Warmup(ResourceCatalog catalog)
        {
            _warmupService = new MxAnimationWarmupService(new ResourcePreloadService(_resourceManager));
            _warmupResult = _warmupService.Warmup(new MxAnimationWarmupRequest(_animationSet, _clipRegistry, catalog));
            if (_warmupResult.Success)
            {
                _warmupSummary = "Warmup: ready keys=" + _warmupResult.RequiredKeys.Count + " labels=" + _warmupResult.Labels.Count;
                return;
            }

            _warmupSummary = "Warmup: failed issues=" + _warmupResult.Issues.Count;
            for (int i = 0; i < _warmupResult.Issues.Count; i++)
                AddEvent("Warmup issue: " + _warmupResult.Issues[i].Code + " " + _warmupResult.Issues[i].Key);
        }

        private static MxAnimationSetDefinition CreateAnimationSet()
        {
            var upperBodyLayer = new MxAnimationLayerId(UpperBodyLayerId);
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
                    Binding("jump", JumpActionId, TempImportedResourceCatalog.SkeletonJumpAnimationId, loop: false),
                    new MxAnimationActionBinding(
                        "upper_attack",
                        "action:" + UpperAttackActionId,
                        Key(TempImportedResourceCatalog.SkeletonJumpAnimationId, ResourceTypeIds.AnimationClip),
                        upperBodyLayer,
                        playbackSpeed: 1.15f,
                        loop: false,
                        alignmentPolicy: MxAnimationAlignmentPolicy.UseCombatFrameAnchor)
                },
                layers: new[]
                {
                    new MxAnimationLayerDefinition(MxAnimationLayerId.Base, "locomotion.base", 1f),
                    new MxAnimationLayerDefinition(
                        upperBodyLayer,
                        "locomotion.upper_body",
                        0f,
                        MxAnimationLayerBlendMode.Override,
                        Key(TempImportedResourceCatalog.SkeletonUpperBodyMaskId, ResourceTypeIds.AvatarMask))
                },
                warmup: new MxAnimationWarmupDefinition(
                    "mxanimation.smoke.locomotion",
                    labels: new[] { TempImportedResourceCatalog.WarmupMxAnimationLabel }),
                blend1DDefinitions: new[]
                {
                    new MxAnimationBlend1DDefinition(
                        LocomotionBlendId,
                        SpeedParameterId,
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend1DPoint(0, Key(TempImportedResourceCatalog.SkeletonIdleAnimationId, ResourceTypeIds.AnimationClip), loop: true),
                            new MxAnimationBlend1DPoint(500, Key(TempImportedResourceCatalog.SkeletonWalkForwardAnimationId, ResourceTypeIds.AnimationClip), loop: true),
                            new MxAnimationBlend1DPoint(1000, Key(TempImportedResourceCatalog.SkeletonRunForwardAnimationId, ResourceTypeIds.AnimationClip), loop: true)
                        },
                        parameterScale: 1000,
                        fadeDurationSeconds: 0.12f)
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
            registry.RegisterTimeline(UpperAttackActionId, Timeline(UpperAttackActionId, 24));
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

                if (TryResolveSpeed(command.Intent, out int speed, out string speedName))
                {
                    SetSpeedParameter(speed, speedName, true);
                    continue;
                }

                if (command.Intent == InputIntent.Jump)
                    TriggerUpperAttack();
            }
        }

        private void SetSpeedParameter(int quantizedSpeed, string displayName, bool addEvent)
        {
            _speedParameter = new MxAnimationQuantizedParameter(SpeedParameterId, quantizedSpeed);
            _speedName = displayName ?? string.Empty;
            MxAnimationBackendResult result = _backend.SetBlend1D(new MxAnimationBlend1DRequest
            {
                BlendId = LocomotionBlendId,
                Parameter = _speedParameter,
                CorrelationId = "speed:" + quantizedSpeed
            });

            _currentAction = "Locomotion";
            _lastRequest = _speedName + " speed=" + _speedParameter.Value.ToString("0.00");
            if (!result.Success)
            {
                AddEvent("Speed rejected: " + result.Message);
                return;
            }

            if (addEvent)
                AddEvent("Speed -> " + _speedName + " (" + _speedParameter.Value.ToString("0.00") + ")");
        }

        private void TriggerUpperAttack()
        {
            _backend.SetLayerWeight(new MxAnimationLayerWeightRequest
            {
                LayerId = new MxAnimationLayerId(UpperBodyLayerId),
                Weight = 1f,
                FadeDurationSeconds = 0.08f,
                TransitionPolicyId = "upper.attack.in",
                CorrelationId = "upper.attack.in"
            });
            ActionResult result = _runner.ForceStartAction(ActorEntityId, UpperAttackActionId, _worldFrame);
            if (!result.Success)
            {
                AddEvent("Upper attack rejected: " + result.Reason);
                return;
            }

            _currentAction = "Upper Attack";
            _lastRequest = "Upper attack instance " + result.ActionInstanceId;
            AddEvent("Upper attack layer -> on");
        }

        private void OnActionFinished(ActionFinishedEvent evt)
        {
            if (evt.ActionId != UpperAttackActionId || _backend == null)
                return;

            _backend.SetLayerWeight(new MxAnimationLayerWeightRequest
            {
                LayerId = new MxAnimationLayerId(UpperBodyLayerId),
                Weight = 0f,
                FadeDurationSeconds = 0.12f,
                TransitionPolicyId = "upper.attack.out",
                CorrelationId = "upper.attack.out"
            });
            _currentAction = "Locomotion";
            AddEvent("Upper attack layer -> off");
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
            _speedLabel = root.Q<Label>("speed");
            _clipLabel = root.Q<Label>("clip");
            _layerLabel = root.Q<Label>("layers");
            _warmupLabel = root.Q<Label>("warmup");
            _backendLabel = root.Q<Label>("backend");
            _resourceLabel = root.Q<Label>("resources");
            _bridgeLabel = root.Q<Label>("bridge");
            _errorLabel = root.Q<Label>("error");
            _eventList = root.Q<VisualElement>("events");

            ApplyLabelFallback(_title, 20f, new Color(0.95f, 0.98f, 1f, 1f), FontStyle.Bold);
            ApplyLabelFallback(_instructions, 13f, new Color(0.72f, 0.80f, 0.86f, 1f), FontStyle.Normal);
            ApplyLabelFallback(_actionLabel, 14f, Color.white, FontStyle.Bold);
            ApplyLabelFallback(_speedLabel, 13f, Color.white, FontStyle.Normal);
            ApplyLabelFallback(_clipLabel, 13f, Color.white, FontStyle.Normal);
            ApplyLabelFallback(_layerLabel, 13f, Color.white, FontStyle.Normal);
            ApplyLabelFallback(_warmupLabel, 13f, Color.white, FontStyle.Normal);
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
            MxAnimationLayerDiagnostic upper = FindLayer(animation, new MxAnimationLayerId(UpperBodyLayerId));
            ResourceDebugSnapshot resources = _resourceManager?.CreateDebugSnapshot();
            CombatMxAnimationBridgeDiagnosticSnapshot bridge = _bridge?.CreateSnapshot();

            SetText(_title, "MxAnimation 1D Locomotion Blend");
            SetText(_instructions, "I idle | O walk | P run | Space upper attack");
            SetText(_actionLabel, "Action: " + _currentAction + " | frame " + _worldFrame.Value + " | " + _lastRequest);
            SetText(_speedLabel, "Speed: " + _speedName + " | " + _speedParameter.ParameterId + "=" + _speedParameter.Value.ToString("0.00"));
            SetText(_clipLabel, layer != null
                ? "Blend: " + FormatBlendWeights(layer) + " | dominant " + layer.CurrentClipKey.Id
                : "Clip: loading");
            SetText(_layerLabel, "Layers: base="
                + (layer != null ? layer.LayerWeight.ToString("0.00") : "n/a")
                + " upper="
                + (upper != null ? upper.LayerWeight.ToString("0.00") : "n/a")
                + " upperClip="
                + (upper != null && upper.CurrentClipKey.IsValid ? upper.CurrentClipKey.Id : "none"));
            SetText(_warmupLabel, _warmupSummary);
            SetText(_backendLabel, animation != null
                ? "Backend: " + animation.BackendName + " graph=" + animation.GraphIsValid + " set=" + animation.SetId
                : "Backend: unavailable");
            SetText(_resourceLabel, resources != null
                ? "Resources: loaded=" + resources.LoadedCount + " refs=" + resources.TotalRefCount + " failed=" + resources.FailedCount
                : "Resources: unavailable");
            SetText(_bridgeLabel, bridge != null
                ? "Bridge: initialized=" + bridge.IsInitialized + " actors=" + bridge.ActorCount + " events=" + bridge.RecentEntries.Count + " " + FormatBridgeTail(bridge)
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

        private static bool TryResolveSpeed(InputIntent intent, out int quantizedSpeed, out string displayName)
        {
            switch (intent)
            {
                case InputIntent.DebugPrimary:
                    displayName = "Idle";
                    quantizedSpeed = 0;
                    return true;
                case InputIntent.AttackPrimary:
                    displayName = "Walk";
                    quantizedSpeed = 500;
                    return true;
                case InputIntent.AttackSecondary:
                    displayName = "Run";
                    quantizedSpeed = 1000;
                    return true;
                default:
                    displayName = string.Empty;
                    quantizedSpeed = 0;
                    return false;
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
            return FindLayer(snapshot, MxAnimationLayerId.Base);
        }

        private static MxAnimationLayerDiagnostic FindLayer(MxAnimationDiagnosticSnapshot snapshot, MxAnimationLayerId layerId)
        {
            if (snapshot == null)
                return null;

            for (int i = 0; i < snapshot.LayerStates.Count; i++)
            {
                if (snapshot.LayerStates[i].LayerId == layerId)
                    return snapshot.LayerStates[i];
            }

            return null;
        }

        private static string FormatBlendWeights(MxAnimationLayerDiagnostic layer)
        {
            if (layer == null || layer.Blend1DWeights.Count == 0)
                return "none";

            var text = string.Empty;
            for (int i = 0; i < layer.Blend1DWeights.Count; i++)
            {
                MxAnimationBlend1DWeight weight = layer.Blend1DWeights[i];
                if (text.Length > 0)
                    text += " | ";
                text += ShortClipName(weight.ClipKey.Id) + "=" + weight.Weight.ToString("0.00");
            }

            return text;
        }

        private static string FormatBridgeTail(CombatMxAnimationBridgeDiagnosticSnapshot bridge)
        {
            if (bridge == null || bridge.RecentEntries.Count == 0)
                return string.Empty;

            CombatMxAnimationBridgeDiagnosticEntry entry = bridge.RecentEntries[bridge.RecentEntries.Count - 1];
            return "last=" + entry.EventKind + ":" + entry.BindingId;
        }

        private static string ShortClipName(string clipId)
        {
            if (string.IsNullOrWhiteSpace(clipId))
                return "none";

            int index = clipId.LastIndexOf('.');
            return index >= 0 && index + 1 < clipId.Length ? clipId.Substring(index + 1) : clipId;
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

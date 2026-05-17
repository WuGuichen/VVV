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

namespace MxFramework.Demo.MxAnimationShowcase
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Demo/MxAnimation Showcase Demo Bootstrap")]
    public sealed class MxAnimationShowcaseDemoBootstrap : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/MxAnimationSystemShowcase.unity";
        public const string LocomotionBlendId = "showcase.locomotion.1d";
        public const string DirectionalBlendId = "showcase.directional.2d";
        public const string SpeedParameterId = "showcase.speed";
        public const string DirectionXParameterId = "showcase.direction.x";
        public const string DirectionYParameterId = "showcase.direction.y";
        public const int UpperAttackActionId = 9301;

        private const string SetId = "mxanimation.showcase.skeleton";
        private const string UpperBodyLayerId = "upper_body";
        private const string LocomotionActorId = "showcase.actor.locomotion";
        private const string DirectionalActorId = "showcase.actor.directional";
        private const string LayerActorId = "showcase.actor.layer";
        private const string OverrideActorId = "showcase.actor.override";
        private const string BakeReportId = "art.character.skeleton.bake.standing_jump";
        private const string CompatibilityProfileId = "art.character.skeleton.profile.humanoid";
        private const string CatalogHash = "mxanimation.showcase.catalog";
        private const int TicksPerSecond = 30;
        private const float FixedDeltaTime = 1f / TicksPerSecond;
        private const int MaxEvents = 10;

        private static readonly CombatEntityId LayerEntityId = new CombatEntityId(30);
        private static readonly PropertyInfo KeyboardCurrentProperty =
            Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem")?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);

        [SerializeField] private DefaultInputService _inputService;
        [SerializeField] private UIDocument _document;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private StyleSheet _styleSheet;
        [SerializeField] private Transform _locomotionAnchor;
        [SerializeField] private Transform _directionalAnchor;
        [SerializeField] private Transform _layerAnchor;
        [SerializeField] private Transform _overrideAnchor;
        [SerializeField] private GameObject _skeletonModel;
        [SerializeField] private AnimationClip _idleClip;
        [SerializeField] private AnimationClip _walkForwardClip;
        [SerializeField] private AnimationClip _walkBackClip;
        [SerializeField] private AnimationClip _walkLeftClip;
        [SerializeField] private AnimationClip _walkRightClip;
        [SerializeField] private AnimationClip _runForwardClip;
        [SerializeField] private AnimationClip _runBackClip;
        [SerializeField] private AnimationClip _runLeftClip;
        [SerializeField] private AnimationClip _runRightClip;
        [SerializeField] private AnimationClip _sprintForwardClip;
        [SerializeField] private AnimationClip _jumpClip;
        [SerializeField] private AnimationClip _jumpRunningClip;
        [SerializeField] private AnimationClip _jumpRunningLandingClip;
        [SerializeField] private AnimationClip _landToIdleClip;
        [SerializeField] private AnimationClip _turnLeft90Clip;
        [SerializeField] private AnimationClip _turnRight90Clip;
        [SerializeField] private AvatarMask _upperBodyMask;
        [SerializeField] private TextAsset _bakeReport;

        private readonly List<ShowcaseActor> _actors = new List<ShowcaseActor>();
        private readonly List<InputCommand> _drainedCommands = new List<InputCommand>();
        private readonly List<string> _events = new List<string>();

        private ResourceCatalog _catalog;
        private ResourceManager _resourceManager;
        private MemoryResourceProvider _provider;
        private ResourceHandle<GameObject> _modelHandle;
        private MxAnimationSetDefinition _baseDefinition;
        private MxAnimationSetDefinition _showcaseDefinition;
        private MxAnimationClipRegistry _clipRegistry;
        private MxAnimationCompatibilityProfile _compatibilityProfile;
        private MxAnimationPackageExpectation _packageExpectation;
        private MxAnimationPackageCatalog _packageCatalog;
        private MxAnimationModOverrideMergeResult _modMergeResult;
        private MxAnimationWarmupService _warmupService;
        private MxAnimationWarmupResult _warmupResult;
        private CombatAnimationContext _combatContext;
        private CombatActionRegistry _actionRegistry;
        private CombatActionRunner _runner;
        private CombatMxAnimationUnityBridge _bridge;
        private CombatFrame _worldFrame = CombatFrame.Zero;
        private float _accumulator;
        private bool _autoCycle = true;
        private float _autoTimer;
        private int _autoStep;
        private int _speed = 0;
        private string _speedName = "Idle";
        private int _directionX;
        private int _directionY;
        private string _lastManualRequest = "Auto cycle active";
        private string _initializationError = string.Empty;
        private string _warmupSummary = "Warmup: not started";
        private string _packageSummary = "Package: not checked";
        private string _compatibilitySummary = "Compatibility: not checked";
        private string _modSummary = "Mod override: not checked";
        private string _bakeSummary = "Bake: not checked";
        private string _fallbackSummary = "Fallback: press F";

        private Label _title;
        private Label _mode;
        private Label _controls;
        private Label _locomotionLabel;
        private Label _directionalLabel;
        private Label _layerLabel;
        private Label _overrideLabel;
        private Label _packageLabel;
        private Label _compatibilityLabel;
        private Label _bakeLabel;
        private Label _cacheLabel;
        private Label _resourceLabel;
        private Label _bridgeLabel;
        private Label _errorLabel;
        private VisualElement _eventList;

        public bool IsInitialized { get; private set; }
        public bool HasInitializationError => !string.IsNullOrEmpty(_initializationError);
        public int ActorCount => _actors.Count;
        public MxAnimationWarmupResult WarmupResult => _warmupResult;
        public ResourceManager ResourceManager => _resourceManager;
        public MxAnimationModOverrideMergeResult ModMergeResult => _modMergeResult;

        public void InitializeForValidation()
        {
            if (IsInitialized)
                return;

            BindHud();
            InitializeShowcase();
        }

        public void DisposeForValidation()
        {
            OnDestroy();
        }

        private void Awake()
        {
            BindHud();
            InitializeShowcase();
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
            HandleDirectShowcaseKeys();
            UpdateDirectionalInput();
            TickAutoCycle();
            TickCombat();

            for (int i = 0; i < _actors.Count; i++)
                _actors[i].Backend?.Tick(Time.deltaTime);

            RefreshHud();
        }

        private void OnDestroy()
        {
            _bridge?.Dispose();
            if (_runner != null)
                _runner.ActionFinished -= OnActionFinished;

            for (int i = _actors.Count - 1; i >= 0; i--)
                _actors[i].Release();
            _actors.Clear();

            _warmupService?.Release(_warmupResult);
            if (_modelHandle != null)
                _resourceManager?.Release(_modelHandle);

            _bridge = null;
            _runner = null;
            _warmupService = null;
            _warmupResult = null;
            _resourceManager = null;
            _provider = null;
            _modelHandle = null;
            _baseDefinition = null;
            _showcaseDefinition = null;
            _clipRegistry = null;
            _compatibilityProfile = null;
            _packageExpectation = null;
            _packageCatalog = null;
            _modMergeResult = null;
            IsInitialized = false;
        }

        public void ConfigureSceneReferences(
            DefaultInputService inputService,
            UIDocument document,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Transform locomotionAnchor,
            Transform directionalAnchor,
            Transform layerAnchor,
            Transform overrideAnchor,
            GameObject skeletonModel,
            AnimationClip idleClip,
            AnimationClip walkForwardClip,
            AnimationClip walkBackClip,
            AnimationClip walkLeftClip,
            AnimationClip walkRightClip,
            AnimationClip runForwardClip,
            AnimationClip runBackClip,
            AnimationClip runLeftClip,
            AnimationClip runRightClip,
            AnimationClip sprintForwardClip,
            AnimationClip jumpClip,
            AnimationClip jumpRunningClip,
            AnimationClip jumpRunningLandingClip,
            AnimationClip landToIdleClip,
            AnimationClip turnLeft90Clip,
            AnimationClip turnRight90Clip,
            AvatarMask upperBodyMask,
            TextAsset bakeReport)
        {
            _inputService = inputService;
            _document = document;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
            _locomotionAnchor = locomotionAnchor;
            _directionalAnchor = directionalAnchor;
            _layerAnchor = layerAnchor;
            _overrideAnchor = overrideAnchor;
            _skeletonModel = skeletonModel;
            _idleClip = idleClip;
            _walkForwardClip = walkForwardClip;
            _walkBackClip = walkBackClip;
            _walkLeftClip = walkLeftClip;
            _walkRightClip = walkRightClip;
            _runForwardClip = runForwardClip;
            _runBackClip = runBackClip;
            _runLeftClip = runLeftClip;
            _runRightClip = runRightClip;
            _sprintForwardClip = sprintForwardClip;
            _jumpClip = jumpClip;
            _jumpRunningClip = jumpRunningClip;
            _jumpRunningLandingClip = jumpRunningLandingClip;
            _landToIdleClip = landToIdleClip;
            _turnLeft90Clip = turnLeft90Clip;
            _turnRight90Clip = turnRight90Clip;
            _upperBodyMask = upperBodyMask;
            _bakeReport = bakeReport;
        }

        private void InitializeShowcase()
        {
            try
            {
                _inputService = _inputService != null ? _inputService : GetComponent<DefaultInputService>();
                _document = _document != null ? _document : GetComponent<UIDocument>();

                _catalog = CreateShowcaseCatalog();
                _provider = new MemoryResourceProvider();
                RegisterSerializedAssets();

                _resourceManager = new ResourceManager();
                _resourceManager.RegisterProvider(_provider);
                _resourceManager.AddCatalog(_catalog);
                _resourceManager.ValidateCatalogs();

                LoadSharedModel();
                _baseDefinition = CreateBaseDefinition();
                _clipRegistry = MxAnimationClipRegistryBuilder.FromCatalog(_catalog, version: 1, catalogHash: CatalogHash);
                _compatibilityProfile = CreateCompatibilityProfile();
                _packageExpectation = CreatePackageExpectation();
                _packageCatalog = new MxAnimationPackageCatalog(
                    _catalog,
                    version: 1,
                    catalogHash: CatalogHash,
                    packageId: TempImportedResourceCatalog.PackageId,
                    catalogId: TempImportedResourceCatalog.CatalogId);
                _modMergeResult = CreateModOverride(_baseDefinition, _compatibilityProfile, _packageCatalog, _packageExpectation);
                _showcaseDefinition = _modMergeResult != null && _modMergeResult.Success
                    ? _modMergeResult.MergedDefinition
                    : _baseDefinition;

                ValidateShowcaseContracts();
                Warmup();
                CreateActors();
                SetupCombatBridge();

                SetLocomotionSpeed(0, "Idle", false);
                SetDirectional(0, 0, false);
                PlayBinding(GetActor(OverrideActorId), "walk", "Override actor base walk");
                IsInitialized = true;
                AddEvent("MxAnimation showcase initialized through ResourceCatalog, warmup, Playables backend, and Combat bridge.");
            }
            catch (Exception ex)
            {
                _initializationError = ex.Message;
                AddEvent("Initialization failed: " + ex.Message);
                Debug.LogError("MxAnimation showcase initialization failed: " + ex);
            }

            RefreshHud();
        }

        private static ResourceCatalog CreateShowcaseCatalog()
        {
            ResourceCatalog baseCatalog = TempImportedResourceCatalog.CreateCatalog();
            var entries = new List<ResourceCatalogEntry>(baseCatalog.Entries);
            entries.Add(new ResourceCatalogEntry(
                BakeReportId,
                MxAnimationResourceTypeIds.BakeArtifact,
                TempImportedResourceCatalog.MemoryProviderId,
                "mxframework.samples/art/character/skeleton/bake/standing_jump",
                packageId: TempImportedResourceCatalog.PackageId,
                labels: new[] { TempImportedResourceCatalog.PackageLabel, TempImportedResourceCatalog.WarmupMxAnimationLabel }));
            entries.Add(new ResourceCatalogEntry(
                CompatibilityProfileId,
                MxAnimationResourceTypeIds.CompatibilityProfile,
                TempImportedResourceCatalog.MemoryProviderId,
                "mxframework.samples/art/character/skeleton/profile/humanoid",
                packageId: TempImportedResourceCatalog.PackageId,
                labels: new[] { TempImportedResourceCatalog.PackageLabel, TempImportedResourceCatalog.WarmupMxAnimationLabel }));
            return new ResourceCatalog(baseCatalog.CatalogId, baseCatalog.PackageId, entries);
        }

        private void RegisterSerializedAssets()
        {
            RegisterSerializedAsset(TempImportedResourceCatalog.SkeletonModelId, ResourceTypeIds.GameObject, _skeletonModel);
            RegisterSerializedAsset(TempImportedResourceCatalog.SkeletonIdleAnimationId, ResourceTypeIds.AnimationClip, _idleClip);
            RegisterSerializedAsset(TempImportedResourceCatalog.SkeletonWalkForwardAnimationId, ResourceTypeIds.AnimationClip, _walkForwardClip);
            RegisterSerializedAsset("art.character.skeleton.animation.standing_walk_back", ResourceTypeIds.AnimationClip, _walkBackClip);
            RegisterSerializedAsset("art.character.skeleton.animation.standing_walk_left", ResourceTypeIds.AnimationClip, _walkLeftClip);
            RegisterSerializedAsset("art.character.skeleton.animation.standing_walk_right", ResourceTypeIds.AnimationClip, _walkRightClip);
            RegisterSerializedAsset(TempImportedResourceCatalog.SkeletonRunForwardAnimationId, ResourceTypeIds.AnimationClip, _runForwardClip);
            RegisterSerializedAsset("art.character.skeleton.animation.standing_run_back", ResourceTypeIds.AnimationClip, _runBackClip);
            RegisterSerializedAsset("art.character.skeleton.animation.standing_run_left", ResourceTypeIds.AnimationClip, _runLeftClip);
            RegisterSerializedAsset("art.character.skeleton.animation.standing_run_right", ResourceTypeIds.AnimationClip, _runRightClip);
            RegisterSerializedAsset("art.character.skeleton.animation.standing_sprint_forward", ResourceTypeIds.AnimationClip, _sprintForwardClip);
            RegisterSerializedAsset(TempImportedResourceCatalog.SkeletonJumpAnimationId, ResourceTypeIds.AnimationClip, _jumpClip);
            RegisterSerializedAsset("art.character.skeleton.animation.standing_jump_running", ResourceTypeIds.AnimationClip, _jumpRunningClip);
            RegisterSerializedAsset("art.character.skeleton.animation.standing_jump_running_landing", ResourceTypeIds.AnimationClip, _jumpRunningLandingClip);
            RegisterSerializedAsset("art.character.skeleton.animation.standing_land_to_standing_idle", ResourceTypeIds.AnimationClip, _landToIdleClip);
            RegisterSerializedAsset("art.character.skeleton.animation.standing_turn_left_90", ResourceTypeIds.AnimationClip, _turnLeft90Clip);
            RegisterSerializedAsset("art.character.skeleton.animation.standing_turn_right_90", ResourceTypeIds.AnimationClip, _turnRight90Clip);
            RegisterSerializedAsset(TempImportedResourceCatalog.SkeletonUpperBodyMaskId, ResourceTypeIds.AvatarMask, _upperBodyMask);
            RegisterSerializedAsset(BakeReportId, MxAnimationResourceTypeIds.BakeArtifact, _bakeReport);
            RegisterSerializedAsset(CompatibilityProfileId, MxAnimationResourceTypeIds.CompatibilityProfile, _bakeReport);
        }

        private void RegisterSerializedAsset(string id, string typeId, UnityEngine.Object asset)
        {
            if (asset == null)
                throw new InvalidOperationException("Showcase scene missing serialized resource asset for " + id + ".");

            ResourceCatalogEntry entry = FindEntry(_catalog, id, typeId);
            _provider.Register(entry.Address, asset);
        }

        private void LoadSharedModel()
        {
            ResourceLoadResult<ResourceHandle<GameObject>> result =
                _resourceManager.Load<GameObject>(Key(TempImportedResourceCatalog.SkeletonModelId, ResourceTypeIds.GameObject));
            if (!result.Success)
                throw new InvalidOperationException("Skeleton model load failed: " + result.Error.Message);

            _modelHandle = result.Value;
        }

        private void CreateActors()
        {
            _actors.Add(CreateActor(LocomotionActorId, _locomotionAnchor, "Locomotion 1D", _showcaseDefinition));
            _actors.Add(CreateActor(DirectionalActorId, _directionalAnchor, "Directional 2D", _showcaseDefinition));
            _actors.Add(CreateActor(LayerActorId, _layerAnchor, "Layer + Combat Bridge", _showcaseDefinition));
            _actors.Add(CreateActor(OverrideActorId, _overrideAnchor, "Mod Override", _showcaseDefinition));
        }

        private ShowcaseActor CreateActor(string actorId, Transform anchor, string displayName, MxAnimationSetDefinition definition)
        {
            if (_modelHandle == null || _modelHandle.Value == null)
                throw new InvalidOperationException("Shared skeleton model is not loaded.");

            Transform parent = anchor != null ? anchor : transform;
            GameObject instance = Instantiate(_modelHandle.Value, parent);
            instance.name = displayName + "_Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            Animator animator = instance.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            animator.applyRootMotion = false;

            var backend = new UnityPlayablesAnimationBackend(animator, _resourceManager, definition, actorId);
            return new ShowcaseActor(actorId, displayName, instance, animator, backend);
        }

        private void SetupCombatBridge()
        {
            _combatContext = new CombatAnimationContext();
            _actionRegistry = new CombatActionRegistry();
            _actionRegistry.RegisterTimeline(UpperAttackActionId, new CombatActionTimeline(
                UpperAttackActionId,
                totalFrames: 32,
                startup: new CombatFrameRange(0, 5),
                active: new CombatFrameRange(6, 13),
                recovery: new CombatFrameRange(14, 31),
                windows: null,
                events: new[] { new CombatActionFrameEvent(8, 77, sourceOrder: 1, intPayload: 1) }));
            _runner = new CombatActionRunner(_actionRegistry);
            _combatContext.SetActionRunner(_runner);
            _runner.ActionFinished += OnActionFinished;

            var options = new CombatMxAnimationBridgeOptions
            {
                StartRequestKind = CombatMxAnimationStartRequestKind.CrossFade,
                FinishedRequestKind = CombatMxAnimationEndRequestKind.Stop,
                CanceledRequestKind = CombatMxAnimationEndRequestKind.Stop,
                StartCrossFadeDurationSeconds = 0.12f,
                StopFadeOutDurationSeconds = 0.12f
            };
            _bridge = new CombatMxAnimationUnityBridge(_combatContext, options, new ShowcasePresentationEventSink(this));
            ShowcaseActor layerActor = GetActor(LayerActorId);
            _bridge.RegisterActor(LayerEntityId, layerActor.Backend, _showcaseDefinition, LayerActorId);
            _bridge.Initialize();
        }

        private void Warmup()
        {
            _warmupService = new MxAnimationWarmupService(new ResourcePreloadService(_resourceManager));
            _warmupResult = _warmupService.Warmup(new MxAnimationWarmupRequest(
                _showcaseDefinition,
                _clipRegistry,
                _catalog,
                null,
                null,
                true,
                _compatibilityProfile,
                _packageExpectation,
                _packageCatalog));

            _warmupSummary = _warmupResult.Success
                ? "Warmup: ready keys=" + _warmupResult.RequiredKeys.Count + " labels=" + _warmupResult.Labels.Count
                : "Warmup: failed issues=" + _warmupResult.Issues.Count;
            for (int i = 0; i < _warmupResult.Issues.Count; i++)
                AddEvent("Warmup issue: " + _warmupResult.Issues[i].Code + " " + _warmupResult.Issues[i].Field);
        }

        private void ValidateShowcaseContracts()
        {
            MxAnimationPackageValidationReport packageReport =
                MxAnimationPackageCatalogValidator.Validate(_packageCatalog, _packageExpectation);
            _packageSummary = !packageReport.Success
                ? "Package: rejected issues=" + packageReport.Issues.Count
                : "Package: OK provider=memory clip/mask/bake/profile";

            MxAnimationCompatibilityValidationReport compatibilityReport =
                MxAnimationCompatibilityValidator.Validate(_compatibilityProfile, _showcaseDefinition.CompatibilityExpectation);
            _compatibilitySummary = compatibilityReport.HasErrors
                ? "Compatibility: rejected issues=" + compatibilityReport.Issues.Count
                : "Compatibility: OK skeleton/profile/clip/mask paths";

            _modSummary = _modMergeResult != null && _modMergeResult.Success
                ? "Mod override: OK overrideHash=" + ShortHash(_modMergeResult.OverrideHash) + " mergedHash=" + ShortHash(_modMergeResult.MergedDefinition.DefinitionHash)
                : "Mod override: rejected issues=" + (_modMergeResult != null ? _modMergeResult.Issues.Count : 0);

            _bakeSummary = _bakeReport != null && _bakeReport.text.Contains("success: true")
                ? "Bake: OK " + ExtractBakeLine("artifactHash")
                : "Bake: report missing or failed";
        }

        private MxAnimationSetDefinition CreateBaseDefinition()
        {
            var upperLayer = new MxAnimationLayerId(UpperBodyLayerId);
            ResourceKey idle = Key(TempImportedResourceCatalog.SkeletonIdleAnimationId, ResourceTypeIds.AnimationClip);
            ResourceKey walkForward = Key(TempImportedResourceCatalog.SkeletonWalkForwardAnimationId, ResourceTypeIds.AnimationClip);
            ResourceKey walkBack = Key("art.character.skeleton.animation.standing_walk_back", ResourceTypeIds.AnimationClip);
            ResourceKey walkLeft = Key("art.character.skeleton.animation.standing_walk_left", ResourceTypeIds.AnimationClip);
            ResourceKey walkRight = Key("art.character.skeleton.animation.standing_walk_right", ResourceTypeIds.AnimationClip);
            ResourceKey runForward = Key(TempImportedResourceCatalog.SkeletonRunForwardAnimationId, ResourceTypeIds.AnimationClip);
            ResourceKey sprint = Key("art.character.skeleton.animation.standing_sprint_forward", ResourceTypeIds.AnimationClip);
            ResourceKey jump = Key(TempImportedResourceCatalog.SkeletonJumpAnimationId, ResourceTypeIds.AnimationClip);
            ResourceKey mask = Key(TempImportedResourceCatalog.SkeletonUpperBodyMaskId, ResourceTypeIds.AvatarMask);
            ResourceKey bake = Key(BakeReportId, MxAnimationResourceTypeIds.BakeArtifact);
            ResourceKey profile = Key(CompatibilityProfileId, MxAnimationResourceTypeIds.CompatibilityProfile);

            return new MxAnimationSetDefinition(
                SetId,
                1,
                idle,
                idle,
                new[]
                {
                    Binding("idle", "action:idle", idle, MxAnimationLayerId.Base, true),
                    Binding("walk", "action:walk", walkForward, MxAnimationLayerId.Base, true),
                    Binding("run", "action:run", runForward, MxAnimationLayerId.Base, true),
                    Binding("sprint", "action:sprint", sprint, MxAnimationLayerId.Base, true),
                    Binding("override_showcase", "action:override_showcase", walkForward, MxAnimationLayerId.Base, true),
                    new MxAnimationActionBinding(
                        "upper_attack",
                        "action:" + UpperAttackActionId,
                        jump,
                        upperLayer,
                        playbackSpeed: 1.15f,
                        loop: false,
                        alignmentPolicy: MxAnimationAlignmentPolicy.UseCombatFrameAnchor,
                        presentationEvents: new[]
                        {
                            new MxAnimationPresentationEvent(
                                "event:77",
                                MxAnimationEventTimeDomain.CombatFrame,
                                8f,
                                "showcase.slash",
                                new ResourceKey("vfx.showcase.slash", "VFX", packageId: TempImportedResourceCatalog.PackageId),
                                socket: "weapon",
                                tag: "upper-attack")
                        },
                        fadeDurationSeconds: 0.12f)
                },
                layers: new[]
                {
                    new MxAnimationLayerDefinition(MxAnimationLayerId.Base, "showcase.base", 1f),
                    new MxAnimationLayerDefinition(upperLayer, "showcase.upper", 0f, MxAnimationLayerBlendMode.Override, mask)
                },
                warmup: new MxAnimationWarmupDefinition(
                    "mxanimation.showcase",
                    requiredKeys: new[] { bake, profile },
                    labels: new[] { TempImportedResourceCatalog.WarmupMxAnimationLabel }),
                blend1DDefinitions: new[]
                {
                    new MxAnimationBlend1DDefinition(
                        LocomotionBlendId,
                        SpeedParameterId,
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend1DPoint(0, idle, loop: true),
                            new MxAnimationBlend1DPoint(500, walkForward, loop: true),
                            new MxAnimationBlend1DPoint(1000, runForward, loop: true)
                        },
                        parameterScale: 1000,
                        fadeDurationSeconds: 0.12f)
                },
                blend2DDefinitions: new[]
                {
                    new MxAnimationBlend2DDefinition(
                        DirectionalBlendId,
                        DirectionXParameterId,
                        DirectionYParameterId,
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend2DPoint(0, 0, idle, loop: true),
                            new MxAnimationBlend2DPoint(0, 1000, walkForward, loop: true),
                            new MxAnimationBlend2DPoint(0, -1000, walkBack, loop: true),
                            new MxAnimationBlend2DPoint(-1000, 0, walkLeft, loop: true),
                            new MxAnimationBlend2DPoint(1000, 0, walkRight, loop: true)
                        },
                        parameterXScale: 1000,
                        parameterYScale: 1000,
                        fadeDurationSeconds: 0.12f)
                },
                compatibilityExpectation: CreateCompatibilityExpectation());
        }

        private MxAnimationPackageExpectation CreatePackageExpectation()
        {
            return new MxAnimationPackageExpectation(
                TempImportedResourceCatalog.PackageId,
                version: 1,
                catalogId: TempImportedResourceCatalog.CatalogId,
                catalogHash: CatalogHash,
                acceptedProviderIds: new[] { TempImportedResourceCatalog.MemoryProviderId },
                resources: new[]
                {
                    new MxAnimationPackageResourceExpectation(Key(TempImportedResourceCatalog.SkeletonIdleAnimationId, ResourceTypeIds.AnimationClip), providerId: TempImportedResourceCatalog.MemoryProviderId),
                    new MxAnimationPackageResourceExpectation(Key(TempImportedResourceCatalog.SkeletonWalkForwardAnimationId, ResourceTypeIds.AnimationClip), providerId: TempImportedResourceCatalog.MemoryProviderId),
                    new MxAnimationPackageResourceExpectation(Key("art.character.skeleton.animation.standing_walk_back", ResourceTypeIds.AnimationClip), providerId: TempImportedResourceCatalog.MemoryProviderId),
                    new MxAnimationPackageResourceExpectation(Key("art.character.skeleton.animation.standing_walk_left", ResourceTypeIds.AnimationClip), providerId: TempImportedResourceCatalog.MemoryProviderId),
                    new MxAnimationPackageResourceExpectation(Key("art.character.skeleton.animation.standing_walk_right", ResourceTypeIds.AnimationClip), providerId: TempImportedResourceCatalog.MemoryProviderId),
                    new MxAnimationPackageResourceExpectation(Key(TempImportedResourceCatalog.SkeletonRunForwardAnimationId, ResourceTypeIds.AnimationClip), providerId: TempImportedResourceCatalog.MemoryProviderId),
                    new MxAnimationPackageResourceExpectation(Key("art.character.skeleton.animation.standing_sprint_forward", ResourceTypeIds.AnimationClip), providerId: TempImportedResourceCatalog.MemoryProviderId),
                    new MxAnimationPackageResourceExpectation(Key(TempImportedResourceCatalog.SkeletonUpperBodyMaskId, ResourceTypeIds.AvatarMask), providerId: TempImportedResourceCatalog.MemoryProviderId),
                    new MxAnimationPackageResourceExpectation(Key(BakeReportId, MxAnimationResourceTypeIds.BakeArtifact), providerId: TempImportedResourceCatalog.MemoryProviderId),
                    new MxAnimationPackageResourceExpectation(Key(CompatibilityProfileId, MxAnimationResourceTypeIds.CompatibilityProfile), providerId: TempImportedResourceCatalog.MemoryProviderId)
                });
        }

        private MxAnimationCompatibilityProfile CreateCompatibilityProfile()
        {
            var skeleton = new MxAnimationSkeletonCompatibilityProfile(
                "showcase.humanoid",
                "sha256:showcase-skeleton",
                new[] { "Hips", "Hips/Spine", "Hips/Spine/Chest", "Hips/Spine/Chest/Head" },
                new[] { "Hips/Spine/Chest/RightShoulder/RightArm/RightForeArm/RightHand" });
            ResourceKey idle = Key(TempImportedResourceCatalog.SkeletonIdleAnimationId, ResourceTypeIds.AnimationClip);
            ResourceKey walk = Key(TempImportedResourceCatalog.SkeletonWalkForwardAnimationId, ResourceTypeIds.AnimationClip);
            ResourceKey sprint = Key("art.character.skeleton.animation.standing_sprint_forward", ResourceTypeIds.AnimationClip);
            ResourceKey mask = Key(TempImportedResourceCatalog.SkeletonUpperBodyMaskId, ResourceTypeIds.AvatarMask);
            return new MxAnimationCompatibilityProfile(
                skeleton,
                new[]
                {
                    new MxAnimationClipCompatibilityProfile(idle, skeleton.ProfileId, skeleton.ProfileHash, new[] { "Hips", "Hips/Spine" }),
                    new MxAnimationClipCompatibilityProfile(walk, skeleton.ProfileId, skeleton.ProfileHash, new[] { "Hips", "Hips/Spine" }),
                    new MxAnimationClipCompatibilityProfile(sprint, skeleton.ProfileId, skeleton.ProfileHash, new[] { "Hips", "Hips/Spine" })
                },
                new[]
                {
                    new MxAnimationAvatarMaskCompatibilityProfile(mask, skeleton.ProfileId, skeleton.ProfileHash, new[] { "Hips/Spine", "Hips/Spine/Chest" })
                });
        }

        private static MxAnimationCompatibilityExpectation CreateCompatibilityExpectation()
        {
            return new MxAnimationCompatibilityExpectation(
                "showcase.humanoid",
                "sha256:showcase-skeleton",
                new[] { "Hips", "Hips/Spine" },
                new[] { "Hips/Spine/Chest/RightShoulder/RightArm/RightForeArm/RightHand" },
                new[]
                {
                    new MxAnimationClipCompatibilityExpectation(Key(TempImportedResourceCatalog.SkeletonIdleAnimationId, ResourceTypeIds.AnimationClip), new[] { "Hips", "Hips/Spine" }),
                    new MxAnimationClipCompatibilityExpectation(Key(TempImportedResourceCatalog.SkeletonWalkForwardAnimationId, ResourceTypeIds.AnimationClip), new[] { "Hips", "Hips/Spine" }),
                    new MxAnimationClipCompatibilityExpectation(Key("art.character.skeleton.animation.standing_sprint_forward", ResourceTypeIds.AnimationClip), new[] { "Hips", "Hips/Spine" })
                },
                new[]
                {
                    new MxAnimationAvatarMaskCompatibilityExpectation(Key(TempImportedResourceCatalog.SkeletonUpperBodyMaskId, ResourceTypeIds.AvatarMask), new[] { "Hips/Spine" })
                });
        }

        private static MxAnimationModOverrideMergeResult CreateModOverride(
            MxAnimationSetDefinition baseDefinition,
            MxAnimationCompatibilityProfile compatibilityProfile,
            MxAnimationPackageCatalog packageCatalog,
            MxAnimationPackageExpectation basePackageExpectation)
        {
            ResourceKey sprint = Key("art.character.skeleton.animation.standing_sprint_forward", ResourceTypeIds.AnimationClip);
            var manifest = new MxAnimationModPackageManifest(
                TempImportedResourceCatalog.PackageId,
                version: 1,
                displayName: "Showcase Sprint Override",
                catalogId: TempImportedResourceCatalog.CatalogId,
                catalogHash: CatalogHash,
                loadOrder: 10);
            var overrideDefinition = new MxAnimationModOverrideDefinition(
                baseDefinition.SetId,
                manifest,
                overrideVersion: 1,
                expectedBaseVersion: baseDefinition.Version,
                expectedBaseHash: baseDefinition.DefinitionHash,
                actionOverrides: new[]
                {
                    new MxAnimationActionBindingOverride(Binding(
                        "override_showcase",
                        "action:override_showcase",
                        sprint,
                        MxAnimationLayerId.Base,
                        true,
                        playbackSpeed: 1.12f,
                        fadeDurationSeconds: 0.1f))
                },
                compatibilityExpectation: new MxAnimationCompatibilityExpectation(
                    "showcase.humanoid",
                    "sha256:showcase-skeleton",
                    new[] { "Hips", "Hips/Spine" },
                    null,
                    new[] { new MxAnimationClipCompatibilityExpectation(sprint, new[] { "Hips", "Hips/Spine" }) }),
                acceptedProviderIds: new[] { TempImportedResourceCatalog.MemoryProviderId });

            return MxAnimationModOverrideMerger.Merge(new MxAnimationModOverrideMergeRequest(
                baseDefinition,
                overrideDefinition,
                packageCatalog != null ? packageCatalog.Catalog : null,
                compatibilityProfile,
                packageCatalog,
                basePackageExpectation));
        }

        private void EnqueueKeyboardCommands(long frame)
        {
            TryEnqueue(frame, "iKey", InputIntent.DebugPrimary, "MxAnimationShowcase.I");
            TryEnqueue(frame, "oKey", InputIntent.AttackPrimary, "MxAnimationShowcase.O");
            TryEnqueue(frame, "pKey", InputIntent.AttackSecondary, "MxAnimationShowcase.P");
            TryEnqueue(frame, "spaceKey", InputIntent.Jump, "MxAnimationShowcase.Space");
        }

        private void TryEnqueue(long frame, string keyPropertyName, InputIntent intent, string traceId)
        {
            if (_inputService == null || !IsKeyboardKeyPressedThisFrame(keyPropertyName))
                return;

            _inputService.Commands.TryEnqueue(new InputCommand(
                frame,
                sourceId: 130,
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
                    SetLocomotionSpeed(speed, speedName, true);
                    _autoCycle = false;
                    continue;
                }

                if (command.Intent == InputIntent.Jump)
                {
                    TriggerUpperAttack();
                    _autoCycle = false;
                }
            }
        }

        private void HandleDirectShowcaseKeys()
        {
            if (IsKeyboardKeyPressedThisFrame("hKey"))
            {
                _autoCycle = !_autoCycle;
                _lastManualRequest = _autoCycle ? "Auto cycle resumed" : "Auto cycle paused";
                AddEvent(_lastManualRequest);
            }

            if (IsKeyboardKeyPressedThisFrame("mKey"))
            {
                PlayBinding(GetActor(OverrideActorId), "override_showcase", "Mod override sprint");
                _lastManualRequest = "M -> merged mod override binding";
                AddEvent("Mod override binding played from merged mapping.");
            }

            if (IsKeyboardKeyPressedThisFrame("fKey"))
            {
                MxAnimationBackendResult result = GetActor(OverrideActorId).Backend.CrossFade(new MxAnimationCrossFadeRequest
                {
                    ClipKey = Key("missing.animation.clip", ResourceTypeIds.AnimationClip),
                    LayerId = MxAnimationLayerId.Base,
                    FadeDurationSeconds = 0.1f,
                    Loop = false,
                    CorrelationId = "showcase.fallback"
                });
                _fallbackSummary = result.Success
                    ? "Fallback: requested missing clip; check recent request diagnostics"
                    : "Fallback: rejected " + result.Message;
                _lastManualRequest = "F -> missing clip fallback probe";
                AddEvent(_fallbackSummary);
            }

            if (IsKeyboardKeyPressedThisFrame("rKey"))
            {
                SetLocomotionSpeed(0, "Idle", true);
                SetDirectional(0, 0, true);
                PlayBinding(GetActor(OverrideActorId), "walk", "Override actor base walk");
                _lastManualRequest = "R -> reset showcase actors";
                AddEvent(_lastManualRequest);
            }
        }

        private void UpdateDirectionalInput()
        {
            Vector2 move = _inputService != null ? _inputService.Snapshot.Move : Vector2.zero;
            if (move.sqrMagnitude < 0.02f)
                return;

            int x = Mathf.RoundToInt(Mathf.Clamp(move.x, -1f, 1f) * 1000f);
            int y = Mathf.RoundToInt(Mathf.Clamp(move.y, -1f, 1f) * 1000f);
            SetDirectional(x, y, true);
            _autoCycle = false;
        }

        private void TickAutoCycle()
        {
            if (!_autoCycle)
                return;

            _autoTimer += Time.deltaTime;
            float angle = _autoTimer * 1.35f;
            int x = Mathf.RoundToInt(Mathf.Cos(angle) * 1000f);
            int y = Mathf.RoundToInt(Mathf.Sin(angle) * 1000f);
            SetDirectional(x, y, false);

            if (_autoTimer < 1.75f)
                return;

            _autoTimer = 0f;
            _autoStep = (_autoStep + 1) % 4;
            switch (_autoStep)
            {
                case 0:
                    SetLocomotionSpeed(0, "Idle", false);
                    break;
                case 1:
                    SetLocomotionSpeed(500, "Walk", false);
                    break;
                case 2:
                    SetLocomotionSpeed(1000, "Run", false);
                    break;
                default:
                    TriggerUpperAttack();
                    break;
            }
        }

        private void SetLocomotionSpeed(int quantizedSpeed, string displayName, bool addEvent)
        {
            _speed = quantizedSpeed;
            _speedName = displayName ?? string.Empty;
            MxAnimationBackendResult result = GetActor(LocomotionActorId).Backend.SetBlend1D(new MxAnimationBlend1DRequest
            {
                BlendId = LocomotionBlendId,
                Parameter = new MxAnimationQuantizedParameter(SpeedParameterId, quantizedSpeed),
                CorrelationId = "showcase.speed:" + quantizedSpeed
            });

            _lastManualRequest = _speedName + " speed=" + (quantizedSpeed / 1000f).ToString("0.00");
            if (!result.Success)
            {
                AddEvent("1D speed rejected: " + result.Message);
                return;
            }

            if (addEvent)
                AddEvent("1D blend -> " + _speedName);
        }

        private void SetDirectional(int x, int y, bool addEvent)
        {
            if (_directionX == x && _directionY == y && !addEvent)
                return;

            _directionX = x;
            _directionY = y;
            MxAnimationBackendResult result = GetActor(DirectionalActorId).Backend.SetBlend2D(new MxAnimationBlend2DRequest
            {
                BlendId = DirectionalBlendId,
                ParameterX = new MxAnimationQuantizedParameter(DirectionXParameterId, x),
                ParameterY = new MxAnimationQuantizedParameter(DirectionYParameterId, y),
                CorrelationId = "showcase.direction:" + x + "," + y
            });

            if (!result.Success)
            {
                AddEvent("2D direction rejected: " + result.Message);
                return;
            }

            if (addEvent)
                AddEvent("2D blend -> x=" + x + " y=" + y);
        }

        private void TriggerUpperAttack()
        {
            ShowcaseActor actor = GetActor(LayerActorId);
            actor.Backend.SetLayerWeight(new MxAnimationLayerWeightRequest
            {
                LayerId = new MxAnimationLayerId(UpperBodyLayerId),
                Weight = 1f,
                FadeDurationSeconds = 0.08f,
                TransitionPolicyId = "showcase.upper.in",
                CorrelationId = "showcase.upper.in"
            });

            ActionResult result = _runner.ForceStartAction(LayerEntityId, UpperAttackActionId, _worldFrame);
            if (!result.Success)
            {
                AddEvent("Upper attack rejected: " + result.Reason);
                return;
            }

            _lastManualRequest = "Upper attack instance " + result.ActionInstanceId;
            AddEvent("Upper-body layer + Combat bridge -> on.");
        }

        private void OnActionFinished(ActionFinishedEvent evt)
        {
            if (evt.ActionId != UpperAttackActionId)
                return;

            ShowcaseActor actor = GetActor(LayerActorId);
            actor.Backend.SetLayerWeight(new MxAnimationLayerWeightRequest
            {
                LayerId = new MxAnimationLayerId(UpperBodyLayerId),
                Weight = 0f,
                FadeDurationSeconds = 0.12f,
                TransitionPolicyId = "showcase.upper.out",
                CorrelationId = "showcase.upper.out"
            });
            AddEvent("Upper-body layer + AvatarMask -> off.");
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

        private static MxAnimationActionBinding Binding(
            string bindingId,
            string actionKey,
            ResourceKey clip,
            MxAnimationLayerId layer,
            bool loop,
            float playbackSpeed = 1f,
            float fadeDurationSeconds = 0.12f)
        {
            return new MxAnimationActionBinding(
                bindingId,
                actionKey,
                clip,
                layer,
                playbackSpeed,
                loop,
                MxAnimationAlignmentPolicy.StartAtZero,
                null,
                fadeDurationSeconds);
        }

        private void PlayBinding(ShowcaseActor actor, string bindingId, string reason)
        {
            if (actor == null || actor.Backend == null)
                return;

            MxAnimationBackendResult result = actor.Backend.CrossFade(new MxAnimationCrossFadeRequest
            {
                BindingId = bindingId,
                FadeDurationSeconds = 0.12f,
                CorrelationId = "showcase.binding:" + bindingId
            });

            if (result.Success)
                AddEvent(reason + " -> " + bindingId);
            else
                AddEvent(reason + " rejected: " + result.Message);
        }

        private ShowcaseActor GetActor(string actorId)
        {
            for (int i = 0; i < _actors.Count; i++)
            {
                if (string.Equals(_actors[i].ActorId, actorId, StringComparison.Ordinal))
                    return _actors[i];
            }

            throw new InvalidOperationException("Showcase actor missing: " + actorId + ".");
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

        private void BindHud()
        {
            _document = _document != null ? _document : GetComponent<UIDocument>();
            VisualElement root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
                return;

            if (_styleSheet != null && !root.styleSheets.Contains(_styleSheet))
                root.styleSheets.Add(_styleSheet);

            _title = root.Q<Label>("title");
            _mode = root.Q<Label>("mode");
            _controls = root.Q<Label>("controls");
            _locomotionLabel = root.Q<Label>("locomotion");
            _directionalLabel = root.Q<Label>("directional");
            _layerLabel = root.Q<Label>("layer");
            _overrideLabel = root.Q<Label>("override");
            _packageLabel = root.Q<Label>("package");
            _compatibilityLabel = root.Q<Label>("compatibility");
            _bakeLabel = root.Q<Label>("bake");
            _cacheLabel = root.Q<Label>("cache");
            _resourceLabel = root.Q<Label>("resources");
            _bridgeLabel = root.Q<Label>("bridge");
            _errorLabel = root.Q<Label>("error");
            _eventList = root.Q<VisualElement>("events");

            ApplyLabelFallback(_title, 21f, Color.white, FontStyle.Bold);
            ApplyLabelFallback(_mode, 13f, new Color(0.75f, 0.86f, 0.92f, 1f), FontStyle.Bold);
            ApplyLabelFallback(_controls, 12f, new Color(0.70f, 0.78f, 0.84f, 1f), FontStyle.Normal);
        }

        private void RefreshHud()
        {
            if (_title == null)
                BindHud();

            MxAnimationDiagnosticSnapshot locomotion = Snapshot(LocomotionActorId);
            MxAnimationDiagnosticSnapshot directional = Snapshot(DirectionalActorId);
            MxAnimationDiagnosticSnapshot layer = Snapshot(LayerActorId);
            MxAnimationDiagnosticSnapshot overrideSnapshot = Snapshot(OverrideActorId);
            ResourceDebugSnapshot resources = _resourceManager?.CreateDebugSnapshot();
            CombatMxAnimationBridgeDiagnosticSnapshot bridge = _bridge?.CreateSnapshot();

            SetText(_title, "MxAnimation System Showcase");
            SetText(_mode, "Mode: " + (_autoCycle ? "auto cycle" : "manual") + " | frame " + _worldFrame.Value + " | " + _lastManualRequest);
            SetText(_controls, "I/O/P 1D idle/walk/run | WASD or arrows 2D direction | Space upper layer | M mod override | F fallback | H auto | R reset");
            SetText(_locomotionLabel, "1D Locomotion: speed=" + _speedName + " " + (_speed / 1000f).ToString("0.00") + " | " + FormatBlend1D(FindBaseLayer(locomotion)));
            SetText(_directionalLabel, "2D Directional: x=" + _directionX + " y=" + _directionY + " | " + FormatBlend2D(FindBaseLayer(directional)));
            SetText(_layerLabel, "Layer + Mask: " + FormatLayer(FindLayer(layer, new MxAnimationLayerId(UpperBodyLayerId))));
            SetText(_overrideLabel, "Mod/Fallback: " + _modSummary + " | " + _fallbackSummary + " | current=" + ShortClip(FindBaseLayer(overrideSnapshot)?.CurrentClipKey.Id));
            SetText(_packageLabel, _packageSummary + " | " + _warmupSummary);
            SetText(_compatibilityLabel, _compatibilitySummary);
            SetText(_bakeLabel, _bakeSummary);
            SetText(_cacheLabel, "Cache: " + FormatCache(locomotion, directional, layer, overrideSnapshot));
            SetText(_resourceLabel, resources != null
                ? "Resources: loaded=" + resources.LoadedCount + " refs=" + resources.TotalRefCount + " failed=" + resources.FailedCount
                : "Resources: unavailable");
            SetText(_bridgeLabel, bridge != null
                ? "Bridge: initialized=" + bridge.IsInitialized + " actors=" + bridge.ActorCount + " events=" + bridge.RecentEntries.Count + " " + FormatBridgeTail(bridge)
                : "Bridge: unavailable");
            SetText(_errorLabel, string.IsNullOrEmpty(_initializationError) ? string.Empty : "Error: " + _initializationError);
            RefreshEvents();
        }

        private MxAnimationDiagnosticSnapshot Snapshot(string actorId)
        {
            if (!IsInitialized)
                return null;

            for (int i = 0; i < _actors.Count; i++)
            {
                if (string.Equals(_actors[i].ActorId, actorId, StringComparison.Ordinal))
                    return _actors[i].Backend?.CreateSnapshot();
            }

            return null;
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

        private static string FormatBlend1D(MxAnimationLayerDiagnostic layer)
        {
            if (layer == null || layer.Blend1DWeights.Count == 0)
                return "weights=none";

            return "dominant=" + ShortClip(layer.CurrentClipKey.Id) + " weights=" + FormatWeights(layer.Blend1DWeights);
        }

        private static string FormatBlend2D(MxAnimationLayerDiagnostic layer)
        {
            if (layer == null || layer.Blend2DWeights.Count == 0)
                return "weights=none";

            return "dominant=" + ShortClip(layer.CurrentClipKey.Id) + " weights=" + FormatWeights(layer.Blend2DWeights);
        }

        private static string FormatLayer(MxAnimationLayerDiagnostic layer)
        {
            if (layer == null)
                return "upper layer unavailable";

            return "weight=" + layer.LayerWeight.ToString("0.00")
                + " target=" + layer.TargetLayerWeight.ToString("0.00")
                + " mask=" + layer.MaskStatus
                + " clip=" + ShortClip(layer.CurrentClipKey.Id);
        }

        private static string FormatWeights(IReadOnlyList<MxAnimationBlend1DWeight> weights)
        {
            string text = string.Empty;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i].Weight <= 0.001f)
                    continue;
                if (text.Length > 0)
                    text += " ";
                text += ShortClip(weights[i].ClipKey.Id) + "=" + weights[i].Weight.ToString("0.00");
            }

            return string.IsNullOrEmpty(text) ? "zero" : text;
        }

        private static string FormatWeights(IReadOnlyList<MxAnimationBlend2DWeight> weights)
        {
            string text = string.Empty;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i].Weight <= 0.001f)
                    continue;
                if (text.Length > 0)
                    text += " ";
                text += ShortClip(weights[i].ClipKey.Id) + "=" + weights[i].Weight.ToString("0.00");
            }

            return string.IsNullOrEmpty(text) ? "zero" : text;
        }

        private static string FormatCache(params MxAnimationDiagnosticSnapshot[] snapshots)
        {
            int hits = 0;
            int misses = 0;
            int residents = 0;
            int cached = 0;
            int active = 0;
            for (int i = 0; i < snapshots.Length; i++)
            {
                if (snapshots[i] == null)
                    continue;

                hits += snapshots[i].Cache.CacheHitCount;
                misses += snapshots[i].Cache.CacheMissCount;
                residents += snapshots[i].Cache.ResidentClipCount;
                cached += snapshots[i].Cache.CachedPlayableCount;
                active += snapshots[i].Cache.ActivePlayableCount;
            }

            return "hits=" + hits + " misses=" + misses + " resident=" + residents + " cached=" + cached + " active=" + active;
        }

        private static string FormatBridgeTail(CombatMxAnimationBridgeDiagnosticSnapshot bridge)
        {
            if (bridge == null || bridge.RecentEntries.Count == 0)
                return string.Empty;

            CombatMxAnimationBridgeDiagnosticEntry entry = bridge.RecentEntries[bridge.RecentEntries.Count - 1];
            return "last=" + entry.EventKind + ":" + entry.BindingId;
        }

        private string ExtractBakeLine(string prefix)
        {
            if (_bakeReport == null || string.IsNullOrEmpty(_bakeReport.text))
                return string.Empty;

            string[] lines = _bakeReport.text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(prefix + ":", StringComparison.Ordinal))
                    return prefix + "=" + ShortHash(lines[i].Substring(prefix.Length + 1).Trim());
            }

            return string.Empty;
        }

        private static string ShortClip(string clipId)
        {
            if (string.IsNullOrWhiteSpace(clipId))
                return "none";

            int index = clipId.LastIndexOf('.');
            return index >= 0 && index + 1 < clipId.Length ? clipId.Substring(index + 1) : clipId;
        }

        private static string ShortHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return "none";

            string value = hash.StartsWith("sha256:", StringComparison.Ordinal) ? hash.Substring(7) : hash;
            return value.Length <= 10 ? value : value.Substring(0, 10);
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

        private static ResourceKey Key(string id, string typeId)
        {
            return new ResourceKey(id, typeId, string.Empty, TempImportedResourceCatalog.PackageId);
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

        private sealed class ShowcaseActor
        {
            public ShowcaseActor(
                string actorId,
                string displayName,
                GameObject instance,
                Animator animator,
                UnityPlayablesAnimationBackend backend)
            {
                ActorId = actorId;
                DisplayName = displayName;
                Instance = instance;
                Animator = animator;
                Backend = backend;
            }

            public string ActorId { get; }
            public string DisplayName { get; }
            public GameObject Instance { get; private set; }
            public Animator Animator { get; }
            public UnityPlayablesAnimationBackend Backend { get; private set; }

            public void Release()
            {
                Backend?.Release();
                Backend = null;
                if (Instance != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(Instance);
                    else
                        UnityEngine.Object.DestroyImmediate(Instance);
                }
                Instance = null;
            }
        }

        private sealed class ShowcasePresentationEventSink : ICombatMxAnimationPresentationEventSink
        {
            private readonly MxAnimationShowcaseDemoBootstrap _owner;

            public ShowcasePresentationEventSink(MxAnimationShowcaseDemoBootstrap owner)
            {
                _owner = owner;
            }

            public void Dispatch(CombatMxAnimationPresentationEventDispatch dispatch)
            {
                _owner?.AddEvent("Presentation event: " + dispatch.PresentationEvent.EventId + " kind=" + dispatch.PresentationEvent.EventKind + " frame=" + dispatch.LocalFrame);
            }
        }
    }
}

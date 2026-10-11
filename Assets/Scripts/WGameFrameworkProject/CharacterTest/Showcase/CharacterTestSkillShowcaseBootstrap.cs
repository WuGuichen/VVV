using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Input;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Vfx.Unity;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestSkillShowcaseBootstrap : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/CharacterTestSkillShowcase.unity";

        private const int PreviewSlotNumber = CharacterTestAbilityHotbarRuntime.LastSlotNumber;
        private const int RuntimeFrameRate = 60;
        private const int WarmupFrames = 6;
        private const int DefaultTargetCount = 5;
        private const int PreviewLoopFrames = 420;
        private const float DefaultTargetDistanceMeters = 7f;
        private const float DefaultTargetSpacingMeters = 2.4f;
        private const float DefaultOrbitYawDegrees = 0f;
        private const float DefaultOrbitPitchDegrees = 18f;
        private const float DefaultOrbitDistanceMeters = 14.5f;
        private const float OrbitYawSensitivity = 0.22f;
        private const float OrbitPitchSensitivity = 0.18f;
        private const string DefaultAbilityStableId = CharacterTestAbilityIds.BeamLanceAbilityStableId;

        private static readonly int[] PriorityAbilityIds =
        {
            CharacterTestAbilityIds.PhysicalShieldAbilityId,
            CharacterTestAbilityIds.BeamLanceAbilityId,
            CharacterTestAbilityIds.ChannelBeamAbilityId,
            CharacterTestAbilityIds.LaserLanceAbilityId,
            CharacterTestAbilityIds.ChainLightningAbilityId,
            CharacterTestAbilityIds.PoisonBottleAbilityId,
            CharacterTestAbilityIds.StunBoltAbilityId,
            CharacterTestAbilityIds.MeteorBarrageAbilityId,
            CharacterTestAbilityIds.SwordWaveAbilityId,
            CharacterTestAbilityIds.DashStrikeAbilityId,
            CharacterTestAbilityIds.HookChainAbilityId,
            CharacterTestAbilityIds.ArcMineAbilityId,
            CharacterTestAbilityIds.RuneTurretAbilityId
        };

        private static readonly Color[] BackgroundPresetColors =
        {
            new Color(0.055f, 0.062f, 0.064f, 1f),
            new Color(0.015f, 0.018f, 0.020f, 1f),
            new Color(0.070f, 0.085f, 0.110f, 1f),
            new Color(0.080f, 0.066f, 0.046f, 1f),
            new Color(0.025f, 0.070f, 0.075f, 1f)
        };

        private static readonly string[] BackgroundPresetNames =
        {
            "Charcoal",
            "Black",
            "Slate",
            "Warm",
            "Teal"
        };

        [SerializeField] private UIDocument _document = null;
        [SerializeField] private UnityEngine.Camera _stageCamera = null;
        [SerializeField] private VisualTreeAsset _visualTree = null;
        [SerializeField] private StyleSheet _styleSheet = null;
        [SerializeField] private Transform _stageRoot = null;
        [SerializeField] private Transform _floorRoot = null;
        [SerializeField] private CharacterTestSceneProfile _profile = null;

        private readonly List<AbilityEntry> _entries = new List<AbilityEntry>(32);
        private readonly List<AbilityEntry> _visibleEntries = new List<AbilityEntry>(32);
        private readonly List<Button> _entryButtons = new List<Button>(32);
        private readonly List<Button> _backgroundSwatchButtons = new List<Button>(8);
        private readonly List<CharacterTestCharacterSpawnSpec> _spawnSpecs =
            new List<CharacterTestCharacterSpawnSpec>(12);
        private readonly List<GameplayStatusEntry> _statusScratch = new List<GameplayStatusEntry>(8);
        private readonly System.Text.StringBuilder _telemetryBuilder = new System.Text.StringBuilder(512);
        private readonly System.Text.StringBuilder _statusBuilder = new System.Text.StringBuilder(64);
        private readonly FakeInputProvider _previewInputProvider = new FakeInputProvider();
        private readonly PreviewHotbarInputSource _previewHotbarInputSource = new PreviewHotbarInputSource();

        private VisualElement _root;
        private TextField _searchField;
        private ScrollView _abilityList;
        private Label _countLabel;
        private Label _titleLabel;
        private Label _statusLabel;
        private Label _frameLabel;
        private Label _cooldownLabel;
        private Label _lockLabel;
        private Label _telemetryLabel;
        private Label _detailsLabel;
        private Button _playButton;
        private Button _replayButton;
        private Button _stopButton;
        private Button _stepButton;
        private Button _resetButton;
        private Toggle _loopToggle;
        private Toggle _lockTargetToggle;
        private Toggle _friendlyTargetToggle;
        private SliderInt _targetCountSlider;
        private Slider _targetDistanceSlider;
        private Slider _targetSpacingSlider;
        private Image _stageImage;
        private VisualElement _backgroundSwatchRow;
        private VisualElement _stageInputElement;
        private RenderTexture _stageRenderTexture;
        private GameSlice _slice;
        private AbilityEntry _selectedEntry;
        private string _searchText = string.Empty;
        [NonSerialized] private bool _initialized;
        [NonSerialized] private bool _autoTick;
        [NonSerialized] private bool _isOrbitDragging;
        private int _selectedIndex = -1;
        private int _targetCount = DefaultTargetCount;
        private int _orbitPointerId = -1;
        private int _framesSinceReplay;
        private int _backgroundPresetIndex;
        private float _targetDistanceMeters = DefaultTargetDistanceMeters;
        private float _targetSpacingMeters = DefaultTargetSpacingMeters;
        private float _orbitYawDegrees = DefaultOrbitYawDegrees;
        private float _orbitPitchDegrees = DefaultOrbitPitchDegrees;
        private float _orbitDistanceMeters = DefaultOrbitDistanceMeters;
        private float _tickAccumulator;
        private Vector2 _lastOrbitPointerPosition;
        private string _lastStatus = string.Empty;
        private int _lastCastCommandSourceId;
        private int _lastCastCommandSlotNumber;
        private int _lastCastCommandAbilityId;

        public bool IsInitialized => _initialized;
        public int AbilityCount => _entries.Count;
        public int VisibleAbilityCount => _visibleEntries.Count;
        public int SelectedAbilityId => _selectedEntry != null ? _selectedEntry.Ability.AbilityId : 0;
        public int ActiveRuntimeVfxCount => _slice?.VfxPresenter != null ? _slice.VfxPresenter.ActiveCount : 0;
        public string LastStatus => _lastStatus;
        internal int LastCastCommandSourceIdForValidation => _lastCastCommandSourceId;
        internal int LastCastCommandSlotNumberForValidation => _lastCastCommandSlotNumber;
        internal int LastCastCommandAbilityIdForValidation => _lastCastCommandAbilityId;
        internal int PreviewHotbarInputReadCountForValidation => _previewHotbarInputSource.ReadCount;
        internal CharacterActionId CasterActiveActionIdForValidation =>
            TryFindCaster(out CharacterTestCharacterEntity caster)
                ? caster.Action.ActiveActionId
                : CharacterActionId.None;
        internal int CasterActionLocalFrameForValidation =>
            TryFindCaster(out CharacterTestCharacterEntity caster) ? caster.Action.LocalFrame : -1;
        internal int ActiveProjectileCountForValidation => _slice?.CombatTraceSystem?.ActiveProjectileCount ?? 0;
        internal int ActiveProjectilePresenterCountForValidation => _slice?.ProjectilePresenter?.ActiveCount ?? 0;
        internal int ActivePoisonBottlePoolCountForValidation =>
            _slice?.CombatTraceSystem?.ActivePoisonBottlePoolCount ?? 0;
        internal int LastPoisonTickTargetCountForValidation =>
            _slice?.CombatTraceSystem?.LastPoisonTickTargetCount ?? 0;
        internal bool AnyHostileHasPoisonedStatusForValidation =>
            CountHostileStatusesForValidation(CharacterTestPoisonIds.PoisonedStatusId) > 0;
        internal bool LastReplayUsedCharacterTestHotbarInputForValidation =>
            _lastCastCommandSourceId == CharacterTestRuntimeCommandSources.PlayerInput
            && _lastCastCommandSlotNumber == PreviewSlotNumber
            && _selectedEntry != null
            && _lastCastCommandAbilityId == _selectedEntry.Ability.AbilityId;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (!_initialized)
                Initialize();

            TickRuntime(Time.deltaTime);
        }

        private void OnDestroy()
        {
            DisposeRuntime();
        }

        public void ConfigureSceneReferences(
            UIDocument document,
            UnityEngine.Camera stageCamera,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Transform stageRoot,
            Transform floorRoot,
            CharacterTestSceneProfile profile)
        {
            _document = document;
            _stageCamera = stageCamera;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
            _stageRoot = stageRoot;
            _floorRoot = floorRoot;
            _profile = profile;
        }

        public void InitializeForValidation()
        {
            Initialize();
        }

        public void DisposeForValidation()
        {
            DisposeRuntime();
        }

        public void RunFrameForValidation(float deltaSeconds)
        {
            if (!_initialized)
                Initialize();

            if (deltaSeconds < 0f)
                deltaSeconds = 0f;

            _tickAccumulator += deltaSeconds;
            TickOneFrame();
        }

        public bool SelectAbilityForValidation(int abilityId)
        {
            if (!_initialized)
                Initialize();

            return SelectAbility(abilityId, false);
        }

        public bool ReplayForValidation()
        {
            if (!_initialized)
                Initialize();

            return ReplaySelected();
        }

        public bool ContainsAbilityForValidation(int abilityId)
        {
            if (!_initialized)
                Initialize();

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Ability.AbilityId == abilityId)
                    return true;
            }

            return false;
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            EnsureSceneReferences();
            BindUi();
            RebuildRuntime();
            BuildEntries();
            ApplyFilters();
            if (!SelectAbility(DefaultAbilityStableId, false) && _entries.Count > 0)
                SelectAbility(_entries[0].Ability.AbilityId, false);

            ResetOrbitCamera();
            _initialized = true;
            SetStatus("Ready. " + _entries.Count.ToString(CultureInfo.InvariantCulture) + " abilities loaded.");
            UpdateTelemetry();
        }

        private void EnsureSceneReferences()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();
            if (_stageCamera == null)
                _stageCamera = UnityEngine.Camera.main;
            if (_stageRoot == null)
                _stageRoot = CreateChildTransform("CharacterTestSkillShowcaseStage", Vector3.zero);
            if (_floorRoot == null)
                _floorRoot = _stageRoot;
        }

        private Transform CreateChildTransform(string objectName, Vector3 localPosition)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        private void BindUi()
        {
            if (_document == null)
                return;

            _root = _document.rootVisualElement;
            if (_root == null)
                return;

            if (_root.childCount == 0 && _visualTree != null)
                _visualTree.CloneTree(_root);
            if (_styleSheet != null && !_root.styleSheets.Contains(_styleSheet))
                _root.styleSheets.Add(_styleSheet);

            _searchField = _root.Q<TextField>("search-field");
            _abilityList = _root.Q<ScrollView>("ability-list");
            _countLabel = _root.Q<Label>("count-value");
            _titleLabel = _root.Q<Label>("ability-title");
            _statusLabel = _root.Q<Label>("status-value");
            _frameLabel = _root.Q<Label>("frame-value");
            _cooldownLabel = _root.Q<Label>("cooldown-value");
            _lockLabel = _root.Q<Label>("lock-value");
            _telemetryLabel = _root.Q<Label>("telemetry-value");
            _detailsLabel = _root.Q<Label>("details-value");
            _playButton = _root.Q<Button>("play-button");
            _replayButton = _root.Q<Button>("replay-button");
            _stopButton = _root.Q<Button>("stop-button");
            _stepButton = _root.Q<Button>("step-button");
            _resetButton = _root.Q<Button>("reset-button");
            _loopToggle = _root.Q<Toggle>("loop-toggle");
            _lockTargetToggle = _root.Q<Toggle>("lock-target-toggle");
            _friendlyTargetToggle = _root.Q<Toggle>("friendly-target-toggle");
            _targetCountSlider = _root.Q<SliderInt>("target-count-slider");
            _targetDistanceSlider = _root.Q<Slider>("target-distance-slider");
            _targetSpacingSlider = _root.Q<Slider>("target-spacing-slider");
            _stageImage = _root.Q<Image>("stage-view");
            _backgroundSwatchRow = _root.Q<VisualElement>("background-swatch-row");

            ConfigureStageRenderTexture();
            BindStageViewCallbacks();
            BuildBackgroundSwatches();
            SelectBackgroundPreset(_backgroundPresetIndex);

            if (_searchField != null)
            {
                _searchField.RegisterValueChangedCallback(evt =>
                {
                    _searchText = evt.newValue ?? string.Empty;
                    ApplyFilters();
                });
            }

            if (_playButton != null)
                _playButton.clicked += () => ReplaySelected();
            if (_replayButton != null)
                _replayButton.clicked += () => ReplaySelected();
            if (_stopButton != null)
                _stopButton.clicked += StopPlayback;
            if (_stepButton != null)
                _stepButton.clicked += StepFrame;
            if (_resetButton != null)
                _resetButton.clicked += ResetActors;

            if (_lockTargetToggle != null)
                _lockTargetToggle.value = true;
            if (_friendlyTargetToggle != null)
            {
                _friendlyTargetToggle.value = true;
                _friendlyTargetToggle.RegisterValueChangedCallback(_ => SetStatus("Target settings will apply on Reset or Replay."));
            }

            if (_targetCountSlider != null)
            {
                _targetCountSlider.lowValue = 1;
                _targetCountSlider.highValue = 8;
                _targetCountSlider.value = _targetCount;
                _targetCountSlider.RegisterValueChangedCallback(evt =>
                {
                    _targetCount = Mathf.Clamp(evt.newValue, 1, 8);
                    SetStatus("Target settings will apply on Reset or Replay.");
                    UpdateDetails();
                });
            }

            if (_targetDistanceSlider != null)
            {
                _targetDistanceSlider.lowValue = 3f;
                _targetDistanceSlider.highValue = 18f;
                _targetDistanceSlider.value = _targetDistanceMeters;
                _targetDistanceSlider.RegisterValueChangedCallback(evt =>
                {
                    _targetDistanceMeters = Mathf.Clamp(evt.newValue, 3f, 18f);
                    SetStatus("Target settings will apply on Reset or Replay.");
                    UpdateDetails();
                });
            }

            if (_targetSpacingSlider != null)
            {
                _targetSpacingSlider.lowValue = 1f;
                _targetSpacingSlider.highValue = 5f;
                _targetSpacingSlider.value = _targetSpacingMeters;
                _targetSpacingSlider.RegisterValueChangedCallback(evt =>
                {
                    _targetSpacingMeters = Mathf.Clamp(evt.newValue, 1f, 5f);
                    SetStatus("Target settings will apply on Reset or Replay.");
                    UpdateDetails();
                });
            }
        }

        private void ConfigureStageRenderTexture()
        {
            if (_stageCamera == null || _stageImage == null)
                return;

            ReleaseStageRenderTexture();
            _stageRenderTexture = new RenderTexture(1536, 864, 24, RenderTextureFormat.ARGB32)
            {
                name = "CharacterTestSkillShowcaseStageRenderTexture",
                antiAliasing = 4
            };
            _stageRenderTexture.Create();
            _stageCamera.targetTexture = _stageRenderTexture;
            _stageImage.image = _stageRenderTexture;
        }

        private void BuildBackgroundSwatches()
        {
            if (_backgroundSwatchRow == null)
                return;

            _backgroundSwatchRow.Clear();
            _backgroundSwatchButtons.Clear();
            for (int i = 0; i < BackgroundPresetColors.Length; i++)
            {
                int presetIndex = i;
                var button = new Button(() => SelectBackgroundPreset(presetIndex))
                {
                    name = "background-swatch-" + presetIndex.ToString(CultureInfo.InvariantCulture),
                    tooltip = BackgroundPresetNames[presetIndex]
                };
                button.AddToClassList("background-swatch");
                button.style.backgroundColor = new StyleColor(BackgroundPresetColors[presetIndex]);
                _backgroundSwatchButtons.Add(button);
                _backgroundSwatchRow.Add(button);
            }

            UpdateBackgroundSwatchState();
        }

        private void SelectBackgroundPreset(int presetIndex)
        {
            if (BackgroundPresetColors.Length == 0)
                return;

            _backgroundPresetIndex = Mathf.Clamp(presetIndex, 0, BackgroundPresetColors.Length - 1);
            Color color = BackgroundPresetColors[_backgroundPresetIndex];
            if (_stageCamera != null)
                _stageCamera.backgroundColor = color;
            UpdateBackgroundSwatchState();
        }

        private void UpdateBackgroundSwatchState()
        {
            for (int i = 0; i < _backgroundSwatchButtons.Count; i++)
            {
                Button button = _backgroundSwatchButtons[i];
                if (button == null)
                    continue;

                button.EnableInClassList("selected", i == _backgroundPresetIndex);
            }
        }

        private void BindStageViewCallbacks()
        {
            _stageInputElement = _stageImage;
            if (_stageInputElement == null)
                return;

            _stageInputElement.RegisterCallback<PointerDownEvent>(OnStagePointerDown);
            _stageInputElement.RegisterCallback<PointerMoveEvent>(OnStagePointerMove);
            _stageInputElement.RegisterCallback<PointerUpEvent>(OnStagePointerUp);
            _stageInputElement.RegisterCallback<PointerCancelEvent>(OnStagePointerCancel);
        }

        private void UnbindStageViewCallbacks()
        {
            if (_stageInputElement == null)
                return;

            _stageInputElement.UnregisterCallback<PointerDownEvent>(OnStagePointerDown);
            _stageInputElement.UnregisterCallback<PointerMoveEvent>(OnStagePointerMove);
            _stageInputElement.UnregisterCallback<PointerUpEvent>(OnStagePointerUp);
            _stageInputElement.UnregisterCallback<PointerCancelEvent>(OnStagePointerCancel);
            _stageInputElement = null;
        }

        private void OnStagePointerDown(PointerDownEvent evt)
        {
            if (evt.button == 1 || evt.clickCount >= 2)
            {
                ResetOrbitCamera();
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            _isOrbitDragging = true;
            _orbitPointerId = evt.pointerId;
            _lastOrbitPointerPosition = new Vector2(evt.position.x, evt.position.y);
            _stageInputElement?.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnStagePointerMove(PointerMoveEvent evt)
        {
            if (!_isOrbitDragging || evt.pointerId != _orbitPointerId)
                return;

            var position = new Vector2(evt.position.x, evt.position.y);
            Vector2 delta = position - _lastOrbitPointerPosition;
            _lastOrbitPointerPosition = position;
            _orbitYawDegrees += delta.x * OrbitYawSensitivity;
            _orbitPitchDegrees = Mathf.Clamp(_orbitPitchDegrees - delta.y * OrbitPitchSensitivity, -8f, 74f);
            ApplyOrbitCamera();
            evt.StopPropagation();
        }

        private void OnStagePointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _orbitPointerId)
                return;

            EndOrbitDrag(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnStagePointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId != _orbitPointerId)
                return;

            EndOrbitDrag(evt.pointerId);
        }

        private void EndOrbitDrag(int pointerId)
        {
            _isOrbitDragging = false;
            _orbitPointerId = -1;
            if (_stageInputElement != null && _stageInputElement.HasPointerCapture(pointerId))
                _stageInputElement.ReleasePointer(pointerId);
        }

        private void ResetOrbitCamera()
        {
            _orbitYawDegrees = DefaultOrbitYawDegrees;
            _orbitPitchDegrees = DefaultOrbitPitchDegrees;
            _orbitDistanceMeters = DefaultOrbitDistanceMeters;
            ApplyOrbitCamera();
        }

        private void ApplyOrbitCamera()
        {
            if (_stageCamera == null)
                return;

            Vector3 pivot = ResolveOrbitPivot();
            Quaternion rotation = Quaternion.Euler(_orbitPitchDegrees, _orbitYawDegrees, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -_orbitDistanceMeters);
            _stageCamera.transform.position = pivot + offset;
            _stageCamera.transform.LookAt(pivot, Vector3.up);
        }

        private Vector3 ResolveOrbitPivot()
        {
            float centerZ = _targetDistanceMeters + (_targetCount - 1) * _targetSpacingMeters * 0.35f;
            return _stageRoot != null
                ? _stageRoot.position + new Vector3(0f, 1.2f, centerZ * 0.5f)
                : new Vector3(0f, 1.2f, centerZ * 0.5f);
        }

        private void RebuildRuntime()
        {
            DisposeSlice();

            CharacterTestSceneProfile profile = _profile != null ? _profile : CharacterTestSceneProfile.LoadDefault();
            GlobalResourceRuntimeServices resources = CreateResourceRuntimeServices(profile);
            CharacterTestRuntimeAiPlannerProfileLoadResult runtimeAiPlannerProfileLoad =
                CharacterTestRuntimeAiPlannerProfileJson.LoadFromStreamingAssetsOrDefault(
                    profile.RuntimeAiPlannerProfilePath);
            _previewInputProvider.SetSnapshot(InputSnapshot.Empty);
            _previewInputProvider.SetContext(InputContext.Gameplay);
            _previewHotbarInputSource.Reset();
            ResetLastCastCommand();

            _slice = new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                Resources = resources,
                BaseResourcePreloadGroupId = profile.BaseResourcePreloadGroupId,
                InitialCameraMode = CharacterTestCameraMode.ThirdPersonControlled,
                CreateCharacterViews = true,
                InputProvider = _previewInputProvider,
                PhysicsRuntime = CreateShowcasePhysicsRuntime(),
                EnableLocalInputCommands = true,
                EnableCharacterSimulation = true,
                RuntimeAiPlannerProfile = runtimeAiPlannerProfileLoad.Profile,
                RuntimeAiPlannerProfileReport = runtimeAiPlannerProfileLoad.Report,
                StartupCharacterSpawnSpecs = BuildSpawnSpecs()
            });
            _slice.Services.Modules.LocalInputCommands?.SetInputProvider(_previewInputProvider);
            _slice.Services.Modules.LocalInputCommands?.SetAbilityHotbarInputSource(_previewHotbarInputSource);
            _slice.Services.Modules.LocalInputCommands?.SetSuppressed(false);
            _slice.SetHeadPadDisplayMode(CharacterTestHeadPadDisplayMode.Hidden);
            _slice.SetTargetLockMaxDistance(64f);

            for (int i = 0; i < WarmupFrames; i++)
                TickSliceOneFrame();

            _framesSinceReplay = 0;
            _autoTick = false;
            _tickAccumulator = 0f;
            ApplyOrbitCamera();
        }

        private GlobalResourceRuntimeServices CreateResourceRuntimeServices(CharacterTestSceneProfile profile)
        {
            var options = new GlobalResourceRuntimeOptions
            {
                Mode = CharacterTestRuntimeSessionBuilder.ResolveResourceMode(
                    profile.ResourceMode,
                    Application.isEditor),
                AllowMissingPreloadGroupInEditor = true
            };

            IVfxUnityPrefabBindingSource publishedBindings =
                VfxUnityPrefabBindingSourceRegistry.Current ?? profile.PublishedVfxBindings;
            ResourceCatalog applicationCatalog = null;
            MemoryResourceProvider applicationProvider = null;
            if (profile.FireballVfxPrefab != null)
            {
                applicationProvider = CharacterTestVfxDefinitionSource.CreateMemoryProvider(
                        profile.FireballVfxPrefab,
                        profile.FireballBurstFlightVfxPrefab,
                        profile.FireballBurstImpactVfxPrefab,
                        profile.FlameLanceFlightVfxPrefab,
                        profile.FlameLanceImpactVfxPrefab,
                        profile.BeamLanceBeamVfxPrefab,
                        profile.BeamLanceImpactVfxPrefab,
                        profile.BeamLanceChargeVfxPrefab,
                        profile.ChannelBeamChargeVfxPrefab,
                        profile.ChannelBeamBeamVfxPrefab,
                        profile.ChannelBeamImpactVfxPrefab,
                        profile.LaserLanceChargeVfxPrefab,
                        profile.LaserLancePathVfxPrefab,
                        profile.LaserLanceImpactVfxPrefab,
                        profile.ChainLightningChargeVfxPrefab,
                        profile.ChainLightningArcVfxPrefab,
                        profile.ChainLightningImpactVfxPrefab,
                        profile.PoisonBottleFlightVfxPrefab,
                        profile.PoisonBottleShatterVfxPrefab,
                        profile.PoisonBottlePoolVfxPrefab,
                        profile.PoisonedTickVfxPrefab,
                        profile.GuardAuraVfxPrefab,
                        profile.PhysicalShieldVfxPrefab,
                        profile.FireballExplosionImpactVfxPrefab,
                        profile.TornadoFlightVfxPrefab,
                        profile.TornadoImpactVfxPrefab,
                        profile.TornadoChargeVfxPrefab,
                        profile.TornadoTickVfxPrefab,
                        areaConfinementZonePrefab: profile.AreaConfinementZoneVfxPrefab,
                        arcMineFlightPrefab: profile.ArcMineFlightVfxPrefab,
                        arcMineDeployPrefab: profile.ArcMineDeployVfxPrefab,
                        arcMineFusePrefab: profile.ArcMineFuseVfxPrefab,
                        arcMineTriggerPrefab: profile.ArcMineTriggerVfxPrefab,
                        publishedBindings: publishedBindings);
                applicationCatalog = CharacterTestVfxDefinitionSource.CreateResourceCatalog();
            }

            GlobalResourceRuntimeServices services = GlobalResourceRuntimeServices.Create(options);
            if (applicationProvider != null)
                services.ResourceManager.RegisterProvider(applicationProvider);
            if (applicationCatalog != null)
                services.ResourceManager.AddCatalog(applicationCatalog);
            return services;
        }

        private static CharacterTestCharacterPhysicsRuntime CreateShowcasePhysicsRuntime()
        {
            var world = new CombatPhysicsWorld();
            var groundBodyId = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart),
                groundBodyId,
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                groundBodyId,
                new CombatColliderId(1),
                layer: 0,
                localMin: new FixVector3(Fix64.FromInt(-30), Fix64.FromRatio(-5, 100), Fix64.FromInt(-12)),
                localMax: new FixVector3(Fix64.FromInt(30), Fix64.FromRatio(5, 100), Fix64.FromInt(36)),
                surfaceFriction: Fix64.Zero));

            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(0),
                new FixVector3(Fix64.FromRatio(45, 100), Fix64.FromRatio(9, 10), Fix64.FromRatio(45, 100)),
                Fix64.FromRatio(1, 100),
                Fix64.FromRatio(35, 100),
                staticColliderCount: 1,
                sourceDescription: "skill_showcase.ground");
        }

        private IReadOnlyList<CharacterTestCharacterSpawnSpec> BuildSpawnSpecs()
        {
            _spawnSpecs.Clear();
            _spawnSpecs.Add(new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0f,
                controlOwner: CharacterTestCharacterControlOwner.LocalPlayer));

            int hostileCount = Mathf.Clamp(_targetCount, 1, 8);
            for (int i = 0; i < hostileCount; i++)
            {
                _spawnSpecs.Add(new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 1f,
                    z: _targetDistanceMeters + i * _targetSpacingMeters,
                    controlOwner: CharacterTestCharacterControlOwner.None));
            }

            if (_friendlyTargetToggle == null || _friendlyTargetToggle.value)
            {
                _spawnSpecs.Add(new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                    teamId: 1,
                    x: 3.2f,
                    y: 1f,
                    z: _targetDistanceMeters + _targetSpacingMeters,
                    controlOwner: CharacterTestCharacterControlOwner.None));
            }

            return _spawnSpecs;
        }

        private void BuildEntries()
        {
            _entries.Clear();
            if (_slice?.AbilityRuntime == null)
                return;

            IReadOnlyList<AbilityDefinitionConfig> definitions = _slice.AbilityRuntime.Definitions.CreateSnapshot();
            for (int i = 0; i < definitions.Count; i++)
            {
                AbilityDefinitionConfig definition = definitions[i];
                _entries.Add(new AbilityEntry(definition, ResolvePriority(definition.AbilityId), ResolveGroup(definition)));
            }

            _entries.Sort(AbilityEntryComparer.Instance);
            for (int i = 0; i < _entries.Count; i++)
                _entries[i].Index = i;
        }

        private void ApplyFilters()
        {
            _visibleEntries.Clear();
            _entryButtons.Clear();
            if (_abilityList != null)
                _abilityList.Clear();

            string search = _searchText ?? string.Empty;
            for (int i = 0; i < _entries.Count; i++)
            {
                AbilityEntry entry = _entries[i];
                if (!MatchesSearch(entry, search))
                    continue;

                _visibleEntries.Add(entry);
                AddAbilityButton(entry);
            }

            if (_countLabel != null)
            {
                _countLabel.text =
                    _visibleEntries.Count.ToString(CultureInfo.InvariantCulture)
                    + " shown / "
                    + _entries.Count.ToString(CultureInfo.InvariantCulture)
                    + " total";
            }
        }

        private void AddAbilityButton(AbilityEntry entry)
        {
            if (_abilityList == null)
                return;

            var button = new Button(() => SelectAbility(entry.Ability.AbilityId, false));
            button.AddToClassList("ability-row");
            button.EnableInClassList("selected", _selectedEntry == entry);

            var title = new Label(TrimDisplayName(entry.Ability.DisplayName));
            title.AddToClassList("ability-row-title");
            button.Add(title);

            var sub = new Label(entry.Ability.AbilityId.ToString(CultureInfo.InvariantCulture) + "  " + entry.Group);
            sub.AddToClassList("ability-row-subtitle");
            button.Add(sub);

            _entryButtons.Add(button);
            _abilityList.Add(button);
        }

        private bool SelectAbility(string stableId, bool playImmediately)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (!string.Equals(_entries[i].Ability.AbilityStableId, stableId, StringComparison.Ordinal))
                    continue;

                return SelectAbility(_entries[i].Ability.AbilityId, playImmediately);
            }

            return false;
        }

        private bool SelectAbility(int abilityId, bool playImmediately)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                AbilityEntry entry = _entries[i];
                if (entry.Ability.AbilityId != abilityId)
                    continue;

                _selectedEntry = entry;
                _selectedIndex = entry.Index;
                UpdateAbilityRowSelection();
                UpdateDetails();
                if (playImmediately)
                    ReplaySelected();
                return true;
            }

            return false;
        }

        private void UpdateAbilityRowSelection()
        {
            for (int i = 0; i < _entryButtons.Count; i++)
            {
                Button button = _entryButtons[i];
                if (button == null || i >= _visibleEntries.Count)
                    continue;

                button.EnableInClassList("selected", _visibleEntries[i] == _selectedEntry);
            }
        }

        private bool ReplaySelected()
        {
            if (_selectedEntry == null)
            {
                SetStatus("No ability selected.");
                return false;
            }

            RebuildRuntime();
            if (!CastSelected())
                return false;

            _autoTick = true;
            _framesSinceReplay = 0;
            SetStatus(
                "Playing "
                + TrimDisplayName(_selectedEntry.Ability.DisplayName)
                + " through CharacterTest hotbar input.");
            UpdateDetails();
            UpdateTelemetry();
            return true;
        }

        private bool CastSelected()
        {
            if (_slice == null || _selectedEntry == null)
                return false;

            if (!TryFindCaster(out CharacterTestCharacterEntity caster))
            {
                SetStatus("Caster was not found.");
                return false;
            }

            if (!_slice.AbilityHotbar.AssignSlot(PreviewSlotNumber, _selectedEntry.Ability.AbilityId, out string message))
            {
                SetStatus("Preview slot assignment failed: " + message);
                return false;
            }

            ConfigureTargetLock(caster);
            TickSliceOneFrame();

            _previewHotbarInputSource.QueuePress(PreviewSlotNumber);
            TickOneFrame();
            if (!TryCaptureLastAppliedPreviewCast())
            {
                SetStatus("Preview hotbar input was not applied by CharacterTest runtime.");
                return false;
            }

            return true;
        }

        private void ConfigureTargetLock(CharacterTestCharacterEntity caster)
        {
            bool wantsLock = _lockTargetToggle == null || _lockTargetToggle.value;
            int targetId = 0;
            if (wantsLock && TryFindFirstHostile(caster, out CharacterTestCharacterEntity target))
                targetId = target.Id.Value;

            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetTargetLock(
                _slice.CurrentFrame,
                CharacterTestRuntimeCommandSources.SkillPanel,
                targetId > 0,
                targetId,
                new RuntimeTraceIdentity(90876));
            _slice.EnqueueRuntimeCommand(command);
        }

        private bool TryCaptureLastAppliedPreviewCast()
        {
            ResetLastCastCommand();
            IReadOnlyList<CharacterTestRuntimeEvent> events = _slice != null
                ? _slice.RecentRuntimeEvents
                : null;
            if (events == null)
                return false;

            for (int i = events.Count - 1; i >= 0; i--)
            {
                CharacterTestRuntimeEvent evt = events[i];
                if (evt.Type != CharacterTestRuntimeEventType.ControlIntentApplied
                    || evt.CommandId != CharacterTestRuntimeCommandIds.CastAbilitySlot
                    || evt.SourceId != CharacterTestRuntimeCommandSources.PlayerInput
                    || evt.Payload0 != PreviewSlotNumber
                    || _selectedEntry == null
                    || evt.Payload1 != _selectedEntry.Ability.AbilityId)
                {
                    continue;
                }

                _lastCastCommandSourceId = evt.SourceId;
                _lastCastCommandSlotNumber = evt.Payload0;
                _lastCastCommandAbilityId = evt.Payload1;
                return true;
            }

            return false;
        }

        private void ResetLastCastCommand()
        {
            _lastCastCommandSourceId = 0;
            _lastCastCommandSlotNumber = 0;
            _lastCastCommandAbilityId = 0;
        }

        private void StopPlayback()
        {
            _autoTick = false;
            SetStatus("Stopped.");
            UpdateTelemetry();
        }

        private void StepFrame()
        {
            _autoTick = false;
            TickOneFrame();
        }

        private void ResetActors()
        {
            RebuildRuntime();
            SetStatus("Actors reset.");
            UpdateTelemetry();
        }

        private void TickRuntime(float deltaSeconds)
        {
            if (!_autoTick || _slice == null)
                return;

            if (deltaSeconds < 0f)
                deltaSeconds = 0f;

            _tickAccumulator += deltaSeconds;
            float fixedDelta = 1f / RuntimeFrameRate;
            int steps = 0;
            while (_tickAccumulator >= fixedDelta && steps < 4)
            {
                _tickAccumulator -= fixedDelta;
                TickOneFrame();
                steps++;
            }
        }

        private void TickOneFrame()
        {
            TickSliceOneFrame();
            _framesSinceReplay++;
            UpdateTelemetry();

            if (_framesSinceReplay < PreviewLoopFrames)
                return;

            if (_loopToggle != null && _loopToggle.value)
                ReplaySelected();
            else
                _autoTick = false;
        }

        private void TickSliceOneFrame()
        {
            if (_slice == null)
                return;

            double fixedDelta = 1d / RuntimeFrameRate;
            _slice.Tick(fixedDelta);
            _slice.TickPresentation(fixedDelta);
        }

        private void UpdateDetails()
        {
            if (_selectedEntry == null)
                return;

            AbilityDefinitionConfig ability = _selectedEntry.Ability;
            if (_titleLabel != null)
                _titleLabel.text = TrimDisplayName(ability.DisplayName);
            if (_detailsLabel != null)
            {
                _detailsLabel.text =
                    ability.AbilityId.ToString(CultureInfo.InvariantCulture)
                    + " / "
                    + ability.AbilityStableId
                    + "\nAction: "
                    + ability.ActionBinding.ActionId
                    + " / "
                    + ability.ActionBinding.ActionIntentId
                    + "\nCooldown: "
                    + ability.Cooldown.Frames.ToString(CultureInfo.InvariantCulture)
                    + "f"
                    + "\nTargets: "
                    + _targetCount.ToString(CultureInfo.InvariantCulture)
                    + " hostile, distance "
                    + _targetDistanceMeters.ToString("0.0", CultureInfo.InvariantCulture)
                    + "m, spacing "
                    + _targetSpacingMeters.ToString("0.0", CultureInfo.InvariantCulture)
                    + "m";
            }
        }

        private void UpdateTelemetry()
        {
            if (_slice == null)
                return;

            if (_frameLabel != null)
                _frameLabel.text = _slice.CurrentFrame.Value.ToString(CultureInfo.InvariantCulture);

            if (_cooldownLabel != null)
            {
                long remaining = 0L;
                if (_selectedEntry != null && TryFindCaster(out CharacterTestCharacterEntity caster))
                {
                    remaining = _slice.AbilityRuntime.GetRemainingCooldownFrames(
                        caster.Id.Value,
                        _selectedEntry.Ability,
                        _slice.CurrentFrame);
                }

                _cooldownLabel.text = remaining.ToString(CultureInfo.InvariantCulture) + "f";
            }

            if (_lockLabel != null)
            {
                _lockLabel.text = _slice.TargetLockEnabled
                    ? _slice.LockedTargetEntityId.ToString(CultureInfo.InvariantCulture)
                    : "none";
            }

            if (_telemetryLabel != null)
                _telemetryLabel.text = BuildTelemetryText();
        }

        private string BuildTelemetryText()
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _slice.CharacterEntities;
            if (entities == null || entities.Count == 0)
                return "No entities.";

            System.Text.StringBuilder builder = _telemetryBuilder;
            builder.Clear();
            builder.Append("VFX active: ")
                .Append(ActiveRuntimeVfxCount.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                builder.Append('#').Append(entity.Id.Value.ToString(CultureInfo.InvariantCulture))
                    .Append(" team ").Append(entity.Team.TeamId.ToString(CultureInfo.InvariantCulture))
                    .Append(entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer ? " caster" : string.Empty)
                    .Append(" HP ").Append(entity.CombatState.Health.ToString(CultureInfo.InvariantCulture))
                    .Append('/').Append(entity.CombatState.MaxHealth.ToString(CultureInfo.InvariantCulture));
                if (entity.CombatState.LastDamage > 0)
                {
                    builder.Append(" last-")
                        .Append(entity.CombatState.LastDamage.ToString(CultureInfo.InvariantCulture))
                        .Append("@")
                        .Append(entity.CombatState.LastHitFrame.ToString(CultureInfo.InvariantCulture));
                }

                string statuses = BuildStatusText(entity);
                if (!string.IsNullOrEmpty(statuses))
                    builder.Append(" ").Append(statuses);
                if (i + 1 < entities.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private string BuildStatusText(CharacterTestCharacterEntity entity)
        {
            if (entity == null
                || _slice?.CharacterRuntime?.World?.GameplayComponentWorld == null
                || !_slice.CharacterRuntime.World.GameplayComponentWorld.TryGetStore(
                    out GameplayComponentStore<GameplayStatusComponent> statusStore)
                || !statusStore.TryGet(entity.GameplayEntityId, out GameplayStatusComponent statuses))
            {
                return string.Empty;
            }

            _statusScratch.Clear();
            statuses.CopyEntriesTo(_statusScratch);
            if (_statusScratch.Count == 0)
                return string.Empty;

            System.Text.StringBuilder builder = _statusBuilder;
            builder.Clear();
            builder.Append('[');
            for (int i = 0; i < _statusScratch.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(ToStatusShortName(_statusScratch[i].StatusId.Value));
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string ToStatusShortName(int statusId)
        {
            if (statusId == CharacterTestStasisIds.StasisStatusId)
                return "STOP";
            if (statusId == CharacterTestPoisonIds.PoisonedStatusId)
                return "POIS";

            return statusId.ToString(CultureInfo.InvariantCulture);
        }

        private int CountHostileStatusesForValidation(int statusId)
        {
            if (!TryFindCaster(out CharacterTestCharacterEntity caster)
                || _slice?.CharacterRuntime?.World?.GameplayComponentWorld == null
                || !_slice.CharacterRuntime.World.GameplayComponentWorld.TryGetStore(
                    out GameplayComponentStore<GameplayStatusComponent> statusStore))
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<CharacterTestCharacterEntity> entities = _slice.CharacterEntities;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == caster
                    || entity.Team.TeamId == caster.Team.TeamId
                    || !entity.CombatState.IsAlive)
                {
                    continue;
                }

                if (statusStore.TryGet(entity.GameplayEntityId, out GameplayStatusComponent statuses)
                    && statuses.Contains(new GameplayStatusId(statusId)))
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryFindCaster(out CharacterTestCharacterEntity caster)
        {
            caster = null;
            IReadOnlyList<CharacterTestCharacterEntity> entities = _slice?.CharacterEntities;
            if (entities == null)
                return false;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                {
                    caster = entity;
                    return true;
                }
            }

            if (entities.Count > 0)
            {
                caster = entities[0];
                return true;
            }

            return false;
        }

        private bool TryFindFirstHostile(
            CharacterTestCharacterEntity caster,
            out CharacterTestCharacterEntity target)
        {
            target = null;
            IReadOnlyList<CharacterTestCharacterEntity> entities = _slice?.CharacterEntities;
            if (entities == null || caster == null)
                return false;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == caster
                    || entity.Team.TeamId == caster.Team.TeamId
                    || !entity.CombatState.IsAlive)
                {
                    continue;
                }

                target = entity;
                return true;
            }

            return false;
        }

        private void SetStatus(string value)
        {
            _lastStatus = value ?? string.Empty;
            if (_statusLabel != null)
                _statusLabel.text = _lastStatus;
        }

        private void DisposeRuntime()
        {
            UnbindStageViewCallbacks();
            DisposeSlice();
            ReleaseStageRenderTexture();
            _entries.Clear();
            _visibleEntries.Clear();
            _entryButtons.Clear();
            _backgroundSwatchButtons.Clear();
            _initialized = false;
            _isOrbitDragging = false;
            _orbitPointerId = -1;
        }

        private void DisposeSlice()
        {
            _previewHotbarInputSource.Reset();
            if (_slice == null)
                return;

            _slice.Dispose();
            _slice = null;
        }

        private void ReleaseStageRenderTexture()
        {
            if (_stageCamera != null)
                _stageCamera.targetTexture = null;
            if (_stageRenderTexture != null)
            {
                UnityEngine.Camera[] cameras =
                    Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsInactive.Include);
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i] != null && cameras[i].targetTexture == _stageRenderTexture)
                        cameras[i].targetTexture = null;
                }
            }

            if (_stageImage != null)
                _stageImage.image = null;
            if (_stageRenderTexture != null)
            {
                _stageRenderTexture.Release();
                DestroyUnityObject(_stageRenderTexture);
                _stageRenderTexture = null;
            }
        }

        private static bool MatchesSearch(AbilityEntry entry, string search)
        {
            if (entry == null)
                return false;
            if (string.IsNullOrWhiteSpace(search))
                return true;

            return Contains(entry.Ability.DisplayName, search)
                || Contains(entry.Ability.AbilityStableId, search)
                || Contains(entry.Ability.AbilityId.ToString(CultureInfo.InvariantCulture), search)
                || Contains(entry.Group, search);
        }

        private static bool Contains(string source, string search)
        {
            return !string.IsNullOrEmpty(source)
                && source.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ResolvePriority(int abilityId)
        {
            for (int i = 0; i < PriorityAbilityIds.Length; i++)
            {
                if (PriorityAbilityIds[i] == abilityId)
                    return i;
            }

            return PriorityAbilityIds.Length + abilityId;
        }

        private static string ResolveGroup(AbilityDefinitionConfig ability)
        {
            if (ability == null)
                return "Other";

            string stable = ability.AbilityStableId ?? string.Empty;
            if (stable.IndexOf("beam", StringComparison.OrdinalIgnoreCase) >= 0
                || stable.IndexOf("laser", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Beam/Laser";
            }

            if (stable.IndexOf("chain", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Lightning";
            if (stable.IndexOf("poison", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Poison";
            if (stable.IndexOf("fireball", StringComparison.OrdinalIgnoreCase) >= 0
                || stable.IndexOf("flame", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Fire";
            }

            if (stable.IndexOf("tornado", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Wind";
            if (stable.IndexOf("area", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Area";
            return "Other";
        }

        private static string TrimDisplayName(string displayName)
        {
            const string prefix = "CharacterTest ";
            if (string.IsNullOrWhiteSpace(displayName))
                return "Ability";

            return displayName.StartsWith(prefix, StringComparison.Ordinal)
                ? displayName.Substring(prefix.Length)
                : displayName;
        }

        private static void DestroyUnityObject(Object value)
        {
            if (value == null)
                return;

            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }

        private sealed class AbilityEntry
        {
            public AbilityEntry(AbilityDefinitionConfig ability, int priority, string group)
            {
                Ability = ability;
                Priority = priority;
                Group = group ?? string.Empty;
            }

            public int Index;
            public AbilityDefinitionConfig Ability { get; }
            public int Priority { get; }
            public string Group { get; }
        }

        private sealed class AbilityEntryComparer : IComparer<AbilityEntry>
        {
            public static readonly AbilityEntryComparer Instance = new AbilityEntryComparer();

            public int Compare(AbilityEntry left, AbilityEntry right)
            {
                if (ReferenceEquals(left, right))
                    return 0;
                if (left == null)
                    return -1;
                if (right == null)
                    return 1;

                int priority = left.Priority.CompareTo(right.Priority);
                if (priority != 0)
                    return priority;

                return left.Ability.AbilityId.CompareTo(right.Ability.AbilityId);
            }
        }

        private sealed class PreviewHotbarInputSource : ICharacterTestAbilityHotbarInputSource
        {
            private int _pendingSlotNumber;

            public int ReadCount { get; private set; }

            public void QueuePress(int slotNumber)
            {
                _pendingSlotNumber = CharacterTestAbilityHotbarRuntime.IsValidSlot(slotNumber)
                    ? slotNumber
                    : 0;
            }

            public int ReadPressedSlot()
            {
                int slotNumber = _pendingSlotNumber;
                _pendingSlotNumber = 0;
                if (CharacterTestAbilityHotbarRuntime.IsValidSlot(slotNumber))
                    ReadCount++;
                return slotNumber;
            }

            public void Reset()
            {
                _pendingSlotNumber = 0;
                ReadCount = 0;
            }
        }
    }
}

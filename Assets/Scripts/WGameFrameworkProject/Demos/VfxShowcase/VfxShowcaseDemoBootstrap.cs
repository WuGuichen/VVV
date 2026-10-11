using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Demo.VfxShowcase
{
    public sealed class VfxShowcaseDemoBootstrap : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/MxVfxShowcase.unity";
        public const string GeneratedDefinitionsPath = "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json";
        public const string AuthoringCatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";

        private const float DefaultLengthMeters = 8f;
        private const float DefaultTargetDistanceMeters = 8f;
        private const float DefaultDirectPrefabLifetimeSeconds = 2.5f;
        private const int RuntimeFrameRate = 30;
        private const string DefaultPreviewResourceId = "vfx.magic.fireball";
        private const float DefaultOrbitYawDegrees = 0f;
        private const float DefaultOrbitPitchDegrees = 16f;
        private const float DefaultOrbitDistanceMeters = 13.2f;
        private const float OrbitYawSensitivity = 0.22f;
        private const float OrbitPitchSensitivity = 0.18f;
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
        [SerializeField] private Transform _previewRoot = null;
        [SerializeField] private Transform _muzzleAnchor = null;
        [SerializeField] private Transform _targetAnchor = null;
        [SerializeField] private Transform _endpointAnchor = null;
        [SerializeField] private Transform _groundAnchor = null;
        [SerializeField] private Transform _casterPreviewRoot = null;
        [SerializeField] private Transform _targetPreviewRoot = null;
        [SerializeField] private VfxShowcaseSerializedAsset[] _serializedAssets = Array.Empty<VfxShowcaseSerializedAsset>();

        private readonly List<ShowcaseEntry> _entries = new List<ShowcaseEntry>(64);
        private readonly List<ShowcaseEntry> _visibleEntries = new List<ShowcaseEntry>(64);
        private readonly List<Button> _entryButtons = new List<Button>(64);
        private readonly List<Button> _backgroundSwatchButtons = new List<Button>(8);
        private readonly List<DirectPrefabInstance> _directInstances = new List<DirectPrefabInstance>(8);
        private readonly List<VfxInstanceSnapshot> _activeRuntimeInstances = new List<VfxInstanceSnapshot>(8);
        private readonly Dictionary<ResourceKey, ResourceCatalogEntry> _catalogEntriesByKey = new Dictionary<ResourceKey, ResourceCatalogEntry>();
        private readonly Dictionary<string, VfxShowcaseSemanticEffect> _semanticEffectsByResource = new Dictionary<string, VfxShowcaseSemanticEffect>(StringComparer.Ordinal);
        private readonly Dictionary<string, VfxShowcaseSemanticRecipe> _semanticRecipesById = new Dictionary<string, VfxShowcaseSemanticRecipe>(StringComparer.Ordinal);
        private readonly Dictionary<string, VfxShowcaseSemanticPrimitive> _semanticPrimitivesById = new Dictionary<string, VfxShowcaseSemanticPrimitive>(StringComparer.Ordinal);
        private readonly HashSet<ResourceKey> _definitionResourceKeys = new HashSet<ResourceKey>();

        private ResourceCatalog _catalog;
        private MemoryResourceProvider _memoryProvider;
        private ResourceManager _resourceManager;
        private RuntimeCommandBuffer _commandBuffer;
        private VfxRuntimeModule _vfxRuntime;
        private VfxRuntimeEventFanout _vfxFanout;
        private UnityVfxPresenter _vfxPresenter;
        private VisualElement _root;
        private TextField _searchField;
        private VisualElement _groupRow;
        private ScrollView _entryList;
        private Button _playButton;
        private Button _replayButton;
        private Button _clearButton;
        private Toggle _loopToggle;
        private Toggle _autoAdvanceToggle;
        private Slider _lengthSlider;
        private Slider _targetDistanceSlider;
        private Label _titleLabel;
        private Label _statusLabel;
        private Label _countLabel;
        private Label _definitionLabel;
        private Label _resourceLabel;
        private Label _lifetimeLabel;
        private Label _assetPathLabel;
        private Label _kindLabel;
        private Label _labelsLabel;
        private Label _parametersLabel;
        private Label _semanticLabel;
        private Label _recipeLabel;
        private Label _budgetLabel;
        private VisualElement _semanticParameterControls;
        private Image _stageImage;
        private VisualElement _backgroundSwatchRow;
        private VisualElement _stageInputElement;
        private RenderTexture _stageRenderTexture;
        private ShowcaseEntry _selectedEntry;
        private int _selectedIndex = -1;
        private string _selectedGroup = "All";
        private string _searchText = string.Empty;
        [NonSerialized] private bool _initialized;
        [NonSerialized] private bool _isOrbitDragging;
        private int _orbitPointerId = -1;
        private Vector2 _lastOrbitPointerPosition;
        private float _orbitYawDegrees = DefaultOrbitYawDegrees;
        private float _orbitPitchDegrees = DefaultOrbitPitchDegrees;
        private float _orbitDistanceMeters = DefaultOrbitDistanceMeters;
        private int _backgroundPresetIndex;
        private long _frameIndex;
        private double _elapsedSeconds;
        private float _autoAdvanceDelay;
        private float _replayDelay;
        private string _lastStatus = string.Empty;
        private readonly int[] _semanticSlotValues = new int[4];
        private readonly bool[] _semanticSlotOverrides = new bool[4];

        public bool IsInitialized => _initialized;
        public int EntryCount => _entries.Count;
        public int VisibleEntryCount => _visibleEntries.Count;
        public int RuntimeDefinitionCount { get; private set; }
        public int CatalogPrefabCount { get; private set; }
        public int SemanticEffectCount => _semanticEffectsByResource.Count;
        public int ActiveRuntimeVfxCount => _vfxPresenter != null ? _vfxPresenter.ActiveCount : 0;
        public int ActiveDirectPrefabCount => _directInstances.Count;
        public string SelectedResourceId => _selectedEntry != null ? _selectedEntry.ResourceKey.Id : string.Empty;
        public string SelectedSemanticText => _selectedEntry != null ? _selectedEntry.SemanticText : string.Empty;
        public string SelectedRecipeText => _selectedEntry != null ? _selectedEntry.RecipeText : string.Empty;
        public string SelectedBudgetText => _selectedEntry != null ? _selectedEntry.BudgetText : string.Empty;
        public int SelectedRuntimeParameterCount => _selectedEntry != null && _selectedEntry.RuntimeParameters != null ? _selectedEntry.RuntimeParameters.Length : 0;
        public string LastStatus => _lastStatus;
        public ResourceManager ResourceManager => _resourceManager;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (!EnsureRuntimeReady())
                return;

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
            Transform previewRoot,
            Transform muzzleAnchor,
            Transform targetAnchor,
            Transform endpointAnchor,
            Transform groundAnchor,
            VfxShowcaseSerializedAsset[] serializedAssets)
        {
            ConfigureSceneReferences(
                document,
                stageCamera,
                visualTree,
                styleSheet,
                previewRoot,
                muzzleAnchor,
                targetAnchor,
                endpointAnchor,
                groundAnchor,
                null,
                null,
                serializedAssets);
        }

        public void ConfigureSceneReferences(
            UIDocument document,
            UnityEngine.Camera stageCamera,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Transform previewRoot,
            Transform muzzleAnchor,
            Transform targetAnchor,
            Transform endpointAnchor,
            Transform groundAnchor,
            Transform casterPreviewRoot,
            Transform targetPreviewRoot,
            VfxShowcaseSerializedAsset[] serializedAssets)
        {
            _document = document;
            _stageCamera = stageCamera;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
            _previewRoot = previewRoot;
            _muzzleAnchor = muzzleAnchor;
            _targetAnchor = targetAnchor;
            _endpointAnchor = endpointAnchor;
            _groundAnchor = groundAnchor;
            _casterPreviewRoot = casterPreviewRoot;
            _targetPreviewRoot = targetPreviewRoot;
            _serializedAssets = serializedAssets ?? Array.Empty<VfxShowcaseSerializedAsset>();
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

            TickRuntime(deltaSeconds);
        }

        public bool SelectEntryForValidation(string resourceOrStableId)
        {
            if (!_initialized)
                Initialize();

            return SelectEntry(resourceOrStableId);
        }

        public bool ReplayForValidation()
        {
            if (!_initialized)
                Initialize();

            return ReplaySelected();
        }

        public void ClearForValidation()
        {
            ClearPlayback();
        }

        public bool SetLengthForValidation(float lengthMeters)
        {
            if (_lengthSlider == null)
                return false;

            _lengthSlider.value = Mathf.Clamp(lengthMeters, _lengthSlider.lowValue, _lengthSlider.highValue);
            UpdateStageMarkers();
            UpdateDetails();
            return true;
        }

        public bool SetTargetDistanceForValidation(float distanceMeters)
        {
            if (_targetDistanceSlider == null)
                return false;

            _targetDistanceSlider.value = Mathf.Clamp(distanceMeters, _targetDistanceSlider.lowValue, _targetDistanceSlider.highValue);
            UpdateStageMarkers();
            UpdateDetails();
            return true;
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            EnsureSceneReferences();
            BuildResourceRuntime();
            BuildEntries();
            BindUi();
            ApplyFilters();
            if (!SelectEntry(DefaultPreviewResourceId) && _entries.Count > 0)
                SelectEntry(0, playImmediately: false);
            ReplaySelected();

            _initialized = true;
            SetStatus("Ready. " + _entries.Count + " VFX entries loaded.");
        }

        private bool EnsureRuntimeReady()
        {
            if (!_initialized)
                Initialize();

            if (!_initialized)
                return false;

            if (_resourceManager != null && _vfxRuntime != null && _vfxFanout != null && _vfxPresenter != null)
                return true;

            DisposeRuntime();
            Initialize();
            return _initialized && _resourceManager != null && _vfxRuntime != null && _vfxFanout != null && _vfxPresenter != null;
        }

        private void DisposeRuntime()
        {
            UnbindStageViewCallbacks();
            ClearDirectPrefabs();
            if (_vfxPresenter != null)
            {
                _vfxPresenter.Dispose();
                _vfxPresenter = null;
            }

            if (_resourceManager != null)
            {
                _resourceManager.ReleaseAllLoaded("vfx-showcase-dispose");
                _resourceManager = null;
            }

            _vfxFanout = null;
            _vfxRuntime = null;
            _commandBuffer = null;
            _memoryProvider = null;
            _catalog = null;
            ReleaseStageRenderTexture();
            _catalogEntriesByKey.Clear();
            _definitionResourceKeys.Clear();
            _semanticEffectsByResource.Clear();
            _semanticRecipesById.Clear();
            _semanticPrimitivesById.Clear();
            _entries.Clear();
            _visibleEntries.Clear();
            _entryButtons.Clear();
            _backgroundSwatchButtons.Clear();
            _initialized = false;
            _isOrbitDragging = false;
            _orbitPointerId = -1;
        }

        private void EnsureSceneReferences()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();
            if (_stageCamera == null)
                _stageCamera = UnityEngine.Camera.main;
            if (_previewRoot == null)
                _previewRoot = CreateChildTransform("VfxShowcasePreviewRoot", Vector3.zero);
            if (_muzzleAnchor == null)
                _muzzleAnchor = CreateChildTransform("MuzzleAnchor", new Vector3(0f, 1.15f, -3f));
            if (_targetAnchor == null)
                _targetAnchor = CreateChildTransform("TargetAnchor", new Vector3(0f, 1f, DefaultTargetDistanceMeters - 3f));
            if (_endpointAnchor == null)
                _endpointAnchor = CreateChildTransform("EndpointAnchor", new Vector3(0f, 1.15f, DefaultLengthMeters - 3f));
            if (_groundAnchor == null)
                _groundAnchor = CreateChildTransform("GroundAnchor", new Vector3(0f, 0.04f, 4f));
        }

        private Transform CreateChildTransform(string objectName, Vector3 localPosition)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        private void BuildResourceRuntime()
        {
            _catalog = TempImportedResourceCatalog.CreateCatalog();
            LoadAuthoringSemantics();
            _memoryProvider = new MemoryResourceProvider();
            RegisterSerializedAssets(_catalog, _memoryProvider);

            _resourceManager = new ResourceManager();
            _resourceManager.RegisterProvider(_memoryProvider);
            _resourceManager.AddCatalog(_catalog);
            _resourceManager.ValidateCatalogs();

            VfxDefinitionRegistry definitions = LoadVfxDefinitions();
            _commandBuffer = new RuntimeCommandBuffer();
            _vfxRuntime = new VfxRuntimeModule(definitions, _commandBuffer);
            _vfxFanout = new VfxRuntimeEventFanout(_vfxRuntime);
            _vfxPresenter = new UnityVfxPresenter(_vfxRuntime, _resourceManager, usePooling: true);
            _vfxFanout.AddSink(_vfxPresenter);
        }

        private void RegisterSerializedAssets(ResourceCatalog catalog, MemoryResourceProvider provider)
        {
            if (catalog == null || provider == null)
                return;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null || !IsVfxGameObjectEntry(entry))
                    continue;

                GameObject prefab = FindSerializedPrefab(entry);
                if (prefab != null)
                    provider.Register(entry.Address, prefab);
            }
        }

        private GameObject FindSerializedPrefab(ResourceCatalogEntry entry)
        {
            if (_serializedAssets == null)
                return null;

            ResourceKey key = entry.CreateKey(_catalog.PackageId);
            for (int i = 0; i < _serializedAssets.Length; i++)
            {
                VfxShowcaseSerializedAsset asset = _serializedAssets[i];
                if (asset == null || asset.Prefab == null)
                    continue;

                if (asset.CreateKey() == key)
                    return asset.Prefab;
            }

            return null;
        }

        private static VfxDefinitionRegistry LoadVfxDefinitions()
        {
            if (!File.Exists(GeneratedDefinitionsPath))
                return new VfxDefinitionRegistry();

            return VfxConfigJsonImporter.ImportDefinitionFile(GeneratedDefinitionsPath);
        }

        private void LoadAuthoringSemantics()
        {
            _semanticEffectsByResource.Clear();
            _semanticRecipesById.Clear();
            _semanticPrimitivesById.Clear();
            if (!File.Exists(AuthoringCatalogPath))
                return;

            try
            {
                VfxShowcaseSemanticCatalog catalog = JsonUtility.FromJson<VfxShowcaseSemanticCatalog>(File.ReadAllText(AuthoringCatalogPath));
                if (catalog == null)
                    return;

                if (catalog.recipes != null)
                {
                    for (int i = 0; i < catalog.recipes.Length; i++)
                    {
                        VfxShowcaseSemanticRecipe recipe = catalog.recipes[i];
                        if (recipe != null && !string.IsNullOrWhiteSpace(recipe.recipeId))
                            _semanticRecipesById[recipe.recipeId] = recipe;
                    }
                }

                if (catalog.primitives != null)
                {
                    for (int i = 0; i < catalog.primitives.Length; i++)
                    {
                        VfxShowcaseSemanticPrimitive primitive = catalog.primitives[i];
                        if (primitive != null && !string.IsNullOrWhiteSpace(primitive.primitiveId))
                            _semanticPrimitivesById[primitive.primitiveId] = primitive;
                    }
                }

                if (catalog.effects == null)
                    return;

                for (int i = 0; i < catalog.effects.Length; i++)
                {
                    VfxShowcaseSemanticEffect effect = catalog.effects[i];
                    if (effect == null || effect.resourceKey == null || string.IsNullOrWhiteSpace(effect.resourceKey.id))
                        continue;

                    _semanticEffectsByResource[CreateSemanticResourceKey(effect.resourceKey.id, effect.resourceKey.typeId, effect.resourceKey.variant, effect.resourceKey.packageId)] = effect;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                Debug.LogWarning("VFX Showcase could not load authoring semantics: " + exception.Message, this);
            }
        }

        private static string CreateSemanticResourceKey(string id, string typeId, string variant, string packageId)
        {
            return (packageId ?? string.Empty) + "|" + (id ?? string.Empty) + "|" + (typeId ?? string.Empty) + "|" + (variant ?? string.Empty);
        }

        private void BuildEntries()
        {
            _entries.Clear();
            _catalogEntriesByKey.Clear();
            _definitionResourceKeys.Clear();
            RuntimeDefinitionCount = 0;
            CatalogPrefabCount = 0;

            for (int i = 0; i < _catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = _catalog.Entries[i];
                if (entry == null || !IsVfxGameObjectEntry(entry))
                    continue;

                ResourceKey key = entry.CreateKey(_catalog.PackageId);
                _catalogEntriesByKey[key] = entry;
            }

            IReadOnlyList<VfxDefinition> definitions = _vfxRuntime.Definitions.CreateSnapshot();
            for (int i = 0; i < definitions.Count; i++)
            {
                VfxDefinition definition = definitions[i];
                if (!_catalogEntriesByKey.TryGetValue(definition.PrefabResource, out ResourceCatalogEntry entry))
                    continue;

                _definitionResourceKeys.Add(definition.PrefabResource);
                _semanticEffectsByResource.TryGetValue(CreateSemanticResourceKey(definition.PrefabResource.Id, definition.PrefabResource.TypeId, definition.PrefabResource.Variant, definition.PrefabResource.PackageId), out VfxShowcaseSemanticEffect semantics);
                _entries.Add(ShowcaseEntry.FromDefinition(_entries.Count, definition, entry, semantics, ResolveRecipe(semantics), _semanticPrimitivesById));
                RuntimeDefinitionCount++;
            }

            foreach (KeyValuePair<ResourceKey, ResourceCatalogEntry> pair in _catalogEntriesByKey)
            {
                if (_definitionResourceKeys.Contains(pair.Key))
                    continue;

                _semanticEffectsByResource.TryGetValue(CreateSemanticResourceKey(pair.Key.Id, pair.Key.TypeId, pair.Key.Variant, pair.Key.PackageId), out VfxShowcaseSemanticEffect semantics);
                _entries.Add(ShowcaseEntry.FromCatalog(_entries.Count, pair.Key, pair.Value, semantics, ResolveRecipe(semantics), _semanticPrimitivesById));
                CatalogPrefabCount++;
            }

            _entries.Sort(ShowcaseEntryComparer.Instance);
            for (int i = 0; i < _entries.Count; i++)
                _entries[i].Index = i;
        }

        private VfxShowcaseSemanticRecipe ResolveRecipe(VfxShowcaseSemanticEffect effect)
        {
            if (effect == null || string.IsNullOrWhiteSpace(effect.recipeId))
                return null;

            _semanticRecipesById.TryGetValue(effect.recipeId, out VfxShowcaseSemanticRecipe recipe);
            return recipe;
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
            _groupRow = _root.Q<VisualElement>("group-row");
            _entryList = _root.Q<ScrollView>("entry-list");
            _playButton = _root.Q<Button>("play-button");
            _replayButton = _root.Q<Button>("replay-button");
            _clearButton = _root.Q<Button>("clear-button");
            _loopToggle = _root.Q<Toggle>("loop-toggle");
            _autoAdvanceToggle = _root.Q<Toggle>("auto-advance-toggle");
            _lengthSlider = _root.Q<Slider>("length-slider");
            _targetDistanceSlider = _root.Q<Slider>("target-distance-slider");
            _titleLabel = _root.Q<Label>("entry-title");
            _statusLabel = _root.Q<Label>("status-value");
            _countLabel = _root.Q<Label>("count-value");
            _definitionLabel = _root.Q<Label>("definition-value");
            _resourceLabel = _root.Q<Label>("resource-value");
            _lifetimeLabel = _root.Q<Label>("lifetime-value");
            _assetPathLabel = _root.Q<Label>("asset-path-value");
            _kindLabel = _root.Q<Label>("kind-value");
            _labelsLabel = _root.Q<Label>("labels-value");
            _parametersLabel = _root.Q<Label>("parameters-value");
            _semanticLabel = _root.Q<Label>("semantic-value");
            _recipeLabel = _root.Q<Label>("recipe-value");
            _budgetLabel = _root.Q<Label>("budget-value");
            _semanticParameterControls = _root.Q<VisualElement>("semantic-parameter-controls");
            _stageImage = _root.Q<Image>("stage-view");
            _backgroundSwatchRow = _root.Q<VisualElement>("background-swatch-row");

            ConfigureStageRenderTexture();
            BindStageViewCallbacks();
            BuildBackgroundSwatches();
            SelectBackgroundPreset(_backgroundPresetIndex);
            if (_lengthSlider != null)
            {
                _lengthSlider.lowValue = 1f;
                _lengthSlider.highValue = 44f;
                _lengthSlider.value = DefaultLengthMeters;
                _lengthSlider.RegisterValueChangedCallback(_ =>
                {
                    UpdateStageMarkers();
                    UpdateDetails();
                });
            }

            if (_targetDistanceSlider != null)
            {
                _targetDistanceSlider.lowValue = 1f;
                _targetDistanceSlider.highValue = 18f;
                _targetDistanceSlider.value = DefaultTargetDistanceMeters;
                _targetDistanceSlider.RegisterValueChangedCallback(_ =>
                {
                    UpdateStageMarkers();
                    UpdateDetails();
                });
            }

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
            if (_clearButton != null)
                _clearButton.clicked += ClearPlayback;

            BuildGroupButtons();
            UpdateStageMarkers();
            ResetOrbitCamera();
        }

        private void ConfigureStageRenderTexture()
        {
            if (_stageCamera == null || _stageImage == null)
                return;

            ReleaseStageRenderTexture();
            _stageRenderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32)
            {
                name = "VfxShowcaseStageRenderTexture",
                antiAliasing = 4,
                useMipMap = false,
                autoGenerateMips = false
            };
            _stageRenderTexture.Create();
            _stageCamera.targetTexture = _stageRenderTexture;
            _stageImage.image = _stageRenderTexture;
        }

        private void BuildBackgroundSwatches()
        {
            _backgroundSwatchButtons.Clear();
            if (_backgroundSwatchRow == null)
                return;

            _backgroundSwatchRow.Clear();
            for (int i = 0; i < BackgroundPresetColors.Length; i++)
            {
                int presetIndex = i;
                var button = new Button(() => SelectBackgroundPreset(presetIndex))
                {
                    text = string.Empty,
                    tooltip = BackgroundPresetNames[presetIndex]
                };
                button.focusable = false;
                button.AddToClassList("background-swatch");
                button.style.backgroundColor = new StyleColor(BackgroundPresetColors[presetIndex]);
                _backgroundSwatchRow.Add(button);
                _backgroundSwatchButtons.Add(button);
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
            if (_stageImage != null)
                _stageImage.style.backgroundColor = new StyleColor(color);

            UpdateBackgroundSwatchState();
        }

        private void UpdateBackgroundSwatchState()
        {
            for (int i = 0; i < _backgroundSwatchButtons.Count; i++)
            {
                Button button = _backgroundSwatchButtons[i];
                if (button == null)
                    continue;

                if (i == _backgroundPresetIndex)
                    button.AddToClassList("background-swatch-selected");
                else
                    button.RemoveFromClassList("background-swatch-selected");
            }
        }

        private void ReleaseStageRenderTexture()
        {
            if (_stageCamera != null && _stageCamera.targetTexture == _stageRenderTexture)
                _stageCamera.targetTexture = null;
            if (_stageImage != null && _stageImage.image == _stageRenderTexture)
                _stageImage.image = null;
            if (_stageRenderTexture != null)
            {
                _stageRenderTexture.Release();
                DestroyUnityObject(_stageRenderTexture);
                _stageRenderTexture = null;
            }
        }

        private void BindStageViewCallbacks()
        {
            UnbindStageViewCallbacks();
            if (_stageImage == null)
                return;

            _stageImage.pickingMode = PickingMode.Position;
            _stageInputElement = _stageImage;
            _stageInputElement.RegisterCallback<PointerDownEvent>(OnStagePointerDown);
            _stageInputElement.RegisterCallback<PointerMoveEvent>(OnStagePointerMove);
            _stageInputElement.RegisterCallback<PointerUpEvent>(OnStagePointerUp);
            _stageInputElement.RegisterCallback<PointerCancelEvent>(OnStagePointerCancel);
            _stageInputElement.RegisterCallback<WheelEvent>(OnStageWheel);
        }

        private void UnbindStageViewCallbacks()
        {
            if (_stageInputElement == null)
                return;

            _stageInputElement.UnregisterCallback<PointerDownEvent>(OnStagePointerDown);
            _stageInputElement.UnregisterCallback<PointerMoveEvent>(OnStagePointerMove);
            _stageInputElement.UnregisterCallback<PointerUpEvent>(OnStagePointerUp);
            _stageInputElement.UnregisterCallback<PointerCancelEvent>(OnStagePointerCancel);
            _stageInputElement.UnregisterCallback<WheelEvent>(OnStageWheel);
            _stageInputElement = null;
        }

        private void OnStagePointerDown(PointerDownEvent evt)
        {
            if (_stageCamera == null)
                return;

            if (evt.button == 2 || evt.clickCount > 1)
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

            Vector2 position = new Vector2(evt.position.x, evt.position.y);
            Vector2 delta = position - _lastOrbitPointerPosition;
            _lastOrbitPointerPosition = position;
            _orbitYawDegrees += delta.x * OrbitYawSensitivity;
            _orbitPitchDegrees = Mathf.Clamp(_orbitPitchDegrees - delta.y * OrbitPitchSensitivity, -10f, 78f);
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
            evt.StopPropagation();
        }

        private void OnStageWheel(WheelEvent evt)
        {
            if (_stageCamera == null || Mathf.Abs(evt.delta.y) < 0.01f)
                return;

            float step = Mathf.Clamp(Mathf.Abs(evt.delta.y) * 0.02f, 0.25f, 2.5f);
            _orbitDistanceMeters = Mathf.Clamp(_orbitDistanceMeters + Mathf.Sign(evt.delta.y) * step, 3.2f, 26f);
            ApplyOrbitCamera();
            evt.StopPropagation();
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
            Vector3 position = pivot - rotation * Vector3.forward * _orbitDistanceMeters;
            _stageCamera.transform.SetPositionAndRotation(position, rotation);
            _stageCamera.transform.LookAt(pivot, Vector3.up);
        }

        private Vector3 ResolveOrbitPivot()
        {
            Vector3 forward = _muzzleAnchor != null ? _muzzleAnchor.forward : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 muzzle = _muzzleAnchor != null ? _muzzleAnchor.position : transform.position;
            Vector3 pivot = muzzle + forward * Mathf.Min(GetPreviewLengthMeters() * 0.65f, 8f);
            float groundY = _groundAnchor != null ? _groundAnchor.position.y : 0f;
            pivot.y = groundY + 0.9f;
            return pivot;
        }

        private void BuildGroupButtons()
        {
            if (_groupRow == null)
                return;

            _groupRow.Clear();
            AddGroupButton("All");
            var groups = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _entries.Count; i++)
                groups.Add(_entries[i].Group);
            foreach (string group in groups)
                AddGroupButton(group);
        }

        private void AddGroupButton(string group)
        {
            var button = new Button(() =>
            {
                _selectedGroup = group;
                ApplyFilters();
            })
            {
                text = group
            };
            button.AddToClassList("group-button");
            _groupRow.Add(button);
        }

        private void ApplyFilters()
        {
            _visibleEntries.Clear();
            for (int i = 0; i < _entries.Count; i++)
            {
                ShowcaseEntry entry = _entries[i];
                if (!MatchesGroup(entry) || !MatchesSearch(entry))
                    continue;

                _visibleEntries.Add(entry);
            }

            RebuildEntryList();
            UpdateCountLabel();
        }

        private bool MatchesGroup(ShowcaseEntry entry)
        {
            return string.Equals(_selectedGroup, "All", StringComparison.Ordinal)
                || string.Equals(entry.Group, _selectedGroup, StringComparison.Ordinal);
        }

        private bool MatchesSearch(ShowcaseEntry entry)
        {
            if (string.IsNullOrWhiteSpace(_searchText))
                return true;

            return entry.ResourceKey.Id.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.StableName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.DisplayName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Group.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.SemanticSearchText.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RebuildEntryList()
        {
            if (_entryList == null)
                return;

            _entryButtons.Clear();
            _entryList.contentContainer.Clear();
            for (int i = 0; i < _visibleEntries.Count; i++)
            {
                ShowcaseEntry entry = _visibleEntries[i];
                int index = entry.Index;
                var button = new Button(() => SelectEntry(index, playImmediately: true));
                button.tooltip = entry.ResourceKey.Id;
                button.AddToClassList("entry-button");
                if (_selectedEntry == entry)
                    button.AddToClassList("entry-button-selected");

                var title = new Label(entry.DisplayName);
                title.AddToClassList("entry-name");
                button.Add(title);

                var sub = new Label(entry.KindText + "  " + entry.Group);
                sub.AddToClassList("entry-subtitle");
                button.Add(sub);
                _entryList.contentContainer.Add(button);
                _entryButtons.Add(button);
            }
        }

        private void UpdateCountLabel()
        {
            if (_countLabel != null)
                _countLabel.text = _visibleEntries.Count + " shown / " + _entries.Count + " total";
        }

        private bool SelectEntry(string resourceOrStableId)
        {
            if (string.IsNullOrWhiteSpace(resourceOrStableId))
                return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                ShowcaseEntry entry = _entries[i];
                if (string.Equals(entry.ResourceKey.Id, resourceOrStableId, StringComparison.Ordinal)
                    || string.Equals(entry.StableName, resourceOrStableId, StringComparison.Ordinal))
                {
                    SelectEntry(i, playImmediately: false);
                    return true;
                }
            }

            return false;
        }

        private void SelectEntry(int index, bool playImmediately)
        {
            if (index < 0 || index >= _entries.Count)
                return;

            _selectedIndex = index;
            _selectedEntry = _entries[index];
            Array.Clear(_semanticSlotOverrides, 0, _semanticSlotOverrides.Length);
            ApplySemanticPreviewDefaults(_selectedEntry);
            UpdateEntryButtonState();
            UpdateDetails();
            if (playImmediately)
                ReplaySelected();
        }

        private void ApplySemanticPreviewDefaults(ShowcaseEntry entry)
        {
            if (entry == null || !entry.HasSemanticPreview)
                return;
            if (_targetDistanceSlider != null && entry.PreviewTargetDistanceMeters > 0f)
                _targetDistanceSlider.SetValueWithoutNotify(Mathf.Clamp(entry.PreviewTargetDistanceMeters, _targetDistanceSlider.lowValue, _targetDistanceSlider.highValue));
            if (_lengthSlider != null && entry.PreviewTargetDistanceMeters > 0f && string.Equals(entry.PreviewAnchorPreset, "Path", StringComparison.Ordinal))
                _lengthSlider.SetValueWithoutNotify(Mathf.Clamp(entry.PreviewTargetDistanceMeters, _lengthSlider.lowValue, _lengthSlider.highValue));
            if (entry.PreviewCameraScaleMeters > 0f)
                _orbitDistanceMeters = Mathf.Clamp(entry.PreviewCameraScaleMeters * 1.35f, 3.2f, 26f);
            UpdateStageMarkers();
            ApplyOrbitCamera();
        }

        private void UpdateEntryButtonState()
        {
            for (int i = 0; i < _entryButtons.Count; i++)
                _entryButtons[i].RemoveFromClassList("entry-button-selected");

            for (int i = 0; i < _entryButtons.Count; i++)
            {
                if (_entryButtons[i].tooltip == _selectedEntry.ResourceKey.Id)
                {
                    _entryButtons[i].AddToClassList("entry-button-selected");
                    break;
                }
            }
        }

        private void UpdateDetails()
        {
            if (_selectedEntry == null)
                return;

            if (_titleLabel != null)
                _titleLabel.text = _selectedEntry.DisplayName;
            if (_definitionLabel != null)
                _definitionLabel.text = _selectedEntry.DefinitionId > 0 ? _selectedEntry.DefinitionId.ToString() + " / " + _selectedEntry.StableName : "-";
            if (_resourceLabel != null)
                _resourceLabel.text = _selectedEntry.ResourceKey.ToString();
            if (_lifetimeLabel != null)
                _lifetimeLabel.text = _selectedEntry.DefaultLifetimeFrames > 0 ? _selectedEntry.DefaultLifetimeFrames + " frames" : "catalog prefab";
            if (_assetPathLabel != null)
                _assetPathLabel.text = _selectedEntry.AssetPath;
            if (_kindLabel != null)
                _kindLabel.text = _selectedEntry.KindText;
            if (_labelsLabel != null)
                _labelsLabel.text = _selectedEntry.LabelText;
            if (_parametersLabel != null)
                _parametersLabel.text = "length=" + GetPreviewLengthMeters().ToString("0.0") + "m, target=" + GetTargetDistanceMeters().ToString("0.0") + "m";
            if (_semanticLabel != null)
                _semanticLabel.text = _selectedEntry.SemanticText;
            if (_recipeLabel != null)
                _recipeLabel.text = _selectedEntry.RecipeText;
            if (_budgetLabel != null)
                _budgetLabel.text = _selectedEntry.BudgetText;
            RebuildSemanticParameterControls();
        }

        private void RebuildSemanticParameterControls()
        {
            if (_semanticParameterControls == null)
                return;
            _semanticParameterControls.Clear();
            VfxShowcaseRuntimeParameter[] parameters = _selectedEntry.RuntimeParameters;
            if (parameters == null || parameters.Length == 0)
            {
                _semanticParameterControls.Add(new Label("No runtime-mapped semantic parameters."));
                return;
            }
            for (int i = 0; i < parameters.Length; i++)
            {
                VfxShowcaseRuntimeParameter parameter = parameters[i];
                int slot = parameter.Slot;
                float scale = Mathf.Abs(parameter.RuntimeScale) > 0.000001f ? parameter.RuntimeScale : 1f;
                var slider = new Slider(parameter.ParameterId + " (slot " + slot + ")", parameter.MinValue, parameter.MaxValue)
                {
                    value = parameter.DefaultValue
                };
                _semanticSlotValues[slot] = Mathf.RoundToInt(parameter.DefaultValue / scale);
                _semanticSlotOverrides[slot] = true;
                slider.RegisterValueChangedCallback(evt =>
                {
                    _semanticSlotValues[slot] = Mathf.RoundToInt(evt.newValue / scale);
                    ReplaySelected();
                });
                _semanticParameterControls.Add(slider);
            }
        }

        private bool ReplaySelected()
        {
            if (_selectedEntry == null)
            {
                SetStatus("Select a VFX entry first.");
                return false;
            }

            ClearPlayback();
            bool spawned = _selectedEntry.Kind == VfxShowcaseEntryKind.RuntimeDefinition
                ? SpawnRuntimeDefinition(_selectedEntry)
                : SpawnCatalogPrefab(_selectedEntry);
            if (spawned)
            {
                _autoAdvanceDelay = ComputePreviewLifetime(_selectedEntry);
                _replayDelay = _autoAdvanceDelay;
                SetStatus("Playing " + _selectedEntry.DisplayName + ".");
            }

            return spawned;
        }

        private bool SpawnRuntimeDefinition(ShowcaseEntry entry)
        {
            VfxAnchor anchor = CreateAnchor(entry);
            int lengthMillimeters = Mathf.RoundToInt(GetPreviewLengthMeters() * 1000f);
            var parameters = CreatePreviewParameters(lengthMillimeters, Mathf.RoundToInt(GetTargetDistanceMeters() * 1000f), entry.Index);
            var request = new VfxSpawnRequest(
                new RuntimeFrame(_frameIndex),
                VfxSourceDomains.Manual,
                _frameIndex,
                0,
                0,
                0,
                0,
                0,
                new VfxDefinitionId(entry.DefinitionId),
                anchor,
                parameters,
                0,
                new RuntimeTraceIdentity(VfxSourceDomains.Manual, entry.DefinitionId, _frameIndex, entry.Index));
            _vfxRuntime.EnqueueSpawnRequest(request);
            TickVfxRuntime(0d);
            return _vfxPresenter.ActiveCount > 0 || _vfxRuntime.PendingSpawnCount > 0;
        }

        private VfxParameterBlock CreatePreviewParameters(int value0, int value1, int value2)
        {
            int slot0 = _semanticSlotOverrides[0] ? _semanticSlotValues[0] : value0;
            int slot1 = _semanticSlotOverrides[1] ? _semanticSlotValues[1] : value1;
            int slot2 = _semanticSlotOverrides[2] ? _semanticSlotValues[2] : value2;
            int slot3 = _semanticSlotOverrides[3] ? _semanticSlotValues[3] : 0;
            return new VfxParameterBlock(slot0, slot1, slot2, slot3);
        }

        private bool SpawnCatalogPrefab(ShowcaseEntry entry)
        {
            ResourceLoadResult<ResourceHandle<GameObject>> load = _resourceManager.Load<GameObject>(entry.ResourceKey);
            if (!load.Success || load.Value == null || load.Value.Value == null)
            {
                SetStatus("Catalog prefab load failed: " + load.Error);
                return false;
            }

            GameObject instance = Instantiate(load.Value.Value, _previewRoot);
            instance.name = "VFX_Showcase_" + entry.ResourceKey.Id;
            ApplyTransform(instance.transform, entry);
            PlayParticles(instance);
            _directInstances.Add(new DirectPrefabInstance(load.Value, instance, ComputePreviewLifetime(entry), IsFlightEntry(entry)));
            return true;
        }

        private VfxAnchor CreateAnchor(ShowcaseEntry entry)
        {
            Vector3 position;
            Vector3 forward;
            ResolvePreviewPose(entry, out position, out forward);
            return VfxAnchor.World(new VfxPose(ToVfxVector(position), ToVfxVector(forward)));
        }

        private void ApplyTransform(Transform target, ShowcaseEntry entry)
        {
            Vector3 position;
            Vector3 forward;
            ResolvePreviewPose(entry, out position, out forward);
            target.position = position;
            target.forward = forward;
        }

        private void ResolvePreviewPose(ShowcaseEntry entry, out Vector3 position, out Vector3 forward)
        {
            forward = _muzzleAnchor != null ? _muzzleAnchor.forward : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            string id = entry.ResourceKey.Id;
            if (id.IndexOf(".impact", StringComparison.Ordinal) >= 0
                || id.IndexOf(".tick", StringComparison.Ordinal) >= 0
                || id.IndexOf("status_aura", StringComparison.Ordinal) >= 0)
            {
                position = _targetAnchor != null ? _targetAnchor.position : transform.position + forward * GetTargetDistanceMeters();
                return;
            }

            if (id.IndexOf(".pool", StringComparison.Ordinal) >= 0
                || id.IndexOf(".zone", StringComparison.Ordinal) >= 0)
            {
                position = _groundAnchor != null ? _groundAnchor.position : transform.position;
                return;
            }

            position = _muzzleAnchor != null ? _muzzleAnchor.position : transform.position;
        }

        private void ClearPlayback()
        {
            ClearDirectPrefabs();
            ResetVfxRuntime();
            _autoAdvanceDelay = 0f;
            _replayDelay = 0f;
            SetStatus("Cleared.");
        }

        private void ClearDirectPrefabs()
        {
            for (int i = _directInstances.Count - 1; i >= 0; i--)
            {
                DirectPrefabInstance instance = _directInstances[i];
                if (instance.GameObject != null)
                    DestroyUnityObject(instance.GameObject);
                if (instance.Handle != null && !instance.Handle.IsReleased && _resourceManager != null)
                    _resourceManager.Release(instance.Handle);
            }

            _directInstances.Clear();
        }

        private void ResetVfxRuntime()
        {
            if (_vfxPresenter != null)
                _vfxPresenter.Dispose();

            _commandBuffer = new RuntimeCommandBuffer();
            VfxDefinitionRegistry definitions = _vfxRuntime != null ? _vfxRuntime.Definitions : LoadVfxDefinitions();
            _vfxRuntime = new VfxRuntimeModule(definitions, _commandBuffer);
            _vfxFanout = new VfxRuntimeEventFanout(_vfxRuntime);
            _vfxPresenter = new UnityVfxPresenter(_vfxRuntime, _resourceManager, usePooling: true);
            _vfxFanout.AddSink(_vfxPresenter);
        }

        private void TickRuntime(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
                deltaSeconds = 0f;

            _elapsedSeconds += deltaSeconds;
            TickVfxRuntime(deltaSeconds);
            TickDirectPrefabs(deltaSeconds);
            TickAutoPlayback(deltaSeconds);
        }

        private void TickVfxRuntime(double deltaSeconds)
        {
            if (_vfxRuntime == null || _vfxFanout == null || _vfxPresenter == null)
                return;

            var context = new RuntimeTickContext(_frameIndex, deltaSeconds, _elapsedSeconds, RuntimeTickStage.PostSimulation);
            _vfxRuntime.Tick(context);
            _vfxFanout.DrainFrame(new RuntimeFrame(_frameIndex));
            _vfxPresenter.SyncActiveAnchors();
            _frameIndex++;
        }

        private void TickDirectPrefabs(float deltaSeconds)
        {
            for (int i = _directInstances.Count - 1; i >= 0; i--)
            {
                DirectPrefabInstance instance = _directInstances[i];
                instance.RemainingSeconds -= deltaSeconds;
                if (instance.GameObject != null && instance.IsFlight)
                {
                    Vector3 start = _muzzleAnchor != null ? _muzzleAnchor.position : transform.position;
                    Vector3 forward = _muzzleAnchor != null ? _muzzleAnchor.forward : Vector3.forward;
                    float normalizedAge = 1f - Mathf.Clamp01(instance.RemainingSeconds / Mathf.Max(0.01f, ComputePreviewLifetime(_selectedEntry)));
                    instance.GameObject.transform.position = start + forward.normalized * (GetPreviewLengthMeters() * normalizedAge);
                }

                if (instance.RemainingSeconds > 0f)
                    continue;

                if (instance.GameObject != null)
                    DestroyUnityObject(instance.GameObject);
                if (instance.Handle != null && !instance.Handle.IsReleased)
                    _resourceManager.Release(instance.Handle);
                _directInstances.RemoveAt(i);
            }
        }

        private void TickAutoPlayback(float deltaSeconds)
        {
            if (_selectedEntry == null)
                return;

            if (_loopToggle != null && _loopToggle.value)
            {
                _replayDelay -= deltaSeconds;
                if (_replayDelay <= 0f)
                    ReplaySelected();
                return;
            }

            if (_autoAdvanceToggle == null || !_autoAdvanceToggle.value)
                return;

            _autoAdvanceDelay -= deltaSeconds;
            if (_autoAdvanceDelay > 0f)
                return;

            SelectNextVisibleEntry();
            ReplaySelected();
        }

        private void SelectNextVisibleEntry()
        {
            if (_visibleEntries.Count == 0)
                return;

            int visibleIndex = 0;
            for (int i = 0; i < _visibleEntries.Count; i++)
            {
                if (_visibleEntries[i] == _selectedEntry)
                {
                    visibleIndex = i + 1;
                    break;
                }
            }

            if (visibleIndex >= _visibleEntries.Count)
                visibleIndex = 0;
            SelectEntry(_visibleEntries[visibleIndex].Index, playImmediately: false);
        }

        private void UpdateStageMarkers()
        {
            Vector3 forward = _muzzleAnchor != null ? _muzzleAnchor.forward : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 muzzle = _muzzleAnchor != null ? _muzzleAnchor.position : transform.position;
            Vector3 casterOrigin = _casterPreviewRoot != null ? _casterPreviewRoot.position : muzzle;
            Vector3 targetPosition = casterOrigin + forward * GetTargetDistanceMeters();
            float groundY = _groundAnchor != null ? _groundAnchor.position.y : 0.04f;
            if (_targetPreviewRoot != null)
            {
                _targetPreviewRoot.position = new Vector3(targetPosition.x, groundY - 0.04f, targetPosition.z);
                _targetPreviewRoot.forward = -forward;
            }
            else if (_targetAnchor != null)
            {
                _targetAnchor.position = targetPosition;
            }

            if (_endpointAnchor != null)
                _endpointAnchor.position = muzzle + forward * GetPreviewLengthMeters();
            if (_groundAnchor != null)
                _groundAnchor.position = new Vector3(targetPosition.x, groundY, targetPosition.z);
        }

        private float ComputePreviewLifetime(ShowcaseEntry entry)
        {
            if (entry == null)
                return DefaultDirectPrefabLifetimeSeconds;
            if (entry.DefaultLifetimeFrames > 0)
                return Mathf.Max(0.4f, entry.DefaultLifetimeFrames / (float)RuntimeFrameRate);

            return DefaultDirectPrefabLifetimeSeconds;
        }

        private float GetPreviewLengthMeters()
        {
            return _lengthSlider != null ? _lengthSlider.value : DefaultLengthMeters;
        }

        private float GetTargetDistanceMeters()
        {
            return _targetDistanceSlider != null ? _targetDistanceSlider.value : DefaultTargetDistanceMeters;
        }

        private void SetStatus(string value)
        {
            _lastStatus = value ?? string.Empty;
            if (_statusLabel != null)
                _statusLabel.text = _lastStatus;
        }

        private static bool IsVfxGameObjectEntry(ResourceCatalogEntry entry)
        {
            return entry != null
                && string.Equals(entry.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal)
                && HasLabel(entry, TempImportedResourceCatalog.DomainVfxLabel);
        }

        private static bool HasLabel(ResourceCatalogEntry entry, string label)
        {
            if (entry == null || string.IsNullOrWhiteSpace(label))
                return false;

            for (int i = 0; i < entry.Labels.Count; i++)
            {
                if (string.Equals(entry.Labels[i], label, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsFlightEntry(ShowcaseEntry entry)
        {
            return entry != null && entry.ResourceKey.Id.IndexOf(".flight", StringComparison.Ordinal) >= 0;
        }

        private static void PlayParticles(GameObject instance)
        {
            if (instance == null)
                return;

            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] == null)
                    continue;

                particles[i].Clear(true);
                particles[i].Play(true);
            }
        }

        private static VfxVector3 ToVfxVector(Vector3 value)
        {
            return VfxVector3.FromRaw(
                Mathf.RoundToInt(value.x * VfxVector3.Scale),
                Mathf.RoundToInt(value.y * VfxVector3.Scale),
                Mathf.RoundToInt(value.z * VfxVector3.Scale));
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

        private sealed class ShowcaseEntry
        {
            public int Index;
            public VfxShowcaseEntryKind Kind;
            public ResourceKey ResourceKey;
            public int DefinitionId;
            public string StableName;
            public int DefaultLifetimeFrames;
            public string DisplayName;
            public string Group;
            public string AssetPath;
            public string LabelText;
            public string KindText;
            public string SemanticText;
            public string SemanticSearchText;
            public string RecipeText;
            public string BudgetText;
            public VfxShowcaseRuntimeParameter[] RuntimeParameters;
            public bool HasSemanticPreview;
            public string PreviewAnchorPreset;
            public float PreviewTargetDistanceMeters;
            public float PreviewCameraScaleMeters;

            public static ShowcaseEntry FromDefinition(int index, VfxDefinition definition, ResourceCatalogEntry entry, VfxShowcaseSemanticEffect semantics, VfxShowcaseSemanticRecipe recipe, Dictionary<string, VfxShowcaseSemanticPrimitive> primitives)
            {
                ShowcaseEntry result = new ShowcaseEntry
                {
                    Index = index,
                    Kind = VfxShowcaseEntryKind.RuntimeDefinition,
                    ResourceKey = definition.PrefabResource,
                    DefinitionId = definition.DefinitionId.Value,
                    StableName = definition.StableName,
                    DefaultLifetimeFrames = definition.DefaultLifetimeFrames,
                    DisplayName = ToDisplayName(definition.PrefabResource.Id),
                    Group = ResolveGroup(definition.PrefabResource.Id),
                    AssetPath = TryGetAssetPath(entry),
                    LabelText = JoinLabels(entry),
                    KindText = "RuntimeDefinition"
                };
                ApplySemantics(result, semantics, recipe, primitives);
                return result;
            }

            public static ShowcaseEntry FromCatalog(int index, ResourceKey key, ResourceCatalogEntry entry, VfxShowcaseSemanticEffect semantics, VfxShowcaseSemanticRecipe recipe, Dictionary<string, VfxShowcaseSemanticPrimitive> primitives)
            {
                ShowcaseEntry result = new ShowcaseEntry
                {
                    Index = index,
                    Kind = VfxShowcaseEntryKind.CatalogPrefab,
                    ResourceKey = key,
                    DefinitionId = 0,
                    StableName = string.Empty,
                    DefaultLifetimeFrames = 0,
                    DisplayName = ToDisplayName(key.Id),
                    Group = ResolveGroup(key.Id),
                    AssetPath = TryGetAssetPath(entry),
                    LabelText = JoinLabels(entry),
                    KindText = "Catalog Prefab"
                };
                ApplySemantics(result, semantics, recipe, primitives);
                return result;
            }

            private static void ApplySemantics(ShowcaseEntry entry, VfxShowcaseSemanticEffect effect, VfxShowcaseSemanticRecipe recipe, Dictionary<string, VfxShowcaseSemanticPrimitive> primitives)
            {
                if (effect == null)
                {
                    entry.SemanticText = "Uncovered — legacy name classification";
                    entry.SemanticSearchText = string.Empty;
                    entry.RecipeText = "-";
                    entry.BudgetText = "-";
                    entry.RuntimeParameters = Array.Empty<VfxShowcaseRuntimeParameter>();
                    return;
                }

                string roles = Join(effect.roles);
                string themes = Join(effect.themeIds);
                string anchor = effect.anchorContract != null ? effect.anchorContract.anchor + "/" + effect.anchorContract.space : "-";
                entry.DisplayName = string.IsNullOrWhiteSpace(effect.displayName) ? entry.DisplayName : effect.displayName;
                entry.Group = !string.IsNullOrWhiteSpace(roles) ? roles.Split(',')[0].Trim() : entry.Group;
                entry.SemanticText = "role=" + EmptyDash(roles) + ", theme=" + EmptyDash(themes) + ", anchor=" + anchor + ", backend=" + EmptyDash(effect.backend) + ", review=" + EmptyDash(effect.reviewStatus);
                entry.SemanticSearchText = entry.SemanticText + " " + Join(effect.tags);
                entry.RecipeText = recipe == null
                    ? (string.IsNullOrWhiteSpace(effect.recipeId) ? "Published prefab (no Recipe registered)" : effect.recipeId + " (missing)")
                    : BuildRecipeTree(recipe);
                entry.BudgetText = effect.budget == null
                    ? "-"
                    : EmptyDash(effect.quality != null ? effect.quality.costTier : string.Empty) + " cost, max=" + effect.budget.recommendedMaxConcurrent + ", prewarm=" + effect.budget.prewarmCount + ", cull=" + effect.budget.cullDistanceMeters.ToString("0.0") + "m, fallback=" + EmptyDash(effect.quality != null ? effect.quality.fallbackPolicy : string.Empty);
                entry.RuntimeParameters = CollectRuntimeParameters(recipe, primitives);
                entry.HasSemanticPreview = effect.preview != null;
                entry.PreviewAnchorPreset = effect.preview != null ? effect.preview.anchorPreset : string.Empty;
                entry.PreviewTargetDistanceMeters = effect.preview != null ? effect.preview.targetDistanceMeters : 0f;
                entry.PreviewCameraScaleMeters = effect.preview != null ? effect.preview.cameraScaleMeters : 0f;
            }

            private static string BuildRecipeTree(VfxShowcaseSemanticRecipe recipe)
            {
                string text = recipe.recipeId + " — includes: " + EmptyDash(Join(recipe.includedRecipeIds));
                if (recipe.layers == null)
                    return text;
                Array.Sort(recipe.layers, (left, right) => left.order != right.order ? left.order.CompareTo(right.order) : string.Compare(left.layerId, right.layerId, StringComparison.Ordinal));
                for (int i = 0; i < recipe.layers.Length; i++)
                    text += "\n  " + recipe.layers[i].order + ": " + recipe.layers[i].layerId + " → " + recipe.layers[i].primitiveId;
                return text;
            }

            private static VfxShowcaseRuntimeParameter[] CollectRuntimeParameters(VfxShowcaseSemanticRecipe recipe, Dictionary<string, VfxShowcaseSemanticPrimitive> primitives)
            {
                if (recipe == null || recipe.layers == null || primitives == null)
                    return Array.Empty<VfxShowcaseRuntimeParameter>();
                var values = new List<VfxShowcaseRuntimeParameter>();
                for (int layerIndex = 0; layerIndex < recipe.layers.Length; layerIndex++)
                {
                    if (!primitives.TryGetValue(recipe.layers[layerIndex].primitiveId, out VfxShowcaseSemanticPrimitive primitive) || primitive.parameters == null)
                        continue;
                    for (int parameterIndex = 0; parameterIndex < primitive.parameters.Length; parameterIndex++)
                    {
                        VfxShowcaseSemanticParameter parameter = primitive.parameters[parameterIndex];
                        int slot = ParseRuntimeSlot(parameter.transport);
                        if (slot < 0)
                            continue;
                        values.Add(new VfxShowcaseRuntimeParameter(parameter.parameterId, slot, parameter.defaultValue, parameter.minValue, parameter.maxValue, parameter.runtimeScale));
                    }
                }
                return values.ToArray();
            }

            private static int ParseRuntimeSlot(string transport)
            {
                if (string.Equals(transport, "RuntimeSlot0", StringComparison.Ordinal)) return 0;
                if (string.Equals(transport, "RuntimeSlot1", StringComparison.Ordinal)) return 1;
                if (string.Equals(transport, "RuntimeSlot2", StringComparison.Ordinal)) return 2;
                if (string.Equals(transport, "RuntimeSlot3", StringComparison.Ordinal)) return 3;
                return -1;
            }

            private static string Join(string[] values) => values == null || values.Length == 0 ? string.Empty : string.Join(", ", values);
            private static string EmptyDash(string value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

            private static string TryGetAssetPath(ResourceCatalogEntry entry)
            {
                if (entry != null && entry.ProviderData.TryGetValue(TempImportedResourceCatalog.ProviderDataAssetPathKey, out string path))
                    return path;

                return string.Empty;
            }

            private static string JoinLabels(ResourceCatalogEntry entry)
            {
                if (entry == null || entry.Labels.Count == 0)
                    return string.Empty;

                return string.Join(", ", entry.Labels);
            }

            private static string ToDisplayName(string resourceId)
            {
                if (string.IsNullOrWhiteSpace(resourceId))
                    return "VFX";

                string[] parts = resourceId.Split('.');
                if (parts.Length == 0)
                    return resourceId;

                string tail = parts[parts.Length - 1].Replace('_', ' ');
                if (parts.Length >= 3)
                    tail = parts[parts.Length - 2].Replace('_', ' ') + " " + tail;

                return CultureTitle(tail);
            }

            private static string CultureTitle(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return string.Empty;

                char[] chars = value.ToCharArray();
                bool upperNext = true;
                for (int i = 0; i < chars.Length; i++)
                {
                    if (chars[i] == ' ')
                    {
                        upperNext = true;
                        continue;
                    }

                    chars[i] = upperNext ? char.ToUpperInvariant(chars[i]) : chars[i];
                    upperNext = false;
                }

                return new string(chars);
            }

            private static string ResolveGroup(string id)
            {
                if (id.IndexOf("status_aura", StringComparison.Ordinal) >= 0 || id.IndexOf("vfx.status.", StringComparison.Ordinal) >= 0)
                    return "Status Aura";
                if (id.IndexOf("poison", StringComparison.Ordinal) >= 0)
                    return "Poison";
                if (id.IndexOf("area_confinement", StringComparison.Ordinal) >= 0 || id.IndexOf(".zone", StringComparison.Ordinal) >= 0)
                    return "Area";
                if (id.IndexOf(".flight", StringComparison.Ordinal) >= 0)
                    return "Projectile Flight";
                if (id.IndexOf("beam", StringComparison.Ordinal) >= 0
                    || id.IndexOf("laser", StringComparison.Ordinal) >= 0
                    || id.IndexOf("lance", StringComparison.Ordinal) >= 0
                    || id.IndexOf("chain_lightning", StringComparison.Ordinal) >= 0)
                {
                    return "Beam/Laser";
                }

                return id.IndexOf("vfx.magic", StringComparison.Ordinal) >= 0 ? "Magic" : "Other";
            }
        }

        private sealed class ShowcaseEntryComparer : IComparer<ShowcaseEntry>
        {
            public static readonly ShowcaseEntryComparer Instance = new ShowcaseEntryComparer();

            public int Compare(ShowcaseEntry left, ShowcaseEntry right)
            {
                if (ReferenceEquals(left, right))
                    return 0;
                if (left == null)
                    return -1;
                if (right == null)
                    return 1;

                int group = string.Compare(left.Group, right.Group, StringComparison.Ordinal);
                if (group != 0)
                    return group;

                return string.Compare(left.ResourceKey.Id, right.ResourceKey.Id, StringComparison.Ordinal);
            }
        }

        private sealed class DirectPrefabInstance
        {
            public DirectPrefabInstance(ResourceHandle<GameObject> handle, GameObject gameObject, float remainingSeconds, bool isFlight)
            {
                Handle = handle;
                GameObject = gameObject;
                RemainingSeconds = remainingSeconds;
                IsFlight = isFlight;
            }

            public ResourceHandle<GameObject> Handle { get; }
            public GameObject GameObject { get; }
            public float RemainingSeconds { get; set; }
            public bool IsFlight { get; }
        }
    }

    [Serializable]
    internal sealed class VfxShowcaseSemanticCatalog
    {
        public VfxShowcaseSemanticPrimitive[] primitives = Array.Empty<VfxShowcaseSemanticPrimitive>();
        public VfxShowcaseSemanticRecipe[] recipes = Array.Empty<VfxShowcaseSemanticRecipe>();
        public VfxShowcaseSemanticEffect[] effects = Array.Empty<VfxShowcaseSemanticEffect>();
    }

    [Serializable]
    internal sealed class VfxShowcaseSemanticPrimitive
    {
        public string primitiveId = string.Empty;
        public VfxShowcaseSemanticParameter[] parameters = Array.Empty<VfxShowcaseSemanticParameter>();
    }

    [Serializable]
    internal sealed class VfxShowcaseSemanticParameter
    {
        public string parameterId = string.Empty;
        public float defaultValue;
        public float minValue;
        public float maxValue = 1f;
        public string transport = string.Empty;
        public float runtimeScale = 1f;
    }

    internal readonly struct VfxShowcaseRuntimeParameter
    {
        public VfxShowcaseRuntimeParameter(string parameterId, int slot, float defaultValue, float minValue, float maxValue, float runtimeScale)
        {
            ParameterId = parameterId ?? string.Empty;
            Slot = slot;
            DefaultValue = defaultValue;
            MinValue = minValue;
            MaxValue = maxValue > minValue ? maxValue : minValue + 1f;
            RuntimeScale = runtimeScale;
        }

        public string ParameterId { get; }
        public int Slot { get; }
        public float DefaultValue { get; }
        public float MinValue { get; }
        public float MaxValue { get; }
        public float RuntimeScale { get; }
    }

    [Serializable]
    internal sealed class VfxShowcaseSemanticRecipe
    {
        public string recipeId = string.Empty;
        public string[] includedRecipeIds = Array.Empty<string>();
        public VfxShowcaseSemanticRecipeLayer[] layers = Array.Empty<VfxShowcaseSemanticRecipeLayer>();
    }

    [Serializable]
    internal sealed class VfxShowcaseSemanticRecipeLayer
    {
        public string layerId = string.Empty;
        public string primitiveId = string.Empty;
        public int order;
    }

    [Serializable]
    internal sealed class VfxShowcaseSemanticEffect
    {
        public string effectId = string.Empty;
        public string displayName = string.Empty;
        public VfxShowcaseSemanticResourceKey resourceKey = new VfxShowcaseSemanticResourceKey();
        public string recipeId = string.Empty;
        public string backend = string.Empty;
        public string[] roles = Array.Empty<string>();
        public string[] themeIds = Array.Empty<string>();
        public VfxShowcaseSemanticAnchor anchorContract = new VfxShowcaseSemanticAnchor();
        public VfxShowcaseSemanticQuality quality = new VfxShowcaseSemanticQuality();
        public VfxShowcaseSemanticBudget budget = new VfxShowcaseSemanticBudget();
        public VfxShowcaseSemanticPreview preview = new VfxShowcaseSemanticPreview();
        public string reviewStatus = string.Empty;
        public string[] tags = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class VfxShowcaseSemanticResourceKey
    {
        public string id = string.Empty;
        public string typeId = ResourceTypeIds.GameObject;
        public string variant = string.Empty;
        public string packageId = string.Empty;
    }

    [Serializable]
    internal sealed class VfxShowcaseSemanticAnchor
    {
        public string anchor = string.Empty;
        public string space = string.Empty;
    }

    [Serializable]
    internal sealed class VfxShowcaseSemanticQuality
    {
        public string costTier = string.Empty;
        public string fallbackPolicy = string.Empty;
    }

    [Serializable]
    internal sealed class VfxShowcaseSemanticBudget
    {
        public int prewarmCount;
        public int recommendedMaxConcurrent;
        public float cullDistanceMeters;
    }

    [Serializable]
    internal sealed class VfxShowcaseSemanticPreview
    {
        public string anchorPreset = string.Empty;
        public float targetDistanceMeters;
        public float cameraScaleMeters;
    }

    public enum VfxShowcaseEntryKind
    {
        RuntimeDefinition,
        CatalogPrefab
    }

    [Serializable]
    public sealed class VfxShowcaseSerializedAsset
    {
        [SerializeField] private string _resourceId = string.Empty;
        [SerializeField] private string _typeId = ResourceTypeIds.GameObject;
        [SerializeField] private string _variant = string.Empty;
        [SerializeField] private string _packageId = TempImportedResourceCatalog.PackageId;
        [SerializeField] private string _assetPath = string.Empty;
        [SerializeField] private GameObject _prefab = null;

        public VfxShowcaseSerializedAsset(
            string resourceId,
            string typeId,
            string variant,
            string packageId,
            string assetPath,
            GameObject prefab)
        {
            _resourceId = resourceId ?? string.Empty;
            _typeId = string.IsNullOrWhiteSpace(typeId) ? ResourceTypeIds.GameObject : typeId;
            _variant = variant ?? string.Empty;
            _packageId = packageId ?? string.Empty;
            _assetPath = assetPath ?? string.Empty;
            _prefab = prefab;
        }

        public string ResourceId => _resourceId;
        public string TypeId => _typeId;
        public string Variant => _variant;
        public string PackageId => _packageId;
        public string AssetPath => _assetPath;
        public GameObject Prefab => _prefab;

        public ResourceKey CreateKey()
        {
            return new ResourceKey(_resourceId, _typeId, _variant, _packageId);
        }
    }
}

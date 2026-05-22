using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/Runtime Resource Bootstrap")]
    public sealed class CharacterRuntimeResourceBootstrap : MonoBehaviour
    {
        [SerializeField] private string _catalogId = "character.runtime";
        [SerializeField] private string _packageId = string.Empty;
        [SerializeField] private string _characterResourceId = string.Empty;
        [SerializeField] private string _characterResourceVariant = "default";
        [SerializeField] private Transform _spawnParent;
        [SerializeField] private bool _loadOnStart = true;
        [SerializeField] private CharacterRuntimeSerializedResource[] _resources = Array.Empty<CharacterRuntimeSerializedResource>();
        [SerializeField] private bool _warmupAnimationOnLoad = true;
        [SerializeField] private TextAsset _animationSetDefinitionJson;
        [SerializeField] private TextAsset _animationClipRegistryJson;
        [SerializeField] private string _animationSetId = string.Empty;

        private ResourceManager _resourceManager;
        private MemoryResourceProvider _memoryProvider;
        private ResourceCatalog _resourceCatalog;
        private MxAnimationWarmupService _animationWarmupService;
        private MxAnimationWarmupResult _animationWarmupResult;
        private MxAnimationSetDefinition _runtimeAnimationSetDefinition;
        private ResourceHandle<GameObject> _characterHandle;
        private GameObject _characterInstance;

        public ResourceManager ResourceManager => _resourceManager;
        public GameObject CharacterInstance => _characterInstance;
        public MxAnimationWarmupResult AnimationWarmupResult => _animationWarmupResult;
        public string PackageId => _packageId;
        public string CharacterResourceId => _characterResourceId;
        public string CharacterResourceVariant => _characterResourceVariant;
        public string AnimationSetId => _animationSetId;
        public MxAnimationSetDefinition RuntimeAnimationSetDefinition => _runtimeAnimationSetDefinition;

        private void Start()
        {
            if (_loadOnStart)
                LoadCharacter();
        }

        private void OnDestroy()
        {
            ReleaseAnimationWarmup();
            if (_characterInstance != null)
                Destroy(_characterInstance);
            if (_characterHandle != null)
                _resourceManager?.Release(_characterHandle);

            _characterInstance = null;
            _characterHandle = null;
            _resourceManager = null;
            _memoryProvider = null;
            _resourceCatalog = null;
        }

        public bool LoadCharacter()
        {
            EnsureResourceManager();
            if (_characterInstance != null)
                return true;

            WarmupAnimationResources();

            var key = new ResourceKey(_characterResourceId, ResourceTypeIds.GameObject, _characterResourceVariant, _packageId);
            ResourceLoadResult<ResourceHandle<GameObject>> result = _resourceManager.Load<GameObject>(key);
            if (!result.Success)
            {
                Debug.LogError("MxFramework Character: failed to load character resource. key=" + key + " error=" + result.Error.Message, this);
                return false;
            }

            _characterHandle = result.Value;
            Transform parent = _spawnParent != null ? _spawnParent : transform;
            _characterInstance = Instantiate(result.Value.Value, parent);
            _characterInstance.name = result.Value.Value.name + "_RuntimeInstance";

            CharacterDefaultEquipmentRuntimeBinder binder = _characterInstance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
            if (binder != null)
                binder.ConfigureResourceManager(_resourceManager, instantiateDefaultWeapons: true);

            CharacterRuntimeControllerBinding controllerBinding = _characterInstance.GetComponent<CharacterRuntimeControllerBinding>();
            if (controllerBinding != null)
                controllerBinding.Initialize();

            ConfigureRuntimeAnimationPlayback(_characterInstance);
            return true;
        }

        public bool WarmupAnimationResources()
        {
            ReleaseAnimationWarmup();
            if (!_warmupAnimationOnLoad || _animationSetDefinitionJson == null || _animationClipRegistryJson == null)
                return true;

            EnsureResourceManager();
            IReadOnlyList<MxAnimationSetDefinition> definitions;
            try
            {
                definitions = MxAnimationCompiledArtifactJson.LoadSetDefinitions(_animationSetDefinitionJson.text, _packageId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("MxFramework Character: failed to parse animation set definition. " + ex.Message, this);
                return false;
            }

            MxAnimationSetDefinition definition = ResolveAnimationSet(definitions);
            if (definition == null)
            {
                Debug.LogWarning("MxFramework Character: animation set definition is missing. setId=" + _animationSetId, this);
                return false;
            }

            MxAnimationClipRegistry registry;
            try
            {
                registry = MxAnimationCompiledArtifactJson.LoadClipRegistry(_animationClipRegistryJson.text, _resourceCatalog, _packageId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("MxFramework Character: failed to parse animation clip registry. " + ex.Message, this);
                return false;
            }

            _animationWarmupService = new MxAnimationWarmupService(new ResourcePreloadService(_resourceManager));
            _animationWarmupResult = _animationWarmupService.Warmup(new MxAnimationWarmupRequest(
                definition,
                registry,
                _resourceCatalog,
                skipPreloadWhenInvalid: false));
            _runtimeAnimationSetDefinition = definition;
            if (_animationWarmupResult == null || !_animationWarmupResult.Success)
            {
                Debug.LogWarning("MxFramework Character: animation warmup completed with issues. " + FormatWarmupIssues(_animationWarmupResult), this);
                return false;
            }

            return true;
        }

        private void ConfigureRuntimeAnimationPlayback(GameObject characterInstance)
        {
            if (characterInstance == null || !_warmupAnimationOnLoad || _animationSetDefinitionJson == null)
                return;

            CharacterRuntimeLocomotionBlendController locomotion =
                characterInstance.GetComponent<CharacterRuntimeLocomotionBlendController>();
            if (locomotion == null)
                return;

            MxAnimationSetDefinition definition = ResolveRuntimeAnimationSetDefinition();
            if (definition == null)
                return;

            MxAnimationBlend2DDefinition blend = ResolveDefaultLocomotionBlend2D(definition);
            if (blend == null)
            {
                Debug.LogWarning("MxFramework Character: no 2D locomotion blend definition was found. setId="
                    + definition.SetId, characterInstance);
                return;
            }

            Animator animator = characterInstance.GetComponentInChildren<Animator>(includeInactive: true);
            if (animator == null)
            {
                Debug.LogWarning("MxFramework Character: runtime animation backend was not created because the character prefab has no Animator. "
                    + "The locomotion controller will keep fallback motion enabled.", characterInstance);
                return;
            }

            var backend = new UnityPlayablesAnimationBackend(
                animator,
                _resourceManager,
                definition,
                characterInstance.name);
            locomotion.ConfigureAnimationBackend(backend, blend, ownsBackend: true);
        }

        public void EnsureResourceManager()
        {
            if (_resourceManager != null)
                return;

            _memoryProvider = new MemoryResourceProvider();
            var catalogEntries = new List<ResourceCatalogEntry>();
            bool usesMemoryProvider = false;
            bool usesResourcesProvider = false;
            for (int i = 0; i < _resources.Length; i++)
            {
                CharacterRuntimeSerializedResource resource = _resources[i];
                if (resource == null || string.IsNullOrWhiteSpace(resource.Id))
                    continue;

                string address = string.IsNullOrWhiteSpace(resource.Address) ? resource.Id : resource.Address;
                string providerId = string.IsNullOrWhiteSpace(resource.ProviderId) ? ResourcesProvider.Id : resource.ProviderId;
                if (resource.Asset != null)
                {
                    providerId = _memoryProvider.ProviderId;
                    _memoryProvider.Register(address, resource.Asset);
                    usesMemoryProvider = true;
                }
                else if (string.Equals(providerId, ResourcesProvider.Id, StringComparison.Ordinal))
                {
                    usesResourcesProvider = true;
                }

                catalogEntries.Add(new ResourceCatalogEntry(
                    resource.Id,
                    string.IsNullOrWhiteSpace(resource.TypeId) ? ResourceTypeIds.GameObject : resource.TypeId,
                    providerId,
                    address,
                    resource.Variant,
                    string.IsNullOrWhiteSpace(resource.PackageId) ? _packageId : resource.PackageId));
            }

            _resourceCatalog = new ResourceCatalog(_catalogId, _packageId, catalogEntries);
            _resourceManager = new ResourceManager();
            if (usesMemoryProvider)
                _resourceManager.RegisterProvider(_memoryProvider);
            if (usesResourcesProvider)
                _resourceManager.RegisterProvider(new ResourcesProvider());
            _resourceManager.AddCatalog(_resourceCatalog);
            _resourceManager.ValidateCatalogs();
        }

        private void ReleaseAnimationWarmup()
        {
            if (_animationWarmupService != null && _animationWarmupResult != null)
                _animationWarmupService.Release(_animationWarmupResult);
            _animationWarmupResult = null;
            _animationWarmupService = null;
            _runtimeAnimationSetDefinition = null;
        }

        private MxAnimationSetDefinition ResolveRuntimeAnimationSetDefinition()
        {
            if (_runtimeAnimationSetDefinition != null)
                return _runtimeAnimationSetDefinition;

            if (_animationSetDefinitionJson == null)
                return null;

            IReadOnlyList<MxAnimationSetDefinition> definitions;
            try
            {
                definitions = MxAnimationCompiledArtifactJson.LoadSetDefinitions(_animationSetDefinitionJson.text, _packageId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("MxFramework Character: failed to parse animation set definition for playback. " + ex.Message, this);
                return null;
            }

            _runtimeAnimationSetDefinition = ResolveAnimationSet(definitions);
            return _runtimeAnimationSetDefinition;
        }

        private static MxAnimationBlend2DDefinition ResolveDefaultLocomotionBlend2D(MxAnimationSetDefinition definition)
        {
            if (definition == null || definition.Blend2DDefinitions.Count == 0)
                return null;

            for (int i = 0; i < definition.Blend2DDefinitions.Count; i++)
            {
                MxAnimationBlend2DDefinition candidate = definition.Blend2DDefinitions[i];
                if (candidate != null
                    && (candidate.BlendId.IndexOf("locomotion", StringComparison.OrdinalIgnoreCase) >= 0
                        || candidate.BlendId.IndexOf("move", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return candidate;
                }
            }

            return definition.Blend2DDefinitions[0];
        }

        private MxAnimationSetDefinition ResolveAnimationSet(IReadOnlyList<MxAnimationSetDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
                return null;
            if (string.IsNullOrWhiteSpace(_animationSetId))
                return definitions[0];

            for (int i = 0; i < definitions.Count; i++)
            {
                MxAnimationSetDefinition definition = definitions[i];
                if (definition != null && string.Equals(definition.SetId, _animationSetId, StringComparison.Ordinal))
                    return definition;
            }

            return null;
        }

        private static string FormatWarmupIssues(MxAnimationWarmupResult result)
        {
            if (result == null)
                return "result=missing";
            if (result.Issues.Count == 0)
                return "none";

            var messages = new List<string>();
            for (int i = 0; i < result.Issues.Count; i++)
                messages.Add(result.Issues[i].Code + ":" + result.Issues[i].Message);
            return string.Join("; ", messages);
        }
    }

    [Serializable]
    public sealed class CharacterRuntimeSerializedResource
    {
        [SerializeField] private string _id = string.Empty;
        [SerializeField] private string _typeId = ResourceTypeIds.GameObject;
        [SerializeField] private string _providerId = ResourcesProvider.Id;
        [SerializeField] private string _variant = "default";
        [SerializeField] private string _packageId = string.Empty;
        [SerializeField] private string _address = string.Empty;
        [SerializeField] private UnityEngine.Object _asset;

        public string Id => _id;
        public string TypeId => _typeId;
        public string ProviderId => _providerId;
        public string Variant => _variant;
        public string PackageId => _packageId;
        public string Address => _address;
        public UnityEngine.Object Asset => _asset;
    }
}

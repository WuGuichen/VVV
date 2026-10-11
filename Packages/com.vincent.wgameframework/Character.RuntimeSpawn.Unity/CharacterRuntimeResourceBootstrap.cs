using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/Runtime Resource Bootstrap")]
    public sealed class CharacterRuntimeResourceBootstrap : MonoBehaviour
    {
        [SerializeField] private string _catalogId = "character.runtime";
        [SerializeField] private string _importedPackageRoot = string.Empty;
        [SerializeField] private string _packageId = string.Empty;
        [SerializeField] private string _characterResourceId = string.Empty;
        [SerializeField] private string _characterResourceVariant = "default";
        [SerializeField] private Transform _spawnParent;
        [SerializeField] private bool _loadOnStart = true;
        [SerializeField] private CharacterRuntimeSerializedResource[] _resources = Array.Empty<CharacterRuntimeSerializedResource>();
        [SerializeField] private bool _warmupAnimationOnLoad = true;
        [SerializeField] private TextAsset _animationSetDefinitionJson;
        [SerializeField] private TextAsset _animationClipRegistryJson;
        [SerializeField] private string _animationSetDefinitionJsonPath = string.Empty;
        [SerializeField] private string _animationClipRegistryPath = string.Empty;
        [SerializeField] private string _animationResourcePlanJsonPath = string.Empty;
        [SerializeField] private string _animationSetDefinitionContentHash = string.Empty;
        [SerializeField] private string _animationClipRegistryContentHash = string.Empty;
        [SerializeField] private string _animationResourcePlanContentHash = string.Empty;
        [SerializeField] private string _importReportPath = string.Empty;
        [SerializeField] private string _sourcePackageHash = string.Empty;
        [SerializeField] private string _generatedConfigHash = string.Empty;
        [SerializeField] private string _geometryBindingHash = string.Empty;
        [SerializeField] private string _resourceMappingHash = string.Empty;
        [SerializeField] private string _animationSetId = string.Empty;

        private ResourceManager _resourceManager;
        private MemoryResourceProvider _memoryProvider;
        private ResourceCatalog _resourceCatalog;
        private MxAnimationWarmupService _animationWarmupService;
        private MxAnimationWarmupResult _animationWarmupResult;
        private MxAnimationSetDefinition _runtimeAnimationSetDefinition;
        private ResourceHandle<GameObject> _characterHandle;
        private GameObject _characterInstance;
        private CharacterImportedPackage _compositionPackageOverride;
        private CharacterImportedPackage _runtimeCompositionPackage;
        private CharacterRuntimeSpawnResult _runtimeSpawnResult;

        public ResourceManager ResourceManager => _resourceManager;
        public GameObject CharacterInstance => _characterInstance;
        public MxAnimationWarmupResult AnimationWarmupResult => _animationWarmupResult;
        public string PackageId => _packageId;
        public string ImportedPackageRoot => _importedPackageRoot;
        public string CharacterResourceId => _characterResourceId;
        public string CharacterResourceVariant => _characterResourceVariant;
        public string AnimationSetId => _animationSetId;
        public bool HasAnimationArtifacts => _animationSetDefinitionJson != null && _animationClipRegistryJson != null;
        public string AnimationSetDefinitionJsonPath => _animationSetDefinitionJsonPath;
        public string AnimationClipRegistryPath => _animationClipRegistryPath;
        public string AnimationResourcePlanPath => _animationResourcePlanJsonPath;
        public string AnimationSetDefinitionContentHash => _animationSetDefinitionContentHash;
        public string AnimationClipRegistryContentHash => _animationClipRegistryContentHash;
        public string AnimationResourcePlanContentHash => _animationResourcePlanContentHash;
        public string ImportReportPath => _importReportPath;
        public string SourcePackageHash => _sourcePackageHash;
        public string GeneratedConfigHash => _generatedConfigHash;
        public string GeometryBindingHash => _geometryBindingHash;
        public string ResourceMappingHash => _resourceMappingHash;
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
                DestroyRuntimeObject(_characterInstance);
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

            if (!PrepareRuntimeComposition())
                return false;
            if (!WarmupAnimationResources())
                return false;

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

            if (!ComposeRuntimeCharacter(_characterInstance))
            {
                DestroyRuntimeObject(_characterInstance);
                _characterInstance = null;
                if (_characterHandle != null)
                    _resourceManager.Release(_characterHandle);
                _characterHandle = null;
                return false;
            }

            if (!ConfigureRuntimeAnimationPlayback(_characterInstance))
            {
                DestroyRuntimeObject(_characterInstance);
                _characterInstance = null;
                if (_characterHandle != null)
                    _resourceManager.Release(_characterHandle);
                _characterHandle = null;
                return false;
            }
            return true;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        public bool WarmupAnimationResources()
        {
            ReleaseAnimationWarmup();
            if (!_warmupAnimationOnLoad)
                return true;
            if (_animationSetDefinitionJson == null || _animationClipRegistryJson == null)
            {
                Debug.LogError("MxFramework Character: required animation artifacts are missing.", this);
                return false;
            }

            EnsureResourceManager();
            IReadOnlyList<MxAnimationSetDefinition> definitions;
            try
            {
                definitions = MxAnimationCompiledArtifactJson.LoadSetDefinitions(
                    _animationSetDefinitionJson.text,
                    ResolveAnimationResourcePackageId());
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
                registry = MxAnimationCompiledArtifactJson.LoadClipRegistry(
                    _animationClipRegistryJson.text,
                    _resourceCatalog,
                    ResolveAnimationResourcePackageId());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("MxFramework Character: failed to parse animation clip registry. " + ex.Message, this);
                return false;
            }

            ReportMissingRuntimeClipResources(registry);

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

        public bool TryFindSerializedResourceAsset(ResourceKey key, out UnityEngine.Object asset)
        {
            asset = null;
            if (!key.IsValid)
                return false;

            for (int i = 0; i < _resources.Length; i++)
            {
                CharacterRuntimeSerializedResource resource = _resources[i];
                if (resource == null || resource.Asset == null)
                    continue;

                string packageId = string.IsNullOrWhiteSpace(resource.PackageId) ? _packageId : resource.PackageId;
                var candidate = new ResourceKey(resource.Id, resource.TypeId, resource.Variant, packageId);
                if (candidate != key)
                    continue;

                asset = resource.Asset;
                return true;
            }

            return false;
        }

        /// <summary>
        /// LowFreqAlloc: provides an already-loaded imported package for runtime composition.
        /// </summary>
        public void ConfigureImportedPackageForRuntime(CharacterImportedPackage package)
        {
            _compositionPackageOverride = package;
        }

        private bool ConfigureRuntimeAnimationPlayback(GameObject characterInstance)
        {
            if (characterInstance == null || !_warmupAnimationOnLoad || _animationSetDefinitionJson == null)
                return characterInstance != null;

            CharacterRuntimeLocomotionBlendController locomotion =
                characterInstance.GetComponent<CharacterRuntimeLocomotionBlendController>();
            if (locomotion == null)
            {
                Debug.LogError("MxFramework Character: runtime character has no locomotion controller.", characterInstance);
                return false;
            }

            if (!CharacterRuntimeAnimationResources.TryCreateBinding(
                    _runtimeCompositionPackage,
                    out CharacterRuntimeAnimationBinding binding,
                    out _))
            {
                Debug.LogError("MxFramework Character: canonical runtime animation binding is unavailable.", characterInstance);
                return false;
            }

            bool configured = CharacterRuntimeAnimationResources.TryConfigureLocomotion(
                locomotion,
                _resourceManager,
                binding,
                characterInstance.name,
                NullRuntimeLogger.Instance,
                animationSetDefinitionOverride: _animationSetDefinitionJson);
            if (!configured)
                Debug.LogError("MxFramework Character: canonical locomotion backend configuration failed.", characterInstance);
            return configured;
        }

        private bool ComposeRuntimeCharacter(GameObject characterInstance)
        {
            CharacterImportedPackage package = _runtimeCompositionPackage;
            CharacterRuntimeSpawnResult spawnResult = _runtimeSpawnResult;
            if (package == null || spawnResult == null || !spawnResult.IsSuccess)
                return false;

            CharacterRuntimePrefabCompositionResult composition = CharacterRuntimePrefabComposer.Compose(
                characterInstance,
                new CharacterRuntimePrefabCompositionOptions(package, spawnResult)
                {
                    ResourceManager = _resourceManager,
                    InstantiateDefaultWeapons = true,
                    InstallInputMotionController = true,
                    EnableInputMotion = true,
                    UseExternalRuntimeTick = false
                });
            if (composition.Success)
                return true;

            Debug.LogError("MxFramework Character: runtime prefab composition failed. "
                + string.Join("; ", composition.Issues), this);
            return false;
        }

        private bool PrepareRuntimeComposition()
        {
            _runtimeCompositionPackage = LoadImportedPackageForComposition();
            if (_runtimeCompositionPackage == null)
                return false;

            _runtimeSpawnResult = CharacterRuntimeSpawnResolver.Resolve(_runtimeCompositionPackage, default);
            if (_runtimeSpawnResult == null || !_runtimeSpawnResult.IsSuccess)
            {
                Debug.LogError("MxFramework Character: runtime composition failed because spawn resolve failed. "
                    + FormatSpawnIssues(_runtimeSpawnResult), this);
                return false;
            }

            CharacterRuntimeAnimationProfileBinding animationBinding =
                _runtimeSpawnResult.Binding.AnimationProfileBinding;
            _animationSetId = animationBinding.SetId;
            return true;
        }

        private string ResolveAnimationResourcePackageId()
        {
            CharacterRuntimeAnimationProfileBinding animationBinding =
                _runtimeSpawnResult?.Binding?.AnimationProfileBinding ?? default;
            return animationBinding.IsValid ? animationBinding.ResourcePackageId : _packageId;
        }

        private CharacterImportedPackage LoadImportedPackageForComposition()
        {
            if (_compositionPackageOverride != null)
                return _compositionPackageOverride;

            if (string.IsNullOrWhiteSpace(_importedPackageRoot))
            {
                Debug.LogError("MxFramework Character: imported package root is missing; runtime composition cannot run.", this);
                return null;
            }

            try
            {
                string root = Path.GetFullPath(_importedPackageRoot);
                if (!Directory.Exists(root))
                {
                    Debug.LogError("MxFramework Character: imported package root was not found: " + _importedPackageRoot, this);
                    return null;
                }

                return CharacterImportedPackageJson.LoadFromDirectory(root);
            }
            catch (Exception ex)
            {
                Debug.LogError("MxFramework Character: failed to load imported package for runtime composition. "
                    + ex.Message, this);
                return null;
            }
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

        private void ReportMissingRuntimeClipResources(MxAnimationClipRegistry registry)
        {
            if (registry == null || registry.Entries == null || registry.Entries.Count == 0 || _resourceCatalog == null)
                return;

            var missing = new List<string>();
            for (int i = 0; i < registry.Entries.Count; i++)
            {
                ResourceKey key = registry.Entries[i].ClipKey;
                if (!key.IsValid || ContainsCatalogEntry(key))
                    continue;

                missing.Add(key.ToString());
            }

            if (missing.Count == 0)
                return;

            Debug.LogWarning(
                "MxFramework Character: runtime animation bootstrap snapshot is stale. "
                + "The clip registry references animation resources that are missing from CharacterRuntimeResourceBootstrap._resources. "
                + "Reimport the character package or run the Sync Runtime Entry Points menu. Missing="
                + string.Join(", ", missing),
                this);
        }

        private bool ContainsCatalogEntry(ResourceKey key)
        {
            for (int i = 0; i < _resourceCatalog.Entries.Count; i++)
            {
                if (_resourceCatalog.Entries[i].CreateKey(_resourceCatalog.PackageId) == key)
                    return true;
            }

            return false;
        }

        private void ReleaseAnimationWarmup()
        {
            if (_animationWarmupService != null && _animationWarmupResult != null)
                _animationWarmupService.Release(_animationWarmupResult);
            _animationWarmupResult = null;
            _animationWarmupService = null;
            _runtimeAnimationSetDefinition = null;
        }

        private MxAnimationSetDefinition ResolveAnimationSet(IReadOnlyList<MxAnimationSetDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
                return null;
            if (string.IsNullOrWhiteSpace(_animationSetId))
                return null;

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

        private static string FormatSpawnIssues(CharacterRuntimeSpawnResult result)
        {
            if (result == null || result.Issues.Length == 0)
                return "unknown error";

            var messages = new List<string>();
            for (int i = 0; i < result.Issues.Length; i++)
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

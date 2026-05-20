using System;
using System.Collections.Generic;
using MxFramework.Resources;
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

        private ResourceManager _resourceManager;
        private MemoryResourceProvider _memoryProvider;
        private ResourceHandle<GameObject> _characterHandle;
        private GameObject _characterInstance;

        public ResourceManager ResourceManager => _resourceManager;
        public GameObject CharacterInstance => _characterInstance;

        private void Start()
        {
            if (_loadOnStart)
                LoadCharacter();
        }

        private void OnDestroy()
        {
            if (_characterInstance != null)
                Destroy(_characterInstance);
            if (_characterHandle != null)
                _resourceManager?.Release(_characterHandle);

            _characterInstance = null;
            _characterHandle = null;
            _resourceManager = null;
            _memoryProvider = null;
        }

        public bool LoadCharacter()
        {
            EnsureResourceManager();
            if (_characterInstance != null)
                return true;

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

            return true;
        }

        public void EnsureResourceManager()
        {
            if (_resourceManager != null)
                return;

            _memoryProvider = new MemoryResourceProvider();
            var catalogEntries = new List<ResourceCatalogEntry>();
            for (int i = 0; i < _resources.Length; i++)
            {
                CharacterRuntimeSerializedResource resource = _resources[i];
                if (resource == null || resource.Asset == null || string.IsNullOrWhiteSpace(resource.Id))
                    continue;

                string address = string.IsNullOrWhiteSpace(resource.Address) ? resource.Id : resource.Address;
                _memoryProvider.Register(address, resource.Asset);
                catalogEntries.Add(new ResourceCatalogEntry(
                    resource.Id,
                    string.IsNullOrWhiteSpace(resource.TypeId) ? ResourceTypeIds.GameObject : resource.TypeId,
                    _memoryProvider.ProviderId,
                    address,
                    resource.Variant,
                    string.IsNullOrWhiteSpace(resource.PackageId) ? _packageId : resource.PackageId));
            }

            _resourceManager = new ResourceManager();
            _resourceManager.RegisterProvider(_memoryProvider);
            _resourceManager.AddCatalog(new ResourceCatalog(_catalogId, _packageId, catalogEntries));
            _resourceManager.ValidateCatalogs();
        }
    }

    [Serializable]
    public sealed class CharacterRuntimeSerializedResource
    {
        [SerializeField] private string _id = string.Empty;
        [SerializeField] private string _typeId = ResourceTypeIds.GameObject;
        [SerializeField] private string _variant = "default";
        [SerializeField] private string _packageId = string.Empty;
        [SerializeField] private string _address = string.Empty;
        [SerializeField] private UnityEngine.Object _asset;

        public string Id => _id;
        public string TypeId => _typeId;
        public string Variant => _variant;
        public string PackageId => _packageId;
        public string Address => _address;
        public UnityEngine.Object Asset => _asset;
    }
}

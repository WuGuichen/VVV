using System;
using System.Collections.Generic;
using MxFramework.Resources;
using UnityEngine;

namespace MxFramework.Vfx.Unity
{
    /// <summary>One Unity prefab projection for a stable VFX resource key.</summary>
    [Serializable]
    public sealed class VfxUnityPrefabBinding
    {
        [SerializeField] private string _resourceId = string.Empty;
        [SerializeField] private string _resourceTypeId = ResourceTypeIds.GameObject;
        [SerializeField] private string _variant = string.Empty;
        [SerializeField] private string _packageId = string.Empty;
        [SerializeField] private string _address = string.Empty;
        [SerializeField] private GameObject _prefab;

        /// <summary>Gets the stable resource key projected by this entry.</summary>
        public ResourceKey ResourceKey => new ResourceKey(
            _resourceId,
            _resourceTypeId,
            _variant,
            _packageId);

        /// <summary>Gets the provider address used by the generated Resource Catalog entry.</summary>
        public string Address => _address;

        /// <summary>Gets the published Unity prefab projected for this stable key.</summary>
        public GameObject Prefab => _prefab;

        /// <summary>Assigns one generated projection entry.</summary>
        /// <remarks>AllocByDesign; Editor generation and initialization only.</remarks>
        public void Configure(ResourceKey resourceKey, string address, GameObject prefab)
        {
            _resourceId = resourceKey.Id ?? string.Empty;
            _resourceTypeId = resourceKey.TypeId ?? ResourceTypeIds.GameObject;
            _variant = resourceKey.Variant ?? string.Empty;
            _packageId = resourceKey.PackageId ?? string.Empty;
            _address = address ?? string.Empty;
            _prefab = prefab;
        }
    }

    /// <summary>Provides Unity prefab bindings without exposing authoring manifests to Runtime.</summary>
    public interface IVfxUnityPrefabBindingSource
    {
        /// <summary>Copies all bindings into a caller-owned buffer.</summary>
        /// <remarks>LowFreqAlloc during resource-provider initialization.</remarks>
        int CopyBindingsTo(List<VfxUnityPrefabBinding> output);

        /// <summary>Resolves a cached binding by stable resource key.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        bool TryResolve(ResourceKey resourceKey, out VfxUnityPrefabBinding binding);
    }

    /// <summary>
    /// Editor composition roots may install a transient binding source before a Runtime session is built.
    /// </summary>
    public static class VfxUnityPrefabBindingSourceRegistry
    {
        /// <summary>Gets the Editor-installed binding overlay used by subsequently created sessions.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static IVfxUnityPrefabBindingSource Current { get; private set; }

        /// <summary>Installs an Editor-owned source for subsequent session initialization.</summary>
        /// <remarks>LowFreqAlloc; never call from Tick or presentation sync.</remarks>
        public static void SetCurrent(IVfxUnityPrefabBindingSource source)
        {
            Current = source;
        }

        /// <summary>Removes the Editor-installed binding source.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static void Clear()
        {
            Current = null;
        }
    }
}

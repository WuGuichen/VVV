using System;
using System.Collections.Generic;
using MxFramework.Resources;
using UnityEngine;

namespace MxFramework.Vfx.Unity
{
    /// <summary>Generated Unity object projection of the published VFX binding manifest.</summary>
    /// <remarks>
    /// The JSON manifest remains authoritative. Cache construction is LowFreqAlloc;
    /// lookup is NoAlloc after initialization.
    /// </remarks>
    [CreateAssetMenu(menuName = "MxFramework/VFX/Published Unity Bindings")]
    public sealed class VfxPublishedUnityBindingAsset : ScriptableObject, IVfxUnityPrefabBindingSource
    {
        [SerializeField] private string _manifestHash = string.Empty;
        [SerializeField] private List<VfxUnityPrefabBinding> _bindings = new List<VfxUnityPrefabBinding>();

        private Dictionary<ResourceKey, VfxUnityPrefabBinding> _byKey;

        /// <summary>Gets the authoritative manifest hash used to generate this projection.</summary>
        public string ManifestHash => _manifestHash;

        /// <summary>Gets the serialized generated bindings.</summary>
        public IReadOnlyList<VfxUnityPrefabBinding> Bindings => _bindings;

        /// <summary>Replaces generated projection data.</summary>
        /// <remarks>AllocByDesign; Editor generation only.</remarks>
        public void Configure(string manifestHash, List<VfxUnityPrefabBinding> bindings)
        {
            _manifestHash = manifestHash ?? string.Empty;
            _bindings = bindings ?? new List<VfxUnityPrefabBinding>();
            _byKey = null;
        }

        /// <inheritdoc />
        public int CopyBindingsTo(List<VfxUnityPrefabBinding> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int start = output.Count;
            for (int i = 0; i < _bindings.Count; i++)
                output.Add(_bindings[i]);
            return output.Count - start;
        }

        /// <inheritdoc />
        public bool TryResolve(ResourceKey resourceKey, out VfxUnityPrefabBinding binding)
        {
            EnsureLookup();
            return _byKey.TryGetValue(resourceKey, out binding);
        }

        private void OnEnable()
        {
            _byKey = null;
        }

        private void EnsureLookup()
        {
            if (_byKey != null)
                return;

            _byKey = new Dictionary<ResourceKey, VfxUnityPrefabBinding>(_bindings.Count);
            for (int i = 0; i < _bindings.Count; i++)
            {
                VfxUnityPrefabBinding binding = _bindings[i];
                if (binding == null
                    || !binding.ResourceKey.IsValid
                    || string.IsNullOrWhiteSpace(binding.Address)
                    || binding.Prefab == null)
                {
                    continue;
                }
                _byKey[binding.ResourceKey] = binding;
            }
        }
    }
}

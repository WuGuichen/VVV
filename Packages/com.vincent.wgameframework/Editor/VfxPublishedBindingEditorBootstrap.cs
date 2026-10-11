using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MxFramework.Resources;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    [InitializeOnLoad]
    internal static class VfxPublishedBindingEditorBootstrap
    {
        static VfxPublishedBindingEditorBootstrap()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            // Published bindings are project-owned opt-in data. Their absence is valid for a base package consumer.
            if (!File.Exists(VfxPublishedBindingEditorService.ManifestPath))
                return;

            VfxPublishedBindingManifest manifest =
                VfxPublishedBindingEditorService.LoadManifest(out _);
            if (manifest == null)
            {
                // Automatic discovery is non-blocking. Stale project-owned evidence
                // remains available through the explicit authoring validation command.
                return;
            }

            VfxPublishedUnityBindingAsset projection =
                AssetDatabase.LoadAssetAtPath<VfxPublishedUnityBindingAsset>(
                    VfxPublishedBindingEditorService.ProjectionPath);
            string manifestHash = VfxPublishedBindingEditorService.ComputeManifestHash(manifest);
            if (projection == null
                || !string.Equals(projection.ManifestHash, manifestHash, StringComparison.Ordinal))
            {
                // Domain reload is discovery-only. Projection generation is an explicit authoring command.
                return;
            }

            VfxPublishedBindingEditorService.RefreshRuntimeBindingSource();
        }
    }

    internal sealed class VfxEditorPlaytestBindingSource : IVfxUnityPrefabBindingSource
    {
        private readonly List<VfxUnityPrefabBinding> _bindings = new List<VfxUnityPrefabBinding>();
        private readonly Dictionary<ResourceKey, VfxUnityPrefabBinding> _byKey =
            new Dictionary<ResourceKey, VfxUnityPrefabBinding>();

        internal VfxEditorPlaytestBindingSource(VfxPlaytestOverrideDocument document)
        {
            VfxPublishedBindingManifest manifest =
                VfxPublishedBindingEditorService.LoadManifest(out _);
            if (manifest == null)
                return;

            for (int i = 0; i < document.Entries.Count; i++)
            {
                VfxPlaytestOverrideRecord source = document.Entries[i];
                ResourceKey key = source.ResourceKey.ToResourceKey();
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source.CandidateAssetPath);
                VfxCompositionBakeReceipt receipt = null;
                if (!key.IsValid
                    || prefab == null
                    || !VfxPublishedBindingEditorService.TryReadAndValidateReceipt(
                        source.ReceiptPath,
                        source.ReceiptHash,
                        out receipt)
                    || !string.Equals(
                        AssetDatabase.AssetPathToGUID(source.CandidateAssetPath),
                        source.CandidateAssetGuid,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(
                        VfxCompositionDraftService.HashFile(source.CandidateAssetPath),
                        source.CandidateContentHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!string.Equals(
                        receipt.OutputPrefabGuid,
                        source.CandidateAssetGuid,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(
                        receipt.ConsumerResourceKey,
                        key.ToString(),
                        StringComparison.Ordinal))
                    continue;

                VfxPublishedBindingRecord published = manifest.Bindings.FirstOrDefault(
                    value => value.ResourceKey.ToResourceKey() == key);
                VfxConsumerContract contract = VfxConsumerContractResolver.Find(key);
                if (published == null
                    || contract == null
                    || !string.Equals(
                        source.ConsumerContractHash,
                        contract.ContractHash,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(
                        published.ContractHash,
                        contract.ContractHash,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                var binding = new VfxUnityPrefabBinding();
                binding.Configure(key, published.Address, prefab);
                _bindings.Add(binding);
                _byKey[key] = binding;
            }
        }

        public int CopyBindingsTo(List<VfxUnityPrefabBinding> output)
        {
            int start = output.Count;
            output.AddRange(_bindings);
            return output.Count - start;
        }

        public bool TryResolve(ResourceKey resourceKey, out VfxUnityPrefabBinding binding)
        {
            return _byKey.TryGetValue(resourceKey, out binding);
        }
    }

    internal sealed class VfxCompositeUnityBindingSource : IVfxUnityPrefabBindingSource
    {
        private readonly List<VfxUnityPrefabBinding> _bindings = new List<VfxUnityPrefabBinding>();
        private readonly Dictionary<ResourceKey, VfxUnityPrefabBinding> _byKey =
            new Dictionary<ResourceKey, VfxUnityPrefabBinding>();

        internal VfxCompositeUnityBindingSource(IReadOnlyList<IVfxUnityPrefabBindingSource> sources)
        {
            var buffer = new List<VfxUnityPrefabBinding>();
            for (int sourceIndex = sources.Count - 1; sourceIndex >= 0; sourceIndex--)
            {
                buffer.Clear();
                sources[sourceIndex]?.CopyBindingsTo(buffer);
                for (int i = 0; i < buffer.Count; i++)
                {
                    VfxUnityPrefabBinding binding = buffer[i];
                    if (binding != null)
                        _byKey[binding.ResourceKey] = binding;
                }
            }

            _bindings.AddRange(_byKey.Values.OrderBy(value => value.ResourceKey.ToString(), StringComparer.Ordinal));
        }

        public int CopyBindingsTo(List<VfxUnityPrefabBinding> output)
        {
            int start = output.Count;
            output.AddRange(_bindings);
            return output.Count - start;
        }

        public bool TryResolve(ResourceKey resourceKey, out VfxUnityPrefabBinding binding)
        {
            return _byKey.TryGetValue(resourceKey, out binding);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Applies semantic material changes without ever mutating source materials.</summary>
    /// <remarks>Editor-only, AllocByDesign. Candidate assets are transactional until Commit.</remarks>
    internal sealed class VfxSemanticMaterialDerivationSession : IDisposable
    {
        private readonly string _outputRoot;
        private readonly List<string> _createdPaths = new List<string>();
        private readonly Dictionary<string, Material> _derived = new Dictionary<string, Material>(StringComparer.Ordinal);
        private bool _committed;

        public VfxSemanticMaterialDerivationSession(string outputRoot)
        {
            _outputRoot = string.IsNullOrWhiteSpace(outputRoot) ? null : outputRoot.Replace('\\', '/').TrimEnd('/');
        }

        public IReadOnlyList<string> CreatedAssetPaths => _createdPaths;

        public void Apply(
            VfxResolvedLayerAuthoringProfile resolved,
            VfxCompositionLayerView layer,
            VfxCompiledLayerView compiled)
        {
            if (resolved == null || layer == null || compiled == null) return;
            for (int memberIndex = 0; memberIndex < resolved.Members.Count; memberIndex++)
            {
                VfxResolvedComponentAuthoringProfile member = resolved.Members[memberIndex];
                ParticleSystemRenderer renderer = member.ParticleSystem != null
                    ? member.ParticleSystem.GetComponent<ParticleSystemRenderer>()
                    : null;
                Material source = renderer != null ? renderer.sharedMaterial : null;
                if (source == null) continue;
                List<(VfxSemanticOverrideView Semantic, VfxSemanticBindingView Binding)> changes = VfxSemanticParticleSystemAdapter.EffectiveOverridesForMember(layer, member.MemberId)
                    .Select(semantic =>
                    {
                        VfxSemanticBindingView binding = VfxSemanticParticleSystemAdapter.BindingsForProperty(member, semantic.PropertyId)
                            .FirstOrDefault(value => value.Adapter == "DerivedMaterial" && !value.ReadOnly);
                        return (Semantic: semantic, Binding: binding);
                    })
                    .Where(value => value.Binding != null)
                    .ToList();
                if (changes.Count == 0)
                {
                    renderer.sharedMaterial = source;
                    continue;
                }
                Material target = GetOrCreate(source, changes, compiled, member.MemberId);
                if (target == null) continue;
                for (int changeIndex = 0; changeIndex < changes.Count; changeIndex++)
                {
                    (VfxSemanticOverrideView semantic, VfxSemanticBindingView binding) = changes[changeIndex];
                    ApplyValue(target, semantic, binding, compiled, member.MemberId);
                    compiled.Diagnostics.RemoveAll(value => value.Code == "VFXMAT002" && value.MemberId == member.MemberId && value.PropertyId == semantic.PropertyId);
                }
                renderer.sharedMaterial = target;
            }
        }

        public void Commit()
        {
            _committed = true;
        }

        public void TransferPreviewOwnership(GameObject root)
        {
            if (_outputRoot != null || root == null) return;
            VfxPreviewOwnedObjects owner = root.GetComponent<VfxPreviewOwnedObjects>() ?? root.AddComponent<VfxPreviewOwnedObjects>();
            owner.Configure(_derived.Values.Where(value => value != null).Cast<UnityEngine.Object>().ToArray());
            _committed = true;
        }

        public void Dispose()
        {
            if (_committed) return;
            if (_outputRoot == null)
            {
                foreach (Material material in _derived.Values)
                    if (material != null) UnityEngine.Object.DestroyImmediate(material);
                return;
            }
            for (int i = _createdPaths.Count - 1; i >= 0; i--)
                AssetDatabase.DeleteAsset(_createdPaths[i]);
        }

        private Material GetOrCreate(
            Material source,
            IReadOnlyList<(VfxSemanticOverrideView Semantic, VfxSemanticBindingView Binding)> changes,
            VfxCompiledLayerView compiled,
            string memberId)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            string key = VfxCanonicalJson.Hash(new
            {
                sourceGuid,
                sourceHash = string.IsNullOrWhiteSpace(sourcePath) ? string.Empty : AssetDatabase.GetAssetDependencyHash(sourcePath).ToString(),
                changes = changes.Select(value => new
                {
                    value.Semantic.PropertyId,
                    value.Semantic.Operation,
                    value.Semantic.Value,
                    value.Binding.Target,
                    value.Binding.Adapter,
                    value.Binding.Keyword,
                    value.Binding.KeywordRequiredValue
                }).ToArray()
            });
            if (_derived.TryGetValue(key, out Material existing)) return existing;

            var target = new Material(source) { name = source.name + "_MxDerived" };
            if (_outputRoot == null)
            {
                target.hideFlags = HideFlags.HideAndDontSave;
                _derived.Add(key, target);
                return target;
            }

            string folder = _outputRoot + "/Materials";
            VfxCompositionDraftService.EnsureFolder(folder);
            string token = key.Substring("sha256:".Length, 12);
            string path = folder + "/" + VfxCompositionDraftService.FileToken(source.name) + "_" + token + ".mat";
            Material persisted = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (persisted == null)
            {
                AssetDatabase.CreateAsset(target, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                persisted = AssetDatabase.LoadAssetAtPath<Material>(path);
                _createdPaths.Add(path);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
            _derived.Add(key, persisted);
            VfxSemanticRetimeCompiler.AddDiff(
                compiled,
                memberId,
                changes[0].Semantic.PropertyId,
                "DerivedMaterial",
                path,
                sourceGuid,
                "Derive",
                VfxCanonicalJson.Serialize(changes.Select(value => new { value.Semantic.PropertyId, value.Semantic.Operation, value.Semantic.Value }).ToArray()),
                AssetDatabase.AssetPathToGUID(path),
                nameof(VfxSemanticTimeDomain.Static));
            return persisted;
        }

        private static void ApplyValue(Material material, VfxSemanticOverrideView semantic, VfxSemanticBindingView binding, VfxCompiledLayerView compiled, string memberId)
        {
            if (string.Equals(binding.Target, "@RenderQueue", StringComparison.Ordinal))
            {
                int source = material.renderQueue;
                int target = Mathf.Clamp(semantic.IntValue(source), 1000, 5000);
                material.renderQueue = target;
                EditorUtility.SetDirty(material);
                VfxSemanticRetimeCompiler.AddDiff(compiled, memberId, semantic.PropertyId, "DerivedMaterial", "material.renderQueue", source.ToString(), "Replace", target.ToString(), target.ToString(), nameof(VfxSemanticTimeDomain.Static));
                return;
            }
            if (string.IsNullOrWhiteSpace(binding.Target) || !material.HasProperty(binding.Target))
            {
                VfxSemanticRetimeCompiler.Error(compiled, "VFXMAT003", "派生 Material 不包含 Profile 属性：" + binding.Target, memberId, semantic.PropertyId, "EditShaderProfile");
                return;
            }
            if (semantic.PropertyId.StartsWith("render.texture.", StringComparison.Ordinal))
            {
                Texture texture = LoadAsset<Texture>(semantic.Value, compiled, memberId, semantic.PropertyId);
                if (texture == null) return;
                Texture source = material.GetTexture(binding.Target);
                material.SetTexture(binding.Target, texture);
                ApplyKeyword(material, binding, true);
                EditorUtility.SetDirty(material);
                VfxSemanticRetimeCompiler.AddDiff(compiled, memberId, semantic.PropertyId, "DerivedMaterial", binding.Target, AssetGuid(source), "Replace", AssetGuid(texture), AssetGuid(texture), nameof(VfxSemanticTimeDomain.Static));
                return;
            }
            if (semantic.Value.Type == JTokenType.Integer || semantic.Value.Type == JTokenType.Float || semantic.Value.Type == JTokenType.Boolean)
            {
                float source = material.GetFloat(binding.Target);
                float requested = semantic.Value.Type == JTokenType.Boolean
                    ? (semantic.Value.Value<bool>() ? 1f : 0f)
                    : semantic.Value.Value<float>();
                float target = semantic.Operation == nameof(VfxSemanticOperation.Replace)
                    ? requested
                    : semantic.Operation == nameof(VfxSemanticOperation.Add) ? source + requested : source * requested;
                material.SetFloat(binding.Target, target);
                ApplyKeyword(material, binding, binding.KeywordRequiredValue.HasValue
                    ? Mathf.Abs(target - binding.KeywordRequiredValue.Value) <= 0.00001f
                    : target > 0.5f);
                EditorUtility.SetDirty(material);
                VfxSemanticRetimeCompiler.AddDiff(compiled, memberId, semantic.PropertyId, "DerivedMaterial", binding.Target, source.ToString("R"), semantic.Operation, requested.ToString("R"), target.ToString("R"), nameof(VfxSemanticTimeDomain.Static));
                return;
            }
            VfxSemanticRetimeCompiler.Error(compiled, "VFXMAT004", "当前派生 Material Adapter 尚不支持该值类型：" + semantic.PropertyId, memberId, semantic.PropertyId, "EditRawMaterial");
        }

        private static void ApplyKeyword(Material material, VfxSemanticBindingView binding, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(binding.Keyword)) return;
            if (enabled) material.EnableKeyword(binding.Keyword);
            else material.DisableKeyword(binding.Keyword);
        }

        private static T LoadAsset<T>(JToken value, VfxCompiledLayerView compiled, string memberId, string propertyId) where T : UnityEngine.Object
        {
            string reference = value?.Type == JTokenType.String ? value.Value<string>() : string.Empty;
            if (string.IsNullOrWhiteSpace(reference))
            {
                VfxSemanticRetimeCompiler.Error(compiled, "VFXMAT005", "AssetReference 为空。", memberId, propertyId, "SelectAsset");
                return null;
            }
            string path = reference.StartsWith("Assets/", StringComparison.Ordinal)
                ? reference
                : AssetDatabase.GUIDToAssetPath(reference);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                VfxSemanticRetimeCompiler.Error(compiled, "VFXMAT006", "无法解析 AssetReference：" + reference, memberId, propertyId, "SelectAsset");
            return asset;
        }

        private static string AssetGuid(UnityEngine.Object asset)
        {
            return asset == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        }
    }

    [DisallowMultipleComponent]
    internal sealed class VfxPreviewOwnedObjects : MonoBehaviour
    {
        [NonSerialized] private UnityEngine.Object[] _objects = Array.Empty<UnityEngine.Object>();

        public void Configure(UnityEngine.Object[] objects)
        {
            _objects = objects ?? Array.Empty<UnityEngine.Object>();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _objects.Length; i++)
                if (_objects[i] != null) UnityEngine.Object.DestroyImmediate(_objects[i]);
            _objects = Array.Empty<UnityEngine.Object>();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MxFramework.Vfx.Unity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    internal sealed class VfxResolvedComponentAuthoringProfile
    {
        public string MemberId = string.Empty;
        public ParticleSystem ParticleSystem;
        public VfxSourceComponentView Source;
        public VfxParticleSystemSnapshotView Snapshot;
        public VfxComponentProfileView Profile;
        public string ProfileHash = string.Empty;
        public bool Persisted;
        public VfxShaderProfileView ShaderProfile;
        public string ShaderProfileHash = string.Empty;
        public bool ShaderProfilePersisted;
    }

    internal sealed class VfxResolvedLayerAuthoringProfile
    {
        public VfxElementProfileView ElementProfile;
        public bool ElementProfilePersisted;
        public readonly List<VfxResolvedComponentAuthoringProfile> Members = new List<VfxResolvedComponentAuthoringProfile>();
        public readonly List<VfxSemanticDiagnosticView> Diagnostics = new List<VfxSemanticDiagnosticView>();
        public string ProfileDependencyHash = string.Empty;
    }

    /// <summary>Resolves persisted profiles, or produces clearly marked in-memory suggestions.</summary>
    /// <remarks>Editor-only, AllocByDesign.</remarks>
    internal sealed class VfxSemanticProfileResolver
    {
        private readonly VfxSemanticProfileStore _store = new VfxSemanticProfileStore();
        private readonly VfxSourceSnapshotStore _snapshots = new VfxSourceSnapshotStore();

        public VfxResolvedLayerAuthoringProfile Resolve(
            VfxSourceElementCatalog catalog,
            VfxSourceElementView element,
            GameObject instance,
            VfxCompositionLayerView layer,
            bool captureMissingSnapshots)
        {
            var result = new VfxResolvedLayerAuthoringProfile();
            if (catalog == null || element == null || instance == null)
            {
                result.Diagnostics.Add(Diagnostic("VFXPRO100", "无法解析 Layer Profile：Element 或实例为空。", "Error"));
                return result;
            }

            VfxProfileManifestView manifest = _store.LoadManifest(out string manifestMessage);
            if (manifest == null)
            {
                result.Diagnostics.Add(Diagnostic("VFXPRO101", manifestMessage, "Error"));
                manifest = new VfxProfileManifestView();
            }
            VfxProfileDependencyView elementDependency = layer?.ProfileDependencies?.FirstOrDefault(value => value.ProfileId.StartsWith("vfx.element.", StringComparison.Ordinal));
            if (elementDependency != null)
            {
                result.ElementProfile = _store.Load<VfxElementProfileView>(manifest, elementDependency.ProfileId, out string message);
                if (result.ElementProfile == null)
                    result.Diagnostics.Add(Diagnostic("VFXPRO102", message, "Error"));
                else
                {
                    VfxProfileManifestEntryView entry = manifest.Profiles.First(value => value.ProfileId == elementDependency.ProfileId);
                    result.ElementProfilePersisted = IsConfirmed(entry.ReviewState, result.ElementProfile.Status);
                    if (!result.ElementProfilePersisted)
                        result.Diagnostics.Add(Diagnostic("VFXPRO109", "Element Profile 已保存但尚未人工确认：" + elementDependency.ProfileId, "Warning"));
                    ValidateDependencyHash(manifest, elementDependency, result, null);
                }
            }

            IReadOnlyList<VfxSourceComponentView> sourceMembers = catalog.Members(element);
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            int count = Math.Min(sourceMembers.Count, systems.Length);
            if (sourceMembers.Count != systems.Length)
                result.Diagnostics.Add(Diagnostic("VFXPRO103", "Element 成员证据与实例 ParticleSystem 数量不一致；将按稳定顺序匹配可用成员。", "Warning"));

            for (int i = 0; i < count; i++)
            {
                VfxSourceComponentView source = sourceMembers[i];
                ParticleSystem system = systems[i];
                VfxParticleSystemSnapshotView snapshot = _snapshots.Get(source.ComponentId, source.SourceContentHash, out _);
                bool trailSnapshotMissing = snapshot != null && system.trails.enabled && snapshot.Trails == null;
                if (snapshot == null || trailSnapshotMissing)
                {
                    snapshot = VfxParticleSystemSnapshotCapture.Capture(source, system);
                    if (captureMissingSnapshots)
                        _snapshots.Put(snapshot, out _, out _);
                }

                VfxComponentProfileView profile = null;
                bool persisted = false;
                string profileHash = string.Empty;
                string requestedProfileId = ResolveComponentProfileId(layer, result.ElementProfile, source.ComponentId);
                if (!string.IsNullOrWhiteSpace(requestedProfileId))
                {
                    profile = _store.Load<VfxComponentProfileView>(manifest, requestedProfileId, out string message);
                    if (profile == null)
                        result.Diagnostics.Add(Diagnostic("VFXPRO104", message, "Error", source.ComponentId));
                    else
                    {
                        VfxProfileManifestEntryView entry = manifest.Profiles.First(value => value.ProfileId == requestedProfileId);
                        profileHash = entry.ContentHash;
                        persisted = IsConfirmed(entry.ReviewState, profile.Status);
                        if (!persisted)
                            result.Diagnostics.Add(Diagnostic("VFXPRO110", "Component Profile 已保存但尚未人工确认：" + requestedProfileId, "Warning", source.ComponentId));
                        VfxProfileDependencyView dependency = layer?.ProfileDependencies?.FirstOrDefault(value => value.ProfileId == requestedProfileId);
                        if (dependency != null) ValidateDependencyHash(manifest, dependency, result, source.ComponentId);
                        if (!string.Equals(profile.SourceSnapshotHash, snapshot.SnapshotHash, StringComparison.Ordinal))
                        {
                            result.Diagnostics.Add(Diagnostic("VFXPRO107", "Component Profile 的 Source Snapshot 已过期：" + requestedProfileId, "Error", source.ComponentId));
                            persisted = false;
                        }
                    }
                }
                profile ??= VfxParticleSystemProfileSuggestion.Create(source, snapshot, system);
                ValidateTrailContract(profile, snapshot, system, result, source.ComponentId);
                profileHash = string.IsNullOrWhiteSpace(profileHash) ? VfxCanonicalJson.Hash(profile, "contentHash") : profileHash;
                VfxShaderProfileView shaderProfile = ResolveShaderProfile(manifest, layer, system, result, source.ComponentId, out string shaderProfileHash, out bool shaderProfilePersisted);
                result.Members.Add(new VfxResolvedComponentAuthoringProfile
                {
                    MemberId = source.ComponentId,
                    ParticleSystem = system,
                    Source = source,
                    Snapshot = snapshot,
                    Profile = profile,
                    ProfileHash = profileHash,
                    Persisted = persisted,
                    ShaderProfile = shaderProfile,
                    ShaderProfileHash = shaderProfileHash,
                    ShaderProfilePersisted = shaderProfilePersisted
                });
            }
            var dependencies = new List<VfxProfileDependencyView>();
            for (int i = 0; i < result.Members.Count; i++)
            {
                VfxResolvedComponentAuthoringProfile member = result.Members[i];
                dependencies.Add(new VfxProfileDependencyView { ProfileId = member.Profile.ProfileId, Hash = member.ProfileHash });
                if (member.ShaderProfile != null)
                    dependencies.Add(new VfxProfileDependencyView { ProfileId = member.ShaderProfile.ProfileId, Hash = member.ShaderProfileHash });
            }
            result.ProfileDependencyHash = VfxCanonicalJson.Hash(dependencies.OrderBy(value => value.ProfileId, StringComparer.Ordinal).ThenBy(value => value.Hash, StringComparer.Ordinal).ToList());
            return result;
        }

        private static void ValidateTrailContract(
            VfxComponentProfileView profile,
            VfxParticleSystemSnapshotView snapshot,
            ParticleSystem system,
            VfxResolvedLayerAuthoringProfile result,
            string memberId)
        {
            if (system == null || !system.trails.enabled) return;
            VfxTrailAuthoringContractView contract = profile?.Timing?.TrailContract;
            if (contract == null || string.Equals(contract.RetimePolicy, "ReadOnly", StringComparison.Ordinal))
                return;
            if (!string.Equals(contract.RetimePolicy, "PreserveNormalizedLifetime", StringComparison.Ordinal))
            {
                result.Diagnostics.Add(Diagnostic(
                    "VFXTRL001",
                    "未知的 Trail 重定时策略：" + contract.RetimePolicy,
                    "Error",
                    memberId));
                return;
            }
            if (snapshot?.Trails == null
                || string.IsNullOrWhiteSpace(contract.SourceTrailHash)
                || !string.Equals(contract.SourceTrailHash, snapshot.Trails.ContentHash, StringComparison.Ordinal))
            {
                result.Diagnostics.Add(Diagnostic(
                    "VFXTRL002",
                    "Trail 合同与当前 Source Snapshot 不一致；必须重新分析并确认本次合同。",
                    "Error",
                    memberId));
            }
            if (contract.ExpectedDieWithParticles != system.trails.dieWithParticles)
            {
                result.Diagnostics.Add(Diagnostic(
                    "VFXTRL003",
                    "Trail dieWithParticles 已偏离确认合同。",
                    "Error",
                    memberId));
            }
        }

        private static void ValidateDependencyHash(VfxProfileManifestView manifest, VfxProfileDependencyView dependency, VfxResolvedLayerAuthoringProfile result, string memberId)
        {
            if (dependency == null || string.IsNullOrWhiteSpace(dependency.Hash)) return;
            VfxProfileManifestEntryView entry = manifest.Profiles.FirstOrDefault(value => value.ProfileId == dependency.ProfileId);
            if (entry == null || string.Equals(entry.ContentHash, dependency.Hash, StringComparison.Ordinal)) return;
            result.Diagnostics.Add(Diagnostic("VFXPRO108", "Draft 绑定的 Profile hash 已过期：" + dependency.ProfileId, "Error", memberId));
        }

        private VfxShaderProfileView ResolveShaderProfile(
            VfxProfileManifestView manifest,
            VfxCompositionLayerView layer,
            ParticleSystem system,
            VfxResolvedLayerAuthoringProfile result,
            string memberId,
            out string profileHash,
            out bool persisted)
        {
            profileHash = string.Empty;
            persisted = false;
            ParticleSystemRenderer renderer = system != null ? system.GetComponent<ParticleSystemRenderer>() : null;
            Material material = renderer != null ? renderer.sharedMaterial : null;
            Shader shader = material != null ? material.shader : null;
            string shaderPath = shader != null ? AssetDatabase.GetAssetPath(shader) : string.Empty;
            string shaderGuid = string.IsNullOrWhiteSpace(shaderPath) ? string.Empty : AssetDatabase.AssetPathToGUID(shaderPath);
            if (string.IsNullOrWhiteSpace(shaderGuid)) return null;

            IEnumerable<VfxProfileManifestEntryView> entries = manifest.Profiles.Where(value => string.Equals(value.Kind, "Shader", StringComparison.Ordinal));
            VfxProfileDependencyView requested = layer?.ProfileDependencies?.FirstOrDefault(value => value.ProfileId.StartsWith("vfx.shader.", StringComparison.Ordinal));
            if (requested != null)
            {
                VfxProfileManifestEntryView requestedEntry = manifest.Profiles.FirstOrDefault(value => value.ProfileId == requested.ProfileId && value.Kind == "Shader");
                if (requestedEntry != null) entries = new[] { requestedEntry }.Concat(entries.Where(value => value != requestedEntry));
            }

            foreach (VfxProfileManifestEntryView entry in entries)
            {
                VfxShaderProfileView candidate = _store.Load<VfxShaderProfileView>(manifest, entry.ProfileId, out string message);
                if (candidate == null)
                {
                    result.Diagnostics.Add(Diagnostic("VFXPRO105", message, "Error", memberId));
                    continue;
                }
                if (!string.Equals(candidate.ShaderGuid, shaderGuid, StringComparison.Ordinal)) continue;
                string liveHash = AssetDatabase.GetAssetDependencyHash(shaderPath).ToString();
                if (!string.IsNullOrWhiteSpace(candidate.ShaderHash) && !string.Equals(candidate.ShaderHash, liveHash, StringComparison.Ordinal))
                {
                    result.Diagnostics.Add(Diagnostic("VFXPRO106", "Shader Profile 已过期：" + candidate.ProfileId, "Error", memberId));
                    return candidate;
                }
                ValidateShaderProfile(candidate, renderer, result, memberId);
                profileHash = string.IsNullOrWhiteSpace(entry.ContentHash) ? VfxCanonicalJson.Hash(candidate, "contentHash") : entry.ContentHash;
                if (requested != null && requested.ProfileId == candidate.ProfileId)
                    ValidateDependencyHash(manifest, requested, result, memberId);
                persisted = IsConfirmed(entry.ReviewState, candidate.Status);
                if (!persisted)
                    result.Diagnostics.Add(Diagnostic("VFXPRO111", "Shader Profile 已保存但尚未人工确认：" + candidate.ProfileId, "Warning", memberId));
                return candidate;
            }

            VfxShaderProfileView suggested = VfxShaderProfileSuggestion.Create(renderer);
            if (suggested != null)
            {
                ValidateShaderProfile(suggested, renderer, result, memberId);
                profileHash = VfxCanonicalJson.Hash(suggested, "contentHash");
            }
            return suggested;
        }

        private static void ValidateShaderProfile(VfxShaderProfileView profile, ParticleSystemRenderer renderer, VfxResolvedLayerAuthoringProfile result, string memberId)
        {
            if (profile == null || renderer == null) return;
            Material material = renderer.sharedMaterial;
            if (material == null)
            {
                result.Diagnostics.Add(Diagnostic("VFXSHD003", "Renderer 没有可验证的 Material。", "Error", memberId));
                return;
            }
            for (int i = 0; i < profile.Bindings.Count; i++)
            {
                VfxShaderPropertyBindingView binding = profile.Bindings[i];
                if (!string.IsNullOrWhiteSpace(binding.ShaderPropertyName)
                    && binding.ShaderPropertyName[0] != '@'
                    && !material.HasProperty(binding.ShaderPropertyName))
                    result.Diagnostics.Add(Diagnostic("VFXSHD004", "Shader Profile 属性不存在：" + binding.ShaderPropertyName, "Error", memberId));
            }
            var active = new List<ParticleSystemVertexStream>();
            renderer.GetActiveVertexStreams(active);
            var names = new HashSet<string>(active.Select(value => value.ToString()), StringComparer.Ordinal);
            for (int i = 0; i < profile.RequiredVertexStreams.Count; i++)
            {
                string required = profile.RequiredVertexStreams[i];
                if (!names.Contains(required))
                    result.Diagnostics.Add(Diagnostic("VFXSHD001", "缺少 Shader Profile 要求的 Vertex Stream：" + required, "Error", memberId));
            }
            if (profile.CustomDataBindings.Any(value => value.Editable)
                && !profile.RequiredVertexStreams.Any(value => value.StartsWith("Custom", StringComparison.Ordinal)))
                result.Diagnostics.Add(Diagnostic("VFXSHD002", "Shader Profile 声明了 Custom Data，但没有声明对应 Vertex Stream。", "Error", memberId));
            ParticleSystem system = renderer.GetComponent<ParticleSystem>();
            if (system == null) return;
            ParticleSystem.CustomDataModule customData = system.customData;
            for (int i = 0; i < profile.CustomDataBindings.Count; i++)
            {
                VfxShaderCustomDataBindingView binding = profile.CustomDataBindings[i];
                if (!binding.Editable) continue;
                ParticleSystemCustomData stream = string.Equals(binding.Stream, "Custom2", StringComparison.Ordinal)
                    ? ParticleSystemCustomData.Custom2
                    : ParticleSystemCustomData.Custom1;
                if (!customData.enabled || customData.GetMode(stream) == ParticleSystemCustomDataMode.Disabled)
                    result.Diagnostics.Add(Diagnostic("VFXSHD005", "可编辑 Custom Data 绑定没有对应的 ParticleSystem 数据流：" + binding.Stream, "Error", memberId));
            }
        }

        private static string ResolveComponentProfileId(VfxCompositionLayerView layer, VfxElementProfileView elementProfile, string componentId)
        {
            if (elementProfile != null)
            {
                VfxElementProfileMemberView member = elementProfile.Members.FirstOrDefault(value => value.MemberId == componentId);
                if (member != null) return member.ComponentProfileId;
            }
            if (layer?.ProfileDependencies == null) return string.Empty;
            VfxProfileDependencyView direct = layer.ProfileDependencies.FirstOrDefault(value => value.ProfileId.StartsWith("vfx.component.", StringComparison.Ordinal) && value.ProfileId.Contains(VfxCompositionDraftService.FileToken(componentId), StringComparison.Ordinal));
            return direct?.ProfileId ?? string.Empty;
        }

        internal static bool IsConfirmed(string manifestState, string profileState)
        {
            return string.Equals(manifestState, nameof(VfxProfileReviewState.Confirmed), StringComparison.Ordinal)
                && string.Equals(profileState, nameof(VfxProfileReviewState.Confirmed), StringComparison.Ordinal);
        }

        private static VfxSemanticDiagnosticView Diagnostic(string code, string message, string severity, string memberId = null)
        {
            return new VfxSemanticDiagnosticView { Code = code, Message = message, Severity = severity, MemberId = memberId };
        }
    }
}

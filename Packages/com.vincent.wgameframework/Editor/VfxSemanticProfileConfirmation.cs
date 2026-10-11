using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    [Serializable]
    internal sealed class VfxSemanticProfileGapReportView
    {
        [JsonProperty("schemaVersion", Order = 0)] public string SchemaVersion = "mx.vfx.profile-gap-report.v1";
        [JsonProperty("elementId", Order = 1)] public string ElementId = string.Empty;
        [JsonProperty("manifestHash", Order = 2)] public string ManifestHash = string.Empty;
        [JsonProperty("componentCount", Order = 3)] public int ComponentCount;
        [JsonProperty("confirmedComponentCount", Order = 4)] public int ConfirmedComponentCount;
        [JsonProperty("shaderProfileCount", Order = 5)] public int ShaderProfileCount;
        [JsonProperty("confirmedShaderProfileCount", Order = 6)] public int ConfirmedShaderProfileCount;
        [JsonProperty("elementProfileConfirmed", Order = 7)] public bool ElementProfileConfirmed;
        [JsonProperty("missingComponentProfileIds", Order = 8)] public List<string> MissingComponentProfileIds = new List<string>();
        [JsonProperty("missingShaderProfileIds", Order = 9)] public List<string> MissingShaderProfileIds = new List<string>();
        [JsonProperty("capabilityCounts", Order = 10)] public SortedDictionary<string, int> CapabilityCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        [JsonProperty("capabilityLimitations", Order = 11)] public List<string> CapabilityLimitations = new List<string>();
        [JsonProperty("semanticBakeBlockers", Order = 12)] public List<string> SemanticBakeBlockers = new List<string>();
        [JsonProperty("sourceEvidenceHashes", Order = 13)] public List<string> SourceEvidenceHashes = new List<string>();
        [JsonProperty("reportHash", Order = 14)] public string ReportHash = string.Empty;

        public string ToStableJson()
        {
            ReportHash = string.Empty;
            ReportHash = VfxCanonicalJson.Hash(this, "reportHash");
            return JsonConvert.SerializeObject(this, Formatting.Indented, VfxCompositionDraftService.JsonSettings) + Environment.NewLine;
        }
    }

    /// <summary>Builds deterministic evidence about which semantic properties are safe, suggested or blocked.</summary>
    /// <remarks>Editor-only, AllocByDesign. The report never changes Profile review state.</remarks>
    internal static class VfxSemanticProfileGapReportBuilder
    {
        public static VfxSemanticProfileGapReportView Build(
            VfxSourceElementView element,
            VfxResolvedLayerAuthoringProfile resolved,
            VfxSemanticPropertyRegistryView registry,
            VfxProfileManifestView manifest)
        {
            var report = new VfxSemanticProfileGapReportView
            {
                ElementId = element?.ElementId ?? string.Empty,
                ManifestHash = manifest?.ManifestHash ?? string.Empty,
                ComponentCount = resolved?.Members?.Count ?? 0,
                ConfirmedComponentCount = resolved?.Members?.Count(value => value.Persisted) ?? 0,
                ShaderProfileCount = resolved?.Members?.Count(value => value.ShaderProfile != null) ?? 0,
                ConfirmedShaderProfileCount = resolved?.Members?.Count(value => value.ShaderProfile != null && value.ShaderProfilePersisted) ?? 0,
                ElementProfileConfirmed = resolved?.ElementProfilePersisted == true
            };

            if (resolved?.Members != null)
            {
                for (int memberIndex = 0; memberIndex < resolved.Members.Count; memberIndex++)
                {
                    VfxResolvedComponentAuthoringProfile member = resolved.Members[memberIndex];
                    if (!member.Persisted)
                        report.MissingComponentProfileIds.Add(member.Profile?.ProfileId ?? "vfx.component." + VfxCompositionDraftService.FileToken(member.MemberId));
                    if (member.ShaderProfile != null && !member.ShaderProfilePersisted)
                        report.MissingShaderProfileIds.Add(member.ShaderProfile.ProfileId);
                    AddEvidence(report.SourceEvidenceHashes, member.Source?.SourceContentHash);
                    AddEvidence(report.SourceEvidenceHashes, member.Source?.DependencyHash);
                    AddEvidence(report.SourceEvidenceHashes, member.Snapshot?.SnapshotHash);
                }
            }

            IReadOnlyList<VfxSemanticPropertyCapabilityRow> capabilities =
                VfxSemanticPropertyCapabilityAnalyzer.Analyze(registry, resolved?.Members, string.Empty);
            for (int capabilityIndex = 0; capabilityIndex < capabilities.Count; capabilityIndex++)
            {
                string state = capabilities[capabilityIndex].State.ToString();
                report.CapabilityCounts[state] = report.CapabilityCounts.TryGetValue(state, out int count) ? count + 1 : 1;
            }

            if (resolved?.Diagnostics != null)
            {
                report.SemanticBakeBlockers.AddRange(resolved.Diagnostics
                    .Where(value => string.Equals(value.Severity, "Error", StringComparison.Ordinal))
                    .Select(value => value.Code + " · " + value.Message));
            }
            int unsupported = report.CapabilityCounts.TryGetValue(nameof(VfxSemanticPropertyCapabilityState.Unsupported), out int unsupportedCount)
                ? unsupportedCount
                : 0;
            int reserved = report.CapabilityCounts.TryGetValue(nameof(VfxSemanticPropertyCapabilityState.Reserved), out int reservedCount)
                ? reservedCount
                : 0;
            if (unsupported > 0 || reserved > 0)
                report.CapabilityLimitations.Add(unsupported + " 项属性不支持，" + reserved + " 项属性仍为预留合同；只有实际使用相关语义操作时才阻断。");

            report.MissingComponentProfileIds = report.MissingComponentProfileIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            report.MissingShaderProfileIds = report.MissingShaderProfileIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            report.CapabilityLimitations = report.CapabilityLimitations.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            report.SemanticBakeBlockers = report.SemanticBakeBlockers.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            report.SourceEvidenceHashes = report.SourceEvidenceHashes.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            return report;
        }

        private static void AddEvidence(ICollection<string> target, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) target.Add(value);
        }
    }

    internal sealed class VfxProfileImpactAnalysisView
    {
        public string ElementId = string.Empty;
        public int DraftConsumers;
        public int LayerConsumers;
        public int ComponentCount;
        public int TotalComponentCount;
        public int ConfirmedComponentCount;
        public int ShaderProfileCount;
        public int ConfirmedShaderProfileCount;
        public bool ElementProfileConfirmed;
        public string ScopeMemberId = string.Empty;
        public int SharedProfileDraftConsumers;
        public readonly List<string> DraftIds = new List<string>();
        public readonly List<string> AffectedProfileIds = new List<string>();

        public string CoverageSummary => "Profile 覆盖：Component " + ConfirmedComponentCount + "/" + TotalComponentCount
            + " · Shader " + ConfirmedShaderProfileCount + "/" + ShaderProfileCount
            + " · Element " + (ElementProfileConfirmed ? "已确认" : "未确认")
            + (ConfirmedComponentCount < TotalComponentCount ? " · " + (TotalComponentCount - ConfirmedComponentCount) + " 个成员尚未发布共享规则（不阻断 Candidate）" : string.Empty);

        public string Summary => string.IsNullOrWhiteSpace(ScopeMemberId)
            ? "将确认整个 Element：" + ComponentCount + " 个 Component Profile、相关 Shader Profile 与 1 个 Element Profile；直接影响 "
                + DraftConsumers + " 个草稿 / " + LayerConsumers + " 个 Layer，共享 Profile 还被 " + SharedProfileDraftConsumers + " 个草稿引用。"
            : "将只确认当前成员的 Component Profile 与相关 Shader Profile，不创建或覆盖 Element Profile。\n成员：" + ScopeMemberId
                + "\n该 Element 直接用于 " + DraftConsumers + " 个草稿 / " + LayerConsumers + " 个 Layer。";
    }

    /// <summary>Creates guarded shared profiles only after the caller performs explicit human confirmation.</summary>
    /// <remarks>Editor-only, AllocByDesign.</remarks>
    internal sealed class VfxSemanticProfileConfirmationService
    {
        private readonly VfxSemanticProfileStore _store = new VfxSemanticProfileStore();

        public VfxProfileImpactAnalysisView Analyze(
            VfxSourceElementView element,
            VfxCompositionDraftService drafts,
            VfxResolvedLayerAuthoringProfile resolved = null,
            string scopeMemberId = null)
        {
            List<VfxResolvedComponentAuthoringProfile> allMembers = resolved?.Members?.ToList()
                ?? new List<VfxResolvedComponentAuthoringProfile>();
            List<VfxResolvedComponentAuthoringProfile> scopedMembers = string.IsNullOrWhiteSpace(scopeMemberId)
                ? allMembers
                : allMembers.Where(value => string.Equals(value.MemberId, scopeMemberId, StringComparison.Ordinal)).ToList();
            var result = new VfxProfileImpactAnalysisView
            {
                ElementId = element?.ElementId ?? string.Empty,
                ComponentCount = scopedMembers.Count > 0 ? scopedMembers.Count : string.IsNullOrWhiteSpace(scopeMemberId) ? element?.MemberComponentIds?.Count ?? 0 : 0,
                TotalComponentCount = allMembers.Count > 0 ? allMembers.Count : element?.MemberComponentIds?.Count ?? 0,
                ConfirmedComponentCount = allMembers.Count(value => value.Persisted),
                ShaderProfileCount = allMembers.Count(value => value.ShaderProfile != null),
                ConfirmedShaderProfileCount = allMembers.Count(value => value.ShaderProfile != null && value.ShaderProfilePersisted),
                ElementProfileConfirmed = resolved?.ElementProfilePersisted == true,
                ScopeMemberId = scopeMemberId ?? string.Empty
            };
            var profileIds = new HashSet<string>(StringComparer.Ordinal);
            if (scopedMembers.Count > 0)
            {
                for (int i = 0; i < scopedMembers.Count; i++)
                {
                    VfxResolvedComponentAuthoringProfile member = scopedMembers[i];
                    profileIds.Add("vfx.component." + VfxCompositionDraftService.FileToken(member.MemberId));
                    if (member.ShaderProfile != null && !string.IsNullOrWhiteSpace(member.ShaderProfile.ProfileId))
                        profileIds.Add(member.ShaderProfile.ProfileId);
                }
            }
            else if (element?.MemberComponentIds != null && string.IsNullOrWhiteSpace(scopeMemberId))
            {
                for (int i = 0; i < element.MemberComponentIds.Count; i++)
                    profileIds.Add("vfx.component." + VfxCompositionDraftService.FileToken(element.MemberComponentIds[i]));
            }
            result.AffectedProfileIds.AddRange(profileIds.OrderBy(value => value, StringComparer.Ordinal));
            var sharedDraftIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<string> paths = drafts.ListDrafts();
            for (int i = 0; i < paths.Count; i++)
            {
                VfxCompositionDraftView draft = drafts.Load(paths[i], out _);
                if (draft == null) continue;
                int layers = draft.Layers.Count(value => value.ElementId == result.ElementId);
                if (layers <= 0) continue;
                result.DraftConsumers++;
                result.LayerConsumers += layers;
                result.DraftIds.Add(draft.DraftId);
                if (draft.Layers.Any(layer => layer.ProfileDependencies.Any(dependency => profileIds.Contains(dependency.ProfileId))))
                    sharedDraftIds.Add(draft.DraftId);
            }
            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                VfxCompositionDraftView draft = drafts.Load(paths[pathIndex], out _);
                if (draft?.Layers == null) continue;
                if (draft.Layers.Any(layer => layer.ProfileDependencies.Any(dependency => profileIds.Contains(dependency.ProfileId))))
                    sharedDraftIds.Add(draft.DraftId);
            }
            result.SharedProfileDraftConsumers = sharedDraftIds.Count;
            return result;
        }

        public bool Confirm(
            VfxSourceElementView element,
            VfxResolvedLayerAuthoringProfile resolved,
            VfxCompositionLayerView layer,
            string expectedManifestHash,
            string scopeMemberId,
            out string message)
        {
            if (element == null || resolved == null || resolved.Members.Count == 0 || layer == null)
            {
                message = "没有可确认的 Element/Profile。";
                return false;
            }
            VfxProfileManifestView manifest = _store.LoadManifest(out message);
            if (manifest == null) return false;
            if (!string.IsNullOrWhiteSpace(expectedManifestHash) && !string.Equals(expectedManifestHash, manifest.ManifestHash, StringComparison.Ordinal))
            {
                message = "Profile Manifest 已变化；请刷新影响分析。";
                return false;
            }

            List<VfxResolvedComponentAuthoringProfile> targetMembers = string.IsNullOrWhiteSpace(scopeMemberId)
                ? resolved.Members.ToList()
                : resolved.Members.Where(value => string.Equals(value.MemberId, scopeMemberId, StringComparison.Ordinal)).ToList();
            if (targetMembers.Count == 0)
            {
                message = "当前编辑成员不属于该 Element，无法确认 Profile。";
                return false;
            }
            var targetIds = new HashSet<string>(targetMembers.Select(value => value.MemberId), StringComparer.Ordinal);
            VfxSemanticDiagnosticView blockingDiagnostic = resolved.Diagnostics.FirstOrDefault(value =>
                string.Equals(value.Severity, "Error", StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(value.MemberId) || targetIds.Contains(value.MemberId)));
            if (blockingDiagnostic != null)
            {
                message = "Profile 解析仍有阻断错误，不能确认：" + blockingDiagnostic.Code + " · " + blockingDiagnostic.Message;
                return false;
            }

            var writes = new List<VfxProfileBatchWriteView>();
            VfxElementProfileView elementProfile = string.IsNullOrWhiteSpace(scopeMemberId)
                ? new VfxElementProfileView
                {
                    ProfileId = "vfx.element.profile." + VfxCompositionDraftService.FileToken(element.ElementId),
                    ElementId = element.ElementId,
                    Status = nameof(VfxProfileReviewState.Confirmed)
                }
                : null;
            var confirmedShaders = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < targetMembers.Count; i++)
            {
                VfxResolvedComponentAuthoringProfile member = targetMembers[i];
                VfxComponentProfileView profile = Clone(member.Profile);
                profile.ProfileId = "vfx.component." + VfxCompositionDraftService.FileToken(member.MemberId);
                profile.Status = nameof(VfxProfileReviewState.Confirmed);
                profile.ContentHash = string.Empty;
                string relative = "Components/" + VfxCompositionDraftService.FileToken(profile.ProfileId) + ".json";
                var sourceDependencies = new[] { member.Source.SourceContentHash, member.Source.DependencyHash, member.Snapshot.SnapshotHash }
                    .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
                writes.Add(new VfxProfileBatchWriteView
                {
                    Profile = profile,
                    ProfileId = profile.ProfileId,
                    Kind = "Component",
                    RelativePath = relative,
                    SourceDependencies = sourceDependencies,
                    ReviewState = profile.Status
                });

                if (member.ShaderProfile != null && confirmedShaders.Add(member.ShaderProfile.ProfileId))
                {
                    VfxShaderProfileView shaderProfile = Clone(member.ShaderProfile);
                    shaderProfile.Status = nameof(VfxProfileReviewState.Confirmed);
                    shaderProfile.ContentHash = string.Empty;
                    string shaderRelative = "Shaders/" + VfxCompositionDraftService.FileToken(shaderProfile.ProfileId) + ".json";
                    var shaderDependencies = new[] { shaderProfile.ShaderGuid, shaderProfile.ShaderHash }
                        .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
                    writes.Add(new VfxProfileBatchWriteView
                    {
                        Profile = shaderProfile,
                        ProfileId = shaderProfile.ProfileId,
                        Kind = "Shader",
                        RelativePath = shaderRelative,
                        SourceDependencies = shaderDependencies,
                        ReviewState = shaderProfile.Status
                    });
                }
                elementProfile?.Members.Add(new VfxElementProfileMemberView
                {
                    MemberId = member.MemberId,
                    ComponentProfileId = profile.ProfileId,
                    Responsibility = ResolveResponsibility(member.Source),
                    DefaultRetimeMode = ResolveDefaultRetime(profile),
                    Participation = BuildParticipation(profile, member.ShaderProfile)
                });
            }

            if (elementProfile != null)
            {
                elementProfile.ContentHash = string.Empty;
                string elementRelative = "Elements/" + VfxCompositionDraftService.FileToken(elementProfile.ProfileId) + ".json";
                writes.Add(new VfxProfileBatchWriteView
                {
                    Profile = elementProfile,
                    ProfileId = elementProfile.ProfileId,
                    Kind = "Element",
                    RelativePath = elementRelative,
                    SourceDependencies = element.SourceContentHashes,
                    ReviewState = elementProfile.Status
                });
            }
            if (!_store.SaveBatchGuarded(writes, manifest.ManifestHash, out VfxProfileManifestView savedManifest, out message)) return false;
            List<VfxProfileDependencyView> confirmedDependencies = writes
                .Select(write => savedManifest.Profiles.First(value => value.ProfileId == write.ProfileId))
                .Select(entry => new VfxProfileDependencyView { ProfileId = entry.ProfileId, Hash = entry.ContentHash })
                .OrderByDescending(value => elementProfile != null && string.Equals(value.ProfileId, elementProfile.ProfileId, StringComparison.Ordinal))
                .ThenBy(value => value.ProfileId, StringComparer.Ordinal)
                .ToList();
            layer.ProfileDependencies = MergeDependencies(layer.ProfileDependencies, confirmedDependencies);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            message = string.IsNullOrWhiteSpace(scopeMemberId)
                ? "整个 Element 的共享 Profile 已确认并绑定到当前 Layer；Draft 仍需单独保存。"
                : "当前成员的 Component/Shader Profile 已确认并绑定到当前 Layer；其他成员仍保持原状态，Draft 仍需单独保存。";
            return true;
        }

        public bool Confirm(
            VfxSourceElementView element,
            VfxResolvedLayerAuthoringProfile resolved,
            VfxCompositionLayerView layer,
            string expectedManifestHash,
            out string message)
        {
            return Confirm(element, resolved, layer, expectedManifestHash, string.Empty, out message);
        }

        internal static List<VfxProfileDependencyView> MergeDependencies(
            IReadOnlyList<VfxProfileDependencyView> existing,
            IReadOnlyList<VfxProfileDependencyView> confirmed)
        {
            var byId = new Dictionary<string, VfxProfileDependencyView>(StringComparer.Ordinal);
            if (existing != null)
            {
                for (int i = 0; i < existing.Count; i++)
                {
                    VfxProfileDependencyView dependency = existing[i];
                    if (dependency == null || string.IsNullOrWhiteSpace(dependency.ProfileId)) continue;
                    byId[dependency.ProfileId] = new VfxProfileDependencyView { ProfileId = dependency.ProfileId, Hash = dependency.Hash };
                }
            }
            if (confirmed != null)
            {
                for (int i = 0; i < confirmed.Count; i++)
                {
                    VfxProfileDependencyView dependency = confirmed[i];
                    if (dependency == null || string.IsNullOrWhiteSpace(dependency.ProfileId)) continue;
                    byId[dependency.ProfileId] = new VfxProfileDependencyView { ProfileId = dependency.ProfileId, Hash = dependency.Hash };
                }
            }
            return byId.Values
                .OrderByDescending(value => value.ProfileId.StartsWith("vfx.element.", StringComparison.Ordinal))
                .ThenBy(value => value.ProfileId, StringComparer.Ordinal)
                .ToList();
        }

        private static T Clone<T>(T source)
        {
            string json = JsonConvert.SerializeObject(source, VfxCompositionDraftService.JsonSettings);
            return JsonConvert.DeserializeObject<T>(json, VfxCompositionDraftService.JsonSettings);
        }

        private static string ResolveResponsibility(VfxSourceComponentView source)
        {
            string role = source?.Roles?.FirstOrDefault();
            return string.IsNullOrWhiteSpace(role) ? "Detail" : char.ToUpperInvariant(role[0]) + role.Substring(1);
        }

        private static string ResolveDefaultRetime(VfxComponentProfileView profile)
        {
            if (profile.Timing.CycleFrames > 0) return nameof(VfxRetimeMode.RepeatBody);
            if (profile.Timing.RetimeMechanism == "FixedEvent") return nameof(VfxRetimeMode.Fixed);
            return nameof(VfxRetimeMode.ExtendBody);
        }

        private static Dictionary<string, string> BuildParticipation(VfxComponentProfileView profile, VfxShaderProfileView shaderProfile)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < profile.Bindings.Count; i++)
                result[profile.Bindings[i].PropertyId] = profile.Bindings[i].ReadOnly
                    ? nameof(VfxMemberParticipationState.Unsupported)
                    : nameof(VfxMemberParticipationState.Follow);
            if (shaderProfile?.Bindings != null)
            {
                for (int i = 0; i < shaderProfile.Bindings.Count; i++)
                {
                    VfxShaderPropertyBindingView binding = shaderProfile.Bindings[i];
                    result[binding.SemanticPropertyId] = binding.DynamicPropertyBlock || binding.RequiresDerivedMaterial
                        ? nameof(VfxMemberParticipationState.Follow)
                        : nameof(VfxMemberParticipationState.Unsupported);
                }
            }
            if (shaderProfile?.CustomDataBindings != null)
            {
                for (int i = 0; i < shaderProfile.CustomDataBindings.Count; i++)
                {
                    VfxShaderCustomDataBindingView binding = shaderProfile.CustomDataBindings[i];
                    if (string.IsNullOrWhiteSpace(binding.SemanticPropertyId)) continue;
                    result[binding.SemanticPropertyId] = binding.Editable
                        ? nameof(VfxMemberParticipationState.Follow)
                        : nameof(VfxMemberParticipationState.Unsupported);
                }
            }
            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    [Serializable]
    internal sealed class VfxStableResourceAuditReport
    {
        [JsonProperty("generatedUtc")] public string GeneratedUtc = string.Empty;
        [JsonProperty("drafts")] public List<VfxStableResourceAuditDraft> Drafts = new List<VfxStableResourceAuditDraft>();
        [JsonProperty("bindings")] public List<VfxStableResourceAuditBinding> Bindings = new List<VfxStableResourceAuditBinding>();
        [JsonProperty("legacyPrefabPaths")] public List<string> LegacyPrefabPaths = new List<string>();
        [JsonProperty("observedPrefabPaths")] public List<string> ObservedPrefabPaths = new List<string>();
        [JsonProperty("orphanPrefabPaths")] public List<string> OrphanPrefabPaths = new List<string>();
        [JsonProperty("diagnostics")] public List<string> Diagnostics = new List<string>();
    }

    [Serializable]
    internal sealed class VfxStableResourceAuditDraft
    {
        [JsonProperty("draftPath")] public string DraftPath = string.Empty;
        [JsonProperty("draftId")] public string DraftId = string.Empty;
        [JsonProperty("resourceKey")] public string ResourceKey = string.Empty;
        [JsonProperty("stablePrefabPath")] public string StablePrefabPath = string.Empty;
        [JsonProperty("identityPersisted")] public bool IdentityPersisted;
    }

    [Serializable]
    internal sealed class VfxStableResourceAuditBinding
    {
        [JsonProperty("resourceKey")] public string ResourceKey = string.Empty;
        [JsonProperty("prefabPath")] public string PrefabPath = string.Empty;
        [JsonProperty("prefabGuid")] public string PrefabGuid = string.Empty;
        [JsonProperty("contentHash")] public string ContentHash = string.Empty;
        [JsonProperty("status")] public string Status = string.Empty;
        [JsonProperty("consumerCount")] public int ConsumerCount;
        [JsonProperty("consumerFiles")] public List<string> ConsumerFiles = new List<string>();
    }

    /// <summary>AllocByDesign, read-only inventory for stable VFX identity and usages.</summary>
    internal static class VfxStableResourceAuditService
    {
        internal const string OutputRoot = "Temp/MxFramework/VfxStableResourceAudit";

        [MenuItem("MxFramework/VFX/Stable Resources/Generate Read-only Audit", priority = 145)]
        internal static void GenerateMenu()
        {
            VfxStableResourceAuditReport report = Build();
            Write(report);
            Debug.Log("VFX 稳定资源审计已生成：" + OutputRoot);
        }

        [MenuItem("MxFramework/VFX/Stable Resources/Migrate Draft Identities", priority = 146)]
        internal static void MigrateDraftIdentitiesMenu()
        {
            var service = new VfxCompositionDraftService();
            int changed = 0;
            foreach (string path in service.ListDrafts())
            {
                JObject raw = JObject.Parse(File.ReadAllText(path));
                if (raw["resourceIdentity"] != null)
                    continue;
                string hash = VfxCompositionDraftService.HashFile(path);
                VfxCompositionDraftView draft = service.Load(path, out string loadMessage);
                if (draft == null)
                {
                    Debug.LogError(path + ": " + loadMessage);
                    continue;
                }
                if (!service.SaveGuarded(draft, path, hash, out _, out _, out string saveMessage))
                {
                    Debug.LogError(path + ": " + saveMessage);
                    continue;
                }
                changed++;
            }
            GenerateMenu();
            Debug.Log("VFX 草稿稳定身份迁移完成：" + changed + " 个草稿。旧 Prefab 未删除。" );
        }

        internal static VfxStableResourceAuditReport Build()
        {
            var report = new VfxStableResourceAuditReport
            {
                GeneratedUtc = string.Empty
            };
            var service = new VfxCompositionDraftService();
            foreach (string path in service.ListDrafts())
            {
                JObject raw = JObject.Parse(File.ReadAllText(path));
                VfxCompositionDraftView draft = service.Load(path, out string message);
                if (draft == null)
                {
                    report.Diagnostics.Add(path + ": " + message);
                    continue;
                }
                report.Drafts.Add(new VfxStableResourceAuditDraft
                {
                    DraftPath = path,
                    DraftId = draft.DraftId,
                    ResourceKey = draft.ResourceIdentity.ResourceKey.ToResourceKey().ToString(),
                    StablePrefabPath = draft.ResourceIdentity.StablePrefabPath,
                    IdentityPersisted = raw["resourceIdentity"] != null
                });
            }

            VfxPublishedBindingManifest manifest = VfxPublishedBindingEditorService.LoadManifestUnchecked();
            var boundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (VfxPublishedBindingRecord binding in manifest?.Bindings
                         ?? Enumerable.Empty<VfxPublishedBindingRecord>())
            {
                string serializedKey = binding.ResourceKey.ToResourceKey().ToString();
                var entry = new VfxStableResourceAuditBinding
                {
                    ResourceKey = serializedKey,
                    PrefabPath = binding.AssetPath,
                    PrefabGuid = AssetDatabase.AssetPathToGUID(binding.AssetPath),
                    ContentHash = File.Exists(binding.AssetPath)
                        ? VfxCompositionDraftService.HashFile(binding.AssetPath)
                        : string.Empty,
                    Status = binding.VisualMaturity + "/" + binding.ReviewStatus
                };
                entry.ConsumerFiles.AddRange(FindConsumers(serializedKey, binding.ResourceKey.Id));
                entry.ConsumerCount = entry.ConsumerFiles.Count;
                report.Bindings.Add(entry);
                if (!string.IsNullOrWhiteSpace(binding.AssetPath)) boundPaths.Add(binding.AssetPath);
            }

            foreach (string root in new[]
                     {
                         VfxCompositionCandidateBaker.CandidateRoot,
                         "Assets/VFX/MxFramework/Review/Authoring"
                     })
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                    report.LegacyPrefabPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            foreach (string root in new[]
                     {
                         VfxStableResourceIdentityService.StablePrefabRoot,
                         "Assets/VFX/MxFramework/Production",
                         "Assets/VFX/MxFramework/Samples"
                     })
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                    report.ObservedPrefabPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            report.LegacyPrefabPaths = report.LegacyPrefabPaths.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Ordinal).ToList();
            report.ObservedPrefabPaths = report.ObservedPrefabPaths.Concat(report.LegacyPrefabPaths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Ordinal).ToList();
            report.OrphanPrefabPaths = report.LegacyPrefabPaths.Where(value => !boundPaths.Contains(value)).ToList();
            if (File.Exists(VfxPublishedBindingEditorService.PlaytestOverridePath))
                report.Diagnostics.Add("发现旧本地 Playtest Override；当前稳定流程不会再读取它。" );
            return report;
        }

        private static IEnumerable<string> FindConsumers(string serializedKey, string id)
        {
            var result = new List<string>();
            foreach (string root in new[] { "Assets/Config", "Assets/MxFrameworkGenerated", "Tools/MxFramework.Authoring/samples" })
            {
                if (!Directory.Exists(root)) continue;
                foreach (string path in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
                {
                    string normalized = path.Replace('\\', '/');
                    if (string.Equals(normalized, VfxPublishedBindingEditorService.ManifestPath, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(normalized, VfxStablePrefabPublicationService.AuthoringCatalogPath, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(normalized, SampleResourceCatalogBuilder.CatalogPath, StringComparison.OrdinalIgnoreCase)
                        || normalized.StartsWith(VfxCompositionDraftService.DraftRoot + "/", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string contents;
                    try { contents = File.ReadAllText(path); }
                    catch (IOException) { continue; }
                    if (contents.IndexOf(serializedKey, StringComparison.Ordinal) >= 0
                        || contents.IndexOf(id, StringComparison.Ordinal) >= 0)
                        result.Add(normalized);
                }
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static void Write(VfxStableResourceAuditReport report)
        {
            Directory.CreateDirectory(OutputRoot);
            File.WriteAllText(
                OutputRoot + "/report.json",
                JsonConvert.SerializeObject(report, Formatting.Indented) + Environment.NewLine,
                new UTF8Encoding(false));
            var markdown = new StringBuilder();
            markdown.AppendLine("# VFX Stable Resource Audit");
            markdown.AppendLine();
            markdown.AppendLine("- Drafts: " + report.Drafts.Count);
            markdown.AppendLine("- Bindings: " + report.Bindings.Count);
            markdown.AppendLine("- Legacy Prefabs: " + report.LegacyPrefabPaths.Count);
            markdown.AppendLine("- Observed Prefabs: " + report.ObservedPrefabPaths.Count);
            markdown.AppendLine("- Orphan Prefabs: " + report.OrphanPrefabPaths.Count);
            markdown.AppendLine();
            markdown.AppendLine("## Bindings");
            foreach (VfxStableResourceAuditBinding binding in report.Bindings)
                markdown.AppendLine("- `" + binding.ResourceKey + "` -> `" + binding.PrefabPath + "` (usages: " + binding.ConsumerFiles.Count + ")");
            markdown.AppendLine();
            markdown.AppendLine("## Diagnostics");
            foreach (string diagnostic in report.Diagnostics)
                markdown.AppendLine("- " + diagnostic);
            File.WriteAllText(OutputRoot + "/report.md", markdown.ToString(), new UTF8Encoding(false));
        }
    }
}

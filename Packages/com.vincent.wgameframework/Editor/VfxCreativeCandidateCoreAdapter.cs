using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MxFramework.Editor
{
    /// <summary>Adapts actual Unity candidate artifacts into the authoritative Core manifest and receipt schemas.</summary>
    /// <remarks>Editor-only and AllocByDesign. Core JSON field names and canonical hashes remain authoritative.</remarks>
    internal static class VfxCoreCandidateReceiptAdapter
    {
        public static VfxCoreReceiptExportResult Export(
            string coreGenerationPlanPath,
            IReadOnlyList<VfxCandidateGenerationResult> generationResults)
        {
            string manifestPath = string.Empty;
            string receiptPath = string.Empty;
            bool ownsFixedEvidence = false;
            try
            {
                if (!VfxCandidateSandbox.IsControlledEvidencePath(coreGenerationPlanPath))
                    throw new InvalidOperationException("Core generation plan must be under the controlled Authoring evidence root: " + coreGenerationPlanPath);
                if (generationResults == null || generationResults.Count == 0)
                    throw new InvalidOperationException("Core receipt export requires generated Unity candidate artifacts.");

                JObject planDocument = JObject.Parse(File.ReadAllText(coreGenerationPlanPath));
                VfxCoreGenerationPlan plan = planDocument.ToObject<VfxCoreGenerationPlan>();
                ValidatePlan(plan, planDocument);
                if (!VfxCandidateSandbox.TryGetSessionRoot(plan.SessionId, out string sessionRoot, out string sessionError))
                    throw new InvalidOperationException(sessionError);
                if (!string.Equals(VfxCandidateSandbox.NormalizeAssetPath(plan.SandboxRoot), sessionRoot, StringComparison.Ordinal))
                    throw new InvalidOperationException("Core generation plan sandboxRoot must equal its physical session root: " + sessionRoot);
                for (int i = 0; i < plan.Candidates.Count; i++)
                {
                    VfxCoreGenerationCandidateSpec candidate = plan.Candidates[i];
                    if (candidate == null || string.IsNullOrWhiteSpace(candidate.CandidateId) || !candidate.DraftOnly)
                        throw new InvalidOperationException("Every Core generation candidate must have a stable id and remain DraftOnly.");
                    if (!IsAtOrBelow(candidate.OutputRoot + "/placeholder.asset", sessionRoot))
                        throw new InvalidOperationException("Core candidate outputRoot escaped its shared session: " + candidate.OutputRoot);
                }

                JObject templateCatalog = JObject.Parse(File.ReadAllText(VfxCandidateProtocol.GenerationTemplateCatalogPath));
                var assets = new List<VfxCoreCandidateAssetRecord>();
                var coveredCandidates = new HashSet<string>(StringComparer.Ordinal);
                for (int resultIndex = 0; resultIndex < generationResults.Count; resultIndex++)
                {
                    VfxCandidateGenerationResult result = generationResults[resultIndex];
                    if (result == null || !result.Success || result.Receipt == null)
                        throw new InvalidOperationException("Core receipt export cannot consume a failed or missing Editor generation result.");
                    if (!VfxCandidateSandbox.IsWithinSessionRoot(result.SessionRoot + "/placeholder.asset", sessionRoot))
                        throw new InvalidOperationException("Generated candidate session escaped the Core generation session: " + result.SessionRoot);
                    for (int assetIndex = 0; assetIndex < result.Receipt.Assets.Count; assetIndex++)
                    {
                        VfxCandidateAssetReceipt localAsset = result.Receipt.Assets[assetIndex];
                        VfxCoreGenerationCandidateSpec candidate = FindCandidateForAsset(plan.Candidates, localAsset.AssetPath);
                        if (candidate == null)
                            throw new InvalidOperationException("Generated Unity asset is not covered by any Core candidate outputRoot: " + localAsset.AssetPath);
                        coveredCandidates.Add(candidate.CandidateId);
                        ValidateActualArtifact(localAsset);
                        VfxCoreAssetProvenance provenance = BuildProvenance(candidate, result.Receipt, templateCatalog);
                        assets.Add(new VfxCoreCandidateAssetRecord
                        {
                            CandidateId = candidate.CandidateId,
                            AssetKind = NormalizeAssetKind(localAsset.Kind),
                            AssetPath = VfxCandidateSandbox.NormalizeAssetPath(localAsset.AssetPath),
                            ContentHash = localAsset.ContentHash,
                            UnityGuid = localAsset.Guid,
                            DependencyHash = localAsset.DependencyHash,
                            Provenance = provenance
                        });
                    }
                }
                for (int i = 0; i < plan.Candidates.Count; i++)
                {
                    VfxCoreGenerationCandidateSpec candidate = plan.Candidates[i];
                    if (candidate == null || !coveredCandidates.Contains(candidate.CandidateId))
                        throw new InvalidOperationException("Core generation candidate has no actual Unity artifact evidence: " + candidate?.CandidateId);
                }
                assets.Sort((left, right) =>
                {
                    int candidateCompare = string.Compare(left.CandidateId, right.CandidateId, StringComparison.Ordinal);
                    return candidateCompare != 0 ? candidateCompare : string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
                });

                var manifest = new VfxCoreCandidateManifest
                {
                    SessionId = plan.SessionId,
                    GenerationPlanHash = plan.PlanHash,
                    CatalogHash = plan.CatalogHash,
                    GenerationTemplateCatalogHash = plan.GenerationTemplateCatalogHash,
                    ReviewStatus = "Draft",
                    Assets = CloneAssets(assets)
                };
                manifest.ManifestHash = VfxCandidateSandbox.ComputeCoreDocumentHash(manifest, "manifestHash");
                var receipt = new VfxCoreCandidateReceipt
                {
                    SessionId = plan.SessionId,
                    GenerationPlanHash = plan.PlanHash,
                    CatalogHash = plan.CatalogHash,
                    GenerationTemplateCatalogHash = plan.GenerationTemplateCatalogHash,
                    Assets = CloneAssets(assets)
                };
                receipt.ReceiptHash = VfxCandidateSandbox.ComputeCoreDocumentHash(receipt, "receiptHash");

                manifestPath = sessionRoot + "/candidate_manifest.json";
                receiptPath = sessionRoot + "/candidate_receipt.json";
                if (VfxCandidateSandbox.FixedDraftEvidenceAssetExists(manifestPath) ||
                    VfxCandidateSandbox.FixedDraftEvidenceAssetExists(receiptPath))
                {
                    throw new InvalidOperationException(
                        "Fixed Core candidate evidence already exists for this session and is immutable. Create a new session instead: " + sessionRoot);
                }
                ownsFixedEvidence = true;
                VfxCandidateSandbox.WriteJsonAsset(manifestPath, manifest);
                VfxCandidateSandbox.WriteJsonAsset(receiptPath, receipt);
                return new VfxCoreReceiptExportResult
                {
                    Success = true,
                    Message = "Exact Core candidate manifest/receipt exported from Unity artifacts. Review status remains Draft.",
                    ManifestAssetPath = manifestPath,
                    ReceiptAssetPath = receiptPath,
                    Manifest = manifest,
                    Receipt = receipt
                };
            }
            catch (Exception exception)
            {
                string cleanupMessage = string.Empty;
                if (ownsFixedEvidence)
                {
                    var failures = new List<string>();
                    if (!VfxCandidateSandbox.TryDeleteFixedDraftEvidenceAsset(receiptPath, out _, out string receiptError))
                        failures.Add(receiptError);
                    if (!VfxCandidateSandbox.TryDeleteFixedDraftEvidenceAsset(manifestPath, out _, out string manifestError))
                        failures.Add(manifestError);
                    if (failures.Count > 0)
                        cleanupMessage = " Cleanup was incomplete; the session remains blocked and requires recovery: " + string.Join(" | ", failures);
                }
                return new VfxCoreReceiptExportResult { Success = false, Message = exception.Message + cleanupMessage };
            }
        }

        internal static void ValidatePlan(VfxCoreGenerationPlan plan, JObject document)
        {
            if (plan == null || !string.Equals(plan.SchemaVersion, VfxCandidateProtocol.CoreGenerationPlanSchemaVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("Unsupported Core generation plan schema.");
            string actualHash = VfxCandidateSandbox.ComputeCoreDocumentHash(document, "planHash", "diagnostics", "blocked");
            if (!VfxCandidateSandbox.IsSha256(plan.PlanHash) || !string.Equals(plan.PlanHash, actualHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Core generation plan hash is invalid or stale.");
            if (plan.Blocked || VfxCandidateSandbox.HasErrorDiagnostics(plan.Diagnostics))
                throw new InvalidOperationException("Blocked Core generation plans cannot produce authoritative receipts.");
            if (plan.Candidates == null || plan.Candidates.Count == 0)
                throw new InvalidOperationException("Core generation plan has no candidates.");
        }

        private static VfxCoreGenerationCandidateSpec FindCandidateForAsset(List<VfxCoreGenerationCandidateSpec> candidates, string assetPath)
        {
            VfxCoreGenerationCandidateSpec match = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                VfxCoreGenerationCandidateSpec candidate = candidates[i];
                if (candidate == null || !IsAtOrBelow(assetPath, candidate.OutputRoot))
                    continue;
                if (match != null)
                    throw new InvalidOperationException("Generated Unity asset matches multiple Core candidate output roots: " + assetPath);
                match = candidate;
            }
            return match;
        }

        private static VfxCoreAssetProvenance BuildProvenance(
            VfxCoreGenerationCandidateSpec candidate,
            VfxCandidateReceipt localReceipt,
            JObject templateCatalog)
        {
            JObject provider = FindProvider(templateCatalog, candidate.ProviderId);
            if (provider == null
                || !string.Equals(provider.Value<string>("providerVersion"), candidate.ProviderVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("Core candidate provider is absent or version-mismatched in the generation template Catalog: " + candidate.ProviderId);
            string configuredModelId = provider.Value<string>("modelId") ?? string.Empty;
            string configuredModelVersion = provider.Value<string>("modelVersion") ?? string.Empty;
            if (!string.Equals(configuredModelId, candidate.ModelId, StringComparison.Ordinal)
                || !string.Equals(configuredModelVersion, candidate.ModelVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("Core candidate model binding differs from its provider Catalog contract: " + candidate.CandidateId);
            string licenseId = FindLocalProvenance(localReceipt, "licenseId");
            JArray allowedLicenses = provider["allowedLicenseIds"] as JArray ?? new JArray();
            bool allowed = false;
            for (int i = 0; i < allowedLicenses.Count; i++)
                if (string.Equals(allowedLicenses[i]?.Value<string>(), licenseId, StringComparison.Ordinal)) allowed = true;
            if (!allowed)
                throw new InvalidOperationException("Generated candidate license is absent or not allowed by its Core provider: " + licenseId);
            string sourceUri = FindLocalProvenance(localReceipt, "sourceUri");
            if (string.IsNullOrWhiteSpace(sourceUri))
                sourceUri = "mx://vfx/candidate-plan/" + localReceipt.PlanId;
            if (!VfxCandidateSandbox.IsSha256(localReceipt.PlanHash))
                throw new InvalidOperationException("Editor candidate plan hash cannot bind Core source provenance.");
            string providerKind = provider.Value<string>("kind") ?? string.Empty;
            bool externalProvider = string.Equals(providerKind, "LocalCli", StringComparison.Ordinal)
                || string.Equals(providerKind, "LocalHttp", StringComparison.Ordinal)
                || string.Equals(providerKind, "Cloud", StringComparison.Ordinal);
            string sourceContentHash = FindLocalProvenance(localReceipt, "sourceContentHash");
            if (externalProvider && (!VfxCandidateSandbox.IsSha256(sourceContentHash) || string.IsNullOrWhiteSpace(FindLocalProvenance(localReceipt, "sourceUri"))))
                throw new InvalidOperationException("External provider provenance requires its response sourceUri and sourceContentHash; the Editor plan hash cannot substitute for provider evidence.");
            if (!externalProvider && !VfxCandidateSandbox.IsSha256(sourceContentHash))
                sourceContentHash = localReceipt.PlanHash;
            return new VfxCoreAssetProvenance
            {
                ProviderId = candidate.ProviderId,
                ProviderVersion = candidate.ProviderVersion,
                TemplateId = candidate.TemplateId,
                TemplateVersion = candidate.TemplateVersion,
                ModelId = configuredModelId,
                ModelVersion = configuredModelVersion,
                LicenseId = licenseId,
                SourceUri = sourceUri,
                SourceContentHash = sourceContentHash
            };
        }

        private static void ValidateActualArtifact(VfxCandidateAssetReceipt asset)
        {
            string path = VfxCandidateSandbox.NormalizeAssetPath(asset?.AssetPath);
            if (asset == null || !VfxCandidateSandbox.IsCandidateRoot(path) || AssetDatabase.LoadMainAssetAtPath(path) == null || !File.Exists(path))
                throw new InvalidOperationException("Core receipt adapter requires an imported Unity candidate artifact: " + path);
            string actualHash = VfxCandidateSandbox.ComputeAssetFileHash(path);
            if (!VfxCandidateSandbox.IsSha256(asset.ContentHash) || !string.Equals(actualHash, asset.ContentHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Generated Unity candidate artifact content hash is stale: " + path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrWhiteSpace(asset.Guid) || !string.Equals(guid, asset.Guid, StringComparison.Ordinal))
                throw new InvalidOperationException("Generated Unity candidate artifact GUID evidence is stale: " + path);
            string dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString();
            if (string.IsNullOrWhiteSpace(asset.DependencyHash) || !string.Equals(dependencyHash, asset.DependencyHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Generated Unity candidate artifact dependency evidence is stale: " + path);
        }

        private static JObject FindProvider(JObject catalog, string providerId)
        {
            JArray providers = catalog?["providers"] as JArray ?? new JArray();
            for (int i = 0; i < providers.Count; i++)
            {
                JObject provider = providers[i] as JObject;
                if (string.Equals(provider?.Value<string>("providerId"), providerId, StringComparison.Ordinal))
                    return provider;
            }
            return null;
        }

        private static string FindLocalProvenance(VfxCandidateReceipt receipt, string key)
        {
            for (int i = 0; i < receipt.Provenance.Count; i++)
                if (string.Equals(receipt.Provenance[i]?.Key, key, StringComparison.Ordinal)) return receipt.Provenance[i].Value ?? string.Empty;
            return string.Empty;
        }

        private static string NormalizeAssetKind(string kind)
        {
            switch (kind)
            {
                case "Texture": return "Texture";
                case "Material": return "Material";
                case "Prefab": return "Prefab";
                case "VfxGraph": return "VfxGraph";
                default: throw new InvalidOperationException("Unsupported Core candidate asset kind: " + kind);
            }
        }

        private static List<VfxCoreCandidateAssetRecord> CloneAssets(List<VfxCoreCandidateAssetRecord> source)
        {
            var result = new List<VfxCoreCandidateAssetRecord>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                VfxCoreCandidateAssetRecord asset = source[i];
                result.Add(new VfxCoreCandidateAssetRecord
                {
                    CandidateId = asset.CandidateId,
                    AssetKind = asset.AssetKind,
                    AssetPath = asset.AssetPath,
                    ContentHash = asset.ContentHash,
                    UnityGuid = asset.UnityGuid,
                    DependencyHash = asset.DependencyHash,
                    Provenance = new VfxCoreAssetProvenance
                    {
                        ProviderId = asset.Provenance.ProviderId,
                        ProviderVersion = asset.Provenance.ProviderVersion,
                        TemplateId = asset.Provenance.TemplateId,
                        TemplateVersion = asset.Provenance.TemplateVersion,
                        ModelId = asset.Provenance.ModelId,
                        ModelVersion = asset.Provenance.ModelVersion,
                        LicenseId = asset.Provenance.LicenseId,
                        SourceUri = asset.Provenance.SourceUri,
                        SourceContentHash = asset.Provenance.SourceContentHash
                    }
                });
            }
            return result;
        }

        private static bool IsAtOrBelow(string assetPath, string root)
        {
            string path = VfxCandidateSandbox.NormalizeAssetPath(assetPath);
            string normalizedRoot = VfxCandidateSandbox.NormalizeAssetPath(root).TrimEnd('/');
            return VfxCandidateSandbox.IsCandidateRoot(normalizedRoot)
                && (string.Equals(path, normalizedRoot, StringComparison.Ordinal) || path.StartsWith(normalizedRoot + "/", StringComparison.Ordinal));
        }
    }
}

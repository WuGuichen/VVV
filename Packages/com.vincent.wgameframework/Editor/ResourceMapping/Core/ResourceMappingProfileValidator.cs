using System;
using System.Collections.Generic;

namespace MxFramework.ResourceMapping.Authoring
{
    public enum ResourceMappingDiagnosticSeverity
    {
        Warning,
        Error
    }

    public sealed class ResourceMappingDiagnostic
    {
        public ResourceMappingDiagnosticSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    public sealed class ResourceMappingValidationReport
    {
        public List<ResourceMappingDiagnostic> Diagnostics { get; } = new List<ResourceMappingDiagnostic>();
        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Diagnostics.Count; i++)
                {
                    if (Diagnostics[i].Severity == ResourceMappingDiagnosticSeverity.Error)
                        return true;
                }

                return false;
            }
        }
    }

    public static class ResourceMappingCanonicalTypeIds
    {
        private static readonly Dictionary<string, string> CanonicalByInput = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "GameObject", "GameObject" }, { "model", "GameObject" }, { "prefab", "GameObject" }, { "vfx", "GameObject" },
            { "Texture2D", "Texture2D" }, { "texture", "Texture2D" }, { "Sprite", "Sprite" },
            { "AudioClip", "AudioClip" }, { "audio", "AudioClip" },
            { "AnimationClip", "AnimationClip" }, { "animation", "AnimationClip" },
            { "AvatarMask", "AvatarMask" }, { "Scene", "Scene" }, { "SceneAsset", "Scene" },
            { "TextAsset", "TextAsset" }, { "text", "TextAsset" }, { "Material", "Material" },
            { "PanelSettings", "PanelSettings" }, { "VisualTreeAsset", "VisualTreeAsset" },
            { "StyleSheet", "StyleSheet" }, { "Font", "Font" }, { "String", "String" }, { "Object", "Object" }
        };

        public static bool TryCanonicalize(string value, out string canonical) =>
            CanonicalByInput.TryGetValue((value ?? string.Empty).Trim(), out canonical);
    }

    public static class ResourceMappingProfileValidator
    {

        /// <remarks>AllocByDesign. Validation is a low-frequency authoring operation.</remarks>
        public static ResourceMappingValidationReport Validate(ResourceMappingProfileV2 profile)
        {
            var report = new ResourceMappingValidationReport();
            if (profile == null)
            {
                Error(report, "ProfileMissing", "Profile is required.", "$" );
                return report;
            }

            RequireExact(report, profile.Schema, ResourceMappingSchemas.ProfileV2, "SchemaUnsupported", "schema");
            RequireValue(report, profile.ProfileId, "ProfileIdMissing", "profileId");
            RequireValue(report, profile.CatalogId, "CatalogIdMissing", "catalogId");

            var bundleIds = new HashSet<string>(StringComparer.Ordinal);
            var mountIds = new HashSet<string>(StringComparer.Ordinal);
            var catalogIds = new HashSet<string>(StringComparer.Ordinal);
            var mountOrders = new HashSet<int>();
            var preloadIds = new HashSet<string>(StringComparer.Ordinal);
            var ownerKeys = new HashSet<string>(StringComparer.Ordinal);

            ValidateBundleRules(profile.BundleRules, bundleIds, ownerKeys, report);
            ValidateCatalogMounts(profile.CatalogMounts, mountIds, catalogIds, mountOrders, ownerKeys, report);
            ValidatePreloadGroups(profile.PreloadGroups, preloadIds, ownerKeys, report);
            ValidateEntries(profile.Entries, bundleIds, mountIds, ownerKeys, report);
            return report;
        }

        private static void ValidateBundleRules(
            IList<ResourceMappingBundleRule> rules,
            HashSet<string> ids,
            HashSet<string> ownerKeys,
            ResourceMappingValidationReport report)
        {
            if (rules == null)
            {
                Error(report, "BundleRulesMissing", "bundleRules must be an array.", "bundleRules");
                return;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                ResourceMappingBundleRule rule = rules[i];
                string path = "bundleRules[" + i + "]";
                if (rule == null)
                {
                    Error(report, "BundleRuleNull", "Bundle rule cannot be null.", path);
                    continue;
                }

                ValidateOwner(rule.ProducerReceipt, rule.AuthoringRecordId, path, ownerKeys, report);
                RequireValue(report, rule.Id, "BundleRuleIdMissing", path + ".id");
                RequireValue(report, rule.BundleName, "BundleNameMissing", path + ".bundleName");
                RequireValue(report, rule.BuildTarget, "BundleBuildTargetMissing", path + ".buildTarget");
                if (!IsSupportedCompression(rule.Compression))
                    Error(report, "BundleCompressionUnsupported", "Bundle compression must be lz4, lzma or uncompressed.", path + ".compression");
                if (!string.IsNullOrEmpty(rule.Id) && !ids.Add(rule.Id))
                    Error(report, "BundleRuleDuplicate", "Duplicate Bundle rule id: " + rule.Id + ".", path + ".id");
            }
        }

        private static void ValidateCatalogMounts(
            IList<ResourceMappingCatalogMount> mounts,
            HashSet<string> mountIds,
            HashSet<string> catalogIds,
            HashSet<int> mountOrders,
            HashSet<string> ownerKeys,
            ResourceMappingValidationReport report)
        {
            if (mounts == null)
            {
                Error(report, "CatalogMountsMissing", "catalogMounts must be an array.", "catalogMounts");
                return;
            }

            for (int i = 0; i < mounts.Count; i++)
            {
                ResourceMappingCatalogMount mount = mounts[i];
                string path = "catalogMounts[" + i + "]";
                if (mount == null)
                {
                    Error(report, "CatalogMountNull", "Catalog mount cannot be null.", path);
                    continue;
                }

                ValidateOwner(mount.ProducerReceipt, mount.AuthoringRecordId, path, ownerKeys, report);
                RequireValue(report, mount.CatalogMountId, "CatalogMountIdMissing", path + ".catalogMountId");
                RequireValue(report, mount.CatalogId, "MountedCatalogIdMissing", path + ".catalogId");
                RequireValue(report, mount.ExpectedCatalogFingerprint, "CatalogFingerprintMissing", path + ".expectedCatalogFingerprint");
                if (!string.IsNullOrWhiteSpace(mount.ExpectedCatalogFingerprint) && !IsSha256Fingerprint(mount.ExpectedCatalogFingerprint))
                    Error(report, "CatalogFingerprintInvalid", "Catalog fingerprint must use sha256:<64 lowercase or uppercase hex characters>.", path + ".expectedCatalogFingerprint");
                if (!string.IsNullOrEmpty(mount.CatalogMountId) && !mountIds.Add(mount.CatalogMountId))
                    Error(report, "CatalogMountIdDuplicate", "Duplicate Catalog mount id: " + mount.CatalogMountId + ".", path + ".catalogMountId");
                if (!string.IsNullOrEmpty(mount.CatalogId) && !catalogIds.Add(mount.CatalogId))
                    Error(report, "MountedCatalogIdDuplicate", "Duplicate mounted Catalog id: " + mount.CatalogId + ".", path + ".catalogId");
                if (mount.MountOrder < 0 || !mountOrders.Add(mount.MountOrder))
                    Error(report, "CatalogMountOrderInvalid", "Catalog mount order must be unique and non-negative.", path + ".mountOrder");

                string relativePath = mount.RelativeCatalogPath ?? string.Empty;
                if (mount.BootstrapKind == ResourceMappingCatalogBootstrapKind.StreamingAssetsCatalog)
                {
                    if (!IsSafeRelativePath(relativePath))
                        Error(report, "CatalogMountPathInvalid", "StreamingAssets Catalog path must be normalized, relative and non-escaping.", path + ".relativeCatalogPath");
                }
                else if (relativePath.Length > 0)
                {
                    Error(report, "CatalogMountPathForbidden", "compositionRoot mount cannot declare relativeCatalogPath.", path + ".relativeCatalogPath");
                }
            }
        }

        private static void ValidatePreloadGroups(
            IList<ResourceMappingPreloadGroup> groups,
            HashSet<string> ids,
            HashSet<string> ownerKeys,
            ResourceMappingValidationReport report)
        {
            if (groups == null)
            {
                Error(report, "PreloadGroupsMissing", "preloadGroups must be an array.", "preloadGroups");
                return;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                ResourceMappingPreloadGroup group = groups[i];
                string path = "preloadGroups[" + i + "]";
                if (group == null)
                {
                    Error(report, "PreloadGroupNull", "Preload group cannot be null.", path);
                    continue;
                }

                ValidateOwner(group.ProducerReceipt, group.AuthoringRecordId, path, ownerKeys, report);
                RequireValue(report, group.Id, "PreloadGroupIdMissing", path + ".id");
                if (!string.IsNullOrEmpty(group.Id) && !ids.Add(group.Id))
                    Error(report, "PreloadGroupDuplicate", "Duplicate preload group id: " + group.Id + ".", path + ".id");
                if (group.MaxConcurrentLoads <= 0)
                    Error(report, "PreloadConcurrencyInvalid", "maxConcurrentLoads must be greater than zero.", path + ".maxConcurrentLoads");
            }
        }

        private static void ValidateEntries(
            IList<ResourceMappingEntry> entries,
            HashSet<string> bundleIds,
            HashSet<string> mountIds,
            HashSet<string> ownerKeys,
            ResourceMappingValidationReport report)
        {
            if (entries == null)
            {
                Error(report, "EntriesMissing", "entries must be an array.", "entries");
                return;
            }

            var resourceKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                ResourceMappingEntry entry = entries[i];
                string path = "entries[" + i + "]";
                if (entry == null)
                {
                    Error(report, "EntryNull", "Entry cannot be null.", path);
                    continue;
                }

                ValidateOwner(entry.ProducerReceipt, entry.AuthoringRecordId, path, ownerKeys, report);
                bool runtime = entry.DeliveryIntent == ResourceMappingDeliveryIntent.Internal ||
                               entry.DeliveryIntent == ResourceMappingDeliveryIntent.External;
                if (runtime)
                {
                    ValidateResourceKey(entry.ResourceKey, path + ".resourceKey", report);
                    if (entry.ResourceKey != null && !resourceKeys.Add(entry.ResourceKey.ToIdentityString()))
                        Error(report, "ResourceKeyDuplicate", "Duplicate complete ResourceKey.", path + ".resourceKey");
                    if (!entry.OverrideIntent.HasValue)
                        Error(report, "OverrideIntentMissing", "Runtime entry requires overrideIntent.", path + ".overrideIntent");
                }
                else
                {
                    if (entry.ResourceKey != null)
                        Error(report, "ResourceKeyForbidden", "Non-runtime entry cannot reserve a ResourceKey.", path + ".resourceKey");
                    if (entry.OverrideIntent.HasValue)
                        Error(report, "OverrideIntentForbidden", "Non-runtime entry cannot declare overrideIntent.", path + ".overrideIntent");
                }

                if (entry.DeliveryIntent == ResourceMappingDeliveryIntent.Internal)
                {
                    if (entry.SourceLocator == null)
                        Error(report, "SourceLocatorMissing", "Internal entry requires sourceLocator.", path + ".sourceLocator");
                    else
                        ValidateSourceLocator(entry.SourceLocator, path + ".sourceLocator", report);
                    if (entry.ExternalCatalogBinding != null)
                        Error(report, "ExternalBindingForbidden", "Internal entry cannot declare externalCatalogBinding.", path + ".externalCatalogBinding");
                    string ruleId = entry.BundleIntent != null ? entry.BundleIntent.RuleId : string.Empty;
                    if (string.IsNullOrWhiteSpace(ruleId) || !bundleIds.Contains(ruleId))
                        Error(report, "BundleIntentInvalid", "Internal entry requires one existing Bundle rule id.", path + ".bundleIntent.ruleId");
                }
                else if (entry.DeliveryIntent == ResourceMappingDeliveryIntent.External)
                {
                    if (entry.BundleIntent != null)
                        Error(report, "BundleIntentForbidden", "External entry cannot declare bundleIntent.", path + ".bundleIntent");
                    string mountId = entry.ExternalCatalogBinding != null ? entry.ExternalCatalogBinding.CatalogMountId : string.Empty;
                    if (string.IsNullOrWhiteSpace(mountId) || !mountIds.Contains(mountId))
                        Error(report, "ExternalBindingInvalid", "External entry requires one existing Catalog mount id.", path + ".externalCatalogBinding.catalogMountId");
                }
                else
                {
                    if (entry.ExternalCatalogBinding != null)
                        Error(report, "ExternalBindingForbidden", "Non-runtime entry cannot declare externalCatalogBinding.", path + ".externalCatalogBinding");
                    if (entry.BundleIntent != null)
                        Error(report, "BundleIntentForbidden", "Non-runtime entry cannot declare bundleIntent.", path + ".bundleIntent");
                    if (entry.DeliveryIntent == ResourceMappingDeliveryIntent.EditorOnly && entry.SourceLocator == null)
                        Error(report, "SourceLocatorMissing", "Editor-only entry requires sourceLocator.", path + ".sourceLocator");
                    else if (entry.DeliveryIntent == ResourceMappingDeliveryIntent.EditorOnly)
                        ValidateSourceLocator(entry.SourceLocator, path + ".sourceLocator", report);
                }
            }
        }

        private static void ValidateSourceLocator(
            ResourceMappingSourceLocator source,
            string path,
            ResourceMappingValidationReport report)
        {
            switch (source.Kind)
            {
                case ResourceMappingSourceLocatorKind.UnityAsset:
                    RequireValue(report, source.UnityGuid, "UnityGuidMissing", path + ".unityGuid");
                    break;
                case ResourceMappingSourceLocatorKind.GeneratedUnityAsset:
                    RequireValue(report, source.GeneratedAssetId, "GeneratedAssetIdMissing", path + ".generatedAssetId");
                    RequireValue(report, source.CachedPath, "GeneratedAssetPathMissing", path + ".cachedPath");
                    break;
                case ResourceMappingSourceLocatorKind.DomainSource:
                    RequireValue(report, source.DomainSourceId, "DomainSourceIdMissing", path + ".domainSourceId");
                    break;
                case ResourceMappingSourceLocatorKind.ImportedPackageFile:
                    if (!IsSafeRelativePath(source.PackageRelativePath ?? string.Empty))
                        Error(report, "PackageRelativePathInvalid", "Imported package path must be normalized, relative and non-escaping.", path + ".packageRelativePath");
                    RequireValue(report, source.ContentHash, "SourceContentHashMissing", path + ".contentHash");
                    break;
                default:
                    Error(report, "SourceLocatorKindUnsupported", "Source locator kind is unsupported.", path + ".kind");
                    break;
            }
        }

        private static void ValidateOwner(
            ResourceMappingProducerReceipt receipt,
            string authoringRecordId,
            string path,
            HashSet<string> ownerKeys,
            ResourceMappingValidationReport report)
        {
            if (receipt == null)
            {
                Error(report, "ProducerReceiptMissing", "producerReceipt is required.", path + ".producerReceipt");
                return;
            }

            RequireValue(report, receipt.ProducerId, "ProducerIdMissing", path + ".producerReceipt.producerId");
            RequireValue(report, receipt.ProducerContractVersion, "ProducerContractVersionMissing", path + ".producerReceipt.producerContractVersion");
            RequireValue(report, receipt.SourceRecordFingerprint, "SourceRecordFingerprintMissing", path + ".producerReceipt.sourceRecordFingerprint");
            RequireValue(report, authoringRecordId, "AuthoringRecordIdMissing", path + ".authoringRecordId");
            if (!string.IsNullOrWhiteSpace(receipt.ProducerId) && !string.IsNullOrWhiteSpace(authoringRecordId))
            {
                string ownerKey = receipt.ProducerId + "|" + authoringRecordId;
                if (!ownerKeys.Add(ownerKey))
                    Error(report, "ProducerRecordDuplicate", "Duplicate producer-owned record: " + ownerKey + ".", path);
            }
        }

        private static void ValidateResourceKey(ResourceMappingResourceKey key, string path, ResourceMappingValidationReport report)
        {
            if (key == null)
            {
                Error(report, "ResourceKeyMissing", "Runtime entry requires resourceKey.", path);
                return;
            }

            RequireValue(report, key.Id, "ResourceKeyIdMissing", path + ".id");
            RequireValue(report, key.TypeId, "ResourceTypeIdMissing", path + ".typeId");
            if (!IsCanonicalResourceId(key.Id))
                Error(report, "ResourceKeyIdInvalid", "ResourceKey id must use lowercase letters, digits, '.', '_' or '-'.", path + ".id");
            if (ResourceMappingCanonicalTypeIds.TryCanonicalize(key.TypeId, out string canonicalTypeId) &&
                !string.Equals(key.TypeId, canonicalTypeId, StringComparison.Ordinal))
                Error(report, "ResourceTypeAliasRejected", "Persisted TypeId alias '" + key.TypeId + "' is not canonical; use '" + canonicalTypeId + "'.", path + ".typeId");
            if (!IsTrimmed(key.TypeId) || !IsTrimmed(key.Variant) || !IsTrimmed(key.PackageId))
                Error(report, "ResourceKeyWhitespace", "ResourceKey fields must not contain leading or trailing whitespace.", path);
        }

        private static bool IsCanonicalResourceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !IsTrimmed(value))
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')
                    continue;
                return false;
            }

            return true;
        }

        private static bool IsSupportedCompression(string value)
        {
            return string.Equals(value, "lz4", StringComparison.Ordinal) ||
                   string.Equals(value, "lzma", StringComparison.Ordinal) ||
                   string.Equals(value, "uncompressed", StringComparison.Ordinal);
        }

        private static bool IsSha256Fingerprint(string value)
        {
            const string prefix = "sha256:";
            if (value == null || !value.StartsWith(prefix, StringComparison.Ordinal) || value.Length != prefix.Length + 64)
                return false;
            for (int i = prefix.Length; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }

        private static bool IsSafeRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !IsTrimmed(value) || value.IndexOf('\\') >= 0 || value.StartsWith("/", StringComparison.Ordinal))
                return false;
            string[] segments = value.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0 || segments[i] == "." || segments[i] == "..")
                    return false;
            }

            return value.IndexOf(':') < 0;
        }

        private static bool IsTrimmed(string value)
        {
            return value == null || string.Equals(value, value.Trim(), StringComparison.Ordinal);
        }

        private static void RequireValue(ResourceMappingValidationReport report, string value, string code, string path)
        {
            if (string.IsNullOrWhiteSpace(value) || !IsTrimmed(value))
                Error(report, code, path + " is required and must be trimmed.", path);
        }

        private static void RequireExact(ResourceMappingValidationReport report, string value, string expected, string code, string path)
        {
            if (!string.Equals(value, expected, StringComparison.Ordinal))
                Error(report, code, path + " must equal '" + expected + "'.", path);
        }

        private static void Error(ResourceMappingValidationReport report, string code, string message, string path)
        {
            report.Diagnostics.Add(new ResourceMappingDiagnostic
            {
                Severity = ResourceMappingDiagnosticSeverity.Error,
                Code = code,
                Message = message,
                Path = path
            });
        }
    }
}

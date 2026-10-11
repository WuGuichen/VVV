using System;
using System.Collections.Generic;

namespace MxFramework.ResourceMapping.Authoring
{
    public static class ResourceMappingAggregationErrorCodes
    {
        public const string None = "";
        public const string RevisionConflict = "RESOURCE_MAPPING_WRITE_CONFLICT";
        public const string ProducerIdMissing = "RESOURCE_MAPPING_PRODUCER_ID_MISSING";
        public const string ProducerOwnershipMismatch = "RESOURCE_MAPPING_PRODUCER_OWNERSHIP_MISMATCH";
        public const string ValidationFailed = "RESOURCE_MAPPING_VALIDATION_FAILED";
    }

    public sealed class ResourceMappingAggregationResult
    {
        public bool Success { get; set; }
        public bool Changed { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string AcceptedRevision { get; set; } = string.Empty;
        public ResourceMappingProfileV2 Profile { get; set; }
        public ResourceMappingValidationReport Validation { get; set; } = new ResourceMappingValidationReport();
    }

    public sealed class ResourceMappingProfileAggregator
    {
        /// <remarks>AllocByDesign. Applies one complete low-frequency producer snapshot to a cloned Profile.</remarks>
        public ResourceMappingAggregationResult ApplyCompleteSnapshot(
            ResourceMappingProfileV2 current,
            string currentRevision,
            string expectedRevision,
            ResourceMappingProducerSnapshot snapshot)
        {
            currentRevision = currentRevision ?? string.Empty;
            expectedRevision = expectedRevision ?? string.Empty;
            if (!string.Equals(currentRevision, expectedRevision, StringComparison.Ordinal))
            {
                return Failure(
                    ResourceMappingAggregationErrorCodes.RevisionConflict,
                    "Expected revision does not match the current Profile revision.",
                    currentRevision);
            }

            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.ProducerId))
            {
                return Failure(
                    ResourceMappingAggregationErrorCodes.ProducerIdMissing,
                    "Producer snapshot requires producerId.",
                    currentRevision);
            }

            if (!SnapshotOwnershipMatches(snapshot))
            {
                return Failure(
                    ResourceMappingAggregationErrorCodes.ProducerOwnershipMismatch,
                    "Every record in a producer snapshot must carry the same producerId as the snapshot.",
                    currentRevision);
            }

            ResourceMappingProfileV2 candidate = CloneProfile(current ?? new ResourceMappingProfileV2());
            ReplaceOwned(candidate.Entries, snapshot.Entries, snapshot.ProducerId, entry => entry.ProducerReceipt);
            ReplaceOwned(candidate.BundleRules, snapshot.BundleRules, snapshot.ProducerId, rule => rule.ProducerReceipt);
            ReplaceOwned(candidate.CatalogMounts, snapshot.CatalogMounts, snapshot.ProducerId, mount => mount.ProducerReceipt);
            ReplaceOwned(candidate.PreloadGroups, snapshot.PreloadGroups, snapshot.ProducerId, group => group.ProducerReceipt);

            candidate.Entries.Sort(CompareEntries);
            candidate.BundleRules.Sort((left, right) => CompareOwned(left.ProducerReceipt, left.AuthoringRecordId, right.ProducerReceipt, right.AuthoringRecordId));
            candidate.CatalogMounts.Sort((left, right) => CompareOwned(left.ProducerReceipt, left.AuthoringRecordId, right.ProducerReceipt, right.AuthoringRecordId));
            candidate.PreloadGroups.Sort((left, right) => CompareOwned(left.ProducerReceipt, left.AuthoringRecordId, right.ProducerReceipt, right.AuthoringRecordId));

            ResourceMappingValidationReport validation = ResourceMappingProfileValidator.Validate(candidate);
            if (validation.HasErrors)
            {
                return new ResourceMappingAggregationResult
                {
                    Success = false,
                    Changed = false,
                    ErrorCode = ResourceMappingAggregationErrorCodes.ValidationFailed,
                    Message = "Producer snapshot would create an invalid Profile.",
                    AcceptedRevision = currentRevision,
                    Profile = null,
                    Validation = validation
                };
            }


            string acceptedRevision = new GlobalResourceBuildPlanCompiler().ComputeProfileRevision(candidate);

            return new ResourceMappingAggregationResult
            {
                Success = true,
                Changed = true,
                ErrorCode = ResourceMappingAggregationErrorCodes.None,
                Message = string.Empty,
                AcceptedRevision = acceptedRevision,
                Profile = candidate,
                Validation = validation
            };
        }

        private static bool SnapshotOwnershipMatches(ResourceMappingProducerSnapshot snapshot)
        {
            return OwnershipMatches(snapshot.Entries, snapshot.ProducerId, entry => entry != null ? entry.ProducerReceipt : null) &&
                   OwnershipMatches(snapshot.BundleRules, snapshot.ProducerId, rule => rule != null ? rule.ProducerReceipt : null) &&
                   OwnershipMatches(snapshot.CatalogMounts, snapshot.ProducerId, mount => mount != null ? mount.ProducerReceipt : null) &&
                   OwnershipMatches(snapshot.PreloadGroups, snapshot.ProducerId, group => group != null ? group.ProducerReceipt : null);
        }

        private static bool OwnershipMatches<T>(IList<T> records, string producerId, Func<T, ResourceMappingProducerReceipt> receipt)
        {
            if (records == null)
                return false;
            for (int i = 0; i < records.Count; i++)
            {
                ResourceMappingProducerReceipt value = receipt(records[i]);
                if (value == null || !string.Equals(value.ProducerId, producerId, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static void ReplaceOwned<T>(
            List<T> current,
            IList<T> replacement,
            string producerId,
            Func<T, ResourceMappingProducerReceipt> receipt)
        {
            current.RemoveAll(item =>
            {
                ResourceMappingProducerReceipt value = item != null ? receipt(item) : null;
                return value != null && string.Equals(value.ProducerId, producerId, StringComparison.Ordinal);
            });
            for (int i = 0; i < replacement.Count; i++)
                current.Add(CloneRecord(replacement[i]));
        }

        private static int CompareEntries(ResourceMappingEntry left, ResourceMappingEntry right)
        {
            return CompareOwned(left.ProducerReceipt, left.AuthoringRecordId, right.ProducerReceipt, right.AuthoringRecordId);
        }

        private static int CompareOwned(
            ResourceMappingProducerReceipt leftReceipt,
            string leftId,
            ResourceMappingProducerReceipt rightReceipt,
            string rightId)
        {
            int producer = string.Compare(leftReceipt != null ? leftReceipt.ProducerId : string.Empty, rightReceipt != null ? rightReceipt.ProducerId : string.Empty, StringComparison.Ordinal);
            return producer != 0 ? producer : string.Compare(leftId ?? string.Empty, rightId ?? string.Empty, StringComparison.Ordinal);
        }

        private static ResourceMappingAggregationResult Failure(string code, string message, string revision)
        {
            return new ResourceMappingAggregationResult
            {
                Success = false,
                Changed = false,
                ErrorCode = code,
                Message = message,
                AcceptedRevision = revision,
                Profile = null
            };
        }

        private static ResourceMappingProfileV2 CloneProfile(ResourceMappingProfileV2 source)
        {
            var clone = new ResourceMappingProfileV2
            {
                Schema = source.Schema,
                ProfileId = source.ProfileId,
                CatalogId = source.CatalogId,
                Entries = CloneList(source.Entries),
                BundleRules = CloneList(source.BundleRules),
                CatalogMounts = CloneList(source.CatalogMounts),
                PreloadGroups = CloneList(source.PreloadGroups),
                Metadata = CloneDictionary(source.Metadata)
            };
            return clone;
        }

        private static List<T> CloneList<T>(IList<T> values)
        {
            var result = new List<T>();
            if (values == null)
                return result;
            for (int i = 0; i < values.Count; i++)
                result.Add(CloneRecord(values[i]));
            return result;
        }

        private static T CloneRecord<T>(T value)
        {
            if (value == null)
                return default(T);
            object clone;
            if (value is ResourceMappingEntry entry)
            {
                clone = new ResourceMappingEntry
                {
                    ProducerReceipt = CloneReceipt(entry.ProducerReceipt),
                    AuthoringRecordId = entry.AuthoringRecordId,
                    ResourceKey = CloneKey(entry.ResourceKey),
                    SourceLocator = CloneSource(entry.SourceLocator),
                    DeliveryIntent = entry.DeliveryIntent,
                    OverrideIntent = entry.OverrideIntent,
                    ExternalCatalogBinding = entry.ExternalCatalogBinding == null ? null : new ResourceMappingExternalCatalogBinding { CatalogMountId = entry.ExternalCatalogBinding.CatalogMountId },
                    BundleIntent = entry.BundleIntent == null ? null : new ResourceMappingBundleIntent { RuleId = entry.BundleIntent.RuleId },
                    Labels = entry.Labels == null ? new List<string>() : new List<string>(entry.Labels),
                    PreloadGroups = entry.PreloadGroups == null ? new List<string>() : new List<string>(entry.PreloadGroups),
                    Dependencies = CloneKeys(entry.Dependencies),
                    ProviderData = CloneDictionary(entry.ProviderData)
                };
            }
            else if (value is ResourceMappingBundleRule rule)
            {
                clone = new ResourceMappingBundleRule
                {
                    ProducerReceipt = CloneReceipt(rule.ProducerReceipt), AuthoringRecordId = rule.AuthoringRecordId,
                    Id = rule.Id, BundleName = rule.BundleName, Compression = rule.Compression, BuildTarget = rule.BuildTarget,
                    IncludeDependencies = rule.IncludeDependencies, AllowEmpty = rule.AllowEmpty, ProviderData = CloneDictionary(rule.ProviderData)
                };
            }
            else if (value is ResourceMappingCatalogMount mount)
            {
                clone = new ResourceMappingCatalogMount
                {
                    ProducerReceipt = CloneReceipt(mount.ProducerReceipt), AuthoringRecordId = mount.AuthoringRecordId,
                    CatalogMountId = mount.CatalogMountId, CatalogId = mount.CatalogId, BootstrapKind = mount.BootstrapKind,
                    RelativeCatalogPath = mount.RelativeCatalogPath, MountOrder = mount.MountOrder,
                    ExpectedCatalogFingerprint = mount.ExpectedCatalogFingerprint
                };
            }
            else if (value is ResourceMappingPreloadGroup group)
            {
                clone = new ResourceMappingPreloadGroup
                {
                    ProducerReceipt = CloneReceipt(group.ProducerReceipt), AuthoringRecordId = group.AuthoringRecordId,
                    Id = group.Id, ExplicitKeys = CloneKeys(group.ExplicitKeys),
                    Labels = group.Labels == null ? new List<string>() : new List<string>(group.Labels),
                    FailFast = group.FailFast, MaxConcurrentLoads = group.MaxConcurrentLoads, AllowEmpty = group.AllowEmpty,
                    ProviderData = CloneDictionary(group.ProviderData)
                };
            }
            else
            {
                throw new InvalidOperationException("Unsupported resource mapping record type: " + typeof(T).FullName + ".");
            }

            return (T)clone;
        }

        private static ResourceMappingProducerReceipt CloneReceipt(ResourceMappingProducerReceipt value)
        {
            return value == null ? null : new ResourceMappingProducerReceipt
            {
                ProducerId = value.ProducerId,
                ProducerContractVersion = value.ProducerContractVersion,
                SourceRecordFingerprint = value.SourceRecordFingerprint
            };
        }

        private static ResourceMappingResourceKey CloneKey(ResourceMappingResourceKey value)
        {
            return value == null ? null : new ResourceMappingResourceKey
            {
                Id = value.Id, TypeId = value.TypeId, Variant = value.Variant, PackageId = value.PackageId
            };
        }

        private static List<ResourceMappingResourceKey> CloneKeys(IList<ResourceMappingResourceKey> values)
        {
            var result = new List<ResourceMappingResourceKey>();
            if (values == null)
                return result;
            for (int i = 0; i < values.Count; i++)
                result.Add(CloneKey(values[i]));
            return result;
        }

        private static ResourceMappingSourceLocator CloneSource(ResourceMappingSourceLocator value)
        {
            return value == null ? null : new ResourceMappingSourceLocator
            {
                Kind = value.Kind, UnityGuid = value.UnityGuid, CachedPath = value.CachedPath,
                GeneratedAssetId = value.GeneratedAssetId, DomainSourceId = value.DomainSourceId,
                PackageRelativePath = value.PackageRelativePath, ContentHash = value.ContentHash,
                ProviderData = CloneDictionary(value.ProviderData)
            };
        }

        private static Dictionary<string, string> CloneDictionary(IDictionary<string, string> values)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (values == null)
                return result;
            foreach (KeyValuePair<string, string> pair in values)
                result[pair.Key] = pair.Value;
            return result;
        }
    }
}

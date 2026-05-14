using System;
using System.Collections.Generic;
using MxFramework.Resources;

namespace MxFramework.Demo
{
    public static class ResourceKeyConfigProfileValidator
    {
        public static ResourceKeyConfigProfileValidationReport Validate(
            ResourceKeyConfigProfile profile,
            ResourceCatalog catalog)
        {
            var report = new ResourceKeyConfigProfileValidationReport();
            if (profile == null)
            {
                report.Add("ProfileMissing", string.Empty, string.Empty, ResourceTypeIds.Object, default, TempImportedResourceCatalog.CatalogId);
                return report;
            }

            if (catalog == null)
            {
                report.Add("CatalogMissing", profile.Source, string.Empty, ResourceTypeIds.Object, default, "catalog:null");
                return report;
            }

            ValidateField(report, profile.Source, "StartScreenButtonNormalTexture", profile.StartScreenButtonNormalTexture, ResourceTypeIds.Texture2D, catalog);
            ValidateField(report, profile.Source, "StartScreenButtonHoverTexture", profile.StartScreenButtonHoverTexture, ResourceTypeIds.Texture2D, catalog);
            ValidateField(report, profile.Source, "StartScreenSeparatorTexture", profile.StartScreenSeparatorTexture, ResourceTypeIds.Texture2D, catalog);
            ValidateField(report, profile.Source, "StartScreenArchiveIconTexture", profile.StartScreenArchiveIconTexture, ResourceTypeIds.Texture2D, catalog);
            ValidateField(report, profile.Source, "StartScreenContinueIconTexture", profile.StartScreenContinueIconTexture, ResourceTypeIds.Texture2D, catalog);
            ValidateField(report, profile.Source, "StartScreenExitIconTexture", profile.StartScreenExitIconTexture, ResourceTypeIds.Texture2D, catalog);
            ValidateField(report, profile.Source, "StartScreenSettingsIconTexture", profile.StartScreenSettingsIconTexture, ResourceTypeIds.Texture2D, catalog);

            if (profile.StatusAuraPrefabs.Count == 0)
            {
                report.Add("ResourceKeyMissing", profile.Source, "StatusAuraPrefabs", ResourceTypeIds.GameObject, default, CatalogContext(catalog, "StatusAuraPrefabs"));
                return report;
            }

            for (int i = 0; i < profile.StatusAuraPrefabs.Count; i++)
                ValidateField(report, profile.Source, "StatusAuraPrefabs[" + i + "]", profile.StatusAuraPrefabs[i], ResourceTypeIds.GameObject, catalog);

            ValidateField(report, profile.Source, "WeaponPrefab", profile.WeaponPrefab, ResourceTypeIds.GameObject, catalog);

            if (profile.MagicEffectAudioClips.Count == 0)
            {
                report.Add("ResourceKeyMissing", profile.Source, "MagicEffectAudioClips", ResourceTypeIds.AudioClip, default, CatalogContext(catalog, "MagicEffectAudioClips"));
                return report;
            }

            for (int i = 0; i < profile.MagicEffectAudioClips.Count; i++)
                ValidateField(report, profile.Source, "MagicEffectAudioClips[" + i + "]", profile.MagicEffectAudioClips[i], ResourceTypeIds.AudioClip, catalog);

            return report;
        }

        private static void ValidateField(
            ResourceKeyConfigProfileValidationReport report,
            string source,
            string field,
            ResourceKey key,
            string expectedType,
            ResourceCatalog catalog)
        {
            if (!key.IsValid)
                report.Add("ResourceKeyInvalid", source, field, expectedType, key, CatalogContext(catalog, field));

            ValidateNoDirectReferenceValue(report, source, field, expectedType, key, catalog);

            if (!string.Equals(key.PackageId, TempImportedResourceCatalog.PackageId, StringComparison.Ordinal))
                report.Add("PackageMismatch", source, field, expectedType, key, CatalogContext(catalog, field));

            if (!string.Equals(key.Variant, string.Empty, StringComparison.Ordinal))
                report.Add("VariantMismatch", source, field, expectedType, key, CatalogContext(catalog, field));

            if (!string.Equals(key.TypeId, expectedType, StringComparison.Ordinal))
                report.Add("ExpectedTypeMismatch", source, field, expectedType, key, CatalogContext(catalog, field));

            ResourceCatalogEntry entry = FindEntry(catalog, key.Id, key.PackageId, key.Variant);
            if (entry == null)
            {
                report.Add("ResourceKeyMissing", source, field, expectedType, key, CatalogContext(catalog, field));
                return;
            }

            ResourceKey catalogKey = entry.CreateKey(catalog.PackageId);
            if (!string.Equals(entry.TypeId, expectedType, StringComparison.Ordinal))
                report.Add("CatalogTypeMismatch", source, field, expectedType, key, CatalogContext(catalog, field) + " actualCatalogType=" + entry.TypeId);

            if (!string.Equals(catalogKey.PackageId, TempImportedResourceCatalog.PackageId, StringComparison.Ordinal))
                report.Add("CatalogPackageMismatch", source, field, expectedType, key, CatalogContext(catalog, field));

            if (!string.Equals(catalogKey.Variant, string.Empty, StringComparison.Ordinal))
                report.Add("CatalogVariantMismatch", source, field, expectedType, key, CatalogContext(catalog, field));
        }

        private static void ValidateNoDirectReferenceValue(
            ResourceKeyConfigProfileValidationReport report,
            string source,
            string field,
            string expectedType,
            ResourceKey key,
            ResourceCatalog catalog)
        {
            ValidateNoDirectReferenceValue(report, source, field, expectedType, key, catalog, key.Id);
            ValidateNoDirectReferenceValue(report, source, field, expectedType, key, catalog, key.TypeId);
            ValidateNoDirectReferenceValue(report, source, field, expectedType, key, catalog, key.PackageId);
            ValidateNoDirectReferenceValue(report, source, field, expectedType, key, catalog, key.Variant);
        }

        private static void ValidateNoDirectReferenceValue(
            ResourceKeyConfigProfileValidationReport report,
            string source,
            string field,
            string expectedType,
            ResourceKey key,
            ResourceCatalog catalog,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (value.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                || value.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                report.Add("DirectAssetPathForbidden", source, field, expectedType, key, CatalogContext(catalog, field));

            if (value.IndexOf("_TempImportedResources", StringComparison.Ordinal) >= 0)
                report.Add("TempImportedResourcePathForbidden", source, field, expectedType, key, CatalogContext(catalog, field));

            if (value.IndexOf(".bundle", StringComparison.OrdinalIgnoreCase) >= 0)
                report.Add("BundleFileNameForbidden", source, field, expectedType, key, CatalogContext(catalog, field));

            if (value.IndexOf(".bank", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("bank:/", StringComparison.OrdinalIgnoreCase) >= 0)
                report.Add("FmodBankReferenceForbidden", source, field, expectedType, key, CatalogContext(catalog, field));

            if (value.IndexOf("event:/", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("fmod", StringComparison.OrdinalIgnoreCase) >= 0)
                report.Add("FmodEventReferenceForbidden", source, field, expectedType, key, CatalogContext(catalog, field));

            if (value.IndexOf("Unity" + "Engine.Object", StringComparison.Ordinal) >= 0)
                report.Add("UnityObjectReferenceForbidden", source, field, expectedType, key, CatalogContext(catalog, field));

            if (ContainsGuidLikeToken(value))
                report.Add("GuidReferenceForbidden", source, field, expectedType, key, CatalogContext(catalog, field));
        }

        private static ResourceCatalogEntry FindEntry(
            ResourceCatalog catalog,
            string id,
            string packageId,
            string variant)
        {
            if (catalog == null)
                return null;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                ResourceKey entryKey = entry.CreateKey(catalog.PackageId);
                if (string.Equals(entryKey.Id, id, StringComparison.Ordinal)
                    && string.Equals(entryKey.PackageId, packageId, StringComparison.Ordinal)
                    && string.Equals(entryKey.Variant, variant, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static string CatalogContext(ResourceCatalog catalog, string field)
        {
            if (catalog == null)
                return "catalog:null field=" + field;

            return "catalog=" + catalog.CatalogId + " package=" + catalog.PackageId + " field=" + field;
        }

        private static bool ContainsGuidLikeToken(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (!IsHex(value[i]))
                    continue;

                int count = 0;
                int j = i;
                while (j < value.Length && IsHex(value[j]))
                {
                    count++;
                    j++;
                }

                if (count == 32)
                    return true;

                if (count == 8 && ContainsHyphenatedGuidToken(value, i))
                    return true;

                i = j;
            }

            return false;
        }

        private static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9')
                || (c >= 'a' && c <= 'f')
                || (c >= 'A' && c <= 'F');
        }

        private static bool ContainsHyphenatedGuidToken(string value, int start)
        {
            int[] groups = { 8, 4, 4, 4, 12 };
            int index = start;
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                for (int i = 0; i < groups[groupIndex]; i++)
                {
                    if (index >= value.Length || !IsHex(value[index]))
                        return false;

                    index++;
                }

                if (groupIndex == groups.Length - 1)
                    return true;

                if (index >= value.Length || value[index] != '-')
                    return false;

                index++;
            }

            return false;
        }
    }

    public sealed class ResourceKeyConfigProfileValidationReport
    {
        private readonly List<ResourceKeyConfigProfileValidationIssue> _issues =
            new List<ResourceKeyConfigProfileValidationIssue>();

        public IReadOnlyList<ResourceKeyConfigProfileValidationIssue> Issues => _issues;
        public bool HasErrors => _issues.Count > 0;

        public void Add(
            string code,
            string source,
            string field,
            string expectedType,
            ResourceKey actualKey,
            string catalogContext)
        {
            _issues.Add(new ResourceKeyConfigProfileValidationIssue(
                code,
                source,
                field,
                expectedType,
                actualKey,
                catalogContext));
        }
    }

    public sealed class ResourceKeyConfigProfileValidationIssue
    {
        public ResourceKeyConfigProfileValidationIssue(
            string code,
            string source,
            string field,
            string expectedType,
            ResourceKey actualKey,
            string catalogContext)
        {
            Code = code ?? string.Empty;
            Source = source ?? string.Empty;
            Field = field ?? string.Empty;
            ExpectedType = expectedType ?? string.Empty;
            ActualKey = actualKey;
            CatalogContext = catalogContext ?? string.Empty;
            Message = "source=" + Source
                + " field=" + Field
                + " expectedType=" + ExpectedType
                + " actualKey=" + ActualKey
                + " package=" + ActualKey.PackageId
                + " variant=" + ActualKey.Variant
                + " catalogContext=" + CatalogContext;
        }

        public string Code { get; }
        public string Source { get; }
        public string Field { get; }
        public string ExpectedType { get; }
        public ResourceKey ActualKey { get; }
        public string CatalogContext { get; }
        public string Message { get; }
    }
}

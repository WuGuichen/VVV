using System;
using System.Collections.Generic;

namespace MxFramework.Config.Runtime
{
    /// <summary>
    /// A deterministic load plan built from a <see cref="RuntimeModPackageCatalog"/>.
    /// </summary>
    public sealed class RuntimeModPackageLoadPlan
    {
        public RuntimeModPackageLoadPlan(
            IReadOnlyList<RuntimeModPackageCatalogItem> orderedItems,
            IReadOnlyList<RuntimeModPackageCatalogItem> skippedItems,
            IReadOnlyList<string> warnings = null)
        {
            OrderedItems = orderedItems ?? Array.Empty<RuntimeModPackageCatalogItem>();
            SkippedItems = skippedItems ?? Array.Empty<RuntimeModPackageCatalogItem>();
            Warnings = warnings ?? Array.Empty<string>();
        }

        /// <summary>
        /// Items that will be loaded, in load order:
        /// 1. Preview kind, sorted by packageId then root path.
        /// 2. Mod kind, sorted by packageId then root path.
        /// </summary>
        public IReadOnlyList<RuntimeModPackageCatalogItem> OrderedItems { get; }

        /// <summary>
        /// Items that will be skipped (invalid or disabled).
        /// Invalid items are always skipped regardless of enabledPackageIds.
        /// </summary>
        public IReadOnlyList<RuntimeModPackageCatalogItem> SkippedItems { get; }

        /// <summary>Non-blocking plan warnings (for example missing loadout package keys).</summary>
        public IReadOnlyList<string> Warnings { get; }
    }

    /// <summary>
    /// Builds deterministic <see cref="RuntimeModPackageLoadPlan"/>s from catalogs.
    /// Sorting is stable and does not depend on filesystem enumeration order.
    /// </summary>
    public static class RuntimeModPackageLoadPlanBuilder
    {
        /// <summary>
        /// Build a load plan from a catalog.
        /// </summary>
        /// <param name="catalog">The discovered package catalog.</param>
        /// <param name="enabledPackageIds">
        /// Optional set of packageIds to enable. If null, all valid packages are enabled.
        /// Invalid packages are always skipped regardless of this parameter.
        /// Disabled (not in set) valid packages go to SkippedItems, not errors.
        /// </param>
        /// <returns>A deterministic load plan. Never null.</returns>
        public static RuntimeModPackageLoadPlan Build(
            RuntimeModPackageCatalog catalog,
            ISet<string> enabledPackageIds = null)
        {
            if (catalog == null || catalog.Items == null || catalog.Items.Count == 0)
                return new RuntimeModPackageLoadPlan(
                    Array.Empty<RuntimeModPackageCatalogItem>(),
                    Array.Empty<RuntimeModPackageCatalogItem>());

            var ordered = new List<RuntimeModPackageCatalogItem>();
            var skipped = new List<RuntimeModPackageCatalogItem>();
            var warnings = new List<string>();

            for (int i = 0; i < catalog.Items.Count; i++)
            {
                RuntimeModPackageCatalogItem item = catalog.Items[i];

                // Invalid items are always skipped
                if (!item.IsValid)
                {
                    skipped.Add(item);
                    continue;
                }

                // Check enabled filter
                if (enabledPackageIds != null && item.Manifest != null)
                {
                    if (!enabledPackageIds.Contains(item.Manifest.PackageId))
                    {
                        skipped.Add(item);
                        continue;
                    }
                }

                ordered.Add(item);
            }

            // Sort: kind (Preview before Mod), then packageId, then root path
            ordered.Sort(CompareItems);

            // Detect duplicate packageIds within the ordered set
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ordered.Count; i++)
            {
                RuntimeModPackageCatalogItem item = ordered[i];
                string pid = item.Manifest?.PackageId ?? string.Empty;
                if (!string.IsNullOrEmpty(pid) && !seenIds.Add(pid))
                {
                    // Use setter via GetType — we'll create new item with warning
                    var newWarnings = new List<string>(item.Warnings);
                    newWarnings.Add($"Duplicate packageId '{pid}' at '{item.PackageRootPath}' — sorted by root path for determinism.");
                    ordered[i] = new RuntimeModPackageCatalogItem(
                        item.PackageRootPath,
                        item.Manifest,
                        item.IsValid,
                        item.Errors,
                        newWarnings,
                        item.ContainerPath,
                        item.PackageRelativePath,
                        item.PackageKey);
                }
            }

            return new RuntimeModPackageLoadPlan(ordered, skipped, warnings);
        }

        /// <summary>
        /// Build a load plan from a catalog and loadout package keys.
        /// loadout null => all valid packages enabled (same as legacy behavior).
        /// empty enabledPackageKeys => enable no packages.
        /// </summary>
        public static RuntimeModPackageLoadPlan Build(
            RuntimeModPackageCatalog catalog,
            RuntimeModPackageLoadout loadout)
        {
            if (catalog == null || catalog.Items == null || catalog.Items.Count == 0)
            {
                return new RuntimeModPackageLoadPlan(
                    Array.Empty<RuntimeModPackageCatalogItem>(),
                    Array.Empty<RuntimeModPackageCatalogItem>());
            }

            if (loadout == null)
                return Build(catalog, enabledPackageIds: null);

            var enabledKeys = new HashSet<string>(loadout.EnabledPackageKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
            var discoveredKeys = new HashSet<string>(StringComparer.Ordinal);
            var ordered = new List<RuntimeModPackageCatalogItem>();
            var skipped = new List<RuntimeModPackageCatalogItem>();
            var warnings = new List<string>();

            for (int i = 0; i < catalog.Items.Count; i++)
            {
                RuntimeModPackageCatalogItem item = catalog.Items[i];
                if (!string.IsNullOrWhiteSpace(item.PackageKey))
                    discoveredKeys.Add(item.PackageKey);

                if (!item.IsValid)
                {
                    skipped.Add(item);
                    continue;
                }

                bool enabled = enabledKeys.Contains(item.PackageKey);
                if (enabled)
                {
                    ordered.Add(item);
                }
                else
                {
                    skipped.Add(item);
                }
            }

            ordered.Sort(CompareItems);

            for (int i = 0; i < ordered.Count; i++)
            {
                RuntimeModPackageCatalogItem item = ordered[i];
                string pid = item.Manifest?.PackageId ?? string.Empty;
                if (string.IsNullOrEmpty(pid))
                    continue;

                bool seenEarlier = false;
                for (int j = 0; j < i; j++)
                {
                    if (string.Equals(ordered[j].Manifest?.PackageId ?? string.Empty, pid, StringComparison.Ordinal))
                    {
                        seenEarlier = true;
                        break;
                    }
                }

                if (!seenEarlier)
                    continue;

                var newWarnings = new List<string>(item.Warnings);
                newWarnings.Add($"Duplicate packageId '{pid}' at '{item.PackageRootPath}' — sorted by root path for determinism.");
                ordered[i] = new RuntimeModPackageCatalogItem(
                    item.PackageRootPath,
                    item.Manifest,
                    item.IsValid,
                    item.Errors,
                    newWarnings,
                    item.ContainerPath,
                    item.PackageRelativePath,
                    item.PackageKey);
            }

            foreach (string key in enabledKeys)
            {
                if (!discoveredKeys.Contains(key))
                    warnings.Add($"loadout references missing package key: {key}");
            }

            return new RuntimeModPackageLoadPlan(ordered, skipped, warnings);
        }

        private static int CompareItems(RuntimeModPackageCatalogItem a, RuntimeModPackageCatalogItem b)
        {
            string kindA = a.Manifest?.Kind ?? string.Empty;
            string kindB = b.Manifest?.Kind ?? string.Empty;

            // Preview before Mod
            int kindOrder = GetKindOrder(kindA).CompareTo(GetKindOrder(kindB));
            if (kindOrder != 0) return kindOrder;

            // By packageId
            string idA = a.Manifest?.PackageId ?? string.Empty;
            string idB = b.Manifest?.PackageId ?? string.Empty;
            int idOrder = string.Compare(idA, idB, StringComparison.Ordinal);
            if (idOrder != 0) return idOrder;

            // By root path as final tiebreaker
            return string.Compare(a.PackageRootPath, b.PackageRootPath, StringComparison.Ordinal);
        }

        private static int GetKindOrder(string kind)
        {
            return kind == "Preview" ? 0 : 1;
        }
    }
}

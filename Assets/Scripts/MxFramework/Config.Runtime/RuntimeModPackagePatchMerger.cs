using System;
using System.Collections.Generic;
using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    public sealed class RuntimeModPackageMergePackageReport
    {
        public RuntimeModPackageMergePackageReport(
            string packageId,
            string packageRootPath,
            bool isApplied,
            bool isSkipped,
            string reason,
            int modifierPatchCount,
            int buffPatchCount)
        {
            PackageId = packageId ?? string.Empty;
            PackageRootPath = packageRootPath ?? string.Empty;
            IsApplied = isApplied;
            IsSkipped = isSkipped;
            Reason = reason ?? string.Empty;
            ModifierPatchCount = modifierPatchCount;
            BuffPatchCount = buffPatchCount;
        }

        public string PackageId { get; }
        public string PackageRootPath { get; }
        public bool IsApplied { get; }
        public bool IsSkipped { get; }
        public string Reason { get; }
        public int ModifierPatchCount { get; }
        public int BuffPatchCount { get; }
    }

    public sealed class RuntimeModPackageOverrideRecord
    {
        public RuntimeModPackageOverrideRecord(
            string configTypeName,
            int id,
            IReadOnlyList<string> packageChain,
            string winnerPackageId)
        {
            ConfigTypeName = configTypeName ?? string.Empty;
            Id = id;
            PackageChain = packageChain ?? Array.Empty<string>();
            WinnerPackageId = winnerPackageId ?? string.Empty;
        }

        public string ConfigTypeName { get; }
        public int Id { get; }
        public IReadOnlyList<string> PackageChain { get; }
        public string WinnerPackageId { get; }
    }

    public sealed class RuntimeModPackageMergeReport
    {
        public RuntimeModPackageMergeReport(
            IReadOnlyList<RuntimeModPackageMergePackageReport> packages,
            IReadOnlyList<RuntimeModPackageOverrideRecord> overrides,
            IReadOnlyList<string> errors)
        {
            Packages = packages ?? Array.Empty<RuntimeModPackageMergePackageReport>();
            Overrides = overrides ?? Array.Empty<RuntimeModPackageOverrideRecord>();
            Errors = errors ?? Array.Empty<string>();
        }

        public IReadOnlyList<RuntimeModPackageMergePackageReport> Packages { get; }
        public IReadOnlyList<RuntimeModPackageOverrideRecord> Overrides { get; }
        public IReadOnlyList<string> Errors { get; }
        public int AppliedPackageCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Packages.Count; i++)
                {
                    if (Packages[i].IsApplied)
                        count++;
                }
                return count;
            }
        }
        public int SkippedPackageCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Packages.Count; i++)
                {
                    if (Packages[i].IsSkipped)
                        count++;
                }
                return count;
            }
        }
    }

    public sealed class RuntimeModPackageMergeResult
    {
        public RuntimeModPackageMergeResult(
            bool success,
            ConfigRegistry registry,
            RuntimeModPackageMergeReport report,
            ConfigChangeSet changeSet)
        {
            Success = success;
            Registry = registry;
            Report = report ?? new RuntimeModPackageMergeReport(
                Array.Empty<RuntimeModPackageMergePackageReport>(),
                Array.Empty<RuntimeModPackageOverrideRecord>(),
                Array.Empty<string>());
            ChangeSet = changeSet ?? new ConfigChangeSet();
        }

        public bool Success { get; }
        public ConfigRegistry Registry { get; }
        public RuntimeModPackageMergeReport Report { get; }
        public ConfigChangeSet ChangeSet { get; }
    }

    public static class RuntimeModPackagePatchMerger
    {
        public static RuntimeModPackageMergeResult Merge(
            RuntimeModPackageLoadPlan loadPlan,
            IConfigProvider baseRegistry)
        {
            var packageReports = new List<RuntimeModPackageMergePackageReport>();
            var errors = new List<string>();

            if (loadPlan == null)
            {
                errors.Add("LoadPlan is null.");
                return Failed(packageReports, errors);
            }

            if (baseRegistry == null)
            {
                errors.Add("Base registry is null.");
                return Failed(packageReports, errors);
            }

            for (int i = 0; i < loadPlan.SkippedItems.Count; i++)
            {
                RuntimeModPackageCatalogItem item = loadPlan.SkippedItems[i];
                string reason = item.IsValid ? "disabled" : "invalid";
                packageReports.Add(new RuntimeModPackageMergePackageReport(
                    GetPackageId(item),
                    item.PackageRootPath,
                    isApplied: false,
                    isSkipped: true,
                    reason: reason,
                    modifierPatchCount: 0,
                    buffPatchCount: 0));
            }

            var modifierPatches = new List<ConfigPatchEntry<BasicModifierConfig>>();
            var buffPatches = new List<ConfigPatchEntry<BasicBuffConfig>>();
            var touchChains = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            for (int i = 0; i < loadPlan.OrderedItems.Count; i++)
            {
                RuntimeModPackageCatalogItem item = loadPlan.OrderedItems[i];
                string packageId = GetPackageId(item);

                RuntimeModPackageLoadResult loaded;
                try
                {
                    loaded = RuntimeModPackageLoader.LoadFromDirectory(item.PackageRootPath);
                }
                catch (Exception ex)
                {
                    string error = $"Ordered package load failed: packageId={packageId}, path={item.PackageRootPath}, error={ex.Message}";
                    errors.Add(error);
                    packageReports.Add(new RuntimeModPackageMergePackageReport(
                        packageId,
                        item.PackageRootPath,
                        isApplied: false,
                        isSkipped: false,
                        reason: "load-error",
                        modifierPatchCount: 0,
                        buffPatchCount: 0));
                    return Failed(packageReports, errors);
                }

                RuntimeConfigPatchBundle bundle = loaded.PatchBundle;
                packageReports.Add(new RuntimeModPackageMergePackageReport(
                    packageId,
                    item.PackageRootPath,
                    isApplied: true,
                    isSkipped: false,
                    reason: string.Empty,
                    modifierPatchCount: bundle.ModifierPatches.Count,
                    buffPatchCount: bundle.BuffPatches.Count));

                for (int patchIndex = 0; patchIndex < bundle.ModifierPatches.Count; patchIndex++)
                {
                    ConfigPatchEntry<BasicModifierConfig> patch = bundle.ModifierPatches[patchIndex];
                    modifierPatches.Add(patch);
                    TrackTouch(touchChains, typeof(BasicModifierConfig), patch.Id, packageId);
                }

                for (int patchIndex = 0; patchIndex < bundle.BuffPatches.Count; patchIndex++)
                {
                    ConfigPatchEntry<BasicBuffConfig> patch = bundle.BuffPatches[patchIndex];
                    buffPatches.Add(patch);
                    TrackTouch(touchChains, typeof(BasicBuffConfig), patch.Id, packageId);
                }
            }

            IReadOnlyCollection<BasicBuffConfig> baseBuffs = baseRegistry.GetAllConfigs<BasicBuffConfig>();
            IReadOnlyCollection<BasicModifierConfig> baseModifiers = baseRegistry.GetAllConfigs<BasicModifierConfig>();

            ConfigPatchMergeResult<BasicBuffConfig> mergedBuffs = RuntimeConfigPatchMerger.Merge(
                BasicBuffConfig.CreateSchema(),
                baseBuffs,
                buffPatches);

            ConfigPatchMergeResult<BasicModifierConfig> mergedModifiers = RuntimeConfigPatchMerger.Merge(
                BasicModifierConfig.CreateSchema(),
                baseModifiers,
                modifierPatches);

            var changeSet = new ConfigChangeSet();
            for (int i = 0; i < mergedBuffs.ChangeSet.Count; i++)
                changeSet.Add(mergedBuffs.ChangeSet.Changes[i]);
            for (int i = 0; i < mergedModifiers.ChangeSet.Count; i++)
                changeSet.Add(mergedModifiers.ChangeSet.Changes[i]);

            var overrides = BuildOverrides(touchChains);

            var registry = new ConfigRegistry();
            registry.RegisterProvider<BasicBuffConfig>(mergedBuffs.Table);
            registry.RegisterProvider<BasicModifierConfig>(mergedModifiers.Table);

            return new RuntimeModPackageMergeResult(
                success: true,
                registry: registry,
                report: new RuntimeModPackageMergeReport(packageReports, overrides, errors),
                changeSet: changeSet);
        }

        private static RuntimeModPackageMergeResult Failed(
            IReadOnlyList<RuntimeModPackageMergePackageReport> packageReports,
            IReadOnlyList<string> errors)
        {
            return new RuntimeModPackageMergeResult(
                success: false,
                registry: null,
                report: new RuntimeModPackageMergeReport(
                    packageReports,
                    Array.Empty<RuntimeModPackageOverrideRecord>(),
                    errors),
                changeSet: new ConfigChangeSet());
        }

        private static string GetPackageId(RuntimeModPackageCatalogItem item)
        {
            return item?.Manifest?.PackageId ?? item?.DisplayIdentity ?? string.Empty;
        }

        private static void TrackTouch(
            Dictionary<string, List<string>> touchChains,
            Type configType,
            int id,
            string packageId)
        {
            string key = configType.FullName + ":" + id;
            if (!touchChains.TryGetValue(key, out List<string> chain))
            {
                chain = new List<string>();
                touchChains[key] = chain;
            }

            if (chain.Count == 0 || !string.Equals(chain[chain.Count - 1], packageId, StringComparison.Ordinal))
                chain.Add(packageId);
        }

        private static IReadOnlyList<RuntimeModPackageOverrideRecord> BuildOverrides(
            Dictionary<string, List<string>> touchChains)
        {
            var records = new List<RuntimeModPackageOverrideRecord>();

            foreach (KeyValuePair<string, List<string>> kv in touchChains)
            {
                List<string> chain = kv.Value;
                if (chain == null || chain.Count <= 1)
                    continue;

                string key = kv.Key;
                int split = key.LastIndexOf(':');
                if (split <= 0 || split >= key.Length - 1)
                    continue;

                string typeName = key.Substring(0, split);
                if (!int.TryParse(key.Substring(split + 1), out int id))
                    continue;

                records.Add(new RuntimeModPackageOverrideRecord(
                    typeName,
                    id,
                    new List<string>(chain),
                    chain[chain.Count - 1]));
            }

            return records;
        }
    }
}

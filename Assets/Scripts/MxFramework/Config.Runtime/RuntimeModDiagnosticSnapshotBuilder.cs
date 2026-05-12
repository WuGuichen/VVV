using System;
using System.Collections.Generic;

namespace MxFramework.Config.Runtime
{
    public static class RuntimeModDiagnosticSnapshotBuilder
    {
        public static RuntimeModDiagnosticSnapshot Build(
            RuntimeModPackageCatalog catalog,
            RuntimeModPackageLoadout loadout,
            RuntimeModPackageLoadPlan loadPlan,
            RuntimeModPackageMergeResult mergeResult,
            bool includeAbsolutePaths = true)
        {
            var packageKeySet = new HashSet<string>(StringComparer.Ordinal);
            var packageIdToKeys = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var packageSummaries = BuildPackageSummaries(catalog, loadPlan, includeAbsolutePaths, packageKeySet, packageIdToKeys);
            var loadPlanItems = BuildLoadPlanItems(loadPlan);
            var errors = new List<RuntimeModDiagnosticIssue>();
            var warnings = new List<RuntimeModDiagnosticIssue>();
            var overrides = BuildOverrides(mergeResult, packageIdToKeys, warnings);

            if (loadPlan != null && loadPlan.Warnings != null)
            {
                for (int i = 0; i < loadPlan.Warnings.Count; i++)
                {
                    warnings.Add(new RuntimeModDiagnosticIssue
                    {
                        Severity = "Warning",
                        Code = "LoadPlanWarning",
                        Source = "loadPlan",
                        Message = loadPlan.Warnings[i] ?? string.Empty
                    });
                }
            }

            if (mergeResult != null && mergeResult.Report != null && mergeResult.Report.Errors != null)
            {
                for (int i = 0; i < mergeResult.Report.Errors.Count; i++)
                {
                    errors.Add(new RuntimeModDiagnosticIssue
                    {
                        Severity = "Error",
                        Code = "MergeError",
                        Source = "merge",
                        Message = mergeResult.Report.Errors[i] ?? string.Empty
                    });
                }
            }

            SortIssues(errors);
            SortIssues(warnings);
            SortPackages(packageSummaries);
            SortOverrides(overrides);

            int discovered = catalog?.Items?.Count ?? 0;
            int valid = CountValid(catalog);
            int invalid = discovered - valid;
            int orderedCount = loadPlan?.OrderedItems?.Count ?? 0;
            int skippedCount = loadPlan?.SkippedItems?.Count ?? 0;
            int enabledCount = orderedCount;

            var snapshot = new RuntimeModDiagnosticSnapshot
            {
                Format = RuntimeModDiagnosticSnapshot.ExpectedFormat,
                GeneratedUtc = DateTime.UtcNow.ToString("O"),
                Success = (mergeResult?.Success ?? true) && errors.Count == 0,
                Summary = new RuntimeModDiagnosticSummary
                {
                    Discovered = discovered,
                    Valid = valid,
                    Invalid = invalid,
                    Enabled = enabledCount,
                    Ordered = orderedCount,
                    Skipped = skippedCount,
                    Overrides = overrides.Count,
                    Errors = errors.Count,
                    Warnings = warnings.Count
                },
                Loadout = new RuntimeModDiagnosticLoadoutSummary
                {
                    IsDefaultAll = loadout == null,
                    ProfileId = loadout?.ProfileId ?? "default-all",
                    DisplayName = loadout?.DisplayName ?? "Default (all valid packages)",
                    EnabledPackageKeys = loadout?.EnabledPackageKeys ?? Array.Empty<string>()
                },
                Packages = packageSummaries,
                LoadPlan = loadPlanItems,
                Overrides = overrides,
                Errors = errors,
                Warnings = warnings
            };

            return snapshot;
        }

        private static List<RuntimeModDiagnosticPackageSummary> BuildPackageSummaries(
            RuntimeModPackageCatalog catalog,
            RuntimeModPackageLoadPlan loadPlan,
            bool includeAbsolutePaths,
            HashSet<string> packageKeySet,
            Dictionary<string, List<string>> packageIdToKeys)
        {
            var summaries = new List<RuntimeModDiagnosticPackageSummary>();
            if (catalog?.Items == null)
                return summaries;

            var enabledKeys = new HashSet<string>(StringComparer.Ordinal);
            if (loadPlan?.OrderedItems != null)
            {
                for (int i = 0; i < loadPlan.OrderedItems.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(loadPlan.OrderedItems[i].PackageKey))
                        enabledKeys.Add(loadPlan.OrderedItems[i].PackageKey);
                }
            }

            for (int i = 0; i < catalog.Items.Count; i++)
            {
                RuntimeModPackageCatalogItem item = catalog.Items[i];
                string packageId = item.Manifest?.PackageId ?? string.Empty;
                string packageKey = item.PackageKey ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(packageKey))
                    packageKeySet.Add(packageKey);

                if (!string.IsNullOrWhiteSpace(packageId))
                {
                    if (!packageIdToKeys.TryGetValue(packageId, out List<string> keys))
                    {
                        keys = new List<string>();
                        packageIdToKeys[packageId] = keys;
                    }

                    if (!keys.Contains(packageKey))
                        keys.Add(packageKey);
                }

                summaries.Add(new RuntimeModDiagnosticPackageSummary
                {
                    PackageKey = packageKey,
                    PackageId = packageId,
                    DisplayName = item.Manifest?.DisplayName ?? item.DisplayIdentity,
                    Version = item.Manifest?.Version ?? string.Empty,
                    Kind = item.Manifest?.Kind ?? string.Empty,
                    ContainerPath = includeAbsolutePaths ? (item.ContainerPath ?? string.Empty) : string.Empty,
                    PackageRelativePath = item.PackageRelativePath ?? string.Empty,
                    IsValid = item.IsValid,
                    IsEnabled = enabledKeys.Contains(packageKey),
                    Errors = item.Errors ?? Array.Empty<string>(),
                    Warnings = item.Warnings ?? Array.Empty<string>()
                });
            }

            return summaries;
        }

        private static List<RuntimeModDiagnosticLoadPlanItem> BuildLoadPlanItems(RuntimeModPackageLoadPlan loadPlan)
        {
            var items = new List<RuntimeModDiagnosticLoadPlanItem>();
            if (loadPlan == null)
                return items;

            if (loadPlan.OrderedItems != null)
            {
                for (int i = 0; i < loadPlan.OrderedItems.Count; i++)
                {
                    RuntimeModPackageCatalogItem item = loadPlan.OrderedItems[i];
                    items.Add(new RuntimeModDiagnosticLoadPlanItem
                    {
                        PackageKey = item.PackageKey ?? string.Empty,
                        PackageId = item.Manifest?.PackageId ?? string.Empty,
                        State = "Ordered",
                        SkipReason = string.Empty,
                        OrderIndex = i
                    });
                }
            }

            if (loadPlan.SkippedItems != null)
            {
                var skipped = new List<RuntimeModPackageCatalogItem>(loadPlan.SkippedItems);
                skipped.Sort((a, b) => string.Compare(a?.PackageKey ?? string.Empty, b?.PackageKey ?? string.Empty, StringComparison.Ordinal));

                for (int i = 0; i < skipped.Count; i++)
                {
                    RuntimeModPackageCatalogItem item = skipped[i];
                    string reason = item.IsValid ? "Disabled" : "Invalid";
                    items.Add(new RuntimeModDiagnosticLoadPlanItem
                    {
                        PackageKey = item.PackageKey ?? string.Empty,
                        PackageId = item.Manifest?.PackageId ?? string.Empty,
                        State = "Skipped",
                        SkipReason = reason,
                        OrderIndex = -1
                    });
                }
            }

            return items;
        }

        private static List<RuntimeModDiagnosticOverride> BuildOverrides(
            RuntimeModPackageMergeResult mergeResult,
            Dictionary<string, List<string>> packageIdToKeys,
            List<RuntimeModDiagnosticIssue> warnings)
        {
            var result = new List<RuntimeModDiagnosticOverride>();
            if (mergeResult?.Report?.Overrides == null)
                return result;

            for (int i = 0; i < mergeResult.Report.Overrides.Count; i++)
            {
                RuntimeModPackageOverrideRecord record = mergeResult.Report.Overrides[i];
                var chain = new List<string>(record.PackageChain ?? Array.Empty<string>());
                string winnerKey = string.Empty;

                if (!string.IsNullOrWhiteSpace(record.WinnerPackageId))
                {
                    if (packageIdToKeys.TryGetValue(record.WinnerPackageId, out List<string> keys))
                    {
                        if (keys.Count == 1)
                        {
                            winnerKey = keys[0];
                        }
                        else if (keys.Count > 1)
                        {
                            warnings.Add(new RuntimeModDiagnosticIssue
                            {
                                Severity = "Warning",
                                Code = "OverrideWinnerKeyAmbiguous",
                                Source = "merge.override",
                                Message = $"Cannot resolve unique packageKey for winnerPackageId '{record.WinnerPackageId}'."
                            });
                        }
                    }
                    else
                    {
                        warnings.Add(new RuntimeModDiagnosticIssue
                        {
                            Severity = "Warning",
                            Code = "OverrideWinnerKeyMissing",
                            Source = "merge.override",
                            Message = $"No packageKey mapping found for winnerPackageId '{record.WinnerPackageId}'."
                        });
                    }
                }

                result.Add(new RuntimeModDiagnosticOverride
                {
                    ConfigType = record.ConfigTypeName ?? string.Empty,
                    Id = record.Id,
                    PackageChain = chain,
                    WinnerPackageId = record.WinnerPackageId ?? string.Empty,
                    WinnerPackageKey = winnerKey
                });
            }

            return result;
        }

        private static void SortIssues(List<RuntimeModDiagnosticIssue> issues)
        {
            issues.Sort((a, b) =>
            {
                int sourceOrder = string.Compare(a.Source ?? string.Empty, b.Source ?? string.Empty, StringComparison.Ordinal);
                if (sourceOrder != 0) return sourceOrder;
                int codeOrder = string.Compare(a.Code ?? string.Empty, b.Code ?? string.Empty, StringComparison.Ordinal);
                if (codeOrder != 0) return codeOrder;
                return string.Compare(a.Message ?? string.Empty, b.Message ?? string.Empty, StringComparison.Ordinal);
            });
        }

        private static void SortPackages(List<RuntimeModDiagnosticPackageSummary> packages)
        {
            packages.Sort((a, b) => string.Compare(a.PackageKey ?? string.Empty, b.PackageKey ?? string.Empty, StringComparison.Ordinal));
        }

        private static void SortOverrides(List<RuntimeModDiagnosticOverride> overrides)
        {
            overrides.Sort((a, b) =>
            {
                int typeOrder = string.Compare(a.ConfigType ?? string.Empty, b.ConfigType ?? string.Empty, StringComparison.Ordinal);
                if (typeOrder != 0) return typeOrder;
                return a.Id.CompareTo(b.Id);
            });
        }

        private static int CountValid(RuntimeModPackageCatalog catalog)
        {
            if (catalog?.Items == null)
                return 0;

            int count = 0;
            for (int i = 0; i < catalog.Items.Count; i++)
            {
                if (catalog.Items[i].IsValid)
                    count++;
            }

            return count;
        }
    }
}

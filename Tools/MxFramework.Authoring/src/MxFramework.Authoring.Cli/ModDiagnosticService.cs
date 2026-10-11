using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static class ModDiagnosticService
{
    internal static ModDiagnosticSnapshotDto BuildSnapshot(string[] containers, string loadoutPath, bool includeAbsolutePaths)
    {
        var catalogItems = new List<CliCatalogItem>();
        foreach (string container in containers)
        {
            string resolved = container;
            if (!Path.IsPathRooted(resolved))
                resolved = Path.GetFullPath(resolved);

            if (!Directory.Exists(resolved))
                continue;

            foreach (string subDir in Directory.GetDirectories(resolved))
            {
                string modJsonPath = Path.Combine(subDir, "mod.json");
                if (!File.Exists(modJsonPath))
                    continue;

                var item = LoadCatalogItem(subDir, resolved, modJsonPath);
                catalogItems.Add(item);
            }
        }

        CliLoadout loadout = null;
        if (!string.IsNullOrEmpty(loadoutPath))
        {
            string loadoutJson = File.ReadAllText(loadoutPath);
            loadout = JsonSerializer.Deserialize<CliLoadout>(loadoutJson, CreateCliJsonOptions()) ?? new CliLoadout();
            if (!string.Equals(loadout.Format, "mx.modLoadout.v1", StringComparison.Ordinal))
                throw new InvalidOperationException($"Loadout format mismatch: expected 'mx.modLoadout.v1', got '{loadout.Format}'.");
        }

        var ordered = new List<CliCatalogItem>();
        var skipped = new List<CliCatalogItem>();
        var planWarnings = new List<string>();

        if (loadout != null)
        {
            var enabledKeys = new HashSet<string>(loadout.EnabledPackageKeys ?? new List<string>(), StringComparer.Ordinal);
            foreach (var item in catalogItems)
            {
                if (!item.IsValid)
                {
                    skipped.Add(item);
                    continue;
                }
                if (enabledKeys.Contains(item.PackageKey))
                    ordered.Add(item);
                else
                    skipped.Add(item);
            }

            var knownKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in catalogItems)
                knownKeys.Add(item.PackageKey);
            foreach (string key in enabledKeys)
            {
                if (!knownKeys.Contains(key))
                    planWarnings.Add($"Missing loadout package key '{key}' — not found in any container.");
            }
        }
        else
        {
            foreach (var item in catalogItems)
            {
                if (item.IsValid)
                    ordered.Add(item);
                else
                    skipped.Add(item);
            }
        }

        ordered.Sort((a, b) =>
        {
            int kindOrder = string.Compare(a.Kind, b.Kind, StringComparison.Ordinal);
            if (kindOrder != 0) return kindOrder;
            int idOrder = string.Compare(a.PackageId, b.PackageId, StringComparison.Ordinal);
            if (idOrder != 0) return idOrder;
            return string.Compare(a.PackageRootPath ?? string.Empty, b.PackageRootPath ?? string.Empty, StringComparison.Ordinal);
        });
        skipped.Sort((a, b) => string.Compare(a.PackageKey ?? string.Empty, b.PackageKey ?? string.Empty, StringComparison.Ordinal));

        var loadPlanItems = new List<ModDiagnosticLoadPlanItemDto>();
        for (int i = 0; i < ordered.Count; i++)
        {
            loadPlanItems.Add(new ModDiagnosticLoadPlanItemDto
            {
                PackageKey = ordered[i].PackageKey,
                PackageId = ordered[i].PackageId,
                State = "Ordered",
                SkipReason = string.Empty,
                OrderIndex = i
            });
        }
        for (int i = 0; i < skipped.Count; i++)
        {
            string reason = skipped[i].IsValid ? "Disabled" : "Invalid";
            loadPlanItems.Add(new ModDiagnosticLoadPlanItemDto
            {
                PackageKey = skipped[i].PackageKey,
                PackageId = skipped[i].PackageId,
                State = "Skipped",
                SkipReason = reason,
                OrderIndex = -1
            });
        }

        var mergeErrors = new List<ModDiagnosticIssueDto>();
        var mergeWarnings = new List<ModDiagnosticIssueDto>();
        var overrideRecords = new List<ModDiagnosticOverrideDto>();
        bool mergeSuccess = true;

        for (int i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            if (string.IsNullOrEmpty(item.RuntimePatchPath) || !File.Exists(item.RuntimePatchPath))
                continue;

            try
            {
                string patchJson = File.ReadAllText(item.RuntimePatchPath);
                using var doc = JsonDocument.Parse(patchJson);
                var root = doc.RootElement;

                string format = root.TryGetProperty("format", out var fmtEl) ? fmtEl.GetString() ?? "" : "";
                if (format != "mx.runtimeConfigPatch.v1")
                {
                    mergeErrors.Add(new ModDiagnosticIssueDto
                    {
                        Severity = "Error",
                        Code = "MergeFormatError",
                        Source = "merge",
                        Message = $"Package '{item.PackageKey}' patch format is '{format}', expected 'mx.runtimeConfigPatch.v1'."
                    });
                    mergeSuccess = false;
                    continue;
                }

                if (root.TryGetProperty("modifiers", out var modsEl) && modsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in modsEl.EnumerateArray())
                    {
                        if (!entry.TryGetProperty("id", out var idEl)) continue;
                        overrideRecords.Add(new ModDiagnosticOverrideDto
                        {
                            ConfigType = "ModifierConfig",
                            Id = idEl.GetInt32(),
                            PackageChain = new List<string> { item.PackageId },
                            WinnerPackageKey = item.PackageKey,
                            WinnerPackageId = item.PackageId
                        });
                    }
                }

                if (root.TryGetProperty("buffs", out var buffsEl) && buffsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in buffsEl.EnumerateArray())
                    {
                        if (!entry.TryGetProperty("id", out var idEl)) continue;
                        overrideRecords.Add(new ModDiagnosticOverrideDto
                        {
                            ConfigType = "BuffConfig",
                            Id = idEl.GetInt32(),
                            PackageChain = new List<string> { item.PackageId },
                            WinnerPackageKey = item.PackageKey,
                            WinnerPackageId = item.PackageId
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                mergeErrors.Add(new ModDiagnosticIssueDto
                {
                    Severity = "Error",
                    Code = "MergeError",
                    Source = "merge",
                    Message = $"Failed to process patch for package '{item.PackageKey}': {ex.Message}"
                });
                mergeSuccess = false;
            }
        }

        var overrideGroups = new Dictionary<string, List<ModDiagnosticOverrideDto>>(StringComparer.Ordinal);
        foreach (var ov in overrideRecords)
        {
            string key = ov.ConfigType + ":" + ov.Id;
            if (!overrideGroups.TryGetValue(key, out var group))
            {
                group = new List<ModDiagnosticOverrideDto>();
                overrideGroups[key] = group;
            }
            group.Add(ov);
        }

        var finalOverrides = new List<ModDiagnosticOverrideDto>();
        foreach (var kvp in overrideGroups)
        {
            var group = kvp.Value;
            if (group.Count <= 1)
                continue;

            var winner = group[group.Count - 1];
            var chain = new List<string>();
            foreach (var g in group) chain.Add(g.PackageChain[0]);

            finalOverrides.Add(new ModDiagnosticOverrideDto
            {
                ConfigType = group[0].ConfigType,
                Id = group[0].Id,
                PackageChain = chain,
                WinnerPackageKey = winner.WinnerPackageKey,
                WinnerPackageId = winner.WinnerPackageId
            });
        }

        foreach (string pw in planWarnings)
        {
            mergeWarnings.Add(new ModDiagnosticIssueDto
            {
                Severity = "Warning",
                Code = "LoadPlanWarning",
                Source = "loadPlan",
                Message = pw
            });
        }

        var enabledKeysSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in ordered)
            enabledKeysSet.Add(item.PackageKey);

        var packageSummaries = new List<ModDiagnosticPackageSummaryDto>();
        foreach (var item in catalogItems)
        {
            packageSummaries.Add(new ModDiagnosticPackageSummaryDto
            {
                PackageKey = item.PackageKey,
                PackageId = item.PackageId,
                DisplayName = item.DisplayName,
                Version = item.Version,
                Kind = item.Kind,
                ContainerPath = includeAbsolutePaths ? (item.ContainerPath ?? string.Empty) : string.Empty,
                PackageRelativePath = item.PackageRelativePath ?? string.Empty,
                IsValid = item.IsValid,
                IsEnabled = enabledKeysSet.Contains(item.PackageKey),
                Errors = item.Errors ?? new List<string>(),
                Warnings = item.Warnings ?? new List<string>()
            });
        }

        packageSummaries.Sort((a, b) => string.Compare(a.PackageKey, b.PackageKey, StringComparison.Ordinal));
        finalOverrides.Sort((a, b) =>
        {
            int typeOrder = string.Compare(a.ConfigType, b.ConfigType, StringComparison.Ordinal);
            if (typeOrder != 0) return typeOrder;
            return a.Id.CompareTo(b.Id);
        });

        int discovered = catalogItems.Count;
        int valid = 0;
        foreach (var item in catalogItems)
        {
            if (item.IsValid)
                valid++;
        }

        return new ModDiagnosticSnapshotDto
        {
            Format = ModDiagnosticSnapshotDto.ExpectedFormat,
            GeneratedUtc = DateTime.UtcNow.ToString("O"),
            Success = mergeSuccess && mergeErrors.Count == 0,
            Summary = new ModDiagnosticSummaryDto
            {
                Discovered = discovered,
                Valid = valid,
                Invalid = discovered - valid,
                Enabled = ordered.Count,
                Ordered = ordered.Count,
                Skipped = skipped.Count,
                Overrides = finalOverrides.Count,
                Errors = mergeErrors.Count,
                Warnings = mergeWarnings.Count
            },
            Loadout = new ModDiagnosticLoadoutSummaryDto
            {
                IsDefaultAll = loadout == null,
                ProfileId = loadout?.ProfileId ?? "default-all",
                DisplayName = loadout?.DisplayName ?? "Default (all valid packages)",
                EnabledPackageKeys = loadout?.EnabledPackageKeys ?? new List<string>()
            },
            Packages = packageSummaries,
            LoadPlan = loadPlanItems,
            Overrides = finalOverrides,
            Errors = mergeErrors,
            Warnings = mergeWarnings
        };
    }

    private sealed class CliCatalogItem
    {
        public string PackageRootPath { get; set; } = string.Empty;
        public string PackageKey { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string ContainerPath { get; set; } = string.Empty;
        public string PackageRelativePath { get; set; } = string.Empty;
        public string RuntimePatchPath { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    private sealed class CliLoadout
    {
        public string Format { get; set; } = string.Empty;
        public string ProfileId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> EnabledPackageKeys { get; set; } = new();
    }

    private sealed class CliModManifest
    {
        public int SchemaVersion { get; set; }
        public string PackageId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string RuntimePatch { get; set; } = string.Empty;
    }

    private static CliCatalogItem LoadCatalogItem(string packageRootPath, string containerPath, string modJsonPath)
    {
        var item = new CliCatalogItem
        {
            PackageRootPath = packageRootPath,
            ContainerPath = containerPath
        };

        string relativePath;
        try
        {
            relativePath = Path.GetRelativePath(containerPath, packageRootPath).Replace('\\', '/');
        }
        catch
        {
            relativePath = packageRootPath.Replace('\\', '/');
            item.Warnings.Add($"Package key path is not container-relative for '{packageRootPath}'.");
        }
        item.PackageRelativePath = relativePath;

        try
        {
            string modJson = File.ReadAllText(modJsonPath);
            var manifest = JsonSerializer.Deserialize<CliModManifest>(modJson, CreateCliJsonOptions());
            if (manifest == null)
            {
                item.IsValid = false;
                item.Errors.Add("mod.json parsed to null.");
                item.PackageId = Path.GetFileName(packageRootPath);
                item.PackageKey = item.PackageId + "|" + relativePath;
                return item;
            }

            item.PackageId = manifest.PackageId ?? string.Empty;
            item.DisplayName = manifest.DisplayName ?? string.Empty;
            item.Version = manifest.Version ?? string.Empty;
            item.Kind = manifest.Kind ?? string.Empty;

            if (string.IsNullOrWhiteSpace(item.PackageId))
                item.Errors.Add("packageId is required in mod.json.");
            if (manifest.SchemaVersion != 1)
                item.Errors.Add($"schemaVersion must be 1, got {manifest.SchemaVersion}.");

            item.PackageKey = (item.PackageId ?? string.Empty) + "|" + relativePath;

            if (!string.IsNullOrWhiteSpace(manifest.RuntimePatch))
            {
                string patchRel = manifest.RuntimePatch.Replace('\\', '/');
                if (Path.IsPathRooted(patchRel))
                {
                    item.Errors.Add($"runtimePatch must be a relative path, got absolute: '{patchRel}'.");
                }
                else
                {
                    string patchFull = Path.GetFullPath(Path.Combine(packageRootPath, patchRel));
                    string normalizedRoot = Path.GetFullPath(packageRootPath) + Path.DirectorySeparatorChar;
                    if (!patchFull.StartsWith(normalizedRoot, StringComparison.Ordinal))
                    {
                        item.Errors.Add($"runtimePatch '{patchRel}' resolves outside package root.");
                    }
                    else if (!File.Exists(patchFull))
                    {
                        item.Warnings.Add($"Runtime patch file not found: '{patchRel}'.");
                    }
                    else
                    {
                        item.RuntimePatchPath = patchFull;
                    }
                }
            }

            item.IsValid = item.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            item.IsValid = false;
            item.Errors.Add($"Failed to parse mod.json: {ex.Message}");
            item.PackageId = Path.GetFileName(packageRootPath);
            item.PackageKey = item.PackageId + "|" + relativePath;
        }

        return item;
    }

    private static JsonSerializerOptions CreateCliJsonOptions()
    {
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        opts.Converters.Add(new JsonStringEnumConverter());
        return opts;
    }
}

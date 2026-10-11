using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Config.Runtime
{
    /// <summary>
    /// Discovers Mod Packages from container directories.
    /// Scans each container's immediate subdirectories for mod.json,
    /// validates each candidate via <see cref="RuntimeModPackageLoader.TryLoadFromDirectory"/>,
    /// and produces a flat <see cref="RuntimeModPackageCatalog"/>.
    ///
    /// Pure runtime logic — no UnityEditor or Authoring Core dependencies.
    /// </summary>
    public static class RuntimeModPackageDiscovery
    {
        /// <summary>
        /// Discover packages from one or more container directories.
        /// All containers are treated as peers — there is no implicit layering by container path.
        /// </summary>
        /// <param name="packageContainerPaths">
        /// Absolute paths to container directories whose immediate subdirectories will be scanned.
        /// Non-existent containers are silently ignored (empty result, not an error).
        /// </param>
        /// <returns>A catalog with discovered items. Never null.</returns>
        public static RuntimeModPackageCatalog Discover(IEnumerable<string> packageContainerPaths)
        {
            if (packageContainerPaths == null)
                return new RuntimeModPackageCatalog(Array.Empty<RuntimeModPackageCatalogItem>());

            var items = new List<RuntimeModPackageCatalogItem>();

            foreach (string containerPath in packageContainerPaths)
            {
                if (string.IsNullOrWhiteSpace(containerPath))
                    continue;

                string resolvedContainer;
                try
                {
                    resolvedContainer = Path.GetFullPath(containerPath);
                }
                catch
                {
                    // Invalid path characters — skip
                    continue;
                }

                if (!Directory.Exists(resolvedContainer))
                    continue;

                string[] subDirectories;
                try
                {
                    subDirectories = Directory.GetDirectories(resolvedContainer);
                }
                catch
                {
                    // Permission or I/O error — skip this container
                    continue;
                }

                for (int i = 0; i < subDirectories.Length; i++)
                {
                    string subDir = subDirectories[i];
                    string packageRelativePath = BuildPortableRelativePath(resolvedContainer, subDir, out bool portableRelativePath);

                    // Only consider directories that contain mod.json
                    string modJsonPath = Path.Combine(subDir, "mod.json");
                    if (!File.Exists(modJsonPath))
                        continue;

                    // Attempt to load via Try API — never throws
                    if (RuntimeModPackageLoader.TryLoadFromDirectory(subDir, out var result, out var error))
                    {
                        items.Add(new RuntimeModPackageCatalogItem(
                            packageRootPath: subDir,
                            manifest: result.Manifest,
                            isValid: true,
                            errors: Array.Empty<string>(),
                            warnings: BuildWarnings(null, portableRelativePath, subDir),
                            containerPath: resolvedContainer,
                            packageRelativePath: packageRelativePath,
                            packageKey: BuildPackageKey(result.Manifest, packageRelativePath)));
                    }
                    else
                    {
                        // Even failed items go into catalog with IsValid=false and error info
                        string subDirName = Path.GetFileName(subDir);

                        // Attempt to read manifest partially for identity
                        RuntimeModPackageManifest partialManifest = TryReadPartialManifest(modJsonPath);
                        if (partialManifest == null)
                        {
                            partialManifest = new RuntimeModPackageManifest
                            {
                                PackageId = subDirName,
                                DisplayName = subDirName
                            };
                        }

                        items.Add(new RuntimeModPackageCatalogItem(
                            packageRootPath: subDir,
                            manifest: partialManifest,
                            isValid: false,
                            errors: new[] { error?.Message ?? "Unknown error" },
                            warnings: BuildWarnings(Array.Empty<string>(), portableRelativePath, subDir),
                            containerPath: resolvedContainer,
                            packageRelativePath: packageRelativePath,
                            packageKey: BuildPackageKey(partialManifest, packageRelativePath)));
                    }
                }
            }

            return new RuntimeModPackageCatalog(items);
        }

        /// <summary>
        /// Try to read mod.json for identity even when full validation fails.
        /// Returns null if JSON is unparseable.
        /// </summary>
        private static RuntimeModPackageManifest TryReadPartialManifest(string modJsonPath)
        {
            try
            {
                string json = File.ReadAllText(modJsonPath);
                var manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<RuntimeModPackageManifest>(json);
                return manifest;
            }
            catch
            {
                return null;
            }
        }

        private static string BuildPortableRelativePath(string containerRootPath, string packageRootPath, out bool isPortable)
        {
            isPortable = true;
            try
            {
                string relative = Path.GetRelativePath(containerRootPath, packageRootPath);
                if (string.IsNullOrWhiteSpace(relative))
                {
                    isPortable = false;
                    return NormalizePathSlashes(packageRootPath);
                }

                string normalizedRelative = NormalizePathSlashes(relative);
                if (normalizedRelative.StartsWith("../", StringComparison.Ordinal))
                {
                    isPortable = false;
                    return NormalizePathSlashes(packageRootPath);
                }

                return normalizedRelative;
            }
            catch
            {
                isPortable = false;
                return NormalizePathSlashes(packageRootPath);
            }
        }

        private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<string> existingWarnings, bool isPortableRelativePath, string packageRootPath)
        {
            var warnings = existingWarnings == null
                ? new List<string>()
                : new List<string>(existingWarnings);

            if (!isPortableRelativePath)
            {
                warnings.Add(
                    $"Package key path is not container-relative for '{packageRootPath}'. " +
                    "Using normalized package root fallback; this key is not portable across machines.");
            }

            return warnings;
        }

        private static string BuildPackageKey(RuntimeModPackageManifest manifest, string packageRelativePath)
        {
            string packageId = manifest?.PackageId ?? string.Empty;
            return packageId + "|" + (packageRelativePath ?? string.Empty);
        }

        private static string NormalizePathSlashes(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}

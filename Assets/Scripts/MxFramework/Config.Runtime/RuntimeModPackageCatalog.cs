using System;
using System.Collections.Generic;

namespace MxFramework.Config.Runtime
{
    /// <summary>
    /// Catalog of discovered Mod Packages.
    /// Produced by <see cref="RuntimeModPackageDiscovery.Discover"/>.
    /// All items from all container directories are flattened into a single list.
    /// </summary>
    public sealed class RuntimeModPackageCatalog
    {
        public RuntimeModPackageCatalog(IReadOnlyList<RuntimeModPackageCatalogItem> items)
        {
            Items = items ?? Array.Empty<RuntimeModPackageCatalogItem>();
        }

        /// <summary>All discovered packages, including invalid ones.</summary>
        public IReadOnlyList<RuntimeModPackageCatalogItem> Items { get; }
    }

    /// <summary>
    /// A single package entry in the catalog, with validation state.
    /// </summary>
    public sealed class RuntimeModPackageCatalogItem
    {
        public RuntimeModPackageCatalogItem(
            string packageRootPath,
            RuntimeModPackageManifest manifest,
            bool isValid,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings)
            : this(
                packageRootPath: packageRootPath,
                manifest: manifest,
                isValid: isValid,
                errors: errors,
                warnings: warnings,
                containerPath: string.Empty,
                packageRelativePath: string.Empty,
                packageKey: string.Empty)
        {
        }

        public RuntimeModPackageCatalogItem(
            string packageRootPath,
            RuntimeModPackageManifest manifest,
            bool isValid,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings,
            string containerPath,
            string packageRelativePath,
            string packageKey)
        {
            PackageRootPath = packageRootPath ?? string.Empty;
            Manifest = manifest;
            IsValid = isValid;
            Errors = errors ?? Array.Empty<string>();
            Warnings = warnings ?? Array.Empty<string>();
            ContainerPath = containerPath ?? string.Empty;
            PackageRelativePath = packageRelativePath ?? string.Empty;
            PackageKey = packageKey ?? string.Empty;
        }

        /// <summary>Absolute path to the package root directory.</summary>
        public string PackageRootPath { get; }

        /// <summary>Parsed manifest (may be null for invalid packages where mod.json couldn't be read).</summary>
        public RuntimeModPackageManifest Manifest { get; }

        /// <summary>True if manifest parsed and all validation checks passed.</summary>
        public bool IsValid { get; }

        /// <summary>Blocking errors that caused this item to be invalid.</summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>Non-blocking warnings (e.g. duplicate packageId).</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>Resolved package container root path used during discovery.</summary>
        public string ContainerPath { get; }

        /// <summary>
        /// Relative package path under container root, normalized with '/'.
        /// Can be empty when the path is not portable.
        /// </summary>
        public string PackageRelativePath { get; }

        /// <summary>
        /// Stable package key for loadout selection:
        /// packageId + "|" + normalized container-relative path.
        /// </summary>
        public string PackageKey { get; }

        /// <summary>
        /// Package identity string for display purposes.
        /// Falls back to directory name if manifest is null.
        /// </summary>
        public string DisplayIdentity
        {
            get
            {
                if (Manifest != null && !string.IsNullOrWhiteSpace(Manifest.PackageId))
                    return Manifest.PackageId;
                int idx = PackageRootPath.LastIndexOfAny(new[] { '/', '\\' });
                return idx >= 0 ? PackageRootPath.Substring(idx + 1) : PackageRootPath;
            }
        }
    }
}

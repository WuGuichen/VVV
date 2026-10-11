using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Config.Runtime
{
    /// <summary>
    /// Result of loading a Mod Package from a directory.
    /// Contains the parsed manifest, resolved paths, and the runtime patch bundle.
    /// </summary>
    public sealed class RuntimeModPackageLoadResult
    {
        public RuntimeModPackageLoadResult(
            RuntimeModPackageManifest manifest,
            string packageRootPath,
            string runtimePatchFilePath,
            RuntimeConfigPatchBundle patchBundle,
            string resourceCatalogFilePath = "")
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            PackageRootPath = packageRootPath ?? throw new ArgumentNullException(nameof(packageRootPath));
            RuntimePatchFilePath = runtimePatchFilePath ?? throw new ArgumentNullException(nameof(runtimePatchFilePath));
            PatchBundle = patchBundle ?? throw new ArgumentNullException(nameof(patchBundle));
            ResourceCatalogFilePath = resourceCatalogFilePath ?? string.Empty;
        }

        /// <summary>Parsed mod.json manifest.</summary>
        public RuntimeModPackageManifest Manifest { get; }

        /// <summary>Absolute path to the package root directory.</summary>
        public string PackageRootPath { get; }

        /// <summary>Absolute path to the resolved runtime config patch file.</summary>
        public string RuntimePatchFilePath { get; }

        /// <summary>Parsed runtime config patch bundle (buff + modifier patches).</summary>
        public RuntimeConfigPatchBundle PatchBundle { get; }

        /// <summary>Absolute path to the optional resource catalog file, or empty when the package does not declare one.</summary>
        public string ResourceCatalogFilePath { get; }

        /// <summary>True when the package declares and validates a resource catalog file.</summary>
        public bool HasResourceCatalog => !string.IsNullOrWhiteSpace(ResourceCatalogFilePath);
    }

    /// <summary>
    /// Exception thrown when a Mod Package fails to load or validate.
    /// Message is suitable for direct display in demo UI or preview logs.
    /// </summary>
    public sealed class RuntimeModPackageLoadException : Exception
    {
        public RuntimeModPackageLoadException(string message) : base(message) { }
        public RuntimeModPackageLoadException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Structured error result for <see cref="RuntimeModPackageLoader.TryLoadFromDirectory"/>.
    /// </summary>
    public sealed class RuntimeModPackageLoadError
    {
        public RuntimeModPackageLoadError(string packageRootPath, string message)
        {
            PackageRootPath = packageRootPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>The package root path that failed to load.</summary>
        public string PackageRootPath { get; }

        /// <summary>Human-readable error message.</summary>
        public string Message { get; }
    }

    /// <summary>
    /// Loads and validates a Mod Package from a directory on disk.
    /// Pure runtime logic — no UnityEditor or Authoring Core dependencies.
    /// Uses Newtonsoft.Json for JSON parsing.
    /// </summary>
    public static class RuntimeModPackageLoader
    {
        /// <summary>
        /// Load a Mod Package from the given directory path.
        /// </summary>
        /// <param name="packageRootPath">Absolute path to the package root directory (containing mod.json).</param>
        /// <returns>A fully resolved load result with parsed manifest and patch bundle.</returns>
        /// <exception cref="RuntimeModPackageLoadException">On any validation or loading failure.</exception>
        public static RuntimeModPackageLoadResult LoadFromDirectory(string packageRootPath)
        {
            if (string.IsNullOrWhiteSpace(packageRootPath))
                throw new RuntimeModPackageLoadException("Package root path is null or empty.");

            string resolvedRoot = Path.GetFullPath(packageRootPath);

            // 1. Read mod.json
            string manifestPath = Path.Combine(resolvedRoot, "mod.json");
            if (!File.Exists(manifestPath))
                throw new RuntimeModPackageLoadException($"mod.json not found at '{manifestPath}'.");

            RuntimeModPackageManifest manifest;
            try
            {
                string manifestJson = File.ReadAllText(manifestPath);
                manifest = JsonConvert.DeserializeObject<RuntimeModPackageManifest>(manifestJson);
            }
            catch (JsonException ex)
            {
                throw new RuntimeModPackageLoadException($"Failed to parse mod.json: {ex.Message}", ex);
            }

            if (manifest == null)
                throw new RuntimeModPackageLoadException("mod.json parsed to null.");

            // 2. Validate schemaVersion
            if (manifest.SchemaVersion != 1)
                throw new RuntimeModPackageLoadException(
                    $"schemaVersion must be 1, but got {manifest.SchemaVersion}.");

            // 3. Validate kind
            string kind = manifest.Kind;
            if (string.IsNullOrWhiteSpace(kind))
                throw new RuntimeModPackageLoadException("kind is required in mod.json.");
            if (kind != "Preview" && kind != "Mod")
                throw new RuntimeModPackageLoadException($"kind must be 'Preview' or 'Mod', but got '{kind}'.");

            // 4. Validate runtimePatch is specified
            if (string.IsNullOrWhiteSpace(manifest.RuntimePatch))
                throw new RuntimeModPackageLoadException("runtimePatch is required in mod.json.");

            string rawPatchPath = manifest.RuntimePatch.Trim();

            string normalizedRoot = resolvedRoot.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? resolvedRoot
                : resolvedRoot + Path.DirectorySeparatorChar;
            string resolvedPatchPath = ResolvePackageFilePath(
                resolvedRoot,
                normalizedRoot,
                rawPatchPath,
                "runtimePatch",
                required: true);
            string resolvedResourceCatalogPath = ResolvePackageFilePath(
                resolvedRoot,
                normalizedRoot,
                manifest.ResourceCatalog,
                "resourceCatalog",
                required: false);

            // 5. Load and parse the runtime patch JSON
            RuntimeConfigPatchBundle patchBundle;
            string patchJson;
            try
            {
                patchJson = File.ReadAllText(resolvedPatchPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new RuntimeModPackageLoadException($"Failed to read Runtime Patch file: {ex.Message}", ex);
            }

            // Parse to get format and layer before full loading
            try
            {
                JObject patchRoot = JObject.Parse(patchJson);

                // Validate format
                string actualFormat = patchRoot.Value<string>("format") ?? string.Empty;
                if (actualFormat != "mx.runtimeConfigPatch.v1")
                    throw new RuntimeModPackageLoadException(
                        $"Runtime Patch format must be 'mx.runtimeConfigPatch.v1', but got '{actualFormat}'.");

                // Validate kind-layer match
                string actualLayer = patchRoot.Value<string>("layer") ?? string.Empty;
                if (kind == "Mod" && actualLayer != "Mod")
                    throw new RuntimeModPackageLoadException(
                        $"Package kind is 'Mod' but Runtime Patch layer is '{actualLayer}'; expected 'Mod'.");
                if (kind == "Preview" && actualLayer != "Patch")
                    throw new RuntimeModPackageLoadException(
                        $"Package kind is 'Preview' but Runtime Patch layer is '{actualLayer}'; expected 'Patch'.");

                // Full parse via existing loader
                patchBundle = RuntimeConfigPatchJsonLoader.Load(patchJson);
            }
            catch (JsonException ex)
            {
                throw new RuntimeModPackageLoadException($"Failed to parse Runtime Patch JSON: {ex.Message}", ex);
            }
            catch (RuntimeConfigPatchParseException ex)
            {
                throw new RuntimeModPackageLoadException($"Runtime Patch parse error: {ex.Message}", ex);
            }

            return new RuntimeModPackageLoadResult(
                manifest,
                resolvedRoot,
                resolvedPatchPath,
                patchBundle,
                resolvedResourceCatalogPath);
        }

        /// <summary>
        /// Non-throwing version of <see cref="LoadFromDirectory"/>.
        /// Returns false on validation issues instead of throwing.
        /// Discovery callers MUST use this — do NOT wrap <see cref="LoadFromDirectory"/> in try/catch
        /// as the primary control flow.
        /// </summary>
        public static bool TryLoadFromDirectory(
            string packageRootPath,
            out RuntimeModPackageLoadResult result,
            out RuntimeModPackageLoadError error)
        {
            try
            {
                result = LoadFromDirectory(packageRootPath);
                error = null;
                return true;
            }
            catch (RuntimeModPackageLoadException ex)
            {
                result = null;
                error = new RuntimeModPackageLoadError(packageRootPath, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Determines the <see cref="ConfigLayerKind"/> for a package's runtime patch
        /// based on the manifest kind.
        /// </summary>
        public static ConfigLayerKind GetLayerKind(RuntimeModPackageManifest manifest)
        {
            if (manifest == null)
                return ConfigLayerKind.Patch;

            return manifest.Kind == "Mod" ? ConfigLayerKind.Mod : ConfigLayerKind.Patch;
        }

        private static string ResolvePackageFilePath(
            string packageRootPath,
            string normalizedRoot,
            string rawPath,
            string fieldName,
            bool required)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                if (required)
                    throw new RuntimeModPackageLoadException(fieldName + " is required in mod.json.");
                return string.Empty;
            }

            string trimmedPath = rawPath.Trim();
            if (Path.IsPathRooted(trimmedPath))
            {
                throw new RuntimeModPackageLoadException(
                    fieldName + " must be a relative path, but got absolute: '" + trimmedPath + "'.");
            }

            string resolvedPath = Path.GetFullPath(Path.Combine(packageRootPath, trimmedPath));
            if (!resolvedPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
            {
                throw new RuntimeModPackageLoadException(
                    fieldName + " '" + trimmedPath + "' resolves outside the package root.");
            }

            if (!File.Exists(resolvedPath))
            {
                throw new RuntimeModPackageLoadException(
                    fieldName + " file not found: '" + trimmedPath + "' (resolved: " + resolvedPath + ").");
            }

            return resolvedPath;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring.FairyGuiArtSlices
{
    public static class UiArtResourceLibraryService
    {
        public const string DefaultLibraryId = "MxUiFoundation";
        public const string AuthoringApiVersion = "mx.uiArtResources.authoring.v1";
        public const string UiKitsRootPath = "ArtSource/UIKits";
        public const string DefaultLibraryRootPath = "ArtSource/UIKits/MxUiFoundation";
        public const string DefaultCatalogPath = "ArtSource/UIKits/MxUiFoundation/ui-art-resources.json";

        private const string CurrentSchemaVersion = "mx.uiArtResourceLibrary.v1";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
            WriteIndented = true
        };

        public static IReadOnlyList<UiArtResourceLibraryDescriptor> ListLibraries(string repositoryRoot)
        {
            var libraries = new List<UiArtResourceLibraryDescriptor>();
            AddKnownLibrary(repositoryRoot, libraries, DefaultLibraryId, DefaultLibraryRootPath, DefaultCatalogPath);

            string uiKitsRoot = ResolvePath(repositoryRoot, UiKitsRootPath);
            if (Directory.Exists(uiKitsRoot))
            {
                string[] catalogs = Directory.GetFiles(uiKitsRoot, "ui-art-resources.json", SearchOption.AllDirectories);
                for (int i = 0; i < catalogs.Length; i++)
                {
                    string catalogPath = ToRepositoryRelativePath(repositoryRoot, catalogs[i]);
                    UiArtResourceLibraryCatalog catalog = TryReadCatalogFile(repositoryRoot, catalogPath);
                    string rootPath = catalog != null && !string.IsNullOrWhiteSpace(catalog.ResourceRoot)
                        ? NormalizePath(catalog.ResourceRoot)
                        : NormalizePath(Path.GetDirectoryName(catalogPath) ?? string.Empty);
                    string libraryId = catalog != null && !string.IsNullOrWhiteSpace(catalog.LibraryId)
                        ? catalog.LibraryId
                        : Path.GetFileName(rootPath);
                    AddKnownLibrary(repositoryRoot, libraries, libraryId, rootPath, catalogPath);
                }
            }

            libraries.Sort(CompareLibraryDescriptors);
            return libraries;
        }

        public static UiArtResourceLibraryDescriptor ResolveLibraryDescriptor(string repositoryRoot, string library)
        {
            string normalized = NormalizePath(library);
            if (string.IsNullOrWhiteSpace(normalized)
                || string.Equals(normalized, DefaultLibraryId, StringComparison.Ordinal)
                || string.Equals(normalized, DefaultLibraryRootPath, StringComparison.Ordinal)
                || string.Equals(normalized, DefaultCatalogPath, StringComparison.Ordinal))
            {
                return new UiArtResourceLibraryDescriptor(DefaultLibraryId, DefaultLibraryRootPath, DefaultCatalogPath, "Mx UI Foundation");
            }

            IReadOnlyList<UiArtResourceLibraryDescriptor> libraries = ListLibraries(repositoryRoot);
            for (int i = 0; i < libraries.Count; i++)
            {
                UiArtResourceLibraryDescriptor item = libraries[i];
                if (string.Equals(item.LibraryId, normalized, StringComparison.Ordinal)
                    || string.Equals(item.ResourceRoot, normalized, StringComparison.Ordinal)
                    || string.Equals(item.CatalogPath, normalized, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            if (normalized.EndsWith("/ui-art-resources.json", StringComparison.Ordinal))
            {
                EnsureAllowedLibraryCatalogPath(repositoryRoot, normalized);
                UiArtResourceLibraryCatalog catalog = TryReadCatalogFile(repositoryRoot, normalized);
                string rootPath = catalog != null && !string.IsNullOrWhiteSpace(catalog.ResourceRoot)
                    ? NormalizePath(catalog.ResourceRoot)
                    : NormalizePath(Path.GetDirectoryName(normalized) ?? string.Empty);
                EnsureAllowedLibraryRootPath(repositoryRoot, rootPath);
                string libraryId = catalog != null && !string.IsNullOrWhiteSpace(catalog.LibraryId)
                    ? catalog.LibraryId
                    : Path.GetFileName(rootPath);
                return new UiArtResourceLibraryDescriptor(libraryId, rootPath, normalized, libraryId);
            }

            string catalogPath = NormalizePath(normalized.TrimEnd('/') + "/ui-art-resources.json");
            EnsureAllowedLibraryCatalogPath(repositoryRoot, catalogPath);
            if (File.Exists(ResolvePath(repositoryRoot, catalogPath)))
            {
                UiArtResourceLibraryCatalog catalog = TryReadCatalogFile(repositoryRoot, catalogPath);
                string libraryId = catalog != null && !string.IsNullOrWhiteSpace(catalog.LibraryId)
                    ? catalog.LibraryId
                    : Path.GetFileName(normalized);
                string rootPath = catalog != null && !string.IsNullOrWhiteSpace(catalog.ResourceRoot)
                    ? NormalizePath(catalog.ResourceRoot)
                    : normalized;
                EnsureAllowedLibraryRootPath(repositoryRoot, rootPath);
                return new UiArtResourceLibraryDescriptor(libraryId, rootPath, catalogPath, libraryId);
            }

            throw new InvalidOperationException("Unknown UI art resource library: " + library + ".");
        }

        public static UiArtResourceLibraryAuthoringState ReadAuthoringState(string repositoryRoot, string library)
        {
            UiArtResourceLibraryDescriptor descriptor = ResolveLibraryDescriptor(repositoryRoot, library);
            UiArtResourceLibraryValidationResult validation = ValidateLibrary(repositoryRoot, descriptor.CatalogPath);
            UiArtResourceLibraryCatalog catalog = ReadCatalogFile(repositoryRoot, descriptor.CatalogPath);
            return new UiArtResourceLibraryAuthoringState(
                descriptor,
                ComputeCatalogHash(repositoryRoot, descriptor.CatalogPath),
                catalog,
                validation,
                GenerateMarkdownReport(validation));
        }

        public static UiArtResourceLibraryValidationResult ValidateLibrary(string repositoryRoot, string catalogPath)
        {
            string normalizedCatalogPath = NormalizePath(catalogPath);
            UiArtResourceLibraryDescriptor descriptor = ResolveLibraryDescriptor(repositoryRoot, normalizedCatalogPath);
            var diagnostics = new List<UiArtResourceDiagnostic>();
            UiArtResourceLibraryCatalog catalog = LoadCatalog(repositoryRoot, descriptor.CatalogPath, diagnostics);
            string libraryId = catalog != null && !string.IsNullOrWhiteSpace(catalog.LibraryId) ? catalog.LibraryId : descriptor.LibraryId;
            string resourceRoot = catalog != null && !string.IsNullOrWhiteSpace(catalog.ResourceRoot) ? NormalizePath(catalog.ResourceRoot) : descriptor.ResourceRoot;

            if (catalog != null)
                ValidateCatalog(repositoryRoot, descriptor, catalog, resourceRoot, diagnostics);

            SortDiagnostics(diagnostics);
            UiArtResource[] resources = catalog != null && catalog.Resources != null ? catalog.Resources : Array.Empty<UiArtResource>();
            return new UiArtResourceLibraryValidationResult(libraryId, resourceRoot, resources, diagnostics);
        }

        public static UiArtResourceLibraryCatalog ReadCatalogFile(string repositoryRoot, string catalogPath)
        {
            EnsureAllowedLibraryCatalogPath(repositoryRoot, catalogPath);
            string absolutePath = ResolvePath(repositoryRoot, catalogPath);
            string text = File.ReadAllText(absolutePath);
            UiArtResourceLibraryCatalog catalog = JsonSerializer.Deserialize<UiArtResourceLibraryCatalog>(text, JsonOptions);
            if (catalog == null)
                throw new InvalidOperationException("UI art resource catalog JSON is empty.");

            return catalog;
        }

        public static UiArtResourceLibraryCatalog TryReadCatalogFile(string repositoryRoot, string catalogPath)
        {
            EnsureAllowedLibraryCatalogPath(repositoryRoot, catalogPath);
            string absolutePath = ResolvePath(repositoryRoot, catalogPath);
            if (!File.Exists(absolutePath))
                return null;

            try
            {
                return ReadCatalogFile(repositoryRoot, catalogPath);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static void WriteCatalogFile(string repositoryRoot, string catalogPath, UiArtResourceLibraryCatalog catalog)
        {
            EnsureAllowedLibraryCatalogPath(repositoryRoot, catalogPath);
            string absolutePath = ResolvePath(repositoryRoot, catalogPath);
            EnsurePathUnderRoot(repositoryRoot, absolutePath);
            WriteTextAtomic(absolutePath, SerializeCatalog(catalog));
        }

        public static string SerializeCatalog(UiArtResourceLibraryCatalog catalog)
        {
            return JsonSerializer.Serialize(catalog, JsonOptions) + Environment.NewLine;
        }

        public static string ComputeCatalogHash(string repositoryRoot, string catalogPath)
        {
            EnsureAllowedLibraryCatalogPath(repositoryRoot, catalogPath);
            string absolutePath = ResolvePath(repositoryRoot, catalogPath);
            if (!File.Exists(absolutePath))
                return string.Empty;

            byte[] bytes = File.ReadAllBytes(absolutePath);
            using (SHA256 sha256 = SHA256.Create())
            {
                return ToLowerHex(sha256.ComputeHash(bytes));
            }
        }

        public static FairyGuiArtSliceOperationPlan PlanSaveCatalogSemantics(
            string repositoryRoot,
            string library,
            string expectedCatalogHash,
            UiArtResourceLibraryCatalog catalog)
        {
            UiArtResourceLibraryDescriptor descriptor = ResolveLibraryDescriptor(repositoryRoot, library);
            var diagnostics = new List<FairyGuiArtSliceOperationDiagnostic>();
            ValidateCatalogHash(repositoryRoot, descriptor.CatalogPath, expectedCatalogHash, diagnostics);

            if (catalog == null)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI010",
                    "Catalog payload is required.",
                    descriptor.CatalogPath));
            }
            else
            {
                UiArtResourceLibraryCatalog currentCatalog = ReadCatalogFile(repositoryRoot, descriptor.CatalogPath);
                if (!string.Equals(catalog.LibraryId, descriptor.LibraryId, StringComparison.Ordinal))
                {
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                        "UIARTRESAPI011",
                        "Catalog libraryId does not match the requested library.",
                        descriptor.CatalogPath));
                }

                if (!string.Equals(NormalizePath(catalog.ResourceRoot), descriptor.ResourceRoot, StringComparison.Ordinal))
                {
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                        "UIARTRESAPI012",
                        "Catalog resourceRoot does not match the requested library.",
                        descriptor.CatalogPath));
                }

                ValidateSemanticSaveStructure(currentCatalog, catalog, descriptor.CatalogPath, diagnostics);
                AppendCatalogValidationDiagnostics(repositoryRoot, descriptor, catalog, diagnostics);
            }

            if (!HasBlockingDiagnostic(diagnostics))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Info(
                    "UIARTRESAPI013",
                    "Resource semantic fields will be saved. FairyGUI XML, PNG files, atlases, and *_fui.bytes will not be modified.",
                    descriptor.CatalogPath));
            }

            bool blocked = HasBlockingDiagnostic(diagnostics);
            return new FairyGuiArtSliceOperationPlan(
                "SaveCatalogSemantics",
                string.Empty,
                descriptor.CatalogPath,
                string.Empty,
                !blocked,
                false,
                false,
                false,
                false,
                diagnostics);
        }

        public static void SaveCatalogSemantics(
            string repositoryRoot,
            string library,
            string expectedCatalogHash,
            UiArtResourceLibraryCatalog catalog)
        {
            FairyGuiArtSliceOperationPlan plan = PlanSaveCatalogSemantics(repositoryRoot, library, expectedCatalogHash, catalog);
            if (plan.Blocked)
                throw new InvalidOperationException("Save catalog semantics plan is blocked.");

            UiArtResourceLibraryDescriptor descriptor = ResolveLibraryDescriptor(repositoryRoot, library);
            WriteCatalogFile(repositoryRoot, descriptor.CatalogPath, catalog);
        }

        public static FairyGuiArtSliceOperationPlan PlanRegisterDraftResource(
            string repositoryRoot,
            string library,
            string expectedCatalogHash,
            string resourceId,
            string relativePath)
        {
            UiArtResourceLibraryDescriptor descriptor = ResolveLibraryDescriptor(repositoryRoot, library);
            var diagnostics = new List<FairyGuiArtSliceOperationDiagnostic>();
            ValidateCatalogHash(repositoryRoot, descriptor.CatalogPath, expectedCatalogHash, diagnostics);

            if (string.IsNullOrWhiteSpace(resourceId))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI020",
                    "Draft resourceId is required.",
                    descriptor.CatalogPath));
            }

            if (!string.IsNullOrWhiteSpace(relativePath) && !IsSafeRelativePath(relativePath))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI021",
                    "Draft relativePath must stay inside the resource library.",
                    relativePath));
            }

            if (!HasBlockingDiagnostic(diagnostics))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Info(
                    "UIARTRESAPI022",
                    "Draft resource registration will update the resource catalog only.",
                    descriptor.CatalogPath));
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Warning(
                    "UIARTRESAPI023",
                    "Draft registration does not create FairyGUI package resources or component usages; complete real FairyGUI authoring and publishing separately.",
                    relativePath));
            }

            bool blocked = HasBlockingDiagnostic(diagnostics);
            return new FairyGuiArtSliceOperationPlan(
                "RegisterDraftResource",
                resourceId,
                descriptor.CatalogPath,
                relativePath,
                !blocked,
                false,
                false,
                false,
                false,
                diagnostics);
        }

        public static FairyGuiArtSliceOperationPlan PlanCreateResource(
            string repositoryRoot,
            string library,
            string expectedCatalogHash,
            UiArtResource resource,
            byte[] pngBytes)
        {
            UiArtResourceLibraryDescriptor descriptor = ResolveLibraryDescriptor(repositoryRoot, library);
            UiArtResourceLibraryAuthoringState state = ReadAuthoringState(repositoryRoot, descriptor.LibraryId);
            var diagnostics = new List<FairyGuiArtSliceOperationDiagnostic>();
            ValidateCatalogHash(repositoryRoot, descriptor.CatalogPath, expectedCatalogHash, diagnostics);

            string resourceId = resource != null ? resource.ResourceId ?? string.Empty : string.Empty;
            string relativePath = resource != null ? NormalizePath(resource.RelativePath) : string.Empty;
            string targetDisplayPath = !string.IsNullOrWhiteSpace(relativePath)
                ? NormalizePath(descriptor.ResourceRoot + "/" + relativePath)
                : descriptor.ResourceRoot;

            ValidateCreateResourcePayload(repositoryRoot, descriptor, state.Catalog, resource, pngBytes, targetDisplayPath, diagnostics);

            if (!HasBlockingDiagnostic(diagnostics))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Info(
                    "UIARTRESAPI060",
                    "New resource creation will write one PNG under the UI art resource library and append one catalog entry. FairyGUI package.xml, component XML, atlases, and *_fui.bytes will not be modified.",
                    targetDisplayPath));
            }

            bool blocked = HasBlockingDiagnostic(diagnostics);
            return new FairyGuiArtSliceOperationPlan(
                "CreateResource",
                resourceId,
                targetDisplayPath,
                "uploaded-png",
                !blocked,
                !blocked,
                false,
                false,
                false,
                diagnostics);
        }

        public static void CreateResource(
            string repositoryRoot,
            string library,
            string expectedCatalogHash,
            UiArtResource resource,
            byte[] pngBytes)
        {
            FairyGuiArtSliceOperationPlan plan = PlanCreateResource(repositoryRoot, library, expectedCatalogHash, resource, pngBytes);
            if (plan.Blocked)
                throw new InvalidOperationException("Create resource plan is blocked.");

            UiArtResourceLibraryDescriptor descriptor = ResolveLibraryDescriptor(repositoryRoot, library);
            UiArtResourceLibraryCatalog catalog = ReadCatalogFile(repositoryRoot, descriptor.CatalogPath);
            UiArtResource resourceToWrite = CloneResource(resource);
            resourceToWrite.RelativePath = NormalizePath(resourceToWrite.RelativePath);

            UiArtResource[] resources = catalog.Resources ?? Array.Empty<UiArtResource>();
            var nextResources = new UiArtResource[resources.Length + 1];
            Array.Copy(resources, nextResources, resources.Length);
            nextResources[resources.Length] = resourceToWrite;
            Array.Sort(nextResources, CompareResourcesById);
            catalog.Resources = nextResources;

            string targetPath = ResolvePath(repositoryRoot, descriptor.ResourceRoot + "/" + resourceToWrite.RelativePath);
            EnsurePathUnderRoot(ResolvePath(repositoryRoot, descriptor.ResourceRoot), targetPath);
            bool pngWritten = false;
            try
            {
                WriteBytesAtomic(targetPath, pngBytes);
                pngWritten = true;
                WriteCatalogFile(repositoryRoot, descriptor.CatalogPath, catalog);
            }
            catch
            {
                if (pngWritten && File.Exists(targetPath))
                    File.Delete(targetPath);
                throw;
            }
        }

        public static FairyGuiArtSliceOperationPlan PlanReplaceExistingPng(
            string repositoryRoot,
            string library,
            string expectedCatalogHash,
            string resourceId,
            byte[] replacementBytes)
        {
            UiArtResourceLibraryDescriptor descriptor = ResolveLibraryDescriptor(repositoryRoot, library);
            UiArtResourceLibraryAuthoringState state = ReadAuthoringState(repositoryRoot, descriptor.LibraryId);
            var diagnostics = new List<FairyGuiArtSliceOperationDiagnostic>();
            ValidateCatalogHash(repositoryRoot, descriptor.CatalogPath, expectedCatalogHash, diagnostics);

            UiArtResource resource = FindResource(state.Catalog, resourceId);
            if (resource == null)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI030",
                    "Resource was not found in the catalog.",
                    descriptor.CatalogPath));
            }

            string targetDisplayPath = resource != null
                ? NormalizePath(descriptor.ResourceRoot + "/" + resource.RelativePath)
                : string.Empty;
            string targetPath = resource != null
                ? ResolvePath(repositoryRoot, targetDisplayPath)
                : string.Empty;

            if (resource != null && !IsPathAtOrUnderRoot(ResolvePath(repositoryRoot, descriptor.ResourceRoot), targetPath))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI031",
                    "Replacement target is outside the UI art resource library root.",
                    targetDisplayPath));
            }

            if (replacementBytes == null || replacementBytes.Length == 0)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI032",
                    "Replacement PNG bytes are required.",
                    targetDisplayPath));
            }

            if (!HasBlockingDiagnostic(diagnostics))
            {
                FairyGuiArtSlicePngInfo existing = FairyGuiArtSliceCatalogService.ReadPngInfo(targetPath);
                FairyGuiArtSlicePngInfo replacement = FairyGuiArtSliceCatalogService.ReadPngInfo(replacementBytes);
                ValidateReplacementPng(resource, existing, replacement, targetDisplayPath, diagnostics);
            }

            if (resource != null && resource.Replacement != null && resource.Replacement.ReplaceTogether)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Warning(
                    "UIARTRESAPI039",
                    "Resource is marked replaceTogether; review sibling resources before replacing only this PNG.",
                    targetDisplayPath));
            }

            if (!HasBlockingDiagnostic(diagnostics))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Info(
                    "UIARTRESAPI040",
                    "Existing PNG will be replaced at the same resource-library path. FairyGUI XML and published outputs will not be modified, but FairyGUI publish is required before runtime use.",
                    targetDisplayPath));
            }

            bool blocked = HasBlockingDiagnostic(diagnostics);
            return new FairyGuiArtSliceOperationPlan(
                "ReplaceExistingPng",
                resourceId,
                targetDisplayPath,
                "uploaded-png",
                false,
                !blocked,
                false,
                false,
                !blocked,
                diagnostics);
        }

        public static void ReplaceExistingPng(
            string repositoryRoot,
            string library,
            string expectedCatalogHash,
            string resourceId,
            byte[] replacementBytes)
        {
            FairyGuiArtSliceOperationPlan plan = PlanReplaceExistingPng(repositoryRoot, library, expectedCatalogHash, resourceId, replacementBytes);
            if (plan.Blocked)
                throw new InvalidOperationException("Replacement plan is blocked.");

            UiArtResourceLibraryDescriptor descriptor = ResolveLibraryDescriptor(repositoryRoot, library);
            UiArtResourceLibraryAuthoringState state = ReadAuthoringState(repositoryRoot, descriptor.LibraryId);
            UiArtResource resource = FindResource(state.Catalog, resourceId);
            if (resource == null)
                throw new InvalidOperationException("Replacement target was not found.");

            string targetPath = ResolvePath(repositoryRoot, descriptor.ResourceRoot + "/" + resource.RelativePath);
            EnsurePathUnderRoot(ResolvePath(repositoryRoot, descriptor.ResourceRoot), targetPath);
            WriteBytesAtomic(targetPath, replacementBytes);
        }

        public static string GenerateMarkdownReport(UiArtResourceLibraryValidationResult result)
        {
            if (result == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("# UI Art Resource Library Report");
            builder.AppendLine();
            builder.AppendLine("Library: `" + result.LibraryId + "`");
            builder.AppendLine("Root: `" + result.ResourceRoot + "`");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine("- Resources: " + result.Resources.Count.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- Errors: " + result.ErrorCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- Warnings: " + result.WarningCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- Infos: " + result.InfoCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine();
            builder.AppendLine("## Diagnostics");
            builder.AppendLine();
            if (result.Diagnostics.Count == 0)
            {
                builder.AppendLine("No diagnostics.");
            }
            else
            {
                for (int i = 0; i < result.Diagnostics.Count; i++)
                {
                    UiArtResourceDiagnostic diagnostic = result.Diagnostics[i];
                    builder.Append("- `");
                    builder.Append(diagnostic.Code);
                    builder.Append("` ");
                    builder.Append(diagnostic.Severity);
                    builder.Append(" ");
                    builder.Append(diagnostic.Name);
                    if (!string.IsNullOrWhiteSpace(diagnostic.ResourceId))
                    {
                        builder.Append(" resource=`");
                        builder.Append(diagnostic.ResourceId);
                        builder.Append("`");
                    }

                    builder.Append(": ");
                    builder.AppendLine(diagnostic.Message);
                }
            }

            builder.AppendLine();
            builder.AppendLine("## Resources");
            builder.AppendLine();
            for (int i = 0; i < result.Resources.Count; i++)
            {
                UiArtResource resource = result.Resources[i];
                builder.Append("- `");
                builder.Append(resource.ResourceId);
                builder.Append("` ");
                builder.Append(resource.RelativePath);
                builder.Append(" ");
                builder.AppendLine(resource.Category);
            }

            return builder.ToString();
        }

        private static UiArtResourceLibraryCatalog LoadCatalog(
            string repositoryRoot,
            string catalogPath,
            List<UiArtResourceDiagnostic> diagnostics)
        {
            string absolutePath = ResolvePath(repositoryRoot, catalogPath);
            if (!File.Exists(absolutePath))
            {
                AddError(diagnostics, "UIARTRES000", "CatalogMissing", DefaultLibraryId, string.Empty, catalogPath, "UI art resource catalog is missing.");
                return null;
            }

            try
            {
                return ReadCatalogFile(repositoryRoot, catalogPath);
            }
            catch (JsonException ex)
            {
                AddError(diagnostics, "UIARTRES001", "CatalogInvalid", DefaultLibraryId, string.Empty, catalogPath, "UI art resource catalog JSON could not be parsed: " + ex.Message);
                return null;
            }
        }

        private static void ValidateCatalog(
            string repositoryRoot,
            UiArtResourceLibraryDescriptor descriptor,
            UiArtResourceLibraryCatalog catalog,
            string resourceRoot,
            List<UiArtResourceDiagnostic> diagnostics)
        {
            if (!string.Equals(catalog.SchemaVersion ?? string.Empty, CurrentSchemaVersion, StringComparison.Ordinal))
            {
                AddError(diagnostics, "UIARTRES002", "UnsupportedSchema", catalog.LibraryId, string.Empty, descriptor.CatalogPath, "Unsupported UI art resource catalog schema version: " + (catalog.SchemaVersion ?? string.Empty) + ".");
            }

            if (!string.Equals(catalog.LibraryId ?? string.Empty, descriptor.LibraryId, StringComparison.Ordinal))
            {
                AddError(diagnostics, "UIARTRES003", "LibraryIdMismatch", descriptor.LibraryId, string.Empty, descriptor.CatalogPath, "Catalog libraryId does not match the requested library.");
            }

            if (!string.Equals(NormalizePath(catalog.ResourceRoot), descriptor.ResourceRoot, StringComparison.Ordinal))
            {
                AddError(diagnostics, "UIARTRES004", "ResourceRootMismatch", descriptor.LibraryId, string.Empty, descriptor.CatalogPath, "Catalog resourceRoot does not match the resolved resource library root.");
            }

            if (!IsAllowedLibraryRootPath(repositoryRoot, resourceRoot))
            {
                AddError(diagnostics, "UIARTRES019", "ResourceRootOutsideUiKits", descriptor.LibraryId, string.Empty, descriptor.CatalogPath, "Catalog resourceRoot must stay under ArtSource/UIKits.");
                return;
            }

            if (!string.Equals(catalog.PathStyle ?? string.Empty, "library-relative-posix", StringComparison.Ordinal))
                AddError(diagnostics, "UIARTRES020", "UnsupportedPathStyle", descriptor.LibraryId, string.Empty, descriptor.CatalogPath, "Catalog pathStyle must be library-relative-posix.");

            if (catalog.DesignResolution == null || catalog.DesignResolution.Width <= 0 || catalog.DesignResolution.Height <= 0)
                AddError(diagnostics, "UIARTRES021", "InvalidDesignResolution", descriptor.LibraryId, string.Empty, descriptor.CatalogPath, "Catalog designResolution must be positive.");

            string rootAbsolutePath = ResolvePath(repositoryRoot, resourceRoot);
            UiArtResource[] resources = catalog.Resources ?? Array.Empty<UiArtResource>();
            var resourceIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < resources.Length; i++)
            {
                UiArtResource resource = resources[i];
                if (resource == null)
                    continue;

                string resourceId = resource.ResourceId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(resourceId) || !resourceIds.Add(resourceId))
                {
                    AddError(diagnostics, "UIARTRES005", "DuplicateResourceId", catalog.LibraryId, resourceId, descriptor.CatalogPath, "Duplicate or empty resourceId: " + resourceId + ".");
                }

                ValidateResourcePath(repositoryRoot, descriptor, resourceRoot, rootAbsolutePath, resource, diagnostics);
                ValidateRecommendedUsage(catalog.LibraryId, descriptor.CatalogPath, resource, diagnostics);
            }
        }

        private static void ValidateResourcePath(
            string repositoryRoot,
            UiArtResourceLibraryDescriptor descriptor,
            string resourceRoot,
            string rootAbsolutePath,
            UiArtResource resource,
            List<UiArtResourceDiagnostic> diagnostics)
        {
            string relativePath = NormalizePath(resource.RelativePath);
            string displayPath = NormalizePath(resourceRoot + "/" + relativePath);
            if (!IsSafeRelativePath(relativePath))
            {
                AddError(diagnostics, "UIARTRES006", "UnsafeRelativePath", descriptor.LibraryId, resource.ResourceId, displayPath, "Resource relativePath must stay inside the UI art resource library.");
                return;
            }

            string absolutePath = ResolvePath(repositoryRoot, displayPath);
            if (!IsPathAtOrUnderRoot(rootAbsolutePath, absolutePath))
            {
                AddError(diagnostics, "UIARTRES006", "UnsafeRelativePath", descriptor.LibraryId, resource.ResourceId, displayPath, "Resource path escapes the UI art resource library root.");
                return;
            }

            if (!File.Exists(absolutePath))
            {
                AddError(diagnostics, "UIARTRES007", "MissingResourceFile", descriptor.LibraryId, resource.ResourceId, displayPath, "Resource PNG file is missing.");
                return;
            }

            FairyGuiArtSlicePngInfo png = FairyGuiArtSliceCatalogService.ReadPngInfo(absolutePath);
            if (!png.Valid)
            {
                AddError(diagnostics, "UIARTRES008", "InvalidPng", descriptor.LibraryId, resource.ResourceId, displayPath, "PNG metadata could not be read: " + png.Error);
                return;
            }

            ValidatePngAgainstResourcePolicy(descriptor.LibraryId, resource, displayPath, png, diagnostics);
        }

        private static void ValidatePngAgainstResourcePolicy(
            string libraryId,
            UiArtResource resource,
            string displayPath,
            FairyGuiArtSlicePngInfo png,
            List<UiArtResourceDiagnostic> diagnostics)
        {
            UiArtResourceReplacementPolicy replacement = resource.Replacement;
            if (replacement != null)
            {
                if (replacement.RequireAlphaChannel && !png.HasAlphaChannel)
                    AddError(diagnostics, "UIARTRES009", "AlphaChannelRequired", libraryId, resource.ResourceId, displayPath, "Resource PNG must include an alpha channel.");

                if (replacement.RequireNonOpaqueAlpha && !png.HasNonOpaqueAlpha)
                    AddError(diagnostics, "UIARTRES010", "NonOpaqueAlphaRequired", libraryId, resource.ResourceId, displayPath, "Resource PNG must include non-opaque alpha data.");

                if (replacement.MinPixelWidth > 0 && png.Width < replacement.MinPixelWidth)
                    AddError(diagnostics, "UIARTRES011", "MinWidthMismatch", libraryId, resource.ResourceId, displayPath, "Resource PNG width is below minPixelWidth.");

                if (replacement.MinPixelHeight > 0 && png.Height < replacement.MinPixelHeight)
                    AddError(diagnostics, "UIARTRES012", "MinHeightMismatch", libraryId, resource.ResourceId, displayPath, "Resource PNG height is below minPixelHeight.");
            }

            UiArtRecommendedFairyGuiUsage usage = resource.RecommendedFairyGuiUsage;
            if (usage == null)
            {
                AddWarning(diagnostics, "UIARTRES013", "MissingRecommendedUsage", libraryId, resource.ResourceId, displayPath, "Resource has no recommended FairyGUI usage.");
                return;
            }

            if (usage.DesignSize == null || usage.DesignSize.Width <= 0 || usage.DesignSize.Height <= 0)
                AddWarning(diagnostics, "UIARTRES014", "InvalidDesignSize", libraryId, resource.ResourceId, displayPath, "Recommended FairyGUI usage should include a positive designSize.");

            if (string.Equals(usage.Policy, "NineSlice", StringComparison.Ordinal))
                ValidateScale9Grid(libraryId, resource, displayPath, png, usage.Scale9Grid, diagnostics);
        }

        private static void ValidateRecommendedUsage(
            string libraryId,
            string catalogPath,
            UiArtResource resource,
            List<UiArtResourceDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(resource.Category))
                AddWarning(diagnostics, "UIARTRES015", "MissingCategory", libraryId, resource.ResourceId, catalogPath, "Resource category is empty.");
            else if (!IsSupportedCategory(resource.Category))
                AddError(diagnostics, "UIARTRES022", "UnsupportedCategory", libraryId, resource.ResourceId, catalogPath, "Resource category is not supported: " + resource.Category + ".");

            if (string.IsNullOrWhiteSpace(resource.DisplayName))
                AddWarning(diagnostics, "UIARTRES016", "MissingDisplayName", libraryId, resource.ResourceId, catalogPath, "Resource displayName is empty.");

            UiArtRecommendedFairyGuiUsage usage = resource.RecommendedFairyGuiUsage;
            if (usage != null && !IsSupportedUsagePolicy(usage.Policy))
                AddError(diagnostics, "UIARTRES023", "UnsupportedUsagePolicy", libraryId, resource.ResourceId, catalogPath, "Recommended FairyGUI usage policy is not supported: " + (usage.Policy ?? string.Empty) + ".");

            UiArtResourceReplacementPolicy replacement = resource.Replacement;
            if (replacement != null && replacement.RequireNonOpaqueAlpha && !replacement.RequireAlphaChannel)
                AddError(diagnostics, "UIARTRES024", "InconsistentAlphaPolicy", libraryId, resource.ResourceId, catalogPath, "requireNonOpaqueAlpha requires requireAlphaChannel.");
        }

        private static void ValidateScale9Grid(
            string libraryId,
            UiArtResource resource,
            string displayPath,
            FairyGuiArtSlicePngInfo png,
            UiArtRect grid,
            List<UiArtResourceDiagnostic> diagnostics)
        {
            if (grid == null)
            {
                AddError(diagnostics, "UIARTRES017", "Scale9GridMissing", libraryId, resource.ResourceId, displayPath, "NineSlice usage requires scale9Grid.");
                return;
            }

            bool invalid = grid.X < 0
                || grid.Y < 0
                || grid.Width <= 0
                || grid.Height <= 0
                || grid.X + grid.Width > png.Width
                || grid.Y + grid.Height > png.Height;
            if (invalid)
            {
                AddError(diagnostics, "UIARTRES018", "Scale9GridOutOfBounds", libraryId, resource.ResourceId, displayPath, "scale9Grid must stay within the PNG pixel bounds.");
            }
        }

        private static void ValidateReplacementPng(
            UiArtResource resource,
            FairyGuiArtSlicePngInfo existing,
            FairyGuiArtSlicePngInfo replacement,
            string targetDisplayPath,
            List<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            if (!existing.Valid)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI034",
                    "Existing PNG is invalid: " + existing.Error,
                    targetDisplayPath));
                return;
            }

            if (!replacement.Valid)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI035",
                    "Replacement PNG is invalid: " + replacement.Error,
                    targetDisplayPath));
                return;
            }

            UiArtResourceReplacementPolicy policy = resource != null ? resource.Replacement : null;
            if (policy == null)
                return;

            if (policy.RequireSamePixelSize && (existing.Width != replacement.Width || existing.Height != replacement.Height))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI036",
                    "Replacement must keep the same pixel size. Old="
                    + existing.SizeText
                    + ", New="
                    + replacement.SizeText
                    + ".",
                    targetDisplayPath));
            }

            if (policy.MinPixelWidth > 0 && replacement.Width < policy.MinPixelWidth)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI037",
                    "Replacement width is below minPixelWidth.",
                    targetDisplayPath));
            }

            if (policy.MinPixelHeight > 0 && replacement.Height < policy.MinPixelHeight)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI038",
                    "Replacement height is below minPixelHeight.",
                    targetDisplayPath));
            }

            if (policy.RequireAlphaChannel && !replacement.HasAlphaChannel)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI041",
                    "Replacement must include an alpha channel.",
                    targetDisplayPath));
            }

            if (policy.RequireNonOpaqueAlpha && !replacement.HasNonOpaqueAlpha)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI042",
                    "Replacement must include non-opaque alpha data.",
                    targetDisplayPath));
            }
        }

        private static void ValidateCreateResourcePayload(
            string repositoryRoot,
            UiArtResourceLibraryDescriptor descriptor,
            UiArtResourceLibraryCatalog catalog,
            UiArtResource resource,
            byte[] pngBytes,
            string targetDisplayPath,
            List<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            if (resource == null)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI050",
                    "Resource payload is required.",
                    descriptor.CatalogPath));
                return;
            }

            ValidateCreateResourceIdentity(catalog, resource, descriptor.CatalogPath, diagnostics);

            string relativePath = NormalizePath(resource.RelativePath);
            string libraryRoot = ResolvePath(repositoryRoot, descriptor.ResourceRoot);
            string targetPath = ResolvePath(repositoryRoot, descriptor.ResourceRoot + "/" + relativePath);
            if (!IsSafeRelativePath(relativePath) || !IsPathAtOrUnderRoot(libraryRoot, targetPath))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI054",
                    "Resource relativePath must stay inside the UI art resource library.",
                    targetDisplayPath));
            }

            if (!relativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI055",
                    "Resource relativePath must point to a PNG file.",
                    targetDisplayPath));
            }

            if (File.Exists(targetPath))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI056",
                    "Resource PNG target already exists. Use replace instead of create.",
                    targetDisplayPath));
            }

            if (pngBytes == null || pngBytes.Length == 0)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI057",
                    "Uploaded PNG bytes are required.",
                    targetDisplayPath));
                return;
            }

            FairyGuiArtSlicePngInfo png = FairyGuiArtSliceCatalogService.ReadPngInfo(pngBytes);
            if (!png.Valid)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI058",
                    "Uploaded PNG is invalid: " + png.Error,
                    targetDisplayPath));
                return;
            }

            ValidateCreateResourceUsage(resource, png, targetDisplayPath, diagnostics);
            ValidateCreateResourceReplacement(resource, png, targetDisplayPath, diagnostics);
        }

        private static void ValidateCreateResourceIdentity(
            UiArtResourceLibraryCatalog catalog,
            UiArtResource resource,
            string catalogPath,
            List<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            string resourceId = resource.ResourceId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI051",
                    "Resource resourceId is required.",
                    catalogPath));
            }
            else if (ContainsPathSeparatorOrWhitespace(resourceId))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI052",
                    "Resource resourceId must not contain path separators or whitespace.",
                    catalogPath));
            }

            string relativePath = NormalizePath(resource.RelativePath);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI053",
                    "Resource relativePath is required.",
                    catalogPath));
            }

            UiArtResource[] resources = catalog != null && catalog.Resources != null ? catalog.Resources : Array.Empty<UiArtResource>();
            for (int i = 0; i < resources.Length; i++)
            {
                UiArtResource existing = resources[i];
                if (existing == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(resourceId) && string.Equals(existing.ResourceId, resourceId, StringComparison.Ordinal))
                {
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                        "UIARTRESAPI061",
                        "Resource resourceId already exists in the catalog.",
                        catalogPath));
                }

                if (!string.IsNullOrWhiteSpace(relativePath) && string.Equals(NormalizePath(existing.RelativePath), relativePath, StringComparison.Ordinal))
                {
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                        "UIARTRESAPI062",
                        "Resource relativePath already exists in the catalog.",
                        relativePath));
                }
            }
        }

        private static void ValidateCreateResourceUsage(
            UiArtResource resource,
            FairyGuiArtSlicePngInfo png,
            string targetDisplayPath,
            List<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(resource.Category))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Warning(
                    "UIARTRESAPI063",
                    "Resource category is empty.",
                    targetDisplayPath));
            }
            else if (!IsSupportedCategory(resource.Category))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI075",
                    "Resource category is not supported: " + resource.Category + ".",
                    targetDisplayPath));
            }

            if (string.IsNullOrWhiteSpace(resource.DisplayName))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Warning(
                    "UIARTRESAPI064",
                    "Resource displayName is empty.",
                    targetDisplayPath));
            }

            UiArtRecommendedFairyGuiUsage usage = resource.RecommendedFairyGuiUsage;
            if (usage == null)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI065",
                    "recommendedFairyGuiUsage is required for a new hand-curated UI resource.",
                    targetDisplayPath));
                return;
            }

            if (string.IsNullOrWhiteSpace(usage.Policy))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI066",
                    "recommendedFairyGuiUsage.policy is required.",
                    targetDisplayPath));
            }
            else if (!IsSupportedUsagePolicy(usage.Policy))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI076",
                    "recommendedFairyGuiUsage.policy is not supported: " + usage.Policy + ".",
                    targetDisplayPath));
            }

            if (usage.DesignSize == null || usage.DesignSize.Width <= 0 || usage.DesignSize.Height <= 0)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI067",
                    "recommendedFairyGuiUsage.designSize must be positive.",
                    targetDisplayPath));
            }

            if (string.Equals(usage.Policy, "NineSlice", StringComparison.Ordinal))
                ValidateCreateScale9Grid(resource, png, targetDisplayPath, usage.Scale9Grid, diagnostics);
        }

        private static void ValidateCreateScale9Grid(
            UiArtResource resource,
            FairyGuiArtSlicePngInfo png,
            string targetDisplayPath,
            UiArtRect grid,
            List<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            if (grid == null)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI068",
                    "NineSlice usage requires scale9Grid.",
                    targetDisplayPath));
                return;
            }

            bool invalid = grid.X < 0
                || grid.Y < 0
                || grid.Width <= 0
                || grid.Height <= 0
                || grid.X + grid.Width > png.Width
                || grid.Y + grid.Height > png.Height;
            if (invalid)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI069",
                    "scale9Grid must stay within the uploaded PNG pixel bounds.",
                    targetDisplayPath));
            }
        }

        private static void ValidateCreateResourceReplacement(
            UiArtResource resource,
            FairyGuiArtSlicePngInfo png,
            string targetDisplayPath,
            List<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            UiArtResourceReplacementPolicy policy = resource.Replacement;
            if (policy == null)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Warning(
                    "UIARTRESAPI070",
                    "replacement policy is empty; future replacement safety checks will be weaker.",
                    targetDisplayPath));
                return;
            }

            if (policy.RequireAlphaChannel && !png.HasAlphaChannel)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI071",
                    "Uploaded PNG must include an alpha channel.",
                    targetDisplayPath));
            }

            if (policy.RequireNonOpaqueAlpha && !png.HasNonOpaqueAlpha)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI072",
                    "Uploaded PNG must include non-opaque alpha data.",
                    targetDisplayPath));
            }

            if (policy.RequireNonOpaqueAlpha && !policy.RequireAlphaChannel)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI077",
                    "requireNonOpaqueAlpha requires requireAlphaChannel.",
                    targetDisplayPath));
            }

            if (policy.MinPixelWidth > 0 && png.Width < policy.MinPixelWidth)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI073",
                    "Uploaded PNG width is below minPixelWidth.",
                    targetDisplayPath));
            }

            if (policy.MinPixelHeight > 0 && png.Height < policy.MinPixelHeight)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI074",
                    "Uploaded PNG height is below minPixelHeight.",
                    targetDisplayPath));
            }
        }

        private static UiArtResource FindResource(UiArtResourceLibraryCatalog catalog, string resourceId)
        {
            UiArtResource[] resources = catalog != null && catalog.Resources != null ? catalog.Resources : Array.Empty<UiArtResource>();
            for (int i = 0; i < resources.Length; i++)
            {
                if (string.Equals(resources[i].ResourceId, resourceId, StringComparison.Ordinal))
                    return resources[i];
            }

            return null;
        }

        private static void AppendCatalogValidationDiagnostics(
            string repositoryRoot,
            UiArtResourceLibraryDescriptor descriptor,
            UiArtResourceLibraryCatalog catalog,
            List<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            var catalogDiagnostics = new List<UiArtResourceDiagnostic>();
            string resourceRoot = catalog != null ? NormalizePath(catalog.ResourceRoot) : descriptor.ResourceRoot;
            ValidateCatalog(repositoryRoot, descriptor, catalog, resourceRoot, catalogDiagnostics);
            SortDiagnostics(catalogDiagnostics);
            for (int i = 0; i < catalogDiagnostics.Count; i++)
            {
                UiArtResourceDiagnostic diagnostic = catalogDiagnostics[i];
                if (string.Equals(diagnostic.Severity, "error", StringComparison.Ordinal))
                {
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(diagnostic.Code, diagnostic.Message, diagnostic.Path));
                }
                else if (string.Equals(diagnostic.Severity, "warning", StringComparison.Ordinal))
                {
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Warning(diagnostic.Code, diagnostic.Message, diagnostic.Path));
                }
                else
                {
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Info(diagnostic.Code, diagnostic.Message, diagnostic.Path));
                }
            }
        }

        private static void ValidateSemanticSaveStructure(
            UiArtResourceLibraryCatalog current,
            UiArtResourceLibraryCatalog proposed,
            string catalogPath,
            List<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            if (current == null || proposed == null)
                return;

            bool topLevelChanged = !string.Equals(current.SchemaVersion, proposed.SchemaVersion, StringComparison.Ordinal)
                || !string.Equals(current.LibraryId, proposed.LibraryId, StringComparison.Ordinal)
                || !string.Equals(NormalizePath(current.ResourceRoot), NormalizePath(proposed.ResourceRoot), StringComparison.Ordinal)
                || !string.Equals(current.PathStyle, proposed.PathStyle, StringComparison.Ordinal)
                || !SizesEqual(current.DesignResolution, proposed.DesignResolution);
            if (topLevelChanged)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI014",
                    "Semantic save cannot change catalog identity, path style, resource root, schema version, or design resolution.",
                    catalogPath));
            }

            UiArtResource[] currentResources = current.Resources ?? Array.Empty<UiArtResource>();
            UiArtResource[] proposedResources = proposed.Resources ?? Array.Empty<UiArtResource>();
            if (currentResources.Length != proposedResources.Length)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI015",
                    "Semantic save cannot add or remove resources. Use the create operation for new PNG-backed resources.",
                    catalogPath));
                return;
            }

            for (int i = 0; i < currentResources.Length; i++)
            {
                UiArtResource currentResource = currentResources[i];
                if (currentResource == null)
                    continue;

                UiArtResource proposedResource = FindResource(proposed, currentResource.ResourceId);
                if (proposedResource == null
                    || !string.Equals(NormalizePath(currentResource.RelativePath), NormalizePath(proposedResource.RelativePath), StringComparison.Ordinal))
                {
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                        "UIARTRESAPI015",
                        "Semantic save cannot change resourceId or relativePath for resource " + (currentResource.ResourceId ?? string.Empty) + ".",
                        catalogPath));
                }
            }
        }

        private static bool SizesEqual(UiArtSize left, UiArtSize right)
        {
            if (left == null || right == null)
                return left == right;

            return left.Width == right.Width && left.Height == right.Height;
        }

        private static bool IsSupportedCategory(string category)
        {
            switch (category)
            {
                case "ButtonBase":
                case "PanelFrame":
                case "ButtonState":
                case "Icon":
                case "BarTrack":
                case "BarFill":
                case "Divider":
                case "Badge":
                case "InputBackground":
                case "Decoration":
                case "Ignored":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsSupportedUsagePolicy(string policy)
        {
            switch (policy)
            {
                case "Fixed":
                case "PreserveAspect":
                case "Stretch":
                case "HorizontalFill":
                case "VerticalFill":
                case "NineSlice":
                case "Tile":
                    return true;
                default:
                    return false;
            }
        }

        private static void AddKnownLibrary(
            string repositoryRoot,
            List<UiArtResourceLibraryDescriptor> libraries,
            string libraryId,
            string resourceRoot,
            string catalogPath)
        {
            string normalizedRoot = NormalizePath(resourceRoot);
            string normalizedCatalog = NormalizePath(catalogPath);
            if (!IsAllowedLibraryCatalogPath(repositoryRoot, normalizedCatalog)
                || !IsAllowedLibraryRootPath(repositoryRoot, normalizedRoot))
            {
                return;
            }

            for (int i = 0; i < libraries.Count; i++)
            {
                if (string.Equals(libraries[i].CatalogPath, normalizedCatalog, StringComparison.Ordinal))
                    return;
            }

            string absoluteCatalogPath = ResolvePath(repositoryRoot, normalizedCatalog);
            string absoluteRootPath = ResolvePath(repositoryRoot, normalizedRoot);
            if (!File.Exists(absoluteCatalogPath) && !Directory.Exists(absoluteRootPath))
                return;

            string displayName = string.IsNullOrWhiteSpace(libraryId) ? Path.GetFileName(normalizedRoot) : libraryId;
            libraries.Add(new UiArtResourceLibraryDescriptor(libraryId, normalizedRoot, normalizedCatalog, displayName));
        }

        private static int CompareLibraryDescriptors(UiArtResourceLibraryDescriptor left, UiArtResourceLibraryDescriptor right)
        {
            return string.Compare(left.LibraryId, right.LibraryId, StringComparison.Ordinal);
        }

        private static int CompareResourcesById(UiArtResource left, UiArtResource right)
        {
            return string.Compare(left != null ? left.ResourceId : string.Empty, right != null ? right.ResourceId : string.Empty, StringComparison.Ordinal);
        }

        private static UiArtResource CloneResource(UiArtResource resource)
        {
            if (resource == null)
                return null;

            return new UiArtResource
            {
                ResourceId = resource.ResourceId ?? string.Empty,
                RelativePath = NormalizePath(resource.RelativePath),
                Category = resource.Category ?? string.Empty,
                DisplayName = resource.DisplayName ?? string.Empty,
                Description = resource.Description ?? string.Empty,
                Tags = resource.Tags != null ? (string[])resource.Tags.Clone() : Array.Empty<string>(),
                RecommendedFairyGuiUsage = CloneRecommendedUsage(resource.RecommendedFairyGuiUsage),
                Replacement = CloneReplacementPolicy(resource.Replacement)
            };
        }

        private static UiArtRecommendedFairyGuiUsage CloneRecommendedUsage(UiArtRecommendedFairyGuiUsage usage)
        {
            if (usage == null)
                return null;

            return new UiArtRecommendedFairyGuiUsage
            {
                Policy = usage.Policy ?? string.Empty,
                DesignSize = usage.DesignSize != null
                    ? new UiArtSize { Width = usage.DesignSize.Width, Height = usage.DesignSize.Height }
                    : null,
                Scale9Grid = usage.Scale9Grid != null
                    ? new UiArtRect { X = usage.Scale9Grid.X, Y = usage.Scale9Grid.Y, Width = usage.Scale9Grid.Width, Height = usage.Scale9Grid.Height }
                    : null,
                PreserveAspect = usage.PreserveAspect
            };
        }

        private static UiArtResourceReplacementPolicy CloneReplacementPolicy(UiArtResourceReplacementPolicy policy)
        {
            if (policy == null)
                return null;

            return new UiArtResourceReplacementPolicy
            {
                RequireAlphaChannel = policy.RequireAlphaChannel,
                RequireNonOpaqueAlpha = policy.RequireNonOpaqueAlpha,
                RequireSamePixelSize = policy.RequireSamePixelSize,
                AllowStretch = policy.AllowStretch,
                MinPixelWidth = policy.MinPixelWidth,
                MinPixelHeight = policy.MinPixelHeight,
                ReplaceTogether = policy.ReplaceTogether
            };
        }

        private static void ValidateCatalogHash(
            string repositoryRoot,
            string catalogPath,
            string expectedCatalogHash,
            List<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(expectedCatalogHash))
                return;

            string actualHash = ComputeCatalogHash(repositoryRoot, catalogPath);
            if (!string.Equals(actualHash, expectedCatalogHash, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTRESAPI001",
                    "Catalog changed on disk. Reload before applying this operation.",
                    catalogPath));
            }
        }

        private static bool HasBlockingDiagnostic(IReadOnlyList<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Blocking)
                    return true;
            }

            return false;
        }

        private static void AddError(List<UiArtResourceDiagnostic> diagnostics, string code, string name, string libraryId, string resourceId, string path, string message)
        {
            diagnostics.Add(new UiArtResourceDiagnostic(code, name, "error", libraryId, resourceId, path, message));
        }

        private static void AddWarning(List<UiArtResourceDiagnostic> diagnostics, string code, string name, string libraryId, string resourceId, string path, string message)
        {
            diagnostics.Add(new UiArtResourceDiagnostic(code, name, "warning", libraryId, resourceId, path, message));
        }

        private static void SortDiagnostics(List<UiArtResourceDiagnostic> diagnostics)
        {
            diagnostics.Sort(CompareDiagnostics);
        }

        private static int CompareDiagnostics(UiArtResourceDiagnostic left, UiArtResourceDiagnostic right)
        {
            int severity = SeverityRank(left.Severity).CompareTo(SeverityRank(right.Severity));
            if (severity != 0)
                return severity;

            int code = string.Compare(left.Code, right.Code, StringComparison.Ordinal);
            if (code != 0)
                return code;

            int resource = string.Compare(left.ResourceId, right.ResourceId, StringComparison.Ordinal);
            if (resource != 0)
                return resource;

            return string.Compare(left.Path, right.Path, StringComparison.Ordinal);
        }

        private static int SeverityRank(string severity)
        {
            if (string.Equals(severity, "error", StringComparison.Ordinal))
                return 0;
            if (string.Equals(severity, "warning", StringComparison.Ordinal))
                return 1;
            return 2;
        }

        private static bool IsSafeRelativePath(string path)
        {
            string normalized = NormalizePath(path);
            return !string.IsNullOrWhiteSpace(normalized)
                && !Path.IsPathRooted(normalized)
                && normalized.IndexOf("..", StringComparison.Ordinal) < 0
                && normalized.IndexOf(':') < 0;
        }

        private static bool IsAllowedLibraryCatalogPath(string repositoryRoot, string catalogPath)
        {
            string normalized = NormalizePath(catalogPath);
            if (!IsSafeRelativePath(normalized)
                || !normalized.EndsWith("/ui-art-resources.json", StringComparison.Ordinal))
            {
                return false;
            }

            string uiKitsRoot = ResolvePath(repositoryRoot, UiKitsRootPath);
            string absolutePath = ResolvePath(repositoryRoot, normalized);
            return IsPathAtOrUnderRoot(uiKitsRoot, absolutePath);
        }

        private static bool IsAllowedLibraryRootPath(string repositoryRoot, string resourceRoot)
        {
            string normalized = NormalizePath(resourceRoot);
            if (!IsSafeRelativePath(normalized))
                return false;

            string uiKitsRoot = ResolvePath(repositoryRoot, UiKitsRootPath);
            string absolutePath = ResolvePath(repositoryRoot, normalized);
            return IsPathAtOrUnderRoot(uiKitsRoot, absolutePath);
        }

        private static void EnsureAllowedLibraryCatalogPath(string repositoryRoot, string catalogPath)
        {
            if (!IsAllowedLibraryCatalogPath(repositoryRoot, catalogPath))
                throw new InvalidOperationException("UI art resource catalog must stay under ArtSource/UIKits: " + catalogPath + ".");
        }

        private static void EnsureAllowedLibraryRootPath(string repositoryRoot, string resourceRoot)
        {
            if (!IsAllowedLibraryRootPath(repositoryRoot, resourceRoot))
                throw new InvalidOperationException("UI art resource root must stay under ArtSource/UIKits: " + resourceRoot + ".");
        }

        private static bool ContainsPathSeparatorOrWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsWhiteSpace(c) || c == '/' || c == '\\')
                    return true;
            }

            return false;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
        }

        private static string ResolvePath(string repositoryRoot, string relativePath)
        {
            string normalized = NormalizePath(relativePath);
            return Path.GetFullPath(Path.Combine(repositoryRoot ?? string.Empty, normalized.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ToRepositoryRelativePath(string repositoryRoot, string path)
        {
            string fullRoot = Path.GetFullPath(repositoryRoot ?? string.Empty);
            string fullPath = Path.GetFullPath(path ?? string.Empty);
            if (!IsPathAtOrUnderRoot(fullRoot, fullPath))
                return NormalizePath(path);

            return NormalizePath(Path.GetRelativePath(fullRoot, fullPath));
        }

        private static void EnsurePathUnderRoot(string rootPath, string path)
        {
            string fullRoot = Path.GetFullPath(rootPath ?? string.Empty);
            string fullPath = Path.GetFullPath(path ?? string.Empty);
            if (!IsPathAtOrUnderRoot(fullRoot, fullPath))
                throw new InvalidOperationException("Path is outside the allowed root: " + path + ".");
        }

        private static bool IsPathAtOrUnderRoot(string rootPath, string path)
        {
            string fullRoot = Path.GetFullPath(rootPath ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path ?? string.Empty);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), fullRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteTextAtomic(string path, string text)
        {
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            Directory.CreateDirectory(directory);
            string temp = path + ".tmp";
            File.WriteAllText(temp, text);
            ReplaceOrMove(temp, path);
        }

        private static void WriteBytesAtomic(string path, byte[] bytes)
        {
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            Directory.CreateDirectory(directory);
            string temp = path + ".tmp";
            File.WriteAllBytes(temp, bytes);
            ReplaceOrMove(temp, path);
        }

        private static void ReplaceOrMove(string temp, string target)
        {
            if (File.Exists(target))
                File.Replace(temp, target, null);
            else
                File.Move(temp, target);
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    public sealed class UiArtResourceLibraryDescriptor
    {
        public UiArtResourceLibraryDescriptor(string libraryId, string resourceRoot, string catalogPath, string displayName)
        {
            LibraryId = Normalize(libraryId);
            ResourceRoot = Normalize(resourceRoot);
            CatalogPath = Normalize(catalogPath);
            DisplayName = displayName ?? string.Empty;
        }

        [JsonPropertyName("libraryId")]
        public string LibraryId { get; }

        [JsonPropertyName("resourceRoot")]
        public string ResourceRoot { get; }

        [JsonPropertyName("catalogPath")]
        public string CatalogPath { get; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
        }
    }

    public sealed class UiArtResourceLibraryAuthoringState
    {
        public UiArtResourceLibraryAuthoringState(
            UiArtResourceLibraryDescriptor library,
            string catalogHash,
            UiArtResourceLibraryCatalog catalog,
            UiArtResourceLibraryValidationResult validation,
            string markdownReport)
        {
            Library = library;
            CatalogHash = catalogHash ?? string.Empty;
            Catalog = catalog;
            LibraryId = validation != null ? validation.LibraryId : library != null ? library.LibraryId : string.Empty;
            ResourceRoot = validation != null ? validation.ResourceRoot : library != null ? library.ResourceRoot : string.Empty;
            Resources = validation != null ? validation.Resources : Array.Empty<UiArtResource>();
            Diagnostics = validation != null ? validation.Diagnostics : Array.Empty<UiArtResourceDiagnostic>();
            ErrorCount = validation != null ? validation.ErrorCount : 0;
            WarningCount = validation != null ? validation.WarningCount : 0;
            InfoCount = validation != null ? validation.InfoCount : 0;
            MarkdownReport = markdownReport ?? string.Empty;
        }

        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion => "mx.uiArtResourceLibraryAuthoringState.v1";

        [JsonPropertyName("library")]
        public UiArtResourceLibraryDescriptor Library { get; }

        [JsonPropertyName("libraryId")]
        public string LibraryId { get; }

        [JsonPropertyName("resourceRoot")]
        public string ResourceRoot { get; }

        [JsonPropertyName("catalogHash")]
        public string CatalogHash { get; }

        [JsonPropertyName("catalog")]
        public UiArtResourceLibraryCatalog Catalog { get; }

        [JsonPropertyName("resources")]
        public IReadOnlyList<UiArtResource> Resources { get; }

        [JsonPropertyName("diagnostics")]
        public IReadOnlyList<UiArtResourceDiagnostic> Diagnostics { get; }

        [JsonPropertyName("errorCount")]
        public int ErrorCount { get; }

        [JsonPropertyName("warningCount")]
        public int WarningCount { get; }

        [JsonPropertyName("infoCount")]
        public int InfoCount { get; }

        [JsonPropertyName("markdownReport")]
        public string MarkdownReport { get; }
    }

    public sealed class UiArtResourceLibraryValidationResult
    {
        public UiArtResourceLibraryValidationResult(
            string libraryId,
            string resourceRoot,
            IReadOnlyList<UiArtResource> resources,
            IReadOnlyList<UiArtResourceDiagnostic> diagnostics)
        {
            LibraryId = libraryId ?? string.Empty;
            ResourceRoot = resourceRoot ?? string.Empty;
            Resources = resources ?? Array.Empty<UiArtResource>();
            Diagnostics = diagnostics ?? Array.Empty<UiArtResourceDiagnostic>();
            for (int i = 0; i < Diagnostics.Count; i++)
            {
                if (string.Equals(Diagnostics[i].Severity, "error", StringComparison.Ordinal))
                    ErrorCount++;
                else if (string.Equals(Diagnostics[i].Severity, "warning", StringComparison.Ordinal))
                    WarningCount++;
                else
                    InfoCount++;
            }
        }

        public string LibraryId { get; }
        public string ResourceRoot { get; }
        public IReadOnlyList<UiArtResource> Resources { get; }
        public IReadOnlyList<UiArtResourceDiagnostic> Diagnostics { get; }
        public int ErrorCount { get; }
        public int WarningCount { get; }
        public int InfoCount { get; }
        public bool Success => ErrorCount == 0;
    }

    public sealed class UiArtResourceDiagnostic
    {
        public UiArtResourceDiagnostic(string code, string name, string severity, string libraryId, string resourceId, string path, string message)
        {
            Code = code ?? string.Empty;
            Name = name ?? string.Empty;
            Severity = severity ?? string.Empty;
            LibraryId = libraryId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        [JsonPropertyName("code")]
        public string Code { get; }

        [JsonPropertyName("name")]
        public string Name { get; }

        [JsonPropertyName("severity")]
        public string Severity { get; }

        [JsonPropertyName("libraryId")]
        public string LibraryId { get; }

        [JsonPropertyName("resourceId")]
        public string ResourceId { get; }

        [JsonPropertyName("path")]
        public string Path { get; }

        [JsonPropertyName("message")]
        public string Message { get; }
    }

    public sealed class UiArtResourceLibraryCatalog
    {
        public UiArtResourceLibraryCatalog()
        {
            SchemaVersion = string.Empty;
            LibraryId = string.Empty;
            ResourceRoot = string.Empty;
            PathStyle = string.Empty;
            DesignResolution = new UiArtSize();
            Resources = Array.Empty<UiArtResource>();
        }

        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; set; }

        [JsonPropertyName("libraryId")]
        public string LibraryId { get; set; }

        [JsonPropertyName("resourceRoot")]
        public string ResourceRoot { get; set; }

        [JsonPropertyName("pathStyle")]
        public string PathStyle { get; set; }

        [JsonPropertyName("designResolution")]
        public UiArtSize DesignResolution { get; set; }

        [JsonPropertyName("resources")]
        public UiArtResource[] Resources { get; set; }
    }

    public sealed class UiArtResource
    {
        public UiArtResource()
        {
            ResourceId = string.Empty;
            RelativePath = string.Empty;
            Category = string.Empty;
            DisplayName = string.Empty;
            Description = string.Empty;
            Tags = Array.Empty<string>();
        }

        [JsonPropertyName("resourceId")]
        public string ResourceId { get; set; }

        [JsonPropertyName("relativePath")]
        public string RelativePath { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("tags")]
        public string[] Tags { get; set; }

        [JsonPropertyName("recommendedFairyGuiUsage")]
        public UiArtRecommendedFairyGuiUsage RecommendedFairyGuiUsage { get; set; }

        [JsonPropertyName("replacement")]
        public UiArtResourceReplacementPolicy Replacement { get; set; }
    }

    public sealed class UiArtRecommendedFairyGuiUsage
    {
        public UiArtRecommendedFairyGuiUsage()
        {
            Policy = string.Empty;
            DesignSize = new UiArtSize();
        }

        [JsonPropertyName("policy")]
        public string Policy { get; set; }

        [JsonPropertyName("designSize")]
        public UiArtSize DesignSize { get; set; }

        [JsonPropertyName("scale9Grid")]
        public UiArtRect Scale9Grid { get; set; }

        [JsonPropertyName("preserveAspect")]
        public bool PreserveAspect { get; set; }
    }

    public sealed class UiArtResourceReplacementPolicy
    {
        [JsonPropertyName("requireAlphaChannel")]
        public bool RequireAlphaChannel { get; set; }

        [JsonPropertyName("requireNonOpaqueAlpha")]
        public bool RequireNonOpaqueAlpha { get; set; }

        [JsonPropertyName("requireSamePixelSize")]
        public bool RequireSamePixelSize { get; set; }

        [JsonPropertyName("allowStretch")]
        public bool AllowStretch { get; set; }

        [JsonPropertyName("minPixelWidth")]
        public int MinPixelWidth { get; set; }

        [JsonPropertyName("minPixelHeight")]
        public int MinPixelHeight { get; set; }

        [JsonPropertyName("replaceTogether")]
        public bool ReplaceTogether { get; set; }
    }

    public sealed class UiArtSize
    {
        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }

    public sealed class UiArtRect
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }
}

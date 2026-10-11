using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace MxFramework.Authoring.FairyGuiArtSlices
{
    /// <summary>Builds read-only handoff plans from a UI art resource library into FairyGUI packages.</summary>
    /// <remarks>LowFreqAlloc. This service does not write FairyGUI XML, PNG files, atlases, or published outputs.</remarks>
    public static class UiArtResourceFairyGuiIntegrationService
    {
        private const string SchemaVersion = "mx.uiArtFairyGuiIntegrationPlan.v1";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        /// <summary>Lists FairyGUI source packages that can receive a manual resource import.</summary>
        /// <remarks>LowFreqAlloc.</remarks>
        public static IReadOnlyList<UiArtFairyGuiTargetPackage> ListTargetPackages(string repositoryRoot)
        {
            var packages = new List<UiArtFairyGuiTargetPackage>();
            string assetsRoot = ResolvePath(repositoryRoot, "FGUIProject/assets");
            if (!Directory.Exists(assetsRoot))
                return packages;

            string[] directories = Directory.GetDirectories(assetsRoot);
            for (int i = 0; i < directories.Length; i++)
            {
                string packageXmlPath = Path.Combine(directories[i], "package.xml");
                if (!File.Exists(packageXmlPath))
                    continue;

                string packageSourcePath = ToRepositoryRelativePath(repositoryRoot, directories[i]);
                string packageId = Path.GetFileName(directories[i]);
                string packageDescriptionId = ReadPackageDescriptionId(packageXmlPath);
                packages.Add(new UiArtFairyGuiTargetPackage(
                    packageId,
                    packageDescriptionId,
                    packageSourcePath,
                    NormalizePath(packageSourcePath + "/package.xml"),
                    packageId));
            }

            packages.Sort(CompareTargetPackages);
            return packages;
        }

        /// <summary>Builds a stable, read-only FairyGUI import and usage handoff plan.</summary>
        /// <remarks>LowFreqAlloc. No project file is modified.</remarks>
        public static UiArtFairyGuiIntegrationPlan PlanIntegration(
            string repositoryRoot,
            string library,
            string resourceId,
            string targetPackage)
        {
            var diagnostics = new List<FairyGuiArtSliceOperationDiagnostic>();
            UiArtResourceLibraryDescriptor libraryDescriptor = FindLibrary(repositoryRoot, library);
            UiArtResource resource = null;
            UiArtFairyGuiTargetPackage package = FindTargetPackage(repositoryRoot, targetPackage);

            if (libraryDescriptor == null)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTINT001",
                    "UI art resource library was not found.",
                    library ?? string.Empty));
            }
            else
            {
                UiArtResourceLibraryAuthoringState state = UiArtResourceLibraryService.ReadAuthoringState(repositoryRoot, libraryDescriptor.LibraryId);
                resource = FindResource(state.Catalog, resourceId);
                if (resource == null)
                {
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                        "UIARTINT002",
                        "UI art resource was not found in the selected library.",
                        resourceId ?? string.Empty));
                }
            }

            if (package == null)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTINT003",
                    "Target FairyGUI package was not found.",
                    targetPackage ?? string.Empty));
            }

            if (libraryDescriptor == null || resource == null || package == null)
            {
                return CreateBlockedPlan(libraryDescriptor, resourceId, package, diagnostics);
            }

            string sourcePath = NormalizePath(libraryDescriptor.ResourceRoot + "/" + resource.RelativePath);
            string sourceAbsolutePath = ResolvePath(repositoryRoot, sourcePath);
            string libraryRoot = ResolvePath(repositoryRoot, libraryDescriptor.ResourceRoot);
            bool sourcePathSafe = IsPathAtOrUnderRoot(libraryRoot, sourceAbsolutePath);
            string sourceHash = sourcePathSafe
                ? ComputeFileHash(sourceAbsolutePath)
                : string.Empty;
            if (!sourcePathSafe)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTINT007",
                    "Source PNG path escapes the UI art resource library.",
                    sourcePath));
            }
            else if (string.IsNullOrWhiteSpace(sourceHash))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTINT004",
                    "Source PNG is missing or unreadable.",
                    sourcePath));
            }

            string suggestedFileName = Path.GetFileName(NormalizePath(resource.RelativePath));
            string suggestedFolder = BuildSuggestedPackageFolder(libraryDescriptor.LibraryId, resource.RelativePath);
            string suggestedSourcePath = NormalizePath(suggestedFolder + "/" + suggestedFileName);
            string suggestedTargetPath = NormalizePath(package.PackageSourcePath + "/" + suggestedSourcePath);
            string packageRoot = ResolvePath(repositoryRoot, package.PackageSourcePath);
            string packageXmlAbsolutePath = ResolvePath(repositoryRoot, package.PackageXmlPath);

            List<PackageImage> images;
            try
            {
                images = ReadPackageImages(packageXmlAbsolutePath);
            }
            catch (Exception exception) when (exception is IOException || exception is System.Xml.XmlException)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTINT005",
                    "Target package.xml could not be read.",
                    package.PackageXmlPath));
                images = new List<PackageImage>();
            }

            PackageImage existing = FindExactSource(images, suggestedSourcePath);
            bool exactPath = existing != null;
            if (existing == null)
                existing = FindByFileName(images, suggestedFileName);
            if (existing == null && !string.IsNullOrWhiteSpace(sourceHash))
                existing = FindByContentHash(packageRoot, images, sourceHash);

            string status;
            string targetHash = string.Empty;
            bool contentMatches = false;
            bool requiresImport = true;
            if (existing != null)
            {
                string existingAbsolutePath = ResolvePath(packageRoot, existing.SourcePath);
                if (!IsPathAtOrUnderRoot(packageRoot, existingAbsolutePath))
                {
                    status = "UnsafePackageImage";
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                        "UIARTINT008",
                        "package.xml image path escapes the target package.",
                        NormalizePath(package.PackageSourcePath + "/" + existing.SourcePath)));
                }
                else
                {
                    targetHash = ComputeFileHash(existingAbsolutePath);
                    if (string.IsNullOrWhiteSpace(targetHash))
                    {
                        status = "ImportedFileMissing";
                        diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                            "UIARTINT006",
                            "package.xml declares the image, but its source PNG is missing.",
                            NormalizePath(package.PackageSourcePath + "/" + existing.SourcePath)));
                    }
                    else
                    {
                        contentMatches = string.Equals(sourceHash, targetHash, StringComparison.OrdinalIgnoreCase);
                        if (contentMatches)
                        {
                            status = exactPath ? "ImportedExact" : "ImportedEquivalentContent";
                            requiresImport = false;
                            diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Info(
                                exactPath ? "UIARTINT106" : "UIARTINT107",
                                exactPath
                                    ? "The resource is already imported at the suggested package path."
                                    : "Equivalent PNG content is already imported under a different package path.",
                                NormalizePath(package.PackageSourcePath + "/" + existing.SourcePath)));
                        }
                        else
                        {
                            status = exactPath ? "ContentDrift" : "NameConflict";
                            diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Warning(
                                exactPath ? "UIARTINT102" : "UIARTINT103",
                                exactPath
                                    ? "The imported PNG content differs from the resource-library source."
                                    : "The suggested file name is already used by different PNG content.",
                                NormalizePath(package.PackageSourcePath + "/" + existing.SourcePath)));
                        }
                    }
                }
            }
            else
            {
                string suggestedAbsolutePath = ResolvePath(repositoryRoot, suggestedTargetPath);
                if (File.Exists(suggestedAbsolutePath))
                {
                    status = "OrphanTargetFile";
                    targetHash = ComputeFileHash(suggestedAbsolutePath);
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Warning(
                        "UIARTINT104",
                        "A PNG exists at the suggested target path but package.xml does not declare it.",
                        suggestedTargetPath));
                }
                else
                {
                    status = "NotImported";
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Info(
                        "UIARTINT101",
                        "The resource has not been imported into the target FairyGUI package.",
                        suggestedTargetPath));
                }
            }

            string usageStatus = EvaluateUsageStatus(repositoryRoot, package, existing, resource, diagnostics);
            bool blocked = HasBlockingDiagnostic(diagnostics);
            bool requiresPublish = !blocked && (requiresImport || !string.Equals(usageStatus, "Aligned", StringComparison.Ordinal));
            var existingImage = existing == null
                ? null
                : new UiArtFairyGuiExistingImage(
                    existing.ResourceId,
                    existing.FileName,
                    existing.SourcePath,
                    targetHash,
                    contentMatches);

            return new UiArtFairyGuiIntegrationPlan(
                SchemaVersion,
                libraryDescriptor.LibraryId,
                resource.ResourceId,
                sourcePath,
                sourceHash,
                package,
                suggestedFileName,
                "/" + suggestedFolder.Trim('/') + "/",
                suggestedSourcePath,
                suggestedTargetPath,
                "FairyGUIEditor",
                status,
                usageStatus,
                existingImage,
                resource.RecommendedFairyGuiUsage,
                requiresImport,
                true,
                requiresPublish,
                false,
                false,
                false,
                false,
                blocked,
                diagnostics);
        }

        /// <summary>Serializes a deterministic JSON handoff recipe.</summary>
        /// <remarks>AllocByDesign.</remarks>
        public static string GenerateJsonRecipe(UiArtFairyGuiIntegrationPlan plan)
        {
            return NormalizeNewLines(JsonSerializer.Serialize(plan, JsonOptions)) + "\n";
        }

        /// <summary>Generates a deterministic Markdown handoff recipe for people and Development Agents.</summary>
        /// <remarks>AllocByDesign.</remarks>
        public static string GenerateMarkdownRecipe(UiArtFairyGuiIntegrationPlan plan)
        {
            if (plan == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("# FairyGUI 接入配方");
            builder.AppendLine();
            builder.AppendLine("- Resource library: `" + plan.LibraryId + "`");
            builder.AppendLine("- Resource: `" + plan.ResourceId + "`");
            builder.AppendLine("- Audience: `Development Agent / FairyGUI author`");
            builder.AppendLine("- Source PNG: `" + plan.SourcePath + "`");
            builder.AppendLine("- Target package: `" + (plan.TargetPackage != null ? plan.TargetPackage.PackageId : string.Empty) + "`");
            builder.AppendLine("- Suggested package folder: `" + plan.SuggestedPackageFolder + "`");
            builder.AppendLine("- Suggested image name: `" + plan.SuggestedResourceName + "`");
            builder.AppendLine("- Import status: `" + plan.Status + "`");
            builder.AppendLine("- Usage status: `" + plan.UsageStatus + "`");
            builder.AppendLine("- FairyGUI resource id owner: `" + plan.ResourceIdOwnership + "`");
            builder.AppendLine("- Requires FairyGUI publish: `" + (plan.RequiresFairyGuiPublish ? "true" : "false") + "`");
            if (plan.ExistingImage != null)
                builder.AppendLine("- Existing FairyGUI resource id: `" + plan.ExistingImage.ResourceId + "`");
            builder.AppendLine();
            builder.AppendLine("## Recommended FairyGUI Usage");
            builder.AppendLine();
            AppendUsageRecipe(builder, plan.RecommendedFairyGuiUsage);
            builder.AppendLine();
            builder.AppendLine("## Manual Handoff");
            builder.AppendLine();
            builder.AppendLine("1. Open `" + (plan.TargetPackage != null ? plan.TargetPackage.PackageSourcePath : string.Empty) + "` in FairyGUI Editor.");
            builder.AppendLine("2. Import `" + plan.SourcePath + "` into `" + plan.SuggestedPackageFolder + "` using the name `" + plan.SuggestedResourceName + "`.");
            builder.AppendLine("3. Apply the recommended size, aspect, and NineSlice parameters when wiring component usage.");
            builder.AppendLine("4. Publish the FairyGUI package through the existing Editor/helper-plugin flow.");
            builder.AppendLine();
            builder.AppendLine("This recipe is read-only. It does not modify package.xml, component XML, atlas PNG files, or *_fui.bytes.");
            if (plan.Diagnostics.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Diagnostics");
                builder.AppendLine();
                for (int i = 0; i < plan.Diagnostics.Count; i++)
                {
                    FairyGuiArtSliceOperationDiagnostic diagnostic = plan.Diagnostics[i];
                    builder.AppendLine("- `" + diagnostic.Code + "` `" + diagnostic.Severity + "`: " + diagnostic.Message);
                }
            }
            return NormalizeNewLines(builder.ToString()).TrimEnd() + "\n";
        }

        private static UiArtFairyGuiIntegrationPlan CreateBlockedPlan(
            UiArtResourceLibraryDescriptor library,
            string resourceId,
            UiArtFairyGuiTargetPackage package,
            List<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            return new UiArtFairyGuiIntegrationPlan(
                SchemaVersion,
                library != null ? library.LibraryId : string.Empty,
                resourceId ?? string.Empty,
                string.Empty,
                string.Empty,
                package,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "FairyGUIEditor",
                "Blocked",
                "Unknown",
                null,
                null,
                false,
                true,
                false,
                false,
                false,
                false,
                false,
                true,
                diagnostics);
        }

        private static UiArtResourceLibraryDescriptor FindLibrary(string repositoryRoot, string value)
        {
            string normalized = NormalizePath(value);
            IReadOnlyList<UiArtResourceLibraryDescriptor> libraries = UiArtResourceLibraryService.ListLibraries(repositoryRoot);
            for (int i = 0; i < libraries.Count; i++)
            {
                UiArtResourceLibraryDescriptor library = libraries[i];
                if (string.Equals(library.LibraryId, normalized, StringComparison.Ordinal)
                    || string.Equals(library.ResourceRoot, normalized, StringComparison.Ordinal)
                    || string.Equals(library.CatalogPath, normalized, StringComparison.Ordinal))
                {
                    return library;
                }
            }

            return null;
        }

        private static UiArtResource FindResource(UiArtResourceLibraryCatalog catalog, string resourceId)
        {
            UiArtResource[] resources = catalog != null && catalog.Resources != null ? catalog.Resources : Array.Empty<UiArtResource>();
            for (int i = 0; i < resources.Length; i++)
            {
                if (resources[i] != null && string.Equals(resources[i].ResourceId, resourceId, StringComparison.Ordinal))
                    return resources[i];
            }

            return null;
        }

        private static UiArtFairyGuiTargetPackage FindTargetPackage(string repositoryRoot, string value)
        {
            string normalized = NormalizePath(value);
            IReadOnlyList<UiArtFairyGuiTargetPackage> packages = ListTargetPackages(repositoryRoot);
            for (int i = 0; i < packages.Count; i++)
            {
                UiArtFairyGuiTargetPackage package = packages[i];
                if (string.Equals(package.PackageId, normalized, StringComparison.Ordinal)
                    || string.Equals(package.PackageSourcePath, normalized, StringComparison.Ordinal)
                    || string.Equals(package.PackageXmlPath, normalized, StringComparison.Ordinal))
                {
                    return package;
                }
            }

            return null;
        }

        private static string EvaluateUsageStatus(
            string repositoryRoot,
            UiArtFairyGuiTargetPackage package,
            PackageImage existing,
            UiArtResource resource,
            List<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            if (existing == null)
                return "NotImported";

            string catalogPath = NormalizePath(package.PackageSourcePath + "/ui-art-slices.json");
            if (!File.Exists(ResolvePath(repositoryRoot, catalogPath)))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Info(
                    "UIARTINT108",
                    "The target package has no art-slice sidecar for usage drift comparison.",
                    catalogPath));
                return "Untracked";
            }

            FairyGuiArtSliceCatalog catalog;
            try
            {
                catalog = FairyGuiArtSliceCatalogService.ReadCatalogFile(repositoryRoot, catalogPath);
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Warning(
                    "UIARTINT109",
                    "The target package art-slice sidecar could not be read.",
                    catalogPath));
                return "Untracked";
            }

            FairyGuiArtSlice slice = FindCatalogSlice(catalog, existing);
            if (slice == null)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Info(
                    "UIARTINT110",
                    "The imported image is not tracked by the target package art-slice sidecar.",
                    catalogPath));
                return "Untracked";
            }

            if (UsageMatches(resource.RecommendedFairyGuiUsage, slice))
                return "Aligned";

            diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Warning(
                "UIARTINT111",
                "The target package sidecar usage parameters differ from the resource-library recommendation.",
                catalogPath));
            return "Drift";
        }

        private static FairyGuiArtSlice FindCatalogSlice(FairyGuiArtSliceCatalog catalog, PackageImage existing)
        {
            FairyGuiArtSlice[] slices = catalog != null && catalog.Slices != null ? catalog.Slices : Array.Empty<FairyGuiArtSlice>();
            for (int i = 0; i < slices.Length; i++)
            {
                FairyGuiArtSlice slice = slices[i];
                if (slice == null)
                    continue;
                if (string.Equals(slice.FguiResourceId, existing.ResourceId, StringComparison.Ordinal)
                    || string.Equals(NormalizePath(slice.ExpectedSourcePath), existing.SourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    return slice;
                }
            }

            return null;
        }

        private static bool UsageMatches(UiArtRecommendedFairyGuiUsage expected, FairyGuiArtSlice actual)
        {
            if (expected == null || actual == null || actual.RecommendedUsage == null)
                return false;
            if (!string.Equals(expected.Policy, actual.RecommendedUsage.Policy, StringComparison.Ordinal)
                || expected.PreserveAspect != actual.RecommendedUsage.PreserveAspect)
            {
                return false;
            }

            int expectedWidth = expected.DesignSize != null ? expected.DesignSize.Width : 0;
            int expectedHeight = expected.DesignSize != null ? expected.DesignSize.Height : 0;
            int actualWidth = actual.RecommendedUsage.LogicalSize != null ? actual.RecommendedUsage.LogicalSize.Width : 0;
            int actualHeight = actual.RecommendedUsage.LogicalSize != null ? actual.RecommendedUsage.LogicalSize.Height : 0;
            if (expectedWidth != actualWidth || expectedHeight != actualHeight)
                return false;

            if (!string.Equals(expected.Policy, "NineSlice", StringComparison.Ordinal))
                return true;
            if (expected.Scale9Grid == null || actual.Scale9Grid == null)
                return false;
            return expected.Scale9Grid.X == actual.Scale9Grid.X
                && expected.Scale9Grid.Y == actual.Scale9Grid.Y
                && expected.Scale9Grid.Width == actual.Scale9Grid.Width
                && expected.Scale9Grid.Height == actual.Scale9Grid.Height;
        }

        private static void AppendUsageRecipe(StringBuilder builder, UiArtRecommendedFairyGuiUsage usage)
        {
            if (usage == null)
            {
                builder.AppendLine("- No recommended usage is declared.");
                return;
            }

            builder.AppendLine("- Policy: `" + (usage.Policy ?? string.Empty) + "`");
            int width = usage.DesignSize != null ? usage.DesignSize.Width : 0;
            int height = usage.DesignSize != null ? usage.DesignSize.Height : 0;
            builder.AppendLine("- Design size: `" + width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- Preserve aspect: `" + (usage.PreserveAspect ? "true" : "false") + "`");
            if (usage.Scale9Grid != null)
            {
                builder.AppendLine("- scale9Grid: `"
                    + usage.Scale9Grid.X.ToString(CultureInfo.InvariantCulture) + ","
                    + usage.Scale9Grid.Y.ToString(CultureInfo.InvariantCulture) + ","
                    + usage.Scale9Grid.Width.ToString(CultureInfo.InvariantCulture) + ","
                    + usage.Scale9Grid.Height.ToString(CultureInfo.InvariantCulture) + "`");
            }
        }

        private static List<PackageImage> ReadPackageImages(string packageXmlPath)
        {
            XDocument document = XDocument.Load(packageXmlPath, LoadOptions.None);
            var images = new List<PackageImage>();
            foreach (XElement element in document.Descendants())
            {
                if (!string.Equals(element.Name.LocalName, "image", StringComparison.Ordinal))
                    continue;

                string resourceId = (string)element.Attribute("id") ?? string.Empty;
                string fileName = (string)element.Attribute("name") ?? string.Empty;
                string folder = NormalizePath((string)element.Attribute("path") ?? string.Empty).Trim('/');
                string sourcePath = string.IsNullOrWhiteSpace(folder) ? fileName : folder + "/" + fileName;
                images.Add(new PackageImage(resourceId, fileName, NormalizePath(sourcePath)));
            }

            images.Sort(ComparePackageImages);
            return images;
        }

        private static PackageImage FindExactSource(List<PackageImage> images, string sourcePath)
        {
            for (int i = 0; i < images.Count; i++)
            {
                if (string.Equals(images[i].SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
                    return images[i];
            }

            return null;
        }

        private static PackageImage FindByFileName(List<PackageImage> images, string fileName)
        {
            for (int i = 0; i < images.Count; i++)
            {
                if (string.Equals(images[i].FileName, fileName, StringComparison.OrdinalIgnoreCase))
                    return images[i];
            }

            return null;
        }

        private static PackageImage FindByContentHash(string packageRoot, List<PackageImage> images, string sourceHash)
        {
            for (int i = 0; i < images.Count; i++)
            {
                string path = ResolvePath(packageRoot, images[i].SourcePath);
                if (!IsPathAtOrUnderRoot(packageRoot, path))
                    continue;
                string hash = ComputeFileHash(path);
                if (!string.IsNullOrWhiteSpace(hash) && string.Equals(hash, sourceHash, StringComparison.OrdinalIgnoreCase))
                    return images[i];
            }

            return null;
        }

        private static string BuildSuggestedPackageFolder(string libraryId, string relativePath)
        {
            string normalized = NormalizePath(relativePath).Trim('/');
            if (normalized.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("images/".Length);
            string directory = NormalizePath(Path.GetDirectoryName(normalized) ?? string.Empty).Trim('/');
            string folder = "Images/" + SanitizeSegment(libraryId);
            return string.IsNullOrWhiteSpace(directory) ? folder : folder + "/" + directory;
        }

        private static string SanitizeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "UiLibrary";
            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_');
            }
            return builder.ToString();
        }

        private static string ReadPackageDescriptionId(string packageXmlPath)
        {
            try
            {
                XDocument document = XDocument.Load(packageXmlPath, LoadOptions.None);
                return document.Root != null ? (string)document.Root.Attribute("id") ?? string.Empty : string.Empty;
            }
            catch (Exception exception) when (exception is IOException || exception is System.Xml.XmlException)
            {
                return string.Empty;
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

        private static string ComputeFileHash(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return string.Empty;
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(File.ReadAllBytes(path));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static string ResolvePath(string root, string path)
        {
            string relative = NormalizePath(path).Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(root ?? string.Empty, relative));
        }

        private static string ToRepositoryRelativePath(string repositoryRoot, string absolutePath)
        {
            string root = Path.GetFullPath(repositoryRoot ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(absolutePath ?? string.Empty);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return NormalizePath(full);
            return NormalizePath(full.Substring(root.Length));
        }

        private static string NormalizePath(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
        }

        private static string NormalizeNewLines(string value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static bool IsPathAtOrUnderRoot(string rootPath, string path)
        {
            string fullRoot = Path.GetFullPath(rootPath ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path ?? string.Empty);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareTargetPackages(UiArtFairyGuiTargetPackage left, UiArtFairyGuiTargetPackage right)
        {
            return string.Compare(left.PackageId, right.PackageId, StringComparison.Ordinal);
        }

        private static int ComparePackageImages(PackageImage left, PackageImage right)
        {
            int path = string.Compare(left.SourcePath, right.SourcePath, StringComparison.Ordinal);
            return path != 0 ? path : string.Compare(left.ResourceId, right.ResourceId, StringComparison.Ordinal);
        }

        private sealed class PackageImage
        {
            public PackageImage(string resourceId, string fileName, string sourcePath)
            {
                ResourceId = resourceId ?? string.Empty;
                FileName = fileName ?? string.Empty;
                SourcePath = sourcePath ?? string.Empty;
            }

            public string ResourceId { get; }
            public string FileName { get; }
            public string SourcePath { get; }
        }
    }

    public sealed class UiArtFairyGuiTargetPackage
    {
        public UiArtFairyGuiTargetPackage(string packageId, string packageDescriptionId, string packageSourcePath, string packageXmlPath, string displayName)
        {
            PackageId = packageId ?? string.Empty;
            PackageDescriptionId = packageDescriptionId ?? string.Empty;
            PackageSourcePath = (packageSourcePath ?? string.Empty).Replace('\\', '/');
            PackageXmlPath = (packageXmlPath ?? string.Empty).Replace('\\', '/');
            DisplayName = displayName ?? string.Empty;
        }

        [JsonPropertyName("packageId")]
        public string PackageId { get; }

        [JsonPropertyName("packageDescriptionId")]
        public string PackageDescriptionId { get; }

        [JsonPropertyName("packageSourcePath")]
        public string PackageSourcePath { get; }

        [JsonPropertyName("packageXmlPath")]
        public string PackageXmlPath { get; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; }
    }

    public sealed class UiArtFairyGuiExistingImage
    {
        public UiArtFairyGuiExistingImage(string resourceId, string fileName, string sourcePath, string contentHash, bool contentMatches)
        {
            ResourceId = resourceId ?? string.Empty;
            FileName = fileName ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            ContentHash = contentHash ?? string.Empty;
            ContentMatches = contentMatches;
        }

        [JsonPropertyName("fguiResourceId")]
        public string ResourceId { get; }

        [JsonPropertyName("fileName")]
        public string FileName { get; }

        [JsonPropertyName("sourcePath")]
        public string SourcePath { get; }

        [JsonPropertyName("contentHash")]
        public string ContentHash { get; }

        [JsonPropertyName("contentMatches")]
        public bool ContentMatches { get; }
    }

    public sealed class UiArtFairyGuiIntegrationPlan
    {
        public UiArtFairyGuiIntegrationPlan(
            string schemaVersion,
            string libraryId,
            string resourceId,
            string sourcePath,
            string sourceContentHash,
            UiArtFairyGuiTargetPackage targetPackage,
            string suggestedResourceName,
            string suggestedPackageFolder,
            string suggestedSourcePath,
            string suggestedTargetPath,
            string resourceIdOwnership,
            string status,
            string usageStatus,
            UiArtFairyGuiExistingImage existingImage,
            UiArtRecommendedFairyGuiUsage recommendedFairyGuiUsage,
            bool requiresFairyGuiImport,
            bool requiresFairyGuiEditor,
            bool requiresFairyGuiPublish,
            bool wouldModifyCatalog,
            bool wouldModifyPng,
            bool wouldModifyFairyGuiXml,
            bool wouldModifyPublishedOutput,
            bool blocked,
            IReadOnlyList<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            SchemaVersion = schemaVersion ?? string.Empty;
            LibraryId = libraryId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            SourceContentHash = sourceContentHash ?? string.Empty;
            TargetPackage = targetPackage;
            SuggestedResourceName = suggestedResourceName ?? string.Empty;
            SuggestedPackageFolder = suggestedPackageFolder ?? string.Empty;
            SuggestedSourcePath = suggestedSourcePath ?? string.Empty;
            SuggestedTargetPath = suggestedTargetPath ?? string.Empty;
            ResourceIdOwnership = resourceIdOwnership ?? string.Empty;
            Status = status ?? string.Empty;
            UsageStatus = usageStatus ?? string.Empty;
            ExistingImage = existingImage;
            RecommendedFairyGuiUsage = recommendedFairyGuiUsage;
            RequiresFairyGuiImport = requiresFairyGuiImport;
            RequiresFairyGuiEditor = requiresFairyGuiEditor;
            RequiresFairyGuiPublish = requiresFairyGuiPublish;
            WouldModifyCatalog = wouldModifyCatalog;
            WouldModifyPng = wouldModifyPng;
            WouldModifyFairyGuiXml = wouldModifyFairyGuiXml;
            WouldModifyPublishedOutput = wouldModifyPublishedOutput;
            Blocked = blocked;
            Diagnostics = diagnostics ?? Array.Empty<FairyGuiArtSliceOperationDiagnostic>();
        }

        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; }

        [JsonPropertyName("libraryId")]
        public string LibraryId { get; }

        [JsonPropertyName("resourceId")]
        public string ResourceId { get; }

        [JsonPropertyName("sourcePath")]
        public string SourcePath { get; }

        [JsonPropertyName("sourceContentHash")]
        public string SourceContentHash { get; }

        [JsonPropertyName("targetPackage")]
        public UiArtFairyGuiTargetPackage TargetPackage { get; }

        [JsonPropertyName("suggestedResourceName")]
        public string SuggestedResourceName { get; }

        [JsonPropertyName("suggestedPackageFolder")]
        public string SuggestedPackageFolder { get; }

        [JsonPropertyName("suggestedSourcePath")]
        public string SuggestedSourcePath { get; }

        [JsonPropertyName("suggestedTargetPath")]
        public string SuggestedTargetPath { get; }

        [JsonPropertyName("resourceIdOwnership")]
        public string ResourceIdOwnership { get; }

        [JsonPropertyName("status")]
        public string Status { get; }

        [JsonPropertyName("usageStatus")]
        public string UsageStatus { get; }

        [JsonPropertyName("existingImage")]
        public UiArtFairyGuiExistingImage ExistingImage { get; }

        [JsonPropertyName("recommendedFairyGuiUsage")]
        public UiArtRecommendedFairyGuiUsage RecommendedFairyGuiUsage { get; }

        [JsonPropertyName("requiresFairyGuiImport")]
        public bool RequiresFairyGuiImport { get; }

        [JsonPropertyName("requiresFairyGuiEditor")]
        public bool RequiresFairyGuiEditor { get; }

        [JsonPropertyName("requiresFairyGuiPublish")]
        public bool RequiresFairyGuiPublish { get; }

        [JsonPropertyName("wouldModifyCatalog")]
        public bool WouldModifyCatalog { get; }

        [JsonPropertyName("wouldModifyPng")]
        public bool WouldModifyPng { get; }

        [JsonPropertyName("wouldModifyFairyGuiXml")]
        public bool WouldModifyFairyGuiXml { get; }

        [JsonPropertyName("wouldModifyPublishedOutput")]
        public bool WouldModifyPublishedOutput { get; }

        [JsonPropertyName("blocked")]
        public bool Blocked { get; }

        [JsonPropertyName("diagnostics")]
        public IReadOnlyList<FairyGuiArtSliceOperationDiagnostic> Diagnostics { get; }
    }
}

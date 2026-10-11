using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace MxFramework.Authoring.FairyGuiArtSlices
{
    public static partial class FairyGuiArtSliceCatalogService
    {
        public const string CharacterTestPackageId = "MxCharacterTestHud";
        public const string CharacterTestPackageSourcePath = "FGUIProject/assets/MxCharacterTestHud";
        public const string CharacterTestCatalogPath = "FGUIProject/assets/MxCharacterTestHud/ui-art-slices.json";

        private const string CurrentSchemaVersion = "mx.uiArtSliceCatalog.v1";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
            WriteIndented = true
        };

        public static FairyGuiArtSliceValidationResult ValidateCharacterTest(string repositoryRoot)
        {
            return ValidatePackage(repositoryRoot, CharacterTestCatalogPath);
        }

        public static FairyGuiArtSliceValidationResult ValidatePackage(string repositoryRoot, string catalogPath)
        {
            string root = repositoryRoot ?? string.Empty;
            string normalizedCatalogPath = NormalizePath(catalogPath);
            string absoluteCatalogPath = ResolvePath(root, normalizedCatalogPath);
            var diagnostics = new List<FairyGuiArtSliceDiagnostic>();

            FairyGuiArtSliceCatalog catalog = LoadCatalog(absoluteCatalogPath, normalizedCatalogPath, diagnostics);
            string packageSourcePath = catalog != null && !string.IsNullOrWhiteSpace(catalog.PackageSourcePath)
                ? NormalizePath(catalog.PackageSourcePath)
                : CharacterTestPackageSourcePath;
            string packageId = catalog != null && !string.IsNullOrWhiteSpace(catalog.PackageId)
                ? catalog.PackageId
                : CharacterTestPackageId;
            string packageRoot = ResolvePath(root, packageSourcePath);
            string packageXmlPath = Path.Combine(packageRoot, "package.xml");

            List<FairyGuiPackageImageResource> images = ReadPackageImages(packageId, packageSourcePath, packageXmlPath, diagnostics);
            List<FairyGuiArtSliceUsagePoint> usages = ScanUsagePoints(packageId, root, packageSourcePath, packageXmlPath, diagnostics);
            ValidateCatalog(catalog, packageId, root, packageSourcePath, images, usages, diagnostics);

            AddInfo(
                diagnostics,
                "UIART202",
                "CoverageSummary",
                packageId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "Package images=" + images.Count.ToString(CultureInfo.InvariantCulture)
                    + ", catalog slices=" + (catalog != null && catalog.Slices != null ? catalog.Slices.Length : 0).ToString(CultureInfo.InvariantCulture)
                    + ", direct image usages=" + usages.Count.ToString(CultureInfo.InvariantCulture) + ".",
                new FairyGuiArtSliceLocation(normalizedCatalogPath, string.Empty, string.Empty));

            SortDiagnostics(diagnostics);
            return new FairyGuiArtSliceValidationResult(packageId, packageSourcePath, images, usages, diagnostics);
        }

        public static string GenerateMarkdownReport(FairyGuiArtSliceValidationResult result)
        {
            if (result == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("# FairyGUI Art Slice Report");
            builder.AppendLine();
            builder.AppendLine("Package: `" + result.PackageId + "`");
            builder.AppendLine("Source: `" + result.PackageSourcePath + "`");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine("- Images: " + result.PackageImages.Count.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- Direct image usages: " + result.UsagePoints.Count.ToString(CultureInfo.InvariantCulture));
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
                    FairyGuiArtSliceDiagnostic diagnostic = result.Diagnostics[i];
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

                    if (!string.IsNullOrWhiteSpace(diagnostic.ComponentPath))
                    {
                        builder.Append(" component=`");
                        builder.Append(diagnostic.ComponentPath);
                        builder.Append("`");
                    }

                    if (!string.IsNullOrWhiteSpace(diagnostic.NodeId))
                    {
                        builder.Append(" node=`");
                        builder.Append(diagnostic.NodeId);
                        builder.Append("`");
                    }

                    builder.Append(": ");
                    builder.AppendLine(diagnostic.Message);
                }
            }

            builder.AppendLine();
            builder.AppendLine("## Package Images");
            builder.AppendLine();
            for (int i = 0; i < result.PackageImages.Count; i++)
            {
                FairyGuiPackageImageResource image = result.PackageImages[i];
                builder.Append("- `");
                builder.Append(image.ResourceId);
                builder.Append("` ");
                builder.AppendLine(image.SourcePath);
            }

            builder.AppendLine();
            builder.AppendLine("## Direct Image Usages");
            builder.AppendLine();
            for (int i = 0; i < result.UsagePoints.Count; i++)
            {
                FairyGuiArtSliceUsagePoint usage = result.UsagePoints[i];
                builder.Append("- `");
                builder.Append(usage.ResourceId);
                builder.Append("` ");
                builder.Append(usage.ComponentPath);
                builder.Append(" node=`");
                builder.Append(usage.NodeId);
                builder.Append("` size=");
                builder.Append(usage.Size.Width.ToString(CultureInfo.InvariantCulture));
                builder.Append("x");
                builder.Append(usage.Size.Height.ToString(CultureInfo.InvariantCulture));
                builder.Append(" preserveAspect=");
                builder.AppendLine(usage.PreserveAspect ? "true" : "false");
            }

            return builder.ToString();
        }

        public static string GenerateMarkdownSummary(FairyGuiArtSliceValidationResult result)
        {
            if (result == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("## FairyGUI Art Slice Summary");
            builder.AppendLine();
            builder.AppendLine("Package: `" + result.PackageId + "`");
            builder.AppendLine("- Images: " + result.PackageImages.Count.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- Direct image usages: " + result.UsagePoints.Count.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- Errors: " + result.ErrorCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- Warnings: " + result.WarningCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- Infos: " + result.InfoCount.ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        public static string GenerateJsonReport(FairyGuiArtSliceValidationResult result)
        {
            if (result == null)
                return "{}";

            var report = new FairyGuiArtSliceReportDto
            {
                SchemaVersion = "mx.uiArtSliceReport.v1",
                PackageId = result.PackageId,
                PackageSourcePath = result.PackageSourcePath,
                Summary = new FairyGuiArtSliceReportSummaryDto
                {
                    ImageCount = result.PackageImages.Count,
                    UsageCount = result.UsagePoints.Count,
                    ErrorCount = result.ErrorCount,
                    WarningCount = result.WarningCount,
                    InfoCount = result.InfoCount
                },
                Diagnostics = ToDiagnosticDtos(result.Diagnostics),
                PackageImages = ToImageDtos(result.PackageImages),
                UsagePoints = ToUsageDtos(result.UsagePoints)
            };

            return JsonSerializer.Serialize(report, JsonOptions) + Environment.NewLine;
        }

        private static FairyGuiArtSliceCatalog LoadCatalog(
            string absoluteCatalogPath,
            string catalogPath,
            List<FairyGuiArtSliceDiagnostic> diagnostics)
        {
            if (!File.Exists(absoluteCatalogPath))
            {
                AddError(
                    diagnostics,
                    "UIART000",
                    "CatalogInvalid",
                    CharacterTestPackageId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "FairyGUI art slice catalog is missing.",
                    new FairyGuiArtSliceLocation(catalogPath, string.Empty, string.Empty));
                return null;
            }

            try
            {
                string text = File.ReadAllText(absoluteCatalogPath);
                FairyGuiArtSliceCatalog catalog = JsonSerializer.Deserialize<FairyGuiArtSliceCatalog>(text, JsonOptions);
                if (catalog == null)
                {
                    AddError(
                        diagnostics,
                        "UIART000",
                        "CatalogInvalid",
                        CharacterTestPackageId,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        "FairyGUI art slice catalog JSON is empty.",
                        new FairyGuiArtSliceLocation(catalogPath, string.Empty, string.Empty));
                }

                return catalog;
            }
            catch (JsonException ex)
            {
                AddError(
                    diagnostics,
                    "UIART000",
                    "CatalogInvalid",
                    CharacterTestPackageId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "FairyGUI art slice catalog JSON could not be parsed: " + ex.Message,
                    new FairyGuiArtSliceLocation(catalogPath, string.Empty, string.Empty));
                return null;
            }
        }

        private static List<FairyGuiPackageImageResource> ReadPackageImages(
            string packageId,
            string packageSourcePath,
            string packageXmlPath,
            List<FairyGuiArtSliceDiagnostic> diagnostics)
        {
            var images = new List<FairyGuiPackageImageResource>();
            if (!File.Exists(packageXmlPath))
            {
                AddError(
                    diagnostics,
                    "UIART000",
                    "PackageXmlMissing",
                    packageId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "FairyGUI package.xml is missing.",
                    new FairyGuiArtSliceLocation(NormalizePath(Path.Combine(packageSourcePath, "package.xml")), string.Empty, string.Empty));
                return images;
            }

            XDocument document = XDocument.Load(packageXmlPath);
            foreach (XElement image in document.Descendants("image"))
            {
                string id = Attribute(image, "id");
                string name = Attribute(image, "name");
                string path = Attribute(image, "path");
                string sourcePath = BuildPackageRelativePath(path, name);
                images.Add(new FairyGuiPackageImageResource(id, name, sourcePath, NormalizePath(Path.Combine(packageSourcePath, "package.xml"))));
            }

            images.Sort(ComparePackageImages);
            return images;
        }

        private static List<FairyGuiArtSliceUsagePoint> ScanUsagePoints(
            string packageId,
            string repositoryRoot,
            string packageSourcePath,
            string packageXmlPath,
            List<FairyGuiArtSliceDiagnostic> diagnostics)
        {
            var usages = new List<FairyGuiArtSliceUsagePoint>();
            if (!File.Exists(packageXmlPath))
                return usages;

            string packageRoot = ResolvePath(repositoryRoot, packageSourcePath);
            XDocument packageDocument = XDocument.Load(packageXmlPath);
            foreach (XElement component in packageDocument.Descendants("component"))
            {
                string componentName = Attribute(component, "name");
                string componentPath = BuildPackageRelativePath(Attribute(component, "path"), componentName);
                string absoluteComponentPath = Path.Combine(packageRoot, componentPath.Replace('/', Path.DirectorySeparatorChar));
                string repositoryComponentPath = NormalizePath(packageSourcePath + "/" + componentPath);
                if (!File.Exists(absoluteComponentPath))
                {
                    AddWarning(
                        diagnostics,
                        "UIART107",
                        "KnownScannerLimitation",
                        packageId,
                        string.Empty,
                        string.Empty,
                        repositoryComponentPath,
                        string.Empty,
                        "Component XML could not be scanned for art slice usages.",
                        new FairyGuiArtSliceLocation(repositoryComponentPath, string.Empty, string.Empty));
                    continue;
                }

                XDocument componentDocument = XDocument.Load(absoluteComponentPath);
                foreach (XElement image in componentDocument.Descendants("image"))
                {
                    string resourceId = Attribute(image, "src");
                    if (string.IsNullOrWhiteSpace(resourceId))
                        continue;

                    FairyGuiPoint position = ParsePoint(Attribute(image, "xy"));
                    FairyGuiSize size = ParseSize(Attribute(image, "size"));
                    bool preserveAspect = string.Equals(Attribute(image, "aspect"), "true", StringComparison.OrdinalIgnoreCase);
                    usages.Add(new FairyGuiArtSliceUsagePoint(
                        componentName,
                        repositoryComponentPath,
                        Attribute(image, "name"),
                        Attribute(image, "id"),
                        resourceId,
                        NormalizePath(Attribute(image, "fileName")),
                        position,
                        size,
                        preserveAspect,
                        ParseRelations(image),
                        ParseGearDisplay(image)));
                }
            }

            usages.Sort(CompareUsagePoints);
            return usages;
        }

        private static void ValidateCatalog(
            FairyGuiArtSliceCatalog catalog,
            string packageId,
            string repositoryRoot,
            string packageSourcePath,
            List<FairyGuiPackageImageResource> images,
            List<FairyGuiArtSliceUsagePoint> usages,
            List<FairyGuiArtSliceDiagnostic> diagnostics)
        {
            if (catalog == null)
                return;

            if (!string.Equals(catalog.SchemaVersion ?? string.Empty, CurrentSchemaVersion, StringComparison.Ordinal))
            {
                AddError(
                    diagnostics,
                    "UIART000",
                    "CatalogInvalid",
                    packageId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "Unsupported FairyGUI art slice catalog schema version: " + (catalog.SchemaVersion ?? string.Empty) + ".",
                    new FairyGuiArtSliceLocation(CharacterTestCatalogPath, string.Empty, string.Empty));
            }

            var imagesById = new Dictionary<string, FairyGuiPackageImageResource>(StringComparer.Ordinal);
            for (int i = 0; i < images.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(images[i].ResourceId))
                    imagesById[images[i].ResourceId] = images[i];
            }

            FairyGuiArtSlice[] slices = catalog.Slices ?? Array.Empty<FairyGuiArtSlice>();
            FairyGuiArtSliceGroup[] groups = catalog.Groups ?? Array.Empty<FairyGuiArtSliceGroup>();
            var sliceIds = new HashSet<string>(StringComparer.Ordinal);
            var resourceIds = new HashSet<string>(StringComparer.Ordinal);
            var catalogResourceIds = new HashSet<string>(StringComparer.Ordinal);
            var slicesByResourceId = new Dictionary<string, FairyGuiArtSlice>(StringComparer.Ordinal);
            var usageCountByResourceId = CountUsagesByResourceId(usages);

            for (int i = 0; i < slices.Length; i++)
            {
                FairyGuiArtSlice slice = slices[i];
                if (slice == null)
                    continue;

                string sliceId = slice.SliceId ?? string.Empty;
                string resourceId = slice.FguiResourceId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sliceId) || !sliceIds.Add(sliceId))
                {
                    AddError(diagnostics, "UIART005", "DuplicateSliceId", packageId, sliceId, resourceId, string.Empty, string.Empty, "Duplicate or empty sliceId: " + sliceId + ".", LocationForCatalogSlice(sliceId, resourceId));
                }

                if (string.IsNullOrWhiteSpace(resourceId) || !resourceIds.Add(resourceId))
                {
                    AddError(diagnostics, "UIART006", "DuplicateFguiResourceId", packageId, sliceId, resourceId, string.Empty, string.Empty, "Duplicate or empty fguiResourceId: " + resourceId + ".", LocationForCatalogSlice(sliceId, resourceId));
                }
                else
                {
                    catalogResourceIds.Add(resourceId);
                    slicesByResourceId[resourceId] = slice;
                }

                FairyGuiPackageImageResource image;
                if (!imagesById.TryGetValue(resourceId, out image))
                {
                    AddError(diagnostics, "UIART004", "FguiResourceIdMissing", packageId, sliceId, resourceId, string.Empty, string.Empty, "Catalog slice references an image resource id that is missing from package.xml.", LocationForCatalogSlice(sliceId, resourceId));
                    continue;
                }

                ValidateSliceFields(slice, packageId, image, repositoryRoot, packageSourcePath, usageCountByResourceId, diagnostics);
            }

            if (catalog.StrictCoverage)
            {
                for (int i = 0; i < images.Count; i++)
                {
                    FairyGuiPackageImageResource image = images[i];
                    if (!catalogResourceIds.Contains(image.ResourceId))
                    {
                        AddError(diagnostics, "UIART001", "PackageImageUncataloged", packageId, string.Empty, image.ResourceId, string.Empty, string.Empty, "Package image resource is not covered by catalog.", new FairyGuiArtSliceLocation(image.PackageXmlPath, image.ResourceId, string.Empty));
                    }
                }
            }

            ValidateUsageAgainstRecommendedPolicy(packageId, slicesByResourceId, usages, diagnostics);
            ValidateGroups(packageId, groups, slices, repositoryRoot, packageSourcePath, diagnostics);
        }

        private static void ValidateSliceFields(
            FairyGuiArtSlice slice,
            string packageId,
            FairyGuiPackageImageResource image,
            string repositoryRoot,
            string packageSourcePath,
            Dictionary<string, int> usageCountByResourceId,
            List<FairyGuiArtSliceDiagnostic> diagnostics)
        {
            string sliceId = slice.SliceId ?? string.Empty;
            string resourceId = slice.FguiResourceId ?? string.Empty;
            string expectedSourcePath = NormalizePath(slice.ExpectedSourcePath);
            if (!string.IsNullOrWhiteSpace(expectedSourcePath) && !string.Equals(expectedSourcePath, image.SourcePath, StringComparison.Ordinal))
            {
                AddError(diagnostics, "UIART002", "CatalogExpectedSourcePathMismatch", packageId, sliceId, resourceId, string.Empty, string.Empty, "Catalog expectedSourcePath does not match package.xml source path. expected=" + expectedSourcePath + " actual=" + image.SourcePath + ".", LocationForCatalogSlice(sliceId, resourceId));
            }

            string absoluteImagePath = ResolvePath(repositoryRoot, NormalizePath(packageSourcePath + "/" + image.SourcePath));
            if (!File.Exists(absoluteImagePath))
            {
                AddError(diagnostics, "UIART003", "CatalogSourceMissing", packageId, sliceId, resourceId, string.Empty, string.Empty, "Catalog image source PNG is missing: " + image.SourcePath + ".", new FairyGuiArtSliceLocation(image.SourcePath, resourceId, string.Empty));
            }

            if (!IsValidCategory(slice.Category))
            {
                AddError(diagnostics, "UIART007", "InvalidUsagePolicyForSlice", packageId, sliceId, resourceId, string.Empty, string.Empty, "Catalog slice category is unsupported: " + (slice.Category ?? string.Empty) + ".", LocationForCatalogSlice(sliceId, resourceId));
            }

            if (string.Equals(slice.Category ?? string.Empty, "Ignored", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(slice.IgnoreReason))
            {
                AddError(diagnostics, "UIART007", "InvalidUsagePolicyForSlice", packageId, sliceId, resourceId, string.Empty, string.Empty, "Ignored slices must include ignoreReason.", LocationForCatalogSlice(sliceId, resourceId));
            }

            string policy = slice.RecommendedUsage != null ? slice.RecommendedUsage.Policy : string.Empty;
            if (!string.IsNullOrWhiteSpace(policy) && !IsValidUsagePolicy(policy))
            {
                AddError(diagnostics, "UIART007", "InvalidUsagePolicyForSlice", packageId, sliceId, resourceId, string.Empty, string.Empty, "Catalog slice usage policy is unsupported: " + policy + ".", LocationForCatalogSlice(sliceId, resourceId));
            }

            if (string.Equals(policy, "NineSlice", StringComparison.Ordinal) && !IsValidScale9Grid(slice.Scale9Grid, absoluteImagePath))
            {
                AddError(diagnostics, "UIART008", "NineSliceGridInvalid", packageId, sliceId, resourceId, string.Empty, string.Empty, "NineSlice usage requires a non-null scale9Grid inside the source PNG bounds.", LocationForCatalogSlice(sliceId, resourceId));
            }

            if (slice.KnownExternalUsages != null && slice.KnownExternalUsages.Length > 0)
            {
                AddInfo(diagnostics, "UIART203", "ExternalUsageDeclared", packageId, sliceId, resourceId, string.Empty, string.Empty, "Catalog declares known external usage for this slice.", LocationForCatalogSlice(sliceId, resourceId));
            }

            if (slice.Replacement != null && slice.Replacement.ReplaceTogether)
            {
                AddInfo(diagnostics, "UIART201", "ReplaceAsGroupHint", packageId, sliceId, resourceId, string.Empty, string.Empty, "Slice should be replaced together with its group.", LocationForCatalogSlice(sliceId, resourceId));
            }

            int usageCount;
            if ((!usageCountByResourceId.TryGetValue(resourceId, out usageCount) || usageCount == 0)
                && (slice.KnownExternalUsages == null || slice.KnownExternalUsages.Length == 0)
                && !string.Equals(slice.Category ?? string.Empty, "Ignored", StringComparison.Ordinal))
            {
                AddWarning(diagnostics, "UIART101", "OrphanImageResource", packageId, sliceId, resourceId, string.Empty, string.Empty, "Package image resource has no direct image usage and no knownExternalUsages declaration.", LocationForCatalogSlice(sliceId, resourceId));
            }
        }

        private static void ValidateUsageAgainstRecommendedPolicy(
            string packageId,
            Dictionary<string, FairyGuiArtSlice> slicesByResourceId,
            List<FairyGuiArtSliceUsagePoint> usages,
            List<FairyGuiArtSliceDiagnostic> diagnostics)
        {
            for (int i = 0; i < usages.Count; i++)
            {
                FairyGuiArtSliceUsagePoint usage = usages[i];
                FairyGuiArtSlice slice;
                if (!slicesByResourceId.TryGetValue(usage.ResourceId, out slice) || slice == null || slice.RecommendedUsage == null)
                    continue;

                bool requiresAspect = string.Equals(slice.RecommendedUsage.Policy ?? string.Empty, "PreserveAspect", StringComparison.Ordinal)
                    || slice.RecommendedUsage.PreserveAspect;
                if (requiresAspect && !usage.PreserveAspect)
                {
                    AddWarning(diagnostics, "UIART102", "UsageAspectMismatch", packageId, slice.SliceId, usage.ResourceId, usage.ComponentPath, usage.NodeId, "Observed image usage does not preserve aspect as recommended by catalog.", new FairyGuiArtSliceLocation(usage.ComponentPath, usage.ResourceId, usage.NodeId));
                }
            }
        }

        private static void ValidateGroups(
            string packageId,
            FairyGuiArtSliceGroup[] groups,
            FairyGuiArtSlice[] slices,
            string repositoryRoot,
            string packageSourcePath,
            List<FairyGuiArtSliceDiagnostic> diagnostics)
        {
            var slicesByGroup = new Dictionary<string, List<FairyGuiArtSlice>>(StringComparer.Ordinal);
            for (int i = 0; i < slices.Length; i++)
            {
                FairyGuiArtSlice slice = slices[i];
                if (slice == null || string.IsNullOrWhiteSpace(slice.GroupId))
                    continue;

                List<FairyGuiArtSlice> list;
                if (!slicesByGroup.TryGetValue(slice.GroupId, out list))
                {
                    list = new List<FairyGuiArtSlice>();
                    slicesByGroup.Add(slice.GroupId, list);
                }

                list.Add(slice);
            }

            for (int i = 0; i < groups.Length; i++)
            {
                FairyGuiArtSliceGroup group = groups[i];
                if (group == null || string.IsNullOrWhiteSpace(group.GroupId))
                    continue;

                List<FairyGuiArtSlice> groupSlices;
                if (!slicesByGroup.TryGetValue(group.GroupId, out groupSlices))
                    groupSlices = new List<FairyGuiArtSlice>();

                ValidateExpectedGroupRoles(packageId, group, groupSlices, diagnostics);
                if (group.RequireSamePixelSize)
                    ValidateGroupPixelSizes(packageId, group, groupSlices, repositoryRoot, packageSourcePath, diagnostics);
            }
        }

        private static void ValidateExpectedGroupRoles(
            string packageId,
            FairyGuiArtSliceGroup group,
            List<FairyGuiArtSlice> groupSlices,
            List<FairyGuiArtSliceDiagnostic> diagnostics)
        {
            string[] expectedRoles = group.ExpectedRoles ?? Array.Empty<string>();
            for (int i = 0; i < expectedRoles.Length; i++)
            {
                string expectedRole = expectedRoles[i] ?? string.Empty;
                bool found = false;
                for (int j = 0; j < groupSlices.Count; j++)
                {
                    if (string.Equals(groupSlices[j].RoleInGroup ?? string.Empty, expectedRole, StringComparison.Ordinal))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    AddWarning(diagnostics, "UIART103", "ButtonStateGroupIncomplete", packageId, string.Empty, string.Empty, string.Empty, string.Empty, "Group " + group.GroupId + " is missing expected role: " + expectedRole + ".", new FairyGuiArtSliceLocation(CharacterTestCatalogPath, string.Empty, string.Empty));
                }
            }
        }

        private static void ValidateGroupPixelSizes(
            string packageId,
            FairyGuiArtSliceGroup group,
            List<FairyGuiArtSlice> groupSlices,
            string repositoryRoot,
            string packageSourcePath,
            List<FairyGuiArtSliceDiagnostic> diagnostics)
        {
            bool hasReference = false;
            FairyGuiSize reference = default;
            for (int i = 0; i < groupSlices.Count; i++)
            {
                FairyGuiArtSlice slice = groupSlices[i];
                string sourcePath = NormalizePath(slice.ExpectedSourcePath);
                if (string.IsNullOrWhiteSpace(sourcePath))
                    continue;

                string absolutePath = ResolvePath(repositoryRoot, NormalizePath(packageSourcePath + "/" + sourcePath));
                FairyGuiSize size;
                if (!TryReadPngSize(absolutePath, out size))
                    continue;

                if (!hasReference)
                {
                    reference = size;
                    hasReference = true;
                    continue;
                }

                if (reference.Width != size.Width || reference.Height != size.Height)
                {
                    AddWarning(diagnostics, "UIART104", "ButtonStateGroupSizeMismatch", packageId, slice.SliceId, slice.FguiResourceId, string.Empty, string.Empty, "Group " + group.GroupId + " requires same pixel size, but slice has " + size.Width.ToString(CultureInfo.InvariantCulture) + "x" + size.Height.ToString(CultureInfo.InvariantCulture) + " instead of " + reference.Width.ToString(CultureInfo.InvariantCulture) + "x" + reference.Height.ToString(CultureInfo.InvariantCulture) + ".", LocationForCatalogSlice(slice.SliceId, slice.FguiResourceId));
                }
            }
        }

        private static Dictionary<string, int> CountUsagesByResourceId(List<FairyGuiArtSliceUsagePoint> usages)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < usages.Count; i++)
            {
                string resourceId = usages[i].ResourceId ?? string.Empty;
                int count;
                counts.TryGetValue(resourceId, out count);
                counts[resourceId] = count + 1;
            }

            return counts;
        }

        private static bool IsValidCategory(string category)
        {
            switch (category ?? string.Empty)
            {
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

        private static bool IsValidUsagePolicy(string policy)
        {
            switch (policy ?? string.Empty)
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

        private static bool IsValidScale9Grid(FairyGuiScale9Grid grid, string absoluteImagePath)
        {
            if (grid == null)
                return false;

            if (grid.X < 0 || grid.Y < 0 || grid.Width <= 0 || grid.Height <= 0)
                return false;

            FairyGuiSize size;
            if (!TryReadPngSize(absoluteImagePath, out size))
                return false;

            return grid.X + grid.Width <= size.Width && grid.Y + grid.Height <= size.Height;
        }

        private static bool TryReadPngSize(string absolutePath, out FairyGuiSize size)
        {
            size = default;
            if (!File.Exists(absolutePath))
                return false;

            var header = new byte[26];
            using (FileStream stream = File.OpenRead(absolutePath))
            {
                if (stream.Read(header, 0, header.Length) != header.Length)
                    return false;
            }

            if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47)
                return false;

            int width = ReadBigEndianInt32(header, 16);
            int height = ReadBigEndianInt32(header, 20);
            if (width <= 0 || height <= 0)
                return false;

            size = new FairyGuiSize(width, height);
            return true;
        }

        private static int ReadBigEndianInt32(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24)
                | (bytes[offset + 1] << 16)
                | (bytes[offset + 2] << 8)
                | bytes[offset + 3];
        }

        private static FairyGuiPoint ParsePoint(string value)
        {
            int x;
            int y;
            ParsePair(value, out x, out y);
            return new FairyGuiPoint(x, y);
        }

        private static FairyGuiSize ParseSize(string value)
        {
            int width;
            int height;
            ParsePair(value, out width, out height);
            return new FairyGuiSize(width, height);
        }

        private static void ParsePair(string value, out int first, out int second)
        {
            first = 0;
            second = 0;
            string[] parts = (value ?? string.Empty).Split(',');
            if (parts.Length != 2)
                return;

            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out first);
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out second);
        }

        private static FairyGuiRelation[] ParseRelations(XElement image)
        {
            var relations = new List<FairyGuiRelation>();
            foreach (XElement relation in image.Elements("relation"))
            {
                string sidePair = Attribute(relation, "sidePair");
                if (string.IsNullOrWhiteSpace(sidePair))
                    continue;

                string[] entries = sidePair.Split(',');
                for (int i = 0; i < entries.Length; i++)
                {
                    string value = entries[i].Trim();
                    if (value.Length > 0)
                        relations.Add(new FairyGuiRelation(value));
                }
            }

            return relations.ToArray();
        }

        private static FairyGuiGearDisplay ParseGearDisplay(XElement image)
        {
            XElement gear = image.Element("gearDisplay");
            if (gear == null)
                return null;

            return new FairyGuiGearDisplay(Attribute(gear, "controller"), SplitCsv(Attribute(gear, "pages")));
        }

        private static string[] SplitCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();

            string[] parts = value.Split(',');
            var values = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length > 0)
                    values.Add(part);
            }

            return values.ToArray();
        }

        private static string Attribute(XElement element, string name)
        {
            XAttribute attribute = element.Attribute(name);
            return attribute != null ? attribute.Value ?? string.Empty : string.Empty;
        }

        private static string BuildPackageRelativePath(string path, string fileName)
        {
            string folder = NormalizePath(path).Trim('/');
            string name = NormalizePath(fileName).Trim('/');
            if (string.IsNullOrWhiteSpace(folder))
                return name;

            if (string.IsNullOrWhiteSpace(name))
                return folder;

            return folder + "/" + name;
        }

        private static string NormalizePath(string value)
        {
            string path = (value ?? string.Empty).Replace('\\', '/').Trim();
            while (path.Contains("//"))
                path = path.Replace("//", "/");

            return path.TrimStart('/');
        }

        private static string ResolvePath(string root, string relativePath)
        {
            return Path.Combine(root ?? string.Empty, NormalizePath(relativePath).Replace('/', Path.DirectorySeparatorChar));
        }

        private static FairyGuiArtSliceLocation LocationForCatalogSlice(string sliceId, string resourceId)
        {
            return new FairyGuiArtSliceLocation(CharacterTestCatalogPath, resourceId ?? string.Empty, sliceId ?? string.Empty);
        }

        private static void AddError(
            List<FairyGuiArtSliceDiagnostic> diagnostics,
            string code,
            string name,
            string packageId,
            string sliceId,
            string resourceId,
            string componentPath,
            string nodeId,
            string message,
            FairyGuiArtSliceLocation location)
        {
            diagnostics.Add(new FairyGuiArtSliceDiagnostic(code, name, "error", packageId, sliceId, resourceId, componentPath, nodeId, message, location));
        }

        private static void AddWarning(
            List<FairyGuiArtSliceDiagnostic> diagnostics,
            string code,
            string name,
            string packageId,
            string sliceId,
            string resourceId,
            string componentPath,
            string nodeId,
            string message,
            FairyGuiArtSliceLocation location)
        {
            diagnostics.Add(new FairyGuiArtSliceDiagnostic(code, name, "warning", packageId, sliceId, resourceId, componentPath, nodeId, message, location));
        }

        private static void AddInfo(
            List<FairyGuiArtSliceDiagnostic> diagnostics,
            string code,
            string name,
            string packageId,
            string sliceId,
            string resourceId,
            string componentPath,
            string nodeId,
            string message,
            FairyGuiArtSliceLocation location)
        {
            diagnostics.Add(new FairyGuiArtSliceDiagnostic(code, name, "info", packageId, sliceId, resourceId, componentPath, nodeId, message, location));
        }

        private static void SortDiagnostics(List<FairyGuiArtSliceDiagnostic> diagnostics)
        {
            diagnostics.Sort(CompareDiagnostics);
        }

        private static int CompareDiagnostics(FairyGuiArtSliceDiagnostic left, FairyGuiArtSliceDiagnostic right)
        {
            int result = string.Compare(left.Code, right.Code, StringComparison.Ordinal);
            if (result != 0)
                return result;

            result = string.Compare(left.PackageId, right.PackageId, StringComparison.Ordinal);
            if (result != 0)
                return result;

            result = string.Compare(left.ResourceId, right.ResourceId, StringComparison.Ordinal);
            if (result != 0)
                return result;

            result = string.Compare(left.ComponentPath, right.ComponentPath, StringComparison.Ordinal);
            if (result != 0)
                return result;

            return string.Compare(left.NodeId, right.NodeId, StringComparison.Ordinal);
        }

        private static int ComparePackageImages(FairyGuiPackageImageResource left, FairyGuiPackageImageResource right)
        {
            return string.Compare(left.ResourceId, right.ResourceId, StringComparison.Ordinal);
        }

        private static int CompareUsagePoints(FairyGuiArtSliceUsagePoint left, FairyGuiArtSliceUsagePoint right)
        {
            int result = string.Compare(left.ResourceId, right.ResourceId, StringComparison.Ordinal);
            if (result != 0)
                return result;

            result = string.Compare(left.ComponentPath, right.ComponentPath, StringComparison.Ordinal);
            if (result != 0)
                return result;

            return string.Compare(left.NodeId, right.NodeId, StringComparison.Ordinal);
        }

        private static FairyGuiArtSliceDiagnosticDto[] ToDiagnosticDtos(IReadOnlyList<FairyGuiArtSliceDiagnostic> diagnostics)
        {
            var values = new FairyGuiArtSliceDiagnosticDto[diagnostics.Count];
            for (int i = 0; i < diagnostics.Count; i++)
            {
                FairyGuiArtSliceDiagnostic diagnostic = diagnostics[i];
                values[i] = new FairyGuiArtSliceDiagnosticDto
                {
                    Code = diagnostic.Code,
                    Name = diagnostic.Name,
                    Severity = diagnostic.Severity,
                    PackageId = diagnostic.PackageId,
                    SliceId = diagnostic.SliceId,
                    ResourceId = diagnostic.ResourceId,
                    ComponentPath = diagnostic.ComponentPath,
                    NodeId = diagnostic.NodeId,
                    Message = diagnostic.Message,
                    Location = diagnostic.Location
                };
            }

            return values;
        }

        private static FairyGuiPackageImageDto[] ToImageDtos(IReadOnlyList<FairyGuiPackageImageResource> images)
        {
            var values = new FairyGuiPackageImageDto[images.Count];
            for (int i = 0; i < images.Count; i++)
            {
                values[i] = new FairyGuiPackageImageDto
                {
                    ResourceId = images[i].ResourceId,
                    FileName = images[i].FileName,
                    SourcePath = images[i].SourcePath
                };
            }

            return values;
        }

        private static FairyGuiUsagePointDto[] ToUsageDtos(IReadOnlyList<FairyGuiArtSliceUsagePoint> usages)
        {
            var values = new FairyGuiUsagePointDto[usages.Count];
            for (int i = 0; i < usages.Count; i++)
            {
                FairyGuiArtSliceUsagePoint usage = usages[i];
                values[i] = new FairyGuiUsagePointDto
                {
                    ComponentName = usage.ComponentName,
                    ComponentPath = usage.ComponentPath,
                    ControlName = usage.ControlName,
                    NodeId = usage.NodeId,
                    ResourceId = usage.ResourceId,
                    ResourcePath = usage.ResourcePath,
                    Position = usage.Position,
                    Size = usage.Size,
                    PreserveAspect = usage.PreserveAspect,
                    Relations = usage.Relations,
                    GearDisplay = usage.GearDisplay
                };
            }

            return values;
        }
    }

    public sealed class FairyGuiArtSliceValidationResult
    {
        public FairyGuiArtSliceValidationResult(
            string packageId,
            string packageSourcePath,
            IReadOnlyList<FairyGuiPackageImageResource> packageImages,
            IReadOnlyList<FairyGuiArtSliceUsagePoint> usagePoints,
            IReadOnlyList<FairyGuiArtSliceDiagnostic> diagnostics)
        {
            PackageId = packageId ?? string.Empty;
            PackageSourcePath = packageSourcePath ?? string.Empty;
            PackageImages = packageImages ?? Array.Empty<FairyGuiPackageImageResource>();
            UsagePoints = usagePoints ?? Array.Empty<FairyGuiArtSliceUsagePoint>();
            Diagnostics = diagnostics ?? Array.Empty<FairyGuiArtSliceDiagnostic>();
            for (int i = 0; i < Diagnostics.Count; i++)
            {
                if (string.Equals(Diagnostics[i].Severity, "error", StringComparison.Ordinal))
                    ErrorCount++;
                else if (string.Equals(Diagnostics[i].Severity, "warning", StringComparison.Ordinal))
                    WarningCount++;
                else if (string.Equals(Diagnostics[i].Severity, "info", StringComparison.Ordinal))
                    InfoCount++;
            }
        }

        public string PackageId { get; }
        public string PackageSourcePath { get; }
        public IReadOnlyList<FairyGuiPackageImageResource> PackageImages { get; }
        public IReadOnlyList<FairyGuiArtSliceUsagePoint> UsagePoints { get; }
        public IReadOnlyList<FairyGuiArtSliceDiagnostic> Diagnostics { get; }
        public int ErrorCount { get; }
        public int WarningCount { get; }
        public int InfoCount { get; }
        public bool Success => ErrorCount == 0;
    }

    public sealed class FairyGuiPackageImageResource
    {
        public FairyGuiPackageImageResource(string resourceId, string fileName, string sourcePath, string packageXmlPath)
        {
            ResourceId = resourceId ?? string.Empty;
            FileName = fileName ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            PackageXmlPath = packageXmlPath ?? string.Empty;
        }

        public string ResourceId { get; }
        public string FileName { get; }
        public string SourcePath { get; }
        public string PackageXmlPath { get; }
    }

    public sealed class FairyGuiArtSliceUsagePoint
    {
        public FairyGuiArtSliceUsagePoint(
            string componentName,
            string componentPath,
            string controlName,
            string nodeId,
            string resourceId,
            string resourcePath,
            FairyGuiPoint position,
            FairyGuiSize size,
            bool preserveAspect,
            FairyGuiRelation[] relations,
            FairyGuiGearDisplay gearDisplay)
        {
            ComponentName = componentName ?? string.Empty;
            ComponentPath = componentPath ?? string.Empty;
            ControlName = controlName ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            ResourcePath = resourcePath ?? string.Empty;
            Position = position;
            Size = size;
            PreserveAspect = preserveAspect;
            Relations = relations ?? Array.Empty<FairyGuiRelation>();
            GearDisplay = gearDisplay;
        }

        public string ComponentName { get; }
        public string ComponentPath { get; }
        public string ControlName { get; }
        public string NodeId { get; }
        public string ResourceId { get; }
        public string ResourcePath { get; }
        public FairyGuiPoint Position { get; }
        public FairyGuiSize Size { get; }
        public bool PreserveAspect { get; }
        public FairyGuiRelation[] Relations { get; }
        public FairyGuiGearDisplay GearDisplay { get; }
    }

    public sealed class FairyGuiArtSliceDiagnostic
    {
        public FairyGuiArtSliceDiagnostic(
            string code,
            string name,
            string severity,
            string packageId,
            string sliceId,
            string resourceId,
            string componentPath,
            string nodeId,
            string message,
            FairyGuiArtSliceLocation location)
        {
            Code = code ?? string.Empty;
            Name = name ?? string.Empty;
            Severity = severity ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            SliceId = sliceId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            ComponentPath = componentPath ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            Message = message ?? string.Empty;
            Location = location;
        }

        public string Code { get; }
        public string Name { get; }
        public string Severity { get; }
        public string PackageId { get; }
        public string SliceId { get; }
        public string ResourceId { get; }
        public string ComponentPath { get; }
        public string NodeId { get; }
        public string Message { get; }
        public FairyGuiArtSliceLocation Location { get; }
    }

    public readonly struct FairyGuiPoint
    {
        public FairyGuiPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        [JsonPropertyName("x")]
        public int X { get; }

        [JsonPropertyName("y")]
        public int Y { get; }
    }

    public readonly struct FairyGuiSize
    {
        public FairyGuiSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        [JsonPropertyName("width")]
        public int Width { get; }

        [JsonPropertyName("height")]
        public int Height { get; }
    }

    public readonly struct FairyGuiRelation
    {
        public FairyGuiRelation(string type)
        {
            Type = type ?? string.Empty;
        }

        [JsonPropertyName("type")]
        public string Type { get; }
    }

    public sealed class FairyGuiGearDisplay
    {
        public FairyGuiGearDisplay(string controller, string[] pages)
        {
            Controller = controller ?? string.Empty;
            Pages = pages ?? Array.Empty<string>();
        }

        [JsonPropertyName("controller")]
        public string Controller { get; }

        [JsonPropertyName("pages")]
        public string[] Pages { get; }
    }

    public readonly struct FairyGuiArtSliceLocation
    {
        public FairyGuiArtSliceLocation(string file, string resourceId, string nodeId)
        {
            File = file ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
        }

        [JsonPropertyName("file")]
        public string File { get; }

        [JsonPropertyName("resourceId")]
        public string ResourceId { get; }

        [JsonPropertyName("nodeId")]
        public string NodeId { get; }
    }

    public sealed class FairyGuiArtSliceCatalog
    {
        public FairyGuiArtSliceCatalog()
        {
            SchemaVersion = string.Empty;
            PackageId = string.Empty;
            PackageSourcePath = string.Empty;
            PathStyle = string.Empty;
            DesignResolution = new FairyGuiDesignResolution();
            Groups = Array.Empty<FairyGuiArtSliceGroup>();
            Slices = Array.Empty<FairyGuiArtSlice>();
        }

        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; set; }

        [JsonPropertyName("packageId")]
        public string PackageId { get; set; }

        [JsonPropertyName("packageSourcePath")]
        public string PackageSourcePath { get; set; }

        [JsonPropertyName("pathStyle")]
        public string PathStyle { get; set; }

        [JsonPropertyName("designResolution")]
        public FairyGuiDesignResolution DesignResolution { get; set; }

        [JsonPropertyName("strictCoverage")]
        public bool StrictCoverage { get; set; }

        [JsonPropertyName("groups")]
        public FairyGuiArtSliceGroup[] Groups { get; set; }

        [JsonPropertyName("slices")]
        public FairyGuiArtSlice[] Slices { get; set; }
    }

    public sealed class FairyGuiDesignResolution
    {
        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }

    public sealed class FairyGuiArtSliceGroup
    {
        public FairyGuiArtSliceGroup()
        {
            GroupId = string.Empty;
            Kind = string.Empty;
            ExpectedRoles = Array.Empty<string>();
        }

        [JsonPropertyName("groupId")]
        public string GroupId { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        [JsonPropertyName("expectedRoles")]
        public string[] ExpectedRoles { get; set; }

        [JsonPropertyName("requireSamePixelSize")]
        public bool RequireSamePixelSize { get; set; }

        [JsonPropertyName("replaceTogether")]
        public bool ReplaceTogether { get; set; }
    }

    public sealed class FairyGuiArtSlice
    {
        public FairyGuiArtSlice()
        {
            SliceId = string.Empty;
            FguiResourceId = string.Empty;
            ExpectedSourcePath = string.Empty;
            Category = string.Empty;
            DisplayName = string.Empty;
            Description = string.Empty;
            Tags = Array.Empty<string>();
            IgnoreReason = string.Empty;
            GroupId = string.Empty;
            RoleInGroup = string.Empty;
            KnownExternalUsages = Array.Empty<FairyGuiKnownExternalUsage>();
        }

        [JsonPropertyName("sliceId")]
        public string SliceId { get; set; }

        [JsonPropertyName("fguiResourceId")]
        public string FguiResourceId { get; set; }

        [JsonPropertyName("expectedSourcePath")]
        public string ExpectedSourcePath { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("tags")]
        public string[] Tags { get; set; }

        [JsonPropertyName("recommendedUsage")]
        public FairyGuiRecommendedUsage RecommendedUsage { get; set; }

        [JsonPropertyName("replacement")]
        public FairyGuiReplacementPolicy Replacement { get; set; }

        [JsonPropertyName("scale9Grid")]
        public FairyGuiScale9Grid Scale9Grid { get; set; }

        [JsonPropertyName("groupId")]
        public string GroupId { get; set; }

        [JsonPropertyName("roleInGroup")]
        public string RoleInGroup { get; set; }

        [JsonPropertyName("ignoreReason")]
        public string IgnoreReason { get; set; }

        [JsonPropertyName("knownExternalUsages")]
        public FairyGuiKnownExternalUsage[] KnownExternalUsages { get; set; }
    }

    public sealed class FairyGuiRecommendedUsage
    {
        public FairyGuiRecommendedUsage()
        {
            Policy = string.Empty;
            LogicalSize = new FairyGuiDesignResolution();
            RelationHint = string.Empty;
        }

        [JsonPropertyName("policy")]
        public string Policy { get; set; }

        [JsonPropertyName("logicalSize")]
        public FairyGuiDesignResolution LogicalSize { get; set; }

        [JsonPropertyName("preserveAspect")]
        public bool PreserveAspect { get; set; }

        [JsonPropertyName("relationHint")]
        public string RelationHint { get; set; }
    }

    public sealed class FairyGuiReplacementPolicy
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

    public sealed class FairyGuiScale9Grid
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

    public sealed class FairyGuiKnownExternalUsage
    {
        public FairyGuiKnownExternalUsage()
        {
            Kind = string.Empty;
            Owner = string.Empty;
            Note = string.Empty;
        }

        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        [JsonPropertyName("owner")]
        public string Owner { get; set; }

        [JsonPropertyName("note")]
        public string Note { get; set; }
    }

    public sealed class FairyGuiArtSliceReportDto
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; set; }

        [JsonPropertyName("packageId")]
        public string PackageId { get; set; }

        [JsonPropertyName("packageSourcePath")]
        public string PackageSourcePath { get; set; }

        [JsonPropertyName("summary")]
        public FairyGuiArtSliceReportSummaryDto Summary { get; set; }

        [JsonPropertyName("diagnostics")]
        public FairyGuiArtSliceDiagnosticDto[] Diagnostics { get; set; }

        [JsonPropertyName("packageImages")]
        public FairyGuiPackageImageDto[] PackageImages { get; set; }

        [JsonPropertyName("usagePoints")]
        public FairyGuiUsagePointDto[] UsagePoints { get; set; }
    }

    public sealed class FairyGuiArtSliceReportSummaryDto
    {
        [JsonPropertyName("imageCount")]
        public int ImageCount { get; set; }

        [JsonPropertyName("usageCount")]
        public int UsageCount { get; set; }

        [JsonPropertyName("errorCount")]
        public int ErrorCount { get; set; }

        [JsonPropertyName("warningCount")]
        public int WarningCount { get; set; }

        [JsonPropertyName("infoCount")]
        public int InfoCount { get; set; }
    }

    public sealed class FairyGuiArtSliceDiagnosticDto
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("severity")]
        public string Severity { get; set; }

        [JsonPropertyName("packageId")]
        public string PackageId { get; set; }

        [JsonPropertyName("sliceId")]
        public string SliceId { get; set; }

        [JsonPropertyName("resourceId")]
        public string ResourceId { get; set; }

        [JsonPropertyName("componentPath")]
        public string ComponentPath { get; set; }

        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("location")]
        public FairyGuiArtSliceLocation Location { get; set; }
    }

    public sealed class FairyGuiPackageImageDto
    {
        [JsonPropertyName("resourceId")]
        public string ResourceId { get; set; }

        [JsonPropertyName("fileName")]
        public string FileName { get; set; }

        [JsonPropertyName("sourcePath")]
        public string SourcePath { get; set; }
    }

    public sealed class FairyGuiUsagePointDto
    {
        [JsonPropertyName("componentName")]
        public string ComponentName { get; set; }

        [JsonPropertyName("componentPath")]
        public string ComponentPath { get; set; }

        [JsonPropertyName("controlName")]
        public string ControlName { get; set; }

        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; }

        [JsonPropertyName("resourceId")]
        public string ResourceId { get; set; }

        [JsonPropertyName("resourcePath")]
        public string ResourcePath { get; set; }

        [JsonPropertyName("position")]
        public FairyGuiPoint Position { get; set; }

        [JsonPropertyName("size")]
        public FairyGuiSize Size { get; set; }

        [JsonPropertyName("preserveAspect")]
        public bool PreserveAspect { get; set; }

        [JsonPropertyName("relations")]
        public FairyGuiRelation[] Relations { get; set; }

        [JsonPropertyName("gearDisplay")]
        public FairyGuiGearDisplay GearDisplay { get; set; }
    }
}

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
    public static partial class FairyGuiArtSliceCatalogService
    {
        public static IReadOnlyList<FairyGuiArtSlicePackageDescriptor> ListPackages(string repositoryRoot)
        {
            var packages = new List<FairyGuiArtSlicePackageDescriptor>();
            AddKnownPackage(repositoryRoot, packages, CharacterTestPackageId, CharacterTestPackageSourcePath, CharacterTestCatalogPath);

            string assetsRoot = ResolvePath(repositoryRoot, "FGUIProject/assets");
            if (Directory.Exists(assetsRoot))
            {
                string[] catalogs = Directory.GetFiles(assetsRoot, "ui-art-slices.json", SearchOption.AllDirectories);
                for (int i = 0; i < catalogs.Length; i++)
                {
                    string relativeCatalogPath = ToRepositoryRelativePath(repositoryRoot, catalogs[i]);
                    string relativePackagePath = NormalizePath(Path.GetDirectoryName(relativeCatalogPath) ?? string.Empty);
                    string packageId = Path.GetFileName(relativePackagePath);
                    FairyGuiArtSliceCatalog catalog = TryReadCatalogFile(repositoryRoot, relativeCatalogPath);
                    if (catalog != null && !string.IsNullOrWhiteSpace(catalog.PackageId))
                        packageId = catalog.PackageId;

                    AddKnownPackage(repositoryRoot, packages, packageId, relativePackagePath, relativeCatalogPath);
                }
            }

            packages.Sort(ComparePackageDescriptors);
            return packages;
        }

        public static FairyGuiArtSlicePackageDescriptor ResolvePackageDescriptor(string repositoryRoot, string package)
        {
            string normalized = NormalizePath(package);
            if (string.IsNullOrWhiteSpace(normalized)
                || string.Equals(normalized, CharacterTestPackageId, StringComparison.Ordinal)
                || string.Equals(normalized, CharacterTestPackageSourcePath, StringComparison.Ordinal)
                || string.Equals(normalized, CharacterTestCatalogPath, StringComparison.Ordinal))
            {
                return new FairyGuiArtSlicePackageDescriptor(
                    CharacterTestPackageId,
                    CharacterTestPackageSourcePath,
                    CharacterTestCatalogPath,
                    "CharacterTest HUD");
            }

            IReadOnlyList<FairyGuiArtSlicePackageDescriptor> packages = ListPackages(repositoryRoot);
            for (int i = 0; i < packages.Count; i++)
            {
                FairyGuiArtSlicePackageDescriptor item = packages[i];
                if (string.Equals(item.PackageId, normalized, StringComparison.Ordinal)
                    || string.Equals(item.PackageSourcePath, normalized, StringComparison.Ordinal)
                    || string.Equals(item.CatalogPath, normalized, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            if (normalized.EndsWith("/ui-art-slices.json", StringComparison.Ordinal))
            {
                string packageSourcePath = NormalizePath(Path.GetDirectoryName(normalized) ?? string.Empty);
                FairyGuiArtSliceCatalog catalog = TryReadCatalogFile(repositoryRoot, normalized);
                string packageId = catalog != null && !string.IsNullOrWhiteSpace(catalog.PackageId)
                    ? catalog.PackageId
                    : Path.GetFileName(packageSourcePath);
                return new FairyGuiArtSlicePackageDescriptor(packageId, packageSourcePath, normalized, packageId);
            }

            string catalogPath = NormalizePath(normalized.TrimEnd('/') + "/ui-art-slices.json");
            if (File.Exists(ResolvePath(repositoryRoot, catalogPath)))
            {
                FairyGuiArtSliceCatalog catalog = TryReadCatalogFile(repositoryRoot, catalogPath);
                string packageId = catalog != null && !string.IsNullOrWhiteSpace(catalog.PackageId)
                    ? catalog.PackageId
                    : Path.GetFileName(normalized);
                return new FairyGuiArtSlicePackageDescriptor(packageId, normalized, catalogPath, packageId);
            }

            throw new InvalidOperationException("Unknown FairyGUI art slice package: " + package + ".");
        }

        public static FairyGuiArtSliceAuthoringState ReadAuthoringState(string repositoryRoot, string package)
        {
            FairyGuiArtSlicePackageDescriptor descriptor = ResolvePackageDescriptor(repositoryRoot, package);
            FairyGuiArtSliceValidationResult validation = ValidatePackage(repositoryRoot, descriptor.CatalogPath);
            FairyGuiArtSliceCatalog catalog = ReadCatalogFile(repositoryRoot, descriptor.CatalogPath);
            return new FairyGuiArtSliceAuthoringState(
                descriptor,
                ComputeCatalogHash(repositoryRoot, descriptor.CatalogPath),
                catalog,
                validation,
                GenerateMarkdownReport(validation));
        }

        public static FairyGuiArtSliceCatalog ReadCatalogFile(string repositoryRoot, string catalogPath)
        {
            string absolutePath = ResolvePath(repositoryRoot, catalogPath);
            string text = File.ReadAllText(absolutePath);
            FairyGuiArtSliceCatalog catalog = JsonSerializer.Deserialize<FairyGuiArtSliceCatalog>(text, JsonOptions);
            if (catalog == null)
                throw new InvalidOperationException("FairyGUI art slice catalog JSON is empty.");

            return catalog;
        }

        public static FairyGuiArtSliceCatalog TryReadCatalogFile(string repositoryRoot, string catalogPath)
        {
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

        public static void WriteCatalogFile(string repositoryRoot, string catalogPath, FairyGuiArtSliceCatalog catalog)
        {
            string absolutePath = ResolvePath(repositoryRoot, catalogPath);
            EnsurePathUnderRoot(repositoryRoot, absolutePath);
            string text = SerializeCatalog(catalog);
            WriteTextAtomic(absolutePath, text);
        }

        public static string SerializeCatalog(FairyGuiArtSliceCatalog catalog)
        {
            return JsonSerializer.Serialize(catalog, JsonOptions) + Environment.NewLine;
        }

        public static string ComputeCatalogHash(string repositoryRoot, string catalogPath)
        {
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
            string package,
            string expectedCatalogHash,
            FairyGuiArtSliceCatalog catalog)
        {
            FairyGuiArtSlicePackageDescriptor descriptor = ResolvePackageDescriptor(repositoryRoot, package);
            var diagnostics = new List<FairyGuiArtSliceOperationDiagnostic>();
            ValidateCatalogHash(repositoryRoot, descriptor.CatalogPath, expectedCatalogHash, diagnostics);
            if (catalog == null)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTAPI010",
                    "Catalog payload is required.",
                    descriptor.CatalogPath));
            }
            else
            {
                if (!string.Equals(catalog.PackageId, descriptor.PackageId, StringComparison.Ordinal))
                {
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                        "UIARTAPI011",
                        "Catalog packageId does not match the requested package.",
                        descriptor.CatalogPath));
                }

                if (!string.Equals(NormalizePath(catalog.PackageSourcePath), descriptor.PackageSourcePath, StringComparison.Ordinal))
                {
                    diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                        "UIARTAPI012",
                        "Catalog packageSourcePath does not match the requested package.",
                        descriptor.CatalogPath));
                }
            }

            if (!HasBlockingDiagnostic(diagnostics))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Info(
                    "UIARTAPI013",
                    "Catalog semantic fields will be saved. FairyGUI XML, PNG files, atlases, and *_fui.bytes will not be modified.",
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

        public static FairyGuiArtSliceOperationPlan PlanRegisterDraftSlice(
            string repositoryRoot,
            string package,
            string expectedCatalogHash,
            string sliceId,
            string expectedSourcePath)
        {
            FairyGuiArtSlicePackageDescriptor descriptor = ResolvePackageDescriptor(repositoryRoot, package);
            var diagnostics = new List<FairyGuiArtSliceOperationDiagnostic>();
            ValidateCatalogHash(repositoryRoot, descriptor.CatalogPath, expectedCatalogHash, diagnostics);

            if (string.IsNullOrWhiteSpace(sliceId))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTAPI020",
                    "Draft sliceId is required.",
                    descriptor.CatalogPath));
            }

            if (!string.IsNullOrWhiteSpace(expectedSourcePath) && Path.IsPathRooted(expectedSourcePath))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTAPI021",
                    "Draft expectedSourcePath must be package-relative.",
                    expectedSourcePath));
            }

            if (!HasBlockingDiagnostic(diagnostics))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Info(
                    "UIARTAPI022",
                    "Draft slice registration will update the sidecar catalog only.",
                    descriptor.CatalogPath));
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Warning(
                    "UIARTAPI023",
                    "Draft registration does not create package.xml image resources or component XML usages; complete real FairyGUI authoring and publishing separately.",
                    expectedSourcePath));
            }

            bool blocked = HasBlockingDiagnostic(diagnostics);
            return new FairyGuiArtSliceOperationPlan(
                "RegisterDraftSlice",
                sliceId,
                descriptor.CatalogPath,
                expectedSourcePath,
                !blocked,
                false,
                false,
                false,
                false,
                diagnostics);
        }

        public static FairyGuiArtSliceOperationPlan PlanReplaceExistingPng(
            string repositoryRoot,
            string package,
            string expectedCatalogHash,
            string sliceId,
            byte[] replacementBytes)
        {
            FairyGuiArtSlicePackageDescriptor descriptor = ResolvePackageDescriptor(repositoryRoot, package);
            FairyGuiArtSliceAuthoringState state = ReadAuthoringState(repositoryRoot, descriptor.PackageId);
            var diagnostics = new List<FairyGuiArtSliceOperationDiagnostic>();
            ValidateCatalogHash(repositoryRoot, descriptor.CatalogPath, expectedCatalogHash, diagnostics);

            FairyGuiArtSlice slice = FindSlice(state.Catalog, sliceId);
            FairyGuiPackageImageResource image = slice != null
                ? FindImage(state.PackageImages, slice.FguiResourceId)
                : null;

            if (slice == null)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTAPI030",
                    "Slice was not found in the catalog.",
                    descriptor.CatalogPath));
            }

            string targetPath = image != null
                ? ResolvePath(repositoryRoot, descriptor.PackageSourcePath + "/" + image.SourcePath)
                : string.Empty;
            string targetDisplayPath = image != null
                ? NormalizePath(descriptor.PackageSourcePath + "/" + image.SourcePath)
                : string.Empty;

            if (image == null && slice != null)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTAPI031",
                    "Slice resource id was not found in package.xml.",
                    descriptor.PackageSourcePath + "/package.xml"));
            }

            if (image != null)
                EnsurePackagePathForPlan(repositoryRoot, descriptor.PackageSourcePath, targetPath, diagnostics);

            if (replacementBytes == null || replacementBytes.Length == 0)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTAPI032",
                    "Replacement PNG bytes are required.",
                    targetDisplayPath));
            }

            if (!HasBlockingDiagnostic(diagnostics))
            {
                FairyGuiArtSlicePngInfo existing = ReadPngInfo(targetPath);
                FairyGuiArtSlicePngInfo replacement = ReadPngInfo(replacementBytes);
                ValidateReplacementPng(slice, existing, replacement, targetDisplayPath, diagnostics);
            }

            if (slice != null && (!string.IsNullOrWhiteSpace(slice.GroupId)
                || slice.Replacement != null && slice.Replacement.ReplaceTogether))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Warning(
                    "UIARTAPI039",
                    "Slice belongs to a group or is marked replaceTogether; review sibling state images before replacing only this PNG.",
                    targetDisplayPath));
            }

            if (!HasBlockingDiagnostic(diagnostics))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Info(
                    "UIARTAPI040",
                    "Existing PNG will be replaced at the same path. FairyGUI XML and published outputs will not be modified, but FairyGUI publish is required afterwards.",
                    targetDisplayPath));
            }

            bool blocked = HasBlockingDiagnostic(diagnostics);
            return new FairyGuiArtSliceOperationPlan(
                "ReplaceExistingPng",
                sliceId,
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
            string package,
            string expectedCatalogHash,
            string sliceId,
            byte[] replacementBytes)
        {
            FairyGuiArtSliceOperationPlan plan = PlanReplaceExistingPng(repositoryRoot, package, expectedCatalogHash, sliceId, replacementBytes);
            if (plan.Blocked)
                throw new InvalidOperationException("Replacement plan is blocked.");

            FairyGuiArtSlicePackageDescriptor descriptor = ResolvePackageDescriptor(repositoryRoot, package);
            FairyGuiArtSliceAuthoringState state = ReadAuthoringState(repositoryRoot, descriptor.PackageId);
            FairyGuiArtSlice slice = FindSlice(state.Catalog, sliceId);
            FairyGuiPackageImageResource image = slice != null ? FindImage(state.PackageImages, slice.FguiResourceId) : null;
            if (image == null)
                throw new InvalidOperationException("Replacement target was not found.");

            string targetPath = ResolvePath(repositoryRoot, descriptor.PackageSourcePath + "/" + image.SourcePath);
            EnsurePathUnderRoot(ResolvePath(repositoryRoot, descriptor.PackageSourcePath), targetPath);
            WriteBytesAtomic(targetPath, replacementBytes);
        }

        public static FairyGuiArtSlicePngInfo ReadPngInfo(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
                return FairyGuiArtSlicePngInfo.Invalid("PNG file was not found.");

            return ReadPngInfo(File.ReadAllBytes(absolutePath));
        }

        public static FairyGuiArtSlicePngInfo ReadPngInfo(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 33)
                return FairyGuiArtSlicePngInfo.Invalid("PNG data is too small.");

            if (bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47
                || bytes[4] != 0x0D || bytes[5] != 0x0A || bytes[6] != 0x1A || bytes[7] != 0x0A)
            {
                return FairyGuiArtSlicePngInfo.Invalid("Data is not a PNG file.");
            }

            int width = 0;
            int height = 0;
            int bitDepth = 0;
            int colorType = -1;
            bool hasTrns = false;
            int offset = 8;
            while (offset + 12 <= bytes.Length)
            {
                int length = ReadBigEndianInt32(bytes, offset);
                if (length < 0 || offset + 12 + length > bytes.Length)
                    return FairyGuiArtSlicePngInfo.Invalid("PNG chunk length is invalid.");

                string chunkType = Encoding.ASCII.GetString(bytes, offset + 4, 4);
                int dataOffset = offset + 8;
                if (string.Equals(chunkType, "IHDR", StringComparison.Ordinal))
                {
                    if (length < 13)
                        return FairyGuiArtSlicePngInfo.Invalid("PNG IHDR chunk is invalid.");

                    width = ReadBigEndianInt32(bytes, dataOffset);
                    height = ReadBigEndianInt32(bytes, dataOffset + 4);
                    bitDepth = bytes[dataOffset + 8];
                    colorType = bytes[dataOffset + 9];
                }
                else if (string.Equals(chunkType, "tRNS", StringComparison.Ordinal))
                {
                    hasTrns = true;
                }
                else if (string.Equals(chunkType, "IEND", StringComparison.Ordinal))
                {
                    break;
                }

                offset += 12 + length;
            }

            if (width <= 0 || height <= 0 || colorType < 0)
                return FairyGuiArtSlicePngInfo.Invalid("PNG IHDR chunk was not found.");

            bool hasAlphaChannel = colorType == 4 || colorType == 6 || hasTrns;
            return FairyGuiArtSlicePngInfo.CreateValid(width, height, bitDepth, colorType, hasAlphaChannel, hasAlphaChannel);
        }

        private static void AddKnownPackage(
            string repositoryRoot,
            List<FairyGuiArtSlicePackageDescriptor> packages,
            string packageId,
            string packageSourcePath,
            string catalogPath)
        {
            string normalizedPackageSourcePath = NormalizePath(packageSourcePath);
            string normalizedCatalogPath = NormalizePath(catalogPath);
            for (int i = 0; i < packages.Count; i++)
            {
                if (string.Equals(packages[i].CatalogPath, normalizedCatalogPath, StringComparison.Ordinal))
                    return;
            }

            string absoluteCatalogPath = ResolvePath(repositoryRoot, normalizedCatalogPath);
            string absolutePackageXmlPath = ResolvePath(repositoryRoot, normalizedPackageSourcePath + "/package.xml");
            if (!File.Exists(absoluteCatalogPath) && !File.Exists(absolutePackageXmlPath))
                return;

            string displayName = string.IsNullOrWhiteSpace(packageId) ? Path.GetFileName(normalizedPackageSourcePath) : packageId;
            packages.Add(new FairyGuiArtSlicePackageDescriptor(packageId, normalizedPackageSourcePath, normalizedCatalogPath, displayName));
        }

        private static int ComparePackageDescriptors(FairyGuiArtSlicePackageDescriptor left, FairyGuiArtSlicePackageDescriptor right)
        {
            return string.Compare(left.PackageId, right.PackageId, StringComparison.Ordinal);
        }

        private static FairyGuiArtSlice FindSlice(FairyGuiArtSliceCatalog catalog, string sliceId)
        {
            FairyGuiArtSlice[] slices = catalog != null && catalog.Slices != null ? catalog.Slices : Array.Empty<FairyGuiArtSlice>();
            for (int i = 0; i < slices.Length; i++)
            {
                if (string.Equals(slices[i].SliceId, sliceId, StringComparison.Ordinal))
                    return slices[i];
            }

            return null;
        }

        private static FairyGuiPackageImageResource FindImage(IReadOnlyList<FairyGuiPackageImageResource> images, string resourceId)
        {
            for (int i = 0; i < images.Count; i++)
            {
                if (string.Equals(images[i].ResourceId, resourceId, StringComparison.Ordinal))
                    return images[i];
            }

            return null;
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
                    "UIARTAPI001",
                    "Catalog changed on disk. Reload before applying this operation.",
                    catalogPath));
            }
        }

        private static void EnsurePackagePathForPlan(
            string repositoryRoot,
            string packageSourcePath,
            string targetPath,
            List<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            string packageRoot = ResolvePath(repositoryRoot, packageSourcePath);
            if (!IsPathAtOrUnderRoot(packageRoot, targetPath))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTAPI033",
                    "Replacement target is outside the FairyGUI package root.",
                    targetPath));
            }
        }

        private static void ValidateReplacementPng(
            FairyGuiArtSlice slice,
            FairyGuiArtSlicePngInfo existing,
            FairyGuiArtSlicePngInfo replacement,
            string targetDisplayPath,
            List<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            if (!existing.Valid)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTAPI034",
                    "Existing PNG is invalid: " + existing.Error,
                    targetDisplayPath));
                return;
            }

            if (!replacement.Valid)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTAPI035",
                    "Replacement PNG is invalid: " + replacement.Error,
                    targetDisplayPath));
                return;
            }

            FairyGuiReplacementPolicy policy = slice != null ? slice.Replacement : null;
            if (policy == null)
                return;

            if (policy.RequireSamePixelSize && (existing.Width != replacement.Width || existing.Height != replacement.Height))
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTAPI036",
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
                    "UIARTAPI037",
                    "Replacement width is below minPixelWidth.",
                    targetDisplayPath));
            }

            if (policy.MinPixelHeight > 0 && replacement.Height < policy.MinPixelHeight)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTAPI038",
                    "Replacement height is below minPixelHeight.",
                    targetDisplayPath));
            }

            if (policy.RequireAlphaChannel && !replacement.HasAlphaChannel)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTAPI041",
                    "Replacement must include an alpha channel.",
                    targetDisplayPath));
            }

            if (policy.RequireNonOpaqueAlpha && !replacement.HasNonOpaqueAlpha)
            {
                diagnostics.Add(FairyGuiArtSliceOperationDiagnostic.Error(
                    "UIARTAPI042",
                    "Replacement must include non-opaque alpha data.",
                    targetDisplayPath));
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

        private static string ToRepositoryRelativePath(string repositoryRoot, string path)
        {
            string fullRoot = Path.GetFullPath(repositoryRoot ?? string.Empty);
            string fullPath = Path.GetFullPath(path ?? string.Empty);
            if (!IsPathAtOrUnderRoot(fullRoot, fullPath))
                return NormalizePath(path);

            return NormalizePath(Path.GetRelativePath(fullRoot, fullPath));
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

    public sealed class FairyGuiArtSlicePackageDescriptor
    {
        public FairyGuiArtSlicePackageDescriptor(
            string packageId,
            string packageSourcePath,
            string catalogPath,
            string displayName)
        {
            PackageId = packageId ?? string.Empty;
            PackageSourcePath = Normalize(packageSourcePath);
            CatalogPath = Normalize(catalogPath);
            DisplayName = displayName ?? string.Empty;
        }

        [JsonPropertyName("packageId")]
        public string PackageId { get; }

        [JsonPropertyName("packageSourcePath")]
        public string PackageSourcePath { get; }

        [JsonPropertyName("catalogPath")]
        public string CatalogPath { get; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
        }
    }

    public sealed class FairyGuiArtSliceAuthoringState
    {
        public FairyGuiArtSliceAuthoringState(
            FairyGuiArtSlicePackageDescriptor package,
            string catalogHash,
            FairyGuiArtSliceCatalog catalog,
            FairyGuiArtSliceValidationResult validation,
            string markdownReport)
        {
            Package = package;
            CatalogHash = catalogHash ?? string.Empty;
            Catalog = catalog;
            PackageId = validation != null ? validation.PackageId : package != null ? package.PackageId : string.Empty;
            PackageSourcePath = validation != null ? validation.PackageSourcePath : package != null ? package.PackageSourcePath : string.Empty;
            PackageImages = validation != null ? validation.PackageImages : Array.Empty<FairyGuiPackageImageResource>();
            UsagePoints = validation != null ? validation.UsagePoints : Array.Empty<FairyGuiArtSliceUsagePoint>();
            Diagnostics = validation != null ? validation.Diagnostics : Array.Empty<FairyGuiArtSliceDiagnostic>();
            ErrorCount = validation != null ? validation.ErrorCount : 0;
            WarningCount = validation != null ? validation.WarningCount : 0;
            InfoCount = validation != null ? validation.InfoCount : 0;
            MarkdownReport = markdownReport ?? string.Empty;
        }

        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion => "mx.uiArtSliceAuthoringState.v1";

        [JsonPropertyName("package")]
        public FairyGuiArtSlicePackageDescriptor Package { get; }

        [JsonPropertyName("packageId")]
        public string PackageId { get; }

        [JsonPropertyName("packageSourcePath")]
        public string PackageSourcePath { get; }

        [JsonPropertyName("catalogHash")]
        public string CatalogHash { get; }

        [JsonPropertyName("catalog")]
        public FairyGuiArtSliceCatalog Catalog { get; }

        [JsonPropertyName("packageImages")]
        public IReadOnlyList<FairyGuiPackageImageResource> PackageImages { get; }

        [JsonPropertyName("usagePoints")]
        public IReadOnlyList<FairyGuiArtSliceUsagePoint> UsagePoints { get; }

        [JsonPropertyName("diagnostics")]
        public IReadOnlyList<FairyGuiArtSliceDiagnostic> Diagnostics { get; }

        [JsonPropertyName("errorCount")]
        public int ErrorCount { get; }

        [JsonPropertyName("warningCount")]
        public int WarningCount { get; }

        [JsonPropertyName("infoCount")]
        public int InfoCount { get; }

        [JsonPropertyName("markdownReport")]
        public string MarkdownReport { get; }
    }

    public sealed class FairyGuiArtSliceOperationPlan
    {
        public FairyGuiArtSliceOperationPlan(
            string operationKind,
            string sliceId,
            string targetPath,
            string sourcePath,
            bool wouldModifyCatalog,
            bool wouldModifyPng,
            bool wouldModifyFairyGuiXml,
            bool wouldModifyPublishedOutput,
            bool requiresFairyGuiPublish,
            IReadOnlyList<FairyGuiArtSliceOperationDiagnostic> diagnostics)
        {
            OperationKind = operationKind ?? string.Empty;
            SliceId = sliceId ?? string.Empty;
            TargetPath = targetPath ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            WouldModifyCatalog = wouldModifyCatalog;
            WouldModifyPng = wouldModifyPng;
            WouldModifyFairyGuiXml = wouldModifyFairyGuiXml;
            WouldModifyPublishedOutput = wouldModifyPublishedOutput;
            RequiresFairyGuiPublish = requiresFairyGuiPublish;
            Diagnostics = diagnostics ?? Array.Empty<FairyGuiArtSliceOperationDiagnostic>();
        }

        [JsonPropertyName("operationKind")]
        public string OperationKind { get; }

        [JsonPropertyName("sliceId")]
        public string SliceId { get; }

        [JsonPropertyName("targetPath")]
        public string TargetPath { get; }

        [JsonPropertyName("sourcePath")]
        public string SourcePath { get; }

        [JsonPropertyName("wouldModifyCatalog")]
        public bool WouldModifyCatalog { get; }

        [JsonPropertyName("wouldModifyPng")]
        public bool WouldModifyPng { get; }

        [JsonPropertyName("wouldModifyFairyGuiXml")]
        public bool WouldModifyFairyGuiXml { get; }

        [JsonPropertyName("wouldModifyPublishedOutput")]
        public bool WouldModifyPublishedOutput { get; }

        [JsonPropertyName("requiresFairyGuiPublish")]
        public bool RequiresFairyGuiPublish { get; }

        [JsonPropertyName("blocked")]
        public bool Blocked
        {
            get
            {
                for (int i = 0; i < Diagnostics.Count; i++)
                {
                    if (Diagnostics[i].Blocking)
                        return true;
                }

                return false;
            }
        }

        [JsonPropertyName("diagnostics")]
        public IReadOnlyList<FairyGuiArtSliceOperationDiagnostic> Diagnostics { get; }
    }

    public sealed class FairyGuiArtSliceOperationDiagnostic
    {
        private FairyGuiArtSliceOperationDiagnostic(string code, string severity, string message, string path)
        {
            Code = code ?? string.Empty;
            Severity = severity ?? string.Empty;
            Message = message ?? string.Empty;
            Path = path ?? string.Empty;
        }

        public static FairyGuiArtSliceOperationDiagnostic Error(string code, string message, string path)
        {
            return new FairyGuiArtSliceOperationDiagnostic(code, "error", message, path);
        }

        public static FairyGuiArtSliceOperationDiagnostic Warning(string code, string message, string path)
        {
            return new FairyGuiArtSliceOperationDiagnostic(code, "warning", message, path);
        }

        public static FairyGuiArtSliceOperationDiagnostic Info(string code, string message, string path)
        {
            return new FairyGuiArtSliceOperationDiagnostic(code, "info", message, path);
        }

        [JsonPropertyName("code")]
        public string Code { get; }

        [JsonPropertyName("severity")]
        public string Severity { get; }

        [JsonPropertyName("message")]
        public string Message { get; }

        [JsonPropertyName("path")]
        public string Path { get; }

        [JsonPropertyName("blocking")]
        public bool Blocking => string.Equals(Severity, "error", StringComparison.Ordinal);
    }

    public readonly struct FairyGuiArtSlicePngInfo
    {
        private FairyGuiArtSlicePngInfo(
            bool valid,
            int width,
            int height,
            int bitDepth,
            int colorType,
            bool hasAlphaChannel,
            bool hasNonOpaqueAlpha,
            string error)
        {
            Valid = valid;
            Width = width;
            Height = height;
            BitDepth = bitDepth;
            ColorType = colorType;
            HasAlphaChannel = hasAlphaChannel;
            HasNonOpaqueAlpha = hasNonOpaqueAlpha;
            Error = error ?? string.Empty;
        }

        public static FairyGuiArtSlicePngInfo CreateValid(
            int width,
            int height,
            int bitDepth,
            int colorType,
            bool hasAlphaChannel,
            bool hasNonOpaqueAlpha)
        {
            return new FairyGuiArtSlicePngInfo(true, width, height, bitDepth, colorType, hasAlphaChannel, hasNonOpaqueAlpha, string.Empty);
        }

        public static FairyGuiArtSlicePngInfo Invalid(string error)
        {
            return new FairyGuiArtSlicePngInfo(false, 0, 0, 0, -1, false, false, error);
        }

        [JsonPropertyName("valid")]
        public bool Valid { get; }

        [JsonPropertyName("width")]
        public int Width { get; }

        [JsonPropertyName("height")]
        public int Height { get; }

        [JsonPropertyName("bitDepth")]
        public int BitDepth { get; }

        [JsonPropertyName("colorType")]
        public int ColorType { get; }

        [JsonPropertyName("hasAlphaChannel")]
        public bool HasAlphaChannel { get; }

        [JsonPropertyName("hasNonOpaqueAlpha")]
        public bool HasNonOpaqueAlpha { get; }

        [JsonPropertyName("error")]
        public string Error { get; }

        [JsonPropertyName("sizeText")]
        public string SizeText => Width.ToString(CultureInfo.InvariantCulture) + "x" + Height.ToString(CultureInfo.InvariantCulture);
    }
}

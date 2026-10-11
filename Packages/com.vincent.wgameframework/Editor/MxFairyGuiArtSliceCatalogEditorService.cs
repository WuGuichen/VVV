using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class MxFairyGuiArtSliceCatalogEditorService
    {
        public static MxFairyGuiArtSliceEditorDocument LoadDocument(
            string repositoryRoot,
            string packageRootPath,
            string catalogPath)
        {
            string packageRootAbsolutePath = GetPackageRootAbsolutePath(repositoryRoot, packageRootPath);
            string catalogAbsolutePath = GetCatalogAbsolutePath(repositoryRoot, catalogPath);
            JObject catalogRoot = JObject.Parse(File.ReadAllText(catalogAbsolutePath));
            Dictionary<string, PackageImageRecord> images = ReadPackageImages(packageRootAbsolutePath);
            Dictionary<string, List<MxFairyGuiArtSliceEditorUsageRecord>> usages = ReadUsagePoints(packageRootPath, packageRootAbsolutePath);
            var slices = new List<MxFairyGuiArtSliceEditorSliceRecord>();
            JArray sliceTokens = catalogRoot["slices"] as JArray;
            if (sliceTokens != null)
            {
                for (int i = 0; i < sliceTokens.Count; i++)
                {
                    JObject sliceObject = sliceTokens[i] as JObject;
                    if (sliceObject == null)
                        continue;

                    slices.Add(CreateSliceRecord(sliceObject, images, usages));
                }
            }

            slices.Sort(CompareSlices);
            return new MxFairyGuiArtSliceEditorDocument(catalogRoot, slices);
        }

        public static void SaveCatalog(string catalogAbsolutePath, JObject catalogRoot)
        {
            File.WriteAllText(catalogAbsolutePath, JsonConvert.SerializeObject(catalogRoot, Formatting.Indented) + Environment.NewLine);
        }

        public static MxFairyGuiArtSliceEditorReportResult GenerateReport(
            string repositoryRoot,
            string manifestTestProjectPath,
            string reportPath)
        {
            string normalizedReportPath = NormalizePath(reportPath);
            string arguments = "run --project " + Quote(manifestTestProjectPath) + " -- --write-art-slice-report " + Quote(normalizedReportPath);
            ProcessStartInfo startInfo = new ProcessStartInfo("dotnet", arguments)
            {
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                        return MxFairyGuiArtSliceEditorReportResult.Fail("Failed to start dotnet report command.");

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        string absolutePath = Path.Combine(repositoryRoot, normalizedReportPath.Replace('/', Path.DirectorySeparatorChar));
                        return MxFairyGuiArtSliceEditorReportResult.Ok("Generated report: " + normalizedReportPath, absolutePath);
                    }

                    return MxFairyGuiArtSliceEditorReportResult.Fail("Report command failed: " + output + error);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return MxFairyGuiArtSliceEditorReportResult.Fail("Report command failed: " + ex.Message);
            }
        }

        internal static MxFairyGuiArtSliceEditorReplacementCheck ValidateReplacement(
            MxFairyGuiArtSliceEditorSliceRecord selected,
            string targetPath,
            string newPath)
        {
            PngInfo oldInfo = ReadPngInfo(targetPath);
            if (!oldInfo.Valid)
                return MxFairyGuiArtSliceEditorReplacementCheck.Fail("Existing PNG is invalid: " + oldInfo.Error);

            PngInfo newInfo = ReadPngInfo(newPath);
            if (!newInfo.Valid)
                return MxFairyGuiArtSliceEditorReplacementCheck.Fail("Replacement PNG is invalid: " + newInfo.Error);

            if (selected.RequireSamePixelSize && (oldInfo.Width != newInfo.Width || oldInfo.Height != newInfo.Height))
            {
                return MxFairyGuiArtSliceEditorReplacementCheck.Fail(
                    "Replacement must keep the same pixel size. Old="
                    + oldInfo.SizeText
                    + ", New="
                    + newInfo.SizeText
                    + ".");
            }

            if (selected.MinPixelWidth > 0 && newInfo.Width < selected.MinPixelWidth)
                return MxFairyGuiArtSliceEditorReplacementCheck.Fail("Replacement width is below minPixelWidth.");

            if (selected.MinPixelHeight > 0 && newInfo.Height < selected.MinPixelHeight)
                return MxFairyGuiArtSliceEditorReplacementCheck.Fail("Replacement height is below minPixelHeight.");

            if (selected.RequireAlphaChannel && !newInfo.HasAlphaChannel)
                return MxFairyGuiArtSliceEditorReplacementCheck.Fail("Replacement must include an alpha channel.");

            if (selected.RequireNonOpaqueAlpha && !newInfo.HasNonOpaqueAlpha)
                return MxFairyGuiArtSliceEditorReplacementCheck.Fail("Replacement must contain at least one non-opaque alpha pixel.");

            string groupWarning = !string.IsNullOrWhiteSpace(selected.GroupId) || selected.ReplaceTogether
                ? Environment.NewLine + "Group warning: review sibling slices before replacing only this file."
                : string.Empty;
            return MxFairyGuiArtSliceEditorReplacementCheck.Ok(
                "Target: "
                + selected.SourcePath
                + Environment.NewLine
                + "Old: "
                + oldInfo.SizeText
                + ", alpha="
                + oldInfo.HasAlphaChannel
                + ", nonOpaque="
                + oldInfo.HasNonOpaqueAlpha
                + Environment.NewLine
                + "New: "
                + newInfo.SizeText
                + ", alpha="
                + newInfo.HasAlphaChannel
                + ", nonOpaque="
                + newInfo.HasNonOpaqueAlpha
                + groupWarning);
        }

        public static void ReplaceFileAtomically(string sourcePath, string targetPath)
        {
            string directory = Path.GetDirectoryName(targetPath);
            string tempPath = Path.Combine(directory, Path.GetFileName(targetPath) + ".replacement.tmp");
            string backupPath = Path.Combine(directory, Path.GetFileName(targetPath) + ".replacement.bak");
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            if (File.Exists(backupPath))
                File.Delete(backupPath);

            File.Copy(sourcePath, tempPath);
            File.Replace(tempPath, targetPath, backupPath, true);
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }

        public static string GetPackageRootAbsolutePath(string repositoryRoot, string packageRootPath)
        {
            return Path.GetFullPath(Path.Combine(repositoryRoot, packageRootPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        public static string GetCatalogAbsolutePath(string repositoryRoot, string catalogPath)
        {
            return Path.GetFullPath(Path.Combine(repositoryRoot, catalogPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        public static string ResolvePackagePath(string packageRootAbsolutePath, string packageRelativePath)
        {
            return Path.GetFullPath(Path.Combine(packageRootAbsolutePath, NormalizePath(packageRelativePath).Replace('/', Path.DirectorySeparatorChar)));
        }

        internal static bool IsPathInside(string rootPath, string childPath)
        {
            string root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string child = Path.GetFullPath(childPath);
            return child.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        public static JArray ToTagArray(string tags)
        {
            var array = new JArray();
            if (string.IsNullOrWhiteSpace(tags))
                return array;

            string[] split = tags.Split(',');
            for (int i = 0; i < split.Length; i++)
            {
                string value = split[i].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    array.Add(value);
            }

            return array;
        }

        internal static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static MxFairyGuiArtSliceEditorSliceRecord CreateSliceRecord(
            JObject sliceObject,
            Dictionary<string, PackageImageRecord> images,
            Dictionary<string, List<MxFairyGuiArtSliceEditorUsageRecord>> usages)
        {
            string resourceId = ReadString(sliceObject, "fguiResourceId");
            PackageImageRecord image;
            string sourcePath = images.TryGetValue(resourceId, out image)
                ? image.SourcePath
                : ReadString(sliceObject, "expectedSourcePath");
            List<MxFairyGuiArtSliceEditorUsageRecord> usageList;
            if (!usages.TryGetValue(resourceId, out usageList))
                usageList = new List<MxFairyGuiArtSliceEditorUsageRecord>();

            JObject replacement = sliceObject["replacement"] as JObject;
            return new MxFairyGuiArtSliceEditorSliceRecord(
                sliceObject,
                ReadString(sliceObject, "sliceId"),
                resourceId,
                sourcePath,
                ReadString(sliceObject, "expectedSourcePath"),
                ReadString(sliceObject, "category"),
                ReadString(sliceObject, "displayName"),
                ReadString(sliceObject, "description"),
                ReadTags(sliceObject),
                ReadString(sliceObject, "groupId"),
                ReadString(sliceObject, "roleInGroup"),
                ReadBool(replacement, "requireAlphaChannel"),
                ReadBool(replacement, "requireNonOpaqueAlpha"),
                ReadBool(replacement, "requireSamePixelSize"),
                ReadBool(replacement, "replaceTogether"),
                ReadInt(replacement, "minPixelWidth"),
                ReadInt(replacement, "minPixelHeight"),
                usageList);
        }

        private static Dictionary<string, PackageImageRecord> ReadPackageImages(string packageRootAbsolutePath)
        {
            var images = new Dictionary<string, PackageImageRecord>(StringComparer.Ordinal);
            string packageXmlPath = Path.Combine(packageRootAbsolutePath, "package.xml");
            XDocument document = XDocument.Load(packageXmlPath);
            foreach (XElement image in document.Descendants("image"))
            {
                string id = Attribute(image, "id");
                string name = Attribute(image, "name");
                string path = Attribute(image, "path");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                images[id] = new PackageImageRecord(id, BuildPackageRelativePath(path, name));
            }

            return images;
        }

        private static Dictionary<string, List<MxFairyGuiArtSliceEditorUsageRecord>> ReadUsagePoints(
            string packageRootPath,
            string packageRootAbsolutePath)
        {
            var usages = new Dictionary<string, List<MxFairyGuiArtSliceEditorUsageRecord>>(StringComparer.Ordinal);
            string packageXmlPath = Path.Combine(packageRootAbsolutePath, "package.xml");
            XDocument packageDocument = XDocument.Load(packageXmlPath);
            foreach (XElement component in packageDocument.Descendants("component"))
            {
                string componentName = Attribute(component, "name");
                string componentPath = BuildPackageRelativePath(Attribute(component, "path"), componentName);
                string absoluteComponentPath = ResolvePackagePath(packageRootAbsolutePath, componentPath);
                if (!File.Exists(absoluteComponentPath))
                    continue;

                XDocument componentDocument = XDocument.Load(absoluteComponentPath);
                foreach (XElement image in componentDocument.Descendants("image"))
                {
                    string resourceId = Attribute(image, "src");
                    if (string.IsNullOrWhiteSpace(resourceId))
                        continue;

                    List<MxFairyGuiArtSliceEditorUsageRecord> list;
                    if (!usages.TryGetValue(resourceId, out list))
                    {
                        list = new List<MxFairyGuiArtSliceEditorUsageRecord>();
                        usages.Add(resourceId, list);
                    }

                    list.Add(new MxFairyGuiArtSliceEditorUsageRecord(
                        NormalizePath(packageRootPath + "/" + componentPath),
                        Attribute(image, "name"),
                        Attribute(image, "id"),
                        Attribute(image, "size")));
                }
            }

            return usages;
        }

        private static int CompareSlices(MxFairyGuiArtSliceEditorSliceRecord left, MxFairyGuiArtSliceEditorSliceRecord right)
        {
            int category = string.Compare(left.Category, right.Category, StringComparison.Ordinal);
            if (category != 0)
                return category;

            return string.Compare(left.SliceId, right.SliceId, StringComparison.Ordinal);
        }

        private static string ReadString(JObject obj, string propertyName)
        {
            if (obj == null)
                return string.Empty;

            JToken token = obj[propertyName];
            return token == null ? string.Empty : (token.Value<string>() ?? string.Empty);
        }

        private static bool ReadBool(JObject obj, string propertyName)
        {
            if (obj == null)
                return false;

            JToken token = obj[propertyName];
            return token != null && token.Type == JTokenType.Boolean && token.Value<bool>();
        }

        private static int ReadInt(JObject obj, string propertyName)
        {
            if (obj == null)
                return 0;

            JToken token = obj[propertyName];
            return token != null && token.Type == JTokenType.Integer ? token.Value<int>() : 0;
        }

        private static string ReadTags(JObject obj)
        {
            JArray tags = obj != null ? obj["tags"] as JArray : null;
            if (tags == null)
                return string.Empty;

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < tags.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                builder.Append(tags[i].Value<string>());
            }

            return builder.ToString();
        }

        private static string Attribute(XElement element, string name)
        {
            XAttribute attribute = element.Attribute(name);
            return attribute == null ? string.Empty : attribute.Value;
        }

        private static string BuildPackageRelativePath(string path, string fileName)
        {
            string normalizedPath = NormalizePath(path).Trim('/');
            string normalizedName = NormalizePath(fileName).TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalizedPath))
                return normalizedName;

            return normalizedPath + "/" + normalizedName;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static PngInfo ReadPngInfo(string path)
        {
            if (!File.Exists(path))
                return PngInfo.Invalid("File does not exist.");

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 33 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71)
                return PngInfo.Invalid("File is not a PNG.");

            int width = ReadBigEndianInt(bytes, 16);
            int height = ReadBigEndianInt(bytes, 20);
            int colorType = bytes[25];
            bool hasAlpha = colorType == 4 || colorType == 6 || ContainsChunk(bytes, "tRNS");
            bool hasNonOpaqueAlpha = false;
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                if (texture.LoadImage(bytes))
                {
                    Color32[] pixels = texture.GetPixels32();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        if (pixels[i].a < 255)
                        {
                            hasNonOpaqueAlpha = true;
                            break;
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            return PngInfo.Ok(width, height, hasAlpha, hasNonOpaqueAlpha);
        }

        private static int ReadBigEndianInt(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24)
                | (bytes[offset + 1] << 16)
                | (bytes[offset + 2] << 8)
                | bytes[offset + 3];
        }

        private static bool ContainsChunk(byte[] bytes, string chunkName)
        {
            byte[] chunk = System.Text.Encoding.ASCII.GetBytes(chunkName);
            for (int i = 8; i <= bytes.Length - 8; i++)
            {
                if (bytes[i] == chunk[0]
                    && bytes[i + 1] == chunk[1]
                    && bytes[i + 2] == chunk[2]
                    && bytes[i + 3] == chunk[3])
                    return true;
            }

            return false;
        }

        private readonly struct PackageImageRecord
        {
            public PackageImageRecord(string resourceId, string sourcePath)
            {
                ResourceId = resourceId ?? string.Empty;
                SourcePath = sourcePath ?? string.Empty;
            }

            public string ResourceId { get; }
            public string SourcePath { get; }
        }

        private readonly struct PngInfo
        {
            private PngInfo(bool valid, int width, int height, bool hasAlphaChannel, bool hasNonOpaqueAlpha, string error)
            {
                Valid = valid;
                Width = width;
                Height = height;
                HasAlphaChannel = hasAlphaChannel;
                HasNonOpaqueAlpha = hasNonOpaqueAlpha;
                Error = error ?? string.Empty;
            }

            public bool Valid { get; }
            public int Width { get; }
            public int Height { get; }
            public bool HasAlphaChannel { get; }
            public bool HasNonOpaqueAlpha { get; }
            public string Error { get; }
            public string SizeText => Width.ToString(CultureInfo.InvariantCulture) + " x " + Height.ToString(CultureInfo.InvariantCulture);

            public static PngInfo Ok(int width, int height, bool hasAlphaChannel, bool hasNonOpaqueAlpha)
            {
                return new PngInfo(true, width, height, hasAlphaChannel, hasNonOpaqueAlpha, string.Empty);
            }

            public static PngInfo Invalid(string error)
            {
                return new PngInfo(false, 0, 0, false, false, error);
            }
        }
    }

    public sealed class MxFairyGuiArtSliceEditorDocument
    {
        internal MxFairyGuiArtSliceEditorDocument(
            JObject catalogRoot,
            IReadOnlyList<MxFairyGuiArtSliceEditorSliceRecord> slices)
        {
            CatalogRoot = catalogRoot;
            Slices = slices ?? Array.Empty<MxFairyGuiArtSliceEditorSliceRecord>();
        }

        public JObject CatalogRoot { get; }
        public IReadOnlyList<MxFairyGuiArtSliceEditorSliceRecord> Slices { get; }
    }

    public sealed class MxFairyGuiArtSliceEditorSliceRecord
    {
        internal MxFairyGuiArtSliceEditorSliceRecord(
            JObject json,
            string sliceId,
            string resourceId,
            string sourcePath,
            string expectedSourcePath,
            string category,
            string displayName,
            string description,
            string tags,
            string groupId,
            string roleInGroup,
            bool requireAlphaChannel,
            bool requireNonOpaqueAlpha,
            bool requireSamePixelSize,
            bool replaceTogether,
            int minPixelWidth,
            int minPixelHeight,
            IReadOnlyList<MxFairyGuiArtSliceEditorUsageRecord> usages)
        {
            Json = json;
            SliceId = sliceId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            ExpectedSourcePath = expectedSourcePath ?? string.Empty;
            Category = category ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? SliceId : displayName;
            Description = description ?? string.Empty;
            Tags = tags ?? string.Empty;
            GroupId = groupId ?? string.Empty;
            RoleInGroup = roleInGroup ?? string.Empty;
            RequireAlphaChannel = requireAlphaChannel;
            RequireNonOpaqueAlpha = requireNonOpaqueAlpha;
            RequireSamePixelSize = requireSamePixelSize;
            ReplaceTogether = replaceTogether;
            MinPixelWidth = minPixelWidth;
            MinPixelHeight = minPixelHeight;
            Usages = usages ?? Array.Empty<MxFairyGuiArtSliceEditorUsageRecord>();
        }

        public JObject Json { get; }
        public string SliceId { get; }
        public string ResourceId { get; }
        public string SourcePath { get; }
        public string ExpectedSourcePath { get; }
        public string Category { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string Tags { get; }
        public string GroupId { get; }
        public string RoleInGroup { get; }
        public bool RequireAlphaChannel { get; }
        public bool RequireNonOpaqueAlpha { get; }
        public bool RequireSamePixelSize { get; }
        public bool ReplaceTogether { get; }
        public int MinPixelWidth { get; }
        public int MinPixelHeight { get; }
        public IReadOnlyList<MxFairyGuiArtSliceEditorUsageRecord> Usages { get; }
    }

    public readonly struct MxFairyGuiArtSliceEditorUsageRecord
    {
        internal MxFairyGuiArtSliceEditorUsageRecord(string componentPath, string controlName, string nodeId, string size)
        {
            ComponentPath = componentPath ?? string.Empty;
            ControlName = controlName ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            Size = size ?? string.Empty;
        }

        public string ComponentPath { get; }
        public string ControlName { get; }
        public string NodeId { get; }
        public string Size { get; }
    }

    internal readonly struct MxFairyGuiArtSliceEditorReplacementCheck
    {
        private MxFairyGuiArtSliceEditorReplacementCheck(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }

        internal static MxFairyGuiArtSliceEditorReplacementCheck Ok(string message)
        {
            return new MxFairyGuiArtSliceEditorReplacementCheck(true, message);
        }

        internal static MxFairyGuiArtSliceEditorReplacementCheck Fail(string message)
        {
            return new MxFairyGuiArtSliceEditorReplacementCheck(false, message);
        }
    }

    public readonly struct MxFairyGuiArtSliceEditorReportResult
    {
        private MxFairyGuiArtSliceEditorReportResult(bool success, string message, string reportPath)
        {
            Success = success;
            Message = message ?? string.Empty;
            ReportPath = reportPath ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }
        public string ReportPath { get; }

        internal static MxFairyGuiArtSliceEditorReportResult Ok(string message, string reportPath)
        {
            return new MxFairyGuiArtSliceEditorReportResult(true, message, reportPath);
        }

        internal static MxFairyGuiArtSliceEditorReportResult Fail(string message)
        {
            return new MxFairyGuiArtSliceEditorReportResult(false, message, string.Empty);
        }
    }
}

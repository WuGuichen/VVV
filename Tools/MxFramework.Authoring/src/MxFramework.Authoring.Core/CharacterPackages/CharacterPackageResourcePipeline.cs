using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MxFramework.Authoring
{
    public sealed class CharacterResourcePackageValidationOptions
    {
        public string PackageRootPath { get; set; } = string.Empty;
        public bool ValidateResourceFiles { get; set; }
        public bool ValidateResourceHashes { get; set; }
        public bool ValidatePreviewResources { get; set; } = true;
    }

    public sealed class CharacterPackageDependencyNode
    {
        public string ResourceKey { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string TypeId { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
    }

    public sealed class CharacterPackageDependencyEdge
    {
        public string FromResourceKey { get; set; } = string.Empty;
        public string ToResourceKey { get; set; } = string.Empty;
        public bool Required { get; set; } = true;
        public string Relation { get; set; } = string.Empty;
        public bool AffectsDependencyHash { get; set; } = true;
    }

    public sealed class CharacterPackageDependencyGraph
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<CharacterPackageDependencyNode> Nodes { get; set; } = new List<CharacterPackageDependencyNode>();
        public List<CharacterPackageDependencyEdge> Edges { get; set; } = new List<CharacterPackageDependencyEdge>();
    }

    public sealed class CharacterPackageResourceHashEntry
    {
        public string ResourceKey { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public string DeclaredContentHash { get; set; } = string.Empty;
        public string ComputedContentHash { get; set; } = string.Empty;
        public string ComputedImportHash { get; set; } = string.Empty;
        public string ComputedDependencyHash { get; set; } = string.Empty;
    }

    public sealed class CharacterPackageResourceHashReport
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public List<CharacterPackageResourceHashEntry> Entries { get; set; } = new List<CharacterPackageResourceHashEntry>();
        public CharacterAuthoringValidationReport Diagnostics { get; set; } = new CharacterAuthoringValidationReport();

        public bool HasBlockingIssues
        {
            get { return Diagnostics != null && Diagnostics.HasBlockingIssues; }
        }
    }

    public static class CharacterPackageResourceKeyGenerator
    {
        public static string Generate(string packageId, string typeId, string localId, string variant = "")
        {
            string packageSegment = NormalizeSegment(packageId);
            string typeSegment = NormalizeTypeSegment(typeId);
            string localSegment = NormalizeSegment(localId);
            string key = "char." + packageSegment + "." + typeSegment + "." + localSegment;
            string variantSegment = string.IsNullOrWhiteSpace(variant) ? string.Empty : NormalizeSegment(variant);
            if (!string.IsNullOrEmpty(variantSegment) && variantSegment != "default")
                key += "." + variantSegment;
            return key;
        }

        public static bool IsValidResourceKey(string resourceKey)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
                return false;

            for (int i = 0; i < resourceKey.Length; i++)
            {
                char c = resourceKey[i];
                bool valid = (c >= 'a' && c <= 'z') ||
                             (c >= '0' && c <= '9') ||
                             c == '.' ||
                             c == '_' ||
                             c == '-';
                if (!valid)
                    return false;
            }

            return resourceKey.IndexOf("..", StringComparison.Ordinal) < 0 &&
                   !resourceKey.StartsWith(".", StringComparison.Ordinal) &&
                   !resourceKey.EndsWith(".", StringComparison.Ordinal);
        }

        public static string NormalizeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var builder = new StringBuilder();
            bool lastWasDot = false;
            string lower = value.Trim().ToLowerInvariant();
            for (int i = 0; i < lower.Length; i++)
            {
                char c = lower[i];
                bool dot = c == '.' || c == '/' || c == '\\' || char.IsWhiteSpace(c);
                if (dot)
                {
                    if (!lastWasDot && builder.Length > 0)
                    {
                        builder.Append('.');
                        lastWasDot = true;
                    }
                    continue;
                }

                bool valid = (c >= 'a' && c <= 'z') ||
                             (c >= '0' && c <= '9') ||
                             c == '_' ||
                             c == '-';
                if (valid)
                {
                    builder.Append(c);
                    lastWasDot = false;
                }
            }

            string result = builder.ToString().Trim('.');
            return string.IsNullOrEmpty(result) ? "unknown" : result;
        }

        private static string NormalizeTypeSegment(string typeId)
        {
            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase))
                return "anim";
            return NormalizeSegment(typeId);
        }
    }

    public static class CharacterPackageHashUtility
    {
        public static string ComputeSha256(byte[] bytes)
        {
            if (bytes == null)
                bytes = Array.Empty<byte>();

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2 + 7);
                builder.Append("sha256:");
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        public static string ComputeTextSha256(string text)
        {
            return ComputeSha256(Encoding.UTF8.GetBytes(text ?? string.Empty));
        }

        public static string ComputeFileSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2 + 7);
                builder.Append("sha256:");
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        public static string NormalizeSha256(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return string.Empty;

            string trimmed = hash.Trim().ToLowerInvariant();
            if (trimmed.StartsWith("sha256:", StringComparison.Ordinal))
                return trimmed;
            return "sha256:" + trimmed;
        }
    }

    public static class CharacterPackageResourcePipeline
    {
        public static CharacterPackageDependencyGraph BuildDependencyGraph(CharacterPackageResourceCatalog catalog)
        {
            var graph = new CharacterPackageDependencyGraph();
            if (catalog == null)
                return graph;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ResourceKey))
                    continue;

                graph.Nodes.Add(new CharacterPackageDependencyNode
                {
                    ResourceKey = entry.ResourceKey,
                    StableId = entry.StableId,
                    TypeId = entry.TypeId,
                    Usage = entry.Usage,
                    RelativePath = entry.RelativePath
                });

                for (int j = 0; j < entry.Dependencies.Count; j++)
                {
                    CharacterPackageResourceDependency dependency = entry.Dependencies[j];
                    if (dependency == null || string.IsNullOrWhiteSpace(dependency.ResourceKey))
                        continue;

                    graph.Edges.Add(new CharacterPackageDependencyEdge
                    {
                        FromResourceKey = entry.ResourceKey,
                        ToResourceKey = dependency.ResourceKey,
                        Required = dependency.Required,
                        Relation = dependency.Relation,
                        AffectsDependencyHash = dependency.AffectsDependencyHash
                    });
                }
            }

            return graph;
        }

        public static CharacterPackageResourceHashReport BuildHashReport(CharacterResourcePackage package, string packageRootPath)
        {
            var report = new CharacterPackageResourceHashReport();
            if (package == null)
            {
                report.Diagnostics = CharacterResourcePackageValidator.Validate(package);
                return report;
            }

            report.PackageId = package.Manifest != null ? package.Manifest.PackageId : string.Empty;
            report.Diagnostics = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
            {
                PackageRootPath = packageRootPath,
                ValidateResourceFiles = true,
                ValidateResourceHashes = true
            });

            CharacterPackageResourceCatalog catalog = package.ResourceCatalog;
            if (catalog == null)
                return report;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                var hashEntry = new CharacterPackageResourceHashEntry
                {
                    ResourceKey = entry.ResourceKey,
                    RelativePath = entry.RelativePath,
                    DeclaredContentHash = GetDeclaredContentHash(entry),
                    ComputedImportHash = ComputeImportHash(entry),
                    ComputedDependencyHash = ComputeDependencyHash(entry, catalog)
                };

                string fullPath = ResolvePackagePath(packageRootPath, entry.RelativePath);
                hashEntry.Exists = !string.IsNullOrEmpty(fullPath) && File.Exists(fullPath);
                if (hashEntry.Exists)
                    hashEntry.ComputedContentHash = CharacterPackageHashUtility.ComputeFileSha256(fullPath);

                report.Entries.Add(hashEntry);
            }

            return report;
        }

        public static string GetDeclaredContentHash(CharacterPackageResourceEntry entry)
        {
            if (entry == null)
                return string.Empty;
            if (entry.Hashes != null && !string.IsNullOrWhiteSpace(entry.Hashes.ContentHash))
                return CharacterPackageHashUtility.NormalizeSha256(entry.Hashes.ContentHash);
            return CharacterPackageHashUtility.NormalizeSha256(entry.Hash);
        }

        public static string ComputeImportHash(CharacterPackageResourceEntry entry)
        {
            if (entry == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.Append(entry.TypeId).Append('\n');
            builder.Append(entry.SourceFormat).Append('\n');
            builder.Append(entry.Usage).Append('\n');
            CharacterPackageImportHint hint = entry.ImportHints;
            if (hint != null)
            {
                builder.Append(hint.TargetPathPolicy).Append('\n');
                builder.Append(hint.TargetRelativePath).Append('\n');
                builder.Append(hint.Scale.ToString("R")).Append('\n');
                builder.Append(hint.MaterialPolicy).Append('\n');
                builder.Append(hint.AnimationPolicy).Append('\n');
                builder.Append(hint.ProviderId).Append('\n');
                builder.Append(hint.UpAxis).Append('\n');
                builder.Append(hint.ForwardAxis).Append('\n');
                builder.Append(hint.CollisionPolicy).Append('\n');
                builder.Append(hint.PhysicsDataPolicy).Append('\n');
                AppendSorted(builder, hint.Labels);
                AppendSorted(builder, hint.Metadata);
            }

            return CharacterPackageHashUtility.ComputeTextSha256(builder.ToString());
        }

        public static string ComputeDependencyHash(CharacterPackageResourceEntry entry, CharacterPackageResourceCatalog catalog)
        {
            if (entry == null || catalog == null)
                return string.Empty;

            var dependencyKeys = new List<string>();
            for (int i = 0; i < entry.Dependencies.Count; i++)
            {
                CharacterPackageResourceDependency dependency = entry.Dependencies[i];
                if (dependency != null && dependency.AffectsDependencyHash && !string.IsNullOrWhiteSpace(dependency.ResourceKey))
                    dependencyKeys.Add(dependency.ResourceKey);
            }

            dependencyKeys.Sort(StringComparer.Ordinal);
            var builder = new StringBuilder();
            for (int i = 0; i < dependencyKeys.Count; i++)
            {
                string key = dependencyKeys[i];
                CharacterPackageResourceEntry dependencyEntry = FindByKey(catalog, key);
                builder.Append(key).Append('|');
                if (dependencyEntry != null)
                    builder.Append(GetDeclaredContentHash(dependencyEntry));
                builder.Append('\n');
            }

            return CharacterPackageHashUtility.ComputeTextSha256(builder.ToString());
        }

        public static string GetEffectiveSourceFormat(CharacterPackageResourceEntry entry)
        {
            if (entry == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(entry.SourceFormat))
                return entry.SourceFormat.Trim();

            string path = entry.RelativePath ?? string.Empty;
            string extension = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(extension))
                return string.Empty;
            return extension.TrimStart('.').ToLowerInvariant();
        }

        public static bool IsFutureFormat(CharacterPackageResourceEntry entry)
        {
            string format = GetEffectiveSourceFormat(entry);
            return string.Equals(format, CharacterPackageResourceFormatIds.Fbx, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSupportedV1Format(CharacterPackageResourceEntry entry)
        {
            if (entry == null)
                return false;

            string typeId = entry.TypeId ?? string.Empty;
            string format = GetEffectiveSourceFormat(entry);
            if (string.IsNullOrWhiteSpace(format))
                return false;

            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
                return IsAny(format, CharacterPackageResourceFormatIds.Glb, CharacterPackageResourceFormatIds.Gltf);
            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Texture, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(typeId, CharacterPackageResourceTypeIds.Preview, StringComparison.OrdinalIgnoreCase))
                return IsAny(format, CharacterPackageResourceFormatIds.Png, CharacterPackageResourceFormatIds.Jpeg, CharacterPackageResourceFormatIds.Jpg, CharacterPackageResourceFormatIds.Tga);
            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Material, StringComparison.OrdinalIgnoreCase))
                return IsAny(format, CharacterPackageResourceFormatIds.Json, CharacterPackageResourceFormatIds.MaterialJson);
            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase))
                return IsAny(format, CharacterPackageResourceFormatIds.Glb, CharacterPackageResourceFormatIds.Gltf, CharacterPackageResourceFormatIds.AnimationGroupJson, CharacterPackageResourceFormatIds.Json);
            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Audio, StringComparison.OrdinalIgnoreCase))
                return IsAny(format, CharacterPackageResourceFormatIds.Wav, CharacterPackageResourceFormatIds.Ogg);
            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Vfx, StringComparison.OrdinalIgnoreCase))
                return IsAny(format, CharacterPackageResourceFormatIds.Json, CharacterPackageResourceFormatIds.VfxJson);
            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Config, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(typeId, CharacterPackageResourceTypeIds.Geometry, StringComparison.OrdinalIgnoreCase))
                return IsAny(format, CharacterPackageResourceFormatIds.Json);

            return false;
        }

        public static string ResolvePackagePath(string packageRootPath, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(packageRootPath) || string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;
            if (!IsSafePackageRelativePath(relativePath))
                return string.Empty;
            return Path.Combine(packageRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static bool IsSafePackageRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;
            if (relativePath.IndexOf('\0') >= 0)
                return false;
            if (Path.IsPathRooted(relativePath))
                return false;

            string[] parts = relativePath.Replace('\\', '/').Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "..")
                    return false;
            }

            return true;
        }

        public static CharacterPackageResourceEntry FindByKey(CharacterPackageResourceCatalog catalog, string resourceKey)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(resourceKey))
                return null;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry != null && string.Equals(entry.ResourceKey, resourceKey, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static bool IsAny(string value, params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (string.Equals(value, candidates[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void AppendSorted(StringBuilder builder, List<string> values)
        {
            if (values == null || values.Count == 0)
                return;

            var sorted = new List<string>(values);
            sorted.Sort(StringComparer.Ordinal);
            for (int i = 0; i < sorted.Count; i++)
                builder.Append(sorted[i]).Append('\n');
        }

        private static void AppendSorted(StringBuilder builder, Dictionary<string, string> values)
        {
            if (values == null || values.Count == 0)
                return;

            var keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
                builder.Append(keys[i]).Append('=').Append(values[keys[i]]).Append('\n');
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MxFramework.Authoring
{
    public static class VfxReferencePackSchemas
    {
        public const string ManifestV1 = "mx.vfx.reference-pack.v1";
        public const string ManifestV2 = "mx.vfx.reference-pack.v2";
        public const string LocalRegistrationV1 = "mx.vfx.reference-pack-registration.v1";
        public const string LocalRegistrationV2 = "mx.vfx.reference-pack-registration.v2";
    }

    public static class VfxReferencePackIds
    {
        public const string PilotoArpgRealisticDemonVsAngels = "reference.piloto.arpg-realistic-demon-vs-angels";
    }

    public static class VfxReferenceUsagePolicies
    {
        public const string ReferenceOnly = "ReferenceOnly";
        public const string LicensedReusable = "LicensedReusable";
    }

    public static class VfxReferenceSourceKinds
    {
        public const string LicensedThirdParty = "LicensedThirdParty";
    }

    /// <summary>Explicit operator-supplied authorization for a reusable third-party VFX source.</summary>
    /// <remarks>AllocByDesign. Authorization is never inferred from package contents or file names.</remarks>
    public sealed class VfxReferencePackScanOptions
    {
        public string UsagePolicy { get; set; } = VfxReferenceUsagePolicies.ReferenceOnly;
        public string SourcePackageId { get; set; } = string.Empty;
        public string LicenseId { get; set; } = string.Empty;
        public bool PublicRedistributionAllowed { get; set; }

        public static VfxReferencePackScanOptions ReferenceOnly() => new VfxReferencePackScanOptions();
    }

    public static class VfxReferencePackDiagnosticCodes
    {
        public const string SchemaInvalid = "VFXREF001";
        public const string SourceMissing = "VFXREF002";
        public const string DependencyMissing = "VFXREF003";
        public const string UsageViolation = "VFXREF004";
        public const string ContentStale = "VFXREF005";
    }

    /// <summary>Machine-local registration for an external, reference-only VFX pack.</summary>
    /// <remarks>AllocByDesign. This document is written only below Temp and never enters a stable report.</remarks>
    public sealed class VfxReferencePackLocalRegistration
    {
        public string SchemaVersion { get; set; } = VfxReferencePackSchemas.LocalRegistrationV2;
        public string ReferencePackId { get; set; } = VfxReferencePackIds.PilotoArpgRealisticDemonVsAngels;
        public string SourceRoot { get; set; } = string.Empty;
        public string ManifestPath { get; set; } = string.Empty;
        public string UsagePolicy { get; set; } = VfxReferenceUsagePolicies.ReferenceOnly;
        public string SourcePackageId { get; set; } = string.Empty;
        public string LicenseId { get; set; } = string.Empty;
        public bool PublicRedistributionAllowed { get; set; }
    }

    public sealed class VfxReferenceEffectSummary
    {
        public string ReferenceEffectId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ComplexityTier { get; set; } = string.Empty;
        public string CollectionId { get; set; } = string.Empty;
        public string ThemeVariant { get; set; } = string.Empty;
        public string QualityTier { get; set; } = string.Empty;
        public string SourceAssetPath { get; set; } = string.Empty;
        public string SourcePrefabGuid { get; set; } = string.Empty;
        public List<string> SemanticRoles { get; set; } = new List<string>();
        public List<string> LayerRoles { get; set; } = new List<string>();
        public int GameObjectCount { get; set; }
        public int ParticleSystemCount { get; set; }
        public int RendererCount { get; set; }
        public int MaterialCount { get; set; }
        public int MeshCount { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        public string TechnicalFingerprint { get; set; } = string.Empty;
        public string CreativeFingerprint { get; set; } = string.Empty;
        public int RecommendedLayerBudget { get; set; }
    }

    /// <summary>Stable, path-sanitized evidence extracted from a local reference pack.</summary>
    /// <remarks>AllocByDesign. Contains no absolute path and grants no asset reuse permission.</remarks>
    public sealed class VfxReferencePackManifest
    {
        public string SchemaVersion { get; set; } = VfxReferencePackSchemas.ManifestV2;
        public string ReferencePackId { get; set; } = VfxReferencePackIds.PilotoArpgRealisticDemonVsAngels;
        public string UsagePolicy { get; set; } = VfxReferenceUsagePolicies.ReferenceOnly;
        public string SourceKind { get; set; } = VfxReferenceSourceKinds.LicensedThirdParty;
        public string SourcePackageId { get; set; } = string.Empty;
        public string LicenseId { get; set; } = string.Empty;
        public bool PublicRedistributionAllowed { get; set; }
        public string SourceUnityVersion { get; set; } = string.Empty;
        public string SourceUrpVersion { get; set; } = string.Empty;
        public int PrefabCount { get; set; }
        public int DependencyCount { get; set; }
        public int MaterialCount { get; set; }
        public int TextureCount { get; set; }
        public int MeshCount { get; set; }
        public int ShaderGraphCount { get; set; }
        public string DependencySetHash { get; set; } = string.Empty;
        public List<VfxReferenceEffectSummary> Effects { get; set; } = new List<VfxReferenceEffectSummary>();
        public string ManifestHash { get; set; } = string.Empty;
    }

    public sealed class VfxReferencePackScanResult
    {
        public VfxReferencePackManifest Manifest { get; set; }
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
        public bool HasErrors => VfxCreativeFlywheelUtilities.HasErrors(Diagnostics);
    }

    /// <summary>Scans an external Unity VFX package without copying or importing its assets.</summary>
    /// <remarks>AllocByDesign. Performs low-frequency file enumeration, text parsing and hashing.</remarks>
    public static class VfxReferencePackScanner
    {
        private static readonly Regex GuidReference = new Regex("guid:\\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);
        private static readonly Regex MetaGuid = new Regex("^guid:\\s*([0-9a-fA-F]{32})", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex MaterialReference = new Regex("\\{fileID:\\s*2100000,\\s*guid:\\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);
        private static readonly Regex MeshReference = new Regex("m_Mesh:\\s*\\{fileID:[^,}]+,\\s*guid:\\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);

        public static VfxReferencePackScanResult ScanPiloto(string sourceRoot) => ScanPiloto(sourceRoot, VfxReferencePackScanOptions.ReferenceOnly());

        public static VfxReferencePackScanResult ScanPiloto(string sourceRoot, VfxReferencePackScanOptions options)
        {
            var result = new VfxReferencePackScanResult();
            options ??= VfxReferencePackScanOptions.ReferenceOnly();
            if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
            {
                Add(result, CharacterAuthoringValidationSeverity.Error, VfxReferencePackDiagnosticCodes.SourceMissing, "Reference pack source folder is missing.", "Register the local Piloto Studio reference folder.");
                return result;
            }

            string fullSource = Path.GetFullPath(sourceRoot);
            string projectRoot = FindUnityProjectRoot(fullSource);
            if (string.IsNullOrEmpty(projectRoot))
            {
                Add(result, CharacterAuthoringValidationSeverity.Error, VfxReferencePackDiagnosticCodes.SourceMissing, "Reference pack is not located below a Unity project Assets folder.", "Select the ARPG_Realistic Demon VS Angels folder inside its source Unity project.");
                return result;
            }

            string[] prefabs = Directory.GetFiles(fullSource, "*.prefab", SearchOption.AllDirectories);
            Array.Sort(prefabs, StringComparer.OrdinalIgnoreCase);
            var guidIndex = BuildGuidIndex(Path.Combine(projectRoot, "Assets"));
            var dependencyFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var missingGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var manifest = new VfxReferencePackManifest
            {
                UsagePolicy = options.UsagePolicy,
                SourcePackageId = options.SourcePackageId ?? string.Empty,
                LicenseId = options.LicenseId ?? string.Empty,
                PublicRedistributionAllowed = options.PublicRedistributionAllowed,
                SourceUnityVersion = ReadUnityVersion(projectRoot),
                SourceUrpVersion = ReadUrpVersion(projectRoot),
                PrefabCount = prefabs.Length
            };

            for (int i = 0; i < prefabs.Length; i++)
            {
                string text = File.ReadAllText(prefabs[i]);
                CollectDependencies(text, guidIndex, dependencyFiles, missingGuids);
                manifest.Effects.Add(BuildEffectSummary(projectRoot, fullSource, prefabs[i], text));
            }

            var queue = new Queue<string>(dependencyFiles);
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (queue.Count > 0)
            {
                string path = queue.Dequeue();
                if (!processed.Add(path) || !IsTextDependency(path)) continue;
                string text;
                try { text = File.ReadAllText(path); }
                catch (IOException) { continue; }
                var discovered = new HashSet<string>(dependencyFiles, StringComparer.OrdinalIgnoreCase);
                CollectDependencies(text, guidIndex, dependencyFiles, missingGuids);
                foreach (string dependency in dependencyFiles)
                    if (!discovered.Contains(dependency) && IsTextDependency(dependency)) queue.Enqueue(dependency);
            }

            var dependencyEvidence = new List<string>();
            foreach (string dependency in dependencyFiles)
            {
                string extension = Path.GetExtension(dependency).ToLowerInvariant();
                if (extension == ".mat") manifest.MaterialCount++;
                else if (extension == ".png" || extension == ".tga" || extension == ".jpg" || extension == ".jpeg" || extension == ".exr" || extension == ".psd") manifest.TextureCount++;
                else if (extension == ".fbx" || extension == ".obj" || extension == ".asset") manifest.MeshCount++;
                else if (extension == ".shadergraph" || extension == ".shadersubgraph") manifest.ShaderGraphCount++;
                dependencyEvidence.Add(ToProjectRelative(projectRoot, dependency) + "|" + VfxCreativeFlywheelHash.ComputeFile(dependency));
            }
            dependencyEvidence.Sort(StringComparer.Ordinal);
            manifest.DependencyCount = dependencyFiles.Count;
            manifest.DependencySetHash = VfxCreativeFlywheelHash.Compute(dependencyEvidence);
            manifest.ManifestHash = VfxCreativeFlywheelHash.Compute(manifest, "manifestHash");
            result.Manifest = manifest;

            if (string.Equals(options.UsagePolicy, VfxReferenceUsagePolicies.LicensedReusable, StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(options.SourcePackageId) || string.IsNullOrWhiteSpace(options.LicenseId) || !options.PublicRedistributionAllowed))
                Add(result, CharacterAuthoringValidationSeverity.Error, VfxReferencePackDiagnosticCodes.UsageViolation, "LicensedReusable sources require an explicit package id, license id, and redistribution authorization.", "Register the operator-confirmed authorization instead of inferring permission from package contents.");
            if (missingGuids.Count > 0)
                Add(result, CharacterAuthoringValidationSeverity.Warning, VfxReferencePackDiagnosticCodes.DependencyMissing, "Reference pack has " + missingGuids.Count + " unresolved GUID references, including Unity/package built-ins.", "Open the source demo scene and confirm that all reference effects render correctly.");
            return result;
        }

        public static bool ValidateManifest(VfxReferencePackManifest manifest, string expectedHash, out string message)
        {
            if (manifest == null || !EqualsAny(manifest.SchemaVersion, VfxReferencePackSchemas.ManifestV1, VfxReferencePackSchemas.ManifestV2) ||
                !string.Equals(manifest.ReferencePackId, VfxReferencePackIds.PilotoArpgRealisticDemonVsAngels, StringComparison.Ordinal) ||
                !EqualsAny(manifest.UsagePolicy, VfxReferenceUsagePolicies.ReferenceOnly, VfxReferenceUsagePolicies.LicensedReusable))
            { message = "Reference pack manifest schema, id, or usage policy is invalid."; return false; }
            if (string.Equals(manifest.UsagePolicy, VfxReferenceUsagePolicies.LicensedReusable, StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(manifest.SourcePackageId) || string.IsNullOrWhiteSpace(manifest.LicenseId) || !manifest.PublicRedistributionAllowed))
            { message = "LicensedReusable reference pack authorization is incomplete."; return false; }
            string actual = VfxCreativeFlywheelHash.Compute(manifest, "manifestHash");
            if (!string.Equals(actual, manifest.ManifestHash, StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(expectedHash) && !string.Equals(expectedHash, manifest.ManifestHash, StringComparison.OrdinalIgnoreCase))
            { message = "Reference pack manifest hash is invalid or stale."; return false; }
            message = string.Empty;
            return true;
        }

        private static VfxReferenceEffectSummary BuildEffectSummary(string projectRoot, string sourceRoot, string prefabPath, string text)
        {
            string name = Path.GetFileNameWithoutExtension(prefabPath);
            string relative = ToProjectRelative(projectRoot, prefabPath);
            string collection = Collection(relative);
            string quality = QualityTier(relative, text);
            var summary = new VfxReferenceEffectSummary
            {
                ReferenceEffectId = "piloto." + StableToken(Path.ChangeExtension(relative, null)),
                DisplayName = name,
                ComplexityTier = new DirectoryInfo(Path.GetDirectoryName(prefabPath) ?? sourceRoot).Name,
                CollectionId = collection,
                ThemeVariant = ThemeVariant(relative, name),
                QualityTier = quality,
                SourceAssetPath = relative,
                SourcePrefabGuid = ReadMetaGuid(prefabPath + ".meta"),
                GameObjectCount = Count(text, "GameObject:"),
                ParticleSystemCount = Count(text, "ParticleSystem:"),
                RendererCount = Count(text, "ParticleSystemRenderer:"),
                MaterialCount = UniqueCount(MaterialReference, text),
                MeshCount = UniqueCount(MeshReference, text),
                ContentHash = VfxCreativeFlywheelHash.ComputeFile(prefabPath)
            };
            AddSemanticRoles(summary.SemanticRoles, name);
            AddLayerRoles(summary.LayerRoles, text);
            summary.RecommendedLayerBudget = RecommendedLayerBudget(quality);
            summary.TechnicalFingerprint = VfxCreativeFlywheelHash.Compute(new { summary.ParticleSystemCount, summary.RendererCount, summary.MaterialCount, summary.MeshCount, summary.CollectionId, summary.QualityTier });
            summary.CreativeFingerprint = VfxCreativeFlywheelHash.Compute(new { summary.SemanticRoles, summary.LayerRoles, summary.ThemeVariant, summary.QualityTier });
            return summary;
        }

        private static void AddLayerRoles(List<string> roles, string text)
        {
            string lower = text.ToLowerInvariant();
            AddRole(roles, lower, "flare", "Flash");
            AddRole(roles, lower, "glow", "Glow");
            AddRole(roles, lower, "core", "Core");
            AddRole(roles, lower, "flame", "Flame");
            AddRole(roles, lower, "fleck", "Sparks");
            AddRole(roles, lower, "spark", "Sparks");
            AddRole(roles, lower, "smoke", "Smoke");
            AddRole(roles, lower, "nova", "Shockwave");
            AddRole(roles, lower, "decal", "Decal");
            AddRole(roles, lower, "trail", "Trail");
            AddRole(roles, lower, "lightning", "Arc");
            roles.Sort(StringComparer.Ordinal);
        }

        private static void AddRole(List<string> roles, string source, string token, string role)
        { if (source.Contains(token) && !roles.Contains(role)) roles.Add(role); }

        private static string Collection(string path)
        {
            if (path.IndexOf("ARPG_Realistic Essentials Fire", StringComparison.OrdinalIgnoreCase) >= 0) return "Fire";
            if (path.IndexOf("ARPG Lightning Starter Kit", StringComparison.OrdinalIgnoreCase) >= 0) return "Lightning";
            return "General";
        }

        private static string ThemeVariant(string path, string name)
        {
            string value = (path + "/" + name).ToLowerInvariant();
            if (value.Contains("purple")) return "purple";
            if (value.Contains("blue")) return "blue";
            if (value.Contains("green")) return "green";
            if (value.Contains("red")) return "red";
            return "default";
        }

        private static string QualityTier(string path, string text)
        {
            int layers = Count(text, "ParticleSystem:");
            if (path.IndexOf("/Complex/", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("Complex_VFXs", StringComparison.OrdinalIgnoreCase) >= 0 || layers > 30) return "Showcase";
            if (path.IndexOf("/Mid/", StringComparison.OrdinalIgnoreCase) >= 0 || layers > 12) return "GameplayImportant";
            return "Normal";
        }

        private static int RecommendedLayerBudget(string qualityTier)
        { return qualityTier == "Showcase" ? 60 : qualityTier == "GameplayImportant" ? 30 : 12; }

        private static string ReadMetaGuid(string path)
        {
            if (!File.Exists(path)) return string.Empty;
            foreach (string line in File.ReadLines(path)) if (line.StartsWith("guid: ", StringComparison.Ordinal)) return line.Substring(6).Trim();
            return string.Empty;
        }

        private static bool EqualsAny(string value, string left, string right) => string.Equals(value, left, StringComparison.Ordinal) || string.Equals(value, right, StringComparison.Ordinal);

        private static void AddSemanticRoles(List<string> roles, string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("hit") || lower.Contains("explotion") || lower.Contains("impact")) roles.Add(VfxAuthoringRoles.Impact);
            if (lower.Contains("trail") || lower.Contains("orb") || lower.Contains("spear")) roles.Add(VfxAuthoringRoles.Flight);
            if (lower.Contains("trail") || lower.Contains("spear")) roles.Add(VfxAuthoringRoles.Beam);
            if (lower.Contains("aura") || lower.Contains("buff")) { roles.Add(VfxAuthoringRoles.Charge); roles.Add(VfxAuthoringRoles.Aura); roles.Add(VfxAuthoringRoles.Status); }
            if (lower.Contains("telegraph")) roles.Add(VfxAuthoringRoles.Telegraph);
            if (lower.Contains("ground") || lower.Contains("aoe")) roles.Add(VfxAuthoringRoles.Zone);
            if (lower.Contains("shield")) roles.Add(VfxAuthoringRoles.Environment);
            roles.Sort(StringComparer.Ordinal);
        }

        private static Dictionary<string, string> BuildGuidIndex(string assetsRoot)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string meta in Directory.GetFiles(assetsRoot, "*.meta", SearchOption.AllDirectories))
            {
                string text;
                try { text = File.ReadAllText(meta); }
                catch (IOException) { continue; }
                Match match = MetaGuid.Match(text);
                if (match.Success)
                {
                    string asset = meta.Substring(0, meta.Length - 5);
                    if (File.Exists(asset)) result[match.Groups[1].Value] = asset;
                }
            }
            return result;
        }

        private static void CollectDependencies(string text, Dictionary<string, string> guidIndex, HashSet<string> dependencies, HashSet<string> missing)
        {
            foreach (Match match in GuidReference.Matches(text))
            {
                string guid = match.Groups[1].Value;
                if (guidIndex.TryGetValue(guid, out string path)) dependencies.Add(path);
                else if (!guid.StartsWith("0000000000000000", StringComparison.Ordinal)) missing.Add(guid);
            }
        }

        private static string FindUnityProjectRoot(string path)
        {
            var directory = new DirectoryInfo(path);
            while (directory != null)
            {
                if (string.Equals(directory.Name, "Assets", StringComparison.OrdinalIgnoreCase) && directory.Parent != null)
                    return directory.Parent.FullName;
                directory = directory.Parent;
            }
            return string.Empty;
        }

        private static string ReadUnityVersion(string projectRoot)
        {
            string path = Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt");
            if (!File.Exists(path)) return string.Empty;
            foreach (string line in File.ReadLines(path)) if (line.StartsWith("m_EditorVersion:", StringComparison.Ordinal)) return line.Substring(line.IndexOf(':') + 1).Trim();
            return string.Empty;
        }

        private static string ReadUrpVersion(string projectRoot)
        {
            string path = Path.Combine(projectRoot, "Packages", "manifest.json");
            if (!File.Exists(path)) return string.Empty;
            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(path));
            if (json.RootElement.TryGetProperty("dependencies", out JsonElement dependencies) && dependencies.TryGetProperty("com.unity.render-pipelines.universal", out JsonElement version)) return version.GetString() ?? string.Empty;
            return string.Empty;
        }

        private static bool IsTextDependency(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".mat" || extension == ".shadergraph" || extension == ".shadersubgraph" || extension == ".asset" || extension == ".prefab";
        }

        private static int Count(string text, string marker)
        {
            int count = 0, index = 0;
            while ((index = text.IndexOf(marker, index, StringComparison.Ordinal)) >= 0) { count++; index += marker.Length; }
            return count;
        }

        private static int UniqueCount(Regex regex, string text)
        {
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in regex.Matches(text)) values.Add(match.Groups[1].Value);
            return values.Count;
        }

        private static string StableToken(string value)
        {
            string token = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
            return string.IsNullOrEmpty(token) ? "effect" : token;
        }

        private static string ToProjectRelative(string projectRoot, string path)
        {
            string prefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? full.Substring(prefix.Length).Replace('\\', '/') : Path.GetFileName(path);
        }

        private static void Add(VfxReferencePackScanResult result, CharacterAuthoringValidationSeverity severity, string code, string message, string fix)
        {
            result.Diagnostics.Add(new AuthoringResourceDiagnostic { Severity = severity, Code = code, SourceStableId = VfxReferencePackIds.PilotoArpgRealisticDemonVsAngels, Message = message, SuggestedFix = fix, SourceField = "referencePack" });
        }
    }
}

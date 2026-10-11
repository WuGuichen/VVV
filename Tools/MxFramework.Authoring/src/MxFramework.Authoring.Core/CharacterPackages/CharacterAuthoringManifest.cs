using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace MxFramework.Authoring
{
    public sealed class CharacterAuthoringManifestDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string CharacterId { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Revision { get; set; } = 1;
        public string SourceRoot { get; set; } = ".";
        public string DerivedFromCharacterId { get; set; } = string.Empty;
        public CharacterAuthoringAuthority Authority { get; set; } = new CharacterAuthoringAuthority();
        public List<CharacterAuthoringManifestItem> Items { get; set; } = new List<CharacterAuthoringManifestItem>();
    }

    public sealed class CharacterAuthoringAuthority
    {
        public string BlenderPath { get; set; } = string.Empty;
        public string FinalFbxPath { get; set; } = string.Empty;
    }

    public sealed class CharacterAuthoringManifestItem
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
    }

    public static class CharacterAuthoringAssetKinds
    {
        public const string AiRaw = "AiRaw";
        public const string Provenance = "Provenance";
        public const string ModelReference = "ModelReference";
        public const string BlenderWork = "BlenderWork";
        public const string BlenderMilestone = "BlenderMilestone";
        public const string BlenderAuthority = "BlenderAuthority";
        public const string Audit = "Audit";
        public const string FinalFbx = "FinalFbx";
        public const string Texture = "Texture";

        public static readonly HashSet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            AiRaw, Provenance, ModelReference, BlenderWork, BlenderMilestone,
            BlenderAuthority, Audit, FinalFbx, Texture
        };
    }

    public sealed class CharacterAuthoringValidationDiagnostic
    {
        public string Code { get; set; } = string.Empty;
        public string Severity { get; set; } = "Error";
        public string ItemId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class CharacterDccAuthoringValidationReport
    {
        public string CharacterId { get; set; } = string.Empty;
        public List<CharacterAuthoringValidationDiagnostic> Diagnostics { get; set; } = new List<CharacterAuthoringValidationDiagnostic>();
        public bool HasBlockingIssues => Diagnostics.Any(item => string.Equals(item.Severity, "Error", StringComparison.Ordinal));
    }

    public static class CharacterAuthoringManifestValidator
    {
        private static readonly Regex NumericCharacterId = new Regex("^[0-9]{6}$", RegexOptions.CultureInvariant);
        private static readonly Regex Sha256 = new Regex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);
        private static readonly Regex MilestoneName = new Regex("^[0-9]{6}_M[0-3]_[a-z0-9_]+\\.blend$", RegexOptions.CultureInvariant);

        public static CharacterDccAuthoringValidationReport Validate(
            string characterRoot,
            CharacterAuthoringManifestDocument document,
            string expectedCharacterId)
        {
            var report = new CharacterDccAuthoringValidationReport { CharacterId = expectedCharacterId ?? string.Empty };
            if (document == null)
            {
                Error(report, "CHAR_AUTHORING_MANIFEST_MISSING", string.Empty, string.Empty, "authoring_manifest.json is missing or unreadable.");
                return report;
            }
            if (!string.Equals(document.SchemaVersion, "1.0", StringComparison.Ordinal))
                Error(report, "CHAR_AUTHORING_SCHEMA_UNSUPPORTED", string.Empty, string.Empty, "authoring manifest schemaVersion must be 1.0.");
            if (!NumericCharacterId.IsMatch(document.CharacterId ?? string.Empty)
                || !string.Equals(document.CharacterId, expectedCharacterId, StringComparison.Ordinal)
                || !string.Equals(document.PackageId, expectedCharacterId, StringComparison.Ordinal))
                Error(report, "CHAR_AUTHORING_ID_MISMATCH", string.Empty, string.Empty, "characterId/packageId must match the six-digit ArtSource folder.");
            if (!string.Equals(document.SourceRoot ?? string.Empty, ".", StringComparison.Ordinal))
                Error(report, "CHAR_AUTHORING_SOURCE_ROOT_INVALID", string.Empty, document.SourceRoot, "sourceRoot must be '.'.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CharacterAuthoringManifestItem item in document.Items ?? new List<CharacterAuthoringManifestItem>())
            {
                string id = (item?.Id ?? string.Empty).Trim();
                string path = NormalizePath(item?.Path);
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                    Error(report, "CHAR_AUTHORING_ITEM_ID_INVALID", id, path, "Item id must be non-empty and unique.");
                if (item == null || !CharacterAuthoringAssetKinds.All.Contains(item.Kind ?? string.Empty))
                    Error(report, "CHAR_AUTHORING_KIND_INVALID", id, path, "Item kind is not supported.");
                if (!IsSafeRelativePath(path))
                {
                    Error(report, "CHAR_AUTHORING_PATH_UNSAFE", id, path, "Item path must be a safe path inside the character root.");
                    continue;
                }
                if (!paths.Add(path))
                    Error(report, "CHAR_AUTHORING_PATH_DUPLICATE", id, path, "A managed path may be declared only once.");
                ValidateKindPath(report, expectedCharacterId, item, path);
                string fullPath = Path.GetFullPath(Path.Combine(characterRoot, path.Replace('/', Path.DirectorySeparatorChar)));
                string root = Path.GetFullPath(characterRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    Error(report, "CHAR_AUTHORING_PATH_ESCAPE", id, path, "Item path escapes the character root.");
                    continue;
                }
                if (!File.Exists(fullPath))
                {
                    Error(report, "CHAR_AUTHORING_FILE_MISSING", id, path, "Declared optional source file is missing.");
                    continue;
                }
                if (!Sha256.IsMatch(item.Sha256 ?? string.Empty))
                {
                    Error(report, "CHAR_AUTHORING_HASH_INVALID", id, path, "Declared source must carry a lowercase sha256: hash.");
                    continue;
                }
                string actual = ComputeSha256(fullPath);
                if (!string.Equals(actual, item.Sha256, StringComparison.Ordinal))
                    Error(report, "CHAR_AUTHORING_HASH_STALE", id, path, "Declared source hash does not match file content.");
            }

            ValidateAuthority(report, document, paths);
            return report;
        }

        public static string ComputeSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = sha.ComputeHash(stream);
            return "sha256:" + string.Concat(bytes.Select(value => value.ToString("x2")));
        }

        private static void ValidateAuthority(CharacterDccAuthoringValidationReport report, CharacterAuthoringManifestDocument document, HashSet<string> paths)
        {
            string blender = NormalizePath(document.Authority?.BlenderPath);
            string fbx = NormalizePath(document.Authority?.FinalFbxPath);
            if (!string.IsNullOrEmpty(blender) && !paths.Contains(blender))
                Error(report, "CHAR_AUTHORING_AUTHORITY_UNDECLARED", string.Empty, blender, "Blender authority must reference a declared item.");
            if (!string.IsNullOrEmpty(fbx) && !paths.Contains(fbx))
                Error(report, "CHAR_AUTHORING_AUTHORITY_UNDECLARED", string.Empty, fbx, "Final FBX authority must reference a declared item.");
        }

        private static void ValidateKindPath(CharacterDccAuthoringValidationReport report, string characterId, CharacterAuthoringManifestItem item, string path)
        {
            string name = System.IO.Path.GetFileName(path);
            bool valid = item.Kind switch
            {
                CharacterAuthoringAssetKinds.BlenderWork => string.Equals(path, $"Blender_Work/{characterId}_work.blend", StringComparison.Ordinal),
                CharacterAuthoringAssetKinds.BlenderMilestone => path.StartsWith("Blender_Work/milestones/", StringComparison.Ordinal) && MilestoneName.IsMatch(name),
                CharacterAuthoringAssetKinds.BlenderAuthority => string.Equals(path, $"Final_Source/{characterId}_authoring.blend", StringComparison.Ordinal),
                CharacterAuthoringAssetKinds.FinalFbx => string.Equals(path, $"Final_Source/{characterId}_LOD0.fbx", StringComparison.Ordinal),
                CharacterAuthoringAssetKinds.ModelReference => path.StartsWith("references/", StringComparison.Ordinal),
                CharacterAuthoringAssetKinds.AiRaw => path.StartsWith("AI_Raw/", StringComparison.Ordinal),
                CharacterAuthoringAssetKinds.Provenance => path.StartsWith("AI_Raw/", StringComparison.Ordinal),
                CharacterAuthoringAssetKinds.Audit => path.StartsWith("Blender_Work/audit/", StringComparison.Ordinal),
                CharacterAuthoringAssetKinds.Texture => path.StartsWith("Final_Source/textures/", StringComparison.Ordinal),
                _ => true
            };
            if (!valid)
                Error(report, "CHAR_AUTHORING_KIND_PATH_MISMATCH", item.Id, path, "Item kind does not match the standard character source directory.");
        }

        private static bool IsSafeRelativePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && !System.IO.Path.IsPathRooted(path)
                && !path.StartsWith("/", StringComparison.Ordinal)
                && !path.Split('/').Any(segment => segment == ".." || segment.Length == 0);
        }

        private static string NormalizePath(string path) => (path ?? string.Empty).Trim().Replace('\\', '/');

        private static void Error(CharacterDccAuthoringValidationReport report, string code, string itemId, string path, string message)
        {
            report.Diagnostics.Add(new CharacterAuthoringValidationDiagnostic
            {
                Code = code,
                Severity = "Error",
                ItemId = itemId ?? string.Empty,
                Path = path ?? string.Empty,
                Message = message ?? string.Empty
            });
        }
    }
}

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    public enum MagicaCharacterClothSaveStatus
    {
        Saved,
        Conflict,
        Invalid,
        Failed,
    }

    public sealed class MagicaCharacterClothAuthoringLoadResult
    {
        public string PackagePath { get; internal set; } = string.Empty;
        public string DocumentPath { get; internal set; } = string.Empty;
        public string Hash { get; internal set; } = string.Empty;
        public MagicaCharacterClothAuthoringDocument Document { get; internal set; }
    }

    public sealed class MagicaCharacterClothAuthoringSaveResult
    {
        public MagicaCharacterClothSaveStatus Status { get; internal set; }
        public string ExpectedHash { get; internal set; } = string.Empty;
        public string CurrentHash { get; internal set; } = string.Empty;
        public string AcceptedHash { get; internal set; } = string.Empty;
        public string Message { get; internal set; } = string.Empty;
        public bool Succeeded => Status == MagicaCharacterClothSaveStatus.Saved;
    }

    public static class MagicaCharacterClothAuthoringRepository
    {
        public const string ConfigRelativePath = "config/character_cloth_magica.json";

        public static MagicaCharacterClothAuthoringLoadResult Load(string packagePath)
        {
            string packageRoot = ResolvePackageRoot(packagePath);
            string documentPath = ResolveDocumentPath(packageRoot);
            if (!File.Exists(documentPath))
                throw new FileNotFoundException("Character Cloth authoring document was not found.", documentPath);
            string json = File.ReadAllText(documentPath);
            return new MagicaCharacterClothAuthoringLoadResult
            {
                PackagePath = packageRoot,
                DocumentPath = documentPath,
                Hash = ComputeTextHash(json),
                Document = MagicaCharacterClothAuthoringSerializer.Deserialize(json),
            };
        }

        public static MagicaCharacterClothAuthoringSaveResult Save(
            string packagePath,
            MagicaCharacterClothAuthoringDocument document,
            string expectedHash)
        {
            var result = new MagicaCharacterClothAuthoringSaveResult
            {
                ExpectedHash = NormalizeHash(expectedHash),
            };
            try
            {
                string packageRoot = ResolvePackageRoot(packagePath);
                string path = ResolveDocumentPath(packageRoot);
                bool creatingDocument = !File.Exists(path);
                string currentText = creatingDocument ? string.Empty : File.ReadAllText(path);
                result.CurrentHash = creatingDocument ? string.Empty : ComputeTextHash(currentText);
                if (!string.Equals(result.ExpectedHash, result.CurrentHash, StringComparison.Ordinal))
                {
                    result.Status = MagicaCharacterClothSaveStatus.Conflict;
                    result.Message = "Character Cloth document changed on disk; reload before saving.";
                    return result;
                }

                MagicaCharacterClothAuthoringNormalization.Normalize(document);

                MagicaCharacterClothValidationResult validation =
                    MagicaCharacterClothAuthoringValidator.Validate(document, null);
                if (validation.HasErrors)
                {
                    result.Status = MagicaCharacterClothSaveStatus.Invalid;
                    result.Message = validation.Summary;
                    return result;
                }

                string json = MagicaCharacterClothAuthoringSerializer.Serialize(document);
                MagicaCharacterClothAuthoringDocument roundTrip =
                    MagicaCharacterClothAuthoringSerializer.Deserialize(json);
                MagicaCharacterClothValidationResult roundTripValidation =
                    MagicaCharacterClothAuthoringValidator.Validate(roundTrip, null);
                if (roundTripValidation.HasErrors)
                {
                    result.Status = MagicaCharacterClothSaveStatus.Invalid;
                    result.Message = "Serialized Character Cloth document failed validation: " + roundTripValidation.Summary;
                    return result;
                }

                if (creatingDocument) EnsureManifestDeclaresConfig(packageRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? packageRoot);
                string temporaryPath = path + ".tmp." + Guid.NewGuid().ToString("N");
                try
                {
                    File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                    if (File.Exists(path))
                        File.Replace(temporaryPath, path, null, true);
                    else
                        File.Move(temporaryPath, path);
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }

                string accepted = File.ReadAllText(path);
                result.AcceptedHash = ComputeTextHash(accepted);
                result.CurrentHash = result.AcceptedHash;
                result.Status = MagicaCharacterClothSaveStatus.Saved;
                result.Message = "Character Cloth authoring document saved.";
                return result;
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException ||
                exception is Newtonsoft.Json.JsonException || exception is ArgumentException)
            {
                result.Status = MagicaCharacterClothSaveStatus.Failed;
                result.Message = exception.Message;
                return result;
            }
        }

        public static string ComputeTextHash(string text)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes((text ?? string.Empty).Replace("\r\n", "\n"));
            byte[] hash = sha.ComputeHash(bytes);
            var builder = new StringBuilder("sha256:", 71);
            for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }

        public static string ComputeFileHash(string path) =>
            ComputeTextHash(File.ReadAllText(path));

        public static string NormalizeHash(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized.StartsWith("sha256:", StringComparison.Ordinal)
                ? normalized
                : string.IsNullOrEmpty(normalized) ? string.Empty : "sha256:" + normalized;
        }

        private static string ResolvePackageRoot(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentException("Character package path is required.", nameof(packagePath));
            string full = Path.GetFullPath(packagePath);
            if (!Directory.Exists(full))
                throw new DirectoryNotFoundException("Character package directory was not found: " + full);
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string ResolveDocumentPath(string packageRoot)
        {
            string path = Path.GetFullPath(Path.Combine(packageRoot, ConfigRelativePath));
            string prefix = packageRoot + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Character Cloth document path escapes the package root.");
            return path;
        }

        private static void EnsureManifestDeclaresConfig(string packageRoot)
        {
            string manifestPath = Path.Combine(packageRoot, "manifest.json");
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Character package manifest was not found.", manifestPath);

            JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
            JArray configFiles = manifest["configFiles"] as JArray;
            if (configFiles == null)
            {
                configFiles = new JArray();
                manifest["configFiles"] = configFiles;
            }

            for (int i = 0; i < configFiles.Count; i++)
            {
                string value = configFiles[i]?.Value<string>() ?? string.Empty;
                if (string.Equals(
                        value.Replace('\\', '/'),
                        ConfigRelativePath,
                        StringComparison.OrdinalIgnoreCase))
                    return;
            }

            configFiles.Add(ConfigRelativePath);
            string temporaryPath = manifestPath + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                string json = manifest.ToString(Formatting.Indented) + Environment.NewLine;
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                File.Replace(temporaryPath, manifestPath, null, true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}

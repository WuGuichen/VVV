using System.Security.Cryptography;
using System.Text.Json;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    // Verify file witnesses without importing Unity or duplicating the runtime frame decoder.
    internal static bool ValidateSocketBakeArtifacts(
        string projectRoot, string packageId, string indexPath, string indexHash,
        string binaryPath, string binaryHash, long binarySize, out string error)
    {
        error = "Socket Bake acceptance does not match its current index and binary payload.";
        const string expectedIndex = "generated/character_socket_bake_catalog.json";
        const string expectedBinary = "generated/character_socket_bake_catalog.bytes";
        if (string.IsNullOrWhiteSpace(packageId) || packageId is "." or ".."
            || packageId.IndexOfAny(new[] { '/', '\\' }) >= 0
            || indexPath != expectedIndex || binaryPath != expectedBinary
            || binarySize < 28 || binarySize > 256L * 1024 * 1024)
            return false;

        try
        {
            string root = Path.GetFullPath(Path.Combine(projectRoot, "Assets/MxFrameworkGenerated/CharacterPackages", packageId));
            string indexFile = Path.Combine(root, expectedIndex);
            string binaryFile = Path.Combine(root, expectedBinary);
            if (!File.Exists(indexFile) || !File.Exists(binaryFile)) return false;
            var indexInfo = new FileInfo(indexFile);
            if (indexInfo.Length == 0 || indexInfo.Length > 8192) return false;
            byte[] indexBytes = File.ReadAllBytes(indexFile);
            if (indexBytes.Length == 0 || indexBytes.Length > 8192
                || !HashEquals(indexHash, "sha256:" + Convert.ToHexString(SHA256.HashData(indexBytes)).ToLowerInvariant()))
                return false;
            using JsonDocument document = JsonDocument.Parse(indexBytes);
            JsonElement index = document.RootElement;
            if (index.GetProperty("format").GetString() != "mx.character.socketBakeCatalog.v3"
                || index.GetProperty("packageId").GetString() != packageId
                || index.GetProperty("binaryVersion").GetInt32() != 3
                || index.GetProperty("binaryPath").GetString() != expectedBinary
                || index.GetProperty("binarySize").GetInt64() != binarySize
                || !HashEquals(index.GetProperty("binaryHash").GetString(), binaryHash))
                return false;
            using var stream = new FileStream(binaryFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length != binarySize) return false;
            string actualHash = "sha256:" + Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (!HashEquals(binaryHash, actualHash)) return false;
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
            || ex is JsonException || ex is InvalidOperationException || ex is KeyNotFoundException
            || ex is FormatException || ex is OverflowException || ex is ArgumentException)
        {
            error += " " + ex.Message;
            return false;
        }
    }
}

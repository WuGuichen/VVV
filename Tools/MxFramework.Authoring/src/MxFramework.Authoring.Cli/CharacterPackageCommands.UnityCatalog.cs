using System.Text.Json;
using System.Text.Json.Nodes;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class CharacterPackageCommands
{
    // Only Unity-owned observations are copied. Compiler identity, hashes and source metadata remain current.
    private static readonly string[] UnityCatalogObservationFields =
    {
        "unityAssetGuid", "unityMainObjectType", "importerKind", "importStatus", "gltfAnimationMethod",
        "avatarSourceStatus", "avatarSourceGuid", "avatarSourceHumanoidStatus", "avatarSourceRigStatus",
        "avatarSourceMessage", "diagnostics", "diagnosticCount", "diagnosticCodes",
        "unitySnapshotAssetHash", "unitySnapshotMetaHash"
    };

    /// <remarks>AllocByDesign: preserves verified Editor observations only while their import inputs and disk witnesses match.</remarks>
    internal static string PreserveUnityCatalogEnrichment(
        string freshJson, string catalogPath, string projectRoot, JsonSerializerOptions options)
    {
        if (!File.Exists(catalogPath)) return freshJson;
        JsonObject previous;
        try { previous = JsonNode.Parse(File.ReadAllText(catalogPath)) as JsonObject; }
        catch (JsonException) { return freshJson; }
        var current = JsonNode.Parse(freshJson) as JsonObject;
        if (previous == null || current == null ||
            CatalogText(previous, "format") != "mx.characterUnityResourceCatalog.v1" ||
            CatalogText(previous, "packageId") != CatalogText(current, "packageId") ||
            previous["entries"] is not JsonArray oldEntries || current["entries"] is not JsonArray newEntries)
            return freshJson;

        var byKey = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonObject old in oldEntries.OfType<JsonObject>())
        {
            string key = CatalogText(old, "packageResourceKey");
            if (string.IsNullOrEmpty(key)) continue;
            if (!byKey.TryAdd(key, old)) duplicates.Add(key);
        }

        bool changed = false;
        foreach (JsonObject entry in newEntries.OfType<JsonObject>())
        {
            string key = CatalogText(entry, "packageResourceKey");
            if (duplicates.Contains(key) || !byKey.TryGetValue(key, out JsonObject old) ||
                !CanPreserveUnityObservation(entry, old, projectRoot)) continue;
            CopyUnityObservations(old, entry);
            CopyUnityObservations((JsonObject)old["providerData"], (JsonObject)entry["providerData"]);
            changed = true;
        }

        return changed ? EnsureTrailingNewline(current.ToJsonString(options)) : freshJson;
    }

    private static bool CanPreserveUnityObservation(JsonObject current, JsonObject previous, string projectRoot)
    {
        if (current["providerData"] is not JsonObject data || previous["providerData"] is not JsonObject oldData)
            return false;
        string status = CatalogText(previous, "importStatus");
        if (status != "Imported" && status != "Extracted" && status != "Placeholder") return false;
        if (string.IsNullOrWhiteSpace(CatalogText(previous, "unityAssetGuid")) ||
            string.IsNullOrWhiteSpace(CatalogText(previous, "unityMainObjectType"))) return false;
        foreach (string field in new[] { "id", "stableId", "type", "sourceRelativePath", "sourceFormat", "sourceHash",
                     "artifactHash", "importHash", "dependencyHash", "rigType", "avatarSourceRelativePath", "avatarSourceContentHash" })
            if (!string.Equals(CatalogText(current, field), CatalogText(previous, field), StringComparison.Ordinal)) return false;
        if (string.IsNullOrWhiteSpace(CatalogText(current, "importHash"))) return false;

        string relative = CatalogText(current, "unityAssetPath").Replace('\\', '/');
        if (!string.Equals(relative, CatalogText(previous, "unityAssetPath").Replace('\\', '/'), StringComparison.Ordinal)) return false;
        // Extracted clips / external Avatars require their own Editor refresh; do not guess their dependency state.
        string avatarPath = CatalogText(previous, "avatarSourceAssetPath").Replace('\\', '/');
        if (!string.IsNullOrEmpty(avatarPath) && !string.Equals(avatarPath, relative, StringComparison.Ordinal)) return false;
        if (Path.IsPathRooted(relative) || !relative.StartsWith("Assets/", StringComparison.Ordinal)) return false;
        string asset = Path.GetFullPath(Path.Combine(projectRoot, relative));
        string assetsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Assets"));
        if (!IsSameOrDescendantPath(asset, assetsRoot)) return false;
        try
        {
            return File.Exists(asset) && File.Exists(asset + ".meta") &&
                string.Equals(CatalogText(oldData, "unitySnapshotAssetHash"), CharacterPackageHashUtility.ComputeFileSha256(asset), StringComparison.Ordinal) &&
                string.Equals(CatalogText(oldData, "unitySnapshotMetaHash"), CharacterPackageHashUtility.ComputeFileSha256(asset + ".meta"), StringComparison.Ordinal);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static string CatalogText(JsonObject value, string key) =>
        value?[key] is JsonValue token && token.TryGetValue<string>(out string text) ? text : string.Empty;

    private static void CopyUnityObservations(JsonObject source, JsonObject target)
    {
        foreach (string field in UnityCatalogObservationFields)
            if (source.TryGetPropertyValue(field, out JsonNode value)) target[field] = value?.DeepClone();
    }
}

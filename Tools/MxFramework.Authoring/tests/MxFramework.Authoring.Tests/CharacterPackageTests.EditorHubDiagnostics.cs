using System.Text.Json.Nodes;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    internal static void RunEditorHubDiagnostics()
    {
        AuthoringResourceReferenceGraph_ScansCrossConsumerReferencesAndDiagnostics();
        ReferenceGraph_PreviewKeysAreOptionalButBodyRemainsRequired();
        UnityCatalog_PreservesOnlyUnchangedVerifiedEditorObservations();
        EditorServer_CharacterCreate_CreatesArtSourceAndRejectsDuplicatePackageId();
        EditorServer_NumericCharacterIds_AllocateScaffoldAndRetire();
    }

    private static void ReferenceGraph_PreviewKeysAreOptionalButBodyRemainsRequired()
    {
        var package = new CharacterResourcePackage();
        package.Manifest.PackageId = "test";
        package.ApplicationConfig.CharacterStableId = "char.test";
        foreach (var item in new[] {
            ("body", CharacterPackageResourceTypeIds.Model, CharacterPackageResourceUsageIds.CharacterModel),
            ("thumbnail", CharacterPackageResourceTypeIds.Preview, CharacterPackageResourceUsageIds.PreviewThumbnail),
            ("preview", CharacterPackageResourceTypeIds.Model, CharacterPackageResourceUsageIds.PreviewMesh) })
        {
            string key = "char.test." + item.Item1;
            package.ApplicationConfig.ResourceKeys.Add(key);
            package.ResourceCatalog.Entries.Add(new CharacterPackageResourceEntry {
                ResourceKey = key, StableId = key, LocalId = item.Item1, PackageId = "test",
                TypeId = item.Item2, Usage = item.Item3, RelativePath = item.Item1 + ".glb"
            });
        }
        var collection = CharacterPackageAuthoringResourceProvider.FromPackageResourceCatalog(
            package.ResourceCatalog, new AuthoringResourceProviderContext { PackageId = "test" });
        foreach (var item in collection.Items) item.RuntimeAvailability = AuthoringResourceRuntimeAvailability.PreviewOnly;
        var graph = AuthoringResourceReferenceGraphBuilder.FromCharacterPackage(package, collection);
        foreach (string key in new[] { "char.test.thumbnail", "char.test.preview" })
        {
            Require(graph.Edges.Exists(e => e.TargetProviderResourceKey == key && !e.IsRequiredAtRuntime && e.PreloadPolicy == AuthoringResourcePreloadPolicies.UiDeferred), "preview reference must remain visible but optional: " + key);
            Require(!graph.Diagnostics.Exists(d => d.ResourceStableId == key && d.Code == AuthoringResourceDiagnosticCodes.NotRuntimeLoadable), "preview must not cause a runtime-required error.");
        }
        Require(graph.Diagnostics.Exists(d => d.ResourceStableId == "char.test.body" && d.Code == AuthoringResourceDiagnosticCodes.NotRuntimeLoadable), "a preview-only body model must still fail its runtime-required check.");
    }

    private static void UnityCatalog_PreservesOnlyUnchangedVerifiedEditorObservations()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-unity-observations-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        string asset = Path.Combine(root, "Assets", "body.fbx");
        string path = Path.Combine(root, "catalog.json");
        try
        {
            File.WriteAllText(asset, "fixture mesh");
            File.WriteAllText(asset + ".meta", "guid: 01234567890123456789012345678901\n");
            var fresh = JsonNode.Parse("""
                {"format":"mx.characterUnityResourceCatalog.v1","packageId":"000008","entries":[
                  {"id":"body","packageResourceKey":"body","stableId":"body","type":"GameObject","sourceFormat":"fbx",
                   "unityAssetPath":"Assets/body.fbx","importHash":"import-A","artifactHash":"artifact-A",
                   "unityAssetGuid":"","unityMainObjectType":"","importStatus":"PendingUnityImport",
                   "providerData":{"sourcePackageHash":"fresh-source","unityAssetGuid":"","importStatus":"PendingUnityImport"}}]}
                """);
            var previous = fresh.DeepClone();
            JsonObject old = (JsonObject)previous["entries"][0];
            old["unityAssetGuid"] = "01234567890123456789012345678901";
            old["unityMainObjectType"] = "GameObject";
            old["importStatus"] = "Imported";
            old["providerData"]["unityAssetGuid"] = old["unityAssetGuid"].DeepClone();
            old["providerData"]["importStatus"] = "Imported";
            old["providerData"]["sourcePackageHash"] = "old-source";
            old["providerData"]["unitySnapshotAssetHash"] = CharacterPackageHashUtility.ComputeFileSha256(asset);
            old["providerData"]["unitySnapshotMetaHash"] = CharacterPackageHashUtility.ComputeFileSha256(asset + ".meta");
            File.WriteAllText(path, previous.ToJsonString());
            string input = fresh.ToJsonString();
            JsonNode merged = JsonNode.Parse(CharacterPackageCommands.PreserveUnityCatalogEnrichment(input, path, root, JsonOptions));
            Require((string)merged["entries"][0]["importStatus"] == "Imported", "unchanged Editor snapshot should survive CLI import.");
            Require((string)merged["entries"][0]["providerData"]["sourcePackageHash"] == "fresh-source", "compiler-owned metadata must remain current.");
            Require((string)fresh["entries"][0]["importStatus"] == "PendingUnityImport", "merge must not mutate its source.");

            void RequireInvalidated(string message)
            {
                JsonNode value = JsonNode.Parse(CharacterPackageCommands.PreserveUnityCatalogEnrichment(fresh.ToJsonString(), path, root, JsonOptions));
                Require((string)value["entries"][0]["importStatus"] == "PendingUnityImport", message);
            }
            fresh["entries"][0]["importHash"] = "import-B";
            RequireInvalidated("changed import settings invalidate the observation.");
            fresh["entries"][0]["importHash"] = "import-A";
            File.AppendAllText(asset, "changed");
            RequireInvalidated("changed asset bytes invalidate the observation.");
            File.WriteAllText(asset, "fixture mesh");
            File.AppendAllText(asset + ".meta", "changed importer settings");
            RequireInvalidated("changed metadata invalidates the observation, even with the same GUID.");
            File.WriteAllText(asset + ".meta", "guid: 01234567890123456789012345678901\n");
            old["providerData"].AsObject().Remove("unitySnapshotAssetHash");
            File.WriteAllText(path, previous.ToJsonString());
            RequireInvalidated("legacy observations without a disk witness require an Editor refresh.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}

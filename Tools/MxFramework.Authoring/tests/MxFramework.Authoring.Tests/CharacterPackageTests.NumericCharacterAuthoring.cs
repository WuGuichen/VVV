using System;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    private static void EditorServer_NumericCharacterIds_AllocateScaffoldAndRetire()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-numeric-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateCharacterTemplateFixture(tempRoot, corruptResourceCatalog: false);
            MxFramework.Authoring.Cli.EditorServer.RemoveLibrariesFromCharacterActionIdCatalogForTest(tempRoot, new[] { "000001", "000002", "000003" });
            string catalogPath = Path.Combine(tempRoot, "Authoring", "CharacterPackages", "character_id_catalog.json");
            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
            File.WriteAllText(catalogPath, "{\"schemaVersion\":\"1.0\",\"width\":6,\"minimumId\":1,\"maximumId\":999999,\"nextCandidate\":1,\"revision\":1,\"entries\":[],\"retiredIds\":[]}");

            object created = MxFramework.Authoring.Cli.EditorServer.CreateCharacterPackage(
                tempRoot,
                new MxFramework.Authoring.Cli.EditorServer.CharacterStudioCreateRequest
                {
                    displayName = "Numeric Slime",
                    template = "creature",
                    idCatalogRevision = 1
                },
                JsonOptions);

            string allocated = GetAnonymousPropertyForTest(created, "allocatedCharacterId");
            string packageRelative = GetAnonymousPropertyForTest(created, "packageRelative");
            string artSourceRelative = GetAnonymousPropertyForTest(created, "artSourceRelative");
            Require(allocated == "000001", "first numeric character should preserve a six-digit ID.");
            Require(packageRelative == "Authoring/CharacterPackages/character-000001", "numeric package directory should use the allocated ID.");
            Require(artSourceRelative == "ArtSource/Characters/000001", "numeric ArtSource directory should use the allocated ID.");

            string sourceRoot = Path.Combine(tempRoot, "ArtSource", "Characters", "000001");
            CharacterResourcePackage numericPackage = CharacterPackageCommands.ReadPackage(Path.Combine(tempRoot, packageRelative.Replace('/', Path.DirectorySeparatorChar)), JsonOptions);
            Require(numericPackage.Manifest.PackageId == "000001" && numericPackage.Manifest.StableId == "charpkg.000001", "numeric package identity should flow into the stable package ID.");
            Require(numericPackage.ResourceCatalog.Entries.Exists(item => item.ResourceKey.StartsWith("char.000001.", StringComparison.Ordinal)), "numeric package resources should use char.000001.* keys.");
            Require(File.Exists(Path.Combine(sourceRoot, "authoring_manifest.json")), "numeric character should receive an optional authoring manifest.");
            Require(File.Exists(Path.Combine(sourceRoot, "README.md")), "numeric character should receive an ArtSource README.");
            CharacterSourceManifestDocument source = JsonSerializer.Deserialize<CharacterSourceManifestDocument>(File.ReadAllText(Path.Combine(sourceRoot, "character_sources.json")), JsonOptions);
            Require(source != null && source.CharacterId == "000001" && source.PackageId == "000001", "numeric source manifest identity should preserve leading zeroes.");
            Require(source.Items[0].Source.Path == "Final_Source/000001_LOD0.glb", "copied model source should move to the numeric Final_Source path.");
            CharacterAuthoringManifestDocument authoring = JsonSerializer.Deserialize<CharacterAuthoringManifestDocument>(File.ReadAllText(Path.Combine(sourceRoot, "authoring_manifest.json")), JsonOptions);
            Require(authoring != null && authoring.CharacterId == "000001" && authoring.Items.Count == 0, "optional DCC manifest should be valid without declared optional files.");

            string workPath = Path.Combine(sourceRoot, "Blender_Work", "000001_work.blend");
            Directory.CreateDirectory(Path.GetDirectoryName(workPath)!);
            File.WriteAllBytes(workPath, new byte[] { 7, 8, 9 });
            object adopted = MxFramework.Authoring.Cli.EditorServer.ApplyCharacterAuthoringSource(
                tempRoot,
                new MxFramework.Authoring.Cli.EditorServer.CharacterAuthoringSourceApplyRequest
                {
                    package = packageRelative,
                    expectedRevision = authoring.Revision,
                    action = "adopt",
                    id = "blender-work",
                    kind = CharacterAuthoringAssetKinds.BlenderWork,
                    role = "active-work",
                    path = "Blender_Work/000001_work.blend"
                },
                JsonOptions);
            Require(GetAnonymousPropertyForTest(adopted, "success") == "True", "optional Blender work source should be adopted through the guarded API.");
            object authoringState = MxFramework.Authoring.Cli.EditorServer.ReadCharacterAuthoringSourceState(tempRoot, packageRelative, JsonOptions);
            Require(GetAnonymousPropertyForTest(authoringState, "status") == "Current", "adopted optional source should report Current.");

            using (JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(catalogPath)))
            {
                Require(catalog.RootElement.GetProperty("nextCandidate").GetInt32() == 2, "catalog should advance after a committed create.");
                Require(catalog.RootElement.GetProperty("entries")[0].GetProperty("id").GetString() == "000001", "catalog should record the active ID.");
            }

            object duplicated = MxFramework.Authoring.Cli.EditorServer.DuplicateCharacterPackage(
                tempRoot,
                new MxFramework.Authoring.Cli.EditorServer.CharacterStudioDuplicateRequest
                {
                    sourcePackage = packageRelative,
                    displayName = "Numeric Slime Copy",
                    idCatalogRevision = 2
                },
                JsonOptions);
            Require(GetAnonymousPropertyForTest(duplicated, "allocatedCharacterId") == "000002", "duplicate should allocate a new numeric ID from the pool.");
            string duplicatedSourceRoot = Path.Combine(tempRoot, "ArtSource", "Characters", "000002");
            CharacterAuthoringManifestDocument duplicatedAuthoring = JsonSerializer.Deserialize<CharacterAuthoringManifestDocument>(File.ReadAllText(Path.Combine(duplicatedSourceRoot, "authoring_manifest.json")), JsonOptions);
            Require(duplicatedAuthoring != null && duplicatedAuthoring.DerivedFromCharacterId == "000001", "duplicated optional source manifest should record its source identity.");
            Require(duplicatedAuthoring.Items.Exists(item => item.Path == "Blender_Work/000002_work.blend"), "numeric duplicate should rewrite managed optional source filenames.");
            Require(File.Exists(Path.Combine(duplicatedSourceRoot, "Blender_Work", "000002_work.blend")), "numeric duplicate should rename the copied Blender work source.");

            object reallocated = MxFramework.Authoring.Cli.EditorServer.ReallocateDraftCharacterId(
                tempRoot,
                new MxFramework.Authoring.Cli.EditorServer.CharacterIdReallocateRequest
                {
                    sourcePackage = GetAnonymousPropertyForTest(duplicated, "packageRelative"),
                    confirmCharacterId = "000002",
                    idCatalogRevision = 3
                },
                JsonOptions);
            Require(GetAnonymousPropertyForTest(reallocated, "allocatedCharacterId") == "000003", "draft reallocation should acquire the next pool ID.");
            Require(Directory.Exists(Path.Combine(tempRoot, "ArtSource", "Characters", "000003")), "reallocation should leave the replacement ArtSource active.");
            Require(Directory.Exists(Path.Combine(tempRoot, "ArtSource", "Archive", "Characters", "000002")), "reallocation should archive and retire the previous numeric source.");

            MxFramework.Authoring.Cli.EditorServer.ArchiveCharacterPackage(
                tempRoot,
                new MxFramework.Authoring.Cli.EditorServer.CharacterStudioArchiveRequest
                {
                    package = packageRelative,
                    confirmPackageId = "000001"
                },
                JsonOptions);
            using JsonDocument archivedCatalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
            Require(archivedCatalog.RootElement.GetProperty("entries")[0].GetProperty("status").GetString() == "archived", "archived numeric ID should retain its catalog entry.");
            Require(archivedCatalog.RootElement.GetProperty("retiredIds")[0].GetString() == "000001", "archived numeric ID should be permanently retired.");
            Require(Directory.Exists(Path.Combine(tempRoot, "ArtSource", "Archive", "Characters", "000001")), "numeric archive should keep the stable folder without a timestamp.");
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void CharacterAuthoringManifest_OptionalAssetsValidateWhenDeclared()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-character-authoring-manifest-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var empty = new CharacterAuthoringManifestDocument { CharacterId = "000123", PackageId = "000123", DisplayName = "Optional Sources" };
            CharacterDccAuthoringValidationReport emptyReport = CharacterAuthoringManifestValidator.Validate(root, empty, "000123");
            Require(!emptyReport.HasBlockingIssues, "empty optional authoring manifest should be valid.");

            string workRelative = "Blender_Work/000123_work.blend";
            string workPath = Path.Combine(root, workRelative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(workPath)!);
            File.WriteAllBytes(workPath, new byte[] { 1, 2, 3, 4 });
            empty.Items.Add(new CharacterAuthoringManifestItem
            {
                Id = "work",
                Kind = CharacterAuthoringAssetKinds.BlenderWork,
                Role = "active-work",
                Path = workRelative,
                Sha256 = CharacterAuthoringManifestValidator.ComputeSha256(workPath)
            });
            CharacterDccAuthoringValidationReport current = CharacterAuthoringManifestValidator.Validate(root, empty, "000123");
            Require(!current.HasBlockingIssues, "declared optional Blender source with a current hash should validate.");
            File.AppendAllText(workPath, "changed");
            CharacterDccAuthoringValidationReport stale = CharacterAuthoringManifestValidator.Validate(root, empty, "000123");
            Require(stale.Diagnostics.Exists(item => item.Code == "CHAR_AUTHORING_HASH_STALE"), "changed optional source should invalidate its declared hash.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

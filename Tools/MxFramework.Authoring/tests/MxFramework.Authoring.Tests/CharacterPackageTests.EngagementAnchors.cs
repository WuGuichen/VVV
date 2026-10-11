using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    internal static void RunCharacterEngagementAnchors()
    {
        EngagementAnchors_MissingDocumentPreservesPriorCompilerContract();
        EngagementAnchors_CurrentDocumentRoundTripsAndCompilesDeterministically();
        EngagementAnchors_InvalidFieldsProduceTypedDiagnostics();
        EngagementAnchors_UnknownSchemaIsLosslessAndWriteBlocked();
        EngagementAnchors_MissingDocumentIsNotCreatedByPackageSave();
    }

    private static void EngagementAnchors_MissingDocumentPreservesPriorCompilerContract()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        string hitZone = CollectHitZones(package)[0];
        package.Geometry.DamageZonesDocumentState = CharacterDamageZonesDocumentState.Current;
        package.Geometry.DamageZones = new CharacterDamageZonesDocument
        {
            Zones = new List<CharacterDamageZoneAuthoring>
            {
                Zone("zone.approach", hitZone, 3, true, true, true)
            }
        };

        CharacterAuthoringCompileResult result = Compile(package);
        Require(package.Geometry.EngagementAnchorsDocumentState
                == CharacterEngagementAnchorsDocumentState.Missing,
            "legacy package should preserve the missing EngagementAnchor document state.");
        Require(result.GeometryBinding.Format == CharacterAuthoringCompilerFormats.GeometryBindingV2,
            "missing EngagementAnchor document must preserve the prior v2 binding contract.");
        Require(result.GeometryBinding.EngagementAnchors == null
                && result.GeometryBinding.EngagementAnchorRevision == 0
                && result.GeometryBinding.EngagementAnchorContentHash == null,
            "missing EngagementAnchor document must not synthesize runtime anchors or metadata.");
        string json = JsonSerializer.Serialize(
            result.GeometryBinding,
            MxFramework.Authoring.Cli.Program.CreateJsonOptions());
        Require(!json.Contains("engagementAnchor", StringComparison.Ordinal),
            "prior geometry binding bytes must not gain empty EngagementAnchor properties.");
    }

    private static void EngagementAnchors_CurrentDocumentRoundTripsAndCompilesDeterministically()
    {
        CharacterResourcePackage package = CreatePackageWithApproachableZone();
        package.Geometry.EngagementAnchorsDocumentState = CharacterEngagementAnchorsDocumentState.Current;
        package.Geometry.EngagementAnchors = new CharacterEngagementAnchorsDocument
        {
            Revision = 7,
            Anchors = new List<CharacterEngagementAnchorAuthoring>
            {
                Anchor("anchor.zeta", "zone.approach", 2500, 0, -1000, 180000, 2, 3, "phase.default"),
                Anchor("anchor.alpha", "zone.approach", -1500, 25, 2000, 90000, 1, 9, string.Empty)
            }
        };

        CharacterAuthoringCompileResult first = Compile(package);
        Require(first.GeometryBinding.Format == CharacterAuthoringCompilerFormats.GeometryBindingV3
                && first.GeometryBinding.SchemaVersion == "3.0",
            "current EngagementAnchor document should emit geometry binding v3.");
        Require(first.GeometryBinding.EngagementAnchorRevision == 7
                && first.GeometryBinding.EngagementAnchorContentHash.StartsWith("sha256:", StringComparison.Ordinal)
                && first.GeometryBinding.EngagementAnchors.Count == 2,
            "v3 binding should carry revision, independent content hash, and immutable anchors.");
        Require(first.GeometryBinding.EngagementAnchors[0].AnchorLabel == "anchor.alpha"
                && first.GeometryBinding.EngagementAnchors[1].AnchorLabel == "anchor.zeta",
            "compiler should project EngagementAnchors in stable AnchorId order.");

        package.Geometry.EngagementAnchors.Anchors.Reverse();
        CharacterAuthoringCompileResult second = Compile(package);
        Require(first.GeometryBinding.EngagementAnchorContentHash
                    == second.GeometryBinding.EngagementAnchorContentHash
                && first.Hashes.GeometryBindingHash == second.Hashes.GeometryBindingHash
                && first.Hashes.SourcePackageHash == second.Hashes.SourcePackageHash,
            "input order must not affect canonical EngagementAnchor hashes.");

        JsonSerializerOptions options = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        string json = JsonSerializer.Serialize(package.Geometry.EngagementAnchors, options);
        CharacterEngagementAnchorsDocument roundTrip =
            JsonSerializer.Deserialize<CharacterEngagementAnchorsDocument>(json, options);
        Require(roundTrip != null
                && roundTrip.Format == CharacterEngagementAnchorsDocument.CurrentFormat
                && roundTrip.SchemaVersion == CharacterEngagementAnchorsDocument.CurrentSchemaVersion
                && roundTrip.Revision == 7
                && roundTrip.Anchors.Count == 2
                && roundTrip.Anchors[0].ReferenceFrame
                    == CharacterEngagementAnchorReferenceFrame.ModelRoot,
            "current EngagementAnchor schema should round-trip without migration.");
    }

    private static void EngagementAnchors_InvalidFieldsProduceTypedDiagnostics()
    {
        CharacterResourcePackage package = CreatePackageWithApproachableZone();
        package.Geometry.EngagementAnchorsDocumentState = CharacterEngagementAnchorsDocumentState.Current;
        package.Geometry.EngagementAnchors = new CharacterEngagementAnchorsDocument
        {
            Revision = 0,
            Anchors = new List<CharacterEngagementAnchorAuthoring>
            {
                Anchor("anchor.same", "zone.missing", 10000001, 0, 0, 360000, 0, 100001, "Phase Bad"),
                Anchor("anchor.same", "zone.approach", 0, 0, 0, 0, 1, 0, string.Empty)
            }
        };
        package.Geometry.EngagementAnchors.Anchors[0].ReferenceFrame =
            CharacterEngagementAnchorReferenceFrame.World;

        CharacterAuthoringCompileResult result = Compile(package);
        string[] expected =
        {
            CharacterAuthoringValidationCodes.EngagementAnchorInvalidRevision,
            CharacterAuthoringValidationCodes.EngagementAnchorDuplicateId,
            CharacterAuthoringValidationCodes.EngagementAnchorUnknownZone,
            CharacterAuthoringValidationCodes.EngagementAnchorInvalidReferenceFrame,
            CharacterAuthoringValidationCodes.EngagementAnchorInvalidPosition,
            CharacterAuthoringValidationCodes.EngagementAnchorInvalidYaw,
            CharacterAuthoringValidationCodes.EngagementAnchorInvalidCapacity,
            CharacterAuthoringValidationCodes.EngagementAnchorInvalidPriority,
            CharacterAuthoringValidationCodes.EngagementAnchorInvalidPhaseId
        };
        for (int i = 0; i < expected.Length; i++)
        {
            Require(HasCompilerIssue(result, expected[i], CharacterAuthoringValidationGate.ExportBlocked),
                "invalid EngagementAnchor should report typed diagnostic " + expected[i] + ".");
        }
        Require(result.GateReport.ExportBlocked,
            "invalid EngagementAnchor document must block compiler writes.");
    }

    private static void EngagementAnchors_UnknownSchemaIsLosslessAndWriteBlocked()
    {
        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "mx-character-engagement-anchor-unknown-" + Guid.NewGuid().ToString("N"));
        string packagePath = Path.Combine(tempRoot, "CharacterSource", "character-codex-flow-test");
        try
        {
            CopyDirectory(FindSamplePath("character-codex-flow-test"), packagePath);
            string anchorPath = Path.Combine(packagePath, "geometry", "engagement_anchors.json");
            const string payload = "{\n  \"format\": \"mx.characterEngagementAnchors.v9\",\n  \"schemaVersion\": \"9.7\",\n  \"future\": { \"keep\": true }\n}\n";
            File.WriteAllText(anchorPath, payload);
            JsonSerializerOptions options = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, options);
            Require(package.Geometry.EngagementAnchorsDocumentState
                        == CharacterEngagementAnchorsDocumentState.UnsupportedSchema
                    && !package.Geometry.EngagementAnchorsWriteEligible,
                "unknown EngagementAnchor schema should be read-only.");
            Require(package.Geometry.EngagementAnchorsRawPayload == payload,
                "unknown EngagementAnchor payload must be retained byte-for-byte.");
            CharacterAuthoringCompileResult result = CharacterAuthoringCompiler.Compile(
                new CharacterAuthoringCompileRequest { Package = package, PackageRootPath = packagePath });
            Require(HasCompilerIssue(
                    result,
                    CharacterAuthoringValidationCodes.EngagementAnchorUnsupportedSchema,
                    CharacterAuthoringValidationGate.ExportBlocked),
                "unknown EngagementAnchor schema should use CHARANCH002 and block export.");
            try
            {
                EditorServer.SaveCharacterStudioPackage(
                    tempRoot,
                    "CharacterSource/character-codex-flow-test",
                    package,
                    options);
                Require(false, "unknown EngagementAnchor schema must reject package save.");
            }
            catch (InvalidOperationException)
            {
                Require(File.ReadAllText(anchorPath) == payload,
                    "rejected save must not overwrite the unknown EngagementAnchor payload.");
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    private static void EngagementAnchors_MissingDocumentIsNotCreatedByPackageSave()
    {
        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "mx-character-engagement-anchor-missing-" + Guid.NewGuid().ToString("N"));
        string packagePath = Path.Combine(tempRoot, "CharacterSource", "character-codex-flow-test");
        try
        {
            CopyDirectory(FindSamplePath("character-codex-flow-test"), packagePath);
            CopyDirectory(Path.Combine(FindRepoRoot(), "WeaponSource"), Path.Combine(tempRoot, "WeaponSource"));
            CopyDirectory(
                Path.Combine(FindRepoRoot(), "CharacterActionSource"),
                Path.Combine(tempRoot, "CharacterActionSource"));
            JsonSerializerOptions options = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, options);
            EditorServer.SaveCharacterStudioPackage(
                tempRoot,
                "CharacterSource/character-codex-flow-test",
                package,
                options);
            Require(!File.Exists(Path.Combine(packagePath, "geometry", "engagement_anchors.json")),
                "saving an unrelated legacy package must not create engagement_anchors.json.");
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    private static CharacterResourcePackage CreatePackageWithApproachableZone()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        string hitZone = CollectHitZones(package)[0];
        package.Geometry.DamageZonesDocumentState = CharacterDamageZonesDocumentState.Current;
        package.Geometry.DamageZones = new CharacterDamageZonesDocument
        {
            Zones = new List<CharacterDamageZoneAuthoring>
            {
                Zone("zone.approach", hitZone, 3, true, true, true)
            }
        };
        return package;
    }

    private static CharacterEngagementAnchorAuthoring Anchor(
        string anchorId,
        string zoneId,
        int x,
        int y,
        int z,
        int yaw,
        int capacity,
        int priority,
        string phaseId)
    {
        return new CharacterEngagementAnchorAuthoring
        {
            AnchorId = anchorId,
            ZoneId = zoneId,
            ReferenceFrame = CharacterEngagementAnchorReferenceFrame.ModelRoot,
            LocalPositionMillimeters = new CharacterAuthoringIntVector3(x, y, z),
            YawMilliDegrees = yaw,
            Capacity = capacity,
            AuthoredPriority = priority,
            Enabled = true,
            PhaseId = phaseId
        };
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    internal static void RunCharacterDamageZones()
    {
        DamageZones_MissingDocumentPreservesV1CompilerContract();
        DamageZones_CurrentDocumentRoundTripsAndCompilesDeterministically();
        DamageZones_InvalidMembershipProducesTypedDiagnostics();
        DamageZones_UnknownSchemaIsLosslessAndWriteBlocked();
        DamageZones_MissingDocumentIsNotCreatedByPackageSave();
    }

    private static void DamageZones_MissingDocumentPreservesV1CompilerContract()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        Require(package.Geometry.DamageZonesDocumentState == CharacterDamageZonesDocumentState.Missing,
            "legacy package should preserve the missing DamageZone document state.");
        CharacterAuthoringCompileResult result = Compile(package);
        Require(result.GeometryBinding.Format == CharacterAuthoringCompilerFormats.GeometryBinding,
            "missing DamageZone document must keep the v1 geometry binding format.");
        Require(result.GeometryBinding.SchemaVersion == "1.0" && result.GeometryBinding.DamageZones == null,
            "missing DamageZone document must not synthesize a runtime Zone.");
        string json = JsonSerializer.Serialize(result.GeometryBinding, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
        Require(!json.Contains("damageZones", StringComparison.Ordinal),
            "v1 geometry binding bytes must not gain an empty DamageZones property.");
    }

    private static void DamageZones_CurrentDocumentRoundTripsAndCompilesDeterministically()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        List<string> hitZones = CollectHitZones(package);
        Require(hitZones.Count >= 2, "DamageZone fixture requires two HitZone identities.");
        package.Geometry.DamageZonesDocumentState = CharacterDamageZonesDocumentState.Current;
        package.Geometry.DamageZones = new CharacterDamageZonesDocument
        {
            Zones = new List<CharacterDamageZoneAuthoring>
            {
                Zone("zone.zeta", hitZones[1], priority: 2, hittable: true, aimable: false, approachable: true),
                Zone("zone.alpha", hitZones[0], priority: 7, hittable: false, aimable: true, approachable: false)
            }
        };

        CharacterAuthoringCompileResult first = Compile(package);
        Require(first.GeometryBinding.Format == CharacterAuthoringCompilerFormats.GeometryBindingV2,
            "explicit DamageZone document should emit the v2 geometry binding.");
        Require(first.GeometryBinding.DamageZones.Count == 2
            && first.GeometryBinding.DamageZones[0].ZoneLabel == "zone.alpha"
            && first.GeometryBinding.DamageZones[1].ZoneLabel == "zone.zeta",
            "compiler should project DamageZones in stable ZoneId order.");
        Require(first.GeometryBinding.DamageZones[0].Aimable
            && !first.GeometryBinding.DamageZones[0].Hittable
            && !first.GeometryBinding.DamageZones[0].Approachable,
            "DamageZone attributes must remain orthogonal in the binding.");

        package.Geometry.DamageZones.Zones.Reverse();
        CharacterAuthoringCompileResult second = Compile(package);
        Require(first.Hashes.GeometryBindingHash == second.Hashes.GeometryBindingHash
            && first.Hashes.SourcePackageHash == second.Hashes.SourcePackageHash,
            "input list order must not affect canonical DamageZone hashes.");

        JsonSerializerOptions options = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        string json = JsonSerializer.Serialize(package.Geometry.DamageZones, options);
        CharacterDamageZonesDocument roundTrip = JsonSerializer.Deserialize<CharacterDamageZonesDocument>(json, options);
        Require(roundTrip != null && roundTrip.Format == CharacterDamageZonesDocument.CurrentFormat
            && roundTrip.SchemaVersion == CharacterDamageZonesDocument.CurrentSchemaVersion
            && roundTrip.Zones.Count == 2,
            "current DamageZone schema should round-trip without migration.");
    }

    private static void DamageZones_InvalidMembershipProducesTypedDiagnostics()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        string hitZone = CollectHitZones(package)[0];
        package.Geometry.DamageZonesDocumentState = CharacterDamageZonesDocumentState.Current;
        package.Geometry.DamageZones = new CharacterDamageZonesDocument
        {
            Zones = new List<CharacterDamageZoneAuthoring>
            {
                Zone("zone.a", hitZone, 0, true, true, false),
                Zone("zone.b", hitZone, 100001, true, false, false),
                Zone("zone.a", "hit.unknown.damage-zone", 0, false, true, false)
            }
        };
        CharacterAuthoringCompileResult result = Compile(package);
        Require(HasCompilerIssue(result, CharacterAuthoringValidationCodes.DamageZoneDuplicateHitZoneMembership,
                CharacterAuthoringValidationGate.ExportBlocked),
            "duplicate HitZone ownership should use CHARDZ006.");
        Require(HasCompilerIssue(result, CharacterAuthoringValidationCodes.DamageZoneDuplicateZoneId,
                CharacterAuthoringValidationGate.ExportBlocked),
            "duplicate Zone id should use CHARDZ003.");
        Require(HasCompilerIssue(result, CharacterAuthoringValidationCodes.DamageZoneUnknownHitZone,
                CharacterAuthoringValidationGate.ExportBlocked),
            "unknown HitZone should use CHARDZ007.");
        Require(HasCompilerIssue(result, CharacterAuthoringValidationCodes.DamageZoneInvalidPriority,
                CharacterAuthoringValidationGate.ExportBlocked),
            "out-of-range priority should use CHARDZ009.");
        Require(result.GateReport.ExportBlocked, "invalid DamageZone document must block compiler writes.");
    }

    private static void DamageZones_UnknownSchemaIsLosslessAndWriteBlocked()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-damage-zone-unknown-" + Guid.NewGuid().ToString("N"));
        string packagePath = Path.Combine(tempRoot, "CharacterSource", "character-codex-flow-test");
        try
        {
            CopyDirectory(FindSamplePath("character-codex-flow-test"), packagePath);
            string damageZonePath = Path.Combine(packagePath, "geometry", "damage_zones.json");
            const string payload = "{\n  \"format\": \"mx.characterDamageZones.v9\",\n  \"schemaVersion\": \"9.7\",\n  \"future\": { \"keep\": true }\n}\n";
            File.WriteAllText(damageZonePath, payload);
            JsonSerializerOptions options = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, options);
            Require(package.Geometry.DamageZonesDocumentState == CharacterDamageZonesDocumentState.UnsupportedSchema
                && !package.Geometry.DamageZonesWriteEligible,
                "unknown DamageZone schema should be read-only.");
            Require(package.Geometry.DamageZonesRawPayload == payload,
                "unknown DamageZone payload must be retained byte-for-byte in package state.");
            CharacterAuthoringCompileResult result = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
            {
                Package = package,
                PackageRootPath = packagePath
            });
            Require(HasCompilerIssue(result, CharacterAuthoringValidationCodes.DamageZoneUnsupportedSchema,
                    CharacterAuthoringValidationGate.ExportBlocked),
                "unknown DamageZone schema should use CHARDZ002 and block export.");
            try
            {
                EditorServer.SaveCharacterStudioPackage(tempRoot, "CharacterSource/character-codex-flow-test", package, options);
                Require(false, "unknown DamageZone schema must reject package save.");
            }
            catch (InvalidOperationException)
            {
                Require(File.ReadAllText(damageZonePath) == payload,
                    "rejected save must not overwrite the unknown DamageZone payload.");
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    private static void DamageZones_MissingDocumentIsNotCreatedByPackageSave()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-damage-zone-missing-" + Guid.NewGuid().ToString("N"));
        string packagePath = Path.Combine(tempRoot, "CharacterSource", "character-codex-flow-test");
        try
        {
            CopyDirectory(FindSamplePath("character-codex-flow-test"), packagePath);
            CopyDirectory(Path.Combine(FindRepoRoot(), "WeaponSource"), Path.Combine(tempRoot, "WeaponSource"));
            CopyDirectory(Path.Combine(FindRepoRoot(), "CharacterActionSource"), Path.Combine(tempRoot, "CharacterActionSource"));
            JsonSerializerOptions options = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, options);
            EditorServer.SaveCharacterStudioPackage(tempRoot, "CharacterSource/character-codex-flow-test", package, options);
            Require(!File.Exists(Path.Combine(packagePath, "geometry", "damage_zones.json")),
                "saving an unrelated legacy package must not create damage_zones.json.");
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    private static CharacterDamageZoneAuthoring Zone(
        string zoneId,
        string hitZoneId,
        int priority,
        bool hittable,
        bool aimable,
        bool approachable)
    {
        return new CharacterDamageZoneAuthoring
        {
            ZoneId = zoneId,
            DisplayName = zoneId,
            HitZoneIds = new List<string> { hitZoneId },
            AuthoredPriority = priority,
            Hittable = hittable,
            Aimable = aimable,
            Approachable = approachable
        };
    }

    private static List<string> CollectHitZones(CharacterResourcePackage package)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < package.Geometry.Colliders.Count; i++)
        {
            string hitZoneId = package.Geometry.Colliders[i]?.HitZoneId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(hitZoneId) && seen.Add(hitZoneId))
                result.Add(hitZoneId);
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }
}

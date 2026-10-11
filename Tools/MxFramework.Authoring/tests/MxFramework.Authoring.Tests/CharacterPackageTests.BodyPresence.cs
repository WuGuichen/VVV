using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    internal static void RunCharacterBodyPresence()
    {
        BodyPresence_LoadStatesAndCanonicalizationAreStable();
        BodyPresence_InvalidFieldsAndCenterDebtUseTypedDiagnostics();
        BodyPresence_DedicatedSaveUsesRevisionAndWholePackageSavePreservesBytes();
        BodyPresence_FuturePayloadAndAtomicFailurePreserveTargetBytes();
        BodyPresence_V2AnimatedSourcesAreCanonicalAndValidated();
        BodyPresence_V2CompilerEmitsGeometryV7WithoutCopyingShapes();
    }

    private static void BodyPresence_V2AnimatedSourcesAreCanonicalAndValidated()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.Geometry.Colliders[0].PhysicsLayer = "CharacterHurtbox";
        package.Geometry.Colliders[1].PhysicsLayer = "CharacterHurtbox";
        CharacterBodyPresenceDocument document = PresenceDocumentV2(
            new CharacterBodyPresenceAnimatedHurtboxSourceAuthoring
            {
                SourceId = "presence.animated.zeta", ColliderId = package.Geometry.Colliders[1].ColliderId,
                SkinMillimeters = 75, PressureScaleMilli = 900, Enabled = true
            },
            new CharacterBodyPresenceAnimatedHurtboxSourceAuthoring
            {
                SourceId = "presence.animated.alpha", ColliderId = package.Geometry.Colliders[0].ColliderId,
                SkinMillimeters = 50, PressureScaleMilli = 1000, Enabled = true
            });
        package.Geometry.BodyPresenceDocumentState = CharacterBodyPresenceDocumentState.Current;
        package.Geometry.BodyPresence = document;
        CharacterAuthoringValidationReport valid = CharacterResourcePackageValidator.Validate(package);
        Require(!HasValidationIssue(valid, CharacterAuthoringValidationCodes.BodyPresenceMissingAnimatedCollider,
                CharacterAuthoringValidationGate.ExportBlocked)
            && CharacterBodyPresenceDocumentCodec.SerializeCanonical(document).IndexOf("presence.animated.alpha", StringComparison.Ordinal)
                < CharacterBodyPresenceDocumentCodec.SerializeCanonical(document).IndexOf("presence.animated.zeta", StringComparison.Ordinal),
            "v2 animated sources must validate and canonicalize by sourceId.");

        document.AnimatedHurtboxSources[1].ColliderId = document.AnimatedHurtboxSources[0].ColliderId;
        document.AnimatedHurtboxSources.Add(new CharacterBodyPresenceAnimatedHurtboxSourceAuthoring
        {
            SourceId = "Bad Source", ColliderId = "missing", SkinMillimeters = -1,
            PressureScaleMilli = CharacterBodyPresenceLimits.MaximumPressureScaleMilli + 1, Enabled = false
        });
        CharacterAuthoringValidationReport invalid = CharacterResourcePackageValidator.Validate(package);
        string[] codes =
        {
            CharacterAuthoringValidationCodes.BodyPresenceDuplicateAnimatedCollider,
            CharacterAuthoringValidationCodes.BodyPresenceInvalidAnimatedSourceId,
            CharacterAuthoringValidationCodes.BodyPresenceMissingAnimatedCollider,
            CharacterAuthoringValidationCodes.BodyPresenceInvalidAnimatedSourcePolicy
        };
        for (int i = 0; i < codes.Length; i++)
            Require(HasValidationIssue(invalid, codes[i], CharacterAuthoringValidationGate.ExportBlocked),
                "invalid v2 source must report typed blocker " + codes[i] + ".");

        const string legacy = "{\"format\":\"mx.characterBodyPresence.v1\",\"schemaVersion\":\"1.0\",\"revision\":1,\"volumes\":[]}";
        CharacterBodyPresenceLoadResult legacyRead = CharacterBodyPresenceDocumentCodec.Read(
            legacy, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
        Require(legacyRead.State == CharacterBodyPresenceDocumentState.Current
                && CharacterBodyPresenceDocumentCodec.SerializeCanonical(legacyRead.Document) == legacy,
            "v1 canonical bytes must not acquire a v2 field or schema upgrade.");
    }

    private static void BodyPresence_V2CompilerEmitsGeometryV7WithoutCopyingShapes()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.Geometry.Colliders[0].PhysicsLayer = "CharacterHurtbox";
        package.Geometry.BodyPresenceDocumentState = CharacterBodyPresenceDocumentState.Current;
        package.Geometry.BodyPresence = PresenceDocumentV2(new CharacterBodyPresenceAnimatedHurtboxSourceAuthoring
        {
            SourceId = "presence.animated.torso", ColliderId = package.Geometry.Colliders[0].ColliderId,
            SkinMillimeters = 50, PressureScaleMilli = 1000, Enabled = true
        });
        CharacterAuthoringCompileResult result = Compile(package);
        Require((result.GeometryBinding.Format == CharacterAuthoringCompilerFormats.GeometryBindingV7 || result.GeometryBinding.Format == CharacterAuthoringCompilerFormats.GeometryBindingV8)
                && (result.GeometryBinding.SchemaVersion == "7.0" || result.GeometryBinding.SchemaVersion == "8.0")
                && result.GeometryBinding.AnimatedHurtboxSources.Count == 1
                && result.GeometryBinding.AnimatedHurtboxSources[0].ColliderLabel == package.Geometry.Colliders[0].ColliderId,
            "Current Body Presence v2 must emit one additive geometry v7/v8 source reference.");
        string json = JsonSerializer.Serialize(result.GeometryBinding.AnimatedHurtboxSources,
            MxFramework.Authoring.Cli.Program.CreateJsonOptions());
        Require(!json.Contains("localPose", StringComparison.Ordinal)
                && !json.Contains("shape", StringComparison.Ordinal)
                && !json.Contains("partId", StringComparison.Ordinal),
            "animated source binding must not duplicate collider shapes or poses.");
    }

    private static void BodyPresence_LoadStatesAndCanonicalizationAreStable()
    {
        JsonSerializerOptions options = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        CharacterBodyPresenceLoadResult missing = CharacterBodyPresenceDocumentCodec.Missing();
        Require(missing.State == CharacterBodyPresenceDocumentState.Missing
                && !missing.WriteEligible && missing.Document == null,
            "missing Body Presence must not synthesize a document.");

        CharacterBodyPresenceDocument first = PresenceDocument(
            PresenceVolume("presence.zeta", CharacterBodyPresenceShape.Sphere, 0.7f),
            PresenceVolume("presence.alpha", CharacterBodyPresenceShape.Capsule, 0.4f));
        CharacterBodyPresenceDocument second = PresenceDocument(
            PresenceVolume("presence.alpha", CharacterBodyPresenceShape.Capsule, 0.4f),
            PresenceVolume("presence.zeta", CharacterBodyPresenceShape.Sphere, 0.7f));
        string canonicalFirst = CharacterBodyPresenceDocumentCodec.SerializeCanonical(first);
        string canonicalSecond = CharacterBodyPresenceDocumentCodec.SerializeCanonical(second);
        Require(canonicalFirst == canonicalSecond
                && CharacterBodyPresenceDocumentCodec.ComputeContentHash(first)
                    == CharacterBodyPresenceDocumentCodec.ComputeContentHash(second),
            "volume order must not affect Body Presence canonical bytes or hash.");
        Require(canonicalFirst.IndexOf("presence.alpha", StringComparison.Ordinal)
                < canonicalFirst.IndexOf("presence.zeta", StringComparison.Ordinal),
            "canonical Body Presence must sort by volumeId ordinal.");

        string currentPayload = JsonSerializer.Serialize(first, options);
        CharacterBodyPresenceLoadResult current = CharacterBodyPresenceDocumentCodec.Read(currentPayload, options);
        Require(current.State == CharacterBodyPresenceDocumentState.Current
                && current.WriteEligible && current.Document.Volumes.Count == 2,
            "current Body Presence must round-trip as write-eligible.");

        const string numericEnumPayload =
            "{\"format\":\"mx.characterBodyPresence.v1\",\"schemaVersion\":\"1.0\"," +
            "\"revision\":1,\"volumes\":[{\"volumeId\":\"presence.numeric\"," +
            "\"referenceFrame\":1,\"shape\":1," +
            "\"localPose\":{\"position\":{\"x\":0,\"y\":0,\"z\":0}," +
            "\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1}," +
            "\"scale\":{\"x\":1,\"y\":1,\"z\":1}}," +
            "\"radius\":1,\"height\":0,\"size\":{\"x\":0,\"y\":0,\"z\":0}," +
            "\"skinMillimeters\":0,\"pressureScaleMilli\":1000,\"enabled\":true}]}";
        CharacterBodyPresenceLoadResult numericEnum =
            CharacterBodyPresenceDocumentCodec.Read(numericEnumPayload, options);
        Require(numericEnum.State == CharacterBodyPresenceDocumentState.Current
                && numericEnum.Document.Volumes[0].ReferenceFrame == CharacterBodyPresenceReferenceFrame.Unknown
                && numericEnum.Document.Volumes[0].Shape == CharacterBodyPresenceShape.Unknown,
            "numeric enum encodings must remain typed-invalid instead of bypassing the string schema.");

        const string emptyPayload = "{\"format\":\"mx.characterBodyPresence.v1\",\"schemaVersion\":\"1.0\",\"revision\":1,\"volumes\":[]}";
        CharacterBodyPresenceLoadResult empty = CharacterBodyPresenceDocumentCodec.Read(emptyPayload, options);
        Require(empty.State == CharacterBodyPresenceDocumentState.Current
                && empty.Document.Volumes.Count == 0,
            "explicit empty Body Presence is distinct from missing.");

        const string missingRequiredPayload =
            "{\"format\":\"mx.characterBodyPresence.v1\",\"schemaVersion\":\"1.0\",\"revision\":1}";
        CharacterBodyPresenceLoadResult missingRequired =
            CharacterBodyPresenceDocumentCodec.Read(missingRequiredPayload, options);
        Require(missingRequired.State == CharacterBodyPresenceDocumentState.Invalid
                && missingRequired.RawPayload == missingRequiredPayload,
            "known-schema Body Presence with missing required properties must fail closed.");

        const string invalidPayload = "{ not-json";
        CharacterBodyPresenceLoadResult invalid = CharacterBodyPresenceDocumentCodec.Read(invalidPayload, options);
        Require(invalid.State == CharacterBodyPresenceDocumentState.Invalid
                && invalid.RawPayload == invalidPayload,
            "invalid Body Presence must retain raw input and stay write-ineligible.");

        const string futurePayload = "{\n  \"format\": \"mx.characterBodyPresence.v9\",\n  \"schemaVersion\": \"9.0\",\n  \"future\": { \"keep\": true }\n}\n";
        CharacterBodyPresenceLoadResult future = CharacterBodyPresenceDocumentCodec.Read(futurePayload, options);
        Require(future.State == CharacterBodyPresenceDocumentState.UnsupportedSchema
                && future.RawPayload == futurePayload && future.Document == null,
            "future Body Presence must preserve raw UTF-8 text and remain read-only.");
    }

    private static void BodyPresence_InvalidFieldsAndCenterDebtUseTypedDiagnostics()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.Geometry.BodyPresenceDocumentState = CharacterBodyPresenceDocumentState.Current;
        package.Geometry.BodyPresence = PresenceDocument(
            PresenceVolume("presence.same", CharacterBodyPresenceShape.Unknown, -1f),
            PresenceVolume("presence.same", CharacterBodyPresenceShape.Box, 1f),
            PresenceVolume("Bad Id", CharacterBodyPresenceShape.Sphere, 1f),
            PresenceVolume("presence.four", CharacterBodyPresenceShape.Sphere, 1f),
            PresenceVolume("presence.five", CharacterBodyPresenceShape.Sphere, 1f));
        CharacterBodyPresenceVolumeAuthoring invalid = package.Geometry.BodyPresence.Volumes[0];
        invalid.ReferenceFrame = CharacterBodyPresenceReferenceFrame.Unknown;
        invalid.LocalPose.Scale.X = 2f;
        invalid.SkinMillimeters = -1;
        invalid.PressureScaleMilli = CharacterBodyPresenceLimits.MaximumPressureScaleMilli + 1;
        package.Geometry.BodyProfile.DefaultCapsule.Center.X = 0.25f;
        package.Geometry.BodyProfile.DefaultCapsule.Center.Z = -0.5f;

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);
        string[] blockers =
        {
            CharacterAuthoringValidationCodes.BodyPresenceTooManyVolumes,
            CharacterAuthoringValidationCodes.BodyPresenceDuplicateVolumeId,
            CharacterAuthoringValidationCodes.BodyPresenceInvalidVolumeId,
            CharacterAuthoringValidationCodes.BodyPresenceInvalidReferenceFrame,
            CharacterAuthoringValidationCodes.BodyPresenceInvalidShape,
            CharacterAuthoringValidationCodes.BodyPresenceInvalidPose,
            CharacterAuthoringValidationCodes.BodyPresenceInvalidDimensions,
            CharacterAuthoringValidationCodes.BodyPresenceInvalidSkin,
            CharacterAuthoringValidationCodes.BodyPresenceInvalidPressureScale
        };
        for (int i = 0; i < blockers.Length; i++)
        {
            Require(HasValidationIssue(report, blockers[i], CharacterAuthoringValidationGate.ExportBlocked),
                "invalid Body Presence must report typed blocker " + blockers[i] + ".");
        }
        CharacterAuthoringValidationIssue center = FindValidationIssue(
            report,
            CharacterAuthoringValidationCodes.RootCapsuleCenterXZNotConsumed,
            CharacterAuthoringValidationGate.WarningOnly);
        Require(center != null && center.Message.Contains("X=0.25", StringComparison.Ordinal)
                && center.Message.Contains("Z=-0.5", StringComparison.Ordinal)
                && center.Message.Contains("consumes center.y only", StringComparison.Ordinal),
            "center X/Z diagnostic must include source values and the Runtime Y-only fact.");

        package.Geometry.BodyPresence = PresenceDocument(
            PresenceVolume("presence.short-capsule", CharacterBodyPresenceShape.Capsule, 1f));
        package.Geometry.BodyPresence.Volumes[0].Height = 1.5f;
        CharacterAuthoringValidationReport shortCapsule = CharacterResourcePackageValidator.Validate(package);
        Require(HasValidationIssue(
                shortCapsule,
                CharacterAuthoringValidationCodes.BodyPresenceInvalidDimensions,
                CharacterAuthoringValidationGate.ExportBlocked),
            "capsule height shorter than its diameter must fail closed.");
    }

    private static void BodyPresence_DedicatedSaveUsesRevisionAndWholePackageSavePreservesBytes()
    {
        WithTemporaryPackage((tempRoot, relative, packagePath, options) =>
        {
            string beforeRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            CharacterBodyPresenceDocument document = PresenceDocument(
                PresenceVolume("presence.core", CharacterBodyPresenceShape.Capsule, 0.6f));
            EditorServer.SaveCharacterBodyPresenceAtPath(
                packagePath,
                Current(document),
                beforeRevision,
                options);
            string target = Path.Combine(packagePath, "geometry", "body_presence.json");
            Require(File.Exists(target), "explicit dedicated save must create body_presence.json.");
            byte[] saved = File.ReadAllBytes(target);
            CharacterResourcePackage loaded = CharacterPackageCommands.ReadPackage(packagePath, options);
            Require(loaded.Geometry.BodyPresenceDocumentState == CharacterBodyPresenceDocumentState.Current
                    && loaded.Geometry.BodyPresence.Volumes.Count == 1,
                "dedicated save must round-trip through package load.");

            try
            {
                EditorServer.SaveCharacterBodyPresenceAtPath(
                    packagePath,
                    Current(document),
                    beforeRevision,
                    options);
                Require(false, "stale Body Presence revision must be rejected.");
            }
            catch (EditorServer.CharacterPackageWriteConflictException)
            {
                Require(BytesEqual(saved, File.ReadAllBytes(target)),
                    "revision conflict must preserve Body Presence bytes.");
            }

            string currentRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            loaded.Geometry.BodyPresence.Revision = 99;
            loaded.Geometry.BodyPresence.Volumes[0].Radius = 9f;
            EditorServer.SaveCharacterStudioPackage(
                tempRoot,
                relative,
                loaded,
                options,
                currentRevision);
            Require(BytesEqual(saved, File.ReadAllBytes(target)),
                "whole-package save must preserve persisted Body Presence bytes and cannot be a second writer.");
        });
    }

    private static void BodyPresence_FuturePayloadAndAtomicFailurePreserveTargetBytes()
    {
        WithTemporaryPackage((tempRoot, relative, packagePath, options) =>
        {
            string target = Path.Combine(packagePath, "geometry", "body_presence.json");
            const string future = "{\n  \"format\": \"mx.characterBodyPresence.v9\",\n  \"schemaVersion\": \"9.1\",\n  \"future\": true\n}\n";
            File.WriteAllText(target, future);
            string futureRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            try
            {
                EditorServer.SaveCharacterBodyPresenceAtPath(
                    packagePath,
                    Current(PresenceDocument(PresenceVolume(
                        "presence.core", CharacterBodyPresenceShape.Sphere, 1f))),
                    futureRevision,
                    options);
                Require(false, "future Body Presence must reject replacement by a current default document.");
            }
            catch (EditorServer.CharacterPackageValidationException)
            {
                Require(File.ReadAllText(target) == future,
                    "rejected future-schema save must preserve exact source text.");
            }

            File.Delete(target);
            string missingRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            CharacterBodyPresenceDocument initial = PresenceDocument(
                PresenceVolume("presence.core", CharacterBodyPresenceShape.Sphere, 1f));
            EditorServer.SaveCharacterBodyPresenceAtPath(
                packagePath, Current(initial), missingRevision, options);
            byte[] original = File.ReadAllBytes(target);
            string updateRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            CharacterBodyPresenceDocument update = PresenceDocument(
                PresenceVolume("presence.core", CharacterBodyPresenceShape.Sphere, 2f));
            bool failed = false;
            using (File.Open(target, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try
                {
                    EditorServer.SaveCharacterBodyPresenceAtPath(
                        packagePath, Current(update), updateRevision, options);
                }
                catch (IOException)
                {
                    failed = true;
                }
            }
            Require(failed, "locked target must exercise the atomic replacement failure path.");
            Require(BytesEqual(original, File.ReadAllBytes(target)),
                "atomic replacement failure must preserve original Body Presence bytes.");
            Require(!File.Exists(target + ".tmp"),
                "atomic replacement failure must not leave a readable temp file.");
        });
    }

    private static CharacterBodyPresenceDocument PresenceDocument(
        params CharacterBodyPresenceVolumeAuthoring[] volumes)
    {
        return new CharacterBodyPresenceDocument
        {
            Format = CharacterBodyPresenceDocument.LegacyFormat,
            SchemaVersion = CharacterBodyPresenceDocument.LegacySchemaVersion,
            Revision = 1,
            Volumes = new List<CharacterBodyPresenceVolumeAuthoring>(volumes)
        };
    }

    private static CharacterBodyPresenceDocument PresenceDocumentV2(
        params CharacterBodyPresenceAnimatedHurtboxSourceAuthoring[] sources)
    {
        return new CharacterBodyPresenceDocument
        {
            Revision = 2,
            Volumes = new List<CharacterBodyPresenceVolumeAuthoring>(),
            AnimatedHurtboxSources = new List<CharacterBodyPresenceAnimatedHurtboxSourceAuthoring>(sources)
        };
    }

    private static CharacterBodyPresenceVolumeAuthoring PresenceVolume(
        string volumeId,
        CharacterBodyPresenceShape shape,
        float primaryDimension)
    {
        return new CharacterBodyPresenceVolumeAuthoring
        {
            VolumeId = volumeId,
            ReferenceFrame = CharacterBodyPresenceReferenceFrame.ModelRoot,
            Shape = shape,
            LocalPose = new CharacterBodyPresenceLocalPose(),
            Radius = shape == CharacterBodyPresenceShape.Sphere
                || shape == CharacterBodyPresenceShape.Capsule ? primaryDimension : 0f,
            Height = shape == CharacterBodyPresenceShape.Capsule ? primaryDimension * 2f : 0f,
            Size = shape == CharacterBodyPresenceShape.Box
                ? new CharacterAuthoringVector3(primaryDimension, primaryDimension, primaryDimension)
                : new CharacterAuthoringVector3(),
            SkinMillimeters = 25,
            PressureScaleMilli = 1000,
            Enabled = true
        };
    }

    private static CharacterBodyPresenceLoadResult Current(CharacterBodyPresenceDocument document)
    {
        return new CharacterBodyPresenceLoadResult
        {
            State = CharacterBodyPresenceDocumentState.Current,
            Document = document
        };
    }

    private static void WithTemporaryPackage(
        Action<string, string, string, JsonSerializerOptions> action)
    {
        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "mx-character-body-presence-" + Guid.NewGuid().ToString("N"));
        const string relative = "CharacterSource/character-codex-flow-test";
        string packagePath = Path.Combine(tempRoot, "CharacterSource", "character-codex-flow-test");
        try
        {
            CopyDirectory(FindSamplePath("character-codex-flow-test"), packagePath);
            CopyDirectory(Path.Combine(FindRepoRoot(), "WeaponSource"), Path.Combine(tempRoot, "WeaponSource"));
            CopyDirectory(
                Path.Combine(FindRepoRoot(), "CharacterActionSource"),
                Path.Combine(tempRoot, "CharacterActionSource"));
            action(tempRoot, relative, packagePath, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    private static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++)
            if (left[i] != right[i])
                return false;
        return true;
    }
}

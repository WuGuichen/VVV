using System;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    internal static void RunLccM0Integration()
    {
        LccM0_DragonFixtureProjectsCanonicalThreeVolumeState();
        LccM0_CharacterStudioServiceStateAndIndependentSaveFailClosed();
    }

    private static void LccM0_DragonFixtureProjectsCanonicalThreeVolumeState()
    {
        JsonSerializerOptions options = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        string packagePath = Path.Combine(
            FindRepoRoot(),
            "Authoring",
            "CharacterPackages",
            "character-000003");
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, options);
        CharacterBodyPresenceDocument document = package.Geometry.BodyPresence;
        Require(package.Geometry.BodyPresenceDocumentState == CharacterBodyPresenceDocumentState.Current
                && package.Geometry.BodyPresenceWriteEligible
                && document?.Volumes?.Count == 3,
            "Dragon14 must expose a Current three-volume Body Presence fixture.");
        Require(document.Volumes[0].VolumeId == "presence.000003.core.neck"
                && document.Volumes[1].VolumeId == "presence.000003.core.tail_root"
                && document.Volumes[2].VolumeId == "presence.000003.core.torso",
            "Dragon14 Body Presence must retain canonical volumeId order.");

        JsonElement api = ReadBodyPresenceApiJson(packagePath, options);
        Require(api.GetProperty("state").GetString() == "Current"
                && api.GetProperty("writeEligible").GetBoolean()
                && api.GetProperty("document").GetProperty("volumes").GetArrayLength() == 3
                && api.GetProperty("contentHash").GetString()
                    == CharacterBodyPresenceDocumentCodec.ComputeContentHash(document)
                && !string.IsNullOrWhiteSpace(api.GetProperty("packageRevision").GetString()),
            "dedicated service state must project Dragon14 Current content, hash, revision, and all volumes.");
    }

    private static void LccM0_CharacterStudioServiceStateAndIndependentSaveFailClosed()
    {
        WithTemporaryPackage((tempRoot, relative, packagePath, options) =>
        {
            JsonElement missing = ReadBodyPresenceApiJson(packagePath, options);
            Require(missing.GetProperty("state").GetString() == "Missing"
                    && !missing.GetProperty("writeEligible").GetBoolean()
                    && missing.GetProperty("document").ValueKind == JsonValueKind.Null,
                "CharacterStudio service state must expose Missing without synthesizing a document.");

            CharacterResourcePackage dragon = CharacterPackageCommands.ReadPackage(
                Path.Combine(
                    FindRepoRoot(),
                    "Authoring",
                    "CharacterPackages",
                "character-000003"),
                options);
            CharacterBodyPresenceDocument standalonePresence =
                CharacterBodyPresenceDocumentCodec.CreateCanonicalDocument(dragon.Geometry.BodyPresence);
            standalonePresence.AnimatedHurtboxSources.Clear();
            EditorServer.SaveCharacterBodyPresenceAtPath(
                packagePath,
                Current(standalonePresence),
                missing.GetProperty("packageRevision").GetString(),
                options);
            JsonElement reopened = ReadBodyPresenceApiJson(packagePath, options);
            Require(reopened.GetProperty("state").GetString() == "Current"
                    && reopened.GetProperty("writeEligible").GetBoolean()
                    && reopened.GetProperty("document").GetProperty("volumes").GetArrayLength() == 3,
                "dedicated save must reopen the Current Dragon14 three-volume document.");

            string target = Path.Combine(packagePath, "geometry", "body_presence.json");
            const string future = "{\n  \"format\": \"mx.characterBodyPresence.v9\",\n  \"schemaVersion\": \"9.0\",\n  \"future\": true\n}\n";
            File.WriteAllText(target, future);
            JsonElement futureState = ReadBodyPresenceApiJson(packagePath, options);
            Require(futureState.GetProperty("state").GetString() == "UnsupportedSchema"
                    && !futureState.GetProperty("writeEligible").GetBoolean()
                    && futureState.GetProperty("rawPayload").GetString() == future,
                "future Body Presence must reach CharacterStudio as exact read-only raw text.");
            RequireWholePackageSaveBlockedWithoutChangingBodyPresence(
                tempRoot, relative, packagePath, options, future,
                "future Body Presence");

            const string invalid = "{ not-json\n";
            File.WriteAllText(target, invalid);
            JsonElement invalidState = ReadBodyPresenceApiJson(packagePath, options);
            Require(invalidState.GetProperty("state").GetString() == "Invalid"
                    && !invalidState.GetProperty("writeEligible").GetBoolean()
                    && invalidState.GetProperty("rawPayload").GetString() == invalid,
                "invalid Body Presence must reach CharacterStudio as exact read-only raw text.");
            RequireWholePackageSaveBlockedWithoutChangingBodyPresence(
                tempRoot, relative, packagePath, options, invalid,
                "invalid Body Presence");
        });
    }

    private static JsonElement ReadBodyPresenceApiJson(
        string packagePath,
        JsonSerializerOptions options)
    {
        return JsonSerializer.SerializeToElement(
            EditorServer.ReadCharacterBodyPresenceStateAtPath(packagePath, options),
            options);
    }

    private static void RequireWholePackageSaveBlockedWithoutChangingBodyPresence(
        string rootPath,
        string relative,
        string packagePath,
        JsonSerializerOptions options,
        string expectedRaw,
        string label)
    {
        string target = Path.Combine(packagePath, "geometry", "body_presence.json");
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, options);
        bool blocked = false;
        try
        {
            EditorServer.SaveCharacterStudioPackage(
                rootPath,
                relative,
                package,
                options,
                EditorServer.ComputeCharacterPackageRevision(packagePath));
        }
        catch (EditorServer.CharacterPackageValidationException)
        {
            blocked = true;
        }
        Require(blocked, label + " must fail closed through whole-package save.");
        Require(File.ReadAllText(target) == expectedRaw,
            label + " must not be replaced by whole-package save.");
    }
}

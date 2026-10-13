using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    private const string CueDefaultParameterMissingCode = "AUTH_RES_AUDIO_CUE_DEFAULT_PARAMETER_MISSING";

    private static void AudioAuthoringManifestV2_ValidatesCueDefaultParameterReferences()
    {
        AudioSemanticManifestDocument manifest = CreateCueDefaultParameterFixture(out var snapshot);
        AudioCueAuthoringDefinition cue = manifest.Cues[0];
        Require(!AudioAuthoringManifestV2.Validate(manifest, snapshot).HasErrors,
            "a declared Cue default parameter should pass validation.");
        AudioRuntimeCatalogDocument catalog = AudioAuthoringManifestV2.CompileRuntimeCatalog(manifest, "manifest", "snapshot");
        Require(catalog.Cues.Count == 1 && catalog.Cues[0].DefaultParameters.Count == 1
            && catalog.Cues[0].DefaultParameters[0].ParameterId == 500101
            && catalog.Cues[0].DefaultParameters[0].Value == 0.75f,
            "Speed=0.75 should compile unchanged to parameter 500101.");

        foreach (string name in new[] { "Speeed", "OtherOnly", "speed", "" })
        {
            cue.DefaultParameters[0].Name = name;
            AudioAuthoringManifestV2.Normalize(manifest, snapshot);
            AudioSemanticManifestValidationReport report = AudioAuthoringManifestV2.Validate(manifest, snapshot);
            AuthoringResourceDiagnostic diagnostic = report.Diagnostics.Find(item => item.Code == CueDefaultParameterMissingCode);
            Require(report.HasErrors && diagnostic != null
                && diagnostic.Severity == CharacterAuthoringValidationSeverity.Error
                && diagnostic.RuntimeResourceKey == cue.AudioCueId
                && diagnostic.SourceField == "cues/0/defaultParameters/0/name"
                && diagnostic.Message.Contains(cue.AudioCueId, StringComparison.Ordinal)
                && diagnostic.Message.Contains(name, StringComparison.Ordinal),
                "unknown, foreign-Event, case-mismatched and empty parameter names need a located Cue diagnostic: " + name);
            bool rejected = false;
            try { AudioAuthoringManifestV2.CompileRuntimeCatalog(manifest, "manifest", "snapshot"); }
            catch (InvalidOperationException exception)
            {
                rejected = exception.Message.Contains(CueDefaultParameterMissingCode, StringComparison.Ordinal)
                    && exception.Message.Contains(cue.AudioCueId, StringComparison.Ordinal);
            }
            Require(rejected, "direct compilation must reject unresolved defaults instead of silently dropping them.");
        }

        cue.DefaultParameters[0].Name = "Speed";
        cue.DefaultParameters.Add(null);
        AudioSemanticManifestValidationReport nullDefault = AudioAuthoringManifestV2.Validate(manifest, snapshot);
        Require(nullDefault.HasErrors && nullDefault.Diagnostics.Exists(item => item.Code == CueDefaultParameterMissingCode
            && item.SourceField == "cues/0/defaultParameters/1/name"),
            "a null default entry must have its own indexed diagnostic.");

        cue.EventDefinitionId = "event.missing";
        AudioSemanticManifestValidationReport missingEvent = AudioAuthoringManifestV2.Validate(manifest, snapshot);
        Require(missingEvent.HasErrors
            && missingEvent.Diagnostics.Exists(item => item.Code == AuthoringResourceDiagnosticCodes.AudioSemanticEventMissing
                && item.SourceField == "cues/0/eventDefinitionId")
            && !missingEvent.Diagnostics.Exists(item => item.Code == CueDefaultParameterMissingCode),
            "missing Event references should retain their original diagnostic without cascading parameter errors.");

        cue.EventDefinitionId = "event.probe";
        cue.DefaultParameters.Clear();
        Require(!AudioAuthoringManifestV2.Validate(manifest, snapshot).HasErrors,
            "Cues without defaults should remain valid.");
        Require(AudioAuthoringManifestV2.CompileRuntimeCatalog(manifest, "manifest", "snapshot")
            .Cues[0].DefaultParameters.Count == 0, "Cues without defaults should compile normally.");

        var legacy = new AudioSemanticManifestDocument { SchemaVersion = 1 };
        legacy.Entries.Add(new AudioSemanticManifestEntry
        {
            AudioCueId = "sfx.probe.legacy", AudioEventDefinitionId = "event.probe",
            FmodEventPath = "event:/Probe/OneShot", FmodEventGuid = "{probe}",
            DisplayName = "Legacy", Purpose = "test", Context = "test"
        });
        AudioSemanticManifestDocument upgraded = AudioAuthoringManifestV2.Upgrade(legacy, snapshot);
        Require(upgraded.SchemaVersion == 2 && !AudioAuthoringManifestV2.Validate(upgraded, snapshot).HasErrors,
            "schema v1 upgrade should not introduce default parameter reference errors.");
        upgraded.Cues[0].Lifecycle = AudioCueLifecycles.Active;
        Require(AudioAuthoringManifestV2.CompileRuntimeCatalog(upgraded, "manifest", "snapshot")
            .Cues[0].DefaultParameters.Count == 0, "an upgraded Cue with no defaults should compile normally when activated.");
    }

    private static void EditorServer_AudioWorkbench_BlocksUnknownCueDefaultParametersWithoutWrites()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-audio-cue-defaults-" + Guid.NewGuid().ToString("N"));
        try
        {
            AudioSemanticManifestDocument manifest = CreateCueDefaultParameterFixture(out var snapshot);
            string manifestPath = Path.Combine(root, "Assets/Config/MxFramework/Audio/audio_semantic_manifest.json");
            string generationPath = Path.Combine(root, "Assets/Config/MxFramework/Audio/audio_generation_manifest.json");
            string physicalPath = Path.Combine(root, "Assets/Config/MxFramework/Audio/audio_physical_manifest.json");
            string snapshotPath = Path.Combine(root, "Assets/MxFrameworkGenerated/Audio/fmod_audio_library.json");
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath));
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
            File.WriteAllText(generationPath, JsonSerializer.Serialize(new EditorServer.AudioGenerationManifestDocument(), JsonOptions));
            File.WriteAllText(physicalPath, JsonSerializer.Serialize(new PhysicalAudioAuthoringManifestDocument(), JsonOptions));
            File.WriteAllText(snapshotPath, JsonSerializer.Serialize(snapshot, JsonOptions));

            var request = new EditorServer.AudioWorkbenchPublishRequest
            {
                expectedHash = CharacterPackageHashUtility.ComputeFileSha256(manifestPath),
                expectedGenerationHash = CharacterPackageHashUtility.ComputeFileSha256(generationPath),
                expectedPhysicalHash = CharacterPackageHashUtility.ComputeFileSha256(physicalPath)
            };
            var validPublish = EditorServer.PublishAudioWorkbench(root, string.Empty, request, JsonOptions);
            Require(validPublish.success && validPublish.catalog.Cues[0].DefaultParameters[0].Value == 0.75f,
                "the isolated fixture must satisfy all formal publication prerequisites before testing the invalid reference: "
                + validPublish.code + " " + validPublish.message);

            foreach (string lifecycle in new[] { AudioCueLifecycles.Active, AudioCueLifecycles.Draft })
            {
                manifest.Cues[0].Lifecycle = lifecycle;
                manifest.Cues[0].DefaultParameters[0].Name = "Speeed";
                var save = EditorServer.SaveAudioWorkbenchDraft(root, string.Empty,
                    new EditorServer.AudioWorkbenchManifestRequest
                    {
                        expectedHash = CharacterPackageHashUtility.ComputeFileSha256(manifestPath),
                        manifest = manifest
                    }, JsonOptions);
                Require(save.success && save.validation.HasErrors
                    && save.validation.Diagnostics.Exists(item => item.Code == CueDefaultParameterMissingCode),
                    "invalid defaults must remain saveable as drafts with publication diagnostics.");
                var persisted = JsonSerializer.Deserialize<AudioSemanticManifestDocument>(File.ReadAllText(manifestPath), JsonOptions);
                Require(persisted.Cues[0].DefaultParameters[0].Name == "Speeed",
                    "draft save must preserve the author's invalid reference for correction.");

                request.expectedHash = save.currentHash;
                request.expectedGenerationHash = CharacterPackageHashUtility.ComputeFileSha256(generationPath);
                Dictionary<string, string> before = SnapshotCueDefaultParameterFiles(root);
                request.developmentPublishAll = false;
                var blocked = EditorServer.PublishAudioWorkbench(root, string.Empty, request, JsonOptions);
                Require(!blocked.success && blocked.httpStatus == 400 && blocked.code == "AUTH_AUDIO_PUBLISH_BLOCKED"
                    && blocked.validation.Diagnostics.Exists(item => item.Code == CueDefaultParameterMissingCode
                        && item.SourceField == "cues/0/defaultParameters/0/name"),
                    "formal publication must return its existing technical gate error for an unresolved default.");
                RequireCueDefaultParameterFilesUnchanged(root, before);

                request.developmentPublishAll = true;
                var developmentBlocked = EditorServer.PublishAudioWorkbench(root, string.Empty, request, JsonOptions);
                Require(!developmentBlocked.success && developmentBlocked.httpStatus == 400
                    && developmentBlocked.code == "AUTH_AUDIO_DEVELOPMENT_PUBLISH_CUE_INVALID",
                    "development publication must also reject unresolvable defaults before compiling.");
                RequireCueDefaultParameterFilesUnchanged(root, before);
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static AudioSemanticManifestDocument CreateCueDefaultParameterFixture(
        out AuthoringFmodAudioLibrarySnapshotDocument snapshot)
    {
        snapshot = new AuthoringFmodAudioLibrarySnapshotDocument { CacheValid = true };
        snapshot.Events.Add(new AuthoringFmodAudioLibraryEvent
        {
            Path = "event:/Probe/OneShot", Guid = "{probe}", Banks = new List<string> { "Master" },
            Parameters = new List<AuthoringFmodAudioLibraryParameter>
            {
                new AuthoringFmodAudioLibraryParameter { Name = "Speed", DefaultValue = 0.2f, MaxValue = 1f }
            }
        });
        snapshot.Events.Add(new AuthoringFmodAudioLibraryEvent
        {
            Path = "event:/Probe/Other", Guid = "{other}", Banks = new List<string> { "Master" },
            Parameters = new List<AuthoringFmodAudioLibraryParameter>
            {
                new AuthoringFmodAudioLibraryParameter { Name = "OtherOnly", MaxValue = 1f }
            }
        });
        var manifest = new AudioSemanticManifestDocument { SchemaVersion = 2 };
        manifest.Buses.Add(new AudioBusAuthoringDefinition { Id = "probe.bus", RuntimeBusId = 500001, FmodBusPath = "bus:/" });
        manifest.EventDefinitions.Add(new AudioEventAuthoringDefinition
        {
            Id = "event.probe", RuntimeEventId = 500001, FmodEventPath = "event:/Probe/OneShot",
            FmodEventGuid = "{probe}", BusId = "probe.bus",
            Parameters = new List<AudioParameterAuthoringDefinition>
            {
                new AudioParameterAuthoringDefinition { Name = "Speed", RuntimeParameterId = 500101, DefaultValue = 0.2f, MaxValue = 1f }
            }
        });
        manifest.EventDefinitions.Add(new AudioEventAuthoringDefinition
        {
            Id = "event.other", RuntimeEventId = 500002, FmodEventPath = "event:/Probe/Other",
            FmodEventGuid = "{other}", BusId = "probe.bus",
            Parameters = new List<AudioParameterAuthoringDefinition>
            {
                new AudioParameterAuthoringDefinition { Name = "OtherOnly", RuntimeParameterId = 500102, MaxValue = 1f }
            }
        });
        manifest.Cues.Add(new AudioCueAuthoringDefinition
        {
            AudioCueId = "sfx.probe.oneshot", RuntimeCueId = 500001, DisplayName = "Probe",
            Purpose = "test", Context = "test", EventDefinitionId = "event.probe", Lifecycle = AudioCueLifecycles.Active,
            DefaultParameters = new List<AudioCueDefaultParameter> { new AudioCueDefaultParameter { Name = "Speed", Value = 0.75f } }
        });
        return AudioAuthoringManifestV2.Upgrade(manifest, snapshot);
    }

    private static Dictionary<string, string> SnapshotCueDefaultParameterFiles(string root)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            files.Add(Path.GetRelativePath(root, path), CharacterPackageHashUtility.ComputeFileSha256(path));
        return files;
    }

    private static void RequireCueDefaultParameterFilesUnchanged(string root, Dictionary<string, string> before)
    {
        Dictionary<string, string> after = SnapshotCueDefaultParameterFiles(root);
        Require(after.Count == before.Count, "blocked publication must not add or delete files.");
        foreach (var file in before)
            Require(after.TryGetValue(file.Key, out string hash) && hash == file.Value,
                "blocked publication must preserve Generated, StreamingAssets and authoring bytes: " + file.Key);
    }
}

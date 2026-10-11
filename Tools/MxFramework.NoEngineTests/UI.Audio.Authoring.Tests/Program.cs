using MxFramework.Authoring;

namespace MxFramework.NoEngineTests.UI.Audio.Authoring;

internal static class Program
{
    private static int Main()
    {
        ValidSetPasses();
        UnsupportedVersionFails();
        MissingCueFails();
        InvalidCueAndEventContractsFail();
        Console.WriteLine("UI Audio authoring contract tests passed.");
        return 0;
    }

    private static void UnsupportedVersionFails()
    {
        (AudioSemanticManifestDocument manifest,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot) = CreateValid();
        manifest.Metadata[
            AudioAuthoringManifestV2.UiFeedbackCueSetMetadataKey] = "v2";
        AudioSemanticManifestValidationReport report =
            AudioAuthoringManifestV2.Validate(manifest, snapshot);
        Require(
            HasCode(
                report,
                "AUTH_AUDIO_UI_FEEDBACK_SET_VERSION_UNSUPPORTED"),
            "An unknown UI feedback Cue set version must not bypass validation.");
    }

    private static void ValidSetPasses()
    {
        (AudioSemanticManifestDocument manifest,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot) = CreateValid();
        AudioSemanticManifestValidationReport report =
            AudioAuthoringManifestV2.Validate(manifest, snapshot);
        Require(
            !report.HasErrors,
            "A valid UI feedback Cue set should pass authoring validation.");
    }

    private static void MissingCueFails()
    {
        (AudioSemanticManifestDocument manifest,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot) = CreateValid();
        manifest.Cues.RemoveAll(item => item.AudioCueId == "ui.error");
        AudioSemanticManifestValidationReport report =
            AudioAuthoringManifestV2.Validate(manifest, snapshot);
        Require(
            HasCode(report, "AUTH_AUDIO_UI_FEEDBACK_SET_INCOMPLETE"),
            "The v1 UI feedback Cue set must require ui.error.");
    }

    private static void InvalidCueAndEventContractsFail()
    {
        (AudioSemanticManifestDocument manifest,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot) = CreateValid();
        manifest.Cues[0].PlayMode = "StartEvent";
        manifest.EventDefinitions[0].Is3D = true;
        AudioSemanticManifestValidationReport report =
            AudioAuthoringManifestV2.Validate(manifest, snapshot);
        Require(
            HasCode(report, "AUTH_AUDIO_UI_FEEDBACK_CUE_CONTRACT_INVALID"),
            "UI Cue play mode drift must fail validation.");
        Require(
            HasCode(report, "AUTH_AUDIO_UI_FEEDBACK_EVENT_CONTRACT_INVALID"),
            "UI Event spatial drift must fail validation.");
    }

    private static (
        AudioSemanticManifestDocument Manifest,
        AuthoringFmodAudioLibrarySnapshotDocument Snapshot) CreateValid()
    {
        var manifest = new AudioSemanticManifestDocument { SchemaVersion = 2 };
        manifest.Metadata[
            AudioAuthoringManifestV2.UiFeedbackCueSetMetadataKey] =
            AudioAuthoringManifestV2.UiFeedbackCueSetV1;
        manifest.Buses.Add(new AudioBusAuthoringDefinition
        {
            Id = "audio.bus.ui",
            RuntimeBusId = 500007,
            DisplayName = "UI",
            FmodBusPath = "bus:/UI"
        });
        var snapshot = new AuthoringFmodAudioLibrarySnapshotDocument
        {
            CacheValid = true
        };

        Add(
            manifest,
            snapshot,
            "ui.click",
            "Click",
            500391,
            500407);
        Add(
            manifest,
            snapshot,
            "ui.confirm",
            "Confirm",
            500392,
            500408);
        Add(
            manifest,
            snapshot,
            "ui.back",
            "Back",
            500393,
            500409);
        Add(
            manifest,
            snapshot,
            "ui.error",
            "Error",
            500394,
            500412);
        return (manifest, snapshot);
    }

    private static void Add(
        AudioSemanticManifestDocument manifest,
        AuthoringFmodAudioLibrarySnapshotDocument snapshot,
        string cueId,
        string kind,
        int cueRuntimeId,
        int eventRuntimeId)
    {
        string eventId = "audio.event.product.ui." + kind.ToLowerInvariant();
        string eventPath = "event:/MxFramework/Product/UI/" + kind;
        string guid = "{" + kind.ToLowerInvariant() + "}";
        manifest.EventDefinitions.Add(new AudioEventAuthoringDefinition
        {
            Id = eventId,
            RuntimeEventId = eventRuntimeId,
            DisplayName = "UI " + kind,
            FmodEventPath = eventPath,
            FmodEventGuid = guid,
            BusId = "audio.bus.ui",
            Kind = "Event",
            Is3D = false,
            IsLoop = false,
            Metadata = new Dictionary<string, string>
            {
                ["uiFeedback"] = kind,
                ["runtimeControlled"] = "true"
            }
        });
        manifest.Cues.Add(new AudioCueAuthoringDefinition
        {
            AudioCueId = cueId,
            RuntimeCueId = cueRuntimeId,
            DisplayName = "UI " + kind,
            Purpose = "UI feedback",
            Context = "Player-facing UI",
            EventDefinitionId = eventId,
            PlayMode = "OneShot",
            MissingPolicy = "Mute",
            Lifecycle = AudioCueLifecycles.Draft
        });
        snapshot.Events.Add(new AuthoringFmodAudioLibraryEvent
        {
            Path = eventPath,
            Guid = guid,
            Is3D = false,
            IsLoop = false,
            Banks = new List<string> { "Master" }
        });
    }

    private static bool HasCode(
        AudioSemanticManifestValidationReport report,
        string code)
    {
        return report.Diagnostics.Exists(item => item.Code == code);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

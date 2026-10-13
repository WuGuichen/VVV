using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    private static void AudioAuthoringManifestV2_MigratesAndCompilesSharedEvents()
    {
        string dottedEventFolder = MxFramework.Authoring.Cli.EditorServer.SafeAudioProjectSegment("audio.event.mxframework.hit");
        Require(dottedEventFolder.StartsWith("audio_event_mxframework_hit__", StringComparison.Ordinal),
            "stable dotted Event Definition ids should map to safe deterministic adoption folders.");
        Require(dottedEventFolder != MxFramework.Authoring.Cli.EditorServer.SafeAudioProjectSegment("audio_event_mxframework_hit"),
            "normalized Event Definition folder names should retain collision-resistant identity.");
        bool unsafeEventIdRejected = false;
        try { MxFramework.Authoring.Cli.EditorServer.SafeAudioProjectSegment("audio.event/escape"); }
        catch (ArgumentException) { unsafeEventIdRejected = true; }
        Require(unsafeEventIdRejected, "adoption folders must reject path separators.");

        var snapshot = new AuthoringFmodAudioLibrarySnapshotDocument { CacheValid = true };
        snapshot.Events.Add(new AuthoringFmodAudioLibraryEvent
        {
            Path = "event:/Shared/Hit",
            Guid = "{shared}",
            Is3D = true,
            Banks = new List<string> { "Master" }
        });
        var legacy = new AudioSemanticManifestDocument { SchemaVersion = 1 };
        legacy.Entries.Add(new AudioSemanticManifestEntry
        {
            AudioCueId = "sfx.hit.a",
            AudioEventDefinitionId = "audio.event.shared.hit",
            FmodEventPath = "event:/Shared/Hit",
            FmodEventGuid = "{shared}",
            DisplayName = "Hit A",
            Purpose = "test",
            Context = "test"
        });

        AudioSemanticManifestDocument migrated = AudioAuthoringManifestV2.Upgrade(legacy, snapshot);
        Require(migrated.SchemaVersion == 2 && migrated.Entries == null, "v1 audio entries should migrate to v2-only output.");
        Require(migrated.Cues.Count == 1 && migrated.EventDefinitions.Count == 1, "v1 migration should create one Cue and one Event Definition.");
        int firstCueId = migrated.Cues[0].RuntimeCueId;
        migrated.Cues[0].Lifecycle = AudioCueLifecycles.Active;
        migrated.Cues[0].AuditionEvidence = ApprovedEvidence(migrated, migrated.Cues[0], snapshot, "preview-a");
        var second = new AudioCueAuthoringDefinition
        {
            AudioCueId = "sfx.hit.b",
            RuntimeCueId = AudioAuthoringManifestV2.AllocateCueId(migrated),
            DisplayName = "Hit B",
            Purpose = "test",
            Context = "test",
            EventDefinitionId = migrated.EventDefinitions[0].Id,
            Lifecycle = AudioCueLifecycles.Active
        };
        second.AuditionEvidence = ApprovedEvidence(migrated, second, snapshot, "preview-b");
        migrated.Cues.Add(second);
        migrated.EventDefinitions.Add(new AudioEventAuthoringDefinition
        {
            Id = "audio.snapshot.pause",
            RuntimeEventId = AudioAuthoringManifestV2.AllocateEventId(migrated),
            FmodEventPath = "snapshot:/Pause",
            FmodEventGuid = "{pause}",
            BusId = migrated.EventDefinitions[0].BusId,
            Kind = "Snapshot",
            IsLoop = true,
            Metadata = new Dictionary<string, string> { { "runtimeControlled", "true" } }
        });

        AudioRuntimeCatalogDocument catalog = AudioAuthoringManifestV2.CompileRuntimeCatalog(migrated, "manifest", "snapshot");
        Require(catalog.Cues.Count == 2 && catalog.Events.Count == 2,
            "multiple Cues should share one Event while explicitly controlled Events compile without fake Cues.");
        Require(catalog.Cues[0].EventId == catalog.Cues[1].EventId, "shared Event runtime id should be preserved.");
        Require(firstCueId != second.RuntimeCueId, "Cue runtime ids must be monotonic and independent.");
    }

    private static void AudioAuthoringManifestV2_InvalidatesChangedBindingEvidence()
    {
        var snapshot = new AuthoringFmodAudioLibrarySnapshotDocument { CacheValid = true };
        snapshot.Events.Add(new AuthoringFmodAudioLibraryEvent { Path = "event:/A", Guid = "{a}", Banks = new List<string> { "Master" } });
        snapshot.Events.Add(new AuthoringFmodAudioLibraryEvent { Path = "event:/B", Guid = "{b}", Banks = new List<string> { "Master" } });
        var manifest = new AudioSemanticManifestDocument { SchemaVersion = 2 };
        manifest.Buses.Add(new AudioBusAuthoringDefinition { Id = "audio.bus.master", RuntimeBusId = 500001, FmodBusPath = "bus:/" });
        manifest.EventDefinitions.Add(new AudioEventAuthoringDefinition { Id = "event.a", RuntimeEventId = 500001, FmodEventPath = "event:/A", FmodEventGuid = "{a}", BusId = "audio.bus.master" });
        var cue = new AudioCueAuthoringDefinition { AudioCueId = "sfx.a", RuntimeCueId = 500001, DisplayName = "A", Purpose = "test", Context = "test", EventDefinitionId = "event.a", Lifecycle = AudioCueLifecycles.Active };
        cue.AuditionEvidence = ApprovedEvidence(manifest, cue, snapshot, "preview-a");
        manifest.Cues.Add(cue);
        Require(AudioAuthoringManifestV2.GetCueStatus(manifest, cue, snapshot) == AudioSemanticEntryStatuses.Ready, "current audition evidence should make an Active Cue ready.");

        manifest.EventDefinitions[0].FmodEventPath = "event:/B";
        manifest.EventDefinitions[0].FmodEventGuid = "{b}";
        AudioAuthoringManifestV2.Normalize(manifest, snapshot);
        Require(cue.AuditionEvidence.Status == AudioSemanticAuditionStatuses.Needed, "binding changes should invalidate approval evidence.");
        Require(AudioAuthoringManifestV2.GetCueStatus(manifest, cue, snapshot) == AudioSemanticEntryStatuses.Ready, "changed optional audition evidence must not downgrade a technically ready Cue.");
        AudioSemanticManifestValidationReport validation = AudioAuthoringManifestV2.Validate(manifest, snapshot);
        Require(!validation.HasErrors, "optional audition review must not block runtime synchronization or publication.");
        Require(validation.Diagnostics.Exists(diagnostic =>
                diagnostic.Code == AuthoringResourceDiagnosticCodes.AudioSemanticAuditionRecommended &&
                diagnostic.Severity == CharacterAuthoringValidationSeverity.Info),
            "stale audition evidence should remain visible as a non-blocking recommendation.");
    }

    private static void AudioGenerationBriefBuilder_DerivesFoleyAndLayeredActionContracts()
    {
        AudioGenerationBriefV1 footstep = AudioGenerationBriefBuilder.Build(
            new AudioCueAuthoringDefinition
            {
                AudioCueId = "sfx.body_contact.sole.rough",
                DisplayName = "Rough Stone Footstep",
                Purpose = "player foot contact",
                Context = "third-person gameplay",
                Tags = new List<string> { "footstep", "stone", "rough" }
            },
            new AudioEventAuthoringDefinition { Id = "event.footstep", Is3D = true });
        Require(footstep.Category == "Footstep", "footstep semantics should derive a Foley sound set.");
        Require(footstep.Layers.Count == 1 && footstep.Layers[0].Takes == 12, "footsteps should generate twelve body candidates.");
        Require(footstep.AdoptionCountPerLayer == 6, "footsteps should require six adopted Foley variations.");
        Require(footstep.CollisionProfile != null
            && footstep.CollisionProfile.Kind == "Footstep"
            && footstep.CollisionProfile.Impactor.MaterialId == "cloth_leather"
            && footstep.CollisionProfile.Receiver.Surface == "rough",
            "footsteps should derive role-aware sole and receiving-surface facts.");
        Require(footstep.PositivePrompt.Contains("dry", StringComparison.OrdinalIgnoreCase), "derived brief should require dry game-ready output.");
        Require(footstep.NegativePrompt.Contains("music", StringComparison.OrdinalIgnoreCase), "derived brief should exclude music and speech.");
        Require(footstep.DurationMode == "NaturalDecay" && footstep.MaxDurationSeconds == 0.4f
            && footstep.Layers[0].MaxDurationSeconds == 0.4f,
            "new rule-derived briefs should use a natural-decay maximum window instead of a fixed output length.");

        AudioGenerationBriefV1 weapon = AudioGenerationBriefBuilder.Build(
            new AudioCueAuthoringDefinition
            {
                AudioCueId = "sfx.weapon_hit.blocked",
                DisplayName = "Weapon Hit Blocked",
                Purpose = "heavy weapon parry",
                Tags = new List<string> { "weapon", "hit", "blocked", "metal" }
            },
            new AudioEventAuthoringDefinition { Id = "event.weapon", Is3D = true });
        Require(weapon.Category == "WeaponHit", "weapon semantics should derive a WeaponHit brief.");
        Require(weapon.Layers.Count == 3, "Action briefs should derive transient/body/tail layers.");
        Require(weapon.AdoptionCountPerLayer == 4, "layered Action briefs should require four adopted takes per layer.");
        Require(weapon.Layers[0].Name == "transient" && weapon.Layers[1].Name == "body" && weapon.Layers[2].Name == "tail", "Action layer ordering should be stable.");
        Require(weapon.CollisionProfile.Receiver.MaterialId == "metal" && weapon.CollisionProfile.Interaction.Mode == "block",
            "blocked weapon hits should derive a metal receiver and block interaction.");

        AudioGenerationBriefV1 explosion = AudioGenerationBriefBuilder.Build(
            new AudioCueAuthoringDefinition { AudioCueId = "sfx.fireball_explosion", DisplayName = "Fireball Explosion", Tags = new List<string> { "fireball", "explosion" } },
            new AudioEventAuthoringDefinition { Id = "event.explosion", Is3D = true });
        Require(explosion.Category == "Explosion" && explosion.CollisionProfile == null,
            "fireball explosions should remain outside the contact-sound collision model.");
        AudioGenerationRecipeV2 explosionRecipe = AudioGenerationBriefBuilder.BuildRecipe(
            new AudioCueAuthoringDefinition { AudioCueId = "sfx.fireball_explosion", DisplayName = "Fireball Explosion", Tags = new List<string> { "fireball", "explosion" } },
            new AudioEventAuthoringDefinition { Id = "event.explosion", Is3D = true }, "binding");
        Require(explosionRecipe.Layers.TrueForAll(layer => !layer.Prompt.Contains("both bodies", StringComparison.OrdinalIgnoreCase)
            && !layer.Prompt.Contains("receiver resonance", StringComparison.OrdinalIgnoreCase)),
            "generic explosion layer prompts should not inherit contact-sound body terminology.");

        AudioGenerationRecipeV2 sweepRecipe = AudioGenerationBriefBuilder.BuildRecipe(
            new AudioCueAuthoringDefinition
            {
                AudioCueId = "sfx.air_displacement.metal",
                DisplayName = "Metal Air Displacement",
                Tags = new List<string> { "sweep", "whoosh" }
            },
            new AudioEventAuthoringDefinition { Id = "event.sweep", Is3D = true }, "binding");
        Require(sweepRecipe.Category == "AirDisplacement" && sweepRecipe.CollisionProfile == null,
            "air displacement should remain a non-contact generation recipe instead of being normalized as WeaponHit.");
        AudioGenerationBriefBuilder.NormalizeCollisionProfile(sweepRecipe);
        Require(sweepRecipe.Category == "AirDisplacement" && sweepRecipe.CollisionProfile == null,
            "air displacement recipe normalization should preserve its dedicated category.");
    }

    private static void AudioGenerationRecipeV2_PersistsAndBoundsHistory()
    {
        AudioGenerationRecipeV2 recipe = AudioGenerationBriefBuilder.BuildRecipe(
            new AudioCueAuthoringDefinition { AudioCueId = "sfx.weapon", DisplayName = "Weapon Hit", Purpose = "沉重长剑击中石甲", Tags = new List<string> { "weapon", "stone" } },
            new AudioEventAuthoringDefinition { Id = "event.weapon", Is3D = true }, "binding");
        Require(recipe.IntentZh.Contains("长剑"), "recipe should persist the Chinese authoring intent.");
        Require(recipe.CollisionProfile != null && recipe.PositivePrompt.Contains("Impactor:", StringComparison.Ordinal),
            "contact recipes should persist both bodies and project them into the provider prompt.");
        Require(recipe.Layers.Count == 3 && recipe.Layers[0].Prompt != recipe.Layers[2].Prompt, "layered recipes should derive distinct prompts.");
        Require(recipe.Layers.TrueForAll(item => item.Takes == 6 && item.AdoptionTakes == 4), "layered recipes should distinguish generated and adopted take counts.");
        Require(recipe.DurationMode == "NaturalDecay" && recipe.NegativePrompt.Contains("room impulse response", StringComparison.OrdinalIgnoreCase),
            "new recipes should persist the dry natural-decay policy and explicit spatial-tail exclusions.");
        var document = new MxFramework.Authoring.Cli.EditorServer.AudioGenerationManifestDocument { Format = "mx.audioGenerationManifest.v1", SchemaVersion = "1.0" };
        document.Recipes.Add(recipe);
        document.Recipes.Add(new AudioGenerationRecipeV2 { AudioCueId = recipe.AudioCueId });
        for (int i = 0; i < 12; i++) recipe.History.Add(new AudioGenerationRecipeRevisionV2 { Revision = i + 1 });
        document = MxFramework.Authoring.Cli.EditorServer.UpgradeGenerationManifest(document);
        Require(document.Format == "mx.audioGenerationManifest.v3" && document.SchemaVersion == "3.0", "generation manifest should migrate to v3-only output.");
        Require(document.Recipes.Count == 1, "generation recipes should be unique per Cue.");
        Require(document.Recipes[0].History.Count == 10 && document.Recipes[0].History[0].Revision == 3, "recipe history should retain the latest ten revisions.");
        document.Recipes[0].Layers[0].AdoptionTakes = 0;
        document = MxFramework.Authoring.Cli.EditorServer.UpgradeGenerationManifest(document);
        Require(document.Recipes[0].Layers[0].AdoptionTakes == 4 && document.Recipes[0].VariationCount == 18, "legacy recipes should migrate per-layer adoption counts and derive total variations.");
        document.Entries.Add(new MxFramework.Authoring.Cli.EditorServer.AudioGenerationManifestEntry { AudioCueId = "sfx.weapon", EventDefinitionId = "event.weapon", Layer = "body", Take = 1, ProjectRelativePath = "Assets/Audio/test.wav", Status = "Adopted", Provenance = new AudioGenerationProvenance { FinalFileHash = new string('a', 64) } });
        document = MxFramework.Authoring.Cli.EditorServer.UpgradeGenerationManifest(document);
        Require(document.AdoptionSets.Count == 1 && document.EventStates[0].ActiveSetId == document.AdoptionSets[0].SetId, "v2 adopted entries should migrate to an immutable v3 set.");
        document.Recipes[0].LayerTemplate = "BodyOnly";
        document.Recipes[0].Layers.ForEach(item => item.Enabled = true);
        document = MxFramework.Authoring.Cli.EditorServer.UpgradeGenerationManifest(document);
        Require(document.Recipes[0].Layers.Find(item => item.Name == "body").Enabled
            && !document.Recipes[0].Layers.Find(item => item.Name == "transient").Enabled
            && !document.Recipes[0].Layers.Find(item => item.Name == "tail").Enabled
            && document.Recipes[0].VariationCount == document.Recipes[0].Layers.Find(item => item.Name == "body").Takes,
            "BodyOnly should remain authoritative when stale legacy enabled flags still contain three layers.");
        var legacyContact = new AudioGenerationRecipeV2 { AudioCueId = "sfx.object_collision.elastic", Category = "ObjectCollision", Material = "rubber", Weight = "heavy", Intensity = "strong" };
        document.Recipes.Add(legacyContact);
        document = MxFramework.Authoring.Cli.EditorServer.UpgradeGenerationManifest(document);
        Require(legacyContact.CollisionProfile != null
            && legacyContact.CollisionProfile.Receiver.MaterialId == "rubber"
            && legacyContact.CollisionProfile.Impactor.MassClass == "heavy"
            && legacyContact.CollisionProfile.Interaction.Intensity == "strong",
            "legacy single-material contact recipes should migrate into the structured collision profile.");
        Require(legacyContact.DurationMode == "LegacyFixed" && legacyContact.MaxDurationSeconds == legacyContact.TargetDurationSeconds,
            "legacy recipes without a duration policy must retain fixed-duration behavior during migration.");
        Require(MxFramework.Authoring.Cli.EditorServer.IsSuccessfulFmodStudioResponse("log(): connected\0out(): true\r\n\0"),
            "FMOD Script Server should accept only an explicit true result.");
        Require(!MxFramework.Authoring.Cli.EditorServer.IsSuccessfulFmodStudioResponse("error(): project script failed\0out(): true\0"),
            "FMOD Script Server errors must not be treated as a successful apply.");
        var fmodStart = MxFramework.Authoring.Cli.EditorServer.CreateFmodStudioProcessStartInfo(
            "/Applications/FMOD Studio.app/Contents/MacOS/fmodstudio",
            new[] { "-build", "project.fspro" },
            "/tmp/mx audio");
        Require(fmodStart.Environment["FMODSTUDIOCMDLINE"] == "1", "FMOD fallback should launch the real binary in command-line mode.");
        Require(fmodStart.ArgumentList.Count == 4 && fmodStart.ArgumentList[0] == "-working-dir" && fmodStart.ArgumentList[1] == "/tmp/mx audio",
            "FMOD fallback should pass the working directory without shell interpolation.");
        string applicationJson = JsonSerializer.Serialize(
            new MxFramework.Authoring.Cli.EditorServer.AudioApplicationStatus { stage = "Ready", terminal = true },
            JsonOptions);
        Require(applicationJson.Contains("\"terminal\": true", StringComparison.Ordinal)
            && !applicationJson.Contains("\"Terminal\"", StringComparison.Ordinal),
            "audio application responses should serialize one unambiguous terminal field.");
        string fmodAdoptionScript = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "FMOD", "MxFrameworkAudioDemo", "MxFrameworkAudioDemo", "Scripts", "MxFrameworkAudioAdoption.js"));
        Require(fmodAdoptionScript.Contains("byGuid.id !== byPath.id", StringComparison.Ordinal)
            && !fmodAdoptionScript.Contains("byGuid !== byPath", StringComparison.Ordinal),
            "FMOD lookup proxies should be compared by persistent Studio object id, not JavaScript reference identity.");
        Require(fmodAdoptionScript.Contains("Number(audioFile.length)", StringComparison.Ordinal)
            && fmodAdoptionScript.Contains("\"SingleSound\", start, audioDuration(audioFile)", StringComparison.Ordinal)
            && !fmodAdoptionScript.Contains("\"SingleSound\", start, 30", StringComparison.Ordinal)
            && !fmodAdoptionScript.Contains("\"MultiSound\", start, 30", StringComparison.Ordinal),
            "FMOD adoption instruments must use imported source duration instead of a fixed 30-second timeline region.");
        string fmodScriptCommand = MxFramework.Authoring.Cli.EditorServer.CreateFmodStudioScriptCommand(fmodAdoptionScript);
        Require(fmodScriptCommand.Contains("MxFrameworkAudioAdoptionScriptHash", StringComparison.Ordinal)
            && fmodScriptCommand.Contains("eval(", StringComparison.Ordinal),
            "FMOD Script Server commands should hot-reload changed project scripts by content hash.");
        string liveFmodScriptCommand = MxFramework.Authoring.Cli.EditorServer.CreateFmodStudioScriptCommand(
            fmodAdoptionScript,
            "{\"requestId\":\"live-request\"}");
        Require(liveFmodScriptCommand.Contains("MxFrameworkApplyAudioAdoptionRequestAndBuild", StringComparison.Ordinal)
            && liveFmodScriptCommand.Contains("\"requestId\":\"live-request\"", StringComparison.Ordinal)
            && fmodAdoptionScript.Contains("MxFrameworkLiveAudioAdoptionStatus", StringComparison.Ordinal),
            "live FMOD application should inline the adoption request and expose build state without Script Server file access.");

        string historyRoot = Path.Combine(Path.GetTempPath(), "mx-audio-history-delete-" + Guid.NewGuid().ToString("N"));
        try
        {
            string historyManifestPath = Path.Combine(historyRoot, "Assets", "Config", "MxFramework", "Audio", "audio_generation_manifest.json");
            string historySourcePath = Path.Combine(historyRoot, "Assets", "Audio", "MxFramework", "Generated", "event", "sets", "old-set", "body", "01.wav");
            Directory.CreateDirectory(Path.GetDirectoryName(historyManifestPath));
            Directory.CreateDirectory(Path.GetDirectoryName(historySourcePath));
            File.WriteAllBytes(historySourcePath, new byte[] { 1, 2, 3 });
            var historyDocument = new MxFramework.Authoring.Cli.EditorServer.AudioGenerationManifestDocument();
            historyDocument.AdoptionSets.Add(new AudioAdoptionSetV3 { SetId = "active-set", EventDefinitionId = "event", Status = "Applied", SetHash = "active-hash" });
            historyDocument.AdoptionSets.Add(new AudioAdoptionSetV3 { SetId = "baseline-set", EventDefinitionId = "event", SourceType = "OriginalBaseline", Status = "Superseded", SetHash = "baseline-hash" });
            historyDocument.AdoptionSets.Add(new AudioAdoptionSetV3
            {
                SetId = "old-set", EventDefinitionId = "event", Status = "Superseded", SetHash = "old-hash",
                Sources = new List<FmodAudioAdoptionSource> { new FmodAudioAdoptionSource { ProjectRelativePath = "Assets/Audio/MxFramework/Generated/event/sets/old-set/body/01.wav" } }
            });
            historyDocument.EventStates.Add(new AudioAdoptionEventStateV3 { EventDefinitionId = "event", ActiveSetId = "active-set" });
            File.WriteAllText(historyManifestPath, JsonSerializer.Serialize(historyDocument, JsonOptions));

            MxFramework.Authoring.Cli.EditorServer.AudioHistoryDeleteResult deleted = MxFramework.Authoring.Cli.EditorServer.DeleteAudioHistorySet(historyRoot, "old-set", "old-hash", string.Empty, JsonOptions);
            Require(deleted.success && deleted.deletedFiles == 1 && !File.Exists(historySourcePath), "deleting a superseded history set should remove its manifest record and unique generated source.");
            MxFramework.Authoring.Cli.EditorServer.AudioHistoryDeleteResult activeRejected = MxFramework.Authoring.Cli.EditorServer.DeleteAudioHistorySet(historyRoot, "active-set", "active-hash", string.Empty, JsonOptions);
            Require(!activeRejected.success && activeRejected.code == "AUTH_AUDIO_HISTORY_ACTIVE_SET_PROTECTED", "the active audio set must be protected from history deletion.");
            MxFramework.Authoring.Cli.EditorServer.AudioHistoryDeleteResult baselineRejected = MxFramework.Authoring.Cli.EditorServer.DeleteAudioHistorySet(historyRoot, "baseline-set", "baseline-hash", string.Empty, JsonOptions);
            Require(!baselineRejected.success && baselineRejected.code == "AUTH_AUDIO_HISTORY_BASELINE_PROTECTED", "the original FMOD baseline must be protected from history deletion.");
        }
        finally { if (Directory.Exists(historyRoot)) Directory.Delete(historyRoot, true); }

        AudioGenerationBriefV1 validationBrief = AudioGenerationBriefBuilder.Build(
            new AudioCueAuthoringDefinition { AudioCueId = "sfx.weapon", DisplayName = "Weapon Hit", Tags = new List<string> { "weapon" } },
            new AudioEventAuthoringDefinition { Id = "event.weapon", Is3D = true });
        MxFramework.Authoring.Cli.EditorServer.ValidateGenerationBrief(validationBrief);
        validationBrief.CollisionProfile.Impactor.MassClass = "planetary";
        bool invalidCollisionProfileRejected = false;
        try { MxFramework.Authoring.Cli.EditorServer.ValidateGenerationBrief(validationBrief); }
        catch (ArgumentException) { invalidCollisionProfileRejected = true; }
        Require(invalidCollisionProfileRejected, "unknown collision profile option IDs should be rejected.");
        validationBrief.CollisionProfile.Impactor.MassClass = "medium";
        validationBrief.Layers[0].AdoptionTakes = 7;
        bool invalidAdoptionRejected = false;
        try { MxFramework.Authoring.Cli.EditorServer.ValidateGenerationBrief(validationBrief); }
        catch (ArgumentException) { invalidAdoptionRejected = true; }
        Require(invalidAdoptionRejected, "adoption count greater than generated count should be rejected.");
        validationBrief.Layers[0].AdoptionTakes = 4;
        validationBrief.Layers.AddRange(new[]
        {
            new AudioGenerationLayerRequest { Name = "extra-a", Takes = 10, AdoptionTakes = 1 },
            new AudioGenerationLayerRequest { Name = "extra-b", Takes = 10, AdoptionTakes = 1 }
        });
        bool oversizedJobRejected = false;
        try { MxFramework.Authoring.Cli.EditorServer.ValidateGenerationBrief(validationBrief); }
        catch (ArgumentException) { oversizedJobRejected = true; }
        Require(oversizedJobRejected, "generation jobs above thirty-six candidates should be rejected.");
    }

    private static void EditorServer_AudioWorkbench_EnforcesHashGateAndAtomicPublish()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-audio-workbench-" + Guid.NewGuid().ToString("N"));
        string manifestPath = Path.Combine(root, "Assets", "Config", "MxFramework", "Audio", "audio_semantic_manifest.json");
        string generationManifestPath = Path.Combine(root, "Assets", "Config", "MxFramework", "Audio", "audio_generation_manifest.json");
        string physicalManifestPath = Path.Combine(root, "Assets", "Config", "MxFramework", "Audio", "audio_physical_manifest.json");
        string snapshotPath = Path.Combine(root, "Assets", "MxFrameworkGenerated", "Audio", "fmod_audio_library.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath));
            var snapshot = new AuthoringFmodAudioLibrarySnapshotDocument { CacheValid = true, GeneratedAtUtc = "2026-07-10T00:00:00Z" };
            snapshot.Events.Add(new AuthoringFmodAudioLibraryEvent { Path = "event:/Ready", Guid = "{ready}", Banks = new List<string> { "Master" } });
            var manifest = new AudioSemanticManifestDocument { SchemaVersion = 2 };
            manifest.Buses.Add(new AudioBusAuthoringDefinition { Id = "audio.bus.master", RuntimeBusId = 500001, FmodBusPath = "bus:/" });
            manifest.EventDefinitions.Add(new AudioEventAuthoringDefinition { Id = "event.ready", RuntimeEventId = 500001, FmodEventPath = "event:/Ready", FmodEventGuid = "{ready}", BusId = "audio.bus.master" });
            var cue = new AudioCueAuthoringDefinition { AudioCueId = "sfx.ready", RuntimeCueId = 500001, DisplayName = "Ready", Purpose = "test", Context = "test", EventDefinitionId = "event.ready", Lifecycle = AudioCueLifecycles.Active };
            cue.AuditionEvidence = ApprovedEvidence(manifest, cue, snapshot, "preview-ready");
            manifest.Cues.Add(cue);
            manifest.Cues.Add(new AudioCueAuthoringDefinition
            {
                AudioCueId = "sfx.draft",
                RuntimeCueId = 500002,
                DisplayName = "Draft",
                Purpose = "test",
                Context = "test",
                EventDefinitionId = "event.ready",
                Lifecycle = AudioCueLifecycles.Draft
            });
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
            string bankPath = Path.Combine(root, "Assets", "StreamingAssets", "Master.bank");
            Directory.CreateDirectory(Path.GetDirectoryName(bankPath));
            File.WriteAllBytes(bankPath, new byte[] { 1, 2, 3, 4 });
            string buildBankPath = Path.Combine(root, "FMOD", "MxFrameworkAudioDemo", "MxFrameworkAudioDemo", "Build", "Desktop", "Master.bank");
            Directory.CreateDirectory(Path.GetDirectoryName(buildBankPath));
            File.Copy(bankPath, buildBankPath);
            string bankFileHash = CharacterPackageHashUtility.ComputeFileSha256(bankPath).Substring(7);
            string bankSetHash = CharacterPackageHashUtility.ComputeTextSha256("Master.bank:" + bankFileHash + "\n");
            string readyBindingHash = AudioAuthoringManifestV2.ComputeBindingHash(cue, manifest.EventDefinitions[0]);
            cue.AuditionEvidence.AppliedSetId = "set.ready";
            cue.AuditionEvidence.BankHash = bankSetHash;
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
            var generationManifest = new MxFramework.Authoring.Cli.EditorServer.AudioGenerationManifestDocument();
            generationManifest.AdoptionSets.Add(new AudioAdoptionSetV3
            {
                SetId = "set.ready",
                AudioCueId = cue.AudioCueId,
                EventDefinitionId = cue.EventDefinitionId,
                BindingHash = readyBindingHash,
                Status = "Applied",
                BankHash = bankSetHash,
                SnapshotHash = cue.AuditionEvidence.SnapshotHash
            });
            generationManifest.EventStates.Add(new AudioAdoptionEventStateV3
            {
                EventDefinitionId = cue.EventDefinitionId,
                ActiveSetId = "set.ready"
            });
            File.WriteAllText(generationManifestPath, JsonSerializer.Serialize(generationManifest, JsonOptions));
            File.WriteAllText(physicalManifestPath, JsonSerializer.Serialize(
                new PhysicalAudioAuthoringManifestDocument(),
                JsonOptions));
            File.WriteAllText(snapshotPath, JsonSerializer.Serialize(snapshot, JsonOptions));
            string statusPath = Path.Combine(root, "Temp", "MxFrameworkAuthoring", "Audio", "status", "preview-ready.json");
            Directory.CreateDirectory(Path.GetDirectoryName(statusPath));
            File.WriteAllText(statusPath, JsonSerializer.Serialize(new
            {
                requestId = "preview-ready",
                status = "succeeded",
                audioCueId = cue.AudioCueId,
                eventBindingHash = cue.AuditionEvidence.EventBindingHash,
                snapshotHash = cue.AuditionEvidence.SnapshotHash
            }, JsonOptions));

            var conflict = MxFramework.Authoring.Cli.EditorServer.SaveAudioWorkbenchDraft(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.AudioWorkbenchManifestRequest { expectedHash = "sha256:bad", manifest = manifest },
                JsonOptions);
            Require(!conflict.success && conflict.httpStatus == 409, "Audio Workbench draft save should reject stale expectedHash.");

            string currentHash = CharacterPackageHashUtility.ComputeFileSha256(manifestPath);
            AudioSemanticManifestDocument deletionDraft = JsonSerializer.Deserialize<AudioSemanticManifestDocument>(JsonSerializer.Serialize(manifest, JsonOptions), JsonOptions);
            deletionDraft.Cues.Clear();
            var deletionBlocked = MxFramework.Authoring.Cli.EditorServer.SaveAudioWorkbenchDraft(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.AudioWorkbenchManifestRequest { expectedHash = currentHash, manifest = deletionDraft },
                JsonOptions);
            Require(!deletionBlocked.success && deletionBlocked.code == "AUTH_AUDIO_CUE_DELETE_BLOCKED", "non-Archived Cue deletion should be blocked server-side.");

            var publish = MxFramework.Authoring.Cli.EditorServer.PublishAudioWorkbench(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.AudioWorkbenchPublishRequest
                {
                    expectedHash = currentHash,
                    expectedGenerationHash = CharacterPackageHashUtility.ComputeFileSha256(generationManifestPath),
                    expectedPhysicalHash = CharacterPackageHashUtility.ComputeFileSha256(physicalManifestPath)
                },
                JsonOptions);
            Require(publish.success, "ready Active Cue should publish a runtime catalog: " + publish.code + " " + publish.message);
            string generatedPath = Path.Combine(root, "Assets", "MxFrameworkGenerated", "Audio", "audio_runtime_catalog.json");
            string mirrorPath = Path.Combine(root, "Assets", "StreamingAssets", "MxFramework", "Audio", "audio_runtime_catalog.json");
            Require(File.Exists(generatedPath) && File.Exists(mirrorPath), "publish should atomically produce Generated and Player mirror catalogs.");
            Require(File.ReadAllText(generatedPath) == File.ReadAllText(mirrorPath), "runtime catalog mirrors should be identical.");
            Require(publish.catalog.Cues.Count == 1, "formal publish should include only approved Active Cues.");

            var developmentPublish = MxFramework.Authoring.Cli.EditorServer.PublishAudioWorkbench(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.AudioWorkbenchPublishRequest
                {
                    expectedHash = currentHash,
                    expectedGenerationHash = CharacterPackageHashUtility.ComputeFileSha256(generationManifestPath),
                    expectedPhysicalHash = CharacterPackageHashUtility.ComputeFileSha256(physicalManifestPath),
                    developmentPublishAll = true
                },
                JsonOptions);
            Require(developmentPublish.success, "development publish should publish all technically resolvable non-Archived Cues.");
            Require(developmentPublish.catalog.Cues.Count == 2, "development publish should include both Active and Draft Cues.");
            MxFramework.Authoring.Cli.EditorServer.AudioGenerationManifestDocument publishedGeneration =
                JsonSerializer.Deserialize<MxFramework.Authoring.Cli.EditorServer.AudioGenerationManifestDocument>(
                    File.ReadAllText(generationManifestPath),
                    JsonOptions);
            Require(
                publishedGeneration.EventStates.Single().PublishedSetId == "set.ready"
                && developmentPublish.syncedSetCount == 1
                && developmentPublish.outOfSyncSetCount == 0,
                "development one-click sync should record the Applied set only when its Bank hash matches the game Bank.");
            AudioSemanticManifestDocument persistedManifest = JsonSerializer.Deserialize<AudioSemanticManifestDocument>(File.ReadAllText(manifestPath), JsonOptions);
            Require(
                persistedManifest.Cues.Single(item => item.AudioCueId == "sfx.draft").Lifecycle == AudioCueLifecycles.Draft,
                "development publish must not forge or persist authoring approval state.");

            var bulkApproval = MxFramework.Authoring.Cli.EditorServer.BulkApproveAllAudioCues(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.AudioWorkbenchBulkApprovalRequest
                {
                    expectedHash = currentHash
                },
                JsonOptions);
            Require(bulkApproval.success, "bulk operator approval should approve all technically resolvable non-Archived Cues.");
            Require(
                bulkApproval.manifest.Cues.All(item =>
                    item.Lifecycle == AudioCueLifecycles.Active
                    && item.AuditionEvidence.Status == AudioSemanticAuditionStatuses.Approved
                    && item.AuditionEvidence.ReviewMode == AudioCueAuditionReviewModes.BulkOperatorApproval),
                "bulk operator approval should activate all Cues and retain an explicit bulk approval mode.");
            currentHash = bulkApproval.currentHash;

            File.Delete(generatedPath);
            File.Delete(mirrorPath);
            Directory.Delete(Path.GetDirectoryName(mirrorPath), recursive: true);
            Directory.CreateDirectory(Path.Combine(root, "Assets", "StreamingAssets", "MxFramework"));
            File.WriteAllText(Path.Combine(root, "Assets", "StreamingAssets", "MxFramework", "Audio"), "blocks directory creation");
            var failedPublish = MxFramework.Authoring.Cli.EditorServer.PublishAudioWorkbench(
                root,
                string.Empty,
                new MxFramework.Authoring.Cli.EditorServer.AudioWorkbenchPublishRequest
                {
                    expectedHash = currentHash,
                    expectedGenerationHash = CharacterPackageHashUtility.ComputeFileSha256(generationManifestPath),
                    expectedPhysicalHash = CharacterPackageHashUtility.ComputeFileSha256(physicalManifestPath)
                },
                JsonOptions);
            Require(!failedPublish.success && failedPublish.httpStatus == 500, "publication write failure should return a tool error.");
            Require(!File.Exists(generatedPath), "failed multi-file publication must not leave a partially committed Generated catalog.");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void EditorServer_AudioRuntimeActivation_AggregatesAuthoritativeReadiness()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-audio-activation-" + Guid.NewGuid().ToString("N"));
        try
        {
            var snapshot = new AuthoringFmodAudioLibrarySnapshotDocument
            {
                CacheValid = true,
                GeneratedAtUtc = "2026-07-18T00:00:00Z"
            };
            snapshot.Events.Add(new AuthoringFmodAudioLibraryEvent
            {
                Path = "event:/Activation",
                Guid = "{activation}",
                Banks = new List<string> { "Master" }
            });
            var semantic = new AudioSemanticManifestDocument { SchemaVersion = 2 };
            semantic.Buses.Add(new AudioBusAuthoringDefinition
            {
                Id = "audio.bus.master",
                RuntimeBusId = 500001,
                FmodBusPath = "bus:/"
            });
            semantic.EventDefinitions.Add(new AudioEventAuthoringDefinition
            {
                Id = "event.activation",
                RuntimeEventId = 500001,
                FmodEventPath = "event:/Activation",
                FmodEventGuid = "{activation}",
                BusId = "audio.bus.master"
            });
            var activeCue = new AudioCueAuthoringDefinition
            {
                AudioCueId = "sfx.activation",
                RuntimeCueId = 500001,
                DisplayName = "Activation",
                Purpose = "test",
                Context = "test",
                EventDefinitionId = "event.activation",
                Lifecycle = AudioCueLifecycles.Active
            };
            var draftCue = new AudioCueAuthoringDefinition
            {
                AudioCueId = "sfx.draft",
                RuntimeCueId = 500002,
                DisplayName = "Draft",
                Purpose = "test",
                Context = "test",
                EventDefinitionId = "event.activation",
                Lifecycle = AudioCueLifecycles.Draft
            };
            semantic.Cues.Add(activeCue);
            semantic.Cues.Add(draftCue);

            var activeBasis = new PhysicalAudioBasisSoundAuthoring
            {
                Id = "basis.activation",
                RuntimeBasisId = 530001,
                AudioCueId = activeCue.AudioCueId,
                AdoptionSetId = "set.activation",
                Lifecycle = PhysicalAudioLifecycles.Active
            };
            var draftBasis = new PhysicalAudioBasisSoundAuthoring
            {
                Id = "basis.draft",
                RuntimeBasisId = 530002,
                AudioCueId = draftCue.AudioCueId,
                Lifecycle = PhysicalAudioLifecycles.Draft
            };
            var physicalManifest = new PhysicalAudioAuthoringManifestDocument();
            physicalManifest.BasisSounds.Add(activeBasis);
            physicalManifest.BasisSounds.Add(draftBasis);
            var physicalState = new MxFramework.Authoring.Cli.EditorServer.AudioPhysicalStateResponse
            {
                manifest = physicalManifest,
                validation = new PhysicalAudioValidationReport(),
                coverage = new PhysicalAudioCoverageReport()
            };
            var generation = new MxFramework.Authoring.Cli.EditorServer.AudioGenerationManifestDocument();

            MxFramework.Authoring.Cli.EditorServer.AudioRuntimeActivationState blocked =
                MxFramework.Authoring.Cli.EditorServer.BuildAudioRuntimeActivationState(
                    root,
                    semantic,
                    new AudioSemanticManifestValidationReport(),
                    snapshot,
                    generation,
                    physicalState,
                    JsonOptions);
            Require(blocked.activeCueCount == 1 && blocked.activeBasisCount == 1,
                "runtime activation readiness must exclude Draft Cue and Basis entries.");
            Require(blocked.readyCueCount == 1 && blocked.adoptedBasisCount == 0 && !blocked.publishReady,
                "a technically valid Cue should be ready without audition, while missing applied Basis content still blocks publication readiness.");

            string generatedAudioPath = Path.Combine(root, "Assets", "MxFrameworkGenerated", "Audio", "audio_runtime_catalog.json");
            string generatedPhysicalPath = Path.Combine(root, "Assets", "MxFrameworkGenerated", "Audio", "audio_physical_runtime_catalog.json");
            string playerAudioPath = Path.Combine(root, "Assets", "StreamingAssets", "MxFramework", "Audio", "audio_runtime_catalog.json");
            string playerPhysicalPath = Path.Combine(root, "Assets", "StreamingAssets", "MxFramework", "Audio", "audio_physical_runtime_catalog.json");
            Directory.CreateDirectory(Path.GetDirectoryName(generatedAudioPath));
            Directory.CreateDirectory(Path.GetDirectoryName(playerAudioPath));
            AudioRuntimeCatalogDocument runtimeCatalog = AudioAuthoringManifestV2.CompileRuntimeCatalog(
                semantic,
                "sha256:semantic",
                AudioAuthoringManifestV2.ComputeSnapshotHash(snapshot));
            string runtimeCatalogJson = JsonSerializer.Serialize(runtimeCatalog, JsonOptions);
            File.WriteAllText(generatedAudioPath, runtimeCatalogJson);
            File.WriteAllText(playerAudioPath, runtimeCatalogJson);
            string runtimeCatalogHash = CharacterPackageHashUtility.ComputeFileSha256(generatedAudioPath);
            PhysicalAudioRuntimeCatalogDocument physicalCatalog = PhysicalAudioAuthoringManifest.CompileRuntimeCatalog(
                physicalManifest,
                semantic,
                "sha256:physical",
                runtimeCatalogHash);
            string physicalCatalogJson = JsonSerializer.Serialize(physicalCatalog, JsonOptions);
            File.WriteAllText(generatedPhysicalPath, physicalCatalogJson);
            File.WriteAllText(playerPhysicalPath, physicalCatalogJson);

            MxFramework.Authoring.Cli.EditorServer.AudioRuntimeActivationState runtimeLinked =
                MxFramework.Authoring.Cli.EditorServer.BuildAudioRuntimeActivationState(
                    root,
                    semantic,
                    new AudioSemanticManifestValidationReport(),
                    snapshot,
                    generation,
                    physicalState,
                    JsonOptions);
            Require(runtimeLinked.ready && !runtimeLinked.publishReady,
                "linked Generated and Player catalog pairs should activate runtime loading independently of strict content publication readiness.");

            generation.AdoptionSets.Add(new AudioAdoptionSetV3
            {
                SetId = activeBasis.AdoptionSetId,
                AudioCueId = activeCue.AudioCueId,
                EventDefinitionId = activeCue.EventDefinitionId,
                Status = "Applied"
            });
            activeCue.AuditionEvidence = ApprovedEvidence(semantic, activeCue, snapshot, "preview-activation");
            activeCue.AuditionEvidence.AudioBasisId = activeBasis.Id;
            activeCue.AuditionEvidence.RuntimeBasisId = activeBasis.RuntimeBasisId;
            activeCue.AuditionEvidence.AppliedSetId = activeBasis.AdoptionSetId;

            MxFramework.Authoring.Cli.EditorServer.AudioRuntimeActivationState publishReady =
                MxFramework.Authoring.Cli.EditorServer.BuildAudioRuntimeActivationState(
                    root,
                    semantic,
                    new AudioSemanticManifestValidationReport(),
                    snapshot,
                    generation,
                    physicalState,
                    JsonOptions);
            Require(publishReady.publishReady && publishReady.ready,
                "applied Active content should become publish-ready while linked catalog pairs remain runtime-active; optional approval remains informational.");
            Require(publishReady.readyCueCount == 1
                    && publishReady.adoptedBasisCount == 1
                    && publishReady.approvedBasisCount == 1,
                "runtime activation readiness counts should reflect current Cue, Basis and adoption evidence.");
            Require(publishReady.generatedPair.linked
                    && publishReady.playerPair.linked
                    && publishReady.mirrorsMatch,
                "Generated and Player catalog pairs should remain linked after content readiness changes.");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static AudioCueAuditionEvidence ApprovedEvidence(
        AudioSemanticManifestDocument manifest,
        AudioCueAuthoringDefinition cue,
        AuthoringFmodAudioLibrarySnapshotDocument snapshot,
        string requestId)
    {
        return new AudioCueAuditionEvidence
        {
            Status = AudioSemanticAuditionStatuses.Approved,
            PreviewRequestId = requestId,
            EventBindingHash = AudioAuthoringManifestV2.ComputeBindingHash(cue, AudioAuthoringManifestV2.FindEventDefinition(manifest, cue.EventDefinitionId)),
            SnapshotHash = AudioAuthoringManifestV2.ComputeSnapshotHash(snapshot),
            ReviewedAtUtc = DateTime.UtcNow.ToString("O")
        };
    }
}

using System;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    private static void PhysicalAudioManagedEvent_PlansOnlyControlledBindings()
    {
        string repoRoot = FindRepoRoot();
        string root = Path.Combine(Path.GetTempPath(), "mx-managed-physical-event-" + Guid.NewGuid().ToString("N"));
        try
        {
            string configRoot = Path.Combine(root, "Assets", "Config", "MxFramework", "Audio");
            string generatedRoot = Path.Combine(root, "Assets", "MxFrameworkGenerated", "Audio");
            Directory.CreateDirectory(configRoot);
            Directory.CreateDirectory(generatedRoot);
            string semanticPath = Path.Combine(configRoot, "audio_semantic_manifest.json");
            string physicalPath = Path.Combine(configRoot, "audio_physical_manifest.json");
            File.Copy(Path.Combine(repoRoot, "Assets", "Config", "MxFramework", "Audio", "audio_semantic_manifest.json"), semanticPath);
            File.Copy(Path.Combine(repoRoot, "Assets", "Config", "MxFramework", "Audio", "audio_physical_manifest.json"), physicalPath);
            File.Copy(Path.Combine(repoRoot, "Assets", "MxFrameworkGenerated", "Audio", "fmod_audio_library.json"), Path.Combine(generatedRoot, "fmod_audio_library.json"));

            PhysicalAudioAuthoringManifestDocument physical = JsonSerializer.Deserialize<PhysicalAudioAuthoringManifestDocument>(
                File.ReadAllText(physicalPath),
                JsonOptions);
            int expectedRuntimeBasisId = physical.IdAllocation.NextBasisId;
            string semanticHash = CharacterPackageHashUtility.ComputeFileSha256(semanticPath);
            string physicalHash = CharacterPackageHashUtility.ComputeFileSha256(physicalPath);

            EditorServer.ManagedPhysicalEventOperation operation = EditorServer.BuildManagedPhysicalEventOperation(
                root,
                string.Empty,
                new EditorServer.ManagedPhysicalEventApiRequest
                {
                    expectedSemanticHash = semanticHash,
                    expectedPhysicalHash = physicalHash,
                    familyId = "audio.family.object_impact",
                    materialPairId = "audio.pair.generic_solid--generic_solid",
                    variantKind = "Impact",
                    basisId = "audio.basis.object_impact.managed_test",
                    createBasis = true,
                    templateEventDefinitionId = "audio.event.mxframework.charactertest.physicalaudio.objectcollisiondefault",
                    displayName = "Managed Object Impact"
                },
                JsonOptions);

            string expectedPath = "event:/MxFramework/Generated/Physical/audio.family.object_impact/audio.basis.object_impact.managed_test";
            Require(operation.FmodRequest.EventPath == expectedPath
                    && operation.AudioEvent.FmodEventPath == expectedPath,
                "managed Physical Event planning must derive the exact controlled FMOD namespace from Family/Basis ids.");
            Require(operation.Cue.RuntimeCueId >= 500000
                    && operation.AudioEvent.RuntimeEventId >= 500000,
                "managed Cue and Event must use independent monotonic Semantic runtime-id spaces.");
            Require(operation.Basis.RuntimeBasisId == expectedRuntimeBasisId
                    && operation.PhysicalManifest.IdAllocation.NextBasisId == expectedRuntimeBasisId + 1
                    && operation.Basis.Lifecycle == PhysicalAudioLifecycles.Draft
                    && operation.Basis.MaterialPairId == "audio.pair.generic_solid--generic_solid"
                    && operation.Basis.VariantKind == "Impact"
                    && operation.Basis.SideA.MaterialId == "audio.material.generic_solid"
                    && operation.Basis.SideB.MaterialId == "audio.material.generic_solid",
                "createBasis must allocate one monotonic runtime Basis id and bind the requested public Pair Variant into a Draft.");
            Require(operation.Cue.Metadata["managedPhysicalBasisId"] == operation.Basis.Id
                    && operation.AudioEvent.Metadata["managedPhysicalBasisId"] == operation.Basis.Id
                    && operation.Basis.AudioCueId == operation.Cue.AudioCueId,
                "managed ownership must be explicit on Cue, Event, and Basis before FMOD mutation.");
            Require(operation.FmodRequest.Is3D
                    && operation.FmodRequest.MinDistance == 1f
                    && operation.FmodRequest.MaxDistance == 24f
                    && operation.FmodRequest.BankPath == "bank:/Master"
                    && operation.FmodRequest.BusPath == "bus:/SFX/Physical",
                "managed FMOD request must copy Bank, Bus, 3D, and distance facts from the selected template.");
            Require(CharacterPackageHashUtility.ComputeFileSha256(semanticPath) == semanticHash
                    && CharacterPackageHashUtility.ComputeFileSha256(physicalPath) == physicalHash,
                "planning must not write manifests before FMOD and Snapshot verification succeed.");

            EditorServer.ManagedPhysicalEventOperation materialize = EditorServer.BuildManagedPhysicalEventOperation(
                root,
                string.Empty,
                new EditorServer.ManagedPhysicalEventApiRequest
                {
                    expectedSemanticHash = semanticHash,
                    expectedPhysicalHash = physicalHash,
                    familyId = "audio.family.air_displacement",
                    basisId = "audio.basis.air_displacement.light_attack",
                    createBasis = false,
                    templateEventDefinitionId = "audio.event.mxframework.charactertest.action.lightattackhit",
                    displayName = "Light Attack Air Displacement"
                },
                JsonOptions);
            Require(materialize.Basis.RuntimeBasisId == 530101
                    && materialize.Cue.AudioCueId == "sfx.air_displacement.light_attack"
                    && materialize.Cue.RuntimeCueId == 500304
                    && materialize.AudioEvent.RuntimeEventId == 500314
                    && materialize.FmodRequest.ExpectedEventGuid == "{b6d3e343-5b60-495d-b3da-508847136970}"
                    && materialize.FmodRequest.EventPath == "event:/MxFramework/Generated/Physical/audio.family.air_displacement/audio.basis.air_displacement.light_attack",
                "materializing or syncing a preallocated Basis must retain its identities and exact controlled Event ownership.");

            string snapshotPath = Path.Combine(generatedRoot, "fmod_audio_library.json");
            AuthoringFmodAudioLibrarySnapshotDocument occupiedSnapshot = JsonSerializer.Deserialize<AuthoringFmodAudioLibrarySnapshotDocument>(
                File.ReadAllText(snapshotPath),
                JsonOptions);
            occupiedSnapshot.Events.Add(new AuthoringFmodAudioLibraryEvent
            {
                Path = "event:/MxFramework/Generated/Physical/audio.family.object_impact/audio.basis.object_impact.managed_collision",
                Guid = "{1f28c0e0-a96d-46ee-a385-c9c2985cf9db}",
                Banks = new System.Collections.Generic.List<string> { "Master" }
            });
            File.WriteAllText(snapshotPath, JsonSerializer.Serialize(occupiedSnapshot, JsonOptions));
            bool snapshotCollisionRejected = false;
            try
            {
                EditorServer.BuildManagedPhysicalEventOperation(
                    root,
                    string.Empty,
                    new EditorServer.ManagedPhysicalEventApiRequest
                    {
                        expectedSemanticHash = semanticHash,
                        expectedPhysicalHash = physicalHash,
                        familyId = "audio.family.object_impact",
                        basisId = "audio.basis.object_impact.managed_collision",
                        createBasis = true,
                        templateEventDefinitionId = "audio.event.mxframework.charactertest.physicalaudio.objectcollisiondefault"
                    },
                    JsonOptions);
            }
            catch (Exception exception)
            {
                snapshotCollisionRejected = exception.Message.Contains("拒绝接管既存 Event", StringComparison.Ordinal);
            }
            Require(snapshotCollisionRejected,
                "a snapshot-owned path without a tracked Semantic GUID must be rejected before FMOD mutation.");
            Require(EditorServer.RequiresManagedPhysicalUnityRollbackRefresh(backupCompleted: true, unityRefreshRequested: true)
                    && !EditorServer.RequiresManagedPhysicalUnityRollbackRefresh(backupCompleted: true, unityRefreshRequested: false),
                "once a Unity refresh request is issued, failure or timeout must schedule a second refresh after disk rollback.");

            bool injectionRejected = false;
            try { EditorServer.BuildManagedPhysicalEventPath("audio.family/escape", operation.Basis.Id); }
            catch { injectionRejected = true; }
            Require(injectionRejected
                    && !EditorServer.IsManagedPhysicalEventPath("event:/MxFramework/Generated/Physical/family/basis/extra")
                    && !EditorServer.IsManagedPhysicalEventPath("event:/External/family/basis"),
                "managed namespace validation must reject separators, extra path segments, and external Events.");

            string script = File.ReadAllText(Path.Combine(
                repoRoot,
                "FMOD",
                "MxFrameworkAudioDemo",
                "MxFrameworkAudioDemo",
                "Scripts",
                "MxFrameworkManagedPhysicalEvent.js"));
            Require(script.Contains("EVENT_PREFIX + request.familyId + \"/\" + request.basisId", StringComparison.Ordinal)
                    && script.Contains("event.relationships.banks.add(bank)", StringComparison.Ordinal)
                    && script.Contains("event.mixerInput.output = bus", StringComparison.Ordinal)
                    && script.Contains("SpatialiserEffect", StringComparison.Ordinal)
                    && script.Contains("event.id !== request.expectedEventGuid", StringComparison.Ordinal)
                    && script.Contains("if (!request.expectedEventGuid)", StringComparison.Ordinal)
                    && script.Contains("buildEndedConnect", StringComparison.Ordinal)
                    && script.Contains("studio.project.build({ platforms: \"Desktop\" })", StringComparison.Ordinal)
                    && script.Contains("MxFrameworkRollbackManagedPhysicalEvent", StringComparison.Ordinal)
                    && script.Contains("captureEventBackup", StringComparison.Ordinal)
                    && script.Contains("event.relationships.banks.remove", StringComparison.Ordinal)
                    && script.Contains("restoredOutput.id !== backup.busId", StringComparison.Ordinal)
                    && script.Contains("Managed rollback verification failed", StringComparison.Ordinal)
                    && script.Contains("Existing Managed Event spatial mode cannot be changed", StringComparison.Ordinal)
                    && script.Contains("if (!backup.existed)", StringComparison.Ordinal)
                    && script.Contains("backup.createdEventGuid", StringComparison.Ordinal)
                    && script.Contains("studio.project.deleteObject(event)", StringComparison.Ordinal),
                "FMOD managed script must capture and verify a reversible in-memory snapshot for existing Events while retaining exact ownership, Desktop build guards, and failed-new-Event deletion.");
            string command = EditorServer.CreateManagedPhysicalFmodScriptCommand(script);
            Require(command.Contains("MxFrameworkEnsurePendingManagedPhysicalEventAndBuild(false)", StringComparison.Ordinal)
                    && command.Contains("MxFrameworkManagedPhysicalEventScriptHash", StringComparison.Ordinal)
                    && command.Contains("eval(", StringComparison.Ordinal),
                "an open FMOD project must materialize and build through the same guarded Script Server process instead of starting a conflicting CLI process.");
            using JsonDocument backupDocument = JsonDocument.Parse(
                "{\"existed\":true,\"eventPath\":\"" + materialize.FmodRequest.EventPath
                + "\",\"eventGuid\":\"{b6d3e343-5b60-495d-b3da-508847136970}\","
                + "\"createdEventGuid\":\"\",\"bankIds\":[\"{ae3a6ecd-34b8-463b-975b-742aec93c902}\"],"
                + "\"busId\":\"{2ae7bc7f-2724-4768-90d8-5bfa2cad77a4}\",\"spatializerCount\":1,"
                + "\"minDistance\":1,\"maxDistance\":24}");
            string rollbackCommand = EditorServer.CreateManagedPhysicalFmodRollbackCommand(
                script,
                materialize.FmodRequest.EventPath,
                "{b6d3e343-5b60-495d-b3da-508847136970}",
                backupDocument.RootElement.Clone());
            Require(rollbackCommand.Contains("MxFrameworkRollbackManagedPhysicalEvent", StringComparison.Ordinal)
                    && rollbackCommand.Contains("audio.basis.air_displacement.light_attack", StringComparison.Ordinal)
                    && rollbackCommand.Contains("b6d3e343-5b60-495d-b3da-508847136970", StringComparison.Ordinal)
                    && rollbackCommand.Contains("\"existed\":true", StringComparison.Ordinal)
                    && rollbackCommand.Contains("ae3a6ecd-34b8-463b-975b-742aec93c902", StringComparison.Ordinal)
                    && rollbackCommand.Contains("2ae7bc7f-2724-4768-90d8-5bfa2cad77a4", StringComparison.Ordinal),
                "an existing managed Event update failure must send its exact Bank/Bus/distance snapshot back to the same Script Server rollback function.");
            Require(EditorServer.RequiresManagedPhysicalStudioRollback(
                        usedStudioScriptServer: true,
                        mutationStatusAvailable: true,
                        mutationAlreadyRolledBack: false)
                    && !EditorServer.RequiresManagedPhysicalStudioRollback(
                        usedStudioScriptServer: true,
                        mutationStatusAvailable: true,
                        mutationAlreadyRolledBack: true)
                    && !EditorServer.RequiresManagedPhysicalStudioRollback(
                        usedStudioScriptServer: false,
                        mutationStatusAvailable: true,
                        mutationAlreadyRolledBack: false),
                "an existing Event updated through Script Server must roll back open-project memory after any later failure, but must not run twice after an immediate self-rollback.");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}

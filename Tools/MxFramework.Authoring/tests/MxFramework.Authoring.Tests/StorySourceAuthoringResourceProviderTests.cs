using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static class StorySourceAuthoringResourceProviderTests
{
    public static void RunAll()
    {
        Provider_ReadsCurrentDraftPacket();
        Provider_ReportsMissingRequiredAndReferenceDiagnostics();
        Provider_ReportsPacketDerivationDiagnostics();
        EditorServer_UpdatesCanonAndOutlineWithConflictGate();
        EditorServer_RejectsApprovedStatusThroughUpdate();
        EditorServer_ReturnsDiagnosticsForInvalidOutlineApproval();
        EditorServer_BlocksPacketApprovalOnIndexPacketMismatch();
        EditorServer_UpdatesAndApprovesPacketsWithConflictGate();
        ArtSource_DerivesStoryGateFromApprovedPacket();
        ArtSource_KeepsStoryGateBlockedForIndexOnlyApproval();
        Provider_MergesIntoAuthoringResourceCollection();
    }

    private static void EditorServer_UpdatesCanonAndOutlineWithConflictGate()
    {
        string root = CreateTempRoot();
        JsonSerializerOptions jsonOptions = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        try
        {
            WriteStoryFixture(root, approved: false, includeArtTask: true, derivationApproved: false);
            string initialHash = GetCollectionMetadata(BuildCollection(root), "storySourcePacketsHash");

            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse blockedOutline =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourceOutlineApproveForPacket(root, new MxFramework.Authoring.Cli.EditorServer.StorySourceOutlineApproveRequest
                {
                    outlineId = "wgame.outline.test.v1",
                    expectedHash = initialHash
                }, jsonOptions);
            Require(!blockedOutline.success && blockedOutline.httpStatus == 400 && blockedOutline.code == AuthoringResourceDiagnosticCodes.StorySourcePacketApproveInvalid, "Outline approval should be blocked while referenced canon is draft.");

            var canonUpdate = new MxFramework.Authoring.Cli.EditorServer.StorySourceCanonUpdateRequest
            {
                canonId = "wgame.canon.world.test",
                type = "world",
                title = "Test World Approved",
                summary = "Approved canon fact for StorySource tests.",
                status = "canon",
                tags = new List<string> { "test", "approved" },
                relations = new List<MxFramework.Authoring.Cli.EditorServer.StorySourceCanonRelation>(),
                usedByPacketIds = new List<string> { "wgame.story.world.test.v1" },
                usedByOutlineIds = new List<string> { "wgame.outline.test.v1" },
                expectedHash = initialHash
            };
            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse canonSaved =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourceCanonUpdate(root, canonUpdate, jsonOptions);
            Require(canonSaved.success && canonSaved.httpStatus == 200 && canonSaved.currentHash != initialHash, "Canon update should save and change the StorySource hash.");

            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse staleCanon =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourceCanonUpdate(root, canonUpdate, jsonOptions);
            Require(!staleCanon.success && staleCanon.httpStatus == 409 && staleCanon.code == AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateConflict, "Stale canon update should produce a conflict.");

            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse outlineSaved =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourceOutlineUpdate(root, new MxFramework.Authoring.Cli.EditorServer.StorySourceOutlineUpdateRequest
                {
                    outlineId = "wgame.outline.test.v1",
                    title = "Test Outline Updated",
                    stage = "review",
                    outlinePath = "StorySource/Projects/WGame/Outlines/test.outline.md",
                    canonRefs = new List<string> { "wgame.canon.world.test" },
                    beats = new List<MxFramework.Authoring.Cli.EditorServer.StorySourceOutlineBeat>
                    {
                        new MxFramework.Authoring.Cli.EditorServer.StorySourceOutlineBeat { Slug = "arrival", Title = "Arrival", Summary = "The player reaches the test ruin." }
                    },
                    conflicts = new List<string> { "The rule crack changes the arena." },
                    openQuestions = new List<string>(),
                    targetConsumers = new List<string> { "artSource" },
                    expectedHash = canonSaved.currentHash
                }, jsonOptions);
            Require(outlineSaved.success && outlineSaved.httpStatus == 200, "Outline update should save after canon approval.");

            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse outlineApproved =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourceOutlineApproveForPacket(root, new MxFramework.Authoring.Cli.EditorServer.StorySourceOutlineApproveRequest
                {
                    outlineId = "wgame.outline.test.v1",
                    expectedHash = outlineSaved.currentHash
                }, jsonOptions);
            Require(outlineApproved.success && outlineApproved.httpStatus == 200, "Outline should approve for packet after canon approval and valid beats.");

            AuthoringResourceCollection afterApprove = BuildCollection(root);
            AuthoringResourceItem outline = afterApprove.Items.FirstOrDefault(item => GetMetadata(item, "outlineId") == "wgame.outline.test.v1");
            Require(outline != null && GetMetadata(outline, "stage") == "approved-for-packet", "Approved outline should project approved-for-packet stage.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void Provider_ReadsCurrentDraftPacket()
    {
        string root = FindRepoRoot();
        string indexPath = Path.Combine(root, StorySourceAuthoringResourceProvider.IndexRelativePath);
        Require(File.Exists(indexPath), "WGame StorySource packet index should exist.");

        AuthoringResourceCollection collection = BuildCollection(root);
        Require(collection.Providers.Exists(provider => provider.ProviderId == AuthoringResourceProviderIds.StorySource), "StorySource provider descriptor should be present.");
        Require(collection.Metadata.TryGetValue("storySourcePacketsHash", out string hash) && hash.StartsWith("sha256:", StringComparison.Ordinal), "StorySource packet hash should be exposed.");
        Require(collection.Metadata.TryGetValue("storySourcePacketSnapshotJson", out string snapshotJson) && !string.IsNullOrWhiteSpace(snapshotJson), "StorySource packet snapshot should be exposed.");
        Require(collection.Metadata.TryGetValue("storySourceCanonSnapshotJson", out string canonSnapshotJson) && !string.IsNullOrWhiteSpace(canonSnapshotJson), "StorySource canon snapshot should be exposed.");
        Require(collection.Metadata.TryGetValue("storySourceOutlineSnapshotJson", out string outlineSnapshotJson) && !string.IsNullOrWhiteSpace(outlineSnapshotJson), "StorySource outline snapshot should be exposed.");

        using JsonDocument snapshot = JsonDocument.Parse(snapshotJson);
        int packetCount = snapshot.RootElement.GetProperty("packets").GetArrayLength();
        Require(collection.Metadata.TryGetValue("storySourcePacketCount", out string packetCountText) && packetCountText == packetCount.ToString(System.Globalization.CultureInfo.InvariantCulture), "StorySource packet count metadata should match the snapshot.");
        using JsonDocument canonSnapshot = JsonDocument.Parse(canonSnapshotJson);
        int canonCount = canonSnapshot.RootElement.GetProperty("facts").GetArrayLength();
        Require(collection.Metadata.TryGetValue("storySourceCanonFactCount", out string canonCountText) && canonCountText == canonCount.ToString(System.Globalization.CultureInfo.InvariantCulture), "StorySource canon count metadata should match the snapshot.");
        using JsonDocument outlineSnapshot = JsonDocument.Parse(outlineSnapshotJson);
        int outlineCount = outlineSnapshot.RootElement.GetProperty("outlines").GetArrayLength();
        Require(collection.Metadata.TryGetValue("storySourceOutlineCount", out string outlineCountText) && outlineCountText == outlineCount.ToString(System.Globalization.CultureInfo.InvariantCulture), "StorySource outline count metadata should match the snapshot.");
    }

    private static void Provider_ReportsPacketDerivationDiagnostics()
    {
        string root = CreateTempRoot();
        try
        {
            WriteStoryFixture(root, approved: false, includeArtTask: true, derivationApproved: false);
            AuthoringResourceCollection collection = BuildCollection(root);
            AuthoringResourceItem packet = collection.Items.FirstOrDefault(item => GetMetadata(item, "storySourceId") == "wgame.story.world.test.v1");

            Require(packet != null, "Fixture packet should be projected.");
            Require(packet.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.StorySourceCanonRefNotApproved), "Draft canon refs should block packet derivation.");
            Require(packet.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.StorySourcePacketDerivationBlocked), "Packet derivation should report a blocking diagnostic.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void EditorServer_RejectsApprovedStatusThroughUpdate()
    {
        string root = CreateTempRoot();
        JsonSerializerOptions jsonOptions = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        try
        {
            WriteStoryFixture(root, approved: false, includeArtTask: true);
            string initialHash = GetCollectionMetadata(BuildCollection(root), "storySourcePacketsHash");

            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketUpdateRequest packetUpdate = CreateCompletePacketUpdate(initialHash);
            packetUpdate.approvalStatus = "approved-for-art";
            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse packetBlocked =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourcePacketUpdate(root, packetUpdate, jsonOptions);
            Require(!packetBlocked.success && packetBlocked.httpStatus == 400 && packetBlocked.code == AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateInvalid, "Packet update should not enter approved-for-art without the approve endpoint.");
            Require(packetBlocked.currentHash == initialHash, "Rejected packet update should return the latest StorySource hash.");

            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse outlineBlocked =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourceOutlineUpdate(root, new MxFramework.Authoring.Cli.EditorServer.StorySourceOutlineUpdateRequest
                {
                    outlineId = "wgame.outline.test.v1",
                    title = "Test Outline",
                    stage = "approved-for-packet",
                    outlinePath = "StorySource/Projects/WGame/Outlines/test.outline.md",
                    canonRefs = new List<string> { "wgame.canon.world.test" },
                    beats = new List<MxFramework.Authoring.Cli.EditorServer.StorySourceOutlineBeat>
                    {
                        new MxFramework.Authoring.Cli.EditorServer.StorySourceOutlineBeat { Slug = "arrival", Title = "Arrival", Summary = "The player reaches the test ruin." }
                    },
                    conflicts = new List<string>(),
                    openQuestions = new List<string>(),
                    targetConsumers = new List<string> { "artSource" },
                    expectedHash = initialHash
                }, jsonOptions);
            Require(!outlineBlocked.success && outlineBlocked.httpStatus == 400 && outlineBlocked.code == AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateInvalid, "Outline update should not enter approved-for-packet without the approve endpoint.");
            Require(outlineBlocked.currentHash == initialHash, "Rejected outline update should return the latest StorySource hash.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void EditorServer_ReturnsDiagnosticsForInvalidOutlineApproval()
    {
        string root = CreateTempRoot();
        JsonSerializerOptions jsonOptions = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        try
        {
            WriteStoryFixture(root, approved: false, includeArtTask: true);
            File.WriteAllText(Path.Combine(root, "StorySource", "Projects", "WGame", "Outlines", "test.outline.md"), @"---
entry: missing
---
# Test Outline

## Beat arrival
The player reaches the test ruin.
");
            string currentHash = GetCollectionMetadata(BuildCollection(root), "storySourcePacketsHash");

            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse blocked =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourceOutlineApproveForPacket(root, new MxFramework.Authoring.Cli.EditorServer.StorySourceOutlineApproveRequest
                {
                    outlineId = "wgame.outline.test.v1",
                    expectedHash = currentHash
                }, jsonOptions);
            Require(!blocked.success && blocked.httpStatus == 400 && blocked.code == AuthoringResourceDiagnosticCodes.StorySourcePacketApproveInvalid, "Invalid outline Markdown should block outline approval.");
            Require(blocked.currentHash == currentHash, "Failed outline approval should return the latest StorySource hash.");
            Require(blocked.diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.StorySourceOutlineBeatInvalid), "Failed outline approval should return server diagnostics.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void EditorServer_BlocksPacketApprovalOnIndexPacketMismatch()
    {
        string root = CreateTempRoot();
        JsonSerializerOptions jsonOptions = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        try
        {
            WriteStoryFixture(root, approved: false, includeArtTask: true);
            string initialHash = GetCollectionMetadata(BuildCollection(root), "storySourcePacketsHash");
            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse saved =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourcePacketUpdate(root, CreateCompletePacketUpdate(initialHash), jsonOptions);
            Require(saved.success, "Complete packet should save before mismatch test.");

            string packetPath = Path.Combine(root, "StorySource", "Projects", "WGame", "Packets", "test.input_packet.json");
            File.WriteAllText(packetPath, File.ReadAllText(packetPath).Replace(@"""version"": ""2026-06-11""", @"""version"": ""2026-06-10"""));
            string mismatchHash = GetCollectionMetadata(BuildCollection(root), "storySourcePacketsHash");

            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse blocked =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourcePacketApprove(root, new MxFramework.Authoring.Cli.EditorServer.StorySourcePacketApproveRequest
                {
                    storySourceId = "wgame.story.world.test.v1",
                    expectedHash = mismatchHash
                }, jsonOptions);
            Require(!blocked.success && blocked.httpStatus == 400 && blocked.code == AuthoringResourceDiagnosticCodes.StorySourcePacketApproveInvalid, "Packet approval should be blocked when index and packet fields diverge.");
            Require(blocked.currentHash == mismatchHash, "Failed packet approval should return the latest StorySource hash.");
            Require(blocked.diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.StorySourceVersionConflict), "Packet approval mismatch should return version-conflict diagnostics.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void Provider_ReportsMissingRequiredAndReferenceDiagnostics()
    {
        string root = CreateTempRoot();
        try
        {
            WriteStoryFixture(root, approved: false, includeArtTask: false);
            AuthoringResourceCollection collection = BuildCollection(root);
            AuthoringResourceItem packet = collection.Items.FirstOrDefault(item => GetMetadata(item, "storySourceId") == "wgame.story.world.test.v1");

            Require(packet != null, "Fixture packet should be projected.");
            Require(packet.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.StorySourceRequiredFieldMissing), "Missing fields should produce diagnostics.");
            Require(packet.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.StorySourceArtReferenceMissing), "Missing ArtSource task handoff should produce diagnostics.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void EditorServer_UpdatesAndApprovesPacketsWithConflictGate()
    {
        string root = CreateTempRoot();
        JsonSerializerOptions jsonOptions = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        try
        {
            WriteStoryFixture(root, approved: false, includeArtTask: true);
            AuthoringResourceCollection initial = BuildCollection(root);
            string initialHash = GetCollectionMetadata(initial, "storySourcePacketsHash");
            Require(initialHash.StartsWith("sha256:", StringComparison.Ordinal), "StorySource fixture should expose an initial hash.");

            var incompleteApprove = new MxFramework.Authoring.Cli.EditorServer.StorySourcePacketApproveRequest
            {
                storySourceId = "wgame.story.world.test.v1",
                expectedHash = initialHash
            };
            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse blockedApprove =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourcePacketApprove(root, incompleteApprove, jsonOptions);
            Require(!blockedApprove.success && blockedApprove.httpStatus == 400 && blockedApprove.code == AuthoringResourceDiagnosticCodes.StorySourcePacketApproveInvalid, "Incomplete packet approval should be rejected.");

            var update = new MxFramework.Authoring.Cli.EditorServer.StorySourcePacketUpdateRequest
            {
                storySourceId = "wgame.story.world.test.v1",
                version = "2026-06-11",
                approvalStatus = "draft",
                targetConsumers = new List<string> { "artSource" },
                canonRefs = new List<string> { "wgame.canon.world.test" },
                outlineRefs = new List<string> { "wgame.outline.test.v1" },
                reviewStatus = "draft",
                worldPremise = "A shared law world is disrupted by expired executable rules.",
                coreEvent = "An old decree archive keeps enforcing outdated judgments.",
                locationFunction = "Combat-readable archive arena and reference source for ArtSource.",
                civilizationOrigin = "An old law-keeping institution.",
                playerRole = "Law maintainer assigned to repair the archive.",
                enemyOrigins = new List<string> { "expired decree enforcers" },
                powerRules = new List<string> { "law failures emit structured seal feedback" },
                symbolMeanings = new List<string> { "red seal means invalid judgment" },
                toneTags = new List<string> { "rule comedy", "absurd but serious" },
                visualDoNotUse = new List<string> { "modern sci-fi panels" },
                expectedHash = initialHash
            };

            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse saved =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourcePacketUpdate(root, update, jsonOptions);
            Require(saved.success && saved.httpStatus == 200, "Valid StorySource packet update should save.");
            Require(saved.currentHash.StartsWith("sha256:", StringComparison.Ordinal) && saved.currentHash != initialHash, "Successful packet update should change the StorySource hash.");

            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse approved =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourcePacketApprove(root, new MxFramework.Authoring.Cli.EditorServer.StorySourcePacketApproveRequest
                {
                    storySourceId = "wgame.story.world.test.v1",
                    expectedHash = saved.currentHash
                }, jsonOptions);
            Require(approved.success && approved.httpStatus == 200, "Complete packet should approve for ArtSource.");

            AuthoringResourceCollection afterApprove = BuildCollection(root);
            AuthoringResourceItem packet = afterApprove.Items.FirstOrDefault(item => GetMetadata(item, "storySourceId") == "wgame.story.world.test.v1");
            Require(packet != null && GetMetadata(packet, "approvalStatus") == "approved-for-art", "Approved packet should project approved-for-art status.");
            Require(!packet.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.StorySourceRequiredFieldMissing), "Approved complete packet should not report missing fields.");

            MxFramework.Authoring.Cli.EditorServer.StorySourcePacketResponse conflict =
                MxFramework.Authoring.Cli.EditorServer.SaveStorySourcePacketUpdate(root, update, jsonOptions);
            Require(!conflict.success && conflict.httpStatus == 409 && conflict.code == AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateConflict, "Stale expectedHash should produce a StorySource conflict.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void Provider_MergesIntoAuthoringResourceCollection()
    {
        string root = FindRepoRoot();
        var context = new AuthoringResourceProviderContext
        {
            ScopeId = "test",
            ProjectRootPath = root
        };

        AuthoringResourceCollection merged = AuthoringResourceCollectionMerger.Merge(
            new StorySourceAuthoringResourceProvider().BuildResourceCollection(context),
            new ArtSourceAuthoringResourceProvider().BuildResourceCollection(context));

        Require(merged.Providers.Exists(provider => provider.ProviderId == AuthoringResourceProviderIds.StorySource), "Merged collection should keep StorySource provider.");
        Require(merged.Metadata.ContainsKey("storySourcePacketsHash"), "Merged collection should include StorySource metadata.");
    }

    private static void ArtSource_DerivesStoryGateFromApprovedPacket()
    {
        string root = CreateTempRoot();
        try
        {
            WriteStoryFixture(root, approved: true, includeArtTask: true);
            WriteArtFixture(root);
            AuthoringResourceCollection artCollection = new ArtSourceAuthoringResourceProvider().BuildResourceCollection(new AuthoringResourceProviderContext
            {
                ScopeId = "test",
                ProjectRootPath = root
            });
            string snapshotJson = GetCollectionMetadata(artCollection, "artSourceTaskSnapshotJson");
            using JsonDocument snapshot = JsonDocument.Parse(snapshotJson);
            JsonElement tasks = snapshot.RootElement.GetProperty("tasks");
            JsonElement storyTask = FindTask(tasks, "story.input_packet.approval");
            JsonElement referenceTask = FindTask(tasks, "reference.shared_law_world.generate_core_boards");

            Require(storyTask.GetProperty("status").GetString() == "ready", "Approved Story packet should derive story gate task as ready.");
            Require(string.IsNullOrWhiteSpace(storyTask.GetProperty("blockedBy").GetString()), "Approved Story packet should clear story gate blocker in the derived snapshot.");
            Require(ArrayContainsString(storyTask.GetProperty("reviewRefs"), "StorySource/Projects/WGame/Packets/test.input_packet.json"), "Approved Story packet should be written into story gate reviewRefs.");
            Require(referenceTask.GetProperty("status").GetString() == "active", "Approved Story packet should derive reference task as active.");
            Require(string.IsNullOrWhiteSpace(referenceTask.GetProperty("blockedBy").GetString()), "Approved Story packet should clear reference task story dependency in the derived snapshot.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void ArtSource_KeepsStoryGateBlockedForIndexOnlyApproval()
    {
        string root = CreateTempRoot();
        try
        {
            WriteStoryFixture(root, approved: false, includeArtTask: true);
            string indexPath = Path.Combine(root, StorySourceAuthoringResourceProvider.IndexRelativePath);
            File.WriteAllText(indexPath, File.ReadAllText(indexPath)
                .Replace(@"""approvalStatus"": ""draft""", @"""approvalStatus"": ""approved-for-art""")
                .Replace(@"""reviewStatus"": ""draft""", @"""reviewStatus"": ""approved-for-art"""));
            WriteArtFixture(root);

            AuthoringResourceCollection artCollection = new ArtSourceAuthoringResourceProvider().BuildResourceCollection(new AuthoringResourceProviderContext
            {
                ScopeId = "test",
                ProjectRootPath = root
            });
            string snapshotJson = GetCollectionMetadata(artCollection, "artSourceTaskSnapshotJson");
            using JsonDocument snapshot = JsonDocument.Parse(snapshotJson);
            JsonElement tasks = snapshot.RootElement.GetProperty("tasks");
            JsonElement storyTask = FindTask(tasks, "story.input_packet.approval");
            JsonElement referenceTask = FindTask(tasks, "reference.shared_law_world.generate_core_boards");

            Require(storyTask.GetProperty("status").GetString() == "blocked", "Index-only approved packet should not unlock the ArtSource story gate.");
            Require(!string.IsNullOrWhiteSpace(storyTask.GetProperty("blockedBy").GetString()), "Index-only approved packet should keep the story gate blocker.");
            Require(referenceTask.GetProperty("status").GetString() == "blocked", "Index-only approved packet should keep downstream reference work blocked.");
            Require(!ArrayContainsString(storyTask.GetProperty("reviewRefs"), "StorySource/Projects/WGame/Packets/test.input_packet.json"), "Unverified packet should not be written into story gate reviewRefs.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static AuthoringResourceCollection BuildCollection(string root)
    {
        return new StorySourceAuthoringResourceProvider().BuildResourceCollection(new AuthoringResourceProviderContext
        {
            ScopeId = "test",
            ProjectRootPath = root
        });
    }

    private static void WriteStoryFixture(string root, bool approved, bool includeArtTask, bool derivationApproved = true)
    {
        string storyRoot = Path.Combine(root, "StorySource", "Projects", "WGame");
        string canonRoot = Path.Combine(storyRoot, "Canon");
        string outlineRoot = Path.Combine(storyRoot, "Outlines");
        string packetRoot = Path.Combine(storyRoot, "Packets");
        Directory.CreateDirectory(canonRoot);
        Directory.CreateDirectory(outlineRoot);
        Directory.CreateDirectory(packetRoot);
        string canonStatus = derivationApproved || approved ? "canon" : "draft";
        string outlineStage = derivationApproved || approved ? "approved-for-packet" : "review";
        File.WriteAllText(Path.Combine(canonRoot, "canon_facts.json"), @"{
  ""schemaVersion"": 1,
  ""projectId"": ""wgame"",
  ""updated"": ""2026-06-11"",
  ""facts"": [
    {
      ""canonId"": ""wgame.canon.world.test"",
      ""type"": ""world"",
      ""title"": ""Test World"",
      ""summary"": ""A small canon fact for StorySource tests."",
      ""status"": """ + canonStatus + @""",
      ""tags"": [""test""],
      ""relations"": [],
      ""usedByPacketIds"": [""wgame.story.world.test.v1""],
      ""usedByOutlineIds"": [""wgame.outline.test.v1""]
    }
  ]
}");
        File.WriteAllText(Path.Combine(outlineRoot, "story_outlines.json"), @"{
  ""schemaVersion"": 1,
  ""projectId"": ""wgame"",
  ""updated"": ""2026-06-11"",
  ""outlines"": [
    {
      ""outlineId"": ""wgame.outline.test.v1"",
      ""title"": ""Test Outline"",
      ""stage"": """ + outlineStage + @""",
      ""outlinePath"": ""StorySource/Projects/WGame/Outlines/test.outline.md"",
      ""canonRefs"": [""wgame.canon.world.test""],
      ""beats"": [
        { ""slug"": ""arrival"", ""title"": ""Arrival"", ""summary"": ""The player reaches the test ruin."" }
      ],
      ""conflicts"": [""The rule crack changes the arena.""],
      ""openQuestions"": [],
      ""targetConsumers"": [""artSource""]
    }
  ]
}");
        File.WriteAllText(Path.Combine(outlineRoot, "test.outline.md"), @"---
entry: arrival
outlineId: wgame.outline.test.v1
---
# Test Outline

## Beat arrival

The player reaches the test ruin.
");
        File.WriteAllText(Path.Combine(storyRoot, "story_packets.json"), @"{
  ""schemaVersion"": 1,
  ""projectId"": ""wgame"",
  ""updated"": ""2026-06-11"",
  ""packets"": [
    {
      ""storySourceId"": ""wgame.story.world.test.v1"",
      ""version"": ""2026-06-11"",
      ""approvalStatus"": """ + (approved ? "approved-for-art" : "draft") + @""",
      ""targetConsumers"": [""artSource""],
      ""canonRefs"": [""wgame.canon.world.test""],
      ""outlineRefs"": [""wgame.outline.test.v1""],
      ""reviewStatus"": """ + (approved ? "approved-for-art" : "draft") + @""",
      ""packetPath"": ""StorySource/Projects/WGame/Packets/test.input_packet.json"",
      ""exportPath"": """",
      ""artSourceTaskId"": ""story.input_packet.approval"",
      ""notes"": ""test""
    }
  ]
}");
        File.WriteAllText(Path.Combine(packetRoot, "test.input_packet.json"), @"{
  ""schema"": ""wgame.story.input_packet.v1"",
  ""storySourceId"": ""wgame.story.world.test.v1"",
  ""version"": ""2026-06-11"",
  ""approvalStatus"": """ + (approved ? "approved-for-art" : "draft") + @""",
  ""targetConsumers"": [""artSource""],
  ""canonRefs"": [""wgame.canon.world.test""],
  ""outlineRefs"": [""wgame.outline.test.v1""],
  ""reviewStatus"": """ + (approved ? "approved-for-art" : "draft") + @""",
  ""exportPath"": """",
  ""worldPremise"": """ + (approved ? "A shared law world is disrupted by expired executable rules." : "") + @""",
  ""coreEvent"": """ + (approved ? "An old decree archive keeps enforcing outdated judgments." : "") + @""",
  ""locationFunction"": """ + (approved ? "Combat-readable archive arena and reference source for ArtSource." : "") + @""",
  ""civilizationOrigin"": """ + (approved ? "An old law-keeping institution." : "") + @""",
  ""playerRole"": """ + (approved ? "Law maintainer assigned to repair the archive." : "") + @""",
  ""enemyOrigins"": " + (approved ? @"[""expired decree enforcers""]" : "[]") + @",
  ""powerRules"": " + (approved ? @"[""law failures emit structured seal feedback""]" : "[]") + @",
  ""symbolMeanings"": " + (approved ? @"[""red seal means invalid judgment""]" : "[]") + @",
  ""toneTags"": " + (approved ? @"[""rule comedy"", ""absurd but serious""]" : "[]") + @",
  ""visualDoNotUse"": " + (approved ? @"[""modern sci-fi panels""]" : "[]") + @",
  ""changeLog"": []
}");

        if (!includeArtTask)
            return;

        string artRoot = Path.Combine(root, "ArtSource", "Projects", "WGame");
        Directory.CreateDirectory(artRoot);
        File.WriteAllText(Path.Combine(artRoot, "art_tasks.json"), @"{
  ""schemaVersion"": 1,
  ""projectId"": ""wgame"",
  ""updated"": ""2026-06-11"",
  ""workstreams"": [],
  ""tasks"": [
    {
      ""taskId"": ""story.input_packet.approval"",
      ""workstreamId"": ""story-gate"",
      ""title"": ""Story gate"",
      ""status"": ""blocked"",
      ""priority"": ""high"",
      ""assetIds"": [],
      ""blockedBy"": """",
      ""nextAction"": """",
      ""previewPath"": """",
      ""reviewRefs"": []
    }
  ]
}");
    }

    private static void WriteArtFixture(string root)
    {
        string artRoot = Path.Combine(root, "ArtSource", "Projects", "WGame");
        Directory.CreateDirectory(Path.Combine(artRoot, "ArtBible"));
        File.WriteAllText(Path.Combine(artRoot, "ArtBible", "SOURCE.md"), "# Source\n");
        File.WriteAllText(Path.Combine(artRoot, "art_manifest.json"), @"{
  ""schemaVersion"": 1,
  ""projectId"": ""wgame"",
  ""updated"": ""2026-06-11"",
  ""contract"": ""ArtSource/Projects/WGame/ART_SOURCE_CONTRACT.md"",
  ""assets"": [
    {
      ""assetId"": ""shared_law_world.art_bible"",
      ""family"": ""art_bible"",
      ""stage"": ""source"",
      ""sourcePath"": ""ArtSource/Projects/WGame/ArtBible/SOURCE.md"",
      ""exportPath"": """",
      ""unityTargetPath"": """",
      ""resourceKey"": ""shared_law_world.art_bible"",
      ""reviewStatus"": ""current-direction""
    }
  ]
}");
        File.WriteAllText(Path.Combine(artRoot, "art_tasks.json"), @"{
  ""schemaVersion"": 1,
  ""projectId"": ""wgame"",
  ""updated"": ""2026-06-11"",
  ""workstreams"": [
    {
      ""workstreamId"": ""story-gate"",
      ""title"": ""Story Gate"",
      ""phase"": ""story"",
      ""status"": ""blocked"",
      ""summary"": """",
      ""nextAction"": """",
      ""sortOrder"": 10
    },
    {
      ""workstreamId"": ""reference"",
      ""title"": ""Reference"",
      ""phase"": ""reference"",
      ""status"": ""blocked"",
      ""summary"": """",
      ""nextAction"": """",
      ""sortOrder"": 20
    }
  ],
  ""tasks"": [
    {
      ""taskId"": ""story.input_packet.approval"",
      ""workstreamId"": ""story-gate"",
      ""title"": ""Story gate"",
      ""status"": ""blocked"",
      ""priority"": ""high"",
      ""assetIds"": [],
      ""blockedBy"": ""Story 系统尚未交付已批准输入包"",
      ""nextAction"": ""等待输入。"",
      ""previewPath"": """",
      ""reviewRefs"": []
    },
    {
      ""taskId"": ""reference.shared_law_world.generate_core_boards"",
      ""workstreamId"": ""reference"",
      ""title"": ""Reference boards"",
      ""status"": ""blocked"",
      ""priority"": ""high"",
      ""assetIds"": [],
      ""blockedBy"": ""story.input_packet.approval"",
      ""nextAction"": """",
      ""previewPath"": """",
      ""reviewRefs"": []
    }
  ]
}");
    }

    private static JsonElement FindTask(JsonElement tasks, string taskId)
    {
        foreach (JsonElement task in tasks.EnumerateArray())
        {
            if (task.TryGetProperty("taskId", out JsonElement id) && id.GetString() == taskId)
                return task;
        }

        throw new InvalidOperationException("Task not found in snapshot: " + taskId);
    }

    private static bool ArrayContainsString(JsonElement array, string value)
    {
        if (array.ValueKind != JsonValueKind.Array)
            return false;

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String &&
                string.Equals(item.GetString() ?? string.Empty, value ?? string.Empty, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static MxFramework.Authoring.Cli.EditorServer.StorySourcePacketUpdateRequest CreateCompletePacketUpdate(string expectedHash)
    {
        return new MxFramework.Authoring.Cli.EditorServer.StorySourcePacketUpdateRequest
        {
            storySourceId = "wgame.story.world.test.v1",
            version = "2026-06-11",
            approvalStatus = "draft",
            targetConsumers = new List<string> { "artSource" },
            canonRefs = new List<string> { "wgame.canon.world.test" },
            outlineRefs = new List<string> { "wgame.outline.test.v1" },
            reviewStatus = "draft",
            worldPremise = "A shared law world is disrupted by expired executable rules.",
            coreEvent = "An old decree archive keeps enforcing outdated judgments.",
            locationFunction = "Combat-readable archive arena and reference source for ArtSource.",
            civilizationOrigin = "An old law-keeping institution.",
            playerRole = "Law maintainer assigned to repair the archive.",
            enemyOrigins = new List<string> { "expired decree enforcers" },
            powerRules = new List<string> { "law failures emit structured seal feedback" },
            symbolMeanings = new List<string> { "red seal means invalid judgment" },
            toneTags = new List<string> { "rule comedy", "absurd but serious" },
            visualDoNotUse = new List<string> { "modern sci-fi panels" },
            expectedHash = expectedHash ?? string.Empty
        };
    }

    private static string GetCollectionMetadata(AuthoringResourceCollection collection, string key)
    {
        if (collection == null || collection.Metadata == null || string.IsNullOrWhiteSpace(key))
            return string.Empty;
        return collection.Metadata.TryGetValue(key, out string value) ? value ?? string.Empty : string.Empty;
    }

    private static string GetMetadata(AuthoringResourceItem item, string key)
    {
        if (item == null || item.Metadata == null || string.IsNullOrWhiteSpace(key))
            return string.Empty;
        return item.Metadata.TryGetValue(key, out string value) ? value ?? string.Empty : string.Empty;
    }

    private static string FindRepoRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            string indexPath = Path.Combine(current, StorySourceAuthoringResourceProvider.IndexRelativePath);
            if (File.Exists(indexPath))
                return current;

            DirectoryInfo parent = Directory.GetParent(current);
            if (parent == null)
                break;
            current = parent.FullName;
        }

        throw new InvalidOperationException("Could not find repository root with StorySource packet index.");
    }

    private static string CreateTempRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), "mx-storysource-provider-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

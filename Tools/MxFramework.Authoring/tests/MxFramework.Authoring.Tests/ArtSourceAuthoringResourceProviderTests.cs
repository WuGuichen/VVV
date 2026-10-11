using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static class ArtSourceAuthoringResourceProviderTests
{
    public static void RunAll()
    {
        Provider_ReadsCurrentManifestAndProjectsItems();
        Provider_ReadsTaskSnapshotAndAssetTaskMetadata();
        Provider_ReportsTaskFileMissingAndInvalidJson();
        Provider_ReportsTaskReferenceDiagnostics();
        Provider_ReportsStoryGateAndMissingSourceDiagnostics();
        Provider_ProjectsPreviewMetadataAndDiagnostics();
        EditorServer_UpdatesArtTasksWithValidationAndConflictGate();
        EditorServer_CreatesArtTasksAndWorkstreamsWithValidationAndConflictGate();
        Provider_MergesIntoAuthoringResourceCollection();
    }

    private static void Provider_ReadsCurrentManifestAndProjectsItems()
    {
        string root = FindRepoRoot();
        string manifestPath = Path.Combine(root, ArtSourceAuthoringResourceProvider.ManifestRelativePath);
        Require(File.Exists(manifestPath), "WGame ArtSource manifest should exist.");

        int manifestCount = CountManifestAssets(manifestPath);
        AuthoringResourceCollection collection = BuildCollection(root);

        Require(manifestCount > 0, "Current ArtSource manifest should contain at least one asset.");
        Require(collection.Items.Count == manifestCount, "ArtSource provider item count should match art_manifest.json.");
        Require(collection.Providers.Exists(provider => provider.ProviderId == AuthoringResourceProviderIds.ArtSource), "ArtSource provider descriptor should be present.");
        Require(collection.Items.Exists(item => GetMetadata(item, "assetId") == "story_to_art.input_contract"), "Story-to-Art contract should appear as an ArtSource item.");
        AuthoringResourceItem uiKit = collection.Items.FirstOrDefault(item => GetMetadata(item, "assetId") == "ui.mxruinnight.sliced_png_set");
        Require(uiKit != null && GetMetadata(uiKit, "previewExists") == "true", "Current UI kit fallback should expose a real preview.");
        Require(GetMetadata(uiKit, "previewUrl").StartsWith("/ArtSource/UIKits/MxRuinNight/source/raster/", StringComparison.Ordinal), "Current UI kit fallback should expose a static preview URL.");
        Require(collection.Items.All(item => item.RuntimeAvailability == AuthoringResourceRuntimeAvailability.NotRuntimeLoadable), "ArtSource items must not claim runtime loadability.");
    }

    private static void Provider_ReadsTaskSnapshotAndAssetTaskMetadata()
    {
        string root = FindRepoRoot();
        string tasksPath = Path.Combine(root, ArtSourceAuthoringResourceProvider.TasksRelativePath);
        Require(File.Exists(tasksPath), "WGame ArtSource task file should exist.");

        AuthoringResourceCollection collection = BuildCollection(root);
        Require(collection.Metadata.TryGetValue("artSourceTasksAvailable", out string available) && available == "true", "ArtSource tasks should be available in collection metadata.");
        Require(collection.Metadata.TryGetValue("artSourceTasksHash", out string tasksHash) && tasksHash.StartsWith("sha256:", StringComparison.Ordinal), "ArtSource task hash should be exposed in collection metadata.");
        Require(collection.Metadata.TryGetValue("artSourceTaskSnapshotJson", out string snapshotJson) && !string.IsNullOrWhiteSpace(snapshotJson), "ArtSource task snapshot should be serialized into collection metadata.");

        using JsonDocument snapshot = JsonDocument.Parse(snapshotJson);
        JsonElement rootElement = snapshot.RootElement;
        Require(rootElement.GetProperty("workstreams").GetArrayLength() > 0, "Task snapshot should expose workstreams.");
        Require(rootElement.GetProperty("tasks").GetArrayLength() > 0, "Task snapshot should expose tasks.");

        AuthoringResourceItem uiKit = collection.Items.FirstOrDefault(item => GetMetadata(item, "assetId") == "ui.mxruinnight.sliced_png_set");
        Require(uiKit != null, "UI kit item should be present.");
        Require(GetMetadata(uiKit, "workstreamIds").Contains("ui-kit-fallback", StringComparison.Ordinal), "Asset metadata should include related workstream ids.");
        Require(GetMetadata(uiKit, "taskIds").Contains("ui.mxruinnight.fallback_reference", StringComparison.Ordinal), "Asset metadata should include related task ids.");
        Require(GetMetadata(uiKit, "activeTaskCount") == "1", "Asset metadata should count active tasks.");

        AuthoringResourceItem reference = collection.Items.FirstOrDefault(item => GetMetadata(item, "assetId") == "lookdev.night_ruin.shader_family_board");
        Require(reference != null && GetMetadata(reference, "taskIds").Contains("lookdev.legacy_shader_family.reference", StringComparison.Ordinal), "Reference asset should include the current lookdev review task.");
        Require(reference != null && GetMetadata(reference, "blockedTaskCount") == "0", "Approved story gate should leave generated reference assets unblocked.");
    }

    private static void Provider_ReportsTaskFileMissingAndInvalidJson()
    {
        string root = CreateTempRoot();
        try
        {
            WriteMinimalManifest(root);

            AuthoringResourceCollection missing = BuildCollection(root);
            Require(missing.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.ArtSourceTasksMissing), "Missing art_tasks.json should produce a task diagnostic.");

            string artRoot = Path.Combine(root, "ArtSource", "Projects", "WGame");
            File.WriteAllText(Path.Combine(artRoot, "art_tasks.json"), "{ invalid");
            AuthoringResourceCollection invalid = BuildCollection(root);
            Require(invalid.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.ArtSourceTasksJsonInvalid), "Invalid art_tasks.json should produce a JSON diagnostic.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void Provider_ReportsTaskReferenceDiagnostics()
    {
        string root = CreateTempRoot();
        try
        {
            WriteMinimalManifest(root);
            string artRoot = Path.Combine(root, "ArtSource", "Projects", "WGame");
            File.WriteAllText(Path.Combine(artRoot, "art_tasks.json"), @"{
  ""schemaVersion"": 1,
  ""projectId"": ""wgame"",
  ""updated"": ""2026-06-10"",
  ""workstreams"": [
    {
      ""workstreamId"": ""known"",
      ""title"": ""Known"",
      ""phase"": ""source"",
      ""status"": ""active"",
      ""summary"": """",
      ""nextAction"": """",
      ""sortOrder"": 10
    }
  ],
  ""tasks"": [
    {
      ""taskId"": ""task.missing_workstream"",
      ""workstreamId"": ""missing"",
      ""title"": ""Missing workstream"",
      ""status"": ""active"",
      ""priority"": ""medium"",
      ""assetIds"": [""test.ready""],
      ""blockedBy"": """",
      ""nextAction"": """",
      ""previewPath"": """",
      ""reviewRefs"": []
    },
    {
      ""taskId"": ""task.missing_asset"",
      ""workstreamId"": ""known"",
      ""title"": ""Missing asset"",
      ""status"": ""active"",
      ""priority"": ""medium"",
      ""assetIds"": [""test.missing""],
      ""blockedBy"": """",
      ""nextAction"": """",
      ""previewPath"": """",
      ""reviewRefs"": []
    }
  ]
}");

            AuthoringResourceCollection collection = BuildCollection(root);
            Require(collection.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.ArtSourceTaskWorkstreamMissing), "Missing workstream references should produce diagnostics.");
            Require(collection.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.ArtSourceTaskAssetMissing), "Missing task asset references should produce diagnostics.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void Provider_ReportsStoryGateAndMissingSourceDiagnostics()
    {
        string root = CreateTempRoot();
        try
        {
            string artRoot = Path.Combine(root, "ArtSource", "Projects", "WGame");
            Directory.CreateDirectory(Path.Combine(artRoot, "ArtBible"));
            File.WriteAllText(Path.Combine(artRoot, "ArtBible", "SOURCE.md"), "# Source\n");
            File.WriteAllText(Path.Combine(artRoot, "art_manifest.json"), @"{
  ""schemaVersion"": 1,
  ""projectId"": ""wgame"",
  ""updated"": ""2026-06-10"",
  ""contract"": ""ArtSource/Projects/WGame/ART_SOURCE_CONTRACT.md"",
  ""assets"": [
    {
      ""assetId"": ""test.ready"",
      ""family"": ""art_bible"",
      ""stage"": ""source"",
      ""sourcePath"": ""ArtSource/Projects/WGame/ArtBible/SOURCE.md"",
      ""exportPath"": """",
      ""unityTargetPath"": """",
      ""resourceKey"": """",
      ""reviewStatus"": ""current-direction""
    },
    {
      ""assetId"": ""test.waiting_story"",
      ""family"": ""reference"",
      ""stage"": ""source"",
      ""sourcePath"": ""ArtSource/Projects/WGame/Source/Reference/SharedLawWorld/missing.png"",
      ""exportPath"": """",
      ""unityTargetPath"": """",
      ""resourceKey"": """",
      ""reviewStatus"": ""waiting-story-input""
    }
  ]
}");

            AuthoringResourceCollection collection = BuildCollection(root);
            AuthoringResourceItem waiting = collection.Items.FirstOrDefault(item => GetMetadata(item, "assetId") == "test.waiting_story");

            Require(waiting != null, "Waiting story ArtSource item should be projected.");
            Require(waiting.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.ArtSourceStoryInputWaiting), "waiting-story-input should produce a story gate diagnostic.");
            Require(waiting.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.ArtSourceSourceMissing), "missing source path should produce a source diagnostic.");
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
            new ArtSourceAuthoringResourceProvider().BuildResourceCollection(context),
            new ExternalImportStagingAuthoringResourceProvider().BuildResourceCollection(context));

        Require(merged.Providers.Exists(provider => provider.ProviderId == AuthoringResourceProviderIds.ArtSource), "Merged collection should keep ArtSource provider.");
        Require(merged.Items.Exists(item => item.SourceProviderId == AuthoringResourceProviderIds.ArtSource), "Merged collection should include ArtSource items.");
    }

    private static void Provider_ProjectsPreviewMetadataAndDiagnostics()
    {
        string root = CreateTempRoot();
        try
        {
            string artRoot = Path.Combine(root, "ArtSource", "Projects", "WGame");
            string reviewRoot = Path.Combine(root, "Docs", "Validation", "WGameShaderFamily");
            Directory.CreateDirectory(Path.Combine(artRoot, "Source", "Reference"));
            Directory.CreateDirectory(Path.Combine(artRoot, "Exports"));
            Directory.CreateDirectory(reviewRoot);
            File.WriteAllText(Path.Combine(artRoot, "Source", "Reference", "source.png"), "png");
            File.WriteAllText(Path.Combine(artRoot, "Exports", "export.jpg"), "jpg");
            File.WriteAllText(Path.Combine(reviewRoot, "Review Board.png"), "png");
            File.WriteAllText(Path.Combine(artRoot, "art_manifest.json"), @"{
  ""schemaVersion"": 1,
  ""projectId"": ""wgame"",
  ""updated"": ""2026-06-10"",
  ""contract"": ""ArtSource/Projects/WGame/ART_SOURCE_CONTRACT.md"",
  ""assets"": [
    {
      ""assetId"": ""test.preview.explicit"",
      ""family"": ""lookdev"",
      ""stage"": ""reviewed"",
      ""sourcePath"": ""ArtSource/Projects/WGame/Source/Reference/source.png"",
      ""exportPath"": ""ArtSource/Projects/WGame/Exports/export.jpg"",
      ""unityTargetPath"": """",
      ""resourceKey"": """",
      ""reviewStatus"": ""legacy-reference"",
      ""previewPath"": ""Docs/Validation/WGameShaderFamily/Review Board.png"",
      ""previewRole"": ""family-review"",
      ""previewNote"": ""Shared board""
    },
    {
      ""assetId"": ""test.preview.source_fallback"",
      ""family"": ""reference"",
      ""stage"": ""source"",
      ""sourcePath"": ""ArtSource/Projects/WGame/Source/Reference/source.png"",
      ""exportPath"": """",
      ""unityTargetPath"": """",
      ""resourceKey"": """",
      ""reviewStatus"": ""current-direction""
    },
    {
      ""assetId"": ""test.preview.export_fallback"",
      ""family"": ""ui"",
      ""stage"": ""export"",
      ""sourcePath"": ""ArtSource/Projects/WGame/Source/Reference"",
      ""exportPath"": ""ArtSource/Projects/WGame/Exports/export.jpg"",
      ""unityTargetPath"": """",
      ""resourceKey"": """",
      ""reviewStatus"": ""fallback""
    },
    {
      ""assetId"": ""test.preview.missing"",
      ""family"": ""reference"",
      ""stage"": ""source"",
      ""sourcePath"": ""ArtSource/Projects/WGame/Source/Reference/missing.png"",
      ""exportPath"": """",
      ""unityTargetPath"": """",
      ""resourceKey"": """",
      ""reviewStatus"": ""waiting-story-input""
    }
  ]
}");

            AuthoringResourceCollection collection = BuildCollection(root);
            AuthoringResourceItem explicitPreview = collection.Items.FirstOrDefault(item => GetMetadata(item, "assetId") == "test.preview.explicit");
            AuthoringResourceItem sourceFallback = collection.Items.FirstOrDefault(item => GetMetadata(item, "assetId") == "test.preview.source_fallback");
            AuthoringResourceItem exportFallback = collection.Items.FirstOrDefault(item => GetMetadata(item, "assetId") == "test.preview.export_fallback");
            AuthoringResourceItem missing = collection.Items.FirstOrDefault(item => GetMetadata(item, "assetId") == "test.preview.missing");

            Require(explicitPreview != null, "Explicit preview item should be projected.");
            Require(GetMetadata(explicitPreview, "previewPath") == "Docs/Validation/WGameShaderFamily/Review Board.png", "Explicit previewPath should be preserved.");
            Require(GetMetadata(explicitPreview, "previewUrl") == "/Docs/Validation/WGameShaderFamily/Review%20Board.png", "previewUrl should be static-server-safe.");
            Require(GetMetadata(explicitPreview, "previewExists") == "true", "Existing preview should be marked present.");
            Require(GetMetadata(explicitPreview, "previewRole") == "family-review", "previewRole should be preserved.");
            Require(GetMetadata(explicitPreview, "previewNote") == "Shared board", "previewNote should be preserved.");

            Require(GetMetadata(sourceFallback, "previewPath") == "ArtSource/Projects/WGame/Source/Reference/source.png", "Image sourcePath should become preview fallback.");
            Require(GetMetadata(exportFallback, "previewPath") == "ArtSource/Projects/WGame/Exports/export.jpg", "Image exportPath should become preview fallback after non-image source.");
            Require(GetMetadata(missing, "previewExists") == "false", "Missing preview should be marked absent.");
            Require(missing.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.ArtSourcePreviewMissing), "Missing preview should produce ArtSource preview diagnostic.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void EditorServer_UpdatesArtTasksWithValidationAndConflictGate()
    {
        string root = CreateTempRoot();
        JsonSerializerOptions jsonOptions = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        try
        {
            WriteTaskUpdateFixture(root);

            AuthoringResourceCollection initial = BuildCollection(root);
            string initialHash = GetCollectionMetadata(initial, "artSourceTasksHash");
            Require(initialHash.StartsWith("sha256:", StringComparison.Ordinal), "Task update fixture should expose an initial hash.");

            var validRequest = new MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskUpdateRequest
            {
                taskId = "task.update",
                status = "active",
                priority = "low",
                blockedBy = "",
                nextAction = "继续推进任务。",
                assetIds = new List<string> { "test.ready", "test.extra", "test.extra" },
                expectedHash = initialHash
            };

            MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskUpdateResponse valid =
                MxFramework.Authoring.Cli.EditorServer.SaveArtSourceTaskUpdate(root, validRequest, jsonOptions);
            Require(valid.success && valid.httpStatus == 200, "Valid ArtSource task update should save.");
            Require(valid.currentHash.StartsWith("sha256:", StringComparison.Ordinal) && valid.currentHash != initialHash, "Successful task update should change the task hash.");

            string tasksPath = Path.Combine(root, ArtSourceAuthoringResourceProvider.TasksRelativePath);
            using JsonDocument savedDocument = JsonDocument.Parse(File.ReadAllText(tasksPath));
            JsonElement savedTask = savedDocument.RootElement.GetProperty("tasks")[0];
            Require(savedTask.GetProperty("status").GetString() == "active", "Saved task status should be updated.");
            Require(savedTask.GetProperty("priority").GetString() == "low", "Saved task priority should be updated.");
            Require(savedTask.GetProperty("assetIds").GetArrayLength() == 2, "Saved task assetIds should be normalized and de-duplicated.");

            AuthoringResourceCollection afterSave = BuildCollection(root);
            Require(GetCollectionMetadata(afterSave, "artSourceTasksHash") == valid.currentHash, "Provider metadata hash should match the saved task file.");
            AuthoringResourceItem extra = afterSave.Items.FirstOrDefault(item => GetMetadata(item, "assetId") == "test.extra");
            Require(extra != null && GetMetadata(extra, "activeTaskCount") == "1", "Updated asset binding should update per-asset task metadata.");

            RequireInvalidUpdate(root, jsonOptions, valid.currentHash, new MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskUpdateRequest
            {
                taskId = "task.update",
                status = "not-a-status",
                priority = "low",
                expectedHash = valid.currentHash
            }, AuthoringResourceDiagnosticCodes.ArtSourceTaskUpdateInvalid, "Invalid status should be rejected.");

            RequireInvalidUpdate(root, jsonOptions, valid.currentHash, new MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskUpdateRequest
            {
                taskId = "task.update",
                status = "active",
                priority = "not-a-priority",
                expectedHash = valid.currentHash
            }, AuthoringResourceDiagnosticCodes.ArtSourceTaskUpdateInvalid, "Invalid priority should be rejected.");

            RequireInvalidUpdate(root, jsonOptions, valid.currentHash, new MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskUpdateRequest
            {
                taskId = "task.missing",
                status = "active",
                priority = "low",
                expectedHash = valid.currentHash
            }, AuthoringResourceDiagnosticCodes.ArtSourceTaskUpdateInvalid, "Missing task should be rejected.");

            RequireInvalidUpdate(root, jsonOptions, valid.currentHash, new MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskUpdateRequest
            {
                taskId = "task.update",
                status = "active",
                priority = "low",
                assetIds = new List<string> { "test.missing" },
                expectedHash = valid.currentHash
            }, AuthoringResourceDiagnosticCodes.ArtSourceTaskAssetInvalid, "Missing manifest asset should be rejected.");

            MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskUpdateResponse conflict =
                MxFramework.Authoring.Cli.EditorServer.SaveArtSourceTaskUpdate(root, new MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskUpdateRequest
                {
                    taskId = "task.update",
                    status = "review",
                    priority = "medium",
                    expectedHash = initialHash
                }, jsonOptions);
            Require(!conflict.success && conflict.httpStatus == 409 && conflict.code == AuthoringResourceDiagnosticCodes.ArtSourceTaskUpdateConflict, "Stale expectedHash should produce a conflict.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void RequireInvalidUpdate(
        string root,
        JsonSerializerOptions jsonOptions,
        string expectedCurrentHash,
        MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskUpdateRequest request,
        string expectedCode,
        string message)
    {
        MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskUpdateResponse response =
            MxFramework.Authoring.Cli.EditorServer.SaveArtSourceTaskUpdate(root, request, jsonOptions);
        Require(!response.success && response.code == expectedCode, message);
        Require(response.currentHash == expectedCurrentHash, "Rejected update should report the unchanged current hash.");
    }

    private static void EditorServer_CreatesArtTasksAndWorkstreamsWithValidationAndConflictGate()
    {
        string root = CreateTempRoot();
        JsonSerializerOptions jsonOptions = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        try
        {
            WriteTaskUpdateFixture(root);

            AuthoringResourceCollection initial = BuildCollection(root);
            string initialHash = GetCollectionMetadata(initial, "artSourceTasksHash");
            Require(initialHash.StartsWith("sha256:", StringComparison.Ordinal), "Task create fixture should expose an initial hash.");

            MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskCreateResponse workstream =
                MxFramework.Authoring.Cli.EditorServer.SaveArtSourceTaskCreate(root, new MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskCreateRequest
                {
                    kind = "workstream",
                    workstreamId = "new-stream",
                    title = "新增进程",
                    phase = "source",
                    status = "active",
                    summary = "用于测试新建进程。",
                    nextAction = "创建第一条任务。",
                    expectedHash = initialHash
                }, jsonOptions);
            Require(workstream.success && workstream.httpStatus == 200, "Valid ArtSource workstream create should save.");
            Require(workstream.currentHash.StartsWith("sha256:", StringComparison.Ordinal) && workstream.currentHash != initialHash, "Creating a workstream should change the task hash.");

            MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskCreateResponse task =
                MxFramework.Authoring.Cli.EditorServer.SaveArtSourceTaskCreate(root, new MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskCreateRequest
                {
                    kind = "task",
                    taskId = "task.created",
                    workstreamId = "new-stream",
                    title = "新增任务",
                    status = "active",
                    priority = "high",
                    blockedBy = "",
                    nextAction = "继续推进新增任务。",
                    assetIds = new List<string> { "test.ready", "test.extra", "test.extra" },
                    reviewRefs = new List<string> { "Docs/Review.md", "Docs/Review.md" },
                    expectedHash = workstream.currentHash
                }, jsonOptions);
            Require(task.success && task.httpStatus == 200, "Valid ArtSource task create should save.");

            string tasksPath = Path.Combine(root, ArtSourceAuthoringResourceProvider.TasksRelativePath);
            using JsonDocument savedDocument = JsonDocument.Parse(File.ReadAllText(tasksPath));
            Require(savedDocument.RootElement.GetProperty("workstreams").GetArrayLength() == 2, "Saved task file should contain the created workstream.");
            Require(savedDocument.RootElement.GetProperty("tasks").GetArrayLength() == 2, "Saved task file should contain the created task.");
            JsonElement savedTask = savedDocument.RootElement.GetProperty("tasks")[1];
            Require(savedTask.GetProperty("taskId").GetString() == "task.created", "Created task id should be persisted.");
            Require(savedTask.GetProperty("assetIds").GetArrayLength() == 2, "Created task assetIds should be normalized.");
            Require(savedTask.GetProperty("reviewRefs").GetArrayLength() == 1, "Created task review refs should be normalized.");

            AuthoringResourceCollection afterCreate = BuildCollection(root);
            Require(GetCollectionMetadata(afterCreate, "artSourceTasksHash") == task.currentHash, "Provider metadata hash should match the created task file.");
            AuthoringResourceItem extra = afterCreate.Items.FirstOrDefault(item => GetMetadata(item, "assetId") == "test.extra");
            Require(extra != null && GetMetadata(extra, "taskIds").Contains("task.created", StringComparison.Ordinal), "Created task should update per-asset task metadata.");

            RequireInvalidCreate(root, jsonOptions, task.currentHash, new MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskCreateRequest
            {
                kind = "task",
                taskId = "task.created",
                workstreamId = "new-stream",
                title = "重复任务",
                status = "active",
                priority = "medium",
                expectedHash = task.currentHash
            }, AuthoringResourceDiagnosticCodes.ArtSourceTaskCreateInvalid, "Duplicate task id should be rejected.");

            RequireInvalidCreate(root, jsonOptions, task.currentHash, new MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskCreateRequest
            {
                kind = "task",
                taskId = "task.invalid_asset",
                workstreamId = "new-stream",
                title = "非法资产任务",
                status = "active",
                priority = "medium",
                assetIds = new List<string> { "test.missing" },
                expectedHash = task.currentHash
            }, AuthoringResourceDiagnosticCodes.ArtSourceTaskCreateAssetInvalid, "Missing manifest asset should be rejected on create.");

            MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskCreateResponse conflict =
                MxFramework.Authoring.Cli.EditorServer.SaveArtSourceTaskCreate(root, new MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskCreateRequest
                {
                    kind = "workstream",
                    workstreamId = "conflict-stream",
                    title = "冲突进程",
                    status = "active",
                    expectedHash = initialHash
                }, jsonOptions);
            Require(!conflict.success && conflict.httpStatus == 409 && conflict.code == AuthoringResourceDiagnosticCodes.ArtSourceTaskCreateConflict, "Stale expectedHash should reject creates.");
        }
        finally
        {
            TryDeleteDirectory(root);
        }

        string missingTaskFileRoot = CreateTempRoot();
        try
        {
            WriteMinimalManifest(missingTaskFileRoot);
            MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskCreateResponse initialized =
                MxFramework.Authoring.Cli.EditorServer.SaveArtSourceTaskCreate(missingTaskFileRoot, new MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskCreateRequest
                {
                    kind = "workstream",
                    workstreamId = "first-stream",
                    title = "第一个进程",
                    status = "active"
                }, jsonOptions);
            Require(initialized.success && initialized.currentHash.StartsWith("sha256:", StringComparison.Ordinal), "Creating the first workstream should initialize a missing art_tasks.json.");
            AuthoringResourceCollection initializedCollection = BuildCollection(missingTaskFileRoot);
            Require(GetCollectionMetadata(initializedCollection, "artSourceWorkstreamCount") == "1", "Initialized art_tasks.json should be visible through provider metadata.");
        }
        finally
        {
            TryDeleteDirectory(missingTaskFileRoot);
        }
    }

    private static void RequireInvalidCreate(
        string root,
        JsonSerializerOptions jsonOptions,
        string expectedCurrentHash,
        MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskCreateRequest request,
        string expectedCode,
        string message)
    {
        MxFramework.Authoring.Cli.EditorServer.ArtSourceTaskCreateResponse response =
            MxFramework.Authoring.Cli.EditorServer.SaveArtSourceTaskCreate(root, request, jsonOptions);
        Require(!response.success && response.code == expectedCode, message);
        Require(response.currentHash == expectedCurrentHash, "Rejected create should report the unchanged current hash.");
    }

    private static AuthoringResourceCollection BuildCollection(string root)
    {
        var context = new AuthoringResourceProviderContext
        {
            ScopeId = "test",
            ProjectRootPath = root
        };
        return new ArtSourceAuthoringResourceProvider().BuildResourceCollection(context);
    }

    private static int CountManifestAssets(string manifestPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        return document.RootElement.GetProperty("assets").GetArrayLength();
    }

    private static void WriteMinimalManifest(string root)
    {
        string artRoot = Path.Combine(root, "ArtSource", "Projects", "WGame");
        Directory.CreateDirectory(Path.Combine(artRoot, "ArtBible"));
        File.WriteAllText(Path.Combine(artRoot, "ArtBible", "SOURCE.md"), "# Source\n");
        File.WriteAllText(Path.Combine(artRoot, "art_manifest.json"), @"{
  ""schemaVersion"": 1,
  ""projectId"": ""wgame"",
  ""updated"": ""2026-06-10"",
  ""contract"": ""ArtSource/Projects/WGame/ART_SOURCE_CONTRACT.md"",
  ""assets"": [
    {
      ""assetId"": ""test.ready"",
      ""family"": ""art_bible"",
      ""stage"": ""source"",
      ""sourcePath"": ""ArtSource/Projects/WGame/ArtBible/SOURCE.md"",
      ""exportPath"": """",
      ""unityTargetPath"": """",
      ""resourceKey"": """",
      ""reviewStatus"": ""current-direction""
    }
  ]
}");
    }

    private static void WriteTaskUpdateFixture(string root)
    {
        string artRoot = Path.Combine(root, "ArtSource", "Projects", "WGame");
        Directory.CreateDirectory(Path.Combine(artRoot, "ArtBible"));
        File.WriteAllText(Path.Combine(artRoot, "ArtBible", "SOURCE.md"), "# Source\n");
        File.WriteAllText(Path.Combine(artRoot, "art_manifest.json"), @"{
  ""schemaVersion"": 1,
  ""projectId"": ""wgame"",
  ""updated"": ""2026-06-10"",
  ""contract"": ""ArtSource/Projects/WGame/ART_SOURCE_CONTRACT.md"",
  ""assets"": [
    {
      ""assetId"": ""test.ready"",
      ""family"": ""art_bible"",
      ""stage"": ""source"",
      ""sourcePath"": ""ArtSource/Projects/WGame/ArtBible/SOURCE.md"",
      ""exportPath"": """",
      ""unityTargetPath"": """",
      ""resourceKey"": """",
      ""reviewStatus"": ""current-direction""
    },
    {
      ""assetId"": ""test.extra"",
      ""family"": ""reference"",
      ""stage"": ""source"",
      ""sourcePath"": ""ArtSource/Projects/WGame/ArtBible/SOURCE.md"",
      ""exportPath"": """",
      ""unityTargetPath"": """",
      ""resourceKey"": """",
      ""reviewStatus"": ""current-direction""
    }
  ]
}");
        File.WriteAllText(Path.Combine(artRoot, "art_tasks.json"), @"{
  ""schemaVersion"": 1,
  ""projectId"": ""wgame"",
  ""updated"": ""2026-06-10"",
  ""workstreams"": [
    {
      ""workstreamId"": ""known"",
      ""title"": ""Known"",
      ""phase"": ""source"",
      ""status"": ""active"",
      ""summary"": """",
      ""nextAction"": """",
      ""sortOrder"": 10
    }
  ],
  ""tasks"": [
    {
      ""taskId"": ""task.update"",
      ""workstreamId"": ""known"",
      ""title"": ""Update task"",
      ""status"": ""blocked"",
      ""priority"": ""high"",
      ""assetIds"": [""test.ready""],
      ""blockedBy"": ""story"",
      ""nextAction"": ""等待输入。"",
      ""previewPath"": """",
      ""reviewRefs"": []
    }
  ]
}");
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
            string manifestPath = Path.Combine(current, ArtSourceAuthoringResourceProvider.ManifestRelativePath);
            if (File.Exists(manifestPath))
                return current;

            DirectoryInfo parent = Directory.GetParent(current);
            if (parent == null)
                break;
            current = parent.FullName;
        }

        throw new InvalidOperationException("Could not find repository root with ArtSource manifest.");
    }

    private static string CreateTempRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), "mx-artsource-provider-tests-" + Guid.NewGuid().ToString("N"));
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

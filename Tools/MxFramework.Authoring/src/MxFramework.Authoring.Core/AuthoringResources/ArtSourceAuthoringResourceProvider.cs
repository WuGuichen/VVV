using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    public sealed class ArtSourceAuthoringResourceProvider : IAuthoringResourceProvider
    {
        public const string ManifestRelativePath = "ArtSource/Projects/WGame/art_manifest.json";
        public const string TasksRelativePath = "ArtSource/Projects/WGame/art_tasks.json";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public string ProviderId
        {
            get { return AuthoringResourceProviderIds.ArtSource; }
        }

        public AuthoringResourceProviderDescriptor Describe(AuthoringResourceProviderContext context)
        {
            string manifestPath = ResolveManifestPath(context);
            bool available = File.Exists(manifestPath);
            return new AuthoringResourceProviderDescriptor
            {
                ProviderId = ProviderId,
                DisplayName = "ArtSource",
                SourceKind = AuthoringResourceSourceKind.ExternalFile,
                Available = available,
                Status = available ? "Ready" : "Unavailable",
                DiagnosticCode = available ? string.Empty : AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                Message = available ? string.Empty : "ArtSource manifest is not available."
            };
        }

        public AuthoringResourceCollection BuildResourceCollection(AuthoringResourceProviderContext context)
        {
            var collection = new AuthoringResourceCollection
            {
                ScopeId = context != null && !string.IsNullOrWhiteSpace(context.ScopeId)
                    ? context.ScopeId
                    : "artSource"
            };

            AuthoringResourceProviderDescriptor descriptor = Describe(context);
            collection.Providers.Add(descriptor);
            AddCollectionMetadata(collection, context);

            string manifestPath = ResolveManifestPath(context);
            if (context == null || string.IsNullOrWhiteSpace(context.ProjectRootPath) || !File.Exists(manifestPath))
            {
                collection.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Warning,
                    Code = AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                    ProviderId = ProviderId,
                    SourceField = ManifestRelativePath,
                    Message = descriptor.Message,
                    SuggestedFix = "Create ArtSource/Projects/WGame/art_manifest.json, then refresh Resource Manager."
                });
                return collection;
            }

            ArtSourceManifestDocument manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<ArtSourceManifestDocument>(File.ReadAllText(manifestPath), JsonOptions);
            }
            catch (JsonException ex)
            {
                collection.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Error,
                    Code = AuthoringResourceDiagnosticCodes.ArtSourceSourceMissing,
                    ProviderId = ProviderId,
                    SourceField = ManifestRelativePath,
                    Message = "ArtSource manifest JSON is invalid: " + ex.Message,
                    SuggestedFix = "Fix art_manifest.json so Resource Manager can derive ArtSource state."
                });
                return collection;
            }

            if (manifest == null || manifest.Assets == null)
                return collection;

            ArtSourceTaskLoadResult tasks = LoadTaskDocument(context, manifest, collection);
            AddTaskCollectionMetadata(collection, tasks);
            Dictionary<string, ArtSourceAssetTaskSummary> taskIndex = BuildAssetTaskIndex(tasks);

            collection.Metadata["artSourceProjectId"] = manifest.ProjectId ?? string.Empty;
            collection.Metadata["artSourceUpdated"] = manifest.Updated ?? string.Empty;
            collection.Metadata["artSourceAssetCount"] = manifest.Assets.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

            for (int i = 0; i < manifest.Assets.Count; i++)
            {
                ArtSourceManifestEntry entry = manifest.Assets[i];
                if (entry == null)
                    continue;

                AuthoringResourceItem item = CreateItem(context, entry, i, taskIndex);
                AddItemDiagnostics(context, entry, item);
                collection.Items.Add(item);
            }

            return collection;
        }

        private static AuthoringResourceItem CreateItem(
            AuthoringResourceProviderContext context,
            ArtSourceManifestEntry entry,
            int index,
            Dictionary<string, ArtSourceAssetTaskSummary> taskIndex)
        {
            string assetId = FirstNonEmpty(entry.AssetId, "art.asset." + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string stableId = "art.source." + AuthoringResourceProviderUtilities.SanitizeStableSegment(assetId);
            string displayName = FirstNonEmpty(assetId, entry.SourcePath, stableId);
            string kind = FirstNonEmpty(entry.Family, "art");
            string usage = FirstNonEmpty(entry.Stage, "source");
            bool sourceExists = PathExists(context, entry.SourcePath);
            string previewPath = SelectPreviewPath(entry);
            bool previewExists = FilePathExists(context, previewPath);
            var item = new AuthoringResourceItem
            {
                ResourceId = AuthoringResourceProviderUtilities.BuildResourceId(AuthoringResourceProviderIds.ArtSource, stableId, assetId),
                StableId = stableId,
                DisplayName = displayName,
                CanonicalName = displayName,
                CanonicalIdentity = assetId,
                TechnicalIdentity = entry.SourcePath ?? string.Empty,
                Kind = kind,
                Usage = usage,
                SourceProviderId = AuthoringResourceProviderIds.ArtSource,
                SourceKind = AuthoringResourceSourceKind.ExternalFile,
                BindingKind = AuthoringResourceBindingKind.ExternalSource,
                ImportStatus = sourceExists ? AuthoringResourceImportStatus.Clean : AuthoringResourceImportStatus.New,
                RuntimeAvailability = AuthoringResourceRuntimeAvailability.NotRuntimeLoadable,
                Tags = BuildTags(entry)
            };

            AddMetadata(item, context, entry, sourceExists, previewPath, previewExists);
            AddTaskMetadata(item, entry, taskIndex);
            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.ArtSource,
                BindingKind = AuthoringResourceBindingKind.ExternalSource,
                BindingKeyKind = AuthoringResourceBindingKeyKinds.ExternalSourcePath,
                DisplayValue = entry.SourcePath ?? string.Empty,
                IsPrimary = true,
                ProviderResourceKey = assetId,
                ExternalSourcePath = entry.SourcePath ?? string.Empty,
                AssetType = kind,
                ProviderData = new Dictionary<string, string>(item.Metadata, StringComparer.Ordinal)
            });

            return item;
        }

        private static void AddMetadata(
            AuthoringResourceItem item,
            AuthoringResourceProviderContext context,
            ArtSourceManifestEntry entry,
            bool sourceExists,
            string previewPath,
            bool previewExists)
        {
            item.Metadata["assetId"] = entry.AssetId ?? string.Empty;
            item.Metadata["family"] = entry.Family ?? string.Empty;
            item.Metadata["stage"] = entry.Stage ?? string.Empty;
            item.Metadata["sourcePath"] = entry.SourcePath ?? string.Empty;
            item.Metadata["exportPath"] = entry.ExportPath ?? string.Empty;
            item.Metadata["unityTargetPath"] = entry.UnityTargetPath ?? string.Empty;
            item.Metadata["resourceKey"] = entry.ResourceKey ?? string.Empty;
            item.Metadata["reviewStatus"] = entry.ReviewStatus ?? string.Empty;
            item.Metadata["previewPath"] = previewPath ?? string.Empty;
            item.Metadata["previewUrl"] = previewExists ? ToStaticUrl(previewPath) : string.Empty;
            item.Metadata["previewExists"] = previewExists ? "true" : "false";
            item.Metadata["previewRole"] = entry.PreviewRole ?? string.Empty;
            item.Metadata["previewNote"] = entry.PreviewNote ?? string.Empty;
            item.Metadata["sourceExists"] = sourceExists ? "true" : "false";
            item.Metadata["sourceLoadability"] = item.RuntimeAvailability.ToString();
            item.Metadata["runtimeAvailability"] = item.RuntimeAvailability.ToString();
            item.Metadata["bindingKind"] = item.BindingKind.ToString();
            item.Metadata["artSourceManifestPath"] = ManifestRelativePath;
            if (context != null)
                item.Metadata["projectRootPath"] = context.ProjectRootPath ?? string.Empty;
        }

        private static void AddTaskMetadata(
            AuthoringResourceItem item,
            ArtSourceManifestEntry entry,
            Dictionary<string, ArtSourceAssetTaskSummary> taskIndex)
        {
            ArtSourceAssetTaskSummary summary;
            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.AssetId) ||
                taskIndex == null ||
                !taskIndex.TryGetValue(entry.AssetId, out summary))
            {
                item.Metadata["workstreamIds"] = string.Empty;
                item.Metadata["taskIds"] = string.Empty;
                item.Metadata["activeTaskCount"] = "0";
                item.Metadata["blockedTaskCount"] = "0";
                return;
            }

            item.Metadata["workstreamIds"] = string.Join(",", summary.WorkstreamIds);
            item.Metadata["taskIds"] = string.Join(",", summary.TaskIds);
            item.Metadata["activeTaskCount"] = summary.ActiveTaskCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            item.Metadata["blockedTaskCount"] = summary.BlockedTaskCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void AddItemDiagnostics(
            AuthoringResourceProviderContext context,
            ArtSourceManifestEntry entry,
            AuthoringResourceItem item)
        {
            if (string.IsNullOrWhiteSpace(entry.SourcePath) || !PathExists(context, entry.SourcePath))
            {
                AddDiagnostic(
                    item,
                    CharacterAuthoringValidationSeverity.Warning,
                    AuthoringResourceDiagnosticCodes.ArtSourceSourceMissing,
                    "ArtSource source path is missing or does not exist: " + FirstNonEmpty(entry.SourcePath, "(empty)"),
                    "Create the source item or update art_manifest.json to the correct source path.",
                    "sourcePath");
            }

            if (EqualsStatus(entry.ReviewStatus, "waiting-story-input"))
            {
                AddDiagnostic(
                    item,
                    CharacterAuthoringValidationSeverity.Warning,
                    AuthoringResourceDiagnosticCodes.ArtSourceStoryInputWaiting,
                    "Art production is waiting for an approved Story Input Packet.",
                    "Provide an approved Story Input Packet before marking this as a production reference.",
                    "reviewStatus");
            }

            if (IsReviewPending(entry))
            {
                AddDiagnostic(
                    item,
                    CharacterAuthoringValidationSeverity.Warning,
                    AuthoringResourceDiagnosticCodes.ArtSourceReviewPending,
                    "ArtSource item has not passed review yet.",
                    "Run the matching review checklist and update art_manifest.json with the review outcome.",
                    "reviewStatus");
            }

            if (RequiresExport(entry) && string.IsNullOrWhiteSpace(entry.ExportPath))
            {
                AddDiagnostic(
                    item,
                    CharacterAuthoringValidationSeverity.Info,
                    AuthoringResourceDiagnosticCodes.ArtSourceExportPending,
                    "Clean export handoff has not been produced yet.",
                    "Create an export only after source/story gates pass and an import target is selected.",
                    "exportPath");
            }

            if (RequiresUnityImport(entry) && string.IsNullOrWhiteSpace(entry.UnityTargetPath))
            {
                AddDiagnostic(
                    item,
                    CharacterAuthoringValidationSeverity.Info,
                    AuthoringResourceDiagnosticCodes.ArtSourceUnityImportPending,
                    "Unity or FairyGUI target path has not been selected yet.",
                    "Promote through Unity/FairyGUI tooling in a separate Unity-facing task.",
                    "unityTargetPath");
            }

            string previewPath = SelectPreviewPath(entry);
            if (RequiresPreview(entry) && (string.IsNullOrWhiteSpace(previewPath) || !FilePathExists(context, previewPath)))
            {
                AddDiagnostic(
                    item,
                    CharacterAuthoringValidationSeverity.Warning,
                    AuthoringResourceDiagnosticCodes.ArtSourcePreviewMissing,
                    "ArtSource item has no real preview image available.",
                    "Add a real previewPath, create the referenced image source/export, or keep the item in a blocked/fallback state.",
                    "previewPath");
            }
        }

        private static void AddDiagnostic(
            AuthoringResourceItem item,
            CharacterAuthoringValidationSeverity severity,
            string code,
            string message,
            string suggestedFix,
            string sourceField)
        {
            item.Diagnostics.Add(new AuthoringResourceDiagnostic
            {
                Severity = severity,
                Code = code ?? string.Empty,
                ResourceId = item.ResourceId,
                ResourceStableId = item.StableId,
                RuntimeResourceKey = item.Metadata.TryGetValue("resourceKey", out string key) ? key : string.Empty,
                ProviderId = AuthoringResourceProviderIds.ArtSource,
                SourceConfigKind = "art_manifest",
                SourceStableId = item.Metadata.TryGetValue("assetId", out string assetId) ? assetId : string.Empty,
                SourceField = sourceField ?? string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            });
        }

        private static List<string> BuildTags(ArtSourceManifestEntry entry)
        {
            var tags = new List<string> { "art-source" };
            AddTag(tags, entry.Family);
            AddTag(tags, entry.Stage);
            AddTag(tags, entry.ReviewStatus);
            return tags;
        }

        private static void AddCollectionMetadata(AuthoringResourceCollection collection, AuthoringResourceProviderContext context)
        {
            collection.Metadata["artSourceManifestPath"] = ManifestRelativePath;
            collection.Metadata["artSourceTasksPath"] = TasksRelativePath;
            if (context != null)
                collection.Metadata["projectRootPath"] = context.ProjectRootPath ?? string.Empty;
        }

        private static ArtSourceTaskLoadResult LoadTaskDocument(
            AuthoringResourceProviderContext context,
            ArtSourceManifestDocument manifest,
            AuthoringResourceCollection collection)
        {
            var result = new ArtSourceTaskLoadResult { Context = context };
            string tasksPath = ResolveTasksPath(context);
            if (context == null || string.IsNullOrWhiteSpace(context.ProjectRootPath) || !File.Exists(tasksPath))
            {
                collection.Diagnostics.Add(CreateTaskDiagnostic(
                    CharacterAuthoringValidationSeverity.Warning,
                    AuthoringResourceDiagnosticCodes.ArtSourceTasksMissing,
                    string.Empty,
                    "ArtSource task file is not available: " + TasksRelativePath,
                    "Create ArtSource/Projects/WGame/art_tasks.json to drive the multi-workstream Art workspace.",
                    TasksRelativePath));
                return result;
            }

            try
            {
                result.Document = JsonSerializer.Deserialize<ArtSourceTaskDocument>(File.ReadAllText(tasksPath), JsonOptions);
                result.Available = true;
            }
            catch (JsonException ex)
            {
                collection.Diagnostics.Add(CreateTaskDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.ArtSourceTasksJsonInvalid,
                    string.Empty,
                    "ArtSource task JSON is invalid: " + ex.Message,
                    "Fix art_tasks.json so Resource Manager can derive multi-workstream status.",
                    TasksRelativePath));
                return result;
            }

            if (result.Document == null)
                result.Document = new ArtSourceTaskDocument();

            if (result.Document.Workstreams == null)
                result.Document.Workstreams = new List<ArtSourceWorkstreamEntry>();
            if (result.Document.Tasks == null)
                result.Document.Tasks = new List<ArtSourceTaskEntry>();

            ValidateTaskReferences(manifest, result.Document, collection);
            return result;
        }

        private static void AddTaskCollectionMetadata(AuthoringResourceCollection collection, ArtSourceTaskLoadResult tasks)
        {
            ArtSourceTaskDocument document = tasks != null ? tasks.Document : null;
            int workstreamCount = document != null && document.Workstreams != null ? document.Workstreams.Count : 0;
            int taskCount = document != null && document.Tasks != null ? document.Tasks.Count : 0;
            collection.Metadata["artSourceTasksAvailable"] = tasks != null && tasks.Available ? "true" : "false";
            collection.Metadata["artSourceWorkstreamCount"] = workstreamCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            collection.Metadata["artSourceTaskCount"] = taskCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            collection.Metadata["artSourceTasksHash"] = ComputeTaskDocumentHash(tasks);
            collection.Metadata["artSourceTaskSnapshotJson"] = JsonSerializer.Serialize(CreateTaskSnapshot(tasks), JsonOptions);
        }

        private static string ComputeTaskDocumentHash(ArtSourceTaskLoadResult tasks)
        {
            if (tasks == null || tasks.Context == null || string.IsNullOrWhiteSpace(tasks.Context.ProjectRootPath))
                return string.Empty;

            string tasksPath = ResolveTasksPath(tasks.Context);
            return File.Exists(tasksPath) ? CharacterPackageHashUtility.ComputeFileSha256(tasksPath) : string.Empty;
        }

        private static ArtSourceTaskSnapshot CreateTaskSnapshot(ArtSourceTaskLoadResult tasks)
        {
            var snapshot = new ArtSourceTaskSnapshot();
            ArtSourceTaskDocument document = tasks != null ? tasks.Document : null;
            if (document == null)
                return snapshot;

            snapshot.SchemaVersion = document.SchemaVersion;
            snapshot.ProjectId = document.ProjectId ?? string.Empty;
            snapshot.Updated = document.Updated ?? string.Empty;
            ArtSourceApprovedStoryPacket approvedStory = FindApprovedStoryPacket(tasks != null ? tasks.Context : null);

            if (document.Workstreams != null)
            {
                for (int i = 0; i < document.Workstreams.Count; i++)
                {
                    ArtSourceWorkstreamEntry source = document.Workstreams[i];
                    if (source == null)
                        continue;

                    snapshot.Workstreams.Add(new ArtSourceWorkstreamSnapshot
                    {
                        WorkstreamId = source.WorkstreamId ?? string.Empty,
                        Title = source.Title ?? string.Empty,
                        Phase = source.Phase ?? string.Empty,
                        Status = source.Status ?? string.Empty,
                        Summary = source.Summary ?? string.Empty,
                        NextAction = source.NextAction ?? string.Empty,
                        SortOrder = source.SortOrder
                    });
                }
            }

            if (document.Tasks != null)
            {
                for (int i = 0; i < document.Tasks.Count; i++)
                {
                    ArtSourceTaskEntry source = document.Tasks[i];
                    if (source == null)
                        continue;

                    string status = source.Status ?? string.Empty;
                    string blockedBy = source.BlockedBy ?? string.Empty;
                    string nextAction = source.NextAction ?? string.Empty;
                    List<string> reviewRefs = source.ReviewRefs ?? new List<string>();
                    ApplyStoryGateOverride(source.TaskId, approvedStory, ref status, ref blockedBy, ref nextAction, ref reviewRefs);
                    string previewPath = source.PreviewPath ?? string.Empty;
                    bool previewExists = !string.IsNullOrWhiteSpace(previewPath) && tasks != null && FilePathExists(tasks.Context, previewPath);
                    snapshot.Tasks.Add(new ArtSourceTaskSnapshotEntry
                    {
                        TaskId = source.TaskId ?? string.Empty,
                        WorkstreamId = source.WorkstreamId ?? string.Empty,
                        Title = source.Title ?? string.Empty,
                        Status = status,
                        Priority = source.Priority ?? string.Empty,
                        AssetIds = source.AssetIds ?? new List<string>(),
                        BlockedBy = blockedBy,
                        NextAction = nextAction,
                        PreviewPath = previewPath,
                        PreviewUrl = previewExists ? ToStaticUrl(previewPath) : string.Empty,
                        PreviewExists = previewExists,
                        ReviewRefs = reviewRefs
                    });
                }
            }

            return snapshot;
        }

        private static void ApplyStoryGateOverride(
            string taskId,
            ArtSourceApprovedStoryPacket approvedStory,
            ref string status,
            ref string blockedBy,
            ref string nextAction,
            ref List<string> reviewRefs)
        {
            if (approvedStory == null || string.IsNullOrWhiteSpace(approvedStory.StorySourceId))
                return;

            if (EqualsStatus(taskId, "story.input_packet.approval"))
            {
                if (!EqualsAny(status, "completed", "done"))
                    status = "ready";
                blockedBy = string.Empty;
                nextAction = "Story Input Packet 已批准给美术，可以切到参考图任务开始生产。";
                AddListValue(reviewRefs, approvedStory.PacketPath);
                return;
            }

            if (EqualsStatus(blockedBy, "story.input_packet.approval"))
            {
                status = EqualsAny(status, "blocked", "waiting") ? "active" : status;
                blockedBy = string.Empty;
                nextAction = FirstNonEmpty(nextAction, "基于 " + approvedStory.StorySourceId + " " + approvedStory.Version + " 生成或评审正式参考图。");
                AddListValue(reviewRefs, approvedStory.PacketPath);
            }
        }

        private static ArtSourceApprovedStoryPacket FindApprovedStoryPacket(AuthoringResourceProviderContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.ProjectRootPath))
                return null;

            StorySourceVerifiedPacket verified = StorySourceAuthoringValidator.FindVerifiedApprovedArtPacket(
                context.ProjectRootPath,
                "story.input_packet.approval");
            if (verified == null)
                return null;

            return new ArtSourceApprovedStoryPacket
            {
                StorySourceId = verified.StorySourceId ?? string.Empty,
                Version = verified.Version ?? string.Empty,
                PacketPath = verified.PacketPath ?? string.Empty
            };
        }

        private static void ValidateTaskReferences(
            ArtSourceManifestDocument manifest,
            ArtSourceTaskDocument taskDocument,
            AuthoringResourceCollection collection)
        {
            var workstreamIds = new HashSet<string>(StringComparer.Ordinal);
            if (taskDocument.Workstreams != null)
            {
                for (int i = 0; i < taskDocument.Workstreams.Count; i++)
                    AddSetValue(workstreamIds, taskDocument.Workstreams[i] != null ? taskDocument.Workstreams[i].WorkstreamId : string.Empty);
            }

            var assetIds = new HashSet<string>(StringComparer.Ordinal);
            if (manifest != null && manifest.Assets != null)
            {
                for (int i = 0; i < manifest.Assets.Count; i++)
                    AddSetValue(assetIds, manifest.Assets[i] != null ? manifest.Assets[i].AssetId : string.Empty);
            }

            if (taskDocument.Tasks == null)
                return;

            for (int i = 0; i < taskDocument.Tasks.Count; i++)
            {
                ArtSourceTaskEntry task = taskDocument.Tasks[i];
                if (task == null)
                    continue;

                string taskId = FirstNonEmpty(task.TaskId, "task[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]");
                if (string.IsNullOrWhiteSpace(task.WorkstreamId) || !workstreamIds.Contains(task.WorkstreamId))
                {
                    collection.Diagnostics.Add(CreateTaskDiagnostic(
                        CharacterAuthoringValidationSeverity.Warning,
                        AuthoringResourceDiagnosticCodes.ArtSourceTaskWorkstreamMissing,
                        taskId,
                        "ArtSource task references a missing workstream: " + FirstNonEmpty(task.WorkstreamId, "(empty)"),
                        "Add the workstream to art_tasks.json or move the task to an existing workstream.",
                        "workstreamId"));
                }

                if (task.AssetIds == null)
                    continue;

                for (int assetIndex = 0; assetIndex < task.AssetIds.Count; assetIndex++)
                {
                    string assetId = task.AssetIds[assetIndex] ?? string.Empty;
                    if (!assetIds.Contains(assetId))
                    {
                        collection.Diagnostics.Add(CreateTaskDiagnostic(
                            CharacterAuthoringValidationSeverity.Warning,
                            AuthoringResourceDiagnosticCodes.ArtSourceTaskAssetMissing,
                            taskId,
                            "ArtSource task references a missing manifest assetId: " + FirstNonEmpty(assetId, "(empty)"),
                            "Add the asset to art_manifest.json or remove the stale task asset reference.",
                            "assetIds"));
                    }
                }
            }
        }

        private static Dictionary<string, ArtSourceAssetTaskSummary> BuildAssetTaskIndex(ArtSourceTaskLoadResult tasks)
        {
            var index = new Dictionary<string, ArtSourceAssetTaskSummary>(StringComparer.Ordinal);
            ArtSourceTaskDocument document = tasks != null ? tasks.Document : null;
            if (document == null || document.Tasks == null)
                return index;

            for (int i = 0; i < document.Tasks.Count; i++)
            {
                ArtSourceTaskEntry task = document.Tasks[i];
                if (task == null || task.AssetIds == null)
                    continue;

                for (int assetIndex = 0; assetIndex < task.AssetIds.Count; assetIndex++)
                {
                    string assetId = task.AssetIds[assetIndex] ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(assetId))
                        continue;

                    ArtSourceAssetTaskSummary summary;
                    if (!index.TryGetValue(assetId, out summary))
                    {
                        summary = new ArtSourceAssetTaskSummary();
                        index[assetId] = summary;
                    }

                    AddListValue(summary.TaskIds, task.TaskId);
                    AddListValue(summary.WorkstreamIds, task.WorkstreamId);
                    if (IsActiveTaskStatus(task.Status))
                        summary.ActiveTaskCount++;
                    if (IsBlockedTask(task))
                        summary.BlockedTaskCount++;
                }
            }

            return index;
        }

        private static AuthoringResourceDiagnostic CreateTaskDiagnostic(
            CharacterAuthoringValidationSeverity severity,
            string code,
            string taskId,
            string message,
            string suggestedFix,
            string sourceField)
        {
            return new AuthoringResourceDiagnostic
            {
                Severity = severity,
                Code = code ?? string.Empty,
                ProviderId = AuthoringResourceProviderIds.ArtSource,
                SourceConfigKind = "art_tasks",
                SourceStableId = taskId ?? string.Empty,
                SourceField = sourceField ?? string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            };
        }

        private static bool IsActiveTaskStatus(string status)
        {
            string value = status ?? string.Empty;
            return !EqualsAny(value, "done", "completed", "cancelled", "canceled", "archived");
        }

        private static bool IsBlockedTask(ArtSourceTaskEntry task)
        {
            if (task == null)
                return false;
            return EqualsAny(task.Status, "blocked") || !string.IsNullOrWhiteSpace(task.BlockedBy);
        }

        private static bool RequiresExport(ArtSourceManifestEntry entry)
        {
            if (entry == null || EqualsStatus(entry.ReviewStatus, "waiting-story-input"))
                return false;
            string family = entry.Family ?? string.Empty;
            return EqualsAny(family, "reference", "ui", "environment", "character", "props", "vfx", "marker", "decal");
        }

        private static bool RequiresUnityImport(ArtSourceManifestEntry entry)
        {
            if (entry == null || EqualsStatus(entry.ReviewStatus, "waiting-story-input"))
                return false;
            if (string.IsNullOrWhiteSpace(entry.ExportPath))
                return false;
            string family = entry.Family ?? string.Empty;
            return EqualsAny(family, "ui", "environment", "character", "props", "vfx", "marker", "decal", "material", "shader");
        }

        private static bool RequiresPreview(ArtSourceManifestEntry entry)
        {
            if (entry == null)
                return false;
            string family = entry.Family ?? string.Empty;
            return EqualsAny(family, "reference", "ui", "lookdev", "shader", "character", "environment", "props", "vfx", "marker", "decal");
        }

        private static bool IsReviewPending(ArtSourceManifestEntry entry)
        {
            if (entry == null)
                return false;
            string status = entry.ReviewStatus ?? string.Empty;
            return string.IsNullOrWhiteSpace(status) ||
                EqualsStatus(status, "unreviewed") ||
                EqualsStatus(status, "planned-source");
        }

        private static bool PathExists(AuthoringResourceProviderContext context, string projectRelativePath)
        {
            if (context == null || string.IsNullOrWhiteSpace(projectRelativePath))
                return false;
            string path = AuthoringResourceProviderUtilities.ResolveProjectPath(context.ProjectRootPath, projectRelativePath);
            return File.Exists(path) || Directory.Exists(path);
        }

        private static bool FilePathExists(AuthoringResourceProviderContext context, string projectRelativePath)
        {
            if (context == null || string.IsNullOrWhiteSpace(projectRelativePath))
                return false;
            string path = AuthoringResourceProviderUtilities.ResolveProjectPath(context.ProjectRootPath, projectRelativePath);
            return File.Exists(path);
        }

        private static string SelectPreviewPath(ArtSourceManifestEntry entry)
        {
            if (entry == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(entry.PreviewPath))
                return entry.PreviewPath;
            if (IsImagePath(entry.SourcePath))
                return entry.SourcePath;
            if (IsImagePath(entry.ExportPath))
                return entry.ExportPath;
            return string.Empty;
        }

        private static bool IsImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            string extension = Path.GetExtension(path);
            return EqualsAny(extension, ".png", ".jpg", ".jpeg", ".webp");
        }

        private static string ToStaticUrl(string projectRelativePath)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath))
                return string.Empty;
            string normalized = projectRelativePath.Replace('\\', '/').TrimStart('/');
            string[] parts = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return string.Empty;

            var encoded = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
                encoded.Add(Uri.EscapeDataString(parts[i]));
            return "/" + string.Join("/", encoded);
        }

        private static string ResolveManifestPath(AuthoringResourceProviderContext context)
        {
            return context != null
                ? AuthoringResourceProviderUtilities.ResolveProjectPath(context.ProjectRootPath, ManifestRelativePath)
                : ManifestRelativePath;
        }

        private static string ResolveTasksPath(AuthoringResourceProviderContext context)
        {
            return context != null
                ? AuthoringResourceProviderUtilities.ResolveProjectPath(context.ProjectRootPath, TasksRelativePath)
                : TasksRelativePath;
        }

        private static void AddSetValue(HashSet<string> values, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                values.Add(value);
        }

        private static void AddListValue(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value) || values.Contains(value))
                return;
            values.Add(value);
        }

        private static bool EqualsAny(string value, params string[] options)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (string.Equals(value, options[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool EqualsStatus(string value, string expected)
        {
            return string.Equals(value ?? string.Empty, expected ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return AuthoringResourceProviderUtilities.FirstNonEmpty(values);
        }

        private static void AddTag(List<string> tags, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                tags.Add(value);
        }

        private sealed class ArtSourceTaskLoadResult
        {
            public bool Available { get; set; }
            public AuthoringResourceProviderContext Context { get; set; }
            public ArtSourceTaskDocument Document { get; set; } = new ArtSourceTaskDocument();
        }

        private sealed class ArtSourceAssetTaskSummary
        {
            public List<string> WorkstreamIds { get; } = new List<string>();
            public List<string> TaskIds { get; } = new List<string>();
            public int ActiveTaskCount { get; set; }
            public int BlockedTaskCount { get; set; }
        }

        private sealed class ArtSourceApprovedStoryPacket
        {
            public string StorySourceId { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
            public string PacketPath { get; set; } = string.Empty;
        }

        private sealed class ArtSourceManifestDocument
        {
            [JsonPropertyName("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonPropertyName("projectId")]
            public string ProjectId { get; set; } = string.Empty;

            [JsonPropertyName("updated")]
            public string Updated { get; set; } = string.Empty;

            [JsonPropertyName("contract")]
            public string Contract { get; set; } = string.Empty;

            [JsonPropertyName("assets")]
            public List<ArtSourceManifestEntry> Assets { get; set; } = new List<ArtSourceManifestEntry>();
        }

        private sealed class ArtSourceManifestEntry
        {
            [JsonPropertyName("assetId")]
            public string AssetId { get; set; } = string.Empty;

            [JsonPropertyName("family")]
            public string Family { get; set; } = string.Empty;

            [JsonPropertyName("stage")]
            public string Stage { get; set; } = string.Empty;

            [JsonPropertyName("sourcePath")]
            public string SourcePath { get; set; } = string.Empty;

            [JsonPropertyName("exportPath")]
            public string ExportPath { get; set; } = string.Empty;

            [JsonPropertyName("unityTargetPath")]
            public string UnityTargetPath { get; set; } = string.Empty;

            [JsonPropertyName("resourceKey")]
            public string ResourceKey { get; set; } = string.Empty;

            [JsonPropertyName("reviewStatus")]
            public string ReviewStatus { get; set; } = string.Empty;

            [JsonPropertyName("previewPath")]
            public string PreviewPath { get; set; } = string.Empty;

            [JsonPropertyName("previewRole")]
            public string PreviewRole { get; set; } = string.Empty;

            [JsonPropertyName("previewNote")]
            public string PreviewNote { get; set; } = string.Empty;
        }

        private sealed class ArtSourceTaskDocument
        {
            [JsonPropertyName("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonPropertyName("projectId")]
            public string ProjectId { get; set; } = string.Empty;

            [JsonPropertyName("updated")]
            public string Updated { get; set; } = string.Empty;

            [JsonPropertyName("workstreams")]
            public List<ArtSourceWorkstreamEntry> Workstreams { get; set; } = new List<ArtSourceWorkstreamEntry>();

            [JsonPropertyName("tasks")]
            public List<ArtSourceTaskEntry> Tasks { get; set; } = new List<ArtSourceTaskEntry>();
        }

        private sealed class ArtSourceWorkstreamEntry
        {
            [JsonPropertyName("workstreamId")]
            public string WorkstreamId { get; set; } = string.Empty;

            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [JsonPropertyName("phase")]
            public string Phase { get; set; } = string.Empty;

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("summary")]
            public string Summary { get; set; } = string.Empty;

            [JsonPropertyName("nextAction")]
            public string NextAction { get; set; } = string.Empty;

            [JsonPropertyName("sortOrder")]
            public int SortOrder { get; set; }
        }

        private sealed class ArtSourceTaskEntry
        {
            [JsonPropertyName("taskId")]
            public string TaskId { get; set; } = string.Empty;

            [JsonPropertyName("workstreamId")]
            public string WorkstreamId { get; set; } = string.Empty;

            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("priority")]
            public string Priority { get; set; } = string.Empty;

            [JsonPropertyName("assetIds")]
            public List<string> AssetIds { get; set; } = new List<string>();

            [JsonPropertyName("blockedBy")]
            public string BlockedBy { get; set; } = string.Empty;

            [JsonPropertyName("nextAction")]
            public string NextAction { get; set; } = string.Empty;

            [JsonPropertyName("previewPath")]
            public string PreviewPath { get; set; } = string.Empty;

            [JsonPropertyName("reviewRefs")]
            public List<string> ReviewRefs { get; set; } = new List<string>();
        }

        private sealed class ArtSourceTaskSnapshot
        {
            [JsonPropertyName("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonPropertyName("projectId")]
            public string ProjectId { get; set; } = string.Empty;

            [JsonPropertyName("updated")]
            public string Updated { get; set; } = string.Empty;

            [JsonPropertyName("workstreams")]
            public List<ArtSourceWorkstreamSnapshot> Workstreams { get; set; } = new List<ArtSourceWorkstreamSnapshot>();

            [JsonPropertyName("tasks")]
            public List<ArtSourceTaskSnapshotEntry> Tasks { get; set; } = new List<ArtSourceTaskSnapshotEntry>();
        }

        private sealed class ArtSourceWorkstreamSnapshot
        {
            [JsonPropertyName("workstreamId")]
            public string WorkstreamId { get; set; } = string.Empty;

            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [JsonPropertyName("phase")]
            public string Phase { get; set; } = string.Empty;

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("summary")]
            public string Summary { get; set; } = string.Empty;

            [JsonPropertyName("nextAction")]
            public string NextAction { get; set; } = string.Empty;

            [JsonPropertyName("sortOrder")]
            public int SortOrder { get; set; }
        }

        private sealed class ArtSourceTaskSnapshotEntry
        {
            [JsonPropertyName("taskId")]
            public string TaskId { get; set; } = string.Empty;

            [JsonPropertyName("workstreamId")]
            public string WorkstreamId { get; set; } = string.Empty;

            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("priority")]
            public string Priority { get; set; } = string.Empty;

            [JsonPropertyName("assetIds")]
            public List<string> AssetIds { get; set; } = new List<string>();

            [JsonPropertyName("blockedBy")]
            public string BlockedBy { get; set; } = string.Empty;

            [JsonPropertyName("nextAction")]
            public string NextAction { get; set; } = string.Empty;

            [JsonPropertyName("previewPath")]
            public string PreviewPath { get; set; } = string.Empty;

            [JsonPropertyName("previewUrl")]
            public string PreviewUrl { get; set; } = string.Empty;

            [JsonPropertyName("previewExists")]
            public bool PreviewExists { get; set; }

            [JsonPropertyName("reviewRefs")]
            public List<string> ReviewRefs { get; set; } = new List<string>();
        }
    }
}

using System.Net;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    internal static ArtSourceTaskUpdateResponse SaveArtSourceTaskUpdate(string rootPath, ArtSourceTaskUpdateRequest request, JsonSerializerOptions jsonOptions)
    {
        request ??= new ArtSourceTaskUpdateRequest();
        string tasksPath = Path.Combine(rootPath, ArtSourceAuthoringResourceProvider.TasksRelativePath);
        string manifestPath = Path.Combine(rootPath, ArtSourceAuthoringResourceProvider.ManifestRelativePath);
        string relativeTasksPath = ToProjectRelativePath(rootPath, tasksPath);

        if (string.IsNullOrWhiteSpace(request.taskId))
            return ArtSourceTaskUpdateResponse.Invalid(relativeTasksPath, "必须提供任务 id。");
        if (!File.Exists(tasksPath))
            return ArtSourceTaskUpdateResponse.Invalid(relativeTasksPath, "美术任务文件不存在：" + ArtSourceAuthoringResourceProvider.TasksRelativePath);
        if (!File.Exists(manifestPath))
            return ArtSourceTaskUpdateResponse.Invalid(relativeTasksPath, "美术 manifest 文件不存在：" + ArtSourceAuthoringResourceProvider.ManifestRelativePath);

        string currentHash = CharacterPackageHashUtility.ComputeFileSha256(tasksPath);
        string expectedHash = CharacterPackageHashUtility.NormalizeSha256(request.expectedHash);
        if (string.IsNullOrWhiteSpace(expectedHash))
            return ArtSourceTaskUpdateResponse.Invalid(relativeTasksPath, "保存 art_tasks.json 前必须提供 expectedHash。", request.taskId, currentHash);
        if (!string.Equals(expectedHash, currentHash, StringComparison.Ordinal))
            return ArtSourceTaskUpdateResponse.Conflict(relativeTasksPath, request.taskId, expectedHash, currentHash);

        ArtSourceTaskDocument tasks;
        ArtSourceManifestDocument manifest;
        try
        {
            tasks = JsonSerializer.Deserialize<ArtSourceTaskDocument>(File.ReadAllText(tasksPath), jsonOptions) ?? new ArtSourceTaskDocument();
        }
        catch (JsonException ex)
        {
            return ArtSourceTaskUpdateResponse.Invalid(relativeTasksPath, "美术任务 JSON 无效：" + ex.Message, request.taskId, currentHash);
        }

        try
        {
            manifest = JsonSerializer.Deserialize<ArtSourceManifestDocument>(File.ReadAllText(manifestPath), jsonOptions) ?? new ArtSourceManifestDocument();
        }
        catch (JsonException ex)
        {
            return ArtSourceTaskUpdateResponse.Invalid(relativeTasksPath, "美术 manifest JSON 无效：" + ex.Message, request.taskId, currentHash);
        }

        tasks.Workstreams ??= new List<ArtSourceWorkstreamEntry>();
        tasks.Tasks ??= new List<ArtSourceTaskEntry>();
        manifest.Assets ??= new List<ArtSourceManifestEntry>();

        ArtSourceTaskEntry task = tasks.Tasks.FirstOrDefault(item => string.Equals(item != null ? item.TaskId : string.Empty, request.taskId, StringComparison.Ordinal));
        if (task == null)
            return ArtSourceTaskUpdateResponse.Invalid(relativeTasksPath, "找不到美术任务：" + request.taskId, request.taskId, currentHash);

        string status = NormalizeArtTaskStatus(request.status);
        if (string.IsNullOrWhiteSpace(status))
            return ArtSourceTaskUpdateResponse.Invalid(relativeTasksPath, "不支持的美术任务状态：" + (request.status ?? string.Empty), request.taskId, currentHash);

        string priority = NormalizeArtTaskPriority(request.priority);
        if (string.IsNullOrWhiteSpace(priority))
            return ArtSourceTaskUpdateResponse.Invalid(relativeTasksPath, "不支持的美术任务优先级：" + (request.priority ?? string.Empty), request.taskId, currentHash);

        HashSet<string> knownAssetIds = BuildArtSourceAssetIdSet(manifest);
        List<string> assetIds = NormalizeArtTaskAssetIds(request.assetIds);
        for (int i = 0; i < assetIds.Count; i++)
        {
            if (!knownAssetIds.Contains(assetIds[i]))
                return ArtSourceTaskUpdateResponse.AssetInvalid(relativeTasksPath, request.taskId, assetIds[i], currentHash);
        }

        task.Status = status;
        task.Priority = priority;
        task.BlockedBy = (request.blockedBy ?? string.Empty).Trim();
        task.NextAction = (request.nextAction ?? string.Empty).Trim();
        task.AssetIds = assetIds;
        tasks.Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        WriteJsonFileAtomic(tasksPath, tasks, jsonOptions);
        InvalidateAuthoringResourceCache(rootPath);

        string nextHash = CharacterPackageHashUtility.ComputeFileSha256(tasksPath);
        return ArtSourceTaskUpdateResponse.Success(relativeTasksPath, task.TaskId, currentHash, nextHash);
    }

    internal static ArtSourceTaskCreateResponse SaveArtSourceTaskCreate(string rootPath, ArtSourceTaskCreateRequest request, JsonSerializerOptions jsonOptions)
    {
        request ??= new ArtSourceTaskCreateRequest();
        string tasksPath = Path.Combine(rootPath, ArtSourceAuthoringResourceProvider.TasksRelativePath);
        string manifestPath = Path.Combine(rootPath, ArtSourceAuthoringResourceProvider.ManifestRelativePath);
        string relativeTasksPath = ToProjectRelativePath(rootPath, tasksPath);
        bool tasksFileExists = File.Exists(tasksPath);

        if (!File.Exists(manifestPath))
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "美术 manifest 文件不存在：" + ArtSourceAuthoringResourceProvider.ManifestRelativePath);

        ArtSourceManifestDocument manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ArtSourceManifestDocument>(File.ReadAllText(manifestPath), jsonOptions) ?? new ArtSourceManifestDocument();
        }
        catch (JsonException ex)
        {
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "美术 manifest JSON 无效：" + ex.Message);
        }

        string currentHash = tasksFileExists ? CharacterPackageHashUtility.ComputeFileSha256(tasksPath) : string.Empty;
        string expectedHash = CharacterPackageHashUtility.NormalizeSha256(request.expectedHash);
        if (tasksFileExists)
        {
            if (string.IsNullOrWhiteSpace(expectedHash))
                return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "创建前必须提供当前 art_tasks.json 的 expectedHash。", request.kind, request.taskId, request.workstreamId, currentHash);
            if (!string.Equals(expectedHash, currentHash, StringComparison.Ordinal))
                return ArtSourceTaskCreateResponse.Conflict(relativeTasksPath, request.kind, request.taskId, request.workstreamId, expectedHash, currentHash);
        }
        else if (!string.IsNullOrWhiteSpace(expectedHash))
        {
            return ArtSourceTaskCreateResponse.Conflict(relativeTasksPath, request.kind, request.taskId, request.workstreamId, expectedHash, currentHash);
        }

        ArtSourceTaskDocument tasks;
        if (tasksFileExists)
        {
            try
            {
                tasks = JsonSerializer.Deserialize<ArtSourceTaskDocument>(File.ReadAllText(tasksPath), jsonOptions) ?? new ArtSourceTaskDocument();
            }
            catch (JsonException ex)
            {
                return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "美术任务 JSON 无效：" + ex.Message, request.kind, request.taskId, request.workstreamId, currentHash);
            }
        }
        else
        {
            tasks = new ArtSourceTaskDocument
            {
                SchemaVersion = 1,
                ProjectId = "wgame"
            };
        }

        tasks.Workstreams ??= new List<ArtSourceWorkstreamEntry>();
        tasks.Tasks ??= new List<ArtSourceTaskEntry>();
        manifest.Assets ??= new List<ArtSourceManifestEntry>();

        string kind = (request.kind ?? string.Empty).Trim().ToLowerInvariant();
        if (kind == "workstream")
        {
            ArtSourceTaskCreateResponse validation = ValidateArtWorkstreamCreateRequest(relativeTasksPath, request, tasks, currentHash);
            if (validation != null)
                return validation;

            int sortOrder = request.sortOrder.HasValue
                ? request.sortOrder.Value
                : NextArtWorkstreamSortOrder(tasks);
            tasks.Workstreams.Add(new ArtSourceWorkstreamEntry
            {
                WorkstreamId = request.workstreamId.Trim(),
                Title = request.title.Trim(),
                Phase = string.IsNullOrWhiteSpace(request.phase) ? "art" : request.phase.Trim(),
                Status = NormalizeArtTaskStatus(request.status),
                Summary = (request.summary ?? string.Empty).Trim(),
                NextAction = (request.nextAction ?? string.Empty).Trim(),
                SortOrder = sortOrder
            });
        }
        else if (kind == "task")
        {
            ArtSourceTaskCreateResponse validation = ValidateArtTaskCreateRequest(relativeTasksPath, request, tasks, manifest, currentHash);
            if (validation != null)
                return validation;

            tasks.Tasks.Add(new ArtSourceTaskEntry
            {
                TaskId = request.taskId.Trim(),
                WorkstreamId = request.workstreamId.Trim(),
                Title = request.title.Trim(),
                Status = NormalizeArtTaskStatus(request.status),
                Priority = NormalizeArtTaskPriority(request.priority),
                AssetIds = NormalizeArtTaskAssetIds(request.assetIds),
                BlockedBy = (request.blockedBy ?? string.Empty).Trim(),
                NextAction = (request.nextAction ?? string.Empty).Trim(),
                PreviewPath = (request.previewPath ?? string.Empty).Trim(),
                ReviewRefs = NormalizeArtTaskAssetIds(request.reviewRefs)
            });
        }
        else
        {
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "创建类型必须是 workstream 或 task。", request.kind, request.taskId, request.workstreamId, currentHash);
        }

        tasks.Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Directory.CreateDirectory(Path.GetDirectoryName(tasksPath) ?? rootPath);
        WriteJsonFileAtomic(tasksPath, tasks, jsonOptions);
        InvalidateAuthoringResourceCache(rootPath);

        string nextHash = CharacterPackageHashUtility.ComputeFileSha256(tasksPath);
        return ArtSourceTaskCreateResponse.Success(relativeTasksPath, kind, request.taskId, request.workstreamId, currentHash, nextHash);
    }

    private static ArtSourceTaskCreateResponse ValidateArtWorkstreamCreateRequest(
        string relativeTasksPath,
        ArtSourceTaskCreateRequest request,
        ArtSourceTaskDocument tasks,
        string currentHash)
    {
        string workstreamId = (request.workstreamId ?? string.Empty).Trim();
        if (!IsValidArtTaskId(workstreamId))
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "进程 id 只能使用 a-z、0-9、.、_、-。", request.kind, request.taskId, workstreamId, currentHash);
        if (string.IsNullOrWhiteSpace(request.title))
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "必须提供进程标题。", request.kind, request.taskId, workstreamId, currentHash);
        if (tasks.Workstreams.Any(item => string.Equals(item != null ? item.WorkstreamId : string.Empty, workstreamId, StringComparison.Ordinal)))
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "进程 id 已存在：" + workstreamId, request.kind, request.taskId, workstreamId, currentHash);

        string status = NormalizeArtTaskStatus(request.status);
        if (string.IsNullOrWhiteSpace(status))
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "不支持的进程状态：" + (request.status ?? string.Empty), request.kind, request.taskId, workstreamId, currentHash);

        return null;
    }

    private static ArtSourceTaskCreateResponse ValidateArtTaskCreateRequest(
        string relativeTasksPath,
        ArtSourceTaskCreateRequest request,
        ArtSourceTaskDocument tasks,
        ArtSourceManifestDocument manifest,
        string currentHash)
    {
        string taskId = (request.taskId ?? string.Empty).Trim();
        string workstreamId = (request.workstreamId ?? string.Empty).Trim();
        if (!IsValidArtTaskId(taskId))
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "任务 id 只能使用 a-z、0-9、.、_、-。", request.kind, taskId, workstreamId, currentHash);
        if (!IsValidArtTaskId(workstreamId))
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "必须选择有效进程。", request.kind, taskId, workstreamId, currentHash);
        if (string.IsNullOrWhiteSpace(request.title))
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "必须提供任务标题。", request.kind, taskId, workstreamId, currentHash);
        if (tasks.Tasks.Any(item => string.Equals(item != null ? item.TaskId : string.Empty, taskId, StringComparison.Ordinal)))
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "任务 id 已存在：" + taskId, request.kind, taskId, workstreamId, currentHash);
        if (!tasks.Workstreams.Any(item => string.Equals(item != null ? item.WorkstreamId : string.Empty, workstreamId, StringComparison.Ordinal)))
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "任务所属进程不存在：" + workstreamId, request.kind, taskId, workstreamId, currentHash);

        string status = NormalizeArtTaskStatus(request.status);
        if (string.IsNullOrWhiteSpace(status))
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "不支持的任务状态：" + (request.status ?? string.Empty), request.kind, taskId, workstreamId, currentHash);

        string priority = NormalizeArtTaskPriority(request.priority);
        if (string.IsNullOrWhiteSpace(priority))
            return ArtSourceTaskCreateResponse.Invalid(relativeTasksPath, "不支持的任务优先级：" + (request.priority ?? string.Empty), request.kind, taskId, workstreamId, currentHash);

        HashSet<string> knownAssetIds = BuildArtSourceAssetIdSet(manifest);
        List<string> assetIds = NormalizeArtTaskAssetIds(request.assetIds);
        for (int i = 0; i < assetIds.Count; i++)
        {
            if (!knownAssetIds.Contains(assetIds[i]))
                return ArtSourceTaskCreateResponse.AssetInvalid(relativeTasksPath, taskId, workstreamId, assetIds[i], currentHash);
        }

        return null;
    }

    private static bool IsValidArtTaskId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string text = value.Trim();
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if ((ch >= 'a' && ch <= 'z') ||
                (ch >= '0' && ch <= '9') ||
                ch == '.' ||
                ch == '_' ||
                ch == '-')
                continue;
            return false;
        }

        return true;
    }

    private static int NextArtWorkstreamSortOrder(ArtSourceTaskDocument tasks)
    {
        int max = 0;
        if (tasks != null && tasks.Workstreams != null)
        {
            for (int i = 0; i < tasks.Workstreams.Count; i++)
            {
                if (tasks.Workstreams[i] != null && tasks.Workstreams[i].SortOrder > max)
                    max = tasks.Workstreams[i].SortOrder;
            }
        }

        return max + 10;
    }

    private static HashSet<string> BuildArtSourceAssetIdSet(ArtSourceManifestDocument manifest)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (manifest == null || manifest.Assets == null)
            return result;

        for (int i = 0; i < manifest.Assets.Count; i++)
        {
            string assetId = manifest.Assets[i] != null ? manifest.Assets[i].AssetId ?? string.Empty : string.Empty;
            if (!string.IsNullOrWhiteSpace(assetId))
                result.Add(assetId);
        }

        return result;
    }

    private static List<string> NormalizeArtTaskAssetIds(List<string> assetIds)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (assetIds == null)
            return result;

        for (int i = 0; i < assetIds.Count; i++)
        {
            string value = (assetIds[i] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value) || seen.Contains(value))
                continue;

            seen.Add(value);
            result.Add(value);
        }

        return result;
    }

    private static string NormalizeArtTaskStatus(string status)
    {
        string value = (status ?? string.Empty).Trim().ToLowerInvariant();
        return value is "blocked" or "active" or "waiting" or "ready" or "review" or "completed"
            ? value
            : string.Empty;
    }

    private static string NormalizeArtTaskPriority(string priority)
    {
        string value = (priority ?? string.Empty).Trim().ToLowerInvariant();
        return value is "critical" or "high" or "medium" or "low"
            ? value
            : string.Empty;
    }

    internal sealed class ArtSourceTaskUpdateRequest
    {
        public string taskId { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string priority { get; set; } = string.Empty;
        public string blockedBy { get; set; } = string.Empty;
        public string nextAction { get; set; } = string.Empty;
        public List<string> assetIds { get; set; } = new();
        public string expectedHash { get; set; } = string.Empty;
    }

    internal sealed class ArtSourceTaskCreateRequest
    {
        public string kind { get; set; } = string.Empty;
        public string workstreamId { get; set; } = string.Empty;
        public string taskId { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public string phase { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string priority { get; set; } = string.Empty;
        public string summary { get; set; } = string.Empty;
        public string blockedBy { get; set; } = string.Empty;
        public string nextAction { get; set; } = string.Empty;
        public int? sortOrder { get; set; }
        public List<string> assetIds { get; set; } = new();
        public string previewPath { get; set; } = string.Empty;
        public List<string> reviewRefs { get; set; } = new();
        public string expectedHash { get; set; } = string.Empty;
    }

    internal sealed class ArtSourceTaskUpdateResponse
    {
        public bool success { get; set; }
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public int httpStatus { get; set; }
        public string tasksPath { get; set; } = string.Empty;
        public string taskId { get; set; } = string.Empty;
        public string expectedHash { get; set; } = string.Empty;
        public string previousHash { get; set; } = string.Empty;
        public string currentHash { get; set; } = string.Empty;
        public List<AuthoringResourceDiagnostic> diagnostics { get; set; } = new();
        public string assetId { get; set; } = string.Empty;

        public static ArtSourceTaskUpdateResponse Success(string tasksPath, string taskId, string previousHash, string currentHash)
        {
            return new ArtSourceTaskUpdateResponse
            {
                success = true,
                httpStatus = 200,
                tasksPath = tasksPath ?? string.Empty,
                taskId = taskId ?? string.Empty,
                previousHash = previousHash ?? string.Empty,
                currentHash = currentHash ?? string.Empty,
                message = "美术任务已保存。"
            };
        }

        public static ArtSourceTaskUpdateResponse Invalid(string tasksPath, string message, string taskId = "", string currentHash = "")
        {
            return new ArtSourceTaskUpdateResponse
            {
                success = false,
                code = AuthoringResourceDiagnosticCodes.ArtSourceTaskUpdateInvalid,
                httpStatus = 400,
                tasksPath = tasksPath ?? string.Empty,
                taskId = taskId ?? string.Empty,
                currentHash = currentHash ?? string.Empty,
                message = message ?? string.Empty
            };
        }

        public static ArtSourceTaskUpdateResponse Conflict(string tasksPath, string taskId, string expectedHash, string currentHash)
        {
            return new ArtSourceTaskUpdateResponse
            {
                success = false,
                code = AuthoringResourceDiagnosticCodes.ArtSourceTaskUpdateConflict,
                httpStatus = 409,
                tasksPath = tasksPath ?? string.Empty,
                taskId = taskId ?? string.Empty,
                expectedHash = expectedHash ?? string.Empty,
                currentHash = currentHash ?? string.Empty,
                message = "art_tasks.json 已在本页加载后发生变化。请刷新后再保存。"
            };
        }

        public static ArtSourceTaskUpdateResponse AssetInvalid(string tasksPath, string taskId, string assetId, string currentHash)
        {
            return new ArtSourceTaskUpdateResponse
            {
                success = false,
                code = AuthoringResourceDiagnosticCodes.ArtSourceTaskAssetInvalid,
                httpStatus = 400,
                tasksPath = tasksPath ?? string.Empty,
                taskId = taskId ?? string.Empty,
                assetId = assetId ?? string.Empty,
                currentHash = currentHash ?? string.Empty,
                message = "美术任务引用了不存在的 manifest assetId：" + (assetId ?? string.Empty)
            };
        }
    }

    internal sealed class ArtSourceTaskCreateResponse
    {
        public bool success { get; set; }
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public int httpStatus { get; set; }
        public string tasksPath { get; set; } = string.Empty;
        public string kind { get; set; } = string.Empty;
        public string taskId { get; set; } = string.Empty;
        public string workstreamId { get; set; } = string.Empty;
        public string expectedHash { get; set; } = string.Empty;
        public string previousHash { get; set; } = string.Empty;
        public string currentHash { get; set; } = string.Empty;
        public string assetId { get; set; } = string.Empty;

        public static ArtSourceTaskCreateResponse Success(string tasksPath, string kind, string taskId, string workstreamId, string previousHash, string currentHash)
        {
            string normalizedKind = kind ?? string.Empty;
            string label = normalizedKind == "workstream"
                ? "美术进程已创建。"
                : "美术任务已创建。";
            return new ArtSourceTaskCreateResponse
            {
                success = true,
                httpStatus = 200,
                tasksPath = tasksPath ?? string.Empty,
                kind = normalizedKind,
                taskId = taskId ?? string.Empty,
                workstreamId = workstreamId ?? string.Empty,
                previousHash = previousHash ?? string.Empty,
                currentHash = currentHash ?? string.Empty,
                message = label
            };
        }

        public static ArtSourceTaskCreateResponse Invalid(string tasksPath, string message, string kind = "", string taskId = "", string workstreamId = "", string currentHash = "")
        {
            return new ArtSourceTaskCreateResponse
            {
                success = false,
                code = AuthoringResourceDiagnosticCodes.ArtSourceTaskCreateInvalid,
                httpStatus = 400,
                tasksPath = tasksPath ?? string.Empty,
                kind = kind ?? string.Empty,
                taskId = taskId ?? string.Empty,
                workstreamId = workstreamId ?? string.Empty,
                currentHash = currentHash ?? string.Empty,
                message = message ?? string.Empty
            };
        }

        public static ArtSourceTaskCreateResponse Conflict(string tasksPath, string kind, string taskId, string workstreamId, string expectedHash, string currentHash)
        {
            return new ArtSourceTaskCreateResponse
            {
                success = false,
                code = AuthoringResourceDiagnosticCodes.ArtSourceTaskCreateConflict,
                httpStatus = 409,
                tasksPath = tasksPath ?? string.Empty,
                kind = kind ?? string.Empty,
                taskId = taskId ?? string.Empty,
                workstreamId = workstreamId ?? string.Empty,
                expectedHash = expectedHash ?? string.Empty,
                currentHash = currentHash ?? string.Empty,
                message = "art_tasks.json 已在本页加载后发生变化。请刷新后再创建。"
            };
        }

        public static ArtSourceTaskCreateResponse AssetInvalid(string tasksPath, string taskId, string workstreamId, string assetId, string currentHash)
        {
            return new ArtSourceTaskCreateResponse
            {
                success = false,
                code = AuthoringResourceDiagnosticCodes.ArtSourceTaskCreateAssetInvalid,
                httpStatus = 400,
                tasksPath = tasksPath ?? string.Empty,
                kind = "task",
                taskId = taskId ?? string.Empty,
                workstreamId = workstreamId ?? string.Empty,
                assetId = assetId ?? string.Empty,
                currentHash = currentHash ?? string.Empty,
                message = "美术任务引用了不存在的 manifest assetId：" + (assetId ?? string.Empty)
            };
        }
    }

    internal sealed class ArtSourceTaskDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("projectId")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonPropertyName("updated")]
        public string Updated { get; set; } = string.Empty;

        [JsonPropertyName("workstreams")]
        public List<ArtSourceWorkstreamEntry> Workstreams { get; set; } = new();

        [JsonPropertyName("tasks")]
        public List<ArtSourceTaskEntry> Tasks { get; set; } = new();
    }

    internal sealed class ArtSourceWorkstreamEntry
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

    internal sealed class ArtSourceTaskEntry
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
        public List<string> AssetIds { get; set; } = new();

        [JsonPropertyName("blockedBy")]
        public string BlockedBy { get; set; } = string.Empty;

        [JsonPropertyName("nextAction")]
        public string NextAction { get; set; } = string.Empty;

        [JsonPropertyName("previewPath")]
        public string PreviewPath { get; set; } = string.Empty;

        [JsonPropertyName("reviewRefs")]
        public List<string> ReviewRefs { get; set; } = new();
    }

    internal sealed class ArtSourceManifestDocument
    {
        [JsonPropertyName("assets")]
        public List<ArtSourceManifestEntry> Assets { get; set; } = new();
    }

    internal sealed class ArtSourceManifestEntry
    {
        [JsonPropertyName("assetId")]
        public string AssetId { get; set; } = string.Empty;
    }
}

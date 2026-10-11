using System.Net;
using System.Text.Json;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string CharacterActionOpenRequestRelativePath = "Temp/MxFrameworkAuthoring/CharacterAction/open_request.json";
    private const string CharacterActionOpenStatusDirectoryRelativePath = "Temp/MxFrameworkAuthoring/CharacterAction/status";
    private const string DefaultCharacterActionProfileSourceDirectory = "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test";
    private const string DefaultCharacterActionSetSourceDirectory = "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test";
    private const string DefaultCharacterActionLibrarySourceDirectory = "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common";
    private static readonly TimeSpan CharacterActionOpenTimeout = TimeSpan.FromSeconds(15);

    private static bool TryHandleCharacterActionOpenApi(
        HttpListenerContext context,
        string rootPath,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path == "/api/character-action/open" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterActionOpenRequest request = string.IsNullOrWhiteSpace(body)
                ? new CharacterActionOpenRequest()
                : (JsonSerializer.Deserialize<CharacterActionOpenRequest>(body, jsonOptions) ?? new CharacterActionOpenRequest());
            CharacterActionOpenResponse response = WriteCharacterActionOpenRequest(rootPath, request, jsonOptions);
            WriteJson(context.Response, response, jsonOptions, 202);
            return true;
        }

        if (path == "/api/character-action/open-status" && context.Request.HttpMethod == "GET")
        {
            string requestId = context.Request.QueryString["id"] ?? string.Empty;
            CharacterActionOpenStatus status = ReadCharacterActionOpenStatus(rootPath, requestId, jsonOptions);
            WriteJson(context.Response, status, jsonOptions);
            return true;
        }

        return false;
    }

    internal static CharacterActionOpenResponse WriteCharacterActionOpenRequest(
        string rootPath,
        CharacterActionOpenRequest request,
        JsonSerializerOptions jsonOptions)
    {
        request ??= new CharacterActionOpenRequest();
        if (request.abilityId <= 0 && request.actionId <= 0)
            throw new ArgumentException("abilityId or actionId is required.");

        string packageRelative = NormalizeCharacterActionOpenRelativePath(rootPath, request.package);
        string profileSourceDirectory = NormalizeCharacterActionOpenRelativePath(
            rootPath,
            string.IsNullOrWhiteSpace(request.profileSourceDirectory) ? DefaultCharacterActionProfileSourceDirectory : request.profileSourceDirectory);
        string actionSetSourceDirectory = NormalizeCharacterActionOpenRelativePath(
            rootPath,
            string.IsNullOrWhiteSpace(request.actionSetSourceDirectory) ? DefaultCharacterActionSetSourceDirectory : request.actionSetSourceDirectory);
        string actionLibrarySourceDirectory = NormalizeCharacterActionOpenRelativePath(
            rootPath,
            string.IsNullOrWhiteSpace(request.actionLibrarySourceDirectory) ? DefaultCharacterActionLibrarySourceDirectory : request.actionLibrarySourceDirectory);
        string requestId = Guid.NewGuid().ToString("N");
        string mode = string.IsNullOrWhiteSpace(request.mode) ? "focusOnly" : request.mode.Trim();

        var payload = new CharacterActionOpenRequestFile
        {
            requestId = requestId,
            createdUtc = DateTimeOffset.UtcNow.ToString("O"),
            package = packageRelative,
            abilityId = request.abilityId,
            profileSourceDirectory = profileSourceDirectory,
            actionSetSourceDirectory = actionSetSourceDirectory,
            actionLibrarySourceDirectory = actionLibrarySourceDirectory,
            profileId = request.profileId ?? string.Empty,
            actionSetStableId = request.actionSetStableId ?? string.Empty,
            actionLibraryId = request.actionLibraryId ?? string.Empty,
            actionId = request.actionId,
            sourceHash = request.sourceHash ?? string.Empty,
            mode = mode
        };

        var status = new CharacterActionOpenStatus
        {
            requestId = requestId,
            status = "pending",
            message = "Waiting for Unity Editor to consume the CharacterAction open request.",
            abilityId = request.abilityId,
            actionId = request.actionId,
            updatedUtc = payload.createdUtc
        };

        string requestPath = GetCharacterActionOpenRequestPath(rootPath);
        string statusPath = GetCharacterActionOpenStatusPath(rootPath, requestId);
        WriteJsonFileAtomic(statusPath, status, jsonOptions);
        WriteJsonFileAtomic(requestPath, payload, jsonOptions);

        return new CharacterActionOpenResponse
        {
            requestId = requestId,
            status = status.status,
            abilityId = request.abilityId,
            actionId = request.actionId,
            requestPath = ToProjectRelativePath(rootPath, requestPath),
            statusPath = ToProjectRelativePath(rootPath, statusPath),
            message = status.message
        };
    }

    internal static CharacterActionOpenStatus ReadCharacterActionOpenStatus(
        string rootPath,
        string requestId,
        JsonSerializerOptions jsonOptions)
    {
        if (!IsSafeCharacterActionOpenRequestId(requestId))
            throw new ArgumentException("open request id is invalid.");

        string statusPath = GetCharacterActionOpenStatusPath(rootPath, requestId);
        if (!File.Exists(statusPath))
        {
            return new CharacterActionOpenStatus
            {
                requestId = requestId,
                status = "pending",
                message = "Open request has not been acknowledged by Unity Editor yet.",
                updatedUtc = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        CharacterActionOpenStatus status = JsonSerializer.Deserialize<CharacterActionOpenStatus>(File.ReadAllText(statusPath), jsonOptions)
            ?? new CharacterActionOpenStatus
            {
                requestId = requestId,
                status = "pending",
                message = "Open request status file was empty.",
                updatedUtc = DateTimeOffset.UtcNow.ToString("O")
            };
        if (string.Equals(status.status, "pending", StringComparison.OrdinalIgnoreCase)
            && DateTimeOffset.TryParse(status.updatedUtc, out DateTimeOffset updated)
            && DateTimeOffset.UtcNow - updated >= CharacterActionOpenTimeout)
        {
            status.status = "timedOut";
            status.message = "Unity did not acknowledge the CharacterAction open request within 15 seconds.";
            status.updatedUtc = DateTimeOffset.UtcNow.ToString("O");
            WriteCharacterActionOpenStatus(rootPath, status, jsonOptions);
        }
        return status;
    }

    internal static void WriteCharacterActionOpenStatus(
        string rootPath,
        CharacterActionOpenStatus status,
        JsonSerializerOptions jsonOptions)
    {
        if (status == null)
            throw new ArgumentNullException(nameof(status));
        if (!IsSafeCharacterActionOpenRequestId(status.requestId))
            throw new ArgumentException("open request id is invalid.");

        status.updatedUtc = string.IsNullOrWhiteSpace(status.updatedUtc)
            ? DateTimeOffset.UtcNow.ToString("O")
            : status.updatedUtc;
        WriteJsonFileAtomic(GetCharacterActionOpenStatusPath(rootPath, status.requestId), status, jsonOptions);
    }

    private static string NormalizeCharacterActionOpenRelativePath(string rootPath, string path)
    {
        path = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string resolved = ResolveSafePath(rootPath, path);
        return ToProjectRelativePath(rootPath, resolved);
    }

    private static string GetCharacterActionOpenRequestPath(string rootPath)
    {
        return Path.Combine(NormalizeDirectoryRoot(rootPath), CharacterActionOpenRequestRelativePath);
    }

    private static string GetCharacterActionOpenStatusPath(string rootPath, string requestId)
    {
        if (!IsSafeCharacterActionOpenRequestId(requestId))
            throw new ArgumentException("open request id is invalid.");
        return Path.Combine(NormalizeDirectoryRoot(rootPath), CharacterActionOpenStatusDirectoryRelativePath, requestId + ".json");
    }

    private static bool IsSafeCharacterActionOpenRequestId(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 64)
            return false;
        for (int i = 0; i < requestId.Length; i++)
        {
            char ch = requestId[i];
            if (!char.IsLetterOrDigit(ch) && ch != '-')
                return false;
        }

        return true;
    }

    private sealed class CharacterActionOpenRequestFile
    {
        public string requestId { get; set; } = string.Empty;
        public string createdUtc { get; set; } = string.Empty;
        public string package { get; set; } = string.Empty;
        public int abilityId { get; set; }
        public string profileSourceDirectory { get; set; } = string.Empty;
        public string actionSetSourceDirectory { get; set; } = string.Empty;
        public string actionLibrarySourceDirectory { get; set; } = string.Empty;
        public string profileId { get; set; } = string.Empty;
        public string actionSetStableId { get; set; } = string.Empty;
        public string actionLibraryId { get; set; } = string.Empty;
        public int actionId { get; set; }
        public string sourceHash { get; set; } = string.Empty;
        public string mode { get; set; } = "focusOnly";
    }

    internal sealed class CharacterActionOpenRequest
    {
        public string package { get; set; } = string.Empty;
        public int abilityId { get; set; }
        public string profileSourceDirectory { get; set; } = string.Empty;
        public string actionSetSourceDirectory { get; set; } = string.Empty;
        public string actionLibrarySourceDirectory { get; set; } = string.Empty;
        public string profileId { get; set; } = string.Empty;
        public string actionSetStableId { get; set; } = string.Empty;
        public string actionLibraryId { get; set; } = string.Empty;
        public int actionId { get; set; }
        public string sourceHash { get; set; } = string.Empty;
        public string mode { get; set; } = "focusOnly";
    }

    internal sealed class CharacterActionOpenResponse
    {
        public string requestId { get; set; } = string.Empty;
        public string status { get; set; } = "pending";
        public int abilityId { get; set; }
        public int actionId { get; set; }
        public string requestPath { get; set; } = string.Empty;
        public string statusPath { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
    }

    internal sealed class CharacterActionOpenStatus
    {
        public string requestId { get; set; } = string.Empty;
        public string status { get; set; } = "pending";
        public string message { get; set; } = string.Empty;
        public int abilityId { get; set; }
        public int actionId { get; set; }
        public string updatedUtc { get; set; } = string.Empty;
    }
}

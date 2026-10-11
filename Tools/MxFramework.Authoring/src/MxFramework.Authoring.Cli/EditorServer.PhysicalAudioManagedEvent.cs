using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    internal const string ManagedPhysicalEventPathPrefix = "event:/MxFramework/Generated/Physical/";
    private const string ManagedPhysicalEventRequestRelativePath = "Temp/MxFrameworkAuthoring/Audio/pending_managed_physical_event.json";
    private const string ManagedPhysicalEventFmodStatusRelativePath = "Temp/MxFrameworkAuthoring/Audio/managed_physical_event_status.json";
    private const string ManagedPhysicalEventStatusDirectory = "Temp/MxFrameworkAuthoring/Audio/managed_event_status";
    private static readonly Dictionary<string, ManagedPhysicalEventStatus> ManagedPhysicalEventOperations =
        new Dictionary<string, ManagedPhysicalEventStatus>(StringComparer.Ordinal);
    private static string ActiveManagedPhysicalEventOperationId = string.Empty;

    /// <remarks>LowFreqAlloc. Authoring-only HTTP routing and JSON DTO handling.</remarks>
    private static bool TryHandlePhysicalAudioManagedEventApi(
        HttpListenerContext context,
        string rootPath,
        string defaultPackage,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path == "/api/authoring/audio/physical/managed-event" && context.Request.HttpMethod == "POST")
        {
            ManagedPhysicalEventApiRequest request = ReadAudioRequest<ManagedPhysicalEventApiRequest>(context, jsonOptions);
            string package = ResolveAudioRequestPackage(context, rootPath, defaultPackage, request?.package);
            ManagedPhysicalEventStatus response;
            try
            {
                ManagedPhysicalEventOperation operation = BuildManagedPhysicalEventOperation(
                    rootPath,
                    package,
                    request,
                    jsonOptions);
                response = StartManagedPhysicalEventOperation(rootPath, operation, jsonOptions);
            }
            catch (ManagedPhysicalEventException exception)
            {
                response = ManagedPhysicalEventStatus.Failed(
                    request?.basisId ?? string.Empty,
                    exception.Code,
                    exception.Message,
                    exception.HttpStatus);
            }
            catch (Exception exception)
            {
                response = ManagedPhysicalEventStatus.Failed(
                    request?.basisId ?? string.Empty,
                    "AUTH_AUDIO_MANAGED_EVENT_REQUEST_INVALID",
                    SafeFailure(exception.Message),
                    400);
            }
            WriteJson(context.Response, response, jsonOptions, response.httpStatus);
            return true;
        }

        const string statusPrefix = "/api/authoring/audio/physical/managed-event/";
        if (path.StartsWith(statusPrefix, StringComparison.Ordinal) && context.Request.HttpMethod == "GET")
        {
            string operationId = path.Substring(statusPrefix.Length);
            EnsureSafeGenerationId(operationId);
            ManagedPhysicalEventStatus status;
            lock (AudioApplicationSync)
                ManagedPhysicalEventOperations.TryGetValue(operationId, out status);
            if (status == null)
            {
                string statusPath = ResolveAudioBridgePath(
                    rootPath,
                    ManagedPhysicalEventStatusDirectory + "/" + operationId + ".json");
                status = File.Exists(statusPath)
                    ? JsonSerializer.Deserialize<ManagedPhysicalEventStatus>(File.ReadAllText(statusPath), jsonOptions)
                    : null;
            }
            status ??= ManagedPhysicalEventStatus.Failed(
                string.Empty,
                "AUTH_AUDIO_MANAGED_EVENT_NOT_FOUND",
                "找不到 Managed Physical Event 创建任务。",
                404,
                operationId);
            WriteJson(context.Response, status, jsonOptions, status.httpStatus == 404 ? 404 : 200);
            return true;
        }

        return false;
    }


}

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;


internal static partial class EditorServer
{
    private static bool TryRollbackManagedPhysicalEventThroughStudioScriptServer(
        string fmodRoot,
        string eventPath,
        string eventGuid,
        JsonElement eventBackup)
    {
        try
        {
            using var client = new TcpClient();
            client.ConnectAsync(IPAddress.Loopback, 3663).Wait(500);
            if (!client.Connected)
                return false;
            string projectScript = File.ReadAllText(Path.Combine(
                fmodRoot,
                "Scripts",
                "MxFrameworkManagedPhysicalEvent.js"));
            string command = CreateManagedPhysicalFmodRollbackCommand(
                projectScript,
                eventPath,
                eventGuid,
                eventBackup);
            NetworkStream stream = client.GetStream();
            stream.ReadTimeout = 5_000;
            byte[] bytes = Encoding.UTF8.GetBytes(command);
            stream.Write(bytes, 0, bytes.Length);
            var response = new StringBuilder();
            var buffer = new byte[4096];
            try
            {
                while (true)
                {
                    int count = stream.Read(buffer, 0, buffer.Length);
                    if (count <= 0)
                        break;
                    response.Append(Encoding.UTF8.GetString(buffer, 0, count));
                    string current = response.ToString();
                    if (current.Contains("out():", StringComparison.Ordinal)
                        || current.Contains("error():", StringComparison.Ordinal))
                        break;
                }
            }
            catch (IOException)
            {
            }
            string result = response.ToString();
            if (!IsSuccessfulFmodStudioResponse(result))
                throw new InvalidOperationException("FMOD Script Server rollback failed: " + SafeFailure(result));
            return true;
        }
        catch (AggregateException exception) when (exception.InnerExceptions.All(item => item is SocketException))
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    internal static string CreateManagedPhysicalFmodRollbackCommand(
        string projectScript,
        string eventPath,
        string eventGuid,
        JsonElement eventBackup)
    {
        string serializedScript = JsonSerializer.Serialize(projectScript ?? string.Empty);
        string scriptHash = CharacterPackageHashUtility.ComputeTextSha256(projectScript ?? string.Empty);
        return "(function(){var expectedScriptHash=\"" + scriptHash + "\";"
            + "if(typeof MxFrameworkRollbackManagedPhysicalEvent!==\"function\""
            + "||typeof MxFrameworkManagedPhysicalEventScriptHash===\"undefined\""
            + "||MxFrameworkManagedPhysicalEventScriptHash!==expectedScriptHash)"
            + "{eval(" + serializedScript + ");MxFrameworkManagedPhysicalEventScriptHash=expectedScriptHash;}"
            + "return MxFrameworkRollbackManagedPhysicalEvent("
            + JsonSerializer.Serialize(eventPath ?? string.Empty)
            + ","
            + JsonSerializer.Serialize(eventGuid ?? string.Empty)
            + ","
            + (eventBackup.ValueKind == JsonValueKind.Undefined ? "null" : eventBackup.GetRawText())
            + ")===true;}())\n";
    }

    internal static bool RequiresManagedPhysicalStudioRollback(
        bool usedStudioScriptServer,
        bool mutationStatusAvailable,
        bool mutationAlreadyRolledBack) =>
        usedStudioScriptServer && mutationStatusAvailable && !mutationAlreadyRolledBack;

    private static void RefreshUnityAfterManagedEventRollback(
        string rootPath,
        string operationId,
        JsonSerializerOptions jsonOptions)
    {
        string restoredHash = ComputeBankSetHash(ResolveAudioBridgePath(
            rootPath,
            "FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop"));
        string requestId = Guid.NewGuid().ToString("N");
        WriteJsonFileAtomic(
            ResolveAudioBridgePath(rootPath, AudioUnityApplyRequest),
            new
            {
                requestId,
                applicationId = operationId,
                setId = "managed-event-rollback",
                bankHash = restoredHash
            },
            jsonOptions);
        AudioUnityApplyStatus unity = WaitForUnityApply(rootPath, requestId, jsonOptions, 30_000);
        if (!string.Equals(unity.status, "Succeeded", StringComparison.Ordinal))
            throw new InvalidOperationException(unity.message ?? "Unity rollback refresh failed.");
    }

    internal static bool RequiresManagedPhysicalUnityRollbackRefresh(
        bool backupCompleted,
        bool unityRefreshRequested) =>
        backupCompleted && unityRefreshRequested;

    private static void ValidateManagedPhysicalSnapshot(
        FmodManagedPhysicalEventRequestV1 request,
        AuthoringFmodAudioLibraryEvent created)
    {
        if (created == null)
            throw new ManagedPhysicalEventException(
                "AUTH_AUDIO_MANAGED_SNAPSHOT_EVENT_MISSING",
                "Unity snapshot 未包含刚创建的受控 FMOD Event。",
                500);
        if (created.IsLoop || created.Is3D != request.Is3D)
            throw new ManagedPhysicalEventException(
                "AUTH_AUDIO_MANAGED_SNAPSHOT_TECHNICAL_MISMATCH",
                "受控 Event 的 OneShot/3D 技术事实与模板不一致。",
                500);
        if (request.Is3D
            && (Math.Abs(created.MinDistance - request.MinDistance) > 0.01f
                || Math.Abs(created.MaxDistance - request.MaxDistance) > 0.01f))
            throw new ManagedPhysicalEventException(
                "AUTH_AUDIO_MANAGED_SNAPSHOT_DISTANCE_MISMATCH",
                "受控 Event 的 3D 距离与模板不一致。",
                500);
        if (created.Banks == null || !created.Banks.Contains(request.BankName, StringComparer.Ordinal))
            throw new ManagedPhysicalEventException(
                "AUTH_AUDIO_MANAGED_SNAPSHOT_BANK_MISSING",
                "受控 Event 未进入模板指定的 FMOD Bank。",
                500);
    }

    private static FmodManagedPhysicalEventStatus RequireManagedPhysicalEventStatus(
        string rootPath,
        string requestId,
        JsonSerializerOptions jsonOptions)
    {
        string path = ResolveAudioBridgePath(rootPath, ManagedPhysicalEventFmodStatusRelativePath);
        DateTime deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                FmodManagedPhysicalEventStatus status = JsonSerializer.Deserialize<FmodManagedPhysicalEventStatus>(
                    File.ReadAllText(path),
                    jsonOptions);
                if (status?.requestId == requestId)
                {
                    if (!string.Equals(status.status, "Succeeded", StringComparison.Ordinal))
                        throw new ManagedPhysicalEventException(
                            status.code ?? "AUTH_AUDIO_MANAGED_FMOD_FAILED",
                            status.message ?? "FMOD Managed Event 创建失败。",
                            500);
                    if (!IsGuid(status.eventGuid))
                        throw new ManagedPhysicalEventException(
                            "AUTH_AUDIO_MANAGED_FMOD_GUID_INVALID",
                            "FMOD 未返回有效 Event GUID。",
                            500);
                    return status;
                }
            }
            Thread.Sleep(200);
        }
        throw new ManagedPhysicalEventException(
            "AUTH_AUDIO_MANAGED_FMOD_STATUS_MISSING",
            "FMOD 未写回 Managed Event 状态。",
            504);
    }

    private static FmodManagedPhysicalEventStatus TryReadManagedPhysicalEventStatus(
        string rootPath,
        string requestId,
        JsonSerializerOptions jsonOptions)
    {
        string path = ResolveAudioBridgePath(rootPath, ManagedPhysicalEventFmodStatusRelativePath);
        if (!File.Exists(path))
            return null;
        FmodManagedPhysicalEventStatus status = JsonSerializer.Deserialize<FmodManagedPhysicalEventStatus>(
            File.ReadAllText(path),
            jsonOptions);
        return string.Equals(status?.requestId, requestId, StringComparison.Ordinal)
            ? status
            : null;
    }

    private static FmodManagedPhysicalEventStatus RequireManagedPhysicalEventBuildStatus(
        string rootPath,
        string requestId,
        JsonSerializerOptions jsonOptions,
        int timeoutMilliseconds)
    {
        string path = ResolveAudioBridgePath(rootPath, ManagedPhysicalEventFmodStatusRelativePath);
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                FmodManagedPhysicalEventStatus status = JsonSerializer.Deserialize<FmodManagedPhysicalEventStatus>(
                    File.ReadAllText(path),
                    jsonOptions);
                if (status?.requestId == requestId && status.buildCompleted)
                {
                    if (!status.buildSucceeded)
                        throw new ManagedPhysicalEventException(
                            status.code ?? "AUTH_AUDIO_MANAGED_FMOD_BUILD_FAILED",
                            status.message ?? "FMOD Managed Event bank build failed.",
                            500);
                    if (!IsGuid(status.eventGuid))
                        throw new ManagedPhysicalEventException(
                            "AUTH_AUDIO_MANAGED_FMOD_GUID_INVALID",
                            "FMOD 未返回有效 Event GUID。",
                            500);
                    return status;
                }
            }
            Thread.Sleep(250);
        }
        throw new ManagedPhysicalEventException(
            "AUTH_AUDIO_MANAGED_FMOD_BUILD_TIMEOUT",
            "FMOD 已创建 Managed Event，但 Desktop Bank 构建超时。",
            504);
    }
}

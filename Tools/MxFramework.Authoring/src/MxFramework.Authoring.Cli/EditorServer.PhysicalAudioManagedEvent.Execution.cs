using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;


internal static partial class EditorServer
{
    private static ManagedPhysicalEventStatus StartManagedPhysicalEventOperation(
        string rootPath,
        ManagedPhysicalEventOperation operation,
        JsonSerializerOptions jsonOptions)
    {
        lock (AudioApplicationSync)
        {
            if (IsAudioApplicationRunningUnsafe() || IsManagedPhysicalEventOperationActiveUnsafe())
                return ManagedPhysicalEventStatus.Failed(
                    operation.Basis.Id,
                    "AUTH_AUDIO_MANAGED_EVENT_BUSY",
                    "另一个 FMOD 应用或 Managed Event 创建任务正在运行。",
                    409);
            var status = new ManagedPhysicalEventStatus
            {
                operationId = operation.FmodRequest.RequestId,
                basisId = operation.Basis.Id,
                audioCueId = operation.Cue.AudioCueId,
                runtimeCueId = operation.Cue.RuntimeCueId,
                eventDefinitionId = operation.AudioEvent.Id,
                runtimeEventId = operation.AudioEvent.RuntimeEventId,
                eventPath = operation.FmodRequest.EventPath,
                stage = "Planned",
                progress = 5,
                message = "已生成受控 Cue/Event 创建计划。",
                httpStatus = 202,
                updatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            ManagedPhysicalEventOperations[status.operationId] = status;
            ActiveManagedPhysicalEventOperationId = status.operationId;
            SaveManagedPhysicalEventStatus(rootPath, status, jsonOptions);
            WriteJsonFileAtomic(
                ResolveAudioBridgePath(rootPath, ManagedPhysicalEventRequestRelativePath),
                operation.FmodRequest,
                jsonOptions);
            _ = Task.Run(() => RunManagedPhysicalEventOperation(rootPath, operation, status, jsonOptions));
            return status;
        }
    }

    private static void RunManagedPhysicalEventOperation(
        string rootPath,
        ManagedPhysicalEventOperation operation,
        ManagedPhysicalEventStatus status,
        JsonSerializerOptions jsonOptions)
    {
        string fmodRoot = ResolveAudioBridgePath(rootPath, "FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo");
        string buildRoot = Path.Combine(fmodRoot, "Build", "Desktop");
        string streamingRoot = ResolveAudioBridgePath(rootPath, "Assets/StreamingAssets");
        string backupRoot = ResolveAudioBridgePath(
            rootPath,
            "Temp/MxFrameworkAuthoring/Audio/ManagedEventBackups/" + status.operationId);
        string snapshotBackupPath = Path.Combine(backupRoot, "fmod_audio_library.json");
        bool backupCompleted = false;
        bool unityRefreshRequested = false;
        bool usedStudioScriptServer = false;
        FmodManagedPhysicalEventStatus fmodStatus = null;
        try
        {
            UpdateManagedPhysicalEventStatus(rootPath, status, "Backup", 12, "备份 FMOD 工程与 Banks。", jsonOptions);
            BackupApplicationFiles(fmodRoot, buildRoot, streamingRoot, backupRoot);
            if (File.Exists(operation.SnapshotPath))
                File.Copy(operation.SnapshotPath, snapshotBackupPath, overwrite: true);
            backupCompleted = true;

            UpdateManagedPhysicalEventStatus(rootPath, status, "FmodCreate", 28, "创建或更新受控 FMOD Event。", jsonOptions);
            usedStudioScriptServer = true;
            bool studioServerBuildStarted = TryEnsureManagedPhysicalEventThroughStudioScriptServer(fmodRoot);
            usedStudioScriptServer = studioServerBuildStarted;
            if (!studioServerBuildStarted)
            {
                RunProcess(
                    "/Applications/FMOD Studio.app/Contents/MacOS/fmodstudio",
                    new[]
                    {
                        "-script",
                        Path.Combine(fmodRoot, "Automation", "MxFrameworkManagedPhysicalEventHeadless.js"),
                        Path.Combine(fmodRoot, "MxFrameworkAudioDemo.fspro")
                    },
                    rootPath,
                    120_000);
            }
            fmodStatus = RequireManagedPhysicalEventStatus(
                rootPath,
                operation.FmodRequest.RequestId,
                jsonOptions);

            UpdateManagedPhysicalEventStatus(rootPath, status, "BuildBanks", 48, "构建 Desktop Banks。", jsonOptions);
            if (studioServerBuildStarted)
            {
                fmodStatus = RequireManagedPhysicalEventBuildStatus(
                    rootPath,
                    operation.FmodRequest.RequestId,
                    jsonOptions,
                    300_000);
            }
            else
            {
                RunProcess(
                    "/Applications/FMOD Studio.app/Contents/MacOS/fmodstudio",
                    new[]
                    {
                        "-build",
                        "-platforms",
                        "Desktop",
                        Path.Combine(fmodRoot, "MxFrameworkAudioDemo.fspro")
                    },
                    rootPath,
                    300_000);
            }
            string bankHash = ComputeBankSetHash(buildRoot);

            UpdateManagedPhysicalEventStatus(rootPath, status, "Mirror", 66, "同步运行时 Banks。", jsonOptions);
            MirrorBanks(buildRoot, streamingRoot);
            if (!string.Equals(bankHash, ComputeBankSetHash(streamingRoot), StringComparison.Ordinal))
                throw new ManagedPhysicalEventException(
                    "AUTH_AUDIO_MANAGED_BANK_MIRROR_MISMATCH",
                    "FMOD Build 与 StreamingAssets bank hash 不一致。",
                    500);

            UpdateManagedPhysicalEventStatus(rootPath, status, "UnityRefresh", 78, "等待 Unity 刷新 Banks 与 Snapshot。", jsonOptions);
            string unityRequestId = Guid.NewGuid().ToString("N");
            WriteJsonFileAtomic(
                ResolveAudioBridgePath(rootPath, AudioUnityApplyRequest),
                new
                {
                    requestId = unityRequestId,
                    applicationId = status.operationId,
                    setId = "managed-event-" + operation.Basis.RuntimeBasisId.ToString(CultureInfo.InvariantCulture),
                    bankHash
                },
                jsonOptions);
            unityRefreshRequested = true;
            AudioUnityApplyStatus unity = WaitForUnityApply(rootPath, unityRequestId, jsonOptions, 120_000);
            if (!string.Equals(unity.status, "Succeeded", StringComparison.Ordinal))
                throw new ManagedPhysicalEventException(
                    "AUTH_AUDIO_MANAGED_UNITY_REFRESH_FAILED",
                    unity.message ?? "Unity Banks/Snapshot 刷新失败。",
                    503);
            UpdateManagedPhysicalEventStatus(rootPath, status, "Snapshot", 90, "校验 FMOD Snapshot 与受控 Event binding。", jsonOptions);
            AuthoringFmodAudioLibrarySnapshotDocument snapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(
                rootPath,
                operation.SnapshotPath,
                jsonOptions);
            AuthoringFmodAudioLibraryEvent created = AudioSemanticManifestValidator.FindEvent(
                snapshot,
                operation.FmodRequest.EventPath,
                fmodStatus.eventGuid);
            ValidateManagedPhysicalSnapshot(operation.FmodRequest, created);
            operation.AudioEvent.FmodEventGuid = fmodStatus.eventGuid;
            operation.AudioEvent.Is3D = created.Is3D;
            operation.AudioEvent.IsLoop = created.IsLoop;
            operation.AudioEvent.MinDistance = created.MinDistance;
            operation.AudioEvent.MaxDistance = created.MaxDistance;
            AudioAuthoringManifestV2.Normalize(operation.SemanticManifest, snapshot);

            RequireCurrentHash(operation.SemanticPath, operation.ExpectedSemanticHash, "AUTH_AUDIO_MANAGED_SEMANTIC_CONFLICT");
            RequireCurrentHash(operation.PhysicalPath, operation.ExpectedPhysicalHash, "AUTH_AUDIO_MANAGED_PHYSICAL_CONFLICT");
            WriteAudioPublicationAtomically(new List<AudioPublicationWrite>
            {
                AudioPublicationWrite.Create(operation.SemanticPath, operation.SemanticManifest, jsonOptions),
                AudioPublicationWrite.Create(operation.PhysicalPath, operation.PhysicalManifest, jsonOptions)
            });

            status.eventGuid = fmodStatus.eventGuid;
            status.created = fmodStatus.created;
            status.bankHash = bankHash;
            status.snapshotHash = AudioAuthoringManifestV2.ComputeSnapshotHash(snapshot);
            status.semanticManifestHash = ComputeOptionalFileHash(operation.SemanticPath);
            status.physicalManifestHash = ComputeOptionalFileHash(operation.PhysicalPath);
            status.terminal = true;
            status.httpStatus = 200;
            UpdateManagedPhysicalEventStatus(
                rootPath,
                status,
                "Ready",
                100,
                fmodStatus.created
                    ? "受控 Cue/Event 已创建并写入 FMOD，可继续生成和应用素材。"
                    : "受控 Cue/Event 模板已更新并写回 manifest。",
                jsonOptions);
        }
        catch (Exception exception)
        {
            string rollbackFailure = string.Empty;
            try
            {
                if (backupCompleted)
                {
                    fmodStatus ??= TryReadManagedPhysicalEventStatus(
                        rootPath,
                        operation.FmodRequest.RequestId,
                        jsonOptions);
                    if (RequiresManagedPhysicalStudioRollback(
                            usedStudioScriptServer,
                            fmodStatus != null && IsGuid(fmodStatus.eventGuid),
                            fmodStatus?.mutationRolledBack == true)
                        && !TryRollbackManagedPhysicalEventThroughStudioScriptServer(
                            fmodRoot,
                            operation.FmodRequest.EventPath,
                            fmodStatus.eventGuid,
                            fmodStatus.eventBackup))
                        throw new InvalidOperationException(
                            "FMOD Studio Script Server disappeared before the open project could be rolled back.");
                    RestoreApplicationFiles(fmodRoot, buildRoot, streamingRoot, backupRoot);
                    if (File.Exists(snapshotBackupPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(operation.SnapshotPath));
                        File.Copy(snapshotBackupPath, operation.SnapshotPath, overwrite: true);
                    }
                    if (RequiresManagedPhysicalUnityRollbackRefresh(backupCompleted, unityRefreshRequested))
                        RefreshUnityAfterManagedEventRollback(rootPath, status.operationId, jsonOptions);
                }
            }
            catch (Exception rollbackException)
            {
                rollbackFailure = SafeFailure(rollbackException.Message);
            }
            ManagedPhysicalEventException known = exception as ManagedPhysicalEventException;
            status.code = string.IsNullOrEmpty(rollbackFailure)
                ? known?.Code ?? "AUTH_AUDIO_MANAGED_EVENT_FAILED"
                : "AUTH_AUDIO_MANAGED_ROLLBACK_FAILED";
            status.detail = SafeFailure(exception.Message)
                + (string.IsNullOrEmpty(rollbackFailure) ? string.Empty : " | rollback: " + rollbackFailure);
            status.terminal = true;
            status.httpStatus = known?.HttpStatus ?? 500;
            UpdateManagedPhysicalEventStatus(
                rootPath,
                status,
                "Failed",
                status.progress,
                string.IsNullOrEmpty(rollbackFailure)
                    ? backupCompleted
                        ? "Managed Event 创建失败，已恢复 FMOD 工程、Banks 与 Snapshot。"
                        : "Managed Event 创建失败；尚未修改 FMOD 工程或 Banks。"
                    : "Managed Event 创建失败，且回滚未完全完成；请检查技术详情。",
                jsonOptions);
        }
        finally
        {
            try
            {
                string requestPath = ResolveAudioBridgePath(rootPath, ManagedPhysicalEventRequestRelativePath);
                if (File.Exists(requestPath))
                    File.Delete(requestPath);
            }
            catch { }
            lock (AudioApplicationSync)
            {
                if (string.Equals(ActiveManagedPhysicalEventOperationId, status.operationId, StringComparison.Ordinal))
                    ActiveManagedPhysicalEventOperationId = string.Empty;
            }
        }
    }

    /// <remarks>LowFreqAlloc. Authoring-only FMOD Studio Script Server control path.</remarks>
    private static bool TryEnsureManagedPhysicalEventThroughStudioScriptServer(string fmodRoot)
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
            string command = CreateManagedPhysicalFmodScriptCommand(projectScript);
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
                throw new InvalidOperationException("FMOD Script Server failed: " + SafeFailure(result));
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

    internal static string CreateManagedPhysicalFmodScriptCommand(string projectScript)
    {
        string serializedScript = JsonSerializer.Serialize(projectScript ?? string.Empty);
        string scriptHash = CharacterPackageHashUtility.ComputeTextSha256(projectScript ?? string.Empty);
        return "(function(){var expectedScriptHash=\"" + scriptHash + "\";"
            + "if(typeof MxFrameworkEnsurePendingManagedPhysicalEventAndBuild!==\"function\""
            + "||typeof MxFrameworkManagedPhysicalEventScriptHash===\"undefined\""
            + "||MxFrameworkManagedPhysicalEventScriptHash!==expectedScriptHash)"
            + "{eval(" + serializedScript + ");MxFrameworkManagedPhysicalEventScriptHash=expectedScriptHash;}"
            + "return MxFrameworkEnsurePendingManagedPhysicalEventAndBuild(false)===true;}())\n";
    }
}

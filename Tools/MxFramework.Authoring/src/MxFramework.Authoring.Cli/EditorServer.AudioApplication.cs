using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string AudioApplicationStatusDirectory = "Temp/MxFrameworkAuthoring/Audio/application_status";
    private const string AudioUnityApplyRequest = "Temp/MxFrameworkAuthoring/Audio/unity_apply_request.json";
    private const string AudioUnityApplyStatusDirectory = "Temp/MxFrameworkAuthoring/Audio/unity_apply_status";
    private static readonly object AudioApplicationSync = new object();
    private static readonly Dictionary<string, AudioApplicationStatus> AudioApplications = new Dictionary<string, AudioApplicationStatus>(StringComparer.Ordinal);
    private static string ActiveAudioApplicationId = string.Empty;

    private static bool TryHandleAudioApplicationApi(HttpListenerContext context, string rootPath, JsonSerializerOptions jsonOptions, string path)
    {
        if (path == "/api/authoring/audio/generation/history" && context.Request.HttpMethod == "GET")
        {
            string eventId = context.Request.QueryString["eventDefinitionId"] ?? string.Empty;
            AudioGenerationManifestDocument manifest = ReadGenerationManifest(rootPath, jsonOptions);
            WriteJson(context.Response, new
            {
                eventDefinitionId = eventId,
                activeSetId = manifest.EventStates.Find(item => string.Equals(item.EventDefinitionId, eventId, StringComparison.Ordinal))?.ActiveSetId ?? string.Empty,
                sets = manifest.AdoptionSets.Where(item => string.Equals(item.EventDefinitionId, eventId, StringComparison.Ordinal)).OrderByDescending(item => item.CreatedAtUtc).ToList(),
                manifestHash = ComputeOptionalFileHash(ResolveAudioBridgePath(rootPath, AudioGenerationManifestRelativePath))
            }, jsonOptions);
            return true;
        }
        const string applyPrefix = "/api/authoring/audio/generation/apply/";
        if (path.StartsWith(applyPrefix, StringComparison.Ordinal) && context.Request.HttpMethod == "GET")
        {
            string id = path.Substring(applyPrefix.Length);
            EnsureSafeGenerationId(id);
            AudioApplicationStatus status;
            lock (AudioApplicationSync) AudioApplications.TryGetValue(id, out status);
            if (status == null)
            {
                string statusPath = ResolveAudioBridgePath(rootPath, AudioApplicationStatusDirectory + "/" + id + ".json");
                status = File.Exists(statusPath) ? JsonSerializer.Deserialize<AudioApplicationStatus>(File.ReadAllText(statusPath), jsonOptions) : null;
            }
            WriteJson(context.Response, status ?? AudioApplicationStatus.Failed(id, string.Empty, "AUTH_AUDIO_APPLICATION_NOT_FOUND", "找不到音频应用任务。"), jsonOptions, status == null ? 404 : 200);
            return true;
        }
        const string historyPrefix = "/api/authoring/audio/generation/history/";
        if (path.StartsWith(historyPrefix, StringComparison.Ordinal) && path.EndsWith("/delete", StringComparison.Ordinal) && context.Request.HttpMethod == "POST")
        {
            string setId = path.Substring(historyPrefix.Length, path.Length - historyPrefix.Length - "/delete".Length);
            EnsureSafeGenerationId(setId);
            AudioHistoryDeleteRequest request = ReadAudioRequest<AudioHistoryDeleteRequest>(context, jsonOptions);
            AudioHistoryDeleteResult result = DeleteAudioHistorySet(rootPath, setId, request.expectedSetHash, request.expectedGenerationManifestHash, jsonOptions);
            WriteJson(context.Response, result, jsonOptions, result.HttpStatus);
            return true;
        }
        if (path.StartsWith(historyPrefix, StringComparison.Ordinal) && path.EndsWith("/apply", StringComparison.Ordinal) && context.Request.HttpMethod == "POST")
        {
            string setId = path.Substring(historyPrefix.Length, path.Length - historyPrefix.Length - "/apply".Length);
            EnsureSafeGenerationId(setId);
            AudioHistoryApplyRequest request = ReadAudioRequest<AudioHistoryApplyRequest>(context, jsonOptions);
            AudioGenerationManifestDocument manifest = ReadGenerationManifest(rootPath, jsonOptions);
            AudioAdoptionSetV3 set = manifest.AdoptionSets.Find(item => string.Equals(item.SetId, setId, StringComparison.Ordinal));
            if (set == null || !string.Equals(set.SetHash, request.expectedSetHash, StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context.Response, new { code = "AUTH_AUDIO_HISTORY_SET_STALE", message = "历史版本不存在或已变化。" }, jsonOptions, 409);
                return true;
            }
            AudioSemanticManifestDocument audioManifest = AudioAuthoringManifestV2.Upgrade(ReadOptionalJsonFile<AudioSemanticManifestDocument>(rootPath, ResolveAudioSemanticManifestPath(rootPath), jsonOptions), null);
            AudioEventAuthoringDefinition audioEvent = AudioAuthoringManifestV2.FindEventDefinition(audioManifest, set.EventDefinitionId);
            AudioCueAuthoringDefinition audioCue = AudioAuthoringManifestV2.FindCue(audioManifest, set.AudioCueId)
                ?? audioManifest.Cues.Find(item => string.Equals(item.EventDefinitionId, set.EventDefinitionId, StringComparison.Ordinal));
            if (audioEvent == null || audioCue == null || !string.Equals(audioCue.EventDefinitionId, audioEvent.Id, StringComparison.Ordinal))
            {
                WriteJson(context.Response, new { code = "AUTH_AUDIO_HISTORY_BINDING_INVALID", message = "历史版本对应的 Cue 或 Event Definition 已不存在。" }, jsonOptions, 409);
                return true;
            }
            string currentBindingHash = AudioAuthoringManifestV2.ComputeBindingHash(audioCue, audioEvent);
            if (!string.IsNullOrWhiteSpace(set.BindingHash) && !string.Equals(set.BindingHash, currentBindingHash, StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context.Response, new { code = "AUTH_AUDIO_HISTORY_BINDING_STALE", message = "Event binding 已变化，拒绝直接应用旧版本。" }, jsonOptions, 409);
                return true;
            }
            var adoption = new FmodAudioAdoptionRequestV1
            {
                RequestId = Guid.NewGuid().ToString("N"), SetId = set.SetId, EventDefinitionId = set.EventDefinitionId,
                TargetEventPath = audioEvent?.FmodEventPath ?? string.Empty, TargetEventGuid = audioEvent?.FmodEventGuid ?? string.Empty,
                ExpectedBindingHash = currentBindingHash, Layout = set.Layout, Sources = set.Sources,
                Randomization = set.Randomization, LayerMix = set.LayerMix, CreatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            adoption.RequestHash = ComputeObjectSha256(adoption, jsonOptions);
            WriteJsonFileAtomic(ResolveAudioBridgePath(rootPath, AudioAdoptionRequestRelativePath), adoption, jsonOptions);
            AudioApplicationStatus application = StartAudioApplication(rootPath, adoption, jsonOptions);
            WriteJson(context.Response, application, jsonOptions, 202);
            return true;
        }
        return false;
    }

    internal static AudioApplicationStatus StartAudioApplication(string rootPath, FmodAudioAdoptionRequestV1 adoption, JsonSerializerOptions jsonOptions)
    {
        lock (AudioApplicationSync)
        {
            if (IsManagedPhysicalEventOperationActiveUnsafe()
                || (!string.IsNullOrEmpty(ActiveAudioApplicationId) && AudioApplications.TryGetValue(ActiveAudioApplicationId, out AudioApplicationStatus running) && !running.Terminal))
                return AudioApplicationStatus.Failed(adoption.RequestId, adoption.SetId, "AUTH_AUDIO_APPLICATION_BUSY", "另一个音频版本正在应用。");
            var status = new AudioApplicationStatus { applicationId = adoption.RequestId, setId = adoption.SetId, eventDefinitionId = adoption.EventDefinitionId, stage = "Saved", progress = 5, message = "版本已保存。", updatedAtUtc = DateTime.UtcNow.ToString("O") };
            AudioApplications[status.applicationId] = status;
            ActiveAudioApplicationId = status.applicationId;
            SaveApplicationStatus(rootPath, status, jsonOptions);
            _ = Task.Run(() => RunAudioApplication(rootPath, adoption, status, jsonOptions));
            return status;
        }
    }

    private static void RunAudioApplication(string rootPath, FmodAudioAdoptionRequestV1 adoption, AudioApplicationStatus status, JsonSerializerOptions jsonOptions)
    {
        string backupRoot = ResolveAudioBridgePath(rootPath, "Temp/MxFrameworkAuthoring/Audio/ApplicationBackups/" + status.applicationId);
        string fmodRoot = ResolveAudioBridgePath(rootPath, "FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo");
        string buildRoot = Path.Combine(fmodRoot, "Build", "Desktop");
        string streamingRoot = ResolveAudioBridgePath(rootPath, "Assets/StreamingAssets");
        try
        {
            UpdateApplication(rootPath, status, "Backup", 12, "备份 FMOD 工程与 Banks。", jsonOptions);
            BackupApplicationFiles(fmodRoot, buildRoot, streamingRoot, backupRoot);
            CaptureOriginalBaseline(rootPath, adoption, jsonOptions);

            UpdateAdoptionSet(rootPath, adoption.SetId, set => { set.Status = "Applying"; set.FailureCode = string.Empty; set.FailureMessage = string.Empty; }, jsonOptions);
            UpdateApplication(rootPath, status, "FmodApply", 28, "更新 FMOD Event。", jsonOptions);
            bool liveApplied = TryApplyThroughStudioScriptServer(fmodRoot, adoption, jsonOptions);
            if (!liveApplied)
                RunProcess("/Applications/FMOD Studio.app/Contents/MacOS/fmodstudio", new[] { "-script", Path.Combine(fmodRoot, "Automation", "MxFrameworkAudioApplyHeadless.js"), Path.Combine(fmodRoot, "MxFrameworkAudioDemo.fspro") }, rootPath, 120_000);
            else
                WriteLiveAdoptionStatus(rootPath, adoption, jsonOptions, buildCompleted: false, buildSucceeded: false);
            RequireAdoptionStatus(rootPath, adoption.RequestId, jsonOptions);

            UpdateApplication(rootPath, status, "BuildBanks", 48, "构建 Desktop Banks。", jsonOptions);
            if (!liveApplied)
                RunProcess("/Applications/FMOD Studio.app/Contents/MacOS/fmodstudio", new[] { "-build", "-platforms", "Desktop", Path.Combine(fmodRoot, "MxFrameworkAudioDemo.fspro") }, rootPath, 300_000);
            else
                WaitForLiveBuildThroughStudioScriptServer(rootPath, adoption, jsonOptions, 300_000);
            string bankHash = ComputeBankSetHash(buildRoot);

            UpdateApplication(rootPath, status, "Mirror", 66, "同步运行时 Banks。", jsonOptions);
            MirrorBanks(buildRoot, streamingRoot);
            if (!string.Equals(bankHash, ComputeBankSetHash(streamingRoot), StringComparison.Ordinal))
                throw new InvalidDataException("FMOD build and StreamingAssets bank hashes do not match");

            UpdateApplication(rootPath, status, "UnityRefresh", 78, "等待 Unity 刷新 Banks 与 Snapshot。", jsonOptions);
            string unityRequestId = Guid.NewGuid().ToString("N");
            WriteJsonFileAtomic(ResolveAudioBridgePath(rootPath, AudioUnityApplyRequest), new { requestId = unityRequestId, applicationId = status.applicationId, setId = adoption.SetId, bankHash }, jsonOptions);
            AudioUnityApplyStatus unity = WaitForUnityApply(rootPath, unityRequestId, jsonOptions, 120_000);
            if (!string.Equals(unity.status, "Succeeded", StringComparison.Ordinal))
                throw new InvalidOperationException(unity.message);
            if (!string.Equals(unity.setId, adoption.SetId, StringComparison.Ordinal)
                || !string.Equals(unity.bankHash, bankHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unity bank refresh status does not match the active audio set");
            string snapshotPath = ResolveFmodAudioLibrarySnapshotPath(rootPath, ResolvePackageId(rootPath, DefaultCharacterPackageRelativePath, jsonOptions));
            AuthoringFmodAudioLibrarySnapshotDocument refreshedSnapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(rootPath, snapshotPath, jsonOptions);
            string snapshotHash = AudioAuthoringManifestV2.ComputeSnapshotHash(refreshedSnapshot);
            if (string.IsNullOrWhiteSpace(snapshotHash))
                throw new InvalidDataException("Unity did not produce a valid FMOD snapshot");

            UpdateAdoptionSet(rootPath, adoption.SetId, set => { set.Status = "Applied"; set.BankHash = bankHash; set.SnapshotHash = snapshotHash; set.AppliedAtUtc = DateTime.UtcNow.ToString("O"); }, jsonOptions, adoption.EventDefinitionId);
            status.bankHash = bankHash; status.snapshotHash = snapshotHash; status.terminal = true;
            UpdateApplication(rootPath, status, "Ready", 100, "已替换当前 FMOD Event，可进行 Unity/FMOD 试听。", jsonOptions);
        }
        catch (Exception exception)
        {
            try { RestoreApplicationFiles(fmodRoot, buildRoot, streamingRoot, backupRoot); } catch { }
            UpdateAdoptionSet(rootPath, adoption.SetId, set => { set.Status = "ApplyFailed"; set.FailureCode = "AUTH_AUDIO_APPLICATION_FAILED"; set.FailureMessage = SafeFailure(exception.Message); }, jsonOptions);
            status.code = "AUTH_AUDIO_APPLICATION_FAILED"; status.detail = SafeFailure(exception.Message); status.terminal = true;
            UpdateApplication(rootPath, status, "Failed", status.progress, "应用失败，已恢复 FMOD 工程与 Banks。", jsonOptions);
        }
        finally { lock (AudioApplicationSync) if (ActiveAudioApplicationId == status.applicationId) ActiveAudioApplicationId = string.Empty; }
    }

    private static AudioGenerationManifestDocument ReadGenerationManifest(string rootPath, JsonSerializerOptions options)
    {
        string path = ResolveAudioBridgePath(rootPath, AudioGenerationManifestRelativePath);
        return UpgradeGenerationManifest(File.Exists(path) ? JsonSerializer.Deserialize<AudioGenerationManifestDocument>(File.ReadAllText(path), options) : null);
    }

    internal static AudioHistoryDeleteResult DeleteAudioHistorySet(
        string rootPath,
        string setId,
        string expectedSetHash,
        string expectedGenerationManifestHash,
        JsonSerializerOptions options)
    {
        lock (AudioApplicationSync)
        {
            string manifestPath = ResolveAudioBridgePath(rootPath, AudioGenerationManifestRelativePath);
            string currentManifestHash = ComputeOptionalFileHash(manifestPath);
            if (!string.IsNullOrWhiteSpace(expectedGenerationManifestHash)
                && !string.Equals(currentManifestHash, expectedGenerationManifestHash, StringComparison.OrdinalIgnoreCase))
                return AudioHistoryDeleteResult.Error(409, "AUTH_AUDIO_GENERATION_MANIFEST_CONFLICT", "音效版本历史已变化，请刷新后重试。", currentManifestHash);

            AudioGenerationManifestDocument manifest = ReadGenerationManifest(rootPath, options);
            AudioAdoptionSetV3 set = manifest.AdoptionSets.Find(item => string.Equals(item.SetId, setId, StringComparison.Ordinal));
            if (set == null || !string.Equals(set.SetHash, expectedSetHash, StringComparison.OrdinalIgnoreCase))
                return AudioHistoryDeleteResult.Error(409, "AUTH_AUDIO_HISTORY_SET_STALE", "历史版本不存在或已变化。", currentManifestHash);
            if (string.Equals(set.SourceType, "OriginalBaseline", StringComparison.Ordinal))
                return AudioHistoryDeleteResult.Error(409, "AUTH_AUDIO_HISTORY_BASELINE_PROTECTED", "原始 FMOD 版本用于安全恢复，不能删除。", currentManifestHash);

            AudioAdoptionEventStateV3 eventState = manifest.EventStates.Find(item => string.Equals(item.EventDefinitionId, set.EventDefinitionId, StringComparison.Ordinal));
            if (string.Equals(eventState?.ActiveSetId, setId, StringComparison.Ordinal))
                return AudioHistoryDeleteResult.Error(409, "AUTH_AUDIO_HISTORY_ACTIVE_SET_PROTECTED", "当前应用版本不能删除，请先应用其他版本。", currentManifestHash);
            if (string.Equals(eventState?.PublishedSetId, setId, StringComparison.Ordinal))
                return AudioHistoryDeleteResult.Error(409, "AUTH_AUDIO_HISTORY_PUBLISHED_SET_PROTECTED", "当前已发布版本不能删除，请先发布其他版本。", currentManifestHash);
            if (string.Equals(set.Status, "Applying", StringComparison.Ordinal)
                || (!string.IsNullOrEmpty(ActiveAudioApplicationId)
                    && AudioApplications.TryGetValue(ActiveAudioApplicationId, out AudioApplicationStatus running)
                    && !running.Terminal
                    && string.Equals(running.setId, setId, StringComparison.Ordinal)))
                return AudioHistoryDeleteResult.Error(409, "AUTH_AUDIO_HISTORY_SET_BUSY", "该音效版本正在应用，暂时不能删除。", currentManifestHash);

            manifest.AdoptionSets.Remove(set);
            manifest.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            WriteJsonFileAtomic(manifestPath, manifest, options);

            var retainedSources = new HashSet<string>(manifest.AdoptionSets
                .SelectMany(item => item.Sources ?? new List<FmodAudioAdoptionSource>())
                .Select(item => item.ProjectRelativePath), StringComparer.Ordinal);
            int deletedFiles = 0;
            var cleanupWarnings = new List<string>();
            foreach (FmodAudioAdoptionSource source in set.Sources ?? new List<FmodAudioAdoptionSource>())
            {
                if (retainedSources.Contains(source.ProjectRelativePath)) continue;
                try { if (TryDeleteGeneratedAudioSource(rootPath, source.ProjectRelativePath)) deletedFiles++; }
                catch (Exception exception) { cleanupWarnings.Add(SafeFailure(exception.Message)); }
            }
            return AudioHistoryDeleteResult.Success(setId, deletedFiles, cleanupWarnings, ComputeOptionalFileHash(manifestPath));
        }
    }

    private static bool TryDeleteGeneratedAudioSource(string rootPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return false;
        string generatedRoot = Path.GetFullPath(ResolveAudioBridgePath(rootPath, "Assets/Audio/MxFramework/Generated"));
        string absolutePath = Path.GetFullPath(ResolveSafePath(rootPath, relativePath));
        string generatedPrefix = generatedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!absolutePath.StartsWith(generatedPrefix, StringComparison.OrdinalIgnoreCase)) return false;
        if (!File.Exists(absolutePath)) return false;
        File.Delete(absolutePath);
        if (File.Exists(absolutePath + ".meta")) File.Delete(absolutePath + ".meta");
        string directory = Path.GetDirectoryName(absolutePath);
        while (!string.IsNullOrEmpty(directory)
            && directory.StartsWith(generatedPrefix, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(directory)
            && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
            if (File.Exists(directory + ".meta")) File.Delete(directory + ".meta");
            directory = Path.GetDirectoryName(directory);
        }
        return true;
    }

    private static void UpdateAdoptionSet(string rootPath, string setId, Action<AudioAdoptionSetV3> update, JsonSerializerOptions options, string activeEventId = null)
    {
        string path = ResolveAudioBridgePath(rootPath, AudioGenerationManifestRelativePath);
        AudioGenerationManifestDocument manifest = ReadGenerationManifest(rootPath, options);
        AudioAdoptionSetV3 set = manifest.AdoptionSets.Find(item => string.Equals(item.SetId, setId, StringComparison.Ordinal));
        if (set == null) throw new InvalidDataException("adoption set is missing: " + setId);
        update(set);
        if (!string.IsNullOrWhiteSpace(activeEventId))
        {
            foreach (AudioAdoptionSetV3 item in manifest.AdoptionSets.Where(item => string.Equals(item.EventDefinitionId, activeEventId, StringComparison.Ordinal) && !string.Equals(item.SetId, setId, StringComparison.Ordinal) && string.Equals(item.Status, "Applied", StringComparison.Ordinal))) item.Status = "Superseded";
            AudioAdoptionEventStateV3 state = manifest.EventStates.Find(item => string.Equals(item.EventDefinitionId, activeEventId, StringComparison.Ordinal));
            if (state == null) { state = new AudioAdoptionEventStateV3 { EventDefinitionId = activeEventId }; manifest.EventStates.Add(state); }
            state.ActiveSetId = setId;
        }
        manifest.UpdatedAtUtc = DateTime.UtcNow.ToString("O");
        WriteJsonFileAtomic(path, manifest, options);
    }

    private static void CaptureOriginalBaseline(string rootPath, FmodAudioAdoptionRequestV1 adoption, JsonSerializerOptions options)
    {
        AudioGenerationManifestDocument manifest = ReadGenerationManifest(rootPath, options);
        if (manifest.AdoptionSets.Any(item => string.Equals(item.EventDefinitionId, adoption.EventDefinitionId, StringComparison.Ordinal) && string.Equals(item.SourceType, "OriginalBaseline", StringComparison.Ordinal))) return;
        List<FmodAudioAdoptionSource> sources = ReadOriginalEventSources(rootPath, adoption.TargetEventGuid);
        if (sources.Count == 0) return;
        string setId = "original-" + SafeAudioProjectSegment(adoption.EventDefinitionId).Split("__")[1];
        var baseline = new AudioAdoptionSetV3 { SetId = setId, AudioCueId = string.Empty, EventDefinitionId = adoption.EventDefinitionId, BindingHash = adoption.ExpectedBindingHash, SourceType = "OriginalBaseline", Status = "Superseded", Layout = sources.Count == 1 ? "SingleInstrument" : "MultiInstrument", Sources = sources, CreatedAtUtc = DateTime.UtcNow.ToString("O") };
        baseline.SetHash = ComputeTextSha256(JsonSerializer.Serialize(baseline, options)); manifest.AdoptionSets.Add(baseline); manifest.UpdatedAtUtc = DateTime.UtcNow.ToString("O");
        WriteJsonFileAtomic(ResolveAudioBridgePath(rootPath, AudioGenerationManifestRelativePath), manifest, options);
    }

    private static List<FmodAudioAdoptionSource> ReadOriginalEventSources(string rootPath, string eventGuid)
    {
        string guid = (eventGuid ?? string.Empty).Trim('{', '}');
        string eventXml = ResolveAudioBridgePath(rootPath, "FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Metadata/Event/{" + guid + "}.xml");
        if (!File.Exists(eventXml)) return new List<FmodAudioAdoptionSource>();
        XDocument document = XDocument.Load(eventXml);
        List<string> ids = document.Descendants("relationship").Where(item => string.Equals((string)item.Attribute("name"), "audioFile", StringComparison.Ordinal)).SelectMany(item => item.Elements("destination")).Select(item => item.Value).Distinct().ToList();
        var sources = new List<FmodAudioAdoptionSource>(); int take = 1;
        foreach (string id in ids)
        {
            string assetXml = ResolveAudioBridgePath(rootPath, "FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Metadata/AudioFile/" + id + ".xml");
            if (!File.Exists(assetXml)) continue;
            XDocument asset = XDocument.Load(assetXml);
            string relative = asset.Descendants("property").FirstOrDefault(item => string.Equals((string)item.Attribute("name"), "assetPath", StringComparison.Ordinal))?.Element("value")?.Value ?? string.Empty;
            string absolute = ResolveSafePath(rootPath, relative);
            if (File.Exists(absolute)) sources.Add(new FmodAudioAdoptionSource { Layer = "body", Take = take++, ProjectRelativePath = relative, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(absolute))).ToLowerInvariant() });
        }
        return sources;
    }

    /// <remarks>LowFreqAlloc. Authoring-only FMOD Studio Script Server control path.</remarks>
    private static bool TryApplyThroughStudioScriptServer(
        string fmodRoot,
        FmodAudioAdoptionRequestV1 adoption,
        JsonSerializerOptions options)
    {
        try
        {
            using var client = new TcpClient(); client.ConnectAsync(IPAddress.Loopback, 3663).Wait(500);
            if (!client.Connected) return false;
            string projectScript = File.ReadAllText(Path.Combine(fmodRoot, "Scripts", "MxFrameworkAudioAdoption.js"));
            string command = CreateFmodStudioScriptCommand(
                projectScript,
                JsonSerializer.Serialize(adoption, options));
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
                    if (count <= 0) break;
                    response.Append(Encoding.UTF8.GetString(buffer, 0, count));
                    if (response.ToString().Contains("out():", StringComparison.Ordinal) || response.ToString().Contains("error():", StringComparison.Ordinal)) break;
                }
            }
            catch (IOException) { }
            string result = response.ToString();
            if (!IsSuccessfulFmodStudioResponse(result))
                throw new InvalidOperationException("FMOD Script Server failed: " + SafeFailure(result));
            return true;
        }
        catch (AggregateException exception) when (exception.InnerExceptions.All(item => item is SocketException)) { return false; }
        catch (SocketException) { return false; }
    }

    private static void RunProcess(string executable, IEnumerable<string> args, string workingDirectory, int timeoutMs)
    {
        if (!File.Exists(executable)) throw new FileNotFoundException("FMOD Studio CLI is not installed", executable);
        ProcessStartInfo start = CreateFmodStudioProcessStartInfo(executable, args, workingDirectory);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("failed to start FMOD Studio CLI");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMs)) { process.Kill(true); throw new TimeoutException("FMOD Studio CLI timed out"); }
        Task.WaitAll(outputTask, errorTask);
        string output = outputTask.Result; string error = errorTask.Result;
        if (process.ExitCode != 0) throw new InvalidOperationException("FMOD Studio CLI failed: " + SafeFailure(error + " " + output));
    }

    internal static bool IsSuccessfulFmodStudioResponse(string response)
    {
        return !string.IsNullOrWhiteSpace(response)
            && !response.Contains("error():", StringComparison.Ordinal)
            && response.Contains("out(): true", StringComparison.Ordinal);
    }

    internal static string CreateFmodStudioScriptCommand(string projectScript)
    {
        string serializedScript = JsonSerializer.Serialize(projectScript ?? string.Empty);
        string scriptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(projectScript ?? string.Empty))).ToLowerInvariant();
        return "(function(){var expectedScriptHash=\"" + scriptHash + "\";"
            + "if(typeof MxFrameworkApplyPendingAudioAdoptionAndBuild!==\"function\""
            + "||typeof MxFrameworkAudioAdoptionScriptHash===\"undefined\""
            + "||MxFrameworkAudioAdoptionScriptHash!==expectedScriptHash)"
            + "{eval(" + serializedScript + ");MxFrameworkAudioAdoptionScriptHash=expectedScriptHash;}"
            + "return MxFrameworkApplyPendingAudioAdoptionAndBuild(false)===true;}())\n";
    }

    internal static string CreateFmodStudioScriptCommand(string projectScript, string adoptionJson)
    {
        string serializedScript = JsonSerializer.Serialize(projectScript ?? string.Empty);
        string scriptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(projectScript ?? string.Empty))).ToLowerInvariant();
        string request = string.IsNullOrWhiteSpace(adoptionJson) ? "{}" : adoptionJson;
        return "(function(){var expectedScriptHash=\"" + scriptHash + "\";"
            + "if(typeof MxFrameworkApplyAudioAdoptionRequestAndBuild!==\"function\""
            + "||typeof MxFrameworkAudioAdoptionScriptHash===\"undefined\""
            + "||MxFrameworkAudioAdoptionScriptHash!==expectedScriptHash)"
            + "{eval(" + serializedScript + ");MxFrameworkAudioAdoptionScriptHash=expectedScriptHash;}"
            + "return MxFrameworkApplyAudioAdoptionRequestAndBuild(" + request + ",false)===true;}())\n";
    }

    internal static ProcessStartInfo CreateFmodStudioProcessStartInfo(string executable, IEnumerable<string> args, string workingDirectory)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.Environment["FMODSTUDIOCMDLINE"] = "1";
        start.ArgumentList.Add("-working-dir");
        start.ArgumentList.Add(workingDirectory);
        foreach (string arg in args) start.ArgumentList.Add(arg);
        return start;
    }

    private static void RequireAdoptionStatus(string rootPath, string requestId, JsonSerializerOptions options)
    {
        string path = ResolveAudioBridgePath(rootPath, AudioAdoptionStatusRelativePath);
        DateTime deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                FmodAudioAdoptionStatus status = JsonSerializer.Deserialize<FmodAudioAdoptionStatus>(File.ReadAllText(path), options);
                if (status?.requestId == requestId)
                {
                    if (!string.Equals(status.status, "Succeeded", StringComparison.Ordinal)) throw new InvalidDataException(status.message ?? "FMOD adoption failed");
                    return;
                }
            }
            Thread.Sleep(200);
        }
        throw new InvalidDataException("FMOD adoption status was not written");
    }

    /// <remarks>LowFreqAlloc. Authoring-only polling of the FMOD Studio Script Server.</remarks>
    private static void WaitForLiveBuildThroughStudioScriptServer(
        string rootPath,
        FmodAudioAdoptionRequestV1 adoption,
        JsonSerializerOptions options,
        int timeoutMs)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            string state = ReadLiveBuildState(adoption.RequestId);
            if (string.Equals(state, "succeeded", StringComparison.Ordinal))
            {
                WriteLiveAdoptionStatus(rootPath, adoption, options, buildCompleted: true, buildSucceeded: true);
                return;
            }
            if (state.StartsWith("failed:", StringComparison.Ordinal))
                throw new InvalidOperationException("FMOD live build failed: " + state.Substring("failed:".Length));
            Thread.Sleep(250);
        }
        throw new TimeoutException("FMOD live build timed out");
    }

    private static string ReadLiveBuildState(string requestId)
    {
        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, 3663);
        string id = JsonSerializer.Serialize(requestId ?? string.Empty);
        string command = "(function(){var s=typeof MxFrameworkLiveAudioAdoptionStatus!==\"undefined\""
            + "?MxFrameworkLiveAudioAdoptionStatus:null;"
            + "if(!s||s.requestId!==" + id + ")return \"missing\";"
            + "if(!s.buildCompleted)return \"pending\";"
            + "return s.buildSucceeded?\"succeeded\":\"failed:\"+(s.message||\"unknown\");}())\n";
        NetworkStream stream = client.GetStream();
        stream.ReadTimeout = 5_000;
        byte[] bytes = Encoding.UTF8.GetBytes(command);
        stream.Write(bytes, 0, bytes.Length);
        var response = new StringBuilder();
        var buffer = new byte[1024];
        try
        {
            while (true)
            {
                int count = stream.Read(buffer, 0, buffer.Length);
                if (count <= 0) break;
                response.Append(Encoding.UTF8.GetString(buffer, 0, count));
                if (response.ToString().Contains("out():", StringComparison.Ordinal)
                    || response.ToString().Contains("error():", StringComparison.Ordinal))
                    break;
            }
        }
        catch (IOException)
        {
        }
        string result = response.ToString();
        if (result.Contains("error():", StringComparison.Ordinal))
            throw new InvalidOperationException("FMOD Script Server build query failed: " + SafeFailure(result));
        int marker = result.IndexOf("out():", StringComparison.Ordinal);
        return marker < 0
            ? "missing"
            : result.Substring(marker + "out():".Length).Replace("\0", string.Empty, StringComparison.Ordinal).Trim();
    }

    private static void WriteLiveAdoptionStatus(
        string rootPath,
        FmodAudioAdoptionRequestV1 adoption,
        JsonSerializerOptions options,
        bool buildCompleted,
        bool buildSucceeded)
    {
        WriteJsonFileAtomic(
            ResolveAudioBridgePath(rootPath, AudioAdoptionStatusRelativePath),
            new FmodAudioAdoptionStatus
            {
                requestId = adoption.RequestId,
                requestHash = adoption.RequestHash,
                expectedBindingHash = adoption.ExpectedBindingHash,
                status = buildCompleted && !buildSucceeded ? "Failed" : "Succeeded",
                appliedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                eventDefinitionId = adoption.EventDefinitionId,
                eventPath = adoption.TargetEventPath,
                eventGuid = adoption.TargetEventGuid,
                buildCompleted = buildCompleted,
                buildSucceeded = buildSucceeded
            },
            options);
    }

    private static AudioUnityApplyStatus WaitForUnityApply(string rootPath, string id, JsonSerializerOptions options, int timeoutMs)
    {
        string path = ResolveAudioBridgePath(rootPath, AudioUnityApplyStatusDirectory + "/" + id + ".json"); DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline) { if (File.Exists(path)) return JsonSerializer.Deserialize<AudioUnityApplyStatus>(File.ReadAllText(path), options) ?? new AudioUnityApplyStatus { status = "Failed", message = "Unity status is invalid" }; Thread.Sleep(250); }
        return new AudioUnityApplyStatus { status = "Failed", message = "Unity 未连接或刷新 Banks 超时。" };
    }

    private static string ComputeBankSetHash(string directory)
    {
        if (!Directory.Exists(directory)) return string.Empty;
        var builder = new StringBuilder(); foreach (string file in Directory.GetFiles(directory, "*.bank").OrderBy(item => item, StringComparer.Ordinal)) builder.Append(Path.GetFileName(file)).Append(':').Append(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant()).Append('\n');
        if (builder.Length == 0) throw new InvalidDataException("no FMOD banks were produced"); return ComputeTextSha256(builder.ToString());
    }

    private static void MirrorBanks(string source, string destination) { Directory.CreateDirectory(destination); foreach (string file in Directory.GetFiles(source, "*.bank")) { string target = Path.Combine(destination, Path.GetFileName(file)); string temp = target + ".tmp"; File.Copy(file, temp, true); File.Move(temp, target, true); } }
    private static void BackupApplicationFiles(string fmodRoot, string buildRoot, string streamingRoot, string backup) { Directory.CreateDirectory(backup); CopyDirectory(Path.Combine(fmodRoot, "Metadata"), Path.Combine(backup, "Metadata")); CopyDirectory(buildRoot, Path.Combine(backup, "Build")); Directory.CreateDirectory(Path.Combine(backup, "Streaming")); foreach (string file in Directory.Exists(streamingRoot) ? Directory.GetFiles(streamingRoot, "*.bank") : Array.Empty<string>()) File.Copy(file, Path.Combine(backup, "Streaming", Path.GetFileName(file)), true); File.Copy(Path.Combine(fmodRoot, "MxFrameworkAudioDemo.fspro"), Path.Combine(backup, "MxFrameworkAudioDemo.fspro"), true); }
    private static void RestoreApplicationFiles(string fmodRoot, string buildRoot, string streamingRoot, string backup) { if (!Directory.Exists(backup)) return; ReplaceDirectory(Path.Combine(backup, "Metadata"), Path.Combine(fmodRoot, "Metadata")); ReplaceDirectory(Path.Combine(backup, "Build"), buildRoot); Directory.CreateDirectory(streamingRoot); foreach (string file in Directory.GetFiles(streamingRoot, "*.bank")) File.Delete(file); foreach (string file in Directory.GetFiles(Path.Combine(backup, "Streaming"), "*.bank")) File.Copy(file, Path.Combine(streamingRoot, Path.GetFileName(file)), true); File.Copy(Path.Combine(backup, "MxFrameworkAudioDemo.fspro"), Path.Combine(fmodRoot, "MxFrameworkAudioDemo.fspro"), true); }
    private static void CopyDirectory(string source, string destination) { if (!Directory.Exists(source)) return; Directory.CreateDirectory(destination); foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true); foreach (string directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory))); }
    private static void ReplaceDirectory(string source, string destination) { if (Directory.Exists(destination)) Directory.Delete(destination, true); CopyDirectory(source, destination); }
    private static string SafeFailure(string text) { string value = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' '); return value.Length > 800 ? value.Substring(0, 800) : value; }
    private static void UpdateApplication(string root, AudioApplicationStatus status, string stage, int progress, string message, JsonSerializerOptions options) { status.stage = stage; status.progress = progress; status.message = message; status.updatedAtUtc = DateTime.UtcNow.ToString("O"); SaveApplicationStatus(root, status, options); }
    private static void SaveApplicationStatus(string root, AudioApplicationStatus status, JsonSerializerOptions options) => WriteJsonFileAtomic(ResolveAudioBridgePath(root, AudioApplicationStatusDirectory + "/" + status.applicationId + ".json"), status, options);

    internal sealed class AudioHistoryApplyRequest { public string expectedSetHash { get; set; } = string.Empty; }
    internal sealed class AudioHistoryDeleteRequest { public string expectedSetHash { get; set; } = string.Empty; public string expectedGenerationManifestHash { get; set; } = string.Empty; }
    internal sealed class AudioHistoryDeleteResult
    {
        public bool success { get; set; }
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string setId { get; set; } = string.Empty;
        public int deletedFiles { get; set; }
        public List<string> cleanupWarnings { get; set; } = new List<string>();
        public string generationManifestHash { get; set; } = string.Empty;
        [JsonIgnore] public int HttpStatus { get; set; } = 200;
        public static AudioHistoryDeleteResult Error(int status, string code, string message, string hash) => new AudioHistoryDeleteResult { success = false, HttpStatus = status, code = code, message = message, generationManifestHash = hash };
        public static AudioHistoryDeleteResult Success(string setId, int deletedFiles, List<string> warnings, string hash) => new AudioHistoryDeleteResult { success = true, setId = setId, deletedFiles = deletedFiles, cleanupWarnings = warnings, generationManifestHash = hash, message = warnings.Count == 0 ? "历史版本已删除。" : "历史版本已删除，但部分孤立文件清理失败。" };
    }
    internal sealed class AudioApplicationStatus
    {
        public string applicationId { get; set; } = string.Empty; public string setId { get; set; } = string.Empty; public string eventDefinitionId { get; set; } = string.Empty; public string stage { get; set; } = string.Empty; public int progress { get; set; } public string message { get; set; } = string.Empty; public string code { get; set; } = string.Empty; public string detail { get; set; } = string.Empty; public string bankHash { get; set; } = string.Empty; public string snapshotHash { get; set; } = string.Empty; public bool terminal { get; set; } public string updatedAtUtc { get; set; } = string.Empty;
        [JsonIgnore]
        public bool Terminal => terminal || stage == "Ready" || stage == "Failed";
        public static AudioApplicationStatus Failed(string id, string set, string code, string message) => new AudioApplicationStatus { applicationId = id, setId = set, stage = "Failed", progress = 0, code = code, message = message, terminal = true, updatedAtUtc = DateTime.UtcNow.ToString("O") };
    }
    private sealed class AudioUnityApplyStatus { public string status { get; set; } = string.Empty; public string message { get; set; } = string.Empty; public string setId { get; set; } = string.Empty; public string bankHash { get; set; } = string.Empty; public string snapshotHash { get; set; } = string.Empty; }
}

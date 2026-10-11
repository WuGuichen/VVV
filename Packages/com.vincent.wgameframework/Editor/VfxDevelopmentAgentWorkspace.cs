using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxAgentTaskCandidateView
    {
        [JsonProperty("candidateId")] public string CandidateId = string.Empty;
        [JsonProperty("direction")] public string Direction = string.Empty;
        [JsonProperty("goal")] public string Goal = string.Empty;
        [JsonProperty("outputRoot")] public string OutputRoot = string.Empty;
        [JsonProperty("referenceEffectIds")] public List<string> ReferenceEffectIds = new List<string>();
        [JsonProperty("borrowedDimensions")] public List<string> BorrowedDimensions = new List<string>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxAgentTaskView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = string.Empty;
        [JsonProperty("taskId")] public string TaskId = string.Empty;
        [JsonProperty("taskHash")] public string TaskHash = string.Empty;
        [JsonProperty("stage")] public string Stage = string.Empty;
        [JsonProperty("providerId")] public string ProviderId = string.Empty;
        [JsonProperty("targetEffectId")] public string TargetEffectId = string.Empty;
        [JsonProperty("targetContractHash")] public string TargetContractHash = string.Empty;
        [JsonProperty("referencePackId")] public string ReferencePackId = string.Empty;
        [JsonProperty("referencePackHash")] public string ReferencePackHash = string.Empty;
        [JsonProperty("candidateRoot")] public string CandidateRoot = string.Empty;
        [JsonProperty("candidates")] public List<VfxAgentTaskCandidateView> Candidates = new List<VfxAgentTaskCandidateView>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxAgentProgressView
    {
        [JsonProperty("taskId")] public string TaskId = string.Empty;
        [JsonProperty("taskHash")] public string TaskHash = string.Empty;
        [JsonProperty("state")] public string State = string.Empty;
        [JsonProperty("currentCandidateId")] public string CurrentCandidateId = string.Empty;
        [JsonProperty("completedCandidates")] public int CompletedCandidates;
        [JsonProperty("sequence")] public int Sequence;
        [JsonProperty("message")] public string Message = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxAgentSettingView
    {
        [JsonProperty("category")] public string Category = string.Empty;
        [JsonProperty("property")] public string Property = string.Empty;
        [JsonProperty("value")] public string Value = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxAgentAssetView
    {
        [JsonProperty("kind")] public string Kind = string.Empty;
        [JsonProperty("assetPath")] public string AssetPath = string.Empty;
        [JsonProperty("purpose")] public string Purpose = string.Empty;
        [JsonProperty("usagePolicy")] public string UsagePolicy = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxAgentCaptureView
    {
        [JsonProperty("captureId")] public string CaptureId = string.Empty;
        [JsonProperty("assetPath")] public string AssetPath = string.Empty;
        [JsonProperty("viewId")] public string ViewId = string.Empty;
        [JsonProperty("timeSliceId")] public string TimeSliceId = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxAgentCandidateReceiptView
    {
        [JsonProperty("candidateId")] public string CandidateId = string.Empty;
        [JsonProperty("direction")] public string Direction = string.Empty;
        [JsonProperty("selectionRationale")] public string SelectionRationale = string.Empty;
        [JsonProperty("prefabAssetPath")] public string PrefabAssetPath = string.Empty;
        [JsonProperty("serializedBy")] public string SerializedBy = string.Empty;
        [JsonProperty("iterationCount")] public int IterationCount;
        [JsonProperty("reusedAssets")] public List<VfxAgentAssetView> ReusedAssets = new List<VfxAgentAssetView>();
        [JsonProperty("createdAssets")] public List<VfxAgentAssetView> CreatedAssets = new List<VfxAgentAssetView>();
        [JsonProperty("settings")] public List<VfxAgentSettingView> Settings = new List<VfxAgentSettingView>();
        [JsonProperty("captures")] public List<VfxAgentCaptureView> Captures = new List<VfxAgentCaptureView>();
        [JsonProperty("contractReferences")] public List<VfxAgentAssetView> ContractReferences = new List<VfxAgentAssetView>();
        [JsonProperty("actualVisualDependencies")] public List<VfxAgentAssetView> ActualVisualDependencies = new List<VfxAgentAssetView>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxAgentReceiptView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = string.Empty;
        [JsonProperty("taskId")] public string TaskId = string.Empty;
        [JsonProperty("taskHash")] public string TaskHash = string.Empty;
        [JsonProperty("receiptHash")] public string ReceiptHash = string.Empty;
        [JsonProperty("reviewStatus")] public string ReviewStatus = string.Empty;
        [JsonProperty("finalState")] public string FinalState = string.Empty;
        [JsonProperty("referencePackId")] public string ReferencePackId = string.Empty;
        [JsonProperty("referencePackHash")] public string ReferencePackHash = string.Empty;
        [JsonProperty("candidates")] public List<VfxAgentCandidateReceiptView> Candidates = new List<VfxAgentCandidateReceiptView>();
    }

    internal sealed class VfxDevelopmentAgentTaskSession
    {
        public string DirectoryPath = string.Empty;
        public string TaskPath = string.Empty;
        public string ProgressPath = string.Empty;
        public string ReceiptPath = string.Empty;
        public VfxAgentTaskView Task;
        public VfxAgentProgressView Progress;
        public VfxAgentReceiptView Receipt;

        public bool HasValidatedTask { get; set; }
        public string TaskValidationMessage { get; set; } = string.Empty;
        public bool HasValidatedReceipt { get; set; }
        public string ValidationMessage { get; set; } = string.Empty;
    }

    /// <summary>Creates and validates stable hand-off packages for the local Development Agent.</summary>
    /// <remarks>Editor-only, AllocByDesign. Runs only on explicit UI actions and never writes Unity serialized assets.</remarks>
    internal sealed class VfxDevelopmentAgentTaskEditorService
    {
        private const string CliProject = "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj";
        private const string TemplateCatalog = "Assets/Config/MxFramework/Vfx/vfx_generation_templates.json";
        private const int TimeoutMilliseconds = 180000;

        public bool TryCreate(VfxLibraryCreationBrief source, out VfxDevelopmentAgentTaskSession session, out string message)
        {
            session = null;
            string taskToken = new string((source?.BriefName ?? "vfx-task").ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
            if (string.IsNullOrWhiteSpace(taskToken)) taskToken = "vfx-task";
            string taskId = taskToken + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string directory = "Temp/MxFramework/VfxAgentTasks/" + taskId;
            Directory.CreateDirectory(directory);
            string briefPath = directory + "/brief.json";
            var brief = new VfxLibraryCoreCreativeBrief
            {
                BriefId = taskId,
                StyleProfileId = source?.StyleProfileId ?? string.Empty,
                QualityProfileId = source?.QualityProfileId ?? string.Empty,
                QualityProfileVersion = source?.QualityProfileVersion ?? string.Empty,
                PreferredBackend = "ParticleSystem",
                PreferredProviderId = "mx.vfx.provider.development-agent",
                TargetCostTier = "Medium",
                Description = source?.Intent ?? string.Empty,
                CandidateCount = 6,
                Round = 0,
                FixedSeeds = new List<int>(source?.FixedEvaluationSeeds ?? Array.Empty<int>())
            };
            string lifecycle = source?.Lifecycle ?? string.Empty;
            if (lifecycle.Contains("蓄力") || lifecycle.Contains("Charge")) brief.Roles.Add("Charge");
            if (lifecycle.Contains("飞行") || lifecycle.Contains("Flight")) brief.Roles.Add("Flight");
            if (lifecycle.Contains("命中") || lifecycle.Contains("Impact") || brief.Roles.Count == 0) brief.Roles.Add("Impact");
            string intent = (source?.Intent ?? string.Empty).ToLowerInvariant();
            brief.ThemeIds.Add(intent.Contains("lightning") || intent.Contains("闪电") ? "lightning" : intent.Contains("poison") || intent.Contains("毒") ? "poison" : intent.Contains("ice") || intent.Contains("冰") ? "ice" : intent.Contains("holy") || intent.Contains("神圣") ? "holy" : intent.Contains("dark") || intent.Contains("暗") ? "dark" : intent.Contains("fire") || intent.Contains("火") ? "fire" : "generic");
            brief.AnchorContract = new VfxLibraryCoreAnchorContract { Anchor = "Target", Space = "World" };
            brief.Lifecycle = new VfxLibraryCoreLifecycleContract { Mode = "OneShot", DefaultLifetimeFrames = 60, StopPolicy = "Expire" };
            File.WriteAllText(briefPath, JsonConvert.SerializeObject(brief, Formatting.Indented), new UTF8Encoding(false));
            string arguments = "run -c Release --no-restore --project " + Quote(CliProject) +
                               " -- vfx agent-task create --project-root . --task-id " + Quote(taskId) +
                               " --brief " + Quote(briefPath) + " --templates " + Quote(TemplateCatalog) +
                               " --target-effect " + Quote(source?.TargetEffectId ?? string.Empty);
            if (!TryRun(arguments, out message))
                return false;
            session = Load(directory);
            if (session?.Task == null || !session.HasValidatedTask || !string.Equals(session.Task.ProviderId, "mx.vfx.provider.development-agent", StringComparison.Ordinal))
            {
                message = session?.TaskValidationMessage ?? "Agent 任务文件缺失，或正式 Provider 不是 Development Agent。";
                session = null;
                return false;
            }
            message = "已创建通用六方向 Development Agent 任务；尚未生成或修改任何 Unity 资产。";
            return true;
        }

        public VfxDevelopmentAgentTaskSession Load(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return null;
            var session = new VfxDevelopmentAgentTaskSession
            {
                DirectoryPath = directory.Replace('\\', '/'),
                TaskPath = (directory + "/task.json").Replace('\\', '/'),
                ProgressPath = (directory + "/progress.json").Replace('\\', '/'),
                ReceiptPath = (directory + "/receipt.json").Replace('\\', '/')
            };
            session.Task = Read<VfxAgentTaskView>(session.TaskPath);
            session.Progress = Read<VfxAgentProgressView>(session.ProgressPath);
            session.Receipt = Read<VfxAgentReceiptView>(session.ReceiptPath);
            if (session.Task != null)
            {
                string taskArguments = "run -c Release --no-restore --project " + Quote(CliProject) +
                                       " -- vfx agent-task validate --project-root . --task " + Quote(session.TaskPath) +
                                       " --templates " + Quote(TemplateCatalog);
                session.HasValidatedTask = TryRun(taskArguments, out string taskValidation);
                session.TaskValidationMessage = taskValidation;
            }
            if (session.Receipt != null)
            {
                string arguments = "run -c Release --no-restore --project " + Quote(CliProject) +
                                   " -- vfx agent-task validate-receipt --project-root . --task " + Quote(session.TaskPath) +
                                   " --receipt " + Quote(session.ReceiptPath) + " --templates " + Quote(TemplateCatalog);
                session.HasValidatedReceipt = TryRun(arguments, out string validation);
                session.ValidationMessage = validation;
            }
            return session;
        }

        public VfxDevelopmentAgentTaskSession LoadLatest()
        {
            const string root = "Temp/MxFramework/VfxAgentTasks";
            if (!Directory.Exists(root)) return null;
            string[] directories = Directory.GetDirectories(root);
            string latest = string.Empty;
            DateTime latestWrite = DateTime.MinValue;
            for (int i = 0; i < directories.Length; i++)
            {
                string taskPath = Path.Combine(directories[i], "task.json");
                if (!File.Exists(taskPath)) continue;
                DateTime write = File.GetLastWriteTimeUtc(taskPath);
                if (write <= latestWrite) continue;
                latestWrite = write;
                latest = directories[i];
            }
            return string.IsNullOrEmpty(latest) ? null : Load(latest);
        }

        public bool RequestCancel(VfxDevelopmentAgentTaskSession session, out string message)
        {
            if (session?.Task == null || session.Progress == null)
            {
                message = "当前没有可取消的 Agent 任务。";
                return false;
            }
            string arguments = "run -c Release --no-restore --project " + Quote(CliProject) +
                               " -- vfx agent-task request-cancel --task " + Quote(session.TaskPath) + " --progress " + Quote(session.ProgressPath);
            return TryRun(arguments, out message);
        }

        public static string BuildInstruction(VfxDevelopmentAgentTaskSession session)
        {
            string fullPath = Path.GetFullPath(session.TaskPath);
            return "请执行 VFX Development Agent 任务：" + fullPath + "。目标效果 " + session.Task.TargetEffectId +
                   "；任务绑定 Reference Pack " + session.Task.ReferencePackId + " / " + session.Task.ReferencePackHash +
                   "。严格按 Docs/VFX_DEVELOPMENT_AGENT_EXECUTION.md 操作；现有 LegacyPlaceholder 只能提供触发、Anchor、生命周期和参数合同，不能成为任何视觉依赖；Piloto Studio 参考包只能观察构图、节奏、层次和可读性，禁止复制或形成直接/传递依赖。通过 Unity MCP / Unity Editor API 原创制作，不手写 Unity YAML，不使用程序纹理生成器；完成后写回 progress.json、receipt.json 与多时间点捕获，且不得自行晋升或设置 Ready。";
        }

        private static T Read<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try { return JsonConvert.DeserializeObject<T>(File.ReadAllText(path)); }
            catch (Exception exception) { UnityEngine.Debug.LogWarning("VFX Agent task file is invalid: " + path + "\n" + exception.Message); return null; }
        }

        private static bool TryRun(string arguments, out string message)
        {
            var start = new ProcessStartInfo("dotnet", arguments)
            {
                WorkingDirectory = Path.GetFullPath("."),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            try
            {
                using Process process = Process.Start(start);
                if (process == null) { message = "无法启动 Authoring CLI。"; return false; }
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(TimeoutMilliseconds)) { process.Kill(); message = "Authoring CLI 超时。"; return false; }
                message = string.IsNullOrWhiteSpace(error) ? output.Trim() : (output + "\n" + error).Trim();
                return process.ExitCode == 0;
            }
            catch (Exception exception) { message = exception.Message; return false; }
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;

namespace MxFramework.Editor
{
    [Serializable]
    internal sealed class VfxReferencePackRegistrationView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = string.Empty;
        [JsonProperty("referencePackId")] public string ReferencePackId = string.Empty;
        [JsonProperty("sourceRoot")] public string SourceRoot = string.Empty;
        [JsonProperty("manifestPath")] public string ManifestPath = string.Empty;
        [JsonProperty("usagePolicy")] public string UsagePolicy = string.Empty;
        [JsonProperty("sourcePackageId")] public string SourcePackageId = string.Empty;
        [JsonProperty("licenseId")] public string LicenseId = string.Empty;
        [JsonProperty("publicRedistributionAllowed")] public bool PublicRedistributionAllowed;
    }

    [Serializable]
    internal sealed class VfxReferenceEffectSummaryView
    {
        [JsonProperty("referenceEffectId")] public string ReferenceEffectId = string.Empty;
        [JsonProperty("displayName")] public string DisplayName = string.Empty;
        [JsonProperty("complexityTier")] public string ComplexityTier = string.Empty;
        [JsonProperty("collectionId")] public string CollectionId = string.Empty;
        [JsonProperty("themeVariant")] public string ThemeVariant = string.Empty;
        [JsonProperty("qualityTier")] public string QualityTier = string.Empty;
        [JsonProperty("sourceAssetPath")] public string SourceAssetPath = string.Empty;
        [JsonProperty("sourcePrefabGuid")] public string SourcePrefabGuid = string.Empty;
        [JsonProperty("semanticRoles")] public List<string> SemanticRoles = new List<string>();
        [JsonProperty("layerRoles")] public List<string> LayerRoles = new List<string>();
        [JsonProperty("particleSystemCount")] public int ParticleSystemCount;
        [JsonProperty("materialCount")] public int MaterialCount;
        [JsonProperty("meshCount")] public int MeshCount;
        [JsonProperty("technicalFingerprint")] public string TechnicalFingerprint = string.Empty;
        [JsonProperty("creativeFingerprint")] public string CreativeFingerprint = string.Empty;
        [JsonProperty("recommendedLayerBudget")] public int RecommendedLayerBudget;
    }

    [Serializable]
    internal sealed class VfxReferencePackManifestView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = string.Empty;
        [JsonProperty("referencePackId")] public string ReferencePackId = string.Empty;
        [JsonProperty("usagePolicy")] public string UsagePolicy = string.Empty;
        [JsonProperty("sourcePackageId")] public string SourcePackageId = string.Empty;
        [JsonProperty("licenseId")] public string LicenseId = string.Empty;
        [JsonProperty("publicRedistributionAllowed")] public bool PublicRedistributionAllowed;
        [JsonProperty("sourceUnityVersion")] public string SourceUnityVersion = string.Empty;
        [JsonProperty("sourceUrpVersion")] public string SourceUrpVersion = string.Empty;
        [JsonProperty("prefabCount")] public int PrefabCount;
        [JsonProperty("dependencyCount")] public int DependencyCount;
        [JsonProperty("materialCount")] public int MaterialCount;
        [JsonProperty("textureCount")] public int TextureCount;
        [JsonProperty("meshCount")] public int MeshCount;
        [JsonProperty("shaderGraphCount")] public int ShaderGraphCount;
        [JsonProperty("effects")] public List<VfxReferenceEffectSummaryView> Effects = new List<VfxReferenceEffectSummaryView>();
        [JsonProperty("manifestHash")] public string ManifestHash = string.Empty;
    }

    internal sealed class VfxReferencePackEditorStatus
    {
        public bool Connected;
        public string SourceRoot = string.Empty;
        public string Message = string.Empty;
        public VfxReferencePackManifestView Manifest;
    }

    /// <summary>Connects the Creation Lab to a machine-local authorized VFX source library.</summary>
    /// <remarks>Editor-only, AllocByDesign. Absolute paths are stored only below Temp and never copied into Catalog or stable reports.</remarks>
    internal sealed class VfxReferencePackEditorService
    {
        private const string RegistrationPath = "Temp/MxFramework/VfxReferencePacks/local-registration.json";
        private const string CliProject = "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj";
        private const int TimeoutMilliseconds = 120000;

        public VfxReferencePackEditorStatus Load()
        {
            if (!File.Exists(RegistrationPath)) return new VfxReferencePackEditorStatus { Message = "尚未连接本机质量参考包" };
            try
            {
                VfxReferencePackRegistrationView registration = JsonConvert.DeserializeObject<VfxReferencePackRegistrationView>(File.ReadAllText(RegistrationPath));
                string manifestPath = registration?.ManifestPath?.Replace('/', Path.DirectorySeparatorChar) ?? string.Empty;
                if (registration == null || !Directory.Exists(registration.SourceRoot) || !File.Exists(manifestPath)) return new VfxReferencePackEditorStatus { SourceRoot = registration?.SourceRoot ?? string.Empty, Message = "参考包路径或 manifest 已失效，请重新连接" };
                VfxReferencePackManifestView manifest = JsonConvert.DeserializeObject<VfxReferencePackManifestView>(File.ReadAllText(manifestPath));
                bool licensed = manifest != null && manifest.UsagePolicy == "LicensedReusable" && manifest.PublicRedistributionAllowed && !string.IsNullOrWhiteSpace(manifest.SourcePackageId) && !string.IsNullOrWhiteSpace(manifest.LicenseId);
                bool valid = manifest != null && manifest.ReferencePackId == "reference.piloto.arpg-realistic-demon-vs-angels" && licensed && !string.IsNullOrWhiteSpace(manifest.ManifestHash);
                return new VfxReferencePackEditorStatus { Connected = valid, SourceRoot = registration.SourceRoot, Manifest = manifest, Message = valid ? "授权可复用源库 · 已连接" : "源库 manifest 或授权证据无效" };
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                return new VfxReferencePackEditorStatus { Message = "读取本机参考包失败：" + exception.Message };
            }
        }

        public bool Connect(string sourceRoot, out string message)
        {
            if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot)) { message = "请选择有效的 Piloto Studio 源库文件夹。"; return false; }
            return Run("run -c Release --no-restore --project " + Quote(CliProject) + " -- vfx reference-pack register --source " + Quote(Path.GetFullPath(sourceRoot)) + " --licensed-reusable --public-redistribution-allowed --source-package piloto.super-realistic-fx-bundle --license-id operator-confirmed-public-redistribution-2026-07-14", out message);
        }

        public bool Rescan(out string message)
        {
            VfxReferencePackEditorStatus status = Load();
            if (!status.Connected)
            {
                message = status.Message;
                return false;
            }
            return Connect(status.SourceRoot, out message);
        }

        public static void RevealSource(VfxReferencePackEditorStatus status)
        {
            if (status != null && Directory.Exists(status.SourceRoot)) EditorUtility.RevealInFinder(status.SourceRoot);
        }

        private static bool Run(string arguments, out string message)
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
                if (process == null) { message = "无法启动 VFX Authoring CLI。"; return false; }
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(TimeoutMilliseconds)) { process.Kill(); message = "扫描质量参考包超时。"; return false; }
                message = string.IsNullOrWhiteSpace(error) ? output.Trim() : (output + Environment.NewLine + error).Trim();
                return process.ExitCode == 0;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }
        }

        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }
}

using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    [InitializeOnLoad]
    internal static class CharacterPackageImportRequestBridge
    {
        private const string RequestRelativePath = "Temp/MxFrameworkAuthoring/CharacterImport/rebuild_request.json";
        private const string StatusDirectoryRelativePath = "Temp/MxFrameworkAuthoring/CharacterImport/status";
        private const double PollIntervalSeconds = 1.0d;

        private static double s_nextPollTime;
        private static string s_lastConsumedRequestId = string.Empty;

        static CharacterPackageImportRequestBridge()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.delayCall += ConsumePendingRequest;
        }

        private static void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < s_nextPollTime)
                return;

            s_nextPollTime = EditorApplication.timeSinceStartup + PollIntervalSeconds;
            ConsumePendingRequest();
        }

        private static void ConsumePendingRequest()
        {
            // CharacterStudio writes the generated package and the global resource profile immediately
            // before posting this request. Let Unity finish its file-system refresh first; otherwise the
            // rebuild can race the AssetDatabase importer and reopen those files while Unity still has
            // them memory-mapped on Windows.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            string requestPath = ProjectPath(RequestRelativePath);
            if (!File.Exists(requestPath))
                return;

            string json;
            try
            {
                json = File.ReadAllText(requestPath);
            }
            catch (IOException)
            {
                return;
            }

            if (!TryReadRequestJson(json, out CharacterImportRebuildRequest request, out string error))
            {
                Debug.LogWarning("MxFramework Character Import: rebuild request ignored: " + error);
                return;
            }

            if (string.Equals(s_lastConsumedRequestId, request.RequestId, StringComparison.Ordinal))
            {
                TryDeleteRequestFile(requestPath);
                return;
            }

            s_lastConsumedRequestId = request.RequestId;
            TryDeleteRequestFile(requestPath);
            WriteStatus(request, "running", "Unity Editor is rebuilding imported character prefab.", string.Empty);

            try
            {
                string packagePath = ResolveProjectPath(request.Package);
                string generatedRoot = string.IsNullOrWhiteSpace(request.GeneratedRoot)
                    ? CharacterPackageImportCommand.DefaultGeneratedRoot
                    : NormalizeProjectPath(request.GeneratedRoot);

                string reportPath = Path.Combine(generatedRoot, request.PackageId, "package_cache", "import_report.json");
                if (File.Exists(reportPath))
                {
                    try
                    {
                        string reportJson = File.ReadAllText(reportPath);
                        var reportObj = Newtonsoft.Json.Linq.JObject.Parse(reportJson);
                        string repSrcHash = (string)reportObj["sourcePackageHash"] ?? string.Empty;
                        string repCfgHash = (string)reportObj["generatedConfigHash"] ?? string.Empty;
                        string repGeoHash = (string)reportObj["geometryBindingHash"] ?? string.Empty;
                        string repMapHash = (string)reportObj["resourceMappingHash"] ?? string.Empty;
                        string repPlanHash = (string)reportObj["writePlanHash"] ?? string.Empty;

                        bool hashesMatch = string.Equals(request.SourcePackageHash, repSrcHash, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(request.GeneratedConfigHash, repCfgHash, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(request.GeometryBindingHash, repGeoHash, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(request.ResourceMappingHash, repMapHash, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(request.WritePlanHash, repPlanHash, StringComparison.OrdinalIgnoreCase);

                        if (!hashesMatch)
                        {
                            WriteStatus(
                                request,
                                "failed",
                                "Preflight check failed: compiler/import report hash mismatch with rebuild request.",
                                string.Empty,
                                null,
                                errorCode: "UNITY_IMPORTED_PACKAGE_PREFLIGHT_FAILED");
                            Debug.LogError("MxFramework Character Import: preflight hash mismatch between request and import_report.json.");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("MxFramework Character Import: preflight check could not parse import report: " + ex.Message);
                    }
                }

                CharacterImportedPackagePrefabBuildResult result = CharacterPackageImportCommand.SynchronizeImportedAssetsWithReport(packagePath, generatedRoot);
                if (result == null || !string.Equals(result.Status, "Built", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(result.PrefabPath))
                {
                    if (result != null && string.IsNullOrWhiteSpace(result.PackageId))
                        result.PackageId = request.PackageId;
                    var rejectedIdentity = new CharacterUnityImportExpectedIdentity
                    {
                        PackageId = request.PackageId,
                        SourcePackageHash = request.SourcePackageHash,
                        GeneratedConfigHash = request.GeneratedConfigHash,
                        GeometryBindingHash = request.GeometryBindingHash,
                        ResourceMappingHash = request.ResourceMappingHash,
                        WritePlanHash = request.WritePlanHash,
                    };
                    if (result != null && !CharacterUnityEditorImportReceipt.TryWriteRejected(
                            rejectedIdentity,
                            result,
                            out string rejectedReceiptPath,
                            out string rejectedReceiptError))
                        Debug.LogError("MxFramework Character Import: rejected Unity receipt could not be written: " + rejectedReceiptError);
                    WriteStatus(
                        request,
                        "failed",
                        result != null ? result.Message : "Prefab rebuild did not produce a prefab path.",
                        result != null ? result.PrefabPath : string.Empty,
                        result);
                    return;
                }

                var expected = new CharacterUnityImportExpectedIdentity
                {
                    PackageId = request.PackageId,
                    SourcePackageHash = request.SourcePackageHash,
                    GeneratedConfigHash = request.GeneratedConfigHash,
                    GeometryBindingHash = request.GeometryBindingHash,
                    ResourceMappingHash = request.ResourceMappingHash,
                    WritePlanHash = request.WritePlanHash,
                };
                if (!CharacterUnityEditorImportReceipt.TryValidateAndWrite(
                        expected,
                        result,
                        out string receiptPath,
                        out string receiptError))
                {
                    WriteStatus(request, "failed", receiptError, result.PrefabPath, result);
                    Debug.LogError("MxFramework Character Import: persistent Unity acceptance receipt failed: " + receiptError);
                    return;
                }

                WriteStatus(request, "completed", result.Message, result.PrefabPath, result);
                Debug.Log("MxFramework Character Import: Unity Editor consumed CharacterStudio import request. package="
                    + request.Package + " prefab=" + result.PrefabPath + " receipt=" + receiptPath);
            }
            catch (Exception ex)
            {
                WriteStatus(request, "failed", ex.Message, string.Empty);
                Debug.LogError("MxFramework Character Import: rebuild request failed: " + ex);
            }
        }

        private static bool TryReadRequestJson(
            string json,
            out CharacterImportRebuildRequest request,
            out string error)
        {
            request = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "request json is empty.";
                return false;
            }

            CharacterImportRebuildRequestFile file;
            try
            {
                file = JsonConvert.DeserializeObject<CharacterImportRebuildRequestFile>(json);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (file == null)
            {
                error = "request json is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(file.RequestId))
            {
                error = "requestId is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(file.Package))
            {
                error = "package is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(file.PackageId)
                || string.IsNullOrWhiteSpace(file.SourcePackageHash)
                || string.IsNullOrWhiteSpace(file.GeneratedConfigHash)
                || string.IsNullOrWhiteSpace(file.GeometryBindingHash)
                || string.IsNullOrWhiteSpace(file.ResourceMappingHash)
                || string.IsNullOrWhiteSpace(file.WritePlanHash))
            {
                error = "packageId and all expected Compiler hashes are required.";
                return false;
            }

            request = new CharacterImportRebuildRequest
            {
                RequestId = file.RequestId,
                Package = file.Package,
                GeneratedRoot = file.GeneratedRoot,
                PackageId = file.PackageId,
                SourcePackageHash = file.SourcePackageHash,
                GeneratedConfigHash = file.GeneratedConfigHash,
                GeometryBindingHash = file.GeometryBindingHash,
                ResourceMappingHash = file.ResourceMappingHash,
                WritePlanHash = file.WritePlanHash,
            };
            return true;
        }

        private static void WriteStatus(
            CharacterImportRebuildRequest request,
            string status,
            string message,
            string prefabPath,
            string errorCode = null)
        {
            WriteStatus(request, status, message, prefabPath, null, errorCode);
        }

        private static void WriteStatus(
            CharacterImportRebuildRequest request,
            string status,
            string message,
            string prefabPath,
            CharacterImportedPackagePrefabBuildResult result,
            string errorCode = null)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RequestId))
                return;

            string statusPath = ProjectPath(
                StatusDirectoryRelativePath + "/" + SanitizeRequestId(request.RequestId) + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(statusPath)!);
            CharacterImportRebuildStatusFile payload = BuildStatusPayload(
                request,
                status,
                message,
                prefabPath,
                result,
                errorCode);
            File.WriteAllText(statusPath, JsonConvert.SerializeObject(payload, Formatting.Indented));
        }

        internal static CharacterImportRebuildStatusFile BuildStatusPayload(
            CharacterImportRebuildRequest request,
            string status,
            string message,
            string prefabPath,
            CharacterImportedPackagePrefabBuildResult result,
            string errorCode = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            string resolvedErrorCode = !string.IsNullOrWhiteSpace(errorCode)
                ? errorCode
                : (result != null ? result.ErrorCode : string.Empty);

            return new CharacterImportRebuildStatusFile
            {
                RequestId = request.RequestId,
                Status = status,
                ErrorCode = resolvedErrorCode,
                Message = message ?? string.Empty,
                Package = request.Package ?? string.Empty,
                PackageId = request.PackageId ?? string.Empty,
                GeneratedRoot = request.GeneratedRoot ?? string.Empty,
                PrefabPath = prefabPath ?? string.Empty,
                RuntimePrefabPath = result != null ? result.RuntimePrefabPath : string.Empty,
                SourcePackageHash = request.SourcePackageHash ?? string.Empty,
                GeneratedConfigHash = request.GeneratedConfigHash ?? string.Empty,
                GeometryBindingHash = request.GeometryBindingHash ?? string.Empty,
                ResourceMappingHash = request.ResourceMappingHash ?? string.Empty,
                WritePlanHash = request.WritePlanHash ?? string.Empty,
                ArtifactHash = result != null ? result.ArtifactHash : string.Empty,
                AvatarSourceStatus = result != null ? result.AvatarSourceStatus : string.Empty,
                AvatarSourceAssetPath = result != null ? result.AvatarSourceAssetPath : string.Empty,
                AvatarSourceGuid = result != null ? result.AvatarSourceGuid : string.Empty,
                RigType = result != null ? result.RigType : string.Empty,
                AvatarSourceRigStatus = result != null ? result.AvatarSourceRigStatus : string.Empty,
                AvatarSourceHumanoidStatus = result != null ? result.AvatarSourceHumanoidStatus : string.Empty,
                AvatarSourceMessage = result != null ? result.AvatarSourceMessage : string.Empty,
                BodyMaterialBindingStatus = result != null ? result.BodyMaterialBindingStatus : string.Empty,
                BodyMaterialTemplateId = result != null ? result.BodyMaterialTemplateId : string.Empty,
                BodyMaterialPath = result != null ? result.BodyMaterialPath : string.Empty,
                BodyMaterialTextureResourceKey = result != null ? result.BodyMaterialTextureResourceKey : string.Empty,
                BodyMaterialRendererCount = result != null ? result.BodyMaterialRendererCount : 0,
                PaletteAtlasResourceKey = result != null ? result.PaletteAtlasResourceKey : string.Empty,
                PaletteCatalogHash = result != null ? result.PaletteCatalogHash : string.Empty,
                PaletteColorCount = result != null ? result.PaletteColorCount : 0,
                SurfaceProfileCount = result != null ? result.SurfaceProfileCount : 0,
                InvalidFaceCount = result != null ? result.PaletteInvalidTokenCount : 0,
                PaletteRendererCount = result != null ? result.PaletteRendererCount : 0,
                PaletteDiagnostic = result != null ? result.PaletteDiagnostic : string.Empty,
                FaceDetailTextureResourceKey = result != null ? result.FaceDetailTextureResourceKey : string.Empty,
                FaceDetailTextureHash = result != null ? result.FaceDetailTextureHash : string.Empty,
                FaceDetailTextureSize = result != null ? result.FaceDetailTextureSize : 0,
                FaceDetailDiagnostic = result != null ? result.FaceDetailDiagnostic : string.Empty,
                Materials = result != null ? result.Materials : Array.Empty<CharacterGeneratedMaterialBuildResult>(),
                BuilderVersion = result != null ? result.BuilderVersion : string.Empty,
                SocketCount = result != null ? result.SocketCount : 0,
                AbstractBoneStatus = result != null ? result.AbstractBoneStatus : string.Empty,
                AbstractBoneDeclaredCount = result != null ? result.AbstractBoneDeclaredCount : 0,
                AbstractBoneResolvedCount = result != null ? result.AbstractBoneResolvedCount : 0,
                AbstractBoneIssueCount = result != null ? result.AbstractBoneIssueCount : 0,
                AbstractBoneRequiredIds = result != null && result.AbstractBoneRequiredIds != null ? result.AbstractBoneRequiredIds : Array.Empty<string>(),
                AbstractBoneIssues = result != null && result.AbstractBoneIssues != null ? result.AbstractBoneIssues : Array.Empty<string>(),
                WeaponAttachmentCount = result != null ? result.WeaponAttachmentCount : 0,
                MountedWeaponCount = result != null ? result.MountedWeaponCount : 0,
                CharacterActionProfileReady = result != null && result.CharacterActionProfileReady,
                CharacterActionActionSetReady = result != null && result.CharacterActionActionSetReady,
                CharacterActionProfilePath = result != null ? result.CharacterActionProfilePath : string.Empty,
                CharacterActionSetPath = result != null ? result.CharacterActionSetPath : string.Empty,
                WeaponTraceBakeStatus = result != null ? result.WeaponTraceBakeStatus : string.Empty,
                WeaponTraceBakeHash = result != null ? result.WeaponTraceBakeHash : string.Empty,
                WeaponTraceBakeRequestCount = result != null ? result.WeaponTraceBakeRequestCount : 0,
                WeaponTraceBakeArtifactCount = result != null ? result.WeaponTraceBakeArtifactCount : 0,
                WeaponTraceBakeCheckedFrameCount = result != null ? result.WeaponTraceBakeCheckedFrameCount : 0,
                WeaponTraceBakeIssueCount = result != null ? result.WeaponTraceBakeIssueCount : 0,
                WeaponTraceBakeReady = result != null && result.WeaponTraceBakeReady,
                WeaponTraceBakeIssues = result != null && result.WeaponTraceBakeIssues != null
                    ? result.WeaponTraceBakeIssues
                    : Array.Empty<CharacterWeaponTraceBakeIssueReport>(),
                SocketBakeStatus = result != null ? result.SocketBakeStatus : string.Empty,
                SocketBakeHash = result != null ? result.SocketBakeHash : string.Empty,
                SocketBakeRequestCount = result != null ? result.SocketBakeRequestCount : 0,
                SocketBakeClipCount = result != null ? result.SocketBakeClipCount : 0,
                SocketBakeSocketTrackCount = result != null ? result.SocketBakeSocketTrackCount : 0,
                SocketBakeCheckedFrameCount = result != null ? result.SocketBakeCheckedFrameCount : 0,
                SocketBakeIssueCount = result != null ? result.SocketBakeIssueCount : 0,
                SocketBakeReady = result != null && result.SocketBakeReady,
                SocketBakeIssues = result != null && result.SocketBakeIssues != null
                    ? result.SocketBakeIssues
                    : Array.Empty<CharacterWeaponTraceBakeIssueReport>(),
                SocketBakeFormat = result != null ? result.SocketBakeFormat : string.Empty,
                SocketBakeIndexPath = result != null ? result.SocketBakeIndexPath : string.Empty,
                SocketBakeIndexHash = result != null ? result.SocketBakeIndexHash : string.Empty,
                SocketBakeBinaryPath = result != null ? result.SocketBakeBinaryPath : string.Empty,
                SocketBakeBinaryHash = result != null ? result.SocketBakeBinaryHash : string.Empty,
                SocketBakeBinarySize = result != null ? result.SocketBakeBinarySize : 0L,
                HurtboxBakeStatus = result != null ? result.HurtboxBakeStatus : string.Empty,
                HurtboxBakeHash = result != null ? result.HurtboxBakeHash : string.Empty,
                HurtboxBakeClipCount = result != null ? result.HurtboxBakeClipCount : 0,
                HurtboxBakeColliderCount = result != null ? result.HurtboxBakeColliderCount : 0,
                HurtboxBakeShapeCount = result != null ? result.HurtboxBakeShapeCount : 0,
                HurtboxBakeCheckedFrameCount = result != null ? result.HurtboxBakeCheckedFrameCount : 0,
                HurtboxBakeIssueCount = result != null ? result.HurtboxBakeIssueCount : 0,
                HurtboxBakeReady = result != null && result.HurtboxBakeReady,
                HurtboxBakeIssues = result != null && result.HurtboxBakeIssues != null
                    ? result.HurtboxBakeIssues
                    : Array.Empty<CharacterWeaponTraceBakeIssueReport>(),
                DefaultWeaponResourceIds = result != null && result.DefaultWeaponResourceIds != null ? result.DefaultWeaponResourceIds : Array.Empty<string>(),
                Issues = result != null && result.Issues != null ? result.Issues : Array.Empty<string>(),
                UpdatedUtc = DateTimeOffset.UtcNow.ToString("O"),
            };
        }

        private static void TryDeleteRequestFile(string requestPath)
        {
            try
            {
                File.Delete(requestPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string ResolveProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string normalized = path.Replace('\\', '/').Trim();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (Path.IsPathRooted(normalized))
            {
                string full = Path.GetFullPath(normalized);
                if (!full.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Path is outside the Unity project: " + path);
                return full;
            }

            return Path.GetFullPath(Path.Combine(projectRoot, normalized));
        }

        private static string NormalizeProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string normalized = path.Replace('\\', '/').Trim();
            if (Path.IsPathRooted(normalized))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/') + "/";
                string full = Path.GetFullPath(normalized).Replace('\\', '/');
                return full.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                    ? full.Substring(projectRoot.Length)
                    : full;
            }

            return normalized.TrimStart('/');
        }

        private static string ProjectPath(string relative)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string SanitizeRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return "unknown";

            char[] chars = requestId.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                if (!char.IsLetterOrDigit(ch) && ch != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        internal sealed class CharacterImportRebuildRequest
        {
            public string RequestId { get; set; } = string.Empty;
            public string Package { get; set; } = string.Empty;
            public string GeneratedRoot { get; set; } = string.Empty;
            public string PackageId { get; set; } = string.Empty;
            public string SourcePackageHash { get; set; } = string.Empty;
            public string GeneratedConfigHash { get; set; } = string.Empty;
            public string GeometryBindingHash { get; set; } = string.Empty;
            public string ResourceMappingHash { get; set; } = string.Empty;
            public string WritePlanHash { get; set; } = string.Empty;
        }

        private sealed class CharacterImportRebuildRequestFile
        {
            [JsonProperty("requestId")]
            public string RequestId { get; set; } = string.Empty;

            [JsonProperty("package")]
            public string Package { get; set; } = string.Empty;

            [JsonProperty("generatedRoot")]
            public string GeneratedRoot { get; set; } = string.Empty;

            [JsonProperty("packageId")]
            public string PackageId { get; set; } = string.Empty;

            [JsonProperty("sourcePackageHash")]
            public string SourcePackageHash { get; set; } = string.Empty;

            [JsonProperty("generatedConfigHash")]
            public string GeneratedConfigHash { get; set; } = string.Empty;

            [JsonProperty("geometryBindingHash")]
            public string GeometryBindingHash { get; set; } = string.Empty;

            [JsonProperty("resourceMappingHash")]
            public string ResourceMappingHash { get; set; } = string.Empty;

            [JsonProperty("writePlanHash")]
            public string WritePlanHash { get; set; } = string.Empty;
        }

        internal sealed class CharacterImportRebuildStatusFile
        {
            [JsonProperty("requestId")]
            public string RequestId { get; set; } = string.Empty;

            [JsonProperty("status")]
            public string Status { get; set; } = string.Empty;

            [JsonProperty("errorCode")]
            public string ErrorCode { get; set; } = string.Empty;

            [JsonProperty("message")]
            public string Message { get; set; } = string.Empty;

            [JsonProperty("package")]
            public string Package { get; set; } = string.Empty;

            [JsonProperty("packageId")]
            public string PackageId { get; set; } = string.Empty;

            [JsonProperty("generatedRoot")]
            public string GeneratedRoot { get; set; } = string.Empty;

            [JsonProperty("prefabPath")]
            public string PrefabPath { get; set; } = string.Empty;

            [JsonProperty("runtimePrefabPath")]
            public string RuntimePrefabPath { get; set; } = string.Empty;

            [JsonProperty("sourcePackageHash")]
            public string SourcePackageHash { get; set; } = string.Empty;

            [JsonProperty("generatedConfigHash")]
            public string GeneratedConfigHash { get; set; } = string.Empty;

            [JsonProperty("geometryBindingHash")]
            public string GeometryBindingHash { get; set; } = string.Empty;

            [JsonProperty("resourceMappingHash")]
            public string ResourceMappingHash { get; set; } = string.Empty;

            [JsonProperty("writePlanHash")]
            public string WritePlanHash { get; set; } = string.Empty;

            [JsonProperty("artifactHash")]
            public string ArtifactHash { get; set; } = string.Empty;

            [JsonProperty("avatarSourceStatus")]
            public string AvatarSourceStatus { get; set; } = string.Empty;

            [JsonProperty("avatarSourceAssetPath")]
            public string AvatarSourceAssetPath { get; set; } = string.Empty;

            [JsonProperty("avatarSourceGuid")]
            public string AvatarSourceGuid { get; set; } = string.Empty;

            [JsonProperty("rigType")]
            public string RigType { get; set; } = string.Empty;

            [JsonProperty("avatarSourceRigStatus")]
            public string AvatarSourceRigStatus { get; set; } = string.Empty;

            [JsonProperty("avatarSourceHumanoidStatus")]
            public string AvatarSourceHumanoidStatus { get; set; } = string.Empty;

            [JsonProperty("avatarSourceMessage")]
            public string AvatarSourceMessage { get; set; } = string.Empty;

            [JsonProperty("bodyMaterialBindingStatus")]
            public string BodyMaterialBindingStatus { get; set; } = string.Empty;

            [JsonProperty("bodyMaterialTemplateId")]
            public string BodyMaterialTemplateId { get; set; } = string.Empty;

            [JsonProperty("bodyMaterialPath")]
            public string BodyMaterialPath { get; set; } = string.Empty;

            [JsonProperty("bodyMaterialTextureResourceKey")]
            public string BodyMaterialTextureResourceKey { get; set; } = string.Empty;

            [JsonProperty("bodyMaterialRendererCount")]
            public int BodyMaterialRendererCount { get; set; }

            [JsonProperty("paletteAtlasResourceKey")]
            public string PaletteAtlasResourceKey { get; set; } = string.Empty;

            [JsonProperty("paletteCatalogHash")]
            public string PaletteCatalogHash { get; set; } = string.Empty;

            [JsonProperty("paletteColorCount")]
            public int PaletteColorCount { get; set; }

            [JsonProperty("surfaceProfileCount")]
            public int SurfaceProfileCount { get; set; }

            [JsonProperty("invalidFaceCount")]
            public int InvalidFaceCount { get; set; }

            [JsonProperty("paletteRendererCount")]
            public int PaletteRendererCount { get; set; }

            [JsonProperty("paletteDiagnostic")]
            public string PaletteDiagnostic { get; set; } = string.Empty;

            [JsonProperty("faceDetailTextureResourceKey")]
            public string FaceDetailTextureResourceKey { get; set; } = string.Empty;

            [JsonProperty("faceDetailTextureHash")]
            public string FaceDetailTextureHash { get; set; } = string.Empty;

            [JsonProperty("faceDetailTextureSize")]
            public int FaceDetailTextureSize { get; set; }

            [JsonProperty("faceDetailDiagnostic")]
            public string FaceDetailDiagnostic { get; set; } = string.Empty;

            [JsonProperty("materials")]
            public CharacterGeneratedMaterialBuildResult[] Materials { get; set; } = Array.Empty<CharacterGeneratedMaterialBuildResult>();

            [JsonProperty("builderVersion")]
            public string BuilderVersion { get; set; } = string.Empty;

            [JsonProperty("socketCount")]
            public int SocketCount { get; set; }

            [JsonProperty("abstractBoneStatus")] public string AbstractBoneStatus { get; set; } = string.Empty;
            [JsonProperty("abstractBoneDeclaredCount")] public int AbstractBoneDeclaredCount { get; set; }
            [JsonProperty("abstractBoneResolvedCount")] public int AbstractBoneResolvedCount { get; set; }
            [JsonProperty("abstractBoneIssueCount")] public int AbstractBoneIssueCount { get; set; }
            [JsonProperty("abstractBoneRequiredIds")] public string[] AbstractBoneRequiredIds { get; set; } = Array.Empty<string>();
            [JsonProperty("abstractBoneIssues")] public string[] AbstractBoneIssues { get; set; } = Array.Empty<string>();

            [JsonProperty("weaponAttachmentCount")]
            public int WeaponAttachmentCount { get; set; }

            [JsonProperty("mountedWeaponCount")]
            public int MountedWeaponCount { get; set; }

            [JsonProperty("characterActionProfileReady")]
            public bool CharacterActionProfileReady { get; set; }

            [JsonProperty("characterActionActionSetReady")]
            public bool CharacterActionActionSetReady { get; set; }

            [JsonProperty("characterActionProfilePath")]
            public string CharacterActionProfilePath { get; set; } = string.Empty;

            [JsonProperty("characterActionSetPath")]
            public string CharacterActionSetPath { get; set; } = string.Empty;

            [JsonProperty("weaponTraceBakeStatus")]
            public string WeaponTraceBakeStatus { get; set; } = string.Empty;

            [JsonProperty("weaponTraceBakeHash")]
            public string WeaponTraceBakeHash { get; set; } = string.Empty;

            [JsonProperty("weaponTraceBakeRequestCount")]
            public int WeaponTraceBakeRequestCount { get; set; }

            [JsonProperty("weaponTraceBakeArtifactCount")]
            public int WeaponTraceBakeArtifactCount { get; set; }

            [JsonProperty("weaponTraceBakeCheckedFrameCount")]
            public int WeaponTraceBakeCheckedFrameCount { get; set; }

            [JsonProperty("weaponTraceBakeIssueCount")]
            public int WeaponTraceBakeIssueCount { get; set; }

            [JsonProperty("weaponTraceBakeReady")]
            public bool WeaponTraceBakeReady { get; set; }

            [JsonProperty("weaponTraceBakeIssues")]
            public CharacterWeaponTraceBakeIssueReport[] WeaponTraceBakeIssues { get; set; } =
                Array.Empty<CharacterWeaponTraceBakeIssueReport>();

            [JsonProperty("socketBakeStatus")] public string SocketBakeStatus { get; set; } = string.Empty;
            [JsonProperty("socketBakeHash")] public string SocketBakeHash { get; set; } = string.Empty;
            [JsonProperty("socketBakeRequestCount")] public int SocketBakeRequestCount { get; set; }
            [JsonProperty("socketBakeClipCount")] public int SocketBakeClipCount { get; set; }
            [JsonProperty("socketBakeSocketTrackCount")] public int SocketBakeSocketTrackCount { get; set; }
            [JsonProperty("socketBakeCheckedFrameCount")] public int SocketBakeCheckedFrameCount { get; set; }
            [JsonProperty("socketBakeIssueCount")] public int SocketBakeIssueCount { get; set; }
            [JsonProperty("socketBakeReady")] public bool SocketBakeReady { get; set; }
            [JsonProperty("socketBakeIssues")]
            public CharacterWeaponTraceBakeIssueReport[] SocketBakeIssues { get; set; } =
                Array.Empty<CharacterWeaponTraceBakeIssueReport>();

            [JsonProperty("socketBakeFormat")] public string SocketBakeFormat { get; set; } = string.Empty;
            [JsonProperty("socketBakeIndexPath")] public string SocketBakeIndexPath { get; set; } = string.Empty;
            [JsonProperty("socketBakeIndexHash")] public string SocketBakeIndexHash { get; set; } = string.Empty;
            [JsonProperty("socketBakeBinaryPath")] public string SocketBakeBinaryPath { get; set; } = string.Empty;
            [JsonProperty("socketBakeBinaryHash")] public string SocketBakeBinaryHash { get; set; } = string.Empty;
            [JsonProperty("socketBakeBinarySize")] public long SocketBakeBinarySize { get; set; }

            [JsonProperty("hurtboxBakeStatus")] public string HurtboxBakeStatus { get; set; } = string.Empty;
            [JsonProperty("hurtboxBakeHash")] public string HurtboxBakeHash { get; set; } = string.Empty;
            [JsonProperty("hurtboxBakeClipCount")] public int HurtboxBakeClipCount { get; set; }
            [JsonProperty("hurtboxBakeColliderCount")] public int HurtboxBakeColliderCount { get; set; }
            [JsonProperty("hurtboxBakeShapeCount")] public int HurtboxBakeShapeCount { get; set; }
            [JsonProperty("hurtboxBakeCheckedFrameCount")] public int HurtboxBakeCheckedFrameCount { get; set; }
            [JsonProperty("hurtboxBakeIssueCount")] public int HurtboxBakeIssueCount { get; set; }
            [JsonProperty("hurtboxBakeReady")] public bool HurtboxBakeReady { get; set; }
            [JsonProperty("hurtboxBakeIssues")]
            public CharacterWeaponTraceBakeIssueReport[] HurtboxBakeIssues { get; set; } =
                Array.Empty<CharacterWeaponTraceBakeIssueReport>();

            [JsonProperty("defaultWeaponResourceIds")]
            public string[] DefaultWeaponResourceIds { get; set; } = Array.Empty<string>();

            [JsonProperty("issues")]
            public string[] Issues { get; set; } = Array.Empty<string>();

            [JsonProperty("updatedUtc")]
            public string UpdatedUtc { get; set; } = string.Empty;
        }
    }
}

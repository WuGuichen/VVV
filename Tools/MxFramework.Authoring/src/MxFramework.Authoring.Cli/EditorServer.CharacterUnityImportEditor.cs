using System.Net;
using System.Text.Json;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string CharacterUnityImportEditorRequestRelativePath = "Temp/MxFrameworkAuthoring/CharacterImport/rebuild_request.json";
    private const string CharacterUnityImportEditorStatusDirectoryRelativePath = "Temp/MxFrameworkAuthoring/CharacterImport/status";

    private static bool TryHandleCharacterUnityImportEditorApi(
        HttpListenerContext context,
        string rootPath,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path == "/api/character/import-unity/editor-status" && context.Request.HttpMethod == "GET")
        {
            string requestId = context.Request.QueryString["id"] ?? string.Empty;
            CharacterUnityImportEditorStatus status = ReadCharacterUnityImportEditorStatus(rootPath, requestId, jsonOptions);
            WriteJson(context.Response, status, jsonOptions);
            return true;
        }

        return false;
    }

    internal static CharacterUnityImportEditorRequestResponse WriteCharacterUnityImportEditorRequest(
        string rootPath,
        string packageRelative,
        string generatedRoot,
        CharacterUnityImportReport importReport,
        JsonSerializerOptions jsonOptions)
    {
        if (importReport == null)
            throw new ArgumentNullException(nameof(importReport));
        if (string.IsNullOrWhiteSpace(importReport.PackageId))
            throw new ArgumentException("Unity import report packageId is required.", nameof(importReport));
        if (string.IsNullOrWhiteSpace(importReport.SourcePackageHash) ||
            string.IsNullOrWhiteSpace(importReport.GeneratedConfigHash) ||
            string.IsNullOrWhiteSpace(importReport.GeometryBindingHash) ||
            string.IsNullOrWhiteSpace(importReport.ResourceMappingHash) ||
            string.IsNullOrWhiteSpace(importReport.WritePlanHash))
        {
            throw new ArgumentException("Unity import report must contain all five compiler hashes.", nameof(importReport));
        }

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        string normalizedPackage = ToProjectRelativePath(rootPath, packagePath);
        string normalizedGeneratedRoot = NormalizeCharacterUnityImportEditorPath(rootPath, generatedRoot, "Assets/MxFrameworkGenerated/CharacterPackages");
        string requestId = Guid.NewGuid().ToString("N");
        string createdUtc = DateTimeOffset.UtcNow.ToString("O");

        var payload = new CharacterUnityImportEditorRequestFile
        {
            requestId = requestId,
            createdUtc = createdUtc,
            package = normalizedPackage,
            generatedRoot = normalizedGeneratedRoot,
            packageId = importReport.PackageId,
            sourcePackageHash = importReport.SourcePackageHash,
            generatedConfigHash = importReport.GeneratedConfigHash,
            geometryBindingHash = importReport.GeometryBindingHash,
            resourceMappingHash = importReport.ResourceMappingHash,
            writePlanHash = importReport.WritePlanHash
        };

        var status = new CharacterUnityImportEditorStatus
        {
            requestId = requestId,
            status = "pending",
            message = "Waiting for Unity Editor to rebuild the imported character prefab.",
            package = normalizedPackage,
            generatedRoot = normalizedGeneratedRoot,
            packageId = importReport.PackageId,
            sourcePackageHash = importReport.SourcePackageHash,
            generatedConfigHash = importReport.GeneratedConfigHash,
            geometryBindingHash = importReport.GeometryBindingHash,
            resourceMappingHash = importReport.ResourceMappingHash,
            writePlanHash = importReport.WritePlanHash,
            updatedUtc = createdUtc
        };

        string requestPath = GetCharacterUnityImportEditorRequestPath(rootPath);
        string statusPath = GetCharacterUnityImportEditorStatusPath(rootPath, requestId);
        WriteJsonFileAtomic(statusPath, status, jsonOptions);
        WriteJsonFileAtomic(requestPath, payload, jsonOptions);

        return new CharacterUnityImportEditorRequestResponse
        {
            requestId = requestId,
            status = status.status,
            package = normalizedPackage,
            generatedRoot = normalizedGeneratedRoot,
            packageId = importReport.PackageId,
            sourcePackageHash = importReport.SourcePackageHash,
            generatedConfigHash = importReport.GeneratedConfigHash,
            geometryBindingHash = importReport.GeometryBindingHash,
            resourceMappingHash = importReport.ResourceMappingHash,
            writePlanHash = importReport.WritePlanHash,
            requestPath = ToProjectRelativePath(rootPath, requestPath),
            statusPath = ToProjectRelativePath(rootPath, statusPath),
            message = status.message
        };
    }

    internal static CharacterUnityImportEditorStatus ReadCharacterUnityImportEditorStatus(
        string rootPath,
        string requestId,
        JsonSerializerOptions jsonOptions)
    {
        if (!IsSafeCharacterUnityImportEditorRequestId(requestId))
            throw new ArgumentException("Unity import editor request id is invalid.");

        string statusPath = GetCharacterUnityImportEditorStatusPath(rootPath, requestId);
        if (!File.Exists(statusPath))
        {
            return new CharacterUnityImportEditorStatus
            {
                requestId = requestId,
                status = "pending",
                message = "Unity Editor has not acknowledged the prefab rebuild request yet.",
                updatedUtc = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        return JsonSerializer.Deserialize<CharacterUnityImportEditorStatus>(File.ReadAllText(statusPath), jsonOptions)
            ?? new CharacterUnityImportEditorStatus
            {
                requestId = requestId,
                status = "pending",
                message = "Unity import editor status file was empty.",
                updatedUtc = DateTimeOffset.UtcNow.ToString("O")
            };
    }

    internal static CharacterUnityImportEditorStatus ReadLatestCharacterUnityImportEditorStatus(
        string rootPath,
        string packageRelative,
        JsonSerializerOptions jsonOptions)
    {
        return ReadLatestCharacterUnityImportEditorStatusCore(
            rootPath,
            packageRelative,
            string.Empty,
            null,
            jsonOptions);
    }

    internal static CharacterUnityImportEditorStatus ReadLatestCharacterUnityImportEditorStatusForCompilerIdentity(
        string rootPath,
        string packageRelative,
        string packageId,
        CharacterCompilerHashSet expectedHashes,
        JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrWhiteSpace(packageId) || !HasCompleteCharacterCompilerIdentity(expectedHashes))
            return null;
        return ReadLatestCharacterUnityImportEditorStatusCore(
            rootPath,
            packageRelative,
            packageId,
            expectedHashes,
            jsonOptions);
    }

    private static CharacterUnityImportEditorStatus ReadLatestCharacterUnityImportEditorStatusCore(
        string rootPath,
        string packageRelative,
        string requiredPackageId,
        CharacterCompilerHashSet requiredHashes,
        JsonSerializerOptions jsonOptions)
    {
        string statusDirectory = Path.Combine(
            NormalizeDirectoryRoot(rootPath),
            CharacterUnityImportEditorStatusDirectoryRelativePath);
        if (!Directory.Exists(statusDirectory))
            return null;

        string normalizedPackage = (packageRelative ?? string.Empty).Replace('\\', '/').Trim('/');
        CharacterUnityImportEditorStatus latest = null;
        DateTimeOffset latestUpdated = DateTimeOffset.MinValue;
        string[] files = Directory.GetFiles(statusDirectory, "*.json", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            try
            {
                CharacterUnityImportEditorStatus candidate = JsonSerializer.Deserialize<CharacterUnityImportEditorStatus>(
                    File.ReadAllText(files[i]),
                    jsonOptions);
                if (candidate == null ||
                    !string.Equals(
                        (candidate.package ?? string.Empty).Replace('\\', '/').Trim('/'),
                        normalizedPackage,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrWhiteSpace(requiredPackageId) &&
                    !MatchesCharacterCompilerIdentity(candidate, requiredPackageId, requiredHashes))
                    continue;

                DateTimeOffset updated = DateTimeOffset.TryParse(candidate.updatedUtc, out DateTimeOffset parsed)
                    ? parsed
                    : File.GetLastWriteTimeUtc(files[i]);
                if (latest == null || updated > latestUpdated)
                {
                    latest = candidate;
                    latestUpdated = updated;
                }
            }
            catch (JsonException)
            {
                // A partially written or unrelated status file must not hide the latest valid package status.
            }
        }

        return latest;
    }

    private static bool MatchesCharacterCompilerIdentity(
        CharacterUnityImportEditorStatus status,
        string packageId,
        CharacterCompilerHashSet expectedHashes)
    {
        return status != null &&
            HasCompleteCharacterCompilerIdentity(expectedHashes) &&
            string.Equals(status.packageId, packageId, StringComparison.Ordinal) &&
            HashEquals(status.sourcePackageHash, expectedHashes.SourcePackageHash) &&
            HashEquals(status.generatedConfigHash, expectedHashes.GeneratedConfigHash) &&
            HashEquals(status.geometryBindingHash, expectedHashes.GeometryBindingHash) &&
            HashEquals(status.resourceMappingHash, expectedHashes.ResourceMappingHash) &&
            HashEquals(status.writePlanHash, expectedHashes.WritePlanHash);
    }

    private static bool HasCompleteCharacterCompilerIdentity(CharacterCompilerHashSet hashes)
    {
        return hashes != null &&
            !string.IsNullOrWhiteSpace(hashes.SourcePackageHash) &&
            !string.IsNullOrWhiteSpace(hashes.GeneratedConfigHash) &&
            !string.IsNullOrWhiteSpace(hashes.GeometryBindingHash) &&
            !string.IsNullOrWhiteSpace(hashes.ResourceMappingHash) &&
            !string.IsNullOrWhiteSpace(hashes.WritePlanHash);
    }

    private static string NormalizeCharacterUnityImportEditorPath(
        string rootPath,
        string path,
        string fallback)
    {
        path = string.IsNullOrWhiteSpace(path) ? fallback : path.Trim();
        string root = NormalizeDirectoryRoot(rootPath);
        string full = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(root, path));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Path must stay inside the project root.");

        return ToProjectRelativePath(rootPath, full);
    }

    private static string GetCharacterUnityImportEditorRequestPath(string rootPath)
    {
        return Path.Combine(NormalizeDirectoryRoot(rootPath), CharacterUnityImportEditorRequestRelativePath);
    }

    private static string GetCharacterUnityImportEditorStatusPath(string rootPath, string requestId)
    {
        if (!IsSafeCharacterUnityImportEditorRequestId(requestId))
            throw new ArgumentException("Unity import editor request id is invalid.");
        return Path.Combine(NormalizeDirectoryRoot(rootPath), CharacterUnityImportEditorStatusDirectoryRelativePath, requestId + ".json");
    }

    private static bool IsSafeCharacterUnityImportEditorRequestId(string requestId)
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

    private sealed class CharacterUnityImportEditorRequestFile
    {
        public string requestId { get; set; } = string.Empty;
        public string createdUtc { get; set; } = string.Empty;
        public string package { get; set; } = string.Empty;
        public string generatedRoot { get; set; } = string.Empty;
        public string packageId { get; set; } = string.Empty;
        public string sourcePackageHash { get; set; } = string.Empty;
        public string generatedConfigHash { get; set; } = string.Empty;
        public string geometryBindingHash { get; set; } = string.Empty;
        public string resourceMappingHash { get; set; } = string.Empty;
        public string writePlanHash { get; set; } = string.Empty;
    }

    internal sealed class CharacterUnityImportEditorRequestResponse
    {
        public string requestId { get; set; } = string.Empty;
        public string status { get; set; } = "pending";
        public string package { get; set; } = string.Empty;
        public string generatedRoot { get; set; } = string.Empty;
        public string packageId { get; set; } = string.Empty;
        public string sourcePackageHash { get; set; } = string.Empty;
        public string generatedConfigHash { get; set; } = string.Empty;
        public string geometryBindingHash { get; set; } = string.Empty;
        public string resourceMappingHash { get; set; } = string.Empty;
        public string writePlanHash { get; set; } = string.Empty;
        public string requestPath { get; set; } = string.Empty;
        public string statusPath { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
    }

    internal sealed class CharacterUnityImportEditorStatus
    {
        public string requestId { get; set; } = string.Empty;
        public string status { get; set; } = "pending";
        public string errorCode { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string package { get; set; } = string.Empty;
        public string generatedRoot { get; set; } = string.Empty;
        public string packageId { get; set; } = string.Empty;
        public string prefabPath { get; set; } = string.Empty;
        public string runtimePrefabPath { get; set; } = string.Empty;
        public string sourcePackageHash { get; set; } = string.Empty;
        public string generatedConfigHash { get; set; } = string.Empty;
        public string geometryBindingHash { get; set; } = string.Empty;
        public string resourceMappingHash { get; set; } = string.Empty;
        public string writePlanHash { get; set; } = string.Empty;
        public string artifactHash { get; set; } = string.Empty;
        public string avatarSourceStatus { get; set; } = string.Empty;
        public string avatarSourceAssetPath { get; set; } = string.Empty;
        public string avatarSourceGuid { get; set; } = string.Empty;
        public string rigType { get; set; } = string.Empty;
        public string avatarSourceRigStatus { get; set; } = string.Empty;
        public string avatarSourceHumanoidStatus { get; set; } = string.Empty;
        public string avatarSourceMessage { get; set; } = string.Empty;
        public string bodyMaterialBindingStatus { get; set; } = string.Empty;
        public string bodyMaterialTemplateId { get; set; } = string.Empty;
        public string bodyMaterialPath { get; set; } = string.Empty;
        public string bodyMaterialTextureResourceKey { get; set; } = string.Empty;
        public int bodyMaterialRendererCount { get; set; }
        public CharacterUnityEditorImportReceiptMaterial[] materials { get; set; } = Array.Empty<CharacterUnityEditorImportReceiptMaterial>();
        public string builderVersion { get; set; } = string.Empty;
        public int socketCount { get; set; }
        public string abstractBoneStatus { get; set; } = string.Empty;
        public int abstractBoneDeclaredCount { get; set; }
        public int abstractBoneResolvedCount { get; set; }
        public int abstractBoneIssueCount { get; set; }
        public string[] abstractBoneRequiredIds { get; set; } = Array.Empty<string>();
        public string[] abstractBoneIssues { get; set; } = Array.Empty<string>();
        public int weaponAttachmentCount { get; set; }
        public int mountedWeaponCount { get; set; }
        public bool characterActionProfileReady { get; set; }
        public bool characterActionActionSetReady { get; set; }
        public string characterActionProfilePath { get; set; } = string.Empty;
        public string characterActionSetPath { get; set; } = string.Empty;
        public string weaponTraceBakeStatus { get; set; } = string.Empty;
        public string weaponTraceBakeHash { get; set; } = string.Empty;
        public int weaponTraceBakeRequestCount { get; set; }
        public int weaponTraceBakeArtifactCount { get; set; }
        public int weaponTraceBakeCheckedFrameCount { get; set; }
        public int weaponTraceBakeIssueCount { get; set; }
        public bool weaponTraceBakeReady { get; set; }
        public CharacterUnityImportEditorWeaponTraceBakeIssue[] weaponTraceBakeIssues { get; set; } =
            Array.Empty<CharacterUnityImportEditorWeaponTraceBakeIssue>();
        public string socketBakeStatus { get; set; } = string.Empty;
        public string socketBakeHash { get; set; } = string.Empty;
        public int socketBakeRequestCount { get; set; }
        public int socketBakeClipCount { get; set; }
        public int socketBakeSocketTrackCount { get; set; }
        public int socketBakeCheckedFrameCount { get; set; }
        public int socketBakeIssueCount { get; set; }
        public bool socketBakeReady { get; set; }
        public CharacterUnityImportEditorWeaponTraceBakeIssue[] socketBakeIssues { get; set; } =
            Array.Empty<CharacterUnityImportEditorWeaponTraceBakeIssue>();
        public string socketBakeFormat { get; set; } = string.Empty;
        public string socketBakeIndexPath { get; set; } = string.Empty;
        public string socketBakeIndexHash { get; set; } = string.Empty;
        public string socketBakeBinaryPath { get; set; } = string.Empty;
        public string socketBakeBinaryHash { get; set; } = string.Empty;
        public long socketBakeBinarySize { get; set; }
        public string hurtboxBakeStatus { get; set; } = string.Empty;
        public string hurtboxBakeHash { get; set; } = string.Empty;
        public int hurtboxBakeClipCount { get; set; }
        public int hurtboxBakeColliderCount { get; set; }
        public int hurtboxBakeShapeCount { get; set; }
        public int hurtboxBakeCheckedFrameCount { get; set; }
        public int hurtboxBakeIssueCount { get; set; }
        public bool hurtboxBakeReady { get; set; }
        public CharacterUnityImportEditorWeaponTraceBakeIssue[] hurtboxBakeIssues { get; set; } =
            Array.Empty<CharacterUnityImportEditorWeaponTraceBakeIssue>();
        public string[] defaultWeaponResourceIds { get; set; } = Array.Empty<string>();
        public string[] issues { get; set; } = Array.Empty<string>();
        public string updatedUtc { get; set; } = string.Empty;
    }

    internal sealed class CharacterUnityImportEditorWeaponTraceBakeIssue
    {
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string actionStableId { get; set; } = string.Empty;
        public string clipKey { get; set; } = string.Empty;
        public string traceId { get; set; } = string.Empty;
        public int frame { get; set; }
        public string sourcePath { get; set; } = string.Empty;
        public string sourceObjectPath { get; set; } = string.Empty;
    }

    internal sealed class CharacterUnityEditorImportReceipt
    {
        public string format { get; set; } = string.Empty;
        public string schemaVersion { get; set; } = string.Empty;
        public bool ready { get; set; }
        public string packageId { get; set; } = string.Empty;
        public string builderVersion { get; set; } = string.Empty;
        public CharacterUnityEditorImportReceiptHashes hashes { get; set; } = new CharacterUnityEditorImportReceiptHashes();
        public CharacterUnityEditorImportReceiptPrefab prefab { get; set; } = new CharacterUnityEditorImportReceiptPrefab();
        public CharacterUnityEditorImportReceiptAvatar avatar { get; set; } = new CharacterUnityEditorImportReceiptAvatar();
        public CharacterUnityEditorImportReceiptMaterial material { get; set; } = new CharacterUnityEditorImportReceiptMaterial();
        public CharacterUnityEditorImportReceiptMaterial[] materials { get; set; } = Array.Empty<CharacterUnityEditorImportReceiptMaterial>();
        public CharacterUnityEditorImportReceiptGeometry geometry { get; set; } = new CharacterUnityEditorImportReceiptGeometry();
        public CharacterUnityEditorImportReceiptCharacterAction characterAction { get; set; } = new CharacterUnityEditorImportReceiptCharacterAction();
        public CharacterUnityEditorImportReceiptWeaponTraceBake weaponTraceBake { get; set; } = new CharacterUnityEditorImportReceiptWeaponTraceBake();
        public CharacterUnityEditorImportReceiptSocketBake socketBake { get; set; } = new CharacterUnityEditorImportReceiptSocketBake();
        public CharacterUnityEditorImportReceiptHurtboxBake hurtboxBake { get; set; } = new CharacterUnityEditorImportReceiptHurtboxBake();
        public CharacterUnityEditorImportReceiptAbstractBones abstractBones { get; set; } = new CharacterUnityEditorImportReceiptAbstractBones();
        public string[] defaultWeaponResourceIds { get; set; } = Array.Empty<string>();
    }

    internal sealed class CharacterUnityEditorImportReceiptHashes
    {
        public string sourcePackageHash { get; set; } = string.Empty;
        public string generatedConfigHash { get; set; } = string.Empty;
        public string geometryBindingHash { get; set; } = string.Empty;
        public string resourceMappingHash { get; set; } = string.Empty;
        public string writePlanHash { get; set; } = string.Empty;
        public string unityArtifactHash { get; set; } = string.Empty;
    }

    internal sealed class CharacterUnityEditorImportReceiptPrefab
    {
        public string prefabPath { get; set; } = string.Empty;
        public string runtimePrefabPath { get; set; } = string.Empty;
    }

    internal sealed class CharacterUnityEditorImportReceiptAvatar
    {
        public string status { get; set; } = string.Empty;
        public string assetPath { get; set; } = string.Empty;
        public string guid { get; set; } = string.Empty;
        public string rigType { get; set; } = string.Empty;
        public string rigStatus { get; set; } = string.Empty;
        public string humanoidStatus { get; set; } = string.Empty;
    }

    internal sealed class CharacterUnityEditorImportReceiptMaterial
    {
        public string status { get; set; } = string.Empty;
        public string templateId { get; set; } = string.Empty;
        public string materialPath { get; set; } = string.Empty;
        public string textureResourceKey { get; set; } = string.Empty;
        public int rendererCount { get; set; }
        public string sourceMaterialName { get; set; } = string.Empty;
        public string normalTextureResourceKey { get; set; } = string.Empty;
        public string maskTextureResourceKey { get; set; } = string.Empty;
        public string surfaceMode { get; set; } = CharacterMaterialSurfaceModeIds.Opaque;
        public float alphaCutoff { get; set; } = 0.5f;
        public int assignedSlotCount { get; set; }
    }

    internal sealed class CharacterUnityEditorImportReceiptGeometry
    {
        public int socketCount { get; set; }
        public int weaponAttachmentCount { get; set; }
        public int mountedWeaponCount { get; set; }
    }

    internal sealed class CharacterUnityEditorImportReceiptCharacterAction
    {
        public bool profileReady { get; set; }
        public bool actionSetReady { get; set; }
        public string profilePath { get; set; } = string.Empty;
        public string actionSetPath { get; set; } = string.Empty;
    }

    internal sealed class CharacterUnityEditorImportReceiptWeaponTraceBake
    {
        public string status { get; set; } = string.Empty;
        public string hash { get; set; } = string.Empty;
        public int requestCount { get; set; }
        public int artifactCount { get; set; }
        public int checkedFrameCount { get; set; }
        public int issueCount { get; set; }
        public CharacterUnityImportEditorWeaponTraceBakeIssue[] issues { get; set; } =
            Array.Empty<CharacterUnityImportEditorWeaponTraceBakeIssue>();
    }

    internal sealed class CharacterUnityEditorImportReceiptSocketBake
    {
        public string status { get; set; } = string.Empty;
        public string hash { get; set; } = string.Empty;
        public int requestCount { get; set; }
        public int clipCount { get; set; }
        public int socketTrackCount { get; set; }
        public int checkedFrameCount { get; set; }
        public int issueCount { get; set; }
        public CharacterUnityImportEditorWeaponTraceBakeIssue[] issues { get; set; } =
            Array.Empty<CharacterUnityImportEditorWeaponTraceBakeIssue>();
        public string format { get; set; } = string.Empty;
        public string indexPath { get; set; } = string.Empty;
        public string indexHash { get; set; } = string.Empty;
        public string binaryPath { get; set; } = string.Empty;
        public string binaryHash { get; set; } = string.Empty;
        public long binarySize { get; set; }
    }

    internal sealed class CharacterUnityEditorImportReceiptHurtboxBake
    {
        public string status { get; set; } = string.Empty;
        public string hash { get; set; } = string.Empty;
        public int clipCount { get; set; }
        public int colliderCount { get; set; }
        public int shapeCount { get; set; }
        public int checkedFrameCount { get; set; }
        public int issueCount { get; set; }
        public CharacterUnityImportEditorWeaponTraceBakeIssue[] issues { get; set; } =
            Array.Empty<CharacterUnityImportEditorWeaponTraceBakeIssue>();
    }

    internal sealed class CharacterUnityEditorImportReceiptAbstractBones
    {
        public string status { get; set; } = string.Empty;
        public string[] requiredIds { get; set; } = Array.Empty<string>();
        public int declaredCount { get; set; }
        public int resolvedCount { get; set; }
        public int issueCount { get; set; }
        public string[] issues { get; set; } = Array.Empty<string>();
    }
}

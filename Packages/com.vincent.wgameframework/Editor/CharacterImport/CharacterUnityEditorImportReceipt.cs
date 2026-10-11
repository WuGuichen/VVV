using System;
using System.IO;
using MxFramework.CharacterRuntimeSpawn;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    internal sealed class CharacterUnityImportExpectedIdentity
    {
        public string PackageId { get; set; } = string.Empty;
        public string SourcePackageHash { get; set; } = string.Empty;
        public string GeneratedConfigHash { get; set; } = string.Empty;
        public string GeometryBindingHash { get; set; } = string.Empty;
        public string ResourceMappingHash { get; set; } = string.Empty;
        public string WritePlanHash { get; set; } = string.Empty;
    }

    internal static class CharacterUnityEditorImportReceipt
    {
        internal const string Format = "mx.characterUnityEditorImportReceipt.v3";
        internal const string SchemaVersion = "3.0";
        internal const string LegacyFormat = "mx.characterUnityEditorImportReceipt.v1";
        internal const string LegacyFormatV2 = "mx.characterUnityEditorImportReceipt.v2";
        internal const string FileName = "unity_editor_import_receipt.json";

        internal static bool TryValidateAndWrite(
            CharacterUnityImportExpectedIdentity expected,
            CharacterImportedPackagePrefabBuildResult result,
            out string receiptAssetPath,
            out string error)
        {
            return TryValidateAndWriteCore(expected, result, null, out receiptAssetPath, out error);
        }

        internal static bool TryValidateAndWriteForTests(
            CharacterUnityImportExpectedIdentity expected,
            CharacterImportedPackagePrefabBuildResult result,
            Action<string, string> replaceExistingFile,
            out string receiptAssetPath,
            out string error)
        {
            return TryValidateAndWriteCore(expected, result, replaceExistingFile, out receiptAssetPath, out error);
        }

        internal static bool TryWriteRejected(
            CharacterUnityImportExpectedIdentity expected,
            CharacterImportedPackagePrefabBuildResult result,
            out string receiptAssetPath,
            out string error)
        {
            receiptAssetPath = string.Empty;
            error = string.Empty;
            if (expected == null || result == null || !HasExpectedIdentity(expected, out error))
            {
                if (string.IsNullOrWhiteSpace(error)) error = "Expected identity and failed build result are required.";
                return false;
            }
            if (!string.Equals(expected.PackageId, result.PackageId, StringComparison.Ordinal))
            {
                error = "Rejected receipt package does not match the expected package.";
                return false;
            }
            if (!TryBuildReceiptAssetPath(expected.PackageId, out receiptAssetPath, out error)) return false;
            return TryWritePayload(receiptAssetPath, CreatePayload(result, ready: false), null, out error);
        }

        private static bool TryValidateAndWriteCore(
            CharacterUnityImportExpectedIdentity expected,
            CharacterImportedPackagePrefabBuildResult result,
            Action<string, string> replaceExistingFile,
            out string receiptAssetPath,
            out string error)
        {
            receiptAssetPath = string.Empty;
            if (!TryValidate(expected, result, out error))
                return false;

            if (!TryBuildReceiptAssetPath(expected.PackageId, out receiptAssetPath, out error))
                return false;

            string packageRoot = Path.GetDirectoryName(Path.GetDirectoryName(ProjectPath(receiptAssetPath)));
            if (!CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(packageRoot, expected.PackageId,
                    out _, out CharacterSocketBakeCatalogEvidence evidence, out string[] socketIssues))
            {
                error = "Socket Bake pair cannot be accepted: " + string.Join(" | ", socketIssues);
                return false;
            }
            if (!string.Equals(result.SocketBakeIndexHash, evidence.IndexHash, StringComparison.Ordinal)
                || !string.Equals(result.SocketBakeBinaryHash, evidence.BinaryHash, StringComparison.Ordinal)
                || result.SocketBakeBinarySize != evidence.BinarySize
                || result.SocketBakeSocketTrackCount != evidence.TrackCount
                || result.SocketBakeCheckedFrameCount != evidence.FrameCount)
            {
                error = "Socket Bake evidence no longer matches the files being accepted.";
                return false;
            }

            return TryWritePayload(receiptAssetPath, CreatePayload(result, ready: true), replaceExistingFile, out error);
        }

        private static bool TryWritePayload(
            string receiptAssetPath,
            CharacterUnityEditorImportReceiptFile payload,
            Action<string, string> replaceExistingFile,
            out string error)
        {
            error = string.Empty;
            string json = Serialize(payload);
            string fullPath = ProjectPath(receiptAssetPath);
            string directory = Path.GetDirectoryName(fullPath);
            string temporaryPath = string.Empty;
            try
            {
                if (File.Exists(fullPath) && string.Equals(File.ReadAllText(fullPath), json, StringComparison.Ordinal))
                {
                    AssetDatabase.ImportAsset(receiptAssetPath, ImportAssetOptions.ForceUpdate);
                    return true;
                }

                if (string.IsNullOrWhiteSpace(directory))
                {
                    error = "Receipt directory could not be resolved.";
                    return false;
                }

                Directory.CreateDirectory(directory);
                temporaryPath = fullPath + ".tmp." + Guid.NewGuid().ToString("N");
                File.WriteAllText(temporaryPath, json);
                if (File.Exists(fullPath))
                {
                    if (replaceExistingFile != null)
                        replaceExistingFile(temporaryPath, fullPath);
                    else
                        File.Replace(temporaryPath, fullPath, null, ignoreMetadataErrors: true);
                }
                else
                    File.Move(temporaryPath, fullPath);

                AssetDatabase.ImportAsset(receiptAssetPath, ImportAssetOptions.ForceUpdate);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = "Unity import receipt could not be written: " + ex.Message;
                return false;
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(temporaryPath) && File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        internal static bool TryValidate(
            CharacterUnityImportExpectedIdentity expected,
            CharacterImportedPackagePrefabBuildResult result,
            out string error)
        {
            error = string.Empty;
            if (expected == null)
            {
                error = "Expected import identity is required.";
                return false;
            }

            if (result == null)
            {
                error = "Unity prefab build result is required.";
                return false;
            }

            if (!HasExpectedIdentity(expected, out error))
                return false;

            if (!string.Equals(result.Status, "Built", StringComparison.Ordinal))
            {
                error = "Unity prefab build status is not Built.";
                return false;
            }

            if (!Matches("packageId", expected.PackageId, result.PackageId, out error)
                || !Matches("sourcePackageHash", expected.SourcePackageHash, result.SourcePackageHash, out error)
                || !Matches("generatedConfigHash", expected.GeneratedConfigHash, result.GeneratedConfigHash, out error)
                || !Matches("geometryBindingHash", expected.GeometryBindingHash, result.GeometryBindingHash, out error)
                || !Matches("resourceMappingHash", expected.ResourceMappingHash, result.ResourceMappingHash, out error)
                || !Matches("writePlanHash", expected.WritePlanHash, result.WritePlanHash, out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(result.BuilderVersion))
            {
                error = "Unity prefab builder version is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(result.ArtifactHash))
            {
                error = "Unity artifact hash is missing.";
                return false;
            }

            if (!IsExistingProjectAsset(result.PrefabPath, out error)
                || !IsExistingProjectAsset(result.RuntimePrefabPath, out error)
                || !IsExistingProjectAsset(result.AvatarSourceAssetPath, out error)
                || !IsExistingProjectAsset(result.CharacterActionProfilePath, out error)
                || !IsExistingProjectAsset(result.CharacterActionSetPath, out error))
            {
                return false;
            }

            string rigType = ResolveRigType(result);
            string rigStatus = FirstNonEmpty(result.AvatarSourceRigStatus, result.AvatarSourceHumanoidStatus);
            bool validRig = string.Equals(rigType, "Humanoid", StringComparison.Ordinal)
                ? string.Equals(rigStatus, "ValidHuman", StringComparison.Ordinal)
                : string.Equals(rigType, "Generic", StringComparison.Ordinal)
                    && string.Equals(rigStatus, "ValidGeneric", StringComparison.Ordinal);
            if (!string.Equals(result.AvatarSourceStatus, "Valid", StringComparison.Ordinal)
                || !validRig
                || string.IsNullOrWhiteSpace(result.AvatarSourceGuid))
            {
                error = "Unity Avatar acceptance is not valid rig-aware evidence.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(result.BodyMaterialPath)
                && !IsExistingProjectAsset(result.BodyMaterialPath, out error))
            {
                return false;
            }

            for (int i = 0; result.Materials != null && i < result.Materials.Length; i++)
            {
                CharacterGeneratedMaterialBuildResult material = result.Materials[i];
                if (material == null
                    || !string.Equals(material.Status, "Assigned", StringComparison.Ordinal)
                    || material.AssignedSlotCount <= 0
                    || !IsExistingProjectAsset(material.MaterialPath, out error))
                {
                    if (string.IsNullOrWhiteSpace(error))
                        error = "Generated material evidence is missing or not assigned.";
                    return false;
                }
                bool opaque = string.Equals(material.SurfaceMode, "Opaque", StringComparison.OrdinalIgnoreCase);
                bool alphaClip = string.Equals(material.SurfaceMode, "AlphaClip", StringComparison.OrdinalIgnoreCase);
                if ((!opaque && !alphaClip)
                    || (alphaClip
                        && (float.IsNaN(material.AlphaCutoff)
                            || float.IsInfinity(material.AlphaCutoff)
                            || material.AlphaCutoff < 0f
                            || material.AlphaCutoff > 1f)))
                {
                    error = "Generated material surface evidence is unsupported or has an invalid alpha cutoff.";
                    return false;
                }
            }

            if (!result.CharacterActionProfileReady || !result.CharacterActionActionSetReady)
            {
                error = "CharacterAction Profile and ActionSet must both be ready.";
                return false;
            }

            if (!result.WeaponTraceBakeReady
                || !string.Equals(result.WeaponTraceBakeStatus, "Ready", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(result.WeaponTraceBakeHash)
                || result.WeaponTraceBakeIssueCount != 0
                || (result.WeaponTraceBakeIssues != null && result.WeaponTraceBakeIssues.Length != 0))
            {
                error = "Weapon Trace Bake acceptance is not ready.";
                return false;
            }

            if (!result.SocketBakeReady
                || !string.Equals(result.SocketBakeStatus, "Ready", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(result.SocketBakeHash)
                || result.SocketBakeIssueCount != 0
                || (result.SocketBakeIssues != null && result.SocketBakeIssues.Length != 0))
            {
                error = "Socket Bake acceptance is not ready.";
                return false;
            }

            // A v3 receipt must carry the verified pair identity, not only the bake input hash.
            if (!string.Equals(result.SocketBakeFormat, CharacterSocketBakeCatalogBinary.Format, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(result.SocketBakeIndexHash)
                || !string.Equals(
                    result.SocketBakeIndexPath,
                    CharacterSocketBakeCatalogBinary.DefaultIndexRelativePath,
                    StringComparison.Ordinal)
                || !string.Equals(
                    result.SocketBakeBinaryPath,
                    CharacterSocketBakeCatalogBinary.DefaultBinaryRelativePath,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(result.SocketBakeBinaryHash)
                || result.SocketBakeBinarySize <= 0)
            {
                error = "Socket Bake product identity is not ready.";
                return false;
            }

            if (!result.HurtboxBakeReady
                || !string.Equals(result.HurtboxBakeStatus, "Ready", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(result.HurtboxBakeHash)
                || result.HurtboxBakeIssueCount != 0
                || (result.HurtboxBakeIssues != null && result.HurtboxBakeIssues.Length != 0))
            {
                error = "Hurtbox Bake acceptance is not ready.";
                return false;
            }

            if (!string.Equals(result.AbstractBoneStatus, "Ready", StringComparison.Ordinal)
                || result.AbstractBoneDeclaredCount <= 0
                || result.AbstractBoneResolvedCount != result.AbstractBoneDeclaredCount
                || result.AbstractBoneIssueCount != 0
                || (result.AbstractBoneIssues != null && result.AbstractBoneIssues.Length != 0))
            {
                error = "Abstract bone acceptance is not ready.";
                return false;
            }

            if (result.Issues != null && result.Issues.Length != 0)
            {
                error = "Unity prefab build contains acceptance issues.";
                return false;
            }

            return true;
        }

        internal static bool TryBuildReceiptAssetPath(string packageId, out string assetPath, out string error)
        {
            assetPath = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(packageId))
            {
                error = "packageId is required.";
                return false;
            }

            for (int i = 0; i < packageId.Length; i++)
            {
                char value = packageId[i];
                if (!char.IsLetterOrDigit(value) && value != '_' && value != '-' && value != '.')
                {
                    error = "packageId contains an invalid path character.";
                    return false;
                }
            }

            if (packageId == "." || packageId == "..")
            {
                error = "packageId is not a safe path segment.";
                return false;
            }

            assetPath = "Assets/MxFrameworkGenerated/CharacterPackages/"
                + packageId
                + "/package_cache/"
                + FileName;
            return true;
        }

        internal static string Serialize(CharacterUnityEditorImportReceiptFile payload)
        {
            return JsonConvert.SerializeObject(payload, Formatting.Indented) + "\n";
        }

        private static CharacterUnityEditorImportReceiptFile CreatePayload(
            CharacterImportedPackagePrefabBuildResult result,
            bool ready)
        {
            string[] resourceIds = result.DefaultWeaponResourceIds == null
                ? Array.Empty<string>()
                : (string[])result.DefaultWeaponResourceIds.Clone();
            Array.Sort(resourceIds, StringComparer.Ordinal);
            CharacterUnityEditorImportReceiptMaterial[] materials = BuildMaterialReceipts(result);
            CharacterUnityEditorImportReceiptMaterial primaryMaterial = Array.Find(
                materials,
                material => string.Equals(material?.SourceMaterialName, "Body", StringComparison.OrdinalIgnoreCase))
                ?? (materials.Length > 0 ? materials[0] : null);

            return new CharacterUnityEditorImportReceiptFile
            {
                Ready = ready,
                PackageId = result.PackageId,
                BuilderVersion = result.BuilderVersion,
                Hashes = new CharacterUnityEditorImportReceiptHashes
                {
                    SourcePackageHash = result.SourcePackageHash,
                    GeneratedConfigHash = result.GeneratedConfigHash,
                    GeometryBindingHash = result.GeometryBindingHash,
                    ResourceMappingHash = result.ResourceMappingHash,
                    WritePlanHash = result.WritePlanHash,
                    UnityArtifactHash = result.ArtifactHash,
                },
                Prefab = new CharacterUnityEditorImportReceiptPrefab
                {
                    PrefabPath = NormalizeProjectPath(result.PrefabPath),
                    RuntimePrefabPath = NormalizeProjectPath(result.RuntimePrefabPath),
                },
                Avatar = new CharacterUnityEditorImportReceiptAvatar
                {
                    Status = result.AvatarSourceStatus,
                    AssetPath = NormalizeProjectPath(result.AvatarSourceAssetPath),
                    Guid = result.AvatarSourceGuid,
                    RigType = ResolveRigType(result),
                    RigStatus = FirstNonEmpty(result.AvatarSourceRigStatus, result.AvatarSourceHumanoidStatus),
                    HumanoidStatus = result.AvatarSourceHumanoidStatus,
                },
                Material = new CharacterUnityEditorImportReceiptMaterial
                {
                    Status = result.BodyMaterialBindingStatus,
                    TemplateId = result.BodyMaterialTemplateId,
                    MaterialPath = NormalizeProjectPath(result.BodyMaterialPath),
                    TextureResourceKey = result.BodyMaterialTextureResourceKey,
                    RendererCount = result.BodyMaterialRendererCount,
                    PaletteAtlasResourceKey = result.PaletteAtlasResourceKey,
                    PaletteCatalogHash = result.PaletteCatalogHash,
                    PaletteColorCount = result.PaletteColorCount,
                    SurfaceProfileCount = result.SurfaceProfileCount,
                    InvalidFaceCount = result.PaletteInvalidTokenCount,
                    PaletteRendererCount = result.PaletteRendererCount,
                    PaletteDiagnostic = result.PaletteDiagnostic,
                    FaceDetailTextureResourceKey = result.FaceDetailTextureResourceKey,
                    FaceDetailTextureHash = result.FaceDetailTextureHash,
                    FaceDetailTextureSize = result.FaceDetailTextureSize,
                    FaceDetailDiagnostic = result.FaceDetailDiagnostic,
                    SurfaceMode = primaryMaterial?.SurfaceMode ?? "Opaque",
                    AlphaCutoff = primaryMaterial?.AlphaCutoff ?? 0.5f,
                },
                Materials = materials,
                Geometry = new CharacterUnityEditorImportReceiptGeometry
                {
                    SocketCount = result.SocketCount,
                    WeaponAttachmentCount = result.WeaponAttachmentCount,
                    MountedWeaponCount = result.MountedWeaponCount,
                },
                CharacterAction = new CharacterUnityEditorImportReceiptCharacterAction
                {
                    ProfileReady = result.CharacterActionProfileReady,
                    ActionSetReady = result.CharacterActionActionSetReady,
                    ProfilePath = NormalizeProjectPath(result.CharacterActionProfilePath),
                    ActionSetPath = NormalizeProjectPath(result.CharacterActionSetPath),
                },
                WeaponTraceBake = new CharacterUnityEditorImportReceiptWeaponTraceBake
                {
                    Status = result.WeaponTraceBakeStatus,
                    Hash = result.WeaponTraceBakeHash,
                    RequestCount = result.WeaponTraceBakeRequestCount,
                    ArtifactCount = result.WeaponTraceBakeArtifactCount,
                    CheckedFrameCount = result.WeaponTraceBakeCheckedFrameCount,
                    IssueCount = result.WeaponTraceBakeIssueCount,
                    Issues = result.WeaponTraceBakeIssues ?? Array.Empty<CharacterWeaponTraceBakeIssueReport>(),
                },
                SocketBake = new CharacterUnityEditorImportReceiptSocketBake
                {
                    Status = result.SocketBakeStatus,
                    Hash = result.SocketBakeHash,
                    RequestCount = result.SocketBakeRequestCount,
                    ClipCount = result.SocketBakeClipCount,
                    SocketTrackCount = result.SocketBakeSocketTrackCount,
                    CheckedFrameCount = result.SocketBakeCheckedFrameCount,
                    IssueCount = result.SocketBakeIssueCount,
                    Issues = result.SocketBakeIssues ?? Array.Empty<CharacterWeaponTraceBakeIssueReport>(),
                    Format = result.SocketBakeFormat,
                    IndexPath = result.SocketBakeIndexPath,
                    IndexHash = result.SocketBakeIndexHash,
                    BinaryPath = result.SocketBakeBinaryPath,
                    BinaryHash = result.SocketBakeBinaryHash,
                    BinarySize = result.SocketBakeBinarySize,
                },
                HurtboxBake = new CharacterUnityEditorImportReceiptHurtboxBake
                {
                    Status = result.HurtboxBakeStatus,
                    Hash = result.HurtboxBakeHash,
                    ClipCount = result.HurtboxBakeClipCount,
                    ColliderCount = result.HurtboxBakeColliderCount,
                    ShapeCount = result.HurtboxBakeShapeCount,
                    CheckedFrameCount = result.HurtboxBakeCheckedFrameCount,
                    IssueCount = result.HurtboxBakeIssueCount,
                    Issues = result.HurtboxBakeIssues ?? Array.Empty<CharacterWeaponTraceBakeIssueReport>(),
                },
                AbstractBones = new CharacterUnityEditorImportReceiptAbstractBones
                {
                    Status = result.AbstractBoneStatus,
                    RequiredIds = result.AbstractBoneRequiredIds ?? Array.Empty<string>(),
                    DeclaredCount = result.AbstractBoneDeclaredCount,
                    ResolvedCount = result.AbstractBoneResolvedCount,
                    IssueCount = result.AbstractBoneIssueCount,
                    Issues = result.AbstractBoneIssues ?? Array.Empty<string>(),
                },
                DefaultWeaponResourceIds = resourceIds,
            };
        }

        private static CharacterUnityEditorImportReceiptMaterial[] BuildMaterialReceipts(
            CharacterImportedPackagePrefabBuildResult buildResult)
        {
            CharacterGeneratedMaterialBuildResult[] source = buildResult?.Materials;
            if (source == null || source.Length == 0)
            {
                if (string.IsNullOrWhiteSpace(buildResult?.BodyMaterialPath))
                    return Array.Empty<CharacterUnityEditorImportReceiptMaterial>();
                return new[]
                {
                    new CharacterUnityEditorImportReceiptMaterial
                    {
                        Status = buildResult.BodyMaterialBindingStatus,
                        TemplateId = buildResult.BodyMaterialTemplateId,
                        SourceMaterialName = "Body",
                        MaterialPath = NormalizeProjectPath(buildResult.BodyMaterialPath),
                        TextureResourceKey = buildResult.BodyMaterialTextureResourceKey,
                        RendererCount = buildResult.BodyMaterialRendererCount,
                        SurfaceMode = "Opaque",
                        AlphaCutoff = 0.5f,
                        AssignedSlotCount = Math.Max(1, buildResult.BodyMaterialRendererCount)
                    }
                };
            }
            var result = new CharacterUnityEditorImportReceiptMaterial[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CharacterGeneratedMaterialBuildResult material = source[i] ?? new CharacterGeneratedMaterialBuildResult();
                result[i] = new CharacterUnityEditorImportReceiptMaterial
                {
                    Status = material.Status,
                    SourceMaterialName = material.SourceMaterialName,
                    MaterialPath = NormalizeProjectPath(material.MaterialPath),
                    TextureResourceKey = material.AlbedoTextureResourceKey,
                    NormalTextureResourceKey = material.NormalTextureResourceKey,
                    MaskTextureResourceKey = material.MaskTextureResourceKey,
                    SurfaceMode = string.Equals(material.SurfaceMode, "AlphaClip", StringComparison.OrdinalIgnoreCase)
                        ? "AlphaClip"
                        : "Opaque",
                    AlphaCutoff = material.AlphaCutoff,
                    AssignedSlotCount = material.AssignedSlotCount
                };
            }
            Array.Sort(result, (left, right) => string.CompareOrdinal(left?.SourceMaterialName, right?.SourceMaterialName));
            return result;
        }

        private static string ResolveRigType(CharacterImportedPackagePrefabBuildResult result)
        {
            if (string.Equals(result?.RigType, "Humanoid", StringComparison.OrdinalIgnoreCase))
                return "Humanoid";
            if (string.Equals(result?.RigType, "Generic", StringComparison.OrdinalIgnoreCase))
                return "Generic";
            return string.Equals(result?.AvatarSourceHumanoidStatus, "ValidHuman", StringComparison.Ordinal)
                ? "Humanoid"
                : string.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; values != null && i < values.Length; i++)
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            return string.Empty;
        }

        private static bool HasExpectedIdentity(CharacterUnityImportExpectedIdentity expected, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(expected.PackageId)
                || string.IsNullOrWhiteSpace(expected.SourcePackageHash)
                || string.IsNullOrWhiteSpace(expected.GeneratedConfigHash)
                || string.IsNullOrWhiteSpace(expected.GeometryBindingHash)
                || string.IsNullOrWhiteSpace(expected.ResourceMappingHash)
                || string.IsNullOrWhiteSpace(expected.WritePlanHash))
            {
                error = "Request packageId and all expected Compiler hashes are required.";
                return false;
            }

            return true;
        }

        private static bool Matches(string field, string expected, string actual, out string error)
        {
            if (string.Equals(expected, actual, StringComparison.Ordinal))
            {
                error = string.Empty;
                return true;
            }

            error = "Unity result " + field + " does not match the import request.";
            return false;
        }

        private static bool IsExistingProjectAsset(string assetPath, out string error)
        {
            string rawPath = (assetPath ?? string.Empty).Trim();
            string normalized = NormalizeProjectPath(assetPath);
            if (string.IsNullOrWhiteSpace(rawPath)
                || Path.IsPathRooted(rawPath)
                || rawPath.StartsWith("/", StringComparison.Ordinal)
                || rawPath.StartsWith("\\", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(normalized)
                || !normalized.StartsWith("Assets/", StringComparison.Ordinal)
                || normalized.Contains("../", StringComparison.Ordinal)
                || Path.IsPathRooted(normalized))
            {
                error = "Receipt evidence path is not a project-relative Assets path: " + (assetPath ?? string.Empty);
                return false;
            }

            string fullPath = ProjectPath(normalized);
            string projectRoot = ProjectPath(string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            {
                error = "Receipt evidence asset is missing: " + normalized;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string NormalizeProjectPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
        }

        private static string ProjectPath(string relative)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(root, (relative ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)));
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class CharacterUnityEditorImportReceiptFile
    {
        [JsonProperty("format", Order = 1)]
        public string Format { get; set; } = CharacterUnityEditorImportReceipt.Format;

        [JsonProperty("schemaVersion", Order = 2)]
        public string SchemaVersion { get; set; } = CharacterUnityEditorImportReceipt.SchemaVersion;

        [JsonProperty("ready", Order = 3)]
        public bool Ready { get; set; } = true;

        [JsonProperty("packageId", Order = 4)]
        public string PackageId { get; set; } = string.Empty;

        [JsonProperty("builderVersion", Order = 5)]
        public string BuilderVersion { get; set; } = string.Empty;

        [JsonProperty("hashes", Order = 6)]
        public CharacterUnityEditorImportReceiptHashes Hashes { get; set; } = new CharacterUnityEditorImportReceiptHashes();

        [JsonProperty("prefab", Order = 7)]
        public CharacterUnityEditorImportReceiptPrefab Prefab { get; set; } = new CharacterUnityEditorImportReceiptPrefab();

        [JsonProperty("avatar", Order = 8)]
        public CharacterUnityEditorImportReceiptAvatar Avatar { get; set; } = new CharacterUnityEditorImportReceiptAvatar();

        [JsonProperty("material", Order = 9)]
        public CharacterUnityEditorImportReceiptMaterial Material { get; set; } = new CharacterUnityEditorImportReceiptMaterial();

        [JsonProperty("materials", Order = 10)]
        public CharacterUnityEditorImportReceiptMaterial[] Materials { get; set; } = Array.Empty<CharacterUnityEditorImportReceiptMaterial>();

        [JsonProperty("geometry", Order = 11)]
        public CharacterUnityEditorImportReceiptGeometry Geometry { get; set; } = new CharacterUnityEditorImportReceiptGeometry();

        [JsonProperty("characterAction", Order = 12)]
        public CharacterUnityEditorImportReceiptCharacterAction CharacterAction { get; set; } = new CharacterUnityEditorImportReceiptCharacterAction();

        [JsonProperty("weaponTraceBake", Order = 13)]
        public CharacterUnityEditorImportReceiptWeaponTraceBake WeaponTraceBake { get; set; } = new CharacterUnityEditorImportReceiptWeaponTraceBake();

        [JsonProperty("socketBake", Order = 14)]
        public CharacterUnityEditorImportReceiptSocketBake SocketBake { get; set; } = new CharacterUnityEditorImportReceiptSocketBake();

        [JsonProperty("hurtboxBake", Order = 15)]
        public CharacterUnityEditorImportReceiptHurtboxBake HurtboxBake { get; set; } = new CharacterUnityEditorImportReceiptHurtboxBake();

        [JsonProperty("abstractBones", Order = 16)]
        public CharacterUnityEditorImportReceiptAbstractBones AbstractBones { get; set; } = new CharacterUnityEditorImportReceiptAbstractBones();

        [JsonProperty("defaultWeaponResourceIds", Order = 17)]
        public string[] DefaultWeaponResourceIds { get; set; } = Array.Empty<string>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class CharacterUnityEditorImportReceiptHashes
    {
        [JsonProperty("sourcePackageHash", Order = 1)] public string SourcePackageHash { get; set; } = string.Empty;
        [JsonProperty("generatedConfigHash", Order = 2)] public string GeneratedConfigHash { get; set; } = string.Empty;
        [JsonProperty("geometryBindingHash", Order = 3)] public string GeometryBindingHash { get; set; } = string.Empty;
        [JsonProperty("resourceMappingHash", Order = 4)] public string ResourceMappingHash { get; set; } = string.Empty;
        [JsonProperty("writePlanHash", Order = 5)] public string WritePlanHash { get; set; } = string.Empty;
        [JsonProperty("unityArtifactHash", Order = 6)] public string UnityArtifactHash { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class CharacterUnityEditorImportReceiptPrefab
    {
        [JsonProperty("prefabPath", Order = 1)] public string PrefabPath { get; set; } = string.Empty;
        [JsonProperty("runtimePrefabPath", Order = 2)] public string RuntimePrefabPath { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class CharacterUnityEditorImportReceiptAvatar
    {
        [JsonProperty("status", Order = 1)] public string Status { get; set; } = string.Empty;
        [JsonProperty("assetPath", Order = 2)] public string AssetPath { get; set; } = string.Empty;
        [JsonProperty("guid", Order = 3)] public string Guid { get; set; } = string.Empty;
        [JsonProperty("rigType", Order = 4)] public string RigType { get; set; } = string.Empty;
        [JsonProperty("rigStatus", Order = 5)] public string RigStatus { get; set; } = string.Empty;
        [JsonProperty("humanoidStatus", Order = 6)] public string HumanoidStatus { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class CharacterUnityEditorImportReceiptMaterial
    {
        [JsonProperty("status", Order = 1)] public string Status { get; set; } = string.Empty;
        [JsonProperty("templateId", Order = 2)] public string TemplateId { get; set; } = string.Empty;
        [JsonProperty("materialPath", Order = 3)] public string MaterialPath { get; set; } = string.Empty;
        [JsonProperty("textureResourceKey", Order = 4)] public string TextureResourceKey { get; set; } = string.Empty;
        [JsonProperty("rendererCount", Order = 5)] public int RendererCount { get; set; }
        [JsonProperty("sourceMaterialName", Order = 6)] public string SourceMaterialName { get; set; } = string.Empty;
        [JsonProperty("normalTextureResourceKey", Order = 7)] public string NormalTextureResourceKey { get; set; } = string.Empty;
        [JsonProperty("maskTextureResourceKey", Order = 8)] public string MaskTextureResourceKey { get; set; } = string.Empty;
        [JsonProperty("surfaceMode", Order = 9)] public string SurfaceMode { get; set; } = "Opaque";
        [JsonProperty("alphaCutoff", Order = 10)] public float AlphaCutoff { get; set; } = 0.5f;
        [JsonProperty("assignedSlotCount", Order = 11)] public int AssignedSlotCount { get; set; }
        [JsonProperty("paletteAtlasResourceKey", Order = 12)] public string PaletteAtlasResourceKey { get; set; } = string.Empty;
        [JsonProperty("paletteCatalogHash", Order = 13)] public string PaletteCatalogHash { get; set; } = string.Empty;
        [JsonProperty("paletteColorCount", Order = 14)] public int PaletteColorCount { get; set; }
        [JsonProperty("surfaceProfileCount", Order = 15)] public int SurfaceProfileCount { get; set; }
        [JsonProperty("invalidFaceCount", Order = 16)] public int InvalidFaceCount { get; set; }
        [JsonProperty("paletteRendererCount", Order = 17)] public int PaletteRendererCount { get; set; }
        [JsonProperty("paletteDiagnostic", Order = 18)] public string PaletteDiagnostic { get; set; } = string.Empty;
        [JsonProperty("faceDetailTextureResourceKey", Order = 19)] public string FaceDetailTextureResourceKey { get; set; } = string.Empty;
        [JsonProperty("faceDetailTextureHash", Order = 20)] public string FaceDetailTextureHash { get; set; } = string.Empty;
        [JsonProperty("faceDetailTextureSize", Order = 21)] public int FaceDetailTextureSize { get; set; }
        [JsonProperty("faceDetailDiagnostic", Order = 22)] public string FaceDetailDiagnostic { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class CharacterUnityEditorImportReceiptGeometry
    {
        [JsonProperty("socketCount", Order = 1)] public int SocketCount { get; set; }
        [JsonProperty("weaponAttachmentCount", Order = 2)] public int WeaponAttachmentCount { get; set; }
        [JsonProperty("mountedWeaponCount", Order = 3)] public int MountedWeaponCount { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class CharacterUnityEditorImportReceiptCharacterAction
    {
        [JsonProperty("profileReady", Order = 1)] public bool ProfileReady { get; set; }
        [JsonProperty("actionSetReady", Order = 2)] public bool ActionSetReady { get; set; }
        [JsonProperty("profilePath", Order = 3)] public string ProfilePath { get; set; } = string.Empty;
        [JsonProperty("actionSetPath", Order = 4)] public string ActionSetPath { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class CharacterUnityEditorImportReceiptWeaponTraceBake
    {
        [JsonProperty("status", Order = 1)] public string Status { get; set; } = string.Empty;
        [JsonProperty("hash", Order = 2)] public string Hash { get; set; } = string.Empty;
        [JsonProperty("requestCount", Order = 3)] public int RequestCount { get; set; }
        [JsonProperty("artifactCount", Order = 4)] public int ArtifactCount { get; set; }
        [JsonProperty("checkedFrameCount", Order = 5)] public int CheckedFrameCount { get; set; }
        [JsonProperty("issueCount", Order = 6)] public int IssueCount { get; set; }
        [JsonProperty("issues", Order = 7)] public CharacterWeaponTraceBakeIssueReport[] Issues { get; set; } = Array.Empty<CharacterWeaponTraceBakeIssueReport>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class CharacterUnityEditorImportReceiptSocketBake
    {
        [JsonProperty("status", Order = 1)] public string Status { get; set; } = string.Empty;
        [JsonProperty("hash", Order = 2)] public string Hash { get; set; } = string.Empty;
        [JsonProperty("requestCount", Order = 3)] public int RequestCount { get; set; }
        [JsonProperty("clipCount", Order = 4)] public int ClipCount { get; set; }
        [JsonProperty("socketTrackCount", Order = 5)] public int SocketTrackCount { get; set; }
        [JsonProperty("checkedFrameCount", Order = 6)] public int CheckedFrameCount { get; set; }
        [JsonProperty("issueCount", Order = 7)] public int IssueCount { get; set; }
        [JsonProperty("issues", Order = 8)] public CharacterWeaponTraceBakeIssueReport[] Issues { get; set; } = Array.Empty<CharacterWeaponTraceBakeIssueReport>();
        [JsonProperty("format", Order = 9)] public string Format { get; set; } = string.Empty;
        [JsonProperty("indexPath", Order = 10)] public string IndexPath { get; set; } = string.Empty;
        [JsonProperty("indexHash", Order = 11)] public string IndexHash { get; set; } = string.Empty;
        [JsonProperty("binaryPath", Order = 12)] public string BinaryPath { get; set; } = string.Empty;
        [JsonProperty("binaryHash", Order = 13)] public string BinaryHash { get; set; } = string.Empty;
        [JsonProperty("binarySize", Order = 14)] public long BinarySize { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class CharacterUnityEditorImportReceiptHurtboxBake
    {
        [JsonProperty("status", Order = 1)] public string Status { get; set; } = string.Empty;
        [JsonProperty("hash", Order = 2)] public string Hash { get; set; } = string.Empty;
        [JsonProperty("clipCount", Order = 3)] public int ClipCount { get; set; }
        [JsonProperty("colliderCount", Order = 4)] public int ColliderCount { get; set; }
        [JsonProperty("shapeCount", Order = 5)] public int ShapeCount { get; set; }
        [JsonProperty("checkedFrameCount", Order = 6)] public int CheckedFrameCount { get; set; }
        [JsonProperty("issueCount", Order = 7)] public int IssueCount { get; set; }
        [JsonProperty("issues", Order = 8)] public CharacterWeaponTraceBakeIssueReport[] Issues { get; set; } = Array.Empty<CharacterWeaponTraceBakeIssueReport>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class CharacterUnityEditorImportReceiptAbstractBones
    {
        [JsonProperty("status", Order = 1)] public string Status { get; set; } = string.Empty;
        [JsonProperty("requiredIds", Order = 2)] public string[] RequiredIds { get; set; } = Array.Empty<string>();
        [JsonProperty("declaredCount", Order = 3)] public int DeclaredCount { get; set; }
        [JsonProperty("resolvedCount", Order = 4)] public int ResolvedCount { get; set; }
        [JsonProperty("issueCount", Order = 5)] public int IssueCount { get; set; }
        [JsonProperty("issues", Order = 6)] public string[] Issues { get; set; } = Array.Empty<string>();
    }
}

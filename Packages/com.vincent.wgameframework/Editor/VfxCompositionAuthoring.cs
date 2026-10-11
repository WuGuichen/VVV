using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MxFramework.Vfx.Unity;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Editor
{
    [Serializable]
    internal sealed class VfxCompositionDraftView
    {
        internal const string CurrentSchemaVersion = "mx.vfx.composition-draft.v3";
        internal const string PreviousSchemaVersion = "mx.vfx.composition-draft.v2";
        [JsonProperty("schemaVersion")] public string SchemaVersion = CurrentSchemaVersion;
        [JsonProperty("draftId")] public string DraftId = "vfx.composition.untitled";
        [JsonProperty("displayName")] public string DisplayName = "未命名 VFX 组合";
        [JsonProperty("brief")] public string Brief = string.Empty;
        [JsonProperty("targetEffectId")] public string TargetEffectId = string.Empty;
        [JsonProperty("sourceLibraryId")] public string SourceLibraryId = "source.piloto.studio";
        [JsonProperty("sourceIndexHash")] public string SourceIndexHash = string.Empty;
        [JsonProperty("curationHash")] public string CurationHash = string.Empty;
        [JsonProperty("catalogHash")] public string CatalogHash = string.Empty;
        [JsonProperty("framesPerSecond")] public int FramesPerSecond = 60;
        [JsonProperty("dependencies")] public VfxDraftDependencySetView Dependencies = new VfxDraftDependencySetView();
        [JsonProperty("revision")] public int Revision = 1;
        [JsonProperty("loopPlaybackMode")] public string LoopPlaybackMode = "OneShot";
        [JsonProperty("phaseDurationOverrides")] public VfxCompositionPhaseDurationOverridesView PhaseDurationOverrides = new VfxCompositionPhaseDurationOverridesView();
        [JsonProperty("previewContext", NullValueHandling = NullValueHandling.Ignore)] public VfxCompositionPreviewContextView PreviewContext = new VfxCompositionPreviewContextView();
        [JsonProperty("resourceIdentity", NullValueHandling = NullValueHandling.Ignore)] public VfxCompositionStableResourceView ResourceIdentity;
        [JsonProperty("localElements", NullValueHandling = NullValueHandling.Ignore)] public List<VfxSourceElementView> LocalElements = new List<VfxSourceElementView>();
        [JsonProperty("layers")] public List<VfxCompositionLayerView> Layers = new List<VfxCompositionLayerView>();
    }

    [Serializable]
    internal sealed class VfxCompositionStableResourceView
    {
        [JsonProperty("effectId")] public string EffectId = string.Empty;
        [JsonProperty("resourceKey")] public VfxPublishedResourceKey ResourceKey = new VfxPublishedResourceKey();
        [JsonProperty("stablePrefabPath")] public string StablePrefabPath = string.Empty;
        [JsonProperty("role")] public string Role = "Impact";
        [JsonProperty("anchor")] public string Anchor = "HitPoint";
        [JsonProperty("space")] public string Space = "World";
    }

    [Serializable]
    internal sealed class VfxCompositionPreviewContextView
    {
        internal const string VfxOnlyMode = "VfxOnly";
        internal const string CharacterAttachedMode = "CharacterAttached";

        [JsonProperty("mode")] public string Mode = VfxOnlyMode;
        [JsonProperty("characterStableId")] public string CharacterStableId = string.Empty;
        [JsonProperty("packageId")] public string PackageId = string.Empty;
        [JsonProperty("characterEvidenceHash")] public string CharacterEvidenceHash = string.Empty;
        [JsonProperty("posePresetId")] public string PosePresetId = "Idle";
        [JsonProperty("animationResourceKey")] public string AnimationResourceKey = string.Empty;
        [JsonProperty("animationLoop")] public bool AnimationLoop = true;
        [JsonProperty("animationSpeed")] public float AnimationSpeed = 1f;
        [JsonProperty("socketId")] public string SocketId = string.Empty;
        [JsonProperty("phaseOverrides", NullValueHandling = NullValueHandling.Ignore)] public VfxCompositionPhasePreviewOverridesView PhaseOverrides;
    }

    [Serializable]
    internal sealed class VfxCompositionPhasePreviewOverridesView
    {
        [JsonProperty("Build", NullValueHandling = NullValueHandling.Ignore)] public VfxCompositionPreviewContextOverrideView Build;
        [JsonProperty("Rebuild", NullValueHandling = NullValueHandling.Ignore)] public VfxCompositionPreviewContextOverrideView Rebuild;
        [JsonProperty("Loop", NullValueHandling = NullValueHandling.Ignore)] public VfxCompositionPreviewContextOverrideView Loop;
        [JsonProperty("Complete", NullValueHandling = NullValueHandling.Ignore)] public VfxCompositionPreviewContextOverrideView Complete;
        [JsonProperty("Dissipate", NullValueHandling = NullValueHandling.Ignore)] public VfxCompositionPreviewContextOverrideView Dissipate;

        internal VfxCompositionPreviewContextOverrideView Get(string phase)
        {
            return phase switch
            {
                "Build" => Build,
                "Rebuild" => Rebuild,
                "Loop" => Loop,
                "Complete" => Complete,
                "Dissipate" => Dissipate,
                _ => null
            };
        }

        internal void Set(string phase, VfxCompositionPreviewContextOverrideView value)
        {
            switch (phase)
            {
                case "Build": Build = value; break;
                case "Rebuild": Rebuild = value; break;
                case "Loop": Loop = value; break;
                case "Complete": Complete = value; break;
                case "Dissipate": Dissipate = value; break;
            }
        }

        internal bool IsEmpty => Build == null && Rebuild == null && Loop == null && Complete == null && Dissipate == null;
    }

    [Serializable]
    internal sealed class VfxCompositionPreviewContextOverrideView
    {
        [JsonProperty("mode", NullValueHandling = NullValueHandling.Ignore)] public string Mode;
        [JsonProperty("characterStableId", NullValueHandling = NullValueHandling.Ignore)] public string CharacterStableId;
        [JsonProperty("packageId", NullValueHandling = NullValueHandling.Ignore)] public string PackageId;
        [JsonProperty("characterEvidenceHash", NullValueHandling = NullValueHandling.Ignore)] public string CharacterEvidenceHash;
        [JsonProperty("posePresetId", NullValueHandling = NullValueHandling.Ignore)] public string PosePresetId;
        [JsonProperty("animationResourceKey", NullValueHandling = NullValueHandling.Ignore)] public string AnimationResourceKey;
        [JsonProperty("animationLoop", NullValueHandling = NullValueHandling.Ignore)] public bool? AnimationLoop;
        [JsonProperty("animationSpeed", NullValueHandling = NullValueHandling.Ignore)] public float? AnimationSpeed;
        [JsonProperty("socketId", NullValueHandling = NullValueHandling.Ignore)] public string SocketId;

        internal bool IsEmpty => Mode == null
            && CharacterStableId == null && PackageId == null && CharacterEvidenceHash == null
            && PosePresetId == null && AnimationResourceKey == null
            && !AnimationLoop.HasValue && !AnimationSpeed.HasValue && SocketId == null;

        internal int FieldCount
        {
            get
            {
                int count = 0;
                if (Mode != null) count++;
                if (CharacterStableId != null || PackageId != null || CharacterEvidenceHash != null) count++;
                if (PosePresetId != null) count++;
                if (AnimationResourceKey != null) count++;
                if (AnimationLoop.HasValue) count++;
                if (AnimationSpeed.HasValue) count++;
                if (SocketId != null) count++;
                return count;
            }
        }
    }

    [Serializable]
    internal sealed class VfxCompositionPhaseDurationOverridesView
    {
        [JsonProperty("Build", NullValueHandling = NullValueHandling.Include)] public int? BuildFrames;
        [JsonProperty("Rebuild", NullValueHandling = NullValueHandling.Include)] public int? RebuildFrames;
        [JsonProperty("Loop", NullValueHandling = NullValueHandling.Include)] public int? LoopFrames;
        [JsonProperty("Complete", NullValueHandling = NullValueHandling.Include)] public int? CompleteFrames;
        [JsonProperty("Dissipate", NullValueHandling = NullValueHandling.Include)] public int? DissipateFrames;

        public int? Get(string phase)
        {
            switch (phase)
            {
                case "Build": return BuildFrames;
                case "Rebuild": return RebuildFrames;
                case "Loop": return LoopFrames;
                case "Complete": return CompleteFrames;
                case "Dissipate": return DissipateFrames;
                default: return null;
            }
        }

        public void Set(string phase, int? frames)
        {
            switch (phase)
            {
                case "Build": BuildFrames = frames; break;
                case "Rebuild": RebuildFrames = frames; break;
                case "Loop": LoopFrames = frames; break;
                case "Complete": CompleteFrames = frames; break;
                case "Dissipate": DissipateFrames = frames; break;
            }
        }
    }

    [Serializable]
    internal sealed class VfxCompositionLayerView
    {
        [JsonProperty("layerId")] public string LayerId = string.Empty;
        [JsonProperty("elementId")] public string ElementId = string.Empty;
        [JsonProperty("primitiveId")] public string PrimitiveId = string.Empty;
        [JsonProperty("phase")] public string Phase = "Loop";
        [JsonProperty("groupId")] public string GroupId = string.Empty;
        [JsonProperty("enabled")] public bool Enabled = true;
        [JsonProperty("localPosition")] public List<float> LocalPosition = new List<float> { 0f, 0f, 0f };
        [JsonProperty("localEulerAngles")] public List<float> LocalEulerAngles = new List<float> { 0f, 0f, 0f };
        [JsonProperty("localScale")] public List<float> LocalScale = new List<float> { 1f, 1f, 1f };
        [JsonProperty("startOffsetSeconds")] public float StartOffsetSeconds;
        [JsonProperty("playbackSpeed")] public float PlaybackSpeed = 1f;
        [JsonProperty("emissionScale")] public float EmissionScale = 1f;
        [JsonProperty("lifetimeScale")] public float LifetimeScale = 1f;
        [JsonProperty("sizeScale")] public float SizeScale = 1f;
        [JsonProperty("colorTint")] public List<float> ColorTint = new List<float> { 1f, 1f, 1f, 1f };
        [JsonProperty("sortingOrderOffset")] public int SortingOrderOffset;
        [JsonProperty("source")] public VfxLayerSourceReferenceView Source = new VfxLayerSourceReferenceView();
        [JsonProperty("profileDependencies")] public List<VfxProfileDependencyView> ProfileDependencies = new List<VfxProfileDependencyView>();
        [JsonProperty("startFrame")] public int StartFrame;
        [JsonProperty("estimatedSourceFrames")] public int EstimatedSourceFrames;
        [JsonProperty("transform")] public VfxLayerTransformView Transform = new VfxLayerTransformView();
        [JsonProperty("timing")] public VfxLayerTimingView Timing = new VfxLayerTimingView();
        [JsonProperty("visualStart", NullValueHandling = NullValueHandling.Ignore)]
        public VfxLayerVisualStartView VisualStart;
        [JsonProperty("semanticOverrides")] public List<VfxSemanticOverrideView> SemanticOverrides = new List<VfxSemanticOverrideView>();
        [JsonProperty("memberParticipation")] public List<VfxMemberParticipationView> MemberParticipation = new List<VfxMemberParticipationView>();
        [JsonProperty("memberOverrides")] public List<VfxMemberSemanticOverrideView> MemberOverrides = new List<VfxMemberSemanticOverrideView>();

        // Read-only compatibility surface for v2 migration. v3 writes only Transform, StartFrame and semantic overrides.
        public bool ShouldSerializeLocalPosition() => false;
        public bool ShouldSerializeLocalEulerAngles() => false;
        public bool ShouldSerializeLocalScale() => false;
        public bool ShouldSerializeStartOffsetSeconds() => false;
        public bool ShouldSerializePlaybackSpeed() => false;
        public bool ShouldSerializeEmissionScale() => false;
        public bool ShouldSerializeLifetimeScale() => false;
        public bool ShouldSerializeSizeScale() => false;
        public bool ShouldSerializeColorTint() => false;
        public bool ShouldSerializeSortingOrderOffset() => false;
    }

    [Serializable]
    internal sealed class VfxCompositionBakeReceipt
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.composition-bake-receipt.v3";
        [JsonProperty("draftId")] public string DraftId = string.Empty;
        [JsonProperty("draftHash")] public string DraftHash = string.Empty;
        [JsonProperty("compositionContentHash")] public string CompositionContentHash = string.Empty;
        [JsonProperty("consumerResourceKey")] public string ConsumerResourceKey = string.Empty;
        [JsonProperty("consumerContractHash")] public string ConsumerContractHash = string.Empty;
        [JsonProperty("sourceIndexHash")] public string SourceIndexHash = string.Empty;
        [JsonProperty("curationHash")] public string CurationHash = string.Empty;
        [JsonProperty("outputPrefabPath")] public string OutputPrefabPath = string.Empty;
        [JsonProperty("outputPrefabGuid")] public string OutputPrefabGuid = string.Empty;
        [JsonProperty("outputDependencyHash")] public string OutputDependencyHash = string.Empty;
        [JsonProperty("sourceElementIds")] public List<string> SourceElementIds = new List<string>();
        [JsonProperty("directSupportAssetGuids")] public List<string> DirectSupportAssetGuids = new List<string>();
        [JsonProperty("loopPlaybackMode")] public string LoopPlaybackMode = "OneShot";
        [JsonProperty("phases")] public List<VfxCompositionBakedPhaseView> Phases = new List<VfxCompositionBakedPhaseView>();
        [JsonProperty("unitySerializationUsed")] public bool UnitySerializationUsed = true;
        [JsonProperty("receiptHash")] public string ReceiptHash = string.Empty;
        [JsonProperty("propertyRegistryHash")] public string PropertyRegistryHash = string.Empty;
        [JsonProperty("profileManifestHash")] public string ProfileManifestHash = string.Empty;
        [JsonProperty("profileDependencyHash")] public string ProfileDependencyHash = string.Empty;
        [JsonProperty("previewEvidenceHash")] public string PreviewEvidenceHash = string.Empty;
        [JsonProperty("previewCompileHash")] public string PreviewCompileHash = string.Empty;
        [JsonProperty("bakeCompileHash")] public string BakeCompileHash = string.Empty;
        [JsonProperty("fieldDiffHash")] public string FieldDiffHash = string.Empty;
        [JsonProperty("previewBakeEquivalent")] public bool PreviewBakeEquivalent;
        [JsonProperty("sourceIntegrityHashBefore")] public string SourceIntegrityHashBefore = string.Empty;
        [JsonProperty("sourceIntegrityHashAfter")] public string SourceIntegrityHashAfter = string.Empty;
        [JsonProperty("sourceAssetsUnchanged")] public bool SourceAssetsUnchanged;
        [JsonProperty("sourceAssets")] public List<VfxSourceAssetIntegrityView> SourceAssets = new List<VfxSourceAssetIntegrityView>();
        [JsonProperty("candidateContentHash")] public string CandidateContentHash = string.Empty;
        [JsonProperty("compilerVersion")] public string CompilerVersion = "mx.vfx.semantic-compiler/1";
        [JsonProperty("adapterVersion")] public string AdapterVersion = "mx.vfx.particlesystem-adapter/1";
        [JsonProperty("unityVersion")] public string UnityVersion = string.Empty;
        [JsonProperty("validationLevel")] public string ValidationLevel = nameof(VfxSemanticValidationLevel.Structural);
        [JsonProperty("experimental")] public bool Experimental;
        [JsonProperty("compiledLayers")] public List<VfxCompositionBakedLayerView> CompiledLayers = new List<VfxCompositionBakedLayerView>();
        [JsonProperty("diagnostics")] public List<VfxSemanticDiagnosticView> Diagnostics = new List<VfxSemanticDiagnosticView>();
    }

    [Serializable]
    internal sealed class VfxSourceAssetIntegrityView
    {
        [JsonProperty("assetGuid")] public string AssetGuid = string.Empty;
        [JsonProperty("assetPath")] public string AssetPath = string.Empty;
        [JsonProperty("indexedContentHash")] public string IndexedContentHash = string.Empty;
        [JsonProperty("indexedDependencyHash")] public string IndexedDependencyHash = string.Empty;
        [JsonProperty("currentFileHash")] public string CurrentFileHash = string.Empty;
        [JsonProperty("currentDependencyHash")] public string CurrentDependencyHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxCompositionBakedLayerView
    {
        [JsonProperty("layerId")] public string LayerId = string.Empty;
        [JsonProperty("phase")] public string Phase = string.Empty;
        [JsonProperty("startFrame")] public int StartFrame;
        [JsonProperty("artisticEndFrame")] public int ArtisticEndFrame;
        [JsonProperty("technicalEndFrame")] public int TechnicalEndFrame;
        [JsonProperty("sourceElementId")] public string SourceElementId = string.Empty;
        [JsonProperty("profileDependencies")] public List<VfxProfileDependencyView> ProfileDependencies = new List<VfxProfileDependencyView>();
        [JsonProperty("fieldDiffs")] public List<VfxUnityFieldDiffView> FieldDiffs = new List<VfxUnityFieldDiffView>();
        [JsonProperty("compileHash")] public string CompileHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxCompositionBakedPhaseView
    {
        [JsonProperty("phase")] public string Phase = string.Empty;
        [JsonProperty("durationFrames")] public int DurationFrames;
        [JsonProperty("layerIds")] public List<string> LayerIds = new List<string>();
    }

    internal sealed class VfxCompositionValidationResult
    {
        public bool Valid;
        public readonly List<string> Diagnostics = new List<string>();
    }

    /// <summary>Persists versionable manual VFX composition drafts.</summary>
    /// <remarks>Editor-only, AllocByDesign. Drafts are custom JSON and never enter Runtime authority.</remarks>
    internal sealed class VfxCompositionDraftService
    {
        internal const string DraftRoot = "Assets/Config/MxFramework/Vfx/Compositions";
        internal const string CatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        internal static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        public IReadOnlyList<string> ListDrafts()
        {
            if (!Directory.Exists(DraftRoot)) return Array.Empty<string>();
            return Directory.GetFiles(DraftRoot, "*.json", SearchOption.TopDirectoryOnly).Select(value => value.Replace('\\', '/')).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        public VfxCompositionDraftView Create(VfxSourceElementCatalog catalog, string draftId, string displayName)
        {
            VfxSemanticPropertyRegistryView registry = new VfxSemanticPropertyRegistryService().Load(out _);
            VfxProfileManifestView manifest = new VfxSemanticProfileStore().LoadManifest(out _);
            var result = new VfxCompositionDraftView
            {
                DraftId = NormalizeId(draftId), DisplayName = displayName,
                SourceIndexHash = catalog?.Index?.IndexHash ?? string.Empty,
                CurationHash = catalog?.Curation?.CurationHash ?? string.Empty,
                CatalogHash = HashFile(CatalogPath)
            };
            result.Dependencies = new VfxDraftDependencySetView
            {
                CatalogHash = result.CatalogHash,
                SourceIndexHash = result.SourceIndexHash,
                CurationHash = result.CurationHash,
                PropertyRegistryHash = registry?.RegistryHash ?? string.Empty,
                ProfileManifestHash = manifest?.ManifestHash ?? string.Empty
            };
            if (catalog?.LocalElements.Count > 0)
                result.LocalElements = catalog.LocalElements.ToList();
            return result;
        }

        public VfxCompositionDraftView CreateWithUniqueId(VfxSourceElementCatalog catalog, string displayName)
        {
            string draftId;
            string path;
            do
            {
                draftId = Guid.NewGuid().ToString("D");
                path = DraftRoot + "/" + FileToken(draftId) + ".json";
            }
            while (File.Exists(path));
            return Create(catalog, draftId, displayName);
        }

        public bool Save(VfxCompositionDraftView draft, out string path, out string message)
        {
            return SaveGuarded(draft, string.Empty, string.Empty, out path, out _, out message);
        }

        public bool IsOwnedDraftPath(string path)
        {
            string normalized = NormalizeProjectPath(path);
            string directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            return string.Equals(directory, DraftRoot, StringComparison.OrdinalIgnoreCase)
                && normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        }

        public bool DeleteGuarded(
            string path,
            string expectedDraftId,
            string expectedFileHash,
            out string message)
        {
            string normalized = NormalizeProjectPath(path);
            if (!IsOwnedDraftPath(normalized))
            {
                message = "只能删除项目 Compositions 目录中的草稿；外部导入文件不会被工作台删除。";
                return false;
            }
            if (!File.Exists(normalized))
            {
                message = "草稿文件已不存在：" + normalized;
                return false;
            }
            if (string.IsNullOrWhiteSpace(expectedDraftId) || string.IsNullOrWhiteSpace(expectedFileHash))
            {
                message = "缺少 Draft ID 或文件哈希，已拒绝删除。";
                return false;
            }

            VfxCompositionDraftView diskDraft = Load(normalized, out string loadMessage);
            if (diskDraft == null)
            {
                message = "无法验证待删除草稿：" + loadMessage;
                return false;
            }
            if (!string.Equals(diskDraft.DraftId, expectedDraftId, StringComparison.Ordinal))
            {
                message = "磁盘 Draft ID 已变化，已拒绝删除。";
                return false;
            }
            if (!string.Equals(HashFile(normalized), expectedFileHash, StringComparison.Ordinal))
            {
                message = "磁盘草稿已在当前会话之外发生变化，已拒绝删除；请重新打开后确认。";
                return false;
            }
            if (!AssetDatabase.DeleteAsset(normalized))
            {
                message = "Unity 未能删除草稿资产：" + normalized;
                return false;
            }

            new VfxCompositionRecoveryJournalService().Clear(expectedDraftId);
            message = "草稿与本机恢复日志已删除；Candidate、Prefab 和已发布资源均已保留。";
            return true;
        }

        public bool SaveGuarded(
            VfxCompositionDraftView draft,
            string loadedPath,
            string expectedFileHash,
            out string path,
            out string savedFileHash,
            out string message)
        {
            path = string.Empty;
            savedFileHash = string.Empty;
            if (draft == null || string.IsNullOrWhiteSpace(draft.DraftId)) { message = "Draft ID 不能为空。"; return false; }
            EnsureFolder(DraftRoot);
            draft.SchemaVersion = VfxCompositionDraftView.CurrentSchemaVersion;
            NormalizePreviewContext(draft);
            NormalizeV3(draft, forSave: true);
            draft.DraftId = NormalizeId(draft.DraftId);
            draft.Revision = Math.Max(1, draft.Revision);
            path = DraftRoot + "/" + FileToken(draft.DraftId) + ".json";
            string normalizedLoadedPath = (loadedPath ?? string.Empty).Replace('\\', '/');
            if (File.Exists(path))
            {
                if (string.IsNullOrWhiteSpace(normalizedLoadedPath)
                    || !string.Equals(normalizedLoadedPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    message = "目标草稿已存在；请打开现有草稿。系统生成的 Draft ID 不允许手动修改：" + path;
                    return false;
                }
                string actual = HashFile(path);
                if (string.IsNullOrWhiteSpace(expectedFileHash) || !string.Equals(expectedFileHash, actual, StringComparison.Ordinal))
                {
                    message = "磁盘草稿已变化；请执行 base / disk / session 三方合并后再保存。";
                    return false;
                }
                draft.Revision++;
            }
            string json = JsonConvert.SerializeObject(draft, Formatting.Indented) + Environment.NewLine;
            string temporary = path + ".tmp";
            string backup = path + ".bak";
            try
            {
                if (File.Exists(path)) File.Copy(path, backup, true);
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                VfxCompositionDraftView roundtrip = JsonConvert.DeserializeObject<VfxCompositionDraftView>(File.ReadAllText(temporary), JsonSettings);
                if (roundtrip == null || !string.Equals(roundtrip.DraftId, draft.DraftId, StringComparison.Ordinal))
                    throw new InvalidDataException("临时草稿 roundtrip 失败。");
                File.Copy(temporary, path, true);
                savedFileHash = HashFile(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                message = "草稿已通过 expected-hash 事务保存：" + path;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException)
            {
                if (File.Exists(backup)) File.Copy(backup, path, true);
                else if (File.Exists(path)) File.Delete(path);
                message = "草稿保存失败，已恢复原始字节：" + exception.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { }
                try { if (File.Exists(backup)) File.Delete(backup); } catch (IOException) { }
            }
        }

        private static string NormalizeProjectPath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/');
            if (!Path.IsPathRooted(normalized)) return normalized;
            string projectRelative = FileUtil.GetProjectRelativePath(normalized);
            return (projectRelative ?? string.Empty).Replace('\\', '/');
        }

        public bool CanSaveSemanticEdits(VfxCompositionDraftView draft, VfxSourceElementCatalog catalog, out string message)
        {
            message = string.Empty;
            if (draft?.Layers == null || !draft.Layers.Any(HasSemanticIntent)) return true;
            List<string> diagnostics = ValidateDependencyGuards(draft, catalog);
            if (diagnostics.Count == 0) return true;
            message = "语义编辑依赖已过期；可继续查看源效果，但必须完成重映射后才能保存语义覆盖：\n" + string.Join("\n", diagnostics);
            return false;
        }

        public VfxCompositionDraftView Load(string path, out string message)
        {
            try
            {
                VfxCompositionDraftView result = JsonConvert.DeserializeObject<VfxCompositionDraftView>(File.ReadAllText(path), JsonSettings);
                NormalizePreviewContext(result);
                if (result != null && result.SchemaVersion == VfxCompositionDraftView.CurrentSchemaVersion)
                    NormalizeV3(result, forSave: false);
                message = result == null ? "草稿内容为空。" : "草稿已加载。";
                return result;
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                message = "草稿读取失败：" + exception.Message; return null;
            }
        }

        public VfxCompositionValidationResult Validate(VfxCompositionDraftView draft, VfxSourceElementCatalog catalog)
        {
            var result = new VfxCompositionValidationResult();
            if (draft == null) { result.Diagnostics.Add("草稿不存在。"); return result; }
            catalog?.SetLocalElements(draft.LocalElements);
            if (draft.SchemaVersion != VfxCompositionDraftView.CurrentSchemaVersion)
                result.Diagnostics.Add("不支持的草稿 schema；v1 草稿必须重新创建，不能烘焙。");
            NormalizeV3(draft, forSave: false);
            if (draft.LoopPlaybackMode != "OneShot" && draft.LoopPlaybackMode != "Continuous")
                result.Diagnostics.Add("loopPlaybackMode 必须为 OneShot 或 Continuous。");
            NormalizePreviewContext(draft);
            ValidatePreviewContext(result.Diagnostics, "共享默认", draft.PreviewContext);
            foreach (string phase in new[] { "Build", "Rebuild", "Loop", "Complete", "Dissipate" })
                ValidatePreviewContext(result.Diagnostics, phase, ResolvePreviewContext(draft.PreviewContext, phase));
            VfxCompositionPhaseDurationOverridesView timings = draft.PhaseDurationOverrides ?? new VfxCompositionPhaseDurationOverridesView();
            if (InvalidPhaseDuration(timings.BuildFrames) || InvalidPhaseDuration(timings.RebuildFrames) || InvalidPhaseDuration(timings.LoopFrames) || InvalidPhaseDuration(timings.CompleteFrames) || InvalidPhaseDuration(timings.DissipateFrames))
                result.Diagnostics.Add("阶段帧数覆盖必须为空（自动）或非负帧数。");
            if (catalog == null || !catalog.Available) result.Diagnostics.Add("Element 素材目录不可用。" );
            else
            {
                ValidateLocalElements(result.Diagnostics, draft, catalog);
                var layerIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < draft.Layers.Count; i++)
                {
                    VfxCompositionLayerView layer = draft.Layers[i];
                    if (!layerIds.Add(layer.LayerId)) result.Diagnostics.Add("Layer ID 重复：" + layer.LayerId);
                    if (!IsPhase(layer.Phase)) result.Diagnostics.Add("Layer 生命周期阶段无效：" + layer.Phase);
                    VfxSourceElementView element = catalog.Find(layer.ElementId);
                    if (layer.StartFrame < 0) result.Diagnostics.Add("Layer 起始帧不能为负数：" + layer.LayerId);
                    if (layer.VisualStart != null)
                    {
                        if (!Enum.TryParse(layer.VisualStart.Mode, false, out VfxVisualStartMode _))
                            result.Diagnostics.Add("Layer 视觉起点模式无效：" + layer.LayerId);
                        if (layer.VisualStart.ManualLeadInFrames < 0)
                            result.Diagnostics.Add("Layer 手动前导帧不能为负数：" + layer.LayerId);
                    }
                    if (layer.Timing == null || layer.Timing.TargetArtisticFrames < 0) result.Diagnostics.Add("Layer 时间合同无效：" + layer.LayerId);
                    if (HasNonDefaultLegacyMultipliers(layer)) result.Diagnostics.Add("Layer 仍包含 v2 粗倍率；必须人工转换为 v3 语义覆盖：" + layer.LayerId);
                    if (element == null) result.Diagnostics.Add("Element 不存在：" + layer.ElementId);
                    else if (element.Stale) result.Diagnostics.Add("Element 已过期：" + layer.ElementId);
                    if (layer.StartOffsetSeconds < 0f || layer.PlaybackSpeed <= 0f || layer.EmissionScale < 0f || layer.LifetimeScale <= 0f || layer.SizeScale <= 0f) result.Diagnostics.Add("Layer 参数超出允许范围：" + layer.LayerId);
                }
            }
            result.Diagnostics.AddRange(ValidateDependencyGuards(draft, catalog));
            result.Valid = result.Diagnostics.Count == 0 && draft.Layers.Count > 0;
            if (draft.Layers.Count == 0) result.Diagnostics.Add("草稿尚未包含任何 Layer。" );
            if (!draft.Layers.Any(value => value.Enabled && value.Phase == "Loop"))
            {
                result.Valid = false;
                result.Diagnostics.Add("Loop 阶段至少需要一个启用的 Layer。");
            }
            return result;
        }

        private static List<string> ValidateDependencyGuards(VfxCompositionDraftView draft, VfxSourceElementCatalog catalog)
        {
            var result = new List<string>();
            if (draft == null) return result;
            VfxDraftDependencySetView dependencies = draft.Dependencies ?? new VfxDraftDependencySetView();
            VfxSemanticPropertyRegistryView registry = new VfxSemanticPropertyRegistryService().Load(out string registryMessage);
            if (registry == null) result.Add("Property Registry 不可用：" + registryMessage);
            else if (!string.IsNullOrWhiteSpace(dependencies.PropertyRegistryHash)
                && !string.Equals(dependencies.PropertyRegistryHash, registry.RegistryHash, StringComparison.Ordinal))
                result.Add("Property Registry 已变化，草稿需要重映射。" );

            VfxProfileManifestView manifest = new VfxSemanticProfileStore().LoadManifest(out string manifestMessage);
            if (manifest == null) result.Add("Profile Manifest 不可用：" + manifestMessage);
            else
            {
                for (int layerIndex = 0; layerIndex < draft.Layers.Count; layerIndex++)
                {
                    VfxCompositionLayerView layer = draft.Layers[layerIndex];
                    for (int dependencyIndex = 0; dependencyIndex < layer.ProfileDependencies.Count; dependencyIndex++)
                    {
                        VfxProfileDependencyView dependency = layer.ProfileDependencies[dependencyIndex];
                        VfxProfileManifestEntryView entry = manifest.Profiles.FirstOrDefault(value => value.ProfileId == dependency.ProfileId);
                        if (entry == null)
                            result.Add("Profile 不存在：" + dependency.ProfileId + "（" + layer.LayerId + "）");
                        else if (!string.Equals(entry.ContentHash, dependency.Hash, StringComparison.Ordinal))
                            result.Add("Profile 已变化：" + dependency.ProfileId + "（" + layer.LayerId + "）");
                    }
                }
            }
            if (catalog != null && catalog.Available)
            {
                if (!string.Equals(dependencies.SourceIndexHash, catalog.Index.IndexHash, StringComparison.Ordinal))
                    result.Add("Source Index 已变化，草稿需要重映射。" );
                if (!string.Equals(dependencies.CurationHash, catalog.Curation.CurationHash, StringComparison.Ordinal))
                    result.Add("Element 策展目录已变化，草稿需要重映射。" );
            }
            return result.Distinct(StringComparer.Ordinal).ToList();
        }

        internal static bool HasSemanticIntent(VfxCompositionLayerView layer)
        {
            if (layer == null) return false;
            return layer.SemanticOverrides?.Count > 0
                || layer.MemberOverrides?.Any(value => value.Overrides?.Count > 0 || !string.IsNullOrWhiteSpace(value.RetimeMode)) == true
                || layer.MemberParticipation?.Count > 0
                || (layer.Timing?.SourceSegments != null
                    && layer.Timing.TargetArtisticFrames > 0
                    && layer.Timing.TargetArtisticFrames != layer.Timing.SourceSegments.ArtisticFrames);
        }

        internal static bool IsPhase(string phase)
        {
            return phase == "Build" || phase == "Rebuild" || phase == "Loop" || phase == "Complete" || phase == "Dissipate";
        }

        internal static string HashDraft(VfxCompositionDraftView draft)
        {
            return VfxCanonicalJson.Hash(draft);
        }

        internal static string HashCompositionContent(VfxCompositionDraftView draft)
        {
            if (draft == null) return string.Empty;
            return VfxCanonicalJson.Hash(draft, "previewContext");
        }

        internal static void NormalizeV3(VfxCompositionDraftView draft, bool forSave)
        {
            if (draft == null) return;
            draft.FramesPerSecond = 60;
            draft.Dependencies ??= new VfxDraftDependencySetView();
            if (forSave)
            {
                draft.Dependencies.CatalogHash = draft.CatalogHash;
                draft.Dependencies.SourceIndexHash = draft.SourceIndexHash;
                draft.Dependencies.CurationHash = draft.CurationHash;
                VfxSemanticPropertyRegistryView registry = new VfxSemanticPropertyRegistryService().Load(out _);
                VfxProfileManifestView manifest = new VfxSemanticProfileStore().LoadManifest(out _);
                draft.Dependencies.PropertyRegistryHash = registry?.RegistryHash ?? draft.Dependencies.PropertyRegistryHash;
                draft.Dependencies.ProfileManifestHash = manifest?.ManifestHash ?? draft.Dependencies.ProfileManifestHash;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(draft.Dependencies.CatalogHash)) draft.CatalogHash = draft.Dependencies.CatalogHash;
                if (!string.IsNullOrWhiteSpace(draft.Dependencies.SourceIndexHash)) draft.SourceIndexHash = draft.Dependencies.SourceIndexHash;
                if (!string.IsNullOrWhiteSpace(draft.Dependencies.CurationHash)) draft.CurationHash = draft.Dependencies.CurationHash;
            }
            draft.PhaseDurationOverrides ??= new VfxCompositionPhaseDurationOverridesView();
            draft.LocalElements ??= new List<VfxSourceElementView>();
            draft.Layers ??= new List<VfxCompositionLayerView>();
            for (int i = 0; i < draft.Layers.Count; i++)
            {
                VfxCompositionLayerView layer = draft.Layers[i];
                bool missingTransform = layer.Transform == null;
                layer.Source ??= new VfxLayerSourceReferenceView();
                layer.Transform ??= new VfxLayerTransformView();
                layer.Timing ??= new VfxLayerTimingView();
                layer.ProfileDependencies ??= new List<VfxProfileDependencyView>();
                layer.SemanticOverrides ??= new List<VfxSemanticOverrideView>();
                layer.MemberParticipation ??= new List<VfxMemberParticipationView>();
                layer.MemberOverrides ??= new List<VfxMemberSemanticOverrideView>();
                if (forSave)
                {
                    layer.Source.Kind = string.IsNullOrWhiteSpace(layer.PrimitiveId) ? "Element" : "Primitive";
                    layer.Source.StableId = string.IsNullOrWhiteSpace(layer.PrimitiveId) ? layer.ElementId : layer.PrimitiveId;
                    if (missingTransform)
                    {
                        layer.Transform.Position = CopyVector(layer.LocalPosition, Vector3.zero);
                        layer.Transform.RotationEuler = CopyVector(layer.LocalEulerAngles, Vector3.zero);
                        layer.Transform.Scale = CopyVector(layer.LocalScale, Vector3.one);
                    }
                    layer.LocalPosition = CopyVector(layer.Transform.Position, Vector3.zero);
                    layer.LocalEulerAngles = CopyVector(layer.Transform.RotationEuler, Vector3.zero);
                    layer.LocalScale = CopyVector(layer.Transform.Scale, Vector3.one);
                }
                else
                {
                    if (layer.Source.Kind == "Element" && !string.IsNullOrWhiteSpace(layer.Source.StableId)) layer.ElementId = layer.Source.StableId;
                    if (layer.Source.Kind == "Primitive" && !string.IsNullOrWhiteSpace(layer.Source.StableId)) layer.PrimitiveId = layer.Source.StableId;
                    layer.LocalPosition = CopyVector(layer.Transform.Position, Vector3.zero);
                    layer.LocalEulerAngles = CopyVector(layer.Transform.RotationEuler, Vector3.zero);
                    layer.LocalScale = CopyVector(layer.Transform.Scale, Vector3.one);
                }
            }
        }

        internal static bool HasNonDefaultLegacyMultipliers(VfxCompositionLayerView layer)
        {
            if (layer == null) return false;
            return Math.Abs(layer.PlaybackSpeed - 1f) > 0.00001f
                || Math.Abs(layer.EmissionScale - 1f) > 0.00001f
                || Math.Abs(layer.LifetimeScale - 1f) > 0.00001f
                || Math.Abs(layer.SizeScale - 1f) > 0.00001f
                || layer.SortingOrderOffset != 0
                || !IsWhite(layer.ColorTint);
        }

        private static bool InvalidPhaseDuration(int? frames) => frames.HasValue && frames.Value < 0;

        private static bool IsWhite(List<float> value)
        {
            return value == null || value.Count != 4
                || value.All(component => Math.Abs(component - 1f) <= 0.00001f);
        }

        private static List<float> CopyVector(List<float> value, Vector3 fallback)
        {
            return value != null && value.Count == 3
                ? new List<float> { value[0], value[1], value[2] }
                : new List<float> { fallback.x, fallback.y, fallback.z };
        }

        internal static void NormalizePreviewContext(VfxCompositionDraftView draft)
        {
            if (draft == null) return;
            draft.PreviewContext ??= new VfxCompositionPreviewContextView();
            if (string.IsNullOrWhiteSpace(draft.PreviewContext.Mode)) draft.PreviewContext.Mode = VfxCompositionPreviewContextView.VfxOnlyMode;
            if (string.IsNullOrWhiteSpace(draft.PreviewContext.PosePresetId)) draft.PreviewContext.PosePresetId = "Idle";
            if (draft.PreviewContext.AnimationSpeed <= 0f) draft.PreviewContext.AnimationSpeed = 1f;
            PrunePreviewContextOverrides(draft.PreviewContext);
        }

        internal static VfxCompositionPreviewContextView ResolvePreviewContext(VfxCompositionPreviewContextView shared, string phase)
        {
            shared ??= new VfxCompositionPreviewContextView();
            var result = new VfxCompositionPreviewContextView
            {
                Mode = string.IsNullOrWhiteSpace(shared.Mode) ? VfxCompositionPreviewContextView.VfxOnlyMode : shared.Mode,
                CharacterStableId = shared.CharacterStableId ?? string.Empty,
                PackageId = shared.PackageId ?? string.Empty,
                CharacterEvidenceHash = shared.CharacterEvidenceHash ?? string.Empty,
                PosePresetId = string.IsNullOrWhiteSpace(shared.PosePresetId) ? "Idle" : shared.PosePresetId,
                AnimationResourceKey = shared.AnimationResourceKey ?? string.Empty,
                AnimationLoop = shared.AnimationLoop,
                AnimationSpeed = shared.AnimationSpeed > 0f ? shared.AnimationSpeed : 1f,
                SocketId = shared.SocketId ?? string.Empty
            };
            VfxCompositionPreviewContextOverrideView value = shared.PhaseOverrides?.Get(phase);
            if (value == null) return result;
            if (value.Mode != null) result.Mode = value.Mode;
            if (value.CharacterStableId != null || value.PackageId != null || value.CharacterEvidenceHash != null)
            {
                result.CharacterStableId = value.CharacterStableId ?? string.Empty;
                result.PackageId = value.PackageId ?? string.Empty;
                result.CharacterEvidenceHash = value.CharacterEvidenceHash ?? string.Empty;
            }
            if (value.PosePresetId != null) result.PosePresetId = value.PosePresetId;
            if (value.AnimationResourceKey != null) result.AnimationResourceKey = value.AnimationResourceKey;
            if (value.AnimationLoop.HasValue) result.AnimationLoop = value.AnimationLoop.Value;
            if (value.AnimationSpeed.HasValue) result.AnimationSpeed = value.AnimationSpeed.Value;
            if (value.SocketId != null) result.SocketId = value.SocketId;
            return result;
        }

        internal static VfxCompositionPreviewContextOverrideView EnsurePreviewContextOverride(VfxCompositionPreviewContextView shared, string phase)
        {
            if (shared == null || !IsPhase(phase)) return null;
            shared.PhaseOverrides ??= new VfxCompositionPhasePreviewOverridesView();
            VfxCompositionPreviewContextOverrideView value = shared.PhaseOverrides.Get(phase);
            if (value != null) return value;
            value = new VfxCompositionPreviewContextOverrideView();
            shared.PhaseOverrides.Set(phase, value);
            return value;
        }

        internal static void ClearPreviewContextOverride(VfxCompositionPreviewContextView shared, string phase)
        {
            if (shared?.PhaseOverrides == null || !IsPhase(phase)) return;
            shared.PhaseOverrides.Set(phase, null);
            PrunePreviewContextOverrides(shared);
        }

        internal static void PrunePreviewContextOverrides(VfxCompositionPreviewContextView shared)
        {
            if (shared?.PhaseOverrides == null) return;
            foreach (string phase in new[] { "Build", "Rebuild", "Loop", "Complete", "Dissipate" })
            {
                VfxCompositionPreviewContextOverrideView value = shared.PhaseOverrides.Get(phase);
                if (value?.IsEmpty == true) shared.PhaseOverrides.Set(phase, null);
            }
            if (shared.PhaseOverrides.IsEmpty) shared.PhaseOverrides = null;
        }

        private static void ValidatePreviewContext(List<string> diagnostics, string scope, VfxCompositionPreviewContextView context)
        {
            if (context.Mode != VfxCompositionPreviewContextView.VfxOnlyMode
                && context.Mode != VfxCompositionPreviewContextView.CharacterAttachedMode)
                diagnostics.Add("previewContext." + scope + ".mode 必须为 VfxOnly 或 CharacterAttached。");
            if (context.AnimationSpeed <= 0f)
                diagnostics.Add("previewContext." + scope + " 的角色动画速度必须大于 0。");
            if (context.Mode == VfxCompositionPreviewContextView.CharacterAttachedMode)
            {
                bool anyIdentity = !string.IsNullOrWhiteSpace(context.CharacterStableId)
                    || !string.IsNullOrWhiteSpace(context.PackageId)
                    || !string.IsNullOrWhiteSpace(context.CharacterEvidenceHash);
                bool completeIdentity = !string.IsNullOrWhiteSpace(context.CharacterStableId)
                    && !string.IsNullOrWhiteSpace(context.PackageId)
                    && !string.IsNullOrWhiteSpace(context.CharacterEvidenceHash);
                if (anyIdentity && !completeIdentity)
                    diagnostics.Add("previewContext." + scope + " 的角色 ID、包 ID 与证据哈希必须成组配置。");
            }
        }

        internal static string HashFile(string path)
        {
            if (!File.Exists(path)) return string.Empty;
            using SHA256 sha = SHA256.Create(); using FileStream stream = File.OpenRead(path);
            return "sha256:" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        internal static string NormalizeId(string value)
        {
            string result = new string((value ?? string.Empty).Trim().ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) || character == '.' || character == '_' || character == '-' ? character : '_').ToArray());
            return string.IsNullOrWhiteSpace(result) ? "vfx.composition.untitled" : result;
        }

        internal static string FileToken(string value) => new string((value ?? string.Empty).Select(character => char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_').ToArray());
        internal static void EnsureFolder(string path) => VfxSourceComponentExtractionService.EnsureFolder(path);

        internal static VfxCompositionLayerView CreateLayerView(VfxSourceElementCatalog catalog, VfxSourceElementView element, string layerId, string phase)
        {
            IReadOnlyList<VfxSourceComponentView> members = catalog?.Members(element) ?? Array.Empty<VfxSourceComponentView>();
            int estimated = members.Count == 0
                ? 60
                : Math.Max(1, Mathf.CeilToInt(members.Max(value => Math.Max(0.05f, value.DurationSeconds)) * 60f));
            int entry = Math.Min(12, Math.Max(0, estimated / 6));
            int exit = element?.Lifecycle == "Loop" ? Math.Min(18, Math.Max(0, estimated / 5)) : 0;
            int body = Math.Max(1, estimated - entry - exit);
            var segments = new VfxTimeSegmentsView
            {
                EntryFrames = entry,
                BodyFrames = body,
                ExitFrames = exit,
                CleanupFrames = Math.Min(30, Math.Max(0, estimated / 3)),
                MinimumBodyFrames = 1,
                CycleFrames = element?.Lifecycle == "Loop" ? body : 0
            };
            return new VfxCompositionLayerView
            {
                LayerId = layerId,
                ElementId = element?.ElementId ?? string.Empty,
                PrimitiveId = element?.ExistingPrimitiveId ?? string.Empty,
                Phase = phase,
                Source = new VfxLayerSourceReferenceView
                {
                    Kind = string.IsNullOrWhiteSpace(element?.ExistingPrimitiveId) ? "Element" : "Primitive",
                    StableId = string.IsNullOrWhiteSpace(element?.ExistingPrimitiveId) ? element?.ElementId ?? string.Empty : element.ExistingPrimitiveId
                },
                EstimatedSourceFrames = estimated,
                Timing = new VfxLayerTimingView
                {
                    RetimeMode = element?.Lifecycle == "Loop" ? nameof(VfxRetimeMode.RepeatBody) : nameof(VfxRetimeMode.ExtendBody),
                    SourceSegments = segments,
                    TargetArtisticFrames = segments.ArtisticFrames
                }
            };
        }

        private static void ValidateLocalElements(List<string> diagnostics, VfxCompositionDraftView draft, VfxSourceElementCatalog catalog)
        {
            if (draft?.LocalElements == null || draft.LocalElements.Count == 0) return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < draft.LocalElements.Count; i++)
            {
                VfxSourceElementView element = draft.LocalElements[i];
                if (element == null)
                {
                    diagnostics.Add("草稿私有 Element 为空。");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(element.ElementId)
                    || !element.ElementId.StartsWith("vfx.element.local.", StringComparison.Ordinal))
                    diagnostics.Add("草稿私有 Element ID 必须使用 vfx.element.local.* 命名空间：" + element.ElementId);
                if (!ids.Add(element.ElementId)) diagnostics.Add("草稿私有 Element ID 重复：" + element.ElementId);
                if (catalog.Elements.Any(value => string.Equals(value.ElementId, element.ElementId, StringComparison.Ordinal)))
                    diagnostics.Add("草稿私有 Element 不得覆盖全局 Element：" + element.ElementId);
                if (element.MemberComponentIds == null || element.MemberComponentIds.Count == 0)
                {
                    diagnostics.Add("草稿私有 Element 没有成员：" + element.ElementId);
                    continue;
                }
                if (element.MemberComponentIds.Distinct(StringComparer.Ordinal).Count() != element.MemberComponentIds.Count)
                    diagnostics.Add("草稿私有 Element 包含重复成员：" + element.ElementId);
                for (int memberIndex = 0; memberIndex < element.MemberComponentIds.Count; memberIndex++)
                {
                    string memberId = element.MemberComponentIds[memberIndex];
                    if (!catalog.Components.ContainsKey(memberId))
                        diagnostics.Add("草稿私有 Element 成员已丢失：" + memberId);
                }
                List<string> currentHashes = element.MemberComponentIds
                    .Where(catalog.Components.ContainsKey)
                    .Select(value => catalog.Components[value].SourceContentHash)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList();
                List<string> expectedHashes = (element.SourceContentHashes ?? new List<string>())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList();
                if (!currentHashes.SequenceEqual(expectedHashes))
                    diagnostics.Add("草稿私有 Element 来源已变化：" + element.ElementId);
            }
        }
    }

    [Serializable]
    internal sealed class VfxCompositionRecoveryEnvelopeView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.composition-recovery.v1";
        [JsonProperty("draftId")] public string DraftId = string.Empty;
        [JsonProperty("diskPath")] public string DiskPath = string.Empty;
        [JsonProperty("baseFileHash")] public string BaseFileHash = string.Empty;
        [JsonProperty("sessionDraftHash")] public string SessionDraftHash = string.Empty;
        [JsonProperty("writtenUtc")] public string WrittenUtc = string.Empty;
        [JsonProperty("baseDraft", NullValueHandling = NullValueHandling.Ignore)] public VfxCompositionDraftView BaseDraft;
        [JsonProperty("draft")] public VfxCompositionDraftView Draft;
    }

    internal sealed class VfxCompositionRecoveryJournalService
    {
        internal const string Root = "Library/MxFramework/VfxSemantic/Recovery";
        internal const string LegacyRoot = "Temp/MxFramework/VfxSemantic/Recovery";
        private readonly Action<string> _createDirectory;
        private readonly Action<string, string, Encoding> _writeAllText;
        private readonly Action<string, string, bool> _copyFile;

        public VfxCompositionRecoveryJournalService()
            : this(
                path => Directory.CreateDirectory(path),
                (path, contents, encoding) => File.WriteAllText(path, contents, encoding),
                (source, destination, overwrite) => File.Copy(source, destination, overwrite))
        {
        }

        internal VfxCompositionRecoveryJournalService(
            Action<string> createDirectory,
            Action<string, string, Encoding> writeAllText,
            Action<string, string, bool> copyFile)
        {
            _createDirectory = createDirectory ?? throw new ArgumentNullException(nameof(createDirectory));
            _writeAllText = writeAllText ?? throw new ArgumentNullException(nameof(writeAllText));
            _copyFile = copyFile ?? throw new ArgumentNullException(nameof(copyFile));
        }

        public string PathFor(string draftId)
        {
            return Root + "/" + VfxCompositionDraftService.FileToken(draftId) + ".recovery.json";
        }

        private static string LegacyPathFor(string draftId)
        {
            return LegacyRoot + "/" + VfxCompositionDraftService.FileToken(draftId) + ".recovery.json";
        }

        public bool Write(VfxCompositionDraftView draft, string diskPath, string baseFileHash, out string message)
        {
            if (draft == null)
            {
                message = "没有可恢复的草稿。";
                return false;
            }
            var envelope = new VfxCompositionRecoveryEnvelopeView
            {
                DraftId = draft.DraftId,
                DiskPath = (diskPath ?? string.Empty).Replace('\\', '/'),
                BaseFileHash = baseFileHash ?? string.Empty,
                SessionDraftHash = VfxCompositionDraftService.HashDraft(draft),
                WrittenUtc = DateTime.UtcNow.ToString("O"),
                Draft = JsonConvert.DeserializeObject<VfxCompositionDraftView>(JsonConvert.SerializeObject(draft), VfxCompositionDraftService.JsonSettings)
            };
            if (!string.IsNullOrWhiteSpace(envelope.DiskPath)
                && File.Exists(envelope.DiskPath)
                && string.Equals(VfxCompositionDraftService.HashFile(envelope.DiskPath), envelope.BaseFileHash, StringComparison.Ordinal))
            {
                try
                {
                    envelope.BaseDraft = JsonConvert.DeserializeObject<VfxCompositionDraftView>(File.ReadAllText(envelope.DiskPath), VfxCompositionDraftService.JsonSettings);
                }
                catch (Exception exception) when (exception is IOException || exception is JsonException)
                {
                    message = "恢复日志无法捕获 base 草稿：" + exception.Message;
                    return false;
                }
            }
            string path = PathFor(draft.DraftId);
            string temporary = path + ".tmp";
            try
            {
                _createDirectory(Root);
                _writeAllText(temporary, JsonConvert.SerializeObject(envelope, Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));
                _copyFile(temporary, path, true);
                message = "已写入未保存恢复日志。";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException)
            {
                message = "恢复日志写入失败：" + exception.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { }
            }
        }

        public VfxCompositionRecoveryEnvelopeView Load(string draftId, out string message)
        {
            string path = PathFor(draftId);
            if (!File.Exists(path) && File.Exists(LegacyPathFor(draftId)))
            {
                try
                {
                    Directory.CreateDirectory(Root);
                    File.Copy(LegacyPathFor(draftId), path, true);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    message = "恢复日志迁移失败：" + exception.Message;
                    return null;
                }
            }
            if (!File.Exists(path))
            {
                message = "没有恢复日志。";
                return null;
            }
            try
            {
                VfxCompositionRecoveryEnvelopeView result = JsonConvert.DeserializeObject<VfxCompositionRecoveryEnvelopeView>(File.ReadAllText(path), VfxCompositionDraftService.JsonSettings);
                if (result?.Draft == null || !string.Equals(result.SessionDraftHash, VfxCompositionDraftService.HashDraft(result.Draft), StringComparison.Ordinal))
                {
                    message = "恢复日志 hash 不匹配。";
                    return null;
                }
                message = "发现未保存恢复日志。";
                return result;
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                message = "恢复日志读取失败：" + exception.Message;
                return null;
            }
        }

        public void Clear(string draftId)
        {
            string path = PathFor(draftId);
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
            string legacyPath = LegacyPathFor(draftId);
            try { if (File.Exists(legacyPath)) File.Delete(legacyPath); } catch (IOException) { }
        }
    }

    internal sealed class VfxCompositionThreeWayMergeResult
    {
        public VfxCompositionDraftView Merged;
        public readonly List<string> Conflicts = new List<string>();
        public bool CanApply => Merged != null && Conflicts.Count == 0;
    }

    internal static class VfxCompositionThreeWayMergeService
    {
        public static VfxCompositionThreeWayMergeResult Merge(
            VfxCompositionDraftView @base,
            VfxCompositionDraftView disk,
            VfxCompositionDraftView session)
        {
            var result = new VfxCompositionThreeWayMergeResult();
            if (@base == null || disk == null || session == null)
            {
                result.Conflicts.Add("base、disk、session 均为必需。" );
                return result;
            }
            JToken merged = MergeToken(JToken.FromObject(@base), JToken.FromObject(disk), JToken.FromObject(session), "$", result.Conflicts);
            if (result.Conflicts.Count == 0)
                result.Merged = merged.ToObject<VfxCompositionDraftView>(JsonSerializer.Create(VfxCompositionDraftService.JsonSettings));
            return result;
        }

        private static JToken MergeToken(JToken @base, JToken disk, JToken session, string path, List<string> conflicts)
        {
            if (JToken.DeepEquals(disk, session)) return disk.DeepClone();
            if (JToken.DeepEquals(@base, session)) return disk.DeepClone();
            if (JToken.DeepEquals(@base, disk)) return session.DeepClone();
            if (@base?.Type == JTokenType.Object && disk?.Type == JTokenType.Object && session?.Type == JTokenType.Object)
            {
                var output = new JObject();
                var names = new HashSet<string>(((JObject)@base).Properties().Select(value => value.Name), StringComparer.Ordinal);
                names.UnionWith(((JObject)disk).Properties().Select(value => value.Name));
                names.UnionWith(((JObject)session).Properties().Select(value => value.Name));
                foreach (string name in names.OrderBy(value => value, StringComparer.Ordinal))
                    output[name] = MergeToken(@base[name], disk[name], session[name], path + "." + name, conflicts);
                return output;
            }
            conflicts.Add(path);
            return session?.DeepClone() ?? JValue.CreateNull();
        }
    }

    internal sealed class VfxSemanticStaleRemapLayerView
    {
        public string LayerId = string.Empty;
        public string ElementId = string.Empty;
        public readonly Dictionary<string, string> MemberMappings = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly List<string> UnresolvedMembers = new List<string>();
        public readonly List<string> StaleProfiles = new List<string>();
    }

    internal sealed class VfxSemanticStaleRemapPlanView
    {
        public string SourceIndexHash = string.Empty;
        public string CurationHash = string.Empty;
        public string PropertyRegistryHash = string.Empty;
        public string ProfileManifestHash = string.Empty;
        public readonly List<VfxSemanticStaleRemapLayerView> Layers = new List<VfxSemanticStaleRemapLayerView>();
        public readonly List<string> Diagnostics = new List<string>();
        public bool CanApplyExact => Diagnostics.Count == 0 && Layers.All(value => value.UnresolvedMembers.Count == 0);
        public int DependencyChangeCount;
        public int ChangedMemberMappingCount => Layers.Sum(value => value.MemberMappings.Count(pair => !string.Equals(pair.Key, pair.Value, StringComparison.Ordinal)));
        public int StaleProfileCount => Layers.Sum(value => value.StaleProfiles.Count);
        public bool HasChanges => DependencyChangeCount > 0 || ChangedMemberMappingCount > 0 || StaleProfileCount > 0;
        public bool RequiresConfirmation => ChangedMemberMappingCount > 0 || StaleProfileCount > 0;
        public bool IsSafeMetadataOnly => HasChanges && CanApplyExact && !RequiresConfirmation;

        public string Summary
        {
            get
            {
                int mappings = Layers.Sum(value => value.MemberMappings.Count);
                int profiles = Layers.Sum(value => value.StaleProfiles.Count);
                return "重映射计划：" + DependencyChangeCount + " 项依赖版本更新，" + Layers.Count + " 个 Layer，"
                    + mappings + " 个稳定成员映射，" + ChangedMemberMappingCount + " 个成员 ID 变更，" + profiles + " 个 Profile hash 更新，"
                    + Layers.Sum(value => value.UnresolvedMembers.Count) + " 个待人工处理。";
            }
        }
    }

    internal sealed class VfxSemanticStaleRemapService
    {
        public VfxSemanticStaleRemapPlanView Analyze(VfxCompositionDraftView draft, VfxSourceElementCatalog catalog)
        {
            var plan = new VfxSemanticStaleRemapPlanView();
            if (draft == null)
            {
                plan.Diagnostics.Add("草稿不存在。" );
                return plan;
            }
            if (catalog == null || !catalog.Available)
            {
                plan.Diagnostics.Add("当前 Source/Element 目录不可用。" );
                return plan;
            }
            VfxSemanticPropertyRegistryView registry = new VfxSemanticPropertyRegistryService().Load(out string registryMessage);
            VfxProfileManifestView manifest = new VfxSemanticProfileStore().LoadManifest(out string manifestMessage);
            if (registry == null) plan.Diagnostics.Add(registryMessage);
            if (manifest == null) plan.Diagnostics.Add(manifestMessage);
            plan.SourceIndexHash = catalog.Index.IndexHash;
            plan.CurationHash = catalog.Curation.CurationHash;
            plan.PropertyRegistryHash = registry?.RegistryHash ?? string.Empty;
            plan.ProfileManifestHash = manifest?.ManifestHash ?? string.Empty;
            VfxDraftDependencySetView dependencies = draft.Dependencies ?? new VfxDraftDependencySetView();
            if (!string.Equals(dependencies.SourceIndexHash, plan.SourceIndexHash, StringComparison.Ordinal)) plan.DependencyChangeCount++;
            if (!string.Equals(dependencies.CurationHash, plan.CurationHash, StringComparison.Ordinal)) plan.DependencyChangeCount++;
            if (!string.Equals(dependencies.PropertyRegistryHash, plan.PropertyRegistryHash, StringComparison.Ordinal)) plan.DependencyChangeCount++;
            if (!string.Equals(dependencies.ProfileManifestHash, plan.ProfileManifestHash, StringComparison.Ordinal)) plan.DependencyChangeCount++;

            for (int layerIndex = 0; layerIndex < draft.Layers.Count; layerIndex++)
            {
                VfxCompositionLayerView layer = draft.Layers[layerIndex];
                var layerPlan = new VfxSemanticStaleRemapLayerView { LayerId = layer.LayerId, ElementId = layer.ElementId };
                plan.Layers.Add(layerPlan);
                VfxSourceElementView element = catalog.Find(layer.ElementId);
                if (element == null || element.Stale)
                {
                    plan.Diagnostics.Add("Element 不存在或 stale：" + layer.ElementId + "（" + layer.LayerId + "）");
                    continue;
                }
                IReadOnlyList<VfxSourceComponentView> currentMembers = catalog.Members(element);
                var currentIds = new HashSet<string>(currentMembers.Select(value => value.ComponentId), StringComparer.Ordinal);
                var referencedIds = new HashSet<string>(layer.MemberParticipation.Select(value => value.MemberId), StringComparer.Ordinal);
                referencedIds.UnionWith(layer.MemberOverrides.Select(value => value.MemberId));
                foreach (string oldId in referencedIds)
                {
                    if (currentIds.Contains(oldId))
                    {
                        layerPlan.MemberMappings[oldId] = oldId;
                        continue;
                    }
                    if (element.EquivalentComponentIds.Contains(oldId) && currentMembers.Count == 1)
                    {
                        layerPlan.MemberMappings[oldId] = currentMembers[0].ComponentId;
                        continue;
                    }
                    layerPlan.UnresolvedMembers.Add(oldId);
                }
                if (manifest == null) continue;
                for (int dependencyIndex = 0; dependencyIndex < layer.ProfileDependencies.Count; dependencyIndex++)
                {
                    VfxProfileDependencyView dependency = layer.ProfileDependencies[dependencyIndex];
                    VfxProfileManifestEntryView entry = manifest.Profiles.FirstOrDefault(value => value.ProfileId == dependency.ProfileId);
                    if (entry == null)
                    {
                        plan.Diagnostics.Add("Profile 已删除，不能自动重映射：" + dependency.ProfileId + "（" + layer.LayerId + "）");
                        continue;
                    }
                    if (!string.Equals(dependency.Hash, entry.ContentHash, StringComparison.Ordinal)) layerPlan.StaleProfiles.Add(dependency.ProfileId);
                }
            }
            return plan;
        }

        public bool ApplyExact(
            VfxCompositionDraftView draft,
            VfxSourceElementCatalog catalog,
            VfxSemanticStaleRemapPlanView plan,
            out string message)
        {
            if (draft == null || plan == null || !plan.CanApplyExact)
            {
                message = "重映射计划仍包含未证明的映射或错误，不能应用。";
                return false;
            }
            VfxProfileManifestView manifest = new VfxSemanticProfileStore().LoadManifest(out message);
            if (manifest == null || !string.Equals(manifest.ManifestHash, plan.ProfileManifestHash, StringComparison.Ordinal))
            {
                message = "Profile Manifest 在重映射确认前再次变化。";
                return false;
            }
            if (catalog == null || !catalog.Available
                || !string.Equals(catalog.Index.IndexHash, plan.SourceIndexHash, StringComparison.Ordinal)
                || !string.Equals(catalog.Curation.CurationHash, plan.CurationHash, StringComparison.Ordinal))
            {
                message = "Source/Element 目录在重映射确认前再次变化。";
                return false;
            }
            for (int layerIndex = 0; layerIndex < draft.Layers.Count; layerIndex++)
            {
                VfxCompositionLayerView layer = draft.Layers[layerIndex];
                VfxSemanticStaleRemapLayerView layerPlan = plan.Layers.First(value => value.LayerId == layer.LayerId);
                for (int i = 0; i < layer.MemberParticipation.Count; i++)
                    if (layerPlan.MemberMappings.TryGetValue(layer.MemberParticipation[i].MemberId, out string mapped))
                        layer.MemberParticipation[i].MemberId = mapped;
                for (int i = 0; i < layer.MemberOverrides.Count; i++)
                    if (layerPlan.MemberMappings.TryGetValue(layer.MemberOverrides[i].MemberId, out string mapped))
                        layer.MemberOverrides[i].MemberId = mapped;
                for (int i = 0; i < layer.ProfileDependencies.Count; i++)
                {
                    VfxProfileManifestEntryView entry = manifest.Profiles.First(value => value.ProfileId == layer.ProfileDependencies[i].ProfileId);
                    layer.ProfileDependencies[i].Hash = entry.ContentHash;
                }
            }
            draft.SourceIndexHash = plan.SourceIndexHash;
            draft.CurationHash = plan.CurationHash;
            draft.Dependencies ??= new VfxDraftDependencySetView();
            draft.Dependencies.SourceIndexHash = plan.SourceIndexHash;
            draft.Dependencies.CurationHash = plan.CurationHash;
            draft.Dependencies.PropertyRegistryHash = plan.PropertyRegistryHash;
            draft.Dependencies.ProfileManifestHash = plan.ProfileManifestHash;
            message = plan.Summary + " 已应用到内存会话；请检查 diff 后显式保存草稿。";
            return true;
        }
    }

    internal sealed class VfxCompositionDraftSession
    {
        private readonly Stack<string> _undo = new Stack<string>();
        private readonly Stack<string> _redo = new Stack<string>();
        public VfxCompositionDraftView Draft { get; private set; }
        public int SelectedLayerIndex = -1;
        public bool Dirty { get; private set; }
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public string LoadedPath { get; private set; } = string.Empty;
        public string BaseFileHash { get; private set; } = string.Empty;

        public VfxCompositionDraftSession(VfxCompositionDraftView draft, string loadedPath = "", string baseFileHash = "")
        {
            Draft = draft;
            LoadedPath = loadedPath ?? string.Empty;
            BaseFileHash = baseFileHash ?? string.Empty;
        }
        public void Replace(VfxCompositionDraftView draft, string loadedPath = "", string baseFileHash = "")
        {
            Draft = draft; _undo.Clear(); _redo.Clear(); Dirty = false; SelectedLayerIndex = -1;
            LoadedPath = loadedPath ?? string.Empty;
            BaseFileHash = baseFileHash ?? string.Empty;
        }
        public void RecordChange()
        {
            _undo.Push(JsonConvert.SerializeObject(Draft, Formatting.None)); _redo.Clear(); Dirty = true;
        }
        public void MarkDirty() => Dirty = true;
        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            _redo.Push(JsonConvert.SerializeObject(Draft, Formatting.None)); Draft = JsonConvert.DeserializeObject<VfxCompositionDraftView>(_undo.Pop(), VfxCompositionDraftService.JsonSettings); Dirty = true; return true;
        }
        public bool Redo()
        {
            if (_redo.Count == 0) return false;
            _undo.Push(JsonConvert.SerializeObject(Draft, Formatting.None)); Draft = JsonConvert.DeserializeObject<VfxCompositionDraftView>(_redo.Pop(), VfxCompositionDraftService.JsonSettings); Dirty = true; return true;
        }
        public void MarkSaved(string path = "", string fileHash = "")
        {
            Dirty = false;
            if (!string.IsNullOrWhiteSpace(path)) LoadedPath = path;
            if (!string.IsNullOrWhiteSpace(fileHash)) BaseFileHash = fileHash;
        }
    }

    internal sealed class VfxCandidatePrefabAssetTransaction : IDisposable
    {
        private readonly string _outputPath;
        private readonly Func<GameObject, string, bool> _existingAssetSaver;
        private bool _commitApplied;
        private bool _completed;
        private bool _hadExistingOutput;
        private string _absoluteOutputPath;
        private byte[] _originalContents;

        internal VfxCandidatePrefabAssetTransaction(
            string outputPath,
            Func<GameObject, string, bool> existingAssetSaver = null)
        {
            _outputPath = outputPath;
            _existingAssetSaver = existingAssetSaver ?? SaveExistingAsset;
            string directory = Path.GetDirectoryName(outputPath)?.Replace('\\', '/');
            string stem = Path.GetFileNameWithoutExtension(outputPath);
            TemporaryPath = AssetDatabase.GenerateUniqueAssetPath(directory + "/__" + stem + "_candidate_tmp.prefab");
        }

        internal string TemporaryPath { get; }

        internal bool Save(GameObject root, out string message)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, TemporaryPath, out bool success);
            if (success && saved != null)
            {
                message = string.Empty;
                return true;
            }

            message = "Unity 未能保存 Candidate 临时 Prefab。";
            return false;
        }

        internal bool Commit(out string message)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TemporaryPath) == null)
            {
                message = "Candidate 临时 Prefab 不存在，无法提交。";
                return false;
            }

            _hadExistingOutput = AssetDatabase.LoadAssetAtPath<GameObject>(_outputPath) != null;
            if (!_hadExistingOutput)
            {
                string moveError = AssetDatabase.MoveAsset(TemporaryPath, _outputPath);
                if (!string.IsNullOrWhiteSpace(moveError))
                {
                    message = "无法提交 Candidate Prefab：" + moveError;
                    return false;
                }
            }
            else
            {
                _absoluteOutputPath = Path.GetFullPath(Path.Combine(
                    Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
                    _outputPath));
                _originalContents = File.ReadAllBytes(_absoluteOutputPath);
                GameObject temporaryContents = null;
                try
                {
                    temporaryContents = PrefabUtility.LoadPrefabContents(TemporaryPath);
                    if (!_existingAssetSaver(temporaryContents, _outputPath))
                    {
                        RestoreExistingAsset(_absoluteOutputPath, _originalContents);
                        message = "Unity 未能替换正式 Candidate Prefab。";
                        return false;
                    }
                    AssetDatabase.ImportAsset(_outputPath, ImportAssetOptions.ForceSynchronousImport);
                }
                catch (Exception exception)
                {
                    RestoreExistingAsset(_absoluteOutputPath, _originalContents);
                    message = "提交 Candidate Prefab 失败，已恢复旧资产：" + exception.Message;
                    return false;
                }
                finally
                {
                    if (temporaryContents != null) PrefabUtility.UnloadPrefabContents(temporaryContents);
                }
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(_outputPath) == null)
                AssetDatabase.ImportAsset(_outputPath, ImportAssetOptions.ForceSynchronousImport);
            _commitApplied = true;
            message = string.Empty;
            return true;
        }

        internal void Complete()
        {
            if (!_commitApplied) throw new InvalidOperationException("Candidate Prefab 尚未提交，不能完成事务。");
            _completed = true;
            _originalContents = null;
        }

        private static bool SaveExistingAsset(GameObject contents, string outputPath)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(contents, outputPath, out bool success);
            return success && saved != null;
        }

        private void RestoreExistingAsset(string absoluteOutputPath, byte[] originalContents)
        {
            File.WriteAllBytes(absoluteOutputPath, originalContents);
            AssetDatabase.ImportAsset(_outputPath, ImportAssetOptions.ForceSynchronousImport);
        }

        public void Dispose()
        {
            try
            {
                if (_commitApplied && !_completed)
                {
                    if (_hadExistingOutput) RestoreExistingAsset(_absoluteOutputPath, _originalContents);
                    else AssetDatabase.DeleteAsset(_outputPath);
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(TemporaryPath);
            }
        }
    }

    internal sealed class VfxCandidateReceiptFileTransaction : IDisposable
    {
        private readonly string _receiptPath;
        private readonly Action<string, string> _writer;
        private readonly Func<string, string, bool> _committer;
        private bool _commitApplied;
        private bool _completed;
        private bool _hadExistingReceipt;
        private byte[] _originalContents;

        internal VfxCandidateReceiptFileTransaction(
            string receiptPath,
            Action<string, string> writer = null,
            Func<string, string, bool> committer = null)
        {
            _receiptPath = receiptPath;
            _writer = writer ?? WriteText;
            _committer = committer ?? CommitFile;
            string directory = Path.GetDirectoryName(receiptPath) ?? string.Empty;
            string fileName = Path.GetFileName(receiptPath);
            TemporaryPath = Path.Combine(directory, "." + fileName + "." + Guid.NewGuid().ToString("N") + ".tmp");
        }

        internal string TemporaryPath { get; }

        internal bool Prepare(VfxCompositionBakeReceipt receipt, out string message)
        {
            try
            {
                string directory = Path.GetDirectoryName(_receiptPath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                string json = JsonConvert.SerializeObject(receipt, Formatting.Indented);
                _writer(TemporaryPath, json);
                VfxCompositionBakeReceipt roundTrip = JsonConvert.DeserializeObject<VfxCompositionBakeReceipt>(File.ReadAllText(TemporaryPath));
                if (roundTrip == null
                    || string.IsNullOrWhiteSpace(roundTrip.ReceiptHash)
                    || !string.Equals(roundTrip.DraftId, receipt.DraftId, StringComparison.Ordinal)
                    || !string.Equals(roundTrip.OutputPrefabPath, receipt.OutputPrefabPath, StringComparison.Ordinal)
                    || !string.Equals(roundTrip.ReceiptHash, VfxCanonicalJson.Hash(roundTrip, "receiptHash"), StringComparison.Ordinal))
                {
                    message = "Candidate Receipt 临时文件往返校验失败。";
                    return false;
                }

                message = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException)
            {
                message = "Candidate Receipt 准备失败：" + exception.Message;
                return false;
            }
        }

        internal bool Commit(out string message)
        {
            try
            {
                _hadExistingReceipt = File.Exists(_receiptPath);
                _originalContents = _hadExistingReceipt ? File.ReadAllBytes(_receiptPath) : null;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                message = "Candidate Receipt 备份失败，未修改旧文件：" + exception.Message;
                return false;
            }

            try
            {
                if (!_committer(TemporaryPath, _receiptPath))
                {
                    RestoreReceipt();
                    message = "Candidate Receipt 提交失败，已恢复旧文件。";
                    return false;
                }
                _commitApplied = true;
                message = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                RestoreReceipt();
                message = "Candidate Receipt 提交失败，已恢复旧文件：" + exception.Message;
                return false;
            }
        }

        internal void Complete()
        {
            if (!_commitApplied) throw new InvalidOperationException("Candidate Receipt 尚未提交，不能完成事务。");
            _completed = true;
            _originalContents = null;
        }

        public void Dispose()
        {
            try
            {
                if (_commitApplied && !_completed) RestoreReceipt();
            }
            finally
            {
                if (File.Exists(TemporaryPath)) File.Delete(TemporaryPath);
            }
        }

        private static void WriteText(string path, string contents)
        {
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }

        private static bool CommitFile(string temporaryPath, string receiptPath)
        {
            if (File.Exists(receiptPath)) File.Replace(temporaryPath, receiptPath, null, true);
            else File.Move(temporaryPath, receiptPath);
            return true;
        }

        private void RestoreReceipt()
        {
            if (_hadExistingReceipt) File.WriteAllBytes(_receiptPath, _originalContents);
            else if (File.Exists(_receiptPath)) File.Delete(_receiptPath);
        }
    }

    /// <summary>Bakes a validated manual composition into one complete Candidate Prefab.</summary>
    /// <remarks>Editor-only, AllocByDesign. It uses Unity serialization and never modifies source assets.</remarks>
    internal sealed class VfxCompositionCandidateBaker
    {
        internal delegate VfxSemanticPropertyRegistryView RegistryLoader(out string message);
        internal delegate VfxProfileManifestView ProfileManifestLoader(out string message);

        private static readonly string[] PhaseNames = { "Build", "Rebuild", "Loop", "Complete", "Dissipate" };
        private readonly RegistryLoader _registryLoader;
        private readonly ProfileManifestLoader _profileManifestLoader;
        internal const string CandidateRoot = "Assets/VFX/MxCandidates/Manual";
        internal const string ReceiptRoot = "Temp/MxFramework/VfxSourceLibrary/CompositionReceipts";

        internal VfxCompositionCandidateBaker(
            RegistryLoader registryLoader = null,
            ProfileManifestLoader profileManifestLoader = null)
        {
            _registryLoader = registryLoader ?? LoadRegistry;
            _profileManifestLoader = profileManifestLoader ?? LoadProfileManifest;
        }

        public bool Bake(VfxCompositionDraftView draft, VfxSourceElementCatalog catalog, out VfxCompositionBakeReceipt receipt, out string message)
        {
            receipt = null;
            VfxCompositionValidationResult validation = new VfxCompositionDraftService().Validate(draft, catalog);
            if (!validation.Valid) { message = string.Join("\n", validation.Diagnostics); return false; }
            string session = VfxCompositionDraftService.FileToken(draft.DraftId);
            string outputRoot = CandidateRoot + "/" + session;
            string outputPath = outputRoot + "/" + session + ".prefab";
            List<VfxSourceAssetIntegrityView> sourceAssetsBefore = CaptureSourceIntegrity(draft, catalog);
            string sourceIntegrityHashBefore = VfxCanonicalJson.Hash(sourceAssetsBefore);
            string previewCompileHash = string.Empty;
            string bakeCompileHash = string.Empty;
            string fieldDiffHash = string.Empty;
            var phaseRoots = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            var phaseFrames = new Dictionary<string, int>(StringComparer.Ordinal);
            var phaseLayers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var supportGuids = new HashSet<string>(StringComparer.Ordinal);
            var schedules = new List<UnityVfxBakedLayerScheduleEntry>();
            var propertyTracks = new List<UnityVfxBakedPropertyTrack>();
            var compiledLayers = new List<VfxCompositionBakedLayerView>();
            var allDiagnostics = new List<VfxSemanticDiagnosticView>();
            var profileDependencies = new List<VfxProfileDependencyView>();
            bool experimental = false;
            VfxSemanticPropertyRegistryView registry = _registryLoader(out string registryMessage);
            if (registry == null) { message = registryMessage; return false; }
            VfxProfileManifestView profileManifest = _profileManifestLoader(out string manifestMessage);
            if (profileManifest == null) { message = manifestMessage; return false; }
            VfxCompositionDraftService.EnsureFolder(outputRoot);
            using var materialSession = new VfxSemanticMaterialDerivationSession(outputRoot);
            using var prefabTransaction = new VfxCandidatePrefabAssetTransaction(outputPath);
            var root = new GameObject(draft.DisplayName);
            try
            {
                for (int phaseIndex = 0; phaseIndex < PhaseNames.Length; phaseIndex++)
                {
                    string phase = PhaseNames[phaseIndex];
                    var phaseRoot = new GameObject(phase);
                    phaseRoot.transform.SetParent(root.transform, false);
                    phaseRoots.Add(phase, phaseRoot);
                    phaseFrames.Add(phase, 0);
                    phaseLayers.Add(phase, new List<string>());
                }
                for (int layerIndex = 0; layerIndex < draft.Layers.Count; layerIndex++)
                {
                    VfxCompositionLayerView layer = draft.Layers[layerIndex];
                    if (!layer.Enabled) continue;
                    VfxSourceElementView element = catalog.Find(layer.ElementId);
                    GameObject instance = CreateLayer(catalog, element);
                    if (instance == null) { message = "无法实例化 Element：" + layer.ElementId; return false; }
                    instance.hideFlags = HideFlags.None; instance.name = layer.LayerId;
                    instance.transform.SetParent(phaseRoots[layer.Phase].transform, false);
                    VfxCompiledLayerView compiled = VfxSemanticParticleSystemAdapter.CompileAndApply(
                        instance,
                        layer,
                        catalog,
                        element,
                        registry,
                        apply: true,
                        allowSuggestedProfiles: true,
                        useExternalLayerScheduler: true,
                        out VfxResolvedLayerAuthoringProfile resolved);
                    materialSession.Apply(resolved, layer, compiled);
                    compiled.CompileHash = VfxCanonicalJson.Hash(compiled, "compileHash");
                    allDiagnostics.AddRange(compiled.Diagnostics);
                    VfxSemanticParticleSystemAdapter.AppendBakedPropertyTracks(resolved, layer, compiled, propertyTracks);
                    if (!compiled.Valid)
                    {
                        message = "Layer " + layer.LayerId + " 编译失败：\n" + string.Join("\n", compiled.Diagnostics.Where(value => value.Severity == "Error").Select(value => value.Code + " " + value.Message));
                        return false;
                    }
                    experimental |= compiled.Experimental;
                    for (int memberIndex = 0; memberIndex < resolved.Members.Count; memberIndex++)
                    {
                        VfxResolvedComponentAuthoringProfile member = resolved.Members[memberIndex];
                        profileDependencies.Add(new VfxProfileDependencyView { ProfileId = member.Profile.ProfileId, Hash = member.ProfileHash });
                        if (member.ShaderProfile != null)
                            profileDependencies.Add(new VfxProfileDependencyView { ProfileId = member.ShaderProfile.ProfileId, Hash = member.ShaderProfileHash });
                    }
                    var schedule = new UnityVfxBakedLayerScheduleEntry();
                    VfxSemanticParticleSystemAdapter.ConfigureLayerSchedule(schedule, instance, layer, compiled);
                    schedules.Add(schedule);
                    compiledLayers.Add(new VfxCompositionBakedLayerView
                    {
                        LayerId = layer.LayerId,
                        Phase = layer.Phase,
                        StartFrame = compiled.StartFrame,
                        ArtisticEndFrame = compiled.ArtisticEndFrame,
                        TechnicalEndFrame = compiled.TechnicalEndFrame,
                        SourceElementId = layer.ElementId,
                        ProfileDependencies = resolved.Members.SelectMany(value => value.ShaderProfile == null
                            ? new[] { new VfxProfileDependencyView { ProfileId = value.Profile.ProfileId, Hash = value.ProfileHash } }
                            : new[]
                            {
                                new VfxProfileDependencyView { ProfileId = value.Profile.ProfileId, Hash = value.ProfileHash },
                                new VfxProfileDependencyView { ProfileId = value.ShaderProfile.ProfileId, Hash = value.ShaderProfileHash }
                            }).ToList(),
                        FieldDiffs = compiled.FieldDiffs,
                        CompileHash = compiled.CompileHash
                    });
                    phaseFrames[layer.Phase] = Math.Max(phaseFrames[layer.Phase], compiled.ArtisticEndFrame);
                    phaseLayers[layer.Phase].Add(layer.LayerId);
                    foreach (VfxSourceComponentView component in catalog.Members(element))
                    {
                        for (int i = 0; i < component.MaterialGuids.Count; i++) supportGuids.Add(component.MaterialGuids[i]);
                        for (int i = 0; i < component.MeshGuids.Count; i++) supportGuids.Add(component.MeshGuids[i]);
                    }
                }
                VfxCompositionPhaseDurationOverridesView overrides = draft.PhaseDurationOverrides ?? new VfxCompositionPhaseDurationOverridesView();
                for (int phaseIndex = 0; phaseIndex < PhaseNames.Length; phaseIndex++)
                {
                    string phase = PhaseNames[phaseIndex];
                    int? value = overrides.Get(phase);
                    if (value.HasValue) phaseFrames[phase] = value.Value;
                }
                phaseFrames["Loop"] = Math.Max(1, phaseFrames["Loop"]);
                UnityVfxBakedLayerScheduler scheduler = root.AddComponent<UnityVfxBakedLayerScheduler>();
                scheduler.Configure(schedules.ToArray());
                if (propertyTracks.Count > 0)
                {
                    UnityVfxBakedPropertyDriver propertyDriver = root.AddComponent<UnityVfxBakedPropertyDriver>();
                    propertyDriver.Configure(propertyTracks.ToArray());
                }
                UnityVfxLifecycleController controller = root.AddComponent<UnityVfxLifecycleController>();
                controller.Configure(
                    phaseRoots["Build"],
                    phaseRoots["Rebuild"],
                    phaseRoots["Loop"],
                    phaseRoots["Complete"],
                    phaseRoots["Dissipate"],
                    phaseFrames["Build"],
                    phaseFrames["Rebuild"],
                    phaseFrames["Loop"],
                    phaseFrames["Complete"],
                    phaseFrames["Dissipate"],
                    draft.LoopPlaybackMode == "Continuous" ? UnityVfxLoopPlaybackMode.Continuous : UnityVfxLoopPlaybackMode.OneShot);
                ClearHideFlags(root);
                if (!prefabTransaction.Save(root, out message)) return false;
                bakeCompileHash = CompileEvidenceHash(compiledLayers);
                fieldDiffHash = VfxCanonicalJson.Hash(compiledLayers
                    .SelectMany(value => value.FieldDiffs)
                    .OrderBy(value => value.MemberId, StringComparer.Ordinal)
                    .ThenBy(value => value.PropertyId, StringComparer.Ordinal)
                    .ThenBy(value => value.UnityPath, StringComparer.Ordinal)
                    .ToList());
                if (!TryBuildPreviewCompileEvidence(draft, catalog, registry, out previewCompileHash, out string evidenceMessage))
                {
                    message = "Preview/Bake 证据编译失败：" + evidenceMessage;
                    return false;
                }
                if (!string.Equals(previewCompileHash, bakeCompileHash, StringComparison.Ordinal))
                {
                    message = "Preview/Bake 编译结果不一致，已回滚 Candidate。preview=" + previewCompileHash + " bake=" + bakeCompileHash;
                    return false;
                }
                List<VfxSourceAssetIntegrityView> sourceAssetsAfterSave = CaptureSourceIntegrity(draft, catalog);
                string sourceHashAfterSave = VfxCanonicalJson.Hash(sourceAssetsAfterSave);
                if (!string.Equals(sourceIntegrityHashBefore, sourceHashAfterSave, StringComparison.Ordinal))
                {
                    message = "Piloto 源资产在烘焙期间发生变化，已回滚 Candidate。";
                    return false;
                }
            }
            finally { Object.DestroyImmediate(root); }
            if (!prefabTransaction.Commit(out message)) return false;
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            for (int i = 0; i < materialSession.CreatedAssetPaths.Count; i++)
            {
                string guid = AssetDatabase.AssetPathToGUID(materialSession.CreatedAssetPaths[i]);
                if (!string.IsNullOrWhiteSpace(guid)) supportGuids.Add(guid);
            }
            receipt = new VfxCompositionBakeReceipt
            {
                DraftId = draft.DraftId, DraftHash = VfxCompositionDraftService.HashDraft(draft), CompositionContentHash = VfxCompositionDraftService.HashCompositionContent(draft), SourceIndexHash = catalog.Index.IndexHash,
                CurationHash = catalog.Curation.CurationHash, OutputPrefabPath = outputPath, OutputPrefabGuid = AssetDatabase.AssetPathToGUID(outputPath),
                OutputDependencyHash = AssetDatabase.GetAssetDependencyHash(outputPath).ToString(),
                SourceElementIds = draft.Layers.Where(value => value.Enabled).Select(value => value.ElementId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList(),
                DirectSupportAssetGuids = supportGuids.OrderBy(value => value, StringComparer.Ordinal).ToList(),
                LoopPlaybackMode = draft.LoopPlaybackMode,
                PropertyRegistryHash = registry.RegistryHash,
                ProfileManifestHash = profileManifest.ManifestHash,
                ProfileDependencyHash = VfxCanonicalJson.Hash(profileDependencies.OrderBy(value => value.ProfileId, StringComparer.Ordinal).ThenBy(value => value.Hash, StringComparer.Ordinal).ToList()),
                PreviewEvidenceHash = VfxCanonicalJson.Hash(new { draft.PreviewContext, PreviewCompileHash = previewCompileHash }),
                PreviewCompileHash = previewCompileHash,
                BakeCompileHash = bakeCompileHash,
                FieldDiffHash = fieldDiffHash,
                PreviewBakeEquivalent = string.Equals(previewCompileHash, bakeCompileHash, StringComparison.Ordinal),
                SourceIntegrityHashBefore = sourceIntegrityHashBefore,
                SourceIntegrityHashAfter = VfxCanonicalJson.Hash(CaptureSourceIntegrity(draft, catalog)),
                SourceAssetsUnchanged = true,
                SourceAssets = sourceAssetsBefore,
                CandidateContentHash = VfxCompositionDraftService.HashFile(outputPath),
                UnityVersion = Application.unityVersion,
                ValidationLevel = experimental ? nameof(VfxSemanticValidationLevel.CandidateBakeable) : nameof(VfxSemanticValidationLevel.ReviewNeededEligible),
                Experimental = experimental,
                CompiledLayers = compiledLayers,
                Diagnostics = allDiagnostics,
                Phases = PhaseNames.Select(phase => new VfxCompositionBakedPhaseView
                {
                    Phase = phase,
                    DurationFrames = phaseFrames[phase],
                    LayerIds = phaseLayers[phase]
                }).ToList()
            };
            receipt.ReceiptHash = VfxCanonicalJson.Hash(receipt, "receiptHash");
            string receiptPath = ReceiptRoot + "/" + session + ".json";
            using var receiptTransaction = new VfxCandidateReceiptFileTransaction(receiptPath);
            if (!receiptTransaction.Prepare(receipt, out message)) { receipt = null; return false; }
            if (!receiptTransaction.Commit(out message)) { receipt = null; return false; }
            materialSession.Commit();
            prefabTransaction.Complete();
            receiptTransaction.Complete();
            message = "Candidate 已烘焙：" + outputPath + "。仍为本地 Draft，未登记 Ready/Production。";
            return true;
        }

        private static VfxSemanticPropertyRegistryView LoadRegistry(out string message)
        {
            return new VfxSemanticPropertyRegistryService().Load(out message);
        }

        private static VfxProfileManifestView LoadProfileManifest(out string message)
        {
            return new VfxSemanticProfileStore().LoadManifest(out message);
        }

        private static string CompileEvidenceHash(IEnumerable<VfxCompositionBakedLayerView> layers)
        {
            return VfxCanonicalJson.Hash((layers ?? Enumerable.Empty<VfxCompositionBakedLayerView>())
                .Select(value => value.LayerId + "|" + value.CompileHash)
                .ToList());
        }

        private static bool TryBuildPreviewCompileEvidence(
            VfxCompositionDraftView draft,
            VfxSourceElementCatalog catalog,
            VfxSemanticPropertyRegistryView registry,
            out string compileHash,
            out string message)
        {
            var evidence = new List<VfxCompositionBakedLayerView>();
            using var materialSession = new VfxSemanticMaterialDerivationSession(null);
            for (int layerIndex = 0; layerIndex < draft.Layers.Count; layerIndex++)
            {
                VfxCompositionLayerView layer = draft.Layers[layerIndex];
                if (!layer.Enabled) continue;
                VfxSourceElementView element = catalog.Find(layer.ElementId);
                GameObject instance = CreateLayer(catalog, element);
                if (instance == null)
                {
                    compileHash = string.Empty;
                    message = "无法实例化 Element：" + layer.ElementId;
                    return false;
                }
                try
                {
                    VfxCompiledLayerView compiled = VfxSemanticParticleSystemAdapter.CompileAndApply(
                        instance,
                        layer,
                        catalog,
                        element,
                        registry,
                        apply: true,
                        allowSuggestedProfiles: true,
                        useExternalLayerScheduler: true,
                        out VfxResolvedLayerAuthoringProfile resolved);
                    materialSession.Apply(resolved, layer, compiled);
                    compiled.CompileHash = VfxCanonicalJson.Hash(compiled, "compileHash");
                    if (!compiled.Valid)
                    {
                        compileHash = string.Empty;
                        message = "Layer " + layer.LayerId + " 证据编译失败：" + string.Join("; ", compiled.Diagnostics
                            .Where(value => value.Severity == "Error")
                            .Select(value => value.Code + " " + value.Message));
                        return false;
                    }
                    evidence.Add(new VfxCompositionBakedLayerView { LayerId = layer.LayerId, CompileHash = compiled.CompileHash });
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
            compileHash = CompileEvidenceHash(evidence);
            message = "Preview 编译证据已生成。";
            return true;
        }

        private static List<VfxSourceAssetIntegrityView> CaptureSourceIntegrity(VfxCompositionDraftView draft, VfxSourceElementCatalog catalog)
        {
            var byGuid = new Dictionary<string, VfxSourceAssetIntegrityView>(StringComparer.Ordinal);
            for (int layerIndex = 0; layerIndex < draft.Layers.Count; layerIndex++)
            {
                VfxCompositionLayerView layer = draft.Layers[layerIndex];
                if (!layer.Enabled) continue;
                VfxSourceElementView element = catalog.Find(layer.ElementId);
                foreach (VfxSourceComponentView component in catalog.Members(element))
                {
                    if (string.IsNullOrWhiteSpace(component.PrefabGuid) || byGuid.ContainsKey(component.PrefabGuid)) continue;
                    string path = AssetDatabase.GUIDToAssetPath(component.PrefabGuid);
                    byGuid.Add(component.PrefabGuid, new VfxSourceAssetIntegrityView
                    {
                        AssetGuid = component.PrefabGuid,
                        AssetPath = path,
                        IndexedContentHash = component.SourceContentHash,
                        IndexedDependencyHash = component.DependencyHash,
                        CurrentFileHash = VfxCompositionDraftService.HashFile(path),
                        CurrentDependencyHash = string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.GetAssetDependencyHash(path).ToString()
                    });
                }
            }
            return byGuid.Values.OrderBy(value => value.AssetGuid, StringComparer.Ordinal).ToList();
        }

        internal static GameObject CreateLayer(VfxSourceElementCatalog catalog, VfxSourceElementView element)
        {
            string primitivePath = ResolvePrimitivePath(element?.ExistingPrimitiveId);
            if (!string.IsNullOrWhiteSpace(primitivePath))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(primitivePath);
                if (prefab != null) return Object.Instantiate(prefab);
            }
            return VfxSourceElementInstantiation.Create(catalog, element, element?.DisplayName);
        }

        private static string ResolvePrimitivePath(string primitiveId)
        {
            if (string.IsNullOrWhiteSpace(primitiveId) || !File.Exists(VfxCompositionDraftService.CatalogPath)) return string.Empty;
            JObject catalog = JObject.Parse(File.ReadAllText(VfxCompositionDraftService.CatalogPath));
            JObject primitive = ((JArray)catalog["primitives"]).OfType<JObject>().FirstOrDefault(value => value.Value<string>("primitiveId") == primitiveId);
            return primitive?["assetRef"]?.Value<string>("path") ?? string.Empty;
        }

        private static void ClearHideFlags(GameObject root) { foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.None; }
    }

    internal static class VfxCompositionStarterDrafts
    {
        public static bool Generate(VfxSourceElementCatalog catalog, out string message)
        {
            if (catalog == null || !catalog.Available) { message = "Element 素材目录不可用。"; return false; }
            var service = new VfxCompositionDraftService();
            IReadOnlyList<string> existing = service.ListDrafts();
            for (int i = 0; i < existing.Count; i++)
            {
                VfxCompositionDraftView old = service.Load(existing[i], out _);
                if (old != null && old.SchemaVersion != VfxCompositionDraftView.CurrentSchemaVersion)
                    AssetDatabase.DeleteAsset(existing[i]);
            }
            if (!CreateSample(service, catalog, "fireball_charge", "Fireball Charge", "vfx.magic.fireball.charge", "Fire", new[] { "glow", "core", "flame", "sparks", "smoke" }, out message)) return false;
            if (!CreateSample(service, catalog, "fireball_flight", "Fireball Flight", "vfx.magic.fireball.flight", "Fire", new[] { "flash", "core", "trail", "sparks", "smoke" }, out message)) return false;
            if (!CreateSample(service, catalog, "fireball_impact", "Fireball Impact", "vfx.magic.fireball.impact", "Fire", new[] { "flash", "shockwave", "flame", "sparks", "smoke", "decal" }, out message)) return false;
            if (!CreateSample(service, catalog, "lightning_impact", "Lightning Impact", "vfx.magic.lightning.impact", "Lightning", new[] { "flash", "arc", "glow", "sparks", "smoke" }, out message)) return false;
            message = "已生成 4 个 v2 五阶段示例草稿：Fireball Charge / Flight / Impact 与 Lightning Impact。";
            return true;
        }

        [MenuItem("Tools/MxFramework/VFX/生成 v2 五阶段示例草稿")]
        private static void GenerateFromMenu()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            bool success = Generate(catalog, out string message);
            if (success) Debug.Log(message);
            else Debug.LogError(message);
        }

        private static bool CreateSample(
            VfxCompositionDraftService service,
            VfxSourceElementCatalog catalog,
            string id,
            string displayName,
            string effectId,
            string theme,
            IReadOnlyList<string> roles,
            out string message)
        {
            VfxCompositionDraftView draft = service.Create(catalog, "vfx.composition.sample." + id, displayName);
            draft.Brief = "单个 VFX 的 Build / Rebuild / Loop / Complete / Dissipate 生命周期示例。";
            draft.TargetEffectId = effectId;
            draft.LoopPlaybackMode = id.Contains("impact") ? "OneShot" : "Continuous";
            for (int i = 0; i < roles.Count; i++)
            {
                string role = roles[i];
                List<VfxSourceElementView> choices = catalog.Elements
                    .Where(value => !value.Stale && value.Roles.Contains(role) && value.Theme == theme)
                    .OrderBy(value => value.BudgetMaxParticles)
                    .ThenBy(value => value.ElementId, StringComparer.Ordinal)
                    .ToList();
                if (choices.Count == 0)
                    choices = catalog.Elements.Where(value => !value.Stale && value.Roles.Contains(role)).OrderBy(value => value.BudgetMaxParticles).ThenBy(value => value.ElementId, StringComparer.Ordinal).ToList();
                if (choices.Count == 0) continue;
                VfxSourceElementView element = choices[i % choices.Count];
                string phase = ResolveSamplePhase(role, i);
                draft.Layers.Add(VfxCompositionDraftService.CreateLayerView(
                    catalog,
                    element,
                    "layer_" + (i + 1).ToString("00") + "_" + role,
                    phase));
            }
            if (!draft.Layers.Any(value => value.Phase == "Loop") && draft.Layers.Count > 0)
                draft.Layers[0].Phase = "Loop";
            return SaveGenerated(service, draft, out message);
        }

        private static bool SaveGenerated(VfxCompositionDraftService service, VfxCompositionDraftView draft, out string message)
        {
            string path = VfxCompositionDraftService.DraftRoot + "/" + VfxCompositionDraftService.FileToken(draft.DraftId) + ".json";
            string expected = File.Exists(path) ? VfxCompositionDraftService.HashFile(path) : string.Empty;
            string loaded = File.Exists(path) ? path : string.Empty;
            return service.SaveGuarded(draft, loaded, expected, out _, out _, out message);
        }

        private static string ResolveSamplePhase(string role, int index)
        {
            if (role == "core" || role == "trail" || role == "arc" || role == "flame" || role == "shockwave") return "Loop";
            if (role == "smoke" || role == "decal") return index % 2 == 0 ? "Complete" : "Dissipate";
            if (role == "flash") return "Build";
            return index % 2 == 0 ? "Rebuild" : "Complete";
        }

        private static bool GenerateLegacy(VfxSourceElementCatalog catalog, out string message)
        {
            if (catalog == null || !catalog.Available) { message = "Element 素材目录不可用。"; return false; }
            var service = new VfxCompositionDraftService();
            string[][] directionRoles =
            {
                new[] { "glow", "core", "trail", "flash", "shockwave", "smoke" },
                new[] { "glow", "core", "trail", "flash", "sparks", "shockwave" },
                new[] { "core", "trail", "flash", "flame", "smoke" },
                new[] { "glow", "flame", "trail", "flash", "shockwave", "decal", "smoke" },
                new[] { "glow", "core", "trail", "flash", "sparks", "smoke" },
                new[] { "core", "flame", "trail", "shockwave", "smoke", "decal" }
            };
            for (int direction = 0; direction < directionRoles.Length; direction++)
            {
                VfxCompositionDraftView draft = service.Create(catalog, "vfx.composition.sample.fireball." + (direction + 1).ToString("00"), "Fireball 组合示例 " + (direction + 1).ToString("00"));
                draft.Brief = "通用手动组装示例；可替换任意 Element，不包含 Fireball 专用执行逻辑。";
                draft.TargetEffectId = "vfx.magic.fireball";
                AddRoles(draft, catalog, directionRoles[direction], "Fire", direction);
                if (!SaveGenerated(service, draft, out message)) return false;
            }
            VfxCompositionDraftView lightning = service.Create(catalog, "vfx.composition.sample.lightning_impact", "Lightning Impact 组合示例");
            lightning.Brief = "非 Fireball 通用性验收示例。";
            AddRoles(lightning, catalog, new[] { "glow", "arc", "flash", "shockwave", "sparks", "smoke" }, "Lightning", 0);
            if (!SaveGenerated(service, lightning, out message)) return false;
            message = "已生成 6 个 Fireball 普通组合草稿和 1 个 Lightning 非专用示例。";
            return true;
        }

        private static void AddRoles(VfxCompositionDraftView draft, VfxSourceElementCatalog catalog, IEnumerable<string> roles, string theme, int offset)
        {
            int index = 0;
            foreach (string role in roles)
            {
                List<VfxSourceElementView> choices = catalog.Elements.Where(value => !value.Stale && value.Roles.Contains(role) && value.Theme == theme).OrderBy(value => value.BudgetMaxParticles).ThenBy(value => value.ElementId, StringComparer.Ordinal).ToList();
                if (choices.Count == 0) choices = catalog.Elements.Where(value => !value.Stale && value.Roles.Contains(role)).OrderBy(value => value.BudgetMaxParticles).ThenBy(value => value.ElementId, StringComparer.Ordinal).ToList();
                if (choices.Count == 0) continue;
                VfxSourceElementView element = choices[(offset + index) % choices.Count];
                string stage = role == "glow" ? "Charge" : role == "core" || role == "trail" ? "Flight" : "Impact";
                draft.Layers.Add(VfxCompositionDraftService.CreateLayerView(catalog, element, "layer_" + (++index).ToString("00") + "_" + role, stage == "Charge" ? "Build" : stage == "Flight" ? "Loop" : "Complete"));
            }
        }
    }
}

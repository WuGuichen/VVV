using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Editor
{
    internal sealed class VfxSourcePrefabDraftImportAnalysis
    {
        public string PrefabPath = string.Empty;
        public string PrefabGuid = string.Empty;
        public VfxSourceEffectView Effect;
        public readonly List<VfxSourceComponentView> Members = new List<VfxSourceComponentView>();
        public readonly List<string> Diagnostics = new List<string>();
        public VfxTimeSegmentsView SourceSegments = new VfxTimeSegmentsView();
        public int EstimatedArtisticFrames;
        public bool IndexIdentityMatched;
        public bool ContentHashMatched;
        public bool DependencyHashMatched;
        public bool MemberIdentityMatched;
        public bool SubEmitterReferencesValid;
        public bool Valid => Diagnostics.Count == 0 && Effect != null && Members.Count > 0;
    }

    internal sealed class VfxSourceMemberDependencyGroup
    {
        public string GroupId = string.Empty;
        public readonly List<string> MemberComponentIds = new List<string>();
    }

    internal sealed class VfxSourcePrefabMemberSelectionAnalysis
    {
        public VfxSourcePrefabDraftImportAnalysis Source;
        public readonly List<VfxSourceComponentView> Members = new List<VfxSourceComponentView>();
        public readonly List<VfxSourceMemberDependencyGroup> DependencyGroups = new List<VfxSourceMemberDependencyGroup>();
        public readonly List<string> Diagnostics = new List<string>();
        public VfxTimeSegmentsView SourceSegments = new VfxTimeSegmentsView();
        public int EstimatedArtisticFrames;
        public string ElementId = string.Empty;
        public bool Valid => Source?.Valid == true && Members.Count > 0 && Diagnostics.Count == 0;
    }

    internal enum VfxSourcePrefabDraftImportAction
    {
        Add,
        LocateExisting,
        UpdateEvidence,
        Blocked
    }

    internal sealed class VfxSourcePrefabDraftImportPlan
    {
        public VfxSourcePrefabDraftImportAction Action;
        public VfxSourceElementView ExistingElement;
        public string ElementId = string.Empty;
        public string Message = string.Empty;
        public bool MemberOrderStable = true;
        public bool CanApply => Action != VfxSourcePrefabDraftImportAction.Blocked;
    }

    /// <summary>Creates a draft-private Compound Element from an indexed Piloto ParticleSystem Prefab.</summary>
    /// <remarks>Editor-only, AllocByDesign. Source Prefabs are loaded read-only and never modified or saved.</remarks>
    internal sealed class VfxSourcePrefabDraftImportService
    {
        internal const int FramesPerSecond = 60;
        private const string SourceOnlyDistortionGrabberType = "PilotoStudio.DistortionGrabber";

        public VfxSourcePrefabDraftImportAnalysis Analyze(string prefabPath, VfxSourceElementCatalog catalog)
        {
            var result = new VfxSourcePrefabDraftImportAnalysis
            {
                PrefabPath = NormalizeAssetPath(prefabPath)
            };
            if (catalog?.Index == null)
            {
                result.Diagnostics.Add("Piloto Source Index 不可用；请先重建原材料索引。");
                return result;
            }
            if (string.IsNullOrWhiteSpace(result.PrefabPath)
                || !result.PrefabPath.StartsWith(VfxSourceLibraryEditorService.SourceRoot + "/", StringComparison.Ordinal))
            {
                result.Diagnostics.Add("只允许导入已索引的 Piloto Source Prefab。");
                return result;
            }
            result.PrefabGuid = AssetDatabase.AssetPathToGUID(result.PrefabPath);
            result.Effect = catalog.Index.Effects.FirstOrDefault(value =>
                string.Equals(value.PrefabGuid, result.PrefabGuid, StringComparison.Ordinal)
                && string.Equals(value.PrefabPath, result.PrefabPath, StringComparison.Ordinal));
            if (result.Effect == null)
            {
                result.Diagnostics.Add("Prefab 不在当前 Piloto Source Index 中；请先重扫原材料库。");
                return result;
            }
            result.IndexIdentityMatched = true;

            string currentContentHash = VfxCompositionDraftService.HashFile(result.PrefabPath);
            string currentDependencyHash = AssetDatabase.GetAssetDependencyHash(result.PrefabPath).ToString();
            result.ContentHashMatched = string.Equals(currentContentHash, result.Effect.ContentHash, StringComparison.Ordinal);
            result.DependencyHashMatched = string.Equals(currentDependencyHash, result.Effect.DependencyHash, StringComparison.Ordinal);
            if (!result.ContentHashMatched)
                result.Diagnostics.Add("Source Index 中的 Prefab 内容 hash 已过期；请先重建 Source Index 再导入。");
            if (!result.DependencyHashMatched)
                result.Diagnostics.Add("Source Index 中的 Prefab 依赖 hash 已过期；请先重建 Source Index 再导入。");
            if (result.Diagnostics.Count > 0) return result;

            GameObject sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
            try
            {
                if (sourceRoot == null)
                {
                    result.Diagnostics.Add("无法读取源 Prefab 内容。");
                    return result;
                }
                ValidateHierarchy(sourceRoot, result.Diagnostics);
                ParticleSystem[] systems = sourceRoot.GetComponentsInChildren<ParticleSystem>(true);
                var systemsInPrefab = new HashSet<ParticleSystem>(systems);
                bool subEmittersValid = true;
                List<VfxSourceComponentView> indexedMembers = catalog.Index.Components
                    .Where(value => string.Equals(value.PrefabGuid, result.PrefabGuid, StringComparison.Ordinal))
                    .ToList();
                var indexedMembersByLocalFileId = new Dictionary<long, VfxSourceComponentView>();
                for (int i = 0; i < indexedMembers.Count; i++)
                {
                    VfxSourceComponentView indexedMember = indexedMembers[i];
                    if (indexedMember.LocalFileId == 0)
                    {
                        result.Diagnostics.Add("Source Index 成员缺少稳定 LocalFileId：" + indexedMember.ComponentId);
                        continue;
                    }
                    if (!indexedMembersByLocalFileId.TryAdd(indexedMember.LocalFileId, indexedMember))
                        result.Diagnostics.Add("Source Index 存在重复 LocalFileId：" + indexedMember.LocalFileId);
                }
                if (result.Diagnostics.Count > 0) return result;
                for (int i = 0; i < systems.Length; i++)
                {
                    ParticleSystem system = systems[i];
                    string objectPath = RelativePath(sourceRoot.transform, system.transform);
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(system, out string systemGuid, out long localFileId)
                        || localFileId == 0)
                    {
                        result.Diagnostics.Add("无法读取 ParticleSystem 稳定身份：" + system.name + " / " + objectPath);
                        continue;
                    }
                    if (!string.Equals(systemGuid, result.PrefabGuid, StringComparison.Ordinal))
                    {
                        result.Diagnostics.Add("ParticleSystem 来源 GUID 与 Prefab 不一致：" + system.name + " / " + objectPath);
                        continue;
                    }
                    if (!indexedMembersByLocalFileId.TryGetValue(localFileId, out VfxSourceComponentView component))
                    {
                        result.Diagnostics.Add("Source Index 缺少 ParticleSystem 成员：" + system.name + " / " + objectPath + " / " + localFileId);
                        continue;
                    }
                    result.Members.Add(component);
                    int diagnosticCount = result.Diagnostics.Count;
                    ValidateSubEmitters(system, sourceRoot, systemsInPrefab, result.Diagnostics);
                    if (result.Diagnostics.Count != diagnosticCount) subEmittersValid = false;
                }
                if (systems.Length != result.Effect.ParticleSystemCount || result.Members.Count != systems.Length)
                    result.Diagnostics.Add("Prefab 成员数与 Source Index 不一致；请重扫后再导入。");
                if (result.Members.Select(value => value.ComponentId).Distinct(StringComparer.Ordinal).Count() != result.Members.Count)
                    result.Diagnostics.Add("Prefab 存在无法唯一映射的 ParticleSystem 成员。");
                result.MemberIdentityMatched = systems.Length == result.Effect.ParticleSystemCount
                    && result.Members.Count == systems.Length
                    && result.Members.Select(value => value.ComponentId).Distinct(StringComparer.Ordinal).Count() == result.Members.Count;
                result.SubEmitterReferencesValid = subEmittersValid;
                result.SourceSegments = CalculateTiming(systems);
                result.EstimatedArtisticFrames = result.SourceSegments.ArtisticFrames;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is IOException)
            {
                result.Diagnostics.Add("分析源 Prefab 失败：" + exception.Message);
            }
            return result;
        }

        /// <summary>Analyzes an ordered member subset and closes it over all SubEmitter dependencies.</summary>
        /// <remarks>Editor-only, AllocByDesign. The source Prefab is loaded read-only.</remarks>
        internal VfxSourcePrefabMemberSelectionAnalysis AnalyzeMemberSelection(
            VfxSourcePrefabDraftImportAnalysis source,
            IEnumerable<string> requestedMemberIds)
        {
            var result = new VfxSourcePrefabMemberSelectionAnalysis { Source = source };
            if (source?.Valid != true)
            {
                result.Diagnostics.Add("请先选择并通过校验的源 Prefab。");
                return result;
            }

            GameObject sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(source.PrefabPath);
            if (sourceRoot == null)
            {
                result.Diagnostics.Add("无法读取源 Prefab。");
                return result;
            }

            ParticleSystem[] systems = sourceRoot.GetComponentsInChildren<ParticleSystem>(true);
            var systemById = new Dictionary<string, ParticleSystem>(StringComparer.Ordinal);
            var idBySystem = new Dictionary<ParticleSystem, string>();
            var memberById = source.Members.ToDictionary(value => value.ComponentId, StringComparer.Ordinal);
            for (int i = 0; i < systems.Length; i++)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(systems[i], out string guid, out long localId)
                    || !string.Equals(guid, source.PrefabGuid, StringComparison.Ordinal))
                {
                    result.Diagnostics.Add("成员身份不稳定：" + systems[i].name);
                    continue;
                }
                VfxSourceComponentView member = source.Members.FirstOrDefault(value => value.LocalFileId == localId);
                if (member == null)
                {
                    result.Diagnostics.Add("Source Index 缺少成员：" + systems[i].name);
                    continue;
                }
                systemById[member.ComponentId] = systems[i];
                idBySystem[systems[i]] = member.ComponentId;
            }
            if (result.Diagnostics.Count > 0) return result;

            var adjacency = source.Members.ToDictionary(
                value => value.ComponentId,
                _ => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, ParticleSystem> pair in systemById)
            {
                ParticleSystem.SubEmittersModule module = pair.Value.subEmitters;
                if (!module.enabled) continue;
                for (int slot = 0; slot < module.subEmittersCount; slot++)
                {
                    ParticleSystem target = module.GetSubEmitterSystem(slot);
                    if (target == null) continue;
                    if (!idBySystem.TryGetValue(target, out string targetId))
                    {
                        result.Diagnostics.Add("存在外部或缺失的 SubEmitter 引用：" + pair.Value.name + " / slot " + slot);
                        continue;
                    }
                    adjacency[pair.Key].Add(targetId);
                    adjacency[targetId].Add(pair.Key);
                }
            }
            if (result.Diagnostics.Count > 0) return result;

            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (VfxSourceComponentView member in source.Members)
            {
                if (!visited.Add(member.ComponentId)) continue;
                var queue = new Queue<string>();
                var groupIds = new HashSet<string>(StringComparer.Ordinal) { member.ComponentId };
                queue.Enqueue(member.ComponentId);
                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();
                    foreach (string next in adjacency[current])
                    {
                        if (!groupIds.Add(next)) continue;
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
                var group = new VfxSourceMemberDependencyGroup
                {
                    GroupId = "subemitters." + ShortHash(string.Join("|", source.Members
                        .Where(value => groupIds.Contains(value.ComponentId)).Select(value => value.ComponentId)))
                };
                group.MemberComponentIds.AddRange(source.Members
                    .Where(value => groupIds.Contains(value.ComponentId)).Select(value => value.ComponentId));
                result.DependencyGroups.Add(group);
            }

            var selected = new HashSet<string>(requestedMemberIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (selected.Any(value => !memberById.ContainsKey(value)))
            {
                result.Diagnostics.Add("选择中包含已缺失的成员，请重新选择。");
                return result;
            }
            for (int i = 0; i < result.DependencyGroups.Count; i++)
            {
                VfxSourceMemberDependencyGroup group = result.DependencyGroups[i];
                if (group.MemberComponentIds.Any(selected.Contains)) selected.UnionWith(group.MemberComponentIds);
            }
            result.Members.AddRange(source.Members.Where(value => selected.Contains(value.ComponentId)));
            if (result.Members.Count == 0)
            {
                result.Diagnostics.Add("尚未选择 ParticleSystem 成员。");
                return result;
            }

            var selectedSystems = result.Members.Select(value => systemById[value.ComponentId]).ToList();
            result.SourceSegments = CalculateTiming(selectedSystems);
            result.EstimatedArtisticFrames = result.SourceSegments.ArtisticFrames;
            result.ElementId = AuthoredElementId(source.PrefabGuid, result.Members.Select(value => value.ComponentId));
            return result;
        }

        internal VfxSourceElementView BuildAuthoredElement(VfxSourcePrefabMemberSelectionAnalysis selection, string displayName)
        {
            if (selection?.Valid != true) return null;
            VfxSourceElementView element = BuildElement(
                selection.Source,
                selection.ElementId,
                selection.Members,
                "ExplicitAuthored",
                "GlobalAuthored");
            element.DisplayName = (displayName ?? selection.Source.Effect.DisplayName).Trim();
            element.AutoSemanticSuggestions.Add(new VfxSourceSemanticSuggestionView
            {
                Field = "subEmitterDependencyGroups",
                Value = SelectedDependencyGroupSignature(selection)
            });
            return element;
        }

        internal static string DependencyGroupSignature(IEnumerable<VfxSourceMemberDependencyGroup> groups)
        {
            string evidence = string.Join(";", (groups ?? Array.Empty<VfxSourceMemberDependencyGroup>())
                .Select(group => string.Join(",", group.MemberComponentIds)));
            return VfxSourceElementCurationService.Hash(evidence);
        }

        internal static string SelectedDependencyGroupSignature(VfxSourcePrefabMemberSelectionAnalysis selection)
        {
            var selected = new HashSet<string>(
                selection?.Members?.Select(value => value.ComponentId) ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            return DependencyGroupSignature((selection?.DependencyGroups ?? new List<VfxSourceMemberDependencyGroup>())
                .Where(group => group.MemberComponentIds.Any(selected.Contains)));
        }

        public bool CreateOrUpdate(
            VfxSourcePrefabDraftImportAnalysis analysis,
            VfxSourceElementCatalog catalog,
            VfxCompositionDraftView existing,
            out VfxCompositionDraftView draft,
            out string message)
        {
            draft = null;
            if (analysis == null || !analysis.Valid)
            {
                message = analysis == null ? "导入分析不存在。" : string.Join("\n", analysis.Diagnostics);
                return false;
            }
            string draftId = "vfx.composition.source." + analysis.PrefabGuid;
            string elementId = ElementIdForPrefabGuid(analysis.PrefabGuid);
            VfxSourceElementView element = BuildElement(analysis, elementId);

            if (existing == null)
            {
                string displayName = string.Equals(analysis.PrefabGuid, "34a4dd93a152d8a4a842b0b06e1834af", StringComparison.Ordinal)
                    ? "Hit Nova Light 源 Prefab 草稿"
                    : analysis.Effect.DisplayName + " 源 Prefab 草稿";
                draft = new VfxCompositionDraftService().Create(catalog, draftId, displayName);
                draft.Brief = "忠实还原 Piloto 源 Prefab：" + analysis.Effect.DisplayName;
                draft.LoopPlaybackMode = "OneShot";
                draft.LocalElements.Add(element);
                catalog.SetLocalElements(draft.LocalElements);
                VfxCompositionLayerView layer = VfxCompositionDraftService.CreateLayerView(catalog, element, "layer_01_source_compound", "Loop");
                ApplySourceTiming(layer, analysis.SourceSegments);
                draft.Layers.Add(layer);
                message = "已创建 1 个草稿私有 Compound Element、1 个 Loop Layer 和 " + analysis.Members.Count + " 个成员。";
                return true;
            }

            if (!string.Equals(existing.DraftId, draftId, StringComparison.Ordinal))
            {
                message = "现有草稿 ID 与源 Prefab 不匹配，已阻止覆盖。";
                return false;
            }
            VfxSourceElementView previous = existing.LocalElements?.FirstOrDefault(value => string.Equals(value.ElementId, elementId, StringComparison.Ordinal));
            if (previous == null || existing.LocalElements.Count != 1 || existing.Layers.Count != 1)
            {
                message = "现有草稿结构不再是单一私有 Compound Layer，已阻止自动覆盖。";
                return false;
            }
            if (!previous.MemberComponentIds.SequenceEqual(element.MemberComponentIds, StringComparer.Ordinal))
            {
                message = "源 Prefab 成员已增删或顺序变化，无法唯一保留人工修改；原草稿未被覆盖。";
                return false;
            }

            draft = Clone(existing);
            draft.LocalElements[0] = element;
            VfxCompositionLayerView retainedLayer = draft.Layers[0];
            bool preserveRetime = HasUserRetimeEdits(retainedLayer);
            RefreshSourceTiming(retainedLayer, analysis.SourceSegments, preserveRetime);
            message = "已刷新源 Prefab 证据，并保留可映射的语义覆盖、成员状态和预览上下文。";
            return true;
        }

        public VfxSourcePrefabDraftImportPlan PlanForCurrentDraft(
            VfxSourcePrefabDraftImportAnalysis analysis,
            VfxCompositionDraftView draft)
        {
            var plan = new VfxSourcePrefabDraftImportPlan();
            if (analysis == null || !analysis.Valid)
            {
                plan.Action = VfxSourcePrefabDraftImportAction.Blocked;
                plan.Message = analysis == null ? "导入分析不存在。" : string.Join("\n", analysis.Diagnostics);
                return plan;
            }
            if (draft == null)
            {
                plan.Action = VfxSourcePrefabDraftImportAction.Blocked;
                plan.Message = "没有当前草稿；请先新建或打开一个草稿。";
                return plan;
            }

            plan.ElementId = ElementIdForPrefabGuid(analysis.PrefabGuid);
            plan.ExistingElement = draft.LocalElements?.FirstOrDefault(value =>
                string.Equals(value.ElementId, plan.ElementId, StringComparison.Ordinal));
            if (plan.ExistingElement == null)
            {
                plan.Action = VfxSourcePrefabDraftImportAction.Add;
                plan.Message = "将创建 1 个草稿本地 Compound Element。";
                return plan;
            }

            IReadOnlyList<string> memberIds = analysis.Members.Select(value => value.ComponentId).ToList();
            if (!(plan.ExistingElement.MemberComponentIds ?? new List<string>()).SequenceEqual(memberIds, StringComparer.Ordinal))
            {
                plan.Action = VfxSourcePrefabDraftImportAction.Blocked;
                plan.MemberOrderStable = false;
                plan.Message = "源 Prefab 的成员身份或顺序已变化，已阻止覆盖。请先完成成员重映射。";
                return plan;
            }

            bool evidenceMatches = string.Equals(plan.ExistingElement.SourcePrefabContentHash, analysis.Effect.ContentHash, StringComparison.Ordinal)
                && string.Equals(plan.ExistingElement.SourceDependencyHash, analysis.Effect.DependencyHash, StringComparison.Ordinal)
                && (plan.ExistingElement.SourceContentHashes ?? new List<string>()).SequenceEqual(
                    analysis.Members.Select(value => value.SourceContentHash).Distinct(StringComparer.Ordinal),
                    StringComparer.Ordinal);
            plan.Action = evidenceMatches
                ? VfxSourcePrefabDraftImportAction.LocateExisting
                : VfxSourcePrefabDraftImportAction.UpdateEvidence;
            plan.Message = evidenceMatches
                ? "当前草稿已包含证据一致的素材；不会重复创建。"
                : "来源内容已变化但成员稳定；可更新素材证据，人工语义与已有 Layer 将保留。";
            return plan;
        }

        public bool ApplyToCurrentDraft(
            VfxSourcePrefabDraftImportAnalysis analysis,
            VfxSourceElementCatalog catalog,
            VfxCompositionDraftView draft,
            VfxSourcePrefabDraftImportPlan plan,
            bool addLayer,
            string layerId,
            string phase,
            out VfxSourceElementView element,
            out VfxCompositionLayerView layer,
            out string message)
        {
            return ApplyToCurrentDraft(
                analysis,
                catalog,
                draft,
                plan,
                analysis?.Effect?.DisplayName,
                addLayer,
                layerId,
                phase,
                out element,
                out layer,
                out message);
        }

        public bool ApplyToCurrentDraft(
            VfxSourcePrefabDraftImportAnalysis analysis,
            VfxSourceElementCatalog catalog,
            VfxCompositionDraftView draft,
            VfxSourcePrefabDraftImportPlan plan,
            string displayName,
            bool addLayer,
            string layerId,
            string phase,
            out VfxSourceElementView element,
            out VfxCompositionLayerView layer,
            out string message)
        {
            element = null;
            layer = null;
            if (analysis == null || !analysis.Valid || draft == null || plan == null || !plan.CanApply)
            {
                message = plan?.Message ?? "导入计划不可用。";
                return false;
            }
            string normalizedDisplayName = (displayName ?? string.Empty).Trim();
            if (plan.Action == VfxSourcePrefabDraftImportAction.Add
                && (normalizedDisplayName.Length == 0 || normalizedDisplayName.Length > 32))
            {
                message = "卡片展示名必须为 1–32 个字符。";
                return false;
            }

            if (plan.Action == VfxSourcePrefabDraftImportAction.Add)
            {
                element = BuildElement(analysis, plan.ElementId);
                element.DisplayName = normalizedDisplayName;
                draft.LocalElements ??= new List<VfxSourceElementView>();
                draft.LocalElements.Add(element);
            }
            else
            {
                element = plan.ExistingElement;
                if (element == null)
                {
                    message = "当前草稿中的既有素材已变化，请重新分析。";
                    return false;
                }
                if (plan.Action == VfxSourcePrefabDraftImportAction.UpdateEvidence)
                    RefreshTechnicalEvidence(element, BuildElement(analysis, plan.ElementId));
            }

            catalog?.SetLocalElements(draft.LocalElements);
            if (addLayer)
            {
                layer = VfxCompositionDraftService.CreateLayerView(catalog, element, layerId, phase);
                ApplySourceTiming(layer, analysis.SourceSegments);
                draft.Layers.Add(layer);
            }

            message = plan.Action switch
            {
                VfxSourcePrefabDraftImportAction.Add => "素材已添加到当前草稿。",
                VfxSourcePrefabDraftImportAction.UpdateEvidence => "素材证据已更新；人工语义与已有 Layer 已保留。",
                _ => "已定位当前草稿中的既有素材。"
            };
            if (addLayer) message += " 已同时加入当前阶段。";
            return true;
        }

        private static VfxSourceElementView BuildElement(VfxSourcePrefabDraftImportAnalysis analysis, string elementId)
        {
            return BuildElement(analysis, elementId, analysis.Members, "DraftLocal", "DraftOnly");
        }

        private static VfxSourceElementView BuildElement(
            VfxSourcePrefabDraftImportAnalysis analysis,
            string elementId,
            IReadOnlyList<VfxSourceComponentView> members,
            string curationStatus,
            string extractionStatus)
        {
            VfxSourceComponentView first = members[0];
            List<string> roles = members.SelectMany(value => value.Roles ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToList();
            return new VfxSourceElementView
            {
                ElementId = elementId,
                DisplayName = analysis.Effect.DisplayName,
                ElementKind = "CompoundGroup",
                MemberComponentIds = members.Select(value => value.ComponentId).ToList(),
                Roles = roles,
                Family = analysis.Effect.Family,
                Theme = analysis.Effect.Theme,
                Shape = first.Shape,
                Anchor = first.Anchor,
                Lifecycle = "OneShot",
                TimingPhase = "Peak",
                QualityTier = analysis.Effect.QualityTier,
                BudgetMaxParticles = members.Sum(value => value.MaxParticles),
                SourcePackageIds = new List<string> { analysis.Effect.SourcePackageId },
                SourceEffectIds = new List<string> { analysis.Effect.SourceEffectId },
                SourcePrefabGuids = new List<string> { analysis.PrefabGuid },
                SourcePrefabContentHash = analysis.Effect.ContentHash,
                SourceDependencyHash = analysis.Effect.DependencyHash,
                SourceContentHashes = members.Select(value => value.SourceContentHash).Distinct(StringComparer.Ordinal).ToList(),
                TechnicalFingerprint = VfxSourceElementCurationService.Hash(string.Join("|", members.Select(value => value.TechnicalFingerprint))),
                CurationStatus = curationStatus,
                ExtractionStatus = extractionStatus,
                AutoSemanticSuggestions = new List<VfxSourceSemanticSuggestionView>
                {
                    new VfxSourceSemanticSuggestionView { Field = "roles", Value = string.Join(",", roles) },
                    new VfxSourceSemanticSuggestionView { Field = "family", Value = analysis.Effect.Family ?? string.Empty },
                    new VfxSourceSemanticSuggestionView { Field = "theme", Value = analysis.Effect.Theme ?? string.Empty },
                    new VfxSourceSemanticSuggestionView { Field = "anchor", Value = first.Anchor ?? string.Empty }
                }
            };
        }

        internal static string ElementIdForPrefabGuid(string prefabGuid)
        {
            return "vfx.element.local.source-prefab." + (prefabGuid ?? string.Empty);
        }

        internal static string AuthoredElementId(string prefabGuid, IEnumerable<string> orderedMemberIds)
        {
            string evidence = (prefabGuid ?? string.Empty) + "\n" + string.Join("\n", orderedMemberIds ?? Array.Empty<string>());
            using SHA256 sha = SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(evidence))).Replace("-", string.Empty).ToLowerInvariant();
            return "vfx.element.authored.source-prefab." + (prefabGuid ?? string.Empty) + "." + hash;
        }

        private static string ShortHash(string value)
        {
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)))
                .Replace("-", string.Empty).ToLowerInvariant().Substring(0, 12);
        }

        private static void RefreshTechnicalEvidence(VfxSourceElementView target, VfxSourceElementView source)
        {
            target.MemberComponentIds = new List<string>(source.MemberComponentIds);
            target.BudgetMaxParticles = source.BudgetMaxParticles;
            target.SourcePackageIds = new List<string>(source.SourcePackageIds);
            target.SourceEffectIds = new List<string>(source.SourceEffectIds);
            target.SourcePrefabGuids = new List<string>(source.SourcePrefabGuids);
            target.SourcePrefabContentHash = source.SourcePrefabContentHash;
            target.SourceDependencyHash = source.SourceDependencyHash;
            target.SourceContentHashes = new List<string>(source.SourceContentHashes);
            target.TechnicalFingerprint = source.TechnicalFingerprint;
            target.AutoSemanticSuggestions = source.AutoSemanticSuggestions
                .Select(value => new VfxSourceSemanticSuggestionView { Field = value.Field, Value = value.Value })
                .ToList();
            target.Stale = false;
        }

        internal static void ApplySourceTiming(VfxCompositionLayerView layer, VfxTimeSegmentsView segments)
        {
            layer.StartFrame = 0;
            layer.VisualStart = new VfxLayerVisualStartView { Mode = nameof(VfxVisualStartMode.PreserveSource) };
            RefreshSourceTiming(layer, segments, false);
        }

        private static void RefreshSourceTiming(VfxCompositionLayerView layer, VfxTimeSegmentsView segments, bool preserveRetime)
        {
            layer.EstimatedSourceFrames = segments.ArtisticFrames;
            if (layer.Timing == null) layer.Timing = new VfxLayerTimingView();
            layer.Timing.SourceSegments = segments.Clone();
            if (preserveRetime) return;
            layer.Timing.RetimeMode = nameof(VfxRetimeMode.PlaybackWindow);
            layer.Timing.TargetArtisticFrames = segments.ArtisticFrames;
            layer.Timing.MinimumBodyFrames = 1;
            layer.Timing.WindowEndPolicy = nameof(UnityVfxLayerWindowEndPolicy.StopEmitting);
            layer.Timing.DissipateFrames = 0;
        }

        private static bool HasUserRetimeEdits(VfxCompositionLayerView layer)
        {
            if (layer?.Timing == null) return false;
            bool importerDefaultMode = string.Equals(layer.Timing.RetimeMode, nameof(VfxRetimeMode.Fixed), StringComparison.Ordinal)
                || string.Equals(layer.Timing.RetimeMode, nameof(VfxRetimeMode.PlaybackWindow), StringComparison.Ordinal);
            return layer.MemberOverrides?.Any(value => !string.IsNullOrWhiteSpace(value.RetimeMode)) == true
                || layer.Timing.MinimumBodyFrames > 1
                || !importerDefaultMode
                || (!string.IsNullOrWhiteSpace(layer.Timing.WindowEndPolicy)
                    && !string.Equals(layer.Timing.WindowEndPolicy, nameof(UnityVfxLayerWindowEndPolicy.StopEmitting), StringComparison.Ordinal))
                || layer.Timing.DissipateFrames > 0
                || layer.Timing.TargetArtisticFrames != layer.Timing.SourceSegments?.ArtisticFrames;
        }

        private static void ValidateHierarchy(GameObject root, List<string> diagnostics)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                GameObject gameObject = transforms[i].gameObject;
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0)
                    diagnostics.Add("存在 Missing Script：" + HierarchyPath(root.transform, transforms[i]));
                Component[] components = gameObject.GetComponents<Component>();
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    Component component = components[componentIndex];
                    if (component == null) continue;
                    if (component is Transform || component is ParticleSystem || component is ParticleSystemRenderer) continue;
                    if (IsSupportedSourceOnlyHelper(component)) continue;
                    diagnostics.Add("暂不支持的组件 " + component.GetType().Name + "：" + HierarchyPath(root.transform, transforms[i]));
                }
            }
        }

        private static bool IsSupportedSourceOnlyHelper(Component component)
        {
            // DistortionGrabber is a legacy Piloto source-preview helper. Derived elements copy only
            // ParticleSystem data and renderers, so the helper is deliberately validated here but
            // never retained in preview, candidate, or published Prefabs.
            return component is MonoBehaviour
                && string.Equals(component.GetType().FullName, SourceOnlyDistortionGrabberType, StringComparison.Ordinal)
                && component.GetComponent<ParticleSystem>() != null;
        }

        private static void ValidateSubEmitters(
            ParticleSystem system,
            GameObject root,
            HashSet<ParticleSystem> systemsInPrefab,
            List<string> diagnostics)
        {
            ParticleSystem.SubEmittersModule module = system.subEmitters;
            if (!module.enabled || module.subEmittersCount == 0) return;
            for (int i = 0; i < module.subEmittersCount; i++)
            {
                ParticleSystem child = module.GetSubEmitterSystem(i);
                if (child == null) continue;
                if (!child.transform.IsChildOf(root.transform))
                    diagnostics.Add("存在外部 SubEmitter 引用：" + system.name);
                else if (!systemsInPrefab.Contains(child))
                    diagnostics.Add("SubEmitter 目标不属于当前完整 Compound Element：" + system.name + " -> " + child.name);
            }
            // Some Piloto Prefabs retain an enabled SubEmitter module whose serialized slots are all
            // null. It has no executable behavior and is normalized to a disabled empty module when
            // the derived ParticleSystem-only copy is created.
        }

        private static VfxTimeSegmentsView CalculateTiming(IReadOnlyList<ParticleSystem> systems)
        {
            float latestEmitterEnd = 0f;
            float latestVisibleEnd = 0f;
            for (int i = 0; i < systems.Count; i++)
            {
                ParticleSystem system = systems[i];
                ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer == null || !renderer.enabled) continue;
                ParticleSystem.MainModule main = system.main;
                ParticleSystem.EmissionModule emission = system.emission;
                float startDelay = Max(main.startDelay);
                float emitterEnd = 0f;
                bool emits = false;
                // Preserve the authored simulation window even for burst-only systems. Some source effects
                // intentionally keep a longer duration than their visible residence for deterministic replay.
                latestEmitterEnd = Mathf.Max(latestEmitterEnd, startDelay + main.duration);
                latestVisibleEnd = Mathf.Max(latestVisibleEnd, startDelay + main.duration);
                if (emission.enabled && (Max(emission.rateOverTime) > 0.00001f || Max(emission.rateOverDistance) > 0.00001f))
                {
                    emits = true;
                    emitterEnd = Mathf.Max(emitterEnd, main.duration);
                }
                if (emission.enabled && emission.burstCount > 0)
                {
                    var bursts = new ParticleSystem.Burst[emission.burstCount];
                    int count = emission.GetBursts(bursts);
                    for (int burstIndex = 0; burstIndex < count; burstIndex++)
                    {
                        emits = true;
                        ParticleSystem.Burst burst = bursts[burstIndex];
                        emitterEnd = Mathf.Max(emitterEnd, burst.time + burst.repeatInterval * Mathf.Max(0, burst.cycleCount - 1));
                    }
                }
                if (!emits) continue;
                float emissionEnd = startDelay + emitterEnd;
                latestEmitterEnd = Mathf.Max(latestEmitterEnd, emissionEnd);
                latestVisibleEnd = Mathf.Max(latestVisibleEnd, emissionEnd + Max(main.startLifetime));
            }
            int body = Math.Max(1, Mathf.CeilToInt(latestEmitterEnd * FramesPerSecond));
            int visible = Math.Max(body, Mathf.CeilToInt(latestVisibleEnd * FramesPerSecond));
            return new VfxTimeSegmentsView
            {
                BodyFrames = body,
                ExitFrames = Math.Max(0, visible - body),
                CleanupFrames = 1,
                MinimumBodyFrames = body
            };
        }

        private static float Max(ParticleSystem.MinMaxCurve curve)
        {
            float multiplier = curve.curveMultiplier;
            return curve.mode switch
            {
                ParticleSystemCurveMode.Constant => Mathf.Max(0f, curve.constant),
                ParticleSystemCurveMode.TwoConstants => Mathf.Max(0f, curve.constantMax),
                ParticleSystemCurveMode.Curve => Mathf.Max(0f, CurveMax(curve.curve) * multiplier),
                ParticleSystemCurveMode.TwoCurves => Mathf.Max(0f, Mathf.Max(CurveMax(curve.curveMin), CurveMax(curve.curveMax)) * multiplier),
                _ => 0f
            };
        }

        private static float CurveMax(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return 0f;
            float result = 0f;
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++) result = Mathf.Max(result, keys[i].value);
            for (int i = 0; i + 1 < keys.Length; i++)
            {
                Keyframe left = keys[i];
                Keyframe right = keys[i + 1];
                double duration = right.time - left.time;
                if (duration <= 0d
                    || float.IsNaN(left.outTangent)
                    || float.IsNaN(right.inTangent)
                    || float.IsInfinity(left.outTangent)
                    || float.IsInfinity(right.inTangent))
                    continue;

                double outWeight = (left.weightedMode & WeightedMode.Out) != 0
                    ? Mathf.Clamp01(left.outWeight)
                    : 1d / 3d;
                double inWeight = (right.weightedMode & WeightedMode.In) != 0
                    ? Mathf.Clamp01(right.inWeight)
                    : 1d / 3d;
                double y0 = left.value;
                double y1 = y0 + duration * outWeight * left.outTangent;
                double y3 = right.value;
                double y2 = y3 - duration * inWeight * right.inTangent;

                double cubic = -y0 + 3d * y1 - 3d * y2 + y3;
                double quadratic = 3d * y0 - 6d * y1 + 3d * y2;
                double linear = -3d * y0 + 3d * y1;
                ConsiderBezierDerivativeRoots(ref result, y0, y1, y2, y3, 3d * cubic, 2d * quadratic, linear);
            }
            return result;
        }

        private static void ConsiderBezierDerivativeRoots(
            ref float result,
            double y0,
            double y1,
            double y2,
            double y3,
            double quadratic,
            double linear,
            double constant)
        {
            const double epsilon = 1e-12d;
            if (Math.Abs(quadratic) <= epsilon)
            {
                if (Math.Abs(linear) > epsilon)
                    ConsiderBezierValue(ref result, y0, y1, y2, y3, -constant / linear);
                return;
            }

            double discriminant = linear * linear - 4d * quadratic * constant;
            if (discriminant < 0d)
            {
                if (discriminant < -epsilon) return;
                discriminant = 0d;
            }
            double squareRoot = Math.Sqrt(discriminant);
            ConsiderBezierValue(ref result, y0, y1, y2, y3, (-linear - squareRoot) / (2d * quadratic));
            ConsiderBezierValue(ref result, y0, y1, y2, y3, (-linear + squareRoot) / (2d * quadratic));
        }

        private static void ConsiderBezierValue(ref float result, double y0, double y1, double y2, double y3, double time)
        {
            if (time <= 0d || time >= 1d || double.IsNaN(time)) return;
            double inverse = 1d - time;
            double value = inverse * inverse * inverse * y0
                + 3d * inverse * inverse * time * y1
                + 3d * inverse * time * time * y2
                + time * time * time * y3;
            if (!double.IsNaN(value)) result = Mathf.Max(result, (float)value);
        }

        private static VfxCompositionDraftView Clone(VfxCompositionDraftView value)
        {
            return JsonConvert.DeserializeObject<VfxCompositionDraftView>(
                JsonConvert.SerializeObject(value, VfxCompositionDraftService.JsonSettings),
                VfxCompositionDraftService.JsonSettings);
        }

        private static string NormalizeAssetPath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/');
            if (!Path.IsPathRooted(normalized)) return normalized;
            string projectRoot = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/');
            string full = Path.GetFullPath(normalized).Replace('\\', '/');
            return full.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? full.Substring(projectRoot.Length + 1)
                : normalized;
        }

        private static string HierarchyPath(Transform root, Transform value)
        {
            if (value == root) return root.name;
            var parts = new Stack<string>();
            Transform cursor = value;
            while (cursor != null && cursor != root) { parts.Push(cursor.name); cursor = cursor.parent; }
            return root.name + "/" + string.Join("/", parts);
        }

        private static string RelativePath(Transform root, Transform value)
        {
            if (value == root) return string.Empty;
            var parts = new Stack<string>();
            Transform cursor = value;
            while (cursor != null && cursor != root) { parts.Push(cursor.name); cursor = cursor.parent; }
            return string.Join("/", parts);
        }
    }
}

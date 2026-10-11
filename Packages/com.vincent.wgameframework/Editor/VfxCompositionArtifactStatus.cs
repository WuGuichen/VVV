using System;
using System.IO;
using System.Linq;
using MxFramework.Resources;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    internal enum VfxCompositionPrefabSyncState
    {
        NotExported,
        Synchronized,
        DraftChanged,
        PrefabChanged,
        EvidenceInvalid
    }

    internal sealed class VfxCompositionArtifactStatusView
    {
        internal string SaveText = "草稿 未保存";
        internal string SaveStyle = "warning";
        internal string PrefabText = "Prefab 未导出";
        internal string PrefabStyle = "neutral";
        internal string PublicationText = "未发布";
        internal string PublicationStyle = "neutral";
        internal string Tooltip = string.Empty;
        internal VfxCompositionPrefabSyncState PrefabState;
    }

    internal sealed class VfxCompositionArtifactEvidence
    {
        internal bool Readable = true;
        internal string Error = string.Empty;
        internal bool BindingFound;
        internal bool PublishedRecordFound;
        internal bool PrefabExists;
        internal bool PathsMatch;
        internal bool GuidsMatch;
        internal string RecordedPrefabHash = string.Empty;
        internal string ActualPrefabHash = string.Empty;
        internal string PublishedCompositionHash = string.Empty;
        internal string VisualMaturity = string.Empty;
        internal string ReviewStatus = string.Empty;
    }

    internal sealed class VfxCompositionArtifactCatalogSnapshot
    {
        internal VfxPublishedBindingManifest Manifest;
        internal JObject AuthoringCatalog;
        internal bool Readable = true;
        internal string Error = string.Empty;
    }

    /// <summary>Resolves saved draft, exported Prefab, and publication state for the VFX workbench.</summary>
    /// <remarks>Editor-only, AllocByDesign. Catalog and asset reads occur only during explicit UI refreshes.</remarks>
    internal sealed class VfxCompositionArtifactStatusService
    {
        internal VfxCompositionArtifactCatalogSnapshot LoadSnapshot()
        {
            var snapshot = new VfxCompositionArtifactCatalogSnapshot();
            try
            {
                if (File.Exists(VfxPublishedBindingEditorService.ManifestPath))
                {
                    snapshot.Manifest = VfxPublishedBindingEditorService.LoadManifestUnchecked();
                    if (snapshot.Manifest == null)
                        throw new InvalidDataException("Published Binding 无法读取。");
                }

                if (File.Exists(VfxStablePrefabPublicationService.AuthoringCatalogPath))
                {
                    snapshot.AuthoringCatalog = JObject.Parse(
                        File.ReadAllText(VfxStablePrefabPublicationService.AuthoringCatalogPath));
                }
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is Newtonsoft.Json.JsonException)
            {
                snapshot.Readable = false;
                snapshot.Error = exception.Message;
            }

            return snapshot;
        }

        internal VfxCompositionArtifactStatusView Resolve(
            VfxCompositionDraftView draft,
            bool dirty,
            bool hasSavedPath,
            VfxCompositionArtifactCatalogSnapshot snapshot)
        {
            if (draft == null)
                return new VfxCompositionArtifactStatusView();

            VfxCompositionArtifactEvidence evidence = CaptureEvidence(draft, snapshot);
            return ResolveFromEvidence(
                VfxCompositionDraftService.HashCompositionContent(draft),
                dirty,
                hasSavedPath,
                evidence);
        }

        internal static VfxCompositionArtifactStatusView ResolveFromEvidence(
            string currentCompositionHash,
            bool dirty,
            bool hasSavedPath,
            VfxCompositionArtifactEvidence evidence)
        {
            var status = new VfxCompositionArtifactStatusView
            {
                SaveText = dirty ? "草稿 未保存" : hasSavedPath ? "草稿 已保存" : "草稿 尚未保存",
                SaveStyle = dirty ? "warning" : hasSavedPath ? "success" : "neutral"
            };

            evidence ??= new VfxCompositionArtifactEvidence
            {
                Readable = false,
                Error = "缺少状态证据。"
            };
            ApplyPublication(status, evidence);

            bool hasAnyExportEvidence = evidence.BindingFound
                || evidence.PublishedRecordFound
                || evidence.PrefabExists;
            if (!evidence.Readable)
            {
                SetPrefab(status, VfxCompositionPrefabSyncState.EvidenceInvalid, "Prefab 证据异常", "error");
                status.Tooltip = "无法读取 Prefab 同步证据：" + evidence.Error;
                return status;
            }

            if (!hasAnyExportEvidence)
            {
                SetPrefab(status, VfxCompositionPrefabSyncState.NotExported, "Prefab 未导出", "neutral");
                status.Tooltip = "当前草稿尚无稳定 Prefab 与发布记录。";
                return status;
            }

            if (!evidence.BindingFound
                || !evidence.PublishedRecordFound
                || !evidence.PrefabExists
                || !evidence.PathsMatch
                || !evidence.GuidsMatch
                || string.IsNullOrWhiteSpace(evidence.RecordedPrefabHash)
                || string.IsNullOrWhiteSpace(evidence.ActualPrefabHash)
                || string.IsNullOrWhiteSpace(evidence.PublishedCompositionHash))
            {
                SetPrefab(status, VfxCompositionPrefabSyncState.EvidenceInvalid, "Prefab 证据异常", "error");
                status.Tooltip = "Prefab、Published Binding 或 Authoring Catalog 证据缺失或互相冲突。";
                return status;
            }

            if (!string.Equals(
                    evidence.RecordedPrefabHash,
                    evidence.ActualPrefabHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                SetPrefab(status, VfxCompositionPrefabSyncState.PrefabChanged, "Prefab 被外部修改", "error");
                status.Tooltip = "稳定 Prefab 内容与发布记录 hash 不一致，请重新烘焙或恢复正确资产。";
                return status;
            }

            if (!string.Equals(
                    evidence.PublishedCompositionHash,
                    currentCompositionHash,
                    StringComparison.Ordinal))
            {
                SetPrefab(status, VfxCompositionPrefabSyncState.DraftChanged, "Prefab 需重新烘焙", "warning");
                status.Tooltip = "当前草稿内容与最后发布的 composition hash 不一致。";
                return status;
            }

            SetPrefab(status, VfxCompositionPrefabSyncState.Synchronized, "Prefab 已同步", "success");
            status.Tooltip = "当前草稿、稳定 Prefab 与发布证据一致。";
            return status;
        }

        private static void ApplyPublication(
            VfxCompositionArtifactStatusView status,
            VfxCompositionArtifactEvidence evidence)
        {
            if (!evidence.BindingFound)
            {
                status.PublicationText = "未发布";
                status.PublicationStyle = "neutral";
                return;
            }

            if (string.Equals(evidence.VisualMaturity, "Production", StringComparison.Ordinal)
                && string.Equals(evidence.ReviewStatus, "Ready", StringComparison.Ordinal))
            {
                status.PublicationText = "Production · Ready";
                status.PublicationStyle = "production";
                return;
            }

            if (string.Equals(evidence.VisualMaturity, "Candidate", StringComparison.Ordinal)
                && string.Equals(evidence.ReviewStatus, "ReviewNeeded", StringComparison.Ordinal))
            {
                status.PublicationText = "Candidate · 待评审";
                status.PublicationStyle = "candidate";
                return;
            }

            status.PublicationText = string.IsNullOrWhiteSpace(evidence.VisualMaturity)
                ? "发布状态异常"
                : evidence.VisualMaturity + " · " + evidence.ReviewStatus;
            status.PublicationStyle = "error";
        }

        private static void SetPrefab(
            VfxCompositionArtifactStatusView status,
            VfxCompositionPrefabSyncState state,
            string text,
            string style)
        {
            status.PrefabState = state;
            status.PrefabText = text;
            status.PrefabStyle = style;
        }

        private static VfxCompositionArtifactEvidence CaptureEvidence(
            VfxCompositionDraftView draft,
            VfxCompositionArtifactCatalogSnapshot snapshot)
        {
            var evidence = new VfxCompositionArtifactEvidence();
            if (snapshot == null || !snapshot.Readable)
            {
                evidence.Readable = false;
                evidence.Error = snapshot?.Error ?? "发布目录快照不存在。";
                return evidence;
            }

            if (!VfxStableResourceIdentityService.IsValid(draft.ResourceIdentity))
                return evidence;

            ResourceKey resourceKey = draft.ResourceIdentity.ResourceKey.ToResourceKey();
            VfxPublishedBindingRecord binding = snapshot.Manifest?.Bindings?.FirstOrDefault(value =>
                value != null && value.ResourceKey.ToResourceKey() == resourceKey);
            JObject effect = FindEffect(snapshot.AuthoringCatalog, draft.ResourceIdentity.EffectId);
            JObject published = effect?["published"] as JObject;
            string stablePath = draft.ResourceIdentity.StablePrefabPath ?? string.Empty;
            string bindingPath = binding?.AssetPath ?? string.Empty;
            string catalogPath = published?.Value<string>("assetPath") ?? string.Empty;
            string bindingGuid = binding?.AssetGuid ?? string.Empty;
            string catalogGuid = published?.Value<string>("assetGuid") ?? string.Empty;

            evidence.BindingFound = binding != null;
            evidence.PublishedRecordFound = published != null;
            evidence.PrefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(stablePath) != null;
            evidence.PathsMatch = evidence.BindingFound
                && evidence.PublishedRecordFound
                && string.Equals(stablePath, bindingPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(stablePath, catalogPath, StringComparison.OrdinalIgnoreCase);
            string actualGuid = evidence.PrefabExists
                ? AssetDatabase.AssetPathToGUID(stablePath)
                : string.Empty;
            evidence.GuidsMatch = evidence.PrefabExists
                && !string.IsNullOrWhiteSpace(actualGuid)
                && string.Equals(actualGuid, bindingGuid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(actualGuid, catalogGuid, StringComparison.OrdinalIgnoreCase);
            evidence.RecordedPrefabHash = published?.Value<string>("assetContentHash")
                ?? binding?.AssetContentHash
                ?? string.Empty;
            if (evidence.PrefabExists)
                evidence.ActualPrefabHash = VfxCompositionDraftService.HashFile(stablePath);
            evidence.PublishedCompositionHash = published?.Value<string>("compositionHash") ?? string.Empty;
            evidence.VisualMaturity = binding?.VisualMaturity
                ?? effect?.Value<string>("visualMaturity")
                ?? string.Empty;
            evidence.ReviewStatus = binding?.ReviewStatus
                ?? effect?.Value<string>("reviewStatus")
                ?? string.Empty;

            if (binding != null
                && published != null
                && !string.Equals(
                    binding.AssetContentHash,
                    published.Value<string>("assetContentHash"),
                    StringComparison.OrdinalIgnoreCase))
            {
                evidence.Readable = false;
                evidence.Error = "Binding 与 Authoring Catalog 的 Prefab hash 不一致。";
            }

            return evidence;
        }

        private static JObject FindEffect(JObject catalog, string effectId)
        {
            return (catalog?["effects"] as JArray)?
                .OfType<JObject>()
                .FirstOrDefault(value =>
                    string.Equals(value.Value<string>("effectId"), effectId, StringComparison.Ordinal));
        }
    }
}

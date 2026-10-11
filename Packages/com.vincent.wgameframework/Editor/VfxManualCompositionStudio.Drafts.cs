using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using MxFramework.Editor.CharacterAction;
using Object = UnityEngine.Object;

namespace MxFramework.Editor
{
    internal sealed partial class VfxManualCompositionStudioController
    {
        private readonly VisualElement _draftCurrentCard;
        private readonly Label _draftCurrentName;
        private readonly Label _draftCurrentId;
        private readonly Label _draftCurrentMeta;
        private readonly Button _draftBrowserOpenButton;
        private readonly VisualElement _draftBrowserOverlay;
        private readonly ToolbarSearchField _draftBrowserSearch;
        private readonly ListView _draftBrowserList;
        private readonly Label _draftBrowserCount;
        private readonly Label _draftBrowserEmpty;

        private sealed class VfxDraftBrowserItem
        {
            internal string Path;
            internal VfxCompositionDraftView Draft;
            internal DateTime ModifiedLocal;
            internal bool IsCurrent;
            internal VfxCompositionArtifactStatusView ArtifactStatus;
        }

        private readonly List<VfxDraftBrowserItem> _draftBrowserItems = new List<VfxDraftBrowserItem>();
        private readonly List<VfxDraftBrowserItem> _filteredDraftBrowserItems = new List<VfxDraftBrowserItem>();
        private readonly VfxCompositionArtifactStatusService _draftArtifactStatusService =
            new VfxCompositionArtifactStatusService();
        private VfxCompositionArtifactCatalogSnapshot _draftArtifactSnapshot;
        private bool _draftBrowserBinding;

        private void NewDraft()
        {
            if (!ConfirmDiscardUnsavedChanges()) return;
            StartNewDraftSession();
        }

        private void StartNewDraftSession()
        {
            VfxCompositionDraftView draft = _draftService.CreateWithUniqueId(_catalog, "未命名 VFX 组合");
            _session = new VfxCompositionDraftSession(draft); _soloLayerId = string.Empty; _lastBakeReceipt = null; ClearComparison(); ResetPreviewForDraftChange(); ShowInspectorTab(false); RefreshDraftUi();
            EditorPrefs.SetString(WorkspacePreferencePrefix + "LastDraftId", string.Empty);
            RefreshDraftBrowser();
        }

        private void DeleteCurrentDraft()
        {
            VfxCompositionDraftView draft = _session?.Draft;
            if (draft == null)
            {
                SetStatus("没有可删除的 VFX 草稿。", true);
                return;
            }

            bool ownsSavedFile = _draftService.IsOwnedDraftPath(_session.LoadedPath)
                && File.Exists(_session.LoadedPath);
            string impact = BuildDraftDeletionImpact(draft, ownsSavedFile);
            string confirmLabel = ownsSavedFile ? "删除草稿" : "丢弃当前会话";
            if (!EditorUtility.DisplayDialog("删除 VFX 草稿", impact, confirmLabel, "取消")) return;

            string deletedDraftId = draft.DraftId;
            string deletedPath = _session.LoadedPath;
            if (ownsSavedFile)
            {
                if (!_draftService.DeleteGuarded(
                        deletedPath,
                        deletedDraftId,
                        _session.BaseFileHash,
                        out string deleteMessage))
                {
                    SetStatus(deleteMessage, true);
                    EditorUtility.DisplayDialog("草稿未删除", deleteMessage, "知道了");
                    return;
                }
            }
            else
            {
                _recoveryJournal.Clear(deletedDraftId);
            }

            EditorPrefs.SetString(WorkspacePreferencePrefix + "LastDraftId", string.Empty);
            IReadOnlyList<string> remaining = _draftService.ListDrafts();
            string nextPath = remaining.FirstOrDefault(value =>
                !string.Equals(value, deletedPath, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(nextPath)) LoadDraft(nextPath);
            else StartNewDraftSession();

            string completion = ownsSavedFile
                ? "草稿与本机恢复日志已删除；Candidate、Prefab 和已发布资源均已保留。"
                : "当前未保存会话与本机恢复日志已丢弃；外部导入文件未删除。";
            SetStatus(completion, false);
            RefreshSaveFeedback();
        }

        private string BuildDraftDeletionImpact(VfxCompositionDraftView draft, bool ownsSavedFile)
        {
            var lines = new List<string>
            {
                "名称：" + DraftDisplayName(draft),
                "Draft ID：" + draft.DraftId,
                ownsSavedFile
                    ? "将删除：Composition Draft 文件与本机恢复日志。"
                    : "当前会话尚未保存到项目草稿目录；将丢弃会话与本机恢复日志。"
            };
            if (_session.Dirty) lines.Add("警告：当前未保存修改也会丢失。");
            if (!string.IsNullOrWhiteSpace(draft.TargetEffectId))
                lines.Add("保留 Target Effect：" + draft.TargetEffectId);
            if (!string.IsNullOrWhiteSpace(draft.ResourceIdentity?.StablePrefabPath))
                lines.Add("保留稳定 Prefab：" + draft.ResourceIdentity.StablePrefabPath);
            if (!string.IsNullOrWhiteSpace(draft.ResourceIdentity?.ResourceKey?.Id))
                lines.Add("保留 Resource Key：" + draft.ResourceIdentity.ResourceKey.Id);
            lines.Add(string.Empty);
            lines.Add("Candidate、Prefab、Published Binding、Authoring Catalog 和使用方配置都不会被连带删除。");
            return string.Join("\n", lines);
        }
        private void OpenDraft()
        {
            string path = EditorUtility.OpenFilePanel("打开 VFX Composition Draft", Path.GetFullPath(VfxCompositionDraftService.DraftRoot), "json");
            if (string.IsNullOrWhiteSpace(path) || !ConfirmDiscardUnsavedChanges()) return;
            LoadDraft(path);
        }

        private void SaveDraft()
        {
            if (_session?.Draft == null)
            {
                SetSaveFeedback("保存失败 · 没有草稿", "error");
                return;
            }

            _saveDraftButton.SetEnabled(false);
            _saveDraftButton.text = "保存中…";
            string dependencyUpdateSummary = string.Empty;
            try
            {
                if (!TryPrepareDependenciesForSave(out dependencyUpdateSummary)) return;
                if (_draftService.SaveGuarded(
                        _session.Draft,
                        _session.LoadedPath,
                        _session.BaseFileHash,
                        out string savedPath,
                        out string savedHash,
                        out string message))
                {
                    _session.MarkSaved(savedPath, savedHash);
                    _recoveryJournal.Clear(_session.Draft.DraftId);
                    _recoveryPending = false;
                    RememberLastDraft();
                    RefreshDraftBrowser();
                    ValidateDraft();
                    string savedAt = DateTime.Now.ToString("HH:mm:ss");
                    string feedback = string.IsNullOrWhiteSpace(dependencyUpdateSummary)
                        ? "已保存 · " + savedAt
                        : "已更新依赖并保存 · " + savedAt;
                    SetStatus(string.IsNullOrWhiteSpace(dependencyUpdateSummary) ? message : dependencyUpdateSummary + "\n" + message, false);
                    SetSaveFeedback(feedback, "success");
                }
                else
                {
                    SetStatus(message, true);
                    SetSaveFeedback("保存失败 · 点击查看原因", "error");
                    EditorUtility.DisplayDialog(
                        "草稿未保存",
                        message + "\n\n当前修改仍保留在恢复日志中。",
                        "知道了");
                }
            }
            finally
            {
                _saveDraftButton.text = "保存草稿";
                _saveDraftButton.SetEnabled(true);
            }
        }

        private bool TryPrepareDependenciesForSave(out string updateSummary)
        {
            updateSummary = string.Empty;
            var service = new VfxSemanticStaleRemapService();
            VfxSemanticStaleRemapPlanView plan = service.Analyze(_session.Draft, _catalog);
            if (!plan.HasChanges && plan.CanApplyExact) return true;

            if (!plan.CanApplyExact)
            {
                string details = BuildRemapDetails(plan);
                SetStatus(details, true);
                SetSaveFeedback("保存失败 · 引用冲突", "error");
                int action = EditorUtility.DisplayDialogComplex(
                    "草稿未保存",
                    details + "\n\n存在无法唯一确认的引用，系统没有修改草稿。当前修改仍保留在恢复日志中。",
                    "检查并修复",
                    "取消",
                    "保留恢复副本");
                if (action == 0) RemapStaleDependencies();
                else if (action == 2)
                {
                    _recoveryJournal.Write(_session.Draft, _session.LoadedPath, _session.BaseFileHash, out string recoveryMessage);
                    SetStatus(recoveryMessage, false);
                    SetSaveFeedback("已保留恢复副本", "warning");
                }
                return false;
            }

            if (plan.RequiresConfirmation && !EditorUtility.DisplayDialog(
                    "确认依赖更新并保存",
                    BuildRemapDetails(plan) + "\n\n成员 ID 或 Profile 内容将更新。应用前会写入恢复快照；取消不会修改草稿。",
                    "更新并保存",
                    "取消"))
            {
                SetSaveFeedback("● 未保存修改", "warning");
                return false;
            }

            _recoveryJournal.Write(_session.Draft, _session.LoadedPath, _session.BaseFileHash, out _);
            _session.RecordChange();
            if (!service.ApplyExact(_session.Draft, _catalog, plan, out string message))
            {
                _session.Undo();
                SetStatus(message, true);
                SetSaveFeedback("保存失败 · 依赖更新失败", "error");
                EditorUtility.DisplayDialog("草稿未保存", message + "\n\n已恢复到依赖更新前的内存状态。", "知道了");
                return false;
            }
            updateSummary = plan.IsSafeMetadataOnly
                ? "已自动更新 " + plan.DependencyChangeCount + " 项依赖版本；Element、成员和 Profile 引用未重定向。"
                : plan.Summary;
            return true;
        }

        private static string BuildRemapDetails(VfxSemanticStaleRemapPlanView plan)
        {
            string details = plan?.Summary ?? "无法生成依赖更新计划。";
            if (plan?.Diagnostics?.Count > 0) details += "\n\n" + string.Join("\n", plan.Diagnostics.Take(10));
            IEnumerable<string> unresolved = plan?.Layers?.SelectMany(value => value.UnresolvedMembers).Distinct(StringComparer.Ordinal)
                ?? Enumerable.Empty<string>();
            if (unresolved.Any()) details += "\n\n待人工处理成员：\n" + string.Join("\n", unresolved.Take(10));
            return details;
        }

        private void RemapStaleDependencies()
        {
            if (_session?.Draft == null) return;
            var service = new VfxSemanticStaleRemapService();
            VfxSemanticStaleRemapPlanView plan = service.Analyze(_session.Draft, _catalog);
            string details = BuildRemapDetails(plan);
            if (!plan.CanApplyExact)
            {
                EditorUtility.DisplayDialog("VFX 依赖重映射", details + "\n\n未证明的映射不会被自动接受。", "返回检查");
                SetStatus(details, true);
                return;
            }
            if (!EditorUtility.DisplayDialog(
                    "确认稳定 VFX 依赖重映射",
                    details + "\n\n只会更新稳定成员 ID 与已存在 Profile 的新 hash，不会猜测缺失成员。",
                    "应用到会话",
                    "取消")) return;
            _session.RecordChange();
            bool success = service.ApplyExact(_session.Draft, _catalog, plan, out string message);
            if (success) RefreshDraftUi();
            SetStatus(message, !success);
        }

        private void ConfigureDraftBrowser()
        {
            _draftBrowserList.makeItem = CreateDraftBrowserRow;
            _draftBrowserList.bindItem = BindDraftBrowserRow;
            _draftBrowserList.selectionType = SelectionType.Single;
            _draftBrowserList.selectionChanged += OnDraftBrowserSelectionChanged;
            _draftBrowserSearch.RegisterValueChangedCallback(_ => ApplyDraftBrowserFilter());
            _draftBrowserOpenButton.clicked += OpenDraftBrowser;
            _draftCurrentCard.AddManipulator(new Clickable(OpenDraftBrowser));
            _root.Q<Button>("composition-draft-browser-close").clicked += CloseDraftBrowser;
            _draftBrowserOverlay.RegisterCallback<PointerDownEvent>(OnDraftBrowserOverlayPointerDown);
            _draftBrowserOverlay.RegisterCallback<KeyDownEvent>(OnDraftBrowserKeyDown);
            RefreshCurrentDraftSummary();
        }

        private static VisualElement CreateDraftBrowserRow()
        {
            var row = new VisualElement();
            row.AddToClassList("draft-browser-row");
            var header = new VisualElement();
            header.AddToClassList("draft-browser-row-header");
            var name = new Label { name = "draft-browser-row-name" };
            name.AddToClassList("draft-browser-row-name");
            var badge = new Label("当前") { name = "draft-browser-row-badge" };
            badge.AddToClassList("draft-browser-row-badge");
            header.Add(name);
            header.Add(badge);
            var id = new Label { name = "draft-browser-row-id" };
            id.AddToClassList("draft-browser-row-id");
            var meta = new Label { name = "draft-browser-row-meta" };
            meta.AddToClassList("draft-browser-row-meta");
            var states = new VisualElement { name = "draft-browser-row-states" };
            states.AddToClassList("draft-browser-row-states");
            states.Add(CreateDraftStateLabel("draft-browser-row-save"));
            states.Add(CreateDraftStateLabel("draft-browser-row-prefab"));
            states.Add(CreateDraftStateLabel("draft-browser-row-publication"));
            row.Add(header);
            row.Add(id);
            row.Add(meta);
            row.Add(states);
            return row;
        }

        private static Label CreateDraftStateLabel(string name)
        {
            var label = new Label { name = name };
            label.AddToClassList("composition-state-badge");
            label.AddToClassList("state-neutral");
            return label;
        }

        private void BindDraftBrowserRow(VisualElement row, int index)
        {
            if (index < 0 || index >= _filteredDraftBrowserItems.Count) return;
            VfxDraftBrowserItem item = _filteredDraftBrowserItems[index];
            VfxCompositionDraftView draft = item.Draft;
            row.Q<Label>("draft-browser-row-name").text = DraftDisplayName(draft);
            row.Q<Label>("draft-browser-row-id").text = draft.DraftId;
            string effect = string.IsNullOrWhiteSpace(draft.TargetEffectId) ? "未绑定 Effect" : draft.TargetEffectId;
            string modified = item.ModifiedLocal == DateTime.MinValue ? "未写入磁盘" : item.ModifiedLocal.ToString("MM-dd HH:mm");
            row.Q<Label>("draft-browser-row-meta").text = effect + "  ·  " + BuildDraftPhaseSummary(draft) + "  ·  rev " + draft.Revision + "  ·  " + modified;
            VfxCompositionArtifactStatusView artifact = item.ArtifactStatus ?? new VfxCompositionArtifactStatusView();
            ApplyDraftStateLabel(row.Q<Label>("draft-browser-row-save"), artifact.SaveText, artifact.SaveStyle, artifact.Tooltip);
            ApplyDraftStateLabel(row.Q<Label>("draft-browser-row-prefab"), artifact.PrefabText, artifact.PrefabStyle, artifact.Tooltip);
            ApplyDraftStateLabel(row.Q<Label>("draft-browser-row-publication"), artifact.PublicationText, artifact.PublicationStyle, artifact.Tooltip);
            string description = string.IsNullOrWhiteSpace(draft.Brief) ? effect : draft.Brief;
            row.tooltip = description + "\n" + artifact.Tooltip;
            row.EnableInClassList("draft-current", item.IsCurrent);
        }

        private void OnDraftBrowserSelectionChanged(IEnumerable<object> selection)
        {
            if (_draftBrowserBinding) return;
            VfxDraftBrowserItem item = selection?.OfType<VfxDraftBrowserItem>().FirstOrDefault();
            _draftBrowserBinding = true;
            try { _draftBrowserList.ClearSelection(); }
            finally { _draftBrowserBinding = false; }
            if (item?.Draft == null) return;
            if (item.IsCurrent)
            {
                CloseDraftBrowser();
                return;
            }
            if (!ConfirmDiscardUnsavedChanges()) return;
            CloseDraftBrowser();
            LoadDraft(item.Path, item.Draft, "草稿已从项目浏览器加载。");
        }

        private void OpenDraftBrowser()
        {
            RefreshDraftBrowser();
            _draftBrowserOverlay.style.display = DisplayStyle.Flex;
            _draftBrowserOverlay.BringToFront();
            _draftBrowserOverlay.Focus();
            _draftBrowserSearch.schedule.Execute(_draftBrowserSearch.Focus);
        }

        private void CloseDraftBrowser()
        {
            _draftBrowserOverlay.style.display = DisplayStyle.None;
            _draftBrowserBinding = true;
            try { _draftBrowserList.ClearSelection(); }
            finally { _draftBrowserBinding = false; }
        }

        private void OnDraftBrowserOverlayPointerDown(PointerDownEvent evt)
        {
            if (ReferenceEquals(evt.target, _draftBrowserOverlay)) CloseDraftBrowser();
        }

        private void OnDraftBrowserKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape) return;
            CloseDraftBrowser();
            evt.StopPropagation();
        }

        private void LoadDraft(string path, VfxCompositionDraftView draft = null, string loadMessage = null)
        {
            draft ??= _draftService.Load(path, out loadMessage);
            if (draft == null)
            {
                SetStatus(loadMessage, true);
                RefreshDraftBrowser();
                return;
            }
            if (draft.SchemaVersion == VfxCompositionDraftView.PreviousSchemaVersion)
            {
                var migration = new VfxCompositionV3MigrationService();
                if (!migration.TryMigrateFile(path, _catalog, out VfxCompositionDraftView migrated, out _, out string migrationMessage))
                {
                    SetStatus(migrationMessage, true);
                    RefreshDraftBrowser();
                    return;
                }
                draft = migrated;
                loadMessage = migrationMessage;
                AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceSynchronousImport);
            }
            string diskHash = VfxCompositionDraftService.HashFile(path);
            VfxCompositionRecoveryEnvelopeView recovery = _recoveryJournal.Load(draft.DraftId, out _);
            if (recovery != null && !string.Equals(recovery.SessionDraftHash, VfxCompositionDraftService.HashDraft(draft), StringComparison.Ordinal))
            {
                if (TryResolveAutomaticRecovery(draft, diskHash, recovery, out VfxCompositionDraftView recovered, out string recoveryMessage))
                {
                    draft = recovered;
                    loadMessage = recoveryMessage;
                }
                else if (!string.IsNullOrWhiteSpace(recoveryMessage)) loadMessage = recoveryMessage;
            }
            if (_session == null) _session = new VfxCompositionDraftSession(draft, path, diskHash);
            else _session.Replace(draft, path, diskHash);
            if (recovery != null && string.Equals(VfxCompositionDraftService.HashDraft(draft), recovery.SessionDraftHash, StringComparison.Ordinal))
            {
                _session.RecordChange();
                _recoveryPending = false;
            }
            RestoreSelectedLayer(draft);
            _soloLayerId = string.Empty;
            _lastBakeReceipt = null;
            ClearComparison();
            ResetPreviewForDraftChange();
            ShowInspectorTab(false);
            RefreshDraftUi();
            RefreshDraftBrowser();
            RememberLastDraft();
            SetStatus(loadMessage ?? "草稿已加载。", false);
        }

        internal static bool TryResolveAutomaticRecovery(
            VfxCompositionDraftView diskDraft,
            string diskHash,
            VfxCompositionRecoveryEnvelopeView recovery,
            out VfxCompositionDraftView recovered,
            out string message)
        {
            recovered = null;
            message = string.Empty;
            if (diskDraft == null || recovery?.Draft == null) return false;
            if (string.Equals(recovery.BaseFileHash, diskHash, StringComparison.Ordinal))
            {
                recovered = recovery.Draft;
            }
            else if (recovery.BaseDraft != null)
            {
                VfxCompositionThreeWayMergeResult merge = VfxCompositionThreeWayMergeService.Merge(recovery.BaseDraft, diskDraft, recovery.Draft);
                if (merge.CanApply) recovered = merge.Merged;
                else message = "检测到恢复日志，但磁盘与会话在以下字段冲突：\n" + string.Join("\n", merge.Conflicts.Take(8));
            }
            if (recovered == null) return false;
            message = "已自动恢复 " + recovery.WrittenUtc + " 的未保存会话；磁盘文件未被覆盖，请检查后显式保存。";
            return true;
        }

        private void ResetPreviewForDraftChange()
        {
            ClearTemporaryElementPreview();
            _preview.ReplaceEffectInstance(null);
            _comparisonPreview.ReplaceEffectInstance(null);
            _previewDirty = false;
        }

        private bool RestoreLastDraft()
        {
            string draftId = EditorPrefs.GetString(WorkspacePreferencePrefix + "LastDraftId", string.Empty);
            if (string.IsNullOrWhiteSpace(draftId)) return false;
            IReadOnlyList<string> paths = _draftService.ListDrafts();
            for (int i = 0; i < paths.Count; i++)
            {
                VfxCompositionDraftView draft = _draftService.Load(paths[i], out string message);
                if (draft == null || !string.Equals(draft.DraftId, draftId, StringComparison.Ordinal)) continue;
                LoadDraft(paths[i], draft, message);
                return true;
            }
            EditorPrefs.SetString(WorkspacePreferencePrefix + "LastDraftId", string.Empty);
            return false;
        }

        private void RememberLastDraft()
        {
            EditorPrefs.SetString(WorkspacePreferencePrefix + "LastDraftId", _session?.Draft?.DraftId ?? string.Empty);
        }

        private void RestoreSelectedLayer(VfxCompositionDraftView draft)
        {
            string layerId = EditorPrefs.GetString(WorkspacePreferencePrefix + "SelectedLayerId", string.Empty);
            int selectedIndex = draft?.Layers?.FindIndex(value => string.Equals(value.LayerId, layerId, StringComparison.Ordinal)) ?? -1;
            _session.SelectedLayerIndex = selectedIndex;
        }

        private void RememberSelectedLayer()
        {
            VfxCompositionLayerView layer = SelectedLayer();
            EditorPrefs.SetString(WorkspacePreferencePrefix + "SelectedLayerId", layer?.LayerId ?? string.Empty);
        }

        private void RestorePreviewWorkspaceState()
        {
            string stage = EditorPrefs.GetString(WorkspacePreferencePrefix + "PreviewStage", "完整生命周期");
            if (_previewStage.choices.Contains(stage)) _previewStage.SetValueWithoutNotify(stage);
            string background = EditorPrefs.GetString(WorkspacePreferencePrefix + "Background", "暗色");
            if (_background.choices.Contains(background)) _background.SetValueWithoutNotify(background);
            string postProcessing = EditorPrefs.GetString(WorkspacePreferencePrefix + "PostProcessing", "原始材质");
            if (_postProcessingMode.choices.Contains(postProcessing)) _postProcessingMode.SetValueWithoutNotify(postProcessing);
            string cameraMode = EditorPrefs.GetString(WorkspacePreferencePrefix + "CameraMode", "透视");
            if (_cameraMode.choices.Contains(cameraMode)) ApplyCameraPreset(cameraMode);

            Vector3 rootPosition = new Vector3(
                EditorPrefs.GetFloat(WorkspacePreferencePrefix + "RootX", 0f),
                EditorPrefs.GetFloat(WorkspacePreferencePrefix + "RootY", 0f),
                EditorPrefs.GetFloat(WorkspacePreferencePrefix + "RootZ", 0f));
            bool hasSavedCamera = EditorPrefs.HasKey(WorkspacePreferencePrefix + "CameraDistance");
            Vector2 orbit = new Vector2(
                EditorPrefs.GetFloat(WorkspacePreferencePrefix + "CameraOrbitX", 25f),
                EditorPrefs.GetFloat(WorkspacePreferencePrefix + "CameraOrbitY", 20f));
            const string perspectiveDefaultVersionKey = WorkspacePreferencePrefix + "PerspectiveDefaultVersion";
            if (EditorPrefs.GetInt(perspectiveDefaultVersionKey, 0) < 2)
            {
                if (hasSavedCamera
                    && string.Equals(cameraMode, "透视", StringComparison.Ordinal)
                    && Mathf.Approximately(orbit.x, 25f)
                    && Mathf.Approximately(orbit.y, -20f))
                {
                    orbit.y = 20f;
                    EditorPrefs.SetFloat(WorkspacePreferencePrefix + "CameraOrbitY", orbit.y);
                }

                EditorPrefs.SetInt(perspectiveDefaultVersionKey, 2);
            }
            float distance = EditorPrefs.GetFloat(WorkspacePreferencePrefix + "CameraDistance", 5f);
            bool gridVisible = EditorPrefs.GetBool(WorkspacePreferencePrefix + "GridVisible", true);
            bool characterVisible = EditorPrefs.GetBool(WorkspacePreferencePrefix + "CharacterVisible", true);
            bool socketAxesVisible = EditorPrefs.GetBool(WorkspacePreferencePrefix + "SocketAxesVisible", true);

            _rootPosition.SetValueWithoutNotify(rootPosition);
            _gridToggle.SetValueWithoutNotify(gridVisible);
            _characterVisible.SetValueWithoutNotify(characterVisible);
            _characterSocketAxes.SetValueWithoutNotify(socketAxesVisible);
            SetCharacterPreviewDrawerExpanded(EditorPrefs.GetBool(WorkspacePreferencePrefix + "CharacterFoldout", false));
            _preview.SetBackground(background);
            _comparisonPreview.SetBackground(background);
            _elementPreview.SetBackground(background);
            _preview.SetPostProcessingMode(postProcessing);
            _comparisonPreview.SetPostProcessingMode(postProcessing);
            _elementPreview.SetPostProcessingMode(postProcessing);
            _preview.SetRootPosition(rootPosition);
            _comparisonPreview.SetRootPosition(rootPosition);
            _preview.SetGridVisible(gridVisible);
            _comparisonPreview.SetGridVisible(gridVisible);
            if (hasSavedCamera)
            {
                _preview.SetCameraPose(orbit, distance);
                _comparisonPreview.SetCameraPose(orbit, distance);
            }
        }

        private void RestoreElementWorkspaceState()
        {
            _search.SetValueWithoutNotify(EditorPrefs.GetString(WorkspacePreferencePrefix + "Search", string.Empty));
            _sourceFilter.SetValueWithoutNotify("全部素材");
            RestoreChoice(_roleFilter, "RoleFilter");
            RestoreChoice(_themeFilter, "ThemeFilter");
            RestoreChoice(_familyFilter, "FamilyFilter");
            RestoreChoice(_anchorFilter, "AnchorFilter");
            RestoreChoice(_qualityFilter, "QualityFilter");
            RestoreChoice(_packageFilter, "PackageFilter");
            _selectedElement = null;
            FilterElements();
            _restoringWorkspace = false;
            if (_selectedElement != null)
                EditorPrefs.SetString(WorkspacePreferencePrefix + "SelectedElementId", _selectedElement.ElementId);
        }

        private void SavePreviewWorkspaceState()
        {
            Vector2 orbit = _preview.CameraOrbit;
            Vector3 rootPosition = _rootPosition.value;
            EditorPrefs.SetFloat(WorkspacePreferencePrefix + "CameraOrbitX", orbit.x);
            EditorPrefs.SetFloat(WorkspacePreferencePrefix + "CameraOrbitY", orbit.y);
            EditorPrefs.SetFloat(WorkspacePreferencePrefix + "CameraDistance", _preview.CameraDistance);
            EditorPrefs.SetString(WorkspacePreferencePrefix + "CameraMode", _cameraMode.value);
            EditorPrefs.SetString(WorkspacePreferencePrefix + "Background", _background.value);
            EditorPrefs.SetString(WorkspacePreferencePrefix + "PostProcessing", _postProcessingMode.value);
            EditorPrefs.SetFloat(WorkspacePreferencePrefix + "RootX", rootPosition.x);
            EditorPrefs.SetFloat(WorkspacePreferencePrefix + "RootY", rootPosition.y);
            EditorPrefs.SetFloat(WorkspacePreferencePrefix + "RootZ", rootPosition.z);
            EditorPrefs.SetBool(WorkspacePreferencePrefix + "GridVisible", _gridToggle.value);
            EditorPrefs.SetBool(WorkspacePreferencePrefix + "CharacterVisible", _characterVisible.value);
            EditorPrefs.SetBool(WorkspacePreferencePrefix + "SocketAxesVisible", _characterSocketAxes.value);
            EditorPrefs.SetBool(WorkspacePreferencePrefix + "CharacterFoldout", _characterPreviewContextExpanded);
            EditorPrefs.SetString(WorkspacePreferencePrefix + "PreviewStage", _previewStage.value);
            EditorPrefs.SetString(WorkspacePreferencePrefix + "Search", _search.value ?? string.Empty);
            SaveChoice(_roleFilter, "RoleFilter");
            SaveChoice(_themeFilter, "ThemeFilter");
            SaveChoice(_familyFilter, "FamilyFilter");
            SaveChoice(_anchorFilter, "AnchorFilter");
            SaveChoice(_qualityFilter, "QualityFilter");
            SaveChoice(_packageFilter, "PackageFilter");
            RememberLastDraft();
            RememberSelectedLayer();
        }

        private static void RestoreChoice(DropdownField field, string key)
        {
            string value = EditorPrefs.GetString(WorkspacePreferencePrefix + key, field.value);
            if (field.choices.Contains(value)) field.SetValueWithoutNotify(value);
        }

        private static void SaveChoice(DropdownField field, string key)
        {
            EditorPrefs.SetString(WorkspacePreferencePrefix + key, field.value ?? string.Empty);
        }

        private bool ConfirmDiscardUnsavedChanges()
        {
            return _session == null || !_session.Dirty || EditorUtility.DisplayDialog(
                "草稿尚未保存",
                "当前草稿有未保存修改。是否放弃这些修改并切换草稿？",
                "放弃并切换",
                "取消");
        }

        private void RefreshDraftBrowser()
        {
            _draftArtifactSnapshot = _draftArtifactStatusService.LoadSnapshot();
            _draftBrowserItems.Clear();
            string currentId = _session?.Draft?.DraftId ?? string.Empty;
            bool currentFound = false;
            IReadOnlyList<string> paths = _draftService.ListDrafts();
            for (int i = 0; i < paths.Count; i++)
            {
                VfxCompositionDraftView draft = _draftService.Load(paths[i], out _);
                if (draft == null) continue;
                bool isCurrent = string.Equals(draft.DraftId, currentId, StringComparison.Ordinal);
                currentFound |= isCurrent;
                VfxCompositionDraftView displayDraft = isCurrent && _session?.Draft != null
                    ? _session.Draft
                    : draft;
                _draftBrowserItems.Add(new VfxDraftBrowserItem
                {
                    Path = paths[i],
                    Draft = displayDraft,
                    ModifiedLocal = File.Exists(paths[i]) ? File.GetLastWriteTime(paths[i]) : DateTime.MinValue,
                    IsCurrent = isCurrent,
                    ArtifactStatus = _draftArtifactStatusService.Resolve(
                        displayDraft,
                        isCurrent && _session?.Dirty == true,
                        true,
                        _draftArtifactSnapshot)
                });
            }
            if (!currentFound && _session?.Draft != null)
            {
                _draftBrowserItems.Add(new VfxDraftBrowserItem
                {
                    Path = string.Empty,
                    Draft = _session.Draft,
                    ModifiedLocal = DateTime.MinValue,
                    IsCurrent = true,
                    ArtifactStatus = _draftArtifactStatusService.Resolve(
                        _session.Draft,
                        _session.Dirty,
                        false,
                        _draftArtifactSnapshot)
                });
            }
            _draftBrowserItems.Sort((left, right) =>
            {
                int current = right.IsCurrent.CompareTo(left.IsCurrent);
                return current != 0 ? current : right.ModifiedLocal.CompareTo(left.ModifiedLocal);
            });
            ApplyDraftBrowserFilter();
            RefreshCurrentDraftSummary();
        }

        private void ApplyDraftBrowserFilter()
        {
            string query = (_draftBrowserSearch.value ?? string.Empty).Trim();
            _filteredDraftBrowserItems.Clear();
            for (int i = 0; i < _draftBrowserItems.Count; i++)
            {
                VfxDraftBrowserItem item = _draftBrowserItems[i];
                VfxCompositionDraftView draft = item.Draft;
                if (query.Length > 0 && !DraftMatchesQuery(draft, query)) continue;
                _filteredDraftBrowserItems.Add(item);
            }
            _draftBrowserBinding = true;
            try
            {
                _draftBrowserList.itemsSource = _filteredDraftBrowserItems;
                _draftBrowserList.Rebuild();
            }
            finally { _draftBrowserBinding = false; }
            _draftBrowserCount.text = _filteredDraftBrowserItems.Count == _draftBrowserItems.Count
                ? _draftBrowserItems.Count + " 个草稿"
                : _filteredDraftBrowserItems.Count + " / " + _draftBrowserItems.Count + " 个草稿";
            _draftBrowserEmpty.style.display = _filteredDraftBrowserItems.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static bool DraftMatchesQuery(VfxCompositionDraftView draft, string query)
        {
            return ContainsIgnoreCase(draft.DisplayName, query)
                || ContainsIgnoreCase(draft.DraftId, query)
                || ContainsIgnoreCase(draft.TargetEffectId, query)
                || ContainsIgnoreCase(draft.Brief, query);
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RefreshCurrentDraftSummary()
        {
            VfxCompositionDraftView draft = _session?.Draft;
            if (draft == null)
            {
                _draftCurrentName.text = "尚未选择草稿";
                _draftCurrentId.text = "创建或打开一个 Composition Draft";
                _draftCurrentMeta.text = string.Empty;
                RefreshDraftArtifactStatus();
                return;
            }
            _draftCurrentName.text = DraftDisplayName(draft);
            _draftCurrentId.text = draft.DraftId;
            string effect = string.IsNullOrWhiteSpace(draft.TargetEffectId) ? "未绑定 Effect" : draft.TargetEffectId;
            string dirty = _session.Dirty ? "  ·  有未保存修改" : string.Empty;
            _draftCurrentMeta.text = effect + "  ·  " + draft.Layers.Count + " Layer  ·  " + draft.LoopPlaybackMode + dirty;
            RefreshDraftArtifactStatus();
        }

        private void RefreshDraftArtifactStatus()
        {
            VfxCompositionDraftView draft = _session?.Draft;
            if (draft == null)
            {
                ApplyDraftStateLabel(_draftSaveState, "草稿 尚未保存", "neutral", "尚未加载草稿。");
                ApplyDraftStateLabel(_draftPrefabSyncState, "Prefab 未导出", "neutral", "尚未加载草稿。");
                ApplyDraftStateLabel(_draftPublicationState, "未发布", "neutral", "尚未加载草稿。");
                return;
            }

            _draftArtifactSnapshot ??= _draftArtifactStatusService.LoadSnapshot();
            VfxCompositionArtifactStatusView status = _draftArtifactStatusService.Resolve(
                draft,
                _session.Dirty,
                !string.IsNullOrWhiteSpace(_session.LoadedPath),
                _draftArtifactSnapshot);
            ApplyDraftStateLabel(_draftSaveState, status.SaveText, status.SaveStyle, status.Tooltip);
            ApplyDraftStateLabel(_draftPrefabSyncState, status.PrefabText, status.PrefabStyle, status.Tooltip);
            ApplyDraftStateLabel(_draftPublicationState, status.PublicationText, status.PublicationStyle, status.Tooltip);
        }

        private static void ApplyDraftStateLabel(
            Label label,
            string text,
            string style,
            string tooltip)
        {
            if (label == null)
                return;

            label.text = text ?? string.Empty;
            label.tooltip = tooltip ?? string.Empty;
            label.EnableInClassList("state-success", string.Equals(style, "success", StringComparison.Ordinal));
            label.EnableInClassList("state-warning", string.Equals(style, "warning", StringComparison.Ordinal));
            label.EnableInClassList("state-error", string.Equals(style, "error", StringComparison.Ordinal));
            label.EnableInClassList("state-neutral", string.Equals(style, "neutral", StringComparison.Ordinal));
            label.EnableInClassList("state-candidate", string.Equals(style, "candidate", StringComparison.Ordinal));
            label.EnableInClassList("state-production", string.Equals(style, "production", StringComparison.Ordinal));
        }

        private static string DraftDisplayName(VfxCompositionDraftView draft)
        {
            return string.IsNullOrWhiteSpace(draft?.DisplayName) ? draft?.DraftId ?? "未命名草稿" : draft.DisplayName;
        }

        private static string BuildDraftPhaseSummary(VfxCompositionDraftView draft)
        {
            var parts = new List<string>();
            for (int i = 0; i < LifecyclePhases.Length; i++)
            {
                string phase = LifecyclePhases[i];
                int count = draft.Layers.Count(layer => layer.Enabled && string.Equals(layer.Phase, phase, StringComparison.Ordinal));
                if (count > 0) parts.Add(phase + " " + count);
            }
            return parts.Count == 0 ? "0 Layer" : draft.Layers.Count + " Layer · " + string.Join(" / ", parts);
        }
        private void BakeDraft()
        {
            BakeDraft(false);
        }

        private void BakeDraftAsProduction()
        {
            BakeDraft(true);
        }

        private void BakeDraft(bool publishAsProduction)
        {
            if (_session?.Draft == null)
            {
                SetStatus("没有可烘焙的 VFX 草稿。", true);
                return;
            }
            if (!VfxStableResourceIdentityService.IsValid(_session.Draft.ResourceIdentity))
            {
                _session.RecordChange();
                VfxStableResourceIdentityService.Ensure(_session.Draft);
            }
            if (!VfxStableResourceIdentityService.ValidateExclusive(_session.Draft, out string identityMessage))
            {
                SetStatus(identityMessage, true);
                return;
            }

            if (publishAsProduction && !EditorUtility.DisplayDialog(
                    "直接发布正式 VFX",
                    "这会烘焙当前草稿，并把当前 Prefab 内容直接批准为 Production + Ready。\n\n"
                    + "ResourceKey 和稳定 Prefab 路径保持不变；以后执行普通烘焙会自动回到待验收状态。是否继续？",
                    "烘焙并正式发布",
                    "取消"))
                return;

            // “烘焙并发布”同时保存 Draft；保存失败或被用户取消时不得继续改正式 Prefab。
            if (_session.Dirty)
            {
                SaveDraft();
                if (_session.Dirty) return;
            }
            List<string> overflowDiagnostics = PhaseOverflowDiagnostics();
            if (overflowDiagnostics.Count > 0 && !EditorUtility.DisplayDialog(
                    "阶段长度存在越界 Layer",
                    string.Join("\n", overflowDiagnostics.Take(5)) + "\n\n阶段切换仍会严格使用已设置的总帧数。是否继续烘焙并发布？",
                    "确认烘焙",
                    "返回调整"))
                return;
            bool success = _baker.Bake(_session.Draft, _catalog, out VfxCompositionBakeReceipt receipt, out string message); SetStatus(message, !success);
            if (success)
            {
                success = publishAsProduction
                    ? VfxStablePrefabBakeService.PublishProduction(_session.Draft, receipt, out message)
                    : VfxStablePrefabBakeService.Publish(_session.Draft, receipt, out message);
                SetStatus(message, !success);
            }
            if (success)
            {
                _lastBakeReceipt = receipt;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_session.Draft.ResourceIdentity.StablePrefabPath); _preview.ReplaceEffectInstance(prefab == null ? null : Object.Instantiate(prefab)); ApplyCharacterPreviewContext(false); _preview.SetTimelineDuration(_timelineWindowSeconds); _preview.Restart();
                RefreshDraftBrowser();
            }
        }

        private void RescanSource()
        {
            Button maintenance = _root.Q<Button>("element-rescan");
            maintenance?.SetEnabled(false);
            _elementIndexRebuild?.SetEnabled(false);
            bool success = new VfxSourceLibraryEditorService().Scan(out string message);
            maintenance?.SetEnabled(true);
            _elementIndexRebuild?.SetEnabled(!success);
            SetStatus(message, !success);
            if (success) ReloadCatalog();
        }
        private void CurateElements()
        {
            bool success = _curationService.Generate(out _catalog, out string message); SetStatus(message, !success); if (success) RefreshElements();
        }
        private void GenerateElementPreviews()
        {
            bool success = VfxSourceElementPreviewCapture.GenerateAll(out string message); SetStatus(message, !success);
        }
        private void GenerateSamples()
        {
            if (_catalog?.Curation?.DraftLocalOnly == true)
            {
                SetStatus("当前项目启用了 DraftLocalOnly；示例草稿生成已禁用。", true);
                return;
            }
            bool success = VfxCompositionStarterDrafts.Generate(_catalog, out string message); SetStatus(message, !success);
        }

        private void ShowEquivalentSources()
        {
            if (_selectedElement == null || _selectedElement.EquivalentComponentIds.Count == 0)
            {
                EditorUtility.DisplayDialog("等价来源", "当前 Element 没有被折叠的等价来源。", "确定");
                return;
            }
            string body = string.Join("\n", _selectedElement.EquivalentComponentIds.Take(40).Select(id => _catalog.Components.TryGetValue(id, out VfxSourceComponentView component) ? component.PrefabPath + " :: " + component.ObjectPath : id));
            if (_selectedElement.EquivalentComponentIds.Count > 40) body += "\n…其余 " + (_selectedElement.EquivalentComponentIds.Count - 40) + " 项可在全部原始来源中检索。";
            EditorUtility.DisplayDialog("等价来源 · " + _selectedElement.DisplayName, body, "确定");
        }
    }
}

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
        private void RefreshDraftUi()
        {
            BindDraftLocalElements();
            _session.Draft.PhaseDurationOverrides ??= new VfxCompositionPhaseDurationOverridesView();
            VfxCompositionDraftService.NormalizePreviewContext(_session.Draft);
            _binding = true;
            try
            {
                _draftId.SetValueWithoutNotify(_session.Draft.DraftId);
                _draftName.SetValueWithoutNotify(_session.Draft.DisplayName);
                _brief.SetValueWithoutNotify(_session.Draft.Brief);
                _loopMode.SetValueWithoutNotify(_session.Draft.LoopPlaybackMode);
            }
            finally { _binding = false; }
            RefreshVisibleLayerList();
            RefreshCharacterPreviewControls();
            _summaryName.text = _session.Draft.DisplayName;
            RefreshCurrentDraftSummary();
            RebuildPreview();
            RefreshPhaseDurationUi();
            ValidateDraft();
        }

        private void RefreshLifecycleStageView()
        {
            if (_session?.Draft == null) return;
            RefreshVisibleLayerList();
            RebuildPreview();
            RefreshPhaseDurationUi();
            ValidateDraft();
        }

        private void OnPhaseDurationChanged(ChangeEvent<int> evt)
        {
            if (_binding || _session?.Draft == null || !IsSpecificLifecyclePhase(_previewStage.value)) return;
            string phase = _previewStage.value;
            int frames = Mathf.Clamp(evt.newValue, phase == "Loop" ? 1 : 0, PreviewFramesPerSecond * 30);
            ChangeDraft(draft => draft.PhaseDurationOverrides.Set(phase, frames));
            RefreshVisibleLayerList();
            RebuildPreview();
            RefreshPhaseDurationUi();
        }

        private void ToggleAutomaticPhaseDuration()
        {
            if (_session?.Draft == null || !IsSpecificLifecyclePhase(_previewStage.value)) return;
            string phase = _previewStage.value;
            VfxCompositionPhaseDurationOverridesView overrides = _session.Draft.PhaseDurationOverrides;
            int? replacement = overrides.Get(phase).HasValue ? null : CalculateAutomaticPhaseFrames(_session.Draft, _catalog, phase);
            ChangeDraft(draft => draft.PhaseDurationOverrides.Set(phase, replacement));
            RefreshVisibleLayerList();
            RebuildPreview();
            RefreshPhaseDurationUi();
        }

        private void RefreshPhaseDurationUi()
        {
            if (_session?.Draft == null) return;
            bool specificPhase = IsSpecificLifecyclePhase(_previewStage.value);
            _phaseDurationEditor.style.display = specificPhase ? DisplayStyle.Flex : DisplayStyle.None;
            _phaseSummaryStrip.style.display = DisplayStyle.Flex;
            _phaseAllButton.EnableInClassList("phase-selected", !specificPhase);

            VfxCompositionPhaseDurationOverridesView overrides = _session.Draft.PhaseDurationOverrides;
            if (specificPhase)
            {
                string phase = _previewStage.value;
                int? configured = overrides.Get(phase);
                int frames = configured ?? CalculateAutomaticPhaseFrames(_session.Draft, _catalog, phase);
                _binding = true;
                try
                {
                    _phaseDurationName.text = PhaseDisplayName(phase) + "长度";
                    _phaseDurationFrames.SetValueWithoutNotify(frames);
                    _phaseDurationFrames.SetEnabled(configured.HasValue);
                    _phaseDurationAuto.EnableInClassList("auto-active", !configured.HasValue);
                    _phaseDurationAuto.text = !configured.HasValue ? "自动中" : "恢复自动";
                    _phaseDurationAuto.tooltip = !configured.HasValue
                        ? "当前按启用 Layer 自动计算；点击后固定为当前帧数并允许编辑"
                        : "点击恢复为按启用 Layer 的最晚结束帧自动计算";
                }
                finally { _binding = false; }
            }

            for (int i = 0; i < LifecyclePhases.Length; i++)
            {
                string phase = LifecyclePhases[i];
                int? configured = overrides.Get(phase);
                int frames = configured ?? CalculateAutomaticPhaseFrames(_session.Draft, _catalog, phase);
                string mode = configured.HasValue ? "手动" : "自动";
                string value = phase == "Loop" && _session.Draft.LoopPlaybackMode == "Continuous"
                    ? "持续（单周期 " + frames + " 帧）"
                    : mode + " · " + frames + " 帧";
                _phaseSummaryButtons[i].text = PhaseDisplayName(phase) + " · " + value;
                _phaseSummaryButtons[i].EnableInClassList("phase-selected", string.Equals(_previewStage.value, phase, StringComparison.Ordinal));
                _phaseSummaryLabels[i].text = value;
            }
        }

        private static string PhaseDisplayName(string phase)
        {
            return phase switch
            {
                "Build" => "构建",
                "Rebuild" => "重建",
                "Loop" => "循环",
                "Complete" => "完成",
                "Dissipate" => "消散",
                _ => phase
            };
        }

        internal static int CalculateAutomaticPhaseFrames(VfxCompositionDraftView draft, VfxSourceElementCatalog catalog, string phase)
        {
            int frames = 0;
            if (draft?.Layers != null)
            {
                for (int i = 0; i < draft.Layers.Count; i++)
                {
                    VfxCompositionLayerView layer = draft.Layers[i];
                    if (!layer.Enabled || !string.Equals(layer.Phase, phase, StringComparison.Ordinal)) continue;
                    VfxSourceElementView element = catalog?.Find(layer.ElementId);
                    int startFrame = Math.Max(0, layer.StartFrame);
                    int durationFrames = layer.Timing?.TargetArtisticFrames > 0
                        ? layer.Timing.TargetArtisticFrames
                        : Mathf.Max(1, SecondsToFrame(EstimateLayerDuration(element, layer)));
                    frames = Mathf.Max(frames, startFrame + durationFrames);
                }
            }
            return phase == "Loop" ? Mathf.Max(1, frames) : frames;
        }

        internal static int CalculateFullLifecyclePreviewFrames(VfxCompositionDraftView draft, VfxSourceElementCatalog catalog)
        {
            if (draft == null) return 1;
            VfxCompositionPhaseDurationOverridesView overrides = draft.PhaseDurationOverrides ?? new VfxCompositionPhaseDurationOverridesView();
            int build = overrides.Get("Build") ?? CalculateAutomaticPhaseFrames(draft, catalog, "Build");
            int loop = overrides.Get("Loop") ?? CalculateAutomaticPhaseFrames(draft, catalog, "Loop");
            int complete = overrides.Get("Complete") ?? CalculateAutomaticPhaseFrames(draft, catalog, "Complete");
            return Mathf.Max(1, build + Mathf.Max(1, loop) + complete);
        }

        private int EffectivePhaseFrames(string phase)
        {
            if (_session?.Draft == null) return phase == "Loop" ? 1 : 0;
            int? configured = _session.Draft.PhaseDurationOverrides.Get(phase);
            return configured ?? CalculateAutomaticPhaseFrames(_session.Draft, _catalog, phase);
        }

        private int LayerOverflowFrames(VfxCompositionLayerView layer)
        {
            if (layer == null || !layer.Enabled) return 0;
            VfxSourceElementView element = _catalog?.Find(layer.ElementId);
            int endFrame = Math.Max(0, layer.StartFrame) + (layer.Timing?.TargetArtisticFrames > 0
                ? layer.Timing.TargetArtisticFrames
                : Mathf.Max(1, SecondsToFrame(EstimateLayerDuration(element, layer))));
            return PhaseOverflowFrames(layer, endFrame);
        }

        private int PhaseOverflowFrames(VfxCompositionLayerView layer, int endFrame)
        {
            return layer == null || !layer.Enabled ? 0 : Mathf.Max(0, endFrame - EffectivePhaseFrames(layer.Phase));
        }

        private static bool IsSpecificLifecyclePhase(string phase)
        {
            return Array.IndexOf(LifecyclePhases, phase) >= 0;
        }

        private void RefreshVisibleLayerList()
        {
            int preferredSourceIndex = _session?.SelectedLayerIndex ?? -1;
            _visibleLayerIndices.Clear();
            if (_session?.Draft != null)
            {
                for (int i = 0; i < _session.Draft.Layers.Count; i++)
                    if (IsLayerVisibleForStage(_session.Draft.Layers[i], _previewStage.value))
                        _visibleLayerIndices.Add(i);
            }
            RefreshTimelineScale();
            _layerList.itemsSource = _visibleLayerIndices;
            _layerList.Rebuild();
            int visibleSelection = _visibleLayerIndices.IndexOf(preferredSourceIndex);
            if (visibleSelection < 0 && _visibleLayerIndices.Count > 0) visibleSelection = 0;
            if (visibleSelection >= 0)
            {
                _layerList.SetSelection(visibleSelection);
                BindSelectedLayer();
            }
            else
            {
                _layerList.ClearSelection();
                BindSelectedLayer();
            }
            bool allStages = string.Equals(_previewStage.value, "完整生命周期", StringComparison.Ordinal);
            _layerCount.text = allStages
                ? _visibleLayerIndices.Count + " 层"
                : _visibleLayerIndices.Count + " / " + (_session?.Draft.Layers.Count ?? 0) + " 层 · " + _previewStage.value;
            _layerEmpty.text = allStages ? "Layer Stack 为空" : _previewStage.value + " 阶段没有 Layer";
            _layerEmpty.style.display = _visibleLayerIndices.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshLayerCommandState();
        }
        private void ValidateDraft()
        {
            VfxCompositionValidationResult result = _draftService.Validate(_session?.Draft, _catalog);
            List<string> overflowDiagnostics = PhaseOverflowDiagnostics();
            string solo = string.IsNullOrWhiteSpace(_soloLayerId) ? string.Empty : " · Solo " + _soloLayerId;
            _diagnostics.text = result.Valid ? "草稿有效 · " + _session.Draft.Layers.Count + " Layers" + solo + (_session.Dirty ? " · 未保存" : string.Empty) : string.Join("\n", result.Diagnostics.Take(5));
            _diagnostics.EnableInClassList("status-warning", !result.Valid);
            int budget = CompositionBudget();
            bool overBudget = budget > CompositionBudgetLimit;
            _budget.value = Mathf.Clamp(budget, 0, CompositionBudgetLimit);
            _budget.title = budget + " / " + CompositionBudgetLimit;
            _budget.EnableInClassList("over-budget", overBudget);
            _budgetLabel.text = "总预算 " + budget + " / " + CompositionBudgetLimit;
            _summaryBudget.text = budget + " / " + CompositionBudgetLimit;
            _summaryDiagnostics.text = (result.Diagnostics.Count + overflowDiagnostics.Count + (overBudget ? 1 : 0)) + " 个问题";
            _summaryLifecycle.text = LifecycleSummary();
            if (result.Valid && overBudget)
            {
                _diagnostics.text = "预算超出当前 GameplayImportant 质量档：" + budget + " / " + CompositionBudgetLimit + " · 请降低层数或发射倍率";
                _diagnostics.EnableInClassList("status-warning", true);
            }
            else if (result.Valid && overflowDiagnostics.Count > 0)
            {
                _diagnostics.text = string.Join("\n", overflowDiagnostics.Take(5));
                _diagnostics.EnableInClassList("status-warning", true);
            }
            bool sourceIndexReady = _catalog?.SourceIndexAvailable == true;
            _bakeButton.SetEnabled(sourceIndexReady && result.Valid && !overBudget && _session.Draft.Layers.Count > 0);
            _productionBakeButton.SetEnabled(sourceIndexReady && result.Valid && !overBudget && _session.Draft.Layers.Count > 0);
            RefreshLayerCommandState();
            RefreshSaveFeedback();
            RefreshDraftArtifactStatus();
            ScheduleRecoveryJournal();
        }

        private void RefreshSaveFeedback()
        {
            if (_session?.Draft == null)
            {
                SetSaveFeedback("未加载草稿", string.Empty);
                return;
            }
            if (_session.Dirty)
            {
                SetSaveFeedback("● 未保存修改", "warning");
                return;
            }
            SetSaveFeedback(
                string.IsNullOrWhiteSpace(_session.LoadedPath) ? "尚未保存" : "✓ 已保存",
                string.IsNullOrWhiteSpace(_session.LoadedPath) ? string.Empty : "success");
        }

        private void SetSaveFeedback(string message, string state)
        {
            if (_saveDraftStatus == null) return;
            _saveDraftStatus.text = message ?? string.Empty;
            _saveDraftStatus.tooltip = _diagnostics?.text ?? message ?? string.Empty;
            _saveDraftStatus.EnableInClassList("save-success", string.Equals(state, "success", StringComparison.Ordinal));
            _saveDraftStatus.EnableInClassList("save-warning", string.Equals(state, "warning", StringComparison.Ordinal));
            _saveDraftStatus.EnableInClassList("save-error", string.Equals(state, "error", StringComparison.Ordinal));
        }

        private void ScheduleRecoveryJournal()
        {
            if (_session?.Dirty != true) return;
            _recoveryPending = true;
            _recoveryDueTime = EditorApplication.timeSinceStartup + 0.5d;
        }

        private void FlushRecoveryJournal()
        {
            if (!_recoveryPending && _session?.Dirty != true) return;
            _recoveryPending = false;
            if (_session?.Dirty == true)
                _recoveryJournal.Write(_session.Draft, _session.LoadedPath, _session.BaseFileHash, out _);
        }

        private List<string> PhaseOverflowDiagnostics()
        {
            var diagnostics = new List<string>();
            if (_session?.Draft?.Layers == null) return diagnostics;
            for (int i = 0; i < _session.Draft.Layers.Count; i++)
            {
                VfxCompositionLayerView layer = _session.Draft.Layers[i];
                int overflow = LayerOverflowFrames(layer);
                if (overflow > 0) diagnostics.Add(layer.LayerId + " 超出 " + layer.Phase + " 阶段总长度 " + overflow + " 帧");
            }
            return diagnostics;
        }

        private void ToggleFavorite(VfxSourceElementView element)
        {
            if (element == null) return;
            if (!_favoriteElementIds.Add(element.ElementId)) _favoriteElementIds.Remove(element.ElementId);
            EditorPrefs.SetString("MxFramework.Vfx.Manual.Favorites", string.Join("|", _favoriteElementIds.OrderBy(value => value, StringComparer.Ordinal)));
            if (_sourceFilter.value == "已收藏") FilterElements();
            else _elementList.RefreshItems();
        }

        private void ToggleLayerEnabled(int index)
        {
            if (_session?.Draft == null || index < 0 || index >= _session.Draft.Layers.Count) return;
            _session.RecordChange();
            _session.Draft.Layers[index].Enabled = !_session.Draft.Layers[index].Enabled;
            _session.SelectedLayerIndex = index;
            SelectSourceLayer(index);
            _layerList.RefreshItems();
            RebuildPreview();
            ValidateDraft();
        }

        private void ToggleLayerSolo(int index)
        {
            if (_session?.Draft == null || index < 0 || index >= _session.Draft.Layers.Count) return;
            string layerId = _session.Draft.Layers[index].LayerId;
            _soloLayerId = string.Equals(_soloLayerId, layerId, StringComparison.Ordinal) ? string.Empty : layerId;
            _session.SelectedLayerIndex = index;
            SelectSourceLayer(index);
            _layerList.RefreshItems();
            RebuildPreview();
            ValidateDraft();
        }

        private void ToggleComparisonMode()
        {
            if (_comparisonDraft == null)
            {
                CaptureComparisonBaseline();
                return;
            }
            _abToggle.value = !_abToggle.value;
        }

        private void ClearComparison()
        {
            _comparisonDraft = null;
            _abToggle.SetValueWithoutNotify(false);
            _comparisonCanvas.style.display = DisplayStyle.None;
            _comparisonPreview.ReplaceEffectInstance(null);
            _comparisonPreview.ClearCharacterContext();
        }

        private void ShowInspectorTab(bool showLayer)
        {
            _compositionOverview.style.display = showLayer ? DisplayStyle.None : DisplayStyle.Flex;
            _layerInspector.style.display = showLayer ? DisplayStyle.Flex : DisplayStyle.None;
            _compositionTab.EnableInClassList("active", !showLayer);
            _layerInspectorTab.EnableInClassList("active", showLayer);
        }

        private void RefreshPlaybackUi()
        {
            VfxLibraryPlaybackState state = _preview.State;
            _playButton.EnableInClassList("playback-active", state == VfxLibraryPlaybackState.Playing);
            _pauseButton.EnableInClassList("playback-active", state == VfxLibraryPlaybackState.Paused);
            _stopButton.EnableInClassList("playback-active", state == VfxLibraryPlaybackState.Stopped);
            _playbackStatus.text = state == VfxLibraryPlaybackState.Playing ? "播放中" : state == VfxLibraryPlaybackState.Paused ? "已暂停" : "已停止";
            _playbackStatus.RemoveFromClassList("playing");
            _playbackStatus.RemoveFromClassList("paused");
            _playbackStatus.RemoveFromClassList("stopped");
            _playbackStatus.AddToClassList(state == VfxLibraryPlaybackState.Playing ? "playing" : state == VfxLibraryPlaybackState.Paused ? "paused" : "stopped");
            int durationFrames = TimelineWindowFrames;
            int currentFrame = Mathf.Clamp(SecondsToFrame(_preview.ElapsedSeconds), 0, durationFrames);
            _currentTime.text = "第 " + currentFrame + " / " + durationFrames + " 帧";
            RefreshLayerPlayheads(currentFrame);
        }

        private int TimelineWindowFrames => _timelineWindowFrameCount;

        internal static int SecondsToFrame(float seconds)
        {
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0f, seconds) * PreviewFramesPerSecond));
        }

        internal static float FrameToSeconds(int frame)
        {
            return frame / (float)PreviewFramesPerSecond;
        }

        private void BindLayerPlayhead(VisualElement timeline)
        {
            VisualElement playhead = timeline?.Q("playhead");
            if (playhead == null) return;
            UnityVfxLifecyclePhase current = _preview.CurrentLifecyclePhase;
            if (current != UnityVfxLifecyclePhase.None
                && timeline.userData is int layerIndex
                && layerIndex >= 0
                && layerIndex < _session.Draft.Layers.Count
                && _session.Draft.Layers[layerIndex].Phase != current.ToString())
            {
                playhead.style.display = DisplayStyle.None;
                return;
            }
            playhead.style.display = DisplayStyle.Flex;
            float percent = Mathf.Clamp01(_preview.ElapsedSeconds / Mathf.Max(0.01f, _timelineWindowSeconds)) * 100f;
            playhead.style.left = new Length(percent, LengthUnit.Percent);
        }

        private void RefreshLayerPlayheads(int currentFrame)
        {
            for (int i = 0; i < _layerTimelineElements.Count; i++)
            {
                VisualElement timeline = _layerTimelineElements[i];
                if (timeline?.panel != null) BindLayerPlayhead(timeline);
            }
            if (_layerCurrentFrame != null)
                _layerCurrentFrame.text = "F " + currentFrame.ToString("000") + " / " + TimelineWindowFrames;
        }

        private int CompositionBudget()
        {
            if (_session?.Draft == null || _catalog == null) return 0;
            int total = 0;
            for (int i = 0; i < _session.Draft.Layers.Count; i++)
                if (_session.Draft.Layers[i].Enabled)
                    total += BudgetPoints(_catalog.Find(_session.Draft.Layers[i].ElementId), _session.Draft.Layers[i]);
            return total;
        }

        private string LifecycleSummary()
        {
            if (_session?.Draft == null || _session.Draft.Layers.Count == 0) return "—";
            string[] stages = _session.Draft.Layers.Select(value => value.Phase).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
            return stages.Length == 1 ? stages[0] : string.Join(" → ", stages);
        }

        private static int BudgetPoints(VfxSourceElementView element, VfxCompositionLayerView layer = null)
        {
            if (element == null) return 0;
            float multiplier = SemanticFloat(layer, "emission.density", 1f);
            multiplier = Mathf.Max(multiplier, SemanticFloat(layer, "emission.burst.scale", 1f));
            int baseValue = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1, element.BudgetMaxParticles) / 20f), 1, 40);
            if (element.ElementKind == "CompoundGroup") baseValue += 3;
            return Mathf.Max(1, Mathf.RoundToInt(baseValue * multiplier));
        }

        private static float EstimateLayerDuration(VfxSourceElementView element, VfxCompositionLayerView layer)
        {
            return FrameToSeconds(LayerArtisticFrames(element, layer));
        }

        private static int LayerArtisticFrames(VfxSourceElementView element, VfxCompositionLayerView layer)
        {
            if (layer?.Timing?.TargetArtisticFrames > 0) return layer.Timing.TargetArtisticFrames;
            if (layer?.Timing?.SourceSegments != null) return Mathf.Max(1, layer.Timing.SourceSegments.ArtisticFrames);
            if (layer != null && layer.EstimatedSourceFrames > 0) return layer.EstimatedSourceFrames;
            string role = element?.Roles.FirstOrDefault() ?? string.Empty;
            float duration = role == "flash" ? 0.2f : role == "shockwave" ? 0.8f : role == "sparks" ? 2f : role == "smoke" ? 3.2f : role == "trail" ? 2.5f : role == "decal" ? 3f : 1.5f;
            return Mathf.Max(1, SecondsToFrame(duration));
        }

        private static float SemanticFloat(VfxCompositionLayerView layer, string propertyId, float fallback)
        {
            VfxSemanticOverrideView value = layer?.SemanticOverrides?.LastOrDefault(item => item.PropertyId == propertyId);
            return value?.FloatValue(fallback) ?? fallback;
        }

        internal static string DisplayElementName(VfxSourceElementView element)
        {
            if (element == null) return "未知 Element";
            string raw = element.DisplayName ?? string.Empty;
            if (raw.Length <= 24 && raw.IndexOf('_') < 0) return raw;
            string prefix = ThemeLabel(element.Theme);
            string role = RoleLabel(element.Roles.FirstOrDefault());
            return prefix + role + (element.ElementKind == "CompoundGroup" ? "组合" : string.Empty);
        }

        private static string RoleLabel(string role)
        {
            return CategoryLabel(string.IsNullOrWhiteSpace(role) ? "detail" : role);
        }

        private static string ThemeLabel(string theme)
        {
            return CategoryLabel(string.IsNullOrWhiteSpace(theme) ? "Generic" : theme) + "·";
        }

        internal static string CategoryLabel(string value) => VfxSourceElementChineseLabels.Format(value);
        private static string CategoryLabels(IEnumerable<string> values) => VfxSourceElementChineseLabels.FormatMany(values);

        private static HashSet<string> LoadFavoriteElementIds()
        {
            string raw = EditorPrefs.GetString("MxFramework.Vfx.Manual.Favorites", string.Empty);
            return new HashSet<string>(raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        }

        private sealed class VfxElementCardRow
        {
            public VfxElementCardRow(VfxSourceElementView left, VfxSourceElementView right) { Left = left; Right = right; }
            public VfxSourceElementView Left { get; }
            public VfxSourceElementView Right { get; }
        }

        private void SetStatus(string message, bool warning) { _diagnostics.text = message; _diagnostics.EnableInClassList("status-warning", warning); }
        private int SelectedSourceLayerIndex()
        {
            int visibleIndex = _layerList.selectedIndex;
            return visibleIndex >= 0 && visibleIndex < _visibleLayerIndices.Count ? _visibleLayerIndices[visibleIndex] : -1;
        }

        private void SelectSourceLayer(int sourceIndex)
        {
            int visibleIndex = _visibleLayerIndices.IndexOf(sourceIndex);
            if (visibleIndex >= 0) _layerList.SetSelection(visibleIndex);
            else _layerList.ClearSelection();
        }

        private VfxCompositionLayerView SelectedLayer()
        {
            int sourceIndex = SelectedSourceLayerIndex();
            return _session != null && sourceIndex >= 0 && sourceIndex < _session.Draft.Layers.Count ? _session.Draft.Layers[sourceIndex] : null;
        }
        private string UniqueLayerId(string preferred) { string value = preferred; int suffix = 2; while (_session.Draft.Layers.Any(layer => layer.LayerId == value)) value = preferred + "_" + suffix++; return value; }
        internal static string SuggestedPhase(VfxSourceElementView element, bool isFirstLayer)
        {
            if (isFirstLayer) return "Loop";
            return element.Roles.Contains("trail") || element.Roles.Contains("core") || element.Roles.Contains("arc") || element.Roles.Contains("flame") ? "Loop" : element.Roles.Contains("smoke") || element.Roles.Contains("decal") ? "Dissipate" : element.Roles.Contains("flash") ? "Build" : "Complete";
        }

        internal static string ResolveAddPhase(string selectedPhase, VfxSourceElementView element, bool isFirstLayer)
        {
            return IsSpecificLifecyclePhase(selectedPhase)
                ? selectedPhase
                : SuggestedPhase(element, isFirstLayer);
        }

        internal static string ResolvePastePhase(string selectedPhase, string copiedPhase)
        {
            return IsSpecificLifecyclePhase(selectedPhase) ? selectedPhase : copiedPhase;
        }

        private static string ExtractionLabel(VfxSourceElementView element)
        {
            if (element == null) return "未知";
            return element.ExtractionStatus switch
            {
                "ReuseExisting" => "复用 " + element.ExistingPrimitiveId,
                "PartiallyExtracted" => "部分提取",
                "VariantRequired" => "需要派生变体",
                "Stale" => "来源已过期",
                "Conflict" => "提取冲突",
                _ => "按需提取"
            };
        }

        internal static string ResolvePreviewPhase(VfxCompositionDraftView draft, string requestedPhase)
        {
            if (requestedPhase != "完整生命周期" || draft?.Layers == null) return requestedPhase;
            if (draft.Layers.Any(value => value.Enabled && value.Phase == "Loop")) return requestedPhase;
            VfxCompositionLayerView firstEnabled = draft.Layers.FirstOrDefault(value => value.Enabled);
            return firstEnabled?.Phase ?? requestedPhase;
        }
        internal static bool IsLayerVisibleForStage(VfxCompositionLayerView layer, string requestedPhase)
        {
            return layer != null
                && (string.Equals(requestedPhase, "完整生命周期", StringComparison.Ordinal)
                    || string.Equals(layer.Phase, requestedPhase, StringComparison.Ordinal));
        }
        private static bool Matches(DropdownField field, string value) => field == null || field.value == "全部" || field.value == value;
        private static bool Matches(DropdownField field, IEnumerable<string> values) => field == null || field.value == "全部" || values.Contains(field.value);
        private void ApplyCameraPreset(string preset)
        {
            _cameraMode.SetValueWithoutNotify(preset);
            _preview.SetCameraMode(preset);
            _comparisonPreview.SetCameraMode(preset);
            for (int i = 0; i < _cameraPresetButtons.Length; i++)
                _cameraPresetButtons[i].EnableInClassList("camera-preset-active", string.Equals(_cameraPresetButtons[i].text, preset, StringComparison.Ordinal));
            MarkPreviewDirty();
        }

        private void MarkPreviewDirty()
        {
            _previewCanvas.MarkDirtyRepaint();
            _comparisonCanvas.MarkDirtyRepaint();
        }

        private void BeginContinuousRefresh()
        {
            if (_liveRefreshEnabled) return;
            _liveRefreshToggle.value = true;
        }

        private void SetLiveRefreshEnabled(bool enabled)
        {
            _liveRefreshEnabled = enabled;
            EditorPrefs.SetBool(LiveRefreshPreference, enabled);
            if (!enabled)
            {
                _preview.Pause();
                _comparisonPreview.Pause();
                _elementPreview.Pause();
            }
            else
            {
                if (_previewDirty) RebuildPreview();
                _semanticInspector?.RefreshNow();
            }
            RefreshPlaybackUi();
            RefreshElementPreviewUi();
            MarkPreviewDirty();
            _elementPreviewCanvas.MarkDirtyRepaint();
        }

        private static void SetChoices(DropdownField field, IEnumerable<string> values) { field.choices = values.ToList(); field.SetValueWithoutNotify(field.choices.FirstOrDefault() ?? string.Empty); }
        private static void SetFilterChoices(DropdownField field, IEnumerable<string> values) { string current = field.value; field.choices = new[] { "全部" }.Concat(values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(CategoryLabel, StringComparer.CurrentCulture)).ToList(); field.SetValueWithoutNotify(field.choices.Contains(current) ? current : "全部"); }
        private static List<float> Values(Vector3 value) => new List<float> { value.x, value.y, value.z };
        private static List<float> Values(Color value) => new List<float> { value.r, value.g, value.b, value.a };
        private static Vector3 Vector(List<float> value, Vector3 fallback) => value != null && value.Count == 3 ? new Vector3(value[0], value[1], value[2]) : fallback;
        private static Color ColorValue(List<float> value) => value != null && value.Count == 4 ? new Color(value[0], value[1], value[2], value[3]) : Color.white;
    }
}

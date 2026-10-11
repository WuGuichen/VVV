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
        private void AddElement(VfxSourceElementView element)
        {
            if (element == null || _session == null) return;
            if (!RequireSourceIndex("添加 Layer")) return;
            ClearTemporaryElementPreview();
            _session.RecordChange();
            string role = element.Roles.FirstOrDefault() ?? "detail";
            string layerId = UniqueLayerId("layer_" + (_session.Draft.Layers.Count + 1).ToString("00") + "_" + role);
            string phase = ResolveAddPhase(_previewStage.value, element, _session.Draft.Layers.Count == 0);
            _session.Draft.Layers.Add(VfxCompositionDraftService.CreateLayerView(_catalog, element, layerId, phase));
            _session.SelectedLayerIndex = _session.Draft.Layers.Count - 1;
            RefreshDraftUi();
        }

        private void RebuildPreview()
        {
            if (_session?.Draft == null || _catalog == null) return;
            if (!_liveRefreshEnabled && _preview.HasInstance)
            {
                _previewDirty = true;
                return;
            }
            _previewDirty = false;
            RefreshTimelineScale();
            _preview.ReplaceEffectInstance(BuildPreviewRoot(_session.Draft));
            _preview.SetTimelineDuration(_timelineWindowSeconds);
            ConfigureLifecycleTimeline(_preview);
            PlaySelectedPreviewPhase(_preview);
            if (_abToggle.value && _comparisonDraft != null)
            {
                _comparisonPreview.ReplaceEffectInstance(BuildPreviewRoot(_comparisonDraft));
                _comparisonPreview.SetTimelineDuration(_timelineWindowSeconds);
                ConfigureLifecycleTimeline(_comparisonPreview);
                PlaySelectedPreviewPhase(_comparisonPreview);
            }
            else
            {
                _comparisonPreview.ReplaceEffectInstance(null);
                _comparisonPreview.ClearCharacterContext();
            }
            ApplyCharacterPreviewContext(false);
            RefreshCompositionPreviewVisibility();
            RefreshPhaseDurationUi();
            RefreshPlaybackUi();
        }

        private void PlaySelectedPreviewPhase(VfxLibraryPreviewSession preview)
        {
            if (preview == null) return;
            string phase = ResolvePreviewPhase(_session?.Draft, _previewStage.value);
            if (phase == "完整生命周期" || !Enum.TryParse(phase, false, out UnityVfxLifecyclePhase parsed))
                preview.PlayLifecyclePhase(UnityVfxLifecyclePhase.Build);
            else
                preview.PlayLifecyclePhase(parsed);
        }

        private void EnsureFullLifecyclePreview()
        {
            if (_previewStage.value == "完整生命周期") return;
            _previewStage.SetValueWithoutNotify("完整生命周期");
            RebuildPreview();
        }

        private GameObject BuildPreviewRoot(VfxCompositionDraftView draft)
        {
            var root = new GameObject("VFX Manual Composition Preview") { hideFlags = HideFlags.HideAndDontSave };
            string phase = ResolvePreviewPhase(draft, _previewStage.value);
            bool fullLifecycle = phase == "完整生命周期";
            var phaseRoots = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            var phaseFrames = new Dictionary<string, int>(StringComparer.Ordinal);
            var schedules = new List<UnityVfxBakedLayerScheduleEntry>();
            var propertyTracks = new List<UnityVfxBakedPropertyTrack>();
            using var materialSession = new VfxSemanticMaterialDerivationSession(null);
            bool hasLayer = false;
            foreach (string name in LifecyclePhases)
            {
                var child = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                child.transform.SetParent(root.transform, false);
                phaseRoots.Add(name, child);
                phaseFrames.Add(name, 0);
            }
            for (int i = 0; i < draft.Layers.Count; i++)
            {
                VfxCompositionLayerView layer = draft.Layers[i];
                if (!layer.Enabled || (!string.IsNullOrWhiteSpace(_soloLayerId) && layer.LayerId != _soloLayerId) || (!fullLifecycle && layer.Phase != phase)) continue;
                GameObject instance = VfxCompositionCandidateBaker.CreateLayer(_catalog, _catalog.Find(layer.ElementId));
                if (instance == null) continue;
                hasLayer = true;
                instance.name = layer.LayerId;
                instance.transform.SetParent(phaseRoots[layer.Phase].transform, false);
                VfxSourceElementView element = _catalog.Find(layer.ElementId);
                VfxCompiledLayerView compiled = VfxSemanticParticleSystemAdapter.CompileAndApply(
                    instance,
                    layer,
                    _catalog,
                    element,
                    _propertyRegistry,
                    apply: true,
                    allowSuggestedProfiles: true,
                    useExternalLayerScheduler: true,
                    out VfxResolvedLayerAuthoringProfile resolved);
                materialSession.Apply(resolved, layer, compiled);
                compiled.CompileHash = VfxCanonicalJson.Hash(compiled, "compileHash");
                phaseFrames[layer.Phase] = Math.Max(phaseFrames[layer.Phase], compiled.ArtisticEndFrame);
                var schedule = new UnityVfxBakedLayerScheduleEntry();
                VfxSemanticParticleSystemAdapter.ConfigureLayerSchedule(schedule, instance, layer, compiled);
                schedules.Add(schedule);
                VfxSemanticParticleSystemAdapter.AppendBakedPropertyTracks(resolved, layer, compiled, propertyTracks);
            }
            if (hasLayer)
            {
                VfxCompositionPhaseDurationOverridesView overrides = draft.PhaseDurationOverrides ?? new VfxCompositionPhaseDurationOverridesView();
                foreach (string name in LifecyclePhases)
                {
                    int? configured = overrides.Get(name);
                    if (configured.HasValue) phaseFrames[name] = configured.Value;
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
                    phaseRoots["Build"], phaseRoots["Rebuild"], phaseRoots["Loop"], phaseRoots["Complete"], phaseRoots["Dissipate"],
                    phaseFrames["Build"], phaseFrames["Rebuild"], phaseFrames["Loop"], phaseFrames["Complete"], phaseFrames["Dissipate"],
                    draft.LoopPlaybackMode == "Continuous" ? UnityVfxLoopPlaybackMode.Continuous : UnityVfxLoopPlaybackMode.OneShot);
                materialSession.TransferPreviewOwnership(root);
            }
            if (hasLayer) return root;
            Object.DestroyImmediate(root); return null;
        }

        private void CaptureComparisonBaseline()
        {
            if (_session?.Draft == null) return;
            _comparisonDraft = JsonConvert.DeserializeObject<VfxCompositionDraftView>(JsonConvert.SerializeObject(_session.Draft), VfxCompositionDraftService.JsonSettings);
            _abToggle.SetValueWithoutNotify(true); _comparisonCanvas.style.display = DisplayStyle.Flex; RebuildPreview();
            SetStatus("已将当前组合保存为 A；后续编辑显示为 B，并保持同步播放。", false);
        }

        private void BindSelectedLayer()
        {
            if (_session?.Draft == null)
            {
                _semanticInspector?.Bind(null);
                return;
            }
            _session.SelectedLayerIndex = SelectedSourceLayerIndex();
            VfxCompositionLayerView layer = SelectedLayer();
            _binding = true;
            try
            {
                bool enabled = layer != null;
                foreach (VisualElement field in new VisualElement[] { _layerStage, _layerGroup, _layerEnabled, _position, _rotation, _scale, _startOffset, _playbackSpeed, _emission, _lifetime, _size, _tint, _sortingOrder })
                    field?.SetEnabled(enabled);
                _selectedLayerTitle.text = enabled ? "Layer · " + layer.LayerId : "请选择一个 Layer";
                if (enabled)
                {
                    _layerStage.SetValueWithoutNotify(layer.Phase); _layerGroup.SetValueWithoutNotify(layer.GroupId); _layerEnabled.SetValueWithoutNotify(layer.Enabled);
                    _position.SetValueWithoutNotify(Vector(layer.Transform?.Position, Vector3.zero)); _rotation.SetValueWithoutNotify(Vector(layer.Transform?.RotationEuler, Vector3.zero)); _scale.SetValueWithoutNotify(Vector(layer.Transform?.Scale, Vector3.one));
                    _startOffset.SetValueWithoutNotify(layer.StartFrame); _playbackSpeed.SetValueWithoutNotify(layer.PlaybackSpeed); _emission.SetValueWithoutNotify(layer.EmissionScale);
                    _lifetime.SetValueWithoutNotify(layer.LifetimeScale); _size.SetValueWithoutNotify(layer.SizeScale); _tint.SetValueWithoutNotify(ColorValue(layer.ColorTint)); _sortingOrder.SetValueWithoutNotify(layer.SortingOrderOffset);
                }
            }
            finally { _binding = false; }
            _semanticInspector.Bind(layer);
            if (layer != null) ShowInspectorTab(true);
        }

        private void ChangeDraft(Action<VfxCompositionDraftView> change)
        {
            if (_binding || _session?.Draft == null) return; _session.RecordChange(); change(_session.Draft); _summaryName.text = _session.Draft.DisplayName; ValidateDraft();
        }
        private void ChangeLayer(Action<VfxCompositionLayerView> change)
        {
            if (_binding) return; VfxCompositionLayerView layer = SelectedLayer(); if (layer == null) return;
            _session.RecordChange(); change(layer); RefreshLifecycleStageView();
        }

        private static void SetLayerTransform(VfxCompositionLayerView layer, List<float> position = null, List<float> rotation = null, List<float> scale = null)
        {
            layer.Transform ??= new VfxLayerTransformView();
            if (position != null) { layer.Transform.Position = position; layer.LocalPosition = new List<float>(position); }
            if (rotation != null) { layer.Transform.RotationEuler = rotation; layer.LocalEulerAngles = new List<float>(rotation); }
            if (scale != null) { layer.Transform.Scale = scale; layer.LocalScale = new List<float>(scale); }
        }

        private void OnLayerListKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace) { RemoveLayer(); evt.StopPropagation(); return; }
            if (evt.actionKey && evt.keyCode == KeyCode.C) { CopyLayer(); evt.StopPropagation(); return; }
            if (evt.actionKey && evt.keyCode == KeyCode.V) { PasteLayer(); evt.StopPropagation(); return; }
            if (evt.actionKey && evt.keyCode == KeyCode.Z) { if (_session.Undo()) RefreshDraftUi(); evt.StopPropagation(); return; }
            if (evt.actionKey && evt.keyCode == KeyCode.Y) { if (_session.Redo()) RefreshDraftUi(); evt.StopPropagation(); return; }
            if (evt.altKey && evt.keyCode == KeyCode.UpArrow) { MoveLayer(-1); evt.StopPropagation(); return; }
            if (evt.altKey && evt.keyCode == KeyCode.DownArrow) { MoveLayer(1); evt.StopPropagation(); }
        }

        private void ShowLayerContextMenu(int index)
        {
            if (_session?.Draft == null || index < 0 || index >= _session.Draft.Layers.Count) return;
            SelectSourceLayer(index);
            VfxCompositionLayerView layer = _session.Draft.Layers[index];
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(layer.Enabled ? "隐藏 Layer" : "启用 Layer"), false, () => ToggleLayerEnabled(index));
            menu.AddItem(new GUIContent(string.Equals(_soloLayerId, layer.LayerId, StringComparison.Ordinal) ? "取消 Solo" : "Solo Layer"), false, () => ToggleLayerSolo(index));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("复制 Layer"), false, CopyLayer);
            menu.AddItem(new GUIContent("粘贴 Layer"), false, PasteLayer);
            menu.AddItem(new GUIContent("删除 Layer"), false, RemoveLayer);
            menu.AddItem(new GUIContent("复制 Layer ID"), false, () => EditorGUIUtility.systemCopyBuffer = layer.LayerId);
            menu.AddItem(new GUIContent("定位源 Prefab"), false, () => PingLayerSource(index));
            menu.ShowAsContext();
        }

        private void PingLayerSource(int index)
        {
            if (_session?.Draft == null || index < 0 || index >= _session.Draft.Layers.Count) return;
            VfxSourceElementView element = _catalog.Find(_session.Draft.Layers[index].ElementId);
            VfxSourceComponentView component = _catalog.Members(element).FirstOrDefault();
            if (component == null || string.IsNullOrWhiteSpace(component.PrefabPath)) return;
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(component.PrefabPath);
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }

        private void FrameTimeline()
        {
            RefreshTimelineScale();
            _preview.SetTimelineDuration(_timelineWindowSeconds);
            _comparisonPreview.SetTimelineDuration(_timelineWindowSeconds);
            _layerList.RefreshItems();
            RefreshPlaybackUi();
        }

        private void RefreshTimelineScale()
        {
            if (_session?.Draft != null && IsSpecificLifecyclePhase(_previewStage.value))
            {
                VfxCompositionPhaseDurationOverridesView overrides = _session.Draft.PhaseDurationOverrides;
                int? configured = overrides.Get(_previewStage.value);
                _timelineWindowFrameCount = configured
                    ?? CalculateAutomaticPhaseFrames(_session.Draft, _catalog, _previewStage.value);
                _timelineWindowSeconds = FrameToSeconds(Mathf.Max(1, _timelineWindowFrameCount));
                UpdateTimelineTickLabels();
                return;
            }
            _timelineWindowFrameCount = CalculateFullLifecyclePreviewFrames(_session?.Draft, _catalog);
            _timelineWindowSeconds = FrameToSeconds(Mathf.Max(1, _timelineWindowFrameCount));
            UpdateTimelineTickLabels();
        }

        private void ConfigureLifecycleTimeline(VfxLibraryPreviewSession preview)
        {
            bool fullLifecycle = !IsSpecificLifecyclePhase(_previewStage.value);
            preview.SetStopAtTimelineEnd(true);
            if (!fullLifecycle || _session?.Draft == null || _session.Draft.LoopPlaybackMode != "Continuous")
            {
                preview.SetLifecycleAutoExit(-1f, VfxStopMode.Complete);
                return;
            }
            int exitFrame = EffectivePhaseFrames("Build") + EffectivePhaseFrames("Loop");
            preview.SetLifecycleAutoExit(FrameToSeconds(exitFrame), VfxStopMode.Complete);
        }

        private void UpdateTimelineTickLabels()
        {
            for (int i = 0; i < _timelineTicks.Length; i++)
                if (_timelineTicks[i] != null)
                    _timelineTicks[i].text = Mathf.RoundToInt(_timelineWindowFrameCount * i / (float)(_timelineTicks.Length - 1)).ToString();
        }

        private static float NiceTimelineWindow(float required)
        {
            if (required <= 4f) return 4f;
            if (required <= 8f) return 8f;
            if (required <= 12f) return 12f;
            if (required <= 20f) return 20f;
            return Mathf.Clamp(Mathf.Ceil(required / 5f) * 5f, 25f, 30f);
        }

        private void RefreshLayerCommandState()
        {
            bool hasLayer = SelectedLayer() != null;
            int visibleIndex = _layerList.selectedIndex;
            _root.Q<Button>("layer-up").SetEnabled(hasLayer && visibleIndex > 0);
            _root.Q<Button>("layer-down").SetEnabled(hasLayer && visibleIndex + 1 < _visibleLayerIndices.Count);
            _root.Q<Button>("layer-copy").SetEnabled(hasLayer);
            Button paste = _root.Q<Button>("layer-paste");
            bool canPaste = CanPasteLayer(out string pasteMessage);
            paste.SetEnabled(canPaste);
            paste.tooltip = canPaste
                ? "粘贴到当前草稿和生命周期阶段（Ctrl+V）"
                : pasteMessage;
            _root.Q<Button>("layer-remove").SetEnabled(hasLayer);
            Button solo = _root.Q<Button>("layer-solo");
            solo.SetEnabled(hasLayer);
            solo.text = hasLayer && string.Equals(_soloLayerId, SelectedLayer().LayerId, StringComparison.Ordinal) ? "取消 Solo" : "Solo";
            _root.Q<Button>("layer-frame-timeline")?.SetEnabled(_visibleLayerIndices.Count > 0);
            _root.Q<Button>("layer-undo").SetEnabled(_session?.CanUndo == true);
            _root.Q<Button>("layer-redo").SetEnabled(_session?.CanRedo == true);
        }

        private void MoveLayer(int direction)
        {
            int visibleIndex = _layerList.selectedIndex;
            int targetVisibleIndex = visibleIndex + direction;
            if (visibleIndex < 0 || targetVisibleIndex < 0 || targetVisibleIndex >= _visibleLayerIndices.Count) return;
            ReorderLayer(_visibleLayerIndices[visibleIndex], _visibleLayerIndices[targetVisibleIndex]);
        }
        private void ReorderLayer(int sourceIndex, int targetIndex)
        {
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex >= _session.Draft.Layers.Count || targetIndex >= _session.Draft.Layers.Count || sourceIndex == targetIndex) return;
            _session.RecordChange();
            VfxCompositionLayerView layer = _session.Draft.Layers[sourceIndex];
            _session.Draft.Layers.RemoveAt(sourceIndex);
            _session.Draft.Layers.Insert(targetIndex, layer);
            _session.SelectedLayerIndex = targetIndex;
            RefreshDraftUi();
        }
        private void CopyLayer()
        {
            VfxCompositionLayerView layer = SelectedLayer();
            if (layer == null || _session?.Draft == null) return;
            VfxSourceElementView localElement = _session.Draft.LocalElements?
                .FirstOrDefault(value => string.Equals(value.ElementId, layer.ElementId, StringComparison.Ordinal));
            VfxSourceElementView resolvedElement = _catalog.Find(layer.ElementId);
            if (resolvedElement == null)
            {
                SetStatus("当前 Layer 的 Element 无法解析，不能复制：" + layer.ElementId, true);
                return;
            }
            EditorGUIUtility.systemCopyBuffer = VfxCompositionLayerClipboard.Serialize(
                layer,
                localElement,
                resolvedElement,
                _catalog.Index?.IndexHash,
                _catalog.Curation?.CurationHash);
            SetStatus("已复制 Layer：" + layer.LayerId + "。可切换草稿或生命周期后粘贴。", false);
            RefreshLayerCommandState();
        }

        private bool CanPasteLayer(out string message)
        {
            message = string.Empty;
            if (_session?.Draft == null)
            {
                message = "请先打开草稿。";
                return false;
            }
            return VfxCompositionLayerClipboard.TryDeserialize(
                    EditorGUIUtility.systemCopyBuffer,
                    out VfxCompositionLayerClipboardData data,
                    out message)
                && TryResolvePastedElement(data, out message);
        }

        private void PasteLayer()
        {
            if (_session?.Draft == null) return;
            if (!VfxCompositionLayerClipboard.TryDeserialize(
                    EditorGUIUtility.systemCopyBuffer,
                    out VfxCompositionLayerClipboardData data,
                    out string message))
            {
                SetStatus(message, true);
                return;
            }

            if (!TryResolvePastedElement(data, out message))
            {
                SetStatus(message, true);
                return;
            }

            _session.RecordChange();
            _session.Draft.LocalElements ??= new List<VfxSourceElementView>();
            if (data.LocalElement != null
                && !_session.Draft.LocalElements.Any(value => string.Equals(value.ElementId, data.LocalElement.ElementId, StringComparison.Ordinal)))
            {
                _session.Draft.LocalElements.Add(Clone(data.LocalElement));
                _catalog.SetLocalElements(_session.Draft.LocalElements);
            }

            VfxCompositionLayerView pasted = Clone(data.Layer);
            pasted.LayerId = UniqueLayerId(data.Layer.LayerId + "_copy");
            pasted.Phase = ResolvePastePhase(_previewStage.value, pasted.Phase);
            int selectedIndex = SelectedSourceLayerIndex();
            int insertIndex = selectedIndex >= 0 ? selectedIndex + 1 : _session.Draft.Layers.Count;
            _session.Draft.Layers.Insert(insertIndex, pasted);
            _session.SelectedLayerIndex = insertIndex;
            RefreshDraftUi();
            SetStatus("已粘贴 Layer：" + pasted.LayerId + " → " + pasted.Phase + "。", false);
        }

        private bool TryResolvePastedElement(VfxCompositionLayerClipboardData data, out string message)
        {
            message = string.Empty;
            if (data == null)
            {
                message = "剪贴板 VFX Layer 内容为空。";
                return false;
            }
            string elementId = data.Layer.ElementId;
            VfxSourceElementView localElement = data.LocalElement;
            if (!VfxCompositionLayerClipboard.TryValidateEvidence(data, _catalog, out _, out message)) return false;

            if (localElement == null) return true;

            if (_catalog.Elements.Any(value => string.Equals(value.ElementId, elementId, StringComparison.Ordinal)))
            {
                message = "私有 Element ID 与全局 Element 冲突，已阻止粘贴：" + elementId;
                return false;
            }

            VfxSourceElementView existing = _session.Draft.LocalElements?
                .FirstOrDefault(value => string.Equals(value.ElementId, elementId, StringComparison.Ordinal));
            if (existing == null) return true;
            string existingJson = JsonConvert.SerializeObject(existing, Formatting.None, VfxCompositionDraftService.JsonSettings);
            string incomingJson = JsonConvert.SerializeObject(localElement, Formatting.None, VfxCompositionDraftService.JsonSettings);
            if (string.Equals(existingJson, incomingJson, StringComparison.Ordinal)) return true;
            message = "目标草稿已有同 ID、但内容不同的私有 Element；为避免覆盖来源证据，已阻止粘贴：" + elementId;
            return false;
        }

        private static T Clone<T>(T value)
        {
            return JsonConvert.DeserializeObject<T>(
                JsonConvert.SerializeObject(value, Formatting.None, VfxCompositionDraftService.JsonSettings),
                VfxCompositionDraftService.JsonSettings);
        }
        private void RemoveLayer()
        {
            int index = SelectedSourceLayerIndex(); if (index < 0) return; _session.RecordChange(); _session.Draft.Layers.RemoveAt(index); _session.SelectedLayerIndex = Mathf.Min(index, _session.Draft.Layers.Count - 1); RefreshDraftUi();
        }
        private void ToggleSolo()
        {
            VfxCompositionLayerView layer = SelectedLayer(); if (layer == null) return; _soloLayerId = _soloLayerId == layer.LayerId ? string.Empty : layer.LayerId; _layerList.RefreshItems(); RebuildPreview(); ValidateDraft();
        }
    }
}

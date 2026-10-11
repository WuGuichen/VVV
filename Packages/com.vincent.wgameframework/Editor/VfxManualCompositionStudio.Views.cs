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
        private void ConfigureLists()
        {
            _elementList.itemsSource = _elementRows;
            _elementList.fixedItemHeight = 146f;
            _elementList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _elementList.makeItem = () =>
            {
                var row = new VisualElement();
                row.AddToClassList("element-card-row");
                row.Add(CreateElementCard("left-card"));
                row.Add(CreateElementCard("right-card"));
                return row;
            };
            _elementList.bindItem = (element, index) =>
            {
                VfxElementCardRow row = _elementRows[index];
                BindElementCard(element.Q("left-card"), row.Left);
                BindElementCard(element.Q("right-card"), row.Right);
            };

            _layerList.fixedItemHeight = 58f;
            _layerList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _layerList.makeItem = CreateLayerRow;
            _layerList.bindItem = (element, index) =>
            {
                int sourceIndex = _visibleLayerIndices[index];
                VfxCompositionLayerView layer = _session.Draft.Layers[sourceIndex];
                VfxSourceElementView material = _catalog?.Find(layer.ElementId);
                BindLayerRow(element, sourceIndex, layer, material);
            };
            _layerList.selectionChanged += _ =>
            {
                if (_session?.Draft == null) return;
                BindSelectedLayer();
                RememberSelectedLayer();
                RefreshLayerCommandState();
            };
            _layerList.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (DragAndDrop.GetGenericData(VfxElementDragManipulator.PayloadKey) is VfxSourceElementView) { DragAndDrop.visualMode = DragAndDropVisualMode.Copy; evt.StopPropagation(); }
            });
            _layerList.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (DragAndDrop.GetGenericData(VfxElementDragManipulator.PayloadKey) is VfxSourceElementView item) { DragAndDrop.AcceptDrag(); AddElement(item); evt.StopPropagation(); }
            });
        }

        private VisualElement CreateElementCard(string name)
        {
            var card = new VisualElement { name = name };
            card.AddToClassList("element-card");
            var image = new Image { name = "thumbnail", scaleMode = ScaleMode.ScaleAndCrop };
            image.AddToClassList("element-card-image");
            var placeholder = new Label("✦") { name = "placeholder" };
            placeholder.AddToClassList("element-card-placeholder");
            var favorite = new Button { name = "favorite", text = "☆", tooltip = "收藏仅保存在本机 EditorPrefs" };
            favorite.AddToClassList("element-card-favorite");
            var selected = new Label("✓") { name = "selected" };
            selected.AddToClassList("element-card-check");
            var title = new Label { name = "title" };
            title.AddToClassList("element-card-name");
            var chips = new VisualElement();
            chips.AddToClassList("element-card-chips");
            var role = new Label { name = "role" };
            role.AddToClassList("element-chip");
            var theme = new Label { name = "theme" };
            theme.AddToClassList("element-chip");
            chips.Add(role);
            chips.Add(theme);
            var meta = new Label { name = "meta" };
            meta.AddToClassList("element-card-meta");
            card.Add(image);
            card.Add(placeholder);
            card.Add(favorite);
            card.Add(selected);
            card.Add(title);
            card.Add(chips);
            card.Add(meta);
            favorite.RegisterCallback<ClickEvent>(evt =>
            {
                ToggleFavorite(card.userData as VfxSourceElementView);
                evt.StopPropagation();
            });
            card.RegisterCallback<ClickEvent>(evt =>
            {
                if (card.userData is not VfxSourceElementView item) return;
                SelectElement(item);
                _elementList.RefreshItems();
                if (evt.clickCount >= 2) AddElement(item);
                else OpenElementPreview(item);
            });
            card.AddManipulator(new VfxElementDragManipulator());
            card.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if (card.userData is not VfxSourceElementView item
                    || !string.Equals(item.CurationStatus, "ExplicitAuthored", StringComparison.Ordinal)) return;
                Vector2 position = card.worldBound.position;
                evt.menu.AppendAction("重命名全局素材…", _ => ShowAuthoredElementRename(item, position));
                evt.menu.AppendAction("删除全局素材…", _ => DeleteAuthoredElement(item));
            }));
            return card;
        }

        private void ShowAuthoredElementRename(VfxSourceElementView element, Vector2 position)
        {
            UnityEditor.PopupWindow.Show(new Rect(position, Vector2.zero), new VfxAuthoredElementRenamePopup(element.DisplayName, value =>
            {
                VfxAuthoredElementLibraryView library = new VfxAuthoredElementLibraryService().Load(out string loadMessage);
                if (library == null)
                {
                    SetStatus(loadMessage, true);
                    return;
                }
                if (!new VfxAuthoredElementLibraryService().Rename(element.ElementId, value, library.LibraryHash, out string message))
                {
                    SetStatus(message, true);
                    return;
                }
                ReloadCatalog();
                SelectElement(_catalog.Find(element.ElementId));
                SetStatus(message, false);
            }));
        }

        private void DeleteAuthoredElement(VfxSourceElementView element)
        {
            var service = new VfxAuthoredElementLibraryService();
            VfxAuthoredElementLibraryView library = service.Load(out string loadMessage);
            if (library == null)
            {
                SetStatus(loadMessage, true);
                return;
            }
            List<string> references = service.FindReferencingDrafts(element.ElementId);
            if (_session?.Draft?.Layers?.Any(layer => string.Equals(layer.ElementId, element.ElementId, StringComparison.Ordinal)) == true)
            {
                string current = string.IsNullOrWhiteSpace(_session.Draft.DisplayName) ? _session.Draft.DraftId : _session.Draft.DisplayName;
                if (!references.Contains(current)) references.Insert(0, current + "（当前会话）");
            }
            if (references.Count > 0)
            {
                EditorUtility.DisplayDialog("无法删除全局素材", "仍被以下草稿引用：\n" + string.Join("\n", references), "知道了");
                return;
            }
            if (!EditorUtility.DisplayDialog("删除全局素材", "确认删除“" + element.DisplayName + "”？源 Prefab 不会被修改。", "删除", "取消")) return;
            if (!service.Delete(element.ElementId, library.LibraryHash, out _, out string message))
            {
                SetStatus(message, true);
                return;
            }
            ReloadCatalog();
            SetStatus(message, false);
        }

        private void BindElementCard(VisualElement card, VfxSourceElementView item)
        {
            card.userData = item;
            card.style.display = item == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (item == null) return;
            Texture2D thumbnail = LoadThumbnail(item);
            card.Q<Image>("thumbnail").image = thumbnail;
            card.Q<Label>("placeholder").style.display = thumbnail == null ? DisplayStyle.Flex : DisplayStyle.None;
            card.Q<Label>("title").text = DisplayElementName(item);
            Label role = card.Q<Label>("role");
            role.text = RoleLabel(item.Roles.FirstOrDefault());
            Label theme = card.Q<Label>("theme");
            theme.text = ThemeLabel(item.Theme).TrimEnd('·');
            theme.ClearClassList();
            theme.AddToClassList("element-chip");
            theme.AddToClassList("theme-" + (item.Theme ?? string.Empty).ToLowerInvariant());
            card.Q<Label>("meta").text = (item.ElementKind == "CompoundGroup" ? "复合" : "单层") + " · " + CategoryLabel(item.QualityTier) + " · " + ExtractionLabel(item) + (item.EquivalentComponentIds.Count > 0 ? " · 等价 " + item.EquivalentComponentIds.Count : string.Empty);
            bool selected = ReferenceEquals(item, _selectedElement) || string.Equals(item.ElementId, _selectedElement?.ElementId, StringComparison.Ordinal);
            card.EnableInClassList("element-selected", selected);
            card.Q<Label>("selected").style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
            Button favorite = card.Q<Button>("favorite");
            bool favorited = _favoriteElementIds.Contains(item.ElementId);
            favorite.text = favorited ? "★" : "☆";
            favorite.EnableInClassList("favorited", favorited);
            card.tooltip = item.DisplayName + "\n" + item.ElementId + "\n" + CategoryLabels(item.Roles) + " · " + CategoryLabel(item.Anchor) + " · " + CategoryLabel(item.Lifecycle)
                + "\n\n单击：打开独立小窗预览\nS：添加/移除中央临时试放\nA：仅组件/与 Layer Stack 一起\n双击：正式加入当前生命周期阶段";
        }

        private VisualElement CreateLayerRow()
        {
            var row = new VisualElement();
            row.AddToClassList("layer-stack-row");
            row.tooltip = "单击选择；拖动左侧手柄排序；右键打开 Layer 操作";
            var dragHandle = new Label("⋮⋮") { name = "drag-handle", tooltip = "拖动调整 Layer 顺序" };
            dragHandle.AddToClassList("layer-drag-handle");
            var enabled = new Button { name = "enabled", tooltip = "启用或隐藏此层" };
            enabled.AddToClassList("layer-inline-button");
            var solo = new Button { name = "solo", text = "S", tooltip = "Solo 此层" };
            solo.AddToClassList("layer-inline-button");
            var image = new Image { name = "thumbnail", scaleMode = ScaleMode.ScaleAndCrop };
            image.AddToClassList("layer-thumb");
            var nameBlock = new VisualElement();
            nameBlock.AddToClassList("layer-name-block");
            var title = new Label { name = "title" };
            title.AddToClassList("layer-name");
            var subtitle = new Label { name = "subtitle" };
            subtitle.AddToClassList("layer-subtitle");
            nameBlock.Add(title);
            nameBlock.Add(subtitle);
            var role = new Label { name = "role" };
            role.AddToClassList("layer-role");
            var timeline = new VisualElement { name = "timeline" };
            timeline.AddToClassList("layer-time-host");
            _layerTimelineElements.Add(timeline);
            var bar = new VisualElement { name = "bar" };
            bar.AddToClassList("layer-time-bar");
            var timeLabel = new Label { name = "time-label" };
            timeLabel.AddToClassList("layer-time-label");
            var resize = new VisualElement { name = "time-resize", tooltip = "拖动调整生命周期" };
            resize.AddToClassList("layer-time-resize");
            bar.Add(timeLabel);
            bar.Add(resize);
            timeline.Add(bar);
            var playhead = new VisualElement { name = "playhead", pickingMode = PickingMode.Ignore };
            playhead.AddToClassList("layer-time-playhead");
            var playheadCap = new VisualElement { pickingMode = PickingMode.Ignore };
            playheadCap.AddToClassList("layer-time-playhead-cap");
            playhead.Add(playheadCap);
            timeline.Add(playhead);
            var range = new Label { name = "range" };
            range.AddToClassList("layer-range");
            var budget = new Label { name = "budget" };
            budget.AddToClassList("layer-number");
            var status = new Label { name = "status" };
            status.AddToClassList("layer-status");
            row.Add(dragHandle);
            row.Add(enabled);
            row.Add(solo);
            row.Add(image);
            row.Add(nameBlock);
            row.Add(role);
            row.Add(timeline);
            row.Add(range);
            row.Add(budget);
            row.Add(status);
            enabled.RegisterCallback<ClickEvent>(evt => { ToggleLayerEnabled((int)enabled.userData); evt.StopPropagation(); });
            solo.RegisterCallback<ClickEvent>(evt => { ToggleLayerSolo((int)solo.userData); evt.StopPropagation(); });
            row.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (DragAndDrop.GetGenericData(VfxLayerDragManipulator.PayloadKey) is not int) return;
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                row.AddToClassList("layer-drop-target");
                evt.StopPropagation();
            });
            row.RegisterCallback<DragLeaveEvent>(_ => row.RemoveFromClassList("layer-drop-target"));
            row.RegisterCallback<DragPerformEvent>(evt =>
            {
                row.RemoveFromClassList("layer-drop-target");
                if (DragAndDrop.GetGenericData(VfxLayerDragManipulator.PayloadKey) is not int sourceIndex || row.userData is not int targetIndex) return;
                DragAndDrop.AcceptDrag();
                ReorderLayer(sourceIndex, targetIndex);
                evt.StopPropagation();
            });
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1 || row.userData is not int index) return;
                ShowLayerContextMenu(index);
                evt.StopImmediatePropagation();
            });
            ConfigureLayerTimelineInteraction(timeline, resize);
            row.AddManipulator(new VfxLayerDragManipulator());
            return row;
        }

        private void BindLayerRow(VisualElement row, int index, VfxCompositionLayerView layer, VfxSourceElementView material)
        {
            Button enabled = row.Q<Button>("enabled");
            enabled.userData = index;
            enabled.text = layer.Enabled ? "●" : "○";
            enabled.EnableInClassList("inline-active", layer.Enabled);
            Button solo = row.Q<Button>("solo");
            solo.userData = index;
            solo.EnableInClassList("inline-active", string.Equals(_soloLayerId, layer.LayerId, StringComparison.Ordinal));
            row.Q<Image>("thumbnail").image = LoadThumbnail(material);
            row.Q<Label>("title").text = material == null ? layer.ElementId : DisplayElementName(material);
            row.Q<Label>("subtitle").text = layer.LayerId + " · " + layer.Phase + (string.IsNullOrWhiteSpace(layer.GroupId) ? string.Empty : " · " + layer.GroupId);
            row.Q<Label>("role").text = RoleLabel(material?.Roles.FirstOrDefault());
            VisualElement timeline = row.Q("timeline");
            timeline.userData = index;
            BindLayerTimeline(timeline, layer, material);
            row.Q<Label>("budget").text = BudgetPoints(material, layer).ToString();
            Label status = row.Q<Label>("status");
            bool warning = material == null || material.Stale || LayerOverflowFrames(layer) > 0;
            status.text = warning ? "⚠" : "●";
            status.EnableInClassList("warning", warning);
            row.EnableInClassList("layer-disabled", !layer.Enabled);
            foreach (string phase in new[] { "Build", "Rebuild", "Loop", "Complete", "Dissipate" })
                row.EnableInClassList("phase-" + phase.ToLowerInvariant(), layer.Phase == phase);
            row.userData = index;
        }

        private void BindLayerTimeline(VisualElement timeline, VfxCompositionLayerView layer, VfxSourceElementView material)
        {
            int durationFrames = LayerArtisticFrames(material, layer);
            int windowFrames = Mathf.Max(1, TimelineWindowFrames);
            int startFrame = Mathf.Max(0, layer.StartFrame);
            float startPercent = Mathf.Clamp01(startFrame / (float)windowFrames) * 100f;
            float widthPercent = Mathf.Clamp(durationFrames / (float)windowFrames * 100f, 2f, Mathf.Max(2f, 100f - startPercent));
            VisualElement bar = timeline.Q("bar");
            bar.style.left = new Length(startPercent, LengthUnit.Percent);
            bar.style.width = new Length(widthPercent, LengthUnit.Percent);
            int endFrame = startFrame + durationFrames;
            int overflowFrames = PhaseOverflowFrames(layer, endFrame);
            bar.EnableInClassList("phase-overflow", overflowFrames > 0);
            bar.Q<Label>("time-label").text = overflowFrames > 0 ? durationFrames + "f · 超 " + overflowFrames + "f" : durationFrames + "f";
            Label range = timeline.parent.Q<Label>("range");
            range.text = startFrame + "–" + endFrame;
            timeline.tooltip = "开始第 " + startFrame + " 帧 · 持续 " + durationFrames + " 帧"
                + (overflowFrames > 0 ? "\n⚠ 超出 " + layer.Phase + " 阶段总长度 " + overflowFrames + " 帧" : string.Empty)
                + "\n拖动色条移动起始帧；拖动右端调整艺术总长度并交给当前重定时模式编译；按整帧吸附";
            BindLayerPlayhead(timeline);
        }

        private void ConfigureLayerTimelineInteraction(VisualElement timeline, VisualElement resizeHandle)
        {
            bool dragging = false;
            bool resizing = false;
            bool changed = false;
            int pointerId = -1;
            float pointerStart = 0f;
            int originalStartFrame = 0;
            int originalDurationFrames = 1;

            timeline.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || timeline.userData is not int index || index < 0 || index >= _session.Draft.Layers.Count) return;
                SelectSourceLayer(index);
                VfxCompositionLayerView layer = _session.Draft.Layers[index];
                originalStartFrame = Mathf.Max(0, layer.StartFrame);
                originalDurationFrames = LayerArtisticFrames(_catalog.Find(layer.ElementId), layer);
                pointerStart = evt.position.x;
                pointerId = evt.pointerId;
                resizing = ReferenceEquals(evt.target, resizeHandle);
                dragging = true;
                changed = false;
                timeline.CapturePointer(pointerId);
                evt.StopPropagation();
            });
            timeline.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging || pointerId != evt.pointerId || timeline.userData is not int index || index < 0 || index >= _session.Draft.Layers.Count) return;
                float deltaPixels = evt.position.x - pointerStart;
                if (!changed && Mathf.Abs(deltaPixels) < 2f) return;
                if (!changed) { _session.RecordChange(); changed = true; }
                int deltaFrames = Mathf.RoundToInt(deltaPixels / Mathf.Max(1f, timeline.resolvedStyle.width) * TimelineWindowFrames);
                VfxCompositionLayerView layer = _session.Draft.Layers[index];
                if (resizing)
                {
                    int available = Mathf.Max(1, TimelineWindowFrames - originalStartFrame);
                    layer.Timing ??= new VfxLayerTimingView();
                    layer.Timing.TargetArtisticFrames = Mathf.Clamp(originalDurationFrames + deltaFrames, 1, available);
                }
                else
                {
                    layer.StartFrame = Mathf.Clamp(originalStartFrame + deltaFrames, 0, Mathf.Max(0, TimelineWindowFrames - originalDurationFrames));
                }
                BindLayerTimeline(timeline, layer, _catalog.Find(layer.ElementId));
                BindSelectedLayer();
                evt.StopPropagation();
            });
            timeline.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!dragging || pointerId != evt.pointerId) return;
                if (timeline.HasPointerCapture(pointerId)) timeline.ReleasePointer(pointerId);
                dragging = false;
                if (changed)
                {
                    RefreshTimelineScale();
                    _layerList.RefreshItems();
                    RebuildPreview();
                    ValidateDraft();
                    RefreshLayerCommandState();
                }
                evt.StopPropagation();
            });
            timeline.RegisterCallback<PointerCaptureOutEvent>(_ => dragging = false);
        }
    }

    internal sealed class VfxAuthoredElementRenamePopup : PopupWindowContent
    {
        private readonly Action<string> _commit;
        private string _value;

        internal VfxAuthoredElementRenamePopup(string value, Action<string> commit)
        {
            _value = value ?? string.Empty;
            _commit = commit;
        }

        public override Vector2 GetWindowSize() => new Vector2(300f, 72f);

        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.LabelField("重命名全局派生素材", EditorStyles.boldLabel);
            GUI.SetNextControlName("authored-element-name");
            _value = EditorGUILayout.TextField(_value);
            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_value) || _value.Trim().Length > 32);
            if (GUILayout.Button("保存") || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
            {
                _commit?.Invoke(_value.Trim());
                editorWindow.Close();
            }
            EditorGUI.EndDisabledGroup();
            if (Event.current.type == EventType.Repaint) EditorGUI.FocusTextInControl("authored-element-name");
        }
    }
}

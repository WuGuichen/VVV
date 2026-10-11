using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Combat.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Combat.Editor
{
    public sealed partial class CombatAuthoringWindow
    {
        private void OnTimelineSelectionChanged(IEnumerable<object> selected)
        {
            int selectedIndex = -1;
            foreach (object item in selected)
            {
                if (item is TimelineRow row)
                {
                    selectedIndex = _timelineRows.IndexOf(row);
                    break;
                }
            }

            SelectTimelineRow(selectedIndex);
        }

        private void OnTimelineSelectedIndicesChanged(IEnumerable<int> selectedIndices)
        {
            int selectedIndex = -1;
            foreach (int index in selectedIndices)
            {
                selectedIndex = index;
                break;
            }

            SelectTimelineRow(selectedIndex);
        }

        private void SelectTimelineRow(int index)
        {
            if (index < 0 || index >= _timelineRows.Count)
            {
                _selectedTimelineRowIndex = -1;
                ClearPersistentTimelineSelection();
                CombatAuthoringSceneState.SetSelection(default);
                RefreshDetail(null);
                RefreshTimelineStrip();
                return;
            }

            _selectedTimelineRowIndex = index;
            TimelineRow row = _timelineRows[index];
            StorePersistentTimelineSelection(row);
            CombatAuthoringSceneState.SetSelection(new CombatAuthoringSelection(row.Section, row.TrackId, row.PropertyPath));
            if (_timelineList != null && _timelineList.selectedIndex != index)
            {
                _timelineList.SetSelection(index);
            }

            RefreshDetail(row);
            RefreshTimelineStrip();
        }

        private void RefreshDetail(TimelineRow? row)
        {
            if (_detailRoot == null)
            {
                return;
            }

            _detailRoot.Clear();
            if (row == null || _actionSerialized == null)
            {
                _detailRoot.Add(new HelpBox("选择一条 timeline 行查看或轻编辑字段。", HelpBoxMessageType.Info));
                return;
            }

            SerializedProperty property = _actionSerialized.FindProperty(row.Value.PropertyPath);
            if (property == null)
            {
                _detailRoot.Add(new HelpBox("找不到 SerializedProperty：" + row.Value.PropertyPath, HelpBoxMessageType.Warning));
                return;
            }

            _detailRoot.Add(new HelpBox(row.Value.Section + " / " + row.Value.Label, HelpBoxMessageType.None));
            if (IsShapeSection(row.Value.Section))
            {
                AddShapeDetailFields(_detailRoot, property, row.Value);
            }
            else
            {
                var field = new PropertyField(property, row.Value.Label);
                field.tooltip = BuildTimelineTooltip(row.Value);
                _detailRoot.Add(field);
            }

            _detailRoot.Bind(_actionSerialized);
            RegisterSerializedRefresh(_detailRoot);
        }

        private void AddShapeDetailFields(VisualElement root, SerializedProperty property, TimelineRow row)
        {
            root.Add(new HelpBox(
                "localCenter 是 marker 本地空间中心偏移；radiusRaw/heightRaw 使用 fixed raw 比例 1,000,000 = 1 Unity unit。Capsule 的 heightRaw 为总高度，需不小于直径；heightRaw 为 0 时按旧资产默认高度预览。",
                HelpBoxMessageType.Info));
            root.Add(CreateShapeQuickActions());

            AddShapeKindField(root, property, row);
            AddMarkerField(root, property, row);
            AddFrameRangeField(root, property, row);
            AddRelativeProperty(root, property, "localCenter", "本地中心");
            AddRawShapeField(root, property, row, "radiusRaw", "半径 Raw", MinShapeRadiusRaw, MaxRadiusSliderRaw, new[]
            {
                new RawPreset("小 0.25", RawPresetSmall),
                new RawPreset("中 0.5", RawPresetMedium),
                new RawPreset("大 1.0", RawPresetLarge),
            });
            AddRawShapeField(root, property, row, "heightRaw", "Capsule 高度 Raw", 0, MaxHeightSliderRaw, new[]
            {
                new RawPreset("默认 0", 0),
                new RawPreset("0.5", RawPresetMedium),
                new RawPreset("1.0", RawPresetLarge),
                new RawPreset("2.0", RawPresetLarge * 2),
            });
            AddCapsuleHeightWarning(root, property, row);
            AddRelativeProperty(root, property, "sourceOrder", "排序");
        }

        private VisualElement CreateShapeQuickActions()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.marginTop = 4;
            root.style.marginBottom = 6;
            root.style.paddingTop = 6;
            root.style.paddingBottom = 6;
            root.style.borderTopWidth = 1;
            root.style.borderBottomWidth = 1;
            root.style.borderTopColor = UiColors.Border;
            root.style.borderBottomColor = UiColors.Border;

            var label = new Label("Shape 操作");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 10;
            label.style.color = UiColors.TextSecondary;
            label.style.marginBottom = 4;
            root.Add(label);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;

            var duplicate = CreateButton("复制 Shape", () => TryDuplicateSelectedShape());
            duplicate.tooltip = "复制当前 Shape，并为副本创建新的 trackId。";
            row.Add(duplicate);

            var delete = CreateButton("删除 Shape", () => TryDeleteSelectedShape());
            delete.tooltip = "删除当前 Shape；可用 Undo 恢复。";
            row.Add(delete);

            root.Add(row);
            return root;
        }

        private void AddShapeKindField(VisualElement root, SerializedProperty property, TimelineRow row)
        {
            SerializedProperty shapeKind = property.FindPropertyRelative("shapeKind");
            if (shapeKind == null)
            {
                root.Add(new HelpBox("缺少字段：shapeKind", HelpBoxMessageType.Warning));
                return;
            }

            var choices = new List<string>(ShapeKindValues.Length);
            for (int i = 0; i < ShapeKindValues.Length; i++)
            {
                choices.Add(FormatShapeKindChoice(ShapeKindValues[i]));
            }

            int currentIndex = Clamp(shapeKind.enumValueIndex, 0, choices.Count - 1);
            var dropdown = new DropdownField("Shape 类型", choices, currentIndex)
            {
                tooltip = Tooltip("Shape 类型"),
            };
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int selectedIndex = choices.IndexOf(evt.newValue);
                if (selectedIndex < 0)
                {
                    return;
                }

                MutateShapeProperty(property.propertyPath, "Change Combat Shape Kind", shape =>
                {
                    SerializedProperty target = shape.FindPropertyRelative("shapeKind");
                    if (target != null)
                    {
                        target.enumValueIndex = selectedIndex;
                    }
                }, row, refreshTimeline: true, refreshDetail: true);
            });
            root.Add(dropdown);
        }

        private void AddMarkerField(VisualElement root, SerializedProperty property, TimelineRow row)
        {
            SerializedProperty markerId = property.FindPropertyRelative("markerId");
            if (markerId == null)
            {
                root.Add(new HelpBox("缺少字段：markerId", HelpBoxMessageType.Warning));
                return;
            }

            string currentMarkerId = markerId.stringValue ?? string.Empty;
            List<MarkerDropdownItem> markerItems = BuildMarkerDropdownItems(currentMarkerId);
            if (markerItems.Count > 0)
            {
                var choices = new List<string>(markerItems.Count);
                int selectedIndex = 0;
                for (int i = 0; i < markerItems.Count; i++)
                {
                    choices.Add(markerItems[i].Label);
                    if (string.Equals(markerItems[i].MarkerId, currentMarkerId, StringComparison.Ordinal))
                    {
                        selectedIndex = i;
                    }
                }

                var dropdown = new DropdownField("绑定 Marker", choices, selectedIndex)
                {
                    tooltip = Tooltip("绑定 Marker"),
                };
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    int markerIndex = choices.IndexOf(evt.newValue);
                    if (markerIndex < 0 || markerIndex >= markerItems.Count || markerItems[markerIndex].IsPlaceholder)
                    {
                        return;
                    }

                    string selectedMarkerId = markerItems[markerIndex].MarkerId;
                    MutateShapeProperty(property.propertyPath, "Change Combat Shape Marker", shape =>
                    {
                        SerializedProperty target = shape.FindPropertyRelative("markerId");
                        if (target != null)
                        {
                            target.stringValue = selectedMarkerId;
                        }
                    }, row, refreshTimeline: false, refreshDetail: true);
                });
                root.Add(dropdown);
            }
            else
            {
                root.Add(new HelpBox("当前没有可选 Binding marker。请先创建或选择 Scene Binding，并从场景生成 / 重连 marker。", HelpBoxMessageType.Warning));
            }

            var advanced = new TextField("Marker Id（高级）")
            {
                value = currentMarkerId,
                tooltip = Tooltip("Marker Id（高级）"),
            };
            advanced.RegisterValueChangedCallback(evt =>
            {
                MutateShapeProperty(property.propertyPath, "Edit Combat Shape Marker Id", shape =>
                {
                    SerializedProperty target = shape.FindPropertyRelative("markerId");
                    if (target != null)
                    {
                        target.stringValue = evt.newValue ?? string.Empty;
                    }
                }, row, refreshTimeline: false, refreshDetail: false);
            });
            root.Add(advanced);

            var relink = CreateButton("使用当前选择重连", () => RelinkSelectedMarkerForShape(property.propertyPath, row));
            root.Add(relink);
            AddMarkerWarning(root, currentMarkerId);
        }

        private void AddFrameRangeField(VisualElement root, SerializedProperty property, TimelineRow row)
        {
            SerializedProperty frameRange = property.FindPropertyRelative("frameRange");
            if (frameRange == null)
            {
                root.Add(new HelpBox("缺少字段：frameRange", HelpBoxMessageType.Warning));
                return;
            }

            SerializedProperty startFrame = frameRange.FindPropertyRelative("startFrame");
            SerializedProperty endFrame = frameRange.FindPropertyRelative("endFrame");
            if (startFrame == null || endFrame == null)
            {
                root.Add(new HelpBox("缺少字段：frameRange.startFrame / endFrame", HelpBoxMessageType.Warning));
                return;
            }

            int maxFrame = GetMaxActionFrame();
            int startValue = startFrame.intValue;
            int endValue = endFrame.intValue;
            bool invalid = startValue < 0 || endValue < 0 || startValue > maxFrame || endValue > maxFrame || startValue > endValue;
            if (invalid)
            {
                root.Add(new HelpBox("当前帧范围超出 Action TotalFrames 或 start > end。使用下面控件会自动 clamp 到 0-" + maxFrame + "。", HelpBoxMessageType.Warning));
            }

            var rangeRoot = new VisualElement();
            rangeRoot.style.marginBottom = 4;
            rangeRoot.Add(CreateFrameStepperRow("Start", startValue, maxFrame, property.propertyPath, row));
            rangeRoot.Add(CreateFrameStepperRow("End", endValue, maxFrame, property.propertyPath, row));
            root.Add(rangeRoot);
        }

        private VisualElement CreateFrameStepperRow(string label, int value, int maxFrame, string shapePropertyPath, TimelineRow row)
        {
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;
            line.style.marginBottom = 3;

            var field = new IntegerField(label)
            {
                value = Clamp(value, 0, maxFrame),
                isDelayed = true,
                tooltip = Tooltip(label == "Start" ? "Frame Start" : "Frame End"),
            };
            field.style.flexGrow = 1;
            field.labelElement.style.minWidth = 42;
            field.labelElement.style.width = 42;
            field.RegisterValueChangedCallback(evt =>
            {
                SetShapeFrame(shapePropertyPath, row, label == "Start", evt.newValue);
            });
            line.Add(field);

            var minus = CreateMiniButton("-", Tooltip("Frame Step Down"), () => StepShapeFrame(shapePropertyPath, row, label == "Start", -1));
            var plus = CreateMiniButton("+", Tooltip("Frame Step Up"), () => StepShapeFrame(shapePropertyPath, row, label == "Start", 1));
            line.Add(minus);
            line.Add(plus);

            return line;
        }

        private void AddRawShapeField(VisualElement root, SerializedProperty property, TimelineRow row, string propertyName, string label, int minValue, int sliderDefaultMax, RawPreset[] presets)
        {
            SerializedProperty raw = property.FindPropertyRelative(propertyName);
            if (raw == null)
            {
                root.Add(new HelpBox("缺少字段：" + propertyName, HelpBoxMessageType.Warning));
                return;
            }

            int current = Clamp(raw.intValue, minValue, Math.Max(sliderDefaultMax, raw.intValue));
            if (raw.intValue != current)
            {
                root.Add(new HelpBox(label + " 当前值非法；下一次调整会自动修正为 " + current + "。", HelpBoxMessageType.Warning));
            }

            var unit = new Label(label + " 单位：1,000,000 raw = 1 Unity unit；当前 " + FormatRawUnit(current) + " unit");
            unit.tooltip = Tooltip(label);
            unit.style.whiteSpace = WhiteSpace.Normal;
            unit.style.marginTop = 3;
            root.Add(unit);

            int highValue = Math.Max(sliderDefaultMax, current);
            var slider = new SliderInt(label, minValue, highValue)
            {
                value = current,
                tooltip = Tooltip(label),
            };
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(evt =>
            {
                int clamped = Math.Max(minValue, evt.newValue);
                MutateShapeProperty(property.propertyPath, "Edit Combat Shape " + propertyName, shape =>
                {
                    SerializedProperty target = shape.FindPropertyRelative(propertyName);
                    if (target != null)
                    {
                        target.intValue = clamped;
                    }
                }, row, refreshTimeline: false, refreshDetail: false);
            });
            root.Add(slider);

            var presetRow = new VisualElement();
            presetRow.style.flexDirection = FlexDirection.Row;
            presetRow.style.flexWrap = Wrap.Wrap;
            for (int i = 0; i < presets.Length; i++)
            {
                RawPreset preset = presets[i];
                presetRow.Add(CreateMiniButton(preset.Label, Tooltip("Raw Preset"), () =>
                {
                    int clamped = Math.Max(minValue, preset.RawValue);
                    MutateShapeProperty(property.propertyPath, "Apply Combat Shape Raw Preset", shape =>
                    {
                        SerializedProperty target = shape.FindPropertyRelative(propertyName);
                        if (target != null)
                        {
                            target.intValue = clamped;
                        }
                    }, row, refreshTimeline: false, refreshDetail: true);
                }));
            }

            root.Add(presetRow);
        }

        private void AddCapsuleHeightWarning(VisualElement root, SerializedProperty property, TimelineRow row)
        {
            SerializedProperty shapeKind = property.FindPropertyRelative("shapeKind");
            SerializedProperty radiusRaw = property.FindPropertyRelative("radiusRaw");
            SerializedProperty heightRaw = property.FindPropertyRelative("heightRaw");
            if (shapeKind == null || radiusRaw == null || heightRaw == null)
            {
                return;
            }

            if (shapeKind.enumValueIndex == (int)CombatAuthoringShapeKind.Capsule
                && radiusRaw.intValue > 0
                && heightRaw.intValue > 0
                && (long)heightRaw.intValue < (long)radiusRaw.intValue * 2L)
            {
                root.Add(new HelpBox("Capsule 高度小于直径，预览和 validation 会提示风险。", HelpBoxMessageType.Warning));
                root.Add(CreateButton("高度=直径", () =>
                {
                    MutateShapeProperty(property.propertyPath, "Fix Combat Capsule Height", shape =>
                    {
                        SerializedProperty radius = shape.FindPropertyRelative("radiusRaw");
                        SerializedProperty height = shape.FindPropertyRelative("heightRaw");
                        if (radius != null && height != null)
                        {
                            height.intValue = Math.Max(0, radius.intValue * 2);
                        }
                    }, row, refreshTimeline: false, refreshDetail: true);
                }));
            }
        }

        private static void AddRelativeProperty(VisualElement root, SerializedProperty property, string propertyName, string label)
        {
            SerializedProperty relative = property.FindPropertyRelative(propertyName);
            if (relative == null)
            {
                root.Add(new HelpBox("缺少字段：" + propertyName, HelpBoxMessageType.Warning));
                return;
            }

            var field = new PropertyField(relative, label);
            field.tooltip = Tooltip(label);
            root.Add(field);
        }

        private void MutateShapeProperty(
            string shapePropertyPath,
            string undoName,
            Action<SerializedProperty> mutate,
            TimelineRow row,
            bool refreshTimeline,
            bool refreshDetail)
        {
            if (_actionAsset == null || _actionSerialized == null || mutate == null)
            {
                return;
            }

            _actionSerialized.Update();
            SerializedProperty shape = _actionSerialized.FindProperty(shapePropertyPath);
            if (shape == null)
            {
                return;
            }

            Undo.RecordObject(_actionAsset, undoName);
            mutate(shape);
            _actionSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_actionAsset);
            _actionSerialized.Update();
            CombatAuthoringSceneState.NotifyDataChanged();

            if (refreshTimeline)
            {
                RefreshTimeline();
                SelectTimelineRow(FindTimelineRow(row.Section, row.TrackId));
            }
            else if (refreshDetail)
            {
                RefreshDetail(row);
            }
            else
            {
                if (_timelineList != null)
                {
                    _timelineList.RefreshItems();
                }

                RefreshTimelineStrip();
            }

            RefreshValidation();
            SceneView.RepaintAll();
            Repaint();
        }

        private bool TryDeleteSelectedShape()
        {
            if (!TryGetSelectedShapeRow(out TimelineRow row))
            {
                SetQuickActionStatus("请先选择 Hitbox 或 Hurtbox。", UiColors.WarnAmber);
                return false;
            }

            if (!TryGetShapeArray(row.Section, out SerializedProperty shapes))
            {
                SetQuickActionStatus("找不到 Shape 数组：" + row.Section, UiColors.ErrorRed);
                return false;
            }

            int shapeIndex = FindShapeIndex(shapes, row);
            if (shapeIndex < 0)
            {
                SetQuickActionStatus("找不到当前选中的 Shape。", UiColors.ErrorRed);
                return false;
            }

            int nextTrackId = FindNearestShapeTrackIdAfterDelete(shapes, shapeIndex);
            Undo.RecordObject(_actionAsset, "Delete Combat Shape");
            shapes.DeleteArrayElementAtIndex(shapeIndex);
            _actionSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_actionAsset);
            _actionSerialized.Update();
            CombatAuthoringSceneState.NotifyDataChanged();

            RefreshTimeline();
            SelectTimelineRow(nextTrackId >= 0 ? FindTimelineRow(row.Section, nextTrackId) : -1);
            RefreshValidation();
            SceneView.RepaintAll();
            Repaint();
            SetQuickActionStatus("已删除 " + row.Section + " Shape；可用 Undo 恢复。", UiColors.TextSecondary);
            return true;
        }

        private bool TryDuplicateSelectedShape()
        {
            if (!TryGetSelectedShapeRow(out TimelineRow row))
            {
                SetQuickActionStatus("请先选择 Hitbox 或 Hurtbox。", UiColors.WarnAmber);
                return false;
            }

            if (!TryGetShapeArray(row.Section, out SerializedProperty shapes))
            {
                SetQuickActionStatus("找不到 Shape 数组：" + row.Section, UiColors.ErrorRed);
                return false;
            }

            int shapeIndex = FindShapeIndex(shapes, row);
            if (shapeIndex < 0)
            {
                SetQuickActionStatus("找不到当前选中的 Shape。", UiColors.ErrorRed);
                return false;
            }

            ShapeSnapshot snapshot = ShapeSnapshot.Read(shapes.GetArrayElementAtIndex(shapeIndex));
            int newTrackId = GetNextShapeTrackId(shapes);
            int newSourceOrder = GetNextShapeSourceOrder(shapes);
            int insertIndex = shapes.arraySize;

            Undo.RecordObject(_actionAsset, "Duplicate Combat Shape");
            shapes.InsertArrayElementAtIndex(insertIndex);
            ShapeSnapshot.Write(shapes.GetArrayElementAtIndex(insertIndex), snapshot, newTrackId, newSourceOrder);
            _actionSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_actionAsset);
            _actionSerialized.Update();
            CombatAuthoringSceneState.NotifyDataChanged();

            RefreshTimeline();
            SelectTimelineRow(FindTimelineRow(row.Section, newTrackId));
            RefreshValidation();
            SceneView.RepaintAll();
            Repaint();
            SetQuickActionStatus("已复制 " + row.Section + " Shape，新 trackId=" + newTrackId + "。", UiColors.TextSecondary);
            return true;
        }

        private bool TryGetSelectedShapeRow(out TimelineRow row)
        {
            if (_selectedTimelineRowIndex >= 0 && _selectedTimelineRowIndex < _timelineRows.Count)
            {
                row = _timelineRows[_selectedTimelineRowIndex];
                return IsShapeRow(row);
            }

            row = default;
            return false;
        }

        private static bool IsShapeRow(TimelineRow row)
        {
            return IsShapeSection(row.Section);
        }

        private bool TryGetShapeArray(string section, out SerializedProperty shapes)
        {
            shapes = null;
            if (_actionAsset == null)
            {
                return false;
            }

            if (_actionSerialized == null)
            {
                _actionSerialized = new SerializedObject(_actionAsset);
            }

            _actionSerialized.Update();
            string propertyRoot = GetShapePropertyRoot(section);
            shapes = string.IsNullOrEmpty(propertyRoot) ? null : _actionSerialized.FindProperty(propertyRoot);
            return shapes != null && shapes.isArray;
        }

        private static string GetShapePropertyRoot(string section)
        {
            if (string.Equals(section, "Hitbox", StringComparison.Ordinal))
            {
                return "hitboxes";
            }

            if (string.Equals(section, "Hurtbox", StringComparison.Ordinal))
            {
                return "hurtboxes";
            }

            return string.Empty;
        }

        private static int FindShapeIndex(SerializedProperty shapes, TimelineRow row)
        {
            if (shapes == null || !shapes.isArray)
            {
                return -1;
            }

            for (int i = 0; i < shapes.arraySize; i++)
            {
                SerializedProperty element = shapes.GetArrayElementAtIndex(i);
                SerializedProperty trackId = element.FindPropertyRelative("trackId");
                if (trackId != null && trackId.intValue == row.TrackId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindNearestShapeTrackIdAfterDelete(SerializedProperty shapes, int deletedIndex)
        {
            if (shapes == null || shapes.arraySize <= 1)
            {
                return -1;
            }

            int nextIndex = deletedIndex < shapes.arraySize - 1 ? deletedIndex + 1 : deletedIndex - 1;
            SerializedProperty trackId = shapes.GetArrayElementAtIndex(nextIndex).FindPropertyRelative("trackId");
            return trackId == null ? -1 : trackId.intValue;
        }

        private static int GetNextShapeSourceOrder(SerializedProperty shapes)
        {
            int maxSourceOrder = 0;
            for (int i = 0; i < shapes.arraySize; i++)
            {
                SerializedProperty element = shapes.GetArrayElementAtIndex(i);
                SerializedProperty sourceOrder = element.FindPropertyRelative("sourceOrder");
                if (sourceOrder != null)
                {
                    maxSourceOrder = Math.Max(maxSourceOrder, sourceOrder.intValue);
                }
            }

            return maxSourceOrder + 1;
        }

        private void SetQuickActionStatus(string message, Color color)
        {
            if (_quickActionStatusLabel != null)
            {
                _quickActionStatusLabel.text = message ?? string.Empty;
                _quickActionStatusLabel.style.color = color;
            }

            if (_timelineDragStatus != null && !string.IsNullOrEmpty(message))
            {
                _timelineDragStatus.text = message;
                _timelineDragStatus.style.color = color;
            }
        }

        private bool IsKeyboardEventFromEditableField(KeyDownEvent evt)
        {
            return IsEditableInputElement(evt?.target as VisualElement)
                || IsEditableInputElement(rootVisualElement?.panel?.focusController?.focusedElement as VisualElement);
        }

        private bool IsEditableFieldFocused()
        {
            return IsEditableInputElement(rootVisualElement?.panel?.focusController?.focusedElement as VisualElement);
        }

        private bool IsDetailEditableFieldFocused()
        {
            VisualElement focused = rootVisualElement?.panel?.focusController?.focusedElement as VisualElement;
            return IsDescendantOf(focused, _detailRoot) && IsEditableInputElement(focused);
        }

        private static bool IsDescendantOf(VisualElement element, VisualElement ancestor)
        {
            if (element == null || ancestor == null)
            {
                return false;
            }

            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current == ancestor)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEditableInputElement(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current is TextField
                    || current is IntegerField
                    || current is LongField
                    || current is FloatField
                    || current is DoubleField
                    || current is Slider
                    || current is SliderInt
                    || current is Vector2Field
                    || current is Vector3Field
                    || current is Vector4Field)
                {
                    return true;
                }

                string typeName = current.GetType().Name;
                if (current.focusable && typeName.EndsWith("Field", StringComparison.Ordinal))
                {
                    return true;
                }

                if (current.ClassListContains("unity-text-input")
                    || current.ClassListContains("unity-base-text-field__input")
                    || typeName.IndexOf("TextInput", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetShapeFrame(string shapePropertyPath, TimelineRow row, bool setStart, int requestedValue)
        {
            int maxFrame = GetMaxActionFrame();
            MutateShapeProperty(shapePropertyPath, "Edit Combat Shape Frame Range", shape =>
            {
                SerializedProperty frameRange = shape.FindPropertyRelative("frameRange");
                SerializedProperty start = frameRange?.FindPropertyRelative("startFrame");
                SerializedProperty end = frameRange?.FindPropertyRelative("endFrame");
                if (start == null || end == null)
                {
                    return;
                }

                int nextStart = Clamp(start.intValue, 0, maxFrame);
                int nextEnd = Clamp(end.intValue, 0, maxFrame);
                int clampedRequested = Clamp(requestedValue, 0, maxFrame);
                if (setStart)
                {
                    nextStart = clampedRequested;
                    if (nextStart > nextEnd)
                    {
                        nextEnd = nextStart;
                    }
                }
                else
                {
                    nextEnd = clampedRequested;
                    if (nextEnd < nextStart)
                    {
                        nextStart = nextEnd;
                    }
                }

                start.intValue = nextStart;
                end.intValue = nextEnd;
            }, row, refreshTimeline: true, refreshDetail: true);
        }

        private void StepShapeFrame(string shapePropertyPath, TimelineRow row, bool setStart, int delta)
        {
            if (_actionSerialized == null)
            {
                return;
            }

            _actionSerialized.Update();
            SerializedProperty shape = _actionSerialized.FindProperty(shapePropertyPath);
            SerializedProperty frameRange = shape?.FindPropertyRelative("frameRange");
            SerializedProperty target = frameRange?.FindPropertyRelative(setStart ? "startFrame" : "endFrame");
            if (target == null)
            {
                return;
            }

            SetShapeFrame(shapePropertyPath, row, setStart, target.intValue + delta);
        }

        private void RelinkSelectedMarkerForShape(string shapePropertyPath, TimelineRow row)
        {
            if (!TryRelinkSelectedTransform(out string markerId) || string.IsNullOrEmpty(markerId))
            {
                return;
            }

            MutateShapeProperty(shapePropertyPath, "Relink Combat Shape Marker", shape =>
            {
                SerializedProperty target = shape.FindPropertyRelative("markerId");
                if (target != null)
                {
                    target.stringValue = markerId;
                }
            }, row, refreshTimeline: false, refreshDetail: true);
        }

        private List<MarkerDropdownItem> BuildMarkerDropdownItems(string currentMarkerId)
        {
            var items = new List<MarkerDropdownItem>();
            if (_sceneBindingAsset == null || _sceneBindingAsset.Markers == null || _sceneBindingAsset.Markers.Length == 0)
            {
                return items;
            }

            CombatMarkerBindingData[] markers = (CombatMarkerBindingData[])_sceneBindingAsset.Markers.Clone();
            Array.Sort(markers, ComparePreviewMarkers);
            bool foundCurrent = false;
            if (string.IsNullOrEmpty(currentMarkerId))
            {
                items.Add(new MarkerDropdownItem(string.Empty, "未选择 Marker", true));
                foundCurrent = true;
            }

            for (int i = 0; i < markers.Length; i++)
            {
                CombatMarkerBindingData marker = markers[i];
                if (string.IsNullOrEmpty(marker.MarkerId))
                {
                    continue;
                }

                if (string.Equals(marker.MarkerId, currentMarkerId, StringComparison.Ordinal))
                {
                    foundCurrent = true;
                }

                items.Add(new MarkerDropdownItem(
                    marker.MarkerId,
                    FormatMarkerDropdownLabel(marker.MarkerId, marker.TargetPath),
                    false));
            }

            if (!foundCurrent)
            {
                items.Insert(0, new MarkerDropdownItem(currentMarkerId, "当前值找不到：" + currentMarkerId, true));
            }

            return items;
        }

        private void AddMarkerWarning(VisualElement root, string markerId)
        {
            if (string.IsNullOrWhiteSpace(markerId))
            {
                root.Add(new HelpBox("Marker 为空。请选择 Binding marker，或使用当前选择重连生成 marker。", HelpBoxMessageType.Warning));
                return;
            }

            if (!MarkerExistsInCurrentBinding(markerId))
            {
                root.Add(new HelpBox("找不到 Marker：" + markerId + "。请选择下拉 marker，或使用当前选择重连。", HelpBoxMessageType.Warning));
            }
        }

        private bool MarkerExistsInCurrentBinding(string markerId)
        {
            if (string.IsNullOrEmpty(markerId) || _sceneBindingAsset == null || _sceneBindingAsset.Markers == null)
            {
                return false;
            }

            CombatMarkerBindingData[] markers = _sceneBindingAsset.Markers;
            for (int i = 0; i < markers.Length; i++)
            {
                if (string.Equals(markers[i].MarkerId, markerId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetMaxActionFrame()
        {
            return Math.Max(0, _actionAsset == null ? 0 : _actionAsset.TotalFrames - 1);
        }

        private static string FormatShapeKindChoice(CombatAuthoringShapeKind shapeKind)
        {
            switch (shapeKind)
            {
                case CombatAuthoringShapeKind.Sphere:
                    return "球体 Sphere";
                case CombatAuthoringShapeKind.Capsule:
                    return "胶囊 Capsule";
                case CombatAuthoringShapeKind.Aabb:
                    return "轴对齐盒 Aabb";
                case CombatAuthoringShapeKind.Sector:
                    return "扇形 Sector";
                default:
                    return shapeKind.ToString();
            }
        }

        private static string FormatMarkerDropdownLabel(string markerId, string targetPath)
        {
            string path = string.IsNullOrEmpty(targetPath) ? "targetPath 未设置" : ShortenTargetPath(targetPath);
            return markerId + " | " + path;
        }

        private static string ShortenTargetPath(string targetPath)
        {
            const int maxLength = 44;
            if (string.IsNullOrEmpty(targetPath) || targetPath.Length <= maxLength)
            {
                return targetPath ?? string.Empty;
            }

            return "..." + targetPath.Substring(targetPath.Length - maxLength + 3);
        }

        private static string FormatRawUnit(int raw)
        {
            return (raw / (float)RawPerUnityUnit).ToString("0.###");
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static bool IsShapeSection(string section)
        {
            return string.Equals(section, "Hitbox", StringComparison.Ordinal)
                || string.Equals(section, "Hurtbox", StringComparison.Ordinal);
        }

    }
}

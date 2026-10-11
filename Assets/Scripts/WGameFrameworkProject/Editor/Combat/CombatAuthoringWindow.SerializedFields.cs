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
        private void RebuildActionFields()
        {
            if (_actionFieldsRoot == null)
            {
                return;
            }

            _actionFieldsRoot.Clear();
            if (_actionSerialized == null)
            {
                _actionFieldsRoot.Add(new HelpBox("请选择或创建 CombatActionAuthoringAsset。", HelpBoxMessageType.Info));
                return;
            }
            AddActionIntField(_actionFieldsRoot, "totalFrames", "Total Frames", 1);
            AddActionIntField(_actionFieldsRoot, "actionId", "Action Id", 1);
            AddActionTextField(_actionFieldsRoot, "schemaVersion", "Schema Version");
            AddActionFrameRangeField(_actionFieldsRoot, "startup", "Startup");
            AddActionFrameRangeField(_actionFieldsRoot, "active", "Active");
            AddActionFrameRangeField(_actionFieldsRoot, "recovery", "Recovery");
        }

        private void RebuildBindingFields()
        {
            if (_bindingFieldsRoot == null)
            {
                return;
            }

            _bindingFieldsRoot.Clear();
            if (_bindingSerialized == null)
            {
                _bindingFieldsRoot.Add(new HelpBox("Scene Binding 可选；未设置时 marker 校验会按缺失处理。", HelpBoxMessageType.Info));
                return;
            }

            AddProperty(_bindingFieldsRoot, _bindingSerialized, "sceneGuid", "Scene Guid");
            AddProperty(_bindingFieldsRoot, _bindingSerialized, "bindingProfileId", "Binding Profile Id");
            AddProperty(_bindingFieldsRoot, _bindingSerialized, "actors", "Actors");
            AddProperty(_bindingFieldsRoot, _bindingSerialized, "markers", "Markers");
            _bindingFieldsRoot.Bind(_bindingSerialized);
            RegisterSerializedRefresh(_bindingFieldsRoot);
        }

        private void AddActionIntField(VisualElement root, string propertyName, string label, int minValue)
        {
            SerializedProperty property = _actionSerialized.FindProperty(propertyName);
            if (property == null)
            {
                root.Add(new HelpBox("缺少字段：" + propertyName, HelpBoxMessageType.Warning));
                return;
            }

            var group = new VisualElement();
            group.style.marginTop = 4;
            group.style.marginBottom = 6;

            var title = new Label(label);
            title.tooltip = Tooltip(label);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 11;
            title.style.color = UiColors.TextSecondary;
            title.style.marginBottom = 2;
            group.Add(title);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexWrap = Wrap.Wrap;

            var field = new IntegerField()
            {
                value = Math.Max(minValue, property.intValue),
                isDelayed = true,
                tooltip = Tooltip(label),
            };
            ConfigureWideValueField(field);
            field.RegisterValueChangedCallback(evt =>
            {
                SetActionIntValue(propertyName, minValue, evt.newValue);
            });
            row.Add(field);
            row.Add(CreateMiniButton("-", Tooltip("Frame Step Down"), () => StepActionIntValue(propertyName, minValue, -1)));
            row.Add(CreateMiniButton("+", Tooltip("Frame Step Up"), () => StepActionIntValue(propertyName, minValue, 1)));
            if (string.Equals(propertyName, "totalFrames", StringComparison.Ordinal))
            {
                row.Add(CreateMiniButton("适配", "把 Total Frames 调整到可以包含当前 Startup / Active / Recovery / Shape / Trace 的最大结束帧。", () =>
                {
                    SetActionIntValue(propertyName, minValue, GetRequiredTotalFrames());
                }));
            }

            group.Add(row);
            root.Add(group);
        }

        private void SetActionIntValue(string propertyName, int minValue, int requestedValue)
        {
            int value = Math.Max(minValue, requestedValue);
            MutateActionProperty("Edit Combat Action " + propertyName, serialized =>
            {
                SerializedProperty target = serialized.FindProperty(propertyName);
                if (target != null)
                {
                    target.intValue = value;
                }
            });
        }

        private void StepActionIntValue(string propertyName, int minValue, int delta)
        {
            if (_actionSerialized == null)
            {
                return;
            }

            _actionSerialized.Update();
            SerializedProperty property = _actionSerialized.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            SetActionIntValue(propertyName, minValue, property.intValue + delta);
        }

        private void AddActionTextField(VisualElement root, string propertyName, string label)
        {
            SerializedProperty property = _actionSerialized.FindProperty(propertyName);
            if (property == null)
            {
                root.Add(new HelpBox("缺少字段：" + propertyName, HelpBoxMessageType.Warning));
                return;
            }

            var field = new TextField(label)
            {
                value = property.stringValue ?? string.Empty,
                isDelayed = true,
                tooltip = Tooltip(label),
            };
            ConfigureCompactField(field);
            field.RegisterValueChangedCallback(evt =>
            {
                string value = evt.newValue ?? string.Empty;
                MutateActionProperty("Edit Combat Action " + propertyName, serialized =>
                {
                    SerializedProperty target = serialized.FindProperty(propertyName);
                    if (target != null)
                    {
                        target.stringValue = value;
                    }
                });
            });
            root.Add(field);
        }

        private void AddActionFrameRangeField(VisualElement root, string propertyName, string label)
        {
            SerializedProperty range = _actionSerialized.FindProperty(propertyName);
            SerializedProperty start = range?.FindPropertyRelative("startFrame");
            SerializedProperty end = range?.FindPropertyRelative("endFrame");
            if (range == null || start == null || end == null)
            {
                root.Add(new HelpBox("缺少字段：" + propertyName + ".startFrame / endFrame", HelpBoxMessageType.Warning));
                return;
            }

            var group = new VisualElement();
            group.style.marginTop = 4;
            group.style.marginBottom = 4;

            var title = new Label(label);
            title.tooltip = Tooltip(label);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 11;
            title.style.color = UiColors.TextSecondary;
            group.Add(title);

            group.Add(CreateActionFrameRangeRow(propertyName, "Start", start.intValue, true));
            group.Add(CreateActionFrameRangeRow(propertyName, "End", end.intValue, false));
            root.Add(group);
        }

        private VisualElement CreateActionFrameRangeRow(string propertyName, string label, int value, bool setStart)
        {
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;
            line.style.flexWrap = Wrap.Wrap;
            line.style.marginBottom = 2;

            var rowLabel = new Label(label);
            rowLabel.tooltip = Tooltip(setStart ? "Frame Start" : "Frame End");
            rowLabel.style.width = 42;
            rowLabel.style.flexShrink = 0;
            rowLabel.style.color = UiColors.TextSecondary;
            line.Add(rowLabel);

            var field = new IntegerField()
            {
                value = value,
                isDelayed = true,
                tooltip = Tooltip(setStart ? "Frame Start" : "Frame End"),
            };
            ConfigureWideValueField(field);
            field.RegisterValueChangedCallback(evt =>
            {
                SetActionFrameRangeValue(propertyName, setStart, evt.newValue);
            });
            line.Add(field);

            line.Add(CreateMiniButton("-", Tooltip("Frame Step Down"), () => StepActionFrameRangeValue(propertyName, setStart, -1)));
            line.Add(CreateMiniButton("+", Tooltip("Frame Step Up"), () => StepActionFrameRangeValue(propertyName, setStart, 1)));
            return line;
        }

        private void SetActionFrameRangeValue(string propertyName, bool setStart, int requestedValue)
        {
            MutateActionProperty("Edit Combat Action Frame Range", serialized =>
            {
                SerializedProperty range = serialized.FindProperty(propertyName);
                SerializedProperty start = range?.FindPropertyRelative("startFrame");
                SerializedProperty end = range?.FindPropertyRelative("endFrame");
                if (start == null || end == null)
                {
                    return;
                }

                if (setStart)
                {
                    start.intValue = Math.Max(0, requestedValue);
                    if (!IsEmptyFrameRange(start.intValue, end.intValue) && start.intValue > end.intValue)
                    {
                        end.intValue = start.intValue;
                    }
                }
                else
                {
                    end.intValue = Math.Max(-1, requestedValue);
                    if (!IsEmptyFrameRange(start.intValue, end.intValue) && end.intValue < start.intValue)
                    {
                        start.intValue = end.intValue;
                    }
                }
            });
        }

        private void StepActionFrameRangeValue(string propertyName, bool setStart, int delta)
        {
            if (_actionSerialized == null)
            {
                return;
            }

            _actionSerialized.Update();
            SerializedProperty range = _actionSerialized.FindProperty(propertyName);
            SerializedProperty target = range?.FindPropertyRelative(setStart ? "startFrame" : "endFrame");
            if (target == null)
            {
                return;
            }

            SetActionFrameRangeValue(propertyName, setStart, target.intValue + delta);
        }

        private void MutateActionProperty(string undoName, Action<SerializedObject> mutate)
        {
            if (_actionAsset == null || _actionSerialized == null || mutate == null)
            {
                return;
            }

            _actionSerialized.Update();
            Undo.RecordObject(_actionAsset, undoName);
            mutate(_actionSerialized);
            _actionSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_actionAsset);
            _actionSerialized.Update();

            if (!IsEditableFieldFocused())
            {
                RebuildActionFields();
            }

            RefreshTimeline();
            RefreshContext();
            RefreshValidation();
            CombatAuthoringSceneState.NotifyDataChanged();
            SceneView.RepaintAll();
            Repaint();
        }

        private static bool IsEmptyFrameRange(int startFrame, int endFrame)
        {
            return endFrame < startFrame;
        }

        private static void ConfigureCompactField<T>(BaseField<T> field)
        {
            field.style.marginBottom = 3;
            field.style.minWidth = 0;
            field.labelElement.style.minWidth = 78;
            field.labelElement.style.width = 78;
            RegisterInputFocusAssist(field);
        }

        private static void ConfigureWideValueField<T>(BaseField<T> field)
        {
            field.style.minWidth = 96;
            field.style.width = 112;
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.style.marginRight = 4;
            field.style.marginBottom = 2;
            field.labelElement.style.minWidth = 0;
            field.labelElement.style.width = 0;
            field.labelElement.style.maxWidth = 0;
            field.labelElement.style.opacity = 0;
            field.labelElement.pickingMode = PickingMode.Ignore;
            RegisterInputFocusAssist(field);
        }

        private static void RegisterInputFocusAssist<T>(BaseField<T> field)
        {
            if (field == null)
            {
                return;
            }

            field.RegisterCallback<PointerDownEvent>(_ =>
            {
                field.schedule.Execute(() => FocusFieldInput(field));
            }, TrickleDown.TrickleDown);
        }

        private static void FocusFieldInput<T>(BaseField<T> field)
        {
            if (field == null || field.panel == null || !field.enabledInHierarchy)
            {
                return;
            }

            VisualElement input = field.Q<VisualElement>(className: "unity-base-text-field__input")
                ?? field.Q<VisualElement>(className: "unity-text-input");
            if (input != null && input.focusable && input.enabledInHierarchy)
            {
                input.Focus();
                return;
            }

            field.Focus();
        }

        private int GetRequiredTotalFrames()
        {
            int maxEndFrame = 0;
            if (_actionAsset == null)
            {
                return 1;
            }

            maxEndFrame = Math.Max(maxEndFrame, GetRangeEnd(_actionAsset.Startup));
            maxEndFrame = Math.Max(maxEndFrame, GetRangeEnd(_actionAsset.Active));
            maxEndFrame = Math.Max(maxEndFrame, GetRangeEnd(_actionAsset.Recovery));
            maxEndFrame = Math.Max(maxEndFrame, GetMaxShapeEndFrame(_actionAsset.Hitboxes));
            maxEndFrame = Math.Max(maxEndFrame, GetMaxShapeEndFrame(_actionAsset.Hurtboxes));
            maxEndFrame = Math.Max(maxEndFrame, GetMaxTraceEndFrame(_actionAsset.WeaponTraces));
            return Math.Max(1, maxEndFrame + 1);
        }

        private static int GetRangeEnd(CombatAuthoringFrameRange range)
        {
            return range.IsEmpty ? 0 : Math.Max(0, range.EndFrame);
        }

        private static int GetMaxShapeEndFrame(CombatShapeAuthoringData[] shapes)
        {
            int maxEndFrame = 0;
            if (shapes == null)
            {
                return maxEndFrame;
            }

            for (int i = 0; i < shapes.Length; i++)
            {
                maxEndFrame = Math.Max(maxEndFrame, GetRangeEnd(shapes[i].FrameRange));
            }

            return maxEndFrame;
        }

        private static int GetMaxTraceEndFrame(CombatWeaponTraceAuthoringData[] traces)
        {
            int maxEndFrame = 0;
            if (traces == null)
            {
                return maxEndFrame;
            }

            for (int i = 0; i < traces.Length; i++)
            {
                maxEndFrame = Math.Max(maxEndFrame, GetRangeEnd(traces[i].FrameRange));
            }

            return maxEndFrame;
        }

    }
}

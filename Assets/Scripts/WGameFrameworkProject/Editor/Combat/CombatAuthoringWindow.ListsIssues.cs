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
        private VisualElement MakeTimelineRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.RegisterCallback<PointerUpEvent>(OnTimelineListRowPointerUp, TrickleDown.TrickleDown);
            row.RegisterCallback<ClickEvent>(OnTimelineListRowClicked, TrickleDown.TrickleDown);

            var section = new Label { name = "section" };
            section.style.width = 90;
            section.style.flexShrink = 0;
            row.Add(section);

            var label = new Label { name = "label" };
            label.style.flexGrow = 1;
            label.style.minWidth = 120;
            row.Add(label);

            var range = new Label { name = "range" };
            range.style.width = 96;
            range.style.flexShrink = 0;
            row.Add(range);

            return row;
        }

        private void BindTimelineRow(VisualElement element, int index)
        {
            TimelineRow row = _timelineRows[index];
            element.userData = index;
            element.Q<Label>("section").text = row.Section;
            element.Q<Label>("label").text = row.Label;
            element.Q<Label>("range").text = FormatRange(row.FrameRange);
            element.tooltip = BuildTimelineTooltip(row);
        }

        private void OnTimelineListRowClicked(ClickEvent evt)
        {
            if (evt.currentTarget is VisualElement element && element.userData is int index)
            {
                SelectTimelineRow(index);
                evt.StopPropagation();
            }
        }

        private void OnTimelineListRowPointerUp(PointerUpEvent evt)
        {
            if (evt.currentTarget is VisualElement element && element.userData is int index)
            {
                SelectTimelineRow(index);
                evt.StopPropagation();
            }
        }

        private VisualElement MakeIssueRow()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.Center;
            root.style.paddingTop = 3;
            root.style.paddingBottom = 3;

            var title = new Label { name = "title" };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.whiteSpace = WhiteSpace.NoWrap;
            root.Add(title);

            var detail = new Label { name = "detail" };
            detail.style.whiteSpace = WhiteSpace.Normal;
            root.Add(detail);

            var actions = new VisualElement { name = "actions" };
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 3;
            root.Add(actions);

            return root;
        }

        private void BindIssueRow(VisualElement element, int index)
        {
            CombatAuthoringIssue issue = _issueRows[index];
            Label titleLabel = element.Q<Label>("title");
            titleLabel.text = IssueSeverityIcon(issue.Severity) + " " + issue.Severity + " | " + issue.SourceAsset + " | " + issue.Section + " | " + issue.Field;
            titleLabel.style.color = IssueSeverityColor(issue.Severity);
            element.Q<Label>("detail").text = issue.Message + " 建议：" + issue.SuggestedFix + " Frame=" + FormatRange(issue.FrameRange);
            VisualElement actions = element.Q<VisualElement>("actions");
            CreateIssueQuickActions(actions, issue);
            element.tooltip = "严重度：" + issue.Severity
                + "\n来源：" + issue.SourceAsset
                + "\n位置：" + issue.Section + " / " + issue.Field
                + "\n帧范围：" + FormatRange(issue.FrameRange)
                + "\n建议：" + issue.SuggestedFix;
        }

        private void CreateIssueQuickActions(VisualElement root, CombatAuthoringIssue issue)
        {
            if (root == null)
            {
                return;
            }

            root.Clear();
            if (IsFrameRangeIssue(issue))
            {
                root.Add(CreateIssueActionButton("适配总帧数", "把 Action Total Frames 扩到能包含当前最大结束帧；只修改 Authoring Asset，可用 Undo 恢复。", issue, CombatAuthoringQuickActionKind.FitTotalFrames));
                root.Add(CreateIssueActionButton("修正帧范围", "把该问题对应的帧范围 clamp 到 0 到 TotalFrames - 1，并修正 start > end；只修改 Authoring Asset，可用 Undo 恢复。", issue, CombatAuthoringQuickActionKind.ClampFrameRange));
                root.Add(CreateIssueActionButton("定位目标", "选中该问题对应的行，并同步 Inspector、Timeline 和 Scene gizmo。", issue, CombatAuthoringQuickActionKind.SelectIssueTarget));
                return;
            }

            if (IsMarkerIssue(issue))
            {
                root.Add(CreateIssueActionButton("定位目标", "选中缺失 marker 的目标行，并同步 Inspector、Timeline 和 Scene gizmo。", issue, CombatAuthoringQuickActionKind.SelectIssueTarget));
                if (TryFindIssueTimelineRow(issue) >= 0)
                {
                    root.Add(CreateIssueActionButton("使用当前选择重连", "把当前场景选择登记为 Binding marker，并写回当前 Shape / Trace 的 markerId；不会静默移动或创建 scene transform，可用 Undo 恢复。", issue, CombatAuthoringQuickActionKind.RelinkSelectedTransform));
                }
                else
                {
                    root.Add(CreateIssueActionButton("处理提示", "定位相关 asset，并提示需要在 Binding 字段中手动重连；不会静默修改 scene transform。", issue, CombatAuthoringQuickActionKind.CreatePreviewMarker));
                }

                return;
            }

            if (issue.QuickAction == CombatAuthoringQuickActionKind.SelectAsset)
            {
                root.Add(CreateIssueActionButton("定位 Asset", "在 Project 中定位当前 Action 或 Binding asset。", issue, CombatAuthoringQuickActionKind.SelectAsset));
                return;
            }

            root.Add(CreateIssueActionButton("定位目标", "选中该问题对应的行；无法映射到行时会定位相关 asset。", issue, CombatAuthoringQuickActionKind.SelectIssueTarget));
        }

        private Button CreateIssueActionButton(string text, string tooltip, CombatAuthoringIssue issue, CombatAuthoringQuickActionKind action)
        {
            var button = CreateMiniButton(text, tooltip, () => ExecuteIssueQuickAction(issue, action));
            button.style.marginRight = 4;
            return button;
        }

        private void ExecuteIssueQuickAction(CombatAuthoringIssue issue, CombatAuthoringQuickActionKind action)
        {
            switch (action)
            {
                case CombatAuthoringQuickActionKind.FitTotalFrames:
                    FitTotalFramesForIssue(issue);
                    break;
                case CombatAuthoringQuickActionKind.ClampFrameRange:
                    ClampIssueFrameRange(issue);
                    break;
                case CombatAuthoringQuickActionKind.SelectIssueTarget:
                    SelectIssueTarget(issue, showStatus: true);
                    break;
                case CombatAuthoringQuickActionKind.SelectAsset:
                    SelectIssueAsset(issue);
                    break;
                case CombatAuthoringQuickActionKind.RelinkSelectedTransform:
                    RelinkSelectedTransformForIssue(issue);
                    break;
                case CombatAuthoringQuickActionKind.CreatePreviewMarker:
                    ShowMarkerIssueHint(issue);
                    break;
                default:
                    SetQuickActionStatus("该问题暂不支持一键修复，请先定位目标后手动处理。", UiColors.WarnAmber);
                    SelectIssueTarget(issue, showStatus: false);
                    break;
            }
        }

        private void SelectIssueTarget(CombatAuthoringIssue issue, bool showStatus)
        {
            int rowIndex = TryFindIssueTimelineRow(issue);
            if (rowIndex >= 0)
            {
                SelectTimelineRow(rowIndex);
                TimelineRow row = _timelineRows[rowIndex];
                int targetFrame = row.FrameRange.IsEmpty ? CombatAuthoringSceneState.Frame : Clamp(row.FrameRange.StartFrame, 0, GetMaxActionFrame());
                CombatAuthoringSceneState.SetFrame(targetFrame);
                RefreshFrameSlider();
                SceneView.RepaintAll();
                Repaint();
                if (showStatus)
                {
                    SetQuickActionStatus("已定位到 " + row.Section + " / " + row.Label + "。", UiColors.TextSecondary);
                }

                return;
            }

            SelectIssueAsset(issue);
            if (showStatus)
            {
                SetQuickActionStatus("该问题没有对应 timeline 行，已定位相关 asset。", UiColors.WarnAmber);
            }
        }

        private void SelectIssueAsset(CombatAuthoringIssue issue)
        {
            UnityEngine.Object target = null;
            if (_actionAsset != null && string.Equals(issue.SourceAsset, _actionAsset.name, StringComparison.Ordinal))
            {
                target = _actionAsset;
            }
            else if (_sceneBindingAsset != null && string.Equals(issue.SourceAsset, _sceneBindingAsset.name, StringComparison.Ordinal))
            {
                target = _sceneBindingAsset;
            }
            else
            {
                target = _actionAsset != null ? (UnityEngine.Object)_actionAsset : _sceneBindingAsset;
            }

            PingObject(target);
        }

        private void FitTotalFramesForIssue(CombatAuthoringIssue issue)
        {
            if (_actionAsset == null || _actionSerialized == null)
            {
                SetQuickActionStatus("请先选择 Action Asset。", UiColors.WarnAmber);
                return;
            }

            int requiredFrames = GetRequiredTotalFrames();
            if (requiredFrames <= _actionAsset.TotalFrames)
            {
                SetQuickActionStatus("Total Frames 已能包含当前最大结束帧。", UiColors.TextSecondary);
                SelectIssueTarget(issue, showStatus: false);
                return;
            }

            MutateActionProperty("Fit Combat Total Frames", serialized =>
            {
                SerializedProperty totalFrames = serialized.FindProperty("totalFrames");
                if (totalFrames != null)
                {
                    totalFrames.intValue = requiredFrames;
                }
            });
            RebuildActionFields();
            SelectIssueTarget(issue, showStatus: false);
            SetQuickActionStatus("已把 Total Frames 调整为 " + requiredFrames + "；可用 Undo 恢复。", UiColors.TextSecondary);
        }

        private void ClampIssueFrameRange(CombatAuthoringIssue issue)
        {
            if (_actionAsset == null || _actionSerialized == null)
            {
                SetQuickActionStatus("请先选择 Action Asset。", UiColors.WarnAmber);
                return;
            }

            int maxFrame = GetMaxActionFrame();
            bool changed = false;
            bool found = false;

            MutateActionProperty("Clamp Combat Frame Range", serialized =>
            {
                SerializedProperty range = FindIssueFrameRangeProperty(serialized, issue);
                SerializedProperty start = range?.FindPropertyRelative("startFrame");
                SerializedProperty end = range?.FindPropertyRelative("endFrame");
                if (start == null || end == null)
                {
                    return;
                }

                found = true;
                int nextStart = Clamp(start.intValue, 0, maxFrame);
                int nextEnd = Clamp(end.intValue, 0, maxFrame);
                if (nextStart > nextEnd)
                {
                    nextEnd = nextStart;
                }

                changed = start.intValue != nextStart || end.intValue != nextEnd;
                start.intValue = nextStart;
                end.intValue = nextEnd;
            });

            RebuildActionFields();
            SelectIssueTarget(issue, showStatus: false);
            if (!found)
            {
                SetQuickActionStatus("找不到该 issue 对应的帧范围字段。", UiColors.ErrorRed);
                return;
            }

            SetQuickActionStatus(changed ? "已修正帧范围；可用 Undo 恢复。" : "帧范围已经在合法范围内。", UiColors.TextSecondary);
        }

        private void RelinkSelectedTransformForIssue(CombatAuthoringIssue issue)
        {
            if (!IsMarkerIssue(issue))
            {
                SetQuickActionStatus("该问题不是 marker 缺失问题。", UiColors.WarnAmber);
                return;
            }

            int rowIndex = TryFindIssueTimelineRow(issue);
            if (rowIndex < 0)
            {
                SelectIssueTarget(issue, showStatus: false);
                SetQuickActionStatus("请在 Binding 字段中手动重连该 marker；不会静默修改 scene transform。", UiColors.WarnAmber);
                return;
            }

            TimelineRow row = _timelineRows[rowIndex];
            if (!TryRelinkSelectedTransform(out string markerId) || string.IsNullOrEmpty(markerId))
            {
                return;
            }

            if (IsShapeSection(row.Section))
            {
                MutateShapeProperty(row.PropertyPath, "Relink Combat Shape Marker", shape =>
                {
                    SerializedProperty target = shape.FindPropertyRelative("markerId");
                    if (target != null)
                    {
                        target.stringValue = markerId;
                    }
                }, row, refreshTimeline: true, refreshDetail: true);
                SetQuickActionStatus("已用当前选择重连 " + row.Section + " marker；可用 Undo 恢复。", UiColors.TextSecondary);
                return;
            }

            if (string.Equals(row.Section, "WeaponTrace", StringComparison.Ordinal))
            {
                string fieldName = string.Equals(issue.Field, "tipMarkerId", StringComparison.Ordinal) ? "tipMarkerId" : "rootMarkerId";
                MutateActionProperty("Relink Combat WeaponTrace Marker", serialized =>
                {
                    SerializedProperty trace = serialized.FindProperty(row.PropertyPath);
                    SerializedProperty target = trace?.FindPropertyRelative(fieldName);
                    if (target != null)
                    {
                        target.stringValue = markerId;
                    }
                });
                SelectIssueTarget(issue, showStatus: false);
                SetQuickActionStatus("已用当前选择重连 WeaponTrace " + fieldName + "；可用 Undo 恢复。", UiColors.TextSecondary);
            }
        }

        private void ShowMarkerIssueHint(CombatAuthoringIssue issue)
        {
            SelectIssueTarget(issue, showStatus: false);
            SetQuickActionStatus("Marker 缺失：请在 Binding 的 Actors / Colliders / Markers 中手动重连，或先选择目标 row 后使用“使用当前选择重连”。不会静默修改 scene transform。", UiColors.WarnAmber);
        }

        private int TryFindIssueTimelineRow(CombatAuthoringIssue issue)
        {
            string section = GetTimelineSectionForIssue(issue);
            string property = GetActionPhasePropertyName(issue);
            for (int i = 0; i < _timelineRows.Count; i++)
            {
                TimelineRow row = _timelineRows[i];
                if (!string.Equals(row.Section, section, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(section, "Action", StringComparison.Ordinal))
                {
                    if (string.Equals(row.PropertyPath, property, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
                else if (row.TrackId == issue.TrackId)
                {
                    return i;
                }
            }

            return -1;
        }

        private SerializedProperty FindIssueFrameRangeProperty(SerializedObject serialized, CombatAuthoringIssue issue)
        {
            if (serialized == null)
            {
                return null;
            }

            string phaseProperty = GetActionPhasePropertyName(issue);
            if (!string.IsNullOrEmpty(phaseProperty))
            {
                return serialized.FindProperty(phaseProperty);
            }

            if (IsShapeSection(issue.Section))
            {
                SerializedProperty shapes = serialized.FindProperty(GetShapePropertyRoot(issue.Section));
                int index = FindElementIndexByTrackAndOrder(shapes, "trackId", issue.TrackId, issue.SourceOrder);
                SerializedProperty shape = index >= 0 ? shapes.GetArrayElementAtIndex(index) : null;
                return shape?.FindPropertyRelative("frameRange");
            }

            if (string.Equals(issue.Section, "WeaponTrace", StringComparison.Ordinal))
            {
                SerializedProperty traces = serialized.FindProperty("weaponTraces");
                int index = FindElementIndexByTrackAndOrder(traces, "traceId", issue.TrackId, issue.SourceOrder);
                SerializedProperty trace = index >= 0 ? traces.GetArrayElementAtIndex(index) : null;
                return trace?.FindPropertyRelative("frameRange");
            }

            return null;
        }

        private static int FindElementIndexByTrackAndOrder(SerializedProperty array, string idPropertyName, int id, int sourceOrder)
        {
            if (array == null || !array.isArray)
            {
                return -1;
            }

            int fallback = -1;
            for (int i = 0; i < array.arraySize; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                SerializedProperty trackId = element.FindPropertyRelative(idPropertyName);
                if (trackId == null || trackId.intValue != id)
                {
                    continue;
                }

                if (fallback < 0)
                {
                    fallback = i;
                }

                SerializedProperty order = element.FindPropertyRelative("sourceOrder");
                if (order != null && order.intValue == sourceOrder)
                {
                    return i;
                }
            }

            return fallback;
        }

        private static bool IsFrameRangeIssue(CombatAuthoringIssue issue)
        {
            return issue.QuickAction == CombatAuthoringQuickActionKind.ClampFrameRange
                && (string.Equals(issue.Field, "frameRange", StringComparison.Ordinal)
                    || !string.IsNullOrEmpty(GetActionPhasePropertyName(issue)));
        }

        private static bool IsMarkerIssue(CombatAuthoringIssue issue)
        {
            return issue.QuickAction == CombatAuthoringQuickActionKind.CreatePreviewMarker
                || issue.Field.IndexOf("marker", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetTimelineSectionForIssue(CombatAuthoringIssue issue)
        {
            return string.IsNullOrEmpty(GetActionPhasePropertyName(issue)) ? issue.Section : "Action";
        }

        private static string GetActionPhasePropertyName(CombatAuthoringIssue issue)
        {
            if (string.Equals(issue.Field, "startup", StringComparison.Ordinal)
                || string.Equals(issue.Section, "Startup", StringComparison.Ordinal))
            {
                return "startup";
            }

            if (string.Equals(issue.Field, "active", StringComparison.Ordinal)
                || string.Equals(issue.Section, "Active", StringComparison.Ordinal))
            {
                return "active";
            }

            if (string.Equals(issue.Field, "recovery", StringComparison.Ordinal)
                || string.Equals(issue.Section, "Recovery", StringComparison.Ordinal))
            {
                return "recovery";
            }

            return string.Empty;
        }

    }
}

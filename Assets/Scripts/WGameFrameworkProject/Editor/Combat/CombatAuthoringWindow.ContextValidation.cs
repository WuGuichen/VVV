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
        private void OnSceneStateChanged()
        {
            bool contextChanged = _actionAsset != CombatAuthoringSceneState.ActionAsset
                || _sceneBindingAsset != CombatAuthoringSceneState.SceneBindingAsset;
            bool dataChanged = _observedDataRevision != CombatAuthoringSceneState.DataRevision;
            if (contextChanged || dataChanged)
            {
                if (_isApplyingLocalSerializedRefresh && !contextChanged)
                {
                    _observedDataRevision = CombatAuthoringSceneState.DataRevision;
                }
                else
                {
                    ApplySharedContextToLocal();
                    if (HasValidSerializedTarget(_actionSerialized))
                    {
                        _actionSerialized.Update();
                    }

                    if (HasValidSerializedTarget(_bindingSerialized))
                    {
                        _bindingSerialized.Update();
                    }

                    if (contextChanged || !IsEditableFieldFocused())
                    {
                        RebuildActionFields();
                        RebuildBindingFields();
                    }

                    RefreshTimeline();
                    RefreshContext();
                    RefreshEmptyState();
                    RefreshValidation();
                    _observedDataRevision = CombatAuthoringSceneState.DataRevision;
                }
            }

            ApplySharedSelectionToLocal(true);

            int frame = CombatAuthoringSceneState.Frame;
            int maxFrame = Math.Max(0, _actionAsset == null ? 0 : _actionAsset.TotalFrames - 1);
            if (_frameSlider != null)
            {
                _frameSlider.highValue = maxFrame;
                if (_frameSlider.value != frame)
                {
                    _frameSlider.SetValueWithoutNotify(frame);
                }
            }

            if (_frameLabel != null)
            {
                _frameLabel.text = "当前帧 " + frame;
            }

            UpdateTimelinePlayhead();
            Repaint();
        }

        private void ApplySharedContextToLocal()
        {
            CombatActionAuthoringAsset sharedAction = CombatAuthoringSceneState.ActionAsset;
            CombatSceneBindingAsset sharedBinding = CombatAuthoringSceneState.SceneBindingAsset;
            if (sharedAction == null && sharedBinding == null && (_actionAsset != null || _sceneBindingAsset != null))
            {
                return;
            }

            if (_actionAsset != sharedAction)
            {
                _actionAsset = sharedAction;
                _actionSerialized = sharedAction == null ? null : new SerializedObject(sharedAction);
                if (_actionField != null && _actionField.value != sharedAction)
                {
                    _actionField.SetValueWithoutNotify(sharedAction);
                }
            }

            if (_sceneBindingAsset != sharedBinding)
            {
                _sceneBindingAsset = sharedBinding;
                _bindingSerialized = sharedBinding == null ? null : new SerializedObject(sharedBinding);
                if (_bindingField != null && _bindingField.value != sharedBinding)
                {
                    _bindingField.SetValueWithoutNotify(sharedBinding);
                }
            }
        }

        private void ApplySharedSelectionToLocal(bool refreshIfChanged)
        {
            CombatAuthoringSelection selection = CombatAuthoringSceneState.Selection;
            int index = selection.IsEmpty ? -1 : FindTimelineRow(selection.Section, selection.TrackId);
            if (index == _selectedTimelineRowIndex)
            {
                if (_timelineList != null && index >= 0 && _timelineList.selectedIndex != index)
                {
                    _timelineList.SetSelectionWithoutNotify(new[] { index });
                }

                return;
            }

            _selectedTimelineRowIndex = index;
            if (_timelineList != null)
            {
                if (index >= 0)
                {
                    _timelineList.SetSelectionWithoutNotify(new[] { index });
                }
                else
                {
                    _timelineList.SetSelectionWithoutNotify(Array.Empty<int>());
                }
            }

            if (!refreshIfChanged)
            {
                return;
            }

            if (!IsDetailEditableFieldFocused())
            {
                RefreshDetail(index >= 0 ? _timelineRows[index] : (TimelineRow?)null);
            }

            RefreshTimelineStrip();
        }

        private void RefreshValidation()
        {
            _lastReport = CombatAuthoringValidator.Validate(_actionAsset, _sceneBindingAsset);
            _lastReportText = BuildReportText(_lastReport);
            ApplyValidationReportToUi(_lastReport, _lastReportText);
        }

        private void ApplyValidationReportToUi(CombatAuthoringReport report, string reportText)
        {
            _issueRows.Clear();
            if (report != null)
            {
                for (int i = 0; i < report.IssueCount; i++)
                {
                    _issueRows.Add(report.GetIssue(i));
                }
            }

            if (_issueList != null)
            {
                _issueList.RefreshItems();
            }

            if (_reportPreview != null)
            {
                _reportPreview.value = reportText ?? string.Empty;
            }

            if (_validationLabel != null)
            {
                if (report == null || report.IssueCount == 0)
                {
                    _validationLabel.text = "\u2705 验证通过";
                    _validationLabel.style.color = UiColors.SuccessGreen;
                }
                else
                {
                    _validationLabel.text = (report.HasErrors ? "\u274C 存在错误：" : "\u26A0 存在提示：") + report.IssueCount;
                    _validationLabel.style.color = report.HasErrors ? UiColors.ErrorRed : UiColors.WarnAmber;
                }
            }
        }

        private void RefreshEmptyState()
        {
            if (_emptyStateRoot != null)
            {
                _emptyStateRoot.style.display = _actionAsset == null ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_toolbarRoot != null)
            {
                _toolbarRoot.style.display = _actionAsset == null ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void RefreshContext()
        {
            if (_contextLabel == null)
            {
                return;
            }

            string actionName = _actionAsset == null ? "Action：未选择" : "Action：" + _actionAsset.name + " / Frames " + _actionAsset.TotalFrames;
            string bindingName = _sceneBindingAsset == null ? "Binding：未选择（可选）" : "Binding：" + _sceneBindingAsset.name;
            string mode = EditorApplication.isPlaying ? "Play Mode" : "Edit Mode";
            string showcaseStatus = CombatAuthoringShowcasePlaySession.HasSession
                ? "    Showcase：" + CombatAuthoringShowcasePlaySession.LastStatus
                : string.Empty;
            _contextLabel.text = actionName + "    " + bindingName + "    " + mode + showcaseStatus;
        }

    }
}

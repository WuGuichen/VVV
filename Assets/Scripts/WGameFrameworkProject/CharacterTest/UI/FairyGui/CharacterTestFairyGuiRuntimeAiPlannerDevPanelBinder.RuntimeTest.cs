using System;
using System.Globalization;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder
    {
        private const int RuntimeTestAgentCapacity = 6;
        internal const string RuntimeTestAdvancedPopupName = "runtimeTestAdvancedPopover";
        private const float RuntimeTestAgentPopupWidth = 360f;
        private const float RuntimeTestPopupRowHeight = 44f;
        private const float RuntimeTestPopupRowStride = 52f;
        private Fgui.GComponent _runtimeTestAgentPopupHost;
        private Fgui.GGraph _runtimeTestAgentPopupBackground;
        private readonly Fgui.GButton[] _runtimeTestAgentButtons =
            new Fgui.GButton[RuntimeTestAgentCapacity];
        private Fgui.GComponent _runtimeTestAdvancedPopupHost;
        private Fgui.GGraph _runtimeTestAdvancedPopupBackground;
        private Fgui.GTextInput _runtimeTestJumpInput;
        private Fgui.GButton _runtimeTestJumpButton;
        private Fgui.GButton _runtimeTestLatestButton;
        private Fgui.GButton _runtimeTestSpeedButton;
        private Fgui.GButton _runtimeTestLoopStartButton;
        private Fgui.GButton _runtimeTestLoopEndButton;
        private Fgui.GButton _runtimeTestLoopButton;
        private Fgui.GButton _runtimeTestReplayButton;

        private void BindRuntimeTestAgentSelector(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            Fgui.GObject anchor = root?.GetChild(RuntimeFrameSlotBadge);
            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel workspace =
                viewModel?.RuntimeFrame;
            CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel[] options =
                workspace?.AgentOptions;
            bool enabled = anchor != null
                && _popupService != null
                && options != null
                && options.Length > 0;
            if (anchor == null)
                return;

            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase,
                CharacterTestFairyGuiRuntimeAiLabPanelLayoutBinder.ResolveRuntimeFrameSlotLabel(viewModel)
                    + (enabled ? " ▾" : string.Empty));
            anchor.touchable = enabled;
            anchor.alpha = enabled ? 1f : 0.65f;
            BindTooltip(
                anchor,
                enabled ? "切换当前观察的智能体" : "当前没有可选择的智能体");
            if (!enabled)
            {
                if (_runtimeTestAgentPopupHost != null
                    && _popupService?.IsOpen(_runtimeTestAgentPopupHost) == true)
                {
                    _popupService.Hide(
                        _runtimeTestAgentPopupHost,
                        MxFairyGuiPopupCloseReason.OwnerClosed);
                }
                return;
            }

            EnsureRuntimeTestAgentPopup();
            RenderRuntimeTestAgentPopup(options);
            anchor.onClick.Set(() =>
            {
                if (_popupService.IsOpen(_runtimeTestAgentPopupHost))
                {
                    _popupService.Hide(
                        _runtimeTestAgentPopupHost,
                        MxFairyGuiPopupCloseReason.Toggle);
                    return;
                }
                _popupService.Show(
                    _viewId,
                    _runtimeTestAgentPopupHost,
                    anchor,
                    new MxFairyGuiPopupOptions(
                        MxFairyGuiPopupPlacement.Below,
                        safeMargin: 16f,
                        anchorGap: 8f));
            });
        }

        private void EnsureRuntimeTestAgentPopup()
        {
            if (_runtimeTestAgentPopupHost != null)
                return;
            _runtimeTestAgentPopupHost = new Fgui.GComponent
            {
                name = "runtimeTestAgentPopover",
                opaque = true
            };
            _runtimeTestAgentPopupBackground = new Fgui.GGraph
            {
                name = "background",
                touchable = false
            };
            _runtimeTestAgentPopupHost.AddChild(_runtimeTestAgentPopupBackground);
            for (int i = 0; i < _runtimeTestAgentButtons.Length; i++)
            {
                Fgui.GButton button = CreateRuntimeTestPopupButton(
                    "runtimeTestAgent" + i);
                _runtimeTestAgentButtons[i] = button;
                _runtimeTestAgentPopupHost.AddChild(button);
            }
        }

        private void RenderRuntimeTestAgentPopup(
            CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel[] options)
        {
            int count = Math.Min(options?.Length ?? 0, _runtimeTestAgentButtons.Length);
            float height = 16f + count * RuntimeTestPopupRowStride;
            _runtimeTestAgentPopupHost.SetSize(RuntimeTestAgentPopupWidth, height);
            DrawRuntimeTestPopupBackground(
                _runtimeTestAgentPopupBackground,
                RuntimeTestAgentPopupWidth,
                height);
            for (int i = 0; i < _runtimeTestAgentButtons.Length; i++)
            {
                Fgui.GButton button = _runtimeTestAgentButtons[i];
                bool visible = i < count;
                button.visible = visible;
                if (!visible)
                    continue;
                CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel option = options[i];
                button.SetXY(8f, 8f + i * RuntimeTestPopupRowStride);
                button.SetSize(
                    RuntimeTestAgentPopupWidth - 16f,
                    RuntimeTestPopupRowHeight);
                button.text = (option.Selected ? "✓  " : "    ")
                    + option.SlotLabel
                    + "  ·  "
                    + option.DisplayName;
                button.grayed = false;
                button.enabled = true;
                int agentIndex = option.AgentIndex;
                button.onClick.Set(() =>
                {
                    _popupService?.Hide(
                        _runtimeTestAgentPopupHost,
                        MxFairyGuiPopupCloseReason.Toggle);
                    _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                            .RuntimeTestSelectAgent,
                        agentIndex));
                });
            }
        }

        internal void BindRuntimeTestTimelineMore(
            Fgui.GComponent timeline,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel workspace)
        {
            Fgui.GButton anchor = timeline?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineMore)?.asButton;
            if (anchor == null)
                return;
            bool enabled = _popupService != null && workspace != null && workspace.Available;
            anchor.text = "…";
            anchor.mode = Fgui.ButtonMode.Common;
            anchor.selected = false;
            anchor.enabled = true;
            anchor.grayed = false;
            anchor.touchable = enabled;
            anchor.alpha = enabled ? 1f : 0.45f;
            BindTooltip(anchor, "更多时间轴操作");
            if (!enabled)
                return;

            EnsureRuntimeTestAdvancedPopup();
            RenderRuntimeTestAdvancedPopup(viewModel, workspace);
            anchor.onClick.Set(() =>
            {
                if (_popupService.IsOpen(_runtimeTestAdvancedPopupHost))
                {
                    _popupService.Hide(
                        _runtimeTestAdvancedPopupHost,
                        MxFairyGuiPopupCloseReason.Toggle);
                    return;
                }
                _popupService.Show(
                    _viewId,
                    _runtimeTestAdvancedPopupHost,
                    anchor,
                    new MxFairyGuiPopupOptions(
                        MxFairyGuiPopupPlacement.Above,
                        safeMargin: 16f,
                        anchorGap: 8f));
            });
        }

        private void EnsureRuntimeTestAdvancedPopup()
        {
            if (_runtimeTestAdvancedPopupHost != null)
                return;
            const float width = 360f;
            const float height = 390f;
            _runtimeTestAdvancedPopupHost = new Fgui.GComponent
            {
                name = RuntimeTestAdvancedPopupName,
                opaque = true
            };
            _runtimeTestAdvancedPopupHost.SetSize(width, height);
            _runtimeTestAdvancedPopupBackground = new Fgui.GGraph
            {
                name = "background",
                touchable = false
            };
            _runtimeTestAdvancedPopupHost.AddChild(_runtimeTestAdvancedPopupBackground);
            DrawRuntimeTestPopupBackground(
                _runtimeTestAdvancedPopupBackground,
                width,
                height);

            var title = new Fgui.GTextField
            {
                name = "title",
                text = "时间轴操作",
                color = RuntimeFrameSrgb(225, 233, 238),
                touchable = false
            };
            title.SetXY(16f, 14f);
            title.SetSize(328f, 30f);
            SetTimelineFontSize(title, 18);
            _runtimeTestAdvancedPopupHost.AddChild(title);

            var inputBackground = new Fgui.GGraph
            {
                name = "jumpInputBackground",
                touchable = false
            };
            inputBackground.SetXY(16f, 52f);
            inputBackground.SetSize(196f, 42f);
            inputBackground.DrawRect(
                196f,
                42f,
                1,
                RuntimeFrameSrgb(57, 83, 98),
                RuntimeFrameSrgb(7, 18, 27));
            _runtimeTestAdvancedPopupHost.AddChild(inputBackground);
            _runtimeTestJumpInput = new Fgui.GTextInput
            {
                name = "jumpFrameInput",
                color = RuntimeFrameSrgb(225, 233, 238),
                promptText = "帧号"
            };
            _runtimeTestJumpInput.SetXY(28f, 58f);
            _runtimeTestJumpInput.SetSize(172f, 30f);
            SetTimelineFontSize(_runtimeTestJumpInput, 17);
            _runtimeTestAdvancedPopupHost.AddChild(_runtimeTestJumpInput);

            _runtimeTestJumpButton = AddRuntimeTestPopupButton(
                "jump",
                220f,
                52f,
                124f,
                42f);
            _runtimeTestLatestButton = AddRuntimeTestPopupButton(
                "latest",
                16f,
                104f,
                328f,
                42f);
            _runtimeTestSpeedButton = AddRuntimeTestPopupButton(
                "speed",
                16f,
                156f,
                328f,
                42f);
            _runtimeTestLoopStartButton = AddRuntimeTestPopupButton(
                "loopStart",
                16f,
                208f,
                158f,
                42f);
            _runtimeTestLoopEndButton = AddRuntimeTestPopupButton(
                "loopEnd",
                186f,
                208f,
                158f,
                42f);
            _runtimeTestLoopButton = AddRuntimeTestPopupButton(
                "loop",
                16f,
                260f,
                328f,
                42f);
            _runtimeTestReplayButton = AddRuntimeTestPopupButton(
                "replay",
                16f,
                320f,
                328f,
                50f);
        }

        private Fgui.GButton AddRuntimeTestPopupButton(
            string name,
            float x,
            float y,
            float width,
            float height)
        {
            Fgui.GButton button = CreateRuntimeTestPopupButton(name);
            button.SetXY(x, y);
            button.SetSize(width, height);
            _runtimeTestAdvancedPopupHost.AddChild(button);
            return button;
        }

        private static Fgui.GButton CreateRuntimeTestPopupButton(string name)
        {
            Fgui.GButton button = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "CompactNeutralButton")?.asButton;
            button ??= new Fgui.GButton();
            button.name = name;
            button.mode = Fgui.ButtonMode.Common;
            return button;
        }

        private void RenderRuntimeTestAdvancedPopup(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel workspace)
        {
            if (!Application.isPlaying || !_runtimeTestJumpInput.focused)
                _runtimeTestJumpInput.text = workspace.JumpFrameInputText;
            _runtimeTestJumpButton.text = "跳转到帧";
            _runtimeTestJumpButton.enabled = workspace.CanJumpFrame;
            _runtimeTestJumpButton.grayed = !workspace.CanJumpFrame;
            _runtimeTestJumpButton.onClick.Set(() =>
            {
                string value = _runtimeTestJumpInput.text ?? string.Empty;
                object payload = int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int frame)
                        ? (object)frame
                        : value;
                EnqueueRuntimeTestPopupCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.JumpFrame,
                    payload);
            });
            BindRuntimeTestAdvancedButton(
                _runtimeTestLatestButton,
                workspace.SelectedFrame < workspace.LatestFrame,
                "定位最新帧",
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LatestFrame);
            BindRuntimeTestAdvancedButton(
                _runtimeTestSpeedButton,
                viewModel.CanChangeSpeed,
                viewModel.SpeedLabel,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PlaybackSpeed);
            BindRuntimeTestAdvancedButton(
                _runtimeTestLoopStartButton,
                workspace.CanSetLoopRange,
                viewModel.LoopStartLabel,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopStart);
            BindRuntimeTestAdvancedButton(
                _runtimeTestLoopEndButton,
                workspace.CanSetLoopRange,
                viewModel.LoopEndLabel,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopEnd);
            BindRuntimeTestAdvancedButton(
                _runtimeTestLoopButton,
                workspace.CanSetLoop,
                viewModel.ToggleLoopLabel,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleLoop);
            BindRuntimeTestAdvancedButton(
                _runtimeTestReplayButton,
                workspace.Available,
                "Replay / 校验",
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsReplayTab);
        }

        private void BindRuntimeTestAdvancedButton(
            Fgui.GButton button,
            bool enabled,
            string text,
            string commandId)
        {
            button.text = text ?? string.Empty;
            button.enabled = enabled;
            button.grayed = !enabled;
            button.onClick.Set(() =>
            {
                if (enabled)
                    EnqueueRuntimeTestPopupCommand(commandId, null);
            });
        }

        private void EnqueueRuntimeTestPopupCommand(string commandId, object payload)
        {
            _popupService?.Hide(
                _runtimeTestAdvancedPopupHost,
                MxFairyGuiPopupCloseReason.Toggle);
            _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, payload));
        }

        private static void DrawRuntimeTestPopupBackground(
            Fgui.GGraph background,
            float width,
            float height)
        {
            background.SetXY(0f, 0f);
            background.SetSize(width, height);
            background.DrawRect(
                width,
                height,
                1,
                RuntimeFrameSrgb(55, 79, 93),
                RuntimeFrameSrgb(8, 20, 29, 250));
        }

        internal static CharacterTestRuntimeAiLabTimelineItem FindRuntimeTestCausalItem(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            int stageIndex)
        {
            CharacterTestRuntimeAiLabTimelineProjection projection =
                viewModel?.TimelineProjection;
            if (projection == null)
                return null;
            CharacterTestRuntimeAiLabTimelineTrackKind kind =
                stageIndex == 0
                    ? CharacterTestRuntimeAiLabTimelineTrackKind.Perception
                    : stageIndex == 1
                        ? CharacterTestRuntimeAiLabTimelineTrackKind.Decision
                        : stageIndex == 2
                            ? CharacterTestRuntimeAiLabTimelineTrackKind.Action
                            : CharacterTestRuntimeAiLabTimelineTrackKind.Result;
            int selectedFrame = projection.Domain.SelectedFrame;
            int selectedAgent = viewModel.RuntimeFrame?.AgentEntityId ?? 0;
            for (int trackIndex = 0; trackIndex < projection.Tracks.Count; trackIndex++)
            {
                CharacterTestRuntimeAiLabTimelineTrackProjection track =
                    projection.Tracks[trackIndex];
                if (track.Kind != kind)
                    continue;
                for (int itemIndex = track.Items.Count - 1; itemIndex >= 0; itemIndex--)
                {
                    CharacterTestRuntimeAiLabTimelineItem match =
                        FindRuntimeTestCausalLeaf(
                            track.Items[itemIndex],
                            selectedFrame,
                            selectedAgent,
                            requireOverlap: true);
                    if (match != null)
                        return match;
                }
                for (int itemIndex = track.Items.Count - 1; itemIndex >= 0; itemIndex--)
                {
                    CharacterTestRuntimeAiLabTimelineItem match =
                        FindRuntimeTestCausalLeaf(
                            track.Items[itemIndex],
                            selectedFrame,
                            selectedAgent,
                            requireOverlap: false);
                    if (match != null)
                        return match;
                }
                return null;
            }
            return null;
        }

        private static CharacterTestRuntimeAiLabTimelineItem FindRuntimeTestCausalLeaf(
            CharacterTestRuntimeAiLabTimelineItem item,
            int selectedFrame,
            int selectedAgent,
            bool requireOverlap)
        {
            if (item == null
                || item.StartFrame > selectedFrame
                || (requireOverlap && item.EndFrame < selectedFrame))
            {
                return null;
            }
            for (int i = item.Members.Count - 1; i >= 0; i--)
            {
                CharacterTestRuntimeAiLabTimelineItem member =
                    FindRuntimeTestCausalLeaf(
                        item.Members[i],
                        selectedFrame,
                        selectedAgent,
                        requireOverlap);
                if (member != null)
                    return member;
            }
            return selectedAgent <= 0
                || item.AgentEntityId <= 0
                || item.AgentEntityId == selectedAgent
                    ? item
                    : null;
        }

        internal void SelectRuntimeTestCausalItem(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            int stageIndex)
        {
            CharacterTestRuntimeAiLabTimelineItem item =
                FindRuntimeTestCausalItem(viewModel, stageIndex);
            if (item == null)
                return;
            _commandSink?.Enqueue(new MxUiCommand(
                _viewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineSelectItem,
                item.CreateLocator()));
        }

        internal static CharacterTestRuntimeAiLabFactsStatus ResolveRuntimeTestCausalStatus(
            CharacterTestRuntimeAiLabTimelineItem item)
        {
            if (item == null)
                return CharacterTestRuntimeAiLabFactsStatus.Neutral;
            if (item.Severity == CharacterTestRuntimeAiLabTimelineSeverity.Error
                || item.CommandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected
                || item.PlannerLogicKind == CharacterTestRuntimeAiLabPlannerLogicCommitKind.Rejected
                || item.RuntimeEffectKind == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Failed)
            {
                return CharacterTestRuntimeAiLabFactsStatus.Error;
            }
            if (item.CommandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued
                && item.RuntimeEffectKind
                    == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved)
            {
                return CharacterTestRuntimeAiLabFactsStatus.Warning;
            }
            if (item.Severity == CharacterTestRuntimeAiLabTimelineSeverity.Warning
                || item.RuntimeEffectKind
                    == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Cancelled)
            {
                return CharacterTestRuntimeAiLabFactsStatus.Warning;
            }
            if (item.Severity == CharacterTestRuntimeAiLabTimelineSeverity.Success
                || item.RuntimeEffectKind
                    == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Started
                || item.RuntimeEffectKind
                    == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Running
                || item.RuntimeEffectKind
                    == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Completed)
            {
                return CharacterTestRuntimeAiLabFactsStatus.Success;
            }
            return CharacterTestRuntimeAiLabFactsStatus.Active;
        }

        internal static void ResolveRuntimeTestCausalTone(
            CharacterTestRuntimeAiLabFactsStatus status,
            out Color border,
            out Color fill)
        {
            switch (status)
            {
                case CharacterTestRuntimeAiLabFactsStatus.Success:
                    border = RuntimeFrameSrgb(76, 180, 112, 235);
                    fill = RuntimeFrameSrgb(10, 38, 31, 236);
                    return;
                case CharacterTestRuntimeAiLabFactsStatus.Warning:
                    border = RuntimeFrameSrgb(226, 165, 61, 245);
                    fill = RuntimeFrameSrgb(43, 31, 12, 238);
                    return;
                case CharacterTestRuntimeAiLabFactsStatus.Error:
                    border = RuntimeFrameSrgb(223, 93, 93, 245);
                    fill = RuntimeFrameSrgb(44, 19, 24, 238);
                    return;
                case CharacterTestRuntimeAiLabFactsStatus.Active:
                    border = RuntimeFrameSrgb(65, 150, 208, 235);
                    fill = RuntimeFrameSrgb(9, 29, 43, 236);
                    return;
                default:
                    border = RuntimeFrameSrgb(48, 68, 81, 220);
                    fill = RuntimeFrameSrgb(10, 23, 33, 232);
                    return;
            }
        }

        internal static string StripRuntimeTestSemanticPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            int separator = value.IndexOf("  ", StringComparison.Ordinal);
            return separator >= 0 && separator + 2 < value.Length
                ? value.Substring(separator + 2)
                : value;
        }

        private void CloseRuntimeTestPopupsForWorkspaceChange()
        {
            if (_runtimeTestAgentPopupHost != null
                && _popupService?.IsOpen(_runtimeTestAgentPopupHost) == true)
            {
                _popupService.Hide(
                    _runtimeTestAgentPopupHost,
                    MxFairyGuiPopupCloseReason.OwnerClosed);
            }
            if (_runtimeTestAdvancedPopupHost != null
                && _popupService?.IsOpen(_runtimeTestAdvancedPopupHost) == true)
            {
                _popupService.Hide(
                    _runtimeTestAdvancedPopupHost,
                    MxFairyGuiPopupCloseReason.OwnerClosed);
            }
        }

        private void ResetRuntimeTestTransientVisuals()
        {
            CloseRuntimeTestPopupsForWorkspaceChange();
            _runtimeTestAgentPopupHost?.Dispose();
            _runtimeTestAdvancedPopupHost?.Dispose();
            _runtimeTestAgentPopupHost = null;
            _runtimeTestAgentPopupBackground = null;
            for (int i = 0; i < _runtimeTestAgentButtons.Length; i++)
                _runtimeTestAgentButtons[i] = null;
            _runtimeTestAdvancedPopupHost = null;
            _runtimeTestAdvancedPopupBackground = null;
            _runtimeTestJumpInput = null;
            _runtimeTestJumpButton = null;
            _runtimeTestLatestButton = null;
            _runtimeTestSpeedButton = null;
            _runtimeTestLoopStartButton = null;
            _runtimeTestLoopEndButton = null;
            _runtimeTestLoopButton = null;
            _runtimeTestReplayButton = null;
        }
    }
}

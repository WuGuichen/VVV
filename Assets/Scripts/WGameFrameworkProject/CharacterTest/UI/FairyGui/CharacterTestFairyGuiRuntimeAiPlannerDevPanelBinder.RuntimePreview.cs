using System;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder
    {
        private int _lastBehaviorCardScrubFrame = int.MinValue;
        private bool _behaviorCardScrubActive;
        private bool _suppressBehaviorCardScrubClick;

        private void BindBehaviorCardRuntimeControls(
            Fgui.GComponent header,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel runtime =
                viewModel.BehaviorCardRuntimeControl;
            if (runtime == null)
                return;

            BindButton(
                header,
                "btnRuntimeFirst",
                "首帧",
                runtime.CanFirst && runtime.Available,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FirstFrame);
            BindButton(
                header,
                "btnRuntimeBack10",
                "-10",
                runtime.CanStepBackward,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepBack10);
            BindButton(
                header,
                "btnRuntimeBack1",
                "-1",
                runtime.CanStepBackward,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepBack);
            BindButton(
                header,
                "btnRuntimePlayPause",
                runtime.PlayPauseLabel,
                runtime.CanPlayPause,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PauseResume,
                selected: !runtime.Paused,
                checkMode: true);
            BindButton(
                header,
                "btnRuntimeForward1",
                "+1",
                runtime.CanStepForward,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step);
            BindButton(
                header,
                "btnRuntimeForward10",
                "+10",
                runtime.CanStepForward,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepForward10);
            BindButton(
                header,
                "btnRuntimeLatest",
                "最新",
                runtime.CanLatest && runtime.Available,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LatestFrame);
            BindButton(
                header,
                "btnRuntimeBranch",
                runtime.BranchActionLabel,
                runtime.CanBranchRecompute,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BranchRecompute,
                runtime.BranchRecomputeArmed,
                checkMode: true);
            BindButton(
                header,
                "btnRuntimeOverlay",
                "场景标记",
                viewModel.CanTogglePreviewOverlay,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TogglePreviewOverlay,
                viewModel.PreviewOverlaySelected,
                checkMode: true);
            Fgui.GButton locateActiveCard = header.GetChild("btnRuntimeLocateActiveCard")?.asButton;
            if (locateActiveCard != null)
            {
                locateActiveCard.visible = runtime.HasDifferentActiveCard;
                locateActiveCard.text = string.IsNullOrEmpty(runtime.ActiveCardName)
                    ? "定位执行卡"
                    : "查看：" + runtime.ActiveCardName;
                locateActiveCard.enabled = runtime.HasDifferentActiveCard;
                locateActiveCard.grayed = !locateActiveCard.enabled;
                locateActiveCard.onClick.Set(() =>
                {
                    if (!locateActiveCard.enabled)
                        return;
                    _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsLocateBehaviorNode,
                        null));
                });
            }
            Action fitView = _behaviorCardPreviewSurface != null
                ? _behaviorCardPreviewSurface.FitView
                : null;
            Action toggleFollow = _behaviorCardPreviewSurface != null
                ? _behaviorCardPreviewSurface.CycleCameraMode
                : null;
            BindPreviewActionButton(header, "btnRuntimeFit", "适配视野", fitView);
            BindPreviewActionButton(header, "btnRuntimeFollow", _behaviorCardPreviewSurface?.CameraModeLabel ?? "镜头模式", toggleFollow);
            ApplyRuntimePreviewReadability(header);
            bool tactical = viewModel.BehaviorCardWorkspace?.Timeline?.CompositionEditor != null || viewModel.BehaviorCardWorkspace?.Timeline?.TacticalPreview != null;
            BindText(header, "runtimeFrameSummaryText", tactical ? viewModel.TacticalFrameSummary?.Invoke() ?? runtime.FrameSummaryText : runtime.FrameSummaryText);
            BindText(header, "runtimeSeekStatusText", runtime.SeekStatusText);
            BindBranchConfirmation(header, runtime);
            SetRuntimeControlTooltip(header, "btnRuntimeFirst", runtime.FirstDisabledReasonText);
            SetRuntimeControlTooltip(header, "btnRuntimeBack10", runtime.BackwardDisabledReasonText);
            SetRuntimeControlTooltip(header, "btnRuntimeBack1", runtime.BackwardDisabledReasonText);
            SetRuntimeControlTooltip(header, "btnRuntimeForward1", runtime.ForwardDisabledReasonText);
            SetRuntimeControlTooltip(header, "btnRuntimeForward10", runtime.ForwardDisabledReasonText);
            SetRuntimeControlTooltip(header, "btnRuntimeLatest", runtime.LatestDisabledReasonText);
            SetRuntimeControlTooltip(header, "btnRuntimePlayPause", runtime.PlayDisabledReasonText);
            SetRuntimeControlTooltip(header, "btnRuntimeBranch", runtime.BranchDisabledReasonText);

            Fgui.GTextInput input = header.GetChild("runtimeFrameInput") as Fgui.GTextInput;
            if (input != null && (!Application.isPlaying || !input.focused))
                input.text = runtime.FrameInputText;
            Fgui.GButton jump = header.GetChild("btnRuntimeJump")?.asButton;
            if (jump != null)
            {
                jump.text = "跳转";
                jump.enabled = runtime.CanJump && runtime.Available;
                jump.grayed = !jump.enabled;
                BindTooltip(jump, jump.enabled ? string.Empty : runtime.JumpDisabledReasonText);
                jump.onClick.Set(() =>
                {
                    if (!jump.enabled)
                        return;
                    object payload = int.TryParse(input?.text, out int frame)
                        ? (object)frame
                        : input?.text;
                    _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.JumpFrame,
                        payload));
                });
            }

            BindBehaviorCardRuntimeScrub(header, runtime);
            ApplyFocusedBehaviorPreviewControls(header);
            if (viewModel.BehaviorCardWorkspace?.Timeline?.TacticalPreview != null)
            {
                bool startTacticalTest = viewModel.PresentationContext
                    == CharacterTestRuntimeAiPlannerDevPresentationContext.Setup;
                if (startTacticalTest)
                {
                    // The setup models are presentation-only. The first Play must create the normal
                    // frozen single-card test, not try to resume a runtime session that does not exist.
                    BindButton(header, "btnRuntimePlayPause", "播放", viewModel.CanTestCurrentChanges,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TestCurrentChanges, payload: true);
                    SetRuntimeControlTooltip(header, "btnRuntimePlayPause", viewModel.CanTestCurrentChanges
                        ? "使用当前修改开始测试" : viewModel.TestCurrentChangesDisabledReasonText);
                }
                // Reuse an existing published button; keep rewind and restart as separate commands.
                BindButton(header, "btnRuntimeBack10", "↻", viewModel.CanTestCurrentChanges,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TestCurrentChanges, payload: true);
                if (header.GetChild("btnRuntimeBack10") is Fgui.GObject restart)
                {
                    restart.visible = true;
                    restart.tooltips = "从头播放：从第 0 帧重新开始测试";
                }
                var hurtboxPreview = _behaviorCardPreviewSurface as CharacterTestRuntimeAiLabBehaviorCardPreviewHost;
                BindPreviewActionButton(header, "btnRuntimeOverlay", "目标受击框",
                    () => hurtboxPreview?.ToggleTargetHurtboxes());
                if (header.GetChild("btnRuntimeOverlay") is Fgui.GButton hurtboxes)
                {
                    hurtboxes.visible = true;
                    hurtboxes.title = hurtboxPreview?.ShowTargetHurtboxes == true ? "受击框：开" : "受击框：关";
                    hurtboxes.tooltips = "显隐当前目标的真实受击形状；历史帧缺少几何数据时不显示。";
                }
            }
        }

        private static void ApplyFocusedBehaviorPreviewControls(Fgui.GComponent header)
        {
            string[] hiddenControls =
            {
                "btnRuntimeBack10",
                "btnRuntimeBack1",
                "btnRuntimeForward10",
                "btnRuntimeLatest",
                "runtimeFrameInput",
                "btnRuntimeJump",
                "btnRuntimeBranch",
                "btnRuntimeFollow",
                "btnRuntimeOverlay",
                "btnRuntimeLocateActiveCard",
                "runtimeSeekStatusText",
                "branchConfirmationPanel",
                "branchConfirmationText",
                "btnBranchCancel",
                "btnBranchConfirm"
            };
            for (int i = 0; i < hiddenControls.Length; i++)
            {
                Fgui.GObject control = header.GetChild(hiddenControls[i]);
                if (control != null)
                    control.visible = false;
            }

            SetRuntimePreviewControlGeometry(header, "btnRuntimeFirst", 12f, 350f, 112f, 44f);
            SetRuntimePreviewControlGeometry(header, "btnRuntimePlayPause", 136f, 350f, 128f, 44f);
            SetRuntimePreviewControlGeometry(header, "btnRuntimeForward1", 276f, 350f, 112f, 44f);
            SetRuntimePreviewControlGeometry(header, "btnRuntimeFit", 400f, 350f, 112f, 44f);
        }

        private static void SetRuntimePreviewControlGeometry(
            Fgui.GComponent header,
            string childName,
            float x,
            float y,
            float width,
            float height)
        {
            Fgui.GObject control = header.GetChild(childName);
            if (control == null)
                return;
            control.visible = true;
            control.SetXY(x, y);
            control.SetSize(width, height);
        }

        private static void ApplyRuntimePreviewReadability(Fgui.GComponent header)
        {
            SetRuntimePreviewButtonFontSize(header, "btnRuntimeFirst", 17);
            SetRuntimePreviewButtonFontSize(header, "btnRuntimeBack10", 17);
            SetRuntimePreviewButtonFontSize(header, "btnRuntimeBack1", 17);
            SetRuntimePreviewButtonFontSize(header, "btnRuntimePlayPause", 18);
            SetRuntimePreviewButtonFontSize(header, "btnRuntimeForward1", 17);
            SetRuntimePreviewButtonFontSize(header, "btnRuntimeForward10", 17);
            SetRuntimePreviewButtonFontSize(header, "btnRuntimeLatest", 17);
            SetRuntimePreviewButtonFontSize(header, "btnRuntimeJump", 17);
            SetRuntimePreviewButtonFontSize(header, "btnRuntimeBranch", 17);
            SetRuntimePreviewButtonFontSize(header, "btnRuntimeFit", 17);
            SetRuntimePreviewButtonFontSize(header, "btnRuntimeFollow", 17);
            SetRuntimePreviewButtonFontSize(header, "btnRuntimeOverlay", 17);

            Fgui.GTextInput frameInput = header.GetChild("runtimeFrameInput") as Fgui.GTextInput;
            if (frameInput != null)
                frameInput.textFormat.size = 18;
        }

        private static void SetRuntimePreviewButtonFontSize(
            Fgui.GComponent header,
            string childName,
            int fontSize)
        {
            Fgui.GButton button = header.GetChild(childName)?.asButton;
            if (button != null && button.titleFontSize != fontSize)
                button.titleFontSize = fontSize;
        }

        private void BindBranchConfirmation(
            Fgui.GComponent header,
            CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel runtime)
        {
            bool visible = runtime.BranchConfirmationPending;
            Fgui.GObject panel = header.GetChild("branchConfirmationPanel");
            Fgui.GObject textObject = header.GetChild("branchConfirmationText");
            Fgui.GButton cancel = header.GetChild("btnBranchCancel")?.asButton;
            Fgui.GButton confirm = header.GetChild("btnBranchConfirm")?.asButton;
            if (panel != null)
                panel.visible = visible;
            if (textObject != null)
            {
                textObject.visible = visible;
                if (textObject.asTextField != null)
                {
                    textObject.asTextField.text = "建立新未来将使旧历史失效\n"
                        + runtime.BranchRiskText;
                }
            }
            if (cancel != null)
            {
                cancel.visible = visible;
                cancel.text = "取消";
                cancel.enabled = runtime.CanBranchRecompute;
                cancel.grayed = !cancel.enabled;
                BindTooltip(cancel, cancel.enabled ? string.Empty : runtime.BranchDisabledReasonText);
                cancel.onClick.Set(() =>
                {
                    if (cancel.enabled)
                    {
                        _commandSink?.Enqueue(new MxUiCommand(
                            _viewId,
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BranchRecomputeCancel,
                            null));
                    }
                });
            }
            if (confirm != null)
            {
                confirm.visible = visible;
                confirm.text = "确认建立新未来";
                confirm.enabled = runtime.CanBranchRecompute;
                confirm.grayed = !confirm.enabled;
                BindTooltip(confirm, confirm.enabled ? string.Empty : runtime.BranchDisabledReasonText);
                confirm.onClick.Set(() =>
                {
                    if (confirm.enabled)
                    {
                        _commandSink?.Enqueue(new MxUiCommand(
                            _viewId,
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BranchRecompute,
                            null));
                    }
                });
            }
        }

        private void SetRuntimeControlTooltip(
            Fgui.GComponent header,
            string childName,
            string disabledReason)
        {
            Fgui.GObject control = header.GetChild(childName);
            if (control != null)
                BindTooltip(
                    control,
                    control.enabled ? string.Empty : disabledReason ?? string.Empty);
        }

        private void BindPreviewActionButton(
            Fgui.GComponent header,
            string childName,
            string label,
            Action action)
        {
            Fgui.GButton button = header.GetChild(childName)?.asButton;
            if (button == null)
                return;
            bool enabled = action != null;
            button.text = label;
            button.enabled = enabled;
            button.grayed = !enabled;
            button.onClick.Set(() =>
            {
                if (enabled)
                    action();
            });
        }

        private void BindBehaviorCardRuntimeScrub(
            Fgui.GComponent header,
            CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel runtime)
        {
            Fgui.GObject track = header.GetChild("runtimeScrubTrack");
            Fgui.GObject evicted = header.GetChild("runtimeScrubEvicted");
            Fgui.GObject captured = header.GetChild("runtimeScrubCaptured");
            Fgui.GObject future = header.GetChild("runtimeScrubFuture");
            Fgui.GObject thumb = header.GetChild("runtimeScrubThumb");
            Fgui.GObject pending = header.GetChild("runtimeScrubPending");
            Fgui.GObject queued = header.GetChild("runtimeScrubQueued");
            Fgui.GObject hover = header.GetChild("runtimeScrubHover");
            if (track == null)
                return;

            int start = runtime.SessionStartFrame;
            int end = Math.Max(start, runtime.SessionEndFrame);
            int range = Math.Max(1, end - start);
            float trackX = track.x;
            float trackWidth = Mathf.Max(1f, track.width);
            float ToX(int frame) =>
                trackX + trackWidth * Mathf.Clamp01((frame - start) / (float)range);

            float retainedX = ToX(runtime.RetainedStartFrame);
            float latestX = ToX(runtime.LatestFrame);
            SetSegment(evicted, trackX, retainedX - trackX);
            SetSegment(captured, retainedX, latestX - retainedX);
            SetSegment(future, latestX, trackX + trackWidth - latestX);
            if (thumb != null)
            {
                thumb.x = ToX(runtime.SelectedFrame) - thumb.width * 0.5f;
                thumb.visible = runtime.Available;
            }
            SetRuntimeMarker(pending, runtime.PendingTargetFrame, ToX(runtime.PendingTargetFrame));
            SetRuntimeMarker(queued, runtime.QueuedTargetFrame, ToX(runtime.QueuedTargetFrame));
            SetRuntimeMarker(
                hover,
                _behaviorCardScrubActive ? _lastBehaviorCardScrubFrame : 0,
                ToX(_lastBehaviorCardScrubFrame));

            track.enabled = runtime.CanSeek && runtime.Available;
            bool TryResolve(Fgui.EventContext context, out int frame)
            {
                frame = 0;
                if (!track.enabled || context?.inputEvent == null)
                    return false;
                Vector2 local = track.GlobalToLocal(new Vector2(
                    context.inputEvent.x,
                    context.inputEvent.y));
                frame = start + Mathf.RoundToInt(range * Mathf.Clamp01(local.x / trackWidth));
                return true;
            }

            void Dispatch(Fgui.EventContext context, string commandId)
            {
                if (!TryResolve(context, out int frame))
                    return;
                if (commandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubUpdate
                    && frame == _lastBehaviorCardScrubFrame)
                {
                    return;
                }
                _lastBehaviorCardScrubFrame = frame;
                if (hover != null)
                {
                    hover.visible = true;
                    hover.x = ToX(frame) - hover.width * 0.5f;
                }
                _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, frame));
            }

            track.onTouchBegin.Set(context =>
            {
                _behaviorCardScrubActive = true;
                _suppressBehaviorCardScrubClick = false;
                _lastBehaviorCardScrubFrame = int.MinValue;
                context.CaptureTouch();
                Dispatch(context, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubBegin);
            });
            track.onTouchMove.Set(context =>
            {
                if (_behaviorCardScrubActive)
                {
                    Dispatch(
                        context,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubUpdate);
                }
            });
            track.onTouchEnd.Set(context =>
            {
                Dispatch(context, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubEnd);
                _behaviorCardScrubActive = false;
                _suppressBehaviorCardScrubClick = true;
                if (hover != null)
                    hover.visible = false;
            });
            track.onClick.Set(context =>
            {
                if (_behaviorCardScrubActive)
                    return;
                if (_suppressBehaviorCardScrubClick)
                {
                    _suppressBehaviorCardScrubClick = false;
                    return;
                }
                Dispatch(context, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubBegin);
                Dispatch(context, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubEnd);
                if (hover != null)
                    hover.visible = false;
            });
        }

        private static void SetRuntimeMarker(
            Fgui.GObject marker,
            int frame,
            float x)
        {
            if (marker == null)
                return;
            marker.visible = frame > 0;
            if (marker.visible)
                marker.x = x - marker.width * 0.5f;
        }

        private static void SetSegment(Fgui.GObject segment, float x, float width)
        {
            if (segment == null)
                return;
            segment.x = x;
            segment.width = Mathf.Max(0f, width);
            segment.visible = segment.width > 0.5f;
        }
    }
}

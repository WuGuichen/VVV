using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Owns the legacy root timeline jump and scrub controls.</summary>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabLegacyTimelineInputBinder
    {
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;

        internal CharacterTestFairyGuiRuntimeAiLabLegacyTimelineInputBinder(
            IMxUiCommandSink commandSink,
            MxUiViewId viewId)
        {
            _commandSink = commandSink;
            _viewId = viewId;
        }

        internal void BindJumpFrameButton(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            Fgui.GButton button = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.JumpFrame)?.asButton;
            if (button == null)
                return;

            button.text = viewModel.JumpFrameLabel ?? string.Empty;
            button.enabled = viewModel.CanJumpFrame;
            button.grayed = !viewModel.CanJumpFrame;
            button.visible = true;
            button.onClick.Set(() =>
            {
                if (!viewModel.CanJumpFrame)
                    return;

                Fgui.GObject input = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.JumpFrameInput);
                string text = input is Fgui.GTextInput textInput
                    ? textInput.text
                    : string.Empty;
                object payload = int.TryParse(text, out int runtimeFrame)
                    ? runtimeFrame
                    : text;
                _commandSink?.Enqueue(new MxUiCommand(
                    _viewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.JumpFrame,
                    payload));
            });
        }

        internal void BindScrubTrack(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            Fgui.GObject track = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineScrubTrack);
            if (track == null)
                return;

            Fgui.GObject fill = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineProgressFill);
            Fgui.GObject thumb = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineProgressThumb);
            track.visible = true;
            track.enabled = viewModel.CanJumpFrame
                && viewModel.TimelineEndFrame >= viewModel.TimelineStartFrame;
            int lastSentRuntimeFrame = -1;

            bool TryResolveRuntimeFrame(
                Fgui.EventContext context,
                out int runtimeFrame,
                out float normalizedPosition)
            {
                runtimeFrame = 0;
                normalizedPosition = 0f;
                if (!viewModel.CanJumpFrame
                    || viewModel.TimelineEndFrame < viewModel.TimelineStartFrame
                    || context?.inputEvent == null)
                {
                    return false;
                }

                Vector2 local = track.GlobalToLocal(
                    new Vector2(context.inputEvent.x, context.inputEvent.y));
                normalizedPosition = Mathf.Clamp01(local.x / Mathf.Max(1f, track.width));
                int runtimeFrameRange =
                    viewModel.TimelineEndFrame - viewModel.TimelineStartFrame;
                runtimeFrame = viewModel.TimelineStartFrame
                    + Mathf.RoundToInt(runtimeFrameRange * normalizedPosition);
                return true;
            }

            void UpdateIndicator(float normalizedPosition)
            {
                float clampedPosition = Mathf.Clamp01(normalizedPosition);
                if (fill != null)
                    fill.width = Mathf.Max(1f, track.width * clampedPosition);
                if (thumb != null)
                    thumb.x = track.x + track.width * clampedPosition - thumb.width * 0.5f;
            }

            void EnqueueRuntimeFrameFromPointer(Fgui.EventContext context)
            {
                if (!TryResolveRuntimeFrame(
                        context,
                        out int runtimeFrame,
                        out float normalizedPosition))
                {
                    return;
                }

                UpdateIndicator(normalizedPosition);
                if (runtimeFrame == lastSentRuntimeFrame)
                    return;

                lastSentRuntimeFrame = runtimeFrame;
                _commandSink?.Enqueue(new MxUiCommand(
                    _viewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.JumpFrame,
                    runtimeFrame));
            }

            track.onClick.Set(EnqueueRuntimeFrameFromPointer);
            track.onTouchMove.Set(context =>
            {
                if (TryResolveRuntimeFrame(context, out _, out float normalizedPosition))
                    UpdateIndicator(normalizedPosition);
            });
            track.onTouchEnd.Set(EnqueueRuntimeFrameFromPointer);

            int selectedRuntimeFrameRange =
                viewModel.TimelineEndFrame - viewModel.TimelineStartFrame;
            float selectedPosition = selectedRuntimeFrameRange > 0
                ? (viewModel.TimelineSelectedFrame - viewModel.TimelineStartFrame)
                    / (float)selectedRuntimeFrameRange
                : 0f;
            UpdateIndicator(selectedPosition);
        }
    }
}

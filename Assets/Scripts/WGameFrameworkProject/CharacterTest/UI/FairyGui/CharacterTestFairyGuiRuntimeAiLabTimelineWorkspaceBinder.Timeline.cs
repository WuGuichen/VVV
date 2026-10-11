using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;
using TimelineGeometry = MxFramework.Game.CharacterTest.CharacterTestRuntimeAiLabTimelineGeometry;


namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder
    {
        internal static string TrackLabel(CharacterTestRuntimeAiLabTimelineTrackKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabTimelineTrackKind.Perception: return "感知";
                case CharacterTestRuntimeAiLabTimelineTrackKind.Decision: return "决策";
                case CharacterTestRuntimeAiLabTimelineTrackKind.Action: return "动作";
                case CharacterTestRuntimeAiLabTimelineTrackKind.Result: return "结果";
                default: return "警告";
            }
        }

        private static string ItemKindLabel(CharacterTestRuntimeAiLabTimelineItemKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabTimelineItemKind.Span: return "区间";
                case CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack: return "同帧堆叠";
                case CharacterTestRuntimeAiLabTimelineItemKind.ZoomDensityAggregate: return "密度聚合";
                default: return "事件";
            }
        }

        private void BindFormalTimelineScrub(
            Fgui.GComponent timeline,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            CharacterTestRuntimeAiLabTimelineProjection projection = viewModel.TimelineProjection;
            if (projection == null || !projection.Domain.HasFrames)
            {
                _timelineViewportInputBinder.Reset();
                return;
            }

            // A scrub-begin command refreshes the ViewModel before the pointer-up event
            // arrives. Preserve the live input binder state across that refresh so its
            // hit surfaces remain enabled and can deliver the matching scrub-end.
            bool scrubEnabled = ShouldKeepTimelineScrubEnabled(
                viewModel.CanJumpFrame,
                _timelineScrubActive,
                _timelineViewportInputBinder.Active);
            _timelineViewportInputBinder.Bind(
                timeline,
                projection,
                scrubEnabled,
                _timelineViewportIntentSink);
            _timelineScrubActive = _timelineViewportInputBinder.Active;
        }

        internal static bool ShouldKeepTimelineScrubEnabled(
            bool canJumpFrame,
            bool scrubLatched,
            bool pointerGestureActive)
        {
            return canJumpFrame || scrubLatched || pointerGestureActive;
        }

        internal static bool ShouldKeepTimelinePreview(
            bool scrubLatched,
            bool pointerGestureActive)
        {
            return scrubLatched || pointerGestureActive;
        }

        private static void ClearTimelinePreview(Fgui.GComponent timeline)
        {
            SetTimelineChildVisible(timeline, "timelinePreviewPlayhead", false);
            SetTimelineChildVisible(timeline, "timelinePreviewFrameBadge", false);
            SetTimelineChildVisible(timeline, "timelinePreviewFrameLabel", false);
        }

        private static void SetTimelineChildVisible(Fgui.GComponent root, string childName, bool visible)
        {
            Fgui.GObject child = root?.GetChild(childName);
            if (child != null)
                child.visible = visible;
        }

        private static void HideLegacyTimeline(Fgui.GComponent root)
        {
            for (int i = 0; i < LegacyTimelineRootChildren.Length; i++)
            {
                Fgui.GObject child = root.GetChild(LegacyTimelineRootChildren[i]);
                if (child != null)
                    child.visible = false;
            }
        }
    }
}

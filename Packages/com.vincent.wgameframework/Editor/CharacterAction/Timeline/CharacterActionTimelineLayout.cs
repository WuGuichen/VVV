using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal static class CharacterActionTimelineLayout
    {
        public const float LabelColumnWidth = 128f;
        public const float RowHeight = 28f;
        public const float AnimationRowHeight = 32f;
        public const float RulerHeight = 24f;
        public const float ToolbarHeight = 30f;
        public const float ItemLaneHeight = 24f;
        public const float LaneVerticalPadding = 4f;
        public const float LaneItemGap = 2f;
        public const float ClipLaneInset = 2f;
        public const float ClipHeight = 20f;
        public const float MinClipWidth = 28f;
        public const float ClipResizeHandleWidth = 10f;
        public const float NarrowClipWidth = 44f;
        public const float MicroClipWidth = 32f;
        public const float MarkerHeight = 18f;
        public const float MinPixelsPerFrame = 4f;
        public const float MaxPixelsPerFrame = 48f;
        public const float DefaultPixelsPerFrame = 12f;
        public const float ContentPadding = 20f;
        public const int EditorTimelineFps = CharacterActionAnimationKeyUtility.TimelineFramesPerSecond;
        public const float MarkerNodeWidth = 18f;
        public const float MinMarkerWidth = 12f;
        public const float MarkerCharWidth = 6.5f;

        public static float FrameToX(int frame, float pixelsPerFrame)
        {
            return frame * pixelsPerFrame;
        }

        public static int XToFrame(float localX, float pixelsPerFrame, bool snapToFrame)
        {
            if (pixelsPerFrame <= 0.0001f)
                return 0;

            float raw = localX / pixelsPerFrame;
            return snapToFrame ? Mathf.RoundToInt(raw) : Mathf.FloorToInt(raw);
        }

        public static int ClampFrame(int frame, int durationFrames)
        {
            int maxFrame = Math.Max(0, durationFrames - 1);
            return Mathf.Clamp(frame, 0, maxFrame);
        }

        public static float ContentWidth(int durationFrames, float pixelsPerFrame)
        {
            int span = Math.Max(1, durationFrames);
            return span * pixelsPerFrame + ContentPadding * 2f;
        }

        public static int ResolveDurationFrames(int? durationFrames, int fallback)
        {
            return durationFrames.HasValue && durationFrames.Value > 0 ? durationFrames.Value : Math.Max(1, fallback);
        }

        public static int TransitionFramesFromSeconds(float transitionSeconds)
        {
            if (transitionSeconds <= 0f)
                return 0;
            return Math.Max(0, Mathf.RoundToInt(transitionSeconds * EditorTimelineFps));
        }

        public static int ComputeSpanFrames(int sourceClipFrames, float playbackSpeed)
        {
            int clipFrames = Math.Max(1, sourceClipFrames);
            float speed = playbackSpeed <= 0.0001f ? 1f : playbackSpeed;
            return Math.Max(1, Mathf.RoundToInt(clipFrames / speed));
        }

        public static int ComputeEndFrame(int startFrame, int spanFrames, int durationFrames)
        {
            int maxFrame = Math.Max(0, durationFrames - 1);
            int end = startFrame + Math.Max(1, spanFrames) - 1;
            return Mathf.Clamp(end, startFrame, maxFrame);
        }

        public static float EstimateMarkerWidth(string title, float pixelsPerFrame, int startFrame, int endFrame)
        {
            int spanFrames = Math.Max(1, endFrame - startFrame + 1);
            float spanPixels = spanFrames * pixelsPerFrame;
            float textWidth = string.IsNullOrEmpty(title) ? MinMarkerWidth : title.Length * MarkerCharWidth + 8f;
            return Mathf.Clamp(Math.Max(MinMarkerWidth, textWidth), MinMarkerWidth, Math.Max(MinMarkerWidth, spanPixels));
        }
    }

    internal static class CharacterActionPhaseTimelineUtility
    {
        public static bool NormalizePhases(CharacterActionJsonDto action, int durationFrames)
        {
            if (action == null)
                return false;

            int duration = Math.Max(1, durationFrames);
            CharacterActionPhaseJsonDto[] source = action.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>();
            var entries = new List<PhaseEntry>(Math.Max(1, source.Length));
            for (int i = 0; i < source.Length; i++)
            {
                CharacterActionPhaseJsonDto phase = source[i] ?? new CharacterActionPhaseJsonDto();
                EnsureDefaults(phase);
                entries.Add(new PhaseEntry(
                    phase,
                    CharacterActionTimelineLayout.ClampFrame(phase.StartFrame, duration),
                    i));
            }

            if (entries.Count == 0)
            {
                action.Phases = new[]
                {
                    CreatePhase(CharacterActionPhaseKind.Startup.ToString(), 0, duration - 1),
                };
                return true;
            }

            entries.Sort(ComparePhaseEntries);
            int count = Math.Min(entries.Count, duration);
            bool changed = count != source.Length;
            if (entries.Count > count)
                entries.RemoveRange(count, entries.Count - count);

            for (int i = 0; i < entries.Count; i++)
            {
                CharacterActionPhaseJsonDto phase = entries[i].Phase;
                int start;
                if (i == 0)
                {
                    start = 0;
                }
                else
                {
                    int minStart = entries[i - 1].Phase.StartFrame + 1;
                    int maxStart = duration - (entries.Count - i);
                    start = Mathf.Clamp(entries[i].AnchorStartFrame, minStart, maxStart);
                }

                if (phase.StartFrame != start)
                    changed = true;
                phase.StartFrame = start;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                CharacterActionPhaseJsonDto phase = entries[i].Phase;
                int end = i == entries.Count - 1
                    ? duration - 1
                    : entries[i + 1].Phase.StartFrame - 1;
                if (phase.EndFrame != end)
                    changed = true;
                phase.EndFrame = end;
                if (entries[i].OriginalIndex != i)
                    changed = true;
            }

            var normalized = new CharacterActionPhaseJsonDto[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                normalized[i] = entries[i].Phase;
            action.Phases = normalized;
            return changed;
        }

        public static bool InsertPhaseAt(
            CharacterActionJsonDto action,
            int frame,
            int durationFrames,
            string kind)
        {
            if (action == null)
                return false;

            int duration = Math.Max(1, durationFrames);
            NormalizePhases(action, duration);
            var phases = new List<CharacterActionPhaseJsonDto>(action.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>());
            if (phases.Count >= duration)
                return false;

            frame = CharacterActionTimelineLayout.ClampFrame(frame, duration);
            int insertIndex = FindPhaseIndexAtOrAfter(phases, frame);
            var phase = CreatePhase(
                string.IsNullOrWhiteSpace(kind) ? CharacterActionPhaseKind.Active.ToString() : kind,
                frame,
                frame);

            if (insertIndex < 0)
            {
                phases.Add(phase);
            }
            else if (frame <= phases[insertIndex].StartFrame)
            {
                phases.Insert(insertIndex, phase);
            }
            else
            {
                phases.Insert(insertIndex + 1, phase);
            }

            action.Phases = phases.ToArray();
            NormalizePhases(action, duration);
            return true;
        }

        public static bool RemovePhaseAt(CharacterActionJsonDto action, int index, int durationFrames)
        {
            if (action == null)
                return false;

            int duration = Math.Max(1, durationFrames);
            NormalizePhases(action, duration);
            var phases = new List<CharacterActionPhaseJsonDto>(action.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>());
            if (phases.Count <= 1 || index < 0 || index >= phases.Count)
                return false;

            phases.RemoveAt(index);
            action.Phases = phases.ToArray();
            NormalizePhases(action, duration);
            return true;
        }

        public static bool SetPhaseStartFrame(
            CharacterActionJsonDto action,
            CharacterActionPhaseJsonDto phase,
            int startFrame,
            int durationFrames)
        {
            if (action == null || phase == null)
                return false;

            int duration = Math.Max(1, durationFrames);
            NormalizePhases(action, duration);
            if (!ContainsPhase(action, phase))
                return false;

            phase.StartFrame = CharacterActionTimelineLayout.ClampFrame(startFrame, duration);
            NormalizePhases(action, duration);
            return true;
        }

        public static bool SetPhaseStartFrame(
            CharacterActionJsonDto action,
            int index,
            int startFrame,
            int durationFrames)
        {
            CharacterActionPhaseJsonDto phase = GetPhase(action, index);
            return SetPhaseStartFrame(action, phase, startFrame, durationFrames);
        }

        public static bool SetPhaseEndFrame(
            CharacterActionJsonDto action,
            CharacterActionPhaseJsonDto phase,
            int endFrame,
            int durationFrames)
        {
            if (action == null || phase == null)
                return false;

            int duration = Math.Max(1, durationFrames);
            NormalizePhases(action, duration);
            CharacterActionPhaseJsonDto[] phases = action.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>();
            int index = IndexOf(phases, phase);
            if (index < 0)
                return false;

            if (index < phases.Length - 1)
            {
                int nextStart = CharacterActionTimelineLayout.ClampFrame(endFrame + 1, duration);
                phases[index + 1].StartFrame = nextStart;
            }

            NormalizePhases(action, duration);
            return true;
        }

        public static bool SetPhaseEndFrame(
            CharacterActionJsonDto action,
            int index,
            int endFrame,
            int durationFrames)
        {
            CharacterActionPhaseJsonDto phase = GetPhase(action, index);
            return SetPhaseEndFrame(action, phase, endFrame, durationFrames);
        }

        public static bool SetPhaseRange(
            CharacterActionJsonDto action,
            CharacterActionPhaseJsonDto phase,
            int startFrame,
            int endFrame,
            int durationFrames)
        {
            if (action == null || phase == null)
                return false;

            int duration = Math.Max(1, durationFrames);
            NormalizePhases(action, duration);
            CharacterActionPhaseJsonDto[] phases = action.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>();
            int index = IndexOf(phases, phase);
            if (index < 0)
                return false;

            int nextStart = CharacterActionTimelineLayout.ClampFrame(Math.Min(startFrame, endFrame), duration);
            int nextEnd = CharacterActionTimelineLayout.ClampFrame(Math.Max(startFrame, endFrame), duration);
            int originalStart = phase.StartFrame;
            int originalEnd = phase.EndFrame;

            if (nextStart != originalStart && nextEnd == originalEnd)
                return SetPhaseStartFrame(action, phase, nextStart, duration);

            if (nextStart == originalStart && nextEnd != originalEnd)
                return SetPhaseEndFrame(action, phase, nextEnd, duration);

            phase.StartFrame = nextStart;
            if (index < phases.Length - 1)
                phases[index + 1].StartFrame = Math.Min(duration - 1, nextEnd + 1);
            else
                phase.EndFrame = nextEnd;

            NormalizePhases(action, duration);
            return true;
        }

        private static CharacterActionPhaseJsonDto GetPhase(CharacterActionJsonDto action, int index)
        {
            CharacterActionPhaseJsonDto[] phases = action?.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>();
            return index >= 0 && index < phases.Length ? phases[index] : null;
        }

        private static bool ContainsPhase(CharacterActionJsonDto action, CharacterActionPhaseJsonDto phase)
        {
            return IndexOf(action?.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>(), phase) >= 0;
        }

        private static int IndexOf(CharacterActionPhaseJsonDto[] phases, CharacterActionPhaseJsonDto phase)
        {
            for (int i = 0; i < phases.Length; i++)
            {
                if (ReferenceEquals(phases[i], phase))
                    return i;
            }

            return -1;
        }

        private static int FindPhaseIndexAtOrAfter(List<CharacterActionPhaseJsonDto> phases, int frame)
        {
            for (int i = 0; i < phases.Count; i++)
            {
                CharacterActionPhaseJsonDto phase = phases[i];
                if (phase != null && frame <= phase.EndFrame)
                    return i;
            }

            return -1;
        }

        private static CharacterActionPhaseJsonDto CreatePhase(string kind, int startFrame, int endFrame)
        {
            var phase = new CharacterActionPhaseJsonDto
            {
                Kind = string.IsNullOrWhiteSpace(kind) ? CharacterActionPhaseKind.Active.ToString() : kind,
                StartFrame = startFrame,
                EndFrame = Math.Max(startFrame, endFrame),
                CombatPhaseAnchor = CombatActionPhase.None.ToString(),
                RequiresCombatPhaseMatch = true,
            };
            return phase;
        }

        private static void EnsureDefaults(CharacterActionPhaseJsonDto phase)
        {
            if (string.IsNullOrWhiteSpace(phase.Kind))
                phase.Kind = CharacterActionPhaseKind.Startup.ToString();
            if (string.IsNullOrWhiteSpace(phase.CombatPhaseAnchor))
                phase.CombatPhaseAnchor = CombatActionPhase.None.ToString();
        }

        private static int ComparePhaseEntries(PhaseEntry left, PhaseEntry right)
        {
            int compare = left.AnchorStartFrame.CompareTo(right.AnchorStartFrame);
            if (compare != 0)
                return compare;
            return left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private readonly struct PhaseEntry
        {
            public PhaseEntry(
                CharacterActionPhaseJsonDto phase,
                int anchorStartFrame,
                int originalIndex)
            {
                Phase = phase;
                AnchorStartFrame = anchorStartFrame;
                OriginalIndex = originalIndex;
            }

            public CharacterActionPhaseJsonDto Phase { get; }
            public int AnchorStartFrame { get; }
            public int OriginalIndex { get; }
        }
    }
}

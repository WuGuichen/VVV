using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Editor.Animation;
using MxFramework.Editor.CharacterAction;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal static class CharacterActionAnimationTimelineResolver
    {
        public const float MinPlaybackSpeed = 0.05f;
        public const float MaxPlaybackSpeed = 16f;

        private static readonly Dictionary<AnimationSourceLookupKey, int> SourceFramesByLookupKey =
            new Dictionary<AnimationSourceLookupKey, int>();

        public static bool ApplyAutoSpan(
            AnimationTrackEventJsonDto evt,
            CharacterActionJsonDto action,
            int fallbackDurationFrames,
            string characterStableId = "")
        {
            if (evt == null || action == null)
                return false;

            NormalizeEvent(evt);
            int duration = CharacterActionTimelineLayout.ResolveDurationFrames(action.DurationFrames, fallbackDurationFrames);
            if (!TryResolveSourceClipFrames(evt, characterStableId, out int sourceFrames))
                return false;

            NormalizeOffsets(evt, sourceFrames);
            int span = ComputeTimelineSpanFrames(
                ComputePlayableSourceFrames(sourceFrames, evt),
                evt.PlaybackSpeed);
            evt.EndFrame = CharacterActionTimelineLayout.ComputeEndFrame(evt.Frame, span, duration);
            return true;
        }

        public static bool FitEndToResolvedClip(
            AnimationTrackEventJsonDto evt,
            CharacterActionJsonDto action,
            int fallbackDurationFrames,
            string characterStableId = "")
        {
            return ApplyAutoSpan(evt, action, fallbackDurationFrames, characterStableId);
        }

        public static void FitEndToActionDuration(
            AnimationTrackEventJsonDto evt,
            CharacterActionJsonDto action,
            int fallbackDurationFrames,
            string characterStableId = "")
        {
            if (evt == null || action == null)
                return;

            int duration = CharacterActionTimelineLayout.ResolveDurationFrames(action.DurationFrames, fallbackDurationFrames);
            ApplyRange(
                evt,
                evt.Frame,
                duration - 1,
                trimSource: false,
                actionDurationFrames: duration,
                characterStableId);
        }

        public static bool TryResolveSourceClipFrames(AnimationTrackEventJsonDto evt, string characterStableId, out int clipFrames)
        {
            string animationKey = CharacterActionAnimationKeyUtility.ResolveEffectiveKey(
                evt?.AnimationClipResourceKey,
                evt?.AnimationActionKey);
            return TryResolveSourceClipFrames(animationKey, characterStableId, out clipFrames);
        }

        public static bool TryResolveSourceClipFrames(string animationActionKey, out int clipFrames)
        {
            return TryResolveSourceClipFrames(animationActionKey, string.Empty, out clipFrames);
        }

        public static bool TryResolveSourceClipFrames(string animationActionKey, string characterStableId, out int clipFrames)
        {
            clipFrames = 0;
            if (string.IsNullOrWhiteSpace(animationActionKey))
                return false;

            var lookupKey = new AnimationSourceLookupKey(animationActionKey, characterStableId);
            if (SourceFramesByLookupKey.TryGetValue(lookupKey, out clipFrames))
                return clipFrames > 0;

            if (CharacterActionPreviewBindingDiscovery.TryFindAnimationClipChoice(
                    animationActionKey,
                    characterStableId,
                    out CharacterActionAnimationClipChoice choice)
                && TryResolveClipFrames(choice, out clipFrames))
            {
                SourceFramesByLookupKey[lookupKey] = clipFrames;
                return true;
            }

            string[] guids = AssetDatabase.FindAssets("t:MxAnimationClipRegistryAsset");
            for (int g = 0; g < guids.Length; g++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                var registry = AssetDatabase.LoadAssetAtPath<MxAnimationClipRegistryAsset>(path);
                if (registry == null)
                    continue;

                MxAnimationClipRegistryBindingEntry[] bindings = registry.Bindings;
                for (int i = 0; i < bindings.Length; i++)
                {
                    MxAnimationClipRegistryBindingEntry binding = bindings[i];
                    if (!string.Equals(binding.ResolveActionKey(), animationActionKey, StringComparison.Ordinal))
                        continue;

                    if (TryResolveClipFrames(registry, binding.ClipId, out clipFrames))
                    {
                        SourceFramesByLookupKey[lookupKey] = clipFrames;
                        return true;
                    }
                }
            }

            SourceFramesByLookupKey[lookupKey] = 0;
            return false;
        }

        internal static void InvalidateSourceFrameCache()
        {
            SourceFramesByLookupKey.Clear();
        }

        public static int ComputePlayableSourceFrames(int sourceFrames, AnimationTrackEventJsonDto evt)
        {
            int source = Math.Max(1, sourceFrames);
            int startOffset = Math.Max(0, evt?.AnimationStartOffsetFrames ?? 0);
            int endOffset = Math.Max(0, evt?.AnimationEndOffsetFrames ?? 0);
            return Math.Max(1, source - startOffset - endOffset);
        }

        public static int ComputeTimelineSpanFrames(int playableSourceFrames, float playbackSpeed)
        {
            float speed = ClampPlaybackSpeed(playbackSpeed);
            return Math.Max(1, Mathf.CeilToInt(Math.Max(1, playableSourceFrames) / speed));
        }

        public static float ComputePlaybackSpeed(int playableSourceFrames, int timelineSpanFrames)
        {
            float speed = Math.Max(1, playableSourceFrames) / (float)Math.Max(1, timelineSpanFrames);
            return Mathf.Clamp(speed, MinPlaybackSpeed, MaxPlaybackSpeed);
        }

        public static void ApplyRange(
            AnimationTrackEventJsonDto evt,
            int requestedStartFrame,
            int requestedEndFrame,
            bool trimSource,
            int actionDurationFrames,
            string characterStableId = "")
        {
            if (evt == null)
                return;

            NormalizeEvent(evt);
            int actionEndFrame = Math.Max(0, actionDurationFrames - 1);
            int oldStart = Mathf.Clamp(evt.Frame, 0, actionEndFrame);
            int oldEnd = Mathf.Clamp(Math.Max(oldStart, evt.EndFrame), oldStart, actionEndFrame);
            int requestedStart = Mathf.Clamp(
                Math.Min(requestedStartFrame, requestedEndFrame),
                0,
                actionEndFrame);
            int requestedEnd = Mathf.Clamp(
                Math.Max(requestedStart, Math.Max(requestedStartFrame, requestedEndFrame)),
                requestedStart,
                actionEndFrame);
            bool startChanged = requestedStart != oldStart;
            bool endChanged = requestedEnd != oldEnd;
            bool moved = startChanged
                && endChanged
                && requestedEnd - requestedStart == oldEnd - oldStart;

            if (moved)
            {
                evt.Frame = requestedStart;
                evt.EndFrame = requestedEnd;
                return;
            }

            if (!TryResolveSourceClipFrames(evt, characterStableId, out int sourceFrames))
            {
                evt.Frame = requestedStart;
                evt.EndFrame = requestedEnd;
                return;
            }

            NormalizeOffsets(evt, sourceFrames);
            if (trimSource)
            {
                ApplyTrim(
                    evt,
                    sourceFrames,
                    requestedStart,
                    requestedEnd,
                    startChanged,
                    endChanged,
                    actionEndFrame);
                return;
            }

            int playableFrames = ComputePlayableSourceFrames(sourceFrames, evt);
            int requestedSpan = Math.Max(1, requestedEnd - requestedStart + 1);
            evt.PlaybackSpeed = ComputePlaybackSpeed(playableFrames, requestedSpan);
            int actualSpan = ComputeTimelineSpanFrames(playableFrames, evt.PlaybackSpeed);
            if (startChanged && !endChanged)
            {
                evt.EndFrame = oldEnd;
                evt.Frame = Math.Max(0, oldEnd - actualSpan + 1);
            }
            else
            {
                evt.Frame = oldStart;
                evt.EndFrame = Math.Min(actionEndFrame, oldStart + actualSpan - 1);
            }
        }

        public static void NormalizeOffsets(AnimationTrackEventJsonDto evt, int sourceFrames)
        {
            if (evt == null)
                return;

            int source = Math.Max(1, sourceFrames);
            evt.AnimationStartOffsetFrames = Mathf.Clamp(
                evt.AnimationStartOffsetFrames,
                0,
                source - 1);
            evt.AnimationEndOffsetFrames = Mathf.Clamp(
                evt.AnimationEndOffsetFrames,
                0,
                source - evt.AnimationStartOffsetFrames - 1);
        }

        private static bool TryResolveClipFrames(CharacterActionAnimationClipChoice choice, out int clipFrames)
        {
            clipFrames = 0;
            if (!CharacterActionPreviewBindingDiscovery.TryLoadAnimationClip(choice, out AnimationClip clip))
                return false;

            clipFrames = ComputeClipFrames(clip);
            return true;
        }

        private static bool TryResolveClipFrames(MxAnimationClipRegistryAsset registry, string clipId, out int clipFrames)
        {
            clipFrames = 0;
            if (registry == null || string.IsNullOrWhiteSpace(clipId))
                return false;

            MxAnimationClipRegistryClipEntry[] clips = registry.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                MxAnimationClipRegistryClipEntry entry = clips[i];
                if (!string.Equals(entry.ClipId, clipId, StringComparison.Ordinal))
                    continue;

                AnimationClip clip = entry.Clip;
                if (clip == null)
                    continue;

                clipFrames = ComputeClipFrames(clip);
                return true;
            }

            return false;
        }

        private static int ComputeClipFrames(AnimationClip clip)
        {
            if (clip == null)
                return 0;

            return Math.Max(1, Mathf.CeilToInt(clip.length * CharacterActionTimelineLayout.EditorTimelineFps));
        }

        private static void ApplyTrim(
            AnimationTrackEventJsonDto evt,
            int sourceFrames,
            int requestedStart,
            int requestedEnd,
            bool startChanged,
            bool endChanged,
            int actionEndFrame)
        {
            float speed = ClampPlaybackSpeed(evt.PlaybackSpeed);
            evt.PlaybackSpeed = speed;
            if (startChanged && !endChanged)
            {
                int requestedSpan = Math.Max(1, evt.EndFrame - requestedStart + 1);
                int targetPlayable = Mathf.Clamp(
                    Mathf.RoundToInt(requestedSpan * speed),
                    1,
                    sourceFrames - evt.AnimationEndOffsetFrames);
                evt.AnimationStartOffsetFrames =
                    sourceFrames - evt.AnimationEndOffsetFrames - targetPlayable;
                int actualSpan = ComputeTimelineSpanFrames(targetPlayable, speed);
                evt.Frame = Math.Max(0, evt.EndFrame - actualSpan + 1);
                return;
            }

            if (endChanged)
            {
                int requestedSpan = Math.Max(1, requestedEnd - evt.Frame + 1);
                int targetPlayable = Mathf.Clamp(
                    Mathf.RoundToInt(requestedSpan * speed),
                    1,
                    sourceFrames - evt.AnimationStartOffsetFrames);
                evt.AnimationEndOffsetFrames =
                    sourceFrames - evt.AnimationStartOffsetFrames - targetPlayable;
                int actualSpan = ComputeTimelineSpanFrames(targetPlayable, speed);
                evt.EndFrame = Math.Min(actionEndFrame, evt.Frame + actualSpan - 1);
            }
        }

        public static float ClampPlaybackSpeed(float playbackSpeed)
        {
            if (float.IsNaN(playbackSpeed) || float.IsInfinity(playbackSpeed))
                return 1f;
            return Mathf.Clamp(playbackSpeed, MinPlaybackSpeed, MaxPlaybackSpeed);
        }

        private static void NormalizeEvent(AnimationTrackEventJsonDto evt)
        {
            evt.PlaybackSpeed = ClampPlaybackSpeed(evt.PlaybackSpeed);
            evt.AnimationStartOffsetFrames = Math.Max(0, evt.AnimationStartOffsetFrames);
            evt.AnimationEndOffsetFrames = Math.Max(0, evt.AnimationEndOffsetFrames);
            if (evt.EndFrame < evt.Frame)
                evt.EndFrame = evt.Frame;
        }

        private readonly struct AnimationSourceLookupKey : IEquatable<AnimationSourceLookupKey>
        {
            public AnimationSourceLookupKey(string animationKey, string characterStableId)
            {
                AnimationKey = animationKey ?? string.Empty;
                CharacterStableId = characterStableId ?? string.Empty;
            }

            private string AnimationKey { get; }
            private string CharacterStableId { get; }

            public bool Equals(AnimationSourceLookupKey other)
            {
                return string.Equals(AnimationKey, other.AnimationKey, StringComparison.Ordinal)
                    && string.Equals(CharacterStableId, other.CharacterStableId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is AnimationSourceLookupKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = StringComparer.Ordinal.GetHashCode(AnimationKey);
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(CharacterStableId);
                    return hash;
                }
            }
        }
    }

    internal sealed class CharacterActionAnimationTimelineCachePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (HasChanges(importedAssets)
                || HasChanges(deletedAssets)
                || HasChanges(movedAssets)
                || HasChanges(movedFromAssetPaths))
            {
                CharacterActionAnimationTimelineResolver.InvalidateSourceFrameCache();
            }
        }

        private static bool HasChanges(string[] paths)
        {
            return paths != null && paths.Length > 0;
        }
    }
}

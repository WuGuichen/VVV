using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Resources;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    /// <summary>Resolves editor-only VFX source timing and applies clip-style retime/trim operations.</summary>
    /// <remarks>Editor-only, LowFreqAlloc while loading the catalog; drag math is NoAlloc.</remarks>
    internal static class CharacterActionVfxTimelineResolver
    {
        private const string CatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        private static readonly Dictionary<ResourceKey, int> SourceFramesByResource =
            new Dictionary<ResourceKey, int>();
        private static bool _loaded;

        public static bool TryResolveSourceFrames(string serializedResourceKey, out int sourceFrames)
        {
            sourceFrames = 0;
            if (!CharacterActionVfxSelectionCatalog.TryParseResourceKey(serializedResourceKey, out ResourceKey key))
                return false;

            EnsureLoaded();
            return SourceFramesByResource.TryGetValue(key, out sourceFrames) && sourceFrames > 0;
        }

        internal static void InvalidateCatalog()
        {
            _loaded = false;
        }

        public static int ComputePlayableSourceFrames(int sourceFrames, int startOffsetFrames, int endOffsetFrames)
        {
            int source = Math.Max(1, sourceFrames);
            int start = Mathf.Clamp(startOffsetFrames, 0, source - 1);
            int end = Mathf.Clamp(endOffsetFrames, 0, source - start - 1);
            return Math.Max(1, source - start - end);
        }

        public static int ComputeTimelineSpanFrames(int playableSourceFrames, float playbackRate)
        {
            float safeRate = Mathf.Clamp(
                IsFinite(playbackRate) ? playbackRate : 1f,
                CharacterActionVfxEventConfig.MinPlaybackRate,
                CharacterActionVfxEventConfig.MaxPlaybackRate);
            return Math.Max(1, Mathf.CeilToInt(Math.Max(1, playableSourceFrames) / safeRate));
        }

        public static float ComputePlaybackRate(int playableSourceFrames, int timelineSpanFrames)
        {
            float rate = Math.Max(1, playableSourceFrames) / (float)Math.Max(1, timelineSpanFrames);
            return Mathf.Clamp(
                rate,
                CharacterActionVfxEventConfig.MinPlaybackRate,
                CharacterActionVfxEventConfig.MaxPlaybackRate);
        }

        public static bool SynchronizeSpan(PresentationTrackEventJsonDto evt)
        {
            if (!IsFiniteRetimeMode(evt) || !TryResolveSourceFrames(evt.ResourceKey, out int sourceFrames))
                return false;

            NormalizeOffsets(evt, sourceFrames);
            int playable = ComputePlayableSourceFrames(
                sourceFrames,
                evt.VfxStartOffsetFrames,
                evt.VfxEndOffsetFrames);
            int span = ComputeTimelineSpanFrames(playable, evt.VfxPlaybackRate);
            int endFrame = checked(evt.Frame + span - 1);
            if (evt.EndFrame == endFrame)
                return false;

            evt.EndFrame = endFrame;
            return true;
        }

        public static void ApplyRange(
            PresentationTrackEventJsonDto evt,
            int requestedStartFrame,
            int requestedEndFrame,
            bool trimSource)
        {
            if (evt == null)
                return;

            int oldStart = Math.Max(0, evt.Frame);
            int oldEnd = Math.Max(oldStart, evt.EndFrame);
            int requestedStart = Math.Max(0, Math.Min(requestedStartFrame, requestedEndFrame));
            int requestedEnd = Math.Max(requestedStart, Math.Max(requestedStartFrame, requestedEndFrame));
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

            bool hasFiniteSource = TryResolveSourceFrames(evt.ResourceKey, out int sourceFrames);
            if (!hasFiniteSource)
            {
                evt.Frame = requestedStart;
                evt.EndFrame = requestedEnd;
                return;
            }

            NormalizeOffsets(evt, sourceFrames);
            if (trimSource)
            {
                ApplyTrim(evt, sourceFrames, requestedStart, requestedEnd, startChanged, endChanged);
                return;
            }

            if (!IsFiniteRetimeMode(evt))
            {
                evt.Frame = requestedStart;
                evt.EndFrame = requestedEnd;
                return;
            }

            int playable = ComputePlayableSourceFrames(
                sourceFrames,
                evt.VfxStartOffsetFrames,
                evt.VfxEndOffsetFrames);
            int requestedSpan = Math.Max(1, requestedEnd - requestedStart + 1);
            evt.VfxPlaybackRate = ComputePlaybackRate(playable, requestedSpan);
            int actualSpan = ComputeTimelineSpanFrames(playable, evt.VfxPlaybackRate);
            if (startChanged && !endChanged)
            {
                evt.EndFrame = oldEnd;
                evt.Frame = Math.Max(0, oldEnd - actualSpan + 1);
            }
            else
            {
                evt.Frame = oldStart;
                evt.EndFrame = checked(oldStart + actualSpan - 1);
            }
        }

        public static void NormalizeOffsets(PresentationTrackEventJsonDto evt, int sourceFrames)
        {
            if (evt == null)
                return;

            int source = Math.Max(1, sourceFrames);
            evt.VfxStartOffsetFrames = Mathf.Clamp(evt.VfxStartOffsetFrames, 0, source - 1);
            evt.VfxEndOffsetFrames = Mathf.Clamp(
                evt.VfxEndOffsetFrames,
                0,
                source - evt.VfxStartOffsetFrames - 1);
        }

        public static bool IsFiniteRetimeMode(PresentationTrackEventJsonDto evt)
        {
            if (evt == null
                || !Enum.TryParse(
                    evt.VfxPlaybackMode,
                    ignoreCase: true,
                    out CharacterActionVfxPlaybackMode mode))
            {
                return true;
            }

            return mode != CharacterActionVfxPlaybackMode.LoopUntilEventEnd;
        }

        public static bool IsPlayToCompletion(PresentationTrackEventJsonDto evt)
        {
            if (evt == null
                || !Enum.TryParse(
                    evt.VfxPlaybackMode,
                    ignoreCase: true,
                    out CharacterActionVfxPlaybackMode mode))
            {
                return true;
            }

            return mode == CharacterActionVfxPlaybackMode.PlayToCompletion;
        }

        private static void ApplyTrim(
            PresentationTrackEventJsonDto evt,
            int sourceFrames,
            int requestedStart,
            int requestedEnd,
            bool startChanged,
            bool endChanged)
        {
            float rate = Mathf.Clamp(
                IsFinite(evt.VfxPlaybackRate) ? evt.VfxPlaybackRate : 1f,
                CharacterActionVfxEventConfig.MinPlaybackRate,
                CharacterActionVfxEventConfig.MaxPlaybackRate);
            if (startChanged && !endChanged)
            {
                int requestedSpan = Math.Max(1, evt.EndFrame - requestedStart + 1);
                int targetPlayable = Mathf.Clamp(
                    Mathf.RoundToInt(requestedSpan * rate),
                    1,
                    sourceFrames - evt.VfxEndOffsetFrames);
                evt.VfxStartOffsetFrames = sourceFrames - evt.VfxEndOffsetFrames - targetPlayable;
                int actualSpan = ComputeTimelineSpanFrames(targetPlayable, rate);
                evt.Frame = Math.Max(0, evt.EndFrame - actualSpan + 1);
                return;
            }

            if (endChanged)
            {
                int requestedSpan = Math.Max(1, requestedEnd - evt.Frame + 1);
                int targetPlayable = Mathf.Clamp(
                    Mathf.RoundToInt(requestedSpan * rate),
                    1,
                    sourceFrames - evt.VfxStartOffsetFrames);
                evt.VfxEndOffsetFrames = sourceFrames - evt.VfxStartOffsetFrames - targetPlayable;
                int actualSpan = ComputeTimelineSpanFrames(targetPlayable, rate);
                evt.EndFrame = checked(evt.Frame + actualSpan - 1);
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            SourceFramesByResource.Clear();
            if (!File.Exists(CatalogPath))
                return;

            try
            {
                JObject root = JObject.Parse(File.ReadAllText(CatalogPath));
                JArray effects = root["effects"] as JArray;
                for (int i = 0; effects != null && i < effects.Count; i++)
                {
                    if (!(effects[i] is JObject effect)
                        || !(effect["resourceKey"] is JObject keyFields)
                        || !(effect["lifecycle"] is JObject lifecycle))
                    {
                        continue;
                    }

                    var key = new ResourceKey(
                        keyFields.Value<string>("id") ?? string.Empty,
                        keyFields.Value<string>("typeId") ?? ResourceTypeIds.GameObject,
                        keyFields.Value<string>("variant") ?? string.Empty,
                        keyFields.Value<string>("packageId") ?? string.Empty);
                    int frames = lifecycle.Value<int?>("defaultLifetimeFrames") ?? 0;
                    if (key.IsValid)
                        SourceFramesByResource[key] = Math.Max(0, frames);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is Newtonsoft.Json.JsonException)
            {
                SourceFramesByResource.Clear();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal sealed class CharacterActionVfxTimelineCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsCatalog(importedAssets)
                || ContainsCatalog(deletedAssets)
                || ContainsCatalog(movedAssets)
                || ContainsCatalog(movedFromAssetPaths))
            {
                CharacterActionVfxTimelineResolver.InvalidateCatalog();
            }
        }

        private static bool ContainsCatalog(string[] paths)
        {
            for (int i = 0; paths != null && i < paths.Length; i++)
            {
                if (string.Equals(
                    paths[i],
                    "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json",
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [InitializeOnLoad]
    internal static class CharacterActionVfxTimingValidationRegistration
    {
        static CharacterActionVfxTimingValidationRegistration()
        {
            CharacterActionAuthoringLocalValidationRegistry.Register(
                new CharacterActionVfxTimingValidationProvider());
        }
    }

    internal sealed class CharacterActionVfxTimingValidationProvider :
        ICharacterActionAuthoringLocalValidationProvider
    {
        internal const string MissingFiniteLifetimeCode = "CA_VFX_FINITE_LIFETIME_REQUIRED";
        internal const string InvalidSourceTrimCode = "CA_VFX_SOURCE_TRIM_OUT_OF_RANGE";

        /// <remarks>AllocByDesign: editor-only validation pass.</remarks>
        public CharacterActionValidationDiagnostic[] Validate(
            CharacterActionAuthoringLocalValidationContext context)
        {
            if (context?.Model == null)
                return Array.Empty<CharacterActionValidationDiagnostic>();

            var diagnostics = new List<CharacterActionValidationDiagnostic>();
            List<CharacterActionJsonDto> actions = context.Model.Actions;
            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                CharacterActionJsonDto action = actions[actionIndex];
                PresentationTrackEventJsonDto[] events =
                    action?.PresentationTrack?.Events ?? Array.Empty<PresentationTrackEventJsonDto>();
                for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
                {
                    PresentationTrackEventJsonDto evt = events[eventIndex];
                    if (evt == null
                        || !string.Equals(
                            evt.Kind,
                            CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string stablePath = "$.actions["
                        + (action.ActionId <= 0
                            ? actionIndex.ToString()
                            : action.ActionId.ToString())
                        + "].presentationTrack.events["
                        + eventIndex
                        + "]";
                    if (!CharacterActionVfxTimelineResolver.TryResolveSourceFrames(
                            evt.ResourceKey,
                            out int sourceFrames))
                    {
                        if (CharacterActionVfxTimelineResolver.IsPlayToCompletion(evt))
                        {
                            diagnostics.Add(new CharacterActionValidationDiagnostic(
                                "CharacterActionAuthoring",
                                stablePath + ".resourceKey",
                                MissingFiniteLifetimeCode,
                                CharacterActionDiagnosticSeverity.Error,
                                "PlayToCompletion requires a VFX resource with defaultLifetimeFrames > 0."));
                        }

                        continue;
                    }

                    int startOffset = Math.Max(0, evt.VfxStartOffsetFrames);
                    int endOffset = Math.Max(0, evt.VfxEndOffsetFrames);
                    if (startOffset + endOffset >= sourceFrames)
                    {
                        diagnostics.Add(new CharacterActionValidationDiagnostic(
                            "CharacterActionAuthoring",
                            stablePath,
                            InvalidSourceTrimCode,
                            CharacterActionDiagnosticSeverity.Error,
                            "VFX source trims must leave at least one source frame."));
                    }
                }
            }

            return diagnostics.ToArray();
        }
    }
}

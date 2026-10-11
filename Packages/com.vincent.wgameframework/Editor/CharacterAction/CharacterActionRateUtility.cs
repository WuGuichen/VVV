using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using Newtonsoft.Json;

namespace MxFramework.Editor.CharacterAction
{
    /// <summary>Editor-only, allocating retime transaction over an isolated action draft.</summary>
    internal static class CharacterActionRateUtility
    {
        internal static bool TryCreateScaledAction(CharacterActionJsonDto source, float rate,
            out CharacterActionJsonDto result, out string error)
        {
            result = null;
            error = string.Empty;
            if (source == null || !source.DurationFrames.HasValue || source.DurationFrames.Value < 1)
            {
                error = "请先为动作设置有效时长。";
                return false;
            }
            if (float.IsNaN(rate) || float.IsInfinity(rate) || rate < 0.05f || rate > 16f)
            {
                error = "整体速率必须在 0.05–16 之间。";
                return false;
            }
            if (!string.IsNullOrEmpty(source.TimelineAuthority)
                && source.TimelineAuthority != nameof(CharacterActionTimelineAuthority.CharacterAuthored))
            {
                error = "该动作由 Combat 时间轴控制，请在其权威来源调整时序。";
                return false;
            }

            try
            {
                var draft = JsonConvert.DeserializeObject<CharacterActionJsonDto>(
                    JsonConvert.SerializeObject(source, CharacterActionAuthoringJsonService.JsonSettings),
                    CharacterActionAuthoringJsonService.JsonSettings);
                // Use the entered decimal rate, not float representation noise at frame boundaries.
                Scale(draft, double.Parse(rate.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
                result = draft;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
            catch (OverflowException)
            {
                error = "缩放后的帧数超出支持范围。";
                return false;
            }
        }

        internal static void CopyTiming(CharacterActionJsonDto source, CharacterActionJsonDto target)
        {
            target.DurationFrames = source.DurationFrames;
            target.Phases = source.Phases;
            target.CancelRules = source.CancelRules;
            target.CohesionSupport = source.CohesionSupport;
            target.MotionTrack = source.MotionTrack;
            target.CombatTrack = source.CombatTrack;
            target.GameplayTrack = source.GameplayTrack;
            target.AnimationTrack = source.AnimationTrack;
            target.PresentationTrack = source.PresentationTrack;
            target.DebugTrack = source.DebugTrack;
        }

        private static void Scale(CharacterActionJsonDto action, double rate)
        {
            int oldDuration = action.DurationFrames.Value;
            action.DurationFrames = Boundary(oldDuration, rate);
            foreach (var phase in action.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>())
            {
                if (phase == null) continue;
                Range(phase.StartFrame, phase.EndFrame, rate, out int start, out int end);
                phase.StartFrame = start;
                phase.EndFrame = end;
            }
            foreach (var rule in action.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>())
            {
                if (rule == null) continue;
                Range(rule.StartFrame, rule.EndFrame, rate, out int start, out int end);
                rule.StartFrame = start;
                rule.EndFrame = end;
            }
            var support = action.CohesionSupport;
            if (support != null && support.HasCohesionSupportWindow)
            {
                Range(support.CohesionSupportWindowStartFrame, support.CohesionSupportWindowEndFrame,
                    rate, out int start, out int end);
                support.CohesionSupportWindowStartFrame = start;
                support.CohesionSupportWindowEndFrame = end;
            }
            foreach (var evt in action.MotionTrack?.Events ?? Array.Empty<MotionTrackEventJsonDto>())
            {
                if (evt == null) continue;
                if (evt.ShouldSerializeEndFrame())
                {
                    Range(evt.Frame, evt.EndFrame, rate, out int start, out int end);
                    evt.Frame = start;
                    evt.EndFrame = end;
                }
                else evt.Frame = Point(evt.Frame, oldDuration, rate);
                evt.CollisionTransitionFrames = Boundary(evt.CollisionTransitionFrames, rate);
            }
            foreach (var evt in action.CombatTrack?.Events ?? Array.Empty<CombatTrackEventJsonDto>())
            {
                if (evt == null) continue;
                if (evt.ShouldSerializeEndFrame())
                {
                    Range(evt.Frame, evt.EndFrame, rate, out int start, out int end);
                    evt.Frame = start;
                    evt.EndFrame = end;
                }
                else evt.Frame = Point(evt.Frame, oldDuration, rate);
            }
            foreach (var evt in action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>())
            {
                if (evt == null) continue;
                Range(evt.Frame, evt.EndFrame, rate, out int start, out int end);
                evt.Frame = start;
                evt.EndFrame = end;
                evt.PlaybackSpeed = PlaybackRate(evt.PlaybackSpeed, rate);
                evt.TransitionSeconds = checked((float)(evt.TransitionSeconds / rate));
            }
            foreach (var evt in action.PresentationTrack?.Events ?? Array.Empty<PresentationTrackEventJsonDto>())
            {
                if (evt == null) continue;
                // Audio cues may omit EndFrame; their point event is not a duration window.
                if (evt.Kind == nameof(CharacterActionTrackEventKind.SpawnVisualCue))
                {
                    Range(evt.Frame, Math.Max(evt.Frame, evt.EndFrame), rate, out int start, out int end);
                    evt.Frame = start;
                    evt.EndFrame = end;
                    evt.VfxPlaybackRate = PlaybackRate(evt.VfxPlaybackRate, rate);
                }
                else if (evt.EndFrame > evt.Frame)
                {
                    Range(evt.Frame, evt.EndFrame, rate, out int start, out int end);
                    evt.Frame = start;
                    evt.EndFrame = end;
                }
                else
                {
                    evt.Frame = Point(evt.Frame, oldDuration, rate);
                    evt.EndFrame = evt.Frame;
                }
            }
            foreach (var evt in action.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEventJsonDto>())
                if (evt != null) evt.Frame = Point(evt.Frame, oldDuration, rate);
            foreach (var evt in action.DebugTrack?.Events ?? Array.Empty<DebugTrackEventJsonDto>())
                if (evt != null) evt.Frame = Point(evt.Frame, oldDuration, rate);
            if (action.MotionTrack?.RootMotion != null)
                action.MotionTrack.RootMotion.Frames = Resample(action.MotionTrack.RootMotion.Frames, rate);
        }

        private static float PlaybackRate(float value, double rate)
        {
            double scaled = value * rate;
            if (double.IsNaN(scaled) || double.IsInfinity(scaled) || scaled < 0.05 || scaled > 16)
                throw new ArgumentException("有动画或特效缩放后超出 0.05–16 倍速范围，请减小调整幅度。");
            return (float)scaled;
        }

        private static int Boundary(int frame, double rate)
        {
            if (frame < 0) throw new ArgumentException("动作包含负数时间，请先修正后再缩放。");
            // A shared boundary mapping keeps adjacent inclusive windows adjacent.
            return checked((int)Math.Ceiling(frame / rate - 0.00001));
        }

        private static int Point(int frame, int duration, double rate)
        {
            return Math.Min(Boundary(frame, rate), Boundary(duration, rate) - 1);
        }

        private static void Range(int start, int end, double rate, out int mappedStart, out int mappedEnd)
        {
            if (end < start) throw new ArgumentException("动作包含结束帧早于开始帧的事件，请先修正后再缩放。");
            mappedStart = Boundary(start, rate);
            mappedEnd = Boundary(checked(end + 1), rate) - 1;
            if (mappedEnd < mappedStart)
                throw new ArgumentException("该倍率会使短事件或阶段不足一帧，请减小加速倍率。");
        }

        private static RootMotionFrameJsonDto[] Resample(RootMotionFrameJsonDto[] frames, double rate)
        {
            if (frames == null || frames.Length == 0) return Array.Empty<RootMotionFrameJsonDto>();
            var samples = new SortedDictionary<int, RootMotionFrameJsonDto>();
            foreach (var frame in frames)
            {
                if (frame == null) continue;
                if (frame.Frame < 0) throw new ArgumentException("RootMotion 包含负数帧。");
                double start = frame.Frame / rate;
                double end = (frame.Frame + 1.0) / rate;
                int first = checked((int)Math.Floor(start + 0.00001));
                int last = Boundary(checked(frame.Frame + 1), rate) - 1;
                double assignedWeight = 0;
                for (int index = first; index <= last; index++)
                {
                    double weight = index == last ? 1 - assignedWeight
                        : (Math.Min(end, index + 1.0) - Math.Max(start, index)) * rate;
                    if (weight <= 0) continue;
                    assignedWeight += weight;
                    if (!samples.TryGetValue(index, out RootMotionFrameJsonDto sample))
                    {
                        sample = new RootMotionFrameJsonDto { Frame = index };
                        samples.Add(index, sample);
                    }
                    sample.X += (float)(frame.X * weight);
                    sample.Y = 0f;
                    sample.Z += (float)(frame.Z * weight);
                }
            }
            var result = new RootMotionFrameJsonDto[samples.Count];
            samples.Values.CopyTo(result, 0);
            return result;
        }
    }
}

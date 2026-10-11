using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Editor
{
    internal sealed class VfxSemanticCompileContext
    {
        public VfxSemanticPropertyRegistryView Registry;
        public VfxComponentProfileView ComponentProfile;
        public IReadOnlyList<VfxComponentProfileView> ComponentProfiles = Array.Empty<VfxComponentProfileView>();
        public VfxElementProfileView ElementProfile;
        public IReadOnlyList<VfxTimeEventView> Events = Array.Empty<VfxTimeEventView>();
        public int FramesPerSecond = 60;
        public bool AllowAutoSuggestedProfile;
    }

    /// <summary>Compiles author intent into deterministic frame segments and event positions.</summary>
    /// <remarks>Editor-only, AllocByDesign. It has no Unity object side effects.</remarks>
    internal static class VfxSemanticRetimeCompiler
    {
        private static readonly HashSet<string> LegacyDerivedTimingPropertyIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "timing.preRoll.frames",
            "timing.entry.frames",
            "timing.body.frames",
            "timing.exit.frames",
            "timing.cleanup.frames",
            "timing.cycle.frames"
        };

        public static VfxCompiledLayerView Compile(VfxCompositionLayerView layer, VfxSemanticCompileContext context)
        {
            var result = new VfxCompiledLayerView
            {
                LayerId = layer?.LayerId ?? string.Empty,
                StartFrame = Math.Max(0, layer?.StartFrame ?? 0)
            };
            if (layer == null)
            {
                Error(result, "VFXSEM001", "Layer 不存在。");
                Finalize(result);
                return result;
            }
            if (context?.Registry == null)
                Error(result, "VFXSEM002", "Property Registry 未加载。", fixAction: "ReloadRegistry");

            IReadOnlyList<VfxComponentProfileView> profiles = context?.ComponentProfiles;
            if (profiles == null || profiles.Count == 0)
                profiles = context?.ComponentProfile == null
                    ? Array.Empty<VfxComponentProfileView>()
                    : new[] { context.ComponentProfile };
            VfxComponentProfileView profile = context?.ComponentProfile ?? profiles.FirstOrDefault();
            VfxTimeSegmentsView source = ResolveSourceSegments(layer, profile);
            VfxLayerTimingView timing = layer.Timing ?? new VfxLayerTimingView();
            if (timing.MinimumBodyFrames > 0)
                source.MinimumBodyFrames = Mathf.Clamp(timing.MinimumBodyFrames, 1, Math.Max(1, source.BodyFrames));
            result.SourceSegments = source.Clone();
            result.TargetSegments = source.Clone();

            if (profiles.Count == 0)
            {
                Warning(result, "VFXPRO001", "当前成员没有 Component Profile；仅允许源效果预览，语义写入缺少精确绑定。", fixAction: "CreateProfile");
            }
            else
            {
                for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
                {
                    VfxComponentProfileView memberProfile = profiles[profileIndex];
                    if (memberProfile.Status == nameof(VfxProfileReviewState.Stale))
                        Error(result, "VFXPRO002", "Component Profile 已过期：" + memberProfile.ProfileId, memberProfile.SourceComponentId, fixAction: "RemapProfile");
                    else if (memberProfile.Status == nameof(VfxProfileReviewState.Deprecated))
                        Error(result, "VFXPRO003", "Component Profile 已弃用，不能用于新编译：" + memberProfile.ProfileId, memberProfile.SourceComponentId, fixAction: "ChooseProfile");
                    else if (memberProfile.Status == nameof(VfxProfileReviewState.AutoSuggested) && !(context?.AllowAutoSuggestedProfile ?? false))
                        Warning(result, "VFXPRO004", "Component Profile 尚未人工确认；可预览，但不能晋升：" + memberProfile.ProfileId, memberProfile.SourceComponentId, fixAction: "ConfirmProfile");
                    List<string> unconditionalBlockers = (memberProfile.Blockers ?? new List<string>())
                        .Where(value => !IsCoupledParticleModule(value))
                        .ToList();
                    if (unconditionalBlockers.Count > 0)
                        Error(result, "VFXPRO005", "Profile 包含阻断模块：" + string.Join("、", unconditionalBlockers), memberProfile.SourceComponentId, fixAction: "InspectRawModules");
                }
            }

            if (!Enum.TryParse(timing.RetimeMode, false, out VfxRetimeMode mode))
            {
                Error(result, "VFXRET001", "未知重定时模式：" + timing.RetimeMode, fixAction: "ChooseRetimeMode");
                mode = VfxRetimeMode.Fixed;
            }
            int target = timing.TargetArtisticFrames > 0 ? timing.TargetArtisticFrames : source.ArtisticFrames;
            switch (mode)
            {
                case VfxRetimeMode.PlaybackWindow:
                    CompilePlaybackWindow(target, result);
                    break;
                case VfxRetimeMode.ExtendBody:
                    CompileExtendBody(source, target, result);
                    break;
                case VfxRetimeMode.ExtendTail:
                    CompileExtendTail(source, target, timing, profile, result);
                    break;
                case VfxRetimeMode.RepeatBody:
                    CompileRepeatBody(source, target, timing, result);
                    break;
                case VfxRetimeMode.EventRemap:
                    CompileEventRemap(source, target, timing, result);
                    break;
                case VfxRetimeMode.WholeSpeed:
                    CompileWholeSpeed(source, target, result);
                    break;
                case VfxRetimeMode.Fixed:
                    CompileFixed(source, target, result);
                    break;
            }

            BuildTimeMap(timing, mode, result);

            IReadOnlyList<VfxTimeEventView> events = context?.Events;
            if (events == null) events = profile?.Events;
            if (events == null) events = Array.Empty<VfxTimeEventView>();
            CompileEvents(events, timing, mode, result);
            CompileSemanticOverrides(layer, context, profiles, result);
            ValidateCoupledParticleModules(layer, mode, context?.ElementProfile, profiles, result);
            if (mode != VfxRetimeMode.PlaybackWindow
                && result.TargetSegments.ArtisticFrames < result.TargetSegments.MinimumArtisticFrames)
                Error(result, "VFXRET002", "目标艺术长度小于不可压缩的最短长度。", fixAction: "SetMinimumLength");
            Finalize(result);
            return result;
        }

        public static int MapFrame(int sourceFrame, VfxTimeSegmentsView source, VfxTimeSegmentsView target, VfxTimeEventAnchor anchor, int cycleFrames = 0)
        {
            int sourceArtistic = Math.Max(1, source.ArtisticFrames);
            int targetArtistic = Math.Max(1, target.ArtisticFrames);
            switch (anchor)
            {
                case VfxTimeEventAnchor.StartLocked:
                    return Mathf.Clamp(sourceFrame, 0, targetArtistic);
                case VfxTimeEventAnchor.EndLocked:
                    return Mathf.Clamp(targetArtistic - (sourceArtistic - sourceFrame), 0, targetArtistic);
                case VfxTimeEventAnchor.BodyNormalized:
                {
                    int sourceBodyStart = source.PreRollFrames + source.EntryFrames;
                    int targetBodyStart = target.PreRollFrames + target.EntryFrames;
                    float normalized = source.BodyFrames <= 0 ? 0f : (sourceFrame - sourceBodyStart) / (float)source.BodyFrames;
                    return targetBodyStart + Mathf.RoundToInt(Mathf.Clamp01(normalized) * target.BodyFrames);
                }
                case VfxTimeEventAnchor.CycleLocal:
                {
                    int cycle = Math.Max(1, cycleFrames > 0 ? cycleFrames : source.CycleFrames);
                    int sourceBodyStart = source.PreRollFrames + source.EntryFrames;
                    int targetBodyStart = target.PreRollFrames + target.EntryFrames;
                    int local = Math.Max(0, sourceFrame - sourceBodyStart) % cycle;
                    return targetBodyStart + local;
                }
                default:
                    return sourceFrame;
            }
        }

        public static AnimationCurve RemapCurve(AnimationCurve sourceCurve, IReadOnlyList<VfxRetimeMapPointView> map)
        {
            if (sourceCurve == null) return null;
            if (!ValidateMap(map, out _)) return new AnimationCurve(sourceCurve.keys);
            Keyframe[] sourceKeys = sourceCurve.keys;
            var targetKeys = new Keyframe[sourceKeys.Length];
            for (int i = 0; i < sourceKeys.Length; i++)
            {
                Keyframe key = sourceKeys[i];
                float targetTime = MapPiecewise(key.time, map, out float sourcePerTargetSlope);
                key.time = targetTime;
                key.inTangent *= sourcePerTargetSlope;
                key.outTangent *= sourcePerTargetSlope;
                targetKeys[i] = key;
            }
            return new AnimationCurve(targetKeys)
            {
                preWrapMode = sourceCurve.preWrapMode,
                postWrapMode = sourceCurve.postWrapMode
            };
        }

        public static AnimationCurve RemapNormalizedCurve(
            AnimationCurve sourceCurve,
            IReadOnlyList<VfxRetimeMapPointView> frameMap,
            int sourceFrames,
            int targetFrames)
        {
            return RemapNormalizedCurve(sourceCurve, frameMap, sourceFrames, targetFrames, 0, 0);
        }

        public static AnimationCurve RemapNormalizedCurve(
            AnimationCurve sourceCurve,
            IReadOnlyList<VfxRetimeMapPointView> frameMap,
            int sourceFrames,
            int targetFrames,
            int sourceOffsetFrames,
            int targetOffsetFrames)
        {
            if (sourceCurve == null) return null;
            if (!ValidateMap(frameMap, out _) || sourceFrames <= 0 || targetFrames <= 0)
                return new AnimationCurve(sourceCurve.keys) { preWrapMode = sourceCurve.preWrapMode, postWrapMode = sourceCurve.postWrapMode };
            Keyframe[] sourceKeys = sourceCurve.keys;
            var targetKeys = new Keyframe[sourceKeys.Length];
            for (int i = 0; i < sourceKeys.Length; i++)
            {
                Keyframe key = sourceKeys[i];
                float sourceFrame = sourceOffsetFrames + Mathf.Clamp01(key.time) * sourceFrames;
                float targetFrame = MapPiecewise(sourceFrame, frameMap, out float sourceFramePerTargetFrame);
                key.time = Mathf.Clamp01((targetFrame - targetOffsetFrames) / targetFrames);
                float normalizedSlope = sourceFramePerTargetFrame * targetFrames / sourceFrames;
                key.inTangent *= normalizedSlope;
                key.outTangent *= normalizedSlope;
                targetKeys[i] = key;
            }
            return new AnimationCurve(targetKeys)
            {
                preWrapMode = sourceCurve.preWrapMode,
                postWrapMode = sourceCurve.postWrapMode
            };
        }

        public static bool ValidateMap(IReadOnlyList<VfxRetimeMapPointView> map, out string message)
        {
            if (map == null || map.Count < 2)
            {
                message = "事件重排至少需要两个映射点。";
                return false;
            }
            for (int i = 1; i < map.Count; i++)
            {
                if (map[i].SourceFrame <= map[i - 1].SourceFrame || map[i].TargetFrame <= map[i - 1].TargetFrame)
                {
                    message = "事件重排的 source/target 帧必须严格递增。";
                    return false;
                }
            }
            message = string.Empty;
            return true;
        }

        private static VfxTimeSegmentsView ResolveSourceSegments(VfxCompositionLayerView layer, VfxComponentProfileView profile)
        {
            VfxTimeSegmentsView source = layer.Timing?.SourceSegments?.Clone() ?? profile?.Timing?.ToSegments();
            if (source != null)
            {
                source.BodyFrames = Math.Max(1, source.BodyFrames);
                source.MinimumBodyFrames = Math.Max(1, source.MinimumBodyFrames);
                return source;
            }

            int estimated = Math.Max(1, layer.EstimatedSourceFrames > 0 ? layer.EstimatedSourceFrames : 60);
            int entry = Math.Min(12, Math.Max(0, estimated / 6));
            int exit = Math.Min(18, Math.Max(0, estimated / 5));
            int body = Math.Max(1, estimated - entry - exit);
            return new VfxTimeSegmentsView
            {
                EntryFrames = entry,
                BodyFrames = body,
                ExitFrames = exit,
                CleanupFrames = Math.Min(30, Math.Max(0, estimated / 3)),
                MinimumBodyFrames = 1
            };
        }

        private static void CompileExtendBody(VfxTimeSegmentsView source, int target, VfxCompiledLayerView result)
        {
            int fixedFrames = source.PreRollFrames + source.EntryFrames + source.ExitFrames;
            int body = target - fixedFrames;
            if (body < source.MinimumBodyFrames)
            {
                Error(result, "VFXRET010", "最短艺术长度为 " + source.MinimumArtisticFrames + " 帧；淡入 " + source.EntryFrames + "、主体最少 " + source.MinimumBodyFrames + "、淡出 " + source.ExitFrames + "。", fixAction: "SetMinimumLength");
                return;
            }
            result.TargetSegments.BodyFrames = body;
        }

        private static void CompilePlaybackWindow(int target, VfxCompiledLayerView result)
        {
            if (target <= 0)
            {
                Error(result, "VFXRET080", "播放窗口必须大于 0 帧。", fixAction: "SetPlaybackWindow");
                return;
            }
            result.PlaybackWindowFrames = target;
        }

        private static void CompileExtendTail(VfxTimeSegmentsView source, int target, VfxLayerTimingView timing, VfxComponentProfileView profile, VfxCompiledLayerView result)
        {
            string policy = !string.IsNullOrWhiteSpace(timing.TailPolicy) ? timing.TailPolicy : profile?.Timing?.TailPolicy;
            if (string.IsNullOrWhiteSpace(policy) || policy == "Unconfirmed")
            {
                Error(result, "VFXRET020", "延长尾迹需要 Profile 明确 Hold/Continue/Repeat/EndLocked/Unchanged 策略。", fixAction: "ConfirmTailPolicy");
                return;
            }
            int fixedFrames = source.PreRollFrames + source.EntryFrames + source.BodyFrames;
            int exit = target - fixedFrames;
            if (exit < 0)
            {
                Error(result, "VFXRET021", "目标长度不足以保留 Pre-roll、Entry 和 Body。", fixAction: "IncreaseTargetLength");
                return;
            }
            result.TargetSegments.ExitFrames = exit;
        }

        private static void CompileRepeatBody(VfxTimeSegmentsView source, int target, VfxLayerTimingView timing, VfxCompiledLayerView result)
        {
            int cycle = source.CycleFrames;
            if (cycle <= 0)
            {
                Error(result, "VFXRET030", "RepeatBody 只能用于已确认周期。", fixAction: "ConfirmCycle");
                return;
            }
            int fixedFrames = source.PreRollFrames + source.EntryFrames + source.ExitFrames;
            int requestedBody = target - fixedFrames;
            if (requestedBody < source.MinimumBodyFrames)
            {
                Error(result, "VFXRET031", "目标长度不足以保留最短 Body。", fixAction: "IncreaseTargetLength");
                return;
            }
            if (timing.AllowPartialCycle)
            {
                result.TargetSegments.BodyFrames = requestedBody;
                result.Experimental = requestedBody % cycle != 0;
                if (result.Experimental)
                    Warning(result, "VFXRET032", "启用了非完整末周期；结果标记为 Experimental。", fixAction: "SnapToWholeCycle");
                return;
            }
            int cycles = Math.Max(1, Mathf.RoundToInt(requestedBody / (float)cycle));
            int snapped = cycles * cycle;
            result.TargetSegments.BodyFrames = snapped;
            if (snapped != requestedBody)
                Warning(result, "VFXRET033", "主体长度已吸附到 " + cycles + " 个完整周期（" + snapped + " 帧）。", fixAction: "AcceptSnappedLength");
        }

        private static void CompileEventRemap(VfxTimeSegmentsView source, int target, VfxLayerTimingView timing, VfxCompiledLayerView result)
        {
            if (!ValidateMap(timing.EventMap, out string message))
            {
                Error(result, "VFXRET040", message, fixAction: "EditEventMap");
                return;
            }
            if (timing.EventMap[0].SourceFrame != 0 || timing.EventMap[0].TargetFrame != 0
                || timing.EventMap[timing.EventMap.Count - 1].SourceFrame != source.ArtisticFrames
                || timing.EventMap[timing.EventMap.Count - 1].TargetFrame != target)
            {
                Error(result, "VFXRET041", "事件重排必须包含艺术区间的起点与终点。", fixAction: "AddBoundaryMapPoints");
                return;
            }
            int bodyStartSource = source.PreRollFrames + source.EntryFrames;
            int bodyEndSource = bodyStartSource + source.BodyFrames;
            int bodyStartTarget = Mathf.RoundToInt(MapPiecewise(bodyStartSource, timing.EventMap, out _));
            int bodyEndTarget = Mathf.RoundToInt(MapPiecewise(bodyEndSource, timing.EventMap, out _));
            result.TargetSegments.PreRollFrames = source.PreRollFrames;
            result.TargetSegments.EntryFrames = Math.Max(0, bodyStartTarget - source.PreRollFrames);
            result.TargetSegments.BodyFrames = Math.Max(source.MinimumBodyFrames, bodyEndTarget - bodyStartTarget);
            result.TargetSegments.ExitFrames = Math.Max(0, target - bodyEndTarget);
        }

        private static void CompileWholeSpeed(VfxTimeSegmentsView source, int target, VfxCompiledLayerView result)
        {
            if (target <= 0)
            {
                Error(result, "VFXRET050", "整体变速的目标长度必须大于 0。", fixAction: "SetTargetLength");
                return;
            }
            float scale = target / (float)Math.Max(1, source.ArtisticFrames);
            result.TargetSegments.PreRollFrames = Mathf.RoundToInt(source.PreRollFrames * scale);
            result.TargetSegments.EntryFrames = Mathf.RoundToInt(source.EntryFrames * scale);
            result.TargetSegments.BodyFrames = Math.Max(1, Mathf.RoundToInt(source.BodyFrames * scale));
            result.TargetSegments.ExitFrames = Math.Max(0, target - result.TargetSegments.PreRollFrames - result.TargetSegments.EntryFrames - result.TargetSegments.BodyFrames);
            result.TargetSegments.CleanupFrames = Mathf.RoundToInt(source.CleanupFrames * scale);
            result.TargetSegments.CycleFrames = Mathf.RoundToInt(source.CycleFrames * scale);
            AddDiff(result, string.Empty, "timing.materialLocalTime.scale", "RetimeCompiler", "Layer.simulationSpeed", "1", "Replace", (1f / scale).ToString("0.######", CultureInfo.InvariantCulture), (1f / scale).ToString("0.######", CultureInfo.InvariantCulture), nameof(VfxSemanticTimeDomain.LayerFrame));
        }

        private static void CompileFixed(VfxTimeSegmentsView source, int target, VfxCompiledLayerView result)
        {
            if (target != source.ArtisticFrames)
                Error(result, "VFXRET060", "Fixed 模式只允许移动 Layer 起点，不能修改内部长度。", fixAction: "ChooseRetimeMode");
        }

        private static void BuildTimeMap(VfxLayerTimingView timing, VfxRetimeMode mode, VfxCompiledLayerView result)
        {
            result.TimeMap.Clear();
            if (mode == VfxRetimeMode.EventRemap && ValidateMap(timing.EventMap, out _))
            {
                for (int i = 0; i < timing.EventMap.Count; i++)
                    result.TimeMap.Add(new VfxRetimeMapPointView { SourceFrame = timing.EventMap[i].SourceFrame, TargetFrame = timing.EventMap[i].TargetFrame });
                return;
            }

            VfxTimeSegmentsView source = result.SourceSegments;
            VfxTimeSegmentsView target = result.TargetSegments;
            AddMapPoint(result.TimeMap, 0, 0);
            AddMapPoint(result.TimeMap, source.PreRollFrames, target.PreRollFrames);
            AddMapPoint(result.TimeMap,
                source.PreRollFrames + source.EntryFrames,
                target.PreRollFrames + target.EntryFrames);
            AddMapPoint(result.TimeMap,
                source.PreRollFrames + source.EntryFrames + source.BodyFrames,
                target.PreRollFrames + target.EntryFrames + target.BodyFrames);
            AddMapPoint(result.TimeMap, source.ArtisticFrames, target.ArtisticFrames);
            if (result.TimeMap.Count < 2)
            {
                result.TimeMap.Clear();
                result.TimeMap.Add(new VfxRetimeMapPointView { SourceFrame = 0, TargetFrame = 0 });
                result.TimeMap.Add(new VfxRetimeMapPointView { SourceFrame = Math.Max(1, source.ArtisticFrames), TargetFrame = Math.Max(1, target.ArtisticFrames) });
            }
        }

        private static void AddMapPoint(List<VfxRetimeMapPointView> map, int sourceFrame, int targetFrame)
        {
            sourceFrame = Math.Max(0, sourceFrame);
            targetFrame = Math.Max(0, targetFrame);
            if (map.Count == 0)
            {
                map.Add(new VfxRetimeMapPointView { SourceFrame = sourceFrame, TargetFrame = targetFrame });
                return;
            }
            VfxRetimeMapPointView previous = map[map.Count - 1];
            if (sourceFrame == previous.SourceFrame && targetFrame == previous.TargetFrame) return;
            if (sourceFrame <= previous.SourceFrame || targetFrame <= previous.TargetFrame) return;
            map.Add(new VfxRetimeMapPointView { SourceFrame = sourceFrame, TargetFrame = targetFrame });
        }

        private static void CompileEvents(IReadOnlyList<VfxTimeEventView> events, VfxLayerTimingView timing, VfxRetimeMode mode, VfxCompiledLayerView result)
        {
            if (events == null) return;
            for (int i = 0; i < events.Count; i++)
            {
                VfxTimeEventView sourceEvent = events[i];
                if (!Enum.TryParse(sourceEvent.Anchor, false, out VfxTimeEventAnchor anchor))
                {
                    Error(result, "VFXRET070", "事件锚点无效：" + sourceEvent.EventId, sourceEvent.EventId, fixAction: "ChooseEventAnchor");
                    continue;
                }
                int targetFrame;
                if (mode == VfxRetimeMode.EventRemap && ValidateMap(timing.EventMap, out _))
                    targetFrame = Mathf.RoundToInt(MapPiecewise(sourceEvent.Frame, timing.EventMap, out _));
                else
                    targetFrame = MapFrame(sourceEvent.Frame, result.SourceSegments, result.TargetSegments, anchor, result.SourceSegments.CycleFrames);
                result.Events.Add(new VfxCompiledEventView
                {
                    EventId = sourceEvent.EventId,
                    SourceFrame = sourceEvent.Frame,
                    TargetFrame = targetFrame,
                    Anchor = sourceEvent.Anchor
                });
            }
        }

        private static void CompileSemanticOverrides(VfxCompositionLayerView layer, VfxSemanticCompileContext context, IReadOnlyList<VfxComponentProfileView> profiles, VfxCompiledLayerView result)
        {
            var primaryOpacityProperties = new Dictionary<string, string>(StringComparer.Ordinal);
            ValidateSemanticOverrideList(
                layer,
                layer.SemanticOverrides,
                context,
                profiles,
                result,
                primaryOpacityProperties,
                explicitMemberId: null);

            ValidateMemberParticipation(layer, context, profiles, result);
            ValidateExplicitMemberPropertyOverrides(layer, profiles, result);
            if (layer.MemberOverrides == null) return;
            var seenMembers = new HashSet<string>(StringComparer.Ordinal);
            for (int memberIndex = 0; memberIndex < layer.MemberOverrides.Count; memberIndex++)
            {
                VfxMemberSemanticOverrideView memberOverride = layer.MemberOverrides[memberIndex];
                string memberId = memberOverride?.MemberId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(memberId))
                {
                    Error(result, "VFXSEM017", "成员级语义覆盖缺少 memberId。", fixAction: "ChooseMember");
                    continue;
                }
                if (!seenMembers.Add(memberId))
                {
                    Error(result, "VFXSEM018", "同一成员存在多组语义覆盖：" + memberId, memberId, fixAction: "MergeMemberOverrides");
                    continue;
                }
                VfxComponentProfileView profile = profiles.FirstOrDefault(value =>
                    string.Equals(value.SourceComponentId, memberId, StringComparison.Ordinal));
                if (profile == null)
                {
                    Error(result, "VFXSEM019", "成员级语义覆盖引用了不存在的成员：" + memberId, memberId, fixAction: "RemapMember");
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(memberOverride.RetimeMode)
                    && !Enum.TryParse(memberOverride.RetimeMode, false, out VfxRetimeMode _))
                {
                    Error(
                        result,
                        "VFXRET061",
                        "成员级重定时模式无效：" + memberOverride.RetimeMode,
                        memberId,
                        fixAction: "ChooseMemberRetimeMode");
                }
                VfxMemberParticipationView participation = layer.MemberParticipation?.FirstOrDefault(value =>
                    string.Equals(value.MemberId, memberId, StringComparison.Ordinal));
                if (!string.IsNullOrWhiteSpace(memberOverride.RetimeMode)
                    && participation != null
                    && (participation.State == nameof(VfxMemberParticipationState.Excluded)
                        || participation.State == nameof(VfxMemberParticipationState.Unsupported)
                        || participation.State == nameof(VfxMemberParticipationState.Unconfirmed)))
                {
                    Error(
                        result,
                        "VFXRET062",
                        "成员当前参与状态为 " + participation.State + "，不能设置独立重定时。",
                        memberId,
                        fixAction: "ChangeMemberParticipation");
                }
                ValidateSemanticOverrideList(
                    layer,
                    memberOverride.Overrides,
                    context,
                    new[] { profile },
                    result,
                    primaryOpacityProperties,
                    memberId);
            }
        }

        private static void ValidateMemberParticipation(
            VfxCompositionLayerView layer,
            VfxSemanticCompileContext context,
            IReadOnlyList<VfxComponentProfileView> profiles,
            VfxCompiledLayerView result)
        {
            if (layer.MemberParticipation == null) return;
            var seenMembers = new HashSet<string>(StringComparer.Ordinal);
            for (int memberIndex = 0; memberIndex < layer.MemberParticipation.Count; memberIndex++)
            {
                VfxMemberParticipationView participation = layer.MemberParticipation[memberIndex];
                string memberId = participation?.MemberId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(memberId))
                {
                    Error(result, "VFXSEM024", "成员参与配置缺少 memberId。", fixAction: "ChooseMember");
                    continue;
                }
                if (!seenMembers.Add(memberId))
                {
                    Error(result, "VFXSEM025", "同一成员存在多组参与配置：" + memberId, memberId, fixAction: "MergeMemberParticipation");
                    continue;
                }
                if (!profiles.Any(value => string.Equals(value.SourceComponentId, memberId, StringComparison.Ordinal)))
                {
                    Error(result, "VFXSEM026", "成员参与配置引用了不存在的成员：" + memberId, memberId, fixAction: "RemapMember");
                    continue;
                }
                if (!Enum.TryParse(participation.State, false, out VfxMemberParticipationState _))
                {
                    Error(result, "VFXSEM027", "成员参与状态无效：" + participation.State, memberId, fixAction: "ChooseMemberParticipation");
                }
                if (participation.PropertyStates == null) continue;
                foreach (KeyValuePair<string, string> propertyState in participation.PropertyStates)
                {
                    if (string.IsNullOrWhiteSpace(propertyState.Key) || context?.Registry?.Find(propertyState.Key) == null)
                    {
                        Error(result, "VFXSEM028", "成员参与配置引用了未注册属性：" + propertyState.Key, memberId, propertyState.Key, "ChooseRegisteredProperty");
                    }
                    if (!Enum.TryParse(propertyState.Value, false, out VfxMemberParticipationState _))
                    {
                        Error(result, "VFXSEM029", "成员属性参与状态无效：" + propertyState.Value, memberId, propertyState.Key, "ChooseMemberParticipation");
                    }
                }
            }
        }

        private static void ValidateExplicitMemberPropertyOverrides(
            VfxCompositionLayerView layer,
            IReadOnlyList<VfxComponentProfileView> profiles,
            VfxCompiledLayerView result)
        {
            if (layer.MemberParticipation == null) return;
            for (int memberIndex = 0; memberIndex < layer.MemberParticipation.Count; memberIndex++)
            {
                VfxMemberParticipationView participation = layer.MemberParticipation[memberIndex];
                if (participation?.PropertyStates == null) continue;
                foreach (KeyValuePair<string, string> propertyState in participation.PropertyStates)
                {
                    if (propertyState.Value != nameof(VfxMemberParticipationState.Override)) continue;
                    bool knownMember = profiles.Any(value =>
                        string.Equals(value.SourceComponentId, participation.MemberId, StringComparison.Ordinal));
                    if (!knownMember) continue;
                    bool hasValue = layer.MemberOverrides?.Any(value =>
                        string.Equals(value.MemberId, participation.MemberId, StringComparison.Ordinal)
                        && value.Overrides?.Any(item => string.Equals(item.PropertyId, propertyState.Key, StringComparison.Ordinal)) == true) == true;
                    if (!hasValue)
                    {
                        Error(
                            result,
                            "VFXSEM023",
                            "成员属性标记为覆盖但没有独立值：" + propertyState.Key,
                            participation.MemberId,
                            propertyState.Key,
                            "SetMemberOverrideValue");
                    }
                }
            }
        }

        private static void ValidateSemanticOverrideList(
            VfxCompositionLayerView layer,
            IReadOnlyList<VfxSemanticOverrideView> overrides,
            VfxSemanticCompileContext context,
            IReadOnlyList<VfxComponentProfileView> profiles,
            VfxCompiledLayerView result,
            Dictionary<string, string> primaryOpacityProperties,
            string explicitMemberId)
        {
            if (overrides == null) return;
            var seenProperties = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < overrides.Count; i++)
            {
                VfxSemanticOverrideView semantic = overrides[i];
                if (semantic == null || string.IsNullOrWhiteSpace(semantic.PropertyId))
                {
                    Error(result, "VFXSEM020", "语义覆盖缺少 propertyId。", explicitMemberId, fixAction: "ChooseRegisteredProperty");
                    continue;
                }
                if (LegacyDerivedTimingPropertyIds.Contains(semantic.PropertyId))
                    continue;
                if (!seenProperties.Add(semantic.PropertyId))
                {
                    Error(result, "VFXSEM021", "同一作用域重复覆盖属性：" + semantic.PropertyId, explicitMemberId, semantic.PropertyId, "MergePropertyOverrides");
                    continue;
                }
                if (!string.IsNullOrEmpty(explicitMemberId)
                    && !CanOverrideMemberProperty(layer, context?.ElementProfile, explicitMemberId, semantic.PropertyId, out string participationState))
                {
                    Error(
                        result,
                        "VFXSEM022",
                        "成员属性 " + semantic.PropertyId + " 当前参与状态为 " + participationState + "，不能写入成员级覆盖。",
                        explicitMemberId,
                        semantic.PropertyId,
                        participationState == nameof(VfxMemberParticipationState.Unconfirmed)
                            ? "ConfirmMemberParticipation"
                            : "ChangeMemberParticipation");
                    continue;
                }
                VfxSemanticPropertyDefinitionView definition = context?.Registry?.Find(semantic.PropertyId);
                if (definition == null)
                {
                    Error(result, "VFXSEM010", "未注册的语义属性：" + semantic.PropertyId, explicitMemberId, semantic.PropertyId, "ChooseRegisteredProperty");
                    continue;
                }
                if (!Enum.TryParse(semantic.Operation, false, out VfxSemanticOperation operation) || !definition.Supports(operation))
                {
                    Error(result, "VFXSEM011", semantic.PropertyId + " 不允许 " + semantic.Operation + " 运算。", explicitMemberId, semantic.PropertyId, "ChooseSupportedOperation");
                    continue;
                }
                if (semantic.Value == null || semantic.Value.Type == JTokenType.Null)
                {
                    Error(result, "VFXSEM012", semantic.PropertyId + " 缺少值。", explicitMemberId, semantic.PropertyId, "SetPropertyValue");
                    continue;
                }
                if (IsOutsideRecommendedRange(semantic, definition))
                {
                    semantic.Experimental = true;
                    result.Experimental = true;
                    Warning(result, "VFXSEM013", semantic.PropertyId + " 超出 Profile/Registry 推荐范围，结果标记为 Experimental。", propertyId: semantic.PropertyId, fixAction: "ResetRecommendedRange");
                }

                var bindings = new List<(VfxComponentProfileView Profile, VfxSemanticBindingView Binding)>();
                for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
                {
                    VfxComponentProfileView memberProfile = profiles[profileIndex];
                    for (int bindingIndex = 0; bindingIndex < memberProfile.Bindings.Count; bindingIndex++)
                    {
                        VfxSemanticBindingView candidate = memberProfile.Bindings[bindingIndex];
                        if (candidate.PropertyId == semantic.PropertyId) bindings.Add((memberProfile, candidate));
                    }
                }
                if (bindings.Count == 0)
                {
                    Error(result, "VFXSEM014", semantic.PropertyId + " 没有 Component Profile 绑定。", explicitMemberId, semantic.PropertyId, "EditProfileBindings");
                    continue;
                }
                for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
                {
                    VfxComponentProfileView bindingProfile = bindings[bindingIndex].Profile;
                    VfxSemanticBindingView binding = bindings[bindingIndex].Binding;
                    if (binding.ReadOnly)
                    {
                        Error(result, "VFXSEM015", semantic.PropertyId + " 当前绑定只读。", bindingProfile.SourceComponentId, semantic.PropertyId, "InspectRawModules");
                        continue;
                    }
                    if (binding.PrimaryOpacity)
                    {
                        if (primaryOpacityProperties.TryGetValue(bindingProfile.SourceComponentId, out string existingProperty)
                            && !string.Equals(existingProperty, semantic.PropertyId, StringComparison.Ordinal))
                        {
                            Error(result, "VFXSEM016", "同一成员只能有一个 Primary Opacity Binding。", bindingProfile.SourceComponentId, semantic.PropertyId, "EditProfileBindings");
                        }
                        else
                        {
                            primaryOpacityProperties[bindingProfile.SourceComponentId] = semantic.PropertyId;
                        }
                    }
                }
            }
        }

        private static bool CanOverrideMemberProperty(
            VfxCompositionLayerView layer,
            VfxElementProfileView elementProfile,
            string memberId,
            string propertyId,
            out string state)
        {
            state = nameof(VfxMemberParticipationState.Follow);
            VfxElementProfileMemberView profileMember = elementProfile?.Members?.FirstOrDefault(value =>
                string.Equals(value.MemberId, memberId, StringComparison.Ordinal));
            if (profileMember?.Participation != null
                && profileMember.Participation.TryGetValue(propertyId, out string profileState)
                && !string.IsNullOrWhiteSpace(profileState))
            {
                state = profileState;
            }

            VfxMemberParticipationView draftMember = layer.MemberParticipation?.FirstOrDefault(value =>
                string.Equals(value.MemberId, memberId, StringComparison.Ordinal));
            if (draftMember != null)
            {
                if (!string.IsNullOrWhiteSpace(draftMember.State)
                    && draftMember.State != nameof(VfxMemberParticipationState.Follow))
                {
                    state = draftMember.State;
                }
                if (draftMember.PropertyStates != null
                    && draftMember.PropertyStates.TryGetValue(propertyId, out string draftState)
                    && !string.IsNullOrWhiteSpace(draftState))
                {
                    state = draftState;
                }
            }

            return state == nameof(VfxMemberParticipationState.Follow)
                || state == nameof(VfxMemberParticipationState.Override);
        }

        private static void ValidateCoupledParticleModules(
            VfxCompositionLayerView layer,
            VfxRetimeMode mode,
            VfxElementProfileView elementProfile,
            IReadOnlyList<VfxComponentProfileView> profiles,
            VfxCompiledLayerView result)
        {
            bool timingChanged = result.SourceSegments.PreRollFrames != result.TargetSegments.PreRollFrames
                || result.SourceSegments.EntryFrames != result.TargetSegments.EntryFrames
                || result.SourceSegments.BodyFrames != result.TargetSegments.BodyFrames
                || result.SourceSegments.ExitFrames != result.TargetSegments.ExitFrames
                || result.SourceSegments.CleanupFrames != result.TargetSegments.CleanupFrames
                || result.SourceSegments.CycleFrames != result.TargetSegments.CycleFrames;
            for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
            {
                VfxComponentProfileView profile = profiles[profileIndex];
                if (profile == null || IsMemberExcluded(layer, profile.SourceComponentId)) continue;
                VfxRetimeMode memberMode = ResolveMemberRetimeMode(layer, profile.SourceComponentId, mode);
                bool memberTimingChanged = timingChanged && memberMode != VfxRetimeMode.Fixed;
                HashSet<string> properties = PropertiesForMember(layer, elementProfile, profile.SourceComponentId);
                bool hasSemanticChanges = properties.Count > 0;
                bool hasMotionChanges = properties.Any(value => value.StartsWith("motion.", StringComparison.Ordinal));
                bool changesVisibleTrailLength = properties.Contains("timing.startLifetime.exact")
                    || properties.Contains("motion.speed")
                    || properties.Contains("motion.startSpeed.exact")
                    || properties.Contains("motion.travelRange");

                if (HasModule(profile, "SubEmitters") && (memberTimingChanged || hasSemanticChanges))
                {
                    Error(result, "VFXMOD001",
                        "SubEmitter 的触发关系尚无安全语义 Adapter；源效果可预览，但修改时间或属性前必须建立专用 Profile。",
                        profile.SourceComponentId, fixAction: "CreateSubEmitterProfile");
                }
                bool hasTrails = HasModule(profile, "Trails") || profile.Timing?.TrailContract != null;
                bool supportedTrailRetime = string.Equals(
                    profile.Timing?.TrailContract?.RetimePolicy,
                    "PreserveNormalizedLifetime",
                    StringComparison.Ordinal);
                if (hasTrails
                    && ((!supportedTrailRetime
                            && (memberMode == VfxRetimeMode.ExtendTail && memberTimingChanged
                                || memberMode == VfxRetimeMode.WholeSpeed && memberTimingChanged))
                        || changesVisibleTrailLength))
                {
                    Error(result, "VFXMOD002",
                        "该成员启用了 Trails；当前操作会改变粒子或尾迹的可见长度，必须先确认 Trail lifetime/width/color/dieWithParticles 合同。",
                        profile.SourceComponentId, fixAction: "CreateTrailProfile");
                }
                if (HasModule(profile, "Collision")
                    && (memberMode == VfxRetimeMode.WholeSpeed && memberTimingChanged || hasMotionChanges))
                {
                    Error(result, "VFXMOD003",
                        "该成员启用了 Collision；运动或整体变速会改变碰撞轨迹，当前只能读取原始配置。",
                        profile.SourceComponentId, fixAction: "CreateCollisionProfile");
                }
                if (HasModule(profile, "LimitVelocityOverLifetime"))
                {
                    bool onlySupportedDrag = properties.Count > 0 && properties.All(value => value == "motion.drag");
                    if (memberMode == VfxRetimeMode.WholeSpeed && memberTimingChanged
                        || hasMotionChanges && !onlySupportedDrag)
                    {
                        Error(result, "VFXMOD004",
                            "该成员启用了 Limit Velocity；除已绑定的 motion.drag 外，运动重算需要经过动力学 Fixture 验证。",
                            profile.SourceComponentId, fixAction: "CreateLimitVelocityProfile");
                    }
                    else if (onlySupportedDrag)
                    {
                        result.Experimental = true;
                        Warning(result, "VFXMOD005",
                            "motion.drag 已精确写入 Limit Velocity 曲线，但实际行程变化尚未通过动力学 Fixture，结果标记为 Experimental。",
                            profile.SourceComponentId, "motion.drag", "ReviewLimitVelocityMotion");
                    }
                }
            }
        }

        internal static VfxRetimeMode ResolveMemberRetimeMode(
            VfxCompositionLayerView layer,
            string memberId,
            VfxRetimeMode layerMode)
        {
            VfxMemberSemanticOverrideView memberOverride = layer.MemberOverrides?.FirstOrDefault(value =>
                string.Equals(value.MemberId, memberId, StringComparison.Ordinal));
            return Enum.TryParse(memberOverride?.RetimeMode, false, out VfxRetimeMode draftMode)
                ? draftMode
                : layerMode;
        }

        private static HashSet<string> PropertiesForMember(
            VfxCompositionLayerView layer,
            VfxElementProfileView elementProfile,
            string memberId)
        {
            var result = new HashSet<string>(
                (layer.SemanticOverrides ?? new List<VfxSemanticOverrideView>())
                    .Where(value => value != null && !string.IsNullOrWhiteSpace(value.PropertyId))
                    .Where(value => CanOverrideMemberProperty(layer, elementProfile, memberId, value.PropertyId, out _))
                    .Select(value => value.PropertyId),
                StringComparer.Ordinal);
            VfxMemberSemanticOverrideView member = layer.MemberOverrides?.FirstOrDefault(value =>
                string.Equals(value.MemberId, memberId, StringComparison.Ordinal));
            if (member?.Overrides != null)
            {
                for (int i = 0; i < member.Overrides.Count; i++)
                {
                    string propertyId = member.Overrides[i]?.PropertyId;
                    if (!string.IsNullOrWhiteSpace(propertyId)
                        && CanOverrideMemberProperty(layer, elementProfile, memberId, propertyId, out _))
                    {
                        result.Add(propertyId);
                    }
                }
            }
            return result;
        }

        private static bool IsMemberExcluded(VfxCompositionLayerView layer, string memberId)
        {
            VfxMemberParticipationView participation = layer.MemberParticipation?.FirstOrDefault(value =>
                string.Equals(value.MemberId, memberId, StringComparison.Ordinal));
            return participation != null
                && (participation.State == nameof(VfxMemberParticipationState.Excluded)
                    || participation.State == nameof(VfxMemberParticipationState.Unsupported)
                    || participation.State == nameof(VfxMemberParticipationState.Unconfirmed));
        }

        private static bool HasModule(VfxComponentProfileView profile, string module)
        {
            return (profile.ReadOnlyModules?.Contains(module) ?? false)
                || (profile.Blockers?.Contains(module) ?? false);
        }

        private static bool IsCoupledParticleModule(string module)
        {
            return module == "Trails"
                || module == "SubEmitters"
                || module == "Collision"
                || module == "LimitVelocityOverLifetime";
        }

        private static bool IsOutsideRecommendedRange(VfxSemanticOverrideView semantic, VfxSemanticPropertyDefinitionView definition)
        {
            if (semantic.Value.Type != JTokenType.Float && semantic.Value.Type != JTokenType.Integer) return false;
            double value = semantic.Value.Value<double>();
            return (definition.RecommendedMinimum.HasValue && value < definition.RecommendedMinimum.Value)
                || (definition.RecommendedMaximum.HasValue && value > definition.RecommendedMaximum.Value);
        }

        internal static float MapPiecewise(float sourceFrame, IReadOnlyList<VfxRetimeMapPointView> map, out float sourcePerTargetSlope)
        {
            int index = 0;
            while (index + 1 < map.Count - 1 && sourceFrame > map[index + 1].SourceFrame) index++;
            VfxRetimeMapPointView left = map[index];
            VfxRetimeMapPointView right = map[Math.Min(index + 1, map.Count - 1)];
            float sourceSpan = Math.Max(0.000001f, right.SourceFrame - left.SourceFrame);
            float targetSpan = Math.Max(0.000001f, right.TargetFrame - left.TargetFrame);
            float t = Mathf.Clamp01((sourceFrame - left.SourceFrame) / sourceSpan);
            sourcePerTargetSlope = sourceSpan / targetSpan;
            return Mathf.Lerp(left.TargetFrame, right.TargetFrame, t);
        }

        private static void Finalize(VfxCompiledLayerView result)
        {
            result.CompileHash = VfxCanonicalJson.Hash(result, "compileHash");
        }

        internal static void AddDiff(VfxCompiledLayerView result, string memberId, string propertyId, string adapter, string unityPath, string sourceValue, string operation, string semanticValue, string finalValue, string timeDomain)
        {
            result.FieldDiffs.Add(new VfxUnityFieldDiffView
            {
                MemberId = memberId,
                PropertyId = propertyId,
                Adapter = adapter,
                UnityPath = unityPath,
                SourceValue = sourceValue,
                Operation = operation,
                SemanticValue = semanticValue,
                FinalValue = finalValue,
                TimeDomain = timeDomain
            });
        }

        internal static void Error(VfxCompiledLayerView result, string code, string message, string memberId = null, string propertyId = null, string fixAction = null)
        {
            result.Diagnostics.Add(new VfxSemanticDiagnosticView { Code = code, Severity = "Error", Message = message, MemberId = memberId, PropertyId = propertyId, FixAction = fixAction });
        }

        internal static void Warning(VfxCompiledLayerView result, string code, string message, string memberId = null, string propertyId = null, string fixAction = null)
        {
            result.Diagnostics.Add(new VfxSemanticDiagnosticView { Code = code, Severity = "Warning", Message = message, MemberId = memberId, PropertyId = propertyId, FixAction = fixAction });
        }
    }
}

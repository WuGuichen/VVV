using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Core;
using MxFramework.Editor;
using MxFramework.Editor.CharacterAction.Timeline;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void BuildPresentationTrack(VisualElement parent, CharacterActionJsonDto action)
        {
            if (action.PresentationTrack == null)
            {
                parent.Add(new Button(() => ApplyStructuralChange(() => action.PresentationTrack = new PresentationTrackJsonDto(), UiRefreshScope.Tracks))
                {
                    text = "启用表现轨道",
                });
                return;
            }

            PresentationTrackEventJsonDto[] events = action.PresentationTrack.Events ?? Array.Empty<PresentationTrackEventJsonDto>();
            for (int i = 0; i < events.Length; i++)
                AddPresentationEvent(parent, action, events, i);

            AddButtonRow(parent,
                ("添加音效", () => AddPresentationEvent(action, CharacterActionTrackEventKind.PlayAudioCue)),
                ("添加特效", () => AddPresentationEvent(action, CharacterActionTrackEventKind.SpawnVisualCue)),
                ("禁用表现轨道", () => ApplyStructuralChange(() => action.PresentationTrack = null, UiRefreshScope.Tracks)));
        }

        private void BuildPresentationEventInspector(
            VisualElement parent,
            CharacterActionJsonDto action,
            int index,
            bool timelineInspector = false)
        {
            if (action.PresentationTrack == null)
            {
                parent.Add(new Label("表现轨道未启用。"));
                return;
            }

            PresentationTrackEventJsonDto[] events = action.PresentationTrack.Events ?? Array.Empty<PresentationTrackEventJsonDto>();
            if (index < 0 || index >= events.Length)
            {
                parent.Add(new Label("表现事件不存在。"));
                return;
            }

            AddPresentationEvent(parent, action, events, index, timelineInspector);
        }

        private void AddPresentationEvent(
            VisualElement parent,
            CharacterActionJsonDto action,
            PresentationTrackEventJsonDto[] events,
            int index,
            bool timelineInspector = false)
        {
            PresentationTrackEventJsonDto evt = events[index] ?? new PresentationTrackEventJsonDto();
            CharacterActionTrackEventKind kind = ResolvePresentationEventKind(evt);
            VisualElement row = AddCard(parent, "表现事件 " + index);
            row.Add(new Label(BuildPresentationEventSummary(evt, kind)));
            if (evt.BladeTrailGenerated)
            {
                row.Add(new HelpBox("此拖尾由命中窗口自动生成。请在对应命中窗口修改启用与提前/延后帧；样式由当前武器提供。",
                    HelpBoxMessageType.Info));
                return;
            }
            if (timelineInspector)
            {
                AddTimelineIntegerField(row, "触发帧", evt.Frame, value =>
                {
                    evt.Frame = Math.Max(0, value);
                    if (evt.EndFrame < evt.Frame)
                        evt.EndFrame = evt.Frame;
                }, requestFullTracksRefresh: true);
                AddTimelineEnumPopup<CharacterActionTrackEventKind>(
                    row,
                    "事件类型",
                    evt.Kind,
                    value => SetPresentationEventKind(evt, value),
                    requestFullTracksRefresh: true);
                AddPresentationPayloadFields(row, evt, kind, timelineInspector: true);
                AddTimelineTextField(row, "稳定事件 ID", evt.StableEventId, value => evt.StableEventId = value);
            }
            else
            {
                AddIntegerField(row, "触发帧", evt.Frame, value =>
                {
                    evt.Frame = Math.Max(0, value);
                    if (evt.EndFrame < evt.Frame)
                        evt.EndFrame = evt.Frame;
                });
                AddEnumPopup<CharacterActionTrackEventKind>(row, "事件类型", evt.Kind, value => SetPresentationEventKind(evt, value));
                AddPresentationPayloadFields(row, evt, kind, timelineInspector: false);
                AddTextField(row, "稳定事件 ID", evt.StableEventId, value => evt.StableEventId = value);
            }

            row.Add(new Button(() => RemovePresentationEvent(action, evt, index)) { text = "删除事件" });
            events[index] = evt;
        }

        private void AddPresentationPayloadFields(
            VisualElement row,
            PresentationTrackEventJsonDto evt,
            CharacterActionTrackEventKind kind,
            bool timelineInspector)
        {
            bool visualCue = kind == CharacterActionTrackEventKind.SpawnVisualCue;
            bool audioCue = kind == CharacterActionTrackEventKind.PlayAudioCue;
            if (visualCue)
            {
                if (timelineInspector)
                {
                    AddVfxEndFrameField(row, evt, timelineInspector: true);
                    AddTimelineVfxResourceCard(
                        row,
                        evt.ResourceKey,
                        evt.Frame,
                        value => evt.ResourceKey = value,
                        requestFullTracksRefresh: true);
                    AddVfxConfigurationFields(row, evt, timelineInspector: true);
                }
                else
                {
                    AddVfxEndFrameField(row, evt, timelineInspector: false);
                    AddVfxResourceCard(
                        row,
                        evt.ResourceKey,
                        evt.Frame,
                        value => ApplyFieldChange(() => evt.ResourceKey = value));
                    AddVfxConfigurationFields(row, evt, timelineInspector: false);
                }
            }
            else if (audioCue)
            {
                if (timelineInspector)
                {
                    AddTimelineReferenceTextField(
                        row,
                        "音效 Cue ID",
                        evt.CueId,
                        _model.ReferenceContext.AudioCueIds,
                        value => evt.CueId = value,
                        requestFullTracksRefresh: true);
                }
                else
                {
                    AddReferenceTextField(
                        row,
                        "音效 Cue ID",
                        evt.CueId,
                        _model.ReferenceContext.AudioCueIds,
                        value => evt.CueId = value);
                }
            }
            else
            {
                if (timelineInspector)
                {
                    AddTimelineReferenceTextField(row, "音效 Cue ID", evt.CueId, _model.ReferenceContext.AudioCueIds, value => evt.CueId = value);
                    AddTimelineReferenceTextField(
                        row,
                        "特效资源 Key",
                        evt.ResourceKey,
                        _model.ReferenceContext.PresentationResourceKeys,
                        value => evt.ResourceKey = value);
                }
                else
                {
                    AddReferenceTextField(row, "音效 Cue ID", evt.CueId, _model.ReferenceContext.AudioCueIds, value => evt.CueId = value);
                    AddReferenceTextField(
                        row,
                        "特效资源 Key",
                        evt.ResourceKey,
                        _model.ReferenceContext.PresentationResourceKeys,
                        value => evt.ResourceKey = value);
                }
            }
        }

        private void AddVfxConfigurationFields(
            VisualElement parent,
            PresentationTrackEventJsonDto evt,
            bool timelineInspector)
        {
            parent.Add(new MxAttachmentTransformField(
                evt.VfxAttachment,
                value =>
                {
                    if (timelineInspector)
                        ApplyTimelineFieldChange(() => evt.VfxAttachment = value, requestFullTracksRefresh: true);
                    else
                        ApplyFieldChange(() => evt.VfxAttachment = value);
                },
                CharacterActionAuthoringIds.LabelsList(
                    _model?.ReferenceContext?.EquipmentSlotIds, 405),
                ResolveVfxAttachmentSocketStatus,
                LocateVfxAttachmentSocket));

            var playback = new Foldout { text = "播放控制", value = true };
            parent.Add(playback);
            AddVfxPlaybackModeField(playback, evt, timelineInspector);
            AddVfxSourceTimingFields(playback, evt, timelineInspector);
            AddPresentationFloatField(
                playback,
                timelineInspector,
                "播放速率（倍）",
                evt.VfxPlaybackRate,
                value => evt.VfxPlaybackRate = Math.Max(
                    CharacterActionVfxEventConfig.MinPlaybackRate,
                    Math.Min(CharacterActionVfxEventConfig.MaxPlaybackRate, value)));
            AddPresentationFloatField(
                playback,
                timelineInspector,
                "整体尺寸（倍）",
                evt.VfxScaleMultiplier,
                value => evt.VfxScaleMultiplier = Math.Max(
                    CharacterActionVfxEventConfig.MinScaleMultiplier,
                    Math.Min(CharacterActionVfxEventConfig.MaxScaleMultiplier, value)));
            AddVfxNaturalEndPolicyField(playback, evt, timelineInspector);

            var parameters = new Foldout { text = "特效参数覆盖（运行时槽）", value = false };
            parent.Add(parameters);
            AddPresentationIntegerField(parameters, timelineInspector, "参数 0", evt.VfxParameter0, value => evt.VfxParameter0 = value);
            AddPresentationIntegerField(parameters, timelineInspector, "参数 1", evt.VfxParameter1, value => evt.VfxParameter1 = value);
            AddPresentationIntegerField(parameters, timelineInspector, "参数 2", evt.VfxParameter2, value => evt.VfxParameter2 = value);
            AddPresentationIntegerField(parameters, timelineInspector, "参数 3", evt.VfxParameter3, value => evt.VfxParameter3 = value);
        }

        private void AddVfxEndFrameField(
            VisualElement parent,
            PresentationTrackEventJsonDto evt,
            bool timelineInspector)
        {
            bool automatic = CharacterActionVfxTimelineResolver.IsFiniteRetimeMode(evt)
                && CharacterActionVfxTimelineResolver.TryResolveSourceFrames(
                    evt.ResourceKey,
                    out _);
            if (automatic)
            {
                var field = new IntegerField("结束帧（自动）")
                {
                    value = Math.Max(evt.Frame, evt.EndFrame),
                };
                field.SetEnabled(false);
                parent.Add(field);
                return;
            }

            if (timelineInspector)
            {
                AddTimelineIntegerField(
                    parent,
                    "结束帧",
                    Math.Max(evt.Frame, evt.EndFrame),
                    value => evt.EndFrame = Math.Max(evt.Frame, value),
                    requestFullTracksRefresh: true);
            }
            else
            {
                AddIntegerField(
                    parent,
                    "结束帧",
                    Math.Max(evt.Frame, evt.EndFrame),
                    value => evt.EndFrame = Math.Max(evt.Frame, value));
            }
        }

        private void AddVfxSourceTimingFields(
            VisualElement parent,
            PresentationTrackEventJsonDto evt,
            bool timelineInspector)
        {
            bool hasFiniteSource = CharacterActionVfxTimelineResolver.TryResolveSourceFrames(
                evt.ResourceKey,
                out int sourceFrames);
            if (!hasFiniteSource)
            {
                bool requiresFiniteSource = CharacterActionVfxTimelineResolver.IsPlayToCompletion(evt);
                parent.Add(new HelpBox(
                    requiresFiniteSource
                        ? "当前 VFX 没有有限基础长度，无法使用完整播放。请改用截止/循环窗口，或在 VFX 资源库补齐生命周期。"
                        : "当前 VFX 没有有限基础长度：播放窗口由时间轴控制，不提供自动反算速度，后截断不可用。",
                    requiresFiniteSource ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning));
                AddPresentationIntegerField(
                    parent,
                    timelineInspector,
                    "前截断（源帧）",
                    Math.Max(0, evt.VfxStartOffsetFrames),
                    value => evt.VfxStartOffsetFrames = Math.Max(0, value),
                    requestFullTracksRefresh: true);
                return;
            }

            CharacterActionVfxTimelineResolver.NormalizeOffsets(evt, sourceFrames);
            int playableFrames = CharacterActionVfxTimelineResolver.ComputePlayableSourceFrames(
                sourceFrames,
                evt.VfxStartOffsetFrames,
                evt.VfxEndOffsetFrames);
            int timelineFrames = CharacterActionVfxTimelineResolver.ComputeTimelineSpanFrames(
                playableFrames,
                evt.VfxPlaybackRate);
            int sourceEndInclusive = sourceFrames - evt.VfxEndOffsetFrames - 1;
            parent.Add(new HelpBox(
                $"基础 {sourceFrames} 帧 · 使用源区间 {evt.VfxStartOffsetFrames}–{sourceEndInclusive}"
                + $" · 有效 {playableFrames} 帧 · 时间轴 {timelineFrames} 帧",
                HelpBoxMessageType.Info));

            AddPresentationIntegerField(
                parent,
                timelineInspector,
                "前截断（源帧）",
                evt.VfxStartOffsetFrames,
                value =>
                {
                    evt.VfxStartOffsetFrames = Math.Max(0, value);
                    CharacterActionVfxTimelineResolver.NormalizeOffsets(evt, sourceFrames);
                },
                requestFullTracksRefresh: true);
            AddPresentationIntegerField(
                parent,
                timelineInspector,
                "后截断（源帧）",
                evt.VfxEndOffsetFrames,
                value =>
                {
                    evt.VfxEndOffsetFrames = Math.Max(0, value);
                    CharacterActionVfxTimelineResolver.NormalizeOffsets(evt, sourceFrames);
                },
                requestFullTracksRefresh: true);
        }

        private void AddVfxPlaybackModeField(
            VisualElement parent,
            PresentationTrackEventJsonDto evt,
            bool timelineInspector)
        {
            CharacterActionVfxPlaybackMode current = Enum.TryParse(
                evt.VfxPlaybackMode,
                ignoreCase: true,
                out CharacterActionVfxPlaybackMode parsed)
                    ? parsed
                    : CharacterActionVfxPlaybackMode.PlayToCompletion;
            var choices = new List<string>
            {
                "完整播放（资源自身时长）",
                "截止到结束帧",
                "持续播放到结束帧",
            };
            var mode = new PopupField<string>("播放方式", choices, (int)current)
            {
                name = "character-action-vfx-playback-mode",
            };
            mode.RegisterValueChangedCallback(change =>
            {
                int selected = choices.IndexOf(change.newValue);
                if (selected < 0)
                    return;

                Action apply = () => evt.VfxPlaybackMode =
                    ((CharacterActionVfxPlaybackMode)selected).ToString();
                if (timelineInspector)
                    ApplyTimelineFieldChange(apply, requestFullTracksRefresh: true);
                else
                    ApplyFieldChange(apply);
            });
            parent.Add(mode);

            if (current == CharacterActionVfxPlaybackMode.PlayToCompletion)
                return;

            int duration = Math.Max(1, evt.EndFrame - evt.Frame + 1);
            parent.Add(new HelpBox(
                $"将在结束帧 {evt.EndFrame} 发出结束指令，当前播放窗口为 {duration} 帧。",
                HelpBoxMessageType.Info));
        }

        private void AddVfxNaturalEndPolicyField(
            VisualElement parent,
            PresentationTrackEventJsonDto evt,
            bool timelineInspector)
        {
            CharacterActionVfxStopPolicy current = Enum.TryParse(
                evt.VfxNaturalEndPolicy,
                ignoreCase: true,
                out CharacterActionVfxStopPolicy parsed)
                    ? parsed
                    : CharacterActionVfxStopPolicy.Complete;
            var choices = new List<string>
            {
                "自然收尾",
                "消散收尾",
                "立即清除",
            };
            var field = new PopupField<string>("正常结束", choices, (int)current);
            field.RegisterValueChangedCallback(change =>
            {
                int selected = choices.IndexOf(change.newValue);
                if (selected < 0)
                    return;

                Action apply = () => evt.VfxNaturalEndPolicy =
                    ((CharacterActionVfxStopPolicy)selected).ToString();
                if (timelineInspector)
                    ApplyTimelineFieldChange(apply, requestFullTracksRefresh: true);
                else
                    ApplyFieldChange(apply);
            });
            parent.Add(field);
        }

        private void AddPresentationIntegerField(
            VisualElement parent,
            bool timeline,
            string label,
            int value,
            Action<int> setter,
            bool requestFullTracksRefresh = false)
        {
            if (timeline)
                AddTimelineIntegerField(parent, label, value, setter, requestFullTracksRefresh);
            else
                AddIntegerField(parent, label, value, setter);
        }

        private void AddPresentationFloatField(VisualElement parent, bool timeline, string label, float value, Action<float> setter)
        {
            if (timeline)
                AddTimelineFloatField(parent, label, value, setter, requestFullTracksRefresh: true);
            else
                AddFloatField(parent, label, value, setter);
        }

        private string ResolveVfxAttachmentSocketStatus(string socketId)
        {
            return _timelinePreviewSession != null
                && _timelinePreviewSession.TryDescribeAttachmentSocket(socketId, out string description)
                    ? description
                    : string.Empty;
        }

        private void LocateVfxAttachmentSocket(string socketId)
        {
            EnsureTimelinePreviewSession();
            if (_timelinePreviewSession == null)
                return;

            if (!_timelinePreviewSession.LocateAttachmentSocket(socketId))
            {
                UpdateTimelineRuntimePreview(_timelinePlayback?.CurrentFrame ?? 0);
                _timelinePreviewSession.LocateAttachmentSocket(socketId);
            }
        }

        private void AddPresentationEvent(CharacterActionJsonDto action, CharacterActionTrackEventKind kind)
        {
            ApplyStructuralChange(() =>
            {
                action.PresentationTrack = action.PresentationTrack ?? new PresentationTrackJsonDto();
                var events = new List<PresentationTrackEventJsonDto>(action.PresentationTrack.Events ?? Array.Empty<PresentationTrackEventJsonDto>());
                events.Add(new PresentationTrackEventJsonDto
                {
                    Frame = 0,
                    EndFrame = 0,
                    Kind = kind.ToString(),
                    CueId = kind == CharacterActionTrackEventKind.PlayAudioCue
                        ? CharacterActionAuthoringIds.Resolve("sfx." + action.ActionId, 401)
                        : 0,
                    ResourceKey = kind == CharacterActionTrackEventKind.SpawnVisualCue ? "vfx." + action.ActionId : string.Empty,
                    VfxAttachment = MxAttachmentTransform.Default,
                    VfxPlaybackMode = CharacterActionVfxPlaybackMode.PlayToCompletion.ToString(),
                    VfxPlaybackRate = 1f,
                    VfxScaleMultiplier = 1f,
                    VfxStartOffsetFrames = 0,
                    VfxEndOffsetFrames = 0,
                    VfxNaturalEndPolicy = CharacterActionVfxStopPolicy.Complete.ToString(),
                    StableEventId = CharacterActionAuthoringIds.Resolve(
                        CharacterActionTimelineIdentity.MakeStableEventId(action, kind.ToString()), 401),
                });
                action.PresentationTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }

        private static CharacterActionTrackEventKind ResolvePresentationEventKind(PresentationTrackEventJsonDto evt)
        {
            if (Enum.TryParse(evt?.Kind, out CharacterActionTrackEventKind kind))
                return kind;
            return CharacterActionTrackEventKind.SpawnVisualCue;
        }

        private static void SetPresentationEventKind(PresentationTrackEventJsonDto evt, string kind)
        {
            if (evt == null)
                return;

            evt.Kind = kind ?? string.Empty;
            CharacterActionTrackEventKind resolved = ResolvePresentationEventKind(evt);
            if (resolved == CharacterActionTrackEventKind.PlayAudioCue || evt.EndFrame < evt.Frame)
                evt.EndFrame = evt.Frame;
        }

        private static string BuildPresentationEventSummary(PresentationTrackEventJsonDto evt, CharacterActionTrackEventKind kind)
        {
            evt = evt ?? new PresentationTrackEventJsonDto();
            int endFrame = kind == CharacterActionTrackEventKind.SpawnVisualCue
                ? Math.Max(evt.Frame, evt.EndFrame)
                : evt.Frame;
            string range = CharacterActionAuthoringText.FrameRangeLabel(evt.Frame, endFrame);
            if (kind == CharacterActionTrackEventKind.SpawnVisualCue)
                return range + " SpawnVisualCue resource=" + EmptyPresentationValue(evt.ResourceKey);
            if (kind == CharacterActionTrackEventKind.PlayAudioCue)
                return range + " PlayAudioCue cue=" + EmptyPresentationValue(
                    CharacterActionAuthoringIds.Label(evt.CueId, 401));
            return range + " " + kind;
        }

        private static string EmptyPresentationValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<unset>" : value;
        }

        private void RemovePresentationEvent(CharacterActionJsonDto action, PresentationTrackEventJsonDto evt, int fallbackIndex)
        {
            ApplyStructuralChange(() =>
            {
                var events = new List<PresentationTrackEventJsonDto>(action.PresentationTrack?.Events ?? Array.Empty<PresentationTrackEventJsonDto>());
                int index = ResolveEventIndex(events, evt, CharacterActionAuthoringIds.Label(evt?.StableEventId ?? 0, 401), fallbackIndex,
                    candidate => CharacterActionAuthoringIds.Label(candidate?.StableEventId ?? 0, 401));
                if (index >= 0)
                    events.RemoveAt(index);
                if (action.PresentationTrack != null)
                    action.PresentationTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }
    }
}

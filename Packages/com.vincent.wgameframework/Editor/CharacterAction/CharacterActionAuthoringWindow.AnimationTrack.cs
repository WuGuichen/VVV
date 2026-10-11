using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void BuildAnimationTrack(VisualElement parent, CharacterActionJsonDto action)
        {
            if (action.AnimationTrack == null)
            {
                parent.Add(new Button(() => ApplyStructuralChange(() => action.AnimationTrack = new AnimationTrackJsonDto(), UiRefreshScope.Tracks))
                {
                    text = "启用动画轨道",
                });
                return;
            }

            AnimationTrackEventJsonDto[] events = action.AnimationTrack.Events ?? Array.Empty<AnimationTrackEventJsonDto>();
            for (int i = 0; i < events.Length; i++)
                AddAnimationEvent(parent, action, events, i);

            AddButtonRow(parent,
                ("添加 CrossFade", () => AddAnimationEvent(action)),
                ("禁用动画轨道", () => ApplyStructuralChange(() => action.AnimationTrack = null, UiRefreshScope.Tracks)));
        }

        private void BuildAnimationEventInspector(
            VisualElement parent,
            CharacterActionJsonDto action,
            int index,
            bool timelineInspector = false)
        {
            if (action.AnimationTrack == null)
            {
                parent.Add(new Label("动画轨道未启用。"));
                return;
            }

            AnimationTrackEventJsonDto[] events = action.AnimationTrack.Events ?? Array.Empty<AnimationTrackEventJsonDto>();
            if (index < 0 || index >= events.Length)
            {
                parent.Add(new Label("动画事件不存在。"));
                return;
            }

            AddAnimationEvent(parent, action, events, index, timelineInspector);
        }

        private void AddAnimationEvent(
            VisualElement parent,
            CharacterActionJsonDto action,
            AnimationTrackEventJsonDto[] events,
            int index,
            bool timelineInspector = false)
        {
            AnimationTrackEventJsonDto evt = events[index] ?? new AnimationTrackEventJsonDto();
            VisualElement row = AddCard(parent, "动画事件 " + index);
            row.Add(new Label(CharacterActionAuthoringText.AnimationSummary(evt)));

            if (timelineInspector)
            {
                AddTimelineIntegerField(row, "触发帧", evt.Frame, value =>
                {
                    evt.Frame = Math.Max(0, value);
                    if (!CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                            evt,
                            action,
                            DefaultDurationFrames,
                            _model.ActionSet?.CharacterStableId)
                        && evt.EndFrame < evt.Frame)
                    {
                        evt.EndFrame = evt.Frame;
                    }
                }, requestFullTracksRefresh: true);
                AddAnimationEndFrameField(row, evt, timelineInspector: true);
                AddTimelineEnumPopup<CharacterActionTrackEventKind>(row, "事件类型", evt.Kind, value => evt.Kind = value, requestFullTracksRefresh: true);
                AddTimelineAnimationActionKeyField(row, evt, action);
                AddAnimationTimingSummary(row, evt);
                AddTimelineAnimationOffsetFields(row, evt, action);
                AddTimelineFloatField(
                    row,
                    "播放速度",
                    CharacterActionAnimationTimelineResolver.ClampPlaybackSpeed(evt.PlaybackSpeed),
                    value =>
                {
                    evt.PlaybackSpeed = CharacterActionAnimationTimelineResolver.ClampPlaybackSpeed(value);
                    CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                        evt,
                        action,
                        DefaultDurationFrames,
                        _model.ActionSet?.CharacterStableId);
                }, requestFullTracksRefresh: true);
                AddTimelineFloatField(row, "过渡秒数", evt.TransitionSeconds, value => evt.TransitionSeconds = value);
                int transitionFrames = CharacterActionTimelineLayout.TransitionFramesFromSeconds(evt.TransitionSeconds);
                row.Add(new Label("过渡约 " + transitionFrames + " 帧 (@"
                    + CharacterActionTimelineLayout.EditorTimelineFps + "fps)"));
            }
            else
            {
                AddIntegerField(row, "触发帧", evt.Frame, value =>
                {
                    evt.Frame = Math.Max(0, value);
                    if (!CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                            evt,
                            action,
                            DefaultDurationFrames,
                            _model.ActionSet?.CharacterStableId)
                        && evt.EndFrame < evt.Frame)
                    {
                        evt.EndFrame = evt.Frame;
                    }
                });
                AddAnimationEndFrameField(row, evt, timelineInspector: false);
                AddEnumPopup<CharacterActionTrackEventKind>(row, "事件类型", evt.Kind, value => evt.Kind = value);
                AddAnimationClipField(row, evt, action, timelineInspector: false);
                AddAnimationTimingSummary(row, evt);
                AddAnimationOffsetFields(row, evt, action);
                AddFloatField(
                    row,
                    "播放速度",
                    CharacterActionAnimationTimelineResolver.ClampPlaybackSpeed(evt.PlaybackSpeed),
                    value =>
                {
                    evt.PlaybackSpeed = CharacterActionAnimationTimelineResolver.ClampPlaybackSpeed(value);
                    CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                        evt,
                        action,
                        DefaultDurationFrames,
                        _model.ActionSet?.CharacterStableId);
                });
                AddFloatField(row, "过渡秒数", evt.TransitionSeconds, value => evt.TransitionSeconds = value);
            }

            if (timelineInspector)
                AddTimelineTextField(row, "稳定事件 ID", evt.StableEventId, value => evt.StableEventId = value);
            else
                AddTextField(row, "稳定事件 ID", evt.StableEventId, value => evt.StableEventId = value);

            row.Add(new Button(() => RemoveAnimationEvent(action, evt, index)) { text = "删除事件" });
            events[index] = evt;
        }

        private void AddAnimationTimingSummary(
            VisualElement parent,
            AnimationTrackEventJsonDto evt)
        {
            if (!CharacterActionAnimationTimelineResolver.TryResolveSourceClipFrames(
                    evt,
                    _model.ActionSet?.CharacterStableId,
                    out int sourceFrames))
            {
                parent.Add(new HelpBox(
                    "无法解析动画源长度：当前只保留人工事件窗口，不提供自动变速或 Alt 截断。",
                    HelpBoxMessageType.Warning));
                return;
            }

            int startOffset = Mathf.Clamp(
                evt.AnimationStartOffsetFrames,
                0,
                sourceFrames - 1);
            int endOffset = Mathf.Clamp(
                evt.AnimationEndOffsetFrames,
                0,
                sourceFrames - startOffset - 1);
            int playableFrames = Math.Max(1, sourceFrames - startOffset - endOffset);
            int timelineSpan = CharacterActionAnimationTimelineResolver.ComputeTimelineSpanFrames(
                playableFrames,
                evt.PlaybackSpeed);
            int sourceEndInclusive = sourceFrames - endOffset - 1;
            parent.Add(new Label(
                $"基础 {sourceFrames} 帧 · 使用源区间 {startOffset}–{sourceEndInclusive}"
                + $" · 最终时间轴 {timelineSpan} 帧")
            {
                tooltip = "拖动片段边缘调整播放速度；按住 Alt 拖动边缘修改动画前后截断。",
            });
        }

        private void AddAnimationEndFrameField(
            VisualElement parent,
            AnimationTrackEventJsonDto evt,
            bool timelineInspector)
        {
            bool automatic = CharacterActionAnimationTimelineResolver.TryResolveSourceClipFrames(
                evt,
                _model.ActionSet?.CharacterStableId,
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
                return;
            }

            AddIntegerField(
                parent,
                "结束帧",
                Math.Max(evt.Frame, evt.EndFrame),
                value => evt.EndFrame = Math.Max(evt.Frame, value));
        }

        private void AddTimelineAnimationActionKeyField(
            VisualElement parent,
            AnimationTrackEventJsonDto evt,
            CharacterActionJsonDto action)
        {
            AddAnimationClipField(parent, evt, action, timelineInspector: true);
        }

        private void AddTimelineAnimationOffsetFields(
            VisualElement parent,
            AnimationTrackEventJsonDto evt,
            CharacterActionJsonDto action)
        {
            AddTimelineIntegerField(parent, "动画起始偏移帧", Math.Max(0, evt.AnimationStartOffsetFrames), value =>
            {
                evt.AnimationStartOffsetFrames = Math.Max(0, value);
                CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                    evt,
                    action,
                    DefaultDurationFrames,
                    _model.ActionSet?.CharacterStableId);
            }, requestFullTracksRefresh: true);
            AddTimelineIntegerField(parent, "动画结束偏移帧", Math.Max(0, evt.AnimationEndOffsetFrames), value =>
            {
                evt.AnimationEndOffsetFrames = Math.Max(0, value);
                CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                    evt,
                    action,
                    DefaultDurationFrames,
                    _model.ActionSet?.CharacterStableId);
            }, requestFullTracksRefresh: true);
        }

        private void AddAnimationOffsetFields(
            VisualElement parent,
            AnimationTrackEventJsonDto evt,
            CharacterActionJsonDto action)
        {
            AddIntegerField(parent, "动画起始偏移帧", Math.Max(0, evt.AnimationStartOffsetFrames), value =>
            {
                evt.AnimationStartOffsetFrames = Math.Max(0, value);
                CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                    evt,
                    action,
                    DefaultDurationFrames,
                    _model.ActionSet?.CharacterStableId);
            });
            AddIntegerField(parent, "动画结束偏移帧", Math.Max(0, evt.AnimationEndOffsetFrames), value =>
            {
                evt.AnimationEndOffsetFrames = Math.Max(0, value);
                CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                    evt,
                    action,
                    DefaultDurationFrames,
                    _model.ActionSet?.CharacterStableId);
            });
        }

        private void AddAnimationClipField(
            VisualElement parent,
            AnimationTrackEventJsonDto evt,
            CharacterActionJsonDto action,
            bool timelineInspector)
        {
            string currentKey = GetAnimationEventEffectiveKey(evt);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            var field = new TextField("动画片段")
            {
                value = currentKey,
                isDelayed = true,
            };
            field.style.flexGrow = 1f;
            field.RegisterValueChangedCallback(callbackEvt =>
            {
                ApplyAnimationClipFieldChange(
                    evt,
                    action,
                    callbackEvt.newValue ?? string.Empty,
                    timelineInspector);
            });
            row.Add(field);

            parent.Add(row);
            var resources = new Foldout { text = "选择动画资源 / 定位来源", value = false };
            parent.Add(resources);
            bool built = false;
            resources.RegisterValueChangedCallback(change =>
            {
                if (!ReferenceEquals(change.target, resources) || !change.newValue || built)
                    return;
                built = true;
                BuildAnimationClipResourceControls(resources, evt, action, timelineInspector);
            });
        }

        private void BuildAnimationClipResourceControls(
            VisualElement parent,
            AnimationTrackEventJsonDto evt,
            CharacterActionJsonDto action,
            bool timelineInspector)
        {
            string currentKey = GetAnimationEventEffectiveKey(evt);
            var row = new VisualElement();
            List<CharacterActionAnimationClipChoice> clipChoices = BuildAnimationClipChoices(currentKey);
            if (clipChoices.Count > 0)
            {
                var menuLabels = new List<string>(clipChoices.Count);
                int selectedIndex = 0;
                for (int i = 0; i < clipChoices.Count; i++)
                {
                    menuLabels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(clipChoices[i].Label));
                    if (string.Equals(clipChoices[i].ResourceKey, currentKey, StringComparison.Ordinal))
                        selectedIndex = i;
                }

                var popup = new PopupField<string>("", menuLabels, selectedIndex);
                popup.style.minWidth = 320f;
                popup.RegisterValueChangedCallback(callbackEvt =>
                {
                    int choiceIndex = menuLabels.IndexOf(callbackEvt.newValue);
                    if (choiceIndex < 0 || choiceIndex >= clipChoices.Count)
                        return;
                    ApplyAnimationClipFieldChange(
                        evt,
                        action,
                        clipChoices[choiceIndex].ResourceKey,
                        timelineInspector);
                });
                row.Add(popup);
            }

            parent.Add(row);

            CharacterActionAnimationClipChoice selected = FindAnimationClipChoice(currentKey, clipChoices);
            AddAnimationClipObjectField(parent, evt, action, selected, timelineInspector);
            if (!string.IsNullOrWhiteSpace(selected.UnityAssetPath))
                AddAnimationClipSourceRow(parent, selected);
        }

        private void AddAnimationClipObjectField(
            VisualElement parent,
            AnimationTrackEventJsonDto evt,
            CharacterActionJsonDto action,
            CharacterActionAnimationClipChoice selected,
            bool timelineInspector)
        {
            CharacterActionPreviewBindingDiscovery.TryLoadAnimationClip(selected, out AnimationClip selectedClip);
            var objectField = new ObjectField("Clip 资源")
            {
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
                value = selectedClip,
            };
            objectField.RegisterValueChangedCallback(callbackEvt =>
            {
                if (callbackEvt.newValue == null)
                {
                    ApplyAnimationClipFieldChange(evt, action, string.Empty, timelineInspector);
                    return;
                }

                var clip = callbackEvt.newValue as AnimationClip;
                if (clip == null)
                {
                    objectField.SetValueWithoutNotify(selectedClip);
                    return;
                }

                if (!CharacterActionPreviewBindingDiscovery.TryFindAnimationClipChoice(
                        clip,
                        _model.ActionSet?.CharacterStableId,
                        out CharacterActionAnimationClipChoice choice))
                {
                    objectField.SetValueWithoutNotify(selectedClip);
                    EditorUtility.DisplayDialog(
                        "未找到动画片段",
                        "拖入的 AnimationClip 不在 Resource Manager RuntimeReady 动画列表或生成的角色动画片段注册表中。",
                        "确定");
                    return;
                }

                ApplyAnimationClipFieldChange(evt, action, choice.ResourceKey, timelineInspector);
            });
            parent.Add(objectField);
        }

        private void ApplyAnimationClipFieldChange(
            AnimationTrackEventJsonDto evt,
            CharacterActionJsonDto action,
            string resourceKey,
            bool timelineInspector)
        {
            void Mutate()
            {
                evt.AnimationClipResourceKey = resourceKey ?? string.Empty;
                evt.AnimationActionKey = string.Empty;
                _model.ReferenceContext.AddAnimationActionKey(GetAnimationEventEffectiveKey(evt));
                CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                    evt,
                    action,
                    DefaultDurationFrames,
                    _model.ActionSet?.CharacterStableId);
            }

            if (timelineInspector)
                ApplyTimelineFieldChange(Mutate);
            else
                ApplyFieldChange(Mutate);
        }

        private List<CharacterActionAnimationClipChoice> BuildAnimationClipChoices(string currentResourceKey)
        {
            var result = new List<CharacterActionAnimationClipChoice>();
            CharacterActionAnimationClipChoice[] discovered =
                CharacterActionPreviewBindingDiscovery.DiscoverAnimationClips(_model.ActionSet?.CharacterStableId);
            bool hasCurrent = string.IsNullOrWhiteSpace(currentResourceKey);
            for (int i = 0; i < discovered.Length; i++)
            {
                if (string.Equals(discovered[i].ResourceKey, currentResourceKey, StringComparison.Ordinal))
                    hasCurrent = true;
                result.Add(discovered[i]);
            }

            if (!hasCurrent)
            {
                result.Insert(0, new CharacterActionAnimationClipChoice(
                    currentResourceKey,
                    currentResourceKey + "  [manual or action binding]",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty));
            }

            return result;
        }

        private static string GetAnimationEventEffectiveKey(AnimationTrackEventJsonDto evt)
        {
            return CharacterActionAnimationKeyUtility.ResolveEffectiveKey(
                evt?.AnimationClipResourceKey,
                evt?.AnimationActionKey);
        }

        private static CharacterActionAnimationClipChoice FindAnimationClipChoice(
            string resourceKey,
            List<CharacterActionAnimationClipChoice> choices)
        {
            choices = choices ?? new List<CharacterActionAnimationClipChoice>();
            for (int i = 0; i < choices.Count; i++)
            {
                if (string.Equals(choices[i].ResourceKey, resourceKey, StringComparison.Ordinal))
                    return choices[i];
            }

            return default;
        }

        private static void AddAnimationClipSourceRow(
            VisualElement parent,
            CharacterActionAnimationClipChoice clip)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            Label label = new Label("来源：" + clip.UnityAssetPath);
            label.style.flexGrow = 1f;
            label.style.whiteSpace = WhiteSpace.Normal;
            row.Add(label);
            row.Add(new Button(() => PingAnimationClipSource(clip.UnityAssetPath)) { text = "定位来源" });
            parent.Add(row);
        }

        private static void PingAnimationClipSource(string unityAssetPath)
        {
            if (string.IsNullOrWhiteSpace(unityAssetPath))
                return;

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(unityAssetPath);
            if (asset == null)
                return;

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void AddTimelineIntegerField(
            VisualElement parent,
            string label,
            int value,
            Action<int> setter,
            bool requestFullTracksRefresh = false)
        {
            var field = new IntegerField(label)
            {
                value = value,
                isDelayed = true,
            };
            field.RegisterValueChangedCallback(evt => ApplyTimelineFieldChange(() => setter(evt.newValue), requestFullTracksRefresh));
            parent.Add(field);
        }

        private void AddTimelineFloatField(
            VisualElement parent,
            string label,
            float value,
            Action<float> setter,
            bool requestFullTracksRefresh = false)
        {
            var field = new FloatField(label)
            {
                value = value,
                isDelayed = true,
            };
            field.RegisterValueChangedCallback(evt => ApplyTimelineFieldChange(() => setter(evt.newValue), requestFullTracksRefresh));
            parent.Add(field);
        }

        private void AddTimelineTextField(
            VisualElement parent,
            string label,
            string value,
            Action<string> setter,
            bool requestFullTracksRefresh = false)
        {
            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                isDelayed = true,
            };
            field.RegisterValueChangedCallback(evt =>
                ApplyTimelineFieldChange(() => setter(evt.newValue ?? string.Empty), requestFullTracksRefresh));
            parent.Add(field);
        }

        private void AddTimelineReferenceTextField(
            VisualElement parent,
            string label,
            string value,
            List<string> suggestions,
            Action<string> setter,
            bool requestFullTracksRefresh = false)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                isDelayed = true,
            };
            field.style.flexGrow = 1f;
            field.RegisterValueChangedCallback(evt =>
                ApplyTimelineFieldChange(() => setter(evt.newValue ?? string.Empty), requestFullTracksRefresh));
            row.Add(field);

            List<string> popupChoices = BuildReferenceChoices(value, suggestions);
            if (popupChoices.Count > 0)
            {
                var menuLabels = new List<string>(popupChoices.Count);
                for (int i = 0; i < popupChoices.Count; i++)
                    menuLabels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(popupChoices[i]));

                var popup = new PopupField<string>("", menuLabels, menuLabels[0]);
                popup.style.minWidth = 220f;
                popup.RegisterValueChangedCallback(evt =>
                {
                    int index = menuLabels.IndexOf(evt.newValue);
                    if (index < 0 || index >= popupChoices.Count)
                        return;
                    ApplyTimelineFieldChange(() => setter(popupChoices[index] ?? string.Empty), requestFullTracksRefresh);
                });
                row.Add(popup);
            }

            parent.Add(row);
        }

        private void AddTimelineEnumPopup<TEnum>(
            VisualElement parent,
            string label,
            string value,
            Action<string> setter,
            bool requestFullTracksRefresh = false)
            where TEnum : struct
        {
            string[] rawNames = Enum.GetNames(typeof(TEnum));
            var choices = new List<string>(rawNames.Length);
            var rawValues = new List<string>(rawNames.Length);
            for (int i = 0; i < rawNames.Length; i++)
            {
                rawValues.Add(rawNames[i]);
                choices.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(
                    CharacterActionAuthoringText.EnumDisplayLabel(typeof(TEnum), rawNames[i])));
            }

            value = string.IsNullOrWhiteSpace(value) ? rawValues[0] : value;
            int selectedMenuIndex = rawValues.IndexOf(value);
            if (selectedMenuIndex < 0)
            {
                rawValues.Add(value);
                choices.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(
                    CharacterActionAuthoringText.EnumDisplayLabel(typeof(TEnum), value)));
                selectedMenuIndex = choices.Count - 1;
            }

            var field = new PopupField<string>(label, choices, choices[selectedMenuIndex]);
            field.RegisterValueChangedCallback(evt =>
            {
                int index = choices.IndexOf(evt.newValue);
                if (index < 0 || index >= rawValues.Count)
                    return;
                ApplyTimelineFieldChange(() => setter(rawValues[index]), requestFullTracksRefresh);
            });
            parent.Add(field);
        }

        private void AddAnimationEvent(CharacterActionJsonDto action)
        {
            ApplyStructuralChange(() =>
            {
                action.AnimationTrack = action.AnimationTrack ?? new AnimationTrackJsonDto();
                var events = new List<AnimationTrackEventJsonDto>(action.AnimationTrack.Events ?? Array.Empty<AnimationTrackEventJsonDto>());
                var evt = new AnimationTrackEventJsonDto
                {
                    Frame = 0,
                    Kind = CharacterActionTrackEventKind.CrossFadeAnimation.ToString(),
                    AnimationActionKey = "anim." + action.ActionId,
                    TransitionSeconds = 0.08f,
                    PlaybackSpeed = 1f,
                    StableEventId = CharacterActionAuthoringIds.Resolve(
                        CharacterActionTimelineIdentity.MakeStableEventId(action, "animation"), 401),
                };
                CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                    evt,
                    action,
                    DefaultDurationFrames,
                    _model.ActionSet?.CharacterStableId);
                events.Add(evt);
                action.AnimationTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }

        private void RemoveAnimationEvent(CharacterActionJsonDto action, AnimationTrackEventJsonDto evt, int fallbackIndex)
        {
            ApplyStructuralChange(() =>
            {
                var events = new List<AnimationTrackEventJsonDto>(action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>());
                int index = ResolveEventIndex(events, evt, CharacterActionAuthoringIds.Label(evt?.StableEventId ?? 0, 401), fallbackIndex,
                    candidate => CharacterActionAuthoringIds.Label(candidate?.StableEventId ?? 0, 401));
                if (index >= 0)
                    events.RemoveAt(index);
                if (action.AnimationTrack != null)
                    action.AnimationTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }
    }
}

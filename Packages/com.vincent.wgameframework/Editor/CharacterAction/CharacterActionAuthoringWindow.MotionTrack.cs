using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private static readonly CharacterActionTrackEventKind[] MotionTrackEventKinds =
        {
            CharacterActionTrackEventKind.SetMovementMode,
            CharacterActionTrackEventKind.ApplyImpulse,
            CharacterActionTrackEventKind.LockMovement,
            CharacterActionTrackEventKind.SetControlScale,
            CharacterActionTrackEventKind.SetCollisionProfile,
            CharacterActionTrackEventKind.SetVerticalVelocity,
            CharacterActionTrackEventKind.IgnoreGravity,
            CharacterActionTrackEventKind.SampleRootMotionY,
        };

        private int _rootMotionEditStartFrame;
        private int _rootMotionEditEndFrame;
        private float _rootMotionEditScaleX = 1f;
        private float _rootMotionEditScaleZ = 1f;
        private bool _rootMotionEditRangeInitialized;
        private int _rootMotionEditActionId;

        private void BuildMotionTrack(VisualElement parent, CharacterActionJsonDto action)
        {
            if (action.MotionTrack == null)
            {
                parent.Add(new Button(() => ApplyStructuralChange(() => action.MotionTrack = new MotionTrackJsonDto(), UiRefreshScope.Tracks))
                {
                    text = "启用运动轨道",
                });
                return;
            }

            AddToggle(parent, "使用 RootMotion", action.MotionTrack.UsesRootMotion, value => action.MotionTrack.UsesRootMotion = value);
            BuildRootMotionInspector(parent, action);
            MotionTrackEventJsonDto[] events = action.MotionTrack.Events ?? Array.Empty<MotionTrackEventJsonDto>();
            for (int i = 0; i < events.Length; i++)
                AddMotionEvent(parent, action, events, i);

            if (action.MotionTrack.RootMotion == null)
            {
                AddButtonRow(parent,
                    ("添加 RootMotion", () => AddRootMotion(action)),
                    ("添加运动冲量", () => AddMotionEvent(action)),
                    ("设置控制倍率", () => AddControlScaleEvent(action)),
                    ("禁用运动轨道", () => ApplyStructuralChange(() => action.MotionTrack = null, UiRefreshScope.Tracks)));
            }
            else
            {
                AddButtonRow(parent,
                    ("添加运动冲量", () => AddMotionEvent(action)),
                    ("设置控制倍率", () => AddControlScaleEvent(action)),
                    ("移除 RootMotion", () => RemoveRootMotion(action)),
                    ("禁用运动轨道", () => ApplyStructuralChange(() => action.MotionTrack = null, UiRefreshScope.Tracks)));
            }
            AddButtonRow(parent,
                ("持续垂直速度", () => AddVerticalMotionEvent(action, CharacterActionTrackEventKind.SetVerticalVelocity)),
                ("忽略重力", () => AddVerticalMotionEvent(action, CharacterActionTrackEventKind.IgnoreGravity)),
                ("采样动画 Y", () => AddVerticalMotionEvent(action, CharacterActionTrackEventKind.SampleRootMotionY)));
        }
        private void BuildMotionEventInspector(
            VisualElement parent,
            CharacterActionJsonDto action,
            int index,
            bool timelineInspector = false)
        {
            if (action.MotionTrack == null)
            {
                parent.Add(new Label("运动轨道未启用。"));
                return;
            }

            MotionTrackEventJsonDto[] events = action.MotionTrack.Events ?? Array.Empty<MotionTrackEventJsonDto>();
            if (index < 0 || index >= events.Length)
            {
                parent.Add(new Label("运动事件不存在。"));
                return;
            }

            AddMotionEvent(parent, action, events, index, timelineInspector);
        }

        private void BuildRootMotionInspector(
            VisualElement parent,
            CharacterActionJsonDto action,
            bool timelineInspector = false)
        {
            if (action?.MotionTrack == null || !action.MotionTrack.UsesRootMotion)
                return;

            VisualElement row = AddCard(parent, "RootMotion");
            RootMotionTrackJsonDto rootMotion = action.MotionTrack.RootMotion;
            if (rootMotion == null)
            {
                row.Add(new Label("RootMotion 采样数=0"));
                AddButtonRow(
                    row,
                    ("添加 RootMotion", () => AddRootMotion(action)),
                    ("烘焙来源范围", () => GenerateRootMotionFromAnimation(action)),
                    ("烘焙全部动画事件", () => GenerateAllRootMotionFromAnimationEvents(action)));
                return;
            }

            row.Add(new Label(CharacterActionAuthoringText.RootMotionSummary(rootMotion)));
            AddRootMotionSourceEventField(row, action, rootMotion, timelineInspector);
            if (timelineInspector)
            {
                AddTimelineEnumPopup<CharacterActionRootMotionSpace>(row, "空间", rootMotion.Space, value => rootMotion.Space = value, requestFullTracksRefresh: true);
                AddTimelineEnumPopup<CharacterActionRootMotionObstaclePolicy>(row, "碰撞策略", rootMotion.ObstaclePolicy, value => rootMotion.ObstaclePolicy = value, requestFullTracksRefresh: true);
                AddTimelineIntegerField(row, "采样 Tick Rate", rootMotion.SampleTickRate, value => rootMotion.SampleTickRate = Math.Max(1, value), requestFullTracksRefresh: true);
            }
            else
            {
                AddEnumPopup<CharacterActionRootMotionSpace>(row, "空间", rootMotion.Space, value => rootMotion.Space = value);
                AddEnumPopup<CharacterActionRootMotionObstaclePolicy>(row, "碰撞策略", rootMotion.ObstaclePolicy, value => rootMotion.ObstaclePolicy = value);
                AddIntegerField(row, "采样 Tick Rate", rootMotion.SampleTickRate, value => rootMotion.SampleTickRate = Math.Max(1, value));
            }

            var advanced = new Foldout { text = "高级来源", value = false };
            if (timelineInspector)
            {
                AddTimelineTextField(advanced, "来源事件 ID", rootMotion.SourceAnimationEventId, value => rootMotion.SourceAnimationEventId = value, requestFullTracksRefresh: true);
                AddTimelineTextField(advanced, "来源动画资源 Key", rootMotion.SourceAnimationClipResourceKey, value => rootMotion.SourceAnimationClipResourceKey = value, requestFullTracksRefresh: true);
            }
            else
            {
                AddTextField(advanced, "来源事件 ID", rootMotion.SourceAnimationEventId, value => rootMotion.SourceAnimationEventId = value);
                AddTextField(advanced, "来源动画资源 Key", rootMotion.SourceAnimationClipResourceKey, value => rootMotion.SourceAnimationClipResourceKey = value);
            }
            row.Add(advanced);
            AddRootMotionAdjustmentControls(row, action, rootMotion, timelineInspector);

            AddButtonRow(
                row,
                ("烘焙来源范围", () => GenerateRootMotionFromAnimation(action)),
                ("烘焙全部动画事件", () => GenerateAllRootMotionFromAnimationEvents(action)),
                ("清空来源范围", () => ClearRootMotionSourceRange(action)),
                ("清空全部采样", () => ClearRootMotionSamples(action)));
        }

        private void AddMotionEvent(
            VisualElement parent,
            CharacterActionJsonDto action,
            MotionTrackEventJsonDto[] events,
            int index,
            bool timelineInspector = false)
        {
            MotionTrackEventJsonDto evt = events[index] ?? new MotionTrackEventJsonDto();
            VisualElement row = AddCard(parent, "运动事件 " + index);
            row.Add(new Label(CharacterActionAuthoringText.MotionSummary(evt)));
            CharacterActionTrackEventKind kind = ResolveMotionTrackEventKind(evt);
            bool showMovementMode = UsesMovementMode(kind);
            bool showVector = UsesImpulseVector(kind);
            bool showControlScale = UsesControlScale(kind);
            bool showCollisionProfile = UsesCollisionProfile(kind);
            bool showVerticalVelocity = kind == CharacterActionTrackEventKind.SetVerticalVelocity;
            bool showRange = kind == CharacterActionTrackEventKind.LockMovement
                || kind == CharacterActionTrackEventKind.SetControlScale
                || kind == CharacterActionTrackEventKind.SetCollisionProfile
                || showVerticalVelocity
                || kind == CharacterActionTrackEventKind.IgnoreGravity
                || kind == CharacterActionTrackEventKind.SampleRootMotionY;
            if (timelineInspector)
            {
                AddTimelineIntegerField(row, "触发帧", evt.Frame, value => evt.Frame = value);
                if (showRange)
                    AddTimelineIntegerField(row, "结束帧", evt.EndFrame, value => evt.EndFrame = Math.Max(evt.Frame, value));
                AddTimelineMotionEventKindPopup(row, evt);
                if (showMovementMode)
                    AddTimelineEnumPopup<CharacterMovementMode>(row, "运动模式", evt.MovementMode, value => evt.MovementMode = value);
                if (showControlScale)
                {
                    AddTimelineControlScaleFields(row, evt);
                }
                if (showCollisionProfile)
                    AddTimelineCollisionProfileFields(row, evt);
                if (showVector)
                {
                    AddTimelineEnumPopup<CharacterActionMotionVectorSpace>(row, "向量空间", evt.Space, value => evt.Space = value);
                    AddTimelineFloatField(row, "X", evt.X, value => evt.X = value);
                    AddTimelineFloatField(row, "Y", evt.Y, value => evt.Y = value);
                    AddTimelineFloatField(row, "Z", evt.Z, value => evt.Z = value);
                }
                if (showVerticalVelocity)
                    AddTimelineFloatField(row, "垂直速度 (米/秒)", evt.Y, value => evt.Y = value);
                AddTimelineTextField(row, "稳定事件 ID", evt.StableEventId, value => evt.StableEventId = value);
            }
            else
            {
                AddIntegerField(row, "触发帧", evt.Frame, value => evt.Frame = value);
                if (showRange)
                    AddIntegerField(row, "结束帧", evt.EndFrame, value => evt.EndFrame = Math.Max(evt.Frame, value));
                AddMotionEventKindPopup(row, evt);
                if (showMovementMode)
                    AddEnumPopup<CharacterMovementMode>(row, "运动模式", evt.MovementMode, value => evt.MovementMode = value);
                if (showControlScale)
                {
                    AddControlScaleFields(row, evt);
                }
                if (showCollisionProfile)
                    AddCollisionProfileFields(row, evt);
                if (showVector)
                {
                    AddEnumPopup<CharacterActionMotionVectorSpace>(row, "向量空间", evt.Space, value => evt.Space = value);
                    AddFloatField(row, "X", evt.X, value => evt.X = value);
                    AddFloatField(row, "Y", evt.Y, value => evt.Y = value);
                    AddFloatField(row, "Z", evt.Z, value => evt.Z = value);
                }
                if (showVerticalVelocity)
                    AddFloatField(row, "垂直速度 (米/秒)", evt.Y, value => evt.Y = value);
                AddTextField(row, "稳定事件 ID", evt.StableEventId, value => evt.StableEventId = value);
            }

            if (kind == CharacterActionTrackEventKind.SampleRootMotionY)
            {
                int sampleCount = action.MotionTrack.VerticalRootMotion?.Frames?.Length ?? 0;
                row.Add(new Label("动画 Y 轴采样：" + sampleCount + " 帧"));
                row.Add(new Button(() => BakeVerticalRootMotionFromAnimation(action, evt))
                {
                    text = "从动画烘焙 Y 轴采样",
                });
            }

            row.Add(new Button(() => RemoveMotionEvent(action, evt, index)) { text = "删除事件" });
            events[index] = evt;
        }

        private void AddMotionEventKindPopup(VisualElement parent, MotionTrackEventJsonDto evt)
        {
            BuildMotionEventKindChoices(evt?.Kind, out List<string> labels, out List<string> rawValues, out int selectedIndex);
            var field = new PopupField<string>("事件类型", labels, labels[selectedIndex]);
            field.RegisterValueChangedCallback(change =>
            {
                int index = labels.IndexOf(change.newValue);
                if (index < 0 || index >= rawValues.Count)
                    return;

                ApplyStructuralChange(
                    () => SetMotionEventKind(evt, rawValues[index]),
                    UiRefreshScope.Tracks);
            });
            parent.Add(field);
        }

        private void AddTimelineMotionEventKindPopup(VisualElement parent, MotionTrackEventJsonDto evt)
        {
            BuildMotionEventKindChoices(evt?.Kind, out List<string> labels, out List<string> rawValues, out int selectedIndex);
            var field = new PopupField<string>("事件类型", labels, labels[selectedIndex]);
            field.RegisterValueChangedCallback(change =>
            {
                int index = labels.IndexOf(change.newValue);
                if (index < 0 || index >= rawValues.Count)
                    return;

                ApplyTimelineFieldChange(
                    () => SetMotionEventKind(evt, rawValues[index]),
                    requestFullTracksRefresh: true);
            });
            parent.Add(field);
        }

        private static void BuildMotionEventKindChoices(
            string currentKind,
            out List<string> labels,
            out List<string> rawValues,
            out int selectedIndex)
        {
            labels = new List<string>(MotionTrackEventKinds.Length + 1);
            rawValues = new List<string>(MotionTrackEventKinds.Length + 1);
            selectedIndex = 0;
            string current = string.IsNullOrWhiteSpace(currentKind)
                ? CharacterActionTrackEventKind.ApplyImpulse.ToString()
                : currentKind;

            for (int i = 0; i < MotionTrackEventKinds.Length; i++)
            {
                string raw = MotionTrackEventKinds[i].ToString();
                rawValues.Add(raw);
                labels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(
                    CharacterActionAuthoringText.MotionEventKindDisplayLabel(raw)));
                if (string.Equals(raw, current, StringComparison.Ordinal))
                    selectedIndex = i;
            }

            if (!IsSupportedMotionEventKind(current))
            {
                rawValues.Add(current);
                labels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(
                    CharacterActionAuthoringText.MotionEventKindDisplayLabel(current)));
                selectedIndex = rawValues.Count - 1;
            }
        }

        private static void SetMotionEventKind(MotionTrackEventJsonDto evt, string kind)
        {
            if (evt == null)
                return;

            evt.Kind = string.IsNullOrWhiteSpace(kind)
                ? CharacterActionTrackEventKind.ApplyImpulse.ToString()
                : kind;
            NormalizeMotionEventDefaults(evt);
        }

        private static void NormalizeMotionEventDefaults(MotionTrackEventJsonDto evt)
        {
            if (evt == null)
                return;

            CharacterActionTrackEventKind kind = ResolveMotionTrackEventKind(evt);
            if (UsesMovementMode(kind) && string.IsNullOrWhiteSpace(evt.MovementMode))
                evt.MovementMode = kind == CharacterActionTrackEventKind.ApplyImpulse
                    ? CharacterMovementMode.Airborne.ToString()
                    : CharacterMovementMode.Idle.ToString();
            if (kind == CharacterActionTrackEventKind.ApplyImpulse && string.IsNullOrWhiteSpace(evt.Space))
                evt.Space = CharacterActionMotionVectorSpace.World.ToString();
            if (kind == CharacterActionTrackEventKind.SetVerticalVelocity
                || kind == CharacterActionTrackEventKind.IgnoreGravity
                || kind == CharacterActionTrackEventKind.SampleRootMotionY)
                evt.EndFrame = Math.Max(evt.Frame, evt.EndFrame);

            if (kind == CharacterActionTrackEventKind.SetControlScale)
            {
                if (evt.MovementControlScale.HasValue)
                    evt.MovementControlScale = Clamp01(evt.MovementControlScale.Value);
                if (evt.RotationControlScale.HasValue)
                    evt.RotationControlScale = Clamp01(evt.RotationControlScale.Value);
            }
            if (kind == CharacterActionTrackEventKind.SetCollisionProfile)
            {
                if (evt.CollisionProfileId <= 0)
                    evt.CollisionProfileId = CharacterActionAuthoringIds.Resolve("standing", 402);
                if (string.IsNullOrWhiteSpace(evt.CollisionRestorePolicy))
                    evt.CollisionRestorePolicy = CharacterActionCollisionProfileRestorePolicy.OnActionEnd.ToString();
                if (evt.CollisionTransitionFrames < 0)
                    evt.CollisionTransitionFrames = 0;
            }
        }

        private static CharacterActionTrackEventKind ResolveMotionTrackEventKind(MotionTrackEventJsonDto evt)
        {
            if (Enum.TryParse(evt?.Kind, ignoreCase: false, out CharacterActionTrackEventKind kind)
                && IsSupportedMotionEventKind(kind.ToString()))
            {
                return kind;
            }

            return CharacterActionTrackEventKind.ApplyImpulse;
        }

        private static bool UsesMovementMode(CharacterActionTrackEventKind kind)
        {
            return kind == CharacterActionTrackEventKind.SetMovementMode
                || kind == CharacterActionTrackEventKind.ApplyImpulse
                || kind == CharacterActionTrackEventKind.LockMovement;
        }

        private static bool UsesImpulseVector(CharacterActionTrackEventKind kind)
        {
            return kind == CharacterActionTrackEventKind.ApplyImpulse;
        }

        private static bool UsesControlScale(CharacterActionTrackEventKind kind)
        {
            return kind == CharacterActionTrackEventKind.SetControlScale;
        }

        private static bool UsesCollisionProfile(CharacterActionTrackEventKind kind)
        {
            return kind == CharacterActionTrackEventKind.SetCollisionProfile;
        }

        private static bool IsSupportedMotionEventKind(string kind)
        {
            for (int i = 0; i < MotionTrackEventKinds.Length; i++)
            {
                if (string.Equals(MotionTrackEventKinds[i].ToString(), kind, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void AddMotionEvent(CharacterActionJsonDto action)
        {
            ApplyStructuralChange(() =>
            {
                action.MotionTrack = action.MotionTrack ?? new MotionTrackJsonDto();
                var events = new List<MotionTrackEventJsonDto>(action.MotionTrack.Events ?? Array.Empty<MotionTrackEventJsonDto>());
                events.Add(new MotionTrackEventJsonDto
                {
                    Frame = 0,
                    Kind = CharacterActionTrackEventKind.ApplyImpulse.ToString(),
                    MovementMode = CharacterMovementMode.Airborne.ToString(),
                    Space = CharacterActionMotionVectorSpace.World.ToString(),
                    Y = 8f,
                    StableEventId = CharacterActionAuthoringIds.Resolve(
                        CharacterActionTimelineIdentity.MakeStableEventId(action, "impulse"), 401),
                });
                action.MotionTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }

        private void AddVerticalMotionEvent(CharacterActionJsonDto action, CharacterActionTrackEventKind kind)
        {
            ApplyStructuralChange(() =>
            {
                action.MotionTrack = action.MotionTrack ?? new MotionTrackJsonDto();
                var events = new List<MotionTrackEventJsonDto>(
                    action.MotionTrack.Events ?? Array.Empty<MotionTrackEventJsonDto>());
                events.Add(new MotionTrackEventJsonDto
                {
                    Frame = 0,
                    EndFrame = Math.Max(0, (action.DurationFrames ?? 1) - 1),
                    Kind = kind.ToString(),
                    StableEventId = CharacterActionAuthoringIds.Resolve(
                        CharacterActionTimelineIdentity.MakeStableEventId(action, kind.ToString()), 401),
                });
                IReadOnlyDictionary<int, int> orders = CharacterContentIdentityAuthoringCatalog.ReadEventOrders();
                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i] != null && orders.TryGetValue(events[i].StableEventId, out int order))
                        events[i].StableEventOrder = order;
                }
                action.MotionTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }


        private void AddControlScaleEvent(CharacterActionJsonDto action)
        {
            AddControlScaleEvent(action, frame: 0, reset: false);
        }

        private void AddControlScaleEvent(CharacterActionJsonDto action, int frame, bool reset)
        {
            ApplyStructuralChange(() =>
            {
                action.MotionTrack = action.MotionTrack ?? new MotionTrackJsonDto();
                var events = new List<MotionTrackEventJsonDto>(action.MotionTrack.Events ?? Array.Empty<MotionTrackEventJsonDto>());
                events.Add(CreateControlScaleEvent(action, frame, reset));
                action.MotionTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }

        private static MotionTrackEventJsonDto CreateControlScaleEvent(CharacterActionJsonDto action, int frame, bool reset)
        {
            return new MotionTrackEventJsonDto
            {
                Frame = frame,
                EndFrame = frame,
                Kind = CharacterActionTrackEventKind.SetControlScale.ToString(),
                MovementControlScale = reset ? 1f : 0f,
                RotationControlScale = reset ? 1f : 0f,
                StableEventId = CharacterActionAuthoringIds.Resolve(
                    CharacterActionTimelineIdentity.MakeStableEventId(action, "control_scale"), 401),
            };
        }

        private void AddControlScaleFields(VisualElement row, MotionTrackEventJsonDto evt)
        {
            AddToggle(row, "设置移动控制倍率", evt.MovementControlScale.HasValue, value =>
            {
                evt.MovementControlScale = value ? Clamp01(evt.MovementControlScale ?? 0f) : (float?)null;
            });
            if (evt.MovementControlScale.HasValue)
                AddFloatField(row, "移动控制倍率", evt.MovementControlScale.Value, value => evt.MovementControlScale = Clamp01(value));

            AddToggle(row, "设置旋转控制倍率", evt.RotationControlScale.HasValue, value =>
            {
                evt.RotationControlScale = value ? Clamp01(evt.RotationControlScale ?? 0f) : (float?)null;
            });
            if (evt.RotationControlScale.HasValue)
                AddFloatField(row, "旋转控制倍率", evt.RotationControlScale.Value, value => evt.RotationControlScale = Clamp01(value));
        }

        private void AddTimelineControlScaleFields(VisualElement row, MotionTrackEventJsonDto evt)
        {
            AddTimelineToggle(row, "设置移动控制倍率", evt.MovementControlScale.HasValue, value =>
            {
                evt.MovementControlScale = value ? Clamp01(evt.MovementControlScale ?? 0f) : (float?)null;
            });
            if (evt.MovementControlScale.HasValue)
                AddTimelineFloatField(row, "移动控制倍率", evt.MovementControlScale.Value, value => evt.MovementControlScale = Clamp01(value));

            AddTimelineToggle(row, "设置旋转控制倍率", evt.RotationControlScale.HasValue, value =>
            {
                evt.RotationControlScale = value ? Clamp01(evt.RotationControlScale ?? 0f) : (float?)null;
            });
            if (evt.RotationControlScale.HasValue)
                AddTimelineFloatField(row, "旋转控制倍率", evt.RotationControlScale.Value, value => evt.RotationControlScale = Clamp01(value));
        }

        private void AddCollisionProfileFields(VisualElement row, MotionTrackEventJsonDto evt)
        {
            AddTextField(row, "Collision Profile", evt.CollisionProfileId, value => evt.CollisionProfileId = value, domainId: 402);
            AddEnumPopup<CharacterActionCollisionProfileRestorePolicy>(
                row,
                "Restore Policy",
                evt.CollisionRestorePolicy,
                value => evt.CollisionRestorePolicy = value);
            AddIntegerField(
                row,
                "Transition Frames",
                evt.CollisionTransitionFrames,
                value => evt.CollisionTransitionFrames = Math.Max(0, value));
        }

        private void AddTimelineCollisionProfileFields(VisualElement row, MotionTrackEventJsonDto evt)
        {
            AddTimelineTextField(
                row,
                "Collision Profile",
                evt.CollisionProfileId,
                value => evt.CollisionProfileId = value,
                domainId: 402);
            AddTimelineEnumPopup<CharacterActionCollisionProfileRestorePolicy>(
                row,
                "Restore Policy",
                evt.CollisionRestorePolicy,
                value => evt.CollisionRestorePolicy = value);
            AddTimelineIntegerField(
                row,
                "Transition Frames",
                evt.CollisionTransitionFrames,
                value => evt.CollisionTransitionFrames = Math.Max(0, value));
        }

        private void AddTimelineToggle(VisualElement parent, string label, bool value, Action<bool> setter)
        {
            var field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt => ApplyTimelineFieldChange(() => setter(evt.newValue)));
            parent.Add(field);
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
        private void AddRootMotion(CharacterActionJsonDto action)
        {
            ApplyStructuralChange(() =>
            {
                action.MotionTrack = action.MotionTrack ?? new MotionTrackJsonDto();
                action.MotionTrack.UsesRootMotion = true;
                action.MotionTrack.RootMotion = action.MotionTrack.RootMotion
                    ?? CharacterActionAuthoringModel.CreateRootMotionDraftFromAnimation(action);
            }, UiRefreshScope.Tracks);
        }

        private void RemoveRootMotion(CharacterActionJsonDto action)
        {
            ApplyStructuralChange(() =>
            {
                if (action?.MotionTrack != null)
                    action.MotionTrack.RootMotion = null;
            }, UiRefreshScope.Tracks);
        }

        private void RemoveMotionEvent(CharacterActionJsonDto action, MotionTrackEventJsonDto evt, int fallbackIndex)
        {
            ApplyStructuralChange(() =>
            {
                var events = new List<MotionTrackEventJsonDto>(action.MotionTrack?.Events ?? Array.Empty<MotionTrackEventJsonDto>());
                int index = ResolveEventIndex(events, evt, CharacterActionAuthoringIds.Label(evt?.StableEventId ?? 0, 401), fallbackIndex,
                    candidate => CharacterActionAuthoringIds.Label(candidate?.StableEventId ?? 0, 401));
                if (index >= 0)
                    events.RemoveAt(index);
                if (action.MotionTrack != null)
                    action.MotionTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }

        private void BakeVerticalRootMotionFromAnimation(
            CharacterActionJsonDto action,
            MotionTrackEventJsonDto verticalEvent)
        {
            ApplyStructuralChange(() =>
            {
                AnimationTrackEventJsonDto source = null;
                AnimationTrackEventJsonDto[] animationEvents =
                    action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>();
                for (int i = 0; i < animationEvents.Length; i++)
                {
                    AnimationTrackEventJsonDto candidate = animationEvents[i];
                    if (candidate != null && candidate.Frame <= verticalEvent.Frame
                        && candidate.EndFrame >= verticalEvent.EndFrame
                        && !string.IsNullOrWhiteSpace(candidate.AnimationClipResourceKey))
                    {
                        source = candidate;
                        break;
                    }
                }
                if (source == null
                    || !TryLoadRootMotionClip(source.AnimationClipResourceKey, out AnimationClip clip))
                {
                    Debug.LogWarning("Vertical root motion bake requires a covering animation clip.");
                    return;
                }
                AnimationCurve rootY = FindRootCurve(clip, "RootT.y", "m_LocalPosition.y", "localPosition.y");
                if (rootY == null)
                {
                    Debug.LogWarning("Vertical root motion bake found no Y root curve. clip=" + clip.name);
                    return;
                }

                int start = Math.Max(0, verticalEvent.Frame);
                int end = Math.Max(start, verticalEvent.EndFrame);
                var samples = new VerticalRootMotionFrameJsonDto[end - start + 1];
                float speed = source.PlaybackSpeed <= 0.0001f ? 1f : source.PlaybackSpeed;
                float previous = rootY.Evaluate(Mathf.Clamp(
                    ResolveClipSampleTime(start, source, speed, CharacterActionTimelineLayout.EditorTimelineFps),
                    0f, clip.length));
                for (int frame = start; frame <= end; frame++)
                {
                    float current = rootY.Evaluate(Mathf.Clamp(
                        ResolveClipSampleTime(frame, source, speed, CharacterActionTimelineLayout.EditorTimelineFps),
                        0f, clip.length));
                    samples[frame - start] = new VerticalRootMotionFrameJsonDto
                    {
                        Frame = frame,
                        Y = frame == start ? 0f : current - previous,
                    };
                    previous = current;
                }
                action.MotionTrack.VerticalRootMotion = new VerticalRootMotionTrackJsonDto
                {
                    SourceAnimationEventId = source.StableEventId,
                    SourceAnimationClipResourceKey = source.AnimationClipResourceKey,
                    Frames = samples,
                };
            }, UiRefreshScope.Tracks);
        }

        private void GenerateRootMotionFromAnimation(CharacterActionJsonDto action)
        {
            ApplyStructuralChange(() =>
            {
                action.MotionTrack = action.MotionTrack ?? new MotionTrackJsonDto();
                action.MotionTrack.UsesRootMotion = true;
                RootMotionTrackJsonDto rootMotion = action.MotionTrack.RootMotion
                    ?? CharacterActionAuthoringModel.CreateRootMotionDraftFromAnimation(action);
                if (!CharacterActionAuthoringModel.TryResolveRootMotionSourceAnimationEvent(
                        action,
                        rootMotion,
                        preferredFrame: -1,
                        out AnimationTrackEventJsonDto animationEvent))
                {
                    Debug.LogWarning("RootMotion generation failed. The action has no animation event.");
                    action.MotionTrack.RootMotion = rootMotion;
                    return;
                }

                CharacterActionAuthoringModel.ApplyRootMotionSource(rootMotion, animationEvent);
                if (!TryLoadRootMotionClip(rootMotion.SourceAnimationClipResourceKey, out AnimationClip clip))
                {
                    Debug.LogWarning("RootMotion generation failed. Animation clip was not found. key=" + rootMotion.SourceAnimationClipResourceKey);
                    action.MotionTrack.RootMotion = rootMotion;
                    return;
                }

                if (!TryBakeRootMotionFrames(clip, animationEvent, rootMotion.SampleTickRate, out RootMotionFrameJsonDto[] frames))
                {
                    Debug.LogWarning("RootMotion generation failed. Root position curves were not found. clip=" + clip.name);
                    action.MotionTrack.RootMotion = rootMotion;
                    return;
                }

                int startFrame = Math.Min(animationEvent.Frame, animationEvent.EndFrame);
                int endFrame = Math.Max(animationEvent.Frame, animationEvent.EndFrame);
                rootMotion.Space = CharacterActionRootMotionSpace.Local.ToString();
                rootMotion.SampleTickRate = rootMotion.SampleTickRate <= 0
                    ? CharacterActionAnimationKeyUtility.TimelineFramesPerSecond
                    : rootMotion.SampleTickRate;
                rootMotion.Frames = CharacterActionAuthoringModel.MergeRootMotionFrames(
                    rootMotion.Frames,
                    frames,
                    startFrame,
                    endFrame);
                action.MotionTrack.RootMotion = rootMotion;
            }, UiRefreshScope.Tracks);
        }

        private void GenerateAllRootMotionFromAnimationEvents(CharacterActionJsonDto action)
        {
            ApplyStructuralChange(() =>
            {
                action.MotionTrack = action.MotionTrack ?? new MotionTrackJsonDto();
                action.MotionTrack.UsesRootMotion = true;
                RootMotionTrackJsonDto rootMotion = action.MotionTrack.RootMotion
                    ?? CharacterActionAuthoringModel.CreateRootMotionDraftFromAnimation(action);
                AnimationTrackEventJsonDto[] sourceEvents = action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>();
                var events = new List<AnimationTrackEventJsonDto>(sourceEvents.Length);
                for (int i = 0; i < sourceEvents.Length; i++)
                {
                    if (sourceEvents[i] != null)
                        events.Add(sourceEvents[i]);
                }

                events.Sort((left, right) =>
                {
                    int compare = left.Frame.CompareTo(right.Frame);
                    return compare != 0 ? compare : left.EndFrame.CompareTo(right.EndFrame);
                });

                RootMotionFrameJsonDto[] merged = Array.Empty<RootMotionFrameJsonDto>();
                bool bakedAny = false;
                bool sourceAssigned = rootMotion.SourceAnimationEventId > 0
                    || !string.IsNullOrWhiteSpace(rootMotion.SourceAnimationClipResourceKey);
                for (int i = 0; i < events.Count; i++)
                {
                    AnimationTrackEventJsonDto animationEvent = events[i];
                    string clipKey = CharacterActionAnimationKeyUtility.ResolveEffectiveKey(
                        animationEvent.AnimationClipResourceKey,
                        animationEvent.AnimationActionKey);
                    if (string.IsNullOrWhiteSpace(clipKey))
                        continue;

                    if (!TryLoadRootMotionClip(clipKey, out AnimationClip clip))
                    {
                        Debug.LogWarning("RootMotion bake skipped. Animation clip was not found. key=" + clipKey);
                        continue;
                    }

                    if (!TryBakeRootMotionFrames(clip, animationEvent, rootMotion.SampleTickRate, out RootMotionFrameJsonDto[] frames))
                    {
                        Debug.LogWarning("RootMotion bake skipped. Root position curves were not found. clip=" + clip.name);
                        continue;
                    }

                    int startFrame = Math.Min(animationEvent.Frame, animationEvent.EndFrame);
                    int endFrame = Math.Max(animationEvent.Frame, animationEvent.EndFrame);
                    merged = CharacterActionAuthoringModel.MergeRootMotionFrames(
                        merged,
                        frames,
                        startFrame,
                        endFrame);
                    if (!sourceAssigned)
                    {
                        CharacterActionAuthoringModel.ApplyRootMotionSource(rootMotion, animationEvent);
                        sourceAssigned = true;
                    }

                    bakedAny = true;
                }

                if (!bakedAny)
                {
                    Debug.LogWarning("RootMotion bake failed. No animation event produced root motion samples.");
                    action.MotionTrack.RootMotion = rootMotion;
                    return;
                }

                rootMotion.Space = CharacterActionRootMotionSpace.Local.ToString();
                rootMotion.SampleTickRate = rootMotion.SampleTickRate <= 0
                    ? CharacterActionAnimationKeyUtility.TimelineFramesPerSecond
                    : rootMotion.SampleTickRate;
                rootMotion.Frames = merged;
                action.MotionTrack.RootMotion = rootMotion;
            }, UiRefreshScope.Tracks);
        }

        private void AddRootMotionSourceEventField(
            VisualElement parent,
            CharacterActionJsonDto action,
            RootMotionTrackJsonDto rootMotion,
            bool timelineInspector)
        {
            AnimationTrackEventJsonDto[] events = action?.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>();
            var labels = new List<string> { "<No animation event>" };
            var eventIndices = new List<int> { -1 };
            int selectedIndex = 0;
            string sourceEventId = CharacterActionAuthoringIds.Label(rootMotion?.SourceAnimationEventId ?? 0, 401);

            for (int i = 0; i < events.Length; i++)
            {
                AnimationTrackEventJsonDto animationEvent = events[i];
                if (animationEvent == null)
                    continue;

                labels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(BuildRootMotionSourceEventLabel(animationEvent, i)));
                eventIndices.Add(i);
                if (!string.IsNullOrWhiteSpace(sourceEventId)
                    && string.Equals(CharacterActionAuthoringIds.Label(animationEvent.StableEventId, 401), sourceEventId, StringComparison.Ordinal))
                {
                    selectedIndex = labels.Count - 1;
                }
            }

            if (selectedIndex == 0
                && CharacterActionAuthoringModel.TryResolveRootMotionSourceAnimationEvent(
                    action,
                    rootMotion,
                    preferredFrame: -1,
                    out AnimationTrackEventJsonDto resolved))
            {
                for (int i = 0; i < events.Length; i++)
                {
                    if (!ReferenceEquals(events[i], resolved))
                        continue;

                    selectedIndex = eventIndices.IndexOf(i);
                    break;
                }
            }

            var popup = new PopupField<string>("来源动画事件", labels, selectedIndex);
            popup.RegisterValueChangedCallback(evt =>
            {
                int selected = labels.IndexOf(evt.newValue);
                if (selected < 0 || selected >= eventIndices.Count)
                    return;

                int eventIndex = eventIndices[selected];
                if (eventIndex < 0 || eventIndex >= events.Length)
                    return;

                void Mutate()
                {
                    CharacterActionAuthoringModel.ApplyRootMotionSource(rootMotion, events[eventIndex]);
                    SetRootMotionEditRange(events[eventIndex], action);
                }

                if (timelineInspector)
                    ApplyTimelineFieldChange(Mutate, requestFullTracksRefresh: true);
                else
                    ApplyStructuralChange(Mutate, UiRefreshScope.Tracks);
            });
            parent.Add(popup);
        }

        private void AddRootMotionAdjustmentControls(
            VisualElement parent,
            CharacterActionJsonDto action,
            RootMotionTrackJsonDto rootMotion,
            bool timelineInspector)
        {
            EnsureRootMotionEditRange(action, rootMotion);
            var foldout = new Foldout { text = "RootMotion 调整", value = false };

            var rangeRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                },
            };
            var startField = new IntegerField("开始帧")
            {
                value = _rootMotionEditStartFrame,
                isDelayed = true,
            };
            startField.RegisterValueChangedCallback(evt =>
            {
                _rootMotionEditStartFrame = Math.Max(0, evt.newValue);
                _rootMotionEditRangeInitialized = true;
            });
            rangeRow.Add(startField);

            var endField = new IntegerField("结束帧")
            {
                value = _rootMotionEditEndFrame,
                isDelayed = true,
            };
            endField.RegisterValueChangedCallback(evt =>
            {
                _rootMotionEditEndFrame = Math.Max(0, evt.newValue);
                _rootMotionEditRangeInitialized = true;
            });
            rangeRow.Add(endField);
            foldout.Add(rangeRow);

            var scaleRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                },
            };
            AddRootMotionScaleField(scaleRow, "缩放 X", _rootMotionEditScaleX, value => _rootMotionEditScaleX = value);
            AddRootMotionScaleField(scaleRow, "缩放 Z", _rootMotionEditScaleZ, value => _rootMotionEditScaleZ = value);
            foldout.Add(scaleRow);

            AddButtonRow(
                foldout,
                ("应用缩放", () => ScaleRootMotionEditRange(action, timelineInspector)),
                ("范围归零", () => ZeroRootMotionEditRange(action, timelineInspector)));
            parent.Add(foldout);
        }

        private static void AddRootMotionScaleField(VisualElement parent, string label, float value, Action<float> setter)
        {
            var field = new FloatField(label)
            {
                value = value,
                isDelayed = true,
            };
            field.style.minWidth = 120f;
            field.RegisterValueChangedCallback(evt => setter(evt.newValue));
            parent.Add(field);
        }

        private static string BuildRootMotionSourceEventLabel(AnimationTrackEventJsonDto animationEvent, int index)
        {
            string eventId = animationEvent.StableEventId <= 0
                ? "animation[" + index + "]"
                : CharacterActionAuthoringIds.Label(animationEvent.StableEventId, 401);
            int startFrame = Math.Min(animationEvent.Frame, animationEvent.EndFrame);
            int endFrame = Math.Max(animationEvent.Frame, animationEvent.EndFrame);
            string span = startFrame == endFrame
                ? startFrame.ToString()
                : startFrame + "-" + endFrame;
            string clipKey = CharacterActionAnimationKeyUtility.ResolveEffectiveKey(
                animationEvent.AnimationClipResourceKey,
                animationEvent.AnimationActionKey);
            return string.IsNullOrWhiteSpace(clipKey)
                ? eventId + "  " + span
                : eventId + "  " + span + "  " + clipKey;
        }

        private void ClearRootMotionSamples(CharacterActionJsonDto action)
        {
            ApplyStructuralChange(() =>
            {
                if (action?.MotionTrack?.RootMotion != null)
                    action.MotionTrack.RootMotion.Frames = Array.Empty<RootMotionFrameJsonDto>();
            }, UiRefreshScope.Tracks);
        }

        private void ClearRootMotionSourceRange(CharacterActionJsonDto action)
        {
            ApplyStructuralChange(() =>
            {
                RootMotionTrackJsonDto rootMotion = action?.MotionTrack?.RootMotion;
                if (rootMotion == null)
                    return;

                if (!CharacterActionAuthoringModel.TryResolveRootMotionSourceAnimationEvent(
                        action,
                        rootMotion,
                        preferredFrame: -1,
                        out AnimationTrackEventJsonDto animationEvent))
                {
                    Debug.LogWarning("RootMotion clear source range failed. The action has no matching animation event.");
                    return;
                }

                int startFrame = Math.Min(animationEvent.Frame, animationEvent.EndFrame);
                int endFrame = Math.Max(animationEvent.Frame, animationEvent.EndFrame);
                rootMotion.Frames = CharacterActionAuthoringModel.MergeRootMotionFrames(
                    rootMotion.Frames,
                    Array.Empty<RootMotionFrameJsonDto>(),
                    startFrame,
                    endFrame);
            }, UiRefreshScope.Tracks);
        }

        private void ScaleRootMotionEditRange(CharacterActionJsonDto action, bool timelineInspector)
        {
            ApplyRootMotionEditMutation(
                action,
                rootMotion =>
                {
                    ResolveRootMotionEditRange(out int startFrame, out int endFrame);
                    rootMotion.Frames = CharacterActionAuthoringModel.ScaleRootMotionFrames(
                        rootMotion.Frames,
                        startFrame,
                        endFrame,
                        _rootMotionEditScaleX,
                        0f,
                        _rootMotionEditScaleZ);
                },
                timelineInspector);
        }

        private void ZeroRootMotionEditRange(CharacterActionJsonDto action, bool timelineInspector)
        {
            ApplyRootMotionEditMutation(
                action,
                rootMotion =>
                {
                    ResolveRootMotionEditRange(out int startFrame, out int endFrame);
                    rootMotion.Frames = CharacterActionAuthoringModel.ZeroRootMotionFrames(
                        rootMotion.Frames,
                        startFrame,
                        endFrame);
                },
                timelineInspector);
        }

        private void ApplyRootMotionEditMutation(
            CharacterActionJsonDto action,
            Action<RootMotionTrackJsonDto> mutate,
            bool timelineInspector)
        {
            void Mutate()
            {
                RootMotionTrackJsonDto rootMotion = action?.MotionTrack?.RootMotion;
                if (rootMotion == null)
                    return;

                mutate?.Invoke(rootMotion);
            }

            if (timelineInspector)
                ApplyTimelineFieldChange(Mutate, requestFullTracksRefresh: true);
            else
                ApplyStructuralChange(Mutate, UiRefreshScope.Tracks);
        }

        private void EnsureRootMotionEditRange(CharacterActionJsonDto action, RootMotionTrackJsonDto rootMotion)
        {
            int actionId = action?.ActionId ?? 0;
            if (_rootMotionEditRangeInitialized
                && _rootMotionEditActionId == actionId)
            {
                return;
            }

            if (CharacterActionAuthoringModel.TryResolveRootMotionSourceAnimationEvent(
                    action,
                    rootMotion,
                    preferredFrame: -1,
                    out AnimationTrackEventJsonDto animationEvent))
            {
                SetRootMotionEditRange(animationEvent, action);
                return;
            }

            RootMotionFrameJsonDto[] frames = rootMotion?.Frames ?? Array.Empty<RootMotionFrameJsonDto>();
            if (frames.Length > 0)
            {
                int startFrame = frames[0]?.Frame ?? 0;
                int endFrame = startFrame;
                for (int i = 1; i < frames.Length; i++)
                {
                    startFrame = Math.Min(startFrame, frames[i]?.Frame ?? 0);
                    endFrame = Math.Max(endFrame, frames[i]?.Frame ?? 0);
                }

                SetRootMotionEditRange(startFrame, endFrame, actionId);
                return;
            }

            int actionEndFrame = Math.Max(0, (action?.DurationFrames ?? 1) - 1);
            SetRootMotionEditRange(0, actionEndFrame, actionId);
        }

        private void SetRootMotionEditRange(AnimationTrackEventJsonDto animationEvent, CharacterActionJsonDto action)
        {
            if (animationEvent == null)
                return;

            SetRootMotionEditRange(
                Math.Min(animationEvent.Frame, animationEvent.EndFrame),
                Math.Max(animationEvent.Frame, animationEvent.EndFrame),
                action?.ActionId ?? 0);
        }

        private void SetRootMotionEditRange(int startFrame, int endFrame, int actionId)
        {
            if (endFrame < startFrame)
            {
                int temp = startFrame;
                startFrame = endFrame;
                endFrame = temp;
            }

            _rootMotionEditStartFrame = Math.Max(0, startFrame);
            _rootMotionEditEndFrame = Math.Max(_rootMotionEditStartFrame, endFrame);
            _rootMotionEditActionId = actionId;
            _rootMotionEditRangeInitialized = true;
        }

        private void ResolveRootMotionEditRange(out int startFrame, out int endFrame)
        {
            startFrame = _rootMotionEditStartFrame;
            endFrame = _rootMotionEditEndFrame;
            if (endFrame < startFrame)
            {
                int temp = startFrame;
                startFrame = endFrame;
                endFrame = temp;
            }

            startFrame = Math.Max(0, startFrame);
            endFrame = Math.Max(startFrame, endFrame);
        }

        private bool TryLoadRootMotionClip(string clipKey, out AnimationClip clip)
        {
            clip = null;
            if (string.IsNullOrWhiteSpace(clipKey))
                return false;

            string characterStableId = _model?.ActionSet?.CharacterStableId ?? string.Empty;
            if (CharacterActionPreviewBindingDiscovery.TryFindAnimationClipChoice(clipKey, characterStableId, out CharacterActionAnimationClipChoice choice))
                return CharacterActionPreviewBindingDiscovery.TryLoadAnimationClip(choice, out clip);

            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipKey);
            return clip != null;
        }

        private static bool TryBakeRootMotionFrames(
            AnimationClip clip,
            AnimationTrackEventJsonDto animationEvent,
            int sampleTickRate,
            out RootMotionFrameJsonDto[] frames)
        {
            frames = Array.Empty<RootMotionFrameJsonDto>();
            if (clip == null || animationEvent == null)
                return false;

            AnimationCurve rootX = FindRootCurve(clip, "RootT.x", "m_LocalPosition.x", "localPosition.x");
            AnimationCurve rootY = null;
            AnimationCurve rootZ = FindRootCurve(clip, "RootT.z", "m_LocalPosition.z", "localPosition.z");
            if (rootX == null && rootY == null && rootZ == null)
                return false;

            int tickRate = sampleTickRate <= 0
                ? CharacterActionAnimationKeyUtility.TimelineFramesPerSecond
                : sampleTickRate;
            int startFrame = Math.Max(0, animationEvent.Frame);
            int endFrame = Math.Max(startFrame, animationEvent.EndFrame);
            float playbackSpeed = animationEvent.PlaybackSpeed <= 0.0001f ? 1f : animationEvent.PlaybackSpeed;
            var samples = new List<RootMotionFrameJsonDto>(endFrame - startFrame + 1);
            Vector3 previous = SampleRootPosition(
                rootX,
                rootY,
                rootZ,
                ResolveClipSampleTime(startFrame, animationEvent, playbackSpeed, tickRate),
                clip.length);

            for (int actionFrame = startFrame; actionFrame <= endFrame; actionFrame++)
            {
                Vector3 current = SampleRootPosition(
                    rootX,
                    rootY,
                    rootZ,
                    ResolveClipSampleTime(actionFrame, animationEvent, playbackSpeed, tickRate),
                    clip.length);
                Vector3 delta = actionFrame == startFrame ? Vector3.zero : current - previous;
                samples.Add(new RootMotionFrameJsonDto
                {
                    Frame = actionFrame,
                    X = delta.x,
                    Y = 0f,
                    Z = delta.z,
                });
                previous = current;
            }

            frames = samples.ToArray();
            return true;
        }

        private static AnimationCurve FindRootCurve(AnimationClip clip, params string[] propertyNames)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                for (int j = 0; j < propertyNames.Length; j++)
                {
                    if (!string.Equals(binding.propertyName, propertyNames[j], StringComparison.Ordinal))
                        continue;
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve != null)
                        return curve;
                }
            }

            return null;
        }

        private static float ResolveClipSampleTime(
            int actionFrame,
            AnimationTrackEventJsonDto animationEvent,
            float playbackSpeed,
            int sampleTickRate)
        {
            int localFrame = Math.Max(0, actionFrame - animationEvent.Frame);
            int scaledFrame = Mathf.Max(0, Mathf.RoundToInt(localFrame * playbackSpeed));
            int clipFrame = Math.Max(0, animationEvent.AnimationStartOffsetFrames + scaledFrame);
            return clipFrame / (float)Math.Max(1, sampleTickRate);
        }

        private static Vector3 SampleRootPosition(
            AnimationCurve rootX,
            AnimationCurve rootY,
            AnimationCurve rootZ,
            float time,
            float clipLength)
        {
            float t = Mathf.Clamp(time, 0f, Math.Max(0f, clipLength));
            return new Vector3(
                rootX != null ? rootX.Evaluate(t) : 0f,
                rootY != null ? rootY.Evaluate(t) : 0f,
                rootZ != null ? rootZ.Evaluate(t) : 0f);
        }
    }
}

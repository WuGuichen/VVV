using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Editor.CharacterAction.Timeline;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void BuildGameplayTrack(VisualElement parent, CharacterActionJsonDto action)
        {
            if (action.GameplayTrack == null)
            {
                parent.Add(new Button(() => ApplyStructuralChange(
                    () => action.GameplayTrack = new GameplayTrackJsonDto(),
                    UiRefreshScope.Tracks))
                {
                    text = "启用玩法轨道",
                });
                return;
            }

            GameplayTrackEventJsonDto[] events = action.GameplayTrack.Events ?? Array.Empty<GameplayTrackEventJsonDto>();
            for (int i = 0; i < events.Length; i++)
                AddGameplayEvent(parent, action, events, i);

            AddButtonRow(
                parent,
                ("添加玩法效果", () => AddGameplayEvent(action, CharacterActionTrackEventKind.ApplyGameplayEffect)),
                ("添加玩法请求", () => AddGameplayEvent(action, CharacterActionTrackEventKind.SendGameplayRequest)),
                ("禁用玩法轨道", () => ApplyStructuralChange(() => action.GameplayTrack = null, UiRefreshScope.Tracks)));
        }

        private void BuildGameplayEventInspector(
            VisualElement parent,
            CharacterActionJsonDto action,
            int index,
            bool timelineInspector = false)
        {
            if (action.GameplayTrack == null)
            {
                parent.Add(new Label("玩法轨道未启用。"));
                return;
            }

            GameplayTrackEventJsonDto[] events = action.GameplayTrack.Events ?? Array.Empty<GameplayTrackEventJsonDto>();
            if (index < 0 || index >= events.Length)
            {
                parent.Add(new Label("玩法事件不存在。"));
                return;
            }

            AddGameplayEvent(parent, action, events, index, timelineInspector);
        }

        private void AddGameplayEvent(
            VisualElement parent,
            CharacterActionJsonDto action,
            GameplayTrackEventJsonDto[] events,
            int index,
            bool timelineInspector = false)
        {
            GameplayTrackEventJsonDto evt = events[index] ?? new GameplayTrackEventJsonDto();
            CharacterActionTrackEventKind kind = ResolveGameplayEventKind(evt);
            VisualElement row = AddCard(parent, BuildGameplayCardTitle(evt, kind));
            CharacterActionProjectileAuthoringStore projectileStore = null;
            bool isProjectileRequest = false;
            if (kind == CharacterActionTrackEventKind.SendGameplayRequest)
            {
                projectileStore = LoadProjectileAuthoringStoreForWindow();
                isProjectileRequest = CharacterActionProjectileRequestMembership.Contains(
                    projectileStore,
                    CharacterActionAuthoringIds.Label(evt.RequestId, 408));
            }

            if (isProjectileRequest)
            {
                AddProjectileGameplayEventEditor(
                    row,
                    action,
                    evt,
                    projectileStore,
                    timelineInspector);
                AddProjectileSpecialTuning(row, CharacterActionAuthoringIds.Label(evt.RequestId, 408));
                AddGameplayDeleteButton(row, action, evt, index, emphasize: true);
                events[index] = evt;
                return;
            }

            row.Add(new Label(BuildGameplayEventSummary(evt, kind)));

            if (timelineInspector)
            {
                AddTimelineIntegerField(row, "触发帧", evt.Frame, value => evt.Frame = Math.Max(0, value));
                AddTimelineEnumPopup<CharacterActionTrackEventKind>(
                    row,
                    "事件类型",
                    evt.Kind,
                    value => evt.Kind = value,
                    requestFullTracksRefresh: true);
                AddGameplayPayloadFields(row, action, evt, kind, timelineInspector: true);
                AddTimelineTextField(row, "稳定事件 ID", evt.StableEventId, value => evt.StableEventId = value);
            }
            else
            {
                AddIntegerField(row, "触发帧", evt.Frame, value => evt.Frame = Math.Max(0, value));
                AddEnumPopup<CharacterActionTrackEventKind>(row, "事件类型", evt.Kind, value => evt.Kind = value);
                AddGameplayPayloadFields(row, action, evt, kind, timelineInspector: false);
                AddTextField(row, "稳定事件 ID", evt.StableEventId, value => evt.StableEventId = value);
            }

            AddGameplayDeleteButton(row, action, evt, index);
            events[index] = evt;
        }

        private void AddGameplayDeleteButton(
            VisualElement parent,
            CharacterActionJsonDto action,
            GameplayTrackEventJsonDto evt,
            int index,
            bool emphasize = false)
        {
            var delete = new Button(() => RemoveGameplayEvent(action, evt, index))
            {
                text = "删除事件",
            };
            if (emphasize)
                delete.AddToClassList("ca-projectile-delete");
            parent.Add(delete);
        }

        private void AddProjectileSpecialTuning(VisualElement parent, string requestId)
        {
            if (string.Equals(requestId, "character_test.arc_mine.deploy", StringComparison.Ordinal))
                AddArcMineRequestInspector(parent);
            else if (string.Equals(requestId, "character_test.rune_turret.deploy", StringComparison.Ordinal))
                AddRuneTurretRequestInspector(parent);
        }

        private void AddGameplayPayloadFields(
            VisualElement row,
            CharacterActionJsonDto action,
            GameplayTrackEventJsonDto evt,
            CharacterActionTrackEventKind kind,
            bool timelineInspector)
        {
            if (kind == CharacterActionTrackEventKind.ApplyGameplayEffect)
            {
                AddSharedGameplayHitEventFields(row, evt, timelineInspector, requestFullTracksRefresh: true);
                AddTargetPolicyField(row, "目标策略", evt.TargetPolicyId, value => evt.TargetPolicyId = value);
                return;
            }

            if (kind == CharacterActionTrackEventKind.CastAbility)
            {
                if (timelineInspector)
                    AddTimelineTextField(row, "能力 StableId", evt.AbilityStableId, value => evt.AbilityStableId = value);
                else
                    AddTextField(row, "能力 StableId", evt.AbilityStableId, value => evt.AbilityStableId = value);
                return;
            }

            if (kind == CharacterActionTrackEventKind.StartEffectTimeline)
            {
                if (timelineInspector)
                    AddTimelineTextField(row, "Effect Timeline ID", evt.EffectTimelineId, value => evt.EffectTimelineId = value);
                else
                    AddTextField(row, "Effect Timeline ID", evt.EffectTimelineId, value => evt.EffectTimelineId = value);
                return;
            }

            CharacterActionProjectileAuthoringStore projectileStore =
                LoadProjectileAuthoringStoreForWindow();
            AddGameplayRequestField(
                row,
                "请求 ID",
                evt.RequestId,
                projectileStore,
                action,
                value => evt.RequestId = value,
                timelineInspector);

            string requestLabel = CharacterActionAuthoringIds.Label(evt.RequestId, 408);
            if (CharacterActionProjectileRequestMembership.Contains(projectileStore, requestLabel))
                AddProjectileRequestReadOnlySummary(row, requestLabel, projectileStore, action);
                if (string.Equals(requestLabel, "character_test.arc_mine.deploy", StringComparison.Ordinal))
                    AddArcMineRequestInspector(row);
                else if (string.Equals(requestLabel, "character_test.rune_turret.deploy", StringComparison.Ordinal))
                AddRuneTurretRequestInspector(row);
        }

        private void AddGameplayEvent(CharacterActionJsonDto action, CharacterActionTrackEventKind kind)
        {
            ApplyStructuralChange(() =>
            {
                action.GameplayTrack = action.GameplayTrack ?? new GameplayTrackJsonDto();
                var events = new List<GameplayTrackEventJsonDto>(
                    action.GameplayTrack.Events ?? Array.Empty<GameplayTrackEventJsonDto>());
                var evt = new GameplayTrackEventJsonDto
                {
                    Frame = 0,
                    Kind = kind.ToString(),
                    RequestId = kind == CharacterActionTrackEventKind.SendGameplayRequest
                        ? CharacterActionAuthoringIds.Resolve("gameplay." + action.ActionId, 408)
                        : 0,
                    AbilityStableId = kind == CharacterActionTrackEventKind.CastAbility
                        ? CharacterActionAuthoringIds.Resolve("ability." + action.ActionId, 404)
                        : 0,
                    EffectTimelineId = kind == CharacterActionTrackEventKind.StartEffectTimeline
                        ? CharacterActionAuthoringIds.Resolve("effect." + action.ActionId, 407)
                        : 0,
                    TargetPolicyId = kind == CharacterActionTrackEventKind.ApplyGameplayEffect
                        ? CharacterActionAuthoringIds.Resolve("target.area_2m", 406)
                        : 0,
                    StableEventId = CharacterActionAuthoringIds.Resolve(
                        CharacterActionTimelineIdentity.MakeStableEventId(action, kind.ToString()), 401),
                };
                if (kind == CharacterActionTrackEventKind.ApplyGameplayEffect)
                    ApplyDefaultAreaDamage(evt);
                events.Add(evt);
                action.GameplayTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }

        private void AddTargetPolicyField(
            VisualElement parent,
            string label,
            string value,
            Action<string> setter)
        {
            List<string> rawValues = BuildTargetPolicyChoices(value);
            var labels = new List<string>(rawValues.Count);
            int selectedIndex = 0;
            value = value ?? string.Empty;
            for (int i = 0; i < rawValues.Count; i++)
            {
                labels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(FormatTargetPolicyChoiceLabel(rawValues[i])));
                if (string.Equals(rawValues[i], value, StringComparison.Ordinal))
                    selectedIndex = i;
            }

            var field = new PopupField<string>(label, labels, selectedIndex)
            {
                tooltip = "选择非武器伤害事件的目标策略。CharacterTest 当前支持 area_2m 一类范围策略。",
            };
            field.RegisterValueChangedCallback(evt =>
            {
                int index = labels.IndexOf(evt.newValue);
                if (index < 0 || index >= rawValues.Count)
                    return;
                ApplyFieldChange(() => setter(rawValues[index]));
            });
            parent.Add(field);

            if (string.IsNullOrWhiteSpace(value))
                parent.Add(new HelpBox("未选择目标策略时，ApplyGameplayEffect 无法找到目标。", HelpBoxMessageType.Warning));
        }

        private static List<string> BuildTargetPolicyChoices(string currentValue)
        {
            var choices = new List<string>();
            AddTargetPolicyChoice(choices, currentValue);
            AddTargetPolicyChoice(choices, "target.area_2m");
            AddTargetPolicyChoice(choices, "target.locked.area_2m");
            return choices;
        }

        private static void AddTargetPolicyChoice(List<string> choices, string value)
        {
            value = value ?? string.Empty;
            if (choices.Contains(value))
                return;
            if (string.IsNullOrWhiteSpace(value) && choices.Count > 0)
                return;
            choices.Add(value);
        }

        private static string FormatTargetPolicyChoiceLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "未选择";
            if (string.Equals(value, "target.area_2m", StringComparison.Ordinal))
                return "自身周围 2m (target.area_2m)";
            if (string.Equals(value, "target.locked.area_2m", StringComparison.Ordinal))
                return "锁定目标周围 2m (target.locked.area_2m)";
            return value;
        }

        private void AddGameplayRequestField(
            VisualElement parent,
            string label,
            string value,
            CharacterActionProjectileAuthoringStore projectileStore,
            CharacterActionJsonDto action,
            Action<string> setter,
            bool timelineInspector)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                isDelayed = true,
            };
            field.style.flexGrow = 1f;
            field.RegisterValueChangedCallback(evt =>
            {
                string next = evt.newValue ?? string.Empty;
                ApplyGameplayRequestValue(setter, next, timelineInspector);
            });
            row.Add(field);

            Button chooseButton = null;
            chooseButton = new Button(() => OpenGameplayRequestPicker(
                chooseButton,
                value,
                projectileStore,
                setter,
                timelineInspector))
            {
                text = "选择...",
                tooltip = "搜索、收藏或选择已有 Gameplay Request ID。",
            };
            row.Add(chooseButton);

            parent.Add(row);
        }

        private void OpenGameplayRequestPicker(
            VisualElement anchor,
            string currentValue,
            CharacterActionProjectileAuthoringStore projectileStore,
            Action<string> setter,
            bool timelineInspector)
        {
            string scope = BuildGameplayRequestPickerScope();
            HashSet<string> favorites = CharacterActionGameplayRequestPickerCatalog.LoadSet(scope, "favorites");
            HashSet<string> recent = CharacterActionGameplayRequestPickerCatalog.LoadSet(scope, "recent");
            List<CharacterActionGameplayRequestPickerItem> items =
                CharacterActionGameplayRequestPickerCatalog.BuildItems(
                    currentValue,
                    projectileStore,
                    _model,
                    favorites,
                    recent);
            CharacterActionGameplayRequestPickerWindow.Show(
                AnchorRect(anchor),
                scope,
                items,
                selected => ApplyGameplayRequestValue(setter, selected, timelineInspector));
        }

        private void ApplyGameplayRequestValue(
            Action<string> setter,
            string next,
            bool timelineInspector)
        {
            if (timelineInspector)
                ApplyTimelineFieldChange(() => setter(next ?? string.Empty), requestFullTracksRefresh: true);
            else
                ApplyStructuralChange(() => setter(next ?? string.Empty), UiRefreshScope.Tracks);
        }

        private string BuildGameplayRequestPickerScope()
        {
            return (_actionLibrarySourceDirectory ?? string.Empty)
                + "|"
                + (_model?.ActionSet?.StableId ?? string.Empty);
        }

        private static Rect AnchorRect(VisualElement anchor)
        {
            if (anchor == null)
                return new Rect(100f, 100f, 1f, 1f);
            Rect rect = anchor.worldBound;
            if (rect.width <= 0f || rect.height <= 0f)
                return new Rect(rect.x, rect.y, 1f, 20f);
            return rect;
        }

        private static void AddReadOnlyLine(VisualElement parent, string label, string value)
        {
            var row = new Label(label + "： " + EmptyGameplayValue(value));
            row.style.marginTop = 2f;
            parent.Add(row);
        }

        private static CharacterActionTrackEventKind ResolveGameplayEventKind(GameplayTrackEventJsonDto evt)
        {
            if (Enum.TryParse(evt?.Kind, out CharacterActionTrackEventKind kind))
                return kind;
            return CharacterActionTrackEventKind.SendGameplayRequest;
        }

        private static string BuildGameplayCardTitle(GameplayTrackEventJsonDto evt, CharacterActionTrackEventKind kind)
        {
            if (kind == CharacterActionTrackEventKind.SendGameplayRequest
                && IsProjectileRequestId(CharacterActionAuthoringIds.Label(evt?.RequestId ?? 0, 408)))
                return "玩法事件 - 发射投射物";
            return "玩法事件 - " + CharacterActionAuthoringText.EnumDisplayLabel(
                typeof(CharacterActionTrackEventKind),
                kind.ToString());
        }

        private static string BuildGameplayEventSummary(GameplayTrackEventJsonDto evt, CharacterActionTrackEventKind kind)
        {
            evt = evt ?? new GameplayTrackEventJsonDto();
            if (kind == CharacterActionTrackEventKind.SendGameplayRequest)
            {
                string requestLabel = CharacterActionAuthoringIds.Label(evt.RequestId, 408);
                string label = IsProjectileRequestId(requestLabel) ? "发射投射物" : "发送玩法请求";
                return label + "：请求 " + EmptyGameplayValue(requestLabel);
            }

            if (kind == CharacterActionTrackEventKind.ApplyGameplayEffect)
            {
                return "应用玩法效果：伤害 "
                    + CharacterActionDamageAuthoringText.FormatDamageSummary(evt.HitEvent?.Damage)
                    + "，目标 "
                    + EmptyGameplayValue(CharacterActionAuthoringIds.Label(evt.TargetPolicyId, 406));
            }

            if (kind == CharacterActionTrackEventKind.CastAbility)
                return "释放能力：" + EmptyGameplayValue(CharacterActionAuthoringIds.Label(evt.AbilityStableId, 404));

            if (kind == CharacterActionTrackEventKind.StartEffectTimeline)
                return "Start effect timeline: " + EmptyGameplayValue(CharacterActionAuthoringIds.Label(evt.EffectTimelineId, 407));

            return CharacterActionAuthoringText.EnumDisplayLabel(typeof(CharacterActionTrackEventKind), kind.ToString());
        }

        private static bool IsProjectileRequestId(string requestId)
        {
            return CharacterActionProjectileRequestMembership.Contains(requestId);
        }

        private static string EmptyGameplayValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未设置" : value;
        }

        private void RemoveGameplayEvent(CharacterActionJsonDto action, GameplayTrackEventJsonDto evt, int fallbackIndex)
        {
            ApplyStructuralChange(() =>
            {
                var events = new List<GameplayTrackEventJsonDto>(
                    action.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEventJsonDto>());
                int index = ResolveEventIndex(
                    events,
                    evt,
                    CharacterActionAuthoringIds.Label(evt?.StableEventId ?? 0, 401),
                    fallbackIndex,
                    candidate => CharacterActionAuthoringIds.Label(candidate?.StableEventId ?? 0, 401));
                if (index >= 0)
                    events.RemoveAt(index);
                if (action.GameplayTrack != null)
                    action.GameplayTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }
    }
}

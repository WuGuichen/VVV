using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Combat.Hit;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void BuildCombatTrack(VisualElement parent, CharacterActionJsonDto action)
        {
            if (action.CombatTrack == null)
            {
                parent.Add(new Button(() => ApplyStructuralChange(() => action.CombatTrack = new CombatTrackJsonDto(), UiRefreshScope.Tracks))
                {
                    text = "启用战斗轨道",
                });
                return;
            }

            AddReferenceTextField(parent, "战斗动作 ID", action.CombatTrack.CombatActionId, _model.ReferenceContext.CombatActionIds, value => action.CombatTrack.CombatActionId = value);
            CombatTrackEventJsonDto[] events = action.CombatTrack.Events ?? Array.Empty<CombatTrackEventJsonDto>();
            AddHitTraceWindowDiagnostics(parent, events);
            for (int i = 0; i < events.Length; i++)
                AddCombatEvent(parent, action, events, i);

            AddButtonRow(parent,
                ("添加判定窗口", () => AddHitTraceWindow(action, 0)),
                ("添加角色状态窗口", () => AddHitTargetStateWindow(action, 0)),
                ("添加战斗动作", () => AddCombatEvent(action, CharacterActionTrackEventKind.StartCombatAction)),
                ("禁用战斗轨道", () => ApplyStructuralChange(() => action.CombatTrack = null, UiRefreshScope.Tracks)));
        }

        private void BuildCombatEventInspector(
            VisualElement parent,
            CharacterActionJsonDto action,
            int index,
            bool timelineInspector = false)
        {
            if (action.CombatTrack == null)
            {
                parent.Add(new Label("战斗轨道未启用。"));
                return;
            }

            CombatTrackEventJsonDto[] events = action.CombatTrack.Events ?? Array.Empty<CombatTrackEventJsonDto>();
            if (index < 0 || index >= events.Length)
            {
                parent.Add(new Label("战斗事件不存在。"));
                return;
            }

            AddCombatEvent(parent, action, events, index, timelineInspector);
        }

        private void BuildHitTraceWindowInspector(
            VisualElement parent,
            CharacterActionJsonDto action,
            HitTraceWindowTimelineAdapter adapter)
        {
            if (action?.CombatTrack == null || adapter == null)
            {
                parent.Add(new Label("命中判定窗口不存在。"));
                return;
            }

            CombatTrackEventJsonDto trackEvent = adapter.GetEvent();
            if (trackEvent != null)
            {
                VisualElement slotRow = AddCard(parent, "命中窗口");
                slotRow.name = "character-action-hit-window";
                slotRow.AddToClassList("ca-hit-card");
                AddHitWindowRangeField(slotRow, trackEvent);
                if (!HasHitVolume(trackEvent))
                {
                    AddTimelineEquipmentSlotField(slotRow, "装备槽位", trackEvent.EquipmentSlotId, action,
                        value => trackEvent.EquipmentSlotId = value, requestFullTracksRefresh: true);
                }
                AddHitVolumeFields(slotRow, trackEvent, timeline: true, requestFullTracksRefresh: true);
                AddTimelineHitEventFields(slotRow, trackEvent, requestFullTracksRefresh: true);
                AddBladeTrailFields(slotRow, action, trackEvent);
                var maintenance = new Foldout { text = "事件标识与维护", value = false };
                maintenance.AddToClassList("ca-hit-foldout");
                AddLegacyTraceProfileHelp(maintenance, trackEvent, trackEvent);
                AddTimelineTextField(maintenance, "事件 ID", trackEvent.StableEventId, value => trackEvent.StableEventId = value, requestFullTracksRefresh: true);
                maintenance.Add(new Button(() => ApplyStructuralChange(() => adapter.Remove(), UiRefreshScope.Tracks)) { text = "删除窗口" });
                parent.Add(maintenance);
                return;
            }
            parent.Add(new Label("命中判定窗口事件已不存在。"));
        }

        private void AddBladeTrailFields(VisualElement parent, CharacterActionJsonDto action, CombatTrackEventJsonDto hit)
        {
            var enabled = new Toggle("启用武器拖尾") { value = hit.BladeTrail?.Enabled == true };
            enabled.name = "hit-window-blade-trail-enabled";
            enabled.SetEnabled(hit.EquipmentSlotId > 0);
            enabled.RegisterValueChangedCallback(change => ApplyTimelineFieldChange(() =>
            {
                hit.BladeTrail = hit.BladeTrail ?? new CharacterActionBladeTrailJsonDto();
                if (change.newValue)
                    CharacterActionBladeTrailAuthoringProjection.Enable(action, hit);
                hit.BladeTrail.Enabled = change.newValue;
            }, requestFullTracksRefresh: true));
            parent.Add(enabled);
            if (hit.EquipmentSlotId <= 0)
                parent.Add(new HelpBox("此命中窗口没有武器装备槽，无法生成武器拖尾。", HelpBoxMessageType.Info));
            if (hit.BladeTrail?.Enabled != true) return;
            AddTimelineIntegerField(parent, "拖尾提前帧", hit.BladeTrail.AdvanceFrames,
                value => hit.BladeTrail.AdvanceFrames = Math.Max(0, value), requestFullTracksRefresh: true);
            AddTimelineIntegerField(parent, "拖尾延后帧", hit.BladeTrail.DelayFrames,
                value => hit.BladeTrail.DelayFrames = Math.Max(0, value), requestFullTracksRefresh: true);
            parent.Add(new HelpBox("使用此装备槽武器的默认拖尾样式，自动生成关联 VFX。提前/延后只影响拖尾。", HelpBoxMessageType.Info));
        }

        private void AddHitWindowRangeField(
            VisualElement parent,
            CombatTrackEventJsonDto trackEvent)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ca-hit-range-row");
            Label label = new Label("有效帧");
            label.AddToClassList("ca-hit-range-label");
            row.Add(label);

            var startField = new IntegerField
            {
                name = "hit-window-start-frame",
                value = trackEvent.Frame,
                isDelayed = true,
            };
            startField.RegisterValueChangedCallback(change => ApplyTimelineFieldChange(() =>
            {
                int safeStart = Math.Max(0, change.newValue);
                trackEvent.Frame = safeStart;
                if (trackEvent.EndFrame < safeStart)
                    trackEvent.EndFrame = safeStart;
            }, requestFullTracksRefresh: true));
            row.Add(startField);
            row.Add(new Label("—"));

            var endField = new IntegerField
            {
                name = "hit-window-end-frame",
                value = Math.Max(trackEvent.Frame, trackEvent.EndFrame),
                isDelayed = true,
            };
            endField.RegisterValueChangedCallback(change => ApplyTimelineFieldChange(
                () => trackEvent.EndFrame = Math.Max(trackEvent.Frame, change.newValue),
                requestFullTracksRefresh: true));
            row.Add(endField);
            row.Add(new Label("帧"));
            parent.Add(row);
        }

        private void AddCombatEvent(
            VisualElement parent,
            CharacterActionJsonDto action,
            CombatTrackEventJsonDto[] events,
            int index,
            bool timelineInspector = false)
        {
            CombatTrackEventJsonDto evt = events[index] ?? new CombatTrackEventJsonDto();
            VisualElement row = AddCard(parent, "战斗事件 " + index);
            row.Add(new Label(CharacterActionAuthoringText.CombatSummary(evt)));
            CharacterActionTrackEventKind kind = ResolveCombatEventKind(evt);
            if (kind == CharacterActionTrackEventKind.HitTraceWindow)
                AddBladeTrailFields(row, action, evt);
            if (timelineInspector)
            {
                AddTimelineIntegerField(row, "触发帧", evt.Frame, value => evt.Frame = value);
                AddTimelineEnumPopup<CharacterActionTrackEventKind>(row, "事件类型", evt.Kind, value => evt.Kind = value, requestFullTracksRefresh: true);
                if (kind == CharacterActionTrackEventKind.StartCombatAction)
                    AddTimelineReferenceTextField(row, "战斗动作 ID", evt.CombatActionId, _model.ReferenceContext.CombatActionIds, value => evt.CombatActionId = value);
                else if (kind == CharacterActionTrackEventKind.SetHitTargetState)
                    AddHitTargetStateFields(row, evt, timeline: true);
                else if (IsHitTraceEvent(kind))
                {
                    bool usesHitVolume = kind == CharacterActionTrackEventKind.HitTraceWindow && HasHitVolume(evt);
                    if (!usesHitVolume)
                        AddTimelineEquipmentSlotField(row, "装备槽位", evt.EquipmentSlotId, action, value => evt.EquipmentSlotId = value);
                    else
                        row.Add(new HelpBox(HitVolumeHelpText(evt.HitVolume), HelpBoxMessageType.Info));
                    if (kind == CharacterActionTrackEventKind.HitTraceWindow)
                    {
                        AddHitVolumeFields(row, evt, timeline: true, requestFullTracksRefresh: true);
                        AddTimelineHitEventFields(row, evt);
                    }
                    AddLegacyTraceProfileHelp(row, evt, null);
                }
                AddTimelineTextField(row, "稳定事件 ID", evt.StableEventId, value => evt.StableEventId = value);
            }
            else
            {
                AddIntegerField(row, "触发帧", evt.Frame, value => evt.Frame = value);
                AddEnumPopup<CharacterActionTrackEventKind>(row, "事件类型", evt.Kind, value => evt.Kind = value);
                if (kind == CharacterActionTrackEventKind.StartCombatAction)
                    AddReferenceTextField(row, "战斗动作 ID", evt.CombatActionId, _model.ReferenceContext.CombatActionIds, value => evt.CombatActionId = value);
                else if (kind == CharacterActionTrackEventKind.SetHitTargetState)
                    AddHitTargetStateFields(row, evt, timeline: false);
                else if (IsHitTraceEvent(kind))
                {
                    bool usesHitVolume = kind == CharacterActionTrackEventKind.HitTraceWindow && HasHitVolume(evt);
                    if (!usesHitVolume)
                        AddEquipmentSlotField(row, "装备槽位", evt.EquipmentSlotId, action, value => evt.EquipmentSlotId = value);
                    else
                        row.Add(new HelpBox(HitVolumeHelpText(evt.HitVolume), HelpBoxMessageType.Info));
                    if (kind == CharacterActionTrackEventKind.HitTraceWindow)
                    {
                        AddHitVolumeFields(row, evt, timeline: false, requestFullTracksRefresh: false);
                        AddHitEventFields(row, evt);
                    }
                    AddLegacyTraceProfileHelp(row, evt, null);
                }
                AddTextField(row, "稳定事件 ID", evt.StableEventId, value => evt.StableEventId = value);
            }

            row.Add(new Button(() => RemoveCombatEvent(action, evt, index)) { text = "删除事件" });
            events[index] = evt;
        }

        private void AddCombatEvent(CharacterActionJsonDto action, CharacterActionTrackEventKind kind)
        {
            ApplyStructuralChange(() =>
            {
                action.CombatTrack = action.CombatTrack ?? new CombatTrackJsonDto();
                var events = new List<CombatTrackEventJsonDto>(action.CombatTrack.Events ?? Array.Empty<CombatTrackEventJsonDto>());
                int combatActionId = action.CombatTrack.CombatActionId;
                if (combatActionId <= 0)
                    combatActionId = CharacterActionAuthoringIds.Resolve("combat." + action.ActionId, 412);
                var evt = new CombatTrackEventJsonDto
                {
                    Frame = 0,
                    Kind = kind.ToString(),
                    CombatActionId = kind == CharacterActionTrackEventKind.StartCombatAction ? combatActionId : 0,
                    EquipmentSlotId = IsHitTraceEvent(kind) ? ResolveDefaultHitTraceEquipmentSlotId(action) : 0,
                    StableEventId = CharacterActionAuthoringIds.Resolve(
                        CharacterActionTimelineIdentity.MakeStableEventId(action, kind.ToString()), 401),
                };
                if (kind == CharacterActionTrackEventKind.HitTraceWindow)
                {
                    ApplyDefaultWeaponDamage(evt);
                    CharacterActionBladeTrailAuthoringProjection.Enable(action, evt);
                }
                evt.StableEventOrder = CharacterContentIdentityAuthoringCatalog.ReadEventOrders()[evt.StableEventId];
                events.Add(evt);
                action.CombatTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }

        private void AddHitTraceWindow(CharacterActionJsonDto action, int startFrame)
        {
            ApplyStructuralChange(() =>
            {
                action.CombatTrack = action.CombatTrack ?? new CombatTrackJsonDto();
                var events = new List<CombatTrackEventJsonDto>(action.CombatTrack.Events ?? Array.Empty<CombatTrackEventJsonDto>());
                int durationFrames = CharacterActionTimelineLayout.ResolveDurationFrames(action.DurationFrames, DefaultDurationFrames);
                int safeStart = Math.Max(0, Math.Min(startFrame, Math.Max(0, durationFrames - 1)));
                int safeEnd = Math.Max(safeStart, Math.Min(safeStart + 3, Math.Max(0, durationFrames - 1)));
                int equipmentSlotId = ResolveDefaultHitTraceEquipmentSlotId(action);
                var window = new CombatTrackEventJsonDto
                {
                    Frame = safeStart,
                    EndFrame = safeEnd,
                    Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                    EquipmentSlotId = equipmentSlotId,
                    StableEventId = CharacterActionAuthoringIds.Resolve(
                        CharacterActionTimelineIdentity.MakeStableEventId(action, "hit_window"), 401),
                };
                ApplyDefaultWeaponDamage(window);
                CharacterActionBladeTrailAuthoringProjection.Enable(action, window);
                window.StableEventOrder = CharacterContentIdentityAuthoringCatalog.ReadEventOrders()[window.StableEventId];
                events.Add(window);
                action.CombatTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }

        private void AddHitTargetStateWindow(CharacterActionJsonDto action, int startFrame)
        {
            ApplyStructuralChange(() =>
            {
                action.CombatTrack = action.CombatTrack ?? new CombatTrackJsonDto();
                var events = new List<CombatTrackEventJsonDto>(action.CombatTrack.Events ?? Array.Empty<CombatTrackEventJsonDto>());
                int durationFrames = CharacterActionTimelineLayout.ResolveDurationFrames(action.DurationFrames, DefaultDurationFrames);
                int safeStart = Math.Max(0, Math.Min(startFrame, Math.Max(0, durationFrames - 1)));
                int safeEnd = Math.Max(safeStart, Math.Min(safeStart + 3, Math.Max(0, durationFrames - 1)));
                var window = new CombatTrackEventJsonDto
                {
                    Frame = safeStart,
                    EndFrame = safeEnd,
                    Kind = CharacterActionTrackEventKind.SetHitTargetState.ToString(),
                    TargetStateMask = (int)HitTargetStateFlags.Unhittable,
                    StableEventId = CharacterActionAuthoringIds.Resolve(
                        CharacterActionTimelineIdentity.MakeStableEventId(action, "hit_target_state"), 401),
                };
                window.StableEventOrder = CharacterContentIdentityAuthoringCatalog.ReadEventOrders()[window.StableEventId];
                events.Add(window);
                action.CombatTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }

        private void AddHitTargetStateFields(
            VisualElement parent,
            CombatTrackEventJsonDto evt,
            bool timeline)
        {
            if (timeline)
            {
                AddTimelineIntegerField(parent, "结束帧", Math.Max(evt.Frame, evt.EndFrame), value =>
                    evt.EndFrame = Math.Max(evt.Frame, value), requestFullTracksRefresh: true);
            }
            else
            {
                AddIntegerField(parent, "结束帧", Math.Max(evt.Frame, evt.EndFrame), value =>
                    evt.EndFrame = Math.Max(evt.Frame, value));
            }

            AddHitTargetStateToggle(parent, evt, HitTargetStateFlags.Unhittable, "不可命中", timeline);
            AddHitTargetStateToggle(parent, evt, HitTargetStateFlags.Invincible, "无敌", timeline);
            AddHitTargetStateToggle(parent, evt, HitTargetStateFlags.Parrying, "招架", timeline);
            AddHitTargetStateToggle(parent, evt, HitTargetStateFlags.Blocking, "格挡", timeline);
            AddHitTargetStateToggle(parent, evt, HitTargetStateFlags.SuperArmor, "霸体", timeline);
        }

        private void AddHitTargetStateToggle(
            VisualElement parent,
            CombatTrackEventJsonDto evt,
            HitTargetStateFlags flag,
            string label,
            bool timeline)
        {
            var toggle = new Toggle(label) { value = (((HitTargetStateFlags)evt.TargetStateMask) & flag) != 0 };
            toggle.RegisterValueChangedCallback(change =>
            {
                Action apply = () =>
                {
                    HitTargetStateFlags flags = (HitTargetStateFlags)evt.TargetStateMask;
                    evt.TargetStateMask = (int)(change.newValue ? flags | flag : flags & ~flag);
                };
                if (timeline)
                    ApplyTimelineFieldChange(apply, requestFullTracksRefresh: true);
                else
                    ApplyFieldChange(apply);
            });
            parent.Add(toggle);
        }

        private static int ResolveDefaultHitTraceEquipmentSlotId(CharacterActionJsonDto action)
        {
            CombatTrackEventJsonDto[] events = action?.CombatTrack?.Events ?? Array.Empty<CombatTrackEventJsonDto>();
            for (int i = 0; i < events.Length; i++)
            {
                int slot = events[i]?.EquipmentSlotId ?? 0;
                if (slot > 0)
                    return slot;
            }

            return CharacterActionAuthoringIds.Resolve("mainHand", 405);
        }

        private void AddEquipmentSlotField(
            VisualElement parent,
            string label,
            string value,
            CharacterActionJsonDto action,
            Action<string> setter)
        {
            AddEquipmentSlotPopup(parent, label, value, action, selected => ApplyFieldChange(() => setter(selected)));
        }

        private void AddTimelineEquipmentSlotField(
            VisualElement parent,
            string label,
            string value,
            CharacterActionJsonDto action,
            Action<string> setter,
            bool requestFullTracksRefresh = false)
        {
            AddEquipmentSlotPopup(parent, label, value, action, selected =>
                ApplyTimelineFieldChange(() => setter(selected), requestFullTracksRefresh));
        }

        private void AddEquipmentSlotPopup(
            VisualElement parent,
            string label,
            string value,
            CharacterActionJsonDto action,
            Action<string> apply)
        {
            List<string> rawValues = BuildEquipmentSlotChoices(value, action);
            var labels = new List<string>(rawValues.Count);
            int selectedIndex = 0;
            value = value ?? string.Empty;
            for (int i = 0; i < rawValues.Count; i++)
            {
                labels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(FormatEquipmentSlotChoiceLabel(rawValues[i])));
                if (string.Equals(rawValues[i], value, StringComparison.Ordinal))
                    selectedIndex = i;
            }

            var field = new PopupField<string>(label, labels, selectedIndex)
            {
                tooltip = "Weapon Trace 模式使用装备槽解析武器 locator、半径和判定配置；Action Hit Volume 模式可留空。",
            };
            field.RegisterValueChangedCallback(evt =>
            {
                int index = labels.IndexOf(evt.newValue);
                if (index < 0 || index >= rawValues.Count)
                    return;
                apply(rawValues[index]);
            });
            parent.Add(field);

            if (string.IsNullOrWhiteSpace(value))
            {
                parent.Add(new HelpBox("未选择装备槽位时，只适用于 Action Hit Volume 等不依赖武器轨迹的命中窗口；Weapon Trace 需要选择主手或副手等槽位。", HelpBoxMessageType.Info));
            }
            else if (!IsKnownEquipmentSlotChoice(value))
            {
                parent.Add(new HelpBox("当前槽位不在已知装备槽位列表中，已保留旧值以避免丢配置；建议从角色装备配置中补齐槽位来源。", HelpBoxMessageType.Info));
            }
        }

        private List<string> BuildEquipmentSlotChoices(string currentValue, CharacterActionJsonDto action)
        {
            var choices = new List<string>();
            if (string.IsNullOrWhiteSpace(currentValue))
                AddEquipmentSlotChoice(choices, string.Empty);
            else
                AddEquipmentSlotChoice(choices, currentValue);
            AddEquipmentSlotChoice(choices, "mainHand");
            AddEquipmentSlotChoice(choices, "offHand");

            List<int> referenceSlots = _model?.ReferenceContext?.EquipmentSlotIds;
            if (referenceSlots != null)
            {
                for (int i = 0; i < referenceSlots.Count; i++)
                    AddEquipmentSlotChoice(choices, CharacterActionAuthoringIds.Label(referenceSlots[i], 405));
            }

            CombatTrackEventJsonDto[] events = action?.CombatTrack?.Events ?? Array.Empty<CombatTrackEventJsonDto>();
            for (int i = 0; i < events.Length; i++)
                AddEquipmentSlotChoice(choices, CharacterActionAuthoringIds.Label(events[i]?.EquipmentSlotId ?? 0, 405));

            return choices;
        }

        private static void AddEquipmentSlotChoice(List<string> choices, string value)
        {
            value = value ?? string.Empty;
            if (choices.Contains(value))
                return;
            if (string.IsNullOrWhiteSpace(value) && choices.Count > 0)
                return;
            choices.Add(value);
        }

        private bool IsKnownEquipmentSlotChoice(string value)
        {
            if (string.Equals(value, "mainHand", StringComparison.Ordinal)
                || string.Equals(value, "offHand", StringComparison.Ordinal))
            {
                return true;
            }

            List<int> referenceSlots = _model?.ReferenceContext?.EquipmentSlotIds;
            return referenceSlots != null && referenceSlots.Contains(CharacterActionAuthoringIds.Resolve(value, 405, allocate: false));
        }

        private static string FormatEquipmentSlotChoiceLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "未选择";
            if (string.Equals(value, "mainHand", StringComparison.Ordinal))
                return "主手 (mainHand)";
            if (string.Equals(value, "offHand", StringComparison.Ordinal))
                return "副手 (offHand)";
            return value;
        }

        private void AddDamageFields(VisualElement parent, CombatTrackEventJsonDto evt)
        {
            AddWeaponDamageFields(parent, evt, timeline: false, requestFullTracksRefresh: false);
        }

        private void AddHitEventFields(VisualElement parent, CombatTrackEventJsonDto evt)
        {
            AddSharedCombatHitEventFields(parent, evt, timeline: false, requestFullTracksRefresh: false);
        }

        private void AddHitVolumeFields(
            VisualElement parent,
            CombatTrackEventJsonDto evt,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            if (evt == null)
                return;

            VisualElement card = new VisualElement { name = "character-action-hit-volume" };
            card.AddToClassList("ca-hit-volume-section");
            bool enabled = IsEnabledHitVolume(evt.HitVolume);
            VisualElement toggleRow = new VisualElement();
            toggleRow.AddToClassList("ca-hit-volume-toggle-row");
            var toggle = new Toggle("自定义命中体")
            {
                value = enabled,
                tooltip = "开启后使用动作本地碰撞框作为判定源，并优先于装备槽/武器轨迹；关闭则回到 Weapon Trace 行为。",
            };
            toggle.RegisterValueChangedCallback(change =>
            {
                Action apply = () =>
                {
                    evt.HitVolume = change.newValue
                        ? CreateDefaultHitVolumeDto(evt.HitVolume)
                        : null;
                };
                if (timeline)
                    ApplyTimelineFieldChange(apply, requestFullTracksRefresh);
                else
                    ApplyFieldChange(apply);
            });
            toggleRow.Add(toggle);
            Label helper = new Label(enabled ? "使用动作本地碰撞体" : "关闭时使用武器轨迹");
            helper.AddToClassList("ca-hit-volume-helper");
            toggleRow.Add(helper);
            card.Add(toggleRow);
            parent.Add(card);

            if (!enabled)
                return;

            evt.HitVolume = CreateDefaultHitVolumeDto(evt.HitVolume);
            VisualElement details = new VisualElement();
            details.AddToClassList("ca-hit-volume-details");
            card.Add(details);
            AddHitVolumeSourceKindPopup(details, "来源", evt.HitVolume.SourceKind, value =>
            {
                evt.HitVolume.SourceKind = value;
                evt.HitVolume = CreateDefaultHitVolumeDto(evt.HitVolume);
            }, timeline, requestFullTracksRefresh: true);
            if (IsCasterBodyHitVolume(evt.HitVolume))
            {
                details.Add(new HelpBox(
                    "身体撞击判定 / Caster Body：运行时从施法者 CharacterTestCharacterPhysicsComponent 的 effective radius / height / centerY 派生命中体；offset/scale/add 只修饰命中查询，不改变真实移动碰撞体。",
                    HelpBoxMessageType.Info));
                AddHitVolumePointFields(details, "身体偏移", evt.HitVolume.BodyOffset, timeline, requestFullTracksRefresh);
                AddHitVolumeFloatField(details, "半径缩放", evt.HitVolume.BodyRadiusScale, value => evt.HitVolume.BodyRadiusScale = Math.Max(0.001f, value), timeline, requestFullTracksRefresh);
                AddHitVolumeFloatField(details, "半径增量", evt.HitVolume.BodyRadiusAdd, value => evt.HitVolume.BodyRadiusAdd = value, timeline, requestFullTracksRefresh);
                AddHitVolumeFloatField(details, "高度缩放", evt.HitVolume.BodyHeightScale, value => evt.HitVolume.BodyHeightScale = Math.Max(0.001f, value), timeline, requestFullTracksRefresh);
                AddHitVolumeFloatField(details, "高度增量", evt.HitVolume.BodyHeightAdd, value => evt.HitVolume.BodyHeightAdd = value, timeline, requestFullTracksRefresh);
                return;
            }

            if (IsBakedSocketCapsuleHitVolume(evt.HitVolume))
            {
                details.Add(new HelpBox(
                    "烘焙挂点 Capsule 使用当前角色包的确定性动画挂点轨道；挂点或帧缺失时失败关闭。",
                    HelpBoxMessageType.Info));
                List<string> socketIds = ResolveCurrentCharacterSocketIds();
                AddHitVolumeSocketField(details, "起点挂点", evt.HitVolume.StartSocketId, socketIds, value => evt.HitVolume.StartSocketId = value, timeline, requestFullTracksRefresh);
                AddHitVolumeSocketField(details, "终点挂点", evt.HitVolume.EndSocketId, socketIds, value => evt.HitVolume.EndSocketId = value, timeline, requestFullTracksRefresh);
                AddHitVolumeFloatField(details, "半径", evt.HitVolume.Radius, value => evt.HitVolume.Radius = Math.Max(0f, value), timeline, requestFullTracksRefresh);
                return;
            }

            AddHitVolumeEnumPopup(details, "形状", evt.HitVolume.ShapeKind, value => evt.HitVolume.ShapeKind = value, timeline, requestFullTracksRefresh);
            AddHitVolumePointFields(details, "端点 A", evt.HitVolume.LocalPointA, timeline, requestFullTracksRefresh);
            AddHitVolumePointFields(details, "端点 B", evt.HitVolume.LocalPointB, timeline, requestFullTracksRefresh);
            AddHitVolumeFloatField(details, "半径", evt.HitVolume.Radius, value => evt.HitVolume.Radius = Math.Max(0f, value), timeline, requestFullTracksRefresh);
        }

        private void AddHitVolumePointFields(
            VisualElement parent,
            string label,
            CharacterActionHitVolumePointJsonDto point,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            point = point ?? new CharacterActionHitVolumePointJsonDto();
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.Add(new Label(label));
            AddHitVolumeFloatField(row, "X", point.X, value => point.X = value, timeline, requestFullTracksRefresh);
            AddHitVolumeFloatField(row, "Y", point.Y, value => point.Y = value, timeline, requestFullTracksRefresh);
            AddHitVolumeFloatField(row, "Z", point.Z, value => point.Z = value, timeline, requestFullTracksRefresh);
            parent.Add(row);
        }

        private void AddHitVolumeFloatField(
            VisualElement parent,
            string label,
            float value,
            Action<float> setter,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            if (timeline)
                AddTimelineFloatField(parent, label, value, setter, requestFullTracksRefresh);
            else
                AddFloatField(parent, label, value, setter);
        }

        private void AddHitVolumeTextField(
            VisualElement parent,
            string label,
            string value,
            Action<string> setter,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            if (timeline)
                AddTimelineTextField(parent, label, value ?? string.Empty, setter, requestFullTracksRefresh);
            else
                AddTextField(parent, label, value ?? string.Empty, setter);
        }

        private void AddHitVolumeSocketField(
            VisualElement parent,
            string label,
            string value,
            List<string> socketIds,
            Action<string> setter,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            if (timeline)
                AddTimelineReferenceTextField(parent, label, value ?? string.Empty, socketIds, setter, requestFullTracksRefresh);
            else
                AddReferenceTextField(parent, label, value ?? string.Empty, socketIds, setter);
        }

        private List<string> ResolveCurrentCharacterSocketIds()
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(_actionSetSourceDirectory))
                return result;

            string directory = AbsoluteProjectPath(_actionSetSourceDirectory);
            var current = new DirectoryInfo(directory);
            for (int depth = 0; current != null && depth < 8; depth++, current = current.Parent)
            {
                string socketsPath = Path.Combine(current.FullName, "geometry", "sockets.json");
                if (!File.Exists(socketsPath))
                    continue;

                try
                {
                    JArray sockets = JObject.Parse(File.ReadAllText(socketsPath))["sockets"] as JArray;
                    for (int i = 0; sockets != null && i < sockets.Count; i++)
                    {
                        string socketId = ((string)sockets[i]?["socketId"] ?? (string)sockets[i]?["id"] ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(socketId) && !result.Contains(socketId))
                            result.Add(socketId);
                    }
                    result.Sort(StringComparer.Ordinal);
                }
                catch (Exception)
                {
                    result.Clear();
                }
                return result;
            }

            return result;
        }

        private void AddHitVolumeEnumPopup(
            VisualElement parent,
            string label,
            string value,
            Action<string> setter,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            if (timeline)
                AddTimelineEnumPopup<CharacterActionHitVolumeShapeKind>(parent, label, value, setter, requestFullTracksRefresh);
            else
                AddEnumPopup<CharacterActionHitVolumeShapeKind>(parent, label, value, setter);
        }

        private void AddHitVolumeSourceKindPopup(
            VisualElement parent,
            string label,
            string value,
            Action<string> setter,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            if (timeline)
                AddTimelineEnumPopup<CharacterActionHitVolumeSourceKind>(parent, label, value, setter, requestFullTracksRefresh);
            else
                AddEnumPopup<CharacterActionHitVolumeSourceKind>(parent, label, value, setter);
        }

        private static CharacterActionHitVolumeJsonDto CreateDefaultHitVolumeDto(CharacterActionHitVolumeJsonDto current)
        {
            current = current ?? new CharacterActionHitVolumeJsonDto();
            current.SourceKind = string.IsNullOrWhiteSpace(current.SourceKind)
                ? CharacterActionHitVolumeSourceKind.ManualCapsule.ToString()
                : current.SourceKind;
            if (IsCasterBodyHitVolume(current))
            {
                current.ShapeKind = string.Empty;
                current.LocalPointA = null;
                current.LocalPointB = null;
                current.Radius = 0f;
                current.BodyOffset = current.BodyOffset ?? new CharacterActionHitVolumePointJsonDto();
                current.BodyRadiusScale = current.BodyRadiusScale <= 0f ? 1f : current.BodyRadiusScale;
                current.BodyHeightScale = current.BodyHeightScale <= 0f ? 1f : current.BodyHeightScale;
                return current;
            }


            if (IsBakedSocketCapsuleHitVolume(current))
            {
                current.ShapeKind = string.Empty;
                current.LocalPointA = null;
                current.LocalPointB = null;
                current.BodyOffset = null;
                current.BodyRadiusScale = 1f;
                current.BodyRadiusAdd = 0f;
                current.BodyHeightScale = 1f;
                current.BodyHeightAdd = 0f;
                current.StartSocketId = current.StartSocketId;
                current.EndSocketId = current.EndSocketId;
                if (current.Radius <= 0f)
                    current.Radius = 0.35f;
                return current;
            }

            current.BodyOffset = null;
            current.BodyRadiusScale = 1f;
            current.BodyRadiusAdd = 0f;
            current.BodyHeightScale = 1f;
            current.BodyHeightAdd = 0f;
            current.StartSocketId = 0;
            current.EndSocketId = 0;
            current.ShapeKind = string.IsNullOrWhiteSpace(current.ShapeKind)
                ? CharacterActionHitVolumeShapeKind.Capsule.ToString()
                : current.ShapeKind;
            current.LocalPointA = current.LocalPointA ?? new CharacterActionHitVolumePointJsonDto
            {
                X = 0f,
                Y = 0.8f,
                Z = 0.6f,
            };
            current.LocalPointB = current.LocalPointB ?? new CharacterActionHitVolumePointJsonDto
            {
                X = 0f,
                Y = 0.8f,
                Z = 1.8f,
            };
            if (current.Radius <= 0f)
                current.Radius = 0.65f;
            return current;
        }

        private void AddTimelineHitEventFields(
            VisualElement parent,
            CombatTrackEventJsonDto evt,
            bool requestFullTracksRefresh = false)
        {
            AddSharedCombatHitEventFields(parent, evt, timeline: true, requestFullTracksRefresh: requestFullTracksRefresh);
        }

        private void AddHitEventFields(
            VisualElement parent,
            CombatTrackEventJsonDto evt,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            if (evt == null)
                return;

            evt.HitEvent = evt.HitEvent ?? CreateDefaultHitEventDto(evt);
            VisualElement card = AddCard(parent, "命中目标");
            AddHitTargetToggle(card, "敌方角色", evt, CombatHitTargetTypes.HostileCharacter, timeline, requestFullTracksRefresh);
            AddHitTargetToggle(card, "友方角色", evt, CombatHitTargetTypes.FriendlyCharacter, timeline, requestFullTracksRefresh);
            AddHitTargetToggle(card, "自身", evt, CombatHitTargetTypes.Self, timeline, requestFullTracksRefresh);
            AddHitTargetToggle(card, "第三方角色", evt, CombatHitTargetTypes.ThirdPartyCharacter, timeline, requestFullTracksRefresh);
            AddHitTargetToggle(card, "可破坏对象", evt, CombatHitTargetTypes.DamageableObject, timeline, requestFullTracksRefresh);
            AddHitTargetToggle(card, "墙面", evt, CombatHitTargetTypes.WorldWall, timeline, requestFullTracksRefresh);
            AddHitTargetToggle(card, "地面", evt, CombatHitTargetTypes.WorldGround, timeline, requestFullTracksRefresh);
            AddHitTargetToggle(card, "静态场景", evt, CombatHitTargetTypes.WorldStatic, timeline, requestFullTracksRefresh);
            AddHitEventEnumPopup<CombatEnvironmentHitPolicy>(
                card,
                "环境命中",
                evt.HitEvent.EnvironmentHitPolicy,
                value => evt.HitEvent.EnvironmentHitPolicy = value,
                timeline,
                requestFullTracksRefresh);
            AddHitEventIntegerField(card, "最大有效命中数", evt.HitEvent.MaxAcceptedHits <= 0 ? 1 : evt.HitEvent.MaxAcceptedHits, value =>
            {
                evt.HitEvent.MaxAcceptedHits = Math.Max(1, value);
            }, timeline, requestFullTracksRefresh);
            AddHitEventEnumPopup<CombatHitSameTargetPolicy>(
                card,
                "同目标命中策略",
                evt.HitEvent.HitSameTargetPolicy,
                value => evt.HitEvent.HitSameTargetPolicy = value,
                timeline,
                requestFullTracksRefresh);

            if (HasAnyWorldTarget(evt.HitEvent)
                && !string.Equals(evt.HitEvent.EnvironmentHitPolicy, CombatEnvironmentHitPolicy.BlockedPresentation.ToString(), StringComparison.Ordinal))
            {
                card.Add(new HelpBox("已选择墙面/地面/静态场景，但环境命中策略不是“阻挡表现”。运行时会查询到场景体但不会产生命中表现。", HelpBoxMessageType.Info));
            }
        }

        private void AddHitTargetToggle(
            VisualElement parent,
            string label,
            CombatTrackEventJsonDto evt,
            CombatHitTargetTypes targetType,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            var toggle = new Toggle(label)
            {
                value = HasHitTarget(evt.HitEvent, targetType),
            };
            toggle.RegisterValueChangedCallback(change =>
                ApplyHitEventChange(() => SetHitTarget(evt.HitEvent, targetType, change.newValue), timeline, requestFullTracksRefresh));
            parent.Add(toggle);
        }

        private void AddHitEventEnumPopup<TEnum>(
            VisualElement parent,
            string label,
            string value,
            Action<string> setter,
            bool timeline,
            bool requestFullTracksRefresh)
            where TEnum : struct
        {
            string[] rawValues = Enum.GetNames(typeof(TEnum));
            var labels = new List<string>(rawValues.Length);
            int selectedIndex = 0;
            for (int i = 0; i < rawValues.Length; i++)
            {
                labels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(FormatHitEventEnumLabel(rawValues[i])));
                if (string.Equals(rawValues[i], value, StringComparison.Ordinal))
                    selectedIndex = i;
            }

            var field = new PopupField<string>(label, labels, selectedIndex);
            field.RegisterValueChangedCallback(change =>
            {
                int index = labels.IndexOf(change.newValue);
                if (index < 0 || index >= rawValues.Length)
                    return;
                ApplyHitEventChange(() => setter(rawValues[index]), timeline, requestFullTracksRefresh);
            });
            parent.Add(field);
        }

        private void AddHitEventTextField(
            VisualElement parent,
            string label,
            string value,
            Action<string> setter,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            var field = new TextField(label) { value = value ?? string.Empty, isDelayed = true };
            field.RegisterValueChangedCallback(change =>
                ApplyHitEventChange(() => setter(change.newValue ?? string.Empty), timeline, requestFullTracksRefresh));
            parent.Add(field);
        }

        private void AddHitEventIntegerField(
            VisualElement parent,
            string label,
            int value,
            Action<int> setter,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            var field = new IntegerField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(change =>
                ApplyHitEventChange(() => setter(change.newValue), timeline, requestFullTracksRefresh));
            parent.Add(field);
        }

        private void ApplyHitEventChange(Action mutate, bool timeline, bool requestFullTracksRefresh)
        {
            if (timeline)
                ApplyTimelineFieldChange(mutate, requestFullTracksRefresh);
            else
                ApplyFieldChange(mutate);
        }

        private static CharacterActionHitEventJsonDto CreateDefaultHitEventDto(CombatTrackEventJsonDto evt)
        {
            return new CharacterActionHitEventJsonDto
            {
                TargetTypes = new[] { CombatHitTargetTypes.HostileCharacter.ToString() },
                EnvironmentHitPolicy = CombatEnvironmentHitPolicy.None.ToString(),
                StableEventId = evt?.StableEventId ?? 0,
                MaxAcceptedHits = 1,
                HitSameTargetPolicy = CombatHitSameTargetPolicy.AllowRepeat.ToString(),
            };
        }

        private static CharacterActionHitEventJsonDto CreateDefaultHitEventDto(GameplayTrackEventJsonDto evt)
        {
            return new CharacterActionHitEventJsonDto
            {
                TargetTypes = new[] { CombatHitTargetTypes.HostileCharacter.ToString() },
                EnvironmentHitPolicy = CombatEnvironmentHitPolicy.None.ToString(),
                StableEventId = evt?.StableEventId ?? 0,
                MaxAcceptedHits = 1,
                HitSameTargetPolicy = CombatHitSameTargetPolicy.AllowRepeat.ToString(),
            };
        }

        private static CharacterActionHitDamageJsonDto EnsureDamage(CombatTrackEventJsonDto evt)
        {
            evt.HitEvent = evt.HitEvent ?? CreateDefaultHitEventDto(evt);
            evt.HitEvent.Damage = evt.HitEvent.Damage ?? new CharacterActionHitDamageJsonDto();
            return evt.HitEvent.Damage;
        }

        private static CharacterActionHitDamageJsonDto EnsureDamage(GameplayTrackEventJsonDto evt)
        {
            evt.HitEvent = evt.HitEvent ?? CreateDefaultHitEventDto(evt);
            evt.HitEvent.Damage = evt.HitEvent.Damage ?? new CharacterActionHitDamageJsonDto();
            return evt.HitEvent.Damage;
        }

        private static bool HasHitTarget(CharacterActionHitEventJsonDto hitEvent, CombatHitTargetTypes targetType)
        {
            return (ReadHitTargetTypes(hitEvent) & targetType) != 0;
        }

        private static void SetHitTarget(CharacterActionHitEventJsonDto hitEvent, CombatHitTargetTypes targetType, bool enabled)
        {
            CombatHitTargetTypes value = ReadHitTargetTypes(hitEvent);
            value = enabled ? value | targetType : value & ~targetType;
            if (value == CombatHitTargetTypes.None)
                value = CombatHitTargetTypes.HostileCharacter;
            hitEvent.TargetTypes = ToTargetTypeNames(value);
        }

        private static CombatHitTargetTypes ReadHitTargetTypes(CharacterActionHitEventJsonDto hitEvent)
        {
            string[] values = hitEvent?.TargetTypes ?? Array.Empty<string>();
            CombatHitTargetTypes result = CombatHitTargetTypes.None;
            for (int i = 0; i < values.Length; i++)
            {
                if (Enum.TryParse(values[i], false, out CombatHitTargetTypes parsed)
                    && Enum.IsDefined(typeof(CombatHitTargetTypes), parsed))
                {
                    result |= parsed;
                }
            }

            return result == CombatHitTargetTypes.None ? CombatHitTargetTypes.HostileCharacter : result;
        }

        private static string[] ToTargetTypeNames(CombatHitTargetTypes targetTypes)
        {
            var values = new List<string>();
            CombatHitTargetTypes[] candidates =
            {
                CombatHitTargetTypes.HostileCharacter,
                CombatHitTargetTypes.FriendlyCharacter,
                CombatHitTargetTypes.Self,
                CombatHitTargetTypes.ThirdPartyCharacter,
                CombatHitTargetTypes.DamageableObject,
                CombatHitTargetTypes.WorldWall,
                CombatHitTargetTypes.WorldGround,
                CombatHitTargetTypes.WorldStatic,
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                CombatHitTargetTypes candidate = candidates[i];
                if ((targetTypes & candidate) != 0)
                    values.Add(candidate.ToString());
            }

            return values.ToArray();
        }

        private static bool HasAnyWorldTarget(CharacterActionHitEventJsonDto hitEvent)
        {
            return CombatHitEventConfig.HasAnyWorldTarget(ReadHitTargetTypes(hitEvent));
        }

        private static string FormatHitEventEnumLabel(string value)
        {
            switch (value)
            {
                case nameof(CombatEnvironmentHitPolicy.None):
                    return "不触发";
                case nameof(CombatEnvironmentHitPolicy.BlockedPresentation):
                    return "阻挡表现";
                case nameof(CombatHitSameTargetPolicy.AllowRepeat):
                    return "允许重复";
                case nameof(CombatHitSameTargetPolicy.OncePerEvent):
                    return "同事件只命中一次";
                default:
                    return value;
            }
        }

        private void AddTimelineDamageFields(
            VisualElement parent,
            CombatTrackEventJsonDto evt,
            bool requestFullTracksRefresh = false)
        {
            AddWeaponDamageFields(parent, evt, timeline: true, requestFullTracksRefresh: requestFullTracksRefresh);
        }

        private void AddDamageFields(VisualElement parent, GameplayTrackEventJsonDto evt)
        {
            if (evt == null)
                return;

            CharacterActionHitDamageJsonDto damage = EnsureDamage(evt);
            AddDamageInlineFields(
                parent,
                CharacterActionAuthoringIds.Label(damage.DamageTypeId, 409, damage.DamageTypeLabel),
                CharacterActionAuthoringIds.Label(damage.FormulaId, 410, damage.FormulaLabel),
                damage.BaseDamage,
                ResolveAttackScale(damage.SourceAttributeScales),
                Math.Max(0, damage.ImpactPower),
                Math.Max(0, damage.LaunchPower),
                CharacterActionAuthoringIds.Label(damage.AnalysisCategory, 411, damage.AnalysisCategoryLabel),
                CharacterActionAuthoringIds.Labels(damage.Tags, 410, damage.TagLabels),
                (damageTypeId, formulaId, baseDamage, attackScale, impactPower, launchPower, analysisCategory, tags) =>
                {
                    CharacterActionHitDamageJsonDto target = EnsureDamage(evt);
                    target.DamageTypeId = CharacterActionAuthoringIds.Resolve(damageTypeId, 409);
                    target.FormulaId = CharacterActionAuthoringIds.Resolve(formulaId, 410);
                    target.DamageTypeLabel = damageTypeId ?? string.Empty;
                    target.FormulaLabel = formulaId ?? string.Empty;
                    target.BaseDamage = baseDamage;
                    SetDamageComparisonAttackScale(target, attackScale);
                    target.ImpactPower = impactPower;
                    target.LaunchPower = launchPower;
                    target.AnalysisCategory = CharacterActionAuthoringIds.Resolve(analysisCategory, 411);
                    target.AnalysisCategoryLabel = analysisCategory ?? string.Empty;
                    target.Tags = CharacterActionAuthoringIds.ResolveMany(tags, 410);
                    target.TagLabels = tags ?? Array.Empty<string>();
                },
                timeline: false);
        }

        private void AddWeaponDamageFields(
            VisualElement parent,
            CombatTrackEventJsonDto evt,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            if (evt == null)
                return;

            CharacterActionHitDamageJsonDto damage = EnsureDamage(evt);
            string damageTypeId = CharacterActionAuthoringIds.Label(
                damage.DamageTypeId, 409,
                string.IsNullOrWhiteSpace(damage.DamageTypeLabel)
                    ? CharacterActionDamageAuthoringText.DefaultDamageTypeId
                    : damage.DamageTypeLabel);
            string formulaId = CharacterActionAuthoringIds.Label(damage.FormulaId, 410, damage.FormulaLabel);
            int baseDamage = Math.Max(0, damage.BaseDamage);
            float attackScale = ResolveAttackScale(damage.SourceAttributeScales);
            int impactPower = Math.Max(0, damage.ImpactPower);
            int launchPower = Math.Max(0, damage.LaunchPower);
            string analysisCategory = CharacterActionAuthoringIds.Label(
                damage.AnalysisCategory, 411,
                string.IsNullOrWhiteSpace(damage.AnalysisCategoryLabel) ? "BasicAttack" : damage.AnalysisCategoryLabel);
            string[] tags = CharacterActionAuthoringIds.Labels(damage.Tags, 410, damage.TagLabels);

            VisualElement card = AddCard(parent, "命中数值 / 战斗调参");
            card.Add(new HelpBox(CharacterActionDamageAuthoringText.DamageTuningSemanticsHelpText, HelpBoxMessageType.Info));
            card.Add(new HelpBox(CharacterActionDamageAuthoringText.DamageTuningBaselineHelpText, HelpBoxMessageType.Info));
            AddDamageIntegerField(card, CharacterActionDamageAuthoringText.BaseDamageFieldLabel, baseDamage, value =>
                ApplyWeaponDamageChange(evt, damageTypeId, formulaId, Math.Max(0, value), attackScale, impactPower, launchPower, analysisCategory, tags, timeline, requestFullTracksRefresh), timeline, requestFullTracksRefresh);
            AddDamageFloatField(card, CharacterActionDamageAuthoringText.AttackScaleFieldLabel, attackScale, value =>
                ApplyWeaponDamageChange(evt, damageTypeId, formulaId, baseDamage, Math.Max(0f, value), impactPower, launchPower, analysisCategory, tags, timeline, requestFullTracksRefresh), timeline, requestFullTracksRefresh);
            AddDamageIntegerField(card, CharacterActionDamageAuthoringText.ImpactPowerFieldLabel, impactPower, value =>
                ApplyWeaponDamageChange(evt, damageTypeId, formulaId, baseDamage, attackScale, Math.Max(0, value), launchPower, analysisCategory, tags, timeline, requestFullTracksRefresh), timeline, requestFullTracksRefresh);
            AddDamageIntegerField(card, CharacterActionDamageAuthoringText.LaunchPowerFieldLabel, launchPower, value =>
                ApplyWeaponDamageChange(evt, damageTypeId, formulaId, baseDamage, attackScale, impactPower, Math.Max(0, value), analysisCategory, tags, timeline, requestFullTracksRefresh), timeline, requestFullTracksRefresh);
            AddDamageChoiceField(card, "伤害性质", damageTypeId, BuildDamageTypeChoices(damageTypeId), value =>
                ApplyWeaponDamageChange(evt, value, formulaId, baseDamage, attackScale, impactPower, launchPower, analysisCategory, tags, timeline, requestFullTracksRefresh));
            AddDamageChoiceField(card, "分析分类", analysisCategory, BuildAnalysisCategoryChoices(analysisCategory), value =>
                ApplyWeaponDamageChange(evt, damageTypeId, formulaId, baseDamage, attackScale, impactPower, launchPower, value, tags, timeline, requestFullTracksRefresh));
            AddDamageTagToggles(card, tags, value =>
                ApplyWeaponDamageChange(evt, damageTypeId, formulaId, baseDamage, attackScale, impactPower, launchPower, analysisCategory, value, timeline, requestFullTracksRefresh));
        }

        private void ApplyWeaponDamageChange(
            CombatTrackEventJsonDto evt,
            string damageTypeId,
            string formulaId,
            int baseDamage,
            float attackScale,
            int impactPower,
            int launchPower,
            string analysisCategory,
            string[] tags,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            Action change = () =>
            {
                CharacterActionHitDamageJsonDto damage = EnsureDamage(evt);
                string normalizedFormula = string.IsNullOrWhiteSpace(formulaId) && attackScale > 0.0001f
                    ? CharacterActionDamageAuthoringText.DefaultDamageFormulaId
                    : formulaId ?? string.Empty;
                damage.DamageTypeId = CharacterActionAuthoringIds.Resolve(damageTypeId, 409);
                damage.FormulaId = CharacterActionAuthoringIds.Resolve(normalizedFormula, 410);
                damage.DamageTypeLabel = damageTypeId ?? string.Empty;
                damage.FormulaLabel = normalizedFormula;
                damage.BaseDamage = Math.Max(0, baseDamage);
                SetDamageComparisonAttackScale(damage, attackScale);
                damage.ImpactPower = Math.Max(0, impactPower);
                damage.LaunchPower = Math.Max(0, launchPower);
                damage.AnalysisCategory = CharacterActionAuthoringIds.Resolve(analysisCategory, 411);
                damage.AnalysisCategoryLabel = analysisCategory ?? string.Empty;
                damage.Tags = CharacterActionAuthoringIds.ResolveMany(tags, 410);
                damage.TagLabels = tags ?? Array.Empty<string>();
            };
            if (timeline)
                ApplyTimelineFieldChange(change, requestFullTracksRefresh);
            else
                ApplyFieldChange(change);
        }

        private void AddDamageInlineFields(
            VisualElement parent,
            string damageTypeId,
            string formulaId,
            int baseDamage,
            float attackScale,
            int impactPower,
            int launchPower,
            string analysisCategory,
            string[] tags,
            Action<string, string, int, float, int, int, string, string[]> setter,
            bool timeline,
            bool requestFullTracksRefresh = false)
        {
            damageTypeId = string.IsNullOrWhiteSpace(damageTypeId)
                ? CharacterActionDamageAuthoringText.DefaultDamageTypeId
                : damageTypeId;
            formulaId = formulaId ?? string.Empty;
            analysisCategory = string.IsNullOrWhiteSpace(analysisCategory) ? "BasicAttack" : analysisCategory;
            tags = tags ?? Array.Empty<string>();

            VisualElement card = AddCard(parent, "命中数值 / 战斗调参");
            card.Add(new HelpBox(CharacterActionDamageAuthoringText.DamageTuningSemanticsHelpText, HelpBoxMessageType.Info));
            card.Add(new HelpBox(CharacterActionDamageAuthoringText.DamageTuningBaselineHelpText, HelpBoxMessageType.Info));
            AddDamageIntegerField(card, CharacterActionDamageAuthoringText.BaseDamageFieldLabel, baseDamage, value =>
                ApplyDamageInlineChange(setter, damageTypeId, formulaId, Math.Max(0, value), attackScale, impactPower, launchPower, analysisCategory, tags, timeline, requestFullTracksRefresh), timeline, requestFullTracksRefresh);
            AddDamageFloatField(card, CharacterActionDamageAuthoringText.AttackScaleFieldLabel, attackScale, value =>
                ApplyDamageInlineChange(setter, damageTypeId, formulaId, baseDamage, Math.Max(0f, value), impactPower, launchPower, analysisCategory, tags, timeline, requestFullTracksRefresh), timeline, requestFullTracksRefresh);
            AddDamageIntegerField(card, CharacterActionDamageAuthoringText.ImpactPowerFieldLabel, impactPower, value =>
                ApplyDamageInlineChange(setter, damageTypeId, formulaId, baseDamage, attackScale, Math.Max(0, value), launchPower, analysisCategory, tags, timeline, requestFullTracksRefresh), timeline, requestFullTracksRefresh);
            AddDamageIntegerField(card, CharacterActionDamageAuthoringText.LaunchPowerFieldLabel, launchPower, value =>
                ApplyDamageInlineChange(setter, damageTypeId, formulaId, baseDamage, attackScale, impactPower, Math.Max(0, value), analysisCategory, tags, timeline, requestFullTracksRefresh), timeline, requestFullTracksRefresh);
            AddDamageChoiceField(card, "伤害性质", damageTypeId, BuildDamageTypeChoices(damageTypeId), value =>
                ApplyDamageInlineChange(setter, value, formulaId, baseDamage, attackScale, impactPower, launchPower, analysisCategory, tags, timeline, requestFullTracksRefresh));
            AddDamageChoiceField(card, "分析分类", analysisCategory, BuildAnalysisCategoryChoices(analysisCategory), value =>
                ApplyDamageInlineChange(setter, damageTypeId, formulaId, baseDamage, attackScale, impactPower, launchPower, value, tags, timeline, requestFullTracksRefresh));
            AddDamageTagToggles(card, tags, value =>
                ApplyDamageInlineChange(setter, damageTypeId, formulaId, baseDamage, attackScale, impactPower, launchPower, analysisCategory, value, timeline, requestFullTracksRefresh));
        }

        private void AddDamageChoiceField(
            VisualElement parent,
            string label,
            string value,
            List<(string Value, string Label)> choices,
            Action<string> apply)
        {
            var labels = new List<string>(choices.Count);
            int selectedIndex = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                labels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(choices[i].Label));
                if (string.Equals(choices[i].Value, value ?? string.Empty, StringComparison.Ordinal))
                    selectedIndex = i;
            }

            var field = new PopupField<string>(label, labels, selectedIndex);
            field.RegisterValueChangedCallback(evt =>
            {
                int index = labels.IndexOf(evt.newValue);
                if (index < 0 || index >= choices.Count)
                    return;
                apply(choices[index].Value);
            });
            parent.Add(field);
        }

        private void AddDamageIntegerField(
            VisualElement parent,
            string label,
            int value,
            Action<int> setter,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            if (timeline)
                AddTimelineIntegerField(parent, label, value, setter, requestFullTracksRefresh);
            else
                AddIntegerField(parent, label, value, setter);
        }

        private void AddDamageFloatField(
            VisualElement parent,
            string label,
            float value,
            Action<float> setter,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            if (timeline)
                AddTimelineFloatField(parent, label, value, setter, requestFullTracksRefresh);
            else
                AddFloatField(parent, label, value, setter);
        }

        private void AddDamageTagToggles(VisualElement parent, string[] tags, Action<string[]> apply)
        {
            VisualElement row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            row.Add(new Label("标签"));
            string[] choices = { "melee", "weapon", "light", "heavy", "area", "shockwave", "scatter", "projectile", "control" };
            for (int i = 0; i < choices.Length; i++)
            {
                string tag = choices[i];
                var toggle = new Toggle(FormatDamageTagLabel(tag))
                {
                    value = ContainsTag(tags, tag),
                };
                toggle.style.marginLeft = 6f;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    string[] nextTags = evt.newValue
                        ? AddTag(tags, tag)
                        : RemoveTag(tags, tag);
                    apply(nextTags);
                });
                row.Add(toggle);
            }

            parent.Add(row);
        }

        private void ApplyDamageInlineChange(
            Action<string, string, int, float, int, int, string, string[]> setter,
            string damageTypeId,
            string formulaId,
            int baseDamage,
            float attackScale,
            int impactPower,
            int launchPower,
            string analysisCategory,
            string[] tags,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            Action change = () => setter(
                damageTypeId ?? string.Empty,
                string.IsNullOrWhiteSpace(formulaId) && attackScale > 0.0001f
                    ? CharacterActionDamageAuthoringText.DefaultDamageFormulaId
                    : formulaId ?? string.Empty,
                Math.Max(0, baseDamage),
                Math.Max(0f, attackScale),
                Math.Max(0, impactPower),
                Math.Max(0, launchPower),
                analysisCategory ?? string.Empty,
                tags ?? Array.Empty<string>());
            if (timeline)
                ApplyTimelineFieldChange(change, requestFullTracksRefresh);
            else
                ApplyFieldChange(change);
        }

        private static List<(string Value, string Label)> BuildDamageTypeChoices(string currentValue)
        {
            return CharacterActionDamageAuthoringText.BuildDamageTypeChoices(currentValue);
        }

        private static List<(string Value, string Label)> BuildAnalysisCategoryChoices(string currentValue)
        {
            return CharacterActionDamageAuthoringText.BuildAnalysisCategoryChoices(currentValue);
        }

        private static float ResolveAttackScale(CharacterActionDamageAttributeScaleJsonDto[] scales)
        {
            return CharacterActionHitEventAuthoringModel.ResolveAttackScale(scales);
        }

        private static CharacterActionDamageAttributeScaleJsonDto[] BuildAttackScaleArray(float attackScale)
        {
            return CharacterActionHitEventAuthoringModel.BuildAttackScaleArray(attackScale);
        }

        private static void ApplyDefaultWeaponDamage(CombatTrackEventJsonDto evt)
        {
            if (evt == null)
                return;

            evt.HitEvent = evt.HitEvent ?? CreateDefaultHitEventDto(evt);
            evt.HitEvent.StableEventId = evt.HitEvent.StableEventId <= 0
                ? evt.StableEventId
                : evt.HitEvent.StableEventId;
            CharacterActionHitDamageJsonDto damage = EnsureDamage(evt);
            damage.DamageTypeId = CharacterActionAuthoringIds.Resolve(CharacterActionDamageAuthoringText.DefaultDamageTypeId, 409);
            damage.FormulaId = CharacterActionAuthoringIds.Resolve(CharacterActionDamageAuthoringText.DefaultDamageFormulaId, 410);
            damage.DamageTypeLabel = CharacterActionDamageAuthoringText.DefaultDamageTypeId;
            damage.FormulaLabel = CharacterActionDamageAuthoringText.DefaultDamageFormulaId;
            damage.BaseDamage = CharacterActionDamageAuthoringText.BaselineLightAttackBaseDamage;
            damage.SourceAttributeScales = BuildAttackScaleArray(CharacterActionDamageAuthoringText.BaselineLightAttackAttackScale);
            damage.ImpactPower = CharacterActionDamageAuthoringText.BaselineLightAttackImpactPower;
            damage.LaunchPower = 0;
            damage.AnalysisCategory = CharacterActionAuthoringIds.Resolve("BasicAttack", 411);
            damage.AnalysisCategoryLabel = "BasicAttack";
            damage.Tags = CharacterActionAuthoringIds.ResolveMany(new[] { "melee", "weapon", "light", "scatter" }, 410);
            damage.TagLabels = new[] { "melee", "weapon", "light", "scatter" };
        }

        private static void ApplyDefaultAreaDamage(GameplayTrackEventJsonDto evt)
        {
            if (evt == null)
                return;

            CharacterActionHitDamageJsonDto damage = EnsureDamage(evt);
            damage.DamageTypeId = CharacterActionAuthoringIds.Resolve("force", 409);
            damage.DamageTypeLabel = "force";
            damage.BaseDamage = 6;
            damage.ImpactPower = 12;
            damage.LaunchPower = 0;
            damage.AnalysisCategory = CharacterActionAuthoringIds.Resolve("AreaPulse", 411);
            damage.AnalysisCategoryLabel = "AreaPulse";
            damage.Tags = CharacterActionAuthoringIds.ResolveMany(new[] { "area", "shockwave", "scatter" }, 410);
            damage.TagLabels = new[] { "area", "shockwave", "scatter" };
        }

        private static string FormatDamageTagLabel(string tag)
        {
            return CharacterActionDamageAuthoringText.FormatDamageTagLabel(tag);
        }

        private static bool ContainsTag(string[] tags, string tag)
        {
            tags = tags ?? Array.Empty<string>();
            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string[] AddTag(string[] tags, string tag)
        {
            if (ContainsTag(tags, tag))
                return tags ?? Array.Empty<string>();
            var values = new List<string>(tags ?? Array.Empty<string>()) { tag };
            return values.ToArray();
        }

        private static string[] RemoveTag(string[] tags, string tag)
        {
            tags = tags ?? Array.Empty<string>();
            var values = new List<string>(tags.Length);
            for (int i = 0; i < tags.Length; i++)
            {
                if (!string.Equals(tags[i], tag, StringComparison.Ordinal))
                    values.Add(tags[i]);
            }

            return values.ToArray();
        }

        private static CharacterActionTrackEventKind ResolveCombatEventKind(CombatTrackEventJsonDto evt)
        {
            if (Enum.TryParse(evt?.Kind, out CharacterActionTrackEventKind kind))
                return kind;
            return CharacterActionTrackEventKind.StartCombatAction;
        }

        private static bool IsHitTraceEvent(CharacterActionTrackEventKind kind)
        {
            return kind == CharacterActionTrackEventKind.HitTraceWindow;
        }

        private static bool HasHitVolume(CombatTrackEventJsonDto evt)
        {
            return IsEnabledHitVolume(evt?.HitVolume);
        }

        private static bool IsEnabledHitVolume(CharacterActionHitVolumeJsonDto hitVolume)
        {
            return hitVolume != null
                && (IsCasterBodyHitVolume(hitVolume) || hitVolume.Radius > 0f);
        }

        private static bool IsCasterBodyHitVolume(CharacterActionHitVolumeJsonDto hitVolume)
        {
            return string.Equals(
                hitVolume?.SourceKind,
                CharacterActionHitVolumeSourceKind.CasterBody.ToString(),
                StringComparison.Ordinal);
        }

        private static bool IsBakedSocketCapsuleHitVolume(CharacterActionHitVolumeJsonDto hitVolume)
        {
            return string.Equals(
                hitVolume?.SourceKind,
                CharacterActionHitVolumeSourceKind.BakedSocketCapsule.ToString(),
                StringComparison.Ordinal);
        }

        private static string HitVolumeHelpText(CharacterActionHitVolumeJsonDto hitVolume)
        {
            return IsCasterBodyHitVolume(hitVolume)
                ? "身体撞击判定 / Caster Body 使用施法者 runtime 物理体作为判定源，不依赖装备槽、武器 locator 或手动 capsule 数值。"
                : "Action Hit Volume 使用动作本地碰撞框作为判定源，不依赖装备槽或武器 locator。";
        }

        private static string BuildHitTraceWindowStatusSummary(HitTraceWindowTimelineAdapter adapter)
        {
            CombatTrackEventJsonDto window = adapter?.GetEvent();
            if (window == null)
                return "命中窗口不可用";

            string slot = CharacterActionAuthoringIds.Label(window.EquipmentSlotId, 405);
            string slotLabel = string.Equals(slot, "mainHand", StringComparison.Ordinal)
                ? "主手"
                : string.Equals(slot, "offHand", StringComparison.Ordinal) ? "副手" : slot;
            if (HasHitVolume(window))
                slotLabel = "自定义命中体";
            if (string.IsNullOrWhiteSpace(slotLabel))
                slotLabel = "未选择槽位";

            CharacterActionHitEventAuthoringModel hit =
                CharacterActionHitEventAuthoringModel.FromCombatTrackEvent(window);
            string damageType = CharacterActionDamageAuthoringText.FormatDamageTypeLabel(hit.DamageTypeId);
            string targetLabel = FormatHitTargetSummary(hit.TargetTypes);
            string repeatLabel = string.Equals(
                hit.HitSameTargetPolicy,
                CombatHitSameTargetPolicy.OncePerEvent.ToString(),
                StringComparison.Ordinal)
                ? "单次命中"
                : "允许重复";
            return slotLabel
                + " · " + damageType
                + " · " + Math.Max(0, window.Frame) + "—" + Math.Max(window.Frame, window.EndFrame) + " 帧"
                + " · " + targetLabel
                + " · " + repeatLabel;
        }

        private static string FormatHitTargetSummary(CombatHitTargetTypes targetTypes)
        {
            CombatHitTargetTypes allCharacters = CombatHitTargetTypes.HostileCharacter
                | CombatHitTargetTypes.FriendlyCharacter
                | CombatHitTargetTypes.Self
                | CombatHitTargetTypes.ThirdPartyCharacter;
            CombatHitTargetTypes environment = CombatHitTargetTypes.DamageableObject
                | CombatHitTargetTypes.WorldWall
                | CombatHitTargetTypes.WorldGround
                | CombatHitTargetTypes.WorldStatic;
            if (targetTypes == CombatHitTargetTypes.HostileCharacter)
                return "仅敌方";
            if (targetTypes == allCharacters)
                return "所有角色";
            if (targetTypes == environment)
                return "环境交互";
            return "自定义目标";
        }

        private static string ResolveWindowTraceProfile(CombatTrackEventJsonDto start, CombatTrackEventJsonDto stop)
        {
            return CharacterActionAuthoringIds.Label(
                start?.TraceProfileId > 0 ? start.TraceProfileId : stop?.TraceProfileId ?? 0, 403);
        }

        private static void AddLegacyTraceProfileHelp(VisualElement parent, CombatTrackEventJsonDto start, CombatTrackEventJsonDto stop)
        {
            string traceProfileId = ResolveWindowTraceProfile(start, stop);
            if (string.IsNullOrWhiteSpace(traceProfileId))
                return;

            parent.Add(new HelpBox(
                "兼容旧数据：仍保存判定配置 ID = " + traceProfileId + "。新配置应只选择装备槽位，具体判定配置由武器 TraceBindings 解析。",
                HelpBoxMessageType.Info));
        }

        private static void AddHitTraceWindowDiagnostics(VisualElement parent, CombatTrackEventJsonDto[] events)
        {
            string[] warnings = HitTraceWindowTimelineAdapter.BuildWindowWarnings(events);
            for (int i = 0; i < warnings.Length; i++)
                parent.Add(new HelpBox(warnings[i], HelpBoxMessageType.Warning));
        }

        private void RemoveCombatEvent(CharacterActionJsonDto action, CombatTrackEventJsonDto evt, int fallbackIndex)
        {
            ApplyStructuralChange(() =>
            {
                var events = new List<CombatTrackEventJsonDto>(action.CombatTrack?.Events ?? Array.Empty<CombatTrackEventJsonDto>());
                int index = ResolveEventIndex(events, evt, CharacterActionAuthoringIds.Label(evt?.StableEventId ?? 0, 401), fallbackIndex,
                    candidate => CharacterActionAuthoringIds.Label(candidate?.StableEventId ?? 0, 401));
                if (index >= 0)
                    events.RemoveAt(index);
                if (action.CombatTrack != null)
                    action.CombatTrack.Events = events.ToArray();
            }, UiRefreshScope.Tracks);
        }
    }
}

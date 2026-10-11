using System;
using System.Globalization;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        // Reuses published Inspector controls inside the existing scrollable event row.
        private float BindTacticalMovement(CharacterTestTacticalEditingSession editor, string timeline, string eventId,
            CharacterTestTacticalMovement movement, float y, float width)
        {
            if (movement == null) return y;
            bool compact = editor.Preview.FocusBlockId != null;
            void Choose(string name, string label, string[] choices, Action<CharacterTestTacticalMovement, string> change,
                Func<string, string> display = null)
            {
                if (compact) label = label.Replace("移动方式：", "方式：").Replace("移动目标：", "目标：").Replace("距离来源：", "距离：").Replace("关联动作槽：", "动作槽：");
                TacticalButton(name, label, 8, y, width - 16, editor.CanEdit, () => {
                    var token = editor.Token;
                    TacticalChoices(_root.GetChild(name), choices, value => editor.Commit(token, "修改移动配置", source =>
                        change(CharacterTestTacticalAuthoringEdits.Event(source, timeline, eventId).Operation.Movement, value)), display: display);
                });
                y += 40;
            }
            void Number(string name, string labelName, string label, float value, Action<CharacterTestTacticalMovement, float> change, bool integer = false)
            {
                SetVisible(labelName, true); SetText(labelName, label); Place(labelName, 8, y, 110, 32);
                TacticalInput(name, value.ToString(CultureInfo.InvariantCulture), label, 122, y, width - 130, editor, editor.CanEdit,
                    (token, text) => {
                        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
                            || float.IsNaN(number) || float.IsInfinity(number) || integer && (number < 0 || number > int.MaxValue || number != Math.Truncate(number)))
                        { editor.Feedback(integer ? "请输入非负整数。" : "请输入有限数值。"); return false; }
                        return editor.Commit(token, "修改" + label, source => change(CharacterTestTacticalAuthoringEdits.Event(source, timeline, eventId).Operation.Movement, number));
                    });
                _root.GetChild(name).asTextInput.align = global::FairyGUI.AlignType.Left;
                y += 40;
            }
            var strategies = Enum.GetValues(typeof(CharacterTestRuntimeAiLabMovementStrategy)).Cast<CharacterTestRuntimeAiLabMovementStrategy>().ToArray();
            Choose("btnDurationMode", "移动方式：" + CharacterTestTacticalMovement.Label(movement.Strategy), strategies.Select(CharacterTestTacticalMovement.Label).ToArray(),
                (m, value) => {
                    m.Strategy = strategies.First(s => CharacterTestTacticalMovement.Label(s) == value);
                    if (!m.UsesSide) m.Side = 0;
                    if (m.Strategy != CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget) m.RadialWeight = 0.65f;
                    if (!m.UsesDistance) { m.DistanceSource = "Manual"; m.ActionSlotId = string.Empty; }
                });
            string[] targets = { "当前战斗目标", "指定位置", "指定单位" };
            Choose("btnSignal", "移动目标：" + ((int)movement.Target >= 0 && (int)movement.Target < targets.Length ? targets[(int)movement.Target] : "未知目标"), targets, (m, value) => {
                m.Target = (CharacterTestRuntimeAiLabMovementTargetKind)Array.IndexOf(targets, value); m.EntityId = 0; m.X = m.Z = 0;
                if (m.Target != CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget) { m.DistanceSource = "Manual"; m.ActionSlotId = string.Empty; }
            });
            if (movement.Target == CharacterTestRuntimeAiLabMovementTargetKind.Entity)
                Number("inputLocalFrame", "timeLabel", "单位编号（0=队友）", movement.EntityId, (m, v) => m.EntityId = (int)v, true);
            if (movement.Target == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition)
            {
                Number("inputLocalFrame", "timeLabel", "目标 X", movement.X, (m, v) => m.X = v);
                Number("inputDurationFrames", "durationFramesLabel", "目标 Z", movement.Z, (m, v) => m.Z = v);
            }
            if (movement.UsesDistance)
            {
                string[] sources = { "手填距离", "动作有效距离带", "动作最佳距离" };
                string[] keys = { "Manual", "ActionBand", "ActionOptimal" };
                int selected = Array.IndexOf(keys, movement.DistanceSource);
                if (movement.Target == CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget)
                    Choose("btnTargetTimeline", "距离来源：" + (selected >= 0 ? sources[selected] : "未知"), sources, (m, value) => {
                        m.DistanceSource = keys[Array.IndexOf(sources, value)];
                        m.ActionSlotId = m.DistanceSource == "Manual" ? string.Empty : editor.Source.Slots.FirstOrDefault(s => !s.IsFunctional)?.SlotId ?? string.Empty;
                    });
                if (movement.DistanceSource != "Manual")
                    Choose("btnCharacterActionParameter", "距离参考：" + CharacterTestRuntimeAiPlannerDevSceneController.TacticalActionUseLabel(editor.Source, movement.ActionSlotId)
                        + (compact ? " · " + editor.Preview.DescribeMovementDistance(eventId) : ""),
                        editor.Source.Slots.Where(s => !s.IsFunctional).Select(s => s.SlotId).ToArray(), (m, value) => m.ActionSlotId = value,
                        value => CharacterTestRuntimeAiPlannerDevSceneController.TacticalActionUseLabel(editor.Source, value));
                else
                {
                    Number("inputMaxExecutions", "repeatLabel", movement.UsesBand ? "最小距离（米）" : "停止／触发距离", movement.Minimum, (m, value) => { m.Minimum = value; m.Maximum = Math.Max(m.Maximum, value); });
                    if (movement.UsesBand) Number("inputCooldown", "cooldownHint", "最大距离（米）", movement.Maximum, (m, value) => m.Maximum = value);
                }
            }
            if (movement.UsesSide)
            {
                string[] sides = { "自动方向", "向左", "向右" };
                Choose("btnSignalReason", "方向：" + sides[movement.Side == 0 ? 0 : movement.Side > 0 ? 1 : 2], sides,
                    (m, value) => m.Side = value == sides[0] ? 0 : value == sides[1] ? 1 : -1);
            }
            if (movement.Strategy == CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget)
                Number("inputOperationSecondaryParameter", "operationSecondaryParameterLabel", "径向修正强度", movement.RadialWeight, (m, value) => m.RadialWeight = value);
            if (compact) return y;
            var ev = CharacterTestTacticalAuthoringEdits.Event(editor.Source, timeline, eventId);
            int? duration = ev.Start?.Kind == "Absolute" && ev.End?.Kind == "Absolute" ? ev.End.Frame - ev.Start.Frame
                : ev.End?.Kind == "Anchor" && ev.End.SourceEventId == eventId && ev.End.Anchor == "Start" ? ev.End.OffsetFrames : (int?)null;
            SetVisible("priorityLabel", true); SetText("priorityLabel", "持续帧数"); Place("priorityLabel", 8, y, 110, 32);
            TacticalInput("inputPriority", duration?.ToString(CultureInfo.InvariantCulture) ?? "按时间轴关联", "持续帧数", 122, y, width - 130, editor, editor.CanEdit,
                (token, text) => {
                    if (!int.TryParse(text, out int frames) || frames <= 0) { editor.Feedback("持续帧数必须是正整数。"); return false; }
                    return editor.Commit(token, "修改移动持续帧数", source => CharacterTestTacticalAuthoringEdits.Event(source, timeline, eventId).End
                        = CharacterTestTacticalAuthoringTemplates.Anchor(eventId, "Start", frames));
                });
            _root.GetChild("inputPriority").asTextInput.align = global::FairyGUI.AlignType.Left;
            y += 40;
            SetVisible("runtimeState", true); SetText("runtimeState", editor.Preview.DescribeCompiledMovement(eventId));
            Place("runtimeState", 8, y, width - 16, 52);
            var resolved = _root.GetChild("runtimeState").asTextField;
            resolved.textFormat.size = 14; resolved.color = new UnityEngine.Color32(153, 193, 211, 255); resolved.alpha = 1; resolved.singleLine = false;
            return y + 56;
        }
    }
}

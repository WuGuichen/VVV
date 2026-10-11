using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Owns the stable candidate projection used by the behavior-plan decision surface.
    /// FairyGUI geometry and styling stay in the parent presentation binder.
    /// </summary>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabBehaviorPlanDecisionBinder
    {
        private int[] _candidateOrder = Array.Empty<int>();
        private int _candidateCount;
        private readonly Action<Fgui.GObject, string> _bindTooltip;
        internal readonly List<BehaviorPlanCandidateRowSlot> Rows =
            new List<BehaviorPlanCandidateRowSlot>(8);

        internal bool TechnicalDetailsVisible { get; set; }

        internal CharacterTestFairyGuiRuntimeAiLabBehaviorPlanDecisionBinder(
            Action<Fgui.GObject, string> bindTooltip = null)
        {
            _bindTooltip = bindTooltip;
        }

        private void BindTooltip(Fgui.GObject target, string text)
        {
            if (target == null)
                return;
            if (_bindTooltip != null)
                _bindTooltip(target, text ?? string.Empty);
            else
                target.tooltips = string.Empty;
        }

        internal int BindCandidates(CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            _candidateCount = Math.Min(
                timeline?.BehaviorCandidateCount ?? 0,
                timeline?.BehaviorCandidates?.Length ?? 0);
            EnsureCapacity(_candidateCount);
            for (int index = 0; index < _candidateCount; index++)
                _candidateOrder[index] = index;
            Sort(timeline);
            return _candidateCount;
        }

        internal int GetSourceIndex(int visibleIndex)
        {
            return visibleIndex >= 0 && visibleIndex < _candidateCount
                ? _candidateOrder[visibleIndex]
                : -1;
        }

        internal BehaviorPlanCandidateRowSlot GetRow(
            Fgui.GComponent details,
            int index,
            ICharacterTestRuntimeAiLabTimelineCommandSink commands)
        {
            while (Rows.Count <= index)
            {
                var slot = new BehaviorPlanCandidateRowSlot();
                var visual = new Fgui.GComponent
                {
                    name = "behaviorPlanCandidateRow"
                        + Rows.Count.ToString(CultureInfo.InvariantCulture),
                    touchable = true
                };
                var background = new Fgui.GGraph { touchable = false };
                var accent = new Fgui.GGraph { touchable = false };
                var title = CreateText(Fgui.AlignType.Left, RuntimeFrameSrgb(228, 239, 244));
                var condition = CreateText(Fgui.AlignType.Left, RuntimeFrameSrgb(173, 195, 204));
                var state = CreateText(Fgui.AlignType.Right, Color.white);
                visual.AddChild(background);
                visual.AddChild(accent);
                visual.AddChild(title);
                visual.AddChild(condition);
                visual.AddChild(state);
                slot.Visual = visual;
                slot.Background = background;
                slot.Accent = accent;
                slot.Title = title;
                slot.Condition = condition;
                slot.State = state;
                visual.onClick.Set(context =>
                {
                    context.StopPropagation();
                    if (slot.EntryIndex >= 0)
                    {
                        commands.Enqueue(
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                                .SelectWorkbenchEntry(slot.EntryIndex));
                    }
                });
                Rows.Add(slot);
                details.AddChild(visual);
            }
            BehaviorPlanCandidateRowSlot result = Rows[index];
            result.Visual.visible = true;
            return result;
        }

        internal void BindRow(
            BehaviorPlanCandidateRowSlot slot,
            CharacterTestRuntimeAiLabBehaviorCandidateViewModel candidate,
            float y,
            float height)
        {
            const float x = 24f;
            const float width = 472f;
            slot.EntryIndex = candidate?.EntryIndex ?? -1;
            slot.Visual.SetXY(x, y);
            slot.Visual.SetSize(width, height);
            BindTooltip(slot.Visual, candidate?.TooltipText);
            Color border = candidate?.Committed == true
                ? RuntimeFrameSrgb(22, 226, 220)
                : candidate?.Eligible == true
                    ? RuntimeFrameSrgb(74, 177, 88)
                    : RuntimeFrameSrgb(78, 101, 112);
            Color fill = candidate?.Committed == true
                ? RuntimeFrameSrgb(5, 50, 59, 244)
                : candidate?.Eligible == true
                    ? RuntimeFrameSrgb(8, 36, 27, 236)
                    : RuntimeFrameSrgb(12, 27, 35, 232);
            slot.Background.DrawRect(width, height, 1, border, fill);
            slot.Accent.SetXY(0f, 0f);
            slot.Accent.DrawRect(4f, height, 0, border, border);
            slot.Title.SetXY(14f, 0f);
            slot.Title.SetSize(164f, height);
            SetTimelineFontSize(slot.Title, height >= 46f ? 20 : 18);
            slot.Title.text = candidate?.Title ?? string.Empty;
            slot.Condition.SetXY(182f, 0f);
            slot.Condition.SetSize(180f, height);
            SetTimelineFontSize(slot.Condition, height >= 46f ? 17 : 15);
            int conditionIndex = ResolveCondition(candidate);
            slot.Condition.text = conditionIndex >= 0
                ? candidate.ConditionTexts[conditionIndex]
                : string.Empty;
            bool conditionKnown = conditionIndex >= 0 && candidate.ConditionKnown[conditionIndex];
            bool conditionSatisfied = conditionIndex >= 0
                && candidate.ConditionSatisfied[conditionIndex];
            slot.Condition.color = !conditionKnown
                ? RuntimeFrameSrgb(130, 151, 160)
                : conditionSatisfied
                    ? RuntimeFrameSrgb(91, 201, 105)
                    : RuntimeFrameSrgb(205, 213, 217);
            slot.State.SetXY(364f, 0f);
            slot.State.SetSize(94f, height);
            SetTimelineFontSize(slot.State, height >= 46f ? 17 : 15);
            slot.State.text = candidate?.ScoreText ?? string.Empty;
            slot.State.color = border;
        }

        internal void HideRows(int firstHidden = 0)
        {
            for (int index = firstHidden; index < Rows.Count; index++)
                Rows[index].Visual.visible = false;
        }

        internal void Reset()
        {
            _candidateCount = 0;
            TechnicalDetailsVisible = false;
        }

        internal void ResetForRoot()
        {
            Reset();
            Rows.Clear();
        }

        private void EnsureCapacity(int count)
        {
            if (_candidateOrder.Length < count)
                Array.Resize(ref _candidateOrder, Math.Max(count, 8));
        }

        /// <remarks>NoAlloc after the candidate high-water mark is initialized.</remarks>
        private void Sort(CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            for (int index = 1; index < _candidateCount; index++)
            {
                int candidateIndex = _candidateOrder[index];
                int insertionIndex = index;
                while (insertionIndex > 0
                    && Compare(
                        timeline.BehaviorCandidates[candidateIndex],
                        timeline.BehaviorCandidates[_candidateOrder[insertionIndex - 1]]) < 0)
                {
                    _candidateOrder[insertionIndex] = _candidateOrder[insertionIndex - 1];
                    insertionIndex--;
                }
                _candidateOrder[insertionIndex] = candidateIndex;
            }
        }

        private static int Compare(
            CharacterTestRuntimeAiLabBehaviorCandidateViewModel left,
            CharacterTestRuntimeAiLabBehaviorCandidateViewModel right)
        {
            int leftRank = ResolveRank(left);
            int rightRank = ResolveRank(right);
            if (leftRank != rightRank)
                return leftRank.CompareTo(rightRank);
            if (left?.ScoreAvailable == true && right?.ScoreAvailable == true)
            {
                int scoreOrder = right.Score.CompareTo(left.Score);
                if (scoreOrder != 0)
                    return scoreOrder;
            }
            else if (left?.ScoreAvailable == true)
            {
                return -1;
            }
            else if (right?.ScoreAvailable == true)
            {
                return 1;
            }
            return (left?.EntryIndex ?? int.MaxValue).CompareTo(
                right?.EntryIndex ?? int.MaxValue);
        }

        private static int ResolveRank(CharacterTestRuntimeAiLabBehaviorCandidateViewModel candidate)
        {
            if (candidate?.Committed == true)
                return 0;
            return candidate?.Eligible == true ? 1 : 2;
        }

        private static Fgui.GTextField CreateText(Fgui.AlignType align, Color color)
        {
            return new Fgui.GTextField
            {
                autoSize = Fgui.AutoSizeType.None,
                align = align,
                verticalAlign = Fgui.VertAlignType.Middle,
                singleLine = true,
                color = color
            };
        }

        private static int ResolveCondition(
            CharacterTestRuntimeAiLabBehaviorCandidateViewModel candidate)
        {
            int count = Math.Min(
                candidate?.ConditionCount ?? 0,
                candidate?.ConditionTexts?.Length ?? 0);
            for (int index = 0; index < count; index++)
            {
                if (candidate.ConditionKnown[index] && !candidate.ConditionSatisfied[index])
                    return index;
            }
            return count > 0 ? 0 : -1;
        }
    }

    internal sealed class BehaviorPlanCandidateRowSlot
    {
        internal Fgui.GComponent Visual;
        internal Fgui.GGraph Background;
        internal Fgui.GGraph Accent;
        internal Fgui.GTextField Title;
        internal Fgui.GTextField Condition;
        internal Fgui.GTextField State;
        internal int EntryIndex = -1;
    }
}

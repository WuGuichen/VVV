using System;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void BuildPhaseInspector(VisualElement parent, CharacterActionJsonDto action, int index)
        {
            CharacterActionPhaseJsonDto[] phases = action.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>();
            if (index < 0 || index >= phases.Length)
            {
                parent.Add(new Label("阶段不存在。"));
                return;
            }

            AddPhase(parent, action, phases, index);
        }

        private void AddPhase(VisualElement parent, CharacterActionJsonDto action, CharacterActionPhaseJsonDto[] phases, int index)
        {
            CharacterActionPhaseJsonDto phase = phases[index] ?? new CharacterActionPhaseJsonDto();
            VisualElement row = AddCard(parent, "阶段 " + index);
            row.Add(new Label(CharacterActionAuthoringText.PhaseSummary(phase)));
            AddEnumPopup<CharacterActionPhaseKind>(row, "阶段类型", phase.Kind, value => phase.Kind = value);
            AddIntegerField(row, "开始帧", phase.StartFrame, value =>
                CharacterActionPhaseTimelineUtility.SetPhaseStartFrame(
                    action,
                    index,
                    value,
                    action.DurationFrames ?? DefaultDurationFrames));
            AddIntegerField(row, "结束帧", phase.EndFrame, value =>
                CharacterActionPhaseTimelineUtility.SetPhaseEndFrame(
                    action,
                    index,
                    value,
                    action.DurationFrames ?? DefaultDurationFrames));
            row.Add(new Button(() => RemovePhase(action, index)) { text = "删除阶段" });
            phases[index] = phase;
        }

        private void AddPhase(CharacterActionJsonDto action)
        {
            ApplyStructuralChange(() =>
            {
                CharacterActionPhaseTimelineUtility.InsertPhaseAt(
                    action,
                    action.DurationFrames ?? DefaultDurationFrames,
                    action.DurationFrames ?? DefaultDurationFrames,
                    CharacterActionPhaseKind.Startup.ToString());
            }, UiRefreshScope.Tracks);
        }

        private void RemovePhase(CharacterActionJsonDto action, int index)
        {
            ApplyStructuralChange(() =>
            {
                CharacterActionPhaseTimelineUtility.RemovePhaseAt(
                    action,
                    index,
                    action.DurationFrames ?? DefaultDurationFrames);
            }, UiRefreshScope.Tracks);
        }

        private void FitLastPhaseToDuration(CharacterActionJsonDto action)
        {
            ApplyStructuralChange(() =>
            {
                CharacterActionPhaseTimelineUtility.NormalizePhases(
                    action,
                    action.DurationFrames ?? DefaultDurationFrames);
            }, UiRefreshScope.Tracks);
        }
    }
}

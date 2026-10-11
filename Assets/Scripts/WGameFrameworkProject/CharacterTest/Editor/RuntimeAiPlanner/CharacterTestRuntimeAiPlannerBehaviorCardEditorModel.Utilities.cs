using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestRuntimeAiPlannerBehaviorCardEditorModel
    {
        public static string FormatStepSummary(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            int index)
        {
            if (step == null)
                return (index + 1).ToString(CultureInfo.InvariantCulture) + ". <missing>";

            return (index + 1).ToString(CultureInfo.InvariantCulture)
                + ". "
                + step.DisplayTitle
                + " | "
                + CharacterTestRuntimeAiPlannerBehaviorCardStepDraft.FormatKind(step.Kind)
                + " | "
                + step.TargetSummary;
        }

        public static List<string> BuildStepChoiceLabels(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft)
        {
            var choices = new List<string> { "End" };
            if (draft == null)
                return choices;

            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step != null && !string.IsNullOrWhiteSpace(step.StepId))
                    choices.Add(step.StepId);
            }

            return choices;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardStepDraft FindStep(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            string stepId)
        {
            if (draft == null)
                return null;

            for (int i = 0; i < draft.Steps.Count; i++)
            {
                if (string.Equals(draft.Steps[i].StepId, stepId, StringComparison.Ordinal))
                    return draft.Steps[i];
            }

            return null;
        }
    }
}

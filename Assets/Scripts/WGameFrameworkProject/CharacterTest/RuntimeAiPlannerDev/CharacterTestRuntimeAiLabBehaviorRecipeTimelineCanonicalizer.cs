using System;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Keeps legacy v4 presentation fields deterministic after Track/Event authoring changes.
    /// Runtime execution never reads these values as an absolute multi-stage clock.
    /// </summary>
    internal static class CharacterTestRuntimeAiLabBehaviorRecipeTimelineCanonicalizer
    {
        private const int MaximumFrame = 36000;

        internal static bool TryCanonicalize(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            out string diagnostic)
        {
            if (payload == null)
            {
                diagnostic = "Behavior recipe payload is unavailable.";
                return false;
            }
            int count = payload.Stages.Count;
            int[] order = new int[count];
            int[] starts = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
                int cursor = i;
                while (cursor > 0
                    && Compare(payload, order[cursor], order[cursor - 1]) < 0)
                {
                    (order[cursor - 1], order[cursor]) = (order[cursor], order[cursor - 1]);
                    cursor--;
                }
            }
            int frame = 0;
            for (int orderedIndex = 0; orderedIndex < count; orderedIndex++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[order[orderedIndex]];
                int duration = ResolveDisplayDuration(stage);
                if ((long)frame + duration > MaximumFrame)
                {
                    diagnostic = "BEHAVIOR_RECIPE_TIMELINE_OVERFLOW: canonical Stage display exceeds 36000f.";
                    return false;
                }
                starts[orderedIndex] = frame;
                frame += duration;
            }
            for (int orderedIndex = 0; orderedIndex < count; orderedIndex++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[order[orderedIndex]];
                stage.Order = orderedIndex;
                stage.PlannedStartFrame = starts[orderedIndex];
                for (int layoutIndex = 0; layoutIndex < payload.Layout.Stages.Count; layoutIndex++)
                {
                    CharacterTestRuntimeAiLabLogicStageLayout layout = payload.Layout.Stages[layoutIndex];
                    if (layout != null
                        && string.Equals(layout.StageId, stage.StageId, StringComparison.Ordinal))
                    {
                        layout.Column = orderedIndex;
                        layout.Width = 1f;
                        layout.Offset = 0f;
                        break;
                    }
                }
            }
            diagnostic = string.Empty;
            return true;
        }

        private static int ResolveDisplayDuration(CharacterTestRuntimeAiLabLogicStage stage)
        {
            if (stage == null)
                return 1;
            if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait && stage.WaitFrames > 0)
                return stage.WaitFrames;
            if (stage.PlannedDurationFrames > 0)
                return stage.PlannedDurationFrames;
            if (stage.PreviewDurationFrames > 0)
                return stage.PreviewDurationFrames;
            return stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Logic ? 1 : 60;
        }

        private static int Compare(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            int leftIndex,
            int rightIndex)
        {
            CharacterTestRuntimeAiLabLogicStage left = payload.Stages[leftIndex];
            CharacterTestRuntimeAiLabLogicStage right = payload.Stages[rightIndex];
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            int order = left.Order.CompareTo(right.Order);
            return order != 0
                ? order
                : string.CompareOrdinal(left.StageId, right.StageId);
        }
    }
}

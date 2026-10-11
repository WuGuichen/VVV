using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiLabBehaviorCardStageMutation
    {
        private static bool TryValidateSelection(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string[] selectedStageIds,
            int selectedCount,
            out string diagnostic)
        {
            if (payload == null || selectedStageIds == null
                || selectedCount <= 0 || selectedCount > selectedStageIds.Length)
            {
                diagnostic = "Stage selection 不可用。";
                return false;
            }
            for (int i = 0; i < selectedCount; i++)
            {
                if (FindStage(payload, selectedStageIds[i]) == null)
                {
                    diagnostic = "Stage selection 包含失效 stable ID。";
                    return false;
                }
                for (int previous = 0; previous < i; previous++)
                {
                    if (string.Equals(selectedStageIds[previous], selectedStageIds[i], StringComparison.Ordinal))
                    {
                        diagnostic = "Stage selection 包含重复 stable ID。";
                        return false;
                    }
                }
            }
            diagnostic = string.Empty;
            return true;
        }

        private static CharacterTestRuntimeAiLabLogicStage FindStage(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId)
        {
            for (int i = 0; payload != null && i < payload.Stages.Count; i++)
            {
                if (string.Equals(payload.Stages[i].StageId, stageId, StringComparison.Ordinal))
                    return payload.Stages[i];
            }
            return null;
        }

        private static CharacterTestRuntimeAiLabLogicStageLayout FindLayout(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId)
        {
            for (int i = 0; payload?.Layout != null && i < payload.Layout.Stages.Count; i++)
            {
                if (string.Equals(payload.Layout.Stages[i].StageId, stageId, StringComparison.Ordinal))
                    return payload.Layout.Stages[i];
            }
            return null;
        }

        private static bool Contains(string[] values, int count, string value)
        {
            for (int i = 0; i < count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string AllocateStageId(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            Dictionary<string, string> allocated)
        {
            int suffix = 1;
            string candidate;
            do candidate = "stage.copy." + suffix++;
            while (FindStage(payload, candidate) != null || allocated.ContainsValue(candidate));
            return candidate;
        }

        private static string AllocateDetectorId(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            Dictionary<string, string> allocated)
        {
            int suffix = 1;
            string candidate;
            do candidate = "detector.copy." + suffix++;
            while (ContainsDetector(payload, candidate) || allocated.ContainsValue(candidate));
            return candidate;
        }

        private static string AllocateTransitionId(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            Dictionary<string, string> allocated)
        {
            int suffix = 1;
            string candidate;
            do candidate = "transition.copy." + suffix++;
            while (ContainsTransition(payload, candidate) || allocated.ContainsValue(candidate));
            return candidate;
        }

        private static bool ContainsDetector(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId)
        {
            for (int i = 0; i < payload.Detectors.Count; i++)
            {
                if (string.Equals(payload.Detectors[i].DetectorId, detectorId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool ContainsTransition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string transitionId)
        {
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                if (string.Equals(payload.Transitions[i].TransitionId, transitionId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static int MaximumLayoutColumn(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            int result = -1;
            for (int i = 0; payload?.Layout != null && i < payload.Layout.Stages.Count; i++)
                result = Math.Max(result, payload.Layout.Stages[i].Column);
            return result;
        }

        private static void NormalizeStageOrder(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            for (int i = 0; i < payload.Stages.Count; i++)
                payload.Stages[i].Order = i;
        }

        private static void NormalizeLayoutColumns(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            payload.Layout.Stages.Sort(LayoutComparer.Instance);
            for (int i = 0; i < payload.Layout.Stages.Count; i++)
                payload.Layout.Stages[i].Column = i;
        }

        private static void RemoveTransitionsForDetector(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId)
        {
            for (int i = payload.Transitions.Count - 1; i >= 0; i--)
            {
                if (string.Equals(payload.Transitions[i].DetectorId, detectorId, StringComparison.Ordinal))
                    payload.Transitions.RemoveAt(i);
            }
        }

        private static bool NoChange(out bool changed, out string diagnostic)
        {
            changed = false;
            diagnostic = "Stage 字段没有变化。";
            return true;
        }

        private static bool TryValidateUniqueGraphIds(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            out string diagnostic)
        {
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                if (stage == null || string.IsNullOrWhiteSpace(stage.StageId))
                {
                    diagnostic = "DraftInvalid 包含空 Stage stable ID，无法复制。";
                    return false;
                }
                for (int previous = 0; previous < i; previous++)
                {
                    if (string.Equals(payload.Stages[previous]?.StageId, stage.StageId, StringComparison.Ordinal))
                    {
                        diagnostic = "DraftInvalid 包含重复 Stage stable ID，无法复制。";
                        return false;
                    }
                }
            }
            for (int i = 0; i < payload.Detectors.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                if (detector == null || string.IsNullOrWhiteSpace(detector.DetectorId))
                {
                    diagnostic = "DraftInvalid 包含空 Detector stable ID，无法复制。";
                    return false;
                }
                for (int previous = 0; previous < i; previous++)
                {
                    if (string.Equals(payload.Detectors[previous]?.DetectorId, detector.DetectorId, StringComparison.Ordinal))
                    {
                        diagnostic = "DraftInvalid 包含重复 Detector stable ID，无法复制。";
                        return false;
                    }
                }
            }
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                if (transition == null || string.IsNullOrWhiteSpace(transition.TransitionId))
                {
                    diagnostic = "DraftInvalid 包含空 Transition stable ID，无法复制。";
                    return false;
                }
                for (int previous = 0; previous < i; previous++)
                {
                    if (string.Equals(
                            payload.Transitions[previous]?.TransitionId,
                            transition.TransitionId,
                            StringComparison.Ordinal))
                    {
                        diagnostic = "DraftInvalid 包含重复 Transition stable ID，无法复制。";
                        return false;
                    }
                }
            }
            diagnostic = string.Empty;
            return true;
        }

        private static int ClampToInt(long value)
        {
            return value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value;
        }

        private sealed class LayoutComparer : IComparer<CharacterTestRuntimeAiLabLogicStageLayout>
        {
            internal static readonly LayoutComparer Instance = new LayoutComparer();

            public int Compare(
                CharacterTestRuntimeAiLabLogicStageLayout left,
                CharacterTestRuntimeAiLabLogicStageLayout right)
            {
                if (ReferenceEquals(left, right))
                    return 0;
                if (left == null)
                    return -1;
                if (right == null)
                    return 1;
                int column = left.Column.CompareTo(right.Column);
                return column != 0
                    ? column
                    : string.CompareOrdinal(left.StageId, right.StageId);
            }
        }
    }
}

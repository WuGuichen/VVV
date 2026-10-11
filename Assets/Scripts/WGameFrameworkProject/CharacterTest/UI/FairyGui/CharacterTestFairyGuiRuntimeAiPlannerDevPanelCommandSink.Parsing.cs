using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private static bool TryParseSetupSlotCommand(
            string commandId,
            out int teamId,
            out int slotIndex)
        {
            teamId = 0;
            slotIndex = -1;
            string prefix = CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectSetupSlotPrefix;
            if (string.IsNullOrEmpty(commandId)
                || !commandId.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string payload = commandId.Substring(prefix.Length);
            int separator = payload.IndexOf('.');
            return separator > 0
                && int.TryParse(payload.Substring(0, separator), out teamId)
                && int.TryParse(payload.Substring(separator + 1), out slotIndex);
        }

        private static bool TryParseWorkbenchEntryCommand(string commandId, out int entryIndex)
        {
            entryIndex = -1;
            string prefix = CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntryPrefix;
            return !string.IsNullOrEmpty(commandId)
                && commandId.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(commandId.Substring(prefix.Length), out entryIndex)
                && entryIndex >= 0;
        }

        private static bool TryParseWorkbenchScrollCommand(string commandId, out int offset)
        {
            offset = 0;
            string prefix = CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ScrollWorkbenchPrefix;
            return !string.IsNullOrEmpty(commandId)
                && commandId.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(commandId.Substring(prefix.Length), out offset);
        }

        private static bool TryParseCompiledStepCommand(string commandId, out int stepIndex)
        {
            stepIndex = -1;
            string prefix = CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectCompiledStepPrefix;
            return !string.IsNullOrEmpty(commandId)
                && commandId.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(commandId.Substring(prefix.Length), out stepIndex)
                && stepIndex >= 0;
        }

        private static bool TryParseResponsibilityMove(
            string payload,
            out int entryIndex,
            out CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind)
        {
            entryIndex = -1;
            responsibilityKind = CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other;
            if (string.IsNullOrWhiteSpace(payload))
                return false;
            int separator = payload.IndexOf('|');
            return separator > 0
                && int.TryParse(payload.Substring(0, separator), out entryIndex)
                && entryIndex >= 0
                && Enum.TryParse(payload.Substring(separator + 1), out responsibilityKind);
        }

        private static bool TryParseResponsibilitySet(
            string payload,
            out int entryIndex,
            out CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] responsibilityKinds)
        {
            entryIndex = -1;
            responsibilityKinds = Array.Empty<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>();
            if (string.IsNullOrWhiteSpace(payload))
                return false;
            int separator = payload.IndexOf('|');
            if (separator <= 0
                || !int.TryParse(payload.Substring(0, separator), out entryIndex)
                || entryIndex < 0)
            {
                return false;
            }

            string[] tokens = payload.Substring(separator + 1).Split(',');
            var parsed = new CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[tokens.Length];
            int count = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!Enum.TryParse(tokens[i], out CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind value))
                    return false;
                parsed[count++] = value;
            }
            if (count == 0)
                return false;
            responsibilityKinds = CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy
                .Normalize(parsed);
            return true;
        }

    }
}

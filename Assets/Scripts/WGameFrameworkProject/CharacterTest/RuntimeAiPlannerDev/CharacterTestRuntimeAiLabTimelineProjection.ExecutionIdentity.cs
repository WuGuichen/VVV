using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Resolves the behavior-composition identity embedded in generated action names.
    /// This is a low-frequency Runtime AI Lab projection helper; it is never called by Tick.
    /// </summary>
    internal static class CharacterTestRuntimeAiLabExecutionIdentity
    {
        public static bool TryParseRuntimeEntryId(string actionName, out string entryId)
        {
            entryId = string.Empty;
            if (string.IsNullOrEmpty(actionName) || actionName[actionName.Length - 1] != ']')
                return false;

            int openingBracket = actionName.LastIndexOf('[');
            if (openingBracket < 0 || openingBracket + 1 >= actionName.Length - 1)
                return false;

            entryId = actionName.Substring(openingBracket + 1, actionName.Length - openingBracket - 2);
            return !string.IsNullOrEmpty(entryId);
        }
    }
}

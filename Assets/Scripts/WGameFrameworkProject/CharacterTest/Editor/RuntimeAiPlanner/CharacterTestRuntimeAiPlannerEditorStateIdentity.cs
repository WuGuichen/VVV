using System;
using MxFramework.Editor.CharacterAction;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestRuntimeAiPlannerEditorStateIdentity
    {
        // Reserved authoring domain for persistent Runtime AI Planner HFSM state identities.
        internal const int DomainId = 419;
        private const string SourcePrefix = "runtime-ai-planner/hfsm-state/";

        internal static int ResolveOrAllocate(string value)
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
                return 0;
            if (int.TryParse(normalized, out int numericId))
                return ValidateNumeric(numericId);
            if (normalized.StartsWith(SourcePrefix, StringComparison.Ordinal))
                return CharacterContentIdentityAuthoringCatalog.GetExisting(DomainId, normalized);
            switch (normalized)
            {
                case "root": return CharacterTestRuntimeAiPlannerProfileSource.RootStateId;
                case "engage": return CharacterTestRuntimeAiPlannerProfileSource.EngageStateId;
                case "chase": return CharacterTestRuntimeAiPlannerProfileSource.ChaseStateId;
                case "attack": return CharacterTestRuntimeAiPlannerProfileSource.AttackStateId;
            }
            return CharacterContentIdentityAuthoringCatalog.GetOrAllocate(
                DomainId,
                SourcePrefix + normalized);
        }

        internal static int ValidateNumeric(int numericId)
        {
            if (numericId <= 0)
                throw new InvalidOperationException("Runtime AI Planner HFSM state numeric identity must be positive.");
            if (numericId == CharacterTestRuntimeAiPlannerProfileSource.RootStateId
                || numericId == CharacterTestRuntimeAiPlannerProfileSource.EngageStateId
                || numericId == CharacterTestRuntimeAiPlannerProfileSource.ChaseStateId
                || numericId == CharacterTestRuntimeAiPlannerProfileSource.AttackStateId)
                return numericId;
            CharacterContentIdentityAuthoringCatalog.GetSourceKey(DomainId, numericId);
            return numericId;
        }

        internal static string LabelForNumeric(int numericId)
        {
            switch (ValidateNumeric(numericId))
            {
                case CharacterTestRuntimeAiPlannerProfileSource.RootStateId: return "root";
                case CharacterTestRuntimeAiPlannerProfileSource.EngageStateId: return "engage";
                case CharacterTestRuntimeAiPlannerProfileSource.ChaseStateId: return "chase";
                case CharacterTestRuntimeAiPlannerProfileSource.AttackStateId: return "attack";
                default: return CharacterContentIdentityAuthoringCatalog.GetSourceKey(DomainId, numericId);
            }
        }

        internal static bool IsBuiltin(int numericId)
        {
            return numericId == CharacterTestRuntimeAiPlannerProfileSource.RootStateId
                || numericId == CharacterTestRuntimeAiPlannerProfileSource.EngageStateId
                || numericId == CharacterTestRuntimeAiPlannerProfileSource.ChaseStateId
                || numericId == CharacterTestRuntimeAiPlannerProfileSource.AttackStateId;
        }

        internal static void Retire(int numericId)
        {
            if (!IsBuiltin(numericId))
                CharacterContentIdentityAuthoringCatalog.Retire(DomainId, ValidateNumeric(numericId));
        }
    }
}

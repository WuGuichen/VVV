using System;
using System.Collections.Generic;

namespace MxFramework.CharacterApplication
{
    public readonly struct CombatActionBindingResolveResult
    {
        public CombatActionBindingResolveResult(
            CombatActionSetId actionSetId,
            CombatActionEntry[] actions,
            CharacterDiagnostic[] diagnostics)
        {
            ActionSetId = actionSetId;
            Actions = actions ?? Array.Empty<CombatActionEntry>();
            Diagnostics = diagnostics ?? Array.Empty<CharacterDiagnostic>();
        }

        public CombatActionSetId ActionSetId { get; }
        public CombatActionEntry[] Actions { get; }
        public CharacterDiagnostic[] Diagnostics { get; }

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Diagnostics.Length; i++)
                {
                    if (Diagnostics[i].Severity == CharacterDiagnosticSeverity.Error)
                        return true;
                }

                return false;
            }
        }
    }

    public static class CombatActionBindingResolver
    {
        public static CombatActionBindingResolveResult Resolve(CombatActionSetConfig actionSet)
        {
            var diagnostics = new CharacterDiagnosticBuilder();
            if (actionSet == null)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.MissingCombatActionSet,
                    CombatActionSetConfig.TableName,
                    0,
                    string.Empty,
                    nameof(actionSet),
                    "Combat action set is required.");
                return new CombatActionBindingResolveResult(default, Array.Empty<CombatActionEntry>(), diagnostics.ToArray());
            }

            var actionKeys = new HashSet<string>(StringComparer.Ordinal);
            var actions = new List<CombatActionEntry>();
            if (actionSet.Actions == null || actionSet.Actions.Length == 0)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.MissingCombatAction,
                    CombatActionSetConfig.TableName,
                    actionSet.Id,
                    actionSet.StableId,
                    nameof(CombatActionSetConfig.Actions),
                    "Combat action set has no action bindings.");
                return new CombatActionBindingResolveResult(actionSet.ActionSetId, Array.Empty<CombatActionEntry>(), diagnostics.ToArray());
            }

            for (int i = 0; i < actionSet.Actions.Length; i++)
            {
                CombatActionEntry action = actionSet.Actions[i];
                bool valid = true;
                if (string.IsNullOrEmpty(action.ActionKey))
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.MissingCombatAction,
                        CombatActionSetConfig.TableName,
                        actionSet.Id,
                        actionSet.StableId,
                        "Actions[" + i + "].ActionKey",
                        "Combat action binding must declare a non-empty action key.");
                    valid = false;
                }
                else if (!actionKeys.Add(action.ActionKey))
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.DuplicateCombatActionKey,
                        CombatActionSetConfig.TableName,
                        actionSet.Id,
                        actionSet.StableId,
                        "Actions[" + i + "].ActionKey",
                        "Combat action set contains duplicate action key: " + action.ActionKey + ".");
                    valid = false;
                }

                if (!action.CombatActionId.IsValid)
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.MissingCombatAction,
                        CombatActionSetConfig.TableName,
                        actionSet.Id,
                        actionSet.StableId,
                        "Actions[" + i + "].CombatActionId",
                        "Combat action binding references an invalid combat action id.");
                    valid = false;
                }

                if (string.IsNullOrEmpty(action.AnimationActionKey))
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.MissingAnimationAction,
                        CombatActionSetConfig.TableName,
                        actionSet.Id,
                        actionSet.StableId,
                        "Actions[" + i + "].AnimationActionKey",
                        "Combat action binding must declare an animation action key.");
                    valid = false;
                }

                if (valid)
                    actions.Add(action);
            }

            return new CombatActionBindingResolveResult(actionSet.ActionSetId, actions.ToArray(), diagnostics.ToArray());
        }
    }
}

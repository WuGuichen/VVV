using System;
using System.Collections.Generic;
using MxFramework.Input;

namespace MxFramework.Settings.Input
{
    public sealed class MxInputRebindingSettingsController
    {
        private readonly IInputRebindingService _rebinding;
        private readonly InputBindingDisplayService _bindingDisplay;

        public MxInputRebindingSettingsController(
            IInputRebindingService rebinding,
            InputBindingDisplayService bindingDisplay)
        {
            _rebinding = rebinding;
            _bindingDisplay = bindingDisplay;
        }

        public bool IsRebinding => _rebinding != null && _rebinding.IsRebinding;
        public bool CanRebind => _rebinding != null;
        public bool CanListBindings => _bindingDisplay != null;
        public bool IsEditSessionActive =>
            (_rebinding as IInputRebindingEditSessionService)?.IsEditSessionActive == true;
        public bool HasPendingChanges =>
            (_rebinding as IInputRebindingEditSessionService)?.HasPendingChanges == true;

        public MxInputBindingListResult ListBindings(IReadOnlyList<MxInputBindingTarget> targets)
        {
            if (_bindingDisplay == null)
            {
                return MxInputBindingListResult.Failed(new MxSettingsIssue(
                    MxSettingsIssueCode.AdapterUnavailable,
                    "settings.input.bindingDisplay",
                    "Input binding display service is not configured."));
            }

            if (targets == null || targets.Count == 0)
                return MxInputBindingListResult.Succeeded(Array.Empty<MxInputBindingInfo>());

            var bindings = new List<MxInputBindingInfo>(targets.Count);
            var issues = new List<MxSettingsIssue>();
            for (int i = 0; i < targets.Count; i++)
            {
                MxInputBindingTarget target = targets[i];
                string path = "settings.input.bindings[" + i + "]";
                if (string.IsNullOrWhiteSpace(target.ActionId) || target.BindingIndex < 0)
                {
                    issues.Add(new MxSettingsIssue(
                        MxSettingsIssueCode.InvalidTarget,
                        path,
                        "Input binding target is invalid."));
                    continue;
                }

                if (!_bindingDisplay.TryGetDisplayString(
                        target.ActionId,
                        target.BindingIndex,
                        out string displayString))
                {
                    issues.Add(new MxSettingsIssue(
                        MxSettingsIssueCode.InvalidTarget,
                        path,
                        "Input binding target was not found: " + target.ActionId + "[" + target.BindingIndex + "]."));
                    continue;
                }

                bindings.Add(new MxInputBindingInfo(target, displayString));
            }

            return issues.Count == 0
                ? MxInputBindingListResult.Succeeded(bindings)
                : MxInputBindingListResult.WithIssues(bindings, issues);
        }

        public MxInputSettingsOperationResult StartRebind(string actionId, int bindingIndex)
        {
            return InvokeRebinding(
                "settings.input.rebind",
                () => _rebinding.StartRebind(actionId, bindingIndex));
        }

        public MxInputSettingsOperationResult Cancel()
        {
            return InvokeRebinding("settings.input.cancel", () => _rebinding.Cancel());
        }

        public MxInputSettingsOperationResult Save()
        {
            return InvokeRebinding("settings.input.save", () => _rebinding.Save());
        }

        public MxInputSettingsOperationResult Load()
        {
            return InvokeRebinding("settings.input.load", () => _rebinding.Load());
        }

        public MxInputSettingsOperationResult ResetToDefault()
        {
            return InvokeRebinding("settings.input.reset", () => _rebinding.ResetToDefault());
        }

        public MxInputSettingsOperationResult BeginEditSession()
        {
            return InvokeEditSession(
                "settings.input.edit.begin",
                session => session.BeginEditSession());
        }

        public MxInputSettingsOperationResult CommitEditSession()
        {
            return InvokeEditSession(
                "settings.input.edit.commit",
                session => session.CommitEditSession(),
                fallback: () => _rebinding.Save());
        }

        public MxInputSettingsOperationResult RollbackEditSession()
        {
            return InvokeEditSession(
                "settings.input.edit.rollback",
                session => session.RollbackEditSession(),
                fallback: () => _rebinding.Load());
        }

        private MxInputSettingsOperationResult InvokeRebinding(string path, Action operation)
        {
            if (_rebinding == null)
            {
                return MxInputSettingsOperationResult.Failed(new MxSettingsIssue(
                    MxSettingsIssueCode.AdapterUnavailable,
                    path,
                    "Input rebinding service is not configured."));
            }

            try
            {
                operation();
                return MxInputSettingsOperationResult.Succeeded();
            }
            catch (Exception ex)
            {
                return MxInputSettingsOperationResult.Failed(new MxSettingsIssue(
                    MxSettingsIssueCode.OperationFailed,
                    path,
                    ex.Message,
                    ex));
            }
        }

        private MxInputSettingsOperationResult InvokeEditSession(
            string path,
            Action<IInputRebindingEditSessionService> operation,
            Action fallback = null)
        {
            if (_rebinding == null)
            {
                return MxInputSettingsOperationResult.Failed(new MxSettingsIssue(
                    MxSettingsIssueCode.AdapterUnavailable,
                    path,
                    "Input rebinding service is not configured."));
            }

            try
            {
                var editSession = _rebinding as IInputRebindingEditSessionService;
                if (editSession != null)
                    operation(editSession);
                else
                    fallback?.Invoke();
                return MxInputSettingsOperationResult.Succeeded();
            }
            catch (Exception ex)
            {
                return MxInputSettingsOperationResult.Failed(new MxSettingsIssue(
                    MxSettingsIssueCode.OperationFailed,
                    path,
                    ex.Message,
                    ex));
            }
        }
    }
}

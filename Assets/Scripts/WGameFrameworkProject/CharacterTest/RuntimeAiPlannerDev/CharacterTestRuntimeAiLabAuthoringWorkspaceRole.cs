#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabAuthoringWorkspaceRole
    {
        Main = 0,
        ExperimentA = 1,
        ExperimentB = 2
    }

    internal readonly struct CharacterTestRuntimeAiLabAuthoringWorkspaceRoleDefinition
    {
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleDefinition(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            string displayName,
            bool canPublish,
            bool canReset)
        {
            Role = role;
            DisplayName = displayName;
            CanPublish = canPublish;
            CanReset = canReset;
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRole Role { get; }
        internal string DisplayName { get; }
        internal bool CanPublish { get; }
        internal bool CanReset { get; }
    }

    internal static class CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract
    {
        private static readonly CharacterTestRuntimeAiLabAuthoringWorkspaceRoleDefinition[] Definitions =
        {
            new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleDefinition(
                CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main,
                "Main Workspace",
                canPublish: true,
                canReset: false),
            new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleDefinition(
                CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA,
                "Experiment A",
                canPublish: false,
                canReset: true),
            new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleDefinition(
                CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB,
                "Experiment B",
                canPublish: false,
                canReset: true)
        };

        internal static IReadOnlyList<CharacterTestRuntimeAiLabAuthoringWorkspaceRoleDefinition> All =>
            Definitions;

        internal static bool IsValid(CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                || role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA
                || role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB;
        }

        internal static bool IsExperiment(CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA
                || role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB;
        }

        internal static CharacterTestRuntimeAiLabAuthoringWorkspaceRoleDefinition Get(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            if (!IsValid(role))
                throw new ArgumentOutOfRangeException(nameof(role));
            return Definitions[(int)role];
        }
    }

    internal enum CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus
    {
        NotStarted = 0,
        Succeeded = 1,
        DisabledForIsolatedRoot = 2,
        InvalidRole = 3,
        RegistryInvalid = 4,
        MainUnavailable = 5,
        ExperimentNotInitialized = 6,
        WorkspaceLoadFailed = 7,
        FlushFailed = 8,
        StateSaveFailed = 9,
        ExperimentAlreadyInitialized = 10,
        ReadOnly = 11,
        MainRevisionConflict = 12,
        RollbackFailed = 13
    }

    internal readonly struct CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult
    {
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus status,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            string message)
        {
            Status = status;
            Role = role;
            Workspace = workspace;
            Message = message ?? string.Empty;
        }

        internal bool Success => Status == CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.Succeeded;
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus Status { get; }
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRole Role { get; }
        internal CharacterTestRuntimeAiLabDraftWorkspace Workspace { get; }
        internal string Message { get; }
    }

    /// <summary>
    /// Editor-only fixed-role coordinator. Draft bodies and their transactions remain owned by
    /// <see cref="CharacterTestRuntimeAiLabDraftPersistence"/>.
    /// </summary>
    internal sealed partial class CharacterTestRuntimeAiLabAuthoringWorkspaceRoles
    {
        private readonly CharacterTestRuntimeAiLabDraftPersistence _persistence;
        private readonly CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistry _registry;
        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState _state;
        private CharacterTestRuntimeAiLabAuthoringWorkspaceRole _activeRole;
        private bool _hasActiveRole;

        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoles(
            CharacterTestRuntimeAiLabDraftPersistence persistence,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistry registry)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _registry = registry;
        }

        internal bool Enabled => _registry != null;
        internal bool HasActiveRole => _hasActiveRole;
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRole ActiveRole => _activeRole;
        internal CharacterTestRuntimeAiLabDraftWorkspace ActiveWorkspace =>
            _hasActiveRole ? _persistence.Workspace : null;

        internal static CharacterTestRuntimeAiLabAuthoringWorkspaceRoles Create(
            CharacterTestRuntimeAiLabDraftPersistence persistence)
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));
            return new CharacterTestRuntimeAiLabAuthoringWorkspaceRoles(
                persistence,
                persistence.UsesSharedAuthoringRoot
                    ? new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistry(
                        Path.Combine(persistence.DraftsRoot, "workspace-roles.json"),
                        Path.Combine("UserSettings", "RuntimeAiLab", "active-workspace-role.txt"))
                    : null);
        }

        internal static CharacterTestRuntimeAiLabAuthoringWorkspaceRoles CreateIsolated(
            CharacterTestRuntimeAiLabDraftPersistence persistence,
            string registryPath,
            string activeRolePath,
            Func<string, bool> shouldFailStateWrite = null)
        {
            return new CharacterTestRuntimeAiLabAuthoringWorkspaceRoles(
                persistence,
                new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistry(
                    registryPath,
                    activeRolePath,
                    shouldFailStateWrite));
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult OpenInitial(
            CharacterTestArenaMatchConfig currentSetup)
        {
            _persistence.DeactivateWorkspace();
            _state = null;
            _hasActiveRole = false;
            if (!Enabled)
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.DisabledForIsolatedRoot);

            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus load =
                _registry.Load(out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState state, out string message);
            if (load == CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus.Invalid)
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.RegistryInvalid, message);
            if (load == CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus.Missing)
                return CreateMain(currentSetup);

            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role =
                _registry.ReadActiveRole(out bool rewriteActiveRole);
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry target = state.Get(role);
            if (target == null)
            {
                return Fail(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ExperimentNotInitialized,
                    "The selected experiment has not been copied from Main.",
                    role);
            }

            if (!TryLoad(target, out CharacterTestRuntimeAiLabDraftWorkspace workspace, out message))
            {
                return Fail(
                    role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                        ? CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainUnavailable
                        : CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed,
                    message,
                    role);
            }
            if (rewriteActiveRole && !_registry.WriteActiveRole(role, out message))
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed, message, role);

            Activate(state, role, workspace, message);
            return Ok(role, workspace, "Opened " + CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.Get(role).DisplayName + ".");
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult Switch(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            if (!CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsValid(role))
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.InvalidRole, role: role);
            if (!Enabled)
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.DisabledForIsolatedRoot, role: role);
            if (_hasActiveRole && _activeRole == role && _persistence.Workspace != null)
                return Ok(role, _persistence.Workspace, "The requested workspace is already active.");
            if (!ReloadAndValidate(
                    role,
                    requireActiveSession: false,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState state,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult failure))
                return failure;

            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry target = state.Get(role);
            if (target == null)
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ExperimentNotInitialized, role: role);
            if (_hasActiveRole && !Flush(role, out failure))
                return failure;
            if (!TryLoad(target, out CharacterTestRuntimeAiLabDraftWorkspace workspace, out string message))
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed, message, role);
            if (!_registry.WriteActiveRole(role, out message))
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed, message, role);

            Activate(state, role, workspace, message);
            return Ok(role, workspace, "Switched to " + CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.Get(role).DisplayName + ".");
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult InitializeExperimentFromMain(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return CopyMain(role, reset: false);
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult ResetExperimentFromMain(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return CopyMain(role, reset: true);
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult ApplyExperimentToMain(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            if (!CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsExperiment(role))
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.InvalidRole, role: role);
            if (!ReloadAndValidate(
                    role,
                    requireActiveSession: true,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState state,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult failure))
                return failure;

            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry experiment = state.Get(role);
            if (experiment == null)
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ExperimentNotInitialized, role: role);
            if (!Flush(role, out failure))
                return failure;
            if (!TryLoad(state.Main, out CharacterTestRuntimeAiLabDraftWorkspace main, out string message))
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainUnavailable, message, role);
            if (!string.Equals(experiment.SourceMainWorkspaceId, main.WorkspaceId, StringComparison.Ordinal)
                || experiment.SourceMainRevision != main.CurrentRevision)
            {
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainRevisionConflict, role: role);
            }
            if (!TryLoad(experiment, out CharacterTestRuntimeAiLabDraftWorkspace source, out message))
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed, message, role);
            if (main.IsReadOnly || source.IsReadOnly)
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ReadOnly, role: role);

            int revision = Math.Max(main.CurrentRevision, source.CurrentRevision) + 1;
            if (!_persistence.TryCloneWorkspace(
                    source.WorkspaceId,
                    Guid.NewGuid().ToString("D"),
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.Get(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main).DisplayName,
                    revision,
                    out CharacterTestRuntimeAiLabDraftWorkspace nextMain,
                    out message))
            {
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed, message, role);
            }

            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState next = state.Clone();
            next.Main = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry(
                nextMain.WorkspaceId,
                previousWorkspaceId: main.WorkspaceId);
            if (!_registry.Save(next, out message))
            {
                return FailAfterOrphanCleanup(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed,
                    message,
                    role,
                    nextMain.WorkspaceId,
                    state);
            }

            CharacterTestRuntimeAiLabAuthoringWorkspaceRole previousRole = _activeRole;
            bool activeRoleChanged = previousRole != CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main;
            if (activeRoleChanged
                && !_registry.WriteActiveRole(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, out message))
            {
                if (!_registry.Save(state, out string rollbackMessage))
                    return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.RollbackFailed, message + " " + rollbackMessage, role);
                return FailAfterOrphanCleanup(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed,
                    message,
                    role,
                    nextMain.WorkspaceId,
                    state);
            }
            if (!_persistence.TryWriteLatestWorkspacePointer(nextMain.WorkspaceId, out message))
            {
                bool registryRestored = _registry.Save(state, out string registryRollback);
                string activeRollback = "unchanged";
                bool activeRestored = !activeRoleChanged
                    || _registry.WriteActiveRole(previousRole, out activeRollback);
                bool latestRestored = _persistence.TryWriteLatestWorkspacePointer(
                    main.WorkspaceId,
                    out string latestRollback);
                if (!registryRestored || !activeRestored || !latestRestored)
                {
                    return Fail(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.RollbackFailed,
                        message + " Registry: " + registryRollback
                            + " Active: " + activeRollback
                            + " Latest: " + latestRollback,
                        role);
                }
                return FailAfterOrphanCleanup(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed,
                    message,
                    role,
                    nextMain.WorkspaceId,
                    state);
            }

            Activate(next, CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, nextMain, message);
            return Ok(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, nextMain, "Applied the experiment to Main.");
        }

        internal bool TryGetEntry(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry entry)
        {
            entry = _state?.Get(role);
            return entry != null;
        }

        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult CreateMain(
            CharacterTestArenaMatchConfig currentSetup)
        {
            CharacterTestRuntimeAiLabDraftWorkspace main;
            string message;
            if (_persistence.HasLatestWorkspacePointer)
            {
                if (!_persistence.TryReadLatestWorkspaceId(out string id, out message)
                    || !_persistence.TryLoad(id, out main, out message))
                {
                    return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainUnavailable, message);
                }
            }
            else
            {
                _persistence.SetLatestWorkspacePointerUpdateEnabled(enabled: true);
                main = _persistence.CreateFromCurrent(currentSetup);
                main.DisplayName = CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.Get(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main).DisplayName;
                if (!_persistence.Flush())
                {
                    _persistence.DeactivateWorkspace(_persistence.LastStatus);
                    return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed, _persistence.LastStatus);
                }
                message = _persistence.LastStatus;
            }

            var state = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState
            {
                Main = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry(main.WorkspaceId)
            };
            if (!_registry.Save(state, out message))
            {
                _persistence.DeactivateWorkspace(message);
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed, message);
            }
            if (!_registry.WriteActiveRole(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, out message))
            {
                _persistence.DeactivateWorkspace(message);
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed, message);
            }

            Activate(state, CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, main, message);
            return Ok(CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main, main, "Initialized Main.");
        }

        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult CopyMain(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            bool reset)
        {
            if (!CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsExperiment(role))
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.InvalidRole, role: role);
            if (!ReloadAndValidate(
                    role,
                    requireActiveSession: true,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState state,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult failure))
                return failure;

            bool initialized = state.Get(role) != null;
            if (!reset && initialized)
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ExperimentAlreadyInitialized, role: role);
            if (reset && !initialized)
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ExperimentNotInitialized, role: role);
            if (!Flush(role, out failure))
                return failure;
            if (!TryLoad(state.Main, out CharacterTestRuntimeAiLabDraftWorkspace main, out string message))
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainUnavailable, message, role);
            if (main.IsReadOnly)
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ReadOnly, role: role);

            if (!_persistence.TryCloneWorkspace(
                    main.WorkspaceId,
                    Guid.NewGuid().ToString("D"),
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.Get(role).DisplayName,
                    0,
                    out CharacterTestRuntimeAiLabDraftWorkspace experiment,
                    out message))
            {
                return Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed, message, role);
            }

            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState next = state.Clone();
            next.Set(role, new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry(
                experiment.WorkspaceId,
                main.WorkspaceId,
                main.CurrentRevision,
                DateTime.UtcNow));
            if (!_registry.Save(next, out message))
            {
                return FailAfterOrphanCleanup(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed,
                    message,
                    role,
                    experiment.WorkspaceId,
                    state);
            }

            _state = next;
            if (_activeRole == role)
                Activate(next, role, experiment, message);
            return Ok(role, experiment, reset ? "Reset the experiment from Main." : "Copied Main to the experiment.");
        }

        private bool ReloadAndValidate(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole operationRole,
            bool requireActiveSession,
            out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState state,
            out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult failure)
        {
            if (!Enabled)
            {
                state = null;
                failure = Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.DisabledForIsolatedRoot, role: operationRole);
                return false;
            }
            if (_registry.Load(out state, out string message)
                != CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus.Loaded)
            {
                failure = Fail(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.RegistryInvalid, message, operationRole);
                return false;
            }
            if (!_hasActiveRole)
            {
                if (!requireActiveSession)
                {
                    failure = default;
                    return true;
                }
                failure = Fail(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed,
                    "No active authoring workspace session is available.",
                    operationRole);
                return false;
            }

            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry active = state.Get(_activeRole);
            if (active == null
                || _persistence.Workspace == null
                || !string.Equals(active.WorkspaceId, _persistence.Workspace.WorkspaceId, StringComparison.Ordinal))
            {
                failure = Fail(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.RegistryInvalid,
                    "The role registry changed externally; reopen Runtime AI Lab.",
                    operationRole);
                return false;
            }
            failure = default;
            return true;
        }

        private bool Flush(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole operationRole,
            out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult failure)
        {
            _persistence.SetLatestWorkspacePointerUpdateEnabled(
                _activeRole == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main);
            if (!_persistence.Flush())
            {
                failure = Fail(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.FlushFailed,
                    _persistence.LastStatus,
                    operationRole);
                return false;
            }
            failure = default;
            return true;
        }

        private bool TryLoad(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry entry,
            out CharacterTestRuntimeAiLabDraftWorkspace workspace,
            out string message)
        {
            workspace = null;
            if (entry == null)
            {
                message = "The workspace role mapping is missing.";
                return false;
            }
            return _persistence.TryLoad(entry.WorkspaceId, out workspace, out message);
        }

        private void Activate(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState state,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            string message)
        {
            _state = state;
            _activeRole = role;
            _hasActiveRole = true;
            _persistence.SetLatestWorkspacePointerUpdateEnabled(
                role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main);
            _persistence.ActivateLoadedWorkspace(workspace, message);
        }

        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult FailAfterOrphanCleanup(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus status,
            string message,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            string orphanWorkspaceId,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState referencedState)
        {
            if (!_persistence.TryDeleteUnreferencedWorkspace(
                    orphanWorkspaceId,
                    referencedState.WorkspaceIds(),
                    out string cleanupMessage))
            {
                message += " " + cleanupMessage;
            }
            return Fail(status, message, role);
        }

        private static CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult Ok(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            string message)
        {
            return new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult(
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.Succeeded,
                role,
                workspace,
                message);
        }

        private static CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult Fail(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus status,
            string message = null,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role =
                CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main)
        {
            return new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult(
                status,
                role,
                null,
                string.IsNullOrWhiteSpace(message) ? status.ToString() : message);
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState
    {
        NotCreated = 0,
        Saved = 1,
        Dirty = 2,
        SaveFailed = 3,
        Unavailable = 4
    }

    internal readonly struct CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRow
    {
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRow(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            string displayName,
            string roleKindText,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState state,
            string stateText,
            bool isCurrent,
            bool canSelect,
            bool canCopyFromMain,
            bool canReset,
            int revision,
            DateTime? updatedUtc,
            int sourceMainRevision,
            DateTime? copiedUtc,
            string sourceSummaryText,
            string diagnostic,
            string technicalWorkspaceId)
        {
            Role = role;
            DisplayName = displayName ?? string.Empty;
            RoleKindText = roleKindText ?? string.Empty;
            State = state;
            StateText = stateText ?? string.Empty;
            IsCurrent = isCurrent;
            CanSelect = canSelect;
            CanCopyFromMain = canCopyFromMain;
            CanReset = canReset;
            Revision = Math.Max(0, revision);
            UpdatedUtc = updatedUtc;
            SourceMainRevision = Math.Max(0, sourceMainRevision);
            CopiedUtc = copiedUtc;
            SourceSummaryText = sourceSummaryText ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
            TechnicalWorkspaceId = technicalWorkspaceId ?? string.Empty;
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRole Role { get; }
        internal string DisplayName { get; }
        internal string RoleKindText { get; }
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState State { get; }
        internal string StateText { get; }
        internal bool IsCurrent { get; }
        internal bool CanSelect { get; }
        internal bool CanCopyFromMain { get; }
        internal bool CanReset { get; }
        internal int Revision { get; }
        internal DateTime? UpdatedUtc { get; }
        internal int SourceMainRevision { get; }
        internal DateTime? CopiedUtc { get; }
        internal string SourceSummaryText { get; }
        internal string Diagnostic { get; }
        internal string TechnicalWorkspaceId { get; }
    }

    internal sealed class CharacterTestRuntimeAiLabAuthoringWorkspaceSelectorSnapshot
    {
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceSelectorSnapshot(
            bool available,
            bool hasActiveRole,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole activeRole,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRow[] rows,
            string diagnostic)
        {
            Available = available;
            HasActiveRole = hasActiveRole;
            ActiveRole = activeRole;
            Rows = rows ?? Array.Empty<CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRow>();
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal bool Available { get; }
        internal bool HasActiveRole { get; }
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRole ActiveRole { get; }
        internal IReadOnlyList<CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRow> Rows { get; }
        internal string Diagnostic { get; }
    }

    /// <summary>
    /// Editor-only backend for the fixed authoring workspace picker. The picker never accepts
    /// a workspace ID or path; the only selectable identity is the fixed role enum.
    /// </summary>
    internal sealed partial class CharacterTestRuntimeAiLabAuthoringWorkspaceSelector
    {
        private readonly CharacterTestRuntimeAiLabAuthoringWorkspaceRoles _roles;
        private readonly CharacterTestRuntimeAiLabDraftPersistence _persistence;

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceSelector(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoles roles,
            CharacterTestRuntimeAiLabDraftPersistence persistence)
        {
            _roles = roles ?? throw new ArgumentNullException(nameof(roles));
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        /// <remarks>LowFreqAlloc: popup open or explicit refresh only.</remarks>
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceSelectorSnapshot Capture(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult lastOperation)
        {
            IReadOnlyList<CharacterTestRuntimeAiLabAuthoringWorkspaceRoleDefinition> definitions =
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.All;
            var rows = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRow[definitions.Count];
            bool anyAvailable = false;
            for (int i = 0; i < definitions.Count; i++)
            {
                rows[i] = BuildRow(definitions[i], lastOperation);
                anyAvailable |= rows[i].State !=
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState.Unavailable;
            }

            string diagnostic = _roles.Enabled
                ? string.Empty
                : "Authoring workspace roles are disabled for this isolated persistence root.";
            return new CharacterTestRuntimeAiLabAuthoringWorkspaceSelectorSnapshot(
                _roles.Enabled && anyAvailable,
                _roles.HasActiveRole,
                _roles.HasActiveRole
                    ? _roles.ActiveRole
                    : CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main,
                rows,
                diagnostic);
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult Select(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            if (!CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsValid(role))
            {
                return Failure(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.InvalidRole,
                    role,
                    "The workspace selector accepts only Main, ExperimentA, or ExperimentB.");
            }
            if (_roles.HasActiveRole
                && _roles.ActiveRole == role
                && _persistence.Workspace != null)
            {
                if (_persistence.Workspace.RecoveredFromPrevious)
                {
                    return Failure(
                        role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                            ? CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainUnavailable
                            : CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed,
                        role,
                        "The active workspace snapshot is damaged and was opened from its previous recovery copy.");
                }
                if (_persistence.Workspace.IsReadOnly)
                {
                    return Failure(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ReadOnly,
                        role,
                        "The active workspace uses a newer read-only schema.");
                }
                return new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.Succeeded,
                    role,
                    _persistence.Workspace,
                    "The requested authoring workspace role is already active.");
            }

            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult inspection =
                _roles.InspectSelectionTarget(role, out _);
            if (!inspection.Success)
                return inspection;
            return _roles.Switch(role);
        }

        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRow BuildRow(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleDefinition definition,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult lastOperation)
        {
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role = definition.Role;
            bool isCurrent = _roles.HasActiveRole && _roles.ActiveRole == role;
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry entry = null;
            CharacterTestRuntimeAiLabDraftWorkspace workspace = null;
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult inspection = default;

            if (isCurrent && _persistence.Workspace != null)
            {
                workspace = _persistence.Workspace;
                _roles.TryGetEntry(role, out entry);
                if (workspace.RecoveredFromPrevious || workspace.IsReadOnly)
                {
                    inspection = Failure(
                        workspace.IsReadOnly
                            ? CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ReadOnly
                            : role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                                ? CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainUnavailable
                                : CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed,
                        role,
                        workspace.IsReadOnly
                            ? "The active workspace uses a newer read-only schema."
                            : "The active workspace snapshot is damaged and was opened from its previous recovery copy.");
                    workspace = null;
                }
                else
                {
                    inspection = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.Succeeded,
                        role,
                        workspace,
                        string.Empty);
                }
            }
            else
            {
                inspection = _roles.InspectSelectionTarget(role, out entry);
                if (inspection.Success)
                    workspace = inspection.Workspace;
            }

            bool initialized = entry != null;
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState state;
            string stateText;
            string diagnostic = inspection.Message;
            if (!initialized
                && CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsExperiment(role)
                && inspection.Status ==
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ExperimentNotInitialized)
            {
                state = CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState.NotCreated;
                stateText = "未创建";
                diagnostic = string.Empty;
            }
            else if (isCurrent && IsSaveFailure(lastOperation.Status))
            {
                state = CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState.SaveFailed;
                stateText = "保存失败";
                diagnostic = lastOperation.Message;
            }
            else if (workspace == null)
            {
                state = CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState.Unavailable;
                stateText = "无法打开";
            }
            else if (workspace.Dirty)
            {
                state = CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState.Dirty;
                stateText = "有修改";
                diagnostic = string.Empty;
            }
            else
            {
                state = CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRowState.Saved;
                stateText = "已保存";
                diagnostic = string.Empty;
            }

            bool experiment = CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsExperiment(role);
            string sourceSummary = entry != null && experiment
                ? "来源 Main r" + entry.SourceMainRevision
                : string.Empty;
            return new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRow(
                role,
                DisplayName(role),
                experiment ? "测试工作区" : "主工作区",
                state,
                stateText,
                isCurrent,
                !isCurrent && workspace != null && inspection.Success,
                experiment && !initialized,
                experiment && initialized && isCurrent,
                workspace?.CurrentRevision ?? 0,
                workspace?.UpdatedUtc,
                entry?.SourceMainRevision ?? 0,
                entry?.CopiedUtc,
                sourceSummary,
                diagnostic,
                entry?.WorkspaceId ?? string.Empty);
        }

        private static string DisplayName(CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                ? "主工作区"
                : role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA
                    ? "实验 A"
                    : "实验 B";
        }

        private static bool IsSaveFailure(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus status)
        {
            return status == CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.FlushFailed
                || status == CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.StateSaveFailed
                || status == CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.RollbackFailed;
        }

        private static CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult Failure(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus status,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            string message)
        {
            return new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult(
                status,
                role,
                null,
                message);
        }
    }
}
#endif

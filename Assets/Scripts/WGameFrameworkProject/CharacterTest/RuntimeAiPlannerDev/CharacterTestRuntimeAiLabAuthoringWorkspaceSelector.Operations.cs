#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview
    {
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            bool available,
            bool initialized,
            bool mainMatchesSource,
            int mainRevision,
            int sourceMainRevision,
            int experimentRevision,
            int mainGroupCount,
            int mainCardCount,
            int changedGroupCount,
            int changedCardCount,
            bool experimentDirty,
            string diagnostic)
        {
            Role = role;
            Available = available;
            Initialized = initialized;
            MainMatchesSource = mainMatchesSource;
            MainRevision = Math.Max(0, mainRevision);
            SourceMainRevision = Math.Max(0, sourceMainRevision);
            ExperimentRevision = Math.Max(0, experimentRevision);
            MainGroupCount = Math.Max(0, mainGroupCount);
            MainCardCount = Math.Max(0, mainCardCount);
            ChangedGroupCount = Math.Max(0, changedGroupCount);
            ChangedCardCount = Math.Max(0, changedCardCount);
            ExperimentDirty = experimentDirty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRole Role { get; }
        internal bool Available { get; }
        internal bool Initialized { get; }
        internal bool MainMatchesSource { get; }
        internal int MainRevision { get; }
        internal int SourceMainRevision { get; }
        internal int ExperimentRevision { get; }
        internal int MainGroupCount { get; }
        internal int MainCardCount { get; }
        internal int ChangedGroupCount { get; }
        internal int ChangedCardCount { get; }
        internal bool ExperimentDirty { get; }
        internal string Diagnostic { get; }
    }

    internal sealed partial class CharacterTestRuntimeAiLabAuthoringWorkspaceSelector
    {
        /// <remarks>LowFreqAlloc: explicit workspace-picker refresh or confirmation only.</remarks>
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview CaptureExperimentPreview(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            if (!CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsExperiment(role))
            {
                return UnavailablePreview(
                    role,
                    "The experiment preview accepts only ExperimentA or ExperimentB.");
            }

            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult mainInspection =
                ResolveWorkspace(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry mainEntry,
                    out CharacterTestRuntimeAiLabDraftWorkspace main);
            if (!mainInspection.Success || mainEntry == null || main == null)
                return UnavailablePreview(role, mainInspection.Message);

            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult experimentInspection =
                ResolveWorkspace(
                    role,
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry experimentEntry,
                    out CharacterTestRuntimeAiLabDraftWorkspace experiment);
            if (experimentInspection.Status ==
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ExperimentNotInitialized)
            {
                return new CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview(
                    role: role,
                    available: true,
                    initialized: false,
                    mainMatchesSource: true,
                    mainRevision: main.CurrentRevision,
                    sourceMainRevision: 0,
                    experimentRevision: 0,
                    mainGroupCount: main.Groups.Count,
                    mainCardCount: main.Cards.Count,
                    changedGroupCount: 0,
                    changedCardCount: 0,
                    experimentDirty: false,
                    diagnostic: string.Empty);
            }
            if (experimentEntry != null && (!experimentInspection.Success || experiment == null))
            {
                bool sourceStillMatches = string.Equals(
                        experimentEntry.SourceMainWorkspaceId,
                        main.WorkspaceId,
                        StringComparison.Ordinal)
                    && experimentEntry.SourceMainRevision == main.CurrentRevision;
                return new CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview(
                    role: role,
                    available: true,
                    initialized: true,
                    mainMatchesSource: sourceStillMatches,
                    mainRevision: main.CurrentRevision,
                    sourceMainRevision: experimentEntry.SourceMainRevision,
                    experimentRevision: 0,
                    mainGroupCount: main.Groups.Count,
                    mainCardCount: main.Cards.Count,
                    changedGroupCount: 0,
                    changedCardCount: 0,
                    experimentDirty: false,
                    diagnostic: "当前实验无法读取；可以用主工作区副本覆盖恢复。 "
                        + experimentInspection.Message);
            }
            if (!experimentInspection.Success || experimentEntry == null || experiment == null)
                return UnavailablePreview(role, experimentInspection.Message);

            bool mainMatchesSource = string.Equals(
                    experimentEntry.SourceMainWorkspaceId,
                    main.WorkspaceId,
                    StringComparison.Ordinal)
                && experimentEntry.SourceMainRevision == main.CurrentRevision;
            return new CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview(
                role: role,
                available: true,
                initialized: true,
                mainMatchesSource: mainMatchesSource,
                mainRevision: main.CurrentRevision,
                sourceMainRevision: experimentEntry.SourceMainRevision,
                experimentRevision: experiment.CurrentRevision,
                mainGroupCount: main.Groups.Count,
                mainCardCount: main.Cards.Count,
                changedGroupCount: CountChangedGroups(main.Groups, experiment.Groups),
                changedCardCount: CountChangedCards(main.Cards, experiment.Cards),
                experimentDirty: experiment.Dirty,
                diagnostic: mainMatchesSource
                    ? string.Empty
                    : "Main changed after this experiment was copied.");
        }

        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult ResolveWorkspace(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry entry,
            out CharacterTestRuntimeAiLabDraftWorkspace workspace)
        {
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult inspection =
                _roles.InspectSelectionTarget(role, out entry);
            workspace = inspection.Workspace;
            if (_roles.HasActiveRole
                && _roles.ActiveRole == role
                && _persistence.Workspace != null
                && !_persistence.Workspace.IsReadOnly
                && !_persistence.Workspace.RecoveredFromPrevious)
            {
                workspace = _persistence.Workspace;
                if (entry != null)
                {
                    inspection = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.Succeeded,
                        role,
                        workspace,
                        string.Empty);
                }
            }
            return inspection;
        }

        private static int CountChangedGroups(
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorGroupDraft> main,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorGroupDraft> experiment)
        {
            int changed = 0;
            foreach (KeyValuePair<string, CharacterTestRuntimeAiLabBehaviorGroupDraft> pair in experiment)
            {
                if (!main.TryGetValue(pair.Key, out CharacterTestRuntimeAiLabBehaviorGroupDraft value)
                    || value == null
                    || pair.Value == null
                    || value.CurrentRevision != pair.Value.CurrentRevision)
                {
                    changed++;
                }
            }
            foreach (string key in main.Keys)
            {
                if (!experiment.ContainsKey(key))
                    changed++;
            }
            return changed;
        }

        private static int CountChangedCards(
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> main,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> experiment)
        {
            int changed = 0;
            foreach (KeyValuePair<string, CharacterTestRuntimeAiLabBehaviorCardDraft> pair in experiment)
            {
                if (!main.TryGetValue(pair.Key, out CharacterTestRuntimeAiLabBehaviorCardDraft value)
                    || value == null
                    || pair.Value == null
                    || value.CurrentRevision != pair.Value.CurrentRevision)
                {
                    changed++;
                }
            }
            foreach (string key in main.Keys)
            {
                if (!experiment.ContainsKey(key))
                    changed++;
            }
            return changed;
        }

        private static CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview UnavailablePreview(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            string diagnostic)
        {
            return new CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview(
                role: role,
                available: false,
                initialized: false,
                mainMatchesSource: false,
                mainRevision: 0,
                sourceMainRevision: 0,
                experimentRevision: 0,
                mainGroupCount: 0,
                mainCardCount: 0,
                changedGroupCount: 0,
                changedCardCount: 0,
                experimentDirty: false,
                diagnostic: diagnostic);
        }
    }
}
#endif

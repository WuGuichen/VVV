using System;
using System.Collections.Generic;

namespace MxFramework.CharacterAction
{
    public sealed class CharacterActionConfigRegistry
    {
        public CharacterActionConfigRegistry(
            CharacterActionSetConfig[] actionSets,
            CharacterActionConfig[] actions,
            string actionSetsSourcePath = CharacterActionJsonImporter.DefaultActionSetsSourcePath,
            string actionsSourcePath = CharacterActionJsonImporter.DefaultActionsSourcePath,
            CharacterReactionProfile[] reactionProfiles = null,
            string reactionProfilesSourcePath = CharacterActionJsonImporter.DefaultReactionProfilesSourcePath,
            CharacterLandingProfile[] landingProfiles = null,
            string landingProfilesSourcePath = CharacterActionJsonImporter.DefaultLandingProfilesSourcePath)
        {
            ActionSetsSourcePath = string.IsNullOrWhiteSpace(actionSetsSourcePath)
                ? CharacterActionJsonImporter.DefaultActionSetsSourcePath
                : actionSetsSourcePath;
            ActionsSourcePath = string.IsNullOrWhiteSpace(actionsSourcePath)
                ? CharacterActionJsonImporter.DefaultActionsSourcePath
                : actionsSourcePath;
            ReactionProfilesSourcePath = string.IsNullOrWhiteSpace(reactionProfilesSourcePath)
                ? CharacterActionJsonImporter.DefaultReactionProfilesSourcePath
                : reactionProfilesSourcePath;
            LandingProfilesSourcePath = string.IsNullOrWhiteSpace(landingProfilesSourcePath)
                ? CharacterActionJsonImporter.DefaultLandingProfilesSourcePath
                : landingProfilesSourcePath;

            ActionSets = CopyAndSort(actionSets, CompareActionSets);
            Actions = CopyAndSort(actions, CompareActions);
            ReactionProfiles = CopyAndSort(reactionProfiles, CompareReactionProfiles);
            LandingProfiles = CopyAndSort(landingProfiles, CompareLandingProfiles);

            var diagnostics = new List<CharacterActionValidationDiagnostic>();
            _actionSetsByStableId = BuildActionSetLookup(ActionSets, diagnostics);
            _reactionProfilesByStableId = BuildReactionProfileLookup(ReactionProfiles, diagnostics);
            _landingProfilesByStableId = BuildLandingProfileLookup(LandingProfiles, diagnostics);
            _actionSetsById = BuildActionSetIdLookup(ActionSets, diagnostics);
            _actionsById = BuildActionIdLookup(Actions, diagnostics);

            Diagnostics = diagnostics.ToArray();
            Array.Sort(Diagnostics, CharacterActionValidationDiagnostic.Compare);
        }

        public CharacterActionSetConfig[] ActionSets { get; }
        public CharacterActionConfig[] Actions { get; }
        public CharacterReactionProfile[] ReactionProfiles { get; }
        public CharacterLandingProfile[] LandingProfiles { get; }
        public CharacterActionValidationDiagnostic[] Diagnostics { get; }
        public string ActionSetsSourcePath { get; }
        public string ActionsSourcePath { get; }
        public string ReactionProfilesSourcePath { get; }
        public string LandingProfilesSourcePath { get; }

        private readonly Dictionary<string, CharacterActionSetConfig> _actionSetsByStableId;
        private readonly Dictionary<string, CharacterReactionProfile> _reactionProfilesByStableId;
        private readonly Dictionary<string, CharacterLandingProfile> _landingProfilesByStableId;
        private readonly Dictionary<int, CharacterActionSetConfig> _actionSetsById;
        private readonly Dictionary<CharacterActionId, CharacterActionConfig> _actionsById;

        public bool TryGetActionSet(string stableId, out CharacterActionSetConfig actionSet)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                actionSet = null;
                return false;
            }

            return _actionSetsByStableId.TryGetValue(stableId, out actionSet);
        }

        public bool TryGetActionSet(int id, out CharacterActionSetConfig actionSet)
        {
            return _actionSetsById.TryGetValue(id, out actionSet);
        }

        public bool TryGetAction(CharacterActionId actionId, out CharacterActionConfig action)
        {
            if (!actionId.IsValid)
            {
                action = null;
                return false;
            }

            return _actionsById.TryGetValue(actionId, out action);
        }

        public bool TryGetReactionProfile(string stableId, out CharacterReactionProfile profile)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                profile = null;
                return false;
            }

            return _reactionProfilesByStableId.TryGetValue(stableId, out profile);
        }

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetLandingProfile(string stableId, out CharacterLandingProfile profile)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                profile = null;
                return false;
            }

            return _landingProfilesByStableId.TryGetValue(stableId, out profile);
        }

        private static T[] CopyAndSort<T>(T[] values, Comparison<T> comparison)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<T>();

            var copy = new T[values.Length];
            Array.Copy(values, copy, values.Length);
            Array.Sort(copy, comparison);
            return copy;
        }

        private Dictionary<string, CharacterActionSetConfig> BuildActionSetLookup(
            CharacterActionSetConfig[] actionSets,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            var lookup = new Dictionary<string, CharacterActionSetConfig>(StringComparer.Ordinal);
            for (int i = 0; i < actionSets.Length; i++)
            {
                CharacterActionSetConfig actionSet = actionSets[i];
                string path = "actionSets[" + i + "]";
                if (actionSet == null)
                    continue;

                if (string.IsNullOrWhiteSpace(actionSet.StableId))
                {
                    diagnostics.Add(Error(
                        ActionSetsSourcePath,
                        path + ".stableId",
                        CharacterActionDiagnosticCodes.JsonInvalidConfig,
                        "Action set stable id must be non-empty."));
                    continue;
                }

                if (lookup.ContainsKey(actionSet.StableId))
                {
                    diagnostics.Add(Error(
                        ActionSetsSourcePath,
                        path + ".stableId",
                        CharacterActionDiagnosticCodes.ConfigDuplicateStableId,
                        "Duplicate action set stable id '" + actionSet.StableId + "'."));
                    continue;
                }

                lookup.Add(actionSet.StableId, actionSet);
            }

            return lookup;
        }

        private Dictionary<string, CharacterReactionProfile> BuildReactionProfileLookup(
            CharacterReactionProfile[] profiles,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            var lookup = new Dictionary<string, CharacterReactionProfile>(StringComparer.Ordinal);
            for (int i = 0; i < profiles.Length; i++)
            {
                CharacterReactionProfile profile = profiles[i];
                if (profile == null || string.IsNullOrEmpty(profile.StableId))
                    continue;

                if (lookup.ContainsKey(profile.StableId))
                {
                    diagnostics.Add(new CharacterActionValidationDiagnostic(
                        ReactionProfilesSourcePath,
                        "reactionProfiles[" + profile.StableId + "]",
                        CharacterActionDiagnosticCodes.JsonDuplicateStableId,
                        CharacterActionDiagnosticSeverity.Error,
                        "Duplicate reaction profile stable id '" + profile.StableId + "'."));
                    continue;
                }

                lookup.Add(profile.StableId, profile);
            }

            return lookup;
        }

        private Dictionary<string, CharacterLandingProfile> BuildLandingProfileLookup(
            CharacterLandingProfile[] profiles,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            var lookup = new Dictionary<string, CharacterLandingProfile>(StringComparer.Ordinal);
            for (int i = 0; i < profiles.Length; i++)
            {
                CharacterLandingProfile profile = profiles[i];
                if (profile == null || string.IsNullOrEmpty(profile.StableId))
                    continue;

                if (lookup.ContainsKey(profile.StableId))
                {
                    diagnostics.Add(new CharacterActionValidationDiagnostic(
                        LandingProfilesSourcePath,
                        "landingProfiles[" + profile.StableId + "]",
                        CharacterActionDiagnosticCodes.JsonDuplicateStableId,
                        CharacterActionDiagnosticSeverity.Error,
                        "Duplicate landing profile stable id '" + profile.StableId + "'."));
                    continue;
                }

                lookup.Add(profile.StableId, profile);
            }

            return lookup;
        }

        private Dictionary<int, CharacterActionSetConfig> BuildActionSetIdLookup(
            CharacterActionSetConfig[] actionSets,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            var lookup = new Dictionary<int, CharacterActionSetConfig>();
            for (int i = 0; i < actionSets.Length; i++)
            {
                CharacterActionSetConfig actionSet = actionSets[i];
                string path = "actionSets[" + i + "]";
                if (actionSet == null)
                    continue;

                if (actionSet.Id <= 0)
                {
                    diagnostics.Add(Error(
                        ActionSetsSourcePath,
                        path + ".id",
                        CharacterActionDiagnosticCodes.JsonInvalidConfig,
                        "Action set numeric id must be positive."));
                    continue;
                }

                if (lookup.ContainsKey(actionSet.Id))
                {
                    diagnostics.Add(Error(
                        ActionSetsSourcePath,
                        path + ".id",
                        CharacterActionDiagnosticCodes.ConfigDuplicateId,
                        "Duplicate action set numeric id " + actionSet.Id + "."));
                    continue;
                }

                lookup.Add(actionSet.Id, actionSet);
            }

            return lookup;
        }

        private Dictionary<CharacterActionId, CharacterActionConfig> BuildActionIdLookup(
            CharacterActionConfig[] actions,
            List<CharacterActionValidationDiagnostic> diagnostics)
        {
            var lookup = new Dictionary<CharacterActionId, CharacterActionConfig>();
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                string path = "actions[" + i + "]";
                if (action == null)
                    continue;

                if (!action.ActionId.IsValid)
                {
                    diagnostics.Add(Error(
                        ActionsSourcePath,
                        path + ".actionId",
                        CharacterActionDiagnosticCodes.JsonInvalidConfig,
                        "Action numeric id must be positive."));
                    continue;
                }

                if (lookup.ContainsKey(action.ActionId))
                {
                    diagnostics.Add(Error(
                        ActionsSourcePath,
                        path + ".actionId",
                        CharacterActionDiagnosticCodes.ConfigDuplicateId,
                        "Duplicate action numeric id " + action.ActionId.Value + "."));
                    continue;
                }

                lookup.Add(action.ActionId, action);
            }

            return lookup;
        }

        private static CharacterActionValidationDiagnostic Error(string sourcePath, string stablePath, string code, string message)
        {
            return new CharacterActionValidationDiagnostic(
                sourcePath,
                stablePath,
                code,
                CharacterActionDiagnosticSeverity.Error,
                message);
        }

        private static int CompareActionSets(CharacterActionSetConfig left, CharacterActionSetConfig right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int compare = string.CompareOrdinal(left.StableId, right.StableId);
            return compare != 0 ? compare : left.Id.CompareTo(right.Id);
        }

        private static int CompareActions(CharacterActionConfig left, CharacterActionConfig right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return left.ActionId.CompareTo(right.ActionId);
        }

        private static int CompareReactionProfiles(CharacterReactionProfile left, CharacterReactionProfile right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return string.CompareOrdinal(left.StableId, right.StableId);
        }

        private static int CompareLandingProfiles(CharacterLandingProfile left, CharacterLandingProfile right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return string.CompareOrdinal(left.StableId, right.StableId);
        }
    }
}

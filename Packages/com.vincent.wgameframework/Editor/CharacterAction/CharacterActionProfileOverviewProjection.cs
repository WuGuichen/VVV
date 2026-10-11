using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed class CharacterActionProfileDirectReference
    {
        internal CharacterActionProfileDirectReference(int actionId, string sourceLabel)
        {
            ActionId = actionId;
            SourceLabel = sourceLabel ?? string.Empty;
        }

        internal int ActionId { get; }
        internal string SourceLabel { get; }
    }

    internal sealed class CharacterActionProfileActionSetSummary
    {
        internal CharacterActionSetJsonDto ActionSet { get; set; }
        internal CharacterReactionProfileJsonDto ReactionProfile { get; set; }
        internal CharacterActionProfileDirectReference[] DirectReferences { get; set; } =
            Array.Empty<CharacterActionProfileDirectReference>();
        internal int[] ReferencedActionIds { get; set; } = Array.Empty<int>();
        internal int MissingActionCount { get; set; }
        internal bool IsSelected { get; set; }

        internal int CommandBindingCount => ActionSet?.CommandBindings?.Length ?? 0;
        internal int AbilityBindingCount => ActionSet?.AbilityBindings?.Length ?? 0;
        internal bool HasDefaultAction => (ActionSet?.DefaultActionId ?? 0) > 0;
        internal bool HasReactionProfile => ReactionProfile != null;
        internal bool IsHealthy => HasReactionProfile && MissingActionCount == 0;
    }

    internal sealed class CharacterActionProfileLibrarySummary
    {
        internal CharacterActionAuthoringActionLibraryDocument Library { get; set; }
        internal CharacterActionProfileDirectReference[] DirectReferences { get; set; } =
            Array.Empty<CharacterActionProfileDirectReference>();
        internal bool IsSelected { get; set; }
        internal bool IsCommon { get; set; }

        internal int ActionCount => Library?.Document.Actions?.Length ?? 0;
    }

    internal sealed class CharacterActionProfileOverviewProjection
    {
        internal CharacterActionProfileJsonDto Profile { get; set; }
        internal CharacterActionProfileActionSetSummary[] ActionSets { get; set; } =
            Array.Empty<CharacterActionProfileActionSetSummary>();
        internal CharacterActionProfileLibrarySummary[] Libraries { get; set; } =
            Array.Empty<CharacterActionProfileLibrarySummary>();
        internal CharacterActionSetJsonDto[] AvailableActionSets { get; set; } =
            Array.Empty<CharacterActionSetJsonDto>();
        internal CharacterActionProfileActionSetSummary SelectedActionSet { get; set; }

        internal static CharacterActionProfileOverviewProjection Build(
            CharacterActionAuthoringDocumentSession session,
            CharacterActionProfileJsonDto fallbackProfile = null,
            CharacterActionSetJsonDto fallbackActionSet = null)
        {
            CharacterActionProfileJsonDto profile = session?.CurrentProfile ?? fallbackProfile
                ?? new CharacterActionProfileJsonDto();
            var result = new CharacterActionProfileOverviewProjection { Profile = profile };
            if (session == null)
            {
                CharacterActionProfileActionSetSummary fallback = BuildActionSetSummary(
                    null,
                    fallbackActionSet,
                    null,
                    string.Empty);
                result.ActionSets = fallback == null
                    ? Array.Empty<CharacterActionProfileActionSetSummary>()
                    : new[] { fallback };
                result.SelectedActionSet = fallback;
                return result;
            }

            string[] memberIds = profile.ActionSetIds ?? Array.Empty<string>();
            var summaries = new List<CharacterActionProfileActionSetSummary>(memberIds.Length);
            var available = new List<CharacterActionSetJsonDto>();
            for (int bundleIndex = 0; bundleIndex < session.ActionSetBundles.Count; bundleIndex++)
            {
                CharacterActionAuthoringActionSetBundleDocument bundle =
                    session.ActionSetBundles[bundleIndex];
                CharacterActionSetJsonDto[] actionSets = bundle?.ActionSetsDocument.ActionSets
                    ?? Array.Empty<CharacterActionSetJsonDto>();
                for (int setIndex = 0; setIndex < actionSets.Length; setIndex++)
                {
                    CharacterActionSetJsonDto actionSet = actionSets[setIndex];
                    if (actionSet == null || string.IsNullOrWhiteSpace(actionSet.StableId))
                        continue;
                    if (!Contains(memberIds, actionSet.StableId))
                    {
                        available.Add(actionSet);
                        continue;
                    }

                    CharacterReactionProfileJsonDto reaction = FindReactionProfile(bundle, actionSet.ReactionProfileId);
                    CharacterActionProfileActionSetSummary summary = BuildActionSetSummary(
                        session,
                        actionSet,
                        reaction,
                        session.Selection.ActionSetStableId);
                    summaries.Add(summary);
                    if (summary.IsSelected)
                        result.SelectedActionSet = summary;
                }
            }

            result.ActionSets = summaries.ToArray();
            result.AvailableActionSets = available.ToArray();
            if (result.SelectedActionSet == null && result.ActionSets.Length > 0)
                result.SelectedActionSet = result.ActionSets[0];
            result.Libraries = BuildLibrarySummaries(session, profile, result.ActionSets);
            return result;
        }

        private static CharacterActionProfileActionSetSummary BuildActionSetSummary(
            CharacterActionAuthoringDocumentSession session,
            CharacterActionSetJsonDto actionSet,
            CharacterReactionProfileJsonDto reactionProfile,
            string selectedActionSetId)
        {
            if (actionSet == null)
                return null;

            CharacterActionProfileDirectReference[] references = CollectDirectReferences(actionSet, reactionProfile);
            var uniqueIds = new HashSet<int>();
            int missingCount = 0;
            for (int i = 0; i < references.Length; i++)
            {
                int actionId = references[i].ActionId;
                if (actionId <= 0 || !uniqueIds.Add(actionId))
                    continue;
                if (session != null && session.FindActionLibraryForAction(actionId) == null)
                    missingCount++;
            }

            var actionIds = new int[uniqueIds.Count];
            uniqueIds.CopyTo(actionIds);
            Array.Sort(actionIds);
            return new CharacterActionProfileActionSetSummary
            {
                ActionSet = actionSet,
                ReactionProfile = reactionProfile,
                DirectReferences = references,
                ReferencedActionIds = actionIds,
                MissingActionCount = missingCount,
                IsSelected = string.Equals(actionSet.StableId, selectedActionSetId, StringComparison.Ordinal),
            };
        }

        private static CharacterActionProfileLibrarySummary[] BuildLibrarySummaries(
            CharacterActionAuthoringDocumentSession session,
            CharacterActionProfileJsonDto profile,
            CharacterActionProfileActionSetSummary[] actionSets)
        {
            var result = new List<CharacterActionProfileLibrarySummary>();
            string[] memberIds = profile.ActionLibraryIds ?? Array.Empty<string>();
            for (int i = 0; i < session.ActionLibraries.Count; i++)
            {
                CharacterActionAuthoringActionLibraryDocument library = session.ActionLibraries[i];
                if (library == null || !Contains(memberIds, library.LibraryId))
                    continue;

                var libraryReferences = new List<CharacterActionProfileDirectReference>();
                for (int actionSetIndex = 0; actionSetIndex < actionSets.Length; actionSetIndex++)
                {
                    CharacterActionProfileDirectReference[] references = actionSets[actionSetIndex].DirectReferences;
                    for (int referenceIndex = 0; referenceIndex < references.Length; referenceIndex++)
                    {
                        CharacterActionAuthoringActionLibraryDocument owner =
                            session.FindActionLibraryForAction(references[referenceIndex].ActionId);
                        if (ReferenceEquals(owner, library))
                            libraryReferences.Add(new CharacterActionProfileDirectReference(
                                references[referenceIndex].ActionId,
                                (actionSets[actionSetIndex].ActionSet.DisplayName
                                    ?? actionSets[actionSetIndex].ActionSet.StableId)
                                + " · " + references[referenceIndex].SourceLabel));
                    }
                }

                result.Add(new CharacterActionProfileLibrarySummary
                {
                    Library = library,
                    DirectReferences = libraryReferences.ToArray(),
                    IsSelected = string.Equals(
                        library.LibraryId,
                        session.Selection.ActionLibraryId,
                        StringComparison.Ordinal),
                    IsCommon = string.Equals(library.LibraryId, "Common", StringComparison.OrdinalIgnoreCase),
                });
            }

            result.Sort((left, right) =>
            {
                int commonOrder = left.IsCommon.CompareTo(right.IsCommon);
                return commonOrder != 0
                    ? commonOrder
                    : string.Compare(left.Library.LibraryId, right.Library.LibraryId, StringComparison.Ordinal);
            });
            return result.ToArray();
        }

        private static CharacterActionProfileDirectReference[] CollectDirectReferences(
            CharacterActionSetJsonDto actionSet,
            CharacterReactionProfileJsonDto reactionProfile)
        {
            var result = new List<CharacterActionProfileDirectReference>();
            AddReference(result, actionSet.DefaultActionId, "默认动作");

            CharacterActionBindingJsonDto[] commands = actionSet.CommandBindings
                ?? Array.Empty<CharacterActionBindingJsonDto>();
            for (int i = 0; i < commands.Length; i++)
                AddReference(result, commands[i]?.ActionId ?? 0, "输入触发 · "
                    + CharacterActionAuthoringIds.Label(commands[i]?.IntentId ?? 0, 404));

            CharacterAbilityActionBindingJsonDto[] abilities = actionSet.AbilityBindings
                ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            for (int i = 0; i < abilities.Length; i++)
                AddReference(result, abilities[i]?.ActionId ?? 0, "技能触发 · "
                    + CharacterActionAuthoringIds.Label(abilities[i]?.AbilityId ?? 0, 404));

            if (reactionProfile != null)
            {
                AddReference(result, reactionProfile.DefaultActionId, "受击默认动作");
                CharacterReactionRuleJsonDto[] rules = reactionProfile.Rules
                    ?? Array.Empty<CharacterReactionRuleJsonDto>();
                for (int i = 0; i < rules.Length; i++)
                    AddReference(result, rules[i]?.ActionId ?? 0, "受击规则 · " + (rules[i]?.Trigger ?? string.Empty));
            }

            return result.ToArray();
        }

        private static void AddReference(
            List<CharacterActionProfileDirectReference> references,
            int actionId,
            string sourceLabel)
        {
            if (actionId > 0)
                references.Add(new CharacterActionProfileDirectReference(actionId, sourceLabel));
        }

        private static CharacterReactionProfileJsonDto FindReactionProfile(
            CharacterActionAuthoringActionSetBundleDocument bundle,
            string stableId)
        {
            CharacterReactionProfileJsonDto[] profiles = bundle?.ReactionProfilesDocument.ReactionProfiles
                ?? Array.Empty<CharacterReactionProfileJsonDto>();
            for (int i = 0; i < profiles.Length; i++)
            {
                if (string.Equals(profiles[i]?.StableId, stableId, StringComparison.Ordinal))
                    return profiles[i];
            }

            return null;
        }

        private static bool Contains(string[] values, string value)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}

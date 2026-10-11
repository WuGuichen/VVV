using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;

namespace MxFramework.Editor.CharacterAction
{
    internal static class CharacterActionAuthoringSearchCatalogBuilder
    {
        internal static CharacterActionAuthoringSearchCatalog BuildActions(
            CharacterActionAuthoringDocumentSession session,
            CharacterActionAuthoringModel model)
        {
            var candidates = new List<CharacterActionAuthoringSearchCandidate>();
            IReadOnlyList<CharacterActionAuthoringActionReference> references = session?.ProfileActions;
            if (references != null && references.Count > 0)
            {
                for (int i = 0; i < references.Count; i++)
                    AddActionCandidate(candidates, references[i]?.Action, references[i]?.LibraryId);
            }
            else
            {
                List<CharacterActionJsonDto> actions = model?.Actions;
                if (actions != null)
                {
                    for (int i = 0; i < actions.Count; i++)
                        AddActionCandidate(candidates, actions[i], "当前 Action Library");
                }
            }

            return new CharacterActionAuthoringSearchCatalog(candidates);
        }

        internal static CharacterActionAuthoringSearchCatalog BuildIntents(
            CharacterActionAuthoringDocumentSession session,
            CharacterActionAuthoringModel model)
        {
            var candidates = new List<CharacterActionAuthoringSearchCandidate>();
            Type inputIntentType = Type.GetType(
                "MxFramework.Input.InputIntent, MxFramework.Input",
                throwOnError: false);
            string[] enumNames = inputIntentType != null && inputIntentType.IsEnum
                ? Enum.GetNames(inputIntentType)
                : Array.Empty<string>();
            for (int i = 0; i < enumNames.Length; i++)
            {
                string value = enumNames[i];
                if (string.Equals(value, "Unknown", StringComparison.Ordinal))
                    continue;
                candidates.Add(new CharacterActionAuthoringSearchCandidate(
                    CharacterActionAuthoringSearchCategory.Intent,
                    value,
                    value,
                    "MxFramework.Input.InputIntent",
                    "Input enum"));
            }

            IReadOnlyList<CharacterActionAuthoringActionSetBundleDocument> bundles = session?.ActionSetBundles;
            if (bundles != null)
            {
                for (int i = 0; i < bundles.Count; i++)
                {
                    CharacterActionSetJsonDto[] sets = bundles[i]?.ActionSetsDocument.ActionSets
                        ?? Array.Empty<CharacterActionSetJsonDto>();
                    for (int j = 0; j < sets.Length; j++)
                        AddIntentBindings(candidates, sets[j]);
                }
            }

            AddIntentBindings(candidates, model?.ActionSet);
            return new CharacterActionAuthoringSearchCatalog(candidates);
        }

        internal static CharacterActionAuthoringSearchCatalog BuildAbilities(
            CharacterActionAuthoringDocumentSession session,
            CharacterActionAuthoringModel model,
            int[] effectiveAbilityIds)
        {
            var candidates = new List<CharacterActionAuthoringSearchCandidate>();
            effectiveAbilityIds = effectiveAbilityIds ?? Array.Empty<int>();
            for (int i = 0; i < effectiveAbilityIds.Length; i++)
            {
                if (effectiveAbilityIds[i] <= 0)
                    continue;
                string value = effectiveAbilityIds[i].ToString(CultureInfo.InvariantCulture);
                candidates.Add(new CharacterActionAuthoringSearchCandidate(
                    CharacterActionAuthoringSearchCategory.Ability,
                    value,
                    "Ability " + value,
                    "当前角色/装备已授予",
                    "Effective abilities"));
            }
            return new CharacterActionAuthoringSearchCatalog(candidates);
        }

        internal static CharacterActionAuthoringSearchCatalog BuildReactionProfiles(
            CharacterActionAuthoringDocumentSession session,
            CharacterActionAuthoringModel model)
        {
            var candidates = new List<CharacterActionAuthoringSearchCandidate>();
            if (session != null)
            {
                CharacterActionAuthoringActionSetBundleDocument bundle =
                    session.FindActionSetBundle(session.Selection.ActionSetStableId);
                CharacterReactionProfileJsonDto[] profiles =
                    bundle?.ReactionProfilesDocument.ReactionProfiles
                    ?? Array.Empty<CharacterReactionProfileJsonDto>();
                for (int i = 0; i < profiles.Length; i++)
                {
                    AddReactionProfileCandidate(
                        candidates,
                        profiles[i],
                        bundle?.Source.DisplayName);
                }

                return new CharacterActionAuthoringSearchCatalog(candidates);
            }

            if (model?.ReactionProfiles != null)
            {
                for (int i = 0; i < model.ReactionProfiles.Count; i++)
                    AddReactionProfileCandidate(candidates, model.ReactionProfiles[i], "当前 ActionSet bundle");
            }

            return new CharacterActionAuthoringSearchCatalog(candidates);
        }

        internal static CharacterActionAuthoringSearchCatalog BuildCharacters(
            CharacterActionPreviewBindingChoice[] discovered,
            CharacterActionAuthoringReferenceContext references)
        {
            return BuildPreviewReferences(
                CharacterActionAuthoringSearchCategory.Character,
                discovered,
                references?.CharacterStableIds,
                "Character reference");
        }

        internal static CharacterActionAuthoringSearchCatalog BuildEquipmentStates(
            CharacterActionPreviewBindingChoice[] discovered,
            CharacterActionAuthoringReferenceContext references)
        {
            return BuildPreviewReferences(
                CharacterActionAuthoringSearchCategory.EquipmentState,
                discovered,
                references?.EquipmentStateStableIds,
                "Equipment reference");
        }

        internal static CharacterActionAuthoringSearchCatalog BuildTags(
            CharacterActionAuthoringDocumentSession session,
            CharacterActionAuthoringModel model)
        {
            var candidates = new List<CharacterActionAuthoringSearchCandidate>();
            AddStringCandidates(
                candidates,
                CharacterActionAuthoringSearchCategory.Tag,
                model?.ReferenceContext?.EquipmentTags,
                "Equipment tag");

            IReadOnlyList<CharacterActionAuthoringActionSetBundleDocument> bundles = session?.ActionSetBundles;
            if (bundles != null)
            {
                for (int i = 0; i < bundles.Count; i++)
                {
                    CharacterActionSetJsonDto[] sets = bundles[i]?.ActionSetsDocument.ActionSets
                        ?? Array.Empty<CharacterActionSetJsonDto>();
                    for (int j = 0; j < sets.Length; j++)
                        AddBindingTags(candidates, sets[j], sets[j]?.StableId);
                }
            }
            AddBindingTags(candidates, model?.ActionSet, model?.ActionSet?.StableId);

            List<CharacterActionJsonDto> actions = model?.Actions;
            if (actions != null)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    CharacterActionJsonDto action = actions[i];
                    AddStringCandidates(
                        candidates,
                        CharacterActionAuthoringSearchCategory.Tag,
                        action?.Tags,
                        (action?.ActionId ?? 0).ToString(CultureInfo.InvariantCulture));
                }
            }
            return new CharacterActionAuthoringSearchCatalog(candidates);
        }

        private static CharacterActionAuthoringSearchCatalog BuildPreviewReferences(
            CharacterActionAuthoringSearchCategory category,
            CharacterActionPreviewBindingChoice[] discovered,
            IEnumerable<string> manualReferences,
            string manualSource)
        {
            var candidates = new List<CharacterActionAuthoringSearchCandidate>();
            discovered = discovered ?? Array.Empty<CharacterActionPreviewBindingChoice>();
            for (int i = 0; i < discovered.Length; i++)
            {
                CharacterActionPreviewBindingChoice choice = discovered[i];
                if (string.IsNullOrWhiteSpace(choice.Value))
                    continue;
                candidates.Add(new CharacterActionAuthoringSearchCandidate(
                    category,
                    choice.Value,
                    string.IsNullOrWhiteSpace(choice.Label) ? choice.Value : choice.Label,
                    choice.Value,
                    "Generated character package"));
            }

            AddStringCandidates(candidates, category, manualReferences, manualSource);
            return new CharacterActionAuthoringSearchCatalog(candidates);
        }

        private static void AddActionCandidate(
            List<CharacterActionAuthoringSearchCandidate> candidates,
            CharacterActionJsonDto action,
            string libraryId)
        {
            if (action == null || action.ActionId <= 0)
                return;
            string actionId = action.ActionId.ToString(CultureInfo.InvariantCulture);
            string secondary = action.Category ?? string.Empty;
            if (action.DurationFrames.HasValue)
                secondary += (secondary.Length == 0 ? string.Empty : " · ") + action.DurationFrames.Value + " 帧";
            candidates.Add(new CharacterActionAuthoringSearchCandidate(
                CharacterActionAuthoringSearchCategory.Action,
                actionId,
                (string.IsNullOrWhiteSpace(action.DisplayName) ? "Action" : action.DisplayName)
                    + " (#" + actionId + ")",
                secondary,
                libraryId));
        }

        private static void AddIntentBindings(
            List<CharacterActionAuthoringSearchCandidate> candidates,
            CharacterActionSetJsonDto actionSet)
        {
            CharacterActionBindingJsonDto[] bindings = actionSet?.CommandBindings
                ?? Array.Empty<CharacterActionBindingJsonDto>();
            for (int i = 0; i < bindings.Length; i++)
            {
                CharacterActionBindingJsonDto binding = bindings[i];
                if (binding == null || binding.IntentId <= 0)
                    continue;
                string intentLabel = CharacterActionAuthoringIds.Label(binding.IntentId, 404);
                candidates.Add(new CharacterActionAuthoringSearchCandidate(
                    CharacterActionAuthoringSearchCategory.Intent,
                    intentLabel,
                    intentLabel,
                    binding.ActionId <= 0 ? string.Empty : "→ #" + binding.ActionId,
                    actionSet.StableId));
            }
        }

        private static void AddReactionProfileCandidate(
            List<CharacterActionAuthoringSearchCandidate> candidates,
            CharacterReactionProfileJsonDto profile,
            string source)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.StableId))
                return;
            candidates.Add(new CharacterActionAuthoringSearchCandidate(
                CharacterActionAuthoringSearchCategory.ReactionProfile,
                profile.StableId,
                string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.StableId : profile.DisplayName,
                profile.DefaultActionId > 0
                    ? "#" + profile.DefaultActionId.ToString(CultureInfo.InvariantCulture)
                    : string.Empty,
                source));
        }

        private static void AddBindingTags(
            List<CharacterActionAuthoringSearchCandidate> candidates,
            CharacterActionSetJsonDto actionSet,
            string source)
        {
            CharacterAbilityActionBindingJsonDto[] bindings = actionSet?.AbilityBindings
                ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            for (int i = 0; i < bindings.Length; i++)
            {
                AddStringCandidates(
                    candidates,
                    CharacterActionAuthoringSearchCategory.Tag,
                    bindings[i]?.RequiredTags,
                    source);
                AddStringCandidates(
                    candidates,
                    CharacterActionAuthoringSearchCategory.Tag,
                    bindings[i]?.ForbiddenTags,
                    source);
            }
        }

        private static void AddStringCandidates(
            List<CharacterActionAuthoringSearchCandidate> candidates,
            CharacterActionAuthoringSearchCategory category,
            IEnumerable<string> values,
            string source)
        {
            if (values == null)
                return;
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                candidates.Add(new CharacterActionAuthoringSearchCandidate(
                    category,
                    value,
                    value,
                    string.Empty,
                    source));
            }
        }
    }
}

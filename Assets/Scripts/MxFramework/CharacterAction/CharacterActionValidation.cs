using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;

namespace MxFramework.CharacterAction
{
    public static class CharacterActionValidation
    {
        public static CharacterActionDiagnostic[] ValidateActionSet(
            CharacterActionSetConfig actionSet,
            CharacterActionConfig[] actions,
            CharacterReactionProfile[] reactionProfiles = null)
        {
            if (actionSet == null)
            {
                return new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.MissingActionSet,
                        "Character action set is missing.")
                };
            }

            actions = actions ?? Array.Empty<CharacterActionConfig>();
            reactionProfiles = reactionProfiles ?? Array.Empty<CharacterReactionProfile>();
            var diagnostics = new List<CharacterActionDiagnostic>();

            for (int i = 0; i < actionSet.CommandBindings.Length; i++)
            {
                CharacterActionBinding binding = actionSet.CommandBindings[i];
                if (binding == null || string.IsNullOrEmpty(binding.IntentId) || string.IsNullOrEmpty(binding.ActionId))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.MissingActionBinding,
                        "Command binding is missing an intent id or action id."));
                    continue;
                }

                if (!ContainsAction(actions, binding.ActionId))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.MissingActionConfig,
                        "Command binding action '" + binding.ActionId + "' is missing."));
                }
            }

            for (int i = 0; i < actionSet.AbilityBindings.Length; i++)
            {
                CharacterAbilityActionBinding binding = actionSet.AbilityBindings[i];
                if (binding == null || binding.AbilityId <= 0 || string.IsNullOrEmpty(binding.ActionId))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.MissingAbilityBinding,
                        "Ability binding is missing an ability id or action id."));
                    continue;
                }

                if (!ContainsAction(actions, binding.ActionId))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.MissingActionConfig,
                        "Ability binding action '" + binding.ActionId + "' is missing."));
                }
            }

            if (!string.IsNullOrEmpty(actionSet.ReactionProfileId) && !ContainsReactionProfile(reactionProfiles, actionSet.ReactionProfileId))
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.MissingReactionProfile,
                    "Reaction profile '" + actionSet.ReactionProfileId + "' is missing."));
            }

            return diagnostics.ToArray();
        }

        public static CharacterActionDiagnostic[] ValidateActionConfig(
            CharacterActionConfig action,
            CombatActionTimeline combatTimeline = null)
        {
            if (action == null)
            {
                return new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.MissingActionConfig,
                        "Action config is missing.")
                };
            }

            var diagnostics = new List<CharacterActionDiagnostic>();
            if (string.IsNullOrEmpty(action.StableId))
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.MissingActionConfig,
                    "Action config stable id is missing."));
            }

            CharacterActionValidationIssue[] phaseIssues = CharacterActionPhaseValidator.Validate(
                action.TimelineAuthority,
                action.Phases,
                combatTimeline);
            for (int i = 0; i < phaseIssues.Length; i++)
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(phaseIssues[i].Code, phaseIssues[i].Message));
            }

            for (int i = 0; i < action.CancelRules.Length; i++)
            {
                CharacterCancelRule rule = action.CancelRules[i];
                if (rule.EndFrame >= action.DurationFrames.GetValueOrDefault(int.MaxValue))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Warning(
                        CharacterActionDiagnosticCodes.InvalidCancelWindow,
                        "Cancel window extends beyond explicit action duration."));
                }
            }

            if (action.TimelineAuthority == CharacterActionTimelineAuthority.CombatAnchored && combatTimeline == null)
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.CombatActionMissing,
                    "CombatAnchored action requires a registered CombatActionTimeline."));
            }

            ValidateTrackDependencies(action, diagnostics);

            return diagnostics.ToArray();
        }

        public static CharacterActionDiagnostic[] ValidatePressureOnlyReactionProfile(
            CharacterReactionProfile profile,
            CharacterActionConfig[] actions)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            actions = actions ?? Array.Empty<CharacterActionConfig>();
            var diagnostics = new List<CharacterActionDiagnostic>();
            diagnostics.AddRange(CharacterReactionRuleValidator.ValidatePressureOnlyProfile(profile));

            for (int i = 0; i < profile.Rules.Length; i++)
            {
                CharacterReactionRule rule = profile.Rules[i];
                if (rule == null)
                    continue;

                ValidateReactionAction(rule.ActionId, actions, diagnostics);
            }

            if (!string.IsNullOrEmpty(profile.DefaultActionId))
                ValidateReactionAction(profile.DefaultActionId, actions, diagnostics);

            return diagnostics.ToArray();
        }

        public static CharacterActionDiagnostic[] ValidateCancelConflict(
            CharacterActionTimelineAuthority authority,
            CharacterCancelRule[] characterRules,
            CombatActionTimeline combatTimeline,
            int localFrame,
            int targetActionId,
            CharacterActionSourceKind sourceKind)
        {
            CharacterCancelConflictResult result = CharacterCancelConflictClassifier.Classify(
                authority,
                characterRules,
                combatTimeline,
                localFrame,
                targetActionId,
                sourceKind);

            if (result.Allowed)
                return Array.Empty<CharacterActionDiagnostic>();

            return new[]
            {
                CharacterActionDiagnostic.Error(
                    result.Code,
                    "Cancel conflict rejected by " + result.RejectedBy + ".")
            };
        }

        private static void ValidateReactionAction(
            string actionId,
            CharacterActionConfig[] actions,
            List<CharacterActionDiagnostic> diagnostics)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.ReactionRuleNoTarget,
                    "Reaction rule action id is missing."));
                return;
            }

            CharacterActionConfig action = FindAction(actions, actionId);
            if (action == null)
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.MissingActionConfig,
                    "Reaction action '" + actionId + "' is missing."));
                return;
            }

            if (action.Category != CharacterActionCategory.Reaction)
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.ReactionRuleNoTarget,
                    "Reaction action '" + actionId + "' must use CharacterActionCategory.Reaction."));
            }
        }

        private static void ValidateTrackDependencies(
            CharacterActionConfig action,
            List<CharacterActionDiagnostic> diagnostics)
        {
            for (int i = 0; i < action.CombatTrack.Events.Length; i++)
            {
                CombatTrackEvent trackEvent = action.CombatTrack.Events[i];
                if (trackEvent.Kind == CharacterActionTrackEventKind.StartCombatAction
                    && string.IsNullOrEmpty(trackEvent.CombatActionId)
                    && string.IsNullOrEmpty(action.CombatTrack.CombatActionId))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.CombatActionMissing,
                        "Combat track start event requires a combat action id."));
                }
            }

            for (int i = 0; i < action.GameplayTrack.Events.Length; i++)
            {
                GameplayTrackEvent trackEvent = action.GameplayTrack.Events[i];
                if (trackEvent.Kind == CharacterActionTrackEventKind.SendGameplayRequest
                    && string.IsNullOrEmpty(trackEvent.RequestId))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.ResourceCostWithoutResourceId,
                        "Gameplay request track event requires a request id."));
                }
            }

            for (int i = 0; i < action.AnimationTrack.Events.Length; i++)
            {
                AnimationTrackEvent trackEvent = action.AnimationTrack.Events[i];
                if (string.IsNullOrEmpty(trackEvent.AnimationActionKey))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.AnimationActionMissing,
                        "Animation track event requires an animation action key."));
                }
            }

            for (int i = 0; i < action.PresentationTrack.Events.Length; i++)
            {
                PresentationTrackEvent trackEvent = action.PresentationTrack.Events[i];
                if (trackEvent.Kind == CharacterActionTrackEventKind.PlayAudioCue
                    && string.IsNullOrEmpty(trackEvent.CueId))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.AudioCueMissing,
                        "Audio presentation event requires a cue id."));
                }

                if (trackEvent.Kind == CharacterActionTrackEventKind.SpawnVisualCue
                    && string.IsNullOrEmpty(trackEvent.ResourceKey))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.PresentationResourceMissing,
                        "Visual presentation event requires a resource key."));
                }
            }
        }

        private static bool ContainsAction(CharacterActionConfig[] actions, string actionId)
        {
            return FindAction(actions, actionId) != null;
        }

        private static CharacterActionConfig FindAction(CharacterActionConfig[] actions, string actionId)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action != null && string.Equals(action.StableId, actionId, StringComparison.Ordinal))
                    return action;
            }

            return null;
        }

        private static bool ContainsReactionProfile(CharacterReactionProfile[] profiles, string profileId)
        {
            for (int i = 0; i < profiles.Length; i++)
            {
                CharacterReactionProfile profile = profiles[i];
                if (profile != null && string.Equals(profile.StableId, profileId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}

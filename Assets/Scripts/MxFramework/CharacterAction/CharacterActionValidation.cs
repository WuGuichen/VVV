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

            ValidateCancelTargets(actions, diagnostics);

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

            int? durationFrames = ResolveValidationDuration(action, combatTimeline);
            CharacterActionValidationIssue[] phaseIssues = CharacterActionPhaseValidator.Validate(
                action.TimelineAuthority,
                action.Phases,
                combatTimeline,
                durationFrames);
            for (int i = 0; i < phaseIssues.Length; i++)
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(phaseIssues[i].Code, phaseIssues[i].Message));
            }

            ValidateCancelWindows(action, durationFrames, diagnostics);

            if (action.TimelineAuthority == CharacterActionTimelineAuthority.CombatAnchored && combatTimeline == null)
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.CombatActionMissing,
                    "CombatAnchored action requires a registered CombatActionTimeline."));
            }

            ValidateTrackDependencies(action, diagnostics);
            ValidateCombatTrackTimeline(action, durationFrames, diagnostics);

            return diagnostics.ToArray();
        }

        public static CharacterActionDiagnostic[] ValidatePressureOnlyReactionProfile(
            CharacterReactionProfile profile,
            CharacterActionConfig[] actions)
        {
            return ValidateReactionProfile(
                profile,
                actions,
                CharacterReactionContextCompleteness.PressureOnly);
        }

        public static CharacterActionDiagnostic[] ValidateReactionProfile(
            CharacterReactionProfile profile,
            CharacterActionConfig[] actions,
            CharacterReactionContextCompleteness completeness)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            actions = actions ?? Array.Empty<CharacterActionConfig>();
            var diagnostics = new List<CharacterActionDiagnostic>();
            diagnostics.AddRange(CharacterReactionRuleValidator.ValidateProfileForCompleteness(profile, completeness));

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
            CharacterActionResourceDependency[] dependencies = CharacterActionResourceDependencyCollector.Collect(action);
            for (int i = 0; i < dependencies.Length; i++)
            {
                CharacterActionResourceDependency dependency = dependencies[i];
                if (!dependency.IsMissing)
                    continue;

                AddMissingDependencyDiagnostic(dependency, diagnostics);
            }
        }

        private static void AddMissingDependencyDiagnostic(
            CharacterActionResourceDependency dependency,
            List<CharacterActionDiagnostic> diagnostics)
        {
            switch (dependency.Kind)
            {
                case CharacterActionResourceDependencyKind.CombatAction:
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.CombatActionMissing,
                        "Combat track start event requires a combat action id."));
                    break;
                case CharacterActionResourceDependencyKind.GameplayRequest:
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.ResourceCostWithoutResourceId,
                        "Gameplay request track event requires a request id."));
                    break;
                case CharacterActionResourceDependencyKind.AnimationAction:
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.AnimationActionMissing,
                        "Animation track event requires an animation action key."));
                    break;
                case CharacterActionResourceDependencyKind.AudioCue:
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.AudioCueMissing,
                        "Audio presentation event requires a cue id."));
                    break;
                case CharacterActionResourceDependencyKind.VfxResource:
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.PresentationResourceMissing,
                        "Visual presentation event requires a resource key."));
                    break;
            }
        }

        private static void ValidateCancelTargets(
            CharacterActionConfig[] actions,
            List<CharacterActionDiagnostic> diagnostics)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action == null)
                    continue;

                for (int j = 0; j < action.CancelRules.Length; j++)
                {
                    CharacterCancelRule rule = action.CancelRules[j];
                    if (rule.TargetActionId > 0 && !ContainsActionId(actions, rule.TargetActionId))
                    {
                        diagnostics.Add(CharacterActionDiagnostic.Error(
                            CharacterActionDiagnosticCodes.CancelTargetMissing,
                            "Cancel target action id " + rule.TargetActionId + " is missing."));
                    }
                }

                for (int j = 0; j < action.InterruptRules.Length; j++)
                {
                    CharacterInterruptRule rule = action.InterruptRules[j];
                    if (rule.TargetActionId > 0 && !ContainsActionId(actions, rule.TargetActionId))
                    {
                        diagnostics.Add(CharacterActionDiagnostic.Error(
                            CharacterActionDiagnosticCodes.InterruptTargetMissing,
                            "Interrupt target action id " + rule.TargetActionId + " is missing."));
                    }
                }
            }
        }

        private static int? ResolveValidationDuration(CharacterActionConfig action, CombatActionTimeline combatTimeline)
        {
            if (action.DurationFrames.HasValue)
                return action.DurationFrames.Value;
            if (combatTimeline != null)
                return combatTimeline.TotalFrames;
            return null;
        }

        private static void ValidateCancelWindows(
            CharacterActionConfig action,
            int? durationFrames,
            List<CharacterActionDiagnostic> diagnostics)
        {
            for (int i = 0; i < action.CancelRules.Length; i++)
            {
                CharacterCancelRule rule = action.CancelRules[i];
                if (durationFrames.HasValue && rule.EndFrame >= durationFrames.Value)
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.InvalidCancelWindow,
                        "Cancel window extends beyond action duration."));
                }

                if (!IsCancelWindowInsideCancelablePhase(action.Phases, rule.StartFrame, rule.EndFrame))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.InvalidCancelWindow,
                        "Cancel window must fall within a cancelable phase."));
                }
            }
        }

        private static bool IsCancelWindowInsideCancelablePhase(CharacterActionPhase[] phases, int startFrame, int endFrame)
        {
            if (phases == null || phases.Length == 0)
                return true;

            for (int i = 0; i < phases.Length; i++)
            {
                CharacterActionPhase phase = phases[i];
                if (phase.StartFrame <= startFrame
                    && phase.EndFrame >= endFrame
                    && IsCancelablePhase(phase.Kind))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCancelablePhase(CharacterActionPhaseKind kind)
        {
            return kind == CharacterActionPhaseKind.Active
                || kind == CharacterActionPhaseKind.Recovery
                || kind == CharacterActionPhaseKind.Loop
                || kind == CharacterActionPhaseKind.Channel
                || kind == CharacterActionPhaseKind.Hold
                || kind == CharacterActionPhaseKind.Exit;
        }

        private static void ValidateCombatTrackTimeline(
            CharacterActionConfig action,
            int? durationFrames,
            List<CharacterActionDiagnostic> diagnostics)
        {
            for (int i = 0; i < action.CombatTrack.Events.Length; i++)
            {
                CombatTrackEvent trackEvent = action.CombatTrack.Events[i];
                bool outsideDuration = durationFrames.HasValue && trackEvent.Frame >= durationFrames.Value;
                if (outsideDuration)
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.TrackEventOutsideDuration,
                        "Combat track event frame must be within action duration."));
                }

                if ((trackEvent.Kind == CharacterActionTrackEventKind.StartHitTrace
                        || trackEvent.Kind == CharacterActionTrackEventKind.StopHitTrace)
                    && !outsideDuration
                    && !IsFrameInPhase(action.Phases, trackEvent.Frame, CharacterActionPhaseKind.Active))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.CombatTraceOutsideActivePhase,
                        "Combat hit trace events must occur in the Active phase."));
                }
            }
        }

        private static bool IsFrameInPhase(CharacterActionPhase[] phases, int frame, CharacterActionPhaseKind kind)
        {
            if (phases == null || phases.Length == 0)
                return true;

            for (int i = 0; i < phases.Length; i++)
            {
                CharacterActionPhase phase = phases[i];
                if (phase.Kind == kind && phase.Contains(frame))
                    return true;
            }

            return false;
        }

        private static bool ContainsAction(CharacterActionConfig[] actions, string actionId)
        {
            return FindAction(actions, actionId) != null;
        }

        private static bool ContainsActionId(CharacterActionConfig[] actions, int actionId)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action != null && action.Id == actionId)
                    return true;
            }

            return false;
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

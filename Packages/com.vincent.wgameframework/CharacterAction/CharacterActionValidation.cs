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
            CharacterReactionProfile[] reactionProfiles = null,
            CharacterLandingProfile[] landingProfiles = null)
        {
            if (actionSet == null)
            {
                return new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.MissingActionSet,
                        "Character action set is missing.")
                };
            }

            actions = actions ?? Array.Empty<CharacterActionConfig>();
            reactionProfiles = reactionProfiles ?? Array.Empty<CharacterReactionProfile>();
            landingProfiles = landingProfiles ?? Array.Empty<CharacterLandingProfile>();
            var diagnostics = new List<CharacterActionDiagnostic>();
            var commandBindingIntents = new HashSet<int>();

            for (int i = 0; i < actionSet.CommandBindings.Length; i++)
            {
                CharacterActionBinding binding = actionSet.CommandBindings[i];
                if (binding == null || (binding.IntentId <= 0) || !binding.ActionId.IsValid)
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.MissingActionBinding,
                        "Command binding is missing an intent id or action id."));
                    continue;
                }

                commandBindingIntents.Add(binding.IntentId);

                if (!ContainsAction(actions, binding.ActionId))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.MissingActionConfig,
                        "Command binding action '" + binding.ActionId + "' is missing."));
                }
            }

            var commandSequenceIntents = new HashSet<int>();
            for (int i = 0; i < actionSet.CommandSequences.Length; i++)
            {
                CharacterActionCommandSequence sequence = actionSet.CommandSequences[i];
                if (sequence == null || (sequence.IntentId <= 0) || sequence.ActionIds.Length == 0)
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.CommandSequenceInvalid,
                        "Command sequence requires an intent id and at least one action id."));
                    continue;
                }

                if (!commandSequenceIntents.Add(sequence.IntentId))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.CommandSequenceInvalid,
                        "Command sequence intent '" + sequence.IntentId + "' is duplicated."));
                }

                if (commandBindingIntents.Contains(sequence.IntentId))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.CommandIntentConflict,
                        "Intent '" + sequence.IntentId + "' cannot exist in both commandBindings and commandSequences."));
                }

                var sequenceActionIds = new HashSet<CharacterActionId>();
                for (int actionIndex = 0; actionIndex < sequence.ActionIds.Length; actionIndex++)
                {
                    CharacterActionId actionId = sequence.ActionIds[actionIndex];
                    if (!actionId.IsValid)
                    {
                        diagnostics.Add(CharacterActionDiagnostic.Error(
                            CharacterActionDiagnosticCode.CommandSequenceInvalid,
                            "Command sequence '" + sequence.IntentId + "' contains an empty action id."));
                        continue;
                    }

                    if (!sequenceActionIds.Add(actionId))
                    {
                        diagnostics.Add(CharacterActionDiagnostic.Error(
                            CharacterActionDiagnosticCode.CommandSequenceInvalid,
                            "Command sequence '" + sequence.IntentId + "' contains duplicate action '" + actionId + "'."));
                    }

                    if (!ContainsAction(actions, actionId))
                    {
                        diagnostics.Add(CharacterActionDiagnostic.Error(
                            CharacterActionDiagnosticCode.MissingActionConfig,
                            "Command sequence action '" + actionId + "' is missing."));
                    }
                }
            }

            for (int i = 0; i < actionSet.AbilityBindings.Length; i++)
            {
                CharacterAbilityActionBinding binding = actionSet.AbilityBindings[i];
                if (binding == null || binding.AbilityId <= 0 || !binding.ActionId.IsValid)
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.MissingAbilityBinding,
                        "Ability binding is missing an ability id or action id."));
                    continue;
                }

                if (!ContainsAction(actions, binding.ActionId))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.MissingActionConfig,
                        "Ability binding action '" + binding.ActionId + "' is missing."));
                }
            }

            if (!string.IsNullOrEmpty(actionSet.ReactionProfileId) && !ContainsReactionProfile(reactionProfiles, actionSet.ReactionProfileId))
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.MissingReactionProfile,
                    "Reaction profile '" + actionSet.ReactionProfileId + "' is missing."));
            }

            if (!string.IsNullOrEmpty(actionSet.LandingProfileId))
            {
                CharacterLandingProfile landingProfile = FindLandingProfile(
                    landingProfiles,
                    actionSet.LandingProfileId);
                if (landingProfile == null)
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.MissingLandingProfile,
                        "Landing profile '" + actionSet.LandingProfileId + "' is missing."));
                }
                else
                {
                    ValidateOptionalLandingAction(actions, landingProfile.LightLandingActionId, "Light landing", diagnostics);
                    ValidateOptionalLandingAction(actions, landingProfile.HeavyLandingActionId, "Heavy landing", diagnostics);
                }
            }

            ValidateInterruptTargets(actions, diagnostics);

            return diagnostics.ToArray();
        }

        private static CharacterLandingProfile FindLandingProfile(
            CharacterLandingProfile[] profiles,
            string profileId)
        {
            for (int i = 0; i < profiles.Length; i++)
            {
                CharacterLandingProfile profile = profiles[i];
                if (profile != null && string.Equals(profile.StableId, profileId, StringComparison.Ordinal))
                    return profile;
            }

            return null;
        }

        private static void ValidateOptionalLandingAction(
            CharacterActionConfig[] actions,
            CharacterActionId actionId,
            string label,
            List<CharacterActionDiagnostic> diagnostics)
        {
            if (!actionId.IsValid || ContainsAction(actions, actionId))
                return;

            diagnostics.Add(CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCode.MissingActionConfig,
                label + " action '" + actionId + "' is missing."));
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
                        CharacterActionDiagnosticCode.MissingActionConfig,
                        "Action config is missing.")
                };
            }

            var diagnostics = new List<CharacterActionDiagnostic>();
            if (!action.ActionId.IsValid)
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "Action config id is missing."));
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
                    CharacterActionDiagnosticCode.CombatActionMissing,
                    "CombatAnchored action requires a registered CombatActionTimeline."));
            }

            ValidateTrackDependencies(action, diagnostics);
            ValidateCombatTrackTimeline(action, durationFrames, diagnostics);
            ValidateLegacyTraceProfileIds(action, diagnostics);

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

            if (profile.DefaultActionId.IsValid)
                ValidateReactionAction(profile.DefaultActionId, actions, diagnostics);

            return diagnostics.ToArray();
        }

        public static CharacterActionDiagnostic[] ValidateCancelConflict(
            CharacterActionTimelineAuthority authority,
            CharacterCancelRule[] characterRules,
            CombatActionTimeline combatTimeline,
            int localFrame,
            CharacterActionId targetActionId,
            CharacterActionCategory requestCategory)
        {
            CharacterCancelConflictResult result = CharacterCancelConflictClassifier.Classify(
                authority,
                characterRules,
                combatTimeline,
                localFrame,
                targetActionId,
                requestCategory);

            if (result.Allowed)
                return Array.Empty<CharacterActionDiagnostic>();

            return new[]
            {
                CharacterActionDiagnostic.Error(
                    result.CodeId,
                    "Cancel conflict rejected by " + result.RejectedBy + ".")
            };
        }

        private static void ValidateReactionAction(
            CharacterActionId actionId,
            CharacterActionConfig[] actions,
            List<CharacterActionDiagnostic> diagnostics)
        {
            if (!actionId.IsValid)
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.ReactionRuleNoTarget,
                    "Reaction rule action id is missing."));
                return;
            }

            CharacterActionConfig action = FindAction(actions, actionId);
            if (action == null)
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "Reaction action '" + actionId + "' is missing."));
                return;
            }

            if (action.Category != CharacterActionCategory.Reaction)
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.ReactionRuleNoTarget,
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

        private static void ValidateLegacyTraceProfileIds(
            CharacterActionConfig action,
            List<CharacterActionDiagnostic> diagnostics)
        {
            CombatTrackEvent[] events = action.CombatTrack.Events;
            for (int i = 0; i < events.Length; i++)
            {
                CombatTrackEvent trackEvent = events[i];
                if (trackEvent.Kind == CharacterActionTrackEventKind.HitTraceWindow &&
                    (trackEvent.TraceProfileId > 0))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Warning(
                        CharacterActionDiagnosticCode.LegacyTraceProfileId,
                        "Combat hit trace event uses legacy traceProfileId '" + trackEvent.TraceProfileId + "'. Prefer equipmentSlotId so the current equipped weapon selects trace geometry."));
                }
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
                        CharacterActionDiagnosticCode.CombatActionMissing,
                        "Combat track start event requires a combat action id."));
                    break;
                case CharacterActionResourceDependencyKind.GameplayRequest:
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.ResourceCostWithoutResourceId,
                        "Gameplay request track event requires a request id."));
                    break;
                case CharacterActionResourceDependencyKind.AnimationAction:
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.AnimationActionMissing,
                        "Animation track event requires an animation action key."));
                    break;
                case CharacterActionResourceDependencyKind.AudioCue:
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.AudioCueMissing,
                        "Audio presentation event requires a cue id."));
                    break;
                case CharacterActionResourceDependencyKind.VfxResource:
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.PresentationResourceMissing,
                        "Visual presentation event requires a resource key."));
                    break;
            }
        }

        private static void ValidateInterruptTargets(
            CharacterActionConfig[] actions,
            List<CharacterActionDiagnostic> diagnostics)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action == null)
                    continue;

                for (int j = 0; j < action.InterruptRules.Length; j++)
                {
                    CharacterInterruptRule rule = action.InterruptRules[j];
                    if (rule.TargetActionId.IsValid && !ContainsActionId(actions, rule.TargetActionId))
                    {
                        diagnostics.Add(CharacterActionDiagnostic.Error(
                            CharacterActionDiagnosticCode.InterruptTargetMissing,
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
                        CharacterActionDiagnosticCode.InvalidCancelWindow,
                        "Cancel window " + rule.StartFrame + "-" + rule.EndFrame
                        + " extends beyond action duration " + durationFrames.Value + "."));
                }

                if (!IsCancelWindowCoveredByCancelablePhases(
                    action.Phases,
                    rule.StartFrame,
                    rule.EndFrame,
                    allowStartup: rule.CancelInputMask != 0))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.InvalidCancelWindow,
                        "Cancel window " + rule.StartFrame + "-" + rule.EndFrame
                        + " must be fully covered by cancelable phases."));
                }
            }
        }

        internal static bool IsCancelWindowCoveredByCancelablePhases(
            CharacterActionPhase[] phases,
            int startFrame,
            int endFrame,
            bool allowStartup = false)
        {
            if (phases == null || phases.Length == 0)
                return true;

            int frame = startFrame;
            while (frame <= endFrame)
            {
                int coveredEndFrame = -1;
                for (int i = 0; i < phases.Length; i++)
                {
                    CharacterActionPhase phase = phases[i];
                    if (!IsCancelablePhase(phase.Kind, allowStartup))
                        continue;
                    if (phase.StartFrame > frame || phase.EndFrame < frame)
                        continue;
                    if (phase.EndFrame > coveredEndFrame)
                        coveredEndFrame = phase.EndFrame;
                }

                if (coveredEndFrame < frame)
                    return false;
                if (coveredEndFrame >= endFrame)
                    return true;

                frame = coveredEndFrame + 1;
            }

            return true;
        }

        internal static bool IsCancelablePhase(CharacterActionPhaseKind kind, bool allowStartup = false)
        {
            return (allowStartup && kind == CharacterActionPhaseKind.Startup)
                || kind == CharacterActionPhaseKind.Active
                || kind == CharacterActionPhaseKind.Recovery
                || kind == CharacterActionPhaseKind.Loop
                || kind == CharacterActionPhaseKind.Landing
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
                ValidatePartStateDependency(trackEvent, diagnostics);
                bool outsideDuration = durationFrames.HasValue && trackEvent.Frame >= durationFrames.Value;
                if (outsideDuration)
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.TrackEventOutsideDuration,
                        "Combat track event frame must be within action duration."));
                }

                if (trackEvent.Kind == CharacterActionTrackEventKind.HitTraceWindow
                    && !outsideDuration
                    && !IsFrameInPhase(action.Phases, trackEvent.Frame, CharacterActionPhaseKind.Active))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.CombatTraceOutsideActivePhase,
                        "Combat hit trace windows must start in the Active phase."));
                }
            }
        }

        private static void ValidatePartStateDependency(
            CombatTrackEvent trackEvent,
            List<CharacterActionDiagnostic> diagnostics)
        {
            bool hasPartStateId = trackEvent.RequiredPartStateId > 0;
            int rawMask = (int)trackEvent.AllowedPartStructureStateMask;
            if (!hasPartStateId && rawMask == 0)
                return;

            int unknownBits = rawMask & ~(int)CharacterActionPartStructureStateMask.AllKnown;
            if (trackEvent.Kind != CharacterActionTrackEventKind.HitTraceWindow
                || !hasPartStateId
                || rawMask == 0
                || unknownBits != 0)
            {
                diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.InvalidPartStateDependency,
                    "Combat part-state dependency requires a HitTraceWindow, a non-empty requiredPartStateId, and a non-zero allowedPartStructureStateMask containing only known states."));
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

        private static bool ContainsAction(CharacterActionConfig[] actions, CharacterActionId actionId)
        {
            return FindAction(actions, actionId) != null;
        }

        private static bool ContainsActionId(CharacterActionConfig[] actions, CharacterActionId actionId)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action != null && action.ActionId == actionId)
                    return true;
            }

            return false;
        }

        private static CharacterActionConfig FindAction(
            CharacterActionConfig[] actions,
            CharacterActionId actionId)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action != null && action.ActionId == actionId)
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

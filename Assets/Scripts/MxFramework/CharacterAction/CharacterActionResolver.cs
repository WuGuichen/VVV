using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterAction
{
    public sealed class CharacterActionResolverContext
    {
        public CharacterActionResolverContext(
            CharacterActionSetConfig actionSet,
            CharacterActionConfig[] actions,
            CharacterReactionProfile[] reactionProfiles = null,
            CombatActionTimeline[] combatTimelines = null,
            CharacterActionResolverState state = default,
            CharacterActionDurationPolicy durationPolicy = default,
            CharacterActionCombatTimelineBinding[] combatTimelineBindings = null,
            string[] contextTags = null)
        {
            ActionSet = actionSet;
            Actions = actions ?? Array.Empty<CharacterActionConfig>();
            ReactionProfiles = reactionProfiles ?? Array.Empty<CharacterReactionProfile>();
            CombatTimelines = combatTimelines ?? Array.Empty<CombatActionTimeline>();
            CombatTimelineBindings = combatTimelineBindings ?? CreateTimelineBindings(CombatTimelines);
            State = state;
            DurationPolicy = durationPolicy;
            ContextTags = contextTags ?? Array.Empty<string>();
        }

        public CharacterActionSetConfig ActionSet { get; }
        public CharacterActionConfig[] Actions { get; }
        public CharacterReactionProfile[] ReactionProfiles { get; }
        public CombatActionTimeline[] CombatTimelines { get; }
        public CharacterActionCombatTimelineBinding[] CombatTimelineBindings { get; }
        public CharacterActionResolverState State { get; }
        public CharacterActionDurationPolicy DurationPolicy { get; }
        public string[] ContextTags { get; }

        private static CharacterActionCombatTimelineBinding[] CreateTimelineBindings(CombatActionTimeline[] combatTimelines)
        {
            if (combatTimelines == null || combatTimelines.Length == 0)
                return Array.Empty<CharacterActionCombatTimelineBinding>();

            var bindings = new CharacterActionCombatTimelineBinding[combatTimelines.Length];
            for (int i = 0; i < combatTimelines.Length; i++)
            {
                CombatActionTimeline timeline = combatTimelines[i];
                bindings[i] = timeline == null
                    ? default
                    : new CharacterActionCombatTimelineBinding(timeline.ActionId.ToString(), timeline);
            }

            return bindings;
        }
    }

    public readonly struct CharacterActionCombatTimelineBinding : IEquatable<CharacterActionCombatTimelineBinding>
    {
        public CharacterActionCombatTimelineBinding(string combatActionId, CombatActionTimeline timeline)
        {
            CombatActionId = combatActionId ?? string.Empty;
            Timeline = timeline;
        }

        public string CombatActionId { get; }
        public CombatActionTimeline Timeline { get; }

        public bool Equals(CharacterActionCombatTimelineBinding other)
        {
            return string.Equals(CombatActionId, other.CombatActionId, StringComparison.Ordinal)
                && ReferenceEquals(Timeline, other.Timeline);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionCombatTimelineBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((CombatActionId == null ? 0 : CombatActionId.GetHashCode()) * 397)
                    ^ (Timeline == null ? 0 : Timeline.GetHashCode());
            }
        }
    }

    public readonly struct CharacterActionResolverState
    {
        public CharacterActionResolverState(
            bool isDisabled = false,
            bool isDead = false,
            bool hasActiveAction = false,
            bool activeActionBlocksImmediateStart = false,
            string activeActionId = "",
            int activeActionLocalFrame = 0,
            CharacterActionTimelineAuthority activeActionAuthority = CharacterActionTimelineAuthority.CharacterAuthored,
            CharacterCancelRule[] activeCancelRules = null,
            CombatActionTimeline activeCombatTimeline = null)
        {
            if (activeActionLocalFrame < 0)
                throw new ArgumentOutOfRangeException(nameof(activeActionLocalFrame), "Active action local frame cannot be negative.");
            if (!Enum.IsDefined(typeof(CharacterActionTimelineAuthority), activeActionAuthority))
                throw new ArgumentOutOfRangeException(nameof(activeActionAuthority), "Timeline authority is not defined.");

            IsDisabled = isDisabled;
            IsDead = isDead;
            HasActiveAction = hasActiveAction;
            ActiveActionBlocksImmediateStart = activeActionBlocksImmediateStart;
            ActiveActionId = activeActionId ?? string.Empty;
            ActiveActionLocalFrame = activeActionLocalFrame;
            ActiveActionAuthority = activeActionAuthority;
            ActiveCancelRules = activeCancelRules ?? Array.Empty<CharacterCancelRule>();
            ActiveCombatTimeline = activeCombatTimeline;
        }

        public bool IsDisabled { get; }
        public bool IsDead { get; }
        public bool HasActiveAction { get; }
        public bool ActiveActionBlocksImmediateStart { get; }
        public string ActiveActionId { get; }
        public int ActiveActionLocalFrame { get; }
        public CharacterActionTimelineAuthority ActiveActionAuthority { get; }
        public CharacterCancelRule[] ActiveCancelRules { get; }
        public CombatActionTimeline ActiveCombatTimeline { get; }
    }

    public sealed class CharacterActionResolver
    {
        private long _nextPlanId = 1L;

        public CharacterActionResolveResult ResolveCommand(
            CharacterActionResolverContext context,
            CharacterActionIntentRequest request)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.ActionSet == null)
            {
                return Reject(
                    CharacterActionRejectReason.MissingActionSet,
                    CharacterActionDiagnosticCodes.MissingActionSet,
                    "Character action set is missing.",
                    request.TraceId);
            }

            CandidateBuildResult candidates = BuildCommandCandidates(context, request);
            if (candidates.Candidates.Count == 0)
            {
                if (HasErrors(candidates.Diagnostics))
                {
                    return CharacterActionResolveResult.Rejected(
                        ResolveCandidateRejectReason(candidates.Diagnostics),
                        candidates.Diagnostics.ToArray(),
                        request.TraceId);
                }

                return Reject(
                    CharacterActionRejectReason.MissingActionBinding,
                    CharacterActionDiagnosticCodes.MissingActionBinding,
                    "No command binding found for intent '" + request.IntentId + "'.",
                    request.TraceId);
            }

            ResolverCandidate candidate = candidates.Candidates[0];
            return ResolveBoundAction(context, request, candidate.Action, candidate.Candidate.AllowQueue);
        }

        public CharacterActionResolveResult ResolveAbility(
            CharacterActionResolverContext context,
            CharacterActionIntentRequest request)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.ActionSet == null)
            {
                return Reject(
                    CharacterActionRejectReason.MissingActionSet,
                    CharacterActionDiagnosticCodes.MissingActionSet,
                    "Character action set is missing.",
                    request.TraceId);
            }

            if (!request.AbilityId.HasValue)
            {
                return Reject(
                    CharacterActionRejectReason.MissingAbilityBinding,
                    CharacterActionDiagnosticCodes.MissingAbilityBinding,
                    "Ability action request did not include an ability id.",
                    request.TraceId);
            }

            CandidateBuildResult candidates = BuildAbilityCandidates(context, request, request.AbilityId.Value);
            if (candidates.Candidates.Count == 0)
            {
                if (HasErrors(candidates.Diagnostics))
                {
                    return CharacterActionResolveResult.Rejected(
                        ResolveCandidateRejectReason(candidates.Diagnostics),
                        candidates.Diagnostics.ToArray(),
                        request.TraceId);
                }

                return Reject(
                    CharacterActionRejectReason.MissingAbilityBinding,
                    CharacterActionDiagnosticCodes.MissingAbilityBinding,
                    "No ability binding found for ability " + request.AbilityId.Value + ".",
                    request.TraceId);
            }

            return ResolveBoundAction(context, request, candidates.Candidates[0].Action, candidates.Candidates[0].Candidate.AllowQueue);
        }

        public CharacterActionResolveResult ResolveReaction(
            CharacterActionResolverContext context,
            CharacterReactionContext reactionContext)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            string traceId = reactionContext.TraceId;
            if (context.ActionSet == null)
            {
                return Reject(
                    CharacterActionRejectReason.MissingActionSet,
                    CharacterActionDiagnosticCodes.MissingActionSet,
                    "Character action set is missing.",
                    traceId);
            }

            CharacterReactionProfile profile = FindReactionProfile(context, context.ActionSet.ReactionProfileId);
            if (profile == null)
            {
                return Reject(
                    CharacterActionRejectReason.ReactionContextIncomplete,
                    CharacterActionDiagnosticCodes.MissingReactionProfile,
                    "Reaction profile '" + context.ActionSet.ReactionProfileId + "' is missing.",
                    traceId);
            }

            CharacterActionDiagnostic[] profileDiagnostics = CharacterActionValidation.ValidateReactionProfile(
                profile,
                context.Actions,
                reactionContext.Completeness);
            if (HasErrors(profileDiagnostics))
            {
                return CharacterActionResolveResult.Rejected(
                    ResolveReactionProfileRejectReason(profileDiagnostics),
                    profileDiagnostics,
                    traceId);
            }

            CharacterReactionSelectionResult selection = CharacterReactionSelector.Select(profile, reactionContext);
            if (!selection.Accepted)
            {
                return CharacterActionResolveResult.Rejected(
                    CharacterActionRejectReason.ReactionContextIncomplete,
                    selection.Diagnostics,
                    traceId);
            }

            CharacterActionIntentRequest request = new CharacterActionIntentRequest(
                reactionContext.EntityId,
                intentId: string.Empty,
                abilityId: null,
                abilityStableId: string.Empty,
                requestedActionId: selection.SelectedActionId,
                sourceKind: CharacterActionSourceKind.Reaction,
                priority: int.MaxValue,
                frame: reactionContext.Frame,
                traceId: traceId);

            CharacterActionConfig action = FindAction(context.Actions, selection.SelectedActionId);
            return ResolveBoundAction(context, request, action, allowQueue: false);
        }

        private CharacterActionResolveResult ResolveBoundAction(
            CharacterActionResolverContext context,
            CharacterActionIntentRequest request,
            CharacterActionConfig action,
            bool allowQueue)
        {
            if (context.State.IsDisabled)
            {
                return Reject(
                    CharacterActionRejectReason.StateDisabled,
                    CharacterActionDiagnosticCodes.CharacterCancelRejected,
                    "Character action resolver state is disabled.",
                    request.TraceId);
            }

            if (context.State.IsDead && request.SourceKind != CharacterActionSourceKind.Reaction)
            {
                return Reject(
                    CharacterActionRejectReason.Dead,
                    CharacterActionDiagnosticCodes.CharacterCancelRejected,
                    "Dead characters cannot resolve non-reaction actions.",
                    request.TraceId);
            }

            if (action == null)
            {
                return Reject(
                    CharacterActionRejectReason.MissingActionConfig,
                    CharacterActionDiagnosticCodes.MissingActionConfig,
                    "Action config is missing.",
                    request.TraceId);
            }

            CombatActionTimeline timeline = FindCombatTimeline(context, action);
            if (action.TimelineAuthority == CharacterActionTimelineAuthority.CombatAnchored && timeline == null)
            {
                return Reject(
                    CharacterActionRejectReason.CombatActionMissing,
                    CharacterActionDiagnosticCodes.CombatActionMissing,
                    "CombatAnchored action '" + action.StableId + "' requires a registered CombatActionTimeline.",
                    request.TraceId);
            }

            CharacterActionDiagnostic[] validationDiagnostics = CharacterActionValidation.ValidateActionConfig(action, timeline);
            if (HasErrors(validationDiagnostics))
            {
                return CharacterActionResolveResult.Rejected(
                    ResolveActionConfigRejectReason(validationDiagnostics),
                    validationDiagnostics,
                    request.TraceId);
            }

            CharacterActionPlanDurationResult duration = CharacterActionPlanDurationResolver.Resolve(action, timeline, context.DurationPolicy);
            if (!duration.Resolved)
            {
                return CharacterActionResolveResult.Rejected(
                    action.TimelineAuthority == CharacterActionTimelineAuthority.CombatAnchored
                        ? CharacterActionRejectReason.CombatActionMissing
                        : CharacterActionRejectReason.MissingActionConfig,
                    duration.Diagnostics,
                    request.TraceId);
            }

            CharacterActionPlan plan = CreatePlan(action, duration.DurationFrames, request.TraceId);
            List<CharacterActionDiagnostic> diagnostics = new List<CharacterActionDiagnostic>();
            diagnostics.AddRange(duration.Diagnostics);
            diagnostics.AddRange(validationDiagnostics);

            if (context.State.HasActiveAction)
            {
                if (context.State.ActiveActionBlocksImmediateStart)
                {
                    if (allowQueue)
                    {
                        diagnostics.Add(CharacterActionDiagnostic.Info(
                            CharacterActionDiagnosticCodes.ActionQueued,
                            "Action queued because an active action blocks immediate start."));
                        return CharacterActionResolveResult.Queued(plan, diagnostics.ToArray());
                    }

                    return Reject(
                        CharacterActionRejectReason.LowerPriorityRejected,
                        CharacterActionDiagnosticCodes.ActionLowerPriorityRejected,
                        "Active action blocks immediate start and selected binding does not allow queue.",
                        request.TraceId);
                }

                CharacterCancelConflictResult cancel = CharacterCancelConflictClassifier.Classify(
                    context.State.ActiveActionAuthority,
                    context.State.ActiveCancelRules,
                    context.State.ActiveCombatTimeline,
                    context.State.ActiveActionLocalFrame,
                    action.Id,
                    request.SourceKind);

                if (!cancel.Allowed)
                {
                    return CharacterActionResolveResult.Rejected(
                        CharacterActionRejectReason.CancelConflict,
                        new[]
                        {
                            CharacterActionDiagnostic.Error(
                                cancel.Code,
                                "Active action '" + context.State.ActiveActionId + "' rejected cancel to action '" + action.StableId + "'.")
                        },
                        request.TraceId);
                }
            }

            return CharacterActionResolveResult.Success(plan, diagnostics.ToArray());
        }

        private CharacterActionPlan CreatePlan(CharacterActionConfig action, int durationFrames, string traceId)
        {
            return new CharacterActionPlan(
                _nextPlanId++,
                action.StableId,
                action.Category,
                action.Priority,
                durationFrames,
                action.Phases,
                CharacterActionTrackPlan.FromConfig(action),
                traceId);
        }

        private static CharacterActionResolveResult Reject(
            CharacterActionRejectReason reason,
            string code,
            string message,
            string traceId)
        {
            return CharacterActionResolveResult.Rejected(
                reason,
                new[] { CharacterActionDiagnostic.Error(code, message) },
                traceId);
        }

        private static CandidateBuildResult BuildCommandCandidates(
            CharacterActionResolverContext context,
            CharacterActionIntentRequest request)
        {
            var result = new CandidateBuildResult();
            bool hasMatchingBinding = false;
            int highestBindingPriority = int.MinValue;
            for (int i = 0; i < context.ActionSet.CommandBindings.Length; i++)
            {
                CharacterActionBinding binding = context.ActionSet.CommandBindings[i];
                if (binding == null || !string.Equals(binding.IntentId, request.IntentId, StringComparison.Ordinal))
                    continue;

                hasMatchingBinding = true;
                if (binding.Priority > highestBindingPriority)
                    highestBindingPriority = binding.Priority;
            }

            if (!hasMatchingBinding)
                return result;

            for (int i = 0; i < context.ActionSet.CommandBindings.Length; i++)
            {
                CharacterActionBinding binding = context.ActionSet.CommandBindings[i];
                if (binding == null || !string.Equals(binding.IntentId, request.IntentId, StringComparison.Ordinal))
                    continue;
                if (binding.Priority != highestBindingPriority)
                    continue;

                CharacterActionConfig action = FindAction(context.Actions, binding.ActionId);
                if (action == null)
                {
                    result.Diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.MissingActionConfig,
                        "Command binding action '" + binding.ActionId + "' is missing."));
                    continue;
                }

                result.Candidates.Add(new ResolverCandidate(
                    action,
                    new CharacterActionCandidate(
                        action.StableId,
                        request.SourceKind,
                        GetSourcePriority(request.SourceKind),
                        request.Priority,
                        binding.Priority,
                        action.Priority,
                        i,
                        action.StableId,
                        binding.AllowQueue,
                        binding.QueueWindowFrames)));
            }

            SortCandidates(result.Candidates);
            return result;
        }

        private static CandidateBuildResult BuildAbilityCandidates(
            CharacterActionResolverContext context,
            CharacterActionIntentRequest request,
            int abilityId)
        {
            var result = new CandidateBuildResult();
            for (int i = 0; i < context.ActionSet.AbilityBindings.Length; i++)
            {
                CharacterAbilityActionBinding binding = context.ActionSet.AbilityBindings[i];
                if (binding == null || binding.AbilityId != abilityId)
                    continue;

                CharacterActionConfig action = FindAction(context.Actions, binding.ActionId);
                if (action == null)
                {
                    result.Diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.MissingActionConfig,
                        "Ability binding action '" + binding.ActionId + "' is missing."));
                    continue;
                }

                CharacterActionDiagnostic tagDiagnostic;
                if (!TagsMatch(binding.RequiredTags, binding.ForbiddenTags, action.Tags, context.ContextTags, out tagDiagnostic))
                {
                    result.Diagnostics.Add(tagDiagnostic);
                    continue;
                }

                result.Candidates.Add(new ResolverCandidate(
                    action,
                    new CharacterActionCandidate(
                        action.StableId,
                        request.SourceKind,
                        GetSourcePriority(request.SourceKind),
                        request.Priority,
                        0,
                        action.Priority,
                        i,
                        action.StableId,
                        allowQueue: false,
                        queueWindowFrames: 0)));
            }

            SortCandidates(result.Candidates);
            return result;
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

        private static CharacterReactionProfile FindReactionProfile(CharacterActionResolverContext context, string profileId)
        {
            for (int i = 0; i < context.ReactionProfiles.Length; i++)
            {
                CharacterReactionProfile profile = context.ReactionProfiles[i];
                if (profile != null && string.Equals(profile.StableId, profileId, StringComparison.Ordinal))
                    return profile;
            }

            return null;
        }

        private static CombatActionTimeline FindCombatTimeline(CharacterActionResolverContext context, CharacterActionConfig action)
        {
            if (action.TimelineAuthority != CharacterActionTimelineAuthority.CombatAnchored)
                return null;

            if (string.IsNullOrEmpty(action.CombatTrack.CombatActionId))
                return null;

            for (int i = 0; i < context.CombatTimelineBindings.Length; i++)
            {
                CharacterActionCombatTimelineBinding binding = context.CombatTimelineBindings[i];
                if (binding.Timeline != null
                    && string.Equals(binding.CombatActionId, action.CombatTrack.CombatActionId, StringComparison.Ordinal))
                {
                    return binding.Timeline;
                }
            }

            return null;
        }

        private static bool HasErrors(CharacterActionDiagnostic[] diagnostics)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error)
                    return true;
            }

            return false;
        }

        private static bool HasErrors(List<CharacterActionDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error)
                    return true;
            }

            return false;
        }

        private static CharacterActionRejectReason ResolveReactionProfileRejectReason(CharacterActionDiagnostic[] diagnostics)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Severity != CharacterActionDiagnosticSeverity.Error)
                    continue;

                if (diagnostics[i].Code == CharacterActionDiagnosticCodes.MissingActionConfig)
                    return CharacterActionRejectReason.MissingActionConfig;
                if (diagnostics[i].Code == CharacterActionDiagnosticCodes.ReactionRuleNoTarget)
                    return CharacterActionRejectReason.InvalidTarget;
            }

            return CharacterActionRejectReason.ReactionContextIncomplete;
        }

        private static CharacterActionRejectReason ResolveActionConfigRejectReason(CharacterActionDiagnostic[] diagnostics)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Severity != CharacterActionDiagnosticSeverity.Error)
                    continue;

                string code = diagnostics[i].Code;
                if (code == CharacterActionDiagnosticCodes.PhaseCombatAnchorMissing
                    || code == CharacterActionDiagnosticCodes.PhaseCombatRangeMismatch
                    || code == CharacterActionDiagnosticCodes.PhaseOverlap
                    || code == CharacterActionDiagnosticCodes.PhaseGap
                    || code == CharacterActionDiagnosticCodes.PhaseRangeOutsideDuration
                    || code == CharacterActionDiagnosticCodes.CombatTraceOutsideActivePhase
                    || code == CharacterActionDiagnosticCodes.TrackEventOutsideDuration)
                {
                    return CharacterActionRejectReason.PhaseAnchorInvalid;
                }

                if (code == CharacterActionDiagnosticCodes.InvalidCancelWindow
                    || code == CharacterActionDiagnosticCodes.CancelTargetMissing
                    || code == CharacterActionDiagnosticCodes.InterruptTargetMissing
                    || code == CharacterActionDiagnosticCodes.CharacterCancelRejected
                    || code == CharacterActionDiagnosticCodes.CombatCancelRejected
                    || code == CharacterActionDiagnosticCodes.CharacterCombatCancelConflict)
                {
                    return CharacterActionRejectReason.CancelConflict;
                }

                if (code == CharacterActionDiagnosticCodes.CombatActionMissing)
                    return CharacterActionRejectReason.CombatActionMissing;
                if (code == CharacterActionDiagnosticCodes.AnimationActionMissing)
                    return CharacterActionRejectReason.AnimationActionMissing;
                if (code == CharacterActionDiagnosticCodes.ResourceMissing
                    || code == CharacterActionDiagnosticCodes.ResourceCostWithoutResourceId
                    || code == CharacterActionDiagnosticCodes.PresentationResourceMissing
                    || code == CharacterActionDiagnosticCodes.AudioCueMissing)
                {
                    return CharacterActionRejectReason.ResourceMissing;
                }

                if (code == CharacterActionDiagnosticCodes.MissingActionConfig)
                    return CharacterActionRejectReason.MissingActionConfig;
            }

            return CharacterActionRejectReason.MissingActionConfig;
        }

        private static CharacterActionRejectReason ResolveCandidateRejectReason(List<CharacterActionDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity != CharacterActionDiagnosticSeverity.Error)
                    continue;

                string code = diagnostics[i].Code;
                if (code == CharacterActionDiagnosticCodes.MissingActionConfig)
                    return CharacterActionRejectReason.MissingActionConfig;
                if (code == CharacterActionDiagnosticCodes.AbilityRequiredTagMissing
                    || code == CharacterActionDiagnosticCodes.AbilityForbiddenTagMatched)
                {
                    return CharacterActionRejectReason.EquipmentStateMismatch;
                }
            }

            return CharacterActionRejectReason.MissingActionBinding;
        }

        private static bool TagsMatch(
            string[] requiredTags,
            string[] forbiddenTags,
            string[] actionTags,
            string[] contextTags,
            out CharacterActionDiagnostic diagnostic)
        {
            requiredTags = requiredTags ?? Array.Empty<string>();
            forbiddenTags = forbiddenTags ?? Array.Empty<string>();
            actionTags = actionTags ?? Array.Empty<string>();
            contextTags = contextTags ?? Array.Empty<string>();

            for (int i = 0; i < requiredTags.Length; i++)
            {
                string tag = requiredTags[i] ?? string.Empty;
                if (tag.Length == 0)
                    continue;
                if (!ContainsTag(actionTags, tag) && !ContainsTag(contextTags, tag))
                {
                    diagnostic = CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.AbilityRequiredTagMissing,
                        "Ability binding required tag '" + tag + "' is missing.");
                    return false;
                }
            }

            for (int i = 0; i < forbiddenTags.Length; i++)
            {
                string tag = forbiddenTags[i] ?? string.Empty;
                if (tag.Length == 0)
                    continue;
                if (ContainsTag(actionTags, tag) || ContainsTag(contextTags, tag))
                {
                    diagnostic = CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.AbilityForbiddenTagMatched,
                        "Ability binding forbidden tag '" + tag + "' matched.");
                    return false;
                }
            }

            diagnostic = default;
            return true;
        }

        private static bool ContainsTag(string[] tags, string tag)
        {
            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static int GetSourcePriority(CharacterActionSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case CharacterActionSourceKind.PlayerIntervention:
                    return 1000;
                case CharacterActionSourceKind.Reaction:
                case CharacterActionSourceKind.Death:
                case CharacterActionSourceKind.PostureBreak:
                case CharacterActionSourceKind.GuardBreak:
                case CharacterActionSourceKind.ArmorBreak:
                case CharacterActionSourceKind.Hit:
                    return 900;
                case CharacterActionSourceKind.Debug:
                    return 800;
                case CharacterActionSourceKind.LocalInput:
                case CharacterActionSourceKind.Command:
                    return 600;
                case CharacterActionSourceKind.GameplayAbility:
                    return 550;
                case CharacterActionSourceKind.RuntimeAiPlanner:
                    return 500;
                case CharacterActionSourceKind.Scripted:
                    return 450;
                case CharacterActionSourceKind.Replay:
                    return 400;
                default:
                    return 0;
            }
        }

        private static void SortCandidates(List<ResolverCandidate> candidates)
        {
            candidates.Sort(CompareCandidates);
        }

        private static int CompareCandidates(ResolverCandidate left, ResolverCandidate right)
        {
            int compare = right.Candidate.SourcePriority.CompareTo(left.Candidate.SourcePriority);
            if (compare != 0)
                return compare;
            compare = right.Candidate.RequestPriority.CompareTo(left.Candidate.RequestPriority);
            if (compare != 0)
                return compare;
            compare = right.Candidate.BindingPriority.CompareTo(left.Candidate.BindingPriority);
            if (compare != 0)
                return compare;
            compare = right.Candidate.ActionPriority.CompareTo(left.Candidate.ActionPriority);
            if (compare != 0)
                return compare;
            compare = left.Candidate.SourceOrder.CompareTo(right.Candidate.SourceOrder);
            if (compare != 0)
                return compare;
            return string.CompareOrdinal(left.Candidate.StableTieBreaker, right.Candidate.StableTieBreaker);
        }

        private sealed class CandidateBuildResult
        {
            public CandidateBuildResult()
            {
                Candidates = new List<ResolverCandidate>();
                Diagnostics = new List<CharacterActionDiagnostic>();
            }

            public List<ResolverCandidate> Candidates { get; }
            public List<CharacterActionDiagnostic> Diagnostics { get; }
        }

        private readonly struct ResolverCandidate
        {
            public ResolverCandidate(CharacterActionConfig action, CharacterActionCandidate candidate)
            {
                Action = action;
                Candidate = candidate;
            }

            public CharacterActionConfig Action { get; }
            public CharacterActionCandidate Candidate { get; }
        }
    }
}

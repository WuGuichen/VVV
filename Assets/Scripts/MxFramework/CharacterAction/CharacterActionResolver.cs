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
            CharacterActionCombatTimelineBinding[] combatTimelineBindings = null)
        {
            ActionSet = actionSet;
            Actions = actions ?? Array.Empty<CharacterActionConfig>();
            ReactionProfiles = reactionProfiles ?? Array.Empty<CharacterReactionProfile>();
            CombatTimelines = combatTimelines ?? Array.Empty<CombatActionTimeline>();
            CombatTimelineBindings = combatTimelineBindings ?? CreateTimelineBindings(CombatTimelines);
            State = state;
            DurationPolicy = durationPolicy;
        }

        public CharacterActionSetConfig ActionSet { get; }
        public CharacterActionConfig[] Actions { get; }
        public CharacterReactionProfile[] ReactionProfiles { get; }
        public CombatActionTimeline[] CombatTimelines { get; }
        public CharacterActionCombatTimelineBinding[] CombatTimelineBindings { get; }
        public CharacterActionResolverState State { get; }
        public CharacterActionDurationPolicy DurationPolicy { get; }

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

            CharacterActionBinding binding = FindCommandBinding(context.ActionSet, request.IntentId);
            if (binding == null)
            {
                return Reject(
                    CharacterActionRejectReason.MissingActionBinding,
                    CharacterActionDiagnosticCodes.MissingActionBinding,
                    "No command binding found for intent '" + request.IntentId + "'.",
                    request.TraceId);
            }

            return ResolveBoundAction(context, request, binding.ActionId, binding.AllowQueue);
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

            CharacterAbilityActionBinding binding = FindAbilityBinding(context.ActionSet, request.AbilityId.Value);
            if (binding == null)
            {
                return Reject(
                    CharacterActionRejectReason.MissingAbilityBinding,
                    CharacterActionDiagnosticCodes.MissingAbilityBinding,
                    "No ability binding found for ability " + request.AbilityId.Value + ".",
                    request.TraceId);
            }

            return ResolveBoundAction(context, request, binding.ActionId, allowQueue: false);
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

            CharacterActionDiagnostic[] profileDiagnostics = CharacterReactionRuleValidator.ValidatePressureOnlyProfile(profile);
            if (HasErrors(profileDiagnostics))
            {
                return CharacterActionResolveResult.Rejected(
                    CharacterActionRejectReason.ReactionContextIncomplete,
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

            return ResolveBoundAction(context, request, selection.SelectedActionId, allowQueue: false);
        }

        private CharacterActionResolveResult ResolveBoundAction(
            CharacterActionResolverContext context,
            CharacterActionIntentRequest request,
            string actionId,
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

            CharacterActionConfig action = FindAction(context.Actions, actionId);
            if (action == null)
            {
                return Reject(
                    CharacterActionRejectReason.MissingActionConfig,
                    CharacterActionDiagnosticCodes.MissingActionConfig,
                    "Action config '" + actionId + "' is missing.",
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
                    CharacterActionRejectReason.PhaseAnchorInvalid,
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
                        CharacterActionRejectReason.ActionCommitted,
                        CharacterActionDiagnosticCodes.CharacterCancelRejected,
                        "Active action blocks immediate start and binding does not allow queue.",
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

        private static CharacterActionBinding FindCommandBinding(CharacterActionSetConfig actionSet, string intentId)
        {
            CharacterActionBinding best = null;
            for (int i = 0; i < actionSet.CommandBindings.Length; i++)
            {
                CharacterActionBinding binding = actionSet.CommandBindings[i];
                if (binding == null || !string.Equals(binding.IntentId, intentId, StringComparison.Ordinal))
                    continue;
                if (best == null || binding.Priority > best.Priority)
                    best = binding;
            }

            return best;
        }

        private static CharacterAbilityActionBinding FindAbilityBinding(CharacterActionSetConfig actionSet, int abilityId)
        {
            for (int i = 0; i < actionSet.AbilityBindings.Length; i++)
            {
                CharacterAbilityActionBinding binding = actionSet.AbilityBindings[i];
                if (binding != null && binding.AbilityId == abilityId)
                    return binding;
            }

            return null;
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
    }
}

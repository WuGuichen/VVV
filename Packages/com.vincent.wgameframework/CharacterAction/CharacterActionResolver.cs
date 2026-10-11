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
                    : new CharacterActionCombatTimelineBinding(timeline.ActionId, timeline);
            }

            return bindings;
        }
    }

    public readonly struct CharacterActionCombatTimelineBinding : IEquatable<CharacterActionCombatTimelineBinding>
    {
        public CharacterActionCombatTimelineBinding(int combatActionId, CombatActionTimeline timeline)
        {
            CombatActionId = combatActionId;
            Timeline = timeline;
        }

        public int CombatActionId { get; }
        public CombatActionTimeline Timeline { get; }

        public bool Equals(CharacterActionCombatTimelineBinding other)
        {
            return (CombatActionId == other.CombatActionId)
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
                return (CombatActionId * 397)
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
            CharacterActionId activeActionId = default,
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
            ActiveActionId = activeActionId;
            ActiveActionLocalFrame = activeActionLocalFrame;
            ActiveActionAuthority = activeActionAuthority;
            ActiveCancelRules = activeCancelRules ?? Array.Empty<CharacterCancelRule>();
            ActiveCombatTimeline = activeCombatTimeline;
        }

        public bool IsDisabled { get; }
        public bool IsDead { get; }
        public bool HasActiveAction { get; }
        public bool ActiveActionBlocksImmediateStart { get; }
        public CharacterActionId ActiveActionId { get; }
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
                    CharacterActionDiagnosticCode.MissingActionSet,
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
                    CharacterActionDiagnosticCode.MissingActionBinding,
                    "No command binding found for intent '" + request.IntentId + "'.",
                    request.TraceId);
            }

            ResolverCandidate candidate = candidates.Candidates[0];
            return ResolveBoundAction(
                context,
                request,
                candidate.Action,
                candidate.Candidate.AllowQueue,
                candidates.Diagnostics.ToArray());
        }

        public CharacterActionResolveResult ResolveActionSlot(
            CharacterActionResolverContext context,
            CharacterActionIntentRequest request)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.ActionSet == null)
            {
                return Reject(
                    CharacterActionRejectReason.MissingActionSet,
                    CharacterActionDiagnosticCode.MissingActionSet,
                    "Character action set is missing.",
                    request.TraceId);
            }

            if (request.ActionSlotId <= 0)
            {
                return Reject(
                    CharacterActionRejectReason.ActionSlotBlocked,
                    CharacterActionDiagnosticCode.ActionSlotBlocked,
                    "Action slot request requires a positive actionSlotId.",
                    request.TraceId);
            }

            if (!request.RequestedActionId.IsValid)
                return ResolveEmptyActionSlot(context, request);

            CharacterActionConfig action = FindAction(context.Actions, request.RequestedActionId);
            if (action == null)
            {
                return Reject(
                    CharacterActionRejectReason.MissingActionConfig,
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "Action slot target action '" + request.RequestedActionId + "' is missing.",
                    request.TraceId);
            }

            return ResolveBoundAction(context, request, action, allowQueue: false);
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
                    CharacterActionDiagnosticCode.MissingActionSet,
                    "Character action set is missing.",
                    request.TraceId);
            }

            if (!request.AbilityId.HasValue)
            {
                return Reject(
                    CharacterActionRejectReason.MissingAbilityBinding,
                    CharacterActionDiagnosticCode.MissingAbilityBinding,
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
                    CharacterActionDiagnosticCode.MissingAbilityBinding,
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

            RuntimeTraceIdentity traceId = reactionContext.TraceId;
            if (context.ActionSet == null)
            {
                return Reject(
                    CharacterActionRejectReason.MissingActionSet,
                    CharacterActionDiagnosticCode.MissingActionSet,
                    "Character action set is missing.",
                    traceId);
            }

            CharacterReactionProfile profile = FindReactionProfile(context, context.ActionSet.ReactionProfileId);
            if (profile == null)
            {
                return Reject(
                    CharacterActionRejectReason.ReactionContextIncomplete,
                    CharacterActionDiagnosticCode.MissingReactionProfile,
                    "Reaction profile '" + context.ActionSet.ReactionProfileId + "' is missing.",
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
                intentId: 0,
                abilityId: null,
                abilityStableId: 0,
                requestedActionId: selection.SelectedActionId,
                sourceKind: CharacterActionSourceKind.Reaction,
                priority: int.MaxValue,
                frame: reactionContext.Frame,
                traceId: traceId);

            CharacterActionConfig action = FindAction(context.Actions, selection.SelectedActionId);
            CharacterActionDiagnostic targetDiagnostic;
            if (!TryValidateSelectedReactionAction(selection.SelectedActionId, action, out targetDiagnostic))
            {
                return CharacterActionResolveResult.Rejected(
                    ResolveReactionTargetRejectReason(targetDiagnostic),
                    MergeDiagnostics(new[] { targetDiagnostic }, selection.Diagnostics),
                    traceId);
            }

            return ResolveBoundAction(context, request, action, allowQueue: false, selection.Diagnostics);
        }

        private CharacterActionResolveResult ResolveEmptyActionSlot(
            CharacterActionResolverContext context,
            CharacterActionIntentRequest request)
        {
            CharacterActionDiagnostic diagnostic;
            if (!TryValidateActionSlotWindow(
                    context,
                    request,
                    targetActionId: CharacterActionId.None,
                    CharacterActionCategory.None,
                    out diagnostic))
            {
                return CharacterActionResolveResult.Rejected(
                    CharacterActionRejectReason.ActionSlotBlocked,
                    new[] { diagnostic },
                    request.TraceId);
            }

            return Reject(
                CharacterActionRejectReason.ActionSlotEmpty,
                CharacterActionDiagnosticCode.ActionSlotEmpty,
                "Action slot " + request.ActionSlotId + " has no resolved action.",
                request.TraceId);
        }

        private CharacterActionResolveResult ResolveBoundAction(
            CharacterActionResolverContext context,
            CharacterActionIntentRequest request,
            CharacterActionConfig action,
            bool allowQueue,
            CharacterActionDiagnostic[] preDiagnostics = null)
        {
            if (context.State.IsDisabled)
            {
                return Reject(
                    CharacterActionRejectReason.StateDisabled,
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "Character action resolver state is disabled.",
                    request.TraceId);
            }

            if (context.State.IsDead && request.SourceKind != CharacterActionSourceKind.Reaction)
            {
                return Reject(
                    CharacterActionRejectReason.Dead,
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "Dead characters cannot resolve non-reaction actions.",
                    request.TraceId);
            }

            if (action == null)
            {
                return Reject(
                    CharacterActionRejectReason.MissingActionConfig,
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "Action config is missing.",
                    request.TraceId);
            }

            CombatActionTimeline timeline = FindCombatTimeline(context, action);
            if (action.TimelineAuthority == CharacterActionTimelineAuthority.CombatAnchored && timeline == null)
            {
                return Reject(
                    CharacterActionRejectReason.CombatActionMissing,
                    CharacterActionDiagnosticCode.CombatActionMissing,
                    "CombatAnchored action '" + action.ActionId + "' requires a registered CombatActionTimeline.",
                    request.TraceId);
            }

            CharacterActionDiagnostic[] validationDiagnostics = CharacterActionValidation.ValidateActionConfig(action, timeline);
            if (HasErrors(validationDiagnostics))
            {
                return CharacterActionResolveResult.Rejected(
                    ResolveActionConfigRejectReason(validationDiagnostics),
                    MergeDiagnostics(preDiagnostics, validationDiagnostics),
                    request.TraceId);
            }

            CharacterActionPlanDurationResult duration = CharacterActionPlanDurationResolver.Resolve(action, timeline, context.DurationPolicy);
            if (!duration.Resolved)
            {
                return CharacterActionResolveResult.Rejected(
                    action.TimelineAuthority == CharacterActionTimelineAuthority.CombatAnchored
                        ? CharacterActionRejectReason.CombatActionMissing
                        : CharacterActionRejectReason.MissingActionConfig,
                    MergeDiagnostics(preDiagnostics, duration.Diagnostics),
                    request.TraceId);
            }

            CharacterActionPlan plan = CreatePlan(action, duration.DurationFrames, request.TraceId);
            List<CharacterActionDiagnostic> diagnostics = new List<CharacterActionDiagnostic>();
            AddDiagnostics(diagnostics, preDiagnostics);
            diagnostics.AddRange(duration.Diagnostics);
            diagnostics.AddRange(validationDiagnostics);

            if (request.ActionSlotId > 0)
            {
                CharacterActionDiagnostic slotDiagnostic;
                if (!TryValidateActionSlotWindow(context, request, action.ActionId, action.Category, out slotDiagnostic))
                {
                    return CharacterActionResolveResult.Rejected(
                        CharacterActionRejectReason.ActionSlotBlocked,
                        MergeDiagnostics(preDiagnostics, new[] { slotDiagnostic }),
                        request.TraceId);
                }
            }

            if (context.State.HasActiveAction && request.ActionSlotId <= 0)
            {
                if (context.State.ActiveActionBlocksImmediateStart)
                {
                    if (allowQueue)
                    {
                        diagnostics.Add(CharacterActionDiagnostic.Info(
                            CharacterActionDiagnosticCode.ActionQueued,
                            "Action queued because an active action blocks immediate start."));
                        return CharacterActionResolveResult.Queued(plan, diagnostics.ToArray());
                    }

                    return Reject(
                        CharacterActionRejectReason.LowerPriorityRejected,
                        CharacterActionDiagnosticCode.ActionLowerPriorityRejected,
                        "Active action blocks immediate start and selected binding does not allow queue.",
                        request.TraceId);
                }

                CharacterCancelConflictResult cancel = CharacterCancelConflictClassifier.Classify(
                    context.State.ActiveActionAuthority,
                    context.State.ActiveCancelRules,
                    context.State.ActiveCombatTimeline,
                    context.State.ActiveActionLocalFrame,
                    action.ActionId,
                    action.Category,
                    request.ActionSlotId);

                if (!cancel.Allowed)
                {
                    CharacterActionDiagnostic[] cancelDiagnostics =
                    {
                        CharacterActionDiagnostic.Error(
                            cancel.CodeId,
                            "Active action '" + context.State.ActiveActionId + "' rejected cancel to action '" + action.ActionId + "'.")
                    };

                    return CharacterActionResolveResult.Rejected(
                        CharacterActionRejectReason.CancelConflict,
                        MergeDiagnostics(preDiagnostics, cancelDiagnostics),
                        request.TraceId);
                }
            }

            return CharacterActionResolveResult.Success(plan, diagnostics.ToArray());
        }

        private static bool TryValidateActionSlotWindow(
            CharacterActionResolverContext context,
            CharacterActionIntentRequest request,
            CharacterActionId targetActionId,
            CharacterActionCategory requestCategory,
            out CharacterActionDiagnostic diagnostic)
        {
            if (request.ActionSlotId <= 0)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.ActionSlotBlocked,
                    "Action slot request requires a positive actionSlotId.");
                return false;
            }

            if (!context.State.HasActiveAction)
            {
                if ((context.ActionSet.DefaultActionSlotMask & request.ActionSlotId) != 0)
                {
                    diagnostic = default(CharacterActionDiagnostic);
                    return true;
                }

                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.ActionSlotBlocked,
                    "Default action slot mask rejected action slot " + request.ActionSlotId + ".");
                return false;
            }

            CharacterCancelConflictResult cancel = CharacterCancelConflictClassifier.Classify(
                context.State.ActiveActionAuthority,
                context.State.ActiveCancelRules,
                context.State.ActiveCombatTimeline,
                context.State.ActiveActionLocalFrame,
                targetActionId,
                requestCategory,
                request.ActionSlotId);

            if (cancel.Allowed)
            {
                diagnostic = default(CharacterActionDiagnostic);
                return true;
            }

            diagnostic = CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCode.ActionSlotBlocked,
                "Active action '" + context.State.ActiveActionId + "' rejected action slot " + request.ActionSlotId + ".");
            return false;
        }

        private CharacterActionPlan CreatePlan(CharacterActionConfig action, int durationFrames, RuntimeTraceIdentity traceId)
        {
            return new CharacterActionPlan(
                _nextPlanId++,
                action.ActionId,
                action.Category,
                action.Priority,
                durationFrames,
                action.Phases,
                CharacterActionTrackPlan.FromConfig(action),
                traceId);
        }

        private static CharacterActionResolveResult Reject(
            CharacterActionRejectReason reason,
            CharacterActionDiagnosticCode code,
            string message,
            RuntimeTraceIdentity traceId)
        {
            return CharacterActionResolveResult.Rejected(
                reason,
                new[] { CharacterActionDiagnostic.Error(code, message) },
                traceId);
        }

        private static bool TryValidateSelectedReactionAction(
            CharacterActionId actionId,
            CharacterActionConfig action,
            out CharacterActionDiagnostic diagnostic)
        {
            if (!actionId.IsValid)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.ReactionRuleNoTarget,
                    "Reaction rule action id is missing.");
                return false;
            }

            if (action == null)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "Reaction action '" + actionId + "' is missing.");
                return false;
            }

            if (action.Category != CharacterActionCategory.Reaction)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.ReactionRuleNoTarget,
                    "Reaction action '" + actionId + "' must use CharacterActionCategory.Reaction.");
                return false;
            }

            diagnostic = default;
            return true;
        }

        private static CharacterActionRejectReason ResolveReactionTargetRejectReason(CharacterActionDiagnostic diagnostic)
        {
            return diagnostic.CodeId == CharacterActionDiagnosticCode.MissingActionConfig
                ? CharacterActionRejectReason.MissingActionConfig
                : CharacterActionRejectReason.InvalidTarget;
        }

        private static CharacterActionDiagnostic[] MergeDiagnostics(
            CharacterActionDiagnostic[] first,
            CharacterActionDiagnostic[] second)
        {
            first = first ?? Array.Empty<CharacterActionDiagnostic>();
            second = second ?? Array.Empty<CharacterActionDiagnostic>();

            if (first.Length == 0)
                return second;
            if (second.Length == 0)
                return first;

            var merged = new CharacterActionDiagnostic[first.Length + second.Length];
            Array.Copy(first, 0, merged, 0, first.Length);
            Array.Copy(second, 0, merged, first.Length, second.Length);
            return merged;
        }

        private static void AddDiagnostics(
            List<CharacterActionDiagnostic> target,
            CharacterActionDiagnostic[] diagnostics)
        {
            if (diagnostics == null || diagnostics.Length == 0)
                return;

            target.AddRange(diagnostics);
        }

        private static CandidateBuildResult BuildCommandCandidates(
            CharacterActionResolverContext context,
            CharacterActionIntentRequest request)
        {
            var result = new CandidateBuildResult();
            CharacterActionCommandSequence matchingSequence = null;
            int matchingSequenceOrder = -1;
            for (int i = 0; i < context.ActionSet.CommandSequences.Length; i++)
            {
                CharacterActionCommandSequence sequence = context.ActionSet.CommandSequences[i];
                if (sequence == null || !(sequence.IntentId == request.IntentId))
                    continue;

                if (matchingSequence == null || sequence.Priority > matchingSequence.Priority)
                {
                    matchingSequence = sequence;
                    matchingSequenceOrder = i;
                }
            }

            bool hasMatchingBinding = false;
            int highestBindingPriority = int.MinValue;
            for (int i = 0; i < context.ActionSet.CommandBindings.Length; i++)
            {
                CharacterActionBinding binding = context.ActionSet.CommandBindings[i];
                if (binding == null || !(binding.IntentId == request.IntentId))
                    continue;

                hasMatchingBinding = true;
                if (binding.Priority > highestBindingPriority)
                    highestBindingPriority = binding.Priority;
            }

            if (matchingSequence != null && hasMatchingBinding)
            {
                result.Diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CommandIntentConflict,
                    "Intent '" + request.IntentId + "' exists in both commandBindings and commandSequences."));
                return result;
            }

            if (matchingSequence != null)
            {
                AddCommandSequenceCandidate(context, request, matchingSequence, matchingSequenceOrder, result);
                return result;
            }

            if (!hasMatchingBinding)
                return result;

            for (int i = 0; i < context.ActionSet.CommandBindings.Length; i++)
            {
                CharacterActionBinding binding = context.ActionSet.CommandBindings[i];
                if (binding == null || !(binding.IntentId == request.IntentId))
                    continue;
                if (binding.Priority != highestBindingPriority)
                    continue;

                CharacterActionConfig action = FindAction(context.Actions, binding.ActionId);
                if (action == null)
                {
                    result.Diagnostics.Add(CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.MissingActionConfig,
                        "Command binding action '" + binding.ActionId + "' is missing."));
                    continue;
                }

                result.Candidates.Add(new ResolverCandidate(
                    action,
                    new CharacterActionCandidate(
                        action.ActionId,
                        request.SourceKind,
                        GetSourcePriority(request.SourceKind),
                        request.Priority,
                        binding.Priority,
                        action.Priority,
                        i,
                        action.ActionId,
                        binding.AllowQueue,
                        binding.QueueWindowFrames)));
            }

            SortCandidates(result.Candidates);
            return result;
        }

        private static void AddCommandSequenceCandidate(
            CharacterActionResolverContext context,
            CharacterActionIntentRequest request,
            CharacterActionCommandSequence sequence,
            int sourceOrder,
            CandidateBuildResult result)
        {
            if (sequence.ActionIds.Length == 0)
            {
                result.Diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CommandSequenceInvalid,
                    "Command sequence '" + sequence.IntentId + "' has no actions."));
                return;
            }

            int targetIndex = 0;
            if (context.State.HasActiveAction)
            {
                int activeIndex = IndexOf(sequence.ActionIds, context.State.ActiveActionId);
                if (activeIndex >= 0)
                {
                    if (activeIndex + 1 < sequence.ActionIds.Length)
                    {
                        targetIndex = activeIndex + 1;
                    }
                    else if (sequence.WrapAfterLast)
                    {
                        targetIndex = 0;
                    }
                    else
                    {
                        result.Diagnostics.Add(CharacterActionDiagnostic.Error(
                            CharacterActionDiagnosticCode.CommandSequenceExhausted,
                            "Command sequence '" + sequence.IntentId + "' is already at its final action."));
                        return;
                    }
                }
            }

            CharacterActionId actionId = sequence.ActionIds[targetIndex];
            CharacterActionConfig action = FindAction(context.Actions, actionId);
            if (action == null)
            {
                result.Diagnostics.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "Command sequence action '" + actionId + "' is missing."));
                return;
            }

            result.Candidates.Add(new ResolverCandidate(
                action,
                new CharacterActionCandidate(
                    action.ActionId,
                    request.SourceKind,
                    GetSourcePriority(request.SourceKind),
                    request.Priority,
                    sequence.Priority,
                    action.Priority,
                    sourceOrder,
                    action.ActionId,
                    sequence.AllowQueue,
                    sequence.QueueWindowFrames)));
        }

        private static int IndexOf(CharacterActionId[] values, CharacterActionId value)
        {
            if (values == null || !value.IsValid)
                return -1;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == value)
                    return i;
            }

            return -1;
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
                        CharacterActionDiagnosticCode.MissingActionConfig,
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
                        action.ActionId,
                        request.SourceKind,
                        GetSourcePriority(request.SourceKind),
                        request.Priority,
                        0,
                        action.Priority,
                        i,
                        action.ActionId,
                        allowQueue: false,
                        queueWindowFrames: 0)));
            }

            SortCandidates(result.Candidates);
            return result;
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

            if ((action.CombatTrack.CombatActionId <= 0))
                return null;

            for (int i = 0; i < context.CombatTimelineBindings.Length; i++)
            {
                CharacterActionCombatTimelineBinding binding = context.CombatTimelineBindings[i];
                if (binding.Timeline != null
                    && (binding.CombatActionId == action.CombatTrack.CombatActionId))
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

        private static CharacterActionRejectReason ResolveActionConfigRejectReason(CharacterActionDiagnostic[] diagnostics)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Severity != CharacterActionDiagnosticSeverity.Error)
                    continue;

                CharacterActionDiagnosticCode code = diagnostics[i].CodeId;
                if (code == CharacterActionDiagnosticCode.PhaseCombatAnchorMissing
                    || code == CharacterActionDiagnosticCode.PhaseCombatRangeMismatch
                    || code == CharacterActionDiagnosticCode.PhaseOverlap
                    || code == CharacterActionDiagnosticCode.PhaseGap
                    || code == CharacterActionDiagnosticCode.PhaseRangeOutsideDuration
                    || code == CharacterActionDiagnosticCode.CombatTraceOutsideActivePhase
                    || code == CharacterActionDiagnosticCode.TrackEventOutsideDuration)
                {
                    return CharacterActionRejectReason.PhaseAnchorInvalid;
                }

                if (code == CharacterActionDiagnosticCode.InvalidCancelWindow
                    || code == CharacterActionDiagnosticCode.CancelTargetMissing
                    || code == CharacterActionDiagnosticCode.InterruptTargetMissing
                    || code == CharacterActionDiagnosticCode.CharacterCancelRejected
                    || code == CharacterActionDiagnosticCode.CombatCancelRejected
                    || code == CharacterActionDiagnosticCode.CharacterCombatCancelConflict)
                {
                    return CharacterActionRejectReason.CancelConflict;
                }

                if (code == CharacterActionDiagnosticCode.CombatActionMissing)
                    return CharacterActionRejectReason.CombatActionMissing;
                if (code == CharacterActionDiagnosticCode.AnimationActionMissing)
                    return CharacterActionRejectReason.AnimationActionMissing;
                if (code == CharacterActionDiagnosticCode.ResourceMissing
                    || code == CharacterActionDiagnosticCode.ResourceCostWithoutResourceId
                    || code == CharacterActionDiagnosticCode.PresentationResourceMissing
                    || code == CharacterActionDiagnosticCode.AudioCueMissing)
                {
                    return CharacterActionRejectReason.ResourceMissing;
                }

                if (code == CharacterActionDiagnosticCode.MissingActionConfig)
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

                CharacterActionDiagnosticCode code = diagnostics[i].CodeId;
                if (code == CharacterActionDiagnosticCode.MissingActionConfig)
                    return CharacterActionRejectReason.MissingActionConfig;
                if (code == CharacterActionDiagnosticCode.AbilityRequiredTagMissing
                    || code == CharacterActionDiagnosticCode.AbilityForbiddenTagMatched)
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
                        CharacterActionDiagnosticCode.AbilityRequiredTagMissing,
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
                        CharacterActionDiagnosticCode.AbilityForbiddenTagMatched,
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
                case CharacterActionSourceKind.CohesionBreak:
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
            return left.Candidate.StableTieBreaker.CompareTo(right.Candidate.StableTieBreaker);
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

    /// <summary>Cached action plan, resolve result, and runner definition for a stable action/trace pair.</summary>
    /// <remarks>NoAlloc to read after the entry has been created by <see cref="CharacterActionRuntimePlanCache"/>.</remarks>
    public sealed class CharacterActionRuntimePlanEntry
    {
        internal CharacterActionRuntimePlanEntry(
            CharacterActionConfig action,
            CharacterActionPlan plan,
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionDiagnostic[] diagnostics,
            CharacterActionRejectReason rejectReason)
        {
            Action = action;
            Plan = plan;
            ResolveResult = resolveResult;
            Definition = definition;
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
            RejectReason = rejectReason;
        }

        public CharacterActionConfig Action { get; }
        public CharacterActionPlan Plan { get; }
        public CharacterActionResolveResult ResolveResult { get; }
        public CharacterActionRunnerActionDefinition Definition { get; }
        public CharacterActionDiagnostic[] Diagnostics { get; }
        public CharacterActionRejectReason RejectReason { get; }
        public bool IsSuccess => ResolveResult != null && ResolveResult.IsSuccess && Definition != null;
    }

    /// <summary>Runtime cache for action validation, plan construction, and runner definitions.</summary>
    /// <remarks>
    /// Allocates during construction and the first request for a new action/trace pair.
    /// Repeated <see cref="TryGetOrCreate"/> calls for cached pairs are NoAlloc.
    /// </remarks>
    public sealed class CharacterActionRuntimePlanCache
    {
        private readonly Dictionary<CharacterActionId, CachedAction> _actionsById;
        private readonly Dictionary<PlanKey, CharacterActionRuntimePlanEntry> _plansByTrace;

        public CharacterActionRuntimePlanCache(CharacterActionResolverContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            CharacterActionConfig[] actions = context.Actions ?? Array.Empty<CharacterActionConfig>();
            _actionsById = new Dictionary<CharacterActionId, CachedAction>(
                Math.Max(1, actions.Length));
            _plansByTrace = new Dictionary<PlanKey, CharacterActionRuntimePlanEntry>(
                Math.Max(1, actions.Length),
                PlanKeyComparer.Instance);

            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action == null || !action.ActionId.IsValid || _actionsById.ContainsKey(action.ActionId))
                    continue;

                _actionsById.Add(action.ActionId, CachedAction.Create(context, action));
            }
        }

        /// <remarks>NoAlloc after the requested action/trace pair has been cached.</remarks>
        public bool TryGetOrCreate(
            CharacterActionId actionId,
            RuntimeTraceIdentity traceId,
            out CharacterActionRuntimePlanEntry entry)
        {
            entry = null;
            if (!actionId.IsValid || !_actionsById.TryGetValue(actionId, out CachedAction action))
                return false;

            RuntimeTraceIdentity safeTraceId = traceId;
            var key = new PlanKey(actionId, safeTraceId);
            if (_plansByTrace.TryGetValue(key, out entry))
                return true;

            entry = action.CreateEntry(safeTraceId);
            _plansByTrace.Add(key, entry);
            return true;
        }

        private sealed class CachedAction
        {
            private CachedAction(
                CharacterActionConfig action,
                CharacterActionRunnerActionDefinition definition,
                CharacterActionTrackPlan[] tracks,
                int durationFrames,
                CharacterActionDiagnostic[] diagnostics,
                CharacterActionRejectReason rejectReason,
                bool success)
            {
                Action = action;
                Definition = definition;
                Tracks = tracks ?? Array.Empty<CharacterActionTrackPlan>();
                DurationFrames = durationFrames;
                Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
                RejectReason = rejectReason;
                Success = success;
            }

            public CharacterActionConfig Action { get; }
            public CharacterActionRunnerActionDefinition Definition { get; }
            public CharacterActionTrackPlan[] Tracks { get; }
            public int DurationFrames { get; }
            public CharacterActionDiagnostic[] Diagnostics { get; }
            public CharacterActionRejectReason RejectReason { get; }
            public bool Success { get; }

            public static CachedAction Create(CharacterActionResolverContext context, CharacterActionConfig action)
            {
                CombatActionTimeline timeline = FindCombatTimeline(context, action);
                if (action.TimelineAuthority == CharacterActionTimelineAuthority.CombatAnchored && timeline == null)
                {
                    return Rejected(
                        action,
                        CharacterActionRejectReason.CombatActionMissing,
                        CharacterActionDiagnostic.Error(
                            CharacterActionDiagnosticCode.CombatActionMissing,
                            "CombatAnchored action '" + action.ActionId + "' requires a registered CombatActionTimeline."));
                }

                CharacterActionDiagnostic[] validationDiagnostics =
                    CharacterActionValidation.ValidateActionConfig(action, timeline);
                if (HasErrors(validationDiagnostics))
                {
                    return new CachedAction(
                        action,
                        null,
                        null,
                        0,
                        validationDiagnostics,
                        ResolveActionConfigRejectReason(validationDiagnostics),
                        success: false);
                }

                CharacterActionPlanDurationResult duration =
                    CharacterActionPlanDurationResolver.Resolve(action, timeline, context.DurationPolicy);
                if (!duration.Resolved)
                {
                    return new CachedAction(
                        action,
                        null,
                        null,
                        0,
                        duration.Diagnostics,
                        action.TimelineAuthority == CharacterActionTimelineAuthority.CombatAnchored
                            ? CharacterActionRejectReason.CombatActionMissing
                            : CharacterActionRejectReason.MissingActionConfig,
                        success: false);
                }

                CharacterActionDiagnostic[] diagnostics = MergeDiagnostics(duration.Diagnostics, validationDiagnostics);
                return new CachedAction(
                    action,
                    CharacterActionRunnerActionDefinition.FromConfig(action, timeline),
                    CharacterActionTrackPlan.FromConfig(action),
                    duration.DurationFrames,
                    diagnostics,
                    CharacterActionRejectReason.None,
                    success: true);
            }

            public CharacterActionRuntimePlanEntry CreateEntry(RuntimeTraceIdentity traceId)
            {
                if (!Success)
                {
                    CharacterActionResolveResult rejected = CharacterActionResolveResult.Rejected(
                        RejectReason,
                        Diagnostics,
                        traceId);
                    return new CharacterActionRuntimePlanEntry(
                        Action,
                        null,
                        rejected,
                        null,
                        Diagnostics,
                        RejectReason);
                }

                var plan = new CharacterActionPlan(
                    Action.ActionId.Value,
                    Action.ActionId,
                    Action.Category,
                    Action.Priority,
                    DurationFrames,
                    Action.Phases,
                    Tracks,
                    traceId);
                CharacterActionResolveResult resolve = CharacterActionResolveResult.Success(plan, Diagnostics);
                return new CharacterActionRuntimePlanEntry(
                    Action,
                    plan,
                    resolve,
                    Definition,
                    Diagnostics,
                    CharacterActionRejectReason.None);
            }

            private static CachedAction Rejected(
                CharacterActionConfig action,
                CharacterActionRejectReason reason,
                CharacterActionDiagnostic diagnostic)
            {
                return new CachedAction(
                    action,
                    null,
                    null,
                    0,
                    new[] { diagnostic },
                    reason,
                    success: false);
            }
        }

        private readonly struct PlanKey
        {
            public PlanKey(CharacterActionId actionId, RuntimeTraceIdentity traceId)
            {
                ActionId = actionId;
                TraceId = traceId;
            }

            public CharacterActionId ActionId { get; }
            public RuntimeTraceIdentity TraceId { get; }
        }

        private sealed class PlanKeyComparer : IEqualityComparer<PlanKey>
        {
            public static readonly PlanKeyComparer Instance = new PlanKeyComparer();

            public bool Equals(PlanKey x, PlanKey y)
            {
                return x.ActionId == y.ActionId
                    && x.TraceId.Equals(y.TraceId);
            }

            public int GetHashCode(PlanKey obj)
            {
                unchecked
                {
                    return (obj.ActionId.GetHashCode() * 397)
                        ^ (obj.TraceId == null ? 0 : StringComparer.Ordinal.GetHashCode(obj.TraceId));
                }
            }
        }

        private static CombatActionTimeline FindCombatTimeline(
            CharacterActionResolverContext context,
            CharacterActionConfig action)
        {
            if (action.TimelineAuthority != CharacterActionTimelineAuthority.CombatAnchored)
                return null;

            if ((action.CombatTrack.CombatActionId <= 0))
                return null;

            for (int i = 0; i < context.CombatTimelineBindings.Length; i++)
            {
                CharacterActionCombatTimelineBinding binding = context.CombatTimelineBindings[i];
                if (binding.Timeline != null
                    && (binding.CombatActionId == action.CombatTrack.CombatActionId))
                {
                    return binding.Timeline;
                }
            }

            return null;
        }

        private static bool HasErrors(CharacterActionDiagnostic[] diagnostics)
        {
            diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error)
                    return true;
            }

            return false;
        }

        private static CharacterActionDiagnostic[] MergeDiagnostics(
            CharacterActionDiagnostic[] first,
            CharacterActionDiagnostic[] second)
        {
            first = first ?? Array.Empty<CharacterActionDiagnostic>();
            second = second ?? Array.Empty<CharacterActionDiagnostic>();

            if (first.Length == 0)
                return second;
            if (second.Length == 0)
                return first;

            var merged = new CharacterActionDiagnostic[first.Length + second.Length];
            Array.Copy(first, 0, merged, 0, first.Length);
            Array.Copy(second, 0, merged, first.Length, second.Length);
            return merged;
        }

        private static CharacterActionRejectReason ResolveActionConfigRejectReason(
            CharacterActionDiagnostic[] diagnostics)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Severity != CharacterActionDiagnosticSeverity.Error)
                    continue;

                CharacterActionDiagnosticCode code = diagnostics[i].CodeId;
                if (code == CharacterActionDiagnosticCode.PhaseCombatAnchorMissing
                    || code == CharacterActionDiagnosticCode.PhaseCombatRangeMismatch
                    || code == CharacterActionDiagnosticCode.PhaseOverlap
                    || code == CharacterActionDiagnosticCode.PhaseGap
                    || code == CharacterActionDiagnosticCode.PhaseRangeOutsideDuration
                    || code == CharacterActionDiagnosticCode.CombatTraceOutsideActivePhase
                    || code == CharacterActionDiagnosticCode.TrackEventOutsideDuration)
                {
                    return CharacterActionRejectReason.PhaseAnchorInvalid;
                }

                if (code == CharacterActionDiagnosticCode.InvalidCancelWindow
                    || code == CharacterActionDiagnosticCode.CancelTargetMissing
                    || code == CharacterActionDiagnosticCode.InterruptTargetMissing
                    || code == CharacterActionDiagnosticCode.CharacterCancelRejected
                    || code == CharacterActionDiagnosticCode.CombatCancelRejected
                    || code == CharacterActionDiagnosticCode.CharacterCombatCancelConflict)
                {
                    return CharacterActionRejectReason.CancelConflict;
                }

                if (code == CharacterActionDiagnosticCode.CombatActionMissing)
                    return CharacterActionRejectReason.CombatActionMissing;
                if (code == CharacterActionDiagnosticCode.AnimationActionMissing)
                    return CharacterActionRejectReason.AnimationActionMissing;
                if (code == CharacterActionDiagnosticCode.ResourceMissing
                    || code == CharacterActionDiagnosticCode.ResourceCostWithoutResourceId
                    || code == CharacterActionDiagnosticCode.PresentationResourceMissing
                    || code == CharacterActionDiagnosticCode.AudioCueMissing)
                {
                    return CharacterActionRejectReason.ResourceMissing;
                }

                if (code == CharacterActionDiagnosticCode.MissingActionConfig)
                    return CharacterActionRejectReason.MissingActionConfig;
            }

            return CharacterActionRejectReason.MissingActionConfig;
        }
    }
}

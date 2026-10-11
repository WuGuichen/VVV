using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Authoring-time validation for the v5 Timeline/Event authority. Track rows are deliberately absent:
    /// they are a projection of these event kinds and have no persisted or runtime identity.
    /// </summary>
    internal static class CharacterTestRuntimeAiLabBehaviorRecipeV5Validator
    {
        internal static IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> Validate(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references = null)
        {
            var diagnostics = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
            if (payload == null)
            {
                Error(diagnostics, "CARD_V5_NULL", "$", "Behavior recipe payload is null.");
                return diagnostics;
            }

            for (int i = 0; i < payload.MigrationDiagnostics.Count; i++)
                diagnostics.Add(payload.MigrationDiagnostics[i]);

            var timelineIds = new Dictionary<string, CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(
                StringComparer.Ordinal);
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            var conditionIds = new HashSet<string>(StringComparer.Ordinal);
            ValidateConditions(
                payload.ActivationConditions,
                "$.activationConditions",
                conditionIds,
                references,
                diagnostics);

            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                string path = "$.timelines[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                if (timeline == null || string.IsNullOrWhiteSpace(timeline.TimelineId))
                {
                    Error(diagnostics, "CARD_V5_TIMELINE_ID", path + ".timelineId", "Timeline id is required.");
                    continue;
                }
                if (!timelineIds.TryAdd(timeline.TimelineId, timeline))
                    Error(diagnostics, "CARD_V5_TIMELINE_DUPLICATE", path + ".timelineId", "Timeline id must be unique.");

                if (timeline.EntryEvent == null)
                {
                    Error(diagnostics, "CARD_V5_ENTRY_EVENT_MISSING", path + ".entryEvent", "Timeline requires exactly one entry event.");
                }
                else
                {
                    ValidateEventId(timeline.EntryEvent.EventId, path + ".entryEvent.eventId", eventIds, diagnostics);
                    ValidateConditions(
                        timeline.EntryEvent.Conditions,
                        path + ".entryEvent.conditions",
                        conditionIds,
                        references,
                        diagnostics);
                }

                if (timeline.PrimaryActionEvent == null)
                {
                    Error(diagnostics, "CARD_V5_PRIMARY_EVENT_MISSING", path + ".primaryActionEvent", "Timeline requires exactly one primary-action event.");
                }
                else
                {
                    ValidateEventId(
                        timeline.PrimaryActionEvent.EventId,
                        path + ".primaryActionEvent.eventId",
                        eventIds,
                        diagnostics);
                    ValidatePrimaryAction(timeline.PrimaryActionEvent, path + ".primaryActionEvent", references, diagnostics);
                }

                for (int eventIndex = 0; eventIndex < timeline.TriggerEvents.Count; eventIndex++)
                {
                    ValidateTrigger(
                        timeline.TriggerEvents[eventIndex],
                        timeline,
                        path + ".triggerEvents[" + eventIndex.ToString(CultureInfo.InvariantCulture) + "]",
                        eventIds,
                        conditionIds,
                        references,
                        diagnostics);
                }
                if (timeline.TriggerEvents.Count == 0
                    && !HasLegacySharedTriggerForTimeline(payload, timeline.TimelineId))
                {
                    Warning(
                        diagnostics,
                        "CARD_V5_TIMELINE_NO_EXIT",
                        path + ".triggerEvents",
                        "Timeline has no exit event; this is valid only for an intentional persistent behavior.");
                }
            }

            if (payload.Timelines.Count == 0)
                Error(diagnostics, "CARD_V5_TIMELINES_EMPTY", "$.timelines", "Behavior recipe requires at least one timeline.");
            if (!timelineIds.ContainsKey(payload.EntryTimelineId ?? string.Empty))
                Error(diagnostics, "CARD_V5_ENTRY_TIMELINE_UNKNOWN", "$.entryTimelineId", "Entry timeline does not exist.");

            var validatedSharedGroups = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < payload.LegacySharedTriggerEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent shared = payload.LegacySharedTriggerEvents[i];
                string path = "$.legacySharedTriggerEvents[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline owner = null;
                if (shared != null && !string.IsNullOrWhiteSpace(shared.TimelineId))
                    timelineIds.TryGetValue(shared.TimelineId, out owner);
                bool firstSharedProjection = shared == null
                    || string.IsNullOrWhiteSpace(shared.LegacySharedTriggerId)
                    || validatedSharedGroups.Add(shared.LegacySharedTriggerId);
                ValidateTrigger(
                    shared,
                    owner,
                    path,
                    eventIds,
                    firstSharedProjection
                        ? conditionIds
                        : new HashSet<string>(StringComparer.Ordinal),
                    references,
                    diagnostics);
                if (shared != null && shared.ClockOrigin != CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe)
                    Error(diagnostics, "CARD_V5_SHARED_CLOCK", path + ".clockOrigin", "Legacy shared trigger must use the recipe clock.");
                if (shared != null && string.IsNullOrWhiteSpace(shared.LegacySharedTriggerId))
                    Error(diagnostics, "CARD_V5_SHARED_ID", path + ".legacySharedTriggerId", "Legacy shared trigger requires its original shared detector identity.");
            }

            ValidateTargetsAndReachability(payload, timelineIds, diagnostics);
            return diagnostics;
        }

        private static bool HasLegacySharedTriggerForTimeline(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId)
        {
            for (int i = 0; i < payload.LegacySharedTriggerEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger =
                    payload.LegacySharedTriggerEvents[i];
                if (trigger != null
                    && (string.IsNullOrWhiteSpace(trigger.TimelineId)
                        || string.Equals(trigger.TimelineId, timelineId, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ValidatePrimaryAction(
            CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent action,
            string path,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardStepKind), action.Kind)
                || action.Kind == CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly)
            {
                Error(diagnostics, "CARD_V5_PRIMARY_KIND", path + ".kind", "Primary-action kind is invalid or has not been migrated.");
                return;
            }
            switch (action.Kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                    if (action.AbilityId <= 0)
                        Error(diagnostics, "CARD_V5_ABILITY_ID", path + ".abilityId", "Ability event requires a positive ability id.");
                    else if (references != null && !references.HasAbility(action.AbilityId))
                        Error(diagnostics, "CARD_V5_ABILITY_UNKNOWN", path + ".abilityId", "Ability reference is unavailable in the current GameSlice.");
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    if (!action.CharacterActionId.IsValid)
                        Error(diagnostics, "CARD_V5_ACTION_ID", path + ".characterActionId", "CharacterAction event requires a stable action id.");
                    else if (references != null && !references.HasCharacterAction(action.CharacterActionId))
                        Error(diagnostics, "CARD_V5_ACTION_UNKNOWN", path + ".characterActionId", "CharacterAction reference is unavailable in the current GameSlice.");
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabMovementStrategy), action.MovementStrategy))
                        Error(diagnostics, "CARD_V5_MOVE_STRATEGY", path + ".movementStrategy", "Move event requires a supported movement strategy.");
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabMovementTargetKind), action.MovementTargetKind))
                        Error(diagnostics, "CARD_V5_MOVE_TARGET", path + ".movementTargetKind", "Move event requires a supported target kind.");
                    if (action.MoveMinDistance < 0f || action.MoveMaxDistance < action.MoveMinDistance)
                        Error(diagnostics, "CARD_V5_MOVE_BAND", path, "Move distance band is invalid.");
                    if (action.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition
                        && (!IsFinite(action.MoveTargetPositionX) || !IsFinite(action.MoveTargetPositionZ)))
                    {
                        Error(diagnostics, "CARD_V5_MOVE_POSITION", path, "World-position target coordinates must be finite.");
                    }
                    if (action.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.Entity
                        && action.MoveTargetEntityId < 0)
                    {
                        Error(diagnostics, "CARD_V5_MOVE_ENTITY", path + ".moveTargetEntityId", "Entity target id must be zero or positive.");
                    }
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.Wait:
                    if (action.WaitFrames <= 0)
                        Error(diagnostics, "CARD_V5_WAIT_FRAMES", path + ".waitFrames", "Wait event requires a positive frame count.");
                    break;
            }
        }

        private static void ValidateTrigger(
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger,
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline owner,
            string path,
            HashSet<string> eventIds,
            HashSet<string> conditionIds,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            if (trigger == null)
            {
                Error(diagnostics, "CARD_V5_TRIGGER_NULL", path, "Trigger event is null.");
                return;
            }
            ValidateEventId(trigger.EventId, path + ".eventId", eventIds, diagnostics);
            if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind), trigger.SourceKind))
                Error(diagnostics, "CARD_V5_TRIGGER_SOURCE", path + ".sourceKind", "Trigger source is invalid.");
            if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin), trigger.ClockOrigin))
                Error(diagnostics, "CARD_V5_TRIGGER_CLOCK", path + ".clockOrigin", "Trigger clock origin is invalid.");
            if (trigger.ActivationStartFrame < 0)
                Error(diagnostics, "CARD_V5_TRIGGER_START", path + ".activationStartFrame", "Trigger start frame cannot be negative.");
            if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicDetectorWindowEndMode), trigger.WindowEndMode))
                Error(diagnostics, "CARD_V5_TRIGGER_WINDOW", path + ".windowEndMode", "Trigger window mode is invalid.");
            if (trigger.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                && trigger.ActivationDurationFrames <= 0)
            {
                Error(diagnostics, "CARD_V5_TRIGGER_DURATION", path + ".activationDurationFrames", "Fixed trigger window requires a positive duration.");
            }
            else if (trigger.ActivationDurationFrames < 0)
            {
                Error(diagnostics, "CARD_V5_TRIGGER_DURATION", path + ".activationDurationFrames", "Trigger duration cannot be negative.");
            }

            switch (trigger.SourceKind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.Predicate:
                    if (trigger.Conditions.Count == 0)
                        Error(diagnostics, "CARD_V5_PREDICATE_EMPTY", path + ".conditions", "Predicate trigger requires at least one concrete condition.");
                    ValidateConditions(trigger.Conditions, path + ".conditions", conditionIds, references, diagnostics);
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor), trigger.LocalFrameAnchor))
                    {
                        Error(diagnostics, "CARD_V5_LOCAL_FRAME_ANCHOR", path + ".localFrameAnchor", "Local-frame anchor is invalid.");
                    }
                    else if (trigger.LocalFrameAnchor == CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame)
                    {
                        if (trigger.LocalFrame < 1)
                            Error(diagnostics, "CARD_V5_LOCAL_FRAME_ZERO", path + ".localFrame", "Local-frame event must be at 1f or later; 0f is reserved for entry and primary action.");
                    }
                    else if (!TryResolvePrimaryActionEnd(owner?.PrimaryActionEvent, references, out _))
                    {
                        Error(diagnostics, "CARD_V5_PRIMARY_END_UNRESOLVED", path + ".localFrameAnchor", "PrimaryActionEnd requires an authoritative finite action duration.");
                    }
                    break;
                case CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal), trigger.ActionSignal))
                        Error(diagnostics, "CARD_V5_ACTION_SIGNAL", path + ".actionSignal", "Primary-action signal is invalid.");
                    bool ownsActionInstance = owner?.PrimaryActionEvent != null
                        && (owner.PrimaryActionEvent.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                            || owner.PrimaryActionEvent.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility);
                    if (!ownsActionInstance && !trigger.LegacyUnfilteredSignal)
                        Error(diagnostics, "CARD_V5_ACTION_SIGNAL_OWNER", path, "Primary-action signal requires a CharacterAction or Ability instance owned by this timeline.");
                    if ((trigger.ActionSignal == CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Failed
                            || trigger.ActionSignal == CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Rejected)
                        && trigger.ActionSignalReasonCode == 0
                        && !trigger.LegacyUnfilteredSignal)
                    {
                        Error(diagnostics, "CARD_V5_ACTION_SIGNAL_REASON", path + ".actionSignalReasonCode", "Failed and rejected signals require a concrete structured reason code.");
                    }
                    if (trigger.ActionSignal == CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Completed
                        && !trigger.LegacyUnfilteredSignal)
                    {
                        Error(diagnostics, "CARD_V5_ACTION_SIGNAL_COMPLETED", path + ".actionSignal", "New completion events must use LocalFrame.PrimaryActionEnd; unfiltered Completed is legacy compatibility only.");
                    }
                    if (trigger.LegacyUnfilteredSignal)
                    {
                        Warning(diagnostics, "CARD_V5_LEGACY_ACTION_SIGNAL", path, "Compatibility signal is not reason-filtered; replace it with a concrete predicate or reason code when editing.");
                    }
                    break;
            }

            if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind), trigger.OutcomeKind))
                Error(diagnostics, "CARD_V5_OUTCOME", path + ".outcomeKind", "Trigger must have exactly one supported outcome.");
            if (trigger.OutcomeKind == CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline
                && string.IsNullOrWhiteSpace(trigger.TargetTimelineId))
            {
                Error(diagnostics, "CARD_V5_OUTCOME_TARGET", path + ".targetTimelineId", "EnterTimeline requires a target timeline id.");
            }
        }

        internal static bool TryResolvePrimaryActionEnd(
            CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent action,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            out int frame)
        {
            frame = 0;
            if (action == null)
                return false;
            switch (action.Kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.Wait:
                    frame = action.WaitFrames;
                    return frame > 0;
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    if (references != null
                        && references.TryResolveCharacterActionDurationFrames(action.CharacterActionId, out frame))
                    {
                        return true;
                    }
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                    if (references != null
                        && references.TryResolveAbilityAction(action.AbilityId, out _, out frame))
                    {
                        return true;
                    }
                    break;
                default:
                    return false;
            }
            // v4 migration keeps the old authored duration only as a compatibility fallback. A compile
            // with a live reference catalog never relies on it.
            if (references == null && action.FallbackDurationFrames > 0)
            {
                frame = action.FallbackDurationFrames;
                return true;
            }
            return false;
        }

        private static void ValidateTargetsAndReachability(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorRecipeTimeline> timelines,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            if (timelines.ContainsKey(payload.EntryTimelineId ?? string.Empty))
                reachable.Add(payload.EntryTimelineId);
            bool changed;
            do
            {
                changed = false;
                for (int timelineIndex = 0; timelineIndex < payload.Timelines.Count; timelineIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[timelineIndex];
                    if (timeline == null || !reachable.Contains(timeline.TimelineId))
                        continue;
                    for (int eventIndex = 0; eventIndex < timeline.TriggerEvents.Count; eventIndex++)
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger = timeline.TriggerEvents[eventIndex];
                        string path = "$.timelines[" + timelineIndex + "].triggerEvents[" + eventIndex + "].targetTimelineId";
                        if (trigger == null
                            || trigger.OutcomeKind != CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline)
                        {
                            continue;
                        }
                        if (!timelines.ContainsKey(trigger.TargetTimelineId ?? string.Empty))
                            Error(diagnostics, "CARD_V5_OUTCOME_TARGET_UNKNOWN", path, "Trigger target timeline does not exist.");
                        else if (reachable.Add(trigger.TargetTimelineId))
                            changed = true;
                    }
                }
                for (int sharedIndex = 0; sharedIndex < payload.LegacySharedTriggerEvents.Count; sharedIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger =
                        payload.LegacySharedTriggerEvents[sharedIndex];
                    if (trigger == null
                        || trigger.OutcomeKind != CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline
                        || (!string.IsNullOrWhiteSpace(trigger.TimelineId)
                            && !reachable.Contains(trigger.TimelineId)))
                    {
                        continue;
                    }
                    if (timelines.ContainsKey(trigger.TargetTimelineId ?? string.Empty)
                        && reachable.Add(trigger.TargetTimelineId))
                    {
                        changed = true;
                    }
                }
            }
            while (changed);

            for (int i = 0; i < payload.LegacySharedTriggerEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger = payload.LegacySharedTriggerEvents[i];
                if (trigger != null
                    && trigger.OutcomeKind == CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline
                    && !timelines.ContainsKey(trigger.TargetTimelineId ?? string.Empty))
                {
                    Error(diagnostics, "CARD_V5_OUTCOME_TARGET_UNKNOWN", "$.legacySharedTriggerEvents[" + i + "].targetTimelineId", "Shared trigger target timeline does not exist.");
                }
            }

            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                if (timeline != null
                    && !string.IsNullOrWhiteSpace(timeline.TimelineId)
                    && !reachable.Contains(timeline.TimelineId))
                {
                    Error(diagnostics, "CARD_V5_TIMELINE_UNREACHABLE", "$.timelines[" + i + "].timelineId", "Timeline is unreachable from the entry timeline.");
                }
            }
        }

        private static void ValidateEventId(
            string eventId,
            string path,
            HashSet<string> eventIds,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                Error(diagnostics, "CARD_V5_EVENT_ID", path, "Event id is required.");
            else if (!eventIds.Add(eventId))
                Error(diagnostics, "CARD_V5_EVENT_DUPLICATE", path, "Event id must be unique across the behavior recipe.");
        }

        private static void ValidateConditions(
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions,
            string path,
            HashSet<string> conditionIds,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                string itemPath = path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                if (condition == null)
                {
                    Error(diagnostics, "CARD_V5_CONDITION_NULL", itemPath, "Condition is null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(condition.ConditionId))
                    Error(diagnostics, "CARD_V5_CONDITION_ID", itemPath + ".conditionId", "Condition id is required.");
                else if (!conditionIds.Add(condition.ConditionId))
                    Error(diagnostics, "CARD_V5_CONDITION_DUPLICATE", itemPath + ".conditionId", "Condition id must be unique across the behavior recipe.");
                if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardConditionFact), condition.Fact))
                    Error(diagnostics, "CARD_V5_CONDITION_FACT", itemPath + ".fact", "Condition fact is invalid.");
                if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardConditionOperator), condition.Operator))
                    Error(diagnostics, "CARD_V5_CONDITION_OPERATOR", itemPath + ".operator", "Condition operator is invalid.");
                bool numeric = condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                    || condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance
                    || condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames;
                if (numeric && (!IsFinite(condition.Value) || !IsFinite(condition.MaxValue)))
                    Error(diagnostics, "CARD_V5_CONDITION_NON_FINITE", itemPath, "Numeric condition values must be finite.");
                if (!numeric
                    && condition.Operator != CharacterTestRuntimeAiLabCardConditionOperator.Equals
                    && condition.Operator != CharacterTestRuntimeAiLabCardConditionOperator.NotEquals)
                {
                    Error(diagnostics, "CARD_V5_CONDITION_BOOLEAN_OPERATOR", itemPath + ".operator", "Boolean fact only supports Equals or NotEquals.");
                }
                if (condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between
                    && condition.MaxValue < condition.Value)
                {
                    Error(diagnostics, "CARD_V5_CONDITION_RANGE", itemPath, "Between maximum must be greater than or equal to minimum.");
                }
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable)
                {
                    if (condition.AbilityId <= 0)
                        Error(diagnostics, "CARD_V5_CONDITION_ABILITY", itemPath + ".abilityId", "AbilityAvailable requires an ability id.");
                    else if (references != null && !references.HasAbility(condition.AbilityId))
                        Error(diagnostics, "CARD_V5_CONDITION_ABILITY_UNKNOWN", itemPath + ".abilityId", "Condition ability reference is unavailable.");
                }
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction)
                {
                    if (!condition.CharacterActionId.IsValid)
                        Error(diagnostics, "CARD_V5_CONDITION_ACTION", itemPath + ".characterActionId", "SelfCanTransitionToAction requires an action id.");
                    else if (references != null && !references.HasCharacterAction(condition.CharacterActionId))
                        Error(diagnostics, "CARD_V5_CONDITION_ACTION_UNKNOWN", itemPath + ".characterActionId", "Condition action reference is unavailable.");
                }
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void Error(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                code,
                path,
                message));
        }

        private static void Warning(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                CharacterTestRuntimeAiLabCardDiagnosticSeverity.Warning,
                code,
                path,
                message));
        }
    }

    internal static class CharacterTestRuntimeAiLabBehaviorRecipeV5SemanticHash
    {
        internal static long Compute(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null)
                return 0L;
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForWrite(payload);
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("recipe.schema", CharacterTestRuntimeAiLabBehaviorCardLogicPayload.Schema);
            hash.AddStringStable("recipe.entryTimeline", payload.EntryTimelineId);
            AddConditions(ref hash, "recipe.activation", payload.ActivationConditions);

            var timelines = new List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(payload.Timelines);
            timelines.Sort(CompareTimelines);
            hash.AddInt("recipe.timelineCount", timelines.Count);
            for (int i = 0; i < timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = timelines[i];
                hash.AddStringStable("recipe.timeline.id", timeline?.TimelineId);
                hash.AddInt("recipe.timeline.order", timeline?.Order ?? 0);
                hash.AddStringStable("recipe.timeline.entry.id", timeline?.EntryEvent?.EventId);
                AddConditions(ref hash, "recipe.timeline.entry", timeline?.EntryEvent?.Conditions);
                AddPrimaryAction(ref hash, timeline?.PrimaryActionEvent);
                var events = new List<CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent>(
                    timeline?.TriggerEvents ?? new List<CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent>());
                events.Sort(CompareEvents);
                hash.AddInt("recipe.timeline.triggerCount", events.Count);
                for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                    AddTrigger(ref hash, events[eventIndex]);
            }

            var shared = new List<CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent>(
                payload.LegacySharedTriggerEvents);
            shared.Sort(CompareEvents);
            hash.AddInt("recipe.sharedTriggerCount", shared.Count);
            for (int i = 0; i < shared.Count; i++)
                AddTrigger(ref hash, shared[i]);
            return hash.ToHash();
        }

        private static void AddPrimaryAction(
            ref RuntimeContentHashAccumulator hash,
            CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent action)
        {
            hash.AddStringStable("recipe.primary.id", action?.EventId);
            hash.AddInt("recipe.primary.kind", action != null ? (int)action.Kind : -1);
            hash.AddInt("recipe.primary.ability", action?.AbilityId ?? 0);
            hash.AddInt("recipe.primary.action", action?.CharacterActionId.Value ?? 0);
            hash.AddInt("recipe.primary.moveStrategy", action != null ? (int)action.MovementStrategy : 0);
            hash.AddInt("recipe.primary.moveTargetKind", action != null ? (int)action.MovementTargetKind : 0);
            hash.AddInt("recipe.primary.moveX", Quantize(action?.MoveTargetPositionX ?? 0f));
            hash.AddInt("recipe.primary.moveZ", Quantize(action?.MoveTargetPositionZ ?? 0f));
            hash.AddInt("recipe.primary.moveEntity", action?.MoveTargetEntityId ?? 0);
            hash.AddInt("recipe.primary.moveMin", Quantize(action?.MoveMinDistance ?? 0f));
            hash.AddInt("recipe.primary.moveMax", Quantize(action?.MoveMaxDistance ?? 0f));
            hash.AddInt("recipe.primary.wait", action?.WaitFrames ?? 0);
            hash.AddInt("recipe.primary.fallbackDuration", action?.FallbackDurationFrames ?? 0);
        }

        private static void AddTrigger(
            ref RuntimeContentHashAccumulator hash,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger)
        {
            hash.AddStringStable("recipe.trigger.id", trigger?.EventId);
            hash.AddStringStable("recipe.trigger.sharedId", trigger?.LegacySharedTriggerId);
            hash.AddStringStable("recipe.trigger.timeline", trigger?.TimelineId);
            hash.AddInt("recipe.trigger.priority", trigger?.Priority ?? 0);
            hash.AddInt("recipe.trigger.source", trigger != null ? (int)trigger.SourceKind : -1);
            hash.AddInt("recipe.trigger.clock", trigger != null ? (int)trigger.ClockOrigin : -1);
            hash.AddInt("recipe.trigger.start", trigger?.ActivationStartFrame ?? 0);
            hash.AddInt("recipe.trigger.duration", trigger?.ActivationDurationFrames ?? 0);
            hash.AddInt("recipe.trigger.window", trigger != null ? (int)trigger.WindowEndMode : -1);
            hash.AddInt("recipe.trigger.expected", trigger != null && trigger.ExpectedResult ? 1 : 0);
            hash.AddInt("recipe.trigger.frameAnchor", trigger != null ? (int)trigger.LocalFrameAnchor : -1);
            hash.AddInt("recipe.trigger.frame", trigger?.LocalFrame ?? 0);
            hash.AddInt("recipe.trigger.signal", trigger != null ? (int)trigger.ActionSignal : -1);
            hash.AddInt("recipe.trigger.reason", trigger?.ActionSignalReasonCode ?? 0);
            hash.AddInt("recipe.trigger.legacySignal", trigger != null && trigger.LegacyUnfilteredSignal ? 1 : 0);
            hash.AddInt("recipe.trigger.outcome", trigger != null ? (int)trigger.OutcomeKind : -1);
            hash.AddStringStable("recipe.trigger.target", trigger?.TargetTimelineId);
            AddConditions(ref hash, "recipe.trigger.conditions", trigger?.Conditions);
        }

        private static void AddConditions(
            ref RuntimeContentHashAccumulator hash,
            string prefix,
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions)
        {
            var ordered = new List<CharacterTestRuntimeAiLabCardCondition>();
            for (int i = 0; conditions != null && i < conditions.Count; i++)
                ordered.Add(conditions[i]);
            ordered.Sort(CompareConditions);
            hash.AddInt(prefix + ".count", ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = ordered[i];
                hash.AddStringStable(prefix + ".id", condition?.ConditionId);
                hash.AddInt(prefix + ".fact", condition != null ? (int)condition.Fact : -1);
                hash.AddInt(prefix + ".operator", condition != null ? (int)condition.Operator : -1);
                hash.AddInt(prefix + ".bool", condition != null && condition.BoolValue ? 1 : 0);
                hash.AddInt(prefix + ".value", Quantize(condition?.Value ?? 0f));
                hash.AddInt(prefix + ".max", Quantize(condition?.MaxValue ?? 0f));
                hash.AddInt(prefix + ".ability", condition?.AbilityId ?? 0);
                hash.AddInt(prefix + ".action", condition?.CharacterActionId.Value ?? 0);
            }
        }

        private static int CompareTimelines(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline left,
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.CompareOrdinal(left.TimelineId, right.TimelineId);
        }

        private static int CompareEvents(
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent left,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : string.CompareOrdinal(left.EventId, right.EventId);
        }

        private static int CompareConditions(
            CharacterTestRuntimeAiLabCardCondition left,
            CharacterTestRuntimeAiLabCardCondition right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            return string.CompareOrdinal(left.ConditionId, right.ConditionId);
        }

        private static int Quantize(float value) => (int)Math.Round(value * 1000f);
    }
}

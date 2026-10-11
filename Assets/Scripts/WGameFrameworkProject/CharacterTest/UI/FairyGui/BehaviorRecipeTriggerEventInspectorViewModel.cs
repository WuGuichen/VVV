using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal enum BehaviorRecipeInspectorMode
    {
        Recipe,
        Timeline,
        Event
    }

    internal enum BehaviorRecipeTriggerConditionValueKind
    {
        Boolean,
        Number,
        Range,
        Ability,
        CharacterAction,
        TargetState,
        EventSignal
    }

    internal enum BehaviorRecipeConditionObservationState
    {
        NotObserved = 0,
        Satisfied = 1,
        Unsatisfied = 2,
        SubjectUnavailable = 3
    }

    internal sealed class BehaviorRecipeTriggerConditionViewModel
    {
        internal string ConditionId = string.Empty;
        internal string NodeId = string.Empty;
        internal int Depth;
        internal int ChildCount;
        internal int LeafIndex = -1;
        internal CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind NodeKind;
        internal CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind Subject;
        internal bool SubjectScope;
        internal string SourceEventId = string.Empty;
        internal CharacterTestRuntimeAiLabBehaviorRecipeEventSignal EventSignal;
        internal CharacterTestRuntimeAiLabCardConditionFact Fact;
        internal string FactText = string.Empty;
        internal int EffectiveOperator;
        internal string OperatorText = string.Empty;
        internal BehaviorRecipeTriggerConditionValueKind ValueKind;
        internal bool BoolValue;
        internal float Value;
        internal float MaxValue;
        internal int AbilityId;
        internal MxFramework.CharacterAction.CharacterActionId CharacterActionId;
        internal uint AllowedTargetPhases;
        internal uint RequiredTargetFlags;
        internal uint ForbiddenTargetFlags;
        internal string ValueText = string.Empty;
        internal string EffectiveSummary = string.Empty;
        internal string ValidationText = string.Empty;
        internal BehaviorRecipeConditionObservationState ObservationState;
        internal string ObservedValueText = string.Empty;
        internal string ObservationReasonText = string.Empty;
        internal string SubjectIdentityText = string.Empty;
        internal int ObservationRuntimeFrame = -1;
    }

    internal sealed class BehaviorRecipeTriggerTimelineOptionViewModel
    {
        internal string TimelineId = string.Empty;
        internal string DisplayText = string.Empty;
        internal int Order;
    }

    internal sealed class BehaviorRecipeTriggerEventSignalOptionViewModel
    {
        internal string EventId = string.Empty;
        internal string DisplayText = string.Empty;
    }

    internal sealed class BehaviorRecipeCharacterActionOptionViewModel
    {
        internal MxFramework.CharacterAction.CharacterActionId ActionId;
        internal string DisplayName = string.Empty;
        internal string CategoryText = string.Empty;
        internal int DurationFrames;
        internal string TagsText = string.Empty;
        internal string SearchText = string.Empty;
        internal string DisplayText = string.Empty;
        internal bool Valid;
        internal CharacterActionTimelinePreviewViewModel TimelinePreview;
    }

    internal sealed class BehaviorRecipeTriggerEventInspectorViewModel
    {
        internal bool Available;
        internal bool UnifiedV6;
        internal string TacticalDescription;
        internal CharacterTestTacticalEditingSession TacticalEditor;
        internal CharacterTestTacticalEditToken TacticalToken;
        internal string TacticalConditionTimeline;
        internal string TacticalConditionEvent;
        internal bool ConditionOwnerOnly;
        internal BehaviorRecipeInspectorMode InspectorMode = BehaviorRecipeInspectorMode.Event;
        internal bool AllowsEventSignal = true;
        internal bool IsEntryTimeline;
        internal string TimelineId = string.Empty;
        internal string EventId = string.Empty;
        internal string TimelineTitle = string.Empty;
        internal string DisplayTitle = string.Empty;
        internal string EffectiveTriggerSummary = string.Empty;
        internal CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind SourceKind;
        internal CharacterTestRuntimeAiLabBehaviorRecipePredicateGroupMode PredicateGroupMode;
        internal BehaviorRecipeTriggerConditionViewModel[] Conditions =
            Array.Empty<BehaviorRecipeTriggerConditionViewModel>();
        internal CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor LocalFrameAnchor;
        internal int LocalFrame;
        internal int ResolvedPrimaryActionEndFrame;
        internal bool CanUsePrimaryActionEnd;
        internal CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal ActionSignal;
        internal string ActionSignalReasonCode = string.Empty;
        internal bool CanUseActionSignal;
        internal bool CompatibilitySignal;
        internal int ActivationStartFrame;
        internal CharacterTestRuntimeAiLabLogicDetectorWindowEndMode WindowEndMode;
        internal int ActivationDurationFrames;
        internal CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind OutcomeKind;
        internal string TargetTimelineId = string.Empty;
        internal BehaviorRecipeTriggerTimelineOptionViewModel[] TimelineOptions =
            Array.Empty<BehaviorRecipeTriggerTimelineOptionViewModel>();
        internal BehaviorRecipeTriggerEventSignalOptionViewModel[] SignalEventOptions =
            Array.Empty<BehaviorRecipeTriggerEventSignalOptionViewModel>();
        internal int Priority;
        internal int StartFrame;
        internal CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind TimingKind;
        internal CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode DurationMode;
        internal int DurationFrames;
        internal int MaxExecutionsPerPass;
        internal int MaxLoopRepeats;
        internal bool DurationReviewRequired;
        internal int CooldownFrames;
        internal CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind OperationKind;
        internal int OperationAbilityId;
        internal MxFramework.CharacterAction.CharacterActionId OperationCharacterActionId;
        internal string SelectedCharacterActionDisplayName = string.Empty;
        internal int SelectedCharacterActionDurationFrames;
        internal bool CharacterActionReferenceValid;
        internal bool CharacterActionEvent;
        internal bool CharacterActionConditionRepairRequired;
        internal CharacterActionTimelinePreviewViewModel CharacterActionTimelinePreview;
        internal BehaviorRecipeCharacterActionOptionViewModel[] CharacterActionOptions =
            Array.Empty<BehaviorRecipeCharacterActionOptionViewModel>();
        internal float OperationMoveMinDistance;
        internal float OperationMoveMaxDistance;
        internal float OperationMoveSpeedMultiplier;
        internal bool LegacyShared;
        internal BehaviorRecipeEventGroupKind EventGroupKind;
        internal string EventGroupText = string.Empty;
        internal string RuntimeStateText = string.Empty;
        internal string ValidationText = string.Empty;
        internal bool CanEdit;
        internal bool CanEditDisplayTitle;
        internal bool CanEditResponsibilities;
        internal int EntryIndex = -1;
        internal int ResponsibilityCompatibilityMask;
        internal CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] ResponsibilityKinds =
            { CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other };
    }

    internal static partial class BehaviorRecipeTriggerEventInspectorProjection
    {
        internal static void Rebuild(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel destination,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState,
            CharacterTestRuntimeAiPlannerAgentSnapshot? observedAgent = null,
            int observedRuntimeFrame = -1)
        {
            destination.RecipeInspector = null;
            if (destination.InspectCompiledExecution && destination.NativeExecution != null)
            {
                RebuildV6(destination, destination.NativeExecution.Payload, runtimeState, observedAgent, observedRuntimeFrame);
                destination.NativeExecution.ApplyInspector(destination.RecipeInspector, observedAgent, observedRuntimeFrame, runtimeState);
                return;
            }
            if (destination.TacticalPreview != null)
            {
                RebuildTactical(destination);
                return;
            }
            if (payload == null)
                return;
            if (CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
            {
                RebuildV6(destination, payload, runtimeState, observedAgent, observedRuntimeFrame);
                return;
            }
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(payload);
            if (!TryResolveSelectedIdentity(destination, out string timelineId, out string eventId))
                return;
            if (!TryFind(payload, timelineId, eventId, out var timeline, out var trigger, out bool shared))
                return;

            var model = new BehaviorRecipeTriggerEventInspectorViewModel
            {
                Available = true,
                TimelineId = timeline?.TimelineId ?? timelineId,
                EventId = trigger.EventId ?? string.Empty,
                TimelineTitle = BuildTimelineTitle(timeline),
                DisplayTitle = string.IsNullOrWhiteSpace(trigger.Title) ? "分支事件" : trigger.Title.Trim(),
                SourceKind = trigger.SourceKind,
                PredicateGroupMode = trigger.ExpectedResult
                    ? CharacterTestRuntimeAiLabBehaviorRecipePredicateGroupMode.AllConditionsMatch
                    : CharacterTestRuntimeAiLabBehaviorRecipePredicateGroupMode.AnyConditionDoesNotMatch,
                LocalFrameAnchor = trigger.LocalFrameAnchor,
                LocalFrame = Math.Max(1, trigger.LocalFrame),
                ResolvedPrimaryActionEndFrame = ResolvePrimaryActionEnd(timeline),
                CanUsePrimaryActionEnd = HasAuthoritativePrimaryActionEnd(timeline),
                ActionSignal = trigger.ActionSignal,
                ActionSignalReasonCode = trigger.ActionSignalReasonCode.ToString(CultureInfo.InvariantCulture),
                CanUseActionSignal = OwnsActionInstance(timeline),
                CompatibilitySignal = trigger.LegacyUnfilteredSignal
                    || trigger.ActionSignal == CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Completed,
                ActivationStartFrame = Math.Max(0, trigger.ActivationStartFrame),
                WindowEndMode = trigger.WindowEndMode,
                ActivationDurationFrames = Math.Max(0, trigger.ActivationDurationFrames),
                OutcomeKind = trigger.OutcomeKind,
                TargetTimelineId = trigger.TargetTimelineId ?? string.Empty,
                Priority = trigger.Priority,
                LegacyShared = shared || trigger.ClockOrigin == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe,
                CanEdit = destination.CanEdit && !destination.Historical,
                CanEditDisplayTitle = destination.CanEdit && !destination.Historical
            };
            model.Conditions = BuildConditions(trigger);
            model.TimelineOptions = BuildTimelineOptions(payload);
            model.EffectiveTriggerSummary = BuildEffectiveSummary(trigger, timeline, model.Conditions, payload);
            model.RuntimeStateText = ResolveRuntimeState(destination, trigger, runtimeState);
            model.ValidationText = BuildValidationText(model);
            destination.RecipeInspector = model;
        }

        private static void RebuildV6(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel destination,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState,
            CharacterTestRuntimeAiPlannerAgentSnapshot? observedAgent,
            int observedRuntimeFrame)
        {
            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForRead(payload);
            BehaviorPlanPresentationState selection = destination.Presentation;
            if (selection.RecipeInspectorTargetKind == BehaviorRecipeInspectorTargetKind.Event
                && TryFindV6(
                    payload,
                    selection.SelectedRecipeTimelineId,
                    selection.SelectedRecipeEventId,
                    out var eventTimeline,
                    out var selectedEvent))
            {
                RebuildV6Event(destination, payload, runtimeState, eventTimeline, selectedEvent);
                ApplyV10ConditionObservations(
                    destination.RecipeInspector,
                    observedAgent,
                    runtimeState,
                    observedRuntimeFrame,
                    destination.CompiledRecipeV6);
                return;
            }

            if (selection.RecipeInspectorTargetKind != BehaviorRecipeInspectorTargetKind.Recipe
                && TryFindV6Timeline(payload, selection.SelectedRecipeTimelineId, out var selectedTimeline))
            {
                selection.SelectTimeline(selectedTimeline.TimelineId);
                RebuildV6Timeline(destination, payload, runtimeState, selectedTimeline);
                ApplyV10ConditionObservations(
                    destination.RecipeInspector,
                    observedAgent,
                    runtimeState,
                    observedRuntimeFrame,
                    destination.CompiledRecipeV6);
                return;
            }

            bool missingSelection =
                selection.RecipeInspectorTargetKind != BehaviorRecipeInspectorTargetKind.Recipe;
            selection.SelectRecipe();
            RebuildV6Recipe(
                destination,
                payload,
                missingSelection ? "所选对象不存在，已返回行为配方。" : string.Empty);
            ApplyV10ConditionObservations(
                destination.RecipeInspector,
                observedAgent,
                runtimeState,
                observedRuntimeFrame,
                destination.CompiledRecipeV6);
        }

        private static void RebuildV6Event(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel destination,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState,
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt)
        {

            BehaviorRecipeCharacterActionOptionViewModel[] actionOptions =
                destination.CharacterActionOptions ?? Array.Empty<BehaviorRecipeCharacterActionOptionViewModel>();
            var rows = new List<BehaviorRecipeTriggerConditionViewModel>();
            int leafIndex = 0;
            BuildV6Conditions(evt.ConditionRoot, 0, rows, payload, timeline, ref leafIndex);
            ApplyCharacterActionPresentation(rows, actionOptions);
            MxFramework.CharacterAction.CharacterActionId selectedActionId =
                evt.Operation?.CharacterActionId ?? default;
            BehaviorRecipeCharacterActionOptionViewModel selectedAction =
                FindCharacterActionOption(actionOptions, selectedActionId);
            BehaviorRecipeEventGroupKind groupKind = BehaviorRecipeTimelineProjection.ResolveEventGroupKind(evt);
            string groupText = BehaviorRecipeTimelineProjection.FormatEventGroupDisplayName(groupKind);
            var model = new BehaviorRecipeTriggerEventInspectorViewModel
            {
                Available = true,
                UnifiedV6 = true,
                InspectorMode = BehaviorRecipeInspectorMode.Event,
                TimelineId = timeline.TimelineId ?? string.Empty,
                EventId = evt.EventId ?? string.Empty,
                TimelineTitle = BuildTimelineTitle(timeline),
                DisplayTitle = string.IsNullOrWhiteSpace(evt.Title) ? "事件" : evt.Title.Trim(),
                EffectiveTriggerSummary = BuildV6EventSummary(evt, payload, actionOptions),
                Conditions = rows.ToArray(),
                EventGroupKind = groupKind,
                EventGroupText = groupText,
                ActivationStartFrame = Math.Max(0, evt.StartFrame),
                LocalFrame = Math.Max(0, evt.StartFrame),
                StartFrame = Math.Max(0, evt.StartFrame),
                TimingKind = evt.TimingKind,
                DurationMode = evt.DurationMode,
                DurationFrames = Math.Max(1, evt.DurationFrames),
                ActivationDurationFrames = Math.Max(1, evt.DurationFrames),
                MaxExecutionsPerPass = Math.Max(0, evt.MaxExecutionsPerPass),
                MaxLoopRepeats = Math.Max(0, evt.Operation?.MaxLoopRepeats ?? 0),
                DurationReviewRequired = evt.DurationReviewRequired,
                CooldownFrames = Math.Max(0, evt.CooldownFrames),
                OperationKind = evt.Operation?.Kind
                    ?? CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured,
                OperationAbilityId = evt.Operation?.AbilityId ?? 0,
                OperationCharacterActionId = selectedActionId,
                SelectedCharacterActionDisplayName = selectedAction?.DisplayName ?? string.Empty,
                SelectedCharacterActionDurationFrames = selectedAction?.DurationFrames ?? 0,
                CharacterActionReferenceValid = selectedAction?.Valid == true,
                CharacterActionEvent = evt.Operation?.Kind
                    == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                CharacterActionConditionRepairRequired = evt.Operation?.Kind
                    == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction
                    && evt.ConditionRoot != null,
                CharacterActionTimelinePreview = selectedAction?.TimelinePreview,
                CharacterActionOptions = actionOptions,
                OperationMoveMinDistance = evt.Operation?.MoveMinDistance ?? 0f,
                OperationMoveMaxDistance = evt.Operation?.MoveMaxDistance ?? 0f,
                OperationMoveSpeedMultiplier = evt.Operation?.MoveSpeedMultiplier
                    ?? CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.DefaultMoveSpeedMultiplier,
                OutcomeKind = evt.Operation?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
                    ? CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline
                    : CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner,
                TargetTimelineId = evt.Operation?.TargetTimelineId ?? string.Empty,
                TimelineOptions = BuildTimelineOptions(payload),
                SignalEventOptions = BuildV6SignalEventOptions(timeline, evt.EventId),
                Priority = evt.Priority,
                LegacyShared = evt.ClockOrigin == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe,
                RuntimeStateText = ResolveV6RuntimeState(destination, evt, runtimeState),
                ValidationText = evt.DurationReviewRequired
                    ? "此事件原为开放区间，请确认固定长度。"
                    : evt.Operation == null
                    || evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured
                        ? "尚未配置执行操作；请配置后再保存或运行。"
                        : evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction
                            && !selectedActionId.IsValid
                            ? "请选择角色动作。"
                        : evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction
                            && selectedAction?.Valid != true
                            ? selectedAction == null
                                ? "动作引用不存在：" + selectedActionId
                                : "动作缺少权威时长：" + selectedActionId
                        : evt.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction
                            && evt.ConditionRoot != null
                            ? "角色动作事件不能带触发条件。"
                        : !CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.IsOperationCompatible(
                            evt.TimingKind,
                            evt.Operation.Kind)
                            ? "当前操作与时间类别不兼容。"
                        : string.Empty,
                CanEdit = destination.CanEdit && !destination.Historical
            };
            model.CanEditDisplayTitle = model.CanEdit;
            destination.RecipeInspector = model;
        }

        private static void RebuildV6Recipe(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel destination,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string selectionDiagnostic)
        {
            var rows = new List<BehaviorRecipeTriggerConditionViewModel>();
            int leafIndex = 0;
            BuildV6Conditions(payload.ActivationConditionRoot, 0, rows, payload, null, ref leafIndex);
            destination.RecipeInspector = new BehaviorRecipeTriggerEventInspectorViewModel
            {
                Available = true,
                UnifiedV6 = true,
                ConditionOwnerOnly = true,
                InspectorMode = BehaviorRecipeInspectorMode.Recipe,
                AllowsEventSignal = false,
                EventId = CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.ActivationConditionOwnerId,
                TimelineTitle = destination.CardTitleText,
                DisplayTitle = destination.CardTitleText,
                EffectiveTriggerSummary = payload.ActivationConditionRoot == null
                    ? "无条件"
                    : BehaviorRecipeTimelineProjection.BuildV6ConditionSummary(payload.ActivationConditionRoot),
                Conditions = rows.ToArray(),
                ValidationText = selectionDiagnostic ?? string.Empty,
                RuntimeStateText = destination.Historical ? "历史帧只读" : "编辑配方",
                CharacterActionOptions = destination.CharacterActionOptions,
                CanEdit = destination.CanEdit && !destination.Historical,
                CanEditDisplayTitle = destination.CanEditCardDisplayName,
                CanEditResponsibilities = destination.CanEditResponsibilities,
                EntryIndex = destination.EntryIndex,
                ResponsibilityCompatibilityMask = destination.ResponsibilityCompatibilityMask,
                ResponsibilityKinds = CopyResponsibilities(destination.ResponsibilityKinds)
            };
        }

        private static CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] CopyResponsibilities(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] source) =>
            source == null || source.Length == 0
                ? new[] { CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other }
                : (CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[])source.Clone();

        private static void RebuildV6Timeline(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel destination,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState,
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline)
        {
            var rows = new List<BehaviorRecipeTriggerConditionViewModel>();
            int leafIndex = 0;
            BuildV6Conditions(timeline.EntryConditionRoot, 0, rows, payload, timeline, ref leafIndex);
            destination.RecipeInspector = new BehaviorRecipeTriggerEventInspectorViewModel
            {
                Available = true,
                UnifiedV6 = true,
                ConditionOwnerOnly = true,
                InspectorMode = BehaviorRecipeInspectorMode.Timeline,
                AllowsEventSignal = false,
                TimelineId = timeline.TimelineId ?? string.Empty,
                EventId = CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TimelineEntryConditionOwnerId,
                TimelineTitle = BuildTimelineTitle(timeline),
                DisplayTitle = string.IsNullOrWhiteSpace(timeline.Title)
                    ? timeline.TimelineId
                    : timeline.Title.Trim(),
                EffectiveTriggerSummary = timeline.EntryConditionRoot == null
                    ? "无条件，进入时允许执行"
                    : BehaviorRecipeTimelineProjection.BuildV6ConditionSummary(timeline.EntryConditionRoot),
                Conditions = rows.ToArray(),
                IsEntryTimeline = string.Equals(
                    payload.EntryTimelineId,
                    timeline.TimelineId,
                    StringComparison.Ordinal),
                RuntimeStateText = ResolveV6TimelineRuntimeState(destination, timeline, runtimeState),
                CharacterActionOptions = destination.CharacterActionOptions,
                CanEdit = destination.CanEdit && !destination.Historical
            };
            destination.RecipeInspector.CanEditDisplayTitle = destination.RecipeInspector.CanEdit;
        }

        private static bool TryFindV6Timeline(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId,
            out CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline)
        {
            timeline = null;
            for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
            {
                if (!string.Equals(payload.Timelines[i]?.TimelineId, timelineId, StringComparison.Ordinal))
                    continue;
                timeline = payload.Timelines[i];
                return true;
            }
            return false;
        }

        private static string ResolveV6TimelineRuntimeState(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel destination,
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState)
        {
            if (destination.Historical)
                return "历史帧只读";
            CharacterTestRuntimeAiLabCompiledRecipeV6 recipe = destination.CompiledRecipeV6;
            int currentIndex = runtimeState?.CurrentTimelineIndex ?? -1;
            if (recipe == null || currentIndex < 0 || currentIndex >= recipe.Timelines.Count)
                return "未运行";
            return string.Equals(
                recipe.Timelines[currentIndex].TimelineId,
                timeline.TimelineId,
                StringComparison.Ordinal)
                    ? "正在执行"
                    : "未运行";
        }

        private static bool TryResolveSelectedV6Identity(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source,
            out string timelineId,
            out string eventId)
        {
            timelineId = string.Empty;
            eventId = string.Empty;
            string key = source?.Presentation.SelectedRecipeEventStableKey ?? string.Empty;
            const string prefix = "event:";
            const string token = ":v6:";
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            int split = key.IndexOf(token, prefix.Length, StringComparison.Ordinal);
            if (split <= prefix.Length)
                return false;
            timelineId = key.Substring(prefix.Length, split - prefix.Length);
            eventId = key.Substring(split + token.Length);
            return eventId.Length > 0;
        }

        private static bool TryFindV6(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId,
            string eventId,
            out CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            out CharacterTestRuntimeAiLabBehaviorRecipeEvent evt)
        {
            timeline = null;
            evt = null;
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline selectedTimeline = null;
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline candidate = payload.Timelines[i];
                if (candidate == null || !string.Equals(candidate.TimelineId, timelineId, StringComparison.Ordinal))
                    continue;
                selectedTimeline = candidate;
                for (int j = 0; j < candidate.Events.Count; j++)
                {
                    if (!string.Equals(candidate.Events[j]?.EventId, eventId, StringComparison.Ordinal))
                        continue;
                    timeline = candidate;
                    evt = candidate.Events[j];
                    return true;
                }
            }
            for (int i = 0; selectedTimeline != null && i < payload.LegacySharedEvents.Count; i++)
            {
                if (!string.Equals(payload.LegacySharedEvents[i]?.EventId, eventId, StringComparison.Ordinal))
                    continue;
                timeline = selectedTimeline;
                evt = payload.LegacySharedEvents[i];
                return true;
            }
            return false;
        }

    }
}

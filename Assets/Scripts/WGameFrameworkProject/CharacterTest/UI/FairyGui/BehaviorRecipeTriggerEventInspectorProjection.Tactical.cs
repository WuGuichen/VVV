using System;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class BehaviorRecipeTriggerEventInspectorProjection
    {
        internal static BehaviorRecipeTriggerEventInspectorViewModel BuildActivationOverview(
            CharacterTestTacticalAuthoringPreview preview, BehaviorRecipeCharacterActionOptionViewModel[] actions)
        {
            var destination = new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel {
                TacticalPreview = preview, CharacterActionOptions = actions ?? Array.Empty<BehaviorRecipeCharacterActionOptionViewModel>() };
            RebuildTactical(destination);
            return destination.RecipeInspector;
        }
        internal static BehaviorRecipeTriggerEventInspectorViewModel BuildEmbeddedEventConditions(
            CharacterTestTacticalEditingSession editor, BehaviorRecipeCharacterActionOptionViewModel[] actions)
        {
            var destination = new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel {
                TacticalPreview = editor.Preview, CharacterActionOptions = actions };
            RebuildTactical(destination, editor);
            return destination.RecipeInspector;
        }

        private static void RebuildTactical(CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel destination,
            CharacterTestTacticalEditingSession embeddedEditor = null)
        {
            var preview = destination.TacticalPreview;
            var editor = embeddedEditor ?? preview.Editor;
            var selection = destination.Presentation;
            if (editor?.ConditionsOpen == true && (embeddedEditor != null || preview.IsConditionProjection
                || editor.Token.Target == CharacterTestTacticalEditTarget.CardActivation))
            {
                var proxy = CharacterTestTacticalConditionEditing.Projection(preview.Source);
                if (editor.ConditionEvent == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.ActivationConditionOwnerId)
                    RebuildV6Recipe(destination, proxy, string.Empty);
                else if (editor.ConditionEvent == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TimelineEntryConditionOwnerId)
                    RebuildV6Timeline(destination, proxy, null, proxy.Timelines.Single(t => t.TimelineId == editor.ConditionTimeline));
                else
                {
                    var timeline = proxy.Timelines.Single(t => t.TimelineId == editor.ConditionTimeline);
                    var ev = timeline.Events.FirstOrDefault(e => e.EventId == editor.ConditionEvent);
                    if (ev == null) { editor.ConditionsOpen = false; RebuildTactical(destination); return; }
                    RebuildV6Event(destination, proxy, null, timeline, ev);
                }
                var model = destination.RecipeInspector;
                model.ConditionOwnerOnly = true;
                model.CanEdit = editor.CanEdit;
                model.CanEditDisplayTitle = false;
                model.CanEditResponsibilities = false;
                model.TacticalEditor = editor;
                model.TacticalToken = editor.Token;
                model.TacticalConditionTimeline = editor.ConditionTimeline;
                model.TacticalConditionEvent = editor.ConditionEvent;
                if (preview.IsConditionProjection && editor.Token.Target != CharacterTestTacticalEditTarget.CardActivation)
                {
                    model.AllowsEventSignal = true;
                    model.SignalEventOptions = editor.CompositionSignalOptions();
                    foreach (var row in model.Conditions.Where(r => r.NodeKind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal))
                    {
                        var option = model.SignalEventOptions.FirstOrDefault(o => o.EventId == row.SourceEventId
                            || o.EventId == editor.Token.NodeId + "/" + row.SourceEventId);
                        if (option == null) continue;
                        row.FactText = option.DisplayText;
                        row.EffectiveSummary = option.DisplayText + " " + row.ValueText;
                    }
                }
                foreach (var row in model.Conditions)
                {
                    var binding = preview.Source.PredicateBindings.FirstOrDefault(b => b.NodeId == row.NodeId);
                    if (binding == null) continue;
                    row.FactText = binding.Kind == "MovementArrived" ? "移动到位" : binding.Kind == "TurnAligned" ? "面向达标" : binding.Kind == "RootDistanceRange" ? "动作距离" : binding.Kind == "TargetFacing" ? "动作朝向" : "已停用：动作机会";
                    row.ValueText = binding.Kind == "MovementArrived" ? binding.SourceMovementEventId : binding.Kind == "TurnAligned" ? binding.SourceTurnEventId : binding.SourceActionEventId;
                    row.EffectiveSummary = row.FactText + " ← " + row.ValueText;
                    row.ValidationText = string.Empty;
                }
                return;
            }
            var action = FindCharacterActionOption(destination.CharacterActionOptions,
                new MxFramework.CharacterAction.CharacterActionId(
                    preview.Compilation?.EventActionId(selection.SelectedRecipeEventId) is int eventAction && eventAction > 0
                        ? eventAction : preview.Compilation?.ActionId ?? preview.SummaryRequest?.ActionId ?? 0));
            destination.RecipeInspector = new BehaviorRecipeTriggerEventInspectorViewModel {
                Available = true, CanEdit = editor?.CanEdit == true, TacticalEditor = editor,
                CharacterActionOptions = destination.CharacterActionOptions,
                InspectorMode = selection.RecipeInspectorTargetKind == BehaviorRecipeInspectorTargetKind.Timeline ? BehaviorRecipeInspectorMode.Timeline
                    : selection.RecipeInspectorTargetKind == BehaviorRecipeInspectorTargetKind.Recipe ? BehaviorRecipeInspectorMode.Recipe : BehaviorRecipeInspectorMode.Event,
                TacticalToken = editor?.Token ?? default,
                CharacterActionEvent = preview.FindEvent(selection.SelectedRecipeTimelineId, selection.SelectedRecipeEventId)?.Operation?.Kind == "CharacterAction",
                CharacterActionReferenceValid = action?.Valid == true,
                OperationCharacterActionId = action?.ActionId ?? default,
                CharacterActionTimelinePreview = action?.TimelinePreview,
                SelectedCharacterActionDisplayName = action?.DisplayName ?? string.Empty,
                TimelineId = string.IsNullOrEmpty(selection.SelectedRecipeTimelineId) ? preview.Source.EntryTimelineId : selection.SelectedRecipeTimelineId,
                EventId = selection.SelectedRecipeEventId,
                TacticalDescription = preview.DescribeEvent(selection.SelectedRecipeTimelineId, selection.SelectedRecipeEventId) };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    [Flags]
    internal enum CharacterTestTacticalInputRequirements
    {
        None = 0, PrimaryAction = 1, SpatialSummary = 2, StepBack = 4, StepForward = 8,
        FunctionalActions = StepBack | StepForward,
        All = PrimaryAction | SpatialSummary | StepBack
    }

    /// <summary>Low-frequency source dependency analysis. Unused declared slots are not dependencies.</summary>
    internal static class CharacterTestTacticalDependencies
    {
        internal static CharacterTestTacticalInputRequirements Analyze(CharacterTestTacticalAuthoringSource source)
        {
            var result = CharacterTestTacticalInputRequirements.None;
            if (source == null) return result;
            var usedConditions = new HashSet<string>(StringComparer.Ordinal);
            void Track(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
            {
                if(node==null)return;
                usedConditions.Add(node.NodeId);
                if(node.Children!=null)foreach(var child in node.Children)Track(child);
            }
            foreach (var timeline in source.Timelines)
            foreach (var evt in timeline.Events)
            {
                var operation = evt?.Operation;
                if (operation == null) continue;
                if (operation.Kind == "CharacterAction")
                    result |= source.Slots.FirstOrDefault(s => s?.SlotId == operation.SlotId)?.Resolver switch {
                        "stepBack" => CharacterTestTacticalInputRequirements.StepBack,
                        "stepForward" => CharacterTestTacticalInputRequirements.StepForward,
                        _ => CharacterTestTacticalInputRequirements.PrimaryAction };
                if (operation.Kind == "ApproachOptimalRange" || operation.Kind == "Movement" && operation.Movement?.DistanceSource != "Manual")
                    result |= CharacterTestTacticalInputRequirements.PrimaryAction | CharacterTestTacticalInputRequirements.SpatialSummary;
            }
            result |= AnalyzeCondition(source.ActivationConditionRoot);
            Track(source.ActivationConditionRoot);
            foreach (var timeline in source.Timelines)
            {
                result |= AnalyzeCondition(timeline.EntryConditionRoot);
                Track(timeline.EntryConditionRoot);
                foreach (var evt in timeline.Events)
                    if(evt?.Operation?.Kind!="CharacterAction")
                    {result |= AnalyzeCondition(evt?.ConditionRoot);Track(evt?.ConditionRoot);}
            }
            if (source.Timelines.SelectMany(t => t.Events).Any(e => e.Operation?.FacingSource == "ActionFacing")
                || source.PredicateBindings.Any(b => b != null && usedConditions.Contains(b.NodeId) && b.Kind != "IntentDistanceRange" && b.Kind != "MovementArrived" && b.Kind != "TurnAligned")
                || source.Timelines.SelectMany(t => t.EventBlocks).Any(b => b?.DistanceData?.Kind == "BoundActionRange"))
                result |= CharacterTestTacticalInputRequirements.PrimaryAction | CharacterTestTacticalInputRequirements.SpatialSummary;
            return result;
        }

        internal static CharacterTestTacticalInputRequirements Analyze(CharacterTestTacticalDslSource source)
        {
            var used = CharacterTestTacticalCompositionEdits.Nodes(source.Root).Where(n => n.Kind == "Reference")
                .Select(n => n.DefinitionId).ToHashSet(StringComparer.Ordinal);
            var result = CharacterTestTacticalInputRequirements.None;
            foreach (var definition in source.Definitions)
                if (used.Contains(definition.DefinitionId)) result |= Analyze(definition.Source);
            foreach (var node in CharacterTestTacticalCompositionEdits.Nodes(source.Root))
            { result |= AnalyzeCondition(node.Condition); result |= AnalyzeCondition(node.EndCondition); }
            return result | AnalyzeCondition(source.ActivationConditionRoot);
        }

        private static CharacterTestTacticalInputRequirements AnalyzeCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
        {
            if (node == null) return CharacterTestTacticalInputRequirements.None;
            var result = node.Predicate?.CharacterActionId.IsValid == true ? CharacterTestTacticalInputRequirements.PrimaryAction : CharacterTestTacticalInputRequirements.None;
            foreach (var child in node.Children) result |= AnalyzeCondition(child);
            return result;
        }
    }
}

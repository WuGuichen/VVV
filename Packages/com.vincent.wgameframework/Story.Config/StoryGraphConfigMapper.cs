using System;
using System.Collections.Generic;

namespace MxFramework.Story.Config
{
    public sealed class StoryGraphConfigMappingResult
    {
        public StoryGraphConfigMappingResult(
            StoryGraphDefinition definition,
            StoryConfigValidationResult validation)
        {
            Definition = definition;
            Validation = validation ?? new StoryConfigValidationResult(Array.Empty<StoryConfigValidationDiagnostic>());
        }

        public StoryGraphDefinition Definition { get; }
        public StoryConfigValidationResult Validation { get; }
        public IReadOnlyList<StoryConfigValidationDiagnostic> Diagnostics => Validation.Diagnostics;
        public bool IsValid => Definition != null && Validation.IsValid;
        public bool Succeeded => IsValid;
        public int DiagnosticCount => Validation.DiagnosticCount;
    }

    public static class StoryGraphConfigMapper
    {
        public static StoryGraphConfigMappingResult Map(
            StoryConfigSet configSet,
            int graphId,
            StoryConfigReferenceIndex referenceIndex = null,
            string sourcePath = null)
        {
            StoryConfigValidationResult validation = StoryConfigValidator.Validate(configSet, graphId, referenceIndex, sourcePath);
            if (!validation.IsValid)
                return new StoryGraphConfigMappingResult(null, validation);

            StoryGraphConfig graph = FindGraph(configSet.Graphs, graphId);
            StoryBeatConfig[] beatRows = CollectBeats(configSet.Beats, graphId);
            Array.Sort(beatRows, CompareBeats);

            var beats = new StoryBeatDefinition[beatRows.Length];
            for (int i = 0; i < beatRows.Length; i++)
                beats[i] = MapBeat(configSet, graphId, beatRows[i]);

            var definition = new StoryGraphDefinition(
                graph.Id,
                graph.Version,
                graph.EntryBeatId,
                beats);

            return new StoryGraphConfigMappingResult(definition, validation);
        }

        public static bool TryMap(
            StoryConfigSet configSet,
            int graphId,
            out StoryGraphDefinition definition,
            out StoryGraphConfigMappingResult result,
            StoryConfigReferenceIndex referenceIndex = null,
            string sourcePath = null)
        {
            result = Map(configSet, graphId, referenceIndex, sourcePath);
            definition = result.Definition;
            return result.IsValid;
        }

        private static StoryBeatDefinition MapBeat(
            StoryConfigSet configSet,
            int graphId,
            StoryBeatConfig beat)
        {
            StoryStepConfig[] stepRows = CollectSteps(configSet.Steps, graphId, beat.Id);
            Array.Sort(stepRows, CompareSteps);
            var steps = new StoryStepDefinition[stepRows.Length];
            for (int i = 0; i < stepRows.Length; i++)
                steps[i] = MapStep(stepRows[i]);

            StoryChoiceConfig[] choiceRows = CollectChoices(configSet.Choices, graphId, beat.Id);
            Array.Sort(choiceRows, CompareChoices);
            var choices = new StoryChoiceDefinition[choiceRows.Length];
            for (int i = 0; i < choiceRows.Length; i++)
                choices[i] = MapChoice(choiceRows[i]);

            StoryBranchConfig[] branchRows = CollectBranches(configSet.Branches, graphId, beat.Id);
            Array.Sort(branchRows, CompareBranches);
            var branches = new StoryBranchDefinition[branchRows.Length];
            for (int i = 0; i < branchRows.Length; i++)
                branches[i] = MapBranch(branchRows[i]);

            return new StoryBeatDefinition(
                beat.Id,
                steps,
                choices,
                branches,
                CopySortedIds(beat.TriggerIds),
                beat.ChoiceSetId);
        }

        private static StoryStepDefinition MapStep(StoryStepConfig step)
        {
            StoryFactKey factKey = default;
            StoryValue factValue = default;
            if (step.Kind == StoryStepKind.SetFact)
            {
                factKey = new StoryFactKey(step.FactNamespace, step.FactId);
                factValue = new StoryValue(step.FactValueKind, step.FactValueRaw);
            }

            return new StoryStepDefinition(
                step.Id,
                step.Kind,
                step.TextKey,
                step.SpeakerId,
                step.ResourceId,
                step.WaitPolicy,
                factKey,
                factValue,
                step.AuxId);
        }

        private static StoryBranchDefinition MapBranch(StoryBranchConfig branch)
        {
            return new StoryBranchDefinition(
                branch.Id,
                branch.TargetBeatId,
                branch.ConditionFactId,
                branch.Priority,
                branch.IsFallback);
        }

        private static StoryChoiceDefinition MapChoice(StoryChoiceConfig choice)
        {
            return new StoryChoiceDefinition(
                choice.Id,
                choice.LabelTextKey,
                choice.TargetBeatId,
                choice.ConditionFactId,
                CopySortedIds(choice.EffectIds));
        }

        private static StoryGraphConfig FindGraph(IReadOnlyList<StoryGraphConfig> graphs, int graphId)
        {
            for (int i = 0; i < graphs.Count; i++)
            {
                StoryGraphConfig graph = graphs[i];
                if (graph != null && graph.Id == graphId)
                    return graph;
            }

            return null;
        }

        private static StoryBeatConfig[] CollectBeats(IReadOnlyList<StoryBeatConfig> rows, int graphId)
        {
            var result = new List<StoryBeatConfig>();
            for (int i = 0; i < rows.Count; i++)
            {
                StoryBeatConfig row = rows[i];
                if (row != null && row.GraphId == graphId)
                    result.Add(row);
            }

            return result.ToArray();
        }

        private static StoryStepConfig[] CollectSteps(IReadOnlyList<StoryStepConfig> rows, int graphId, int beatId)
        {
            var result = new List<StoryStepConfig>();
            for (int i = 0; i < rows.Count; i++)
            {
                StoryStepConfig row = rows[i];
                if (row != null && row.GraphId == graphId && row.BeatId == beatId)
                    result.Add(row);
            }

            return result.ToArray();
        }

        private static StoryBranchConfig[] CollectBranches(IReadOnlyList<StoryBranchConfig> rows, int graphId, int beatId)
        {
            var result = new List<StoryBranchConfig>();
            for (int i = 0; i < rows.Count; i++)
            {
                StoryBranchConfig row = rows[i];
                if (row != null && row.GraphId == graphId && row.BeatId == beatId)
                    result.Add(row);
            }

            return result.ToArray();
        }

        private static StoryChoiceConfig[] CollectChoices(IReadOnlyList<StoryChoiceConfig> rows, int graphId, int beatId)
        {
            var result = new List<StoryChoiceConfig>();
            for (int i = 0; i < rows.Count; i++)
            {
                StoryChoiceConfig row = rows[i];
                if (row != null && row.GraphId == graphId && row.BeatId == beatId)
                    result.Add(row);
            }

            return result.ToArray();
        }

        private static int[] CopySortedIds(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<int>();

            var copy = new int[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                copy[i] = ids[i];
            Array.Sort(copy);
            return copy;
        }

        private static int CompareBeats(StoryBeatConfig left, StoryBeatConfig right)
        {
            int sortOrder = left.SortOrder.CompareTo(right.SortOrder);
            return sortOrder != 0 ? sortOrder : left.Id.CompareTo(right.Id);
        }

        private static int CompareSteps(StoryStepConfig left, StoryStepConfig right)
        {
            int sortOrder = left.SortOrder.CompareTo(right.SortOrder);
            return sortOrder != 0 ? sortOrder : left.Id.CompareTo(right.Id);
        }

        private static int CompareChoices(StoryChoiceConfig left, StoryChoiceConfig right)
        {
            int sortOrder = left.SortOrder.CompareTo(right.SortOrder);
            return sortOrder != 0 ? sortOrder : left.Id.CompareTo(right.Id);
        }

        private static int CompareBranches(StoryBranchConfig left, StoryBranchConfig right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            return priority != 0 ? priority : left.Id.CompareTo(right.Id);
        }
    }
}

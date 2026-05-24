using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Story
{
    public enum StoryStepKind : byte
    {
        None = 0,
        Line = 1,
        Presentation = 2,
        SetFact = 3,
        Wait = 4
    }

    public enum StoryPresentationWaitPolicy : byte
    {
        NoWait = 0,
        WaitForCommand = 1,
        WaitWithFrameTimeout = 2
    }

    public sealed class StoryGraphDefinition
    {
        public StoryGraphDefinition(
            int graphId,
            int version,
            int entryBeatId,
            IReadOnlyList<StoryBeatDefinition> beats)
        {
            GraphId = graphId;
            Version = version;
            EntryBeatId = entryBeatId;
            Beats = CopyList(beats);
        }

        public int GraphId { get; }
        public int Version { get; }
        public int EntryBeatId { get; }
        public IReadOnlyList<StoryBeatDefinition> Beats { get; }

        internal StoryBeatDefinition FindBeat(int beatId)
        {
            for (int i = 0; i < Beats.Count; i++)
            {
                StoryBeatDefinition beat = Beats[i];
                if (beat != null && beat.BeatId == beatId)
                {
                    return beat;
                }
            }

            return null;
        }

        internal static ReadOnlyCollection<T> CopyList<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
            {
                return new ReadOnlyCollection<T>(new List<T>());
            }

            var copy = new List<T>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                copy.Add(source[i]);
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }

    public sealed class StoryBeatDefinition
    {
        public StoryBeatDefinition(
            int beatId,
            IReadOnlyList<StoryStepDefinition> steps,
            IReadOnlyList<StoryChoiceDefinition> choices = null,
            IReadOnlyList<StoryBranchDefinition> branches = null,
            IReadOnlyList<int> triggerIds = null,
            int choiceSetId = 0)
        {
            BeatId = beatId;
            Steps = StoryGraphDefinition.CopyList(steps);
            Choices = StoryGraphDefinition.CopyList(choices);
            Branches = StoryGraphDefinition.CopyList(branches);
            TriggerIds = StoryGraphDefinition.CopyList(triggerIds);
            ChoiceSetId = choiceSetId;
        }

        public int BeatId { get; }
        public IReadOnlyList<StoryStepDefinition> Steps { get; }
        public IReadOnlyList<StoryChoiceDefinition> Choices { get; }
        public IReadOnlyList<StoryBranchDefinition> Branches { get; }
        public IReadOnlyList<int> TriggerIds { get; }
        public int ChoiceSetId { get; }

        internal bool HasTrigger(int triggerId)
        {
            for (int i = 0; i < TriggerIds.Count; i++)
            {
                if (TriggerIds[i] == triggerId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class StoryStepDefinition
    {
        public StoryStepDefinition(
            int stepId,
            StoryStepKind kind,
            int textKey = 0,
            int speakerId = 0,
            int resourceId = 0,
            StoryPresentationWaitPolicy waitPolicy = StoryPresentationWaitPolicy.NoWait,
            StoryFactKey factKey = default,
            StoryValue factValue = default,
            int auxId = 0)
        {
            StepId = stepId;
            Kind = kind;
            TextKey = textKey;
            SpeakerId = speakerId;
            ResourceId = resourceId;
            WaitPolicy = waitPolicy;
            FactKey = factKey;
            FactValue = factValue;
            AuxId = auxId;
        }

        public int StepId { get; }
        public StoryStepKind Kind { get; }
        public int TextKey { get; }
        public int SpeakerId { get; }
        public int ResourceId { get; }
        public StoryPresentationWaitPolicy WaitPolicy { get; }
        public StoryFactKey FactKey { get; }
        public StoryValue FactValue { get; }
        public int AuxId { get; }
    }

    public sealed class StoryBranchDefinition
    {
        public StoryBranchDefinition(
            int branchId,
            int targetBeatId,
            int conditionId = 0,
            int priority = 0,
            bool isFallback = false)
        {
            BranchId = branchId;
            TargetBeatId = targetBeatId;
            ConditionId = conditionId;
            Priority = priority;
            IsFallback = isFallback;
        }

        public int BranchId { get; }
        public int TargetBeatId { get; }
        public int ConditionId { get; }
        public int Priority { get; }
        public bool IsFallback { get; }
    }

    public sealed class StoryChoiceDefinition
    {
        public StoryChoiceDefinition(
            int choiceId,
            int labelTextKey,
            int targetBeatId,
            int conditionId = 0,
            IReadOnlyList<int> effectIds = null)
        {
            ChoiceId = choiceId;
            LabelTextKey = labelTextKey;
            TargetBeatId = targetBeatId;
            ConditionId = conditionId;
            EffectIds = StoryGraphDefinition.CopyList(effectIds);
        }

        public int ChoiceId { get; }
        public int LabelTextKey { get; }
        public int TargetBeatId { get; }
        public int ConditionId { get; }
        public IReadOnlyList<int> EffectIds { get; }
    }

    public readonly struct StoryChoiceView
    {
        public StoryChoiceView(int choiceId, int labelTextKey, bool enabled)
        {
            ChoiceId = choiceId;
            LabelTextKey = labelTextKey;
            Enabled = enabled;
        }

        public int ChoiceId { get; }
        public int LabelTextKey { get; }
        public bool Enabled { get; }
    }

    public interface IStoryChoiceSnapshotReader
    {
        int GetChoices(int beatInstanceId, int choiceSetId, Span<StoryChoiceView> buffer);
    }
}

using System;
using System.Collections.Generic;
using MxFramework.Events;

namespace MxFramework.Story
{
    public interface IStoryDirector : IStoryChoiceSnapshotReader
    {
        IStoryBlackboard Blackboard { get; }
        IEventBus<StoryEvent> Events { get; }
        IDisposable SubscribeEvents(Action<StoryEvent> handler);
        StoryLoadGraphResult TryLoadGraph(StoryGraphDefinition graph);
        bool LoadGraph(in StoryGraphDefinition graph);
        StoryTriggerResult TryRaiseTrigger(int triggerId, in StoryActivationContext context);
        StoryEnterBeatResult TryEnterBeat(int graphId, int beatId, in StoryActivationContext context);
        StoryTickResult Tick(in StoryTickContext context);
        StoryChoiceResult TryResolveChoice(int beatInstanceId, int choiceId);
        StoryPresentationResult CompletePresentation(int beatInstanceId, int stepId);
        StoryAbortResult AbortGraph(int graphId, int reason);
        StoryDirectorSnapshot CreateSnapshot();
    }

    public sealed class StoryDirector : IStoryDirector
    {
        public const int SchemaVersion = 1;

        private const int MaxAutoAdvanceOperations = 1024;

        private readonly Dictionary<int, GraphRuntimeState> _graphs = new Dictionary<int, GraphRuntimeState>();
        private readonly List<BeatInstanceState> _activeBeats = new List<BeatInstanceState>();
        private readonly EventBus<StoryEvent> _events;
        private int _nextBeatInstanceId = 1;

        public StoryDirector()
            : this(new StoryBlackboard(), new EventBus<StoryEvent>())
        {
        }

        public StoryDirector(StoryBlackboard blackboard, EventBus<StoryEvent> events)
        {
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public StoryBlackboard Blackboard { get; }
        IStoryBlackboard IStoryDirector.Blackboard => Blackboard;
        public IEventBus<StoryEvent> Events => _events;
        public int NextBeatInstanceId => _nextBeatInstanceId;

        public IDisposable SubscribeEvents(Action<StoryEvent> handler)
        {
            return _events.Subscribe(handler);
        }

        public StoryLoadGraphResult TryLoadGraph(StoryGraphDefinition graph)
        {
            StoryLoadGraphResult validation = ValidateGraph(graph);
            if (!validation.Success)
            {
                return validation;
            }

            _graphs[graph.GraphId] = new GraphRuntimeState(graph, StoryGraphRuntimeStatus.Loaded);
            Publish(new StoryEvent(StoryEventKind.GraphLoaded, graphId: graph.GraphId));
            return StoryLoadGraphResult.Succeeded(graph.GraphId);
        }

        public bool LoadGraph(in StoryGraphDefinition graph)
        {
            return TryLoadGraph(graph).Success;
        }

        public StoryTriggerResult TryRaiseTrigger(int triggerId, in StoryActivationContext context)
        {
            if (triggerId <= 0)
            {
                return StoryTriggerResult.Failed(StoryDirectorResultCode.InvalidTriggerId, triggerId, "Story trigger id must be positive.");
            }

            GraphRuntimeState selectedGraph = null;
            StoryBeatDefinition selectedBeat = null;
            foreach (GraphRuntimeState graph in SortedGraphs())
            {
                for (int i = 0; i < graph.Definition.Beats.Count; i++)
                {
                    StoryBeatDefinition beat = graph.Definition.Beats[i];
                    if (beat != null && beat.HasTrigger(triggerId))
                    {
                        selectedGraph = graph;
                        selectedBeat = beat;
                        break;
                    }
                }

                if (selectedBeat != null)
                {
                    break;
                }
            }

            if (selectedGraph == null || selectedBeat == null)
            {
                return StoryTriggerResult.Failed(StoryDirectorResultCode.TriggerNotFound, triggerId, "No loaded Story beat handles this trigger.");
            }

            StoryEnterBeatResult enterResult = TryEnterBeat(selectedGraph.Definition.GraphId, selectedBeat.BeatId, context);
            if (!enterResult.Success)
            {
                return new StoryTriggerResult(false, enterResult.Code, triggerId, enterResult.GraphId, enterResult.BeatId, enterResult.BeatInstanceId, enterResult.Message);
            }

            return StoryTriggerResult.Succeeded(triggerId, enterResult);
        }

        public StoryEnterBeatResult TryEnterBeat(int graphId, int beatId, in StoryActivationContext context)
        {
            return TryEnterBeatInternal(graphId, beatId, context, 0);
        }

        private StoryEnterBeatResult TryEnterBeatInternal(int graphId, int beatId, in StoryActivationContext context, int operationDepth)
        {
            if (graphId <= 0)
            {
                return StoryEnterBeatResult.Failed(StoryDirectorResultCode.InvalidGraphId, graphId, beatId, "Story graph id must be positive.");
            }

            if (beatId <= 0)
            {
                return StoryEnterBeatResult.Failed(StoryDirectorResultCode.InvalidBeatId, graphId, beatId, "Story beat id must be positive.");
            }

            GraphRuntimeState graph;
            if (!_graphs.TryGetValue(graphId, out graph))
            {
                return StoryEnterBeatResult.Failed(StoryDirectorResultCode.GraphNotLoaded, graphId, beatId, "Story graph is not loaded.");
            }

            StoryBeatDefinition beat = graph.Definition.FindBeat(beatId);
            if (beat == null)
            {
                return StoryEnterBeatResult.Failed(StoryDirectorResultCode.BeatNotFound, graphId, beatId, "Story beat is not defined in the graph.");
            }

            int instanceId = _nextBeatInstanceId++;
            var instance = new BeatInstanceState(graphId, beatId, instanceId);
            _activeBeats.Add(instance);
            graph.Status = StoryGraphRuntimeStatus.Active;
            Publish(new StoryEvent(StoryEventKind.BeatEntered, graphId, beatId, instanceId));

            StoryDirectorResultCode advance = AdvanceBeat(instance, operationDepth + 1);
            if (advance != StoryDirectorResultCode.Success)
            {
                return StoryEnterBeatResult.Failed(advance, graphId, beatId, "Story director auto-advance guard failed.");
            }

            return StoryEnterBeatResult.Succeeded(graphId, beatId, instanceId);
        }

        public StoryTickResult Tick(in StoryTickContext context)
        {
            return StoryTickResult.Succeeded();
        }

        public StoryChoiceResult TryResolveChoice(int beatInstanceId, int choiceId)
        {
            StoryChoiceResult validation = CanResolveChoice(beatInstanceId, choiceId);
            if (!validation.Success)
            {
                return validation;
            }

            BeatInstanceState instance = FindBeatInstance(beatInstanceId);
            GraphRuntimeState graph = _graphs[instance.GraphId];
            StoryBeatDefinition beat = graph.Definition.FindBeat(instance.BeatId);
            StoryChoiceDefinition choice = FindChoice(beat, choiceId);

            instance.AwaitingChoiceSetId = 0;
            Publish(new StoryEvent(StoryEventKind.ChoiceResolved, instance.GraphId, instance.BeatId, instance.BeatInstanceId, choiceSetId: ChoiceSetId(beat), auxId: choiceId));
            ExitBeat(instance);
            StoryDirectorResultCode advance = EnterChoiceTargetOrComplete(graph, choice);
            if (advance != StoryDirectorResultCode.Success)
            {
                return StoryChoiceResult.Failed(advance, beatInstanceId, choiceId, "Story director choice target failed.");
            }

            return StoryChoiceResult.Succeeded(beatInstanceId, choiceId);
        }

        public StoryPresentationResult CompletePresentation(int beatInstanceId, int stepId)
        {
            StoryPresentationResult validation = CanCompletePresentation(beatInstanceId, stepId);
            if (!validation.Success)
            {
                return validation;
            }

            BeatInstanceState instance = FindBeatInstance(beatInstanceId);
            instance.PendingPresentationStepId = 0;
            instance.PendingPresentationPolicy = StoryPresentationWaitPolicy.NoWait;
            Publish(new StoryEvent(StoryEventKind.StepCompleted, instance.GraphId, instance.BeatId, instance.BeatInstanceId, stepId: stepId));

            StoryDirectorResultCode advance = AdvanceBeat(instance, 0);
            if (advance != StoryDirectorResultCode.Success)
            {
                return StoryPresentationResult.Failed(advance, beatInstanceId, stepId, "Story director auto-advance guard failed.");
            }

            return StoryPresentationResult.Succeeded(beatInstanceId, stepId);
        }

        public StoryAbortResult AbortGraph(int graphId, int reason)
        {
            if (graphId <= 0)
            {
                return StoryAbortResult.Failed(StoryDirectorResultCode.InvalidGraphId, graphId, reason, "Story graph id must be positive.");
            }

            GraphRuntimeState graph;
            if (!_graphs.TryGetValue(graphId, out graph))
            {
                return StoryAbortResult.Failed(StoryDirectorResultCode.GraphNotLoaded, graphId, reason, "Story graph is not loaded.");
            }

            for (int i = _activeBeats.Count - 1; i >= 0; i--)
            {
                BeatInstanceState instance = _activeBeats[i];
                if (instance.GraphId == graphId)
                {
                    _activeBeats.RemoveAt(i);
                    Publish(new StoryEvent(StoryEventKind.BeatExited, instance.GraphId, instance.BeatId, instance.BeatInstanceId, auxId: reason));
                }
            }

            graph.Status = StoryGraphRuntimeStatus.Aborted;
            Publish(new StoryEvent(StoryEventKind.GraphAborted, graphId: graphId, auxId: reason));
            return StoryAbortResult.Succeeded(graphId, reason);
        }

        public StoryChoiceResult CanResolveChoice(int beatInstanceId, int choiceId)
        {
            if (beatInstanceId <= 0)
            {
                return StoryChoiceResult.Failed(StoryDirectorResultCode.InvalidBeatInstanceId, beatInstanceId, choiceId, "Story beat instance id must be positive.");
            }

            if (choiceId <= 0)
            {
                return StoryChoiceResult.Failed(StoryDirectorResultCode.InvalidChoiceId, beatInstanceId, choiceId, "Story choice id must be positive.");
            }

            BeatInstanceState instance = FindBeatInstance(beatInstanceId);
            if (instance == null)
            {
                return StoryChoiceResult.Failed(StoryDirectorResultCode.BeatInstanceNotLive, beatInstanceId, choiceId, "Story beat instance is not live.");
            }

            if (instance.AwaitingChoiceSetId <= 0)
            {
                return StoryChoiceResult.Failed(StoryDirectorResultCode.ChoiceNotOffered, beatInstanceId, choiceId, "Story beat instance is not awaiting a choice.");
            }

            GraphRuntimeState graph = _graphs[instance.GraphId];
            StoryBeatDefinition beat = graph.Definition.FindBeat(instance.BeatId);
            StoryChoiceDefinition choice = FindChoice(beat, choiceId);
            if (choice == null)
            {
                return StoryChoiceResult.Failed(StoryDirectorResultCode.ChoiceNotFound, beatInstanceId, choiceId, "Story choice is not offered by this beat.");
            }

            if (!IsConditionMet(instance.GraphId, choice.ConditionId))
            {
                return StoryChoiceResult.Failed(StoryDirectorResultCode.ChoiceDisabled, beatInstanceId, choiceId, "Story choice condition is not met.");
            }

            return StoryChoiceResult.Succeeded(beatInstanceId, choiceId);
        }

        public StoryPresentationResult CanCompletePresentation(int beatInstanceId, int stepId)
        {
            if (beatInstanceId <= 0)
            {
                return StoryPresentationResult.Failed(StoryDirectorResultCode.InvalidBeatInstanceId, beatInstanceId, stepId, "Story beat instance id must be positive.");
            }

            if (stepId <= 0)
            {
                return StoryPresentationResult.Failed(StoryDirectorResultCode.InvalidStepId, beatInstanceId, stepId, "Story step id must be positive.");
            }

            BeatInstanceState instance = FindBeatInstance(beatInstanceId);
            if (instance == null)
            {
                return StoryPresentationResult.Failed(StoryDirectorResultCode.BeatInstanceNotLive, beatInstanceId, stepId, "Story beat instance is not live.");
            }

            if (instance.PendingPresentationStepId <= 0)
            {
                return StoryPresentationResult.Failed(StoryDirectorResultCode.PresentationNotWaiting, beatInstanceId, stepId, "Story beat instance is not waiting for presentation completion.");
            }

            if (instance.PendingPresentationStepId != stepId)
            {
                return StoryPresentationResult.Failed(StoryDirectorResultCode.PresentationStepMismatch, beatInstanceId, stepId, "Story presentation completion step id does not match the pending step.");
            }

            return StoryPresentationResult.Succeeded(beatInstanceId, stepId);
        }

        public bool CanEnterBeat(int graphId, int beatId)
        {
            GraphRuntimeState graph;
            return graphId > 0
                && beatId > 0
                && _graphs.TryGetValue(graphId, out graph)
                && graph.Definition.FindBeat(beatId) != null;
        }

        public bool IsGraphLoaded(int graphId)
        {
            return graphId > 0 && _graphs.ContainsKey(graphId);
        }

        public int GetChoices(int beatInstanceId, int choiceSetId, Span<StoryChoiceView> buffer)
        {
            BeatInstanceState instance = FindBeatInstance(beatInstanceId);
            if (instance == null || instance.AwaitingChoiceSetId != choiceSetId)
            {
                return 0;
            }

            GraphRuntimeState graph = _graphs[instance.GraphId];
            StoryBeatDefinition beat = graph.Definition.FindBeat(instance.BeatId);
            int required = beat.Choices.Count;
            int written = Math.Min(required, buffer.Length);
            for (int i = 0; i < written; i++)
            {
                StoryChoiceDefinition choice = beat.Choices[i];
                buffer[i] = new StoryChoiceView(choice.ChoiceId, choice.LabelTextKey, IsConditionMet(instance.GraphId, choice.ConditionId));
            }

            return required;
        }

        public StoryDirectorSnapshot CreateSnapshot()
        {
            var graphSnapshots = new List<StoryGraphRuntimeSnapshot>(_graphs.Count);
            foreach (GraphRuntimeState graph in SortedGraphs())
            {
                graphSnapshots.Add(new StoryGraphRuntimeSnapshot(
                    graph.Definition.GraphId,
                    graph.Definition.Version,
                    graph.Status));
            }

            var beatSnapshots = new List<StoryBeatInstanceSnapshot>(_activeBeats.Count);
            var beats = new List<BeatInstanceState>(_activeBeats);
            beats.Sort(CompareBeatInstances);
            for (int i = 0; i < beats.Count; i++)
            {
                BeatInstanceState beat = beats[i];
                beatSnapshots.Add(beat.ToSnapshot());
            }

            return new StoryDirectorSnapshot(
                SchemaVersion,
                _nextBeatInstanceId,
                graphSnapshots,
                beatSnapshots,
                Blackboard.CreateOrderedSnapshot());
        }

        public StoryDirectorSaveState CaptureSaveState()
        {
            var graphs = new List<StoryGraphSaveState>(_graphs.Count);
            foreach (GraphRuntimeState graph in SortedGraphs())
            {
                graphs.Add(new StoryGraphSaveState(graph.Definition, graph.Status));
            }

            var beats = new List<BeatInstanceState>(_activeBeats);
            beats.Sort(CompareBeatInstances);
            var beatStates = new List<StoryBeatInstanceSaveState>(beats.Count);
            for (int i = 0; i < beats.Count; i++)
            {
                beatStates.Add(beats[i].ToSaveState());
            }

            return new StoryDirectorSaveState(
                StoryDirectorSaveState.CurrentSchemaVersion,
                _nextBeatInstanceId,
                graphs,
                beatStates,
                Blackboard.CreateOrderedSnapshot());
        }

        public StoryLoadGraphResult RestoreSaveState(StoryDirectorSaveState state)
        {
            List<GraphRuntimeState> stagedGraphs;
            List<BeatInstanceState> stagedBeats;
            List<StoryFactEntry> stagedFacts;
            int stagedNextBeatInstanceId;
            StoryLoadGraphResult validation = ValidateSaveState(
                state,
                out stagedGraphs,
                out stagedBeats,
                out stagedFacts,
                out stagedNextBeatInstanceId);
            if (!validation.Success)
            {
                return validation;
            }

            _graphs.Clear();
            _activeBeats.Clear();
            Blackboard.Clear();

            for (int i = 0; i < stagedGraphs.Count; i++)
            {
                GraphRuntimeState graph = stagedGraphs[i];
                _graphs[graph.Definition.GraphId] = graph;
            }

            for (int i = 0; i < stagedFacts.Count; i++)
            {
                StoryFactEntry fact = stagedFacts[i];
                Blackboard.Set(fact.Key, fact.Value);
            }

            for (int i = 0; i < stagedBeats.Count; i++)
            {
                _activeBeats.Add(stagedBeats[i]);
            }

            _nextBeatInstanceId = stagedNextBeatInstanceId;
            return StoryLoadGraphResult.Succeeded(0);
        }

        private StoryDirectorResultCode AdvanceBeat(BeatInstanceState instance, int operationDepth)
        {
            if (operationDepth > MaxAutoAdvanceOperations)
            {
                return StoryDirectorResultCode.DirectorGuardExceeded;
            }

            GraphRuntimeState graph = _graphs[instance.GraphId];
            StoryBeatDefinition beat = graph.Definition.FindBeat(instance.BeatId);

            while (instance.CurrentStepIndex < beat.Steps.Count)
            {
                StoryStepDefinition step = beat.Steps[instance.CurrentStepIndex];
                instance.CurrentStepIndex++;
                Publish(new StoryEvent(StoryEventKind.StepStarted, instance.GraphId, instance.BeatId, instance.BeatInstanceId, stepId: step.StepId, auxId: step.AuxId));

                if (step.Kind == StoryStepKind.SetFact)
                {
                    Blackboard.Set(step.FactKey, step.FactValue);
                    Publish(new StoryEvent(StoryEventKind.FactChanged, instance.GraphId, instance.BeatId, instance.BeatInstanceId, stepId: step.StepId, auxId: step.FactKey.Id));
                }

                if (step.WaitPolicy == StoryPresentationWaitPolicy.WaitForCommand
                    || step.WaitPolicy == StoryPresentationWaitPolicy.WaitWithFrameTimeout)
                {
                    instance.PendingPresentationStepId = step.StepId;
                    instance.PendingPresentationPolicy = step.WaitPolicy;
                    return StoryDirectorResultCode.Success;
                }

                Publish(new StoryEvent(StoryEventKind.StepCompleted, instance.GraphId, instance.BeatId, instance.BeatInstanceId, stepId: step.StepId));
            }

            if (beat.Choices.Count > 0)
            {
                instance.AwaitingChoiceSetId = ChoiceSetId(beat);
                Publish(new StoryEvent(StoryEventKind.ChoiceOffered, instance.GraphId, instance.BeatId, instance.BeatInstanceId, choiceSetId: instance.AwaitingChoiceSetId));
                return StoryDirectorResultCode.Success;
            }

            StoryBranchDefinition branch = SelectBranch(instance.GraphId, beat);
            ExitBeat(instance);
            if (branch == null || branch.TargetBeatId <= 0)
            {
                CompleteGraph(graph);
                return StoryDirectorResultCode.Success;
            }

            StoryEnterBeatResult enterResult = TryEnterBeatInternal(graph.Definition.GraphId, branch.TargetBeatId, default, operationDepth + 1);
            return enterResult.Success ? StoryDirectorResultCode.Success : enterResult.Code;
        }

        private StoryDirectorResultCode EnterChoiceTargetOrComplete(GraphRuntimeState graph, StoryChoiceDefinition choice)
        {
            if (choice.TargetBeatId <= 0)
            {
                CompleteGraph(graph);
                return StoryDirectorResultCode.Success;
            }

            StoryEnterBeatResult enterResult = TryEnterBeatInternal(graph.Definition.GraphId, choice.TargetBeatId, default, 0);
            return enterResult.Success ? StoryDirectorResultCode.Success : enterResult.Code;
        }

        private void ExitBeat(BeatInstanceState instance)
        {
            _activeBeats.Remove(instance);
            Publish(new StoryEvent(StoryEventKind.BeatExited, instance.GraphId, instance.BeatId, instance.BeatInstanceId));
        }

        private void CompleteGraph(GraphRuntimeState graph)
        {
            graph.Status = StoryGraphRuntimeStatus.Completed;
            Publish(new StoryEvent(StoryEventKind.GraphCompleted, graphId: graph.Definition.GraphId));
        }

        private StoryBranchDefinition SelectBranch(int graphId, StoryBeatDefinition beat)
        {
            if (beat.Branches.Count == 0)
            {
                return null;
            }

            var branches = new List<StoryBranchDefinition>(beat.Branches);
            branches.Sort(CompareBranches);
            StoryBranchDefinition fallback = null;
            for (int i = 0; i < branches.Count; i++)
            {
                StoryBranchDefinition branch = branches[i];
                if (branch.IsFallback && fallback == null)
                {
                    fallback = branch;
                }

                if (!branch.IsFallback && IsConditionMet(graphId, branch.ConditionId))
                {
                    return branch;
                }
            }

            return fallback;
        }

        private bool IsConditionMet(int graphId, int conditionId)
        {
            if (conditionId <= 0)
            {
                return true;
            }

            StoryValue value;
            if (Blackboard.TryGet(new StoryFactKey(graphId, conditionId), out value) && value.Kind == StoryValueKind.Bool)
            {
                return value.Raw != 0L;
            }

            if (Blackboard.TryGet(new StoryFactKey(0, conditionId), out value) && value.Kind == StoryValueKind.Bool)
            {
                return value.Raw != 0L;
            }

            return false;
        }

        private BeatInstanceState FindBeatInstance(int beatInstanceId)
        {
            for (int i = 0; i < _activeBeats.Count; i++)
            {
                if (_activeBeats[i].BeatInstanceId == beatInstanceId)
                {
                    return _activeBeats[i];
                }
            }

            return null;
        }

        private static StoryChoiceDefinition FindChoice(StoryBeatDefinition beat, int choiceId)
        {
            for (int i = 0; i < beat.Choices.Count; i++)
            {
                StoryChoiceDefinition choice = beat.Choices[i];
                if (choice.ChoiceId == choiceId)
                {
                    return choice;
                }
            }

            return null;
        }

        private List<GraphRuntimeState> SortedGraphs()
        {
            var graphs = new List<GraphRuntimeState>(_graphs.Count);
            foreach (KeyValuePair<int, GraphRuntimeState> pair in _graphs)
            {
                graphs.Add(pair.Value);
            }

            graphs.Sort(CompareGraphs);
            return graphs;
        }

        private void Publish(in StoryEvent evt)
        {
            _events.Publish(evt);
        }

        private static StoryLoadGraphResult ValidateSaveState(
            StoryDirectorSaveState state,
            out List<GraphRuntimeState> stagedGraphs,
            out List<BeatInstanceState> stagedBeats,
            out List<StoryFactEntry> stagedFacts,
            out int nextBeatInstanceId)
        {
            stagedGraphs = new List<GraphRuntimeState>();
            stagedBeats = new List<BeatInstanceState>();
            stagedFacts = new List<StoryFactEntry>();
            nextBeatInstanceId = 1;

            if (state == null)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, 0, "Story director save state is null.");
            }

            if (state.SchemaVersion != StoryDirectorSaveState.CurrentSchemaVersion)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.UnsupportedSchemaVersion, 0, "Story director save state schema version is unsupported.");
            }

            if (state.NextBeatInstanceId <= 0)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidBeatInstanceId, 0, "Story director save state next beat instance id must be positive.");
            }

            var graphMap = new Dictionary<int, GraphRuntimeState>();
            for (int i = 0; i < state.Graphs.Count; i++)
            {
                StoryGraphSaveState graphState = state.Graphs[i];
                if (graphState == null)
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, 0, "Story director save state contains a null graph state.");
                }

                StoryLoadGraphResult graphValidation = ValidateGraph(graphState.Definition);
                if (!graphValidation.Success)
                {
                    return graphValidation;
                }

                if (!IsValidGraphStatus(graphState.Status))
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, graphState.Definition.GraphId, "Story director save state graph status is invalid.");
                }

                if (graphMap.ContainsKey(graphState.Definition.GraphId))
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, graphState.Definition.GraphId, "Story director save state contains duplicate graph ids.");
                }

                var graph = new GraphRuntimeState(graphState.Definition, graphState.Status);
                graphMap.Add(graphState.Definition.GraphId, graph);
                stagedGraphs.Add(graph);
            }

            var factKeys = new HashSet<StoryFactKey>();
            for (int i = 0; i < state.Facts.Count; i++)
            {
                StoryFactEntry fact = state.Facts[i];
                if (!fact.Key.IsValid)
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, fact.Key.Namespace, "Story director save state contains an invalid fact key.");
                }

                if (!factKeys.Add(fact.Key))
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, fact.Key.Namespace, "Story director save state contains duplicate fact keys.");
                }

                stagedFacts.Add(fact);
            }

            var beatInstanceIds = new HashSet<int>();
            var activeGraphIds = new HashSet<int>();
            nextBeatInstanceId = state.NextBeatInstanceId;
            for (int i = 0; i < state.ActiveBeatInstances.Count; i++)
            {
                StoryBeatInstanceSaveState saved = state.ActiveBeatInstances[i];
                StoryLoadGraphResult beatValidation = ValidateBeatInstanceSaveState(saved, graphMap);
                if (!beatValidation.Success)
                {
                    return beatValidation;
                }

                if (!beatInstanceIds.Add(saved.BeatInstanceId))
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidBeatInstanceId, saved.GraphId, "Story director save state contains duplicate beat instance ids.");
                }

                stagedBeats.Add(new BeatInstanceState(saved));
                activeGraphIds.Add(saved.GraphId);
                nextBeatInstanceId = Math.Max(nextBeatInstanceId, saved.BeatInstanceId + 1);
            }

            if (stagedBeats.Count > 0 && state.NextBeatInstanceId != nextBeatInstanceId)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidBeatInstanceId, 0, "Story director save state next beat instance id would reuse an active instance id.");
            }

            for (int i = 0; i < stagedGraphs.Count; i++)
            {
                GraphRuntimeState graph = stagedGraphs[i];
                bool hasActiveBeat = activeGraphIds.Contains(graph.Definition.GraphId);
                if (hasActiveBeat && graph.Status != StoryGraphRuntimeStatus.Active)
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, graph.Definition.GraphId, "Story director save state active beat references a non-active graph.");
                }

                if (!hasActiveBeat && graph.Status == StoryGraphRuntimeStatus.Active)
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, graph.Definition.GraphId, "Story director save state active graph has no active beat instance.");
                }
            }

            return StoryLoadGraphResult.Succeeded(0);
        }

        private static StoryLoadGraphResult ValidateBeatInstanceSaveState(
            StoryBeatInstanceSaveState saved,
            Dictionary<int, GraphRuntimeState> graphMap)
        {
            if (saved == null)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, 0, "Story director save state contains a null beat instance.");
            }

            if (saved.BeatInstanceId <= 0)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidBeatInstanceId, saved.GraphId, "Story director save state beat instance id must be positive.");
            }

            GraphRuntimeState graph;
            if (!graphMap.TryGetValue(saved.GraphId, out graph))
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.GraphNotLoaded, saved.GraphId, "Saved Story beat instance references an unloaded graph.");
            }

            StoryBeatDefinition beat = graph.Definition.FindBeat(saved.BeatId);
            if (beat == null)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.BeatNotFound, saved.GraphId, "Saved Story beat instance references a missing beat.");
            }

            if (saved.CurrentStepIndex < 0 || saved.CurrentStepIndex > beat.Steps.Count)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidStepId, saved.GraphId, "Saved Story beat instance has an invalid current step cursor.");
            }

            if (saved.PendingPresentationStepId < 0)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidStepId, saved.GraphId, "Saved Story beat instance has an invalid pending presentation step id.");
            }

            if (saved.AwaitingChoiceSetId < 0)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.ChoiceNotOffered, saved.GraphId, "Saved Story beat instance has an invalid awaiting choice set id.");
            }

            if (!IsValidPresentationWaitPolicy(saved.PendingPresentationPolicy))
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.PresentationNotWaiting, saved.GraphId, "Saved Story beat instance has an invalid presentation wait policy.");
            }

            if (saved.PendingPresentationStepId > 0)
            {
                if (saved.PendingPresentationPolicy == StoryPresentationWaitPolicy.NoWait)
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.PresentationNotWaiting, saved.GraphId, "Saved Story beat instance has a pending presentation step with NoWait policy.");
                }

                if (saved.AwaitingChoiceSetId > 0)
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, saved.GraphId, "Saved Story beat instance cannot wait for presentation and choice at the same time.");
                }

                int stepIndex = FindStepIndex(beat, saved.PendingPresentationStepId);
                if (stepIndex < 0)
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidStepId, saved.GraphId, "Saved Story beat instance pending presentation step is missing.");
                }

                StoryStepDefinition step = beat.Steps[stepIndex];
                if (step.WaitPolicy != saved.PendingPresentationPolicy)
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.PresentationStepMismatch, saved.GraphId, "Saved Story beat instance pending presentation policy does not match the step definition.");
                }

                if (saved.CurrentStepIndex != stepIndex + 1)
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidStepId, saved.GraphId, "Saved Story beat instance step cursor does not match the pending presentation step.");
                }
            }
            else if (saved.PendingPresentationPolicy != StoryPresentationWaitPolicy.NoWait)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.PresentationNotWaiting, saved.GraphId, "Saved Story beat instance has a presentation policy without a pending step.");
            }

            if (saved.AwaitingChoiceSetId > 0)
            {
                if (beat.Choices.Count == 0 || saved.AwaitingChoiceSetId != ChoiceSetId(beat))
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.ChoiceNotOffered, saved.GraphId, "Saved Story beat instance has an invalid choice set.");
                }

                if (saved.CurrentStepIndex != beat.Steps.Count)
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidStepId, saved.GraphId, "Saved Story beat instance offers choices before all steps are complete.");
                }
            }
            else if (saved.PendingPresentationStepId == 0)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, saved.GraphId, "Saved Story active beat instance is neither waiting for presentation nor awaiting choices.");
            }

            return StoryLoadGraphResult.Succeeded(saved.GraphId);
        }

        private static StoryLoadGraphResult ValidateGraph(StoryGraphDefinition graph)
        {
            if (graph == null)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, 0, "Story graph cannot be null.");
            }

            if (graph.GraphId <= 0)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidGraphId, graph.GraphId, "Story graph id must be positive.");
            }

            if (graph.EntryBeatId <= 0)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidBeatId, graph.GraphId, "Story entry beat id must be positive.");
            }

            var beatIds = new HashSet<int>();
            bool hasEntry = false;
            for (int i = 0; i < graph.Beats.Count; i++)
            {
                StoryBeatDefinition beat = graph.Beats[i];
                if (beat == null || beat.BeatId <= 0 || !beatIds.Add(beat.BeatId))
                {
                    return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, graph.GraphId, "Story graph contains a null, invalid, or duplicate beat.");
                }

                hasEntry |= beat.BeatId == graph.EntryBeatId;
            }

            for (int i = 0; i < graph.Beats.Count; i++)
            {
                StoryBeatDefinition beat = graph.Beats[i];
                var stepIds = new HashSet<int>();
                for (int stepIndex = 0; stepIndex < beat.Steps.Count; stepIndex++)
                {
                    StoryStepDefinition step = beat.Steps[stepIndex];
                    if (step == null || step.StepId <= 0 || step.Kind == StoryStepKind.None || !stepIds.Add(step.StepId))
                    {
                        return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, graph.GraphId, "Story beat contains a null, invalid, or duplicate step.");
                    }

                    if (step.Kind == StoryStepKind.SetFact && !step.FactKey.IsValid)
                    {
                        return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, graph.GraphId, "Story set-fact step has an invalid fact key.");
                    }

                    if (!IsValidPresentationWaitPolicy(step.WaitPolicy))
                    {
                        return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, graph.GraphId, "Story step has an invalid presentation wait policy.");
                    }
                }

                var choiceIds = new HashSet<int>();
                for (int choiceIndex = 0; choiceIndex < beat.Choices.Count; choiceIndex++)
                {
                    StoryChoiceDefinition choice = beat.Choices[choiceIndex];
                    if (choice == null || choice.ChoiceId <= 0 || !choiceIds.Add(choice.ChoiceId))
                    {
                        return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, graph.GraphId, "Story beat contains a null, invalid, or duplicate choice.");
                    }

                    if (choice.TargetBeatId > 0 && !beatIds.Contains(choice.TargetBeatId))
                    {
                        return StoryLoadGraphResult.Failed(StoryDirectorResultCode.BeatNotFound, graph.GraphId, "Story choice targets a missing beat.");
                    }
                }

                for (int branchIndex = 0; branchIndex < beat.Branches.Count; branchIndex++)
                {
                    StoryBranchDefinition branch = beat.Branches[branchIndex];
                    if (branch == null || branch.BranchId <= 0)
                    {
                        return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, graph.GraphId, "Story beat contains a null or invalid branch.");
                    }

                    if (branch.TargetBeatId > 0 && !beatIds.Contains(branch.TargetBeatId))
                    {
                        return StoryLoadGraphResult.Failed(StoryDirectorResultCode.BeatNotFound, graph.GraphId, "Story branch targets a missing beat.");
                    }
                }

                for (int triggerIndex = 0; triggerIndex < beat.TriggerIds.Count; triggerIndex++)
                {
                    if (beat.TriggerIds[triggerIndex] <= 0)
                    {
                        return StoryLoadGraphResult.Failed(StoryDirectorResultCode.InvalidDefinition, graph.GraphId, "Story beat contains an invalid trigger id.");
                    }
                }
            }

            if (!hasEntry)
            {
                return StoryLoadGraphResult.Failed(StoryDirectorResultCode.BeatNotFound, graph.GraphId, "Story entry beat does not exist in graph.");
            }

            return StoryLoadGraphResult.Succeeded(graph.GraphId);
        }

        private static bool IsValidGraphStatus(StoryGraphRuntimeStatus status)
        {
            return status == StoryGraphRuntimeStatus.Loaded
                || status == StoryGraphRuntimeStatus.Active
                || status == StoryGraphRuntimeStatus.Completed
                || status == StoryGraphRuntimeStatus.Aborted;
        }

        private static bool IsValidPresentationWaitPolicy(StoryPresentationWaitPolicy policy)
        {
            return policy == StoryPresentationWaitPolicy.NoWait
                || policy == StoryPresentationWaitPolicy.WaitForCommand
                || policy == StoryPresentationWaitPolicy.WaitWithFrameTimeout;
        }

        private static int FindStepIndex(StoryBeatDefinition beat, int stepId)
        {
            for (int i = 0; i < beat.Steps.Count; i++)
            {
                if (beat.Steps[i].StepId == stepId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int ChoiceSetId(StoryBeatDefinition beat)
        {
            return beat.ChoiceSetId > 0 ? beat.ChoiceSetId : beat.BeatId;
        }

        private static int CompareGraphs(GraphRuntimeState left, GraphRuntimeState right)
        {
            return left.Definition.GraphId.CompareTo(right.Definition.GraphId);
        }

        private static int CompareBeatInstances(BeatInstanceState left, BeatInstanceState right)
        {
            return left.BeatInstanceId.CompareTo(right.BeatInstanceId);
        }

        private static int CompareBranches(StoryBranchDefinition left, StoryBranchDefinition right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            return left.BranchId.CompareTo(right.BranchId);
        }

        private sealed class GraphRuntimeState
        {
            public GraphRuntimeState(StoryGraphDefinition definition, StoryGraphRuntimeStatus status)
            {
                Definition = definition;
                Status = status;
            }

            public StoryGraphDefinition Definition { get; }
            public StoryGraphRuntimeStatus Status { get; set; }
        }

        private sealed class BeatInstanceState
        {
            public BeatInstanceState(int graphId, int beatId, int beatInstanceId)
            {
                GraphId = graphId;
                BeatId = beatId;
                BeatInstanceId = beatInstanceId;
            }

            public BeatInstanceState(StoryBeatInstanceSaveState state)
            {
                GraphId = state.GraphId;
                BeatId = state.BeatId;
                BeatInstanceId = state.BeatInstanceId;
                CurrentStepIndex = state.CurrentStepIndex;
                PendingPresentationStepId = state.PendingPresentationStepId;
                PendingPresentationPolicy = state.PendingPresentationPolicy;
                AwaitingChoiceSetId = state.AwaitingChoiceSetId;
            }

            public int GraphId { get; }
            public int BeatId { get; }
            public int BeatInstanceId { get; }
            public int CurrentStepIndex { get; set; }
            public int PendingPresentationStepId { get; set; }
            public StoryPresentationWaitPolicy PendingPresentationPolicy { get; set; }
            public int AwaitingChoiceSetId { get; set; }

            public StoryBeatInstanceSnapshot ToSnapshot()
            {
                return new StoryBeatInstanceSnapshot(
                    GraphId,
                    BeatId,
                    BeatInstanceId,
                    CurrentStepIndex,
                    PendingPresentationStepId,
                    PendingPresentationPolicy,
                    AwaitingChoiceSetId);
            }

            public StoryBeatInstanceSaveState ToSaveState()
            {
                return new StoryBeatInstanceSaveState(
                    GraphId,
                    BeatId,
                    BeatInstanceId,
                    CurrentStepIndex,
                    PendingPresentationStepId,
                    PendingPresentationPolicy,
                    AwaitingChoiceSetId);
            }
        }
    }
}

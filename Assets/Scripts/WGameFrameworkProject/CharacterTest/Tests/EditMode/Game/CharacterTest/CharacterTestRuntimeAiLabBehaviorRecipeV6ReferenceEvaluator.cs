using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiLabBehaviorRecipeV6Evaluator
    {
        private readonly CharacterTestRuntimeAiLabCompiledRecipeV6 _recipe;
        private readonly ICharacterTestRuntimeAiLabBehaviorCardLogicRuntime _runtime;
        private readonly ICharacterTestRuntimeAiLabBehaviorRecipeV5Runtime _orchestration;
        private readonly int[] _executionCounts;
        private readonly int[] _lastExecutionFrames;
        private readonly CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult[] _results;
        private readonly CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult[] _previousResults;
        private readonly int[] _loopRepeatCounts;
        private int _currentTimelineIndex = -1;
        private int _cardStartedFrame = -1;
        private int _timelineEnteredFrame = -1;
        private int _lastEvaluatedFrame = -1;
        private int _localPlayheadFrame;
        private int _lastSampledPlayheadFrame = -1;
        private int _timelinePassGeneration;
        private int _activeWaitEventIndex = -1;
        private int _pendingPlayheadTargetFrame = -1;
        private int _pendingTimeControlEventIndex = -1;
        private bool _waitingForInstantOwnerAtTimelineEnd;
        private int _actorOwnerEventIndex = -1;
        private long _actorOwnerActionInstanceId;
        private int _pendingEventIndex = -1;
        private int _pendingTargetTimelineIndex = -1;
        private CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation _pendingOperation;
        private int _lastTriggeredEventIndex = -1;
        private string _lastTriggeredEventId = string.Empty;

        public CharacterTestRuntimeAiLabBehaviorRecipeV6Evaluator(
            CharacterTestRuntimeAiLabCompiledRecipeV6 recipe,
            ICharacterTestRuntimeAiLabBehaviorCardLogicRuntime runtime)
        {
            _recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _orchestration = runtime as ICharacterTestRuntimeAiLabBehaviorRecipeV5Runtime;
            _executionCounts = new int[_recipe.Events.Count];
            _lastExecutionFrames = new int[_recipe.Events.Count];
            _results = new CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult[_recipe.Events.Count];
            _previousResults = new CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult[_recipe.Events.Count];
            _loopRepeatCounts = new int[_recipe.Events.Count];
            ResetArrays();
        }

        public CharacterTestRuntimeAiLabLogicRuntimeState State { get; private set; } = CharacterTestRuntimeAiLabLogicRuntimeState.Inactive;
        public int CurrentTimelineIndex => _currentTimelineIndex;
        public int CardStartedFrame => _cardStartedFrame;
        public int TimelineEnteredFrame => _timelineEnteredFrame;
        public int LastEvaluatedFrame => _lastEvaluatedFrame;
        public int LocalFrame => Math.Max(0, _localPlayheadFrame);
        public int TimelinePassGeneration => _timelinePassGeneration;
        public int ActiveWaitEventIndex => _activeWaitEventIndex;
        public int PendingPlayheadTargetFrame => _pendingPlayheadTargetFrame;
        public int PendingTimeControlEventIndex => _pendingTimeControlEventIndex;
        public bool WaitingForInstantOwnerAtTimelineEnd => _waitingForInstantOwnerAtTimelineEnd;
        public int[] LoopRepeatCounts => _loopRepeatCounts;
        public int ActorOwnerEventIndex => _actorOwnerEventIndex;
        public int PendingEventIndex => _pendingEventIndex;
        public int PendingTargetTimelineIndex => _pendingTargetTimelineIndex;
        public CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation PendingOperation => _pendingOperation;
        public int LastTriggeredEventIndex => _lastTriggeredEventIndex;
        public string LastTriggeredEventId => _lastTriggeredEventId;
        public long OwnedPrimaryActionInstanceId => _actorOwnerActionInstanceId;

        public void Reset()
        {
            State = CharacterTestRuntimeAiLabLogicRuntimeState.Inactive;
            _currentTimelineIndex = -1;
            _cardStartedFrame = -1;
            _timelineEnteredFrame = -1;
            _lastEvaluatedFrame = -1;
            _localPlayheadFrame = 0;
            _lastSampledPlayheadFrame = -1;
            _timelinePassGeneration = 0;
            _activeWaitEventIndex = -1;
            _pendingPlayheadTargetFrame = -1;
            _pendingTimeControlEventIndex = -1;
            _waitingForInstantOwnerAtTimelineEnd = false;
            _actorOwnerEventIndex = -1;
            _actorOwnerActionInstanceId = 0L;
            _pendingEventIndex = -1;
            _pendingTargetTimelineIndex = -1;
            _pendingOperation = CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.None;
            _lastTriggeredEventIndex = -1;
            _lastTriggeredEventId = string.Empty;
            ResetArrays();
        }

        public bool Start(int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            if (State != CharacterTestRuntimeAiLabLogicRuntimeState.Inactive
                || !Evaluate(_recipe.ActivationCondition))
                return false;
            return StartCommitted(frame, events);
        }

        public bool StartCommitted(int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            if (State != CharacterTestRuntimeAiLabLogicRuntimeState.Inactive)
                return false;
            if (_recipe.EntryTimelineIndex < 0 || _recipe.EntryTimelineIndex >= _recipe.Timelines.Count)
                return false;
            if (!Evaluate(_recipe.Timelines[_recipe.EntryTimelineIndex].EntryCondition))
                return false;
            State = CharacterTestRuntimeAiLabLogicRuntimeState.Running;
            _cardStartedFrame = frame;
            return EnterTimeline(_recipe.EntryTimelineIndex, frame, events);
        }

        // NoAlloc after evaluator buffers are initialized.
        public void Tick(int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            events?.Clear();
            if (State != CharacterTestRuntimeAiLabLogicRuntimeState.Running
                && State != CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning)
                return;

            Array.Copy(_results, _previousResults, _results.Length);
            _lastEvaluatedFrame = frame;
            RefreshOwnedOperationResult(frame, events);

            if (_pendingOperation != CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.None)
            {
                bool actorBoundary = _pendingOperation == CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.StartActorEvent
                    || _pendingOperation == CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.CancelActorEvent;
                AdvancePending(frame, events);
                if (!actorBoundary || _pendingOperation != CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.None
                    || State != CharacterTestRuntimeAiLabLogicRuntimeState.Running)
                    return;
            }
            if (_currentTimelineIndex < 0 || _currentTimelineIndex >= _recipe.Timelines.Count)
            {
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Failed;
                return;
            }

            CharacterTestRuntimeAiLabCompiledRecipeV6Timeline timeline = _recipe.Timelines[_currentTimelineIndex];
            int localFrame = LocalFrame;
            if (timeline.DurationFrames <= 0 || localFrame >= timeline.DurationFrames)
            {
                if (_actorOwnerEventIndex >= 0 && _recipe.Events[_actorOwnerEventIndex].IsInstant
                    && _recipe.Events[_actorOwnerEventIndex].OperationKind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                {
                    _waitingForInstantOwnerAtTimelineEnd = true;
                    return;
                }
                _waitingForInstantOwnerAtTimelineEnd = false;
                BeginTimelineCompletion(frame, events);
                return;
            }
            _waitingForInstantOwnerAtTimelineEnd = false;

            CharacterTestRuntimeAiLabCompiledRecipeV6Event selected = null;
            CharacterTestRuntimeAiLabCompiledRecipeV6Event timelineJump = null;
            for (int i = 0; i < timeline.Events.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledRecipeV6Event candidate = timeline.Events[i];
                if (!IsEligible(candidate, frame, localFrame))
                    continue;
                if (candidate.IsTimelineControl)
                {
                    if (candidate.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline)
                    {
                        // A jump is committed after sampling; only a card exit stops this pass.
                        timelineJump ??= candidate;
                        continue;
                    }
                    _lastSampledPlayheadFrame = localFrame;
                    BeginTimelineControl(candidate, frame, events);
                    return;
                }
                if (!candidate.UsesActorControl)
                    continue;
                if (candidate.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                {
                    // Select without starting: later conditions still observe the actual actor.
                    selected = candidate;
                    continue;
                }
                if (selected != null)
                    continue;
                if (_actorOwnerEventIndex >= 0)
                {
                    CharacterTestRuntimeAiLabCompiledRecipeV6Event owner = _recipe.Events[_actorOwnerEventIndex];
                    if (candidate.Index == owner.Index)
                        continue;
                    if (Compare(candidate, owner) >= 0)
                        continue;
                }
                selected = candidate;
            }
            _lastSampledPlayheadFrame = localFrame;
            if (timelineJump != null)
            {
                BeginTimelineControl(timelineJump, frame, events);
                return;
            }
            if (selected != null)
            {
                if (_actorOwnerEventIndex >= 0)
                    BeginReplaceOwner(selected, frame, events);
                else if (!StartActorEvent(selected, frame, events))
                    return;
            }
            else if (_actorOwnerEventIndex >= 0)
            {
                CharacterTestRuntimeAiLabCompiledRecipeV6Event owner = _recipe.Events[_actorOwnerEventIndex];
                int clock = owner.ClockOrigin == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe
                    ? Math.Max(0, frame - _cardStartedFrame) : localFrame;
                if ((owner.IsDuration || owner.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                    && (clock < owner.StartFrame || clock >= owner.EndFrame))
                    BeginCancelOwner(owner, frame, events);
            }
            AdvancePlayhead(timeline, frame, events);
        }

        public CharacterTestRuntimeAiLabLogicEvaluatorSnapshot CreateSnapshot()
        {
            var counts = (int[])_executionCounts.Clone();
            var lastFrames = (int[])_lastExecutionFrames.Clone();
            var results = new int[_results.Length];
            var previous = new int[_previousResults.Length];
            for (int i = 0; i < results.Length; i++)
            {
                results[i] = (int)_results[i];
                previous[i] = (int)_previousResults[i];
            }
            return new CharacterTestRuntimeAiLabLogicEvaluatorSnapshot(
                State,
                _currentTimelineIndex,
                _cardStartedFrame,
                _timelineEnteredFrame,
                -1,
                Array.Empty<bool>(),
                _lastEvaluatedFrame,
                OwnedPrimaryActionInstanceId,
                Array.Empty<bool>(),
                _lastTriggeredEventIndex,
                counts,
                lastFrames,
                results,
                previous,
                _actorOwnerEventIndex,
                _pendingEventIndex,
                _pendingTargetTimelineIndex,
                (int)_pendingOperation,
                _localPlayheadFrame,
                _timelinePassGeneration,
                (int[])_loopRepeatCounts.Clone(),
                _activeWaitEventIndex,
                _pendingPlayheadTargetFrame,
                _pendingTimeControlEventIndex,
                _waitingForInstantOwnerAtTimelineEnd,
                _lastSampledPlayheadFrame);
        }

        public bool TryRestoreSnapshot(CharacterTestRuntimeAiLabLogicEvaluatorSnapshot snapshot)
        {
            if (snapshot == null
                || snapshot.EventExecutionCounts.Length != _executionCounts.Length
                || snapshot.EventLastExecutionFrames.Length != _lastExecutionFrames.Length
                || snapshot.EventResults.Length != _results.Length
                || snapshot.PreviousEventResults.Length != _previousResults.Length
                || snapshot.LoopRepeatCounts.Length != _loopRepeatCounts.Length
                || snapshot.CurrentTimelineIndex < -1
                || snapshot.CurrentTimelineIndex >= _recipe.Timelines.Count
                || snapshot.ActorOwnerEventIndex < -1
                || snapshot.ActorOwnerEventIndex >= _recipe.Events.Count
                || snapshot.PendingEventIndex < -1
                || snapshot.PendingEventIndex >= _recipe.Events.Count
                || snapshot.LastTriggeredEventIndex < -1
                || snapshot.LastTriggeredEventIndex >= _recipe.Events.Count)
                return false;
            State = snapshot.State;
            _currentTimelineIndex = snapshot.CurrentTimelineIndex;
            _cardStartedFrame = snapshot.CardStartedFrame;
            _timelineEnteredFrame = snapshot.TimelineEnteredFrame;
            _lastEvaluatedFrame = snapshot.LastEvaluatedFrame;
            _localPlayheadFrame = Math.Max(0, snapshot.LocalPlayheadFrame);
            _lastSampledPlayheadFrame = snapshot.LastSampledPlayheadFrame;
            _timelinePassGeneration = Math.Max(0, snapshot.TimelinePassGeneration);
            _activeWaitEventIndex = snapshot.ActiveWaitEventIndex;
            _pendingPlayheadTargetFrame = snapshot.PendingPlayheadTargetFrame;
            _pendingTimeControlEventIndex = snapshot.PendingTimeControlEventIndex;
            _waitingForInstantOwnerAtTimelineEnd = snapshot.WaitingForInstantOwnerAtTimelineEnd;
            _actorOwnerEventIndex = snapshot.ActorOwnerEventIndex;
            _actorOwnerActionInstanceId = snapshot.OwnedPrimaryActionInstanceId;
            _pendingEventIndex = snapshot.PendingEventIndex;
            _pendingTargetTimelineIndex = snapshot.PendingTimelineIndex;
            _pendingOperation = (CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation)snapshot.PendingV6Operation;
            _lastTriggeredEventIndex = snapshot.LastTriggeredEventIndex;
            _lastTriggeredEventId = _lastTriggeredEventIndex < 0
                ? string.Empty
                : _recipe.Events[_lastTriggeredEventIndex].EventId;
            Array.Copy(snapshot.EventExecutionCounts, _executionCounts, _executionCounts.Length);
            Array.Copy(snapshot.EventLastExecutionFrames, _lastExecutionFrames, _lastExecutionFrames.Length);
            Array.Copy(snapshot.LoopRepeatCounts, _loopRepeatCounts, _loopRepeatCounts.Length);
            for (int i = 0; i < _results.Length; i++)
            {
                _results[i] = (CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult)snapshot.EventResults[i];
                _previousResults[i] = (CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult)snapshot.PreviousEventResults[i];
            }
            return Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation), _pendingOperation);
        }

        private bool IsEligible(CharacterTestRuntimeAiLabCompiledRecipeV6Event evt, int frame, int localFrame)
        {
            int clock = evt.ClockOrigin == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe
                ? Math.Max(0, frame - _cardStartedFrame)
                : localFrame;
            bool sampleOnce = evt.IsInstant
                || evt.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction;
            if (evt.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
            {
                // Only an actual position change creates an entry, even after loop rewind.
                return clock == evt.StartFrame
                    && (evt.ClockOrigin != CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline
                        || localFrame != _lastSampledPlayheadFrame)
                    && Evaluate(evt.Condition);
            }
            if (sampleOnce ? clock != evt.StartFrame : clock < evt.StartFrame || clock >= evt.EndFrame)
                return false;
            if (evt.MaxExecutionsPerPass > 0 && _executionCounts[evt.Index] >= evt.MaxExecutionsPerPass)
                return false;
            if (_lastExecutionFrames[evt.Index] != int.MinValue
                && frame - _lastExecutionFrames[evt.Index] < evt.CooldownFrames)
                return false;
            return Evaluate(evt.Condition);
        }

        private bool Evaluate(CharacterTestRuntimeAiLabCompiledRecipeV6Condition node)
        {
            if (node == null)
                return true;
            switch (node.Kind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate:
                    return node.Predicate.HasValue && _runtime.Evaluate(node.Predicate.Value);
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal:
                    return node.SourceEventIndex >= 0
                        && node.SourceEventIndex < _results.Length
                        && Matches(_results[node.SourceEventIndex], node.Signal);
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not:
                    return node.Children.Count == 1 && !Evaluate(node.Children[0]);
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any:
                    for (int i = 0; i < node.Children.Count; i++) if (Evaluate(node.Children[i])) return true;
                    return false;
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All:
                    for (int i = 0; i < node.Children.Count; i++) if (!Evaluate(node.Children[i])) return false;
                    return node.Children.Count > 0;
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope:
                    return node.Children.Count == 1
                        && CharacterTestRuntimeAiLabBehaviorRecipeSubjectAvailability.IsAvailable(
                            node.Subject,
                            _runtime)
                        && Evaluate(node.Children[0]);
                default:
                    return false;
            }
        }

        private bool StartActorEvent(CharacterTestRuntimeAiLabCompiledRecipeV6Event evt, int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            RecordAttempt(evt, frame);
            if (!_runtime.TryEnterStage(evt.ActionStage))
            {
                _results[evt.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected;
                AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.ActionStartRejected, evt);
                if (evt.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                    BeginTimelineCompletion(frame, events);
                return false;
            }
            _actorOwnerEventIndex = evt.Index;
            _actorOwnerActionInstanceId = _orchestration?.PrimaryActionInstanceId ?? 0L;
            _results[evt.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Running;
            _lastTriggeredEventIndex = evt.Index;
            _lastTriggeredEventId = evt.EventId;
            AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered, evt);
            return true;
        }

        private void BeginReplaceOwner(CharacterTestRuntimeAiLabCompiledRecipeV6Event evt, int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            if (_orchestration == null || !_orchestration.TryBeginTimelineJump(evt.ActionStage))
            {
                RecordAttempt(evt, frame);
                _results[evt.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected;
                return;
            }
            RecordAttempt(evt, frame);
            _pendingEventIndex = evt.Index;
            _pendingOperation = CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.StartActorEvent;
            _lastTriggeredEventIndex = evt.Index;
            _lastTriggeredEventId = evt.EventId;
            State = CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning;
            AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.TransitionPending, evt);
        }

        private void BeginCancelOwner(CharacterTestRuntimeAiLabCompiledRecipeV6Event owner, int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            BeginCancelOwner(owner, frame, events, CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.CancelActorEvent, -1);
        }

        private void BeginCancelOwner(
            CharacterTestRuntimeAiLabCompiledRecipeV6Event owner,
            int frame,
            CharacterTestRuntimeAiLabLogicEventBuffer events,
            CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation completion,
            int playheadTargetFrame,
            int timeControlEventIndex = -1)
        {
            if (_orchestration == null || !_orchestration.TryBeginTimelineJump(_recipe.Timelines[_currentTimelineIndex].HeaderStage))
            {
                _results[owner.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled;
                _actorOwnerEventIndex = -1;
                _actorOwnerActionInstanceId = 0L;
                if (completion == CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.RewindLoop)
                    CommitLoopRewind(timeControlEventIndex, playheadTargetFrame);
                return;
            }
            _pendingEventIndex = owner.Index;
            _pendingOperation = completion;
            _pendingPlayheadTargetFrame = playheadTargetFrame;
            _pendingTimeControlEventIndex = timeControlEventIndex;
            State = CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning;
            AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.ActionInterruptRequested, owner);
        }

        private void BeginTimelineCompletion(int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            if (_actorOwnerEventIndex >= 0)
                _results[_actorOwnerEventIndex] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled;
            if (_orchestration == null || !_orchestration.TryBeginReturnToPlanner())
            {
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Completed;
                _actorOwnerEventIndex = -1;
                _actorOwnerActionInstanceId = 0L;
                return;
            }
            _pendingEventIndex = -1;
            _pendingTargetTimelineIndex = -1;
            _pendingOperation = CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.CompleteTimeline;
            State = CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning;
            if (events != null)
            {
                string timelineId = _recipe.Timelines[_currentTimelineIndex].TimelineId;
                events.Add(new CharacterTestRuntimeAiLabLogicEvent(
                    frame,
                    CharacterTestRuntimeAiLabLogicEventKind.TransitionPending,
                    timelineId,
                    string.Empty,
                    "TimelineCompleted",
                    localFrame: LocalFrame));
            }
        }

        private void BeginTimelineControl(CharacterTestRuntimeAiLabCompiledRecipeV6Event evt, int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            RecordAttempt(evt, frame);
            if (_actorOwnerEventIndex >= 0)
                _results[_actorOwnerEventIndex] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled;
            bool began;
            if (evt.OperationKind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner)
            {
                began = _orchestration != null && _orchestration.TryBeginReturnToPlanner();
                _pendingOperation = CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.ReturnToPlanner;
            }
            else if (!Evaluate(_recipe.Timelines[evt.TargetTimelineIndex].EntryCondition))
            {
                began = _orchestration != null && _orchestration.TryBeginReturnToPlanner();
                _pendingOperation = CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.ReturnToPlanner;
                _results[evt.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected;
            }
            else
            {
                began = _orchestration != null && _orchestration.TryBeginTimelineJump(_recipe.Timelines[evt.TargetTimelineIndex].HeaderStage);
                _pendingOperation = CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.EnterTimeline;
                _pendingTargetTimelineIndex = evt.TargetTimelineIndex;
            }
            if (!began)
            {
                _pendingOperation = CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.None;
                _results[evt.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected;
                return;
            }
            _pendingEventIndex = evt.Index;
            State = CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning;
            _lastTriggeredEventIndex = evt.Index;
            _lastTriggeredEventId = evt.EventId;
            AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.TransitionPending, evt);
        }

        private void AdvancePending(int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            if (_orchestration == null)
            {
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Failed;
                return;
            }
            if (_orchestration.OrchestrationState == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending)
                return;
            CharacterTestRuntimeAiLabCompiledRecipeV6Event evt = _pendingEventIndex >= 0 ? _recipe.Events[_pendingEventIndex] : null;
            if (_orchestration.OrchestrationState == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Rejected)
            {
                if (evt != null) _results[evt.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected;
                ClearPending();
                _orchestration.CompleteOrchestration();
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Failed;
                return;
            }
            if (_orchestration.OrchestrationState != CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded)
                return;

            CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation completed = _pendingOperation;
            int target = _pendingTargetTimelineIndex;
            int playheadTarget = _pendingPlayheadTargetFrame;
            int timeControlEventIndex = _pendingTimeControlEventIndex;
            ClearPending();
            if (completed == CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.CompleteTimeline
                && evt != null)
            {
                _results[evt.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled;
                _actorOwnerEventIndex = -1;
                _actorOwnerActionInstanceId = 0L;
                _runtime.TryEnterStage(_recipe.Timelines[_currentTimelineIndex].HeaderStage);
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Running;
            }
            else if (completed == CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.ReturnToPlanner
                || completed == CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.CompleteTimeline)
            {
                if (evt != null && _results[evt.Index] != CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected)
                {
                    _results[evt.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed;
                    AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted, evt);
                }
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Completed;
                _actorOwnerEventIndex = -1;
                _actorOwnerActionInstanceId = 0L;
            }
            else if (completed == CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.EnterTimeline)
            {
                if (evt != null)
                {
                    _results[evt.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed;
                    AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted, evt);
                }
                EnterTimeline(target, frame, events);
            }
            else if (completed == CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.StartActorEvent)
            {
                if (_actorOwnerEventIndex >= 0)
                {
                    CharacterTestRuntimeAiLabCompiledRecipeV6Event previousOwner =
                        _recipe.Events[_actorOwnerEventIndex];
                    bool actionClipOverwrite = evt != null
                        && previousOwner.OperationKind ==
                            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction
                        && evt.OperationKind ==
                            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction;
                    _results[_actorOwnerEventIndex] = actionClipOverwrite
                        ? CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed
                        : CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled;
                }
                _actorOwnerActionInstanceId = 0L;
                if (evt == null || !_runtime.TryEnterStage(evt.ActionStage))
                {
                    if (evt != null) _results[evt.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected;
                    State = CharacterTestRuntimeAiLabLogicRuntimeState.Failed;
                }
                else
                {
                    _actorOwnerEventIndex = evt.Index;
                    _actorOwnerActionInstanceId = _orchestration.PrimaryActionInstanceId;
                    _results[evt.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Running;
                    State = CharacterTestRuntimeAiLabLogicRuntimeState.Running;
                }
            }
            else if (completed == CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.CancelActorEvent)
            {
                if (evt != null) _results[evt.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled;
                _actorOwnerEventIndex = -1;
                _actorOwnerActionInstanceId = 0L;
                _runtime.TryEnterStage(_recipe.Timelines[_currentTimelineIndex].HeaderStage);
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Running;
            }
            else if (completed == CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.RewindLoop)
            {
                if (evt != null) _results[evt.Index] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled;
                _actorOwnerEventIndex = -1;
                _actorOwnerActionInstanceId = 0L;
                _runtime.TryEnterStage(_recipe.Timelines[_currentTimelineIndex].HeaderStage);
                CommitLoopRewind(timeControlEventIndex, playheadTarget);
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Running;
            }
            _orchestration.CompleteOrchestration();
        }

        private bool EnterTimeline(int timelineIndex, int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            if (timelineIndex < 0 || timelineIndex >= _recipe.Timelines.Count)
                return false;
            CharacterTestRuntimeAiLabCompiledRecipeV6Timeline timeline = _recipe.Timelines[timelineIndex];
            if (!_runtime.TryEnterStage(timeline.HeaderStage))
            {
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Failed;
                return false;
            }
            _currentTimelineIndex = timelineIndex;
            _timelineEnteredFrame = frame + 1;
            _lastEvaluatedFrame = frame;
            _localPlayheadFrame = 0;
            _lastSampledPlayheadFrame = -1;
            _timelinePassGeneration = 0;
            _activeWaitEventIndex = -1;
            _pendingPlayheadTargetFrame = -1;
            _pendingTimeControlEventIndex = -1;
            _waitingForInstantOwnerAtTimelineEnd = false;
            _actorOwnerEventIndex = -1;
            _actorOwnerActionInstanceId = 0L;
            for (int i = 0; i < _recipe.Events.Count; i++)
            {
                if (_recipe.Events[i].TimelineIndex != timelineIndex || _recipe.Events[i].IsLegacyShared)
                    continue;
                _executionCounts[i] = 0;
                _lastExecutionFrames[i] = int.MinValue;
                _results[i] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.None;
                _previousResults[i] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.None;
                _loopRepeatCounts[i] = 0;
            }
            State = CharacterTestRuntimeAiLabLogicRuntimeState.Running;
            if (events != null)
                events.Add(new CharacterTestRuntimeAiLabLogicEvent(frame, CharacterTestRuntimeAiLabLogicEventKind.StageEntered, timeline.TimelineId, string.Empty, string.Empty, targetTimelineId: timeline.TimelineId, localFrame: 0));
            return true;
        }

        private void AdvancePlayhead(
            CharacterTestRuntimeAiLabCompiledRecipeV6Timeline timeline,
            int frame,
            CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            _activeWaitEventIndex = -1;
            for (int i = 0; i < timeline.Events.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledRecipeV6Event timeControl = timeline.Events[i];
                if (!timeControl.IsWaitControl
                    || timeControl.ClockOrigin != CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline
                    || timeControl.StartFrame != _localPlayheadFrame)
                    continue;
                if (timeControl.Condition == null || !Evaluate(timeControl.Condition))
                {
                    _activeWaitEventIndex = timeControl.Index;
                    return;
                }
            }

            for (int i = 0; i < timeline.Events.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledRecipeV6Event loop = timeline.Events[i];
                if (!loop.IsLoopControl
                    || loop.ClockOrigin != CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline
                    || _localPlayheadFrame != loop.EndFrame - 1)
                    continue;
                bool exit = loop.Condition != null && Evaluate(loop.Condition);
                bool repeatAvailable = loop.MaxLoopRepeats == 0
                    || _loopRepeatCounts[loop.Index] < loop.MaxLoopRepeats;
                if (exit || !repeatAvailable)
                    break;

                CommitLoopRewind(loop.Index, loop.StartFrame);
                return;
            }

            _localPlayheadFrame++;
        }

        private void CommitLoopRewind(int loopEventIndex, int targetFrame)
        {
            if (loopEventIndex < 0 || loopEventIndex >= _recipe.Events.Count)
                return;
            CharacterTestRuntimeAiLabCompiledRecipeV6Event loop = _recipe.Events[loopEventIndex];
            _loopRepeatCounts[loopEventIndex]++;
            _timelinePassGeneration++;
            _activeWaitEventIndex = -1;
            for (int i = 0; i < _recipe.Events.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledRecipeV6Event evt = _recipe.Events[i];
                if (evt.TimelineIndex != _currentTimelineIndex
                    || evt.IsLegacyShared
                    || evt.StartFrame < loop.StartFrame
                    || evt.EndFrame > loop.EndFrame)
                    continue;
                _executionCounts[i] = 0;
                _results[i] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.None;
                _previousResults[i] = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.None;
            }
            _localPlayheadFrame = Math.Max(0, targetFrame);
        }

        private void RefreshOwnedOperationResult(int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            if (_actorOwnerEventIndex < 0 || _pendingOperation != CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.None)
                return;
            CharacterTestRuntimeAiLabCompiledRecipeV6Event owner = _recipe.Events[_actorOwnerEventIndex];
            if (_actorOwnerActionInstanceId > 0L
                && _orchestration?.PrimaryActionInstanceId != _actorOwnerActionInstanceId)
            {
                return;
            }
            CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult result = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Running;
            if (_orchestration?.PrimaryActionRejected == true)
                result = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected;
            else if (_runtime.PrimaryActionState == CharacterTestRuntimeAiLabLogicActionState.Completed)
                result = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed;
            else if (_runtime.PrimaryActionState == CharacterTestRuntimeAiLabLogicActionState.Failed)
                result = CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Failed;
            if (result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Running)
                return;
            _results[owner.Index] = result;
            _actorOwnerEventIndex = -1;
            _actorOwnerActionInstanceId = 0L;
            AddEvent(events, frame, result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected ? CharacterTestRuntimeAiLabLogicEventKind.ActionStartRejected : CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted, owner);
            // Publish the confirmed result before sampling configured exits. An operation ending
            // does not implicitly exit its timeline or bypass same-frame result consumers.
        }

        private void RecordAttempt(CharacterTestRuntimeAiLabCompiledRecipeV6Event evt, int frame)
        {
            _executionCounts[evt.Index]++;
            _lastExecutionFrames[evt.Index] = frame;
        }

        private void ClearPending()
        {
            _pendingEventIndex = -1;
            _pendingTargetTimelineIndex = -1;
            _pendingOperation = CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation.None;
            _pendingPlayheadTargetFrame = -1;
            _pendingTimeControlEventIndex = -1;
        }

        private void ResetArrays()
        {
            Array.Clear(_executionCounts, 0, _executionCounts.Length);
            Array.Clear(_results, 0, _results.Length);
            Array.Clear(_previousResults, 0, _previousResults.Length);
            Array.Clear(_loopRepeatCounts, 0, _loopRepeatCounts.Length);
            for (int i = 0; i < _lastExecutionFrames.Length; i++) _lastExecutionFrames[i] = int.MinValue;
        }

        private static bool Matches(CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult result, CharacterTestRuntimeAiLabBehaviorRecipeEventSignal signal)
        {
            switch (signal)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Completed: return result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed;
                case CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Failed: return result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Failed;
                case CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Rejected: return result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected;
                case CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Cancelled: return result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled;
                default: return false;
            }
        }

        private static int Compare(CharacterTestRuntimeAiLabCompiledRecipeV6Event left, CharacterTestRuntimeAiLabCompiledRecipeV6Event right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : string.CompareOrdinal(left.EventId, right.EventId);
        }

        private void AddEvent(CharacterTestRuntimeAiLabLogicEventBuffer events, int frame, CharacterTestRuntimeAiLabLogicEventKind kind, CharacterTestRuntimeAiLabCompiledRecipeV6Event evt)
        {
            if (events == null || evt == null) return;
            string timelineId = _currentTimelineIndex >= 0 ? _recipe.Timelines[_currentTimelineIndex].TimelineId : string.Empty;
            string target = evt.TargetTimelineIndex >= 0 ? _recipe.Timelines[evt.TargetTimelineIndex].TimelineId : string.Empty;
            events.Add(new CharacterTestRuntimeAiLabLogicEvent(frame, kind, timelineId, evt.EventId, string.Empty, targetTimelineId: target, localFrame: LocalFrame, primaryActionInstanceId: OwnedPrimaryActionInstanceId));
        }
    }
}

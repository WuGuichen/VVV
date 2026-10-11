using System;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal interface ICharacterTestBehaviorExecutionRuntime
    {
        int EntityId { get; }
        bool Evaluate(in CharacterTestBehaviorExecutionPredicate predicate);
        bool IsSubjectAvailable(CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject);
        bool TrySampleMovement(in CharacterTestBehaviorExecutionMovement movement, out CharacterTestRuntimeAiLabMovementTargetSample sample);
        bool TrySampleTurn(in CharacterTestBehaviorExecutionTurn turn, out int targetXMillimeters, out int targetZMillimeters);
        bool TryStartAction(int actionId);
        bool TryStopAction(long ownedInstanceId);
        CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState ActionCommandState { get; }
        CharacterTestRuntimeAiLabLogicActionState ActualActionState { get; }
        long ActualActionInstanceId { get; }
        long StartedActionInstanceId { get; }
        void CompleteActionCommand();
        void ObserveOwnedAction(long instanceId);
        void SetExecutionControl(Vector2 move, float movementSpeed, in CharacterTestBehaviorControlInput control);
    }
    internal interface ICharacterTestBehaviorExecutionContinuationRuntime
    {
        int ActionCommitFailureReasonId { get; }
        int ActionCommitFailureDetailId { get; }
        CharacterTestActionUseFailure ActionCommitFailureDetails { get; }
        void AttachExecutionEvaluator(CharacterTestBehaviorExecutionEvaluator evaluator);
    }
    internal interface ICharacterTestBehaviorExecutionActionClock
    { int ActualActionFrame { get; } }

    /// <summary>Per-instance authority state; no source strings, definitions or service objects.</summary>
    public sealed class CharacterTestBehaviorExecutionState
    {
        public const int CurrentVersion = 6;
        internal const int LegacyVersion = 5;
        internal const int BehindTargetVersion = 7;
        public int Version { get; set; } = CurrentVersion;
        public int ProgramFormat { get; set; } = CharacterTestBehaviorExecutionProgram.CurrentFormatVersion;
        public long ProgramIdentity { get; set; }
        public int EntityId { get; set; }
        public int State { get; set; }
        public int Timeline { get; set; } = -1;
        public int CardStartedFrame { get; set; } = -1;
        public int TimelineEnteredFrame { get; set; } = -1;
        public int LocalFrame { get; set; }
        public int LastSampledFrame { get; set; } = -1;
        public int LastWorldFrame { get; set; } = -1;
        public int PassGeneration { get; set; }
        public int WaitEvent { get; set; } = -1;
        public int AttackWaitBlock { get; set; } = -1;
        public int AttackWaitTargetId { get; set; }
        public long AttackWaitInstanceId { get; set; }
        public int[] AttackWaitWindowIds { get; set; } = Array.Empty<int>();
        public bool ShouldSerializeAttackWaitBlock() => Version >= CurrentVersion;
        public bool ShouldSerializeAttackWaitTargetId() => Version >= CurrentVersion;
        public bool ShouldSerializeAttackWaitInstanceId() => Version >= CurrentVersion;
        public bool ShouldSerializeAttackWaitWindowIds() => Version >= CurrentVersion;
        public int MovementOwner { get; set; } = -1;
        public int TurnOwner { get; set; } = -1;
        public int ActionOwner { get; set; } = -1;
        public long ActionInstanceId { get; set; }
        public int PendingAction { get; set; } = -1;
        public bool StopPending { get; set; }
        public int JumpTarget { get; set; } = -1;
        public int FlowEvent { get; set; } = -1;
        public bool FinishRequested { get; set; }
        public bool FailureRequested { get; set; }
        public int LastTriggeredEvent { get; set; } = -1;
        public int[] Counts { get; set; } = Array.Empty<int>();
        public int[] LastExecutions { get; set; } = Array.Empty<int>();
        public int[] Results { get; set; } = Array.Empty<int>();
        public int[] CommitFailureReasons { get; set; } = Array.Empty<int>();
        public CharacterTestActionUseFailure[] CommitFailureDetails { get; set; } = Array.Empty<CharacterTestActionUseFailure>();
        public int[] CommitFailureDetailIds { get; set; } = Array.Empty<int>();
        public int[] LoopRepeats { get; set; } = Array.Empty<int>();
        public int[] Generations { get; set; } = Array.Empty<int>();
        public bool[] ActiveControls { get; set; } = Array.Empty<bool>();
        public bool[] Stopped { get; set; } = Array.Empty<bool>();
        public int[] RearOrbitSides { get; set; } = Array.Empty<int>();
        public bool ShouldSerializeRearOrbitSides() => Version == BehindTargetVersion;
        public int[] RearTargetIds { get; set; } = Array.Empty<int>();
        public bool ShouldSerializeRearTargetIds() => Version == BehindTargetVersion;
        public int[] BlockStarts { get; set; } = Array.Empty<int>();
        public int[] BlockResults { get; set; } = Array.Empty<int>();
        public int EndReason { get; set; }
        public int StopStartedFrame { get; set; } = -1;
        internal void CopyTo(CharacterTestBehaviorExecutionState target)
        {
            target.Version = Version;
            target.ProgramFormat = ProgramFormat;
            target.ProgramIdentity = ProgramIdentity;
            target.EntityId = EntityId;
            target.State = State;
            target.Timeline = Timeline;
            target.CardStartedFrame = CardStartedFrame;
            target.TimelineEnteredFrame = TimelineEnteredFrame;
            target.LocalFrame = LocalFrame;
            target.LastSampledFrame = LastSampledFrame;
            target.LastWorldFrame = LastWorldFrame;
            target.PassGeneration = PassGeneration;
            target.WaitEvent = WaitEvent;
            target.AttackWaitBlock = AttackWaitBlock; target.AttackWaitTargetId = AttackWaitTargetId;
            target.AttackWaitInstanceId = AttackWaitInstanceId;
            if (target.AttackWaitWindowIds?.Length != AttackWaitWindowIds.Length)
                target.AttackWaitWindowIds = new int[AttackWaitWindowIds.Length];
            Array.Copy(AttackWaitWindowIds, target.AttackWaitWindowIds, AttackWaitWindowIds.Length);
            target.MovementOwner = MovementOwner;
            target.TurnOwner = TurnOwner;
            target.ActionOwner = ActionOwner;
            target.ActionInstanceId = ActionInstanceId;
            target.PendingAction = PendingAction;
            target.StopPending = StopPending;
            target.JumpTarget = JumpTarget;
            target.FlowEvent = FlowEvent;
            target.FinishRequested = FinishRequested;
            target.FailureRequested = FailureRequested;
            target.LastTriggeredEvent = LastTriggeredEvent;
            target.EndReason = EndReason; target.StopStartedFrame = StopStartedFrame;
            Array.Copy(BlockStarts, target.BlockStarts, BlockStarts.Length);
            Array.Copy(BlockResults, target.BlockResults, BlockResults.Length);
            Array.Copy(Counts, target.Counts, Counts.Length);
            Array.Copy(LastExecutions, target.LastExecutions, LastExecutions.Length);
            Array.Copy(Results, target.Results, Results.Length);
            Array.Copy(CommitFailureReasons, target.CommitFailureReasons, CommitFailureReasons.Length);
            Array.Copy(CommitFailureDetails, target.CommitFailureDetails, CommitFailureDetails.Length);
            Array.Copy(CommitFailureDetailIds, target.CommitFailureDetailIds, CommitFailureDetailIds.Length);
            Array.Copy(LoopRepeats, target.LoopRepeats, LoopRepeats.Length);
            Array.Copy(Generations, target.Generations, Generations.Length);
            Array.Copy(ActiveControls, target.ActiveControls, ActiveControls.Length);
            Array.Copy(Stopped, target.Stopped, Stopped.Length);
            if (target.RearOrbitSides?.Length != RearOrbitSides.Length) target.RearOrbitSides = new int[RearOrbitSides.Length];
            Array.Copy(RearOrbitSides, target.RearOrbitSides, RearOrbitSides.Length);
            if (target.RearTargetIds?.Length != RearTargetIds.Length) target.RearTargetIds = new int[RearTargetIds.Length];
            Array.Copy(RearTargetIds, target.RearTargetIds, RearTargetIds.Length);
        }

        public CharacterTestBehaviorExecutionState Clone()
        {
            var result = (CharacterTestBehaviorExecutionState)MemberwiseClone();
            result.Counts = Copy(Counts); result.LastExecutions = Copy(LastExecutions); result.Results = Copy(Results);
            result.CommitFailureReasons = Copy(CommitFailureReasons);
            result.CommitFailureDetails = Copy(CommitFailureDetails);
            result.CommitFailureDetailIds = Copy(CommitFailureDetailIds);
            result.LoopRepeats = Copy(LoopRepeats); result.Generations = Copy(Generations);
            result.ActiveControls = Copy(ActiveControls); result.Stopped = Copy(Stopped); result.RearOrbitSides = Copy(RearOrbitSides);
            result.RearTargetIds = Copy(RearTargetIds);
            result.BlockStarts = Copy(BlockStarts); result.BlockResults = Copy(BlockResults);
            result.AttackWaitWindowIds = Copy(AttackWaitWindowIds);
            return result;
        }
        private static T[] Copy<T>(T[] values) => values == null ? Array.Empty<T>() : (T[])values.Clone();
        public void AddHash(RuntimeHashAccumulator hash, int key = 1)
        {
            hash.BeginObject(key, BehaviorExecutionStateHashFields.SchemaId, Version == BehindTargetVersion ? 5 : Version == LegacyVersion ? 3 : BehaviorExecutionStateHashFields.SchemaVersion);
            hash.AddInt(BehaviorExecutionStateHashFields.ExecutionStateVersion, Version); hash.AddInt(BehaviorExecutionStateHashFields.ExecutionStateFormat, ProgramFormat);
            hash.AddLong(BehaviorExecutionStateHashFields.ExecutionStateIdentity, ProgramIdentity); hash.AddInt(BehaviorExecutionStateHashFields.State, State); hash.AddInt(BehaviorExecutionStateHashFields.Timeline, Timeline);
            hash.AddInt(BehaviorExecutionStateHashFields.ExecutionStateEntity, EntityId);
            hash.AddInt(BehaviorExecutionStateHashFields.CardStart, CardStartedFrame); hash.AddInt(BehaviorExecutionStateHashFields.TimelineStart, TimelineEnteredFrame);
            hash.AddInt(BehaviorExecutionStateHashFields.Local, LocalFrame); hash.AddInt(BehaviorExecutionStateHashFields.LastSample, LastSampledFrame); hash.AddInt(BehaviorExecutionStateHashFields.LastWorld, LastWorldFrame);
            hash.AddInt(BehaviorExecutionStateHashFields.Pass, PassGeneration); hash.AddInt(BehaviorExecutionStateHashFields.Wait, WaitEvent); hash.AddInt(BehaviorExecutionStateHashFields.MoveOwner, MovementOwner);
            hash.AddInt(BehaviorExecutionStateHashFields.TurnOwner, TurnOwner); hash.AddInt(BehaviorExecutionStateHashFields.ActionOwner, ActionOwner); hash.AddLong(BehaviorExecutionStateHashFields.Instance, ActionInstanceId);
            hash.AddInt(BehaviorExecutionStateHashFields.PendingAction, PendingAction); hash.AddInt(BehaviorExecutionStateHashFields.StopPending, StopPending ? 1 : 0);
            hash.AddInt(BehaviorExecutionStateHashFields.Jump, JumpTarget); hash.AddInt(BehaviorExecutionStateHashFields.Flow, FlowEvent); hash.AddInt(BehaviorExecutionStateHashFields.Finish, FinishRequested ? 1 : 0);
            hash.AddInt(BehaviorExecutionStateHashFields.Failure, FailureRequested ? 1 : 0); hash.AddInt(BehaviorExecutionStateHashFields.LastEvent, LastTriggeredEvent);
            AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.Count, Counts, BehaviorExecutionStateHashFields.CountValue, BehaviorExecutionStateHashFields.CountPresent); AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.LastExecution, LastExecutions, BehaviorExecutionStateHashFields.LastExecutionValue, BehaviorExecutionStateHashFields.LastExecutionPresent); AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.Result, Results, BehaviorExecutionStateHashFields.ResultValue, BehaviorExecutionStateHashFields.ResultPresent);
            AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.CommitFailure, CommitFailureReasons, BehaviorExecutionStateHashFields.CommitFailureValue, BehaviorExecutionStateHashFields.CommitFailurePresent);
            hash.AddBool(BehaviorExecutionStateHashFields.CommitFailureDetailsPresent, CommitFailureDetails != null);
            hash.BeginArray(BehaviorExecutionStateHashFields.CommitFailureDetails, CommitFailureDetails?.Length ?? 0);
            for (int i = 0; CommitFailureDetails != null && i < CommitFailureDetails.Length; i++)
            {
                hash.BeginElement(i);
                CommitFailureDetails[i].ContributeHash(hash, BehaviorExecutionStateHashFields.CommitFailureDetailValue);
                hash.EndElement();
            }
            hash.EndArray();
            AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.CommitFailureDetailIds, CommitFailureDetailIds, BehaviorExecutionStateHashFields.CommitFailureDetailIdsValue, BehaviorExecutionStateHashFields.CommitFailureDetailIdsPresent);
            AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.Repeat, LoopRepeats, BehaviorExecutionStateHashFields.RepeatValue, BehaviorExecutionStateHashFields.RepeatPresent); AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.Generation, Generations, BehaviorExecutionStateHashFields.GenerationValue, BehaviorExecutionStateHashFields.GenerationPresent);
            AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.Active, ActiveControls, BehaviorExecutionStateHashFields.ActiveValue, BehaviorExecutionStateHashFields.ActivePresent);
            AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.Stopped, Stopped, BehaviorExecutionStateHashFields.StoppedValue, BehaviorExecutionStateHashFields.StoppedPresent);
            if (Version == BehindTargetVersion)
            {
                AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.RearOrbitSides, RearOrbitSides,
                    BehaviorExecutionStateHashFields.RearOrbitSideValue, BehaviorExecutionStateHashFields.RearOrbitSidesPresent);
                AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.RearTargetIds, RearTargetIds,
                    BehaviorExecutionStateHashFields.RearTargetIdValue, BehaviorExecutionStateHashFields.RearTargetIdsPresent);
            }
            AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.BlockStarts, BlockStarts, BehaviorExecutionStateHashFields.BlockStartValue, BehaviorExecutionStateHashFields.BlockStartPresent);
            AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.BlockResults, BlockResults, BehaviorExecutionStateHashFields.BlockResultValue, BehaviorExecutionStateHashFields.BlockResultPresent);
            hash.AddInt(BehaviorExecutionStateHashFields.EndReason, EndReason); hash.AddInt(BehaviorExecutionStateHashFields.StopStart, StopStartedFrame);
            if (Version >= CurrentVersion)
            {
                hash.AddInt(BehaviorExecutionStateHashFields.AttackWaitBlock, AttackWaitBlock);
                hash.AddInt(BehaviorExecutionStateHashFields.AttackWaitTarget, AttackWaitTargetId);
                hash.AddLong(BehaviorExecutionStateHashFields.AttackWaitInstance, AttackWaitInstanceId);
                AiStateHashArray.Add(hash, BehaviorExecutionStateHashFields.AttackWaitWindows, AttackWaitWindowIds,
                    BehaviorExecutionStateHashFields.AttackWaitWindowValue, BehaviorExecutionStateHashFields.AttackWaitWindowsPresent);
            }
            hash.EndObject();
        }
    }

    internal sealed partial class CharacterTestBehaviorExecutionEvaluator
    {
        private readonly CharacterTestBehaviorExecutionProgram _program;
        private readonly ICharacterTestBehaviorExecutionRuntime _runtime;
        private readonly CharacterTestBehaviorExecutionDebugMap _debug;
        private CharacterTestBehaviorExecutionState _state;
        public CharacterTestBehaviorExecutionEvaluator(CharacterTestBehaviorExecutionProgram program,
            ICharacterTestBehaviorExecutionRuntime runtime, CharacterTestBehaviorExecutionDebugMap debugMap = null)
        {
            _program = program ?? throw new ArgumentNullException(nameof(program));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime)); _debug = debugMap;
            int count = program.Events.Length;
            _state = new CharacterTestBehaviorExecutionState { Version = SnapshotVersion(program), ProgramIdentity = program.Identity, EntityId = runtime.EntityId,
                Counts = new int[count], LastExecutions = new int[count], Results = new int[count], CommitFailureReasons = new int[count], CommitFailureDetails = new CharacterTestActionUseFailure[count], CommitFailureDetailIds = new int[count], LoopRepeats = new int[count],
                Generations = new int[count], ActiveControls = new bool[count], Stopped = new bool[count],
                RearOrbitSides = SnapshotVersion(program) == CharacterTestBehaviorExecutionState.BehindTargetVersion ? new int[count] : Array.Empty<int>(),
                RearTargetIds = SnapshotVersion(program) == CharacterTestBehaviorExecutionState.BehindTargetVersion ? new int[count] : Array.Empty<int>(),
                BlockStarts = new int[program.Blocks.Length], BlockResults = new int[program.Blocks.Length] };
            Reset();
            if (runtime is ICharacterTestBehaviorExecutionContinuationRuntime continuation) continuation.AttachExecutionEvaluator(this);
        }
        public CharacterTestRuntimeAiLabLogicRuntimeState State => (CharacterTestRuntimeAiLabLogicRuntimeState)_state.State;
        public int CurrentTimelineIndex => _state.Timeline;
        internal bool NeedsPrimaryAttackFact(int frame)
        {
            if (_state.Timeline < 0)
                return _program.NeedsPrimaryAttackFact(_program.ActivationCondition)
                    || _program.NeedsPrimaryAttackFact(_program.Timelines[_program.EntryTimeline].EntryCondition);
            if (_state.JumpTarget >= 0 && _program.NeedsPrimaryAttackFact(_program.Timelines[_state.JumpTarget].EntryCondition))
                return true;
            var timeline = _program.Timelines[_state.Timeline];
            foreach (var block in _program.Blocks)
                if (block.Timeline == _state.Timeline && _state.LocalFrame >= block.Start && _state.LocalFrame < block.End
                    && _program.NeedsPrimaryAttackFact(block.SuccessCondition)) return true;
            for (int i = 0; i < timeline.EventCount; i++)
            {
                var evt = _program.Events[_program.TimelineEvents[timeline.EventStart + i]];
                if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction) continue;
                int clock = Clock(evt, frame);
                bool instant = evt.Timing == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant;
                if (instant ? clock != evt.StartFrame : !InRange(evt, frame)) continue;
                if (_program.NeedsPrimaryAttackFact(evt.Condition)) return true;
                if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline
                    && _program.NeedsPrimaryAttackFact(_program.Timelines[_program.Flows[evt.Parameter].TargetTimeline].EntryCondition))
                    return true;
            }
            return false;
        }

        public int LocalFrame => _state.LocalFrame;
        public int CardStartedFrame => _state.CardStartedFrame;
        public int TimelineEnteredFrame => _state.TimelineEnteredFrame;
        public long OwnedActionInstanceId => _state.ActionInstanceId;
        public int LastTriggeredEventIndex => _state.LastTriggeredEvent;
        public string LastTriggeredEventId => _debug?.Event(_state.LastTriggeredEvent) ?? string.Empty;
        public int LastActionConditionRejectedEvent { get; private set; } = -1;
        internal bool HasReadyCommandReceipt => (_state.PendingAction >= 0 || _state.StopPending)
            && (_runtime.ActionCommandState == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded
                || _runtime.ActionCommandState == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Rejected);
        public bool CanActivate() => Evaluate(_program.ActivationCondition) && Evaluate(_program.Timelines[_program.EntryTimeline].EntryCondition);
        internal bool CanCommitPendingAction(int actionId)
        {
            if (_state.PendingAction < 0) return true;
            var pending = _program.Events[_state.PendingAction];
            return !_state.FinishRequested && _program.Actions[pending.Parameter].ActionId == actionId;
        }
        public bool Start(int frame)
        {
            if (State != CharacterTestRuntimeAiLabLogicRuntimeState.Inactive || !CanActivate()) return false;
            _state.CardStartedFrame = frame; _state.LastWorldFrame = frame;
            EnterTimeline(_program.EntryTimeline, frame);
            return true;
        }
        public bool StartCommitted(int frame)
        {
            if (State != CharacterTestRuntimeAiLabLogicRuntimeState.Inactive || !Evaluate(_program.Timelines[_program.EntryTimeline].EntryCondition)) return false;
            _state.CardStartedFrame = frame; _state.LastWorldFrame = frame; EnterTimeline(_program.EntryTimeline, frame); return true;
        }
        public void Reset()
        {
            _state.State = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Inactive; _state.Timeline = -1;
            _state.CardStartedFrame = -1; _state.TimelineEnteredFrame = -1; _state.LocalFrame = 0;
            _state.LastSampledFrame = -1; _state.LastWorldFrame = -1; _state.PassGeneration = 0; _state.WaitEvent = -1;
            ClearAttackWait();
            _state.MovementOwner = -1; _state.TurnOwner = -1; _state.ActionOwner = -1; _state.ActionInstanceId = 0;
            _state.PendingAction = -1; _state.StopPending = false; _state.JumpTarget = -1; _state.FlowEvent = -1;
            _state.FinishRequested = false; _state.FailureRequested = false; _state.LastTriggeredEvent = -1;
            Array.Clear(_state.Counts, 0, _state.Counts.Length); Array.Clear(_state.Results, 0, _state.Results.Length);
            Array.Clear(_state.CommitFailureReasons, 0, _state.CommitFailureReasons.Length);
            Array.Clear(_state.CommitFailureDetails, 0, _state.CommitFailureDetails.Length);
            Array.Clear(_state.CommitFailureDetailIds, 0, _state.CommitFailureDetailIds.Length);
            Array.Clear(_state.LoopRepeats, 0, _state.LoopRepeats.Length); Array.Clear(_state.Generations, 0, _state.Generations.Length);
            Array.Clear(_state.ActiveControls, 0, _state.ActiveControls.Length); Array.Clear(_state.Stopped, 0, _state.Stopped.Length);
            Array.Clear(_state.RearOrbitSides, 0, _state.RearOrbitSides.Length);
            Array.Clear(_state.RearTargetIds, 0, _state.RearTargetIds.Length);
            for (int i = 0; i < _state.LastExecutions.Length; i++) _state.LastExecutions[i] = int.MinValue;
            LastActionConditionRejectedEvent = -1;
            _state.EndReason = 0; _state.StopStartedFrame = -1;
            Array.Clear(_state.BlockResults, 0, _state.BlockResults.Length);
            Array.Fill(_state.BlockStarts, -1);
            _runtime.ObserveOwnedAction(0L);
        }
        public void Stop(bool failed = false)
        {
            if (State == CharacterTestRuntimeAiLabLogicRuntimeState.Inactive) return;
            if (_state.StopStartedFrame < 0) _state.StopStartedFrame = _state.LastWorldFrame;
            if (_state.EndReason == 0) _state.EndReason = (int)(failed ? CharacterTestBehaviorExecutionEndReason.ActionRejected : CharacterTestBehaviorExecutionEndReason.Completed);
            _state.FinishRequested = true; _state.FailureRequested |= failed; _state.JumpTarget = -1;
            ClearAttackWait();
            ReleaseControls(); BeginStop();
        }
        /// <remarks>Steady sampling is NoAlloc. Entering a window wait captures its nonempty identity set once (LowFreqAlloc).
        /// Sampling never directly modifies character authority.</remarks>
        public void Tick(int frame, CharacterTestRuntimeAiLabLogicEventBuffer events = null)
        {
            events?.Clear(); LastActionConditionRejectedEvent = -1;
            if (State == CharacterTestRuntimeAiLabLogicRuntimeState.Inactive || State == CharacterTestRuntimeAiLabLogicRuntimeState.Completed
                || State == CharacterTestRuntimeAiLabLogicRuntimeState.Failed || frame <= _state.LastWorldFrame) return;
            int previousWorld = _state.LastWorldFrame; _state.LastWorldFrame = frame;
            bool pendingReady = RefreshPending(frame);
            if (State == CharacterTestRuntimeAiLabLogicRuntimeState.Completed || State == CharacterTestRuntimeAiLabLogicRuntimeState.Failed) return;
            if (CleanupBudgetExpired(frame)) return;
            if (_runtime.ActualActionState == CharacterTestRuntimeAiLabLogicActionState.Failed) { Stop(true); return; }
            if (_state.FinishRequested || _state.JumpTarget >= 0)
            { ReleaseControls(); BeginStop(); return; }
            RefreshAction();
            if (!UpdateExecutionBlocks(frame)) return;
            if (!pendingReady)
            { SampleControlsWhilePending(frame); return; }
            Sample(frame, previousWorld, events, true);
        }

        private void Sample(int frame, int previousWorld, CharacterTestRuntimeAiLabLogicEventBuffer events, bool advance)
        {
            if (!UpdateExecutionBlocks(frame)) return;
            var timeline = _program.Timelines[_state.Timeline];
            if (_state.LocalFrame >= timeline.DurationFrames) { Stop(); return; }
            CompleteExpiredControls(frame);
            int move = -1, turn = -1, action = -1, jump = -1;
            Vector2 movement = Vector2.zero; float movementSpeed = 1f; int moveLimit = -1;
            int turnX = 0, turnZ = 0; float turnSpeed = 1f;
            for (int offset = 0; offset < timeline.EventCount; offset++)
            {
                int index = _program.TimelineEvents[timeline.EventStart + offset];
                var evt = _program.Events[index];
                bool activeControl = _state.ActiveControls[index];
                if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget)
                {
                    if (move >= 0 || (!activeControl && !Eligible(index, frame, previousWorld))) continue;
                    if (!InRange(evt, frame) || !_runtime.TrySampleMovement(_program.Movements[evt.Parameter], out var sample)) continue;
                    _state.MovementOwner = SelectControl(index, _state.MovementOwner, frame);
                    move = index; movement = ResolveMovement(_program.Movements[evt.Parameter], sample, index, out moveLimit);
                    movementSpeed = _program.Movements[evt.Parameter].Speed;
                    continue;
                }
                if (evt.Kind == CharacterTestBehaviorExecutionOperations.TurnToTarget)
                {
                    if (turn >= 0 || (!activeControl && !Eligible(index, frame, previousWorld))) continue;
                    if (!InRange(evt, frame) || !_runtime.TrySampleTurn(_program.Turns[evt.Parameter], out int x, out int z)) continue;
                    _state.TurnOwner = SelectControl(index, _state.TurnOwner, frame); turn = index; turnX = x; turnZ = z; turnSpeed = _program.Turns[evt.Parameter].Speed;
                    continue;
                }
                if (!Eligible(index, frame, previousWorld)) continue;
                if (evt.Kind == CharacterTestBehaviorExecutionOperations.CompleteBlock)
                {
                    Record(index, frame); SetResult(index, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed);
                    for (int b = 0; b < _program.Blocks.Length; b++)
                    {
                        var block = _program.Blocks[b];
                        if (block.Timeline != _state.Timeline || block.End != evt.Parameter || block.Start > evt.StartFrame
                            || block.HasAction || block.Completion != CharacterTestBehaviorBlockCompletion.Timed) continue;
                        CompleteExecutionBlock(b, block); break;
                    }
                    _state.LastTriggeredEvent = index;
                    Emit(events, frame, index, CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted);
                    return;
                }
                if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner)
                {
                    Record(index, frame); SetResult(index, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed);
                    _state.LastTriggeredEvent = index; _state.LastSampledFrame = _state.LocalFrame;
                    Emit(events, frame, index, CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted); Stop(); return;
                }
                if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline)
                { if (jump < 0) jump = index; continue; }
                if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction) action = index;
            }
            _state.LastSampledFrame = _state.LocalFrame;
            if (jump >= 0)
            {
                Record(jump, frame); _state.FlowEvent = jump; _state.LastTriggeredEvent = jump;
                int target = _program.Flows[_program.Events[jump].Parameter].TargetTimeline;
                if (!Evaluate(_program.Timelines[target].EntryCondition)) { SetResult(jump, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected); Stop(); }
                else { _state.JumpTarget = target; ReleaseControls(); BeginStop(); }
                return;
            }
            PublishControl(move, turn, movement, movementSpeed, moveLimit, turnX, turnZ, turnSpeed);
            if (action >= 0)
            {
                Record(action, frame); _state.LastTriggeredEvent = action;
                if (!_runtime.TryStartAction(_program.Actions[_program.Events[action].Parameter].ActionId))
                { SetCommitFailed(action); if (advance) Advance(timeline, frame); return; }
                _state.PendingAction = action; _state.State = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning;
                Emit(events, frame, action, CharacterTestRuntimeAiLabLogicEventKind.TransitionPending);
            }
            else if (!IsPassiveWaitActive && _state.ActionOwner >= 0 && !IsCompletionAction(_state.ActionOwner) && !InRange(_program.Events[_state.ActionOwner], frame)) BeginStop();
            if (advance) Advance(timeline, frame);
        }

        /// <summary>Consumes a command receipt without advancing the world and samples only its configured continuation.</summary>
        internal void ContinueAfterActionCommit(int frame)
        {
            if (_state.LastWorldFrame != frame || (_state.PendingAction < 0 && !_state.StopPending)) return;
            bool failed = _state.PendingAction >= 0 && _runtime.ActionCommandState == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Rejected;
            int beforeTimeline = _state.Timeline, nextCursor = _state.LocalFrame;
            if (!RefreshPending(frame) && _state.Timeline == beforeTimeline) return;
            if (State != CharacterTestRuntimeAiLabLogicRuntimeState.Running) return;
            if (!failed && _state.Timeline == beforeTimeline) return;
            if (failed && _state.Timeline == beforeTimeline) _state.LocalFrame = _state.LastSampledFrame;
            for (int transitions = 0; transitions <= _program.Timelines.Length; transitions++)
            {
                int sampledTimeline = _state.Timeline;
                bool entered = sampledTimeline != beforeTimeline;
                if (entered) _state.TimelineEnteredFrame = frame;
                Sample(frame, frame, null, entered);
                if (_state.Timeline == sampledTimeline)
                {
                    if (!entered && _state.PendingAction < 0 && !_state.StopPending) _state.LocalFrame = nextCursor;
                    return;
                }
                if (State != CharacterTestRuntimeAiLabLogicRuntimeState.Running) return;
            }
            Stop(true);
        }

        private void SetCommitFailed(int index)
        {
            SetResult(index, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.CommitFailed);
            _state.CommitFailureReasons[index] = (_runtime as ICharacterTestBehaviorExecutionContinuationRuntime)?.ActionCommitFailureReasonId ?? (int)CharacterTestBehaviorFailureCode.ActionCommitRejected;
            _state.CommitFailureDetails[index] = (_runtime as ICharacterTestBehaviorExecutionContinuationRuntime)?.ActionCommitFailureDetails ?? default;
            _state.CommitFailureDetailIds[index] = (_runtime as ICharacterTestBehaviorExecutionContinuationRuntime)?.ActionCommitFailureDetailId ?? 0;
            _state.Generations[index]++;
        }
        private bool Eligible(int index, int frame, int previousWorld)
        {
            var evt = _program.Events[index]; int clock = Clock(evt, frame);
            if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
            {
                int previous = evt.Clock == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe
                    ? Math.Max(0, previousWorld - _state.CardStartedFrame) : _state.LastSampledFrame;
                if (clock != evt.StartFrame || clock == previous) return false;
            }
            else if (evt.Timing == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant ? clock != evt.StartFrame : !InRange(evt, frame)) return false;
            if (evt.MaxExecutionsPerPass > 0 && _state.Counts[index] >= evt.MaxExecutionsPerPass) return false;
            if (_state.LastExecutions[index] != int.MinValue && (long)frame - _state.LastExecutions[index] < evt.CooldownFrames) return false;
            // A scheduled action event requests playback. Preparation belongs to explicit flow/control nodes.
            return evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction || Evaluate(evt.Condition);
        }
        private int Clock(in CharacterTestBehaviorExecutionEvent evt, int frame) => evt.Clock == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe
            ? Math.Max(0, frame - _state.CardStartedFrame) : _state.LocalFrame;
        private bool InRange(in CharacterTestBehaviorExecutionEvent evt, int frame)
        { int clock = Clock(evt, frame); return clock >= evt.StartFrame && clock < evt.EndFrame; }
        private int SelectControl(int index, int owner, int frame)
        {
            if (owner >= 0 && owner != index)
            { _state.ActiveControls[owner] = false; SetResult(owner, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled); }
            if (!_state.ActiveControls[index])
            { Record(index, frame); _state.ActiveControls[index] = true; _state.Stopped[index] = false; _state.Generations[index]++;
                if (_state.RearOrbitSides.Length > 0) { _state.RearOrbitSides[index] = 0; _state.RearTargetIds[index] = 0; } }
            SetResult(index, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Running); return index;
        }
        private void CompleteExpiredControls(int frame)
        {
            _state.MovementOwner = CompleteExpired(_state.MovementOwner, frame); _state.TurnOwner = CompleteExpired(_state.TurnOwner, frame);
        }
        private int CompleteExpired(int owner, int frame)
        {
            if (owner < 0 || InRange(_program.Events[owner], frame)) return owner;
            _state.ActiveControls[owner] = false; SetResult(owner, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed); return -1;
        }
        private void SampleControlsWhilePending(int frame)
        {
            CompleteExpiredControls(frame);
            int move = -1, turn = -1, limit = -1, tx = 0, tz = 0;
            Vector2 vector = Vector2.zero; float speed = 1f, turnSpeed = 1f;
            if (_state.MovementOwner >= 0)
            {
                var parameter = _program.Movements[_program.Events[_state.MovementOwner].Parameter];
                if (_runtime.TrySampleMovement(parameter, out var sample))
                { move = _state.MovementOwner; vector = ResolveMovement(parameter, sample, move, out limit); speed = parameter.Speed; }
            }
            if (_state.TurnOwner >= 0)
            {
                var parameter = _program.Turns[_program.Events[_state.TurnOwner].Parameter];
                if (_runtime.TrySampleTurn(parameter, out tx, out tz)) { turn = _state.TurnOwner; turnSpeed = parameter.Speed; }
            }
            PublishControl(move, turn, vector, speed, limit, tx, tz, turnSpeed);
        }
        private Vector2 ResolveMovement(in CharacterTestBehaviorExecutionMovement move, in CharacterTestRuntimeAiLabMovementTargetSample sample, int index, out int limit)
        {
            if (move.BehindTarget.Enabled)
            {
                ObserveBehindTarget(index, sample);
                return CharacterTestBehindTargetResolver.Resolve(move, sample, ref _state.Stopped[index], ref _state.RearOrbitSides[index], out limit);
            }
            limit = -1; Vector2 toward = sample.TowardTarget.sqrMagnitude > 0.0001f ? sample.TowardTarget.normalized : sample.NavigationMove.normalized;
            Vector2 navigation = sample.NavigationMove.sqrMagnitude > 0.0001f ? sample.NavigationMove.normalized : toward;
            if (move.IsOptimalRange)
            {
                if (_state.Stopped[index] && (sample.Distance < move.Minimum || sample.Distance > move.Maximum)) _state.Stopped[index] = false;
                float error = sample.Distance - move.Optimal;
                if (!_state.Stopped[index] && Math.Abs(error) <= move.StopTolerance) _state.Stopped[index] = true;
                limit = _state.Stopped[index] ? 0 : (int)Math.Min(int.MaxValue, Math.Round(Math.Abs((double)error) * 1000d));
                return _state.Stopped[index] ? Vector2.zero : error < 0f ? -toward : navigation;
            }
            bool band = move.ExplicitDistance || move.Minimum > 0f || move.Maximum > 0f;
            float threshold = move.ExplicitDistance || move.Minimum > 0f ? move.Minimum : move.Maximum;
            switch (move.Strategy)
            {
                case CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget: return !move.ExplicitDistance && move.Minimum <= 0f || sample.Distance > move.Minimum ? navigation : Vector2.zero;
                case CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance:
                    if (!band) return -toward;
                    float error = sample.Distance < move.EffectiveMinimum ? sample.Distance - move.EffectiveMinimum
                        : sample.Distance > move.EffectiveMaximum ? sample.Distance - move.EffectiveMaximum : 0f;
                    // Bound the remaining travel so braking cannot carry a single-distance hold across its stop band.
                    if (move.DistanceTolerance > 0f)
                        limit = (int)Math.Min(int.MaxValue, Math.Round(Math.Abs((double)error) * 1000d));
                    return error < 0f ? -toward : error > 0f ? navigation : Vector2.zero;
                case CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose: return !move.ExplicitDistance && threshold <= 0f || sample.Distance < threshold ? -toward : Vector2.zero;
                case CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard: return -toward;
                case CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked:
                case CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget:
                    Vector2 tangent = new Vector2(-toward.y, toward.x) * _runtimeEntitySide(move);
                    if (move.Strategy == CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked || !band) return tangent;
                    float radial = sample.Distance < move.Minimum ? -1f : sample.Distance > move.Maximum ? 1f : 0f;
                    return Vector2.ClampMagnitude(tangent + toward * (radial * move.RadialWeight), 1f);
                default: return Vector2.zero;
            }
        }
        // Set once by the shared host/overlay; identity is also fixed in its binding snapshot.
        public int EntityId
        {
            get => _state.EntityId;
            set
            {
                if (State != CharacterTestRuntimeAiLabLogicRuntimeState.Inactive && _state.EntityId != value)
                    throw new InvalidOperationException("Cannot rebind an active execution instance.");
                _state.EntityId = value;
            }
        }
        private int _runtimeEntitySide(in CharacterTestBehaviorExecutionMovement move) => move.ResolveSide(EntityId);
        private void PublishControl(int move, int turn, Vector2 vector, float speed, int limit, int tx, int tz, float turnSpeed)
        {
            if (move < 0 && turn < 0 && IsPassiveWaitActive) return;
            bool compatibilityFacing = move >= 0 && !_program.Movements[_program.Events[move].Parameter].IndependentFacing
                && !_program.Movements[_program.Events[move].Parameter].IsOptimalRange
                && (turn < 0 || _program.Events[move].Rank < _program.Events[turn].Rank);
            int mode = compatibilityFacing ? 0 : turn >= 0 ? 2 : 1;
            var control = new CharacterTestBehaviorControlInput(mode, tx, tz,
                turnSpeedScaleMilli: (int)Math.Round((mode == 2 ? turnSpeed : 1f) * 1000f), maxMoveStepMillimeters: limit, ownerToken: ControlOwnerToken,
                isRelease: move < 0 && turn < 0 && _state.AttackWaitBlock < 0,
                isOwnerOnlyRelease: move < 0 && turn < 0 && UsesOwnedRelease);
            _runtime.SetExecutionControl(vector, speed, control);
        }
        private bool IsPassiveWaitActive
        {
            get
            {
                if (_state.AttackWaitBlock >= 0 && _program.Blocks[_state.AttackWaitBlock].PassiveWait) return true;
                for (int i = 0; i < _program.Blocks.Length; i++)
                    if (_state.BlockResults[i] == 1 && _program.Blocks[i].Timeline == _state.Timeline && _program.Blocks[i].PassiveWait
                        && (_program.Blocks[i].Completion == CharacterTestBehaviorBlockCompletion.Delay
                            || _program.Blocks[i].Completion == CharacterTestBehaviorBlockCompletion.Distance)) return true;
                return false;
            }
        }
        private bool UsesOwnedRelease
        {
            get { foreach (var block in _program.Blocks) if (block.PassiveWait || block.Completion == CharacterTestBehaviorBlockCompletion.Facing || BlockHasBehindTarget(block)) return true; return false; }
        }
        private bool UsesPassiveWait
        {
            get { foreach (var block in _program.Blocks) if (block.PassiveWait) return true; return false; }
        }
        private void ReleaseControls(bool forceOwnedRelease = false)
        {
            if (UsesPassiveWait && !forceOwnedRelease && _state.MovementOwner < 0 && _state.TurnOwner < 0) return;
            if (_state.MovementOwner >= 0) { _state.ActiveControls[_state.MovementOwner] = false; SetResult(_state.MovementOwner, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled); }
            if (_state.TurnOwner >= 0) { _state.ActiveControls[_state.TurnOwner] = false; SetResult(_state.TurnOwner, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled); }
            _state.MovementOwner = -1; _state.TurnOwner = -1;
            _runtime.SetExecutionControl(Vector2.zero, 1f, new CharacterTestBehaviorControlInput(1, 0, 0, ownerToken: ControlOwnerToken, isRelease: true, isOwnerOnlyRelease: UsesOwnedRelease));
        }
        private long ControlOwnerToken => _program.Identity ^ ((long)_state.EntityId << 32) ^ _state.CardStartedFrame;
        private void BeginStop()
        {
            if (_state.StopStartedFrame < 0) _state.StopStartedFrame = _state.LastWorldFrame;
            if (_state.PendingAction >= 0 || _state.StopPending) return;
            if (_state.ActionInstanceId <= 0)
            {
                if (_state.JumpTarget >= 0)
                {
                    if (_runtime.TryStopAction(0)) { _state.StopPending = true; _state.State = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning; }
                    else Stop(true);
                }
                else FinishBoundary(_state.LastWorldFrame);
                return;
            }
            if (_runtime.TryStopAction(_state.ActionInstanceId))
            { _state.StopPending = true; _state.State = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning; }
            else { _state.EndReason = (int)CharacterTestBehaviorExecutionEndReason.CleanupFailed;
                _state.FailureRequested = true; _state.FinishRequested = true; _state.State = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Failed; }
        }
        private bool RefreshPending(int frame)
        {
            if (_state.PendingAction < 0 && !_state.StopPending) return true;
            bool flowBoundary = _state.StopPending && _state.JumpTarget >= 0;
            var status = _runtime.ActionCommandState;
            if (status == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending || status == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.None) return false;
            if (status == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Rejected)
            {
                bool actionFailure = _state.PendingAction >= 0;
                if (actionFailure) SetCommitFailed(_state.PendingAction);
                _state.PendingAction = -1; _state.StopPending = false; _runtime.CompleteActionCommand();
                if (!actionFailure) { Stop(true); return false; }
                _state.State = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Running;
                return true;
            }
            if (_state.PendingAction >= 0)
            {
                if (_state.ActionOwner >= 0) SetResult(_state.ActionOwner, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed);
                _state.ActionOwner = _state.PendingAction; _state.PendingAction = -1;
                _state.ActionInstanceId = _runtime.StartedActionInstanceId;
                _runtime.ObserveOwnedAction(_state.ActionInstanceId);
                SetResult(_state.ActionOwner, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Running);
                _state.Generations[_state.ActionOwner]++;
            }
            else
            {
                if (_state.ActionOwner >= 0 && !IsCompletedGuardBlock(_state.ActionOwner))
                    SetResult(_state.ActionOwner, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled);
                _state.ActionOwner = -1; _state.ActionInstanceId = 0; _state.StopPending = false;
                _runtime.ObserveOwnedAction(0L);
            }
            _runtime.CompleteActionCommand(); _state.State = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Running;
            if (!_state.FinishRequested && _state.JumpTarget < 0) _state.StopStartedFrame = -1;
            FinishBoundary(frame); return !flowBoundary && State == CharacterTestRuntimeAiLabLogicRuntimeState.Running;
        }
        private void FinishBoundary(int frame)
        {
            if (_state.ActionInstanceId > 0) return;
            if (_state.FinishRequested)
            { _state.State = (int)(_state.FailureRequested ? CharacterTestRuntimeAiLabLogicRuntimeState.Failed : CharacterTestRuntimeAiLabLogicRuntimeState.Completed); return; }
            if (_state.JumpTarget >= 0)
            {
                int target = _state.JumpTarget; _state.JumpTarget = -1;
                if (_state.FlowEvent >= 0) SetResult(_state.FlowEvent, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed);
                _state.FlowEvent = -1; EnterTimeline(target, frame);
            }
        }
        private void RefreshAction()
        {
            if (_state.ActionOwner < 0 || _state.PendingAction >= 0 || _state.StopPending) return;
            if (_runtime.ActualActionInstanceId != _state.ActionInstanceId || _runtime.ActualActionState == CharacterTestRuntimeAiLabLogicActionState.Completed)
            { SetResult(_state.ActionOwner, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed); _state.ActionOwner = -1; _state.ActionInstanceId = 0; _runtime.ObserveOwnedAction(0L); }
            else if (_runtime.ActualActionState == CharacterTestRuntimeAiLabLogicActionState.Failed)
            { SetResult(_state.ActionOwner, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Failed); _state.ActionOwner = -1; _state.ActionInstanceId = 0; _runtime.ObserveOwnedAction(0L); }
        }
        private void EnterTimeline(int timeline, int frame)
        {
            ClearAttackWait();
            _state.StopStartedFrame = -1;
            for (int i = 0; i < _program.Blocks.Length; i++)
                if (_program.Blocks[i].Timeline == timeline) { _state.BlockStarts[i] = -1; _state.BlockResults[i] = 0; }
            _state.Timeline = timeline; _state.TimelineEnteredFrame = frame + 1; _state.LocalFrame = 0; _state.LastSampledFrame = -1;
            _state.PassGeneration = 0; _state.WaitEvent = -1; _state.State = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Running;
            for (int i = 0; i < _program.Events.Length; i++)
            {
                var evt = _program.Events[i]; if (evt.TimelineIndex != timeline || evt.Shared) continue;
                _state.Counts[i] = 0; _state.LastExecutions[i] = int.MinValue; _state.Results[i] = 0; _state.LoopRepeats[i] = 0;
                _state.CommitFailureReasons[i] = 0;
                _state.CommitFailureDetails[i] = default;
                _state.CommitFailureDetailIds[i] = 0;
                _state.ActiveControls[i] = false; _state.Stopped[i] = false;
            }
        }
        private void Advance(in CharacterTestBehaviorExecutionTimeline timeline, int frame)
        {
            _state.WaitEvent = -1;
            for (int i = 0; i < timeline.EventCount; i++)
            {
                int index = _program.TimelineEvents[timeline.EventStart + i]; var evt = _program.Events[index];
                if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition && evt.StartFrame == _state.LocalFrame
                    && evt.Clock == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline && (evt.Condition < 0 || !Evaluate(evt.Condition)))
                { _state.WaitEvent = index; return; }
            }
            for (int i = 0; i < timeline.EventCount; i++)
            {
                int index = _program.TimelineEvents[timeline.EventStart + i]; var evt = _program.Events[index];
                if (evt.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion || evt.EndFrame - 1 != _state.LocalFrame
                    || evt.Clock != CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline) continue;
                int repeats = _program.Flows[evt.Parameter].MaxLoopRepeats;
                if ((evt.Condition >= 0 && Evaluate(evt.Condition)) || (repeats > 0 && _state.LoopRepeats[index] >= repeats)) break;
                _state.LoopRepeats[index]++; _state.PassGeneration++;
                for (int e = 0; e < _program.Events.Length; e++)
                {
                    var reset = _program.Events[e];
                    if (reset.TimelineIndex != _state.Timeline || reset.Shared || reset.StartFrame < evt.StartFrame || reset.EndFrame > evt.EndFrame) continue;
                    _state.Counts[e] = 0; _state.Results[e] = 0;
                    if (e != _state.MovementOwner && e != _state.TurnOwner) { _state.ActiveControls[e] = false; _state.Stopped[e] = false; }
                }
                _state.LocalFrame = evt.StartFrame; return;
            }
            _state.LocalFrame++;
        }
        private bool Evaluate(int index)
            => EvaluateCondition(_program, index, _runtime, _state.Results, _state);

        internal static bool EvaluateCondition(CharacterTestBehaviorExecutionProgram program, int index,
            ICharacterTestBehaviorExecutionRuntime runtime, int[] results = null, CharacterTestBehaviorExecutionState state = null)
        {
            if (index < 0) return true;
            var node = program.Conditions[index];
            switch (node.Kind)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate:
                    var predicate = program.Predicates[node.Predicate];
                    if (predicate.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetCurrentAttackWindowEnded)
                        return predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.Fact
                            && state != null && state.AttackWaitBlock >= 0
                            && runtime is ICharacterTestBehaviorExecutionAttackWindowRuntime windows
                            && windows.IsCapturedTargetAvailable(state.AttackWaitTargetId)
                            && CompareAttackWait(!windows.AnyCapturedTargetAttackWindowActive(state.AttackWaitTargetId,
                                state.AttackWaitInstanceId, state.AttackWaitWindowIds), predicate);
                    return EvaluateExecutionPredicate(predicate, runtime);
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal:
                    return results != null && results[node.SourceEvent] == (int)(node.Signal switch
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Completed => CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed,
                        CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Failed => CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Failed,
                        CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Rejected => CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected,
                        CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.CommitFailed => CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.CommitFailed,
                        _ => CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled
                    });
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not: return !EvaluateCondition(program, program.Children[node.ChildStart], runtime, results, state);
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope: return runtime.IsSubjectAvailable(node.Subject) && EvaluateCondition(program, program.Children[node.ChildStart], runtime, results, state);
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All:
                    for (int i = 0; i < node.ChildCount; i++) if (!EvaluateCondition(program, program.Children[node.ChildStart + i], runtime, results, state)) return false; return true;
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any:
                    for (int i = 0; i < node.ChildCount; i++) if (EvaluateCondition(program, program.Children[node.ChildStart + i], runtime, results, state)) return true; return false;
                default: return false;
            }
        }
        private void Record(int index, int frame) { _state.Counts[index]++; _state.LastExecutions[index] = frame; }
        private void SetResult(int index, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult result) { _state.Results[index] = (int)result; }
        private void Emit(CharacterTestRuntimeAiLabLogicEventBuffer events, int frame, int index, CharacterTestRuntimeAiLabLogicEventKind kind)
        { events?.Add(new CharacterTestRuntimeAiLabLogicEvent(frame, kind, _debug?.Timeline(_state.Timeline) ?? string.Empty,
            _debug?.Event(index) ?? string.Empty, string.Empty, localFrame: LocalFrame, primaryActionInstanceId: OwnedActionInstanceId, stageIndex: _state.Timeline, detectorIndex: index)); }
        internal void CopySnapshotTo(CharacterTestBehaviorExecutionState target) => _state.CopyTo(target);
        internal bool CanCopySnapshotTo(CharacterTestBehaviorExecutionState target) => target != null
            && target.Version == _state.Version && target.ProgramIdentity == _program.Identity
            && target.Counts?.Length == _state.Counts.Length && target.BlockStarts?.Length == _state.BlockStarts.Length;
        private bool ValidRearOrbitSnapshot(CharacterTestBehaviorExecutionState source)
        {
            if (source.RearOrbitSides == null || source.RearOrbitSides.Length != _state.RearOrbitSides.Length
                || source.RearTargetIds == null || source.RearTargetIds.Length != _state.RearTargetIds.Length) return false;
            for (int i = 0; i < source.RearOrbitSides.Length; i++)
            {
                int side = source.RearOrbitSides[i];
                if (side < -1 || side > 1 || source.RearTargetIds[i] < 0) return false;
                var evt = _program.Events[i];
                if ((side != 0 || source.RearTargetIds[i] != 0) && (evt.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                    || !_program.Movements[evt.Parameter].BehindTarget.Enabled)) return false;
            }
            return true;
        }
        public CharacterTestBehaviorExecutionState CreateSnapshot() => _state.Clone();
        public void AddHash(RuntimeHashAccumulator hash, int key = 1) => _state.AddHash(hash, key);
        public bool TryRestoreSnapshot(CharacterTestBehaviorExecutionState source)
        {
            int count = _program.Events.Length;
            if (source == null || source.Version != SnapshotVersion(_program)
                || source.ProgramFormat != CharacterTestBehaviorExecutionProgram.CurrentFormatVersion || source.ProgramIdentity != _program.Identity
                || source.EntityId != _state.EntityId || source.ActionInstanceId < 0 || (source.StopPending && source.PendingAction >= 0)
                || !Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicRuntimeState), source.State)
                || source.Timeline < -1 || source.Timeline >= _program.Timelines.Length || source.LocalFrame < 0
                || source.Counts?.Length != count || source.LastExecutions?.Length != count || source.Results?.Length != count || source.CommitFailureReasons?.Length != count || source.CommitFailureDetails?.Length != count || source.CommitFailureDetailIds?.Length != count
                || source.LoopRepeats?.Length != count || source.Generations?.Length != count || source.ActiveControls?.Length != count || source.Stopped?.Length != count
                || source.BlockStarts?.Length != _program.Blocks.Length || source.BlockResults?.Length != _program.Blocks.Length
                || source.StopStartedFrame < -1 || source.StopStartedFrame > source.LastWorldFrame
                || !Enum.IsDefined(typeof(CharacterTestBehaviorExecutionEndReason), source.EndReason)
                || !ValidIndex(source.MovementOwner, count) || !ValidIndex(source.TurnOwner, count) || !ValidIndex(source.ActionOwner, count)
                || !ValidIndex(source.PendingAction, count) || !ValidIndex(source.FlowEvent, count) || !ValidIndex(source.WaitEvent, count)
                || !ValidIndex(source.LastTriggeredEvent, count) || !ValidIndex(source.JumpTarget, _program.Timelines.Length)
                || !ValidAttackWaitSnapshot(source) || !ValidRearOrbitSnapshot(source)) return false;
            if ((source.State == (int)CharacterTestRuntimeAiLabLogicRuntimeState.Running || source.State == (int)CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning)
                && (source.Timeline < 0 || source.LocalFrame > _program.Timelines[source.Timeline].DurationFrames)) return false;
            if (!OwnerKind(source.MovementOwner, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget)
                || !OwnerKind(source.TurnOwner, CharacterTestBehaviorExecutionOperations.TurnToTarget)
                || !OwnerKind(source.ActionOwner, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                || !OwnerKind(source.PendingAction, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)) return false;
            for (int i = 0; i < count; i++)
                if (!source.CommitFailureDetails[i].IsValid || source.Counts[i] < 0 || source.Generations[i] < 0 || source.LoopRepeats[i] < 0
                    || source.Results[i] < 0 || source.Results[i] > (int)CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.CommitFailed
                    || !CharacterTestBehaviorFailureCodes.IsValid(source.CommitFailureReasons[i], source.CommitFailureDetailIds[i])) return false;
            for (int i = 0; i < _program.Blocks.Length; i++)
                if (source.BlockStarts[i] < -1 || source.BlockStarts[i] > source.LastWorldFrame
                    || source.BlockResults[i] < 0 || source.BlockResults[i] > 3
                    || (source.BlockResults[i] == 0) != (source.BlockStarts[i] == -1)) return false;
            _state = source.Clone(); LastActionConditionRejectedEvent = -1; return true;
        }
        private static bool ValidIndex(int index, int count) => index >= -1 && index < count;
        private bool OwnerKind(int index, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind kind) => index < 0 || _program.Events[index].Kind == kind;
    }

    internal static class BehaviorExecutionStateHashFields
    {
        internal const int RearOrbitSides = 71, RearOrbitSideValue = 72, RearOrbitSidesPresent = 73;
        internal const int RearTargetIds = 74, RearTargetIdValue = 75, RearTargetIdsPresent = 76;
        internal const int AttackWaitBlock = 65, AttackWaitTarget = 66, AttackWaitInstance = 67;
        internal const int AttackWaitWindows = 68, AttackWaitWindowValue = 69, AttackWaitWindowsPresent = 70;
        internal const int BlockStarts = 57, BlockStartValue = 58, BlockStartPresent = 59;
        internal const int BlockResults = 60, BlockResultValue = 61, BlockResultPresent = 62;
        internal const int EndReason = 63, StopStart = 64;
        internal const int CommitFailureDetailValue = 37;
        internal const int CommitFailureDetailsPresent = 38;
        internal const int CountValue = 39;
        internal const int CountPresent = 40;
        internal const int LastExecutionValue = 41;
        internal const int LastExecutionPresent = 42;
        internal const int ResultValue = 43;
        internal const int ResultPresent = 44;
        internal const int CommitFailureValue = 45;
        internal const int CommitFailurePresent = 46;
        internal const int CommitFailureDetailIdsValue = 47;
        internal const int CommitFailureDetailIdsPresent = 48;
        internal const int RepeatValue = 49;
        internal const int RepeatPresent = 50;
        internal const int GenerationValue = 51;
        internal const int GenerationPresent = 52;
        internal const int ActiveValue = 53;
        internal const int ActivePresent = 54;
        internal const int StoppedValue = 55;
        internal const int StoppedPresent = 56;
        internal const int CommitFailureDetails = 35;
        internal const int CommitFailureDetailIds = 36;
        internal const int SchemaId = RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecutionState;
        internal const int SchemaVersion = 4;
        internal const int ExecutionStateVersion = 1;
        internal const int ExecutionStateFormat = 2;
        internal const int ExecutionStateIdentity = 3;
        internal const int State = 4;
        internal const int Timeline = 5;
        internal const int ExecutionStateEntity = 6;
        internal const int CardStart = 7;
        internal const int TimelineStart = 8;
        internal const int Local = 9;
        internal const int LastSample = 10;
        internal const int LastWorld = 11;
        internal const int Pass = 12;
        internal const int Wait = 13;
        internal const int MoveOwner = 14;
        internal const int TurnOwner = 15;
        internal const int ActionOwner = 16;
        internal const int Instance = 17;
        internal const int PendingAction = 18;
        internal const int StopPending = 19;
        internal const int Jump = 20;
        internal const int Flow = 21;
        internal const int Finish = 22;
        internal const int Failure = 23;
        internal const int LastEvent = 24;
        internal const int CommitFailure = 25;
        internal const int Active = 26;
        internal const int Stopped = 27;
        internal const int Count = 28;
        internal const int LastExecution = 29;
        internal const int Result = 30;
        internal const int Repeat = 31;
        internal const int Generation = 32;
    }
}

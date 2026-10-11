using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestBehaviorOrchestrationReason
    {
        None = 0,
        EntityMissing = 1,
        CharacterActionRuntimeMissing = 2,
        CharacterActionUnknown = 3,
        CharacterActionReplaced = 4,
        CharacterActionCommitRejected = 5,
        AbilityRuntimeMissing = 6,
        AbilityUnknown = 7,
        AbilityActionUnknown = 8,
        AbilityReplaced = 9,
        AbilityCommitRejected = 10,
        TimelineJumpTerminateRejected = 11,
        TimelineJumpTerminated = 12,
        ReturnToPlannerTerminateRejected = 13,
        ReturnToPlannerTerminated = 14,
        OrchestrationOperationInvalid = 15
    }

    internal readonly struct CharacterTestBehaviorOrchestrationReceipt
    {
        public readonly RuntimeFrame Frame;
        public readonly int EntityId, ResourceId;
        public readonly CharacterTestBehaviorRecipeOrchestrationOperation Operation;
        public readonly bool Success;
        public readonly long ActionInstanceId;
        public readonly string Reason;
        public readonly CharacterTestBehaviorOrchestrationReason ReasonCode;
        public readonly CharacterTestActionUseFailure FailureDetails;
        public CharacterTestBehaviorOrchestrationReceipt(RuntimeFrame frame, int entityId,
            CharacterTestBehaviorRecipeOrchestrationOperation operation, int resourceId, bool success, long instanceId,
            CharacterTestBehaviorOrchestrationReason reasonCode, string reason, in CharacterTestActionUseFailure failureDetails)
        { Frame = frame; EntityId = entityId; Operation = operation; ResourceId = resourceId; Success = success; ActionInstanceId = instanceId; ReasonCode = reasonCode; Reason = reason; FailureDetails = failureDetails; }
    }
    internal interface ICharacterTestBehaviorReceiptObserver
    { void OnBehaviorOrchestrationReceipt(in CharacterTestBehaviorOrchestrationReceipt receipt); }
    internal interface ICharacterTestBehaviorActionContext
    { bool TryGetActionFacts(RuntimeFrame frame, int entity, int resource, out CharacterTestRuntimeAiPlannerFactState facts); }
    internal interface ICharacterTestBehaviorActionCommitGuard
    { bool CanCommitAction(int actionId); }
    internal interface ICharacterTestBehaviorContinuationObserver
    { bool ContinueAfterCommand(RuntimeFrame frame, Func<RuntimeCommand, RuntimeCommandValidationResult> apply); }

    public sealed partial class CharacterTestCharacterRuntimeModule
    {
        // Receivers retain the applied receipt in their own authority snapshots. The registry itself is derived wiring.
        private readonly List<WeakReference<ICharacterTestBehaviorReceiptObserver>> _behaviorReceiptObservers =
            new List<WeakReference<ICharacterTestBehaviorReceiptObserver>>(16);
        internal void RegisterBehaviorReceiptObserver(ICharacterTestBehaviorReceiptObserver observer)
        { _behaviorReceiptObservers.Add(new WeakReference<ICharacterTestBehaviorReceiptObserver>(observer)); }
        internal void ConsumeBehaviorContinuations(RuntimeFrame frame, Func<RuntimeCommand, RuntimeCommandValidationResult> apply)
        {
            for (int pass = 0; pass < 128; pass++)
            {
                bool progressed = false;
                for (int i = _behaviorReceiptObservers.Count - 1; i >= 0; i--)
                    if (_behaviorReceiptObservers[i].TryGetTarget(out var receiver) && receiver is ICharacterTestBehaviorContinuationObserver continuation)
                        progressed |= continuation.ContinueAfterCommand(frame, apply);
                if (!progressed) return;
            }
            throw new InvalidOperationException("Behavior command continuation exceeded its same-frame transition budget.");
        }
        private CharacterTestRuntimeAiPlannerFactState ResolveBehaviorActionFacts(RuntimeFrame frame, int entity, int resource)
        {
            for (int i = _behaviorReceiptObservers.Count - 1; i >= 0; i--)
                if (_behaviorReceiptObservers[i].TryGetTarget(out var receiver) && receiver is ICharacterTestBehaviorActionContext context
                    && context.TryGetActionFacts(frame, entity, resource, out var facts)) return facts;
            return null;
        }

        private bool CanCommitBehaviorAction(RuntimeFrame frame, int entity, int resource)
        {
            for (int i = _behaviorReceiptObservers.Count - 1; i >= 0; i--)
                if (_behaviorReceiptObservers[i].TryGetTarget(out var receiver) && receiver is ICharacterTestBehaviorActionContext context
                    && context.TryGetActionFacts(frame, entity, resource, out _)
                    && receiver is ICharacterTestBehaviorActionCommitGuard guard) return guard.CanCommitAction(resource);
            return true;
        }
        private readonly CharacterTestBehaviorCommandReceipts<CharacterTestBehaviorOrchestrationReceipt> _behaviorCommandReceipts = new();
        private bool _behaviorCommandApplying;
        internal void ResetBehaviorCommandReceipts(RuntimeFrame frame)
        {
            if(_behaviorCommandApplying)throw new InvalidOperationException("Behavior receipts cannot reset during a command commit.");
            _behaviorCommandReceipts.ResetFromFrame(frame.Value);
        }
        internal bool TryApplyBehaviorRecipeOrchestration(RuntimeFrame frame, int targetEntityId,
            CharacterTestBehaviorRecipeOrchestrationOperation operation, int resourceId, RuntimeTraceIdentity traceId, out string message)
            => TryApplyBehaviorRecipeOrchestration(frame,targetEntityId,operation,resourceId,traceId,-1,0,out message);

        internal bool TryApplyBehaviorRecipeOrchestration(RuntimeFrame frame, int targetEntityId,
            CharacterTestBehaviorRecipeOrchestrationOperation operation, int resourceId, RuntimeTraceIdentity traceId,
            long commandSequence,int commandSource,out string message,int commandPayload2=0,RuntimeCommandEnvelope commandEnvelope=default)
        {
            var actor=FindEntity(targetEntityId)?.GameplayEntityId??default;
            if(_behaviorCommandApplying){message="BehaviorRecipe.CommandReentered";return false;}
            if(commandSequence>=0){var match=_behaviorCommandReceipts.Match(frame.Value,commandSource,commandSequence,actor,(int)operation,resourceId,traceId,out var saved,commandPayload2,commandEnvelope);
                if(match==BehaviorCommandReceiptMatch.Replay){message=saved.Reason;return saved.Success;}
                if(match==BehaviorCommandReceiptMatch.Rejected){message="BehaviorRecipe.CommandIdentityRejected";return false;}}
            // Record the cause before any authority write. An interrupted/throwing commit stays closed to replay.
            int receiptIndex=commandSequence>=0?_behaviorCommandReceipts.Begin(commandSource,commandSequence,actor,(int)operation,resourceId,traceId,commandPayload2,commandEnvelope):-1;
            _behaviorCommandApplying=true;
            try {
            bool success = TryApplyBehaviorRecipeOrchestrationCore(frame, targetEntityId, operation, resourceId, traceId, out message, out CharacterTestBehaviorOrchestrationReason reasonCode, out CharacterTestActionUseFailure failureDetails);
            long instance = 0;
            ActionRuntime?.TryGetActiveActionPresentationState(new CharacterTestCharacterEntityId(targetEntityId), out instance, out _, out _, out _);
            var receipt = new CharacterTestBehaviorOrchestrationReceipt(frame, targetEntityId, operation, resourceId, success, instance, reasonCode, message, failureDetails);
            if(commandSequence>=0)_behaviorCommandReceipts.Complete(receiptIndex,in receipt);
            for (int i = _behaviorReceiptObservers.Count - 1; i >= 0; i--)
            {
                if (_behaviorReceiptObservers[i].TryGetTarget(out var receiver)) receiver.OnBehaviorOrchestrationReceipt(receipt);
                else _behaviorReceiptObservers.RemoveAt(i);
            }
            return success;
            } finally {_behaviorCommandApplying=false;}
        }
    }
}

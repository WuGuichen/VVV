using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestPositionRecallOperation { None = 0, Record = 1, Return = 2 }
    internal enum CharacterTestPositionRecallResult
    { None = 0, Pending = 1, Recorded = 2, Returned = 3, NoRecord = 4, ActorUnavailable = 5, ActorBusy = 6, DestinationBlocked = 7, Cancelled = 8 }

    /// <summary>One actor generation in one battle owns a frozen, single-use world position and authoritative receipts.</summary>
    internal struct CharacterTestPositionRecallState
    {
        public ulong StageRun { get; set; }
        public GameplayEntityId Actor { get; set; }
        public bool HasRecord { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public long RecordedTick { get; set; }
        public long ExpiresTick { get; set; }
        public int RemainingUses { get; set; }
        public ulong ExecutionId { get; set; }
        public CharacterTestPositionRecallOperation Operation { get; set; }
        public CharacterTestPositionRecallResult Result { get; set; }
        public int LifetimeFrames { get; set; }
        public long RequestedTick { get; set; }
        public long CompletedTick { get; set; }
        public long StartedTick { get; set; }
        internal bool Pending => Result == CharacterTestPositionRecallResult.Pending;
        internal bool Succeeded => Result == CharacterTestPositionRecallResult.Recorded || Result == CharacterTestPositionRecallResult.Returned;
        internal bool Matches(ulong stage, ulong execution) => StageRun == stage && ExecutionId == execution && execution != 0;
        internal bool RecordValid(long frame) => HasRecord && RemainingUses == 1 && frame >= RecordedTick && frame < ExpiresTick;
        internal static CharacterTestPositionRecallState Bind(ulong stage, GameplayEntityId actor) =>
            new CharacterTestPositionRecallState { StageRun = stage, Actor = actor, StartedTick = -1, CompletedTick = -1 };

        /// <remarks>NoAlloc. Monotonic execution identities prevent duplicate/stale commands from recording or returning again.</remarks>
        internal bool Submit(ulong stage, GameplayEntityId actor, ulong execution, CharacterTestPositionRecallOperation operation,
            int lifetime, long frame)
        {
            if (stage == 0 || StageRun != stage || Actor != actor || !actor.IsValid || execution == 0 || execution > long.MaxValue
                || execution <= ExecutionId || Pending || frame < 0 || frame < RequestedTick
                || (operation != CharacterTestPositionRecallOperation.Record && operation != CharacterTestPositionRecallOperation.Return)
                || (operation == CharacterTestPositionRecallOperation.Record ? lifetime < 1 || lifetime > 3600 : lifetime != 0)) return false;
            ExecutionId = execution; Operation = operation; LifetimeFrames = lifetime;
            RequestedTick = frame; CompletedTick = StartedTick = -1; Result = CharacterTestPositionRecallResult.Pending;
            Expire(frame); return true;
        }
        internal void Expire(long frame) { if (HasRecord && !RecordValid(frame)) ClearRecord(); }
        internal void ClearRecord() { HasRecord = false; X = Y = Z = RemainingUses = 0; RecordedTick = ExpiresTick = 0; }
        internal void Complete(CharacterTestPositionRecallResult result, long frame)
        {
            if (!Pending || frame < RequestedTick || result <= CharacterTestPositionRecallResult.Pending
                || result > CharacterTestPositionRecallResult.Cancelled
                || result == CharacterTestPositionRecallResult.Recorded && Operation != CharacterTestPositionRecallOperation.Record
                || result == CharacterTestPositionRecallResult.Returned && Operation != CharacterTestPositionRecallOperation.Return) return;
            Result = result; CompletedTick = frame; StartedTick = Succeeded ? frame : -1;
        }
        internal void CompleteRecord(int x, int y, int z, long frame)
        {
            if (!Pending || Operation != CharacterTestPositionRecallOperation.Record || frame < RequestedTick || frame > long.MaxValue - LifetimeFrames) return;
            HasRecord = true; X = x; Y = y; Z = z; RecordedTick = frame; ExpiresTick = frame + LifetimeFrames; RemainingUses = 1;
            Complete(CharacterTestPositionRecallResult.Recorded, frame);
        }
        // Call only after the movement authority has successfully committed the exact destination.
        internal void CompleteReturn(long frame)
        {
            if (!Pending || Operation != CharacterTestPositionRecallOperation.Return || !RecordValid(frame)) return;
            ClearRecord(); Complete(CharacterTestPositionRecallResult.Returned, frame);
        }
        internal bool IsValidSnapshot(long frame = long.MaxValue)
        {
            if (StageRun == 0) return Actor == default && !HasRecord && X == 0 && Y == 0 && Z == 0 && RemainingUses == 0
                && RecordedTick == 0 && ExpiresTick == 0 && ExecutionId == 0 && Operation == 0 && Result == 0 && LifetimeFrames == 0
                && RequestedTick == 0 && CompletedTick == 0 && StartedTick == 0;
            if (!Actor.IsValid || Result < CharacterTestPositionRecallResult.None || Result > CharacterTestPositionRecallResult.Cancelled) return false;
            if (HasRecord ? RemainingUses != 1 || RecordedTick < 0 || RecordedTick > frame || ExpiresTick <= RecordedTick
                || ExpiresTick - RecordedTick > 3600 || frame != long.MaxValue && frame >= ExpiresTick
                : RemainingUses != 0 || X != 0 || Y != 0 || Z != 0 || RecordedTick != 0 || ExpiresTick != 0) return false;
            if (ExecutionId == 0) return !HasRecord && Operation == 0 && Result == 0 && LifetimeFrames == 0 && RequestedTick == 0 && CompletedTick == -1 && StartedTick == -1;
            return ExecutionId <= long.MaxValue && (Operation == CharacterTestPositionRecallOperation.Record
                ? LifetimeFrames > 0 && LifetimeFrames <= 3600 : Operation == CharacterTestPositionRecallOperation.Return && LifetimeFrames == 0)
                && RequestedTick >= 0 && RequestedTick <= frame && Result != CharacterTestPositionRecallResult.None
                && (Result != CharacterTestPositionRecallResult.Recorded || Operation == CharacterTestPositionRecallOperation.Record)
                && (Result != CharacterTestPositionRecallResult.Returned || Operation == CharacterTestPositionRecallOperation.Return && !HasRecord)
                && (Pending ? CompletedTick == -1 && StartedTick == -1 : CompletedTick >= RequestedTick && CompletedTick <= frame
                && (Succeeded ? StartedTick == CompletedTick : StartedTick == -1));
        }
        internal void AddHash(ref RuntimeHashAccumulator hash)
        {
            hash.AddLong(NetworkRootStateFieldIds.RecallStage, unchecked((long)StageRun));
            hash.AddInt(NetworkRootStateFieldIds.RecallActorIndex, Actor.Index);
            hash.AddInt(NetworkRootStateFieldIds.RecallActorGeneration, Actor.Generation);
            hash.AddInt(NetworkRootStateFieldIds.RecallHasRecord, HasRecord ? 1 : 0);
            hash.AddInt(NetworkRootStateFieldIds.RecallX, X); hash.AddInt(NetworkRootStateFieldIds.RecallY, Y); hash.AddInt(NetworkRootStateFieldIds.RecallZ, Z);
            hash.AddLong(NetworkRootStateFieldIds.RecallRecordedTick, RecordedTick); hash.AddLong(NetworkRootStateFieldIds.RecallExpiresTick, ExpiresTick);
            hash.AddInt(NetworkRootStateFieldIds.RecallRemainingUses, RemainingUses); hash.AddLong(NetworkRootStateFieldIds.RecallExecution, unchecked((long)ExecutionId));
            hash.AddInt(NetworkRootStateFieldIds.RecallOperation, (int)Operation); hash.AddInt(NetworkRootStateFieldIds.RecallResult, (int)Result);
            hash.AddInt(NetworkRootStateFieldIds.RecallLifetime, LifetimeFrames); hash.AddLong(NetworkRootStateFieldIds.RecallRequestedTick, RequestedTick);
            hash.AddLong(NetworkRootStateFieldIds.RecallCompletedTick, CompletedTick); hash.AddLong(NetworkRootStateFieldIds.RecallStartedTick, StartedTick);
        }
    }
}

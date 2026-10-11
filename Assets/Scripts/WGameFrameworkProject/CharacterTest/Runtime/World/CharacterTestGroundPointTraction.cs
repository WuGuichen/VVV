using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestGroundPointTractionEnd
    {
        None = 0, Active = 1, Arrived = 2, Blocked = 3, Cancelled = 4,
        TimedOut = 5, ActorUnavailable = 6, ActorNotFree = 7, GroundLost = 8, Rejected = 9
    }

    /// <summary>Actor-owned, saved transient motion. It never owns the actor's shared velocity.</summary>
    internal struct CharacterTestGroundPointTractionState
    {
        public ulong StageRun { get; set; }
        public ulong ExecutionId { get; set; }
        public GameplayEntityId Actor { get; set; }
        public int AnchorX { get; set; }
        public int AnchorY { get; set; }
        public int AnchorZ { get; set; }
        public int StopMillimeters { get; set; }
        public int MaximumFrames { get; set; }
        public int SpeedMillimetersPerSecond { get; set; }
        public int AccelerationMillimetersPerSecondSquared { get; set; }
        public int CurrentSpeed { get; set; }
        public long AcceptedTick { get; set; }
        public long StartedTick { get; set; }
        public long LastTick { get; set; }
        public CharacterTestGroundPointTractionEnd End { get; set; }
        public bool IsActive => End == CharacterTestGroundPointTractionEnd.Active;
        internal bool Matches(ulong stageRun, ulong executionId) => StageRun == stageRun && ExecutionId == executionId;
        internal void Finish(CharacterTestGroundPointTractionEnd reason, long frame)
        { End = reason; CurrentSpeed = 0; LastTick = frame; }

        internal bool IsValidSnapshot(long snapshotFrame = long.MaxValue)
        {
            if (End == CharacterTestGroundPointTractionEnd.None) return ExecutionId == 0 && StageRun == 0 && CurrentSpeed == 0;
            return End >= CharacterTestGroundPointTractionEnd.Active && End <= CharacterTestGroundPointTractionEnd.Rejected
                && StageRun != 0 && ExecutionId > 0 && ExecutionId <= long.MaxValue && Actor.IsValid
                && StopMillimeters > 0 && MaximumFrames > 0 && MaximumFrames <= 600
                && SpeedMillimetersPerSecond >= 100 && SpeedMillimetersPerSecond <= 20000
                && AccelerationMillimetersPerSecondSquared >= 100 && AccelerationMillimetersPerSecondSquared <= 100000
                && CurrentSpeed >= 0 && CurrentSpeed <= SpeedMillimetersPerSecond
                && (IsActive || CurrentSpeed == 0) && AcceptedTick >= 0 && StartedTick >= -1
                && AcceptedTick <= snapshotFrame && LastTick >= AcceptedTick - 1 && LastTick <= snapshotFrame
                && (StartedTick == -1 || (StartedTick >= AcceptedTick && StartedTick <= LastTick));
        }
        internal void AddHash(ref RuntimeHashAccumulator hash)
        {
            hash.AddLong(NetworkRootStateFieldIds.GroundTractionStageRun, unchecked((long)StageRun));
            hash.AddLong(NetworkRootStateFieldIds.GroundTractionExecutionId, unchecked((long)ExecutionId));
            hash.AddInt(NetworkRootStateFieldIds.GroundTractionActorIndex, Actor.Index);
            hash.AddInt(NetworkRootStateFieldIds.GroundTractionActorGeneration, Actor.Generation);
            hash.AddInt(NetworkRootStateFieldIds.GroundTractionAnchorX, AnchorX);
            hash.AddInt(NetworkRootStateFieldIds.GroundTractionAnchorY, AnchorY);
            hash.AddInt(NetworkRootStateFieldIds.GroundTractionAnchorZ, AnchorZ);
            hash.AddInt(NetworkRootStateFieldIds.GroundTractionStop, StopMillimeters);
            hash.AddInt(NetworkRootStateFieldIds.GroundTractionMaximumFrames, MaximumFrames);
            hash.AddInt(NetworkRootStateFieldIds.GroundTractionSpeed, SpeedMillimetersPerSecond);
            hash.AddInt(NetworkRootStateFieldIds.GroundTractionAcceleration, AccelerationMillimetersPerSecondSquared);
            hash.AddInt(NetworkRootStateFieldIds.GroundTractionCurrentSpeed, CurrentSpeed);
            hash.AddLong(NetworkRootStateFieldIds.GroundTractionAcceptedTick, AcceptedTick);
            hash.AddLong(NetworkRootStateFieldIds.GroundTractionStartedTick, StartedTick);
            hash.AddLong(NetworkRootStateFieldIds.GroundTractionLastTick, LastTick);
            hash.AddInt(NetworkRootStateFieldIds.GroundTractionEnd, (int)End);
        }

        /// <summary>NoAlloc. Bounded horizontal displacement, with no velocity left in other motion channels.</summary>
        internal bool TryStep(int x, int z, int tickRate, long frame, out int dx, out int dz)
        {
            dx = dz = 0;
            if (!IsActive || frame <= LastTick || tickRate <= 0) return false;
            LastTick = frame;
            if (frame - AcceptedTick >= MaximumFrames) { Finish(CharacterTestGroundPointTractionEnd.TimedOut, frame); return false; }
            long deltaX = (long)AnchorX - x, deltaZ = (long)AnchorZ - z;
            if (Math.Abs(deltaX) > int.MaxValue / 2 || Math.Abs(deltaZ) > int.MaxValue / 2)
            { Finish(CharacterTestGroundPointTractionEnd.ActorUnavailable, frame); return false; }
            int distance = CharacterTestDeterministicMath.Magnitude((int)deltaX, (int)deltaZ);
            if (distance <= StopMillimeters + 1) { Finish(CharacterTestGroundPointTractionEnd.Arrived, frame); return false; }
            CurrentSpeed = Math.Min(SpeedMillimetersPerSecond,
                CurrentSpeed + Math.Max(1, AccelerationMillimetersPerSecondSquared / tickRate));
            int step = Math.Min(distance - StopMillimeters, Math.Max(1, CurrentSpeed / tickRate));
            dx = (int)(deltaX * step / distance); dz = (int)(deltaZ * step / distance);
            if (dx == 0 && dz == 0)
            { if (Math.Abs(deltaX) >= Math.Abs(deltaZ)) dx = Math.Sign(deltaX); else dz = Math.Sign(deltaZ); }
            return true;
        }

        internal void CompleteStep(int previousX, int previousZ, int x, int z, int requestedX, int requestedZ,
            bool grounded, long frame)
        {
            if (!IsActive) return;
            long movedX = (long)x - previousX, movedZ = (long)z - previousZ;
            // Require the actual contribution beyond the same motor query without this traction.
            long progress = movedX * requestedX + movedZ * requestedZ;
            if (progress > 0 && StartedTick < 0) StartedTick = frame;
            if (!grounded) { Finish(CharacterTestGroundPointTractionEnd.GroundLost, frame); return; }
            if (CharacterTestDeterministicMath.Magnitude(AnchorX - x, AnchorZ - z) <= StopMillimeters + 1)
            { Finish(CharacterTestGroundPointTractionEnd.Arrived, frame); return; }
            // Stop promptly when a motor cannot execute the requested traction step.
            long requestedSquared = (long)requestedX * requestedX + (long)requestedZ * requestedZ;
            if (progress * 4 < requestedSquared) Finish(CharacterTestGroundPointTractionEnd.Blocked, frame);
        }
    }
}
using MxFramework.Runtime;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class GameSlice
    {
        internal void RestoreDetachedPausedClock(long completedFrame, double elapsed)
        {
            SessionSnapshotCheck.Require(Host.State == RuntimeLifecycleState.Created && completedFrame >= 0 &&
                !double.IsNaN(elapsed) && !double.IsInfinity(elapsed) && elapsed >= 0, "detached-clock");
            RestoreRuntimeState(new RuntimeFrame(checked(completedFrame + 1)), elapsed);
        }

        // Commit preparation can fail while the previous aggregate is still current and time is detached.
        internal void StartDetachedPausedHost()
        {
            SessionSnapshotCheck.Require(Services.Modules.MixedPlayerTacticalStage.PausedWorldTime.IsDetached,
                "detached-time-required");
            _composition.InitializeAndStartHost();
        }
    }
}

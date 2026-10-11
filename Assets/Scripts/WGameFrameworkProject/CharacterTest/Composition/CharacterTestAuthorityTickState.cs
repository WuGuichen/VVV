using System;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    // A failed authority attempt is terminal for this session. Recovery creates a new owner.
    internal sealed class CharacterTestAuthorityTickState
    {
        internal bool IsSuspended => Failure != null;
        internal Exception Failure { get; private set; }
        internal RuntimeFrame FailedFrame { get; private set; }

        internal void Suspend(RuntimeFrame frame, Exception failure)
        {
            if (failure == null) throw new ArgumentNullException(nameof(failure));
            if (IsSuspended) return;
            FailedFrame = frame;
            Failure = failure;
        }

        internal void ThrowIfSuspended()
        {
            if (IsSuspended)
                throw new InvalidOperationException(
                    "Authority session is suspended after a failed tick. Dispose and explicitly rebuild the session.",
                    Failure);
        }
    }
}

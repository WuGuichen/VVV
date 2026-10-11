using System;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestAttackWindowObservationTests
    {
        private static CharacterTestActiveHitTrace Trace(int window, long instance = 10, int target = 2)
            => new CharacterTestActiveHitTrace(target, new CharacterActionId(301), instance, 1, window,
                0, 0, default, default, default, window, 0);

        [Test] public void CaptureUsesOnlyCurrentTargetAndCurrentInstance()
            => CollectionAssert.AreEqual(new[] { 1 }, CharacterTestAttackWindowObservation.Capture(
                new[] { Trace(1), Trace(2, 11), Trace(3, 10, 3) }, 2, 10));

        [Test] public void EmptyCaptureIsImmediatelySatisfied()
            => Assert.IsFalse(CharacterTestAttackWindowObservation.AnyCapturedActive(
                new[] { Trace(4) }, 2, 10, Array.Empty<int>()));

        [Test] public void CapturedWindowRemainsPendingWhileActive()
            => Assert.IsTrue(CharacterTestAttackWindowObservation.AnyCapturedActive(new[] { Trace(1) }, 2, 10, new[] { 1 }));

        [Test] public void EndedCurrentAttackDoesNotWaitForSeamlessNextAttack()
            => Assert.IsFalse(CharacterTestAttackWindowObservation.AnyCapturedActive(new[] { Trace(2, 11) }, 2, 10, new[] { 1 }));

        [Test] public void LaterWindowInSameActionDoesNotExtendCapturedSet()
            => Assert.IsFalse(CharacterTestAttackWindowObservation.AnyCapturedActive(new[] { Trace(2) }, 2, 10, new[] { 1 }));

        [Test] public void OverlappingCapturedWindowsRemainPendingUntilAllEnd()
        {
            Assert.IsTrue(CharacterTestAttackWindowObservation.AnyCapturedActive(new[] { Trace(2) }, 2, 10, new[] { 1, 2 }));
            Assert.IsFalse(CharacterTestAttackWindowObservation.AnyCapturedActive(Array.Empty<CharacterTestActiveHitTrace>(), 2, 10, new[] { 1, 2 }));
        }

        [Test] public void OtherTargetCannotProlongWait()
            => Assert.IsFalse(CharacterTestAttackWindowObservation.AnyCapturedActive(new[] { Trace(1, 10, 3) }, 2, 10, new[] { 1 }));

        [Test] public void SameWindowNumberInOtherInstanceCannotProlongWait()
            => Assert.IsFalse(CharacterTestAttackWindowObservation.AnyCapturedActive(new[] { Trace(1, 11) }, 2, 10, new[] { 1 }));

        [Test] public void ObservationDoesNotMutateCapture()
        {
            var captured = new[] { 1, 2 };
            CharacterTestAttackWindowObservation.AnyCapturedActive(new[] { Trace(2) }, 2, 10, captured);
            CollectionAssert.AreEqual(new[] { 1, 2 }, captured);
        }
    }
}

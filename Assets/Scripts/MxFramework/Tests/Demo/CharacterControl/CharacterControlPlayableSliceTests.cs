using System;
using MxFramework.CharacterControl;
using MxFramework.CharacterControl.Animation;
using MxFramework.Core.Math;
using MxFramework.Demo.CharacterControl;
using NUnit.Framework;

namespace MxFramework.Tests.Demo.CharacterControl
{
    public sealed class CharacterControlPlayableSliceTests
    {
        [Test]
        public void LocalInput_MoveThenAttack_ProducesMotionActionAndDiagnostics()
        {
            using var slice = new CharacterControlPlayableSlice();

            slice.EnqueueMove(1f, 0f);
            CharacterControlPlayableSnapshot moved = slice.Tick();
            slice.EnqueueAttack();
            CharacterControlPlayableSnapshot attacked = slice.Tick();

            Assert.Greater(moved.Position.X.RawValue, Fix64.Zero.RawValue);
            Assert.AreEqual(CharacterControlPlayableSlice.LocalInputSourceId, attacked.LastCommand.SourceId);
            Assert.AreEqual(CharacterActionKind.Attack, attacked.LastCommand.ActionRequest.Kind);
            Assert.AreEqual(CharacterControlPlayableSlice.LightAttackId, attacked.LastCommand.ActionRequest.CombatActionId);
            Assert.AreEqual(CharacterControlState.Action, attacked.State);
            Assert.IsTrue(attacked.LastActionResult.Success, attacked.LastActionResult.Message);
            Assert.That(attacked.DebugReport, Does.Contain("Action"));
        }

        [Test]
        public void LocalInput_Jump_UsesMotionResolverImpulse()
        {
            using var slice = new CharacterControlPlayableSlice();

            slice.EnqueueJump();
            CharacterControlPlayableSnapshot jumped = slice.Tick();

            Assert.IsTrue(jumped.LastCommand.JumpPressed);
            Assert.IsTrue(jumped.LastMotion.JumpStarted);
            Assert.Greater(jumped.Velocity.Y.RawValue, Fix64.Zero.RawValue);
        }

        [Test]
        public void RuntimeAiPlannerCommandSource_EmitsAttackThroughRuntimeHostLoop()
        {
            using var slice = new CharacterControlPlayableSlice();

            slice.EnqueueRuntimeAiStep();
            CharacterControlPlayableSnapshot snapshot = slice.Tick();

            Assert.AreEqual(CharacterControlPlayableSlice.RuntimeAiSourceId, snapshot.LastCommand.SourceId);
            Assert.AreEqual("Runtime AI Planner", snapshot.LastCommandSource);
            Assert.AreEqual(CharacterActionKind.Attack, snapshot.LastCommand.ActionRequest.Kind);
            Assert.AreEqual(CharacterControlPlayableSlice.LightAttackId, snapshot.LastCommand.ActionRequest.CombatActionId);
            Assert.AreEqual(CharacterControlState.Action, snapshot.State);
        }

        [Test]
        public void RuntimeAiPlannerCommandSource_RepeatedSteps_EmitActionRequests()
        {
            using var slice = new CharacterControlPlayableSlice();

            slice.EnqueueRuntimeAiStep();
            CharacterControlPlayableSnapshot first = slice.Tick();
            slice.Tick(6);
            slice.EnqueueRuntimeAiStep();
            CharacterControlPlayableSnapshot second = slice.Tick();

            Assert.AreEqual(CharacterActionKind.Attack, first.LastCommand.ActionRequest.Kind);
            Assert.AreEqual(CharacterControlPlayableSlice.RuntimeAiSourceId, second.LastCommand.SourceId);
            Assert.AreEqual(CharacterActionKind.Attack, second.LastCommand.ActionRequest.Kind);
            Assert.AreEqual(CharacterControlPlayableSlice.LightAttackId, second.LastCommand.ActionRequest.CombatActionId);
            Assert.AreEqual(CharacterControlState.Action, second.State);
            Assert.IsTrue(second.LastActionResult.Success, second.LastActionResult.Message);
            Assert.AreNotEqual(first.LastCommand.TraceId, second.LastCommand.TraceId);
        }

        [Test]
        public void PressureBreak_TransitionsToReactionThenExpiresWithDebugSnapshot()
        {
            using var slice = new CharacterControlPlayableSlice();

            slice.EnqueuePressureBreak();
            CharacterControlPlayableSnapshot broken = slice.Tick();
            CharacterControlPlayableSnapshot recovered = slice.Tick(4);

            Assert.AreEqual(CharacterControlState.Reaction, broken.State);
            Assert.AreEqual(CharacterPressureReactionKind.PostureBreak, broken.LastPressureResult.Kind);
            Assert.IsTrue(broken.LastPressureResult.ReactionStarted);
            Assert.AreEqual(CharacterAnimationPresentationEventKind.ReactionCrossFade, broken.LastAnimation.EventKind);
            Assert.That(broken.DebugReport, Does.Contain("Pressure"));
            Assert.AreEqual(CharacterControlState.Locomotion, recovered.State);
            Assert.IsTrue(recovered.LastPressureResult.ReactionFinished);
        }

        [Test]
        public void ReplayAndHash_AreStableForSameScriptedSequence()
        {
            CharacterControlPlayableSnapshot first = RunScriptedSequence();
            CharacterControlPlayableSnapshot second = RunScriptedSequence();

            Assert.AreEqual(first.RuntimeHash, second.RuntimeHash);
            Assert.AreEqual(first.ReplayFrameCount, second.ReplayFrameCount);
        }

        [Test]
        public void ReplayDiagnostics_RecordCurrentFrame()
        {
            using var slice = new CharacterControlPlayableSlice();

            slice.Tick(3);

            Assert.AreEqual(3, slice.ReplaySnapshot.Count);
            Assert.That(slice.ReplaySnapshot.Records[2].DiagnosticsSummary, Does.Contain("hashFrame=2"));
        }

        [Test]
        public void ResetAll_AfterDispose_ThrowsObjectDisposed()
        {
            var slice = new CharacterControlPlayableSlice();
            slice.Dispose();

            Assert.Throws<ObjectDisposedException>(() => slice.ResetAll());
            Assert.Throws<ObjectDisposedException>(() => slice.Tick());
        }

        private static CharacterControlPlayableSnapshot RunScriptedSequence()
        {
            using var slice = new CharacterControlPlayableSlice();
            slice.EnqueueMove(1f, 0f);
            slice.Tick();
            slice.EnqueueAttack();
            slice.Tick();
            slice.EnqueuePressureBreak();
            return slice.Tick();
        }
    }
}

using System;
using MxFramework.CharacterControl;
using MxFramework.CharacterControl.Animation;
using MxFramework.Core.Math;
using MxFramework.Demo;
using MxFramework.Demo.CharacterControl;
using MxFramework.Runtime;
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
        public void RuntimeCombatSlice_RuntimeAiPlannerAttack_UsesFormalCharacterActionAndFinishes()
        {
            const int combatActionId = 7101;
            using var slice = new RuntimeCombatCharacterControlSlice(combatActionId);
            var startFrame = new RuntimeFrame(1);

            Assert.IsTrue(slice.TryReadRuntimeAiPlannerCommand(startFrame, out CharacterCommand command));
            slice.RecordCommand(command);
            CharacterActionResult started = slice.SubmitAction(command.ActionRequest);

            Assert.IsTrue(started.Success, started.Message);
            Assert.AreEqual(combatActionId, command.ActionRequest.CombatActionId);
            Assert.AreEqual(CharacterControlState.Action, slice.StateMachine.CurrentState);

            for (int frame = 1; frame <= 6; frame++)
                slice.TickRuntime(new RuntimeFrame(frame));

            Assert.AreEqual(CharacterControlState.Locomotion, slice.StateMachine.CurrentState);
            StringAssert.Contains("runtimeTicks=6", slice.BuildSummary());
            string debugReport = slice.BuildDebugReport();
            StringAssert.Contains("lastActionInstanceId: 1", debugReport);
            StringAssert.Contains("lastEventType: Finished", debugReport);
        }

        [Test]
        public void PressureBreak_StartsFormalReactionAndClearsPressureWindowAfterActionEnds()
        {
            using var slice = new CharacterControlPlayableSlice();

            slice.EnqueuePressureBreak();
            CharacterControlPlayableSnapshot broken = slice.Tick();
            CharacterControlPlayableSnapshot recovered = slice.Tick(4);

            Assert.AreEqual(CharacterControlState.Reaction, broken.State);
            Assert.AreEqual(CharacterPressureReactionKind.CohesionBreak, broken.LastPressureResult.Kind);
            Assert.IsTrue(
                broken.LastPressureResult.ReactionStarted,
                "Pressure controller should still own the independent control-state reaction window.");
            Assert.AreEqual(CharacterActionEventType.Started, broken.LastActionEvent.Type);
            Assert.Greater(broken.LastActionEvent.ActionInstanceId, 0);
            Assert.AreEqual(
                CharacterAnimationPresentationEventKind.ActionHandledByCombatBridge,
                broken.LastAnimation.EventKind);
            Assert.That(broken.DebugReport, Does.Contain("Pressure"));
            Assert.AreEqual(CharacterControlState.Locomotion, recovered.State);
            Assert.AreEqual(CharacterPressureReactionKind.CohesionBreak, recovered.LastPressureResult.Kind);
            Assert.IsFalse(
                recovered.LastPressureResult.ReactionFinished,
                "Formal CharacterAction already ended the control-state reaction; pressure only clears its own window.");
            StringAssert.Contains("already left Reaction state", recovered.LastPressureResult.Message);
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

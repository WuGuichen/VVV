using System.Collections.Generic;
using MxFramework.CharacterControl;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterControlStateMachineTests
    {
        [Test]
        public void TransitionTable_CoversActionReactionDisabledRestore()
        {
            var machine = new CharacterControlStateMachine();

            AssertTransition(machine.BeginAction(RuntimeFrame.Zero), CharacterControlState.Action, version: 1);
            AssertTransition(machine.FinishAction(new RuntimeFrame(1)), CharacterControlState.Locomotion, version: 2);
            AssertTransition(machine.BeginAction(new RuntimeFrame(2)), CharacterControlState.Action, version: 3);
            AssertTransition(machine.ApplyPressureBreak(new RuntimeFrame(3)), CharacterControlState.Reaction, version: 4);
            AssertTransition(machine.FinishReaction(new RuntimeFrame(4)), CharacterControlState.Locomotion, version: 5);
            AssertTransition(machine.Disable(new RuntimeFrame(5), CharacterControlTransitionReason.Death), CharacterControlState.Disabled, version: 6);
            AssertTransition(machine.RestoreLocomotion(new RuntimeFrame(6)), CharacterControlState.Locomotion, version: 7);
        }

        [Test]
        public void Disabled_RejectsActionUntilExplicitRestore()
        {
            var machine = new CharacterControlStateMachine();
            machine.Disable(RuntimeFrame.Zero, CharacterControlTransitionReason.Cutscene);
            var events = new List<CharacterControlEvent>();
            machine.ControlEvent += events.Add;

            CharacterControlTransitionResult rejected = machine.BeginAction(new RuntimeFrame(1));

            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(CharacterControlState.Action, rejected.CurrentState);
            Assert.AreEqual(CharacterControlState.Disabled, rejected.PreviousState);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(CharacterControlEventType.TransitionRejected, events[0].Type);
            Assert.AreEqual(1, machine.Version);
        }

        [Test]
        public void ReenterSameState_DoesNotIncrementVersionOrEmitStateChanged()
        {
            var machine = new CharacterControlStateMachine();
            int changed = 0;
            machine.StateChanged += _ => changed++;

            machine.BeginAction(RuntimeFrame.Zero);
            CharacterControlTransitionResult same = machine.BeginAction(new RuntimeFrame(1));

            Assert.IsTrue(same.Success);
            Assert.IsFalse(same.Changed);
            Assert.AreEqual(1, machine.Version);
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void Events_ArePublishedInStateThenControlOrder()
        {
            var machine = new CharacterControlStateMachine();
            var order = new List<string>();
            machine.StateChanged += evt => order.Add("state:" + evt.CurrentState);
            machine.ControlEvent += evt => order.Add("control:" + evt.Type);

            machine.BeginReaction(RuntimeFrame.Zero);

            CollectionAssert.AreEqual(new[] { "state:Reaction", "control:StateChanged" }, order);
            Assert.AreEqual(CharacterControlLockMask.Move | CharacterControlLockMask.Jump | CharacterControlLockMask.Action, machine.ControlLockMask);
        }

        [Test]
        public void SetControlLockMask_EmitsLockChangedWithoutChangingVersion()
        {
            var machine = new CharacterControlStateMachine();
            var events = new List<CharacterControlEvent>();
            machine.ControlEvent += events.Add;

            bool changed = machine.SetControlLockMask(CharacterControlLockMask.Move, RuntimeFrame.Zero, "rooted");

            Assert.IsTrue(changed);
            Assert.AreEqual(0, machine.Version);
            Assert.AreEqual(CharacterControlEventType.LockChanged, events[0].Type);
            Assert.AreEqual(CharacterControlLockMask.Move, machine.ControlLockMask);
        }

        private static void AssertTransition(CharacterControlTransitionResult result, CharacterControlState expectedState, int version)
        {
            Assert.IsTrue(result.Success, result.Message);
            Assert.IsTrue(result.Changed);
            Assert.AreEqual(expectedState, result.CurrentState);
            Assert.AreEqual(version, result.Version);
        }
    }
}

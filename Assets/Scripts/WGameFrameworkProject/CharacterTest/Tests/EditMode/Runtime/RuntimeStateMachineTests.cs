using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeStateMachineTests
    {
        [Test]
        public void DefaultConstructor_UsesDefaultCurrentState()
        {
            var machine = new RuntimeStateMachine<int>();

            Assert.AreEqual(default(int), machine.Current);
            Assert.AreEqual(0, machine.Version);
            Assert.AreEqual(string.Empty, machine.LastTransitionReason);
        }

        [Test]
        public void TryTransition_UpdatesCurrentReasonAndVersion()
        {
            var machine = new RuntimeStateMachine<TestState>(TestState.Idle);

            bool transitioned = machine.TryTransition(TestState.Running, "start");

            Assert.IsTrue(transitioned);
            Assert.AreEqual(TestState.Running, machine.Current);
            Assert.AreEqual("start", machine.LastTransitionReason);
            Assert.AreEqual(1, machine.Version);
        }

        [Test]
        public void TryTransition_DefaultRulesAllowAllStateChanges()
        {
            var machine = new RuntimeStateMachine<TestState>(TestState.Idle);

            Assert.IsTrue(machine.CanTransition(TestState.Complete));
            Assert.IsTrue(machine.TryTransition(TestState.Complete, "skip"));
            Assert.AreEqual(TestState.Complete, machine.Current);
        }

        [Test]
        public void TryTransition_PredicateCanBlockTransition()
        {
            var machine = new RuntimeStateMachine<TestState>(
                TestState.Idle,
                (current, next, reason) => current == TestState.Idle && next == TestState.Running);

            Assert.IsFalse(machine.CanTransition(TestState.Complete, "invalid"));
            Assert.IsFalse(machine.TryTransition(TestState.Complete, "invalid"));

            Assert.AreEqual(TestState.Idle, machine.Current);
            Assert.AreEqual(0, machine.Version);
            Assert.AreEqual(string.Empty, machine.LastTransitionReason);
        }

        [Test]
        public void TryTransition_SameStateDoesNotIncrementVersion()
        {
            var machine = new RuntimeStateMachine<TestState>(TestState.Idle);

            Assert.IsTrue(machine.TryTransition(TestState.Idle, "same"));

            Assert.AreEqual(TestState.Idle, machine.Current);
            Assert.AreEqual(0, machine.Version);
            Assert.AreEqual(string.Empty, machine.LastTransitionReason);
        }

        [Test]
        public void Force_BypassesPredicateAndStoresReason()
        {
            var machine = new RuntimeStateMachine<TestState>(
                TestState.Idle,
                (current, next, reason) => false);

            machine.Force(TestState.Complete, "override");

            Assert.AreEqual(TestState.Complete, machine.Current);
            Assert.AreEqual("override", machine.LastTransitionReason);
            Assert.AreEqual(1, machine.Version);
        }

        [Test]
        public void RuntimeStateMachine_HasNoAppFlowDependency()
        {
            var machineType = typeof(RuntimeStateMachine<TestState>);

            Assert.AreEqual("MxFramework.Runtime", machineType.Namespace);
            Assert.IsFalse(machineType.FullName.Contains("AppFlow"));
        }

        private enum TestState
        {
            Idle,
            Running,
            Complete
        }
    }
}

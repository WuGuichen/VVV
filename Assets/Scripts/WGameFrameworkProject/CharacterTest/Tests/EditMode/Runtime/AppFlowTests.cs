using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class AppFlowTests
    {
        [Test]
        public void RegisterState_StoresStateAndRejectsDuplicateStateId()
        {
            var controller = new AppFlowController();
            controller.RegisterState(new RecordingState("boot", new List<string>()));

            AppFlowSnapshot snapshot = controller.CaptureSnapshot();

            CollectionAssert.AreEqual(new[] { "boot" }, snapshot.RegisteredStateIds);
            Assert.Throws<System.InvalidOperationException>(() => controller.RegisterState(new RecordingState("boot", new List<string>())));
        }

        [Test]
        public void Start_ImmediatelyEntersInitialState()
        {
            var calls = new List<string>();
            var controller = new AppFlowController();
            controller.RegisterState(new RecordingState("boot", calls));

            AppFlowTransitionResult result = controller.Start("boot", "test-start");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(string.Empty, result.Transition.FromStateId);
            Assert.AreEqual("boot", result.Transition.ToStateId);
            Assert.AreEqual("test-start", result.Transition.Reason);
            AppFlowSnapshot snapshot = controller.CaptureSnapshot();
            Assert.IsTrue(snapshot.IsStarted);
            Assert.AreEqual("boot", snapshot.CurrentStateId);
            CollectionAssert.AreEqual(new[] { "enter:boot::boot:test-start" }, calls);
        }

        [Test]
        public void Start_RejectsUnregisteredInitialState()
        {
            var controller = new AppFlowController();

            AppFlowTransitionResult result = controller.Start("missing", "bad-start");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(AppFlowTransitionErrorCode.StateNotRegistered, result.ErrorCode);
            AppFlowSnapshot snapshot = controller.CaptureSnapshot();
            Assert.IsFalse(snapshot.IsStarted);
            Assert.AreSame(result, snapshot.LastResult);
        }

        [Test]
        public void RequestTransition_RegistersPendingTransitionUntilNextTick()
        {
            var calls = new List<string>();
            var controller = new AppFlowController();
            controller.RegisterState(new RecordingState("boot", calls));
            controller.RegisterState(new RecordingState("menu", calls));
            controller.Start("boot");
            calls.Clear();

            AppFlowTransitionResult result = controller.RequestTransition("menu", "boot-complete");

            Assert.IsTrue(result.Success);
            AppFlowSnapshot pending = controller.CaptureSnapshot();
            Assert.AreEqual("boot", pending.CurrentStateId);
            Assert.IsTrue(pending.HasPendingTransition);
            Assert.AreEqual("menu", pending.PendingStateId);
            CollectionAssert.IsEmpty(calls);

            controller.Tick(10, 0.016d, 0.16d);

            AppFlowSnapshot applied = controller.CaptureSnapshot();
            Assert.AreEqual("menu", applied.CurrentStateId);
            Assert.IsFalse(applied.HasPendingTransition);
            Assert.AreEqual(1, applied.TickCount);
            CollectionAssert.AreEqual(
                new[]
                {
                    "exit:boot:boot:menu:boot-complete",
                    "enter:menu:boot:menu:boot-complete",
                    "tick:menu:10"
                },
                calls);
        }

        [Test]
        public void StateTickRequestedTransition_AppliesOnFollowingTick()
        {
            var calls = new List<string>();
            var controller = new AppFlowController();
            controller.RegisterState(new RequestingState("boot", calls, "menu"));
            controller.RegisterState(new RecordingState("menu", calls));
            controller.Start("boot");
            calls.Clear();

            controller.Tick(1, 0.016d, 0.016d);

            AppFlowSnapshot firstTick = controller.CaptureSnapshot();
            Assert.AreEqual("boot", firstTick.CurrentStateId);
            Assert.IsTrue(firstTick.HasPendingTransition);
            Assert.AreEqual("menu", firstTick.PendingStateId);
            CollectionAssert.AreEqual(new[] { "tick:boot:1", "request:boot:menu:True" }, calls);

            controller.Tick(2, 0.016d, 0.032d);

            AppFlowSnapshot secondTick = controller.CaptureSnapshot();
            Assert.AreEqual("menu", secondTick.CurrentStateId);
            Assert.IsFalse(secondTick.HasPendingTransition);
            CollectionAssert.AreEqual(
                new[]
                {
                    "tick:boot:1",
                    "request:boot:menu:True",
                    "exit:boot:boot:menu:from-tick",
                    "enter:menu:boot:menu:from-tick",
                    "tick:menu:2"
                },
                calls);
        }

        [Test]
        public void RequestTransition_RejectsUnregisteredState()
        {
            var controller = new AppFlowController();
            controller.RegisterState(new RecordingState("boot", new List<string>()));
            controller.Start("boot");

            AppFlowTransitionResult result = controller.RequestTransition("missing", "bad-target");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(AppFlowTransitionErrorCode.StateNotRegistered, result.ErrorCode);
            AppFlowSnapshot snapshot = controller.CaptureSnapshot();
            Assert.AreEqual("boot", snapshot.CurrentStateId);
            Assert.IsFalse(snapshot.HasPendingTransition);
            Assert.AreSame(result, snapshot.LastResult);
        }

        [Test]
        public void TickBeforeStart_RecordsDiagnosticFailure()
        {
            var controller = new AppFlowController();
            controller.RegisterState(new RecordingState("boot", new List<string>()));

            controller.Tick(3, 0.02d, 0.06d);

            AppFlowSnapshot snapshot = controller.CaptureSnapshot();
            Assert.IsFalse(snapshot.IsStarted);
            Assert.AreEqual(0, snapshot.TickCount);
            Assert.AreEqual(3, snapshot.LastFrameIndex);
            Assert.AreEqual(AppFlowTransitionErrorCode.NotStarted, snapshot.LastResult.ErrorCode);
        }

        [Test]
        public void RequestTransition_RejectsSecondPendingTransition()
        {
            var controller = new AppFlowController();
            controller.RegisterState(new RecordingState("boot", new List<string>()));
            controller.RegisterState(new RecordingState("menu", new List<string>()));
            controller.RegisterState(new RecordingState("gameplay", new List<string>()));
            controller.Start("boot");

            AppFlowTransitionResult first = controller.RequestTransition("menu");
            AppFlowTransitionResult second = controller.RequestTransition("gameplay");

            Assert.IsTrue(first.Success);
            Assert.IsFalse(second.Success);
            Assert.AreEqual(AppFlowTransitionErrorCode.PendingTransitionExists, second.ErrorCode);
            Assert.AreEqual("menu", controller.CaptureSnapshot().PendingStateId);
        }

        [Test]
        public void RuntimeModule_TickAdvancesControllerThroughRuntimeHost()
        {
            var calls = new List<string>();
            var controller = new AppFlowController();
            controller.RegisterState(new RecordingState("boot", calls));
            controller.RegisterState(new RecordingState("menu", calls));
            controller.Start("boot");
            controller.RequestTransition("menu", "host-tick");
            calls.Clear();

            var host = new RuntimeHost();
            host.RegisterModule(new AppFlowRuntimeModule(controller));
            host.Initialize();
            host.Start();
            host.Tick(4, 0.02d, 0.08d);

            AppFlowSnapshot snapshot = controller.CaptureSnapshot();
            Assert.AreEqual("menu", snapshot.CurrentStateId);
            Assert.AreEqual(1, snapshot.TickCount);
            CollectionAssert.AreEqual(
                new[]
                {
                    "exit:boot:boot:menu:host-tick",
                    "enter:menu:boot:menu:host-tick",
                    "tick:menu:4"
                },
                calls);
        }

        private class RecordingState : IAppFlowState
        {
            protected readonly List<string> Calls;

            public RecordingState(string stateId, List<string> calls)
            {
                StateId = stateId;
                Calls = calls;
            }

            public string StateId { get; }

            public virtual void Enter(AppFlowStateContext context, AppFlowTransition transition)
            {
                Calls.Add("enter:" + StateId + ":" + transition.FromStateId + ":" + transition.ToStateId + ":" + transition.Reason);
            }

            public virtual void Tick(AppFlowTickContext context)
            {
                Calls.Add("tick:" + StateId + ":" + context.FrameIndex);
            }

            public virtual void Exit(AppFlowStateContext context, AppFlowTransition transition)
            {
                Calls.Add("exit:" + StateId + ":" + transition.FromStateId + ":" + transition.ToStateId + ":" + transition.Reason);
            }
        }

        private sealed class RequestingState : RecordingState
        {
            private readonly string _targetStateId;

            public RequestingState(string stateId, List<string> calls, string targetStateId)
                : base(stateId, calls)
            {
                _targetStateId = targetStateId;
            }

            public override void Enter(AppFlowStateContext context, AppFlowTransition transition)
            {
            }

            public override void Tick(AppFlowTickContext context)
            {
                base.Tick(context);
                AppFlowTransitionResult result = context.RequestTransition(_targetStateId, "from-tick");
                Calls.Add("request:" + StateId + ":" + _targetStateId + ":" + result.Success);
            }
        }
    }
}

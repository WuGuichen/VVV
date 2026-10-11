using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Animation
{
    public class CombatActionRunnerTests
    {
        [Test]
        public void Registry_RegistersQueriesAndUnregistersTimeline()
        {
            var registry = new CombatActionRegistry();
            CombatActionTimeline timeline = QuickAttack();

            registry.RegisterTimeline(timeline.ActionId, timeline);

            Assert.IsTrue(registry.TryGetTimeline(timeline.ActionId, out CombatActionTimeline found));
            Assert.AreSame(timeline, found);
            Assert.IsTrue(registry.UnregisterTimeline(timeline.ActionId));
            Assert.IsFalse(registry.TryGetTimeline(timeline.ActionId, out _));
        }

        [Test]
        public void StartAndTick_AdvancesLocalFramePhaseAndFinish()
        {
            CombatActionRunner runner = CreateRunner(out _);
            var phases = new List<ActionPhaseChangedEvent>();
            var finished = new List<ActionFinishedEvent>();
            runner.ActionPhaseChanged += phases.Add;
            runner.ActionFinished += finished.Add;
            var entity = new CombatEntityId(7);

            ActionResult result = runner.StartAction(entity, 1001, CombatFrame.Zero);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.ActionInstanceId);
            Assert.AreEqual(CombatActionPhase.Startup, runner.GetCurrentPhase(entity));

            runner.TickActions(new CombatFrame(1));
            Assert.AreEqual(1, runner.GetActionState(entity).Value.LocalFrame);
            Assert.AreEqual(CombatActionPhase.Startup, runner.GetCurrentPhase(entity));

            runner.TickActions(new CombatFrame(2));
            Assert.AreEqual(CombatActionPhase.Active, runner.GetCurrentPhase(entity));
            Assert.AreEqual(CombatActionPhase.Startup, phases[0].OldPhase);
            Assert.AreEqual(CombatActionPhase.Active, phases[0].NewPhase);

            runner.TickActions(new CombatFrame(4));
            Assert.AreEqual(CombatActionPhase.Active, runner.GetCurrentPhase(entity));

            runner.TickActions(new CombatFrame(5));
            Assert.AreEqual(CombatActionPhase.Recovery, runner.GetCurrentPhase(entity));

            runner.TickActions(new CombatFrame(6));
            Assert.AreEqual(1, finished.Count);
            Assert.IsNull(runner.GetActionState(entity));
            Assert.AreEqual(CombatActionPhase.None, runner.GetCurrentPhase(entity));
        }

        [Test]
        public void StartAction_PublishesFrameZeroEventsAfterStarted()
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(5005, TimelineWithEvents(
                5005,
                4,
                new[]
                {
                    new CombatActionFrameEvent(0, 100, sourceOrder: 2),
                    new CombatActionFrameEvent(0, 50, sourceOrder: 1, intPayload: 7),
                }));
            var runner = new CombatActionRunner(registry);
            var order = new List<string>();
            var raised = new List<ActionFrameEventRaisedEvent>();
            var entity = new CombatEntityId(21);
            runner.ActionStarted += evt => order.Add("started:" + evt.ActionId);
            runner.ActionFrameEventRaised += evt =>
            {
                order.Add("frame:" + evt.FrameEvent.EventId);
                raised.Add(evt);
            };

            ActionResult result = runner.StartAction(entity, 5005, new CombatFrame(10));

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(new[] { "started:5005", "frame:50", "frame:100" }, order);
            Assert.AreEqual(2, raised.Count);
            Assert.AreEqual(entity, raised[0].EntityId);
            Assert.AreEqual(5005, raised[0].ActionId);
            Assert.AreEqual(result.ActionInstanceId, raised[0].ActionInstanceId);
            Assert.AreEqual(new CombatFrame(10), raised[0].WorldFrame);
            Assert.AreEqual(0, raised[0].LocalFrame);
            Assert.AreEqual(new CombatActionFrameEvent(0, 50, sourceOrder: 1, intPayload: 7), raised[0].FrameEvent);
        }

        [Test]
        public void TickActions_PublishesFrameEventsForAdvancedLocalFrame()
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(5101, TimelineWithEvents(
                5101,
                5,
                new[]
                {
                    new CombatActionFrameEvent(1, 11),
                    new CombatActionFrameEvent(2, 12),
                }));
            var runner = new CombatActionRunner(registry);
            var raised = new List<ActionFrameEventRaisedEvent>();
            var entity = new CombatEntityId(22);
            runner.ActionFrameEventRaised += raised.Add;

            ActionResult result = runner.StartAction(entity, 5101, new CombatFrame(4));
            runner.TickActions(new CombatFrame(5));
            runner.TickActions(new CombatFrame(6));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, raised.Count);
            Assert.AreEqual(entity, raised[0].EntityId);
            Assert.AreEqual(5101, raised[0].ActionId);
            Assert.AreEqual(result.ActionInstanceId, raised[0].ActionInstanceId);
            Assert.AreEqual(new CombatFrame(5), raised[0].WorldFrame);
            Assert.AreEqual(1, raised[0].LocalFrame);
            Assert.AreEqual(new CombatActionFrameEvent(1, 11), raised[0].FrameEvent);
            Assert.AreEqual(new CombatFrame(6), raised[1].WorldFrame);
            Assert.AreEqual(2, raised[1].LocalFrame);
            Assert.AreEqual(new CombatActionFrameEvent(2, 12), raised[1].FrameEvent);
        }

        [Test]
        public void TickActions_PublishesFrameEventsInDeterministicEntityOrder()
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(5201, TimelineWithEvents(
                5201,
                4,
                new[] { new CombatActionFrameEvent(1, 1) }));
            var runner = new CombatActionRunner(registry);
            var raised = new List<ActionFrameEventRaisedEvent>();
            var highEntity = new CombatEntityId(30);
            var lowEntity = new CombatEntityId(3);
            runner.ActionFrameEventRaised += raised.Add;

            runner.StartAction(highEntity, 5201, CombatFrame.Zero);
            runner.StartAction(lowEntity, 5201, CombatFrame.Zero);
            runner.TickActions(new CombatFrame(1));

            Assert.AreEqual(2, raised.Count);
            Assert.AreEqual(lowEntity, raised[0].EntityId);
            Assert.AreEqual(highEntity, raised[1].EntityId);
        }

        [Test]
        public void TickActions_PublishesSameFrameEventsUsingTimelineSortOrder()
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(5301, TimelineWithEvents(
                5301,
                4,
                new[]
                {
                    new CombatActionFrameEvent(1, 20, sourceOrder: 2),
                    new CombatActionFrameEvent(1, 10, sourceOrder: 1),
                    new CombatActionFrameEvent(1, 5, sourceOrder: 1),
                }));
            var runner = new CombatActionRunner(registry);
            var raised = new List<ActionFrameEventRaisedEvent>();
            runner.ActionFrameEventRaised += raised.Add;

            runner.StartAction(new CombatEntityId(23), 5301, CombatFrame.Zero);
            runner.TickActions(new CombatFrame(1));

            Assert.AreEqual(3, raised.Count);
            Assert.AreEqual(5, raised[0].FrameEvent.EventId);
            Assert.AreEqual(10, raised[1].FrameEvent.EventId);
            Assert.AreEqual(20, raised[2].FrameEvent.EventId);
        }

        [Test]
        public void ForceCanceledAction_DoesNotPublishSubsequentFrameEvents()
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(5401, TimelineWithEvents(
                5401,
                5,
                new[]
                {
                    new CombatActionFrameEvent(1, 101),
                    new CombatActionFrameEvent(2, 102),
                }));
            var runner = new CombatActionRunner(registry);
            var raised = new List<ActionFrameEventRaisedEvent>();
            var entity = new CombatEntityId(24);
            runner.ActionFrameEventRaised += raised.Add;

            runner.StartAction(entity, 5401, CombatFrame.Zero);
            runner.TickActions(new CombatFrame(1));
            bool canceled = runner.ForceCancel(entity);
            runner.TickActions(new CombatFrame(2));

            Assert.IsTrue(canceled);
            Assert.AreEqual(1, raised.Count);
            Assert.AreEqual(101, raised[0].FrameEvent.EventId);
            Assert.IsNull(runner.GetActionState(entity));
        }

        [Test]
        public void FinishedAction_DoesNotPublishFrameEventsPastTotalFrames()
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(5501, TimelineWithEvents(
                5501,
                2,
                new[] { new CombatActionFrameEvent(1, 201) }));
            var runner = new CombatActionRunner(registry);
            var raised = new List<ActionFrameEventRaisedEvent>();
            var finished = new List<ActionFinishedEvent>();
            var entity = new CombatEntityId(25);
            runner.ActionFrameEventRaised += raised.Add;
            runner.ActionFinished += finished.Add;

            runner.StartAction(entity, 5501, CombatFrame.Zero);
            runner.TickActions(new CombatFrame(1));
            runner.TickActions(new CombatFrame(2));

            Assert.AreEqual(1, raised.Count);
            Assert.AreEqual(201, raised[0].FrameEvent.EventId);
            Assert.AreEqual(1, finished.Count);
            Assert.IsNull(runner.GetActionState(entity));
        }

        [Test]
        public void StartAction_UsesCancelWindowsForRunningAction()
        {
            CombatActionRunner runner = CreateRunner(out _);
            var rejected = new List<ActionCancelRejectedEvent>();
            var canceled = new List<ActionCanceledEvent>();
            runner.ActionCancelRejected += rejected.Add;
            runner.ActionCanceled += canceled.Add;
            var entity = new CombatEntityId(8);

            runner.StartAction(entity, 1001, CombatFrame.Zero);
            ActionResult outsideWindow = runner.StartAction(entity, 2002, new CombatFrame(1));

            Assert.IsFalse(outsideWindow.Success);
            Assert.IsNotEmpty(outsideWindow.Reason);
            Assert.AreEqual(1, rejected.Count);
            Assert.AreEqual(1001, runner.GetActionState(entity).Value.ActionId);

            runner.TickActions(new CombatFrame(1));
            runner.TickActions(new CombatFrame(2));
            runner.TickActions(new CombatFrame(3));

            ActionResult inWindow = runner.StartAction(entity, 2002, new CombatFrame(3));

            Assert.IsTrue(inWindow.Success);
            Assert.AreEqual(2, inWindow.ActionInstanceId);
            Assert.AreEqual(1, canceled.Count);
            Assert.AreEqual(2002, runner.GetActionState(entity).Value.ActionId);
        }

        [Test]
        public void TryCancel_RejectsUncancelableActionId()
        {
            CombatActionRunner runner = CreateRunner(out _);
            var entity = new CombatEntityId(9);
            runner.StartAction(entity, 1001, CombatFrame.Zero);
            runner.TickActions(new CombatFrame(1));
            runner.TickActions(new CombatFrame(2));
            runner.TickActions(new CombatFrame(3));

            ActionResult result = runner.TryCancel(entity, 3003, new CombatFrame(3));

            Assert.IsFalse(result.Success);
            Assert.IsNotEmpty(result.Reason);
            Assert.AreEqual(1001, runner.GetActionState(entity).Value.ActionId);
        }

        [Test]
        public void ForceStartAction_ReplacesRunningActionAndPublishesCanceledStarted()
        {
            CombatActionRunner runner = CreateRunner(out _);
            var canceled = new List<ActionCanceledEvent>();
            var started = new List<ActionStartedEvent>();
            runner.ActionCanceled += canceled.Add;
            runner.ActionStarted += started.Add;
            var entity = new CombatEntityId(10);
            runner.StartAction(entity, 1001, CombatFrame.Zero);

            ActionResult result = runner.ForceStartAction(entity, 3003, new CombatFrame(1));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, canceled.Count);
            Assert.AreEqual(2, started.Count);
            Assert.AreEqual(3003, started[1].ActionId);
            Assert.AreEqual(3003, runner.GetActionState(entity).Value.ActionId);
        }

        [Test]
        public void ForceCancel_RemovesRunningActionAndPublishesEvent()
        {
            CombatActionRunner runner = CreateRunner(out _);
            var canceled = new List<ActionCanceledEvent>();
            runner.ActionCanceled += canceled.Add;
            var entity = new CombatEntityId(11);
            runner.StartAction(entity, 1001, CombatFrame.Zero);

            bool result = runner.ForceCancel(entity);

            Assert.IsTrue(result);
            Assert.AreEqual(1, canceled.Count);
            Assert.AreEqual(1001, canceled[0].ActionId);
            Assert.IsNull(runner.GetActionState(entity));
            Assert.IsFalse(runner.ForceCancel(entity));
        }

        [Test]
        public void StateQueries_ReturnCurrentPhaseWindowsAndInstanceId()
        {
            CombatActionRunner runner = CreateRunner(out _);
            var entity = new CombatEntityId(12);

            runner.StartAction(entity, 1001, CombatFrame.Zero);
            runner.TickActions(new CombatFrame(1));
            runner.TickActions(new CombatFrame(2));
            runner.TickActions(new CombatFrame(3));

            Assert.AreEqual(CombatActionPhase.Active, runner.GetCurrentPhase(entity));
            Assert.IsTrue(runner.IsInCancelWindow(entity, 2002));
            Assert.IsTrue(runner.IsInInvincibleWindow(entity));
            Assert.IsTrue(runner.IsInParryWindow(entity));
            Assert.IsFalse(runner.IsInSuperArmorWindow(entity));
            Assert.AreEqual(1, runner.GetActionInstanceId(entity));
            Assert.AreEqual(1, runner.GetRunningActions().Length);
        }

        [Test]
        public void InstanceId_IncrementsForEachStartedAction()
        {
            CombatActionRunner runner = CreateRunner(out _);
            var entity = new CombatEntityId(13);

            ActionResult first = runner.StartAction(entity, 1001, CombatFrame.Zero);
            runner.ForceCancel(entity);
            ActionResult second = runner.StartAction(entity, 1001, new CombatFrame(1));

            Assert.AreEqual(1, first.ActionInstanceId);
            Assert.AreEqual(2, second.ActionInstanceId);
        }

        [Test]
        public void EmptyPhaseRanges_DoNotBreakLifecycle()
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(4004, new CombatActionTimeline(
                4004,
                2,
                CombatFrameRange.Empty,
                CombatFrameRange.Empty,
                new CombatFrameRange(0, 1),
                null,
                null));
            var runner = new CombatActionRunner(registry);
            var entity = new CombatEntityId(14);

            runner.StartAction(entity, 4004, CombatFrame.Zero);

            Assert.AreEqual(CombatActionPhase.Recovery, runner.GetCurrentPhase(entity));
            runner.TickActions(new CombatFrame(1));
            Assert.AreEqual(CombatActionPhase.Recovery, runner.GetCurrentPhase(entity));
            runner.TickActions(new CombatFrame(2));
            Assert.IsNull(runner.GetActionState(entity));
        }

        private static CombatActionRunner CreateRunner(out CombatActionRegistry registry)
        {
            registry = new CombatActionRegistry();
            registry.RegisterTimeline(1001, QuickAttack());
            registry.RegisterTimeline(2002, HeavyAttack());
            registry.RegisterTimeline(3003, LongRecovery());
            return new CombatActionRunner(registry);
        }

        private static CombatActionTimeline QuickAttack()
        {
            return new CombatActionTimeline(
                1001,
                5,
                new CombatFrameRange(0, 1),
                new CombatFrameRange(2, 3),
                new CombatFrameRange(4, 4),
                new[]
                {
                    new CombatActionWindow(CombatActionWindowKind.Cancel, new CombatFrameRange(3, 3), targetActionId: 2002),
                    new CombatActionWindow(CombatActionWindowKind.Invincible, new CombatFrameRange(3, 3)),
                    new CombatActionWindow(CombatActionWindowKind.Parry, new CombatFrameRange(3, 3)),
                },
                null);
        }

        private static CombatActionTimeline TimelineWithEvents(
            int actionId,
            int totalFrames,
            CombatActionFrameEvent[] events,
            CombatActionWindow[] windows = null)
        {
            return new CombatActionTimeline(
                actionId,
                totalFrames,
                new CombatFrameRange(0, 0),
                totalFrames > 2 ? new CombatFrameRange(1, totalFrames - 2) : CombatFrameRange.Empty,
                new CombatFrameRange(totalFrames - 1, totalFrames - 1),
                windows,
                events);
        }

        private static CombatActionTimeline HeavyAttack()
        {
            return new CombatActionTimeline(
                2002,
                8,
                new CombatFrameRange(0, 3),
                new CombatFrameRange(4, 5),
                new CombatFrameRange(6, 7),
                null,
                null);
        }

        private static CombatActionTimeline LongRecovery()
        {
            return new CombatActionTimeline(
                3003,
                12,
                new CombatFrameRange(0, 1),
                new CombatFrameRange(2, 2),
                new CombatFrameRange(3, 11),
                new[] { new CombatActionWindow(CombatActionWindowKind.SuperArmor, new CombatFrameRange(2, 5)) },
                null);
        }
    }
}

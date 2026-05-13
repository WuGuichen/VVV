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

using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Animation
{
    public class CombatActionTimelineTests
    {
        [Test]
        public void GetPhase_ReturnsConfiguredFramePhase()
        {
            CombatActionTimeline timeline = CreateTimeline();

            Assert.AreEqual(CombatActionPhase.Startup, timeline.GetPhase(0));
            Assert.AreEqual(CombatActionPhase.Startup, timeline.GetPhase(4));
            Assert.AreEqual(CombatActionPhase.Active, timeline.GetPhase(5));
            Assert.AreEqual(CombatActionPhase.Active, timeline.GetPhase(9));
            Assert.AreEqual(CombatActionPhase.Recovery, timeline.GetPhase(10));
            Assert.AreEqual(CombatActionPhase.Recovery, timeline.GetPhase(19));
            Assert.AreEqual(CombatActionPhase.Finished, timeline.GetPhase(20));
        }

        [Test]
        public void Windows_CanBeQueriedByKindAndFrame()
        {
            CombatActionTimeline timeline = CreateTimeline();
            var windows = new List<CombatActionWindow>();

            Assert.IsTrue(timeline.IsInWindow(CombatActionWindowKind.Cancel, 12));
            Assert.IsFalse(timeline.IsInWindow(CombatActionWindowKind.Cancel, 9));
            Assert.AreEqual(1, timeline.CollectWindows(CombatActionWindowKind.Cancel, 12, windows));
            Assert.AreEqual(2002, windows[0].TargetActionId);
        }

        [Test]
        public void Events_AreCollectedInDeterministicOrder()
        {
            CombatActionTimeline timeline = CreateTimeline();
            var events = new List<CombatActionFrameEvent>();

            int count = timeline.CollectEvents(5, events);

            Assert.AreEqual(2, count);
            Assert.AreEqual(100, events[0].EventId);
            Assert.AreEqual(101, events[1].EventId);
            Assert.AreEqual(2, events[1].IntPayload);
        }

        [Test]
        public void Instance_ConvertsWorldFrameToLocalState()
        {
            CombatActionTimeline timeline = CreateTimeline();
            var instance = new CombatActionInstance(new CombatEntityId(7), timeline, new CombatFrame(30));

            CombatActionState state = instance.GetState(new CombatFrame(35));

            Assert.AreEqual(7, state.EntityId.Value);
            Assert.AreEqual(1001, state.ActionId);
            Assert.AreEqual(5, state.LocalFrame);
            Assert.AreEqual(CombatActionPhase.Active, state.Phase);
            Assert.IsTrue(instance.IsInWindow(CombatActionWindowKind.Invincible, new CombatFrame(35)));
        }

        [Test]
        public void Instance_RejectsFrameBeforeStart()
        {
            CombatActionTimeline timeline = CreateTimeline();
            var instance = new CombatActionInstance(new CombatEntityId(7), timeline, new CombatFrame(30));

            Assert.Throws<ArgumentOutOfRangeException>(() => instance.GetState(new CombatFrame(29)));
        }

        [Test]
        public void Timeline_RejectsInvalidConfiguration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatActionTimeline(
                0,
                20,
                new CombatFrameRange(0, 1),
                CombatFrameRange.Empty,
                CombatFrameRange.Empty,
                null,
                null));

            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatActionTimeline(
                1,
                20,
                new CombatFrameRange(0, 20),
                CombatFrameRange.Empty,
                CombatFrameRange.Empty,
                null,
                null));

            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatActionTimeline(
                1,
                20,
                new CombatFrameRange(0, 1),
                CombatFrameRange.Empty,
                CombatFrameRange.Empty,
                null,
                new[] { new CombatActionFrameEvent(20, 1) }));
        }

        private static CombatActionTimeline CreateTimeline()
        {
            return new CombatActionTimeline(
                1001,
                20,
                new CombatFrameRange(0, 4),
                new CombatFrameRange(5, 9),
                new CombatFrameRange(10, 19),
                new[]
                {
                    new CombatActionWindow(CombatActionWindowKind.Cancel, new CombatFrameRange(12, 16), targetActionId: 2002),
                    new CombatActionWindow(CombatActionWindowKind.Invincible, new CombatFrameRange(5, 6)),
                },
                new[]
                {
                    new CombatActionFrameEvent(5, 101, sourceOrder: 2, intPayload: 2),
                    new CombatActionFrameEvent(5, 100, sourceOrder: 1, intPayload: 1),
                    new CombatActionFrameEvent(10, 200, sourceOrder: 1),
                });
        }
    }
}

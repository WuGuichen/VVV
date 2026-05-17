using System;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Animation
{
    public sealed class CombatActionSupportProfileTests
    {
        [Test]
        public void Timeline_StoresOptionalSupportProfile()
        {
            var profile = new CombatActionSupportProfile(
                Fix64.FromRatio(3, 2),
                Fix64.FromRatio(5, 4),
                hasHyperArmorWindow: true,
                hyperArmorWindow: new CombatFrameRange(2, 4));

            CombatActionTimeline timeline = CreateTimeline(profile);

            Assert.IsTrue(timeline.SupportProfile.HasValue);
            Assert.AreEqual(profile, timeline.SupportProfile.Value);
        }

        [Test]
        public void Timeline_DefaultsSupportProfileToNull()
        {
            CombatActionTimeline timeline = CreateTimeline(null);

            Assert.IsFalse(timeline.SupportProfile.HasValue);
        }

        [Test]
        public void Timeline_RejectsHyperArmorWindowOutsideTotalFrames()
        {
            var profile = new CombatActionSupportProfile(
                Fix64.One,
                Fix64.One,
                hasHyperArmorWindow: true,
                hyperArmorWindow: new CombatFrameRange(4, 5));

            Assert.Throws<ArgumentOutOfRangeException>(() => CreateTimeline(profile));
        }

        [Test]
        public void Runner_ReturnsCurrentSupportProfileAndLocalFrame()
        {
            var profile = new CombatActionSupportProfile(Fix64.FromInt(2), Fix64.One);
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(1001, CreateTimeline(profile));
            var runner = new CombatActionRunner(registry);
            var entity = new CombatEntityId(7);

            Assert.IsFalse(runner.GetCurrentSupportProfile(entity).HasValue);
            Assert.AreEqual(-1, runner.GetCurrentLocalFrame(entity));

            runner.StartAction(entity, 1001, CombatFrame.Zero);
            Assert.AreEqual(profile, runner.GetCurrentSupportProfile(entity).Value);
            Assert.AreEqual(0, runner.GetCurrentLocalFrame(entity));

            runner.TickActions(new CombatFrame(1));
            Assert.AreEqual(1, runner.GetCurrentLocalFrame(entity));

            runner.ForceCancel(entity);
            Assert.IsFalse(runner.GetCurrentSupportProfile(entity).HasValue);
            Assert.AreEqual(-1, runner.GetCurrentLocalFrame(entity));
        }

        private static CombatActionTimeline CreateTimeline(CombatActionSupportProfile? supportProfile)
        {
            return new CombatActionTimeline(
                1001,
                5,
                new CombatFrameRange(0, 1),
                new CombatFrameRange(2, 3),
                new CombatFrameRange(4, 4),
                null,
                null,
                supportProfile);
        }
    }
}

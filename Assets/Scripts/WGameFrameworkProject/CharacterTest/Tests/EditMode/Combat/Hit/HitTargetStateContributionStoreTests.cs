using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Hit
{
    public sealed class HitTargetStateContributionStoreTests
    {
        [Test]
        public void Contributions_OrBySourceAndRemoveOnlyTheirOwner()
        {
            var store = new HitTargetStateContributionStore();
            var entity = new CombatEntityId(42);
            store.Upsert(entity, 1001, 960001, HitTargetStateFlags.Unhittable);
            store.Upsert(entity, 1002, 960002, HitTargetStateFlags.Unhittable | HitTargetStateFlags.SuperArmor);

            Assert.AreEqual(
                HitTargetStateFlags.Unhittable | HitTargetStateFlags.SuperArmor,
                store.ResolveFlags(entity));
            Assert.IsTrue(store.Remove(entity, 1001, 960001));
            Assert.AreEqual(
                HitTargetStateFlags.Unhittable | HitTargetStateFlags.SuperArmor,
                store.ResolveFlags(entity));
            Assert.AreEqual(1, store.RemoveInstance(entity, 1002));
            Assert.AreEqual(HitTargetStateFlags.None, store.ResolveFlags(entity));
        }

        [Test]
        public void Contributions_RepeatedActiveUpdateIsIdempotent()
        {
            var store = new HitTargetStateContributionStore();
            var entity = new CombatEntityId(7);
            store.Upsert(entity, 99, 960003, HitTargetStateFlags.Unhittable);
            store.Upsert(entity, 99, 960003, HitTargetStateFlags.Unhittable);

            Assert.AreEqual(1, store.Count);
            Assert.AreEqual(HitTargetStateFlags.Unhittable, store.ResolveFlags(entity));
        }

        [TestCase((int)HitTargetStateFlags.None)]
        [TestCase((int)HitTargetStateFlags.Alive)]
        [TestCase(1 << 20)]
        public void Contributions_RejectInvalidOrLifecycleOwnedMasks(int rawMask)
        {
            var store = new HitTargetStateContributionStore();
            Assert.Throws<ArgumentOutOfRangeException>(() => store.Upsert(
                new CombatEntityId(1),
                1,
                960004,
                (HitTargetStateFlags)rawMask));
        }
    }
}

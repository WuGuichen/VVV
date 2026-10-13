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

        [Test]
        public void RemoveInstance_RemovesAllEventsOnlyForRequestedEntityAndInstance()
        {
            var store = new HitTargetStateContributionStore();
            var entity = new CombatEntityId(42);
            var other = new CombatEntityId(43);
            store.Upsert(entity, 9001, 960001, HitTargetStateFlags.Blocking);
            store.Upsert(entity, 9001, 960002, HitTargetStateFlags.Invincible);
            store.Upsert(entity, 9002, 960001, HitTargetStateFlags.Parrying);
            store.Upsert(other, 9001, 960001, HitTargetStateFlags.SuperArmor);

            Assert.AreEqual(2, store.RemoveInstance(entity, 9001));
            Assert.AreEqual(2, store.Count);
            Assert.AreEqual(HitTargetStateFlags.Parrying, store.ResolveFlags(entity));
            Assert.AreEqual(HitTargetStateFlags.SuperArmor, store.ResolveFlags(other));
            Assert.AreEqual(0, store.RemoveInstance(entity, 9001));
            Assert.AreEqual(0, store.RemoveInstance(CombatEntityId.None, 9002));
            Assert.AreEqual(0, store.RemoveInstance(entity, 0));
            Assert.AreEqual(0, store.RemoveInstance(entity, -1));
            Assert.AreEqual(2, store.Count);
            Assert.AreEqual(HitTargetStateFlags.Parrying, store.ResolveFlags(entity));
            Assert.IsTrue(store.Remove(entity, 9002, 960001));
            Assert.AreEqual(HitTargetStateFlags.None, store.ResolveFlags(entity));
            Assert.AreEqual(1, store.Count);
        }

        [Test]
        public void ClearEntity_RemovesAllInstancesOnlyForRequestedEntityAndClearResetsStore()
        {
            var store = new HitTargetStateContributionStore();
            var entity = new CombatEntityId(42);
            var other = new CombatEntityId(43);
            store.Upsert(entity, 9001, 960001, HitTargetStateFlags.Blocking);
            store.Upsert(entity, 9001, 960002, HitTargetStateFlags.Invincible);
            store.Upsert(entity, 9002, 960001, HitTargetStateFlags.Parrying);
            store.Upsert(other, 9001, 960001, HitTargetStateFlags.SuperArmor);

            Assert.AreEqual(3, store.ClearEntity(entity));
            Assert.AreEqual(1, store.Count);
            Assert.AreEqual(HitTargetStateFlags.None, store.ResolveFlags(entity));
            Assert.AreEqual(HitTargetStateFlags.SuperArmor, store.ResolveFlags(other));
            Assert.AreEqual(0, store.ClearEntity(entity));
            Assert.AreEqual(0, store.ClearEntity(CombatEntityId.None));
            store.Clear();
            Assert.AreEqual(0, store.Count);
            Assert.AreEqual(HitTargetStateFlags.None, store.ResolveFlags(other));
            store.Upsert(entity, 9001, 960001, HitTargetStateFlags.Unhittable);
            Assert.AreEqual(1, store.RemoveInstance(entity, 9001));
            Assert.AreEqual(0, store.Count);
            Assert.AreEqual(HitTargetStateFlags.None, store.ResolveFlags(entity));
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(1, 32)]
        [TestCase(2, 1)]
        [TestCase(2, 32)]
        [TestCase(3, 1)]
        [TestCase(3, 32)]
        [TestCase(4, 32)]
        public void Cleanup_AfterHighWaterWarmupDoesNotAllocate(int operation, int sourceCount)
        {
            var store = new HitTargetStateContributionStore();
            var entity = new CombatEntityId(42);
            var other = new CombatEntityId(43);
            store.Upsert(other, 9001, 960001, HitTargetStateFlags.SuperArmor);
            if (operation == 3)
                for (int source = 0; source < sourceCount; source++)
                    store.Upsert(entity, 9002, 960001 + source, HitTargetStateFlags.Parrying);
            for (int warmup = 0; warmup < 32; warmup++)
                CleanupCycle(store, entity, operation, sourceCount);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int removed = 0;
            for (int cycle = 0; cycle < 1000; cycle++)
                removed += CleanupCycle(store, entity, operation, sourceCount);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated, "Steady-state cleanup must be NoAlloc.");
            Assert.AreEqual(operation == 3 ? 0 : 1000 * sourceCount, removed);
            Assert.AreEqual(operation == 4 ? 0 : operation == 3 ? sourceCount + 1 : 1, store.Count);
            Assert.AreEqual(operation == 3 ? HitTargetStateFlags.Parrying : HitTargetStateFlags.None, store.ResolveFlags(entity));
            Assert.AreEqual(operation == 4 ? HitTargetStateFlags.None : HitTargetStateFlags.SuperArmor, store.ResolveFlags(other));
        }

        [Test]
        public void EmptyBulkCleanup_AfterWarmupDoesNotAllocate()
        {
            var store = new HitTargetStateContributionStore();
            var entity = new CombatEntityId(42);
            for (int warmup = 0; warmup < 32; warmup++)
            {
                store.RemoveInstance(entity, 9001);
                store.ClearEntity(entity);
            }
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int removed = 0;
            for (int cycle = 0; cycle < 1000; cycle++)
            {
                removed += store.RemoveInstance(entity, 9001);
                removed += store.ClearEntity(entity);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(0, removed);
            Assert.AreEqual(0, store.Count);
        }

        private static int CleanupCycle(
            HitTargetStateContributionStore store,
            CombatEntityId entity,
            int operation,
            int sourceCount)
        {
            if (operation == 3)
                return store.RemoveInstance(entity, 9001);
            for (int source = 0; source < sourceCount; source++)
                store.Upsert(entity, 9001, 960001 + source, HitTargetStateFlags.Blocking);
            switch (operation)
            {
                case 0:
                    return store.Remove(entity, 9001, 960001) ? 1 : 0;
                case 2:
                    return store.ClearEntity(entity);
                case 4:
                    int removed = store.RemoveInstance(entity, 9001);
                    store.Clear();
                    return removed;
                default:
                    return store.RemoveInstance(entity, 9001);
            }
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

using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using NUnit.Framework;
using MxFramework.Runtime;
using Newtonsoft.Json.Linq;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentStoreTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void LifecycleRestore_DeadSlotsFifoAndGenerationContinueAcrossEmptyBoundary(bool empty)
        {
            var source = new GameplayComponentWorld();
            var first = source.Registry.CreateEntity();
            var second = source.Registry.CreateEntity();
            var third = source.Registry.CreateEntity();
            source.Registry.DestroyEntity(second); source.Registry.DestroyEntity(first);
            if (empty) source.Registry.DestroyEntity(third);
            var saved = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState();
            var restored = new GameplayComponentWorld();
            var result = new GameplayComponentWorldSaveStateProvider(restored).RestoreSaveState(saved.Value);
            Assert.IsTrue(result.Success, result.Error.ToString());
            Assert.AreEqual(3, restored.Registry.Lifecycle.CountAllocated);
            Assert.IsFalse(restored.Registry.IsAlive(first));
            for (int i = 0; i < 6; i++)
            {
                var expected = source.Registry.CreateEntity(); var actual = restored.Registry.CreateEntity();
                Assert.AreEqual(expected, actual, "allocation continuation index=" + i);
                if (i % 2 == 0) { source.Registry.DestroyEntity(expected); restored.Registry.DestroyEntity(actual); }
            }
        }

        [Test]
        public void LifecycleRestore_InvalidOrMissingStateLeavesLiveIdentityAndStoresUnchanged()
        {
            var world = new GameplayComponentWorld();
            var live = world.Registry.CreateEntity();
            var store = world.Registry.GetOrCreateStore<TestStatComponent>();
            store.Set(live, new TestStatComponent(42));
            var malformed = new GameplayEntityLifecycleSnapshot(new[] { 2 }, new[] { false }, new[] { 1, 1 });
            Assert.Throws<ArgumentException>(() => world.Registry.RestoreAllocationState(malformed));
            Assert.IsTrue(world.Registry.IsAlive(live));
            Assert.IsTrue(store.TryGet(live, out var value)); Assert.AreEqual(42, value.Value);
            var provider = new GameplayComponentWorldSaveStateProvider(world);
            var saved = provider.CaptureSaveState().Value;
            var payload = JObject.Parse(saved.ModuleStates[0].CustomState.PayloadJson);
            payload.Remove("allocation");
            var original = saved.ModuleStates[0];
            var invalidModule = new RuntimeModuleSaveState(original.ModuleId, original.SchemaVersion,
                new RuntimeCustomState(original.CustomState.NumericTypeId, original.CustomState.TypeId,
                    original.CustomState.SchemaVersion, payload.ToString()));
            var invalid = new RuntimeSaveState(saved.SchemaVersion, saved.CreatedAtUtc, saved.FrameworkVersion, saved.ConfigVersion,
                saved.ResourceCatalogVersion, saved.Frame, saved.Entities, saved.GlobalCounters, new[] { invalidModule }, saved.Metadata);
            // Construct the envelope from provider output; only its allocation field is missing.
            Assert.IsFalse(provider.RestoreSaveState(invalid).Success);
            Assert.IsTrue(world.Registry.IsAlive(live));
            Assert.IsTrue(store.TryGet(live, out value)); Assert.AreEqual(42, value.Value);
        }

        [Test]
        public void LifecycleRestore_SchemaOneRejectsMissingGenerationHistoryWithoutMutation()
        {
            var world = new GameplayComponentWorld(); var live = world.Registry.CreateEntity();
            var provider = new GameplayComponentWorldSaveStateProvider(world);
            var saved = provider.CaptureSaveState().Value;
            var module = saved.ModuleStates[0];
            var payload = JObject.Parse(module.CustomState.PayloadJson);
            payload["schemaVersion"] = 1; payload.Remove("allocation");
            var legacy = new RuntimeModuleSaveState(module.ModuleId, 1,
                new RuntimeCustomState(module.CustomState.NumericTypeId, module.CustomState.TypeId, 1, payload.ToString()));
            var envelope = new RuntimeSaveState(saved.SchemaVersion, saved.CreatedAtUtc, saved.FrameworkVersion, saved.ConfigVersion,
                saved.ResourceCatalogVersion, saved.Frame, saved.Entities, saved.GlobalCounters, new[] { legacy }, saved.Metadata);
            var result = provider.RestoreSaveState(envelope);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.UnsupportedVersion, result.Error.Code);
            Assert.IsTrue(world.Registry.IsAlive(live));
        }

        [Test]
        public void LifecycleRestore_ReplacesMaximumGenerationWithoutClearingItFirst()
        {
            var world = new GameplayComponentWorld();
            world.Registry.Lifecycle.RestoreAllocationState(new GameplayEntityLifecycleSnapshot(
                new[] { int.MaxValue }, new[] { true }, System.Array.Empty<int>()));
            var provider = new GameplayComponentWorldSaveStateProvider(world);
            var result = provider.RestoreSaveState(provider.CaptureSaveState().Value);
            Assert.IsTrue(result.Success, result.Error.ToString());
            Assert.IsTrue(world.Registry.IsAlive(new GameplayEntityId(1, int.MaxValue)));
            Assert.AreEqual(new GameplayEntityId(2, 1), world.Registry.CreateEntity());
        }

        [Test]
        public void LifecycleHash_DistinguishesReuseOrderWithEqualLiveEntities()
        {
            var first = new GameplayComponentWorld(); var second = new GameplayComponentWorld();
            var a = first.Registry.CreateEntity(); var b = first.Registry.CreateEntity();
            second.Registry.CreateEntity(); second.Registry.CreateEntity();
            first.Registry.DestroyEntity(a); first.Registry.DestroyEntity(b);
            second.Registry.DestroyEntity(b); second.Registry.DestroyEntity(a);
            CollectionAssert.AreEqual(first.CreateEntitySnapshot(), second.CreateEntitySnapshot());
            var left = new RuntimeHashAccumulator(1, 1); var right = new RuntimeHashAccumulator(1, 1);
            new GameplayComponentWorldHashContributor(first).Contribute(default, left);
            new GameplayComponentWorldHashContributor(second).Contribute(default, right);
            Assert.AreNotEqual(left.ToHash(), right.ToHash());
        }

        [Test]
        public void LifecycleBoundedSchemaOneRejectsLossyDeadSlotCapture()
        {
            var world = new GameplayComponentWorld();
            var entity = world.Registry.CreateEntity(); world.Registry.DestroyEntity(entity);
            var state = new GameplayComponentWorldNetworkState(world, 4);
            Assert.AreEqual(GameplayComponentNetworkStateErrorCode.UnsupportedSchema,
                state.TryCapture(new byte[state.MaximumEncodedBytes], out int written).ErrorCode);
            Assert.AreEqual(0, written);
        }

        [Test]
        public void GameplayEntityId_DefaultIsInvalidAndOrdersByIndexThenGeneration()
        {
            Assert.IsFalse(default(GameplayEntityId).IsValid);

            var first = new GameplayEntityId(1, 2);
            var second = new GameplayEntityId(2, 1);
            var sameIndexOlder = new GameplayEntityId(1, 1);
            var ids = new[] { second, first, sameIndexOlder };

            Array.Sort(ids);

            Assert.AreEqual(sameIndexOlder, ids[0]);
            Assert.AreEqual(first, ids[1]);
            Assert.AreEqual(second, ids[2]);
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplayEntityId(-1, 1));
            Assert.Throws<ArgumentException>(() => new GameplayEntityId(1, 0));
        }

        [Test]
        public void GameplayEntityLifecycle_ReusesIndexWithNewGenerationAndRejectsStaleId()
        {
            var lifecycle = new GameplayEntityLifecycle();
            GameplayEntityId first = lifecycle.Create();

            Assert.AreEqual(1, first.Index);
            Assert.AreEqual(1, first.Generation);
            Assert.IsTrue(lifecycle.IsAlive(first));
            Assert.AreEqual(1, lifecycle.CountAlive);

            Assert.IsTrue(lifecycle.Destroy(first));
            Assert.IsFalse(lifecycle.IsAlive(first));
            Assert.IsFalse(lifecycle.Destroy(first));

            GameplayEntityId second = lifecycle.Create();

            Assert.AreEqual(first.Index, second.Index);
            Assert.AreNotEqual(first.Generation, second.Generation);
            Assert.IsFalse(lifecycle.IsAlive(first));
            Assert.IsTrue(lifecycle.IsAlive(second));
        }

        [Test]
        public void GameplayEntityLifecycle_SnapshotIsStableAndClearInvalidatesAliveIds()
        {
            var lifecycle = new GameplayEntityLifecycle();
            GameplayEntityId first = lifecycle.Create();
            GameplayEntityId second = lifecycle.Create();
            GameplayEntityId third = lifecycle.Create();

            Assert.IsTrue(lifecycle.Destroy(second));

            GameplayEntityId[] snapshot = lifecycle.CreateSnapshot();

            Assert.AreEqual(2, snapshot.Length);
            Assert.AreEqual(first, snapshot[0]);
            Assert.AreEqual(third, snapshot[1]);

            lifecycle.Clear();

            Assert.AreEqual(0, lifecycle.CountAlive);
            Assert.IsFalse(lifecycle.IsAlive(first));
            Assert.IsFalse(lifecycle.IsAlive(third));

            GameplayEntityId reused = lifecycle.Create();
            Assert.AreEqual(2, reused.Generation);
            Assert.IsFalse(lifecycle.IsAlive(first));
        }

        [Test]
        public void GameplayComponentStore_AddGetSetRemoveAndClear()
        {
            var lifecycle = new GameplayEntityLifecycle();
            GameplayEntityId entityId = lifecycle.Create();
            var store = new GameplayComponentStore<TestStatComponent>();

            Assert.IsTrue(store.TryAdd(entityId, new TestStatComponent(10)));
            Assert.IsFalse(store.TryAdd(entityId, new TestStatComponent(20)));
            Assert.IsTrue(store.TryGet(entityId, out TestStatComponent component));
            Assert.AreEqual(10, component.Value);

            store.Set(entityId, new TestStatComponent(30));

            Assert.IsTrue(store.Contains(entityId));
            Assert.IsTrue(store.TryGet(entityId, out component));
            Assert.AreEqual(30, component.Value);

            Assert.IsTrue(store.Remove(entityId));
            Assert.IsFalse(store.Remove(entityId));
            Assert.IsFalse(store.TryGet(entityId, out _));

            store.Set(entityId, new TestStatComponent(40));
            Assert.AreEqual(1, store.Count);
            store.Clear();
            Assert.AreEqual(0, store.Count);
        }

        [Test]
        public void GameplayComponentStore_RejectsInvalidEntityId()
        {
            var store = new GameplayComponentStore<TestStatComponent>();

            Assert.Throws<ArgumentException>(() => store.TryAdd(default, new TestStatComponent(1)));
            Assert.Throws<ArgumentException>(() => store.Set(default, new TestStatComponent(1)));
            Assert.IsFalse(store.Contains(default));
            Assert.IsFalse(store.TryGet(default, out _));
            Assert.IsFalse(store.Remove(default));
        }

        [Test]
        public void GameplayComponentStore_SnapshotAndCopyUseStableEntityOrdering()
        {
            var lifecycle = new GameplayEntityLifecycle();
            GameplayEntityId first = lifecycle.Create();
            GameplayEntityId second = lifecycle.Create();
            GameplayEntityId third = lifecycle.Create();
            var store = new GameplayComponentStore<TestStatComponent>();

            Assert.IsTrue(store.TryAdd(third, new TestStatComponent(30)));
            Assert.IsTrue(store.TryAdd(first, new TestStatComponent(10)));
            Assert.IsTrue(store.TryAdd(second, new TestStatComponent(20)));

            GameplayComponentSnapshot<TestStatComponent>[] snapshot = store.CreateSnapshot();

            Assert.AreEqual(first, snapshot[0].EntityId);
            Assert.AreEqual(10, snapshot[0].Component.Value);
            Assert.AreEqual(second, snapshot[1].EntityId);
            Assert.AreEqual(20, snapshot[1].Component.Value);
            Assert.AreEqual(third, snapshot[2].EntityId);
            Assert.AreEqual(30, snapshot[2].Component.Value);

            var copied = new List<GameplayComponentSnapshot<TestStatComponent>>();
            Assert.AreEqual(3, store.CopyTo(copied));
            Assert.AreEqual(first, copied[0].EntityId);
            Assert.AreEqual(second, copied[1].EntityId);
            Assert.AreEqual(third, copied[2].EntityId);
        }

        [Test]
        public void GameplayComponentStore_CopyToDoesNotAllocateAfterWarmup()
        {
            var lifecycle = new GameplayEntityLifecycle();
            GameplayEntityId first = lifecycle.Create();
            GameplayEntityId second = lifecycle.Create();
            GameplayEntityId third = lifecycle.Create();
            var store = new GameplayComponentStore<TestStatComponent>();
            store.Set(third, new TestStatComponent(30));
            store.Set(first, new TestStatComponent(10));
            store.Set(second, new TestStatComponent(20));
            var copied = new List<GameplayComponentSnapshot<TestStatComponent>>(3);
            store.CopyTo(copied);
            copied.Clear();

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int copiedCount = store.CopyTo(copied);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(3, copiedCount);
            Assert.AreEqual(first, copied[0].EntityId);
            Assert.AreEqual(second, copied[1].EntityId);
            Assert.AreEqual(third, copied[2].EntityId);
            Assert.AreEqual(0L, allocatedBytes);
        }

        private readonly struct TestStatComponent : IGameplayComponent
        {
            public TestStatComponent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }
}

using System;
using System.Collections.Generic;
using MxFramework.Buffs;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public class GameplayWorldContractTests
    {
        private const int AttrHp = 1;
        private const int BuffRecorder = 500001;

        [Test]
        public void GameplayWorld_RegisterQueryAndRemoveEntity()
        {
            var world = new GameplayWorld();
            RuntimeEntity entity = CreateEntity(10);

            world.Register(entity);

            Assert.AreEqual(1, world.Entities.Count);
            Assert.IsTrue(world.Entities.TryGet(10, out IRuntimeEntity registered));
            Assert.AreSame(entity, registered);

            Assert.IsTrue(world.Remove(10));
            Assert.IsFalse(world.Entities.TryGet(10, out registered));
            Assert.IsNull(registered);
            Assert.IsFalse(world.Remove(10));
            Assert.IsFalse(world.Entities.TryGet(0, out registered));
            Assert.IsNull(registered);
            Assert.IsFalse(world.Remove(0));
        }

        [Test]
        public void RuntimeEntityRegistry_RegisterRejectsNullDuplicateAndInvalidIds()
        {
            var registry = new RuntimeEntityRegistry();
            RuntimeEntity entity = CreateEntity(1);

            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => registry.Register(CreateEntity(0)));

            registry.Register(entity);

            InvalidOperationException duplicate = Assert.Throws<InvalidOperationException>(() => registry.Register(CreateEntity(1)));
            StringAssert.Contains("already registered", duplicate.Message);
        }

        [Test]
        public void RuntimeEntityRegistry_EnumeratesByEntityId()
        {
            var registry = new RuntimeEntityRegistry();
            registry.Register(CreateEntity(30));
            registry.Register(CreateEntity(10));
            registry.Register(CreateEntity(20));

            AssertIds(new[] { 10, 20, 30 }, registry.CreateSnapshot());
            AssertIds(new[] { 10, 20, 30 }, registry);
        }

        [Test]
        public void GameplayWorld_TickAdvancesCountAndTicksEntitiesInStableOrder()
        {
            var world = new GameplayWorld();
            var tickOrder = new List<int>();
            RuntimeEntity late = CreateEntity(30);
            RuntimeEntity early = CreateEntity(10);

            late.Buffs.AddBuff(new RecordingBuff(tickOrder), late);
            early.Buffs.AddBuff(new RecordingBuff(tickOrder), early);

            world.Register(late);
            world.Register(early);

            world.Tick(0.25d);

            Assert.AreEqual(1L, world.TickCount);
            CollectionAssert.AreEqual(new[] { 10, 30 }, tickOrder);
        }

        [Test]
        public void GameplayWorld_TickRejectsInvalidDeltaTime()
        {
            var world = new GameplayWorld();

            Assert.Throws<ArgumentOutOfRangeException>(() => world.Tick(-0.01d));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.Tick(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.Tick(double.PositiveInfinity));
        }

        [Test]
        public void GameplayWorldSnapshot_CopiesRegistryState()
        {
            var world = new GameplayWorld();
            RuntimeEntity first = CreateEntity(1);
            RuntimeEntity second = CreateEntity(2);

            world.Register(first);
            world.Tick(0d);
            GameplayWorldSnapshot snapshot = world.CreateSnapshot();

            world.Remove(1);
            world.Register(second);
            world.Tick(0d);

            Assert.AreEqual(1L, snapshot.TickCount);
            Assert.AreEqual(1, snapshot.Entities.Count);
            Assert.AreSame(first, snapshot.Entities[0]);
            Assert.AreEqual(1, snapshot.Entities[0].EntityId);
            Assert.AreEqual(1, world.Entities.Count);
            Assert.IsTrue(world.Entities.TryGet(2, out _));
        }

        private static RuntimeEntity CreateEntity(int entityId)
        {
            var entity = new RuntimeEntity(entityId, teamId: 1, hpAttributeId: AttrHp);
            if (entityId > 0)
                entity.Store.RegisterAttribute(AttrHp, 100);

            return entity;
        }

        private static void AssertIds(IReadOnlyList<int> expected, IEnumerable<IRuntimeEntity> entities)
        {
            var actual = new List<int>();
            foreach (IRuntimeEntity entity in entities)
                actual.Add(entity.EntityId);

            CollectionAssert.AreEqual(expected, actual);
        }

        private sealed class RecordingBuff : BuffBase
        {
            private readonly List<int> _tickOrder;

            public RecordingBuff(List<int> tickOrder)
                : base(BuffRecorder, duration: 1f)
            {
                _tickOrder = tickOrder;
            }

            public override void OnTick(float deltaTime, IBuffTarget target)
            {
                base.OnTick(deltaTime, target);
                var entity = (IRuntimeEntity)target;
                _tickOrder.Add(entity.EntityId);
            }
        }
    }
}

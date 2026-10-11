using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.GameplayBridge
{
    public sealed class CombatActionStateSyncSystemTests
    {
        [Test]
        public void Tick_ActiveActionStateWritesSnapshot()
        {
            var map = new CombatEntityGameplayMap();
            var world = new GameplayComponentWorld();
            CombatEntityId combatId = Combat(10);
            GameplayEntityId gameplayId = world.CreateEntity();
            map.Register(combatId, gameplayId);
            var state = new CombatActionState(
                combatId,
                actionId: 1001,
                localFrame: 7,
                startedAtFrame: new CombatFrame(3),
                phase: CombatActionPhase.Active);
            var system = new CombatActionStateSyncSystem(map, world, id => state);

            system.Tick(CreateContext(world));

            CombatActionStateComponent component = GetComponent(world, gameplayId);
            Assert.IsTrue(component.IsActive);
            Assert.AreEqual(1001, component.ActionId);
            Assert.AreEqual(CombatActionPhase.Active, component.Phase);
            Assert.AreEqual(7, component.LocalFrame);
            Assert.IsFalse(component.IsFinished);
        }

        [Test]
        public void Tick_NullActionStateWritesInactiveSnapshot()
        {
            var map = new CombatEntityGameplayMap();
            var world = new GameplayComponentWorld();
            CombatEntityId combatId = Combat(10);
            GameplayEntityId gameplayId = world.CreateEntity();
            map.Register(combatId, gameplayId);
            var system = new CombatActionStateSyncSystem(map, world, id => null);

            system.Tick(CreateContext(world));

            CombatActionStateComponent component = GetComponent(world, gameplayId);
            Assert.IsFalse(component.IsActive);
            Assert.AreEqual(0, component.ActionId);
            Assert.AreEqual(CombatActionPhase.None, component.Phase);
            Assert.AreEqual(0, component.LocalFrame);
            Assert.IsFalse(component.IsFinished);
        }

        [Test]
        public void Tick_MultipleEntitiesWritesMappedStates()
        {
            var map = new CombatEntityGameplayMap();
            var world = new GameplayComponentWorld();
            CombatEntityId firstCombatId = Combat(10);
            CombatEntityId secondCombatId = Combat(20);
            CombatEntityId thirdCombatId = Combat(30);
            GameplayEntityId firstGameplayId = world.CreateEntity();
            GameplayEntityId secondGameplayId = world.CreateEntity();
            GameplayEntityId thirdGameplayId = world.CreateEntity();
            map.Register(firstCombatId, firstGameplayId);
            map.Register(secondCombatId, secondGameplayId);
            map.Register(thirdCombatId, thirdGameplayId);
            var queried = new List<CombatEntityId>();
            var states = new Dictionary<CombatEntityId, CombatActionState?>
            {
                {
                    firstCombatId,
                    new CombatActionState(
                        firstCombatId,
                        actionId: 1001,
                        localFrame: 1,
                        startedAtFrame: new CombatFrame(0),
                        phase: CombatActionPhase.Startup)
                },
                { secondCombatId, null },
                {
                    thirdCombatId,
                    new CombatActionState(
                        thirdCombatId,
                        actionId: 3003,
                        localFrame: 12,
                        startedAtFrame: new CombatFrame(4),
                        phase: CombatActionPhase.Finished)
                },
            };
            var system = new CombatActionStateSyncSystem(
                map,
                world,
                id =>
                {
                    queried.Add(id);
                    return states[id];
                });

            system.Tick(CreateContext(world));

            CollectionAssert.AreEqual(new[] { firstCombatId, secondCombatId, thirdCombatId }, queried);
            CombatActionStateComponent first = GetComponent(world, firstGameplayId);
            Assert.IsTrue(first.IsActive);
            Assert.AreEqual(1001, first.ActionId);
            Assert.AreEqual(CombatActionPhase.Startup, first.Phase);
            Assert.AreEqual(1, first.LocalFrame);

            CombatActionStateComponent second = GetComponent(world, secondGameplayId);
            Assert.IsFalse(second.IsActive);
            Assert.AreEqual(0, second.ActionId);

            CombatActionStateComponent third = GetComponent(world, thirdGameplayId);
            Assert.IsTrue(third.IsActive);
            Assert.AreEqual(3003, third.ActionId);
            Assert.AreEqual(CombatActionPhase.Finished, third.Phase);
            Assert.AreEqual(12, third.LocalFrame);
            Assert.IsTrue(third.IsFinished);
        }

        [Test]
        public void Tick_EmptyMapDoesNotThrowOrQuery()
        {
            var map = new CombatEntityGameplayMap();
            var world = new GameplayComponentWorld();
            int queryCount = 0;
            var system = new CombatActionStateSyncSystem(
                map,
                world,
                id =>
                {
                    queryCount++;
                    return null;
                });

            Assert.DoesNotThrow(() => system.Tick(CreateContext(world)));
            Assert.AreEqual(0, queryCount);
            Assert.IsFalse(world.TryGetStore(out GameplayComponentStore<CombatActionStateComponent> store));
            Assert.IsNull(store);
        }

        [Test]
        public void Tick_QueryFailurePropagates()
        {
            var map = new CombatEntityGameplayMap();
            var world = new GameplayComponentWorld();
            CombatEntityId combatId = Combat(10);
            map.Register(combatId, world.CreateEntity());
            var expected = new InvalidOperationException("deleted combat entity");
            var system = new CombatActionStateSyncSystem(
                map,
                world,
                id => throw expected);

            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(
                () => system.Tick(CreateContext(world)));

            Assert.AreSame(expected, actual);
        }

        [Test]
        public void SetEnabled_DisabledTickDoesNotQueryOrWrite()
        {
            var map = new CombatEntityGameplayMap();
            var world = new GameplayComponentWorld();
            CombatEntityId combatId = Combat(10);
            GameplayEntityId gameplayId = world.CreateEntity();
            map.Register(combatId, gameplayId);
            int queryCount = 0;
            var system = new CombatActionStateSyncSystem(
                map,
                world,
                id =>
                {
                    queryCount++;
                    return new CombatActionState(
                        id,
                        actionId: 1001,
                        localFrame: 1,
                        startedAtFrame: CombatFrame.Zero,
                        phase: CombatActionPhase.Active);
                });

            system.SetEnabled(false);
            system.Tick(CreateContext(world));

            Assert.IsFalse(system.IsEnabled);
            Assert.AreEqual(0, queryCount);
            Assert.IsFalse(world.TryGetStore(out GameplayComponentStore<CombatActionStateComponent> store));
            Assert.IsNull(store);

            system.SetEnabled(true);
            system.Tick(CreateContext(world));

            Assert.IsTrue(system.IsEnabled);
            Assert.AreEqual(1, queryCount);
            Assert.IsTrue(GetComponent(world, gameplayId).IsActive);
        }

        [Test]
        public void Constructor_StoresSystemMetadata()
        {
            var map = new CombatEntityGameplayMap();
            var world = new GameplayComponentWorld();
            var system = new CombatActionStateSyncSystem(
                map,
                world,
                id => null,
                systemId: "custom.action.sync",
                phase: GameplaySystemPhase.Diagnostics,
                priority: 120);

            Assert.AreEqual(CombatActionStateSyncSystem.DefaultSystemId, "mxframework.bridge.combat.action_sync");
            Assert.AreEqual("custom.action.sync", system.SystemId);
            Assert.AreEqual(GameplaySystemPhase.Diagnostics, system.Phase);
            Assert.AreEqual(120, system.Priority);
            Assert.IsTrue(system.IsEnabled);
        }

        [Test]
        public void Constructor_DefaultsToSimulationPriorityAndDefaultId()
        {
            var system = new CombatActionStateSyncSystem(
                new CombatEntityGameplayMap(),
                new GameplayComponentWorld(),
                id => null);

            Assert.AreEqual(CombatActionStateSyncSystem.DefaultSystemId, system.SystemId);
            Assert.AreEqual(GameplaySystemPhase.Simulation, system.Phase);
            Assert.AreEqual(60, system.Priority);
        }

        private static CombatActionStateComponent GetComponent(GameplayComponentWorld world, GameplayEntityId entityId)
        {
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<CombatActionStateComponent> store));
            Assert.IsTrue(store.TryGet(entityId, out CombatActionStateComponent component));
            return component;
        }

        private static GameplaySystemContext CreateContext(GameplayComponentWorld world)
        {
            return new GameplaySystemContext(
                RuntimeFrame.Zero,
                0d,
                0d,
                new GameplayWorld(),
                Array.Empty<RuntimeCommand>(),
                world.Events,
                componentWorld: world);
        }

        private static CombatEntityId Combat(int value)
        {
            return new CombatEntityId(value);
        }
    }
}

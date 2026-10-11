using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.GameplayBridge
{
    public sealed class CombatGameplayAuraBuffSystemTests
    {
        private const int AuraId = 501001;
        private const int BuffId = 601001;
        private const int CombatLayer = 0;
        private const int DefenseAttributeId = 3;

        [Test]
        public void Tick_AppliesDefenseBuffToInRangeSameTeamTarget()
        {
            GameplayComponentWorld world = new GameplayComponentWorld();
            var physicsWorld = new CombatPhysicsWorld();
            var map = new CombatEntityGameplayMap();
            var anchors = new DictionaryAuraAnchorProvider();
            GameplayEntityId caster = CreateActor(world, teamId: 1);
            GameplayEntityId ally = CreateActor(world, teamId: 1);
            GameplayEntityId enemy = CreateActor(world, teamId: 2);
            GameplayEntityId farAlly = CreateActor(world, teamId: 1);
            RegisterActor(map, physicsWorld, anchors, caster, Combat(1), Body(1), Position(0));
            RegisterActor(map, physicsWorld, anchors, ally, Combat(2), Body(2), Position(2));
            RegisterActor(map, physicsWorld, anchors, enemy, Combat(3), Body(3), Position(2));
            RegisterActor(map, physicsWorld, anchors, farAlly, Combat(4), Body(4), Position(8));
            AddAuraSource(world, caster, AuraId, endFrame: 20);
            AuraFixture fixture = CreateFixture(world, physicsWorld, map, anchors, Definition());

            fixture.Tick(10);

            AssertBuff(world, ally, endFrame: 12);
            Assert.AreEqual(15, GameplayComponentAttributeQuery.GetCurrentValueOrDefault(world, ally, DefenseAttributeId));
            AssertNoBuff(world, caster);
            AssertNoBuff(world, enemy);
            AssertNoBuff(world, farAlly);
            Assert.AreEqual(1, fixture.System.LastTickSummary.SourceCount);
            Assert.AreEqual(1, fixture.System.LastTickSummary.QueryCount);
            Assert.AreEqual(1, fixture.System.LastTickSummary.AppliedOrRefreshedCount);
        }

        [Test]
        public void Tick_TargetLeavingRangeStopsRefreshAndCleanupRemovesBuffAndModifier()
        {
            GameplayComponentWorld world = new GameplayComponentWorld();
            var physicsWorld = new CombatPhysicsWorld();
            var map = new CombatEntityGameplayMap();
            var anchors = new DictionaryAuraAnchorProvider();
            GameplayEntityId caster = CreateActor(world, teamId: 1);
            GameplayEntityId ally = CreateActor(world, teamId: 1);
            RegisterActor(map, physicsWorld, anchors, caster, Combat(1), Body(1), Position(0));
            RegisterActor(map, physicsWorld, anchors, ally, Combat(2), Body(2), Position(2));
            AddAuraSource(world, caster, AuraId, endFrame: 20);
            AuraFixture fixture = CreateFixture(world, physicsWorld, map, anchors, Definition());

            fixture.Tick(10);
            physicsWorld.SetBodyPosition(Body(2), Position(8));
            fixture.Tick(12);

            AssertNoBuff(world, ally);
            Assert.AreEqual(10, GameplayComponentAttributeQuery.GetCurrentValueOrDefault(world, ally, DefenseAttributeId));
            Assert.AreEqual(0, fixture.System.LastTickSummary.AppliedOrRefreshedCount);
        }

        [Test]
        public void Tick_MultipleAuraSourcesRefreshOneBuffEntry()
        {
            GameplayComponentWorld world = new GameplayComponentWorld();
            var physicsWorld = new CombatPhysicsWorld();
            var map = new CombatEntityGameplayMap();
            var anchors = new DictionaryAuraAnchorProvider();
            GameplayEntityId casterA = CreateActor(world, teamId: 1);
            GameplayEntityId casterB = CreateActor(world, teamId: 1);
            GameplayEntityId ally = CreateActor(world, teamId: 1);
            RegisterActor(map, physicsWorld, anchors, casterA, Combat(1), Body(1), Position(0));
            RegisterActor(map, physicsWorld, anchors, casterB, Combat(2), Body(2), Position(4));
            RegisterActor(map, physicsWorld, anchors, ally, Combat(3), Body(3), Position(2));
            AddAuraSource(world, casterA, AuraId, endFrame: 20);
            AddAuraSource(world, casterB, AuraId, endFrame: 20);
            AuraFixture fixture = CreateFixture(world, physicsWorld, map, anchors, Definition(includeCaster: false));

            fixture.Tick(10);

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore));
            Assert.IsTrue(buffStore.TryGet(ally, out GameplayComponentBuffSetComponent buffs));
            Assert.AreEqual(1, buffs.Count);
            Assert.IsTrue(buffs.TryGet(BuffId, out GameplayComponentBuffEntry buff));
            Assert.AreEqual(12, buff.EndFrame);
            Assert.AreEqual(2, fixture.System.LastTickSummary.AppliedOrRefreshedCount);
        }

        [Test]
        public void Tick_RelationFilterAndIncludeCasterUseGameplayTargetingRules()
        {
            GameplayComponentWorld world = new GameplayComponentWorld();
            var physicsWorld = new CombatPhysicsWorld();
            var map = new CombatEntityGameplayMap();
            var anchors = new DictionaryAuraAnchorProvider();
            GameplayEntityId caster = CreateActor(world, teamId: 1);
            GameplayEntityId ally = CreateActor(world, teamId: 1);
            GameplayEntityId enemy = CreateActor(world, teamId: 2);
            RegisterActor(map, physicsWorld, anchors, caster, Combat(1), Body(1), Position(0));
            RegisterActor(map, physicsWorld, anchors, ally, Combat(2), Body(2), Position(2));
            RegisterActor(map, physicsWorld, anchors, enemy, Combat(3), Body(3), Position(2));
            AddAuraSource(world, caster, AuraId, endFrame: 20);
            AuraFixture fixture = CreateFixture(
                world,
                physicsWorld,
                map,
                anchors,
                Definition(
                    relationFilter: GameplayTargetRelationFilter.Enemy,
                    includeCaster: true));

            fixture.Tick(10);

            AssertBuff(world, enemy, endFrame: 12);
            AssertNoBuff(world, caster);
            AssertNoBuff(world, ally);
            Assert.GreaterOrEqual(fixture.System.LastTickSummary.RejectedTargetCount, 2);
        }

        [Test]
        public void Tick_DoesNotAllocateWhenAuraSourceHasNoTargetsAfterWarmup()
        {
            GameplayComponentWorld world = new GameplayComponentWorld();
            var physicsWorld = new CombatPhysicsWorld();
            var map = new CombatEntityGameplayMap();
            var anchors = new DictionaryAuraAnchorProvider();
            GameplayEntityId caster = CreateActor(world, teamId: 1);
            RegisterActor(map, physicsWorld, anchors, caster, Combat(1), Body(1), Position(0));
            AddAuraSource(world, caster, AuraId, endFrame: 200);
            AuraFixture fixture = CreateFixture(world, physicsWorld, map, anchors, Definition(includeCaster: false));
            GameplaySystemContext context = CreateContext(world, 10);

            fixture.System.Tick(context);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                fixture.System.Tick(context);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(1, fixture.System.LastTickSummary.SourceCount);
            Assert.AreEqual(1, fixture.System.LastTickSummary.QueryCount);
            Assert.AreEqual(0, fixture.System.LastTickSummary.AppliedOrRefreshedCount);
        }

        [Test]
        public void Tick_MissingDefinitionMapAndAnchorAreSummarizedWithoutThrowing()
        {
            GameplayComponentWorld world = new GameplayComponentWorld();
            var physicsWorld = new CombatPhysicsWorld();
            var map = new CombatEntityGameplayMap();
            var anchors = new DictionaryAuraAnchorProvider();
            GameplayEntityId missingDefinition = CreateActor(world, teamId: 1);
            GameplayEntityId missingMap = CreateActor(world, teamId: 1);
            GameplayEntityId missingAnchor = CreateActor(world, teamId: 1);
            AddAuraSource(world, missingDefinition, auraId: 999, endFrame: 20);
            AddAuraSource(world, missingMap, AuraId, endFrame: 20);
            AddAuraSource(world, missingAnchor, auraId: 777, endFrame: 20);
            map.Register(Combat(7), missingAnchor);
            AuraFixture fixture = CreateFixture(
                world,
                physicsWorld,
                map,
                anchors,
                Definition(),
                Definition(auraId: 777));

            Assert.DoesNotThrow(() => fixture.Tick(10));
            Assert.AreEqual(3, fixture.System.LastTickSummary.SourceCount);
            Assert.AreEqual(1, fixture.System.LastTickSummary.SkippedMissingDefinitionCount);
            Assert.AreEqual(1, fixture.System.LastTickSummary.SkippedMissingCombatMapCount);
            Assert.AreEqual(1, fixture.System.LastTickSummary.SkippedMissingAnchorCount);
        }

        [Test]
        public void DefinitionRegistry_SortsRejectsDuplicatesAndComputesStableHash()
        {
            var registry = new CombatGameplayAuraBuffDefinitionRegistry(
                Definition(auraId: 300),
                Definition(auraId: 100));

            CombatGameplayAuraBuffDefinition[] definitions = registry.ToArray();

            Assert.AreEqual(2, registry.Count);
            Assert.AreEqual(100, definitions[0].AuraId);
            Assert.AreEqual(300, definitions[1].AuraId);
            Assert.AreEqual(registry.ComputeStableHash(), new CombatGameplayAuraBuffDefinitionRegistry(
                Definition(auraId: 300),
                Definition(auraId: 100)).ComputeStableHash());
            Assert.Throws<ArgumentException>(() => new CombatGameplayAuraBuffDefinitionRegistry(
                Definition(auraId: 100),
                Definition(auraId: 100)));
        }

        private static AuraFixture CreateFixture(
            GameplayComponentWorld world,
            CombatPhysicsWorld physicsWorld,
            CombatEntityGameplayMap map,
            DictionaryAuraAnchorProvider anchors,
            params CombatGameplayAuraBuffDefinition[] definitions)
        {
            var requests = new GameplayBuffRequestBuffer();
            var buffDefinitions = new GameplayBuffDefinitionRegistry(new GameplayBuffDefinition(
                BuffId,
                "buff.test.guard",
                2,
                maxStackCount: 1,
                GameplayBuffStackPolicy.RefreshDuration,
                new[] { GameplayBuffEffectDefinition.AttributeModifier(1, DefenseAttributeId, 5) }));
            var system = new CombatGameplayAuraBuffSystem(
                map,
                world,
                physicsWorld,
                new CombatGameplayAuraBuffDefinitionRegistry(definitions),
                anchors,
                requests);
            return new AuraFixture(
                system,
                new GameplayComponentBuffCleanupSystem(),
                new GameplayBuffApplicationSystem(buffDefinitions, requests),
                new GameplayBuffEffectProjectionSystem(buffDefinitions),
                world);
        }

        private static CombatGameplayAuraBuffDefinition Definition(
            int auraId = AuraId,
            GameplayTargetRelationFilter relationFilter = GameplayTargetRelationFilter.SameTeam,
            bool includeCaster = false)
        {
            return new CombatGameplayAuraBuffDefinition(
                auraId,
                BuffId,
                radius: Fix64.FromInt(3),
                layerMask: CombatPhysicsLayerMask.FromLayer(CombatLayer),
                relationFilter: relationFilter,
                includeCaster: includeCaster);
        }

        private static GameplayEntityId CreateActor(GameplayComponentWorld world, int teamId)
        {
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayLifecycleComponent>().Set(entity, GameplayLifecycleComponent.Alive);
            world.GetOrCreateStore<GameplayTeamComponent>().Set(entity, new GameplayTeamComponent(teamId));
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(DefenseAttributeId, 10, 10)));
            return entity;
        }

        private static void RegisterActor(
            CombatEntityGameplayMap map,
            CombatPhysicsWorld physicsWorld,
            DictionaryAuraAnchorProvider anchors,
            GameplayEntityId gameplayEntityId,
            CombatEntityId combatEntityId,
            CombatBodyId bodyId,
            FixVector3 position)
        {
            map.Register(combatEntityId, gameplayEntityId);
            anchors.Set(combatEntityId, position);
            physicsWorld.UpsertBody(new CombatPhysicsBody(combatEntityId, bodyId, position));
            physicsWorld.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(bodyId.Value),
                CombatLayer,
                new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half),
                new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half)));
        }

        private static void AddAuraSource(
            GameplayComponentWorld world,
            GameplayEntityId entity,
            int auraId,
            long endFrame)
        {
            GameplayComponentStore<GameplayComponentAuraSourceSetComponent> store =
                world.GetOrCreateStore<GameplayComponentAuraSourceSetComponent>();
            store.TryGet(entity, out GameplayComponentAuraSourceSetComponent current);
            store.Set(entity, current.Upsert(new GameplayComponentAuraSourceEntry(auraId, endFrame)));
        }

        private static void AssertBuff(GameplayComponentWorld world, GameplayEntityId entity, long endFrame)
        {
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore));
            Assert.IsTrue(buffStore.TryGet(entity, out GameplayComponentBuffSetComponent buffs));
            Assert.IsTrue(buffs.TryGet(BuffId, out GameplayComponentBuffEntry buff));
            Assert.AreEqual(endFrame, buff.EndFrame);
            Assert.AreEqual(AuraId, buff.SourceId);
        }

        private static void AssertNoBuff(GameplayComponentWorld world, GameplayEntityId entity)
        {
            if (!world.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore))
                return;
            if (!buffStore.TryGet(entity, out GameplayComponentBuffSetComponent buffs))
                return;

            Assert.IsFalse(buffs.TryGet(BuffId, out _));
        }

        private static GameplaySystemContext CreateContext(GameplayComponentWorld world, long frame)
        {
            return new GameplaySystemContext(
                new RuntimeFrame(frame),
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

        private static CombatBodyId Body(int value)
        {
            return new CombatBodyId(value);
        }

        private static FixVector3 Position(int x)
        {
            return new FixVector3(Fix64.FromInt(x), Fix64.Zero, Fix64.Zero);
        }

        private sealed class AuraFixture
        {
            private readonly GameplayComponentBuffCleanupSystem _cleanup;
            private readonly GameplayBuffApplicationSystem _application;
            private readonly GameplayBuffEffectProjectionSystem _projection;
            private readonly GameplayComponentWorld _world;

            public AuraFixture(
                CombatGameplayAuraBuffSystem system,
                GameplayComponentBuffCleanupSystem cleanup,
                GameplayBuffApplicationSystem application,
                GameplayBuffEffectProjectionSystem projection,
                GameplayComponentWorld world)
            {
                System = system;
                _cleanup = cleanup;
                _application = application;
                _projection = projection;
                _world = world;
            }

            public CombatGameplayAuraBuffSystem System { get; }

            public void Tick(long frame)
            {
                GameplaySystemContext context = CreateContext(_world, frame);
                _cleanup.Tick(context);
                System.Tick(context);
                _application.Tick(context);
                _projection.Tick(context);
            }
        }

        private sealed class DictionaryAuraAnchorProvider : ICombatGameplayAuraAnchorProvider
        {
            private readonly Dictionary<int, FixVector3> _centers = new Dictionary<int, FixVector3>();

            public void Set(CombatEntityId entityId, FixVector3 center)
            {
                _centers[entityId.Value] = center;
            }

            public bool TryGetAuraCenter(CombatEntityId sourceCombatId, out FixVector3 center)
            {
                return _centers.TryGetValue(sourceCombatId.Value, out center);
            }
        }
    }
}

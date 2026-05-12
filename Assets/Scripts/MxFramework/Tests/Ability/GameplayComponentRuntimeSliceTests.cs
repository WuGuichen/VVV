using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentRuntimeSliceTests
    {
        private const int HeroDefinitionId = 19001;
        private const int EnemyDefinitionId = 19002;
        private const int StrikeAbilityId = 39001;
        private const int HpAttributeId = 1;
        private const int ManaAttributeId = 2;
        private const int AttackAttributeId = 3;

        [Test]
        public void ComponentRuntimeSlice_SpawnCastRulesAndCleanupFlow()
        {
            using (SliceFixture fixture = SliceFixture.Create())
            {
                long emptyHash = fixture.ComputeHash();
                SpawnActors(fixture, out GameplayEntityId hero, out GameplayEntityId enemy);
                long spawnHash = fixture.ComputeHash();

                Assert.AreNotEqual(emptyHash, spawnHash);
                Assert.AreEqual(30, GetCurrent(fixture.World, hero, HpAttributeId));
                Assert.AreEqual(12, GetCurrent(fixture.World, enemy, HpAttributeId));

                EnqueueStrike(fixture, 1, hero, enemy);
                fixture.Tick(1);
                long firstCastHash = fixture.ComputeHash();

                Assert.AreNotEqual(spawnHash, firstCastHash);
                Assert.AreEqual(6, GetCurrent(fixture.World, enemy, HpAttributeId));
                Assert.AreEqual(7, GetCurrent(fixture.World, hero, ManaAttributeId));
                AssertCooldown(fixture.World, hero, StrikeAbilityId, new RuntimeFrame(1), 2L);

                EnqueueStrike(fixture, 2, hero, enemy);
                fixture.Tick(2);
                long cooldownRejectedHash = fixture.ComputeHash();

                Assert.AreEqual(firstCastHash, cooldownRejectedHash);
                Assert.AreEqual(6, GetCurrent(fixture.World, enemy, HpAttributeId));
                Assert.AreEqual(7, GetCurrent(fixture.World, hero, ManaAttributeId));

                EnqueueStrike(fixture, 3, hero, enemy);
                fixture.Tick(3);

                Assert.AreEqual(0, GetCurrent(fixture.World, enemy, HpAttributeId));
                Assert.AreEqual(4, GetCurrent(fixture.World, hero, ManaAttributeId));

                MarkPendingDestroy(fixture.World, enemy);
                long pendingDestroyHash = fixture.ComputeHash();
                fixture.Tick(4);
                long cleanupHash = fixture.ComputeHash();

                Assert.AreNotEqual(pendingDestroyHash, cleanupHash);
                Assert.IsFalse(fixture.World.IsAlive(enemy));
                AssertRegisteredStoresDoNotContain(fixture.World, enemy);
            }
        }

        [Test]
        public void ComponentRuntimeSlice_EventsAreEmittedInStableOrder()
        {
            using (SliceFixture fixture = SliceFixture.Create())
            {
                SpawnActors(fixture, out GameplayEntityId hero, out GameplayEntityId enemy);

                var spawnEvents = new List<GameplayRuntimeEvent>();
                Assert.AreEqual(2, fixture.Module.DrainEvents(RuntimeFrame.Zero, spawnEvents));
                Assert.AreEqual(GameplayRuntimeEventType.ComponentEntityCreated, spawnEvents[0].Type);
                Assert.AreEqual(hero, spawnEvents[0].ComponentEntityId);
                Assert.AreEqual(GameplayRuntimeEventType.ComponentEntityCreated, spawnEvents[1].Type);
                Assert.AreEqual(enemy, spawnEvents[1].ComponentEntityId);

                EnqueueStrike(fixture, 1, hero, enemy);
                fixture.Tick(1);

                var castEvents = new List<GameplayRuntimeEvent>();
                Assert.AreEqual(3, fixture.Module.DrainEvents(new RuntimeFrame(1), castEvents));
                Assert.AreEqual(GameplayRuntimeEventType.ComponentAttributeChanged, castEvents[0].Type);
                Assert.AreEqual(GameplayComponentAbilityEvents.AbilityCostCommittedReason, castEvents[0].Reason);
                Assert.AreEqual(hero, castEvents[0].ComponentEntityId);
                Assert.AreEqual(ManaAttributeId, castEvents[0].AttributeId);
                Assert.AreEqual(GameplayRuntimeEventType.ComponentAttributeChanged, castEvents[1].Type);
                Assert.AreEqual(GameplayAttributeEvents.AddAttributeReason, castEvents[1].Reason);
                Assert.AreEqual(enemy, castEvents[1].ComponentEntityId);
                Assert.AreEqual(HpAttributeId, castEvents[1].AttributeId);
                Assert.AreEqual(GameplayRuntimeEventType.AbilityCastSucceeded, castEvents[2].Type);
                Assert.AreEqual(GameplayComponentAbilityEvents.CastComponentAbilityReason, castEvents[2].Reason);
                Assert.AreEqual(enemy, castEvents[2].ComponentEntityId);

                MarkPendingDestroy(fixture.World, enemy);
                fixture.Tick(2);

                var cleanupEvents = new List<GameplayRuntimeEvent>();
                Assert.AreEqual(1, fixture.Module.DrainEvents(new RuntimeFrame(2), cleanupEvents));
                Assert.AreEqual(GameplayRuntimeEventType.ComponentEntityDestroyed, cleanupEvents[0].Type);
                Assert.AreEqual(GameplayLifecycleEvents.PendingDestroyCleanupReason, cleanupEvents[0].Reason);
                Assert.AreEqual(enemy, cleanupEvents[0].ComponentEntityId);
            }
        }

        [Test]
        public void ComponentRuntimeSlice_CooldownRejectDoesNotApplyEffect()
        {
            using (SliceFixture fixture = SliceFixture.Create())
            {
                SpawnActors(fixture, out GameplayEntityId hero, out GameplayEntityId enemy);
                fixture.Module.DrainEvents(RuntimeFrame.Zero, new List<GameplayRuntimeEvent>());
                EnqueueStrike(fixture, 1, hero, enemy);
                fixture.Tick(1);
                fixture.Module.DrainEvents(new RuntimeFrame(1), new List<GameplayRuntimeEvent>());
                long beforeRejected = fixture.ComputeHash();

                EnqueueStrike(fixture, 2, hero, enemy);
                fixture.Tick(2);

                Assert.AreEqual(beforeRejected, fixture.ComputeHash());
                Assert.AreEqual(6, GetCurrent(fixture.World, enemy, HpAttributeId));
                Assert.AreEqual(7, GetCurrent(fixture.World, hero, ManaAttributeId));
                var events = new List<GameplayRuntimeEvent>();
                Assert.AreEqual(1, fixture.Module.DrainEvents(new RuntimeFrame(2), events));
                Assert.AreEqual(GameplayRuntimeEventType.AbilityCastFailed, events[0].Type);
                Assert.AreEqual(GameplayComponentAbilityEvents.AbilityOnCooldownReason, events[0].Reason);
                Assert.AreEqual(hero, events[0].ComponentEntityId);
            }
        }

        [Test]
        public void ComponentRuntimeSlice_SaveStateRoundtripRestoresHash()
        {
            using (SliceFixture fixture = SliceFixture.Create())
            {
                SpawnActors(fixture, out GameplayEntityId hero, out GameplayEntityId enemy);
                EnqueueStrike(fixture, 1, hero, enemy);
                fixture.Tick(1);
                long saveHash = fixture.ComputeHash();

                RuntimeSaveState saveState = CaptureJsonRoundtrip(fixture.World);
                GameplayComponentWorld restored = SliceFixture.CreateWorld();
                RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(restored).RestoreSaveState(saveState);

                Assert.IsTrue(restore.Success, restore.Error.ToString());
                Assert.AreEqual(saveHash, ComputeHash(restored));
                Assert.AreEqual(30, GetCurrent(restored, hero, HpAttributeId));
                Assert.AreEqual(6, GetCurrent(restored, enemy, HpAttributeId));
                Assert.AreEqual(7, GetCurrent(restored, hero, ManaAttributeId));
                AssertCooldown(restored, hero, StrikeAbilityId, new RuntimeFrame(1), 2L);
            }
        }

        [Test]
        public void ComponentRuntimeSlice_CommandDrivenFlowProducesStableHash()
        {
            long first = RunDeterministicFlow();
            long second = RunDeterministicFlow();

            Assert.AreEqual(first, second);
        }

        [Test]
        public void ComponentRuntimeSlice_RequestStoreIsNotSaved()
        {
            using (SliceFixture fixture = SliceFixture.Create())
            {
                SpawnActors(fixture, out GameplayEntityId hero, out GameplayEntityId enemy);
                AddStrikeRequest(fixture, hero, enemy);
                Assert.AreEqual(1, fixture.RequestStore.Count);
                long beforeSaveHash = fixture.ComputeHash();

                string json = RuntimeSaveStateJson.SaveToJson(
                    new GameplayComponentWorldSaveStateProvider(fixture.World).CaptureSaveState().Value);
                RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(json);
                Assert.IsTrue(loaded.Success, loaded.Error.ToString());
                GameplayComponentWorld restored = SliceFixture.CreateWorld();
                RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(restored).RestoreSaveState(loaded.Value);

                Assert.IsTrue(restore.Success, restore.Error.ToString());
                Assert.AreEqual(beforeSaveHash, ComputeHash(restored));
                Assert.IsFalse(json.Contains("AbilityRequest"));
                Assert.IsFalse(json.Contains("componentAbilityRequest"));
            }
        }

        [Test]
        public void ComponentRuntimeSlice_RestoreThenContinueCastWorksWithRuntimeRegistries()
        {
            using (SliceFixture fixture = SliceFixture.Create())
            {
                SpawnActors(fixture, out GameplayEntityId hero, out GameplayEntityId enemy);
                RuntimeSaveState saveState = CaptureJsonRoundtrip(fixture.World);
                GameplayComponentWorld restored = SliceFixture.CreateWorld();
                RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(restored).RestoreSaveState(saveState);
                Assert.IsTrue(restore.Success, restore.Error.ToString());

                using (SliceFixture continuation = SliceFixture.Create(restored))
                {
                    EnqueueStrike(continuation, 1, hero, enemy);
                    continuation.Tick(1);

                    Assert.AreEqual(6, GetCurrent(restored, enemy, HpAttributeId));
                    Assert.AreEqual(7, GetCurrent(restored, hero, ManaAttributeId));
                }
            }
        }

        private static long RunDeterministicFlow()
        {
            using (SliceFixture fixture = SliceFixture.Create())
            {
                SpawnActors(fixture, out GameplayEntityId hero, out GameplayEntityId enemy);
                EnqueueStrike(fixture, 1, hero, enemy);
                fixture.Tick(1);
                EnqueueStrike(fixture, 3, hero, enemy);
                fixture.Tick(3);
                MarkPendingDestroy(fixture.World, enemy);
                fixture.Tick(4);
                return fixture.ComputeHash();
            }
        }

        private static void SpawnActors(SliceFixture fixture, out GameplayEntityId hero, out GameplayEntityId enemy)
        {
            fixture.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.SpawnComponentEntity(
                RuntimeFrame.Zero,
                HeroDefinitionId,
                traceId: "spawn-hero"));
            fixture.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.SpawnComponentEntity(
                RuntimeFrame.Zero,
                EnemyDefinitionId,
                traceId: "spawn-enemy"));
            fixture.Tick(0);

            GameplayEntityId[] entities = fixture.World.CreateEntitySnapshot();
            Assert.AreEqual(2, entities.Length);
            hero = entities[0];
            enemy = entities[1];
        }

        private static void EnqueueStrike(SliceFixture fixture, long frame, GameplayEntityId hero, GameplayEntityId enemy)
        {
            GameplayComponentAbilityRequestHandle handle = AddStrikeRequest(fixture, hero, enemy);
            fixture.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbilityRequest(
                new RuntimeFrame(frame),
                handle,
                StrikeAbilityId,
                traceId: "strike"));
        }

        private static GameplayComponentAbilityRequestHandle AddStrikeRequest(
            SliceFixture fixture,
            GameplayEntityId hero,
            GameplayEntityId enemy)
        {
            var query = new GameplayComponentTargetQuery(
                hero,
                casterTeamId: 1,
                relationFilter: GameplayTargetRelationFilter.Enemy,
                maxTargets: 1);
            return fixture.RequestStore.Add(new GameplayComponentAbilityRequest(
                hero,
                StrikeAbilityId,
                new[] { enemy },
                query));
        }

        private static void MarkPendingDestroy(GameplayComponentWorld world, GameplayEntityId entityId)
        {
            world.GetOrCreateStore<GameplayLifecycleComponent>().Set(entityId, GameplayLifecycleComponent.PendingDestroy);
        }

        private static int GetCurrent(GameplayComponentWorld world, GameplayEntityId entityId, int attributeId)
        {
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store));
            Assert.IsTrue(store.TryGet(entityId, out GameplayAttributeSetComponent attributes));
            return attributes.GetCurrentValueOrDefault(attributeId);
        }

        private static void AssertCooldown(
            GameplayComponentWorld world,
            GameplayEntityId entityId,
            int abilityId,
            RuntimeFrame frame,
            long expectedRemainingFrames)
        {
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAbilityCooldownComponent> store));
            Assert.IsTrue(store.TryGet(entityId, out GameplayAbilityCooldownComponent cooldown));
            Assert.AreEqual(expectedRemainingFrames, cooldown.GetRemainingFrames(abilityId, frame));
        }

        private static void AssertRegisteredStoresDoNotContain(GameplayComponentWorld world, GameplayEntityId entityId)
        {
            AssertStoreMissing<GameplayIdentityComponent>(world, entityId);
            AssertStoreMissing<GameplayTeamComponent>(world, entityId);
            AssertStoreMissing<GameplayLifecycleComponent>(world, entityId);
            AssertStoreMissing<GameplayAttributeSetComponent>(world, entityId);
            AssertStoreMissing<GameplayAbilityCooldownComponent>(world, entityId);
        }

        private static void AssertStoreMissing<T>(GameplayComponentWorld world, GameplayEntityId entityId)
            where T : struct, IGameplayComponent
        {
            if (world.TryGetStore(out GameplayComponentStore<T> store))
                Assert.IsFalse(store.Contains(entityId));
        }

        private static RuntimeSaveState CaptureJsonRoundtrip(GameplayComponentWorld world)
        {
            RuntimeSaveStateResult<RuntimeSaveState> save = new GameplayComponentWorldSaveStateProvider(world).CaptureSaveState();
            Assert.IsTrue(save.Success, save.Error.ToString());
            RuntimeSaveStateResult<RuntimeSaveState> load = RuntimeSaveStateJson.LoadFromJson(RuntimeSaveStateJson.SaveToJson(save.Value));
            Assert.IsTrue(load.Success, load.Error.ToString());
            return load.Value;
        }

        private static long ComputeHash(GameplayComponentWorld world)
        {
            return RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new GameplayComponentWorldHashContributor(world) });
        }

        private sealed class SliceFixture : System.IDisposable
        {
            private SliceFixture(
                GameplayComponentWorld world,
                GameplayComponentSpawnRegistry spawnRegistry,
                GameplayComponentAbilityRegistry abilityRegistry,
                GameplayComponentAbilityRequestStore requestStore,
                RuntimeCommandBuffer commandBuffer,
                GameplayRuntimeModule module,
                RuntimeHost host)
            {
                World = world;
                SpawnRegistry = spawnRegistry;
                AbilityRegistry = abilityRegistry;
                RequestStore = requestStore;
                CommandBuffer = commandBuffer;
                Module = module;
                Host = host;
            }

            public GameplayComponentWorld World { get; }
            public GameplayComponentSpawnRegistry SpawnRegistry { get; }
            public GameplayComponentAbilityRegistry AbilityRegistry { get; }
            public GameplayComponentAbilityRequestStore RequestStore { get; }
            public RuntimeCommandBuffer CommandBuffer { get; }
            public GameplayRuntimeModule Module { get; }
            public RuntimeHost Host { get; }

            public static SliceFixture Create(GameplayComponentWorld world = null)
            {
                GameplayComponentWorld componentWorld = world ?? CreateWorld();
                GameplayComponentSpawnRegistry spawnRegistry = CreateSpawnRegistry();
                GameplayComponentAbilityRegistry abilityRegistry = CreateAbilityRegistry();
                var requestStore = new GameplayComponentAbilityRequestStore();
                var commandBuffer = new RuntimeCommandBuffer();
                var module = new GameplayRuntimeModule(
                    new GameplayWorld(),
                    new GameplayAbilityRegistry(),
                    commandBuffer,
                    tickWorldAutomatically: false,
                    configureDefaultPipeline: pipeline =>
                    {
                        pipeline.Add(new GameplayComponentSpawnCommandSystem(spawnRegistry));
                        pipeline.Add(new GameplayAttributeCommandSystem());
                        pipeline.Add(new GameplayComponentAbilityCommandSystem(
                            abilityRegistry,
                            requestStore,
                            new GameplayComponentTargetingService()));
                        pipeline.Add(new GameplayLifecycleCleanupSystem());
                    },
                    componentWorld: componentWorld);
                var host = new RuntimeHost();
                host.RegisterModule(module);
                host.Initialize();
                host.Start();

                return new SliceFixture(
                    componentWorld,
                    spawnRegistry,
                    abilityRegistry,
                    requestStore,
                    commandBuffer,
                    module,
                    host);
            }

            public static GameplayComponentWorld CreateWorld()
            {
                var world = new GameplayComponentWorld();
                GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
                GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
                GameplayCoreComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
                GameplayAttributeComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
                GameplayAttributeComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
                GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
                GameplayAbilityCooldownComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
                GameplayAbilityCooldownComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
                GameplayAbilityCooldownComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
                return world;
            }

            public void Tick(long frame)
            {
                Host.Tick(new RuntimeTickContext(frame, 0d, 0d, RuntimeTickStage.Simulation));
            }

            public long ComputeHash()
            {
                return GameplayComponentRuntimeSliceTests.ComputeHash(World);
            }

            public void Dispose()
            {
                Host.Dispose();
            }

            private static GameplayComponentSpawnRegistry CreateSpawnRegistry()
            {
                var registry = new GameplayComponentSpawnRegistry();
                registry.Register(CreateActorDefinition(
                    HeroDefinitionId,
                    "mxframework.gameplay.test.slice.hero",
                    teamId: 1,
                    hp: 30,
                    mana: 10,
                    attack: 6));
                registry.Register(CreateActorDefinition(
                    EnemyDefinitionId,
                    "mxframework.gameplay.test.slice.enemy",
                    teamId: 2,
                    hp: 12,
                    mana: 0,
                    attack: 0));
                return registry;
            }

            private static GameplayComponentSpawnDefinition CreateActorDefinition(
                int definitionId,
                string stableId,
                int teamId,
                int hp,
                int mana,
                int attack)
            {
                return new GameplayComponentSpawnDefinition(
                    definitionId,
                    stableId,
                    1,
                    new IGameplayComponentSpawnInitializer[]
                    {
                        new GameplayComponentSpawnInitializer<GameplayIdentityComponent>(
                            GameplayCoreComponentSchemaDescriptors.IdentityStableId,
                            new GameplayIdentityComponent(definitionId)),
                        new GameplayComponentSpawnInitializer<GameplayTeamComponent>(
                            GameplayCoreComponentSchemaDescriptors.TeamStableId,
                            new GameplayTeamComponent(teamId)),
                        new GameplayComponentSpawnInitializer<GameplayLifecycleComponent>(
                            GameplayCoreComponentSchemaDescriptors.LifecycleStableId,
                            GameplayLifecycleComponent.Alive),
                        new GameplayComponentSpawnInitializer<GameplayAttributeSetComponent>(
                            GameplayAttributeComponentSchemaDescriptors.AttributesStableId,
                            new GameplayAttributeSetComponent(
                                new GameplayAttributeValue(HpAttributeId, hp, hp),
                                new GameplayAttributeValue(ManaAttributeId, mana, mana),
                                new GameplayAttributeValue(AttackAttributeId, attack, attack)))
                    });
            }

            private static GameplayComponentAbilityRegistry CreateAbilityRegistry()
            {
                var registry = new GameplayComponentAbilityRegistry();
                registry.Register(new GameplayComponentAttributeDeltaAbility(
                    StrikeAbilityId,
                    HpAttributeId,
                    -6,
                    GameplayComponentTargetMode.ExplicitSingle,
                    new GameplayComponentAbilityRuleSet(
                        cooldownFrames: 2,
                        costs: new[] { new GameplayAbilityCost(ManaAttributeId, 3) })));
                return registry;
            }
        }
    }
}

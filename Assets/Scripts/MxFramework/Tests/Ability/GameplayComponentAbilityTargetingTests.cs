using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentAbilityTargetingTests
    {
        private const int Hp = 1;
        private const int AbilityStrike = 300001;
        private const int TagBoss = 10;
        private const int StatusShielded = 20;

        [Test]
        public void ComponentTargeting_SelectsSelfByGenerationEntity()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, 1, 100);
            var candidates = new List<GameplayComponentTargetCandidate>();
            GameplayComponentTargetCandidates.CopyFromWorld(world, candidates);
            var query = new GameplayComponentTargetQuery(caster, 1, relationFilter: GameplayTargetRelationFilter.Self);

            GameplayComponentTargetingResult result = new GameplayComponentTargetingService().Select(query, candidates);

            Assert.AreEqual(1, result.SelectedCount);
            Assert.AreEqual(caster, result.SelectedTargets[0].EntityId);
        }

        [Test]
        public void ComponentTargeting_FiltersDeadLifecycle()
        {
            GameplayEntityId caster = new GameplayEntityId(1, 1);
            var candidates = new[]
            {
                new GameplayComponentTargetCandidate(caster, 1, GameplayLifecycleState.PendingDestroy)
            };
            var query = new GameplayComponentTargetQuery(caster, 1, requireAlive: true);

            GameplayComponentTargetingResult result = new GameplayComponentTargetingService().Select(query, candidates);

            Assert.AreEqual(0, result.SelectedCount);
            Assert.AreEqual(GameplayTargetRejectReason.Dead, result.RejectedTargets[0].Reason);
        }

        [Test]
        public void ComponentTargeting_FiltersSameTeamAndEnemy()
        {
            GameplayEntityId caster = new GameplayEntityId(1, 1);
            GameplayEntityId ally = new GameplayEntityId(2, 1);
            GameplayEntityId enemy = new GameplayEntityId(3, 1);
            var candidates = new[]
            {
                new GameplayComponentTargetCandidate(ally, 1, GameplayLifecycleState.Alive),
                new GameplayComponentTargetCandidate(enemy, 2, GameplayLifecycleState.Alive)
            };

            GameplayComponentTargetingResult enemyResult = new GameplayComponentTargetingService().Select(
                new GameplayComponentTargetQuery(caster, 1, relationFilter: GameplayTargetRelationFilter.Enemy),
                candidates);
            GameplayComponentTargetingResult sameTeamResult = new GameplayComponentTargetingService().Select(
                new GameplayComponentTargetQuery(caster, 1, relationFilter: GameplayTargetRelationFilter.SameTeam),
                candidates);

            Assert.AreEqual(enemy, enemyResult.SelectedTargets[0].EntityId);
            Assert.AreEqual(GameplayTargetRejectReason.SameTeam, enemyResult.RejectedTargets[0].Reason);
            Assert.AreEqual(ally, sameTeamResult.SelectedTargets[0].EntityId);
            Assert.AreEqual(GameplayTargetRejectReason.DifferentTeam, sameTeamResult.RejectedTargets[0].Reason);
        }

        [Test]
        public void ComponentTargeting_FiltersRequiredTagsAndBlockedStatuses()
        {
            GameplayEntityId caster = new GameplayEntityId(1, 1);
            GameplayEntityId boss = new GameplayEntityId(2, 1);
            GameplayEntityId shielded = new GameplayEntityId(3, 1);
            GameplayEntityId missingTag = new GameplayEntityId(4, 1);
            var candidates = new[]
            {
                new GameplayComponentTargetCandidate(boss, 2, GameplayLifecycleState.Alive, new[] { TagBoss }, null),
                new GameplayComponentTargetCandidate(shielded, 2, GameplayLifecycleState.Alive, new[] { TagBoss }, new[] { StatusShielded }),
                new GameplayComponentTargetCandidate(missingTag, 2, GameplayLifecycleState.Alive)
            };
            var query = new GameplayComponentTargetQuery(
                caster,
                1,
                requiredTags: new[] { TagBoss },
                blockedStatuses: new[] { StatusShielded });

            GameplayComponentTargetingResult result = new GameplayComponentTargetingService().Select(query, candidates);

            Assert.AreEqual(1, result.SelectedCount);
            Assert.AreEqual(boss, result.SelectedTargets[0].EntityId);
            Assert.AreEqual(GameplayTargetRejectReason.BlockedStatus, result.RejectedTargets[0].Reason);
            Assert.AreEqual(GameplayTargetRejectReason.MissingRequiredTag, result.RejectedTargets[1].Reason);
        }

        [Test]
        public void ComponentTargeting_RespectsMaxTargets()
        {
            GameplayEntityId caster = new GameplayEntityId(1, 1);
            var candidates = new[]
            {
                new GameplayComponentTargetCandidate(new GameplayEntityId(2, 1), 2, GameplayLifecycleState.Alive),
                new GameplayComponentTargetCandidate(new GameplayEntityId(3, 1), 2, GameplayLifecycleState.Alive)
            };
            var query = new GameplayComponentTargetQuery(caster, 1, maxTargets: 1);

            GameplayComponentTargetingResult result = new GameplayComponentTargetingService().Select(query, candidates);

            Assert.AreEqual(1, result.SelectedCount);
            Assert.AreEqual(GameplayTargetRejectReason.MaxTargetsReached, result.RejectedTargets[0].Reason);
        }

        [Test]
        public void ComponentTargetCandidates_CopyFromWorldUsesStableEntityOrder()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId first = CreateActor(world, 1, 100);
            GameplayEntityId second = CreateActor(world, 2, 100);
            var candidates = new List<GameplayComponentTargetCandidate>();

            GameplayComponentTargetCandidates.CopyFromWorld(world, candidates);

            Assert.AreEqual(first, candidates[0].EntityId);
            Assert.AreEqual(second, candidates[1].EntityId);
        }

        [Test]
        public void AbilityRequestStore_RejectsStaleHandle()
        {
            var store = new GameplayComponentAbilityRequestStore();
            GameplayEntityId caster = new GameplayEntityId(1, 1);
            GameplayComponentAbilityRequestHandle handle = store.Add(new GameplayComponentAbilityRequest(caster, AbilityStrike));

            Assert.IsTrue(store.Remove(handle));

            Assert.IsFalse(store.TryGet(handle, out _));
            Assert.AreEqual(0, store.Count);
        }

        [Test]
        public void CastComponentAbilityRequest_ExplicitTargetUpdatesTargetAttribute()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, 1, 100);
            GameplayEntityId target = CreateActor(world, 2, 100);
            GameplayComponentAbilityRequestStore requestStore = CreateRequestStore(caster, target, out GameplayComponentAbilityRequestHandle handle);
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry(GameplayComponentTargetMode.ExplicitSingle), requestStore);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbilityRequest(
                RuntimeFrame.Zero,
                handle,
                AbilityStrike));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            AssertAttribute(world, caster, 100);
            AssertAttribute(world, target, 90);
            Assert.AreEqual(0, requestStore.Count);
        }

        [Test]
        public void CastComponentAbilityRequest_RejectsStaleTarget()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, 1, 100);
            GameplayEntityId target = CreateActor(world, 2, 100);
            Assert.IsTrue(world.DestroyEntity(target));
            GameplayComponentAbilityRequestStore requestStore = CreateRequestStore(caster, target, out GameplayComponentAbilityRequestHandle handle);
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry(GameplayComponentTargetMode.ExplicitSingle), requestStore);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbilityRequest(RuntimeFrame.Zero, handle, AbilityStrike));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.AbilityCastFailed, events[0].Type);
            Assert.AreEqual(GameplayComponentAbilityEvents.MissingTargetReason, events[0].Reason);
            Assert.AreEqual(0, requestStore.Count);
        }

        [Test]
        public void CastComponentAbilityRequest_RejectsNoValidTarget()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, 1, 100);
            GameplayEntityId ally = CreateActor(world, 1, 100);
            var query = new GameplayComponentTargetQuery(caster, 1, relationFilter: GameplayTargetRelationFilter.Enemy);
            var requestStore = new GameplayComponentAbilityRequestStore();
            GameplayComponentAbilityRequestHandle handle = requestStore.Add(new GameplayComponentAbilityRequest(
                caster,
                AbilityStrike,
                new[] { ally },
                query));
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry(GameplayComponentTargetMode.ExplicitSingle), requestStore);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbilityRequest(RuntimeFrame.Zero, handle, AbilityStrike));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayComponentAbilityEvents.NoValidTargetReason, events[0].Reason);
            Assert.AreEqual(0, requestStore.Count);
        }

        [Test]
        public void CastComponentAbilityRequest_RemovesRequestAfterHandled()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, 1, 100);
            GameplayEntityId target = CreateActor(world, 2, 100);
            GameplayComponentAbilityRequestStore requestStore = CreateRequestStore(caster, target, out GameplayComponentAbilityRequestHandle handle);
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry(GameplayComponentTargetMode.ExplicitSingle), requestStore);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbilityRequest(RuntimeFrame.Zero, handle, AbilityStrike));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(0, requestStore.Count);
            Assert.IsFalse(requestStore.TryGet(handle, out _));
        }

        [Test]
        public void CastComponentAbilityRequest_IsHandledBeforeUnsupportedSystem()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, 1, 100);
            GameplayEntityId target = CreateActor(world, 2, 100);
            GameplayComponentAbilityRequestStore requestStore = CreateRequestStore(caster, target, out GameplayComponentAbilityRequestHandle handle);
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry(GameplayComponentTargetMode.ExplicitSingle), requestStore);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbilityRequest(RuntimeFrame.Zero, handle, AbilityStrike));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(2, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreNotEqual(GameplayUnsupportedCommandSystem.UnsupportedReason, events[0].Reason);
            Assert.AreNotEqual(GameplayUnsupportedCommandSystem.UnsupportedReason, events[1].Reason);
        }

        [Test]
        public void CastComponentAbilityRequest_ChangesComponentWorldHash()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: true);
            GameplayEntityId caster = CreateActor(world, 1, 100);
            GameplayEntityId target = CreateActor(world, 2, 100);
            long before = ComputeHash(world);
            GameplayComponentAbilityRequestStore requestStore = CreateRequestStore(caster, target, out GameplayComponentAbilityRequestHandle handle);
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry(GameplayComponentTargetMode.ExplicitSingle), requestStore);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbilityRequest(RuntimeFrame.Zero, handle, AbilityStrike));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreNotEqual(before, ComputeHash(world));
        }

        [Test]
        public void CastComponentAbilityRequest_SaveStateRoundtripPreservesResultState()
        {
            GameplayComponentWorld source = CreateWorld(registerSchemas: true);
            GameplayEntityId caster = CreateActor(source, 1, 100);
            GameplayEntityId target = CreateActor(source, 2, 100);
            GameplayComponentAbilityRequestStore requestStore = CreateRequestStore(caster, target, out GameplayComponentAbilityRequestHandle handle);
            GameplayRuntimeModule module = CreateModule(source, CreateRegistry(GameplayComponentTargetMode.ExplicitSingle), requestStore);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbilityRequest(RuntimeFrame.Zero, handle, AbilityStrike));
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));
            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            GameplayComponentWorld restored = CreateWorld(registerSchemas: true);

            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(restored).RestoreSaveState(saveState);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(ComputeHash(source), ComputeHash(restored));
            AssertAttribute(restored, target, 90);
        }

        private static GameplayComponentAbilityRequestStore CreateRequestStore(
            GameplayEntityId caster,
            GameplayEntityId target,
            out GameplayComponentAbilityRequestHandle handle)
        {
            var requestStore = new GameplayComponentAbilityRequestStore();
            var query = new GameplayComponentTargetQuery(
                caster,
                1,
                relationFilter: GameplayTargetRelationFilter.Enemy,
                maxTargets: 1);
            handle = requestStore.Add(new GameplayComponentAbilityRequest(
                caster,
                AbilityStrike,
                new[] { target },
                query));
            return requestStore;
        }

        private static GameplayComponentAbilityRegistry CreateRegistry(GameplayComponentTargetMode targetMode)
        {
            var registry = new GameplayComponentAbilityRegistry();
            registry.Register(new GameplayComponentAttributeDeltaAbility(AbilityStrike, Hp, -10, targetMode));
            return registry;
        }

        private static GameplayEntityId CreateActor(GameplayComponentWorld world, int teamId, int hp)
        {
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayLifecycleComponent>().Set(entity, GameplayLifecycleComponent.Alive);
            world.GetOrCreateStore<GameplayTeamComponent>().Set(entity, new GameplayTeamComponent(teamId));
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(Hp, hp, hp)));
            return entity;
        }

        private static void AssertAttribute(GameplayComponentWorld world, GameplayEntityId entity, int expectedValue)
        {
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store));
            Assert.IsTrue(store.TryGet(entity, out GameplayAttributeSetComponent attributes));
            Assert.AreEqual(expectedValue, attributes.GetCurrentValueOrDefault(Hp));
        }

        private static GameplayComponentWorld CreateWorld(bool registerSchemas)
        {
            var world = new GameplayComponentWorld();
            if (registerSchemas)
            {
                GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
                GameplayCoreComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
                GameplayAttributeComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
                GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            }

            return world;
        }

        private static GameplayRuntimeModule CreateModule(
            GameplayComponentWorld world,
            GameplayComponentAbilityRegistry abilityRegistry,
            GameplayComponentAbilityRequestStore requestStore)
        {
            return new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                new RuntimeCommandBuffer(),
                tickWorldAutomatically: false,
                configureDefaultPipeline: pipeline =>
                {
                    pipeline.Add(new GameplayAttributeCommandSystem());
                    pipeline.Add(new GameplayComponentAbilityCommandSystem(
                        abilityRegistry,
                        requestStore,
                        new GameplayComponentTargetingService()));
                },
                componentWorld: world);
        }

        private static long ComputeHash(GameplayComponentWorld world)
        {
            return RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new GameplayComponentWorldHashContributor(world) });
        }
    }
}

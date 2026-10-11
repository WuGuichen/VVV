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
        public void ComponentTargetFilter_ValidatesRelationContextAndEvaluatesWithoutSnapshots()
        {
            var enemyFilter = new GameplayComponentTargetFilter(
                GameplayTargetIdentityTypes.Character,
                GameplayTargetCapabilities.Damageable,
                requireAlive: true,
                GameplayTargetRelationFilter.Enemy,
                referenceTeamId: 1);
            var classification =
                new GameplayTargetClassificationComponent(
                    GameplayTargetIdentityTypes.Character,
                    GameplayTargetCapabilities.Damageable
                        | GameplayTargetCapabilities.Movable);

            Assert.That(
                GameplayComponentTargetFilterEvaluator.TryAccept(
                    in enemyFilter,
                    new GameplayEntityId(2, 1),
                    candidateTeamId: 2,
                    GameplayLifecycleState.Alive,
                    in classification,
                    out GameplayTargetRejectReason acceptedReason,
                    out int acceptedDetail),
                Is.True);
            Assert.That(acceptedReason, Is.EqualTo(GameplayTargetRejectReason.None));
            Assert.That(acceptedDetail, Is.Zero);

            Assert.That(
                GameplayComponentTargetFilterEvaluator.TryAccept(
                    in enemyFilter,
                    new GameplayEntityId(3, 1),
                    candidateTeamId: 1,
                    GameplayLifecycleState.Alive,
                    in classification,
                    out GameplayTargetRejectReason sameTeamReason,
                    out int sameTeamDetail),
                Is.False);
            Assert.That(
                sameTeamReason,
                Is.EqualTo(GameplayTargetRejectReason.SameTeam));
            Assert.That(sameTeamDetail, Is.EqualTo(1));

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new GameplayComponentTargetFilter(
                    GameplayTargetIdentityTypes.Character,
                    GameplayTargetCapabilities.None,
                    requireAlive: true,
                    GameplayTargetRelationFilter.Enemy));
            Assert.Throws<System.ArgumentException>(
                () => new GameplayComponentTargetFilter(
                    GameplayTargetIdentityTypes.Character,
                    GameplayTargetCapabilities.None,
                    requireAlive: true,
                    GameplayTargetRelationFilter.Self));
        }

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
        public void ComponentTargeting_FiltersNeutralRelation()
        {
            GameplayEntityId caster = new GameplayEntityId(1, 1);
            GameplayEntityId sameTeam = new GameplayEntityId(2, 1);
            GameplayEntityId enemy = new GameplayEntityId(3, 1);
            GameplayEntityId neutral = new GameplayEntityId(4, 1);
            var candidates = new[]
            {
                new GameplayComponentTargetCandidate(
                    sameTeam,
                    1,
                    GameplayLifecycleState.Alive),
                new GameplayComponentTargetCandidate(
                    enemy,
                    2,
                    GameplayLifecycleState.Alive),
                new GameplayComponentTargetCandidate(
                    neutral,
                    0,
                    GameplayLifecycleState.Alive)
            };

            GameplayComponentTargetingResult result =
                new GameplayComponentTargetingService().Select(
                    new GameplayComponentTargetQuery(
                        caster,
                        1,
                        relationFilter:
                            GameplayTargetRelationFilter.Neutral),
                    candidates);

            Assert.AreEqual(1, result.SelectedCount);
            Assert.AreEqual(neutral, result.SelectedTargets[0].EntityId);
            Assert.AreEqual(
                GameplayTargetRejectReason.SameTeam,
                result.RejectedTargets[0].Reason);
            Assert.AreEqual(
                GameplayTargetRejectReason.DifferentTeam,
                result.RejectedTargets[1].Reason);
        }

        [Test]
        public void ComponentTargeting_FiltersIdentityAndRequiredCapabilities()
        {
            GameplayEntityId caster = new GameplayEntityId(1, 1);
            GameplayEntityId wrongIdentity = new GameplayEntityId(2, 1);
            GameplayEntityId missingCapability =
                new GameplayEntityId(3, 1);
            GameplayEntityId accepted = new GameplayEntityId(4, 1);
            var candidates = new[]
            {
                new GameplayComponentTargetCandidate(
                    wrongIdentity,
                    2,
                    GameplayLifecycleState.Alive,
                    classification:
                        new GameplayTargetClassificationComponent(
                            GameplayTargetIdentityTypes.WorldObject,
                            GameplayTargetCapabilities.Damageable
                                | GameplayTargetCapabilities.Movable)),
                new GameplayComponentTargetCandidate(
                    missingCapability,
                    2,
                    GameplayLifecycleState.Alive,
                    classification:
                        new GameplayTargetClassificationComponent(
                            GameplayTargetIdentityTypes.Character,
                            GameplayTargetCapabilities.Damageable)),
                new GameplayComponentTargetCandidate(
                    accepted,
                    2,
                    GameplayLifecycleState.Alive,
                    classification:
                        new GameplayTargetClassificationComponent(
                            GameplayTargetIdentityTypes.Character,
                            GameplayTargetCapabilities.Damageable
                                | GameplayTargetCapabilities.Movable))
            };
            var query = new GameplayComponentTargetQuery(
                caster,
                1,
                acceptedIdentityTypes:
                    GameplayTargetIdentityTypes.Character,
                requiredCapabilities:
                    GameplayTargetCapabilities.Damageable
                        | GameplayTargetCapabilities.Movable);

            GameplayComponentTargetingResult result =
                new GameplayComponentTargetingService().Select(
                    query,
                    candidates);

            Assert.AreEqual(1, result.SelectedCount);
            Assert.AreEqual(accepted, result.SelectedTargets[0].EntityId);
            Assert.AreEqual(
                GameplayTargetRejectReason.IdentityTypeMismatch,
                result.RejectedTargets[0].Reason);
            Assert.AreEqual(
                (int)GameplayTargetIdentityTypes.WorldObject,
                result.RejectedTargets[0].DetailId);
            Assert.AreEqual(
                GameplayTargetRejectReason.MissingRequiredCapability,
                result.RejectedTargets[1].Reason);
            Assert.AreEqual(
                (int)GameplayTargetCapabilities.Movable,
                result.RejectedTargets[1].DetailId);
        }

        [Test]
        public void ComponentTargeting_PreservesUnclassifiedCompatibilityUntilFiltered()
        {
            GameplayEntityId caster = new GameplayEntityId(1, 1);
            GameplayEntityId unclassified = new GameplayEntityId(2, 1);
            var candidates = new[]
            {
                new GameplayComponentTargetCandidate(
                    unclassified,
                    2,
                    GameplayLifecycleState.Alive)
            };
            var service = new GameplayComponentTargetingService();

            GameplayComponentTargetingResult compatible =
                service.Select(
                    new GameplayComponentTargetQuery(caster, 1),
                    candidates);
            GameplayComponentTargetingResult identityFiltered =
                service.Select(
                    new GameplayComponentTargetQuery(
                        caster,
                        1,
                        acceptedIdentityTypes:
                            GameplayTargetIdentityTypes.Character),
                    candidates);
            GameplayComponentTargetingResult capabilityFiltered =
                service.Select(
                    new GameplayComponentTargetQuery(
                        caster,
                        1,
                        requiredCapabilities:
                            GameplayTargetCapabilities.Damageable),
                    candidates);

            Assert.AreEqual(1, compatible.SelectedCount);
            Assert.AreEqual(0, identityFiltered.SelectedCount);
            Assert.AreEqual(
                GameplayTargetRejectReason.IdentityTypeMismatch,
                identityFiltered.RejectedTargets[0].Reason);
            Assert.AreEqual(0, capabilityFiltered.SelectedCount);
            Assert.AreEqual(
                GameplayTargetRejectReason.MissingRequiredCapability,
                capabilityFiltered.RejectedTargets[0].Reason);
        }

        [Test]
        public void ComponentTargetQuery_RejectsUndefinedSemanticFilters()
        {
            GameplayEntityId caster = new GameplayEntityId(1, 1);

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new GameplayComponentTargetQuery(
                    caster,
                    1,
                    relationFilter: (GameplayTargetRelationFilter)99));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new GameplayComponentTargetQuery(
                    caster,
                    1,
                    acceptedIdentityTypes:
                        (GameplayTargetIdentityTypes)(1 << 20)));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new GameplayComponentTargetQuery(
                    caster,
                    1,
                    requiredCapabilities:
                        (GameplayTargetCapabilities)(1 << 20)));
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
            Assert.AreEqual(
                GameplayTargetIdentityTypes.Character,
                candidates[0].Classification.IdentityType);
            Assert.AreEqual(
                GameplayTargetCapabilities.Damageable
                    | GameplayTargetCapabilities.Movable,
                candidates[0].Classification.Capabilities);
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
            world.GetOrCreateStore<
                    GameplayTargetClassificationComponent>()
                .Set(
                    entity,
                    new GameplayTargetClassificationComponent(
                        GameplayTargetIdentityTypes.Character,
                        GameplayTargetCapabilities.Damageable
                            | GameplayTargetCapabilities.Movable));
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

using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentAbilityRulesTests
    {
        private const int Hp = 1;
        private const int Mana = 2;
        private const int AbilityStrike = 300001;

        [Test]
        public void CooldownComponent_StartsAndReportsRemainingFrames()
        {
            var cooldown = new GameplayAbilityCooldownComponent()
                .Start(AbilityStrike, new RuntimeFrame(10), 5);

            Assert.AreEqual(1, cooldown.Count);
            Assert.IsTrue(cooldown.TryGetEndFrame(AbilityStrike, out long endFrame));
            Assert.AreEqual(15L, endFrame);
            Assert.AreEqual(5L, cooldown.GetRemainingFrames(AbilityStrike, new RuntimeFrame(10)));
            Assert.AreEqual(1L, cooldown.GetRemainingFrames(AbilityStrike, new RuntimeFrame(14)));
            Assert.AreEqual(0L, cooldown.GetRemainingFrames(AbilityStrike, new RuntimeFrame(15)));
        }

        [Test]
        public void CooldownComponent_RemoveExpiredClearsExpiredEntries()
        {
            var cooldown = new GameplayAbilityCooldownComponent(
                new GameplayAbilityCooldownEntry(AbilityStrike, 10),
                new GameplayAbilityCooldownEntry(AbilityStrike + 1, 12));

            GameplayAbilityCooldownComponent updated = cooldown.RemoveExpired(new RuntimeFrame(10));

            Assert.AreEqual(1, updated.Count);
            Assert.IsFalse(updated.TryGetEndFrame(AbilityStrike, out _));
            Assert.IsTrue(updated.TryGetEndFrame(AbilityStrike + 1, out long endFrame));
            Assert.AreEqual(12L, endFrame);
        }

        [Test]
        public void AbilityRules_EvaluateRejectsOnCooldown()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, hp: 100, mana: 10);
            world.GetOrCreateStore<GameplayAbilityCooldownComponent>().Set(
                caster,
                new GameplayAbilityCooldownComponent(new GameplayAbilityCooldownEntry(AbilityStrike, 5)));

            GameplayComponentAbilityRuleResult result = GameplayComponentAbilityRules.Evaluate(
                world,
                caster,
                AbilityStrike,
                new GameplayComponentAbilityRuleSet(cooldownFrames: 3),
                new RuntimeFrame(3));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(GameplayComponentAbilityFailureCode.OnCooldown, result.FailureCode);
            Assert.AreEqual(GameplayComponentAbilityEvents.AbilityOnCooldownReason, result.Reason);
            Assert.AreEqual(2L, result.RemainingFrames);
        }

        [Test]
        public void AbilityRules_EvaluateRejectsInsufficientCost()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, hp: 100, mana: 4);

            GameplayComponentAbilityRuleResult result = GameplayComponentAbilityRules.Evaluate(
                world,
                caster,
                AbilityStrike,
                new GameplayComponentAbilityRuleSet(costs: new[] { new GameplayAbilityCost(Mana, 5) }),
                RuntimeFrame.Zero);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(GameplayComponentAbilityFailureCode.InsufficientCost, result.FailureCode);
            Assert.AreEqual(GameplayComponentAbilityEvents.InsufficientCostReason, result.Reason);
            Assert.AreEqual(Mana, result.DetailId);
        }

        [Test]
        public void AbilityRules_CommitDeductsCostAndStartsCooldown()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, hp: 100, mana: 10);

            GameplayComponentAbilityRuleResult result = GameplayComponentAbilityRules.Commit(
                world,
                caster,
                AbilityStrike,
                new GameplayComponentAbilityRuleSet(
                    cooldownFrames: 3,
                    costs: new[] { new GameplayAbilityCost(Mana, 5) }),
                new RuntimeFrame(7),
                GameplayRuntimeCommandIds.CastComponentAbility,
                CharacterTestRuntimeTraceIds.Rule);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> attributesStore));
            Assert.IsTrue(attributesStore.TryGet(caster, out GameplayAttributeSetComponent attributes));
            Assert.AreEqual(5, attributes.GetCurrentValueOrDefault(Mana));
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAbilityCooldownComponent> cooldownStore));
            Assert.IsTrue(cooldownStore.TryGet(caster, out GameplayAbilityCooldownComponent cooldown));
            Assert.IsTrue(cooldown.TryGetEndFrame(AbilityStrike, out long endFrame));
            Assert.AreEqual(10L, endFrame);
        }

        [Test]
        public void CastComponentAbility_RuleRejectedDoesNotRunAbilityEffect()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, hp: 100, mana: 10);
            world.GetOrCreateStore<GameplayAbilityCooldownComponent>().Set(
                caster,
                new GameplayAbilityCooldownComponent(new GameplayAbilityCooldownEntry(AbilityStrike, 10)));
            GameplayRuntimeModule module = CreateModule(
                world,
                CreateRegistry(new GameplayComponentAbilityRuleSet(cooldownFrames: 3)));

            EnqueueCast(module, RuntimeFrame.Zero, caster);
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(100, GetCurrent(world, caster, Hp));
            Assert.AreEqual(10, GetCurrent(world, caster, Mana));
            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.AbilityCastFailed, events[0].Type);
            Assert.AreEqual(GameplayComponentAbilityEvents.AbilityOnCooldownReason, events[0].Reason);
        }

        [Test]
        public void CastComponentAbility_SuccessCommitsCostAndCooldown()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, hp: 100, mana: 10);
            GameplayRuntimeModule module = CreateModule(
                world,
                CreateRegistry(new GameplayComponentAbilityRuleSet(
                    cooldownFrames: 5,
                    costs: new[] { new GameplayAbilityCost(Mana, 3) })));

            EnqueueCast(module, RuntimeFrame.Zero, caster);
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(90, GetCurrent(world, caster, Hp));
            Assert.AreEqual(7, GetCurrent(world, caster, Mana));
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAbilityCooldownComponent> cooldownStore));
            Assert.IsTrue(cooldownStore.TryGet(caster, out GameplayAbilityCooldownComponent cooldown));
            Assert.AreEqual(5L, cooldown.GetRemainingFrames(AbilityStrike, RuntimeFrame.Zero));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(3, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.ComponentAttributeChanged, events[0].Type);
            Assert.AreEqual(GameplayComponentAbilityEvents.AbilityCostCommittedReason, events[0].Reason);
            Assert.AreEqual(Mana, events[0].AttributeId);
            Assert.AreEqual(-3, events[0].AttributeDelta);
            Assert.AreEqual(GameplayRuntimeEventType.ComponentAttributeChanged, events[1].Type);
            Assert.AreEqual(GameplayAttributeEvents.AddAttributeReason, events[1].Reason);
            Assert.AreEqual(GameplayRuntimeEventType.AbilityCastSucceeded, events[2].Type);
        }

        [Test]
        public void CastComponentAbility_EffectFailureDoesNotStartCooldownButCostIsNotRefunded()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, hp: 100, mana: 10);
            var registry = new GameplayComponentAbilityRegistry();
            registry.Register(new FailingComponentAbility(new GameplayComponentAbilityRuleSet(
                cooldownFrames: 5,
                costs: new[] { new GameplayAbilityCost(Mana, 3) })));
            GameplayRuntimeModule module = CreateModule(world, registry);

            EnqueueCast(module, RuntimeFrame.Zero, caster);
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(100, GetCurrent(world, caster, Hp));
            Assert.AreEqual(7, GetCurrent(world, caster, Mana));
            Assert.IsFalse(world.TryGetStore(out GameplayComponentStore<GameplayAbilityCooldownComponent> _));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(2, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayComponentAbilityEvents.AbilityCostCommittedReason, events[0].Reason);
            Assert.AreEqual(GameplayRuntimeEventType.AbilityCastFailed, events[1].Type);
            Assert.AreEqual(GameplayComponentAbilityEvents.EffectFailedReason, events[1].Reason);
        }

        [Test]
        public void CastComponentAbility_CooldownBlocksSecondCastUntilEndFrame()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, hp: 100, mana: 10);
            GameplayRuntimeModule module = CreateModule(
                world,
                CreateRegistry(new GameplayComponentAbilityRuleSet(cooldownFrames: 2)));

            EnqueueCast(module, RuntimeFrame.Zero, caster);
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));
            module.DrainEvents(RuntimeFrame.Zero, new List<GameplayRuntimeEvent>());

            EnqueueCast(module, new RuntimeFrame(1), caster);
            module.Tick(new RuntimeTickContext(1, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(90, GetCurrent(world, caster, Hp));
            var blocked = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(new RuntimeFrame(1), blocked));
            Assert.AreEqual(GameplayComponentAbilityEvents.AbilityOnCooldownReason, blocked[0].Reason);

            EnqueueCast(module, new RuntimeFrame(2), caster);
            module.Tick(new RuntimeTickContext(2, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(80, GetCurrent(world, caster, Hp));
        }

        [Test]
        public void CastComponentAbility_CostFailureDoesNotStartCooldown()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, hp: 100, mana: 2);
            GameplayRuntimeModule module = CreateModule(
                world,
                CreateRegistry(new GameplayComponentAbilityRuleSet(
                    cooldownFrames: 5,
                    costs: new[] { new GameplayAbilityCost(Mana, 3) })));

            EnqueueCast(module, RuntimeFrame.Zero, caster);
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(100, GetCurrent(world, caster, Hp));
            Assert.AreEqual(2, GetCurrent(world, caster, Mana));
            Assert.IsFalse(world.TryGetStore(out GameplayComponentStore<GameplayAbilityCooldownComponent> _));
            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayComponentAbilityEvents.InsufficientCostReason, events[0].Reason);
        }

        [Test]
        public void CooldownSchema_HashChangesWhenCooldownChanges()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: true);
            GameplayEntityId caster = CreateActor(world, hp: 100, mana: 10);
            long before = ComputeHash(world);
            world.GetOrCreateStore<GameplayAbilityCooldownComponent>().Set(
                caster,
                new GameplayAbilityCooldownComponent(new GameplayAbilityCooldownEntry(AbilityStrike, 5)));

            Assert.AreNotEqual(before, ComputeHash(world));
        }

        [Test]
        public void CooldownSchema_SaveStateRoundtripRestoresCooldown()
        {
            GameplayComponentWorld source = CreateWorld(registerSchemas: true);
            GameplayEntityId caster = CreateActor(source, hp: 100, mana: 10);
            source.GetOrCreateStore<GameplayAbilityCooldownComponent>().Set(
                caster,
                new GameplayAbilityCooldownComponent(new GameplayAbilityCooldownEntry(AbilityStrike, 5)));
            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);

            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(saveState);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(ComputeHash(source), ComputeHash(target));
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayAbilityCooldownComponent> cooldownStore));
            Assert.IsTrue(cooldownStore.TryGet(caster, out GameplayAbilityCooldownComponent cooldown));
            Assert.AreEqual(5L, cooldown.GetRemainingFrames(AbilityStrike, RuntimeFrame.Zero));
        }

        [Test]
        public void CastComponentAbilityRequest_RulesApplyToExplicitTargetCast()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, hp: 100, mana: 10);
            GameplayEntityId target = CreateActor(world, hp: 100, mana: 0);
            var requestStore = new GameplayComponentAbilityRequestStore();
            var request = new GameplayComponentAbilityRequest(
                caster,
                AbilityStrike,
                new[] { target },
                new GameplayComponentTargetQuery(caster, casterTeamId: 0, requireAlive: true));
            GameplayComponentAbilityRequestHandle handle = requestStore.Add(request);
            var registry = new GameplayComponentAbilityRegistry();
            registry.Register(new GameplayComponentAttributeDeltaAbility(
                AbilityStrike,
                Hp,
                -10,
                GameplayComponentTargetMode.ExplicitSingle,
                new GameplayComponentAbilityRuleSet(
                    cooldownFrames: 4,
                    costs: new[] { new GameplayAbilityCost(Mana, 5) })));
            GameplayRuntimeModule module = CreateModule(world, registry, requestStore);

            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbilityRequest(
                RuntimeFrame.Zero,
                handle,
                AbilityStrike));
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(90, GetCurrent(world, target, Hp));
            Assert.AreEqual(5, GetCurrent(world, caster, Mana));
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAbilityCooldownComponent> cooldownStore));
            Assert.IsTrue(cooldownStore.TryGet(caster, out GameplayAbilityCooldownComponent cooldown));
            Assert.AreEqual(4L, cooldown.GetRemainingFrames(AbilityStrike, RuntimeFrame.Zero));
        }

        private static GameplayComponentAbilityRegistry CreateRegistry(GameplayComponentAbilityRuleSet rules)
        {
            var registry = new GameplayComponentAbilityRegistry();
            registry.Register(new GameplayComponentAttributeDeltaAbility(
                AbilityStrike,
                Hp,
                -10,
                GameplayComponentTargetMode.Self,
                rules));
            return registry;
        }

        private static GameplayEntityId CreateActor(GameplayComponentWorld world, int hp, int mana)
        {
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayLifecycleComponent>().Set(entity, GameplayLifecycleComponent.Alive);
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(
                    new GameplayAttributeValue(Hp, hp, hp),
                    new GameplayAttributeValue(Mana, mana, mana)));
            return entity;
        }

        private static GameplayComponentWorld CreateWorld(bool registerSchemas)
        {
            var world = new GameplayComponentWorld();
            if (registerSchemas)
            {
                GameplayAttributeComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
                GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
                GameplayAbilityCooldownComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
                GameplayAbilityCooldownComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            }

            return world;
        }

        private static GameplayRuntimeModule CreateModule(
            GameplayComponentWorld world,
            GameplayComponentAbilityRegistry abilityRegistry,
            GameplayComponentAbilityRequestStore requestStore = null)
        {
            return new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                new RuntimeCommandBuffer(),
                tickWorldAutomatically: false,
                configureDefaultPipeline: pipeline =>
                {
                    pipeline.Add(new GameplayAttributeCommandSystem());
                    pipeline.Add(new GameplayComponentAbilityCommandSystem(abilityRegistry, requestStore));
                },
                componentWorld: world);
        }

        private static void EnqueueCast(GameplayRuntimeModule module, RuntimeFrame frame, GameplayEntityId caster)
        {
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility(
                frame,
                caster,
                AbilityStrike));
        }

        private static int GetCurrent(GameplayComponentWorld world, GameplayEntityId entity, int attributeId)
        {
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store));
            Assert.IsTrue(store.TryGet(entity, out GameplayAttributeSetComponent attributes));
            return attributes.GetCurrentValueOrDefault(attributeId);
        }

        private static long ComputeHash(GameplayComponentWorld world)
        {
            return RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new GameplayComponentWorldHashContributor(world) });
        }

        private sealed class FailingComponentAbility : IGameplayComponentAbility
        {
            public FailingComponentAbility(GameplayComponentAbilityRuleSet rules)
            {
                Rules = rules;
            }

            public int AbilityId => AbilityStrike;
            public GameplayComponentAbilityRuleSet Rules { get; }

            public GameplayComponentAbilityResult Cast(GameplayComponentAbilityContext context)
            {
                return GameplayComponentAbilityResult.Failed(
                    AbilityId,
                    context.CasterEntityId,
                    GameplayComponentAbilityFailureCode.EffectFailed,
                    GameplayComponentAbilityEvents.EffectFailedReason,
                    new[] { context.CasterEntityId });
            }
        }
    }

    public sealed class AbilityRuntimeModuleStateTests
    {
        private const int AbilityFireball = 910001;

        [Test]
        public void RuntimeModule_CooldownBlocksUntilEndFrameAndSnapshotIsSorted()
        {
            AbilityRuntimeModule module = CreateModule(cooldownFrames: 7);
            AbilityDefinitionConfig ability = GetAbility(module);

            Assert.IsTrue(module.TryResolveInputIntent(2, 920011, new RuntimeFrame(10), out AbilityActivationResult accepted));
            module.CommitActivation(2, accepted.Ability, new RuntimeFrame(10));

            Assert.IsFalse(module.TryResolveInputIntent(2, 920011, new RuntimeFrame(12), out AbilityActivationResult rejected));
            Assert.AreEqual("OnCooldown", rejected.Reason);
            Assert.AreEqual(5L, rejected.RemainingCooldownFrames);

            AbilityRuntimeState state = module.CreateStateSnapshot();
            Assert.AreEqual(AbilityRuntimeState.CurrentSchemaVersion, state.SchemaVersion);
            Assert.AreEqual(1, state.Cooldowns.Count);
            Assert.AreEqual(2, state.Cooldowns[0].CasterEntityId);
            Assert.AreEqual(AbilityFireball, state.Cooldowns[0].AbilityId);
            Assert.AreEqual(17L, state.Cooldowns[0].EndFrame);
            Assert.AreEqual(7L, module.GetRemainingCooldownFrames(2, ability, new RuntimeFrame(10)));

            module.Tick(new RuntimeTickContext(17, 0d, 0d, RuntimeTickStage.PreSimulation));

            Assert.AreEqual(0, module.ActiveCooldownCount);
            Assert.IsTrue(module.TryResolveInputIntent(2, 920011, new RuntimeFrame(17), out _));
        }

        [Test]
        public void RuntimeHash_StableAcrossCooldownInsertionOrderAndChangesWithState()
        {
            AbilityRuntimeModule first = CreateModule(cooldownFrames: 7);
            AbilityRuntimeModule second = CreateModule(cooldownFrames: 7);
            AbilityDefinitionConfig firstAbility = GetAbility(first);
            AbilityDefinitionConfig secondAbility = GetAbility(second);

            first.CommitActivation(2, firstAbility, RuntimeFrame.Zero);
            first.CommitActivation(1, firstAbility, new RuntimeFrame(1));
            second.CommitActivation(1, secondAbility, new RuntimeFrame(1));
            second.CommitActivation(2, secondAbility, RuntimeFrame.Zero);

            long firstHash = ComputeHash(first);
            long secondHash = ComputeHash(second);
            Assert.AreEqual(firstHash, secondHash);

            second.CommitActivation(3, secondAbility, RuntimeFrame.Zero);

            Assert.AreNotEqual(firstHash, ComputeHash(second));
        }

        [Test]
        public void SaveState_RoundtripRestoresCooldownStateAndHash()
        {
            AbilityRuntimeModule module = CreateModule(cooldownFrames: 7);
            AbilityDefinitionConfig ability = GetAbility(module);
            module.CommitActivation(2, ability, new RuntimeFrame(10));
            long originalHash = ComputeHash(module);

            RuntimeSaveState saveState = new AbilityRuntimeSaveStateProvider(module).CaptureSaveState().Value;
            RuntimeSaveStateResult<RuntimeSaveState> loaded =
                RuntimeSaveStateJson.LoadFromJson(RuntimeSaveStateJson.SaveToJson(saveState));
            Assert.IsTrue(loaded.Success, loaded.Error.ToString());

            AbilityRuntimeModule restored = CreateModule(cooldownFrames: 7);
            RuntimeSaveStateResult<bool> restore = new AbilityRuntimeSaveStateProvider(restored).RestoreSaveState(loaded.Value);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(originalHash, ComputeHash(restored));
            Assert.AreEqual(5L, restored.GetRemainingCooldownFrames(2, GetAbility(restored), new RuntimeFrame(12)));
        }

        [Test]
        public void SaveState_RestoreRejectsDefinitionHashMismatch()
        {
            AbilityRuntimeModule module = CreateModule(cooldownFrames: 7);
            module.CommitActivation(2, GetAbility(module), RuntimeFrame.Zero);
            RuntimeSaveState saveState = new AbilityRuntimeSaveStateProvider(module).CaptureSaveState().Value;

            AbilityRuntimeModule changedDefinitions = CreateModule(cooldownFrames: 8);
            RuntimeSaveStateResult<bool> restore =
                new AbilityRuntimeSaveStateProvider(changedDefinitions).RestoreSaveState(saveState);

            Assert.IsFalse(restore.Success, restore.Error.ToString());
            Assert.AreEqual(RuntimeSaveStateErrorCode.CustomStateMismatch, restore.Error.Code);
        }

        private static AbilityRuntimeModule CreateModule(long cooldownFrames)
        {
            var registry = new AbilityDefinitionRegistry();
            Assert.IsTrue(registry.TryRegister(new AbilityDefinitionConfig(
                AbilityFireball,
                "ability.character_test.fireball",
                "CharacterTest Fireball",
                new AbilityInputBindingConfig(920011, 920012, "AttackSecondary", "secondary"),
                new AbilityCooldownConfig(cooldownFrames, 920013, "character_test.fireball"),
                Array.Empty<AbilityCostConfig>(),
                920014,
                new AbilityActionBindingConfig(MxFramework.Tests.CharacterActionLegacyFixtureIds.From("fireball_impact"), MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id2),
                new AbilityDamageConfig(
                    920015,
                    baseDamage: 10,
                    impactPower: 8,
                    Array.Empty<int>(),
                    920016,
                    damageTypeLabel: "fire",
                    analysisCategoryLabel: "FireballImpact"),
                920017,
                targetPolicyLabel: "target.area_3m",
                presentationProfileLabel: "character_test.fireball.impact",
                effectTimelineId: 920018,
                effectTimelineLabel: "character_test.fireball.impact"), out string failure), failure);

            return new AbilityRuntimeModule(registry, "test.ability");
        }

        private static AbilityDefinitionConfig GetAbility(AbilityRuntimeModule module)
        {
            Assert.IsTrue(module.Definitions.TryGet(AbilityFireball, out AbilityDefinitionConfig ability));
            return ability;
        }

        private static long ComputeHash(AbilityRuntimeModule module)
        {
            return RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new AbilityRuntimeHashContributor(module) });
        }
    }
}

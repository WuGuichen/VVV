using System.Collections.Generic;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentAuraAbilityTests
    {
        private const int AbilityId = 401001;
        private const int AuraId = 501001;
        private const int ManaAttributeId = 7001;

        [Test]
        public void CastAuraAbility_UsesComponentAbilityRulesAndCreatesSource()
        {
            GameplayComponentWorld world = new GameplayComponentWorld();
            GameplayEntityId caster = CreateCaster(world, mana: 10);
            var registry = new GameplayComponentAbilityRegistry();
            registry.Register(new CombatGameplayAuraBuffAbility(
                AbilityId,
                AuraId,
                durationFrames: 5,
                rules: new GameplayComponentAbilityRuleSet(
                    cooldownFrames: 3,
                    costs: new[] { new GameplayAbilityCost(ManaAttributeId, 4) })));
            GameplayRuntimeModule module = CreateModule(world, registry);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility(
                new RuntimeFrame(2),
                caster,
                AbilityId,
                traceId: CharacterTestRuntimeTraceIds.AuraCast));

            module.Tick(new RuntimeTickContext(2, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayComponentAuraSourceSetComponent> auraStore));
            Assert.IsTrue(auraStore.TryGet(caster, out GameplayComponentAuraSourceSetComponent sources));
            Assert.IsTrue(sources.TryGet(AuraId, out GameplayComponentAuraSourceEntry source));
            Assert.AreEqual(7, source.EndFrame);
            Assert.AreEqual(AbilityId, source.SourceId);
            Assert.AreEqual(6, ReadMana(world, caster));
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAbilityCooldownComponent> cooldownStore));
            Assert.IsTrue(cooldownStore.Contains(caster));
        }

        [Test]
        public void CastAuraAbility_FailedCostDoesNotCreateSource()
        {
            GameplayComponentWorld world = new GameplayComponentWorld();
            GameplayEntityId caster = CreateCaster(world, mana: 2);
            var registry = new GameplayComponentAbilityRegistry();
            registry.Register(new CombatGameplayAuraBuffAbility(
                AbilityId,
                AuraId,
                durationFrames: 5,
                rules: new GameplayComponentAbilityRuleSet(
                    costs: new[] { new GameplayAbilityCost(ManaAttributeId, 4) })));
            GameplayRuntimeModule module = CreateModule(world, registry);
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility(
                RuntimeFrame.Zero,
                caster,
                AbilityId));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.IsFalse(world.TryGetStore(out GameplayComponentStore<GameplayComponentAuraSourceSetComponent> _));
            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.AbilityCastFailed, events[0].Type);
            Assert.AreEqual(GameplayComponentAbilityEvents.InsufficientCostReason, events[0].Reason);
        }

        private static GameplayEntityId CreateCaster(GameplayComponentWorld world, int mana)
        {
            GameplayEntityId caster = world.CreateEntity();
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                caster,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(ManaAttributeId, mana, mana)));
            return caster;
        }

        private static int ReadMana(GameplayComponentWorld world, GameplayEntityId caster)
        {
            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> attributeStore));
            Assert.IsTrue(attributeStore.TryGet(caster, out GameplayAttributeSetComponent attributes));
            return attributes.GetCurrentValueOrDefault(ManaAttributeId);
        }

        private static GameplayRuntimeModule CreateModule(
            GameplayComponentWorld world,
            GameplayComponentAbilityRegistry abilityRegistry)
        {
            return new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                new RuntimeCommandBuffer(),
                tickWorldAutomatically: false,
                configureDefaultPipeline: pipeline =>
                {
                    pipeline.Add(new GameplayComponentAbilityCommandSystem(abilityRegistry));
                },
                componentWorld: world);
        }
    }
}

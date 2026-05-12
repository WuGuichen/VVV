using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentAbilityCommandSystemTests
    {
        private const int Hp = 1;
        private const int AbilityStrike = 300001;

        [Test]
        public void ComponentAbilityRegistry_RejectsDuplicateAbilityId()
        {
            var registry = new GameplayComponentAbilityRegistry();
            registry.Register(CreateHpDeltaAbility(-10));

            Assert.Throws<System.InvalidOperationException>(() => registry.Register(CreateHpDeltaAbility(-5)));
        }

        [Test]
        public void ComponentAbilityRegistry_SnapshotSortsByAbilityId()
        {
            var registry = new GameplayComponentAbilityRegistry();
            registry.Register(new GameplayComponentAttributeDeltaAbility(300003, Hp, -1, GameplayComponentTargetMode.Self));
            registry.Register(new GameplayComponentAttributeDeltaAbility(300001, Hp, -1, GameplayComponentTargetMode.Self));
            registry.Register(new GameplayComponentAttributeDeltaAbility(300002, Hp, -1, GameplayComponentTargetMode.Self));

            IGameplayComponentAbility[] snapshot = registry.CreateSnapshot();

            Assert.AreEqual(300001, snapshot[0].AbilityId);
            Assert.AreEqual(300002, snapshot[1].AbilityId);
            Assert.AreEqual(300003, snapshot[2].AbilityId);
        }

        [Test]
        public void CastComponentAbility_SelfDeltaUpdatesAttribute()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, 100);
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry());
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility(
                RuntimeFrame.Zero,
                caster,
                AbilityStrike));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store));
            Assert.IsTrue(store.TryGet(caster, out GameplayAttributeSetComponent attributes));
            Assert.AreEqual(90, attributes.GetCurrentValueOrDefault(Hp));
        }

        [Test]
        public void CastComponentAbility_EmitsSuccessEventWithComponentEntityId()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, 100);
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry());
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility(
                RuntimeFrame.Zero,
                caster,
                AbilityStrike,
                traceId: "cast-component"));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(2, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.AbilityCastSucceeded, events[1].Type);
            Assert.AreEqual(GameplayComponentAbilityEvents.CastComponentAbilityReason, events[1].Reason);
            Assert.AreEqual(AbilityStrike, events[1].AbilityId);
            Assert.AreEqual(caster, events[1].ComponentEntityId);
            Assert.AreEqual("cast-component", events[1].TraceId);
        }

        [Test]
        public void CastComponentAbility_EmitsAttributeChangedEvent()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, 100);
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry());
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility(
                RuntimeFrame.Zero,
                caster,
                AbilityStrike));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(2, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.ComponentAttributeChanged, events[0].Type);
            Assert.AreEqual(GameplayRuntimeCommandIds.CastComponentAbility, events[0].CommandId);
            Assert.AreEqual(AbilityStrike, events[0].AbilityId);
            Assert.AreEqual(caster, events[0].ComponentEntityId);
            Assert.AreEqual(Hp, events[0].AttributeId);
            Assert.AreEqual(100, events[0].OldAttributeValue);
            Assert.AreEqual(90, events[0].NewAttributeValue);
            Assert.AreEqual(-10, events[0].AttributeDelta);
        }

        [Test]
        public void CastComponentAbility_RejectsMissingComponentWorld()
        {
            var command = new RuntimeCommand(
                RuntimeFrame.Zero,
                0,
                GameplayRuntimeCommandIds.CastComponentAbility,
                targetId: 1,
                payload0: 1,
                payload1: AbilityStrike);
            var events = new RuntimeEventQueue<GameplayRuntimeEvent>();
            var context = new GameplaySystemContext(
                RuntimeFrame.Zero,
                0d,
                0d,
                new GameplayWorld(),
                new[] { command },
                events);

            new GameplayComponentAbilityCommandSystem(CreateRegistry()).Tick(context);

            var drained = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, events.Drain(RuntimeFrame.Zero, drained));
            Assert.AreEqual(GameplayRuntimeEventType.AbilityCastFailed, drained[0].Type);
            Assert.AreEqual(GameplayComponentAbilityEvents.MissingComponentWorldReason, drained[0].Reason);
        }

        [Test]
        public void CastComponentAbility_RejectsStaleCaster()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, 100);
            Assert.IsTrue(world.DestroyEntity(caster));
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry());
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility(
                RuntimeFrame.Zero,
                caster,
                AbilityStrike));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.AbilityCastFailed, events[0].Type);
            Assert.AreEqual(GameplayComponentAbilityEvents.MissingCasterReason, events[0].Reason);
            Assert.AreEqual(caster, events[0].ComponentEntityId);
        }

        [Test]
        public void CastComponentAbility_RejectsMissingAbility()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, 100);
            GameplayRuntimeModule module = CreateModule(world, new GameplayComponentAbilityRegistry());
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility(
                RuntimeFrame.Zero,
                caster,
                AbilityStrike));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.AbilityCastFailed, events[0].Type);
            Assert.AreEqual(GameplayComponentAbilityEvents.MissingAbilityReason, events[0].Reason);
        }

        [Test]
        public void CastComponentAbility_RejectsExplicitCandidatePayload()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, 100);
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry());
            module.CommandBuffer.Enqueue(new RuntimeCommand(
                RuntimeFrame.Zero,
                0,
                GameplayRuntimeCommandIds.CastComponentAbility,
                targetId: caster.Index,
                payload0: caster.Generation,
                payload1: AbilityStrike,
                payload2: caster.Index));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.AbilityCastFailed, events[0].Type);
            Assert.AreEqual(GameplayComponentAbilityEvents.InvalidCommandPayloadReason, events[0].Reason);
        }

        [Test]
        public void CastComponentAbility_RejectsMissingAttributeSet()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = world.CreateEntity();
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry());
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility(
                RuntimeFrame.Zero,
                caster,
                AbilityStrike));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreEqual(GameplayRuntimeEventType.AbilityCastFailed, events[0].Type);
            Assert.AreEqual(GameplayComponentAbilityEvents.MissingAttributeSetReason, events[0].Reason);
        }

        [Test]
        public void CastComponentAbility_IsHandledBeforeUnsupportedSystem()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: false);
            GameplayEntityId caster = CreateActor(world, 100);
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry());
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility(
                RuntimeFrame.Zero,
                caster,
                AbilityStrike));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(2, module.DrainEvents(RuntimeFrame.Zero, events));
            Assert.AreNotEqual(GameplayUnsupportedCommandSystem.UnsupportedReason, events[0].Reason);
            Assert.AreNotEqual(GameplayUnsupportedCommandSystem.UnsupportedReason, events[1].Reason);
        }

        [Test]
        public void CastComponentAbility_ChangesComponentWorldHash()
        {
            GameplayComponentWorld world = CreateWorld(registerSchemas: true);
            GameplayEntityId caster = CreateActor(world, 100);
            long before = ComputeHash(world);
            GameplayRuntimeModule module = CreateModule(world, CreateRegistry());
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility(
                RuntimeFrame.Zero,
                caster,
                AbilityStrike));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreNotEqual(before, ComputeHash(world));
        }

        [Test]
        public void CastComponentAbility_SaveStateRoundtripPreservesResultState()
        {
            GameplayComponentWorld source = CreateWorld(registerSchemas: true);
            GameplayEntityId caster = CreateActor(source, 100);
            GameplayRuntimeModule module = CreateModule(source, CreateRegistry());
            module.CommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility(
                RuntimeFrame.Zero,
                caster,
                AbilityStrike));
            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));
            RuntimeSaveState saveState = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState().Value;
            GameplayComponentWorld target = CreateWorld(registerSchemas: true);

            RuntimeSaveStateResult<bool> restore = new GameplayComponentWorldSaveStateProvider(target).RestoreSaveState(saveState);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(ComputeHash(source), ComputeHash(target));
            Assert.IsTrue(target.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store));
            Assert.IsTrue(store.TryGet(caster, out GameplayAttributeSetComponent attributes));
            Assert.AreEqual(90, attributes.GetCurrentValueOrDefault(Hp));
        }

        private static GameplayComponentAbilityRegistry CreateRegistry()
        {
            var registry = new GameplayComponentAbilityRegistry();
            registry.Register(CreateHpDeltaAbility(-10));
            return registry;
        }

        private static GameplayComponentAttributeDeltaAbility CreateHpDeltaAbility(int delta)
        {
            return new GameplayComponentAttributeDeltaAbility(
                AbilityStrike,
                Hp,
                delta,
                GameplayComponentTargetMode.Self);
        }

        private static GameplayEntityId CreateActor(GameplayComponentWorld world, int hp)
        {
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(Hp, hp, hp)));
            return entity;
        }

        private static GameplayComponentWorld CreateWorld(bool registerSchemas)
        {
            var world = new GameplayComponentWorld();
            if (registerSchemas)
            {
                GameplayAttributeComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
                GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            }

            return world;
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
                    pipeline.Add(new GameplayAttributeCommandSystem());
                    pipeline.Add(new GameplayComponentAbilityCommandSystem(abilityRegistry));
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

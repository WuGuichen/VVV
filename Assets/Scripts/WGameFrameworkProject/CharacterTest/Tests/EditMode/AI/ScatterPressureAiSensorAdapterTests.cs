using MxFramework.AI;
using MxFramework.AI.GameplayBridge;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.AI
{
    public sealed class ScatterPressureAiSensorAdapterTests
    {
        [Test]
        public void Sense_WritesSelfAndTargetPressureFacts()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId self = world.CreateEntity();
            GameplayEntityId target = world.CreateEntity();
            world.GetOrCreateStore<GameplayScatterPressureComponent>().Set(self, new GameplayScatterPressureComponent(100, currentScatterPressure: 60));
            world.GetOrCreateStore<GameplayGuardPressureComponent>().Set(self, new GameplayGuardPressureComponent(100, currentPressure: 100));
            world.GetOrCreateStore<GameplayArmorIntegrityComponent>().Set(self, new GameplayArmorIntegrityComponent(80, 20));
            world.GetOrCreateStore<GameplayScatterPressureComponent>().Set(target, new GameplayScatterPressureComponent(200, currentScatterPressure: 100));
            world.GetOrCreateStore<GameplayGuardPressureComponent>().Set(target, new GameplayGuardPressureComponent(100, currentPressure: 0));
            world.GetOrCreateStore<GameplayArmorIntegrityComponent>().Set(target, new GameplayArmorIntegrityComponent(50, 0));
            var state = new AiWorldState();
            var adapter = new ScatterPressureAiSensorAdapter(world, _ => self, _ => target);

            adapter.Sense(new TestAiAgent(7, state), state);

            AssertFact(state, RuntimeAiGameplayPressureFactKeys.SelfScatterPressureBand, (int)PressureBand.Pressed);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.SelfScatterPressureRatio, 0.6f);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.SelfScatterPressureBroken, false);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.SelfGuardBand, (int)PressureBand.Broken);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.SelfGuardRatio, 1f);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.SelfGuardBroken, true);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.SelfArmorRatio, 0.25f);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.SelfArmorBroken, false);

            AssertFact(state, RuntimeAiGameplayPressureFactKeys.TargetScatterPressureBand, (int)PressureBand.Pressed);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.TargetScatterPressureRatio, 0.5f);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.TargetScatterPressureBroken, false);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.TargetGuardBand, (int)PressureBand.Stable);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.TargetGuardRatio, 0f);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.TargetGuardBroken, false);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.TargetArmorRatio, 0f);
            AssertFact(state, RuntimeAiGameplayPressureFactKeys.TargetArmorBroken, true);
            Assert.IsFalse(new ExploitScatterPressureWeaknessGoal().IsRelevant(state));
        }

        [Test]
        public void Sense_RemovesFactsWhenComponentIsMissing()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId self = world.CreateEntity();
            var state = new AiWorldState();
            state.SetValue(RuntimeAiGameplayPressureFactKeys.SelfScatterPressureBand, (int)PressureBand.Critical);
            state.SetValue(RuntimeAiGameplayPressureFactKeys.SelfScatterPressureRatio, 0.75f);
            state.SetValue(RuntimeAiGameplayPressureFactKeys.SelfScatterPressureBroken, false);
            state.SetValue(RuntimeAiGameplayPressureFactKeys.SelfGuardBand, (int)PressureBand.Pressed);
            world.GetOrCreateStore<GameplayGuardPressureComponent>().Set(self, new GameplayGuardPressureComponent(100, currentPressure: 25));
            var adapter = new ScatterPressureAiSensorAdapter(world, _ => self);

            adapter.Sense(new TestAiAgent(1, state), state);

            Assert.IsFalse(state.Contains(RuntimeAiGameplayPressureFactKeys.SelfScatterPressureBand));
            Assert.IsFalse(state.Contains(RuntimeAiGameplayPressureFactKeys.SelfScatterPressureRatio));
            Assert.IsFalse(state.Contains(RuntimeAiGameplayPressureFactKeys.SelfScatterPressureBroken));
            Assert.IsTrue(state.Contains(RuntimeAiGameplayPressureFactKeys.SelfGuardBand));
            Assert.IsFalse(state.Contains(RuntimeAiGameplayPressureFactKeys.TargetScatterPressureBand));
            Assert.IsFalse(state.Contains(RuntimeAiGameplayPressureFactKeys.TargetGuardBand));
            Assert.IsFalse(state.Contains(RuntimeAiGameplayPressureFactKeys.TargetArmorRatio));
        }

        [Test]
        public void Sense_CanUseExplicitGameplayWorldParameter()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId self = world.CreateEntity();
            world.GetOrCreateStore<GameplayScatterPressureComponent>().Set(
                self,
                new GameplayScatterPressureComponent(100, currentScatterPressure: 80));
            var state = new AiWorldState();
            var adapter = new ScatterPressureAiSensorAdapter(_ => self);

            adapter.Sense(new TestAiAgent(2, state), state, world);

            AssertFact(state, RuntimeAiPressureFactKeys.SelfScatterPressureBand, (int)PressureBand.Cracked);
        }

        [Test]
        public void Sense_RemovesFactsWhenResolvedEntityIsMissing()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId self = world.CreateEntity();
            var state = new AiWorldState();
            state.SetValue(RuntimeAiGameplayPressureFactKeys.SelfArmorRatio, 1f);
            state.SetValue(RuntimeAiGameplayPressureFactKeys.SelfArmorBroken, false);
            var adapter = new ScatterPressureAiSensorAdapter(world, _ => self);

            world.DestroyEntity(self);
            adapter.Sense(new TestAiAgent(1, state), state);

            Assert.IsFalse(state.Contains(RuntimeAiGameplayPressureFactKeys.SelfArmorRatio));
            Assert.IsFalse(state.Contains(RuntimeAiGameplayPressureFactKeys.SelfArmorBroken));
        }

        private static void AssertFact<T>(IAiWorldState worldState, AiFactKey key, T expected)
        {
            Assert.IsTrue(worldState.TryGetValue(key, out T value), "Missing fact: " + key);
            Assert.AreEqual(expected, value);
        }

        private sealed class TestAiAgent : IAiAgent
        {
            public TestAiAgent(int id, IAiWorldState worldState)
            {
                Id = id;
                WorldState = worldState;
            }

            public int Id { get; }
            public IAiWorldState WorldState { get; }
        }
    }
}

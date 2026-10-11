using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestReactionLifecycleProtectionTests
    {
        [TestCase(PressureBand.Stable, false)]
        [TestCase(PressureBand.Pressed, false)]
        [TestCase(PressureBand.Cracked, false)]
        [TestCase(PressureBand.Critical, false)]
        [TestCase(PressureBand.Broken, true)]
        public void OrdinaryHit_ReactsOnlyWhileAliveAndBroken(PressureBand band, bool expected)
        {
            var hit = new HitResolveResult(new CombatEntityId(1), new CombatEntityId(2),
                301, 1, 1, new CombatFrame(10), HitResolveKind.Damage, 17, 1, FixVector3.Zero);
            var state = CharacterTestCharacterCombatStateComponent.CreateDefault()
                .WithScatterPressure(100, 100, band, 0, -1);
            var reaction = CharacterTestHitReactionResolver.Resolve(hit, default, state);
            Assert.AreEqual(expected, reaction.ShouldReact);
            Assert.IsFalse(CharacterTestHitReactionResolver.Resolve(hit, default, state.WithHealth(100, 0)).ShouldReact);
        }

        [TestCase(CharacterActionOrchestrationReason.BehaviorRecipeTimelineJump)]
        [TestCase(CharacterActionOrchestrationReason.BehaviorRecipeReturnToPlanner)]
        public void DeathAction_SurvivesBehaviorCleanupAndKeepsTerminalPose(CharacterActionOrchestrationReason reason)
        {
            var module = new CharacterTestCharacterRuntimeModule(null, NullRuntimeLogger.Instance, createViews: false);
            try
            {
                var definitions = new CharacterTestCharacterDefinitionRegistry();
                Assert.IsTrue(definitions.TryGet(CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, out var definition));
                var entity = module.World.Spawn(new CharacterTestCharacterSpawnRequest(definition.CharacterId,
                    1, 0, 0, 0, CharacterTestCharacterControlOwner.None), definition);
                module.World.SetCurrentAttribute(entity.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 0);
                module.World.SetGameplayLifecycle(entity, GameplayLifecycleState.Destroyed);
                entity.BeginDeath(10, 2, 1);
                CharacterTestBattleStateSystem.RefreshEntity(module.World, entity, new RuntimeFrame(10), null);
                var death = CharacterReactionContextBuilder.FromDeathNoAlloc(new RuntimeFrame(10),
                    entity.GameplayEntityId, "hp_zero", RuntimeTraceIdentity.None);
                Assert.IsTrue(module.ActionRuntime.TryReact(entity.Id, death.Context, allowFallback: false));
                var deathId = entity.Action.ActiveActionId;
                Assert.IsTrue(module.ActionRuntime.Registry.TryGetAction(deathId, out var action));
                Assert.IsTrue(action.DurationFrames.HasValue);
                var hit = CharacterReactionContextBuilder.FromHitSourceNoAlloc(new CharacterReactionHitSource(
                    new RuntimeFrame(11), entity.GameplayEntityId, currentScatterPressureBand: PressureBand.Broken,
                    currentScatterPressure: 100, maxScatterPressure: 100, impactForce: 34));
                Assert.IsFalse(module.ActionRuntime.TryReact(entity.Id, hit.Context));
                Assert.IsFalse(module.ActionRuntime.ReplaceForOrchestration(entity.Id,
                    CharacterTestCharacterActionConfigSource.LightAttackActionId, reason, new RuntimeFrame(11)));
                for (int frame = 11; frame < 10 + action.DurationFrames.Value + 10; frame++)
                {
                    Assert.IsFalse(module.ActionRuntime.TerminateForOrchestration(entity.Id, new RuntimeFrame(frame), reason));
                    module.Tick(new RuntimeTickContext(frame, 1d / 60d, frame / 60d, RuntimeTickStage.Simulation));
                    Assert.AreEqual(deathId, entity.Action.ActiveActionId, "Death identity changed at frame " + frame);
                    Assert.IsTrue(entity.Animation.ActionActive, "Death playback released at frame " + frame);
                }
                Assert.AreEqual(CharacterTestCharacterLifeState.Dead, entity.CombatState.LifeState);
                Assert.IsTrue(CharacterTestCharacterActionSystem.IsDeathTerminalActionHold(entity));
            }
            finally { module.Dispose(); }
        }
    }
}

using System.Collections.Generic;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayAbilityRuntimeAdapterTests
    {
        private const int AttrHp = 1;
        private const int AttrAttack = 2;
        private const int AttrDefense = 3;
        private const int AbilityStrike = 300001;
        private const int AbilityOther = 300002;

        [Test]
        public void GameplayAbilityRegistry_RegisterAndQuery_ReturnsStableIds()
        {
            var registry = new GameplayAbilityRegistry();
            IAbility later = new TestAbility(AbilityOther);
            IAbility earlier = new TestAbility(AbilityStrike);

            Assert.IsTrue(registry.TryRegister(later, out string laterFailure), laterFailure);
            Assert.IsTrue(registry.TryRegister(earlier, out string earlierFailure), earlierFailure);

            Assert.IsTrue(registry.TryGetAbility(AbilityStrike, out IAbility found));
            Assert.AreSame(earlier, found);

            IReadOnlyList<int> ids = registry.GetAbilityIds();
            Assert.AreEqual(2, ids.Count);
            Assert.AreEqual(AbilityStrike, ids[0]);
            Assert.AreEqual(AbilityOther, ids[1]);
        }

        [Test]
        public void GameplayAbilityRegistry_DuplicateAbilityId_IsRejected()
        {
            var registry = new GameplayAbilityRegistry();
            Assert.IsTrue(registry.TryRegister(new TestAbility(AbilityStrike), out string firstFailure), firstFailure);

            bool registered = registry.TryRegister(new TestAbility(AbilityStrike), out string duplicateFailure);

            Assert.IsFalse(registered);
            Assert.AreEqual(GameplayAbilityRegistry.DuplicateAbilityIdFailureReason, duplicateFailure);
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void GameplayAbilityRuntimeService_Cast_SuccessWrapsAbilityCastResult()
        {
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity enemy = CreateEntity(2, 2, 600, 80, 10);
            GameplayAbilityRuntimeService service = CreateService(new IRuntimeEntity[] { player, enemy }, CreateStrikeAbility());

            GameplayAbilityRuntimeResult result = service.Cast(new GameplayAbilityCastRequest(
                player.EntityId,
                AbilityStrike,
                traceId: "trace-success"));

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.CastResult.Success);
            Assert.AreEqual(GameplayAbilityRuntimeFailureCode.None, result.FailureCode);
            Assert.IsNull(result.FailureReason);
            Assert.AreEqual("trace-success", result.TraceId);
            Assert.AreEqual(1, result.CastResult.Targets.Count);
            Assert.AreEqual(enemy.EntityId, result.CastResult.Targets[0].EntityId);
            Assert.AreEqual(enemy.EntityId, result.TargetEntityIds[0]);
        }

        [Test]
        public void GameplayAbilityRuntimeService_Cast_AbilityFailurePreservesCastResultReason()
        {
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            GameplayAbilityRuntimeService service = CreateService(new IRuntimeEntity[] { player }, CreateStrikeAbility());

            GameplayAbilityRuntimeResult result = service.Cast(new GameplayAbilityCastRequest(player.EntityId, AbilityStrike));

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.CastResult.Success);
            Assert.AreEqual(GameplayAbilityRuntimeFailureCode.AbilityCastFailed, result.FailureCode);
            Assert.AreEqual("NoValidTargets", result.FailureReason);
            Assert.AreEqual("NoValidTargets", result.CastResult.FailureReason);
            Assert.AreEqual(0, result.TargetEntityIds.Count);
        }

        [Test]
        public void GameplayAbilityRuntimeService_Cast_MissingCasterReturnsStructuredFailure()
        {
            RuntimeEntity enemy = CreateEntity(2, 2, 600, 80, 10);
            GameplayAbilityRuntimeService service = CreateService(new IRuntimeEntity[] { enemy }, CreateStrikeAbility());

            GameplayAbilityRuntimeResult result = service.Cast(new GameplayAbilityCastRequest(99, AbilityStrike));

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.CastResult.Success);
            Assert.AreEqual(GameplayAbilityRuntimeFailureCode.MissingCaster, result.FailureCode);
            Assert.AreEqual(GameplayAbilityRuntimeService.MissingCasterFailureReason, result.FailureReason);
            Assert.AreEqual(GameplayAbilityRuntimeService.MissingCasterFailureReason, result.CastResult.FailureReason);
        }

        [Test]
        public void GameplayAbilityRuntimeService_Cast_MissingAbilityReturnsStructuredFailure()
        {
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            var registry = new GameplayAbilityRegistry();
            var service = new GameplayAbilityRuntimeService(new IRuntimeEntity[] { player }, registry);

            GameplayAbilityRuntimeResult result = service.Cast(new GameplayAbilityCastRequest(player.EntityId, AbilityStrike));

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.CastResult.Success);
            Assert.AreEqual(GameplayAbilityRuntimeFailureCode.MissingAbility, result.FailureCode);
            Assert.AreEqual(GameplayAbilityRuntimeService.MissingAbilityFailureReason, result.FailureReason);
            Assert.AreEqual(GameplayAbilityRuntimeService.MissingAbilityFailureReason, result.CastResult.FailureReason);
        }

        [Test]
        public void GameplayAbilityRuntimeService_Cast_ExplicitCandidateIdsLimitCandidates()
        {
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity firstEnemy = CreateEntity(2, 2, 600, 80, 10);
            RuntimeEntity secondEnemy = CreateEntity(3, 2, 600, 80, 10);
            GameplayAbilityRuntimeService service = CreateService(
                new IRuntimeEntity[] { player, firstEnemy, secondEnemy },
                CreateStrikeAbility());

            GameplayAbilityRuntimeResult result = service.Cast(new GameplayAbilityCastRequest(
                player.EntityId,
                AbilityStrike,
                new[] { secondEnemy.EntityId }));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.CandidateEntityIds.Count);
            Assert.AreEqual(secondEnemy.EntityId, result.CandidateEntityIds[0]);
            Assert.AreEqual(secondEnemy.EntityId, result.TargetEntityIds[0]);
            Assert.AreEqual(600, firstEnemy.Store.GetAttribute(AttrHp));
            Assert.AreEqual(490, secondEnemy.Store.GetAttribute(AttrHp));
        }

        [Test]
        public void GameplayAbilityRuntimeService_Cast_EmptyCandidatesReturnsStructuredFailure()
        {
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            int abilityEventCount = 0;
            player.AbilityEvents.Subscribe(_ => abilityEventCount++);
            GameplayAbilityRuntimeService service = CreateService(new IRuntimeEntity[] { player }, CreateStrikeAbility());

            GameplayAbilityRuntimeResult result = service.Cast(new GameplayAbilityCastRequest(
                player.EntityId,
                AbilityStrike,
                new[] { 99 }));

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.CastResult.Success);
            Assert.AreEqual(GameplayAbilityRuntimeFailureCode.EmptyCandidates, result.FailureCode);
            Assert.AreEqual(GameplayAbilityRuntimeService.EmptyCandidatesFailureReason, result.FailureReason);
            Assert.AreEqual(0, result.CandidateEntityIds.Count);
            Assert.AreEqual(0, abilityEventCount);
        }

        private static RuntimeEntity CreateEntity(int id, int team, int hp, int attack, int defense)
        {
            var entity = new RuntimeEntity(id, team, AttrHp);
            entity.Store.RegisterAttribute(AttrHp, hp);
            entity.Store.RegisterAttribute(AttrAttack, attack);
            entity.Store.RegisterAttribute(AttrDefense, defense);
            return entity;
        }

        private static IAbility CreateStrikeAbility()
        {
            return new SimpleAbility(
                AbilityStrike,
                new SingleEnemyTargetSelector(),
                new IAbilityEffect[]
                {
                    new DamageEffect(AttrAttack, AttrDefense, AttrHp)
                });
        }

        private static GameplayAbilityRuntimeService CreateService(IReadOnlyList<IRuntimeEntity> entities, IAbility ability)
        {
            var registry = new GameplayAbilityRegistry();
            Assert.IsTrue(registry.TryRegister(ability, out string failureReason), failureReason);
            return new GameplayAbilityRuntimeService(entities, registry);
        }

        private sealed class TestAbility : IAbility
        {
            public TestAbility(int abilityId)
            {
                AbilityId = abilityId;
            }

            public int AbilityId { get; }

            public AbilityCastResult Cast(AbilityContext context)
            {
                return AbilityCastResult.Ok(context.Candidates);
            }
        }
    }
}

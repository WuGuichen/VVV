using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Buffs;
using MxFramework.Gameplay;
using MxFramework.Modifiers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public class GameplayDiagnosticsHashTests
    {
        private const int AttrHp = 1;
        private const int AttrAttack = 2;
        private const int AttrDefense = 3;
        private const int BuffBurning = 100001;
        private const int ModifierRage = 200001;

        [Test]
        public void GameplayHashContributor_StableEntityAndAttributeOrdering()
        {
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity enemy = CreateEntity(2, 2, 600, 80, 10);

            long firstHash = ComputeHash(
                new IRuntimeEntity[] { player, enemy },
                new[] { AttrDefense, AttrHp, AttrAttack });
            long secondHash = ComputeHash(
                new IRuntimeEntity[] { enemy, player },
                new[] { AttrAttack, AttrHp, AttrDefense });

            Assert.AreEqual(firstHash, secondHash);
        }

        [Test]
        public void GameplayHashContributor_AttributeChangeChangesHash()
        {
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity enemy = CreateEntity(2, 2, 600, 80, 10);
            var entities = new IRuntimeEntity[] { player, enemy };
            var attributes = new[] { AttrHp, AttrAttack, AttrDefense };

            long before = ComputeHash(entities, attributes);
            enemy.Store.SetAttribute(AttrHp, 550, "test.damage");
            long after = ComputeHash(entities, attributes);

            Assert.AreNotEqual(before, after);
        }

        [Test]
        public void GameplayHashContributor_WorldOverloadIncludesWorldTick()
        {
            var world = new GameplayWorld();
            world.Register(CreateEntity(1, 1, 1000, 120, 20));
            var attributes = new[] { AttrHp, AttrAttack, AttrDefense };

            long before = ComputeHash(world, attributes);
            world.Tick(0d);
            long after = ComputeHash(world, attributes);

            Assert.AreNotEqual(before, after);
        }

        [Test]
        public void GameplayHashContributor_BuffAndModifierStateChangesHash()
        {
            RuntimeEntity entity = CreateEntity(1, 1, 1000, 120, 20);
            var entities = new IRuntimeEntity[] { entity };
            var attributes = new[] { AttrHp, AttrAttack, AttrDefense };

            long baseline = ComputeHash(entities, attributes);
            entity.Buffs.AddBuff(new TestBuff(BuffBurning, duration: 5f, maxLayers: 3), entity);
            long withBuff = ComputeHash(entities, attributes);
            entity.Modifiers.AddModifier(new ModifierBase(ModifierRage, paramIndex: 7));
            long withModifier = ComputeHash(entities, attributes);

            Assert.AreNotEqual(baseline, withBuff);
            Assert.AreNotEqual(withBuff, withModifier);
        }

        [Test]
        public void GameplayHashContributor_ContributorIdIsStable()
        {
            var contributor = new GameplayHashContributor(
                new IRuntimeEntity[0],
                new int[0]);

            Assert.AreEqual(GameplayHashContributor.StableContributorId, contributor.ContributorId);
            Assert.AreEqual("mxframework.gameplay.world", contributor.ContributorId);
        }

        [Test]
        public void GameplayWorldDiagnostics_BuildsSnapshotAndSummaryFromExistingBuilder()
        {
            RuntimeEntity alive = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity defeated = CreateEntity(2, 2, 0, 80, 10);
            alive.Buffs.AddBuff(new TestBuff(BuffBurning, duration: 5f, maxLayers: 3), alive);
            defeated.Modifiers.AddModifier(new ModifierBase(ModifierRage, paramIndex: 7));

            var diagnostics = new GameplayWorldDiagnostics();
            GameplayDiagnosticSnapshot snapshot = diagnostics.BuildSnapshot(
                "hash-diagnostics",
                "none",
                new[] { defeated, alive },
                new[] { AttrHp, AttrAttack },
                AbilityCastResult.Fail("NotCast"),
                Array.Empty<AbilityEvent>(),
                Array.Empty<MxFramework.Attributes.AttributeChangedEvent>());
            GameplayWorldDiagnosticsSummary summary = diagnostics.BuildSummary(snapshot);

            Assert.AreEqual(2, snapshot.Entities.Count);
            Assert.AreEqual("hash-diagnostics", summary.SourceName);
            Assert.AreEqual(2, summary.EntityCount);
            Assert.AreEqual(1, summary.AliveEntityCount);
            Assert.AreEqual(4, summary.AttributeCount);
            Assert.AreEqual(1, summary.BuffCount);
            Assert.AreEqual(1, summary.ModifierCount);
        }

        [Test]
        public void GameplayAsmdef_KeepsNoEngineReferencesAndReferencesRuntime()
        {
            string asmdef = File.ReadAllText("Assets/Scripts/MxFramework/Gameplay/MxFramework.Gameplay.asmdef");

            StringAssert.Contains("\"MxFramework.Runtime\"", asmdef);
            StringAssert.Contains("\"noEngineReferences\": true", asmdef);
        }

        private static long ComputeHash(
            IReadOnlyList<IRuntimeEntity> entities,
            IReadOnlyList<int> attributeIds)
        {
            var contributor = new GameplayHashContributor(entities, attributeIds);
            return RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { contributor });
        }

        private static long ComputeHash(
            GameplayWorld world,
            IReadOnlyList<int> attributeIds)
        {
            var contributor = new GameplayHashContributor(world, attributeIds);
            return RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { contributor });
        }

        private static RuntimeEntity CreateEntity(int id, int team, int hp, int attack, int defense)
        {
            var entity = new RuntimeEntity(id, team, AttrHp);
            entity.Store.RegisterAttribute(AttrHp, hp);
            entity.Store.RegisterAttribute(AttrAttack, attack);
            entity.Store.RegisterAttribute(AttrDefense, defense);
            return entity;
        }

        private sealed class TestBuff : BuffBase
        {
            public TestBuff(int id, float duration, int maxLayers)
                : base(id, duration, maxLayers)
            {
            }
        }
    }
}

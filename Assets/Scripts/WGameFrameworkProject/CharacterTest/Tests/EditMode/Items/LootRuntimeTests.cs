using MxFramework.Items;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Items
{
    public sealed class LootRuntimeTests
    {
        [Test]
        public void LootResolver_SameSeedAndContextProducesSameSortedResult()
        {
            ItemDefinitionRegistry definitions = CreateDefinitions();
            LootTableDefinition table = new LootTableDefinition(
                new LootTableId(1),
                "loot.test.sorted",
                new[]
                {
                    new LootGroupDefinition(
                        "ore",
                        1,
                        1,
                        LootContext.BasisPointsScale,
                        0,
                        new[] { new LootEntryDefinition(new ItemDefinitionId(1002), 1, 2, 2) }),
                    new LootGroupDefinition(
                        "herb",
                        1,
                        1,
                        LootContext.BasisPointsScale,
                        0,
                        new[] { new LootEntryDefinition(new ItemDefinitionId(1001), 1, 3, 3) })
                });

            var resolver = new LootResolver(definitions);
            LootContext context = new LootContext("source.test", 3, new ItemTagSet(new[] { 42 }));
            LootResolveResult left = resolver.Resolve(table, context, new DeterministicRandom(1234u));
            LootResolveResult right = resolver.Resolve(table, context, new DeterministicRandom(1234u));

            Assert.IsTrue(left.Success, Describe(left));
            Assert.IsTrue(right.Success, Describe(right));
            Assert.AreEqual(2, left.Items.Length);
            Assert.AreEqual(left.Items.Length, right.Items.Length);
            for (int i = 0; i < left.Items.Length; i++)
                Assert.AreEqual(left.Items[i], right.Items[i]);
            Assert.AreEqual(1001, left.Items[0].ItemDefinitionId.Value);
            Assert.AreEqual(3, left.Items[0].Count);
            Assert.AreEqual(1002, left.Items[1].ItemDefinitionId.Value);
            Assert.AreEqual(2, left.Items[1].Count);
        }

        [Test]
        public void LootResolver_GroupActivationEmptyWeightRollCountAndQuantityMultiplier()
        {
            ItemDefinitionRegistry definitions = CreateDefinitions();
            var resolver = new LootResolver(definitions);

            LootTableDefinition inactive = new LootTableDefinition(
                new LootTableId(2),
                "loot.test.inactive",
                new[]
                {
                    new LootGroupDefinition(
                        "inactive",
                        1,
                        1,
                        0,
                        0,
                        new[] { new LootEntryDefinition(new ItemDefinitionId(1001), 1, 1, 1) })
                });
            LootResolveResult inactiveResult = resolver.Resolve(
                inactive,
                new LootContext("source.test", 1, new ItemTagSet(null)),
                new DeterministicRandom(1u));
            Assert.IsTrue(inactiveResult.Success, Describe(inactiveResult));
            Assert.AreEqual(0, inactiveResult.Items.Length);

            LootTableDefinition emptyOnly = new LootTableDefinition(
                new LootTableId(3),
                "loot.test.empty",
                new[]
                {
                    new LootGroupDefinition(
                        "empty",
                        2,
                        2,
                        LootContext.BasisPointsScale,
                        10,
                        new[] { new LootEntryDefinition(new ItemDefinitionId(1001), 0, 1, 1) })
                });
            LootResolveResult emptyResult = resolver.Resolve(
                emptyOnly,
                new LootContext("source.test", 1, new ItemTagSet(null)),
                new DeterministicRandom(2u));
            Assert.IsTrue(emptyResult.Success, Describe(emptyResult));
            Assert.AreEqual(0, emptyResult.Items.Length);

            LootTableDefinition twoRolls = new LootTableDefinition(
                new LootTableId(4),
                "loot.test.two_rolls",
                new[]
                {
                    new LootGroupDefinition(
                        "guaranteed",
                        2,
                        2,
                        LootContext.BasisPointsScale,
                        0,
                        new[] { new LootEntryDefinition(new ItemDefinitionId(1001), 1, 2, 2) })
                });
            LootResolveResult twoRollResult = resolver.Resolve(
                twoRolls,
                new LootContext(
                    "source.test",
                    1,
                    new ItemTagSet(null),
                    quantityMultiplierBasisPoints: 15000),
                new DeterministicRandom(3u));
            Assert.IsTrue(twoRollResult.Success, Describe(twoRollResult));
            Assert.AreEqual(1, twoRollResult.Items.Length);
            Assert.AreEqual(6, twoRollResult.Items[0].Count);
        }

        [Test]
        public void LootResolver_LevelAndTagConditionsFilterEntries()
        {
            ItemDefinitionRegistry definitions = CreateDefinitions();
            var resolver = new LootResolver(definitions);
            LootTableDefinition table = new LootTableDefinition(
                new LootTableId(5),
                "loot.test.conditions",
                new[]
                {
                    new LootGroupDefinition(
                        "conditional",
                        1,
                        1,
                        LootContext.BasisPointsScale,
                        0,
                        new[]
                        {
                            new LootEntryDefinition(
                                new ItemDefinitionId(1001),
                                1,
                                1,
                                1,
                                minSourceLevel: 5,
                                maxSourceLevel: 10,
                                requiredContextTags: new ItemTagSet(new[] { 42 })),
                            new LootEntryDefinition(
                                new ItemDefinitionId(1002),
                                1,
                                1,
                                1,
                                blockedContextTags: new ItemTagSet(new[] { 77 }))
                        })
                });

            LootResolveResult matching = resolver.Resolve(
                table,
                new LootContext("source.test", 7, new ItemTagSet(new[] { 42 })),
                new DeterministicRandom(4u));
            Assert.IsTrue(matching.Success, Describe(matching));
            Assert.AreEqual(1, matching.Items.Length);

            LootResolveResult blocked = resolver.Resolve(
                table,
                new LootContext("source.test", 7, new ItemTagSet(new[] { 42, 77 })),
                new DeterministicRandom(4u));
            Assert.IsTrue(blocked.Success, Describe(blocked));
            Assert.AreEqual(1, blocked.Items.Length);
            Assert.AreEqual(1001, blocked.Items[0].ItemDefinitionId.Value);

            LootResolveResult lowLevel = resolver.Resolve(
                table,
                new LootContext("source.test", 2, new ItemTagSet(new[] { 42, 77 })),
                new DeterministicRandom(4u));
            Assert.IsTrue(lowLevel.Success, Describe(lowLevel));
            Assert.AreEqual(0, lowLevel.Items.Length);
        }

        [Test]
        public void LootResolver_InvalidConfigReturnsDiagnostics()
        {
            ItemDefinitionRegistry definitions = CreateDefinitions();
            var resolver = new LootResolver(definitions);

            LootResolveResult emptyGroup = resolver.Resolve(
                new LootTableDefinition(
                    new LootTableId(10),
                    "loot.invalid.empty_group",
                    new[] { new LootGroupDefinition("bad", 1, 1, LootContext.BasisPointsScale, 0, null) }),
                new LootContext("source.test", 1, new ItemTagSet(null)),
                new DeterministicRandom(1u));
            Assert.IsFalse(emptyGroup.Success);
            Assert.AreEqual(LootResolveStatus.InvalidGroup, emptyGroup.Status);

            LootResolveResult invalidEntry = resolver.Resolve(
                new LootTableDefinition(
                    new LootTableId(11),
                    "loot.invalid.entry",
                    new[]
                    {
                        new LootGroupDefinition(
                            "bad",
                            1,
                            1,
                            LootContext.BasisPointsScale,
                            0,
                            new[] { new LootEntryDefinition(new ItemDefinitionId(0), -1, 0, -1) })
                    }),
                new LootContext("source.test", 1, new ItemTagSet(null)),
                new DeterministicRandom(1u));
            Assert.IsFalse(invalidEntry.Success);
            Assert.AreEqual(LootResolveStatus.InvalidEntry, invalidEntry.Status);

            LootResolveResult missingDefinition = resolver.Resolve(
                new LootTableDefinition(
                    new LootTableId(12),
                    "loot.invalid.missing",
                    new[]
                    {
                        new LootGroupDefinition(
                            "missing",
                            1,
                            1,
                            LootContext.BasisPointsScale,
                            0,
                            new[] { new LootEntryDefinition(new ItemDefinitionId(9999), 1, 1, 1) })
                    }),
                new LootContext("source.test", 1, new ItemTagSet(null)),
                new DeterministicRandom(1u));
            Assert.IsFalse(missingDefinition.Success);
            Assert.AreEqual(LootResolveStatus.DefinitionMissing, missingDefinition.Status);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void LootResolver_OverflowingRangesFailBeforeAdvancingRandom(int invalidKind)
        {
            var random = new DeterministicRandom(123u);
            var control = new DeterministicRandom(123u);
            var entry = new LootEntryDefinition(new ItemDefinitionId(1001),
                invalidKind == 0 ? int.MaxValue : 1, 1, invalidKind == 2 ? int.MaxValue : 1);
            var table = new LootTableDefinition(new LootTableId(1), "loot.extreme", new[]
            {
                new LootGroupDefinition("group.extreme", 1, invalidKind == 1 ? int.MaxValue : 1,
                    LootContext.BasisPointsScale, invalidKind == 0 ? 1 : 0, new[] { entry })
            });
            var result = new LootResolver(CreateDefinitions()).Resolve(table,
                new LootContext("source.test", 1, new ItemTagSet(null)), random);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(invalidKind == 2 ? LootResolveStatus.InvalidEntry : LootResolveStatus.InvalidGroup, result.Status);
            Assert.AreEqual(control.NextInt(0, 100000), random.NextInt(0, 100000));
        }

        private static ItemDefinitionRegistry CreateDefinitions()
        {
            var definitions = new ItemDefinitionRegistry();
            Assert.IsTrue(definitions.Register(ItemDefinitionTests.CreateDefinition(1001, "item.herb", ItemKind.Material, 20)).Success);
            Assert.IsTrue(definitions.Register(ItemDefinitionTests.CreateDefinition(1002, "item.ore", ItemKind.Material, 10)).Success);
            return definitions;
        }

        private static string Describe(LootResolveResult result)
        {
            if (result == null || result.Diagnostics.Length == 0)
                return string.Empty;
            return result.Diagnostics[0].Status + ":" + result.Diagnostics[0].Message;
        }
    }
}

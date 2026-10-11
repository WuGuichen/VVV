using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionAuthoringBindingTableTests
    {
        [Test]
        public void CommandProjection_FilterAndSort_DoNotMutateDtoOrder()
        {
            var first = new CharacterActionBindingJsonDto
            {
                IntentId = CharacterTestBulkIds38.Id790003,
                ActionId = 102,
                Priority = 5,
            };
            var second = new CharacterActionBindingJsonDto
            {
                IntentId = CharacterTestBulkIds38.Id790004,
                ActionId = 101,
                Priority = 10,
            };
            CharacterActionBindingJsonDto[] bindings = { first, second };

            List<CharacterActionBindingTableRow> rows =
                CharacterActionBindingTableProjection.BuildCommandRows(
                    bindings,
                    "set.main",
                    new HashSet<int> { 101, 102 },
                    "#",
                    CharacterActionBindingTableSort.TargetAction);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(1, rows[0].OriginalIndex);
            Assert.AreEqual(0, rows[1].OriginalIndex);
            Assert.AreSame(first, bindings[0]);
            Assert.AreSame(second, bindings[1]);
        }

        [Test]
        public void AbilityProjection_PreservesUnknownTargetAndMarksProblem()
        {
            var binding = new CharacterAbilityActionBindingJsonDto
            {
                AbilityId = 900001,
                ActionId = 999,
                RequiredTags = new[] { "weapon.light" },
            };

            List<CharacterActionBindingTableRow> rows =
                CharacterActionBindingTableProjection.BuildAbilityRows(
                    new[] { binding },
                    "set.main",
                    new HashSet<int> { 101 },
                    new HashSet<int> { 900001 },
                    string.Empty,
                    CharacterActionBindingTableSort.SourceOrder);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("#999", rows[0].TargetAction);
            Assert.AreEqual("未知动作引用", rows[0].Status);
            Assert.GreaterOrEqual(rows[0].ProblemCount, 1);
            Assert.AreEqual(999, binding.ActionId);
        }

        [TestCase("filter", CharacterActionBindingTableSort.SourceOrder, false)]
        [TestCase("", CharacterActionBindingTableSort.TargetAction, false)]
        [TestCase("", CharacterActionBindingTableSort.SourceOrder, true)]
        public void ReorderGuard_RequiresUnfilteredSourceOrder(
            string query,
            object sort,
            bool expected)
        {
            Assert.AreEqual(
                expected,
                CharacterActionBindingTableProjection.CanReorder(
                    query,
                    (CharacterActionBindingTableSort)sort));
        }

        [Test]
        public void TryMove_CreatesReorderedCopyAndLeavesInputUnchanged()
        {
            string[] source = { "first", "second", "third" };

            bool moved = CharacterActionBindingTableProjection.TryMove(
                source,
                1,
                -1,
                out string[] result);

            Assert.IsTrue(moved);
            CollectionAssert.AreEqual(new[] { "second", "first", "third" }, result);
            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, source);
        }
    }
}

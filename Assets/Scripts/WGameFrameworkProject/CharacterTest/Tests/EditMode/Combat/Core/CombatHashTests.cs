using MxFramework.Combat.Core;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Core
{
    public class CombatHashTests
    {
        [Test]
        public void SameInput_ProducesSameHash()
        {
            CombatHash first = BuildHash(12, 3);
            CombatHash second = BuildHash(12, 3);

            Assert.AreEqual(first, second);
            Assert.AreEqual(first.ToString(), second.ToString());
        }

        [Test]
        public void DifferentFrame_ChangesHash()
        {
            CombatHash first = BuildHash(12, 3);
            CombatHash second = BuildHash(13, 3);

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void DifferentEntity_ChangesHash()
        {
            CombatHash first = BuildHash(12, 3);
            CombatHash second = BuildHash(12, 4);

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void SortKey_IsIncludedInHash()
        {
            var firstKey = new CombatSortKey(
                1,
                new CombatEntityId(2),
                new CombatBodyId(3),
                new CombatColliderId(4),
                5,
                6,
                7);
            var secondKey = new CombatSortKey(
                1,
                new CombatEntityId(2),
                new CombatBodyId(3),
                new CombatColliderId(4),
                5,
                6,
                8);

            CombatHash first = CombatHash.Empty.Add(firstKey);
            CombatHash second = CombatHash.Empty.Add(secondKey);

            Assert.AreNotEqual(first, second);
        }

        private static CombatHash BuildHash(int frame, int entityId)
        {
            return CombatHash.Empty
                .Add(new CombatFrame(frame))
                .Add(new CombatEntityId(entityId))
                .Add(new CombatBodyId(1))
                .Add(new CombatColliderId(2));
        }
    }
}

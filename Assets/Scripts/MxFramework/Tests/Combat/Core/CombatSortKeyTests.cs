using System.Collections.Generic;
using MxFramework.Combat.Core;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Core
{
    public class CombatSortKeyTests
    {
        [Test]
        public void Sort_UsesDeterministicTieBreakers()
        {
            var keys = new List<CombatSortKey>
            {
                Key(primary: 10, entity: 2, body: 1, collider: 1, trace: 1, action: 1, order: 1),
                Key(primary: 5, entity: 3, body: 1, collider: 1, trace: 1, action: 1, order: 1),
                Key(primary: 5, entity: 1, body: 5, collider: 1, trace: 1, action: 1, order: 1),
                Key(primary: 5, entity: 1, body: 2, collider: 2, trace: 1, action: 1, order: 1),
                Key(primary: 5, entity: 1, body: 2, collider: 1, trace: 2, action: 1, order: 1),
                Key(primary: 5, entity: 1, body: 2, collider: 1, trace: 1, action: 2, order: 1),
                Key(primary: 5, entity: 1, body: 2, collider: 1, trace: 1, action: 1, order: 2),
                Key(primary: 5, entity: 1, body: 2, collider: 1, trace: 1, action: 1, order: 1),
            };

            keys.Sort();

            Assert.AreEqual("5:1:2:1:1:1:1", keys[0].ToString());
            Assert.AreEqual("5:1:2:1:1:1:2", keys[1].ToString());
            Assert.AreEqual("5:1:2:1:1:2:1", keys[2].ToString());
            Assert.AreEqual("5:1:2:1:2:1:1", keys[3].ToString());
            Assert.AreEqual("5:1:2:2:1:1:1", keys[4].ToString());
            Assert.AreEqual("5:1:5:1:1:1:1", keys[5].ToString());
            Assert.AreEqual("5:3:1:1:1:1:1", keys[6].ToString());
            Assert.AreEqual("10:2:1:1:1:1:1", keys[7].ToString());
        }

        [Test]
        public void NegativeIds_AreRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CombatEntityId(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CombatBodyId(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CombatColliderId(-1));
        }

        private static CombatSortKey Key(
            int primary,
            int entity,
            int body,
            int collider,
            int trace,
            int action,
            int order)
        {
            return new CombatSortKey(
                primary,
                new CombatEntityId(entity),
                new CombatBodyId(body),
                new CombatColliderId(collider),
                trace,
                action,
                order);
        }
    }
}

using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public class GameplayTeamTagStatusTests
    {
        [Test]
        public void GameplayTeamRelations_Resolve_ReturnsSameEnemyAndNeutral()
        {
            Assert.AreEqual(GameplayTeamRelation.SameTeam, GameplayTeamRelations.Resolve(1, 1));
            Assert.AreEqual(GameplayTeamRelation.Enemy, GameplayTeamRelations.Resolve(1, 2));
            Assert.AreEqual(GameplayTeamRelation.Neutral, GameplayTeamRelations.Resolve(0, 1));
            Assert.AreEqual(GameplayTeamRelation.Neutral, GameplayTeamRelations.Resolve(1, 0));
            Assert.AreEqual(GameplayTeamRelation.Neutral, GameplayTeamRelations.Resolve(-1, 1));
        }

        [Test]
        public void GameplayTeamRelations_UsesStableRelationValues()
        {
            Assert.AreEqual(1, (int)GameplayTeamRelation.SameTeam);
            Assert.AreEqual(2, (int)GameplayTeamRelation.Enemy);
            Assert.AreEqual(3, (int)GameplayTeamRelation.Neutral);
        }

        [Test]
        public void GameplayTagSet_AddRemoveContains_AreIdempotent()
        {
            var set = new GameplayTagSet();
            var tag = new GameplayTagId(100);

            Assert.IsTrue(set.Add(tag));
            Assert.IsFalse(set.Add(tag));
            Assert.IsTrue(set.Contains(tag));
            Assert.AreEqual(1, set.Count);

            Assert.IsTrue(set.Remove(tag));
            Assert.IsFalse(set.Remove(tag));
            Assert.IsFalse(set.Contains(tag));
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void GameplayTagSet_EnumeratesInStableIdOrder()
        {
            var set = new GameplayTagSet();
            set.Add(new GameplayTagId(30));
            set.Add(new GameplayTagId(10));
            set.Add(new GameplayTagId(20));

            GameplayTagId[] ids = set.ToArray();

            Assert.AreEqual(3, ids.Length);
            Assert.AreEqual(10, ids[0].Value);
            Assert.AreEqual(20, ids[1].Value);
            Assert.AreEqual(30, ids[2].Value);
        }

        [Test]
        public void GameplayTagSet_IgnoresNoneAndRejectsNegativeIds()
        {
            var set = new GameplayTagSet();

            Assert.IsFalse(set.Add(default));
            Assert.IsFalse(set.Add(GameplayTagId.None));
            Assert.IsFalse(set.Contains(default));
            Assert.IsFalse(set.Remove(GameplayTagId.None));
            Assert.AreEqual(0, set.Count);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new GameplayTagId(-1));
        }

        [Test]
        public void GameplayStatusSet_AddRemoveContains_AreIdempotent()
        {
            var set = new GameplayStatusSet();
            var status = new GameplayStatusId(200);

            Assert.IsTrue(set.Add(status));
            Assert.IsFalse(set.Add(status));
            Assert.IsTrue(set.Contains(status));
            Assert.AreEqual(1, set.Count);

            Assert.IsTrue(set.Remove(status));
            Assert.IsFalse(set.Remove(status));
            Assert.IsFalse(set.Contains(status));
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void GameplayStatusSet_EnumeratesInStableIdOrder()
        {
            var set = new GameplayStatusSet();
            set.Add(new GameplayStatusId(300));
            set.Add(new GameplayStatusId(100));
            set.Add(new GameplayStatusId(200));

            GameplayStatusId[] ids = set.ToArray();

            Assert.AreEqual(3, ids.Length);
            Assert.AreEqual(100, ids[0].Value);
            Assert.AreEqual(200, ids[1].Value);
            Assert.AreEqual(300, ids[2].Value);
        }

        [Test]
        public void GameplayStatusSet_IgnoresNoneAndRejectsNegativeIds()
        {
            var set = new GameplayStatusSet();

            Assert.IsFalse(set.Add(default));
            Assert.IsFalse(set.Add(GameplayStatusId.None));
            Assert.IsFalse(set.Contains(default));
            Assert.IsFalse(set.Remove(GameplayStatusId.None));
            Assert.AreEqual(0, set.Count);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new GameplayStatusId(-1));
        }
    }
}

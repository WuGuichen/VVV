using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.GameplayBridge
{
    public class CombatEntityGameplayMapTests
    {
        [Test]
        public void Register_AddsBidirectionalMapping()
        {
            var map = new CombatEntityGameplayMap();
            CombatEntityId combatId = Combat(10);
            GameplayEntityId gameplayId = Gameplay(1, 1);

            map.Register(combatId, gameplayId);

            Assert.AreEqual(1, map.Count);
            Assert.IsTrue(map.TryGetGameplayId(combatId, out GameplayEntityId resolvedGameplayId));
            Assert.AreEqual(gameplayId, resolvedGameplayId);
            Assert.IsTrue(map.TryGetCombatId(gameplayId, out CombatEntityId resolvedCombatId));
            Assert.AreEqual(combatId, resolvedCombatId);
        }

        [Test]
        public void Register_WithInvalidIds_ThrowsArgumentException()
        {
            var map = new CombatEntityGameplayMap();

            Assert.Throws<ArgumentException>(() => map.Register(CombatEntityId.None, Gameplay(1, 1)));
            Assert.Throws<ArgumentException>(() => map.Register(Combat(10), default));
        }

        [Test]
        public void Register_SameCombatIdWithNewGameplayId_ClearsStaleGameplayMapping()
        {
            var map = new CombatEntityGameplayMap();
            CombatEntityId combatId = Combat(10);
            GameplayEntityId originalGameplayId = Gameplay(1, 1);
            GameplayEntityId replacementGameplayId = Gameplay(2, 1);

            map.Register(combatId, originalGameplayId);
            map.Register(combatId, replacementGameplayId);

            Assert.AreEqual(1, map.Count);
            Assert.IsFalse(map.TryGetCombatId(originalGameplayId, out CombatEntityId staleCombatId));
            Assert.AreEqual(default(CombatEntityId), staleCombatId);
            Assert.IsTrue(map.TryGetCombatId(replacementGameplayId, out CombatEntityId resolvedCombatId));
            Assert.AreEqual(combatId, resolvedCombatId);
            CollectionAssert.AreEqual(new[] { combatId }, map.CreateCombatIdSnapshot());
            CollectionAssert.AreEqual(new[] { replacementGameplayId }, map.CreateGameplayIdSnapshot());
        }

        [Test]
        public void Register_SameGameplayIdWithNewCombatId_ClearsStaleCombatMapping()
        {
            var map = new CombatEntityGameplayMap();
            CombatEntityId originalCombatId = Combat(10);
            CombatEntityId replacementCombatId = Combat(20);
            GameplayEntityId gameplayId = Gameplay(1, 1);

            map.Register(originalCombatId, gameplayId);
            map.Register(replacementCombatId, gameplayId);

            Assert.AreEqual(1, map.Count);
            Assert.IsFalse(map.TryGetGameplayId(originalCombatId, out GameplayEntityId staleGameplayId));
            Assert.AreEqual(default(GameplayEntityId), staleGameplayId);
            Assert.IsTrue(map.TryGetGameplayId(replacementCombatId, out GameplayEntityId resolvedGameplayId));
            Assert.AreEqual(gameplayId, resolvedGameplayId);
            CollectionAssert.AreEqual(new[] { replacementCombatId }, map.CreateCombatIdSnapshot());
            CollectionAssert.AreEqual(new[] { gameplayId }, map.CreateGameplayIdSnapshot());
        }

        [Test]
        public void RemoveCombat_RemovesBothDirections()
        {
            var map = new CombatEntityGameplayMap();
            CombatEntityId combatId = Combat(10);
            GameplayEntityId gameplayId = Gameplay(1, 1);
            map.Register(combatId, gameplayId);

            Assert.IsTrue(map.RemoveCombat(combatId));

            Assert.AreEqual(0, map.Count);
            Assert.IsFalse(map.TryGetGameplayId(combatId, out GameplayEntityId resolvedGameplayId));
            Assert.AreEqual(default(GameplayEntityId), resolvedGameplayId);
            Assert.IsFalse(map.TryGetCombatId(gameplayId, out CombatEntityId resolvedCombatId));
            Assert.AreEqual(default(CombatEntityId), resolvedCombatId);
            Assert.IsFalse(map.RemoveCombat(combatId));
        }

        [Test]
        public void RemoveGameplay_RemovesBothDirections()
        {
            var map = new CombatEntityGameplayMap();
            CombatEntityId combatId = Combat(10);
            GameplayEntityId gameplayId = Gameplay(1, 1);
            map.Register(combatId, gameplayId);

            Assert.IsTrue(map.RemoveGameplay(gameplayId));

            Assert.AreEqual(0, map.Count);
            Assert.IsFalse(map.TryGetGameplayId(combatId, out GameplayEntityId resolvedGameplayId));
            Assert.AreEqual(default(GameplayEntityId), resolvedGameplayId);
            Assert.IsFalse(map.TryGetCombatId(gameplayId, out CombatEntityId resolvedCombatId));
            Assert.AreEqual(default(CombatEntityId), resolvedCombatId);
            Assert.IsFalse(map.RemoveGameplay(gameplayId));
        }

        [Test]
        public void Clear_RemovesAllMappings()
        {
            var map = new CombatEntityGameplayMap();
            map.Register(Combat(10), Gameplay(1, 1));
            map.Register(Combat(20), Gameplay(2, 1));

            map.Clear();

            Assert.AreEqual(0, map.Count);
            CollectionAssert.IsEmpty(map.CreateCombatIdSnapshot());
            CollectionAssert.IsEmpty(map.CreateGameplayIdSnapshot());
        }

        [Test]
        public void MissingKeys_ReturnFalseAndDefault()
        {
            var map = new CombatEntityGameplayMap();

            Assert.IsFalse(map.TryGetGameplayId(Combat(10), out GameplayEntityId gameplayId));
            Assert.AreEqual(default(GameplayEntityId), gameplayId);
            Assert.IsFalse(map.TryGetCombatId(Gameplay(1, 1), out CombatEntityId combatId));
            Assert.AreEqual(default(CombatEntityId), combatId);
            Assert.IsFalse(map.RemoveCombat(Combat(10)));
            Assert.IsFalse(map.RemoveGameplay(Gameplay(1, 1)));
        }

        [Test]
        public void SnapshotsAndCopyMethods_ReturnRegistrationOrder()
        {
            var map = new CombatEntityGameplayMap();
            CombatEntityId firstCombatId = Combat(10);
            CombatEntityId secondCombatId = Combat(20);
            CombatEntityId thirdCombatId = Combat(30);
            GameplayEntityId firstGameplayId = Gameplay(1, 1);
            GameplayEntityId secondGameplayId = Gameplay(2, 1);
            GameplayEntityId thirdGameplayId = Gameplay(3, 1);

            map.Register(firstCombatId, firstGameplayId);
            map.Register(secondCombatId, secondGameplayId);
            map.Register(thirdCombatId, thirdGameplayId);

            var combatIds = new List<CombatEntityId>();
            var gameplayIds = new List<GameplayEntityId>();
            map.CopyCombatIds(combatIds);
            map.CopyGameplayIds(gameplayIds);

            CollectionAssert.AreEqual(new[] { firstCombatId, secondCombatId, thirdCombatId }, combatIds);
            CollectionAssert.AreEqual(new[] { firstGameplayId, secondGameplayId, thirdGameplayId }, gameplayIds);
            CollectionAssert.AreEqual(combatIds, map.CreateCombatIdSnapshot());
            CollectionAssert.AreEqual(gameplayIds, map.CreateGameplayIdSnapshot());
        }

        [Test]
        public void Overwrite_DoesNotChangeOriginalRegistrationOrder()
        {
            var map = new CombatEntityGameplayMap();
            CombatEntityId firstCombatId = Combat(10);
            CombatEntityId secondCombatId = Combat(20);
            GameplayEntityId replacementGameplayId = Gameplay(3, 1);

            map.Register(firstCombatId, Gameplay(1, 1));
            map.Register(secondCombatId, Gameplay(2, 1));
            map.Register(firstCombatId, replacementGameplayId);

            CollectionAssert.AreEqual(new[] { firstCombatId, secondCombatId }, map.CreateCombatIdSnapshot());
            CollectionAssert.AreEqual(new[] { replacementGameplayId, Gameplay(2, 1) }, map.CreateGameplayIdSnapshot());
        }

        private static CombatEntityId Combat(int value)
        {
            return new CombatEntityId(value);
        }

        private static GameplayEntityId Gameplay(int index, int generation)
        {
            return new GameplayEntityId(index, generation);
        }
    }
}

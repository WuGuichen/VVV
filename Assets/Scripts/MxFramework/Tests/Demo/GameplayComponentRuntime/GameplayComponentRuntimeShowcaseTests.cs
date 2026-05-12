using System.Linq;
using MxFramework.Demo.GameplayComponentRuntime;
using NUnit.Framework;

namespace MxFramework.Tests.Demo.GameplayComponentRuntime
{
    public sealed class GameplayComponentRuntimeShowcaseTests
    {
        [Test]
        public void Showcase_SpawnCastCooldownAndCleanupFlow()
        {
            using (var showcase = new GameplayComponentRuntimeShowcase())
            {
                Assert.IsTrue(showcase.SpawnActors());
                GameplayComponentRuntimeShowcaseSnapshot spawned = showcase.CreateSnapshot();
                Assert.IsTrue(spawned.HeroAlive);
                Assert.IsTrue(spawned.EnemyAlive);
                Assert.AreEqual(30, spawned.HeroHp);
                Assert.AreEqual(10, spawned.HeroMana);
                Assert.AreEqual(12, spawned.EnemyHp);

                Assert.IsTrue(showcase.CastStrike());
                GameplayComponentRuntimeShowcaseSnapshot firstCast = showcase.CreateSnapshot();
                Assert.AreEqual(7, firstCast.HeroMana);
                Assert.AreEqual(6, firstCast.EnemyHp);
                Assert.AreEqual(2L, firstCast.StrikeCooldownRemainingFrames);
                AssertContains(firstCast, "AbilityCastSucceeded");

                Assert.IsTrue(showcase.CastStrike());
                GameplayComponentRuntimeShowcaseSnapshot rejectedCast = showcase.CreateSnapshot();
                Assert.AreEqual(7, rejectedCast.HeroMana);
                Assert.AreEqual(6, rejectedCast.EnemyHp);
                AssertContains(rejectedCast, "ComponentAbilityOnCooldown");

                Assert.IsTrue(showcase.CastStrike());
                GameplayComponentRuntimeShowcaseSnapshot secondCast = showcase.CreateSnapshot();
                Assert.AreEqual(4, secondCast.HeroMana);
                Assert.AreEqual(0, secondCast.EnemyHp);

                Assert.IsTrue(showcase.MarkEnemyPendingDestroyAndTick());
                GameplayComponentRuntimeShowcaseSnapshot cleaned = showcase.CreateSnapshot();
                Assert.IsFalse(cleaned.EnemyAlive);
                AssertContains(cleaned, "ComponentEntityDestroyed");
            }
        }

        [Test]
        public void Showcase_SaveRestoreThenContinueCastUsesRuntimeRegistries()
        {
            using (var showcase = new GameplayComponentRuntimeShowcase())
            {
                showcase.SpawnActors();
                showcase.CastStrike();
                Assert.IsTrue(showcase.Save());
                GameplayComponentRuntimeShowcaseSnapshot saved = showcase.CreateSnapshot();

                showcase.CastStrike();
                AssertContains(showcase.CreateSnapshot(), "ComponentAbilityOnCooldown");

                Assert.IsTrue(showcase.Restore());
                GameplayComponentRuntimeShowcaseSnapshot restored = showcase.CreateSnapshot();
                Assert.AreEqual(saved.HeroHp, restored.HeroHp);
                Assert.AreEqual(saved.HeroMana, restored.HeroMana);
                Assert.AreEqual(saved.EnemyHp, restored.EnemyHp);
                Assert.AreEqual(saved.HeroEntityId, restored.HeroEntityId);
                Assert.AreEqual(saved.EnemyEntityId, restored.EnemyEntityId);

                Assert.IsTrue(showcase.CastStrike());
                GameplayComponentRuntimeShowcaseSnapshot continued = showcase.CreateSnapshot();
                Assert.AreEqual(0, continued.EnemyHp);
                Assert.AreEqual(4, continued.HeroMana);
            }
        }

        private static void AssertContains(GameplayComponentRuntimeShowcaseSnapshot snapshot, string text)
        {
            Assert.IsTrue(
                snapshot.EventLog.Any(entry => entry.Contains(text)),
                "Expected event log to contain '" + text + "'. Actual: " + string.Join(" | ", snapshot.EventLog));
        }
    }
}

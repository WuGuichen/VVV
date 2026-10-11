using MxFramework.Combat.Hit;
using NUnit.Framework;

namespace MxFramework.Tests.Combat
{
    public sealed class CombatHitEventConfigTests
    {
        [Test]
        public void WithDefaults_ConvertsEmptyConfigToHostileHitEvent()
        {
            CombatHitEventConfig config = default;

            CombatHitEventConfig normalized = config.WithDefaults(950001, "light_attack.hit_start");

            Assert.AreEqual(CombatHitTargetTypes.HostileCharacter, normalized.TargetTypes);
            Assert.AreEqual(CombatEnvironmentHitPolicy.None, normalized.EnvironmentHitPolicy);
            Assert.AreEqual(950001, normalized.StableEventId);
            Assert.AreEqual(1, normalized.MaxAcceptedHits);
            Assert.AreEqual(CombatHitSameTargetPolicy.AllowRepeat, normalized.HitSameTargetPolicy);
        }

        [Test]
        public void Equals_TreatsImplicitAndExplicitDefaultsAsSameConfig()
        {
            CombatHitEventConfig implicitDefaults = default;
            CombatHitEventConfig explicitDefaults = new CombatHitEventConfig(
                CombatHitTargetTypes.HostileCharacter,
                CombatEnvironmentHitPolicy.None,
                0,
                maxAcceptedHits: 1,
                CombatHitSameTargetPolicy.AllowRepeat);

            Assert.AreEqual(
                explicitDefaults,
                implicitDefaults.WithDefaults(0));
            Assert.AreEqual(
                explicitDefaults.GetHashCode(),
                implicitDefaults.WithDefaults(0).GetHashCode());
        }

        [Test]
        public void HasAnyWorldTarget_DetectsEnvironmentTargetTypes()
        {
            Assert.IsFalse(CombatHitEventConfig.HasAnyWorldTarget(CombatHitTargetTypes.HostileCharacter));
            Assert.IsTrue(CombatHitEventConfig.HasAnyWorldTarget(CombatHitTargetTypes.WorldWall));
            Assert.IsTrue(CombatHitEventConfig.HasAnyWorldTarget(
                CombatHitTargetTypes.HostileCharacter | CombatHitTargetTypes.WorldGround));
        }

        [Test]
        public void DamageConfig_NormalizesTagsAndParticipatesInEquality()
        {
            var leftDamage = new CombatHitDamageConfig(
                950010,
                10,
                8,
                new[] { 950011, 950012, 950011 },
                950013,
                launchPower: 35);
            var rightDamage = new CombatHitDamageConfig(
                950010,
                10,
                8,
                new[] { 950012, 950011 },
                950013,
                launchPower: 35);

            CollectionAssert.AreEqual(new[] { 950012, 950011 }, leftDamage.Tags);
            Assert.AreEqual(35, leftDamage.LaunchPower);
            Assert.AreEqual(leftDamage, rightDamage);
            Assert.AreEqual(leftDamage.GetHashCode(), rightDamage.GetHashCode());

            var config = new CombatHitEventConfig(
                CombatHitTargetTypes.HostileCharacter,
                damage: leftDamage);
            CombatHitEventConfig normalized = config.WithDefaults(950002, "projectile.impact");

            Assert.AreEqual(leftDamage, normalized.Damage);
            Assert.AreEqual(950002, normalized.StableEventId);
        }
    }
}

using MxFramework.CharacterAction;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestUnifiedDamageTests
    {
        [Test]
        public void LegacyEventFormulaDoesNotOverrideGlobalSoftDefense()
        {
            var damage = new CharacterActionDamagePayload(
                damageTypeId: 1,
                formulaId: 60,
                baseDamage: 6,
                sourceAttributeScales: new[] { new DamageAttributeScale(2, 0.65f) });

            Assert.IsTrue(CharacterTestCombatDamageResolver.TryResolveCharacterActionDamage(
                damage, default, attribute => attribute == 2 ? 20 : 0,
                attribute => attribute == DamageFormulaIds.CharacterTestDefenseAttributeId ? 50 : 0,
                CharacterTestHitZoneResolution.Default, 0, out DamageProfileResolveResult result));
            Assert.IsTrue(result.Success);
            Assert.AreEqual(DamageFormulaIds.CharacterTestSoftDefenseV1, result.FormulaTrace.FormulaId);
            Assert.AreEqual(13, result.IncomingDamage);
        }

        [Test]
        public void ProjectileUsesSameCoefficientsAndZeroHpControlHitStillResolves()
        {
            var scaled = new CombatHitDamageConfig(
                damageTypeId: 1, baseDamage: 6,
                sourceAttributeScales: new[] { new DamageAttributeScale(2, 0.65f) });
            Assert.IsTrue(CharacterTestCombatDamageResolver.TryResolveProjectileDamagePayload(
                scaled, default, attribute => attribute == 2 ? 20 : 0,
                attribute => attribute == DamageFormulaIds.CharacterTestDefenseAttributeId ? 50 : 0,
                CharacterTestHitZoneResolution.Default, 0, out DamageProfileResolveResult scaledResult));
            Assert.IsTrue(scaledResult.Success);
            Assert.AreEqual(13, scaledResult.IncomingDamage);
            Assert.AreEqual(DamageFormulaIds.CharacterTestSoftDefenseV1, scaledResult.FormulaTrace.FormulaId);

            var control = new CombatHitDamageConfig(damageTypeId: 11, baseDamage: 0, tags: new[] { 25 });
            Assert.IsTrue(CharacterTestCombatDamageResolver.TryResolveProjectileDamagePayload(
                control, default, null, attribute => attribute == DamageFormulaIds.CharacterTestDefenseAttributeId ? 50 : 0,
                CharacterTestHitZoneResolution.Default, 0, out DamageProfileResolveResult controlResult));
            Assert.IsTrue(controlResult.Success);
            Assert.AreEqual(0, controlResult.IncomingDamage);
        }

        [Test]
        public void AbilityTimelinePreservesItsSourceCoefficient()
        {
            var authored = new AbilityDamageConfig(
                damageTypeId: 1, baseDamage: 6, impactPower: 34,
                tags: new[] { 14 }, analysisCategory: 28,
                sourceAttributeScales: new[] { new AbilityDamageAttributeScale(2, 0.65f) });
            CharacterActionDamagePayload damage =
                CharacterTestCharacterCombatTraceSystem.CreateAbilityDamagePayload(authored);
            Assert.AreEqual(1, damage.SourceAttributeScales.Length);
            Assert.AreEqual(0.65f, damage.SourceAttributeScales[0].Scale);
            Assert.IsTrue(CharacterTestCombatDamageResolver.TryResolveCharacterActionDamage(
                damage, default, attribute => attribute == 2 ? 20 : 0,
                attribute => attribute == DamageFormulaIds.CharacterTestDefenseAttributeId ? 50 : 0,
                CharacterTestHitZoneResolution.Default, 0, out DamageProfileResolveResult result));
            Assert.AreEqual(13, result.IncomingDamage);
            Assert.AreEqual(DamageFormulaIds.CharacterTestSoftDefenseV1, result.FormulaTrace.FormulaId);
        }

        [Test]
        public void PoisonStatusTickUsesSoftDefenseWithoutAnAttackCoefficient()
        {
            CharacterActionDamagePayload damage = CharacterTestCombatDamageResolver.PoisonTickDamage;
            Assert.IsEmpty(damage.SourceAttributeScales);
            Assert.IsTrue(CharacterTestCombatDamageResolver.TryResolveCharacterActionDamage(
                damage, default, null,
                attribute => attribute == DamageFormulaIds.CharacterTestDefenseAttributeId ? 50 : 0,
                CharacterTestHitZoneResolution.Default, 0, out DamageProfileResolveResult result));
            Assert.AreEqual(1, result.IncomingDamage);
            Assert.AreEqual(DamageFormulaIds.CharacterTestSoftDefenseV1, result.FormulaTrace.FormulaId);
        }
    }
}

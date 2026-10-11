using MxFramework.Combat.Damage;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat
{
    public sealed class DamageProfileResolverTests
    {
        [Test]
        public void Importer_ReadsDamageLightSlashCatalogProfile()
        {
            const string json = @"{
  ""schemaVersion"": ""2"",
  ""formulaDefaults"": {
    ""ct.soft_defense_v1"": {
      ""defenseReference"": 75,
      ""defenseWeight"": 0.4
    }
  },
  ""profiles"": [
    {
      ""damageProfileId"": 970001,
      ""stableId"": ""damage.light_slash"",
      ""displayName"": ""轻斩"",
      ""damageTypeId"": 970005,
      ""formulaId"": 4,
      ""baseDamage"": 6,
      ""sourceAttributeScales"": [
        { ""attributeId"": 2, ""scale"": 0.65 }
      ],
      ""minDamage"": 1,
      ""maxDamage"": 60,
      ""staggerFrames"": 8,
      ""knockback"": { ""x"": 0, ""y"": 0, ""z"": 0 },
      ""impactPower"": 12,
      ""launchPower"": 100,
      ""guardPressure"": 10,
      ""presentationProfileId"": 970006,
      ""tags"": [ 970007, 970008 ],
      ""analysisCategory"": 970009
    }
  ]
}";

            MemoryDamageProfileCatalog catalog = DamageProfileJsonImporter.Import(json);

            Assert.IsTrue(catalog.TryGet(970001, out DamageProfileConfig profile));
            Assert.AreEqual(DamageFormulaIds.CharacterTestSoftDefenseV1, profile.FormulaId);
            Assert.AreEqual(1, profile.SourceAttributeScales.Length);
            Assert.AreEqual(2, profile.SourceAttributeScales[0].AttributeId);
            Assert.AreEqual(0.65f, profile.SourceAttributeScales[0].Scale);
            Assert.AreEqual(0, profile.TargetAttributeScales.Length);
            Assert.AreEqual(1f, profile.DamageMultiplier);
            Assert.AreEqual(75, profile.DefenseReference);
            Assert.AreEqual(0.4f, profile.DefenseWeight);
            Assert.AreEqual(12, profile.ImpactPower);
            Assert.AreEqual(100, profile.LaunchPower);
            DamageProfileCatalogRow[] rows = DamageProfileJsonImporter.CreateCatalogRows(catalog);
            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(970001, rows[0].DamageProfileId);
            Assert.AreEqual(12, rows[0].ImpactPower);
            Assert.AreEqual(100, rows[0].LaunchPower);
            Assert.AreEqual(970009, rows[0].AnalysisCategory);
        }

        [Test]
        public void Importer_IgnoresLegacyScatterPressureDamageField()
        {
            const string json = @"{
  ""schemaVersion"": ""2"",
  ""profiles"": [
    {
      ""damageProfileId"": 970002,
      ""damageTypeId"": 970005,
      ""formulaId"": 1,
      ""baseDamage"": 6,
      ""scatterDamage"": 99
    }
  ]
}";

            MemoryDamageProfileCatalog catalog = DamageProfileJsonImporter.Import(json);

            Assert.IsTrue(catalog.TryGet(970002, out DamageProfileConfig profile));
            Assert.AreEqual(6, profile.BaseDamage);
            Assert.AreEqual(0, profile.ImpactPower);
            DamageProfileResolveResult result = DamageProfileResolver.Resolve(
                profile,
                new DamageProfileUsage(DamageProfileSourceKind.WeaponTrace, MxFramework.Tests.CharacterActionLegacyFixtureIds.From("legacy")));
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.ImpactPower);
        }

        [Test]
        public void Resolve_CharacterTestSoftDefenseUsesDeterministicMitigationTrace()
        {
            var profile = new DamageProfileConfig(
                970003,
                formulaId: DamageFormulaIds.CharacterTestSoftDefenseV1,
                baseDamage: 6,
                sourceAttributeScales: new[] { new DamageAttributeScale(1, 0.65f) },
                damageMultiplier: 1f,
                defenseReference: 50,
                defenseWeight: 0.6f,
                impactPower: 12,
                guardPressure: 6);

            DamageProfileResolveResult result = DamageProfileResolver.Resolve(
                profile,
                new DamageProfileUsage(DamageProfileSourceKind.WeaponTrace, new MxFramework.CharacterAction.CharacterActionId(301)),
                attributeId => attributeId == 1 ? 20 : 0,
                attributeId => attributeId == DamageFormulaIds.CharacterTestDefenseAttributeId ? 10 : 0);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(17, result.IncomingDamage);
            Assert.AreEqual(12, result.ImpactPower);
            Assert.AreEqual(0, result.GuardPressure);
            Assert.AreEqual(DamageFormulaIds.CharacterTestSoftDefenseV1, result.FormulaTrace.FormulaId);
            Assert.AreEqual(20f, result.FormulaTrace.SourceAttributeValue);
            Assert.AreEqual(0.65f, result.FormulaTrace.SourceAttributeScale);
            Assert.AreEqual(13f, result.FormulaTrace.SourceAttributeContribution);
            Assert.AreEqual(19f, result.FormulaTrace.Offense);
            Assert.AreEqual(10f, result.FormulaTrace.TargetDefense);
            Assert.AreEqual(50, result.FormulaTrace.DefenseReference);
            Assert.AreEqual(0.6f, result.FormulaTrace.DefenseWeight);
            Assert.AreEqual(0.1f, result.FormulaTrace.Mitigation, 0.0001f);
            Assert.AreEqual(17.1f, result.FormulaTrace.RawDamage, 0.0001f);
            Assert.AreEqual(17, result.FormulaTrace.IncomingDamage);
        }

        [Test]
        public void Resolve_AppliesAttributeScalesBodyPartMultipliersWithoutDamageClamp()
        {
            var profile = new DamageProfileConfig(
                970004,
                formulaId: DamageFormulaIds.AttackScaledMinusDefense,
                baseDamage: 5,
                sourceAttributeScales: new[] { new DamageAttributeScale(1, 1.5f) },
                targetAttributeScales: new[] { new DamageAttributeScale(2, -0.5f) },
                minDamage: 20,
                maxDamage: 10,
                staggerFrames: 10,
                knockback: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                impactPower: 20,
                launchPower: 100,
                guardPressure: 7,
                presentationProfileId: 970010);

            DamageProfileResolveResult result = DamageProfileResolver.Resolve(
                profile,
                new DamageProfileUsage(DamageProfileSourceKind.WeaponTrace, new MxFramework.CharacterAction.CharacterActionId(301)),
                attributeId => attributeId == 1 ? 20 : 0,
                attributeId => attributeId == 2 ? 8 : 0,
                damageMultiplier: 1.5f,
                staggerScale: 0.5f,
                impactPowerScale: 2f);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(970004, result.DamageProfileId);
            Assert.AreEqual(47, result.IncomingDamage);
            Assert.AreEqual(0, result.StaggerFrames);
            Assert.AreEqual(0, result.ImpactForce);
            Assert.AreEqual(FixVector3.Zero, result.Knockback);
            Assert.AreEqual(40, result.ImpactPower);
            Assert.AreEqual(100, result.LaunchPower);
            Assert.AreEqual(0, result.GuardPressure);
            Assert.AreEqual(970010, result.PresentationProfileId);
        }

        [Test]
        public void Resolve_FloorsNegativeDamageToZero()
        {
            var profile = new DamageProfileConfig(
                970005,
                formulaId: DamageFormulaIds.AttackScaledMinusDefense,
                baseDamage: 1,
                targetAttributeScales: new[] { new DamageAttributeScale(2, -2f) },
                minDamage: 20,
                maxDamage: 30);

            DamageProfileResolveResult result = DamageProfileResolver.Resolve(
                profile,
                new DamageProfileUsage(DamageProfileSourceKind.Scripted),
                null,
                attributeId => attributeId == 2 ? 10 : 0);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.IncomingDamage);
        }

        [Test]
        public void Resolve_UnknownFormulaFailsWithoutExpressionFallback()
        {
            var profile = new DamageProfileConfig(
                970006,
                formulaId: 999999,
                baseDamage: 10);

            DamageProfileResolveResult result = DamageProfileResolver.Resolve(
                profile,
                new DamageProfileUsage(DamageProfileSourceKind.Scripted));

            Assert.IsFalse(result.Success);
            StringAssert.Contains("unknown", result.Reason);
            Assert.AreEqual(0, result.IncomingDamage);
        }
    }
}

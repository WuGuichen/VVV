using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public sealed class AbilityAuthoringContractTests
    {
        private const int AttrHp = 1;
        private const int AttrAttack = 2;
        private const int AttrDefense = 3;
        private const int AbilityStrike = 300001;
        private const int BuffBurning = 100001;

        [Test]
        public void AbilityAuthoringContractMapper_ValidDamageContract_MapsToBasicAbilityConfig()
        {
            AbilityAuthoringContract contract = CreateDamageContract();

            bool mapped = AbilityAuthoringContractMapper.TryMap(
                contract,
                out BasicAbilityConfig config,
                out AbilityAuthoringValidationReport report);

            Assert.IsTrue(mapped);
            Assert.IsTrue(report.IsValid);
            Assert.AreEqual(AbilityStrike, config.Id);
            Assert.AreEqual("Strike", config.NameText.Value);
            Assert.AreEqual(AbilityTargetSelectorKind.SingleEnemy, config.TargetSelectorKind);
            Assert.AreEqual(1, config.Effects.Length);
            Assert.AreEqual(AbilityEffectKind.DamageByAttackDefense, config.Effects[0].Kind);
            Assert.AreEqual(AttrAttack, config.Effects[0].NamedParameters.AttackAttributeId);
            Assert.AreEqual(AttrDefense, config.Effects[0].NamedParameters.DefenseAttributeId);
            Assert.AreEqual(AttrHp, config.Effects[0].NamedParameters.HpAttributeId);
        }

        [Test]
        public void AbilityAuthoringContractMapper_ValidApplyBuffContract_MapsNamedBuffParameter()
        {
            var contract = new AbilityAuthoringContract
            {
                AbilityId = AbilityStrike,
                DisplayName = "Ignite",
                Description = "Apply a buff.",
                TargetSelectorKind = AbilityAuthoringTargetSelectorKind.SingleEnemy,
                Effects = new[]
                {
                    AbilityAuthoringEffectContract.ApplyBuff(BuffBurning)
                }
            };

            bool mapped = AbilityAuthoringContractMapper.TryMap(
                contract,
                out BasicAbilityConfig config,
                out AbilityAuthoringValidationReport report);

            Assert.IsTrue(mapped);
            Assert.IsTrue(report.IsValid);
            Assert.AreEqual(AbilityEffectKind.ApplyBuff, config.Effects[0].Kind);
            Assert.AreEqual(BuffBurning, config.Effects[0].NamedParameters.BuffId);
        }

        [Test]
        public void AbilityAuthoringContract_ConfigFactory_CreatesAndCastsMappedAbility()
        {
            AbilityAuthoringContractMapper.TryMap(
                CreateDamageContract(),
                out BasicAbilityConfig config,
                out AbilityAuthoringValidationReport report);
            Assert.IsTrue(report.IsValid);
            var table = new ConfigTable<BasicAbilityConfig>(BasicAbilityConfig.CreateSchema());
            table.Add(config);
            var factory = new ConfigAbilityFactory(table);
            Assert.IsTrue(factory.TryCreate(AbilityStrike, out IAbility ability, out string error), error);
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity enemy = CreateEntity(2, 2, 600, 80, 10);

            AbilityCastResult result = ability.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(490, enemy.Store.GetAttribute(AttrHp));
        }

        [Test]
        public void AbilityAuthoringContractValidator_InvalidInputs_ReturnStableCodes()
        {
            var contract = new AbilityAuthoringContract
            {
                ContractVersion = 99,
                AbilityId = 299999,
                DisplayName = "",
                TargetSelectorKind = (AbilityAuthoringTargetSelectorKind)999,
                Effects = new[]
                {
                    new AbilityAuthoringEffectContract { Kind = (AbilityAuthoringEffectKind)999 }
                }
            };

            AbilityAuthoringValidationReport report = AbilityAuthoringContractValidator.Validate(contract);

            Assert.IsFalse(report.IsValid);
            AssertHasCode(report, AbilityAuthoringValidationCode.UnsupportedContractVersion);
            AssertHasCode(report, AbilityAuthoringValidationCode.InvalidAbilityId);
            AssertHasCode(report, AbilityAuthoringValidationCode.MissingDisplayName);
            AssertHasCode(report, AbilityAuthoringValidationCode.UnknownTargetSelector);
            AssertHasCode(report, AbilityAuthoringValidationCode.UnknownEffectKind);
        }

        [Test]
        public void AbilityAuthoringContractValidator_MissingEffectParameters_ReturnStableCodes()
        {
            var contract = new AbilityAuthoringContract
            {
                AbilityId = AbilityStrike,
                DisplayName = "Strike",
                TargetSelectorKind = AbilityAuthoringTargetSelectorKind.SingleEnemy,
                Effects = new[]
                {
                    AbilityAuthoringEffectContract.DamageByAttackDefense(AttrAttack, 0, -1),
                    AbilityAuthoringEffectContract.ApplyBuff(0)
                }
            };

            AbilityAuthoringValidationReport report = AbilityAuthoringContractValidator.Validate(contract);

            Assert.IsFalse(report.IsValid);
            AssertHasCode(report, AbilityAuthoringValidationCode.MissingEffectParameter);
            AssertHasCode(report, AbilityAuthoringValidationCode.InvalidAttributeId);
        }

        [Test]
        public void AbilityAuthoringContractValidator_MissingAbilityAndEffects_ReturnStableCodes()
        {
            var contract = new AbilityAuthoringContract
            {
                DisplayName = "Empty",
                TargetSelectorKind = AbilityAuthoringTargetSelectorKind.Self,
                Effects = new AbilityAuthoringEffectContract[0]
            };

            AbilityAuthoringValidationReport report = AbilityAuthoringContractValidator.Validate(contract);

            Assert.IsFalse(report.IsValid);
            AssertHasCode(report, AbilityAuthoringValidationCode.MissingAbilityId);
            AssertHasCode(report, AbilityAuthoringValidationCode.MissingEffect);
        }

        [Test]
        public void AbilityAuthoringSchemaSummary_IncludesCoreFieldsAllowedValuesAndErrorCodes()
        {
            AbilityAuthoringSchemaSummary summary = AbilityAuthoringSchema.CreateSummary();

            Assert.AreEqual(AbilityAuthoringContract.CurrentVersion, summary.ContractVersion);
            AssertField(summary.Fields, "AbilityId");
            AssertField(summary.Fields, "TargetSelectorKind");
            AssertField(summary.Fields, "Effects[].Kind");
            AssertField(summary.Fields, "Effects[].AttackAttributeId");
            AssertContains(summary.ErrorCodes, AbilityAuthoringValidationCode.MissingAbilityId);
            AssertContains(summary.ErrorCodes, AbilityAuthoringValidationCode.UnknownEffectKind);
            AssertContains(summary.ErrorCodes, AbilityAuthoringValidationCode.UnsupportedContractVersion);
        }

        private static AbilityAuthoringContract CreateDamageContract()
        {
            return new AbilityAuthoringContract
            {
                AbilityId = AbilityStrike,
                DisplayName = "Strike",
                Description = "Deal attack minus defense damage.",
                TargetSelectorKind = AbilityAuthoringTargetSelectorKind.SingleEnemy,
                Effects = new[]
                {
                    AbilityAuthoringEffectContract.DamageByAttackDefense(AttrAttack, AttrDefense, AttrHp)
                }
            };
        }

        private static RuntimeEntity CreateEntity(int id, int team, int hp, int attack, int defense)
        {
            var entity = new RuntimeEntity(id, team, AttrHp);
            entity.Store.RegisterAttribute(AttrHp, hp);
            entity.Store.RegisterAttribute(AttrAttack, attack);
            entity.Store.RegisterAttribute(AttrDefense, defense);
            return entity;
        }

        private static void AssertHasCode(
            AbilityAuthoringValidationReport report,
            AbilityAuthoringValidationCode code)
        {
            Assert.IsTrue(report.Contains(code), "Expected validation code: " + code);
        }

        private static void AssertField(
            IReadOnlyList<AbilityAuthoringFieldDescriptor> fields,
            string fieldPath)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].FieldPath == fieldPath)
                    return;
            }

            Assert.Fail("Expected schema field: " + fieldPath);
        }

        private static void AssertContains(
            IReadOnlyList<AbilityAuthoringValidationCode> codes,
            AbilityAuthoringValidationCode expected)
        {
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i] == expected)
                    return;
            }

            Assert.Fail("Expected schema error code: " + expected);
        }
    }
}

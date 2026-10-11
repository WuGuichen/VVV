using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    /// <summary>Checks policy with native action contracts before any package or Ability host is touched.</summary>
    public sealed class CharacterTestWeaponTypePolicyTests
    {
        private static CharacterActionConfig Action(int id, CharacterActionCategory category, params CharacterActionRequirement[] requirements)
            => new CharacterActionConfig(new CharacterActionId(id), "Weapon policy test", category,
                CharacterActionTimelineAuthority.CharacterAuthored, null, 0, 0, requirements, null, null, null);

        [TestCase(410)] [TestCase(999979)]
        [TestCase(950000)] [TestCase(950001)] [TestCase(950002)]
        public void UnclassifiedBasicAttackRejectsBeforeEquipmentOrCosts(int id)
        {
            Assert.IsFalse(CharacterTestWeaponTypeAdmission.Evaluate(null, null, Action(id, CharacterActionCategory.BasicAttack), out _, out string issue));
            Assert.AreEqual("WeaponTypeClassificationPending:action=" + id, issue);
        }

        [TestCase(301)] [TestCase(324)] [TestCase(325)]
        public void ReviewedInitialAttackUsesSwordRequirement(int id)
        {
            var requirement = new CharacterActionRequirement(CharacterActionRequirementKind.WeaponTypeRequired, "sword", 1);
            Assert.IsTrue(requirement.HasValidReferenceShape());
            Assert.IsFalse(CharacterTestWeaponTypeAdmission.Evaluate(null, null,
                Action(id, CharacterActionCategory.BasicAttack, requirement), out _, out string issue));
            Assert.IsFalse(issue.StartsWith("WeaponTypeClassificationPending:"));
        }

        [Test]
        public void BodyAttackRequiresAnExplicitIndependentDeclaration()
        {
            Assert.IsTrue(CharacterTestWeaponTypeAdmission.Evaluate(null, null,
                Action(940000, CharacterActionCategory.BasicAttack, new CharacterActionRequirement(CharacterActionRequirementKind.WeaponTypeIndependent)), out _, out _));
        }

        [TestCase(CharacterActionCategory.Guard)] [TestCase(CharacterActionCategory.Dodge)]
        public void GenericDefenseAndDodgeNeedNoPhysicalWeapon(CharacterActionCategory category)
            => Assert.IsTrue(CharacterTestWeaponTypeAdmission.Evaluate(null, null, Action(999979, category), out _, out _));

        [Test]
        public void IndependentAndRequiredDeclarationsCannotBeCombined()
        {
            Assert.IsFalse(CharacterTestWeaponTypeAdmission.Evaluate(null, null,
                Action(999978, CharacterActionCategory.BasicAttack,
                    new CharacterActionRequirement(CharacterActionRequirementKind.WeaponTypeIndependent),
                    new CharacterActionRequirement(CharacterActionRequirementKind.WeaponTypeRequired, "staff", 1)), out _, out string issue));
            Assert.AreEqual("WeaponTypeRequirementConflict", issue);
        }
    }
}

using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterCommandTests
    {
        [Test]
        public void Constructor_ClampsHorizontalMoveAndNormalizesTrace()
        {
            var entity = CharacterControlEntityRef.FromGameplayAndCombat(
                new GameplayEntityId(1, 1),
                new CombatEntityId(10),
                new CombatBodyId(20),
                stableId: 7);

            var command = new CharacterCommand(
                RuntimeFrame.Zero,
                sourceId: 3,
                entity,
                new FixVector3(Fix64.FromInt(4), Fix64.FromInt(9), Fix64.Zero),
                CharacterFacingBasis.Identity,
                jumpPressed: true,
                sprintHeld: false,
                CharacterActionButtons.Primary,
                default(CharacterActionRequest),
                traceId: null);

            Assert.AreEqual(Fix64.One, command.MoveDirection.X);
            Assert.AreEqual(Fix64.Zero, command.MoveDirection.Y);
            Assert.AreEqual(Fix64.Zero, command.MoveDirection.Z);
            Assert.AreEqual(string.Empty, command.TraceId);
            Assert.AreEqual(entity, command.Entity);
        }

        [Test]
        public void FacingBasis_FromForwardMapsLocalForwardToWorldForward()
        {
            CharacterFacingBasis basis = CharacterFacingBasis.FromForward(new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero));

            FixVector3 world = basis.ToWorldDirection(new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One));

            Assert.AreEqual(new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), world);
        }

        [Test]
        public void Constructor_RejectsNegativeMoveSpeedScale()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CharacterCommand(
                RuntimeFrame.Zero,
                sourceId: 0,
                default,
                FixVector3.Zero,
                CharacterFacingBasis.Identity,
                jumpPressed: false,
                sprintHeld: false,
                CharacterActionButtons.None,
                default,
                -Fix64.One));
        }

        [Test]
        public void ActionRequest_FactoriesCreateStableRequests()
        {
            var entity = CharacterControlEntityRef.FromGameplay(new GameplayEntityId(2, 1), stableId: 2);
            var target = new GameplayEntityId(3, 1);

            CharacterActionRequest combat = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                entity,
                CharacterActionKind.Attack,
                combatActionId: 1001,
                sourceId: 8,
                traceId: "atk");
            CharacterActionRequest ability = CharacterActionRequest.GameplayAbility(
                RuntimeFrame.Zero,
                entity,
                abilityId: 300001,
                target,
                sourceId: 9,
                traceId: "skill");

            Assert.IsTrue(combat.HasCombatAction);
            Assert.AreEqual(1001, combat.CombatActionId);
            Assert.IsTrue(ability.HasGameplayAbility);
            Assert.IsTrue(ability.HasTarget);
            Assert.AreEqual(target, ability.TargetGameplayEntityId);
            Assert.AreEqual("skill", ability.TraceId);
        }
    }
}

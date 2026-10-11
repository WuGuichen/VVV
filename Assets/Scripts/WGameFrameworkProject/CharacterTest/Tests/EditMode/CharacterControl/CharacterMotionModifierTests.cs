using System.Collections.Generic;
using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterMotionModifierTests
    {
        [Test]
        public void NoProviders_ReturnsIdentityScale()
        {
            var motor = new CombatKinematicMotor(CombatMotionConfig.Default);
            var resolver = new CharacterMotionResolver(motor);
            var machine = new CharacterControlStateMachine();
            var command = new CharacterCommand(
                RuntimeFrame.Zero,
                0,
                default,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                CharacterFacingBasis.Identity,
                jumpPressed: false,
                sprintHeld: false,
                CharacterActionButtons.None,
                default);

            CharacterMotionResult result = resolver.Step(command, machine, CreateMotionState());

            Assert.AreEqual(Fix64.One, result.ModifierResult.FinalMoveSpeedScale);
            Assert.AreEqual(0, result.ModifierResult.Count);
            Assert.AreEqual(Fix64.One, result.MotionInput.MoveSpeedScale);
        }

        [Test]
        public void Providers_AreSortedAndMultipliedDeterministically()
        {
            var motor = new CombatKinematicMotor(CombatMotionConfig.Default);
            var resolver = new CharacterMotionResolver(
                motor,
                CharacterMotionSettings.Default,
                new ICharacterMotionModifierProvider[]
                {
                    new FixedProvider(new CharacterMotionModifier("buff.slow", Fix64.Half, "slow", priority: 10)),
                    new FixedProvider(new CharacterMotionModifier("traction", Fix64.FromRatio(3, 2), "ice", priority: 0))
                });
            var machine = new CharacterControlStateMachine();
            var command = new CharacterCommand(
                RuntimeFrame.Zero,
                0,
                default,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                CharacterFacingBasis.Identity,
                jumpPressed: false,
                sprintHeld: false,
                CharacterActionButtons.None,
                default);

            CharacterMotionResult result = resolver.Step(command, machine, CreateMotionState());

            Assert.AreEqual(Fix64.FromRatio(3, 4), result.ModifierResult.FinalMoveSpeedScale);
            Assert.AreEqual("traction", result.ModifierResult.Modifiers[0].Source);
            Assert.AreEqual("buff.slow", result.ModifierResult.Modifiers[1].Source);
            Assert.AreEqual(Fix64.FromRatio(3, 4), result.MotionInput.MoveSpeedScale);
        }

        private sealed class FixedProvider : ICharacterMotionModifierProvider
        {
            private readonly CharacterMotionModifier _modifier;

            public FixedProvider(CharacterMotionModifier modifier)
            {
                _modifier = modifier;
            }

            public void CollectModifiers(CharacterMotionModifierContext context, IList<CharacterMotionModifier> destination)
            {
                destination.Add(_modifier);
            }
        }

        private static CombatMotionState CreateMotionState()
        {
            return new CombatMotionState(
                CombatFrame.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                grounded: true,
                lastCollisionNormal: FixVector3.Zero,
                CombatMotionCollisionFlags.None);
        }
    }
}

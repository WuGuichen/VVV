using System.Collections;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    public sealed class CharacterTestLandingActionPlayModeTests
    {
        [UnityTest]
        public IEnumerator NormalLanding_StartsLightAction()
        {
            yield return null;

            RunLanding(1000, CharacterTestCharacterActionConfigSource.LandingLightActionId, 1500);
        }

        [UnityTest]
        public IEnumerator SubThresholdLanding_DoesNotStartLandingAction()
        {
            yield return null;

            RunLanding(0, CharacterActionId.None, 1000);
        }

        [UnityTest]
        public IEnumerator HeavyLanding_StartsHeavyAction()
        {
            yield return null;

            RunLanding(9000, CharacterTestCharacterActionConfigSource.LandingHeavyActionId, 800);
        }

        [UnityTest]
        public IEnumerator LandingSlots_FallbackBothDirections()
        {
            yield return null;

            var lightOnly = new CharacterLandingProfile(
                "landing.light",
                CharacterTestCharacterActionConfigSource.LandingLightActionId,
                CharacterActionId.None);
            Assert.That(lightOnly.TryResolveAction(
                heavy: true,
                out CharacterActionId lightActionId,
                out bool resolvedHeavy,
                out bool usedFallback), Is.True);
            Assert.That(lightActionId, Is.EqualTo(CharacterTestCharacterActionConfigSource.LandingLightActionId));
            Assert.That(resolvedHeavy, Is.False);
            Assert.That(usedFallback, Is.True);

            var heavyOnly = new CharacterLandingProfile(
                "landing.heavy",
                CharacterActionId.None,
                CharacterTestCharacterActionConfigSource.LandingHeavyActionId);
            Assert.That(heavyOnly.TryResolveAction(
                heavy: false,
                out CharacterActionId heavyActionId,
                out resolvedHeavy,
                out usedFallback), Is.True);
            Assert.That(heavyActionId, Is.EqualTo(CharacterTestCharacterActionConfigSource.LandingHeavyActionId));
            Assert.That(resolvedHeavy, Is.True);
            Assert.That(usedFallback, Is.True);
        }

        private static void RunLanding(
            int downwardSpeedMillimetersPerSecond,
            CharacterActionId expectedActionId,
            int expectedPlaybackSpeedMilli)
        {
            var definitions = new CharacterTestCharacterDefinitionRegistry();
            Assert.That(definitions.TryGet(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                out CharacterTestCharacterDefinition definition), Is.True);
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0.005f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            entity.SetMotionStateDeterministic(
                -downwardSpeedMillimetersPerSecond,
                0,
                0,
                isGrounded: false);

            var motion = new CharacterTestCharacterMotionSystem();
            var actions = new CharacterTestCharacterActionSystem(NullRuntimeLogger.Instance);
            actions.Runtime.BindRuntimeWorld(world);
            var frame = new RuntimeFrame(1);

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, frame);
            Assert.That(motion.LandingEventCount, Is.EqualTo(1));

            actions.ProcessLandingActions(motion, frame);

            if (!expectedActionId.IsValid)
            {
                Assert.That(entity.Action.ActiveActionId.IsValid, Is.False);
                Assert.That(world.CharacterComponents.GetAnimationRequest(entity.Id).IsValid, Is.False);
                return;
            }

            Assert.That(entity.Action.ActiveActionId, Is.EqualTo(expectedActionId));
            Assert.That(entity.Action.AnimationPlaybackSpeedMilli, Is.EqualTo(expectedPlaybackSpeedMilli));
            Assert.That(
                world.CharacterComponents.GetAnimationRequest(entity.Id).PlaybackSpeedMilli,
                Is.EqualTo(expectedPlaybackSpeedMilli));
        }
    }
}

using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestGameplayCameraProfileTests
    {
        [Test]
        public void FromProfile_PreservesExplicitCharacterCameraContract()
        {
            var profile = new CharacterGameplayCameraProfile(
                "camera.000003.gameplay",
                "body_center",
                new CharacterGameplayCameraVector3(0f, 0.1f, 0f),
                new CharacterGameplayCameraVector3(0.06f, 1.77f, -0.32f),
                new CharacterGameplayCameraVector3(7.03f, 3.24f, 8.58f),
                new CharacterGameplayCameraNormalSettings(14f, 10f, 20f, 60f, 24f, 0f, 0.8f),
                new CharacterGameplayCameraLockedSettings(14f, 28f, 0.62f, 1.5f, 0.65f),
                new CharacterGameplayCameraObstructionSettings(0.55f, 0.3f, 3f, 0.8f));

            CharacterTestGameplayCameraSettings settings = CharacterTestGameplayCameraSettings.FromProfile(profile);

            Assert.IsTrue(settings.ExplicitProfile);
            Assert.AreEqual("body_center", settings.FocusAbstractBoneId);
            Assert.AreEqual(new Vector3(7.03f, 3.24f, 8.58f), settings.FramingBoundsExtents);
            Assert.AreEqual(14f, settings.Distance);
            Assert.AreEqual(28f, settings.LockedMaxDistance);
            Assert.AreEqual(0.65f, settings.LockedFramingScale);
            Assert.AreEqual(0.55f, settings.ObstructionRadius);
        }

        [Test]
        public void LockedComposer_UsesEachCharactersOwnBoundsAndHonorsPlayerRange()
        {
            CharacterTestGameplayCameraSettings playerSettings = CreateSettings(new Vector3(0.5f, 1f, 0.5f));
            CharacterTestGameplayCameraSettings smallTargetSettings = CreateSettings(new Vector3(0.5f, 1f, 0.5f));
            CharacterTestGameplayCameraSettings dragonTargetSettings = CreateSettings(new Vector3(7.03f, 3.24f, 8.58f));
            var player = CreateTarget(Vector3.zero, playerSettings.FramingBoundsExtents, playerSettings);
            var smallTarget = CreateTarget(new Vector3(0f, 0f, 8f), smallTargetSettings.FramingBoundsExtents, smallTargetSettings);
            var dragonTarget = CreateTarget(new Vector3(0f, 0f, 8f), dragonTargetSettings.FramingBoundsExtents, dragonTargetSettings);
            var composeSettings = new CharacterTestLockedThirdPersonCameraCompositionSettings(
                14f,
                14f,
                28f,
                60f,
                16f / 9f,
                0.62f,
                1.5f,
                0f,
                24f);

            CharacterTestLockedThirdPersonCameraComposition small = CharacterTestLockedThirdPersonCameraComposer.Compose(player, smallTarget, composeSettings);
            CharacterTestLockedThirdPersonCameraComposition dragon = CharacterTestLockedThirdPersonCameraComposer.Compose(player, dragonTarget, composeSettings);

            Assert.That(small.Distance, Is.InRange(14f, 28f));
            Assert.That(dragon.Distance, Is.InRange(14f, 28f));
            float smallBackDistance = -ResolveCameraPosition(player.BoundsCenter, small).z;
            float dragonBackDistance = -ResolveCameraPosition(player.BoundsCenter, dragon).z;
            Assert.GreaterOrEqual(dragonBackDistance, smallBackDistance);
        }

        [Test]
        public void LockedComposer_DragonPlayerAtCloseRange_UsesProjectedBoundsWithoutRadialOverpull()
        {
            CharacterTestGameplayCameraSettings dragonSettings = CreateSettings(new Vector3(7.03f, 3.24f, 8.58f));
            CharacterTestGameplayCameraSettings humanSettings = CreateSettings(new Vector3(0.6f, 1.1f, 0.6f), false);
            var dragon = CreateTarget(new Vector3(0f, 3.24f, 0f), dragonSettings.FramingBoundsExtents, dragonSettings);
            var human = CreateTarget(new Vector3(0f, 0f, 8f), humanSettings.FramingBoundsExtents, humanSettings);
            var composeSettings = new CharacterTestLockedThirdPersonCameraCompositionSettings(
                14f,
                14f,
                28f,
                60f,
                16f / 9f,
                0.62f,
                1.5f,
                0f,
                24f);

            CharacterTestLockedThirdPersonCameraComposition composition =
                CharacterTestLockedThirdPersonCameraComposer.Compose(dragon, human, composeSettings);

            Assert.That(composition.Distance, Is.InRange(14f, 21f));
        }

        [Test]
        public void LockedComposer_DragonCombatFramingScale_KeepsCloseTargetNearMinimumDistance()
        {
            CharacterTestGameplayCameraSettings dragonSettings = CreateSettings(
                new Vector3(7.03f, 3.24f, 8.58f),
                true,
                0.65f);
            CharacterTestGameplayCameraSettings humanSettings = CreateSettings(new Vector3(0.6f, 1.1f, 0.6f), false);
            var dragon = CreateTarget(new Vector3(0f, 3.24f, 0f), dragonSettings.FramingBoundsExtents, dragonSettings);
            var human = CreateTarget(new Vector3(0f, 0f, 2f), humanSettings.FramingBoundsExtents, humanSettings);
            var composeSettings = new CharacterTestLockedThirdPersonCameraCompositionSettings(
                14f, 10f, 28f, 60f, 16f / 9f, 0.62f, 1f, 0f, 24f);

            CharacterTestLockedThirdPersonCameraComposition composition =
                CharacterTestLockedThirdPersonCameraComposer.Compose(dragon, human, composeSettings);

            Assert.That(composition.Distance, Is.InRange(10f, 13f));
        }

        [Test]
        public void LockedComposer_ExplicitPlayerFocusRemainsTheOpticalCenter()
        {
            CharacterTestGameplayCameraSettings dragonSettings = CreateSettings(
                new Vector3(7.03f, 3.24f, 8.58f),
                true,
                0.5f);
            CharacterTestGameplayCameraSettings humanSettings = CreateSettings(
                new Vector3(0.6f, 1.1f, 0.6f),
                false);
            var dragon = CreateTarget(new Vector3(0f, 3.24f, 0f), dragonSettings.FramingBoundsExtents, dragonSettings);
            var human = CreateTarget(new Vector3(2f, 0f, 8f), humanSettings.FramingBoundsExtents, humanSettings);
            var composeSettings = new CharacterTestLockedThirdPersonCameraCompositionSettings(
                12f, 10f, 28f, 60f, 16f / 9f, 0.62f, 1f, 0f, 24f);

            CharacterTestLockedThirdPersonCameraComposition composition =
                CharacterTestLockedThirdPersonCameraComposer.Compose(dragon, human, composeSettings);

            Vector3 cameraPosition = ResolveCameraPosition(dragon.BoundsCenter, composition);
            Vector3 opticalCenter = cameraPosition + ResolveForward(composition) * composition.Distance;
            Assert.That(Vector3.Distance(dragon.Focus, opticalCenter), Is.LessThan(0.0001f));
            Assert.AreEqual(dragon.Focus - dragon.BoundsCenter, composition.WorldOffset);
            Assert.That(composition.Pitch, Is.InRange(12f, 36f));
        }

        [Test]
        public void LockedComposer_ExplicitPlayerVerticalOverflowDoesNotPullCameraBack()
        {
            CharacterTestGameplayCameraSettings normalHeightSettings = CreateSettings(
                new Vector3(7.03f, 3.24f, 8.58f),
                true,
                0.5f);
            CharacterTestGameplayCameraSettings overflowingHeightSettings = CreateSettings(
                new Vector3(7.03f, 30f, 8.58f),
                true,
                0.5f);
            CharacterTestGameplayCameraSettings humanSettings = CreateSettings(
                new Vector3(0.6f, 1.1f, 0.6f),
                false);
            var normalHeightDragon = CreateTarget(
                new Vector3(0f, 3.24f, 0f),
                normalHeightSettings.FramingBoundsExtents,
                normalHeightSettings);
            var overflowingHeightDragon = CreateTarget(
                new Vector3(0f, 3.24f, 0f),
                overflowingHeightSettings.FramingBoundsExtents,
                overflowingHeightSettings);
            var human = CreateTarget(new Vector3(0f, 0f, 4f), humanSettings.FramingBoundsExtents, humanSettings);
            var composeSettings = new CharacterTestLockedThirdPersonCameraCompositionSettings(
                12f, 10f, 28f, 60f, 16f / 9f, 0.62f, 1f, 0f, 24f);

            CharacterTestLockedThirdPersonCameraComposition normalHeight =
                CharacterTestLockedThirdPersonCameraComposer.Compose(normalHeightDragon, human, composeSettings);
            CharacterTestLockedThirdPersonCameraComposition overflowingHeight =
                CharacterTestLockedThirdPersonCameraComposer.Compose(overflowingHeightDragon, human, composeSettings);

            Assert.AreEqual(normalHeight.Distance, overflowingHeight.Distance, 0.0001f);
        }

        [Test]
        public void LockedComposer_HumanPlayerAgainstDragon_UsesTargetProjectedBounds()
        {
            CharacterTestGameplayCameraSettings humanSettings = CreateSettings(new Vector3(0.6f, 1.1f, 0.6f), false);
            CharacterTestGameplayCameraSettings dragonSettings = CreateSettings(new Vector3(7.03f, 3.24f, 8.58f));
            var human = CreateTarget(Vector3.zero, humanSettings.FramingBoundsExtents, humanSettings);
            var dragon = CreateTarget(new Vector3(0f, 3.24f, 10f), dragonSettings.FramingBoundsExtents, dragonSettings);
            var composeSettings = new CharacterTestLockedThirdPersonCameraCompositionSettings(
                6f,
                2.5f,
                28f,
                60f,
                16f / 9f,
                0.62f,
                1.5f,
                0f,
                18f);

            CharacterTestLockedThirdPersonCameraComposition composition =
                CharacterTestLockedThirdPersonCameraComposer.Compose(human, dragon, composeSettings);

            Vector3 cameraPosition = ResolveCameraPosition(human.BoundsCenter, composition);
            Assert.That(-cameraPosition.z, Is.InRange(2.5f, 22f));
            Assert.That(composition.Distance, Is.InRange(2.5f, 28f));
        }

        [Test]
        public void LockedComposer_LegacyProfileKeepsPreProfileFramingSemantics()
        {
            CharacterTestGameplayCameraSettings legacy = CreateSettings(new Vector3(0.6f, 1.1f, 0.6f), false);
            var player = CreateTarget(Vector3.zero, legacy.FramingBoundsExtents, legacy);
            var target = CreateTarget(new Vector3(0f, 0f, 8f), legacy.FramingBoundsExtents, legacy);
            var withoutPadding = new CharacterTestLockedThirdPersonCameraCompositionSettings(6f, 2.5f, 18f, 60f, 16f / 9f, 0.68f, 0f, 0f, 18f);
            var ignoredNewPadding = new CharacterTestLockedThirdPersonCameraCompositionSettings(6f, 2.5f, 18f, 60f, 16f / 9f, 0.68f, 5f, 0f, 18f);

            CharacterTestLockedThirdPersonCameraComposition expected = CharacterTestLockedThirdPersonCameraComposer.Compose(player, target, withoutPadding);
            CharacterTestLockedThirdPersonCameraComposition actual = CharacterTestLockedThirdPersonCameraComposer.Compose(player, target, ignoredNewPadding);

            Assert.AreEqual(expected.Distance, actual.Distance, 0.0001f);
            Assert.AreEqual(expected.WorldOffset, actual.WorldOffset);
        }

        private static Vector3 ResolveCameraPosition(
            Vector3 targetGroupCenter,
            CharacterTestLockedThirdPersonCameraComposition composition)
        {
            return targetGroupCenter + composition.WorldOffset - ResolveForward(composition) * composition.Distance;
        }

        private static Vector3 ResolveForward(
            CharacterTestLockedThirdPersonCameraComposition composition)
        {
            float yaw = composition.Yaw * Mathf.Deg2Rad;
            float pitch = composition.Pitch * Mathf.Deg2Rad;
            return new Vector3(
                Mathf.Sin(yaw) * Mathf.Cos(pitch),
                -Mathf.Sin(pitch),
                Mathf.Cos(yaw) * Mathf.Cos(pitch)).normalized;
        }

        private static CharacterTestGameplayCameraSettings CreateSettings(
            Vector3 extents,
            bool explicitProfile = true,
            float lockedFramingScale = 1f)
        {
            return new CharacterTestGameplayCameraSettings(
                explicitProfile,
                "camera.test",
                "body_center",
                Vector3.zero,
                Vector3.up,
                extents,
                14f,
                10f,
                20f,
                60f,
                24f,
                0f,
                0.8f,
                14f,
                28f,
                0.62f,
                1.5f,
                0.55f,
                0.3f,
                3f,
                0.8f,
                lockedFramingScale);
        }

        private static CharacterTestCameraTargetPresentation CreateTarget(
            Vector3 center,
            Vector3 extents,
            CharacterTestGameplayCameraSettings settings)
        {
            return new CharacterTestCameraTargetPresentation(
                null,
                center + Vector3.up,
                Vector3.forward,
                settings.ExplicitProfile
                    ? center - Vector3.up * extents.y
                    : center,
                center,
                extents,
                settings);
        }
    }
}

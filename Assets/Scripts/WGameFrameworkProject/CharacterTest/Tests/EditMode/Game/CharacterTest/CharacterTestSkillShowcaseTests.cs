using System.Collections;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestSkillShowcaseTests
    {
        [TearDown]
        public void TearDown()
        {
            if (!Application.isPlaying)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void SceneGeneratorSmoke_BindsUiStageProfileAndBootstrap()
        {
            CreateCharacterTestSkillShowcaseScene.ValidateSmoke();
        }

        [Test]
        public void SkillShowcase_BuildsRecentSkillChoicesAndReplaysThroughPreviewSlot()
        {
            CreateCharacterTestSkillShowcaseScene.ValidatePlayableSmoke();
        }

        [UnityTest]
        public IEnumerator SkillShowcaseScene_PlayModeReplaysRepresentativeAbilities()
        {
            EditorSceneManager.OpenScene(CharacterTestSkillShowcaseBootstrap.ScenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();

            CharacterTestSkillShowcaseBootstrap bootstrap =
                Object.FindAnyObjectByType<CharacterTestSkillShowcaseBootstrap>();
            Assert.IsNotNull(bootstrap);
            bootstrap.InitializeForValidation();
            Assert.Greater(bootstrap.AbilityCount, 0);
            Assert.IsTrue(bootstrap.ContainsAbilityForValidation(CharacterTestAbilityIds.BeamLanceAbilityId));
            Assert.IsTrue(bootstrap.ContainsAbilityForValidation(CharacterTestAbilityIds.ChainLightningAbilityId));
            Assert.IsTrue(bootstrap.ContainsAbilityForValidation(CharacterTestAbilityIds.PoisonBottleAbilityId));
            Assert.IsTrue(bootstrap.ContainsAbilityForValidation(CharacterTestAbilityIds.DashStrikeAbilityId));
            Assert.IsTrue(bootstrap.ContainsAbilityForValidation(CharacterTestAbilityIds.HookChainAbilityId));
            Assert.IsTrue(bootstrap.ContainsAbilityForValidation(CharacterTestAbilityIds.ArcMineAbilityId));
            Assert.IsTrue(bootstrap.ContainsAbilityForValidation(CharacterTestAbilityIds.RuneTurretAbilityId));

            Assert.IsTrue(bootstrap.SelectAbilityForValidation(CharacterTestAbilityIds.BeamLanceAbilityId));
            Assert.IsTrue(bootstrap.ReplayForValidation());
            Assert.IsTrue(bootstrap.LastReplayUsedCharacterTestHotbarInputForValidation);
            Assert.AreEqual(CharacterTestRuntimeCommandSources.PlayerInput, bootstrap.LastCastCommandSourceIdForValidation);
            Assert.AreEqual(CharacterTestAbilityHotbarRuntime.LastSlotNumber, bootstrap.LastCastCommandSlotNumberForValidation);
            Assert.AreEqual(CharacterTestAbilityIds.BeamLanceAbilityId, bootstrap.LastCastCommandAbilityIdForValidation);
            Assert.GreaterOrEqual(bootstrap.PreviewHotbarInputReadCountForValidation, 1);
            for (int i = 0; i < 20; i++)
                yield return null;
            Assert.Greater(bootstrap.SelectedAbilityId, 0);

            Assert.IsTrue(bootstrap.SelectAbilityForValidation(CharacterTestAbilityIds.ChainLightningAbilityId));
            Assert.IsTrue(bootstrap.ReplayForValidation());
            Assert.IsTrue(bootstrap.LastReplayUsedCharacterTestHotbarInputForValidation);
            Assert.AreEqual(CharacterTestRuntimeCommandSources.PlayerInput, bootstrap.LastCastCommandSourceIdForValidation);
            Assert.AreEqual(CharacterTestAbilityHotbarRuntime.LastSlotNumber, bootstrap.LastCastCommandSlotNumberForValidation);
            Assert.AreEqual(CharacterTestAbilityIds.ChainLightningAbilityId, bootstrap.LastCastCommandAbilityIdForValidation);
            for (int i = 0; i < 60; i++)
                yield return null;
            Assert.Greater(bootstrap.SelectedAbilityId, 0);

            Assert.IsTrue(bootstrap.SelectAbilityForValidation(CharacterTestAbilityIds.PoisonBottleAbilityId));
            Assert.IsTrue(bootstrap.ReplayForValidation());
            Assert.IsTrue(bootstrap.LastReplayUsedCharacterTestHotbarInputForValidation);
            Assert.AreEqual(CharacterTestRuntimeCommandSources.PlayerInput, bootstrap.LastCastCommandSourceIdForValidation);
            Assert.AreEqual(CharacterTestAbilityHotbarRuntime.LastSlotNumber, bootstrap.LastCastCommandSlotNumberForValidation);
            Assert.AreEqual(CharacterTestAbilityIds.PoisonBottleAbilityId, bootstrap.LastCastCommandAbilityIdForValidation);
            bool poisonActionStarted = false;
            bool sawPoisonProjectile = false;
            bool sawPoisonProjectilePresenter = false;
            bool sawPoisonPool = false;
            bool sawPoisonStatus = false;
            bool sawPoisonTick = false;
            for (int i = 0; i < 220; i++)
            {
                bootstrap.RunFrameForValidation(1f / 60f);
                poisonActionStarted |= string.Equals(
                    (bootstrap.CasterActiveActionIdForValidation).ToString(),
                    (CharacterTestCharacterActionConfigSource.PoisonBottleActionId).ToString(),
                    System.StringComparison.Ordinal);
                sawPoisonProjectile |= bootstrap.ActiveProjectileCountForValidation > 0;
                sawPoisonProjectilePresenter |= bootstrap.ActiveProjectilePresenterCountForValidation > 0;
                sawPoisonPool |= bootstrap.ActivePoisonBottlePoolCountForValidation > 0;
                sawPoisonStatus |= bootstrap.AnyHostileHasPoisonedStatusForValidation;
                sawPoisonTick |= bootstrap.LastPoisonTickTargetCountForValidation > 0;
                if (poisonActionStarted
                    && sawPoisonProjectile
                    && sawPoisonProjectilePresenter
                    && sawPoisonPool
                    && sawPoisonStatus
                    && sawPoisonTick)
                {
                    break;
                }
            }

            Assert.IsTrue(
                poisonActionStarted,
                "Poison Bottle should start the real CharacterTest action from the showcase hotbar cast.");
            Assert.IsTrue(
                sawPoisonProjectile,
                "Poison Bottle should spawn an authoritative projectile in the showcase.");
            Assert.IsTrue(
                sawPoisonProjectilePresenter,
                "Poison Bottle should create projectile flight presentation in the showcase.");
            Assert.IsTrue(
                sawPoisonPool,
                "Poison Bottle should create its poison pool after hit or expiry in the showcase.");
            Assert.IsTrue(
                sawPoisonStatus,
                "Poison Bottle pool should apply POIS to at least one hostile in the showcase.");
            Assert.IsTrue(
                sawPoisonTick,
                "Poisoned status should tick after Poison Bottle applies POIS in the showcase.");
            Assert.Greater(bootstrap.SelectedAbilityId, 0);

            yield return new ExitPlayMode();
        }
    }
}

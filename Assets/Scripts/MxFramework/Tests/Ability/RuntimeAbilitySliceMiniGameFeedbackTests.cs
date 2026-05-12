using MxFramework.Demo;
using MxFramework.UI.Toolkit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Ability
{
    public sealed class RuntimeAbilitySliceMiniGameFeedbackTests
    {
        private GameObject _runnerObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_runnerObject);
        }

        [Test]
        public void GameplayShowcaseUxml_ContainsMiniGameFeedbackCriticalElements()
        {
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI/MxFramework/Showcase/GameplayShowcase.uxml");
            Assert.IsNotNull(tree);

            VisualElement root = tree.CloneTree();

            Assert.IsNotNull(root.Q<VisualElement>("mini-game-feedback"));
            Assert.IsNotNull(root.Q<Label>("feedback-player-status"));
            Assert.IsNotNull(root.Q<Label>("feedback-enemy-status"));
            Assert.IsNotNull(root.Q<Label>("feedback-player-buff"));
            Assert.IsNotNull(root.Q<Label>("feedback-enemy-buff"));
            Assert.IsNotNull(root.Q<Label>("feedback-skill-buttons"));
            Assert.IsNotNull(root.Q<Label>("feedback-recent-action"));
            Assert.IsNotNull(root.Q<VisualElement>("config-controls"));
            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-view"));
        }

        [Test]
        public void FillMiniGameFeedback_InitialState_ShowsStableBadgesAndReadyButtons()
        {
            RuntimeAbilitySliceRunner runner = CreateRunner();
            var view = new MxRuntimeMiniGameFeedbackViewModel();

            RuntimeAbilitySliceShowcaseUi.FillMiniGameFeedback(
                view,
                runner.Player,
                runner.PlayerMaxHp,
                runner.Enemy,
                runner.EnemyMaxHp,
                runner.EventLog,
                configDriven: false);

            Assert.AreEqual("Player: Stable", view.PlayerStatusText);
            Assert.AreEqual("positive", view.PlayerStatusTone);
            Assert.AreEqual("Enemy: Stable", view.EnemyStatusText);
            Assert.AreEqual("positive", view.EnemyStatusTone);
            Assert.AreEqual("Player Buff: none", view.PlayerBuffText);
            Assert.AreEqual("Enemy Buff: none", view.EnemyBuffText);
            Assert.IsTrue(view.StrikeButtonHot);
            Assert.IsTrue(view.IgniteButtonHot);
            Assert.IsTrue(view.BuffButtonHot);
            Assert.That(view.SkillFeedbackText, Does.Contain("Strike ready"));
            Assert.That(view.RecentActionText, Does.Not.Contain("waiting"));
        }

        [Test]
        public void FillMiniGameFeedback_BurningEnemy_ShowsBuffCountdownAndRefreshFeedback()
        {
            RuntimeAbilitySliceRunner runner = CreateRunner();
            runner.ApplyBurningBuffToEnemy(2);
            runner.TickBuffs(1f);
            var view = new MxRuntimeMiniGameFeedbackViewModel();

            RuntimeAbilitySliceShowcaseUi.FillMiniGameFeedback(
                view,
                runner.Player,
                runner.PlayerMaxHp,
                runner.Enemy,
                runner.EnemyMaxHp,
                runner.EventLog,
                configDriven: true);

            Assert.AreEqual("Enemy: Burning", view.EnemyStatusText);
            Assert.AreEqual("warning", view.EnemyStatusTone);
            Assert.That(view.EnemyBuffText, Does.Contain("#100001 2L"));
            Assert.That(view.EnemyBuffText, Does.Contain("4.0s"));
            Assert.That(view.IgniteButtonFeedbackText, Does.Contain("refresh Burning"));
            Assert.That(view.SkillFeedbackText, Does.Contain("Config mode"));
        }

        [Test]
        public void FillMiniGameFeedback_DownEnemy_MutesTargetedButtons()
        {
            RuntimeAbilitySliceRunner runner = CreateRunner();
            for (int i = 0; i < 6; i++)
                runner.CastStrike();

            var view = new MxRuntimeMiniGameFeedbackViewModel();
            RuntimeAbilitySliceShowcaseUi.FillMiniGameFeedback(
                view,
                runner.Player,
                runner.PlayerMaxHp,
                runner.Enemy,
                runner.EnemyMaxHp,
                runner.EventLog,
                configDriven: false);

            Assert.AreEqual("Enemy: Down", view.EnemyStatusText);
            Assert.AreEqual("danger", view.EnemyStatusTone);
            Assert.IsFalse(view.StrikeButtonHot);
            Assert.IsFalse(view.IgniteButtonHot);
            Assert.IsFalse(view.BuffButtonHot);
            Assert.That(view.StrikeButtonFeedbackText, Does.Contain("no living enemy target"));
        }

        private RuntimeAbilitySliceRunner CreateRunner()
        {
            _runnerObject = new GameObject("RuntimeAbilitySliceMiniGameFeedbackTests");
            RuntimeAbilitySliceRunner runner = _runnerObject.AddComponent<RuntimeAbilitySliceRunner>();
            runner.ResetDemo();
            return runner;
        }
    }
}

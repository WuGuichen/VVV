using System.Collections.Generic;
using MxFramework.UI.Toolkit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Ability
{
    public sealed class RuntimeAbilitySliceUiToolkitComponentTests
    {
        [Test]
        public void Components_CanBeConstructedWithThemeTokenClasses()
        {
            var badge = new MxStatusBadge("Ready", MxUiTone.Positive);
            Assert.IsTrue(badge.ClassListContains(MxUiThemeTokens.StatusBadge));
            Assert.IsTrue(badge.ClassListContains(MxUiThemeTokens.StatusPositive));

            var button = new MxCommandButton(null, "Cast");
            button.SetState(enabled: true, hot: false, tooltipText: "blocked");
            Assert.IsTrue(button.ClassListContains(MxUiThemeTokens.CommandButton));
            Assert.IsTrue(button.ClassListContains(MxUiThemeTokens.CommandEnabled));
            Assert.IsTrue(button.ClassListContains(MxUiThemeTokens.CommandMuted));
            Assert.AreEqual("blocked", button.tooltip);

            var statBar = new MxStatBar();
            statBar.SetValue(25f, 100f, MxUiTone.Warning);
            Assert.AreEqual(0.25f, statBar.NormalizedValue);

            var eventLog = new MxEventLog();
            eventLog.SetItems(new List<string> { "first", "second" }, "empty");
            Assert.IsTrue(eventLog.ClassListContains(MxUiThemeTokens.EventLog));
            Assert.AreEqual(2, eventLog.childCount);

            var tabs = new MxPanelTabs();
            tabs.SetTabs(new[] { "Summary", "Technical" }, 1);
            Assert.IsTrue(tabs.ClassListContains(MxUiThemeTokens.PanelTabs));
            Assert.AreEqual(1, tabs.ActiveIndex);
        }

        [Test]
        public void GameplayShowcaseUxml_KeepsCriticalNamesAndAddsComponentClasses()
        {
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI/MxFramework/Showcase/GameplayShowcase.uxml");
            Assert.IsNotNull(tree);

            VisualElement root = tree.CloneTree();

            VisualElement miniGame = root.Q<VisualElement>("mini-game-feedback");
            VisualElement manual = root.Q<VisualElement>("manual-controls");
            VisualElement config = root.Q<VisualElement>("config-controls");
            VisualElement diagnostic = root.Q<VisualElement>("diagnostic-view");

            Assert.IsNotNull(miniGame);
            Assert.IsNotNull(manual);
            Assert.IsNotNull(config);
            Assert.IsNotNull(diagnostic);
            Assert.IsTrue(miniGame.ClassListContains(MxUiThemeTokens.Panel));
            Assert.IsTrue(manual.ClassListContains(MxUiThemeTokens.Panel));
            Assert.IsTrue(config.ClassListContains(MxUiThemeTokens.Panel));
            Assert.IsTrue(diagnostic.ClassListContains(MxUiThemeTokens.Panel));

            Assert.IsTrue(root.Q<Label>("feedback-player-status").ClassListContains(MxUiThemeTokens.StatusBadge));
            Assert.IsTrue(root.Q<Button>("manual-strike-button").ClassListContains(MxUiThemeTokens.CommandButton));
            Assert.IsTrue(root.Q<Button>("config-load-patch-button").ClassListContains(MxUiThemeTokens.CommandButton));
            Assert.IsTrue(root.Q<VisualElement>("player-hp-fill").ClassListContains(MxUiThemeTokens.StatBarFill));
            Assert.IsTrue(root.Q<VisualElement>("event-list").ClassListContains(MxUiThemeTokens.EventLog));
            Assert.IsTrue(root.Q<Button>("diagnostic-summary-button").ClassListContains(MxUiThemeTokens.PanelTab));
        }
    }
}

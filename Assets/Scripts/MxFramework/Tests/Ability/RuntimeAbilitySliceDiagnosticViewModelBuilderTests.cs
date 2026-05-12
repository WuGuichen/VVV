using MxFramework.Config.Runtime;
using MxFramework.Demo;
using MxFramework.Gameplay;
using MxFramework.UI.Toolkit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Ability
{
    public sealed class RuntimeAbilitySliceDiagnosticViewModelBuilderTests
    {
        [Test]
        public void Build_EmptyInputs_ReturnsStableEmptyStates()
        {
            MxRuntimeDiagnosticViewModel view = RuntimeAbilitySliceDiagnosticViewModelBuilder.Build(null, null);

            Assert.That(view.HeaderText, Does.Contain("waiting"));
            Assert.AreEqual("Last cast: waiting", view.LastCastText);
            Assert.AreEqual("No runtime errors", view.ErrorSummaryText);
            Assert.AreEqual(0, view.EntitySummaryLines.Count);
            Assert.AreEqual(0, view.AbilityEventTechnicalLines.Count);
            Assert.AreEqual(0, view.AttributeEventTechnicalLines.Count);
            Assert.AreEqual("No ability events", view.AbilityEventsEmptyText);
            Assert.AreEqual("No attribute changed events", view.AttributeEventsEmptyText);
        }

        [Test]
        public void Build_MapsSnapshotEventsAndConfigSummaryIntoSeparateSections()
        {
            var snapshot = new GameplayDiagnosticSnapshot(
                "RuntimeAbilitySlice",
                "BasicAbilityConfig -> ConfigAbilityFactory",
                new[]
                {
                    new GameplayEntitySnapshot(
                        1,
                        1,
                        true,
                        new[]
                        {
                            new GameplayAttributeSnapshot(1, 1000),
                            new GameplayAttributeSnapshot(2, 120),
                            new GameplayAttributeSnapshot(3, 20)
                        },
                        new[]
                        {
                            new GameplayBuffSnapshot(100001, 5f, 4f, 1, 3, false, false)
                        },
                        new[]
                        {
                            new GameplayModifierSnapshot(200001, 2)
                        })
                },
                new GameplayAbilityCastSnapshot(
                    "BasicAbilityConfig -> ConfigAbilityFactory",
                    false,
                    "NoValidTargets",
                    new int[0]),
                new[]
                {
                    new GameplayAbilityEventSnapshot("CastStarted", 300001, 1, null, null),
                    new GameplayAbilityEventSnapshot("CastFailed", 300001, 1, null, "NoValidTargets")
                },
                new[]
                {
                    new GameplayAttributeEventSnapshot(1, 565, 600, 565, -35, "AbilityBurningBuff")
                });

            var config = new RuntimeConfigChangeSummary(
                "Showcase Patch",
                "RuntimeAbilitySliceDemoData",
                "RebuildOnResolve");
            config.AddChangedAbility(300001);
            config.AddChangedBuff(100001);
            config.AddChangedModifier(200001);
            config.AddRebuiltAbility(300001);
            config.AddFailedAbility(300002, "Ability rebuild failed");

            MxRuntimeDiagnosticViewModel view = RuntimeAbilitySliceDiagnosticViewModelBuilder.Build(snapshot, config);

            Assert.That(view.HeaderText, Does.Contain("Ability Events 2"));
            Assert.That(view.LastCastText, Does.Contain("NoValidTargets"));
            Assert.That(view.ConfigSourceText, Does.Contain("Showcase Patch"));
            Assert.That(view.EntitySummaryLines[0], Does.Contain("HP=1000"));
            Assert.That(view.EntityTechnicalLines[0], Does.Contain("Buffs: id=100001"));
            Assert.AreEqual(3, view.AbilityEventSummaryLines.Count);
            Assert.AreEqual(2, view.AbilityEventTechnicalLines.Count);
            Assert.That(view.AbilityEventTechnicalLines[1], Does.Contain("CastFailed"));
            Assert.AreEqual(2, view.AttributeEventSummaryLines.Count);
            Assert.AreEqual(1, view.AttributeEventTechnicalLines.Count);
            Assert.That(view.AttributeEventTechnicalLines[0], Does.Contain("source=AbilityBurningBuff"));
            Assert.That(view.ConfigTechnicalLines[3], Does.Contain("300001"));
            Assert.That(view.ErrorTechnicalLines[0], Does.Contain("Last cast failure"));
            Assert.That(view.ErrorTechnicalLines[1], Does.Contain("Config error"));
        }

        [Test]
        public void GameplayShowcaseUxml_ContainsDiagnosticViewCriticalElements()
        {
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI/MxFramework/Showcase/GameplayShowcase.uxml");
            Assert.IsNotNull(tree);

            VisualElement root = tree.CloneTree();

            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-view"));
            Assert.IsNotNull(root.Q<Button>("diagnostic-summary-button"));
            Assert.IsNotNull(root.Q<Button>("diagnostic-technical-button"));
            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-summary-view"));
            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-technical-view"));
            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-ability-events-list"));
            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-attribute-events-list"));
            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-config-source-list"));
            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-errors-list"));
        }
    }
}

using MxFramework.Demo;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Ability
{
    public sealed class RuntimeAbilitySliceConfigPanelTests
    {
        private GameObject _runnerObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_runnerObject);
        }

        [Test]
        public void LoadPatch_RebuildAndCompare_ShowsOldAbilityIsNotHotSwapped()
        {
            RuntimeAbilitySliceRunner runner = CreateRunner();

            runner.LoadPatchConfig();
            Assert.IsTrue(runner.UseConfigDriven);
            Assert.That(runner.ConfigModeStatus, Does.Contain("Config Driven"));
            Assert.That(runner.RuntimeConfigChangeSummaryText, Does.Contain("source=Showcase Patch"));
            Assert.That(runner.RuntimeConfigChangeSummaryText, Does.Contain("changed abilities=1"));
            Assert.That(runner.RuntimeConfigChangeSummaryText, Does.Contain("failed=0"));

            Assert.IsTrue(runner.RebuildConfiguredAbilities());
            Assert.That(runner.RuntimeConfigChangeSummaryText, Does.Contain("rebuilt=2"));

            Assert.IsTrue(runner.CompareOldAndNewConfig());
            Assert.That(runner.ConfigComparisonSummary, Does.Contain("Old object"));
            Assert.That(runner.ConfigComparisonSummary, Does.Contain("damage=110"));
            Assert.That(runner.ConfigComparisonSummary, Does.Contain("damage=10"));
            Assert.That(runner.ConfigComparisonSummary, Does.Contain("old object unchanged"));
        }

        [Test]
        public void LoadModPackage_RebuildAndCompare_UsesModLayerAbilityConfig()
        {
            RuntimeAbilitySliceRunner runner = CreateRunner();

            runner.LoadModPackageConfig();
            Assert.IsTrue(runner.UseConfigDriven);
            Assert.That(runner.RuntimeConfigChangeSummaryText, Does.Contain("source=Showcase Mod Package"));
            Assert.That(runner.RuntimeConfigChangeSummaryText, Does.Contain("changed abilities=1"));

            Assert.IsTrue(runner.RebuildConfiguredAbilities());
            Assert.IsTrue(runner.CompareOldAndNewConfig());
            Assert.That(runner.ConfigComparisonSummary, Does.Contain("damage=110"));
            Assert.That(runner.ConfigComparisonSummary, Does.Contain("damage=220"));
        }

        private RuntimeAbilitySliceRunner CreateRunner()
        {
            _runnerObject = new GameObject("RuntimeAbilitySliceConfigPanelTests");
            RuntimeAbilitySliceRunner runner = _runnerObject.AddComponent<RuntimeAbilitySliceRunner>();
            runner.ResetDemo();
            return runner;
        }
    }
}

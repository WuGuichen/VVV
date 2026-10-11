using MxFramework.Config;
using MxFramework.Config.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class RuntimeConfigPatchMergerTests
    {
        [Test]
        public void Merge_WhenPatchOverridesModifier_ReturnsMergedRuntimeTable()
        {
            BasicModifierConfig baseModifier = CreateModifier(200001, 10);
            BasicModifierConfig patchedModifier = CreateModifier(200001, 20);

            ConfigPatchMergeResult<BasicModifierConfig> result = RuntimeConfigPatchMerger.Merge(
                BasicModifierConfig.CreateSchema(),
                new[] { baseModifier },
                new[] { ConfigPatchEntry<BasicModifierConfig>.Upsert(patchedModifier, ConfigLayerKind.Patch, "hotfix") });

            Assert.IsTrue(result.Table.TryGetConfig(200001, out BasicModifierConfig merged));
            Assert.AreEqual(20, merged.Parameters[0]);
            Assert.AreEqual(1, result.ChangeSet.Count);
            Assert.AreEqual(ConfigMergeChangeKind.Replaced, result.ChangeSet.Changes[0].ChangeKind);
            Assert.AreEqual(ConfigLayerKind.Patch, result.ChangeSet.Changes[0].Layer);
        }

        [Test]
        public void Merge_WhenModAddsModifier_ReportsAddedChange()
        {
            ConfigPatchMergeResult<BasicModifierConfig> modifiers = RuntimeConfigPatchMerger.Merge(
                BasicModifierConfig.CreateSchema(),
                new[] { CreateModifier(200001, 10) },
                new[] { ConfigPatchEntry<BasicModifierConfig>.Upsert(CreateModifier(200002, 30), ConfigLayerKind.Mod, "demo-mod") });

            Assert.IsTrue(modifiers.Table.ContainsConfig<BasicModifierConfig>(200002));
            Assert.AreEqual(ConfigMergeChangeKind.Added, modifiers.ChangeSet.Changes[0].ChangeKind);
            Assert.AreEqual(ConfigLayerKind.Mod, modifiers.ChangeSet.Changes[0].Layer);
        }

        [Test]
        public void Merge_WhenPatchRemovesModifier_RemovesRuntimeRowAndReportsChange()
        {
            ConfigPatchMergeResult<BasicModifierConfig> result = RuntimeConfigPatchMerger.Merge(
                BasicModifierConfig.CreateSchema(),
                new[]
                {
                    CreateModifier(200001, 10),
                    CreateModifier(200002, 20)
                },
                new[] { ConfigPatchEntry<BasicModifierConfig>.Remove(200002, ConfigLayerKind.Patch, "hotfix") });

            Assert.IsTrue(result.Table.ContainsConfig<BasicModifierConfig>(200001));
            Assert.IsFalse(result.Table.ContainsConfig<BasicModifierConfig>(200002));
            Assert.AreEqual(ConfigPatchOperation.Remove, result.ChangeSet.Changes[0].Operation);
            Assert.AreEqual(ConfigMergeChangeKind.Removed, result.ChangeSet.Changes[0].ChangeKind);
        }

        [Test]
        public void ChangeSet_ReportIncludesLayerAndSource()
        {
            ConfigPatchMergeResult<BasicModifierConfig> result = RuntimeConfigPatchMerger.Merge(
                BasicModifierConfig.CreateSchema(),
                new BasicModifierConfig[0],
                new[] { ConfigPatchEntry<BasicModifierConfig>.Upsert(CreateModifier(200001, 10), ConfigLayerKind.Debug, "runtime-debug") });

            string report = result.ChangeSet.ToReportText();

            StringAssert.Contains("type=BasicModifierConfig", report);
            StringAssert.Contains("layer=Debug", report);
            StringAssert.Contains("source=runtime-debug", report);
        }

        private static BasicModifierConfig CreateModifier(int id, int value)
        {
            return new BasicModifierConfig(
                id,
                new LocalizedTextKey("mod." + id + ".name"),
                new LocalizedTextKey("mod." + id + ".desc"),
                paramIndex: 1,
                parameters: new[] { value });
        }
    }
}

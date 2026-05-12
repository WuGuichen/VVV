using System.Collections.Generic;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class RuntimeConfigPatchMergerTests
    {
        [Test]
        public void Merge_WhenPatchOverridesBuff_ReturnsMergedRuntimeTable()
        {
            BasicBuffConfig baseBuff = CreateBuff(100001, duration: 5f, maxLayers: 1, modifierId: 200001);
            BasicBuffConfig patchedBuff = CreateBuff(100001, duration: 8f, maxLayers: 3, modifierId: 200001);

            ConfigPatchMergeResult<BasicBuffConfig> result = RuntimeConfigPatchMerger.Merge(
                BasicBuffConfig.CreateSchema(),
                new[] { baseBuff },
                new[] { ConfigPatchEntry<BasicBuffConfig>.Upsert(patchedBuff, ConfigLayerKind.Patch, "hotfix") });

            Assert.IsTrue(result.Table.TryGetConfig(100001, out BasicBuffConfig merged));
            Assert.AreEqual(8f, merged.Duration);
            Assert.AreEqual(3, merged.MaxLayers);
            Assert.AreEqual(1, result.ChangeSet.Count);
            Assert.AreEqual(ConfigMergeChangeKind.Replaced, result.ChangeSet.Changes[0].ChangeKind);
            Assert.AreEqual(ConfigLayerKind.Patch, result.ChangeSet.Changes[0].Layer);
        }

        [Test]
        public void Merge_WhenModAddsBuffAndModifier_ValidatesReferences()
        {
            ConfigPatchMergeResult<BasicModifierConfig> modifiers = RuntimeConfigPatchMerger.Merge(
                BasicModifierConfig.CreateSchema(),
                new[] { CreateModifier(200001) },
                new[] { ConfigPatchEntry<BasicModifierConfig>.Upsert(CreateModifier(200002), ConfigLayerKind.Mod, "demo-mod") });

            ConfigPatchMergeResult<BasicBuffConfig> buffs = RuntimeConfigPatchMerger.Merge(
                BasicBuffConfig.CreateSchema(),
                new[] { CreateBuff(100001, duration: 5f, maxLayers: 1, modifierId: 200001) },
                new[] { ConfigPatchEntry<BasicBuffConfig>.Upsert(CreateBuff(100002, duration: 12f, maxLayers: 2, modifierId: 200002), ConfigLayerKind.Mod, "demo-mod") });

            var registry = new ConfigRegistry();
            registry.RegisterProvider<BasicBuffConfig>(buffs.Table);
            registry.RegisterProvider<BasicModifierConfig>(modifiers.Table);

            ConfigTableValidationReport report = buffs.Table.Validate(registry, CreateLocalization());

            Assert.IsFalse(report.HasErrors);
            Assert.IsTrue(buffs.Table.ContainsConfig<BasicBuffConfig>(100002));
            Assert.IsTrue(modifiers.Table.ContainsConfig<BasicModifierConfig>(200002));
            Assert.AreEqual(ConfigMergeChangeKind.Added, buffs.ChangeSet.Changes[0].ChangeKind);
            Assert.AreEqual(ConfigLayerKind.Mod, buffs.ChangeSet.Changes[0].Layer);
        }

        [Test]
        public void Merge_WhenPatchRemovesBuff_RemovesRuntimeRowAndReportsChange()
        {
            ConfigPatchMergeResult<BasicBuffConfig> result = RuntimeConfigPatchMerger.Merge(
                BasicBuffConfig.CreateSchema(),
                new[]
                {
                    CreateBuff(100001, duration: 5f, maxLayers: 1, modifierId: 200001),
                    CreateBuff(100002, duration: 6f, maxLayers: 1, modifierId: 200001)
                },
                new[] { ConfigPatchEntry<BasicBuffConfig>.Remove(100002, ConfigLayerKind.Patch, "hotfix") });

            Assert.IsTrue(result.Table.ContainsConfig<BasicBuffConfig>(100001));
            Assert.IsFalse(result.Table.ContainsConfig<BasicBuffConfig>(100002));
            Assert.AreEqual(ConfigPatchOperation.Remove, result.ChangeSet.Changes[0].Operation);
            Assert.AreEqual(ConfigMergeChangeKind.Removed, result.ChangeSet.Changes[0].ChangeKind);
        }

        [Test]
        public void ChangeSet_ReportIncludesLayerAndSource()
        {
            ConfigPatchMergeResult<BasicBuffConfig> result = RuntimeConfigPatchMerger.Merge(
                BasicBuffConfig.CreateSchema(),
                new BasicBuffConfig[0],
                new[] { ConfigPatchEntry<BasicBuffConfig>.Upsert(CreateBuff(100001, duration: 5f, maxLayers: 1, modifierId: 0), ConfigLayerKind.Debug, "runtime-debug") });

            string report = result.ChangeSet.ToReportText();

            StringAssert.Contains("type=BasicBuffConfig", report);
            StringAssert.Contains("layer=Debug", report);
            StringAssert.Contains("source=runtime-debug", report);
        }

        private static BasicBuffConfig CreateBuff(int id, float duration, int maxLayers, int modifierId)
        {
            return new BasicBuffConfig(
                id,
                new LocalizedTextKey("buff." + id + ".name"),
                new LocalizedTextKey("buff." + id + ".desc"),
                duration,
                maxLayers,
                modifierId: modifierId);
        }

        private static BasicModifierConfig CreateModifier(int id)
        {
            return new BasicModifierConfig(
                id,
                new LocalizedTextKey("mod." + id + ".name"),
                new LocalizedTextKey("mod." + id + ".desc"),
                paramIndex: 1,
                parameters: new[] { id });
        }

        private static MemoryLocalizationProvider CreateLocalization()
        {
            var localization = new MemoryLocalizationProvider();
            RegisterBuffLocalization(localization, 100001);
            RegisterBuffLocalization(localization, 100002);
            RegisterModifierLocalization(localization, 200001);
            RegisterModifierLocalization(localization, 200002);
            return localization;
        }

        private static void RegisterBuffLocalization(MemoryLocalizationProvider localization, int id)
        {
            localization.Register(new LocalizedTextKey("buff." + id + ".name"), LocaleId.ZhCN, "Buff " + id);
            localization.Register(new LocalizedTextKey("buff." + id + ".name"), LocaleId.EnUS, "Buff " + id);
            localization.Register(new LocalizedTextKey("buff." + id + ".desc"), LocaleId.ZhCN, "Buff Desc " + id);
            localization.Register(new LocalizedTextKey("buff." + id + ".desc"), LocaleId.EnUS, "Buff Desc " + id);
        }

        private static void RegisterModifierLocalization(MemoryLocalizationProvider localization, int id)
        {
            localization.Register(new LocalizedTextKey("mod." + id + ".name"), LocaleId.ZhCN, "Mod " + id);
            localization.Register(new LocalizedTextKey("mod." + id + ".name"), LocaleId.EnUS, "Mod " + id);
            localization.Register(new LocalizedTextKey("mod." + id + ".desc"), LocaleId.ZhCN, "Mod Desc " + id);
            localization.Register(new LocalizedTextKey("mod." + id + ".desc"), LocaleId.EnUS, "Mod Desc " + id);
        }
    }
}

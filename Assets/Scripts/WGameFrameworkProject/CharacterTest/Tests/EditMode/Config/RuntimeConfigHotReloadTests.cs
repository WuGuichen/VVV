using System;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.DebugUI.Adapters;
using MxFramework.Diagnostics;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public sealed class RuntimeConfigHotReloadTests
    {
        [Test]
        public void Reload_ParsesPatchIntoNewProviderWithoutMutatingBase()
        {
            IConfigProvider baseProvider = CreateBaseProvider();
            string json = CreatePatchJson("hotreload-test", modifierParam: 9);
            var service = new RuntimeConfigPatchHotReloadService(baseProvider, _ => json);

            RuntimeConfigHotReloadResult result = service.Reload(new RuntimeConfigHotReloadRequest("memory://patch", "Memory Patch"));

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Provider);
            Assert.That(result.ContentHash, Is.Not.Empty);
            Assert.That(result.ChangedTables, Does.Contain(nameof(BasicModifierConfig)));
            Assert.AreEqual(1, baseProvider.GetConfig<BasicModifierConfig>(200001).Parameters[0]);
            Assert.AreEqual(9, result.Provider.GetConfig<BasicModifierConfig>(200001).Parameters[0]);
        }

        [Test]
        public void Reload_InvalidJsonReturnsFailureAndNoProvider()
        {
            var service = new RuntimeConfigPatchHotReloadService(CreateBaseProvider(), _ => "{ invalid");

            RuntimeConfigHotReloadResult result = service.Reload(new RuntimeConfigHotReloadRequest("memory://broken"));

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Provider);
            Assert.That(result.ErrorSummary, Does.Contain("Invalid JSON"));
        }

        [Test]
        public void Reload_BuffPatchReturnsFailure()
        {
            string json = "{"
                + "\"format\":\"mx.runtimeConfigPatch.v1\","
                + "\"sourceId\":\"retired-buff\","
                + "\"buffs\":[{\"id\":100001,\"operation\":\"Upsert\"}]"
                + "}";
            var service = new RuntimeConfigPatchHotReloadService(CreateBaseProvider(), _ => json);

            RuntimeConfigHotReloadResult result = service.Reload(new RuntimeConfigHotReloadRequest("memory://buff-patch"));

            Assert.IsFalse(result.Success);
            StringAssert.Contains("buff patches are no longer supported", result.ErrorSummary);
        }

        [Test]
        public void DebugSource_ExportsLastReloadResult()
        {
            var result = new RuntimeConfigPatchHotReloadService(CreateBaseProvider(), _ => CreatePatchJson("source-a", 5))
                .Reload(new RuntimeConfigHotReloadRequest("memory://patch", "Patch A"));

            FrameworkDebugSnapshot snapshot = new RuntimeConfigHotReloadDebugSource(() => result).CreateSnapshot();

            Assert.AreEqual("ConfigHotReload", snapshot.SourceName);
            Assert.That(snapshot.Sections[0].Body, Does.Contain("success: true"));
            Assert.That(snapshot.Sections[1].Body, Does.Contain(nameof(BasicModifierConfig)));
        }

        private static IConfigProvider CreateBaseProvider()
        {
            var provider = new ConfigRegistry();
            var modifiers = new ConfigTable<BasicModifierConfig>(BasicModifierConfig.CreateSchema(), ConfigDuplicatePolicy.Replace);
            modifiers.Add(new BasicModifierConfig(
                200001,
                new LocalizedTextKey("mod.base"),
                new LocalizedTextKey("mod.desc"),
                parameters: new[] { 1 }));
            provider.RegisterProvider<BasicModifierConfig>(modifiers);
            return provider;
        }

        private static string CreatePatchJson(string sourceId, int modifierParam)
        {
            return "{"
                + "\"format\":\"mx.runtimeConfigPatch.v1\","
                + "\"sourceId\":\"" + sourceId + "\","
                + "\"layer\":\"Patch\","
                + "\"modifiers\":[{\"id\":200001,\"operation\":\"Upsert\",\"nameText\":\"mod.patch\",\"descriptionText\":\"mod.patch.desc\",\"parameters\":[" + modifierParam + "]}]"
                + "}";
        }
    }
}

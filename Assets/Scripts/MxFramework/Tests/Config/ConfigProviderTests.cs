using System.Collections.Generic;
using MxFramework.Config;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class ConfigProviderTests
    {
        [Test]
        public void RegisterAndGetConfig_ReturnsValue()
        {
            var provider = new MemoryConfigProvider();
            provider.Register(new TestConfig(1, "one"));

            TestConfig config = provider.GetConfig<TestConfig>(1);

            Assert.AreEqual("one", config.Name);
            Assert.IsTrue(provider.TryGetConfig(1, out TestConfig found));
            Assert.AreSame(config, found);
        }

        [Test]
        public void MissingConfig_ReturnsFailureResultAndThrowsOnGet()
        {
            var provider = new MemoryConfigProvider();

            ConfigResult<TestConfig> result = provider.GetConfigResult<TestConfig>(1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ConfigError.TypeNotRegistered, result.Error);
            Assert.Throws<ConfigNotFoundException>(() => provider.GetConfig<TestConfig>(1));
        }

        [Test]
        public void DuplicatePolicyThrow_Throws()
        {
            var provider = new MemoryConfigProvider(ConfigDuplicatePolicy.Throw);
            provider.Register(new TestConfig(1, "one"));

            Assert.Throws<DuplicateConfigException>(() => provider.Register(new TestConfig(1, "duplicate")));
        }

        [Test]
        public void DuplicatePolicyReplace_ReplacesExisting()
        {
            var provider = new MemoryConfigProvider(ConfigDuplicatePolicy.Replace);
            provider.Register(new TestConfig(1, "one"));

            provider.Register(new TestConfig(1, "two"));

            Assert.AreEqual("two", provider.GetConfig<TestConfig>(1).Name);
        }

        [Test]
        public void DuplicatePolicyIgnore_KeepsExisting()
        {
            var provider = new MemoryConfigProvider(ConfigDuplicatePolicy.Ignore);
            provider.Register(new TestConfig(1, "one"));

            provider.Register(new TestConfig(1, "two"));

            Assert.AreEqual("one", provider.GetConfig<TestConfig>(1).Name);
        }

        [Test]
        public void Register_WhenIdInvalid_Throws()
        {
            var provider = new MemoryConfigProvider();

            Assert.Throws<System.ArgumentOutOfRangeException>(() => provider.Register(new TestConfig(0, "invalid")));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => provider.Register(new TestConfig(-1, "none")));
        }

        [Test]
        public void GetAllConfigs_ReturnsRegisteredTypeOnly()
        {
            var provider = new MemoryConfigProvider();
            provider.Register(new TestConfig(1, "one"));
            provider.Register(new OtherConfig(1));

            IReadOnlyCollection<TestConfig> all = provider.GetAllConfigs<TestConfig>();

            Assert.AreEqual(1, all.Count);
        }

        [Test]
        public void ConfigRegistry_DispatchesByConfigType()
        {
            var configs = new MemoryConfigProvider();
            configs.Register(new TestConfig(1, "one"));
            var other = new MemoryConfigProvider();
            other.Register(new OtherConfig(2));
            var registry = new ConfigRegistry();
            registry.RegisterProvider<TestConfig>(configs);
            registry.RegisterProvider<OtherConfig>(other);

            Assert.AreEqual("one", registry.GetConfig<TestConfig>(1).Name);
            Assert.AreEqual(2, registry.GetConfig<OtherConfig>(2).Id);
            Assert.IsFalse(registry.ContainsConfig<TestConfig>(2));
        }

        [Test]
        public void ConfigRegistry_ReloadsEachProviderOnce()
        {
            var provider = new ReloadProvider();
            var registry = new ConfigRegistry();
            registry.RegisterProvider<TestConfig>(provider);
            registry.RegisterProvider<OtherConfig>(provider);

            registry.Reload();

            Assert.AreEqual(1, provider.ReloadCalls);
        }

        [Test]
        public void ValidateReferences_ReportsMissingReference()
        {
            var provider = new MemoryConfigProvider();
            provider.Register(new ReferencingConfig(1, targetId: 99));

            ConfigValidationReport report = ConfigValidator.ValidateReferences<ReferencingConfig>(provider);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(1, report.Issues.Count);
            Assert.AreEqual(ConfigError.NotFound, report.Issues[0].Error);
        }

        [Test]
        public void ValidateReferences_PassesWhenTargetExists()
        {
            var provider = new MemoryConfigProvider();
            provider.Register(new ReferencingConfig(1, targetId: 2));
            provider.Register(new TestConfig(2, "target"));

            ConfigValidationReport report = ConfigValidator.ValidateReferences<ReferencingConfig>(provider);

            Assert.IsFalse(report.HasErrors);
            Assert.AreEqual(0, report.Issues.Count);
        }

        private sealed class TestConfig : IConfigData
        {
            public TestConfig(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public int Id { get; }
            public string Name { get; }
        }

        private sealed class OtherConfig : IConfigData
        {
            public OtherConfig(int id)
            {
                Id = id;
            }

            public int Id { get; }
        }

        private sealed class ReferencingConfig : IConfigData, IConfigReferenceProvider
        {
            private readonly int _targetId;

            public ReferencingConfig(int id, int targetId)
            {
                Id = id;
                _targetId = targetId;
            }

            public int Id { get; }

            public void CollectReferences(ICollection<ConfigReference> references)
            {
                references.Add(new ConfigReference(typeof(ReferencingConfig), Id, typeof(TestConfig), _targetId, "Target"));
            }
        }

        private sealed class ReloadProvider : IConfigProvider
        {
            public int ReloadCalls { get; private set; }

            public bool TryGetConfig<T>(int id, out T config) where T : IConfigData
            {
                config = default;
                return false;
            }

            public ConfigResult<T> GetConfigResult<T>(int id) where T : IConfigData
            {
                return ConfigResult<T>.Failed(ConfigError.NotFound, string.Empty);
            }

            public T GetConfig<T>(int id) where T : IConfigData
            {
                throw new ConfigNotFoundException(typeof(T), id);
            }

            public IReadOnlyCollection<T> GetAllConfigs<T>() where T : IConfigData
            {
                return new T[0];
            }

            public bool ContainsConfig<T>(int id) where T : IConfigData
            {
                return false;
            }

            public void Reload()
            {
                ReloadCalls++;
            }
        }
    }
}

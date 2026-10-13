using System;
using System.Collections.Generic;
using System.Reflection;
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

    public class ConfigReferenceValidationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedReferences_UsePointQueriesWithoutTargetSnapshots(bool tableValidation)
        {
            var table = CreateTable();
            var memory = new MemoryConfigProvider();
            for (int i = 1; i <= 1000; i++)
            {
                table.Add(new ReferenceOwner(i, 1000));
                memory.Register(new Target(i));
            }
            var targetProvider = new ObservingProvider(memory) { RejectTargetSnapshots = true };
            var registry = CreateRegistry(table, targetProvider);

            Assert.IsFalse(Validate(tableValidation, table, registry));

            Assert.AreEqual(0, targetProvider.TargetSnapshots);
            Assert.AreEqual(1000, targetProvider.PointQueries);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Revalidation_UsesCurrentProviderAndCurrentRows(bool tableValidation)
        {
            var table = CreateTable();
            table.Add(new ReferenceOwner(1, 2));
            var first = new MemoryConfigProvider();
            first.Register(new Target(2));
            var registry = CreateRegistry(table, first);
            Assert.IsFalse(Validate(tableValidation, table, registry));

            var replacement = new MemoryConfigProvider();
            registry.RegisterProvider<Target>(replacement);
            Assert.IsTrue(Validate(tableValidation, table, registry));
            replacement.Register(new Target(2));
            Assert.IsFalse(Validate(tableValidation, table, registry));
            replacement.Remove<Target>(2);
            Assert.IsTrue(Validate(tableValidation, table, registry));
            registry.RegisterProvider<Target>(first);
            Assert.IsFalse(Validate(tableValidation, table, registry));
            registry.ClearProviders();
            registry.RegisterProvider<ReferenceOwner>(table);
            Assert.IsTrue(Validate(tableValidation, table, registry));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MultipleTargetTypes_SupportClassAndStructRows(bool tableValidation)
        {
            var table = CreateTable(includeSecondary: true);
            table.Add(new ReferenceOwner(1, 2, 3));
            table.Add(new ReferenceOwner(2, 2, 3));
            var memory = new MemoryConfigProvider();
            memory.Register(new Target(2));
            memory.Register(new StructTarget(3));
            var provider = new ObservingProvider(memory) { RejectTargetSnapshots = true };
            var registry = CreateRegistry(table, provider);
            registry.RegisterProvider<StructTarget>(provider);

            Assert.IsFalse(Validate(tableValidation, table, registry));
            Assert.AreEqual(0, provider.TargetSnapshots);
            Assert.AreEqual(4, provider.PointQueries);

            memory.Remove<StructTarget>(3);
            Assert.IsTrue(Validate(tableValidation, table, registry));
        }

        [Test]
        public void TableValidation_PreservesRequiredOptionalSeverityAndDiagnosticOrder()
        {
            var schema = new ConfigSchema<ReferenceOwner>("Owners", idRange: new ConfigIdRange(1, 2))
                .AddReferenceRule(new ConfigReferenceRule(nameof(ReferenceOwner.TargetId), typeof(Target)))
                .AddReferenceRule(new ConfigReferenceRule(nameof(ReferenceOwner.SecondaryId), typeof(StructTarget), required: false, severity: ConfigValidationSeverity.Warning))
                .AddReferenceRule(new ConfigReferenceRule(nameof(ReferenceOwner.TargetId), "ExternalGraph", ConfigStructureKind.Graph));
            var table = new ConfigTable<ReferenceOwner>(schema);
            table.Add(new ReferenceOwner(1, 99, 98));
            table.Add(new ReferenceOwner(2, 0, 0));
            table.Add(new ReferenceOwner(3, -1, -1));
            table.Add(new ReferenceOwner(4, 2, 3));
            var targets = new MemoryConfigProvider();
            targets.Register(new Target(2));
            targets.Register(new StructTarget(3));
            var registry = CreateRegistry(table, targets);
            registry.RegisterProvider<StructTarget>(targets);

            ConfigTableValidationReport report = table.Validate(registry);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(6, report.Issues.Count);
            AssertTableIssue(report.Issues[0], ConfigValidationSeverity.Error, ConfigError.NotFound, 1, nameof(ReferenceOwner.TargetId), $"Missing config reference. Field=TargetId, Target={typeof(Target).FullName}:99.");
            AssertTableIssue(report.Issues[1], ConfigValidationSeverity.Warning, ConfigError.NotFound, 1, nameof(ReferenceOwner.SecondaryId), $"Missing config reference. Field=SecondaryId, Target={typeof(StructTarget).FullName}:98.");
            AssertTableIssue(report.Issues[2], ConfigValidationSeverity.Error, ConfigError.InvalidId, 2, nameof(ReferenceOwner.TargetId), "Invalid config reference id: 0.");
            AssertTableIssue(report.Issues[3], ConfigValidationSeverity.Error, ConfigError.InvalidId, 3, "Id", "Config id out of range: 3. Expected 1-2.");
            AssertTableIssue(report.Issues[4], ConfigValidationSeverity.Error, ConfigError.InvalidId, 3, nameof(ReferenceOwner.TargetId), "Invalid config reference id: -1.");
            AssertTableIssue(report.Issues[5], ConfigValidationSeverity.Error, ConfigError.InvalidId, 4, "Id", "Config id out of range: 4. Expected 1-2.");
        }

        [TestCase(ConfigValidationSeverity.Info)]
        [TestCase(ConfigValidationSeverity.Warning)]
        public void TableValidation_NonErrorReferenceSeverityDoesNotSetHasErrors(ConfigValidationSeverity severity)
        {
            var schema = new ConfigSchema<ReferenceOwner>("Owners")
                .AddReferenceRule(new ConfigReferenceRule(nameof(ReferenceOwner.TargetId), typeof(Target), severity: severity));
            var table = new ConfigTable<ReferenceOwner>(schema);
            table.Add(new ReferenceOwner(1, 99));
            table.Add(new ReferenceOwner(2, 0));

            ConfigTableValidationReport report = table.Validate();

            Assert.IsFalse(report.HasErrors);
            Assert.AreEqual(2, report.Issues.Count);
            Assert.AreEqual(severity, report.Issues[0].Severity);
            Assert.AreEqual(severity, report.Issues[1].Severity);
        }

        [Test]
        public void InterfaceValidation_PreservesReferencesMessagesAndDiagnosticOrder()
        {
            var table = CreateTable();
            table.Add(new ReferenceOwner(1, 99));
            table.Add(new ReferenceOwner(2, 0));
            table.Add(new ReferenceOwner(3, -1));
            table.Add(new ReferenceOwner(4, 2));
            var targets = new MemoryConfigProvider();
            targets.Register(new Target(2));
            var registry = CreateRegistry(table, targets);

            ConfigValidationReport report = ConfigValidator.ValidateReferences<ReferenceOwner>(registry);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(3, report.Issues.Count);
            for (int i = 0; i < report.Issues.Count; i++)
            {
                ConfigValidationIssue issue = report.Issues[i];
                int targetId = i == 0 ? 99 : i == 1 ? 0 : -1;
                string prefix = i == 0 ? "Missing config reference." : "Invalid config reference id.";
                Assert.AreEqual(ConfigValidationSeverity.Error, issue.Severity);
                Assert.AreEqual(i == 0 ? ConfigError.NotFound : ConfigError.InvalidId, issue.Error);
                Assert.AreEqual(typeof(ReferenceOwner), issue.Reference.OwnerType);
                Assert.AreEqual(i + 1, issue.Reference.OwnerId);
                Assert.AreEqual(typeof(Target), issue.Reference.TargetType);
                Assert.AreEqual(targetId, issue.Reference.TargetId);
                Assert.AreEqual(nameof(ReferenceOwner.TargetId), issue.Reference.FieldName);
                Assert.AreEqual($"{prefix} Owner={typeof(ReferenceOwner).FullName}:{i + 1}, Field=TargetId, Target={typeof(Target).FullName}:{targetId}.", issue.Message);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProviderExceptions_KeepReflectionExceptionEnvelope(bool tableValidation)
        {
            var table = CreateTable();
            table.Add(new ReferenceOwner(1, 2));
            var failure = new InvalidOperationException("Provider failure");
            var provider = new ObservingProvider(new MemoryConfigProvider()) { Failure = failure };
            var registry = CreateRegistry(table, provider);

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => Validate(tableValidation, table, registry));

            Assert.AreSame(failure, exception.InnerException);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NullProviderOrTable_RetainsArgumentNullException(bool tableValidation)
        {
            if (tableValidation)
                Assert.Throws<ArgumentNullException>(() => ConfigTableValidator.Validate<ReferenceOwner>(null));
            else
                Assert.Throws<ArgumentNullException>(() => ConfigValidator.ValidateReferences<ReferenceOwner>(null));
        }

        [Test]
        public void InvalidTargetType_RetainsGenericConstraintException()
        {
            var schema = new ConfigSchema<ReferenceOwner>("Owners")
                .AddReferenceRule(new ConfigReferenceRule(nameof(ReferenceOwner.TargetId), typeof(string)));
            var table = new ConfigTable<ReferenceOwner>(schema);
            table.Add(new ReferenceOwner(1, 2));

            Assert.Throws<ArgumentException>(() => table.Validate());
        }

        [TestCase(null)]
        [TestCase(typeof(string))]
        public void InterfaceValidation_InvalidTargetTypeRetainsBindingException(Type targetType)
        {
            var provider = new MemoryConfigProvider();
            provider.Register(new RawReferenceOwner(new ConfigReference(typeof(RawReferenceOwner), 1, targetType, 2, "Target")));

            if (targetType == null)
                Assert.Throws<ArgumentNullException>(() => ConfigValidator.ValidateReferences<RawReferenceOwner>(provider));
            else
                Assert.Throws<ArgumentException>(() => ConfigValidator.ValidateReferences<RawReferenceOwner>(provider));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SameTypeReferences_ResolveThroughDefaultTableProvider(bool tableValidation)
        {
            var schema = new ConfigSchema<ReferenceOwner>("Owners")
                .AddReferenceRule(new ConfigReferenceRule(nameof(ReferenceOwner.TargetId), typeof(ReferenceOwner)));
            var table = new ConfigTable<ReferenceOwner>(schema);
            table.Add(new ReferenceOwner(1, 2, targetType: typeof(ReferenceOwner)));
            table.Add(new ReferenceOwner(2, 1, targetType: typeof(ReferenceOwner)));

            Assert.IsFalse(Validate(tableValidation, table, table));
        }

        private static ConfigTable<ReferenceOwner> CreateTable(bool includeSecondary = false)
        {
            var schema = new ConfigSchema<ReferenceOwner>("Owners")
                .AddReferenceRule(new ConfigReferenceRule(nameof(ReferenceOwner.TargetId), typeof(Target)));
            if (includeSecondary)
                schema.AddReferenceRule(new ConfigReferenceRule(nameof(ReferenceOwner.SecondaryId), typeof(StructTarget)));
            return new ConfigTable<ReferenceOwner>(schema);
        }

        private static ConfigRegistry CreateRegistry(ConfigTable<ReferenceOwner> table, IConfigProvider targets)
        {
            var registry = new ConfigRegistry();
            registry.RegisterProvider<ReferenceOwner>(table);
            registry.RegisterProvider<Target>(targets);
            return registry;
        }

        private static bool Validate(bool tableValidation, ConfigTable<ReferenceOwner> table, IConfigProvider provider)
        {
            return tableValidation ? table.Validate(provider).HasErrors : ConfigValidator.ValidateReferences<ReferenceOwner>(provider).HasErrors;
        }

        private static void AssertTableIssue(ConfigTableValidationIssue issue, ConfigValidationSeverity severity, ConfigError error, int rowId, string field, string message)
        {
            Assert.AreEqual(severity, issue.Severity);
            Assert.AreEqual(error, issue.Error);
            Assert.AreEqual("Owners", issue.TableName);
            Assert.AreEqual(rowId, issue.RowId);
            Assert.AreEqual(field, issue.FieldName);
            Assert.AreEqual(message, issue.Message);
        }

        private sealed class ReferenceOwner : IConfigData, IConfigReferenceProvider
        {
            private readonly Type _targetType;
            public ReferenceOwner(int id, int targetId, int secondaryId = 0, Type targetType = null)
            {
                Id = id;
                TargetId = targetId;
                SecondaryId = secondaryId;
                _targetType = targetType ?? typeof(Target);
            }
            public int Id { get; }
            public int TargetId { get; }
            public int SecondaryId { get; }
            public void CollectReferences(ICollection<ConfigReference> references)
            {
                references.Add(new ConfigReference(typeof(ReferenceOwner), Id, _targetType, TargetId, nameof(TargetId)));
                if (SecondaryId > 0)
                    references.Add(new ConfigReference(typeof(ReferenceOwner), Id, typeof(StructTarget), SecondaryId, nameof(SecondaryId)));
            }
        }

        private sealed class Target : IConfigData
        {
            public Target(int id) { Id = id; }
            public int Id { get; }
        }

        private sealed class RawReferenceOwner : IConfigData, IConfigReferenceProvider
        {
            private readonly ConfigReference _reference;
            public RawReferenceOwner(ConfigReference reference) { _reference = reference; }
            public int Id => _reference.OwnerId;
            public void CollectReferences(ICollection<ConfigReference> references) => references.Add(_reference);
        }

        private readonly struct StructTarget : IConfigData
        {
            public StructTarget(int id) { Id = id; }
            public int Id { get; }
        }

        private sealed class ObservingProvider : IConfigProvider
        {
            private readonly IConfigProvider _inner;
            public ObservingProvider(IConfigProvider inner) { _inner = inner; }
            public bool RejectTargetSnapshots { get; set; }
            public Exception Failure { get; set; }
            public int TargetSnapshots { get; private set; }
            public int PointQueries { get; private set; }
            public bool TryGetConfig<T>(int id, out T config) where T : IConfigData
            {
                PointQueries++;
                if (Failure != null) throw Failure;
                return _inner.TryGetConfig(id, out config);
            }
            public bool ContainsConfig<T>(int id) where T : IConfigData
            {
                PointQueries++;
                if (Failure != null) throw Failure;
                return _inner.ContainsConfig<T>(id);
            }
            public IReadOnlyCollection<T> GetAllConfigs<T>() where T : IConfigData
            {
                TargetSnapshots++;
                if (Failure != null) throw Failure;
                if (RejectTargetSnapshots) throw new InvalidOperationException("Target snapshots must not be read during reference validation.");
                return _inner.GetAllConfigs<T>();
            }
            public ConfigResult<T> GetConfigResult<T>(int id) where T : IConfigData => _inner.GetConfigResult<T>(id);
            public T GetConfig<T>(int id) where T : IConfigData => _inner.GetConfig<T>(id);
            public void Reload() => _inner.Reload();
        }
    }
}

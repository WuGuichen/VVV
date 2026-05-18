using System;
using MxFramework.DebugUI;
using MxFramework.Diagnostics;
using NUnit.Framework;

namespace MxFramework.Tests.DebugUI
{
    public sealed class DebugUiSnapshotAggregatorTests
    {
        [Test]
        public void Refresh_SortsSourcesByModeThenOrdinalName()
        {
            var registry = new FrameworkDebugSourceRegistry();
            registry.Register(new TestSource("RuntimeB", FrameworkDebugMode.Runtime));
            registry.Register(new TestSource("AuthoringA", FrameworkDebugMode.Authoring));
            registry.Register(new TestSource("RuntimeA", FrameworkDebugMode.Runtime));

            DebugUiDashboardViewModel model = new DebugUiSnapshotAggregator().Refresh(registry);

            Assert.AreEqual("AuthoringA", model.Sources[0].SourceName);
            Assert.AreEqual("RuntimeA", model.Sources[1].SourceName);
            Assert.AreEqual("RuntimeB", model.Sources[2].SourceName);
        }

        [Test]
        public void Refresh_KeepsUnavailableSourceVisible()
        {
            var registry = new FrameworkDebugSourceRegistry();
            registry.Register(new TestSource("Offline", FrameworkDebugMode.Runtime, available: false));

            DebugUiDashboardViewModel model = new DebugUiSnapshotAggregator().Refresh(registry);

            Assert.AreEqual(1, model.SourceCount);
            Assert.AreEqual(DebugUiSourceStatus.Unavailable, model.Sources[0].Status);
            Assert.AreEqual(0, model.ErrorCount);
        }

        [Test]
        public void Refresh_CapturesSourceExceptionAsErrorViewModel()
        {
            var registry = new FrameworkDebugSourceRegistry();
            registry.Register(new ThrowingSource());
            registry.Register(new TestSource("Healthy", FrameworkDebugMode.Runtime));

            DebugUiDashboardViewModel model = new DebugUiSnapshotAggregator().Refresh(registry);

            Assert.AreEqual(2, model.SourceCount);
            Assert.AreEqual(1, model.ErrorCount);
            Assert.AreEqual("Broken", model.Errors[0].SourceName);
            Assert.AreEqual(DebugUiSourceStatus.Error, model.Sources[0].Status);
        }

        [Test]
        public void Refresh_CapturesMetadataExceptionAsErrorViewModel()
        {
            var registry = new FrameworkDebugSourceRegistry();
            var source = new ThrowingMetadataSource("MetadataBroken", FrameworkDebugMode.Runtime);
            registry.Register(source);
            source.ThrowMode = true;

            DebugUiDashboardViewModel model = new DebugUiSnapshotAggregator().Refresh(registry);

            Assert.AreEqual(1, model.SourceCount);
            Assert.AreEqual(1, model.ErrorCount);
            Assert.AreEqual("MetadataBroken", model.Errors[0].SourceName);
            Assert.AreEqual(nameof(InvalidOperationException), model.Errors[0].ExceptionType);
            Assert.AreEqual(DebugUiSourceStatus.Error, model.Sources[0].Status);
        }

        [Test]
        public void Refresh_CapturesNameExceptionWithoutFailingSort()
        {
            var registry = new FrameworkDebugSourceRegistry();
            var source = new ThrowingMetadataSource("NameBroken", FrameworkDebugMode.Runtime);
            registry.Register(source);
            source.ThrowName = true;
            registry.Register(new TestSource("Healthy", FrameworkDebugMode.Runtime));

            DebugUiDashboardViewModel model = new DebugUiSnapshotAggregator().Refresh(registry);

            Assert.AreEqual(2, model.SourceCount);
            Assert.AreEqual(1, model.ErrorCount);
            Assert.AreEqual(nameof(ThrowingMetadataSource), model.Errors[0].SourceName);
        }

        [Test]
        public void Refresh_IncrementsSequenceAndReturnsNewModels()
        {
            var registry = new FrameworkDebugSourceRegistry();
            registry.Register(new TestSource("Runtime", FrameworkDebugMode.Runtime));
            var aggregator = new DebugUiSnapshotAggregator();

            DebugUiDashboardViewModel first = aggregator.Refresh(registry);
            DebugUiDashboardViewModel second = aggregator.Refresh(registry);

            Assert.AreNotSame(first, second);
            Assert.AreEqual(first.RefreshSequence + 1, second.RefreshSequence);
        }

        private sealed class TestSource : IFrameworkDebugSource
        {
            private readonly bool _available;

            public TestSource(string name, FrameworkDebugMode mode, bool available = true)
            {
                Name = name;
                Mode = mode;
                _available = available;
            }

            public string Name { get; }
            public FrameworkDebugMode Mode { get; }
            public bool IsAvailable => _available;

            public FrameworkDebugSnapshot CreateSnapshot()
            {
                return new FrameworkDebugSnapshot(Name, Mode, new[] { new FrameworkDebugSection("Summary", "ok") });
            }
        }

        private sealed class ThrowingSource : IFrameworkDebugSource
        {
            public string Name => "Broken";
            public FrameworkDebugMode Mode => FrameworkDebugMode.Authoring;
            public bool IsAvailable => true;

            public FrameworkDebugSnapshot CreateSnapshot()
            {
                throw new InvalidOperationException("snapshot failed");
            }
        }

        private sealed class ThrowingMetadataSource : IFrameworkDebugSource
        {
            private readonly string _name;
            private readonly FrameworkDebugMode _mode;

            public ThrowingMetadataSource(string name, FrameworkDebugMode mode)
            {
                _name = name;
                _mode = mode;
            }

            public bool ThrowName { get; set; }
            public bool ThrowMode { get; set; }
            public bool ThrowAvailability { get; set; }

            public string Name
            {
                get
                {
                    if (ThrowName)
                        throw new InvalidOperationException("name failed");

                    return _name;
                }
            }

            public FrameworkDebugMode Mode
            {
                get
                {
                    if (ThrowMode)
                        throw new InvalidOperationException("mode failed");

                    return _mode;
                }
            }

            public bool IsAvailable
            {
                get
                {
                    if (ThrowAvailability)
                        throw new InvalidOperationException("availability failed");

                    return true;
                }
            }

            public FrameworkDebugSnapshot CreateSnapshot()
            {
                return new FrameworkDebugSnapshot(_name, _mode, new[] { new FrameworkDebugSection("Summary", "ok") });
            }
        }
    }
}

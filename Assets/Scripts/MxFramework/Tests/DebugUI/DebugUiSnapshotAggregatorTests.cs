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
    }
}

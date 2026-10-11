using MxFramework.DebugUI;
using MxFramework.Diagnostics;
using NUnit.Framework;

namespace MxFramework.Tests.DebugUI
{
    public sealed class FrameworkDebugSourceRegistryTests
    {
        [Test]
        public void Register_RejectsDuplicateNameWithOrdinalComparison()
        {
            var registry = new FrameworkDebugSourceRegistry();

            Assert.IsTrue(registry.Register(new TestSource("Runtime")));
            Assert.IsFalse(registry.Register(new TestSource("Runtime")));
            Assert.IsTrue(registry.Register(new TestSource("runtime")));

            Assert.AreEqual(2, registry.Sources.Count);
        }

        [Test]
        public void Unregister_RemovesSourceByName()
        {
            var registry = new FrameworkDebugSourceRegistry();
            registry.Register(new TestSource("Runtime"));
            registry.Register(new TestSource("Gameplay"));

            Assert.IsTrue(registry.Unregister("Runtime"));
            Assert.IsFalse(registry.TryGet("Runtime", out _));
            Assert.IsTrue(registry.TryGet("Gameplay", out IFrameworkDebugSource source));
            Assert.AreEqual("Gameplay", source.Name);
        }

        private sealed class TestSource : IFrameworkDebugSource
        {
            public TestSource(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
            public bool IsAvailable => true;

            public FrameworkDebugSnapshot CreateSnapshot()
            {
                return new FrameworkDebugSnapshot(Name, Mode, new[] { new FrameworkDebugSection("Summary", "ok") });
            }
        }
    }
}

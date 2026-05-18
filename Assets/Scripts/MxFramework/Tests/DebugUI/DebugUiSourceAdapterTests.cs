using MxFramework.Combat.Core;
using MxFramework.Combat.Diagnostics;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.DebugUI;
using MxFramework.DebugUI.Adapters;
using MxFramework.Diagnostics;
using MxFramework.Gameplay;
using MxFramework.Logging;
using MxFramework.Logging.Diagnostics;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.DebugUI
{
    public sealed class DebugUiSourceAdapterTests
    {
        [Test]
        public void Dashboard_CanAggregateLoggingRuntimeAndResources()
        {
            var logs = new LogBuffer(4);
            logs.Add(new LogEntry(LogLevel.Info, "debug", "ready"));
            var runtimeHost = new RuntimeHost();
            runtimeHost.RegisterModule(new TestRuntimeModule("test.module"));
            runtimeHost.Initialize();
            runtimeHost.Start();

            var registry = new FrameworkDebugSourceRegistry();
            registry.Register(new LogDebugSource(logs));
            registry.Register(new RuntimeHostDebugSource(runtimeHost));
            registry.Register(new ResourceDebugSource(new ResourceManager()));

            DebugUiDashboardViewModel model = new DebugUiSnapshotAggregator().Refresh(registry);

            Assert.AreEqual(3, model.SourceCount);
            Assert.AreEqual(0, model.ErrorCount);
            Assert.That(FindSource(model, "Logging").Sections[0].Body, Does.Contain("ready"));
            Assert.That(FindSource(model, "RuntimeHost").Sections[0].Body, Does.Contain("Started"));
            Assert.That(FindSource(model, "Resources").Sections[0].Body, Does.Contain("catalogs"));
        }

        [Test]
        public void GameplayComponentWorldAdapter_MapsEmptyWorld()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();

            FrameworkDebugSnapshot snapshot = new GameplayComponentWorldDebugSource(world).CreateSnapshot();

            Assert.AreEqual("GameplayComponentWorld", snapshot.SourceName);
            Assert.That(snapshot.Sections[0].Body, Does.Contain("aliveEntities: 1"));
            Assert.AreEqual(entity, world.CreateEntitySnapshot()[0]);
        }

        [Test]
        public void CombatAdapter_MapsSnapshotSummaryAndHits()
        {
            var builder = new CombatDebugSnapshotBuilder();
            builder.AddInput(new CombatReplayInput(new CombatFrame(1), new CombatEntityId(7), commandId: 10, value: 3));
            builder.AddHit(new CombatHitExplain(
                new HitResolveResult(
                    new CombatEntityId(7),
                    new CombatEntityId(8),
                    actionId: 20,
                    actionInstanceId: 21,
                    traceId: 22,
                    frame: new CombatFrame(1),
                    kind: HitResolveKind.Damage,
                    damage: 30,
                    staggerFrames: 2,
                    knockback: FixVector3.Zero),
                "Damage"));

            FrameworkDebugSnapshot snapshot = new CombatDebugSnapshotDebugSource(() => builder.Build(new CombatFrame(1))).CreateSnapshot();

            Assert.AreEqual("Combat", snapshot.SourceName);
            Assert.That(snapshot.Sections[0].Body, Does.Contain("hits=1"));
            Assert.That(snapshot.Sections[3].Body, Does.Contain("damage=30"));
        }

        private static DebugUiSourceViewModel FindSource(DebugUiDashboardViewModel model, string name)
        {
            for (int i = 0; i < model.Sources.Count; i++)
            {
                if (model.Sources[i].SourceName == name)
                    return model.Sources[i];
            }

            Assert.Fail("Missing source: " + name);
            return null;
        }

        private sealed class TestRuntimeModule : IRuntimeModule
        {
            public TestRuntimeModule(string moduleId)
            {
                ModuleId = moduleId;
            }

            public string ModuleId { get; }
            public RuntimeTickStage TickStage => RuntimeTickStage.Diagnostics;
            public int Priority => 0;
            public void Initialize(RuntimeHostContext context) { }
            public void Start(RuntimeHostContext context) { }
            public void Tick(RuntimeTickContext context) { }
            public void Stop(RuntimeHostContext context) { }
            public void Dispose() { }
        }
    }
}

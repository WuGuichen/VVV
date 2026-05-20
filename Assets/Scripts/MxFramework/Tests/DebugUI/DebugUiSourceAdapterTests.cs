using MxFramework.Combat.Core;
using MxFramework.Combat.Diagnostics;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
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

        [Test]
        public void GameplayTimelineAdapter_MapsRuntimeEvents()
        {
            var events = new[]
            {
                new GameplayRuntimeEvent(
                    new RuntimeFrame(2),
                    GameplayRuntimeEventType.ComponentAttributeChanged,
                    commandId: 10,
                    casterEntityId: 0,
                    abilityId: 0,
                    targetEntityId: 0,
                    failureCode: GameplayAbilityRuntimeFailureCode.None,
                    reason: "attribute changed",
                    traceId: "trace-2",
                    componentEntityIndex: 4,
                    componentEntityGeneration: 1,
                    attributeId: 99,
                    oldAttributeValue: 1,
                    newAttributeValue: 5,
                    attributeDelta: 4)
            };

            FrameworkDebugSnapshot snapshot = new GameplayRuntimeEventTimelineDebugSource(() => events).CreateSnapshot();

            Assert.AreEqual("GameplayTimeline", snapshot.SourceName);
            Assert.That(snapshot.Sections[1].Body, Does.Contain("frame=2"));
            Assert.That(snapshot.Sections[1].Body, Does.Contain("entity=4:1"));
            Assert.That(snapshot.Sections[1].Body, Does.Contain("attr=99"));
        }

        [Test]
        public void CombatTimelineAdapter_MapsQueriesAndHits()
        {
            var builder = new CombatDebugSnapshotBuilder();
            builder.AddQuery(new CombatQueryTrace(
                new CombatFrame(1),
                new CombatQueryHeader(
                    3,
                    CombatQueryKind.Capsule,
                    new CombatEntityId(7),
                    traceId: 44,
                    actionId: 100,
                    sourceOrder: 0,
                    CombatPhysicsLayerMask.All)));
            builder.AddHit(new CombatHitExplain(
                new HitResolveResult(
                    new CombatEntityId(7),
                    new CombatEntityId(8),
                    actionId: 100,
                    actionInstanceId: 101,
                    traceId: 44,
                    frame: new CombatFrame(2),
                    kind: HitResolveKind.Blocked,
                    damage: 0,
                    staggerFrames: 0,
                    knockback: FixVector3.Zero),
                "Blocked"));

            FrameworkDebugSnapshot snapshot = new CombatTimelineDebugSource(() => builder.Build(new CombatFrame(2))).CreateSnapshot();

            Assert.That(snapshot.Sections[1].Body, Does.Contain("category=Query"));
            Assert.That(snapshot.Sections[1].Body, Does.Contain("category=Hit"));
            Assert.That(snapshot.Sections[1].Body, Does.Contain("kind=Blocked"));
        }

        [Test]
        public void GameplayEntityWatchAdapter_MapsPressureGuardAndArmor()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayAttributeSetComponent>()
                .Set(entity, new GameplayAttributeSetComponent(new GameplayAttributeValue(1, 100, 75)));
            world.GetOrCreateStore<GameplayPosturePressureComponent>()
                .Set(entity, new GameplayPosturePressureComponent(100, currentPressure: 75));
            world.GetOrCreateStore<GameplayGuardPressureComponent>()
                .Set(entity, new GameplayGuardPressureComponent(80, currentPressure: 80));
            world.GetOrCreateStore<GameplayArmorIntegrityComponent>()
                .Set(entity, new GameplayArmorIntegrityComponent(20, 5));

            FrameworkDebugSnapshot snapshot = new GameplayComponentWorldEntityWatchDebugSource(world).CreateSnapshot();

            Assert.That(snapshot.Sections[1].Body, Does.Contain("entity=1:1"));
            Assert.That(snapshot.Sections[1].Body, Does.Contain("attrs=1=75/100"));
            Assert.That(snapshot.Sections[1].Body, Does.Contain("pressure=Critical 75/100"));
            Assert.That(snapshot.Sections[1].Body, Does.Contain("guard=Broken 80/80 broken"));
            Assert.That(snapshot.Sections[1].Body, Does.Contain("armor=5/20"));
        }

        [Test]
        public void PerformanceCounterAdapters_MapRuntimeAndGameplaySources()
        {
            var runtimeHost = new RuntimeHost();
            runtimeHost.RegisterModule(new TestRuntimeModule("test.module"));
            runtimeHost.Initialize();
            runtimeHost.Start();
            runtimeHost.Tick(0, 0.016d);

            var gameplaySnapshot = new GameplayDiagnosticSnapshot(
                "Gameplay",
                "test",
                new[] { new GameplayEntitySnapshot(1, 1, true, null, null, null) },
                default,
                new[] { new GameplayAbilityEventSnapshot("Cast", 10, 1, 2, string.Empty) },
                null);

            FrameworkPerformanceCounterSnapshot runtimeCounters =
                new RuntimeHostPerformanceCounterSource(runtimeHost).Capture();
            FrameworkPerformanceCounterSnapshot gameplayCounters =
                new GameplayDiagnosticPerformanceCounterSource(() => gameplaySnapshot).Capture();

            Assert.That(runtimeCounters.Samples[0].CounterId, Does.Contain("runtime"));
            Assert.AreEqual(1, FindCounter(runtimeCounters, "runtime.tickCount").Value);
            Assert.AreEqual(1, FindCounter(gameplayCounters, "gameplay.entityCount").Value);
            Assert.AreEqual(1, FindCounter(gameplayCounters, "gameplay.abilityEventCount").Value);
        }

        [Test]
        public void UiCameraAdapter_MapsRigsAndDiagnostics()
        {
            var snapshot = new UiCameraDebugSnapshot(
                new[]
                {
                    new UiCameraRigDebugSnapshot(
                        "ui.preview.character",
                        "PreviewTexture",
                        available: true,
                        stackBound: false,
                        layerPolicyValid: true,
                        uiLayerName: "MxUiPreview3D",
                        targetTextureAssigned: true,
                        targetTextureWidth: 256,
                        targetTextureHeight: 128),
                    new UiCameraRigDebugSnapshot(
                        "ui.presentation",
                        "Overlay3D",
                        available: false,
                        stackBound: false,
                        layerPolicyValid: false,
                        uiLayerName: "MxUi3D",
                        targetTextureAssigned: false,
                        targetTextureWidth: 0,
                        targetTextureHeight: 0,
                        code: "CAM_UI_LAYER_MASK_INVALID",
                        message: "invalid mask")
                },
                new[]
                {
                    new UiCameraDiagnostic("CAM_UI_LAYER_MASK_INVALID", "ui.presentation", "invalid mask"),
                    new UiCameraDiagnostic("CAM_UI_PREVIEW_TEXTURE_MISSING", "ui.preview.inventory", "texture missing")
                });

            FrameworkDebugSnapshot debugSnapshot = new UiCameraDebugSource(() => snapshot).CreateSnapshot();

            Assert.AreEqual("UICamera", debugSnapshot.SourceName);
            Assert.That(debugSnapshot.Sections[0].Body, Does.Contain("rigs: 2"));
            Assert.That(debugSnapshot.Sections[0].Body, Does.Contain("targetTextures: 1"));
            Assert.That(debugSnapshot.Sections[1].Body, Does.Contain("rig=ui.presentation"));
            Assert.That(debugSnapshot.Sections[1].Body, Does.Contain("layerPolicy=invalid"));
            Assert.That(debugSnapshot.Sections[1].Body, Does.Contain("textureSize=256x128"));
            Assert.That(debugSnapshot.Sections[2].Body, Does.Contain("CAM_UI_LAYER_MASK_INVALID"));
            Assert.That(debugSnapshot.Sections[2].Body, Does.Contain("CAM_UI_PREVIEW_TEXTURE_MISSING"));
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

        private static FrameworkPerformanceCounterSample FindCounter(
            FrameworkPerformanceCounterSnapshot snapshot,
            string counterId)
        {
            for (int i = 0; i < snapshot.Samples.Count; i++)
            {
                if (snapshot.Samples[i].CounterId == counterId)
                    return snapshot.Samples[i];
            }

            Assert.Fail("Missing counter: " + counterId);
            return default;
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

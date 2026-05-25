using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Gameplay;
using MxFramework.Rendering;
using MxFramework.Rendering.GameplayBridge;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Rendering
{
    public sealed class GameplayRenderingBridgeTests
    {
        [Test]
        public void BridgeLifecycle_IsIdempotentAndRequiresConstructorDependencies()
        {
            GameplayRuntimeModule gameplay = CreateGameplayModule();
            var registry = new MxRenderSubjectRegistry();
            var publisher = new RenderDataPublisher(registry);
            IRenderSubjectMap<GameplayEntityId> componentSubjects = registry.CreateMap<GameplayEntityId>();

            Assert.Throws<ArgumentNullException>(() => new GameplayRenderingBridge(null, publisher, componentSubjects));
            Assert.Throws<ArgumentNullException>(() => new GameplayRenderingBridge(gameplay, null, componentSubjects));
            Assert.Throws<ArgumentNullException>(() => new GameplayRenderingBridge(gameplay, publisher, null));

            var bridge = new GameplayRenderingBridge(gameplay, publisher, componentSubjects);

            bridge.Install();
            bridge.Install();
            Assert.IsTrue(bridge.IsInstalled);

            bridge.Uninstall();
            bridge.Uninstall();
            Assert.IsFalse(bridge.IsInstalled);

            bridge.Dispose();
            bridge.Dispose();
            Assert.IsTrue(bridge.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() => bridge.Install());
            Assert.Throws<ObjectDisposedException>(() => bridge.DrainFrame(new RuntimeFrame(1)));
        }

        [Test]
        public void DrainFrame_WhenUninstalled_DoesNotConsumeOrPublishEvents()
        {
            GameplayRuntimeModule gameplay = CreateGameplayModule();
            var registry = new MxRenderSubjectRegistry();
            var publisher = new RenderDataPublisher(registry);
            IRenderSubjectMap<GameplayEntityId> componentSubjects = registry.CreateMap<GameplayEntityId>();
            var bridge = new GameplayRenderingBridge(gameplay, publisher, componentSubjects);
            RuntimeFrame frame = new RuntimeFrame(10);
            GameplayEntityId entityId = EnqueueComponentCreated(gameplay, frame);

            Assert.AreEqual(0, bridge.DrainFrame(frame));

            Assert.IsFalse(componentSubjects.TryResolve(entityId, out var _));
            Assert.AreEqual(0, publisher.CaptureSnapshot().TotalEventCount);
            var drained = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(1, gameplay.DrainEvents(frame, drained));
        }

        [Test]
        public void DrainFrame_TranslatesComponentLifecycleAndReleasesMapping()
        {
            GameplayRuntimeModule gameplay = CreateGameplayModule();
            var registry = new MxRenderSubjectRegistry();
            var publisher = new RecordingRenderDataPublisher();
            IRenderSubjectMap<GameplayEntityId> componentSubjects = registry.CreateMap<GameplayEntityId>();
            var bridge = new GameplayRenderingBridge(gameplay, publisher, componentSubjects);
            RuntimeFrame frame = new RuntimeFrame(20);
            GameplayEntityId entityId = EnqueueComponentCreated(gameplay, frame);

            bridge.Install();
            Assert.AreEqual(1, bridge.DrainFrame(frame));

            Assert.IsTrue(componentSubjects.TryResolve(entityId, out MxRenderSubjectId subject));
            Assert.AreEqual(1, publisher.LifecycleEvents.Count);
            Assert.AreEqual(MxSubjectLifecycleKind.Spawned, publisher.LifecycleEvents[0].Lifecycle);
            Assert.AreEqual(subject, publisher.LifecycleEvents[0].Subject);

            EnqueueComponentDestroyed(gameplay, frame, entityId);
            Assert.AreEqual(1, bridge.DrainFrame(frame));

            Assert.IsFalse(componentSubjects.TryResolve(entityId, out var _));
            Assert.IsFalse(registry.TryResolve(subject, out var _));
            Assert.AreEqual(2, publisher.LifecycleEvents.Count);
            Assert.AreEqual(MxSubjectLifecycleKind.Despawned, publisher.LifecycleEvents[1].Lifecycle);
            Assert.AreEqual(subject, publisher.LifecycleEvents[1].Subject);
        }

        [Test]
        public void DrainFrame_TranslatesRuntimeEntityDespawnOnlyForExistingPublicSourceMapping()
        {
            GameplayRuntimeModule gameplay = CreateGameplayModule();
            var registry = new MxRenderSubjectRegistry();
            var publisher = new RecordingRenderDataPublisher();
            IRenderSubjectMap<GameplayEntityId> componentSubjects = registry.CreateMap<GameplayEntityId>();
            IRenderSubjectMap<int> runtimeSubjects = registry.CreateMap<int>();
            var bridge = new GameplayRenderingBridge(gameplay, publisher, componentSubjects, runtimeSubjects);
            RuntimeFrame frame = new RuntimeFrame(30);
            MxRenderSubjectId subject = runtimeSubjects.GetOrCreate(17, MxRenderSubjectRole.Focus);

            gameplay.ComponentWorld.EnqueueEvent(new GameplayRuntimeEvent(
                frame,
                GameplayRuntimeEventType.EntityDespawned,
                commandId: 0,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: 17,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: string.Empty,
                traceId: string.Empty));

            bridge.Install();
            Assert.AreEqual(1, bridge.DrainFrame(frame));

            Assert.IsFalse(runtimeSubjects.TryResolve(17, out var _));
            Assert.IsFalse(registry.TryResolve(subject, out var _));
            Assert.AreEqual(1, publisher.LifecycleEvents.Count);
            Assert.AreEqual(subject, publisher.LifecycleEvents[0].Subject);
            Assert.AreEqual(MxSubjectLifecycleKind.Despawned, publisher.LifecycleEvents[0].Lifecycle);
        }

        [Test]
        public void DrainFrame_IgnoresUnsupportedEventsAndInvalidLifecyclePayloads()
        {
            GameplayRuntimeModule gameplay = CreateGameplayModule();
            var registry = new MxRenderSubjectRegistry();
            var publisher = new RenderDataPublisher(registry);
            IRenderSubjectMap<GameplayEntityId> componentSubjects = registry.CreateMap<GameplayEntityId>();
            var bridge = new GameplayRenderingBridge(gameplay, publisher, componentSubjects);
            RuntimeFrame frame = new RuntimeFrame(40);

            gameplay.ComponentWorld.EnqueueEvent(new GameplayRuntimeEvent(
                frame,
                GameplayRuntimeEventType.WorldTicked,
                commandId: 0,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: 0,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: string.Empty,
                traceId: string.Empty));
            gameplay.ComponentWorld.EnqueueEvent(new GameplayRuntimeEvent(
                frame,
                GameplayRuntimeEventType.ComponentEntityDestroyed,
                commandId: 0,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: 0,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: string.Empty,
                traceId: string.Empty));

            bridge.Install();
            Assert.AreEqual(2, bridge.DrainFrame(frame));

            Assert.AreEqual(0, publisher.CaptureSnapshot().TotalEventCount);
            Assert.AreEqual(0, registry.ActiveCount);
        }

        [Test]
        public void Uninstall_ReleasesKnownComponentSubjects()
        {
            GameplayRuntimeModule gameplay = CreateGameplayModule();
            var registry = new MxRenderSubjectRegistry();
            var publisher = new RenderDataPublisher(registry);
            IRenderSubjectMap<GameplayEntityId> componentSubjects = registry.CreateMap<GameplayEntityId>();
            var bridge = new GameplayRenderingBridge(gameplay, publisher, componentSubjects);
            RuntimeFrame frame = new RuntimeFrame(50);
            GameplayEntityId entityId = EnqueueComponentCreated(gameplay, frame);

            bridge.Install();
            bridge.DrainFrame(frame);
            Assert.IsTrue(componentSubjects.TryResolve(entityId, out var _));

            bridge.Uninstall();

            Assert.IsFalse(componentSubjects.TryResolve(entityId, out var _));
            Assert.AreEqual(0, registry.ActiveCount);
        }

        [Test]
        public void AsmdefDependencies_KeepRenderingBridgeOptionalAndOneWay()
        {
            string rendering = File.ReadAllText("Assets/Scripts/MxFramework/Rendering/MxFramework.Rendering.asmdef");
            string gameplay = File.ReadAllText("Assets/Scripts/MxFramework/Gameplay/MxFramework.Gameplay.asmdef");
            string bridge = File.ReadAllText("Assets/Scripts/MxFramework/Rendering.GameplayBridge/MxFramework.Rendering.GameplayBridge.asmdef");

            StringAssert.DoesNotContain("MxFramework.Rendering.GameplayBridge", rendering);
            StringAssert.DoesNotContain("MxFramework.Gameplay", rendering);
            StringAssert.DoesNotContain("MxFramework.Rendering", gameplay);
            StringAssert.Contains("\"MxFramework.Rendering\"", bridge);
            StringAssert.Contains("\"MxFramework.Gameplay\"", bridge);
            StringAssert.Contains("\"MxFramework.Runtime\"", bridge);
        }

        private static GameplayRuntimeModule CreateGameplayModule()
        {
            return new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                new RuntimeCommandBuffer(),
                componentWorld: new GameplayComponentWorld());
        }

        private static GameplayEntityId EnqueueComponentCreated(GameplayRuntimeModule gameplay, RuntimeFrame frame)
        {
            GameplayEntityId entityId = new GameplayEntityId(3, 1);
            gameplay.ComponentWorld.EnqueueEvent(new GameplayRuntimeEvent(
                frame,
                GameplayRuntimeEventType.ComponentEntityCreated,
                commandId: 0,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: entityId.Index,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: string.Empty,
                traceId: string.Empty,
                componentEntityIndex: entityId.Index,
                componentEntityGeneration: entityId.Generation));
            return entityId;
        }

        private static void EnqueueComponentDestroyed(
            GameplayRuntimeModule gameplay,
            RuntimeFrame frame,
            GameplayEntityId entityId)
        {
            gameplay.ComponentWorld.EnqueueEvent(new GameplayRuntimeEvent(
                frame,
                GameplayRuntimeEventType.ComponentEntityDestroyed,
                commandId: 0,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: entityId.Index,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: string.Empty,
                traceId: string.Empty,
                componentEntityIndex: entityId.Index,
                componentEntityGeneration: entityId.Generation));
        }

        private sealed class RecordingRenderDataPublisher : IRenderDataPublisher
        {
            public readonly List<LifecycleEvent> LifecycleEvents = new List<LifecycleEvent>();

            public void PublishImpact(MxRenderSubjectId subject, in MxRenderImpactEvent impact)
            {
            }

            public void PublishSurfaceContact(MxRenderSubjectId subject, in MxRenderSurfaceContactEvent contact)
            {
            }

            public void PublishFieldImpulse(MxRenderSubjectId subject, in MxRenderFieldImpulseEvent impulse)
            {
            }

            public void PublishSubjectMovement(MxRenderSubjectId subject, UnityEngine.Vector3 velocity)
            {
            }

            public void PublishSubjectLifecycle(MxRenderSubjectId subject, MxSubjectLifecycleKind lifecycle)
            {
                LifecycleEvents.Add(new LifecycleEvent(subject, lifecycle));
            }
        }

        private readonly struct LifecycleEvent
        {
            public LifecycleEvent(MxRenderSubjectId subject, MxSubjectLifecycleKind lifecycle)
            {
                Subject = subject;
                Lifecycle = lifecycle;
            }

            public MxRenderSubjectId Subject { get; }
            public MxSubjectLifecycleKind Lifecycle { get; }
        }
    }
}

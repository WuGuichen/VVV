using System.Collections.Generic;
using System.IO;
using MxFramework.Animation;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Animation.Unity;
using MxFramework.Combat.Core;
using MxFramework.Resources;
using MxFramework.Runtime.Unity;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Animation
{
    public sealed class CombatMxAnimationUnityBridgeTests
    {
        [Test]
        public void ActionStarted_DefaultCrossFadesUsingActionKeyBinding()
        {
            CombatActionRunner runner = CreateRunner(Timeline(1001, 4));
            var context = new TestCombatAnimationContext(runner);
            var backend = new RecordingAnimationBackend();
            var entity = new CombatEntityId(7);
            MxAnimationSetDefinition set = CreateAnimationSet(1001, "attack");
            var bridge = new CombatMxAnimationUnityBridge(context);

            bridge.RegisterActor(entity, backend, set, "actor-7");
            bridge.Initialize();

            ActionResult result = runner.StartAction(entity, 1001, new CombatFrame(10));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, backend.CrossFades.Count);
            Assert.AreEqual("actor-7", backend.CrossFades[0].TargetActorId);
            Assert.AreEqual("action:1001", backend.CrossFades[0].ActionKey);
            Assert.AreEqual("attack", backend.CrossFades[0].BindingId);
            Assert.AreEqual(ClipKey("clip.1001"), backend.CrossFades[0].ClipKey);
            Assert.AreEqual("entity:7|action:1001|instance:1|world:10|local:0", backend.CrossFades[0].CorrelationId);

            CombatMxAnimationBridgeDiagnosticSnapshot snapshot = bridge.CreateSnapshot();
            Assert.AreEqual(1, snapshot.RecentEntries.Count);
            Assert.AreEqual(CombatMxAnimationBridgeEventKind.ActionStarted, snapshot.RecentEntries[0].EventKind);
            Assert.AreEqual(MxAnimationRequestKind.CrossFade, snapshot.RecentEntries[0].RequestKind);
            Assert.AreEqual(entity, snapshot.RecentEntries[0].EntityId);
            Assert.AreEqual(result.ActionInstanceId, snapshot.RecentEntries[0].ActionInstanceId);
            Assert.AreEqual(new CombatFrame(10), snapshot.RecentEntries[0].WorldFrame);
            Assert.AreEqual(0, snapshot.RecentEntries[0].LocalFrame);
        }

        [Test]
        public void ActionCanceledAndFinished_DefaultStopUsingBindingLayer()
        {
            CombatActionRunner runner = CreateRunner(Timeline(1001, 2));
            var context = new TestCombatAnimationContext(runner);
            var backend = new RecordingAnimationBackend();
            var entity = new CombatEntityId(8);
            MxAnimationSetDefinition set = CreateAnimationSet(1001, "attack");
            var bridge = new CombatMxAnimationUnityBridge(context);

            bridge.RegisterActor(entity, backend, set);
            bridge.Initialize();

            runner.StartAction(entity, 1001, CombatFrame.Zero);
            runner.ForceCancel(entity);
            runner.StartAction(entity, 1001, new CombatFrame(4));
            runner.TickActions(new CombatFrame(5));
            runner.TickActions(new CombatFrame(6));

            Assert.AreEqual(2, backend.Stops.Count);
            Assert.AreEqual("attack", backend.Stops[0].BindingId);
            Assert.AreEqual(MxAnimationLayerId.Base, backend.Stops[0].LayerId);
            Assert.AreEqual("ForceCancel canceled the running action.", backend.Stops[0].StopReason);
            Assert.AreEqual("attack", backend.Stops[1].BindingId);
            Assert.AreEqual("Action finished.", backend.Stops[1].StopReason);

            CombatMxAnimationBridgeDiagnosticSnapshot snapshot = bridge.CreateSnapshot();
            Assert.AreEqual(4, snapshot.RecentEntries.Count);
            Assert.AreEqual(CombatMxAnimationBridgeEventKind.ActionCanceled, snapshot.RecentEntries[1].EventKind);
            Assert.AreEqual(MxAnimationRequestKind.Stop, snapshot.RecentEntries[1].RequestKind);
            Assert.AreEqual(CombatMxAnimationBridgeEventKind.ActionFinished, snapshot.RecentEntries[3].EventKind);
            Assert.AreEqual(MxAnimationRequestKind.Stop, snapshot.RecentEntries[3].RequestKind);
        }

        [Test]
        public void FrameEvent_DispatchesPresentationEventFromAnimationBinding()
        {
            CombatActionRunner runner = CreateRunner(Timeline(
                1001,
                4,
                new[] { new CombatActionFrameEvent(1, 77, sourceOrder: 3, intPayload: 23) }));
            var context = new TestCombatAnimationContext(runner);
            var backend = new RecordingAnimationBackend();
            var sink = new RecordingPresentationEventSink();
            var entity = new CombatEntityId(9);
            ResourceKey payload = new ResourceKey("fx.slash", "VFX");
            var presentationEvent = new MxAnimationPresentationEvent(
                "event:77",
                MxAnimationEventTimeDomain.CombatFrame,
                1f,
                "VFX",
                payload,
                socket: "weapon",
                tag: "slash");
            MxAnimationSetDefinition set = CreateAnimationSet(1001, "attack", presentationEvent);
            var bridge = new CombatMxAnimationUnityBridge(context, presentationEventSink: sink);

            bridge.RegisterActor(entity, backend, set, "actor-9");
            bridge.Initialize();

            ActionResult result = runner.StartAction(entity, 1001, new CombatFrame(10));
            runner.TickActions(new CombatFrame(11));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, sink.Dispatches.Count);
            CombatMxAnimationPresentationEventDispatch dispatch = sink.Dispatches[0];
            Assert.AreEqual(entity, dispatch.EntityId);
            Assert.AreEqual("actor-9", dispatch.TargetActorId);
            Assert.AreEqual(1001, dispatch.ActionId);
            Assert.AreEqual("action:1001", dispatch.ActionKey);
            Assert.AreEqual("attack", dispatch.BindingId);
            Assert.AreEqual(result.ActionInstanceId, dispatch.ActionInstanceId);
            Assert.AreEqual(new CombatFrame(11), dispatch.WorldFrame);
            Assert.AreEqual(1, dispatch.LocalFrame);
            Assert.AreEqual(new CombatActionFrameEvent(1, 77, sourceOrder: 3, intPayload: 23), dispatch.FrameEvent);
            Assert.AreEqual("VFX", dispatch.PresentationEvent.EventKind);
            Assert.AreEqual(payload, dispatch.PresentationEvent.PayloadKey);
            Assert.AreEqual("weapon", dispatch.PresentationEvent.Socket);
            Assert.AreEqual("entity:9|action:1001|instance:1|world:11|local:1|event:77|order:3|payload:23", dispatch.CorrelationId);
            Assert.AreEqual(
                new MxAnimationPresentationEventDedupeKey("entity:9", result.ActionInstanceId, 11, 1, "event:77", 3),
                dispatch.DedupeKey);

            CombatMxAnimationBridgeDiagnosticSnapshot snapshot = bridge.CreateSnapshot();
            CombatMxAnimationBridgeDiagnosticEntry entry = snapshot.RecentEntries[snapshot.RecentEntries.Count - 1];
            Assert.AreEqual(CombatMxAnimationBridgeEventKind.FramePresentationEvent, entry.EventKind);
            Assert.IsTrue(entry.HasFrameEvent);
            Assert.AreEqual(77, entry.FrameEvent.EventId);
            Assert.AreEqual(23, entry.FrameEvent.IntPayload);
            Assert.AreEqual("event:77", entry.PresentationEventId);
        }

        [Test]
        public void FrameEvent_DropsDuplicatePresentationEventDispatch()
        {
            CombatActionRunner runner = CreateRunner(Timeline(
                1001,
                4,
                new[]
                {
                    new CombatActionFrameEvent(1, 77, sourceOrder: 3, intPayload: 23),
                    new CombatActionFrameEvent(1, 77, sourceOrder: 3, intPayload: 23)
                }));
            var context = new TestCombatAnimationContext(runner);
            var backend = new RecordingAnimationBackend();
            var sink = new RecordingPresentationEventSink();
            var entity = new CombatEntityId(12);
            var presentationEvent = new MxAnimationPresentationEvent(
                "event:77",
                MxAnimationEventTimeDomain.CombatFrame,
                1f,
                "VFX",
                new ResourceKey("fx.slash", ResourceTypeIds.GameObject));
            MxAnimationSetDefinition set = CreateAnimationSet(1001, "attack", presentationEvent);
            var bridge = new CombatMxAnimationUnityBridge(context, presentationEventSink: sink);

            bridge.RegisterActor(entity, backend, set);
            bridge.Initialize();

            runner.StartAction(entity, 1001, new CombatFrame(10));
            runner.TickActions(new CombatFrame(11));

            Assert.AreEqual(1, sink.Dispatches.Count);

            CombatMxAnimationBridgeDiagnosticSnapshot snapshot = bridge.CreateSnapshot();
            CombatMxAnimationBridgeDiagnosticEntry last = snapshot.RecentEntries[snapshot.RecentEntries.Count - 1];
            Assert.AreEqual(CombatMxAnimationBridgeEventKind.FramePresentationEventDuplicateDropped, last.EventKind);
            Assert.IsFalse(last.Success);
            Assert.AreEqual("event:77", last.PresentationEventId);
        }

        [Test]
        public void LegacyAnimatorModule_RemainsOptInWhenNewBridgeIsInitialized()
        {
            CombatActionRunner runner = CreateRunner(Timeline(1001, 4));
            var context = new TestCombatAnimationContext(runner);
            var backend = new RecordingAnimationBackend();
            var entity = new CombatEntityId(10);
            var legacyModule = new CombatAnimationUnityModule(context);
            var legacyDriver = new RecordingAnimatorDriver();
            var bridge = new CombatMxAnimationUnityBridge(context);

            legacyModule.RegisterDriver(entity, legacyDriver);
            bridge.RegisterActor(entity, backend, CreateAnimationSet(1001, "attack"));
            bridge.Initialize();

            runner.StartAction(entity, 1001, CombatFrame.Zero);

            Assert.AreEqual(1, backend.CrossFades.Count);
            Assert.AreEqual(0, legacyDriver.TotalEvents);
        }

        [Test]
        public void AssemblyBoundaries_KeepNoEngineCombatFreeOfUnityAnimationBackend()
        {
            string combatAsmdef = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Scripts/MxFramework/Combat/MxFramework.Combat.asmdef"));
            string bridgeAsmdef = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Scripts/MxFramework/Combat.Animation.Unity/MxFramework.Combat.Animation.Unity.asmdef"));

            StringAssert.DoesNotContain("MxFramework.Animation.Unity", combatAsmdef);
            StringAssert.DoesNotContain("MxFramework.Combat.Animation.Unity", combatAsmdef);
            StringAssert.Contains("MxFramework.Combat", bridgeAsmdef);
            StringAssert.Contains("MxFramework.Animation", bridgeAsmdef);
            StringAssert.Contains("MxFramework.Animation.Unity", bridgeAsmdef);
        }

        private static CombatActionRunner CreateRunner(CombatActionTimeline timeline)
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(timeline.ActionId, timeline);
            return new CombatActionRunner(registry);
        }

        private static CombatActionTimeline Timeline(int actionId, int totalFrames, CombatActionFrameEvent[] events = null)
        {
            return new CombatActionTimeline(
                actionId,
                totalFrames,
                new CombatFrameRange(0, 0),
                totalFrames > 2 ? new CombatFrameRange(1, totalFrames - 2) : CombatFrameRange.Empty,
                new CombatFrameRange(totalFrames - 1, totalFrames - 1),
                null,
                events);
        }

        private static MxAnimationSetDefinition CreateAnimationSet(
            int actionId,
            string bindingId,
            params MxAnimationPresentationEvent[] presentationEvents)
        {
            var binding = new MxAnimationActionBinding(
                bindingId,
                "action:" + actionId,
                ClipKey("clip." + actionId),
                MxAnimationLayerId.Base,
                playbackSpeed: 1.25f,
                loop: false,
                alignmentPolicy: MxAnimationAlignmentPolicy.UseCombatFrameAnchor,
                presentationEvents: presentationEvents);
            return new MxAnimationSetDefinition("combat.test", 1, default, default, new[] { binding });
        }

        private static ResourceKey ClipKey(string id)
        {
            return new ResourceKey(id, ResourceTypeIds.AnimationClip);
        }

        private sealed class RecordingAnimationBackend : IMxAnimationBackend
        {
            public readonly List<MxAnimationPlayRequest> Plays = new List<MxAnimationPlayRequest>();
            public readonly List<MxAnimationStopRequest> Stops = new List<MxAnimationStopRequest>();
            public readonly List<MxAnimationCrossFadeRequest> CrossFades = new List<MxAnimationCrossFadeRequest>();
            public readonly List<MxAnimationLayerWeightRequest> LayerWeights = new List<MxAnimationLayerWeightRequest>();
            public readonly List<MxAnimationBlend1DRequest> Blend1DRequests = new List<MxAnimationBlend1DRequest>();

            public string BackendName => "Recording";

            public MxAnimationBackendResult Play(MxAnimationPlayRequest request)
            {
                Plays.Add(request);
                return MxAnimationBackendResult.Succeeded(request != null ? request.ClipKey : default, "Recorded play.");
            }

            public MxAnimationBackendResult Stop(MxAnimationStopRequest request)
            {
                Stops.Add(request);
                return MxAnimationBackendResult.Succeeded(default, "Recorded stop.");
            }

            public MxAnimationBackendResult CrossFade(MxAnimationCrossFadeRequest request)
            {
                CrossFades.Add(request);
                return MxAnimationBackendResult.Succeeded(request != null ? request.ClipKey : default, "Recorded crossfade.");
            }

            public MxAnimationBackendResult SetLayerWeight(MxAnimationLayerWeightRequest request)
            {
                LayerWeights.Add(request);
                return MxAnimationBackendResult.Succeeded(default, "Recorded layer weight.");
            }

            public MxAnimationBackendResult SetBlend1D(MxAnimationBlend1DRequest request)
            {
                Blend1DRequests.Add(request);
                return MxAnimationBackendResult.Succeeded(default, "Recorded 1D blend.");
            }

            public void Tick(float deltaTime)
            {
            }

            public MxAnimationDiagnosticSnapshot CreateSnapshot()
            {
                return new MxAnimationDiagnosticSnapshot(
                    BackendName,
                    string.Empty,
                    string.Empty,
                    1,
                    false,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            public void Release()
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class RecordingPresentationEventSink : ICombatMxAnimationPresentationEventSink
        {
            public readonly List<CombatMxAnimationPresentationEventDispatch> Dispatches =
                new List<CombatMxAnimationPresentationEventDispatch>();

            public void Dispatch(CombatMxAnimationPresentationEventDispatch dispatch)
            {
                Dispatches.Add(dispatch);
            }
        }

        private sealed class RecordingAnimatorDriver : ICombatAnimatorDriver
        {
            public readonly List<ActionStartedEvent> Started = new List<ActionStartedEvent>();

            public int TotalEvents => Started.Count;

            public void OnActionStarted(ActionStartedEvent evt)
            {
                Started.Add(evt);
            }

            public void OnActionPhaseChanged(ActionPhaseChangedEvent evt)
            {
            }

            public void OnActionFinished(ActionFinishedEvent evt)
            {
            }

            public void OnActionCanceled(ActionCanceledEvent evt)
            {
            }

            public void OnActionCancelRejected(ActionCancelRejectedEvent evt)
            {
            }
        }

        private sealed class TestCombatAnimationContext : ICombatAnimationContext
        {
            private readonly List<MxFramework.Combat.Hit.HitCandidate> _hitCandidates =
                new List<MxFramework.Combat.Hit.HitCandidate>();

            public TestCombatAnimationContext(CombatActionRunner runner)
            {
                ActionRunner = runner;
            }

            public CombatActionRunner ActionRunner { get; private set; }

            public IReadOnlyList<MxFramework.Combat.Hit.HitCandidate> LastFrameHitCandidates => _hitCandidates;

            public CombatAnimationSnapshot? LastSnapshot { get; private set; }

            public void SetActionRunner(CombatActionRunner runner)
            {
                ActionRunner = runner;
            }

            public void SetLastFrameHitCandidates(List<MxFramework.Combat.Hit.HitCandidate> candidates)
            {
                _hitCandidates.Clear();
                if (candidates != null)
                {
                    _hitCandidates.AddRange(candidates);
                }
            }

            public void SetLastSnapshot(CombatAnimationSnapshot snapshot)
            {
                LastSnapshot = snapshot;
            }
        }
    }
}

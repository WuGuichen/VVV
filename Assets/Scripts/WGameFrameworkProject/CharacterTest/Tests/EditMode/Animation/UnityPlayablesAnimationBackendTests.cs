using System;
using System.Threading;
using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Animation
{
    public sealed class UnityPlayablesAnimationBackendTests
    {
        [Test]
        public void DeferredPose_AdvancesAnchoredClockAndCrossfade_WithoutChangingBoneUntilEvaluation()
        {
            ResourceKey idleKey = ClipKey("pose.cadence.idle"), actionKey = ClipKey("pose.cadence.action");
            AnimationClip idle = CreateClip("cadence.idle"), action = CreateClip("cadence.action");
            action.SetCurve("bone", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var manager = CreateManager(new MemoryResourceProvider().Register("cadence/idle", idle).Register("cadence/action", action),
                Entry(idleKey, "cadence/idle"), Entry(actionKey, "cadence/action"));
            try
            {
                using (BackendFixture fixture = BackendFixture.Create(manager, EmptyDefinition()))
                {
                    Transform bone = new GameObject("bone").transform; bone.SetParent(fixture.Actor.transform, false);
                    Assert.True(fixture.Backend.Play(new MxAnimationPlayRequest { ClipKey = idleKey }).Success);
                    Assert.True(fixture.Backend.CrossFade(new MxAnimationCrossFadeRequest {
                        ClipKey = actionKey, PlaybackSpeed = 1.5f, FadeDurationSeconds = 0.12f }).Success);
                    Assert.True(fixture.Backend.TrySynchronizeCurrentClipTime(MxAnimationLayerId.Base, actionKey, 0.2d));
                    fixture.Backend.Tick(0.04f);
                    float published = bone.localPosition.x;
                    long evaluations = fixture.Backend.PoseEvaluations;
                    Assert.True(fixture.Backend.CanDeferPoseEvaluation);
                    Assert.True(fixture.Backend.TrySynchronizeCurrentClipTime(MxAnimationLayerId.Base, actionKey, 0.7d));
                    fixture.Backend.AdvancePresentationState(0.04f);
                    Assert.AreEqual(0.7d, fixture.Backend.CapturePlaybackState().Layers[0].Slots[0].TimeSeconds, 1e-9);
                    Assert.AreEqual(published, bone.localPosition.x, 1e-6f);
                    Assert.AreEqual(evaluations, fixture.Backend.PoseEvaluations);
                    Assert.Greater(FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base).OutgoingWeight, 0);
                    Assert.True(fixture.Backend.TrySynchronizeCurrentClipTime(MxAnimationLayerId.Base, actionKey, 0.9d));
                    fixture.Backend.AdvancePresentationState(0.08f);
                    Assert.AreEqual(1, FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base).ActivePlayableCount);
                    fixture.Backend.EvaluateCurrentPose();
                    Assert.AreEqual(0.9f, bone.localPosition.x, 0.0001f);
                    Assert.AreEqual(0.9d, fixture.Backend.CapturePlaybackState().Layers[0].Slots[0].TimeSeconds, 1e-9);
                    fixture.Backend.AdvancePresentationState(0.1f);
                    Assert.AreEqual(1.05d, fixture.Backend.CapturePlaybackState().Layers[0].Slots[0].TimeSeconds, 0.0001d);
                }
            }
            finally { Object.DestroyImmediate(idle); Object.DestroyImmediate(action); }
        }

        [Test]
        public void DeferredPose_LocomotionSlotsKeepTheSamePhaseAndClockAsFullEvaluation()
        {
            ResourceKey walkKey = ClipKey("cadence.walk"), runKey = ClipKey("cadence.run");
            AnimationClip walk = CreateClip("cadence.walk"), run = CreateClip("cadence.run");
            walk.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            run.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 0.5f, 1f));
            var manager = CreateManager(new MemoryResourceProvider().Register("cadence/walk", walk).Register("cadence/run", run),
                Entry(walkKey, "cadence/walk"), Entry(runKey, "cadence/run"));
            var blend = new MxAnimationBlend2DDefinition("cadence.blend", "moveX", "moveY", MxAnimationLayerId.Base,
                new[] { new MxAnimationBlend2DPoint(0, 500, walkKey), new MxAnimationBlend2DPoint(0, 1000, runKey) });
            var definition = new MxAnimationSetDefinition("cadence.set", 1, default, default, blend2DDefinitions: new[] { blend });
            try
            {
                using (BackendFixture full = BackendFixture.Create(manager, definition))
                using (BackendFixture deferred = BackendFixture.Create(manager, definition))
                {
                    var request = new MxAnimationBlend2DRequest { BlendId = "cadence.blend",
                        ParameterX = new MxAnimationQuantizedParameter("moveX", 0), ParameterY = new MxAnimationQuantizedParameter("moveY", 750) };
                    Assert.True(full.Backend.SetBlend2D(request).Success); Assert.True(deferred.Backend.SetBlend2D(request).Success);
                    // Publish the initial graph pose through the unchanged full-tick path before permitting holds.
                    full.Backend.Tick(0f); deferred.Backend.Tick(0f);
                    float[] deltas = { 0.03f, 0.04f, 0.1f, 0f, 0.07f };
                    foreach (float delta in deltas)
                    {
                        full.Backend.Tick(delta); deferred.Backend.AdvancePresentationState(delta);
                        var a = FindLayer(full.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                        var b = FindLayer(deferred.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                        Assert.AreEqual(FindPlayback(a, walkKey, true).NormalizedTime, FindPlayback(b, walkKey, true).NormalizedTime, 0.0001f);
                        Assert.AreEqual(FindPlayback(a, runKey, true).NormalizedTime, FindPlayback(b, runKey, true).NormalizedTime, 0.0001f);
                    }
                    deferred.Backend.EvaluateCurrentPose();
                    Assert.AreEqual(full.Actor.transform.localPosition.x, deferred.Actor.transform.localPosition.x, 0.0001f);
                    Assert.AreEqual(2, deferred.Backend.PoseEvaluations);
                }
            }
            finally { Object.DestroyImmediate(walk); Object.DestroyImmediate(run); }
        }

        [Test]
        public void DeferredPose_EventAndRootMotionPlaybackRemainProtected()
        {
            ResourceKey key = ClipKey("cadence.events"); AnimationClip clip = CreateClip("cadence.events");
            UnityEditor.AnimationUtility.SetAnimationEvents(clip,
                new[] { new AnimationEvent { time = 0.5f, functionName = "PresentationEvent" } });
            var manager = CreateManager(new MemoryResourceProvider().Register("cadence/events", clip), Entry(key, "cadence/events"));
            try
            {
                using (BackendFixture fixture = BackendFixture.Create(manager, EmptyDefinition()))
                {
                    Assert.True(fixture.Backend.Play(new MxAnimationPlayRequest { ClipKey = key }).Success);
                    Animator animator = fixture.Actor.GetComponent<Animator>();
                    animator.fireEvents = false; fixture.Backend.Tick(0f);
                    animator.fireEvents = true; Assert.False(fixture.Backend.CanDeferPoseEvaluation);
                    animator.fireEvents = false; Assert.True(fixture.Backend.CanDeferPoseEvaluation);
                    animator.applyRootMotion = true; Assert.False(fixture.Backend.CanDeferPoseEvaluation);
                }
            }
            finally { Object.DestroyImmediate(clip); }
        }

        [Test]
        public void BladeTrailPoseSample_RetainsClipClockSpeedCrossfadeAndAnimatorEvents()
        {
            ResourceKey idleKey = ClipKey("demo.animation.trail.idle");
            ResourceKey attackKey = ClipKey("demo.animation.trail.attack");
            AnimationClip idle = CreateClip("trail.idle");
            AnimationClip attack = CreateClip("trail.attack");
            attack.SetCurve("bone", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f,0f,1f,1f));
            var provider = new MemoryResourceProvider().Register("trail/idle",idle).Register("trail/attack",attack);
            ResourceManager manager = CreateManager(provider,Entry(idleKey,"trail/idle"),Entry(attackKey,"trail/attack"));
            try
            {
                using (BackendFixture fixture = BackendFixture.Create(manager,EmptyDefinition()))
                {
                    Transform bone = new GameObject("bone").transform; bone.SetParent(fixture.Actor.transform,false);
                    Assert.True(fixture.Backend.Play(new MxAnimationPlayRequest { ClipKey=idleKey }).Success);
                    Assert.True(fixture.Backend.CrossFade(new MxAnimationCrossFadeRequest {
                        ClipKey=attackKey, PlaybackSpeed=1.5f, FadeDurationSeconds=0.12f }).Success);
                    Assert.True(fixture.Backend.TrySynchronizeCurrentClipTime(MxAnimationLayerId.Base,attackKey,0.2d));
                    fixture.Backend.Tick(0.04f);
                    var before = fixture.Backend.CapturePlaybackState().Layers[0].Slots[0];
                    var weightsBefore = FindLayer(fixture.Backend.CreateSnapshot(),MxAnimationLayerId.Base);
                    Animator animator = fixture.Actor.GetComponent<Animator>();
                    animator.fireEvents = true;
                    Vector3 savedPosition=bone.localPosition; Quaternion savedRotation=bone.localRotation;
                    Assert.True(fixture.Backend.TryEvaluateClipPose(MxAnimationLayerId.Base,attackKey,0.8d));
                    var after = fixture.Backend.CapturePlaybackState().Layers[0].Slots[0];
                    var weightsAfter = FindLayer(fixture.Backend.CreateSnapshot(),MxAnimationLayerId.Base);
                    Assert.AreEqual(before.TimeSeconds,after.TimeSeconds,1e-9);
                    Assert.AreEqual(before.PlaybackSpeed,after.PlaybackSpeed);
                    Assert.AreEqual(weightsBefore.OutgoingWeight,weightsAfter.OutgoingWeight,1e-6f);
                    Assert.AreEqual(weightsBefore.ActivePlayableCount,weightsAfter.ActivePlayableCount);
                    Assert.True(animator.fireEvents);
                    bone.localPosition=savedPosition; bone.localRotation=savedRotation;
                    Assert.False(fixture.Backend.TryEvaluateClipPose(MxAnimationLayerId.Base,idleKey,0.8d));
                    Assert.False(fixture.Backend.TryEvaluateClipPose(MxAnimationLayerId.Base,attackKey,double.NaN));
                    Assert.True(fixture.Backend.TrySynchronizeCurrentClipTime(MxAnimationLayerId.Base,attackKey,0.5d));
                    fixture.Backend.Tick(0.12f);
                    Assert.AreEqual(0.5d,bone.localPosition.x,0.0001d);
                }
            }
            finally { Object.DestroyImmediate(idle); Object.DestroyImmediate(attack); }
        }

        [Test]
        public void SynchronizedClipTime_DoesNotAdvanceWithRenderDelta_AndPreservesCrossFade()
        {
            ResourceKey idleKey = ClipKey("demo.animation.clock.idle");
            ResourceKey attackKey = ClipKey("demo.animation.clock.attack");
            AnimationClip idle = CreateClip("clock.idle");
            AnimationClip attack = CreateClip("clock.attack");
            attack.SetCurve("bone", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var provider = new MemoryResourceProvider().Register("clock/idle", idle).Register("clock/attack", attack);
            ResourceManager manager = CreateManager(provider, Entry(idleKey, "clock/idle"), Entry(attackKey, "clock/attack"));
            try
            {
                using (BackendFixture fixture = BackendFixture.Create(manager, EmptyDefinition()))
                {
                    Transform bone = new GameObject("bone").transform;
                    bone.SetParent(fixture.Actor.transform, false);
                    Assert.IsTrue(fixture.Backend.Play(new MxAnimationPlayRequest { ClipKey = idleKey }).Success);
                    Assert.IsTrue(fixture.Backend.CrossFade(new MxAnimationCrossFadeRequest {
                        ClipKey = attackKey, PlaybackSpeed = 1.5f, FadeDurationSeconds = 0.12f }).Success);
                    Assert.IsTrue(fixture.Backend.TrySynchronizeCurrentClipTime(MxAnimationLayerId.Base, attackKey, 0d));
                    fixture.Backend.Tick(0.04f);
                    var fading = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                    Assert.AreEqual(2, fading.ActivePlayableCount);
                    Assert.Greater(fading.OutgoingWeight, 0f);
                    Assert.AreEqual(0d, fixture.Backend.CapturePlaybackState().Layers[0].Slots[0].TimeSeconds, 1e-9);

                    double[] times = { 0.4d, 0.4d, 0.55d, 0.2d };
                    float[] renderDeltas = { 0.1f, 1f / 144f, 0.25f, 0f };
                    for (int i = 0; i < times.Length; i++)
                    {
                        Assert.IsTrue(fixture.Backend.TrySynchronizeCurrentClipTime(MxAnimationLayerId.Base, attackKey, times[i]));
                        fixture.Backend.Tick(renderDeltas[i]);
                        var current = fixture.Backend.CapturePlaybackState().Layers[0].Slots[0];
                        Assert.AreEqual(times[i], current.TimeSeconds, 1e-9, "No render-delta drift, including zero-step, catch-up and rewind.");
                        Assert.AreEqual(1.5f, current.PlaybackSpeed);
                        Assert.AreEqual(times[i], bone.localPosition.x, 0.0001d, "The evaluated bone must match the anchored clip time.");
                    }
                    Assert.IsFalse(fixture.Backend.TrySynchronizeCurrentClipTime(MxAnimationLayerId.Base, idleKey, 0d));
                    Assert.IsFalse(fixture.Backend.TrySynchronizeCurrentClipTime(MxAnimationLayerId.Base, attackKey, double.NaN));
                    fixture.Backend.Tick(0.1f);
                    Assert.AreEqual(0.35d, fixture.Backend.CapturePlaybackState().Layers[0].Slots[0].TimeSeconds, 0.0001d,
                        "A one-tick anchor must restore ordinary playback speed afterwards.");
                }
            }
            finally
            {
                Object.DestroyImmediate(idle);
                Object.DestroyImmediate(attack);
            }
        }

        [Test]
        public void PlayableGraphLifecycle_ConstructsManualGraphAndDestroysIt()
        {
            var actor = new GameObject("PlayableGraphLifecycleTestActor");
            MxAnimationPlayableGraphRuntime runtime = null;
            try
            {
                Animator animator = actor.AddComponent<Animator>();
                runtime = new MxAnimationPlayableGraphRuntime(animator, "MxAnimation.Tests.GraphLifecycle");
                IMxAnimationPlayableGraphLifecycle lifecycle = runtime;

                Assert.IsTrue(lifecycle.IsGraphValid);
                Assert.AreEqual(0, lifecycle.LayerCount);

                lifecycle.Evaluate(0f);
                lifecycle.Destroy();

                Assert.IsFalse(lifecycle.IsGraphValid);
                Assert.AreEqual(0, lifecycle.LayerCount);
            }
            finally
            {
                runtime?.Destroy();
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void ClipPlayableFactory_CreatesConfiguredPlayable()
        {
            var actor = new GameObject("ClipPlayableFactoryTestActor");
            AnimationClip clip = CreateClip("factory.clip");
            MxAnimationPlayableGraphRuntime runtime = null;
            try
            {
                runtime = new MxAnimationPlayableGraphRuntime(actor.AddComponent<Animator>(), "MxAnimation.Tests.ClipFactory");
                IMxAnimationClipPlayableFactory factory = runtime;

                AnimationClipPlayable playable = factory.CreateClipPlayable(clip, 1.5f, 0.25f);

                Assert.IsTrue(playable.IsValid());
                Assert.AreEqual(1.5, playable.GetSpeed(), 0.0001);
                Assert.AreEqual(0.25, playable.GetTime(), 0.0001);

                factory.DestroyClipPlayable(playable);
            }
            finally
            {
                runtime?.Destroy();
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void LayerMixerDriver_AddsLayerAndAppliesWeight()
        {
            var actor = new GameObject("LayerMixerDriverTestActor");
            MxAnimationPlayableGraphRuntime runtime = null;
            try
            {
                runtime = new MxAnimationPlayableGraphRuntime(actor.AddComponent<Animator>(), "MxAnimation.Tests.LayerMixer");
                IMxAnimationLayerMixerDriver driver = runtime;

                MxAnimationLayerMixerHandle layer = driver.AddLayerMixer();
                driver.SetLayerWeight(layer, 0.25f);
                driver.SetLayerAdditive(layer, true);

                Assert.IsTrue(layer.IsValid);
                Assert.AreEqual(1, runtime.LayerCount);
                Assert.AreEqual(0.25f, runtime.RootMixer.GetInputWeight(layer.RootInputIndex), 0.0001f);
            }
            finally
            {
                runtime?.Destroy();
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void BlendMixerDriver_ConnectsWeightedClipInputs()
        {
            var actor = new GameObject("BlendMixerDriverTestActor");
            AnimationClip idle = CreateClip("blend.idle");
            AnimationClip run = CreateClip("blend.run");
            MxAnimationPlayableGraphRuntime runtime = null;
            try
            {
                runtime = new MxAnimationPlayableGraphRuntime(actor.AddComponent<Animator>(), "MxAnimation.Tests.BlendMixer");
                IMxAnimationLayerMixerDriver layerDriver = runtime;
                IMxAnimationClipPlayableFactory clipFactory = runtime;
                IMxAnimationBlendMixerDriver blendDriver = runtime;
                MxAnimationLayerMixerHandle layer = layerDriver.AddLayerMixer();
                AnimationClipPlayable idlePlayable = clipFactory.CreateClipPlayable(idle, 1f, 0f);
                AnimationClipPlayable runPlayable = clipFactory.CreateClipPlayable(run, 1f, 0f);

                int idleInput = blendDriver.ConnectClip(layer, idlePlayable, 0.25f);
                int runInput = blendDriver.ConnectClip(layer, runPlayable, 0.75f);

                Assert.AreEqual(0.25f, layer.Mixer.GetInputWeight(idleInput), 0.0001f);
                Assert.AreEqual(0.75f, layer.Mixer.GetInputWeight(runInput), 0.0001f);

                blendDriver.SetClipWeight(layer, runInput, 0.5f);
                Assert.AreEqual(0.5f, layer.Mixer.GetInputWeight(runInput), 0.0001f);

                blendDriver.DisconnectClip(layer, idleInput);
                Assert.AreEqual(0.5f, layer.Mixer.GetInputWeight(runInput), 0.0001f);
            }
            finally
            {
                runtime?.Destroy();
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(idle);
                Object.DestroyImmediate(run);
            }
        }

        [Test]
        public void PlayableDiagnostics_TrimsRequestsAndResourceErrors()
        {
            var diagnostics = new MxAnimationPlayableDiagnosticBuffer(2);
            diagnostics.AddRequest(new MxAnimationRequestDiagnostic(
                MxAnimationRequestKind.Play,
                MxAnimationLayerId.Base,
                default,
                default,
                false,
                MxAnimationBackendResultCode.Success,
                "request:1",
                "first"));
            diagnostics.AddRequest(new MxAnimationRequestDiagnostic(
                MxAnimationRequestKind.Stop,
                MxAnimationLayerId.Base,
                default,
                default,
                false,
                MxAnimationBackendResultCode.Success,
                "request:2",
                "second"));
            diagnostics.AddRequest(new MxAnimationRequestDiagnostic(
                MxAnimationRequestKind.CrossFade,
                MxAnimationLayerId.Base,
                default,
                default,
                false,
                MxAnimationBackendResultCode.Success,
                "request:3",
                "third"));

            diagnostics.TrackResourceError(ResourceError.None);
            diagnostics.TrackResourceError(new ResourceError(ResourceErrorCode.NotFound, ClipKey("missing.a"), "memory", "missing a"));
            diagnostics.TrackResourceError(new ResourceError(ResourceErrorCode.TypeMismatch, ClipKey("missing.b"), "memory", "missing b"));
            diagnostics.TrackResourceError(new ResourceError(ResourceErrorCode.ProviderFailed, ClipKey("missing.c"), "memory", "missing c"));

            Assert.AreEqual(2, diagnostics.RecentRequests.Count);
            Assert.AreEqual(MxAnimationRequestKind.Stop, diagnostics.RecentRequests[0].Kind);
            Assert.AreEqual(MxAnimationRequestKind.CrossFade, diagnostics.RecentRequests[1].Kind);
            Assert.AreEqual(2, diagnostics.RecentResourceErrors.Count);
            Assert.AreEqual(ResourceErrorCode.TypeMismatch, diagnostics.RecentResourceErrors[0].Code);
            Assert.AreEqual(ResourceErrorCode.ProviderFailed, diagnostics.RecentResourceErrors[1].Code);
        }

        [Test]
        public void TryGetClipDuration_LoadsAndReleasesTemporaryHandle()
        {
            ResourceKey clipKey = ClipKey("demo.animation.duration");
            AnimationClip clip = CreateClip("duration");
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "m_LocalPosition.x",
                AnimationCurve.Linear(0f, 0f, 2f, 1f));
            var provider = new MemoryResourceProvider().Register("clips/duration", clip);
            ResourceManager manager = CreateManager(provider, Entry(clipKey, "clips/duration"));

            using (BackendFixture fixture = BackendFixture.Create(manager, EmptyDefinition()))
            {
                Assert.IsTrue(fixture.Backend.TryGetClipDuration(clipKey, out float duration));
                Assert.AreEqual(2f, duration, 0.0001f);
                Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(1, provider.ReleaseCount);
            }

            Object.DestroyImmediate(clip);
        }

        [Test]
        public void Blend2D_LoopingSlotsShareNormalizedPhaseAcrossDifferentDurations()
        {
            ResourceKey walkKey = ClipKey("demo.animation.walk.phase");
            ResourceKey runKey = ClipKey("demo.animation.run.phase");
            AnimationClip walk = CreateClip("walk.phase");
            AnimationClip run = CreateClip("run.phase");
            walk.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            run.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 0.5f, 1f));
            var provider = new MemoryResourceProvider()
                .Register("clips/walk-phase", walk)
                .Register("clips/run-phase", run);
            ResourceManager manager = CreateManager(
                provider,
                Entry(walkKey, "clips/walk-phase"),
                Entry(runKey, "clips/run-phase"));
            var blend = new MxAnimationBlend2DDefinition(
                "blend.phase",
                "moveX",
                "moveY",
                MxAnimationLayerId.Base,
                new[]
                {
                    new MxAnimationBlend2DPoint(0, 500, walkKey),
                    new MxAnimationBlend2DPoint(0, 1000, runKey)
                });
            var definition = new MxAnimationSetDefinition(
                "set.phase",
                1,
                default,
                default,
                blend2DDefinitions: new[] { blend });

            using (BackendFixture fixture = BackendFixture.Create(manager, definition))
            {
                Assert.IsTrue(fixture.Backend.SetBlend2D(new MxAnimationBlend2DRequest
                {
                    BlendId = "blend.phase",
                    ParameterX = new MxAnimationQuantizedParameter("moveX", 0),
                    ParameterY = new MxAnimationQuantizedParameter("moveY", 750)
                }).Success);
                fixture.Backend.Tick(0.25f);
                fixture.Backend.Tick(0.25f);

                MxAnimationLayerDiagnostic layer = FindLayer(
                    fixture.Backend.CreateSnapshot(),
                    MxAnimationLayerId.Base);
                MxAnimationClipPlaybackDiagnostic walkPlayback = FindPlayback(layer, walkKey, blendSlot: true);
                MxAnimationClipPlaybackDiagnostic runPlayback = FindPlayback(layer, runKey, blendSlot: true);
                Assert.AreEqual(
                    walkPlayback.NormalizedTime,
                    runPlayback.NormalizedTime,
                    0.001f);
            }

            Object.DestroyImmediate(walk);
            Object.DestroyImmediate(run);
        }

        [Test]
        public void PlayStop_ReleasesNonResidentHandle()
        {
            ResourceKey idleKey = ClipKey("demo.animation.idle");
            AnimationClip idle = CreateClip("idle");
            var provider = new MemoryResourceProvider().Register("clips/idle", idle);
            ResourceManager manager = CreateManager(provider, Entry(idleKey, "clips/idle"));

            using (BackendFixture fixture = BackendFixture.Create(manager, EmptyDefinition()))
            {
                MxAnimationBackendResult play = fixture.Backend.Play(new MxAnimationPlayRequest { ClipKey = idleKey });

                Assert.IsTrue(play.Success, play.Message);
                MxAnimationLayerDiagnostic playing = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                Assert.AreEqual(MxAnimationLayerStatus.Playing, playing.Status);
                Assert.AreEqual(idleKey, playing.CurrentClipKey);
                Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);

                MxAnimationBackendResult stop = fixture.Backend.Stop(new MxAnimationStopRequest { LayerId = MxAnimationLayerId.Base });

                Assert.IsTrue(stop.Success, stop.Message);
                MxAnimationLayerDiagnostic stopped = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                Assert.AreEqual(MxAnimationLayerStatus.Stopped, stopped.Status);
                Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(1, provider.ReleaseCount);
            }

            Object.DestroyImmediate(idle);
        }

        [Test]
        public void Play_WhenSameClipAlreadyCurrent_ReusesPlayableAndReportsCacheHit()
        {
            ResourceKey idleKey = ClipKey("demo.animation.idle");
            AnimationClip idle = CreateClip("idle");
            var provider = new MemoryResourceProvider().Register("clips/idle", idle);
            ResourceManager manager = CreateManager(provider, Entry(idleKey, "clips/idle"));

            using (BackendFixture fixture = BackendFixture.Create(manager, EmptyDefinition()))
            {
                MxAnimationBackendResult first = fixture.Backend.Play(new MxAnimationPlayRequest { ClipKey = idleKey });
                MxAnimationBackendResult second = fixture.Backend.Play(new MxAnimationPlayRequest
                {
                    ClipKey = idleKey,
                    PlaybackSpeed = 1.25f,
                    StartOffsetSeconds = 0.2f
                });

                Assert.IsTrue(first.Success, first.Message);
                Assert.IsTrue(second.Success, second.Message);
                Assert.AreEqual(1, provider.LoadCount);
                Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);

                MxAnimationDiagnosticSnapshot snapshot = fixture.Backend.CreateSnapshot();
                MxAnimationLayerDiagnostic layer = FindLayer(snapshot, MxAnimationLayerId.Base);
                Assert.AreEqual(MxAnimationLayerStatus.Playing, layer.Status);
                Assert.AreEqual(idleKey, layer.CurrentClipKey);
                Assert.AreEqual(1, layer.ActivePlayableCount);
                Assert.AreEqual(1, snapshot.Cache.CacheHitCount);
                Assert.AreEqual(1, snapshot.Cache.CacheMissCount);
                Assert.AreEqual(1, snapshot.Cache.CachedPlayableCount);
                Assert.AreEqual(1, snapshot.Cache.ActivePlayableCount);
                Assert.AreEqual(1, snapshot.Cache.ResourceLoadedCount);
                Assert.AreEqual(1, snapshot.Cache.ResourceRefCount);
            }

            Object.DestroyImmediate(idle);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CrossFade_WhenFallbackIsDisabled_RejectsMissingReferenceOrResource(bool unresolvedBinding)
        {
            ResourceKey missing = ClipKey("demo.animation.missing");
            ResourceKey fallbackKey = ClipKey("demo.animation.fallback");
            AnimationClip fallback = CreateClip("fallback");
            var provider = new MemoryResourceProvider().Register("clips/fallback", fallback);
            ResourceManager manager = CreateManager(provider,
                Entry(missing, "clips/missing"), Entry(fallbackKey, "clips/fallback"));
            var definition = new MxAnimationSetDefinition("demo.set", 1, fallbackKey, fallbackKey);
            try
            {
                using (BackendFixture fixture = BackendFixture.Create(manager, definition))
                {
                    var result = fixture.Backend.CrossFade(new MxAnimationCrossFadeRequest {
                        ActionKey = unresolvedBinding ? "unbound.death" : string.Empty,
                        ClipKey = unresolvedBinding ? default : missing,
                        AllowFallback = false });
                    Assert.IsFalse(result.Success);
                    Assert.AreNotEqual(fallbackKey, result.ClipKey);
                    foreach (var layer in fixture.Backend.CreateSnapshot().LayerStates)
                    {
                        Assert.IsFalse(layer.CurrentClipIsFallback);
                        Assert.AreNotEqual(fallbackKey, layer.CurrentClipKey);
                    }
                }
            }
            finally { Object.DestroyImmediate(fallback); }
        }

        [Test]
        public void CrossFade_RequiredQueuedClipFailureLogsErrorAndDoesNotPlayFallback()
        {
            ResourceKey missing = ClipKey("demo.animation.required.missing");
            ResourceKey fallbackKey = ClipKey("demo.animation.fallback");
            AnimationClip fallback = CreateClip("fallback");
            var manager = new DelayedResourceManager(missing)
                .RegisterProvider(new MemoryResourceProvider().Register("clips/fallback", fallback))
                .AddCatalog(new ResourceCatalog("animation.tests", string.Empty, new[] {
                    Entry(missing, "clips/missing"), Entry(fallbackKey, "clips/fallback") }));
            try
            {
                using (var fixture = BackendFixture.Create(manager,
                    new MxAnimationSetDefinition("required.test", 1, fallbackKey, fallbackKey)))
                {
                    var result = fixture.Backend.CrossFade(new MxAnimationCrossFadeRequest {
                        ClipKey = missing, AllowFallback = false });
                    Assert.AreEqual(MxAnimationBackendResultCode.Queued, result.Code);
                    manager.LastOperation.Complete();
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                        new System.Text.RegularExpressions.Regex("\\[MxAnimation\\]\\[RequiredClipMissing\\]"));
                    fixture.Backend.Tick(0.1f);
                    var layer = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                    Assert.AreEqual(MxAnimationLayerStatus.Failed, layer.Status);
                    Assert.IsFalse(layer.CurrentClipIsFallback);
                    Assert.AreNotEqual(fallbackKey, layer.CurrentClipKey);
                }
            }
            finally { Object.DestroyImmediate(fallback); }
        }

        [Test]
        public void Play_WhenRequestedClipFails_UsesResidentFallbackAndReportsDiagnostics()
        {
            ResourceKey requestedKey = ClipKey("demo.animation.missing");
            ResourceKey fallbackKey = ClipKey("demo.animation.fallback");
            AnimationClip fallback = CreateClip("fallback");
            var provider = new MemoryResourceProvider().Register("clips/fallback", fallback);
            ResourceManager manager = CreateManager(
                provider,
                Entry(requestedKey, "clips/missing"),
                Entry(fallbackKey, "clips/fallback"));
            var definition = new MxAnimationSetDefinition("demo.set", 1, default, fallbackKey);

            using (BackendFixture fixture = BackendFixture.Create(manager, definition))
            {
                Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(MxAnimationResourceLoadStatus.Loaded, fixture.Backend.CreateSnapshot().FallbackClip.Status);

                MxAnimationBackendResult play = fixture.Backend.Play(new MxAnimationPlayRequest { ClipKey = requestedKey, CorrelationId = "req-1" });

                Assert.IsTrue(play.Success, play.Message);
                Assert.AreEqual(fallbackKey, play.ClipKey);
                MxAnimationDiagnosticSnapshot snapshot = fixture.Backend.CreateSnapshot();
                MxAnimationLayerDiagnostic layer = FindLayer(snapshot, MxAnimationLayerId.Base);
                Assert.AreEqual(MxAnimationLayerStatus.Playing, layer.Status);
                Assert.AreEqual(fallbackKey, layer.CurrentClipKey);
                Assert.IsTrue(layer.CurrentClipIsFallback);
                Assert.AreEqual(1, snapshot.RecentResourceErrors.Count);
                Assert.AreEqual(ResourceErrorCode.NotFound, snapshot.RecentResourceErrors[0].Code);
                Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);

                fixture.Backend.Stop(new MxAnimationStopRequest());
                Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);
            }

            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(1, provider.ReleaseCount);
            Object.DestroyImmediate(fallback);
        }

        [Test]
        public void CrossFade_KeepsOutgoingHandleUntilFadeCompletes()
        {
            ResourceKey idleKey = ClipKey("demo.animation.idle");
            ResourceKey runKey = ClipKey("demo.animation.run");
            AnimationClip idle = CreateClip("idle");
            AnimationClip run = CreateClip("run");
            var provider = new MemoryResourceProvider()
                .Register("clips/idle", idle)
                .Register("clips/run", run);
            ResourceManager manager = CreateManager(
                provider,
                Entry(idleKey, "clips/idle"),
                Entry(runKey, "clips/run"));

            using (BackendFixture fixture = BackendFixture.Create(manager, EmptyDefinition()))
            {
                fixture.Backend.Play(new MxAnimationPlayRequest { ClipKey = idleKey });
                Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);

                MxAnimationBackendResult crossFade = fixture.Backend.CrossFade(new MxAnimationCrossFadeRequest
                {
                    ClipKey = runKey,
                    FadeDurationSeconds = 1f
                });

                Assert.IsTrue(crossFade.Success, crossFade.Message);
                Assert.AreEqual(2, manager.CreateDebugSnapshot().LoadedCount);
                MxAnimationLayerDiagnostic fading = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                Assert.AreEqual(MxAnimationLayerStatus.CrossFading, fading.Status);
                Assert.AreEqual(2, fading.ActivePlayableCount);

                fixture.Backend.Tick(0.5f);
                Assert.AreEqual(2, manager.CreateDebugSnapshot().LoadedCount);
                MxAnimationLayerDiagnostic midFade = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                Assert.AreEqual(2, midFade.ActivePlayableCount);
                Assert.Greater(midFade.OutgoingWeight, 0f);

                fixture.Backend.Tick(0.5f);
                Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);
                MxAnimationLayerDiagnostic complete = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                Assert.AreEqual(MxAnimationLayerStatus.Playing, complete.Status);
                Assert.AreEqual(runKey, complete.CurrentClipKey);
                Assert.AreEqual(1, complete.ActivePlayableCount);
                Assert.AreEqual(1, provider.ReleaseCount);
            }

            Object.DestroyImmediate(idle);
            Object.DestroyImmediate(run);
        }

        [Test]
        public void PlaybackState_RestoresCrossFadeTimeWeightsAndBonePose()
        {
            ResourceKey idleKey = ClipKey("demo.animation.rewind.idle");
            ResourceKey attackKey = ClipKey("demo.animation.rewind.attack");
            AnimationClip idle = CreateClip("rewind.idle");
            AnimationClip attack = CreateClip("rewind.attack");
            idle.SetCurve(
                "bone",
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            attack.SetCurve(
                "bone",
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Linear(0f, 2f, 1f, 3f));
            var provider = new MemoryResourceProvider()
                .Register("clips/rewind-idle", idle)
                .Register("clips/rewind-attack", attack);
            ResourceManager manager = CreateManager(
                provider,
                Entry(idleKey, "clips/rewind-idle"),
                Entry(attackKey, "clips/rewind-attack"));

            using (BackendFixture fixture = BackendFixture.Create(manager, EmptyDefinition()))
            {
                Transform bone = new GameObject("bone").transform;
                bone.SetParent(fixture.Actor.transform, worldPositionStays: false);
                Assert.IsTrue(fixture.Backend.Play(new MxAnimationPlayRequest
                {
                    ClipKey = idleKey
                }).Success);
                fixture.Backend.Tick(0.2f);
                Assert.IsTrue(fixture.Backend.CrossFade(new MxAnimationCrossFadeRequest
                {
                    ClipKey = attackKey,
                    FadeDurationSeconds = 1f
                }).Success);
                Assert.IsTrue(fixture.Backend.SetLayerWeight(new MxAnimationLayerWeightRequest
                {
                    LayerId = MxAnimationLayerId.Base,
                    Weight = 0.4f,
                    FadeDurationSeconds = 1f,
                    CorrelationId = "rewind-layer-fade"
                }).Success);
                fixture.Backend.Tick(0.35f);

                MxAnimationPlaybackState expected = fixture.Backend.CapturePlaybackState();
                Vector3 expectedBonePosition = bone.localPosition;
                fixture.Backend.Tick(0.3f);
                Assert.That(bone.localPosition.x, Is.Not.EqualTo(expectedBonePosition.x).Within(0.0001f));

                MxAnimationBackendResult restored = fixture.Backend.RestorePlaybackState(expected);
                MxAnimationPlaybackState actual = fixture.Backend.CapturePlaybackState();

                Assert.IsTrue(restored.Success, restored.Message);
                Assert.AreEqual(expectedBonePosition.x, bone.localPosition.x, 0.0001f);
                Assert.AreEqual(expected.Layers.Count, actual.Layers.Count);
                MxAnimationLayerPlaybackState expectedLayer = expected.Layers[0];
                MxAnimationLayerPlaybackState actualLayer = actual.Layers[0];
                Assert.AreEqual(expectedLayer.Status, actualLayer.Status);
                Assert.AreEqual(expectedLayer.Weight, actualLayer.Weight, 0.0001f);
                Assert.AreEqual(expectedLayer.StartWeight, actualLayer.StartWeight, 0.0001f);
                Assert.AreEqual(expectedLayer.TargetWeight, actualLayer.TargetWeight, 0.0001f);
                Assert.AreEqual(
                    expectedLayer.WeightTransitionElapsedSeconds,
                    actualLayer.WeightTransitionElapsedSeconds,
                    0.0001f);
                Assert.AreEqual(
                    expectedLayer.WeightTransitionDurationSeconds,
                    actualLayer.WeightTransitionDurationSeconds,
                    0.0001f);
                Assert.AreEqual(expectedLayer.Slots.Count, actualLayer.Slots.Count);
                for (int i = 0; i < expectedLayer.Slots.Count; i++)
                {
                    MxAnimationPlaybackSlotState left = expectedLayer.Slots[i];
                    MxAnimationPlaybackSlotState right = actualLayer.Slots[i];
                    Assert.AreEqual(left.Role, right.Role);
                    Assert.AreEqual(left.ClipKey, right.ClipKey);
                    Assert.AreEqual(left.TimeSeconds, right.TimeSeconds, 0.000001d);
                    Assert.AreEqual(left.PlaybackSpeed, right.PlaybackSpeed, 0.0001f);
                    Assert.AreEqual(left.Weight, right.Weight, 0.0001f);
                    Assert.AreEqual(left.TargetWeight, right.TargetWeight, 0.0001f);
                    Assert.AreEqual(left.FadeElapsedSeconds, right.FadeElapsedSeconds, 0.0001f);
                    Assert.AreEqual(left.FadeDurationSeconds, right.FadeDurationSeconds, 0.0001f);
                }
            }

            Object.DestroyImmediate(idle);
            Object.DestroyImmediate(attack);
        }

        [Test]
        public void PlaybackState_RejectsDuplicateCurrentSlotWithoutMutatingGraph()
        {
            ResourceKey idleKey = ClipKey("demo.animation.rewind.invalid");
            AnimationClip idle = CreateClip("rewind.invalid");
            var provider = new MemoryResourceProvider().Register("clips/rewind-invalid", idle);
            ResourceManager manager = CreateManager(provider, Entry(idleKey, "clips/rewind-invalid"));

            using (BackendFixture fixture = BackendFixture.Create(manager, EmptyDefinition()))
            {
                Assert.IsTrue(fixture.Backend.Play(new MxAnimationPlayRequest
                {
                    ClipKey = idleKey
                }).Success);
                fixture.Backend.Tick(0.25f);
                MxAnimationPlaybackState before = fixture.Backend.CapturePlaybackState();
                MxAnimationLayerPlaybackState sourceLayer = before.Layers[0];
                MxAnimationPlaybackSlotState current = sourceLayer.Slots[0];
                var invalidLayer = new MxAnimationLayerPlaybackState(
                    sourceLayer.LayerId,
                    sourceLayer.Status,
                    sourceLayer.Weight,
                    sourceLayer.StartWeight,
                    sourceLayer.TargetWeight,
                    sourceLayer.WeightTransitionElapsedSeconds,
                    sourceLayer.WeightTransitionDurationSeconds,
                    sourceLayer.WeightTransitionPolicyId,
                    sourceLayer.WeightTransitionCorrelationId,
                    sourceLayer.ActiveBlendKind,
                    sourceLayer.ActiveBlendId,
                    sourceLayer.BlendParameter,
                    sourceLayer.Blend2DParameterX,
                    sourceLayer.Blend2DParameterY,
                    sourceLayer.BlendFadeElapsedSeconds,
                    sourceLayer.BlendFadeDurationSeconds,
                    new[] { current, current },
                    sourceLayer.Blend1DWeights,
                    sourceLayer.Blend2DWeights);
                var invalid = new MxAnimationPlaybackState(
                    before.BackendName,
                    before.ActorId,
                    before.AnimationSetId,
                    new[] { invalidLayer });

                MxAnimationBackendResult result = fixture.Backend.RestorePlaybackState(invalid);
                MxAnimationPlaybackState after = fixture.Backend.CapturePlaybackState();

                Assert.IsFalse(result.Success);
                Assert.AreEqual(MxAnimationBackendResultCode.InvalidRequest, result.Code);
                Assert.AreEqual(before.Layers[0].Slots.Count, after.Layers[0].Slots.Count);
                Assert.AreEqual(
                    before.Layers[0].Slots[0].TimeSeconds,
                    after.Layers[0].Slots[0].TimeSeconds,
                    0.000001d);
            }

            Object.DestroyImmediate(idle);
        }

        [Test]
        public void CrossFade_WhenSameClipAlreadyCurrent_RestartsAtRequestedOffset()
        {
            ResourceKey attackKey = ClipKey("demo.animation.attack");
            AnimationClip attack = CreateClip("attack");
            attack.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var provider = new MemoryResourceProvider().Register("clips/attack", attack);
            ResourceManager manager = CreateManager(provider, Entry(attackKey, "clips/attack"));

            using (BackendFixture fixture = BackendFixture.Create(manager, EmptyDefinition()))
            {
                MxAnimationBackendResult first = fixture.Backend.CrossFade(new MxAnimationCrossFadeRequest
                {
                    ClipKey = attackKey,
                    FadeDurationSeconds = 0f
                });
                Assert.IsTrue(first.Success, first.Message);
                fixture.Backend.Tick(0f);
                fixture.Backend.Tick(0.5f);
                MxAnimationLayerDiagnostic advanced = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                Assert.Greater(FindCurrentPlayback(advanced, attackKey).NormalizedTime, 0.25f);

                MxAnimationBackendResult restart = fixture.Backend.CrossFade(new MxAnimationCrossFadeRequest
                {
                    ClipKey = attackKey,
                    FadeDurationSeconds = 0f,
                    TargetStartOffsetSeconds = 0f
                });
                Assert.IsTrue(restart.Success, restart.Message);
                fixture.Backend.Tick(0f);

                MxAnimationLayerDiagnostic restarted = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                Assert.AreEqual(0f, FindCurrentPlayback(restarted, attackKey).NormalizedTime, 0.0001f);
            }

            Object.DestroyImmediate(attack);
        }

        [Test]
        public void SetLayerWeight_UpdatesRootLayerWeightAndKeepsClipFadeDiagnostics()
        {
            ResourceKey idleKey = ClipKey("demo.animation.idle");
            ResourceKey attackKey = ClipKey("demo.animation.attack");
            MxAnimationLayerId upperBody = new MxAnimationLayerId("upper_body");
            AnimationClip idle = CreateClip("idle");
            AnimationClip attack = CreateClip("attack");
            var provider = new MemoryResourceProvider()
                .Register("clips/idle", idle)
                .Register("clips/attack", attack);
            ResourceManager manager = CreateManager(
                provider,
                Entry(idleKey, "clips/idle"),
                Entry(attackKey, "clips/attack"));
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                default,
                default,
                layers: new[]
                {
                    new MxAnimationLayerDefinition(MxAnimationLayerId.Base, defaultWeight: 1f),
                    new MxAnimationLayerDefinition(upperBody, "humanoid.upper", 0.25f)
                });

            using (BackendFixture fixture = BackendFixture.Create(manager, definition))
            {
                fixture.Backend.Play(new MxAnimationPlayRequest { ClipKey = idleKey, LayerId = MxAnimationLayerId.Base });
                fixture.Backend.Play(new MxAnimationPlayRequest { ClipKey = attackKey, LayerId = upperBody });

                MxAnimationLayerDiagnostic initialUpper = FindLayer(fixture.Backend.CreateSnapshot(), upperBody);
                Assert.AreEqual(0.25f, initialUpper.LayerWeight);
                Assert.AreEqual(0.25f, initialUpper.TargetLayerWeight);
                Assert.AreEqual("humanoid.upper", initialUpper.LayerProfileId);

                MxAnimationBackendResult clamp = fixture.Backend.SetLayerWeight(new MxAnimationLayerWeightRequest
                {
                    LayerId = upperBody,
                    Weight = float.NaN,
                    CorrelationId = "layer:clamp"
                });
                Assert.IsTrue(clamp.Success, clamp.Message);
                Assert.AreEqual(0f, FindLayer(fixture.Backend.CreateSnapshot(), upperBody).LayerWeight);

                MxAnimationBackendResult fade = fixture.Backend.SetLayerWeight(new MxAnimationLayerWeightRequest
                {
                    LayerId = upperBody,
                    Weight = 1f,
                    FadeDurationSeconds = 0.5f,
                    TransitionPolicyId = "upper.fade_in",
                    CorrelationId = "layer:fade"
                });
                Assert.IsTrue(fade.Success, fade.Message);

                fixture.Backend.Tick(0.25f);

                MxAnimationLayerDiagnostic midFade = FindLayer(fixture.Backend.CreateSnapshot(), upperBody);
                Assert.Greater(midFade.LayerWeight, 0f);
                Assert.Less(midFade.LayerWeight, 1f);
                Assert.AreEqual(1f, midFade.TargetLayerWeight);
                Assert.IsTrue(midFade.LayerSyncState.IsTransitioning);
                Assert.AreEqual("upper.fade_in", midFade.LayerSyncState.TransitionPolicyId);
                Assert.AreEqual(1, midFade.ActivePlayableCount);
                Assert.AreEqual(attackKey, midFade.CurrentClipKey);
            }

            Object.DestroyImmediate(idle);
            Object.DestroyImmediate(attack);
        }

        [Test]
        public void SetBlend1D_LoadsWeightedClipsAndCoexistsWithUpperBodyLayer()
        {
            ResourceKey idleKey = ClipKey("demo.animation.idle");
            ResourceKey walkKey = ClipKey("demo.animation.walk");
            ResourceKey runKey = ClipKey("demo.animation.run");
            ResourceKey attackKey = ClipKey("demo.animation.attack");
            MxAnimationLayerId upperBody = new MxAnimationLayerId("upper_body");
            AnimationClip idle = CreateClip("idle");
            AnimationClip walk = CreateClip("walk");
            AnimationClip run = CreateClip("run");
            AnimationClip attack = CreateClip("attack");
            var provider = new MemoryResourceProvider()
                .Register("clips/idle", idle)
                .Register("clips/walk", walk)
                .Register("clips/run", run)
                .Register("clips/attack", attack);
            ResourceManager manager = CreateManager(
                provider,
                Entry(idleKey, "clips/idle"),
                Entry(walkKey, "clips/walk"),
                Entry(runKey, "clips/run"),
                Entry(attackKey, "clips/attack"));
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idleKey,
                idleKey,
                new[]
                {
                    new MxAnimationActionBinding(CharacterTestNumericIds.AnimationBindingAttack, "action:attack", attackKey, upperBody)
                },
                layers: new[]
                {
                    new MxAnimationLayerDefinition(MxAnimationLayerId.Base, defaultWeight: 1f),
                    new MxAnimationLayerDefinition(upperBody, "humanoid.upper", 0f)
                },
                blend1DDefinitions: new[]
                {
                    new MxAnimationBlend1DDefinition(
                        "locomotion",
                        "locomotion.speed",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend1DPoint(0, idleKey),
                            new MxAnimationBlend1DPoint(500, walkKey),
                            new MxAnimationBlend1DPoint(1000, runKey)
                        })
                });

            using (BackendFixture fixture = BackendFixture.Create(manager, definition))
            {
                MxAnimationBackendResult blendResult = fixture.Backend.SetBlend1D(new MxAnimationBlend1DRequest
                {
                    BlendId = "locomotion",
                    Parameter = new MxAnimationQuantizedParameter("locomotion.speed", 750),
                    CorrelationId = "speed:750"
                });
                Assert.IsTrue(blendResult.Success, blendResult.Message);

                fixture.Backend.Play(new MxAnimationPlayRequest
                {
                    BindingId = CharacterTestBulkIds38.Id790002,
                    LayerId = upperBody
                });
                fixture.Backend.SetLayerWeight(new MxAnimationLayerWeightRequest
                {
                    LayerId = upperBody,
                    Weight = 1f,
                    FadeDurationSeconds = 0f
                });

                MxAnimationDiagnosticSnapshot snapshot = fixture.Backend.CreateSnapshot();
                MxAnimationLayerDiagnostic baseLayer = FindLayer(snapshot, MxAnimationLayerId.Base);
                MxAnimationLayerDiagnostic upper = FindLayer(snapshot, upperBody);

                Assert.AreEqual("locomotion", baseLayer.Blend1DId);
                Assert.AreEqual(2, baseLayer.ActivePlayableCount);
                Assert.AreEqual("locomotion.speed", baseLayer.BlendParameter.ParameterId);
                Assert.AreEqual(750, baseLayer.BlendParameter.QuantizedValue);
                Assert.AreEqual(3, baseLayer.Blend1DWeights.Count);
                Assert.AreEqual(0.5f, baseLayer.Blend1DWeights[1].Weight, 0.0001f);
                Assert.AreEqual(0.5f, baseLayer.Blend1DWeights[2].Weight, 0.0001f);
                Assert.AreEqual(attackKey, upper.CurrentClipKey);
                Assert.AreEqual(1f, upper.LayerWeight);
            }

            Object.DestroyImmediate(idle);
            Object.DestroyImmediate(walk);
            Object.DestroyImmediate(run);
            Object.DestroyImmediate(attack);
        }

        [Test]
        public void SetBlend1D_ReusesCachedBlendSlotsAcrossSpeedChanges()
        {
            ResourceKey idleKey = ClipKey("demo.animation.idle");
            ResourceKey walkKey = ClipKey("demo.animation.walk");
            ResourceKey runKey = ClipKey("demo.animation.run");
            AnimationClip idle = CreateClip("idle");
            AnimationClip walk = CreateClip("walk");
            AnimationClip run = CreateClip("run");
            var provider = new MemoryResourceProvider()
                .Register("clips/idle", idle)
                .Register("clips/walk", walk)
                .Register("clips/run", run);
            ResourceManager manager = CreateManager(
                provider,
                Entry(idleKey, "clips/idle"),
                Entry(walkKey, "clips/walk"),
                Entry(runKey, "clips/run"));
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                default,
                default,
                blend1DDefinitions: new[]
                {
                    new MxAnimationBlend1DDefinition(
                        "locomotion",
                        "locomotion.speed",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend1DPoint(0, idleKey),
                            new MxAnimationBlend1DPoint(500, walkKey),
                            new MxAnimationBlend1DPoint(1000, runKey)
                        })
                });

            using (BackendFixture fixture = BackendFixture.Create(manager, definition))
            {
                MxAnimationBackendResult fast = fixture.Backend.SetBlend1D(new MxAnimationBlend1DRequest
                {
                    BlendId = "locomotion",
                    Parameter = new MxAnimationQuantizedParameter("locomotion.speed", 750),
                    CorrelationId = "speed:750"
                });
                Assert.IsTrue(fast.Success, fast.Message);
                Assert.AreEqual(2, provider.LoadCount);
                Assert.AreEqual(2, manager.CreateDebugSnapshot().LoadedCount);

                MxAnimationBackendResult slow = fixture.Backend.SetBlend1D(new MxAnimationBlend1DRequest
                {
                    BlendId = "locomotion",
                    Parameter = new MxAnimationQuantizedParameter("locomotion.speed", 250),
                    CorrelationId = "speed:250"
                });
                Assert.IsTrue(slow.Success, slow.Message);
                Assert.AreEqual(3, provider.LoadCount);
                Assert.AreEqual(3, manager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(3, manager.CreateDebugSnapshot().TotalRefCount);

                MxAnimationDiagnosticSnapshot snapshot = fixture.Backend.CreateSnapshot();
                MxAnimationLayerDiagnostic baseLayer = FindLayer(snapshot, MxAnimationLayerId.Base);
                Assert.AreEqual("locomotion", baseLayer.Blend1DId);
                Assert.AreEqual(2, baseLayer.ActivePlayableCount);
                Assert.AreEqual(3, baseLayer.Blend1DWeights.Count);
                Assert.AreEqual(2, snapshot.Cache.CacheHitCount);
                Assert.AreEqual(3, snapshot.Cache.CacheMissCount);
                Assert.AreEqual(3, snapshot.Cache.CachedPlayableCount);
                Assert.AreEqual(2, snapshot.Cache.ActivePlayableCount);
                Assert.AreEqual(3, snapshot.Cache.ResourceLoadedCount);
                Assert.AreEqual(3, snapshot.Cache.ResourceRefCount);

                fixture.Backend.Stop(new MxAnimationStopRequest { LayerId = MxAnimationLayerId.Base });
                Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(3, provider.ReleaseCount);
            }

            Object.DestroyImmediate(idle);
            Object.DestroyImmediate(walk);
            Object.DestroyImmediate(run);
        }

        [Test]
        public void SetBlend2D_LoadsWeightedClipsAndReportsDiagnostics()
        {
            ResourceKey idleKey = ClipKey("demo.animation.idle");
            ResourceKey rightKey = ClipKey("demo.animation.right");
            ResourceKey upKey = ClipKey("demo.animation.up");
            ResourceKey diagonalKey = ClipKey("demo.animation.diagonal");
            AnimationClip idle = CreateClip("idle");
            AnimationClip right = CreateClip("right");
            AnimationClip up = CreateClip("up");
            AnimationClip diagonal = CreateClip("diagonal");
            var provider = new MemoryResourceProvider()
                .Register("clips/idle", idle)
                .Register("clips/right", right)
                .Register("clips/up", up)
                .Register("clips/diagonal", diagonal);
            ResourceManager manager = CreateManager(
                provider,
                Entry(idleKey, "clips/idle"),
                Entry(rightKey, "clips/right"),
                Entry(upKey, "clips/up"),
                Entry(diagonalKey, "clips/diagonal"));
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                default,
                default,
                blend2DDefinitions: new[]
                {
                    new MxAnimationBlend2DDefinition(
                        "locomotion2d",
                        "move.x",
                        "move.y",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend2DPoint(0, 0, idleKey),
                            new MxAnimationBlend2DPoint(1000, 0, rightKey),
                            new MxAnimationBlend2DPoint(0, 1000, upKey),
                            new MxAnimationBlend2DPoint(1000, 1000, diagonalKey)
                        })
                });

            using (BackendFixture fixture = BackendFixture.Create(manager, definition))
            {
                MxAnimationBackendResult blendResult = fixture.Backend.SetBlend2D(new MxAnimationBlend2DRequest
                {
                    BlendId = "locomotion2D",
                    ParameterX = new MxAnimationQuantizedParameter("move.x", 500),
                    ParameterY = new MxAnimationQuantizedParameter("move.y", 500),
                    CorrelationId = "move:500,500"
                });

                Assert.IsTrue(blendResult.Success, blendResult.Message);
                Assert.AreEqual(4, provider.LoadCount);
                Assert.AreEqual(4, manager.CreateDebugSnapshot().LoadedCount);

                MxAnimationDiagnosticSnapshot snapshot = fixture.Backend.CreateSnapshot();
                MxAnimationLayerDiagnostic layer = FindLayer(snapshot, MxAnimationLayerId.Base);
                Assert.AreEqual(MxAnimationBlendKind.Blend2D, layer.BlendKind);
                Assert.AreEqual(string.Empty, layer.Blend1DId);
                Assert.AreEqual("locomotion2d", layer.Blend2DId);
                Assert.AreEqual("move.x", layer.Blend2DParameterX.ParameterId);
                Assert.AreEqual("move.y", layer.Blend2DParameterY.ParameterId);
                Assert.AreEqual(500, layer.Blend2DParameterX.QuantizedValue);
                Assert.AreEqual(500, layer.Blend2DParameterY.QuantizedValue);
                Assert.AreEqual(4, layer.Blend2DWeights.Count);
                Assert.AreEqual(4, layer.ActivePlayableCount);
                Assert.AreEqual(0.25f, layer.Blend2DWeights[0].Weight, 0.0001f);
                Assert.AreEqual(0.25f, layer.Blend2DWeights[1].Weight, 0.0001f);
                Assert.AreEqual(0.25f, layer.Blend2DWeights[2].Weight, 0.0001f);
                Assert.AreEqual(0.25f, layer.Blend2DWeights[3].Weight, 0.0001f);
                Assert.AreEqual(4, snapshot.Cache.CacheMissCount);
                Assert.AreEqual(4, snapshot.Cache.CachedPlayableCount);
                Assert.AreEqual(4, snapshot.Cache.ActivePlayableCount);
                Assert.AreEqual(4, snapshot.Cache.ResourceLoadedCount);
                Assert.AreEqual(4, snapshot.Cache.ResourceRefCount);

                MxAnimationPlaybackState playbackState = fixture.Backend.CapturePlaybackState();
                Assert.AreEqual(4, playbackState.Layers[0].Blend2DWeights.Count);
                Assert.IsTrue(fixture.Backend.SetBlend2D(new MxAnimationBlend2DRequest
                {
                    BlendId = "locomotion2D",
                    ParameterX = new MxAnimationQuantizedParameter("move.x", 1000),
                    ParameterY = new MxAnimationQuantizedParameter("move.y", 0),
                    CorrelationId = "move:1000,0"
                }).Success);

                MxAnimationBackendResult restoreResult = fixture.Backend.RestorePlaybackState(playbackState);
                Assert.IsTrue(restoreResult.Success, restoreResult.Message);
                MxAnimationLayerDiagnostic restored = FindLayer(
                    fixture.Backend.CreateSnapshot(),
                    MxAnimationLayerId.Base);
                Assert.AreEqual(4, restored.Blend2DWeights.Count);
                for (int weightIndex = 0; weightIndex < restored.Blend2DWeights.Count; weightIndex++)
                    Assert.AreEqual(0.25f, restored.Blend2DWeights[weightIndex].Weight, 0.0001f);
            }

            Object.DestroyImmediate(idle);
            Object.DestroyImmediate(right);
            Object.DestroyImmediate(up);
            Object.DestroyImmediate(diagonal);
        }

        [Test]
        public void SetBlend2D_RepeatedWarmPathDoesNotAllocateAfterWarmup()
        {
            ResourceKey idleKey = ClipKey("demo.animation.idle");
            ResourceKey rightKey = ClipKey("demo.animation.right");
            ResourceKey upKey = ClipKey("demo.animation.up");
            ResourceKey diagonalKey = ClipKey("demo.animation.diagonal");
            AnimationClip idle = CreateClip("idle");
            AnimationClip right = CreateClip("right");
            AnimationClip up = CreateClip("up");
            AnimationClip diagonal = CreateClip("diagonal");
            var provider = new MemoryResourceProvider()
                .Register("clips/idle", idle)
                .Register("clips/right", right)
                .Register("clips/up", up)
                .Register("clips/diagonal", diagonal);
            ResourceManager manager = CreateManager(
                provider,
                Entry(idleKey, "clips/idle"),
                Entry(rightKey, "clips/right"),
                Entry(upKey, "clips/up"),
                Entry(diagonalKey, "clips/diagonal"));
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                default,
                default,
                blend2DDefinitions: new[]
                {
                    new MxAnimationBlend2DDefinition(
                        "locomotion2d",
                        "move.x",
                        "move.y",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend2DPoint(0, 0, idleKey),
                            new MxAnimationBlend2DPoint(1000, 0, rightKey),
                            new MxAnimationBlend2DPoint(0, 1000, upKey),
                            new MxAnimationBlend2DPoint(1000, 1000, diagonalKey)
                        },
                        fadeDurationSeconds: 0f)
                });

            try
            {
                using (BackendFixture fixture = BackendFixture.Create(manager, definition))
                {
                    var request = new MxAnimationBlend2DRequest
                    {
                        BlendId = "locomotion2D",
                        ParameterX = new MxAnimationQuantizedParameter("move.x", 500),
                        ParameterY = new MxAnimationQuantizedParameter("move.y", 500),
                        FadeDurationSeconds = 0f,
                        CorrelationId = "move:warm"
                    };

                    Assert.IsTrue(fixture.Backend.SetBlend2D(request).Success);
                    for (int i = 0; i < 24; i++)
                    {
                        request.ParameterX = new MxAnimationQuantizedParameter("move.x", (i & 1) == 0 ? 500 : 250);
                        request.ParameterY = new MxAnimationQuantizedParameter("move.y", (i & 1) == 0 ? 500 : 750);
                        Assert.IsTrue(fixture.Backend.SetBlend2D(request).Success);
                    }

                    _ = GC.GetAllocatedBytesForCurrentThread();
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < 64; i++)
                    {
                        request.ParameterX = new MxAnimationQuantizedParameter("move.x", (i & 1) == 0 ? 500 : 250);
                        request.ParameterY = new MxAnimationQuantizedParameter("move.y", (i & 1) == 0 ? 500 : 750);
                        MxAnimationBackendResult result = fixture.Backend.SetBlend2D(request);
                        if (!result.Success)
                            Assert.Fail(result.Message);
                    }

                    long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                    Assert.AreEqual(0L, allocatedBytes);
                }
            }
            finally
            {
                Object.DestroyImmediate(idle);
                Object.DestroyImmediate(right);
                Object.DestroyImmediate(up);
                Object.DestroyImmediate(diagonal);
            }
        }

        [Test]
        public void CrossFade_FromLocomotionBlendUsesRequestedDuration()
        {
            ResourceKey idleKey = ClipKey("demo.animation.locomotion.idle");
            ResourceKey runKey = ClipKey("demo.animation.locomotion.run");
            ResourceKey attackKey = ClipKey("demo.animation.locomotion.attack");
            AnimationClip idle = CreateClip("locomotion.idle");
            AnimationClip run = CreateClip("locomotion.run");
            AnimationClip attack = CreateClip("locomotion.attack");
            idle.SetCurve("bone", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0f, 1f, 0f));
            run.SetCurve("bone", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0f, 1f, 2f));
            attack.SetCurve("bone", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0f, 1f, 4f));
            var provider = new MemoryResourceProvider()
                .Register("clips/locomotion-idle", idle)
                .Register("clips/locomotion-run", run)
                .Register("clips/locomotion-attack", attack);
            ResourceManager manager = CreateManager(provider,
                Entry(idleKey, "clips/locomotion-idle"),
                Entry(runKey, "clips/locomotion-run"),
                Entry(attackKey, "clips/locomotion-attack"));
            var definition = new MxAnimationSetDefinition(
                "locomotion.to.attack", 1, default, default,
                blend2DDefinitions: new[]
                {
                    new MxAnimationBlend2DDefinition(
                        "locomotion2d", "move.x", "move.y", MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend2DPoint(0, 0, idleKey),
                            new MxAnimationBlend2DPoint(0, 1000, runKey)
                        })
                });

            try
            {
                using (BackendFixture fixture = BackendFixture.Create(manager, definition))
                {
                    Transform bone = new GameObject("bone").transform;
                    bone.SetParent(fixture.Actor.transform, false);
                    Assert.IsTrue(fixture.Backend.SetBlend2D(new MxAnimationBlend2DRequest
                    {
                        BlendId = "locomotion2d",
                        ParameterX = new MxAnimationQuantizedParameter("move.x", 0),
                        ParameterY = new MxAnimationQuantizedParameter("move.y", 500)
                    }).Success);
                    fixture.Backend.Tick(0f);
                    Assert.AreEqual(1f, bone.localPosition.x, 0.01f);
                    Assert.IsTrue(fixture.Backend.CrossFade(new MxAnimationCrossFadeRequest
                    {
                        ClipKey = attackKey,
                        FadeDurationSeconds = 0.4f
                    }).Success);

                    MxAnimationLayerDiagnostic start = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                    Assert.AreEqual(3, start.ActivePlayableCount);
                    Assert.AreEqual(0f, FindPlayback(start, attackKey, blendSlot: false).Weight, 0.0001f);
                    Assert.AreEqual(1f, start.OutgoingWeight, 0.0001f);

                    fixture.Backend.Tick(0.2f);
                    MxAnimationLayerDiagnostic mid = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                    Assert.AreEqual(0.5f, FindPlayback(mid, attackKey, blendSlot: false).Weight, 0.0001f);
                    Assert.AreEqual(0.5f, mid.OutgoingWeight, 0.0001f);
                    Assert.AreEqual(2.5f, bone.localPosition.x, 0.01f);

                    fixture.Backend.Tick(0.2f);
                    MxAnimationLayerDiagnostic complete = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                    Assert.AreEqual(attackKey, complete.CurrentClipKey);
                    Assert.AreEqual(1, complete.ActivePlayableCount);
                    Assert.AreEqual(0f, complete.OutgoingWeight, 0.0001f);
                    Assert.AreEqual(4f, bone.localPosition.x, 0.01f);
                }
            }
            finally
            {
                Object.DestroyImmediate(idle);
                Object.DestroyImmediate(run);
                Object.DestroyImmediate(attack);
            }
        }

        [Test]
        public void SetBlend2D_FadesFromCurrentActionClipToLocomotionBlend()
        {
            ResourceKey attackKey = ClipKey("demo.animation.attack");
            ResourceKey idleKey = ClipKey("demo.animation.idle");
            AnimationClip attack = CreateClip("attack");
            AnimationClip idle = CreateClip("idle");
            var provider = new MemoryResourceProvider()
                .Register("clips/attack", attack)
                .Register("clips/idle", idle);
            ResourceManager manager = CreateManager(
                provider,
                Entry(attackKey, "clips/attack"),
                Entry(idleKey, "clips/idle"));
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                default,
                default,
                blend2DDefinitions: new[]
                {
                    new MxAnimationBlend2DDefinition(
                        "locomotion2d",
                        "move.x",
                        "move.y",
                        MxAnimationLayerId.Base,
                        new[] { new MxAnimationBlend2DPoint(0, 0, idleKey) },
                        fadeDurationSeconds: 0.2f)
                });

            using (BackendFixture fixture = BackendFixture.Create(manager, definition))
            {
                Assert.IsTrue(fixture.Backend.CrossFade(new MxAnimationCrossFadeRequest
                {
                    ClipKey = attackKey,
                    FadeDurationSeconds = 0f
                }).Success);

                Assert.IsTrue(fixture.Backend.SetBlend2D(new MxAnimationBlend2DRequest
                {
                    BlendId = "locomotion2D",
                    ParameterX = new MxAnimationQuantizedParameter("move.x", 0),
                    ParameterY = new MxAnimationQuantizedParameter("move.y", 0)
                }).Success);

                MxAnimationLayerDiagnostic start = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                Assert.Greater(start.OutgoingWeight, 0f);
                Assert.Greater(FindPlayback(start, attackKey, blendSlot: false).Weight, 0f);
                Assert.AreEqual(0f, FindPlayback(start, idleKey, blendSlot: true).Weight, 0.0001f);

                Assert.IsTrue(fixture.Backend.SetBlend2D(new MxAnimationBlend2DRequest
                {
                    BlendId = "locomotion2D",
                    ParameterX = new MxAnimationQuantizedParameter("move.x", 0),
                    ParameterY = new MxAnimationQuantizedParameter("move.y", 0)
                }).Success);

                MxAnimationLayerDiagnostic repeated = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                Assert.Greater(repeated.OutgoingWeight, 0f);
                Assert.Greater(FindPlayback(repeated, attackKey, blendSlot: false).Weight, 0f);

                fixture.Backend.Tick(0.1f);
                MxAnimationLayerDiagnostic mid = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                Assert.Greater(mid.OutgoingWeight, 0f);
                Assert.Greater(FindPlayback(mid, idleKey, blendSlot: true).Weight, 0f);
                Assert.Less(FindPlayback(mid, idleKey, blendSlot: true).Weight, 1f);

                fixture.Backend.Tick(0.1f);
                MxAnimationLayerDiagnostic complete = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                Assert.AreEqual(idleKey, complete.CurrentClipKey);
                Assert.AreEqual(0f, complete.OutgoingWeight, 0.0001f);
                Assert.AreEqual(1f, FindPlayback(complete, idleKey, blendSlot: true).Weight, 0.0001f);
            }

            Object.DestroyImmediate(attack);
            Object.DestroyImmediate(idle);
        }

        [Test]
        public void SetBlend2D_UsesPlaybackSpeedOverrideInDiagnostics()
        {
            ResourceKey idleKey = ClipKey("demo.animation.idle");
            AnimationClip idle = CreateClip("idle");
            var provider = new MemoryResourceProvider().Register("clips/idle", idle);
            ResourceManager manager = CreateManager(provider, Entry(idleKey, "clips/idle"));
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                default,
                default,
                blend2DDefinitions: new[]
                {
                    new MxAnimationBlend2DDefinition(
                        "locomotion2d",
                        "move.x",
                        "move.y",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend2DPoint(0, 1000, idleKey, playbackSpeed: 1f)
                        })
                });

            using (BackendFixture fixture = BackendFixture.Create(manager, definition))
            {
                Assert.IsTrue(fixture.Backend.SetClipPlaybackSpeedOverride(idleKey, 1.25f));

                MxAnimationBackendResult blendResult = fixture.Backend.SetBlend2D(new MxAnimationBlend2DRequest
                {
                    BlendId = "locomotion2D",
                    ParameterX = new MxAnimationQuantizedParameter("move.x", 0),
                    ParameterY = new MxAnimationQuantizedParameter("move.y", 1000),
                    CorrelationId = "move:0,1000"
                });

                Assert.IsTrue(blendResult.Success, blendResult.Message);
                MxAnimationLayerDiagnostic layer = FindLayer(fixture.Backend.CreateSnapshot(), MxAnimationLayerId.Base);
                Assert.AreEqual(1, layer.Blend2DWeights.Count);
                Assert.AreEqual(1.25f, layer.Blend2DWeights[0].PlaybackSpeed, 0.0001f);
            }

            Object.DestroyImmediate(idle);
        }

        [Test]
        public void UpperBodyAttack_WhenFadeOutCompletes_ReleasesHandleBeforeReplay()
        {
            ResourceKey attackKey = ClipKey("demo.animation.attack");
            MxAnimationLayerId upperBody = new MxAnimationLayerId("upper_body");
            AnimationClip attack = CreateClip("attack");
            var provider = new MemoryResourceProvider().Register("clips/attack", attack);
            ResourceManager manager = CreateManager(provider, Entry(attackKey, "clips/attack"));
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                default,
                default,
                layers: new[]
                {
                    new MxAnimationLayerDefinition(MxAnimationLayerId.Base, defaultWeight: 1f),
                    new MxAnimationLayerDefinition(upperBody, "humanoid.upper", 1f)
                });

            using (BackendFixture fixture = BackendFixture.Create(manager, definition))
            {
                MxAnimationBackendResult first = fixture.Backend.Play(new MxAnimationPlayRequest
                {
                    ClipKey = attackKey,
                    LayerId = upperBody
                });
                Assert.IsTrue(first.Success, first.Message);
                Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);

                fixture.Backend.Stop(new MxAnimationStopRequest { LayerId = upperBody, FadeOutDurationSeconds = 0.25f });
                fixture.Backend.Tick(0.25f);

                Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(1, provider.ReleaseCount);

                MxAnimationBackendResult second = fixture.Backend.Play(new MxAnimationPlayRequest
                {
                    ClipKey = attackKey,
                    LayerId = upperBody
                });
                Assert.IsTrue(second.Success, second.Message);
                Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(2, provider.LoadCount);

                MxAnimationDiagnosticSnapshot snapshot = fixture.Backend.CreateSnapshot();
                MxAnimationLayerDiagnostic upper = FindLayer(snapshot, upperBody);
                Assert.AreEqual(attackKey, upper.CurrentClipKey);
                Assert.AreEqual(1, upper.ActivePlayableCount);
                Assert.AreEqual(1, snapshot.Cache.CachedPlayableCount);
                Assert.AreEqual(1, snapshot.Cache.ResourceRefCount);
            }

            Object.DestroyImmediate(attack);
        }

        [Test]
        public void SetBlend1D_WhenWeightedClipFails_ReleasesAlreadyConnectedBlendSlots()
        {
            ResourceKey idleKey = ClipKey("demo.animation.idle");
            ResourceKey runKey = ClipKey("demo.animation.run_missing");
            AnimationClip idle = CreateClip("idle");
            var provider = new MemoryResourceProvider().Register("clips/idle", idle);
            ResourceManager manager = CreateManager(
                provider,
                Entry(idleKey, "clips/idle"),
                Entry(runKey, "clips/missing"));
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                default,
                default,
                blend1DDefinitions: new[]
                {
                    new MxAnimationBlend1DDefinition(
                        "locomotion",
                        "locomotion.speed",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend1DPoint(0, idleKey),
                            new MxAnimationBlend1DPoint(1000, runKey)
                        })
                });

            using (BackendFixture fixture = BackendFixture.Create(manager, definition))
            {
                MxAnimationBackendResult result = fixture.Backend.SetBlend1D(new MxAnimationBlend1DRequest
                {
                    BlendId = "locomotion",
                    Parameter = new MxAnimationQuantizedParameter("locomotion.speed", 500),
                    CorrelationId = "speed:500"
                });

                Assert.IsFalse(result.Success);
                Assert.AreEqual(MxAnimationBackendResultCode.LoadFailed, result.Code);
                Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
                Assert.AreEqual(1, provider.ReleaseCount);

                MxAnimationDiagnosticSnapshot snapshot = fixture.Backend.CreateSnapshot();
                MxAnimationLayerDiagnostic layer = FindLayer(snapshot, MxAnimationLayerId.Base);
                Assert.AreEqual(MxAnimationLayerStatus.Failed, layer.Status);
                Assert.AreEqual(0, layer.ActivePlayableCount);
                Assert.AreEqual(ResourceErrorCode.NotFound, layer.LastError.Code);
            }

            Object.DestroyImmediate(idle);
        }

        [Test]
        public void AvatarMask_LoadsThroughResourceManagerAndReleasesWithBackend()
        {
            MxAnimationLayerId upperBody = new MxAnimationLayerId("upper_body");
            ResourceKey maskKey = new ResourceKey("demo.animation.mask.upper_body", ResourceTypeIds.AvatarMask);
            var mask = new AvatarMask { name = "upper_body" };
            var provider = new MemoryResourceProvider().Register("masks/upper", mask);
            ResourceManager manager = CreateManager(provider, Entry(maskKey, "masks/upper"));
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                default,
                default,
                layers: new[]
                {
                    new MxAnimationLayerDefinition(upperBody, "humanoid.upper", 1f, MxAnimationLayerBlendMode.Override, maskKey)
                });

            BackendFixture fixture = BackendFixture.Create(manager, definition);
            MxAnimationLayerDiagnostic layer = FindLayer(fixture.Backend.CreateSnapshot(), upperBody);
            Assert.AreEqual(MxAnimationLayerMaskStatus.Loaded, layer.MaskStatus);
            Assert.AreEqual(maskKey, layer.MaskKey);
            Assert.AreEqual(1, manager.CreateDebugSnapshot().LoadedCount);

            fixture.Dispose();

            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(1, provider.ReleaseCount);
            Object.DestroyImmediate(mask);
        }

        [Test]
        public void AvatarMask_WhenMissing_ReportsDiagnosticsWithoutBlockingClipPlayback()
        {
            MxAnimationLayerId upperBody = new MxAnimationLayerId("upper_body");
            ResourceKey maskKey = new ResourceKey("demo.animation.mask.upper_body", ResourceTypeIds.AvatarMask);
            ResourceKey attackKey = ClipKey("demo.animation.attack");
            AnimationClip attack = CreateClip("attack");
            var provider = new MemoryResourceProvider().Register("clips/attack", attack);
            ResourceManager manager = CreateManager(
                provider,
                Entry(maskKey, "masks/missing"),
                Entry(attackKey, "clips/attack"));
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                default,
                default,
                layers: new[]
                {
                    new MxAnimationLayerDefinition(upperBody, "humanoid.upper", 1f, MxAnimationLayerBlendMode.Override, maskKey)
                });

            using (BackendFixture fixture = BackendFixture.Create(manager, definition))
            {
                MxAnimationLayerDiagnostic failedMask = FindLayer(fixture.Backend.CreateSnapshot(), upperBody);
                Assert.AreEqual(MxAnimationLayerMaskStatus.Failed, failedMask.MaskStatus);
                Assert.AreEqual(ResourceErrorCode.NotFound, failedMask.LastError.Code);

                MxAnimationBackendResult play = fixture.Backend.Play(new MxAnimationPlayRequest
                {
                    ClipKey = attackKey,
                    LayerId = upperBody
                });

                Assert.IsTrue(play.Success, play.Message);
                MxAnimationLayerDiagnostic playing = FindLayer(fixture.Backend.CreateSnapshot(), upperBody);
                Assert.AreEqual(MxAnimationLayerStatus.Playing, playing.Status);
                Assert.AreEqual(attackKey, playing.CurrentClipKey);
                Assert.AreEqual(MxAnimationLayerMaskStatus.Failed, playing.MaskStatus);
            }

            Object.DestroyImmediate(attack);
        }

        [Test]
        public void Release_DestroysGraphAndReleasesAllOwnedHandles()
        {
            ResourceKey defaultKey = ClipKey("demo.animation.default");
            ResourceKey fallbackKey = ClipKey("demo.animation.fallback");
            ResourceKey actionKey = ClipKey("demo.animation.action");
            AnimationClip defaultClip = CreateClip("default");
            AnimationClip fallbackClip = CreateClip("fallback");
            AnimationClip actionClip = CreateClip("action");
            var provider = new MemoryResourceProvider()
                .Register("clips/default", defaultClip)
                .Register("clips/fallback", fallbackClip)
                .Register("clips/action", actionClip);
            ResourceManager manager = CreateManager(
                provider,
                Entry(defaultKey, "clips/default"),
                Entry(fallbackKey, "clips/fallback"),
                Entry(actionKey, "clips/action"));
            var definition = new MxAnimationSetDefinition("demo.set", 1, defaultKey, fallbackKey);

            BackendFixture fixture = BackendFixture.Create(manager, definition);
            fixture.Backend.Play(new MxAnimationPlayRequest { ClipKey = actionKey });
            Assert.AreEqual(3, manager.CreateDebugSnapshot().LoadedCount);

            fixture.Dispose();

            MxAnimationDiagnosticSnapshot snapshot = fixture.Backend.CreateSnapshot();
            Assert.IsTrue(snapshot.IsReleased);
            Assert.IsFalse(snapshot.GraphIsValid);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            Assert.AreEqual(3, provider.ReleaseCount);

            Object.DestroyImmediate(defaultClip);
            Object.DestroyImmediate(fallbackClip);
            Object.DestroyImmediate(actionClip);
        }

        [Test]
        public void Release_CancelsPendingClipLoadWithoutLoadingHandle()
        {
            ResourceKey clipKey = ClipKey("demo.animation.delayed");
            AnimationClip clip = CreateClip("delayed");
            var provider = new MemoryResourceProvider().Register("clips/delayed", clip);
            var manager = new DelayedResourceManager(clipKey)
                .RegisterProvider(provider)
                .AddCatalog(new ResourceCatalog("animation.tests", string.Empty, new[] { Entry(clipKey, "clips/delayed") }));

            BackendFixture fixture = BackendFixture.Create(manager, EmptyDefinition());
            MxAnimationBackendResult play = fixture.Backend.Play(new MxAnimationPlayRequest { ClipKey = clipKey });

            Assert.AreEqual(MxAnimationBackendResultCode.Queued, play.Code);
            Assert.IsNotNull(manager.LastOperation);
            Assert.IsFalse(manager.LastOperation.IsCancelled);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);

            fixture.Backend.Release();
            manager.LastOperation.Complete();
            fixture.Backend.Tick(0.1f);

            Assert.IsTrue(manager.LastOperation.IsCancelled);
            Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
            MxAnimationDiagnosticSnapshot snapshot = fixture.Backend.CreateSnapshot();
            Assert.IsTrue(snapshot.IsReleased);
            Assert.IsFalse(snapshot.GraphIsValid);

            fixture.Dispose();
            Object.DestroyImmediate(clip);
        }

        private static MxAnimationSetDefinition EmptyDefinition()
        {
            return new MxAnimationSetDefinition("empty", 1, default, default);
        }

        private static ResourceKey ClipKey(string id)
        {
            return new ResourceKey(id, ResourceTypeIds.AnimationClip);
        }

        private static ResourceCatalogEntry Entry(ResourceKey key, string address)
        {
            return new ResourceCatalogEntry(key.Id, key.TypeId, "memory", address, key.Variant, key.PackageId);
        }

        private static ResourceManager CreateManager(MemoryResourceProvider provider, params ResourceCatalogEntry[] entries)
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog("animation.tests", string.Empty, entries));
            return manager;
        }

        private static AnimationClip CreateClip(string name)
        {
            return new AnimationClip
            {
                name = name,
                frameRate = 30f,
                wrapMode = WrapMode.Once
            };
        }

        private static MxAnimationLayerDiagnostic FindLayer(MxAnimationDiagnosticSnapshot snapshot, MxAnimationLayerId layerId)
        {
            for (int i = 0; i < snapshot.LayerStates.Count; i++)
            {
                if (snapshot.LayerStates[i].LayerId == layerId)
                    return snapshot.LayerStates[i];
            }

            Assert.Fail("Expected layer diagnostic for " + layerId + ".");
            return null;
        }

        private static MxAnimationClipPlaybackDiagnostic FindCurrentPlayback(
            MxAnimationLayerDiagnostic layer,
            ResourceKey clipKey)
        {
            for (int i = 0; i < layer.ActiveClipPlaybacks.Count; i++)
            {
                MxAnimationClipPlaybackDiagnostic playback = layer.ActiveClipPlaybacks[i];
                if (!playback.BlendSlot && playback.ClipKey == clipKey)
                    return playback;
            }

            Assert.Fail("Expected active playback for " + clipKey + ".");
            return null;
        }

        private static MxAnimationClipPlaybackDiagnostic FindPlayback(
            MxAnimationLayerDiagnostic layer,
            ResourceKey clipKey,
            bool blendSlot)
        {
            for (int i = 0; i < layer.ActiveClipPlaybacks.Count; i++)
            {
                MxAnimationClipPlaybackDiagnostic playback = layer.ActiveClipPlaybacks[i];
                if (playback.BlendSlot == blendSlot && playback.ClipKey == clipKey)
                    return playback;
            }

            Assert.Fail("Expected active playback for " + clipKey + " blendSlot=" + blendSlot + ".");
            return null;
        }

        private sealed class BackendFixture : System.IDisposable
        {
            private BackendFixture(GameObject actor, UnityPlayablesAnimationBackend backend)
            {
                Actor = actor;
                Backend = backend;
            }

            public GameObject Actor { get; }
            public UnityPlayablesAnimationBackend Backend { get; }

            public static BackendFixture Create(IResourceManager manager, MxAnimationSetDefinition definition)
            {
                var actor = new GameObject("AnimationBackendTestActor");
                var animator = actor.AddComponent<Animator>();
                var backend = new UnityPlayablesAnimationBackend(animator, manager, definition, "actor.test");
                return new BackendFixture(actor, backend);
            }

            public void Dispose()
            {
                Backend.Release();
                Object.DestroyImmediate(Actor);
            }
        }

        private sealed class DelayedResourceManager : IResourceManager
        {
            private readonly ResourceManager _inner = new ResourceManager();
            private readonly ResourceKey _delayedKey;

            public DelayedResourceManager(ResourceKey delayedKey)
            {
                _delayedKey = delayedKey;
            }

            public IManualResourceOperation LastOperation { get; private set; }

            public DelayedResourceManager RegisterProvider(IResourceProvider provider)
            {
                _inner.RegisterProvider(provider);
                return this;
            }

            IResourceManager IResourceManager.RegisterProvider(IResourceProvider provider)
            {
                return RegisterProvider(provider);
            }

            public DelayedResourceManager AddCatalog(ResourceCatalog catalog)
            {
                _inner.AddCatalog(catalog);
                return this;
            }

            IResourceManager IResourceManager.AddCatalog(ResourceCatalog catalog)
            {
                return AddCatalog(catalog);
            }

            public bool Contains(ResourceKey key)
            {
                return _inner.Contains(key);
            }

            public ResourceLoadResult<ResourceHandle<T>> Load<T>(ResourceKey key)
            {
                return _inner.Load<T>(key);
            }

            public IResourceOperation<ResourceHandle<T>> LoadAsync<T>(ResourceKey key, CancellationToken cancellationToken = default)
            {
                if (key == _delayedKey)
                {
                    var operation = new DelayedResourceOperation<ResourceHandle<T>>(() => _inner.Load<T>(key));
                    LastOperation = operation;
                    if (cancellationToken.IsCancellationRequested)
                        operation.Cancel();
                    return operation;
                }

                return _inner.LoadAsync<T>(key, cancellationToken);
            }

            public void Release<T>(ResourceHandle<T> handle)
            {
                _inner.Release(handle);
            }

            public ResourceDebugSnapshot CreateDebugSnapshot()
            {
                return _inner.CreateDebugSnapshot();
            }
        }

        private interface IManualResourceOperation
        {
            bool IsCancelled { get; }
            void Complete();
        }

        private sealed class DelayedResourceOperation<T> : IResourceOperation<T>, IManualResourceOperation
        {
            private readonly Func<ResourceLoadResult<T>> _load;
            private ResourceLoadResult<T> _result;

            public DelayedResourceOperation(Func<ResourceLoadResult<T>> load)
            {
                _load = load;
            }

            public bool IsDone { get; private set; }
            public bool IsCancelled { get; private set; }
            public float Progress => IsDone ? 1f : 0f;
            public ResourceLoadResult<T> Result => IsDone
                ? _result
                : ResourceLoadResult<T>.Failed(new ResourceError(ResourceErrorCode.ProviderFailed, default, string.Empty, "Resource operation is not completed."));

            public void Complete()
            {
                if (IsDone)
                    return;

                _result = _load();
                IsDone = true;
            }

            public void Cancel()
            {
                if (IsDone)
                    return;

                IsCancelled = true;
                _result = ResourceLoadResult<T>.Failed(new ResourceError(ResourceErrorCode.Cancelled, default, string.Empty, "Resource operation was cancelled."));
                IsDone = true;
            }
        }
    }
}

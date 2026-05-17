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
                    new MxAnimationActionBinding("attack", "action:attack", attackKey, upperBody)
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
                    BindingId = "attack",
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

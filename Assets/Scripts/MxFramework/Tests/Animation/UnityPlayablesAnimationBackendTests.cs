using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Animation
{
    public sealed class UnityPlayablesAnimationBackendTests
    {
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

            public static BackendFixture Create(ResourceManager manager, MxAnimationSetDefinition definition)
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
    }
}

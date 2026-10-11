using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Core.Math;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Vfx.Unity;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Combat.Projectiles
{
    public sealed class CombatProjectileUnityPresenterTests
    {
        private static readonly ResourceKey ProjectilePrefabKey =
            new ResourceKey("projectile.test.fireball", ResourceTypeIds.GameObject);

        [Test]
        public void Sync_CreatesUpdatesAndReleasesProjectileViews()
        {
            GameObject prefab = new GameObject("projectile-prefab");
            prefab.AddComponent<ParticleSystem>();
            try
            {
                MemoryResourceProvider provider;
                ResourceManager resources = CreateResources(prefab, out provider);
                var presenter = new CombatProjectileUnityPresenter(
                    resources,
                    CreateRegistry(),
                    usePooling: false);

                presenter.Sync(CreateState(Snapshot(7, FixVector3.Zero, FixVector3Forward())));
                Assert.AreEqual(1, presenter.ActiveCount);
                Assert.AreEqual(1, provider.LoadCount);
                GameObject view = FindView("ProjectileVFX_1001_7");
                Assert.IsNotNull(view);
                Assert.AreEqual(Vector3.zero, view.transform.position);
                Assert.That(Vector3.Dot(Vector3.forward, view.transform.forward), Is.GreaterThan(0.99f));

                presenter.Sync(CreateState(Snapshot(
                    7,
                    new FixVector3(Fix64.One, Fix64.FromRatio(1, 2), Fix64.FromInt(3)),
                    FixVector3Right())));
                Assert.AreEqual(1, presenter.ActiveCount);
                Assert.AreSame(view, FindView("ProjectileVFX_1001_7"));
                Assert.AreEqual(new Vector3(1f, 0.5f, 3f), view.transform.position);
                Assert.That(Vector3.Dot(Vector3.right, view.transform.forward), Is.GreaterThan(0.99f));

                presenter.Sync(CreateState());
                Assert.AreEqual(0, presenter.ActiveCount);
                Assert.AreEqual(0, provider.ReleaseCount);
                Assert.IsNull(FindView("ProjectileVFX_1001_7"));
                presenter.Dispose();
                Assert.AreEqual(1, provider.ReleaseCount);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Sync_MissingBindingRecordsDiagnosticAndDoesNotThrow()
        {
            GameObject prefab = new GameObject("projectile-prefab-missing-binding");
            try
            {
                MemoryResourceProvider provider;
                ResourceManager resources = CreateResources(prefab, out provider);
                var presenter = new CombatProjectileUnityPresenter(
                    resources,
                    new CombatProjectileUnityPresentationRegistry(),
                    usePooling: false);

                Assert.DoesNotThrow(() =>
                    presenter.Sync(CreateState(Snapshot(8, FixVector3.Zero, FixVector3Forward()))));

                Assert.AreEqual(0, presenter.ActiveCount);
                Assert.AreEqual(0, provider.LoadCount);
                Assert.That(presenter.Diagnostics, Has.Some.Contains("binding is missing"));
                presenter.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void SyncActiveViews_UsesPrewarmedPoolWithoutReloadingPrefab()
        {
            GameObject prefab = new GameObject("projectile-prefab-prewarm");
            prefab.AddComponent<ParticleSystem>();
            try
            {
                MemoryResourceProvider provider;
                ResourceManager resources = CreateResources(prefab, out provider);
                var presenter = new CombatProjectileUnityPresenter(
                    resources,
                    CreateRegistry(),
                    usePooling: true);
                var activeViews = new List<CombatProjectileActiveView>(1);

                presenter.PrewarmBindings(poolSizePerBinding: 1);
                Assert.AreEqual(1, provider.LoadCount);

                activeViews.Add(ActiveView(10, FixVector3.Zero, FixVector3Forward()));
                presenter.Sync(activeViews);
                Assert.AreEqual(1, presenter.ActiveCount);
                Assert.AreEqual(1, provider.LoadCount);

                activeViews.Clear();
                presenter.Sync(activeViews);
                Assert.AreEqual(0, presenter.ActiveCount);
                Assert.AreEqual(0, provider.ReleaseCount);

                activeViews.Add(ActiveView(11, FixVector3.Zero, FixVector3Forward()));
                presenter.Sync(activeViews);
                Assert.AreEqual(1, presenter.ActiveCount);
                Assert.AreEqual(1, provider.LoadCount);

                presenter.Dispose();
                Assert.AreEqual(1, provider.ReleaseCount);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Sync_NotifiesPresentationReceiverWithProjectileContext()
        {
            GameObject prefab = new GameObject("projectile-prefab-receiver");
            prefab.AddComponent<ProjectilePresentationReceiverProbe>();
            try
            {
                MemoryResourceProvider provider;
                ResourceManager resources = CreateResources(prefab, out provider);
                var presenter = new CombatProjectileUnityPresenter(
                    resources,
                    CreateRegistry(),
                    usePooling: false);

                presenter.Sync(CreateState(Snapshot(
                    12,
                    FixVector3.Zero,
                    FixVector3Forward(),
                    localFrame: 73,
                    ageFrames: 9)));

                GameObject view = FindView("ProjectileVFX_1001_12");
                Assert.IsNotNull(view);
                ProjectilePresentationReceiverProbe probe =
                    view.GetComponent<ProjectilePresentationReceiverProbe>();
                Assert.IsNotNull(probe);
                Assert.AreEqual(1, probe.UpdateCount);
                Assert.AreEqual(12, probe.LastUpdate.ProjectileId);
                Assert.AreEqual(1001, probe.LastUpdate.DefinitionId);
                Assert.AreEqual(73, probe.LastUpdate.LocalFrame);
                Assert.AreEqual(9, probe.LastUpdate.AgeFrames);
                presenter.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Sync_LoadFailureRecordsDiagnosticAndCanRecoverOnNextSync()
        {
            var resources = new ResourceManager()
                .RegisterProvider(new MemoryResourceProvider())
                .AddCatalog(new ResourceCatalog("projectile.presentation.test", string.Empty, new[]
                {
                    new ResourceCatalogEntry(
                        ProjectilePrefabKey.Id,
                        ProjectilePrefabKey.TypeId,
                        MemoryResourceProvider.Id,
                        "missing-prefab")
                }));
            var presenter = new CombatProjectileUnityPresenter(resources, CreateRegistry(), usePooling: false);

            Assert.DoesNotThrow(() =>
                presenter.Sync(CreateState(Snapshot(9, FixVector3.Zero, FixVector3Forward()))));

            Assert.AreEqual(0, presenter.ActiveCount);
            Assert.That(presenter.Diagnostics, Has.Some.Contains("prefab load failed"));
            presenter.Dispose();
        }

        [Test]
        public void Sync_LifecycleProjectileUsesRebuildForExistingStateAndRetiresThroughComplete()
        {
            GameObject prefab = new GameObject("projectile-prefab-lifecycle");
            GameObject build = new GameObject("Build");
            GameObject rebuild = new GameObject("Rebuild");
            GameObject loop = new GameObject("Loop");
            GameObject complete = new GameObject("Complete");
            GameObject dissipate = new GameObject("Dissipate");
            build.transform.SetParent(prefab.transform, false);
            rebuild.transform.SetParent(prefab.transform, false);
            loop.transform.SetParent(prefab.transform, false);
            complete.transform.SetParent(prefab.transform, false);
            dissipate.transform.SetParent(prefab.transform, false);
            UnityVfxLifecycleController sourceController =
                prefab.AddComponent<UnityVfxLifecycleController>();
            sourceController.Configure(
                build,
                rebuild,
                loop,
                complete,
                dissipate,
                buildFrames: 2,
                rebuildFrames: 3,
                loopFrames: 60,
                completeFrames: 1,
                dissipateFrames: 1,
                UnityVfxLoopPlaybackMode.Continuous,
                cleanupLimitFrames: 1);
            try
            {
                MemoryResourceProvider provider;
                ResourceManager resources = CreateResources(prefab, out provider);
                var presenter = new CombatProjectileUnityPresenter(
                    resources,
                    CreateRegistry(),
                    usePooling: false);

                presenter.Sync(CreateState(Snapshot(
                    20,
                    FixVector3.Zero,
                    FixVector3Forward(),
                    ageFrames: 4)));
                GameObject view = FindView("ProjectileVFX_1001_20");
                Assert.IsNotNull(view);
                UnityVfxLifecycleController lifecycle =
                    view.GetComponent<UnityVfxLifecycleController>();
                Assert.AreEqual(UnityVfxLifecyclePhase.Rebuild, lifecycle.CurrentPhase);

                presenter.Sync(CreateState());
                Assert.AreEqual(0, presenter.ActiveCount);
                Assert.AreEqual(UnityVfxLifecyclePhase.Complete, lifecycle.CurrentPhase);
                Assert.IsNotNull(FindView("ProjectileVFX_1001_20"));

                lifecycle.Advance(1f);
                presenter.Sync(CreateState());
                Assert.IsNull(FindView("ProjectileVFX_1001_20"));
                presenter.Dispose();
                Assert.AreEqual(1, provider.ReleaseCount);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void EventDrivenExitPolicyMapsCompleteDissipateExternalAndResetWithoutDoubleExit()
        {
            GameObject prefab = CreateLifecyclePrefab("projectile-prefab-exit-policy");
            try
            {
                MemoryResourceProvider provider;
                ResourceManager resources = CreateResources(prefab, out provider);
                var presenter = new CombatProjectileUnityPresenter(resources, CreateRegistry(), usePooling: false);

                presenter.BuildPersistent(Snapshot(31, FixVector3.Zero, FixVector3Forward(), ageFrames: 0));
                GameObject accepted = FindView("ProjectileVFX_1001_31");
                presenter.ExitPersistent(31, CombatProjectilePresentationEventKind.DirectImpact);
                Assert.That(accepted.GetComponent<UnityVfxLifecycleController>().CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Complete));
                presenter.SyncFromBridge(System.Array.Empty<CombatProjectileActiveView>());
                Assert.That(accepted.GetComponent<UnityVfxLifecycleController>().CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Complete),
                    "event exit followed by disappearance sync must not apply a second exit");

                presenter.BuildPersistent(Snapshot(32, FixVector3.Zero, FixVector3Forward(), ageFrames: 0));
                GameObject blocked = FindView("ProjectileVFX_1001_32");
                presenter.ExitPersistent(32, CombatProjectilePresentationEventKind.BlockedImpact);
                Assert.That(blocked.GetComponent<UnityVfxLifecycleController>().CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Dissipate));

                presenter.BuildPersistent(Snapshot(33, FixVector3.Zero, FixVector3Forward(), ageFrames: 0));
                GameObject external = FindView("ProjectileVFX_1001_33");
                presenter.SyncFromBridge(System.Array.Empty<CombatProjectileActiveView>());
                Assert.That(external.GetComponent<UnityVfxLifecycleController>().CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Dissipate));

                presenter.BuildPersistent(Snapshot(34, FixVector3.Zero, FixVector3Forward(), ageFrames: 0));
                GameObject reset = FindView("ProjectileVFX_1001_34");
                presenter.ResetPersistent();
                Assert.That(reset == null, Is.True, "runtime reset must immediately destroy/recycle the persistent view");
                presenter.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void PersistentSlotAppliesLocalOffsetInProjectileBasis()
        {
            GameObject prefab = new GameObject("projectile-prefab-offset");
            try
            {
                MemoryResourceProvider provider;
                ResourceManager resources = CreateResources(prefab, out provider);
                var slot = new CombatProjectileUnityPresentationSlot(
                    CombatProjectileUnityPresentationSlotKind.FlightBody,
                    ProjectilePrefabKey,
                    CombatProjectileUnityPresentationOrientation.ProjectileForward,
                    new FixVector3(Fix64.One, Fix64.FromInt(2), Fix64.FromInt(3)),
                    0);
                var registry = new CombatProjectileUnityPresentationRegistry(new[]
                {
                    new CombatProjectileUnityPresentationBindingV2(1001, "ProjectileVFX",
                        CombatProjectileUnityPresentationExitPolicy.Default, new[] { slot })
                });
                var presenter = new CombatProjectileUnityPresenter(resources, registry, usePooling: false);

                presenter.BuildPersistent(Snapshot(35, FixVector3.Zero, FixVector3Forward(), ageFrames: 0));

                Assert.That(Vector3.Distance(FindView("ProjectileVFX_1001_35").transform.position,
                    new Vector3(1f, 2f, 3f)), Is.LessThan(0.0001f));
                presenter.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void PoolExhaustionInstantiatesFailSoftAndRecordsOneDiagnostic()
        {
            GameObject prefab = new GameObject("projectile-prefab-pool-exhausted");
            try
            {
                MemoryResourceProvider provider;
                ResourceManager resources = CreateResources(prefab, out provider);
                var presenter = new CombatProjectileUnityPresenter(resources, CreateRegistry(), usePooling: true);
                presenter.PrewarmBindings(1);

                presenter.Sync(new[]
                {
                    ActiveView(41, FixVector3.Zero, FixVector3Forward()),
                    ActiveView(42, FixVector3.Zero, FixVector3Forward()),
                    ActiveView(43, FixVector3.Zero, FixVector3Forward())
                });

                Assert.That(presenter.ActiveCount, Is.EqualTo(3));
                Assert.That(presenter.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(presenter.Diagnostics[0], Does.Contain("pool exhausted"));
                presenter.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void ClaimedSpawnSuppressesOnlySpawnOneShotAndStillBuildsStandardPersistentView()
        {
            GameObject prefab = new GameObject("projectile-prefab-custom-claim");
            try
            {
                MemoryResourceProvider provider;
                ResourceManager resources = CreateResources(prefab, out provider);
                CombatProjectileUnityPresentationRegistry registry = CreateRegistry();
                var presenter = new CombatProjectileUnityPresenter(resources, registry, usePooling: false);
                var extension = new ClaimAllExtension();
                var bridge = new CombatProjectileVfxBridge(registry, null, presenter,
                    new ICombatProjectilePresentationExtension[] { extension });
                CombatProjectileSnapshot projectile = Snapshot(44, FixVector3.Zero, FixVector3Forward(), ageFrames: 0);
                var events = new CombatProjectileFrameEventBuffer();
                SetSpawn(events, projectile);

                bridge.ProcessFrame(new RuntimeFrame(1), events,
                    new[] { ActiveView(44, FixVector3.Zero, FixVector3Forward()) });

                Assert.That(extension.HandleCount, Is.EqualTo(1), "claimed Spawn must not fall through to LateAttachRebuild");
                Assert.That(presenter.ActiveCount, Is.EqualTo(1));
                Assert.That(FindView("ProjectileVFX_1001_44"), Is.Not.Null,
                    "custom Spawn claim suppresses only the Spawn one-shot slot, never Flight/Trail Build");
                presenter.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void ClaimedDirectOneShotStillExitsStandardFlightExactlyOnceWhenConsumed()
        {
            GameObject prefab = CreateLifecyclePrefab("projectile-prefab-direct-claim");
            try
            {
                MemoryResourceProvider provider;
                ResourceManager resources = CreateResources(prefab, out provider);
                CombatProjectileUnityPresentationRegistry registry = CreateRegistry();
                var presenter = new CombatProjectileUnityPresenter(resources, registry, usePooling: false);
                var extension = new DirectOnlyExtension();
                var bridge = new CombatProjectileVfxBridge(registry, null, presenter,
                    new ICombatProjectilePresentationExtension[] { extension });
                CombatProjectileSnapshot projectile = Snapshot(45, FixVector3.Zero, FixVector3Forward(), ageFrames: 0);
                var events = new CombatProjectileFrameEventBuffer();
                SetSpawn(events, projectile);
                bridge.ProcessFrame(new RuntimeFrame(1), events,
                    new[] { ActiveView(45, FixVector3.Zero, FixVector3Forward()) });
                GameObject view = FindView("ProjectileVFX_1001_45");
                Assert.That(view, Is.Not.Null);

                SetImpact(events, DirectImpact(projectile, consumed: true));
                bridge.ProcessFrame(new RuntimeFrame(2), events, System.Array.Empty<CombatProjectileActiveView>());
                UnityVfxLifecycleController lifecycle = view.GetComponent<UnityVfxLifecycleController>();
                Assert.That(extension.HandleCount, Is.EqualTo(1));
                Assert.That(presenter.ActiveCount, Is.Zero);
                Assert.That(lifecycle.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Complete));

                bridge.ProcessFrame(new RuntimeFrame(2), events, System.Array.Empty<CombatProjectileActiveView>());
                Assert.That(extension.HandleCount, Is.EqualTo(1), "repeated authoritative buffer is deduped");
                Assert.That(lifecycle.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Complete),
                    "consumed event plus disappearance must not apply a second exit");
                presenter.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void TrackingOverflowNeverBuildsOrLeaksUntrackedPersistentViewAndIsDiagnosed()
        {
            GameObject prefab = new GameObject("projectile-prefab-tracking-overflow");
            try
            {
                MemoryResourceProvider provider;
                ResourceManager resources = CreateResources(prefab, out provider);
                CombatProjectileUnityPresentationRegistry registry = CreateRegistry();
                var presenter = new CombatProjectileUnityPresenter(resources, registry, usePooling: false);
                var extension = new ClaimAllExtension();
                var bridge = new CombatProjectileVfxBridge(registry, null, presenter,
                    new ICombatProjectilePresentationExtension[] { extension }, trackingCapacity: 2, correlationCapacity: 8);
                var events = new CombatProjectileFrameEventBuffer();
                var active = new[]
                {
                    ActiveView(51, FixVector3.Zero, FixVector3Forward()),
                    ActiveView(52, FixVector3.Zero, FixVector3Forward()),
                    ActiveView(53, FixVector3.Zero, FixVector3Forward())
                };

                bridge.ProcessFrame(new RuntimeFrame(3), events, active);
                bridge.ProcessFrame(new RuntimeFrame(4), events, active);

                Assert.That(presenter.ActiveCount, Is.EqualTo(2));
                Assert.That(FindView("ProjectileVFX_1001_51"), Is.Not.Null);
                Assert.That(FindView("ProjectileVFX_1001_52"), Is.Not.Null);
                Assert.That(FindView("ProjectileVFX_1001_53"), Is.Null);
                Assert.That(bridge.GetDiagnostics().TrackingOverflows, Is.GreaterThanOrEqualTo(1));
                Assert.That(extension.HandleCount, Is.EqualTo(2), "only the two tracked late-attach projectiles receive lifecycle callbacks");

                bridge.ProcessFrame(new RuntimeFrame(5), events, System.Array.Empty<CombatProjectileActiveView>());
                Assert.That(presenter.ActiveCount, Is.Zero);
                Assert.That(FindView("ProjectileVFX_1001_51"), Is.Null);
                Assert.That(FindView("ProjectileVFX_1001_52"), Is.Null);
                Assert.That(FindView("ProjectileVFX_1001_53"), Is.Null);
                presenter.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        private static void SetSpawn(CombatProjectileFrameEventBuffer buffer, CombatProjectileSnapshot projectile)
            => SetEvents(buffer, new[] { new CombatProjectileSpawnEvent(projectile) }, null);

        private static void SetImpact(CombatProjectileFrameEventBuffer buffer, CombatProjectileImpactEvent impact)
            => SetEvents(buffer, null, new[] { impact });

        private static void SetEvents(CombatProjectileFrameEventBuffer buffer,
            IReadOnlyList<CombatProjectileSpawnEvent> spawns,
            IReadOnlyList<CombatProjectileImpactEvent> impacts)
        {
            typeof(CombatProjectileFrameEventBuffer).GetMethod("CopyFrom",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(IReadOnlyList<CombatProjectileSpawnEvent>),
                    typeof(IReadOnlyList<CombatProjectileAreaPulseTriggerEvent>),
                    typeof(IReadOnlyList<CombatProjectileImpactEvent>),
                    typeof(IReadOnlyList<CombatProjectileExpiredEvent>),
                    typeof(IReadOnlyList<CombatProjectileRejectedEvent>),
                    typeof(IReadOnlyList<CombatProjectilePhysicalEvent>)
                }, null).Invoke(buffer, new object[]
                {
                    spawns, null, impacts, null, null, null
                });
        }

        private static CombatProjectileImpactEvent DirectImpact(CombatProjectileSnapshot projectile, bool consumed)
        {
            var result = new HitResolveResult(projectile.SourceEntityId, new CombatEntityId(2), projectile.ActionId,
                projectile.ActionInstanceId, projectile.TraceId, projectile.LastFrame, HitResolveKind.Damage, 0,
                0, FixVector3.Zero, projectile.Position, new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
            return new CombatProjectileImpactEvent(projectile, default(CombatQueryResult), result, default,
                new CombatProjectileImpactContext(CombatProjectileImpactSourceKind.DirectSweep), consumed);
        }

        private sealed class ClaimAllExtension : ICombatProjectilePresentationExtension
        {
            public int HandleCount { get; private set; }
            public bool CanHandle(in CombatProjectilePresentationEventContext context) => true;
            public void Handle(in CombatProjectilePresentationEventContext context) => HandleCount++;
        }

        private sealed class DirectOnlyExtension : ICombatProjectilePresentationExtension
        {
            public int HandleCount { get; private set; }
            public bool CanHandle(in CombatProjectilePresentationEventContext context) =>
                context.Kind == CombatProjectilePresentationEventKind.DirectImpact;
            public void Handle(in CombatProjectilePresentationEventContext context) => HandleCount++;
        }

        private static GameObject CreateLifecyclePrefab(string name)
        {
            GameObject prefab = new GameObject(name);
            GameObject build = new GameObject("Build");
            GameObject rebuild = new GameObject("Rebuild");
            GameObject loop = new GameObject("Loop");
            GameObject complete = new GameObject("Complete");
            GameObject dissipate = new GameObject("Dissipate");
            build.transform.SetParent(prefab.transform, false);
            rebuild.transform.SetParent(prefab.transform, false);
            loop.transform.SetParent(prefab.transform, false);
            complete.transform.SetParent(prefab.transform, false);
            dissipate.transform.SetParent(prefab.transform, false);
            prefab.AddComponent<UnityVfxLifecycleController>().Configure(
                build, rebuild, loop, complete, dissipate, 1, 1, 60, 10, 10,
                UnityVfxLoopPlaybackMode.Continuous, 10);
            return prefab;
        }

        private static ResourceManager CreateResources(GameObject prefab, out MemoryResourceProvider provider)
        {
            provider = new MemoryResourceProvider().Register("prefab", prefab);
            return (ResourceManager)new ResourceManager()
                .RegisterProvider(provider)
                .AddCatalog(new ResourceCatalog("projectile.presentation.test", string.Empty, new[]
                {
                    new ResourceCatalogEntry(
                        ProjectilePrefabKey.Id,
                        ProjectilePrefabKey.TypeId,
                        MemoryResourceProvider.Id,
                        "prefab")
                }));
        }

        private static CombatProjectileUnityPresentationRegistry CreateRegistry()
        {
            return new CombatProjectileUnityPresentationRegistry(new[]
            {
                new CombatProjectileUnityPresentationBindingV2(
                    1001,
                    CombatProjectileUnityPresentationBindingV2.DefaultViewNamePrefix,
                    CombatProjectileUnityPresentationExitPolicy.Default,
                    new CombatProjectileUnityPresentationSlot(
                        CombatProjectileUnityPresentationSlotKind.FlightBody,
                        ProjectilePrefabKey,
                        CombatProjectileUnityPresentationOrientation.ProjectileForward,
                        FixVector3.Zero,
                        0))
            });
        }

        private static CombatProjectileRuntimeState CreateState(params CombatProjectileSnapshot[] projectiles)
        {
            return new CombatProjectileRuntimeState(
                CombatProjectileRuntimeState.CurrentSchemaVersion,
                "test.projectile",
                definitionHash: 123L,
                nextProjectileId: 10,
                activeProjectiles: projectiles ?? new CombatProjectileSnapshot[0]);
        }

        private static CombatProjectileSnapshot Snapshot(
            int projectileId,
            FixVector3 position,
            FixVector3 direction,
            int localFrame = 5,
            int ageFrames = 1)
        {
            return new CombatProjectileSnapshot(
                projectileId,
                spawnSequence: projectileId - 1,
                definitionId: 1001,
                definitionStableId: "test.projectile",
                sourceEntityId: new CombatEntityId(1),
                actionId: 2,
                actionInstanceId: 3,
                traceId: 4,
                localFrame: localFrame,
                spawnFrame: CombatFrame.Zero,
                lastFrame: new CombatFrame(1),
                ageFrames: ageFrames,
                position,
                direction);
        }

        private static CombatProjectileActiveView ActiveView(
            int projectileId,
            FixVector3 position,
            FixVector3 direction)
        {
            return new CombatProjectileActiveView(
                projectileId,
                spawnSequence: projectileId - 1,
                definitionId: 1001,
                definitionStableId: "test.projectile",
                sourceEntityId: new CombatEntityId(1),
                actionId: 2,
                actionInstanceId: 3,
                traceId: 4,
                localFrame: 5,
                spawnFrame: CombatFrame.Zero,
                lastFrame: new CombatFrame(1),
                ageFrames: 1,
                position,
                direction,
                origin: FixVector3.Zero,
                hasTargetPoint: false,
                targetPoint: FixVector3.Zero,
                effectiveLifetimeFrames: 0,
                hasTrackingTarget: false,
                trackingTargetEntityId: default,
                acceptedImpactCount: 0);
        }

        private static FixVector3 FixVector3Forward()
        {
            return new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
        }

        private static FixVector3 FixVector3Right()
        {
            return new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
        }

        private static GameObject FindView(string name)
        {
            GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i].name == name)
                    return objects[i];
            }

            return null;
        }

        private sealed class ProjectilePresentationReceiverProbe :
            MonoBehaviour,
            ICombatProjectileUnityPresentationReceiver
        {
            public int UpdateCount { get; private set; }
            public CombatProjectileUnityPresentationUpdate LastUpdate { get; private set; }

            public void OnCombatProjectilePresentationUpdated(in CombatProjectileUnityPresentationUpdate update)
            {
                UpdateCount++;
                LastUpdate = update;
            }
        }
    }
}

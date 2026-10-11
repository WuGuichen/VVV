using System;
using MxFramework.Combat.Projectiles;
using MxFramework.Editor.Authoring;
using MxFramework.Editor.ProjectileAuthoring;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Core.Math;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.ProjectileAuthoring
{
    public sealed class ProjectilePreviewEditorLifecycleTests
    {
        [Test]
        public void PreviewRenderScene_PreservesActiveSceneSelectionDirtyAndRoots()
        {
            Scene before = SceneManager.GetActiveScene();
            string path = before.path;
            bool dirty = before.isDirty;
            int roots = before.rootCount;
            Object selection = Selection.activeObject;
            var scene = new AuthoringPreviewRenderScene();
            try
            {
                Assert.AreNotEqual(before.handle, scene.PreviewScene.handle);
                Assert.AreEqual(0, scene.RootCount);
            }
            finally { scene.Dispose(); }
            Scene after = SceneManager.GetActiveScene();
            Assert.AreEqual(path, after.path);
            Assert.AreEqual(dirty, after.isDirty);
            Assert.AreEqual(roots, after.rootCount);
            Assert.AreSame(selection, Selection.activeObject);
            Assert.IsTrue(scene.IsDisposed);
        }

        [Test]
        public void ProjectilePresenter_PreviewHost_NeverAddsViewsToActiveScene_AndDisposesAll()
        {
            var prefab = new GameObject("ProjectilePreviewHostPrefab");
            Scene active = SceneManager.GetActiveScene();
            int activeRoots = active.rootCount;
            bool dirty = active.isDirty;
            var scene = new AuthoringPreviewRenderScene();
            var host = new ProjectilePreviewUnityObjectHost(scene);
            const string resourceId = "tests.projectile.preview.host";
            var key = new ResourceKey(resourceId, ResourceTypeIds.GameObject);
            var provider = new MemoryResourceProvider().Register("prefab", prefab);
            var resources = (ResourceManager)new ResourceManager()
                .RegisterProvider(provider)
                .AddCatalog(new ResourceCatalog("projectile.preview.host.tests", string.Empty, new[]
                {
                    new ResourceCatalogEntry(key.Id, key.TypeId, MemoryResourceProvider.Id, "prefab")
                }));
            var slot = new CombatProjectileUnityPresentationSlot(
                CombatProjectileUnityPresentationSlotKind.FlightBody,
                key,
                CombatProjectileUnityPresentationOrientation.ProjectileForward,
                FixVector3.Zero,
                0);
            var registry = new CombatProjectileUnityPresentationRegistry(new[]
            {
                new CombatProjectileUnityPresentationBindingV2(
                    1001,
                    "Preview",
                    CombatProjectileUnityPresentationExitPolicy.Default,
                    slot)
            });
            var presenter = new CombatProjectileUnityPresenter(resources, registry, true, host);
            try
            {
                presenter.PrewarmBindings(1);
                Assert.AreEqual(1, host.OwnedCount);
                Assert.AreEqual(1, scene.RootCount);
                Assert.AreEqual(scene.PreviewScene.handle, scene.PreviewScene.GetRootGameObjects()[0].scene.handle);
                Assert.AreEqual(activeRoots, active.rootCount);
                Assert.AreEqual(dirty, active.isDirty);
            }
            finally
            {
                presenter.Dispose();
                Assert.AreEqual(0, host.OwnedCount);
                host.Dispose();
                scene.Dispose();
                Object.DestroyImmediate(prefab);
            }
            Assert.AreEqual(activeRoots - 1, active.rootCount);
        }

        [Test]
        public void SlotAudition_PlaysAndAdvancesParticlesWithoutAProjectileGameplayEvent()
        {
            var prefab = new GameObject("ProjectileSlotAuditionPrefab");
            prefab.AddComponent<ParticleSystem>();
            var scene = new AuthoringPreviewRenderScene();
            var host = new ProjectilePreviewUnityObjectHost(scene);
            var audition = new ProjectilePreviewSlotAudition(scene, host);
            var slot = new CombatProjectileUnityPresentationSlot(
                CombatProjectileUnityPresentationSlotKind.AreaImpact,
                new ResourceKey("tests.projectile.slot-audition", ResourceTypeIds.GameObject),
                CombatProjectileUnityPresentationOrientation.ImpactNormalOrWorldUp,
                FixVector3.Zero,
                60,
                91001);
            try
            {
                Assert.IsTrue(audition.Play(prefab, in slot));
                Assert.IsTrue(audition.IsActive);
                Assert.IsTrue(audition.IsPlaying);
                Assert.AreEqual(1, host.OwnedCount);

                audition.AdvanceForTests(.1f);

                Assert.That(audition.ElapsedSeconds, Is.GreaterThan(0f));
                Assert.That(audition.FirstParticleTimeForTests, Is.GreaterThan(0f));

                scene.Orbit(new Vector2(18f, -7f));
                scene.Pan(new Vector2(12f, -5f));
                scene.Zoom(2f);
                Vector2 orbit = scene.OrbitAngles;
                Vector3 pivot = scene.Pivot;
                float distance = scene.Distance;

                Assert.IsTrue(audition.Play(prefab, in slot));
                Assert.That(scene.OrbitAngles, Is.EqualTo(orbit));
                Assert.That(scene.Pivot, Is.EqualTo(pivot));
                Assert.That(scene.Distance, Is.EqualTo(distance));
            }
            finally
            {
                audition.Dispose();
                host.Dispose();
                scene.Dispose();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void SlotAudition_StartsAndAdvancesBakedLifecycle()
        {
            var prefab = new GameObject("ProjectileBakedLifecycleAuditionPrefab");
            var loopRoot = new GameObject("Loop");
            loopRoot.transform.SetParent(prefab.transform, false);
            ParticleSystem particles = loopRoot.AddComponent<ParticleSystem>();
            var scheduler = prefab.AddComponent<UnityVfxBakedLayerScheduler>();
            var entry = new UnityVfxBakedLayerScheduleEntry();
            entry.Configure(loopRoot, UnityVfxLifecyclePhase.Loop, 0, 60);
            scheduler.Configure(new[] { entry });
            UnityVfxLifecycleController lifecycle = prefab.AddComponent<UnityVfxLifecycleController>();
            lifecycle.Configure(null, null, loopRoot, null, null, 0, 0, 60, 0, 0,
                UnityVfxLoopPlaybackMode.OneShot);

            var scene = new AuthoringPreviewRenderScene();
            var host = new ProjectilePreviewUnityObjectHost(scene);
            var audition = new ProjectilePreviewSlotAudition(scene, host);
            var slot = new CombatProjectileUnityPresentationSlot(
                CombatProjectileUnityPresentationSlotKind.AreaImpact,
                new ResourceKey("tests.projectile.baked-lifecycle-audition", ResourceTypeIds.GameObject),
                CombatProjectileUnityPresentationOrientation.ImpactNormalOrWorldUp,
                FixVector3.Zero,
                120,
                91002);
            try
            {
                Assert.IsTrue(audition.Play(prefab, in slot));
                audition.AdvanceForTests(.1f);

                GameObject instanceRoot = Array.Find(
                    scene.PreviewScene.GetRootGameObjects(),
                    value => value.GetComponent<UnityVfxLifecycleController>() != null);
                Assert.That(instanceRoot, Is.Not.Null);
                UnityVfxLifecycleController instanceLifecycle =
                    instanceRoot.GetComponent<UnityVfxLifecycleController>();
                ParticleSystem instanceParticles =
                    instanceRoot.GetComponentInChildren<ParticleSystem>(true);
                Assert.That(instanceLifecycle.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Loop));
                Assert.That(instanceParticles.time, Is.GreaterThan(0f));
            }
            finally
            {
                audition.Dispose();
                host.Dispose();
                scene.Dispose();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WarmedRealBridgePresenterScrub_UsesCachedStateWithoutAllocation()
        {
            ProjectileDefinitionListItem definition = LinearDefinition(48);
            var input = new ProjectilePreviewCompileInput(definition, null, "draft", ProjectilePreviewScenario.Miss, 48,
                FixVector3.Zero, new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.Zero), "source", "definitions[0]");
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.IsTrue(sandbox.Recompile(input), sandbox.DiagnosticCode);

            var prefab = new GameObject("ProjectilePreviewAllocationPrefab");
            var scene = new AuthoringPreviewRenderScene();
            var host = new ProjectilePreviewUnityObjectHost(scene);
            var key = new ResourceKey("tests.projectile.preview.allocation", ResourceTypeIds.GameObject);
            var provider = new MemoryResourceProvider().Register("prefab", prefab);
            var resources = (ResourceManager)new ResourceManager().RegisterProvider(provider).AddCatalog(
                new ResourceCatalog("projectile.preview.allocation", string.Empty, new[]
                {
                    new ResourceCatalogEntry(key.Id, key.TypeId, MemoryResourceProvider.Id, "prefab")
                }));
            var binding = new CombatProjectileUnityPresentationBindingV2(901, "Preview",
                CombatProjectileUnityPresentationExitPolicy.Default,
                new CombatProjectileUnityPresentationSlot(CombatProjectileUnityPresentationSlotKind.FlightBody, key,
                    CombatProjectileUnityPresentationOrientation.ProjectileForward, FixVector3.Zero, 0));
            var registry = new CombatProjectileUnityPresentationRegistry(new[] { binding });
            var presenter = new CombatProjectileUnityPresenter(resources, registry, true, host);
            var vfx = new VfxRuntimeModule(new VfxDefinitionRegistry(), new RuntimeCommandBuffer());
            var bridge = new CombatProjectileVfxBridge(registry, vfx, presenter, trackingCapacity: 4, correlationCapacity: 16);
            try
            {
                presenter.PrewarmBindings(1);
                for (int i = 0; i < 128; i++)
                    sandbox.RoutePresentationFrame(1 + i % 20, false, bridge, null);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 4096; i++)
                    sandbox.RoutePresentationFrame(1 + i % 20, false, bridge, null);
                Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
                Assert.AreEqual(1, presenter.ActiveCount);
            }
            finally
            {
                bridge.Reset();
                presenter.Dispose();
                host.Dispose();
                scene.Dispose();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WarmedNonEmptyForwardRuntimeBridgePresenterAndVfxRoute_AllocatesZeroFor4096Frames()
        {
            ProjectileDefinitionListItem definition = LinearDefinition(4200);
            definition.detail.collision.stopOnFirstAcceptedImpact = false;
            definition.detail.collision.maxAcceptedImpacts = 2;
            definition.detail.collision.hitSameTargetPolicy = "OncePerProjectile";
            var input = new ProjectilePreviewCompileInput(definition, null, "draft", ProjectilePreviewScenario.DirectHit, 4200,
                FixVector3.Zero, new FixVector3(Fix64.FromInt(2000), Fix64.Zero, Fix64.Zero), "source", "definitions[0]");
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.IsTrue(sandbox.Recompile(input), sandbox.DiagnosticCode);

            var prefab = new GameObject("ProjectilePreviewForwardAllocationPrefab");
            var scene = new AuthoringPreviewRenderScene();
            var host = new ProjectilePreviewUnityObjectHost(scene);
            var key = new ResourceKey("tests.projectile.preview.forward", ResourceTypeIds.GameObject);
            var provider = new MemoryResourceProvider().Register("prefab", prefab);
            var resources = (ResourceManager)new ResourceManager().RegisterProvider(provider).AddCatalog(
                new ResourceCatalog("projectile.preview.forward", string.Empty, new[]
                {
                    new ResourceCatalogEntry(key.Id, key.TypeId, MemoryResourceProvider.Id, "prefab")
                }));
            var binding = new CombatProjectileUnityPresentationBindingV2(901, "Preview",
                CombatProjectileUnityPresentationExitPolicy.Default,
                new CombatProjectileUnityPresentationSlot(CombatProjectileUnityPresentationSlotKind.FlightBody, key,
                    CombatProjectileUnityPresentationOrientation.ProjectileForward, FixVector3.Zero, 0));
            var registry = new CombatProjectileUnityPresentationRegistry(new[] { binding });
            var presenter = new CombatProjectileUnityPresenter(resources, registry, true, host);
            var vfx = new VfxRuntimeModule(new VfxDefinitionRegistry(), new RuntimeCommandBuffer());
            var bridge = new CombatProjectileVfxBridge(registry, vfx, presenter, trackingCapacity: 4, correlationCapacity: 32);
            var drained = new System.Collections.Generic.List<VfxRuntimeEvent>(4);
            Action<RuntimeFrame> tickVfx = frame =>
            {
                vfx.Tick(new RuntimeTickContext(frame.Value, 0d, 0d, RuntimeTickStage.PostSimulation));
                drained.Clear();
                vfx.DrainEvents(frame, drained);
            };
            try
            {
                presenter.PrewarmBindings(1);
                for (int frame = 0; frame < 64; frame++)
                    sandbox.RoutePresentationFrame(frame, true, bridge, tickVfx);
                bridge.Reset();
                vfx = new VfxRuntimeModule(new VfxDefinitionRegistry(), new RuntimeCommandBuffer());
                bridge = new CombatProjectileVfxBridge(registry, vfx, presenter, trackingCapacity: 4, correlationCapacity: 32);
                sandbox.RoutePresentationFrame(0, false, bridge, null);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int frame = 1; frame <= 4096; frame++)
                    sandbox.RoutePresentationFrame(frame, true, bridge, tickVfx);
                Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
                Assert.AreEqual(1, presenter.ActiveCount);
                Assert.GreaterOrEqual(bridge.GetDiagnostics().MissingSlots, 1);
            }
            finally
            {
                bridge.Reset();
                presenter.Dispose();
                host.Dispose();
                scene.Dispose();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void PreviewSession_ForwardBackwardRestart_ReusesRuntimeIdentityAndAllocatesZeroWhenWarmed()
        {
            ProjectileDefinitionListItem definition = LinearDefinition(8);
            var input = new ProjectilePreviewCompileInput(definition, null, "draft", ProjectilePreviewScenario.Miss, 8,
                FixVector3.Zero, new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero), "source", "definitions[0]");
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.That(sandbox.Recompile(input), Is.True, sandbox.DiagnosticCode);
            var binding = new ProjectilePresentationBindingListItem
            {
                projectileDefinitionId = 901,
                detail = new ProjectilePresentationBindingView
                {
                    projectileDefinitionId = 901,
                    slots = new ProjectilePresentationSlotsView
                    {
                        spawn = new ProjectilePresentationSlotView
                        {
                            resource = new ProjectileResourceKeyView { id = "tests.preview.session.spawn", typeId = ResourceTypeIds.GameObject },
                            orientation = "ProjectileForward",
                            lifetimeFramesOverride = 2,
                            vfxDefinitionId = 81
                        }
                    },
                    exitPolicy = new ProjectilePresentationExitPolicyView
                    {
                        acceptedImpact = "Complete", blockedImpact = "Dissipate", expiration = "Complete",
                        externalRemoval = "Dissipate", runtimeReset = "Immediate"
                    }
                }
            };
            using var scene = new AuthoringPreviewRenderScene();
            using var session = new ProjectilePreviewPresentationSession(scene, binding);
            session.Restart(sandbox);
            Assert.That(session.TryGetVfxInstance(1, out VfxInstanceSnapshot first), Is.True);
            session.RouteFrame(sandbox, 0, false);
            Assert.That(session.ActiveVfxCount, Is.Zero);
            session.Restart(sandbox);
            Assert.That(session.TryGetVfxInstance(1, out VfxInstanceSnapshot replay), Is.True);
            Assert.That(replay.InstanceId, Is.EqualTo(first.InstanceId));
            for (int i = 0; i < 64; i++) { session.RouteFrame(sandbox, 0, false); session.Restart(sandbox); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4096; i++) { session.RouteFrame(sandbox, 0, false); session.Restart(sandbox); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        [Test]
        public void SceneEdit_RegistersOnlyExplicitly_AndCommitsOneTypedMutation()
        {
            int commits = 0;
            ProjectileAuthoringFieldEditView committed = null;
            var edit = new ProjectileSceneViewEdit(value => { commits++; committed = value; }, () => null, () => null);
            Assert.IsFalse(edit.IsRegistered);
            edit.Begin();
            Assert.IsTrue(edit.IsRegistered);
            edit.StageForTests(new ProjectileAuthoringFieldEditView { fieldPath = "collision.radius", valueKind = "Float", floatValue = .5f });
            edit.CommitPendingForTests();
            Assert.AreEqual(1, commits);
            Assert.AreEqual("collision.radius", committed.fieldPath);
            edit.End(cancelPending: true);
            Assert.IsFalse(edit.IsRegistered);
            edit.Dispose();
        }

        private static ProjectileDefinitionListItem LinearDefinition(int lifetime)
        {
            return new ProjectileDefinitionListItem
            {
                stableId = "preview.test",
                definitionId = 901,
                detail = new ProjectileDefinitionDetailView
                {
                    definitionId = 901,
                    stableId = "preview.test",
                    motion = new ProjectileMotionView { kind = "Linear", speedPerFrame = .25f, lifetimeFrames = lifetime,
                        speedProfile = new ProjectileSpeedProfileView { kind = "Constant" } },
                    collision = new ProjectileCollisionView { radius = .1f, layerMask = 1 << 1,
                        stopOnFirstAcceptedImpact = true, stopOnBlockedImpact = true, maxAcceptedImpacts = 1,
                        hitSameTargetPolicy = "AllowRepeat" },
                    impact = new ProjectileImpactView { stableEventId = "preview.impact", policyKind = "DirectHit",
                        hitEvent = new CombatHitEventView { targetTypes = new[] { "HostileCharacter" }, environmentHitPolicy = "None", stableEventId = "preview.impact", maxAcceptedHits = 1, hitSameTargetPolicy = "AllowRepeat" },
                        areaHitEvent = new CombatHitEventView { targetTypes = new[] { "HostileCharacter" }, environmentHitPolicy = "None", stableEventId = "preview.area", maxAcceptedHits = 1, hitSameTargetPolicy = "AllowRepeat" } }
                }
            };
        }

        [Test]
        public void SceneEdit_EscapeEquivalentCancel_DropsPendingMutation()
        {
            int commits = 0;
            var edit = new ProjectileSceneViewEdit(_ => commits++, () => null, () => null);
            edit.StageForTests(new ProjectileAuthoringFieldEditView { fieldPath = "motion.arcHeight", valueKind = "Float", floatValue = 2f });
            edit.CancelPendingForTests();
            edit.CommitPendingForTests();
            Assert.AreEqual(0, commits);
            edit.Dispose();
        }

        [Test]
        public void SceneEdit_PreviewTargetIsSessionOnly_CommitAndCancelNeverEmitTypedMutation()
        {
            int mutations = 0;
            int fixtureCommits = 0;
            Vector3 target = new Vector3(6f, 0f, 0f);
            var edit = new ProjectileSceneViewEdit(_ => mutations++, () => LinearDefinition(20), () => null,
                () => target, (value, committed) => { target = value; if (committed) fixtureCommits++; });
            edit.StagePreviewTargetForTests(new Vector3(8f, 1f, 0f));
            edit.End(cancelPending: true);
            Assert.AreEqual(new Vector3(6f, 0f, 0f), target);
            edit.StagePreviewTargetForTests(new Vector3(9f, 0f, 0f));
            edit.CommitPreviewTargetForTests();
            Assert.AreEqual(new Vector3(9f, 0f, 0f), target);
            Assert.AreEqual(1, fixtureCommits);
            Assert.AreEqual(0, mutations);
            edit.Dispose();
        }
    }
}

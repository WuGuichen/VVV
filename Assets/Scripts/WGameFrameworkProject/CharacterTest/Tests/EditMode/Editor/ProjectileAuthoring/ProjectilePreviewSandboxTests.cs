using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Core.Math;
using MxFramework.Editor.Authoring;
using MxFramework.Editor.ProjectileAuthoring;
using NUnit.Framework;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Vfx;

namespace MxFramework.Tests.ProjectileAuthoring
{
    public sealed class ProjectilePreviewSandboxTests
    {
        [TestCase("Linear")]
        [TestCase("ArcToTarget")]
        [TestCase("BoomerangReturn")]
        [TestCase("SwayForward")]
        [TestCase("HomingToTarget")]
        [TestCase("BezierToTarget")]
        [TestCase("BallisticArcToTarget")]
        public void RuntimeMotionCache_UsesFormalRuntimePose(string motionKind)
        {
            ProjectileDefinitionListItem item = Definition(motionKind, lifetime: 12);
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.IsTrue(sandbox.Recompile(Input(item, ProjectilePreviewScenario.Miss, 12)), sandbox.DiagnosticCode);
            Assert.Greater(sandbox.FrameCount, 2);
            ref readonly ProjectilePreviewFrame frame = ref sandbox.GetFrame(1);
            Assert.IsTrue(frame.Active || motionKind == "BallisticArcToTarget", motionKind);
            Assert.AreEqual(1, sandbox.GetFrame(0).SpawnCount, motionKind);
        }

        [TestCase("Linear")]
        [TestCase("ArcToTarget")]
        [TestCase("BoomerangReturn")]
        [TestCase("SwayForward")]
        [TestCase("HomingToTarget")]
        [TestCase("BezierToTarget")]
        [TestCase("BallisticArcToTarget")]
        public void RuntimeMotionCache_MatchesIndependentFormalRuntime(string motionKind)
        {
            ProjectileDefinitionListItem item = Definition(motionKind, 12);
            ProjectilePreviewCompileInput input = Input(item, ProjectilePreviewScenario.Miss, 12);
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.IsTrue(sandbox.Recompile(input), sandbox.DiagnosticCode);

            CombatProjectileDefinition definition = CreateIndependentDefinition(item.detail);
            var spawns = new CombatProjectileSpawnRequestBuffer();
            var runtime = new CombatProjectileRuntime(new CombatProjectileDefinitionRegistry(new[] { definition }), spawns,
                new CombatPhysicsWorld(), GoldenImpactResolver.Instance, trackingTargetProvider: new GoldenMovingTarget(input.Target));
            FixVector3 direction = input.Target - input.Start;
            direction.TryNormalize(out direction);
            spawns.Enqueue(new CombatProjectileSpawnRequest(new CombatFrame(0), definition.DefinitionId, new CombatEntityId(1), input.Start, direction,
                true, input.Target, 1, 1, 1, 0, effectiveLifetimeFrames: 8, hasTrackingTarget: definition.Motion.Kind == CombatProjectileMotionKind.HomingToTarget,
                trackingTargetEntityId: definition.Motion.Kind == CombatProjectileMotionKind.HomingToTarget ? new CombatEntityId(2) : default));
            var events = new CombatProjectileFrameEventBuffer();
            var active = new List<CombatProjectileActiveView>(1);
            for (int frame = 0; frame < sandbox.FrameCount; frame++)
            {
                runtime.Tick(new CombatFrame(frame), events);
                active.Clear(); runtime.CopyActiveViewsTo(active);
                ref readonly ProjectilePreviewFrame cached = ref sandbox.GetFrame(frame);
                Assert.AreEqual(active.Count > 0, cached.Active, motionKind + " frame " + frame);
                if (active.Count > 0)
                {
                    Assert.AreEqual(active[0].Position, cached.Position, motionKind + " frame " + frame);
                    Assert.AreEqual(active[0].Direction, cached.Forward, motionKind + " frame " + frame);
                }
            }
        }

        [TestCase("Constant")]
        [TestCase("EaseInQuadratic")]
        [TestCase("EaseOutQuadratic")]
        [TestCase("EaseInOutSmoothstep")]
        public void SpeedProfiles_AreProjectedIntoFormalRuntime(string speedProfile)
        {
            ProjectileDefinitionListItem item = Definition("ArcToTarget", lifetime: 10);
            item.detail.motion.speedProfile.kind = speedProfile;
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.IsTrue(sandbox.Recompile(Input(item, ProjectilePreviewScenario.Miss, 10)), sandbox.DiagnosticCode);
            Assert.IsTrue(sandbox.GetFrame(2).Position.X > Fix64.Zero, speedProfile);
        }

        [Test]
        public void Scenarios_RecordStableLifecycleCardinality()
        {
            AssertScenario(ProjectilePreviewScenario.Miss, direct: 0, blocked: 0, area: 0, expire: 1);
            AssertScenario(ProjectilePreviewScenario.DirectHit, direct: 1, blocked: 0, area: 0, expire: 0);
            AssertScenario(ProjectilePreviewScenario.Blocked, direct: 0, blocked: 1, area: 0, expire: 0);
            AssertScenario(ProjectilePreviewScenario.AreaHit, direct: 0, blocked: 0, area: 1, expire: 0);
            AssertScenario(ProjectilePreviewScenario.Expire, direct: 0, blocked: 0, area: 1, expire: 1);
        }

        [Test]
        public void Pierce_KeepsFlightUntilTerminalContact()
        {
            ProjectileDefinitionListItem item = Definition("Linear", lifetime: 10);
            item.detail.collision.maxAcceptedImpacts = 2;
            item.detail.collision.stopOnFirstAcceptedImpact = false;
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.IsTrue(sandbox.Recompile(Input(item, ProjectilePreviewScenario.Pierce, 10)), sandbox.DiagnosticCode);
            int direct = Sum(sandbox, f => f.DirectCount);
            Assert.AreEqual(2, direct);
            Assert.AreEqual(1, Sum(sandbox, f => f.PersistentExit ? 1 : 0));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        public void AreaScenario_TargetCardinalityDoesNotMultiplySourceMarker(int targetCount)
        {
            ProjectileDefinitionListItem item = Definition("Linear", 10);
            item.detail.impact.policyKind = "AreaPulseOnImpact";
            item.detail.impact.areaLayerMask = 1 << 2;
            using var sandbox = new ProjectilePreviewSandbox();
            ProjectilePreviewCompileInput input = new ProjectilePreviewCompileInput(item, null, "draft", ProjectilePreviewScenario.AreaHit, 10,
                FixVector3.Zero, new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero), "source", "definitions[0]", targetCount);
            Assert.IsTrue(sandbox.Recompile(input), sandbox.DiagnosticCode);
            Assert.AreEqual(1, Sum(sandbox, f => f.AreaCount));
        }

        [Test]
        public void LateAttach_RebuildsPersistentWithoutHistoricalOneShots()
        {
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.IsTrue(sandbox.Recompile(Input(Definition("Linear", 10), ProjectilePreviewScenario.LateAttach, 10)), sandbox.DiagnosticCode);
            Assert.AreEqual(0, Sum(sandbox, f => f.SpawnCount));
            Assert.AreEqual(1, Sum(sandbox, f => f.PersistentRebuild ? 1 : 0));
        }

        [Test]
        public void RealBridge_NormalForwardRoutesOneShot_SeekAndRepaintDoNotReplayIt()
        {
            ProjectileDefinitionListItem item = Definition("Linear", 10);
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.IsTrue(sandbox.Recompile(Input(item, ProjectilePreviewScenario.Miss, 10)), sandbox.DiagnosticCode);
            var key = new ResourceKey("tests.projectile.preview.spawn", ResourceTypeIds.GameObject);
            var binding = new CombatProjectileUnityPresentationBindingV2(item.definitionId, "Preview",
                CombatProjectileUnityPresentationExitPolicy.Default,
                new CombatProjectileUnityPresentationSlot(CombatProjectileUnityPresentationSlotKind.Spawn, key,
                    CombatProjectileUnityPresentationOrientation.ProjectileForward, FixVector3.Zero, 1, 71));
            var registry = new CombatProjectileUnityPresentationRegistry(new[] { binding });
            var definitions = new VfxDefinitionRegistry();
            Assert.IsTrue(definitions.TryRegister(new VfxDefinition(new VfxDefinitionId(71), "spawn", key, 1), out string failure), failure);
            var vfx = new VfxRuntimeModule(definitions, new RuntimeCommandBuffer());
            var bridge = new CombatProjectileVfxBridge(registry, vfx, trackingCapacity: 4, correlationCapacity: 16);
            var events = new List<VfxRuntimeEvent>(4);
            int spawned = 0;
            Action<RuntimeFrame> drain = frame =>
            {
                vfx.Tick(new RuntimeTickContext(frame.Value, 0d, 0d, RuntimeTickStage.PostSimulation));
                events.Clear();
                vfx.DrainEvents(frame, events);
                for (int index = 0; index < events.Count; index++)
                    if (events[index].Type == VfxRuntimeEventType.Spawned) spawned++;
            };

            sandbox.ResetPresentationReplay(bridge);
            sandbox.RoutePresentationFrame(0, true, bridge, drain);
            Assert.AreEqual(1, spawned);
            for (int i = 0; i < 128; i++)
                sandbox.RoutePresentationFrame(0, false, bridge, drain);
            Assert.AreEqual(1, spawned);
        }

        [Test]
        public void InMemoryDraftHashAndMismatchDiagnostic_AreStable()
        {
            using var sandbox = new ProjectilePreviewSandbox();
            ProjectilePreviewCompileInput input = Input(Definition("Linear", 10), ProjectilePreviewScenario.Miss, 10, "draft-not-disk");
            Assert.IsTrue(sandbox.Recompile(input), sandbox.DiagnosticCode);
            Assert.AreEqual("draft-not-disk", sandbox.DraftCompileHash);
            ProjectilePreviewFrame wrong = new ProjectilePreviewFrame(1, false, false, FixVector3.Zero, FixVector3.Zero, 0, 0, 0, 0, 0, 0, 0, false, false, false);
            Assert.IsFalse(sandbox.VerifyFrame(1, in wrong));
            Assert.AreEqual("PROJAUTH023", sandbox.DiagnosticCode);
        }

        [Test]
        public void RuntimeDefinitionProjection_PreservesAllCompiledFieldsWithoutNormalization()
        {
            ProjectileDefinitionDetailView detail = Definition("BezierToTarget", 17).detail;
            detail.physicalEventsEnabled = true;
            detail.motion.speedPerFrame = .625f;
            detail.motion.speedProfile.kind = "EaseInOutSmoothstep";
            detail.collision.radius = .375f;
            detail.collision.layerMask = unchecked((int)0x80000005);
            detail.collision.stopOnFirstAcceptedImpact = false;
            detail.collision.stopOnBlockedImpact = false;
            detail.collision.maxAcceptedImpacts = 7;
            detail.collision.hitSameTargetPolicy = "OncePerProjectile";
            detail.impact.presentationProfileId = "presentation.test";
            detail.impact.effectTimelineId = "effect.exact";
            detail.impact.hitEvent.targetTypes = new[] { "HostileCharacter", "WorldStatic" };
            detail.impact.hitEvent.environmentHitPolicy = "BlockedPresentation";
            detail.impact.hitEvent.hitSameTargetPolicy = "OncePerEvent";
            detail.impact.hitEvent.damage = new CombatHitDamageView { damageTypeId = "fire", baseDamage = 9, impactPower = 3, launchPower = 2, tags = new[] { "projectile", "magic" }, analysisCategory = "burst" };
            detail.impact.hitEvent.onHitBuffs = new[] { new CombatHitBuffView { buffId = 7 }, new CombatHitBuffView { buffId = 11 } };

            CombatProjectileDefinition actual = ProjectilePreviewSandbox.CreateRuntimeDefinition(detail, ProjectilePreviewScenario.Miss);
            CombatProjectileDefinition expected = CreateIndependentDefinition(detail);

            Assert.That(actual.DefinitionId, Is.EqualTo(expected.DefinitionId));
            Assert.That(actual.StableId, Is.EqualTo(expected.StableId));
            Assert.That(actual.PhysicalEventsEnabled, Is.EqualTo(expected.PhysicalEventsEnabled));
            Assert.That(actual.Motion, Is.EqualTo(expected.Motion));
            Assert.That(actual.Collision, Is.EqualTo(expected.Collision));
            Assert.That(actual.Impact, Is.EqualTo(expected.Impact));
        }

        [Test]
        public void InvalidDraftProjection_FailsClosedWithProjAuth021()
        {
            ProjectileDefinitionListItem item = Definition("Linear", 10);
            item.detail.motion.speedPerFrame = 0f;
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.That(sandbox.Recompile(Input(item, ProjectilePreviewScenario.Miss, 10)), Is.False);
            Assert.That(sandbox.DiagnosticCode, Is.EqualTo("PROJAUTH021"));
        }

        [Test]
        public void SpawnResolver_UsesSocketPoseAndSeparatelyAppliesLocalOffset()
        {
            ProjectileDefinitionListItem item = Definition("Linear", 20);
            ProjectileRequestVariantListItem request = Request("CasterForwardRange", maxRange: 5f);
            request.detail.origin.localOffset = new ProjectileVector3View { x = 1f, y = 2f, z = 3f };
            var fixture = new ProjectilePreviewSpawnFixture(FixVector3.Zero, X(), true,
                new FixVector3(Fix64.FromInt(10), Fix64.Zero, Fix64.Zero), X(), false, FixVector3.Zero);
            var input = new ProjectilePreviewCompileInput(item, request, "draft", ProjectilePreviewScenario.Miss, 20,
                FixVector3.Zero, new FixVector3(Fix64.FromInt(5), Fix64.Zero, Fix64.Zero), "source", "object", 3, fixture);
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.That(sandbox.Recompile(input), Is.True, sandbox.DiagnosticCode);
            Assert.That(sandbox.SpawnResolution.SocketFallback, Is.False);
            Assert.That(sandbox.SpawnResolution.Origin, Is.EqualTo(new FixVector3(Fix64.FromInt(13), Fix64.FromInt(2), Fix64.Zero - Fix64.One)));
        }

        [Test]
        public void SpawnResolver_MissingSocketUsesFallbackRootPlusLocalAndReportsDiagnostic()
        {
            ProjectileDefinitionListItem item = Definition("Linear", 20);
            ProjectileRequestVariantListItem request = Request("CasterForwardRange", maxRange: 5f);
            request.detail.origin.localOffset = new ProjectileVector3View { z = 1f };
            request.detail.origin.fallbackRootOffset = new ProjectileVector3View { x = 2f };
            var fixture = new ProjectilePreviewSpawnFixture(new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero), X(), false,
                FixVector3.Zero, X(), false, FixVector3.Zero);
            var input = new ProjectilePreviewCompileInput(item, request, "draft", ProjectilePreviewScenario.Miss, 20,
                FixVector3.Zero, new FixVector3(Fix64.FromInt(5), Fix64.Zero, Fix64.Zero), "source", "object", 3, fixture);
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.That(sandbox.Recompile(input), Is.True, sandbox.DiagnosticCode);
            Assert.That(sandbox.SpawnResolution.Origin, Is.EqualTo(new FixVector3(Fix64.FromInt(5), Fix64.Zero, Fix64.FromInt(-2))));
            Assert.That(sandbox.SpawnResolution.DiagnosticCode, Is.EqualTo("PROJAUTH022"));
        }

        [TestCase("CasterForwardRange", false, false)]
        [TestCase("LockedTargetCenter", true, false)]
        [TestCase("LockedTargetOrCasterForwardRange", false, true)]
        public void SpawnResolver_CoversAllAimKindsAndLockedFallback(string aimKind, bool hasTarget, bool expectsFallback)
        {
            ProjectileDefinitionListItem item = Definition("Linear", 20);
            ProjectileRequestVariantListItem request = Request(aimKind, maxRange: 5f);
            request.detail.aim.targetHeightOffset = 1.5f;
            var fixture = new ProjectilePreviewSpawnFixture(FixVector3.Zero, X(), false, FixVector3.Zero, X(), hasTarget,
                new FixVector3(Fix64.FromInt(3), Fix64.FromInt(2), Fix64.Zero));
            var input = new ProjectilePreviewCompileInput(item, request, "draft", ProjectilePreviewScenario.Miss, 20,
                FixVector3.Zero, new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero), "source", "object", 3, fixture);
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.That(sandbox.Recompile(input), Is.True, sandbox.DiagnosticCode);
            Assert.That(sandbox.SpawnResolution.LockedTargetFallback, Is.EqualTo(expectsFallback));
            if (hasTarget)
                Assert.That(sandbox.SpawnResolution.TargetPoint.Y, Is.EqualTo(Fix64.FromRaw((long)(3.5f * Fix64.Scale))));
            else
                Assert.That(sandbox.SpawnResolution.TargetPoint.X, Is.EqualTo(Fix64.FromInt(5)));
        }

        [Test]
        public void DefinitionOnlyPreview_UsesVisibleSyntheticProfileDiagnostic()
        {
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.That(sandbox.Recompile(Input(Definition("Linear", 10), ProjectilePreviewScenario.Miss, 10)), Is.True, sandbox.DiagnosticCode);
            Assert.That(sandbox.SpawnResolution.SyntheticProfile, Is.True);
            Assert.That(sandbox.SpawnResolution.DiagnosticCode, Is.EqualTo("PROJAUTH022"));
        }

        [Test]
        public void PresentationTrace_IsIndependentAndCarriesSevenSlotIdentityVfxLifetimePoseAndExit()
        {
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.That(sandbox.Recompile(Input(Definition("Linear", 3), ProjectilePreviewScenario.Miss, 4)), Is.True, sandbox.DiagnosticCode);
            ProjectilePresentationBindingListItem binding = PresentationBinding();
            sandbox.BuildPresentationTrace(binding);

            Assert.That(sandbox.PresentationTraceCount, Is.EqualTo(6));
            var slots = new HashSet<int>();
            bool sawExit = false;
            for (int index = 0; index < sandbox.PresentationTraceCount; index++)
            {
                ref readonly ProjectilePreviewPresentationTrace trace = ref sandbox.GetPresentationTrace(index);
                slots.Add(trace.Identity.SlotKind);
                Assert.That(trace.ResourceKey, Does.Contain("preview.slot"));
                bool persistent = trace.Identity.SlotKind == (int)CombatProjectileUnityPresentationSlotKind.FlightBody
                    || trace.Identity.SlotKind == (int)CombatProjectileUnityPresentationSlotKind.Trail;
                Assert.That(trace.VfxDefinitionId, persistent ? Is.GreaterThanOrEqualTo(0) : Is.GreaterThan(0));
                Assert.That(trace.LifetimeFrames, Is.EqualTo(persistent ? 0 : 5));
                if (trace.Phase == ProjectilePreviewPresentationPhase.Exit)
                {
                    sawExit = true;
                    Assert.That(trace.ExitMode, Is.EqualTo("Complete"));
                }
            }
            CollectionAssert.IsSubsetOf(new[]
            {
                (int)CombatProjectileUnityPresentationSlotKind.Spawn,
                (int)CombatProjectileUnityPresentationSlotKind.FlightBody,
                (int)CombatProjectileUnityPresentationSlotKind.Trail,
                (int)CombatProjectileUnityPresentationSlotKind.Expire
            }, slots);
            Assert.That(sawExit, Is.True);
        }

        [Test]
        public void PresentationTrace_LateAttachUsesRebuildWithoutHistoricalOneShots()
        {
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.That(sandbox.Recompile(Input(Definition("Linear", 6), ProjectilePreviewScenario.LateAttach, 6)), Is.True, sandbox.DiagnosticCode);
            sandbox.BuildPresentationTrace(PresentationBinding());
            int rebuild = 0, historicalSpawn = 0;
            for (int index = 0; index < sandbox.PresentationTraceCount; index++)
            {
                ProjectilePreviewPresentationPhase phase = sandbox.GetPresentationTrace(index).Phase;
                if (phase == ProjectilePreviewPresentationPhase.Rebuild) rebuild++;
                if (phase == ProjectilePreviewPresentationPhase.OneShot
                    && sandbox.GetPresentationTrace(index).Identity.SlotKind == (int)CombatProjectileUnityPresentationSlotKind.Spawn)
                    historicalSpawn++;
            }
            Assert.That(rebuild, Is.EqualTo(2));
            Assert.That(historicalSpawn, Is.Zero);
        }

        [Test]
        public void PresentationTrace_ExpirationTriggeredAreaUsesExpirationExitPolicy()
        {
            ProjectileDefinitionListItem item = Definition("Linear", 3);
            item.detail.impact.policyKind = "AreaPulseOnImpact";
            item.detail.impact.triggerOnExpire = true;
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.That(sandbox.Recompile(Input(item, ProjectilePreviewScenario.Expire, 4)), Is.True, sandbox.DiagnosticCode);
            ProjectilePresentationBindingListItem binding = PresentationBinding();
            binding.detail.exitPolicy.acceptedImpact = "Complete";
            binding.detail.exitPolicy.expiration = "Dissipate";
            sandbox.BuildPresentationTrace(binding);

            int exitCount = 0;
            for (int index = 0; index < sandbox.PresentationTraceCount; index++)
            {
                ref readonly ProjectilePreviewPresentationTrace trace = ref sandbox.GetPresentationTrace(index);
                if (trace.Phase != ProjectilePreviewPresentationPhase.Exit)
                    continue;
                exitCount++;
                Assert.That(trace.ExitMode, Is.EqualTo("Dissipate"));
            }
            Assert.That(exitCount, Is.EqualTo(2), "FlightBody and Trail must both use the expiration exit policy.");
        }

        [Test]
        public void PresentationTrace_PoseMatchesActualBridgeVfxAnchorForOrientationAndOffset()
        {
            ProjectileDefinitionListItem item = Definition("Linear", 6);
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.That(sandbox.Recompile(Input(item, ProjectilePreviewScenario.Miss, 6)), Is.True, sandbox.DiagnosticCode);
            ProjectilePresentationBindingListItem authoring = PresentationBinding();
            authoring.detail.slots.spawn.orientation = "WorldUp";
            authoring.detail.slots.spawn.localOffset = new ProjectileVector3View { x = 1f, y = 2f, z = 3f };
            sandbox.BuildPresentationTrace(authoring);
            ProjectilePreviewPresentationTrace trace = default;
            for (int index = 0; index < sandbox.PresentationTraceCount; index++)
                if (sandbox.GetPresentationTrace(index).Identity.SlotKind == (int)CombatProjectileUnityPresentationSlotKind.Spawn)
                    trace = sandbox.GetPresentationTrace(index);

            Assert.That(ProjectilePreviewPresentationSession.TryCreateSlot(authoring.detail.slots.spawn,
                CombatProjectileUnityPresentationSlotKind.Spawn, out CombatProjectileUnityPresentationSlot spawnSlot), Is.True);
            var registry = new CombatProjectileUnityPresentationRegistry(new[]
            {
                new CombatProjectileUnityPresentationBindingV2(item.definitionId, "Preview",
                    CombatProjectileUnityPresentationExitPolicy.Default, spawnSlot)
            });
            var definitions = new VfxDefinitionRegistry();
            Assert.That(definitions.TryRegister(new VfxDefinition(new VfxDefinitionId(spawnSlot.VfxDefinitionId), "spawn", spawnSlot.Resource, 5), out string failure), Is.True, failure);
            var vfx = new VfxRuntimeModule(definitions, new RuntimeCommandBuffer());
            var bridge = new CombatProjectileVfxBridge(registry, vfx, trackingCapacity: 4, correlationCapacity: 16);
            var events = new List<VfxRuntimeEvent>();
            sandbox.ResetPresentationReplay(bridge);
            sandbox.RoutePresentationFrame(0, true, bridge, frame =>
            {
                vfx.Tick(new RuntimeTickContext(frame.Value, 0d, 0d, RuntimeTickStage.PostSimulation));
                events.Clear();
                vfx.DrainEvents(frame, events);
            });
            VfxRuntimeEvent spawned = events.Find(value => value.Type == VfxRuntimeEventType.Spawned);
            Assert.That(spawned.Anchor.Pose.Position.RawX, Is.EqualTo(trace.Position.X.RawValue));
            Assert.That(spawned.Anchor.Pose.Position.RawY, Is.EqualTo(trace.Position.Y.RawValue));
            Assert.That(spawned.Anchor.Pose.Position.RawZ, Is.EqualTo(trace.Position.Z.RawValue));
            Assert.That(spawned.Anchor.Pose.Forward.RawX, Is.EqualTo(trace.Forward.X.RawValue));
            Assert.That(spawned.Anchor.Pose.Forward.RawY, Is.EqualTo(trace.Forward.Y.RawValue));
            Assert.That(spawned.Anchor.Pose.Forward.RawZ, Is.EqualTo(trace.Forward.Z.RawValue));
        }

        [Test]
        public void WarmedPlaybackAndScrub_AllocateZeroBytes()
        {
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.IsTrue(sandbox.Recompile(Input(Definition("Linear", 40), ProjectilePreviewScenario.DirectHit, 40)), sandbox.DiagnosticCode);
            long before = GC.GetAllocatedBytesForCurrentThread();
            int checksum = 0;
            for (int i = 0; i < 4096; i++) checksum += sandbox.GetFrame(i % sandbox.FrameCount).AgeFrames;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated, checksum.ToString());
        }

        [Test]
        public void SharedPlaybackController_CoversStepSeekLoopSpeedAndDispose()
        {
            var playback = new AuthoringTimelinePlaybackController(60f);
            playback.SetDuration(4);
            playback.Step(1);
            Assert.AreEqual(1, playback.CurrentFrame);
            playback.CyclePlaybackSpeed();
            Assert.AreEqual(2f, playback.PlaybackSpeed);
            playback.Seek(99);
            Assert.AreEqual(3, playback.CurrentFrame);
            playback.Restart();
            Assert.AreEqual(0, playback.CurrentFrame);
            playback.Dispose();
            playback.Play();
            Assert.IsFalse(playback.IsPlaying);
        }

        private static void AssertScenario(ProjectilePreviewScenario scenario, int direct, int blocked, int area, int expire)
        {
            ProjectileDefinitionListItem item = Definition("Linear", scenario == ProjectilePreviewScenario.Expire || scenario == ProjectilePreviewScenario.Miss ? 3 : 10);
            if (scenario == ProjectilePreviewScenario.AreaHit || scenario == ProjectilePreviewScenario.Expire)
            {
                item.detail.impact.policyKind = "AreaPulseOnImpact";
                item.detail.impact.triggerOnExpire = scenario == ProjectilePreviewScenario.Expire;
            }
            if (scenario == ProjectilePreviewScenario.Blocked)
            {
                item.detail.impact.hitEvent.environmentHitPolicy = "BlockedPresentation";
                item.detail.impact.hitEvent.targetTypes = new[] { "HostileCharacter", "WorldStatic" };
            }
            using var sandbox = new ProjectilePreviewSandbox();
            Assert.IsTrue(sandbox.Recompile(Input(item, scenario, 12)), scenario + ": " + sandbox.DiagnosticCode);
            Assert.AreEqual(direct, Sum(sandbox, f => f.DirectCount), scenario.ToString());
            Assert.AreEqual(blocked, Sum(sandbox, f => f.BlockedCount), scenario.ToString());
            Assert.AreEqual(area, Sum(sandbox, f => f.AreaCount), scenario.ToString());
            Assert.AreEqual(expire, Sum(sandbox, f => f.ExpireCount), scenario.ToString());
            Assert.AreEqual(1, Sum(sandbox, f => f.PersistentExit ? 1 : 0), scenario.ToString());
        }

        private static int Sum(ProjectilePreviewSandbox sandbox, Func<ProjectilePreviewFrame, int> selector)
        {
            int result = 0;
            for (int i = 0; i < sandbox.FrameCount; i++) result += selector(sandbox.GetFrame(i));
            return result;
        }

        private static ProjectilePreviewCompileInput Input(ProjectileDefinitionListItem item, ProjectilePreviewScenario scenario, int frames, string hash = "draft")
            => new ProjectilePreviewCompileInput(item, null, hash, scenario, frames, FixVector3.Zero,
                new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero), "config/projectiles/test.json", "definitions[0]");

        private static ProjectileDefinitionListItem Definition(string motionKind, int lifetime)
        {
            return new ProjectileDefinitionListItem
            {
                stableId = "preview.test",
                definitionId = 901,
                detail = new ProjectileDefinitionDetailView
                {
                    definitionId = 901,
                    stableId = "preview.test",
                    motion = new ProjectileMotionView
                    {
                        kind = motionKind,
                        speedPerFrame = motionKind == "BallisticArcToTarget" ? 1.2f : .5f,
                        lifetimeFrames = lifetime,
                        arcHeight = 1.5f,
                        gravityPerFrameSquared = .08f,
                        maxLaunchAngleDegrees = 75f,
                        maxLaunchAngleTangent = .5f,
                        lateralAmplitude = .25f,
                        lateralCycles = 2,
                        bezierControlOffsetA = new ProjectileVector3View { y = 1f },
                        bezierControlOffsetB = new ProjectileVector3View { y = 1f, z = .5f },
                        homing = new ProjectileHomingView { turnRatePerFrame = .2f },
                        speedProfile = new ProjectileSpeedProfileView { kind = "Constant" }
                    },
                    collision = new ProjectileCollisionView
                    {
                        radius = .1f,
                        layerMask = 1 << 1,
                        stopOnFirstAcceptedImpact = true,
                        stopOnBlockedImpact = true,
                        maxAcceptedImpacts = 1,
                        hitSameTargetPolicy = "AllowRepeat"
                    },
                    impact = new ProjectileImpactView
                    {
                        stableEventId = "preview.impact",
                        policyKind = "DirectHit",
                        areaRadius = 1.5f,
                        areaLayerMask = 1 << 1,
                        triggerOnExpire = false,
                        effectTimelineId = "effect.preview"
                        ,hitEvent = new CombatHitEventView { targetTypes = new[] { "HostileCharacter" }, environmentHitPolicy = "None", stableEventId = "preview.impact", maxAcceptedHits = 1, hitSameTargetPolicy = "AllowRepeat" }
                        ,areaHitEvent = new CombatHitEventView { targetTypes = new[] { "HostileCharacter" }, environmentHitPolicy = "None", stableEventId = "preview.area", maxAcceptedHits = 8, hitSameTargetPolicy = "AllowRepeat" }
                    }
                }
            };
        }

        private static ProjectileRequestVariantListItem Request(string aimKind, float maxRange)
        {
            return new ProjectileRequestVariantListItem
            {
                stableId = "preview.request",
                definitionStableId = "preview.test",
                detail = new ProjectileRequestDetailView
                {
                    stableId = "preview.request",
                    projectileStableId = "preview.test",
                    origin = new ProjectileSpawnOriginView
                    {
                        kind = "Socket",
                        socketId = "RightHand",
                        localOffset = new ProjectileVector3View(),
                        fallbackRootOffset = new ProjectileVector3View()
                    },
                    aim = new ProjectileAimView
                    {
                        kind = aimKind,
                        fallbackKind = "CasterForwardRange",
                        maxRange = maxRange
                    }
                }
            };
        }

        private static ProjectilePresentationBindingListItem PresentationBinding()
        {
            ProjectilePresentationSlotView Slot(int id, bool persistent = false) => new ProjectilePresentationSlotView
            {
                resource = new ProjectileResourceKeyView { id = "preview.slot." + id, typeId = ResourceTypeIds.GameObject },
                orientation = "ProjectileForward",
                lifetimeFramesOverride = persistent ? 0 : 5,
                vfxDefinitionId = persistent ? 0 : id
            };
            return new ProjectilePresentationBindingListItem
            {
                projectileDefinitionId = 901,
                detail = new ProjectilePresentationBindingView
                {
                    projectileDefinitionId = 901,
                    slots = new ProjectilePresentationSlotsView
                    {
                        spawn = Slot(1),
                        flightBody = Slot(2, true),
                        trail = Slot(3, true),
                        directImpact = Slot(4),
                        areaImpact = Slot(5),
                        blockedImpact = Slot(6),
                        expire = Slot(7)
                    },
                    exitPolicy = new ProjectilePresentationExitPolicyView
                    {
                        acceptedImpact = "Complete",
                        blockedImpact = "Dissipate",
                        expiration = "Complete",
                        externalRemoval = "Dissipate",
                        runtimeReset = "Immediate"
                    }
                }
            };
        }

        private static CombatProjectileDefinition CreateIndependentDefinition(ProjectileDefinitionDetailView detail)
        {
            var motion = new CombatProjectileMotionConfig(
                (CombatProjectileMotionKind)Enum.Parse(typeof(CombatProjectileMotionKind), detail.motion.kind),
                Fix(detail.motion.speedPerFrame),
                detail.motion.lifetimeFrames,
                Fix(detail.motion.arcHeight),
                (CombatProjectileSpeedProfileKind)Enum.Parse(typeof(CombatProjectileSpeedProfileKind), detail.motion.speedProfile.kind),
                Fix(detail.motion.lateralAmplitude),
                detail.motion.lateralCycles,
                Vector(detail.motion.bezierControlOffsetA),
                Vector(detail.motion.bezierControlOffsetB),
                Fix(detail.motion.homing?.turnRatePerFrame ?? 0f),
                Fix(detail.motion.gravityPerFrameSquared),
                Fix(detail.motion.maxLaunchAngleTangent));
            var collision = new CombatProjectileCollisionConfig(
                Fix(detail.collision.radius),
                new CombatPhysicsLayerMask(detail.collision.layerMask),
                detail.collision.stopOnFirstAcceptedImpact,
                detail.collision.stopOnBlockedImpact,
                detail.collision.maxAcceptedImpacts,
                (CombatProjectileHitSameTargetPolicy)Enum.Parse(typeof(CombatProjectileHitSameTargetPolicy), detail.collision.hitSameTargetPolicy));
            CombatHitEventConfig hit = IndependentHit(detail.impact.hitEvent);
            CombatHitEventConfig areaHit = IndependentHit(detail.impact.areaHitEvent);
            var impact = new CombatProjectileImpactConfig(
                0,
                0,
                (CombatProjectileImpactPolicyKind)Enum.Parse(typeof(CombatProjectileImpactPolicyKind), detail.impact.policyKind),
                Fix(detail.impact.areaRadius),
                new CombatPhysicsLayerMask(detail.impact.areaLayerMask),
                detail.impact.triggerOnExpire,
                hit,
                areaHit,
                0,
                presentationProfileLabel: detail.impact.presentationProfileId,
                stableEventLabel: detail.impact.stableEventId,
                effectTimelineLabel: detail.impact.effectTimelineId);
            return new CombatProjectileDefinition(detail.definitionId, detail.stableId, motion, collision, impact, detail.physicalEventsEnabled);
        }

        private static CombatHitEventConfig IndependentHit(CombatHitEventView view)
        {
            CombatHitTargetTypes targetTypes = CombatHitTargetTypes.None;
            for (int index = 0; index < view.targetTypes.Length; index++)
                targetTypes |= (CombatHitTargetTypes)Enum.Parse(typeof(CombatHitTargetTypes), view.targetTypes[index]);
            CombatHitDamageView sourceDamage = view.damage;
            var damage = sourceDamage == null ? default : new CombatHitDamageConfig(0, sourceDamage.baseDamage,
                sourceDamage.impactPower, Array.Empty<int>(), 0, sourceDamage.launchPower,
                damageTypeLabel: sourceDamage.damageTypeId, analysisCategoryLabel: sourceDamage.analysisCategory,
                tagLabels: sourceDamage.tags);
            var buffs = new CombatHitBuffApplicationConfig[view.onHitBuffs.Length];
            for (int index = 0; index < buffs.Length; index++)
                buffs[index] = new CombatHitBuffApplicationConfig(view.onHitBuffs[index].buffId);
            return new CombatHitEventConfig(targetTypes,
                (CombatEnvironmentHitPolicy)Enum.Parse(typeof(CombatEnvironmentHitPolicy), view.environmentHitPolicy),
                0,
                view.maxAcceptedHits,
                (CombatHitSameTargetPolicy)Enum.Parse(typeof(CombatHitSameTargetPolicy), view.hitSameTargetPolicy),
                damage,
                buffs,
                stableEventLabel: view.stableEventId);
        }

        private static Fix64 Fix(float value) => Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        private static FixVector3 Vector(ProjectileVector3View value) => value == null ? FixVector3.Zero : new FixVector3(Fix(value.x), Fix(value.y), Fix(value.z));
        private static FixVector3 X() => new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);

        private sealed class GoldenImpactResolver : ICombatProjectileImpactResolver
        {
            public static readonly GoldenImpactResolver Instance = new GoldenImpactResolver();
            public bool TryResolveImpact(CombatProjectileSnapshot projectile, CombatQueryResult hit, CombatFrame frame, CombatProjectileImpactContext context, out CombatProjectileImpactPayload payload)
            { payload = new CombatProjectileImpactPayload(1, 0, FixVector3.Zero, HitTargetStateFlags.Alive); return true; }
        }

        private sealed class GoldenMovingTarget : ICombatProjectileTrackingTargetProvider
        {
            private readonly FixVector3 _target;
            public GoldenMovingTarget(FixVector3 target) { _target = target; }
            public bool TryResolveTrackingTarget(CombatProjectileTrackingTargetQuery query, out FixVector3 targetPoint)
            { targetPoint = _target + new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.FromRatio(query.Frame.Value, 10)); return true; }
        }
    }
}

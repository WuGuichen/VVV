using System;
using System.IO;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Vfx;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Projectiles
{
    public sealed class CombatProjectilePresentationV2Tests
    {
        private const string Kind = "mxframework.combat.projectile.presentation-bindings";

        [Test]
        public void JsonV1MapsOnlyFlightBody()
        {
            CombatProjectileUnityPresentationRegistry registry = CombatProjectileUnityPresentationConfigJsonImporter.Import("{\"schemaVersion\":\"1\",\"bindings\":[{\"projectileDefinitionId\":10,\"prefabResource\":{\"id\":\"vfx.test.flight\",\"typeId\":\"GameObject\",\"variant\":\"\",\"packageId\":\"test\"},\"viewNamePrefix\":\"Legacy\"}]}");
            Assert.That(registry.TryGetV2(10, out CombatProjectileUnityPresentationBindingV2 binding), Is.True);
            Assert.That(binding.TryGetSlot(CombatProjectileUnityPresentationSlotKind.FlightBody, out _), Is.True);
            Assert.That(binding.TryGetSlot(CombatProjectileUnityPresentationSlotKind.DirectImpact, out _), Is.False);
        }

        [Test]
        public void JsonAndBinaryV2AreSemanticallyEquivalent()
        {
            const string json = "{\"schemaVersion\":\"2\",\"bindings\":[{\"projectileDefinitionId\":20,\"viewNamePrefix\":\"V2\",\"slots\":{\"spawn\":{\"resource\":{\"id\":\"vfx.test.spawn\",\"typeId\":\"GameObject\",\"variant\":\"\",\"packageId\":\"test\"},\"orientation\":\"WorldUp\",\"localOffset\":{\"x\":1,\"y\":2,\"z\":3},\"lifetimeFramesOverride\":12,\"vfxDefinitionId\":101},\"flightBody\":{\"resource\":{\"id\":\"vfx.test.flight\",\"typeId\":\"GameObject\",\"variant\":\"\",\"packageId\":\"test\"},\"orientation\":\"ProjectileForward\",\"localOffset\":{\"x\":0,\"y\":0,\"z\":0},\"lifetimeFramesOverride\":0}},\"exitPolicy\":{\"acceptedImpact\":\"Complete\",\"blockedImpact\":\"Dissipate\",\"expiration\":\"Complete\",\"externalRemoval\":\"Dissipate\",\"runtimeReset\":\"Immediate\"}}]}";
            CombatProjectileUnityPresentationRegistry jsonRegistry = CombatProjectileUnityPresentationConfigJsonImporter.Import(json);
            RuntimeCatalogBinaryLoadResult<CombatProjectileUnityPresentationRegistry> binary = CombatProjectileUnityPresentationConfigBinaryImporter.ImportBytes(BuildV2Binary());
            Assert.That(binary.Success, Is.True, string.Join("\n", binary.Diagnostics));
            Assert.That(jsonRegistry.TryGetV2(20, out CombatProjectileUnityPresentationBindingV2 fromJson), Is.True);
            Assert.That(binary.Catalog.TryGetV2(20, out CombatProjectileUnityPresentationBindingV2 fromBinary), Is.True);
            Assert.That(fromJson.ExitPolicy.BlockedImpact, Is.EqualTo(fromBinary.ExitPolicy.BlockedImpact));
            Assert.That(fromJson.TryGetSlot(CombatProjectileUnityPresentationSlotKind.Spawn, out CombatProjectileUnityPresentationSlot jsonSpawn), Is.True);
            Assert.That(fromBinary.TryGetSlot(CombatProjectileUnityPresentationSlotKind.Spawn, out CombatProjectileUnityPresentationSlot binarySpawn), Is.True);
            Assert.That(binarySpawn.Resource, Is.EqualTo(jsonSpawn.Resource));
            Assert.That(binarySpawn.VfxDefinitionId, Is.EqualTo(101));
            Assert.That(binarySpawn.LocalOffset, Is.EqualTo(jsonSpawn.LocalOffset));
        }

        [Test]
        public void JsonAndBinaryImportedBindingRouteIdenticalIdPoseLifetimeAndDomain()
        {
            const string json = "{\"schemaVersion\":\"2\",\"bindings\":[{\"projectileDefinitionId\":20,\"viewNamePrefix\":\"V2\",\"slots\":{\"spawn\":{\"resource\":{\"id\":\"vfx.test.spawn\",\"typeId\":\"GameObject\",\"variant\":\"\",\"packageId\":\"test\"},\"orientation\":\"WorldUp\",\"localOffset\":{\"x\":1,\"y\":2,\"z\":3},\"lifetimeFramesOverride\":12,\"vfxDefinitionId\":101},\"flightBody\":{\"resource\":{\"id\":\"vfx.test.flight\",\"typeId\":\"GameObject\",\"variant\":\"\",\"packageId\":\"test\"},\"orientation\":\"ProjectileForward\",\"localOffset\":{\"x\":0,\"y\":0,\"z\":0},\"lifetimeFramesOverride\":0}},\"exitPolicy\":{\"acceptedImpact\":\"Complete\",\"blockedImpact\":\"Dissipate\",\"expiration\":\"Complete\",\"externalRemoval\":\"Dissipate\",\"runtimeReset\":\"Immediate\"}}]}";
            CombatProjectileUnityPresentationRegistry jsonRegistry = CombatProjectileUnityPresentationConfigJsonImporter.Import(json);
            RuntimeCatalogBinaryLoadResult<CombatProjectileUnityPresentationRegistry> binary =
                CombatProjectileUnityPresentationConfigBinaryImporter.ImportBytes(BuildV2Binary());
            Assert.That(binary.Success, Is.True, string.Join("\n", binary.Diagnostics));
            CombatProjectileSnapshot projectile = Snapshot(6, frame: 11, age: 0);
            var events = new CombatProjectileFrameEventBuffer();
            SetEvents(events, new[] { new CombatProjectileSpawnEvent(projectile) }, null, null, null, null, null);
            VfxRuntimeModule jsonVfx = CreateVfxRuntime();
            VfxRuntimeModule binaryVfx = CreateVfxRuntime();

            new CombatProjectileVfxBridge(jsonRegistry, jsonVfx).ProcessFrame(new RuntimeFrame(11), events, null);
            new CombatProjectileVfxBridge(binary.Catalog, binaryVfx).ProcessFrame(new RuntimeFrame(11), events, null);
            var jsonRequests = new System.Collections.Generic.List<VfxSpawnRequest>();
            var binaryRequests = new System.Collections.Generic.List<VfxSpawnRequest>();
            jsonVfx.CopyPendingSpawnRequests(jsonRequests);
            binaryVfx.CopyPendingSpawnRequests(binaryRequests);

            Assert.That(jsonRequests, Has.Count.EqualTo(1));
            Assert.That(binaryRequests, Has.Count.EqualTo(1));
            VfxSpawnRequest jsonRequest = jsonRequests[0];
            VfxSpawnRequest binaryRequest = binaryRequests[0];
            Assert.That(jsonRequest.SourceDomain, Is.EqualTo(VfxSourceDomains.ProjectilePresentation));
            Assert.That(binaryRequest.SourceDomain, Is.EqualTo(jsonRequest.SourceDomain));
            Assert.That(binaryRequest.TriggerId, Is.EqualTo(jsonRequest.TriggerId));
            Assert.That(binaryRequest.DefinitionId, Is.EqualTo(jsonRequest.DefinitionId));
            Assert.That(binaryRequest.Anchor, Is.EqualTo(jsonRequest.Anchor));
            Assert.That(binaryRequest.LifetimeFramesOverride, Is.EqualTo(jsonRequest.LifetimeFramesOverride));
            Assert.That(binaryRequest, Is.EqualTo(jsonRequest),
                "JSON and binary importer results must drive an identical complete VfxSpawnRequest");
        }

        [Test]
        public void InvalidArtifactsFailWithoutPartialRegistry()
        {
            byte[] valid = BuildV2Binary();
            byte[] truncated = new byte[valid.Length - 2];
            Array.Copy(valid, truncated, truncated.Length);
            RuntimeCatalogBinaryLoadResult<CombatProjectileUnityPresentationRegistry> result = CombatProjectileUnityPresentationConfigBinaryImporter.ImportBytes(truncated);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Catalog, Is.Null);
            Assert.Throws<InvalidDataException>(() => CombatProjectileUnityPresentationConfigJsonImporter.Import("{\"schemaVersion\":\"2\",\"bindings\":[{\"projectileDefinitionId\":1,\"slots\":{\"flightBody\":{\"resource\":{\"id\":\"x\",\"typeId\":\"GameObject\"},\"orientation\":\"SurfaceNormal\"}}}]}"));
        }

        [Test]
        public void BinaryContractFailuresAreAtomicAcrossSchemaSlotDuplicateAndOrientation()
        {
            AssertAtomicFailure(BuildSingleSlotBinary("9", 1, 0, false), "unknown schema");
            AssertAtomicFailure(BuildSingleSlotBinary("2", 99, 0, false), "unknown slot");
            AssertAtomicFailure(BuildSingleSlotBinary("2", 1, 2, false), "invalid orientation");
            AssertAtomicFailure(BuildSingleSlotBinary("2", 1, 0, true), "duplicate definition");
        }

        [Test]
        public void SlotLifetimeResourceAndExitPolicyMatrixRejectsInvalidJsonAndBinary()
        {
            var gameObject = new MxFramework.Resources.ResourceKey("vfx.test", "GameObject", packageId: "test");
            var texture = new MxFramework.Resources.ResourceKey("vfx.test", "Texture", packageId: "test");
            Assert.DoesNotThrow(() => new CombatProjectileUnityPresentationSlot(
                CombatProjectileUnityPresentationSlotKind.Spawn, gameObject,
                CombatProjectileUnityPresentationOrientation.WorldUp, MxFramework.Core.Math.FixVector3.Zero, 0, 1));
            Assert.Throws<ArgumentException>(() => new CombatProjectileUnityPresentationSlot(
                CombatProjectileUnityPresentationSlotKind.FlightBody, gameObject,
                CombatProjectileUnityPresentationOrientation.ProjectileForward, MxFramework.Core.Math.FixVector3.Zero, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatProjectileUnityPresentationSlot(
                CombatProjectileUnityPresentationSlotKind.Spawn, gameObject,
                CombatProjectileUnityPresentationOrientation.WorldUp, MxFramework.Core.Math.FixVector3.Zero, -1, 1));
            Assert.Throws<ArgumentException>(() => new CombatProjectileUnityPresentationSlot(
                CombatProjectileUnityPresentationSlotKind.Spawn, texture,
                CombatProjectileUnityPresentationOrientation.WorldUp, MxFramework.Core.Math.FixVector3.Zero, 0, 1));
            Assert.Throws<ArgumentException>(() => new CombatProjectileUnityPresentationExitPolicy(
                CombatProjectileUnityPresentationExitMode.Immediate, CombatProjectileUnityPresentationExitMode.Dissipate,
                CombatProjectileUnityPresentationExitMode.Complete, CombatProjectileUnityPresentationExitMode.Dissipate,
                CombatProjectileUnityPresentationExitMode.Immediate));
            Assert.Throws<ArgumentException>(() => new CombatProjectileUnityPresentationExitPolicy(
                CombatProjectileUnityPresentationExitMode.Complete, CombatProjectileUnityPresentationExitMode.Dissipate,
                CombatProjectileUnityPresentationExitMode.Complete, CombatProjectileUnityPresentationExitMode.Dissipate,
                CombatProjectileUnityPresentationExitMode.Complete));

            Assert.Throws<InvalidDataException>(() => CombatProjectileUnityPresentationConfigJsonImporter.Import(
                "{\"schemaVersion\":\"2\",\"bindings\":[{\"projectileDefinitionId\":1,\"slots\":{\"flightBody\":{\"resource\":{\"id\":\"x\",\"typeId\":\"GameObject\"},\"orientation\":\"ProjectileForward\",\"lifetimeFramesOverride\":1}},\"exitPolicy\":{\"acceptedImpact\":\"Complete\",\"blockedImpact\":\"Dissipate\",\"expiration\":\"Complete\",\"externalRemoval\":\"Dissipate\",\"runtimeReset\":\"Immediate\"}}]}"));
            Assert.Throws<InvalidDataException>(() => CombatProjectileUnityPresentationConfigJsonImporter.Import(
                "{\"schemaVersion\":\"2\",\"bindings\":[{\"projectileDefinitionId\":1,\"slots\":{},\"exitPolicy\":{\"acceptedImpact\":\"Immediate\",\"blockedImpact\":\"Dissipate\",\"expiration\":\"Complete\",\"externalRemoval\":\"Dissipate\",\"runtimeReset\":\"Immediate\"}}]}"));
            AssertAtomicFailure(BuildSingleSlotBinary("2", 1, 0, false, lifetime: 1), "persistent lifetime");
            AssertAtomicFailure(BuildSingleSlotBinary("2", 1, 0, false, acceptedExit: 2), "ordinary Immediate exit");
            AssertAtomicFailure(BuildSingleSlotBinary("2", 1, 0, false, runtimeReset: 0), "non-Immediate runtime reset");
        }

        [Test]
        public void WarmedRegistryLookupUsesOneAuthorityAndStableLegacyAdapter()
        {
            RuntimeCatalogBinaryLoadResult<CombatProjectileUnityPresentationRegistry> imported = CombatProjectileUnityPresentationConfigBinaryImporter.ImportBytes(BuildV2Binary());
            Assert.That(imported.Success, Is.True);
            CombatProjectileUnityPresentationRegistry registry = imported.Catalog;
            int count = registry.Count;
            Assert.That(registry.TryGetV2(20, out CombatProjectileUnityPresentationBindingV2 first), Is.True);
            registry.TryGetV2(20, out _);
            first.TryGetSlot(CombatProjectileUnityPresentationSlotKind.FlightBody, out _);
            registry.TryGetV2(999, out _);
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool known = true;
            bool slotKnown = true;
            bool missing = false;
            CombatProjectileUnityPresentationBindingV2 next = null;
            CombatProjectileUnityPresentationSlot missingSlot = default;
            for (int i = 0; i < 4096; i++)
            {
                known &= registry.TryGetV2(20, out next);
                slotKnown &= next.TryGetSlot(CombatProjectileUnityPresentationSlotKind.FlightBody, out _);
                missing |= registry.TryGetV2(999, out _);
                next.TryGetSlot((CombatProjectileUnityPresentationSlotKind)255, out missingSlot);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(known && slotKnown, Is.True);
            Assert.AreSame(first, next);
            Assert.That(missing, Is.False);
            Assert.That(missingSlot, Is.EqualTo(default(CombatProjectileUnityPresentationSlot)));
            Assert.That(allocated, Is.EqualTo(0), "4096 warmed TryGetV2/TryGetSlot/missing lookups must allocate zero managed bytes.");
#pragma warning disable CS0618
            Assert.That(registry.TryGet(20, out CombatProjectileUnityPresentationBinding legacy), Is.True);
            Assert.That(registry.TryGet(20, out CombatProjectileUnityPresentationBinding repeated), Is.True);
#pragma warning restore CS0618
            Assert.AreSame(legacy, repeated, "The obsolete API must return its precomputed adapter, not construct a second registry entry per lookup.");
            Assert.AreEqual(count, registry.Count, "Warmed lookups must not mutate registry collections.");
        }

        [Test]
        public void BridgeRoutesSpawnAreaExpireInFixedOrderAndDeduplicatesRepeatedBuffer()
        {
            CombatProjectileSnapshot projectile = Snapshot(7, frame: 12, age: 0);
            var events = new CombatProjectileFrameEventBuffer();
            SetEvents(events,
                new[] { new CombatProjectileSpawnEvent(projectile) },
                new[] { new CombatProjectileAreaPulseTriggerEvent(projectile, CombatProjectileAreaPulseTriggerKind.Expiration,
                    projectile.Position, true, false, default, default, default) },
                null,
                new[] { new CombatProjectileExpiredEvent(projectile) },
                null,
                null);
            VfxRuntimeModule vfx = CreateVfxRuntime();
            var bridge = new CombatProjectileVfxBridge(CreateBridgeRegistry(), vfx);

            bridge.ProcessFrame(new RuntimeFrame(12), events, null);
            bridge.ProcessFrame(new RuntimeFrame(12), events, null);

            Assert.That(vfx.PendingSpawnCount, Is.EqualTo(3), "spawn, area and expire must each route exactly once");
            var pending = new System.Collections.Generic.List<VfxSpawnRequest>();
            vfx.CopyPendingSpawnRequests(pending);
            Assert.That(pending, Has.All.Matches<VfxSpawnRequest>(request =>
                request.SourceDomain == VfxSourceDomains.ProjectilePresentation));
            vfx.Tick(new RuntimeTickContext(12, 0d, 0d, RuntimeTickStage.PostSimulation));
            var emitted = new System.Collections.Generic.List<VfxRuntimeEvent>();
            vfx.DrainEvents(new RuntimeFrame(12), emitted);
            Assert.That(emitted.Count, Is.EqualTo(3));
            Assert.That(emitted[0].DefinitionId.Value, Is.EqualTo(101));
            Assert.That(emitted[1].DefinitionId.Value, Is.EqualTo(102));
            Assert.That(emitted[2].DefinitionId.Value, Is.EqualTo(103));
        }

        [Test]
        public void BridgeLateAttachDoesNotReplayOneShotsAndWarmedEmptyFramesAllocateZero()
        {
            var events = new CombatProjectileFrameEventBuffer();
            VfxRuntimeModule vfx = CreateVfxRuntime();
            var bridge = new CombatProjectileVfxBridge(CreateBridgeRegistry(), vfx);
            bridge.ProcessFrame(RuntimeFrame.Zero, events, null);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4096; i++)
                bridge.ProcessFrame(new RuntimeFrame(i), events, null);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(vfx.PendingSpawnCount, Is.Zero);
            Assert.That(allocated, Is.Zero, "warmed bridge routing must allocate zero managed bytes");
        }

        [Test]
        public void WarmedNonEmptyDirectAreaExpireRoutingAllocatesZeroAcross4096Frames()
        {
            const int frameCount = 4096;
            var buffers = new CombatProjectileFrameEventBuffer[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                CombatProjectileSnapshot projectile = Snapshot(1000 + i, frame: 1000 + i, age: 1);
                buffers[i] = new CombatProjectileFrameEventBuffer();
                SetEvents(buffers[i], null,
                    new[] { new CombatProjectileAreaPulseTriggerEvent(projectile, CombatProjectileAreaPulseTriggerKind.Expiration,
                        projectile.Position, false, false, default, default, default) },
                    new[] { Impact(projectile, HitResolveKind.Damage, consumed: false, targetId: 1) },
                    new[] { new CombatProjectileExpiredEvent(projectile) }, null, null);
            }

            VfxRuntimeModule vfx = CreateVfxRuntime();
            var bridge = new CombatProjectileVfxBridge(CreateBridgeRegistry(), vfx);
            for (int i = 0; i < frameCount; i++)
                bridge.ProcessFrame(new RuntimeFrame(1000 + i), buffers[i], null);
            vfx.Tick(new RuntimeTickContext(1000 + frameCount, 0d, 0d, RuntimeTickStage.PostSimulation));
            bridge.Reset(new RuntimeFrame(1000 + frameCount));

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < frameCount; i++)
                bridge.ProcessFrame(new RuntimeFrame(1000 + i), buffers[i], null);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(vfx.PendingSpawnCount, Is.EqualTo(frameCount * 3));
            Assert.That(allocated, Is.Zero,
                "warmed accepted direct, area and expire routing must allocate zero managed bytes across 4096 non-empty frames");
        }

        [Test]
        public void CustomExtensionFirstClaimSuppressesStandardAndDuplicateClaimIsDiagnosed()
        {
            CombatProjectileSnapshot projectile = Snapshot(8, frame: 20, age: 0);
            var events = new CombatProjectileFrameEventBuffer();
            SetEvents(events, new[] { new CombatProjectileSpawnEvent(projectile) }, null, null, null, null, null);
            VfxRuntimeModule vfx = CreateVfxRuntime();
            var first = new ClaimingExtension();
            var second = new ClaimingExtension();
            var bridge = new CombatProjectileVfxBridge(CreateBridgeRegistry(), vfx, null,
                new ICombatProjectilePresentationExtension[] { first, second });

            bridge.ProcessFrame(new RuntimeFrame(20), events, null);

            Assert.That(vfx.PendingSpawnCount, Is.Zero);
            Assert.That(first.HandleCount, Is.EqualTo(1));
            Assert.That(second.HandleCount, Is.Zero, "duplicate claimant must not receive a side-effecting callback");
            Assert.That(bridge.GetDiagnostics().DuplicateExtensionClaims, Is.EqualTo(1));
        }

        [Test]
        public void BridgeRoutesEachAcceptedPierceAndBlockedContactButSkipsRejectedKinds()
        {
            CombatProjectileSnapshot projectile = Snapshot(9, frame: 30, age: 4);
            var events = new CombatProjectileFrameEventBuffer();
            var impacts = new[]
            {
                Impact(projectile, HitResolveKind.Damage, consumed: false, targetId: 20),
                Impact(projectile, HitResolveKind.Damage, consumed: true, targetId: 21),
                Impact(projectile, HitResolveKind.Blocked, consumed: false, targetId: 22),
                Impact(projectile, HitResolveKind.Blocked, consumed: true, targetId: 25),
                Impact(projectile, HitResolveKind.Friendly, consumed: false, targetId: 23),
                Impact(projectile, HitResolveKind.Duplicate, consumed: false, targetId: 24),
                Impact(projectile, HitResolveKind.SelfDamage, consumed: false, targetId: 26),
                Impact(projectile, HitResolveKind.TargetDead, consumed: false, targetId: 27)
            };
            SetEvents(events, null, null, impacts, null, null, null);
            VfxRuntimeModule vfx = CreateVfxRuntime();
            var bridge = new CombatProjectileVfxBridge(CreateBridgeRegistry(), vfx);

            bridge.ProcessFrame(new RuntimeFrame(30), events, null);
            bridge.ProcessFrame(new RuntimeFrame(30), events, null);

            Assert.That(vfx.PendingSpawnCount, Is.EqualTo(4),
                "two accepted pierce contacts and two blocked contacts route; friendly/self/dead/duplicate do not");
        }

        [Test]
        public void AreaPulseRoutesOnceForZeroOneOrManyTargetsAndNeverRoutesTargetImpactsAsSourceCues()
        {
            VfxRuntimeModule vfx = CreateVfxRuntime();
            var bridge = new CombatProjectileVfxBridge(CreateBridgeRegistry(), vfx);
            var events = new CombatProjectileFrameEventBuffer();
            int[] targetCounts = { 0, 1, 3 };
            for (int caseIndex = 0; caseIndex < targetCounts.Length; caseIndex++)
            {
                CombatProjectileSnapshot projectile = Snapshot(40 + caseIndex, frame: 50 + caseIndex, age: 1);
                var impacts = new CombatProjectileImpactEvent[targetCounts[caseIndex]];
                for (int i = 0; i < impacts.Length; i++)
                    impacts[i] = Impact(projectile, HitResolveKind.Damage, consumed: false, targetId: 100 + i,
                        CombatProjectileImpactSourceKind.AreaPulse);
                SetEvents(events, null,
                    new[] { new CombatProjectileAreaPulseTriggerEvent(projectile, CombatProjectileAreaPulseTriggerKind.DirectSweep,
                        projectile.Position, false, false, default, default, default) },
                    impacts, null, null, null);
                bridge.ProcessFrame(new RuntimeFrame(50 + caseIndex), events, null);
            }

            Assert.That(vfx.PendingSpawnCount, Is.EqualTo(3), "each pulse emits exactly one area cue independent of target count");
            vfx.Tick(new RuntimeTickContext(52, 0d, 0d, RuntimeTickStage.PostSimulation));
            var emitted = new System.Collections.Generic.List<VfxRuntimeEvent>();
            vfx.DrainEvents(new RuntimeFrame(52), emitted);
            Assert.That(emitted, Has.Count.EqualTo(3));
            Assert.That(emitted, Has.All.Matches<VfxRuntimeEvent>(item => item.DefinitionId.Value == 102),
                "area target impacts must not produce direct/blocked source cues");
        }

        [Test]
        public void MissingBindingSlotInvalidIdAndBackendUnavailableFailSoftWithDiagnostics()
        {
            CombatProjectileSnapshot projectile = Snapshot(60, frame: 70, age: 0);
            var events = new CombatProjectileFrameEventBuffer();
            SetEvents(events, new[] { new CombatProjectileSpawnEvent(projectile) }, null, null, null, null, null);

            var missingBinding = new CombatProjectileVfxBridge(new CombatProjectileUnityPresentationRegistry(), CreateVfxRuntime());
            Assert.DoesNotThrow(() => missingBinding.ProcessFrame(new RuntimeFrame(70), events, null));
            Assert.That(missingBinding.GetDiagnostics().MissingBindings, Is.EqualTo(1));

            var noSlots = new CombatProjectileUnityPresentationRegistry(new[]
            {
                new CombatProjectileUnityPresentationBindingV2(20, "Empty", CombatProjectileUnityPresentationExitPolicy.Default,
                    Array.Empty<CombatProjectileUnityPresentationSlot>())
            });
            var missingSlot = new CombatProjectileVfxBridge(noSlots, CreateVfxRuntime());
            Assert.DoesNotThrow(() => missingSlot.ProcessFrame(new RuntimeFrame(70), events, null));
            Assert.That(missingSlot.GetDiagnostics().MissingSlots, Is.EqualTo(1));

            var invalidSlots = new[]
            {
                SlotWithInvalidCompactId()
            };
            var invalidRegistry = new CombatProjectileUnityPresentationRegistry(new[]
            {
                new CombatProjectileUnityPresentationBindingV2(20, "Invalid", CombatProjectileUnityPresentationExitPolicy.Default, invalidSlots)
            });
            var invalidId = new CombatProjectileVfxBridge(invalidRegistry, CreateVfxRuntime());
            Assert.DoesNotThrow(() => invalidId.ProcessFrame(new RuntimeFrame(70), events, null));
            Assert.That(invalidId.GetDiagnostics().InvalidDefinitionIds, Is.EqualTo(1));

            var backendUnavailable = new CombatProjectileVfxBridge(CreateBridgeRegistry(), null);
            Assert.DoesNotThrow(() => backendUnavailable.ProcessFrame(new RuntimeFrame(70), events, null));
            Assert.That(backendUnavailable.GetDiagnostics().BackendUnavailable, Is.EqualTo(1));
        }

        [Test]
        public void LateAttachExternalRemovalAndRuntimeResetExposeCustomLifecycleContextsWithoutOneShots()
        {
            var extension = new RecordingExtension();
            VfxRuntimeModule vfx = CreateVfxRuntime();
            var bridge = new CombatProjectileVfxBridge(CreateBridgeRegistry(), vfx, null,
                new ICombatProjectilePresentationExtension[] { extension });
            var events = new CombatProjectileFrameEventBuffer();
            CombatProjectileActiveView active = ActiveViewFrom(Snapshot(80, frame: 90, age: 5));

            bridge.ProcessFrame(new RuntimeFrame(90), events, new[] { active });
            Assert.That(extension.Count(CombatProjectilePresentationEventKind.LateAttachRebuild), Is.EqualTo(1));
            Assert.That(vfx.PendingSpawnCount, Is.Zero, "late attach rebuild must not replay a one-shot");

            bridge.ProcessFrame(new RuntimeFrame(91), events, Array.Empty<CombatProjectileActiveView>());
            Assert.That(extension.Count(CombatProjectilePresentationEventKind.ExternalRemoval), Is.EqualTo(1));
            Assert.That(extension.Count(CombatProjectilePresentationEventKind.Expire), Is.Zero,
                "external removal must not masquerade as natural expiration");

            bridge.ProcessFrame(new RuntimeFrame(92), events, new[] { active });
            bridge.Reset(new RuntimeFrame(93));
            Assert.That(extension.Count(CombatProjectilePresentationEventKind.RuntimeReset), Is.EqualTo(1));
            Assert.That(vfx.PendingSpawnCount, Is.Zero, "reset must not synthesize one-shots");
        }

        [Test]
        public void ResetClearsBoundedCorrelationStateSoAReusedAuthoritativeBufferCanRouteAgain()
        {
            CombatProjectileSnapshot projectile = Snapshot(81, frame: 94, age: 0);
            var events = new CombatProjectileFrameEventBuffer();
            SetEvents(events, new[] { new CombatProjectileSpawnEvent(projectile) }, null, null, null,
                new[] { default(CombatProjectileRejectedEvent) }, null);
            VfxRuntimeModule vfx = CreateVfxRuntime();
            var bridge = new CombatProjectileVfxBridge(CreateBridgeRegistry(), vfx);

            bridge.ProcessFrame(new RuntimeFrame(94), events, null);
            bridge.Reset(new RuntimeFrame(95));
            bridge.ProcessFrame(new RuntimeFrame(94), events, null);

            Assert.That(vfx.PendingSpawnCount, Is.EqualTo(2));
        }

        [Test]
        public void ClaimedSpawnAndConsumedImpactDoNotFallThroughToLateAttachOrExternalRemoval()
        {
            var extension = new RecordingExtension();
            var bridge = new CombatProjectileVfxBridge(CreateBridgeRegistry(), CreateVfxRuntime(), null,
                new ICombatProjectilePresentationExtension[] { extension });
            var events = new CombatProjectileFrameEventBuffer();
            CombatProjectileSnapshot projectile = Snapshot(82, frame: 100, age: 0);
            SetEvents(events, new[] { new CombatProjectileSpawnEvent(projectile) }, null, null, null, null, null);
            bridge.ProcessFrame(new RuntimeFrame(100), events, new[] { ActiveViewFrom(projectile) });

            SetEvents(events, null, null,
                new[] { Impact(projectile, HitResolveKind.Damage, consumed: true, targetId: 9) }, null, null, null);
            bridge.ProcessFrame(new RuntimeFrame(101), events, Array.Empty<CombatProjectileActiveView>());

            Assert.That(extension.Count(CombatProjectilePresentationEventKind.Spawn), Is.EqualTo(1));
            Assert.That(extension.Count(CombatProjectilePresentationEventKind.DirectImpact), Is.EqualTo(1));
            Assert.That(extension.Count(CombatProjectilePresentationEventKind.LateAttachRebuild), Is.Zero);
            Assert.That(extension.Count(CombatProjectilePresentationEventKind.ExternalRemoval), Is.Zero,
                "claimed consumed impact must terminate tracking without a second lifecycle callback");
        }

        private static CombatProjectileSnapshot Snapshot(int id, int frame, int age) => new CombatProjectileSnapshot(
            id, 0, 20, "projectile.test", new CombatEntityId(3), 4, 5, 6, 7,
            new CombatFrame(frame - age), new CombatFrame(frame), age,
            new FixVector3(Fix64.One, Fix64.Zero, Fix64.FromInt(2)),
            new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One));

        private static CombatProjectileActiveView ActiveViewFrom(CombatProjectileSnapshot projectile) => new CombatProjectileActiveView(
            projectile.ProjectileId, projectile.SpawnSequence, projectile.DefinitionId, projectile.DefinitionStableId,
            projectile.SourceEntityId, projectile.ActionId, projectile.ActionInstanceId, projectile.TraceId, projectile.LocalFrame,
            projectile.SpawnFrame, projectile.LastFrame, projectile.AgeFrames, projectile.Position, projectile.Direction,
            projectile.Origin, projectile.HasTargetPoint, projectile.TargetPoint, projectile.EffectiveLifetimeFrames,
            projectile.HasTrackingTarget, projectile.TrackingTargetEntityId, projectile.AcceptedImpacts.Count,
            projectile.LaunchSpeedPerFrame);

        private static CombatProjectileUnityPresentationRegistry CreateBridgeRegistry()
        {
            var slots = new[]
            {
                Slot(CombatProjectileUnityPresentationSlotKind.Spawn, 101, CombatProjectileUnityPresentationOrientation.WorldUp),
                Slot(CombatProjectileUnityPresentationSlotKind.AreaImpact, 102, CombatProjectileUnityPresentationOrientation.WorldUp),
                Slot(CombatProjectileUnityPresentationSlotKind.Expire, 103, CombatProjectileUnityPresentationOrientation.WorldUp),
                Slot(CombatProjectileUnityPresentationSlotKind.DirectImpact, 104, CombatProjectileUnityPresentationOrientation.SurfaceNormal),
                Slot(CombatProjectileUnityPresentationSlotKind.BlockedImpact, 105, CombatProjectileUnityPresentationOrientation.SurfaceNormal)
            };
            return new CombatProjectileUnityPresentationRegistry(new[]
            {
                new CombatProjectileUnityPresentationBindingV2(20, "Bridge", CombatProjectileUnityPresentationExitPolicy.Default, slots)
            });
        }

        private static CombatProjectileUnityPresentationSlot Slot(CombatProjectileUnityPresentationSlotKind kind, int id,
            CombatProjectileUnityPresentationOrientation orientation) => new CombatProjectileUnityPresentationSlot(
                kind, new ResourceKey("vfx.bridge." + id, ResourceTypeIds.GameObject), orientation, FixVector3.Zero, 0, id);

        private static CombatProjectileUnityPresentationSlot SlotWithInvalidCompactId()
        {
            object boxed = Slot(CombatProjectileUnityPresentationSlotKind.Spawn, 101,
                CombatProjectileUnityPresentationOrientation.WorldUp);
            typeof(CombatProjectileUnityPresentationSlot)
                .GetField("<VfxDefinitionId>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(boxed, 0);
            return (CombatProjectileUnityPresentationSlot)boxed;
        }

        private static VfxRuntimeModule CreateVfxRuntime()
        {
            var registry = new VfxDefinitionRegistry();
            for (int id = 101; id <= 105; id++)
            {
                Assert.That(registry.TryRegister(new VfxDefinition(new VfxDefinitionId(id), "vfx-" + id,
                    new ResourceKey("vfx.bridge." + id, ResourceTypeIds.GameObject), 30, "hash-" + id), out string failure), Is.True, failure);
            }
            return new VfxRuntimeModule(registry, new RuntimeCommandBuffer());
        }

        private static CombatProjectileImpactEvent Impact(CombatProjectileSnapshot projectile, HitResolveKind kind, bool consumed, int targetId,
            CombatProjectileImpactSourceKind sourceKind = CombatProjectileImpactSourceKind.DirectSweep)
        {
            var result = new HitResolveResult(projectile.SourceEntityId, new CombatEntityId(targetId), projectile.ActionId,
                projectile.ActionInstanceId, projectile.TraceId, projectile.LastFrame, kind, kind == HitResolveKind.Damage ? 0 : 0,
                0, FixVector3.Zero, projectile.Position, new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
            return new CombatProjectileImpactEvent(projectile, default(CombatQueryResult), result, default,
                new CombatProjectileImpactContext(sourceKind), consumed);
        }

        private sealed class ClaimingExtension : ICombatProjectilePresentationExtension
        {
            public int HandleCount { get; private set; }
            public bool CanHandle(in CombatProjectilePresentationEventContext context) => true;
            public void Handle(in CombatProjectilePresentationEventContext context)
            {
                HandleCount++;
            }
        }

        private sealed class RecordingExtension : ICombatProjectilePresentationExtension
        {
            private readonly int[] _counts = new int[9];
            public bool CanHandle(in CombatProjectilePresentationEventContext context) => true;
            public void Handle(in CombatProjectilePresentationEventContext context) => _counts[(int)context.Kind]++;
            public int Count(CombatProjectilePresentationEventKind kind) => _counts[(int)kind];
        }

        private static void SetEvents(CombatProjectileFrameEventBuffer buffer,
            System.Collections.Generic.IReadOnlyList<CombatProjectileSpawnEvent> spawns,
            System.Collections.Generic.IReadOnlyList<CombatProjectileAreaPulseTriggerEvent> areas,
            System.Collections.Generic.IReadOnlyList<CombatProjectileImpactEvent> impacts,
            System.Collections.Generic.IReadOnlyList<CombatProjectileExpiredEvent> expirations,
            System.Collections.Generic.IReadOnlyList<CombatProjectileRejectedEvent> rejections,
            System.Collections.Generic.IReadOnlyList<CombatProjectilePhysicalEvent> physical)
        {
            typeof(CombatProjectileFrameEventBuffer).GetMethod("CopyFrom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[]
                {
                    typeof(System.Collections.Generic.IReadOnlyList<CombatProjectileSpawnEvent>),
                    typeof(System.Collections.Generic.IReadOnlyList<CombatProjectileAreaPulseTriggerEvent>),
                    typeof(System.Collections.Generic.IReadOnlyList<CombatProjectileImpactEvent>),
                    typeof(System.Collections.Generic.IReadOnlyList<CombatProjectileExpiredEvent>),
                    typeof(System.Collections.Generic.IReadOnlyList<CombatProjectileRejectedEvent>),
                    typeof(System.Collections.Generic.IReadOnlyList<CombatProjectilePhysicalEvent>)
                }, null).Invoke(buffer, new object[] { spawns, areas, impacts, expirations, rejections, physical });
        }

        private static byte[] BuildV2Binary()
        {
            byte[] payload;
            using (var payloadStream = new MemoryStream())
            using (var writer = new BinaryWriter(payloadStream))
            {
                writer.Write(20); writer.Write("V2"); writer.Write(2);
                WriteSlot(writer, 0, "vfx.test.spawn", 1, 1000000, 2000000, 3000000, 12, 101);
                WriteSlot(writer, 1, "vfx.test.flight", 0, 0, 0, 0, 0, 0);
                writer.Write((byte)0); writer.Write((byte)1); writer.Write((byte)0); writer.Write((byte)1); writer.Write((byte)2);
                writer.Flush(); payload = payloadStream.ToArray();
            }
            using var stream = new MemoryStream();
            using var output = new BinaryWriter(stream);
            RuntimeCatalogBinary.WriteHeader(output, Kind, "2", "test", 1, RuntimeCatalogBinary.ComputeStableHash(payload, 0, payload.Length));
            output.Write(payload); output.Flush(); return stream.ToArray();
        }

        private static byte[] BuildSingleSlotBinary(
            string schema,
            byte slotKind,
            byte orientation,
            bool duplicate,
            int lifetime = 0,
            byte acceptedExit = 0,
            byte runtimeReset = 2)
        {
            byte[] payload;
            using (var payloadStream = new MemoryStream())
            using (var writer = new BinaryWriter(payloadStream))
            {
                int count = duplicate ? 2 : 1;
                for (int i = 0; i < count; i++)
                {
                    writer.Write(20); writer.Write("V2"); writer.Write(1);
                    WriteSlot(writer, slotKind, "vfx.test.flight", orientation, 0, 0, 0, lifetime, 0);
                    writer.Write(acceptedExit); writer.Write((byte)1); writer.Write((byte)0); writer.Write((byte)1); writer.Write(runtimeReset);
                }
                writer.Flush(); payload = payloadStream.ToArray();
            }
            using var stream = new MemoryStream();
            using var output = new BinaryWriter(stream);
            RuntimeCatalogBinary.WriteHeader(output, Kind, schema, "test", duplicate ? 2 : 1, RuntimeCatalogBinary.ComputeStableHash(payload, 0, payload.Length));
            output.Write(payload); output.Flush(); return stream.ToArray();
        }

        private static void AssertAtomicFailure(byte[] bytes, string reason)
        {
            RuntimeCatalogBinaryLoadResult<CombatProjectileUnityPresentationRegistry> result = CombatProjectileUnityPresentationConfigBinaryImporter.ImportBytes(bytes);
            Assert.That(result.Success, Is.False, reason);
            Assert.That(result.Catalog, Is.Null, reason + " must not expose a partially registered catalog");
        }

        private static void WriteSlot(BinaryWriter writer, byte kind, string id, byte orientation, long x, long y, long z, int lifetime, int vfxId)
        {
            writer.Write(kind); writer.Write(id); writer.Write("GameObject"); writer.Write(""); writer.Write("test"); writer.Write(orientation); writer.Write(x); writer.Write(y); writer.Write(z); writer.Write(lifetime); writer.Write(vfxId);
        }
    }
}

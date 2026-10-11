using System;
using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestSeveredTargetingInvalidationTests
    {
        private const int TailPartStateId = CharacterTestAuthoredContentIds.D400_Id43;
        private const int TailZoneId = CharacterTestAuthoredContentIds.D400_Id154;
        private const int CoreZoneId = CharacterTestAuthoredContentIds.D400_Id151;
        private const int HeadZoneId = CharacterTestAuthoredContentIds.D400_Id152;
        private const int TailAnchorId = CharacterTestAuthoredContentIds.D400_Id161;
        private const int TailBaseHitZoneId = CharacterTestAuthoredContentIds.D400_Id98;
        private const int TailBaseColliderId = CharacterTestAuthoredContentIds.D400_Id147;
        private const int CoreChestHitZoneId = CharacterTestAuthoredContentIds.D400_Id84;

        // Positive IDs reserved for the synthetic selector fixtures below.
        private const int FixtureHeadZoneId = 1001;
        private const int FixtureTailZoneId = 1002;
        private const int FixtureHeadHitZoneId = 2001;
        private const int FixtureTailHitZoneId = 2002;
        private const int FixtureHeadHurtboxId = 3001;
        private const int FixtureTailHurtboxId = 3002;

        [Test]
        public void Dragon14_SeveredTail_RetainsAuthoredZoneIdentityAndRebuildsFromStateAndBinding()
        {
            DragonZoneFixture fixture = CreateDragonZoneFixture();
            fixture.Sync(new RuntimeFrame(10));
            Assert.IsTrue(fixture.Hurtboxes.TryGetDamageGeometrySnapshot(
                fixture.Target.Id.Value,
                out CharacterTestDamageGeometrySnapshot intactGeometry));
            Assert.IsTrue(fixture.Hurtboxes.TryGetDamageZoneSnapshot(
                fixture.Target.Id.Value,
                out CharacterTestDamageZoneSnapshot intactZones));

            Assert.AreEqual(18, intactGeometry.ColliderCount);
            Assert.AreEqual(5, intactZones.ZoneCount);
            Assert.AreEqual(5, intactZones.AvailableZoneCount);
            Assert.IsTrue(intactZones.TryGetAvailableZone(
                TailZoneId,
                out CharacterTestDamageZoneAggregate intactTail));
            Assert.IsTrue(intactZones.TryGetAvailableZone(
                CoreZoneId,
                out CharacterTestDamageZoneAggregate intactCore));
            Assert.IsTrue(intactZones.ContainsMemberWitness(
                CoreZoneId,
                TailBaseHitZoneId,
                TailBaseColliderId));
            int intactZoneRevision = intactZones.ZoneRevision;
            int intactCoreMemberCount = intactCore.MemberCount;
            int intactHeadMemberCount = GetZone(intactZones, HeadZoneId).MemberCount;
            int intactTopologyRevision = intactGeometry.TopologyRevision;

            SeverPart(fixture.Target, TailPartStateId, breakFrame: 20, severFrame: 21);
            fixture.Sync(new RuntimeFrame(11));
            Assert.IsTrue(fixture.Hurtboxes.TryGetDamageGeometrySnapshot(
                fixture.Target.Id.Value,
                out CharacterTestDamageGeometrySnapshot severedGeometry));
            Assert.IsTrue(fixture.Hurtboxes.TryGetDamageZoneSnapshot(
                fixture.Target.Id.Value,
                out CharacterTestDamageZoneSnapshot severedZones));

            Assert.AreEqual(15, severedGeometry.ColliderCount);
            Assert.Greater(severedGeometry.TopologyRevision, intactTopologyRevision);
            Assert.AreEqual(severedGeometry.TopologyRevision, severedZones.TopologyRevision);
            Assert.AreEqual(5, severedZones.ZoneCount, "Authored Zone identities are stable.");
            Assert.AreEqual(4, severedZones.AvailableZoneCount);
            Assert.IsTrue(severedZones.TryGetZone(
                TailZoneId,
                out CharacterTestDamageZoneAggregate severedTail));
            Assert.IsFalse(severedTail.IsAvailable);
            Assert.AreEqual(0, severedTail.MemberCount);
            Assert.IsFalse(severedZones.TryGetAvailableZone(TailZoneId, out _));
            Assert.IsTrue(severedZones.TryGetAvailableZone(
                CoreZoneId,
                out CharacterTestDamageZoneAggregate severedCore));
            Assert.AreEqual(intactCoreMemberCount - 1, severedCore.MemberCount);
            Assert.IsTrue(severedZones.ContainsHitZone(CoreZoneId, CoreChestHitZoneId));
            Assert.IsFalse(severedZones.ContainsHitZone(CoreZoneId, TailBaseHitZoneId));
            Assert.AreEqual(intactHeadMemberCount, GetZone(severedZones, HeadZoneId).MemberCount);
            Assert.AreNotEqual(intactZoneRevision, severedZones.ZoneRevision);

            CharacterEngagementAnchorRuntimeBinding tailAnchor =
                FindAnchor(fixture.Package.Geometry.EngagementAnchors, TailAnchorId);
            Assert.IsNotNull(tailAnchor, "The authored tail Anchor identity remains in the binding.");
            Assert.AreEqual(TailZoneId, tailAnchor.ZoneId);

            var rebuilt = new CharacterTestDamageZoneSnapshot();
            rebuilt.Build(severedGeometry, fixture.Package.Geometry.DamageZones);
            AssertZoneSnapshotsEqual(severedZones, rebuilt);
        }

        [Test]
        public void AllExplicitLeavesInactive_RetainsAuthoredZonesButNoneAreRuntimeAvailable()
        {
            RuntimeFrame frame = new RuntimeFrame(32);
            CharacterTestCharacterEntity target = CreateEntity(2, 2);
            var geometry = new CharacterTestDamageGeometrySnapshot(2);
            geometry.BeginWrite(
                target.Id.Value,
                target.GameplayEntityId,
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value));
            geometry.CommitUnavailable(
                frame,
                worldRevision: 7,
                hurtboxRevision: 3,
                topologyRevision: 9,
                CharacterTestDamageGeometryStatus.ExplicitLeavesInactive);
            Assert.AreEqual(CharacterTestDamageGeometryStatus.ExplicitLeavesInactive, geometry.Status);
            CharacterDamageZoneRuntimeBinding[] bindings =
            {
                new CharacterDamageZoneRuntimeBinding(
                    HeadZoneId,
                    "Head",
                    new[] { CharacterTestAuthoredContentIds.D400_Id86, CharacterTestAuthoredContentIds.D400_Id104 },
                    true,
                    true,
                    true,
                    10,
                    "zone.head"),
                new CharacterDamageZoneRuntimeBinding(
                    TailZoneId,
                    "Tail",
                    new[] { CharacterTestAuthoredContentIds.D400_Id100 },
                    true,
                    true,
                    true,
                    1,
                    "zone.tail")
            };
            var zones = new CharacterTestDamageZoneSnapshot();
            zones.Build(geometry, bindings);
            Assert.IsTrue(zones.IsValid,
                "Explicit authored geometry keeps a typed Zone view instead of falling back to a synthetic body Zone.");
            Assert.AreEqual(bindings.Length, zones.ZoneCount);
            Assert.AreEqual(0, zones.AvailableZoneCount);
            Assert.AreEqual(0, zones.MemberCount);
            for (int i = 0; i < zones.ZoneCount; i++)
            {
                CharacterTestDamageZoneAggregate zone = zones.GetZone(i);
                Assert.IsFalse(zone.IsAvailable, zone.ZoneId.ToString());
                Assert.AreEqual(0, zone.MemberCount, zone.ZoneId.ToString());
            }

            var adapter = new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(null);
            Assert.IsFalse(adapter.TryProjectJointZoneState(
                target,
                zones,
                GetZone(zones, TailZoneId),
                out _));

            var rebuilt = new CharacterTestDamageZoneSnapshot();
            rebuilt.Build(geometry, bindings);
            AssertZoneSnapshotsEqual(zones, rebuilt);
        }

        [Test]
        public void TailRemoval_InvalidatesOnlyTailLockAndBothTargetingPathsExcludeUnavailableZone()
        {
            SelectionFixture fixture = CreateSelectionFixture();
            fixture.Evaluator.Set(
                FixtureTailZoneId,
                CharacterTestAttackOpportunityLevel.CurrentPose,
                tick: 3,
                FixtureTailHurtboxId,
                FixtureTailHitZoneId);
            fixture.Evaluator.Set(
                FixtureHeadZoneId,
                CharacterTestAttackOpportunityLevel.TurnInPlace,
                tick: 4,
                FixtureHeadHurtboxId,
                FixtureHeadHitZoneId);
            CharacterTestDamageZoneLock tailLock = fixture.SelectBefore(default).ZoneLock;
            Assert.AreEqual(FixtureTailZoneId, tailLock.ZoneId);

            fixture.Evaluator.Set(
                FixtureHeadZoneId,
                CharacterTestAttackOpportunityLevel.CurrentPose,
                tick: 5,
                FixtureHeadHurtboxId,
                FixtureHeadHitZoneId);
            int evaluationsBefore = fixture.Evaluator.EvaluationCount;
            CharacterTestDamageZoneSelectionResult reselected = fixture.SelectAfter(tailLock);
            Assert.AreEqual(FixtureHeadZoneId, reselected.Zone.ZoneId);
            Assert.AreEqual(
                CharacterTestDamageZoneSwitchReason.PreviousLockInvalidated,
                reselected.SwitchReason);
            Assert.AreEqual(evaluationsBefore + 1, fixture.Evaluator.EvaluationCount,
                "Attack Opportunity must not evaluate the unavailable tail Zone.");

            CharacterTestDamageZoneSelectionResult directTail = fixture.Selector.EvaluateZone(
                fixture.AfterQuery,
                fixture.AfterZones,
                FixtureTailZoneId);
            Assert.AreEqual(CharacterTestDamageZoneSelectionStatus.NoValidZone, directTail.Status);
            Assert.AreEqual(evaluationsBefore + 1, fixture.Evaluator.EvaluationCount);
            Assert.IsFalse(fixture.JointAdapter.TryProjectJointZoneState(
                fixture.Target,
                fixture.AfterZones,
                GetZone(fixture.AfterZones, FixtureTailZoneId),
                out _));

            fixture.Evaluator.Set(
                FixtureHeadZoneId,
                CharacterTestAttackOpportunityLevel.CurrentPose,
                tick: 3,
                FixtureHeadHurtboxId,
                FixtureHeadHitZoneId);
            fixture.Evaluator.Set(
                FixtureTailZoneId,
                CharacterTestAttackOpportunityLevel.TurnInPlace,
                tick: 4,
                FixtureTailHurtboxId,
                FixtureTailHitZoneId);
            CharacterTestDamageZoneLock headLock = fixture.SelectBefore(default).ZoneLock;
            CharacterTestDamageZoneSelectionResult kept = fixture.SelectAfter(headLock);
            Assert.AreEqual(FixtureHeadZoneId, kept.Zone.ZoneId);
            Assert.AreEqual(CharacterTestDamageZoneSwitchReason.PreviousLockKept, kept.SwitchReason,
                "A tail topology change must not stale an exact surviving head witness.");

            Assert.AreNotEqual(fixture.BeforeZones.ZoneRevision, fixture.AfterZones.ZoneRevision);
            Assert.AreEqual(
                fixture.BeforeZones.ZoneRevision,
                fixture.TopologyOnlyZones.ZoneRevision,
                "Global Combat topology is diagnostic and does not enter ZoneRevision.");
            Assert.AreNotEqual(
                fixture.BeforeZones.TopologyRevision,
                fixture.TopologyOnlyZones.TopologyRevision);
        }

        [Test]
        public void TargetingInvalidation_WarmedSelectorAndJointProjectionAllocateZeroBytes()
        {
            SelectionFixture fixture = CreateSelectionFixture();
            fixture.Evaluator.Set(
                FixtureHeadZoneId,
                CharacterTestAttackOpportunityLevel.CurrentPose,
                tick: 3,
                FixtureHeadHurtboxId,
                FixtureHeadHitZoneId);
            CharacterTestDamageZoneLock previous = fixture.SelectBefore(default).ZoneLock;
            CharacterTestDamageZoneAggregate tail = GetZone(fixture.AfterZones, FixtureTailZoneId);
            previous = fixture.SelectAfter(previous).ZoneLock;
            fixture.JointAdapter.TryProjectJointZoneState(
                fixture.Target,
                fixture.AfterZones,
                tail,
                out _);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool tailProjected = false;
            for (int i = 0; i < 4096; i++)
            {
                previous = fixture.SelectAfter(previous).ZoneLock;
                tailProjected |= fixture.JointAdapter.TryProjectJointZoneState(
                    fixture.Target,
                    fixture.AfterZones,
                    tail,
                    out _);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsFalse(tailProjected);
            Assert.AreEqual(FixtureHeadZoneId, previous.ZoneId);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void JointSelection_SweepsTailReservationButKeepsCoreAndPlannerRejectsTailAnchor()
        {
            using (DragonRuntimeFixture fixture = CreateDragonRuntimeFixture())
            {
                RuntimeFrame initialFrame = new RuntimeFrame(100);
                fixture.Tick(initialFrame);
                bool hasGeometry = fixture.Runtime.TryGetDamageGeometrySnapshot(
                    fixture.Target.Id.Value,
                    out CharacterTestDamageGeometrySnapshot initialGeometry);
                bool hasBinding = fixture.Runtime.TryGetRuntimeGeometryBinding(
                    fixture.Target.Id.Value,
                    out CharacterImportedGeometryBinding binding);
                Assert.IsTrue(fixture.Runtime.TryGetDamageZoneSnapshot(
                    fixture.Target.Id.Value,
                    out CharacterTestDamageZoneSnapshot intactZones),
                    "Initial runtime Zone snapshot is missing. geometry=" + hasGeometry
                    + " geometryStatus=" + (hasGeometry ? initialGeometry.Status.ToString() : "missing")
                    + " binding=" + hasBinding
                    + " entities=" + fixture.Runtime.Entities.Count);
                Assert.IsTrue(hasBinding, "Initial runtime geometry binding is missing.");
                CharacterEngagementAnchorRuntimeBinding tailAnchor =
                    FindAnchor(binding.EngagementAnchors, TailAnchorId);
                CharacterEngagementAnchorRuntimeBinding coreAnchor =
                    FindAnchor(binding.EngagementAnchors, CharacterTestAuthoredContentIds.D400_Id156);
                Assert.IsNotNull(tailAnchor);
                Assert.IsNotNull(coreAnchor);

                fixture.Navigation = BuildNavigation(fixture.Physics);
                Assert.IsTrue(fixture.Navigation.IsAvailable, fixture.Navigation.BuildResult.Status.ToString());
                RuntimeFrame topologyFrame = new RuntimeFrame(102);
                CharacterTestEngagementReservationAuthority reservations =
                    fixture.Adapter.ReservationAuthority;
                Assert.IsTrue(reservations.TryAcquire(
                    fixture.TailSource.Id.Value,
                    fixture.Target.Id.Value,
                    resolvedActionRevision: 5,
                    topologyFrame,
                    CreateReservationCandidate(
                        fixture,
                        intactZones,
                        binding,
                        tailAnchor,
                        topologyFrame),
                    out _),
                    "Tail reservation setup failed.");
                Assert.IsTrue(reservations.TryAcquire(
                    fixture.CoreSource.Id.Value,
                    fixture.Target.Id.Value,
                    resolvedActionRevision: 5,
                    topologyFrame,
                    CreateReservationCandidate(
                        fixture,
                        intactZones,
                        binding,
                        coreAnchor,
                        topologyFrame),
                    out _),
                    "Core reservation setup failed.");
                Assert.AreEqual(2, reservations.Count);

                SeverPart(fixture.Target, TailPartStateId, breakFrame: 101, severFrame: 102);
                fixture.Tick(topologyFrame);
                Assert.IsTrue(fixture.Runtime.TryGetDamageZoneSnapshot(
                    fixture.Target.Id.Value,
                    out CharacterTestDamageZoneSnapshot severedZones),
                    "Severed runtime Zone snapshot is missing.");
                Assert.IsFalse(severedZones.TryGetAvailableZone(TailZoneId, out _));
                Assert.IsTrue(severedZones.TryGetAvailableZone(CoreZoneId, out _),
                    "Core Zone should remain available after tail sever.");

                var planner = new CharacterTestEngagementPlanner();
                Assert.IsFalse(planner.TryProjectAndValidate(
                    new CharacterTestEngagementCandidateQuery(
                        topologyFrame,
                        fixture.TailSource,
                        fixture.Target,
                        5,
                        tailAnchor,
                        binding.EngagementAnchorRevision,
                        binding.EngagementAnchorContentDigest,
                        severedZones,
                        fixture.Physics,
                        fixture.Navigation,
                        CharacterTestAuthoredContentIds.D400_Id33,
                        1),
                    out _,
                    out CharacterTestEngagementCandidateStatus tailStatus));
                Assert.AreEqual(CharacterTestEngagementCandidateStatus.InvalidZone, tailStatus);

                planner.TryProjectAndValidate(
                    new CharacterTestEngagementCandidateQuery(
                        topologyFrame,
                        fixture.CoreSource,
                        fixture.Target,
                        5,
                        coreAnchor,
                        binding.EngagementAnchorRevision,
                        binding.EngagementAnchorContentDigest,
                        severedZones,
                        fixture.Physics,
                        fixture.Navigation,
                        CharacterTestAuthoredContentIds.D400_Id33,
                        1),
                    out _,
                    out CharacterTestEngagementCandidateStatus coreStatus);
                Assert.AreNotEqual(CharacterTestEngagementCandidateStatus.InvalidZone, coreStatus);

                Assert.IsTrue(CharacterTestRuntimeAiPlannerCompiledProfile.TryCompile(
                    CharacterTestRuntimeAiPlannerProfileSource.CreateDefault(),
                    out CharacterTestRuntimeAiPlannerCompiledProfile profile,
                    out _),
                    "Default Runtime AI Planner profile failed to compile.");
                var joint = new CharacterTestRuntimeAiJointRuntime(
                    fixture.Runtime,
                    fixture.Navigation,
                    fixture.Adapter);
                joint.BeginFrame();
                _ = joint.Select(
                    fixture.TailSource,
                    fixture.Runtime.Entities,
                    profile,
                    topologyFrame);

                Assert.AreEqual(1, reservations.Count);
                Assert.IsFalse(reservations.TryGetForSource(fixture.TailSource.Id.Value, out _));
                Assert.IsTrue(reservations.TryGetForSource(
                    fixture.CoreSource.Id.Value,
                    out CharacterTestEngagementReservation kept),
                    "Unrelated core reservation should remain active.");
                Assert.AreEqual(CoreZoneId, kept.ZoneId);
                Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id156, kept.AnchorId);
                Assert.AreEqual(0, kept.CapacitySlot);
            }
        }

        private static DragonZoneFixture CreateDragonZoneFixture()
        {
            CharacterTestCharacterDefinition definition = FindDragonDefinition();
            CharacterImportedPackage package =
                CharacterTestGeneratedPackageSource.LoadPackage(definition.GeneratedPackageRoot);
            Assert.IsNotNull(package);
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            return new DragonZoneFixture(
                world,
                target,
                new CharacterTestCharacterPhysicsRuntime(
                    new CombatPhysicsWorld(),
                    CombatPhysicsLayerMask.FromLayer(2),
                    new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                    Fix64.FromRatio(1, 100),
                    Fix64.Half,
                    staticColliderCount: 0,
                    sourceDescription: "lcc-m3-t3.zone-invalidation"),
                new CharacterTestCharacterHurtboxSystem(),
                package);
        }

        private static DragonRuntimeFixture CreateDragonRuntimeFixture()
        {
            CharacterTestCharacterDefinition definition = FindDragonDefinition();
            var physics = new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Zero,
                staticColliderCount: 1,
                sourceDescription: "lcc-m3-t3.reservation-invalidation");
            var runtime = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: physics,
                createViews: false);
            CharacterTestCharacterEntity tailSource = Spawn(
                runtime.World,
                definition,
                teamId: 1,
                x: -5f,
                CharacterTestCharacterControlOwner.AiControlled);
            CharacterTestCharacterEntity coreSource = Spawn(
                runtime.World,
                definition,
                teamId: 1,
                x: 5f,
                CharacterTestCharacterControlOwner.AiControlled);
            CharacterTestCharacterEntity target = Spawn(
                runtime.World,
                definition,
                teamId: 2,
                x: 0f,
                CharacterTestCharacterControlOwner.None);
            return new DragonRuntimeFixture(
                runtime,
                physics,
                tailSource,
                coreSource,
                target,
                new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(runtime));
        }

        private static CharacterTestCharacterEntity Spawn(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterDefinition definition,
            int teamId,
            float x,
            CharacterTestCharacterControlOwner owner)
        {
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId,
                    x,
                    y: 0f,
                    z: 0f,
                    owner),
                definition);
            entity.SetMotionState(0f, UnityEngine.Vector2.zero, isGrounded: true);
            return entity;
        }

        private static SelectionFixture CreateSelectionFixture()
        {
            var world = new CombatPhysicsWorld();
            RuntimeFrame frame = new RuntimeFrame(40);
            CharacterTestCharacterEntity source = CreateEntity(1, 1);
            CharacterTestCharacterEntity target = CreateEntity(2, 2);
            CharacterTestDamageGeometrySnapshot beforeGeometry = CreateGeometry(
                target,
                frame,
                world.Revision,
                topologyRevision: 7,
                includeTail: true);
            CharacterTestDamageGeometrySnapshot afterGeometry = CreateGeometry(
                target,
                frame,
                world.Revision,
                topologyRevision: 8,
                includeTail: false);
            CharacterTestDamageGeometrySnapshot topologyOnlyGeometry = CreateGeometry(
                target,
                frame,
                world.Revision,
                topologyRevision: 99,
                includeTail: true);
            CharacterDamageZoneRuntimeBinding[] bindings =
            {
                new CharacterDamageZoneRuntimeBinding(
                    FixtureHeadZoneId,
                    "Head",
                    new[] { FixtureHeadHitZoneId },
                    hittable: true,
                    aimable: true,
                    approachable: true,
                    authoredPriority: 10,
                    zoneLabel: "zone.head"),
                new CharacterDamageZoneRuntimeBinding(
                    FixtureTailZoneId,
                    "Tail",
                    new[] { FixtureTailHitZoneId },
                    hittable: true,
                    aimable: true,
                    approachable: true,
                    authoredPriority: 1,
                    zoneLabel: "zone.tail")
            };
            CharacterTestDamageZoneSnapshot beforeZones = BuildZones(beforeGeometry, bindings);
            CharacterTestDamageZoneSnapshot afterZones = BuildZones(afterGeometry, bindings);
            CharacterTestDamageZoneSnapshot topologyOnlyZones =
                BuildZones(topologyOnlyGeometry, bindings);
            var evaluator = new FakeEvaluator();
            return new SelectionFixture(
                source,
                target,
                beforeZones,
                afterZones,
                topologyOnlyZones,
                CreateQuery(world, source, target, beforeGeometry, frame),
                CreateQuery(world, source, target, afterGeometry, frame),
                evaluator);
        }

        private static CharacterTestDamageGeometrySnapshot CreateGeometry(
            CharacterTestCharacterEntity target,
            RuntimeFrame frame,
            int worldRevision,
            int topologyRevision,
            bool includeTail)
        {
            var geometry = new CharacterTestDamageGeometrySnapshot(2);
            geometry.BeginWrite(
                target.Id.Value,
                target.GameplayEntityId,
                new CombatEntityId(
                    CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value));
            AddCollider(geometry, FixtureHeadHurtboxId, "hurt.head", 1, FixtureHeadHitZoneId, "hit.head");
            if (includeTail)
                AddCollider(geometry, FixtureTailHurtboxId, "hurt.tail", 2, FixtureTailHitZoneId, "hit.tail");
            geometry.SortCollidersByStableLogicalId();
            geometry.Commit(frame, worldRevision, hurtboxRevision: 3, topologyRevision);
            return geometry;
        }

        private static void AddCollider(
            CharacterTestDamageGeometrySnapshot geometry,
            int logicalId,
            string logicalLabel,
            int colliderId,
            int hitZoneId,
            string hitZoneLabel)
        {
            geometry.AddCollider(
                logicalId,
                logicalLabel,
                new CombatColliderId(colliderId),
                logicalId,
                logicalLabel,
                hitZoneId,
                hitZoneLabel,
                0,
                CombatPhysicsShape.Aabb(FixVector3.Zero, FixVector3.Zero),
                FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.One, Fix64.One));
        }

        private static CharacterTestDamageZoneSnapshot BuildZones(
            CharacterTestDamageGeometrySnapshot geometry,
            CharacterDamageZoneRuntimeBinding[] bindings)
        {
            var zones = new CharacterTestDamageZoneSnapshot();
            zones.Build(geometry, bindings);
            return zones;
        }

        private static CharacterTestAttackOpportunityQuery CreateQuery(
            CombatPhysicsWorld world,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterTestDamageGeometrySnapshot geometry,
            RuntimeFrame frame)
        {
            Assert.IsTrue(CharacterTestAttackEvaluationSnapshot.TryCapture(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 3,
                sourceFacingRevision: 5,
                resolvedActionRevision: 13,
                out CharacterTestAttackEvaluationSnapshot snapshot));
            return new CharacterTestAttackOpportunityQuery(
                snapshot,
                geometry,
                world,
                source,
                target,
                default,
                false,
                default,
                false,
                frame,
                world.Revision,
                3,
                5,
                geometry.HurtboxRevision,
                13,
                null,
                null);
        }

        private static CharacterTestCharacterEntity CreateEntity(int id, int teamId)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(id),
                new CharacterTestCharacterIdentityComponent(
                    1,
                    "LCC-M3-T3",
                    default(ResourceKey),
                    default(CharacterRuntimeAnimationBinding)),
                new CharacterTestCharacterTeamComponent(teamId, teamId),
                new CharacterTestCharacterTransformComponent(id == 1 ? -3f : 0f, 0f, 0f, 0f),
                new CharacterTestCharacterEquipmentComponent(false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                new GameplayEntityId(id, 1),
                CharacterTestCharacterPhysicsComponent.Default);
        }

        private static CharacterTestNavigationSession BuildNavigation(
            CharacterTestCharacterPhysicsRuntime physics)
        {
            var input = new CharacterTestNavigationSessionBuildInput(
                revision: 31,
                minimumXMillimeters: -7000,
                maximumXMillimeters: 7000,
                minimumZMillimeters: -7000,
                maximumZMillimeters: 7000,
                planeYMillimeters: 0,
                cellSizeMillimeters: 1000,
                connectDiagonals: true,
                maxNodeCount: 512,
                maxEdgeCount: 4096,
                maxEndpointSnapDistanceMillimeters: 1500,
                maxNodeExpansions: 512,
                maxPathNodeCount: 128);
            return CharacterTestNavigationSessionBuilder.Build(
                input,
                physics,
                new CharacterTestNavigationSceneInputResult(
                    CharacterTestNavigationSceneInputStatus.Success));
        }

        private static CharacterTestEngagementCandidateResult CreateReservationCandidate(
            DragonRuntimeFixture fixture,
            CharacterTestDamageZoneSnapshot zones,
            CharacterImportedGeometryBinding binding,
            CharacterEngagementAnchorRuntimeBinding anchor,
            RuntimeFrame frame)
        {
            CharacterTestNavigationGraph graph = fixture.Navigation.Graph;
            var pose = new CharacterTestEngagementCandidatePose(
                frame,
                fixture.Physics.GetEntityId(
                    anchor.ZoneId == TailZoneId
                        ? fixture.TailSource
                        : fixture.CoreSource),
                fixture.Physics.GetEntityId(fixture.Target),
                resolvedActionRevision: 5,
                anchor.ZoneId,
                anchor.AnchorId,
                FixVector3.Zero,
                worldYawMilliDegrees: 0,
                sourcePoseRevision: 1,
                sourceFacingRevision: 1,
                targetPoseRevision: 1,
                targetFacingRevision: 1,
                targetXMillimeters: fixture.Target.Transform.XMillimeters,
                targetYMillimeters: fixture.Target.Transform.YMillimeters,
                targetZMillimeters: fixture.Target.Transform.ZMillimeters,
                targetYawMilliDegrees: fixture.Target.Transform.YawMilliDegrees,
                zoneBindingRevision: zones.BindingRevision,
                zoneRevision: zones.ZoneRevision,
                anchorRevision: binding.EngagementAnchorRevision,
                anchorContentHash: binding.EngagementAnchorContentDigest,
                phaseId: CharacterTestAuthoredContentIds.D400_Id33,
                phaseRevision: 1,
                combatPhysicsRevision: fixture.Physics.World.Revision,
                graphRevision: graph.Revision,
                graphContentHash: graph.ContentHash,
                standingNodeId: graph.GetNode(0).NodeId);
            var opportunity = new CharacterTestAttackOpportunityResult(
                CharacterTestAttackOpportunityStatus.Valid,
                CharacterTestAttackEvaluationMode.CandidatePose,
                CharacterTestAttackOpportunityLevel.RequiresTranslation,
                hasCompatibleHurtbox: true,
                envelopeIntersectsTarget: true,
                traceCanResolveTargetHit: true,
                canHitFromCurrentPose: false,
                canHitAfterTurnInPlace: false,
                requiresTranslation: true,
                requiredYawDeltaMilliDegrees: 0,
                earliestCandidateHitTick: 4,
                traceSampleIndex: 0,
                stableSampleId: 1,
                selectedHurtboxId: FixtureHeadHurtboxId,
                selectedHitZoneId: FixtureHeadHitZoneId,
                selectedDamageZoneId: anchor.ZoneId,
                witnessPoint: FixVector3.Zero,
                aimPoint: FixVector3.Zero,
                CharacterTestAttackOpportunityFailureReason.None,
                CharacterTestAttackOpportunityFailureFlags.None);
            var path = new CharacterTestPathfindingSample(
                CharacterTestPathStatus.Success,
                graph.Revision,
                graph.ContentHash,
                sourceNodeId: graph.GetNode(0).NodeId,
                targetNodeId: graph.GetNode(0).NodeId,
                expandedNodeCount: 1,
                pathNodeCount: 1,
                pathEdgeCount: 0,
                sourceSnapDistanceMillimeters: 0,
                targetSnapDistanceMillimeters: 0,
                remainingDistanceMillimeters: 0,
                totalCostMillimeters: 0,
                hasNextWaypoint: false,
                nextWaypoint: FixVector3.Zero);
            return new CharacterTestEngagementCandidateResult(
                CharacterTestEngagementCandidateStatus.Success,
                pose,
                opportunity,
                path,
                anchor.AuthoredPriority,
                anchor.Capacity,
                reservationCount: 0,
                isLockedZone: false);
        }

        private static CharacterTestCharacterDefinition FindDragonDefinition()
        {
            IReadOnlyList<CharacterTestCharacterDefinition> definitions =
                CharacterTestPlayableCharacterCatalog.Discover();
            for (int i = 0; i < definitions.Count; i++)
            {
                string root = (definitions[i].GeneratedPackageRoot ?? string.Empty)
                    .Replace('\\', '/')
                    .TrimEnd('/');
                if (root.EndsWith("/000003", StringComparison.OrdinalIgnoreCase))
                    return definitions[i];
            }
            Assert.Fail("Playable Dragon14 definition is missing.");
            return default;
        }

        private static void SeverPart(
            CharacterTestCharacterEntity entity,
            int partStateId,
            long breakFrame,
            long severFrame)
        {
            CharacterTestBodyPartStateComponent state = entity.BodyPartState;
            Assert.IsTrue(state.TryResolveDefinitionIndexByPartStateId(partStateId, out int index));
            Assert.IsTrue(state.TryGetDefinition(index, out CharacterTestBodyPartStateDefinition definition));
            Assert.IsTrue(state.TryBeginFrame(breakFrame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageAcceptedDamage(new CharacterTestBodyPartAcceptedDamageFact(
                    breakFrame,
                    sourceEntityId: 9001,
                    targetEntityId: entity.Id.Value,
                    actionInstanceId: 1,
                    policyEventId: 1,
                    traceId: 1,
                    localFrame: 1,
                    definitionIndex: index,
                    finalDamage: int.MaxValue)));
            Assert.IsTrue(state.CommitFrame().Succeeded);
            Assert.IsTrue(state.TryBeginFrame(severFrame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageSever(new CharacterTestBodyPartSeverFact(
                    severFrame,
                    sourceEntityId: 9001,
                    targetEntityId: entity.Id.Value,
                    definitionIndex: index,
                    severTag: definition.SeverRequiredTag,
                    correlationId: severFrame)));
            Assert.IsTrue(state.CommitFrame().Succeeded);
        }

        private static CharacterTestDamageZoneAggregate GetZone(
            CharacterTestDamageZoneSnapshot zones,
            int zoneId)
        {
            Assert.IsTrue(zones.TryGetZone(zoneId, out CharacterTestDamageZoneAggregate zone));
            return zone;
        }

        private static CharacterEngagementAnchorRuntimeBinding FindAnchor(
            CharacterEngagementAnchorRuntimeBinding[] anchors,
            int anchorId)
        {
            for (int i = 0; anchors != null && i < anchors.Length; i++)
            {
                if (anchors[i].AnchorId == anchorId)
                    return anchors[i];
            }
            return null;
        }

        private static void AssertZoneSnapshotsEqual(
            CharacterTestDamageZoneSnapshot expected,
            CharacterTestDamageZoneSnapshot actual)
        {
            Assert.AreEqual(expected.IsValid, actual.IsValid);
            Assert.AreEqual(expected.BindingRevision, actual.BindingRevision);
            Assert.AreEqual(expected.ZoneRevision, actual.ZoneRevision);
            Assert.AreEqual(expected.ZoneCount, actual.ZoneCount);
            Assert.AreEqual(expected.AvailableZoneCount, actual.AvailableZoneCount);
            Assert.AreEqual(expected.MemberCount, actual.MemberCount);
            for (int i = 0; i < expected.ZoneCount; i++)
            {
                CharacterTestDamageZoneAggregate left = expected.GetZone(i);
                CharacterTestDamageZoneAggregate right = actual.GetZone(i);
                Assert.AreEqual(left.ZoneId, right.ZoneId);
                Assert.AreEqual(left.IsAvailable, right.IsAvailable, left.ZoneId.ToString());
                Assert.AreEqual(left.MemberStart, right.MemberStart, left.ZoneId.ToString());
                Assert.AreEqual(left.MemberCount, right.MemberCount, left.ZoneId.ToString());
            }
            for (int i = 0; i < expected.MemberCount; i++)
            {
                CharacterTestDamageZoneMember left = expected.GetMember(i);
                CharacterTestDamageZoneMember right = actual.GetMember(i);
                Assert.AreEqual(left.HitZoneId, right.HitZoneId);
                Assert.AreEqual(left.LogicalHurtboxId, right.LogicalHurtboxId);
                Assert.AreEqual(left.CombatColliderId, right.CombatColliderId);
            }
        }

        private sealed class DragonZoneFixture
        {
            internal DragonZoneFixture(
                CharacterTestCharacterRuntimeWorld world,
                CharacterTestCharacterEntity target,
                CharacterTestCharacterPhysicsRuntime physics,
                CharacterTestCharacterHurtboxSystem hurtboxes,
                CharacterImportedPackage package)
            {
                World = world;
                Target = target;
                Physics = physics;
                Hurtboxes = hurtboxes;
                Package = package;
            }

            internal CharacterTestCharacterRuntimeWorld World { get; }
            internal CharacterTestCharacterEntity Target { get; }
            internal CharacterTestCharacterPhysicsRuntime Physics { get; }
            internal CharacterTestCharacterHurtboxSystem Hurtboxes { get; }
            internal CharacterImportedPackage Package { get; }

            internal void Sync(RuntimeFrame frame) =>
                Hurtboxes.SyncAll(World.Entities, Physics, frame);
        }

        private sealed class DragonRuntimeFixture : IDisposable
        {
            internal DragonRuntimeFixture(
                CharacterTestCharacterRuntimeModule runtime,
                CharacterTestCharacterPhysicsRuntime physics,
                CharacterTestCharacterEntity tailSource,
                CharacterTestCharacterEntity coreSource,
                CharacterTestCharacterEntity target,
                CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter)
            {
                Runtime = runtime;
                Physics = physics;
                TailSource = tailSource;
                CoreSource = coreSource;
                Target = target;
                Adapter = adapter;
            }

            internal CharacterTestCharacterRuntimeModule Runtime { get; }
            internal CharacterTestCharacterPhysicsRuntime Physics { get; }
            internal CharacterTestCharacterEntity TailSource { get; }
            internal CharacterTestCharacterEntity CoreSource { get; }
            internal CharacterTestCharacterEntity Target { get; }
            internal CharacterTestRuntimeAiPlannerAttackOpportunityAdapter Adapter { get; }
            internal CharacterTestNavigationSession Navigation { get; set; }

            internal void Tick(RuntimeFrame frame)
            {
                Runtime.Tick(new RuntimeTickContext(
                    frame.Value,
                    1d / 60d,
                    frame.Value / 60d,
                    RuntimeTickStage.Simulation));
            }

            public void Dispose() => Runtime.Dispose();
        }

        private sealed class SelectionFixture
        {
            internal SelectionFixture(
                CharacterTestCharacterEntity source,
                CharacterTestCharacterEntity target,
                CharacterTestDamageZoneSnapshot beforeZones,
                CharacterTestDamageZoneSnapshot afterZones,
                CharacterTestDamageZoneSnapshot topologyOnlyZones,
                CharacterTestAttackOpportunityQuery beforeQuery,
                CharacterTestAttackOpportunityQuery afterQuery,
                FakeEvaluator evaluator)
            {
                Source = source;
                Target = target;
                BeforeZones = beforeZones;
                AfterZones = afterZones;
                TopologyOnlyZones = topologyOnlyZones;
                BeforeQuery = beforeQuery;
                AfterQuery = afterQuery;
                Evaluator = evaluator;
                Selector = new CharacterTestDamageZoneSelector(evaluator);
                JointAdapter = new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(null);
            }

            internal CharacterTestCharacterEntity Source { get; }
            internal CharacterTestCharacterEntity Target { get; }
            internal CharacterTestDamageZoneSnapshot BeforeZones { get; }
            internal CharacterTestDamageZoneSnapshot AfterZones { get; }
            internal CharacterTestDamageZoneSnapshot TopologyOnlyZones { get; }
            internal CharacterTestAttackOpportunityQuery BeforeQuery { get; }
            internal CharacterTestAttackOpportunityQuery AfterQuery { get; }
            internal FakeEvaluator Evaluator { get; }
            internal CharacterTestDamageZoneSelector Selector { get; }
            internal CharacterTestRuntimeAiPlannerAttackOpportunityAdapter JointAdapter { get; }

            internal CharacterTestDamageZoneSelectionResult SelectBefore(
                CharacterTestDamageZoneLock previous) =>
                Selector.Select(BeforeQuery, BeforeZones, previous);

            internal CharacterTestDamageZoneSelectionResult SelectAfter(
                CharacterTestDamageZoneLock previous) =>
                Selector.Select(AfterQuery, AfterZones, previous);
        }

        private sealed class FakeEvaluator : ICharacterTestAttackOpportunityEvaluator
        {
            private readonly Dictionary<int, CharacterTestAttackOpportunityResult> _results =
                new Dictionary<int, CharacterTestAttackOpportunityResult>();

            internal int EvaluationCount { get; private set; }

            internal void Set(
                int zoneId,
                CharacterTestAttackOpportunityLevel level,
                int tick,
                int hurtboxId,
                int hitZoneId)
            {
                _results[zoneId] = new CharacterTestAttackOpportunityResult(
                    CharacterTestAttackOpportunityStatus.Valid,
                    CharacterTestAttackEvaluationMode.CurrentPose,
                    level,
                    hasCompatibleHurtbox: true,
                    envelopeIntersectsTarget: true,
                    traceCanResolveTargetHit: true,
                    canHitFromCurrentPose: level == CharacterTestAttackOpportunityLevel.CurrentPose,
                    canHitAfterTurnInPlace: level >= CharacterTestAttackOpportunityLevel.TurnInPlace,
                    requiresTranslation: level == CharacterTestAttackOpportunityLevel.RequiresTranslation,
                    requiredYawDeltaMilliDegrees: 0,
                    earliestCandidateHitTick: tick,
                    traceSampleIndex: 0,
                    stableSampleId: 1,
                    selectedHurtboxId: hurtboxId,
                    selectedHitZoneId: hitZoneId,
                    selectedDamageZoneId: zoneId,
                    witnessPoint: FixVector3.Zero,
                    aimPoint: FixVector3.Zero,
                    CharacterTestAttackOpportunityFailureReason.None,
                    CharacterTestAttackOpportunityFailureFlags.None);
            }

            public CharacterTestAttackOpportunityResult Evaluate(
                CharacterTestAttackOpportunityQuery query,
                int requiredYawDeltaMilliDegrees = 0)
            {
                EvaluationCount++;
                return _results.TryGetValue(
                    query.RequiredDamageZoneId,
                    out CharacterTestAttackOpportunityResult result)
                    ? result
                    : default;
            }
        }
    }
}

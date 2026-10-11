using System;
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
    public sealed class CharacterTestCharacterDistanceM3AnchorRuntimeTests
    {
        private static readonly RuntimePreparedDigest AnchorDigest = RuntimePreparedDigest.PrepareExisting("sha256:1111111111111111111111111111111111111111111111111111111111111111");
        private static readonly RuntimePreparedDigest ChangedAnchorDigest = RuntimePreparedDigest.PrepareExisting("sha256:2222222222222222222222222222222222222222222222222222222222222222");
        private const int SyntheticHitBodyId = 1201401;
        private const int SyntheticHurtBodyId = 1201402;

        private const string V8Prefix =
            "{\"format\":\"mx.characterGeometryBinding.v8\",\"schemaVersion\":\"8.0\",\"features\":2,\"packageId\":\"dragon\","
            + "\"bodyColliders\":[],\"hitZoneBindings\":[{\"hitZoneId\":55}],"
            + "\"damageZones\":[{\"zoneId\":151,\"zoneLabel\":\"zone.approach\",\"displayName\":\"Approach\","
            + "\"hitZoneIds\":[55],\"hittable\":true,\"aimable\":true,"
            + "\"approachable\":true,\"authoredPriority\":3}],\"engagementAnchorRevision\":7,"
            + "\"engagementAnchorContentHash\":\"sha256:e08b99f656b39808688b00040043fd23984c5b2b663b2b3206da5cb5d428c85a\","
            + "\"engagementAnchors\":";

        private const string V8Suffix =
            ",\"sockets\":[],\"weaponAttachments\":[],\"weaponTraces\":[]}";

        private const string ValidAnchor =
            "{\"anchorId\":156,\"anchorLabel\":\"anchor.alpha\",\"zoneId\":151,\"zoneLabel\":\"zone.approach\","
            + "\"referenceFrame\":\"ModelRoot\",\"localPositionMillimeters\":{\"x\":-1500,\"y\":25,\"z\":2000},"
            + "\"yawMilliDegrees\":90000,\"capacity\":1,\"authoredPriority\":9,"
            + "\"enabled\":true,\"phaseId\":0,\"phaseLabel\":\"\"}";

        [Test]
        public void GeometryBindingV8_WithoutAnchorFeatureRemainsRootApproach()
        {
            CharacterImportedGeometryBinding binding = CharacterImportedPackageJson.LoadGeometryBinding(
                V8Prefix.Replace("\"features\":2", "\"features\":0") + "[]" + V8Suffix);
            Assert.AreEqual(CharacterEngagementAnchorMode.CompatibilityRootApproach, binding.EngagementAnchorMode);
            Assert.AreEqual(0, binding.EngagementAnchors.Length);
            Assert.AreEqual(0, binding.EngagementAnchorRevision);
            Assert.AreEqual(string.Empty, binding.EngagementAnchorContentHash);
        }

        [Test]
        public void GeometryBindingV8_ParsesCanonicalImmutableEngagementAnchor()
        {
            CharacterImportedGeometryBinding binding = CharacterImportedPackageJson.LoadGeometryBinding(
                V8Prefix + "[" + ValidAnchor + "]" + V8Suffix);

            Assert.AreEqual(CharacterEngagementAnchorMode.AuthoredAnchors, binding.EngagementAnchorMode);
            Assert.AreEqual(7, binding.EngagementAnchorRevision);
            Assert.AreEqual(1, binding.EngagementAnchors.Length);
            CharacterEngagementAnchorRuntimeBinding anchor = binding.EngagementAnchors[0];
            Assert.AreEqual(156, anchor.AnchorId);
            Assert.AreEqual("anchor.alpha", anchor.AnchorLabel);
            Assert.AreEqual(151, anchor.ZoneId);
            Assert.AreEqual("zone.approach", anchor.ZoneLabel);
            Assert.AreEqual(CharacterEngagementAnchorReferenceFrame.ModelRoot, anchor.ReferenceFrame);
            Assert.AreEqual(-1500, anchor.LocalPositionMillimeters.X);
            Assert.AreEqual(25, anchor.LocalPositionMillimeters.Y);
            Assert.AreEqual(2000, anchor.LocalPositionMillimeters.Z);
            Assert.AreEqual(90000, anchor.YawMilliDegrees);
            Assert.AreEqual(1, anchor.Capacity);
            Assert.AreEqual(9, anchor.AuthoredPriority);
            Assert.IsTrue(anchor.Enabled);
            Assert.AreEqual(0, anchor.PhaseId);
        }

        [Test]
        public void GeometryBindingV8_RejectsBadFrameZoneOrderAndHash()
        {
            Assert.Throws<CharacterImportedPackageJsonException>(() =>
                CharacterImportedPackageJson.LoadGeometryBinding(
                    V8Prefix + "[" + ValidAnchor.Replace("ModelRoot", "World") + "]" + V8Suffix));
            Assert.Throws<CharacterImportedPackageJsonException>(() =>
                CharacterImportedPackageJson.LoadGeometryBinding(
                    V8Prefix + "[" + ValidAnchor.Replace("\"zoneId\":151", "\"zoneId\":999") + "]" + V8Suffix));
            Assert.Throws<CharacterImportedPackageJsonException>(() =>
                CharacterImportedPackageJson.LoadGeometryBinding(
                    (V8Prefix + "[" + ValidAnchor + "]" + V8Suffix).Replace(
                        "sha256:e08b99f6",
                        "sha256:f08b99f6")));
        }

        [Test]
        public void GeometryBindingV8_HashUsesNumericIdentityAndIgnoresDisplayLabels()
        {
            string json = V8Prefix + "[" + ValidAnchor + "]" + V8Suffix;
            CharacterImportedGeometryBinding renamed = CharacterImportedPackageJson.LoadGeometryBinding(
                json.Replace("anchor.alpha", "anchor.renamed")
                    .Replace("zone.approach", "zone.renamed")
                    .Replace("\"phaseLabel\":\"\"", "\"phaseLabel\":\"display-only\""));
            Assert.AreEqual(156, renamed.EngagementAnchors[0].AnchorId);
            Assert.AreEqual("anchor.renamed", renamed.EngagementAnchors[0].AnchorLabel);

            foreach (string changed in new[]
            {
                json.Replace("\"anchorId\":156", "\"anchorId\":157"),
                json.Replace("\"zoneId\":151", "\"zoneId\":152"),
                json.Replace("\"phaseId\":0", "\"phaseId\":33")
            })
            {
                CharacterImportedPackageJsonException error = Assert.Throws<CharacterImportedPackageJsonException>(
                    () => CharacterImportedPackageJson.LoadGeometryBinding(changed));
                StringAssert.Contains("CHARANCH102_CONTENT_HASH_MISMATCH", error.Message);
            }
        }

        [Test]
        public void ReservationAuthority_SameFrameConflict_IsIndependentOfEvaluationOrder()
        {
            CharacterTestEngagementCandidateResult high = CreateCandidate(sourceCombatId: 20, capacity: 1);
            CharacterTestEngagementCandidateResult low = CreateCandidate(sourceCombatId: 10, capacity: 1);
            var highThenLow = new CharacterTestEngagementReservationAuthority();
            Assert.IsTrue(highThenLow.TryAcquire(20, 200, 5, new RuntimeFrame(12), high, out _));
            Assert.IsTrue(highThenLow.TryAcquire(10, 200, 5, new RuntimeFrame(12), low, out _));
            Assert.IsFalse(highThenLow.TryGetForSource(20, out _));
            Assert.IsTrue(highThenLow.TryGetForSource(10, out CharacterTestEngagementReservation first));

            var lowThenHigh = new CharacterTestEngagementReservationAuthority();
            Assert.IsTrue(lowThenHigh.TryAcquire(10, 200, 5, new RuntimeFrame(12), low, out _));
            Assert.IsFalse(lowThenHigh.TryAcquire(20, 200, 5, new RuntimeFrame(12), high, out _));
            Assert.IsTrue(lowThenHigh.TryGetForSource(10, out CharacterTestEngagementReservation second));

            Assert.AreEqual(first.SourceEntityId, second.SourceEntityId);
            Assert.AreEqual(first.CapacitySlot, second.CapacitySlot);
            Assert.AreEqual(0, first.CapacitySlot);
        }

        [Test]
        public void ReservationAuthority_CanAcquirePredictsConflictWithoutMutation()
        {
            var authority = new CharacterTestEngagementReservationAuthority();
            CharacterTestEngagementCandidateResult incumbent =
                CreateCandidate(sourceCombatId: 20, capacity: 1);
            Assert.IsTrue(authority.TryAcquire(
                20,
                200,
                5,
                new RuntimeFrame(12),
                incumbent,
                out CharacterTestEngagementReservation before));

            CharacterTestEngagementCandidateResult lowerStableOwner =
                CreateCandidate(sourceCombatId: 10, capacity: 1);
            CharacterTestEngagementCandidateResult higherStableOwner =
                CreateCandidate(sourceCombatId: 30, capacity: 1);
            Assert.IsTrue(authority.CanAcquire(
                10, 200, 5, new RuntimeFrame(12), lowerStableOwner));
            Assert.IsFalse(authority.CanAcquire(
                30, 200, 5, new RuntimeFrame(12), higherStableOwner));

            Assert.AreEqual(1, authority.Count);
            Assert.IsTrue(authority.TryGetForSource(20, out CharacterTestEngagementReservation after));
            Assert.AreEqual(before.SourceEntityId, after.SourceEntityId);
            Assert.AreEqual(before.CapacitySlot, after.CapacitySlot);
            Assert.IsFalse(authority.TryGetForSource(10, out _));
            Assert.IsFalse(authority.TryGetForSource(30, out _));
        }

        [Test]
        public void ReservationAuthority_RevisionChangesOnlyWhenAuthorityMutates()
        {
            var authority = new CharacterTestEngagementReservationAuthority();
            int initial = authority.Revision;
            CharacterTestEngagementCandidateResult candidate =
                CreateCandidate(sourceCombatId: 10, capacity: 1);

            Assert.IsTrue(authority.CanAcquire(
                10, 200, 5, new RuntimeFrame(12), candidate));
            Assert.AreEqual(initial, authority.Revision);
            Assert.IsTrue(authority.TryAcquire(
                10, 200, 5, new RuntimeFrame(12), candidate, out _));
            Assert.AreNotEqual(initial, authority.Revision);
            int acquired = authority.Revision;

            Assert.IsTrue(authority.CanAcquire(
                10, 200, 5, new RuntimeFrame(13), candidate));
            Assert.AreEqual(acquired, authority.Revision);
            Assert.IsTrue(authority.Release(
                10,
                CharacterTestEngagementReservationReleaseReason.NotRenewed));
            Assert.AreNotEqual(acquired, authority.Revision);
        }

        [Test]
        public void ReservationAuthority_CaptureRestore_PreservesCanonicalSlotsAndWitnesses()
        {
            var original = new CharacterTestEngagementReservationAuthority();
            CharacterTestEngagementCandidateResult second = CreateCandidate(sourceCombatId: 20, capacity: 2);
            CharacterTestEngagementCandidateResult first = CreateCandidate(sourceCombatId: 10, capacity: 2);
            Assert.IsTrue(original.TryAcquire(20, 200, 5, new RuntimeFrame(12), second, out _));
            Assert.IsTrue(original.TryAcquire(10, 200, 5, new RuntimeFrame(12), first, out _));

            CharacterTestEngagementReservationStateSnapshot[] captured = original.Capture();
            var restored = new CharacterTestEngagementReservationAuthority();
            Assert.IsTrue(restored.TryRestore(captured, out string failure), failure);
            CharacterTestEngagementReservationStateSnapshot[] roundTrip = restored.Capture();

            Assert.AreEqual(2, roundTrip.Length);
            Assert.AreEqual(10, roundTrip[0].SourceEntityId);
            Assert.AreEqual(0, roundTrip[0].CapacitySlot);
            Assert.AreEqual(20, roundTrip[1].SourceEntityId);
            Assert.AreEqual(1, roundTrip[1].CapacitySlot);
            Assert.AreEqual(AnchorDigest, roundTrip[0].AnchorContentHash);
            Assert.AreEqual(77L, roundTrip[0].GraphContentHash);
        }

        [Test]
        public void ReservationAuthority_WarmedRenewal_AllocatesZeroBytes()
        {
            var authority = new CharacterTestEngagementReservationAuthority();
            CharacterTestEngagementCandidateResult candidate = CreateCandidate(sourceCombatId: 10, capacity: 1);
            Assert.IsTrue(authority.TryAcquire(10, 200, 5, new RuntimeFrame(12), candidate, out _));
            Assert.IsTrue(authority.TryAcquire(10, 200, 5, new RuntimeFrame(13), candidate, out _));

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            Assert.IsTrue(authority.TryAcquire(10, 200, 5, new RuntimeFrame(14), candidate, out _));
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void PlannerSnapshot_ReservationCloneAndHash_CoverAuthoritativeWitness()
        {
            CharacterTestEngagementReservationStateSnapshot reservation = CreateReservationSnapshot();
            var state = new CharacterTestRuntimeAiPlannerStateSnapshot
            {
                SchemaVersion = CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion,
                Slots = Array.Empty<CharacterTestRuntimeAiPlannerSlotStateSnapshot>(),
                BehaviorLogicSlots = Array.Empty<CharacterTestRuntimeAiLabLogicSlotStateSnapshot>(),
                DamageZoneLocks = Array.Empty<CharacterTestDamageZoneLockStateSnapshot>(),
                EngagementReservations = new[] { reservation }
            };
            CharacterTestRuntimeAiPlannerStateSnapshot clone = state.CloneForRollback();
            long baseline = Hash(state);

            state.EngagementReservations[0].AnchorContentHash = ChangedAnchorDigest;

            Assert.AreEqual(AnchorDigest, clone.EngagementReservations[0].AnchorContentHash);
            Assert.AreNotEqual(baseline, Hash(state));
            Assert.AreEqual(12, CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion);
        }

        [Test]
        public void PlannerSnapshot_HashIgnoresProcessLocalCombatPhysicsRevisionWitness()
        {
            CharacterTestRuntimeAiPlannerStateSnapshot baseline =
                CharacterTestRuntimeAiPlannerStateSnapshot.Empty();
            baseline.EngagementReservations = new[] { CreateReservationSnapshot() };
            CharacterTestRuntimeAiPlannerStateSnapshot rebuilt = baseline.CloneForRollback();
            rebuilt.EngagementReservations[0].CombatPhysicsRevision++;

            Assert.AreEqual(Hash(baseline), Hash(rebuilt));
        }

        [Test]
        public void NetworkSchema28_DropsReservations_WhileSchema29RetainsThem()
        {
            CharacterTestEngagementReservationStateSnapshot reservation = CreateReservationSnapshot();
            CharacterTestRuntimeAiPlannerStateSnapshot planner = CharacterTestRuntimeAiPlannerStateSnapshot.Empty();
            planner.EngagementReservations = new[] { reservation };
            var legacy = new CharacterTestNetworkSnapshotDocument { RuntimeAiPlannerState = planner };

            legacy.NormalizeForSchema(CharacterTestNetworkSnapshotParticipant.DamageZoneLockSchemaVersionValue);

            Assert.AreEqual(CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion,
                legacy.RuntimeAiPlannerState.SchemaVersion);
            Assert.AreEqual(0, legacy.RuntimeAiPlannerState.EngagementReservations.Length);

            CharacterTestRuntimeAiPlannerStateSnapshot currentPlanner = CharacterTestRuntimeAiPlannerStateSnapshot.Empty();
            currentPlanner.EngagementReservations = new[] { CreateReservationSnapshot() };
            var current = new CharacterTestNetworkSnapshotDocument { RuntimeAiPlannerState = currentPlanner };
            current.NormalizeForSchema(CharacterTestNetworkSnapshotParticipant.SchemaVersionValue);
            Assert.AreEqual(1, current.RuntimeAiPlannerState.EngagementReservations.Length);
        }

        [Test]
        public void EngagementPlanner_ProjectsModelRootPoseAndRejectsStaleWitness()
        {
            EngagementFixture fixture = CreateEngagementFixture();
            Assert.IsTrue(fixture.Planner.TryProjectAndValidate(
                fixture.Query,
                out CharacterTestEngagementCandidatePose pose,
                out CharacterTestEngagementCandidateStatus status), status.ToString());
            Assert.AreEqual(1000, CharacterTestDeterministicUnits.Fix64MetersToMillimeters(pose.WorldPosition.X));
            Assert.AreEqual(1000, CharacterTestDeterministicUnits.Fix64MetersToMillimeters(pose.WorldPosition.Z));
            Assert.AreEqual(180000, pose.WorldYawMilliDegrees);
            Assert.IsTrue(fixture.Planner.TryEvaluatePath(
                fixture.Query,
                pose,
                out CharacterTestPathfindingSample path,
                out status), status.ToString());
            Assert.AreEqual(CharacterTestPathStatus.Success, path.Status);

            CharacterTestEngagementCandidateQuery stale = fixture.CreateQuery(
                anchorRevision: fixture.Query.AnchorRevision + 1);
            Assert.IsFalse(fixture.Planner.TryEvaluatePath(stale, pose, out _, out status));
            Assert.AreEqual(CharacterTestEngagementCandidateStatus.StaleSnapshot, status);
        }

        [Test]
        public void EngagementPlanner_StandingCollisionReturnsTypedBlockedFailure()
        {
            EngagementFixture fixture = CreateEngagementFixture(addBlockerAfterGraphBuild: true);
            Assert.IsFalse(fixture.Planner.TryProjectAndValidate(
                fixture.Query,
                out _,
                out CharacterTestEngagementCandidateStatus status));
            Assert.AreEqual(CharacterTestEngagementCandidateStatus.Blocked, status);
        }

        [Test]
        public void EngagementPlanner_StableComparatorUsesLockPriorityCostTickAndIds()
        {
            CharacterTestEngagementCandidateResult baseline = CreateCandidate(
                10, 2, anchorId: 1201404, zoneId: 1201407,
                authoredPriority: 1, pathCost: 2000, earliestTick: 5);
            Assert.IsTrue(CharacterTestEngagementPlanner.IsBetter(
                CreateCandidate(10, 2, anchorId: 1201405, zoneId: 1201408,
                    authoredPriority: 0, pathCost: 9000, earliestTick: 9, isLockedZone: true),
                baseline));
            Assert.IsTrue(CharacterTestEngagementPlanner.IsBetter(
                CreateCandidate(10, 2, anchorId: 1201405, zoneId: 1201408,
                    authoredPriority: 2, pathCost: 9000, earliestTick: 9),
                baseline));
            Assert.IsTrue(CharacterTestEngagementPlanner.IsBetter(
                CreateCandidate(10, 2, anchorId: 1201405, zoneId: 1201408,
                    authoredPriority: 1, pathCost: 1000, earliestTick: 9),
                baseline));
            Assert.IsTrue(CharacterTestEngagementPlanner.IsBetter(
                CreateCandidate(10, 2, anchorId: 1201405, zoneId: 1201408,
                    authoredPriority: 1, pathCost: 2000, earliestTick: 4),
                baseline));
            Assert.IsTrue(CharacterTestEngagementPlanner.IsBetter(
                CreateCandidate(10, 2, anchorId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0007, zoneId: 1201406,
                    authoredPriority: 1, pathCost: 2000, earliestTick: 5),
                baseline));
            Assert.IsFalse(CharacterTestEngagementPlanner.IsBetter(
                CreateCandidate(10, 1, reservationCount: 1), baseline));
        }

        [Test]
        public void EngagementPlanner_WarmedProjectionStandingPathAndSelectionAllocateZeroBytes()
        {
            EngagementFixture fixture = CreateEngagementFixture();
            Assert.IsTrue(fixture.Planner.TryProjectAndValidate(fixture.Query, out var pose, out _));
            Assert.IsTrue(fixture.Planner.TryEvaluatePath(fixture.Query, pose, out _, out _));
            CharacterTestEngagementCandidateResult left = CreateCandidate(10, 2, pathCost: 900);
            CharacterTestEngagementCandidateResult right = CreateCandidate(10, 2, pathCost: 1000);
            _ = CharacterTestEngagementPlanner.IsBetter(left, right);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int successes = 0;
            for (int i = 0; i < 32; i++)
            {
                if (fixture.Planner.TryProjectAndValidate(fixture.Query, out pose, out _)
                    && fixture.Planner.TryEvaluatePath(fixture.Query, pose, out _, out _)
                    && CharacterTestEngagementPlanner.IsBetter(left, right))
                {
                    successes++;
                }
            }
            Assert.AreEqual(32, successes);
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Test]
        public void ReservationAuthority_CapacityTwoReleaseAndReacquireUseCanonicalSlots()
        {
            var authority = new CharacterTestEngagementReservationAuthority();
            Assert.IsTrue(authority.TryAcquire(30, 200, 5, new RuntimeFrame(12), CreateCandidate(30, 2), out _));
            Assert.IsTrue(authority.TryAcquire(10, 200, 5, new RuntimeFrame(12), CreateCandidate(10, 2), out _));
            Assert.IsFalse(authority.TryAcquire(20, 200, 5, new RuntimeFrame(13), CreateCandidate(20, 2), out _));
            Assert.IsTrue(authority.Release(10, CharacterTestEngagementReservationReleaseReason.NotRenewed));
            Assert.IsTrue(authority.TryAcquire(20, 200, 5, new RuntimeFrame(13), CreateCandidate(20, 2), out _));
            CharacterTestEngagementReservationStateSnapshot[] values = authority.Capture();
            Assert.AreEqual(2, values.Length);
            Assert.AreEqual(20, values[0].SourceEntityId);
            Assert.AreEqual(0, values[0].CapacitySlot);
            Assert.AreEqual(30, values[1].SourceEntityId);
            Assert.AreEqual(1, values[1].CapacitySlot);
        }

        private static CharacterTestEngagementCandidateResult CreateCandidate(
            int sourceCombatId,
            int capacity,
            int anchorId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0007,
            int zoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0090,
            int authoredPriority = 1,
            long pathCost = 1000,
            int earliestTick = 4,
            int reservationCount = 0,
            bool isLockedZone = false)
        {
            var pose = new CharacterTestEngagementCandidatePose(
                new RuntimeFrame(12),
                new CombatEntityId(sourceCombatId),
                new CombatEntityId(200),
                resolvedActionRevision: 5,
                zoneId,
                anchorId,
                FixVector3.Zero,
                worldYawMilliDegrees: 0,
                sourcePoseRevision: 3,
                sourceFacingRevision: 4,
                targetPoseRevision: 5,
                targetFacingRevision: 6,
                targetXMillimeters: 1000,
                targetYMillimeters: 0,
                targetZMillimeters: 2000,
                targetYawMilliDegrees: 90000,
                zoneBindingRevision: 7,
                zoneRevision: 8,
                anchorRevision: 9,
                anchorContentHash: AnchorDigest,
                phaseId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id33,
                phaseRevision: 1,
                combatPhysicsRevision: 10,
                graphRevision: 11,
                graphContentHash: 77L,
                standingNodeId: 12);
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
                earliestCandidateHitTick: earliestTick,
                traceSampleIndex: 0,
                stableSampleId: 1,
                selectedHurtboxId: SyntheticHurtBodyId,
                selectedHitZoneId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0028,
                selectedDamageZoneId: zoneId,
                witnessPoint: FixVector3.Zero,
                aimPoint: FixVector3.Zero,
                failureReason: CharacterTestAttackOpportunityFailureReason.None,
                failureFlags: CharacterTestAttackOpportunityFailureFlags.None);
            var path = new CharacterTestPathfindingSample(
                CharacterTestPathStatus.Success,
                graphRevision: 11,
                graphContentHash: 77L,
                sourceNodeId: 1,
                targetNodeId: 12,
                expandedNodeCount: 2,
                pathNodeCount: 2,
                pathEdgeCount: 1,
                sourceSnapDistanceMillimeters: 0,
                targetSnapDistanceMillimeters: 0,
                remainingDistanceMillimeters: 1000,
                totalCostMillimeters: pathCost,
                hasNextWaypoint: true,
                nextWaypoint: FixVector3.Zero);
            return new CharacterTestEngagementCandidateResult(
                CharacterTestEngagementCandidateStatus.Success,
                pose,
                opportunity,
                path,
                authoredPriority,
                capacity,
                reservationCount,
                isLockedZone);
        }

        private static EngagementFixture CreateEngagementFixture(bool addBlockerAfterGraphBuild = false)
        {
            var physics = new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.FromRatio(1, 4),
                staticColliderCount: 1,
                sourceDescription: "test.engagement");
            var farBodyId = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 90);
            physics.World.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 90),
                farBodyId,
                Position(100f, 0f, 100f)));
            physics.World.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                farBodyId,
                new CombatColliderId(1),
                layer: 1,
                localMin: Position(-0.5f, 0f, -0.5f),
                localMax: Position(0.5f, 2f, 0.5f),
                CombatSurfaceMaterial.Default));
            var input = new CharacterTestNavigationSessionBuildInput(
                revision: 11,
                minimumXMillimeters: -4000,
                maximumXMillimeters: 4000,
                minimumZMillimeters: -4000,
                maximumZMillimeters: 4000,
                planeYMillimeters: 0,
                cellSizeMillimeters: 1000,
                connectDiagonals: true,
                maxNodeCount: 128,
                maxEdgeCount: 1024,
                maxEndpointSnapDistanceMillimeters: 1500,
                maxNodeExpansions: 128,
                maxPathNodeCount: 64);
            CharacterTestNavigationSession navigation = CharacterTestNavigationSessionBuilder.Build(
                input,
                physics,
                new CharacterTestNavigationSceneInputResult(CharacterTestNavigationSceneInputStatus.Success));
            Assert.IsTrue(navigation.IsAvailable, navigation.BuildResult.Status.ToString());
            if (addBlockerAfterGraphBuild)
            {
                var bodyId = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 91);
                physics.World.UpsertBody(new CombatPhysicsBody(
                    new CombatEntityId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 91),
                    bodyId,
                    Position(1f, 0f, 1f)));
                physics.World.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                    bodyId,
                    new CombatColliderId(1),
                    layer: 1,
                    localMin: Position(-0.6f, 0f, -0.6f),
                    localMax: Position(0.6f, 2f, 0.6f),
                    CombatSurfaceMaterial.Default));
            }
            RuntimeFrame frame = new RuntimeFrame(12);
            CharacterTestCharacterEntity source = CreateEntity(1, 0f, 0f, 0f);
            CharacterTestCharacterEntity target = CreateEntity(2, 1f, 2f, 90f);
            var geometry = new CharacterTestDamageGeometrySnapshot(2);
            geometry.BeginWrite(2, target.GameplayEntityId,
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 2));
            geometry.AddCollider(
                CharacterTestAuthoredContentIds.D400_Id70,
                "hurt.body",
                new CombatColliderId(1),
                CharacterTestAuthoredContentIds.D400_Id34,
                "part.body",
                CharacterTestAuthoredContentIds.D400_Id55,
                "hit.body",
                0,
                CombatPhysicsShape.Aabb(FixVector3.Zero, FixVector3.Zero),
                FixVector3.Zero, new FixVector3(Fix64.One, Fix64.One, Fix64.One));
            geometry.SortCollidersByStableLogicalId();
            geometry.Commit(frame, physics.World.Revision, 7);
            var zones = new CharacterTestDamageZoneSnapshot();
            zones.Build(geometry, new[]
            {
                new CharacterDamageZoneRuntimeBinding(
                    global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0090, "Approach", new[] { SyntheticHitBodyId },
                    true, true, true, 3)
            });
            var anchor = new CharacterEngagementAnchorRuntimeBinding(
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0007, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0090,
                CharacterEngagementAnchorReferenceFrame.ModelRoot,
                new CharacterRuntimeIntVector3(1000, 0, 0),
                yawMilliDegrees: 90000,
                capacity: 2,
                authoredPriority: 9,
                enabled: true,
                phaseId: 0);
            return new EngagementFixture(
                new CharacterTestEngagementPlanner(), frame, source, target,
                anchor, zones, physics, navigation);
        }

        private static CharacterTestCharacterEntity CreateEntity(
            int id, float x, float z, float yaw)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(id),
                new CharacterTestCharacterIdentityComponent(
                    1, "M3", default(ResourceKey), default(CharacterRuntimeAnimationBinding)),
                new CharacterTestCharacterTeamComponent(id, id),
                new CharacterTestCharacterTransformComponent(x, 0f, z, yaw),
                new CharacterTestCharacterEquipmentComponent(false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                new GameplayEntityId(id, 1),
                CharacterTestCharacterPhysicsComponent.Default);
        }

        private static FixVector3 Position(float x, float y, float z)
        {
            return new FixVector3(Fix(x), Fix(y), Fix(z));
        }

        private static Fix64 Fix(float value) =>
            Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));

        private sealed class EngagementFixture
        {
            internal EngagementFixture(
                CharacterTestEngagementPlanner planner,
                RuntimeFrame frame,
                CharacterTestCharacterEntity source,
                CharacterTestCharacterEntity target,
                CharacterEngagementAnchorRuntimeBinding anchor,
                CharacterTestDamageZoneSnapshot zones,
                CharacterTestCharacterPhysicsRuntime physics,
                CharacterTestNavigationSession navigation)
            {
                Planner = planner;
                Frame = frame;
                Source = source;
                Target = target;
                Anchor = anchor;
                Zones = zones;
                Physics = physics;
                Navigation = navigation;
            }

            internal CharacterTestEngagementPlanner Planner { get; }
            internal RuntimeFrame Frame { get; }
            internal CharacterTestCharacterEntity Source { get; }
            internal CharacterTestCharacterEntity Target { get; }
            internal CharacterEngagementAnchorRuntimeBinding Anchor { get; }
            internal CharacterTestDamageZoneSnapshot Zones { get; }
            internal CharacterTestCharacterPhysicsRuntime Physics { get; }
            internal CharacterTestNavigationSession Navigation { get; }
            internal CharacterTestEngagementCandidateQuery Query => CreateQuery();

            internal CharacterTestEngagementCandidateQuery CreateQuery(int anchorRevision = 9)
            {
                return new CharacterTestEngagementCandidateQuery(
                    Frame, Source, Target, 5, Anchor, anchorRevision, AnchorDigest,
                    Zones, Physics, Navigation, MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id33, 1);
            }
        }

        private static CharacterTestEngagementReservationStateSnapshot CreateReservationSnapshot()
        {
            return new CharacterTestEngagementReservationStateSnapshot
            {
                SourceEntityId = 10,
                TargetEntityId = 200,
                TargetCombatEntityId = 200,
                ResolvedActionRevision = 5,
                ZoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0090,
                AnchorId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0007,
                Capacity = 1,
                CapacitySlot = 0,
                AcquiredFrame = 12,
                UpdatedFrame = 12,
                ZoneBindingRevision = 7,
                ZoneRevision = 8,
                AnchorRevision = 9,
                AnchorContentHash = AnchorDigest,
                PhaseId = CharacterTestAuthoredContentIds.D400_Id33,
                PhaseRevision = 1,
                CombatPhysicsRevision = 10,
                GraphRevision = 11,
                GraphContentHash = 77L,
                ReleaseReason = (int)CharacterTestEngagementReservationReleaseReason.None
            };
        }

        private static long Hash(CharacterTestRuntimeAiPlannerStateSnapshot state)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerState, 2);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(accumulator, state);
            return accumulator.ToHash();
        }
    }
}

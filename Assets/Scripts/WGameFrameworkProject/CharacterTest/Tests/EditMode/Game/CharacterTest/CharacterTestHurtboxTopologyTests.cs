using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestHurtboxTopologyTests
    {
        private const int TailPartStateId = CharacterTestAuthoredContentIds.D400_Id43;
        private const int TailBaseColliderId = CharacterTestAuthoredContentIds.D400_Id147;
        private const int TailMidColliderId = CharacterTestAuthoredContentIds.D400_Id148;
        private const int TailTipColliderId = CharacterTestAuthoredContentIds.D400_Id149;
        private const int LeftForelimbColliderId = CharacterTestAuthoredContentIds.D400_Id133;
        private const int LeftForelimbChannelId = CharacterTestAuthoredContentIds.D400_Id114;
        private static readonly CharacterActionId RightClawActionId = new CharacterActionId(940000);
        private const string RightClawAnimationActionKey = "000003.attack1a";
        private const string RightClawClipKey = "char.000003.animation.attack1a";

        [Test]
        public void Dragon14_AuthorityPose_TransformsActualHurtboxShapeBeforeRoot_AndComposesWithSeveredFilter()
        {
            DragonFixture fixture = CreateDragonFixture();
            fixture.Entity.SetAction(CharacterTestCharacterActionComponent.FromDeterministic(
                RightClawActionId,
                state: MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running,
                phase: MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Active,
                localFrame: 0,
                lastRejectReason: default,
                lastAnimationActionKey: RightClawAnimationActionKey,
                animationActionKeyThisFrame: RightClawAnimationActionKey,
                animationTransitionFrames: 0,
                animationHoldFrames: 0,
                jumpImpulseYMillimetersPerSecond: 0));
            fixture.Entity.SetAnimation(new CharacterTestCharacterAnimationComponent(
                UnityEngine.Vector2.zero,
                locomotionSpeed01: 0f,
                grounded: true,
                airborne: false,
                actionActive: true,
                actionKey: RightClawAnimationActionKey));

            fixture.Hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, new RuntimeFrame(60));
            Assert.IsTrue(fixture.Hurtboxes.TryGetDamageGeometrySnapshot(
                fixture.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot baseline));
            Assert.IsTrue(baseline.TryGetCollider(
                CharacterTestAuthoredContentIds.D400_Id135,
                out CharacterTestDamageGeometryColliderMetadata baselineCollider));
            Assert.AreEqual(CombatPhysicsShapeKind.Capsule, baselineCollider.LocalShape.Kind);

            CharacterAuthorityPoseRigRuntimeBinding rig = fixture.Package.Geometry.AuthorityPoseRig;
            Assert.IsNotNull(rig);
            Assert.IsTrue(CharacterTestDefaultBakedWeaponTraceSource.Instance.TryGetPackageSocketCatalog(
                fixture.Entity.Identity.GeneratedPackageRoot,
                out CharacterSocketBakeCatalog catalog));
            Assert.IsTrue(catalog.TryGetTrack(
                RightClawClipKey,
                LeftForelimbChannelId,
                out CharacterSocketBakeTrack channelTrack));
            Assert.IsTrue(channelTrack.TryGetFrame(0, out MxAnimationBakedSocketFrame channelFrame));
            Assert.IsTrue(channelTrack.TryGetBasis(0, out CharacterSocketBakeBasisFrame channelBasis));

            FixVector3 basePosition = ToFixVector3(channelFrame.Position, channelTrack.QuantizationScale);
            FixVector3 baseRight = Normalize(ToFixVector3(channelBasis.Right, channelTrack.BasisQuantizationScale));
            FixVector3 baseUp = Normalize(ToFixVector3(channelBasis.Up, channelTrack.BasisQuantizationScale));
            FixVector3 baseForward = Normalize(ToFixVector3(channelBasis.Forward, channelTrack.BasisQuantizationScale));
            FixVector3 translation = new FixVector3(Fix64.FromInt(2), Fix64.FromInt(3), Fix64.FromInt(4));

            CharacterTestTerrainIkStateComponent terrain = fixture.Entity.TerrainIkState;
            Assert.IsTrue(terrain.Configure(
                CharacterAuthorityPoseRigDefinition.FromRuntimeBinding(rig),
                baseSourceRevision: 77,
                baseSourceContentHash: "sha256:test-hurtbox-authority-base"));
            terrain.BeginResolvedFrame(runtimeFrame: 72);
            Assert.IsTrue(terrain.ResolvedFrame.TryAdd(new CharacterTestResolvedAuthorityPoseSample(
                FindChannelOrdinal(rig, LeftForelimbChannelId),
                LeftForelimbChannelId,
                RotateQuarterTurn(basePosition) + translation,
                new CharacterTestResolvedAuthorityPoseBasis(
                    RotateQuarterTurn(baseRight),
                    RotateQuarterTurn(baseUp),
                    RotateQuarterTurn(baseForward)),
                valid: true,
                CharacterTestAuthorityPoseSampleSourceMask.Base
                    | CharacterTestAuthorityPoseSampleSourceMask.TerrainIk,
                CharacterTestAuthorityPoseFailure.Success)));

            SeverPart(fixture.Entity, TailPartStateId, breakFrame: 70, severFrame: 71);
            fixture.Hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, new RuntimeFrame(72));
            Assert.IsTrue(fixture.Hurtboxes.TryGetDamageGeometrySnapshot(
                fixture.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot resolved));
            Assert.AreEqual(15, resolved.ColliderCount);
            Assert.IsTrue(resolved.TryGetCollider(
                CharacterTestAuthoredContentIds.D400_Id135,
                out CharacterTestDamageGeometryColliderMetadata resolvedCollider));
            Assert.AreEqual(72L, fixture.Entity.ResolvedAuthorityPoseFrame.RuntimeFrame);
            Assert.AreEqual(terrain.LastPublishedRevision, fixture.Entity.ResolvedAuthorityPoseFrame.Revision);
            TestContext.WriteLine(
                "hurtbox.numeric frame=" + fixture.Entity.ResolvedAuthorityPoseFrame.RuntimeFrame
                + " revision=" + fixture.Entity.ResolvedAuthorityPoseFrame.Revision
                + " baselineA.raw=(" + baselineCollider.LocalShape.PointA.X.RawValue
                + "," + baselineCollider.LocalShape.PointA.Y.RawValue
                + "," + baselineCollider.LocalShape.PointA.Z.RawValue
                + ") resolvedA.raw=(" + resolvedCollider.LocalShape.PointA.X.RawValue
                + "," + resolvedCollider.LocalShape.PointA.Y.RawValue
                + "," + resolvedCollider.LocalShape.PointA.Z.RawValue
                + ") baselineB.raw=(" + baselineCollider.LocalShape.PointB.X.RawValue
                + "," + baselineCollider.LocalShape.PointB.Y.RawValue
                + "," + baselineCollider.LocalShape.PointB.Z.RawValue
                + ") resolvedB.raw=(" + resolvedCollider.LocalShape.PointB.X.RawValue
                + "," + resolvedCollider.LocalShape.PointB.Y.RawValue
                + "," + resolvedCollider.LocalShape.PointB.Z.RawValue + ")");
            Assert.IsTrue(CharacterTestAuthorityPoseConsumerMath.TryResolveAttachmentDelta(
                fixture.Entity,
                rig,
                catalog,
                CharacterAuthorityPoseRigAttachmentKind.Collider,
                LeftForelimbColliderId,
                RightClawClipKey,
                localFrame: 0,
                loop: false,
                new RuntimeFrame(72),
                out CharacterTestAuthorityPoseAttachmentDelta expectedDelta));
            AssertVectorWithinRawTolerance(
                QuantizeMillimeters(expectedDelta.TransformPoint(baselineCollider.LocalShape.PointA)),
                resolvedCollider.LocalShape.PointA,
                tolerance: 16L);
            AssertVectorWithinRawTolerance(
                QuantizeMillimeters(expectedDelta.TransformPoint(baselineCollider.LocalShape.PointB)),
                resolvedCollider.LocalShape.PointB,
                tolerance: 16L);
            Assert.AreEqual(
                baselineCollider.LocalShape.Radius.RawValue,
                resolvedCollider.LocalShape.Radius.RawValue);
            Assert.IsFalse(resolved.TryGetCollider(CharacterTestAuthoredContentIds.D400_Id147, out _));
            Assert.IsFalse(resolved.TryGetCollider(CharacterTestAuthoredContentIds.D400_Id148, out _));
            Assert.IsFalse(resolved.TryGetCollider(CharacterTestAuthoredContentIds.D400_Id149, out _));
        }

        [Test]
        public void Dragon14_SeveredTail_RemovesThreeLeavesPreservesOrdinalsAndStableTopology()
        {
            DragonFixture fixture = CreateDragonFixture();
            CharacterTestCharacterHurtboxSystem hurtboxes = fixture.Hurtboxes;

            hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, new RuntimeFrame(10));
            Assert.IsTrue(hurtboxes.TryGetDamageGeometrySnapshot(
                fixture.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot intact));
            Assert.AreEqual(18, intact.ColliderCount);
            Assert.AreEqual(18, QueryHurtboxes(fixture.Physics.World).Count);
            AssertOriginalBindingOrdinals(fixture.Package.Geometry.BodyColliders, intact, expectTail: true);
            int intactTopologyRevision = intact.TopologyRevision;
            int intactPoseRevision = intact.HurtboxRevision;

            hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, new RuntimeFrame(11));
            Assert.IsTrue(hurtboxes.TryGetDamageGeometrySnapshot(
                fixture.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot stableIntact));
            Assert.AreEqual(intactTopologyRevision, stableIntact.TopologyRevision,
                "A stable pose refit must not advance the topology witness.");
            Assert.Greater(stableIntact.HurtboxRevision, intactPoseRevision,
                "The animated Hurtbox revision remains independent from topology.");

            SeverPart(fixture.Entity, TailPartStateId, breakFrame: 20, severFrame: 21);
            hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, new RuntimeFrame(12));
            Assert.IsTrue(hurtboxes.TryGetDamageGeometrySnapshot(
                fixture.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot severed));
            Assert.AreEqual(CharacterTestDamageGeometryStatus.Available, severed.Status);
            Assert.AreEqual(15, severed.ColliderCount);
            Assert.Greater(severed.TopologyRevision, intactTopologyRevision);
            AssertOriginalBindingOrdinals(fixture.Package.Geometry.BodyColliders, severed, expectTail: false);
            Assert.IsFalse(severed.TryGetCollider(CharacterTestAuthoredContentIds.D400_Id147, out _));
            Assert.IsFalse(severed.TryGetCollider(CharacterTestAuthoredContentIds.D400_Id148, out _));
            Assert.IsFalse(severed.TryGetCollider(CharacterTestAuthoredContentIds.D400_Id149, out _));
            List<CombatQueryResult> postSeverHits = QueryHurtboxes(fixture.Physics.World);
            Assert.AreEqual(15, postSeverHits.Count);
            AssertNoCollider(postSeverHits, FindCombatColliderId(fixture.Package.Geometry.BodyColliders, TailBaseColliderId));
            AssertNoCollider(postSeverHits, FindCombatColliderId(fixture.Package.Geometry.BodyColliders, TailMidColliderId));
            AssertNoCollider(postSeverHits, FindCombatColliderId(fixture.Package.Geometry.BodyColliders, TailTipColliderId));

            int severedTopologyRevision = severed.TopologyRevision;
            hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, new RuntimeFrame(13));
            Assert.IsTrue(hurtboxes.TryGetDamageGeometrySnapshot(
                fixture.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot stableSevered));
            Assert.AreEqual(severedTopologyRevision, stableSevered.TopologyRevision);
            AssertOriginalBindingOrdinals(fixture.Package.Geometry.BodyColliders, stableSevered, expectTail: false);

            // Rebuild from the stable binding plus current authority state, not from
            // the previous compound's mutation history.
            hurtboxes.Clear(fixture.Physics);
            hurtboxes = new CharacterTestCharacterHurtboxSystem();
            hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, new RuntimeFrame(14));
            Assert.IsTrue(hurtboxes.TryGetDamageGeometrySnapshot(
                fixture.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot rebuilt));
            Assert.AreEqual(15, rebuilt.ColliderCount);
            AssertOriginalBindingOrdinals(fixture.Package.Geometry.BodyColliders, rebuilt, expectTail: false);
        }

        [Test]
        public void AllExplicitLeavesInactive_IsTypedUnavailableWithoutCompatibilityFallback_AndReinitializeRestoresIds()
        {
            DragonFixture fixture = CreateDragonFixture();
            fixture.Hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, new RuntimeFrame(30));
            Assert.IsTrue(fixture.Hurtboxes.TryGetDamageGeometrySnapshot(
                fixture.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot intact));
            var originalIds = new Dictionary<int, int>();
            for (int i = 0; i < intact.ColliderCount; i++)
            {
                CharacterTestDamageGeometryColliderMetadata collider = intact.GetCollider(i);
                originalIds.Add(collider.LogicalColliderId, collider.CombatColliderId.Value);
            }

            CharacterImportedGeometryBinding allTracked = CreateAllTrackedBinding(fixture.Package.Geometry);
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                fixture.Entity.ReinitializeBodyPartState(allTracked));
            SeverPart(fixture.Entity, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0059, breakFrame: 31, severFrame: 32);
            fixture.Hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, new RuntimeFrame(33));

            Assert.IsFalse(fixture.Hurtboxes.TryGetDamageGeometrySnapshot(fixture.Entity.Id.Value, out _));
            Assert.IsTrue(fixture.Hurtboxes.TryGetDamageGeometrySnapshotState(
                fixture.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot unavailable));
            Assert.AreEqual(CharacterTestDamageGeometryStatus.ExplicitLeavesInactive, unavailable.Status);
            Assert.IsTrue(unavailable.IsCommitted);
            Assert.IsFalse(unavailable.IsValid);
            Assert.AreEqual(0, unavailable.ColliderCount);
            CombatBodyId bodyId = fixture.Physics.GetHurtboxBodyId(fixture.Entity);
            Assert.IsTrue(fixture.Physics.World.TryGetBody(bodyId, out _),
                "The explicit hurtbox body remains as an empty compound authority.");
            Assert.AreEqual(0, fixture.Physics.World.CompoundLeafCount);
            Assert.IsFalse(fixture.Physics.World.TryGetAabbCollider(
                bodyId,
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart),
                out _),
                "Explicit leaves becoming inactive must never create the compatibility AABB.");
            Assert.AreEqual(0, QueryHurtboxes(fixture.Physics.World).Count);

            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                fixture.Entity.ReinitializeBodyPartState(fixture.Package.Geometry));
            fixture.Hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, new RuntimeFrame(34));
            Assert.IsTrue(fixture.Hurtboxes.TryGetDamageGeometrySnapshot(
                fixture.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot restored));
            Assert.AreEqual(18, restored.ColliderCount);
            for (int i = 0; i < restored.ColliderCount; i++)
            {
                CharacterTestDamageGeometryColliderMetadata collider = restored.GetCollider(i);
                Assert.AreEqual(originalIds[collider.LogicalColliderId], collider.CombatColliderId.Value,
                    collider.LogicalColliderId);
            }
        }

        [Test]
        public void InvalidBodyPartBinding_FailsClosedAndRemovesHurtboxBody()
        {
            DragonFixture fixture = CreateDragonFixture();
            fixture.Hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, new RuntimeFrame(40));
            CombatBodyId bodyId = fixture.Physics.GetHurtboxBodyId(fixture.Entity);
            Assert.IsTrue(fixture.Physics.World.TryGetBody(bodyId, out _));

            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.InvalidBinding,
                fixture.Entity.ReinitializeBodyPartState(CreateInvalidBinding(fixture.Package.Geometry)));
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Error,
                "CHARACTER_HURTBOX_BODY_PART_BINDING_INVALID package=Assets/MxFrameworkGenerated/CharacterPackages/000003 entity=1");
            fixture.Hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, new RuntimeFrame(41));

            Assert.IsFalse(fixture.Physics.World.TryGetBody(bodyId, out _));
            Assert.IsFalse(fixture.Hurtboxes.TryGetDamageGeometrySnapshot(fixture.Entity.Id.Value, out _));
            Assert.IsTrue(fixture.Hurtboxes.TryGetDamageGeometrySnapshotState(
                fixture.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot invalid));
            Assert.AreEqual(CharacterTestDamageGeometryStatus.InvalidBodyPartBinding, invalid.Status);
            Assert.AreEqual(0, QueryHurtboxes(fixture.Physics.World).Count);
        }

        [Test]
        public void CompoundActiveWriteSet_SingleRemovalAndOrderPerturbationKeepStableIdentity()
        {
            CombatPhysicsWorld world = CreateCompoundWorld(out CombatBodyId bodyId);
            UpsertCompound(world, bodyId, 1, 2, 3);
            int initialTopologyRevision = world.TopologyRevision;

            UpsertCompound(world, bodyId, 3, 1);
            Assert.Greater(world.TopologyRevision, initialTopologyRevision);
            CollectionAssert.AreEqual(new[] { 1, 3 }, QueryColliderValues(world));

            int filteredTopologyRevision = world.TopologyRevision;
            UpsertCompound(world, bodyId, 3, 1);
            Assert.AreEqual(filteredTopologyRevision, world.TopologyRevision,
                "Repeating the same active set and order is a refit, not a topology rebuild.");

            CombatPhysicsWorld canonical = CreateCompoundWorld(out CombatBodyId canonicalBodyId);
            UpsertCompound(canonical, canonicalBodyId, 1, 3);
            CollectionAssert.AreEqual(QueryColliderValues(canonical), QueryColliderValues(world),
                "Query identity is stable even when the active write order is perturbed.");
        }

        [Test]
        public void SeveredTail_StableWarmedSync_AllocatesZeroBytes()
        {
            DragonFixture fixture = CreateDragonFixture();
            SeverPart(fixture.Entity, TailPartStateId, breakFrame: 50, severFrame: 51);
            for (int frame = 0; frame < 64; frame++)
                fixture.Hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, new RuntimeFrame(100 + frame));

            bool stable = true;
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 512; frame++)
            {
                fixture.Hurtboxes.SyncAll(
                    fixture.World.Entities,
                    fixture.Physics,
                    new RuntimeFrame(1000 + frame));
                stable &= fixture.Hurtboxes.TryGetDamageGeometrySnapshot(
                    fixture.Entity.Id.Value,
                    out CharacterTestDamageGeometrySnapshot snapshot)
                    && snapshot.ColliderCount == 15;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(stable);
            Assert.AreEqual(0L, allocated);
        }

        private static DragonFixture CreateDragonFixture()
        {
            CharacterTestCharacterDefinition definition = FindDragonDefinition();
            CharacterImportedPackage package =
                CharacterTestGeneratedPackageSource.LoadPackage(definition.GeneratedPackageRoot);
            Assert.IsNotNull(package);
            Assert.AreEqual(18, package.Geometry.BodyColliders.Length);

            var runtimeWorld = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = runtimeWorld.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                entity.BodyPartState.InitializationStatus);

            return new DragonFixture(
                runtimeWorld,
                entity,
                new CharacterTestCharacterPhysicsRuntime(
                    new CombatPhysicsWorld(),
                    CombatPhysicsLayerMask.FromLayer(2),
                    new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                    Fix64.FromRatio(1, 100),
                    Fix64.Half,
                    staticColliderCount: 0,
                    sourceDescription: "lcc-m3-t2.hurtbox-topology"),
                new CharacterTestCharacterHurtboxSystem(),
                package);
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
            Assert.IsTrue(state.TryResolveDefinitionIndexByPartStateId(partStateId, out int definitionIndex));
            Assert.IsTrue(state.TryGetDefinition(definitionIndex, out CharacterTestBodyPartStateDefinition definition));
            Assert.IsTrue(definition.Severable);

            Assert.IsTrue(state.TryBeginFrame(breakFrame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageAcceptedDamage(new CharacterTestBodyPartAcceptedDamageFact(
                    authorityFrame: breakFrame,
                    sourceEntityId: 9001,
                    targetEntityId: entity.Id.Value,
                    actionInstanceId: 1,
                    policyEventId: 1,
                    traceId: 1,
                    localFrame: 1,
                    definitionIndex: definitionIndex,
                    finalDamage: int.MaxValue)));
            Assert.IsTrue(state.CommitFrame().Succeeded);
            Assert.IsTrue(state.TryGetCurrentEntry(definitionIndex, out CharacterTestBodyPartStateEntry broken));
            Assert.AreEqual(CharacterTestBodyPartStructureState.Broken, broken.StructureState);

            Assert.IsTrue(state.TryBeginFrame(severFrame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageSever(new CharacterTestBodyPartSeverFact(
                    authorityFrame: severFrame,
                    sourceEntityId: 9001,
                    targetEntityId: entity.Id.Value,
                    definitionIndex: definitionIndex,
                    severTag: definition.SeverRequiredTag,
                    correlationId: severFrame)));
            Assert.IsTrue(state.CommitFrame().Succeeded);
            Assert.IsTrue(state.TryGetCurrentEntry(definitionIndex, out CharacterTestBodyPartStateEntry severed));
            Assert.AreEqual(CharacterTestBodyPartStructureState.Severed, severed.StructureState);
        }

        private static void AssertOriginalBindingOrdinals(
            CharacterBodyColliderRuntimeBinding[] bindings,
            CharacterTestDamageGeometrySnapshot snapshot,
            bool expectTail)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                CharacterBodyColliderRuntimeBinding binding = bindings[i];
                bool tail = IsTailCollider(binding.ColliderId);
                bool present = snapshot.TryGetCollider(binding.ColliderId, out CharacterTestDamageGeometryColliderMetadata metadata);
                Assert.AreEqual(expectTail || !tail, present, "Collider {0}", binding.ColliderId);
                if (present)
                {
                    Assert.AreEqual(
                        CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart + i,
                        metadata.CombatColliderId.Value,
                        binding.ColliderId + " must retain its original binding ordinal.");
                }
            }
        }

        private static bool IsTailCollider(int colliderId)
        {
            return colliderId == TailBaseColliderId
                || colliderId == TailMidColliderId
                || colliderId == TailTipColliderId;
        }

        private static CombatColliderId FindCombatColliderId(
            CharacterBodyColliderRuntimeBinding[] bindings,
            int logicalColliderId)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].ColliderId == logicalColliderId)
                {
                    return new CombatColliderId(
                        CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart + i);
                }
            }
            Assert.Fail("Collider is missing from the formal binding: " + logicalColliderId);
            return default;
        }

        private static List<CombatQueryResult> QueryHurtboxes(CombatPhysicsWorld world)
        {
            var results = new List<CombatQueryResult>(32);
            world.QuerySphere(
                new CombatSphereQuery(
                    new CombatQueryHeader(
                        queryId: 1,
                        kind: CombatQueryKind.Sphere,
                        sourceEntityId: new CombatEntityId(999999),
                        traceId: 1,
                        actionId: 1,
                        sourceOrder: 0,
                        layerMask: CombatPhysicsLayerMask.FromLayer(
                            CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer)),
                    FixVector3.Zero,
                    Fix64.FromInt(100)),
                results);
            return results;
        }

        private static void AssertNoCollider(
            List<CombatQueryResult> results,
            CombatColliderId colliderId)
        {
            for (int i = 0; i < results.Count; i++)
                Assert.AreNotEqual(colliderId, results[i].TargetColliderId);
        }

        private static int FindChannelOrdinal(
            CharacterAuthorityPoseRigRuntimeBinding rig,
            int channelId)
        {
            for (int i = 0; i < rig.Channels.Length; i++)
            {
                if (rig.Channels[i].ChannelId == channelId)
                    return rig.Channels[i].Ordinal;
            }
            Assert.Fail("Authority channel is missing: " + channelId);
            return -1;
        }

        private static FixVector3 ToFixVector3(MxAnimationBakedVector3 value, int scale)
        {
            Assert.Greater(scale, 0);
            return new FixVector3(
                Fix64.FromRatio(value.X, scale),
                Fix64.FromRatio(value.Y, scale),
                Fix64.FromRatio(value.Z, scale));
        }

        private static FixVector3 Normalize(FixVector3 value)
        {
            Assert.IsTrue(value.TryNormalize(out FixVector3 normalized));
            return normalized;
        }

        private static FixVector3 RotateQuarterTurn(FixVector3 value)
        {
            return new FixVector3(value.Z, value.Y, -value.X);
        }

        private static FixVector3 QuantizeMillimeters(FixVector3 value)
        {
            return new FixVector3(
                QuantizeMillimeters(value.X),
                QuantizeMillimeters(value.Y),
                QuantizeMillimeters(value.Z));
        }

        private static Fix64 QuantizeMillimeters(Fix64 value)
        {
            long millimeters = value.RawValue * 1000L / Fix64.Scale;
            return Fix64.FromRaw(millimeters * Fix64.Scale / 1000L);
        }

        private static void AssertVector(FixVector3 expected, FixVector3 actual)
        {
            Assert.AreEqual(expected.X.RawValue, actual.X.RawValue, "x");
            Assert.AreEqual(expected.Y.RawValue, actual.Y.RawValue, "y");
            Assert.AreEqual(expected.Z.RawValue, actual.Z.RawValue, "z");
        }

        private static void AssertVectorWithinRawTolerance(
            FixVector3 expected,
            FixVector3 actual,
            long tolerance)
        {
            Assert.That(actual.X.RawValue, Is.EqualTo(expected.X.RawValue).Within(tolerance), "x");
            Assert.That(actual.Y.RawValue, Is.EqualTo(expected.Y.RawValue).Within(tolerance), "y");
            Assert.That(actual.Z.RawValue, Is.EqualTo(expected.Z.RawValue).Within(tolerance), "z");
        }

        private static CharacterImportedGeometryBinding CreateAllTrackedBinding(
            CharacterImportedGeometryBinding source)
        {
            var parts = new SortedSet<int>();
            for (int i = 0; i < source.BodyColliders.Length; i++)
                parts.Add(source.BodyColliders[i].PartId);
            int[] memberPartIds = new int[parts.Count];
            parts.CopyTo(memberPartIds);
            var lookup = new CharacterBodyPartDamageLookupRuntimeBinding[memberPartIds.Length];
            for (int i = 0; i < memberPartIds.Length; i++)
                lookup[i] = new CharacterBodyPartDamageLookupRuntimeBinding(memberPartIds[i], 0);

            var definition = new CharacterBodyPartDamageRuntimeBinding(
                definitionIndex: 0,
                partStateId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0059,
                memberPartIds: memberPartIds,
                damageZoneId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0092,
                woundCapacity: 0,
                openDamageScaleMilli: 0,
                focusConsumeTag: string.Empty,
                maxIntegrity: 1,
                integrityDamageScaleMilli: 1000,
                breakReactionId: "reaction.test.all.break",
                authoredPriorityDeltaOpen: 0,
                authoredPriorityDeltaBroken: 0,
                severable: true,
                severRequiredTag: "action.test.all.sever",
                presentationEventId: "event.test.all.severed");
            return CloneBodyPartBinding(
                source,
                new[] { definition },
                lookup,
                CharacterBodyPartDamageMode.Authored);
        }

        private static CharacterImportedGeometryBinding CreateInvalidBinding(
            CharacterImportedGeometryBinding source)
        {
            return CloneBodyPartBinding(
                source,
                Array.Empty<CharacterBodyPartDamageRuntimeBinding>(),
                Array.Empty<CharacterBodyPartDamageLookupRuntimeBinding>(),
                CharacterBodyPartDamageMode.Authored);
        }

        private static CharacterImportedGeometryBinding CloneBodyPartBinding(
            CharacterImportedGeometryBinding source,
            CharacterBodyPartDamageRuntimeBinding[] definitions,
            CharacterBodyPartDamageLookupRuntimeBinding[] lookup,
            CharacterBodyPartDamageMode mode)
        {
            return new CharacterImportedGeometryBinding(
                source.Format,
                source.SchemaVersion,
                source.PackageId,
                source.BodyProfile,
                source.BodyColliders,
                source.HitZoneBindings,
                source.DamageZones,
                source.Sockets,
                source.WeaponAttachments,
                source.WeaponTraces,
                source.EngagementAnchors,
                source.EngagementAnchorRevision,
                source.EngagementAnchorContentHash,
                source.EngagementAnchorMode,
                source.BodyPresenceVolumes,
                source.BodyPresenceRevision,
                source.BodyPresenceContentHash,
                source.BodyPresenceMode,
                definitions,
                lookup,
                "mx.characterBodyPartDamage.v1",
                "1.0",
                bodyPartDamageRevision: 1,
                bodyPartDamageContentHash: "sha256:test-hurtbox-topology",
                bodyPartDamageMode: mode);
        }

        private static CombatPhysicsWorld CreateCompoundWorld(out CombatBodyId bodyId)
        {
            var world = new CombatPhysicsWorld();
            bodyId = new CombatBodyId(7001);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(7001),
                bodyId,
                FixVector3.Zero));
            return world;
        }

        private static void UpsertCompound(
            CombatPhysicsWorld world,
            CombatBodyId bodyId,
            params int[] colliderIds)
        {
            world.BeginCompoundColliderUpdate(bodyId, expectedLeafCount: 3);
            for (int i = 0; i < colliderIds.Length; i++)
            {
                int id = colliderIds[i];
                world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(
                    new CombatColliderId(id),
                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer,
                    CombatPhysicsShape.Sphere(
                        new FixVector3(Fix64.FromInt(id * 2), Fix64.Zero, Fix64.Zero),
                        Fix64.Half)));
            }
            world.EndCompoundColliderUpdate();
        }

        private static int[] QueryColliderValues(CombatPhysicsWorld world)
        {
            List<CombatQueryResult> results = QueryHurtboxes(world);
            var values = new int[results.Count];
            for (int i = 0; i < results.Count; i++)
                values[i] = results[i].TargetColliderId.Value;
            Array.Sort(values);
            return values;
        }

        private sealed class DragonFixture
        {
            public DragonFixture(
                CharacterTestCharacterRuntimeWorld world,
                CharacterTestCharacterEntity entity,
                CharacterTestCharacterPhysicsRuntime physics,
                CharacterTestCharacterHurtboxSystem hurtboxes,
                CharacterImportedPackage package)
            {
                World = world;
                Entity = entity;
                Physics = physics;
                Hurtboxes = hurtboxes;
                Package = package;
            }

            public CharacterTestCharacterRuntimeWorld World { get; }
            public CharacterTestCharacterEntity Entity { get; }
            public CharacterTestCharacterPhysicsRuntime Physics { get; }
            public CharacterTestCharacterHurtboxSystem Hurtboxes { get; }
            public CharacterImportedPackage Package { get; }
        }
    }
}

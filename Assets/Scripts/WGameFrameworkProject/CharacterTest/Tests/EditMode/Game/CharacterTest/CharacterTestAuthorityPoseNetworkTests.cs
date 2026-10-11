using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestAuthorityPoseNetworkTests
    {
        [Test]
        public void AuthorityPoseHashPreservesNullEntityBoundary()
        {
            const int TestSchemaId = 91030001;
            var empty = new RuntimeHashAccumulator(TestSchemaId, 1);
            var withNull = new RuntimeHashAccumulator(TestSchemaId, 1);
            CharacterTestNetworkAuthorityPoseSnapshotCodec.AddStateHash(
                empty, Array.Empty<CharacterTestCharacterEntity>());
            CharacterTestNetworkAuthorityPoseSnapshotCodec.AddStateHash(
                withNull, new CharacterTestCharacterEntity[] { null });
            long first = withNull.ToHash();
            Assert.AreNotEqual(empty.ToHash(), first);
            Assert.AreEqual(first, withNull.ToHash());
            withNull.Reset();
            CharacterTestNetworkAuthorityPoseSnapshotCodec.AddStateHash(
                withNull, new CharacterTestCharacterEntity[] { null, null });
            Assert.AreNotEqual(first, withNull.ToHash());
        }

        [Test]
        public void Schema33_CaptureCloneHashesAndOriginTargetRoundTripFutureStateOnly()
        {
            CharacterTestCharacterDefinition definition = CreateCharacterDefinition();
            CharacterTestCharacterRuntimeModule source = CreateRuntime(definition);
            CharacterTestCharacterRuntimeModule replica = CreateRuntime(definition);
            try
            {
                CharacterTestCharacterEntity sourceEntity = Spawn(source, definition);
                CharacterTestCharacterEntity replicaEntity = Spawn(replica, definition);
                ConfigureAuthority(sourceEntity);
                ConfigureAuthority(replicaEntity);
                var sourceParticipant = new CharacterTestNetworkSnapshotParticipant(source, null);
                var replicaParticipant = new CharacterTestNetworkSnapshotParticipant(replica, null);
                var frame = new RuntimeFrame(10);
                long baselineFull = sourceParticipant.ComputeStateHash(frame);
                long baselineOwned = sourceParticipant.ComputeOwnedStateHash(frame, sourceEntity.Id.Value);
                long baselineNonOwned = sourceParticipant.ComputeNonOwnedStateHash(frame, sourceEntity.Id.Value);

                SetOriginHistory(sourceEntity, frame.Value);

                Assert.AreNotEqual(baselineFull, sourceParticipant.ComputeStateHash(frame));
                Assert.AreNotEqual(
                    baselineOwned,
                    sourceParticipant.ComputeOwnedStateHash(frame, sourceEntity.Id.Value));
                Assert.AreEqual(
                    baselineNonOwned,
                    sourceParticipant.ComputeNonOwnedStateHash(frame, sourceEntity.Id.Value));

                RuntimeCustomState wire = sourceParticipant.CaptureSnapshot(frame);
                Assert.AreEqual(32, wire.SchemaVersion);
                JObject json = JObject.Parse(wire.PayloadJson);
                JToken limb = json["authorityPoseTerrainStates"]?[0]?["limbs"]?[0];
                Assert.NotNull(limb);
                Assert.AreEqual(0L, (long)limb["lastValidTargetXRaw"]);
                Assert.AreEqual(frame.Value, (long)limb["lastValidFrame"]);
                Assert.NotNull(limb["currentWeightRaw"]);
                Assert.IsNull(limb["currentValid"]);
                Assert.IsNull(limb["currentFailure"]);
                Assert.IsNull(limb["targetWeight"]);
                Assert.IsNull(limb["releaseProgress"]);

                CharacterTestNetworkSnapshotDocument canonical =
                    sourceParticipant.CaptureRollbackDocument(frame);
                CharacterTestNetworkSnapshotDocument clone = canonical.CloneForRollback();
                Assert.AreNotSame(
                    canonical.AuthorityPoseTerrainStates,
                    clone.AuthorityPoseTerrainStates);
                Assert.AreNotSame(
                    canonical.AuthorityPoseTerrainStates[0].Limbs,
                    clone.AuthorityPoseTerrainStates[0].Limbs);

                NetworkSnapshotApplyResult apply = replicaParticipant.ApplySnapshot(frame, wire);

                Assert.IsTrue(apply.Success, apply.Message);
                CharacterTestTerrainIkLimbState restored = replicaEntity.TerrainIkState.GetLimb(0);
                Assert.AreEqual(frame.Value, restored.LastValidFrame);
                Assert.AreEqual(FixVector3.Zero, restored.LastValidTargetWorld);
                Assert.AreEqual(AxisY(), restored.LastValidNormalWorld);
                Assert.IsTrue(restored.HasRetainedTarget);
                Assert.AreEqual(Fix64.Half.RawValue, restored.CurrentWeight.RawValue);
                Assert.AreEqual(8101, restored.SurfaceEntityId);
                Assert.IsFalse(replicaEntity.ResolvedAuthorityPoseFrame.IsPublished,
                    "derived mid/tip frame must be cleared and recomputed after restore");
                Assert.AreEqual(
                    sourceParticipant.ComputeStateHash(frame),
                    replicaParticipant.ComputeStateHash(frame));
            }
            finally
            {
                source.Dispose();
                replica.Dispose();
            }
        }

        [Test]
        public void Schema33_SourceMismatchFutureAndLegacyMigrationFailOrResetNarrowly()
        {
            CharacterTestCharacterDefinition definition = CreateCharacterDefinition();
            CharacterTestCharacterRuntimeModule runtime = CreateRuntime(definition);
            try
            {
                CharacterTestCharacterEntity entity = Spawn(runtime, definition);
                ConfigureAuthority(entity);
                SetOriginHistory(entity, frame: 20);
                var participant = new CharacterTestNetworkSnapshotParticipant(runtime, null);
                var frame = new RuntimeFrame(20);
                CharacterTestNetworkSnapshotDocument canonical =
                    participant.CaptureRollbackDocument(frame);
                long beforeHash = participant.ComputeStateHash(frame);

                CharacterTestNetworkSnapshotDocument mismatch = canonical.CloneForRollback();
                mismatch.AuthorityPoseTerrainStates[0].BaseSourceContentHash =
                    "sha256:drifted-base";
                NetworkSnapshotApplyResult mismatchApply = participant.ApplyRollbackDocument(
                    frame,
                    mismatch,
                    preserveLocalPredictedEntity: false);
                Assert.IsFalse(mismatchApply.Success);
                StringAssert.Contains("source identity mismatch", mismatchApply.Message);
                Assert.AreEqual(beforeHash, participant.ComputeStateHash(frame));
                Assert.AreEqual(20L, entity.TerrainIkState.GetLimb(0).LastValidFrame);

                RuntimeCustomState validWire = participant.CaptureSnapshot(frame);
                var future = new RuntimeCustomState(validWire.NumericTypeId, validWire.TypeId,
                    CharacterTestNetworkSnapshotParticipant.SchemaVersionValue + 1,
                    validWire.PayloadJson);
                NetworkSnapshotApplyResult futureApply = participant.ApplySnapshot(frame, future);
                Assert.IsFalse(futureApply.Success);
                StringAssert.Contains("schema mismatch", futureApply.Message);
                Assert.AreEqual(beforeHash, participant.ComputeStateHash(frame));

                CharacterTestNetworkSnapshotDocument legacy27 = canonical.CloneForRollback();
                legacy27.SchemaVersion =
                    CharacterTestNetworkSnapshotParticipant.CasterBodyPreviousCenterSchemaVersionValue;
                legacy27.NormalizeForSchema(legacy27.SchemaVersion);
                Assert.IsEmpty(legacy27.AuthorityPoseTerrainStates);
                NetworkSnapshotApplyResult legacy27Apply = participant.ApplyRollbackDocument(
                    frame,
                    legacy27,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(legacy27Apply.Success, legacy27Apply.Message);
                Assert.AreEqual(-1L, entity.TerrainIkState.GetLimb(0).LastValidFrame);

                ConfigureAuthority(entity);
                SetOriginHistory(entity, frame.Value);
                CharacterTestNetworkSnapshotDocument legacy = canonical.CloneForRollback();
                legacy.SchemaVersion =
                    CharacterTestNetworkSnapshotParticipant.SeveredBodyPartStateSchemaVersionValue;
                legacy.NormalizeForSchema(legacy.SchemaVersion);
                Assert.IsEmpty(legacy.AuthorityPoseTerrainStates);
                NetworkSnapshotApplyResult legacyApply = participant.ApplyRollbackDocument(
                    frame,
                    legacy,
                    preserveLocalPredictedEntity: false);

                Assert.IsTrue(legacyApply.Success, legacyApply.Message);
                CharacterTestTerrainIkLimbState reset = entity.TerrainIkState.GetLimb(0);
                Assert.AreEqual(-1L, reset.LastValidFrame);
                Assert.AreEqual(0, reset.ConsecutiveMissCount);
                Assert.AreEqual(Fix64.Zero.RawValue, reset.CurrentWeight.RawValue);
                Assert.AreEqual(0L, entity.TerrainIkState.LastPublishedRevision);
                Assert.IsFalse(entity.ResolvedAuthorityPoseFrame.IsPublished);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void Schema33_RollbackPlusOneRecomputesIdenticalPoseHistoryAndHash()
        {
            CharacterTestCharacterDefinition definition = CreateCharacterDefinition();
            CharacterTestCharacterRuntimeModule source = CreateRuntime(definition);
            CharacterTestCharacterRuntimeModule rollback = CreateRuntime(definition);
            try
            {
                CharacterTestCharacterEntity sourceEntity = Spawn(source, definition);
                CharacterTestCharacterEntity rollbackEntity = Spawn(rollback, definition);
                CharacterAuthorityPoseRigRuntimeBinding rig =
                    CharacterTestAuthorityPoseTerrainTests.CreateRig(
                        limbCount: 1,
                        enterFrames: 1,
                        exitFrames: 2,
                        lostFrames: 1);
                CharacterTestAuthorityPoseTerrainTests.TerrainFixture sourceFixture =
                    CharacterTestAuthorityPoseTerrainTests.ConfigureFixture(
                        source.World,
                        actionRuntime: null,
                        rig,
                        existingEntity: sourceEntity);
                CharacterTestAuthorityPoseTerrainTests.TerrainFixture rollbackFixture =
                    CharacterTestAuthorityPoseTerrainTests.ConfigureFixture(
                        rollback.World,
                        actionRuntime: null,
                        rig,
                        existingEntity: rollbackEntity);
                CombatPhysicsWorld ground =
                    CharacterTestAuthorityPoseTerrainTests.CreateGroundWorld();
                Assert.IsTrue(sourceFixture.System.TickForTests(
                    sourceEntity,
                    ground,
                    new RuntimeFrame(1),
                    CharacterTestAuthorityPoseTerrainTests.ClipKey,
                    0,
                    true));
                var sourceParticipant = new CharacterTestNetworkSnapshotParticipant(source, null);
                var rollbackParticipant = new CharacterTestNetworkSnapshotParticipant(rollback, null);
                CharacterTestNetworkSnapshotDocument checkpoint =
                    sourceParticipant.CaptureRollbackDocument(new RuntimeFrame(1));

                NetworkSnapshotApplyResult apply = rollbackParticipant.ApplyRollbackDocument(
                    new RuntimeFrame(1),
                    checkpoint,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(apply.Success, apply.Message);
                var noGround = new CombatPhysicsWorld();
                Assert.IsTrue(sourceFixture.System.TickForTests(
                    sourceEntity,
                    noGround,
                    new RuntimeFrame(2),
                    CharacterTestAuthorityPoseTerrainTests.ClipKey,
                    0,
                    true));
                Assert.IsTrue(rollbackFixture.System.TickForTests(
                    rollbackEntity,
                    noGround,
                    new RuntimeFrame(2),
                    CharacterTestAuthorityPoseTerrainTests.ClipKey,
                    0,
                    true));

                AssertLimbEqual(
                    sourceEntity.TerrainIkState.GetLimb(0),
                    rollbackEntity.TerrainIkState.GetLimb(0));
                AssertResolvedEqual(
                    sourceEntity.ResolvedAuthorityPoseFrame,
                    rollbackEntity.ResolvedAuthorityPoseFrame);
                Assert.AreEqual(
                    sourceParticipant.ComputeStateHash(new RuntimeFrame(2)),
                    rollbackParticipant.ComputeStateHash(new RuntimeFrame(2)));
            }
            finally
            {
                source.Dispose();
                rollback.Dispose();
            }
        }

        [Test]
        public void Schema33_ConfiguredDragonFourLimbLateJoinRestoresSourceWitnesses()
        {
            CharacterTestCharacterRuntimeModule source = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterRuntimeModule lateJoin = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            try
            {
                CharacterTestCharacterDefinition dragon = FindDragonDefinition();
                CharacterTestCharacterEntity sourceEntity = Spawn(source, dragon);
                Assert.IsTrue(sourceEntity.TerrainIkState.IsConfigured);
                Assert.AreEqual(4, sourceEntity.TerrainIkState.LimbCount);
                var sourceParticipant = new CharacterTestNetworkSnapshotParticipant(source, null);
                var lateJoinParticipant = new CharacterTestNetworkSnapshotParticipant(lateJoin, null);
                var frame = RuntimeFrame.Zero;

                NetworkSnapshotApplyResult apply = lateJoinParticipant.ApplySnapshot(
                    frame,
                    sourceParticipant.CaptureSnapshot(frame));

                Assert.IsTrue(apply.Success, apply.Message);
                Assert.AreEqual(1, lateJoin.Entities.Count);
                CharacterTestCharacterEntity restored = lateJoin.Entities[0];
                Assert.IsTrue(restored.TerrainIkState.IsConfigured);
                Assert.AreEqual(4, restored.TerrainIkState.LimbCount);
                Assert.AreEqual(
                    sourceEntity.TerrainIkState.DefinitionRevision,
                    restored.TerrainIkState.DefinitionRevision);
                Assert.AreEqual(
                    sourceEntity.TerrainIkState.DefinitionContentHash,
                    restored.TerrainIkState.DefinitionContentHash);
                Assert.AreEqual(
                    sourceEntity.TerrainIkState.BaseSourceRevision,
                    restored.TerrainIkState.BaseSourceRevision);
                Assert.AreEqual(
                    sourceEntity.TerrainIkState.BaseSourceContentHash,
                    restored.TerrainIkState.BaseSourceContentHash);
                Assert.IsFalse(restored.ResolvedAuthorityPoseFrame.IsPublished);
                Assert.AreEqual(
                    sourceParticipant.ComputeStateHash(frame),
                    lateJoinParticipant.ComputeStateHash(frame));
            }
            finally
            {
                source.Dispose();
                lateJoin.Dispose();
            }
        }

        [Test]
        public void AuthorityPoseResourceIdentity_V7AuthoredRigPassesAndChangesCatalogHash()
        {
            CharacterTestCharacterDefinition dragon = FindDragonDefinition();
            CharacterImportedPackage package =
                CharacterTestGeneratedPackageSource.LoadPackage(dragon.GeneratedPackageRoot);
            Assert.IsNotNull(package);
            Assert.AreEqual("mx.characterGeometryBinding.v7", package.Geometry.Format);
            Assert.AreEqual("7.0", package.Geometry.SchemaVersion);
            Assert.IsTrue(package.Geometry.AuthorityPoseRig.IsAuthored);

            string baseline = CharacterTestNetworkSessionFactory
                .ComputePlayableCharacterResourceCatalogHash(
                    new[] { dragon },
                    _ => package);
            CharacterAuthorityPoseRigRuntimeBinding rig = package.Geometry.AuthorityPoseRig;
            CharacterImportedGeometryBinding revisionChanged = CloneGeometryWithAuthorityPoseRig(
                package.Geometry,
                CloneAuthorityPoseRig(rig, rig.Revision + 1, rig.ContentHash));
            CharacterImportedGeometryBinding contentChanged = CloneGeometryWithAuthorityPoseRig(
                package.Geometry,
                CloneAuthorityPoseRig(rig, rig.Revision, ChangeSha256(rig.ContentHash)));

            string revisionIdentity = CharacterTestNetworkSessionFactory
                .ComputePlayableCharacterResourceCatalogHash(
                    new[] { dragon },
                    _ => ClonePackage(package, revisionChanged));
            string contentIdentity = CharacterTestNetworkSessionFactory
                .ComputePlayableCharacterResourceCatalogHash(
                    new[] { dragon },
                    _ => ClonePackage(package, contentChanged));

            StringAssert.StartsWith(
                "mx.character-test.playable-character-catalog.v1:sha256:",
                baseline);
            Assert.AreNotEqual(baseline, revisionIdentity);
            Assert.AreNotEqual(baseline, contentIdentity);
        }

        [Test]
        public void AuthorityPoseResourceIdentity_V7InvalidRigFailsClosed()
        {
            CharacterTestCharacterDefinition dragon = FindDragonDefinition();
            CharacterImportedPackage package =
                CharacterTestGeneratedPackageSource.LoadPackage(dragon.GeneratedPackageRoot);
            Assert.IsNotNull(package);

            AssertAuthorityPoseRigIdentityRejected(
                dragon,
                package,
                CharacterAuthorityPoseRigRuntimeBinding.Missing);
            CharacterAuthorityPoseRigRuntimeBinding rig = package.Geometry.AuthorityPoseRig;
            AssertAuthorityPoseRigIdentityRejected(
                dragon,
                package,
                CloneAuthorityPoseRig(rig, rig.Revision, "sha256:not-valid"));
        }

        private static CharacterTestCharacterRuntimeModule CreateRuntime(
            CharacterTestCharacterDefinition definition)
        {
            return new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                definitions: new CharacterTestCharacterDefinitionRegistry(new[] { definition }),
                createViews: false);
        }

        private static CharacterTestCharacterEntity Spawn(
            CharacterTestCharacterRuntimeModule runtime,
            CharacterTestCharacterDefinition definition)
        {
            CharacterTestCharacterEntity entity = runtime.World.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            entity.SetMotionState(0f, UnityEngine.Vector2.zero, isGrounded: true);
            return entity;
        }

        private static void ConfigureAuthority(CharacterTestCharacterEntity entity)
        {
            CharacterAuthorityPoseRigDefinition definition =
                CharacterAuthorityPoseRigDefinition.FromRuntimeBinding(
                    CharacterTestAuthorityPoseTerrainTests.CreateRig(1));
            Assert.IsTrue(entity.TerrainIkState.Configure(
                definition,
                baseSourceRevision: 7,
                baseSourceContentHash: "sha256:test-base-v1"));
        }

        private static void SetOriginHistory(CharacterTestCharacterEntity entity, long frame)
        {
            CharacterTestTerrainIkLimbState limb = entity.TerrainIkState.GetLimb(0);
            limb.CurrentValid = true;
            limb.CurrentFailure = CharacterTestAuthorityPoseFailure.Success;
            limb.LastValidFrame = frame;
            limb.ConsecutiveMissCount = 0;
            limb.LastValidTargetWorld = FixVector3.Zero;
            limb.LastValidNormalWorld = AxisY();
            limb.SurfaceEntityId = 8101;
            limb.SurfaceBodyId = 8102;
            limb.SurfaceColliderId = 8103;
            limb.SurfaceLeafId = 8103;
            limb.SurfaceShapeKind = CombatPhysicsShapeKind.Aabb;
            limb.SurfaceMaterial = CombatSurfaceMaterial.Rough;
            limb.CurrentWeight = Fix64.Half;
            entity.TerrainIkState.SetLimb(0, limb);
            entity.TerrainIkState.BeginResolvedFrame(frame);
        }

        private static void AssertLimbEqual(
            in CharacterTestTerrainIkLimbState expected,
            in CharacterTestTerrainIkLimbState actual)
        {
            Assert.AreEqual(expected.LastValidFrame, actual.LastValidFrame);
            Assert.AreEqual(expected.ConsecutiveMissCount, actual.ConsecutiveMissCount);
            Assert.AreEqual(expected.LastValidTargetWorld, actual.LastValidTargetWorld);
            Assert.AreEqual(expected.LastValidNormalWorld, actual.LastValidNormalWorld);
            Assert.AreEqual(expected.SurfaceEntityId, actual.SurfaceEntityId);
            Assert.AreEqual(expected.SurfaceBodyId, actual.SurfaceBodyId);
            Assert.AreEqual(expected.SurfaceColliderId, actual.SurfaceColliderId);
            Assert.AreEqual(expected.SurfaceLeafId, actual.SurfaceLeafId);
            Assert.AreEqual(expected.SurfaceShapeKind, actual.SurfaceShapeKind);
            Assert.AreEqual(expected.SurfaceMaterial, actual.SurfaceMaterial);
            Assert.AreEqual(expected.CurrentWeight.RawValue, actual.CurrentWeight.RawValue);
        }

        private static void AssertResolvedEqual(
            CharacterTestResolvedAuthorityPoseFrame expected,
            CharacterTestResolvedAuthorityPoseFrame actual)
        {
            Assert.AreEqual(expected.RuntimeFrame, actual.RuntimeFrame);
            Assert.AreEqual(expected.Revision, actual.Revision);
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].ChannelId, actual[i].ChannelId);
                Assert.AreEqual(expected[i].Position, actual[i].Position);
                Assert.AreEqual(expected[i].Basis.Right, actual[i].Basis.Right);
                Assert.AreEqual(expected[i].Basis.Up, actual[i].Basis.Up);
                Assert.AreEqual(expected[i].Basis.Forward, actual[i].Basis.Forward);
                Assert.AreEqual(expected[i].Valid, actual[i].Valid);
                Assert.AreEqual(expected[i].SourceMask, actual[i].SourceMask);
                Assert.AreEqual(expected[i].Failure, actual[i].Failure);
            }
        }

        private static CharacterTestCharacterDefinition CreateCharacterDefinition()
        {
            return new CharacterTestCharacterDefinition(
                characterId: 9201,
                displayName: "Authority Pose Network Test",
                prefabKey: default(ResourceKey),
                defaultTeamId: 1,
                animationBinding: default);
        }

        private static CharacterTestCharacterDefinition FindDragonDefinition()
        {
            var definitions = CharacterTestPlayableCharacterCatalog.Discover();
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

        private static void AssertAuthorityPoseRigIdentityRejected(
            CharacterTestCharacterDefinition dragon,
            CharacterImportedPackage package,
            CharacterAuthorityPoseRigRuntimeBinding rig)
        {
            CharacterImportedPackage invalid = ClonePackage(
                package,
                CloneGeometryWithAuthorityPoseRig(package.Geometry, rig));
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                CharacterTestNetworkSessionFactory.ComputePlayableCharacterResourceCatalogHash(
                    new[] { dragon },
                    _ => invalid));
            StringAssert.Contains(
                "authored Authority Pose Rig identity is invalid",
                failure.Message);
        }

        private static CharacterImportedPackage ClonePackage(
            CharacterImportedPackage source,
            CharacterImportedGeometryBinding geometry)
        {
            return new CharacterImportedPackage(
                source.RootPath,
                source.PackageId,
                source.Configs,
                geometry,
                source.ResourceMapping,
                source.UnityResourceCatalog,
                source.ImportReport,
                source.RuntimeResourceCatalog,
                source.RuntimeResourcePlan,
                source.AudioCueManifest,
                source.CompiledAnimationArtifacts);
        }

        private static CharacterImportedGeometryBinding CloneGeometryWithAuthorityPoseRig(
            CharacterImportedGeometryBinding source,
            CharacterAuthorityPoseRigRuntimeBinding authorityPoseRig)
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
                source.BodyPartDamageDefinitions,
                source.BodyPartDamagePartLookup,
                source.BodyPartDamageFormat,
                source.BodyPartDamageSchemaVersion,
                source.BodyPartDamageRevision,
                source.BodyPartDamageContentHash,
                source.BodyPartDamageMode,
                authorityPoseRig);
        }

        private static CharacterAuthorityPoseRigRuntimeBinding CloneAuthorityPoseRig(
            CharacterAuthorityPoseRigRuntimeBinding source,
            int revision,
            string contentHash)
        {
            return new CharacterAuthorityPoseRigRuntimeBinding(
                source.Format,
                source.SchemaVersion,
                revision,
                contentHash,
                source.CoordinateSpace,
                source.DistanceUnit,
                source.NormalQuantizationScale,
                source.Channels,
                source.Limbs,
                source.Attachments);
        }

        private static string ChangeSha256(string value)
        {
            Assert.IsNotEmpty(value);
            char replacement = value[value.Length - 1] == '0' ? '1' : '0';
            return value.Substring(0, value.Length - 1) + replacement;
        }

        private static FixVector3 AxisY() =>
            new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
    }
}

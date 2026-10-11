using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestLargeCharacterPresenceAuthorityTests
    {
        [Test]
        public void ProductionCatalogIdentity_CoversLegacyAndV7PackagesAndIgnoresDiscoveryOrder()
        {
            IReadOnlyList<CharacterTestCharacterDefinition> definitions =
                CharacterTestPlayableCharacterCatalog.Discover();
            Assert.GreaterOrEqual(definitions.Count, 3);
            Assert.AreEqual(
                "mx.characterGeometryBinding.v8",
                LoadPackage(FindDefinition(definitions, "000004")).Geometry.Format);
            Assert.AreEqual(
                "mx.characterGeometryBinding.v8",
                LoadPackage(FindDefinition(definitions, "000001")).Geometry.Format);
            CharacterImportedGeometryBinding dragonGeometry =
                LoadPackage(FindDefinition(definitions, "000003")).Geometry;
            Assert.AreEqual("mx.characterGeometryBinding.v8", dragonGeometry.Format);
            Assert.AreEqual("8.0", dragonGeometry.SchemaVersion);
            Assert.AreEqual(CharacterBodyPresenceMode.Authored, dragonGeometry.BodyPresenceMode);
            Assert.AreEqual(CharacterBodyPartDamageMode.Authored, dragonGeometry.BodyPartDamageMode);
            Assert.IsTrue(dragonGeometry.AuthorityPoseRig.IsAuthored);
            Assert.AreEqual(
                "mx.characterBodyPartDamage.v3",
                dragonGeometry.BodyPartDamageFormat);

            string expected = ComputeIdentity(definitions);
            var reversed = new CharacterTestCharacterDefinition[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
                reversed[i] = definitions[definitions.Count - i - 1];

            Assert.AreEqual(expected, ComputeIdentity(reversed));
            StringAssert.StartsWith(
                "mx.character-test.playable-character-catalog.v1:sha256:",
                expected);
            NetworkSessionConfig production =
                CharacterTestNetworkSessionFactory.CreateNetworkSessionConfig(
                    new CharacterTestNetworkSessionFactoryOptions());
            Assert.AreEqual(expected, production.ResourceCatalogHash);
            Assert.IsNotEmpty(production.ResourceCatalogHash);
        }

        [Test]
        public void CatalogIdentity_AuthoredDefinitionCatalogRevisionOrHashChangesChangeHash()
        {
            IReadOnlyList<CharacterTestCharacterDefinition> definitions =
                CharacterTestPlayableCharacterCatalog.Discover();
            string baseline = CharacterTestNetworkSessionFactory.ComputePlayableCharacterResourceCatalogHash(
                definitions,
                CharacterTestGeneratedPackageSource.LoadPackage);
            string revisionChanged = CharacterTestNetworkSessionFactory.ComputePlayableCharacterResourceCatalogHash(
                definitions,
                CharacterTestGeneratedPackageSource.LoadPackage,
                CharacterTestAuthoredContentIds.Revision + 1L,
                CharacterTestAuthoredContentIds.CatalogHash);
            string hashChanged = CharacterTestNetworkSessionFactory.ComputePlayableCharacterResourceCatalogHash(
                definitions,
                CharacterTestGeneratedPackageSource.LoadPackage,
                CharacterTestAuthoredContentIds.Revision,
                CharacterTestAuthoredContentIds.CatalogHash.Substring(
                    0,
                    CharacterTestAuthoredContentIds.CatalogHash.Length - 1) + "0");

            Assert.AreNotEqual(baseline, revisionChanged);
            Assert.AreNotEqual(baseline, hashChanged);
        }

        [Test]
        public void CatalogIdentity_SourceGeometryAndPresenceIdentityChangesChangeHash()
        {
            IReadOnlyList<CharacterTestCharacterDefinition> definitions =
                CharacterTestPlayableCharacterCatalog.Discover();
            CharacterTestCharacterDefinition dragon = FindDefinition(definitions, "000003");
            CharacterImportedPackage package = LoadPackage(dragon);
            string baseline = ComputeIdentity(definitions);

            CharacterUnityImportRuntimeReport sourceChanged = CloneReport(
                package.ImportReport,
                sourcePackageHash: ChangeHash(package.ImportReport.SourcePackageHash));
            Assert.AreNotEqual(
                baseline,
                ComputeIdentity(definitions, dragon.GeneratedPackageRoot, ClonePackage(package, sourceChanged, package.Geometry)));

            CharacterUnityImportRuntimeReport geometryChanged = CloneReport(
                package.ImportReport,
                geometryBindingHash: ChangeHash(package.ImportReport.GeometryBindingHash));
            Assert.AreNotEqual(
                baseline,
                ComputeIdentity(definitions, dragon.GeneratedPackageRoot, ClonePackage(package, geometryChanged, package.Geometry)));

            CharacterImportedGeometryBinding revisionChanged = CloneGeometry(
                package.Geometry,
                package.Geometry.BodyPresenceRevision + 1,
                package.Geometry.BodyPresenceContentHash);
            Assert.AreNotEqual(
                baseline,
                ComputeIdentity(definitions, dragon.GeneratedPackageRoot, ClonePackage(package, package.ImportReport, revisionChanged)));

            CharacterImportedGeometryBinding contentChanged = CloneGeometry(
                package.Geometry,
                package.Geometry.BodyPresenceRevision,
                ChangeHash(package.Geometry.BodyPresenceContentHash));
            Assert.AreNotEqual(
                baseline,
                ComputeIdentity(definitions, dragon.GeneratedPackageRoot, ClonePackage(package, package.ImportReport, contentChanged)));
        }

        [Test]
        public void CatalogIdentity_V4StillRequiresAuthoredBodyPresenceIdentity()
        {
            IReadOnlyList<CharacterTestCharacterDefinition> definitions =
                CharacterTestPlayableCharacterCatalog.Discover();
            CharacterTestCharacterDefinition dragon = FindDefinition(definitions, "000003");
            CharacterImportedPackage package = LoadPackage(dragon);
            CharacterImportedGeometryBinding v4 = CloneAsV4Geometry(
                package.Geometry,
                package.Geometry.BodyPresenceRevision,
                package.Geometry.BodyPresenceContentHash);

            Assert.IsNotEmpty(
                ComputeIdentity(
                    definitions,
                    dragon.GeneratedPackageRoot,
                    ClonePackage(package, package.ImportReport, v4)));

            CharacterImportedGeometryBinding invalid = CloneAsV4Geometry(
                package.Geometry,
                bodyPresenceRevision: 0,
                bodyPresenceContentHash: package.Geometry.BodyPresenceContentHash);
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                ComputeIdentity(
                    definitions,
                    dragon.GeneratedPackageRoot,
                    ClonePackage(package, package.ImportReport, invalid)));
            StringAssert.Contains("authored Body Presence identity is invalid", failure.Message);
        }

        [Test]
        public void CatalogIdentity_V7BodyPartDamageIdentityChangesHashAndFailsClosed()
        {
            IReadOnlyList<CharacterTestCharacterDefinition> definitions =
                CharacterTestPlayableCharacterCatalog.Discover();
            CharacterTestCharacterDefinition dragon = FindDefinition(definitions, "000003");
            CharacterImportedPackage package = LoadPackage(dragon);
            string baseline = ComputeIdentity(definitions);

            CharacterImportedGeometryBinding revisionChanged = CloneGeometry(
                package.Geometry,
                package.Geometry.BodyPresenceRevision,
                package.Geometry.BodyPresenceContentHash,
                bodyPartDamageRevision: package.Geometry.BodyPartDamageRevision + 1);
            Assert.AreNotEqual(
                baseline,
                ComputeIdentity(
                    definitions,
                    dragon.GeneratedPackageRoot,
                    ClonePackage(package, package.ImportReport, revisionChanged)));

            CharacterImportedGeometryBinding contentChanged = CloneGeometry(
                package.Geometry,
                package.Geometry.BodyPresenceRevision,
                package.Geometry.BodyPresenceContentHash,
                bodyPartDamageContentHash: ChangeHash(package.Geometry.BodyPartDamageContentHash));
            Assert.AreNotEqual(
                baseline,
                ComputeIdentity(
                    definitions,
                    dragon.GeneratedPackageRoot,
                    ClonePackage(package, package.ImportReport, contentChanged)));

            AssertBodyPartDamageIdentityRejected(
                definitions,
                dragon,
                package,
                CloneGeometry(
                    package.Geometry,
                    package.Geometry.BodyPresenceRevision,
                    package.Geometry.BodyPresenceContentHash,
                    bodyPartDamageFormat: "mx.characterBodyPartDamage.invalid"));
            AssertBodyPartDamageIdentityRejected(
                definitions,
                dragon,
                package,
                CloneGeometry(
                    package.Geometry,
                    package.Geometry.BodyPresenceRevision,
                    package.Geometry.BodyPresenceContentHash,
                    bodyPartDamageSchemaVersion: "2.0"));
            AssertBodyPartDamageIdentityRejected(
                definitions,
                dragon,
                package,
                CloneGeometry(
                    package.Geometry,
                    package.Geometry.BodyPresenceRevision,
                    package.Geometry.BodyPresenceContentHash,
                    bodyPartDamageRevision: 0));
            AssertBodyPartDamageIdentityRejected(
                definitions,
                dragon,
                package,
                CloneGeometry(
                    package.Geometry,
                    package.Geometry.BodyPresenceRevision,
                    package.Geometry.BodyPresenceContentHash,
                    bodyPartDamageContentHash: "sha256:invalid"));
            AssertBodyPartDamageIdentityRejected(
                definitions,
                dragon,
                package,
                CloneGeometry(
                    package.Geometry,
                    package.Geometry.BodyPresenceRevision,
                    package.Geometry.BodyPresenceContentHash,
                    bodyPartDamageMode: CharacterBodyPartDamageMode.Missing));

        }

        [Test]
        public void CatalogIdentity_V6CompatibilityAndV7AuthoredContractsRemainDistinct()
        {
            IReadOnlyList<CharacterTestCharacterDefinition> definitions =
                CharacterTestPlayableCharacterCatalog.Discover();
            CharacterTestCharacterDefinition dragon = FindDefinition(definitions, "000003");
            CharacterImportedPackage package = LoadPackage(dragon);
            string authoredIdentity = ComputeIdentity(definitions);

            CharacterImportedGeometryBinding compatibilityPresence = CloneAsV6Geometry(
                package.Geometry,
                bodyPresenceRevision: 0,
                bodyPresenceContentHash: string.Empty,
                bodyPresenceMode: CharacterBodyPresenceMode.CompatibilityHardRoot,
                bodyPresenceVolumes: Array.Empty<CharacterBodyPresenceRuntimeBinding>());
            string compatibilityIdentity = ComputeIdentity(
                definitions,
                dragon.GeneratedPackageRoot,
                ClonePackage(package, package.ImportReport, compatibilityPresence));
            Assert.IsNotEmpty(compatibilityIdentity);
            Assert.AreNotEqual(authoredIdentity, compatibilityIdentity);

            AssertV7BodyPresenceIdentityRejected(
                definitions,
                dragon,
                package,
                CloneGeometry(
                    package.Geometry,
                    bodyPresenceRevision: 1,
                    bodyPresenceContentHash: string.Empty,
                    bodyPresenceMode: CharacterBodyPresenceMode.CompatibilityHardRoot,
                    bodyPresenceVolumes: Array.Empty<CharacterBodyPresenceRuntimeBinding>()));
            AssertV7BodyPresenceIdentityRejected(
                definitions,
                dragon,
                package,
                CloneGeometry(
                    package.Geometry,
                    bodyPresenceRevision: 0,
                    bodyPresenceContentHash: package.Geometry.BodyPresenceContentHash,
                    bodyPresenceMode: CharacterBodyPresenceMode.CompatibilityHardRoot,
                    bodyPresenceVolumes: Array.Empty<CharacterBodyPresenceRuntimeBinding>()));
            AssertV7BodyPresenceIdentityRejected(
                definitions,
                dragon,
                package,
                CloneGeometry(
                    package.Geometry,
                    bodyPresenceRevision: 0,
                    bodyPresenceContentHash: string.Empty,
                    bodyPresenceMode: CharacterBodyPresenceMode.CompatibilityHardRoot,
                    bodyPresenceVolumes: package.Geometry.BodyPresenceVolumes));
            AssertV7BodyPresenceIdentityRejected(
                definitions,
                dragon,
                package,
                CloneGeometry(
                    package.Geometry,
                    bodyPresenceRevision: 0,
                    bodyPresenceContentHash: string.Empty,
                    bodyPresenceMode: CharacterBodyPresenceMode.Authored,
                    bodyPresenceVolumes: Array.Empty<CharacterBodyPresenceRuntimeBinding>()));
        }

        [Test]
        public void CatalogIdentity_NonEmptyUnresolvablePackageFailsClosed()
        {
            IReadOnlyList<CharacterTestCharacterDefinition> definitions =
                CharacterTestPlayableCharacterCatalog.Discover();
            CharacterTestCharacterDefinition definition = definitions[0];

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                CharacterTestNetworkSessionFactory.ComputePlayableCharacterResourceCatalogHash(
                    new[] { definition },
                    _ => null));

            StringAssert.Contains("package load failed", failure.Message);
        }

        [Test]
        public void NetworkHandshake_RejectsPlayablePackageResourceIdentityMismatch()
        {
            NetworkSessionConfig serverConfig =
                CharacterTestNetworkSessionFactory.CreateNetworkSessionConfig(
                    new CharacterTestNetworkSessionFactoryOptions());
            var clientConfig = new NetworkSessionConfig(
                serverConfig.ProtocolVersion,
                serverConfig.TickRate,
                serverConfig.InputDelayFrames,
                serverConfig.SnapshotIntervalFrames,
                serverConfig.ConfigHash,
                ChangeHash(serverConfig.ResourceCatalogHash),
                serverConfig.StateSyncPolicy,
                serverConfig.HeartbeatIntervalFrames);
            var transport = new LoopbackNetworkTransport();
            var server = new NetworkSessionRunner(
                NetworkSessionRole.Server,
                "presence-authority",
                "server",
                "server",
                serverConfig,
                transport);
            var client = new NetworkSessionRunner(
                NetworkSessionRole.Client,
                "presence-authority",
                "client",
                "server",
                clientConfig,
                transport);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);

            Assert.AreEqual(NetworkSessionStatus.Rejected, client.Status);
            StringAssert.Contains("resource catalog hash", client.RejectionReason);
        }

        [Test]
        public void ContactVelocity_RoundTripsThroughRollbackAndParticipatesInAllAuthorityHashes()
        {
            using (var authority = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            using (var restored = new GameSlice(NullRuntimeLogger.Instance, createCharacterViews: false))
            {
                for (int i = 0; i < 8; i++)
                {
                    authority.Tick(1d / CharacterTestDeterministicUnits.DefaultTickRate);
                    restored.Tick(1d / CharacterTestDeterministicUnits.DefaultTickRate);
                }

                Assert.GreaterOrEqual(authority.CharacterEntities.Count, 2);
                int ownedEntityId = authority.CharacterEntities[0].Id.Value;
                SetContactVelocity(authority.CharacterEntities[0], 1234, -567);
                SetContactVelocity(authority.CharacterEntities[1], -890, 345);

                CharacterTestNetworkCharacterSnapshot direct =
                    CharacterTestNetworkCharacterSnapshot.FromEntity(authority.CharacterEntities[0]);
                CharacterTestCharacterPhysicsComponent directPhysics =
                    direct.CreatePhysicsComponent(CharacterTestCharacterPhysicsComponent.Default);
                Assert.AreEqual(1234, directPhysics.ContactVelocityXMillimetersPerSecond);
                Assert.AreEqual(-567, directPhysics.ContactVelocityYMillimetersPerSecond);

                var authorityParticipant = new CharacterTestNetworkSnapshotParticipant(
                    authority.CharacterRuntime,
                    authority.RuntimeCommands);
                var restoredParticipant = new CharacterTestNetworkSnapshotParticipant(
                    restored.CharacterRuntime,
                    restored.RuntimeCommands);
                RuntimeFrame frame = authority.CurrentFrame;
                Assert.AreNotEqual(
                    restoredParticipant.ComputeStateHash(frame),
                    authorityParticipant.ComputeStateHash(frame));
                Assert.AreNotEqual(
                    restoredParticipant.ComputeOwnedStateHash(frame, ownedEntityId),
                    authorityParticipant.ComputeOwnedStateHash(frame, ownedEntityId));
                Assert.AreNotEqual(
                    restoredParticipant.ComputeNonOwnedStateHash(frame, ownedEntityId),
                    authorityParticipant.ComputeNonOwnedStateHash(frame, ownedEntityId));

                CharacterTestNetworkSnapshotDocument rollback =
                    authorityParticipant.CaptureRollbackDocument(frame);
                NetworkSnapshotApplyResult apply = restoredParticipant.ApplyRollbackDocument(
                    frame,
                    rollback,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(apply.Success, apply.Message);
                Assert.AreEqual(
                    authorityParticipant.ComputeStateHash(frame),
                    restoredParticipant.ComputeStateHash(frame));
                Assert.AreEqual(
                    authorityParticipant.ComputeOwnedStateHash(frame, ownedEntityId),
                    restoredParticipant.ComputeOwnedStateHash(frame, ownedEntityId));
                Assert.AreEqual(
                    authorityParticipant.ComputeNonOwnedStateHash(frame, ownedEntityId),
                    restoredParticipant.ComputeNonOwnedStateHash(frame, ownedEntityId));
                Assert.AreEqual(
                    1234,
                    restored.CharacterEntities[0].Physics.ContactVelocityXMillimetersPerSecond);
                Assert.AreEqual(
                    -567,
                    restored.CharacterEntities[0].Physics.ContactVelocityYMillimetersPerSecond);

                authority.Tick(1d / CharacterTestDeterministicUnits.DefaultTickRate);
                restored.Tick(1d / CharacterTestDeterministicUnits.DefaultTickRate);
                RuntimeFrame nextFrame = authority.CurrentFrame;
                Assert.AreEqual(nextFrame, restored.CurrentFrame);
                Assert.AreEqual(
                    authorityParticipant.ComputeStateHash(nextFrame),
                    restoredParticipant.ComputeStateHash(nextFrame));
            }
        }

        [Test]
        public void PresenceDiagnostics_ProjectStableConstraintOrderAndWarmedReadsAllocateZeroBytes()
        {
            PresenceDiagnosticRun forward = RunPresenceDiagnostics(reverseRoster: false);
            PresenceDiagnosticRun reverse = RunPresenceDiagnostics(reverseRoster: true);

            Assert.AreEqual(CharacterTestBodyPresenceContactBuildStatus.Success, forward.Contact.Status);
            Assert.AreEqual(1, forward.Contact.PairCount);
            Assert.AreEqual(1, forward.Contact.ConstraintCount);
            Assert.AreEqual(28, forward.Contact.RawCandidateCapacity);
            Assert.AreEqual(28, forward.Contact.PairCapacity);
            Assert.AreEqual(224, forward.Contact.ConstraintCapacity);
            Assert.AreEqual(3, forward.Contact.PressureIterationCount);
            Assert.AreEqual(2, forward.Sync.ProjectionCount);
            Assert.AreEqual(1, forward.Sync.ProjectedLeafCount);
            Assert.AreEqual(2, forward.Sync.RegisteredEntityCount);
            Assert.IsTrue(forward.Sync.LastSyncSucceeded);
            Assert.AreEqual(1, forward.Constraint.PresenceEntityId);
            Assert.AreEqual(2, forward.Constraint.TargetEntityId);
            Assert.AreEqual(0, forward.Constraint.VolumeOrdinal);
            Assert.Greater(forward.Constraint.NormalXFix64Raw, 0L);
            Assert.Greater(forward.Constraint.PenetrationFix64Raw, 0L);
            Assert.AreEqual(forward.Constraint.PresenceEntityId, reverse.Constraint.PresenceEntityId);
            Assert.AreEqual(forward.Constraint.TargetEntityId, reverse.Constraint.TargetEntityId);
            Assert.AreEqual(forward.Constraint.ColliderId, reverse.Constraint.ColliderId);
            Assert.AreEqual(forward.Constraint.PenetrationFix64Raw, reverse.Constraint.PenetrationFix64Raw);
            Assert.AreEqual(0L, forward.AllocatedBytes);
            Assert.AreEqual(0L, reverse.AllocatedBytes);
        }

        [Test]
        public void PresenceDiagnostics_ProjectTypedFailureAndFourIndependentLayerToggles()
        {
            var contact = new CharacterTestBodyPresenceContactDiagnostics(
                CharacterTestBodyPresenceContactBuildStatus.CapacityExceeded,
                rawCandidateCount: 29,
                rawCandidateCapacity: 28,
                pairCount: 0,
                pairCapacity: 28,
                constraintCount: 0,
                constraintCapacity: 224,
                pressureIterationCount: 3,
                surfaceClampCount: 0);
            var sync = new CharacterTestBodyPresenceSyncDiagnostics(
                lastSyncSucceeded: false,
                projectionCount: 0,
                projectionCapacity: 8,
                projectedLeafCount: 0,
                projectedLeafCapacity: 32,
                registeredEntityCount: 0);
            CharacterTestLargeCharacterPresenceDebugLayer previous =
                CharacterTestLargeCharacterPresenceDebug.EnabledLayers;
            try
            {
                CharacterTestLargeCharacterPresenceDebug.EnabledLayers =
                    CharacterTestLargeCharacterPresenceDebugLayer.Presence
                    | CharacterTestLargeCharacterPresenceDebugLayer.ActiveTrace;
                CharacterTestDeveloperDebugCard card =
                    CharacterTestLargeCharacterPresenceDebug.CreateContactDiagnosticsCard(
                        contact,
                        sync);
                string projection = string.Join(
                    "\n",
                    System.Linq.Enumerable.Select(
                        card.Lines,
                        line => line.Name + "=" + line.Value));

                StringAssert.Contains("3/CapacityExceeded", projection);
                StringAssert.Contains("raw=29/28", projection);
                StringAssert.Contains("Hard Root=off", projection);
                StringAssert.Contains("Body Presence=on", projection);
                StringAssert.Contains("Animated Hurtbox=off", projection);
                StringAssert.Contains("Active Trace=on", projection);
                Assert.IsFalse(CharacterTestLargeCharacterPresenceDebug.IsLayerEnabled(
                    CharacterTestLargeCharacterPresenceDebug.EnabledLayers,
                    CharacterTestLargeCharacterPresenceDebugLayer.HardRoot));
                Assert.IsTrue(CharacterTestLargeCharacterPresenceDebug.IsLayerEnabled(
                    CharacterTestLargeCharacterPresenceDebug.EnabledLayers,
                    CharacterTestLargeCharacterPresenceDebugLayer.Presence));
                Assert.IsFalse(CharacterTestLargeCharacterPresenceDebug.IsLayerEnabled(
                    CharacterTestLargeCharacterPresenceDebug.EnabledLayers,
                    CharacterTestLargeCharacterPresenceDebugLayer.Hurtbox));
                Assert.IsTrue(CharacterTestLargeCharacterPresenceDebug.IsLayerEnabled(
                    CharacterTestLargeCharacterPresenceDebug.EnabledLayers,
                    CharacterTestLargeCharacterPresenceDebugLayer.ActiveTrace));
            }
            finally
            {
                CharacterTestLargeCharacterPresenceDebug.EnabledLayers = previous;
            }
        }

        [Test]
        public void FullPresenceAndHurtboxWindow_EightByFourByEighteen_HasWarmedZeroAllocationAndTimingBaseline()
        {
            const int CharacterCount = 8;
            const int WarmupFrameCount = 64;
            const int CaptureFrameCount = 4096;
            const float PresenceRadiusMeters = 0.1f;
            CharacterTestCharacterDefinition dragon = FindDefinition(
                CharacterTestPlayableCharacterCatalog.Discover(),
                "000003");
            var world = new CharacterTestCharacterRuntimeWorld();
            for (int i = 0; i < CharacterCount; i++)
            {
                world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        dragon.CharacterId,
                        teamId: 1,
                        x: 0f,
                        y: 0f,
                        z: 0f,
                        CharacterTestCharacterControlOwner.None),
                    dragon);
            }

            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var bodyPresence = new CharacterTestCharacterBodyPresenceSystem();
            var resolver = new CharacterTestCharacterContactResolver(null);
            CharacterImportedGeometryBinding calibrationBinding = CreateBinding(
                SphereAt("presence.perf.0", 0f, PresenceRadiusMeters),
                SphereAt("presence.perf.1", 0f, PresenceRadiusMeters),
                SphereAt("presence.perf.2", 0f, PresenceRadiusMeters),
                SphereAt("presence.perf.3", 0f, PresenceRadiusMeters));
            var calibrationBindings = new CharacterImportedGeometryBinding[CharacterCount];
            for (int i = 0; i < CharacterCount; i++)
                calibrationBindings[i] = calibrationBinding;
            Assert.IsTrue(bodyPresence.SyncAll(world.Entities, physics, calibrationBindings));
            resolver.ResolveCharacterContactPressure(world.Entities, physics, bodyPresenceReady: true);
            Assert.IsTrue(resolver.TryGetBodyPresenceConstraintDiagnostic(
                0,
                out CharacterTestBodyPresenceConstraintDiagnostic calibration));
            float nearTouchingOffsetMeters = Math.Max(
                0f,
                calibration.PenetrationFix64Raw / (float)Fix64.Scale - 0.001f);
            Assert.Greater(nearTouchingOffsetMeters, 0f);
            bodyPresence.Clear(physics);
            resolver.Clear();

            CharacterImportedGeometryBinding maximumPresenceBinding = CreateBinding(
                SphereAt("presence.perf.0", nearTouchingOffsetMeters, PresenceRadiusMeters),
                SphereAt("presence.perf.1", nearTouchingOffsetMeters, PresenceRadiusMeters),
                SphereAt("presence.perf.2", nearTouchingOffsetMeters, PresenceRadiusMeters),
                SphereAt("presence.perf.3", nearTouchingOffsetMeters, PresenceRadiusMeters));
            var bindings = new CharacterImportedGeometryBinding[CharacterCount];
            var motionSamples = new CharacterTestBodyPresenceMotionSample[CharacterCount];
            for (int i = 0; i < CharacterCount; i++)
            {
                bindings[i] = maximumPresenceBinding;
                CharacterTestCharacterEntity entity = world.Entities[i];
                motionSamples[i] = new CharacterTestBodyPresenceMotionSample(
                    entity.Id.Value,
                    entity.Transform.XMillimeters,
                    entity.Transform.YMillimeters,
                    entity.Transform.ZMillimeters,
                    entity.Transform.YawMilliDegrees,
                    entity.Transform.XMillimeters,
                    entity.Transform.YMillimeters,
                    entity.Transform.ZMillimeters,
                    entity.Transform.YawMilliDegrees,
                    maximumPresenceBinding,
                    rotationWitnessValid: true);
            }

            var hurtboxes = new CharacterTestCharacterHurtboxSystem();
            CharacterTestCharacterMovementProfileRuntime movement =
                CharacterTestCharacterMovementProfileSource.Load(null, null);
            int peakRawCandidateCount = 0;
            int peakPairCount = 0;
            int peakConstraintCount = 0;
            int peakClampCount = 0;
            bool allFramesValid = true;

            void ExecuteFrame(int frame)
            {
                bool presenceReady = bodyPresence.SyncAll(world.Entities, physics, bindings);
                hurtboxes.SyncAll(world.Entities, physics, new RuntimeFrame(frame));
                resolver.Clear();
                resolver.RecordAttemptedContactVelocity(world.Entities[0].Id.Value, 1000, 0);
                resolver.ResolveCharacterContactPressure(
                    world.Entities,
                    physics,
                    presenceReady,
                    motionSamples,
                    motionSamples.Length,
                    bodyPresenceMotionReady: true,
                    movement.RunSpeedMillimetersPerSecond,
                    movement.AccelerationMillimetersPerSecondSquared,
                    movement.TurnSpeedMilliDegreesPerSecond,
                    CharacterTestDeterministicUnits.DefaultTickRate);
                resolver.ApplyCharacterContactState(world.Entities);
                CharacterTestBodyPresenceContactDiagnostics contact =
                    resolver.CaptureBodyPresenceContactDiagnostics();
                CharacterTestBodyPresenceSyncDiagnostics sync = bodyPresence.CaptureDiagnostics();
                peakRawCandidateCount = Math.Max(peakRawCandidateCount, contact.RawCandidateCount);
                peakPairCount = Math.Max(peakPairCount, contact.PairCount);
                peakConstraintCount = Math.Max(peakConstraintCount, contact.ConstraintCount);
                peakClampCount = Math.Max(peakClampCount, contact.SurfaceClampCount);
                allFramesValid &= presenceReady
                    && sync.LastSyncSucceeded
                    && sync.ProjectionCount == CharacterCount
                    && sync.ProjectedLeafCount == CharacterCount * 4
                    && sync.RegisteredEntityCount == CharacterCount
                    && contact.Status == CharacterTestBodyPresenceContactBuildStatus.Success
                    && contact.PairCount == 28
                    && contact.ConstraintCount == 224
                    && contact.PressureIterationCount == 3
                    && !contact.HasFailure;
            }

            for (int i = 0; i < WarmupFrameCount; i++)
                ExecuteFrame(i);

            int activeHurtboxCount = 0;
            for (int i = 0; i < CharacterCount; i++)
            {
                Assert.IsTrue(hurtboxes.TryGetDamageGeometrySnapshot(
                    world.Entities[i].Id.Value,
                    out CharacterTestDamageGeometrySnapshot snapshot),
                    "Missing warmed Hurtbox snapshot for entity " + world.Entities[i].Id.Value);
                Assert.AreEqual(18, snapshot.ColliderCount);
                activeHurtboxCount += snapshot.ColliderCount;
            }
            Assert.AreEqual(144, activeHurtboxCount);
            CharacterTestBodyPresenceContactDiagnostics warmContact =
                resolver.CaptureBodyPresenceContactDiagnostics();
            CharacterTestBodyPresenceSyncDiagnostics warmSync = bodyPresence.CaptureDiagnostics();
            Assert.IsTrue(
                allFramesValid,
                "Warm window invalid. status=" + warmContact.Status
                    + " raw=" + warmContact.RawCandidateCount
                    + " pairs=" + warmContact.PairCount
                    + " constraints=" + warmContact.ConstraintCount
                    + " clamps=" + warmContact.SurfaceClampCount
                    + " sync=" + warmSync.LastSyncSucceeded
                    + "/" + warmSync.ProjectionCount
                    + "/" + warmSync.ProjectedLeafCount
                    + "/" + warmSync.RegisteredEntityCount);

            var elapsedTicks = new long[CaptureFrameCount];
            _ = Stopwatch.GetTimestamp();
            _ = GC.GetAllocatedBytesForCurrentThread();
            long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < CaptureFrameCount; i++)
            {
                long started = Stopwatch.GetTimestamp();
                ExecuteFrame(WarmupFrameCount + i);
                elapsedTicks[i] = Stopwatch.GetTimestamp() - started;
            }
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;

            Array.Sort(elapsedTicks);
            double medianMilliseconds = ToMilliseconds(Percentile(elapsedTicks, 0.50d));
            double p95Milliseconds = ToMilliseconds(Percentile(elapsedTicks, 0.95d));
            double maximumMilliseconds = ToMilliseconds(elapsedTicks[elapsedTicks.Length - 1]);
            CharacterTestBodyPresenceSyncDiagnostics finalSync = bodyPresence.CaptureDiagnostics();
            CharacterTestBodyPresenceContactDiagnostics finalContact =
                resolver.CaptureBodyPresenceContactDiagnostics();
            TestContext.Out.WriteLine(
                "scenario=lcc-m1-t4.8x4x18"
                + " unity=" + Application.unityVersion
                + " processor=" + SystemInfo.processorType
                + " characters=" + CharacterCount.ToString(CultureInfo.InvariantCulture)
                + " activePresence=" + finalSync.ProjectedLeafCount.ToString(CultureInfo.InvariantCulture)
                + " activeHurtbox=" + activeHurtboxCount.ToString(CultureInfo.InvariantCulture)
                + " warmup=" + WarmupFrameCount.ToString(CultureInfo.InvariantCulture)
                + " frames=" + CaptureFrameCount.ToString(CultureInfo.InvariantCulture)
                + " medianMs=" + medianMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)
                + " p95Ms=" + p95Milliseconds.ToString("0.###", CultureInfo.InvariantCulture)
                + " maxMs=" + maximumMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)
                + " gcBytes=" + allocatedBytes.ToString(CultureInfo.InvariantCulture)
                + " rawPeak=" + peakRawCandidateCount.ToString(CultureInfo.InvariantCulture)
                + " pairPeak=" + peakPairCount.ToString(CultureInfo.InvariantCulture)
                + " constraintPeak=" + peakConstraintCount.ToString(CultureInfo.InvariantCulture)
                + " iterations=" + finalContact.PressureIterationCount.ToString(CultureInfo.InvariantCulture)
                + " clampPeak=" + peakClampCount.ToString(CultureInfo.InvariantCulture)
                + " overflow=0"
                + " failure=0"
                + " sync=Success"
                + " projections=" + finalSync.ProjectionCount.ToString(CultureInfo.InvariantCulture)
                + " leaves=" + finalSync.ProjectedLeafCount.ToString(CultureInfo.InvariantCulture)
                + " registered=" + finalSync.RegisteredEntityCount.ToString(CultureInfo.InvariantCulture));

            Assert.IsTrue(allFramesValid);
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(28, peakRawCandidateCount);
            Assert.AreEqual(28, peakPairCount);
            Assert.AreEqual(224, peakConstraintCount);
            Assert.AreEqual(224, peakClampCount);
            Assert.AreEqual(8, finalSync.ProjectionCount);
            Assert.AreEqual(32, finalSync.ProjectedLeafCount);
            Assert.AreEqual(8, finalSync.RegisteredEntityCount);
            Assert.AreEqual(CharacterTestBodyPresenceContactBuildStatus.Success, finalContact.Status);
        }

        private static string ComputeIdentity(
            IReadOnlyList<CharacterTestCharacterDefinition> definitions,
            string replacementRoot = "",
            CharacterImportedPackage replacement = null)
        {
            return CharacterTestNetworkSessionFactory.ComputePlayableCharacterResourceCatalogHash(
                definitions,
                root => !string.IsNullOrEmpty(replacementRoot)
                    && string.Equals(
                        NormalizeRoot(root),
                        NormalizeRoot(replacementRoot),
                        StringComparison.Ordinal)
                        ? replacement
                        : CharacterTestGeneratedPackageSource.LoadPackage(root));
        }

        private static CharacterTestCharacterDefinition FindDefinition(
            IReadOnlyList<CharacterTestCharacterDefinition> definitions,
            string packageId)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (NormalizeRoot(definitions[i].GeneratedPackageRoot).EndsWith(
                    "/" + packageId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return definitions[i];
                }
            }
            Assert.Fail("Playable character definition is missing: " + packageId);
            return default;
        }

        private static CharacterImportedPackage LoadPackage(
            CharacterTestCharacterDefinition definition)
        {
            CharacterImportedPackage package =
                CharacterTestGeneratedPackageSource.LoadPackage(definition.GeneratedPackageRoot);
            Assert.IsNotNull(package, definition.GeneratedPackageRoot);
            return package;
        }

        private static CharacterImportedPackage ClonePackage(
            CharacterImportedPackage source,
            CharacterUnityImportRuntimeReport report,
            CharacterImportedGeometryBinding geometry)
        {
            return new CharacterImportedPackage(
                source.RootPath,
                source.PackageId,
                source.Configs,
                geometry,
                source.ResourceMapping,
                source.UnityResourceCatalog,
                report,
                source.RuntimeResourceCatalog,
                source.RuntimeResourcePlan,
                source.AudioCueManifest,
                source.CompiledAnimationArtifacts);
        }

        private static CharacterUnityImportRuntimeReport CloneReport(
            CharacterUnityImportRuntimeReport source,
            string sourcePackageHash = null,
            string geometryBindingHash = null)
        {
            return new CharacterUnityImportRuntimeReport(
                source.PackageId,
                source.PackageStableId,
                source.Status,
                source.CanWriteToUnityProject,
                source.CanSpawnAfterImport,
                source.TargetRootPath,
                source.ReportPath,
                sourcePackageHash ?? source.SourcePackageHash,
                source.GeneratedConfigHash,
                geometryBindingHash ?? source.GeometryBindingHash,
                source.ResourceMappingHash,
                source.WritePlanHash,
                source.ConflictCount,
                source.ErrorCount);
        }

        private static CharacterImportedGeometryBinding CloneGeometry(
            CharacterImportedGeometryBinding source,
            int bodyPresenceRevision,
            string bodyPresenceContentHash,
            CharacterBodyPresenceMode? bodyPresenceMode = null,
            CharacterBodyPresenceRuntimeBinding[] bodyPresenceVolumes = null,
            string bodyPartDamageFormat = null,
            string bodyPartDamageSchemaVersion = null,
            int? bodyPartDamageRevision = null,
            string bodyPartDamageContentHash = null,
            CharacterBodyPartDamageMode? bodyPartDamageMode = null)
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
                bodyPresenceVolumes ?? source.BodyPresenceVolumes,
                bodyPresenceRevision,
                bodyPresenceContentHash,
                bodyPresenceMode ?? source.BodyPresenceMode,
                source.BodyPartDamageDefinitions,
                source.BodyPartDamagePartLookup,
                bodyPartDamageFormat ?? source.BodyPartDamageFormat,
                bodyPartDamageSchemaVersion ?? source.BodyPartDamageSchemaVersion,
                bodyPartDamageRevision ?? source.BodyPartDamageRevision,
                bodyPartDamageContentHash ?? source.BodyPartDamageContentHash,
                bodyPartDamageMode ?? source.BodyPartDamageMode,
                source.AuthorityPoseRig);
        }

        private static CharacterImportedGeometryBinding CloneAsV4Geometry(
            CharacterImportedGeometryBinding source,
            int bodyPresenceRevision,
            string bodyPresenceContentHash)
        {
            return new CharacterImportedGeometryBinding(
                "mx.characterGeometryBinding.v4",
                "4.0",
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
                bodyPresenceRevision,
                bodyPresenceContentHash,
                CharacterBodyPresenceMode.Authored);
        }

        private static CharacterImportedGeometryBinding CloneAsV6Geometry(
            CharacterImportedGeometryBinding source,
            int bodyPresenceRevision,
            string bodyPresenceContentHash,
            CharacterBodyPresenceMode bodyPresenceMode,
            CharacterBodyPresenceRuntimeBinding[] bodyPresenceVolumes)
        {
            return new CharacterImportedGeometryBinding(
                "mx.characterGeometryBinding.v6",
                "6.0",
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
                bodyPresenceVolumes,
                bodyPresenceRevision,
                bodyPresenceContentHash,
                bodyPresenceMode,
                source.BodyPartDamageDefinitions,
                source.BodyPartDamagePartLookup,
                source.BodyPartDamageFormat,
                source.BodyPartDamageSchemaVersion,
                source.BodyPartDamageRevision,
                source.BodyPartDamageContentHash,
                source.BodyPartDamageMode,
                source.AuthorityPoseRig);
        }

        private static void AssertBodyPartDamageIdentityRejected(
            IReadOnlyList<CharacterTestCharacterDefinition> definitions,
            CharacterTestCharacterDefinition dragon,
            CharacterImportedPackage package,
            CharacterImportedGeometryBinding geometry)
        {
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                ComputeIdentity(
                    definitions,
                    dragon.GeneratedPackageRoot,
                    ClonePackage(package, package.ImportReport, geometry)));
            StringAssert.Contains("authored Body-Part Damage identity is invalid", failure.Message);
        }

        private static void AssertV7BodyPresenceIdentityRejected(
            IReadOnlyList<CharacterTestCharacterDefinition> definitions,
            CharacterTestCharacterDefinition dragon,
            CharacterImportedPackage package,
            CharacterImportedGeometryBinding geometry)
        {
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                ComputeIdentity(
                    definitions,
                    dragon.GeneratedPackageRoot,
                    ClonePackage(package, package.ImportReport, geometry)));
            StringAssert.Contains("authored Body Presence identity is invalid", failure.Message);
        }

        private static string ChangeHash(string value)
        {
            Assert.IsNotEmpty(value);
            char replacement = value[value.Length - 1] == '0' ? '1' : '0';
            return value.Substring(0, value.Length - 1) + replacement;
        }

        private static string NormalizeRoot(string value)
        {
            return (value ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
        }

        private static PresenceDiagnosticRun RunPresenceDiagnostics(bool reverseRoster)
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            var presence = new CharacterTestCharacterBodyPresenceSystem();
            CharacterTestCharacterEntity source = CreateEntity(1, 0f);
            CharacterTestCharacterEntity target = CreateEntity(2, 2f);
            CharacterTestCharacterEntity[] entities = { source, target };
            CharacterImportedGeometryBinding[] bindings =
            {
                CreateBinding(SphereAt(1f, 0.7f)),
                CreateBinding()
            };
            if (reverseRoster)
            {
                Array.Reverse(entities);
                Array.Reverse(bindings);
            }

            Assert.IsTrue(presence.SyncAll(entities, physics, bindings));
            CharacterTestBodyPresenceSyncDiagnostics sync = presence.CaptureDiagnostics();
            var resolver = new CharacterTestCharacterContactResolver(null);
            resolver.ResolveCharacterContactPressure(entities, physics, bodyPresenceReady: true);
            CharacterTestBodyPresenceContactDiagnostics contact =
                resolver.CaptureBodyPresenceContactDiagnostics();
            Assert.IsTrue(resolver.TryGetBodyPresenceConstraintDiagnostic(
                0,
                out CharacterTestBodyPresenceConstraintDiagnostic constraint));

            for (int i = 0; i < 16; i++)
            {
                _ = resolver.CaptureBodyPresenceContactDiagnostics();
                resolver.TryGetBodyPresenceConstraintDiagnostic(0, out _);
                _ = presence.CaptureDiagnostics();
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool stable = true;
            for (int i = 0; i < 256; i++)
            {
                CharacterTestBodyPresenceContactDiagnostics current =
                    resolver.CaptureBodyPresenceContactDiagnostics();
                stable &= current.Status == CharacterTestBodyPresenceContactBuildStatus.Success;
                stable &= resolver.TryGetBodyPresenceConstraintDiagnostic(0, out _);
                stable &= presence.CaptureDiagnostics().LastSyncSucceeded;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(stable);

            return new PresenceDiagnosticRun(contact, sync, constraint, allocated);
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePhysics()
        {
            return new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(2),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                staticColliderCount: 1,
                sourceDescription: "test.presence-authority");
        }

        private static void SetContactVelocity(
            CharacterTestCharacterEntity entity,
            int velocityXMillimetersPerSecond,
            int velocityYMillimetersPerSecond)
        {
            CharacterTestCharacterPhysicsComponent physics = entity.Physics;
            entity.SetPhysics(CharacterTestCharacterPhysicsComponent.FromDeterministic(
                physics.CapsuleRadiusMillimeters,
                physics.CapsuleHeightMillimeters,
                physics.CapsuleCenterYMillimeters,
                physics.MassGrams,
                physics.MassScaleMilli,
                physics.RadiusScaleMilli,
                physics.PushScaleMilli,
                physics.BlocksCharacters,
                physics.CanBePushed,
                velocityXMillimetersPerSecond,
                velocityYMillimetersPerSecond,
                physics.ContactDisplacementXMillimeters,
                physics.ContactDisplacementYMillimeters,
                physics.ContactCount));
        }

        private static CharacterTestCharacterEntity CreateEntity(int id, float x)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(id),
                new CharacterTestCharacterIdentityComponent(
                    characterId: 1,
                    displayName: "Presence Authority Test Character",
                    prefabKey: default(ResourceKey),
                    animationBinding: default(CharacterRuntimeAnimationBinding)),
                new CharacterTestCharacterTeamComponent(teamId: 1, defaultTeamId: 1),
                new CharacterTestCharacterTransformComponent(x, 0f, 0f, 0f),
                new CharacterTestCharacterEquipmentComponent(useDefaultLoadout: false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                new GameplayEntityId(id, 1),
                CharacterTestCharacterPhysicsComponent.Default);
        }

        private static CharacterImportedGeometryBinding CreateBinding(
            params CharacterBodyPresenceRuntimeBinding[] volumes)
        {
            return new CharacterImportedGeometryBinding(
                "mx.characterGeometryBinding.v4",
                "4.0",
                "test.presence-authority",
                null,
                Array.Empty<CharacterBodyColliderRuntimeBinding>(),
                Array.Empty<CharacterHitZoneRuntimeBinding>(),
                Array.Empty<CharacterDamageZoneRuntimeBinding>(),
                Array.Empty<CharacterSocketRuntimeBinding>(),
                Array.Empty<CharacterWeaponAttachmentRuntimeBinding>(),
                Array.Empty<CharacterWeaponTraceRuntimeBinding>(),
                bodyPresenceVolumes: volumes,
                bodyPresenceRevision: 1,
                bodyPresenceContentHash: "sha256:" + new string('a', 64),
                bodyPresenceMode: CharacterBodyPresenceMode.Authored);
        }

        private static CharacterBodyPresenceRuntimeBinding SphereAt(float x, float radius)
        {
            return SphereAt("presence.test.core", x, radius);
        }

        private static CharacterBodyPresenceRuntimeBinding SphereAt(
            string volumeId,
            float x,
            float radius)
        {
            return new CharacterBodyPresenceRuntimeBinding(
                volumeId,
                CharacterBodyPresenceReferenceFrame.ModelRoot,
                CharacterBodyPresenceShape.Sphere,
                new CharacterRuntimePose(
                    "ModelRoot",
                    string.Empty,
                    new CharacterRuntimeVector3(x, 0.93f, 0f),
                    new CharacterRuntimeQuaternion(0f, 0f, 0f, 1f),
                    new CharacterRuntimeVector3(1f, 1f, 1f)),
                radius,
                height: 0f,
                size: default,
                skinMillimeters: 0,
                pressureScaleMilli: 1000,
                enabled: true);
        }

        private static long Percentile(long[] sorted, double percentile)
        {
            int index = (int)Math.Ceiling((sorted.Length - 1) * percentile);
            return sorted[Math.Max(0, Math.Min(sorted.Length - 1, index))];
        }

        private static double ToMilliseconds(long elapsedTicks)
        {
            return elapsedTicks * 1000d / Stopwatch.Frequency;
        }

        private readonly struct PresenceDiagnosticRun
        {
            internal PresenceDiagnosticRun(
                CharacterTestBodyPresenceContactDiagnostics contact,
                CharacterTestBodyPresenceSyncDiagnostics sync,
                CharacterTestBodyPresenceConstraintDiagnostic constraint,
                long allocatedBytes)
            {
                Contact = contact;
                Sync = sync;
                Constraint = constraint;
                AllocatedBytes = allocatedBytes;
            }

            internal CharacterTestBodyPresenceContactDiagnostics Contact { get; }
            internal CharacterTestBodyPresenceSyncDiagnostics Sync { get; }
            internal CharacterTestBodyPresenceConstraintDiagnostic Constraint { get; }
            internal long AllocatedBytes { get; }
        }
    }
}

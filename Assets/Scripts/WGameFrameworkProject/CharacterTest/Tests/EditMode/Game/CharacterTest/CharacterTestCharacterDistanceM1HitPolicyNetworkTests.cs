using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestCharacterDistanceM1HitPolicyNetworkTests
    {
        [Test]
        public void NetworkSnapshot_RestoresMidWindowLedgerAndContinuesSameMultiHitBranch()
        {
            using (GameSlice slice = CreateSlice())
            {
                Tick(slice, 8);
                CharacterTestHitResolutionPolicyState policy =
                    slice.CharacterRuntime.CombatTraceSystem.HitResolutionPolicyState;
                CombatHitEventConfig config = CreateOncePerEventConfig(maxAcceptedHits: 3);
                const int policyEventId = 17;

                Assert.IsTrue(policy.TryAccept(
                    CreateCandidate(attacker: 1, target: 2, actionInstance: 21, traceId: 1),
                    policyEventId,
                    config));

                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame frame = slice.CurrentFrame;
                long hashAfterFirstHit = participant.ComputeStateHash(frame);
                RuntimeCustomState snapshot = participant.CaptureSnapshot(frame);
                CharacterTestNetworkSnapshotDocument rollbackDocument =
                    participant.CaptureRollbackDocument(frame);
                JObject document = JObject.Parse(snapshot.PayloadJson);
                JArray acceptedEvents = (JArray)document["hitResolutionPolicy"]["acceptedEvents"];
                JArray acceptedTargets = (JArray)document["hitResolutionPolicy"]["acceptedTargets"];
                Assert.AreEqual(1, acceptedEvents.Count);
                Assert.AreEqual(1, acceptedTargets.Count);
                Assert.AreEqual(17, (int)acceptedEvents[0]["policyEventId"]);
                Assert.AreEqual(2, (int)acceptedTargets[0]["targetEntityId"]);

                bool originalDuplicate = policy.TryAccept(
                    CreateCandidate(attacker: 1, target: 2, actionInstance: 21, traceId: 2),
                    policyEventId,
                    config);
                bool originalSecondTarget = policy.TryAccept(
                    CreateCandidate(attacker: 1, target: 3, actionInstance: 21, traceId: 3),
                    policyEventId,
                    config);
                long originalBranchHash = participant.ComputeStateHash(frame);
                Assert.IsFalse(originalDuplicate, "OncePerEvent must reject the repeated target.");
                Assert.IsTrue(originalSecondTarget, "OncePerEvent must accept a new target within MaxAcceptedHits.");

                Assert.IsTrue(policy.TryAccept(
                    CreateCandidate(attacker: 1, target: 4, actionInstance: 21, traceId: 4),
                    policyEventId,
                    config));
                Assert.AreNotEqual(hashAfterFirstHit, participant.ComputeStateHash(frame));

                NetworkSnapshotApplyResult apply = participant.ApplySnapshot(frame, snapshot);
                Assert.IsTrue(apply.Success, apply.Message);
                Assert.AreEqual(hashAfterFirstHit, participant.ComputeStateHash(frame));

                Assert.IsTrue(policy.TryAccept(
                    CreateCandidate(attacker: 1, target: 4, actionInstance: 21, traceId: 4),
                    policyEventId,
                    config));
                NetworkSnapshotApplyResult rollbackApply = participant.ApplyRollbackDocument(
                    frame,
                    rollbackDocument,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(rollbackApply.Success, rollbackApply.Message);
                Assert.AreEqual(hashAfterFirstHit, participant.ComputeStateHash(frame));

                bool restoredDuplicate = policy.TryAccept(
                    CreateCandidate(attacker: 1, target: 2, actionInstance: 21, traceId: 2),
                    policyEventId,
                    config);
                bool restoredSecondTarget = policy.TryAccept(
                    CreateCandidate(attacker: 1, target: 3, actionInstance: 21, traceId: 3),
                    policyEventId,
                    config);
                Assert.AreEqual(originalDuplicate, restoredDuplicate);
                Assert.AreEqual(originalSecondTarget, restoredSecondTarget);
                Assert.AreEqual(originalBranchHash, participant.ComputeStateHash(frame));
            }
        }

        [Test]
        public void NetworkSnapshot_RandomPolicyInsertionOrderProducesStableJsonOrderAndHash()
        {
            using (GameSlice first = CreateSlice())
            using (GameSlice second = CreateSlice())
            {
                Tick(first, 8);
                Tick(second, 8);
                AddPolicyEntries(
                    first.CharacterRuntime.CombatTraceSystem.HitResolutionPolicyState,
                    new[] { 0, 1, 2, 3, 4, 5 });
                AddPolicyEntries(
                    second.CharacterRuntime.CombatTraceSystem.HitResolutionPolicyState,
                    new[] { 5, 4, 3, 2, 1, 0 });

                CharacterTestNetworkSnapshotParticipant firstParticipant =
                    first.Services.Network.CreateSnapshotParticipant();
                CharacterTestNetworkSnapshotParticipant secondParticipant =
                    second.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame frame = first.CurrentFrame;
                RuntimeCustomState firstSnapshot = firstParticipant.CaptureSnapshot(frame);
                RuntimeCustomState secondSnapshot = secondParticipant.CaptureSnapshot(frame);
                JObject firstDocument = JObject.Parse(firstSnapshot.PayloadJson);
                JObject secondDocument = JObject.Parse(secondSnapshot.PayloadJson);

                Assert.IsTrue(
                    JToken.DeepEquals(
                        firstDocument["hitResolutionPolicy"]["acceptedEvents"],
                        secondDocument["hitResolutionPolicy"]["acceptedEvents"]));
                Assert.IsTrue(
                    JToken.DeepEquals(
                        firstDocument["hitResolutionPolicy"]["acceptedTargets"],
                        secondDocument["hitResolutionPolicy"]["acceptedTargets"]));
                Assert.AreEqual(
                    firstParticipant.ComputeStateHash(frame),
                    secondParticipant.ComputeStateHash(frame));
            }
        }

        [Test]
        public void NetworkSnapshot_LegacyCharacterSchemaDefaultsSourceRevisions()
        {
            var legacy = new CharacterTestNetworkCharacterSnapshot
            {
                SourcePoseRevision = 91,
                SourceFacingRevision = 73
            };

            legacy.NormalizeForSchema(
                CharacterTestNetworkSnapshotParticipant.HitTraceWindowSchemaVersionValue);

            Assert.AreEqual(
                CharacterTestCharacterSourceRevisionComponent.InitialRevision,
                legacy.SourcePoseRevision);
            Assert.AreEqual(
                CharacterTestCharacterSourceRevisionComponent.InitialRevision,
                legacy.SourceFacingRevision);

            var current = new CharacterTestNetworkCharacterSnapshot
            {
                SourcePoseRevision = 91,
                SourceFacingRevision = 73
            };
            current.NormalizeForSchema(CharacterTestNetworkSnapshotParticipant.SchemaVersionValue);

            Assert.AreEqual(91, current.SourcePoseRevision);
            Assert.AreEqual(73, current.SourceFacingRevision);
        }

        [Test]
        public void NetworkSnapshot_Schema25ApplyAndRollbackNormalizeSourceRevisionsAndKeepActiveWindow()
        {
            var startupSpecs = new[]
            {
                new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 1f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.None)
            };

            using (GameSlice slice = new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: CreateNetworkTestPhysicsRuntime(),
                createCharacterViews: false,
                enableLocalInputCommands: false,
                startupCharacterSpawnSpecs: startupSpecs))
            {
                Tick(slice, 8);
                CharacterTestCharacterEntity source =
                    slice.Services.CharacterRuntime.FindEntityById(1);
                Assert.NotNull(source);
                RuntimeFrame frame = slice.CurrentFrame;
                source.EnqueueCombatRequest(CreateActiveHitWindowRequest(
                    frame,
                    source.Id.Value,
                    instanceId: 1101L,
                    stableEventId: CharacterTestAuthoredContentIds.D401_Id161,
                    lifecyclePhase: CharacterActionTrackLifecyclePhase.Enter,
                    lifecycleBoundary: CharacterActionTrackLifecycleBoundary.BeforeFrame));
                Tick(slice, 1);

                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeCustomState captured = participant.CaptureSnapshot(slice.CurrentFrame);
                JObject schema25Document = JObject.Parse(captured.PayloadJson);
                JToken capturedPolicy = schema25Document["hitResolutionPolicy"]?.DeepClone();
                JArray characters = (JArray)schema25Document["characters"];
                Assert.IsNotNull(capturedPolicy);
                Assert.IsNotNull(characters);
                Assert.Greater(((JArray)capturedPolicy["activeTraces"]).Count, 0);
                for (int i = 0; i < characters.Count; i++)
                {
                    characters[i]["sourcePoseRevision"] = 91;
                    characters[i]["sourceFacingRevision"] = 73;
                }
                schema25Document["schemaVersion"] =
                    CharacterTestNetworkSnapshotParticipant.HitTraceWindowSchemaVersionValue;
                RuntimeCustomState schema25 = new RuntimeCustomState(
                    captured.NumericTypeId,
                    captured.TypeId,
                    CharacterTestNetworkSnapshotParticipant.HitTraceWindowSchemaVersionValue,
                    schema25Document.ToString(Newtonsoft.Json.Formatting.None));

                source.RestoreSourceRevisions(99, 88);
                NetworkSnapshotApplyResult apply = participant.ApplySnapshot(slice.CurrentFrame, schema25);
                Assert.IsTrue(apply.Success, apply.Message);
                Assert.AreEqual(
                    CharacterTestCharacterSourceRevisionComponent.InitialRevision,
                    source.SourcePoseRevision);
                Assert.AreEqual(
                    CharacterTestCharacterSourceRevisionComponent.InitialRevision,
                    source.SourceFacingRevision);
                JObject appliedDocument = JObject.Parse(
                    participant.CaptureSnapshot(slice.CurrentFrame).PayloadJson);
                Assert.IsTrue(JToken.DeepEquals(
                    capturedPolicy,
                    appliedDocument["hitResolutionPolicy"]));

                source.RestoreSourceRevisions(77, 66);
                NetworkSnapshotApplyResult rollback = participant.ApplyRollbackSnapshot(
                    slice.CurrentFrame,
                    schema25,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(rollback.Success, rollback.Message);
                Assert.AreEqual(
                    CharacterTestCharacterSourceRevisionComponent.InitialRevision,
                    source.SourcePoseRevision);
                Assert.AreEqual(
                    CharacterTestCharacterSourceRevisionComponent.InitialRevision,
                    source.SourceFacingRevision);
                JObject rolledBackDocument = JObject.Parse(
                    participant.CaptureSnapshot(slice.CurrentFrame).PayloadJson);
                Assert.IsTrue(JToken.DeepEquals(
                    capturedPolicy,
                    rolledBackDocument["hitResolutionPolicy"]));
            }
        }

        [Test]
        public void NetworkSnapshot_Schema24JsonApplyAndRollbackKeepsLedgerClearsUntrustedWindow()
        {
            var startupSpecs = new[]
            {
                new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 1f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.None)
            };

            using (GameSlice slice = new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: CreateNetworkTestPhysicsRuntime(),
                createCharacterViews: false,
                enableLocalInputCommands: false,
                startupCharacterSpawnSpecs: startupSpecs))
            {
                Tick(slice, 8);
                CharacterTestCharacterEntity source =
                    slice.Services.CharacterRuntime.FindEntityById(1);
                CharacterTestCharacterCombatTraceSystem combatTrace =
                    slice.Services.CombatTraceSystem;
                RuntimeFrame frame = slice.CurrentFrame;
                source.EnqueueCombatRequest(CreateActiveHitWindowRequest(
                    frame,
                    source.Id.Value,
                    instanceId: 2401L,
                    stableEventId: CharacterTestAuthoredContentIds.D401_Id161,
                    lifecyclePhase: CharacterActionTrackLifecyclePhase.Enter,
                    lifecycleBoundary: CharacterActionTrackLifecycleBoundary.BeforeFrame));
                Tick(slice, 1);
                Assert.AreEqual(1, combatTrace.ActiveTraceCount);
                Assert.IsTrue(combatTrace.HitResolutionPolicyState.TryAccept(
                    CreateCandidate(
                        CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1,
                        CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 2,
                        actionInstance: 1,
                        traceId: 24),
                    policyEventId: 24,
                    CreateOncePerEventConfig(maxAcceptedHits: 2)));

                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeCustomState captured = participant.CaptureSnapshot(slice.CurrentFrame);
                JObject schema24Document = JObject.Parse(captured.PayloadJson);
                schema24Document["schemaVersion"] =
                    CharacterTestNetworkSnapshotParticipant.HitResolutionPolicySchemaVersionValue;
                JArray characters = (JArray)schema24Document["characters"];
                for (int i = 0; i < characters.Count; i++)
                {
                    characters[i]["sourcePoseRevision"] = 91;
                    characters[i]["sourceFacingRevision"] = 73;
                }
                RuntimeCustomState schema24 = new RuntimeCustomState(
                    captured.NumericTypeId,
                    captured.TypeId,
                    CharacterTestNetworkSnapshotParticipant.HitResolutionPolicySchemaVersionValue,
                    schema24Document.ToString(Newtonsoft.Json.Formatting.None));

                source.RestoreSourceRevisions(99, 88);
                NetworkSnapshotApplyResult apply = participant.ApplySnapshot(slice.CurrentFrame, schema24);
                Assert.IsTrue(apply.Success, apply.Message);
                Assert.AreEqual(
                    CharacterTestCharacterSourceRevisionComponent.InitialRevision,
                    source.SourcePoseRevision);
                Assert.AreEqual(
                    CharacterTestCharacterSourceRevisionComponent.InitialRevision,
                    source.SourceFacingRevision);
                Assert.AreEqual(0, combatTrace.ActiveTraceCount);
                JObject applied = JObject.Parse(participant.CaptureSnapshot(slice.CurrentFrame).PayloadJson);
                Assert.AreEqual(1, ((JArray)applied["hitResolutionPolicy"]["acceptedEvents"]).Count);
                Assert.AreEqual(0, ((JArray)applied["hitResolutionPolicy"]["activeTraces"]).Count);
                Assert.AreEqual(0, ((JArray)applied["hitResolutionPolicy"]["instanceIds"]).Count);
                Assert.AreEqual(0, ((JArray)applied["hitResolutionPolicy"]["hitOnceKeys"]).Count);
                Assert.AreEqual(0, ((JArray)applied["hitResolutionPolicy"]["bodyHitVolumePreviousCenters"]).Count);

                source.EnqueueCombatRequest(CreateActiveHitWindowRequest(
                    slice.CurrentFrame,
                    source.Id.Value,
                    instanceId: 2402L,
                    stableEventId: CharacterTestAuthoredContentIds.D401_Id161,
                    lifecyclePhase: CharacterActionTrackLifecyclePhase.Enter,
                    lifecycleBoundary: CharacterActionTrackLifecycleBoundary.BeforeFrame));
                Tick(slice, 1);
                Assert.AreEqual(1, combatTrace.ActiveTraceCount);
                source.RestoreSourceRevisions(77, 66);
                NetworkSnapshotApplyResult rollback = participant.ApplyRollbackSnapshot(
                    slice.CurrentFrame,
                    schema24,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(rollback.Success, rollback.Message);
                Assert.AreEqual(
                    CharacterTestCharacterSourceRevisionComponent.InitialRevision,
                    source.SourcePoseRevision);
                Assert.AreEqual(
                    CharacterTestCharacterSourceRevisionComponent.InitialRevision,
                    source.SourceFacingRevision);
                Assert.AreEqual(0, combatTrace.ActiveTraceCount);
                JObject rolledBack = JObject.Parse(participant.CaptureSnapshot(slice.CurrentFrame).PayloadJson);
                Assert.AreEqual(1, ((JArray)rolledBack["hitResolutionPolicy"]["acceptedEvents"]).Count);
                Assert.AreEqual(0, ((JArray)rolledBack["hitResolutionPolicy"]["activeTraces"]).Count);
                Assert.AreEqual(0, ((JArray)rolledBack["hitResolutionPolicy"]["bodyHitVolumePreviousCenters"]).Count);
            }
        }

        [Test]
        public void NetworkSnapshot_RejectsDifferentSourceInstancesMappedToOneActionInstance()
        {
            var state = new CharacterTestHitTraceWindowState();
            CharacterTestHitResolutionPolicyNetworkSnapshot snapshot = state.CaptureNetworkSnapshot();
            snapshot.InstanceIds = new[]
            {
                new CharacterTestHitTraceInstanceNetworkSnapshot
                {
                    SourceInstanceId = 10L,
                    ActionInstanceId = 7
                },
                new CharacterTestHitTraceInstanceNetworkSnapshot
                {
                    SourceInstanceId = 11L,
                    ActionInstanceId = 7
                }
            };
            snapshot.NextInstanceId = 8;

            Assert.IsFalse(state.TryRestoreNetworkSnapshot(snapshot, null, out string failure));
            StringAssert.Contains("multiple source instance ids", failure);
        }

        [Test]
        public void NetworkSnapshot_RestoresDirectHitServiceDamageDuplicateLedger()
        {
            using (GameSlice slice = CreateSlice())
            {
                Tick(slice, 8);
                CharacterTestCharacterCombatTraceSystem combatTrace =
                    slice.CharacterRuntime.CombatTraceSystem;
                CharacterTestHitResolutionPolicyState policy = combatTrace.HitResolutionPolicyState;
                CombatHitEventConfig config = CreateOncePerEventConfig(maxAcceptedHits: 2);
                DirectHitNetworkFixture fixture = CreateDirectHitNetworkFixture(config);
                var service = new CharacterTestDirectHitResolutionService();
                var results = new List<HitResolveResult>(1);
                RuntimeFrame frame = slice.CurrentFrame;

                results.Clear();
                service.Resolve(
                    new[] { fixture.Candidates[0] },
                    fixture.Payloads,
                    fixture.PolicyEventIds,
                    policy,
                    results,
                    teamRelations: null);
                Assert.AreEqual(HitResolveKind.Damage, results[0].Kind);

                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeCustomState snapshot = participant.CaptureSnapshot(frame);

                results.Clear();
                service.Resolve(
                    new[] { fixture.Candidates[1] },
                    fixture.Payloads,
                    fixture.PolicyEventIds,
                    policy,
                    results,
                    teamRelations: null);
                HitResolveKind originalDuplicateKind = results[0].Kind;
                results.Clear();
                service.Resolve(
                    new[] { fixture.Candidates[2] },
                    fixture.Payloads,
                    fixture.PolicyEventIds,
                    policy,
                    results,
                    teamRelations: null);
                HitResolveKind originalNewTargetKind = results[0].Kind;
                Assert.AreEqual(HitResolveKind.Duplicate, originalDuplicateKind);
                Assert.AreEqual(HitResolveKind.Damage, originalNewTargetKind);

                NetworkSnapshotApplyResult apply = participant.ApplySnapshot(frame, snapshot);
                Assert.IsTrue(apply.Success, apply.Message);

                results.Clear();
                service.Resolve(
                    new[] { fixture.Candidates[1] },
                    fixture.Payloads,
                    fixture.PolicyEventIds,
                    policy,
                    results,
                    teamRelations: null);
                Assert.AreEqual(originalDuplicateKind, results[0].Kind);
                results.Clear();
                service.Resolve(
                    new[] { fixture.Candidates[2] },
                    fixture.Payloads,
                    fixture.PolicyEventIds,
                    policy,
                    results,
                    teamRelations: null);
                Assert.AreEqual(originalNewTargetKind, results[0].Kind);
            }
        }

        [Test]
        public void NetworkHash_OwnedAndNonOwnedPartitionsFollowAttackerSource()
        {
            using (GameSlice slice = CreateSlice())
            {
                Tick(slice, 8);
                CharacterTestHitResolutionPolicyState policy =
                    slice.CharacterRuntime.CombatTraceSystem.HitResolutionPolicyState;
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame frame = slice.CurrentFrame;
                long ownedBaseline = participant.ComputeOwnedStateHash(frame, ownedEntityId: 1);
                long nonOwnedBaseline = participant.ComputeNonOwnedStateHash(frame, ownedEntityId: 1);
                CombatHitEventConfig config = CreateOncePerEventConfig(maxAcceptedHits: 2);

                Assert.IsTrue(policy.TryAccept(
                    CreateCandidate(
                        attacker: CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1,
                        target: CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 2,
                        actionInstance: 31,
                        traceId: 1),
                    policyEventId: 9,
                    config));
                long ownedAfterLocalSource = participant.ComputeOwnedStateHash(frame, ownedEntityId: 1);
                long nonOwnedAfterLocalSource = participant.ComputeNonOwnedStateHash(frame, ownedEntityId: 1);
                Assert.AreNotEqual(ownedBaseline, ownedAfterLocalSource);
                Assert.AreEqual(nonOwnedBaseline, nonOwnedAfterLocalSource);

                Assert.IsTrue(policy.TryAccept(
                    CreateCandidate(
                        attacker: CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 2,
                        target: CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 3,
                        actionInstance: 32,
                        traceId: 2),
                    policyEventId: 10,
                    config));
                long ownedAfterRemoteSource = participant.ComputeOwnedStateHash(frame, ownedEntityId: 1);
                long nonOwnedAfterRemoteSource = participant.ComputeNonOwnedStateHash(frame, ownedEntityId: 1);
                Assert.AreEqual(ownedAfterLocalSource, ownedAfterRemoteSource);
                Assert.AreNotEqual(nonOwnedAfterLocalSource, nonOwnedAfterRemoteSource);
            }
        }

        [Test]
        public void NetworkSnapshot_RestoresRealActiveHitWindowMidFrameAndContinuesLifecycle()
        {
            var startupSpecs = new[]
            {
                new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 1f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.None)
            };

            using (GameSlice slice = new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: CreateNetworkTestPhysicsRuntime(),
                createCharacterViews: false,
                enableLocalInputCommands: false,
                startupCharacterSpawnSpecs: startupSpecs))
            {
                Tick(slice, 8);
                CharacterTestCharacterEntity source =
                    slice.Services.CharacterRuntime.FindEntityById(1);
                Assert.NotNull(source);
                CharacterTestCharacterCombatTraceSystem combatTrace =
                    slice.Services.CombatTraceSystem;
                RuntimeFrame frame = slice.CurrentFrame;

                source.EnqueueCombatRequest(CreateActiveHitWindowRequest(
                    frame,
                    source.Id.Value,
                    instanceId: 1001L,
                    stableEventId: CharacterTestAuthoredContentIds.D401_Id161,
                    lifecyclePhase: CharacterActionTrackLifecyclePhase.Enter,
                    lifecycleBoundary: CharacterActionTrackLifecycleBoundary.BeforeFrame));
                Tick(slice, 1);
                Assert.AreEqual(1, combatTrace.ActiveTraceCount);
                frame = slice.CurrentFrame;

                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeCustomState captured = participant.CaptureSnapshot(frame);
                JObject capturedDocument = JObject.Parse(captured.PayloadJson);
                JArray capturedTraces =
                    (JArray)capturedDocument["hitResolutionPolicy"]["activeTraces"];
                JArray capturedInstances =
                    (JArray)capturedDocument["hitResolutionPolicy"]["instanceIds"];
                Assert.AreEqual(1, capturedTraces.Count);
                Assert.AreEqual(1, capturedInstances.Count);
                Assert.AreEqual(1, (int)capturedTraces[0]["hitWindowId"]);
                Assert.AreEqual(1, (int)capturedInstances[0]["actionInstanceId"]);

                // Advance a same-frame branch with a second real Enter event and
                // a policy hit. Apply must remove both branch additions.
                source.EnqueueCombatRequest(CreateActiveHitWindowRequest(
                    frame,
                    source.Id.Value,
                    instanceId: 1002L,
                    stableEventId: CharacterTestAuthoredContentIds.D401_Id161,
                    lifecyclePhase: CharacterActionTrackLifecyclePhase.Enter,
                    lifecycleBoundary: CharacterActionTrackLifecycleBoundary.BeforeFrame));
                Tick(slice, 1);
                Assert.AreEqual(2, combatTrace.ActiveTraceCount);
                Assert.IsTrue(combatTrace.HitResolutionPolicyState.TryAccept(
                    CreateCandidate(
                        attacker: CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1,
                        target: CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 2,
                        actionInstance: 1,
                        traceId: 71),
                    policyEventId: 17,
                    CreateOncePerEventConfig(maxAcceptedHits: 2)));
                RuntimeFrame branchFrame = slice.CurrentFrame;
                long branchHash = participant.ComputeStateHash(branchFrame);

                NetworkSnapshotApplyResult apply = participant.ApplySnapshot(branchFrame, captured);
                Assert.IsTrue(apply.Success, apply.Message);
                Assert.AreEqual(1, combatTrace.ActiveTraceCount);
                long restoredHash = participant.ComputeStateHash(branchFrame);
                Assert.AreNotEqual(branchHash, restoredHash);
                RuntimeCustomState restored = participant.CaptureSnapshot(branchFrame);
                JObject restoredDocument = JObject.Parse(restored.PayloadJson);
                Assert.IsTrue(JToken.DeepEquals(
                    capturedDocument["hitResolutionPolicy"],
                    restoredDocument["hitResolutionPolicy"]));

                // The restored window remains live for the next combat tick;
                // its real Exit event then closes only the captured window.
                Tick(slice, 1);
                Assert.AreEqual(1, combatTrace.ActiveTraceCount);
                source = slice.Services.CharacterRuntime.FindEntityById(1);
                frame = slice.CurrentFrame;
                source.EnqueueCombatRequest(CreateActiveHitWindowRequest(
                    frame,
                    source.Id.Value,
                    instanceId: 1001L,
                    stableEventId: CharacterTestAuthoredContentIds.D401_Id161,
                    lifecyclePhase: CharacterActionTrackLifecyclePhase.Exit,
                    lifecycleBoundary: CharacterActionTrackLifecycleBoundary.AfterFrame));
                Tick(slice, 1);
                Assert.AreEqual(0, combatTrace.ActiveTraceCount);
            }
        }

        [Test]
        public void NetworkSnapshot_RestoresRealCasterBodySweepAgainstUnbranchedBaseline()
        {
            var startupSpecs = new[]
            {
                new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 1f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.None),
                new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 2,
                    x: 0.55f,
                    y: 1f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.None)
            };

            CharacterTestCharacterPhysicsRuntime baselinePhysics = CreateNetworkTestPhysicsRuntime();
            CharacterTestCharacterPhysicsRuntime branchPhysics = CreateNetworkTestPhysicsRuntime();
            using (GameSlice baseline = CreateNetworkSlice(startupSpecs, baselinePhysics))
            using (GameSlice branch = CreateNetworkSlice(startupSpecs, branchPhysics))
            {
                Tick(baseline, 8);
                Tick(branch, 8);
                CharacterTestCharacterEntity baselineSource =
                    baseline.Services.CharacterRuntime.FindEntityById(1);
                CharacterTestCharacterEntity baselineTarget =
                    baseline.Services.CharacterRuntime.FindEntityById(2);
                CharacterTestCharacterEntity branchSource =
                    branch.Services.CharacterRuntime.FindEntityById(1);
                CharacterTestCharacterEntity branchTarget =
                    branch.Services.CharacterRuntime.FindEntityById(2);
                Assert.NotNull(baselineSource);
                Assert.NotNull(baselineTarget);
                Assert.NotNull(branchSource);
                Assert.NotNull(branchTarget);

                RuntimeFrame frame = baseline.CurrentFrame;
                baselineSource.EnqueueCombatRequest(CreateActiveHitWindowRequest(
                    frame,
                    baselineSource.Id.Value,
                    instanceId: 3001L,
                    stableEventId: CharacterTestAuthoredContentIds.D401_Id161,
                    lifecyclePhase: CharacterActionTrackLifecyclePhase.Enter,
                    lifecycleBoundary: CharacterActionTrackLifecycleBoundary.BeforeFrame,
                    casterBody: true));
                branchSource.EnqueueCombatRequest(CreateActiveHitWindowRequest(
                    frame,
                    branchSource.Id.Value,
                    instanceId: 3001L,
                    stableEventId: CharacterTestAuthoredContentIds.D401_Id161,
                    lifecyclePhase: CharacterActionTrackLifecyclePhase.Enter,
                    lifecycleBoundary: CharacterActionTrackLifecycleBoundary.BeforeFrame,
                    casterBody: true));
                Tick(baseline, 1);
                Tick(branch, 1);

                CharacterTestCharacterCombatTraceSystem baselineCombat =
                    baseline.Services.CombatTraceSystem;
                CharacterTestCharacterCombatTraceSystem branchCombat =
                    branch.Services.CombatTraceSystem;
                Assert.AreEqual(1, baselineCombat.ActiveTraceCount);
                Assert.AreEqual(1, branchCombat.ActiveTraceCount);
                Assert.Greater(branchCombat.LastCandidateCount, 0, "branch CasterBody candidate count");
                Assert.Greater(baselineCombat.LastCandidateCount, 0, "baseline CasterBody candidate count");
                Assert.Greater(baselineCombat.LastResolvedHitCount, 0, "baseline CasterBody resolved hit count");
                Assert.Greater(branchCombat.LastResolvedHitCount, 0, "branch CasterBody resolved hit count");
                Assert.Greater(baselineSource.LastCombatHitCount, 0, "baseline CasterBody hit count");
                Assert.Greater(branchSource.LastCombatHitCount, 0, "branch CasterBody hit count");
                AssertResolvedDamageOrDuplicate(baselineSource.LastCombatHitResults, "baseline initial CasterBody hit");
                AssertResolvedDamageOrDuplicate(branchSource.LastCombatHitResults, "branch initial CasterBody hit");
                Assert.Greater(
                    baselineTarget.CombatState.Health,
                    0,
                    "baseline initial target health=" + baselineTarget.CombatState.Health
                        + " damage=" + baselineSource.LastCombatHitResults[0].Damage
                        + " attribute=" + baseline.Services.CharacterRuntime.World.GetCurrentAttribute(
                            baselineTarget,
                            CharacterTestCharacterAttributeIds.Hp));
                Assert.Greater(
                    branchTarget.CombatState.Health,
                    0,
                    "branch initial target health=" + branchTarget.CombatState.Health
                        + " damage=" + branchSource.LastCombatHitResults[0].Damage
                        + " attribute=" + branch.Services.CharacterRuntime.World.GetCurrentAttribute(
                            branchTarget,
                            CharacterTestCharacterAttributeIds.Hp));

                CharacterTestNetworkSnapshotParticipant branchParticipant =
                    branch.Services.Network.CreateSnapshotParticipant();
                RuntimeCustomState captured = branchParticipant.CaptureSnapshot(branch.CurrentFrame);
                JObject capturedDocument = JObject.Parse(captured.PayloadJson);
                JArray capturedCenters =
                    (JArray)capturedDocument["hitResolutionPolicy"]["bodyHitVolumePreviousCenters"];
                Assert.AreEqual(1, capturedCenters.Count);
                Assert.AreEqual(1, (int)capturedCenters[0]["entityId"]);
                Assert.AreEqual(1, (int)capturedCenters[0]["hitWindowId"]);

                // Baseline advances normally. The branch moves the real target,
                // opens a second real CasterBody window, and therefore perturbs
                // the center map and actual hit results before rollback.
                baselineSource.SetTransform(new CharacterTestCharacterTransformComponent(0.2f, 1f, 0f));
                Tick(baseline, 1);

                branchSource.SetTransform(new CharacterTestCharacterTransformComponent(0.2f, 1f, 0f));
                branchTarget.SetTransform(new CharacterTestCharacterTransformComponent(0.7f, 1f, 0f));
                branchSource.EnqueueCombatRequest(CreateActiveHitWindowRequest(
                    branch.CurrentFrame,
                    branchSource.Id.Value,
                    instanceId: 3002L,
                    stableEventId: CharacterTestAuthoredContentIds.D401_Id161,
                    lifecyclePhase: CharacterActionTrackLifecyclePhase.Enter,
                    lifecycleBoundary: CharacterActionTrackLifecycleBoundary.BeforeFrame,
                    casterBody: true));
                Tick(branch, 1);
                Assert.AreEqual(2, branchCombat.ActiveTraceCount);
                Assert.Greater(branchCombat.LastCandidateCount, 0);
                Assert.Greater(branchSource.LastCombatHitCount, 0);
                long branchHash = branchParticipant.ComputeStateHash(branch.CurrentFrame);

                NetworkSnapshotApplyResult baselineApply =
                    baseline.Services.Network.CreateSnapshotParticipant().ApplySnapshot(
                        baseline.CurrentFrame,
                        captured);
                Assert.IsTrue(baselineApply.Success, baselineApply.Message);
                NetworkSnapshotApplyResult branchApply =
                    branchParticipant.ApplyRollbackSnapshot(
                        branch.CurrentFrame,
                        captured,
                        preserveLocalPredictedEntity: false);
                Assert.IsTrue(branchApply.Success, branchApply.Message);
                Assert.AreEqual(1, baselineCombat.ActiveTraceCount);
                Assert.AreEqual(1, branchCombat.ActiveTraceCount);
                Assert.AreNotEqual(branchHash, branchParticipant.ComputeStateHash(branch.CurrentFrame));
                Assert.AreEqual(
                    baseline.Services.Network.CreateSnapshotParticipant().ComputeStateHash(baseline.CurrentFrame),
                    branchParticipant.ComputeStateHash(branch.CurrentFrame));

                // Both peers now continue from the same captured previous center
                // and real active target. The next physical query/sample and the
                // damage/duplicate result must be identical.
                baselineSource = baseline.Services.CharacterRuntime.FindEntityById(1);
                baselineTarget = baseline.Services.CharacterRuntime.FindEntityById(2);
                branchSource = branch.Services.CharacterRuntime.FindEntityById(1);
                branchTarget = branch.Services.CharacterRuntime.FindEntityById(2);
                baselineSource.SetTransform(new CharacterTestCharacterTransformComponent(0.2f, 1f, 0f));
                baselineTarget.SetTransform(new CharacterTestCharacterTransformComponent(0.55f, 1f, 0f));
                branchSource.SetTransform(new CharacterTestCharacterTransformComponent(0.2f, 1f, 0f));
                branchTarget.SetTransform(new CharacterTestCharacterTransformComponent(0.55f, 1f, 0f));
                Tick(baseline, 1);
                Tick(branch, 1);

                Assert.AreEqual(baselineCombat.LastCandidateCount, branchCombat.LastCandidateCount);
                Assert.AreEqual(baselineCombat.LastResolvedHitCount, branchCombat.LastResolvedHitCount);
                Assert.Greater(baselineCombat.LastResolvedHitCount, 0, "baseline rolled-back CasterBody resolved hit count");
                Assert.Greater(branchCombat.LastResolvedHitCount, 0, "branch rolled-back CasterBody resolved hit count");
                AssertResolvedDamageOrDuplicate(
                    baselineSource.LastCombatHitResults,
                    "baseline rolled-back CasterBody hit");
                AssertResolvedDamageOrDuplicate(
                    branchSource.LastCombatHitResults,
                    "branch rolled-back CasterBody hit");
                AssertHitResultsEqual(baselineSource.LastCombatHitResults, branchSource.LastCombatHitResults);
                AssertCasterBodySamplesEqual(
                    CaptureCasterBodySamples(baselineCombat),
                    CaptureCasterBodySamples(branchCombat));
            }
        }

        private static void AddPolicyEntries(
            CharacterTestHitResolutionPolicyState policy,
            int[] order)
        {
            CombatHitEventConfig config = CreateOncePerEventConfig(maxAcceptedHits: 4);
            int[] attackers = { 3, 1, 2, 1, 3, 2 };
            int[] actionInstances = { 41, 12, 27, 9, 4, 33 };
            int[] policyEvents = { 4, 8, 2, 5, 1, 7 };
            int[] targets = { 6, 2, 9, 3, 5, 8 };
            for (int i = 0; i < order.Length; i++)
            {
                int index = order[i];
                Assert.IsTrue(policy.TryAccept(
                    CreateCandidate(
                        attackers[index],
                        targets[index],
                        actionInstances[index],
                        traceId: index + 1),
                    policyEvents[index],
                    config));
            }
        }

        private static DirectHitNetworkFixture CreateDirectHitNetworkFixture(
            CombatHitEventConfig hitEvent)
        {
            CombatEntityId attackerId = new CombatEntityId(101);
            CombatEntityId targetId = new CombatEntityId(102);
            CombatEntityId secondTargetId = new CombatEntityId(103);
            CombatPhysicsLayerMask layerMask = CombatPhysicsLayerMask.FromLayer(
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer);
            CombatQueryHeader query = new CombatQueryHeader(
                queryId: 1,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: attackerId,
                traceId: 71,
                actionId: 7,
                sourceOrder: 0,
                layerMask: layerMask);
            CombatQueryResult firstHit = new CombatQueryResult(
                query,
                targetId,
                new CombatBodyId(50002),
                new CombatColliderId(1002),
                Fix64.Zero,
                new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
            CombatQueryResult secondHit = new CombatQueryResult(
                query,
                targetId,
                new CombatBodyId(50002),
                new CombatColliderId(1002),
                Fix64.FromInt(1),
                new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
            CombatQueryResult thirdHit = new CombatQueryResult(
                query,
                secondTargetId,
                new CombatBodyId(50003),
                new CombatColliderId(1003),
                Fix64.FromInt(1),
                new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero));
            var candidates = new List<HitCandidate>
            {
                CreateCandidate(attackerId, targetId, 42, 71, 4, firstHit),
                CreateCandidate(attackerId, targetId, 42, 71, 5, secondHit),
                CreateCandidate(attackerId, secondTargetId, 42, 71, 6, thirdHit)
            };
            var trace = new CharacterTestActiveHitTrace(
                entityId: attackerId.Value,
                actionId: new CharacterActionId(1201610),
                sourceInstanceId: 1,
                sourceActionInstanceId: 42,
                hitWindowId: 42,
                equipmentSlotId: 0,
                traceProfileId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0039,
                damagePayload: default(CharacterActionDamagePayload),
                hitEvent: hitEvent,
                hitVolume: default(CharacterActionHitVolumeConfig),
                stableEventId: hitEvent.StableEventId,
                startFrame: 0);
            var payloads = new Dictionary<CharacterTestTracePayloadKey, CharacterTestTracePayload>
            {
                [new CharacterTestTracePayloadKey(42, 71)] = new CharacterTestTracePayload(
                    trace,
                    default(CharacterTestWeaponTraceBinding),
                    string.Empty,
                    localFrame: 0)
            };
            var policyEventIds = new Dictionary<CombatQueryHeader, int>
            {
                [query] = 701
            };
            return new DirectHitNetworkFixture(candidates, payloads, policyEventIds);
        }

        private static HitCandidate CreateCandidate(
            CombatEntityId attacker,
            CombatEntityId target,
            int actionInstance,
            int traceId,
            int frame,
            CombatQueryResult queryResult)
        {
            return new HitCandidate(
                attacker,
                target,
                actionId: 7,
                actionInstanceId: actionInstance,
                traceId: traceId,
                frame: new CombatFrame(frame),
                physicsHit: queryResult,
                damage: 0,
                staggerFrames: 0,
                knockback: FixVector3.Zero,
                targetState: HitTargetStateFlags.Alive);
        }

        private sealed class DirectHitNetworkFixture
        {
            internal DirectHitNetworkFixture(
                List<HitCandidate> candidates,
                Dictionary<CharacterTestTracePayloadKey, CharacterTestTracePayload> payloads,
                Dictionary<CombatQueryHeader, int> policyEventIds)
            {
                Candidates = candidates;
                Payloads = payloads;
                PolicyEventIds = policyEventIds;
            }

            internal List<HitCandidate> Candidates { get; }
            internal Dictionary<CharacterTestTracePayloadKey, CharacterTestTracePayload> Payloads { get; }
            internal Dictionary<CombatQueryHeader, int> PolicyEventIds { get; }
        }

        private static CombatHitEventConfig CreateOncePerEventConfig(int maxAcceptedHits)
        {
            return new CombatHitEventConfig(
                CombatHitTargetTypes.HostileCharacter,
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0010,
                maxAcceptedHits: maxAcceptedHits,
                hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent);
        }

        private static CharacterActionCombatRequest CreateActiveHitWindowRequest(
            RuntimeFrame frame,
            int sourceEntityId,
            long instanceId,
            int stableEventId,
            CharacterActionTrackLifecyclePhase lifecyclePhase,
            CharacterActionTrackLifecycleBoundary lifecycleBoundary,
            bool casterBody = false)
        {
            var metadata = new CharacterActionAdapterDispatchMetadata(
                frame,
                instanceId,
                planId: 2001L,
                actionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("cd-m1.network.active-action"),
                localFrame: (int)frame.Value,
                stableEventId: stableEventId,
                sourceId: sourceEntityId,
                traceId: new RuntimeTraceIdentity(941847),
                lifecyclePhase: lifecyclePhase,
                lifecycleBoundary: lifecycleBoundary);
            var damagePayload = new CharacterActionDamagePayload(
                damageTypeId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D409_Id2,
                formulaId: DamageFormulaIds.Flat,
                baseDamage: 10,
                sourceAttributeScales: null,
                impactPower: 1,
                tags: new[] { 1201611, 1201612 },
                analysisCategory: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0056,
                presentationProfileId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0009,
                launchPower: 0);
            var hitEvent = new CombatHitEventConfig(
                CombatHitTargetTypes.HostileCharacter,
                stableEventId: stableEventId,
                maxAcceptedHits: 2,
                hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent,
                damage: new CombatHitDamageConfig(
                    damageTypeId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D409_Id2,
                    baseDamage: 10,
                    impactPower: 1,
                    tags: new[] { 1201611, 1201612 },
                    analysisCategory: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0056));
            CharacterActionHitVolumeConfig hitVolume = casterBody
                ? CharacterActionHitVolumeConfig.CasterBody(
                    0f,
                    1f,
                    0f,
                    2f,
                    0f,
                    1f,
                    0f)
                : CharacterActionHitVolumeConfig.Capsule(
                    0f,
                    0.8f,
                    0f,
                    0f,
                    1.2f,
                    0.6f,
                    0.25f);
            return new CharacterActionCombatRequest(
                metadata,
                new CombatEntityId(
                    CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + sourceEntityId),
                CharacterActionTrackEventKind.HitTraceWindow,
                combatActionId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D412_Id5,
                traceProfileId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986,
                equipmentSlotId: CharacterTestAuthoredContentIds.D405_Id1,
                damagePayload: damagePayload,
                hitEvent: hitEvent,
                hitVolume: hitVolume);
        }

        private static HitCandidate CreateCandidate(
            int attacker,
            int target,
            int actionInstance,
            int traceId)
        {
            return new HitCandidate(
                new CombatEntityId(attacker),
                new CombatEntityId(target),
                actionId: 1,
                actionInstanceId: actionInstance,
                traceId: traceId,
                frame: default(CombatFrame),
                physicsHit: default(CombatQueryResult),
                damage: 10,
                staggerFrames: 0,
                knockback: FixVector3.Zero,
                targetState: HitTargetStateFlags.Alive);
        }

        private static List<CharacterTestDirectHitTracePlanSample> CaptureCasterBodySamples(
            CharacterTestCharacterCombatTraceSystem combat)
        {
            var samples = new List<CharacterTestDirectHitTracePlanSample>();
            CharacterTestDirectHitTracePlanCollectionView plans = combat.DirectHitTracePlans;
            for (int planIndex = 0; planIndex < plans.Count; planIndex++)
            {
                if (!plans.TryGetPlan(planIndex, out CharacterTestDirectHitTracePlanView plan))
                    continue;
                for (int sampleIndex = 0; sampleIndex < plan.Count; sampleIndex++)
                {
                    if (plan.TryGetSample(sampleIndex, out CharacterTestDirectHitTracePlanSample sample)
                        && sample.Kind == CharacterTestDirectHitTraceSampleKind.CasterBody)
                    {
                        samples.Add(sample);
                    }
                }
            }
            return samples;
        }

        private static void AssertCasterBodySamplesEqual(
            IReadOnlyList<CharacterTestDirectHitTracePlanSample> expected,
            IReadOnlyList<CharacterTestDirectHitTracePlanSample> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                CharacterTestDirectHitTracePlanSample left = expected[i];
                CharacterTestDirectHitTracePlanSample right = actual[i];
                Assert.AreEqual(left.Kind, right.Kind, "sample kind index=" + i);
                Assert.AreEqual(left.LocalFrame, right.LocalFrame, "sample local frame index=" + i);
                Assert.AreEqual(left.SampleOrder, right.SampleOrder, "sample order index=" + i);
                Assert.AreEqual(left.QueryOffset, right.QueryOffset, "sample query offset index=" + i);
                Assert.AreEqual(left.HitWindowId, right.HitWindowId, "sample hit window index=" + i);
                Assert.AreEqual(left.ActionId, right.ActionId, "sample action index=" + i);
                Assert.AreEqual(left.TraceId, right.TraceId, "sample trace index=" + i);
                Assert.AreEqual(left.PolicyEventId, right.PolicyEventId, "sample policy event index=" + i);
                Assert.AreEqual(left.Query.Header, right.Query.Header, "sample query header index=" + i);
                Assert.AreEqual(left.Query.PointA, right.Query.PointA, "sample point A index=" + i);
                Assert.AreEqual(left.Query.PointB, right.Query.PointB, "sample point B index=" + i);
                Assert.AreEqual(left.Query.Radius, right.Query.Radius, "sample radius index=" + i);
            }
        }

        private static void AssertHitResultsEqual(
            IReadOnlyList<HitResolveResult> expected,
            IReadOnlyList<HitResolveResult> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
                Assert.AreEqual(expected[i], actual[i], "hit result index=" + i);
        }

        private static void AssertResolvedDamageOrDuplicate(
            IReadOnlyList<HitResolveResult> results,
            string context)
        {
            Assert.IsNotNull(results, context + " results");
            Assert.Greater(results.Count, 0, context + " result count");
            for (int i = 0; i < results.Count; i++)
            {
                HitResolveResult result = results[i];
                Assert.IsTrue(
                    result.Kind == HitResolveKind.Damage
                    || result.Kind == HitResolveKind.Duplicate,
                    context + " kind index=" + i
                        + " actual=" + result.Kind
                        + " damage=" + result.Damage);
                if (result.Kind == HitResolveKind.Damage)
                    Assert.Greater(result.Damage, 0, context + " damage index=" + i);
            }
        }

        private static GameSlice CreateSlice()
        {
            return new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableLocalInputCommands: false);
        }

        private static GameSlice CreateNetworkSlice(CharacterTestCharacterSpawnSpec[] startupSpecs)
        {
            return CreateNetworkSlice(startupSpecs, CreateNetworkTestPhysicsRuntime());
        }

        private static GameSlice CreateNetworkSlice(
            CharacterTestCharacterSpawnSpec[] startupSpecs,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            return new GameSlice(
                NullRuntimeLogger.Instance,
                physicsRuntime: physicsRuntime,
                createCharacterViews: false,
                enableLocalInputCommands: false,
                startupCharacterSpawnSpecs: startupSpecs);
        }

        private static CharacterTestCharacterPhysicsRuntime CreateNetworkTestPhysicsRuntime()
        {
            return new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer),
                new FixVector3(
                    Fix64.FromRatio(45, 100),
                    Fix64.FromRatio(90, 100),
                    Fix64.FromRatio(45, 100)),
                Fix64.Zero,
                Fix64.Zero,
                staticColliderCount: 1,
                sourceDescription: "cd-m1 active-window network test");
        }

        private static void Tick(GameSlice slice, int count)
        {
            for (int i = 0; i < count; i++)
                slice.Tick(1d / 60d);
        }
    }
}

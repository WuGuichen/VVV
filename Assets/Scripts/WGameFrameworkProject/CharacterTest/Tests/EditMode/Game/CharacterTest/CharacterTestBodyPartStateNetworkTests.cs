using System;
using System.Diagnostics;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestBodyPartStateNetworkTests
    {
        [Test]
        public void NumericBodyPartHashPreservesANullEntityBoundary()
        {
            var empty = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, 1);
            var absentEntity = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, 1);
            CharacterTestBodyPartStateNetworkSnapshotCodec.AddStateHash(empty,
                Array.Empty<CharacterTestCharacterEntity>());
            CharacterTestBodyPartStateNetworkSnapshotCodec.AddStateHash(absentEntity,
                new CharacterTestCharacterEntity[] { null });
            Assert.DoesNotThrow(() => absentEntity.ToHash());
            Assert.AreNotEqual(empty.ToHash(), absentEntity.ToHash());
        }

        [Test]
        public void Schema31_CaptureCanonicalCloneAndOwnershipHashes_CoverAllAuthorityFields()
        {
            using (GameSlice slice = CreateSlice(spawnDragon: true))
            {
                TickUntilSpawned(slice);
                CharacterTestCharacterEntity entity = slice.CharacterEntities[0];
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame frame = slice.CurrentFrame;
                long baselineFull = participant.ComputeStateHash(frame);
                long baselineOwned = participant.ComputeOwnedStateHash(frame, entity.Id.Value);
                long baselineNonOwned = participant.ComputeNonOwnedStateHash(frame, entity.Id.Value);

                CommitDamage(entity, frame.Value, eventKey: 1, finalDamage: 7);

                Assert.AreNotEqual(baselineFull, participant.ComputeStateHash(frame));
                Assert.AreNotEqual(
                    baselineOwned,
                    participant.ComputeOwnedStateHash(frame, entity.Id.Value));
                Assert.AreEqual(
                    baselineNonOwned,
                    participant.ComputeNonOwnedStateHash(frame, entity.Id.Value));

                RuntimeCustomState wire = participant.CaptureSnapshot(frame);
                Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                    wire.SchemaVersion);
                JObject json = JObject.Parse(wire.PayloadJson);
                JArray states = (JArray)json["bodyPartStates"];
                Assert.NotNull(states);
                Assert.AreEqual(1, states.Count);
                Assert.AreEqual(entity.Id.Value, (int)states[0]["entityId"]);
                Assert.AreEqual(entity.BodyPartState.Revision, (long)states[0]["componentRevision"]);
                Assert.AreEqual(
                    entity.BodyPartState.DefinitionSourceContentHash,
                    (string)states[0]["definitionSourceContentHash"]);

                CharacterTestNetworkSnapshotDocument canonical =
                    participant.CaptureRollbackDocument(frame);
                CharacterTestNetworkSnapshotDocument reordered = canonical.CloneForRollback();
                Array.Reverse(reordered.BodyPartStates[0].Entries);
                canonical.NormalizeForSchema(canonical.SchemaVersion);
                reordered.NormalizeForSchema(reordered.SchemaVersion);
                Assert.AreEqual(
                    JsonConvert.SerializeObject(canonical),
                    JsonConvert.SerializeObject(reordered),
                    "Canonical formal bytes must not depend on input entry order.");

                CharacterTestNetworkSnapshotDocument clone = canonical.CloneForRollback();
                Assert.AreNotSame(canonical.BodyPartStates, clone.BodyPartStates);
                Assert.AreNotSame(
                    canonical.BodyPartStates[0].Entries,
                    clone.BodyPartStates[0].Entries);
                Assert.AreEqual(
                    canonical.BodyPartStates[0].Entries[0].LastAcceptedHitFrame,
                    clone.BodyPartStates[0].Entries[0].LastAcceptedHitFrame);
            }
        }

        [Test]
        public void Schema32_SeveredCaptureCloneHashesAndLateJoinRestoreStateWithoutEventReplay()
        {
            using (GameSlice source = CreateSlice(spawnDragon: true))
            using (GameSlice lateJoin = CreateSlice(spawnDragon: false))
            {
                TickUntilSpawned(source);
                Tick(lateJoin, 8);
                CharacterTestCharacterEntity entity = source.CharacterEntities[0];
                CharacterTestNetworkSnapshotParticipant sourceParticipant =
                    source.Services.Network.CreateSnapshotParticipant();
                CharacterTestNetworkSnapshotParticipant lateJoinParticipant =
                    lateJoin.Services.Network.CreateSnapshotParticipant();
                int tailIndex = FindDefinitionIndex(
                    entity.BodyPartState,
                    "partstate.000003.tail");
                long breakFrame = source.CurrentFrame.Value;
                long severFrame = breakFrame + 1L;
                RuntimeFrame hashFrame = new RuntimeFrame(severFrame);
                long baselineFull = sourceParticipant.ComputeStateHash(hashFrame);
                long baselineOwned = sourceParticipant.ComputeOwnedStateHash(
                    hashFrame,
                    entity.Id.Value);
                long baselineNonOwned = sourceParticipant.ComputeNonOwnedStateHash(
                    hashFrame,
                    entity.Id.Value);

                CommitDamage(
                    entity,
                    breakFrame,
                    eventKey: 701,
                    finalDamage: int.MaxValue,
                    definitionIndex: tailIndex);
                CommitSever(entity, severFrame, tailIndex, correlationId: 702L);

                Assert.AreNotEqual(baselineFull, sourceParticipant.ComputeStateHash(hashFrame));
                Assert.AreNotEqual(
                    baselineOwned,
                    sourceParticipant.ComputeOwnedStateHash(hashFrame, entity.Id.Value));
                Assert.AreEqual(
                    baselineNonOwned,
                    sourceParticipant.ComputeNonOwnedStateHash(hashFrame, entity.Id.Value));
                Assert.IsTrue(entity.BodyPartState.TryGetCurrentEntry(
                    tailIndex,
                    out CharacterTestBodyPartStateEntry severed));
                Assert.AreEqual(CharacterTestBodyPartStructureState.Severed, severed.StructureState);

                RuntimeCustomState wire = sourceParticipant.CaptureSnapshot(hashFrame);
                Assert.AreEqual(
                    CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                    wire.SchemaVersion);
                JObject json = JObject.Parse(wire.PayloadJson);
                Assert.AreEqual(
                    (int)CharacterTestBodyPartStructureState.Severed,
                    (int)json["bodyPartStates"][0]["entries"][tailIndex]["structureState"]);
                Assert.AreEqual(
                    severed.Revision,
                    (long)json["bodyPartStates"][0]["entries"][tailIndex]["revision"]);

                CharacterTestNetworkSnapshotDocument clone =
                    sourceParticipant.CaptureRollbackDocument(hashFrame).CloneForRollback();
                Assert.AreEqual(
                    (int)CharacterTestBodyPartStructureState.Severed,
                    clone.BodyPartStates[0].Entries[tailIndex].StructureState);
                Assert.AreEqual(severed.Revision, clone.BodyPartStates[0].Entries[tailIndex].Revision);

                NetworkSnapshotApplyResult apply = lateJoinParticipant.ApplySnapshot(hashFrame, wire);
                Assert.IsTrue(apply.Success, apply.Message);
                CharacterTestCharacterEntity restored = lateJoin.CharacterEntities[0];
                Assert.IsTrue(restored.BodyPartState.TryGetCurrentEntry(
                    tailIndex,
                    out CharacterTestBodyPartStateEntry restoredTail));
                Assert.AreEqual(CharacterTestBodyPartStructureState.Severed, restoredTail.StructureState);
                Assert.AreEqual(severed.Revision, restoredTail.Revision);
                Assert.AreEqual(0, restored.BodyPartState.TransitionCount);
                Assert.AreEqual(
                    sourceParticipant.ComputeStateHash(hashFrame),
                    lateJoinParticipant.ComputeStateHash(hashFrame));
            }
        }

        [Test]
        public void Schema31_RawSeveredIntegerIsNotAcceptedAndNormalizesToBroken()
        {
            using (GameSlice slice = CreateSlice(spawnDragon: true))
            {
                TickUntilSpawned(slice);
                CharacterTestCharacterEntity entity = slice.CharacterEntities[0];
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                int tailIndex = FindDefinitionIndex(
                    entity.BodyPartState,
                    "partstate.000003.tail");
                long breakFrame = slice.CurrentFrame.Value;
                long severFrame = breakFrame + 1L;
                CommitDamage(
                    entity,
                    breakFrame,
                    eventKey: 711,
                    finalDamage: int.MaxValue,
                    definitionIndex: tailIndex);
                CommitSever(entity, severFrame, tailIndex, correlationId: 712L);

                CharacterTestNetworkSnapshotDocument legacy =
                    participant.CaptureRollbackDocument(new RuntimeFrame(severFrame));
                legacy.SchemaVersion =
                    CharacterTestNetworkSnapshotParticipant.BodyPartStateSchemaVersionValue;
                Assert.AreEqual(
                    (int)CharacterTestBodyPartStructureState.Severed,
                    legacy.BodyPartStates[0].Entries[tailIndex].StructureState);
                Assert.IsFalse(CharacterTestBodyPartStateNetworkSnapshotCodec.TryValidateDocumentShape(
                    legacy.SchemaVersion,
                    legacy.Frame,
                    legacy.Characters,
                    legacy.BodyPartStates,
                    out _),
                    "Schema 31 must not opt into Severed merely because enum integer 2 is defined.");

                legacy.NormalizeForSchema(legacy.SchemaVersion);
                Assert.AreEqual(
                    (int)CharacterTestBodyPartStructureState.Broken,
                    legacy.BodyPartStates[0].Entries[tailIndex].StructureState);
                NetworkSnapshotApplyResult apply = participant.ApplyRollbackDocument(
                    new RuntimeFrame(severFrame),
                    legacy,
                    preserveLocalPredictedEntity: false);

                Assert.IsTrue(apply.Success, apply.Message);
                Assert.IsTrue(entity.BodyPartState.TryGetCurrentEntry(
                    tailIndex,
                    out CharacterTestBodyPartStateEntry restored));
                Assert.AreEqual(CharacterTestBodyPartStructureState.Broken, restored.StructureState);
                Assert.AreNotEqual(CharacterTestBodyPartStructureState.Severed, restored.StructureState);
                Assert.AreEqual(0, entity.BodyPartState.TransitionCount);
            }
        }

        [Test]
        public void Schema32_NonSeverableDefinitionTamper_FailsBeforeCharacterBodyAuthorityOrHashMutation()
        {
            using (GameSlice slice = CreateSlice(spawnDragon: true))
            {
                TickUntilSpawned(slice);
                CharacterTestCharacterEntity entity = slice.CharacterEntities[0];
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame frame = slice.CurrentFrame;
                int nonSeverableIndex = FindNonSeverableDefinitionIndex(entity.BodyPartState);
                RuntimeCustomState valid = participant.CaptureSnapshot(frame);
                JObject payload = JObject.Parse(valid.PayloadJson);
                JToken entry = payload["bodyPartStates"]?[0]?["entries"]?[nonSeverableIndex];
                Assert.NotNull(entry);
                entry["currentIntegrity"] = 0;
                entry["structureState"] =
                    (int)CharacterTestBodyPartStructureState.Severed;
                var tampered = new RuntimeCustomState(
                    valid.NumericTypeId,
                    valid.TypeId,
                    valid.SchemaVersion,
                    payload.ToString(Formatting.None));

                CharacterTestBodyPartStateComponent beforeComponent = entity.BodyPartState;
                long beforeHash = participant.ComputeStateHash(frame);
                CharacterTestNetworkSnapshotDocument before =
                    participant.CaptureRollbackDocument(frame);
                string beforeCharacterAuthority = JsonConvert.SerializeObject(before.Characters);
                string beforeBodyAuthority = JsonConvert.SerializeObject(before.BodyPartStates);

                NetworkSnapshotApplyResult apply = participant.ApplySnapshot(frame, tampered);

                Assert.IsFalse(apply.Success);
                StringAssert.Contains("body-part snapshot entry", apply.Message);
                Assert.AreSame(entity, slice.CharacterEntities[0]);
                Assert.AreSame(beforeComponent, entity.BodyPartState);
                Assert.AreEqual(beforeHash, participant.ComputeStateHash(frame));
                CharacterTestNetworkSnapshotDocument after =
                    participant.CaptureRollbackDocument(frame);
                Assert.AreEqual(
                    beforeCharacterAuthority,
                    JsonConvert.SerializeObject(after.Characters));
                Assert.AreEqual(
                    beforeBodyAuthority,
                    JsonConvert.SerializeObject(after.BodyPartStates));
            }
        }

        [Test]
        public void Schema32_RealPhysicsLateJoinAndRollback_RebuildSeveredTopologyBeforePlannerPlusOne()
        {
            using (GameSlice source = CreatePhysicsNetworkSlice(spawnPair: true))
            using (GameSlice lateJoin = CreatePhysicsNetworkSlice(spawnPair: false))
            using (GameSlice rollback = CreatePhysicsNetworkSlice(spawnPair: false))
            {
                Tick(source, 8);
                Tick(lateJoin, 8);
                Tick(rollback, 8);
                Assert.AreEqual(2, source.CharacterEntities.Count);
                CharacterTestCharacterEntity attacker = source.CharacterEntities[0];
                CharacterTestCharacterEntity target = source.CharacterEntities[1];
                Assert.AreEqual(CharacterTestCharacterControlOwner.AiControlled, attacker.Control.Owner);
                Assert.AreEqual(2, target.Team.TeamId);
                Assert.IsTrue(source.CharacterRuntime.TryGetDamageGeometrySnapshot(
                    target.Id.Value,
                    out CharacterTestDamageGeometrySnapshot intactGeometry));
                Assert.IsTrue(source.CharacterRuntime.TryGetDamageZoneSnapshot(
                    target.Id.Value,
                    out CharacterTestDamageZoneSnapshot intactZones));
                Assert.AreEqual(18, intactGeometry.ColliderCount);
                Assert.IsTrue(intactZones.TryGetAvailableZone(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id154, out CharacterTestDamageZoneAggregate intactTail));
                Assert.IsTrue(intactZones.TryGetAvailableZone(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id151, out _));
                CharacterTestDamageZoneMember tailMember =
                    intactZones.GetMember(intactTail.MemberStart);
                Assert.IsTrue(source.CharacterRuntime.TryGetRuntimeGeometryBinding(
                    target.Id.Value,
                    out CharacterImportedGeometryBinding geometryBinding));
                CharacterEngagementAnchorRuntimeBinding tailAnchor =
                    FindAnchor(geometryBinding.EngagementAnchors, "anchor.tail.rear");
                Assert.NotNull(tailAnchor);
                CharacterTestNavigationSession navigation =
                    source.Services.Modules.NavigationSession;
                Assert.IsTrue(navigation.IsAvailable, navigation.BuildResult.Status.ToString());

                int tailIndex = FindDefinitionIndex(
                    target.BodyPartState,
                    "partstate.000003.tail");
                long breakFrame = source.CurrentFrame.Value;
                CommitDamage(
                    target,
                    breakFrame,
                    eventKey: 1201,
                    finalDamage: int.MaxValue,
                    definitionIndex: tailIndex);
                CommitSever(target, breakFrame + 1L, tailIndex, correlationId: 1202L);
                Tick(source, 1);
                RuntimeFrame restoreFrame = source.CurrentFrame;
                Assert.IsTrue(source.CharacterRuntime.TryGetDamageGeometrySnapshot(
                    target.Id.Value,
                    out CharacterTestDamageGeometrySnapshot sourceSeveredGeometry));
                Assert.AreEqual(15, sourceSeveredGeometry.ColliderCount);

                CharacterTestNetworkSnapshotParticipant sourceParticipant =
                    source.Services.Network.CreateSnapshotParticipant();
                CharacterTestNetworkSnapshotDocument document =
                    sourceParticipant.CaptureRollbackDocument(restoreFrame);
                document.RuntimeAiPlannerState.DamageZoneLocks = new[]
                {
                    new CharacterTestDamageZoneLockStateSnapshot
                    {
                        SourceEntityId = attacker.Id.Value,
                        TargetGameplayEntityIndex = target.GameplayEntityId.Index,
                        TargetGameplayEntityGeneration = target.GameplayEntityId.Generation,
                        TargetCombatEntityId = CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart
                            + target.Id.Value,
                        ResolvedActionRevision = 1,
                        ZoneId = MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id154,
                        HitZoneId = tailMember.HitZoneId,
                        LogicalHurtboxId = tailMember.LogicalHurtboxId,
                        OpportunityLevel = (int)CharacterTestAttackOpportunityLevel.CurrentPose,
                        EarliestHitTick = 0,
                        BindingRevision = intactZones.BindingRevision,
                        HurtboxRevision = intactGeometry.HurtboxRevision,
                        WorldRevision = intactGeometry.WorldRevision,
                        ZoneRevision = intactZones.ZoneRevision,
                        BodyPartStateRevision = target.BodyPartState.Revision,
                        SelectedFrame = restoreFrame.Value,
                        SwitchReason = (int)CharacterTestDamageZoneSwitchReason.InitialSelection
                    }
                };
                document.RuntimeAiPlannerState.EngagementReservations = new[]
                {
                    new CharacterTestEngagementReservationStateSnapshot
                    {
                        SourceEntityId = attacker.Id.Value,
                        TargetEntityId = target.Id.Value,
                        TargetCombatEntityId = CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart
                            + target.Id.Value,
                        ResolvedActionRevision = 1,
                        ZoneId = MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id154,
                        AnchorId = tailAnchor.AnchorId,
                        Capacity = tailAnchor.Capacity,
                        CapacitySlot = 0,
                        AcquiredFrame = restoreFrame.Value,
                        UpdatedFrame = restoreFrame.Value,
                        ZoneBindingRevision = intactZones.BindingRevision,
                        ZoneRevision = intactZones.ZoneRevision,
                        AnchorRevision = geometryBinding.EngagementAnchorRevision,
                        AnchorContentHash = geometryBinding.EngagementAnchorContentDigest,
                        PhaseId = tailAnchor.PhaseId,
                        PhaseRevision = 1,
                        CombatPhysicsRevision = source.PhysicsRuntime.World.Revision,
                        GraphRevision = navigation.Graph.Revision,
                        GraphContentHash = navigation.Graph.ContentHash,
                        ReleaseReason = (int)CharacterTestEngagementReservationReleaseReason.None
                    }
                };
                document.NormalizeForSchema(document.SchemaVersion);
                var wire = new RuntimeCustomState(
                    CharacterTestNetworkSnapshotParticipant.NumericParticipantId,
                    CharacterTestNetworkSnapshotParticipant.ParticipantIdValue,
                    CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                    SerializeNetworkDocument(document));

                CharacterTestNetworkSnapshotParticipant lateJoinParticipant =
                    lateJoin.Services.Network.CreateSnapshotParticipant();
                CharacterTestNetworkSnapshotParticipant rollbackParticipant =
                    rollback.Services.Network.CreateSnapshotParticipant();
                NetworkSnapshotApplyResult lateJoinApply =
                    lateJoinParticipant.ApplySnapshot(restoreFrame, wire);
                NetworkSnapshotApplyResult rollbackApply = rollbackParticipant.ApplyRollbackSnapshot(
                    restoreFrame,
                    wire,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(lateJoinApply.Success, lateJoinApply.Message);
                Assert.IsTrue(rollbackApply.Success, rollbackApply.Message);

                AssertRestoredSeveredTopology(lateJoin, target.Id.Value);
                AssertRestoredSeveredTopology(rollback, target.Id.Value);
                AssertTailPlannerWitnessReleased(lateJoin);
                AssertTailPlannerWitnessReleased(rollback);

                RuntimeCommandValidationResult lateJoinCommand = EnqueueParityCommand(
                    lateJoin,
                    restoreFrame,
                    attacker.Id.Value);
                RuntimeCommandValidationResult rollbackCommand = EnqueueParityCommand(
                    rollback,
                    restoreFrame,
                    attacker.Id.Value);
                Assert.IsTrue(lateJoinCommand.Success, lateJoinCommand.Error.Message);
                Assert.IsTrue(rollbackCommand.Success, rollbackCommand.Error.Message);
                Tick(lateJoin, 1);
                Tick(rollback, 1);

                AssertTailPlannerWitnessReleased(lateJoin);
                AssertTailPlannerWitnessReleased(rollback);
                RuntimeFrame resultFrame = lateJoin.CurrentFrame;
                Assert.AreEqual(resultFrame.Value, rollback.CurrentFrame.Value);
                Assert.AreEqual(
                    lateJoinParticipant.ComputeStateHash(resultFrame),
                    rollbackParticipant.ComputeStateHash(resultFrame),
                    "Identical +1 commands must produce the same authority result hash.");
            }
        }

        [Test]
        public void Schema33_EightCharacterLowFrequencySwitchCaptureApply_IsDeterministicAndSilent()
        {
            const int CharacterCount = 8;
            using (GameSlice source = CreateMultiDragonSlice(CharacterCount))
            using (GameSlice replica = CreateMultiDragonSlice(0))
            {
                Tick(source, 8);
                Tick(replica, 8);
                Assert.AreEqual(CharacterCount, source.CharacterEntities.Count);
                Assert.AreEqual(0, replica.CharacterEntities.Count);
                var tailDefinitionIndexes = new int[CharacterCount];
                var tailDefinitions = new CharacterTestBodyPartStateDefinition[CharacterCount];
                for (int i = 0; i < CharacterCount; i++)
                {
                    tailDefinitionIndexes[i] = FindDefinitionIndex(
                        source.CharacterEntities[i].BodyPartState,
                        "partstate.000003.tail");
                    Assert.IsTrue(source.CharacterEntities[i].BodyPartState.TryGetDefinition(
                        tailDefinitionIndexes[i],
                        out tailDefinitions[i]),
                        "Missing source tail definition for entity index " + i);
                }
                CharacterTestNetworkSnapshotParticipant sourceParticipant =
                    source.Services.Network.CreateSnapshotParticipant();
                CharacterTestNetworkSnapshotParticipant replicaParticipant =
                    replica.Services.Network.CreateSnapshotParticipant();
                long breakFrame = source.CurrentFrame.Value;
                RuntimeFrame severFrame = new RuntimeFrame(breakFrame + 1L);
                bool switchesSucceeded = true;
                RuntimeCustomState captured = null;
                NetworkSnapshotApplyResult apply = default;
                var stopwatch = new Stopwatch();
                _ = GC.GetAllocatedBytesForCurrentThread();
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                stopwatch.Start();
                for (int i = 0; i < CharacterCount; i++)
                {
                    switchesSucceeded &= TryCommitMeasuredSever(
                        source.CharacterEntities[i],
                        tailDefinitionIndexes[i],
                        tailDefinitions[i],
                        breakFrame,
                        eventKey: 900 + i);
                }
                captured = sourceParticipant.CaptureSnapshot(severFrame);
                apply = replicaParticipant.ApplySnapshot(severFrame, captured);
                stopwatch.Stop();
                long allocatedBytes =
                    GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

                TestContext.Out.WriteLine(
                    "lcc_m3_t6_eight_character_low_frequency_switch elapsed_ms="
                    + stopwatch.Elapsed.TotalMilliseconds.ToString(
                        "F4",
                        System.Globalization.CultureInfo.InvariantCulture)
                    + " allocated_bytes="
                    + allocatedBytes
                    + " characters="
                    + CharacterCount);
                Assert.IsTrue(switchesSucceeded, "Every bounded low-frequency sever switch must commit.");
                Assert.NotNull(captured);
                Assert.AreEqual(
                    CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                    captured.SchemaVersion);
                Assert.IsTrue(apply.Success, apply.Message);
                Assert.AreEqual(CharacterCount, replica.CharacterEntities.Count);
                Assert.AreEqual(
                    0,
                    replica.CombatTraceSystem.BodyPartDamageBridge.TransitionProjectionCount,
                    "Late join must not replay transient presentation-event projections.");

                source.Tick(1d / CharacterTestDeterministicUnits.DefaultTickRate);
                replica.Tick(1d / CharacterTestDeterministicUnits.DefaultTickRate);
                RuntimeFrame nextFrame = source.CurrentFrame;
                Assert.AreEqual(nextFrame, replica.CurrentFrame);
                Assert.AreEqual(
                    sourceParticipant.ComputeStateHash(nextFrame),
                    replicaParticipant.ComputeStateHash(nextFrame));

                for (int i = 0; i < CharacterCount; i++)
                {
                    CharacterTestCharacterEntity expected = source.CharacterEntities[i];
                    CharacterTestCharacterEntity restored =
                        replica.CharacterRuntime.FindEntityById(expected.Id.Value);
                    Assert.NotNull(restored);
                    Assert.IsTrue(restored.BodyPartState.TryGetCurrentEntry(
                        tailDefinitionIndexes[i],
                        out CharacterTestBodyPartStateEntry restoredTail),
                        "Missing restored tail state for entity index " + i);
                    Assert.AreEqual(
                        CharacterTestBodyPartStructureState.Severed,
                        restoredTail.StructureState);
                    Assert.AreEqual(0, restored.BodyPartState.TransitionCount);
                }
                Assert.AreEqual(
                    0,
                    replica.CombatTraceSystem.BodyPartDamageBridge.TransitionProjectionCount);
            }
        }

        [Test]
        public void LateJoin_RestoresDualAxisAccumulatorRevisionAndHash_WithoutTransitionReplay()
        {
            using (GameSlice source = CreateSlice(spawnDragon: true))
            using (GameSlice lateJoin = CreateSlice(spawnDragon: false))
            {
                TickUntilSpawned(source);
                Tick(lateJoin, 8);
                CharacterTestCharacterEntity sourceEntity = source.CharacterEntities[0];
                RuntimeFrame frame = source.CurrentFrame;
                CommitDamage(sourceEntity, frame.Value, eventKey: 2, finalDamage: int.MaxValue);
                CharacterTestNetworkSnapshotParticipant sourceParticipant =
                    source.Services.Network.CreateSnapshotParticipant();
                CharacterTestNetworkSnapshotParticipant lateJoinParticipant =
                    lateJoin.Services.Network.CreateSnapshotParticipant();

                RuntimeCustomState sourceState = sourceParticipant.CaptureSnapshot(frame);
                JObject sourceMismatchDocument = JObject.Parse(sourceState.PayloadJson);
                sourceMismatchDocument["bodyPartStates"][0]["definitionSourceContentHash"] =
                    "sha256:late-join-mismatch";
                var sourceMismatch = new RuntimeCustomState(
                    sourceState.NumericTypeId,
                    sourceState.TypeId,
                    sourceState.SchemaVersion,
                    sourceMismatchDocument.ToString(Formatting.None));
                long emptyAuthorityHash = lateJoinParticipant.ComputeStateHash(frame);
                NetworkSnapshotApplyResult mismatchApply =
                    lateJoinParticipant.ApplySnapshot(frame, sourceMismatch);
                Assert.IsFalse(mismatchApply.Success);
                Assert.AreEqual(0, lateJoin.CharacterEntities.Count,
                    "Source mismatch must fail before late-join character spawn.");
                Assert.AreEqual(emptyAuthorityHash, lateJoinParticipant.ComputeStateHash(frame));

                NetworkSnapshotApplyResult apply = lateJoinParticipant.ApplySnapshot(
                    frame,
                    sourceState);

                Assert.IsTrue(apply.Success, apply.Message);
                Assert.AreEqual(1, lateJoin.CharacterEntities.Count);
                CharacterTestCharacterEntity restored = lateJoin.CharacterEntities[0];
                AssertBodyStateEqual(sourceEntity.BodyPartState, restored.BodyPartState);
                Assert.AreEqual(0, restored.BodyPartState.TransitionCount,
                    "Restore must not replay one-shot transitions.");
                Assert.AreEqual(
                    sourceParticipant.ComputeStateHash(frame),
                    lateJoinParticipant.ComputeStateHash(frame));
                Assert.AreEqual(
                    sourceParticipant.ComputeOwnedStateHash(frame, sourceEntity.Id.Value),
                    lateJoinParticipant.ComputeOwnedStateHash(frame, restored.Id.Value));
            }
        }

        [Test]
        public void LegacyFutureAndSourceMismatch_FailOrDefaultWithoutPartialAuthority()
        {
            using (GameSlice slice = CreateSlice(spawnDragon: true))
            {
                TickUntilSpawned(slice);
                CharacterTestCharacterEntity entity = slice.CharacterEntities[0];
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame frame = slice.CurrentFrame;
                CommitDamage(entity, frame.Value, eventKey: 3, finalDamage: int.MaxValue);

                CharacterTestNetworkSnapshotDocument legacy =
                    participant.CaptureRollbackDocument(frame);
                legacy.SchemaVersion =
                    CharacterTestNetworkSnapshotParticipant.EngagementReservationSchemaVersionValue;
                legacy.BodyPartStates = null;
                legacy.RuntimeAiPlannerState.DamageZoneLocks = new[]
                {
                    CreateLock(entity, bodyPartStateRevision: 99L)
                };
                legacy.NormalizeForSchema(legacy.SchemaVersion);
                Assert.AreEqual(0L,
                    legacy.RuntimeAiPlannerState.DamageZoneLocks[0].BodyPartStateRevision);
                NetworkSnapshotApplyResult legacyApply = participant.ApplyRollbackDocument(
                    frame,
                    legacy,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(legacyApply.Success, legacyApply.Message);
                AssertLegacyDefaults(entity.BodyPartState);
                CharacterTestNetworkSnapshotDocument normalizedLegacyAuthority =
                    participant.CaptureRollbackDocument(frame);
                Assert.AreEqual(
                    1,
                    normalizedLegacyAuthority.RuntimeAiPlannerState.DamageZoneLocks.Length);
                Assert.AreEqual(
                    0L,
                    normalizedLegacyAuthority.RuntimeAiPlannerState
                        .DamageZoneLocks[0].BodyPartStateRevision);

                CharacterTestBodyPartDamageAuthorityBridge bridge =
                    slice.CombatTraceSystem.BodyPartDamageBridge;
                RunBridgeAcceptedHit(
                    bridge,
                    entity,
                    frame.Value,
                    eventKey: 31,
                    finalDamage: int.MaxValue);
                int projectionCountBeforeFailure = bridge.TransitionProjectionCount;
                Assert.AreEqual(1, bridge.DamageTelemetryCount);
                Assert.Greater(projectionCountBeforeFailure, 0);
                long beforeMismatchHash = participant.ComputeStateHash(frame);
                CharacterTestNetworkSnapshotDocument mismatch =
                    participant.CaptureRollbackDocument(frame);
                mismatch.BodyPartStates[0].DefinitionSourceContentHash = "sha256:mismatch";
                NetworkSnapshotApplyResult mismatchApply = participant.ApplyRollbackDocument(
                    frame,
                    mismatch,
                    preserveLocalPredictedEntity: false);
                Assert.IsFalse(mismatchApply.Success);
                StringAssert.Contains("source identity mismatch", mismatchApply.Message);
                Assert.AreEqual(beforeMismatchHash, participant.ComputeStateHash(frame));
                Assert.AreEqual(1, bridge.DamageTelemetryCount);
                Assert.AreEqual(projectionCountBeforeFailure, bridge.TransitionProjectionCount);

                RuntimeCustomState current = participant.CaptureSnapshot(frame);
                JObject futureDocument = JObject.Parse(current.PayloadJson);
                futureDocument["schemaVersion"] =
                    CharacterTestNetworkSnapshotParticipant.SchemaVersionValue + 1;
                var future = new RuntimeCustomState(
                    current.NumericTypeId,
                    current.TypeId,
                    CharacterTestNetworkSnapshotParticipant.SchemaVersionValue + 1,
                    futureDocument.ToString(Formatting.None));
                NetworkSnapshotApplyResult futureApply = participant.ApplySnapshot(frame, future);
                Assert.IsFalse(futureApply.Success);
                Assert.AreEqual(beforeMismatchHash, participant.ComputeStateHash(frame));
                Assert.AreEqual(1, bridge.DamageTelemetryCount);
                Assert.AreEqual(projectionCountBeforeFailure, bridge.TransitionProjectionCount);
            }
        }

        [Test]
        public void RollbackPlusOne_RebuildsFromAuthorityAndPublishesEachTransitionOnce()
        {
            using (GameSlice slice = CreateSlice(spawnDragon: true))
            {
                TickUntilSpawned(slice);
                CharacterTestCharacterEntity entity = slice.CharacterEntities[0];
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame checkpointFrame = slice.CurrentFrame;
                CharacterTestNetworkSnapshotDocument checkpoint =
                    participant.CaptureRollbackDocument(checkpointFrame);

                Assert.IsTrue(entity.BodyPartState.TryBeginFrame(checkpointFrame.Value + 100, out _));
                Assert.AreEqual(
                    CharacterTestBodyPartStateStageResult.Staged,
                    entity.BodyPartState.StageAcceptedDamage(CreateDamageFact(
                        entity.Id.Value,
                        checkpointFrame.Value + 100,
                        eventKey: 100,
                        finalDamage: 1)));
                NetworkSnapshotApplyResult firstRestore = participant.ApplyRollbackDocument(
                    checkpointFrame,
                    checkpoint,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(firstRestore.Success, firstRestore.Message);
                Assert.AreEqual(0, entity.BodyPartState.TransitionCount);

                long nextFrame = checkpointFrame.Value + 1;
                CharacterTestBodyPartStateCommitResult first =
                    CommitDamage(entity, nextFrame, eventKey: 4, finalDamage: int.MaxValue);
                Assert.Greater(first.TransitionCount, 0);
                CharacterTestBodyPartStateTransition firstTransition =
                    entity.BodyPartState.GetTransitionAt(0);
                long firstHash = participant.ComputeStateHash(new RuntimeFrame(nextFrame));

                NetworkSnapshotApplyResult secondRestore = participant.ApplyRollbackDocument(
                    checkpointFrame,
                    checkpoint,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(secondRestore.Success, secondRestore.Message);
                Assert.AreEqual(0, entity.BodyPartState.TransitionCount);
                CharacterTestBodyPartStateCommitResult replayed =
                    CommitDamage(entity, nextFrame, eventKey: 4, finalDamage: int.MaxValue);
                CharacterTestBodyPartStateTransition replayedTransition =
                    entity.BodyPartState.GetTransitionAt(0);

                Assert.AreEqual(first.TransitionCount, replayed.TransitionCount);
                Assert.AreEqual(firstTransition.PartStateId, replayedTransition.PartStateId);
                Assert.AreEqual(firstTransition.Axis, replayedTransition.Axis);
                Assert.AreEqual(firstTransition.Reason, replayedTransition.Reason);
                Assert.AreEqual(firstTransition.After.Revision, replayedTransition.After.Revision);
                Assert.AreEqual(firstHash,
                    participant.ComputeStateHash(new RuntimeFrame(nextFrame)));
            }
        }

        [Test]
        public void AuthorityRestore_ClearsFutureBridgeProjections_ThenEmptyAndHitFramesRestartOnce()
        {
            using (GameSlice slice = CreateSlice(spawnDragon: true))
            {
                TickUntilSpawned(slice);
                CharacterTestCharacterEntity entity = slice.CharacterEntities[0];
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                CharacterTestBodyPartDamageAuthorityBridge bridge =
                    slice.CombatTraceSystem.BodyPartDamageBridge;
                RuntimeFrame checkpointFrame = slice.CurrentFrame;
                CharacterTestNetworkSnapshotDocument checkpoint =
                    participant.CaptureRollbackDocument(checkpointFrame);

                long discardedFutureFrame = checkpointFrame.Value + 100;
                RunBridgeAcceptedHit(
                    bridge,
                    entity,
                    discardedFutureFrame,
                    eventKey: 50,
                    finalDamage: int.MaxValue);
                Assert.AreEqual(1, bridge.DamageTelemetryCount);
                Assert.Greater(bridge.TransitionProjectionCount, 0);

                NetworkSnapshotApplyResult restore = participant.ApplyRollbackDocument(
                    checkpointFrame,
                    checkpoint,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(restore.Success, restore.Message);
                Assert.AreEqual(0, bridge.DamageTelemetryCount);
                Assert.AreEqual(0, bridge.TransitionProjectionCount);

                long nextFrame = checkpointFrame.Value + 1;
                Assert.IsTrue(bridge.CompleteAuthorityFrame(nextFrame),
                    "An empty frame after a lower-frame restore must be accepted.");
                RunBridgeAcceptedHit(
                    bridge,
                    entity,
                    nextFrame,
                    eventKey: 51,
                    finalDamage: int.MaxValue);
                int firstTransitionCount = bridge.TransitionProjectionCount;
                long firstHash = participant.ComputeStateHash(new RuntimeFrame(nextFrame));
                Assert.AreEqual(1, bridge.DamageTelemetryCount);
                Assert.Greater(firstTransitionCount, 0);

                restore = participant.ApplyRollbackDocument(
                    checkpointFrame,
                    checkpoint,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(restore.Success, restore.Message);
                Assert.AreEqual(0, bridge.DamageTelemetryCount);
                Assert.AreEqual(0, bridge.TransitionProjectionCount);
                RunBridgeAcceptedHit(
                    bridge,
                    entity,
                    nextFrame,
                    eventKey: 51,
                    finalDamage: int.MaxValue);
                Assert.AreEqual(1, bridge.DamageTelemetryCount);
                Assert.AreEqual(firstTransitionCount, bridge.TransitionProjectionCount);
                Assert.AreEqual(firstHash,
                    participant.ComputeStateHash(new RuntimeFrame(nextFrame)));
            }
        }

        [Test]
        public void RollbackPlusOne_PreservesZoneLockWitnessAndBranchHashParity()
        {
            using (GameSlice slice = CreatePhysicsNetworkSlice(spawnPair: true))
            {
                Tick(slice, 8);
                Assert.AreEqual(2, slice.CharacterEntities.Count);
                CharacterTestCharacterEntity attacker = slice.CharacterEntities[0];
                CharacterTestCharacterEntity target = slice.CharacterEntities[1];
                Assert.IsTrue(slice.CharacterRuntime.TryGetDamageGeometrySnapshot(
                    target.Id.Value,
                    out CharacterTestDamageGeometrySnapshot geometry));
                Assert.IsTrue(slice.CharacterRuntime.TryGetDamageZoneSnapshot(
                    target.Id.Value,
                    out CharacterTestDamageZoneSnapshot zones));
                Assert.IsTrue(zones.TryGetAvailableZone(
                    MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id151,
                    out CharacterTestDamageZoneAggregate core));
                CharacterTestDamageZoneMember member = zones.GetMember(core.MemberStart);
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame checkpointFrame = slice.CurrentFrame;
                CharacterTestNetworkSnapshotDocument withLock =
                    participant.CaptureRollbackDocument(checkpointFrame);
                withLock.RuntimeAiPlannerState.DamageZoneLocks = new[]
                {
                    new CharacterTestDamageZoneLockStateSnapshot
                    {
                        SourceEntityId = attacker.Id.Value,
                        TargetGameplayEntityIndex = target.GameplayEntityId.Index,
                        TargetGameplayEntityGeneration = target.GameplayEntityId.Generation,
                        TargetCombatEntityId = CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart
                            + target.Id.Value,
                        ResolvedActionRevision = 1,
                        ZoneId = MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id151,
                        HitZoneId = member.HitZoneId,
                        LogicalHurtboxId = member.LogicalHurtboxId,
                        OpportunityLevel = (int)CharacterTestAttackOpportunityLevel.CurrentPose,
                        EarliestHitTick = 0,
                        BindingRevision = zones.BindingRevision,
                        HurtboxRevision = geometry.HurtboxRevision,
                        WorldRevision = geometry.WorldRevision,
                        ZoneRevision = zones.ZoneRevision,
                        BodyPartStateRevision = target.BodyPartState.Revision,
                        SelectedFrame = checkpointFrame.Value,
                        SwitchReason = (int)CharacterTestDamageZoneSwitchReason.InitialSelection
                    }
                };
                NetworkSnapshotApplyResult install = participant.ApplyRollbackDocument(
                    checkpointFrame,
                    withLock,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(install.Success, install.Message);

                CharacterTestNetworkSnapshotDocument checkpoint =
                    participant.CaptureRollbackDocument(checkpointFrame);
                Assert.AreEqual(1, checkpoint.RuntimeAiPlannerState.DamageZoneLocks.Length);
                CharacterTestDamageZoneLockStateSnapshot witness =
                    checkpoint.RuntimeAiPlannerState.DamageZoneLocks[0];
                Assert.AreEqual(target.BodyPartState.Revision, witness.BodyPartStateRevision);
                Assert.AreEqual("zone.core", witness.ZoneId);

                long nextFrame = checkpointFrame.Value + 1;
                int tailIndex = FindDefinitionIndex(
                    target.BodyPartState,
                    "partstate.000003.tail");
                CommitDamage(
                    target,
                    nextFrame,
                    eventKey: 60,
                    finalDamage: int.MaxValue,
                    definitionIndex: tailIndex);
                long firstHash = participant.ComputeStateHash(new RuntimeFrame(nextFrame));
                CharacterTestDamageZoneLockStateSnapshot firstBranchWitness =
                    participant.CaptureRollbackDocument(new RuntimeFrame(nextFrame))
                        .RuntimeAiPlannerState.DamageZoneLocks[0];

                NetworkSnapshotApplyResult restore = participant.ApplyRollbackDocument(
                    checkpointFrame,
                    checkpoint,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(restore.Success, restore.Message);
                CharacterTestDamageZoneLockStateSnapshot restoredWitness =
                    participant.CaptureRollbackDocument(checkpointFrame)
                        .RuntimeAiPlannerState.DamageZoneLocks[0];
                AssertLockEqual(witness, restoredWitness);

                CommitDamage(
                    target,
                    nextFrame,
                    eventKey: 60,
                    finalDamage: int.MaxValue,
                    definitionIndex: tailIndex);
                CharacterTestDamageZoneLockStateSnapshot replayWitness =
                    participant.CaptureRollbackDocument(new RuntimeFrame(nextFrame))
                        .RuntimeAiPlannerState.DamageZoneLocks[0];
                AssertLockEqual(firstBranchWitness, replayWitness);
                Assert.AreEqual(firstHash,
                    participant.ComputeStateHash(new RuntimeFrame(nextFrame)));
            }
        }

        [Test]
        public void BadLockTarget_FailsBeforeCharacterOrBodyAuthorityChanges()
        {
            using (GameSlice slice = CreateSlice(spawnDragon: true))
            {
                TickUntilSpawned(slice);
                CharacterTestCharacterEntity entity = slice.CharacterEntities[0];
                CharacterTestNetworkSnapshotParticipant participant =
                    slice.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame frame = slice.CurrentFrame;
                long beforeHash = participant.ComputeStateHash(frame);
                CharacterTestBodyPartStateComponent beforeComponent = entity.BodyPartState;
                CharacterTestNetworkSnapshotDocument invalid =
                    participant.CaptureRollbackDocument(frame);
                CharacterTestDamageZoneLockStateSnapshot badLock =
                    CreateLock(entity, entity.BodyPartState.Revision);
                badLock.TargetGameplayEntityIndex += 100;
                invalid.RuntimeAiPlannerState.DamageZoneLocks = new[] { badLock };

                NetworkSnapshotApplyResult apply = participant.ApplyRollbackDocument(
                    frame,
                    invalid,
                    preserveLocalPredictedEntity: false);

                Assert.IsFalse(apply.Success);
                StringAssert.Contains("post-apply runtime identity", apply.Message);
                Assert.AreSame(entity, slice.CharacterEntities[0]);
                Assert.AreSame(beforeComponent, entity.BodyPartState);
                Assert.AreEqual(beforeHash, participant.ComputeStateHash(frame));
            }
        }

        [Test]
        public void DamageZoneLockCloneAndHash_AreCanonicalAndIgnoreDerivedBodyRevision()
        {
            CharacterTestDamageZoneLockStateSnapshot first = CreateLock(
                sourceEntityId: 1,
                targetEntityId: 2,
                bodyPartStateRevision: 10L);
            CharacterTestDamageZoneLockStateSnapshot second = CreateLock(
                sourceEntityId: 2,
                targetEntityId: 1,
                bodyPartStateRevision: 20L);
            CharacterTestRuntimeAiPlannerStateSnapshot forward = CreatePlannerState(first, second);
            CharacterTestRuntimeAiPlannerStateSnapshot reverse = CreatePlannerState(second, first);

            Assert.AreEqual(HashPlanner(forward), HashPlanner(reverse));
            Assert.AreEqual(
                JsonConvert.SerializeObject(forward.CloneForRollback()),
                JsonConvert.SerializeObject(reverse.CloneForRollback()),
                "Canonical lock bytes must not depend on input array order.");
            CharacterTestRuntimeAiPlannerStateSnapshot clone = reverse.CloneForRollback();
            Assert.AreEqual(1, clone.DamageZoneLocks[0].SourceEntityId);
            Assert.AreEqual(10L, clone.DamageZoneLocks[0].BodyPartStateRevision);

            clone.DamageZoneLocks[0].BodyPartStateRevision++;
            Assert.AreEqual(HashPlanner(forward), HashPlanner(clone));
        }

        private static GameSlice CreateSlice(bool spawnDragon)
        {
            CharacterTestCharacterSpawnSpec[] startup = spawnDragon
                ? new[]
                {
                    new CharacterTestCharacterSpawnSpec(
                        CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000003"),
                        teamId: 1,
                        x: 0f,
                        y: 1f,
                        z: 0f,
                        controlOwner: CharacterTestCharacterControlOwner.AiControlled)
                }
                : Array.Empty<CharacterTestCharacterSpawnSpec>();
            return new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                StartupCharacterSpawnSpecs = startup,
                SuppressMissingStartupSpawnWarning = true
            });
        }

        private static GameSlice CreateMultiDragonSlice(int characterCount)
        {
            int count = Math.Max(0, characterCount);
            var startup = new CharacterTestCharacterSpawnSpec[count];
            for (int i = 0; i < count; i++)
            {
                startup[i] = new CharacterTestCharacterSpawnSpec(
                    CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000003"),
                    teamId: 1,
                    x: (i % 4) * 12f,
                    y: 1f,
                    z: (i / 4) * 12f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled);
            }
            return new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                StartupCharacterSpawnSpecs = startup,
                SuppressMissingStartupSpawnWarning = true
            });
        }

        private static GameSlice CreatePhysicsNetworkSlice(bool spawnPair)
        {
            CharacterTestCharacterSpawnSpec[] startup = spawnPair
                ? new[]
                {
                    new CharacterTestCharacterSpawnSpec(
                        CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000003"),
                        teamId: 1,
                        x: -5f,
                        y: 1f,
                        z: 0f,
                        controlOwner: CharacterTestCharacterControlOwner.AiControlled),
                    new CharacterTestCharacterSpawnSpec(
                        CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000003"),
                        teamId: 2,
                        x: 0f,
                        y: 1f,
                        z: 0f,
                        controlOwner: CharacterTestCharacterControlOwner.None)
                }
                : Array.Empty<CharacterTestCharacterSpawnSpec>();
            var physics = new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Zero,
                staticColliderCount: 1,
                sourceDescription: "lcc-m3-t6.schema32-derived-topology");
            return new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                PhysicsRuntime = physics,
                NavigationBuildInput = CreateNavigationBuildInput(),
                NavigationInputResult = new CharacterTestNavigationSceneInputResult(
                    CharacterTestNavigationSceneInputStatus.Success),
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                StartupCharacterSpawnSpecs = startup,
                SuppressMissingStartupSpawnWarning = true
            });
        }

        private static CharacterTestNavigationSessionBuildInput CreateNavigationBuildInput()
        {
            return new CharacterTestNavigationSessionBuildInput(
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
        }

        private static void TickUntilSpawned(GameSlice slice)
        {
            for (int i = 0; i < 8 && slice.CharacterEntities.Count == 0; i++)
                slice.Tick(1d / CharacterTestDeterministicUnits.DefaultTickRate);
            Assert.AreEqual(1, slice.CharacterEntities.Count);
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                slice.CharacterEntities[0].BodyPartState.InitializationStatus);
        }

        private static void Tick(GameSlice slice, int count)
        {
            for (int i = 0; i < count; i++)
                slice.Tick(1d / CharacterTestDeterministicUnits.DefaultTickRate);
        }

        private static CharacterTestBodyPartStateCommitResult CommitDamage(
            CharacterTestCharacterEntity entity,
            long frame,
            int eventKey,
            int finalDamage,
            int definitionIndex = 0)
        {
            Assert.IsTrue(entity.BodyPartState.TryBeginFrame(frame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                entity.BodyPartState.StageAcceptedDamage(CreateDamageFact(
                    entity.Id.Value,
                    frame,
                    eventKey,
                    finalDamage,
                    definitionIndex)));
            CharacterTestBodyPartStateCommitResult result = entity.BodyPartState.CommitFrame();
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.Committed, result.Status);
            return result;
        }

        private static CharacterTestBodyPartAcceptedDamageFact CreateDamageFact(
            int targetEntityId,
            long frame,
            int eventKey,
            int finalDamage,
            int definitionIndex = 0)
        {
            return new CharacterTestBodyPartAcceptedDamageFact(
                frame,
                sourceEntityId: 99,
                targetEntityId,
                actionInstanceId: eventKey,
                policyEventId: eventKey,
                traceId: eventKey,
                localFrame: eventKey,
                definitionIndex,
                finalDamage);
        }

        private static CharacterTestBodyPartStateCommitResult CommitSever(
            CharacterTestCharacterEntity entity,
            long frame,
            int definitionIndex,
            long correlationId)
        {
            Assert.IsTrue(entity.BodyPartState.TryBeginFrame(frame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                entity.BodyPartState.StageSever(new CharacterTestBodyPartSeverFact(
                    frame,
                    sourceEntityId: 99,
                    targetEntityId: entity.Id.Value,
                    definitionIndex,
                    severTag: "action.000003.tail.sever",
                    correlationId)));
            CharacterTestBodyPartStateCommitResult result = entity.BodyPartState.CommitFrame();
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.Committed, result.Status);
            Assert.AreEqual(1, result.SeverFactCount);
            Assert.AreEqual(1, result.TransitionCount);
            Assert.AreEqual(
                CharacterTestBodyPartTransitionReason.Severed,
                entity.BodyPartState.GetTransitionAt(0).Reason);
            return result;
        }

        private static bool TryCommitMeasuredSever(
            CharacterTestCharacterEntity entity,
            int definitionIndex,
            CharacterTestBodyPartStateDefinition definition,
            long breakFrame,
            int eventKey)
        {
            CharacterTestBodyPartStateComponent state = entity?.BodyPartState;
            if (state == null || !state.TryBeginFrame(breakFrame, out _))
                return false;
            CharacterTestBodyPartStateStageResult damageStage = state.StageAcceptedDamage(
                new CharacterTestBodyPartAcceptedDamageFact(
                    authorityFrame: breakFrame,
                    sourceEntityId: 99,
                    targetEntityId: entity.Id.Value,
                    actionInstanceId: eventKey,
                    policyEventId: eventKey,
                    traceId: eventKey,
                    localFrame: eventKey,
                    definitionIndex,
                    finalDamage: int.MaxValue));
            CharacterTestBodyPartStateCommitResult damageCommit = state.CommitFrame();
            if (damageStage != CharacterTestBodyPartStateStageResult.Staged
                || damageCommit.Status != CharacterTestBodyPartStateCommitStatus.Committed
                || !state.TryGetCurrentEntry(
                    definitionIndex,
                    out CharacterTestBodyPartStateEntry broken)
                || broken.StructureState != CharacterTestBodyPartStructureState.Broken
                || !state.TryBeginFrame(breakFrame + 1L, out _))
            {
                return false;
            }

            CharacterTestBodyPartStateStageResult severStage = state.StageSever(
                new CharacterTestBodyPartSeverFact(
                    authorityFrame: breakFrame + 1L,
                    sourceEntityId: 99,
                    targetEntityId: entity.Id.Value,
                    definitionIndex,
                    severTag: definition.SeverRequiredTag,
                    correlationId: eventKey));
            CharacterTestBodyPartStateCommitResult severCommit = state.CommitFrame();
            return severStage == CharacterTestBodyPartStateStageResult.Staged
                && severCommit.Status == CharacterTestBodyPartStateCommitStatus.Committed
                && severCommit.TransitionCount == 1
                && state.TryGetCurrentEntry(
                    definitionIndex,
                    out CharacterTestBodyPartStateEntry severed)
                && severed.StructureState == CharacterTestBodyPartStructureState.Severed;
        }

        private static int FindDefinitionIndex(
            CharacterTestBodyPartStateComponent component,
            string partStateId)
        {
            for (int i = 0; i < component.DefinitionCount; i++)
            {
                Assert.IsTrue(component.TryGetDefinition(
                    i,
                    out CharacterTestBodyPartStateDefinition definition));
                if (definition.PartStateId == ResolvePartStateId(partStateId))
                    return i;
            }
            Assert.Fail("Missing body-part state definition: " + partStateId);
            return -1;
        }

        private static int FindNonSeverableDefinitionIndex(
            CharacterTestBodyPartStateComponent component)
        {
            for (int i = 0; i < component.DefinitionCount; i++)
            {
                Assert.IsTrue(component.TryGetDefinition(
                    i,
                    out CharacterTestBodyPartStateDefinition definition));
                if (!definition.Severable && definition.MaxIntegrity > 0)
                    return i;
            }
            Assert.Fail("Missing non-Severable Dragon14 body-part definition.");
            return -1;
        }

        private static int ResolvePartStateId(string partStateId)
        {
            return string.Equals(partStateId, "partstate.000003.tail", StringComparison.Ordinal)
                ? MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id43
                : 0;
        }

        private static CharacterEngagementAnchorRuntimeBinding FindAnchor(
            CharacterEngagementAnchorRuntimeBinding[] anchors,
            string anchorId)
        {
            for (int i = 0; anchors != null && i < anchors.Length; i++)
            {
                if (anchors[i].AnchorId == ResolveAnchorId(anchorId))
                    return anchors[i];
            }
            return null;
        }

        private static int ResolveAnchorId(string anchorId)
        {
            return string.Equals(anchorId, "anchor.tail.rear", StringComparison.Ordinal)
                ? MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id161
                : 0;
        }

        private static string SerializeNetworkDocument(
            CharacterTestNetworkSnapshotDocument document)
        {
            return JsonConvert.SerializeObject(document, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Include
            });
        }

        private static void AssertRestoredSeveredTopology(GameSlice slice, int targetEntityId)
        {
            CharacterTestCharacterEntity target = slice.CharacterEntities[1];
            Assert.AreEqual(targetEntityId, target.Id.Value);
            int tailIndex = FindDefinitionIndex(
                target.BodyPartState,
                "partstate.000003.tail");
            Assert.IsTrue(target.BodyPartState.TryGetCurrentEntry(
                tailIndex,
                out CharacterTestBodyPartStateEntry tail));
            Assert.AreEqual(CharacterTestBodyPartStructureState.Severed, tail.StructureState);
            Assert.AreEqual(0, target.BodyPartState.TransitionCount);
            Assert.IsTrue(slice.CharacterRuntime.TryGetDamageGeometrySnapshot(
                targetEntityId,
                out CharacterTestDamageGeometrySnapshot geometry));
            Assert.AreEqual(15, geometry.ColliderCount);
            Assert.IsTrue(slice.CharacterRuntime.TryGetDamageZoneSnapshot(
                targetEntityId,
                out CharacterTestDamageZoneSnapshot zones));
            Assert.IsTrue(zones.TryGetZone(
                MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id154,
                out CharacterTestDamageZoneAggregate tailZone));
            Assert.IsFalse(tailZone.IsAvailable);
            Assert.AreEqual(0, tailZone.MemberCount);
            Assert.IsFalse(zones.TryGetAvailableZone(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id154, out _));
            Assert.IsTrue(zones.TryGetAvailableZone(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id151, out _));
            Assert.IsTrue(zones.TryGetAvailableZone(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id152, out _));
        }

        private static RuntimeCommandValidationResult EnqueueParityCommand(
            GameSlice slice,
            RuntimeFrame frame,
            int entityId)
        {
            return slice.EnqueueRuntimeCommand(CharacterTestRuntimeCommandFactory.SetControlIntent(
                frame,
                CharacterTestRuntimeCommandSources.System,
                entityId,
                Vector2.zero,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: false,
                traceId: new RuntimeTraceIdentity(953432)));
        }

        private static void AssertTailPlannerWitnessReleased(GameSlice slice)
        {
            CharacterTestRuntimeAiPlannerStateSnapshot state =
                slice.RuntimeAiPlannerCommands.CreateStateSnapshot();
            bool retainedTailTarget = false;
            for (int i = 0; i < state.DamageZoneLocks.Length; i++)
            {
                CharacterTestDamageZoneLockStateSnapshot locked = state.DamageZoneLocks[i];
                if (locked.ZoneId != MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id154)
                    continue;
                retainedTailTarget = true;
                var handle = new CharacterTestCombatTargetHandle(
                    locked.SourceEntityId,
                    locked.TargetCombatEntityId
                        - CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart,
                    new GameplayEntityId(
                        locked.TargetGameplayEntityIndex,
                        locked.TargetGameplayEntityGeneration),
                    new CombatEntityId(locked.TargetCombatEntityId),
                    locked.ZoneId,
                    locked.HitZoneId,
                    locked.LogicalHurtboxId,
                    locked.BindingRevision,
                    new RuntimeFrame(locked.SelectedFrame));
                Assert.IsFalse(slice.CharacterRuntime.PerceptionFrameCache.TryGetLockedHurtboxDistance(
                    slice.CurrentFrame,
                    handle,
                    out CharacterTestLockedHurtboxDistanceSample unavailable));
                Assert.AreEqual(
                    CharacterTestLockedHurtboxAvailabilityStatus.HurtboxUnavailable,
                    unavailable.Status,
                    "A severed locked Hurtbox remains the subtarget but must fail closed.");
            }
            Assert.IsTrue(retainedTailTarget,
                "The restored tail combat target must remain locked for the same actor.");
            for (int i = 0; i < state.EngagementReservations.Length; i++)
            {
                Assert.AreNotEqual(
                    "zone.tail",
                    state.EngagementReservations[i].ZoneId,
                    "The restored tail engagement reservation must not survive Planner +1.");
            }
        }

        private static void RunBridgeAcceptedHit(
            CharacterTestBodyPartDamageAuthorityBridge bridge,
            CharacterTestCharacterEntity target,
            long frame,
            int eventKey,
            int finalDamage)
        {
            var hit = new HitResolveResult(
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 99),
                new CombatEntityId(
                    CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value),
                actionId: 11,
                actionInstanceId: eventKey,
                traceId: eventKey,
                frame: new CombatFrame((int)frame),
                HitResolveKind.Damage,
                finalDamage,
                staggerFrames: 0,
                knockback: default);
            var usage = new DamageProfileUsage(
                DamageProfileSourceKind.WeaponTrace,
                new CharacterActionId(hit.ActionId),
                hit.ActionInstanceId,
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0025,
                 MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D405_Id1,
                 780001,
                sourceEntityId: 99,
                targetEntityId: target.Id.Value,
                hitZoneId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id104,
                 bodyPartId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id3,
                frame: hit.Frame.Value,
                localFrame: eventKey);
            DamageProfileResolveResult payload = CreateDamage(finalDamage, usage);
            CharacterTestBodyPartHitEvidence evidence = ExplicitHeadEvidence();

            Assert.IsTrue(bridge.BeginAuthorityFrame(frame));
            DamageProfileResolveResult resolved = bridge.ResolvePreHitDamage(
                frame,
                target.BodyPartState,
                evidence,
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                payload,
                out CharacterTestBodyPartPreHitDecision decision);
            Assert.IsTrue(bridge.RecordResolvedHit(
                new CharacterTestResolvedHitKey(hit),
                target.BodyPartState,
                evidence,
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                sourceEntityId: 99,
                targetEntityId: target.Id.Value,
                policyEventId: eventKey,
                localFrame: eventKey,
                decision));
            Assert.IsTrue(bridge.CacheResolvedDamageEvent(new DamageResolvedEvent(
                new DamageRequest(resolved.DamageProfileId, resolved.Usage),
                resolved,
                hit,
                finalDamage: resolved.IncomingDamage)));
            Assert.IsTrue(bridge.CompleteAuthorityFrame(frame));
        }

        private static DamageProfileResolveResult CreateDamage(
            int incomingDamage,
            DamageProfileUsage usage)
        {
            return new DamageProfileResolveResult(
                success: true,
                reason: string.Empty,
                damageProfileId: 0,
                damageTypeId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D409_Id2,
                formulaId: DamageFormulaIds.Flat,
                incomingDamage,
                staggerFrames: 0,
                impactForce: 0,
                knockback: default,
                impactPower: 0,
                guardPressure: 0,
                presentationProfileId: 0,
                usage);
        }

        private static CharacterTestBodyPartHitEvidence ExplicitHeadEvidence()
        {
            return new CharacterTestBodyPartHitEvidence(
                new CharacterTestHitZoneResolution(
                    MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id104,
                     MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id3,
                     CharacterTestReactionGroupIds.HeightMid,
                    damageMultiplier: 1f,
                    staggerScale: 1f,
                    impulseScale: 1f,
                    impactPowerScale: 1f),
                CharacterTestBodyPartHitProvenance.ExplicitColliderBinding);
        }

        private static void AssertBodyStateEqual(
            CharacterTestBodyPartStateComponent expected,
            CharacterTestBodyPartStateComponent actual)
        {
            Assert.AreEqual(expected.InitializationStatus, actual.InitializationStatus);
            Assert.AreEqual(expected.DefinitionCount, actual.DefinitionCount);
            Assert.AreEqual(expected.Revision, actual.Revision);
            Assert.AreEqual(expected.DefinitionSourceFormat, actual.DefinitionSourceFormat);
            Assert.AreEqual(expected.DefinitionSourceSchemaVersion, actual.DefinitionSourceSchemaVersion);
            Assert.AreEqual(expected.DefinitionSourceRevision, actual.DefinitionSourceRevision);
            Assert.AreEqual(expected.DefinitionSourceContentHash, actual.DefinitionSourceContentHash);
            for (int i = 0; i < expected.DefinitionCount; i++)
            {
                Assert.IsTrue(expected.TryGetCurrentEntry(i, out CharacterTestBodyPartStateEntry left));
                Assert.IsTrue(actual.TryGetCurrentEntry(i, out CharacterTestBodyPartStateEntry right));
                Assert.AreEqual(left.DefinitionIndex, right.DefinitionIndex);
                Assert.AreEqual(left.WoundValue, right.WoundValue);
                Assert.AreEqual(left.WoundState, right.WoundState);
                Assert.AreEqual(left.CurrentIntegrity, right.CurrentIntegrity);
                Assert.AreEqual(left.StructureState, right.StructureState);
                Assert.AreEqual(left.LastAcceptedHitFrame, right.LastAcceptedHitFrame);
                Assert.AreEqual(left.Revision, right.Revision);
            }
        }

        private static void AssertLegacyDefaults(CharacterTestBodyPartStateComponent component)
        {
            Assert.AreEqual(0L, component.Revision);
            for (int i = 0; i < component.DefinitionCount; i++)
            {
                Assert.IsTrue(component.TryGetDefinition(i, out CharacterTestBodyPartStateDefinition definition));
                Assert.IsTrue(component.TryGetCurrentEntry(i, out CharacterTestBodyPartStateEntry entry));
                Assert.AreEqual(0, entry.WoundValue);
                Assert.AreEqual(CharacterTestBodyPartWoundState.Closed, entry.WoundState);
                Assert.AreEqual(definition.MaxIntegrity, entry.CurrentIntegrity);
                Assert.AreEqual(CharacterTestBodyPartStructureState.Intact, entry.StructureState);
                Assert.AreEqual(-1L, entry.LastAcceptedHitFrame);
                Assert.AreEqual(0L, entry.Revision);
            }
        }

        private static void AssertLockEqual(
            CharacterTestDamageZoneLockStateSnapshot expected,
            CharacterTestDamageZoneLockStateSnapshot actual)
        {
            Assert.AreEqual(expected.SourceEntityId, actual.SourceEntityId);
            Assert.AreEqual(expected.TargetGameplayEntityIndex, actual.TargetGameplayEntityIndex);
            Assert.AreEqual(expected.TargetGameplayEntityGeneration, actual.TargetGameplayEntityGeneration);
            Assert.AreEqual(expected.TargetCombatEntityId, actual.TargetCombatEntityId);
            Assert.AreEqual(expected.ResolvedActionRevision, actual.ResolvedActionRevision);
            Assert.AreEqual(expected.ZoneId, actual.ZoneId);
            Assert.AreEqual(expected.BodyPartStateRevision, actual.BodyPartStateRevision);
            Assert.AreEqual(expected.SelectedFrame, actual.SelectedFrame);
            Assert.AreEqual(expected.SwitchReason, actual.SwitchReason);
        }

        private static CharacterTestDamageZoneLockStateSnapshot CreateLock(
            CharacterTestCharacterEntity entity,
            long bodyPartStateRevision)
        {
            return CreateLock(
                entity.Id.Value,
                entity.Id.Value,
                bodyPartStateRevision,
                entity.GameplayEntityId.Index,
                entity.GameplayEntityId.Generation);
        }

        private static CharacterTestDamageZoneLockStateSnapshot CreateLock(
            int sourceEntityId,
            int targetEntityId,
            long bodyPartStateRevision,
            int targetGameplayIndex = 1,
            int targetGameplayGeneration = 1)
        {
            return new CharacterTestDamageZoneLockStateSnapshot
            {
                SourceEntityId = sourceEntityId,
                TargetGameplayEntityIndex = targetGameplayIndex,
                TargetGameplayEntityGeneration = targetGameplayGeneration,
                TargetCombatEntityId = CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart
                    + targetEntityId,
                ResolvedActionRevision = 1,
                ZoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0092,
                HitZoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0027,
                LogicalHurtboxId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0053,
                OpportunityLevel = (int)CharacterTestAttackOpportunityLevel.CurrentPose,
                EarliestHitTick = 0,
                BindingRevision = 1,
                HurtboxRevision = 1,
                WorldRevision = 1,
                ZoneRevision = 1,
                BodyPartStateRevision = bodyPartStateRevision,
                SelectedFrame = 0L,
                SwitchReason = (int)CharacterTestDamageZoneSwitchReason.InitialSelection
            };
        }

        private static CharacterTestRuntimeAiPlannerStateSnapshot CreatePlannerState(
            params CharacterTestDamageZoneLockStateSnapshot[] locks)
        {
            CharacterTestRuntimeAiPlannerStateSnapshot result =
                CharacterTestRuntimeAiPlannerStateSnapshot.Empty();
            result.DamageZoneLocks = locks;
            return result;
        }

        private static long HashPlanner(CharacterTestRuntimeAiPlannerStateSnapshot snapshot)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerState, 2);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(accumulator, snapshot);
            return accumulator.ToHash();
        }
    }
}

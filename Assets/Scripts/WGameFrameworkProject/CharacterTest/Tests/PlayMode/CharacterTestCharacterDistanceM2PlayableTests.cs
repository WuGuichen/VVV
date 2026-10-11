using System;
using System.Collections;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestCharacterDistanceM2PlayableTests
    {
        private const double TickSeconds = 1d / 60d;

        [UnityTest]
        public IEnumerator Dragon14_FormalV2PackageBuildsFiveRuntimeDamageZones()
        {
            yield return null;
            CharacterTestCharacterDefinition dragon = FindDragon14();
            using (GameSlice slice = CreateSlice())
            {
                QueueSpawn(slice, dragon.CharacterId, teamId: 1, x: 0.95f,
                    CharacterTestCharacterControlOwner.LocalPlayer);
                QueueSpawn(slice, CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 2, x: 0f, CharacterTestCharacterControlOwner.AiControlled);
                slice.Tick(TickSeconds);
                CharacterTestCharacterEntity dragonEntity = FindEntity(
                    slice, CharacterTestCharacterControlOwner.LocalPlayer);

                Assert.IsTrue(slice.CharacterRuntime.TryGetDamageZoneSnapshot(
                    dragonEntity.Id.Value, out CharacterTestDamageZoneSnapshot zones));
                Assert.IsTrue(zones.IsValid);
                Assert.AreEqual(5, zones.ZoneCount);
                Assert.GreaterOrEqual(zones.MemberCount, 16);
                Assert.IsTrue(zones.TryGetZone(CharacterTestAuthoredContentIds.D400_Id152, out CharacterTestDamageZoneAggregate head));
                Assert.AreEqual(2, head.MemberCount);
                Assert.AreEqual(100, head.AuthoredPriority);
            }
        }

        [UnityTest]
        public IEnumerator RuntimePlanner_ConsumesFormalZoneSelectionAndExposesAuthorityLock()
        {
            yield return null;
            CharacterTestCharacterDefinition dragon = FindDragon14();
            using (GameSlice slice = CreateSlice())
            {
                QueueSpawn(slice, dragon.CharacterId, teamId: 1, x: 0.95f,
                    CharacterTestCharacterControlOwner.LocalPlayer);
                QueueSpawn(slice, CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 2, x: 0f, CharacterTestCharacterControlOwner.AiControlled);
                slice.Tick(TickSeconds);
                slice.Tick(TickSeconds);

                CharacterTestCharacterEntity ai = FindEntity(
                    slice, CharacterTestCharacterControlOwner.AiControlled);
                CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter = GetAttackAdapter(slice);
                Assert.IsTrue(adapter.TryGetDamageZoneLock(
                    ai.Id.Value, out CharacterTestDamageZoneLock zoneLock),
                    "The real Planner must publish a formal DamageZone authority lock.");
                Assert.Greater(zoneLock.ZoneId, 0);
                Assert.Greater(zoneLock.HitZoneId, 0);
                Assert.Greater(zoneLock.LogicalHurtboxId, 0);
                Assert.AreNotEqual(0, zoneLock.BindingRevision);
                Assert.AreNotEqual(0, zoneLock.ZoneRevision);

                CharacterTestRuntimeAiPlannerStateSnapshot snapshot =
                    slice.RuntimeAiPlannerCommands.CreateStateSnapshot();
                Assert.AreEqual(1, snapshot.DamageZoneLocks.Length);
                Assert.AreEqual(zoneLock.ZoneId, snapshot.DamageZoneLocks[0].ZoneId);
            }
        }

        [UnityTest]
        public IEnumerator RuntimePlanner_SameActorKeepsExactHurtboxAndRefreshesDistance()
        {
            yield return null;
            CharacterTestCharacterDefinition dragon = FindDragon14();
            using (GameSlice slice = CreateSlice())
            {
                QueueSpawn(slice, dragon.CharacterId, teamId: 1, x: 0.95f,
                    CharacterTestCharacterControlOwner.LocalPlayer);
                QueueSpawn(slice, CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 2, x: 0f, CharacterTestCharacterControlOwner.AiControlled);
                slice.Tick(TickSeconds);
                slice.Tick(TickSeconds);

                CharacterTestCharacterEntity ai = FindEntity(
                    slice, CharacterTestCharacterControlOwner.AiControlled);
                CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter = GetAttackAdapter(slice);
                Assert.IsTrue(adapter.TryGetDamageZoneLock(
                    ai.Id.Value, out CharacterTestDamageZoneLock first));
                CharacterTestCombatTargetHandle handle = first.ToCombatTargetHandle(ai.Id.Value);
                Assert.IsTrue(slice.CharacterRuntime.PerceptionFrameCache.TryGetLockedHurtboxDistance(
                    slice.CurrentFrame, handle, out CharacterTestLockedHurtboxDistanceSample firstDistance));
                Assert.IsTrue(firstDistance.HasDistance);
                Assert.GreaterOrEqual(firstDistance.SurfaceGapMillimeters, 0L);
                CharacterTestLockedHurtboxDistanceSample warmed = default;
                bool warmedSuccess = false;
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int queryIndex = 0; queryIndex < 4096; queryIndex++)
                {
                    warmedSuccess = slice.CharacterRuntime.PerceptionFrameCache
                        .TryGetLockedHurtboxDistance(
                            slice.CurrentFrame,
                            handle,
                            out warmed);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.IsTrue(warmedSuccess);
                Assert.AreEqual(firstDistance.SurfaceGapMillimeters,
                    warmed.SurfaceGapMillimeters);
                Assert.AreEqual(0L, allocated,
                    "Warmed exact-Hurtbox cache queries must allocate 0 B.");

                slice.Tick(TickSeconds);

                Assert.IsTrue(adapter.TryGetDamageZoneLock(
                    ai.Id.Value, out CharacterTestDamageZoneLock next));
                Assert.AreEqual(first.TargetGameplayEntityId, next.TargetGameplayEntityId);
                Assert.AreEqual(first.LogicalHurtboxId, next.LogicalHurtboxId);
                Assert.AreEqual(0,
                    slice.CharacterRuntime.PerceptionFrameCache.CompletedInitialHurtboxScanCount,
                    "A same-actor planner tick must not scan alternative Hurtboxes.");
                Assert.IsTrue(slice.CharacterRuntime.PerceptionFrameCache.TryGetLockedHurtboxDistance(
                    slice.CurrentFrame,
                    next.ToCombatTargetHandle(ai.Id.Value),
                    out CharacterTestLockedHurtboxDistanceSample nextDistance));
                Assert.IsTrue(nextDistance.HasDistance);
                Assert.Greater(nextDistance.GeometryGeneration, firstDistance.GeometryGeneration);
            }
        }

        [UnityTest]
        public IEnumerator RuntimePlanner_HurtboxOnlyCharacterBuildsExactWitnessWithoutDamageZone()
        {
            yield return null;
            using (GameSlice slice = CreateSlice())
            {
                QueueSpawn(
                    slice,
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 1,
                    x: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer);
                QueueSpawn(
                    slice,
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    teamId: 2,
                    x: 3f,
                    CharacterTestCharacterControlOwner.AiControlled);
                slice.Tick(TickSeconds);
                slice.Tick(TickSeconds);

                CharacterTestCharacterEntity ai = FindEntity(
                    slice,
                    CharacterTestCharacterControlOwner.AiControlled);
                CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter =
                    GetAttackAdapter(slice);
                Assert.IsTrue(adapter.TryGetDamageZoneLock(
                    ai.Id.Value,
                    out CharacterTestDamageZoneLock targetLock));
                Assert.AreEqual(0, targetLock.ZoneId);
                Assert.Greater(targetLock.LogicalHurtboxId, 0);

                CharacterTestCombatTargetHandle handle =
                    targetLock.ToCombatTargetHandle(ai.Id.Value);
                Assert.IsTrue(slice.CharacterRuntime.PerceptionFrameCache
                    .TryGetLockedHurtboxDistance(
                        slice.CurrentFrame,
                        handle,
                        out CharacterTestLockedHurtboxDistanceSample distance));
                Assert.IsTrue(distance.HasDistance);
                Assert.AreNotEqual(FixVector3.Zero, distance.TargetWitnessPoint);

                CharacterTestRuntimeAiPlannerSnapshot planner =
                    slice.RuntimeAiPlannerCommands.CreateSnapshot();
                CharacterTestRuntimeAiPlannerAgentSnapshot agent = default;
                bool foundAgent = false;
                for (int i = 0; i < planner.Agents.Count; i++)
                {
                    if (planner.Agents[i].EntityId == ai.Id.Value)
                    {
                        agent = planner.Agents[i];
                        foundAgent = true;
                        break;
                    }
                }
                Assert.IsTrue(foundAgent);
                Assert.IsTrue(agent.TargetDistanceAvailable);
                Assert.IsTrue(agent.HasScenePreviewDistanceWitness);
                Assert.AreEqual(targetLock.LogicalHurtboxId, agent.TargetLogicalHurtboxId);

                for (int frame = 0; frame < 36; frame++)
                    slice.Tick(TickSeconds);
                Assert.IsTrue(slice.CharacterRuntime.TryGetDamageGeometrySnapshot(
                    ai.Id.Value,
                    out CharacterTestDamageGeometrySnapshot aiGeometry));
                for (int colliderIndex = 0;
                     colliderIndex < aiGeometry.ColliderCount;
                     colliderIndex++)
                {
                    CharacterTestDamageGeometryColliderMetadata collider =
                        aiGeometry.GetCollider(colliderIndex);
                    FixVector3 center = (collider.WorldMin + collider.WorldMax) * Fix64.Half;
                    double centerX = center.X.RawValue / (double)Fix64.Scale;
                    double centerZ = center.Z.RawValue / (double)Fix64.Scale;
                    double deltaX = centerX - ai.Transform.X;
                    double deltaZ = centerZ - ai.Transform.Z;
                    double planarOffset = Math.Sqrt(
                        deltaX * deltaX + deltaZ * deltaZ);
                    Assert.LessOrEqual(
                        planarOffset,
                        1.5d,
                        collider.LogicalColliderId
                        + " retained locomotion RootT on top of the authority root.");
                }
            }
        }

        [UnityTest]
        public IEnumerator PlayerTargetLock_CommitsCompositeHurtboxAndSnapshotRoundTripsIdentity()
        {
            yield return null;
            CharacterTestCharacterDefinition dragon = FindDragon14();
            using (GameSlice source = CreateSlice())
            using (GameSlice destination = CreateSlice())
            {
                QueueSpawn(source, dragon.CharacterId, 2, 0.95f,
                    CharacterTestCharacterControlOwner.AiControlled);
                QueueSpawn(source, CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    1, 0f, CharacterTestCharacterControlOwner.LocalPlayer);
                QueueSpawn(destination, dragon.CharacterId, 2, 0.95f,
                    CharacterTestCharacterControlOwner.AiControlled);
                QueueSpawn(destination, CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    1, 0f, CharacterTestCharacterControlOwner.LocalPlayer);
                source.Tick(TickSeconds);
                destination.Tick(TickSeconds);

                CharacterTestCharacterEntity player = FindEntity(
                    source, CharacterTestCharacterControlOwner.LocalPlayer);
                RuntimeCommandValidationResult lockResult = source.EnqueueToggleTargetLock(
                    player.Id.Value);
                Assert.IsTrue(lockResult.Success, lockResult.Error.Message);
                source.Tick(TickSeconds);

                Assert.IsTrue(source.TargetLockEnabled);
                Assert.IsTrue(source.LockedCombatTarget.HasTarget);
                Assert.Greater(source.LockedCombatTarget.LogicalHurtboxId, 0);

                var sourceParticipant = new CharacterTestNetworkSnapshotParticipant(
                    source.CharacterRuntime,
                    source.RuntimeCommands,
                    runtimeAiPlannerCommandModule: source.RuntimeAiPlannerCommands);
                var destinationParticipant = new CharacterTestNetworkSnapshotParticipant(
                    destination.CharacterRuntime,
                    destination.RuntimeCommands,
                    runtimeAiPlannerCommandModule: destination.RuntimeAiPlannerCommands);
                CharacterTestNetworkSnapshotDocument document =
                    sourceParticipant.CaptureRollbackDocument(source.CurrentFrame);
                Assert.NotNull(document.LockedCombatTarget);
                Assert.IsTrue(document.LockedCombatTarget.TryToHandle(out _),
                    "Captured composite target DTO must round-trip to a Handle. source="
                    + document.LockedCombatTarget.SourceEntityId
                    + " target=" + document.LockedCombatTarget.TargetEntityId
                    + " gameplay=" + document.LockedCombatTarget.TargetGameplayEntityIndex
                    + "/" + document.LockedCombatTarget.TargetGameplayEntityGeneration
                    + " combat=" + document.LockedCombatTarget.TargetCombatEntityId
                    + " zone=" + document.LockedCombatTarget.DamageZoneId
                    + " hit=" + document.LockedCombatTarget.HitZoneId
                    + " hurtbox=" + document.LockedCombatTarget.LogicalHurtboxId
                    + " binding=" + document.LockedCombatTarget.BindingRevisionAtSelection
                    + " frame=" + document.LockedCombatTarget.SelectedFrame);
                Assert.AreEqual(
                    document.LockedTargetEntityId,
                    document.LockedCombatTarget.TargetEntityId);
                document.NormalizeForSchema(document.SchemaVersion);
                Assert.IsTrue(document.TargetLockEnabled);
                Assert.NotNull(document.LockedCombatTarget);
                Assert.IsTrue(document.LockedCombatTarget.TryToHandle(out _));
                NetworkSnapshotApplyResult apply = destinationParticipant.ApplyRollbackDocument(
                    source.CurrentFrame,
                    document,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(apply.Success, apply.Message);
                Assert.IsTrue(destination.LockedCombatTarget.HasTarget);
                Assert.AreEqual(
                    source.LockedCombatTarget.TargetGameplayEntityId,
                    destination.LockedCombatTarget.TargetGameplayEntityId);
                Assert.AreEqual(
                    source.LockedCombatTarget.LogicalHurtboxId,
                    destination.LockedCombatTarget.LogicalHurtboxId);
            }
        }

        [UnityTest]
        public IEnumerator NetworkRollbackSnapshot_RestoresZoneLockAndStateHash()
        {
            yield return null;
            CharacterTestCharacterDefinition dragon = FindDragon14();
            using (GameSlice source = CreateSlice())
            using (GameSlice destination = CreateSlice())
            {
                QueueSpawn(source, dragon.CharacterId, 1, 0.95f, CharacterTestCharacterControlOwner.LocalPlayer);
                QueueSpawn(source, CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, 2, 0f, CharacterTestCharacterControlOwner.AiControlled);
                QueueSpawn(destination, dragon.CharacterId, 1, 0.95f, CharacterTestCharacterControlOwner.LocalPlayer);
                QueueSpawn(destination, CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, 2, 0f, CharacterTestCharacterControlOwner.AiControlled);
                source.Tick(TickSeconds);
                source.Tick(TickSeconds);
                destination.Tick(TickSeconds);
                destination.Tick(TickSeconds);

                var sourceParticipant = new CharacterTestNetworkSnapshotParticipant(
                    source.CharacterRuntime, source.RuntimeCommands,
                    runtimeAiPlannerCommandModule: source.RuntimeAiPlannerCommands);
                var destinationParticipant = new CharacterTestNetworkSnapshotParticipant(
                    destination.CharacterRuntime, destination.RuntimeCommands,
                    runtimeAiPlannerCommandModule: destination.RuntimeAiPlannerCommands);
                var snapshot = sourceParticipant.CaptureSnapshot(source.CurrentFrame);
                Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                    snapshot.SchemaVersion);
                var apply = destinationParticipant.ApplyRollbackSnapshot(
                    source.CurrentFrame, snapshot, preserveLocalPredictedEntity: false);
                Assert.IsTrue(apply.Success, apply.Message);

                CharacterTestCharacterEntity sourceAi = FindEntity(source, CharacterTestCharacterControlOwner.AiControlled);
                CharacterTestCharacterEntity destinationAi = FindEntity(destination, CharacterTestCharacterControlOwner.AiControlled);
                Assert.IsTrue(GetAttackAdapter(source).TryGetDamageZoneLock(sourceAi.Id.Value, out CharacterTestDamageZoneLock expected));
                Assert.IsTrue(GetAttackAdapter(destination).TryGetDamageZoneLock(destinationAi.Id.Value, out CharacterTestDamageZoneLock actual));
                Assert.AreEqual(expected.ZoneId, actual.ZoneId);
                Assert.AreEqual(expected.LogicalHurtboxId, actual.LogicalHurtboxId);
                Assert.AreEqual(expected.SwitchReason, actual.SwitchReason);
                Assert.AreEqual(
                    sourceParticipant.ComputeStateHash(source.CurrentFrame),
                    destinationParticipant.ComputeStateHash(source.CurrentFrame));

                source.Tick(TickSeconds);
                destination.Tick(TickSeconds);
                Assert.AreEqual(source.CurrentFrame, destination.CurrentFrame);
                Assert.Greater(source.RuntimeCommands.LastDrainedCommands.Count, 0,
                    "The post-rollback branch must execute a real next-tick command stream.");
                Assert.AreEqual(
                    source.RuntimeCommands.LastDrainedCommands.Count,
                    destination.RuntimeCommands.LastDrainedCommands.Count);
                for (int commandIndex = 0;
                    commandIndex < source.RuntimeCommands.LastDrainedCommands.Count;
                    commandIndex++)
                {
                    Assert.AreEqual(
                        source.RuntimeCommands.LastDrainedCommands[commandIndex],
                        destination.RuntimeCommands.LastDrainedCommands[commandIndex],
                        "next-tick command index=" + commandIndex);
                }

                Assert.IsTrue(GetAttackAdapter(source).TryGetDamageZoneLock(
                    sourceAi.Id.Value, out CharacterTestDamageZoneLock expectedNext));
                Assert.IsTrue(GetAttackAdapter(destination).TryGetDamageZoneLock(
                    destinationAi.Id.Value, out CharacterTestDamageZoneLock actualNext));
                Assert.AreEqual(expectedNext.ZoneId, actualNext.ZoneId);
                Assert.AreEqual(expectedNext.LogicalHurtboxId, actualNext.LogicalHurtboxId);
                Assert.AreEqual(expectedNext.SwitchReason, actualNext.SwitchReason);
                Assert.AreEqual(
                    sourceParticipant.ComputeStateHash(source.CurrentFrame),
                    destinationParticipant.ComputeStateHash(destination.CurrentFrame));
            }
        }

        private static CharacterTestCharacterDefinition FindDragon14()
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            for (int i = 0; i < registry.Definitions.Count; i++)
            {
                CharacterTestCharacterDefinition value = registry.Definitions[i];
                if (value.GeneratedPackageRoot.Replace('\\', '/').EndsWith(
                    "/000003", StringComparison.OrdinalIgnoreCase))
                    return value;
            }
            Assert.Fail("Imported 000003 definition was not discovered.");
            return default;
        }

        private static GameSlice CreateSlice()
        {
            return new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                ActionConfigOptions = CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                PhysicsRuntime = new CharacterTestCharacterPhysicsRuntime(
                    new CombatPhysicsWorld(), CombatPhysicsLayerMask.FromLayer(1),
                    new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                    Fix64.FromRatio(1, 100), Fix64.Zero, 1, "cd-m2.playmode"),
                RuntimeAiPlannerProfile = CharacterTestRuntimeAiPlannerProfileSource.CreateDefault(),
                StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>()
            });
        }

        private static void QueueSpawn(
            GameSlice slice,
            int characterId,
            int teamId,
            float x,
            CharacterTestCharacterControlOwner owner)
        {
            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SpawnCharacter(
                slice.CurrentFrame,
                CharacterTestRuntimeCommandSources.System,
                new CharacterTestCharacterSpawnSpec(characterId, teamId, x, 1f, 0f, owner),
                traceId: new RuntimeTraceIdentity(9002, CharacterTestRuntimeCommandSources.System, slice.CurrentFrame.Value, characterId));
            RuntimeCommandValidationResult result = slice.EnqueueRuntimeCommand(command);
            Assert.IsTrue(result.Success, result.Error.Message);
        }

        private static CharacterTestCharacterEntity FindEntity(
            GameSlice slice,
            CharacterTestCharacterControlOwner owner)
        {
            for (int i = slice.CharacterEntities.Count - 1; i >= 0; i--)
            {
                if (slice.CharacterEntities[i].Control.Owner == owner)
                    return slice.CharacterEntities[i];
            }
            Assert.Fail("Expected spawned entity was not found.");
            return null;
        }

        private static CharacterTestRuntimeAiPlannerAttackOpportunityAdapter GetAttackAdapter(GameSlice slice)
        {
            FieldInfo field = typeof(CharacterTestRuntimeAiPlannerCommandModule).GetField(
                "_attackOpportunityAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (CharacterTestRuntimeAiPlannerAttackOpportunityAdapter)field.GetValue(
                slice.RuntimeAiPlannerCommands);
        }
    }
}

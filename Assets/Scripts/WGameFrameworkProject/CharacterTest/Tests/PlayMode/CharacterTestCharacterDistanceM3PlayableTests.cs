using System;
using System.Collections;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestCharacterDistanceM3PlayableTests
    {
        private const double TickSeconds = 1d / 60d;

        [UnityTest]
        public IEnumerator Dragon14_FormalV3PackageBuildsSixAuthoredEngagementAnchors()
        {
            yield return null;
            CharacterTestCharacterDefinition dragon = FindDragon14();
            using (GameSlice slice = CreateSlice())
            {
                QueueSpawn(slice, dragon.CharacterId, 1, 4f,
                    CharacterTestCharacterControlOwner.None);
                slice.Tick(TickSeconds);
                CharacterTestCharacterEntity target = FindEntity(
                    slice, CharacterTestCharacterControlOwner.None);
                Assert.IsTrue(slice.CharacterRuntime.TryGetRuntimeGeometryBinding(
                    target.Id.Value, out var geometry));
                Assert.AreEqual(CharacterEngagementAnchorMode.AuthoredAnchors,
                    geometry.EngagementAnchorMode);
                Assert.AreEqual(6, geometry.EngagementAnchors.Length);
                Assert.Greater(geometry.EngagementAnchorRevision, 0);
                Assert.That(geometry.EngagementAnchorContentHash, Does.StartWith("sha256:"));
            }
        }

        [UnityTest]
        public IEnumerator Planner_SelectsFormalAnchorPathAndPublishesReservationDiagnostics()
        {
            yield return null;
            CharacterTestCharacterDefinition dragon = FindDragon14();
            using (GameSlice slice = CreateSlice())
            {
                QueueSpawn(slice, dragon.CharacterId, 1, 4f,
                    CharacterTestCharacterControlOwner.None);
                QueueSpawn(slice, CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    2, -4f, CharacterTestCharacterControlOwner.AiControlled);
                for (int i = 0; i < 3; i++)
                    slice.Tick(TickSeconds);

                CharacterTestCharacterEntity ai = FindEntity(
                    slice, CharacterTestCharacterControlOwner.AiControlled);
                CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter = GetAttackAdapter(slice);
                var completedPlannerFrame = new RuntimeFrame(slice.CurrentFrame.Value - 1);
                Assert.IsTrue(adapter.TryGetEngagementSelection(
                    ai.Id.Value, completedPlannerFrame,
                    out CharacterTestEngagementCandidateResult selection));
                Assert.Greater(selection.Pose.AnchorId, 0);
                Assert.Greater(selection.Pose.ZoneId, 0);
                Assert.AreEqual(CharacterTestPathStatus.Success, selection.Path.Status);
                Assert.GreaterOrEqual(selection.Path.TotalCostMillimeters, 0);
                CharacterTestEngagementReservationStateSnapshot[] reservations =
                    adapter.CaptureEngagementReservations();
                Assert.AreEqual(1, reservations.Length);
                Assert.AreEqual(ai.Id.Value, reservations[0].SourceEntityId);
                Assert.AreEqual(selection.Pose.AnchorId, reservations[0].AnchorId);
                CharacterTestEngagementDiagnosticSnapshot[] diagnostics =
                    adapter.CaptureEngagementDiagnostics();
                Assert.AreEqual(1, diagnostics.Length);
                Assert.AreEqual(CharacterTestEngagementCandidateStatus.Success, diagnostics[0].Status);
                Assert.IsTrue(diagnostics[0].HasReservation);
                Assert.AreEqual(selection.Path.TotalCostMillimeters,
                    diagnostics[0].PathCostMillimeters);
            }
        }

        [UnityTest]
        public IEnumerator JointEvaluation_CandidatePoseBudget_ReportsTypedTruncation()
        {
            yield return null;
            CharacterTestCharacterDefinition dragon = FindDragon14();
            using (GameSlice slice = CreateSlice())
            {
                SpawnPair(slice, dragon);
                for (int i = 0; i < 3; i++)
                    slice.Tick(TickSeconds);

                CharacterTestCharacterEntity source = FindEntity(
                    slice, CharacterTestCharacterControlOwner.AiControlled);
                CharacterTestCharacterEntity target = FindEntity(
                    slice, CharacterTestCharacterControlOwner.None);
                CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter =
                    GetAttackAdapter(slice);
                RuntimeFrame plannerFrame = new RuntimeFrame(slice.CurrentFrame.Value - 1);
                Assert.IsTrue(adapter.TryResolvePlannerPrimaryAttackAction(
                    source, plannerFrame, out CharacterActionConfig action));
                Assert.IsTrue(adapter.TryPrepareJointPrimaryAttack(
                    source, target, action.ActionId, plannerFrame,
                    out CharacterTestRuntimeAiJointPreparedAttack prepared,
                    out CharacterTestAttackOpportunityFailureReason failure), failure.ToString());
                CharacterTestRuntimeAiJointEvaluationBudgetDiagnostics poseBudget = default;
                int zoneId = 0;
                for (int i = 0; i < prepared.Zones.ZoneCount; i++)
                {
                    int candidateZoneId = prepared.Zones.GetZone(i).ZoneId;
                    Assert.IsTrue(adapter.TryEvaluatePreparedJointPrimaryAttack(
                        prepared, candidateZoneId, 0,
                        CharacterTestRuntimeAiJointGenerationBudget.HardPathQueryLimit,
                        out _, out _, out _, out poseBudget));
                    if (poseBudget.TruncationPhase
                        == CharacterTestRuntimeAiJointTruncationPhase.CandidatePose)
                    {
                        zoneId = candidateZoneId;
                        break;
                    }
                }
                Assert.Greater(zoneId, 0, "Expected an authored Zone requiring translation.");
                Assert.AreEqual(CharacterTestRuntimeAiJointTruncationPhase.CandidatePose,
                    poseBudget.TruncationPhase);
                Assert.AreEqual(1, poseBudget.TruncationObservedCount);
                Assert.AreEqual(0, poseBudget.TruncationAcceptedCount);
                Assert.AreEqual(0, poseBudget.TruncationLimit);

            }
        }

        [UnityTest]
        public IEnumerator NetworkRollback_RestoresReservationHashAndNextTickCommands()
        {
            yield return null;
            CharacterTestCharacterDefinition dragon = FindDragon14();
            using (GameSlice source = CreateSlice())
            using (GameSlice destination = CreateSlice())
            {
                SpawnPair(source, dragon);
                SpawnPair(destination, dragon);
                for (int i = 0; i < 3; i++)
                {
                    source.Tick(TickSeconds);
                    destination.Tick(TickSeconds);
                }
                var sourceParticipant = new CharacterTestNetworkSnapshotParticipant(
                    source.CharacterRuntime, source.RuntimeCommands,
                    runtimeAiPlannerCommandModule: source.RuntimeAiPlannerCommands);
                var destinationParticipant = new CharacterTestNetworkSnapshotParticipant(
                    destination.CharacterRuntime, destination.RuntimeCommands,
                    runtimeAiPlannerCommandModule: destination.RuntimeAiPlannerCommands);
                var snapshot = sourceParticipant.CaptureSnapshot(source.CurrentFrame);
                Assert.AreEqual(CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                    snapshot.SchemaVersion);
                Assert.AreEqual(1,
                    GetAttackAdapter(source).CaptureEngagementReservations().Length);
                var apply =
                    destinationParticipant.ApplyRollbackSnapshot(
                        source.CurrentFrame, snapshot, preserveLocalPredictedEntity: false);
                Assert.IsTrue(apply.Success, apply.Message);
                Assert.AreEqual(
                    sourceParticipant.ComputeStateHash(source.CurrentFrame),
                    destinationParticipant.ComputeStateHash(destination.CurrentFrame));

                string preTickAuthority = FormatAuthorityComparison(source, destination);
                source.Tick(TickSeconds);
                destination.Tick(TickSeconds);
                Assert.AreEqual(source.RuntimeCommands.LastDrainedCommands.Count,
                    destination.RuntimeCommands.LastDrainedCommands.Count);
                string plannerDiagnostic = FormatPlannerComparison(
                    source.RuntimeAiPlannerCommands.CreateSnapshot(),
                    destination.RuntimeAiPlannerCommands.CreateSnapshot())
                    + " jointExpected["
                    + FormatJointDiagnostics(
                        source.RuntimeAiPlannerCommands
                            .CaptureJointSelectionDiagnosticsForDiagnostics())
                    + "] jointActual["
                    + FormatJointDiagnostics(
                        destination.RuntimeAiPlannerCommands
                            .CaptureJointSelectionDiagnosticsForDiagnostics())
                    + "] preTickAuthority[" + preTickAuthority + "]"
                    + " postTickAuthority[" + FormatAuthorityComparison(source, destination) + "]";
                for (int i = 0; i < source.RuntimeCommands.LastDrainedCommands.Count; i++)
                {
                    AssertRuntimeCommandEqual(
                        source.RuntimeCommands.LastDrainedCommands[i],
                        destination.RuntimeCommands.LastDrainedCommands[i],
                        i,
                        plannerDiagnostic);
                }
                Assert.AreEqual(
                    sourceParticipant.ComputeStateHash(source.CurrentFrame),
                    destinationParticipant.ComputeStateHash(destination.CurrentFrame),
                    "post-tick planner state expected["
                    + FormatPlannerAuthorityState(
                        source.RuntimeAiPlannerCommands.CreateStateSnapshot())
                    + "] actual["
                    + FormatPlannerAuthorityState(
                        destination.RuntimeAiPlannerCommands.CreateStateSnapshot())
                    + "]");
            }
        }

        private static string FormatPlannerAuthorityState(
            CharacterTestRuntimeAiPlannerStateSnapshot snapshot)
        {
            CharacterTestEngagementReservationStateSnapshot reservation =
                snapshot?.EngagementReservations != null
                && snapshot.EngagementReservations.Length > 0
                    ? snapshot.EngagementReservations[0]
                    : null;
            CharacterTestDamageZoneLockStateSnapshot zoneLock =
                snapshot?.DamageZoneLocks != null && snapshot.DamageZoneLocks.Length > 0
                    ? snapshot.DamageZoneLocks[0]
                    : null;
            return "reservation=" + (reservation == null
                    ? "missing"
                    : reservation.SourceEntityId
                        + "/" + reservation.TargetEntityId
                        + ",updated=" + reservation.UpdatedFrame
                        + ",zoneBinding=" + reservation.ZoneBindingRevision
                        + ",zoneRevision=" + reservation.ZoneRevision
                        + ",physics=" + reservation.CombatPhysicsRevision
                        + ",graph=" + reservation.GraphRevision)
                + ",lock=" + (zoneLock == null
                    ? "missing"
                    : zoneLock.SourceEntityId
                        + "/" + zoneLock.TargetCombatEntityId
                        + ",binding=" + zoneLock.BindingRevision
                        + ",hurtbox=" + zoneLock.HurtboxRevision
                        + ",world=" + zoneLock.WorldRevision
                        + ",zone=" + zoneLock.ZoneRevision
                        + ",selected=" + zoneLock.SelectedFrame);
        }

        private static void AssertRuntimeCommandEqual(
            RuntimeCommand expected,
            RuntimeCommand actual,
            int commandIndex,
            string plannerDiagnostic)
        {
            string prefix = "next-tick command index=" + commandIndex
                + " planner=" + plannerDiagnostic + " field=";
            Assert.AreEqual(expected.Frame, actual.Frame, prefix + nameof(RuntimeCommand.Frame));
            Assert.AreEqual(expected.SourceId, actual.SourceId, prefix + nameof(RuntimeCommand.SourceId));
            Assert.AreEqual(expected.CommandId, actual.CommandId, prefix + nameof(RuntimeCommand.CommandId));
            Assert.AreEqual(expected.TargetId, actual.TargetId, prefix + nameof(RuntimeCommand.TargetId));
            Assert.AreEqual(expected.Payload0, actual.Payload0, prefix + nameof(RuntimeCommand.Payload0));
            Assert.AreEqual(expected.Payload1, actual.Payload1, prefix + nameof(RuntimeCommand.Payload1));
            Assert.AreEqual(expected.Payload2, actual.Payload2, prefix + nameof(RuntimeCommand.Payload2));
            Assert.AreEqual(expected.TraceId, actual.TraceId, prefix + nameof(RuntimeCommand.TraceId));
            Assert.AreEqual(expected.Sequence, actual.Sequence, prefix + nameof(RuntimeCommand.Sequence));
            Assert.AreEqual(expected.Envelope, actual.Envelope, prefix + nameof(RuntimeCommand.Envelope));
        }

        private static string FormatPlannerComparison(
            CharacterTestRuntimeAiPlannerSnapshot expected,
            CharacterTestRuntimeAiPlannerSnapshot actual)
        {
            return "expected[" + FormatPlannerSnapshot(expected) + "] actual["
                + FormatPlannerSnapshot(actual) + "]";
        }

        private static string FormatPlannerSnapshot(CharacterTestRuntimeAiPlannerSnapshot snapshot)
        {
            if (snapshot.Agents == null || snapshot.Agents.Count == 0)
                return "missing";
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = snapshot.Agents[0];
            return "state=" + agent.ActiveStatePath
                + ",action=" + agent.SelectedActionId
                + ",kind=" + agent.SelectedActionKind
                + ",move=(" + agent.ScenePreviewDecisionMoveX
                + "," + agent.ScenePreviewDecisionMoveZ + ")"
                + ",target=" + agent.TargetEntityId
                + ",distance=" + agent.TargetDistance
                + ",path=" + agent.MovePathAvailable
                + ",candidate=" + agent.MoveCandidateIndex
                + ",hold=" + agent.MovementDirectionHoldFrames
                + ",delay=" + agent.MovementReactionDelayFrames;
        }

        private static string FormatJointDiagnostics(
            CharacterTestRuntimeAiJointDiagnosticSnapshot[] diagnostics)
        {
            if (diagnostics == null || diagnostics.Length == 0)
                return "missing";
            CharacterTestRuntimeAiJointDiagnosticSnapshot value = diagnostics[0];
            return "selected=" + value.HasSelection
                + ",action=" + value.PlannerActionId
                + ",resolved=" + value.ResolvedActionId
                + ",status=" + value.Status
                + ",failure=" + value.FailureReason
                + ",candidates=" + value.Generation.CandidateCount
                + ",evaluated=" + value.EvaluationCount
                + ",cacheHits=" + value.ResultCacheHitCount
                + ",truncation=" + value.TruncationPhase
                + ",observed=" + value.TruncationObservedCount
                + ",accepted=" + value.TruncationAcceptedCount
                + ",limit=" + value.TruncationLimit;
        }

        private static string FormatAuthorityComparison(GameSlice expected, GameSlice actual)
        {
            return "expected[" + FormatAuthority(expected) + "] actual["
                + FormatAuthority(actual) + "]";
        }

        private static string FormatAuthority(GameSlice slice)
        {
            CharacterTestCharacterEntity target = FindEntity(
                slice,
                CharacterTestCharacterControlOwner.None);
            bool hasCompleted = slice.CharacterRuntime.TryGetLastCompletedRuntimeFrame(
                out RuntimeFrame completedFrame);
            CharacterTestDamageGeometrySnapshot geometry = null;
            bool hasGeometry = target != null
                && slice.CharacterRuntime.TryGetDamageGeometrySnapshot(
                    target.Id.Value,
                    out geometry);
            bool hasBinding = target != null
                && slice.CharacterRuntime.TryGetRuntimeGeometryBinding(
                    target.Id.Value,
                    out _);
            int worldRevision = slice.CharacterRuntime.PhysicsRuntime?.World?.Revision ?? -1;
            bool classificationCurrent = hasCompleted
                && slice.CharacterRuntime.CombatTraceSystem.WorldTargetClassifications.IsCurrent(
                    completedFrame,
                    worldRevision);
            return "completed=" + hasCompleted
                + ",frame=" + completedFrame.Value
                + ",physics=" + (worldRevision >= 0)
                + ",worldRevision=" + worldRevision
                + ",target=" + (target != null ? target.Id.Value : 0)
                + ",geometry=" + hasGeometry
                + ",geometryValid=" + (hasGeometry && geometry.IsValid)
                + ",geometryFrame=" + (hasGeometry ? geometry.Frame.Value : -1)
                + ",geometryWorldRevision=" + (hasGeometry ? geometry.WorldRevision : -1)
                + ",classificationCurrent=" + classificationCurrent
                + ",binding=" + hasBinding;
        }

        private static void SpawnPair(GameSlice slice, CharacterTestCharacterDefinition dragon)
        {
            QueueSpawn(slice, dragon.CharacterId, 1, 4f,
                CharacterTestCharacterControlOwner.None);
            QueueSpawn(slice, CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                2, -4f, CharacterTestCharacterControlOwner.AiControlled);
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
            var world = new CombatPhysicsWorld();
            var groundId = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart),
                groundId,
                FixVector3.Zero));
            var physics = new CharacterTestCharacterPhysicsRuntime(
                world, CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100), Fix64.Zero, 1, "cd-m3.playmode");
            return new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                ActionConfigOptions =
                    CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                PhysicsRuntime = physics,
                NavigationBuildInput = new CharacterTestNavigationSessionBuildInput(
                    revision: 31,
                    minimumXMillimeters: -8000,
                    maximumXMillimeters: 8000,
                    minimumZMillimeters: -6000,
                    maximumZMillimeters: 6000,
                    planeYMillimeters: 0,
                    cellSizeMillimeters: 1000,
                    connectDiagonals: true,
                    maxNodeCount: 256,
                    maxEdgeCount: 2048,
                    maxEndpointSnapDistanceMillimeters: 2000,
                    maxNodeExpansions: 256,
                    maxPathNodeCount: 128),
                NavigationInputResult = new CharacterTestNavigationSceneInputResult(
                    CharacterTestNavigationSceneInputStatus.Success),
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
                new CharacterTestCharacterSpawnSpec(characterId, teamId, x, 0f, 0f, owner),
                traceId: new RuntimeTraceIdentity(9003, CharacterTestRuntimeCommandSources.System, slice.CurrentFrame.Value, characterId));
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

        private static CharacterTestRuntimeAiPlannerAttackOpportunityAdapter GetAttackAdapter(
            GameSlice slice)
        {
            FieldInfo field = typeof(CharacterTestRuntimeAiPlannerCommandModule).GetField(
                "_attackOpportunityAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (CharacterTestRuntimeAiPlannerAttackOpportunityAdapter)field.GetValue(
                slice.RuntimeAiPlannerCommands);
        }
    }
}

using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestAuthorityPoseNetworkAcceptanceTests
    {
        private static readonly CharacterActionId AttackId = new CharacterActionId(940000);
        private static readonly RuntimeTraceIdentity PlannerTraceId = new RuntimeTraceIdentity(1200101);
        private const int HurtboxId = CharacterTestAuthoredContentIds.D400_Id133;

        [Test]
        public void Dragon14Schema33_RollbackPlusOnePreservesPoseHurtboxBakedSocketTraceAndPlannerHash()
        {
            using (GameSlice source = CreateSlice(spawnPair: true))
            using (GameSlice rollback = CreateSlice(spawnPair: false))
            {
                Tick(source, 2);
                Tick(rollback, 2);
                Assert.AreEqual(2, source.CharacterEntities.Count);
                CharacterTestCharacterEntity sourceActor = source.CharacterEntities[0];
                Assert.That(
                    sourceActor.Identity.GeneratedPackageRoot.Replace('\\', '/'),
                    Does.EndWith("/000003"));

                RuntimeCommandValidationResult start = source.EnqueueRuntimeCommand(
                    CharacterTestRuntimeCommandFactory.SetControlIntent(
                        source.CurrentFrame,
                        CharacterTestRuntimeCommandSources.PlayerInput,
                        sourceActor.Id.Value,
                        Vector2.zero,
                        jumpPressed: false,
                        jumpHeld: false,
                        jumpReleased: false,
                        attackPrimaryPressed: true,
                        traceId: new RuntimeTraceIdentity(913899)));
                Assert.IsTrue(start.Success, start.Error.Message);
                TickUntilLocalFrame(
                    source,
                    rollback,
                    sourceActor,
                    expectedLocalFrame: 35,
                    maximumTicks: 80);

                CharacterTestResolvedAuthorityPoseFrame checkpointPose =
                    sourceActor.ResolvedAuthorityPoseFrame;
                Assert.IsTrue(checkpointPose.IsPublished);
                Assert.AreEqual(12, checkpointPose.Count);
                long checkpointPoseRevision = checkpointPose.Revision;
                RuntimeFrame restoreFrame = source.CurrentFrame;

                CharacterTestNetworkSnapshotParticipant sourceParticipant =
                    source.Services.Network.CreateSnapshotParticipant();
                CharacterTestNetworkSnapshotParticipant rollbackParticipant =
                    rollback.Services.Network.CreateSnapshotParticipant();
                CharacterTestNetworkSnapshotDocument checkpoint =
                    sourceParticipant.CaptureRollbackDocument(restoreFrame);
                NetworkSnapshotApplyResult apply = rollbackParticipant.ApplyRollbackDocument(
                    restoreFrame,
                    checkpoint,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(apply.Success, apply.Message);
                Assert.AreEqual(2, rollback.CharacterEntities.Count);
                CharacterTestCharacterEntity rollbackActor = rollback.CharacterEntities[0];
                Assert.AreEqual(sourceActor.Id.Value, rollbackActor.Id.Value);

                RuntimeCommand sourcePlannerCommand = CreatePlannerCommand(
                    restoreFrame,
                    sourceActor.Id.Value);
                RuntimeCommand rollbackPlannerCommand = CreatePlannerCommand(
                    restoreFrame,
                    rollbackActor.Id.Value);
                Assert.IsTrue(
                    source.EnqueueRuntimeCommand(sourcePlannerCommand).Success,
                    "Source Planner +1 command must enqueue.");
                Assert.IsTrue(
                    rollback.EnqueueRuntimeCommand(rollbackPlannerCommand).Success,
                    "Rollback Planner +1 command must enqueue.");

                source.Tick(0d);
                rollback.Tick(0d);

                Assert.AreEqual(restoreFrame.Value + 1L, source.CurrentFrame.Value);
                Assert.AreEqual(source.CurrentFrame.Value, rollback.CurrentFrame.Value);
                Assert.AreEqual(AttackId, sourceActor.Action.ActiveActionId);
                Assert.AreEqual(36, sourceActor.Action.LocalFrame);
                Assert.AreEqual(sourceActor.Action.LocalFrame, rollbackActor.Action.LocalFrame);
                AssertPlannerCommandDrained(source.RuntimeCommands.LastDrainedCommands);
                AssertPlannerCommandDrained(rollback.RuntimeCommands.LastDrainedCommands);

                CharacterTestResolvedAuthorityPoseFrame sourcePose =
                    sourceActor.ResolvedAuthorityPoseFrame;
                CharacterTestResolvedAuthorityPoseFrame rollbackPose =
                    rollbackActor.ResolvedAuthorityPoseFrame;
                Assert.AreEqual(restoreFrame.Value, sourcePose.RuntimeFrame);
                Assert.Greater(sourcePose.Revision, checkpointPoseRevision);
                AssertResolvedPoseEqual(sourcePose, rollbackPose);

                Assert.IsTrue(source.CharacterRuntime.TryGetDamageGeometrySnapshot(
                    sourceActor.Id.Value,
                    out CharacterTestDamageGeometrySnapshot sourceHurtboxes));
                Assert.IsTrue(rollback.CharacterRuntime.TryGetDamageGeometrySnapshot(
                    rollbackActor.Id.Value,
                    out CharacterTestDamageGeometrySnapshot rollbackHurtboxes));
                Assert.AreEqual(18, sourceHurtboxes.ColliderCount);
                Assert.AreEqual(sourceHurtboxes.ColliderCount, rollbackHurtboxes.ColliderCount);
                Assert.AreEqual(restoreFrame.Value, sourceHurtboxes.Frame.Value);
                Assert.AreEqual(sourceHurtboxes.Frame.Value, rollbackHurtboxes.Frame.Value);
                Assert.IsTrue(sourceHurtboxes.TryGetCollider(
                    HurtboxId,
                    out CharacterTestDamageGeometryColliderMetadata sourceHurtbox));
                Assert.IsTrue(rollbackHurtboxes.TryGetCollider(
                    HurtboxId,
                    out CharacterTestDamageGeometryColliderMetadata rollbackHurtbox));
                TestContext.WriteLine(
                    "hurtbox.shape source.kind=" + sourceHurtbox.LocalShape.Kind
                    + " source.a=(" + sourceHurtbox.LocalShape.PointA.X.RawValue + ","
                    + sourceHurtbox.LocalShape.PointA.Y.RawValue + ","
                    + sourceHurtbox.LocalShape.PointA.Z.RawValue + ") source.b=("
                    + sourceHurtbox.LocalShape.PointB.X.RawValue + ","
                    + sourceHurtbox.LocalShape.PointB.Y.RawValue + ","
                    + sourceHurtbox.LocalShape.PointB.Z.RawValue + ") source.radius="
                    + sourceHurtbox.LocalShape.Radius.RawValue
                    + " rollback.kind=" + rollbackHurtbox.LocalShape.Kind
                    + " rollback.a=(" + rollbackHurtbox.LocalShape.PointA.X.RawValue + ","
                    + rollbackHurtbox.LocalShape.PointA.Y.RawValue + ","
                    + rollbackHurtbox.LocalShape.PointA.Z.RawValue + ") rollback.b=("
                    + rollbackHurtbox.LocalShape.PointB.X.RawValue + ","
                    + rollbackHurtbox.LocalShape.PointB.Y.RawValue + ","
                    + rollbackHurtbox.LocalShape.PointB.Z.RawValue + ") rollback.radius="
                    + rollbackHurtbox.LocalShape.Radius.RawValue);
                Assert.AreEqual(sourceHurtbox.LocalShape, rollbackHurtbox.LocalShape);
                Assert.AreEqual(sourceHurtbox.WorldMin, rollbackHurtbox.WorldMin);
                Assert.AreEqual(sourceHurtbox.WorldMax, rollbackHurtbox.WorldMax);

                CharacterTestDirectHitTracePlanSample sourceTrace =
                    FindBakedSocketTrace(source.CombatTraceSystem.DirectHitTracePlans, sourceActor);
                CharacterTestDirectHitTracePlanSample rollbackTrace =
                    FindBakedSocketTrace(rollback.CombatTraceSystem.DirectHitTracePlans, rollbackActor);
                Assert.AreEqual(36, sourceTrace.LocalFrame);
                Assert.AreEqual(sourceTrace.Kind, rollbackTrace.Kind);
                Assert.AreEqual(sourceTrace.Query.PointA, rollbackTrace.Query.PointA);
                Assert.AreEqual(sourceTrace.Query.PointB, rollbackTrace.Query.PointB);
                Assert.AreEqual(sourceTrace.Query.Radius, rollbackTrace.Query.Radius);
                Assert.Greater(sourceTrace.Query.Radius.RawValue, 0L);

                long sourceHash = sourceParticipant.ComputeStateHash(source.CurrentFrame);
                long rollbackHash = rollbackParticipant.ComputeStateHash(rollback.CurrentFrame);
                Assert.AreEqual(sourceHash, rollbackHash);
                TestContext.WriteLine(
                    "schema33.rollback+1 frame=" + restoreFrame.Value
                    + " pose_revision=" + sourcePose.Revision
                    + " hurtboxes=" + sourceHurtboxes.ColliderCount
                    + " trace_kind=" + sourceTrace.Kind
                    + " trace_local_frame=" + sourceTrace.LocalFrame
                    + " planner_source=" + CharacterTestRuntimeCommandSources.RuntimeAiPlanner
                    + " state_hash=" + sourceHash);
            }
        }

        private static GameSlice CreateSlice(bool spawnPair)
        {
            CharacterTestCharacterSpawnSpec[] startup = spawnPair
                ? new[]
                {
                    new CharacterTestCharacterSpawnSpec(
                        CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000003"),
                        teamId: 1,
                        x: 0f,
                        y: 1f,
                        z: 0f,
                        controlOwner: CharacterTestCharacterControlOwner.LocalPlayer),
                    new CharacterTestCharacterSpawnSpec(
                        CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000003"),
                        teamId: 2,
                        x: 12f,
                        y: 1f,
                        z: 12f,
                        controlOwner: CharacterTestCharacterControlOwner.None)
                }
                : Array.Empty<CharacterTestCharacterSpawnSpec>();
            var physics = new CharacterTestCharacterPhysicsRuntime(
                CharacterTestAuthorityPoseTerrainTests.CreateGroundWorld(),
                CombatPhysicsLayerMask.FromLayer(0),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                staticColliderCount: 1,
                sourceDescription: "lcc-m4-t5.schema33-network-acceptance");
            return new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                PhysicsRuntime = physics,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                StartupCharacterSpawnSpecs = startup,
                SuppressMissingStartupSpawnWarning = true
            });
        }

        private static RuntimeCommand CreatePlannerCommand(RuntimeFrame frame, int entityId)
        {
            return CharacterTestRuntimeCommandFactory.SetControlIntent(
                frame,
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                entityId,
                Vector2.zero,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: false,
                traceId: PlannerTraceId);
        }

        private static void Tick(GameSlice slice, int count)
        {
            for (int i = 0; i < count; i++)
                slice.Tick(0d);
        }

        private static void TickUntilLocalFrame(
            GameSlice source,
            GameSlice rollback,
            CharacterTestCharacterEntity actor,
            int expectedLocalFrame,
            int maximumTicks)
        {
            for (int i = 0; i < maximumTicks; i++)
            {
                source.Tick(0d);
                rollback.Tick(0d);
                if (actor.Action.ActiveActionId == AttackId
                    && actor.Action.LocalFrame == expectedLocalFrame)
                {
                    return;
                }
                if (actor.Action.ActiveActionId == AttackId
                    && actor.Action.LocalFrame > expectedLocalFrame)
                {
                    break;
                }
            }

            Assert.Fail(
                "The real attack1a frame 35 checkpoint was not reachable. action="
                + actor.Action.ActiveActionId + " localFrame=" + actor.Action.LocalFrame);
        }

        private static void AssertPlannerCommandDrained(IReadOnlyList<RuntimeCommand> commands)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                if (command.SourceId == CharacterTestRuntimeCommandSources.RuntimeAiPlanner
                    && command.TraceId == PlannerTraceId)
                {
                    Assert.AreEqual(CharacterTestRuntimeCommandIds.SetControlIntent, command.CommandId);
                    return;
                }
            }
            Assert.Fail("The expected Runtime AI Planner +1 command was not drained.");
        }

        private static void AssertResolvedPoseEqual(
            CharacterTestResolvedAuthorityPoseFrame expected,
            CharacterTestResolvedAuthorityPoseFrame actual)
        {
            Assert.AreEqual(expected.RuntimeFrame, actual.RuntimeFrame);
            Assert.AreEqual(expected.Revision, actual.Revision);
            Assert.AreEqual(expected.DefinitionRevision, actual.DefinitionRevision);
            Assert.AreEqual(expected.DefinitionContentHash, actual.DefinitionContentHash);
            Assert.AreEqual(expected.BaseSourceRevision, actual.BaseSourceRevision);
            Assert.AreEqual(expected.BaseSourceContentHash, actual.BaseSourceContentHash);
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                CharacterTestResolvedAuthorityPoseSample left = expected[i];
                CharacterTestResolvedAuthorityPoseSample right = actual[i];
                Assert.AreEqual(left.Ordinal, right.Ordinal, "ordinal " + i);
                Assert.AreEqual(left.ChannelId, right.ChannelId, "channel " + i);
                Assert.AreEqual(left.Position, right.Position, "position " + i);
                Assert.AreEqual(left.Basis.Right, right.Basis.Right, "right " + i);
                Assert.AreEqual(left.Basis.Up, right.Basis.Up, "up " + i);
                Assert.AreEqual(left.Basis.Forward, right.Basis.Forward, "forward " + i);
                Assert.AreEqual(left.Valid, right.Valid, "valid " + i);
                Assert.AreEqual(left.SourceMask, right.SourceMask, "source " + i);
                Assert.AreEqual(left.Failure, right.Failure, "failure " + i);
            }
        }

        private static CharacterTestDirectHitTracePlanSample FindBakedSocketTrace(
            CharacterTestDirectHitTracePlanCollectionView plans,
            CharacterTestCharacterEntity actor)
        {
            CombatEntityId sourceEntityId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + actor.Id.Value);
            for (int planIndex = 0; planIndex < plans.Count; planIndex++)
            {
                Assert.IsTrue(plans.TryGetPlan(planIndex, out CharacterTestDirectHitTracePlanView plan));
                if (!plan.SourceEntityId.Equals(sourceEntityId))
                    continue;
                for (int sampleIndex = 0; sampleIndex < plan.Count; sampleIndex++)
                {
                    Assert.IsTrue(plan.TryGetSample(
                        sampleIndex,
                        out CharacterTestDirectHitTracePlanSample sample));
                    if (sample.Kind == CharacterTestDirectHitTraceSampleKind.BakedSocketCapsule
                        && sample.LocalFrame == 36)
                    {
                        return sample;
                    }
                }
            }

            Assert.Fail("The actual attack1a frame 36 BakedSocketCapsule trace was not produced.");
            return default;
        }
    }
}

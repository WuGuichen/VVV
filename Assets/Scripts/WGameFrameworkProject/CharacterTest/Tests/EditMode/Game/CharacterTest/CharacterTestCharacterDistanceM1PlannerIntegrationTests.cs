using System;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
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
    /// <summary>
    /// CD-M1 planner integration checks. Adapter tests intentionally drive the
    /// CharacterTest runtime and planner adapter together. The evaluator tests
    /// inject only a frozen opportunity result to isolate planner selection
    /// semantics from the trace geometry setup.
    /// </summary>
    public sealed class CharacterTestCharacterDistanceM1PlannerIntegrationTests
    {
        [Test]
        public void PlannerAdapter_ResolvesRealLightAttackAgainstRuntimeHurtboxes()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                CharacterActionResolveResult resolved = fixture.Runtime.ActionRuntime.ResolveIntent(
                    fixture.Source.Id,
                    CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                    fixture.CompletedFrame,
                    (int)CharacterCancelInputMask.AttackPrimary);
                Assert.IsTrue(resolved.IsSuccess, resolved.RejectReason.ToString());
                Assert.NotNull(resolved.Plan);
                Assert.AreEqual(CharacterActionCategory.BasicAttack, resolved.Plan.Category);

                Assert.IsTrue(fixture.Adapter.TryEvaluatePlannerPrimaryAttack(
                    fixture.Source,
                    fixture.Target,
                    fixture.PlannerFrame,
                    out CharacterTestAttackOpportunityResult result));
                Assert.IsTrue(
                    fixture.Runtime.TryGetDamageGeometrySnapshot(
                        fixture.Target.Id.Value,
                        out CharacterTestDamageGeometrySnapshot geometry),
                    "Target geometry missing");
                Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
                Assert.AreEqual(
                    CharacterTestAttackOpportunityLevel.CurrentPose,
                    result.Level,
                    result.FailureReason
                    + " colliders=" + geometry.ColliderCount
                    + " bounds=" + geometry.WorldMin + ".." + geometry.WorldMax
                    + " sourcePos=" + fixture.Source.Transform.XMillimeters + "," + fixture.Source.Transform.ZMillimeters
                    + " targetPos=" + fixture.Target.Transform.XMillimeters + "," + fixture.Target.Transform.ZMillimeters
                    + " worldColliders=" + fixture.Runtime.PhysicsRuntime.World.ColliderCount);
                Assert.IsTrue(result.CanHitFromCurrentPose);
            }
        }

        [Test]
        public void PlannerAdapter_JointPreviewMatchesCommitWithoutWritingDecisionAuthority()
        {
            using (Fixture fixture = CreateFixture(
                sourceX: 0f,
                sourceZ: 0f,
                targetX: 0f,
                targetZ: 0.95f))
            {
                Assert.IsFalse(fixture.Adapter.TryGetEvidence(fixture.Source.Id.Value, out _));
                Assert.AreEqual(0, fixture.Adapter.CaptureZoneLocks().Length);
                Assert.AreEqual(0, fixture.Adapter.CaptureEngagementReservations().Length);

                Assert.IsTrue(fixture.Adapter.TryPreviewPlannerPrimaryAttack(
                    fixture.Source,
                    fixture.Target,
                    fixture.PlannerFrame,
                    out CharacterTestAttackOpportunityResult preview));
                Assert.AreEqual(CharacterTestAttackOpportunityLevel.CurrentPose, preview.Level);
                Assert.IsFalse(fixture.Adapter.TryGetEvidence(fixture.Source.Id.Value, out _));
                Assert.AreEqual(0, fixture.Adapter.CaptureZoneLocks().Length);
                Assert.AreEqual(0, fixture.Adapter.CaptureEngagementReservations().Length);

                Assert.IsTrue(fixture.Adapter.TryEvaluatePlannerPrimaryAttack(
                    fixture.Source,
                    fixture.Target,
                    fixture.PlannerFrame,
                    out CharacterTestAttackOpportunityResult committed));
                Assert.AreEqual(preview.Status, committed.Status);
                Assert.AreEqual(preview.Level, committed.Level);
                Assert.AreEqual(preview.Mode, committed.Mode);
                Assert.AreEqual(preview.SelectedDamageZoneId, committed.SelectedDamageZoneId);
                Assert.AreEqual(preview.EarliestCandidateHitTick, committed.EarliestCandidateHitTick);
                Assert.IsTrue(fixture.Adapter.TryGetEvidence(fixture.Source.Id.Value, out _));
            }
        }

        [Test]
        public void PlannerAdapter_ReportsTurnInPlaceAsZeroTranslationBeforeAttack()
        {
            using (Fixture fixture = CreateFixture(
                sourceX: 0f,
                sourceZ: 0f,
                targetX: 0.95f,
                targetZ: 0f))
            {
                Assert.IsTrue(fixture.Adapter.TryEvaluatePlannerPrimaryAttack(
                    fixture.Source,
                    fixture.Target,
                    fixture.PlannerFrame,
                    out CharacterTestAttackOpportunityResult result));
                Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
                Assert.AreEqual(CharacterTestAttackEvaluationMode.TurnInPlace, result.Mode);
                Assert.AreEqual(CharacterTestAttackOpportunityLevel.TurnInPlace, result.Level);
                Assert.IsFalse(result.CanHitFromCurrentPose);
                Assert.IsTrue(result.CanHitAfterTurnInPlace);
                Assert.AreNotEqual(0, result.RequiredYawDeltaMilliDegrees);
                Assert.IsFalse(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame));
            }
        }

        [Test]
        public void PlannerAdapter_BlocksMismatchedActionSetWithoutLegacyFallback()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                fixture.Source.SetEquipment(new CharacterTestCharacterEquipmentComponent(
                    useDefaultLoadout: false,
                    equipmentStateStableId: "missing.action-set"));

                Assert.IsFalse(
                    fixture.Runtime.ActionRuntime.TryGetEntityActionSet(
                        fixture.Source.Id,
                        out CharacterActionSetConfig effectiveActionSet),
                    "The mismatched equipment context must not resolve an ActionSet; resolved="
                    + (effectiveActionSet?.StableId ?? "<null>"));

                CharacterActionResolveResult resolved = fixture.Runtime.ActionRuntime.ResolveIntent(
                    fixture.Source.Id,
                    CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                    fixture.CompletedFrame,
                    (int)CharacterCancelInputMask.AttackPrimary);
                Assert.IsFalse(resolved.IsSuccess);
                Assert.AreEqual(CharacterActionRejectReason.MissingActionSet, resolved.RejectReason);

                Assert.IsFalse(fixture.Adapter.TryEvaluatePlannerPrimaryAttack(
                    fixture.Source,
                    fixture.Target,
                    fixture.PlannerFrame,
                    out CharacterTestAttackOpportunityResult result));
                Assert.AreEqual(CharacterTestAttackOpportunityStatus.Unavailable, result.Status);
                Assert.AreEqual(
                    CharacterTestAttackOpportunityFailureReason.TraceEvaluatorUnsupported,
                    result.FailureReason);
                Assert.IsFalse(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame));
            }
        }

        [Test]
        public void PlannerAdapter_ReportsStaleSnapshotInsteadOfUnavailable()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                CombatPhysicsWorld world = fixture.Runtime.PhysicsRuntime.World;
                int previousWorldRevision = world.Revision;
                CombatBodyId blockerBodyId = new CombatBodyId(
                    CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 99);
                world.UpsertBody(new CombatPhysicsBody(
                    new CombatEntityId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 99),
                    blockerBodyId,
                    FixVector3.Zero));
                world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                    blockerBodyId,
                    new CombatColliderId(990099),
                    layer: 1,
                    localMin: new FixVector3(
                        Fix64.FromInt(-1),
                        Fix64.FromInt(-1),
                        Fix64.FromInt(-1)),
                    localMax: new FixVector3(
                        Fix64.FromInt(1),
                        Fix64.FromInt(1),
                        Fix64.FromInt(1))));
                Assert.Greater(world.Revision, previousWorldRevision);

                Assert.IsFalse(fixture.Adapter.TryEvaluatePlannerPrimaryAttack(
                    fixture.Source,
                    fixture.Target,
                    fixture.PlannerFrame,
                    out CharacterTestAttackOpportunityResult result));
                Assert.AreEqual(CharacterTestAttackOpportunityStatus.StaleSnapshot, result.Status);
                Assert.AreEqual(
                    CharacterTestAttackOpportunityFailureReason.StaleSnapshot,
                    result.FailureReason);
                Assert.IsTrue(fixture.Adapter.TryGetEvidence(
                    fixture.Source.Id.Value,
                    out CharacterTestAttackOpportunityDecisionEvidence evidence));
                Assert.AreEqual(CharacterTestAttackOpportunityStatus.StaleSnapshot, evidence.Status);
                Assert.AreEqual(
                    CharacterTestAttackOpportunityFailureReason.StaleSnapshot,
                    evidence.FailureReason);
            }
        }

        [Test]
        public void PlannerAdapter_GuardRejectsCachedDecisionAfterActionConfigHotReload()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                // Seed a valid same-frame decision without depending on the trace fixture for
                // this cache-identity assertion. The guard must still revalidate the live
                // ActionSet after the runtime reload replaces action bindings.
                CharacterTestAttackOpportunityResult valid = CreateOpportunity(
                    CharacterTestAttackOpportunityLevel.CurrentPose);
                fixture.Adapter.CacheResult(
                    fixture.Source,
                    fixture.Target,
                    fixture.PlannerFrame,
                    valid);
                Assert.IsTrue(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame),
                    "initial cached decision should be accepted");

                CharacterTestCharacterActionReloadResult reload =
                    fixture.Runtime.ReloadActionConfig(fixture.PlannerFrame);
                Assert.IsTrue(reload.Success, reload.Message);
                Assert.IsFalse(
                    fixture.Adapter.CanSubmitPrimaryAttack(
                        fixture.Source,
                        fixture.PlannerFrame),
                    "A same-frame cached decision must not survive an ActionSet binding reload.");
            }
        }

        [Test]
        public void PlannerAdapter_RollbackResetInvalidatesDecisionEvidenceAndGuardCache()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                fixture.Adapter.CacheResult(
                    fixture.Source,
                    fixture.Target,
                    fixture.PlannerFrame,
                    CreateOpportunity(CharacterTestAttackOpportunityLevel.CurrentPose));
                fixture.Adapter.MarkCompatibilityLegacyRange(
                    fixture.Source,
                    fixture.PlannerFrame);
                Assert.IsTrue(fixture.Adapter.TryGetEvidence(
                    fixture.Source.Id.Value,
                    out CharacterTestAttackOpportunityDecisionEvidence evidence));
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerAttackDecisionSource.CompatibilityLegacyRange,
                    evidence.Source);

                // This models the authoritative rollback/replay boundary. The next planner
                // frame must rebuild all result, policy and evidence state from restored data.
                fixture.Adapter.ResetForRollbackReplay();

                Assert.IsFalse(fixture.Adapter.TryGetEvidence(
                    fixture.Source.Id.Value,
                    out _));
                Assert.IsFalse(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame),
                    "rollback/reset must invalidate the cached decision");
            }
        }

        [Test]
        public void PlannerAdapter_RejectsCachedDecisionAfterSameFramePoseOrHurtboxRevisionChange()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                fixture.Adapter.CacheResult(
                    fixture.Source,
                    fixture.Target,
                    fixture.PlannerFrame,
                    CreateOpportunity(CharacterTestAttackOpportunityLevel.CurrentPose));
                Assert.IsTrue(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame),
                    "initial cached decision should be accepted");

                // A same-frame source pose change invalidates the cached witness even
                // though the action binding and planner frame are unchanged.
                fixture.Source.SetTransform(new CharacterTestCharacterTransformComponent(
                    0f,
                    0f,
                    0f,
                    9000f));
                Assert.IsFalse(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame),
                    "source pose mutation should invalidate the decision");

                // Restore the source pose and seed a fresh decision, then advance only the
                // target geometry. The authoritative hurtbox revision and physics world
                // revision must prevent a stale same-frame decision from being submitted
                // after the rebuild. A pose mutation intentionally invalidates the old
                // decision even when the transform is later restored because the world
                // revision has advanced.
                fixture.Source.SetTransform(new CharacterTestCharacterTransformComponent(
                    0f,
                    0f,
                    0f,
                    0f));
                fixture.Adapter.CacheResult(
                    fixture.Source,
                    fixture.Target,
                    fixture.PlannerFrame,
                    CreateOpportunity(CharacterTestAttackOpportunityLevel.CurrentPose));
                Assert.IsTrue(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame),
                    "a fresh decision should be accepted before target geometry rebuild");
                int previousHurtboxRevision = fixture.Runtime.GetHurtboxRevision(
                    fixture.Target.Id.Value);
                fixture.Target.SetTransform(new CharacterTestCharacterTransformComponent(
                    0.1f,
                    0f,
                    0.95f,
                    0f));
                fixture.Runtime.Tick(new RuntimeTickContext(
                    frameIndex: 1,
                    deltaTime: 1d / 60d,
                    elapsedTime: 1d / 60d,
                    RuntimeTickStage.Simulation));
                Assert.Greater(
                    fixture.Runtime.GetHurtboxRevision(fixture.Target.Id.Value),
                    previousHurtboxRevision);
                Assert.IsFalse(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame));
            }
        }

        [Test]
        public void PlannerAdapter_RejectsCachedDecisionAfterSameFrameAuthoritySocketRevisionChange()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                fixture.Adapter.CacheResult(
                    fixture.Source,
                    fixture.Target,
                    fixture.PlannerFrame,
                    CreateOpportunity(CharacterTestAttackOpportunityLevel.CurrentPose));
                Assert.IsTrue(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame));

                int worldRevision = fixture.Runtime.PhysicsRuntime.World.Revision;
                int poseRevision = fixture.Source.SourcePoseRevision;
                int facingRevision = fixture.Source.SourceFacingRevision;
                Assert.IsFalse(fixture.Source.AuthorityPose.TryGetSocketOffset(
                    CharacterTestAuthoredContentIds.D400_Id12,
                    out FixVector3 beforeOffset));

                Assert.IsTrue(fixture.Runtime.TryApplyAuthorityPoseImpulse(
                    fixture.Source.Id.Value,
                    new CharacterAuthorityPoseImpulseRequest(
                        CharacterAuthorityPoseRegionIds.Default,
                        new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                        Fix64.One,
                        durationFrames: 4,
                        correlationId: new MxFramework.Runtime.RuntimeTraceIdentity(94007),
                        matchSource: MxFramework.CharacterRuntimeSpawn.CharacterAuthorityPoseMatchSource.PreviewCommand),
                    RuntimeFrame.Zero,
                    out string issue),
                    issue);
                fixture.Source.TickAuthorityPose(RuntimeFrame.Zero);

                Assert.IsTrue(fixture.Source.AuthorityPose.TryGetSocketOffset(
                    CharacterTestAuthoredContentIds.D400_Id12,
                    out FixVector3 afterOffset));
                Assert.AreNotEqual(beforeOffset, afterOffset);
                Assert.Greater(fixture.Source.SourcePoseRevision, poseRevision);
                Assert.AreEqual(facingRevision, fixture.Source.SourceFacingRevision);
                Assert.AreEqual(
                    worldRevision,
                    fixture.Runtime.PhysicsRuntime.World.Revision,
                    "AuthorityPose/socket mutation must invalidate independently of physics-world revision");
                Assert.IsFalse(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame),
                    "same-frame AuthorityPose/socket mutation must stale the cached witness");
            }
        }

        [Test]
        public void CharacterSourceRevisions_InvalidateFrozenSnapshotAndPlanSameFrame()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                Assert.IsTrue(fixture.Runtime.TryGetDamageGeometrySnapshot(
                    fixture.Target.Id.Value,
                    out CharacterTestDamageGeometrySnapshot geometry));
                Assert.IsTrue(CharacterTestAttackEvaluationSnapshot.TryCapture(
                    fixture.CompletedFrame,
                    fixture.Runtime.PhysicsRuntime.World,
                    fixture.Source,
                    fixture.Target,
                    geometry,
                    resolvedActionRevision: 17,
                    out CharacterTestAttackEvaluationSnapshot snapshot));

                CharacterTestDirectHitTracePlanBuffer buffer =
                    new CharacterTestDirectHitTracePlanBuffer();
                buffer.Reset(fixture.CompletedFrame, fixture.Runtime.PhysicsRuntime.World.Revision);
                CombatEntityId sourceCombatId = new CombatEntityId(
                    CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + fixture.Source.Id.Value);
                CharacterTestDirectHitTracePlanPoseIdentity identity =
                    new CharacterTestDirectHitTracePlanPoseIdentity(
                        CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
                        snapshot.SourcePosition,
                        snapshot.SourceYawMilliDegrees,
                        snapshot.SourcePoseRevision,
                        snapshot.SourceFacingRevision);
                int planIndex = buffer.GetOrBeginPlan(sourceCombatId, 17, identity);
                Assert.GreaterOrEqual(planIndex, 0);
                Assert.IsTrue(buffer.UpdatePlan(
                    planIndex,
                    sampleCount: 0,
                    bounds: default(CharacterTestDirectHitTraceBounds)));
                Assert.IsTrue(buffer.Seal(
                    fixture.CompletedFrame,
                    fixture.Runtime.PhysicsRuntime.World.Revision));
                CharacterTestDirectHitTracePlanCollectionView collection = buffer.CreateView();
                Assert.IsTrue(collection.TryGetPlan(planIndex, out CharacterTestDirectHitTracePlanView plan));
                Assert.IsTrue(plan.IsCurrent(
                    fixture.CompletedFrame,
                    fixture.Runtime.PhysicsRuntime.World.Revision,
                    sourceCombatId,
                    17,
                    CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
                    snapshot.SourcePosition,
                    snapshot.SourceYawMilliDegrees,
                    fixture.Source.SourcePoseRevision,
                    fixture.Source.SourceFacingRevision));

                Assert.IsTrue(fixture.Runtime.TryApplyAuthorityPoseImpulse(
                    fixture.Source.Id.Value,
                    new CharacterAuthorityPoseImpulseRequest(
                        CharacterAuthorityPoseRegionIds.Default,
                        new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                        Fix64.One,
                        durationFrames: 4,
                        correlationId: new MxFramework.Runtime.RuntimeTraceIdentity(94010),
                        matchSource: MxFramework.CharacterRuntimeSpawn.CharacterAuthorityPoseMatchSource.PreviewCommand),
                    RuntimeFrame.Zero,
                    out string issue),
                    issue);
                fixture.Source.TickAuthorityPose(RuntimeFrame.Zero);

                Assert.IsFalse(snapshot.IsCurrent(
                    fixture.CompletedFrame,
                    fixture.Runtime.PhysicsRuntime.World,
                    fixture.Source,
                    fixture.Target,
                    geometry,
                    fixture.Source.SourcePoseRevision,
                    fixture.Source.SourceFacingRevision,
                    resolvedActionRevision: 17));
                Assert.IsFalse(plan.IsCurrent(
                    fixture.CompletedFrame,
                    fixture.Runtime.PhysicsRuntime.World.Revision,
                    sourceCombatId,
                    17,
                    CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
                    snapshot.SourcePosition,
                    snapshot.SourceYawMilliDegrees,
                    fixture.Source.SourcePoseRevision,
                    fixture.Source.SourceFacingRevision));
            }
        }

        [Test]
        public void NetworkRollback_RestoresSourceRevisionsAfterSameFramePoseAndAuthorityPoseMutation()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                fixture.Adapter.CacheResult(
                    fixture.Source,
                    fixture.Target,
                    fixture.PlannerFrame,
                    CreateOpportunity(CharacterTestAttackOpportunityLevel.CurrentPose));
                Assert.IsTrue(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame));

                int originalPoseRevision = fixture.Source.SourcePoseRevision;
                int originalFacingRevision = fixture.Source.SourceFacingRevision;
                CharacterTestNetworkSnapshotParticipant participant =
                    new CharacterTestNetworkSnapshotParticipant(fixture.Runtime, null);
                CharacterTestNetworkSnapshotDocument rollbackDocument =
                    participant.CaptureRollbackDocument(fixture.CompletedFrame);
                long originalHash = participant.ComputeStateHash(fixture.CompletedFrame);
                Assert.AreEqual(
                    originalPoseRevision,
                    rollbackDocument.Characters[0].SourcePoseRevision);
                Assert.AreEqual(
                    originalFacingRevision,
                    rollbackDocument.Characters[0].SourceFacingRevision);

                fixture.Source.SetTransform(
                    CharacterTestCharacterTransformComponent.FromDeterministic(
                        fixture.Source.Transform.XMillimeters + 10,
                        fixture.Source.Transform.YMillimeters,
                        fixture.Source.Transform.ZMillimeters,
                        fixture.Source.Transform.YawMilliDegrees + 9000));
                Assert.IsTrue(fixture.Runtime.TryApplyAuthorityPoseImpulse(
                    fixture.Source.Id.Value,
                    new CharacterAuthorityPoseImpulseRequest(
                        CharacterAuthorityPoseRegionIds.Default,
                        new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                        Fix64.One,
                        durationFrames: 4,
                        correlationId: new MxFramework.Runtime.RuntimeTraceIdentity(94011),
                        matchSource: MxFramework.CharacterRuntimeSpawn.CharacterAuthorityPoseMatchSource.PreviewCommand),
                    fixture.CompletedFrame,
                    out string issue),
                    issue);
                fixture.Source.TickAuthorityPose(fixture.CompletedFrame);

                Assert.Greater(fixture.Source.SourcePoseRevision, originalPoseRevision);
                Assert.Greater(fixture.Source.SourceFacingRevision, originalFacingRevision);
                Assert.IsFalse(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame));
                Assert.AreNotEqual(originalHash, participant.ComputeStateHash(fixture.CompletedFrame));

                NetworkSnapshotApplyResult apply = participant.ApplyRollbackDocument(
                    fixture.CompletedFrame,
                    rollbackDocument,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(apply.Success, apply.Message);
                Assert.AreEqual(originalPoseRevision, fixture.Source.SourcePoseRevision);
                Assert.AreEqual(originalFacingRevision, fixture.Source.SourceFacingRevision);
                Assert.AreEqual(originalHash, participant.ComputeStateHash(fixture.CompletedFrame));
                Assert.IsTrue(
                    fixture.Adapter.CanSubmitPrimaryAttack(fixture.Source, fixture.PlannerFrame),
                    "restoring the source revisions must restore the cached adapter witness");
            }
        }

        [Test]
        public void NetworkHash_IncludesSourceRevisionsInFullAndOwnedCharacterPartitions()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                CharacterTestNetworkSnapshotParticipant participant =
                    new CharacterTestNetworkSnapshotParticipant(fixture.Runtime, null);
                RuntimeFrame frame = fixture.CompletedFrame;
                long fullBaseline = participant.ComputeStateHash(frame);
                long ownedBaseline = participant.ComputeOwnedStateHash(frame, fixture.Source.Id.Value);
                long nonOwnedBaseline = participant.ComputeNonOwnedStateHash(frame, fixture.Source.Id.Value);
                int originalPoseRevision = fixture.Source.SourcePoseRevision;
                int originalFacingRevision = fixture.Source.SourceFacingRevision;

                fixture.Source.RestoreSourceRevisions(
                    originalPoseRevision + 1,
                    originalFacingRevision + 1);
                Assert.AreNotEqual(fullBaseline, participant.ComputeStateHash(frame));
                Assert.AreNotEqual(
                    ownedBaseline,
                    participant.ComputeOwnedStateHash(frame, fixture.Source.Id.Value));
                Assert.AreEqual(
                    nonOwnedBaseline,
                    participant.ComputeNonOwnedStateHash(frame, fixture.Source.Id.Value));

                fixture.Source.RestoreSourceRevisions(originalPoseRevision, originalFacingRevision);
                Assert.AreEqual(fullBaseline, participant.ComputeStateHash(frame));
                Assert.AreEqual(
                    ownedBaseline,
                    participant.ComputeOwnedStateHash(frame, fixture.Source.Id.Value));
                Assert.AreEqual(
                    nonOwnedBaseline,
                    participant.ComputeNonOwnedStateHash(frame, fixture.Source.Id.Value));
            }
        }

        [Test]
        public void CharacterSourceRevisions_AreWriteBasedAndSameValueSafe()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                int initialPoseRevision = fixture.Source.SourcePoseRevision;
                int initialFacingRevision = fixture.Source.SourceFacingRevision;

                // Warm reads, including the socket resolver used by the trace
                // planner, must not move either identity frontier.
                for (int i = 0; i < 32; i++)
                {
                    _ = fixture.Source.SourcePoseRevision;
                    _ = fixture.Source.SourceFacingRevision;
                    fixture.Source.AuthorityPose.TryGetSocketOffset(CharacterTestAuthoredContentIds.D400_Id12, out _);
                }
                Assert.AreEqual(initialPoseRevision, fixture.Source.SourcePoseRevision);
                Assert.AreEqual(initialFacingRevision, fixture.Source.SourceFacingRevision);

                fixture.Source.SetTransform(fixture.Source.Transform);
                Assert.AreEqual(initialPoseRevision, fixture.Source.SourcePoseRevision);
                Assert.AreEqual(initialFacingRevision, fixture.Source.SourceFacingRevision);

                CharacterAuthorityPoseSnapshot samePoseSnapshot =
                    fixture.Source.AuthorityPose.CreateSnapshot();
                fixture.Source.RestoreAuthorityPoseSnapshot(samePoseSnapshot);
                Assert.AreEqual(initialPoseRevision, fixture.Source.SourcePoseRevision);
                Assert.AreEqual(initialFacingRevision, fixture.Source.SourceFacingRevision);

                // Replacing the component with a distinct instance carrying
                // the same authoritative value is also an equal write.
                fixture.Source.SetAuthorityPose(
                    new CharacterAuthorityPoseStateComponent(
                        fixture.Source.AuthorityPose.Definition));
                Assert.AreEqual(initialPoseRevision, fixture.Source.SourcePoseRevision);
                Assert.AreEqual(initialFacingRevision, fixture.Source.SourceFacingRevision);

                fixture.Source.SetTransform(
                    CharacterTestCharacterTransformComponent.FromDeterministic(
                        fixture.Source.Transform.XMillimeters,
                        fixture.Source.Transform.YMillimeters,
                        fixture.Source.Transform.ZMillimeters,
                        fixture.Source.Transform.YawMilliDegrees + 9000));
                Assert.AreEqual(initialPoseRevision, fixture.Source.SourcePoseRevision);
                Assert.Greater(fixture.Source.SourceFacingRevision, initialFacingRevision);
                int changedFacingRevision = fixture.Source.SourceFacingRevision;

                fixture.Source.SetTransform(fixture.Source.Transform);
                Assert.AreEqual(initialPoseRevision, fixture.Source.SourcePoseRevision);
                Assert.AreEqual(changedFacingRevision, fixture.Source.SourceFacingRevision);
            }
        }

        [Test]
        public void CharacterSourceRevisions_RestoreWithRollbackStateAndReacceptSameWitness()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                CharacterTestCharacterTransformComponent originalTransform = fixture.Source.Transform;
                int originalPoseRevision = fixture.Source.SourcePoseRevision;
                int originalFacingRevision = fixture.Source.SourceFacingRevision;
                fixture.Adapter.CacheResult(
                    fixture.Source,
                    fixture.Target,
                    fixture.PlannerFrame,
                    CreateOpportunity(CharacterTestAttackOpportunityLevel.CurrentPose));
                Assert.IsTrue(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame));

                fixture.Source.SetTransform(
                    CharacterTestCharacterTransformComponent.FromDeterministic(
                        originalTransform.XMillimeters + 10,
                        originalTransform.YMillimeters,
                        originalTransform.ZMillimeters,
                        originalTransform.YawMilliDegrees));
                Assert.IsFalse(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame));

                fixture.Source.SetTransform(originalTransform);
                fixture.Source.RestoreSourceRevisions(originalPoseRevision, originalFacingRevision);
                Assert.AreEqual(originalPoseRevision, fixture.Source.SourcePoseRevision);
                Assert.AreEqual(originalFacingRevision, fixture.Source.SourceFacingRevision);
                Assert.IsTrue(
                    fixture.Adapter.CanSubmitPrimaryAttack(fixture.Source, fixture.PlannerFrame),
                    "rollback must restore both source identity and the cached witness outcome");
            }
        }

        [Test]
        public void PlannerAdapter_UsesExplicitCompatibilityEvidenceForLegacySkillRange()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                fixture.Adapter.MarkCompatibilityLegacyRange(
                    fixture.Source,
                    fixture.PlannerFrame);

                Assert.IsTrue(fixture.Adapter.TryGetEvidence(
                    fixture.Source.Id.Value,
                    out CharacterTestAttackOpportunityDecisionEvidence evidence));
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerAttackDecisionSource.CompatibilityLegacyRange,
                    evidence.Source);
                Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, evidence.Status);
                Assert.AreEqual(CharacterTestAttackOpportunityLevel.NoOpportunity, evidence.Level);
                Assert.IsFalse(fixture.Adapter.CanSubmitPrimaryAttack(
                    fixture.Source,
                    fixture.PlannerFrame));
            }
        }

        [Test]
        public void CharacterTestPlayerInput_BypassesRuntimeAiOpportunityGuard()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                fixture.Source.SetControl(
                    new CharacterTestCharacterControlComponent(
                        CharacterTestCharacterControlOwner.LocalPlayer));
                fixture.Source.SetInput(new CharacterTestCharacterInputComponent(
                    Vector2.zero,
                    jumpPressed: false,
                    attackPrimaryPressed: true,
                    controlIntentSourceId: CharacterTestRuntimeCommandSources.RuntimeAiPlanner));
                fixture.Source.GameplayIntentBuffer.AddOrRefresh(
                    CharacterTestGameplayIntentKind.AttackPrimary,
                    fixture.Target.Id.Value,
                    frame: fixture.PlannerFrame.Value);

                fixture.Runtime.Tick(new RuntimeTickContext(
                    frameIndex: 1,
                    deltaTime: 1d / 60d,
                    elapsedTime: 1d / 60d,
                    RuntimeTickStage.Simulation));

                Assert.AreEqual(MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running, fixture.Source.Action.State, fixture.Source.Action.LastRejectReason);
                Assert.AreEqual(
                    CharacterTestCharacterActionConfigSource.LightAttackActionId,
                    fixture.Source.Action.ActiveActionId);
            }
        }

        [Test]
        public void PlannerEvaluator_UsesCurrentPoseOpportunityToSelectPrimaryAttack()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                CharacterTestRuntimeAiPlannerEvaluator evaluator = CreateEvaluator(
                    CreateOpportunity(CharacterTestAttackOpportunityLevel.CurrentPose));

                Assert.IsTrue(evaluator.TryEvaluate(
                    fixture.Source,
                    fixture.Runtime.Entities,
                    fixture.PlannerFrame,
                    out CharacterTestRuntimeAiPlannerDecision decision));
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack,
                    decision.SelectedActionKind);
                Assert.IsTrue(decision.AttackPrimaryPressed);
                Assert.That(decision.Move.sqrMagnitude, Is.LessThan(0.0001f));
            }
        }

        [Test]
        public void PlannerEvaluator_UsesInjectedCurrentPoseEvenWhenRootDistanceIsFar()
        {
            // The opportunity result is deliberately supplied by the frozen-query seam. This
            // keeps the assertion about planner authority (geometry, not root distance) while
            // leaving large-target broadphase coverage to the M0 tests.
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 6f))
            {
                CharacterTestRuntimeAiPlannerEvaluator evaluator = CreateEvaluator(
                    CreateOpportunity(CharacterTestAttackOpportunityLevel.CurrentPose));

                Assert.IsTrue(evaluator.TryEvaluate(
                    fixture.Source,
                    fixture.Runtime.Entities,
                    fixture.PlannerFrame,
                    out CharacterTestRuntimeAiPlannerDecision decision));
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack,
                    decision.SelectedActionKind);
                Assert.IsTrue(decision.AttackPrimaryPressed);
                Assert.That(decision.Move.sqrMagnitude, Is.LessThan(0.0001f));
            }
        }

        [Test]
        public void PlannerEvaluator_UsesRequiresTranslationOpportunityToContinueMove()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 3f))
            {
                CharacterTestRuntimeAiPlannerEvaluator evaluator = CreateEvaluator(
                    CreateOpportunity(CharacterTestAttackOpportunityLevel.RequiresTranslation));

                Assert.IsTrue(evaluator.TryEvaluate(
                    fixture.Source,
                    fixture.Runtime.Entities,
                    fixture.PlannerFrame,
                    out CharacterTestRuntimeAiPlannerDecision decision));
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                    decision.SelectedActionKind);
                Assert.IsFalse(decision.AttackPrimaryPressed);
                Assert.Greater(decision.Move.sqrMagnitude, 0.0001f);
            }
        }

        [Test]
        public void PlannerEvaluator_UsesInjectedRequiresTranslationEvenWhenRootDistanceIsNear()
        {
            // A near root distance must not promote a non-current-pose opportunity into an
            // attack. The trace result remains authoritative for the action choice.
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 2.8f))
            {
                CharacterTestRuntimeAiPlannerEvaluator evaluator = CreateEvaluator(
                    CreateOpportunity(CharacterTestAttackOpportunityLevel.RequiresTranslation));

                Assert.IsTrue(evaluator.TryEvaluate(
                    fixture.Source,
                    fixture.Runtime.Entities,
                    fixture.PlannerFrame,
                    out CharacterTestRuntimeAiPlannerDecision decision));
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                    decision.SelectedActionKind);
                Assert.IsFalse(decision.AttackPrimaryPressed);
                Assert.Greater(decision.Move.sqrMagnitude, 0.0001f);
            }
        }

        [Test]
        public void PlannerEvaluator_FailsClosedForUnavailableOpportunity()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                CharacterTestRuntimeAiPlannerEvaluator evaluator = CreateEvaluator(
                    CharacterTestAttackOpportunityResult.Failure(
                        CharacterTestOpportunityFailure.Unavailable(
                            CharacterTestAttackOpportunityFailureReason.SnapshotUnavailable,
                            CharacterTestAttackOpportunityFailureFlags.SnapshotUnavailable),
                        CharacterTestAttackEvaluationMode.CurrentPose));

                Assert.IsTrue(evaluator.TryEvaluate(
                    fixture.Source,
                    fixture.Runtime.Entities,
                    fixture.PlannerFrame,
                    out CharacterTestRuntimeAiPlannerDecision decision));
                Assert.AreNotEqual(
                    CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack,
                    decision.SelectedActionKind);
                Assert.IsFalse(decision.AttackPrimaryPressed);
            }
        }

        [Test]
        public void PlannerEvaluator_FailsClosedForStaleSnapshotOpportunity()
        {
            using (Fixture fixture = CreateFixture(sourceX: 0f, sourceZ: 0f, targetX: 0f, targetZ: 0.95f))
            {
                CharacterTestRuntimeAiPlannerEvaluator evaluator = CreateEvaluator(
                    CharacterTestAttackOpportunityResult.Failure(
                        CharacterTestOpportunityFailure.Stale(
                            CharacterTestAttackOpportunityFailureReason.StaleSnapshot,
                            CharacterTestAttackOpportunityFailureFlags.SnapshotMismatch),
                        CharacterTestAttackEvaluationMode.CurrentPose));

                Assert.IsTrue(evaluator.TryEvaluate(
                    fixture.Source,
                    fixture.Runtime.Entities,
                    fixture.PlannerFrame,
                    out CharacterTestRuntimeAiPlannerDecision decision));
                Assert.AreNotEqual(
                    CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack,
                    decision.SelectedActionKind);
                Assert.IsFalse(decision.AttackPrimaryPressed);
            }
        }

        private static CharacterTestRuntimeAiPlannerEvaluator CreateEvaluator(
            CharacterTestAttackOpportunityResult opportunity)
        {
            CharacterTestRuntimeAiPlannerProfile profile =
                CharacterTestRuntimeAiPlannerProfileSource.CreateDefault();
            Assert.IsTrue(
                CharacterTestRuntimeAiPlannerCompiledProfile.TryCompile(
                    profile,
                    out CharacterTestRuntimeAiPlannerCompiledProfile compiled,
                    out CharacterTestRuntimeAiPlannerValidationReport report),
                report.ToText());
            return new CharacterTestRuntimeAiPlannerEvaluator(
                compiled,
                CharacterTestDirectAiNavigationService.Instance,
                new NoObstaclePerceptionSource(),
                primaryAttackEligibility: (entity, targetEntityId, frame) => opportunity);
        }

        internal static CharacterTestAttackOpportunityResult CreateOpportunity(
            CharacterTestAttackOpportunityLevel level)
        {
            bool current = level == CharacterTestAttackOpportunityLevel.CurrentPose;
            bool turned = level == CharacterTestAttackOpportunityLevel.TurnInPlace;
            bool translation = level == CharacterTestAttackOpportunityLevel.RequiresTranslation;
            return new CharacterTestAttackOpportunityResult(
                CharacterTestAttackOpportunityStatus.Valid,
                CharacterTestAttackEvaluationMode.CurrentPose,
                level,
                hasCompatibleHurtbox: true,
                envelopeIntersectsTarget: true,
                traceCanResolveTargetHit: current || turned,
                canHitFromCurrentPose: current,
                canHitAfterTurnInPlace: current || turned,
                requiresTranslation: translation,
                requiredYawDeltaMilliDegrees: turned ? 90000 : 0,
                earliestCandidateHitTick: current ? 23 : -1,
                traceSampleIndex: current ? 0 : -1,
                stableSampleId: current ? 1 : 0,
                selectedHurtboxId: current ? CharacterTestAuthoredContentIds.D400_Id34 : 0,
                selectedHitZoneId: current ? CharacterTestAuthoredContentIds.D400_Id34 : 0,
                selectedDamageZoneId: 0,
                witnessPoint: FixVector3.Zero,
                aimPoint: FixVector3.Zero,
                failureReason: CharacterTestAttackOpportunityFailureReason.None,
                failureFlags: CharacterTestAttackOpportunityFailureFlags.None);
        }

        private sealed class NoObstaclePerceptionSource
            : ICharacterTestRuntimeAiPlannerPerceptionSource
        {
            public void SampleEnvironment(
                CharacterTestCharacterEntity self,
                CharacterTestRuntimeAiPlannerPerceptionSettings settings,
                CharacterTestRuntimeAiPlannerEnvironmentSample sample)
            {
                sample?.Clear();
            }

            public bool TryResolveLineOfSightBlocked(
                CharacterTestCharacterEntity self,
                CharacterTestCharacterEntity target,
                CharacterTestRuntimeAiPlannerPerceptionSettings settings,
                out bool blocked)
            {
                blocked = false;
                return true;
            }
        }

        private static Fixture CreateFixture(
            float sourceX,
            float sourceZ,
            float targetX,
            float targetZ,
            float sourceYawDegrees = 0f)
        {
            CharacterTestCharacterDefinitionRegistry definitions =
                new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(definitions.TryGet(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                out CharacterTestCharacterDefinition definition));

            CharacterTestCharacterPhysicsRuntime physics = CreatePhysicsRuntime();
            var runtime = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                definitions: definitions,
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: physics,
                createViews: false);
            CharacterTestCharacterEntity source = runtime.World.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: sourceX,
                    y: 0f,
                    z: sourceZ,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            CharacterTestCharacterEntity target = runtime.World.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: targetX,
                    y: 0f,
                    z: targetZ,
                    CharacterTestCharacterControlOwner.None),
                definition);
            if (Math.Abs(sourceYawDegrees) > float.Epsilon)
            {
                source.SetTransform(new CharacterTestCharacterTransformComponent(
                    sourceX,
                    0f,
                    sourceZ,
                    sourceYawDegrees));
            }
            source.SetMotionState(0f, UnityEngine.Vector2.zero, isGrounded: true);
            target.SetMotionState(0f, UnityEngine.Vector2.zero, isGrounded: true);

            runtime.Tick(new RuntimeTickContext(
                frameIndex: 0,
                deltaTime: 1d / 60d,
                elapsedTime: 0d,
                RuntimeTickStage.Simulation));

            var adapter = new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(runtime);
            return new Fixture(runtime, adapter, source, target, new RuntimeFrame(0), new RuntimeFrame(1));
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePhysicsRuntime()
        {
            var world = new CombatPhysicsWorld();
            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new MxFramework.Core.Math.FixVector3(
                    MxFramework.Core.Math.Fix64.Half,
                    MxFramework.Core.Math.Fix64.One,
                    MxFramework.Core.Math.Fix64.Half),
                MxFramework.Core.Math.Fix64.FromRatio(1, 100),
                MxFramework.Core.Math.Fix64.Zero,
                staticColliderCount: 1,
                sourceDescription: "cd-m1-planner-integration");
        }

        private sealed class Fixture : IDisposable
        {
            internal Fixture(
                CharacterTestCharacterRuntimeModule runtime,
                CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter,
                CharacterTestCharacterEntity source,
                CharacterTestCharacterEntity target,
                RuntimeFrame completedFrame,
                RuntimeFrame plannerFrame)
            {
                Runtime = runtime;
                Adapter = adapter;
                Source = source;
                Target = target;
                CompletedFrame = completedFrame;
                PlannerFrame = plannerFrame;
            }

            internal CharacterTestCharacterRuntimeModule Runtime { get; }
            internal CharacterTestRuntimeAiPlannerAttackOpportunityAdapter Adapter { get; }
            internal CharacterTestCharacterEntity Source { get; }
            internal CharacterTestCharacterEntity Target { get; }
            internal RuntimeFrame CompletedFrame { get; }
            internal RuntimeFrame PlannerFrame { get; }

            public void Dispose()
            {
                Runtime.Dispose();
            }
        }
    }

    public sealed partial class CharacterTestRuntimeAiPlannerFormalizationTests
    {
        [Test]
        public void RuntimeAiPlannerCommandModule_PrimaryAttackUsesCurrentPoseOpportunity()
        {
            using var harness = CreateHarness(
                disableLegacyStartup: true,
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: CreatePlannerCommandPhysicsRuntime());
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0.95f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            harness.Tick();

            RuntimeCommand command = FindRuntimeAiPlannerCommand(harness);
            CharacterTestControlIntent intent = CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
            Assert.AreEqual(CharacterTestRuntimeCommandIds.SetControlIntent, command.CommandId);
            Assert.AreEqual(CharacterTestRuntimeCommandSources.RuntimeAiPlanner, command.SourceId);
            Assert.AreEqual(ai.Id.Value, intent.TargetEntityId);
            Assert.IsTrue(intent.AttackPrimaryPressed);
            Assert.IsFalse(intent.TurnInPlace);
            Assert.AreEqual(0, intent.WorldMoveXMilli);
            Assert.AreEqual(0, intent.WorldMoveYMilli);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                ai.Action.ActiveActionId,
                "action reject=" + ai.Action.LastRejectReason
                + " inputAttack=" + ai.Input.AttackPrimaryPressed
                + " controlSource=" + ai.Input.ControlIntentSourceId
                + " frame=" + harness.CurrentFrame.Value
                + " " + DescribePlannerAttackAdapter(harness, ai));
        }

        [Test]
        public void RuntimeAiPlannerCommandModule_TurnInPlaceSubmitsZeroMoveAndNoAttack()
        {
            using var harness = CreateHarness(
                disableLegacyStartup: true,
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: CreatePlannerCommandPhysicsRuntime());
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0.95f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));

            harness.Tick();

            RuntimeCommand command = FindRuntimeAiPlannerCommand(harness);
            CharacterTestControlIntent intent = CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
            Assert.IsTrue(intent.TurnInPlace);
            Assert.IsFalse(intent.AttackPrimaryPressed);
            Assert.AreEqual(0, intent.MoveXMilli);
            Assert.AreEqual(0, intent.MoveYMilli);
            Assert.AreEqual(0, intent.WorldMoveXMilli);
            Assert.AreEqual(0, intent.WorldMoveYMilli);
            Assert.AreNotEqual(0, intent.TurnInPlaceYawMilliDegrees);
            Assert.AreNotEqual(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                ai.Action.ActiveActionId);
        }

        [Test]
        public void RuntimeAiPlannerCommandModule_JointSelectionPrefersFartherCurrentPoseOverNearerTurn()
        {
            using var harness = CreateHarness(
                disableLegacyStartup: true,
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: CreatePlannerCommandPhysicsRuntime());
            CharacterTestCharacterEntity nearerTurnTarget = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: -0.7f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity fartherCurrentTarget = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0.95f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));

            harness.Tick();

            CharacterTestRuntimeAiJointDiagnosticSnapshot[] diagnostics =
                harness.Modules.RuntimeAiPlannerCommands
                    .CaptureJointSelectionDiagnosticsForDiagnostics();
            Assert.AreEqual(1, diagnostics.Length);
            Assert.IsTrue(
                diagnostics[0].HasSelection,
                "joint selection missing: candidates=" + diagnostics[0].Generation.CandidateCount
                    + " evaluated=" + diagnostics[0].EvaluationCount
                    + " traceBuilds=" + diagnostics[0].TracePlanBuildCount
                    + " cacheHits=" + diagnostics[0].ResultCacheHitCount
                    + " truncation=" + diagnostics[0].TruncationPhase);
            RuntimeCommand command = FindRuntimeAiPlannerCommand(harness);
            CharacterTestControlIntent intent = CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
            Assert.IsTrue(intent.AttackPrimaryPressed);
            Assert.IsFalse(intent.TurnInPlace);
            Assert.AreEqual(ai.Id.Value, diagnostics[0].SourceEntityId);
            Assert.AreEqual(fartherCurrentTarget.Id.Value, diagnostics[0].TargetEntityId);
            Assert.AreNotEqual(nearerTurnTarget.Id.Value, diagnostics[0].TargetEntityId);
            Assert.IsTrue(diagnostics[0].CurrentPose);
        }

        [Test]
        public void RuntimeAiPlannerCommandModule_JointDiagnosticsAreReadOnlyAndRollbackDerived()
        {
            using var harness = CreateHarness(
                disableLegacyStartup: true,
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: CreatePlannerCommandPhysicsRuntime());
            CharacterTestCharacterEntity target = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0.95f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            harness.Tick();

            CharacterTestRuntimeAiJointRuntime joint = GetJointRuntime(harness);
            Assert.IsTrue(joint.TryGetDiagnostic(
                ai.Id.Value,
                out CharacterTestRuntimeAiJointDiagnosticSnapshot before));
            Assert.AreEqual(target.Id.Value, before.TargetEntityId);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                Assert.IsTrue(joint.TryGetDiagnostic(ai.Id.Value, out _));
            }
            Assert.AreEqual(
                0L,
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore,
                "The simulation-facing diagnostic read must be warmed and allocation-free.");

            CharacterTestRuntimeAiPlannerStateSnapshot snapshot =
                harness.Modules.RuntimeAiPlannerCommands.CreateStateSnapshot();
            Assert.IsTrue(
                harness.Modules.RuntimeAiPlannerCommands.TryApplyStateSnapshot(
                    snapshot,
                    out string failure),
                failure);
            Assert.IsFalse(joint.TryGetDiagnostic(ai.Id.Value, out _));
            Assert.AreEqual(
                0,
                harness.Modules.RuntimeAiPlannerCommands
                    .CaptureJointSelectionDiagnosticsForDiagnostics().Length);

            ResetPlannerAttackAttempt(
                harness,
                ai,
                harness.Modules.RuntimeCommands.CommandBuffer.CurrentFrame.Value);
            ResetPlannerAttackTarget(target);
            harness.Tick();
            Assert.IsTrue(joint.TryGetDiagnostic(
                ai.Id.Value,
                out CharacterTestRuntimeAiJointDiagnosticSnapshot replayed));
            Assert.AreEqual(before.TargetEntityId, replayed.TargetEntityId);
            Assert.AreEqual(before.ResolvedActionId, replayed.ResolvedActionId);
            Assert.AreEqual(before.ZoneId, replayed.ZoneId);
            Assert.AreEqual(before.AnchorId, replayed.AnchorId);
            Assert.AreEqual(before.Status, replayed.Status);
        }

        [Test]
        public void RuntimeAiPlannerCommandModule_MismatchedActionSetFailsClosedWithoutAttack()
        {
            using var harness = CreateHarness(
                disableLegacyStartup: true,
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: CreatePlannerCommandPhysicsRuntime());
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0.95f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            ai.SetEquipment(new CharacterTestCharacterEquipmentComponent(
                useDefaultLoadout: false,
                equipmentStateStableId: "missing.action-set"));

            harness.Tick();

            RuntimeCommand command = FindRuntimeAiPlannerCommand(harness);
            CharacterTestControlIntent intent = CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
            Assert.IsFalse(intent.AttackPrimaryPressed);
            Assert.IsFalse(intent.TurnInPlace);
            Assert.AreNotEqual(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                ai.Action.ActiveActionId);

            CharacterTestRuntimeAiJointDiagnosticSnapshot[] diagnostics =
                harness.Modules.RuntimeAiPlannerCommands
                    .CaptureJointSelectionDiagnosticsForDiagnostics();
            Assert.AreEqual(1, diagnostics.Length);
            Assert.IsFalse(diagnostics[0].HasSelection);
            Assert.AreEqual(
                CharacterTestAttackOpportunityFailureReason.TraceEvaluatorUnsupported,
                diagnostics[0].FailureReason);
        }

        [Test]
        public void RuntimeAiPlannerCommandModule_StateSnapshotApplyInvalidatesAttackOpportunityCache()
        {
            using var harness = CreateHarness(
                disableLegacyStartup: true,
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: CreatePlannerCommandPhysicsRuntime());
            CharacterTestCharacterEntity target = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0.95f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            harness.Tick();

            FieldInfo adapterField = typeof(CharacterTestRuntimeAiPlannerCommandModule)
                .GetField("_attackOpportunityAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(adapterField);
            CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter =
                adapterField.GetValue(harness.Modules.RuntimeAiPlannerCommands)
                    as CharacterTestRuntimeAiPlannerAttackOpportunityAdapter;
            Assert.NotNull(adapter);

            // Seed both the decision and the compatibility evidence so the assertion
            // proves the authoritative command-module restore path clears every cache,
            // rather than only checking a direct adapter reset.
            adapter.MarkCompatibilityLegacyRange(ai, harness.CurrentFrame);
            adapter.CacheResult(
                ai,
                target,
                harness.CurrentFrame,
                CharacterTestCharacterDistanceM1PlannerIntegrationTests.CreateOpportunity(
                    CharacterTestAttackOpportunityLevel.CurrentPose));
            Assert.IsTrue(adapter.TryGetEvidence(
                ai.Id.Value,
                out CharacterTestAttackOpportunityDecisionEvidence beforeEvidence));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerAttackDecisionSource.CompatibilityLegacyRange,
                beforeEvidence.Source);
            Assert.IsTrue(adapter.CanSubmitPrimaryAttack(ai, harness.CurrentFrame));

            CharacterTestRuntimeAiPlannerStateSnapshot snapshot =
                harness.Modules.RuntimeAiPlannerCommands.CreateStateSnapshot();
            Assert.IsTrue(
                harness.Modules.RuntimeAiPlannerCommands.TryApplyStateSnapshot(
                    snapshot,
                    out string failure),
                failure);

            Assert.IsFalse(adapter.TryGetEvidence(ai.Id.Value, out _));
            Assert.IsFalse(
                adapter.CanSubmitPrimaryAttack(ai, harness.CurrentFrame),
                "Authoritative state restore must not reuse a same-frame attack decision.");
        }

        [Test]
        public void RuntimeAiPlannerCommandModule_WarmedPrimaryAttackAdapterGuardAllocatesZeroBytes()
        {
            using var harness = CreateHarness(
                disableLegacyStartup: true,
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: CreatePlannerCommandPhysicsRuntime());
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0.95f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            harness.Tick();

            FieldInfo adapterField = typeof(CharacterTestRuntimeAiPlannerCommandModule)
                .GetField("_attackOpportunityAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(adapterField);
            CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter =
                adapterField.GetValue(harness.Modules.RuntimeAiPlannerCommands)
                    as CharacterTestRuntimeAiPlannerAttackOpportunityAdapter;
            Assert.NotNull(adapter);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                adapter.CanSubmitPrimaryAttack(ai, harness.CurrentFrame);
            }

            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Test]
        public void RuntimeAiPlannerCommandModule_WarmedPlannerSubmitApplyGuardChainAllocatesZeroBytes()
        {
            using var harness = CreateHarness(
                disableLegacyStartup: true,
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: CreatePlannerCommandPhysicsRuntime());
            CharacterTestCharacterEntity target = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0.95f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));

            // The fixture is intentionally stepped through the real module boundary instead
            // of calling the adapter directly: planner Tick evaluates, the authoritative
            // submitter enqueues, RuntimeCommands applies, and CharacterRuntime runs the
            // action-level guard in the same deterministic frame.
            harness.Tick();
            Assert.IsTrue(
                TryFindRuntimeAiPlannerIntent(harness, ai.Id.Value, out CharacterTestControlIntent intent),
                "The warmed-chain fixture must submit a planner control intent before measuring.");
            Assert.IsTrue(intent.AttackPrimaryPressed);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                ai.Action.ActiveActionId,
                "The measured chain must reach the action-level PrimaryAttack guard. reject="
                + ai.Action.LastRejectReason
                + " inputAttack=" + ai.Input.AttackPrimaryPressed
                + " controlSource=" + ai.Input.ControlIntentSourceId
                + " frame=" + harness.CurrentFrame.Value
                + " " + DescribePlannerAttackAdapter(harness, ai));

            const int warmupTicks = 8;
            for (int i = 1; i < warmupTicks; i++)
            {
                ResetPlannerAttackAttempt(
                    harness,
                    ai,
                    harness.Modules.RuntimeCommands.CommandBuffer.CurrentFrame.Value);
                ResetPlannerAttackTarget(target);
                harness.Tick();
            }

            ResetPlannerAttackAttempt(
                harness,
                ai,
                harness.Modules.RuntimeCommands.CommandBuffer.CurrentFrame.Value);
            ResetPlannerAttackTarget(target);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            const int measuredTicks = 32;
            int attackCommandCount = 0;
            int acceptedAttackCount = 0;
            for (int i = 0; i < measuredTicks; i++)
            {
                harness.Tick();
                bool hasAttackIntent = TryFindRuntimeAiPlannerIntent(
                    harness,
                    ai.Id.Value,
                    out CharacterTestControlIntent measuredIntent)
                    && measuredIntent.AttackPrimaryPressed;
                if (hasAttackIntent)
                {
                    attackCommandCount++;
                }

                if (string.Equals(
                    (ai.Action.ActiveActionId).ToString(),
                    (CharacterTestCharacterActionConfigSource.LightAttackActionId).ToString(),
                    StringComparison.Ordinal))
                {
                    acceptedAttackCount++;
                }

                // The action reset is part of the bounded steady-state fixture. It uses the
                // production lifecycle API, so the next iteration must really re-enter the
                // planner, adapter and action-level guard rather than observing SelfActionRunning.
                ResetPlannerAttackAttempt(
                    harness,
                    ai,
                    harness.Modules.RuntimeCommands.CommandBuffer.CurrentFrame.Value);
                ResetPlannerAttackTarget(target);
            }

            Assert.AreEqual(
                0L,
                GC.GetAllocatedBytesForCurrentThread() - before,
                "Planner evaluator -> adapter -> submit -> command apply -> action guard must be warmed and bounded.");
            Assert.AreEqual(
                measuredTicks,
                attackCommandCount,
                "Every measured host tick must submit a PrimaryAttack intent; observed="
                + attackCommandCount);
            Assert.AreEqual(
                measuredTicks,
                acceptedAttackCount,
                "Every measured host tick must pass the action-level guard; observed="
                + acceptedAttackCount);
        }

        private static void ResetPlannerAttackTarget(CharacterTestCharacterEntity target)
        {
            // Real hit resolution may apply deterministic knockback/reaction motion to the
            // target. Restore only the fixture pose between iterations so each measured tick
            // exercises the same opportunity geometry while still using the real hit path.
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 1f, 0.95f, 0f));
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);
        }

        private static CharacterTestRuntimeAiJointRuntime GetJointRuntime(
            CharacterTestSliceTestHarness harness)
        {
            FieldInfo field = typeof(CharacterTestRuntimeAiPlannerCommandModule)
                .GetField("_jointRuntime", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            CharacterTestRuntimeAiJointRuntime joint = field.GetValue(
                harness.Modules.RuntimeAiPlannerCommands) as CharacterTestRuntimeAiJointRuntime;
            Assert.NotNull(joint);
            return joint;
        }

        private static void ResetPlannerAttackAttempt(
            CharacterTestSliceTestHarness harness,
            CharacterTestCharacterEntity ai,
            long frame)
        {
            harness.Modules.ActionRuntime.TerminateAndReleaseEntityAction(
                ai.Id,
                new RuntimeFrame(frame),
                CharacterActionLifecycleReason.PreviewSeek,
                new MxFramework.Runtime.RuntimeTraceIdentity(94009));
            ai.SetInput(ai.Input.WithoutActionStartRequests().WithoutAbilitySlotCast());
            ai.GameplayIntentBuffer.Clear();
            ai.SetTransform(new CharacterTestCharacterTransformComponent(0f, 1f, 0f, 0f));
            ai.SetMotionState(0f, Vector2.zero, isGrounded: true);
        }

        private static string DescribePlannerAttackAdapter(
            CharacterTestSliceTestHarness harness,
            CharacterTestCharacterEntity source)
        {
            FieldInfo adapterField = typeof(CharacterTestRuntimeAiPlannerCommandModule)
                .GetField("_attackOpportunityAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
            CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter =
                adapterField?.GetValue(harness.Modules.RuntimeAiPlannerCommands)
                    as CharacterTestRuntimeAiPlannerAttackOpportunityAdapter;
            if (adapter == null)
                return "adapter=<null>";
            bool hasEvidence = adapter.TryGetEvidence(
                source.Id.Value,
                out CharacterTestAttackOpportunityDecisionEvidence evidence);
            string evidenceText = hasEvidence
                ? "evidenceFrame=" + evidence.PlannerFrame.Value
                    + " target=" + evidence.TargetEntityId
                    + " source=" + evidence.Source
                    + " status=" + evidence.Status
                    + " level=" + evidence.Level
                    + " failure=" + evidence.FailureReason
                : "evidence=<none>";
            return "guardNow=" + adapter.CanSubmitPrimaryAttack(source, harness.CurrentFrame)
                + " " + evidenceText;
        }
    }
}

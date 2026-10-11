using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestCharacterDistanceM1Tests
    {
        [Test]
        public void OpportunityReuse_RepeatedReadDoesNotRepeatGeometryAndRejectsChangedSource()
        {
            var fixture = CreateFixture(currentHits: true, turnHits: true);
            var first = fixture.Service.Evaluate(fixture.Query);
            long queries = fixture.Service.SampleQueryCount;
            Assert.Greater(queries, 0);
            var second = fixture.Service.Evaluate(fixture.Query);
            Assert.AreEqual(first, second);
            Assert.AreEqual(queries, fixture.Service.SampleQueryCount);
            Assert.AreEqual(1, fixture.Service.ModeCacheHitCount);
            fixture.Query.Source.SetTransform(new CharacterTestCharacterTransformComponent(10f, 0f, 0f, 0f));
            Assert.AreEqual(CharacterTestAttackOpportunityStatus.StaleSnapshot, fixture.Service.Evaluate(fixture.Query).Status);
        }

        [Test]
        public void OpportunityReuse_HurtboxFilterDoesNotReuseAnotherResult()
        {
            var fixture = CreateFixture(currentHits: true, turnHits: true);
            Assert.IsTrue(fixture.Service.Evaluate(fixture.Query).CanHitFromCurrentPose);
            long evaluations = fixture.Service.ExactModeEvaluationCount;
            var other = fixture.Service.Evaluate(fixture.Query.WithDamageZoneFilter(null, 0, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0053));
            Assert.IsFalse(other.CanHitFromCurrentPose);
            Assert.Greater(fixture.Service.ExactModeEvaluationCount, evaluations);
        }

        [TestCase(true, 0)]
        [TestCase(false, 1)]
        public void OpportunityQuery_OnlyResolvesTurnWhenCurrentPoseMisses(bool currentHits, int expectedBuilds)
        {
            var fixture = CreateFixture(currentHits: currentHits, turnHits: true);
            var q = fixture.Query;
            var provider = new CountingTurnProvider(q.TurnInPlacePlan);
            var lazy = new CharacterTestAttackOpportunityQuery(q.Snapshot, q.TargetGeometry, q.PhysicsWorld,
                q.Source, q.Target, q.CurrentPosePlan, true, default, true, q.CurrentCompletedFrame,
                q.CurrentCombatPhysicsRevision, q.CurrentSourcePoseRevision, q.CurrentSourceFacingRevision,
                q.CurrentTargetHurtboxRevision, q.CurrentResolvedActionRevision, q.WorldTargets, q.SamplePolicies,
                turnPlanProvider: provider, turnPlanGeneration: 1);
            lazy = lazy.WithDamageZoneFilter(null, 0);
            Assert.AreEqual(0, provider.Builds, "Copying a query must not prepare its turn plan.");
            var result = fixture.Service.Evaluate(lazy, currentHits ? 0 : 90000);
            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(expectedBuilds, provider.Builds);
        }

        private sealed class CountingTurnProvider : ICharacterTestAttackTurnPlanProvider
        {
            private readonly CharacterTestDirectHitTracePlanView _plan;
            internal int Builds;
            internal CountingTurnProvider(CharacterTestDirectHitTracePlanView plan) { _plan = plan; }
            public bool TryGetTurnPlan(long generation, out CharacterTestDirectHitTracePlanView plan)
            { Builds = 1; plan = _plan; return true; }
        }

        [Test]
        public void OpportunityReuse_ForcedGeometryEvaluationRemainsAllocationFreeAfterWarmup()
        {
            var fixture = CreateFixture(currentHits: false, turnHits: true);
            for (int i = 0; i < 8; i++) { fixture.Service.ClearQueryCache(); fixture.Service.Evaluate(fixture.Query, 90000); }
            using (var positive = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory,
                "GC.Alloc", 1024, Unity.Profiling.ProfilerRecorderOptions.StartImmediately
                    | Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
                System.GC.KeepAlive(new byte[8192]);
                positive.Stop();
                Assert.Greater(positive.Count, 0, "Allocation events must be observable before asserting their absence.");
            }
            using var recorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory,
                "GC.Alloc", 2048, Unity.Profiling.ProfilerRecorderOptions.StartImmediately
                    | Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            for (int i = 0; i < 8; i++) { fixture.Service.ClearQueryCache(); fixture.Service.Evaluate(fixture.Query, 90000); }
            recorder.Stop();
            Assert.IsTrue(recorder.Valid);
            Assert.IsFalse(recorder.WrappedAround);
            Assert.AreEqual(0, recorder.Count, "GC.Alloc Value is time, not bytes. No allocation events may occur during exact geometry.");
        }

        [Test]
        public void AttackOpportunity_CurrentPose_ReturnsAcceptedTraceEvidence()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(fixture.Query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(CharacterTestAttackOpportunityLevel.CurrentPose, result.Level);
            Assert.IsTrue(result.CanHitFromCurrentPose);
            Assert.IsTrue(result.CanHitAfterTurnInPlace);
            Assert.IsTrue(result.EnvelopeIntersectsTarget);
            Assert.IsTrue(result.TraceCanResolveTargetHit);
            Assert.AreEqual(4, result.EarliestCandidateHitTick);
            Assert.AreEqual("tail", result.SelectedHurtboxId);
            Assert.AreEqual("rear", result.SelectedHitZoneId);
            Assert.AreEqual(string.Empty, result.SelectedDamageZoneId);
            Assert.AreEqual(new CombatBodyId(50002), result.SelectedBodyId);
            Assert.AreEqual(new CombatColliderId(1002), result.SelectedColliderId);
            Assert.AreNotEqual(result.SourceWitnessPoint, result.TargetWitnessPoint);
            Assert.IsFalse(result.RequiresTranslation);
            Assert.AreEqual(CharacterTestAttackPredictionAssumption.StaticTarget, result.PredictionAssumption);
            Assert.AreEqual(fixture.Query.Snapshot.SourceEntityId, result.SourceEntityId);
            Assert.AreEqual(fixture.Query.Snapshot.TargetEntityId, result.TargetEntityId);
            Assert.AreEqual(7, result.ActionId);
            Assert.AreEqual(fixture.Query.Snapshot.SimulationFrame, result.SnapshotSimulationFrame);
            Assert.AreEqual(fixture.Query.Snapshot.CombatPhysicsRevision, result.SnapshotCombatPhysicsRevision);
            Assert.AreEqual(fixture.Query.Snapshot.SourcePoseRevision, result.SnapshotSourcePoseRevision);
            Assert.AreEqual(fixture.Query.Snapshot.SourceFacingRevision, result.SnapshotSourceFacingRevision);
            Assert.AreEqual(fixture.Query.Snapshot.TargetHurtboxRevision, result.SnapshotTargetHurtboxRevision);
            Assert.AreEqual(fixture.Query.Snapshot.ResolvedActionRevision, result.SnapshotResolvedActionRevision);
            Assert.AreEqual(0, result.ExpectedSourceYawMilliDegrees);
            Assert.AreEqual(0, result.EvaluatedSourceYawMilliDegrees);
            Assert.IsTrue(result.OrientationMatched);
            Assert.IsTrue(result.VerticalOverlap);
            Assert.IsTrue(result.MaskCompatible);
            Assert.IsTrue(result.QualificationAccepted);
        }

        [Test]
        public void AttackOpportunity_MissingDamageMatchesActualRejectedCandidate()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            CharacterActionDamagePayload damage = CharacterActionDamagePayload.Empty;
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(
                samplePolicies: BuildPoliciesWithDamage(
                    fixture.Query.Snapshot,
                    fixture.Query.Source.Team.TeamId,
                    fixture.Query.Target.Team.TeamId,
                    HitTargetStateFlags.Alive,
                    CreateHitEvent(blockedPresentation: false),
                    damage,
                    fixture.Query.CurrentPosePlan,
                    fixture.Query.TurnInPlacePlan));

            CharacterTestAttackOpportunityResult preview = fixture.Service.Evaluate(query);
            int actualCandidateCount = ResolveActualDirectHitCandidates(
                damage,
                out _);

            Assert.AreEqual(0, actualCandidateCount);
            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Unavailable, preview.Status);
            Assert.AreEqual(
                CharacterTestAttackOpportunityFailureReason.DamagePayloadUnavailable,
                preview.FailureReason);
            Assert.AreEqual(
                CharacterTestAttackOpportunityFailureFlags.DamagePayloadUnavailable,
                preview.FailureFlags);
            Assert.IsFalse(preview.TraceCanResolveTargetHit);
        }

        [Test]
        public void AttackOpportunity_InvalidDamageMatchesActualRejectedCandidate()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            var damage = new CharacterActionDamagePayload(
                damageTypeId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D409_Id2,
                formulaId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0045,
                baseDamage: 1);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(
                samplePolicies: BuildPoliciesWithDamage(
                    fixture.Query.Snapshot,
                    fixture.Query.Source.Team.TeamId,
                    fixture.Query.Target.Team.TeamId,
                    HitTargetStateFlags.Alive,
                    CreateHitEvent(blockedPresentation: false),
                    damage,
                    fixture.Query.CurrentPosePlan,
                    fixture.Query.TurnInPlacePlan));

            CharacterTestAttackOpportunityResult preview = fixture.Service.Evaluate(query);
            int actualCandidateCount = ResolveActualDirectHitCandidates(
                damage,
                out _);

            Assert.AreEqual(0, actualCandidateCount);
            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Unavailable, preview.Status);
            Assert.AreEqual(
                CharacterTestAttackOpportunityFailureReason.DamagePayloadInvalid,
                preview.FailureReason);
            Assert.AreEqual(
                CharacterTestAttackOpportunityFailureFlags.DamagePayloadInvalid,
                preview.FailureFlags);
            Assert.IsFalse(preview.TraceCanResolveTargetHit);
        }

        [Test]
        public void AttackOpportunity_HitZoneFailureUsesSameActualCompatibilityFallback()
        {
            Fixture fixture = CreateFixture(
                currentHits: true,
                turnHits: true,
                targetPartId: string.Empty,
                targetHitZoneId: string.Empty);

            CharacterTestAttackOpportunityResult preview = fixture.Service.Evaluate(fixture.Query);
            int actualCandidateCount = ResolveActualDirectHitCandidates(
                CreateValidDamagePayload(),
                out CharacterTestHitZoneResolution actualHitZone);

            Assert.AreEqual(1, actualCandidateCount);
            Assert.AreEqual(CharacterTestHitZoneDefaults.HitZoneId, actualHitZone.HitZoneId);
            Assert.AreEqual(CharacterTestHitZoneDefaults.BodyPartId, actualHitZone.BodyPartId);
            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, preview.Status);
            Assert.IsTrue(preview.TraceCanResolveTargetHit);
            Assert.AreEqual(CharacterTestHitZoneDefaults.HitZoneId, preview.SelectedHitZoneId);
        }

        [Test]
        public void AttackOpportunity_ValidDamageMatchesActualAcceptedCandidate()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            CharacterActionDamagePayload damage = CreateValidDamagePayload();

            CharacterTestAttackOpportunityResult preview = fixture.Service.Evaluate(fixture.Query);
            int actualCandidateCount = ResolveActualDirectHitCandidates(
                damage,
                out _);

            Assert.AreEqual(1, actualCandidateCount);
            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, preview.Status);
            Assert.IsTrue(preview.TraceCanResolveTargetHit);
            Assert.AreEqual(
                CharacterTestDirectHitDamageEligibility.Eligible,
                CharacterTestCombatDamageResolver.EvaluateDirectHitDamageEligibility(
                    damage,
                    CharacterTestHitZoneResolution.Default));
        }

        [Test]
        public void PreviewWeaponEvents_SharedTraceProfileKeepIndependentPolicyEventIds()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            RuntimeFrame frame = fixture.Query.CurrentCompletedFrame;
            CombatPhysicsLayerMask mask = CombatPhysicsLayerMask.FromLayer(
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer);
            var poses = new List<CharacterTestDirectHitTraceSourcePoseOffset>
            {
                new CharacterTestDirectHitTraceSourcePoseOffset(0, FixVector3.Zero, 0)
            };
            var weapons = new List<CharacterTestDirectHitTracePreviewWeaponFrame>
            {
                new CharacterTestDirectHitTracePreviewWeaponFrame(
                    0,
                    0,
                    traceId: 71,
                    policyEventId: 701,
                    actionId: 7,
                    V(0, 0, 0),
                    V(1, 0, 0),
                    V(0, 0, 0),
                    V(1, 0, 0),
                    F(1) / F(4),
                    mask,
                    Fix64.Zero,
                    Fix64.Zero),
                new CharacterTestDirectHitTracePreviewWeaponFrame(
                    0,
                    1,
                    traceId: 71,
                    policyEventId: 702,
                    actionId: 7,
                    V(0, 0, 0),
                    V(1, 0, 0),
                    V(0, 0, 0),
                    V(1, 0, 0),
                    F(1) / F(4),
                    mask,
                    Fix64.Zero,
                    Fix64.Zero)
            };
            var output = new CharacterTestDirectHitTracePlanBuffer();
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(
                frame,
                fixture.World.Revision,
                fixture.Query.Snapshot.SourceEntityId,
                fixture.Query.CurrentResolvedActionRevision,
                CreatePlanPoseIdentity(),
                poses,
                weapons,
                new List<CharacterTestDirectHitTracePreviewCapsuleFrame>(),
                useTipSweepCapsules: false,
                maxTipDistancePerSubstep: F(1),
                maxSubsteps: 1,
                new List<CombatCapsuleQuery>(),
                output,
                out CharacterTestDirectHitTracePlanView plan));

            Assert.AreEqual(2, plan.Count);
            Assert.IsTrue(plan.TryGetSample(0, out CharacterTestDirectHitTracePlanSample first));
            Assert.IsTrue(plan.TryGetSample(1, out CharacterTestDirectHitTracePlanSample second));
            Assert.AreEqual(71, first.TraceId);
            Assert.AreEqual(71, second.TraceId);
            Assert.AreNotEqual(first.PolicyEventId, second.PolicyEventId);
            Assert.AreNotEqual(first.StableSampleId, second.StableSampleId);
        }

        [Test]
        public void DirectHitResolution_SharedTraceUsesIndependentPolicyEventIds()
        {
            CombatEntityId attackerId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1);
            CombatEntityId targetId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 2);
            CombatPhysicsLayerMask layerMask = CombatPhysicsLayerMask.FromLayer(
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer);
            CombatQueryHeader firstQuery = new CombatQueryHeader(
                queryId: 1,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: attackerId,
                traceId: 71,
                actionId: 7,
                sourceOrder: 0,
                layerMask: layerMask);
            CombatQueryHeader secondQuery = new CombatQueryHeader(
                queryId: 2,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: attackerId,
                traceId: 71,
                actionId: 7,
                sourceOrder: 0,
                layerMask: layerMask);
            CombatQueryResult firstHit = new CombatQueryResult(
                firstQuery,
                targetId,
                new CombatBodyId(50002),
                new CombatColliderId(1002),
                Fix64.Zero,
                V(3, 0, 0),
                V(0, 1, 0));
            CombatQueryResult secondHit = new CombatQueryResult(
                secondQuery,
                targetId,
                new CombatBodyId(50002),
                new CombatColliderId(1002),
                Fix64.FromInt(1),
                V(3, 0, 0),
                V(0, 1, 0));
            var candidates = new List<HitCandidate>
            {
                new HitCandidate(
                    attackerId,
                    targetId,
                    actionId: 7,
                    actionInstanceId: 42,
                    traceId: 71,
                    frame: new CombatFrame(4),
                    physicsHit: firstHit,
                    damage: 0,
                    staggerFrames: 0,
                    knockback: FixVector3.Zero,
                    targetState: HitTargetStateFlags.Alive),
                new HitCandidate(
                    attackerId,
                    targetId,
                    actionId: 7,
                    actionInstanceId: 42,
                    traceId: 71,
                    frame: new CombatFrame(5),
                    physicsHit: secondHit,
                    damage: 0,
                    staggerFrames: 0,
                    knockback: FixVector3.Zero,
                    targetState: HitTargetStateFlags.Alive)
            };
            CombatHitEventConfig hitEvent = new CombatHitEventConfig(
                CombatHitTargetTypes.HostileCharacter,
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0044,
                maxAcceptedHits: 1,
                hitSameTargetPolicy: CombatHitSameTargetPolicy.AllowRepeat);
            var trace = new CharacterTestActiveHitTrace(
                entityId: 1,
                actionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("m1.shared-trace"),
                sourceInstanceId: 1,
                sourceActionInstanceId: 42,
                hitWindowId: 42,
                equipmentSlotId: 0,
                traceProfileId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0044,
                damagePayload: default(CharacterActionDamagePayload),
                hitEvent: hitEvent,
                hitVolume: default(CharacterActionHitVolumeConfig),
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0044,
                startFrame: 0);
            var payloads = new Dictionary<CharacterTestTracePayloadKey, CharacterTestTracePayload>
            {
                [new CharacterTestTracePayloadKey(42, 71)] =
                    new CharacterTestTracePayload(
                        trace,
                        default(CharacterTestWeaponTraceBinding),
                        string.Empty,
                        localFrame: 0)
            };
            var policyEventIds = new Dictionary<CombatQueryHeader, int>
            {
                [firstQuery] = 701,
                [secondQuery] = 702
            };
            var state = new CharacterTestHitResolutionPolicyState();
            var results = new List<HitResolveResult>();
            var service = new CharacterTestDirectHitResolutionService();

            service.Resolve(
                candidates,
                payloads,
                policyEventIds,
                state,
                results,
                teamRelations: null);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(HitResolveKind.Damage, results[0].Kind);
            Assert.AreEqual(HitResolveKind.Damage, results[1].Kind);
            Assert.AreEqual(71, results[0].TraceId);
            Assert.AreEqual(71, results[1].TraceId);
            Assert.AreEqual(0, service.MissingPolicyCount);
        }

        [Test]
        public void DirectHitResolution_MissingPayloadFailsClosed()
        {
            CombatEntityId attackerId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1);
            CombatEntityId targetId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 2);
            CombatQueryHeader query = new CombatQueryHeader(
                queryId: 1,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: attackerId,
                traceId: 71,
                actionId: 7,
                sourceOrder: 0,
                layerMask: CombatPhysicsLayerMask.FromLayer(
                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer));
            var candidates = new List<HitCandidate>
            {
                new HitCandidate(
                    attackerId,
                    targetId,
                    actionId: 7,
                    actionInstanceId: 42,
                    traceId: 71,
                    frame: new CombatFrame(4),
                    physicsHit: new CombatQueryResult(
                        query,
                        targetId,
                        new CombatBodyId(50002),
                        new CombatColliderId(1002),
                        Fix64.Zero,
                        V(3, 0, 0),
                        V(0, 1, 0)),
                    damage: 0,
                    staggerFrames: 0,
                    knockback: FixVector3.Zero,
                    targetState: HitTargetStateFlags.Alive)
            };
            var state = new CharacterTestHitResolutionPolicyState();
            var results = new List<HitResolveResult>();
            var service = new CharacterTestDirectHitResolutionService();

            service.Resolve(
                candidates,
                payloadsByTrace: null,
                policyEventIdsByQuery: null,
                policyState: state,
                results: results,
                teamRelations: null);

            Assert.AreEqual(0, results.Count);
            Assert.AreEqual(1, service.MissingPolicyCount);
        }

        [Test]
        public void DirectHitResolution_OncePerEventPersistsAcrossTicksAndColliders()
        {
            DirectHitPolicyFixture fixture = CreateDirectHitPolicyFixture(
                new CombatHitEventConfig(
                    CombatHitTargetTypes.HostileCharacter,
                    stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0040,
                    maxAcceptedHits: 4,
                    hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent),
                includeThirdCollider: true);
            var state = new CharacterTestHitResolutionPolicyState();
            var results = new List<HitResolveResult>(1);
            var tickCandidates = new List<HitCandidate>(1);
            var service = new CharacterTestDirectHitResolutionService();

            for (int tick = 0; tick < fixture.Candidates.Count; tick++)
            {
                tickCandidates.Clear();
                tickCandidates.Add(fixture.Candidates[tick]);
                results.Clear();
                service.Resolve(
                    tickCandidates,
                    fixture.Payloads,
                    fixture.PolicyEventIds,
                    state,
                    results,
                    teamRelations: null);

                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(
                    tick == 0 ? HitResolveKind.Damage : HitResolveKind.Duplicate,
                    results[0].Kind);
                Assert.AreEqual(71, results[0].TraceId);
            }
        }

        [Test]
        public void DirectHitResolution_AllowRepeatHonorsMaxAcceptedHitsAcrossTicks()
        {
            DirectHitPolicyFixture fixture = CreateDirectHitPolicyFixture(
                new CombatHitEventConfig(
                    CombatHitTargetTypes.HostileCharacter,
                    stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0037,
                    maxAcceptedHits: 2,
                    hitSameTargetPolicy: CombatHitSameTargetPolicy.AllowRepeat),
                includeThirdCollider: true);
            var state = new CharacterTestHitResolutionPolicyState();
            var results = new List<HitResolveResult>(1);
            var tickCandidates = new List<HitCandidate>(1);
            var service = new CharacterTestDirectHitResolutionService();

            for (int tick = 0; tick < fixture.Candidates.Count; tick++)
            {
                tickCandidates.Clear();
                tickCandidates.Add(fixture.Candidates[tick]);
                results.Clear();
                service.Resolve(
                    tickCandidates,
                    fixture.Payloads,
                    fixture.PolicyEventIds,
                    state,
                    results,
                    teamRelations: null);

                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(
                    tick < 2 ? HitResolveKind.Damage : HitResolveKind.Duplicate,
                    results[0].Kind);
            }
        }

        [Test]
        public void DirectHitResolution_ReplayStateRestoresPolicyLedgerAcrossTicks()
        {
            DirectHitPolicyFixture fixture = CreateDirectHitPolicyFixture(
                new CombatHitEventConfig(
                    CombatHitTargetTypes.HostileCharacter,
                    stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0043,
                    maxAcceptedHits: 2,
                    hitSameTargetPolicy: CombatHitSameTargetPolicy.AllowRepeat),
                includeThirdCollider: true);
            var liveWindow = new CharacterTestHitTraceWindowState();
            var results = new List<HitResolveResult>(1);
            var tickCandidates = new List<HitCandidate>(1);
            var service = new CharacterTestDirectHitResolutionService();

            tickCandidates.Add(fixture.Candidates[0]);
            service.Resolve(
                tickCandidates,
                fixture.Payloads,
                fixture.PolicyEventIds,
                liveWindow.HitResolutionPolicyState,
                results,
                teamRelations: null);
            Assert.AreEqual(HitResolveKind.Damage, results[0].Kind);

            CharacterTestHitTraceWindowReplayState replay = liveWindow.CaptureReplayState();
            var restoredWindow = new CharacterTestHitTraceWindowState();
            restoredWindow.RestoreReplayState(replay);

            tickCandidates.Clear();
            tickCandidates.Add(fixture.Candidates[1]);
            results.Clear();
            service.Resolve(
                tickCandidates,
                fixture.Payloads,
                fixture.PolicyEventIds,
                restoredWindow.HitResolutionPolicyState,
                results,
                teamRelations: null);
            Assert.AreEqual(HitResolveKind.Damage, results[0].Kind);

            tickCandidates.Clear();
            tickCandidates.Add(fixture.Candidates[2]);
            results.Clear();
            service.Resolve(
                tickCandidates,
                fixture.Payloads,
                fixture.PolicyEventIds,
                restoredWindow.HitResolutionPolicyState,
                results,
                teamRelations: null);
            Assert.AreEqual(HitResolveKind.Duplicate, results[0].Kind);
        }

        [Test]
        public void DirectHitResolution_WarmedResolveAllocatesZeroBytes()
        {
            DirectHitPolicyFixture fixture = CreateDirectHitPolicyFixture(
                new CombatHitEventConfig(
                    CombatHitTargetTypes.HostileCharacter,
                    stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0046,
                    maxAcceptedHits: 1,
                    hitSameTargetPolicy: CombatHitSameTargetPolicy.AllowRepeat),
                includeThirdCollider: false);
            var state = new CharacterTestHitResolutionPolicyState();
            var results = new List<HitResolveResult>(1);
            var tickCandidates = new List<HitCandidate>(1);
            var service = new CharacterTestDirectHitResolutionService();
            tickCandidates.Add(fixture.Candidates[0]);
            service.Resolve(tickCandidates, fixture.Payloads, fixture.PolicyEventIds, state, results, null);
            results.Clear();
            state.Clear();
            service.Resolve(tickCandidates, fixture.Payloads, fixture.PolicyEventIds, state, results, null);
            results.Clear();
            state.Clear();

            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            service.Resolve(tickCandidates, fixture.Payloads, fixture.PolicyEventIds, state, results, null);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(HitResolveKind.Damage, results[0].Kind);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void AttackOpportunity_NonTargetActorWithoutFrozenPolicyFailsClosed()
        {
            Fixture fixture = CreateFixture(
                currentHits: true,
                turnHits: true,
                addFrontActor: true);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(fixture.Query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Unavailable, result.Status);
            Assert.AreEqual(
                CharacterTestAttackOpportunityFailureReason.NonTargetActorStateUnavailable,
                result.FailureReason);
            Assert.IsTrue(
                (result.FailureFlags & CharacterTestAttackOpportunityFailureFlags.NonTargetActorStateUnavailable) != 0);
            Assert.IsFalse(result.TraceCanResolveTargetHit);
        }

        [Test]
        public void AttackOpportunity_TurnInPlace_ReturnsQuantizedTurnBeforeAttack()
        {
            Fixture fixture = CreateFixture(currentHits: false, turnHits: true);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(
                fixture.Query,
                requiredYawDeltaMilliDegrees: 90000);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(CharacterTestAttackEvaluationMode.TurnInPlace, result.Mode);
            Assert.AreEqual(CharacterTestAttackOpportunityLevel.TurnInPlace, result.Level);
            Assert.IsFalse(result.CanHitFromCurrentPose);
            Assert.IsTrue(result.CanHitAfterTurnInPlace);
            Assert.AreEqual(90000, result.RequiredYawDeltaMilliDegrees);
            Assert.AreEqual(90000, result.ExpectedSourceYawMilliDegrees);
            Assert.AreEqual(90000, result.EvaluatedSourceYawMilliDegrees);
            Assert.IsTrue(result.OrientationMatched);
            Assert.AreEqual("tail", result.SelectedHurtboxId);
            Assert.AreEqual(4, result.EarliestCandidateHitTick);
        }

        [Test]
        public void AttackOpportunity_WhenBothPosesSpatiallyMiss_RequiresTranslation()
        {
            Fixture fixture = CreateFixture(currentHits: false, turnHits: false);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(
                fixture.Query,
                requiredYawDeltaMilliDegrees: 90000);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(CharacterTestAttackOpportunityLevel.RequiresTranslation, result.Level);
            Assert.IsTrue(result.HasCompatibleHurtbox);
            Assert.IsFalse(result.TraceCanResolveTargetHit);
            Assert.IsTrue(result.RequiresTranslation);
            Assert.AreEqual(-1, result.EarliestCandidateHitTick);
            Assert.AreEqual(CharacterTestAttackOpportunityFailureReason.NoAcceptedHit, result.FailureReason);
        }

        [Test]
        public void AttackOpportunity_CandidatePose_UsesSameTraceEvaluator()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);

            CharacterTestDirectHitTracePlanView candidatePlan = BuildPlan(
                fixture.World,
                fixture.Query.CurrentCompletedFrame,
                fixture.Query.Snapshot.SourceEntityId,
                fixture.Query.CurrentResolvedActionRevision,
                CharacterTestDirectHitTracePlanPoseKind.CandidatePose,
                sourceYawMilliDegrees: 0,
                hit: true,
                useWorldMask: false,
                verticalMiss: false,
                includeDuplicateHitSample: false,
                forceIncompatibleMask: false,
                sourcePosition: V(0, 0, 0));
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(
                samplePolicies: BuildPolicies(
                    fixture.Query.Snapshot,
                    fixture.Query.Source.Team.TeamId,
                    fixture.Query.Target.Team.TeamId,
                    HitTargetStateFlags.Alive,
                    CreateHitEvent(blockedPresentation: false),
                    fixture.Query.CurrentPosePlan,
                    fixture.Query.TurnInPlacePlan,
                    candidatePlan))
                .WithCandidatePose(candidatePlan, V(0, 0, 0), 0);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(
                query,
                CharacterTestAttackEvaluationMode.CandidatePose);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(CharacterTestAttackEvaluationMode.CandidatePose, result.Mode);
            Assert.AreEqual(CharacterTestAttackOpportunityLevel.RequiresTranslation, result.Level);
            Assert.IsTrue(result.TraceCanResolveTargetHit);
            Assert.IsTrue(result.RequiresTranslation);
            Assert.AreEqual(4, result.EarliestCandidateHitTick);
        }

        [Test]
        public void AttackOpportunity_EmptyTracePlanRetainsTracePlanInvalidReason()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            CharacterTestDirectHitTracePlanView emptyPlan = BuildEmptyPlan(
                fixture.World,
                fixture.Query.CurrentCompletedFrame,
                fixture.Query.Snapshot.SourceEntityId,
                fixture.Query.CurrentResolvedActionRevision);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(currentPlan: emptyPlan);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(
                query,
                CharacterTestAttackEvaluationMode.CurrentPose);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Unavailable, result.Status);
            Assert.AreEqual(
                CharacterTestAttackOpportunityFailureReason.TracePlanInvalid,
                result.FailureReason);
            Assert.IsFalse(result.IsUsable);
        }

        [Test]
        public void AttackOpportunity_HeterogeneousActionPlanIsUnavailable()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            CharacterTestDirectHitTracePlanView heterogeneous = BuildHeterogeneousActionPlan(
                fixture.World,
                fixture.Query.CurrentCompletedFrame,
                fixture.Query.Snapshot.SourceEntityId,
                fixture.Query.CurrentResolvedActionRevision);
            CharacterTestAttackOpportunitySamplePolicySnapshot policies = BuildPolicies(
                fixture.Query.Snapshot,
                sourceTeamId: 1,
                targetTeamId: 2,
                HitTargetStateFlags.Alive,
                CreateHitEvent(blockedPresentation: false),
                heterogeneous);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(
                currentPlan: heterogeneous,
                samplePolicies: policies);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(
                query,
                CharacterTestAttackEvaluationMode.CurrentPose);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Unavailable, result.Status);
            Assert.AreEqual(
                CharacterTestAttackOpportunityFailureReason.TracePlanInvalid,
                result.FailureReason);
            Assert.IsFalse(result.IsUsable);
        }

        [Test]
        public void AttackOpportunity_WorldRevisionMismatch_IsStale()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            fixture.World.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(9100),
                new CombatBodyId(9100),
                V(20, 0, 0)));

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(fixture.Query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.StaleSnapshot, result.Status);
            Assert.AreEqual(CharacterTestAttackOpportunityFailureReason.StaleSnapshot, result.FailureReason);
        }

        [Test]
        public void AttackOpportunity_CurrentCompletedFrameMismatchAlone_IsStale()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(
                currentCompletedFrame: fixture.Query.CurrentCompletedFrame.Next());

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.StaleSnapshot, result.Status);
            Assert.IsTrue(
                (result.FailureFlags & CharacterTestAttackOpportunityFailureFlags.SnapshotMismatch) != 0);
        }

        [Test]
        public void AttackOpportunity_CurrentResolvedActionRevisionMismatchAlone_IsStale()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(
                currentResolvedActionRevision: fixture.Query.CurrentResolvedActionRevision + 1);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.StaleSnapshot, result.Status);
            Assert.IsTrue(
                (result.FailureFlags & CharacterTestAttackOpportunityFailureFlags.SnapshotMismatch) != 0);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AttackOpportunity_WorldClassificationFrameOrRevisionMismatch_IsStale(
            bool revisionMismatch)
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            RuntimeFrame classificationFrame = revisionMismatch
                ? fixture.Query.CurrentCompletedFrame
                : fixture.Query.CurrentCompletedFrame.Next();
            int classificationRevision = revisionMismatch
                ? fixture.World.Revision + 1
                : fixture.World.Revision;
            var classifications = new CharacterTestCombatWorldTargetClassificationSnapshot();
            classifications.Reset(classificationFrame, classificationRevision);
            Assert.IsTrue(classifications.Seal(classificationFrame, classificationRevision));
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(
                worldTargets: classifications);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.StaleSnapshot, result.Status);
            Assert.AreEqual(
                CharacterTestAttackOpportunityFailureReason.StaleSnapshot,
                result.FailureReason);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void AttackOpportunity_CurrentPosePlanSourceIdentityMismatch_IsStale(int mismatchKind)
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            CharacterTestDirectHitTracePlanView mismatchedPlan = BuildPlan(
                fixture.World,
                fixture.Query.CurrentCompletedFrame,
                fixture.Query.Snapshot.SourceEntityId,
                fixture.Query.CurrentResolvedActionRevision,
                CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
                sourceYawMilliDegrees: mismatchKind == 1 ? 1000 : 0,
                hit: true,
                useWorldMask: false,
                verticalMiss: false,
                includeDuplicateHitSample: false,
                forceIncompatibleMask: false,
                sourcePosition: mismatchKind == 0 ? V(1, 0, 0) : V(0, 0, 0),
                sourcePoseRevision: mismatchKind == 2 ? 2 : 1,
                sourceFacingRevision: mismatchKind == 3 ? 2 : 1);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(currentPlan: mismatchedPlan);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(
                query,
                CharacterTestAttackEvaluationMode.CurrentPose);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.StaleSnapshot, result.Status);
            Assert.IsTrue(
                (result.FailureFlags & CharacterTestAttackOpportunityFailureFlags.TracePlanMismatch) != 0);
        }

        [Test]
        public void AttackOpportunity_TurnInPlacePlanMustBindExpectedTurnYaw()
        {
            Fixture fixture = CreateFixture(currentHits: false, turnHits: true);
            CharacterTestDirectHitTracePlanView wrongTurnPlan = BuildPlan(
                fixture.World,
                fixture.Query.CurrentCompletedFrame,
                fixture.Query.Snapshot.SourceEntityId,
                fixture.Query.CurrentResolvedActionRevision,
                CharacterTestDirectHitTracePlanPoseKind.TurnInPlace,
                sourceYawMilliDegrees: 0,
                hit: true,
                useWorldMask: false,
                verticalMiss: false,
                includeDuplicateHitSample: false,
                forceIncompatibleMask: false);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(turnPlan: wrongTurnPlan);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(
                query,
                CharacterTestAttackEvaluationMode.TurnInPlace,
                requiredYawDeltaMilliDegrees: 90000);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.StaleSnapshot, result.Status);
            Assert.IsTrue(
                (result.FailureFlags & CharacterTestAttackOpportunityFailureFlags.TracePlanMismatch) != 0);
        }

        [Test]
        public void AttackOpportunity_BlockedPresentationUsesZeroPenetration()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true, addBlocker: true);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(fixture.Query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.IsFalse(result.TraceCanResolveTargetHit);
            Assert.IsFalse(result.RequiresTranslation);
            Assert.AreEqual(
                CharacterTestAttackOpportunityFailureReason.BlockedByEnvironment,
                result.FailureReason);
            Assert.IsTrue(
                (result.FailureFlags & CharacterTestAttackOpportunityFailureFlags.BlockedByEnvironment) != 0);
            Assert.AreEqual(new CombatBodyId(60001), result.BlockerBodyId);
            Assert.AreEqual(new CombatColliderId(60001), result.BlockerColliderId);
            Assert.AreEqual(CharacterTestCombatWorldTargetKind.Wall, result.BlockerWorldTargetKind);
        }

        [Test]
        public void AttackOpportunity_KnownWorldColliderUsesSameOrderedBlockerForActualAndPreview()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true, addBlocker: true);
            Assert.IsTrue(fixture.Query.CurrentPosePlan.TryGetSample(
                0,
                out CharacterTestDirectHitTracePlanSample sample));
            var orderedHits = new List<CombatQueryResult>();
            fixture.World.QueryCapsule(sample.Query, orderedHits, includeSourceEntity: false);

            var actualCandidates = new List<HitCandidate>(orderedHits.Count);
            for (int i = 0; i < orderedHits.Count; i++)
            {
                actualCandidates.Add(new HitCandidate(
                    sample.Query.Header.SourceEntityId,
                    orderedHits[i].TargetEntityId,
                    sample.ActionId,
                    sample.HitWindowId,
                    sample.TraceId,
                    new CombatFrame(24),
                    orderedHits[i],
                    damage: 0,
                    staggerFrames: 0,
                    knockback: FixVector3.Zero,
                    targetState: HitTargetStateFlags.Alive));
            }

            var trace = new CharacterTestActiveHitTrace(
                entityId: 1,
                actionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("m1.actual-preview-known"),
                sourceInstanceId: 1,
                sourceActionInstanceId: sample.HitWindowId,
                hitWindowId: sample.HitWindowId,
                equipmentSlotId: 0,
                traceProfileId: 0,
                damagePayload: default(CharacterActionDamagePayload),
                hitEvent: CreateWorldHitEvent(
                    CombatHitTargetTypes.HostileCharacter | CombatHitTargetTypes.WorldWall),
                hitVolume: default(CharacterActionHitVolumeConfig),
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0036,
                startFrame: 0);
            var payloads = new Dictionary<CharacterTestTracePayloadKey, CharacterTestTracePayload>
            {
                [new CharacterTestTracePayloadKey(sample.HitWindowId, sample.TraceId)] =
                    new CharacterTestTracePayload(
                        trace,
                        default(CharacterTestWeaponTraceBinding),
                        string.Empty,
                        sample.LocalFrame)
            };
            var lookup = new CharacterTestCombatEntityLookup();
            lookup.Configure(new CharacterTestCharacterRuntimeWorld());
            var actualResolved = new List<HitCandidate>();
            var actualDamage = new Dictionary<CharacterTestResolvedHitKey, DamageProfileResolveResult>();
            var damageResolver = new CharacterTestCombatDamageResolver(reportError: null);
            damageResolver.ResolveCandidates(
                actualCandidates,
                payloads,
                lookup,
                actualResolved,
                actualDamage,
                null,
                null,
                null,
                fixture.World,
                fixture.Query.CurrentCompletedFrame,
                fixture.Query.WorldTargets);

            Assert.AreEqual(1, actualResolved.Count);
            Assert.IsTrue(CharacterTestCombatHitEventResolver.IsStaticEnvironmentTarget(
                actualResolved[0].TargetId));
            Assert.AreEqual(new CombatBodyId(60001), actualResolved[0].PhysicsHit.TargetBodyId);
            Assert.AreEqual(new CombatColliderId(60001), actualResolved[0].PhysicsHit.TargetColliderId);
            Assert.AreEqual(
                CharacterTestCombatWorldQualification.QualifiedBlocker,
                CharacterTestCombatHitEventResolver.QualifyWorldTarget(
                    trace.HitEvent,
                    actualResolved[0].PhysicsHit,
                    fixture.Query.CurrentCompletedFrame,
                    fixture.World,
                    fixture.Query.WorldTargets,
                    out CharacterTestCombatWorldTargetKind actualKind));
            Assert.AreEqual(CharacterTestCombatWorldTargetKind.Wall, actualKind);

            CharacterTestAttackOpportunityResult preview = fixture.Service.Evaluate(fixture.Query);
            Assert.AreEqual(CharacterTestAttackOpportunityFailureReason.BlockedByEnvironment, preview.FailureReason);
            Assert.AreEqual(actualResolved[0].PhysicsHit.TargetBodyId, preview.BlockerBodyId);
            Assert.AreEqual(actualResolved[0].PhysicsHit.TargetColliderId, preview.BlockerColliderId);
            Assert.AreEqual(actualKind, preview.BlockerWorldTargetKind);
        }

        [Test]
        public void AttackOpportunity_UnknownWorldColliderBlocksOrderedTargetWithoutClaimingSubtype()
        {
            Fixture fixture = CreateFixture(
                currentHits: true,
                turnHits: true,
                addBlocker: true,
                classifyBlockers: false);
            Assert.IsTrue(fixture.Query.CurrentPosePlan.TryGetSample(
                0,
                out CharacterTestDirectHitTracePlanSample sample));
            var orderedHits = new List<CombatQueryResult>();
            fixture.World.QueryCapsule(sample.Query, orderedHits, includeSourceEntity: false);
            int blockerIndex = -1;
            int targetIndex = -1;
            for (int i = 0; i < orderedHits.Count; i++)
            {
                if (CharacterTestCombatHitEventResolver.IsStaticEnvironmentTarget(
                        orderedHits[i].TargetEntityId))
                {
                    blockerIndex = i;
                }
                else if (orderedHits[i].TargetEntityId.Equals(fixture.Query.Snapshot.TargetEntityId))
                {
                    targetIndex = i;
                }
            }

            Assert.GreaterOrEqual(blockerIndex, 0);
            Assert.Greater(targetIndex, blockerIndex);
            Assert.AreEqual(
                CharacterTestCombatWorldQualification.UnknownFailClosedBlocker,
                CharacterTestCombatHitEventResolver.QualifyWorldTarget(
                    CreateWorldHitEvent(CombatHitTargetTypes.WorldWall),
                    orderedHits[blockerIndex],
                    fixture.Query.CurrentCompletedFrame,
                    fixture.World,
                    fixture.Query.WorldTargets,
                    out CharacterTestCombatWorldTargetKind resolvedKind));
            Assert.AreEqual(CharacterTestCombatWorldTargetKind.Unknown, resolvedKind);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(fixture.Query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(
                CharacterTestAttackOpportunityFailureReason.BlockedByEnvironment,
                result.FailureReason);
            Assert.IsFalse(result.TraceCanResolveTargetHit);
            Assert.AreEqual(CharacterTestCombatWorldTargetKind.Unknown, result.BlockerWorldTargetKind);

            var actualCandidates = new List<HitCandidate>(orderedHits.Count);
            var candidateFrame = new CombatFrame(24);
            for (int i = 0; i < orderedHits.Count; i++)
            {
                actualCandidates.Add(new HitCandidate(
                    sample.Query.Header.SourceEntityId,
                    orderedHits[i].TargetEntityId,
                    sample.ActionId,
                    sample.HitWindowId,
                    sample.TraceId,
                    candidateFrame,
                    orderedHits[i],
                    damage: 0,
                    staggerFrames: 0,
                    knockback: FixVector3.Zero,
                    HitTargetStateFlags.Alive));
            }

            var trace = new CharacterTestActiveHitTrace(
                entityId: 1,
                actionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("m1.actual-preview"),
                sourceInstanceId: 1,
                sourceActionInstanceId: sample.HitWindowId,
                hitWindowId: sample.HitWindowId,
                equipmentSlotId: 0,
                traceProfileId: 0,
                damagePayload: default(CharacterActionDamagePayload),
                hitEvent: CreateWorldHitEvent(CombatHitTargetTypes.WorldWall),
                hitVolume: default(CharacterActionHitVolumeConfig),
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0035,
                startFrame: 0);
            var payloads = new Dictionary<CharacterTestTracePayloadKey, CharacterTestTracePayload>
            {
                [new CharacterTestTracePayloadKey(sample.HitWindowId, sample.TraceId)] =
                    new CharacterTestTracePayload(
                        trace,
                        default(CharacterTestWeaponTraceBinding),
                        string.Empty,
                        sample.LocalFrame)
            };
            var lookup = new CharacterTestCombatEntityLookup();
            lookup.Configure(new CharacterTestCharacterRuntimeWorld());
            var actualResolved = new List<HitCandidate>();
            var actualDamage = new Dictionary<CharacterTestResolvedHitKey, DamageProfileResolveResult>();
            var damageResolver = new CharacterTestCombatDamageResolver(reportError: null);

            damageResolver.ResolveCandidates(
                actualCandidates,
                payloads,
                lookup,
                actualResolved,
                actualDamage,
                null,
                null,
                null,
                fixture.World,
                fixture.Query.CurrentCompletedFrame,
                fixture.Query.WorldTargets);

            Assert.AreEqual(1, actualResolved.Count);
            Assert.IsTrue(CharacterTestCombatHitEventResolver.IsStaticEnvironmentTarget(
                actualResolved[0].TargetId));
        }

        [Test]
        public void HitEventResolver_WorldKindQualificationIsExplicitAndUnknownFallsBackOnlyToStatic()
        {
            CombatHitEventConfig staticOnly = CreateWorldHitEvent(CombatHitTargetTypes.WorldStatic);
            CombatHitEventConfig wallOnly = CreateWorldHitEvent(CombatHitTargetTypes.WorldWall);
            CombatHitEventConfig groundOnly = CreateWorldHitEvent(CombatHitTargetTypes.WorldGround);

            Assert.IsTrue(CharacterTestCombatHitEventResolver.AllowsWorldTarget(
                staticOnly,
                CharacterTestCombatWorldTargetKind.Unknown));
            Assert.IsFalse(CharacterTestCombatHitEventResolver.AllowsWorldTarget(
                wallOnly,
                CharacterTestCombatWorldTargetKind.Unknown));
            Assert.IsTrue(CharacterTestCombatHitEventResolver.AllowsWorldTarget(
                wallOnly,
                CharacterTestCombatWorldTargetKind.Wall));
            Assert.IsFalse(CharacterTestCombatHitEventResolver.AllowsWorldTarget(
                wallOnly,
                CharacterTestCombatWorldTargetKind.Ground));
            Assert.IsTrue(CharacterTestCombatHitEventResolver.AllowsWorldTarget(
                groundOnly,
                CharacterTestCombatWorldTargetKind.Ground));
            Assert.IsFalse(CharacterTestCombatHitEventResolver.AllowsWorldTarget(
                groundOnly,
                CharacterTestCombatWorldTargetKind.Wall));
            Assert.IsFalse(CharacterTestCombatHitEventResolver.AllowsWorldTarget(
                groundOnly,
                CharacterTestCombatWorldTargetKind.Unknown));
        }

        [Test]
        public void HitEventResolver_ExplicitWorldKindRequiresExactBodyColliderAndLayer()
        {
            const int layer = ObstacleLayer;
            var bodyId = new CombatBodyId(62001);
            var colliderId = new CombatColliderId(62002);
            var targetEntityId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 21);
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(targetEntityId, bodyId, V(0, 0, 0)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                colliderId,
                layer,
                V(-1, -1, -1),
                V(1, 1, 1)));
            var header = new CombatQueryHeader(
                1,
                CombatQueryKind.Capsule,
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1),
                1,
                1,
                0,
                CombatPhysicsLayerMask.FromLayer(layer));
            var hit = new CombatQueryResult(
                header,
                targetEntityId,
                bodyId,
                colliderId,
                Fix64.Zero,
                FixVector3.Zero,
                V(-1, 0, 0));
            var frame = new RuntimeFrame(31);
            var classifications = new CharacterTestCombatWorldTargetClassificationSnapshot(1);
            classifications.Reset(frame, world.Revision);
            Assert.IsTrue(classifications.TryAdd(new CharacterTestCombatWorldTargetClassification(
                bodyId,
                colliderId,
                layer,
                CharacterTestCombatWorldTargetKind.Wall)));
            Assert.IsTrue(classifications.Seal(frame, world.Revision));

            Assert.AreEqual(
                CharacterTestCombatWorldQualification.QualifiedBlocker,
                CharacterTestCombatHitEventResolver.QualifyWorldTarget(
                CreateWorldHitEvent(CombatHitTargetTypes.WorldWall),
                hit,
                frame,
                world,
                classifications,
                out CharacterTestCombatWorldTargetKind resolvedKind));
            Assert.AreEqual(CharacterTestCombatWorldTargetKind.Wall, resolvedKind);
            Assert.AreEqual(
                CharacterTestCombatWorldQualification.Rejected,
                CharacterTestCombatHitEventResolver.QualifyWorldTarget(
                CreateWorldHitEvent(CombatHitTargetTypes.WorldGround),
                hit,
                frame,
                world,
                classifications,
                out _));

            var wrongLayerClassifications = new CharacterTestCombatWorldTargetClassificationSnapshot(1);
            wrongLayerClassifications.Reset(frame, world.Revision);
            Assert.IsTrue(wrongLayerClassifications.TryAdd(new CharacterTestCombatWorldTargetClassification(
                bodyId,
                colliderId,
                layer + 1,
                CharacterTestCombatWorldTargetKind.Wall)));
            Assert.IsTrue(wrongLayerClassifications.Seal(frame, world.Revision));
            Assert.AreEqual(
                CharacterTestCombatWorldQualification.Rejected,
                CharacterTestCombatHitEventResolver.QualifyWorldTarget(
                CreateWorldHitEvent(CombatHitTargetTypes.WorldWall),
                hit,
                frame,
                world,
                wrongLayerClassifications,
                out resolvedKind));
            Assert.AreEqual(CharacterTestCombatWorldTargetKind.Unknown, resolvedKind);
            Assert.AreEqual(
                CharacterTestCombatWorldQualification.Rejected,
                CharacterTestCombatHitEventResolver.QualifyWorldTarget(
                CreateWorldHitEvent(CombatHitTargetTypes.WorldStatic),
                hit,
                frame,
                world,
                wrongLayerClassifications,
                out resolvedKind));
            Assert.AreEqual(CharacterTestCombatWorldTargetKind.Unknown, resolvedKind);
        }

        [Test]
        public void AttackOpportunity_MultipleBlockersRemainStableWhenInsertedInReverseOrder()
        {
            Fixture forward = CreateFixture(
                currentHits: true,
                turnHits: true,
                addBlocker: true,
                blockerCount: 2,
                reverseBlockerOrder: false);
            Fixture reverse = CreateFixture(
                currentHits: true,
                turnHits: true,
                addBlocker: true,
                blockerCount: 2,
                reverseBlockerOrder: true);

            CharacterTestAttackOpportunityResult forwardResult = forward.Service.Evaluate(forward.Query);
            CharacterTestAttackOpportunityResult reverseResult = reverse.Service.Evaluate(reverse.Query);

            Assert.AreEqual(CharacterTestAttackOpportunityFailureReason.BlockedByEnvironment, forwardResult.FailureReason);
            Assert.AreEqual(forwardResult.FailureReason, reverseResult.FailureReason);
            Assert.AreEqual(forwardResult.FailureFlags, reverseResult.FailureFlags);
            Assert.IsFalse(forwardResult.RequiresTranslation);
            Assert.IsFalse(reverseResult.RequiresTranslation);
        }

        [Test]
        public void AttackOpportunity_VerticalEnvelopeMissIsPureSpatialMiss()
        {
            Fixture fixture = CreateFixture(
                currentHits: true,
                turnHits: true,
                verticalMiss: true);

            CharacterTestAttackOpportunityResult current = fixture.Service.Evaluate(
                fixture.Query,
                CharacterTestAttackEvaluationMode.CurrentPose);
            CharacterTestAttackOpportunityResult turned = fixture.Service.Evaluate(
                fixture.Query,
                CharacterTestAttackEvaluationMode.TurnInPlace,
                requiredYawDeltaMilliDegrees: 90000);
            Assert.AreEqual(
                CharacterTestAttackOpportunityStatus.Valid,
                current.Status,
                $"current flags={current.FailureFlags} expectedYaw={current.ExpectedSourceYawMilliDegrees} evaluatedYaw={current.EvaluatedSourceYawMilliDegrees}");
            Assert.AreEqual(
                CharacterTestAttackOpportunityStatus.Valid,
                turned.Status,
                $"turn flags={turned.FailureFlags} expectedYaw={turned.ExpectedSourceYawMilliDegrees} evaluatedYaw={turned.EvaluatedSourceYawMilliDegrees}");

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(
                fixture.Query,
                requiredYawDeltaMilliDegrees: 90000);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.IsTrue(result.HasCompatibleHurtbox);
            Assert.IsFalse(result.EnvelopeIntersectsTarget);
            Assert.IsFalse(result.TraceCanResolveTargetHit);
            Assert.IsTrue(result.RequiresTranslation);
            Assert.IsFalse(result.VerticalOverlap);
        }

        [Test]
        public void AttackOpportunity_MovingTargetAuthorityRevisionReturnsStale()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            fixture.Query.Target.SetTransform(new CharacterTestCharacterTransformComponent(30f, 0f, 0f, 0f));
            fixture.Query.TargetGeometry.Commit(
                fixture.Query.Snapshot.SimulationFrame,
                fixture.World.Revision,
                fixture.Query.Snapshot.TargetHurtboxRevision + 1);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(fixture.Query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.StaleSnapshot, result.Status);
            Assert.AreEqual(CharacterTestAttackOpportunityFailureReason.StaleSnapshot, result.FailureReason);
        }

        [Test]
        public void AttackOpportunity_TargetLiveTransformChangeDoesNotMoveFrozenHurtboxes()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            fixture.Query.Target.SetTransform(new CharacterTestCharacterTransformComponent(30f, 0f, 0f, 0f));

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(fixture.Query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.IsTrue(result.CanHitFromCurrentPose);
            Assert.AreEqual(1002, result.SelectedColliderId.Value);
            Assert.Less(result.TargetWitnessPoint.X.RawValue, F(10).RawValue);
        }

        [Test]
        public void AttackOpportunity_EarlierTickWinsOverLaterNearerHit()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            CharacterTestDirectHitTracePlanView plan = BuildEarlyFarLateNearPlan(
                fixture.World,
                fixture.Query.CurrentCompletedFrame,
                fixture.Query.Snapshot.SourceEntityId,
                fixture.Query.CurrentResolvedActionRevision);
            CharacterTestAttackOpportunitySamplePolicySnapshot policies = BuildPolicies(
                fixture.Query.Snapshot,
                sourceTeamId: 1,
                targetTeamId: 2,
                HitTargetStateFlags.Alive,
                CreateHitEvent(blockedPresentation: false),
                plan);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(
                currentPlan: plan,
                turnPlan: plan,
                samplePolicies: policies);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(2, result.EarliestCandidateHitTick);
            Assert.AreEqual(0, result.TraceSampleIndex);
            Assert.AreEqual(1, result.AcceptedHitCount);
        }

        [Test]
        public void AttackOpportunity_RealPreviewRootMotionPlanFindsFirstHittingFrame()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            CharacterTestDirectHitTracePlanView plan = BuildRootMotionPreviewPlan(
                fixture.World,
                fixture.Query.CurrentCompletedFrame,
                fixture.Query.Snapshot.SourceEntityId,
                fixture.Query.CurrentResolvedActionRevision);
            CharacterTestAttackOpportunitySamplePolicySnapshot policies = BuildPolicies(
                fixture.Query.Snapshot,
                1,
                2,
                HitTargetStateFlags.Alive,
                CreateHitEvent(blockedPresentation: false),
                plan);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(
                currentPlan: plan,
                turnPlan: plan,
                samplePolicies: policies);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(4, result.EarliestCandidateHitTick);
            Assert.AreEqual(2, result.TraceSampleIndex);
            Assert.IsTrue(plan.TryGetSample(
                result.TraceSampleIndex,
                out CharacterTestDirectHitTracePlanSample sampledEvidence));
            Assert.AreEqual(sampledEvidence.StableSampleId, result.StableSampleId);
            Assert.AreEqual(sampledEvidence.ActionId, result.ActionId);
            Assert.AreEqual(sampledEvidence.LocalFrame, result.EarliestCandidateHitTick);
        }

        [Test]
        public void AttackOpportunity_OncePerEventAcceptsSameTargetAcrossTicksOnlyOnce()
        {
            Fixture fixture = CreateFixture(
                currentHits: true,
                turnHits: true,
                includeDuplicateHitSample: true);
            CharacterTestAttackOpportunitySamplePolicySnapshot policies = BuildPolicies(
                fixture.Query.Snapshot,
                sourceTeamId: 1,
                targetTeamId: 2,
                HitTargetStateFlags.Alive,
                CreateHitEvent(
                    blockedPresentation: false,
                    maxAcceptedHits: 4,
                    hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent),
                fixture.Query.CurrentPosePlan,
                fixture.Query.TurnInPlacePlan);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(samplePolicies: policies);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(1, result.AcceptedHitCount);
            Assert.AreEqual(4, result.EarliestCandidateHitTick);
        }

        [Test]
        public void AttackOpportunity_AllowRepeatAcceptsSameEventTargetAcrossTicksUntilCap()
        {
            Fixture fixture = CreateFixture(
                currentHits: true,
                turnHits: true,
                includeDuplicateHitSample: true);
            CharacterTestAttackOpportunitySamplePolicySnapshot policies = BuildPolicies(
                fixture.Query.Snapshot,
                sourceTeamId: 1,
                targetTeamId: 2,
                HitTargetStateFlags.Alive,
                CreateHitEvent(
                    blockedPresentation: false,
                    maxAcceptedHits: 2,
                    hitSameTargetPolicy: CombatHitSameTargetPolicy.AllowRepeat),
                fixture.Query.CurrentPosePlan,
                fixture.Query.TurnInPlacePlan);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(samplePolicies: policies);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(2, result.AcceptedHitCount);
            Assert.AreEqual(4, result.EarliestCandidateHitTick);
        }

        [Test]
        public void AttackOpportunity_AllowRepeatHonorsMaxAcceptedHitsAcrossTicks()
        {
            Fixture fixture = CreateFixture(
                currentHits: true,
                turnHits: true,
                includeDuplicateHitSample: true);
            CharacterTestAttackOpportunitySamplePolicySnapshot policies = BuildPolicies(
                fixture.Query.Snapshot,
                sourceTeamId: 1,
                targetTeamId: 2,
                HitTargetStateFlags.Alive,
                CreateHitEvent(
                    blockedPresentation: false,
                    maxAcceptedHits: 1,
                    hitSameTargetPolicy: CombatHitSameTargetPolicy.AllowRepeat),
                fixture.Query.CurrentPosePlan,
                fixture.Query.TurnInPlacePlan);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(samplePolicies: policies);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(1, result.AcceptedHitCount);
            Assert.AreEqual(4, result.EarliestCandidateHitTick);
        }

        [Test]
        public void AttackOpportunity_FrozenFriendlyRelationIsTargetNotEligible()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            CharacterTestAttackOpportunitySamplePolicySnapshot policies = BuildPolicies(
                fixture.Query.Snapshot,
                sourceTeamId: 1,
                targetTeamId: 1,
                HitTargetStateFlags.Alive,
                CreateHitEvent(blockedPresentation: false),
                fixture.Query.CurrentPosePlan,
                fixture.Query.TurnInPlacePlan);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(samplePolicies: policies);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(CharacterTestAttackOpportunityFailureReason.TargetNotEligible, result.FailureReason);
            Assert.IsFalse(result.RequiresTranslation);
            Assert.IsFalse(result.QualificationAccepted);
        }

        [Test]
        public void AttackOpportunity_FrozenInvincibleStateIsTargetStateRejected()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            CharacterTestAttackOpportunitySamplePolicySnapshot policies = BuildPolicies(
                fixture.Query.Snapshot,
                1,
                2,
                HitTargetStateFlags.Alive | HitTargetStateFlags.Invincible,
                CreateHitEvent(blockedPresentation: false),
                fixture.Query.CurrentPosePlan,
                fixture.Query.TurnInPlacePlan);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(samplePolicies: policies);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(CharacterTestAttackOpportunityFailureReason.TargetStateRejected, result.FailureReason);
            Assert.IsFalse(result.RequiresTranslation);
            Assert.IsFalse(result.QualificationAccepted);
        }

        [Test]
        public void AttackOpportunity_MaskIncompatibleHurtboxFailsWithoutTranslation()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            CharacterTestDirectHitTracePlanView plan = BuildPlan(
                fixture.World,
                fixture.Query.CurrentCompletedFrame,
                fixture.Query.Snapshot.SourceEntityId,
                fixture.Query.CurrentResolvedActionRevision,
                CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
                sourceYawMilliDegrees: 0,
                hit: true,
                useWorldMask: false,
                verticalMiss: false,
                includeDuplicateHitSample: false,
                forceIncompatibleMask: true);
            CharacterTestAttackOpportunitySamplePolicySnapshot policies = BuildPolicies(
                fixture.Query.Snapshot,
                1,
                2,
                HitTargetStateFlags.Alive,
                CreateHitEvent(blockedPresentation: false),
                plan);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(
                currentPlan: plan,
                turnPlan: plan,
                samplePolicies: policies);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(query);

            Assert.AreEqual(CharacterTestAttackOpportunityFailureReason.NoCompatibleHurtbox, result.FailureReason);
            Assert.IsFalse(result.HasCompatibleHurtbox);
            Assert.IsFalse(result.RequiresTranslation);
            Assert.IsFalse(result.MaskCompatible);
            Assert.IsTrue(result.QualificationAccepted);
        }

        [Test]
        public void AttackOpportunity_SamePointCollidersKeepExactEvidenceIdentity()
        {
            Fixture fixture = CreateFixture(
                currentHits: true,
                turnHits: true,
                addSamePointSecondCollider: true);

            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(fixture.Query);

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(new CombatBodyId(50002), result.SelectedBodyId);
            Assert.AreEqual(new CombatColliderId(1002), result.SelectedColliderId);
            Assert.AreEqual("tail", result.SelectedHurtboxId);
            Assert.AreEqual("rear", result.SelectedHitZoneId);
            Assert.AreEqual(1, result.AcceptedHitCount);
        }

        [Test]
        public void AttackOpportunity_MoreThanSixteenCandidatesWarmedAllocatesZeroBytes()
        {
            Fixture fixture = CreateFixture(currentHits: true, turnHits: true);
            CharacterTestDirectHitTracePlanView plan = BuildManyCandidatePlan(
                fixture.World,
                fixture.Query.CurrentCompletedFrame,
                fixture.Query.Snapshot.SourceEntityId,
                fixture.Query.CurrentResolvedActionRevision,
                sampleCount: 24);
            CharacterTestAttackOpportunitySamplePolicySnapshot policies = BuildPolicies(
                fixture.Query.Snapshot,
                1,
                2,
                HitTargetStateFlags.Alive,
                CreateHitEvent(
                    blockedPresentation: false,
                    maxAcceptedHits: 24,
                    hitSameTargetPolicy: CombatHitSameTargetPolicy.AllowRepeat),
                plan);
            CharacterTestAttackOpportunityQuery query = fixture.CreateQuery(
                currentPlan: plan,
                turnPlan: plan,
                samplePolicies: policies);
            _ = fixture.Service.Evaluate(query);
            _ = fixture.Service.Evaluate(query);

            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            CharacterTestAttackOpportunityResult result = fixture.Service.Evaluate(query);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, result.Status);
            Assert.AreEqual(24, result.AcceptedHitCount);
            Assert.AreEqual(0L, allocated);
        }

        private static Fixture CreateFixture(
            bool currentHits,
            bool turnHits,
            bool addBlocker = false,
            bool verticalMiss = false,
            bool includeDuplicateHitSample = false,
            int blockerCount = 1,
            bool reverseBlockerOrder = false,
            bool addSamePointSecondCollider = false,
            bool classifyBlockers = true,
            bool addFrontActor = false,
            string targetPartId = "tail",
            string targetHitZoneId = "rear")
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            CharacterTestCharacterEntity source = CreateEntity(1, 0f, 0f, 0f, 0f, teamId: 1);
            CharacterTestCharacterEntity target = CreateEntity(2, 3f, 0f, 0f, 0f, teamId: 2);
            CombatEntityId sourceCombatId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + source.Id.Value);
            CombatEntityId targetCombatId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value);
            CombatBodyId targetBodyId = new CombatBodyId(
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxBodyIdStart + target.Id.Value);
            world.UpsertBody(new CombatPhysicsBody(targetCombatId, targetBodyId, V(3, 0, 0)));
            AddTargetCollider(world, targetBodyId, new CombatColliderId(1002));
            if (addSamePointSecondCollider)
                AddTargetCollider(world, targetBodyId, new CombatColliderId(1003));
            if (addFrontActor)
            {
                CombatBodyId frontBodyId = new CombatBodyId(
                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxBodyIdStart + 3);
                world.UpsertBody(new CombatPhysicsBody(
                    new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 3),
                    frontBodyId,
                    V(1, 0, 0)));
                AddTargetCollider(world, frontBodyId, new CombatColliderId(1004));
            }
            AddBlockers(
                world,
                addBlocker,
                blockerCount,
                reverseBlockerOrder);

            RuntimeFrame frame = new RuntimeFrame(20);
            var worldTargets = new CharacterTestCombatWorldTargetClassificationSnapshot(blockerCount);
            worldTargets.Reset(frame, world.Revision);
            if (addBlocker && classifyBlockers)
                RegisterBlockerClassifications(worldTargets, blockerCount);
            Assert.IsTrue(worldTargets.Seal(frame, world.Revision));
            CharacterTestDamageGeometrySnapshot geometry = new CharacterTestDamageGeometrySnapshot(
                addSamePointSecondCollider ? 2 : 1);
            geometry.BeginWrite(target.Id.Value, target.GameplayEntityId, targetCombatId);
            AddGeometryCollider(geometry, "tail", 1002, targetPartId, targetHitZoneId);
            if (addSamePointSecondCollider)
                AddGeometryCollider(geometry, "head", 1003, "head", "front");
            geometry.SortCollidersByStableLogicalId();
            geometry.Commit(frame, world.Revision, hurtboxRevision: 9);

            Assert.IsTrue(CharacterTestAttackEvaluationSnapshot.TryCapture(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 1,
                sourceFacingRevision: 1,
                resolvedActionRevision: 17,
                out CharacterTestAttackEvaluationSnapshot snapshot));

            CharacterTestDirectHitTracePlanView currentPlan = BuildPlan(
                world,
                frame,
                sourceCombatId,
                resolvedActionRevision: 17,
                CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
                sourceYawMilliDegrees: 0,
                hit: currentHits,
                useWorldMask: addBlocker,
                verticalMiss,
                includeDuplicateHitSample,
                forceIncompatibleMask: false);
            CharacterTestDirectHitTracePlanView turnPlan = BuildPlan(
                world,
                frame,
                sourceCombatId,
                resolvedActionRevision: 17,
                CharacterTestDirectHitTracePlanPoseKind.TurnInPlace,
                sourceYawMilliDegrees: 90000,
                hit: turnHits,
                useWorldMask: addBlocker,
                verticalMiss,
                includeDuplicateHitSample,
                forceIncompatibleMask: false);
            CharacterTestAttackOpportunitySamplePolicySnapshot policies = BuildPolicies(
                snapshot,
                source.Team.TeamId,
                target.Team.TeamId,
                HitTargetStateFlags.Alive,
                CreateHitEvent(addBlocker),
                currentPlan,
                turnPlan);
            CharacterTestAttackOpportunityQuery query = new CharacterTestAttackOpportunityQuery(
                snapshot,
                geometry,
                world,
                source,
                target,
                currentPlan,
                hasCurrentPosePlan: true,
                turnPlan,
                hasTurnInPlacePlan: true,
                currentCompletedFrame: frame,
                currentCombatPhysicsRevision: world.Revision,
                currentSourcePoseRevision: 1,
                currentSourceFacingRevision: 1,
                currentTargetHurtboxRevision: geometry.HurtboxRevision,
                currentResolvedActionRevision: 17,
                worldTargets,
                policies,
                traceEvaluatorSupported: true);
            return new Fixture(world, query, new CharacterTestAttackOpportunityService());
        }

        private static void AddTargetCollider(
            CombatPhysicsWorld world,
            CombatBodyId bodyId,
            CombatColliderId colliderId)
        {
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                colliderId,
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer,
                V(-F(1) / F(2), -F(1), -F(1)),
                V(F(1) / F(2), F(1), F(1))));
        }

        private static void AddGeometryCollider(
            CharacterTestDamageGeometrySnapshot geometry,
            string logicalId,
            int colliderId,
            string partId,
            string hitZoneId)
        {
            geometry.AddCollider(
                ResolveLogicalColliderId(logicalId),
                logicalId,
                new CombatColliderId(colliderId),
                ResolvePartId(partId),
                partId,
                ResolveHitZoneId(hitZoneId),
                hitZoneId,
                priority: 1,
                layer: CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer,
                localShape: CombatPhysicsShape.Aabb(
                    V(F(5) / F(2), -F(1), -F(1)),
                    V(F(7) / F(2), F(1), F(1))),
                worldMin: V(F(5) / F(2), -F(1), -F(1)),
                worldMax: V(F(7) / F(2), F(1), F(1)));
        }

        private static int ResolveLogicalColliderId(string label)
        {
            switch (label)
            {
                case "head": return CharacterTestAuthoredContentIds.D400_Id71;
                case "tail": return CharacterTestAuthoredContentIds.D400_Id103;
                default: return CharacterTestAuthoredContentIds.D400_Id70;
            }
        }

        private static int ResolvePartId(string label)
        {
            return label == "tail"
                ? CharacterTestAuthoredContentIds.D400_Id103
                : CharacterTestAuthoredContentIds.D400_Id34;
        }

        private static int ResolveHitZoneId(string label)
        {
            return label == "rear"
                ? CharacterTestAuthoredContentIds.D400_Id103
                : CharacterTestAuthoredContentIds.D400_Id56;
        }

        private static void AddBlockers(
            CombatPhysicsWorld world,
            bool addBlocker,
            int blockerCount,
            bool reverseBlockerOrder)
        {
            if (!addBlocker)
                return;

            int count = blockerCount < 1 ? 1 : blockerCount;
            for (int blockerIndex = 0; blockerIndex < count; blockerIndex++)
            {
                int sourceIndex = reverseBlockerOrder ? count - blockerIndex - 1 : blockerIndex;
                int blockerNumber = sourceIndex + 1;
                var blockerBodyId = new CombatBodyId(60000 + blockerNumber);
                var blockerColliderId = new CombatColliderId(60000 + blockerNumber);
                world.UpsertBody(new CombatPhysicsBody(
                    new CombatEntityId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + blockerNumber),
                    blockerBodyId,
                    V(blockerNumber, 0, 0)));
                world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                    blockerBodyId,
                    blockerColliderId,
                    ObstacleLayer,
                    V(-F(1) / F(4), -F(1), -F(1)),
                    V(F(1) / F(4), F(1), F(1))));
            }
        }

        private static void RegisterBlockerClassifications(
            CharacterTestCombatWorldTargetClassificationSnapshot worldTargets,
            int blockerCount)
        {
            int count = blockerCount < 1 ? 1 : blockerCount;
            for (int blockerIndex = 0; blockerIndex < count; blockerIndex++)
            {
                int blockerNumber = blockerIndex + 1;
                Assert.IsTrue(worldTargets.TryAdd(
                    new CharacterTestCombatWorldTargetClassification(
                        new CombatBodyId(60000 + blockerNumber),
                        new CombatColliderId(60000 + blockerNumber),
                        ObstacleLayer,
                        CharacterTestCombatWorldTargetKind.Wall)));
            }
        }

        private static CombatHitEventConfig CreateHitEvent(
            bool blockedPresentation,
            int maxAcceptedHits = 1,
            CombatHitSameTargetPolicy hitSameTargetPolicy = CombatHitSameTargetPolicy.OncePerEvent)
        {
            return new CombatHitEventConfig(
                blockedPresentation
                    ? CombatHitTargetTypes.HostileCharacter | CombatHitTargetTypes.WorldWall
                    : CombatHitTargetTypes.HostileCharacter,
                blockedPresentation
                    ? CombatEnvironmentHitPolicy.BlockedPresentation
                    : CombatEnvironmentHitPolicy.None,
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0042,
                maxAcceptedHits,
                hitSameTargetPolicy);
        }

        private static CombatHitEventConfig CreateWorldHitEvent(CombatHitTargetTypes targetTypes)
        {
            return new CombatHitEventConfig(
                targetTypes,
                CombatEnvironmentHitPolicy.BlockedPresentation,
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0047,
                maxAcceptedHits: 1,
                hitSameTargetPolicy: CombatHitSameTargetPolicy.OncePerEvent);
        }

        private static CharacterActionDamagePayload CreateValidDamagePayload()
        {
            return new CharacterActionDamagePayload(
                damageTypeId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D409_Id2,
                formulaId: DamageFormulaIds.Flat,
                baseDamage: 1);
        }

        private static CharacterTestAttackOpportunitySamplePolicySnapshot BuildPolicies(
            CharacterTestAttackEvaluationSnapshot snapshot,
            int sourceTeamId,
            int targetTeamId,
            HitTargetStateFlags targetState,
            CombatHitEventConfig hitEvent,
            params CharacterTestDirectHitTracePlanView[] plans)
        {
            return BuildPoliciesWithDamage(
                snapshot,
                sourceTeamId,
                targetTeamId,
                targetState,
                hitEvent,
                CreateValidDamagePayload(),
                plans);
        }

        private static CharacterTestAttackOpportunitySamplePolicySnapshot BuildPoliciesWithDamage(
            CharacterTestAttackEvaluationSnapshot snapshot,
            int sourceTeamId,
            int targetTeamId,
            HitTargetStateFlags targetState,
            CombatHitEventConfig hitEvent,
            CharacterActionDamagePayload damagePayload,
            params CharacterTestDirectHitTracePlanView[] plans)
        {
            var policies = new CharacterTestAttackOpportunitySamplePolicySnapshot(32);
            for (int planIndex = 0; planIndex < plans.Length; planIndex++)
            {
                CharacterTestDirectHitTracePlanView plan = plans[planIndex];
                for (int sampleIndex = 0; sampleIndex < plan.Count; sampleIndex++)
                {
                    Assert.IsTrue(plan.TryGetSample(sampleIndex, out CharacterTestDirectHitTracePlanSample sample));
                    policies.TryAdd(new CharacterTestAttackOpportunitySamplePolicy(
                        CharacterTestAttackOpportunitySampleIdentity.From(sample),
                        snapshot.SourceEntityId,
                        snapshot.TargetEntityId,
                        sourceTeamId,
                        targetTeamId,
                        targetState,
                        hitEvent,
                        damagePayload));
                }
            }
            policies.Seal();
            return policies;
        }

        private static CharacterTestDirectHitTracePlanView BuildPlan(
            CombatPhysicsWorld world,
            RuntimeFrame frame,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision,
            CharacterTestDirectHitTracePlanPoseKind poseKind,
            int sourceYawMilliDegrees,
            bool hit,
            bool useWorldMask,
            bool verticalMiss,
            bool includeDuplicateHitSample,
            bool forceIncompatibleMask,
            FixVector3? sourcePosition = null,
            int sourcePoseRevision = 1,
            int sourceFacingRevision = 1)
        {
            var buffer = new CharacterTestDirectHitTracePlanBuffer();
            buffer.Reset(frame, world.Revision);
            int planIndex = buffer.GetOrBeginPlan(
                sourceEntityId,
                resolvedActionRevision,
                CreatePlanPoseIdentity(
                    poseKind,
                    sourceYawMilliDegrees,
                    sourcePosition,
                    sourcePoseRevision,
                    sourceFacingRevision));
            List<CharacterTestDirectHitTracePlanSample> samples = buffer.GetPlanSamplesBuffer(planIndex);
            var bounds = default(CharacterTestDirectHitTraceBounds);
            FixVector3 pointA = verticalMiss
                ? V(0, 4, 0)
                : hit ? V(0, 0, 0) : V(0, 0, 4);
            FixVector3 pointB = verticalMiss
                ? V(4, 4, 0)
                : hit ? V(4, 0, 0) : V(0, 0, 5);
            CombatPhysicsLayerMask mask = forceIncompatibleMask
                ? CombatPhysicsLayerMask.FromLayer(ObstacleLayer)
                : CombatPhysicsLayerMask.FromLayer(
                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer);
            if (useWorldMask)
            {
                mask = new CombatPhysicsLayerMask(
                    mask.Value | CombatPhysicsLayerMask.FromLayer(ObstacleLayer).Value);
            }

            int sampleOrder = 0;
            AppendSample(
                sourceEntityId,
                mask,
                hitWindowId: resolvedActionRevision,
                localFrame: 4,
                sourceOrder: 0,
                queryOffset: 0,
                pointA,
                pointB,
                actionId: 7,
                traceId: 71,
                ref sampleOrder,
                samples,
                ref bounds);
            if (includeDuplicateHitSample && hit && !verticalMiss)
            {
                AppendSample(
                    sourceEntityId,
                    mask,
                    hitWindowId: resolvedActionRevision,
                    localFrame: 5,
                    sourceOrder: 1,
                    queryOffset: 1,
                    V(0, 0, 0),
                    V(4, 0, 0),
                    actionId: 7,
                    traceId: 71,
                    ref sampleOrder,
                    samples,
                    ref bounds);
            }
            buffer.UpdatePlan(planIndex, samples.Count, bounds);
            buffer.Seal(frame, world.Revision);
            CharacterTestDirectHitTracePlanCollectionView collection = buffer.CreateView();
            Assert.IsTrue(collection.TryGetPlan(0, out CharacterTestDirectHitTracePlanView plan));
            return plan;
        }

        private static CharacterTestDirectHitTracePlanView BuildEarlyFarLateNearPlan(
            CombatPhysicsWorld world,
            RuntimeFrame frame,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision)
        {
            var buffer = new CharacterTestDirectHitTracePlanBuffer();
            buffer.Reset(frame, world.Revision);
            int planIndex = buffer.GetOrBeginPlan(
                sourceEntityId,
                resolvedActionRevision,
                CreatePlanPoseIdentity());
            List<CharacterTestDirectHitTracePlanSample> samples = buffer.GetPlanSamplesBuffer(planIndex);
            var bounds = default(CharacterTestDirectHitTraceBounds);
            CombatPhysicsLayerMask mask = CombatPhysicsLayerMask.FromLayer(
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer);
            int sampleOrder = 0;
            AppendSample(
                sourceEntityId,
                mask,
                resolvedActionRevision,
                localFrame: 2,
                sourceOrder: 0,
                queryOffset: 0,
                V(F(0), F(0), F(0)),
                V(F(9) / F(4), F(0), F(0)),
                actionId: 7,
                traceId: 71,
                ref sampleOrder,
                samples,
                ref bounds);
            AppendSample(
                sourceEntityId,
                mask,
                resolvedActionRevision,
                localFrame: 4,
                sourceOrder: 1,
                queryOffset: 1,
                V(3, 0, 0),
                V(3, 0, 0),
                actionId: 7,
                traceId: 71,
                ref sampleOrder,
                samples,
                ref bounds);
            return SealPlan(buffer, planIndex, samples, bounds, frame, world.Revision);
        }

        private static CharacterTestDirectHitTracePlanView BuildHeterogeneousActionPlan(
            CombatPhysicsWorld world,
            RuntimeFrame frame,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision)
        {
            var buffer = new CharacterTestDirectHitTracePlanBuffer();
            buffer.Reset(frame, world.Revision);
            int planIndex = buffer.GetOrBeginPlan(
                sourceEntityId,
                resolvedActionRevision,
                CreatePlanPoseIdentity());
            List<CharacterTestDirectHitTracePlanSample> samples = buffer.GetPlanSamplesBuffer(planIndex);
            var bounds = default(CharacterTestDirectHitTraceBounds);
            CombatPhysicsLayerMask mask = CombatPhysicsLayerMask.FromLayer(
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer);
            int sampleOrder = 0;
            AppendSample(
                sourceEntityId,
                mask,
                resolvedActionRevision,
                localFrame: 4,
                sourceOrder: 0,
                queryOffset: 0,
                V(0, 0, 0),
                V(4, 0, 0),
                actionId: 7,
                traceId: 71,
                ref sampleOrder,
                samples,
                ref bounds);
            AppendSample(
                sourceEntityId,
                mask,
                resolvedActionRevision,
                localFrame: 5,
                sourceOrder: 1,
                queryOffset: 1,
                V(0, 0, 0),
                V(4, 0, 0),
                actionId: 8,
                traceId: 72,
                ref sampleOrder,
                samples,
                ref bounds);
            return SealPlan(buffer, planIndex, samples, bounds, frame, world.Revision);
        }

        private static CharacterTestDirectHitTracePlanView BuildRootMotionPreviewPlan(
            CombatPhysicsWorld world,
            RuntimeFrame frame,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision)
        {
            var poses = new List<CharacterTestDirectHitTraceSourcePoseOffset>
            {
                new CharacterTestDirectHitTraceSourcePoseOffset(2, V(0, 0, 0), 0),
                new CharacterTestDirectHitTraceSourcePoseOffset(3, V(1, 0, 0), 0),
                new CharacterTestDirectHitTraceSourcePoseOffset(4, V(2, 0, 0), 0)
            };
            CombatPhysicsLayerMask mask = CombatPhysicsLayerMask.FromLayer(
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer);
            var capsules = new List<CharacterTestDirectHitTracePreviewCapsuleFrame>
            {
                CreatePreviewCapsule(2, mask),
                CreatePreviewCapsule(3, mask),
                CreatePreviewCapsule(4, mask)
            };
            var output = new CharacterTestDirectHitTracePlanBuffer();
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(
                frame,
                world.Revision,
                sourceEntityId,
                resolvedActionRevision,
                CreatePlanPoseIdentity(),
                poses,
                new List<CharacterTestDirectHitTracePreviewWeaponFrame>(),
                capsules,
                useTipSweepCapsules: false,
                maxTipDistancePerSubstep: F(1),
                maxSubsteps: 1,
                new List<CombatCapsuleQuery>(),
                output,
                out CharacterTestDirectHitTracePlanView plan));
            return plan;
        }

        private static CharacterTestDirectHitTracePreviewCapsuleFrame CreatePreviewCapsule(
            int localFrame,
            CombatPhysicsLayerMask mask)
        {
            return new CharacterTestDirectHitTracePreviewCapsuleFrame(
                CharacterTestDirectHitTraceSampleKind.ManualCapsule,
                localFrame,
                sourceOrder: 0,
                queryOffset: 0,
                actionId: 7,
                traceId: 71,
                hitWindowId: 17,
                mask,
                V(0, 0, 0),
                V(1, 0, 0),
                F(1) / F(4));
        }

        private static CharacterTestDirectHitTracePlanView BuildManyCandidatePlan(
            CombatPhysicsWorld world,
            RuntimeFrame frame,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision,
            int sampleCount)
        {
            var buffer = new CharacterTestDirectHitTracePlanBuffer();
            buffer.Reset(frame, world.Revision);
            int planIndex = buffer.GetOrBeginPlan(
                sourceEntityId,
                resolvedActionRevision,
                CreatePlanPoseIdentity());
            List<CharacterTestDirectHitTracePlanSample> samples = buffer.GetPlanSamplesBuffer(planIndex);
            var bounds = default(CharacterTestDirectHitTraceBounds);
            CombatPhysicsLayerMask mask = CombatPhysicsLayerMask.FromLayer(
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer);
            int sampleOrder = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                AppendSample(
                    sourceEntityId,
                    mask,
                    hitWindowId: resolvedActionRevision,
                    localFrame: 4,
                    sourceOrder: i,
                    queryOffset: i,
                    V(0, 0, 0),
                    V(4, 0, 0),
                    actionId: 7,
                    traceId: 71,
                    ref sampleOrder,
                    samples,
                    ref bounds);
            }
            return SealPlan(buffer, planIndex, samples, bounds, frame, world.Revision);
        }

        private static CharacterTestDirectHitTracePlanView BuildEmptyPlan(
            CombatPhysicsWorld world,
            RuntimeFrame frame,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision)
        {
            var buffer = new CharacterTestDirectHitTracePlanBuffer();
            buffer.Reset(frame, world.Revision);
            int planIndex = buffer.GetOrBeginPlan(
                sourceEntityId,
                resolvedActionRevision,
                CreatePlanPoseIdentity());
            buffer.UpdatePlan(planIndex, 0, default(CharacterTestDirectHitTraceBounds));
            buffer.Seal(frame, world.Revision);
            CharacterTestDirectHitTracePlanCollectionView collection = buffer.CreateView();
            Assert.IsTrue(collection.TryGetPlan(0, out CharacterTestDirectHitTracePlanView plan));
            return plan;
        }

        private static CharacterTestDirectHitTracePlanView SealPlan(
            CharacterTestDirectHitTracePlanBuffer buffer,
            int planIndex,
            List<CharacterTestDirectHitTracePlanSample> samples,
            CharacterTestDirectHitTraceBounds bounds,
            RuntimeFrame frame,
            int worldRevision)
        {
            buffer.UpdatePlan(planIndex, samples.Count, bounds);
            buffer.Seal(frame, worldRevision);
            CharacterTestDirectHitTracePlanCollectionView collection = buffer.CreateView();
            Assert.IsTrue(collection.TryGetPlan(0, out CharacterTestDirectHitTracePlanView plan));
            return plan;
        }

        private static void AppendSample(
            CombatEntityId sourceEntityId,
            CombatPhysicsLayerMask mask,
            int hitWindowId,
            int localFrame,
            int sourceOrder,
            int queryOffset,
            FixVector3 pointA,
            FixVector3 pointB,
            int actionId,
            int traceId,
            ref int sampleOrder,
            List<CharacterTestDirectHitTracePlanSample> samples,
            ref CharacterTestDirectHitTraceBounds bounds)
        {
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryAppendHitVolumeSample(
                CharacterTestDirectHitTraceSampleKind.ManualCapsule,
                sourceEntityId,
                actionId,
                traceId,
                hitWindowId,
                localFrame,
                sourceOrder,
                queryOffset,
                mask,
                pointA,
                pointB,
                radius: F(1) / F(4),
                ref sampleOrder,
                samples,
                ref bounds,
                out _));
        }

        private const int ObstacleLayer = 11;

        private static CharacterTestCharacterEntity CreateEntity(
            int id,
            float x,
            float y,
            float z,
            float yawDegrees,
            int teamId)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(id),
                new CharacterTestCharacterIdentityComponent(
                    characterId: 1,
                    displayName: "CD-M1 Test Character",
                    prefabKey: default(ResourceKey),
                    animationBinding: default(CharacterRuntimeAnimationBinding)),
                new CharacterTestCharacterTeamComponent(teamId, teamId),
                new CharacterTestCharacterTransformComponent(x, y, z, yawDegrees),
                new CharacterTestCharacterEquipmentComponent(useDefaultLoadout: false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                new GameplayEntityId(id, 1),
                CharacterTestCharacterPhysicsComponent.Default);
        }

        private static int ResolveActualDirectHitCandidates(
            CharacterActionDamagePayload damage,
            out CharacterTestHitZoneResolution hitZone)
        {
            var definition = new CharacterTestCharacterDefinition(
                characterId: 101,
                displayName: "M1 Damage Eligibility",
                prefabKey: default(ResourceKey),
                defaultTeamId: 1,
                animationBinding: default);
            var runtimeWorld = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity source = runtimeWorld.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity target = runtimeWorld.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 3f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            var lookup = new CharacterTestCombatEntityLookup();
            lookup.Configure(runtimeWorld);

            CombatEntityId sourceCombatId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + source.Id.Value);
            CombatEntityId targetCombatId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value);
            CombatQueryHeader query = new CombatQueryHeader(
                queryId: 1,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: sourceCombatId,
                traceId: 71,
                actionId: 7,
                sourceOrder: 0,
                layerMask: CombatPhysicsLayerMask.FromLayer(
                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer));
            CombatQueryResult physicsHit = new CombatQueryResult(
                query,
                targetCombatId,
                new CombatBodyId(
                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxBodyIdStart
                    + target.Id.Value),
                new CombatColliderId(
                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart),
                Fix64.Zero,
                V(3, 0, 0),
                V(0, 1, 0));
            var candidates = new List<HitCandidate>
            {
                new HitCandidate(
                    sourceCombatId,
                    targetCombatId,
                    actionId: 7,
                    actionInstanceId: 17,
                    traceId: 71,
                    frame: new CombatFrame(20),
                    physicsHit,
                    damage: 0,
                    staggerFrames: 0,
                    knockback: FixVector3.Zero,
                    targetState: HitTargetStateFlags.Alive)
            };
            CombatHitEventConfig hitEvent = CreateHitEvent(blockedPresentation: false);
            var trace = new CharacterTestActiveHitTrace(
                source.Id.Value,
                actionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("m1.damage-eligibility"),
                sourceInstanceId: 1L,
                sourceActionInstanceId: 17,
                hitWindowId: 17,
                equipmentSlotId: 0,
                traceProfileId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0038,
                damage,
                hitEvent,
                hitVolume: default(CharacterActionHitVolumeConfig),
                stableEventId: hitEvent.StableEventId,
                startFrame: 0);
            var payloads = new Dictionary<CharacterTestTracePayloadKey, CharacterTestTracePayload>
            {
                [new CharacterTestTracePayloadKey(17, 71)] = new CharacterTestTracePayload(
                    trace,
                    default(CharacterTestWeaponTraceBinding),
                    string.Empty,
                    localFrame: 4)
            };
            var profiledCandidates = new List<HitCandidate>();
            var damagePayloads = new Dictionary<CharacterTestResolvedHitKey, DamageProfileResolveResult>();
            var hitZones = new Dictionary<CharacterTestResolvedHitKey, CharacterTestHitZoneResolution>();
            var resolver = new CharacterTestCombatDamageResolver(reportError: null);
            resolver.ResolveCandidates(
                candidates,
                payloads,
                lookup,
                profiledCandidates,
                damagePayloads,
                hitZones,
                null,
                null,
                new CombatPhysicsWorld(),
                new RuntimeFrame(20),
                worldTargetClassifications: null);

            hitZone = CharacterTestHitZoneResolution.Default;
            if (profiledCandidates.Count > 0)
            {
                if (hitZones.TryGetValue(
                        new CharacterTestResolvedHitKey(profiledCandidates[0]),
                        out CharacterTestHitZoneResolution resolvedHitZone))
                {
                    hitZone = resolvedHitZone;
                }
            }
            return profiledCandidates.Count;
        }

        private static DirectHitPolicyFixture CreateDirectHitPolicyFixture(
            CombatHitEventConfig hitEvent,
            bool includeThirdCollider)
        {
            CombatEntityId attackerId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 1);
            CombatEntityId targetId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 2);
            CombatPhysicsLayerMask layerMask = CombatPhysicsLayerMask.FromLayer(
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer);
            CombatQueryHeader firstQuery = new CombatQueryHeader(
                queryId: 1,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: attackerId,
                traceId: 71,
                actionId: 7,
                sourceOrder: 0,
                layerMask: layerMask);
            CombatQueryHeader secondQuery = new CombatQueryHeader(
                queryId: 2,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: attackerId,
                traceId: 71,
                actionId: 7,
                sourceOrder: 0,
                layerMask: layerMask);
            CombatQueryResult firstHit = new CombatQueryResult(
                firstQuery,
                targetId,
                new CombatBodyId(50002),
                new CombatColliderId(1002),
                Fix64.Zero,
                V(3, 0, 0),
                V(0, 1, 0));
            CombatQueryResult secondHit = new CombatQueryResult(
                secondQuery,
                targetId,
                new CombatBodyId(50002),
                new CombatColliderId(1002),
                Fix64.FromInt(1),
                V(3, 0, 0),
                V(0, 1, 0));
            var candidates = new List<HitCandidate>(includeThirdCollider ? 3 : 2)
            {
                new HitCandidate(
                    attackerId,
                    targetId,
                    actionId: 7,
                    actionInstanceId: 42,
                    traceId: 71,
                    frame: new CombatFrame(4),
                    physicsHit: firstHit,
                    damage: 0,
                    staggerFrames: 0,
                    knockback: FixVector3.Zero,
                    targetState: HitTargetStateFlags.Alive),
                new HitCandidate(
                    attackerId,
                    targetId,
                    actionId: 7,
                    actionInstanceId: 42,
                    traceId: 71,
                    frame: new CombatFrame(5),
                    physicsHit: secondHit,
                    damage: 0,
                    staggerFrames: 0,
                    knockback: FixVector3.Zero,
                    targetState: HitTargetStateFlags.Alive)
            };
            if (includeThirdCollider)
            {
                candidates.Add(new HitCandidate(
                    attackerId,
                    targetId,
                    actionId: 7,
                    actionInstanceId: 42,
                    traceId: 71,
                    frame: new CombatFrame(6),
                    physicsHit: new CombatQueryResult(
                        secondQuery,
                        targetId,
                        new CombatBodyId(50002),
                        new CombatColliderId(1003),
                        Fix64.FromInt(2),
                        V(3, 0, 0),
                        V(0, 1, 0)),
                    damage: 0,
                    staggerFrames: 0,
                    knockback: FixVector3.Zero,
                    targetState: HitTargetStateFlags.Alive));
            }

            var trace = new CharacterTestActiveHitTrace(
                entityId: 1,
                actionId: new CharacterActionId(1201610),
                sourceInstanceId: 1,
                sourceActionInstanceId: 42,
                hitWindowId: 42,
                equipmentSlotId: 0,
                traceProfileId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0041,
                damagePayload: default(CharacterActionDamagePayload),
                hitEvent: hitEvent,
                hitVolume: default(CharacterActionHitVolumeConfig),
                stableEventId: hitEvent.StableEventId,
                startFrame: 0);
            var payloads = new Dictionary<CharacterTestTracePayloadKey, CharacterTestTracePayload>
            {
                [new CharacterTestTracePayloadKey(42, 71)] =
                    new CharacterTestTracePayload(
                        trace,
                        default(CharacterTestWeaponTraceBinding),
                        string.Empty,
                        localFrame: 0)
            };
            var policyEventIds = new Dictionary<CombatQueryHeader, int>
            {
                [firstQuery] = 701,
                [secondQuery] = 701
            };
            return new DirectHitPolicyFixture(candidates, payloads, policyEventIds);
        }

        private sealed class DirectHitPolicyFixture
        {
            internal DirectHitPolicyFixture(
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

        private sealed class Fixture
        {
            internal Fixture(
                CombatPhysicsWorld world,
                CharacterTestAttackOpportunityQuery query,
                CharacterTestAttackOpportunityService service)
            {
                World = world;
                Query = query;
                Service = service;
            }

            internal CombatPhysicsWorld World { get; }
            internal CharacterTestAttackOpportunityQuery Query { get; }
            internal CharacterTestAttackOpportunityService Service { get; }

            internal CharacterTestAttackOpportunityQuery CreateQuery(
                CharacterTestDirectHitTracePlanView? currentPlan = null,
                CharacterTestDirectHitTracePlanView? turnPlan = null,
                CharacterTestAttackOpportunitySamplePolicySnapshot samplePolicies = null,
                RuntimeFrame? currentCompletedFrame = null,
                int? currentCombatPhysicsRevision = null,
                int? currentSourcePoseRevision = null,
                int? currentSourceFacingRevision = null,
                int? currentTargetHurtboxRevision = null,
                int? currentResolvedActionRevision = null,
                CharacterTestCombatWorldTargetClassificationSnapshot worldTargets = null)
            {
                return new CharacterTestAttackOpportunityQuery(
                    Query.Snapshot,
                    Query.TargetGeometry,
                    Query.PhysicsWorld,
                    Query.Source,
                    Query.Target,
                    currentPlan ?? Query.CurrentPosePlan,
                    Query.HasCurrentPosePlan,
                    turnPlan ?? Query.TurnInPlacePlan,
                    Query.HasTurnInPlacePlan,
                    currentCompletedFrame ?? Query.CurrentCompletedFrame,
                    currentCombatPhysicsRevision ?? Query.CurrentCombatPhysicsRevision,
                    currentSourcePoseRevision ?? Query.CurrentSourcePoseRevision,
                    currentSourceFacingRevision ?? Query.CurrentSourceFacingRevision,
                    currentTargetHurtboxRevision ?? Query.CurrentTargetHurtboxRevision,
                    currentResolvedActionRevision ?? Query.CurrentResolvedActionRevision,
                    worldTargets ?? Query.WorldTargets,
                    samplePolicies ?? Query.SamplePolicies,
                    Query.TraceEvaluatorSupported);
            }
        }

        private static FixVector3 V(int x, int y, int z)
        {
            return new FixVector3(F(x), F(y), F(z));
        }

        private static CharacterTestDirectHitTracePlanPoseIdentity CreatePlanPoseIdentity(
            CharacterTestDirectHitTracePlanPoseKind kind = CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
            int sourceYawMilliDegrees = 0,
            FixVector3? sourcePosition = null,
            int sourcePoseRevision = 1,
            int sourceFacingRevision = 1)
        {
            return new CharacterTestDirectHitTracePlanPoseIdentity(
                kind,
                sourcePosition ?? V(0, 0, 0),
                sourceYawMilliDegrees,
                sourcePoseRevision,
                sourceFacingRevision);
        }

        private static FixVector3 V(Fix64 x, Fix64 y, Fix64 z)
        {
            return new FixVector3(x, y, z);
        }

        private static Fix64 F(int value)
        {
            return Fix64.FromInt(value);
        }
    }
}

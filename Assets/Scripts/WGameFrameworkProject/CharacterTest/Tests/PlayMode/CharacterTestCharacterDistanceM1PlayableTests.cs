using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    /// <summary>
    /// CD-M1 PlayMode evidence using only deterministic CharacterTest fixtures.
    /// No imported model or presentation prefab is required: these tests drive
    /// the real GameSlice/Planner command path and formal Hurtbox authority.
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestCharacterDistanceM1PlayableTests
    {
        private const double TickSeconds = 1d / 60d;

        [UnityTest]
        public IEnumerator RuntimePlanner_CurrentPose_SubmitsPrimaryAttack()
        {
            yield return null;

            using (GameSlice slice = CreateSlice())
            {
                Spawn(slice, teamId: 1, x: 0f, z: 0.95f,
                    CharacterTestCharacterControlOwner.LocalPlayer);
                CharacterTestCharacterEntity ai = Spawn(
                    slice,
                    teamId: 2,
                    x: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled);

                slice.Tick(TickSeconds);

                RuntimeCommand command = FindPlannerControl(slice, ai.Id.Value);
                CharacterTestControlIntent intent =
                    CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
                Assert.IsTrue(intent.AttackPrimaryPressed);
                Assert.IsFalse(intent.TurnInPlace);
                Assert.AreEqual(0, intent.WorldMoveXMilli);
                Assert.AreEqual(0, intent.WorldMoveYMilli);

                Assert.AreEqual(
                    CharacterTestCharacterActionConfigSource.LightAttackActionId,
                    ai.Action.ActiveActionId);
            }
        }

        [UnityTest]
        public IEnumerator RuntimePlanner_UnavailableAuthority_FailsClosedWithoutPrimaryAttack()
        {
            yield return null;

            using (GameSlice slice = CreateSlice())
            {
                QueueSpawn(slice, teamId: 1, x: 0f, z: 0.95f,
                    CharacterTestCharacterControlOwner.LocalPlayer);
                QueueSpawn(slice, teamId: 2, x: 0f, z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled);
                slice.Tick(TickSeconds);

                CharacterTestCharacterEntity target = FindEntity(
                    slice,
                    teamId: 1,
                    CharacterTestCharacterControlOwner.LocalPlayer);
                CharacterTestCharacterEntity ai = FindEntity(
                    slice,
                    teamId: 2,
                    CharacterTestCharacterControlOwner.AiControlled);
                Assert.IsTrue(
                    slice.CharacterRuntime.TryGetDamageGeometrySnapshot(
                        target.Id.Value,
                        out CharacterTestDamageGeometrySnapshot geometry),
                    "The real target Hurtbox authority must exist before invalidation.");
                Assert.IsTrue(geometry.IsValid);

                // Withdraw the completed-frame authority before the next
                // PreSimulation. This exercises the real Planner/adapter path,
                // rather than injecting an opportunity result.
                geometry.Invalidate();
                slice.Tick(TickSeconds);

                if (TryFindPlannerControl(
                    slice,
                    ai.Id.Value,
                    out RuntimeCommand command))
                {
                    CharacterTestControlIntent intent =
                        CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
                    Assert.IsFalse(
                        intent.AttackPrimaryPressed,
                        "Unavailable/stale target authority must never submit PrimaryAttack.");
                }

                CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter =
                    GetAttackAdapter(slice);
                Assert.IsTrue(
                    adapter.TryGetEvidence(
                        ai.Id.Value,
                        out CharacterTestAttackOpportunityDecisionEvidence evidence),
                    "Planner must retain per-source fail-closed evidence.");
                Assert.AreEqual(CharacterTestAttackOpportunityStatus.Unavailable, evidence.Status);
                Assert.AreEqual(
                    CharacterTestAttackOpportunityFailureReason.SnapshotUnavailable,
                    evidence.FailureReason);
                Assert.IsFalse(adapter.CanSubmitPrimaryAttack(ai, slice.CurrentFrame));
                Assert.AreNotEqual(
                    CharacterTestCharacterActionConfigSource.LightAttackActionId,
                    ai.Action.ActiveActionId,
                    "Unavailable authority must not start the light attack.");
            }
        }

        [UnityTest]
        public IEnumerator RuntimePlanner_CompoundMultiHurtboxTarget_NearLeaf_IsCurrentPoseAttack()
        {
            yield return null;

            using (GameSlice slice = CreateSlice())
            {
                QueueSpawn(slice, teamId: 1, x: 0.95f, z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer);
                QueueSpawn(slice, teamId: 2, x: 0f, z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled);
                slice.Tick(TickSeconds);

                CharacterTestCharacterEntity target = FindEntity(
                    slice,
                    teamId: 1,
                    CharacterTestCharacterControlOwner.LocalPlayer);
                CharacterTestCharacterEntity ai = FindEntity(
                    slice,
                    teamId: 2,
                    CharacterTestCharacterControlOwner.AiControlled);
                // Face +X toward the stable near-side compound Hurtbox leaves.
                // This is M1 PlayMode coverage for consuming the formal
                // multi-Hurtbox authority; it does not claim root-far coverage
                // because x=.95 is below the legacy 1.6 root range.
                ai.SetTransform(new CharacterTestCharacterTransformComponent(0f, 1f, 0f, 90f));

                Assert.IsTrue(
                    slice.CharacterRuntime.TryGetDamageGeometrySnapshot(
                        target.Id.Value,
                        out CharacterTestDamageGeometrySnapshot geometry),
                    "The compound target must expose authoritative multi-Hurtbox geometry.");
                Assert.GreaterOrEqual(
                    geometry.ColliderCount,
                    10,
                    "This fixture must exercise the generated multi-Hurtbox target.");
                Assert.IsTrue(
                    geometry.WorldMin.X < Fix64.FromRatio(4, 5),
                    "A stable near-side Hurtbox leaf must remain in the real preview envelope. bounds="
                    + geometry.WorldMin + ".." + geometry.WorldMax
                    + " colliders=" + geometry.ColliderCount);

                slice.Tick(TickSeconds);

                RuntimeCommand command = FindPlannerControl(slice, ai.Id.Value);
                CharacterTestControlIntent intent =
                    CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
                Assert.IsTrue(intent.AttackPrimaryPressed);
                Assert.IsFalse(intent.TurnInPlace);
                Assert.AreEqual(0, intent.WorldMoveXMilli);
                Assert.AreEqual(0, intent.WorldMoveYMilli);

                CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter =
                    GetAttackAdapter(slice);
                Assert.IsTrue(
                    adapter.TryGetEvidence(
                        ai.Id.Value,
                        out CharacterTestAttackOpportunityDecisionEvidence evidence));
                Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, evidence.Status);
                Assert.AreEqual(CharacterTestAttackOpportunityLevel.CurrentPose, evidence.Level);
            }
        }

        [UnityTest]
        public IEnumerator RuntimePlanner_TurnInPlace_IsZeroMoveAndConsumedOnce()
        {
            yield return null;

            using (GameSlice slice = CreateSlice())
            {
                Spawn(slice, teamId: 1, x: 0f, z: 0.95f,
                    CharacterTestCharacterControlOwner.LocalPlayer);
                CharacterTestCharacterEntity ai = Spawn(
                    slice,
                    teamId: 2,
                    x: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled);
                ai.SetTransform(new CharacterTestCharacterTransformComponent(0f, 1f, 0f, 180f));

                slice.Tick(TickSeconds);

                RuntimeCommand firstCommand = FindPlannerControl(slice, ai.Id.Value);
                CharacterTestControlIntent firstIntent =
                    CharacterTestRuntimeCommandFactory.DecodeControlIntent(firstCommand);
                Assert.IsTrue(
                    firstIntent.TurnInPlace,
                    "Unexpected turn intent: attack=" + firstIntent.AttackPrimaryPressed
                    + " move=" + firstIntent.MoveXMilli + "," + firstIntent.MoveYMilli
                    + " world=" + firstIntent.WorldMoveXMilli + "," + firstIntent.WorldMoveYMilli
                    + " yaw=" + firstIntent.TurnInPlaceYawMilliDegrees
                    + " action=" + ai.Action.ActiveActionId
                    + " evidence=" + DescribeEvidence(GetAttackAdapter(slice).LastEvidence)
                    + " planner=" + DescribePlannerAgents(slice));
                Assert.IsFalse(firstIntent.AttackPrimaryPressed);
                Assert.AreEqual(0, firstIntent.MoveXMilli);
                Assert.AreEqual(0, firstIntent.MoveYMilli);
                Assert.AreEqual(0, firstIntent.WorldMoveXMilli);
                Assert.AreEqual(0, firstIntent.WorldMoveYMilli);
                Assert.AreNotEqual(0, firstIntent.TurnInPlaceYawMilliDegrees);
                int yawAfterTurn = ai.Transform.YawMilliDegrees;
                Assert.AreNotEqual(0, yawAfterTurn);

                // Motion consumes the edge command after writing the facing.
                Assert.IsFalse(ai.Input.TurnInPlace);

                slice.Tick(TickSeconds);
                CharacterTestControlIntent secondIntent;
                if (TryFindPlannerControl(
                    slice,
                    ai.Id.Value,
                    out RuntimeCommand secondCommand))
                {
                    secondIntent = CharacterTestRuntimeCommandFactory.DecodeControlIntent(secondCommand);
                    Assert.IsFalse(
                        secondIntent.TurnInPlace,
                        "The same turn delta must not be submitted on the next tick.");
                }

                Assert.AreEqual(yawAfterTurn, ai.Transform.YawMilliDegrees);
            }
        }

        [UnityTest]
        public IEnumerator RuntimePlanner_RequiresTranslation_ContinuesApproach()
        {
            yield return null;

            using (GameSlice slice = CreateSlice())
            {
                Spawn(slice, teamId: 1, x: 0f, z: 3f,
                    CharacterTestCharacterControlOwner.LocalPlayer);
                CharacterTestCharacterEntity ai = Spawn(
                    slice,
                    teamId: 2,
                    x: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled);

                slice.Tick(TickSeconds);

                RuntimeCommand command = FindPlannerControl(slice, ai.Id.Value);
                CharacterTestControlIntent intent =
                    CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
                Assert.IsFalse(intent.AttackPrimaryPressed);
                Assert.IsFalse(intent.TurnInPlace);
                Assert.Greater(
                    intent.WorldMoveXMilli * intent.WorldMoveXMilli
                    + intent.WorldMoveYMilli * intent.WorldMoveYMilli,
                    0,
                    "Both current and turned poses miss; Planner must continue approaching.");

                CharacterTestAttackOpportunityDecisionEvidence evidence =
                    GetAttackAdapter(slice).LastEvidence;
                Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, evidence.Status);
                Assert.AreEqual(
                    CharacterTestAttackOpportunityLevel.RequiresTranslation,
                    evidence.Level);
            }
        }

        [UnityTest]
        public IEnumerator CastAbilitySlot_RecordsExplicitCompatibilityLegacyRange()
        {
            yield return null;

            using (GameSlice slice = CreateSlice())
            {
                CharacterTestCharacterEntity ai = Spawn(
                    slice,
                    teamId: 2,
                    x: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled);
                slice.Tick(TickSeconds);

                CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter =
                    GetAttackAdapter(slice);
                adapter.MarkCompatibilityLegacyRange(ai, slice.CurrentFrame);
                Assert.IsTrue(adapter.TryGetEvidence(
                    ai.Id.Value,
                    out CharacterTestAttackOpportunityDecisionEvidence evidence));
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerAttackDecisionSource.CompatibilityLegacyRange,
                    evidence.Source);
                Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid, evidence.Status);
                Assert.AreEqual(CharacterTestAttackOpportunityLevel.NoOpportunity, evidence.Level);

                Assert.IsTrue(
                    slice.AbilityHotbar.TryGetAbilityForSlot(
                        1,
                        out AbilityDefinitionConfig ability),
                    "The CharacterTest hotbar must expose the synthetic cast slot.");
                RuntimeCommandValidationResult enqueue = slice.EnqueueCastAbilitySlot(
                    ai.Id.Value,
                    slotNumber: 1,
                    abilityId: ability.AbilityId,
                    sourceId: CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                    traceId: new RuntimeTraceIdentity(936775));
                Assert.IsTrue(enqueue.Success, enqueue.Error.Message);

                slice.Tick(TickSeconds);
                RuntimeCommand cast = FindCommand(
                    slice.RuntimeCommands.LastDrainedCommands,
                    CharacterTestRuntimeCommandIds.CastAbilitySlot,
                    ai.Id.Value,
                    CharacterTestRuntimeCommandSources.RuntimeAiPlanner);
                CharacterTestAbilitySlotCastRequest request =
                    CharacterTestRuntimeCommandFactory.DecodeAbilitySlotCast(cast);
                Assert.AreEqual(1, request.SlotNumber);
                Assert.AreEqual(ability.AbilityId, request.AbilityId);
                Assert.AreEqual(
                    CharacterTestCharacterActionConfigSource.FireballActionId,
                    ai.Action.ActiveActionId);
            }
        }

        private static GameSlice CreateSlice()
        {
            return new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                ActionConfigOptions = CharacterTestCharacterActionConfigSourceOptions
                    .CreateLegacyFixtureFallback(),
                PhysicsRuntime = CreatePlannerPhysicsRuntime(),
                RuntimeAiPlannerProfile = CharacterTestRuntimeAiPlannerProfileSource.CreateDefault(),
                StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>()
            });
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePlannerPhysicsRuntime()
        {
            return new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Zero,
                staticColliderCount: 1,
                sourceDescription: "cd-m1.playmode");
        }

        private static CharacterTestCharacterEntity Spawn(
            GameSlice slice,
            int teamId,
            float x,
            float z,
            CharacterTestCharacterControlOwner owner)
        {
            QueueSpawn(slice, teamId, x, z, owner);
            slice.Tick(TickSeconds);
            return FindEntity(slice, teamId, owner);
        }

        private static void QueueSpawn(
            GameSlice slice,
            int teamId,
            float x,
            float z,
            CharacterTestCharacterControlOwner owner)
        {
            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SpawnCharacter(
                slice.CurrentFrame,
                CharacterTestRuntimeCommandSources.System,
                new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                    teamId,
                    x,
                    1f,
                    z,
                    owner),
                traceId: new RuntimeTraceIdentity(9001, CharacterTestRuntimeCommandSources.System, slice.CurrentFrame.Value, CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId));
            RuntimeCommandValidationResult result = slice.EnqueueRuntimeCommand(command);
            Assert.IsTrue(result.Success, result.Error.Message);
        }

        private static CharacterTestCharacterEntity FindEntity(
            GameSlice slice,
            int teamId,
            CharacterTestCharacterControlOwner owner)
        {
            for (int i = slice.CharacterEntities.Count - 1; i >= 0; i--)
            {
                CharacterTestCharacterEntity entity = slice.CharacterEntities[i];
                if (entity.Team.TeamId == teamId && entity.Control.Owner == owner)
                    return entity;
            }

            Assert.Fail("CharacterTest spawn did not produce the requested entity.");
            return null;
        }

        private static RuntimeCommand FindPlannerControl(GameSlice slice, int entityId)
        {
            Assert.IsTrue(
                TryFindPlannerControl(slice, entityId, out RuntimeCommand command),
                "No Runtime AI Planner SetControlIntent was drained. "
                + DescribePlannerState(slice));
            return command;
        }

        private static bool TryFindPlannerControl(
            GameSlice slice,
            int entityId,
            out RuntimeCommand command)
        {
            IReadOnlyList<RuntimeCommand> commands = slice.RuntimeCommands.LastDrainedCommands;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand candidate = commands[i];
                if (candidate.SourceId != CharacterTestRuntimeCommandSources.RuntimeAiPlanner
                    || candidate.CommandId != CharacterTestRuntimeCommandIds.SetControlIntent)
                {
                    continue;
                }

                CharacterTestControlIntent intent =
                    CharacterTestRuntimeCommandFactory.DecodeControlIntent(candidate);
                if (intent.TargetEntityId == entityId)
                {
                    command = candidate;
                    return true;
                }
            }

            command = default(RuntimeCommand);
            return false;
        }

        private static RuntimeCommand FindCommand(
            IReadOnlyList<RuntimeCommand> commands,
            int commandId,
            int targetEntityId,
            int sourceId)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                if (command.CommandId == commandId
                    && command.TargetId == targetEntityId
                    && command.SourceId == sourceId)
                {
                    return command;
                }
            }

            Assert.Fail("Expected command was not drained: id=" + commandId
                + " target=" + targetEntityId
                + " source=" + sourceId);
            return default(RuntimeCommand);
        }

        private static CharacterTestRuntimeAiPlannerAttackOpportunityAdapter GetAttackAdapter(
            GameSlice slice)
        {
            FieldInfo field = typeof(CharacterTestRuntimeAiPlannerCommandModule).GetField(
                "_attackOpportunityAdapter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, "Runtime AI Planner adapter field is missing.");
            CharacterTestRuntimeAiPlannerAttackOpportunityAdapter adapter =
                field.GetValue(slice.RuntimeAiPlannerCommands)
                    as CharacterTestRuntimeAiPlannerAttackOpportunityAdapter;
            Assert.NotNull(adapter, "Runtime AI Planner adapter was not composed.");
            return adapter;
        }

        private static string DescribePlannerState(GameSlice slice)
        {
            CharacterTestRuntimeAiPlannerSnapshot snapshot =
                slice.RuntimeAiPlannerCommands.CreateSnapshot();
            return "plannerAvailable=" + snapshot.Available
                + " profile=" + snapshot.ProfileId
                + " agents=" + snapshot.Agents.Count;
        }

        private static string DescribePlannerAgents(GameSlice slice)
        {
            CharacterTestRuntimeAiPlannerSnapshot snapshot =
                slice.RuntimeAiPlannerCommands.CreateSnapshot();
            string text = string.Empty;
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                CharacterTestRuntimeAiPlannerAgentSnapshot agent = snapshot.Agents[i];
                text += "[" + agent.EntityId
                    + " target=" + agent.TargetEntityId
                    + " exists=" + agent.TargetExists
                    + " range=" + agent.TargetInAttackRange
                    + " selected=" + agent.SelectedActionKind
                    + " suppressed=" + agent.SuppressedReason + "]";
            }
            return text;
        }

        private static string DescribeEvidence(CharacterTestAttackOpportunityDecisionEvidence evidence)
        {
            return evidence == null
                ? "none"
                : evidence.Status + "/" + evidence.Level + "/" + evidence.FailureReason
                    + "/yaw=" + evidence.RequiredYawDeltaMilliDegrees;
        }

    }
}

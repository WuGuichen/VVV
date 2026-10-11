using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Shields;
using MxFramework.Combat.Projectiles;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiLabFrameSyncProbe
    {
        private static void CompareCommandState(
            CharacterTestRuntimeAiLabFrameSyncCommandStateSample expected,
            CharacterTestRuntimeAiLabFrameSyncCommandStateSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            bool includeTransientFrameState)
        {
            AddMismatch(rows, "command", 0, "cameraMode", expected.CameraMode, actual.CameraMode);
            AddMismatch(rows, "command", 0, "targetLockEnabled", expected.TargetLockEnabled, actual.TargetLockEnabled);
            AddMismatch(rows, "command", 0, "lockedTargetEntityId", expected.LockedTargetEntityId, actual.LockedTargetEntityId);
            AddMismatch(
                rows,
                "command",
                0,
                "runtimeAiBehaviorSelectionUnlocked",
                expected.RuntimeAiBehaviorSelectionUnlocked,
                actual.RuntimeAiBehaviorSelectionUnlocked);
            AddMismatch(rows, "command", 0, "pendingCommandCount", expected.PendingCommandCount, actual.PendingCommandCount);
            if (!includeTransientFrameState)
                return;

            AddMismatch(
                rows,
                "command",
                0,
                "lastDrainedCommandCount",
                expected.LastDrainedCommandCount,
                actual.LastDrainedCommandCount);
            AddMismatch(
                rows,
                "command",
                0,
                "lastDrainedCommandHash",
                expected.LastDrainedCommandHash,
                actual.LastDrainedCommandHash);
            AddMismatch(
                rows,
                "command",
                0,
                "lastCommandErrorCount",
                expected.LastCommandErrorCount,
                actual.LastCommandErrorCount);
            if (expected.LastDrainedCommandCount != actual.LastDrainedCommandCount
                || expected.LastDrainedCommandHash != actual.LastDrainedCommandHash)
            {
                AddMismatch(
                    rows,
                    "command",
                    0,
                    "lastDrainedCommands",
                    expected.LastDrainedCommandSignature,
                    actual.LastDrainedCommandSignature);
            }
        }

        private static void CompareRuntimeEvents(
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample expected,
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            if (!expected.Available && !actual.Available)
                return;

            AddMismatch(rows, "runtimeEvent", 0, "available", expected.Available, actual.Available);
            if (expected.Available != actual.Available)
                return;

            AddMismatch(rows, "runtimeEvent", 0, "pendingCount", expected.PendingCount, actual.PendingCount);
            AddMismatch(rows, "runtimeEvent", 0, "oldestFrame", expected.OldestFrame, actual.OldestFrame);
            AddMismatch(rows, "runtimeEvent", 0, "newestFrame", expected.NewestFrame, actual.NewestFrame);
            AddMismatch(rows, "runtimeEvent", 0, "nextSequence", expected.NextSequence, actual.NextSequence);
            AddMismatch(rows, "runtimeEvent", 0, "eventTypeName", expected.EventTypeName, actual.EventTypeName);
            AddMismatch(rows, "runtimeEvent", 0, "recentCount", expected.RecentCount, actual.RecentCount);
            AddMismatch(rows, "runtimeEvent", 0, "recentHash", expected.RecentHash, actual.RecentHash);
            AddMismatch(rows, "runtimeEvent", 0, "lastFrame", expected.LastFrame, actual.LastFrame);
            AddMismatch(rows, "runtimeEvent", 0, "lastType", expected.LastType, actual.LastType);
            AddMismatch(rows, "runtimeEvent", 0, "lastCommandId", expected.LastCommandId, actual.LastCommandId);
            AddMismatch(rows, "runtimeEvent", 0, "lastSourceId", expected.LastSourceId, actual.LastSourceId);
            AddMismatch(rows, "runtimeEvent", 0, "lastTargetId", expected.LastTargetId, actual.LastTargetId);
            AddMismatch(rows, "runtimeEvent", 0, "lastEntityId", expected.LastEntityId, actual.LastEntityId);
            AddMismatch(rows, "runtimeEvent", 0, "lastPayload0", expected.LastPayload0, actual.LastPayload0);
            AddMismatch(rows, "runtimeEvent", 0, "lastPayload1", expected.LastPayload1, actual.LastPayload1);
            AddMismatch(rows, "runtimeEvent", 0, "lastPayload2", expected.LastPayload2, actual.LastPayload2);
            AddMismatch(rows, "runtimeEvent", 0, "lastMessage", expected.LastMessage, actual.LastMessage);
        }

        private static void ComparePlanner(
            CharacterTestRuntimeAiLabFrameSyncPlannerSample expected,
            CharacterTestRuntimeAiLabFrameSyncPlannerSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "planner", 0, "available", expected.Available, actual.Available);
            AddMismatch(rows, "planner", 0, "frame", expected.Frame, actual.Frame);
            AddMismatch(rows, "planner", 0, "profileId", expected.ProfileId, actual.ProfileId);
            AddMismatch(rows, "planner", 0, "agentCount", expected.AgentCount, actual.AgentCount);
            int expectedCount = expected.Agents != null ? expected.Agents.Count : 0;
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample left = expected.Agents[i];
                if (!TryFindPlannerAgent(
                    actual.Agents,
                    left.EntityId,
                    out CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "planner",
                        left.EntityId,
                        "missing",
                        "present",
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "planner", left.EntityId, "targetEntityId", left.TargetEntityId, right.TargetEntityId);
                AddMismatch(rows, "planner", left.EntityId, "activeStatePath", left.ActiveStatePath, right.ActiveStatePath);
                AddMismatch(rows, "planner", left.EntityId, "targetExists", left.TargetExists, right.TargetExists);
                AddMismatch(rows, "planner", left.EntityId, "targetInAttackRange", left.TargetInAttackRange, right.TargetInAttackRange);
                AddMismatch(rows, "planner", left.EntityId, "targetDistanceMillimeters", left.TargetDistanceMillimeters, right.TargetDistanceMillimeters);
                AddMismatch(rows, "planner", left.EntityId, "targetVisible", left.TargetVisible, right.TargetVisible);
                AddMismatch(rows, "planner", left.EntityId, "targetLineOfSightBlocked", left.TargetLineOfSightBlocked, right.TargetLineOfSightBlocked);
                AddMismatch(rows, "planner", left.EntityId, "targetLastSeenAgeFrames", left.TargetLastSeenAgeFrames, right.TargetLastSeenAgeFrames);
                AddMismatch(rows, "planner", left.EntityId, "targetThreatScoreMilli", left.TargetThreatScoreMilli, right.TargetThreatScoreMilli);
                AddMismatch(rows, "planner", left.EntityId, "movePathAvailable", left.MovePathAvailable, right.MovePathAvailable);
                AddMismatch(rows, "planner", left.EntityId, "moveDirectPathClear", left.MoveDirectPathClear, right.MoveDirectPathClear);
                AddMismatch(rows, "planner", left.EntityId, "moveCandidateIndex", left.MoveCandidateIndex, right.MoveCandidateIndex);
                AddMismatch(rows, "planner", left.EntityId, "environmentNearObstacle", left.EnvironmentNearObstacle, right.EnvironmentNearObstacle);
                AddMismatch(rows, "planner", left.EntityId, "environmentNearHazard", left.EnvironmentNearHazard, right.EnvironmentNearHazard);
                AddMismatch(rows, "planner", left.EntityId, "environmentNearUsefulObject", left.EnvironmentNearUsefulObject, right.EnvironmentNearUsefulObject);
                AddMismatch(rows, "planner", left.EntityId, "selectedActionId", left.SelectedActionId, right.SelectedActionId);
                AddMismatch(rows, "planner", left.EntityId, "selectedActionKind", left.SelectedActionKind, right.SelectedActionKind);
                AddMismatch(rows, "planner", left.EntityId, "selectedActionName", left.SelectedActionName, right.SelectedActionName);
                AddMismatch(rows, "planner", left.EntityId, "selectedUtilityScoreMilli", left.SelectedUtilityScoreMilli, right.SelectedUtilityScoreMilli);
                AddMismatch(rows, "planner", left.EntityId, "selectedActionCostMilli", left.SelectedActionCostMilli, right.SelectedActionCostMilli);
                AddMismatch(rows, "planner", left.EntityId, "selectedAbilitySlot", left.SelectedAbilitySlot, right.SelectedAbilitySlot);
                AddMismatch(rows, "planner", left.EntityId, "targetAttackActionId", left.TargetAttackActionId, right.TargetAttackActionId);
                AddMismatch(rows, "planner", left.EntityId, "targetAttackSequence", left.TargetAttackSequence, right.TargetAttackSequence);
                AddMismatch(rows, "planner", left.EntityId, "targetAttackImpactFrames", left.TargetAttackImpactFrames, right.TargetAttackImpactFrames);
                AddMismatch(rows, "planner", left.EntityId, "targetAttackWillHitSelf", left.TargetAttackWillHitSelf, right.TargetAttackWillHitSelf);
                AddMismatch(rows, "planner", left.EntityId, "targetAttackInstanceResponded", left.TargetAttackInstanceResponded, right.TargetAttackInstanceResponded);
                AddMismatch(rows, "planner", left.EntityId, "selfDodgeCooldownReady", left.SelfDodgeCooldownReady, right.SelfDodgeCooldownReady);
                AddMismatch(rows, "planner", left.EntityId, "nextDodgeAllowedFrame", left.NextDodgeAllowedFrame, right.NextDodgeAllowedFrame);
                AddMismatch(rows, "planner", left.EntityId, "lastCommand", left.LastCommand, right.LastCommand);
                AddMismatch(rows, "planner", left.EntityId, "suppressedReason", left.SuppressedReason, right.SuppressedReason);
                AddMismatch(rows, "planner", left.EntityId, "rejectReason", left.RejectReason, right.RejectReason);
                AddMismatch(rows, "planner", left.EntityId, "transitionFromStateId", left.TransitionFromStateId, right.TransitionFromStateId);
                AddMismatch(rows, "planner", left.EntityId, "transitionToStateId", left.TransitionToStateId, right.TransitionToStateId);
                AddMismatch(rows, "planner", left.EntityId, "runtimeAiPlannerProfileSlot", left.RuntimeAiPlannerProfileSlot, right.RuntimeAiPlannerProfileSlot);
                AddMismatch(rows, "planner", left.EntityId, "runtimeAiBehaviorLoadoutId", left.RuntimeAiBehaviorLoadoutId, right.RuntimeAiBehaviorLoadoutId);
                AddMismatch(rows, "planner", left.EntityId, "runtimeAiBehaviorVariantId", left.RuntimeAiBehaviorVariantId, right.RuntimeAiBehaviorVariantId);
                AddMismatch(rows, "planner", left.EntityId, "runtimeAiBehaviorCatalogHash", left.RuntimeAiBehaviorCatalogHash, right.RuntimeAiBehaviorCatalogHash);
                AddMismatch(rows, "planner", left.EntityId, "runtimeAiBehaviorProfileSlot", left.RuntimeAiBehaviorProfileSlot, right.RuntimeAiBehaviorProfileSlot);
                AddMismatch(rows, "planner", left.EntityId, "movementSpeedScaleMilli", left.MovementSpeedScaleMilli, right.MovementSpeedScaleMilli);
                AddMismatch(rows, "planner", left.EntityId, "movementStrafeSide", left.MovementStrafeSide, right.MovementStrafeSide);
                AddMismatch(rows, "planner", left.EntityId, "movementDirectionHoldFrames", left.MovementDirectionHoldFrames, right.MovementDirectionHoldFrames);
                AddMismatch(rows, "planner", left.EntityId, "movementReactionDelayFrames", left.MovementReactionDelayFrames, right.MovementReactionDelayFrames);
                AddMismatch(rows, "planner", left.EntityId, "hasScenePreviewGeometry", left.HasScenePreviewGeometry, right.HasScenePreviewGeometry);
                AddMismatch(rows, "planner", left.EntityId, "sourceCenterXMillimeters", left.SourceCenterXMillimeters, right.SourceCenterXMillimeters);
                AddMismatch(rows, "planner", left.EntityId, "targetCenterXMillimeters", left.TargetCenterXMillimeters, right.TargetCenterXMillimeters);
                AddMismatch(rows, "planner", left.EntityId, "decisionMoveXMilli", left.DecisionMoveXMilli, right.DecisionMoveXMilli);
                AddMismatch(rows, "planner", left.EntityId, "attackRangeMillimeters", left.AttackRangeMillimeters, right.AttackRangeMillimeters);
            }
        }

        private static bool TryFindPlannerAgent(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample> samples,
            int entityId,
            out CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.EntityId == entityId)
                    return true;
            }

            sample = default;
            return false;
        }
    }
}
namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncPlannerSample
    {
        public CharacterTestRuntimeAiLabFrameSyncPlannerSample(
            bool available,
            int frame,
            string profileId,
            CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample[] agents)
        {
            Available = available;
            Frame = frame;
            ProfileId = profileId ?? string.Empty;
            Agents = agents ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample>();
        }

        public bool Available { get; }
        public int Frame { get; }
        public string ProfileId { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample> Agents { get; }
        public int AgentCount => Agents != null ? Agents.Count : 0;

        public static CharacterTestRuntimeAiLabFrameSyncPlannerSample Capture(
            CharacterTestRuntimeAiPlannerSnapshot snapshot,
            int frame)
        {
            if (snapshot == null)
            {
                return new CharacterTestRuntimeAiLabFrameSyncPlannerSample(
                    available: false,
                    frame: frame,
                    profileId: string.Empty,
                    agents: Array.Empty<CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample>());
            }

            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> source = snapshot.Agents;
            int count = source != null ? source.Count : 0;
            var agents = new CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample[count];
            for (int i = 0; i < count; i++)
                agents[i] = CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample.Capture(source[i]);

            return new CharacterTestRuntimeAiLabFrameSyncPlannerSample(
                snapshot.Available,
                frame,
                snapshot.ProfileId,
                agents);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample
    {
        public CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample(
            int entityId,
            int targetEntityId,
            string activeStatePath,
            bool targetExists,
            bool targetInAttackRange,
            int targetDistanceMillimeters,
            bool targetVisible,
            bool targetLineOfSightBlocked,
            int targetLastSeenAgeFrames,
            int targetThreatScoreMilli,
            bool movePathAvailable,
            bool moveDirectPathClear,
            int moveCandidateIndex,
            bool environmentNearObstacle,
            bool environmentNearHazard,
            bool environmentNearUsefulObject,
            int selectedActionId,
            int selectedActionKind,
            string selectedActionName,
            int selectedUtilityScoreMilli,
            int selectedActionCostMilli,
            int selectedAbilitySlot,
            string lastCommand,
            string suppressedReason,
            string rejectReason,
            int transitionFromStateId,
            int transitionToStateId,
            int runtimeAiPlannerProfileSlot,
            int runtimeAiBehaviorLoadoutId,
            int runtimeAiBehaviorVariantId,
            long runtimeAiBehaviorCatalogHash,
            int runtimeAiBehaviorProfileSlot,
            int movementSpeedScaleMilli,
            int movementStrafeSide,
            int movementDirectionHoldFrames,
            int movementReactionDelayFrames,
            bool hasScenePreviewGeometry,
            int sourceCenterXMillimeters,
            int targetCenterXMillimeters,
            int decisionMoveXMilli,
            int attackRangeMillimeters,
            int targetAttackActionId = 0,
            int targetAttackSequence = 0,
            int targetAttackImpactFrames = -1,
            bool targetAttackWillHitSelf = false,
            bool targetAttackInstanceResponded = false,
            bool selfDodgeCooldownReady = true,
            int nextDodgeAllowedFrame = 0)
        {
            EntityId = entityId;
            TargetEntityId = targetEntityId;
            ActiveStatePath = activeStatePath ?? string.Empty;
            TargetExists = targetExists;
            TargetInAttackRange = targetInAttackRange;
            TargetDistanceMillimeters = targetDistanceMillimeters;
            TargetVisible = targetVisible;
            TargetLineOfSightBlocked = targetLineOfSightBlocked;
            TargetLastSeenAgeFrames = targetLastSeenAgeFrames;
            TargetThreatScoreMilli = targetThreatScoreMilli;
            MovePathAvailable = movePathAvailable;
            MoveDirectPathClear = moveDirectPathClear;
            MoveCandidateIndex = moveCandidateIndex;
            EnvironmentNearObstacle = environmentNearObstacle;
            EnvironmentNearHazard = environmentNearHazard;
            EnvironmentNearUsefulObject = environmentNearUsefulObject;
            SelectedActionId = selectedActionId;
            SelectedActionKind = selectedActionKind;
            SelectedActionName = selectedActionName ?? string.Empty;
            SelectedUtilityScoreMilli = selectedUtilityScoreMilli;
            SelectedActionCostMilli = selectedActionCostMilli;
            SelectedAbilitySlot = selectedAbilitySlot;
            LastCommand = lastCommand ?? string.Empty;
            SuppressedReason = suppressedReason ?? string.Empty;
            RejectReason = rejectReason ?? string.Empty;
            TransitionFromStateId = transitionFromStateId;
            TransitionToStateId = transitionToStateId;
            RuntimeAiPlannerProfileSlot = runtimeAiPlannerProfileSlot;
            RuntimeAiBehaviorLoadoutId = runtimeAiBehaviorLoadoutId;
            RuntimeAiBehaviorVariantId = runtimeAiBehaviorVariantId;
            RuntimeAiBehaviorCatalogHash = runtimeAiBehaviorCatalogHash;
            RuntimeAiBehaviorProfileSlot = runtimeAiBehaviorProfileSlot;
            MovementSpeedScaleMilli = movementSpeedScaleMilli;
            MovementStrafeSide = movementStrafeSide;
            MovementDirectionHoldFrames = movementDirectionHoldFrames;
            MovementReactionDelayFrames = movementReactionDelayFrames;
            HasScenePreviewGeometry = hasScenePreviewGeometry;
            SourceCenterXMillimeters = sourceCenterXMillimeters;
            TargetCenterXMillimeters = targetCenterXMillimeters;
            DecisionMoveXMilli = decisionMoveXMilli;
            AttackRangeMillimeters = attackRangeMillimeters;
            TargetAttackActionId = targetAttackActionId;
            TargetAttackSequence = targetAttackSequence;
            TargetAttackImpactFrames = targetAttackImpactFrames;
            TargetAttackWillHitSelf = targetAttackWillHitSelf;
            TargetAttackInstanceResponded = targetAttackInstanceResponded;
            SelfDodgeCooldownReady = selfDodgeCooldownReady;
            NextDodgeAllowedFrame = nextDodgeAllowedFrame;
        }

        public int EntityId { get; }
        public int TargetEntityId { get; }
        public string ActiveStatePath { get; }
        public bool TargetExists { get; }
        public bool TargetInAttackRange { get; }
        public int TargetDistanceMillimeters { get; }
        public bool TargetVisible { get; }
        public bool TargetLineOfSightBlocked { get; }
        public int TargetLastSeenAgeFrames { get; }
        public int TargetThreatScoreMilli { get; }
        public bool MovePathAvailable { get; }
        public bool MoveDirectPathClear { get; }
        public int MoveCandidateIndex { get; }
        public bool EnvironmentNearObstacle { get; }
        public bool EnvironmentNearHazard { get; }
        public bool EnvironmentNearUsefulObject { get; }
        public int SelectedActionId { get; }
        public int SelectedActionKind { get; }
        public string SelectedActionName { get; }
        public int SelectedUtilityScoreMilli { get; }
        public int SelectedActionCostMilli { get; }
        public int SelectedAbilitySlot { get; }
        public string LastCommand { get; }
        public string SuppressedReason { get; }
        public string RejectReason { get; }
        public int TransitionFromStateId { get; }
        public int TransitionToStateId { get; }
        public int RuntimeAiPlannerProfileSlot { get; }
        public int RuntimeAiBehaviorLoadoutId { get; }
        public int RuntimeAiBehaviorVariantId { get; }
        public long RuntimeAiBehaviorCatalogHash { get; }
        public int RuntimeAiBehaviorProfileSlot { get; }
        public int MovementSpeedScaleMilli { get; }
        public int MovementStrafeSide { get; }
        public int MovementDirectionHoldFrames { get; }
        public int MovementReactionDelayFrames { get; }
        public bool HasScenePreviewGeometry { get; }
        public int SourceCenterXMillimeters { get; }
        public int TargetCenterXMillimeters { get; }
        public int DecisionMoveXMilli { get; }
        public int AttackRangeMillimeters { get; }
        public int TargetAttackActionId { get; }
        public int TargetAttackSequence { get; }
        public int TargetAttackImpactFrames { get; }
        public bool TargetAttackWillHitSelf { get; }
        public bool TargetAttackInstanceResponded { get; }
        public bool SelfDodgeCooldownReady { get; }
        public int NextDodgeAllowedFrame { get; }

        public static CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample Capture(
            CharacterTestRuntimeAiPlannerAgentSnapshot snapshot)
        {
            return new CharacterTestRuntimeAiLabFrameSyncPlannerAgentSample(
                snapshot.EntityId,
                snapshot.TargetEntityId,
                snapshot.ActiveStatePath,
                snapshot.TargetExists,
                snapshot.TargetInAttackRange,
                ToMilli(snapshot.TargetDistance),
                snapshot.TargetVisible,
                snapshot.TargetLineOfSightBlocked,
                snapshot.TargetLastSeenAgeFrames,
                ToMilli(snapshot.TargetThreatScore),
                snapshot.MovePathAvailable,
                snapshot.MoveDirectPathClear,
                snapshot.MoveCandidateIndex,
                snapshot.EnvironmentNearObstacle,
                snapshot.EnvironmentNearHazard,
                snapshot.EnvironmentNearUsefulObject,
                snapshot.SelectedActionId,
                (int)snapshot.SelectedActionKind,
                snapshot.SelectedActionName,
                ToMilli(snapshot.SelectedUtilityScore),
                ToMilli(snapshot.SelectedActionCost),
                snapshot.SelectedAbilitySlot,
                snapshot.LastCommand,
                snapshot.SuppressedReason,
                snapshot.RejectReason,
                snapshot.TransitionFromStateId,
                snapshot.TransitionToStateId,
                snapshot.RuntimeAiPlannerProfileSlot,
                snapshot.RuntimeAiBehaviorLoadoutId,
                snapshot.RuntimeAiBehaviorVariantId,
                snapshot.RuntimeAiBehaviorCatalogHash,
                snapshot.RuntimeAiBehaviorProfileSlot,
                ToMilli(snapshot.MovementSpeedScale),
                snapshot.MovementStrafeSide,
                snapshot.MovementDirectionHoldFrames,
                snapshot.MovementReactionDelayFrames,
                snapshot.HasScenePreviewGeometry,
                ToMilli(snapshot.ScenePreviewSourceCenterX),
                ToMilli(snapshot.ScenePreviewTargetCenterX),
                ToMilli(snapshot.ScenePreviewDecisionMoveX),
                ToMilli(snapshot.ScenePreviewAttackRange),
                snapshot.TargetAttackActionId.Value,
                snapshot.TargetAttackSequence,
                snapshot.TargetAttackImpactFrames,
                snapshot.TargetAttackWillHitSelf,
                snapshot.TargetAttackInstanceResponded,
                snapshot.SelfDodgeCooldownReady,
                snapshot.NextDodgeAllowedFrame);
        }

        private static int ToMilli(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0;

            return (int)Math.Round(value * 1000f, MidpointRounding.AwayFromZero);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncCommandStateSample
    {
        public CharacterTestRuntimeAiLabFrameSyncCommandStateSample(
            int cameraMode,
            bool targetLockEnabled,
            int lockedTargetEntityId,
            bool runtimeAiBehaviorSelectionUnlocked,
            int pendingCommandCount,
            int lastDrainedCommandCount,
            long lastDrainedCommandHash,
            string lastDrainedCommandSignature,
            int lastCommandErrorCount)
        {
            CameraMode = cameraMode;
            TargetLockEnabled = targetLockEnabled;
            LockedTargetEntityId = lockedTargetEntityId;
            RuntimeAiBehaviorSelectionUnlocked = runtimeAiBehaviorSelectionUnlocked;
            PendingCommandCount = pendingCommandCount;
            LastDrainedCommandCount = lastDrainedCommandCount;
            LastDrainedCommandHash = lastDrainedCommandHash;
            LastDrainedCommandSignature = lastDrainedCommandSignature ?? string.Empty;
            LastCommandErrorCount = lastCommandErrorCount;
        }

        public int CameraMode { get; }
        public bool TargetLockEnabled { get; }
        public int LockedTargetEntityId { get; }
        public bool RuntimeAiBehaviorSelectionUnlocked { get; }
        public int PendingCommandCount { get; }
        public int LastDrainedCommandCount { get; }
        public long LastDrainedCommandHash { get; }
        public string LastDrainedCommandSignature { get; }
        public int LastCommandErrorCount { get; }

        public static CharacterTestRuntimeAiLabFrameSyncCommandStateSample Capture(
            CharacterTestRuntimeCommandModule module)
        {
            if (module == null)
                return default;

            return new CharacterTestRuntimeAiLabFrameSyncCommandStateSample(
                (int)module.CameraMode,
                module.TargetLockEnabled,
                module.TargetLockEnabled ? module.LockedTargetEntityId : 0,
                module.RuntimeAiBehaviorSelectionUnlocked,
                module.PendingCommandCount,
                module.LastDrainedCommands != null ? module.LastDrainedCommands.Count : 0,
                ComputeCommandHash(module.LastDrainedCommands),
                BuildCommandSignature(module.LastDrainedCommands),
                module.LastCommandErrors != null ? module.LastCommandErrors.Count : 0);
        }

        private static string BuildCommandSignature(IReadOnlyList<RuntimeCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return "none";

            var builder = new StringBuilder(commands.Count * 48);
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                if (i > 0)
                    builder.Append("; ");
                builder.Append(i.ToString(CultureInfo.InvariantCulture));
                builder.Append(":f=");
                builder.Append(command.Frame.Value.ToString(CultureInfo.InvariantCulture));
                builder.Append(",s=");
                builder.Append(command.SourceId.ToString(CultureInfo.InvariantCulture));
                builder.Append(",c=");
                builder.Append(command.CommandId.ToString(CultureInfo.InvariantCulture));
                builder.Append(",t=");
                builder.Append(command.TargetId.ToString(CultureInfo.InvariantCulture));
                builder.Append(",p=");
                builder.Append(command.Payload0.ToString(CultureInfo.InvariantCulture));
                builder.Append('/');
                builder.Append(command.Payload1.ToString(CultureInfo.InvariantCulture));
                builder.Append('/');
                builder.Append(command.Payload2.ToString(CultureInfo.InvariantCulture));
                if (command.TraceId.IsValid)
                {
                    builder.Append(",trace=");
                    builder.Append(command.TraceId.KindId.ToString(CultureInfo.InvariantCulture));
                    builder.Append(':');
                    builder.Append(command.TraceId.SourceId.ToString(CultureInfo.InvariantCulture));
                    builder.Append(':');
                    builder.Append(command.TraceId.ExecutionId.ToString(CultureInfo.InvariantCulture));
                    builder.Append(':');
                    builder.Append(command.TraceId.Sequence.ToString(CultureInfo.InvariantCulture));
                }
            }

            return builder.ToString();
        }

        private static long ComputeCommandHash(IReadOnlyList<RuntimeCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return 0L;

            unchecked
            {
                long hash = 1469598103934665603L;
                for (int i = 0; i < commands.Count; i++)
                {
                    RuntimeCommand command = commands[i];
                    hash = AddHash(hash, command.Frame.Value);
                    hash = AddHash(hash, command.SourceId);
                    hash = AddHash(hash, command.CommandId);
                    hash = AddHash(hash, command.TargetId);
                    hash = AddHash(hash, command.Payload0);
                    hash = AddHash(hash, command.Payload1);
                    hash = AddHash(hash, command.Payload2);
                    hash = AddHash(hash, i);
                    hash = AddHash(hash, command.TraceId.KindId);
                    hash = AddHash(hash, command.TraceId.SourceId);
                    hash = AddHash(hash, command.TraceId.ExecutionId);
                    hash = AddHash(hash, command.TraceId.Sequence);
                }

                return hash;
            }
        }

        private static long AddHash(long hash, long value)
        {
            unchecked
            {
                hash ^= value;
                return hash * 1099511628211L;
            }
        }

    }

}

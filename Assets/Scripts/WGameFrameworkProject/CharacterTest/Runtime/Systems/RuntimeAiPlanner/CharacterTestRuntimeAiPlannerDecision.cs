using System;
using System.Collections.Generic;
using MxFramework.AI;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiPlannerDecision
    {
        public CharacterTestRuntimeAiPlannerDecision(
            int entityId,
            int targetEntityId,
            int activeStateId,
            string activeStatePath,
            int selectedActionId,
            string selectedActionName,
            CharacterTestRuntimeAiPlannerActionKind selectedActionKind,
            float selectedUtilityScore,
            float selectedActionCost,
            Vector2 move,
            bool attackPrimaryPressed,
            int skillSlotPressedMask,
            bool shouldSubmitControlIntent,
            string suppressedReason,
            string rejectReason,
            int transitionFromStateId,
            int transitionToStateId,
            bool targetExists,
            bool targetInAttackRange,
            float targetDistance,
            bool movePathAvailable)
        {
            EntityId = entityId;
            TargetEntityId = targetEntityId;
            ActiveStateId = activeStateId;
            ActiveStatePath = activeStatePath ?? string.Empty;
            SelectedActionId = selectedActionId;
            SelectedActionName = selectedActionName ?? string.Empty;
            SelectedActionKind = selectedActionKind;
            SelectedUtilityScore = selectedUtilityScore;
            SelectedActionCost = selectedActionCost;
            Move = Vector2.ClampMagnitude(move, 1f);
            AttackPrimaryPressed = attackPrimaryPressed;
            SkillSlotPressedMask = skillSlotPressedMask & CharacterTestControlIntent.SkillSlotMaskBits;
            ShouldSubmitControlIntent = shouldSubmitControlIntent;
            SuppressedReason = suppressedReason ?? string.Empty;
            RejectReason = rejectReason ?? string.Empty;
            TransitionFromStateId = transitionFromStateId;
            TransitionToStateId = transitionToStateId;
            TargetExists = targetExists;
            TargetInAttackRange = targetInAttackRange;
            TargetDistance = targetDistance;
            MovePathAvailable = movePathAvailable;
            TargetVisible = false;
            TargetLineOfSightBlocked = false;
            TargetLastSeenAgeFrames = 0;
            TargetThreatScore = 0f;
            SelfSurfaceKind = 0;
            SelfOnHardSlick = false;
            SelfNearMovableObject = false;
            MoveDirectPathClear = false;
            MoveCandidateIndex = -1;
            EnvironmentNearObstacle = false;
            EnvironmentNearHazard = false;
            EnvironmentNearUsefulObject = false;
            MovementSpeedScale = 0f;
            MovementStrafeSide = 0;
            MovementDirectionHoldFrames = 0;
            MovementReactionDelayFrames = 0;
            HasScenePreviewGeometry = false;
            ScenePreviewSourceCenterX = 0f;
            ScenePreviewSourceCenterY = 0f;
            ScenePreviewSourceCenterZ = 0f;
            ScenePreviewTargetCenterX = 0f;
            ScenePreviewTargetCenterY = 0f;
            ScenePreviewTargetCenterZ = 0f;
            ScenePreviewTargetDirectionX = 0f;
            ScenePreviewTargetDirectionZ = 0f;
            ScenePreviewTargetBearingMilliDegrees = 0;
            ScenePreviewFacingDeltaMilliDegrees = 0;
            ScenePreviewFacingDot = 0f;
            ScenePreviewDecisionMoveX = Move.x;
            ScenePreviewDecisionMoveZ = Move.y;
            ScenePreviewAttackRange = 0f;
            TargetDead = false;
            TargetLifeState = CharacterTestRuntimeAiPlannerTargetLifeState.None;
        }

        public CharacterTestRuntimeAiPlannerDecision(
            int entityId,
            int targetEntityId,
            int activeStateId,
            string activeStatePath,
            int selectedActionId,
            string selectedActionName,
            CharacterTestRuntimeAiPlannerActionKind selectedActionKind,
            float selectedUtilityScore,
            float selectedActionCost,
            Vector2 move,
            bool attackPrimaryPressed,
            int skillSlotPressedMask,
            bool shouldSubmitControlIntent,
            string suppressedReason,
            string rejectReason,
            int transitionFromStateId,
            int transitionToStateId,
            bool targetExists,
            bool targetInAttackRange,
            float targetDistance,
            bool movePathAvailable,
            bool targetVisible,
            bool targetLineOfSightBlocked,
            int targetLastSeenAgeFrames,
            float targetThreatScore,
            int selfSurfaceKind,
            bool selfOnHardSlick,
            bool selfNearMovableObject,
            bool moveDirectPathClear,
            int moveCandidateIndex,
            bool environmentNearObstacle,
            bool environmentNearHazard,
            bool environmentNearUsefulObject,
            float movementSpeedScale = 0f,
            int movementStrafeSide = 0,
            int movementDirectionHoldFrames = 0,
            int movementReactionDelayFrames = 0,
            bool hasScenePreviewGeometry = false,
            float scenePreviewSourceCenterX = 0f,
            float scenePreviewSourceCenterY = 0f,
            float scenePreviewSourceCenterZ = 0f,
            float scenePreviewTargetCenterX = 0f,
            float scenePreviewTargetCenterY = 0f,
            float scenePreviewTargetCenterZ = 0f,
            float scenePreviewTargetDirectionX = 0f,
            float scenePreviewTargetDirectionZ = 0f,
            int scenePreviewTargetBearingMilliDegrees = 0,
            int scenePreviewFacingDeltaMilliDegrees = 0,
            float scenePreviewFacingDot = 0f,
            float scenePreviewAttackRange = 0f,
            bool targetDead = false,
            CharacterTestRuntimeAiPlannerTargetLifeState targetLifeState =
                CharacterTestRuntimeAiPlannerTargetLifeState.None)
            : this(
                entityId,
                targetEntityId,
                activeStateId,
                activeStatePath,
                selectedActionId,
                selectedActionName,
                selectedActionKind,
                selectedUtilityScore,
                selectedActionCost,
                move,
                attackPrimaryPressed,
                skillSlotPressedMask,
                shouldSubmitControlIntent,
                suppressedReason,
                rejectReason,
                transitionFromStateId,
                transitionToStateId,
                targetExists,
                targetInAttackRange,
                targetDistance,
                movePathAvailable)
        {
            TargetVisible = targetVisible;
            TargetLineOfSightBlocked = targetLineOfSightBlocked;
            TargetLastSeenAgeFrames = targetLastSeenAgeFrames < 0 ? 0 : targetLastSeenAgeFrames;
            TargetThreatScore = targetThreatScore > 0f ? targetThreatScore : 0f;
            SelfSurfaceKind = selfSurfaceKind;
            SelfOnHardSlick = selfOnHardSlick;
            SelfNearMovableObject = selfNearMovableObject;
            MoveDirectPathClear = moveDirectPathClear;
            MoveCandidateIndex = moveCandidateIndex;
            EnvironmentNearObstacle = environmentNearObstacle;
            EnvironmentNearHazard = environmentNearHazard;
            EnvironmentNearUsefulObject = environmentNearUsefulObject;
            MovementSpeedScale = movementSpeedScale > 0f ? movementSpeedScale : 0f;
            MovementStrafeSide = movementStrafeSide;
            MovementDirectionHoldFrames = movementDirectionHoldFrames > 0 ? movementDirectionHoldFrames : 0;
            MovementReactionDelayFrames = movementReactionDelayFrames > 0 ? movementReactionDelayFrames : 0;
            HasScenePreviewGeometry = hasScenePreviewGeometry;
            ScenePreviewSourceCenterX = scenePreviewSourceCenterX;
            ScenePreviewSourceCenterY = scenePreviewSourceCenterY;
            ScenePreviewSourceCenterZ = scenePreviewSourceCenterZ;
            ScenePreviewTargetCenterX = scenePreviewTargetCenterX;
            ScenePreviewTargetCenterY = scenePreviewTargetCenterY;
            ScenePreviewTargetCenterZ = scenePreviewTargetCenterZ;
            ScenePreviewTargetDirectionX = scenePreviewTargetDirectionX;
            ScenePreviewTargetDirectionZ = scenePreviewTargetDirectionZ;
            ScenePreviewTargetBearingMilliDegrees = scenePreviewTargetBearingMilliDegrees;
            ScenePreviewFacingDeltaMilliDegrees = scenePreviewFacingDeltaMilliDegrees;
            ScenePreviewFacingDot = scenePreviewFacingDot;
            ScenePreviewAttackRange = scenePreviewAttackRange > 0f ? scenePreviewAttackRange : 0f;
            TargetDead = targetDead;
            TargetLifeState = targetLifeState;
        }

        public int EntityId { get; }
        public int TargetEntityId { get; }
        public int ActiveStateId { get; }
        public string ActiveStatePath { get; }
        public int SelectedActionId { get; }
        public string SelectedActionName { get; }
        public CharacterTestRuntimeAiPlannerActionKind SelectedActionKind { get; }
        public float SelectedUtilityScore { get; }
        public float SelectedActionCost { get; }
        public Vector2 Move { get; }
        public bool AttackPrimaryPressed { get; }
        public bool DodgePressed => SelectedActionKind == CharacterTestRuntimeAiPlannerActionKind.Dodge;
        public int SkillSlotPressedMask { get; }
        public bool ShouldSubmitControlIntent { get; }
        public string SuppressedReason { get; }
        public string RejectReason { get; }
        public int TransitionFromStateId { get; }
        public int TransitionToStateId { get; }
        public bool TargetExists { get; }
        public bool TargetInAttackRange { get; }
        public float TargetDistance { get; }
        public bool MovePathAvailable { get; }
        public bool TargetVisible { get; }
        public bool TargetDead { get; }
        public CharacterTestRuntimeAiPlannerTargetLifeState TargetLifeState { get; }
        public bool TargetLineOfSightBlocked { get; }
        public int TargetLastSeenAgeFrames { get; }
        public float TargetThreatScore { get; }
        public int SelfSurfaceKind { get; }
        public bool SelfOnHardSlick { get; }
        public bool SelfNearMovableObject { get; }
        public bool MoveDirectPathClear { get; }
        public int MoveCandidateIndex { get; }
        public bool EnvironmentNearObstacle { get; }
        public bool EnvironmentNearHazard { get; }
        public bool EnvironmentNearUsefulObject { get; }
        public float MovementSpeedScale { get; }
        public int MovementStrafeSide { get; }
        public int MovementDirectionHoldFrames { get; }
        public int MovementReactionDelayFrames { get; }
        public bool HasScenePreviewGeometry { get; }
        public float ScenePreviewSourceCenterX { get; }
        public float ScenePreviewSourceCenterY { get; }
        public float ScenePreviewSourceCenterZ { get; }
        public float ScenePreviewTargetCenterX { get; }
        public float ScenePreviewTargetCenterY { get; }
        public float ScenePreviewTargetCenterZ { get; }
        public float ScenePreviewTargetDirectionX { get; }
        public float ScenePreviewTargetDirectionZ { get; }
        public int ScenePreviewTargetBearingMilliDegrees { get; }
        public int ScenePreviewFacingDeltaMilliDegrees { get; }
        public float ScenePreviewFacingDot { get; }
        public float ScenePreviewDecisionMoveX { get; }
        public float ScenePreviewDecisionMoveZ { get; }
        public float ScenePreviewAttackRange { get; }

        public static CharacterTestRuntimeAiPlannerDecision Empty(
            int entityId,
            int activeStateId,
            string activeStatePath,
            string suppressedReason)
        {
            return new CharacterTestRuntimeAiPlannerDecision(
                entityId,
                targetEntityId: 0,
                activeStateId,
                activeStatePath,
                selectedActionId: 0,
                selectedActionName: string.Empty,
                CharacterTestRuntimeAiPlannerActionKind.Wait,
                selectedUtilityScore: 0f,
                selectedActionCost: 0f,
                Vector2.zero,
                attackPrimaryPressed: false,
                skillSlotPressedMask: 0,
                shouldSubmitControlIntent: false,
                suppressedReason,
                rejectReason: string.Empty,
                transitionFromStateId: 0,
                transitionToStateId: 0,
                targetExists: false,
                targetInAttackRange: false,
                targetDistance: 0f,
                movePathAvailable: false,
                targetVisible: false,
                targetLineOfSightBlocked: false,
                targetLastSeenAgeFrames: 0,
                targetThreatScore: 0f,
                selfSurfaceKind: 0,
                selfOnHardSlick: false,
                selfNearMovableObject: false,
                moveDirectPathClear: false,
                moveCandidateIndex: -1,
                environmentNearObstacle: false,
                environmentNearHazard: false,
                environmentNearUsefulObject: false);
        }
    }
}

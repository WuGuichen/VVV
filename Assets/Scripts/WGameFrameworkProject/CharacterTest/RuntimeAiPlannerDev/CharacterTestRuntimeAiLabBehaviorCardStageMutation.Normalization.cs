using System;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiLabBehaviorCardStageMutation
    {
        private static void NormalizeStageKind(
            CharacterTestRuntimeAiLabLogicStage stage,
            CharacterTestRuntimeAiLabCardStepKind target)
        {
            CharacterTestRuntimeAiLabCardStepKind sourceKind = stage.Kind;
            int oldAbilityId = stage.AbilityId;
            CharacterActionId oldActionId = stage.CharacterActionId;
            CharacterTestRuntimeAiPlannerActionKind oldLegacy = stage.LegacyActionKind;
            CharacterTestRuntimeAiLabMovementStrategy oldMovementStrategy = stage.MovementStrategy;
            CharacterTestRuntimeAiLabMovementTargetKind oldMoveTargetKind = stage.MovementTargetKind;
            float oldMoveTargetX = stage.MoveTargetPositionX;
            float oldMoveTargetZ = stage.MoveTargetPositionZ;
            int oldMoveTargetEntityId = stage.MoveTargetEntityId;
            float oldMoveMin = stage.MoveMinDistance;
            float oldMoveMax = stage.MoveMaxDistance;
            int oldWait = stage.WaitFrames;
            stage.Kind = target;
            ResetActionParameters(stage);
            switch (target)
            {
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                    if (sourceKind == CharacterTestRuntimeAiLabCardStepKind.UseAbility
                        || (sourceKind == CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly
                            && oldLegacy == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot))
                        stage.AbilityId = Math.Max(0, oldAbilityId);
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    if (sourceKind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction)
                        stage.CharacterActionId = oldActionId;
                    else if (sourceKind == CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly
                        && oldLegacy == CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack)
                        stage.CharacterActionId = CharacterTestCharacterActionConfigSource.LightAttackActionId;
                    else if (sourceKind == CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly
                        && oldLegacy == CharacterTestRuntimeAiPlannerActionKind.Dodge)
                        stage.CharacterActionId = CharacterTestCharacterActionConfigSource.DefaultEvadeActionId;
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                    if ((sourceKind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                            || (sourceKind == CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly
                                && IsLegacyMovement(oldLegacy)))
                        && IsValidMoveBand(oldMoveMin, oldMoveMax))
                    {
                        stage.MoveMinDistance = oldMoveMin;
                        stage.MoveMaxDistance = oldMoveMax;
                        if (sourceKind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget)
                        {
                            stage.MovementTargetKind = oldMoveTargetKind;
                            stage.MoveTargetPositionX = oldMoveTargetX;
                            stage.MoveTargetPositionZ = oldMoveTargetZ;
                            stage.MoveTargetEntityId = oldMoveTargetEntityId;
                        }
                    }
                    else
                    {
                        SetDefaultMoveBand(stage);
                    }
                    if (sourceKind == CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly)
                        stage.MovementStrategy = MovementStrategyFromLegacy(oldLegacy);
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.Wait:
                    stage.WaitFrames = (sourceKind == CharacterTestRuntimeAiLabCardStepKind.Wait
                            || (sourceKind == CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly
                                && oldLegacy == CharacterTestRuntimeAiPlannerActionKind.Wait))
                        && oldWait > 0
                        ? oldWait
                        : Math.Max(1, stage.PlannedDurationFrames > 0 ? stage.PlannedDurationFrames : 60);
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly:
                    RestoreLegacyProjection(
                        stage,
                        sourceKind,
                        oldAbilityId,
                        oldActionId,
                        oldMovementStrategy,
                        oldMoveMin,
                        oldMoveMax,
                        oldWait);
                    break;
            }
        }

        private static void RestoreLegacyProjection(
            CharacterTestRuntimeAiLabLogicStage stage,
            CharacterTestRuntimeAiLabCardStepKind sourceKind,
            int oldAbilityId,
            CharacterActionId oldActionId,
            CharacterTestRuntimeAiLabMovementStrategy oldMovementStrategy,
            float oldMoveMin,
            float oldMoveMax,
            int oldWait)
        {
            switch (sourceKind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                    stage.LegacyActionKind = CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot;
                    stage.AbilityId = Math.Max(0, oldAbilityId);
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    stage.LegacyActionKind = oldActionId == CharacterTestCharacterActionConfigSource.DefaultEvadeActionId
                        ? CharacterTestRuntimeAiPlannerActionKind.Dodge
                        : CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack;
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                    stage.LegacyActionKind = LegacyActionFromMovementStrategy(oldMovementStrategy);
                    if (IsValidMoveBand(oldMoveMin, oldMoveMax))
                    {
                        stage.MoveMinDistance = oldMoveMin;
                        stage.MoveMaxDistance = oldMoveMax;
                    }
                    else
                    {
                        SetDefaultMoveBand(stage);
                    }
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.Wait:
                    stage.LegacyActionKind = CharacterTestRuntimeAiPlannerActionKind.Wait;
                    stage.WaitFrames = oldWait > 0
                        ? oldWait
                        : Math.Max(1, stage.PlannedDurationFrames > 0 ? stage.PlannedDurationFrames : 60);
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly:
                    stage.LegacyActionKind = CharacterTestRuntimeAiPlannerActionKind.Wait;
                    break;
            }
        }

        private static bool IsLegacyMovement(CharacterTestRuntimeAiPlannerActionKind value)
        {
            return value == CharacterTestRuntimeAiPlannerActionKind.MoveToTarget
                || value == CharacterTestRuntimeAiPlannerActionKind.Strafe
                || value == CharacterTestRuntimeAiPlannerActionKind.Retreat;
        }

        private static CharacterTestRuntimeAiLabMovementStrategy MovementStrategyFromLegacy(
            CharacterTestRuntimeAiPlannerActionKind value)
        {
            switch (value)
            {
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                    return CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked;
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    return CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose;
                default:
                    return CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget;
            }
        }

        private static CharacterTestRuntimeAiPlannerActionKind LegacyActionFromMovementStrategy(
            CharacterTestRuntimeAiLabMovementStrategy value)
        {
            switch (value)
            {
                case CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked:
                case CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget:
                    return CharacterTestRuntimeAiPlannerActionKind.Strafe;
                case CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard:
                case CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose:
                    return CharacterTestRuntimeAiPlannerActionKind.Retreat;
                default:
                    return CharacterTestRuntimeAiPlannerActionKind.MoveToTarget;
            }
        }

        private static void NormalizeLegacyActionKind(
            CharacterTestRuntimeAiLabLogicStage stage,
            CharacterTestRuntimeAiPlannerActionKind target)
        {
            CharacterTestRuntimeAiPlannerActionKind source = stage.LegacyActionKind;
            NormalizeLegacyActionKind(
                stage,
                target,
                source == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot
                    ? stage.AbilityId
                    : 0,
                source == CharacterTestRuntimeAiPlannerActionKind.MoveToTarget
                    ? stage.MoveMinDistance
                    : -1f,
                source == CharacterTestRuntimeAiPlannerActionKind.MoveToTarget
                    ? stage.MoveMaxDistance
                    : -1f);
        }

        private static void NormalizeLegacyActionKind(
            CharacterTestRuntimeAiLabLogicStage stage,
            CharacterTestRuntimeAiPlannerActionKind target,
            int oldAbilityId,
            float oldMoveMin,
            float oldMoveMax)
        {
            ResetActionParameters(stage);
            stage.LegacyActionKind = target;
            if (target == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot)
                stage.AbilityId = Math.Max(0, oldAbilityId);
            else if (target == CharacterTestRuntimeAiPlannerActionKind.MoveToTarget)
            {
                if (IsValidMoveBand(oldMoveMin, oldMoveMax))
                {
                    stage.MoveMinDistance = oldMoveMin;
                    stage.MoveMaxDistance = oldMoveMax;
                }
                else
                {
                    SetDefaultMoveBand(stage);
                }
            }
        }

        private static void ResetActionParameters(CharacterTestRuntimeAiLabLogicStage stage)
        {
            stage.AbilityId = 0;
            stage.CharacterActionId = CharacterActionId.None;
            stage.LegacyActionKind = CharacterTestRuntimeAiPlannerActionKind.Wait;
            stage.MovementStrategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget;
            stage.MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget;
            stage.MoveTargetPositionX = 0f;
            stage.MoveTargetPositionZ = 0f;
            stage.MoveTargetEntityId = 0;
            stage.MoveMinDistance = 0f;
            stage.MoveMaxDistance = 0f;
            stage.WaitFrames = 0;
        }

        private static void SetDefaultMoveBand(CharacterTestRuntimeAiLabLogicStage stage)
        {
            stage.MoveMinDistance = 0f;
            stage.MoveMaxDistance = 1.5f;
        }

        private static bool UsesAbility(CharacterTestRuntimeAiLabLogicStage stage)
        {
            return stage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility;
        }

        private static bool UsesMoveBand(CharacterTestRuntimeAiLabLogicStage stage)
        {
            return stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
        }

        private static bool IsValidMoveBand(float minimum, float maximum)
        {
            return minimum >= 0f && maximum >= minimum;
        }
    }
}

using System;
using System.Collections.Generic;
using MxFramework.AI;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Legacy direct-input wrapper kept for focused tests and compatibility.
    /// Runtime sessions use <see cref="CharacterTestRuntimeAiPlannerCommandModule"/> so AI input crosses the command boundary.
    /// </summary>
    public sealed class CharacterTestRuntimeAiPlannerControlSystem
    {
        public const int MoveToTargetActionId =
            CharacterTestAiBehaviorProfileSource.DefaultMoveToTargetActionId;
        public const int LightAttackActionId =
            CharacterTestAiBehaviorProfileSource.DefaultLightAttackActionId;

        private const float DefaultAttackRange = CharacterTestAiBehaviorProfileSource.DefaultAttackRange;

        private readonly CharacterTestRuntimeAiPlannerEvaluator _evaluator;

        public CharacterTestRuntimeAiPlannerControlSystem(
            float attackRange = DefaultAttackRange,
            IAiPlanner planner = null,
            ICharacterTestAiNavigationService navigationService = null,
            CharacterTestAiBehaviorProfile profile = null)
        {
            _ = planner;
            CharacterTestAiBehaviorProfile requestedProfile = profile
                ?? new CharacterTestAiBehaviorProfile(attackRange: attackRange);
            CharacterTestRuntimeAiPlannerProfile runtimeProfile =
                CharacterTestRuntimeAiPlannerProfileSource.CreateDefault(requestedProfile);
            if (!CharacterTestRuntimeAiPlannerCompiledProfile.TryCompile(
                    runtimeProfile,
                    out CharacterTestRuntimeAiPlannerCompiledProfile compiled,
                    out CharacterTestRuntimeAiPlannerValidationReport report))
            {
                throw new ArgumentException(report.ToText(), nameof(profile));
            }

            _evaluator = new CharacterTestRuntimeAiPlannerEvaluator(
                compiled,
                navigationService ?? CharacterTestDirectAiNavigationService.Instance);
        }

        /// <remarks>NoAlloc on the steady-state path once AI agents are registered and the agent-state list capacity is stable.</remarks>
        public void Tick(CharacterTestCharacterRuntimeWorld world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Control.Owner != CharacterTestCharacterControlOwner.AiControlled)
                    continue;
                if (!entity.BattleState.CanRunRuntimeAiPlanner)
                {
                    entity.SetInput(CharacterTestCharacterInputComponent.None);
                    continue;
                }

                if (!_evaluator.TryEvaluate(
                        entity,
                        entities,
                        RuntimeFrame.Zero,
                        out CharacterTestRuntimeAiPlannerDecision decision))
                {
                    continue;
                }

                ApplyDirectInput(entity, decision);
            }
        }

        private static void ApplyDirectInput(
            CharacterTestCharacterEntity entity,
            CharacterTestRuntimeAiPlannerDecision decision)
        {
            if (!decision.TargetExists || decision.SelectedActionId == 0)
            {
                entity.SetInput(CharacterTestCharacterInputComponent.None);
                return;
            }

            entity.SetInput(new CharacterTestCharacterInputComponent(
                decision.Move,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: decision.AttackPrimaryPressed,
                attackSecondaryPressed: false,
                debugSecondaryPressed: false,
                debugPrimaryPressed: false,
                worldMove: Vector2.zero,
                hasWorldMove: false,
                skillSlotPressedMask: decision.SkillSlotPressedMask,
                dodgePressed: decision.DodgePressed,
                controlIntentSourceId: CharacterTestRuntimeCommandSources.RuntimeAiPlanner));
        }
    }
}

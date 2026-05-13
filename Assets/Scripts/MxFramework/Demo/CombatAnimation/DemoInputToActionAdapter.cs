using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Input;
using UnityEngine;

namespace MxFramework.Demo.CombatAnimation
{
    public sealed class DemoInputToActionAdapter
    {
        private readonly InputCommandQueue _commands;
        private readonly CombatActionRunner _runner;
        private readonly CombatDemoPoseSource _poseSource;
        private readonly CombatEntityId _playerEntityId;
        private readonly List<InputCommand> _drained = new List<InputCommand>();

        public DemoInputToActionAdapter(
            InputCommandQueue commands,
            CombatActionRunner runner,
            CombatDemoPoseSource poseSource,
            CombatEntityId playerEntityId)
        {
            _commands = commands;
            _runner = runner;
            _poseSource = poseSource;
            _playerEntityId = playerEntityId;
        }

        public int LastStartedActionId { get; private set; }

        public void Tick(long frame, InputSnapshot snapshot, float deltaTime, float moveSpeed)
        {
            ApplyMove(snapshot.Move, deltaTime, moveSpeed);

            _drained.Clear();
            _commands.DrainForFrame(frame, _drained);
            for (int i = 0; i < _drained.Count; i++)
            {
                InputCommand command = _drained[i];
                if (command.Phase != InputCommandPhase.Pressed && command.Phase != InputCommandPhase.Performed)
                {
                    continue;
                }

                int actionId = GetActionId(command.Intent);
                if (actionId <= 0)
                {
                    continue;
                }

                ActionResult result = _runner.StartAction(_playerEntityId, actionId, new CombatFrame((int)frame));
                if (!result.Success)
                {
                    result = _runner.ForceStartAction(_playerEntityId, actionId, new CombatFrame((int)frame));
                }

                if (result.Success)
                {
                    LastStartedActionId = actionId;
                }
            }
        }

        private void ApplyMove(Vector2 move, float deltaTime, float moveSpeed)
        {
            Vector3 delta = new Vector3(move.x, 0f, move.y);
            if (delta.sqrMagnitude > 1f)
            {
                delta.Normalize();
            }

            if (delta.sqrMagnitude > 0.0001f)
            {
                _poseSource.Move(_playerEntityId, delta * Mathf.Max(0f, moveSpeed) * Mathf.Max(0f, deltaTime));
            }
        }

        private static int GetActionId(InputIntent intent)
        {
            switch (intent)
            {
                case InputIntent.DebugPrimary:
                case InputIntent.AttackPrimary:
                    return CombatAnimationDemoIds.LightAttackActionId;
                case InputIntent.DebugSecondary:
                case InputIntent.AttackSecondary:
                    return CombatAnimationDemoIds.HeavyAttackActionId;
                case InputIntent.Jump:
                case InputIntent.Dodge:
                    return CombatAnimationDemoIds.DodgeRollActionId;
                default:
                    return 0;
            }
        }
    }
}

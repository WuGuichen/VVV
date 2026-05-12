using System;
using UnityEngine;

namespace MxFramework.Input
{
    public readonly struct InputSnapshot : IEquatable<InputSnapshot>
    {
        public InputSnapshot(
            Vector2 move,
            Vector2 look,
            Vector2 navigate,
            Vector2 point,
            Vector2 scroll,
            float throttle,
            bool jumpPressed,
            bool jumpHeld,
            bool jumpReleased,
            bool attackPrimaryPressed,
            bool attackPrimaryHeld,
            bool attackSecondaryPressed,
            bool interactPressed,
            bool dodgePressed,
            bool sprintHeld,
            bool submitPressed,
            bool cancelPressed,
            bool pausePressed,
            bool debugTogglePressed,
            bool clickPressed = false,
            bool clickHeld = false,
            bool clickReleased = false,
            bool rightClickPressed = false,
            bool rightClickHeld = false,
            bool rightClickReleased = false,
            bool restartPressed = false,
            bool debugPrimaryPressed = false,
            bool debugSecondaryPressed = false,
            bool debugCyclePressed = false,
            bool debugStepPressed = false,
            bool toggleHudPressed = false,
            bool audioPrimaryPressed = false,
            bool audioSecondaryPressed = false)
        {
            Move = move;
            Look = look;
            Navigate = navigate;
            Point = point;
            Scroll = scroll;
            Throttle = throttle;
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            JumpReleased = jumpReleased;
            AttackPrimaryPressed = attackPrimaryPressed;
            AttackPrimaryHeld = attackPrimaryHeld;
            AttackSecondaryPressed = attackSecondaryPressed;
            InteractPressed = interactPressed;
            DodgePressed = dodgePressed;
            SprintHeld = sprintHeld;
            SubmitPressed = submitPressed;
            CancelPressed = cancelPressed;
            PausePressed = pausePressed;
            DebugTogglePressed = debugTogglePressed;
            ClickPressed = clickPressed;
            ClickHeld = clickHeld;
            ClickReleased = clickReleased;
            RightClickPressed = rightClickPressed;
            RightClickHeld = rightClickHeld;
            RightClickReleased = rightClickReleased;
            RestartPressed = restartPressed;
            DebugPrimaryPressed = debugPrimaryPressed;
            DebugSecondaryPressed = debugSecondaryPressed;
            DebugCyclePressed = debugCyclePressed;
            DebugStepPressed = debugStepPressed;
            ToggleHudPressed = toggleHudPressed;
            AudioPrimaryPressed = audioPrimaryPressed;
            AudioSecondaryPressed = audioSecondaryPressed;
        }

        public Vector2 Move { get; }
        public Vector2 Look { get; }
        public Vector2 Navigate { get; }
        public Vector2 Point { get; }
        public Vector2 Scroll { get; }
        public float Throttle { get; }

        public bool JumpPressed { get; }
        public bool JumpHeld { get; }
        public bool JumpReleased { get; }
        public bool AttackPrimaryPressed { get; }
        public bool AttackPrimaryHeld { get; }
        public bool AttackSecondaryPressed { get; }
        public bool InteractPressed { get; }
        public bool DodgePressed { get; }
        public bool SprintHeld { get; }
        public bool SubmitPressed { get; }
        public bool CancelPressed { get; }
        public bool PausePressed { get; }
        public bool DebugTogglePressed { get; }
        public bool ClickPressed { get; }
        public bool ClickHeld { get; }
        public bool ClickReleased { get; }
        public bool RightClickPressed { get; }
        public bool RightClickHeld { get; }
        public bool RightClickReleased { get; }
        public bool RestartPressed { get; }
        public bool DebugPrimaryPressed { get; }
        public bool DebugSecondaryPressed { get; }
        public bool DebugCyclePressed { get; }
        public bool DebugStepPressed { get; }
        public bool ToggleHudPressed { get; }
        public bool AudioPrimaryPressed { get; }
        public bool AudioSecondaryPressed { get; }

        public static InputSnapshot Empty => default;

        public bool Equals(InputSnapshot other)
        {
            return Move == other.Move
                && Look == other.Look
                && Navigate == other.Navigate
                && Point == other.Point
                && Scroll == other.Scroll
                && Throttle.Equals(other.Throttle)
                && JumpPressed == other.JumpPressed
                && JumpHeld == other.JumpHeld
                && JumpReleased == other.JumpReleased
                && AttackPrimaryPressed == other.AttackPrimaryPressed
                && AttackPrimaryHeld == other.AttackPrimaryHeld
                && AttackSecondaryPressed == other.AttackSecondaryPressed
                && InteractPressed == other.InteractPressed
                && DodgePressed == other.DodgePressed
                && SprintHeld == other.SprintHeld
                && SubmitPressed == other.SubmitPressed
                && CancelPressed == other.CancelPressed
                && PausePressed == other.PausePressed
                && DebugTogglePressed == other.DebugTogglePressed
                && ClickPressed == other.ClickPressed
                && ClickHeld == other.ClickHeld
                && ClickReleased == other.ClickReleased
                && RightClickPressed == other.RightClickPressed
                && RightClickHeld == other.RightClickHeld
                && RightClickReleased == other.RightClickReleased
                && RestartPressed == other.RestartPressed
                && DebugPrimaryPressed == other.DebugPrimaryPressed
                && DebugSecondaryPressed == other.DebugSecondaryPressed
                && DebugCyclePressed == other.DebugCyclePressed
                && DebugStepPressed == other.DebugStepPressed
                && ToggleHudPressed == other.ToggleHudPressed
                && AudioPrimaryPressed == other.AudioPrimaryPressed
                && AudioSecondaryPressed == other.AudioSecondaryPressed;
        }

        public override bool Equals(object obj)
        {
            return obj is InputSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Move.GetHashCode();
                hashCode = (hashCode * 397) ^ Look.GetHashCode();
                hashCode = (hashCode * 397) ^ Navigate.GetHashCode();
                hashCode = (hashCode * 397) ^ Point.GetHashCode();
                hashCode = (hashCode * 397) ^ Scroll.GetHashCode();
                hashCode = (hashCode * 397) ^ Throttle.GetHashCode();
                hashCode = (hashCode * 397) ^ JumpPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ JumpHeld.GetHashCode();
                hashCode = (hashCode * 397) ^ JumpReleased.GetHashCode();
                hashCode = (hashCode * 397) ^ AttackPrimaryPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ AttackPrimaryHeld.GetHashCode();
                hashCode = (hashCode * 397) ^ AttackSecondaryPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ InteractPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ DodgePressed.GetHashCode();
                hashCode = (hashCode * 397) ^ SprintHeld.GetHashCode();
                hashCode = (hashCode * 397) ^ SubmitPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ CancelPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ PausePressed.GetHashCode();
                hashCode = (hashCode * 397) ^ DebugTogglePressed.GetHashCode();
                hashCode = (hashCode * 397) ^ ClickPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ ClickHeld.GetHashCode();
                hashCode = (hashCode * 397) ^ ClickReleased.GetHashCode();
                hashCode = (hashCode * 397) ^ RightClickPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ RightClickHeld.GetHashCode();
                hashCode = (hashCode * 397) ^ RightClickReleased.GetHashCode();
                hashCode = (hashCode * 397) ^ RestartPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ DebugPrimaryPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ DebugSecondaryPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ DebugCyclePressed.GetHashCode();
                hashCode = (hashCode * 397) ^ DebugStepPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ ToggleHudPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ AudioPrimaryPressed.GetHashCode();
                hashCode = (hashCode * 397) ^ AudioSecondaryPressed.GetHashCode();
                return hashCode;
            }
        }
    }
}

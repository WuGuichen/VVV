using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestCharacterInputComponent
    {
        private const int ActiveMoveEpsilonMilliSquared = 1000;

        public CharacterTestCharacterInputComponent(
            Vector2 move,
            bool jumpPressed,
            bool jumpHeld = false,
            bool jumpReleased = false,
            bool attackPrimaryPressed = false,
            bool attackSecondaryPressed = false,
            bool debugSecondaryPressed = false,
            bool debugPrimaryPressed = false,
            Vector2 worldMove = default,
            bool hasWorldMove = false,
            int skillSlotPressedMask = 0,
            int abilitySlotPressed = 0,
            int abilityIdPressed = 0,
            bool dodgePressed = false,
            int movementSpeedScaleMilli = CharacterTestControlIntent.DefaultMovementSpeedScaleMilli,
            bool turnInPlace = false,
            int turnInPlaceYawMilliDegrees = 0,
            int controlIntentSourceId = 0,
            CharacterTestBehaviorControlInput behaviorControl = default)
        {
            Vector2 clampedMove = turnInPlace
                ? Vector2.zero
                : Vector2.ClampMagnitude(move, 1f);
            Vector2 clampedWorldMove = turnInPlace
                ? Vector2.zero
                : Vector2.ClampMagnitude(worldMove, 1f);
            MoveXMilli = CharacterTestDeterministicUnits.AxisToMilli(clampedMove.x);
            MoveYMilli = CharacterTestDeterministicUnits.AxisToMilli(clampedMove.y);
            WorldMoveXMilli = CharacterTestDeterministicUnits.AxisToMilli(clampedWorldMove.x);
            WorldMoveYMilli = CharacterTestDeterministicUnits.AxisToMilli(clampedWorldMove.y);
            HasWorldMove = turnInPlace ? false : hasWorldMove;
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            JumpReleased = jumpReleased;
            AttackPrimaryPressed = turnInPlace ? false : attackPrimaryPressed;
            AttackSecondaryPressed = turnInPlace ? false : attackSecondaryPressed;
            DebugPrimaryPressed = debugPrimaryPressed;
            DebugSecondaryPressed = debugSecondaryPressed;
            SkillSlotPressedMask = skillSlotPressedMask & CharacterTestControlIntent.SkillSlotMaskBits;
            DodgePressed = dodgePressed;
            MovementSpeedScaleMilli = CharacterTestControlIntent.NormalizeMovementSpeedScaleMilli(
                movementSpeedScaleMilli);
            TurnInPlace = turnInPlace;
            TurnInPlaceYawMilliDegrees = CharacterTestDeterministicUnits.NormalizeMilliDegrees(
                turnInPlaceYawMilliDegrees);
            ControlIntentSourceId = controlIntentSourceId;
            BehaviorControl = behaviorControl;
            AbilitySlotPressed = CharacterTestAbilityHotbarRuntime.IsValidSlot(abilitySlotPressed)
                ? abilitySlotPressed
                : 0;
            AbilityIdPressed = AbilitySlotPressed > 0 && abilityIdPressed > 0 ? abilityIdPressed : 0;
        }

        private CharacterTestCharacterInputComponent(
            short moveXMilli,
            short moveYMilli,
            short worldMoveXMilli,
            short worldMoveYMilli,
            bool hasWorldMove,
            bool jumpPressed,
            bool jumpHeld,
            bool jumpReleased,
            bool attackPrimaryPressed,
            bool attackSecondaryPressed,
            bool debugSecondaryPressed,
            bool debugPrimaryPressed,
            int skillSlotPressedMask,
            bool dodgePressed = false,
            int movementSpeedScaleMilli = CharacterTestControlIntent.DefaultMovementSpeedScaleMilli,
            bool turnInPlace = false,
            int turnInPlaceYawMilliDegrees = 0,
            int controlIntentSourceId = 0,
            CharacterTestBehaviorControlInput behaviorControl = default)
        {
            MoveXMilli = turnInPlace ? (short)0 : ClampAxisMilli(moveXMilli);
            MoveYMilli = turnInPlace ? (short)0 : ClampAxisMilli(moveYMilli);
            WorldMoveXMilli = turnInPlace ? (short)0 : ClampAxisMilli(worldMoveXMilli);
            WorldMoveYMilli = turnInPlace ? (short)0 : ClampAxisMilli(worldMoveYMilli);
            HasWorldMove = turnInPlace ? false : hasWorldMove;
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            JumpReleased = jumpReleased;
            AttackPrimaryPressed = turnInPlace ? false : attackPrimaryPressed;
            AttackSecondaryPressed = turnInPlace ? false : attackSecondaryPressed;
            DebugPrimaryPressed = debugPrimaryPressed;
            DebugSecondaryPressed = debugSecondaryPressed;
            SkillSlotPressedMask = skillSlotPressedMask & CharacterTestControlIntent.SkillSlotMaskBits;
            DodgePressed = dodgePressed;
            MovementSpeedScaleMilli = CharacterTestControlIntent.NormalizeMovementSpeedScaleMilli(
                movementSpeedScaleMilli);
            TurnInPlace = turnInPlace;
            TurnInPlaceYawMilliDegrees = CharacterTestDeterministicUnits.NormalizeMilliDegrees(
                turnInPlaceYawMilliDegrees);
            ControlIntentSourceId = controlIntentSourceId;
            BehaviorControl = behaviorControl;
            AbilitySlotPressed = 0;
            AbilityIdPressed = 0;
        }

        public short MoveXMilli { get; }
        public short MoveYMilli { get; }
        public short WorldMoveXMilli { get; }
        public short WorldMoveYMilli { get; }
        public Vector2 Move => new Vector2(
            CharacterTestDeterministicUnits.MilliToAxis(MoveXMilli),
            CharacterTestDeterministicUnits.MilliToAxis(MoveYMilli));
        public Vector2 WorldMove => new Vector2(
            CharacterTestDeterministicUnits.MilliToAxis(WorldMoveXMilli),
            CharacterTestDeterministicUnits.MilliToAxis(WorldMoveYMilli));
        public bool HasWorldMove { get; }
        /// <summary>Whether quantized movement input exceeds the locomotion activation threshold.</summary>
        /// <remarks>NoAlloc.</remarks>
        public bool HasActiveMove
        {
            get
            {
                if (TurnInPlace)
                    return false;

                int moveX = HasWorldMove ? WorldMoveXMilli : MoveXMilli;
                int moveY = HasWorldMove ? WorldMoveYMilli : MoveYMilli;
                return CharacterTestDeterministicMath.SqrMagnitude(moveX, moveY)
                    > ActiveMoveEpsilonMilliSquared;
            }
        }
        public bool JumpPressed { get; }
        public bool JumpHeld { get; }
        public bool JumpReleased { get; }
        public bool AttackPrimaryPressed { get; }
        public bool AttackSecondaryPressed { get; }
        public bool DebugPrimaryPressed { get; }
        public bool DebugSecondaryPressed { get; }
        public int SkillSlotPressedMask { get; }
        public bool DodgePressed { get; }
        public int MovementSpeedScaleMilli { get; }
        public int AbilitySlotPressed { get; }
        public int AbilityIdPressed { get; }
        public bool HasAbilitySlotCast => AbilitySlotPressed > 0 && AbilityIdPressed > 0;
        /// <summary>Explicit zero-translation facing command.</summary>
        public bool TurnInPlace { get; }
        public int TurnInPlaceYawMilliDegrees { get; }
        /// <summary>Source of the control command that supplied this input; zero is a direct/runtime-default source.</summary>
        public int ControlIntentSourceId { get; }
        public CharacterTestBehaviorControlInput BehaviorControl { get; }

        public CharacterTestCharacterInputComponent WithAbilitySlotCast(
            int abilitySlotPressed,
            int abilityIdPressed)
        {
            return new CharacterTestCharacterInputComponent(
                Move,
                JumpPressed,
                JumpHeld,
                JumpReleased,
                AttackPrimaryPressed,
                AttackSecondaryPressed,
                DebugSecondaryPressed,
                DebugPrimaryPressed,
                WorldMove,
                HasWorldMove,
                SkillSlotPressedMask,
                abilitySlotPressed,
                abilityIdPressed,
                DodgePressed,
                MovementSpeedScaleMilli,
                TurnInPlace,
                TurnInPlaceYawMilliDegrees,
                ControlIntentSourceId,
                behaviorControl: BehaviorControl);
        }

        public CharacterTestCharacterInputComponent WithoutAbilitySlotCast()
        {
            return new CharacterTestCharacterInputComponent(
                Move,
                JumpPressed,
                JumpHeld,
                JumpReleased,
                AttackPrimaryPressed,
                AttackSecondaryPressed,
                DebugSecondaryPressed,
                DebugPrimaryPressed,
                WorldMove,
                HasWorldMove,
                SkillSlotPressedMask,
                dodgePressed: DodgePressed,
                movementSpeedScaleMilli: MovementSpeedScaleMilli,
                turnInPlace: TurnInPlace,
                turnInPlaceYawMilliDegrees: TurnInPlaceYawMilliDegrees,
                controlIntentSourceId: ControlIntentSourceId,
                behaviorControl: BehaviorControl);
        }

        public CharacterTestCharacterInputComponent WithoutActionStartRequests()
        {
            return new CharacterTestCharacterInputComponent(
                Move,
                jumpPressed: false,
                JumpHeld,
                JumpReleased,
                attackPrimaryPressed: false,
                attackSecondaryPressed: false,
                DebugSecondaryPressed,
                DebugPrimaryPressed,
                WorldMove,
                HasWorldMove,
                dodgePressed: false,
                movementSpeedScaleMilli: MovementSpeedScaleMilli,
                turnInPlace: TurnInPlace,
                turnInPlaceYawMilliDegrees: TurnInPlaceYawMilliDegrees,
                controlIntentSourceId: ControlIntentSourceId,
                behaviorControl: BehaviorControl);
        }

        /// <summary>
        /// Consumes the one-tick TurnInPlace control while retaining all other
        /// input state. Motion calls this only after it has applied the
        /// quantized facing delta to the authoritative transform.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public CharacterTestCharacterInputComponent WithoutTurnInPlace()
        {
            return new CharacterTestCharacterInputComponent(
                Move,
                JumpPressed,
                JumpHeld,
                JumpReleased,
                AttackPrimaryPressed,
                AttackSecondaryPressed,
                DebugSecondaryPressed,
                DebugPrimaryPressed,
                WorldMove,
                HasWorldMove,
                SkillSlotPressedMask,
                abilitySlotPressed: AbilitySlotPressed,
                abilityIdPressed: AbilityIdPressed,
                dodgePressed: DodgePressed,
                movementSpeedScaleMilli: MovementSpeedScaleMilli,
                turnInPlace: false,
                turnInPlaceYawMilliDegrees: 0,
                controlIntentSourceId: ControlIntentSourceId,
                behaviorControl: BehaviorControl);
        }

        /// <summary>Releases owned movement/facing only; independent action requests remain intact.</summary>
        internal CharacterTestCharacterInputComponent WithoutOwnedBehaviorControl()
            => new CharacterTestCharacterInputComponent(Vector2.zero, JumpPressed, JumpHeld, JumpReleased,
                AttackPrimaryPressed, AttackSecondaryPressed, DebugSecondaryPressed, DebugPrimaryPressed,
                Vector2.zero, false, SkillSlotPressedMask, AbilitySlotPressed, AbilityIdPressed, DodgePressed);

        internal bool TryReleaseOwnedBehaviorControl(long ownerToken, int sourceId, out CharacterTestCharacterInputComponent input)
        {
            input = this;
            if (!BehaviorControl.IsPresent || BehaviorControl.OwnerToken != ownerToken || ControlIntentSourceId != sourceId) return false;
            input = WithoutOwnedBehaviorControl();
            return true;
        }

        public CharacterTestCharacterInputComponent WithBehaviorControl(CharacterTestBehaviorControlInput control)
        {
            return new CharacterTestCharacterInputComponent(Move, JumpPressed, JumpHeld, JumpReleased,
                AttackPrimaryPressed, AttackSecondaryPressed, DebugSecondaryPressed, DebugPrimaryPressed,
                WorldMove, HasWorldMove, SkillSlotPressedMask, AbilitySlotPressed, AbilityIdPressed,
                DodgePressed, MovementSpeedScaleMilli, TurnInPlace, TurnInPlaceYawMilliDegrees, ControlIntentSourceId, control);
        }

        public static CharacterTestCharacterInputComponent None => default;

        public static CharacterTestCharacterInputComponent FromDeterministic(
            short moveXMilli,
            short moveYMilli,
            short worldMoveXMilli,
            short worldMoveYMilli,
            bool hasWorldMove,
            bool jumpPressed,
            bool jumpHeld = false,
            bool jumpReleased = false,
            bool attackPrimaryPressed = false,
            bool attackSecondaryPressed = false,
            bool debugSecondaryPressed = false,
            bool debugPrimaryPressed = false,
            bool dodgePressed = false,
            int movementSpeedScaleMilli = CharacterTestControlIntent.DefaultMovementSpeedScaleMilli,
            bool turnInPlace = false,
            int turnInPlaceYawMilliDegrees = 0,
            int controlIntentSourceId = 0,
            CharacterTestBehaviorControlInput behaviorControl = default)
        {
            return new CharacterTestCharacterInputComponent(
                moveXMilli,
                moveYMilli,
                worldMoveXMilli,
                worldMoveYMilli,
                hasWorldMove,
                jumpPressed,
                jumpHeld,
                jumpReleased,
                attackPrimaryPressed,
                attackSecondaryPressed,
                debugSecondaryPressed,
                debugPrimaryPressed,
                0,
                dodgePressed,
                movementSpeedScaleMilli,
                turnInPlace,
                turnInPlaceYawMilliDegrees,
                controlIntentSourceId,
                behaviorControl);
        }

        internal static CharacterTestCharacterInputComponent FromDeterministicWithSkillSlotMask(
            short moveXMilli,
            short moveYMilli,
            short worldMoveXMilli,
            short worldMoveYMilli,
            bool hasWorldMove,
            bool jumpPressed,
            bool jumpHeld,
            bool jumpReleased,
            bool attackPrimaryPressed,
            bool attackSecondaryPressed,
            bool debugSecondaryPressed,
            bool debugPrimaryPressed,
            int skillSlotPressedMask,
            bool dodgePressed = false,
            int movementSpeedScaleMilli = CharacterTestControlIntent.DefaultMovementSpeedScaleMilli,
            bool turnInPlace = false,
            int turnInPlaceYawMilliDegrees = 0,
            int controlIntentSourceId = 0,
            CharacterTestBehaviorControlInput behaviorControl = default)
        {
            return new CharacterTestCharacterInputComponent(
                moveXMilli,
                moveYMilli,
                worldMoveXMilli,
                worldMoveYMilli,
                hasWorldMove,
                jumpPressed,
                jumpHeld,
                jumpReleased,
                attackPrimaryPressed,
                attackSecondaryPressed,
                debugSecondaryPressed,
                debugPrimaryPressed,
                skillSlotPressedMask,
                dodgePressed,
                movementSpeedScaleMilli,
                turnInPlace,
                turnInPlaceYawMilliDegrees,
                controlIntentSourceId,
                behaviorControl);
        }

        private static short ClampAxisMilli(short value)
        {
            if (value < CharacterTestDeterministicUnits.MinInputAxisMilli)
                return CharacterTestDeterministicUnits.MinInputAxisMilli;
            if (value > CharacterTestDeterministicUnits.MaxInputAxisMilli)
                return CharacterTestDeterministicUnits.MaxInputAxisMilli;
            return value;
        }
    }
}

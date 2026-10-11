using UnityEngine;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestBehaviorRecipeOrchestrationOperation
    {
        ReplaceCharacterAction = 1,
        ReplaceAbility = 2,
        TerminateForTimelineJump = 3,
        TerminateForReturnToPlanner = 4
    }

    public enum CharacterTestSurfaceProbeOperation
    {
        None = 0,
        ApplyImpulse = 1,
        ApplyHit = 2,
        ApplyPush = 3,
        Reset = 4
    }

    public readonly struct CharacterTestAbilitySlotCastRequest
    {
        public CharacterTestAbilitySlotCastRequest(
            int targetEntityId,
            int slotNumber,
            int abilityId,
            int schemaVersion)
        {
            TargetEntityId = targetEntityId;
            SlotNumber = slotNumber;
            AbilityId = abilityId;
            SchemaVersion = schemaVersion;
        }

        public int TargetEntityId { get; }
        public int SlotNumber { get; }
        public int AbilityId { get; }
        public int SchemaVersion { get; }
    }

    public readonly struct CharacterTestRuntimeAiBehaviorSelectionRequest
    {
        public CharacterTestRuntimeAiBehaviorSelectionRequest(
            int targetEntityId,
            int loadoutId,
            int variantId,
            int schemaVersion)
        {
            TargetEntityId = targetEntityId;
            LoadoutId = loadoutId;
            VariantId = variantId;
            SchemaVersion = schemaVersion;
        }

        public int TargetEntityId { get; }
        public int LoadoutId { get; }
        public int VariantId { get; }
        public int SchemaVersion { get; }
    }

    public readonly struct CharacterTestReviveRequest
    {
        public CharacterTestReviveRequest(
            int targetEntityId,
            int reviveHp,
            int schemaVersion)
        {
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            ReviveHp = reviveHp < 0 ? 0 : reviveHp;
            SchemaVersion = schemaVersion;
        }

        public int TargetEntityId { get; }
        public int ReviveHp { get; }
        public int SchemaVersion { get; }
    }

    public readonly struct CharacterTestDestroyCharacterRequest
    {
        public CharacterTestDestroyCharacterRequest(
            int targetEntityId,
            int sourceEntityId,
            int schemaVersion,
            RuntimeTraceIdentity traceId)
        {
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            SourceEntityId = sourceEntityId < 0 ? 0 : sourceEntityId;
            SchemaVersion = schemaVersion;
            TraceId = traceId;
        }

        public int TargetEntityId { get; }
        public int SourceEntityId { get; }
        public int SchemaVersion { get; }
        public RuntimeTraceIdentity TraceId { get; }
    }

    public readonly struct CharacterTestSurfaceProbeRequest
    {
        public CharacterTestSurfaceProbeRequest(
            int targetEntityId,
            CharacterTestSurfaceProbeOperation operation,
            int schemaVersion)
        {
            TargetEntityId = targetEntityId;
            Operation = operation;
            SchemaVersion = schemaVersion;
        }

        public int TargetEntityId { get; }
        public CharacterTestSurfaceProbeOperation Operation { get; }
        public int SchemaVersion { get; }
    }

    public readonly struct CharacterTestMovableNaturalObjectResetRequest
    {
        public CharacterTestMovableNaturalObjectResetRequest(int targetNumericId, int schemaVersion)
        {
            TargetNumericId = targetNumericId < 0 ? 0 : targetNumericId;
            SchemaVersion = schemaVersion;
        }

        public int TargetNumericId { get; }
        public int SchemaVersion { get; }
        public bool ResetAll => TargetNumericId == 0;
    }

    public readonly struct CharacterTestMovableNaturalObjectPotencyInfluenceRequest
    {
        public CharacterTestMovableNaturalObjectPotencyInfluenceRequest(
            int targetNumericId,
            CharacterTestGenesisPotencyInfluenceKind influenceKind,
            int schemaVersion)
        {
            TargetNumericId = targetNumericId < 0 ? 0 : targetNumericId;
            InfluenceKind = CharacterTestGenesisPotencyRuntime.NormalizeInfluenceKind(influenceKind);
            SchemaVersion = schemaVersion;
        }

        public int TargetNumericId { get; }
        public CharacterTestGenesisPotencyInfluenceKind InfluenceKind { get; }
        public int SchemaVersion { get; }
        public bool TargetsAll => TargetNumericId == 0;
    }

    public readonly struct CharacterTestControlIntent
    {
        public const int LegacySchemaVersion = 1;
        public const int WorldMoveSchemaVersion = 3;
        public const int DebugSecondarySchemaVersion = 4;
        public const int DebugPrimarySchemaVersion = 5;
        public const int SkillSlotSchemaVersion = 6;
        public const int DodgeSchemaVersion = 7;
        public const int MovementSpeedScaleSchemaVersion = 8;
        // TurnInPlace is carried in the optional command envelope. The payload
        // layout remains three legacy ints; the schema advances
        // so the optional TurnInPlace envelope can be paired explicitly while
        // readers continue to accept all older payload schema versions.
        public const int TurnInPlaceSchemaVersion = MovementSpeedScaleSchemaVersion + 1;
        public const int BehaviorControlSchemaVersion = TurnInPlaceSchemaVersion + 1;
        public const int SchemaVersion = BehaviorControlSchemaVersion;
        public const int DefaultMovementSpeedScaleMilli = 1000;
        public const int MinimumMovementSpeedScaleMilli = 100;
        public const int MaximumMovementSpeedScaleMilli = 1000;
        public const int JumpPressedFlag = 1 << 0;
        public const int JumpHeldFlag = 1 << 1;
        public const int JumpReleasedFlag = 1 << 2;
        public const int AttackPrimaryPressedFlag = 1 << 3;
        public const int AttackSecondaryPressedFlag = 1 << 4;
        public const int DebugSecondaryPressedFlag = 1 << 5;
        public const int DebugPrimaryPressedFlag = 1 << 6;
        public const int SkillSlotPressedMaskShift = 7;
        public const int SkillSlotCount = 9;
        public const int SkillSlotMaskBits = 0x1ff;
        public const int SkillSlotPressedFlags = SkillSlotMaskBits << SkillSlotPressedMaskShift;
        public const int DodgePressedFlag = 1 << 16;
        public const int MovementSpeedPercentShift = 17;
        public const int MovementSpeedPercentBits = 0x7f;
        public const int MovementSpeedPercentFlags = MovementSpeedPercentBits << MovementSpeedPercentShift;
        public const int KnownFlags =
            JumpPressedFlag
            | JumpHeldFlag
            | JumpReleasedFlag
            | AttackPrimaryPressedFlag
            | AttackSecondaryPressedFlag
            | DebugSecondaryPressedFlag
            | DebugPrimaryPressedFlag
            | SkillSlotPressedFlags
            | DodgePressedFlag
            | MovementSpeedPercentFlags;

        public CharacterTestControlIntent(
            int targetEntityId,
            Vector2 move,
            Vector2 worldMove,
            bool jumpPressed,
            bool jumpHeld,
            bool jumpReleased,
            bool attackPrimaryPressed,
            bool attackSecondaryPressed = false,
            bool debugSecondaryPressed = false,
            bool debugPrimaryPressed = false,
            int skillSlotPressedMask = 0,
            bool dodgePressed = false,
            float movementSpeedMultiplier = 1f,
            bool turnInPlace = false,
            int turnInPlaceYawMilliDegrees = 0)
        {
            TargetEntityId = targetEntityId;
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
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            JumpReleased = jumpReleased;
            AttackPrimaryPressed = turnInPlace ? false : attackPrimaryPressed;
            AttackSecondaryPressed = turnInPlace ? false : attackSecondaryPressed;
            DebugPrimaryPressed = debugPrimaryPressed;
            DebugSecondaryPressed = debugSecondaryPressed;
            SkillSlotPressedMask = skillSlotPressedMask & SkillSlotMaskBits;
            DodgePressed = dodgePressed;
            MovementSpeedScaleMilli = NormalizeMovementSpeedScaleMilli(
                CharacterTestDeterministicUnits.ScaleToMilli(movementSpeedMultiplier));
            TurnInPlace = turnInPlace;
            TurnInPlaceYawMilliDegrees = CharacterTestDeterministicUnits.NormalizeMilliDegrees(
                turnInPlaceYawMilliDegrees);
        }

        public int TargetEntityId { get; }
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
        public bool JumpPressed { get; }
        public bool JumpHeld { get; }
        public bool JumpReleased { get; }
        public bool AttackPrimaryPressed { get; }
        public bool AttackSecondaryPressed { get; }
        public bool DebugPrimaryPressed { get; }
        public bool DebugSecondaryPressed { get; }
        public int SkillSlotPressedMask { get; }
        public bool DodgePressed { get; }
        /// <summary>Final locomotion target-speed scale in deterministic milli units.</summary>
        public int MovementSpeedScaleMilli { get; }
        /// <summary>Explicit zero-translation facing command.</summary>
        public bool TurnInPlace { get; }
        public int TurnInPlaceYawMilliDegrees { get; }

        private CharacterTestControlIntent(
            int targetEntityId,
            short moveXMilli,
            short moveYMilli,
            short worldMoveXMilli,
            short worldMoveYMilli,
            bool jumpPressed,
            bool jumpHeld,
            bool jumpReleased,
            bool attackPrimaryPressed,
            bool attackSecondaryPressed,
            bool debugSecondaryPressed,
            bool debugPrimaryPressed,
            int skillSlotPressedMask = 0,
            bool dodgePressed = false,
            int movementSpeedScaleMilli = DefaultMovementSpeedScaleMilli,
            bool turnInPlace = false,
            int turnInPlaceYawMilliDegrees = 0)
        {
            TargetEntityId = targetEntityId;
            MoveXMilli = turnInPlace ? (short)0 : ClampAxisMilli(moveXMilli);
            MoveYMilli = turnInPlace ? (short)0 : ClampAxisMilli(moveYMilli);
            WorldMoveXMilli = turnInPlace ? (short)0 : ClampAxisMilli(worldMoveXMilli);
            WorldMoveYMilli = turnInPlace ? (short)0 : ClampAxisMilli(worldMoveYMilli);
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            JumpReleased = jumpReleased;
            AttackPrimaryPressed = turnInPlace ? false : attackPrimaryPressed;
            AttackSecondaryPressed = turnInPlace ? false : attackSecondaryPressed;
            DebugPrimaryPressed = debugPrimaryPressed;
            DebugSecondaryPressed = debugSecondaryPressed;
            SkillSlotPressedMask = skillSlotPressedMask & SkillSlotMaskBits;
            DodgePressed = dodgePressed;
            MovementSpeedScaleMilli = NormalizeMovementSpeedScaleMilli(movementSpeedScaleMilli);
            TurnInPlace = turnInPlace;
            TurnInPlaceYawMilliDegrees = CharacterTestDeterministicUnits.NormalizeMilliDegrees(
                turnInPlaceYawMilliDegrees);
        }

        public static CharacterTestControlIntent FromDeterministic(
            int targetEntityId,
            short moveXMilli,
            short moveYMilli,
            short worldMoveXMilli,
            short worldMoveYMilli,
            bool jumpPressed,
            bool jumpHeld,
            bool jumpReleased,
            bool attackPrimaryPressed,
            bool attackSecondaryPressed = false,
            bool debugSecondaryPressed = false,
            bool debugPrimaryPressed = false,
            int skillSlotPressedMask = 0,
            bool dodgePressed = false,
            int movementSpeedScaleMilli = DefaultMovementSpeedScaleMilli,
            bool turnInPlace = false,
            int turnInPlaceYawMilliDegrees = 0)
        {
            return new CharacterTestControlIntent(
                targetEntityId,
                moveXMilli,
                moveYMilli,
                worldMoveXMilli,
                worldMoveYMilli,
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
                turnInPlaceYawMilliDegrees);
        }

        internal static int NormalizeMovementSpeedScaleMilli(int value)
        {
            if (value <= 0)
                return DefaultMovementSpeedScaleMilli;
            if (value < MinimumMovementSpeedScaleMilli)
                return MinimumMovementSpeedScaleMilli;
            if (value > MaximumMovementSpeedScaleMilli)
                return MaximumMovementSpeedScaleMilli;
            return value;
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

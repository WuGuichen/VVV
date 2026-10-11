using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public static partial class CharacterTestRuntimeCommandFactory
    {
        internal const int SpawnCharacterEnvelopeTypeId = 901101;
        internal const int SpawnCharacterEnvelopeSchemaVersion = 1;
        public const int AbilitySlotCastSchemaVersion = 1;
        internal const int CharacterActionRequestSchemaVersion = 2;
        internal const int CharacterActionStartMarker = 1;
        internal const int TurnInPlaceEnvelopeTypeId = 901102;
        internal const int TurnInPlaceEnvelopeSchemaVersion = 1;
        internal const int BehaviorRecipeOrchestrationSchemaVersion = 1;
        public const int RuntimeAiBehaviorSelectionSchemaVersion = 1;
        public const int ReviveCharacterSchemaVersion = 1;
        public const int DestroyCharacterSchemaVersion = 1;
        public const int SurfaceProbeSchemaVersion = 1;
        public const int MovableNaturalObjectResetSchemaVersion = 1;
        public const int MovableNaturalObjectPotencyInfluenceSchemaVersion = 1;

        public static RuntimeCommand SpawnCharacter(
            RuntimeFrame frame,
            int sourceId,
            CharacterTestCharacterSpawnSpec spec,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.SpawnCharacter,
                targetId: spec.CharacterId,
                payload0: CharacterTestRuntimeCommandPayloadCodec.PackSpawnMetadata(
                    spec.TeamId,
                    spec.RuntimeAiPlannerProfileSlot),
                payload1: CharacterTestRuntimeCommandPayloadCodec.PackCoordinatePair(spec.X, spec.Z),
                payload2: CharacterTestRuntimeCommandPayloadCodec.PackYAndControlOwner(spec.Y, spec.ControlOwner),
                traceId: traceId,
                envelope: CreateSpawnCharacterEnvelope(spec.RuntimeAiBehaviorSelection));
        }

        public static RuntimeCommand SetCameraMode(
            RuntimeFrame frame,
            int sourceId,
            CharacterTestCameraMode mode,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.SetCameraMode,
                targetId: 0,
                payload0: (int)mode,
                traceId: traceId);
        }

        public static RuntimeCommand ToggleTargetLock(
            RuntimeFrame frame,
            int sourceId,
            int primaryEntityId,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.ToggleTargetLock,
                targetId: primaryEntityId,
                traceId: traceId);
        }

        public static RuntimeCommand ToggleTargetLockViewSwitch(
            RuntimeFrame frame,
            int sourceId,
            int primaryEntityId,
            int requestedTargetEntityId,
            Vector2 lookDirection,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.ToggleTargetLock,
                targetId: primaryEntityId,
                payload0: CharacterTestRuntimeCommandPayloadCodec.TargetLockToggleModeViewSwitch,
                payload1: requestedTargetEntityId,
                payload2: CharacterTestRuntimeCommandPayloadCodec.PackTargetLockLookDirection(
                    lookDirection.x,
                    lookDirection.y),
                traceId: traceId);
        }

        public static RuntimeCommand SetTargetLock(
            RuntimeFrame frame,
            int sourceId,
            bool enabled,
            int targetEntityId,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.SetTargetLock,
                targetId: targetEntityId,
                payload0: enabled ? 1 : 0,
                traceId: traceId);
        }

        public static RuntimeCommand SetControlIntent(
            RuntimeFrame frame,
            int sourceId,
            CharacterTestControlIntent intent,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.SetControlIntent,
                targetId: intent.TargetEntityId,
                payload0: CharacterTestRuntimeCommandPayloadCodec.PackControlMovePair(
                    intent.MoveXMilli,
                    intent.MoveYMilli),
                payload1: CharacterTestRuntimeCommandPayloadCodec.PackControlMovePair(
                    intent.WorldMoveXMilli,
                    intent.WorldMoveYMilli),
                payload2: CharacterTestRuntimeCommandPayloadCodec.PackControlIntentMetadata(
                    CharacterTestRuntimeCommandPayloadCodec.PackControlIntentFlags(
                        intent.JumpPressed,
                        intent.JumpHeld,
                        intent.JumpReleased,
                        intent.AttackPrimaryPressed,
                        intent.AttackSecondaryPressed,
                        intent.DebugSecondaryPressed,
                        intent.DebugPrimaryPressed,
                        intent.SkillSlotPressedMask,
                        intent.DodgePressed,
                        intent.MovementSpeedScaleMilli),
                    intent.TurnInPlace
                        ? CharacterTestControlIntent.TurnInPlaceSchemaVersion
                        : CharacterTestControlIntent.MovementSpeedScaleSchemaVersion),
                traceId: traceId,
                envelope: CreateTurnInPlaceEnvelope(intent));
        }

        public static RuntimeCommand CastAbilitySlot(
            RuntimeFrame frame,
            int sourceId,
            int targetEntityId,
            int slotNumber,
            int abilityId,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.CastAbilitySlot,
                targetId: targetEntityId,
                payload0: slotNumber,
                payload1: abilityId,
                payload2: AbilitySlotCastSchemaVersion,
                traceId: traceId);
        }

        internal static RuntimeCommand RequestCharacterAction(
            RuntimeFrame frame,
            int sourceId,
            int targetEntityId,
            int characterActionNumericId,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.RequestCharacterAction,
                targetId: targetEntityId,
                payload0: CharacterActionStartMarker,
                payload1: characterActionNumericId,
                payload2: CharacterActionRequestSchemaVersion,
                traceId: traceId);
        }

        internal static RuntimeCommand BehaviorRecipeOrchestration(
            RuntimeFrame frame,
            int sourceId,
            int targetEntityId,
            CharacterTestBehaviorRecipeOrchestrationOperation operation,
            int resourceId,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.BehaviorRecipeOrchestration,
                targetId: targetEntityId,
                payload0: (int)operation,
                payload1: resourceId,
                payload2: BehaviorRecipeOrchestrationSchemaVersion,
                traceId: traceId);
        }

        public static RuntimeCommand ReviveCharacter(
            RuntimeFrame frame,
            int sourceId,
            int targetEntityId,
            int reviveHp = 0,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.ReviveCharacter,
                targetId: targetEntityId,
                payload0: reviveHp < 0 ? 0 : reviveHp,
                payload1: ReviveCharacterSchemaVersion,
                payload2: 0,
                traceId: traceId);
        }

        public static RuntimeCommand DestroyCharacter(
            RuntimeFrame frame,
            int sourceId,
            int targetEntityId,
            int sourceEntityId = 0,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.DestroyCharacter,
                targetId: targetEntityId,
                payload0: sourceEntityId < 0 ? 0 : sourceEntityId,
                payload1: DestroyCharacterSchemaVersion,
                payload2: 0,
                traceId: traceId);
        }

        public static RuntimeCommand SetRuntimeAiBehaviorSelection(
            RuntimeFrame frame,
            int sourceId,
            int targetEntityId,
            int loadoutId,
            int variantId,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.SetRuntimeAiBehaviorSelection,
                targetId: targetEntityId,
                payload0: loadoutId,
                payload1: variantId,
                payload2: RuntimeAiBehaviorSelectionSchemaVersion,
                traceId: traceId);
        }

        public static RuntimeCommand ApplyAuthorityPosePreview(
            RuntimeFrame frame,
            int sourceId,
            int targetEntityId,
            CharacterTestProceduralEffectPreviewId effectId,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.ApplyAuthorityPosePreview,
                targetId: targetEntityId,
                payload0: (int)effectId,
                payload1: CharacterTestAuthorityPosePreviewRequest.SchemaVersion,
                traceId: traceId);
        }

        public static RuntimeCommand SurfaceProbe(
            RuntimeFrame frame,
            int sourceId,
            int targetEntityId,
            CharacterTestSurfaceProbeOperation operation,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.SurfaceProbe,
                targetId: targetEntityId,
                payload0: (int)operation,
                payload1: SurfaceProbeSchemaVersion,
                traceId: traceId);
        }

        public static RuntimeCommand MovableNaturalObjectReset(
            RuntimeFrame frame,
            int sourceId,
            int targetNumericId = 0,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.MovableNaturalObjectReset,
                targetId: targetNumericId < 0 ? 0 : targetNumericId,
                payload0: MovableNaturalObjectResetSchemaVersion,
                traceId: traceId);
        }

        public static RuntimeCommand MovableNaturalObjectPotencyInfluence(
            RuntimeFrame frame,
            int sourceId,
            int targetNumericId,
            CharacterTestGenesisPotencyInfluenceKind influenceKind,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.MovableNaturalObjectPotencyInfluence,
                targetId: targetNumericId < 0 ? 0 : targetNumericId,
                payload0: (int)CharacterTestGenesisPotencyRuntime.NormalizeInfluenceKind(influenceKind),
                payload1: MovableNaturalObjectPotencyInfluenceSchemaVersion,
                payload2: 0,
                traceId: traceId);
        }

        public static RuntimeCommand SetControlIntent(
            RuntimeFrame frame,
            int sourceId,
            int targetEntityId,
            Vector2 move,
            bool jumpPressed,
            bool jumpHeld,
            bool jumpReleased,
            bool attackPrimaryPressed,
            RuntimeTraceIdentity traceId = default)
        {
            return SetControlIntent(
                frame,
                sourceId,
                targetEntityId,
                move,
                jumpPressed,
                jumpHeld,
                jumpReleased,
                attackPrimaryPressed,
                attackSecondaryPressed: false,
                traceId: traceId);
        }

        public static RuntimeCommand SetControlIntent(
            RuntimeFrame frame,
            int sourceId,
            int targetEntityId,
            Vector2 move,
            bool jumpPressed,
            bool jumpHeld,
            bool jumpReleased,
            bool attackPrimaryPressed,
            bool attackSecondaryPressed,
            bool debugSecondaryPressed = false,
            bool debugPrimaryPressed = false,
            RuntimeTraceIdentity traceId = default,
            int skillSlotPressedMask = 0,
            bool dodgePressed = false,
            float movementSpeedMultiplier = 1f,
            bool turnInPlace = false,
            int turnInPlaceYawMilliDegrees = 0)
        {
            return SetControlIntent(
                frame,
                sourceId,
                targetEntityId,
                move,
                move,
                jumpPressed,
                jumpHeld,
                jumpReleased,
                attackPrimaryPressed,
                attackSecondaryPressed,
                debugSecondaryPressed,
                debugPrimaryPressed,
                traceId,
                skillSlotPressedMask,
                dodgePressed,
                movementSpeedMultiplier,
                turnInPlace,
                turnInPlaceYawMilliDegrees);
        }

        public static RuntimeCommand SetControlIntent(
            RuntimeFrame frame,
            int sourceId,
            int targetEntityId,
            Vector2 move,
            Vector2 worldMove,
            bool jumpPressed,
            bool jumpHeld,
            bool jumpReleased,
            bool attackPrimaryPressed,
            RuntimeTraceIdentity traceId = default)
        {
            return SetControlIntent(
                frame,
                sourceId,
                targetEntityId,
                move,
                worldMove,
                jumpPressed,
                jumpHeld,
                jumpReleased,
                attackPrimaryPressed,
                attackSecondaryPressed: false,
                traceId: traceId);
        }

        public static RuntimeCommand SetControlIntent(
            RuntimeFrame frame,
            int sourceId,
            int targetEntityId,
            Vector2 move,
            Vector2 worldMove,
            bool jumpPressed,
            bool jumpHeld,
            bool jumpReleased,
            bool attackPrimaryPressed,
            bool attackSecondaryPressed,
            bool debugSecondaryPressed = false,
            bool debugPrimaryPressed = false,
            RuntimeTraceIdentity traceId = default,
            int skillSlotPressedMask = 0,
            bool dodgePressed = false,
            float movementSpeedMultiplier = 1f,
            bool turnInPlace = false,
            int turnInPlaceYawMilliDegrees = 0)
        {
            return SetControlIntent(
                frame,
                sourceId,
                new CharacterTestControlIntent(
                    targetEntityId,
                    move,
                    worldMove,
                    jumpPressed,
                    jumpHeld,
                    jumpReleased,
                    attackPrimaryPressed,
                    attackSecondaryPressed,
                    debugSecondaryPressed,
                    debugPrimaryPressed,
                    skillSlotPressedMask,
                    dodgePressed,
                    movementSpeedMultiplier,
                    turnInPlace,
                    turnInPlaceYawMilliDegrees),
                traceId);
        }

        public static CharacterTestCharacterSpawnRequest DecodeSpawnCharacter(RuntimeCommand command)
        {
            CharacterTestRuntimeCommandPayloadCodec.UnpackCoordinatePairMillimeters(
                command.Payload1,
                out int xMillimeters,
                out int zMillimeters);
            CharacterTestRuntimeCommandPayloadCodec.UnpackYMillimetersAndControlOwner(
                command.Payload2,
                out int yMillimeters,
                out CharacterTestCharacterControlOwner owner);
            CharacterTestRuntimeCommandPayloadCodec.UnpackSpawnMetadata(
                command.Payload0,
                out int teamId,
                out int runtimeAiPlannerProfileSlot);
            CharacterTestRuntimeAiBehaviorSelection runtimeAiBehaviorSelection =
                DecodeSpawnCharacterBehaviorSelection(command.Envelope);

            return CharacterTestCharacterSpawnRequest.FromDeterministic(
                command.TargetId,
                teamId,
                xMillimeters,
                yMillimeters,
                zMillimeters,
                owner,
                runtimeAiPlannerProfileSlot,
                runtimeAiBehaviorSelection);
        }

        private static RuntimeCommandEnvelope CreateSpawnCharacterEnvelope(
            CharacterTestRuntimeAiBehaviorSelection selection)
        {
            if (!selection.HasSelection)
                return default;

            return new RuntimeCommandEnvelope(
                SpawnCharacterEnvelopeTypeId,
                SpawnCharacterEnvelopeSchemaVersion,
                RuntimeCommandProvenanceKind.Derived,
                data0: selection.LoadoutId,
                data1: selection.VariantId,
                data2: selection.CatalogHash,
                data3: selection.ProfileSlot);
        }

        private static CharacterTestRuntimeAiBehaviorSelection DecodeSpawnCharacterBehaviorSelection(
            RuntimeCommandEnvelope envelope)
        {
            if (!envelope.IsPresent
                || envelope.TypeId != SpawnCharacterEnvelopeTypeId
                || envelope.SchemaVersion != SpawnCharacterEnvelopeSchemaVersion)
            {
                return CharacterTestRuntimeAiBehaviorSelection.None;
            }

            return new CharacterTestRuntimeAiBehaviorSelection(
                (int)envelope.Data0,
                (int)envelope.Data1,
                envelope.Data2,
                (int)envelope.Data3);
        }

        public static CharacterTestControlIntent DecodeControlIntent(RuntimeCommand command)
        {
            CharacterTestRuntimeCommandPayloadCodec.UnpackControlMovePairMilli(
                command.Payload0,
                out short xMilli,
                out short yMilli);
            int schemaVersion = CharacterTestRuntimeCommandPayloadCodec.UnpackControlIntentSchemaVersion(
                command.Payload2);
            int flags;
            short worldXMilli;
            short worldYMilli;
            if (schemaVersion >= CharacterTestControlIntent.WorldMoveSchemaVersion
                && schemaVersion <= CharacterTestControlIntent.SchemaVersion)
            {
                flags = CharacterTestRuntimeCommandPayloadCodec.UnpackControlIntentFlags(command.Payload2);
                CharacterTestRuntimeCommandPayloadCodec.UnpackControlMovePairMilli(
                    command.Payload1,
                    out worldXMilli,
                    out worldYMilli);
            }
            else
            {
                flags = command.Payload1;
                worldXMilli = xMilli;
                worldYMilli = yMilli;
            }

            return CharacterTestControlIntent.FromDeterministic(
                command.TargetId,
                xMilli,
                yMilli,
                worldXMilli,
                worldYMilli,
                (flags & CharacterTestControlIntent.JumpPressedFlag) != 0,
                (flags & CharacterTestControlIntent.JumpHeldFlag) != 0,
                (flags & CharacterTestControlIntent.JumpReleasedFlag) != 0,
                (flags & CharacterTestControlIntent.AttackPrimaryPressedFlag) != 0,
                schemaVersion >= CharacterTestControlIntent.WorldMoveSchemaVersion
                    && (flags & CharacterTestControlIntent.AttackSecondaryPressedFlag) != 0,
                schemaVersion >= CharacterTestControlIntent.DebugSecondarySchemaVersion
                    && (flags & CharacterTestControlIntent.DebugSecondaryPressedFlag) != 0,
                schemaVersion >= CharacterTestControlIntent.DebugPrimarySchemaVersion
                    && (flags & CharacterTestControlIntent.DebugPrimaryPressedFlag) != 0,
                schemaVersion >= CharacterTestControlIntent.SkillSlotSchemaVersion
                    ? (flags >> CharacterTestControlIntent.SkillSlotPressedMaskShift)
                        & CharacterTestControlIntent.SkillSlotMaskBits
                    : 0,
                schemaVersion >= CharacterTestControlIntent.DodgeSchemaVersion
                    && (flags & CharacterTestControlIntent.DodgePressedFlag) != 0,
                CharacterTestRuntimeCommandPayloadCodec.UnpackControlIntentMovementSpeedScaleMilli(
                    command.Payload2),
                DecodeTurnInPlace(command.Envelope, out int turnYawMilliDegrees),
                turnYawMilliDegrees);
        }

        private static RuntimeCommandEnvelope CreateTurnInPlaceEnvelope(
            CharacterTestControlIntent intent)
        {
            if (!intent.TurnInPlace)
                return default;

            return new RuntimeCommandEnvelope(
                TurnInPlaceEnvelopeTypeId,
                TurnInPlaceEnvelopeSchemaVersion,
                RuntimeCommandProvenanceKind.Unspecified,
                data0: intent.TurnInPlaceYawMilliDegrees);
        }

        private static bool DecodeTurnInPlace(
            RuntimeCommandEnvelope envelope,
            out int yawMilliDegrees)
        {
            yawMilliDegrees = 0;
            if (!envelope.IsPresent
                || envelope.TypeId != TurnInPlaceEnvelopeTypeId
                || envelope.SchemaVersion != TurnInPlaceEnvelopeSchemaVersion)
            {
                return false;
            }

            if (envelope.Data0 < int.MinValue || envelope.Data0 > int.MaxValue)
                return false;
            yawMilliDegrees = CharacterTestDeterministicUnits.NormalizeMilliDegrees((int)envelope.Data0);
            return true;
        }

        public static CharacterTestAbilitySlotCastRequest DecodeAbilitySlotCast(RuntimeCommand command)
        {
            return new CharacterTestAbilitySlotCastRequest(
                command.TargetId,
                command.Payload0,
                command.Payload1,
                command.Payload2);
        }

        public static CharacterTestReviveRequest DecodeReviveCharacter(RuntimeCommand command)
        {
            return new CharacterTestReviveRequest(
                command.TargetId,
                command.Payload0,
                command.Payload1);
        }

        public static CharacterTestDestroyCharacterRequest DecodeDestroyCharacter(RuntimeCommand command)
        {
            return new CharacterTestDestroyCharacterRequest(
                command.TargetId,
                command.Payload0,
                command.Payload1,
                command.TraceId);
        }

        public static CharacterTestRuntimeAiBehaviorSelectionRequest DecodeRuntimeAiBehaviorSelection(
            RuntimeCommand command)
        {
            return new CharacterTestRuntimeAiBehaviorSelectionRequest(
                command.TargetId,
                command.Payload0,
                command.Payload1,
                command.Payload2);
        }

        public static CharacterTestAuthorityPosePreviewRequest DecodeAuthorityPosePreview(
            RuntimeCommand command)
        {
            return new CharacterTestAuthorityPosePreviewRequest(
                command.TargetId,
                (CharacterTestProceduralEffectPreviewId)command.Payload0,
                command.Payload1);
        }

        public static CharacterTestSurfaceProbeRequest DecodeSurfaceProbe(RuntimeCommand command)
        {
            return new CharacterTestSurfaceProbeRequest(
                command.TargetId,
                (CharacterTestSurfaceProbeOperation)command.Payload0,
                command.Payload1);
        }

        public static CharacterTestMovableNaturalObjectResetRequest DecodeMovableNaturalObjectReset(
            RuntimeCommand command)
        {
            return new CharacterTestMovableNaturalObjectResetRequest(
                command.TargetId,
                command.Payload0);
        }

        public static CharacterTestMovableNaturalObjectPotencyInfluenceRequest
            DecodeMovableNaturalObjectPotencyInfluence(RuntimeCommand command)
        {
            return new CharacterTestMovableNaturalObjectPotencyInfluenceRequest(
                command.TargetId,
                (CharacterTestGenesisPotencyInfluenceKind)command.Payload0,
                command.Payload1);
        }

    }
}

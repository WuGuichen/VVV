using System;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestRuntimeCommandRegistry
    {
        public static RuntimeCommandRegistry Create()
        {
            var registry = new RuntimeCommandRegistry();
            registry.Register(new RuntimeCommandDefinition(CharacterTestRuntimeCommandIds.PositionRecall, "CharacterTest.PositionRecall",
                new RuntimeCommandPayloadSchema("Generation- and battle-bound explicit record/return/cancel, schema 1.", CharacterTestRuntimeCommandFactory.IsPositionRecallValid)));
            registry.Register(new RuntimeCommandDefinition(CharacterTestRuntimeCommandIds.GroundPointTraction,
                "CharacterTest.GroundPointTraction", new RuntimeCommandPayloadSchema(
                    "Owned, generation-bound fixed ground anchor traction start/cancel, schema 1.",
                    CharacterTestRuntimeCommandFactory.IsGroundPointTractionValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.SpawnCharacter,
                "CharacterTest.SpawnCharacter",
                new RuntimeCommandPayloadSchema(
                    "targetId=characterId, payload0=packed teamId/runtimeAiPlannerProfileSlot, payload1=packed x/z, payload2=packed y/controlOwner.",
                    IsSpawnPayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.SetCameraMode,
                "CharacterTest.SetCameraMode",
                new RuntimeCommandPayloadSchema(
                    "payload0=CharacterTestCameraMode.",
                    IsCameraModePayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.ToggleTargetLock,
                "CharacterTest.ToggleTargetLock",
                new RuntimeCommandPayloadSchema(
                    "targetId=primaryEntityId, 0 means resolve local player. payload0=0 legacy toggle, payload0=1 view switch with payload1=requestedTargetEntityId and payload2=packed look direction.",
                    IsToggleTargetLockPayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.SetTargetLock,
                "CharacterTest.SetTargetLock",
                new RuntimeCommandPayloadSchema(
                    "payload0=enabled, targetId=targetEntityId.",
                    command => command.Payload0 == 0 || command.Payload0 == 1)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.SetControlIntent,
                "CharacterTest.SetControlIntent",
                new RuntimeCommandPayloadSchema(
                    "targetId=controlled entity id, payload0=packed input move x/y, payload1=packed world move x/y, payload2=packed schema/flags.",
                    IsControlIntentPayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.CastAbilitySlot,
                "CharacterTest.CastAbilitySlot",
                new RuntimeCommandPayloadSchema(
                    "targetId=controlled entity id, payload0=slot 1-9, payload1=abilityId, payload2=schemaVersion 1.",
                    IsAbilitySlotCastPayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.RequestCharacterAction,
                "CharacterTest.RequestCharacterAction",
                new RuntimeCommandPayloadSchema(
                    "targetId=controlled entity id, payload0=1 start marker, payload1=positive CharacterAction numeric id, payload2=schemaVersion 2.",
                    IsCharacterActionRequestPayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.BehaviorRecipeOrchestration,
                "CharacterTest.BehaviorRecipeOrchestration",
                new RuntimeCommandPayloadSchema(
                    "targetId=controlled entity id, payload0=orchestration operation, payload1=action/ability id or 0, payload2=schemaVersion 1.",
                    IsBehaviorRecipeOrchestrationPayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.ReviveCharacter,
                "CharacterTest.ReviveCharacter",
                new RuntimeCommandPayloadSchema(
                    "targetId=character entity id, payload0=revive hp or 0 default, payload1=schemaVersion 1, payload2=0.",
                    IsReviveCharacterPayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.DestroyCharacter,
                "CharacterTest.DestroyCharacter",
                new RuntimeCommandPayloadSchema(
                    "targetId=dead character entity id, payload0=source entity id or 0, payload1=schemaVersion 1, payload2=0.",
                    IsDestroyCharacterPayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.SetRuntimeAiBehaviorSelection,
                "CharacterTest.SetRuntimeAiBehaviorSelection",
                new RuntimeCommandPayloadSchema(
                    "targetId=controlled entity id, payload0=loadoutId, payload1=variantId, payload2=schemaVersion 1.",
                    IsRuntimeAiBehaviorSelectionPayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.ApplyAuthorityPosePreview,
                "CharacterTest.ApplyAuthorityPosePreview",
                new RuntimeCommandPayloadSchema(
                    "targetId=character entity id, payload0=CharacterTestProceduralEffectPreviewId, payload1=schemaVersion 1.",
                    IsAuthorityPosePreviewPayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.SurfaceProbe,
                "CharacterTest.SurfaceProbe",
                new RuntimeCommandPayloadSchema(
                    "targetId=character entity id, payload0=CharacterTestSurfaceProbeOperation, payload1=schemaVersion 1.",
                    IsSurfaceProbePayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.GenesisMovableObjectProbe,
                "CharacterTest.GenesisMovableObjectProbe",
                new RuntimeCommandPayloadSchema(
                    "payload0=CharacterTestGenesisMovableObjectProbeKind, payload1=CharacterTestGenesisForceOriginKind, payload2=schemaVersion 1.",
                    IsGenesisMovableObjectProbePayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.GenesisPotencyMechanicsProbe,
                "CharacterTest.GenesisPotencyMechanicsProbe",
                new RuntimeCommandPayloadSchema(
                    "payload0=CharacterTestGenesisPotencyMechanicsProbeKind, payload2=schemaVersion 1.",
                    IsGenesisPotencyMechanicsProbePayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.GenesisLivingBeingProbe,
                "CharacterTest.GenesisLivingBeingProbe",
                new RuntimeCommandPayloadSchema(
                    "payload0=CharacterTestGenesisLivingBeingProbeKind, payload2=schemaVersion 1.",
                    IsGenesisLivingBeingProbePayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesProbe,
                "CharacterTest.GenesisLivingNaturalAttributesProbe",
                new RuntimeCommandPayloadSchema(
                    "payload0=CharacterTestGenesisLivingNaturalAttributesProbeKind, payload2=schemaVersion 1.",
                    IsGenesisLivingNaturalAttributesProbePayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesRuntimeState,
                "CharacterTest.GenesisLivingNaturalAttributesRuntimeState",
                new RuntimeCommandPayloadSchema(
                    "targetId=character entity id, payload0=CharacterTestGenesisLivingNaturalAttributesRuntimeOperation, payload1=magnitude/default 0, payload2=schemaVersion 1.",
                    IsGenesisLivingNaturalAttributesRuntimeStatePayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.MovableNaturalObjectReset,
                "CharacterTest.MovableNaturalObjectReset",
                new RuntimeCommandPayloadSchema(
                    "targetId=0 reset all, targetId=numeric movable object id reset one, payload0=schemaVersion 1.",
                    IsMovableNaturalObjectResetPayloadValid)));
            registry.Register(new RuntimeCommandDefinition(
                CharacterTestRuntimeCommandIds.MovableNaturalObjectPotencyInfluence,
                "CharacterTest.MovableNaturalObjectPotencyInfluence",
                new RuntimeCommandPayloadSchema(
                    "targetId=0 apply to all, targetId=numeric movable object id apply to one, payload0=CharacterTestGenesisPotencyInfluenceKind, payload1=schemaVersion 1, payload2=0.",
                    IsMovableNaturalObjectPotencyInfluencePayloadValid)));
            return registry;
        }

        private static bool IsToggleTargetLockPayloadValid(RuntimeCommand command)
        {
            return command.Payload0 == CharacterTestRuntimeCommandPayloadCodec.TargetLockToggleModeLegacy
                || command.Payload0 == CharacterTestRuntimeCommandPayloadCodec.TargetLockToggleModeViewSwitch;
        }

        private static bool IsSpawnPayloadValid(RuntimeCommand command)
        {
            if (command.TargetId <= 0)
                return false;

            CharacterTestRuntimeCommandPayloadCodec.UnpackSpawnMetadata(
                command.Payload0,
                out int teamId,
                out _);
            if (teamId <= 0)
                return false;

            CharacterTestRuntimeCommandPayloadCodec.UnpackYAndControlOwner(
                command.Payload2,
                out _,
                out CharacterTestCharacterControlOwner owner);
            if (!CharacterTestRuntimeCommandPayloadCodec.IsKnownControlOwner(owner))
                return false;

            RuntimeCommandEnvelope envelope = command.Envelope;
            if (!envelope.IsPresent)
                return true;

            return envelope.TypeId == CharacterTestRuntimeCommandFactory.SpawnCharacterEnvelopeTypeId
                && envelope.SchemaVersion ==
                    CharacterTestRuntimeCommandFactory.SpawnCharacterEnvelopeSchemaVersion
                && envelope.Provenance == RuntimeCommandProvenanceKind.Derived
                && envelope.Data0 > 0
                && envelope.Data0 <= int.MaxValue
                && envelope.Data1 > 0
                && envelope.Data1 <= int.MaxValue
                && envelope.Data3 > 0
                && envelope.Data3 <= int.MaxValue;
        }

        private static bool IsCameraModePayloadValid(RuntimeCommand command)
        {
            return command.Payload0 == (int)CharacterTestCameraMode.ThirdPersonControlled
                || command.Payload0 == (int)CharacterTestCameraMode.MultiTargetAuto;
        }

        private static bool IsControlIntentPayloadValid(RuntimeCommand command)
        {
            if (command.TargetId <= 0)
                return false;

            RuntimeCommandEnvelope envelope = command.Envelope;
            if (command.Payload2 == CharacterTestControlIntent.LegacySchemaVersion)
            {
                return !envelope.IsPresent
                    && CharacterTestRuntimeCommandPayloadCodec.AreControlIntentFlagsKnown(command.Payload1);
            }

            int schemaVersion = CharacterTestRuntimeCommandPayloadCodec.UnpackControlIntentSchemaVersion(
                command.Payload2);
            int flags = CharacterTestRuntimeCommandPayloadCodec.UnpackControlIntentFlags(command.Payload2);
            if (schemaVersion < CharacterTestControlIntent.WorldMoveSchemaVersion
                || schemaVersion > CharacterTestControlIntent.SchemaVersion
                || !CharacterTestRuntimeCommandPayloadCodec.AreControlIntentFlagsKnown(flags))
            {
                return false;
            }

            if (!envelope.IsPresent)
                return true;

            if (schemaVersion < CharacterTestControlIntent.TurnInPlaceSchemaVersion)
                return false;

            if (schemaVersion == CharacterTestControlIntent.BehaviorControlSchemaVersion)
                return CharacterTestRuntimeCommandFactory.IsBehaviorControlEnvelopeValid(envelope);

            return envelope.TypeId == CharacterTestRuntimeCommandFactory.TurnInPlaceEnvelopeTypeId
                && envelope.SchemaVersion == CharacterTestRuntimeCommandFactory.TurnInPlaceEnvelopeSchemaVersion
                && envelope.Provenance == RuntimeCommandProvenanceKind.Unspecified
                && envelope.Data0 >= int.MinValue
                && envelope.Data0 <= int.MaxValue;
        }

        private static bool IsAbilitySlotCastPayloadValid(RuntimeCommand command)
        {
            return command.TargetId > 0
                && CharacterTestAbilityHotbarRuntime.IsValidSlot(command.Payload0)
                && command.Payload1 > 0
                && command.Payload2 == CharacterTestRuntimeCommandFactory.AbilitySlotCastSchemaVersion;
        }

        private static bool IsCharacterActionRequestPayloadValid(RuntimeCommand command)
        {
            if (command.TargetId <= 0
                || command.Payload2 != CharacterTestRuntimeCommandFactory.CharacterActionRequestSchemaVersion)
            {
                return false;
            }
            return command.Payload0 == CharacterTestRuntimeCommandFactory.CharacterActionStartMarker
                && command.Payload1 > 0;
        }

        private static bool IsBehaviorRecipeOrchestrationPayloadValid(RuntimeCommand command)
        {
            if (command.Payload2 == 3)
                return command.TargetId > 0 && command.SourceId == CharacterTestRuntimeCommandSources.RuntimeAiPlanner
                    && command.Payload0 == (int)CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction
                    && command.Payload1 > 0 && command.Envelope.TypeId == TacticalCard.TacticalFixedCardPaymentCommit.CommandEnvelopeType
                    && command.Envelope.SchemaVersion == 1 && command.Envelope.Provenance == RuntimeCommandProvenanceKind.Derived
                    && command.Envelope.Data0 != 0 && command.Envelope.Data1 >= 0 && command.Envelope.Data2 > 0
                    && command.Envelope.Data3 != 0 && command.Envelope.Data4 != 0 && command.Envelope.Data5 != 0
                    && command.Envelope.Data6 != 0 && command.Envelope.Data7 != 0;
            if (command.Payload2 == 2)
                return command.TargetId > 0 && command.Payload0 == (int)CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForReturnToPlanner
                    && command.Payload1 == 0 && command.Envelope.TypeId == CharacterTestRuntimeCommandFactory.BehaviorStopEnvelopeTypeId
                    && command.Envelope.SchemaVersion == 1 && command.Envelope.Provenance == RuntimeCommandProvenanceKind.Unspecified
                    && command.Envelope.Data0 > 0;
            if (command.TargetId <= 0 || command.Envelope.IsPresent
                || command.Payload2 != CharacterTestRuntimeCommandFactory.BehaviorRecipeOrchestrationSchemaVersion
                || !Enum.IsDefined(
                    typeof(CharacterTestBehaviorRecipeOrchestrationOperation),
                    command.Payload0))
            {
                return false;
            }
            var operation = (CharacterTestBehaviorRecipeOrchestrationOperation)command.Payload0;
            return operation == CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction
                || operation == CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceAbility
                    ? command.Payload1 > 0
                    : command.Payload1 == 0;
        }

        private static bool IsReviveCharacterPayloadValid(RuntimeCommand command)
        {
            return command.TargetId > 0
                && command.Payload0 >= 0
                && command.Payload1 == CharacterTestRuntimeCommandFactory.ReviveCharacterSchemaVersion
                && command.Payload2 == 0;
        }

        private static bool IsDestroyCharacterPayloadValid(RuntimeCommand command)
        {
            return command.TargetId > 0
                && command.Payload0 >= 0
                && command.Payload1 == CharacterTestRuntimeCommandFactory.DestroyCharacterSchemaVersion
                && command.Payload2 == 0;
        }

        private static bool IsRuntimeAiBehaviorSelectionPayloadValid(RuntimeCommand command)
        {
            return command.TargetId > 0
                && command.Payload0 > 0
                && command.Payload1 > 0
                && command.Payload2 ==
                    CharacterTestRuntimeCommandFactory.RuntimeAiBehaviorSelectionSchemaVersion;
        }

        private static bool IsAuthorityPosePreviewPayloadValid(RuntimeCommand command)
        {
            return command.TargetId > 0
                && command.Payload1 == CharacterTestAuthorityPosePreviewRequest.SchemaVersion
                && command.Payload0 >= (int)CharacterTestProceduralEffectPreviewId.LightSecondary
                && command.Payload0 <= (int)CharacterTestProceduralEffectPreviewId.SideSecondary;
        }

        private static bool IsSurfaceProbePayloadValid(RuntimeCommand command)
        {
            return command.TargetId > 0
                && command.Payload1 == CharacterTestRuntimeCommandFactory.SurfaceProbeSchemaVersion
                && command.Payload0 >= (int)CharacterTestSurfaceProbeOperation.ApplyImpulse
                && command.Payload0 <= (int)CharacterTestSurfaceProbeOperation.Reset;
        }

        private static bool IsGenesisMovableObjectProbePayloadValid(RuntimeCommand command)
        {
            return command.Payload2 == CharacterTestRuntimeCommandFactory.GenesisMovableObjectProbeSchemaVersion
                && command.Payload0 >= (int)CharacterTestGenesisMovableObjectProbeKind.ValidationSuite
                && command.Payload0 <= (int)CharacterTestGenesisMovableObjectProbeKind.ObjectContact
                && command.Payload1 >= (int)CharacterTestGenesisForceOriginKind.SelfWeight
                && command.Payload1 <= (int)CharacterTestGenesisForceOriginKind.InstantImpulse;
        }

        private static bool IsGenesisPotencyMechanicsProbePayloadValid(RuntimeCommand command)
        {
            return command.Payload2 == CharacterTestRuntimeCommandFactory.GenesisPotencyMechanicsProbeSchemaVersion
                && command.Payload0 >= (int)CharacterTestGenesisPotencyMechanicsProbeKind.ValidationSuite
                && command.Payload0 <= (int)CharacterTestGenesisPotencyMechanicsProbeKind.HammerStone;
        }

        private static bool IsGenesisLivingBeingProbePayloadValid(RuntimeCommand command)
        {
            return command.Payload2 == CharacterTestRuntimeCommandFactory.GenesisLivingBeingProbeSchemaVersion
                && command.Payload1 == 0
                && command.Payload0 >= (int)CharacterTestGenesisLivingBeingProbeKind.ValidationSuite
                && command.Payload0 <= (int)CharacterTestGenesisLivingBeingProbeKind.Inactive;
        }

        private static bool IsGenesisLivingNaturalAttributesProbePayloadValid(RuntimeCommand command)
        {
            return command.Payload2 ==
                    CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesProbeSchemaVersion
                && command.Payload1 == 0
                && command.Payload0 >= (int)CharacterTestGenesisLivingNaturalAttributesProbeKind.ValidationSuite
                && command.Payload0 <= (int)CharacterTestGenesisLivingNaturalAttributesProbeKind.InactiveMaterial;
        }

        private static bool IsGenesisLivingNaturalAttributesRuntimeStatePayloadValid(RuntimeCommand command)
        {
            return command.TargetId > 0
                && command.Payload2 ==
                    CharacterTestRuntimeCommandFactory.GenesisLivingNaturalAttributesRuntimeStateSchemaVersion
                && CharacterTestLivingNaturalAttributesRuntimeState.IsValidCommandMagnitude(command.Payload1)
                && CharacterTestLivingNaturalAttributesRuntimeState.IsValidOperation(
                    (CharacterTestGenesisLivingNaturalAttributesRuntimeOperation)command.Payload0);
        }

        private static bool IsMovableNaturalObjectResetPayloadValid(RuntimeCommand command)
        {
            return command.TargetId >= 0
                && command.Payload0 == CharacterTestRuntimeCommandFactory.MovableNaturalObjectResetSchemaVersion
                && command.Payload1 == 0
                && command.Payload2 == 0;
        }

        private static bool IsMovableNaturalObjectPotencyInfluencePayloadValid(RuntimeCommand command)
        {
            return command.TargetId >= 0
                && CharacterTestGenesisPotencyRuntime.IsCommandInfluenceKind(
                    (CharacterTestGenesisPotencyInfluenceKind)command.Payload0)
                && command.Payload1 ==
                    CharacterTestRuntimeCommandFactory.MovableNaturalObjectPotencyInfluenceSchemaVersion
                && command.Payload2 == 0;
        }
    }
}

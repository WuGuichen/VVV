using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestRuntimeEvent
    {
        public CharacterTestRuntimeEvent(
            RuntimeFrame frame,
            CharacterTestRuntimeEventType type,
            int commandId,
            int sourceId,
            int targetId,
            int entityId = 0,
            int payload0 = 0,
            int payload1 = 0,
            int payload2 = 0,
            string message = "")
        {
            Frame = frame;
            Type = type;
            CommandId = commandId;
            SourceId = sourceId;
            TargetId = targetId;
            EntityId = entityId;
            Payload0 = payload0;
            Payload1 = payload1;
            Payload2 = payload2;
            Message = message ?? string.Empty;
        }

        public RuntimeFrame Frame { get; }
        public CharacterTestRuntimeEventType Type { get; }
        public int CommandId { get; }
        public int SourceId { get; }
        public int TargetId { get; }
        public int EntityId { get; }
        public int Payload0 { get; }
        public int Payload1 { get; }
        public int Payload2 { get; }
        public string Message { get; }

        public override string ToString()
        {
            return "frame=" + Frame.Value
                + " type=" + Type
                + " command=" + CommandIdToString(CommandId)
                + " source=" + SourceId
                + " target=" + TargetId
                + " entity=" + EntityId
                + (string.IsNullOrWhiteSpace(Message) ? string.Empty : " message=" + Message);
        }

        private static string CommandIdToString(int commandId)
        {
            switch (commandId)
            {
                case CharacterTestRuntimeCommandIds.SpawnCharacter:
                    return "SpawnCharacter";
                case CharacterTestRuntimeCommandIds.SetCameraMode:
                    return "SetCameraMode";
                case CharacterTestRuntimeCommandIds.ToggleTargetLock:
                    return "ToggleTargetLock";
                case CharacterTestRuntimeCommandIds.SetTargetLock:
                    return "SetTargetLock";
                case CharacterTestRuntimeCommandIds.SetControlIntent:
                    return "SetControlIntent";
                case CharacterTestRuntimeCommandIds.CastAbilitySlot:
                    return "CastAbilitySlot";
                case CharacterTestRuntimeCommandIds.BehaviorRecipeOrchestration:
                    return "BehaviorRecipeOrchestration";
                case CharacterTestRuntimeCommandIds.RequestCharacterAction:
                    return "RequestCharacterAction";
                case CharacterTestRuntimeCommandIds.ReviveCharacter:
                    return "ReviveCharacter";
                case CharacterTestRuntimeCommandIds.DestroyCharacter:
                    return "DestroyCharacter";
                case CharacterTestRuntimeCommandIds.SetRuntimeAiBehaviorSelection:
                    return "SetRuntimeAiBehaviorSelection";
                case CharacterTestRuntimeCommandIds.ApplyAuthorityPosePreview:
                    return "ApplyAuthorityPosePreview";
                case CharacterTestRuntimeCommandIds.SurfaceProbe:
                    return "SurfaceProbe";
                case CharacterTestRuntimeCommandIds.GenesisMovableObjectProbe:
                    return "GenesisMovableObjectProbe";
                case CharacterTestRuntimeCommandIds.GenesisPotencyMechanicsProbe:
                    return "GenesisPotencyMechanicsProbe";
                case CharacterTestRuntimeCommandIds.GenesisLivingBeingProbe:
                    return "GenesisLivingBeingProbe";
                case CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesProbe:
                    return "GenesisLivingNaturalAttributesProbe";
                case CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesRuntimeState:
                    return "GenesisLivingNaturalAttributesRuntimeState";
                case CharacterTestRuntimeCommandIds.MovableNaturalObjectReset:
                    return "MovableNaturalObjectReset";
                case CharacterTestRuntimeCommandIds.MovableNaturalObjectPotencyInfluence:
                    return "MovableNaturalObjectPotencyInfluence";
                default:
                    return commandId.ToString();
            }
        }
    }
}

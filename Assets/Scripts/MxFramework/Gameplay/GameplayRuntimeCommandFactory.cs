using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public static class GameplayRuntimeCommandFactory
    {
        public static RuntimeCommand CastAbility(
            RuntimeFrame frame,
            int casterEntityId,
            int abilityId,
            int candidateEntityId = 0,
            int sourceId = 0,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayRuntimeCommandIds.CastAbility,
                targetId: casterEntityId,
                payload0: casterEntityId,
                payload1: abilityId,
                payload2: candidateEntityId,
                traceId: traceId);
        }

        public static RuntimeCommand DespawnEntity(
            RuntimeFrame frame,
            int entityId,
            int sourceId = 0,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayRuntimeCommandIds.DespawnEntity,
                targetId: entityId,
                payload0: entityId,
                traceId: traceId);
        }

        public static RuntimeCommand CreateComponentEntity(
            RuntimeFrame frame,
            int sourceId = 0,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayRuntimeCommandIds.CreateComponentEntity,
                targetId: 0,
                traceId: traceId);
        }

        public static RuntimeCommand DestroyComponentEntity(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            int sourceId = 0,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayRuntimeCommandIds.DestroyComponentEntity,
                targetId: entityId.Index,
                payload0: entityId.Index,
                payload1: entityId.Generation,
                traceId: traceId);
        }

        public static RuntimeCommand SpawnComponentEntity(
            RuntimeFrame frame,
            int spawnDefinitionId,
            int variantId = 0,
            int sourceId = 0,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayRuntimeCommandIds.SpawnComponentEntity,
                targetId: spawnDefinitionId,
                payload0: spawnDefinitionId,
                payload1: variantId,
                traceId: traceId);
        }

        public static RuntimeCommand SetComponentAttribute(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            int attributeId,
            int value,
            int sourceId = 0,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayRuntimeCommandIds.SetComponentAttribute,
                targetId: entityId.Index,
                payload0: entityId.Generation,
                payload1: attributeId,
                payload2: value,
                traceId: traceId);
        }

        public static RuntimeCommand AddComponentAttribute(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            int attributeId,
            int delta,
            int sourceId = 0,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayRuntimeCommandIds.AddComponentAttribute,
                targetId: entityId.Index,
                payload0: entityId.Generation,
                payload1: attributeId,
                payload2: delta,
                traceId: traceId);
        }

        public static RuntimeCommand CastComponentAbility(
            RuntimeFrame frame,
            GameplayEntityId casterEntityId,
            int abilityId,
            int sourceId = 0,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayRuntimeCommandIds.CastComponentAbility,
                targetId: casterEntityId.Index,
                payload0: casterEntityId.Generation,
                payload1: abilityId,
                payload2: 0,
                traceId: traceId);
        }

        public static RuntimeCommand CastComponentAbilityRequest(
            RuntimeFrame frame,
            GameplayComponentAbilityRequestHandle requestHandle,
            int abilityId,
            int sourceId = 0,
            string traceId = "")
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayRuntimeCommandIds.CastComponentAbilityRequest,
                targetId: requestHandle.Index,
                payload0: requestHandle.Index,
                payload1: requestHandle.Generation,
                payload2: abilityId,
                traceId: traceId);
        }
    }
}

using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Structured result for a runtime ability cast request.</summary>
    public readonly struct GameplayAbilityRuntimeResult
    {
        public readonly bool Success;
        public readonly GameplayAbilityRuntimeFailureCode FailureCode;
        public readonly string FailureReason;
        public readonly string TraceId;
        public readonly int CasterEntityId;
        public readonly int AbilityId;
        public readonly AbilityCastResult CastResult;
        public readonly IReadOnlyList<IRuntimeEntity> Candidates;
        public readonly IReadOnlyList<int> CandidateEntityIds;
        public readonly IReadOnlyList<int> TargetEntityIds;

        private GameplayAbilityRuntimeResult(
            bool success,
            GameplayAbilityRuntimeFailureCode failureCode,
            string failureReason,
            string traceId,
            int casterEntityId,
            int abilityId,
            AbilityCastResult castResult,
            IReadOnlyList<IRuntimeEntity> candidates,
            IReadOnlyList<int> candidateEntityIds,
            IReadOnlyList<int> targetEntityIds)
        {
            Success = success;
            FailureCode = failureCode;
            FailureReason = failureReason;
            TraceId = traceId;
            CasterEntityId = casterEntityId;
            AbilityId = abilityId;
            CastResult = castResult;
            Candidates = candidates ?? Array.Empty<IRuntimeEntity>();
            CandidateEntityIds = candidateEntityIds ?? Array.Empty<int>();
            TargetEntityIds = targetEntityIds ?? Array.Empty<int>();
        }

        public static GameplayAbilityRuntimeResult Fail(
            GameplayAbilityCastRequest request,
            GameplayAbilityRuntimeFailureCode failureCode,
            string failureReason,
            IReadOnlyList<IRuntimeEntity> candidates = null)
        {
            IReadOnlyList<IRuntimeEntity> candidateSnapshot = CopyEntities(candidates);
            return new GameplayAbilityRuntimeResult(
                success: false,
                failureCode: failureCode,
                failureReason: failureReason,
                traceId: request.TraceId,
                casterEntityId: request.CasterEntityId,
                abilityId: request.AbilityId,
                castResult: AbilityCastResult.Fail(failureReason),
                candidates: candidateSnapshot,
                candidateEntityIds: BuildEntityIds(candidateSnapshot),
                targetEntityIds: Array.Empty<int>());
        }

        public static GameplayAbilityRuntimeResult FromCast(
            GameplayAbilityCastRequest request,
            AbilityCastResult castResult,
            IReadOnlyList<IRuntimeEntity> candidates)
        {
            IReadOnlyList<IRuntimeEntity> candidateSnapshot = CopyEntities(candidates);
            IReadOnlyList<int> targetEntityIds = BuildEntityIds(castResult.Targets);
            bool success = castResult.Success;
            string failureReason = success ? null : castResult.FailureReason;

            return new GameplayAbilityRuntimeResult(
                success: success,
                failureCode: success ? GameplayAbilityRuntimeFailureCode.None : GameplayAbilityRuntimeFailureCode.AbilityCastFailed,
                failureReason: failureReason,
                traceId: request.TraceId,
                casterEntityId: request.CasterEntityId,
                abilityId: request.AbilityId,
                castResult: castResult,
                candidates: candidateSnapshot,
                candidateEntityIds: BuildEntityIds(candidateSnapshot),
                targetEntityIds: targetEntityIds);
        }

        private static IReadOnlyList<IRuntimeEntity> CopyEntities(IReadOnlyList<IRuntimeEntity> entities)
        {
            if (entities == null || entities.Count == 0)
                return Array.Empty<IRuntimeEntity>();

            IRuntimeEntity[] copy = new IRuntimeEntity[entities.Count];
            for (int i = 0; i < entities.Count; i++)
                copy[i] = entities[i];

            return copy;
        }

        private static IReadOnlyList<int> BuildEntityIds(IReadOnlyList<IRuntimeEntity> entities)
        {
            if (entities == null || entities.Count == 0)
                return Array.Empty<int>();

            int[] ids = new int[entities.Count];
            for (int i = 0; i < entities.Count; i++)
                ids[i] = entities[i] == null ? 0 : entities[i].EntityId;

            return ids;
        }
    }
}

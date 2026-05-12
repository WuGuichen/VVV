using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>World-level request for casting an ability by entity and ability ids.</summary>
    public readonly struct GameplayAbilityCastRequest
    {
        public readonly int CasterEntityId;
        public readonly int AbilityId;
        public readonly IReadOnlyList<int> CandidateEntityIds;
        public readonly string TraceId;

        public GameplayAbilityCastRequest(
            int casterEntityId,
            int abilityId,
            IReadOnlyList<int> candidateEntityIds = null,
            string traceId = null)
        {
            CasterEntityId = casterEntityId;
            AbilityId = abilityId;
            CandidateEntityIds = candidateEntityIds;
            TraceId = traceId;
        }

        public bool HasExplicitCandidateIds => CandidateEntityIds != null;
    }
}

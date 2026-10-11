using MxFramework.Combat.Core;

namespace MxFramework.Combat.Hit
{
    public interface IHitTargetStateResolver
    {
        HitTargetStateFlags ResolveTargetState(CombatEntityId targetId);
    }

    /// <summary>Optional per-candidate eligibility for transient target states.</summary>
    /// <remarks>NoAlloc. The resolver must not consume a hit, mutate authority, or
    /// infer missing motion from presentation. Ordinary target resolvers retain
    /// the existing target-only contract.</remarks>
    public interface IHitTargetStateCandidateResolver : IHitTargetStateResolver
    {
        HitTargetStateFlags ResolveTargetState(HitCandidate candidate);
    }
}

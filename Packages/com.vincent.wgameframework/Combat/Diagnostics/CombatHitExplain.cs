using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;

namespace MxFramework.Combat.Diagnostics
{
    public readonly struct CombatHitExplain : IComparable<CombatHitExplain>, IEquatable<CombatHitExplain>
    {
        public CombatHitExplain(HitResolveResult result, string reason)
        {
            Result = result;
            Reason = reason ?? string.Empty;
        }

        public HitResolveResult Result { get; }

        public string Reason { get; }

        public CombatHash AppendHash(CombatHash hash)
        {
            return hash
                .Add(Result.Frame)
                .Add(Result.AttackerId)
                .Add(Result.TargetId)
                .Add(Result.ActionId)
                .Add(Result.ActionInstanceId)
                .Add(Result.TraceId)
                .Add((int)Result.Kind)
                .Add(Result.Damage)
                .Add(Result.StaggerFrames)
                .Add(unchecked((ulong)Result.Knockback.X.RawValue))
                .Add(unchecked((ulong)Result.Knockback.Y.RawValue))
                .Add(unchecked((ulong)Result.Knockback.Z.RawValue));
        }

        public int CompareTo(CombatHitExplain other)
        {
            int compare = Result.Frame.CompareTo(other.Result.Frame);
            if (compare != 0)
            {
                return compare;
            }

            compare = Result.TargetId.CompareTo(other.Result.TargetId);
            if (compare != 0)
            {
                return compare;
            }

            compare = Result.TraceId.CompareTo(other.Result.TraceId);
            if (compare != 0)
            {
                return compare;
            }

            return ((int)Result.Kind).CompareTo((int)other.Result.Kind);
        }

        public bool Equals(CombatHitExplain other)
        {
            return Result.Equals(other.Result) && Reason == other.Reason;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatHitExplain other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Result.GetHashCode() * 397) ^ Reason.GetHashCode();
            }
        }
    }
}

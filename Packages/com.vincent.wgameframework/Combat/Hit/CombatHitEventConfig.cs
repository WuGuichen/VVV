using System;
using MxFramework.Combat.Damage;
using MxFramework.Runtime;

namespace MxFramework.Combat.Hit
{
    internal static class CombatHitBuffApplicationConfigHashFields
    {
        public const int BuffId = 1;
    }

    internal static class CombatHitDamageConfigHashFields
    {
        public const int DamageTypeId = 7;
        public const int BaseDamage = 1;
        public const int ImpactPower = 2;
        public const int LaunchPower = 3;
        public const int Tags = 4;
        public const int TagCount = 5;
        public const int TagDigest = 6;
        public const int TagValue = 8;
        public const int AnalysisCategory = 9;
        public const int SourceAttributeScales = 10;
    }

    internal static class CombatHitEventConfigHashFields
    {
        public const int TargetTypes = 1;
        public const int EnvironmentHitPolicy = 2;
        public const int MaxAcceptedHits = 3;
        public const int HitSameTargetPolicy = 4;
        public const int Damage = 5;
        public const int OnHitBuffs = 6;
        public const int BuffCount = 7;
        public const int Buff = 8;
        public const int StableEventId = 9;
    }

    /// <summary>
    /// Frozen integer field numbering for the V2 content hash. Definition identity is
    /// carried by numeric ids; labels remain diagnostic/content text for the v1
    /// accumulator path. Existing field numbers remain frozen and newly introduced
    /// numeric identity fields use appended slots.
    /// </summary>
    public readonly struct CombatHitBuffApplicationConfig : IComparable<CombatHitBuffApplicationConfig>, IEquatable<CombatHitBuffApplicationConfig>
    {
        public CombatHitBuffApplicationConfig(int buffId)
        {
            if (buffId <= 0)
                throw new ArgumentOutOfRangeException(nameof(buffId), "Combat hit buff id must be greater than zero.");

            BuffId = buffId;
        }

        public int BuffId { get; }

        public int CompareTo(CombatHitBuffApplicationConfig other)
        {
            return BuffId.CompareTo(other.BuffId);
        }

        public bool Equals(CombatHitBuffApplicationConfig other)
        {
            return BuffId == other.BuffId;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatHitBuffApplicationConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            return BuffId;
        }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddInt(prefix + ".buffId", BuffId);
        }
    }

    public readonly struct CombatHitDamageConfig : IEquatable<CombatHitDamageConfig>
    {
        public CombatHitDamageConfig(
            int damageTypeId = 0,
            int baseDamage = 0,
            int impactPower = 0,
            int[] tags = null,
            int analysisCategory = 0,
            int launchPower = 0,
            string damageTypeLabel = "",
            string analysisCategoryLabel = "",
            string[] tagLabels = null,
            DamageAttributeScale[] sourceAttributeScales = null)
        {
            DamageTypeId = damageTypeId;
            DamageTypeLabel = damageTypeLabel ?? string.Empty;
            AnalysisCategoryLabel = analysisCategoryLabel ?? string.Empty;
            TagLabels = NormalizeTagLabels(tagLabels);
            BaseDamage = baseDamage < 0 ? 0 : baseDamage;
            ImpactPower = impactPower < 0 ? 0 : impactPower;
            LaunchPower = launchPower < 0 ? 0 : launchPower;
            Tags = NormalizeTags(tags);
            AnalysisCategory = analysisCategory;
            SourceAttributeScales = sourceAttributeScales == null
                ? Array.Empty<DamageAttributeScale>()
                : (DamageAttributeScale[])sourceAttributeScales.Clone();
        }

        public string DamageTypeLabel { get; }
        public string AnalysisCategoryLabel { get; }
        public string[] TagLabels { get; }
        public int DamageTypeId { get; }
        public int BaseDamage { get; }
        public int ImpactPower { get; }
        public int LaunchPower { get; }
        public int[] Tags { get; }
        public int AnalysisCategory { get; }
        public DamageAttributeScale[] SourceAttributeScales { get; }
        public bool HasDamage =>
            DamageTypeId > 0
            || BaseDamage > 0
            || ImpactPower > 0
            || LaunchPower > 0
            || (Tags != null && Tags.Length > 0)
            || (SourceAttributeScales != null && SourceAttributeScales.Length > 0)
            || AnalysisCategory > 0;

        public bool Equals(CombatHitDamageConfig other)
        {
            return DamageTypeId == other.DamageTypeId
                && BaseDamage == other.BaseDamage
                && ImpactPower == other.ImpactPower
                && LaunchPower == other.LaunchPower
                && AnalysisCategory == other.AnalysisCategory
                && SequenceEquals(Tags, other.Tags)
                && SequenceEquals(SourceAttributeScales, other.SourceAttributeScales);
        }

        public override bool Equals(object obj) => obj is CombatHitDamageConfig other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = DamageTypeId;
                hash = (hash * 397) ^ BaseDamage;
                hash = (hash * 397) ^ ImpactPower;
                hash = (hash * 397) ^ LaunchPower;
                hash = (hash * 397) ^ AnalysisCategory;
                int[] tags = Tags ?? Array.Empty<int>();
                for (int i = 0; i < tags.Length; i++)
                    hash = (hash * 397) ^ tags[i];
                DamageAttributeScale[] scales = SourceAttributeScales ?? Array.Empty<DamageAttributeScale>();
                for (int i = 0; i < scales.Length; i++)
                    hash = (hash * 397) ^ scales[i].GetHashCode();
                return hash;
            }
        }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddStringStable(prefix + ".damageTypeId", DamageTypeLabel);
            accumulator.AddInt(prefix + ".baseDamage", BaseDamage);
            accumulator.AddInt(prefix + ".impactPower", ImpactPower);
            accumulator.AddInt(prefix + ".launchPower", LaunchPower);
            accumulator.AddStringStable(prefix + ".analysisCategory", AnalysisCategoryLabel);
            string[] tags = TagLabels ?? Array.Empty<string>();
            accumulator.AddInt(prefix + ".tags.count", tags.Length);
            for (int i = 0; i < tags.Length; i++)
                accumulator.AddStringStable(prefix + ".tags." + i, tags[i]);
            DamageAttributeScale[] scales = SourceAttributeScales ?? Array.Empty<DamageAttributeScale>();
            accumulator.AddInt(prefix + ".sourceAttributeScales.count", scales.Length);
            for (int i = 0; i < scales.Length; i++)
            {
                accumulator.AddInt(prefix + ".sourceAttributeScales." + i + ".attributeId", scales[i].AttributeId);
                accumulator.AddDoubleQuantized(prefix + ".sourceAttributeScales." + i + ".scale", scales[i].Scale, 1000000d);
            }
        }

        private static string[] NormalizeTagLabels(string[] tags)
        {
            if (tags == null || tags.Length == 0) return Array.Empty<string>();
            var normalized = new string[tags.Length]; int count = 0;
            for (int i = 0; i < tags.Length; i++)
            {
                string tag = tags[i];
                if (!string.IsNullOrWhiteSpace(tag)) normalized[count++] = tag.Trim();
            }
            if (count == 0) return Array.Empty<string>();
            Array.Resize(ref normalized, count); Array.Sort(normalized, StringComparer.Ordinal);
            int uniqueCount = 1;
            for (int i = 1; i < normalized.Length; i++)
                if (!string.Equals(normalized[i], normalized[uniqueCount - 1], StringComparison.Ordinal)) normalized[uniqueCount++] = normalized[i];
            if (uniqueCount != normalized.Length) Array.Resize(ref normalized, uniqueCount);
            return normalized;
        }

        private static int[] NormalizeTags(int[] tags)
        {
            if (tags == null || tags.Length == 0) return Array.Empty<int>();
            var normalized = new int[tags.Length]; int count = 0;
            for (int i = 0; i < tags.Length; i++)
                if (tags[i] > 0) normalized[count++] = tags[i];
            if (count == 0) return Array.Empty<int>();
            Array.Resize(ref normalized, count); Array.Sort(normalized);
            int uniqueCount = 1;
            for (int i = 1; i < normalized.Length; i++)
                if (normalized[i] != normalized[uniqueCount - 1]) normalized[uniqueCount++] = normalized[i];
            if (uniqueCount != normalized.Length) Array.Resize(ref normalized, uniqueCount);
            return normalized;
        }

        private static bool SequenceEquals(int[] left, int[] right)
        {
            left = left ?? Array.Empty<int>(); right = right ?? Array.Empty<int>();
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        private static bool SequenceEquals(DamageAttributeScale[] left, DamageAttributeScale[] right)
        {
            left = left ?? Array.Empty<DamageAttributeScale>();
            right = right ?? Array.Empty<DamageAttributeScale>();
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (!left[i].Equals(right[i])) return false;
            return true;
        }
    }

    [Flags]
    public enum CombatHitTargetTypes
    {
        None = 0,
        HostileCharacter = 1 << 0,
        FriendlyCharacter = 1 << 1,
        Self = 1 << 2,
        ThirdPartyCharacter = 1 << 3,
        NeutralCharacter = ThirdPartyCharacter,
        DamageableObject = 1 << 4,
        WorldWall = 1 << 5,
        WorldGround = 1 << 6,
        WorldStatic = 1 << 7,
    }

    public enum CombatEnvironmentHitPolicy
    {
        None = 0,
        BlockedPresentation = 1,
    }

    public enum CombatHitSameTargetPolicy
    {
        AllowRepeat = 0,
        OncePerEvent = 1,
    }

    public readonly struct CombatHitEventConfig : IEquatable<CombatHitEventConfig>
    {
        public static readonly CombatHitTargetTypes DefaultTargetTypes = CombatHitTargetTypes.HostileCharacter;

        public CombatHitEventConfig(
            CombatHitTargetTypes targetTypes,
            CombatEnvironmentHitPolicy environmentHitPolicy = CombatEnvironmentHitPolicy.None,
            int stableEventId = 0,
            int maxAcceptedHits = 1,
            CombatHitSameTargetPolicy hitSameTargetPolicy = CombatHitSameTargetPolicy.AllowRepeat,
            CombatHitDamageConfig damage = default,
            CombatHitBuffApplicationConfig[] onHitBuffs = null,
            string stableEventLabel = "")
        {
            if (!Enum.IsDefined(typeof(CombatEnvironmentHitPolicy), environmentHitPolicy))
                throw new ArgumentOutOfRangeException(nameof(environmentHitPolicy), "Combat environment hit policy is not supported.");
            if (!Enum.IsDefined(typeof(CombatHitSameTargetPolicy), hitSameTargetPolicy))
                throw new ArgumentOutOfRangeException(nameof(hitSameTargetPolicy), "Combat hit same-target policy is not supported.");

            TargetTypes = targetTypes;
            EnvironmentHitPolicy = environmentHitPolicy;
            StableEventId = stableEventId;
            StableEventLabel = stableEventLabel ?? string.Empty;
            MaxAcceptedHits = maxAcceptedHits <= 0 ? 1 : maxAcceptedHits;
            HitSameTargetPolicy = hitSameTargetPolicy;
            Damage = damage;
            OnHitBuffs = CopySortedOnHitBuffs(onHitBuffs);
        }

        public CombatHitTargetTypes TargetTypes { get; }
        public CombatHitTargetTypes EffectiveTargetTypes =>
            TargetTypes == CombatHitTargetTypes.None ? DefaultTargetTypes : TargetTypes;
        public CombatEnvironmentHitPolicy EnvironmentHitPolicy { get; }
        public string StableEventLabel { get; }
        public int StableEventId { get; }
        public int MaxAcceptedHits { get; }
        public CombatHitSameTargetPolicy HitSameTargetPolicy { get; }
        public CombatHitDamageConfig Damage { get; }
        public CombatHitBuffApplicationConfig[] OnHitBuffs { get; }
        public int OnHitBuffCount => OnHitBuffs == null ? 0 : OnHitBuffs.Length;
        public bool AllowsEnvironmentBlockedPresentation =>
            EnvironmentHitPolicy == CombatEnvironmentHitPolicy.BlockedPresentation
            && HasAnyWorldTarget(EffectiveTargetTypes);

        public CombatHitEventConfig WithDefaults(int stableEventId, string stableEventLabel = "")
        {
            return new CombatHitEventConfig(
                EffectiveTargetTypes,
                EnvironmentHitPolicy,
                StableEventId <= 0 ? stableEventId : StableEventId,
                MaxAcceptedHits <= 0 ? 1 : MaxAcceptedHits,
                HitSameTargetPolicy,
                Damage,
                OnHitBuffs, StableEventId <= 0 ? stableEventLabel : StableEventLabel);
        }

        public bool Equals(CombatHitEventConfig other)
        {
            return EffectiveTargetTypes == other.EffectiveTargetTypes
                && EnvironmentHitPolicy == other.EnvironmentHitPolicy
                && StableEventId == other.StableEventId
                && (MaxAcceptedHits <= 0 ? 1 : MaxAcceptedHits) == (other.MaxAcceptedHits <= 0 ? 1 : other.MaxAcceptedHits)
                && HitSameTargetPolicy == other.HitSameTargetPolicy
                && Damage.Equals(other.Damage)
                && SequenceEquals(OnHitBuffs, other.OnHitBuffs);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatHitEventConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)EffectiveTargetTypes;
                hash = (hash * 397) ^ (int)EnvironmentHitPolicy;
                hash = (hash * 397) ^ StableEventId;
                hash = (hash * 397) ^ (MaxAcceptedHits <= 0 ? 1 : MaxAcceptedHits);
                hash = (hash * 397) ^ (int)HitSameTargetPolicy;
                hash = (hash * 397) ^ Damage.GetHashCode();
                CombatHitBuffApplicationConfig[] onHitBuffs = OnHitBuffs ?? Array.Empty<CombatHitBuffApplicationConfig>();
                for (int i = 0; i < onHitBuffs.Length; i++)
                    hash = (hash * 397) ^ onHitBuffs[i].GetHashCode();
                return hash;
            }
        }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddInt(prefix + ".targetTypes", (int)EffectiveTargetTypes);
            accumulator.AddInt(prefix + ".environmentHitPolicy", (int)EnvironmentHitPolicy);
            accumulator.AddStringStable(prefix + ".stableEventId", StableEventLabel);
            accumulator.AddInt(prefix + ".maxAcceptedHits", MaxAcceptedHits <= 0 ? 1 : MaxAcceptedHits);
            accumulator.AddInt(prefix + ".hitSameTargetPolicy", (int)HitSameTargetPolicy);
            Damage.Contribute(accumulator, prefix + ".damage");
            CombatHitBuffApplicationConfig[] onHitBuffs = OnHitBuffs ?? Array.Empty<CombatHitBuffApplicationConfig>();
            accumulator.AddInt(prefix + ".onHitBuffs.count", onHitBuffs.Length);
            for (int i = 0; i < onHitBuffs.Length; i++)
                onHitBuffs[i].Contribute(accumulator, prefix + ".onHitBuffs." + i);
        }

        public static bool HasAnyWorldTarget(CombatHitTargetTypes targetTypes)
        {
            return (targetTypes & (CombatHitTargetTypes.WorldWall
                | CombatHitTargetTypes.WorldGround
                | CombatHitTargetTypes.WorldStatic)) != 0;
        }

        private static CombatHitBuffApplicationConfig[] CopySortedOnHitBuffs(
            CombatHitBuffApplicationConfig[] onHitBuffs)
        {
            if (onHitBuffs == null || onHitBuffs.Length == 0)
                return Array.Empty<CombatHitBuffApplicationConfig>();

            var copy = new CombatHitBuffApplicationConfig[onHitBuffs.Length];
            Array.Copy(onHitBuffs, copy, onHitBuffs.Length);
            Array.Sort(copy);
            for (int i = 1; i < copy.Length; i++)
            {
                if (copy[i].BuffId == copy[i - 1].BuffId)
                    throw new ArgumentException("Combat hit event cannot contain duplicate on-hit buff ids.", nameof(onHitBuffs));
            }

            return copy;
        }

        private static bool SequenceEquals(
            CombatHitBuffApplicationConfig[] left,
            CombatHitBuffApplicationConfig[] right)
        {
            left = left ?? Array.Empty<CombatHitBuffApplicationConfig>();
            right = right ?? Array.Empty<CombatHitBuffApplicationConfig>();
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (!left[i].Equals(right[i]))
                    return false;
            }

            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Combat.Projectiles
{
    public enum CombatProjectileSpawnOriginKind
    {
        Socket = 0,
    }

    public enum CombatProjectileAimKind
    {
        CasterForwardRange = 0,
        LockedTargetCenter = 1,
        LockedTargetOrCasterForwardRange = 2,
    }

    public sealed class CombatProjectileSpawnProfileRegistry
    {
        private readonly Dictionary<int, CombatProjectileSpawnProfile> _byId =
            new Dictionary<int, CombatProjectileSpawnProfile>();
        private bool _stableHashDirty = true;
        private long _stableHash;

        public CombatProjectileSpawnProfileRegistry()
        {
        }

        public CombatProjectileSpawnProfileRegistry(IEnumerable<CombatProjectileSpawnProfile> profiles)
        {
            if (profiles == null)
                return;

            foreach (CombatProjectileSpawnProfile profile in profiles)
            {
                if (!TryRegister(profile, out string failure))
                    throw new ArgumentException(failure, nameof(profiles));
            }
        }

        public int Count => _byId.Count;

        public bool TryRegister(CombatProjectileSpawnProfile profile, out string failure)
        {
            failure = string.Empty;
            if (profile == null)
            {
                failure = "Projectile spawn profile cannot be null.";
                return false;
            }

            if (_byId.ContainsKey(profile.ProfileId))
            {
                failure = "Duplicate projectile spawn profile id: " + profile.ProfileId + ".";
                return false;
            }

            _byId.Add(profile.ProfileId, profile);
            _stableHashDirty = true;
            return true;
        }

        public bool TryGet(int profileId, out CombatProjectileSpawnProfile profile)
        {
            if (profileId <= 0)
            {
                profile = null;
                return false;
            }

            return _byId.TryGetValue(profileId, out profile);
        }

        public CombatProjectileSpawnProfile[] CreateSnapshot()
        {
            var result = new CombatProjectileSpawnProfile[_byId.Count];
            int index = 0;
            foreach (KeyValuePair<int, CombatProjectileSpawnProfile> pair in _byId)
                result[index++] = pair.Value;

            Array.Sort(result, CombatProjectileSpawnProfileComparer.Instance);
            return result;
        }

        public long ComputeStableHash()
        {
            if (!_stableHashDirty)
                return _stableHash;

            var accumulator = new RuntimeContentHashAccumulator();
            CombatProjectileSpawnProfile[] profiles = CreateSnapshot();
            accumulator.AddInt("projectile.spawnProfile.count", profiles.Length);
            for (int i = 0; i < profiles.Length; i++)
                profiles[i].Contribute(accumulator, "projectile.spawnProfile." + i);

            _stableHash = accumulator.ToHash();
            _stableHashDirty = false;
            return _stableHash;
        }
    }

    public sealed class CombatProjectileSpawnProfile
    {
        public CombatProjectileSpawnProfile(
            int profileId,
            string stableId,
            int projectileDefinitionId,
            string projectileStableId,
            CombatProjectileSpawnOriginConfig origin,
            CombatProjectileAimConfig aim)
        {
            if (profileId <= 0)
                throw new ArgumentOutOfRangeException(nameof(profileId), "Projectile spawn profile id must be positive.");
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("Projectile spawn profile stable id cannot be null or empty.", nameof(stableId));
            if (projectileDefinitionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectileDefinitionId), "Projectile definition id must be positive.");
            if (string.IsNullOrWhiteSpace(projectileStableId))
                throw new ArgumentException("Projectile spawn profile projectile stable id cannot be null or empty.", nameof(projectileStableId));

            ProfileId = profileId;
            StableId = stableId;
            ProjectileDefinitionId = projectileDefinitionId;
            ProjectileStableId = projectileStableId;
            Origin = origin;
            Aim = aim;
        }

        public int ProfileId { get; }
        public string StableId { get; }
        public int ProjectileDefinitionId { get; }
        public string ProjectileStableId { get; }
        public CombatProjectileSpawnOriginConfig Origin { get; }
        public CombatProjectileAimConfig Aim { get; }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddStringStable(prefix + ".stableId", StableId);
            accumulator.AddStringStable(prefix + ".projectileStableId", ProjectileStableId);
            Origin.Contribute(accumulator, prefix + ".origin");
            Aim.Contribute(accumulator, prefix + ".aim");
        }
    }

    public readonly struct CombatProjectileSpawnOriginConfig : IEquatable<CombatProjectileSpawnOriginConfig>
    {
        public CombatProjectileSpawnOriginConfig(
            CombatProjectileSpawnOriginKind kind,
            int socketId,
            FixVector3 localOffset,
            FixVector3 fallbackRootOffset,
            string socketLabel = "")
        {
            if (!Enum.IsDefined(typeof(CombatProjectileSpawnOriginKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Projectile spawn origin kind is not supported.");
            if (kind == CombatProjectileSpawnOriginKind.Socket && socketId <= 0)
                throw new ArgumentOutOfRangeException(nameof(socketId), "Projectile socket origin requires a positive socket id.");

            Kind = kind;
            SocketId = socketId;
            SocketLabel = socketLabel ?? string.Empty;
            LocalOffset = localOffset;
            FallbackRootOffset = fallbackRootOffset;
        }

        public CombatProjectileSpawnOriginKind Kind { get; }
        public int SocketId { get; }
        public string SocketLabel { get; }
        public FixVector3 LocalOffset { get; }
        public FixVector3 FallbackRootOffset { get; }

        public bool Equals(CombatProjectileSpawnOriginConfig other)
        {
            return Kind == other.Kind
                && SocketId == other.SocketId
                && LocalOffset.Equals(other.LocalOffset)
                && FallbackRootOffset.Equals(other.FallbackRootOffset);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileSpawnOriginConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ SocketId;
                hash = (hash * 397) ^ LocalOffset.GetHashCode();
                hash = (hash * 397) ^ FallbackRootOffset.GetHashCode();
                return hash;
            }
        }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddInt(prefix + ".kind", (int)Kind);
            accumulator.AddStringStable(prefix + ".socketId", SocketLabel);
            AddVector(accumulator, prefix + ".localOffset", LocalOffset);
            AddVector(accumulator, prefix + ".fallbackRootOffset", FallbackRootOffset);
        }

        private static void AddVector(RuntimeContentHashAccumulator accumulator, string prefix, FixVector3 value)
        {
            accumulator.AddLong(prefix + ".x.raw", value.X.RawValue);
            accumulator.AddLong(prefix + ".y.raw", value.Y.RawValue);
            accumulator.AddLong(prefix + ".z.raw", value.Z.RawValue);
        }
    }

    public readonly struct CombatProjectileAimConfig : IEquatable<CombatProjectileAimConfig>
    {
        public CombatProjectileAimConfig(CombatProjectileAimKind kind)
            : this(kind, Fix64.Zero, CombatProjectileAimKind.CasterForwardRange, Fix64.Zero)
        {
        }

        public CombatProjectileAimConfig(
            CombatProjectileAimKind kind,
            Fix64 targetHeightOffset,
            CombatProjectileAimKind fallbackKind,
            Fix64 maxRange)
        {
            if (!Enum.IsDefined(typeof(CombatProjectileAimKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Projectile aim kind is not supported.");
            if (!Enum.IsDefined(typeof(CombatProjectileAimKind), fallbackKind))
                throw new ArgumentOutOfRangeException(nameof(fallbackKind), "Projectile aim fallback kind is not supported.");
            if (maxRange < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxRange), "Projectile aim max range cannot be negative.");
            if (kind != CombatProjectileAimKind.CasterForwardRange
                && fallbackKind != CombatProjectileAimKind.CasterForwardRange)
            {
                throw new ArgumentException(
                    "Projectile locked-target aim currently only supports CasterForwardRange fallback.",
                    nameof(fallbackKind));
            }

            Kind = kind;
            TargetHeightOffset = targetHeightOffset;
            FallbackKind = fallbackKind;
            MaxRange = maxRange;
        }

        public CombatProjectileAimKind Kind { get; }
        public Fix64 TargetHeightOffset { get; }
        public CombatProjectileAimKind FallbackKind { get; }
        public Fix64 MaxRange { get; }

        public bool Equals(CombatProjectileAimConfig other)
        {
            return Kind == other.Kind
                && TargetHeightOffset.Equals(other.TargetHeightOffset)
                && FallbackKind == other.FallbackKind
                && MaxRange.Equals(other.MaxRange);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileAimConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ TargetHeightOffset.GetHashCode();
                hash = (hash * 397) ^ (int)FallbackKind;
                hash = (hash * 397) ^ MaxRange.GetHashCode();
                return hash;
            }
        }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddInt(prefix + ".kind", (int)Kind);
            if (Kind == CombatProjectileAimKind.CasterForwardRange
                && TargetHeightOffset == Fix64.Zero
                && FallbackKind == CombatProjectileAimKind.CasterForwardRange
                && MaxRange == Fix64.Zero)
            {
                return;
            }

            accumulator.AddLong(prefix + ".targetHeightOffset.raw", TargetHeightOffset.RawValue);
            accumulator.AddInt(prefix + ".fallbackKind", (int)FallbackKind);
            accumulator.AddLong(prefix + ".maxRange.raw", MaxRange.RawValue);
        }
    }

    internal sealed class CombatProjectileSpawnProfileComparer : IComparer<CombatProjectileSpawnProfile>
    {
        public static readonly CombatProjectileSpawnProfileComparer Instance =
            new CombatProjectileSpawnProfileComparer();

        public int Compare(CombatProjectileSpawnProfile left, CombatProjectileSpawnProfile right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return string.CompareOrdinal(left.StableId, right.StableId);
        }
    }
}

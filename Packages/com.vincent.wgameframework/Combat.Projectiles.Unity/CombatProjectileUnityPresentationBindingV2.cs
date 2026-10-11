using System;
using MxFramework.Core.Math;
using MxFramework.Resources;

namespace MxFramework.Combat.Projectiles.Unity
{
    /// <summary>Fixed projectile lifecycle presentation slot index.</summary>
    public enum CombatProjectileUnityPresentationSlotKind : byte
    {
        Spawn = 0,
        FlightBody = 1,
        Trail = 2,
        DirectImpact = 3,
        AreaImpact = 4,
        BlockedImpact = 5,
        Expire = 6
    }

    /// <summary>Orientation policy constrained by a projectile lifecycle slot.</summary>
    public enum CombatProjectileUnityPresentationOrientation : byte
    {
        ProjectileForward = 0,
        WorldUp = 1,
        SurfaceNormal = 2,
        ImpactNormalOrWorldUp = 3
    }

    /// <summary>View-only flight/trail exit mode.</summary>
    public enum CombatProjectileUnityPresentationExitMode : byte
    {
        Complete = 0,
        Dissipate = 1,
        Immediate = 2
    }

    public readonly struct CombatProjectileUnityPresentationPose
    {
        public CombatProjectileUnityPresentationPose(FixVector3 position, FixVector3 forward)
        { Position = position; Forward = forward; }
        public FixVector3 Position { get; }
        public FixVector3 Forward { get; }
    }

    /// <summary>Shared deterministic pose projection used by projectile presenters and authoring trace observers.</summary>
    public static class CombatProjectileUnityPresentationPoseResolver
    {
        /// <summary>Resolves final slot position and forward from the formal orientation and local-offset contract.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static CombatProjectileUnityPresentationPose Resolve(
            CombatProjectileUnityPresentationOrientation orientation,
            FixVector3 localOffset,
            FixVector3 position,
            FixVector3 projectileForward,
            FixVector3 impactNormal)
        {
            FixVector3 forward = ResolveForward(orientation, projectileForward, impactNormal);
            FixVector3 up = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
            FixVector3 right = Cross(up, forward);
            if (!right.TryNormalize(out right))
                right = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
            FixVector3 basisUp = Cross(forward, right);
            FixVector3 resolvedPosition = position
                + right * localOffset.X
                + basisUp * localOffset.Y
                + forward * localOffset.Z;
            return new CombatProjectileUnityPresentationPose(resolvedPosition, forward);
        }

        private static FixVector3 ResolveForward(
            CombatProjectileUnityPresentationOrientation orientation,
            FixVector3 projectileForward,
            FixVector3 normal)
        {
            if (orientation == CombatProjectileUnityPresentationOrientation.WorldUp)
                return new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
            if (orientation == CombatProjectileUnityPresentationOrientation.SurfaceNormal)
                return normal.TryNormalize(out FixVector3 normalizedNormal) ? normalizedNormal : projectileForward;
            if (orientation == CombatProjectileUnityPresentationOrientation.ImpactNormalOrWorldUp)
                return normal.TryNormalize(out FixVector3 impactNormal) ? impactNormal : new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
            return projectileForward.TryNormalize(out FixVector3 normalizedForward)
                ? normalizedForward
                : new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
        }

        private static FixVector3 Cross(FixVector3 left, FixVector3 right) => new FixVector3(
            left.Y * right.Z - left.Z * right.Y,
            left.Z * right.X - left.X * right.Z,
            left.X * right.Y - left.Y * right.X);
    }

    /// <summary>Immutable configuration for one configured lifecycle slot.</summary>
    /// <remarks>NoAlloc: value lookup and property access allocate no managed memory.</remarks>
    public readonly struct CombatProjectileUnityPresentationSlot
    {
        public CombatProjectileUnityPresentationSlot(
            CombatProjectileUnityPresentationSlotKind kind,
            ResourceKey resource,
            CombatProjectileUnityPresentationOrientation orientation,
            FixVector3 localOffset,
            int lifetimeFramesOverride,
            int vfxDefinitionId = 0)
        {
            if (!resource.IsValid) throw new ArgumentException("Projectile presentation slot resource must be valid.", nameof(resource));
            if (!string.Equals(resource.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal)) throw new ArgumentException("Projectile presentation slot resource must be a GameObject.", nameof(resource));
            if (!Enum.IsDefined(typeof(CombatProjectileUnityPresentationSlotKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(CombatProjectileUnityPresentationOrientation), orientation) || !IsOrientationAllowed(kind, orientation)) throw new ArgumentException("Projectile presentation orientation is incompatible with its slot.", nameof(orientation));
            if (lifetimeFramesOverride < 0) throw new ArgumentOutOfRangeException(nameof(lifetimeFramesOverride));
            bool persistent = kind == CombatProjectileUnityPresentationSlotKind.FlightBody || kind == CombatProjectileUnityPresentationSlotKind.Trail;
            if (persistent && lifetimeFramesOverride != 0) throw new ArgumentException("Persistent projectile presentation slots cannot override lifetime.", nameof(lifetimeFramesOverride));
            if (!persistent && vfxDefinitionId <= 0) throw new ArgumentException("One-shot projectile presentation slots require a compact VfxDefinitionId.", nameof(vfxDefinitionId));
            Kind = kind;
            Resource = resource;
            Orientation = orientation;
            LocalOffset = localOffset;
            LifetimeFramesOverride = lifetimeFramesOverride;
            VfxDefinitionId = vfxDefinitionId < 0 ? 0 : vfxDefinitionId;
        }

        private static bool IsOrientationAllowed(CombatProjectileUnityPresentationSlotKind kind, CombatProjectileUnityPresentationOrientation orientation)
        {
            switch (kind)
            {
                case CombatProjectileUnityPresentationSlotKind.Spawn:
                case CombatProjectileUnityPresentationSlotKind.Expire:
                    return orientation == CombatProjectileUnityPresentationOrientation.ProjectileForward || orientation == CombatProjectileUnityPresentationOrientation.WorldUp;
                case CombatProjectileUnityPresentationSlotKind.FlightBody:
                case CombatProjectileUnityPresentationSlotKind.Trail:
                    return orientation == CombatProjectileUnityPresentationOrientation.ProjectileForward;
                case CombatProjectileUnityPresentationSlotKind.DirectImpact:
                    return orientation == CombatProjectileUnityPresentationOrientation.SurfaceNormal || orientation == CombatProjectileUnityPresentationOrientation.ProjectileForward;
                case CombatProjectileUnityPresentationSlotKind.AreaImpact:
                    return orientation == CombatProjectileUnityPresentationOrientation.ImpactNormalOrWorldUp || orientation == CombatProjectileUnityPresentationOrientation.WorldUp;
                case CombatProjectileUnityPresentationSlotKind.BlockedImpact:
                    return orientation == CombatProjectileUnityPresentationOrientation.SurfaceNormal;
                default:
                    return false;
            }
        }

        public CombatProjectileUnityPresentationSlotKind Kind { get; }
        public ResourceKey Resource { get; }
        public CombatProjectileUnityPresentationOrientation Orientation { get; }
        public FixVector3 LocalOffset { get; }
        public int LifetimeFramesOverride { get; }
        public int VfxDefinitionId { get; }
    }

    /// <summary>Immutable exit policy for persistent projectile presentation slots.</summary>
    /// <remarks>NoAlloc: value construction and property access allocate no managed memory.</remarks>
    public readonly struct CombatProjectileUnityPresentationExitPolicy
    {
        public CombatProjectileUnityPresentationExitPolicy(
            CombatProjectileUnityPresentationExitMode acceptedImpact,
            CombatProjectileUnityPresentationExitMode blockedImpact,
            CombatProjectileUnityPresentationExitMode expiration,
            CombatProjectileUnityPresentationExitMode externalRemoval,
            CombatProjectileUnityPresentationExitMode runtimeReset)
        {
            if (!IsNormalExit(acceptedImpact) || !IsNormalExit(blockedImpact) || !IsNormalExit(expiration) || !IsNormalExit(externalRemoval)) throw new ArgumentException("Ordinary projectile exits must be Complete or Dissipate.");
            if (runtimeReset != CombatProjectileUnityPresentationExitMode.Immediate) throw new ArgumentException("Projectile runtime reset exit must be Immediate.", nameof(runtimeReset));
            AcceptedImpact = acceptedImpact;
            BlockedImpact = blockedImpact;
            Expiration = expiration;
            ExternalRemoval = externalRemoval;
            RuntimeReset = runtimeReset;
        }

        private static bool IsNormalExit(CombatProjectileUnityPresentationExitMode value)
            => value == CombatProjectileUnityPresentationExitMode.Complete || value == CombatProjectileUnityPresentationExitMode.Dissipate;

        public CombatProjectileUnityPresentationExitMode AcceptedImpact { get; }
        public CombatProjectileUnityPresentationExitMode BlockedImpact { get; }
        public CombatProjectileUnityPresentationExitMode Expiration { get; }
        public CombatProjectileUnityPresentationExitMode ExternalRemoval { get; }
        public CombatProjectileUnityPresentationExitMode RuntimeReset { get; }

        public static CombatProjectileUnityPresentationExitPolicy Default => new CombatProjectileUnityPresentationExitPolicy(
            CombatProjectileUnityPresentationExitMode.Complete,
            CombatProjectileUnityPresentationExitMode.Dissipate,
            CombatProjectileUnityPresentationExitMode.Complete,
            CombatProjectileUnityPresentationExitMode.Dissipate,
            CombatProjectileUnityPresentationExitMode.Immediate);
    }

    /// <summary>Immutable projectile Presentation Binding v2.</summary>
    /// <remarks>LowFreqAlloc on construction for a fixed seven-slot copy; TryGetSlot is NoAlloc.</remarks>
    public class CombatProjectileUnityPresentationBindingV2
    {
        public const string DefaultViewNamePrefix = "ProjectileVFX";
        private const int SlotCount = 7;
        private readonly CombatProjectileUnityPresentationSlot[] slots = new CombatProjectileUnityPresentationSlot[SlotCount];
        private readonly bool[] configured = new bool[SlotCount];

        /// <summary>Creates an immutable v2 binding and copies the configured slots into fixed storage.</summary>
        /// <remarks>LowFreqAlloc: construction allocates two fixed seven-element arrays.</remarks>
        public CombatProjectileUnityPresentationBindingV2(
            int projectileDefinitionId,
            string viewNamePrefix,
            CombatProjectileUnityPresentationExitPolicy exitPolicy,
            params CombatProjectileUnityPresentationSlot[] configuredSlots)
        {
            if (projectileDefinitionId <= 0) throw new ArgumentOutOfRangeException(nameof(projectileDefinitionId));
            ProjectileDefinitionId = projectileDefinitionId;
            ViewNamePrefix = string.IsNullOrWhiteSpace(viewNamePrefix) ? DefaultViewNamePrefix : viewNamePrefix;
            ExitPolicy = exitPolicy;
            for (int i = 0; configuredSlots != null && i < configuredSlots.Length; i++)
            {
                CombatProjectileUnityPresentationSlot slot = configuredSlots[i];
                int index = (int)slot.Kind;
                if ((uint)index >= SlotCount || configured[index]) throw new ArgumentException("Projectile presentation slots must use unique known kinds.", nameof(configuredSlots));
                configured[index] = true;
                slots[index] = slot;
            }
        }

        /// <summary>Gets the deterministic projectile definition identifier.</summary>
        public int ProjectileDefinitionId { get; }
        /// <summary>Gets the view instance name prefix.</summary>
        public string ViewNamePrefix { get; }
        /// <summary>Gets the fixed exit policy.</summary>
        public CombatProjectileUnityPresentationExitPolicy ExitPolicy { get; }
        /// <summary>Gets the legacy-compatible flight resource, or an invalid key when absent.</summary>
        public ResourceKey FlightBodyResource => TryGetSlot(CombatProjectileUnityPresentationSlotKind.FlightBody, out CombatProjectileUnityPresentationSlot slot)
            ? slot.Resource
            : default;

        /// <summary>Looks up one fixed lifecycle slot.</summary>
        /// <remarks>NoAlloc: performs indexed fixed-array lookup only.</remarks>
        public bool TryGetSlot(CombatProjectileUnityPresentationSlotKind kind, out CombatProjectileUnityPresentationSlot slot)
        {
            int index = (int)kind;
            if ((uint)index >= SlotCount || !configured[index])
            {
                slot = default;
                return false;
            }
            slot = slots[index];
            return true;
        }
    }
}

using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public enum CombatSurfaceMaterialKind
    {
        Default = 0,
        HardSlick = 1,
        Rough = 2,
        Elastic = 3,
        BouncePad = 4
    }

    public readonly struct CombatSurfaceMaterial : IEquatable<CombatSurfaceMaterial>
    {
        public static readonly CombatSurfaceMaterial Default = new CombatSurfaceMaterial(
            CombatSurfaceMaterialKind.Default,
            Fix64.Zero,
            Fix64.Zero);

        public static readonly CombatSurfaceMaterial HardSlick = new CombatSurfaceMaterial(
            CombatSurfaceMaterialKind.HardSlick,
            Fix64.FromInt(3),
            Fix64.Zero);

        public static readonly CombatSurfaceMaterial Rough = new CombatSurfaceMaterial(
            CombatSurfaceMaterialKind.Rough,
            Fix64.FromInt(60),
            Fix64.Zero);

        public static readonly CombatSurfaceMaterial Elastic = new CombatSurfaceMaterial(
            CombatSurfaceMaterialKind.Elastic,
            Fix64.FromInt(20),
            Fix64.One);

        public static readonly CombatSurfaceMaterial BouncePad = new CombatSurfaceMaterial(
            CombatSurfaceMaterialKind.BouncePad,
            Fix64.FromInt(20),
            Fix64.Zero,
            Fix64.FromInt(4));

        public CombatSurfaceMaterial(
            CombatSurfaceMaterialKind kind,
            Fix64 friction,
            Fix64 elasticity)
            : this(
                kind,
                friction,
                elasticity,
                Fix64.Zero)
        {
        }

        public CombatSurfaceMaterial(
            CombatSurfaceMaterialKind kind,
            Fix64 friction,
            Fix64 elasticity,
            Fix64 fixedBounceHeight)
        {
            if (!IsDefinedKind(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Surface material kind is not supported.");
            }

            if (friction < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(friction), "Surface friction cannot be negative.");
            }

            if (elasticity < Fix64.Zero || elasticity > Fix64.One)
            {
                throw new ArgumentOutOfRangeException(nameof(elasticity), "Surface elasticity must be in range 0..1.");
            }

            if (fixedBounceHeight < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedBounceHeight), "Surface fixed bounce height cannot be negative.");
            }

            Kind = kind;
            Friction = friction;
            Elasticity = elasticity;
            FixedBounceHeight = fixedBounceHeight;
        }

        public CombatSurfaceMaterialKind Kind { get; }

        public Fix64 Friction { get; }

        public Fix64 Elasticity { get; }

        public Fix64 FixedBounceHeight { get; }

        public string StableId => GetStableId(Kind);

        public static CombatSurfaceMaterial FromFriction(Fix64 surfaceFriction)
        {
            return new CombatSurfaceMaterial(
                CombatSurfaceMaterialKind.Default,
                surfaceFriction,
                Fix64.Zero);
        }

        public bool Equals(CombatSurfaceMaterial other)
        {
            return Kind == other.Kind
                && Friction.Equals(other.Friction)
                && Elasticity.Equals(other.Elasticity)
                && FixedBounceHeight.Equals(other.FixedBounceHeight);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatSurfaceMaterial other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ Friction.GetHashCode();
                hash = (hash * 397) ^ Elasticity.GetHashCode();
                hash = (hash * 397) ^ FixedBounceHeight.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return StableId
                + " friction=" + Friction
                + " elasticity=" + Elasticity
                + " fixedBounceHeight=" + FixedBounceHeight;
        }

        private static bool IsDefinedKind(CombatSurfaceMaterialKind kind)
        {
            return kind == CombatSurfaceMaterialKind.Default
                || kind == CombatSurfaceMaterialKind.HardSlick
                || kind == CombatSurfaceMaterialKind.Rough
                || kind == CombatSurfaceMaterialKind.Elastic
                || kind == CombatSurfaceMaterialKind.BouncePad;
        }

        private static string GetStableId(CombatSurfaceMaterialKind kind)
        {
            switch (kind)
            {
                case CombatSurfaceMaterialKind.HardSlick:
                    return "hard_slick";
                case CombatSurfaceMaterialKind.Rough:
                    return "rough";
                case CombatSurfaceMaterialKind.Elastic:
                    return "elastic";
                case CombatSurfaceMaterialKind.BouncePad:
                    return "bounce_pad";
                default:
                    return "default";
            }
        }
    }

    public readonly struct CombatPhysicsAabbCollider : IEquatable<CombatPhysicsAabbCollider>
    {
        public CombatPhysicsAabbCollider(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            int layer,
            FixVector3 localMin,
            FixVector3 localMax,
            Fix64 surfaceFriction = default,
            int priority = 0)
            : this(
                bodyId,
                colliderId,
                layer,
                localMin,
                localMax,
                CombatSurfaceMaterial.FromFriction(surfaceFriction),
                priority)
        {
        }

        public CombatPhysicsAabbCollider(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            int layer,
            FixVector3 localMin,
            FixVector3 localMax,
            CombatSurfaceMaterial surfaceMaterial,
            int priority = 0)
        {
            if (bodyId.IsNone)
            {
                throw new ArgumentException("Combat physics collider body id cannot be none.", nameof(bodyId));
            }

            if (colliderId.IsNone)
            {
                throw new ArgumentException("Combat physics collider id cannot be none.", nameof(colliderId));
            }

            CombatPhysicsLayerMask.FromLayer(layer);
            if (localMin.X > localMax.X || localMin.Y > localMax.Y || localMin.Z > localMax.Z)
            {
                throw new ArgumentException("AABB collider local min cannot be greater than local max.");
            }

            BodyId = bodyId;
            ColliderId = colliderId;
            Layer = layer;
            LocalMin = localMin;
            LocalMax = localMax;
            SurfaceMaterial = surfaceMaterial;
            Priority = priority;
        }

        public CombatBodyId BodyId { get; }

        public CombatColliderId ColliderId { get; }

        public int Layer { get; }

        public FixVector3 LocalMin { get; }

        public FixVector3 LocalMax { get; }

        public CombatSurfaceMaterial SurfaceMaterial { get; }

        public int Priority { get; }

        public CombatSurfaceMaterialKind SurfaceMaterialKind => SurfaceMaterial.Kind;

        public string SurfaceMaterialStableId => SurfaceMaterial.StableId;

        public Fix64 SurfaceFriction => SurfaceMaterial.Friction;

        public Fix64 SurfaceElasticity => SurfaceMaterial.Elasticity;

        public Fix64 SurfaceFixedBounceHeight => SurfaceMaterial.FixedBounceHeight;

        public FixVector3 GetWorldMin(FixVector3 bodyPosition)
        {
            return bodyPosition + LocalMin;
        }

        public FixVector3 GetWorldMax(FixVector3 bodyPosition)
        {
            return bodyPosition + LocalMax;
        }

        public bool Equals(CombatPhysicsAabbCollider other)
        {
            return BodyId.Equals(other.BodyId)
                && ColliderId.Equals(other.ColliderId)
                && Layer == other.Layer
                && LocalMin.Equals(other.LocalMin)
                && LocalMax.Equals(other.LocalMax)
                && SurfaceMaterial.Equals(other.SurfaceMaterial)
                && Priority == other.Priority;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsAabbCollider other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BodyId.Value;
                hash = (hash * 397) ^ ColliderId.Value;
                hash = (hash * 397) ^ Layer;
                hash = (hash * 397) ^ LocalMin.GetHashCode();
                hash = (hash * 397) ^ LocalMax.GetHashCode();
                hash = (hash * 397) ^ SurfaceMaterial.GetHashCode();
                hash = (hash * 397) ^ Priority;
                return hash;
            }
        }
    }
}

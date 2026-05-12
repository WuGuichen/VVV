using System;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatPhysicsQueryFilter : IEquatable<CombatPhysicsQueryFilter>
    {
        public CombatPhysicsQueryFilter(
            bool includeSourceEntity,
            int ownerId = 0,
            int teamId = 0,
            int targetOwnerMask = 0,
            int targetTeamMask = 0)
        {
            if (ownerId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerId), "Combat physics owner id cannot be negative.");
            }

            if (teamId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(teamId), "Combat physics team id cannot be negative.");
            }

            IncludeSourceEntity = includeSourceEntity;
            OwnerId = ownerId;
            TeamId = teamId;
            TargetOwnerMask = targetOwnerMask;
            TargetTeamMask = targetTeamMask;
        }

        public bool IncludeSourceEntity { get; }

        public int OwnerId { get; }

        public int TeamId { get; }

        public int TargetOwnerMask { get; }

        public int TargetTeamMask { get; }

        public bool Equals(CombatPhysicsQueryFilter other)
        {
            return IncludeSourceEntity == other.IncludeSourceEntity
                && OwnerId == other.OwnerId
                && TeamId == other.TeamId
                && TargetOwnerMask == other.TargetOwnerMask
                && TargetTeamMask == other.TargetTeamMask;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsQueryFilter other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = IncludeSourceEntity ? 1 : 0;
                hash = (hash * 397) ^ OwnerId;
                hash = (hash * 397) ^ TeamId;
                hash = (hash * 397) ^ TargetOwnerMask;
                hash = (hash * 397) ^ TargetTeamMask;
                return hash;
            }
        }
    }

    public readonly struct CombatPhysicsQuery : IEquatable<CombatPhysicsQuery>
    {
        public CombatPhysicsQuery(CombatQueryHeader header, CombatPhysicsShape shape)
            : this(header, shape, default(CombatPhysicsQueryFilter))
        {
        }

        public CombatPhysicsQuery(
            CombatQueryHeader header,
            CombatPhysicsShape shape,
            CombatPhysicsQueryFilter filter)
        {
            if (!HeaderMatchesShape(header.Kind, shape.Kind))
            {
                throw new ArgumentException("Query header kind must match physics shape kind.", nameof(header));
            }

            Header = header;
            Shape = shape;
            Filter = filter;
        }

        public CombatQueryHeader Header { get; }

        public CombatPhysicsShape Shape { get; }

        public CombatPhysicsQueryFilter Filter { get; }

        public static CombatPhysicsQuery From(CombatRayQuery query)
        {
            return From(query, default(CombatPhysicsQueryFilter));
        }

        public static CombatPhysicsQuery From(CombatRayQuery query, CombatPhysicsQueryFilter filter)
        {
            return new CombatPhysicsQuery(
                query.Header,
                CombatPhysicsShape.Ray(query.Origin, query.Direction, query.Length),
                filter);
        }

        public static CombatPhysicsQuery From(CombatSphereQuery query)
        {
            return From(query, default(CombatPhysicsQueryFilter));
        }

        public static CombatPhysicsQuery From(CombatSphereQuery query, CombatPhysicsQueryFilter filter)
        {
            return new CombatPhysicsQuery(
                query.Header,
                CombatPhysicsShape.Sphere(query.Center, query.Radius),
                filter);
        }

        public static CombatPhysicsQuery From(CombatCapsuleQuery query)
        {
            return From(query, default(CombatPhysicsQueryFilter));
        }

        public static CombatPhysicsQuery From(CombatCapsuleQuery query, CombatPhysicsQueryFilter filter)
        {
            return new CombatPhysicsQuery(
                query.Header,
                CombatPhysicsShape.Capsule(query.PointA, query.PointB, query.Radius),
                filter);
        }

        public static CombatPhysicsQuery From(CombatAabbQuery query)
        {
            return From(query, default(CombatPhysicsQueryFilter));
        }

        public static CombatPhysicsQuery From(CombatAabbQuery query, CombatPhysicsQueryFilter filter)
        {
            return new CombatPhysicsQuery(
                query.Header,
                CombatPhysicsShape.Aabb(query.Min, query.Max),
                filter);
        }

        public static CombatPhysicsQuery From(CombatSectorQuery query)
        {
            return From(query, default(CombatPhysicsQueryFilter));
        }

        public static CombatPhysicsQuery From(CombatSectorQuery query, CombatPhysicsQueryFilter filter)
        {
            return new CombatPhysicsQuery(
                query.Header,
                CombatPhysicsShape.Sector(query.Origin, query.Direction, query.Radius, query.MinDot),
                filter);
        }

        public CombatRayQuery ToRayQuery()
        {
            EnsureShape(CombatPhysicsShapeKind.Ray);
            return new CombatRayQuery(Header, Shape.Origin, Shape.Direction, Shape.Length);
        }

        public CombatSphereQuery ToSphereQuery()
        {
            EnsureShape(CombatPhysicsShapeKind.Sphere);
            return new CombatSphereQuery(Header, Shape.Center, Shape.Radius);
        }

        public CombatCapsuleQuery ToCapsuleQuery()
        {
            EnsureShape(CombatPhysicsShapeKind.Capsule);
            return new CombatCapsuleQuery(Header, Shape.PointA, Shape.PointB, Shape.Radius);
        }

        public CombatAabbQuery ToAabbQuery()
        {
            EnsureShape(CombatPhysicsShapeKind.Aabb);
            return new CombatAabbQuery(Header, Shape.Min, Shape.Max);
        }

        public CombatSectorQuery ToSectorQuery()
        {
            EnsureShape(CombatPhysicsShapeKind.Sector);
            return new CombatSectorQuery(Header, Shape.Origin, Shape.Direction, Shape.Radius, Shape.MinDot);
        }

        public bool Equals(CombatPhysicsQuery other)
        {
            return Header.Equals(other.Header)
                && Shape.Equals(other.Shape)
                && Filter.Equals(other.Filter);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsQuery other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Header.GetHashCode();
                hash = (hash * 397) ^ Shape.GetHashCode();
                hash = (hash * 397) ^ Filter.GetHashCode();
                return hash;
            }
        }

        internal static int CompareHeaders(CombatQueryHeader left, CombatQueryHeader right)
        {
            int compare = left.TraceId.CompareTo(right.TraceId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.ActionId.CompareTo(right.ActionId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.SourceOrder.CompareTo(right.SourceOrder);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.QueryId.CompareTo(right.QueryId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.Kind.CompareTo(right.Kind);
            if (compare != 0)
            {
                return compare;
            }

            return left.SourceEntityId.CompareTo(right.SourceEntityId);
        }

        private static bool HeaderMatchesShape(CombatQueryKind queryKind, CombatPhysicsShapeKind shapeKind)
        {
            return (queryKind == CombatQueryKind.Ray && shapeKind == CombatPhysicsShapeKind.Ray)
                || (queryKind == CombatQueryKind.Sphere && shapeKind == CombatPhysicsShapeKind.Sphere)
                || (queryKind == CombatQueryKind.Capsule && shapeKind == CombatPhysicsShapeKind.Capsule)
                || (queryKind == CombatQueryKind.Aabb && shapeKind == CombatPhysicsShapeKind.Aabb)
                || (queryKind == CombatQueryKind.Sector && shapeKind == CombatPhysicsShapeKind.Sector)
                || (queryKind == CombatQueryKind.Obb && shapeKind == CombatPhysicsShapeKind.Obb);
        }

        private void EnsureShape(CombatPhysicsShapeKind kind)
        {
            if (Shape.Kind != kind)
            {
                throw new InvalidOperationException("Combat physics query shape kind does not match the requested conversion.");
            }
        }
    }
}

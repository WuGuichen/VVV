using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using Newtonsoft.Json;

namespace MxFramework.Combat.Levels
{
    public sealed class CombatLevelDefinition
    {
        public const int CurrentSchemaVersion = 2;

        public CombatLevelDefinition(
            int schemaVersion,
            string levelStableId,
            string displayName,
            IReadOnlyList<CombatLevelAabbColliderDefinition> aabbColliders,
            IReadOnlyList<CombatLevelPointDefinition> spawnPoints,
            IReadOnlyList<CombatLevelPointDefinition> checkpoints)
            : this(
                schemaVersion,
                levelStableId,
                displayName,
                aabbColliders,
                Array.Empty<CombatLevelObbColliderDefinition>(),
                spawnPoints,
                checkpoints)
        {
        }

        [JsonConstructor]
        public CombatLevelDefinition(
            int schemaVersion,
            string levelStableId,
            string displayName,
            IReadOnlyList<CombatLevelAabbColliderDefinition> aabbColliders,
            IReadOnlyList<CombatLevelObbColliderDefinition> obbColliders,
            IReadOnlyList<CombatLevelPointDefinition> spawnPoints,
            IReadOnlyList<CombatLevelPointDefinition> checkpoints)
        {
            SchemaVersion = schemaVersion;
            LevelStableId = levelStableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            AabbColliders = CopyList(aabbColliders);
            ObbColliders = CopyList(obbColliders);
            SpawnPoints = CopyList(spawnPoints);
            Checkpoints = CopyList(checkpoints);
        }

        public int SchemaVersion { get; }

        public string LevelStableId { get; }

        public string DisplayName { get; }

        public IReadOnlyList<CombatLevelAabbColliderDefinition> AabbColliders { get; }

        public IReadOnlyList<CombatLevelObbColliderDefinition> ObbColliders { get; }

        public IReadOnlyList<CombatLevelPointDefinition> SpawnPoints { get; }

        public IReadOnlyList<CombatLevelPointDefinition> Checkpoints { get; }

        internal static ReadOnlyCollection<T> CopyList<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
            {
                return new ReadOnlyCollection<T>(new List<T>());
            }

            var copy = new List<T>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                copy.Add(source[i]);
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }

    public sealed class CombatLevelObbColliderDefinition
    {
        public CombatLevelObbColliderDefinition(
            string stableId,
            int entityId,
            int bodyId,
            int colliderId,
            int layer,
            CombatLevelFixedVector3 position,
            CombatLevelFixedVector3 localCenter,
            CombatLevelFixedVector3 halfExtents,
            CombatLevelFixedVector3 axisX,
            CombatLevelFixedVector3 axisY,
            CombatLevelFixedVector3 axisZ,
            CombatLevelSurfaceMaterialDefinition surfaceMaterial)
        {
            StableId = stableId ?? string.Empty;
            EntityId = entityId;
            BodyId = bodyId;
            ColliderId = colliderId;
            Layer = layer;
            Position = position;
            LocalCenter = localCenter;
            HalfExtents = halfExtents;
            AxisX = axisX;
            AxisY = axisY;
            AxisZ = axisZ;
            SurfaceMaterial = surfaceMaterial;
        }

        public string StableId { get; }

        public int EntityId { get; }

        public int BodyId { get; }

        public int ColliderId { get; }

        public int Layer { get; }

        public CombatLevelFixedVector3 Position { get; }

        public CombatLevelFixedVector3 LocalCenter { get; }

        public CombatLevelFixedVector3 HalfExtents { get; }

        public CombatLevelFixedVector3 AxisX { get; }

        public CombatLevelFixedVector3 AxisY { get; }

        public CombatLevelFixedVector3 AxisZ { get; }

        public CombatLevelSurfaceMaterialDefinition SurfaceMaterial { get; }

        public CombatPhysicsBody ToPhysicsBody()
        {
            return new CombatPhysicsBody(
                new CombatEntityId(EntityId),
                new CombatBodyId(BodyId),
                Position.ToFixVector3());
        }

        public CombatPhysicsObbCollider ToObbCollider()
        {
            return new CombatPhysicsObbCollider(
                new CombatBodyId(BodyId),
                new CombatColliderId(ColliderId),
                Layer,
                LocalCenter.ToFixVector3(),
                HalfExtents.ToFixVector3(),
                AxisX.ToFixVector3(),
                AxisY.ToFixVector3(),
                AxisZ.ToFixVector3(),
                SurfaceMaterial.ToSurfaceMaterial(),
                priority: 0);
        }
    }

    public sealed class CombatLevelAabbColliderDefinition
    {
        public CombatLevelAabbColliderDefinition(
            string stableId,
            int entityId,
            int bodyId,
            int colliderId,
            int layer,
            CombatLevelFixedVector3 position,
            CombatLevelFixedVector3 localMin,
            CombatLevelFixedVector3 localMax,
            CombatLevelSurfaceMaterialDefinition surfaceMaterial)
        {
            StableId = stableId ?? string.Empty;
            EntityId = entityId;
            BodyId = bodyId;
            ColliderId = colliderId;
            Layer = layer;
            Position = position ?? CombatLevelFixedVector3.Zero;
            LocalMin = localMin ?? CombatLevelFixedVector3.Zero;
            LocalMax = localMax ?? CombatLevelFixedVector3.Zero;
            SurfaceMaterial = surfaceMaterial ?? CombatLevelSurfaceMaterialDefinition.Default;
        }

        public string StableId { get; }

        public int EntityId { get; }

        public int BodyId { get; }

        public int ColliderId { get; }

        public int Layer { get; }

        public CombatLevelFixedVector3 Position { get; }

        public CombatLevelFixedVector3 LocalMin { get; }

        public CombatLevelFixedVector3 LocalMax { get; }

        public CombatLevelSurfaceMaterialDefinition SurfaceMaterial { get; }

        public CombatPhysicsBody ToPhysicsBody()
        {
            return new CombatPhysicsBody(
                new CombatEntityId(EntityId),
                new CombatBodyId(BodyId),
                Position.ToFixVector3());
        }

        public CombatPhysicsAabbCollider ToAabbCollider()
        {
            return new CombatPhysicsAabbCollider(
                new CombatBodyId(BodyId),
                new CombatColliderId(ColliderId),
                Layer,
                LocalMin.ToFixVector3(),
                LocalMax.ToFixVector3(),
                SurfaceMaterial.ToSurfaceMaterial());
        }
    }

    public sealed class CombatLevelPointDefinition
    {
        public CombatLevelPointDefinition(string stableId, CombatLevelFixedVector3 position)
        {
            StableId = stableId ?? string.Empty;
            Position = position ?? CombatLevelFixedVector3.Zero;
        }

        public string StableId { get; }

        public CombatLevelFixedVector3 Position { get; }
    }

    public sealed class CombatLevelSurfaceMaterialDefinition : IEquatable<CombatLevelSurfaceMaterialDefinition>
    {
        public static readonly CombatLevelSurfaceMaterialDefinition Default =
            FromSurfaceMaterial(CombatSurfaceMaterial.Default);

        public CombatLevelSurfaceMaterialDefinition(
            CombatSurfaceMaterialKind kind,
            long frictionRaw,
            long elasticityRaw,
            long fixedBounceHeightRaw)
        {
            Kind = kind;
            FrictionRaw = frictionRaw;
            ElasticityRaw = elasticityRaw;
            FixedBounceHeightRaw = fixedBounceHeightRaw;
        }

        public CombatSurfaceMaterialKind Kind { get; }

        public long FrictionRaw { get; }

        public long ElasticityRaw { get; }

        public long FixedBounceHeightRaw { get; }

        public static CombatLevelSurfaceMaterialDefinition FromSurfaceMaterial(CombatSurfaceMaterial material)
        {
            return new CombatLevelSurfaceMaterialDefinition(
                material.Kind,
                material.Friction.RawValue,
                material.Elasticity.RawValue,
                material.FixedBounceHeight.RawValue);
        }

        public CombatSurfaceMaterial ToSurfaceMaterial()
        {
            return new CombatSurfaceMaterial(
                Kind,
                Fix64.FromRaw(FrictionRaw),
                Fix64.FromRaw(ElasticityRaw),
                Fix64.FromRaw(FixedBounceHeightRaw));
        }

        public bool Equals(CombatLevelSurfaceMaterialDefinition other)
        {
            return other != null
                && Kind == other.Kind
                && FrictionRaw == other.FrictionRaw
                && ElasticityRaw == other.ElasticityRaw
                && FixedBounceHeightRaw == other.FixedBounceHeightRaw;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CombatLevelSurfaceMaterialDefinition);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ FrictionRaw.GetHashCode();
                hash = (hash * 397) ^ ElasticityRaw.GetHashCode();
                hash = (hash * 397) ^ FixedBounceHeightRaw.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class CombatLevelFixedVector3 : IEquatable<CombatLevelFixedVector3>
    {
        public static readonly CombatLevelFixedVector3 Zero =
            FromFixVector3(FixVector3.Zero);

        public CombatLevelFixedVector3(long xRaw, long yRaw, long zRaw)
        {
            XRaw = xRaw;
            YRaw = yRaw;
            ZRaw = zRaw;
        }

        public long XRaw { get; }

        public long YRaw { get; }

        public long ZRaw { get; }

        public static CombatLevelFixedVector3 FromFixVector3(FixVector3 value)
        {
            return new CombatLevelFixedVector3(
                value.X.RawValue,
                value.Y.RawValue,
                value.Z.RawValue);
        }

        public FixVector3 ToFixVector3()
        {
            return new FixVector3(
                Fix64.FromRaw(XRaw),
                Fix64.FromRaw(YRaw),
                Fix64.FromRaw(ZRaw));
        }

        public bool Equals(CombatLevelFixedVector3 other)
        {
            return other != null
                && XRaw == other.XRaw
                && YRaw == other.YRaw
                && ZRaw == other.ZRaw;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CombatLevelFixedVector3);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = XRaw.GetHashCode();
                hash = (hash * 397) ^ YRaw.GetHashCode();
                hash = (hash * 397) ^ ZRaw.GetHashCode();
                return hash;
            }
        }
    }
}

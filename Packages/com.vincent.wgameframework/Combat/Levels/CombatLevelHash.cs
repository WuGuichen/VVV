using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Levels
{
    public static class CombatLevelHash
    {
        public const int ShapeDiscriminatorAabb = 1;
        public const int ShapeDiscriminatorObb = 2;

        public static CombatHash ComputeContentHash(CombatLevelDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            CombatHash hash = CombatHash.Empty
                .Add(definition.SchemaVersion);
            hash = AddString(hash, definition.LevelStableId);
            hash = AddString(hash, definition.DisplayName);

            var aabbColliders = new List<CombatLevelAabbColliderDefinition>(definition.AabbColliders);
            aabbColliders.Sort(CompareAabbColliders);
            hash = hash.Add(aabbColliders.Count);
            for (int i = 0; i < aabbColliders.Count; i++)
            {
                hash = AddAabbCollider(hash, aabbColliders[i]);
            }

            var obbColliders = new List<CombatLevelObbColliderDefinition>(definition.ObbColliders);
            obbColliders.Sort(CompareObbColliders);
            hash = hash.Add(obbColliders.Count);
            for (int i = 0; i < obbColliders.Count; i++)
            {
                hash = AddObbCollider(hash, obbColliders[i]);
            }

            var spawnPoints = new List<CombatLevelPointDefinition>(definition.SpawnPoints);
            spawnPoints.Sort(ComparePoints);
            hash = hash.Add(spawnPoints.Count);
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                hash = AddPoint(hash, spawnPoints[i]);
            }

            var checkpoints = new List<CombatLevelPointDefinition>(definition.Checkpoints);
            checkpoints.Sort(ComparePoints);
            hash = hash.Add(checkpoints.Count);
            for (int i = 0; i < checkpoints.Count; i++)
            {
                hash = AddPoint(hash, checkpoints[i]);
            }

            return hash;
        }

        private static CombatHash AddAabbCollider(CombatHash hash, CombatLevelAabbColliderDefinition collider)
        {
            hash = hash.Add(ShapeDiscriminatorAabb);
            hash = AddString(hash, collider.StableId)
                .Add(collider.EntityId)
                .Add(collider.BodyId)
                .Add(collider.ColliderId);
            hash = AddVector(hash, collider.Position);
            hash = AddVector(hash, collider.LocalMin);
            hash = AddVector(hash, collider.LocalMax);
            hash = hash.Add(collider.Layer);
            return AddSurfaceMaterial(hash, collider.SurfaceMaterial);
        }

        private static CombatHash AddObbCollider(CombatHash hash, CombatLevelObbColliderDefinition collider)
        {
            hash = hash.Add(ShapeDiscriminatorObb);
            hash = AddString(hash, collider.StableId)
                .Add(collider.EntityId)
                .Add(collider.BodyId)
                .Add(collider.ColliderId);
            hash = AddVector(hash, collider.Position);
            hash = AddVector(hash, collider.LocalCenter);
            hash = AddVector(hash, collider.HalfExtents);
            hash = AddVector(hash, collider.AxisX);
            hash = AddVector(hash, collider.AxisY);
            hash = AddVector(hash, collider.AxisZ);
            hash = hash.Add(collider.Layer);
            return AddSurfaceMaterial(hash, collider.SurfaceMaterial);
        }

        private static CombatHash AddPoint(CombatHash hash, CombatLevelPointDefinition point)
        {
            hash = AddString(hash, point.StableId);
            return AddVector(hash, point.Position);
        }

        private static CombatHash AddSurfaceMaterial(
            CombatHash hash,
            CombatLevelSurfaceMaterialDefinition surfaceMaterial)
        {
            return hash
                .Add((int)surfaceMaterial.Kind)
                .Add(unchecked((ulong)surfaceMaterial.FrictionRaw))
                .Add(unchecked((ulong)surfaceMaterial.ElasticityRaw))
                .Add(unchecked((ulong)surfaceMaterial.FixedBounceHeightRaw));
        }

        private static CombatHash AddVector(CombatHash hash, CombatLevelFixedVector3 value)
        {
            return hash
                .Add(unchecked((ulong)value.XRaw))
                .Add(unchecked((ulong)value.YRaw))
                .Add(unchecked((ulong)value.ZRaw));
        }

        private static CombatHash AddString(CombatHash hash, string value)
        {
            string text = value ?? string.Empty;
            hash = hash.Add(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                hash = hash.Add(text[i]);
            }

            return hash;
        }

        private static int CompareAabbColliders(
            CombatLevelAabbColliderDefinition left,
            CombatLevelAabbColliderDefinition right)
        {
            int compare = string.CompareOrdinal(left.StableId, right.StableId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.BodyId.CompareTo(right.BodyId);
            if (compare != 0)
            {
                return compare;
            }

            return left.ColliderId.CompareTo(right.ColliderId);
        }

        private static int CompareObbColliders(
            CombatLevelObbColliderDefinition left,
            CombatLevelObbColliderDefinition right)
        {
            int compare = string.CompareOrdinal(left.StableId, right.StableId);
            if (compare != 0)
                return compare;

            compare = left.BodyId.CompareTo(right.BodyId);
            return compare != 0 ? compare : left.ColliderId.CompareTo(right.ColliderId);
        }

        private static int ComparePoints(CombatLevelPointDefinition left, CombatLevelPointDefinition right)
        {
            return string.CompareOrdinal(left.StableId, right.StableId);
        }
    }
}

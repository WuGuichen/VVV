using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestPositionRecallSafety
    {
        /// <remarks>NoAlloc with caller-owned scratch. Retains the existing exact-point recall contract.</remarks>
        internal static bool TryValidate(CombatPhysicsWorld world, in CombatQueryHeader groundHeader, in CombatQueryHeader clearanceHeader,
            CombatBodyId ownBody, FixVector3 point, FixVector3 half, Fix64 centerOffset, Fix64 skin, Fix64 rise,
            Fix64 minimumNormal, List<CombatQueryResult> hits, out CombatSurfaceMaterial material) =>
            CharacterTestCharacterPlacementSafety.TryValidateGroundPlacement(world, groundHeader, clearanceHeader,
                ownBody, point, half, centerOffset, skin, rise, minimumNormal, hits, out material);
    }

    /// <summary>Read-only placement queries shared by relocation and future body-size transactions. Owns no mechanism state.</summary>
    internal static class CharacterTestCharacterPlacementSafety
    {
        /// <remarks>NoAlloc with caller-owned scratch. Checks the exact destination; no path check or endpoint snapping.</remarks>
        internal static bool TryValidateGroundPlacement(CombatPhysicsWorld world, in CombatQueryHeader groundHeader, in CombatQueryHeader clearanceHeader,
            CombatBodyId ownBody, FixVector3 point, FixVector3 half, Fix64 centerOffset, Fix64 skin, Fix64 rise,
            Fix64 minimumNormal, List<CombatQueryResult> hits, out CombatSurfaceMaterial material)
        {
            material = default;
            if (world == null || hits == null || ownBody.IsNone || rise <= Fix64.Zero
                || half.X <= Fix64.Zero || half.Y <= Fix64.Zero || half.Z <= Fix64.Zero || skin < Fix64.Zero) return false;
            hits.Clear();
            world.QueryRay(new CombatRayQuery(groundHeader, point + new FixVector3(Fix64.Zero, rise, Fix64.Zero),
                new FixVector3(Fix64.Zero, -Fix64.One, Fix64.Zero), rise + rise), hits);
            var tolerance = skin + Fix64.FromRatio(2, 1000);
            if (hits.Count == 0 || hits[0].Normal.Y < minimumNormal || (hits[0].Point.Y - point.Y).Abs() > tolerance
                || !world.TryGetSurfaceMaterial(hits[0], out material)) return false;
            return HasFullBodyClearance(world, clearanceHeader, ownBody, point, half, centerOffset, skin, hits);
        }

        /// <remarks>NoAlloc with caller-owned scratch. The caller supplies the target dimensions, not the actor's current dimensions.</remarks>
        internal static bool HasFullBodyClearance(CombatPhysicsWorld world, in CombatQueryHeader clearanceHeader,
            CombatBodyId ownBody, FixVector3 point, FixVector3 half, Fix64 centerOffset, Fix64 skin, List<CombatQueryResult> hits)
        {
            if (world == null || hits == null || ownBody.IsNone || half.X <= Fix64.Zero
                || half.Y <= Fix64.Zero || half.Z <= Fix64.Zero || skin < Fix64.Zero) return false;
            skin = Fix64.Max(skin, Fix64.FromRatio(1, 1000));
            // Query the whole motor AABB, including the sides and corners. A width-only expansion needs clearance too.
            var clearanceHalf = new FixVector3(
                Fix64.Max(half.X - skin, Fix64.FromRatio(1, 1000)),
                Fix64.Max(half.Y - skin, Fix64.FromRatio(1, 1000)),
                Fix64.Max(half.Z - skin, Fix64.FromRatio(1, 1000)));
            FixVector3 center = point + new FixVector3(Fix64.Zero, centerOffset, Fix64.Zero);
            hits.Clear(); world.QueryAabb(new CombatAabbQuery(clearanceHeader, center - clearanceHalf, center + clearanceHalf), hits);
            for (int i = 0; i < hits.Count; i++) if (!hits[i].TargetBodyId.Equals(ownBody)) return false;
            return true;
        }
    }
}

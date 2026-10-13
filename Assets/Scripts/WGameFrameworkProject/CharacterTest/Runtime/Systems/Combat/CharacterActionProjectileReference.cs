using MxFramework.CharacterAction;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Bounded configuration-time projectile measurement eligibility.</summary>
    internal static class CharacterActionProjectileReference
    {
        // Changes to the isolated collision consumer invalidate previous projectile measurements.
        internal const string ExecutorFingerprint = "actual-projectile-reference-v2-production-target-body";

        internal static bool TryValidateTargetCollider(CombatPhysicsWorld world, CombatPhysicsLayerMask queryMask,
            CombatBodyId targetBody, CombatEntityId targetEntity, out string issue)
        {
            issue = "ProjectileReferenceCharacterLayerExcluded";
            if (!queryMask.ContainsLayer(CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer)) return false;
            issue = "ProjectileReferenceTargetColliderMissing";
            if (world == null || !world.TryGetBody(targetBody, out var body) || !body.EntityId.Equals(targetEntity)
                || !world.TryGetAabbCollider(targetBody,
                    new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicCharacterColliderId), out var collider)) return false;
            issue = "ProjectileReferenceTargetColliderLayerMismatch";
            if (collider.Layer != CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer) return false;
            issue = string.Empty; return true;
        }

        internal static bool IsCandidate(CharacterActionConfig action)
            => action != null && action.CombatTrack.Events.Length == 0 && action.GameplayTrack.Events.Length == 1
                && action.GameplayTrack.Events[0].Kind == CharacterActionTrackEventKind.SendGameplayRequest;

        // This is a search budget, never an inferred hit range. The normal probe must
        // still observe a miss at the upper boundary and verify a real hit midpoint.
        internal static CharacterActionSpatialMeasurement Measurement()
            => new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50, 0, 50000,
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
    }

    internal sealed class CharacterActionProjectileReferenceCatalog
    {
        private readonly CombatProjectileDefinitionRegistry _projectileDefinitions;
        private readonly CombatProjectileSpawnProfileRegistry _projectileSpawnProfiles;
        internal CharacterActionProjectileReferenceCatalog(CombatProjectileDefinitionRegistry definitions, CombatProjectileSpawnProfileRegistry profiles)
        { _projectileDefinitions = definitions; _projectileSpawnProfiles = profiles; }

        internal bool TryGetReferenceProjectile(CharacterActionConfig action, out GameplayTrackEvent evt,
            out CombatProjectileSpawnProfile profile, out CombatProjectileDefinition definition, out string issue)
        {
            evt = default; profile = null; definition = null; issue = "ProjectileReferenceUnsupported";
            if (!CharacterActionProjectileReference.IsCandidate(action) || !action.DurationFrames.HasValue
                || action.DurationFrames.Value <= 0 || action.DurationFrames.Value > 512) return false;
            evt = action.GameplayTrack.Events[0];
            if (evt.Frame < 0 || evt.Frame >= action.DurationFrames.Value
                || (_projectileSpawnProfiles == null || !_projectileSpawnProfiles.TryGet(evt.RequestId, out profile))
                || (_projectileDefinitions == null || !_projectileDefinitions.TryGetByStableId(profile.ProjectileStableId, out definition))) return false;
            if (definition.Motion.Kind != CombatProjectileMotionKind.Linear
                || definition.Motion.SpeedProfileKind != CombatProjectileSpeedProfileKind.Constant
                || definition.Motion.LifetimeFrames <= 0 || definition.Motion.LifetimeFrames > 512
                || profile.Aim.MaxRange > definition.Motion.SpeedPerFrame * MxFramework.Core.Math.Fix64.FromInt(definition.Motion.LifetimeFrames)
                || definition.Impact.PolicyKind != CombatProjectileImpactPolicyKind.DirectHit
                || definition.Impact.EffectTimelineId != 0) return false;
            if (!definition.Collision.LayerMask.ContainsLayer(CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer))
            { issue = "ProjectileReferenceCharacterLayerExcluded"; return false; }
            issue = string.Empty; return true;
        }

    }

    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        internal CharacterActionProjectileReferenceCatalog ReferenceProjectiles =>
            new CharacterActionProjectileReferenceCatalog(_projectileDefinitions, _projectileSpawnProfiles);
        internal bool TryGetReferenceProjectile(CharacterActionConfig action, out GameplayTrackEvent evt,
            out CombatProjectileSpawnProfile profile, out CombatProjectileDefinition definition, out string issue)
            => ReferenceProjectiles.TryGetReferenceProjectile(action, out evt, out profile, out definition, out issue);
        internal System.Collections.Generic.IReadOnlyList<CombatProjectileSpawnEvent> ReferenceProjectileBirths => _projectileFrameEvents.Spawns;
        internal System.Collections.Generic.IReadOnlyList<CombatProjectileImpactEvent> ReferenceProjectileImpacts => _projectileFrameEvents.Impacts;
    }
}

using System;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterActionReferenceProbeSession
    {
        /// <remarks>LowFreqAlloc. Every sample owns a fresh isolated runtime so projectile,
        /// cooldown, on-hit buff and health state cannot leak into the next distance.</remarks>
        private bool TryEvaluateProjectile(int distance, out CharacterTestAttackOpportunityResult result, out string issue)
        {
            result = default;
            if (!Runtime.CombatTraceSystem.TryGetReferenceProjectile(Action, out _, out _, out var definition, out issue)
                || !TryComputeActionHash(out string expectedHash, out issue)) return false;
            using (var sample = ForTestAction(Source.Identity.CharacterId, ReferenceDummy, Action.ActionId.Value, Source.Equipment, projectileCollisionTarget: true))
            {
                if (!sample.TryComputeActionHash(out var actualHash, out issue) || expectedHash != actualHash)
                { issue = "ProjectileReferenceProviderMismatch:" + issue; return false; }
                if (!sample.TryPositionReference(distance, out int preparedFrame, out issue)) return false;
                var physics = sample.Runtime.PhysicsRuntime;
                if (!CharacterActionProjectileReference.TryValidateTargetCollider(physics.World, definition.Collision.LayerMask,
                    physics.GetBodyId(sample.Target), physics.GetEntityId(sample.Target), out issue)) return false;
                // The reference capsule is the explicit aim target, through the same production aim resolver.
                sample.Runtime.SetLockedTargetEntityIdProvider(() => sample.Target.Id.Value);
                int startFrame = preparedFrame + 1;
                if (!sample.Runtime.ActionRuntime.TryStartActionSlot(sample.Source, (int)CharacterCancelInputMask.AttackPrimary,
                    Action.ActionId, new RuntimeFrame(startFrame)))
                { issue = "ReferenceGatewayRejected:" + sample.Source.Action.LastRejectReason; return false; }
                var targetId = sample.Runtime.PhysicsRuntime.GetEntityId(sample.Target);
                var sourceId = sample.Runtime.PhysicsRuntime.GetEntityId(sample.Source);
                bool born = false;
                int endFrame = startFrame + Action.DurationFrames.Value + definition.Motion.LifetimeFrames + 2;
                for (int frame = startFrame; frame <= endFrame; frame++)
                {
                    sample.Runtime.Tick(new RuntimeTickContext(frame, 1d / 60d, frame / 60d, RuntimeTickStage.Simulation));
                    var combat = sample.Runtime.CombatTraceSystem;
                    for (int i = 0; i < combat.ReferenceProjectileBirths.Count; i++)
                    {
                        var birth = combat.ReferenceProjectileBirths[i].Projectile;
                        if (birth.SourceEntityId.Equals(sourceId) && birth.ActionId == Action.ActionId.Value
                            && birth.DefinitionId == definition.DefinitionId) born = true;
                    }
                    for (int i = 0; i < combat.ReferenceProjectileImpacts.Count; i++)
                    {
                        var impact = combat.ReferenceProjectileImpacts[i];
                        if (!born || !impact.Projectile.SourceEntityId.Equals(sourceId)
                            || impact.Projectile.ActionId != Action.ActionId.Value
                            || impact.Projectile.DefinitionId != definition.DefinitionId
                            || impact.Context.SourceKind != CombatProjectileImpactSourceKind.DirectSweep
                            || !impact.ResolveResult.TargetId.Equals(targetId) || impact.ResolveResult.Kind != HitResolveKind.Damage) continue;
                        result = ProjectileEvidence(true, frame - startFrame, impact.PhysicsHit.Point,
                            impact.PhysicsHit.TargetBodyId, impact.PhysicsHit.TargetColliderId);
                        issue = string.Empty; return true;
                    }
                    if (born && sample.Runtime.CombatTraceSystem.ActiveProjectileCount == 0) break;
                }
                if (!born) { issue = "ProjectileReferenceBirthMissing"; return false; }
                if (sample.Runtime.CombatTraceSystem.ActiveProjectileCount != 0)
                { issue = "ProjectileReferenceDidNotExpireWithinBudget"; return false; }
                result = ProjectileEvidence(false, -1, FixVector3.Zero, default, default);
                issue = string.Empty; return true;
            }
        }

        private static CharacterTestAttackOpportunityResult ProjectileEvidence(bool hit, int tick, FixVector3 point,
            CombatBodyId body, CombatColliderId collider)
            => new CharacterTestAttackOpportunityResult(CharacterTestAttackOpportunityStatus.Valid,
                CharacterTestAttackEvaluationMode.CurrentPose,
                hit ? CharacterTestAttackOpportunityLevel.CurrentPose : CharacterTestAttackOpportunityLevel.NoOpportunity,
                true, hit, hit, hit, false, false, 0, tick, -1, 0, collider.Value, 0, 0, point, point,
                CharacterTestAttackOpportunityFailureReason.None, CharacterTestAttackOpportunityFailureFlags.None,
                targetWitnessPoint: point, acceptedHitCount: hit ? 1 : 0, selectedBodyId: body, selectedColliderId: collider);
    }
}

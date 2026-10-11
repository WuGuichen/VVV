using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.CombatBridge;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        private readonly CharacterTestRuneTurretRuntime _runeTurretRuntime = new CharacterTestRuneTurretRuntime();
        private readonly List<CharacterTestRuneTurretDeployed> _runeTurretDeployedTurrets =
            new List<CharacterTestRuneTurretDeployed>();
        private readonly List<CharacterTestRuneTurretFired> _runeTurretFiredTurrets =
            new List<CharacterTestRuneTurretFired>();
        private readonly List<CharacterTestRuneTurretExpired> _runeTurretExpiredTurrets =
            new List<CharacterTestRuneTurretExpired>();

        internal CharacterTestRuneTurretTickSummary RuneTurretLastTickSummary => _runeTurretRuntime.LastTickSummary;
        public int ActiveRuneTurretCount => _runeTurretRuntime.ActiveCount;
        public int LastRuneTurretShotCount { get; private set; }

        internal CharacterTestRuneTurretRuntimeState ReadRuneTurretStateView()
        {
            return _runeTurretRuntime.ReadStateView();
        }

        internal CharacterTestRuneTurretRuntimeState CreateRuneTurretStateSnapshot()
        {
            return _runeTurretRuntime.CreateStateSnapshot();
        }

        internal bool TryApplyRuneTurretStateSnapshot(
            CharacterTestRuneTurretRuntimeState state,
            out string failure)
        {
            return _runeTurretRuntime.TryApplyStateSnapshot(state, out failure);
        }

        private void ProcessRuneTurretRequests(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
            {
                CharacterTestCharacterEntity entity = entities[entityIndex];
                IReadOnlyList<CharacterActionGameplayRequest> requests = entity.GameplayRequests;
                for (int requestIndex = 0; requestIndex < requests.Count; requestIndex++)
                {
                    CharacterActionGameplayRequest request = requests[requestIndex];
                    if (request.EventKind == CharacterActionTrackEventKind.SendGameplayRequest
                        && request.RequestId == CharacterTestCharacterActionConfigSource.RuneTurretGameplayRequestId)
                    {
                        ProcessRuneTurretRequest(world, entity, request, actionRuntime, frame, requestIndex);
                    }
                }
            }
        }

        private void ProcessRuneTurretRequest(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            int sourceOrder)
        {
            if (!CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                    _projectileSpawnProfiles,
                    CharacterTestFireballProjectileAdapter.RuneTurretDeployRequestId,
                    out CombatProjectileSpawnProfile spawnProfile))
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.RequestId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "RuneTurretProjectileSpawnProfileMissing",
                    "Rune Turret deploy projectile spawn profile is missing.");
                return;
            }

            if (!_projectileDefinitions.TryGetByStableId(
                    spawnProfile.ProjectileStableId,
                    out CombatProjectileDefinition definition))
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    spawnProfile.ProjectileStableId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "RuneTurretProjectileDefinitionMissing",
                    "Rune Turret deploy projectile definition is missing.");
                return;
            }

            CharacterTestRuneTurretDeployResult targetResult = _runeTurretRuntime.TryResolveDeployTarget(
                world,
                _physicsRuntime,
                entity,
                _lockedTargetEntityIdProvider,
                out CharacterTestRuneTurretDeployTarget target);
            if (!targetResult.Success)
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.RequestId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "RuneTurretTargetRejected",
                    targetResult.FailureReason);
                return;
            }

            FixVector3 origin = ResolveProjectileOrigin(entity, request, actionRuntime, spawnProfile);
            if (!CombatProjectileBallistics.TrySolveLaunchArc(
                    origin,
                    target.Center,
                    definition.Motion.SpeedPerFrame,
                    definition.Motion.GravityPerFrameSquared,
                    definition.Motion.MaxLaunchAngleTangent,
                    definition.Motion.LifetimeFrames,
                    out FixVector3 initialVelocity,
                    out Fix64 launchSpeedPerFrame,
                    out int effectiveLifetimeFrames))
            {
                ReportHitTraceWarning(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.RequestId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "RuneTurretProjectileUnreachable",
                    "Rune Turret target point is outside the deploy projectile ballistic reach. Projectile spawn will be rejected.");
                return;
            }

            if (!initialVelocity.TryNormalize(out FixVector3 direction))
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.RequestId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "RuneTurretProjectileDirectionInvalid",
                    "Rune Turret deploy projectile solved a zero launch direction.");
                return;
            }

            _projectileSpawnRequests.Enqueue(new CombatProjectileSpawnRequest(
                ToCombatFrame(frame),
                definition.DefinitionId,
                _physicsRuntime.GetEntityId(entity),
                origin,
                direction,
                true,
                target.Center,
                StablePositiveId(request.Metadata.ActionId.Value),
                StablePositiveId(request.Metadata.InstanceId),
                StablePositiveId(CharacterTestCharacterActionConfigSource.RuneTurretDeployProjectileImpactStableEventId),
                request.Metadata.LocalFrame,
                sourceOrder,
                effectiveLifetimeFrames));

            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                entity.Id.Value,
                "runeTurretThrow request=" + CharacterTestCombatTraceText.EmptyAsNone(request.RequestId)
                + " mode=" + CharacterTestCombatTraceText.EmptyAsNone(target.DeployMode)
                + " projectile=" + CharacterTestCombatTraceText.EmptyAsNone(definition.StableId)
                + " lifetime=" + effectiveLifetimeFrames.ToString(CultureInfo.InvariantCulture)
                + " launchSpeed=" + launchSpeedPerFrame.ToString());
        }

        private void ProcessRuneTurretRuntimeEvents(RuntimeFrame frame)
        {
            LastRuneTurretShotCount = 0;
            _runeTurretDeployedTurrets.Clear();
            _runeTurretRuntime.DrainLastDeployedTurrets(_runeTurretDeployedTurrets);
            for (int i = 0; i < _runeTurretDeployedTurrets.Count; i++)
            {
                CharacterTestRuneTurretDeployed deployed = _runeTurretDeployedTurrets[i];
                SpawnRuneTurretGameplayVfx(
                    deployed.CasterEntityId,
                    deployed.Center,
                    deployed.Forward,
                    deployed.SourceOrder,
                    deployed.LocalFrame,
                    _runeTurretRuntime.Tuning.DeployVfxDefinitionId,
                    CharacterTestRuneTurretIds.DeployVfxLifetimeFrames,
                    CharacterTestCharacterActionConfigSource.RuneTurretDeployVfxStableEventId,
                    frame);
                SpawnRuneTurretGameplayVfx(
                    deployed.CasterEntityId,
                    deployed.Center,
                    deployed.Forward,
                    deployed.SourceOrder,
                    deployed.LocalFrame,
                    _runeTurretRuntime.Tuning.IdleVfxDefinitionId,
                    _runeTurretRuntime.Tuning.DurationFrames,
                    CharacterTestCharacterActionConfigSource.RuneTurretIdleVfxStableEventId,
                    frame);
            }

            _runeTurretDeployedTurrets.Clear();

            _runeTurretFiredTurrets.Clear();
            _runeTurretRuntime.DrainLastFiredTurrets(_runeTurretFiredTurrets);
            for (int i = 0; i < _runeTurretFiredTurrets.Count; i++)
            {
                CharacterTestRuneTurretFired fired = _runeTurretFiredTurrets[i];
                SpawnRuneTurretGameplayVfx(
                    fired.CasterEntityId,
                    fired.Center,
                    fired.Forward,
                    fired.SourceOrder,
                    fired.LocalFrame,
                    _runeTurretRuntime.Tuning.FireVfxDefinitionId,
                    CharacterTestRuneTurretIds.FireVfxLifetimeFrames,
                    CharacterTestCharacterActionConfigSource.RuneTurretFireVfxStableEventId,
                    frame);
                if (TrySpawnRuneTurretShotProjectile(fired, frame))
                    LastRuneTurretShotCount++;
            }

            _runeTurretFiredTurrets.Clear();

            _runeTurretExpiredTurrets.Clear();
            _runeTurretRuntime.DrainLastExpiredTurrets(_runeTurretExpiredTurrets);
            for (int i = 0; i < _runeTurretExpiredTurrets.Count; i++)
            {
                CharacterTestRuneTurretExpired expired = _runeTurretExpiredTurrets[i];
                SpawnRuneTurretGameplayVfx(
                    expired.CasterEntityId,
                    expired.Center,
                    expired.Forward,
                    expired.SourceOrder,
                    expired.LocalFrame,
                    _runeTurretRuntime.Tuning.ExpireVfxDefinitionId,
                    CharacterTestRuneTurretIds.ExpireVfxLifetimeFrames,
                    CharacterTestCharacterActionConfigSource.RuneTurretExpireVfxStableEventId,
                    frame);
            }

            _runeTurretExpiredTurrets.Clear();
        }

        private bool TrySpawnRuneTurretShotProjectile(
            CharacterTestRuneTurretFired fired,
            RuntimeFrame frame)
        {
            if (!_projectileDefinitions.TryGetByStableId(
                    CharacterTestFireballProjectileAdapter.RuneTurretShotStableId,
                    out CombatProjectileDefinition shotDefinition))
            {
                ReportHitTraceError(
                    fired.CasterEntityId,
                    CharacterTestCharacterActionConfigSource.RuneTurretActionId,
                    CharacterTestFireballProjectileAdapter.RuneTurretShotStableId,
                    string.Empty,
                    fired.LocalFrame,
                    "RuneTurretShotProjectileDefinitionMissing",
                    "Rune Turret shot projectile definition is missing.");
                return false;
            }

            FixVector3 origin = fired.Center + new FixVector3(
                Fix64.Zero,
                _runeTurretRuntime.Tuning.MuzzleHeight,
                Fix64.Zero);
            FixVector3 aim = fired.TargetCenter - origin;
            if (!aim.TryNormalize(out FixVector3 direction))
                direction = fired.Forward;
            if (!direction.TryNormalize(out direction))
                direction = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

            _projectileSpawnRequests.Enqueue(new CombatProjectileSpawnRequest(
                ToCombatFrame(frame),
                shotDefinition.DefinitionId,
                new CombatEntityId(fired.SourceCombatEntityId),
                origin,
                direction,
                false,
                FixVector3.Zero,
                StablePositiveId(CharacterTestCharacterActionConfigSource.RuneTurretActionId),
                fired.ActionInstanceId,
                StablePositiveId(CharacterTestCharacterActionConfigSource.RuneTurretShotProjectileImpactStableEventId),
                fired.LocalFrame,
                fired.SourceOrder));
            return true;
        }

        private void CreateRuneTurretFromProjectileImpact(
            CharacterTestCharacterRuntimeWorld world,
            CombatProjectileImpactEvent impact,
            RuntimeFrame frame)
        {
            if (!CharacterTestFireballProjectileAdapter.IsRuneTurretDeployProjectile(impact.Projectile))
                return;

            CharacterTestCharacterEntity caster = _entityLookup.FindEntityByCombatId(impact.Projectile.SourceEntityId);
            if (caster == null)
                return;

            CreateRuneTurretAtProjectilePoint(
                world,
                caster,
                frame,
                impact.Projectile,
                impact.PhysicsHit.Point,
                CharacterTestRuneTurretRuntime.ProjectileImpactDeployMode,
                "impact");
        }

        private void CreateRuneTurretFromProjectileExpiration(
            CharacterTestCharacterRuntimeWorld world,
            CombatProjectileExpiredEvent expiration,
            RuntimeFrame frame)
        {
            if (!CharacterTestFireballProjectileAdapter.IsRuneTurretDeployProjectile(expiration.Projectile))
                return;

            CharacterTestCharacterEntity caster = _entityLookup.FindEntityByCombatId(expiration.Projectile.SourceEntityId);
            if (caster == null)
                return;

            CreateRuneTurretAtProjectilePoint(
                world,
                caster,
                frame,
                expiration.Projectile,
                expiration.Projectile.Position,
                CharacterTestRuneTurretRuntime.ProjectileExpirationDeployMode,
                "expire");
        }

        private void CreateRuneTurretAtProjectilePoint(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity caster,
            RuntimeFrame frame,
            CombatProjectileSnapshot projectile,
            FixVector3 point,
            string deployMode,
            string reason)
        {
            FixVector3 center = ProjectPoisonBottlePoolCenterToGround(point);
            FixVector3 forward = NormalizeProjectileDirection(projectile.Direction);
            CharacterTestRuneTurretDeployResult result = _runeTurretRuntime.CreateTurret(
                world,
                _physicsRuntime,
                caster,
                frame,
                center,
                forward,
                deployMode,
                CharacterTestCharacterActionConfigSource.RuneTurretDeployStableEventId,
                projectile.LocalFrame,
                projectile.SpawnSequence,
                projectile.ActionInstanceId);
            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                caster.Id.Value,
                "runeTurretProjectile " + CharacterTestCombatTraceText.EmptyAsNone(reason)
                + " created=" + result.Success.ToString()
                + " mode=" + CharacterTestCombatTraceText.EmptyAsNone(deployMode)
                + " projectile=" + projectile.ProjectileId.ToString(CultureInfo.InvariantCulture));
        }

        private void SpawnRuneTurretGameplayVfx(
            int casterEntityId,
            FixVector3 center,
            FixVector3 forward,
            int sourceOrder,
            int localFrame,
            int definitionId,
            int lifetimeFrames,
            int traceId,
            RuntimeFrame frame)
        {
            if (_vfxRuntime == null || definitionId <= 0)
                return;

            int triggerId = traceId;
            _vfxRuntime.EnqueueSpawnRequest(new VfxSpawnRequest(
                frame,
                VfxSourceDomains.Gameplay,
                frame.Value,
                localFrame,
                sourceOrder,
                triggerId,
                casterEntityId,
                0,
                new VfxDefinitionId(definitionId),
                VfxAnchor.World(new VfxPose(
                    CombatVfxPhysicsAdapter.ToVfxVector(center),
                    CombatVfxPhysicsAdapter.ToVfxVector(forward))),
                new VfxParameterBlock(triggerId, localFrame),
                lifetimeFrames,
                new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.CombatRuntimeEffectVfx, casterEntityId, frame.Value, traceId)));
        }
    }
}



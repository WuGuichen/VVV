using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.CombatBridge;
using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        private void ProcessProjectileSpawnRequest(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            int sourceOrder)
        {
            if (_projectileRuntime == null)
                return;
            if (!CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                    _projectileSpawnProfiles,
                    request.RequestId,
                    out CombatProjectileSpawnProfile spawnProfile))
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.RequestId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "ProjectileSpawnProfileMissing",
                    "Projectile request has no matching spawn profile stable id: "
                    + CharacterTestCombatTraceText.EmptyAsNone(request.RequestId)
                    + ".");
                return;
            }

            if (!_projectileDefinitions.TryGetByStableId(spawnProfile.ProjectileStableId, out CombatProjectileDefinition definition))
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    spawnProfile.ProjectileStableId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "ProjectileDefinitionMissing",
                    "Projectile spawn profile references a missing projectile definition stable id: "
                    + CharacterTestCombatTraceText.EmptyAsNone(spawnProfile.ProjectileStableId)
                    + ".");
                return;
            }

            CombatFrame combatFrame = ToCombatFrame(frame);
            int actionId = StablePositiveId(request.Metadata.ActionId.Value);
            int actionInstanceId = StablePositiveId(request.Metadata.InstanceId);
            int stableEventId = (definition.Impact.StableEventId == 0)
                ? request.Metadata.StableEventId
                : definition.Impact.StableEventId;
            int traceId = StablePositiveId(stableEventId);
            if (!ResolveProjectileSpawnPose(
                entity,
                request,
                actionRuntime,
                spawnProfile,
                definition,
                out FixVector3 origin,
                out FixVector3 direction,
                out FixVector3 targetPoint,
                out int effectiveLifetimeFrames,
                out bool hasTrackingTarget,
                out CombatEntityId trackingTargetEntityId))
            {
                return;
            }

            var spawnRequest = new CombatProjectileSpawnRequest(
                combatFrame,
                definition.DefinitionId,
                _physicsRuntime.GetEntityId(entity),
                origin,
                direction,
                true,
                targetPoint,
                actionId,
                actionInstanceId,
                traceId,
                request.Metadata.LocalFrame,
                sourceOrder,
                effectiveLifetimeFrames,
                hasTrackingTarget,
                trackingTargetEntityId);
            var doubleEdgeSource = entity.CaptureDoubleEdgeSource(request.Metadata.InstanceId);
            if (!EnqueueAccessoryProjectile(spawnRequest, doubleEdgeSource,
                _entityLookup.AccessoryState?.SecondBatchRoot(doubleEdgeSource) ?? default, frame.Value, true)) return;
            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                entity.Id.Value,
                "projectileSpawn request=" + CharacterTestCombatTraceText.EmptyAsNone(request.RequestId)
                + " profile=" + CharacterTestCombatTraceText.EmptyAsNone(spawnProfile.StableId)
                + " definition=" + CharacterTestCombatTraceText.EmptyAsNone(definition.StableId));
        }

        private void StartEffectTimelineFromCharacterActionRequest(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            RuntimeFrame frame)
        {
            if (_effectTimelineRuntime == null || (request.EffectTimelineId == 0))
                return;

            FixVector3 anchor;
            CombatBodyId sourceBodyId = _physicsRuntime.GetBodyId(entity);
            if (_physicsRuntime.World.TryGetBody(sourceBodyId, out CombatPhysicsBody sourceBody))
            {
                anchor = sourceBody.Position;
            }
            else
            {
                anchor = new FixVector3(
                    ToFix64(entity.Transform.X),
                    ToFix64(entity.Transform.Y),
                    ToFix64(entity.Transform.Z));
            }

            int abilityId = 0;
            if (_abilityDefinitions != null
                && _abilityDefinitions.TryGet(request.AbilityStableId, out AbilityDefinitionConfig ability))
            {
                abilityId = ability.AbilityId;
            }

            var timelineContext = new AbilityEffectTimelineContext(
                abilityId,
                request.EffectTimelineId,
                ownerEntityId: entity.Id.Value,
                sourceEntityId: entity.Id.Value,
                triggerEntityId: 0,
                AbilityEffectTimelineTriggerKind.CharacterActionEvent,
                anchor,
                CharacterTestFireballProjectileAdapter.ResolveDirection(entity));
            AbilityEffectTimelineStartResult start =
                StartAccessoryTimeline(timelineContext, frame, entity.CaptureDoubleEdgeSource(request.Metadata.InstanceId),
                    _entityLookup.AccessoryState?.SecondBatchRoot(entity.CaptureDoubleEdgeSource(request.Metadata.InstanceId)) ?? default,
                    third: _entityLookup.AccessoryState?.ThirdAttack(entity.GameplayEntityId, request.Metadata.InstanceId) ?? default);
            if (!start.Success)
            {
                ReportHitTraceWarning(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.EffectTimelineId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "AbilityEffectTimelineStartRejected",
                    start.FailureReason);
                return;
            }

            LastAbilityEffectTimelineStartCount++;
            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                entity.Id.Value,
                "effectTimelineStart timeline=" + CharacterTestCombatTraceText.EmptyAsNone(request.EffectTimelineId)
                + " action=" + CharacterTestCombatTraceText.EmptyAsNone(request.Metadata.ActionId)
                + " instance=" + start.InstanceId.ToString(CultureInfo.InvariantCulture));
        }

        private void ProcessGameplayDamageRequest(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            RuntimeFrame frame,
            int sourceOrder)
        {
            if (!TryResolveAreaRadius(request.TargetPolicyId, out Fix64 radius))
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.TargetPolicyId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "GameplayTargetPolicyUnsupported",
                    "ApplyGameplayEffect target policy is unsupported: "
                    + CharacterTestCombatTraceText.EmptyAsNone(request.TargetPolicyId) + ".");
                return;
            }

            CombatBodyId sourceBodyId = _physicsRuntime.GetBodyId(entity);
            if (!_physicsRuntime.World.TryGetBody(sourceBodyId, out CombatPhysicsBody sourceBody))
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.TargetPolicyId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "GameplaySourceBodyMissing",
                    "ApplyGameplayEffect source body is not registered in CombatPhysicsWorld.");
                return;
            }

            CombatEntityId sourceCombatId = _physicsRuntime.GetEntityId(entity);
            int actionId = StablePositiveId(request.Metadata.ActionId.Value);
            int actionInstanceId = StablePositiveId(request.Metadata.InstanceId);
            int traceId = request.Metadata.StableEventId;
            var header = new CombatQueryHeader(
                queryId: traceId,
                kind: CombatQueryKind.Sphere,
                sourceEntityId: sourceCombatId,
                traceId: traceId,
                actionId: actionId,
                sourceOrder: sourceOrder,
                layerMask: CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer));
            int startCount = _gameplayQueryResults.Count;
            _physicsRuntime.World.QuerySphere(
                new CombatSphereQuery(header, sourceBody.Position, radius),
                _gameplayQueryResults);
            for (int i = startCount; i < _gameplayQueryResults.Count; i++)
            {
                CombatQueryResult queryResult = _gameplayQueryResults[i];
                if (!_damageResolver.TryResolveGameplayDamage(
                        request,
                        entity,
                        queryResult.TargetEntityId,
                        queryResult.TargetColliderId,
                        _entityLookup,
                        frame,
                        DamageProfileSourceKind.AreaPulse,
                        CharacterTestBodyPartDamageSourcePolicy.NoPartArea,
                        out DamageProfileResolveResult damage,
                        out CharacterTestHitZoneResolution hitZone))
                {
                    continue;
                }

                var candidate = new HitCandidate(
                    sourceCombatId,
                    queryResult.TargetEntityId,
                    actionId,
                    actionInstanceId,
                    traceId,
                    new CombatFrame((int)Math.Min(int.MaxValue, Math.Max(0, frame.Value))),
                    queryResult,
                    damage.IncomingDamage,
                    CharacterTestHitReactionResolver.CreateHitResolveReactionGate(damage),
                    CharacterTestHitReactionResolver.CreateHitResolveKnockbackGate(damage),
                    _entityLookup.ResolveTargetState(queryResult.TargetEntityId));
                var hitKey = new CharacterTestResolvedHitKey(candidate);
                SetAccessoryAwareDamagePayload(hitKey, damage, ActionRequestEvidence(entity, request,
                    candidate.TargetId, frame, unchecked((ulong)(uint)request.Metadata.LocalFrame + 1)));
                _hitZonesByHit[hitKey] = hitZone;
                if (request.HitEvent.OnHitBuffCount > 0)
                    _onHitBuffsByHit[hitKey] = request.HitEvent.OnHitBuffs;
                _gameplayProfiledCandidates.Add(candidate);
            }

            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                entity.Id.Value,
                "gameplayDamage query policy=" + CharacterTestCombatTraceText.EmptyAsNone(request.TargetPolicyId)
                + " damage=" + (request.DamagePayload.HasInlineData
                    ? "hitEvent.damage"
                    : "missing")
                + " candidates=" + (_gameplayQueryResults.Count - startCount).ToString(CultureInfo.InvariantCulture));
        }

        private void ProcessProjectiles(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame)
        {
            if (_projectileRuntime == null)
                return;

            _projectileRuntime.Tick(ToCombatFrame(frame), _projectileFrameEvents);
            PublishDoubleEdgeProjectileBirths(world);
            IReadOnlyList<CombatProjectileAreaPulseTriggerEvent> areaPulseTriggers =
                _projectileFrameEvents.AreaPulseTriggers;
            for (int i = 0; i < areaPulseTriggers.Count; i++)
                StopMeteorBarrageTelegraphFromAreaPulseTrigger(areaPulseTriggers[i], frame);

            IReadOnlyList<CombatProjectileImpactEvent> impacts = _projectileFrameEvents.Impacts;
            for (int i = 0; i < impacts.Count; i++)
            {
                CombatProjectileImpactEvent impact = impacts[i];
                if (impact.ResolveResult.Kind == HitResolveKind.Evaded)
                {
                    GetVerboseCombatTraceRecorder()?.AttackCombat(
                        frame,
                        CharacterTestCombatEntityLookup.ResolveCharacterEntityId(impact.ResolveResult.AttackerId),
                        "projectileEvaded projectile="
                        + impact.Projectile.ProjectileId.ToString(CultureInfo.InvariantCulture)
                        + " target="
                        + CharacterTestCombatEntityLookup.ResolveCharacterEntityId(impact.ResolveResult.TargetId)
                            .ToString(CultureInfo.InvariantCulture));
                    continue;
                }
                if (_entityLookup.FindEntityByCombatId(impact.ResolveResult.TargetId) == null) {
                    // Environment collision is natural production, never an accepted actor-hit receipt.
                    PublishProjectileImpactEffects(world, impact, frame);
                } else {
                    _results.Add(impact.ResolveResult);
                    if (IsArcMineProjectile(impact.Projectile) || IsPoisonBottleProjectile(impact.Projectile) && impact.ResolveResult.IsAcceptedDamage)
                        _accessoryCarrierHits.Add(new CharacterTestResolvedHitKey(impact.ResolveResult));
                    RegisterProjectileDamagePayload(world, impact, frame);
                    _deferredAccessoryImpacts.Add(impact);
                }

                GetVerboseCombatTraceRecorder()?.AttackCombat(
                    frame,
                    CharacterTestCombatEntityLookup.ResolveCharacterEntityId(impact.ResolveResult.AttackerId),
                    "projectileImpact projectile="
                    + impact.Projectile.ProjectileId.ToString(CultureInfo.InvariantCulture)
                    + " target="
                    + CharacterTestCombatEntityLookup.ResolveCharacterEntityId(impact.ResolveResult.TargetId).ToString(CultureInfo.InvariantCulture)
                    + " kind=" + impact.ResolveResult.Kind
                    + " consumed=" + impact.ConsumedProjectile.ToString());
                RecordLaserPathVfxFromProjectileImpact(impact, frame);
            }

            IReadOnlyList<CombatProjectileExpiredEvent> expirations = _projectileFrameEvents.Expirations;
            for (int i = 0; i < expirations.Count; i++)
            {
                CombatProjectileExpiredEvent expiration = expirations[i];
                StartEffectTimelineFromProjectileExpiration(expiration, frame);
                CreateArcMineFromProjectileExpiration(world, expiration, frame);
                CreateRuneTurretFromProjectileExpiration(world, expiration, frame);
                CreatePoisonBottlePoolFromProjectileExpiration(world, expiration, frame);
                RecordLaserPathVfxFromProjectileExpiration(expiration, frame);
            }

            // Physical attack effects are released after the shared causal judgment gate.

            _activeProjectileViews.Clear();
            if (_projectileRuntime.ActiveCount > 0)
                _projectileRuntime.CopyActiveViewsTo(_activeProjectileViews);

            _projectileVfxBridge?.ProcessFrame(frame, _projectileFrameEvents, _activeProjectileViews);

            ApplyTornadoDamageTicks(world, frame, _activeProjectileViews);
        }

        private bool TryRegisterHookChainPullFromProjectileImpact(
            CharacterTestCharacterRuntimeWorld world,
            CombatProjectileImpactEvent impact,
            RuntimeFrame frame)
        {
            if (!CharacterTestFireballProjectileAdapter.IsHookChainProjectile(impact.Projectile)
                || !impact.ResolveResult.IsAcceptedDamage)
            {
                return false;
            }

            CharacterTestCharacterEntity caster = _entityLookup.FindEntityByCombatId(impact.Projectile.SourceEntityId);
            CharacterTestCharacterEntity target = _entityLookup.FindEntityByCombatId(impact.ResolveResult.TargetId);
            if (caster == null
                || target == null
                || caster.Id.Value == target.Id.Value
                || !caster.BattleState.CountsAsAlive
                || !target.BattleState.CanReceiveHpDamage)
            {
                return false;
            }

            long startFrame = frame.Value + 1L;
            long endFrame = startFrame + HookChainPullDurationFrames;
            var state = new ActiveHookPullState(
                caster.Id.Value,
                target.Id.Value,
                startFrame,
                endFrame,
                impact.Projectile.ActionInstanceId,
                impact.Projectile.ProjectileId,
                CharacterTestCharacterActionConfigSource.HookChainProjectileImpactStableEventId,
                HookChainPullStopDistanceMillimeters,
                HookChainPullAnchorForwardOffsetMillimeters,
                HookChainPullAccelerationMillimetersPerSecondSquared,
                HookChainPullTerminalSpeedMillimetersPerSecond);

            for (int i = 0; i < _activeHookPulls.Count; i++)
            {
                ActiveHookPullState active = _activeHookPulls[i];
                if (active.ProjectileId == state.ProjectileId
                    || (active.CasterEntityId == state.CasterEntityId
                        && active.TargetEntityId == state.TargetEntityId))
                {
                    _activeHookPulls[i] = state;
                    return true;
                }
            }

            _activeHookPulls.Add(state);
            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                caster.Id.Value,
                "hookChainPull registered target=" + target.Id.Value.ToString(CultureInfo.InvariantCulture)
                + " projectile=" + impact.Projectile.ProjectileId.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        private void CreateArcMineFromProjectileImpact(
            CharacterTestCharacterRuntimeWorld world,
            CombatProjectileImpactEvent impact,
            RuntimeFrame frame)
        {
            if (!IsArcMineProjectile(impact.Projectile))
                return;

            CharacterTestCharacterEntity caster = _entityLookup.FindEntityByCombatId(impact.Projectile.SourceEntityId);
            if (caster == null)
                return;

            if (CharacterTestFireballProjectileAdapter.IsArcMineCarrierProjectile(impact.Projectile)
                && IsArcMineCarrierHostileImpact(caster, impact))
            {
                bool spawned = TrySpawnArcMineDropProjectileFromCarrierImpact(world, caster, impact, frame);
                GetVerboseCombatTraceRecorder()?.AttackCombat(
                    frame,
                    caster.Id.Value,
                    "arcMineCarrier hostileImpact dropSpawned=" + spawned.ToString()
                    + " projectile=" + impact.Projectile.ProjectileId.ToString(CultureInfo.InvariantCulture));
                return;
            }

            string deployMode = CharacterTestFireballProjectileAdapter.IsArcMineDropProjectile(impact.Projectile)
                ? CharacterTestArcMineRuntime.ProjectileDropImpactDeployMode
                : CharacterTestArcMineRuntime.ProjectileImpactDeployMode;
            CreateArcMineAtProjectilePoint(
                world,
                caster,
                frame,
                impact.Projectile,
                impact.PhysicsHit.Point,
                deployMode,
                "impact");
        }

        private void CreateArcMineFromProjectileExpiration(
            CharacterTestCharacterRuntimeWorld world,
            CombatProjectileExpiredEvent expiration,
            RuntimeFrame frame)
        {
            if (!IsArcMineProjectile(expiration.Projectile))
                return;

            CharacterTestCharacterEntity caster = _entityLookup.FindEntityByCombatId(expiration.Projectile.SourceEntityId);
            if (caster == null)
                return;

            string deployMode = CharacterTestFireballProjectileAdapter.IsArcMineDropProjectile(expiration.Projectile)
                ? CharacterTestArcMineRuntime.ProjectileDropExpirationDeployMode
                : CharacterTestArcMineRuntime.ProjectileExpirationDeployMode;
            CreateArcMineAtProjectilePoint(
                world,
                caster,
                frame,
                expiration.Projectile,
                expiration.Projectile.Position,
                deployMode,
                "expire");
        }

        private bool IsArcMineCarrierHostileImpact(
            CharacterTestCharacterEntity caster,
            CombatProjectileImpactEvent impact)
        {
            if (impact.ResolveResult.Kind != HitResolveKind.Damage)
                return false;

            CharacterTestCharacterEntity target = _entityLookup.FindEntityByCombatId(impact.ResolveResult.TargetId);
            return IsArcMineHostileAlive(caster, target);
        }

        private bool TrySpawnArcMineDropProjectileFromCarrierImpact(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity caster,
            CombatProjectileImpactEvent impact,
            RuntimeFrame frame)
        {
            if (!_projectileDefinitions.TryGetByStableId(
                    CharacterTestFireballProjectileAdapter.ArcMineDropStableId,
                    out CombatProjectileDefinition dropDefinition))
            {
                ReportHitTraceError(
                    caster.Id.Value,
                    CharacterTestCharacterActionConfigSource.ArcMineActionId,
                    CharacterTestFireballProjectileAdapter.ArcMineDropStableId,
                    string.Empty,
                    impact.Projectile.LocalFrame,
                    "ArcMineDropProjectileDefinitionMissing",
                    "Arc Mine drop projectile definition is missing.");
                return false;
            }

            FixVector3 origin = impact.PhysicsHit.Point;
            FixVector3 forward = ResolveArcMineDeployForward(caster, impact.Projectile.Direction);
            FixVector3 targetPoint = ProjectPoisonBottlePoolCenterToGround(origin)
                + new FixVector3(
                    forward.X * CharacterTestFireballProjectileAdapter.ArcMineDropHorizontalCarry,
                    Fix64.Zero,
                    forward.Z * CharacterTestFireballProjectileAdapter.ArcMineDropHorizontalCarry);
            if (!CombatProjectileBallistics.TrySolveLaunchArc(
                    origin,
                    targetPoint,
                    dropDefinition.Motion.SpeedPerFrame,
                    dropDefinition.Motion.GravityPerFrameSquared,
                    dropDefinition.Motion.MaxLaunchAngleTangent,
                    dropDefinition.Motion.LifetimeFrames,
                    out FixVector3 initialVelocity,
                    out Fix64 launchSpeedPerFrame,
                    out int effectiveLifetimeFrames)
                || !initialVelocity.TryNormalize(out FixVector3 direction))
            {
                ReportHitTraceWarning(
                    caster.Id.Value,
                    CharacterTestCharacterActionConfigSource.ArcMineActionId,
                    CharacterTestFireballProjectileAdapter.ArcMineDropStableId,
                    string.Empty,
                    impact.Projectile.LocalFrame,
                    "ArcMineDropProjectileUnreachable",
                    "Arc Mine drop projectile could not solve a downward ballistic path; deploying at projected impact point.");
                CreateArcMineAtProjectilePoint(
                    world,
                    caster,
                    frame,
                    impact.Projectile,
                    origin,
                    CharacterTestArcMineRuntime.ProjectileDropImpactDeployMode,
                    "drop_fallback");
                return false;
            }

            CombatFrame spawnFrame = ToCombatFrame(new RuntimeFrame(frame.Value + 1L));
            var dropRequest = new CombatProjectileSpawnRequest(
                spawnFrame,
                dropDefinition.DefinitionId,
                impact.Projectile.SourceEntityId,
                origin,
                direction,
                true,
                targetPoint,
                impact.Projectile.ActionId,
                impact.Projectile.ActionInstanceId,
                CharacterTestCharacterActionConfigSource.ArcMineDropProjectileImpactStableEventId,
                impact.Projectile.LocalFrame,
                impact.Projectile.SpawnSequence,
                effectiveLifetimeFrames);

            var root = world.DoubleEdgeState.ProjectileSource(impact.Projectile.ProjectileId, impact.Projectile.SpawnSequence);
            var parent = world.DoubleEdgeState.SecondBatchProducer(root, 1, impact.Projectile.ProjectileId, impact.Projectile.SpawnSequence);
            if (!EnqueueAccessoryProjectile(dropRequest, root, parent, frame.Value, false,
                _projectileCausalParent.Source.Matches(parent) ? _projectileCausalParent.Key : default)) return false;

            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                caster.Id.Value,
                "arcMineDrop spawn projectile=" + CharacterTestCombatTraceText.EmptyAsNone(dropDefinition.StableId)
                + " lifetime=" + effectiveLifetimeFrames.ToString(CultureInfo.InvariantCulture)
                + " launchSpeed=" + launchSpeedPerFrame.ToString());
            return true;
        }

        private void CreateArcMineAtProjectilePoint(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity caster,
            RuntimeFrame frame,
            CombatProjectileSnapshot projectile,
            FixVector3 point,
            string deployMode,
            string reason)
        {
            if (!AllowsProjectileAttack(projectile)) return;
            FixVector3 center = ProjectPoisonBottlePoolCenterToGround(point);
            FixVector3 forward = ResolveArcMineDeployForward(caster, projectile.Direction);
            CharacterTestArcMineDeployResult result = _arcMineRuntime.CreateMine(
                world,
                _physicsRuntime,
                caster,
                frame,
                center,
                forward,
                deployMode,
                CharacterTestCharacterActionConfigSource.ArcMineDeployStableEventId,
                projectile.LocalFrame,
                projectile.SpawnSequence,
                projectile.ActionInstanceId);
            if (result.Success) BindProjectileChildProducer(4, result.Deployed.RuntimeId, projectile, frame);
            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                caster.Id.Value,
                "arcMineProjectile " + CharacterTestCombatTraceText.EmptyAsNone(reason)
                + " created=" + result.Success.ToString()
                + " mode=" + CharacterTestCombatTraceText.EmptyAsNone(deployMode)
                + " projectile=" + projectile.ProjectileId.ToString(CultureInfo.InvariantCulture));
        }

        private void ProcessHookChainPulls(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame)
        {
            if (_activeHookPulls.Count == 0 || world == null || _physicsRuntime?.World == null)
                return;

            for (int i = _activeHookPulls.Count - 1; i >= 0; i--)
            {
                ActiveHookPullState pull = _activeHookPulls[i];
                if (frame.Value < pull.StartFrame)
                    continue;
                if (frame.Value > pull.EndFrame || !TryApplyHookChainPull(pull))
                    _activeHookPulls.RemoveAt(i);
            }
        }

        private bool TryApplyHookChainPull(ActiveHookPullState pull)
        {
            CharacterTestCharacterEntity caster = _entityLookup.FindEntity(pull.CasterEntityId);
            CharacterTestCharacterEntity target = _entityLookup.FindEntity(pull.TargetEntityId);
            if (caster == null
                || target == null
                || !caster.BattleState.CountsAsAlive
                || !target.BattleState.CanReceiveHpDamage
                || !TryResolveCharacterBodyCenter(caster, out FixVector3 casterCenter)
                || !TryResolveCharacterBodyCenter(target, out FixVector3 targetCenter))
            {
                return false;
            }

            ResolveCharacterForwardScaled(caster, out int forwardX, out int forwardZ);
            FixVector3 anchor = casterCenter + new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                    CharacterTestDeterministicMath.MulDivRoundToInt(
                        forwardX,
                        pull.AnchorForwardOffsetMillimeters,
                        HookChainDirectionScale)),
                Fix64.Zero,
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                    CharacterTestDeterministicMath.MulDivRoundToInt(
                        forwardZ,
                        pull.AnchorForwardOffsetMillimeters,
                        HookChainDirectionScale)));
            if (!ResolveHookChainHorizontalDirection(
                    anchor - targetCenter,
                    out int directionX,
                    out int directionZ,
                    out int horizontalDistanceMillimeters))
            {
                return false;
            }

            if (horizontalDistanceMillimeters <= pull.StopDistanceMillimeters)
                return false;

            int radialVelocity = CharacterTestDeterministicMath.DivideRoundToInt(
                (long)target.HorizontalVelocityXMillimetersPerSecond * directionX
                + (long)target.HorizontalVelocityYMillimetersPerSecond * directionZ,
                HookChainDirectionScale);
            int terminalScale = ResolveHookChainTerminalScale(
                pull.TerminalSpeedMillimetersPerSecond,
                radialVelocity);
            int accelerationMagnitude = CharacterTestDeterministicMath.MulDivRoundToInt(
                pull.AccelerationMillimetersPerSecondSquared,
                terminalScale,
                CharacterTestDeterministicUnits.ScaleFactorScale);
            if (accelerationMagnitude <= 0)
                return true;

            int accelerationX = CharacterTestDeterministicMath.MulDivRoundToInt(
                directionX,
                accelerationMagnitude,
                HookChainDirectionScale);
            int accelerationZ = CharacterTestDeterministicMath.MulDivRoundToInt(
                directionZ,
                accelerationMagnitude,
                HookChainDirectionScale);
            if (accelerationX == 0 && accelerationZ == 0)
                return true;

            target.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ExternalForceRaw(
                accelerationX,
                0,
                accelerationZ,
                CharacterTestCharacterMotionRequestSpace.World,
                CharacterTestCharacterActionConfigSource.HookChainActionId,
                pull.StableEventId,
                bypassGroundDamping: true));
            return true;
        }

        private bool TryResolveCharacterBodyCenter(
            CharacterTestCharacterEntity entity,
            out FixVector3 center)
        {
            if (entity == null || _physicsRuntime?.World == null)
            {
                center = FixVector3.Zero;
                return false;
            }

            if (_physicsRuntime.World.TryGetBody(_physicsRuntime.GetBodyId(entity), out CombatPhysicsBody body))
            {
                center = body.Position;
                return true;
            }

            center = new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.YMillimeters)
                    + _physicsRuntime.GetCharacterCenterOffsetY(entity),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.ZMillimeters));
            return true;
        }

        private static void ResolveCharacterForwardScaled(
            CharacterTestCharacterEntity entity,
            out int forwardX,
            out int forwardZ)
        {
            int yaw = entity != null ? entity.Transform.YawMilliDegrees : 0;
            CharacterTestDeterministicMath.SinCosMilliDegrees(yaw, out int sin, out int cos);
            forwardX = sin;
            forwardZ = cos;
            if (forwardX == 0 && forwardZ == 0)
                forwardZ = HookChainDirectionScale;
        }

        private static bool ResolveHookChainHorizontalDirection(
            FixVector3 horizontalDelta,
            out int directionX,
            out int directionZ,
            out int horizontalDistanceMillimeters)
        {
            int deltaX = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(horizontalDelta.X);
            int deltaZ = CharacterTestDeterministicUnits.Fix64MetersToMillimeters(horizontalDelta.Z);
            horizontalDistanceMillimeters = CharacterTestDeterministicMath.Magnitude(deltaX, deltaZ);
            CharacterTestDeterministicMath.NormalizeToScale(
                deltaX,
                deltaZ,
                HookChainDirectionScale,
                out directionX,
                out directionZ);
            return directionX != 0 || directionZ != 0;
        }

        private static int ResolveHookChainTerminalScale(
            int terminalSpeedMillimetersPerSecond,
            int radialVelocityMillimetersPerSecond)
        {
            if (terminalSpeedMillimetersPerSecond <= 0)
                return CharacterTestDeterministicUnits.ScaleFactorScale;
            if (radialVelocityMillimetersPerSecond <= 0)
                return CharacterTestDeterministicUnits.ScaleFactorScale;
            if (radialVelocityMillimetersPerSecond >= terminalSpeedMillimetersPerSecond)
                return 0;

            return CharacterTestDeterministicMath.MulDivRoundToInt(
                terminalSpeedMillimetersPerSecond - radialVelocityMillimetersPerSecond,
                CharacterTestDeterministicUnits.ScaleFactorScale,
                terminalSpeedMillimetersPerSecond);
        }

        private void CreatePoisonBottlePoolFromProjectileImpact(
            CharacterTestCharacterRuntimeWorld world,
            CombatProjectileImpactEvent impact,
            RuntimeFrame frame)
        {
            if (!IsPoisonBottleProjectile(impact.Projectile) || !impact.ResolveResult.IsAcceptedDamage)
                return;

            CharacterTestCharacterEntity caster = _entityLookup.FindEntityByCombatId(impact.Projectile.SourceEntityId);
            if (caster == null)
                return;

            if (!AllowsProjectileAttack(impact.Projectile)) return;
            FixVector3 center = ResolvePoisonBottlePoolCenterFromImpact(impact);
            FixVector3 forward = NormalizeProjectileDirection(impact.Projectile.Direction);
            CharacterTestMechanicSourceCreateResult result = _poisonBottlePoolRuntime.CreatePool(
                world,
                _physicsRuntime,
                caster,
                center,
                forward,
                frame,
                CharacterTestCharacterActionConfigSource.PoisonBottlePoolVfxStableEventId,
                impact.Projectile.LocalFrame,
                impact.Projectile.SpawnSequence);
            if (result.Success) BindProjectileChildProducer(6, result.RuntimeSourceId, impact.Projectile, frame);
            SpawnPoisonBottleShatterVfx(
                caster.Id.Value,
                center,
                forward,
                impact.Projectile.SpawnSequence,
                impact.Projectile.LocalFrame,
                frame);
            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                caster.Id.Value,
                "poisonBottlePool impact created=" + result.Success.ToString());
        }

        private void CreatePoisonBottlePoolFromProjectileExpiration(
            CharacterTestCharacterRuntimeWorld world,
            CombatProjectileExpiredEvent expiration,
            RuntimeFrame frame)
        {
            if (!IsPoisonBottleProjectile(expiration.Projectile))
                return;

            CharacterTestCharacterEntity caster = _entityLookup.FindEntityByCombatId(expiration.Projectile.SourceEntityId);
            if (caster == null)
                return;

            if (!AllowsProjectileAttack(expiration.Projectile)) return;
            FixVector3 center = ProjectPoisonBottlePoolCenterToGround(expiration.Projectile.Position);
            FixVector3 forward = NormalizeProjectileDirection(expiration.Projectile.Direction);
            CharacterTestMechanicSourceCreateResult result = _poisonBottlePoolRuntime.CreatePool(
                world,
                _physicsRuntime,
                caster,
                center,
                forward,
                frame,
                CharacterTestCharacterActionConfigSource.PoisonBottlePoolVfxStableEventId,
                expiration.Projectile.LocalFrame,
                expiration.Projectile.SpawnSequence);
            if (result.Success) BindProjectileChildProducer(6, result.RuntimeSourceId, expiration.Projectile, frame);
            SpawnPoisonBottleShatterVfx(
                caster.Id.Value,
                center,
                forward,
                expiration.Projectile.SpawnSequence,
                expiration.Projectile.LocalFrame,
                frame);
            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                caster.Id.Value,
                "poisonBottlePool expire created=" + result.Success.ToString());
        }

        private FixVector3 ResolvePoisonBottlePoolCenterFromImpact(
            CombatProjectileImpactEvent impact)
        {
            CharacterTestCharacterEntity target = _entityLookup.FindEntityByCombatId(impact.ResolveResult.TargetId);
            if (target == null)
                return ProjectPoisonBottlePoolCenterToGround(impact.PhysicsHit.Point);

            CombatBodyId bodyId = _physicsRuntime.GetBodyId(target);
            if (_physicsRuntime.World.TryGetBody(bodyId, out CombatPhysicsBody body))
            {
                return new FixVector3(
                    body.Position.X,
                    body.Position.Y - _physicsRuntime.GetCharacterCenterOffsetY(target),
                    body.Position.Z);
            }

            return new FixVector3(
                ToFix64(target.Transform.X),
                ToFix64(target.Transform.Y),
                ToFix64(target.Transform.Z));
        }

        private static FixVector3 ProjectPoisonBottlePoolCenterToGround(FixVector3 point)
        {
            return new FixVector3(point.X, Fix64.Zero, point.Z);
        }

        private static bool IsPoisonBottleProjectile(CombatProjectileSnapshot projectile)
        {
            return CharacterTestFireballProjectileAdapter.IsPoisonBottleProjectile(projectile);
        }

        private static bool IsArcMineProjectile(CombatProjectileSnapshot projectile)
        {
            return CharacterTestFireballProjectileAdapter.IsArcMineProjectile(projectile);
        }

        private static FixVector3 ResolveArcMineDeployForward(
            CharacterTestCharacterEntity caster,
            FixVector3 fallbackDirection)
        {
            if (caster != null)
            {
                FixVector3 casterForward = CharacterTestFireballProjectileAdapter.ResolveDirection(caster);
                casterForward = new FixVector3(casterForward.X, Fix64.Zero, casterForward.Z);
                if (casterForward.TryNormalize(out FixVector3 normalizedCasterForward))
                    return normalizedCasterForward;
            }

            return NormalizeProjectileDirection(fallbackDirection);
        }

        private static FixVector3 NormalizeProjectileDirection(FixVector3 direction)
        {
            FixVector3 horizontal = new FixVector3(direction.X, Fix64.Zero, direction.Z);
            return horizontal.TryNormalize(out FixVector3 normalized)
                ? normalized
                : new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
        }

        private void SpawnPoisonBottlePoolRuntimeVfx(RuntimeFrame frame)
        {
            _poisonBottleCreatedPools.Clear();
            _poisonBottlePoolRuntime.DrainLastCreatedPools(_poisonBottleCreatedPools);
            for (int i = 0; i < _poisonBottleCreatedPools.Count; i++)
            {
                CharacterTestPoisonBottlePoolCreated pool = _poisonBottleCreatedPools[i];
                SpawnPoisonBottlePoolVfx(pool, frame);
            }

            _poisonBottleCreatedPools.Clear();
        }

        private void SpawnPoisonBottlePoolVfx(
            CharacterTestPoisonBottlePoolCreated pool,
            RuntimeFrame frame)
        {
            if (_vfxRuntime == null || !pool.SourceCreateResult.Success)
                return;

            CharacterTestMechanicSourceDefinition definition = pool.SourceCreateResult.Definition;
            for (int i = 0; i < definition.EffectCount; i++)
            {
                CharacterTestMechanicEffectDefinition effect = definition.GetEffect(i);
                if (effect.Kind != CharacterTestMechanicEffectKind.SpawnVfxOnCreate)
                    continue;

                SpawnPoisonBottleGameplayVfx(
                    pool.CasterEntityId,
                    pool.Center,
                    pool.Forward,
                    pool.SourceOrder,
                    pool.LocalFrame,
                    effect.VfxDefinitionId,
                    (int)effect.VfxLifetimeFrames,
                    effect.TraceId,
                    frame);
            }
        }

        private void SpawnPoisonBottleShatterVfx(
            int casterEntityId,
            FixVector3 center,
            FixVector3 forward,
            int sourceOrder,
            int localFrame,
            RuntimeFrame frame)
        {
            SpawnPoisonBottleGameplayVfx(
                casterEntityId,
                center,
                forward,
                sourceOrder,
                localFrame,
                CharacterTestVfxIds.PoisonBottleShatterDefinitionId,
                CharacterTestVfxIds.PoisonBottleShatterLifetimeFrames,
                CharacterTestCharacterActionConfigSource.PoisonBottleProjectileImpactStableEventId,
                frame);
        }

        private void SpawnPoisonBottleGameplayVfx(
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

        private void StopMeteorBarrageTelegraphFromAreaPulseTrigger(
            CombatProjectileAreaPulseTriggerEvent trigger,
            RuntimeFrame frame)
        {
            if (!CharacterTestFireballProjectileAdapter.IsMeteorBarrageProjectile(trigger.Projectile))
                return;

            if (TryResolveMeteorBarrageIndexFromProjectile(trigger.Projectile, out int meteorIndex))
                StopMeteorBarrageTelegraphVfx(trigger.Projectile, meteorIndex, frame);
        }

        private void RecordLaserPathVfxFromProjectileImpact(
            CombatProjectileImpactEvent impact,
            RuntimeFrame frame)
        {
            if (!IsLaserLanceProjectile(impact.Projectile) || !impact.ResolveResult.IsAcceptedDamage)
                return;

            _laserPathHitProjectileIds.Add(impact.Projectile.ProjectileId);
            FixVector3 endpoint = impact.ResolveResult.HitPointWorld.Equals(FixVector3.Zero)
                ? impact.PhysicsHit.Point
                : impact.ResolveResult.HitPointWorld;
            RecordLaserPathVfx(
                impact.Projectile,
                endpoint,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(impact.ResolveResult.TargetId),
                hit: true,
                frame);
        }

        private void RecordLaserPathVfxFromProjectileExpiration(
            CombatProjectileExpiredEvent expiration,
            RuntimeFrame frame)
        {
            if (!IsLaserLanceProjectile(expiration.Projectile)
                || _laserPathHitProjectileIds.Contains(expiration.Projectile.ProjectileId))
            {
                return;
            }

            FixVector3 direction = NormalizeLaserPathDirection(expiration.Projectile.Direction);
            FixVector3 endpoint = expiration.Projectile.Origin
                + direction * CharacterTestFireballProjectileAdapter.LaserLanceRange;
            RecordLaserPathVfx(expiration.Projectile, endpoint, targetEntityId: 0, hit: false, frame);
        }

        private void RecordLaserPathVfx(
            CombatProjectileSnapshot projectile,
            FixVector3 endpoint,
            int targetEntityId,
            bool hit,
            RuntimeFrame frame)
        {
            FixVector3 origin = projectile.Origin;
            FixVector3 delta = endpoint - origin;
            Fix64 length = delta.LengthSquared().Sqrt();
            FixVector3 direction = NormalizeLaserPathDirection(delta);
            if (length <= Fix64.Zero)
            {
                direction = NormalizeLaserPathDirection(projectile.Direction);
                length = CharacterTestFireballProjectileAdapter.LaserLanceRange;
                endpoint = origin + direction * length;
            }

            _laserPathVfxEvents.Add(new CharacterTestLaserPathVfxEvent(
                frame,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(projectile.SourceEntityId),
                targetEntityId,
                projectile.ProjectileId,
                projectile.ActionInstanceId,
                projectile.LocalFrame,
                origin,
                direction,
                endpoint,
                ToMillimeters(length),
                hit));
        }

        private static bool IsLaserLanceProjectile(CombatProjectileSnapshot projectile)
        {
            return projectile.DefinitionId == CharacterTestFireballProjectileAdapter.LaserLanceDefinitionId;
        }

        private static FixVector3 NormalizeLaserPathDirection(FixVector3 direction)
        {
            return direction.TryNormalize(out FixVector3 normalized)
                ? normalized
                : new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
        }

        private static int ToMillimeters(Fix64 length)
        {
            if (length <= Fix64.Zero)
                return 0;

            long millimeters = checked((length.RawValue * 1000L + Fix64.Scale / 2L) / Fix64.Scale);
            return millimeters > int.MaxValue ? int.MaxValue : (int)millimeters;
        }

        private void RecordProjectilePhysicalInteractions(
            RuntimeFrame frame,
            IReadOnlyList<CombatProjectilePhysicalEvent> physicalEvents)
        {
            if (physicalEvents == null || physicalEvents.Count == 0)
                return;

            for (int i = 0; i < physicalEvents.Count; i++)
            {
                CombatProjectilePhysicalEvent physicalEvent = physicalEvents[i];
                if (!AllowsProjectileAttack(physicalEvent.Projectile)) continue;
                if (physicalEvent.Radius <= Fix64.Zero || physicalEvent.ImpactPower <= 0)
                    continue;

                switch (physicalEvent.Kind)
                {
                    case CombatProjectilePhysicalEventKind.ActiveFrame:
                        if (IsTornadoPhysicalEvent(physicalEvent))
                            RecordTornadoAttractionInteraction(frame, physicalEvent);
                        else if (IsSwordWavePhysicalEvent(physicalEvent))
                            RecordSwordWaveDirectionalImpulseInteraction(frame, physicalEvent);
                        break;
                    case CombatProjectilePhysicalEventKind.AreaPulse:
                    case CombatProjectilePhysicalEventKind.Expiration:
                        RecordAreaProjectilePhysicalInteraction(frame, physicalEvent);
                        break;
                }
            }
        }

        private void RecordAreaProjectilePhysicalInteraction(
            RuntimeFrame frame,
            CombatProjectilePhysicalEvent physicalEvent)
        {
            CombatProjectileSnapshot projectile = physicalEvent.Projectile;
            int sourceDomain = ResolveProjectilePhysicalSourceDomain(physicalEvent);
            if (TryResolveFireballExplosionDefinition(projectile, out CombatProjectileDefinition explosionDefinition))
            {
                if (!TryReserveProjectileInteraction(frame, projectile))
                    return;

                RecordExplosionInteraction(frame, physicalEvent, explosionDefinition, sourceDomain);
                return;
            }

            if (TryResolveMeteorBarrageDefinition(projectile, out CombatProjectileDefinition meteorDefinition))
            {
                if (!TryReserveProjectileInteraction(frame, projectile))
                    return;

                RecordMeteorBarrageInteraction(frame, physicalEvent, meteorDefinition, sourceDomain);
            }
        }

        private void RecordTornadoAttractionInteraction(
            RuntimeFrame frame,
            CombatProjectilePhysicalEvent physicalEvent)
        {
            CombatProjectileSnapshot projectile = physicalEvent.Projectile;
            int traceId = CharacterTestCharacterActionConfigSource.TornadoAttractionStableEventId;
            int acceleration =
                CharacterTestInteractionFieldProfiles.ResolveTornadoAttractionAcceleration(physicalEvent.ImpactPower);
            _interactionHistory.Record(new CharacterTestInteractionEvent(
                frame,
                CharacterTestInteractionKind.PhysicalField,
                CharacterTestCharacterActionConfigSource.TornadoAttractionStableEventId,
                projectile.ProjectileId,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(projectile.SourceEntityId),
                targetEntityId: 0,
                physicalEvent.Position,
                physicalEvent.Direction,
                physicalEvent.Radius,
                acceleration,
                traceId,
                CharacterTestInteractionSourceDomains.CombatProjectileActiveField,
                CharacterTestInteractionFieldProfiles.TornadoAttractionCapsule(physicalEvent.ImpactPower)));
        }

        private void RecordSwordWaveDirectionalImpulseInteraction(
            RuntimeFrame frame,
            CombatProjectilePhysicalEvent physicalEvent)
        {
            CombatProjectileSnapshot projectile = physicalEvent.Projectile;
            int traceId = CharacterTestCharacterActionConfigSource.SwordWaveProjectileImpactStableEventId;
            FixVector3 direction = NormalizeProjectileDirection(physicalEvent.Direction);
            Fix64 segmentHalfLength =
                CharacterTestInteractionFieldProfiles.SwordWaveDirectionalImpulseCapsuleHalfLength;
            FixVector3 anchor = physicalEvent.Position - direction * segmentHalfLength;
            _interactionHistory.Record(new CharacterTestInteractionEvent(
                frame,
                CharacterTestInteractionKind.PhysicalField,
                CharacterTestCharacterActionConfigSource.SwordWaveProjectileImpactStableEventId,
                projectile.ProjectileId,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(projectile.SourceEntityId),
                targetEntityId: 0,
                anchor,
                direction,
                physicalEvent.Radius,
                physicalEvent.ImpactPower,
                traceId,
                CharacterTestInteractionSourceDomains.CombatProjectileDirectionalField,
                CharacterTestInteractionFieldProfiles.SwordWaveDirectionalImpulse(physicalEvent.ImpactPower)));
        }

        private static bool IsTornadoPhysicalEvent(CombatProjectilePhysicalEvent physicalEvent)
        {
            CombatProjectileSnapshot projectile = physicalEvent.Projectile;
            return projectile.DefinitionId == CharacterTestFireballProjectileAdapter.TornadoDefinitionId;
        }

        private static bool IsSwordWavePhysicalEvent(CombatProjectilePhysicalEvent physicalEvent)
        {
            CombatProjectileSnapshot projectile = physicalEvent.Projectile;
            return projectile.DefinitionId == CharacterTestFireballProjectileAdapter.SwordWaveDefinitionId;
        }

        private static int ResolveProjectilePhysicalSourceDomain(CombatProjectilePhysicalEvent physicalEvent)
        {
            return physicalEvent.Kind == CombatProjectilePhysicalEventKind.Expiration
                || physicalEvent.AreaPulseTriggerKind == CombatProjectileAreaPulseTriggerKind.Expiration
                    ? CharacterTestInteractionSourceDomains.CombatProjectileExpiration
                    : CharacterTestInteractionSourceDomains.CombatProjectileAreaPulse;
        }

        private void RecordTornadoAttractionInteractions(
            RuntimeFrame frame,
            IReadOnlyList<CombatProjectileActiveView> activeProjectiles)
        {
            if (activeProjectiles == null || activeProjectiles.Count == 0)
                return;

            int traceId = CharacterTestCharacterActionConfigSource.TornadoAttractionStableEventId;
            for (int i = 0; i < activeProjectiles.Count; i++)
            {
                CombatProjectileActiveView projectile = activeProjectiles[i];
                if (projectile.DefinitionId != CharacterTestFireballProjectileAdapter.TornadoDefinitionId)
                {
                    continue;
                }

                _interactionHistory.Record(new CharacterTestInteractionEvent(
                    frame,
                    CharacterTestInteractionKind.PhysicalField,
                    CharacterTestCharacterActionConfigSource.TornadoAttractionStableEventId,
                    projectile.ProjectileId,
                    CharacterTestCombatEntityLookup.ResolveCharacterEntityId(projectile.SourceEntityId),
                    targetEntityId: 0,
                    projectile.Position,
                    projectile.Direction,
                    CharacterTestInteractionFieldProfiles.TornadoAttractionRadius,
                    CharacterTestInteractionFieldProfiles.TornadoAttractionAccelerationMillimetersPerSecondSquared,
                    traceId,
                    CharacterTestInteractionSourceDomains.CombatProjectileActiveField,
                    CharacterTestInteractionFieldProfiles.TornadoAttractionCapsule()));
            }
        }

        private void RecordSwordWaveDirectionalImpulseInteractions(
            RuntimeFrame frame,
            IReadOnlyList<CombatProjectileActiveView> activeProjectiles)
        {
            if (activeProjectiles == null || activeProjectiles.Count == 0)
                return;

            int traceId = CharacterTestCharacterActionConfigSource.SwordWaveProjectileImpactStableEventId;
            CharacterTestInteractionPhysicalField physicalField =
                CharacterTestInteractionFieldProfiles.SwordWaveDirectionalImpulse(
                    CharacterTestFireballProjectileAdapter.SwordWaveImpactPower);
            Fix64 segmentHalfLength =
                CharacterTestInteractionFieldProfiles.SwordWaveDirectionalImpulseCapsuleHalfLength;
            for (int i = 0; i < activeProjectiles.Count; i++)
            {
                CombatProjectileActiveView projectile = activeProjectiles[i];
                if (!IsSwordWaveProjectile(projectile))
                    continue;

                FixVector3 direction = NormalizeProjectileDirection(projectile.Direction);
                FixVector3 anchor = projectile.Position - direction * segmentHalfLength;
                _interactionHistory.Record(new CharacterTestInteractionEvent(
                    frame,
                    CharacterTestInteractionKind.PhysicalField,
                    CharacterTestCharacterActionConfigSource.SwordWaveProjectileImpactStableEventId,
                    projectile.ProjectileId,
                    CharacterTestCombatEntityLookup.ResolveCharacterEntityId(projectile.SourceEntityId),
                    targetEntityId: 0,
                    anchor,
                    direction,
                    CharacterTestInteractionFieldProfiles.SwordWaveDirectionalImpulseRadius,
                    CharacterTestFireballProjectileAdapter.SwordWaveImpactPower,
                    traceId,
                    CharacterTestInteractionSourceDomains.CombatProjectileDirectionalField,
                    physicalField));
            }
        }

        private void ApplyTornadoDamageTicks(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            IReadOnlyList<CombatProjectileActiveView> activeProjectiles)
        {
            using (TornadoDamageTickMarker.Auto())
            {
                if (world == null || _physicsRuntime?.World == null || activeProjectiles == null || activeProjectiles.Count == 0)
                    return;

                int tickingProjectileCount = CountTornadoDamageTickProjectiles(activeProjectiles);
                if (tickingProjectileCount == 0)
                    return;

                EnsureTornadoDamageQueryResultCapacity(tickingProjectileCount);

                int actionId = StablePositiveId(CharacterTestCharacterActionConfigSource.TornadoActionId);
                int traceId = CharacterTestCharacterActionConfigSource.TornadoProjectileTickStableEventId;
                int startResultCount = _results.Count;
                for (int i = 0; i < activeProjectiles.Count; i++)
                {
                    CombatProjectileActiveView projectile = activeProjectiles[i];
                    if (!IsTornadoDamageTickProjectile(projectile))
                        continue;

                    CharacterTestCharacterEntity source = _entityLookup.FindEntityByCombatId(projectile.SourceEntityId);
                    var frozenActor = _entityLookup.AccessoryState?.FindProjectileActor(projectile.ProjectileId, projectile.SpawnSequence);
                    if (source != null && frozenActor != null && source.GameplayEntityId != frozenActor.Actor) source = null;
                    if (source == null && frozenActor == null) continue;
                    _tornadoDamagePayloadCandidates.Clear();

                    int startCount = _tornadoDamageQueryResults.Count;
                    var header = new CombatQueryHeader(
                        queryId: traceId,
                        kind: CombatQueryKind.Capsule,
                        sourceEntityId: projectile.SourceEntityId,
                        traceId: traceId,
                        actionId: actionId,
                        sourceOrder: projectile.ProjectileId,
                        layerMask: CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer));
                    CombatCapsuleQuery query = CreateTornadoColumnQuery(
                        header,
                        projectile.Position,
                        CharacterTestFireballProjectileAdapter.TornadoDamageTickRadius);
                    using (TornadoDamageQueryMarker.Auto())
                    {
                        // Damage reception uses authored hurtboxes, including OBB/capsule shapes;
                        // the movement-field index contains only dynamic movement colliders.
                        _physicsRuntime.World.QueryCapsule(
                            query,
                            _tornadoDamageQueryResults,
                            includeSourceEntity: false);
                    }

                    using (TornadoDamageCandidateResolveMarker.Auto())
                    {
                        // QueryCapsule orders this appended range by CombatQueryResult.CompareTo.
                        // Within one producer/tick the remaining HitCandidate ordering fields are equal.
                        CharacterTestTornadoDamageTickSelection.AddFirstEligiblePerTarget(
                            _tornadoDamageQueryResults, startCount, _tornadoDamageSelectedTargets,
                            hit => TryAddTornadoDamageTickCandidate(projectile, source, hit, frame, actionId, traceId));
                    }
                    _tornadoDamageHitOnceKeys.Clear();
                    _hitResolve.Resolve(_tornadoDamagePayloadCandidates, _tornadoDamageHitOnceKeys,
                        _results, ProducerRelations(frozenActor), _entityLookup);
                    _tornadoDamageHitOnceKeys.Clear();
                }
                for (int i = startResultCount; i < _results.Count; i++)
                {
                    HitResolveResult result = _results[i];
                    if (result.TraceId == traceId && result.Kind == HitResolveKind.Damage)
                        LastTornadoDamageTickTargetCount++;
                }
            }
        }

        private bool TryAddTornadoDamageTickCandidate(
            CombatProjectileActiveView projectile,
            CharacterTestCharacterEntity source,
            CombatQueryResult query,
            RuntimeFrame frame,
            int actionId,
            int traceId)
        {
            var frozenActor = _entityLookup.AccessoryState?.FindProjectileActor(projectile.ProjectileId, projectile.SpawnSequence);
            if (source == null && frozenActor == null
                || query.TargetEntityId.Equals(projectile.SourceEntityId)
                || !ProducerRelations(frozenActor).AreHostile(projectile.SourceEntityId, query.TargetEntityId))
            {
                return false;
            }

            if (!_damageResolver.TryResolveTornadoTickDamage(
                    projectile,
                    source,
                    query.TargetEntityId,
                    query.TargetColliderId,
                    _entityLookup,
                    frame,
                    out DamageProfileResolveResult damage,
                    out CharacterTestHitZoneResolution hitZone,
                    out CombatHitEventConfig hitEvent))
            {
                return false;
            }

            var candidate = new HitCandidate(
                projectile.SourceEntityId,
                query.TargetEntityId,
                actionId,
                projectile.ActionInstanceId <= 0 ? StablePositiveId(projectile.ProjectileId) : projectile.ActionInstanceId,
                traceId,
                ToCombatFrame(frame),
                query,
                damage.IncomingDamage,
                CharacterTestHitReactionResolver.CreateHitResolveReactionGate(damage),
                CharacterTestHitReactionResolver.CreateHitResolveKnockbackGate(damage),
                _entityLookup.ResolveTargetState(query.TargetEntityId)).WithProducerIdentity(
                    new CombatHitProducerIdentity(CombatHitProducerKind.ProjectilePeriodic, checked((ulong)projectile.ProjectileId),
                        checked((ulong)projectile.SpawnSequence), unchecked((ulong)frame.Value + 1)));
            var hitKey = new CharacterTestResolvedHitKey(candidate);
            SetAccessoryAwareDamagePayload(hitKey, damage, ProjectilePeriodicEvidence(projectile.ProjectileId, projectile.SpawnSequence,
                source, candidate.TargetId, unchecked((ulong)frame.Value + 1)));
            _hitZonesByHit[hitKey] = hitZone;
            if (hitEvent.OnHitBuffCount > 0)
                _onHitBuffsByHit[hitKey] = hitEvent.OnHitBuffs;
            _tornadoDamagePayloadCandidates.Add(candidate);

            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(projectile.SourceEntityId),
                "tornadoTick projectile="
                + projectile.ProjectileId.ToString(CultureInfo.InvariantCulture)
                + " target="
                + CharacterTestCombatEntityLookup.ResolveCharacterEntityId(query.TargetEntityId).ToString(CultureInfo.InvariantCulture)
                + " damage="
                + damage.IncomingDamage.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        private void EnsureTornadoDamageQueryResultCapacity(int tickingProjectileCount)
        {
            int targetCount = _physicsRuntime.World.TotalColliderCount;
            if (targetCount <= 0)
                return;

            int requiredCapacity = _tornadoDamageQueryResults.Count + targetCount * tickingProjectileCount;
            if (_tornadoDamageQueryResults.Capacity < requiredCapacity)
                _tornadoDamageQueryResults.Capacity = requiredCapacity;
        }

        private static int CountTornadoDamageTickProjectiles(IReadOnlyList<CombatProjectileActiveView> activeProjectiles)
        {
            int count = 0;
            for (int i = 0; i < activeProjectiles.Count; i++)
            {
                if (IsTornadoDamageTickProjectile(activeProjectiles[i]))
                    count++;
            }

            return count;
        }

        private static bool IsTornadoDamageTickProjectile(CombatProjectileActiveView projectile)
        {
            return projectile.DefinitionId == CharacterTestFireballProjectileAdapter.TornadoDefinitionId
                && ShouldApplyTornadoDamageTick(projectile);
        }

        private static bool IsSwordWaveProjectile(CombatProjectileActiveView projectile)
        {
            return projectile.DefinitionId == CharacterTestFireballProjectileAdapter.SwordWaveDefinitionId;
        }

        private static bool ShouldApplyTornadoDamageTick(CombatProjectileActiveView projectile)
        {
            return projectile.AgeFrames > 0
                && projectile.AgeFrames % CharacterTestFireballProjectileAdapter.TornadoDamageTickIntervalFrames == 0;
        }

        private void ProcessPoisonTicks(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame)
        {
            if (world == null || _physicsRuntime?.World == null)
                return;
            if (!world.GameplayComponentWorld.TryGetStore(
                    out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore))
            {
                return;
            }

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            int actionId = CharacterTestPoisonIds.PoisonedBuffId;
            int traceId = CharacterTestCharacterActionConfigSource.PoisonTickStableEventId;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null || !entity.BattleState.CanReceiveHpDamage || !entity.GameplayEntityId.IsValid)
                    continue;
                if (!buffStore.TryGet(entity.GameplayEntityId, out GameplayComponentBuffSetComponent buffs))
                    continue;
                if (!buffs.TryGet(CharacterTestPoisonIds.PoisonedBuffId, out GameplayComponentBuffEntry poison))
                    continue;
                if (!ShouldApplyPoisonTick(poison, frame.Value, out int tickIndex))
                    continue;

                AddPoisonTickResult(entity, poison, frame, actionId, traceId + tickIndex, tickIndex);
            }
        }

        private void AddPoisonTickResult(
            CharacterTestCharacterEntity target,
            GameplayComponentBuffEntry poison,
            RuntimeFrame frame,
            int actionId,
            int traceId,
            int tickIndex)
        {
            CombatEntityId targetCombatId = _physicsRuntime.GetEntityId(target);
            if (targetCombatId.IsNone)
                return;

            var origin = _entityLookup.AccessoryState?.FindBuffLineage(target.GameplayEntityId, poison) ?? default;
            if (_entityLookup.AccessoryState?.SuppressesAttack(origin.Binding.Root) == true) return;
            int actionInstanceId = StablePositiveId(poison.StartFrame * 1000003L + target.Id.Value);
            int localFrame = (int)Math.Min(int.MaxValue, Math.Max(0L, frame.Value - poison.StartFrame));
            var targetColliderId = new CombatColliderId(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart);
            if (!_damageResolver.TryResolveStatusTickDamage(
                    targetCombatId,
                    targetColliderId,
                    _entityLookup,
                    frame,
                    sourceEntityId: CharacterTestCombatEntityLookup.ResolveCharacterEntityId(new CombatEntityId(origin.CombatSource)),
                    actionInstanceId,
                    localFrame,
                    out DamageProfileResolveResult damage,
                    out CharacterTestHitZoneResolution hitZone))
            {
                return;
            }

            FixVector3 hitPoint = ResolveProjectileAimTargetPoint(target, Fix64.Zero);
            var result = new HitResolveResult(
                origin.CombatSource > 0 ? new CombatEntityId(origin.CombatSource) : CombatEntityId.None,
                targetCombatId,
                actionId,
                actionInstanceId,
                traceId,
                ToCombatFrame(frame),
                HitResolveKind.Damage,
                damage.IncomingDamage,
                CharacterTestHitReactionResolver.CreateHitResolveReactionGate(damage),
                CharacterTestHitReactionResolver.CreateHitResolveKnockbackGate(damage),
                hitPoint,
                FixVector3.Zero);
            var hitKey = new CharacterTestResolvedHitKey(result);
            _results.Add(result);
            SetAccessoryAwareDamagePayload(hitKey, damage, CharacterTestAccessoryHitEvidence.Periodic.WithSource(origin.Binding.Root)
                .WithCollision(origin.Binding.Source.Producer, target, origin.ActualEnemy, origin.Binding.Source, unchecked((ulong)(uint)tickIndex)));
            _hitZonesByHit[hitKey] = hitZone;
            LastPoisonTickTargetCount++;

            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                target.Id.Value,
                "poisonTick target="
                + target.Id.Value.ToString(CultureInfo.InvariantCulture)
                + " tick="
                + tickIndex.ToString(CultureInfo.InvariantCulture)
                + " damage="
                + damage.IncomingDamage.ToString(CultureInfo.InvariantCulture));
        }

        private static bool ShouldApplyPoisonTick(
            GameplayComponentBuffEntry poison,
            long frame,
            out int tickIndex)
        {
            tickIndex = 0;
            if (poison.BuffId != CharacterTestPoisonIds.PoisonedBuffId)
                return false;
            if (!poison.IsPermanent && poison.EndFrame <= frame)
                return false;

            long elapsed = frame - poison.StartFrame;
            if (elapsed <= 0L || elapsed % PoisonTickIntervalFrames != 0L)
                return false;

            long candidateIndex = elapsed / PoisonTickIntervalFrames;
            if (candidateIndex < 1L || candidateIndex > int.MaxValue)
                return false;

            tickIndex = (int)candidateIndex;
            return true;
        }

        private static CombatCapsuleQuery CreateTornadoColumnQuery(
            CombatQueryHeader header,
            FixVector3 center,
            Fix64 radius)
        {
            FixVector3 pointA = center + new FixVector3(
                Fix64.Zero,
                -CharacterTestFireballProjectileAdapter.TornadoColumnBottomOffset,
                Fix64.Zero);
            FixVector3 pointB = center + new FixVector3(
                Fix64.Zero,
                CharacterTestFireballProjectileAdapter.TornadoColumnTopOffset,
                Fix64.Zero);
            return new CombatCapsuleQuery(header, pointA, pointB, radius);
        }

        private void RecordExplosionInteractionFromProjectileImpact(
            CombatProjectileImpactEvent impact,
            RuntimeFrame frame)
        {
            if (!impact.Context.IsAreaPulse)
                return;
            if (!TryResolveFireballExplosionDefinition(impact.Projectile, out CombatProjectileDefinition definition))
                return;
            if (!TryReserveProjectileInteraction(frame, impact.Projectile))
                return;

            RecordExplosionInteraction(
                frame,
                impact.Projectile,
                definition,
                CharacterTestInteractionSourceDomains.CombatProjectileAreaPulse);
        }

        private void RecordExplosionInteractionFromProjectileExpiration(
            CombatProjectileExpiredEvent expiration,
            RuntimeFrame frame)
        {
            if (!TryResolveFireballExplosionDefinition(expiration.Projectile, out CombatProjectileDefinition definition))
                return;
            if (!definition.Impact.TriggerOnExpire)
                return;
            if (!TryReserveProjectileInteraction(frame, expiration.Projectile))
                return;

            RecordExplosionInteraction(
                frame,
                expiration.Projectile,
                definition,
                CharacterTestInteractionSourceDomains.CombatProjectileExpiration);
        }

        private void RecordMeteorBarrageInteractionFromAreaPulseTrigger(
            CombatProjectileAreaPulseTriggerEvent trigger,
            RuntimeFrame frame)
        {
            if (!TryResolveMeteorBarrageDefinition(trigger.Projectile, out CombatProjectileDefinition definition))
                return;
            if (!TryReserveProjectileInteraction(frame, trigger.Projectile))
                return;

            int sourceDomain = trigger.TriggerKind == CombatProjectileAreaPulseTriggerKind.Expiration
                ? CharacterTestInteractionSourceDomains.CombatProjectileExpiration
                : CharacterTestInteractionSourceDomains.CombatProjectileAreaPulse;
            RecordMeteorBarrageInteraction(frame, trigger, definition, sourceDomain);
        }

        private bool TryResolveFireballExplosionDefinition(
            CombatProjectileSnapshot projectile,
            out CombatProjectileDefinition definition)
        {
            definition = null;
            if (projectile.DefinitionId != CharacterTestFireballProjectileAdapter.FireballExplosionDefinitionId)
                return false;
            if (!_projectileDefinitions.TryGet(projectile.DefinitionId, out definition))
                return false;
            return definition.Impact.PolicyKind == CombatProjectileImpactPolicyKind.AreaPulseOnImpact;
        }

        private bool TryResolveMeteorBarrageDefinition(
            CombatProjectileSnapshot projectile,
            out CombatProjectileDefinition definition)
        {
            definition = null;
            if (!CharacterTestFireballProjectileAdapter.IsMeteorBarrageProjectile(projectile))
                return false;
            if (!_projectileDefinitions.TryGet(projectile.DefinitionId, out definition))
                return false;
            return definition.Impact.PolicyKind == CombatProjectileImpactPolicyKind.AreaPulseOnImpact;
        }

        private bool TryReserveProjectileInteraction(RuntimeFrame frame, CombatProjectileSnapshot projectile)
        {
            return _projectileInteractionKeys.Add(new CharacterTestProjectileInteractionKey(
                frame.Value,
                projectile.ProjectileId,
                projectile.SpawnSequence,
                projectile.DefinitionId));
        }

        private void StopMeteorBarrageTelegraphVfx(
            CombatProjectileSnapshot projectile,
            int meteorIndex,
            RuntimeFrame frame)
        {
            if (_vfxRuntime == null || meteorIndex < 0)
                return;

            RefreshMeteorBarrageTelegraphIndex(frame);
            var key = new MeteorBarrageTelegraphKey(
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(projectile.SourceEntityId),
                projectile.ActionInstanceId,
                meteorIndex);
            if (!_meteorBarrageTelegraphInstances.TryGetValue(key, out VfxInstanceId instanceId)
                || !instanceId.IsValid)
            {
                return;
            }

            _vfxRuntime.EnqueueStopRequest(new VfxStopRequest(
                frame,
                instanceId,
                MeteorBarrageTelegraphStopReason,
                new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.CombatRuntimeEffectVfx, CharacterTestCombatEntityLookup.ResolveCharacterEntityId(projectile.SourceEntityId), projectile.ActionInstanceId, CharacterTestCharacterActionConfigSource.MeteorBarrageTelegraphVfxStableEventId)));
            _meteorBarrageTelegraphInstances.Remove(key);
        }

        private void RefreshMeteorBarrageTelegraphIndex(RuntimeFrame frame)
        {
            if (_vfxRuntime == null || _meteorBarrageTelegraphIndexFrame == frame.Value)
                return;

            _meteorBarrageTelegraphIndexFrame = frame.Value;
            _meteorBarrageTelegraphInstances.Clear();
            _meteorBarrageActiveVfxInstances.Clear();
            _vfxRuntime.CopyActiveInstances(_meteorBarrageActiveVfxInstances);

            int definitionId = CharacterTestVfxIds.MeteorBarrageTelegraphDefinitionId;
            int triggerId = CharacterTestCharacterActionConfigSource.MeteorBarrageTelegraphVfxStableEventId;
            for (int i = 0; i < _meteorBarrageActiveVfxInstances.Count; i++)
            {
                VfxInstanceSnapshot instance = _meteorBarrageActiveVfxInstances[i];
                if (instance.DefinitionId.Value != definitionId
                    || instance.TriggerId != triggerId
                    || instance.Parameters.Value0 != triggerId)
                {
                    continue;
                }

                int meteorIndex = instance.Parameters.Value2;
                int actionInstanceId = instance.Parameters.Value3;
                if (meteorIndex < 0
                    || meteorIndex >= CharacterTestFireballProjectileAdapter.MeteorBarrageProjectileCount
                    || actionInstanceId <= 0
                    || instance.SourceId <= 0)
                {
                    continue;
                }

                _meteorBarrageTelegraphInstances[new MeteorBarrageTelegraphKey(
                    instance.SourceId,
                    actionInstanceId,
                    meteorIndex)] = instance.InstanceId;
            }

            _meteorBarrageActiveVfxInstances.Clear();
        }

        private static int ResolveMeteorBarrageLaunchLocalFrame(int meteorIndex)
        {
            return 24 + Math.Max(0, meteorIndex) * 6;
        }

        private static bool TryResolveMeteorBarrageIndexFromProjectile(
            CombatProjectileSnapshot projectile,
            out int meteorIndex)
        {
            int offset = projectile.LocalFrame - ResolveMeteorBarrageLaunchLocalFrame(0);
            if (offset >= 0 && offset % 6 == 0)
            {
                int resolved = offset / 6;
                if (resolved >= 0 && resolved < CharacterTestFireballProjectileAdapter.MeteorBarrageProjectileCount)
                {
                    meteorIndex = resolved;
                    return true;
                }
            }

            meteorIndex = -1;
            return false;
        }

        private void RecordExplosionInteraction(
            RuntimeFrame frame,
            CombatProjectileSnapshot projectile,
            CombatProjectileDefinition definition,
            int sourceDomain)
        {
            int stableId = ResolveExplosionInteractionStableId(definition);
            int intensity = ResolveExplosionInteractionIntensity(definition.Impact);

            _interactionHistory.Record(new CharacterTestInteractionEvent(
                frame,
                CharacterTestInteractionKind.PhysicalField,
                stableId,
                projectile.SpawnSequence,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(projectile.SourceEntityId),
                targetEntityId: 0,
                projectile.Position,
                projectile.Direction,
                definition.Impact.AreaRadius,
                intensity,
                projectile.TraceId,
                sourceDomain,
                CharacterTestInteractionFieldProfiles.FireballExplosionRadialImpulse(intensity)));
        }

        private void RecordExplosionInteraction(
            RuntimeFrame frame,
            CombatProjectilePhysicalEvent physicalEvent,
            CombatProjectileDefinition definition,
            int sourceDomain)
        {
            CombatProjectileSnapshot projectile = physicalEvent.Projectile;
            int stableId = ResolveExplosionInteractionStableId(definition);

            _interactionHistory.Record(new CharacterTestInteractionEvent(
                frame,
                CharacterTestInteractionKind.PhysicalField,
                stableId,
                projectile.SpawnSequence,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(projectile.SourceEntityId),
                targetEntityId: 0,
                physicalEvent.Position,
                physicalEvent.Direction,
                physicalEvent.Radius,
                physicalEvent.ImpactPower,
                projectile.TraceId,
                sourceDomain,
                CharacterTestInteractionFieldProfiles.FireballExplosionRadialImpulse(physicalEvent.ImpactPower)));
        }

        private void RecordMeteorBarrageInteraction(
            RuntimeFrame frame,
            CombatProjectileAreaPulseTriggerEvent trigger,
            CombatProjectileDefinition definition,
            int sourceDomain)
        {
            int stableId = ResolveExplosionInteractionStableId(definition);
            int intensity = ResolveExplosionInteractionIntensity(definition.Impact);
            CombatProjectileSnapshot projectile = trigger.Projectile;

            _interactionHistory.Record(new CharacterTestInteractionEvent(
                frame,
                CharacterTestInteractionKind.PhysicalField,
                stableId,
                projectile.SpawnSequence,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(projectile.SourceEntityId),
                targetEntityId: 0,
                trigger.Center,
                projectile.Direction,
                definition.Impact.AreaRadius,
                intensity,
                projectile.TraceId,
                sourceDomain,
                CharacterTestInteractionFieldProfiles.MeteorBarrageRadialImpulse(intensity)));
        }

        private void RecordMeteorBarrageInteraction(
            RuntimeFrame frame,
            CombatProjectilePhysicalEvent physicalEvent,
            CombatProjectileDefinition definition,
            int sourceDomain)
        {
            int stableId = ResolveExplosionInteractionStableId(definition);
            CombatProjectileSnapshot projectile = physicalEvent.Projectile;

            _interactionHistory.Record(new CharacterTestInteractionEvent(
                frame,
                CharacterTestInteractionKind.PhysicalField,
                stableId,
                projectile.SpawnSequence,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(projectile.SourceEntityId),
                targetEntityId: 0,
                physicalEvent.Position,
                physicalEvent.Direction,
                physicalEvent.Radius,
                physicalEvent.ImpactPower,
                projectile.TraceId,
                sourceDomain,
                CharacterTestInteractionFieldProfiles.MeteorBarrageRadialImpulse(physicalEvent.ImpactPower)));
        }

        private void RecordExplosionInteractionFromTimelineAreaPulse(
            AbilityEffectTimelineEventDispatch dispatch,
            CharacterTestCharacterEntity source,
            CharacterActionGameplayRequest request)
        {
            if (!IsFireballExplosionTimelineAreaPulse(dispatch))
                return;

            CombatProjectileImpactConfig impact = CharacterTestFireballProjectileAdapter.FireballExplosionImpactConfig;
            int stableId = (request.Metadata.StableEventId == 0)
                ? CharacterTestCharacterActionConfigSource.FireballExplosionTimelineRepeatStableEventId
                : request.Metadata.StableEventId;
            int intensity = ResolveExplosionInteractionIntensity(impact);
            Fix64 radius = dispatch.Event.Radius > Fix64.Zero ? dispatch.Event.Radius : impact.AreaRadius;
            _interactionHistory.Record(new CharacterTestInteractionEvent(
                dispatch.RuntimeFrame,
                CharacterTestInteractionKind.PhysicalField,
                stableId,
                dispatch.InstanceId,
                source?.Id.Value ?? dispatch.Context.SourceEntityId,
                targetEntityId: 0,
                dispatch.AnchorPosition,
                dispatch.Context.AnchorDirection,
                radius,
                intensity,
                stableId,
                CharacterTestInteractionSourceDomains.AbilityEffectTimelineAreaPulse,
                CharacterTestInteractionFieldProfiles.FireballExplosionRadialImpulse(intensity)));
        }

        private static bool IsFireballExplosionTimelineAreaPulse(
            AbilityEffectTimelineEventDispatch dispatch)
        {
            return dispatch.Event.TrackKind == AbilityEffectTimelineTrackKind.Combat
                && dispatch.Event.EventKind == AbilityEffectTimelineEventKind.AreaPulse
                && (dispatch.Context.EffectTimelineId == CharacterTestAbilityEffectTimelineSource.FireballExplosionTimelineId);
        }

        private static int ResolveExplosionInteractionIntensity(CombatProjectileImpactConfig impact)
        {
            int intensity = impact.AreaHitEvent.Damage.ImpactPower;
            if (intensity <= 0)
                intensity = impact.HitEvent.Damage.ImpactPower;
            return intensity;
        }

        private static int ResolveExplosionInteractionStableId(CombatProjectileDefinition definition)
        {
            if (!(definition.Impact.AreaHitEvent.StableEventId == 0))
                return definition.Impact.AreaHitEvent.StableEventId;
            if (!(definition.Impact.HitEvent.StableEventId == 0))
                return definition.Impact.HitEvent.StableEventId;
            if (!(definition.Impact.StableEventId == 0))
                return definition.Impact.StableEventId;
            throw new InvalidOperationException("Explosion interaction requires a prepared event identity in its projectile definition.");
        }

        private void StartEffectTimelineFromProjectileImpact(
            CombatProjectileImpactEvent impact,
            RuntimeFrame frame)
        {
            if ((impact.EffectTimelineId == 0))
                return;

            if (!_projectileDefinitions.TryGet(impact.Projectile.DefinitionId, out CombatProjectileDefinition definition))
                return;

            int sourceEntityId = CharacterTestCombatEntityLookup.ResolveCharacterEntityId(impact.Projectile.SourceEntityId);
            int triggerEntityId = CharacterTestCombatEntityLookup.ResolveCharacterEntityId(impact.ResolveResult.TargetId);
            StartEffectTimelineFromProjectile(
                impact.Projectile,
                definition,
                impact.EffectTimelineId,
                sourceEntityId,
                triggerEntityId,
                AbilityEffectTimelineTriggerKind.ProjectileImpact,
                frame,
                suppressFrameZeroCombat: impact.Context.IsAreaPulse);
        }

        private void StartEffectTimelineFromProjectileExpiration(
            CombatProjectileExpiredEvent expiration,
            RuntimeFrame frame)
        {
            if ((expiration.EffectTimelineId == 0))
                return;

            if (!_projectileDefinitions.TryGet(expiration.Projectile.DefinitionId, out CombatProjectileDefinition definition))
                return;

            int sourceEntityId = CharacterTestCombatEntityLookup.ResolveCharacterEntityId(expiration.Projectile.SourceEntityId);
            StartEffectTimelineFromProjectile(
                expiration.Projectile,
                definition,
                expiration.EffectTimelineId,
                sourceEntityId,
                triggerEntityId: 0,
                AbilityEffectTimelineTriggerKind.ProjectileExpire,
                frame,
                suppressFrameZeroCombat: definition.Impact.PolicyKind == CombatProjectileImpactPolicyKind.AreaPulseOnImpact);
        }

    }

    // Scope is one periodic producer occurrence. A failed target/payload filter does not
    // consume the target; a successful callback records the only damage/part evidence.
    internal static class CharacterTestTornadoDamageTickSelection
    {
        internal static void AddFirstEligiblePerTarget(
            IReadOnlyList<CombatQueryResult> sortedHits,
            int startIndex,
            HashSet<CombatEntityId> selectedTargets,
            Func<CombatQueryResult, bool> tryAddCandidate)
        {
            selectedTargets.Clear();
            for (int i = startIndex; i < sortedHits.Count; i++)
            {
                CombatQueryResult hit = sortedHits[i];
                if (selectedTargets.Contains(hit.TargetEntityId))
                    continue;
                // All existing source/target and payload checks remain inside the callback.
                // Stop before calling its side-effectful resolver for later hurtboxes.
                if (tryAddCandidate(hit))
                    selectedTargets.Add(hit.TargetEntityId);
            }
        }
    }
}



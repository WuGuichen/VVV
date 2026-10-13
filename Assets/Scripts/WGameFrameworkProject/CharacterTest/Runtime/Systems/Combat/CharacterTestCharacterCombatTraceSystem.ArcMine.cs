using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.CombatBridge;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        private readonly CharacterTestArcMineRuntime _arcMineRuntime = new CharacterTestArcMineRuntime();
        private readonly List<CharacterTestArcMineDeployed> _arcMineDeployedMines =
            new List<CharacterTestArcMineDeployed>();
        private readonly List<CharacterTestArcMineDeployed> _arcMineWaitingVfxMines =
            new List<CharacterTestArcMineDeployed>();
        private readonly List<CharacterTestArcMineFuseStarted> _arcMineFuseStartedMines =
            new List<CharacterTestArcMineFuseStarted>();
        private readonly List<CharacterTestArcMineTriggered> _arcMineTriggeredMines =
            new List<CharacterTestArcMineTriggered>();

        public CharacterTestArcMineTickSummary ArcMineLastTickSummary => _arcMineRuntime.LastTickSummary;
        public int ActiveArcMineCount => _arcMineRuntime.ActiveCount;
        public int LastArcMineBlastTargetCount { get; private set; }

        internal CharacterTestArcMineRuntimeState ReadArcMineStateView()
        {
            return _arcMineRuntime.ReadStateView();
        }

        internal CharacterTestArcMineRuntimeState CreateArcMineStateSnapshot()
        {
            return _arcMineRuntime.CreateStateSnapshot();
        }

        internal bool TryApplyArcMineStateSnapshot(
            CharacterTestArcMineRuntimeState state,
            out string failure)
        {
            return _arcMineRuntime.TryApplyStateSnapshot(state, out failure);
        }

        private void ProcessArcMineRequests(
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
                        && (request.RequestId == CharacterTestCharacterActionConfigSource.ArcMineGameplayRequestId))
                    {
                        ProcessArcMineRequest(world, entity, request, actionRuntime, frame, requestIndex);
                    }
                }
            }
        }

        private void ProcessArcMineRequest(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            int sourceOrder)
        {
            if (!CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                    _projectileSpawnProfiles,
                    CharacterTestFireballProjectileAdapter.ArcMineRequestId,
                    out CombatProjectileSpawnProfile spawnProfile))
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.RequestId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "ArcMineProjectileSpawnProfileMissing",
                    "Arc Mine carrier projectile spawn profile is missing.");
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
                    "ArcMineProjectileDefinitionMissing",
                    "Arc Mine carrier projectile definition is missing.");
                return;
            }

            CharacterTestArcMineDeployResult targetResult = _arcMineRuntime.TryResolveDeployTarget(
                world,
                _physicsRuntime,
                entity,
                _lockedTargetEntityIdProvider,
                out CharacterTestArcMineDeployTarget target);
            if (!targetResult.Success)
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.RequestId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "ArcMineTargetRejected",
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
                    "ArcMineProjectileUnreachable",
                    "Arc Mine target point is outside the carrier projectile ballistic reach. Projectile spawn will be rejected.");
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
                    "ArcMineProjectileDirectionInvalid",
                    "Arc Mine carrier projectile solved a zero launch direction.");
                return;
            }

            var spawnRequest = new CombatProjectileSpawnRequest(
                ToCombatFrame(frame),
                definition.DefinitionId,
                _physicsRuntime.GetEntityId(entity),
                origin,
                direction,
                true,
                target.Center,
                StablePositiveId(request.Metadata.ActionId.Value),
                StablePositiveId(request.Metadata.InstanceId),
                CharacterTestCharacterActionConfigSource.ArcMineProjectileImpactStableEventId,
                request.Metadata.LocalFrame,
                sourceOrder,
                effectiveLifetimeFrames);
            var root = entity.CaptureDoubleEdgeSource(request.Metadata.InstanceId);
            if (!EnqueueAccessoryProjectile(spawnRequest, root, _entityLookup.AccessoryState?.SecondBatchRoot(root) ?? default,
                frame.Value, true)) return;

            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                entity.Id.Value,
                "arcMineThrow request=" + CharacterTestCombatTraceText.EmptyAsNone(request.RequestId)
                + " mode=" + CharacterTestCombatTraceText.EmptyAsNone(target.DeployMode)
                + " projectile=" + CharacterTestCombatTraceText.EmptyAsNone(definition.StableId)
                + " lifetime=" + effectiveLifetimeFrames.ToString(CultureInfo.InvariantCulture)
                + " launchSpeed=" + launchSpeedPerFrame.ToString());
        }

        private void ProcessArcMineRuntimeEvents(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame)
        {
            LastArcMineBlastTargetCount = 0;
            _arcMineDeployedMines.Clear();
            _arcMineRuntime.DrainLastDeployedMines(_arcMineDeployedMines);
            for (int i = 0; i < _arcMineDeployedMines.Count; i++)
                SpawnArcMineDeployVfx(_arcMineDeployedMines[i], frame);
            _arcMineDeployedMines.Clear();

            _arcMineWaitingVfxMines.Clear();
            _arcMineRuntime.DrainLastWaitingVfxMines(_arcMineWaitingVfxMines);
            for (int i = 0; i < _arcMineWaitingVfxMines.Count; i++)
                SpawnArcMineWaitingVfx(_arcMineWaitingVfxMines[i], frame);
            _arcMineWaitingVfxMines.Clear();

            _arcMineFuseStartedMines.Clear();
            _arcMineRuntime.DrainLastFuseStartedMines(_arcMineFuseStartedMines);
            for (int i = 0; i < _arcMineFuseStartedMines.Count; i++)
                SpawnArcMineFuseVfx(_arcMineFuseStartedMines[i], frame);
            _arcMineFuseStartedMines.Clear();

            _arcMineTriggeredMines.Clear();
            _arcMineRuntime.DrainLastTriggeredMines(_arcMineTriggeredMines);
            for (int i = 0; i < _arcMineTriggeredMines.Count; i++)
            {
                CharacterTestArcMineTriggered triggered = _arcMineTriggeredMines[i];
                SpawnArcMineTriggerVfx(triggered, frame);
                ProcessArcMineBlast(world, triggered, frame);
            }

            _arcMineTriggeredMines.Clear();
        }

        private void ProcessArcMineBlast(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestArcMineTriggered triggered,
            RuntimeFrame frame)
        {
            CharacterTestCharacterEntity source = _entityLookup.FindEntity(triggered.CasterEntityId);
            if (source == null || !source.BattleState.CountsAsAlive)
                return;

            int actionId = StablePositiveId(CharacterTestCharacterActionConfigSource.ArcMineActionId);
            int traceId = CharacterTestCharacterActionConfigSource.ArcMineTriggerStableEventId;
            var header = new CombatQueryHeader(
                queryId: triggered.RuntimeId,
                kind: CombatQueryKind.Sphere,
                sourceEntityId: new CombatEntityId(triggered.SourceCombatEntityId),
                traceId: traceId,
                actionId: actionId,
                sourceOrder: triggered.SourceOrder,
                layerMask: CombatPhysicsLayerMask.FromLayer(
                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer));
            int startCount = _gameplayQueryResults.Count;
            _physicsRuntime.World.QuerySphere(
                new CombatSphereQuery(header, triggered.Center, _arcMineRuntime.Tuning.BlastRadius),
                _gameplayQueryResults,
                includeSourceEntity: false);

            CharacterActionGameplayRequest damageRequest = CreateArcMineDamageRequest(source, triggered, frame);
            int acceptedCount = 0;
            int maxTargets = _arcMineRuntime.Tuning.MaxBlastTargets;
            for (int i = startCount; i < _gameplayQueryResults.Count; i++)
            {
                if (acceptedCount >= maxTargets)
                    break;

                CombatQueryResult queryResult = _gameplayQueryResults[i];
                CharacterTestCharacterEntity target = _entityLookup.FindEntityByCombatId(queryResult.TargetEntityId);
                if (!IsArcMineHostileAlive(source, target))
                    continue;

                if (!_damageResolver.TryResolveGameplayDamage(
                        damageRequest,
                        source,
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
                    new CombatEntityId(triggered.SourceCombatEntityId),
                    queryResult.TargetEntityId,
                    actionId,
                    triggered.ActionInstanceId,
                    traceId,
                    ToCombatFrame(frame),
                    queryResult,
                    damage.IncomingDamage,
                    CharacterTestHitReactionResolver.CreateHitResolveReactionGate(damage),
                    CharacterTestHitReactionResolver.CreateHitResolveKnockbackGate(damage),
                    _entityLookup.ResolveTargetState(queryResult.TargetEntityId),
                    maxTargets - acceptedCount);
                var hitKey = new CharacterTestResolvedHitKey(candidate);
                SetAccessoryAwareDamagePayload(hitKey, damage, ProducerEvidence(4, triggered.RuntimeId, 0,
                    source, candidate.TargetId, unchecked((ulong)(uint)triggered.RuntimeId)));
                _hitZonesByHit[hitKey] = hitZone;
                _gameplayProfiledCandidates.Add(candidate);
                acceptedCount++;
            }

            LastArcMineBlastTargetCount += acceptedCount;
            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                source.Id.Value,
                "arcMineBlast runtimeId=" + triggered.RuntimeId.ToString(CultureInfo.InvariantCulture)
                + " triggerTarget=" + triggered.TriggerTargetEntityId.ToString(CultureInfo.InvariantCulture)
                + " candidates=" + (_gameplayQueryResults.Count - startCount).ToString(CultureInfo.InvariantCulture)
                + " accepted=" + acceptedCount.ToString(CultureInfo.InvariantCulture));
        }

        private static CharacterActionGameplayRequest CreateArcMineDamageRequest(
            CharacterTestCharacterEntity source,
            CharacterTestArcMineTriggered triggered,
            RuntimeFrame frame)
        {
            var metadata = new CharacterActionAdapterDispatchMetadata(
                frame,
                triggered.ActionInstanceId,
                triggered.ActionInstanceId,
                CharacterTestCharacterActionConfigSource.ArcMineActionId,
                triggered.LocalFrame,
                CharacterTestAuthoredContentIds.D401_Id14,
                triggered.CasterEntityId,
                new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.ProjectileInteractionVfx, triggered.CasterEntityId, triggered.ActionInstanceId, triggered.TraceId));
            var payload = new CharacterActionDamagePayload(
                CharacterTestAuthoredContentIds.D409_Id9,
                DamageFormulaIds.CharacterTestSoftDefenseV1,
                baseDamage: 10,
                impactPower: 14,
                tags: new[] { CharacterTestAuthoredContentIds.D410_Id39, CharacterTestAuthoredContentIds.D410_Id3, CharacterTestAuthoredContentIds.D410_Id14, CharacterTestAuthoredContentIds.D410_Id5, CharacterTestAuthoredContentIds.D410_Id6, CharacterTestAuthoredContentIds.D410_Id40 },
                analysisCategory: CharacterTestAuthoredContentIds.D411_Id16,
                damageTypeLabel: "light", analysisCategoryLabel: "ArcMineBlast",
                tagLabels: new[] { "arc_mine", "area", "light", "magic", "scatter", "trap" },
                presentationProfileId: CharacterTestAuthoredContentIds.D407_Id15, presentationProfileLabel: CharacterTestCharacterActionConfigSource.ArcMinePresentationProfileLabel);
            return new CharacterActionGameplayRequest(
                metadata,
                source.GameplayEntityId,
                CharacterActionTrackEventKind.ApplyGameplayEffect,
                CharacterTestAuthoredContentIds.D401_Id14,
                CharacterTestAbilityIds.ArcMineAbilityId,
                targetPolicyId: CharacterTestAuthoredContentIds.D406_Id12,
                damagePayload: payload);
        }

        private void SpawnArcMineDeployVfx(
            CharacterTestArcMineDeployed mine,
            RuntimeFrame frame)
        {
            SpawnArcMineGameplayVfx(
                mine.CasterEntityId,
                mine.Center,
                mine.Forward,
                mine.SourceOrder,
                mine.LocalFrame,
                _arcMineRuntime.Tuning.DeployVfxDefinitionId,
                CharacterTestArcMineIds.DeployVfxLifetimeFrames,
                CharacterTestCharacterActionConfigSource.ArcMineDeployVfxStableEventId,
                frame);
        }

        private void SpawnArcMineWaitingVfx(
            CharacterTestArcMineDeployed mine,
            RuntimeFrame frame)
        {
            SpawnArcMineGameplayVfx(
                mine.CasterEntityId,
                mine.Center,
                mine.Forward,
                mine.SourceOrder,
                mine.LocalFrame,
                _arcMineRuntime.Tuning.FuseVfxDefinitionId,
                CharacterTestArcMineIds.FuseVfxLifetimeFrames,
                CharacterTestCharacterActionConfigSource.ArcMineWaitingVfxStableEventId,
                frame);
        }

        private void SpawnArcMineFuseVfx(
            CharacterTestArcMineFuseStarted mine,
            RuntimeFrame frame)
        {
            SpawnArcMineGameplayVfx(
                mine.CasterEntityId,
                mine.Center,
                mine.Forward,
                mine.SourceOrder,
                mine.LocalFrame,
                _arcMineRuntime.Tuning.FuseVfxDefinitionId,
                _arcMineRuntime.Tuning.FuseDelayFrames,
                CharacterTestCharacterActionConfigSource.ArcMineFuseVfxStableEventId,
                frame);
        }

        private void SpawnArcMineTriggerVfx(
            CharacterTestArcMineTriggered mine,
            RuntimeFrame frame)
        {
            SpawnArcMineGameplayVfx(
                mine.CasterEntityId,
                mine.Center,
                mine.Forward,
                mine.SourceOrder,
                mine.LocalFrame,
                _arcMineRuntime.Tuning.TriggerVfxDefinitionId,
                CharacterTestArcMineIds.TriggerVfxLifetimeFrames,
                CharacterTestCharacterActionConfigSource.ArcMineTriggerVfxStableEventId,
                frame);
        }

        private void SpawnArcMineGameplayVfx(
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

        private static bool IsArcMineHostileAlive(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target)
        {
            return source != null
                && target != null
                && source.Id.Value != target.Id.Value
                && source.Team.TeamId != target.Team.TeamId
                && target.BattleState.CanReceiveHpDamage;
        }
    }
}


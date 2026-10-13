using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
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
        private void StartEffectTimelineFromProjectile(
            CombatProjectileSnapshot projectile,
            CombatProjectileDefinition definition,
            int effectTimelineId,
            int sourceEntityId,
            int triggerEntityId,
            AbilityEffectTimelineTriggerKind triggerKind,
            RuntimeFrame frame,
            bool suppressFrameZeroCombat)
        {
            if (_effectTimelineRuntime == null || effectTimelineId <= 0)
                return;

            var key = new TimelineProjectileStartKey(
                projectile.ProjectileId,
                frame.Value,
                effectTimelineId);
            if (!_timelineProjectileStartKeys.Add(key))
                return;

            int abilityId = CharacterTestFireballProjectileAdapter.ResolveAbilityId(definition);
            var timelineContext = new AbilityEffectTimelineContext(
                abilityId,
                effectTimelineId,
                ownerEntityId: sourceEntityId,
                sourceEntityId: sourceEntityId,
                triggerEntityId: triggerEntityId,
                triggerKind,
                projectile.Position,
                projectile.Direction);
            var authority = _entityLookup.AccessoryState;
            var root = authority?.ProjectileSource(projectile.ProjectileId, projectile.SpawnSequence) ?? default;
            var parent = authority?.SecondBatchProducer(root, 1, projectile.ProjectileId, projectile.SpawnSequence) ?? default;
            var trigger = triggerKind == AbilityEffectTimelineTriggerKind.ProjectileImpact && parent.IsValid
                && _projectileCausalParent.Source.Matches(parent) ? _projectileCausalParent.Key : default;
            AbilityEffectTimelineStartResult start = StartAccessoryTimeline(timelineContext, frame, root, parent, trigger,
                _entityLookup.AccessoryState?.ThirdSource(1, projectile.ProjectileId, projectile.SpawnSequence).Attack ?? default);
            if (!start.Success)
            {
                ReportHitTraceWarning(
                    sourceEntityId,
                    CharacterTestFireballProjectileAdapter.ResolveCharacterActionId(definition),
                    effectTimelineId,
                    string.Empty,
                    projectile.LocalFrame,
                    "AbilityEffectTimelineStartRejected",
                    start.FailureReason);
                return;
            }

            LastAbilityEffectTimelineStartCount++;
            if (suppressFrameZeroCombat)
                _suppressFrameZeroCombatTimelineInstances.Add(start.InstanceId);
            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                sourceEntityId,
                "effectTimelineStart timeline=" + CharacterTestCombatTraceText.EmptyAsNone(effectTimelineId)
                + " projectile=" + projectile.ProjectileId.ToString(CultureInfo.InvariantCulture)
                + " instance=" + start.InstanceId.ToString(CultureInfo.InvariantCulture));
        }

        private void ProcessAbilityEffectTimelines(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame)
        {
            if (_effectTimelineRuntime == null)
                return;

            _effectTimelineRuntime.Tick(new RuntimeTickContext(
                frame.Value,
                0d,
                0d,
                RuntimeTickStage.Simulation));

            _effectTimelineRuntime.PumpFrameZero(frame);
            PruneTimelineCombatSuppressions();
        }

        public void OnAbilityEffectTimelineEvent(AbilityEffectTimelineEventDispatch dispatch)
        {
            if (dispatch.Event.TrackKind == AbilityEffectTimelineTrackKind.Presentation
                && dispatch.Event.EventKind == AbilityEffectTimelineEventKind.SpawnVfx)
            {
                if (ShouldSuppressInteractionBackedTimelineVfx(dispatch))
                    return;

                SpawnTimelineVfx(dispatch);
                return;
            }

            if (dispatch.Event.TrackKind == AbilityEffectTimelineTrackKind.Combat
                && (dispatch.Event.EventKind == AbilityEffectTimelineEventKind.AreaPulse
                    || dispatch.Event.EventKind == AbilityEffectTimelineEventKind.ShapeQueryWindow))
            {
                ProcessTimelineCombatEvent(dispatch);
            }
        }

        private void SpawnTimelineVfx(AbilityEffectTimelineEventDispatch dispatch)
        {
            if (_vfxRuntime == null || dispatch.Event.EffectId <= 0)
                return;

            int triggerId = dispatch.Event.EventId == 0
                ? dispatch.Event.RequestId
                : dispatch.Event.EventId;
            VfxSpawnRequest spawned = _vfxRuntime.EnqueueSpawnRequest(new VfxSpawnRequest(
                dispatch.RuntimeFrame,
                VfxSourceDomains.Gameplay,
                dispatch.RuntimeFrame.Value,
                dispatch.LocalFrame,
                dispatch.InstanceId,
                triggerId,
                dispatch.Context.SourceEntityId,
                dispatch.Context.TriggerEntityId,
                new VfxDefinitionId(dispatch.Event.EffectId),
                VfxAnchor.World(new VfxPose(
                    CombatVfxPhysicsAdapter.ToVfxVector(dispatch.AnchorPosition),
                    CombatVfxPhysicsAdapter.ToVfxVector(dispatch.Context.AnchorDirection))),
                new VfxParameterBlock(triggerId, dispatch.LocalFrame),
                lifetimeFramesOverride: 0,
                new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.AbilityTimelineVfx, dispatch.Context.SourceEntityId, dispatch.InstanceId, dispatch.Event.EventId)));
            if (spawned.DefinitionId.IsValid)
                LastAbilityEffectTimelineVfxSpawnCount++;
        }

        private static bool ShouldSuppressInteractionBackedTimelineVfx(
            AbilityEffectTimelineEventDispatch dispatch)
        {
            return (dispatch.Context.EffectTimelineId == CharacterTestAbilityEffectTimelineSource.FireballExplosionTimelineId)
                && dispatch.Event.EffectId == CharacterTestVfxIds.FireballExplosionImpactDefinitionId;
        }

        private void ProcessTimelineCombatEvent(AbilityEffectTimelineEventDispatch dispatch)
        {
            _timelineProfiledCandidates.Clear();
            var binding = _entityLookup.AccessoryState?.FindProducer(3, dispatch.InstanceId, 0) ?? default;
            if (_entityLookup.AccessoryState?.SuppressesAttack(binding.Root) == true) return;

            if (ShouldSuppressTimelineCombat(dispatch))
            {
                LastAbilityEffectTimelineSuppressedCombatEventCount++;
                return;
            }

            CharacterTestCharacterEntity source = _entityLookup.FindEntity(dispatch.Context.SourceEntityId);
            var frozenActor = _entityLookup.AccessoryState?.FindTimelineActor(dispatch.InstanceId);
            if (source != null && frozenActor != null && source.GameplayEntityId != frozenActor.Actor) source = null;
            if (source == null && frozenActor == null)
            {
                ReportHitTraceWarning(
                    dispatch.Context.SourceEntityId,
                    CharacterActionId.None,
                    dispatch.Event.RequestId,
                    string.Empty,
                    dispatch.LocalFrame,
                    "AbilityEffectTimelineSourceMissing",
                    "Timeline combat event has neither a live source nor a frozen birth actor.");
                return;
            }

            if (!TryResolveTimelineDamageRequest(
                    dispatch,
                    source,
                    frozenActor,
                    out CharacterActionGameplayRequest request))
            {
                return;
            }

            RecordExplosionInteractionFromTimelineAreaPulse(dispatch, source, request);
            QueryTimelineCombatEvent(dispatch, source, request, frozenActor);
            if (_timelineProfiledCandidates.Count == 0)
                return;

            _timelineHitOnceKeys.Clear();
            _hitResolve.Resolve(
                _timelineProfiledCandidates,
                _timelineHitOnceKeys,
                _results,
                ProducerRelations(frozenActor),
                _entityLookup);
            _timelineProfiledCandidates.Clear();
            _timelineHitOnceKeys.Clear();
            LastAbilityEffectTimelineCombatEventCount++;
        }

        private bool ShouldSuppressTimelineCombat(AbilityEffectTimelineEventDispatch dispatch)
        {
            return dispatch.LocalFrame == 0
                && _suppressFrameZeroCombatTimelineInstances.Contains(dispatch.InstanceId);
        }

        private bool TryResolveTimelineDamageRequest(
            AbilityEffectTimelineEventDispatch dispatch,
            CharacterTestCharacterEntity source,
            TacticalCard.TacticalDoubleEdgeBattleState.ProjectileActor frozenActor,
            out CharacterActionGameplayRequest request)
        {
            request = default;
            CharacterActionDamagePayload damagePayload = CharacterActionDamagePayload.Empty;
            int abilityStableId = 0;
            CharacterActionId actionId = CharacterActionId.None;
            if (_abilityDefinitions != null
                && dispatch.Context.AbilityId > 0
                && _abilityDefinitions.TryGet(dispatch.Context.AbilityId, out AbilityDefinitionConfig ability))
            {
                abilityStableId = ability.AbilityId;
                damagePayload = CreateAbilityDamagePayload(ability.Damage);
                if (ability.ActionBinding.ActionId.IsValid)
                    actionId = ability.ActionBinding.ActionId;
            }

            if (!damagePayload.HasInlineData)
                return false;

            int stableEventId = (dispatch.Event.RequestId == 0)
                ? dispatch.Event.EventId
                : dispatch.Event.RequestId;
            if ((stableEventId == 0))
                stableEventId = dispatch.Context.EffectTimelineId;
            var metadata = new CharacterActionAdapterDispatchMetadata(
                dispatch.RuntimeFrame,
                dispatch.InstanceId,
                dispatch.InstanceId,
                actionId,
                dispatch.LocalFrame,
                stableEventId,
                dispatch.Context.SourceEntityId,
                new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.AbilityTimelineVfx, dispatch.Context.SourceEntityId, dispatch.InstanceId, dispatch.Event.EventId));
            request = new CharacterActionGameplayRequest(
                metadata,
                source?.GameplayEntityId ?? frozenActor.Actor,
                CharacterActionTrackEventKind.ApplyGameplayEffect,
                stableEventId,
                abilityStableId,
                targetPolicyId: CharacterTestAuthoredContentIds.D406_Id11,
                effectTimelineId: dispatch.Context.EffectTimelineId,
                damagePayload: damagePayload);
            return true;
        }

        internal static CharacterActionDamagePayload CreateAbilityDamagePayload(AbilityDamageConfig damage)
        {
            if (!damage.HasDamage)
                return CharacterActionDamagePayload.Empty;

            int[] tags = Array.Empty<int>();
            if (damage.Tags != null && damage.Tags.Count > 0)
            {
                tags = new int[damage.Tags.Count];
                for (int i = 0; i < damage.Tags.Count; i++)
                    tags[i] = damage.Tags[i];
            }

            var scales = new DamageAttributeScale[damage.SourceAttributeScales.Count];
            for (int i = 0; i < scales.Length; i++)
                scales[i] = new DamageAttributeScale(
                    damage.SourceAttributeScales[i].AttributeId,
                    damage.SourceAttributeScales[i].Scale);

            return new CharacterActionDamagePayload(
                damage.DamageTypeId,
                DamageFormulaIds.CharacterTestSoftDefenseV1,
                damage.BaseDamage,
                sourceAttributeScales: scales,
                impactPower: damage.ImpactPower,
                tags: tags,
                analysisCategory: damage.AnalysisCategory,
                launchPower: damage.LaunchPower);
        }

        private void QueryTimelineCombatEvent(
            AbilityEffectTimelineEventDispatch dispatch,
            CharacterTestCharacterEntity source,
            CharacterActionGameplayRequest request,
            TacticalCard.TacticalDoubleEdgeBattleState.ProjectileActor frozenActor)
        {
            CombatEntityId sourceCombatId = frozenActor != null ? new CombatEntityId(frozenActor.CombatSource) : _physicsRuntime.GetEntityId(source);
            var identity = new CombatHitProducerIdentity(CombatHitProducerKind.EffectTimeline, checked((ulong)dispatch.InstanceId),
                0, unchecked((ulong)(uint)dispatch.Event.EventId));
            int actionId = StablePositiveId(request.Metadata.ActionId.Value);
            int traceId = request.Metadata.StableEventId;
            var header = new CombatQueryHeader(
                queryId: traceId,
                kind: dispatch.Event.ShapeKind == AbilityEffectTimelineShapeKind.Capsule
                    ? CombatQueryKind.Capsule
                    : CombatQueryKind.Sphere,
                sourceEntityId: sourceCombatId,
                traceId: traceId,
                actionId: actionId,
                sourceOrder: dispatch.InstanceId,
                layerMask: new CombatPhysicsLayerMask(dispatch.Event.LayerMask));

            _timelineQueryResults.Clear();
            if (dispatch.Event.ShapeKind == AbilityEffectTimelineShapeKind.Capsule)
            {
                _physicsRuntime.World.QueryCapsule(
                    new CombatCapsuleQuery(
                        header,
                        dispatch.AnchorPosition,
                        dispatch.AnchorPosition + dispatch.Event.LocalPointB,
                        dispatch.Event.Radius),
                    _timelineQueryResults,
                    includeSourceEntity: false);
            }
            else
            {
                _physicsRuntime.World.QuerySphere(
                    new CombatSphereQuery(header, dispatch.AnchorPosition, dispatch.Event.Radius),
                    _timelineQueryResults,
                    includeSourceEntity: false);
            }

            for (int i = 0; i < _timelineQueryResults.Count; i++)
            {
                CombatQueryResult queryResult = _timelineQueryResults[i];
                if (!_damageResolver.TryResolveGameplayDamage(
                        request,
                        source,
                        queryResult.TargetEntityId,
                        queryResult.TargetColliderId,
                        _entityLookup,
                        dispatch.RuntimeFrame,
                        DamageProfileSourceKind.AreaPulse,
                        CharacterTestBodyPartDamageSourcePolicy.Ability,
                        out DamageProfileResolveResult damage,
                        out CharacterTestHitZoneResolution hitZone, frozenActor, identity))
                {
                    continue;
                }

                var candidate = new HitCandidate(
                    sourceCombatId,
                    queryResult.TargetEntityId,
                    actionId,
                    dispatch.InstanceId,
                    traceId,
                    new CombatFrame((int)Math.Min(int.MaxValue, Math.Max(0, dispatch.RuntimeFrame.Value))),
                    queryResult,
                    damage.IncomingDamage,
                    CharacterTestHitReactionResolver.CreateHitResolveReactionGate(damage),
                    CharacterTestHitReactionResolver.CreateHitResolveKnockbackGate(damage),
                    _entityLookup.ResolveTargetState(queryResult.TargetEntityId)).WithProducerIdentity(identity);
                var hitKey = new CharacterTestResolvedHitKey(candidate);
                SetAccessoryAwareDamagePayload(hitKey, damage, ProducerEvidence(3, dispatch.InstanceId, 0,
                    source, candidate.TargetId, identity.Occurrence));
                _hitZonesByHit[hitKey] = hitZone;
                _timelineProfiledCandidates.Add(candidate);
            }

            GetVerboseCombatTraceRecorder()?.AttackCombat(
                dispatch.RuntimeFrame,
                dispatch.Context.SourceEntityId,
                "effectTimelineCombat timeline="
                + CharacterTestCombatTraceText.EmptyAsNone(dispatch.Context.EffectTimelineId)
                + " event=" + CharacterTestCombatTraceText.EmptyAsNone(dispatch.Event.EventId)
                + " candidates=" + _timelineQueryResults.Count.ToString(CultureInfo.InvariantCulture));
        }

        private void PruneTimelineCombatSuppressions()
        {
            if (_suppressFrameZeroCombatTimelineInstances.Count == 0 || _effectTimelineRuntime == null)
                return;

            _timelineSuppressionsToRemove.Clear();
            foreach (int instanceId in _suppressFrameZeroCombatTimelineInstances)
            {
                if (!_effectTimelineRuntime.ContainsActiveInstance(instanceId))
                    _timelineSuppressionsToRemove.Add(instanceId);
            }

            for (int i = 0; i < _timelineSuppressionsToRemove.Count; i++)
                _suppressFrameZeroCombatTimelineInstances.Remove(_timelineSuppressionsToRemove[i]);
            _timelineSuppressionsToRemove.Clear();
        }

        private void RegisterProjectileDamagePayload(CharacterTestCharacterRuntimeWorld world, CombatProjectileImpactEvent impact, RuntimeFrame frame)
        {
            HitResolveResult result = impact.ResolveResult;
            if (result.Kind != HitResolveKind.Damage && result.Kind != HitResolveKind.Blocked && result.Kind != HitResolveKind.Parried)
                return;

            CharacterTestCharacterEntity source = _entityLookup.FindEntityByCombatId(result.AttackerId);
            var frozenActor = world.DoubleEdgeState.FindProjectileActor(impact.Projectile.ProjectileId, impact.Projectile.SpawnSequence);
            if (source == null && frozenActor == null) return;

            if (!_projectileDefinitions.TryGet(impact.Projectile.DefinitionId, out CombatProjectileDefinition definition))
                return;

            if (!_damageResolver.TryResolveProjectileDamage(
                    impact.Projectile,
                    definition.Impact,
                    impact.Context,
                    source,
                    result.TargetId,
                    impact.PhysicsHit.TargetColliderId,
                    _entityLookup,
                    frame,
                    CharacterTestFireballProjectileAdapter.ResolveCharacterActionId(definition),
                    out DamageProfileResolveResult damage,
                    out CharacterTestHitZoneResolution hitZone))
            {
                return;
            }

            var hitKey = new CharacterTestResolvedHitKey(result);
            var root = world.DoubleEdgeState.ProjectileSource(impact.Projectile.ProjectileId, impact.Projectile.SpawnSequence);
            var target = _entityLookup.FindEntityByCombatId(result.TargetId);
            var witness = impact.Context.SourceKind == CombatProjectileImpactSourceKind.DirectSweep
                && world.DoubleEdgeState.IsDirectProjectile(impact.Projectile.ProjectileId, impact.Projectile.SpawnSequence)
                ? CharacterTestAccessoryHitEvidence.DirectProjectile(damage.Usage.ActionId, impact.Projectile.Direction, root)
                : CharacterTestAccessoryHitEvidence.Derived.WithSource(root);
            SetAccessoryAwareDamagePayload(hitKey, damage, witness.WithCollision(frozenActor != null ? frozenActor.Actor : source.GameplayEntityId, target,
                target != null && (frozenActor != null ? frozenActor.Team : source.Team.TeamId) != target.Team.TeamId,
                world.DoubleEdgeState.SecondBatchProducer(root, 1, impact.Projectile.ProjectileId, impact.Projectile.SpawnSequence),
                unchecked((ulong)(uint)impact.Projectile.SpawnSequence + 1)));
            _hitZonesByHit[hitKey] = hitZone;
            CombatHitEventConfig hitEvent =
                CharacterTestCombatDamageResolver.ResolveProjectileHitEvent(definition.Impact, impact.Context);
            if (hitEvent.OnHitBuffCount > 0)
                _onHitBuffsByHit[hitKey] = hitEvent.OnHitBuffs;
        }

        private int CountProjectileImpactQueries()
        {
            return _projectileFrameEvents.Impacts.Count;
        }

        private void BuildRunningActions(
            CharacterTestCharacterActionRuntime actionRuntime,
            ICharacterTestStatusControlProvider statusControl)
        {
            IReadOnlyList<CharacterTestActiveHitTrace> activeTraces = _windowState.ActiveTraces;
            for (int i = 0; i < activeTraces.Count; i++)
            {
                CharacterTestActiveHitTrace trace = activeTraces[i];
                if (trace.HitVolume.HasVolume)
                    continue;

                CharacterTestCharacterEntity entity = _entityLookup.FindEntity(trace.EntityId);
                if (entity == null || !entity.BattleState.CountsAsAlive)
                    continue;
                if (statusControl != null && statusControl.IsActionFrozen(entity))
                    continue;

                if (!TryResolveActiveAnimationClipKey(
                    actionRuntime,
                    trace.ActionId,
                    entity.Action.LocalFrame,
                    out string clipKey))
                {
                    ReportHitTraceError(
                        entity.Id.Value,
                        trace.ActionId,
                        trace.DescribeSelector(),
                        string.Empty,
                        entity.Action.LocalFrame,
                        "HitTraceAnimationClipMissing",
                        "Active hit trace cannot resolve an animation clip for the current action frame.");
                    continue;
                }

                if (!_traceProvider.TryPrepare(
                    trace,
                    entity,
                    clipKey,
                    entity.Action.LocalFrame,
                    out CharacterTestWeaponTraceBinding binding))
                {
                    continue;
                }

                int actionId = StablePositiveId(trace.ActionId);
                int traceId = binding.TraceProfileId;
                _payloadsByTrace[new CharacterTestTracePayloadKey(trace.HitWindowId, traceId)] =
                    new CharacterTestTracePayload(trace, binding, clipKey, entity.Action.LocalFrame);
                _runningActions.Add(new CombatActionState(
                    _physicsRuntime.GetEntityId(entity),
                    actionId,
                    entity.Action.LocalFrame,
                    new CombatFrame(trace.StartFrame),
                    CombatActionPhase.Active,
                    trace.HitWindowId));
            }
        }

        private void EvaluateWeaponTracePlan(CombatFrame currentFrame)
        {
            int queryId = 0;
            for (int actionIndex = 0; actionIndex < _runningActions.Count; actionIndex++)
            {
                CombatActionState actionState = _runningActions[actionIndex];
                if (actionState.Phase != CombatActionPhase.Active)
                    continue;
                CharacterTestCharacterEntity actionEntity =
                    _entityLookup.FindEntityByCombatId(actionState.EntityId);
                if (actionEntity == null)
                    continue;

                int planIndex = _directHitTracePlanBuffer.GetOrBeginPlan(
                    actionState.EntityId,
                    actionState.ActionInstanceId,
                    CharacterTestDirectHitTracePlanPoseIdentity.CreateActualExecution(
                        actionEntity.Transform,
                        actionEntity.SourcePoseRevision,
                        actionEntity.SourceFacingRevision));
                _directHitTracePlanSamples = _directHitTracePlanBuffer.GetPlanSamplesBuffer(planIndex);
                if (_directHitTracePlanSamples == null)
                    continue;
                CharacterTestDirectHitTraceBounds actionBounds =
                    _directHitTracePlanBuffer.GetPlanBounds(planIndex);
                _directHitTraceFrames.Clear();
                _directHitIncomingMotionFrames.Clear();
                _directHitTracePolicyEventIds.Clear();
                _traceProvider.GetActiveTraces(
                    actionState.EntityId,
                    actionState.ActionId,
                    actionState.ActionInstanceId,
                    actionState.LocalFrame,
                    _directHitTraceFrames,
                    _directHitTracePolicyEventIds,
                    _directHitIncomingMotionFrames);

                if (_directHitTracePolicyEventIds.Count != _directHitTraceFrames.Count
                    || _directHitIncomingMotionFrames.Count != _directHitTraceFrames.Count)
                    continue;

                for (int traceIndex = 0; traceIndex < _directHitTraceFrames.Count; traceIndex++)
                {
                    int traceSampleStart = _directHitTracePlanSamples.Count;
                    CharacterTestDirectHitTracePlanSampler.AppendWeaponTraceSamples(
                        _directHitTraceFrames[traceIndex],
                        actionState.EntityId,
                        actionState.ActionId,
                        actionState.ActionInstanceId,
                        actionState.LocalFrame,
                        traceIndex,
                        _directHitTracePolicyEventIds[traceIndex],
                        true,
                        AttackTraceSweepMaxTipDistancePerSubstepFix,
                        AttackTraceSweepMaxSubsteps,
                        ref queryId,
                        ref _directHitTraceSampleOrder,
                        _directHitTraceQueryBuffer,
                        _directHitTracePlanSamples,
                        ref actionBounds,
                        incomingMotion: _directHitIncomingMotionFrames[traceIndex]);

                    for (int sampleIndex = traceSampleStart;
                         sampleIndex < _directHitTracePlanSamples.Count;
                         sampleIndex++)
                    {
                        QueryDirectHitTraceSample(
                            _directHitTracePlanSamples[sampleIndex],
                            currentFrame);
                    }
                }

                _directHitTracePlanBuffer.UpdatePlan(
                    planIndex,
                    _directHitTracePlanSamples.Count,
                    actionBounds);
            }
        }

        private int QueryDirectHitTraceSample(
            CharacterTestDirectHitTracePlanSample sample,
            CombatFrame currentFrame)
        {
            if (sample.PolicyEventId > 0)
                _directHitTracePolicyEventIdsByQuery[sample.Query.Header] = sample.PolicyEventId;

            _directHitTraceQueryResults.Clear();
            _physicsRuntime.World.QueryCapsule(
                sample.Query,
                _directHitTraceQueryResults,
                includeSourceEntity: false);

            for (int i = 0; i < _directHitTraceQueryResults.Count; i++)
            {
                CombatQueryResult physicsHit = _directHitTraceQueryResults[i];
                _candidates.Add(new HitCandidate(
                    sample.Query.Header.SourceEntityId,
                    physicsHit.TargetEntityId,
                    sample.ActionId,
                    sample.HitWindowId,
                    sample.TraceId,
                    currentFrame,
                    physicsHit,
                    damage: 0,
                    staggerFrames: 0,
                    knockback: FixVector3.Zero,
                    targetState: _entityLookup.ResolveTargetState(physicsHit.TargetEntityId),
                    resolvePriority: sample.ResolvePriority,
                    incomingDirection: sample.ResolveIncomingDisplacement(physicsHit.Point)));
            }

            return _directHitTraceQueryResults.Count;
        }

        private void ProcessActiveHitVolumes(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            ICharacterTestStatusControlProvider statusControl,
            RuntimeFrame frame)
        {
            if (world == null || _physicsRuntime == null || _physicsRuntime.World == null)
                return;

            IReadOnlyList<CharacterTestActiveHitTrace> activeTraces = _windowState.ActiveTraces;
            for (int i = 0; i < activeTraces.Count; i++)
            {
                CharacterTestActiveHitTrace trace = activeTraces[i];
                if (!trace.HitVolume.HasVolume)
                    continue;

                CharacterTestCharacterEntity entity = _entityLookup.FindEntity(trace.EntityId);
                if (entity == null)
                    continue;
                if (statusControl != null && statusControl.IsActionFrozen(entity))
                    continue;

                QueryActiveHitVolume(entity, trace, actionRuntime, frame, i);
            }

            PruneCasterBodyHitVolumeCache(activeTraces);
        }

        private void QueryActiveHitVolume(
            CharacterTestCharacterEntity entity,
            CharacterTestActiveHitTrace trace,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            int sourceOrder)
        {
            if (trace.HitVolume.ShapeKind != CharacterActionHitVolumeShapeKind.Capsule)
                return;

            CombatEntityId sourceCombatId = _physicsRuntime.GetEntityId(entity);
            int actionId = StablePositiveId(trace.ActionId);
            if (trace.StableEventId <= 0)
                throw new InvalidOperationException("Hit volume requires a prepared positive event identity.");
            int traceId = trace.StableEventId;
            int planIndex = _directHitTracePlanBuffer.GetOrBeginPlan(
                sourceCombatId,
                trace.HitWindowId,
                CharacterTestDirectHitTracePlanPoseIdentity.CreateActualExecution(
                    entity.Transform,
                    entity.SourcePoseRevision,
                    entity.SourceFacingRevision));
            _directHitTracePlanSamples = _directHitTracePlanBuffer.GetPlanSamplesBuffer(planIndex);
            if (_directHitTracePlanSamples == null)
                return;
            CharacterTestDirectHitTraceBounds actionBounds =
                _directHitTracePlanBuffer.GetPlanBounds(planIndex);
            var payloadKey = new CharacterTestTracePayloadKey(trace.HitWindowId, traceId);
            bool usesCasterBody = trace.HitVolume.SourceKind == CharacterActionHitVolumeSourceKind.CasterBody;
            bool usesBakedSockets = trace.HitVolume.SourceKind == CharacterActionHitVolumeSourceKind.BakedSocketCapsule;
            try
            {
                _payloadsByTrace[payloadKey] = new CharacterTestTracePayload(
                trace,
                new CharacterTestWeaponTraceBinding(
                    trace.StableEventId,
                    usesCasterBody
                        ? ResolveCasterBodyHitVolumeLength(entity, trace.HitVolume)
                        : usesBakedSockets ? 0f : ResolveHitVolumeLength(trace.HitVolume),
                    usesCasterBody
                        ? ResolveCasterBodyHitVolumeRadius(entity, trace.HitVolume)
                        : trace.HitVolume.Radius,
                    equipmentSlotId: trace.EquipmentSlotId),
                string.Empty,
                entity.Action.LocalFrame);

            if (usesCasterBody)
            {
                QueryCasterBodyHitVolume(
                    entity,
                    trace,
                    frame,
                    sourceOrder,
                    sourceCombatId,
                    actionId,
                    traceId,
                    ref actionBounds);
                return;
            }

            if (usesBakedSockets)
            {
                QueryBakedSocketHitVolume(
                    entity,
                    trace,
                    actionRuntime,
                    frame,
                    sourceOrder,
                    sourceCombatId,
                    actionId,
                    traceId,
                    ref actionBounds);
                return;
            }

            CharacterTestDirectHitTraceSourcePose sourcePose =
                CharacterTestDirectHitTraceSourcePose.FromTransform(
                    entity.Action.LocalFrame,
                    entity.Transform);
            if (!CharacterTestDirectHitTracePlanSampler.TryBuildManualCapsule(
                    sourcePose,
                    trace.HitVolume,
                    out FixVector3 pointA,
                    out FixVector3 pointB,
                    out Fix64 radius))
                return;

            int candidateCount = QueryHitVolumeCapsule(
                trace,
                sourceCombatId,
                actionId,
                traceId,
                sourceOrder,
                queryOffset: 0,
                pointA,
                pointB,
                radius,
                frame,
                entity.Action.LocalFrame,
                ref actionBounds);

            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                entity.Id.Value,
                "hitVolume candidates=" + candidateCount.ToString(CultureInfo.InvariantCulture)
                + " action=" + CharacterTestCombatTraceText.EmptyAsNone(trace.ActionId)
                + " event=" + CharacterTestCombatTraceText.EmptyAsNone(trace.StableEventId));
            }
            finally
            {
                _directHitTracePlanBuffer.UpdatePlan(
                    planIndex,
                    _directHitTracePlanSamples.Count,
                    actionBounds);
            }
        }

        private void QueryBakedSocketHitVolume(
            CharacterTestCharacterEntity entity,
            CharacterTestActiveHitTrace trace,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            int sourceOrder,
            CombatEntityId sourceCombatId,
            int actionId,
            int traceId,
            ref CharacterTestDirectHitTraceBounds actionBounds)
        {
            string clipKey = string.Empty;
            ICharacterTestPackageBakedSocketSource packageSocketSource =
                _bakedTraceSource as ICharacterTestPackageBakedSocketSource;
            if (packageSocketSource == null
                || !TryResolveActiveAnimationClipKey(
                    actionRuntime,
                    trace.ActionId,
                    entity.Action.LocalFrame,
                    out clipKey)
                || !packageSocketSource.TryGetSocketFrame(
                    entity.Identity.GeneratedPackageRoot,
                    clipKey,
                    trace.HitVolume.StartSocketId,
                    entity.Action.LocalFrame,
                    out MxAnimationBakedSocketFrame startFrame)
                || !packageSocketSource.TryGetSocketFrame(
                    entity.Identity.GeneratedPackageRoot,
                    clipKey,
                    trace.HitVolume.EndSocketId,
                    entity.Action.LocalFrame,
                    out MxAnimationBakedSocketFrame endFrame))
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    trace.ActionId,
                    trace.DescribeSelector(),
                    clipKey ?? string.Empty,
                    entity.Action.LocalFrame,
                    "BakedSocketHitVolumeFrameMissing",
                    "Baked socket capsule requires both socket tracks at the active frame; the query failed closed.");
                return;
            }

            CharacterTestDirectHitTraceSourcePose sourcePose =
                CharacterTestDirectHitTraceSourcePose.FromTransform(
                    entity.Action.LocalFrame,
                    entity.Transform);
            if (!CharacterTestDirectHitTracePlanSampler.TryBuildBakedSocketCapsule(
                    sourcePose,
                    startFrame.Position,
                    endFrame.Position,
                    trace.HitVolume.Radius,
                    out FixVector3 pointA,
                    out FixVector3 pointB,
                    out Fix64 radius))
                return;

            TryGetAuthorityPoseSocketInputs(
                _bakedTraceSource,
                entity,
                out CharacterAuthorityPoseRigRuntimeBinding authorityPoseRig,
                out CharacterSocketBakeCatalog authorityPoseCatalog);

            FixVector3 startLocal = ToFixVector3(startFrame.Position);
            if (CharacterTestAuthorityPoseConsumerMath.TryResolveAttachmentDelta(
                    entity,
                    authorityPoseRig,
                    authorityPoseCatalog,
                    CharacterAuthorityPoseRigAttachmentKind.Socket,
                    trace.HitVolume.StartSocketId,
                    clipKey,
                    entity.Action.LocalFrame,
                    loop: false,
                    frame,
                    out CharacterTestAuthorityPoseAttachmentDelta startDelta))
            {
                pointA = sourcePose.TransformLocal(startDelta.TransformPoint(startLocal));
            }

            FixVector3 endLocal = ToFixVector3(endFrame.Position);
            if (CharacterTestAuthorityPoseConsumerMath.TryResolveAttachmentDelta(
                    entity,
                    authorityPoseRig,
                    authorityPoseCatalog,
                    CharacterAuthorityPoseRigAttachmentKind.Socket,
                    trace.HitVolume.EndSocketId,
                    clipKey,
                    entity.Action.LocalFrame,
                    loop: false,
                    frame,
                    out CharacterTestAuthorityPoseAttachmentDelta endDelta))
            {
                pointB = sourcePose.TransformLocal(endDelta.TransformPoint(endLocal));
            }

            int candidateCount = QueryHitVolumeCapsule(
                trace,
                sourceCombatId,
                actionId,
                traceId,
                sourceOrder,
                queryOffset: 0,
                pointA,
                pointB,
                radius,
                frame,
                entity.Action.LocalFrame,
                ref actionBounds);

            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                entity.Id.Value,
                "bakedSocketHitVolume candidates=" + candidateCount.ToString(CultureInfo.InvariantCulture)
                + " action=" + CharacterTestCombatTraceText.EmptyAsNone(trace.ActionId)
                + " sockets=" + trace.HitVolume.StartSocketId + ".." + trace.HitVolume.EndSocketId);
        }

        internal static bool TryGetAuthorityPoseSocketInputs(
            ICharacterTestBakedWeaponTraceSource bakedTraceSource,
            CharacterTestCharacterEntity entity,
            out CharacterAuthorityPoseRigRuntimeBinding rig,
            out CharacterSocketBakeCatalog catalog)
        {
            rig = null;
            catalog = null;
            if (!(bakedTraceSource is CharacterTestDefaultBakedWeaponTraceSource packageSource)
                || entity == null
                || !packageSource.TryGetPackageSocketCatalog(
                    entity.Identity.GeneratedPackageRoot,
                    out catalog))
            {
                catalog = null;
                return false;
            }

            rig = CharacterTestGeneratedPackageSource
                .LoadPackageForEntity(entity)
                ?.Geometry
                ?.AuthorityPoseRig;
            if (rig != null)
                return true;

            catalog = null;
            return false;
        }

        private void QueryCasterBodyHitVolume(
            CharacterTestCharacterEntity entity,
            CharacterTestActiveHitTrace trace,
            RuntimeFrame frame,
            int sourceOrder,
            CombatEntityId sourceCombatId,
            int actionId,
            int traceId,
            ref CharacterTestDirectHitTraceBounds actionBounds)
        {
            if (!TryResolveCasterBodyHitVolume(
                    entity,
                    trace.HitVolume,
                    out FixVector3 center,
                    out FixVector3 pointA,
                    out FixVector3 pointB,
                    out Fix64 radius,
                    out Fix64 segmentHalf))
            {
                return;
            }

            int candidateCount = QueryHitVolumeCapsule(
                trace,
                sourceCombatId,
                actionId,
                traceId,
                sourceOrder,
                queryOffset: 0,
                pointA,
                pointB,
                radius,
                frame,
                entity.Action.LocalFrame,
                ref actionBounds);

            var key = new CharacterTestBodyHitVolumeCacheKey(entity.Id.Value, trace.HitWindowId);
            if (_bodyHitVolumePreviousCenters.TryGetValue(key, out FixVector3 previousCenter)
                && !previousCenter.Equals(center))
            {
                FixVector3 vertical = new FixVector3(Fix64.Zero, segmentHalf, Fix64.Zero);
                if (segmentHalf > Fix64.Zero)
                {
                    candidateCount += QueryHitVolumeCapsule(
                        trace,
                        sourceCombatId,
                        actionId,
                        traceId,
                        sourceOrder,
                        queryOffset: 1,
                        previousCenter - vertical,
                        center - vertical,
                        radius,
                        frame,
                        entity.Action.LocalFrame,
                        ref actionBounds);
                    candidateCount += QueryHitVolumeCapsule(
                        trace,
                        sourceCombatId,
                        actionId,
                        traceId,
                        sourceOrder,
                        queryOffset: 2,
                        previousCenter,
                        center,
                        radius,
                        frame,
                        entity.Action.LocalFrame,
                        ref actionBounds);
                    candidateCount += QueryHitVolumeCapsule(
                        trace,
                        sourceCombatId,
                        actionId,
                        traceId,
                        sourceOrder,
                        queryOffset: 3,
                        previousCenter + vertical,
                        center + vertical,
                        radius,
                        frame,
                        entity.Action.LocalFrame,
                        ref actionBounds);
                }
                else
                {
                    candidateCount += QueryHitVolumeCapsule(
                        trace,
                        sourceCombatId,
                        actionId,
                        traceId,
                        sourceOrder,
                        queryOffset: 1,
                        previousCenter,
                        center,
                        radius,
                        frame,
                        entity.Action.LocalFrame,
                        ref actionBounds);
                }
            }

            _bodyHitVolumePreviousCenters[key] = center;

            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                entity.Id.Value,
                "casterBodyHitVolume candidates=" + candidateCount.ToString(CultureInfo.InvariantCulture)
                + " action=" + CharacterTestCombatTraceText.EmptyAsNone(trace.ActionId)
                + " event=" + CharacterTestCombatTraceText.EmptyAsNone(trace.StableEventId));
        }

        private int QueryHitVolumeCapsule(
            CharacterTestActiveHitTrace trace,
            CombatEntityId sourceCombatId,
            int actionId,
            int traceId,
            int sourceOrder,
            int queryOffset,
            FixVector3 pointA,
            FixVector3 pointB,
            Fix64 radius,
            RuntimeFrame frame,
            int localFrame,
            ref CharacterTestDirectHitTraceBounds actionBounds)
        {
            CharacterTestDirectHitTraceSampleKind kind =
                trace.HitVolume.SourceKind == CharacterActionHitVolumeSourceKind.CasterBody
                    ? CharacterTestDirectHitTraceSampleKind.CasterBody
                    : trace.HitVolume.SourceKind == CharacterActionHitVolumeSourceKind.BakedSocketCapsule
                        ? CharacterTestDirectHitTraceSampleKind.BakedSocketCapsule
                        : CharacterTestDirectHitTraceSampleKind.ManualCapsule;
            if (!CharacterTestDirectHitTracePlanSampler.TryAppendHitVolumeSample(
                    kind,
                    sourceCombatId,
                    actionId,
                    traceId,
                    StablePolicyEventId(trace.ActionId, trace.StableEventId),
                    trace.HitWindowId,
                    localFrame,
                    sourceOrder,
                    queryOffset,
                    CharacterTestCombatHitEventResolver.ResolveQueryLayerMask(
                        trace.HitEvent,
                        _physicsRuntime),
                    pointA,
                    pointB,
                    radius,
                    ref _directHitTraceSampleOrder,
                    _directHitTracePlanSamples,
                    ref actionBounds,
                    out CharacterTestDirectHitTracePlanSample sample))
            {
                return 0;
            }

            return QueryDirectHitTraceSample(sample, ToCombatFrame(frame));
        }

        private bool TryResolveCasterBodyHitVolume(
            CharacterTestCharacterEntity entity,
            CharacterActionHitVolumeConfig hitVolume,
            out FixVector3 center,
            out FixVector3 pointA,
            out FixVector3 pointB,
            out Fix64 radius,
            out Fix64 segmentHalf)
        {
            CharacterTestDirectHitTraceSourcePose sourcePose =
                CharacterTestDirectHitTraceSourcePose.FromTransform(
                    entity.Action.LocalFrame,
                    entity.Transform);
            return CharacterTestDirectHitTracePlanSampler.TryBuildCasterBodyCapsule(
                sourcePose,
                ResolveCharacterBodyCenter(entity),
                _physicsRuntime.GetCharacterHalfExtents(entity),
                hitVolume,
                out center,
                out pointA,
                out pointB,
                out radius,
                out segmentHalf);
        }

        private FixVector3 ResolveCharacterBodyCenter(CharacterTestCharacterEntity entity)
        {
            if (_physicsRuntime?.World != null
                && _physicsRuntime.World.TryGetBody(_physicsRuntime.GetBodyId(entity), out CombatPhysicsBody body))
            {
                return body.Position;
            }

            return new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.YMillimeters)
                    + _physicsRuntime.GetCharacterCenterOffsetY(entity),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.ZMillimeters));
        }

        private static float ResolveCasterBodyHitVolumeLength(
            CharacterTestCharacterEntity entity,
            CharacterActionHitVolumeConfig hitVolume)
        {
            if (entity == null || !entity.Physics.IsConfigured)
            {
                return ResolveCasterBodyHitVolumeSegmentLength(
                    CharacterTestCharacterPhysicsComponent.DefaultCapsuleRadiusMeters,
                    CharacterTestCharacterPhysicsComponent.DefaultCapsuleHeightMeters,
                    hitVolume);
            }

            return ResolveCasterBodyHitVolumeSegmentLength(
                entity.Physics.EffectiveRadiusMeters,
                entity.Physics.EffectiveHeightMeters,
                hitVolume);
        }

        private static float ResolveCasterBodyHitVolumeRadius(
            CharacterTestCharacterEntity entity,
            CharacterActionHitVolumeConfig hitVolume)
        {
            float baseRadius = entity == null || !entity.Physics.IsConfigured
                ? CharacterTestCharacterPhysicsComponent.DefaultCapsuleRadiusMeters
                : entity.Physics.EffectiveRadiusMeters;
            return Math.Max(0f, baseRadius * hitVolume.BodyRadiusScale + hitVolume.BodyRadiusAdd);
        }

        private static float ResolveCasterBodyHitVolumeSegmentLength(
            float baseRadius,
            float baseHeight,
            CharacterActionHitVolumeConfig hitVolume)
        {
            float radius = Math.Max(0f, baseRadius * hitVolume.BodyRadiusScale + hitVolume.BodyRadiusAdd);
            float height = Math.Max(0f, baseHeight * hitVolume.BodyHeightScale + hitVolume.BodyHeightAdd);
            return Math.Max(0f, (height * 0.5f - radius) * 2f);
        }

        private void PruneCasterBodyHitVolumeCache(IReadOnlyList<CharacterTestActiveHitTrace> activeTraces)
        {
            if (_bodyHitVolumePreviousCenters.Count == 0)
                return;

            _bodyHitVolumeCacheRemovals.Clear();
            foreach (KeyValuePair<CharacterTestBodyHitVolumeCacheKey, FixVector3> pair in _bodyHitVolumePreviousCenters)
            {
                if (!IsActiveCasterBodyHitTrace(activeTraces, pair.Key))
                    _bodyHitVolumeCacheRemovals.Add(pair.Key);
            }

            for (int i = 0; i < _bodyHitVolumeCacheRemovals.Count; i++)
                _bodyHitVolumePreviousCenters.Remove(_bodyHitVolumeCacheRemovals[i]);
            _bodyHitVolumeCacheRemovals.Clear();
        }

        private static bool IsActiveCasterBodyHitTrace(
            IReadOnlyList<CharacterTestActiveHitTrace> activeTraces,
            CharacterTestBodyHitVolumeCacheKey key)
        {
            for (int i = 0; activeTraces != null && i < activeTraces.Count; i++)
            {
                CharacterTestActiveHitTrace trace = activeTraces[i];
                if (trace.EntityId == key.EntityId
                    && trace.HitWindowId == key.HitWindowId
                    && trace.HitVolume.SourceKind == CharacterActionHitVolumeSourceKind.CasterBody
                    && trace.HitVolume.HasVolume)
                {
                    return true;
                }
            }

            return false;
        }

        private static float ResolveHitVolumeLength(CharacterActionHitVolumeConfig hitVolume)
        {
            float dx = hitVolume.LocalPointBX - hitVolume.LocalPointAX;
            float dy = hitVolume.LocalPointBY - hitVolume.LocalPointAY;
            float dz = hitVolume.LocalPointBZ - hitVolume.LocalPointAZ;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static bool TryResolveActiveAnimationClipKey(
            CharacterTestCharacterActionRuntime actionRuntime,
            CharacterActionId actionId,
            int localFrame,
            out string clipKey)
        {
            clipKey = string.Empty;
            if (actionRuntime == null || !actionRuntime.Registry.TryGetAction(actionId, out CharacterActionConfig action))
                return false;

            AnimationTrackEvent[] events = action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEvent>();
            for (int i = 0; i < events.Length; i++)
            {
                AnimationTrackEvent candidate = events[i];
                if (localFrame < candidate.Frame || localFrame > candidate.EndFrame)
                    continue;
                if (!string.IsNullOrWhiteSpace(candidate.EffectiveAnimationKey))
                {
                    clipKey = candidate.EffectiveAnimationKey;
                    return true;
                }
            }

            return false;
        }

    }
}

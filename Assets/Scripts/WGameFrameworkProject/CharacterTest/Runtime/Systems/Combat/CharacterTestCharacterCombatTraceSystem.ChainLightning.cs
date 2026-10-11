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
        private void ProcessChainLightningRequests(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame)
        {
            if (world == null || _physicsRuntime == null || _physicsRuntime.World == null)
                return;

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
            {
                CharacterTestCharacterEntity entity = entities[entityIndex];
                IReadOnlyList<CharacterActionGameplayRequest> requests = entity.GameplayRequests;
                for (int requestIndex = 0; requestIndex < requests.Count; requestIndex++)
                {
                    CharacterActionGameplayRequest request = requests[requestIndex];
                    if (request.EventKind == CharacterActionTrackEventKind.SendGameplayRequest
                        && (request.RequestId == CharacterTestCharacterActionConfigSource.ChainLightningGameplayRequestId))
                    {
                        ProcessChainLightningRequest(entity, request, actionRuntime, frame, requestIndex);
                    }
                }
            }
        }

        private void ProcessChainLightningRequest(
            CharacterTestCharacterEntity source,
            CharacterActionGameplayRequest request,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            int requestIndex)
        {
            if (source == null || !source.BattleState.CountsAsAlive)
                return;

            CombatEntityId sourceCombatId = _physicsRuntime.GetEntityId(source);
            int actionId = StablePositiveId(request.Metadata.ActionId.Value);
            int actionInstanceId = StablePositiveId(request.Metadata.InstanceId);
            int traceId = StablePositiveId(request.Metadata.StableEventId);
            FixVector3 origin = ResolveChainLightningOrigin(source, request, actionRuntime);
            FixVector3 forward = CharacterTestFireballProjectileAdapter.ResolveDirection(source);
            if (!forward.TryNormalize(out forward))
                forward = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

            _chainLightningVisitedTargets.Clear();
            if (!TrySelectChainLightningFirstTarget(
                    source,
                    sourceCombatId,
                    origin,
                    forward,
                    actionId,
                    traceId,
                    requestIndex,
                    out CombatQueryResult selectedQuery,
                    out CharacterTestCharacterEntity selectedTarget,
                    out FixVector3 selectedPoint))
            {
                GetVerboseCombatTraceRecorder()?.AttackCombat(
                    frame,
                    source.Id.Value,
                    "chainLightning emptyCast request="
                    + CharacterTestCombatTraceText.EmptyAsNone(request.RequestId));
                return;
            }

            FixVector3 segmentOrigin = origin;
            int segmentIndex = 0;
            while (selectedTarget != null && segmentIndex < ChainLightningMaxTargets)
            {
                CombatEntityId targetCombatId = _physicsRuntime.GetEntityId(selectedTarget);
                _chainLightningVisitedTargets.Add(targetCombatId);
                ProcessChainLightningTarget(
                    source,
                    sourceCombatId,
                    request,
                    selectedQuery,
                    selectedTarget,
                    selectedPoint,
                    segmentOrigin,
                    actionId,
                    actionInstanceId,
                    traceId,
                    frame,
                    segmentIndex);

                LastChainLightningTargetCount++;
                segmentOrigin = selectedPoint;
                segmentIndex++;
                if (segmentIndex >= ChainLightningMaxTargets)
                    break;

                if (!TrySelectChainLightningJumpTarget(
                        source,
                        sourceCombatId,
                        segmentOrigin,
                        actionId,
                        traceId,
                        segmentIndex,
                        out selectedQuery,
                        out selectedTarget,
                        out selectedPoint))
                {
                    break;
                }
            }

            _chainLightningVisitedTargets.Clear();
            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                source.Id.Value,
                "chainLightning targets="
                + LastChainLightningTargetCount.ToString(CultureInfo.InvariantCulture)
                + " request="
                + CharacterTestCombatTraceText.EmptyAsNone(request.RequestId));
        }

        private void ProcessChainLightningTarget(
            CharacterTestCharacterEntity source,
            CombatEntityId sourceCombatId,
            CharacterActionGameplayRequest request,
            CombatQueryResult queryResult,
            CharacterTestCharacterEntity target,
            FixVector3 targetPoint,
            FixVector3 segmentOrigin,
            int actionId,
            int actionInstanceId,
            int traceId,
            RuntimeFrame frame,
            int segmentIndex)
        {
            int damageValue = ChainLightningSegmentBaseDamages[Math.Min(
                segmentIndex,
                ChainLightningSegmentBaseDamages.Length - 1)];
            CharacterActionGameplayRequest damageRequest =
                CreateChainLightningDamageRequest(request, damageValue);
            if (!_damageResolver.TryResolveGameplayDamage(
                    damageRequest,
                    source,
                    queryResult.TargetEntityId,
                    queryResult.TargetColliderId,
                    _entityLookup,
                    frame,
                    DamageProfileSourceKind.Scripted,
                    CharacterTestBodyPartDamageSourcePolicy.Ability,
                    out DamageProfileResolveResult damage,
                    out CharacterTestHitZoneResolution hitZone))
            {
                return;
            }

            var candidate = new HitCandidate(
                sourceCombatId,
                queryResult.TargetEntityId,
                actionId,
                actionInstanceId,
                traceId,
                ToCombatFrame(frame),
                queryResult,
                damage.IncomingDamage,
                CharacterTestHitReactionResolver.CreateHitResolveReactionGate(damage),
                CharacterTestHitReactionResolver.CreateHitResolveKnockbackGate(damage),
                _entityLookup.ResolveTargetState(queryResult.TargetEntityId),
                ChainLightningMaxTargets - segmentIndex);
            var hitKey = new CharacterTestResolvedHitKey(candidate);
            SetAccessoryAwareDamagePayload(hitKey, damage, CharacterTestAccessoryHitEvidence.Derived);
            _hitZonesByHit[hitKey] = hitZone;
            _gameplayProfiledCandidates.Add(candidate);
            RecordChainLightningArcVfx(
                source.Id.Value,
                target.Id.Value,
                segmentOrigin,
                targetPoint,
                request.Metadata.LocalFrame,
                actionInstanceId,
                segmentIndex,
                frame);
        }

        private static CharacterActionGameplayRequest CreateChainLightningDamageRequest(
            CharacterActionGameplayRequest request,
            int baseDamage)
        {
            var payload = new CharacterActionDamagePayload(
                CharacterTestAuthoredContentIds.D409_Id9,
                DamageFormulaIds.CharacterTestSoftDefenseV1,
                baseDamage,
                sourceAttributeScales: null,
                ChainLightningImpactPower,
                ChainLightningDamageTags,
                CharacterTestAuthoredContentIds.D411_Id9,
                CharacterTestAuthoredContentIds.D407_Id8, presentationProfileLabel: CharacterTestCharacterActionConfigSource.ChainLightningPresentationProfileLabel);
            return new CharacterActionGameplayRequest(
                request.Metadata,
                request.GameplayEntityId,
                CharacterActionTrackEventKind.SendGameplayRequest,
                request.RequestId,
                request.AbilityStableId,
                request.TargetPolicyId,
                request.EffectTimelineId,
                payload);
        }

        private bool TrySelectChainLightningFirstTarget(
            CharacterTestCharacterEntity source,
            CombatEntityId sourceCombatId,
            FixVector3 origin,
            FixVector3 forward,
            int actionId,
            int traceId,
            int requestIndex,
            out CombatQueryResult queryResult,
            out CharacterTestCharacterEntity target,
            out FixVector3 targetPoint)
        {
            if (TrySelectLockedChainLightningTarget(
                    source,
                    sourceCombatId,
                    origin,
                    actionId,
                    traceId,
                    requestIndex,
                    out queryResult,
                    out target,
                    out targetPoint))
            {
                return true;
            }

            return TrySelectNearestChainLightningTarget(
                source,
                sourceCombatId,
                origin,
                forward,
                ChainLightningFirstRange,
                actionId,
                traceId,
                requestIndex,
                requireForwardCone: true,
                out queryResult,
                out target,
                out targetPoint);
        }

        private bool TrySelectLockedChainLightningTarget(
            CharacterTestCharacterEntity source,
            CombatEntityId sourceCombatId,
            FixVector3 origin,
            int actionId,
            int traceId,
            int requestIndex,
            out CombatQueryResult queryResult,
            out CharacterTestCharacterEntity target,
            out FixVector3 targetPoint)
        {
            queryResult = default;
            target = null;
            targetPoint = FixVector3.Zero;
            bool hasCompositeTarget = TryResolveLockedCombatTarget(
                source,
                out CharacterTestCombatTargetHandle combatTarget,
                out CharacterTestLockedHurtboxDistanceSample lockedHurtbox);
            int lockedTargetId = hasCompositeTarget
                ? combatTarget.TargetEntityId
                : ResolveLockedTargetEntityId();
            if (lockedTargetId <= 0)
                return false;
            if (_lockedCombatTargetProvider != null && !hasCompositeTarget)
                return false;

            target = _entityLookup.FindEntity(lockedTargetId);
            if (target == null)
                return false;

            CombatEntityId targetCombatId = _physicsRuntime.GetEntityId(target);
            if (!IsValidChainLightningTarget(source, sourceCombatId, target, targetCombatId))
                return false;

            targetPoint = hasCompositeTarget
                ? lockedHurtbox.AimPoint
                : ResolveProjectileAimTargetPoint(target, Fix64.Zero);
            FixVector3 delta = targetPoint - origin;
            if (delta.LengthSquared() > ChainLightningFirstRange * ChainLightningFirstRange)
                return false;
            if (!delta.TryNormalize(out FixVector3 direction))
                return false;

            var header = new CombatQueryHeader(
                traceId + requestIndex + 1,
                CombatQueryKind.Sphere,
                sourceCombatId,
                traceId,
                actionId,
                ChainLightningSourceOrderBase + requestIndex,
                CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer));
            queryResult = new CombatQueryResult(
                header,
                targetCombatId,
                _physicsRuntime.GetHurtboxBodyId(target),
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart),
                CombatPhysicsShapeKind.Capsule,
                delta.LengthSquared().Sqrt(),
                targetPoint,
                -direction);
            return true;
        }

        private bool TrySelectChainLightningJumpTarget(
            CharacterTestCharacterEntity source,
            CombatEntityId sourceCombatId,
            FixVector3 origin,
            int actionId,
            int traceId,
            int segmentIndex,
            out CombatQueryResult queryResult,
            out CharacterTestCharacterEntity target,
            out FixVector3 targetPoint)
        {
            return TrySelectNearestChainLightningTarget(
                source,
                sourceCombatId,
                origin,
                FixVector3.Zero,
                ChainLightningJumpRange,
                actionId,
                traceId,
                segmentIndex,
                requireForwardCone: false,
                out queryResult,
                out target,
                out targetPoint);
        }

        private bool TrySelectNearestChainLightningTarget(
            CharacterTestCharacterEntity source,
            CombatEntityId sourceCombatId,
            FixVector3 origin,
            FixVector3 forward,
            Fix64 range,
            int actionId,
            int traceId,
            int sourceOrder,
            bool requireForwardCone,
            out CombatQueryResult queryResult,
            out CharacterTestCharacterEntity target,
            out FixVector3 targetPoint)
        {
            queryResult = default;
            target = null;
            targetPoint = FixVector3.Zero;
            int startCount = _chainLightningQueryResults.Count;
            var header = new CombatQueryHeader(
                traceId + sourceOrder + 1,
                CombatQueryKind.Sphere,
                sourceCombatId,
                traceId,
                actionId,
                ChainLightningSourceOrderBase + sourceOrder,
                CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer));
            _physicsRuntime.World.QuerySphere(
                new CombatSphereQuery(header, origin, range),
                _chainLightningQueryResults);

            bool found = false;
            Fix64 bestDistanceSq = Fix64.Zero;
            int bestEntityId = int.MaxValue;
            for (int i = startCount; i < _chainLightningQueryResults.Count; i++)
            {
                CombatQueryResult candidateQuery = _chainLightningQueryResults[i];
                CharacterTestCharacterEntity candidateTarget =
                    _entityLookup.FindEntityByCombatId(candidateQuery.TargetEntityId);
                if (!TryResolveChainLightningCandidate(
                        source,
                        sourceCombatId,
                        candidateTarget,
                        candidateQuery.TargetEntityId,
                        origin,
                        forward,
                        requireForwardCone,
                        out FixVector3 candidatePoint,
                        out Fix64 candidateDistanceSq))
                {
                    continue;
                }

                int candidateEntityId = candidateTarget.Id.Value;
                if (!found
                    || candidateDistanceSq < bestDistanceSq
                    || (candidateDistanceSq.RawValue == bestDistanceSq.RawValue
                        && candidateEntityId < bestEntityId))
                {
                    found = true;
                    bestDistanceSq = candidateDistanceSq;
                    bestEntityId = candidateEntityId;
                    queryResult = candidateQuery;
                    target = candidateTarget;
                    targetPoint = candidatePoint;
                }
            }

            return found;
        }

        private bool TryResolveChainLightningCandidate(
            CharacterTestCharacterEntity source,
            CombatEntityId sourceCombatId,
            CharacterTestCharacterEntity candidateTarget,
            CombatEntityId candidateCombatId,
            FixVector3 origin,
            FixVector3 forward,
            bool requireForwardCone,
            out FixVector3 targetPoint,
            out Fix64 distanceSq)
        {
            targetPoint = FixVector3.Zero;
            distanceSq = Fix64.Zero;
            if (!IsValidChainLightningTarget(source, sourceCombatId, candidateTarget, candidateCombatId)
                || _chainLightningVisitedTargets.Contains(candidateCombatId))
            {
                return false;
            }

            if (_perceptionFrameCache != null
                && _perceptionFrameCache.TryGetTargetGeometry(
                    _frame,
                    source,
                    candidateTarget,
                    out CharacterTestTargetGeometrySample targetGeometry))
            {
                targetPoint = targetGeometry.TargetCenter;
            }
            else
            {
                targetPoint = ResolveProjectileAimTargetPoint(candidateTarget, Fix64.Zero);
            }
            FixVector3 delta = targetPoint - origin;
            distanceSq = delta.LengthSquared();
            if (!delta.TryNormalize(out FixVector3 direction))
                return false;
            return !requireForwardCone || forward.Dot(direction) >= ChainLightningForwardDotThreshold;
        }

        private bool IsValidChainLightningTarget(
            CharacterTestCharacterEntity source,
            CombatEntityId sourceCombatId,
            CharacterTestCharacterEntity target,
            CombatEntityId targetCombatId)
        {
            if (source == null
                || target == null
                || target.Id.Value == source.Id.Value
                || targetCombatId.Equals(sourceCombatId)
                || !target.BattleState.CanReceiveHpDamage
                || _entityLookup.ResolveTargetState(targetCombatId) == HitTargetStateFlags.None)
            {
                return false;
            }

            return _teamRelationProvider.AreHostile(sourceCombatId, targetCombatId);
        }

        private FixVector3 ResolveChainLightningOrigin(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            CharacterTestCharacterActionRuntime actionRuntime)
        {
            if (TryResolveActiveAnimationClipKey(
                    actionRuntime,
                    request.Metadata.ActionId,
                    request.Metadata.LocalFrame,
                    out string clipKey)
                && _bakedTraceSource.TryGetSocketFrame(
                    clipKey,
                    ChainLightningOriginSocketId,
                    request.Metadata.LocalFrame,
                    out MxAnimationBakedSocketFrame socketFrame))
            {
                FixVector3 local = ToFixVector3(socketFrame.Position)
                    + ResolveAuthorityPoseSocketOffset(entity, ChainLightningOriginSocketId);
                return TransformLocal(entity.Transform, local);
            }

            return TransformLocal(entity.Transform, ChainLightningOriginFallbackRootOffset);
        }

        private void RecordChainLightningArcVfx(
            int sourceEntityId,
            int targetEntityId,
            FixVector3 origin,
            FixVector3 endpoint,
            int localFrame,
            int actionInstanceId,
            int segmentIndex,
            RuntimeFrame frame)
        {
            FixVector3 delta = endpoint - origin;
            Fix64 length = delta.LengthSquared().Sqrt();
            if (length <= Fix64.Zero)
                return;

            FixVector3 direction = delta.TryNormalize(out FixVector3 normalized)
                ? normalized
                : CharacterTestFireballProjectileAdapter.ResolveDirection(_entityLookup.FindEntity(sourceEntityId));
            _chainLightningVfxEvents.Add(new CharacterTestChainLightningVfxEvent(
                frame,
                sourceEntityId,
                targetEntityId,
                segmentIndex,
                actionInstanceId,
                localFrame,
                origin,
                direction,
                endpoint,
                ToMillimeters(length),
                hit: true));
        }

    }
}


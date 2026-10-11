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
        private void ProcessGuardAuraRequests(
            CharacterTestCharacterRuntimeWorld world,
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
                        && (request.RequestId == CharacterTestCharacterActionConfigSource.GuardAuraGameplayRequestId))
                    {
                        ProcessGuardAuraRequest(world, entity, request, frame);
                    }
                }
            }
        }

        private void ProcessGuardAuraRequest(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            RuntimeFrame frame)
        {
            RuntimeCommandValidationResult result = _guardAuraRuntime.EnqueueCast(
                world,
                _physicsRuntime,
                entity,
                frame,
                request.Metadata.StableEventId);
            if (!result.Success)
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.RequestId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "GuardAuraCastRejected",
                    result.Error.Message);
                return;
            }

            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                entity.Id.Value,
                "guardAuraCast request=" + CharacterTestCombatTraceText.EmptyAsNone(request.RequestId));
        }

        private void ProcessAreaConfinementRequests(
            CharacterTestCharacterRuntimeWorld world,
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
                        && (request.RequestId == CharacterTestCharacterActionConfigSource.AreaConfinementGameplayRequestId))
                    {
                        ProcessAreaConfinementRequest(world, entity, request, frame);
                    }
                }
            }
        }

        private void ProcessAreaConfinementRequest(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            RuntimeFrame frame)
        {
            CharacterTestAreaConfinementChannelStartResult result =
                _areaConfinementRuntime.BeginChannel(
                    world,
                    _physicsRuntime,
                    entity,
                    frame,
                    request.Metadata.StableEventId,
                    request.Metadata.LocalFrame,
                    StablePositiveId(request.Metadata.InstanceId),
                    _lockedTargetEntityIdProvider);
            if (!result.Success)
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.RequestId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "AreaConfinementCastRejected",
                    result.FailureReason);
                return;
            }

            SpawnAreaConfinementTelegraphVfx(entity, request, result, frame);

            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                entity.Id.Value,
                "areaConfinementChannel request=" + CharacterTestCombatTraceText.EmptyAsNone(request.RequestId)
                + " landing=" + CharacterTestAreaConfinementLandingModeText.Format(result.LandingMode)
                + " activationFrame=" + result.ActivationFrame.ToString(CultureInfo.InvariantCulture));
        }

        private void ProcessPhysicalShieldRequests(
            CharacterTestCharacterRuntimeWorld world,
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
                        && (request.RequestId == CharacterTestCharacterActionConfigSource.PhysicalShieldGameplayRequestId))
                    {
                        ProcessPhysicalShieldRequest(world, entity, request, frame);
                    }
                }
            }
        }

        private void ProcessPhysicalShieldRequest(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            RuntimeFrame frame)
        {
            RuntimeCommandValidationResult result = _physicalShieldRuntime.EnqueueCast(
                world,
                _physicsRuntime,
                entity,
                frame);
            if (!result.Success)
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.RequestId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "PhysicalShieldCastRejected",
                    result.Error.Message);
                return;
            }

            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                entity.Id.Value,
                "physicalShieldCast request=" + CharacterTestCombatTraceText.EmptyAsNone(request.RequestId));
        }

        private void SpawnAreaConfinementRuntimeVfx(RuntimeFrame frame)
        {
            SpawnAreaConfinementActivatedZoneVfx(frame);
            SpawnAreaConfinementCancelledChannelVfx(frame);
        }

        private void SpawnAreaConfinementActivatedZoneVfx(RuntimeFrame frame)
        {
            _areaConfinementActivatedZones.Clear();
            _areaConfinementRuntime.DrainLastActivatedZones(_areaConfinementActivatedZones);
            for (int i = 0; i < _areaConfinementActivatedZones.Count; i++)
            {
                CharacterTestAreaConfinementActivatedZone zone = _areaConfinementActivatedZones[i];
                SpawnAreaConfinementZoneVfx(zone, frame);
                GetVerboseCombatTraceRecorder()?.AttackCombat(
                    frame,
                    zone.CasterEntityId,
                    "areaConfinementActivate landing="
                    + CharacterTestAreaConfinementLandingModeText.Format(zone.LandingMode));
            }

            _areaConfinementActivatedZones.Clear();
        }

        private void SpawnAreaConfinementCancelledChannelVfx(RuntimeFrame frame)
        {
            _areaConfinementCancelledChannels.Clear();
            _areaConfinementRuntime.DrainLastCancelledChannels(_areaConfinementCancelledChannels);
            for (int i = 0; i < _areaConfinementCancelledChannels.Count; i++)
            {
                CharacterTestAreaConfinementCancelledChannel channel = _areaConfinementCancelledChannels[i];
                SpawnAreaConfinementGameplayVfx(
                    channel.CasterEntityId,
                    channel.Center,
                    channel.Forward,
                    channel.SourceOrder,
                    channel.LocalFrame,
                    _areaConfinementRuntime.Tuning.TelegraphCancelVfxDefinitionId,
                    _areaConfinementRuntime.Tuning.TelegraphCancelFrames,
                    CharacterTestCharacterActionConfigSource.AreaConfinementTelegraphCancelVfxStableEventId,
                    frame);
                GetVerboseCombatTraceRecorder()?.AttackCombat(
                    frame,
                    channel.CasterEntityId,
                    "areaConfinementCancel reason="
                    + CharacterTestCombatTraceText.EmptyAsNone(channel.Reason));
            }

            _areaConfinementCancelledChannels.Clear();
        }

        private void SpawnAreaConfinementTelegraphVfx(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            CharacterTestAreaConfinementChannelStartResult result,
            RuntimeFrame frame)
        {
            if (_vfxRuntime == null || entity == null || !result.Success)
                return;

            SpawnAreaConfinementGameplayVfx(
                entity.Id.Value,
                result.Center,
                result.Forward,
                result.SourceOrder,
                request.Metadata.LocalFrame,
                _areaConfinementRuntime.Tuning.TelegraphVfxDefinitionId,
                _areaConfinementRuntime.Tuning.TelegraphFrames,
                CharacterTestCharacterActionConfigSource.AreaConfinementTelegraphVfxStableEventId,
                frame);
        }

        private void SpawnAreaConfinementZoneVfx(
            CharacterTestAreaConfinementActivatedZone zone,
            RuntimeFrame frame)
        {
            if (_vfxRuntime == null || !zone.SourceCreateResult.Success)
                return;

            CharacterTestMechanicSourceDefinition definition = zone.SourceCreateResult.Definition;
            for (int i = 0; i < definition.EffectCount; i++)
            {
                CharacterTestMechanicEffectDefinition effect = definition.GetEffect(i);
                if (effect.Kind != CharacterTestMechanicEffectKind.SpawnVfxOnCreate)
                    continue;

                SpawnAreaConfinementGameplayVfx(
                    zone.CasterEntityId,
                    zone.Center,
                    zone.Forward,
                    zone.SourceOrder,
                    zone.LocalFrame,
                    effect.VfxDefinitionId,
                    (int)effect.VfxLifetimeFrames,
                    effect.TraceId,
                    frame);
            }
        }

        private void SpawnAreaConfinementGameplayVfx(
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
            FixVector3 visualCenter = ResolveAreaConfinementVfxCenter(center);
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
                    CombatVfxPhysicsAdapter.ToVfxVector(visualCenter),
                    CombatVfxPhysicsAdapter.ToVfxVector(forward))),
                new VfxParameterBlock(triggerId, localFrame),
                lifetimeFrames,
                new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.CombatRuntimeEffectVfx, casterEntityId, frame.Value, traceId)));
        }

        private void ProcessMeteorBarrageTelegraphRequest(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            RuntimeFrame frame,
            int sourceOrder)
        {
            if (!TryGetOrCreateMeteorBarrageCastContext(entity, request, frame, out MeteorBarrageCastState state))
                return;

            SpawnMeteorBarrageTelegraphVfx(entity, request, state, frame, sourceOrder);
            GetVerboseCombatTraceRecorder()?.AttackCombat(
                frame,
                entity.Id.Value,
                "meteorBarrageTelegraph fixed count="
                + CharacterTestFireballProjectileAdapter.MeteorBarrageProjectileCount.ToString(CultureInfo.InvariantCulture));
        }

        private void SpawnMeteorBarrageTelegraphVfx(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            MeteorBarrageCastState state,
            RuntimeFrame frame,
            int sourceOrder)
        {
            if (_vfxRuntime == null || entity == null)
                return;

            int triggerId = CharacterTestCharacterActionConfigSource.MeteorBarrageTelegraphVfxStableEventId;
            int actionInstanceId = StablePositiveId(request.Metadata.InstanceId);
            long actionStartFrame = frame.Value - request.Metadata.LocalFrame;
            for (int i = 0; i < CharacterTestFireballProjectileAdapter.MeteorBarrageProjectileCount; i++)
            {
                int launchLocalFrame = ResolveMeteorBarrageLaunchLocalFrame(i);
                int telegraphLocalFrame = launchLocalFrame - MeteorBarrageTelegraphLeadFrames;
                if (telegraphLocalFrame < 0)
                    telegraphLocalFrame = 0;

                long scheduledFrame = actionStartFrame + telegraphLocalFrame;
                if (scheduledFrame < frame.Value)
                    scheduledFrame = frame.Value;

                FixVector3 landingPoint = state.ResolveLandingPoint(i);
                _vfxRuntime.EnqueueSpawnRequest(new VfxSpawnRequest(
                    new RuntimeFrame(scheduledFrame),
                    VfxSourceDomains.Gameplay,
                    scheduledFrame,
                    telegraphLocalFrame,
                    sourceOrder * 100 + i,
                    triggerId,
                    entity.Id.Value,
                    0,
                    new VfxDefinitionId(CharacterTestVfxIds.MeteorBarrageTelegraphDefinitionId),
                    VfxAnchor.World(new VfxPose(
                        CombatVfxPhysicsAdapter.ToVfxVector(landingPoint),
                        CombatVfxPhysicsAdapter.ToVfxVector(state.Forward))),
                    new VfxParameterBlock(triggerId, telegraphLocalFrame, i, actionInstanceId),
                    MeteorBarrageTelegraphFallbackLifetimeFrames,
                    new RuntimeTraceIdentity(CharacterTestFrameworkTraceKinds.CombatRuntimeEffectVfx, entity.Id.Value, request.Metadata.InstanceId, i)));
            }
        }

        private FixVector3 ResolveAreaConfinementVfxCenter(FixVector3 zoneCenter)
        {
            if (_physicsRuntime == null)
                return zoneCenter;

            return new FixVector3(
                zoneCenter.X,
                zoneCenter.Y - _physicsRuntime.CharacterHalfExtents.Y,
                zoneCenter.Z);
        }

    }
}



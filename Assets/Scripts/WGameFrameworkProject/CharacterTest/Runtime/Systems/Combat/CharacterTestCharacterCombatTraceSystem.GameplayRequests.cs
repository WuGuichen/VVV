using System;
using System.Collections.Generic;
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
        private void TraceScatterPressure(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame)
        {
            CharacterTestTraceRecorder traceRecorder = GetVerboseCombatTraceRecorder();
            if (traceRecorder == null)
                return;

            IReadOnlyList<CharacterTestScatterPressureTrace> traces = _damageApplier.ScatterPressureTraces;
            for (int i = 0; i < traces.Count; i++)
            {
                CharacterTestScatterPressureTrace trace = traces[i];
                CharacterTestCharacterEntity target = _entityLookup.FindEntity(trace.TargetEntityId);
                bool hasPressureState = false;
                int scatterPressureValue = 0;
                int cohesionLimit = 0;
                PressureBand pressureBand = default;
                if (target != null && world.TryGetScatterPressure(target, out GameplayScatterPressureComponent scatterPressure))
                {
                    hasPressureState = true;
                    scatterPressureValue = scatterPressure.CurrentScatterPressure;
                    cohesionLimit = scatterPressure.CurrentCohesionLimit;
                    pressureBand = scatterPressure.CurrentBand;
                }

                traceRecorder.AttackCombatScatterPressure(
                    frame,
                    trace.SourceEntityId,
                    new CharacterTestScatterPressureTraceEvent(
                        trace.TargetEntityId,
                        trace.Delta,
                        trace.ImpactPower,
                        trace.EffectiveImpact,
                        trace.SourceMassKg,
                        trace.TargetMassKg,
                        trace.KnockbackSpeed,
                        trace.PreviewDistanceMeters,
                        hasPressureState,
                        scatterPressureValue,
                        cohesionLimit,
                        pressureBand,
                        trace.DamageId,
                        trace.TraceId));
            }

            IReadOnlyList<CohesionBreakEvent> breaks = _damageApplier.CohesionBreakEvents;
            for (int i = 0; i < breaks.Count; i++)
            {
                CohesionBreakEvent evt = breaks[i];
                CharacterTestCharacterEntity target = FindEntityByGameplayId(world, evt.EntityId);
                traceRecorder.AttackCombatCohesionBreak(
                    frame,
                    evt.SourceId,
                    new CharacterTestCohesionBreakTraceEvent(
                        target != null ? target.Id.Value : evt.EntityId.Index,
                        evt.CurrentScatterPressure,
                        evt.CurrentCohesionLimit,
                        evt.Delta,
                        evt.TraceId));
            }
        }

        private static CharacterTestCharacterEntity FindEntityByGameplayId(
            CharacterTestCharacterRuntimeWorld world,
            GameplayEntityId gameplayEntityId)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].GameplayEntityId.Equals(gameplayEntityId))
                    return entities[i];
            }

            return null;
        }

        private void ProcessGameplayDamageRequests(
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
                    if (request.EventKind == CharacterActionTrackEventKind.ApplyGameplayEffect)
                    {
                        if (!request.DamagePayload.HasInlineData)
                        {
                            continue;
                        }

                        ProcessGameplayDamageRequest(entity, request, frame, requestIndex);
                        continue;
                    }

                    if (request.EventKind == CharacterActionTrackEventKind.SendGameplayRequest
                        && (request.RequestId == CharacterTestCharacterActionConfigSource.MeteorBarrageTelegraphGameplayRequestId))
                    {
                        ProcessMeteorBarrageTelegraphRequest(entity, request, frame, requestIndex);
                        continue;
                    }

                    if (request.EventKind == CharacterActionTrackEventKind.SendGameplayRequest
                        && (request.RequestId == CharacterTestCharacterActionConfigSource.ArcMineGameplayRequestId))
                    {
                        continue;
                    }

                    if (request.EventKind == CharacterActionTrackEventKind.SendGameplayRequest
                        && CharacterTestFireballProjectileAdapter.TryResolveSpawnProfile(
                            _projectileSpawnProfiles,
                            request.RequestId,
                            out _))
                    {
                        ProcessProjectileSpawnRequest(entity, request, actionRuntime, frame, requestIndex);
                        continue;
                    }

                    if (request.EventKind == CharacterActionTrackEventKind.StartEffectTimeline)
                    {
                        StartEffectTimelineFromCharacterActionRequest(entity, request, frame);
                    }
                }

                entity.ClearGameplayRequests();
            }
        }

    }
}



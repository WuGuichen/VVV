using System.Collections.Generic;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestCombatHitResultPublisher
    {
        /// <remarks>NoAlloc on the steady-state publish path after caller-owned buffers are initialized; failure diagnostics may allocate.</remarks>
        public void Publish(
            CharacterTestCharacterRuntimeWorld world,
            IReadOnlyList<HitResolveResult> results,
            IReadOnlyList<DamageTelemetryRow> telemetryRows,
            Dictionary<CharacterTestResolvedHitKey, DamageProfileResolveResult> damagePayloadsByHit,
            RuntimeFrame frame,
            CharacterTestTraceRecorder traceRecorder,
            CharacterTestCombatEntityLookup lookup,
            int activeTraceCount,
            int candidateCount,
            CharacterTestTraceRecorder diagnosticTraceRecorder = null)
        {
            CharacterTestTraceRecorder diagnosticRecorder = diagnosticTraceRecorder ?? traceRecorder;
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                entities[i].SetLastCombatHitResults(System.Array.Empty<HitResolveResult>());
                world.SyncCombatStateHealth(entities[i]);
                CharacterTestBattleStateSystem.RefreshEntity(world, entities[i], frame, null);
            }

            for (int i = 0; i < results.Count; i++)
            {
                HitResolveResult result = results[i];
                CharacterTestCharacterEntity attacker = lookup.FindEntityByCombatId(result.AttackerId);
                CharacterTestCharacterEntity target = lookup.FindEntityByCombatId(result.TargetId);
                if (attacker != null && attacker.LastCombatHitResults.Count == 0)
                    attacker.SetLastCombatHitResults(results);

                DamageTelemetryRow telemetry = ResolveTelemetryForResult(
                    telemetryRows,
                    result,
                    attacker,
                    target,
                    i,
                    diagnosticRecorder,
                    frame);
                damagePayloadsByHit.TryGetValue(
                    new CharacterTestResolvedHitKey(result),
                    out DamageProfileResolveResult damagePayload);
                if (target != null)
                {
                    if (target.LastCombatHitResults.Count == 0)
                        target.SetLastCombatHitResults(results);
                    if (result.IsAcceptedDamage)
                    {
                        CharacterTestBattleStateSystem.RefreshEntity(world, target, frame, null);
                        if (!target.BattleState.CanReceiveHpDamage)
                        {
                            TraceHitReactionSkippedAfterDeath(
                                diagnosticRecorder,
                                frame,
                                target,
                                result);
                        }
                        else
                        {
                            CharacterTestHitReactionResult reaction =
                                CharacterTestHitReactionResolver.Resolve(
                                    result,
                                    damagePayload,
                                    target.CombatState);
                            target.ApplyCombatHitReaction(
                                result,
                                telemetry.FinalDamage,
                                reaction.ReactionFrames,
                                frame.Value,
                                attacker != null ? attacker.Id.Value : 0,
                                telemetry.HpAfter,
                                target.CombatState.MaxHealth);
                        }
                    }
                }

                if (traceRecorder != null)
                {
                    traceRecorder.AttackCombatHit(
                        frame,
                        attacker != null ? attacker.Id.Value : 0,
                        new CharacterTestCombatHitTraceEvent(
                            target != null ? target.Id.Value : result.TargetId.Value,
                            result.Kind,
                            damagePayload.Usage.StableEventId,
                            telemetry.FinalDamage,
                            target != null,
                            target != null ? target.CombatState.Health : 0,
                            target != null ? target.CombatState.MaxHealth : 0,
                            target != null ? target.CombatState.ScatterPressure : 0,
                            target != null ? target.CombatState.MaxScatterPressure : 0,
                            target != null ? target.CombatState.ScatterPressureBand : default,
                            target != null ? target.CombatState.HitStunFramesRemaining : 0,
                            result.HitPointWorld,
                            result.HitNormalWorld,
                            result.TraceId));
                }
            }

            if (traceRecorder != null && (activeTraceCount > 0 || candidateCount > 0))
            {
                traceRecorder.AttackCombatHitQuery(
                    frame,
                    new CharacterTestCombatHitQueryTraceEvent(activeTraceCount, candidateCount, results.Count));
            }
        }

        private static DamageTelemetryRow ResolveTelemetryForResult(
            IReadOnlyList<DamageTelemetryRow> telemetryRows,
            HitResolveResult result,
            CharacterTestCharacterEntity attacker,
            CharacterTestCharacterEntity target,
            int resultIndex,
            CharacterTestTraceRecorder traceRecorder,
            RuntimeFrame frame)
        {
            if (telemetryRows != null
                && resultIndex >= 0
                && resultIndex < telemetryRows.Count
                && IsTelemetryAligned(telemetryRows[resultIndex], result, attacker, target))
            {
                return telemetryRows[resultIndex];
            }

            for (int i = 0; telemetryRows != null && i < telemetryRows.Count; i++)
            {
                if (i == resultIndex)
                    continue;

                DamageTelemetryRow candidate = telemetryRows[i];
                if (IsTelemetryAligned(candidate, result, attacker, target))
                    return candidate;
            }

            if (traceRecorder != null && result.IsAcceptedDamage)
            {
                traceRecorder.AttackCombat(
                    frame,
                    target != null ? target.Id.Value : 0,
                    "hit telemetry mismatch target="
                    + (target != null ? target.Id.Value : result.TargetId.Value)
                    + " source="
                    + (attacker != null ? attacker.Id.Value : result.AttackerId.Value)
                    + " trace=" + result.TraceId
                    + " resultKind=" + result.Kind
                    + " telemetryRows=" + (telemetryRows != null ? telemetryRows.Count : 0),
                    warning: true);
            }

            return default;
        }

        private static bool IsTelemetryAligned(
            DamageTelemetryRow telemetry,
            HitResolveResult result,
            CharacterTestCharacterEntity attacker,
            CharacterTestCharacterEntity target)
        {
            if (telemetry.TargetEntityId <= 0
                && telemetry.SourceEntityId <= 0
                && telemetry.FinalDamage <= 0
                && telemetry.HpBefore <= 0
                && telemetry.HpAfter <= 0)
            {
                return false;
            }

            if (telemetry.ResultKind != result.Kind)
                return false;

            int targetEntityId = target != null ? target.Id.Value : 0;
            if (telemetry.TargetEntityId > 0
                && telemetry.TargetEntityId != targetEntityId
                && telemetry.TargetEntityId != result.TargetId.Value)
            {
                return false;
            }

            int sourceEntityId = attacker != null
                ? attacker.Id.Value
                : CharacterTestCombatEntityLookup.ResolveCharacterEntityId(result.AttackerId);
            if (telemetry.SourceEntityId > 0
                && telemetry.SourceEntityId != sourceEntityId
                && telemetry.SourceEntityId != result.AttackerId.Value)
            {
                return false;
            }

            return true;
        }

        private static void TraceHitReactionSkippedAfterDeath(
            CharacterTestTraceRecorder traceRecorder,
            RuntimeFrame frame,
            CharacterTestCharacterEntity target,
            HitResolveResult result)
        {
            if (traceRecorder == null || target == null)
                return;

            traceRecorder.AttackCombat(
                frame,
                target.Id.Value,
                "hit reaction skipped reason=TargetNotAliveAfterDamage target=" + target.Id.Value
                + " lifeState=" + target.BattleState.LifeState
                + " dominantReason=" + target.BattleState.DominantReason
                + " hp=" + target.CombatState.Health
                + " trace=" + result.TraceId);
        }
    }
}



using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Shields;
using MxFramework.Combat.Projectiles;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiLabFrameSyncProbe
    {
        private static void ComparePoisonBottlePool(
            CharacterTestRuntimeAiLabFrameSyncPoisonBottlePoolSample expected,
            CharacterTestRuntimeAiLabFrameSyncPoisonBottlePoolSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            bool includeTransientFrameState)
        {
            AddMismatch(rows, "poisonPool", 0, "runtimeAvailable", expected.RuntimeAvailable, actual.RuntimeAvailable);
            AddMismatch(rows, "poisonPool", 0, "schemaVersion", expected.SchemaVersion, actual.SchemaVersion);
            AddMismatch(rows, "poisonPool", 0, "moduleId", expected.ModuleId, actual.ModuleId);
            AddMismatch(rows, "poisonPool", 0, "nextRuntimeSourceId", expected.NextRuntimeSourceId, actual.NextRuntimeSourceId);
            AddMismatch(rows, "poisonPool", 0, "activeSourceCount", expected.ActiveSourceCount, actual.ActiveSourceCount);
            if (includeTransientFrameState)
            {
                AddMismatch(rows, "poisonPool", 0, "lastTick.activePoolCount", expected.LastTickActivePoolCount, actual.LastTickActivePoolCount);
                AddMismatch(rows, "poisonPool", 0, "lastTick.createdPoolCount", expected.LastTickCreatedPoolCount, actual.LastTickCreatedPoolCount);
                AddMismatch(rows, "poisonPool", 0, "lastTick.expiredPoolCount", expected.LastTickExpiredPoolCount, actual.LastTickExpiredPoolCount);
                AddMismatch(rows, "poisonPool", 0, "lastTick.queryCount", expected.LastTickQueryCount, actual.LastTickQueryCount);
                AddMismatch(rows, "poisonPool", 0, "lastTick.queryHitCount", expected.LastTickQueryHitCount, actual.LastTickQueryHitCount);
                AddMismatch(rows, "poisonPool", 0, "lastTick.appliedOrRefreshedCount", expected.LastTickAppliedOrRefreshedCount, actual.LastTickAppliedOrRefreshedCount);
                AddMismatch(rows, "poisonPool", 0, "lastTick.rejectedTargetCount", expected.LastTickRejectedTargetCount, actual.LastTickRejectedTargetCount);
                AddMismatch(rows, "poisonPool", 0, "lastTick.skippedMissingGameplayTargetCount", expected.LastTickSkippedMissingGameplayTargetCount, actual.LastTickSkippedMissingGameplayTargetCount);
                AddMismatch(rows, "poisonPool", 0, "lastPoisonTickTargetCount", expected.LastPoisonTickTargetCount, actual.LastPoisonTickTargetCount);
            }

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample> expectedSources =
                expected.ActiveSources;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample> actualSources =
                actual.ActiveSources;
            int expectedCount = expectedSources != null ? expectedSources.Count : 0;
            int actualCount = actualSources != null ? actualSources.Count : 0;
            AddMismatch(rows, "poisonPool", 0, "source.count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample left = expectedSources[i];
                if (!TryFindMechanicSource(
                    actualSources,
                    left.RuntimeSourceId,
                    out CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "poisonPool",
                        left.RuntimeSourceId,
                        "source.missing",
                        "present",
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "poisonPool", left.RuntimeSourceId, "source.definitionId", left.SourceDefinitionId, right.SourceDefinitionId);
                AddMismatch(rows, "poisonPool", left.RuntimeSourceId, "source.gameplayEntityIndex", left.SourceGameplayEntityIndex, right.SourceGameplayEntityIndex);
                AddMismatch(rows, "poisonPool", left.RuntimeSourceId, "source.gameplayEntityGeneration", left.SourceGameplayEntityGeneration, right.SourceGameplayEntityGeneration);
                AddMismatch(rows, "poisonPool", left.RuntimeSourceId, "source.combatEntityId", left.SourceCombatEntityId, right.SourceCombatEntityId);
                AddMismatch(rows, "poisonPool", left.RuntimeSourceId, "source.centerXRaw", left.CenterXRaw, right.CenterXRaw);
                AddMismatch(rows, "poisonPool", left.RuntimeSourceId, "source.centerYRaw", left.CenterYRaw, right.CenterYRaw);
                AddMismatch(rows, "poisonPool", left.RuntimeSourceId, "source.centerZRaw", left.CenterZRaw, right.CenterZRaw);
                AddMismatch(rows, "poisonPool", left.RuntimeSourceId, "source.startFrame", left.StartFrame, right.StartFrame);
                AddMismatch(rows, "poisonPool", left.RuntimeSourceId, "source.endFrame", left.EndFrame, right.EndFrame);
                AddMismatch(rows, "poisonPool", left.RuntimeSourceId, "source.traceId", left.TraceId, right.TraceId);
            }
        }

        private static bool TryFindMechanicSource(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample> samples,
            int runtimeSourceId,
            out CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.RuntimeSourceId == runtimeSourceId)
                    return true;
            }

            sample = default;
            return false;
        }

        private static void ComparePhysicalShield(
            CharacterTestRuntimeAiLabFrameSyncPhysicalShieldSample expected,
            CharacterTestRuntimeAiLabFrameSyncPhysicalShieldSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            bool includeTransientFrameState)
        {
            AddMismatch(rows, "physicalShield", 0, "runtimeAvailable", expected.RuntimeAvailable, actual.RuntimeAvailable);
            AddMismatch(rows, "physicalShield", 0, "schemaVersion", expected.SchemaVersion, actual.SchemaVersion);
            AddMismatch(rows, "physicalShield", 0, "moduleId", expected.ModuleId, actual.ModuleId);
            AddMismatch(rows, "physicalShield", 0, "activeShieldCount", expected.ActiveShieldCount, actual.ActiveShieldCount);
            AddMismatch(rows, "physicalShield", 0, "shieldRuntimeAvailable", expected.ShieldRuntimeAvailable, actual.ShieldRuntimeAvailable);
            AddMismatch(rows, "physicalShield", 0, "shieldRuntimeSchemaVersion", expected.ShieldRuntimeSchemaVersion, actual.ShieldRuntimeSchemaVersion);
            AddMismatch(rows, "physicalShield", 0, "shieldRuntimeModuleId", expected.ShieldRuntimeModuleId, actual.ShieldRuntimeModuleId);
            AddMismatch(rows, "physicalShield", 0, "shieldCount", expected.ShieldCount, actual.ShieldCount);
            if (includeTransientFrameState)
            {
                AddMismatch(rows, "physicalShield", 0, "lastTick.activeCount", expected.LastTickActiveCount, actual.LastTickActiveCount);
                AddMismatch(rows, "physicalShield", 0, "lastTick.createdOrRefreshedCount", expected.LastTickCreatedOrRefreshedCount, actual.LastTickCreatedOrRefreshedCount);
                AddMismatch(rows, "physicalShield", 0, "lastTick.expiredCount", expected.LastTickExpiredCount, actual.LastTickExpiredCount);
            }

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample> expectedActive =
                expected.ActiveShields;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample> actualActive =
                actual.ActiveShields;
            int activeCount = expectedActive != null ? expectedActive.Count : 0;
            for (int i = 0; i < activeCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample left = expectedActive[i];
                if (!TryFindPhysicalShieldActive(
                    actualActive,
                    left.ShieldEntityId,
                    out CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "physicalShield",
                        left.ShieldEntityId,
                        "active.missing",
                        "present",
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "active.ownerId", left.OwnerId, right.OwnerId);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "active.bodyId", left.BodyId, right.BodyId);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "active.centerXRaw", left.CenterXRaw, right.CenterXRaw);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "active.centerYRaw", left.CenterYRaw, right.CenterYRaw);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "active.centerZRaw", left.CenterZRaw, right.CenterZRaw);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "active.endFrame", left.EndFrame, right.EndFrame);
            }

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncShieldPotencySample> expectedShields =
                expected.Shields;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncShieldPotencySample> actualShields =
                actual.Shields;
            int shieldCount = expectedShields != null ? expectedShields.Count : 0;
            for (int i = 0; i < shieldCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncShieldPotencySample left = expectedShields[i];
                if (!TryFindShieldPotency(
                    actualShields,
                    left.ShieldEntityId,
                    out CharacterTestRuntimeAiLabFrameSyncShieldPotencySample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "physicalShield",
                        left.ShieldEntityId,
                        "shield.missing",
                        "present",
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "shield.ownerEntityId", left.OwnerEntityId, right.OwnerEntityId);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "shield.bodyId", left.BodyId, right.BodyId);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "shield.colliderId", left.ColliderId, right.ColliderId);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "shield.currentMotion", left.CurrentMotion, right.CurrentMotion);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "shield.currentCohesion", left.CurrentCohesion, right.CurrentCohesion);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "shield.currentScatter", left.CurrentScatter, right.CurrentScatter);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "shield.currentCohesionLimit", left.CurrentCohesionLimit, right.CurrentCohesionLimit);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "shield.lastImpactFrame", left.LastImpactFrame, right.LastImpactFrame);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "shield.currentBand", left.CurrentBand, right.CurrentBand);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "shield.isBroken", left.IsBroken, right.IsBroken);
                AddMismatch(rows, "physicalShield", left.ShieldEntityId, "shield.pressure", left.ShieldPressure, right.ShieldPressure);
            }
        }

        private static bool TryFindPhysicalShieldActive(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample> samples,
            int shieldEntityId,
            out CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.ShieldEntityId == shieldEntityId)
                    return true;
            }

            sample = default;
            return false;
        }

        private static bool TryFindShieldPotency(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncShieldPotencySample> samples,
            int shieldEntityId,
            out CharacterTestRuntimeAiLabFrameSyncShieldPotencySample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.ShieldEntityId == shieldEntityId)
                    return true;
            }

            sample = default;
            return false;
        }
    }
}
namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncPoisonBottlePoolSample
    {
        public CharacterTestRuntimeAiLabFrameSyncPoisonBottlePoolSample(
            bool runtimeAvailable,
            int schemaVersion,
            int moduleId,
            int nextRuntimeSourceId,
            CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample[] activeSources,
            int lastTickActivePoolCount,
            int lastTickCreatedPoolCount,
            int lastTickExpiredPoolCount,
            int lastTickQueryCount,
            int lastTickQueryHitCount,
            int lastTickAppliedOrRefreshedCount,
            int lastTickRejectedTargetCount,
            int lastTickSkippedMissingGameplayTargetCount,
            int lastPoisonTickTargetCount)
        {
            RuntimeAvailable = runtimeAvailable;
            SchemaVersion = schemaVersion;
            ModuleId = moduleId;
            NextRuntimeSourceId = nextRuntimeSourceId;
            ActiveSources = activeSources ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample>();
            LastTickActivePoolCount = lastTickActivePoolCount;
            LastTickCreatedPoolCount = lastTickCreatedPoolCount;
            LastTickExpiredPoolCount = lastTickExpiredPoolCount;
            LastTickQueryCount = lastTickQueryCount;
            LastTickQueryHitCount = lastTickQueryHitCount;
            LastTickAppliedOrRefreshedCount = lastTickAppliedOrRefreshedCount;
            LastTickRejectedTargetCount = lastTickRejectedTargetCount;
            LastTickSkippedMissingGameplayTargetCount = lastTickSkippedMissingGameplayTargetCount;
            LastPoisonTickTargetCount = lastPoisonTickTargetCount;
        }

        public bool RuntimeAvailable { get; }
        public int SchemaVersion { get; }
        public int ModuleId { get; }
        public int NextRuntimeSourceId { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample> ActiveSources { get; }
        public int ActiveSourceCount => ActiveSources != null ? ActiveSources.Count : 0;
        public int LastTickActivePoolCount { get; }
        public int LastTickCreatedPoolCount { get; }
        public int LastTickExpiredPoolCount { get; }
        public int LastTickQueryCount { get; }
        public int LastTickQueryHitCount { get; }
        public int LastTickAppliedOrRefreshedCount { get; }
        public int LastTickRejectedTargetCount { get; }
        public int LastTickSkippedMissingGameplayTargetCount { get; }
        public int LastPoisonTickTargetCount { get; }

        public static CharacterTestRuntimeAiLabFrameSyncPoisonBottlePoolSample Capture(
            CharacterTestMechanicSourceRuntimeState state,
            CharacterTestPoisonBottlePoolTickSummary lastTickSummary,
            int lastPoisonTickTargetCount)
        {
            return new CharacterTestRuntimeAiLabFrameSyncPoisonBottlePoolSample(
                state != null,
                state != null ? state.SchemaVersion : 0,
                state != null ? state.ModuleId : 0,
                state != null ? state.NextRuntimeSourceId : 0,
                CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample.Capture(state),
                lastTickSummary.ActivePoolCount,
                lastTickSummary.CreatedPoolCount,
                lastTickSummary.ExpiredPoolCount,
                lastTickSummary.QueryCount,
                lastTickSummary.QueryHitCount,
                lastTickSummary.AppliedOrRefreshedCount,
                lastTickSummary.RejectedTargetCount,
                lastTickSummary.SkippedMissingGameplayTargetCount,
                lastPoisonTickTargetCount);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample
    {
        public CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample(
            int runtimeSourceId,
            int sourceDefinitionId,
            int sourceGameplayEntityIndex,
            int sourceGameplayEntityGeneration,
            int sourceCombatEntityId,
            long centerXRaw,
            long centerYRaw,
            long centerZRaw,
            long startFrame,
            long endFrame,
            int traceId)
        {
            RuntimeSourceId = runtimeSourceId;
            SourceDefinitionId = sourceDefinitionId;
            SourceGameplayEntityIndex = sourceGameplayEntityIndex;
            SourceGameplayEntityGeneration = sourceGameplayEntityGeneration;
            SourceCombatEntityId = sourceCombatEntityId;
            CenterXRaw = centerXRaw;
            CenterYRaw = centerYRaw;
            CenterZRaw = centerZRaw;
            StartFrame = startFrame;
            EndFrame = endFrame;
            TraceId = traceId;
        }

        public int RuntimeSourceId { get; }
        public int SourceDefinitionId { get; }
        public int SourceGameplayEntityIndex { get; }
        public int SourceGameplayEntityGeneration { get; }
        public int SourceCombatEntityId { get; }
        public long CenterXRaw { get; }
        public long CenterYRaw { get; }
        public long CenterZRaw { get; }
        public long StartFrame { get; }
        public long EndFrame { get; }
        public int TraceId { get; }

        public static CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample[] Capture(
            CharacterTestMechanicSourceRuntimeState state)
        {
            IReadOnlyList<CharacterTestMechanicSourceSnapshot> sources =
                state != null ? state.ActiveSources : null;
            int count = sources != null ? sources.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample>();

            var result = new CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestMechanicSourceSnapshot source = sources[i];
                result[i] = new CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample(
                    source.RuntimeSourceId,
                    source.SourceDefinitionId,
                    source.SourceGameplayEntityIndex,
                    source.SourceGameplayEntityGeneration,
                    source.SourceCombatEntityId,
                    source.CenterXRaw,
                    source.CenterYRaw,
                    source.CenterZRaw,
                    source.StartFrame,
                    source.EndFrame,
                    source.TraceId);
            }

            return result;
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncPhysicalShieldSample
    {
        public CharacterTestRuntimeAiLabFrameSyncPhysicalShieldSample(
            bool runtimeAvailable,
            int schemaVersion,
            int moduleId,
            CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample[] activeShields,
            bool shieldRuntimeAvailable,
            int shieldRuntimeSchemaVersion,
            int shieldRuntimeModuleId,
            CharacterTestRuntimeAiLabFrameSyncShieldPotencySample[] shields,
            int lastTickActiveCount,
            int lastTickCreatedOrRefreshedCount,
            int lastTickExpiredCount)
        {
            RuntimeAvailable = runtimeAvailable;
            SchemaVersion = schemaVersion;
            ModuleId = moduleId;
            ActiveShields = activeShields ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample>();
            ShieldRuntimeAvailable = shieldRuntimeAvailable;
            ShieldRuntimeSchemaVersion = shieldRuntimeSchemaVersion;
            ShieldRuntimeModuleId = shieldRuntimeModuleId;
            Shields = shields ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncShieldPotencySample>();
            LastTickActiveCount = lastTickActiveCount;
            LastTickCreatedOrRefreshedCount = lastTickCreatedOrRefreshedCount;
            LastTickExpiredCount = lastTickExpiredCount;
        }

        public bool RuntimeAvailable { get; }
        public int SchemaVersion { get; }
        public int ModuleId { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample> ActiveShields { get; }
        public int ActiveShieldCount => ActiveShields != null ? ActiveShields.Count : 0;
        public bool ShieldRuntimeAvailable { get; }
        public int ShieldRuntimeSchemaVersion { get; }
        public int ShieldRuntimeModuleId { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncShieldPotencySample> Shields { get; }
        public int ShieldCount => Shields != null ? Shields.Count : 0;
        public int LastTickActiveCount { get; }
        public int LastTickCreatedOrRefreshedCount { get; }
        public int LastTickExpiredCount { get; }

        public static CharacterTestRuntimeAiLabFrameSyncPhysicalShieldSample Capture(
            CharacterTestPhysicalShieldRuntimeState state,
            CharacterTestPhysicalShieldTickSummary lastTickSummary)
        {
            CombatShieldRuntimeState shieldRuntime = state != null ? state.ShieldRuntime : null;
            return new CharacterTestRuntimeAiLabFrameSyncPhysicalShieldSample(
                state != null,
                state != null ? state.SchemaVersion : 0,
                state != null ? state.ModuleId : 0,
                CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample.Capture(state),
                shieldRuntime != null,
                shieldRuntime != null ? shieldRuntime.SchemaVersion : 0,
                shieldRuntime != null ? shieldRuntime.ModuleIdentity : 0,
                CharacterTestRuntimeAiLabFrameSyncShieldPotencySample.Capture(shieldRuntime),
                lastTickSummary.ActiveCount,
                lastTickSummary.CreatedOrRefreshedCount,
                lastTickSummary.ExpiredCount);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample
    {
        public CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample(
            int ownerId,
            int shieldEntityId,
            int bodyId,
            long centerXRaw,
            long centerYRaw,
            long centerZRaw,
            long endFrame)
        {
            OwnerId = ownerId;
            ShieldEntityId = shieldEntityId;
            BodyId = bodyId;
            CenterXRaw = centerXRaw;
            CenterYRaw = centerYRaw;
            CenterZRaw = centerZRaw;
            EndFrame = endFrame;
        }

        public int OwnerId { get; }
        public int ShieldEntityId { get; }
        public int BodyId { get; }
        public long CenterXRaw { get; }
        public long CenterYRaw { get; }
        public long CenterZRaw { get; }
        public long EndFrame { get; }

        public static CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample[] Capture(
            CharacterTestPhysicalShieldRuntimeState state)
        {
            IReadOnlyList<CharacterTestPhysicalShieldActiveSnapshot> shields =
                state != null ? state.ActiveShields : null;
            int count = shields != null ? shields.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample>();

            var result = new CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestPhysicalShieldActiveSnapshot shield = shields[i];
                result[i] = new CharacterTestRuntimeAiLabFrameSyncPhysicalShieldActiveSample(
                    shield.OwnerId,
                    shield.ShieldEntityId,
                    shield.BodyId,
                    shield.CenterXRaw,
                    shield.CenterYRaw,
                    shield.CenterZRaw,
                    shield.EndFrame);
            }

            return result;
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncShieldPotencySample
    {
        public CharacterTestRuntimeAiLabFrameSyncShieldPotencySample(
            int shieldEntityId,
            int ownerEntityId,
            int bodyId,
            int colliderId,
            int baseMotion,
            int baseCohesion,
            int baseScatter,
            int currentMotion,
            int currentCohesion,
            int currentScatter,
            int baseCohesionLimit,
            int currentCohesionLimit,
            int recoveryRate,
            int recoveryDelayFrames,
            long lastImpactFrame,
            int currentBand,
            bool isBroken,
            int shieldPressure)
        {
            ShieldEntityId = shieldEntityId;
            OwnerEntityId = ownerEntityId;
            BodyId = bodyId;
            ColliderId = colliderId;
            BaseMotion = baseMotion;
            BaseCohesion = baseCohesion;
            BaseScatter = baseScatter;
            CurrentMotion = currentMotion;
            CurrentCohesion = currentCohesion;
            CurrentScatter = currentScatter;
            BaseCohesionLimit = baseCohesionLimit;
            CurrentCohesionLimit = currentCohesionLimit;
            RecoveryRate = recoveryRate;
            RecoveryDelayFrames = recoveryDelayFrames;
            LastImpactFrame = lastImpactFrame;
            CurrentBand = currentBand;
            IsBroken = isBroken;
            ShieldPressure = shieldPressure;
        }

        public int ShieldEntityId { get; }
        public int OwnerEntityId { get; }
        public int BodyId { get; }
        public int ColliderId { get; }
        public int BaseMotion { get; }
        public int BaseCohesion { get; }
        public int BaseScatter { get; }
        public int CurrentMotion { get; }
        public int CurrentCohesion { get; }
        public int CurrentScatter { get; }
        public int BaseCohesionLimit { get; }
        public int CurrentCohesionLimit { get; }
        public int RecoveryRate { get; }
        public int RecoveryDelayFrames { get; }
        public long LastImpactFrame { get; }
        public int CurrentBand { get; }
        public bool IsBroken { get; }
        public int ShieldPressure { get; }

        public static CharacterTestRuntimeAiLabFrameSyncShieldPotencySample[] Capture(
            CombatShieldRuntimeState state)
        {
            IReadOnlyList<CombatShieldPotencyComponent> shields = state != null ? state.Shields : null;
            int count = shields != null ? shields.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestRuntimeAiLabFrameSyncShieldPotencySample>();

            var result = new CharacterTestRuntimeAiLabFrameSyncShieldPotencySample[count];
            for (int i = 0; i < count; i++)
            {
                CombatShieldPotencyComponent shield = shields[i];
                result[i] = new CharacterTestRuntimeAiLabFrameSyncShieldPotencySample(
                    shield.ShieldEntityId.Value,
                    shield.OwnerEntityId.Value,
                    shield.BodyId.Value,
                    shield.ColliderId.Value,
                    shield.BasePotency.Motion,
                    shield.BasePotency.Cohesion,
                    shield.BasePotency.Scatter,
                    shield.CurrentPotency.Motion,
                    shield.CurrentPotency.Cohesion,
                    shield.CurrentPotency.Scatter,
                    shield.BaseCohesionLimit,
                    shield.CurrentCohesionLimit,
                    shield.RecoveryRate,
                    shield.RecoveryDelayFrames,
                    shield.LastImpactFrame,
                    (int)shield.CurrentBand,
                    shield.IsBroken,
                    shield.ShieldPressure);
            }

            return result;
        }
    }

    /// <summary>
    /// Read-only character authority/action/runner sample for Runtime AI Lab diagnostics.
    /// </summary>
}

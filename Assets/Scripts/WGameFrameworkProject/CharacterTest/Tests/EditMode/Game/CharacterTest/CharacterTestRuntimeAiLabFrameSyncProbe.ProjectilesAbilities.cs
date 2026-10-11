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
        private static void CompareProjectiles(
            CharacterTestRuntimeAiLabFrameSyncSample expected,
            CharacterTestRuntimeAiLabFrameSyncSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "projectile", 0, "runtimeAvailable", expected.ProjectileRuntimeAvailable, actual.ProjectileRuntimeAvailable);
            AddMismatch(rows, "projectile", 0, "schemaVersion", expected.ProjectileSchemaVersion, actual.ProjectileSchemaVersion);
            AddMismatch(rows, "projectile", 0, "definitionHash", expected.ProjectileDefinitionHash, actual.ProjectileDefinitionHash);
            AddMismatch(rows, "projectile", 0, "nextProjectileId", expected.ProjectileNextProjectileId, actual.ProjectileNextProjectileId);

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncProjectileSample> expectedProjectiles = expected.Projectiles;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncProjectileSample> actualProjectiles = actual.Projectiles;
            int expectedCount = expectedProjectiles != null ? expectedProjectiles.Count : 0;
            int actualCount = actualProjectiles != null ? actualProjectiles.Count : 0;
            AddMismatch(rows, "projectile", 0, "activeCount", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncProjectileSample left = expectedProjectiles[i];
                if (!TryFindProjectile(actualProjectiles, left.ProjectileId, out CharacterTestRuntimeAiLabFrameSyncProjectileSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "projectile",
                        left.ProjectileId,
                        "missing",
                        "present",
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "projectile", left.ProjectileId, "spawnSequence", left.SpawnSequence, right.SpawnSequence);
                AddMismatch(rows, "projectile", left.ProjectileId, "definitionId", left.DefinitionId, right.DefinitionId);
                AddMismatch(rows, "projectile", left.ProjectileId, "definitionStableId", left.DefinitionStableId, right.DefinitionStableId);
                AddMismatch(rows, "projectile", left.ProjectileId, "sourceEntityId", left.SourceEntityId, right.SourceEntityId);
                AddMismatch(rows, "projectile", left.ProjectileId, "actionId", left.ActionId, right.ActionId);
                AddMismatch(rows, "projectile", left.ProjectileId, "actionInstanceId", left.ActionInstanceId, right.ActionInstanceId);
                AddMismatch(rows, "projectile", left.ProjectileId, "traceId", left.TraceId, right.TraceId);
                AddMismatch(rows, "projectile", left.ProjectileId, "localFrame", left.LocalFrame, right.LocalFrame);
                AddMismatch(rows, "projectile", left.ProjectileId, "spawnFrame", left.SpawnFrame, right.SpawnFrame);
                AddMismatch(rows, "projectile", left.ProjectileId, "lastFrame", left.LastFrame, right.LastFrame);
                AddMismatch(rows, "projectile", left.ProjectileId, "ageFrames", left.AgeFrames, right.AgeFrames);
                AddMismatch(rows, "projectile", left.ProjectileId, "positionXRaw", left.PositionXRaw, right.PositionXRaw);
                AddMismatch(rows, "projectile", left.ProjectileId, "positionYRaw", left.PositionYRaw, right.PositionYRaw);
                AddMismatch(rows, "projectile", left.ProjectileId, "positionZRaw", left.PositionZRaw, right.PositionZRaw);
                AddMismatch(rows, "projectile", left.ProjectileId, "directionXRaw", left.DirectionXRaw, right.DirectionXRaw);
                AddMismatch(rows, "projectile", left.ProjectileId, "directionYRaw", left.DirectionYRaw, right.DirectionYRaw);
                AddMismatch(rows, "projectile", left.ProjectileId, "directionZRaw", left.DirectionZRaw, right.DirectionZRaw);
                AddMismatch(rows, "projectile", left.ProjectileId, "originXRaw", left.OriginXRaw, right.OriginXRaw);
                AddMismatch(rows, "projectile", left.ProjectileId, "originYRaw", left.OriginYRaw, right.OriginYRaw);
                AddMismatch(rows, "projectile", left.ProjectileId, "originZRaw", left.OriginZRaw, right.OriginZRaw);
                AddMismatch(rows, "projectile", left.ProjectileId, "hasTargetPoint", left.HasTargetPoint, right.HasTargetPoint);
                AddMismatch(rows, "projectile", left.ProjectileId, "targetPointXRaw", left.TargetPointXRaw, right.TargetPointXRaw);
                AddMismatch(rows, "projectile", left.ProjectileId, "targetPointYRaw", left.TargetPointYRaw, right.TargetPointYRaw);
                AddMismatch(rows, "projectile", left.ProjectileId, "targetPointZRaw", left.TargetPointZRaw, right.TargetPointZRaw);
                AddMismatch(rows, "projectile", left.ProjectileId, "effectiveLifetimeFrames", left.EffectiveLifetimeFrames, right.EffectiveLifetimeFrames);
                AddMismatch(rows, "projectile", left.ProjectileId, "launchSpeedPerFrameRaw", left.LaunchSpeedPerFrameRaw, right.LaunchSpeedPerFrameRaw);
                AddMismatch(rows, "projectile", left.ProjectileId, "hasTrackingTarget", left.HasTrackingTarget, right.HasTrackingTarget);
                AddMismatch(rows, "projectile", left.ProjectileId, "trackingTargetEntityId", left.TrackingTargetEntityId, right.TrackingTargetEntityId);
                AddMismatch(rows, "projectile", left.ProjectileId, "acceptedImpactCount", left.AcceptedImpactCount, right.AcceptedImpactCount);
                AddMismatch(rows, "projectile", left.ProjectileId, "acceptedImpactHash", left.AcceptedImpactHash, right.AcceptedImpactHash);
            }
        }

        private static bool TryFindProjectile(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncProjectileSample> samples,
            int projectileId,
            out CharacterTestRuntimeAiLabFrameSyncProjectileSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.ProjectileId == projectileId)
                    return true;
            }

            sample = default;
            return false;
        }

        private static void CompareAbility(
            CharacterTestRuntimeAiLabFrameSyncSample expected,
            CharacterTestRuntimeAiLabFrameSyncSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "ability", 0, "runtimeAvailable", expected.AbilityRuntimeAvailable, actual.AbilityRuntimeAvailable);
            AddMismatch(rows, "ability", 0, "schemaVersion", expected.AbilitySchemaVersion, actual.AbilitySchemaVersion);
            AddMismatch(rows, "ability", 0, "moduleId", expected.AbilityModuleId, actual.AbilityModuleId);
            AddMismatch(rows, "ability", 0, "definitionHash", expected.AbilityDefinitionHash, actual.AbilityDefinitionHash);

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample> expectedCooldowns =
                expected.AbilityCooldowns;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample> actualCooldowns =
                actual.AbilityCooldowns;
            int expectedCount = expectedCooldowns != null ? expectedCooldowns.Count : 0;
            int actualCount = actualCooldowns != null ? actualCooldowns.Count : 0;
            AddMismatch(rows, "ability", 0, "cooldown.count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample left = expectedCooldowns[i];
                if (!TryFindAbilityCooldown(
                    actualCooldowns,
                    left.CasterEntityId,
                    left.AbilityId,
                    out CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "ability",
                        left.CasterEntityId,
                        "cooldown.missing",
                        left.AbilityId.ToString(CultureInfo.InvariantCulture),
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "ability", left.CasterEntityId, "cooldown.endFrame", left.EndFrame, right.EndFrame);
            }
        }

        private static bool TryFindAbilityCooldown(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample> samples,
            int casterEntityId,
            int abilityId,
            out CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.CasterEntityId == casterEntityId && sample.AbilityId == abilityId)
                    return true;
            }

            sample = default;
            return false;
        }

        private static void CompareGuardAura(
            CharacterTestRuntimeAiLabFrameSyncGuardAuraSample expected,
            CharacterTestRuntimeAiLabFrameSyncGuardAuraSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "guardAura", 0, "lastTick.sourceCount", expected.SourceCount, actual.SourceCount);
            AddMismatch(rows, "guardAura", 0, "lastTick.queryCount", expected.QueryCount, actual.QueryCount);
            AddMismatch(rows, "guardAura", 0, "lastTick.appliedOrRefreshedCount", expected.AppliedOrRefreshedCount, actual.AppliedOrRefreshedCount);
            AddMismatch(rows, "guardAura", 0, "lastTick.rejectedTargetCount", expected.RejectedTargetCount, actual.RejectedTargetCount);
            AddMismatch(rows, "guardAura", 0, "lastTick.skippedMissingDefinitionCount", expected.SkippedMissingDefinitionCount, actual.SkippedMissingDefinitionCount);
            AddMismatch(rows, "guardAura", 0, "lastTick.skippedMissingCombatMapCount", expected.SkippedMissingCombatMapCount, actual.SkippedMissingCombatMapCount);
            AddMismatch(rows, "guardAura", 0, "lastTick.skippedMissingAnchorCount", expected.SkippedMissingAnchorCount, actual.SkippedMissingAnchorCount);
            AddMismatch(rows, "guardAura", 0, "lastTick.skippedMissingGameplayTargetCount", expected.SkippedMissingGameplayTargetCount, actual.SkippedMissingGameplayTargetCount);
            AddMismatch(rows, "guardAura", 0, "onHit.resultCount", expected.OnHitResultCount, actual.OnHitResultCount);
            AddMismatch(rows, "guardAura", 0, "onHit.acceptedDamageCount", expected.OnHitAcceptedDamageCount, actual.OnHitAcceptedDamageCount);
            AddMismatch(rows, "guardAura", 0, "onHit.requestedBuffCount", expected.OnHitRequestedBuffCount, actual.OnHitRequestedBuffCount);
            AddMismatch(rows, "guardAura", 0, "onHit.skippedResultCount", expected.OnHitSkippedResultCount, actual.OnHitSkippedResultCount);
            AddMismatch(rows, "guardAura", 0, "onHit.skippedMissingGameplayTargetCount", expected.OnHitSkippedMissingGameplayTargetCount, actual.OnHitSkippedMissingGameplayTargetCount);
        }
    }
}
namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncGuardAuraSample
    {
        public CharacterTestRuntimeAiLabFrameSyncGuardAuraSample(
            int sourceCount,
            int queryCount,
            int appliedOrRefreshedCount,
            int rejectedTargetCount,
            int skippedMissingDefinitionCount,
            int skippedMissingCombatMapCount,
            int skippedMissingAnchorCount,
            int skippedMissingGameplayTargetCount,
            int onHitResultCount,
            int onHitAcceptedDamageCount,
            int onHitRequestedBuffCount,
            int onHitSkippedResultCount,
            int onHitSkippedMissingGameplayTargetCount)
        {
            SourceCount = sourceCount;
            QueryCount = queryCount;
            AppliedOrRefreshedCount = appliedOrRefreshedCount;
            RejectedTargetCount = rejectedTargetCount;
            SkippedMissingDefinitionCount = skippedMissingDefinitionCount;
            SkippedMissingCombatMapCount = skippedMissingCombatMapCount;
            SkippedMissingAnchorCount = skippedMissingAnchorCount;
            SkippedMissingGameplayTargetCount = skippedMissingGameplayTargetCount;
            OnHitResultCount = onHitResultCount;
            OnHitAcceptedDamageCount = onHitAcceptedDamageCount;
            OnHitRequestedBuffCount = onHitRequestedBuffCount;
            OnHitSkippedResultCount = onHitSkippedResultCount;
            OnHitSkippedMissingGameplayTargetCount = onHitSkippedMissingGameplayTargetCount;
        }

        public int SourceCount { get; }
        public int QueryCount { get; }
        public int AppliedOrRefreshedCount { get; }
        public int RejectedTargetCount { get; }
        public int SkippedMissingDefinitionCount { get; }
        public int SkippedMissingCombatMapCount { get; }
        public int SkippedMissingAnchorCount { get; }
        public int SkippedMissingGameplayTargetCount { get; }
        public int OnHitResultCount { get; }
        public int OnHitAcceptedDamageCount { get; }
        public int OnHitRequestedBuffCount { get; }
        public int OnHitSkippedResultCount { get; }
        public int OnHitSkippedMissingGameplayTargetCount { get; }

        public static CharacterTestRuntimeAiLabFrameSyncGuardAuraSample Capture(
            CombatGameplayAuraBuffTickSummary guardAuraSummary,
            CombatOnHitBuffApplicationSummary onHitSummary)
        {
            return new CharacterTestRuntimeAiLabFrameSyncGuardAuraSample(
                guardAuraSummary.SourceCount,
                guardAuraSummary.QueryCount,
                guardAuraSummary.AppliedOrRefreshedCount,
                guardAuraSummary.RejectedTargetCount,
                guardAuraSummary.SkippedMissingDefinitionCount,
                guardAuraSummary.SkippedMissingCombatMapCount,
                guardAuraSummary.SkippedMissingAnchorCount,
                guardAuraSummary.SkippedMissingGameplayTargetCount,
                onHitSummary.ResultCount,
                onHitSummary.AcceptedDamageCount,
                onHitSummary.RequestedBuffCount,
                onHitSummary.SkippedResultCount,
                onHitSummary.SkippedMissingGameplayTargetCount);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncProjectileSample
    {
        public CharacterTestRuntimeAiLabFrameSyncProjectileSample(
            int projectileId,
            int spawnSequence,
            int definitionId,
            string definitionStableId,
            int sourceEntityId,
            int actionId,
            int actionInstanceId,
            int traceId,
            int localFrame,
            int spawnFrame,
            int lastFrame,
            int ageFrames,
            long positionXRaw,
            long positionYRaw,
            long positionZRaw,
            long directionXRaw,
            long directionYRaw,
            long directionZRaw,
            long originXRaw,
            long originYRaw,
            long originZRaw,
            bool hasTargetPoint,
            long targetPointXRaw,
            long targetPointYRaw,
            long targetPointZRaw,
            int effectiveLifetimeFrames,
            long launchSpeedPerFrameRaw,
            bool hasTrackingTarget,
            int trackingTargetEntityId,
            int acceptedImpactCount,
            long acceptedImpactHash)
        {
            ProjectileId = projectileId;
            SpawnSequence = spawnSequence;
            DefinitionId = definitionId;
            DefinitionStableId = definitionStableId ?? string.Empty;
            SourceEntityId = sourceEntityId;
            ActionId = actionId;
            ActionInstanceId = actionInstanceId;
            TraceId = traceId;
            LocalFrame = localFrame;
            SpawnFrame = spawnFrame;
            LastFrame = lastFrame;
            AgeFrames = ageFrames;
            PositionXRaw = positionXRaw;
            PositionYRaw = positionYRaw;
            PositionZRaw = positionZRaw;
            DirectionXRaw = directionXRaw;
            DirectionYRaw = directionYRaw;
            DirectionZRaw = directionZRaw;
            OriginXRaw = originXRaw;
            OriginYRaw = originYRaw;
            OriginZRaw = originZRaw;
            HasTargetPoint = hasTargetPoint;
            TargetPointXRaw = targetPointXRaw;
            TargetPointYRaw = targetPointYRaw;
            TargetPointZRaw = targetPointZRaw;
            EffectiveLifetimeFrames = effectiveLifetimeFrames;
            LaunchSpeedPerFrameRaw = launchSpeedPerFrameRaw;
            HasTrackingTarget = hasTrackingTarget;
            TrackingTargetEntityId = trackingTargetEntityId;
            AcceptedImpactCount = acceptedImpactCount;
            AcceptedImpactHash = acceptedImpactHash;
        }

        public int ProjectileId { get; }
        public int SpawnSequence { get; }
        public int DefinitionId { get; }
        public string DefinitionStableId { get; }
        public int SourceEntityId { get; }
        public int ActionId { get; }
        public int ActionInstanceId { get; }
        public int TraceId { get; }
        public int LocalFrame { get; }
        public int SpawnFrame { get; }
        public int LastFrame { get; }
        public int AgeFrames { get; }
        public long PositionXRaw { get; }
        public long PositionYRaw { get; }
        public long PositionZRaw { get; }
        public long DirectionXRaw { get; }
        public long DirectionYRaw { get; }
        public long DirectionZRaw { get; }
        public long OriginXRaw { get; }
        public long OriginYRaw { get; }
        public long OriginZRaw { get; }
        public bool HasTargetPoint { get; }
        public long TargetPointXRaw { get; }
        public long TargetPointYRaw { get; }
        public long TargetPointZRaw { get; }
        public int EffectiveLifetimeFrames { get; }
        public long LaunchSpeedPerFrameRaw { get; }
        public bool HasTrackingTarget { get; }
        public int TrackingTargetEntityId { get; }
        public int AcceptedImpactCount { get; }
        public long AcceptedImpactHash { get; }

        public static CharacterTestRuntimeAiLabFrameSyncProjectileSample[] Capture(
            CombatProjectileRuntimeState state)
        {
            IReadOnlyList<CombatProjectileSnapshot> active = state != null
                ? state.ActiveProjectiles
                : null;
            int count = active != null ? active.Count : 0;
            if (count <= 0)
                return Array.Empty<CharacterTestRuntimeAiLabFrameSyncProjectileSample>();

            var samples = new CharacterTestRuntimeAiLabFrameSyncProjectileSample[count];
            for (int i = 0; i < count; i++)
                samples[i] = Capture(active[i]);
            return samples;
        }

        private static CharacterTestRuntimeAiLabFrameSyncProjectileSample Capture(
            CombatProjectileSnapshot projectile)
        {
            return new CharacterTestRuntimeAiLabFrameSyncProjectileSample(
                projectile.ProjectileId,
                projectile.SpawnSequence,
                projectile.DefinitionId,
                projectile.DefinitionStableId,
                projectile.SourceEntityId.Value,
                projectile.ActionId,
                projectile.ActionInstanceId,
                projectile.TraceId,
                projectile.LocalFrame,
                projectile.SpawnFrame.Value,
                projectile.LastFrame.Value,
                projectile.AgeFrames,
                projectile.Position.X.RawValue,
                projectile.Position.Y.RawValue,
                projectile.Position.Z.RawValue,
                projectile.Direction.X.RawValue,
                projectile.Direction.Y.RawValue,
                projectile.Direction.Z.RawValue,
                projectile.Origin.X.RawValue,
                projectile.Origin.Y.RawValue,
                projectile.Origin.Z.RawValue,
                projectile.HasTargetPoint,
                projectile.TargetPoint.X.RawValue,
                projectile.TargetPoint.Y.RawValue,
                projectile.TargetPoint.Z.RawValue,
                projectile.EffectiveLifetimeFrames,
                projectile.LaunchSpeedPerFrame.RawValue,
                projectile.HasTrackingTarget,
                projectile.TrackingTargetEntityId.Value,
                projectile.AcceptedImpactCount,
                ComputeAcceptedImpactHash(projectile.AcceptedImpacts));
        }

        private static long ComputeAcceptedImpactHash(IReadOnlyList<CombatProjectileHitRecord> impacts)
        {
            unchecked
            {
                long hash = 17L;
                int count = impacts != null ? impacts.Count : 0;
                for (int i = 0; i < count; i++)
                {
                    CombatProjectileHitRecord impact = impacts[i];
                    hash = (hash * 397L) ^ impact.TargetEntityId.Value;
                    hash = (hash * 397L) ^ impact.Frame.Value;
                }

                return hash;
            }
        }
    }

    /// <summary>
    /// Read-only ability cooldown authority sample for Runtime AI Lab diagnostics.
    /// </summary>
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample
    {
        public CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample(
            int casterEntityId,
            int abilityId,
            long endFrame)
        {
            CasterEntityId = casterEntityId;
            AbilityId = abilityId;
            EndFrame = endFrame;
        }

        public int CasterEntityId { get; }
        public int AbilityId { get; }
        public long EndFrame { get; }

        public static CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample[] Capture(
            AbilityRuntimeState state)
        {
            IReadOnlyList<AbilityRuntimeCooldownEntry> cooldowns = state != null
                ? state.Cooldowns
                : null;
            int count = cooldowns != null ? cooldowns.Count : 0;
            if (count <= 0)
                return Array.Empty<CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample>();

            var samples = new CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample[count];
            for (int i = 0; i < count; i++)
            {
                AbilityRuntimeCooldownEntry cooldown = cooldowns[i];
                samples[i] = new CharacterTestRuntimeAiLabFrameSyncAbilityCooldownSample(
                    cooldown.CasterEntityId,
                    cooldown.AbilityId,
                    cooldown.EndFrame);
            }

            return samples;
        }
    }
}

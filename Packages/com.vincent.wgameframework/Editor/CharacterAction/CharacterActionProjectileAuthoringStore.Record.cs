using System;
using MxFramework.CharacterAction;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Projectiles;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed partial class CharacterActionProjectileAuthoringStore
    {
        public sealed class CharacterActionProjectileAuthoringRecord
        {
            internal CharacterActionProjectileAuthoringRecord(
                CharacterActionProjectileSpawnProfileData spawnProfile,
                CharacterActionProjectileDefinitionData definition,
                CharacterActionProjectilePresentationBindingData binding,
                bool hasSourceDefinition,
                bool hasSourceSpawnProfile,
                bool hasGeneratedDefinition,
                bool hasGeneratedSpawnProfile,
                bool hasGeneratedPresentationBinding,
                bool generatedInSync,
                string sourceDefinitionPath,
                string sourceSpawnProfilePath,
                string sourcePresentationPath,
                string sourcePath,
                string generatedDefinitionPath,
                string generatedSpawnProfilePath,
                string generatedPresentationPath)
            {
                RequestId = string.IsNullOrWhiteSpace(spawnProfile?.StableId)
                    ? definition?.StableId ?? string.Empty
                    : spawnProfile.StableId;
                DefinitionId = definition?.DefinitionId ?? 0;
                StableId = string.IsNullOrWhiteSpace(RequestId)
                    ? definition?.StableId ?? string.Empty
                    : RequestId;
                ProjectileStableId = string.IsNullOrWhiteSpace(spawnProfile?.ProjectileStableId)
                    ? definition?.StableId ?? string.Empty
                    : spawnProfile.ProjectileStableId;
                DefinitionStableId = definition?.StableId ?? string.Empty;
                PhysicalEventsEnabled = definition?.PhysicalEventsEnabled ?? false;
                MotionKind = string.IsNullOrWhiteSpace(definition?.MotionKind) ? "Linear" : definition.MotionKind;
                SpeedProfileKind = string.IsNullOrWhiteSpace(definition?.SpeedProfileKind)
                    ? "Constant"
                    : definition.SpeedProfileKind;
                SpeedPerFrame = definition?.SpeedPerFrame ?? 0f;
                LifetimeFrames = definition?.LifetimeFrames ?? 0;
                ArcHeight = definition?.ArcHeight ?? 0f;
                GravityPerFrameSquared = definition?.GravityPerFrameSquared ?? 0f;
                MaxLaunchAngleDegrees = definition?.MaxLaunchAngleDegrees ?? 0f;
                MaxLaunchAngleTangent = definition?.MaxLaunchAngleTangent ?? 0f;
                LateralAmplitude = definition?.LateralAmplitude ?? 0f;
                LateralCycles = definition?.LateralCycles ?? 0;
                BezierControlOffsetAX = definition?.BezierControlOffsetAX ?? 0f;
                BezierControlOffsetAY = definition?.BezierControlOffsetAY ?? 0f;
                BezierControlOffsetAZ = definition?.BezierControlOffsetAZ ?? 0f;
                BezierControlOffsetBX = definition?.BezierControlOffsetBX ?? 0f;
                BezierControlOffsetBY = definition?.BezierControlOffsetBY ?? 0f;
                BezierControlOffsetBZ = definition?.BezierControlOffsetBZ ?? 0f;
                HomingTurnRatePerFrame = definition?.HomingTurnRatePerFrame ?? 1f;
                Radius = definition?.Radius ?? 0f;
                StopOnFirstAcceptedImpact = definition?.StopOnFirstAcceptedImpact ?? true;
                StopOnBlockedImpact = definition?.StopOnBlockedImpact ?? false;
                MaxAcceptedImpacts = definition?.MaxAcceptedImpacts ?? 1;
                HitSameTargetPolicy = definition?.HitSameTargetPolicy ?? "AllowRepeat";
                TargetTypes = definition?.TargetTypes ?? CombatHitEventConfig.DefaultTargetTypes;
                LayerMask = definition?.LayerMask > 0 ? definition.LayerMask : ResolveLayerMask(TargetTypes);
                EnvironmentHitPolicy = string.IsNullOrWhiteSpace(definition?.EnvironmentHitPolicy)
                    ? CombatEnvironmentHitPolicy.None.ToString()
                    : definition.EnvironmentHitPolicy;
                HitEventMaxAcceptedHits = definition?.HitEventMaxAcceptedHits ?? MaxAcceptedImpacts;
                HitEventSameTargetPolicy = string.IsNullOrWhiteSpace(definition?.HitEventSameTargetPolicy)
                    ? CharacterActionHitEventAuthoringModel.ToHitEventSameTargetPolicy(HitSameTargetPolicy)
                    : definition.HitEventSameTargetPolicy;
                PresentationProfileId = definition?.PresentationProfileId ?? string.Empty;
                DamageTypeId = definition?.DamageTypeId ?? string.Empty;
                BaseDamage = definition?.BaseDamage ?? 0;
                ImpactPower = definition?.ImpactPower ?? 0;
                LaunchPower = definition?.LaunchPower ?? 0;
                Tags = definition?.Tags ?? string.Empty;
                AnalysisCategory = definition?.AnalysisCategory ?? string.Empty;
                OnHitBuffs = CharacterActionHitEventAuthoringModel.NormalizeOnHitBuffs(definition?.OnHitBuffs);
                StableEventId = definition?.StableEventId ?? string.Empty;
                PolicyKind = definition?.PolicyKind ?? "DirectHit";
                AreaRadius = definition?.AreaRadius ?? 0f;
                AreaTargetTypes = definition?.AreaTargetTypes ?? TargetTypes;
                AreaEnvironmentHitPolicy = string.IsNullOrWhiteSpace(definition?.AreaEnvironmentHitPolicy)
                    ? EnvironmentHitPolicy
                    : definition.AreaEnvironmentHitPolicy;
                AreaHitEventMaxAcceptedHits = definition?.AreaHitEventMaxAcceptedHits > 0
                    ? definition.AreaHitEventMaxAcceptedHits
                    : HitEventMaxAcceptedHits;
                AreaHitEventSameTargetPolicy = string.IsNullOrWhiteSpace(definition?.AreaHitEventSameTargetPolicy)
                    ? HitEventSameTargetPolicy
                    : definition.AreaHitEventSameTargetPolicy;
                AreaLayerMask = definition?.AreaLayerMask > 0
                    ? definition.AreaLayerMask
                    : string.Equals(PolicyKind, CombatProjectileImpactPolicyKind.AreaPulseOnImpact.ToString(), StringComparison.Ordinal)
                        ? ResolveLayerMask(AreaTargetTypes)
                        : 0;
                AreaOnHitBuffs = CharacterActionHitEventAuthoringModel.NormalizeOnHitBuffs(definition?.AreaOnHitBuffs);
                TriggerOnExpire = definition?.TriggerOnExpire ?? false;
                PrefabResourceId = binding?.PrefabResourceId ?? string.Empty;
                PrefabResourceTypeId = binding?.PrefabResourceTypeId ?? string.Empty;
                PrefabResourceVariant = binding?.PrefabResourceVariant ?? string.Empty;
                PrefabResourcePackageId = binding?.PrefabResourcePackageId ?? string.Empty;
                ViewNamePrefix = string.IsNullOrWhiteSpace(binding?.ViewNamePrefix)
                    ? "ProjectileVFX"
                    : binding.ViewNamePrefix;
                IsLegacyV1PresentationBinding = binding?.IsLegacyV1 == true;
                OriginKind = spawnProfile?.OriginKind ?? "Socket";
                SocketId = spawnProfile?.SocketId ?? string.Empty;
                LocalOffsetX = spawnProfile?.LocalOffsetX ?? 0f;
                LocalOffsetY = spawnProfile?.LocalOffsetY ?? 0f;
                LocalOffsetZ = spawnProfile?.LocalOffsetZ ?? 0f;
                FallbackRootOffsetX = spawnProfile?.FallbackRootOffsetX ?? 0f;
                FallbackRootOffsetY = spawnProfile?.FallbackRootOffsetY ?? 1.15f;
                FallbackRootOffsetZ = spawnProfile?.FallbackRootOffsetZ ?? 0.65f;
                AimKind = spawnProfile?.AimKind ?? "CasterForwardRange";
                AimTargetHeightOffset = spawnProfile?.AimTargetHeightOffset ?? 0f;
                AimFallbackKind = string.IsNullOrWhiteSpace(spawnProfile?.AimFallbackKind)
                    ? "CasterForwardRange"
                    : spawnProfile.AimFallbackKind;
                AimMaxRange = spawnProfile?.AimMaxRange ?? 0f;
                HasSpawnProfile = spawnProfile != null;
                HasPresentationBinding = binding != null;
                HasSourceDefinition = hasSourceDefinition;
                HasSourceSpawnProfile = hasSourceSpawnProfile;
                HasGeneratedDefinition = hasGeneratedDefinition;
                HasGeneratedSpawnProfile = hasGeneratedSpawnProfile;
                HasGeneratedPresentationBinding = hasGeneratedPresentationBinding;
                GeneratedInSync = generatedInSync;
                SourceDefinitionPath = sourceDefinitionPath ?? string.Empty;
                SourceSpawnProfilePath = sourceSpawnProfilePath ?? string.Empty;
                SourcePresentationPath = sourcePresentationPath ?? string.Empty;
                SourcePath = sourcePath ?? string.Empty;
                GeneratedDefinitionPath = generatedDefinitionPath ?? string.Empty;
                GeneratedSpawnProfilePath = generatedSpawnProfilePath ?? string.Empty;
                GeneratedPresentationPath = generatedPresentationPath ?? string.Empty;
                DisplayName = FormatProjectileName(StableId);
            }

            public string RequestId { get; }
            public int DefinitionId { get; }
            public string StableId { get; }
            public string ProjectileStableId { get; }
            public string DefinitionStableId { get; }
            public bool PhysicalEventsEnabled { get; }
            public string MotionKind { get; }
            public string SpeedProfileKind { get; }
            public float SpeedPerFrame { get; }
            public int LifetimeFrames { get; }
            public float ArcHeight { get; }
            public float GravityPerFrameSquared { get; }
            public float MaxLaunchAngleDegrees { get; }
            public float MaxLaunchAngleTangent { get; }
            public float LateralAmplitude { get; }
            public int LateralCycles { get; }
            public float BezierControlOffsetAX { get; }
            public float BezierControlOffsetAY { get; }
            public float BezierControlOffsetAZ { get; }
            public float BezierControlOffsetBX { get; }
            public float BezierControlOffsetBY { get; }
            public float BezierControlOffsetBZ { get; }
            public float HomingTurnRatePerFrame { get; }
            public float Radius { get; }
            public int LayerMask { get; }
            public bool StopOnFirstAcceptedImpact { get; }
            public bool StopOnBlockedImpact { get; }
            public int MaxAcceptedImpacts { get; }
            public string HitSameTargetPolicy { get; }
            public CombatHitTargetTypes TargetTypes { get; }
            public string EnvironmentHitPolicy { get; }
            public int HitEventMaxAcceptedHits { get; }
            public string HitEventSameTargetPolicy { get; }
            public string PresentationProfileId { get; }
            public string DamageTypeId { get; }
            public int BaseDamage { get; }
            public int ImpactPower { get; }
            public int LaunchPower { get; }
            public string Tags { get; }
            public string AnalysisCategory { get; }
            public CharacterActionHitBuffJsonDto[] OnHitBuffs { get; }
            public string StableEventId { get; }
            public string PolicyKind { get; }
            public float AreaRadius { get; }
            public int AreaLayerMask { get; }
            public CombatHitTargetTypes AreaTargetTypes { get; }
            public string AreaEnvironmentHitPolicy { get; }
            public int AreaHitEventMaxAcceptedHits { get; }
            public string AreaHitEventSameTargetPolicy { get; }
            public CharacterActionHitBuffJsonDto[] AreaOnHitBuffs { get; }
            public bool TriggerOnExpire { get; }
            public string PrefabResourceId { get; }
            public string PrefabResourceTypeId { get; }
            public string PrefabResourceVariant { get; }
            public string PrefabResourcePackageId { get; }
            public string ViewNamePrefix { get; }
            public bool IsLegacyV1PresentationBinding { get; }
            public string OriginKind { get; }
            public string SocketId { get; }
            public float LocalOffsetX { get; }
            public float LocalOffsetY { get; }
            public float LocalOffsetZ { get; }
            public float FallbackRootOffsetX { get; }
            public float FallbackRootOffsetY { get; }
            public float FallbackRootOffsetZ { get; }
            public string AimKind { get; }
            public float AimTargetHeightOffset { get; }
            public string AimFallbackKind { get; }
            public float AimMaxRange { get; }
            public bool HasSpawnProfile { get; }
            public bool HasPresentationBinding { get; }
            public bool HasSourceDefinition { get; }
            public bool HasSourceSpawnProfile { get; }
            public bool HasGeneratedDefinition { get; }
            public bool HasGeneratedSpawnProfile { get; }
            public bool HasGeneratedPresentationBinding { get; }
            public bool GeneratedInSync { get; }
            public string SourceDefinitionPath { get; }
            public string SourceSpawnProfilePath { get; }
            public string SourcePresentationPath { get; }
            public string SourcePath { get; }
            public string GeneratedDefinitionPath { get; }
            public string GeneratedSpawnProfilePath { get; }
            public string GeneratedPresentationPath { get; }
            public string DisplayName { get; }
            public string FlightPrefabResource => string.IsNullOrWhiteSpace(PrefabResourceId)
                ? string.Empty
                : PrefabResourceId
                    + ":"
                    + PrefabResourceTypeId
                    + (string.IsNullOrWhiteSpace(PrefabResourceVariant) ? string.Empty : "#" + PrefabResourceVariant)
                    + (string.IsNullOrWhiteSpace(PrefabResourcePackageId) ? string.Empty : "@" + PrefabResourcePackageId);
            public string ChoiceLabel => CharacterActionProjectileInspectorViewModel.Create(this).ChoiceLabel
                + " [" + StableId + "]";

        }
    }
}

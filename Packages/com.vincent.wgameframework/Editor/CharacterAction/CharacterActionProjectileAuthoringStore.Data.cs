using System;
using MxFramework.CharacterAction;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Projectiles;
using Newtonsoft.Json.Linq;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed partial class CharacterActionProjectileAuthoringStore
    {
        private static int ResolveLayerMask(CombatHitTargetTypes targetTypes)
        {
            if (targetTypes == CombatHitTargetTypes.None)
                targetTypes = CombatHitEventConfig.DefaultTargetTypes;

            int mask = 0;
            if ((targetTypes & (CombatHitTargetTypes.HostileCharacter
                    | CombatHitTargetTypes.FriendlyCharacter
                    | CombatHitTargetTypes.Self
                    | CombatHitTargetTypes.ThirdPartyCharacter
                    | CombatHitTargetTypes.DamageableObject)) != 0)
            {
                mask |= CharacterTestDynamicCharacterLayerMask;
            }
            if (CombatHitEventConfig.HasAnyWorldTarget(targetTypes))
                mask |= CharacterTestStaticEnvironmentLayerMask;
            return mask;
        }

        internal sealed class CharacterActionProjectileDefinitionData
        {
            private CharacterActionProjectileDefinitionData()
            {
            }

            public int DefinitionId { get; private set; }
            public string StableId { get; private set; }
            public bool PhysicalEventsEnabled { get; private set; }
            public string MotionKind { get; private set; }
            public string SpeedProfileKind { get; private set; }
            public float SpeedPerFrame { get; private set; }
            public int LifetimeFrames { get; private set; }
            public float ArcHeight { get; private set; }
            public float GravityPerFrameSquared { get; private set; }
            public float MaxLaunchAngleDegrees { get; private set; }
            public float MaxLaunchAngleTangent { get; private set; }
            public float LateralAmplitude { get; private set; }
            public int LateralCycles { get; private set; }
            public float BezierControlOffsetAX { get; private set; }
            public float BezierControlOffsetAY { get; private set; }
            public float BezierControlOffsetAZ { get; private set; }
            public float BezierControlOffsetBX { get; private set; }
            public float BezierControlOffsetBY { get; private set; }
            public float BezierControlOffsetBZ { get; private set; }
            public float HomingTurnRatePerFrame { get; private set; }
            public float Radius { get; private set; }
            public int LayerMask { get; private set; }
            public bool StopOnFirstAcceptedImpact { get; private set; }
            public bool StopOnBlockedImpact { get; private set; }
            public int MaxAcceptedImpacts { get; private set; }
            public string HitSameTargetPolicy { get; private set; }
            public CombatHitTargetTypes TargetTypes { get; private set; }
            public string EnvironmentHitPolicy { get; private set; }
            public int HitEventMaxAcceptedHits { get; private set; }
            public string HitEventSameTargetPolicy { get; private set; }
            public string PresentationProfileId { get; private set; }
            public string DamageTypeId { get; private set; }
            public int BaseDamage { get; private set; }
            public int ImpactPower { get; private set; }
            public int LaunchPower { get; private set; }
            public string Tags { get; private set; }
            public string AnalysisCategory { get; private set; }
            public CharacterActionHitBuffJsonDto[] OnHitBuffs { get; private set; }
            public string StableEventId { get; private set; }
            public string PolicyKind { get; private set; }
            public float AreaRadius { get; private set; }
            public int AreaLayerMask { get; private set; }
            public CombatHitTargetTypes AreaTargetTypes { get; private set; }
            public string AreaEnvironmentHitPolicy { get; private set; }
            public int AreaHitEventMaxAcceptedHits { get; private set; }
            public string AreaHitEventSameTargetPolicy { get; private set; }
            public CharacterActionHitBuffJsonDto[] AreaOnHitBuffs { get; private set; }
            public bool TriggerOnExpire { get; private set; }
            public string SourcePath { get; private set; }
            public bool IsAuthoringSource { get; private set; }

            public static CharacterActionProjectileDefinitionData FromJson(
                JObject fields,
                string sourcePath,
                bool isAuthoringSource)
            {
                JObject motion = fields?["motion"] as JObject;
                JObject speedProfile = motion?["speedProfile"] as JObject;
                JObject homing = motion?["homing"] as JObject;
                JObject bezierControlOffsetA = motion?["bezierControlOffsetA"] as JObject;
                JObject bezierControlOffsetB = motion?["bezierControlOffsetB"] as JObject;
                JObject collision = fields?["collision"] as JObject;
                JObject impact = fields?["impact"] as JObject;
                JObject hitEvent = impact?["hitEvent"] as JObject;
                JObject areaHitEvent = impact?["areaHitEvent"] as JObject;
                JObject damage = hitEvent?["damage"] as JObject;
                CombatHitTargetTypes targetTypes =
                    CharacterActionHitEventAuthoringModel.ParseTargetTypes(ReadStringArray(hitEvent?["targetTypes"] as JArray));
                CombatHitTargetTypes areaTargetTypes = areaHitEvent == null
                    ? targetTypes
                    : CharacterActionHitEventAuthoringModel.ParseTargetTypes(ReadStringArray(areaHitEvent?["targetTypes"] as JArray));
                string environmentHitPolicy = ReadString(hitEvent, "environmentHitPolicy", CombatEnvironmentHitPolicy.None.ToString());
                string areaEnvironmentHitPolicy = areaHitEvent == null
                    ? environmentHitPolicy
                    : ReadString(areaHitEvent, "environmentHitPolicy", CombatEnvironmentHitPolicy.None.ToString());
                int hitEventMaxAcceptedHits = ReadInt(hitEvent, "maxAcceptedHits", ReadInt(collision, "maxAcceptedImpacts", 1));
                int areaHitEventMaxAcceptedHits = areaHitEvent == null
                    ? hitEventMaxAcceptedHits
                    : ReadInt(areaHitEvent, "maxAcceptedHits", 1);
                string hitEventSameTargetPolicy = ReadString(
                    hitEvent,
                    "hitSameTargetPolicy",
                    CharacterActionHitEventAuthoringModel.ToHitEventSameTargetPolicy(ReadString(collision, "hitSameTargetPolicy", "AllowRepeat")));
                string areaHitEventSameTargetPolicy = areaHitEvent == null
                    ? hitEventSameTargetPolicy
                    : ReadString(areaHitEvent, "hitSameTargetPolicy", CombatHitSameTargetPolicy.AllowRepeat.ToString());
                return new CharacterActionProjectileDefinitionData
                {
                    DefinitionId = ReadInt(fields, "definitionId"),
                    StableId = ReadString(fields, "stableId"),
                    PhysicalEventsEnabled = ReadBool(fields, "physicalEventsEnabled", false),
                    MotionKind = ReadString(motion, "kind", "Linear"),
                    SpeedProfileKind = ReadString(speedProfile, "kind", "Constant"),
                    SpeedPerFrame = ReadFloat(motion, "speedPerFrame"),
                    LifetimeFrames = ReadInt(motion, "lifetimeFrames"),
                    ArcHeight = ReadFloat(motion, "arcHeight"),
                    GravityPerFrameSquared = ReadFloat(motion, "gravityPerFrameSquared"),
                    MaxLaunchAngleDegrees = ReadFloat(motion, "maxLaunchAngleDegrees"),
                    MaxLaunchAngleTangent = ReadMaxLaunchAngleTangent(motion),
                    LateralAmplitude = ReadFloat(motion, "lateralAmplitude"),
                    LateralCycles = ReadInt(motion, "lateralCycles"),
                    BezierControlOffsetAX = ReadFloat(bezierControlOffsetA, "x"),
                    BezierControlOffsetAY = ReadFloat(bezierControlOffsetA, "y"),
                    BezierControlOffsetAZ = ReadFloat(bezierControlOffsetA, "z"),
                    BezierControlOffsetBX = ReadFloat(bezierControlOffsetB, "x"),
                    BezierControlOffsetBY = ReadFloat(bezierControlOffsetB, "y"),
                    BezierControlOffsetBZ = ReadFloat(bezierControlOffsetB, "z"),
                    HomingTurnRatePerFrame = ReadFloat(homing, "turnRatePerFrame", ReadFloat(motion, "homingTurnRatePerFrame", 1f)),
                    Radius = ReadFloat(collision, "radius"),
                    LayerMask = ReadInt(collision, "layerMask"),
                    StopOnFirstAcceptedImpact = ReadBool(collision, "stopOnFirstAcceptedImpact", true),
                    StopOnBlockedImpact = ReadBool(collision, "stopOnBlockedImpact", false),
                    MaxAcceptedImpacts = ReadInt(collision, "maxAcceptedImpacts", 1),
                    HitSameTargetPolicy = ReadString(collision, "hitSameTargetPolicy", "AllowRepeat"),
                    TargetTypes = targetTypes,
                    EnvironmentHitPolicy = environmentHitPolicy,
                    HitEventMaxAcceptedHits = hitEventMaxAcceptedHits,
                    HitEventSameTargetPolicy = hitEventSameTargetPolicy,
                    PresentationProfileId = ReadString(impact, "presentationProfileId"),
                    DamageTypeId = ReadString(damage, "damageTypeId"),
                    BaseDamage = ReadInt(damage, "baseDamage"),
                    ImpactPower = ReadInt(damage, "impactPower"),
                    LaunchPower = ReadInt(damage, "launchPower"),
                    Tags = ReadDamageTags(damage),
                    AnalysisCategory = ReadString(damage, "analysisCategory"),
                    OnHitBuffs = ReadOnHitBuffs(hitEvent?["onHitBuffs"] as JArray),
                    StableEventId = ReadHitEventString(hitEvent, impact, "stableEventId"),
                    PolicyKind = ReadString(impact, "policyKind", "DirectHit"),
                    AreaRadius = ReadFloat(impact, "areaRadius"),
                    AreaLayerMask = ReadInt(impact, "areaLayerMask"),
                    AreaTargetTypes = areaTargetTypes,
                    AreaEnvironmentHitPolicy = areaEnvironmentHitPolicy,
                    AreaHitEventMaxAcceptedHits = areaHitEventMaxAcceptedHits,
                    AreaHitEventSameTargetPolicy = areaHitEventSameTargetPolicy,
                    AreaOnHitBuffs = ReadOnHitBuffs(areaHitEvent?["onHitBuffs"] as JArray),
                    TriggerOnExpire = ReadBool(impact, "triggerOnExpire", false),
                    SourcePath = sourcePath ?? string.Empty,
                    IsAuthoringSource = isAuthoringSource,
                };
            }

            public static bool ValueEquals(
                CharacterActionProjectileDefinitionData left,
                CharacterActionProjectileDefinitionData right)
            {
                if (ReferenceEquals(left, right))
                    return true;
                if (left == null || right == null)
                    return false;
                return left.DefinitionId == right.DefinitionId
                    && string.Equals(left.StableId, right.StableId, StringComparison.Ordinal)
                    && left.PhysicalEventsEnabled == right.PhysicalEventsEnabled
                    && string.Equals(left.MotionKind, right.MotionKind, StringComparison.Ordinal)
                    && string.Equals(left.SpeedProfileKind, right.SpeedProfileKind, StringComparison.Ordinal)
                    && FloatEquals(left.SpeedPerFrame, right.SpeedPerFrame)
                    && left.LifetimeFrames == right.LifetimeFrames
                    && FloatEquals(left.ArcHeight, right.ArcHeight)
                    && FloatEquals(left.GravityPerFrameSquared, right.GravityPerFrameSquared)
                    && FloatEquals(left.MaxLaunchAngleDegrees, right.MaxLaunchAngleDegrees)
                    && FloatEquals(left.MaxLaunchAngleTangent, right.MaxLaunchAngleTangent)
                    && FloatEquals(left.LateralAmplitude, right.LateralAmplitude)
                    && left.LateralCycles == right.LateralCycles
                    && FloatEquals(left.BezierControlOffsetAX, right.BezierControlOffsetAX)
                    && FloatEquals(left.BezierControlOffsetAY, right.BezierControlOffsetAY)
                    && FloatEquals(left.BezierControlOffsetAZ, right.BezierControlOffsetAZ)
                    && FloatEquals(left.BezierControlOffsetBX, right.BezierControlOffsetBX)
                    && FloatEquals(left.BezierControlOffsetBY, right.BezierControlOffsetBY)
                    && FloatEquals(left.BezierControlOffsetBZ, right.BezierControlOffsetBZ)
                    && FloatEquals(left.HomingTurnRatePerFrame, right.HomingTurnRatePerFrame)
                    && FloatEquals(left.Radius, right.Radius)
                    && ResolveDefinitionLayerMask(left) == ResolveDefinitionLayerMask(right)
                    && left.StopOnFirstAcceptedImpact == right.StopOnFirstAcceptedImpact
                    && left.StopOnBlockedImpact == right.StopOnBlockedImpact
                    && left.MaxAcceptedImpacts == right.MaxAcceptedImpacts
                    && string.Equals(left.HitSameTargetPolicy, right.HitSameTargetPolicy, StringComparison.Ordinal)
                    && left.TargetTypes == right.TargetTypes
                    && string.Equals(left.EnvironmentHitPolicy, right.EnvironmentHitPolicy, StringComparison.Ordinal)
                    && left.HitEventMaxAcceptedHits == right.HitEventMaxAcceptedHits
                    && string.Equals(left.HitEventSameTargetPolicy, right.HitEventSameTargetPolicy, StringComparison.Ordinal)
                    && string.Equals(left.PresentationProfileId, right.PresentationProfileId, StringComparison.Ordinal)
                    && string.Equals(left.DamageTypeId, right.DamageTypeId, StringComparison.Ordinal)
                    && left.BaseDamage == right.BaseDamage
                    && left.ImpactPower == right.ImpactPower
                    && left.LaunchPower == right.LaunchPower
                    && string.Equals(left.Tags, right.Tags, StringComparison.Ordinal)
                    && string.Equals(left.AnalysisCategory, right.AnalysisCategory, StringComparison.Ordinal)
                    && CharacterActionHitEventAuthoringModel.OnHitBuffsEqual(left.OnHitBuffs, right.OnHitBuffs)
                    && string.Equals(left.StableEventId, right.StableEventId, StringComparison.Ordinal)
                    && string.Equals(left.PolicyKind, right.PolicyKind, StringComparison.Ordinal)
                    && FloatEquals(left.AreaRadius, right.AreaRadius)
                    && ResolveDefinitionAreaLayerMask(left) == ResolveDefinitionAreaLayerMask(right)
                    && left.AreaTargetTypes == right.AreaTargetTypes
                    && string.Equals(left.AreaEnvironmentHitPolicy, right.AreaEnvironmentHitPolicy, StringComparison.Ordinal)
                    && left.AreaHitEventMaxAcceptedHits == right.AreaHitEventMaxAcceptedHits
                    && string.Equals(left.AreaHitEventSameTargetPolicy, right.AreaHitEventSameTargetPolicy, StringComparison.Ordinal)
                    && CharacterActionHitEventAuthoringModel.OnHitBuffsEqual(left.AreaOnHitBuffs, right.AreaOnHitBuffs)
                    && left.TriggerOnExpire == right.TriggerOnExpire;
            }

            private static int ResolveDefinitionLayerMask(CharacterActionProjectileDefinitionData value)
            {
                if (value == null)
                    return 0;
                return value.LayerMask > 0 ? value.LayerMask : ResolveLayerMask(value.TargetTypes);
            }

            private static int ResolveDefinitionAreaLayerMask(CharacterActionProjectileDefinitionData value)
            {
                if (value == null)
                    return 0;
                if (value.AreaLayerMask > 0)
                    return value.AreaLayerMask;
                return string.Equals(value.PolicyKind, CombatProjectileImpactPolicyKind.AreaPulseOnImpact.ToString(), StringComparison.Ordinal)
                    ? ResolveLayerMask(value.AreaTargetTypes)
                    : 0;
            }
        }

        internal sealed class CharacterActionProjectileSpawnProfileData
        {
            private CharacterActionProjectileSpawnProfileData()
            {
            }

            public string StableId { get; private set; }
            public string ProjectileStableId { get; private set; }
            public string OriginKind { get; private set; }
            public string SocketId { get; private set; }
            public float LocalOffsetX { get; private set; }
            public float LocalOffsetY { get; private set; }
            public float LocalOffsetZ { get; private set; }
            public float FallbackRootOffsetX { get; private set; }
            public float FallbackRootOffsetY { get; private set; }
            public float FallbackRootOffsetZ { get; private set; }
            public string AimKind { get; private set; }
            public float AimTargetHeightOffset { get; private set; }
            public string AimFallbackKind { get; private set; }
            public float AimMaxRange { get; private set; }
            public string SourcePath { get; private set; }
            public bool IsAuthoringSource { get; private set; }

            public static CharacterActionProjectileSpawnProfileData FromJson(
                JObject fields,
                string sourcePath,
                bool isAuthoringSource)
            {
                JObject origin = fields?["origin"] as JObject;
                JObject localOffset = origin?["localOffset"] as JObject;
                JObject fallbackOffset = origin?["fallbackRootOffset"] as JObject;
                JObject aim = fields?["aim"] as JObject;
                return new CharacterActionProjectileSpawnProfileData
                {
                    StableId = ReadString(fields, "stableId"),
                    ProjectileStableId = ReadString(fields, "projectileStableId"),
                    OriginKind = ReadString(origin, "kind", "Socket"),
                    SocketId = ReadString(origin, "socketId"),
                    LocalOffsetX = ReadFloat(localOffset, "x"),
                    LocalOffsetY = ReadFloat(localOffset, "y"),
                    LocalOffsetZ = ReadFloat(localOffset, "z"),
                    FallbackRootOffsetX = ReadFloat(fallbackOffset, "x"),
                    FallbackRootOffsetY = ReadFloat(fallbackOffset, "y", 1.15f),
                    FallbackRootOffsetZ = ReadFloat(fallbackOffset, "z", 0.65f),
                    AimKind = ReadString(aim, "kind", "CasterForwardRange"),
                    AimTargetHeightOffset = ReadFloat(aim, "targetHeightOffset"),
                    AimFallbackKind = ReadString(aim, "fallbackKind", "CasterForwardRange"),
                    AimMaxRange = ReadFloat(aim, "maxRange"),
                    SourcePath = sourcePath ?? string.Empty,
                    IsAuthoringSource = isAuthoringSource,
                };
            }

            public static bool ValueEquals(
                CharacterActionProjectileSpawnProfileData left,
                CharacterActionProjectileSpawnProfileData right)
            {
                if (ReferenceEquals(left, right))
                    return true;
                if (left == null || right == null)
                    return false;
                return string.Equals(left.StableId, right.StableId, StringComparison.Ordinal)
                    && string.Equals(left.ProjectileStableId, right.ProjectileStableId, StringComparison.Ordinal)
                    && string.Equals(left.OriginKind, right.OriginKind, StringComparison.Ordinal)
                    && string.Equals(left.SocketId, right.SocketId, StringComparison.Ordinal)
                    && FloatEquals(left.LocalOffsetX, right.LocalOffsetX)
                    && FloatEquals(left.LocalOffsetY, right.LocalOffsetY)
                    && FloatEquals(left.LocalOffsetZ, right.LocalOffsetZ)
                    && FloatEquals(left.FallbackRootOffsetX, right.FallbackRootOffsetX)
                    && FloatEquals(left.FallbackRootOffsetY, right.FallbackRootOffsetY)
                    && FloatEquals(left.FallbackRootOffsetZ, right.FallbackRootOffsetZ)
                    && string.Equals(left.AimKind, right.AimKind, StringComparison.Ordinal)
                    && FloatEquals(left.AimTargetHeightOffset, right.AimTargetHeightOffset)
                    && string.Equals(left.AimFallbackKind, right.AimFallbackKind, StringComparison.Ordinal)
                    && FloatEquals(left.AimMaxRange, right.AimMaxRange);
            }
        }

        internal sealed class CharacterActionProjectilePresentationBindingData
        {
            private const string DefaultViewNamePrefix = "ProjectileVFX";
            private static readonly string[] SlotNames =
            {
                "spawn", "flightBody", "trail", "directImpact", "areaImpact", "blockedImpact", "expire"
            };
            private static readonly string[] SlotDefaultOrientations =
            {
                "ProjectileForward", "ProjectileForward", "ProjectileForward", "SurfaceNormal",
                "ImpactNormalOrWorldUp", "SurfaceNormal", "ProjectileForward"
            };

            private CharacterActionProjectilePresentationBindingData()
            {
            }

            public int ProjectileDefinitionId { get; private set; }
            public string PrefabResourceId { get; private set; }
            public string PrefabResourceTypeId { get; private set; }
            public string PrefabResourceVariant { get; private set; }
            public string PrefabResourcePackageId { get; private set; }
            public string ViewNamePrefix { get; private set; }
            public CharacterActionProjectilePresentationSlotData[] Slots { get; private set; }
            public string AcceptedImpactExit { get; private set; }
            public string BlockedImpactExit { get; private set; }
            public string ExpirationExit { get; private set; }
            public string ExternalRemovalExit { get; private set; }
            public string RuntimeResetExit { get; private set; }
            public bool IsLegacyV1 { get; private set; }
            public string SourcePath { get; private set; }
            public bool IsAuthoringSource { get; private set; }

            public static CharacterActionProjectilePresentationBindingData FromJson(
                JObject fields,
                string sourcePath,
                bool isAuthoringSource)
            {
                JObject legacyResource = fields?["prefabResource"] as JObject;
                JObject slots = fields?["slots"] as JObject;
                var normalizedSlots = new CharacterActionProjectilePresentationSlotData[SlotNames.Length];
                for (int i = 0; i < SlotNames.Length; i++)
                {
                    normalizedSlots[i] = CharacterActionProjectilePresentationSlotData.FromJson(
                        slots?[SlotNames[i]] as JObject,
                        SlotDefaultOrientations[i]);
                }

                if (!normalizedSlots[1].IsConfigured && legacyResource != null)
                {
                    normalizedSlots[1] = CharacterActionProjectilePresentationSlotData.FromLegacyFlightBody(
                        legacyResource);
                }

                JObject exit = fields?["exitPolicy"] as JObject;
                CharacterActionProjectilePresentationSlotData flightBody = normalizedSlots[1];
                return new CharacterActionProjectilePresentationBindingData
                {
                    ProjectileDefinitionId = ReadInt(fields, "projectileDefinitionId"),
                    PrefabResourceId = flightBody.ResourceId,
                    PrefabResourceTypeId = flightBody.ResourceTypeId,
                    PrefabResourceVariant = flightBody.ResourceVariant,
                    PrefabResourcePackageId = flightBody.ResourcePackageId,
                    ViewNamePrefix = ReadString(
                        fields,
                        "viewNamePrefix",
                        DefaultViewNamePrefix),
                    Slots = normalizedSlots,
                    AcceptedImpactExit = ReadString(exit, "acceptedImpact", "Complete"),
                    BlockedImpactExit = ReadString(exit, "blockedImpact", "Dissipate"),
                    ExpirationExit = ReadString(exit, "expiration", "Complete"),
                    ExternalRemovalExit = ReadString(exit, "externalRemoval", "Dissipate"),
                    RuntimeResetExit = ReadString(exit, "runtimeReset", "Immediate"),
                    IsLegacyV1 = legacyResource != null && slots == null,
                    SourcePath = sourcePath ?? string.Empty,
                    IsAuthoringSource = isAuthoringSource,
                };
            }

            public static bool ValueEquals(
                CharacterActionProjectilePresentationBindingData left,
                CharacterActionProjectilePresentationBindingData right)
            {
                if (ReferenceEquals(left, right))
                    return true;
                if (left == null || right == null)
                    return false;
                if (left.ProjectileDefinitionId != right.ProjectileDefinitionId
                    || !string.Equals(left.ViewNamePrefix, right.ViewNamePrefix, StringComparison.Ordinal)
                    || !string.Equals(left.AcceptedImpactExit, right.AcceptedImpactExit, StringComparison.Ordinal)
                    || !string.Equals(left.BlockedImpactExit, right.BlockedImpactExit, StringComparison.Ordinal)
                    || !string.Equals(left.ExpirationExit, right.ExpirationExit, StringComparison.Ordinal)
                    || !string.Equals(left.ExternalRemovalExit, right.ExternalRemovalExit, StringComparison.Ordinal)
                    || !string.Equals(left.RuntimeResetExit, right.RuntimeResetExit, StringComparison.Ordinal))
                {
                    return false;
                }

                for (int i = 0; i < SlotNames.Length; i++)
                {
                    CharacterActionProjectilePresentationSlotData leftSlot = left.Slots?[i];
                    CharacterActionProjectilePresentationSlotData rightSlot = right.Slots?[i];
                    if (!CharacterActionProjectilePresentationSlotData.ValueEquals(
                            leftSlot,
                            rightSlot,
                            left.IsAuthoringSource,
                            right.IsAuthoringSource))
                        return false;
                }

                return true;
            }
        }

        internal sealed class CharacterActionProjectilePresentationSlotData
        {
            private CharacterActionProjectilePresentationSlotData()
            {
            }

            public bool IsConfigured { get; private set; }
            public string ResourceId { get; private set; }
            public string ResourceTypeId { get; private set; }
            public string ResourceVariant { get; private set; }
            public string ResourcePackageId { get; private set; }
            public string Orientation { get; private set; }
            public float LocalOffsetX { get; private set; }
            public float LocalOffsetY { get; private set; }
            public float LocalOffsetZ { get; private set; }
            public int LifetimeFramesOverride { get; private set; }
            public int VfxDefinitionId { get; private set; }

            public static CharacterActionProjectilePresentationSlotData FromJson(
                JObject fields,
                string defaultOrientation)
            {
                if (fields == null)
                    return new CharacterActionProjectilePresentationSlotData();

                JObject resource = fields["resource"] as JObject;
                JObject localOffset = fields["localOffset"] as JObject;
                return new CharacterActionProjectilePresentationSlotData
                {
                    IsConfigured = true,
                    ResourceId = ReadString(resource, "id"),
                    ResourceTypeId = ReadString(resource, "typeId"),
                    ResourceVariant = ReadString(resource, "variant"),
                    ResourcePackageId = ReadString(resource, "packageId"),
                    Orientation = ReadString(fields, "orientation", defaultOrientation),
                    LocalOffsetX = ReadFloat(localOffset, "x"),
                    LocalOffsetY = ReadFloat(localOffset, "y"),
                    LocalOffsetZ = ReadFloat(localOffset, "z"),
                    LifetimeFramesOverride = ReadInt(fields, "lifetimeFramesOverride"),
                    VfxDefinitionId = ReadInt(fields, "vfxDefinitionId"),
                };
            }

            public static CharacterActionProjectilePresentationSlotData FromLegacyFlightBody(JObject resource)
            {
                return new CharacterActionProjectilePresentationSlotData
                {
                    IsConfigured = true,
                    ResourceId = ReadString(resource, "id"),
                    ResourceTypeId = ReadString(resource, "typeId"),
                    ResourceVariant = ReadString(resource, "variant"),
                    ResourcePackageId = ReadString(resource, "packageId"),
                    Orientation = "ProjectileForward",
                };
            }

            public static bool ValueEquals(
                CharacterActionProjectilePresentationSlotData left,
                CharacterActionProjectilePresentationSlotData right,
                bool leftIsAuthoringSource,
                bool rightIsAuthoringSource)
            {
                bool leftConfigured = left?.IsConfigured == true;
                bool rightConfigured = right?.IsConfigured == true;
                if (leftConfigured != rightConfigured)
                    return false;
                if (!leftConfigured)
                    return true;

                bool vfxDefinitionIdEqual = left.VfxDefinitionId == right.VfxDefinitionId
                    || (leftIsAuthoringSource && left.VfxDefinitionId == 0)
                    || (rightIsAuthoringSource && right.VfxDefinitionId == 0);
                return string.Equals(left.ResourceId, right.ResourceId, StringComparison.Ordinal)
                    && string.Equals(left.ResourceTypeId, right.ResourceTypeId, StringComparison.Ordinal)
                    && string.Equals(left.ResourceVariant, right.ResourceVariant, StringComparison.Ordinal)
                    && string.Equals(left.ResourcePackageId, right.ResourcePackageId, StringComparison.Ordinal)
                    && string.Equals(left.Orientation, right.Orientation, StringComparison.Ordinal)
                    && FloatEquals(left.LocalOffsetX, right.LocalOffsetX)
                    && FloatEquals(left.LocalOffsetY, right.LocalOffsetY)
                    && FloatEquals(left.LocalOffsetZ, right.LocalOffsetZ)
                    && left.LifetimeFramesOverride == right.LifetimeFramesOverride
                    && vfxDefinitionIdEqual;
            }
        }
    }
}

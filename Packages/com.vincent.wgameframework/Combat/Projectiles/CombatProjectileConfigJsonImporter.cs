using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using Newtonsoft.Json.Linq;

namespace MxFramework.Combat.Projectiles
{
    public sealed class CombatProjectileDefinitionConfig
    {
        public CombatProjectileDefinitionConfig(
            int definitionId,
            string stableId,
            CombatProjectileMotionConfigData motion,
            CombatProjectileCollisionConfigData collision,
            CombatProjectileImpactConfigData impact,
            bool physicalEventsEnabled = false)
        {
            DefinitionId = definitionId;
            StableId = stableId ?? string.Empty;
            Motion = motion ?? new CombatProjectileMotionConfigData();
            Collision = collision ?? new CombatProjectileCollisionConfigData();
            Impact = impact ?? new CombatProjectileImpactConfigData();
            PhysicalEventsEnabled = physicalEventsEnabled;
        }

        public int DefinitionId { get; }
        public string StableId { get; }
        public CombatProjectileMotionConfigData Motion { get; }
        public CombatProjectileCollisionConfigData Collision { get; }
        public CombatProjectileImpactConfigData Impact { get; }
        public bool PhysicalEventsEnabled { get; }

        public CombatProjectileDefinition ToDefinition()
        {
            return new CombatProjectileDefinition(
                DefinitionId,
                StableId,
                new CombatProjectileMotionConfig(
                    Motion.Kind,
                    ProjectileConfigMath.ToFix64(Motion.SpeedPerFrame),
                    Motion.LifetimeFrames,
                    ProjectileConfigMath.ToFix64(Motion.ArcHeight),
                    Motion.SpeedProfileKind,
                    ProjectileConfigMath.ToFix64(Motion.LateralAmplitude),
                    Motion.LateralCycles,
                    Motion.BezierControlOffsetA,
                    Motion.BezierControlOffsetB,
                    ProjectileConfigMath.ToFix64(Motion.HomingTurnRatePerFrame),
                    ProjectileConfigMath.ToFix64(Motion.GravityPerFrameSquared),
                    ProjectileConfigMath.ToFix64(Motion.MaxLaunchAngleTangent)),
                new CombatProjectileCollisionConfig(
                    ProjectileConfigMath.ToFix64(Collision.Radius),
                    new CombatPhysicsLayerMask(Collision.LayerMask),
                    Collision.StopOnFirstAcceptedImpact,
                    Collision.StopOnBlockedImpact,
                    Collision.MaxAcceptedImpacts,
                    Collision.HitSameTargetPolicy),
                new CombatProjectileImpactConfig(
                    Impact.PresentationProfileId,
                    Impact.StableEventId,
                    Impact.PolicyKind,
                    ProjectileConfigMath.ToFix64(Impact.AreaRadius),
                    new CombatPhysicsLayerMask(Impact.AreaLayerMask),
                    Impact.TriggerOnExpire,
                    Impact.HitEvent,
                    Impact.AreaHitEvent,
                    Impact.EffectTimelineId,
                    Impact.PresentationProfileLabel,
                    Impact.StableEventLabel,
                    Impact.EffectTimelineLabel),
                PhysicalEventsEnabled);
        }
    }

    public sealed class CombatProjectileMotionConfigData
    {
        public CombatProjectileMotionConfigData(
            float speedPerFrame = 0f,
            int lifetimeFrames = 0,
            CombatProjectileMotionKind kind = CombatProjectileMotionKind.Linear,
            float arcHeight = 0f,
            CombatProjectileSpeedProfileKind speedProfileKind = CombatProjectileSpeedProfileKind.Constant,
            float lateralAmplitude = 0f,
            int lateralCycles = 0,
            FixVector3 bezierControlOffsetA = default,
            FixVector3 bezierControlOffsetB = default,
            float homingTurnRatePerFrame = 1f,
            float gravityPerFrameSquared = 0f,
            float maxLaunchAngleTangent = 0f)
        {
            SpeedPerFrame = speedPerFrame;
            LifetimeFrames = lifetimeFrames;
            Kind = kind;
            ArcHeight = arcHeight;
            SpeedProfileKind = speedProfileKind;
            LateralAmplitude = lateralAmplitude;
            LateralCycles = lateralCycles;
            BezierControlOffsetA = bezierControlOffsetA;
            BezierControlOffsetB = bezierControlOffsetB;
            HomingTurnRatePerFrame = homingTurnRatePerFrame;
            GravityPerFrameSquared = gravityPerFrameSquared;
            MaxLaunchAngleTangent = maxLaunchAngleTangent;
        }

        public float SpeedPerFrame { get; }
        public int LifetimeFrames { get; }
        public CombatProjectileMotionKind Kind { get; }
        public float ArcHeight { get; }
        public CombatProjectileSpeedProfileKind SpeedProfileKind { get; }
        public float LateralAmplitude { get; }
        public int LateralCycles { get; }
        public FixVector3 BezierControlOffsetA { get; }
        public FixVector3 BezierControlOffsetB { get; }
        public float HomingTurnRatePerFrame { get; }
        public float GravityPerFrameSquared { get; }
        public float MaxLaunchAngleTangent { get; }
    }

    public sealed class CombatProjectileCollisionConfigData
    {
        public CombatProjectileCollisionConfigData(
            float radius = 0f,
            int layerMask = 0,
            bool stopOnFirstAcceptedImpact = true,
            bool stopOnBlockedImpact = false,
            int maxAcceptedImpacts = 1,
            CombatProjectileHitSameTargetPolicy hitSameTargetPolicy = CombatProjectileHitSameTargetPolicy.AllowRepeat)
        {
            Radius = radius;
            LayerMask = layerMask;
            StopOnFirstAcceptedImpact = stopOnFirstAcceptedImpact;
            StopOnBlockedImpact = stopOnBlockedImpact;
            MaxAcceptedImpacts = maxAcceptedImpacts;
            HitSameTargetPolicy = hitSameTargetPolicy;
        }

        public float Radius { get; }
        public int LayerMask { get; }
        public bool StopOnFirstAcceptedImpact { get; }
        public bool StopOnBlockedImpact { get; }
        public int MaxAcceptedImpacts { get; }
        public CombatProjectileHitSameTargetPolicy HitSameTargetPolicy { get; }
    }

    public sealed class CombatProjectileImpactConfigData
    {
        public CombatProjectileImpactConfigData(
            int presentationProfileId = 0,
            int stableEventId = 0,
            CombatProjectileImpactPolicyKind policyKind = CombatProjectileImpactPolicyKind.DirectHit,
            float areaRadius = 0f,
            int areaLayerMask = 0,
            bool triggerOnExpire = false,
            CombatHitEventConfig hitEvent = default,
            CombatHitEventConfig areaHitEvent = default,
            int effectTimelineId = 0,
            string presentationProfileLabel = "",
            string stableEventLabel = "",
            string effectTimelineLabel = "")
        {
            PresentationProfileId = presentationProfileId;
            PresentationProfileLabel = presentationProfileLabel ?? string.Empty;
            StableEventId = stableEventId;
            StableEventLabel = stableEventLabel ?? string.Empty;
            PolicyKind = policyKind;
            AreaRadius = areaRadius;
            AreaLayerMask = areaLayerMask;
            TriggerOnExpire = triggerOnExpire;
            HitEvent = hitEvent.WithDefaults(StableEventId, StableEventLabel);
            AreaHitEvent = areaHitEvent.WithDefaults(StableEventId, StableEventLabel);
            EffectTimelineId = effectTimelineId;
            EffectTimelineLabel = effectTimelineLabel ?? string.Empty;
        }

        public int PresentationProfileId { get; }
        public string PresentationProfileLabel { get; }
        public int StableEventId { get; }
        public string StableEventLabel { get; }
        public CombatProjectileImpactPolicyKind PolicyKind { get; }
        public float AreaRadius { get; }
        public int AreaLayerMask { get; }
        public bool TriggerOnExpire { get; }
        public CombatHitEventConfig HitEvent { get; }
        public CombatHitEventConfig AreaHitEvent { get; }
        public int EffectTimelineId { get; }
        public string EffectTimelineLabel { get; }
    }

    public static class CombatProjectileConfigJsonImporter
    {
        public const string CurrentSchemaVersion = "5";

        public static CombatProjectileDefinitionRegistry ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new CombatProjectileDefinitionRegistry();

            return Import(File.ReadAllText(path), path);
        }

        public static CombatProjectileDefinitionRegistry Import(string json, string sourcePath = "")
        {
            var registry = new CombatProjectileDefinitionRegistry();
            if (string.IsNullOrWhiteSpace(json))
                return registry;

            JObject root = JObject.Parse(json);
            JToken schema = root["schemaVersion"];
            if (schema == null || schema.Type != JTokenType.String
                || !string.Equals(schema.Value<string>(), CurrentSchemaVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Unsupported projectile definition schema version at " + (sourcePath ?? string.Empty)
                    + "; expected " + CurrentSchemaVersion + ". Legacy content must be regenerated before import.");
            }
            JArray definitions = root["definitions"] as JArray;
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                if (!(definitions[i] is JObject fields))
                    continue;

                CombatProjectileDefinition definition;
                try
                {
                    definition = ReadDefinition(fields).ToDefinition();
                }
                catch (Exception ex) when (!(ex is InvalidOperationException))
                {
                    throw new InvalidOperationException(
                        "Invalid projectile definition at " + (sourcePath ?? string.Empty) + "#" + i + ": " + ex.Message,
                        ex);
                }

                if (!registry.TryRegister(definition, out string failure))
                {
                    throw new InvalidOperationException(
                        "Invalid projectile definition at " + (sourcePath ?? string.Empty) + "#" + i + ": " + failure);
                }
            }

            return registry;
        }

        private static CombatProjectileDefinitionConfig ReadDefinition(JObject fields)
        {
            return new CombatProjectileDefinitionConfig(
                ReadInt(fields, "definitionId"),
                ReadString(fields, "stableId"),
                ReadMotion(fields["motion"] as JObject),
                ReadCollision(fields["collision"] as JObject),
                ReadImpact(fields["impact"] as JObject),
                ReadBool(fields, "physicalEventsEnabled", false));
        }

        private static CombatProjectileMotionConfigData ReadMotion(JObject fields)
        {
            if (fields == null)
                return new CombatProjectileMotionConfigData();

            return new CombatProjectileMotionConfigData(
                ReadFloat(fields, "speedPerFrame"),
                ReadInt(fields, "lifetimeFrames"),
                ReadMotionKind(fields, "kind"),
                ReadFloat(fields, "arcHeight"),
                ReadSpeedProfileKind(fields["speedProfile"] as JObject, "kind"),
                ReadFloat(fields, "lateralAmplitude"),
                ReadInt(fields, "lateralCycles"),
                ReadVector3(fields["bezierControlOffsetA"] as JObject),
                ReadVector3(fields["bezierControlOffsetB"] as JObject),
                ReadFloat(fields["homing"] as JObject, "turnRatePerFrame", ReadFloat(fields, "homingTurnRatePerFrame", 1f)),
                ReadFloat(fields, "gravityPerFrameSquared"),
                ReadMaxLaunchAngleTangent(fields));
        }

        private static float ReadMaxLaunchAngleTangent(JObject fields)
        {
            float degrees = ReadFloat(fields, "maxLaunchAngleDegrees");
            if (degrees < 0f || degrees >= 90f)
                throw new ArgumentException("Projectile maxLaunchAngleDegrees must be 0 or greater than 0 and less than 90.");

            float tangent = ReadFloat(fields, "maxLaunchAngleTangent");
            if (tangent < 0f)
                throw new ArgumentException("Projectile maxLaunchAngleTangent cannot be negative.");

            if (tangent > 0f || degrees <= 0f)
                return tangent;

            return (float)Math.Tan(degrees * Math.PI / 180.0);
        }

        private static CombatProjectileCollisionConfigData ReadCollision(JObject fields)
        {
            if (fields == null)
                return new CombatProjectileCollisionConfigData();

            return new CombatProjectileCollisionConfigData(
                ReadFloat(fields, "radius"),
                ReadInt(fields, "layerMask"),
                ReadBool(fields, "stopOnFirstAcceptedImpact", true),
                ReadBool(fields, "stopOnBlockedImpact", false),
                ReadInt(fields, "maxAcceptedImpacts", 1),
                ReadHitSameTargetPolicy(fields, "hitSameTargetPolicy"));
        }

        private static CombatProjectileImpactConfigData ReadImpact(JObject fields)
        {
            if (fields == null)
                return new CombatProjectileImpactConfigData();

            RejectLegacyImpactDamageFields(fields);
            CombatProjectileImpactPolicyKind policyKind = ReadPolicyKind(fields, "policyKind");
            CombatHitEventConfig hitEvent = ReadHitEvent(
                fields["hitEvent"] as JObject,
                fields,
                requireDamage: true);
            JObject areaHitEventFields = fields["areaHitEvent"] as JObject;
            CombatHitDamageConfig inheritedAreaDamage =
                areaHitEventFields != null || policyKind == CombatProjectileImpactPolicyKind.AreaPulseOnImpact
                    ? hitEvent.Damage
                    : default;
            int stableEventId = ReadDefinitionId(fields, "stableEventId");
            string stableEventLabel = ReadString(fields, "stableEventLabel");
            if (stableEventId <= 0)
            {
                stableEventId = hitEvent.StableEventId;
                stableEventLabel = hitEvent.StableEventLabel;
            }
            return new CombatProjectileImpactConfigData(
                ReadDefinitionId(fields, "presentationProfileId"),
                stableEventId,
                policyKind,
                ReadFloat(fields, "areaRadius"),
                ReadInt(fields, "areaLayerMask"),
                ReadBool(fields, "triggerOnExpire", false),
                hitEvent,
                ReadHitEvent(areaHitEventFields, fields, inheritedAreaDamage),
                ReadDefinitionId(fields, "effectTimelineId"),
                ReadString(fields, "presentationProfileLabel"),
                stableEventLabel,
                ReadString(fields, "effectTimelineLabel"));
        }

        private static CombatHitEventConfig ReadHitEvent(
            JObject fields,
            JObject impactDefaults,
            CombatHitDamageConfig inheritedDamage = default,
            bool requireDamage = false)
        {
            if (fields == null)
            {
                if (requireDamage && !inheritedDamage.HasDamage)
                    throw new ArgumentException("hitEvent.damage is required for projectile impact.");

                return new CombatHitEventConfig(
                    CombatHitTargetTypes.HostileCharacter,
                    CombatEnvironmentHitPolicy.None,
                    ReadDefinitionId(impactDefaults, "stableEventId"),
                    damage: inheritedDamage);
            }

            CombatHitDamageConfig damage = ReadDamage(fields["damage"] as JObject, inheritedDamage, fields);
            if (requireDamage && !damage.HasDamage)
                throw new ArgumentException("hitEvent.damage is required for projectile impact.");

            return new CombatHitEventConfig(
                ReadTargetTypes(fields["targetTypes"]),
                ReadEnvironmentHitPolicy(fields, "environmentHitPolicy"),
                ReadHitEventId(fields, impactDefaults, "stableEventId"),
                Math.Max(1, ReadInt(fields, "maxAcceptedHits", 1)),
                ReadCombatHitSameTargetPolicy(fields, "hitSameTargetPolicy"),
                damage,
                ReadOnHitBuffs(fields["onHitBuffs"] as JArray),
                ReadHitEventString(fields, impactDefaults, "stableEventLabel"));
        }

        private static CombatHitBuffApplicationConfig[] ReadOnHitBuffs(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<CombatHitBuffApplicationConfig>();

            var values = new List<CombatHitBuffApplicationConfig>(array.Count);
            var seenBuffIds = new HashSet<int>();
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                if (item == null)
                    throw new ArgumentException("onHitBuffs entry must be an object.");

                int buffId = ReadInt(item, "buffId");
                if (buffId <= 0)
                    throw new ArgumentException("onHitBuffs buffId must be positive.");
                if (item["durationOverrideFrames"] != null)
                    throw new ArgumentException("onHitBuffs durationOverrideFrames is not supported; create a separate BuffId with its own duration.");
                if (item["sourceIdOverride"] != null)
                    throw new ArgumentException("onHitBuffs sourceIdOverride is not supported; source context comes from the hit result.");
                if (!seenBuffIds.Add(buffId))
                    throw new ArgumentException("onHitBuffs cannot repeat the same buffId in one hitEvent.");

                values.Add(new CombatHitBuffApplicationConfig(buffId));
            }

            return values.ToArray();
        }

        private static CombatHitDamageConfig ReadDamage(
            JObject fields,
            CombatHitDamageConfig inheritedDamage,
            JObject hitEventFields)
        {
            RejectLegacyHitEventDamageFields(hitEventFields);
            if (fields == null)
                return inheritedDamage;

            return new CombatHitDamageConfig(
                ReadDefinitionId(fields, "damageTypeId"),
                ReadInt(fields, "baseDamage"),
                ReadInt(fields, "impactPower"),
                ReadIdArray(fields["tags"] as JArray),
                ReadDefinitionId(fields, "analysisCategory"),
                ReadInt(fields, "launchPower"),
                damageTypeLabel: ReadString(fields, "damageTypeLabel"),
                analysisCategoryLabel: ReadString(fields, "analysisCategoryLabel"),
                tagLabels: ReadStringArray(fields["tagLabels"] as JArray),
                sourceAttributeScales: ReadSourceAttributeScales(fields["sourceAttributeScales"] as JArray));
        }

        private static DamageAttributeScale[] ReadSourceAttributeScales(JArray fields)
        {
            if (fields == null || fields.Count == 0)
                return Array.Empty<DamageAttributeScale>();
            var scales = new DamageAttributeScale[fields.Count];
            for (int i = 0; i < fields.Count; i++)
            {
                if (!(fields[i] is JObject item))
                    throw new ArgumentException("sourceAttributeScales entry must be an object.");
                int attributeId = ReadDefinitionId(item, "attributeId");
                float scale = item["scale"]?.Value<float>() ?? 0f;
                if (attributeId <= 0 || float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0f)
                    throw new ArgumentException("sourceAttributeScales entry is invalid.");
                scales[i] = new DamageAttributeScale(attributeId, scale);
            }
            return scales;
        }

        private static void RejectLegacyImpactDamageFields(JObject fields)
        {
            if (fields == null)
                return;
            if (fields["damageProfileId"] != null)
                throw new ArgumentException("impact.damageProfileId is no longer supported; configure impact.hitEvent.damage.");
            if (HasLegacyDamageField(fields))
                throw new ArgumentException("impact top-level damage fields are no longer supported; configure impact.hitEvent.damage.");
        }

        private static void RejectLegacyHitEventDamageFields(JObject fields)
        {
            if (fields == null)
                return;
            if (fields["damageProfileId"] != null)
                throw new ArgumentException("hitEvent.damageProfileId is no longer supported; configure hitEvent.damage.");
            if (HasLegacyDamageField(fields))
                throw new ArgumentException("hitEvent top-level damage fields are no longer supported; configure hitEvent.damage.");
        }

        private static bool HasLegacyDamageField(JObject fields)
        {
            return fields["damageTypeId"] != null
                || fields["formulaId"] != null
                || fields["baseDamage"] != null
                || fields["sourceAttributeScales"] != null
                || fields["impactPower"] != null
                || fields["launchPower"] != null
                || fields["tags"] != null
                || fields["analysisCategory"] != null;
        }

        private static int ReadHitEventId(JObject fields, JObject defaults, string name)
        {
            return fields?[name] != null ? ReadDefinitionId(fields, name) : ReadDefinitionId(defaults, name);
        }

        private static int ReadDefinitionId(JObject fields, string name)
        {
            JToken token = fields?[name];
            if (token == null) return 0;
            if (token.Type != JTokenType.Integer) throw new ArgumentException("Projectile content ID must be authored as integer: " + name);
            int value = token.Value<int>();
            if (value < 0) throw new ArgumentException("Projectile content ID cannot be negative: " + name);
            return value;
        }

        private static int[] ReadIdArray(JArray values)
        {
            if (values == null) return Array.Empty<int>();
            var result = new int[values.Count];
            for (int i = 0; i < result.Length; i++)
            {
                if (values[i].Type != JTokenType.Integer || (result[i] = values[i].Value<int>()) <= 0)
                    throw new ArgumentException("Projectile tag ID must be an authored positive integer.");
            }
            return result;
        }

        private static string ReadHitEventString(JObject fields, JObject impactDefaults, string name)
        {
            string value = ReadString(fields, name);
            return string.IsNullOrWhiteSpace(value) ? ReadString(impactDefaults, name) : value;
        }

        private static CombatHitTargetTypes ReadTargetTypes(JToken token)
        {
            if (token == null)
                return CombatHitTargetTypes.HostileCharacter;

            CombatHitTargetTypes result = CombatHitTargetTypes.None;
            bool hasExplicitTargetTypes = false;
            if (token.Type == JTokenType.String)
            {
                string value = token.Value<string>();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    hasExplicitTargetTypes = true;
                    result |= ReadTargetType(value);
                }
            }
            else if (token is JArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    string value = array[i].Value<string>();
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    hasExplicitTargetTypes = true;
                    result |= ReadTargetType(value);
                }
            }

            return hasExplicitTargetTypes ? result : CombatHitTargetTypes.None;
        }

        private static string[] ReadStringArray(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<string>();

            var values = new List<string>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                string value = array[i]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value);
            }

            return values.ToArray();
        }

        private static CombatHitTargetTypes ReadTargetType(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return CombatHitTargetTypes.None;

            if (Enum.TryParse(value, false, out CombatHitTargetTypes targetType)
                && Enum.IsDefined(typeof(CombatHitTargetTypes), targetType))
            {
                return targetType;
            }

            throw new ArgumentException("Unsupported combat hit target type: " + value + ".");
        }

        private static CombatEnvironmentHitPolicy ReadEnvironmentHitPolicy(JObject fields, string name)
        {
            string value = ReadString(fields, name, CombatEnvironmentHitPolicy.None.ToString());
            if (string.IsNullOrWhiteSpace(value))
                return CombatEnvironmentHitPolicy.None;

            if (Enum.TryParse(value, false, out CombatEnvironmentHitPolicy policy)
                && Enum.IsDefined(typeof(CombatEnvironmentHitPolicy), policy))
            {
                return policy;
            }

            throw new ArgumentException("Unsupported combat environment hit policy: " + value + ".");
        }

        private static CombatHitSameTargetPolicy ReadCombatHitSameTargetPolicy(JObject fields, string name)
        {
            string value = ReadString(fields, name, CombatHitSameTargetPolicy.AllowRepeat.ToString());
            if (string.IsNullOrWhiteSpace(value))
                return CombatHitSameTargetPolicy.AllowRepeat;

            if (Enum.TryParse(value, false, out CombatHitSameTargetPolicy policy)
                && Enum.IsDefined(typeof(CombatHitSameTargetPolicy), policy))
            {
                return policy;
            }

            throw new ArgumentException("Unsupported combat hit same target policy: " + value + ".");
        }

        private static CombatProjectileImpactPolicyKind ReadPolicyKind(JObject fields, string name)
        {
            string value = ReadString(fields, name, CombatProjectileImpactPolicyKind.DirectHit.ToString());
            if (string.IsNullOrWhiteSpace(value))
                return CombatProjectileImpactPolicyKind.DirectHit;

            if (Enum.TryParse(value, ignoreCase: false, out CombatProjectileImpactPolicyKind policy)
                && Enum.IsDefined(typeof(CombatProjectileImpactPolicyKind), policy))
            {
                return policy;
            }

            throw new ArgumentException("Unsupported projectile impact policy kind: " + value + ".");
        }

        private static CombatProjectileMotionKind ReadMotionKind(JObject fields, string name)
        {
            string value = ReadString(fields, name, CombatProjectileMotionKind.Linear.ToString());
            if (string.IsNullOrWhiteSpace(value))
                return CombatProjectileMotionKind.Linear;

            if (Enum.TryParse(value, ignoreCase: false, out CombatProjectileMotionKind kind)
                && Enum.IsDefined(typeof(CombatProjectileMotionKind), kind))
            {
                return kind;
            }

            throw new ArgumentException("Unsupported projectile motion kind: " + value + ".");
        }

        private static CombatProjectileSpeedProfileKind ReadSpeedProfileKind(JObject fields, string name)
        {
            string value = ReadString(fields, name, CombatProjectileSpeedProfileKind.Constant.ToString());
            if (string.IsNullOrWhiteSpace(value))
                return CombatProjectileSpeedProfileKind.Constant;

            if (Enum.TryParse(value, ignoreCase: false, out CombatProjectileSpeedProfileKind kind)
                && Enum.IsDefined(typeof(CombatProjectileSpeedProfileKind), kind))
            {
                return kind;
            }

            throw new ArgumentException("Unsupported projectile speed profile kind: " + value + ".");
        }

        private static CombatProjectileHitSameTargetPolicy ReadHitSameTargetPolicy(JObject fields, string name)
        {
            string value = ReadString(fields, name, CombatProjectileHitSameTargetPolicy.AllowRepeat.ToString());
            if (string.IsNullOrWhiteSpace(value))
                return CombatProjectileHitSameTargetPolicy.AllowRepeat;

            if (Enum.TryParse(value, ignoreCase: false, out CombatProjectileHitSameTargetPolicy policy)
                && Enum.IsDefined(typeof(CombatProjectileHitSameTargetPolicy), policy))
            {
                return policy;
            }

            throw new ArgumentException("Unsupported projectile hit same target policy: " + value + ".");
        }

        private static string ReadString(JObject fields, string name, string fallback = "")
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback ?? string.Empty;

            JToken token = fields[name];
            return token == null ? fallback ?? string.Empty : token.Value<string>() ?? fallback ?? string.Empty;
        }

        private static int ReadInt(JObject fields, string name, int fallback = 0)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;

            JToken token = fields[name];
            return token == null ? fallback : token.Value<int>();
        }

        private static long ReadLong(JObject fields, string name, long fallback = 0L)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;

            JToken token = fields[name];
            if (token == null)
                return fallback;
            if (token.Type == JTokenType.String
                && long.TryParse(token.Value<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            {
                return parsed;
            }

            return token.Value<long>();
        }

        private static bool ReadBool(JObject fields, string name, bool fallback)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;

            JToken token = fields[name];
            return token == null ? fallback : token.Value<bool>();
        }

        private static FixVector3 ReadVector3(JObject fields)
        {
            if (fields == null)
                return FixVector3.Zero;

            return new FixVector3(
                ProjectileConfigMath.ToFix64(ReadFloat(fields, "x")),
                ProjectileConfigMath.ToFix64(ReadFloat(fields, "y")),
                ProjectileConfigMath.ToFix64(ReadFloat(fields, "z")));
        }

        private static float ReadFloat(JObject fields, string name, float fallback = 0f)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;

            JToken token = fields[name];
            if (token == null)
                return fallback;
            if (token.Type == JTokenType.String
                && float.TryParse(token.Value<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return parsed;
            }

            return token.Value<float>();
        }
    }

    internal static class ProjectileConfigMath
    {
        public static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        public static FixVector3 ToFixVector3(float x, float y, float z)
        {
            return new FixVector3(ToFix64(x), ToFix64(y), ToFix64(z));
        }
    }
}

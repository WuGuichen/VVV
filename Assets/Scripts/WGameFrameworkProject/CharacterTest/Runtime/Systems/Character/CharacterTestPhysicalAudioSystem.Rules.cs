using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestPhysicalAudioSystem
    {
        private static AudioTraceIdentity ResolveTraceId(PhysicalAudioInteractionKind kind)
        {
            switch (kind)
            {
                case PhysicalAudioInteractionKind.Footstep:
                    return ResolvedFootstepTraceId;
                case PhysicalAudioInteractionKind.Landing:
                    return ResolvedLandingTraceId;
                case PhysicalAudioInteractionKind.ObjectImpact:
                    return ResolvedObjectImpactTraceId;
                default:
                    return AudioTraceIdentity.None;
            }
        }

        private static PhysicalAudioCueRuleTable CreatePhysicalAudioRuleTable(
            ICharacterTestAudioCueCatalog catalog)
        {
            var rules = new PhysicalAudioCueRule[10];
            int count = 0;
            TryAddRule(
                rules,
                ref count,
                catalog,
                CombatPhysicalContactKind.BodyPartContact,
                CombatSurfaceMaterialKind.Default,
                CharacterTestAudioCueCatalog.SoleContactDefaultCueKey,
                BodyPartContactCooldownFrames,
                priority: 5);
            TryAddRule(
                rules,
                ref count,
                catalog,
                CombatPhysicalContactKind.BodyPartContact,
                CombatSurfaceMaterialKind.Rough,
                CharacterTestAudioCueCatalog.SoleContactRoughCueKey,
                BodyPartContactCooldownFrames,
                priority: 5);
            TryAddRule(
                rules,
                ref count,
                catalog,
                CombatPhysicalContactKind.BodyPartContact,
                CombatSurfaceMaterialKind.HardSlick,
                CharacterTestAudioCueCatalog.SoleContactHardSlickCueKey,
                BodyPartContactCooldownFrames,
                priority: 5);
            TryAddRule(
                rules,
                ref count,
                catalog,
                CombatPhysicalContactKind.BodyPartContact,
                CombatSurfaceMaterialKind.Elastic,
                CharacterTestAudioCueCatalog.SoleContactElasticCueKey,
                BodyPartContactCooldownFrames,
                priority: 5);
            TryAddRule(
                rules,
                ref count,
                catalog,
                CombatPhysicalContactKind.BodyPartContact,
                CombatSurfaceMaterialKind.BouncePad,
                CharacterTestAudioCueCatalog.SoleContactElasticCueKey,
                BodyPartContactCooldownFrames,
                priority: 5);
            TryAddRule(
                rules,
                ref count,
                catalog,
                CombatPhysicalContactKind.ObjectImpact,
                CombatSurfaceMaterialKind.Default,
                CharacterTestAudioCueCatalog.ObjectCollisionDefaultCueKey,
                ObjectContactCooldownFrames,
                priority: 10);
            TryAddRule(
                rules,
                ref count,
                catalog,
                CombatPhysicalContactKind.ObjectImpact,
                CombatSurfaceMaterialKind.Rough,
                CharacterTestAudioCueCatalog.ObjectCollisionRoughCueKey,
                ObjectContactCooldownFrames,
                priority: 10);
            TryAddRule(
                rules,
                ref count,
                catalog,
                CombatPhysicalContactKind.ObjectImpact,
                CombatSurfaceMaterialKind.HardSlick,
                CharacterTestAudioCueCatalog.ObjectCollisionHardSlickCueKey,
                ObjectContactCooldownFrames,
                priority: 10);
            TryAddRule(
                rules,
                ref count,
                catalog,
                CombatPhysicalContactKind.ObjectImpact,
                CombatSurfaceMaterialKind.Elastic,
                CharacterTestAudioCueCatalog.ObjectCollisionElasticCueKey,
                ObjectContactCooldownFrames,
                priority: 10);
            TryAddRule(
                rules,
                ref count,
                catalog,
                CombatPhysicalContactKind.ObjectImpact,
                CombatSurfaceMaterialKind.BouncePad,
                CharacterTestAudioCueCatalog.ObjectCollisionElasticCueKey,
                ObjectContactCooldownFrames,
                priority: 10);
            return new PhysicalAudioCueRuleTable(rules, count);
        }

        private static PhysicalAudioParameterMapping CreatePhysicalAudioParameterMapping(
            ICharacterTestAudioCueCatalog catalog)
        {
            if (catalog == null
                || !catalog.TryGetCue(CharacterTestAudioCueCatalog.SoleContactDefaultCueKey, out AudioCueDefinition cue)
                || !catalog.TryGetParameter(cue.EventId, CharacterTestAudioCueCatalog.PhysicalAudioVolumeParameterName, out AudioParameterDefinition volume)
                || !catalog.TryGetParameter(cue.EventId, CharacterTestAudioCueCatalog.PhysicalAudioPitchParameterName, out AudioParameterDefinition pitch)
                || !catalog.TryGetParameter(cue.EventId, CharacterTestAudioCueCatalog.PhysicalAudioLowPassParameterName, out AudioParameterDefinition lowPass)
                || !catalog.TryGetParameter(cue.EventId, CharacterTestAudioCueCatalog.PhysicalAudioBrightnessParameterName, out AudioParameterDefinition brightness)
                || !catalog.TryGetParameter(cue.EventId, CharacterTestAudioCueCatalog.PhysicalAudioScrapeParameterName, out AudioParameterDefinition scrape))
                return PhysicalAudioParameterMapping.None;

            return new PhysicalAudioParameterMapping(
                volume.Id,
                pitch.Id,
                lowPass.Id,
                brightness.Id,
                scrape.Id);
        }

        private static void TryAddRule(
            PhysicalAudioCueRule[] rules,
            ref int count,
            ICharacterTestAudioCueCatalog catalog,
            CombatPhysicalContactKind contactKind,
            CombatSurfaceMaterialKind surfaceMaterialKind,
            string cueKey,
            int cooldownFrames,
            int priority,
            int minimumIntensity = 0,
            int bodyPartId = 0,
            int contactPointId = 0)
        {
            if (rules == null || count >= rules.Length)
                return;

            if (catalog == null || !catalog.TryGetCue(cueKey, out AudioCueDefinition cue))
                return;

            rules[count] = new PhysicalAudioCueRule(
                contactKind,
                surfaceMaterialKind,
                0,
                minimumIntensity,
                cue.EventId,
                cooldownFrames,
                priority,
                bodyPartId,
                contactPointId);
            count++;
        }

        private static Dictionary<int, string> BuildPhysicalAudioEventCueMap(ICharacterTestAudioCueCatalog catalog)
        {
            var result = new Dictionary<int, string>(16);
            if (catalog == null)
                return result;
            string[] cueKeys =
            {
                CharacterTestAudioCueCatalog.SoleContactDefaultCueKey,
                CharacterTestAudioCueCatalog.SoleContactRoughCueKey,
                CharacterTestAudioCueCatalog.SoleContactHardSlickCueKey,
                CharacterTestAudioCueCatalog.SoleContactElasticCueKey,
                CharacterTestAudioCueCatalog.LandingDefaultCueKey,
                CharacterTestAudioCueCatalog.LandingRoughCueKey,
                CharacterTestAudioCueCatalog.LandingHardSlickCueKey,
                CharacterTestAudioCueCatalog.LandingElasticCueKey,
                CharacterTestAudioCueCatalog.ObjectCollisionDefaultCueKey,
                CharacterTestAudioCueCatalog.ObjectCollisionRoughCueKey,
                CharacterTestAudioCueCatalog.ObjectCollisionHardSlickCueKey,
                CharacterTestAudioCueCatalog.ObjectCollisionElasticCueKey
            };
            for (int i = 0; i < cueKeys.Length; i++)
            {
                if (catalog.TryGetCue(cueKeys[i], out AudioCueDefinition cue))
                    result[cue.EventId] = cueKeys[i];
            }
            return result;
        }

        private string ResolveCueKey(int eventId)
        {
            return _cueKeyByEventId != null && _cueKeyByEventId.TryGetValue(eventId, out string cueKey)
                ? cueKey
                : string.Empty;
        }

        private static FixVector3 CreateFixPoint(int xMillimeters, int yMillimeters, int zMillimeters)
        {
            return new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(xMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(yMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(zMillimeters));
        }

        private static FixVector3 ToFixVector3(MxAnimationBakedVector3 value)
        {
            return ToFixVector3(
                value,
                CharacterWeaponTraceBakeArtifactBuilder.DefaultQuantizationScale);
        }

        private static FixVector3 ToFixVector3(MxAnimationBakedVector3 value, int quantizationScale)
        {
            int scale = quantizationScale > 0
                ? quantizationScale
                : CharacterWeaponTraceBakeArtifactBuilder.DefaultQuantizationScale;
            return new FixVector3(
                Fix64.FromRaw(value.X * Fix64.Scale / scale),
                Fix64.FromRaw(value.Y * Fix64.Scale / scale),
                Fix64.FromRaw(value.Z * Fix64.Scale / scale));
        }

        private static int StableContactPairKey(
            CombatPhysicalContactKind kind,
            string contactPair,
            int fallbackId)
        {
            unchecked
            {
                int hash = 23;
                hash = (hash * 31) + (int)kind;
                hash = (hash * 31) + fallbackId;
                if (!string.IsNullOrEmpty(contactPair))
                {
                    for (int i = 0; i < contactPair.Length; i++)
                    {
                        hash = (hash * 31) + contactPair[i];
                    }
                }

                hash &= int.MaxValue;
                return hash == 0 ? 1 : hash;
            }
        }

        private static int EstimateHorizontalSpeedMillimetersPerSecond(
            int velocityXMillimetersPerSecond,
            int velocityZMillimetersPerSecond)
        {
            return EstimateHorizontalDistanceMillimeters(
                velocityXMillimetersPerSecond,
                velocityZMillimetersPerSecond);
        }

        private static int ResolveBodyPartContactNormalSpeed(
            CharacterTestCharacterEntity entity,
            CharacterBodyPartContactAudioState contactAudioState)
        {
            int previousFallSpeed = 0;
            if (contactAudioState.HasMotionState && !contactAudioState.WasGrounded)
            {
                previousFallSpeed = DownwardSpeedMillimetersPerSecond(
                    contactAudioState.LastVerticalVelocityMillimetersPerSecond);
            }

            int currentFallSpeed = entity == null
                ? 0
                : DownwardSpeedMillimetersPerSecond(entity.VerticalVelocityMillimetersPerSecond);
            return previousFallSpeed >= currentFallSpeed ? previousFallSpeed : currentFallSpeed;
        }

        private static int ResolveBodyPartContactIntensity(
            int horizontalSpeedMillimetersPerSecond,
            int normalSpeedMillimetersPerSecond)
        {
            int normalImpact = normalSpeedMillimetersPerSecond >= MinimumBodyPartImpactSpeedMillimetersPerSecond
                ? normalSpeedMillimetersPerSecond
                : 0;
            return horizontalSpeedMillimetersPerSecond >= normalImpact
                ? horizontalSpeedMillimetersPerSecond
                : normalImpact;
        }

        private static int EstimateContactRelativeSpeed(
            int tangentialSpeedMillimetersPerSecond,
            int normalSpeedMillimetersPerSecond)
        {
            return EstimateHorizontalDistanceMillimeters(
                tangentialSpeedMillimetersPerSecond,
                normalSpeedMillimetersPerSecond);
        }

        private static int ResolveContactPressure(
            int normalSpeedMillimetersPerSecond,
            int tangentialSpeedMillimetersPerSecond,
            int massGrams)
        {
            int weightedSpeed = Math.Max(0, normalSpeedMillimetersPerSecond)
                + Math.Max(0, tangentialSpeedMillimetersPerSecond) / 3;
            return ResolvePressure(weightedSpeed, massGrams);
        }

        private static int ResolvePressure(int speedMillimetersPerSecond, int massGrams)
        {
            return CharacterTestDeterministicMath.DivideRoundToInt(
                (long)Math.Max(0, speedMillimetersPerSecond)
                * Math.Max(CharacterTestCharacterPhysicsComponent.MinimumMassGrams, massGrams),
                CharacterTestDeterministicUnits.PositionScale);
        }

        private static int DownwardSpeedMillimetersPerSecond(int verticalVelocityMillimetersPerSecond)
        {
            if (verticalVelocityMillimetersPerSecond >= 0)
                return 0;

            long speed = -(long)verticalVelocityMillimetersPerSecond;
            return speed >= int.MaxValue ? int.MaxValue : (int)speed;
        }

        private static int ClampFrame(long frame)
        {
            if (frame <= 0)
                return 0;
            return frame >= int.MaxValue ? int.MaxValue : (int)frame;
        }

        private static CombatPhysicsLayerMask GetFootProbeLayerMask(
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            CombatPhysicsLayerMask layerMask = physicsRuntime.StaticObstacleLayerMask.Value == 0
                ? physicsRuntime.ObstacleLayerMask
                : physicsRuntime.StaticObstacleLayerMask;
            return layerMask.AddLayer(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer);
        }

        private static CombatPhysicsLayerMask ResolveContactPointLayerMask(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            string layerMaskProfileId)
        {
            if (physicsRuntime == null)
                return CombatPhysicsLayerMask.None;

            string profile = layerMaskProfileId ?? string.Empty;
            if (string.Equals(profile, "character", StringComparison.OrdinalIgnoreCase))
                return physicsRuntime.DynamicCharacterObstacleLayerMask;

            if (string.Equals(profile, "all", StringComparison.OrdinalIgnoreCase)
                || string.Equals(profile, "obstacle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(profile, "obstacles", StringComparison.OrdinalIgnoreCase))
            {
                return physicsRuntime.ObstacleLayerMask;
            }

            return GetFootProbeLayerMask(physicsRuntime);
        }

        private static bool IsBodyPartContactAllowed(CharacterTestCharacterEntity entity, BodyPartContactHit hit)
        {
            return hit.HasHit && entity != null && entity.IsGrounded;
        }

        private static bool IsSpeedAtLeast(
            int velocityXMillimetersPerSecond,
            int velocityZMillimetersPerSecond,
            int minimumSpeedMillimetersPerSecond)
        {
            long x = velocityXMillimetersPerSecond;
            long z = velocityZMillimetersPerSecond;
            long minimum = minimumSpeedMillimetersPerSecond;
            return (x * x) + (z * z) >= minimum * minimum;
        }

        private static int EstimateHorizontalDistanceMillimeters(
            int deltaXMillimeters,
            int deltaZMillimeters)
        {
            long absoluteX = AbsToLong(deltaXMillimeters);
            long absoluteZ = AbsToLong(deltaZMillimeters);
            long max = absoluteX > absoluteZ ? absoluteX : absoluteZ;
            long min = absoluteX > absoluteZ ? absoluteZ : absoluteX;
            long estimate = max + (min / 2);
            return estimate > int.MaxValue ? int.MaxValue : (int)estimate;
        }

        private static long AbsToLong(int value)
        {
            return value < 0 ? -(long)value : value;
        }
    }
}

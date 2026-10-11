using System;
using System.Collections.Generic;
using System.IO;
using MagicaCloth2;
using Unity.Mathematics;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    internal static class MagicaCharacterClothParameterRanges
    {
        internal const float GravityMin = 0f;
        internal const float GravityMax = 20f;
        internal const float UnitMin = 0f;
        internal const float UnitMax = 1f;
        internal const float MovementSpeedMax = 10f;
        internal const float RotationSpeedMax = 1440f;
        internal const float ParticleSpeedMax = 10f;
        internal const float TeleportDistanceMax = 10f;
        internal const float TeleportRotationMax = 360f;
        internal const float AngleMax = 180f;
        internal const float MaxDistanceMax = 5f;
        internal const float CollisionFrictionMax = 0.5f;
        internal const float CollisionLimitDistanceMax = 1f;
        internal const float SurfaceThicknessMin = 0.001f;
        internal const float SurfaceThicknessMax = 0.05f;
        internal const float WindMax = 2f;
        internal const float SpringPowerMin = 0.001f;
        internal const float SpringPowerMax = 1f;
        internal const float SpringLimitDistanceMax = 5f;
    }

    internal static class MagicaCharacterClothParameterOverrideUtility
    {
        private static readonly Dictionary<string, ClothSerializeData> PresetCache =
            new Dictionary<string, ClothSerializeData>(StringComparer.OrdinalIgnoreCase);

        internal static bool TryLoadPreset(
            MagicaCharacterClothAuthoringEntry entry,
            out ClothSerializeData preset,
            out string error)
        {
            preset = null;
            error = string.Empty;
            if (entry == null || string.IsNullOrWhiteSpace(entry.presetAssetPath))
            {
                error = "Cloth Preset 路径为空。";
                return false;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, entry.presetAssetPath));
            if (!fullPath.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(fullPath))
            {
                error = "Cloth Preset 不存在或位于项目之外：" + entry.presetAssetPath;
                return false;
            }

            string cacheKey = fullPath + "|" + (entry.presetSha256 ?? string.Empty);
            if (PresetCache.TryGetValue(cacheKey, out preset))
                return true;

            try
            {
                var loaded = new ClothSerializeData();
                if (!loaded.ImportJson(File.ReadAllText(fullPath)))
                {
                    error = "Magica Cloth Preset 无法导入：" + entry.presetAssetPath;
                    return false;
                }
                loaded.DataValidate();
                PresetCache[cacheKey] = loaded;
                preset = loaded;
                return true;
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        internal static void Apply(
            ClothSerializeData data,
            MagicaCharacterClothParameterOverrides value)
        {
            if (data == null || value == null) return;

            Set(ref data.gravity, value.gravity);
            if (value.gravityDirection != null)
            {
                data.gravityDirection = new float3(
                    value.gravityDirection.x,
                    value.gravityDirection.y,
                    value.gravityDirection.z);
            }
            Set(data.damping, value.damping);
            Set(ref data.blendWeight, value.blendWeight);

            InertiaConstraint.SerializeData inertia = data.inertiaConstraint;
            Set(ref inertia.worldInertia, value.worldInertia);
            Set(ref inertia.localInertia, value.localInertia);
            Set(ref inertia.movementInertiaSmoothing, value.movementInertiaSmoothing);
            Set(inertia.movementSpeedLimit, value.movementSpeedLimit);
            Set(inertia.rotationSpeedLimit, value.rotationSpeedLimit);
            Set(inertia.particleSpeedLimit, value.particleSpeedLimit);
            Set(ref inertia.teleportDistance, value.teleportDistance);
            Set(ref inertia.teleportRotation, value.teleportRotation);

            Set(data.distanceConstraint.stiffness, value.distanceStiffness);
            Set(data.angleRestorationConstraint.stiffness, value.angleRestorationStiffness);
            if (value.angleRestorationStiffness != null)
                data.angleRestorationConstraint.useAngleRestoration = value.angleRestorationStiffness.use;
            Set(ref data.angleRestorationConstraint.velocityAttenuation, value.velocityAttenuation);
            Set(data.angleLimitConstraint.limitAngle, value.angleLimit);
            if (value.angleLimit != null)
                data.angleLimitConstraint.useAngleLimit = value.angleLimit.use;
            Set(data.motionConstraint.maxDistance, value.maxDistance);
            if (value.maxDistance != null)
                data.motionConstraint.useMaxDistance = value.maxDistance.use;

            Set(ref data.colliderCollisionConstraint.friction, value.collisionFriction);
            Set(data.colliderCollisionConstraint.limitDistance, value.collisionLimitDistance);
            Set(data.selfCollisionConstraint.surfaceThickness, value.surfaceThickness);

            Set(ref data.wind.influence, value.windInfluence);
            Set(ref data.wind.frequency, value.windFrequency);
            Set(ref data.wind.turbulence, value.windTurbulence);

            if (value.springPower != null)
                data.springConstraint.springPower = value.springPower.value;
            if (value.springPower != null)
                data.springConstraint.useSpring = value.springPower.use;
            Set(ref data.springConstraint.limitDistance, value.springLimitDistance);

            data.DataValidate();
        }

        private static void Set(ref float target, MagicaCharacterClothFloatOverride value)
        {
            if (value != null) target = value.value;
        }

        private static void Set(CurveSerializeData target, MagicaCharacterClothFloatOverride value)
        {
            if (target != null && value != null) target.SetValue(value.value);
        }

        private static void Set(CurveSerializeData target, MagicaCharacterClothLimitOverride value)
        {
            if (target != null && value != null) target.SetValue(value.value);
        }

        private static void Set(CheckSliderSerializeData target, MagicaCharacterClothLimitOverride value)
        {
            if (target != null && value != null) target.SetValue(value.use, value.value);
        }
    }
}

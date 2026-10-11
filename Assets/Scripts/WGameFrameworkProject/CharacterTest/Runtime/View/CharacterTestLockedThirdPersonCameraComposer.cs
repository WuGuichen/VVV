using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestLockedThirdPersonCameraComposition
    {
        public CharacterTestLockedThirdPersonCameraComposition(
            float yaw,
            float pitch,
            float distance,
            Vector3 worldOffset)
        {
            Yaw = yaw;
            Pitch = pitch;
            Distance = distance;
            WorldOffset = worldOffset;
        }

        public float Yaw { get; }
        public float Pitch { get; }
        public float Distance { get; }
        public Vector3 WorldOffset { get; }
    }

    internal readonly struct CharacterTestLockedThirdPersonCameraCompositionSettings
    {
        public CharacterTestLockedThirdPersonCameraCompositionSettings(
            float baseDistance,
            float minDistance,
            float maxDistance,
            float fieldOfView,
            float aspectRatio,
            float targetScreenY,
            float targetPadding,
            float fallbackYaw,
            float fallbackPitch)
        {
            BaseDistance = baseDistance;
            MinDistance = minDistance;
            MaxDistance = maxDistance;
            FieldOfView = fieldOfView;
            AspectRatio = aspectRatio;
            TargetScreenY = targetScreenY;
            TargetPadding = targetPadding;
            FallbackYaw = fallbackYaw;
            FallbackPitch = fallbackPitch;
        }

        public float BaseDistance { get; }
        public float MinDistance { get; }
        public float MaxDistance { get; }
        public float FieldOfView { get; }
        public float AspectRatio { get; }
        public float TargetScreenY { get; }
        public float TargetPadding { get; }
        public float FallbackYaw { get; }
        public float FallbackPitch { get; }
    }

    internal static class CharacterTestLockedThirdPersonCameraComposer
    {
        private const float PlayerFeetMinScreenY = 0.08f;
        private const float PlayerCenterMinScreenY = 0.26f;
        private const float TargetHeightRatio = 0.62f;
        private const float PlayerFeetHeightRatio = 0.08f;
        private const float PlayerCenterHeightRatio = 0.55f;
        private const float PrimaryFocusPitchAdjustmentLimit = 12f;
        private const int SearchSteps = 10;

        public static CharacterTestLockedThirdPersonCameraComposition Compose(
            in CharacterTestCameraTargetPresentation player,
            in CharacterTestCameraTargetPresentation target,
            in CharacterTestLockedThirdPersonCameraCompositionSettings settings)
        {
            if (player.Settings.ExplicitProfile)
                return ComposePrimaryFocused(player, target, settings);

            bool useProjectedBounds = player.Settings.ExplicitProfile || target.Settings.ExplicitProfile;
            Vector3 playerExtents = ResolveLockedFramingExtents(player);
            Vector3 targetExtents = ResolveLockedFramingExtents(target);
            Vector3 playerBoundsCenter = ResolveBoundsCenter(player, playerExtents, useProjectedBounds);
            Vector3 targetBoundsCenter = ResolveBoundsCenter(target, targetExtents, useProjectedBounds);
            Vector3 playerGround = useProjectedBounds
                ? player.RootPosition
                : player.BoundsCenter;
            Vector3 targetGround = useProjectedBounds
                ? target.RootPosition
                : target.BoundsCenter;
            Vector3 flatToTarget = targetBoundsCenter - playerBoundsCenter;
            flatToTarget.y = 0f;
            float separation = flatToTarget.magnitude;
            if (separation <= 0.001f)
            {
                return new CharacterTestLockedThirdPersonCameraComposition(
                    settings.FallbackYaw,
                    Mathf.Clamp(settings.FallbackPitch, -80f, 80f),
                    Mathf.Max(0.01f, settings.BaseDistance),
                    player.Settings.ExplicitProfile ? Vector3.zero : player.Settings.FocusOffset);
            }

            Vector3 targetDirection = flatToTarget / separation;
            float yaw = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;
            float fieldOfView = Mathf.Clamp(settings.FieldOfView, 20f, 80f);
            float tanHalfFov = Mathf.Tan(fieldOfView * Mathf.Deg2Rad * 0.5f);
            float tanHalfHorizontalFov = tanHalfFov * Mathf.Max(0.1f, settings.AspectRatio);
            float playerHeight = ResolveCharacterViewHeight(playerExtents);
            float targetHeight = ResolveCharacterViewHeight(targetExtents);
            float actorRadius = Mathf.Max(
                ResolveCharacterViewRadius(playerExtents),
                ResolveCharacterViewRadius(targetExtents));
            Vector3 playerFeetAnchor = playerGround + new Vector3(0f, playerHeight * PlayerFeetHeightRatio, 0f);
            Vector3 playerCenterAnchor = playerGround + new Vector3(0f, playerHeight * PlayerCenterHeightRatio, 0f);
            Vector3 targetAnchor = targetGround + new Vector3(0f, targetHeight * TargetHeightRatio, 0f);
            float cameraHeight = ResolveCameraHeight(playerHeight, separation);
            float backDistance;
            float pitch;
            if (useProjectedBounds)
            {
                backDistance = ResolveProjectedMinimumBackDistance(
                    playerGround,
                    targetDirection,
                    yaw,
                    cameraHeight,
                    playerBoundsCenter,
                    playerExtents,
                    targetBoundsCenter,
                    targetExtents,
                    targetAnchor,
                    tanHalfFov,
                    tanHalfHorizontalFov,
                    settings,
                    out pitch);
            }
            else
            {
                backDistance = ResolveLegacyMinimumBackDistance(
                    playerGround,
                    targetDirection,
                    cameraHeight,
                    playerHeight,
                    actorRadius,
                    separation,
                    playerFeetAnchor,
                    playerCenterAnchor,
                    targetAnchor,
                    tanHalfFov,
                    settings,
                    out pitch);
            }
            Vector3 cameraPosition = playerGround - targetDirection * backDistance + Vector3.up * cameraHeight;
            Vector3 forward = DirectionFromYawPitch(yaw, pitch);
            float distance = Mathf.Max(0.01f, Vector3.Dot(targetAnchor - cameraPosition, forward));
            distance = Mathf.Clamp(distance, Mathf.Max(0.01f, settings.MinDistance), Mathf.Max(settings.MinDistance, settings.MaxDistance));
            Vector3 focus = cameraPosition + forward * distance;
            Vector3 worldOffset = focus - player.Focus;
            return new CharacterTestLockedThirdPersonCameraComposition(yaw, pitch, distance, worldOffset);
        }

        private static CharacterTestLockedThirdPersonCameraComposition ComposePrimaryFocused(
            in CharacterTestCameraTargetPresentation player,
            in CharacterTestCameraTargetPresentation target,
            in CharacterTestLockedThirdPersonCameraCompositionSettings settings)
        {
            Vector3 playerFocus = ResolveConfiguredFocus(player);
            Vector3 playerExtents = ResolveLockedFramingExtents(player);
            Vector3 targetExtents = ResolveLockedFramingExtents(target);
            Vector3 targetBoundsCenter = ResolveBoundsCenter(target, targetExtents, true);
            Vector3 flatToTarget = targetBoundsCenter - playerFocus;
            flatToTarget.y = 0f;
            float separation = flatToTarget.magnitude;
            Vector3 worldOffset = playerFocus - player.BoundsCenter;
            if (separation <= 0.001f)
            {
                return new CharacterTestLockedThirdPersonCameraComposition(
                    settings.FallbackYaw,
                    Mathf.Clamp(settings.FallbackPitch, -80f, 80f),
                    Mathf.Clamp(
                        Mathf.Max(0.01f, settings.BaseDistance),
                        Mathf.Max(0.01f, settings.MinDistance),
                        Mathf.Max(settings.MinDistance, settings.MaxDistance)),
                    worldOffset);
            }

            Vector3 targetDirection = flatToTarget / separation;
            float yaw = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;
            float fieldOfView = Mathf.Clamp(settings.FieldOfView, 20f, 80f);
            float tanHalfVerticalFov = Mathf.Tan(fieldOfView * Mathf.Deg2Rad * 0.5f);
            float tanHalfHorizontalFov = tanHalfVerticalFov * Mathf.Max(0.1f, settings.AspectRatio);
            Vector3 targetAnchor = target.Settings.ExplicitProfile
                ? ResolveConfiguredFocus(target)
                : target.RootPosition + Vector3.up * (ResolveCharacterViewHeight(targetExtents) * TargetHeightRatio);
            float minDistance = Mathf.Max(0.01f, settings.MinDistance);
            float maxDistance = Mathf.Max(minDistance, settings.MaxDistance);
            float padding = Mathf.Max(0f, settings.TargetPadding);
            float distance = ResolvePrimaryFocusedMinimumDistance(
                playerFocus,
                targetDirection,
                yaw,
                player.BoundsCenter,
                playerExtents,
                targetBoundsCenter,
                targetExtents,
                targetAnchor,
                tanHalfVerticalFov,
                tanHalfHorizontalFov,
                padding,
                minDistance,
                maxDistance,
                settings.TargetScreenY,
                settings.FallbackPitch,
                out float pitch);
            return new CharacterTestLockedThirdPersonCameraComposition(
                yaw,
                pitch,
                distance,
                worldOffset);
        }

        private static float ResolvePrimaryFocusedMinimumDistance(
            Vector3 playerFocus,
            Vector3 targetDirection,
            float yaw,
            Vector3 playerBoundsCenter,
            Vector3 playerExtents,
            Vector3 targetBoundsCenter,
            Vector3 targetExtents,
            Vector3 targetAnchor,
            float tanHalfVerticalFov,
            float tanHalfHorizontalFov,
            float padding,
            float minDistance,
            float maxDistance,
            float targetScreenY,
            float fallbackPitch,
            out float resolvedPitch)
        {
            if (EvaluatePrimaryFocusedComposition(
                playerFocus,
                targetDirection,
                yaw,
                minDistance,
                playerBoundsCenter,
                playerExtents,
                targetBoundsCenter,
                targetExtents,
                targetAnchor,
                tanHalfVerticalFov,
                tanHalfHorizontalFov,
                targetScreenY,
                padding,
                fallbackPitch,
                out resolvedPitch))
            {
                return minDistance;
            }

            if (!EvaluatePrimaryFocusedComposition(
                playerFocus,
                targetDirection,
                yaw,
                maxDistance,
                playerBoundsCenter,
                playerExtents,
                targetBoundsCenter,
                targetExtents,
                targetAnchor,
                tanHalfVerticalFov,
                tanHalfHorizontalFov,
                targetScreenY,
                padding,
                fallbackPitch,
                out resolvedPitch))
            {
                return maxDistance;
            }

            float lower = minDistance;
            float upper = maxDistance;
            float best = maxDistance;
            for (int i = 0; i < SearchSteps; i++)
            {
                float middle = (lower + upper) * 0.5f;
                if (EvaluatePrimaryFocusedComposition(
                    playerFocus,
                    targetDirection,
                    yaw,
                    middle,
                    playerBoundsCenter,
                    playerExtents,
                    targetBoundsCenter,
                    targetExtents,
                    targetAnchor,
                    tanHalfVerticalFov,
                    tanHalfHorizontalFov,
                    targetScreenY,
                    padding,
                    fallbackPitch,
                    out resolvedPitch))
                {
                    best = middle;
                    upper = middle;
                }
                else
                {
                    lower = middle;
                }
            }

            EvaluatePrimaryFocusedComposition(
                playerFocus,
                targetDirection,
                yaw,
                best,
                playerBoundsCenter,
                playerExtents,
                targetBoundsCenter,
                targetExtents,
                targetAnchor,
                tanHalfVerticalFov,
                tanHalfHorizontalFov,
                targetScreenY,
                padding,
                fallbackPitch,
                out resolvedPitch);
            return best;
        }

        private static bool EvaluatePrimaryFocusedComposition(
            Vector3 playerFocus,
            Vector3 targetDirection,
            float yaw,
            float distance,
            Vector3 playerBoundsCenter,
            Vector3 playerExtents,
            Vector3 targetBoundsCenter,
            Vector3 targetExtents,
            Vector3 targetAnchor,
            float tanHalfVerticalFov,
            float tanHalfHorizontalFov,
            float targetScreenY,
            float padding,
            float fallbackPitch,
            out float pitch)
        {
            pitch = ResolvePrimaryFocusedPitch(
                playerFocus,
                targetAnchor,
                targetDirection,
                distance,
                targetScreenY,
                tanHalfVerticalFov,
                fallbackPitch);
            Vector3 forward = DirectionFromYawPitch(yaw, pitch);
            Vector3 cameraPosition = playerFocus - forward * distance;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            return FitsPrimaryHorizontalBounds(
                    cameraPosition,
                    forward,
                    right,
                    playerFocus.y,
                    playerBoundsCenter,
                    playerExtents,
                    padding,
                    tanHalfHorizontalFov)
                && FitsProjectedBounds(
                    cameraPosition,
                    forward,
                    right,
                    up,
                    targetBoundsCenter,
                    targetExtents,
                    padding,
                    tanHalfVerticalFov,
                    tanHalfHorizontalFov);
        }

        private static float ResolvePrimaryFocusedPitch(
            Vector3 playerFocus,
            Vector3 targetAnchor,
            Vector3 targetDirection,
            float distance,
            float targetScreenY,
            float tanHalfVerticalFov,
            float fallbackPitch)
        {
            float fallback = Mathf.Clamp(fallbackPitch, -80f, 80f);
            float minimumPitch = Mathf.Max(-80f, fallback - PrimaryFocusPitchAdjustmentLimit);
            float maximumPitch = Mathf.Min(80f, fallback + PrimaryFocusPitchAdjustmentLimit);
            float pitch = fallback;
            float desired = Mathf.Clamp01(targetScreenY);
            float bestPitch = pitch;
            float bestError = float.MaxValue;
            const float DerivativeStep = 0.25f;
            for (int i = 0; i < 8; i++)
            {
                float projected = ProjectPrimaryFocusedScreenY(
                    playerFocus,
                    targetAnchor,
                    targetDirection,
                    distance,
                    pitch,
                    tanHalfVerticalFov);
                float error = projected - desired;
                float absoluteError = Mathf.Abs(error);
                if (absoluteError < bestError)
                {
                    bestError = absoluteError;
                    bestPitch = pitch;
                }
                if (absoluteError <= 0.0001f)
                    break;

                float derivativePitch = Mathf.Clamp(pitch + DerivativeStep, minimumPitch, maximumPitch);
                if (Mathf.Abs(derivativePitch - pitch) <= 0.0001f)
                    derivativePitch = Mathf.Clamp(pitch - DerivativeStep, minimumPitch, maximumPitch);
                float derivativeProjection = ProjectPrimaryFocusedScreenY(
                    playerFocus,
                    targetAnchor,
                    targetDirection,
                    distance,
                    derivativePitch,
                    tanHalfVerticalFov);
                float derivative = (derivativeProjection - projected) / (derivativePitch - pitch);
                if (Mathf.Abs(derivative) <= 0.00001f)
                    break;
                pitch = Mathf.Clamp(pitch - error / derivative, minimumPitch, maximumPitch);
            }
            return bestPitch;
        }

        private static float ProjectPrimaryFocusedScreenY(
            Vector3 playerFocus,
            Vector3 worldPoint,
            Vector3 horizontalForward,
            float distance,
            float pitch,
            float tanHalfVerticalFov)
        {
            float yaw = Mathf.Atan2(horizontalForward.x, horizontalForward.z) * Mathf.Rad2Deg;
            Vector3 forward = DirectionFromYawPitch(yaw, pitch);
            Vector3 cameraPosition = playerFocus - forward * distance;
            return ProjectScreenY(
                cameraPosition,
                worldPoint,
                horizontalForward,
                pitch,
                tanHalfVerticalFov);
        }

        private static bool FitsPrimaryHorizontalBounds(
            Vector3 cameraPosition,
            Vector3 forward,
            Vector3 right,
            float focusHeight,
            Vector3 boundsCenter,
            Vector3 boundsExtents,
            float padding,
            float tanHalfHorizontalFov)
        {
            float safeTanHalfHorizontalFov = Mathf.Max(0.001f, tanHalfHorizontalFov);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = new Vector3(
                        boundsCenter.x + boundsExtents.x * x,
                        focusHeight,
                        boundsCenter.z + boundsExtents.z * z);
                    Vector3 toCorner = corner - cameraPosition;
                    float depth = Vector3.Dot(toCorner, forward);
                    if (depth <= 0.01f)
                        return false;
                    float horizontal = Mathf.Abs(Vector3.Dot(toCorner, right)) + padding;
                    if (horizontal > depth * safeTanHalfHorizontalFov)
                        return false;
                }
            }
            return true;
        }

        private static Vector3 ResolveConfiguredFocus(
            in CharacterTestCameraTargetPresentation presentation)
        {
            Vector3 offset = presentation.Settings.FocusOffset;
            if (offset.sqrMagnitude <= 0.000001f)
                return presentation.Focus;

            Quaternion yawRotation;
            if (presentation.Entity != null)
            {
                yawRotation = Quaternion.Euler(0f, presentation.Entity.Transform.YawDegrees, 0f);
            }
            else
            {
                Vector3 flatForward = presentation.Forward;
                flatForward.y = 0f;
                yawRotation = flatForward.sqrMagnitude > 0.000001f
                    ? Quaternion.LookRotation(flatForward.normalized, Vector3.up)
                    : Quaternion.identity;
            }
            return presentation.Focus + yawRotation * offset;
        }

        private static Vector3 ResolveBoundsCenter(
            in CharacterTestCameraTargetPresentation presentation,
            Vector3 extents,
            bool useProjectedBounds)
        {
            if (!useProjectedBounds || presentation.Settings.ExplicitProfile)
                return presentation.BoundsCenter;

            return presentation.BoundsCenter + Vector3.up * extents.y;
        }

        private static Vector3 ResolveLockedFramingExtents(
            in CharacterTestCameraTargetPresentation presentation)
        {
            Vector3 extents = SanitizeExtents(presentation.BoundsExtents);
            if (!presentation.Settings.ExplicitProfile)
                return extents;

            float scale = Mathf.Clamp(presentation.Settings.LockedFramingScale, 0.25f, 1f);
            return extents * scale;
        }

        private static float ResolveCharacterViewHeight(Vector3 boundsExtents)
        {
            return Mathf.Max(0.25f, SanitizeExtents(boundsExtents).y * 2f);
        }

        private static float ResolveCharacterViewRadius(Vector3 boundsExtents)
        {
            Vector3 extents = SanitizeExtents(boundsExtents);
            return Mathf.Max(extents.x, extents.z);
        }

        private static float ResolveCameraHeight(float playerHeight, float separation)
        {
            return playerHeight * 1.35f + Mathf.Min(playerHeight * 0.65f, separation * 0.035f);
        }

        private static float ResolveLegacyMinimumBackDistance(
            Vector3 playerGround,
            Vector3 targetDirection,
            float cameraHeight,
            float playerHeight,
            float actorRadius,
            float separation,
            Vector3 playerFeetAnchor,
            Vector3 playerCenterAnchor,
            Vector3 targetAnchor,
            float tanHalfFov,
            in CharacterTestLockedThirdPersonCameraCompositionSettings settings,
            out float resolvedPitch)
        {
            float lower = Mathf.Max(settings.MinDistance, playerHeight * 1.15f + actorRadius * 1.5f);
            float upper = Mathf.Max(settings.BaseDistance, lower);
            resolvedPitch = 0f;
            float maxDistance = Mathf.Max(settings.MaxDistance, separation + playerHeight * 3f);

            while (!EvaluateComposition(
                playerGround,
                targetDirection,
                cameraHeight,
                upper,
                playerFeetAnchor,
                playerCenterAnchor,
                targetAnchor,
                tanHalfFov,
                settings.TargetScreenY,
                out resolvedPitch)
                && upper < maxDistance)
            {
                upper = Mathf.Min(maxDistance, upper + Mathf.Max(playerHeight * 0.5f, separation * 0.08f));
            }

            if (upper <= lower)
                return upper;

            float best = upper;
            for (int i = 0; i < SearchSteps; i++)
            {
                float middle = (lower + upper) * 0.5f;
                if (EvaluateComposition(
                    playerGround,
                    targetDirection,
                    cameraHeight,
                    middle,
                    playerFeetAnchor,
                    playerCenterAnchor,
                    targetAnchor,
                    tanHalfFov,
                    settings.TargetScreenY,
                    out resolvedPitch))
                {
                    best = middle;
                    upper = middle;
                }
                else
                {
                    lower = middle;
                }
            }

            EvaluateComposition(
                playerGround,
                targetDirection,
                cameraHeight,
                best,
                playerFeetAnchor,
                playerCenterAnchor,
                targetAnchor,
                tanHalfFov,
                settings.TargetScreenY,
                out resolvedPitch);
            return best;
        }

        private static float ResolveProjectedMinimumBackDistance(
            Vector3 playerGround,
            Vector3 targetDirection,
            float yaw,
            float cameraHeight,
            Vector3 playerBoundsCenter,
            Vector3 playerExtents,
            Vector3 targetBoundsCenter,
            Vector3 targetExtents,
            Vector3 targetAnchor,
            float tanHalfVerticalFov,
            float tanHalfHorizontalFov,
            in CharacterTestLockedThirdPersonCameraCompositionSettings settings,
            out float resolvedPitch)
        {
            float lower = Mathf.Max(0.01f, settings.MinDistance);
            float upper = Mathf.Max(lower, settings.MaxDistance);
            float padding = Mathf.Max(0f, settings.TargetPadding);
            if (EvaluateProjectedComposition(
                playerGround,
                targetDirection,
                yaw,
                cameraHeight,
                lower,
                playerBoundsCenter,
                playerExtents,
                targetBoundsCenter,
                targetExtents,
                targetAnchor,
                tanHalfVerticalFov,
                tanHalfHorizontalFov,
                settings.TargetScreenY,
                padding,
                out resolvedPitch))
            {
                return lower;
            }

            if (!EvaluateProjectedComposition(
                playerGround,
                targetDirection,
                yaw,
                cameraHeight,
                upper,
                playerBoundsCenter,
                playerExtents,
                targetBoundsCenter,
                targetExtents,
                targetAnchor,
                tanHalfVerticalFov,
                tanHalfHorizontalFov,
                settings.TargetScreenY,
                padding,
                out resolvedPitch))
            {
                return upper;
            }

            float best = upper;
            for (int i = 0; i < SearchSteps; i++)
            {
                float middle = (lower + upper) * 0.5f;
                if (EvaluateProjectedComposition(
                    playerGround,
                    targetDirection,
                    yaw,
                    cameraHeight,
                    middle,
                    playerBoundsCenter,
                    playerExtents,
                    targetBoundsCenter,
                    targetExtents,
                    targetAnchor,
                    tanHalfVerticalFov,
                    tanHalfHorizontalFov,
                    settings.TargetScreenY,
                    padding,
                    out resolvedPitch))
                {
                    best = middle;
                    upper = middle;
                }
                else
                {
                    lower = middle;
                }
            }

            EvaluateProjectedComposition(
                playerGround,
                targetDirection,
                yaw,
                cameraHeight,
                best,
                playerBoundsCenter,
                playerExtents,
                targetBoundsCenter,
                targetExtents,
                targetAnchor,
                tanHalfVerticalFov,
                tanHalfHorizontalFov,
                settings.TargetScreenY,
                padding,
                out resolvedPitch);
            return best;
        }

        private static bool EvaluateProjectedComposition(
            Vector3 playerGround,
            Vector3 targetDirection,
            float yaw,
            float cameraHeight,
            float backDistance,
            Vector3 playerBoundsCenter,
            Vector3 playerExtents,
            Vector3 targetBoundsCenter,
            Vector3 targetExtents,
            Vector3 targetAnchor,
            float tanHalfVerticalFov,
            float tanHalfHorizontalFov,
            float targetScreenY,
            float padding,
            out float pitch)
        {
            Vector3 cameraPosition = playerGround - targetDirection * backDistance + Vector3.up * cameraHeight;
            pitch = ResolvePitchForScreenY(
                cameraPosition,
                targetAnchor,
                targetDirection,
                targetScreenY,
                tanHalfVerticalFov);
            Vector3 forward = DirectionFromYawPitch(yaw, pitch);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            return FitsProjectedBounds(
                    cameraPosition,
                    forward,
                    right,
                    up,
                    playerBoundsCenter,
                    playerExtents,
                    padding,
                    tanHalfVerticalFov,
                    tanHalfHorizontalFov)
                && FitsProjectedBounds(
                    cameraPosition,
                    forward,
                    right,
                    up,
                    targetBoundsCenter,
                    targetExtents,
                    padding,
                    tanHalfVerticalFov,
                    tanHalfHorizontalFov);
        }

        private static bool FitsProjectedBounds(
            Vector3 cameraPosition,
            Vector3 forward,
            Vector3 right,
            Vector3 up,
            Vector3 boundsCenter,
            Vector3 boundsExtents,
            float padding,
            float tanHalfVerticalFov,
            float tanHalfHorizontalFov)
        {
            float safeTanHalfHorizontalFov = Mathf.Max(0.001f, tanHalfHorizontalFov);
            float safeTanHalfVerticalFov = Mathf.Max(0.001f, tanHalfVerticalFov);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = boundsCenter + new Vector3(
                            boundsExtents.x * x,
                            boundsExtents.y * y,
                            boundsExtents.z * z);
                        Vector3 toCorner = corner - cameraPosition;
                        float depth = Vector3.Dot(toCorner, forward);
                        if (depth <= 0.01f)
                            return false;

                        float horizontal = Mathf.Abs(Vector3.Dot(toCorner, right)) + padding;
                        float vertical = Mathf.Abs(Vector3.Dot(toCorner, up)) + padding;
                        if (horizontal > depth * safeTanHalfHorizontalFov
                            || vertical > depth * safeTanHalfVerticalFov)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static bool EvaluateComposition(
            Vector3 playerGround,
            Vector3 targetDirection,
            float cameraHeight,
            float backDistance,
            Vector3 playerFeetAnchor,
            Vector3 playerCenterAnchor,
            Vector3 targetAnchor,
            float tanHalfFov,
            float targetScreenY,
            out float pitch)
        {
            Vector3 cameraPosition = playerGround - targetDirection * backDistance + Vector3.up * cameraHeight;
            pitch = ResolvePitchForScreenY(
                cameraPosition,
                targetAnchor,
                targetDirection,
                targetScreenY,
                tanHalfFov);
            float playerFeetScreenY = ProjectScreenY(
                cameraPosition,
                playerFeetAnchor,
                targetDirection,
                pitch,
                tanHalfFov);
            float playerCenterScreenY = ProjectScreenY(
                cameraPosition,
                playerCenterAnchor,
                targetDirection,
                pitch,
                tanHalfFov);
            return playerFeetScreenY >= PlayerFeetMinScreenY
                && playerCenterScreenY >= PlayerCenterMinScreenY;
        }

        private static float ResolvePitchForScreenY(
            Vector3 cameraPosition,
            Vector3 worldPoint,
            Vector3 horizontalForward,
            float screenY,
            float tanHalfFov)
        {
            Vector3 toPoint = worldPoint - cameraPosition;
            float horizontalDistance = Mathf.Max(0.001f, Vector3.Dot(toPoint, horizontalForward));
            float verticalDistance = toPoint.y;
            float pointAngle = Mathf.Atan2(verticalDistance, horizontalDistance);
            float desiredOffset = Mathf.Atan((Mathf.Clamp01(screenY) * 2f - 1f) * tanHalfFov);
            float forwardAngle = pointAngle - desiredOffset;
            return Mathf.Clamp(-forwardAngle * Mathf.Rad2Deg, -80f, 80f);
        }

        private static float ProjectScreenY(
            Vector3 cameraPosition,
            Vector3 worldPoint,
            Vector3 horizontalForward,
            float pitchDegrees,
            float tanHalfFov)
        {
            Vector3 toPoint = worldPoint - cameraPosition;
            float horizontalDistance = Mathf.Max(0.001f, Vector3.Dot(toPoint, horizontalForward));
            float verticalDistance = toPoint.y;
            float pointAngle = Mathf.Atan2(verticalDistance, horizontalDistance);
            float forwardAngle = -pitchDegrees * Mathf.Deg2Rad;
            float normalizedY = Mathf.Tan(pointAngle - forwardAngle) / Mathf.Max(0.001f, tanHalfFov);
            return 0.5f + normalizedY * 0.5f;
        }

        private static Vector3 DirectionFromYawPitch(float yawDegrees, float pitchDegrees)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            float pitch = pitchDegrees * Mathf.Deg2Rad;
            return new Vector3(
                Mathf.Sin(yaw) * Mathf.Cos(pitch),
                -Mathf.Sin(pitch),
                Mathf.Cos(yaw) * Mathf.Cos(pitch)).normalized;
        }

        private static Vector3 SanitizeExtents(Vector3 extents)
        {
            return new Vector3(
                Mathf.Max(0.01f, extents.x),
                Mathf.Max(0.01f, extents.y),
                Mathf.Max(0.01f, extents.z));
        }
    }
}

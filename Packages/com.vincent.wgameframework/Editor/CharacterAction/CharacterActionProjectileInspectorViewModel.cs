using System;
using System.Globalization;
using MxFramework.Combat.Projectiles;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed class CharacterActionProjectileInspectorViewModel
    {
        private CharacterActionProjectileInspectorViewModel()
        {
        }

        public string DisplayName { get; private set; }
        public string TypeLabel { get; private set; }
        public string Summary { get; private set; }
        public string ChoiceLabel { get; private set; }
        public string RangeLabel { get; private set; }
        public string BallisticMaxRangeLabel { get; private set; }
        public string BallisticLaunchAngleLabel { get; private set; }
        public string FlightTimeLabel { get; private set; }
        public string GravityLabel { get; private set; }
        public string RadiusLabel { get; private set; }
        public string DamageLabel { get; private set; }
        public string PhysicalEventsLabel { get; private set; }
        public string FlightPrefabLabel { get; private set; }
        public string AimLabel { get; private set; }
        public string AimTargetPointLabel { get; private set; }
        public string MotionLabel { get; private set; }
        public string SpeedProfileLabel { get; private set; }
        public string HomingTurnLabel { get; private set; }
        public string SyncBadgeText { get; private set; }
        public string SyncBadgeClass { get; private set; }
        public bool IsAreaPolicy { get; private set; }
        public bool IsPiercing { get; private set; }
        public bool IsBallisticMotion { get; private set; }
        public float RangeMeters { get; private set; }
        public float BallisticMaxRangeMeters { get; private set; }

        public static CharacterActionProjectileInspectorViewModel Create(
            CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record)
        {
            record = record ?? throw new ArgumentNullException(nameof(record));

            float designRange = EstimateRange(record.SpeedPerFrame, record.LifetimeFrames);
            bool isBallistic = IsBallisticMotionKind(record.MotionKind);
            float ballisticMaxRange = EstimateBallisticMaxRange(
                record.SpeedPerFrame,
                record.GravityPerFrameSquared,
                record.MaxLaunchAngleTangent);
            float displayRange = isBallistic
                ? ballisticMaxRange
                : designRange;

            string type = FormatProjectileType(record.PolicyKind, record.StopOnFirstAcceptedImpact, record.MaxAcceptedImpacts);
            string displayRangeText = FormatMeters(displayRange);
            string designRangeText = FormatMeters(designRange);
            string ballisticMaxRangeText = FormatMeters(ballisticMaxRange);
            string radiusText = FormatMeters(record.Radius);
            string damageText = FormatDamage(record);
            string physicalEventsText = record.PhysicalEventsEnabled ? "physical events on" : "physical events off";
            string prefab = FormatPrefab(record);
            string aim = FormatAimKind(record.AimKind);
            string motion = FormatMotionKind(record.MotionKind);
            string speedProfile = FormatSpeedProfileKind(record.SpeedProfileKind);
            string flightTimeText = Math.Max(1, record.LifetimeFrames).ToString(CultureInfo.InvariantCulture) + "f max";
            string gravityLabel = FormatFloat(record.GravityPerFrameSquared) + " m/frame^2";
            string launchAngleLabel = FormatLaunchAngleStrategy(record.MaxLaunchAngleDegrees);
            string gravityText = isBallistic ? "game gravity " + gravityLabel : string.Empty;
            string homingTurn = IsHomingMotion(record.MotionKind)
                ? "turn response " + FormatFloat(record.HomingTurnRatePerFrame <= 0f ? 1f : record.HomingTurnRatePerFrame)
                : string.Empty;
            string aimTargetPoint = IsLockedAim(record.AimKind)
                ? "locked target center; invalid target falls back forward " + displayRangeText
                : "origin + caster forward * " + displayRangeText;
            string rangeSummary = isBallistic
                ? " / max initial speed " + FormatSpeed(record.SpeedPerFrame)
                    + " / gravity " + gravityLabel
                    + " / ballistic max " + ballisticMaxRangeText
                    + " / flight cap " + flightTimeText
                    + " / angle " + launchAngleLabel
                : " / range " + designRangeText;

            return new CharacterActionProjectileInspectorViewModel
            {
                DisplayName = record.DisplayName,
                TypeLabel = type,
                RangeMeters = displayRange,
                BallisticMaxRangeMeters = ballisticMaxRange,
                RangeLabel = displayRangeText,
                BallisticMaxRangeLabel = ballisticMaxRangeText,
                BallisticLaunchAngleLabel = launchAngleLabel,
                FlightTimeLabel = flightTimeText,
                GravityLabel = gravityLabel,
                RadiusLabel = radiusText,
                DamageLabel = damageText,
                PhysicalEventsLabel = physicalEventsText,
                FlightPrefabLabel = prefab,
                AimLabel = aim,
                AimTargetPointLabel = aimTargetPoint,
                MotionLabel = motion,
                SpeedProfileLabel = speedProfile,
                HomingTurnLabel = homingTurn,
                Summary = "Projectile: " + record.DisplayName
                    + " / " + type
                    + " / " + motion
                    + (string.IsNullOrWhiteSpace(gravityText) ? string.Empty : " / " + gravityText)
                    + " / " + speedProfile
                    + (string.IsNullOrWhiteSpace(homingTurn) ? string.Empty : " / " + homingTurn)
                    + " / " + aim
                    + rangeSummary
                    + " / radius " + radiusText
                    + " / " + physicalEventsText
                    + " / " + damageText,
                ChoiceLabel = record.DisplayName
                    + " - " + type
                    + " - " + motion
                    + (string.IsNullOrWhiteSpace(gravityText) ? string.Empty : " - " + gravityText)
                    + " - " + speedProfile
                    + (string.IsNullOrWhiteSpace(homingTurn) ? string.Empty : " - " + homingTurn)
                    + " - " + aim
                    + " - " + (isBallistic ? "ballistic max " : string.Empty) + displayRangeText
                    + " - " + damageText,
                SyncBadgeText = ResolveSyncBadgeText(record),
                SyncBadgeClass = ResolveSyncBadgeClass(record),
                IsAreaPolicy = string.Equals(record.PolicyKind, "AreaPulseOnImpact", StringComparison.Ordinal),
                IsPiercing = !record.StopOnFirstAcceptedImpact && record.MaxAcceptedImpacts > 1,
                IsBallisticMotion = isBallistic,
            };
        }

        public static float EstimateRange(float speedPerFrame, int lifetimeFrames)
        {
            return Math.Max(0f, speedPerFrame) * Math.Max(0, lifetimeFrames);
        }

        public static float EstimateBallisticMaxRange(
            float speedPerFrame,
            float gravityPerFrameSquared,
            float maxLaunchAngleTangent = 0f)
        {
            if (speedPerFrame <= 0f || gravityPerFrameSquared <= 0f)
                return 0f;

            float unconstrained = speedPerFrame * speedPerFrame / gravityPerFrameSquared;
            if (maxLaunchAngleTangent <= 0f || maxLaunchAngleTangent >= 1f)
                return unconstrained;

            float tangentSquared = maxLaunchAngleTangent * maxLaunchAngleTangent;
            float factor = 2f * maxLaunchAngleTangent / (1f + tangentSquared);
            return unconstrained * factor;
        }

        public static string FormatMeters(float value)
        {
            return FormatFloat(value) + "m";
        }

        public static string FormatSpeed(float value)
        {
            return FormatFloat(value) + " m/frame";
        }

        public static string FormatProjectileType(string policyKind, bool stopOnFirstAcceptedImpact, int maxAcceptedImpacts)
        {
            if (string.Equals(policyKind, "AreaPulseOnImpact", StringComparison.Ordinal))
                return "Area pulse";
            if (!stopOnFirstAcceptedImpact && maxAcceptedImpacts > 1)
                return "Piercing x" + maxAcceptedImpacts.ToString(CultureInfo.InvariantCulture);
            return "Direct hit";
        }

        public static string FormatImpactPolicy(string policyKind)
        {
            if (string.Equals(policyKind, "AreaPulseOnImpact", StringComparison.Ordinal))
                return "Area pulse (AreaPulseOnImpact)";
            if (string.Equals(policyKind, "DirectHit", StringComparison.Ordinal))
                return "Direct hit (DirectHit)";
            return string.IsNullOrWhiteSpace(policyKind) ? "unset" : policyKind;
        }

        public static string FormatMotionKind(string value)
        {
            if (string.Equals(value, CombatProjectileMotionKind.ArcToTarget.ToString(), StringComparison.Ordinal))
                return "Designed Arc (ArcToTarget)";
            if (string.Equals(value, CombatProjectileMotionKind.BallisticArcToTarget.ToString(), StringComparison.Ordinal))
                return "Ballistic Arc / Max Initial Speed";
            if (string.Equals(value, CombatProjectileMotionKind.BezierToTarget.ToString(), StringComparison.Ordinal))
                return "Bezier";
            if (string.Equals(value, CombatProjectileMotionKind.HomingToTarget.ToString(), StringComparison.Ordinal))
                return "Homing";
            if (string.Equals(value, CombatProjectileMotionKind.BoomerangReturn.ToString(), StringComparison.Ordinal))
                return "Boomerang return";
            if (string.Equals(value, CombatProjectileMotionKind.SwayForward.ToString(), StringComparison.Ordinal))
                return "Sway forward";
            if (string.Equals(value, CombatProjectileMotionKind.Linear.ToString(), StringComparison.Ordinal))
                return "Linear";
            return string.IsNullOrWhiteSpace(value) ? "Linear" : value;
        }

        public static string FormatSpeedProfileKind(string value)
        {
            if (string.Equals(value, CombatProjectileSpeedProfileKind.EaseInQuadratic.ToString(), StringComparison.Ordinal))
                return "Ease in";
            if (string.Equals(value, CombatProjectileSpeedProfileKind.EaseOutQuadratic.ToString(), StringComparison.Ordinal))
                return "Ease out";
            if (string.Equals(value, CombatProjectileSpeedProfileKind.EaseInOutSmoothstep.ToString(), StringComparison.Ordinal))
                return "Ease in/out";
            if (string.Equals(value, CombatProjectileSpeedProfileKind.Constant.ToString(), StringComparison.Ordinal))
                return "Constant";
            return string.IsNullOrWhiteSpace(value) ? "Constant" : value;
        }

        public static string FormatSameTargetPolicy(string value)
        {
            if (string.Equals(value, "OncePerProjectile", StringComparison.Ordinal))
                return "Once per projectile";
            if (string.Equals(value, "AllowRepeat", StringComparison.Ordinal))
                return "Allow repeat";
            return string.IsNullOrWhiteSpace(value) ? "unset" : value;
        }

        public static string FormatAimKind(string value)
        {
            if (string.Equals(value, CombatProjectileAimKind.LockedTargetOrCasterForwardRange.ToString(), StringComparison.Ordinal))
                return "Locked target preferred, forward fallback";
            if (string.Equals(value, CombatProjectileAimKind.LockedTargetCenter.ToString(), StringComparison.Ordinal))
                return "Locked target center";
            if (string.Equals(value, CombatProjectileAimKind.CasterForwardRange.ToString(), StringComparison.Ordinal))
                return "Caster forward";
            return string.IsNullOrWhiteSpace(value) ? "unset" : value;
        }

        public static int LifetimeFramesFromRange(float speedPerFrame, float rangeMeters)
        {
            if (speedPerFrame <= 0f)
                return 1;
            return Math.Max(1, (int)Math.Round(Math.Max(0.01f, rangeMeters) / speedPerFrame));
        }

        private static string FormatPrefab(
            CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.PrefabResourceId))
                return "unset";

            string suffix = string.IsNullOrWhiteSpace(record.PrefabResourcePackageId)
                ? string.Empty
                : " / " + record.PrefabResourcePackageId;
            return record.PrefabResourceId + " (" + EmptyAs(record.PrefabResourceTypeId, "GameObject") + suffix + ")";
        }

        private static string FormatDamage(
            CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record)
        {
            return CharacterActionDamageAuthoringText.FormatDamageSummary(
                record.DamageTypeId,
                record.BaseDamage,
                record.ImpactPower,
                launchPower: record.LaunchPower);
        }

        private static string ResolveSyncBadgeText(
            CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record)
        {
            if (!record.HasSourceDefinition)
                return "readonly fallback";
            if (record.IsLegacyV1PresentationBinding && record.GeneratedInSync)
                return "compatibility / legacy v1";
            return record.GeneratedInSync ? "synced" : "stale";
        }

        private static string ResolveSyncBadgeClass(
            CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record)
        {
            if (!record.HasSourceDefinition)
                return "ca-projectile-badge--fallback";
            if (record.IsLegacyV1PresentationBinding && record.GeneratedInSync)
                return "ca-projectile-badge--compatibility";
            return record.GeneratedInSync ? "ca-projectile-badge--synced" : "ca-projectile-badge--stale";
        }

        private static string EmptyAs(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string FormatLaunchAngleStrategy(float degrees)
        {
            return degrees <= 0f
                ? "auto range-optimal (same-height 45 deg)"
                : "cap " + FormatFloat(degrees) + " deg";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static bool IsLockedAim(string value)
        {
            return string.Equals(value, CombatProjectileAimKind.LockedTargetCenter.ToString(), StringComparison.Ordinal)
                || string.Equals(value, CombatProjectileAimKind.LockedTargetOrCasterForwardRange.ToString(), StringComparison.Ordinal);
        }

        private static bool IsHomingMotion(string value)
        {
            return string.Equals(value, CombatProjectileMotionKind.HomingToTarget.ToString(), StringComparison.Ordinal);
        }

        private static bool IsBallisticMotionKind(string value)
        {
            return string.Equals(value, CombatProjectileMotionKind.BallisticArcToTarget.ToString(), StringComparison.Ordinal);
        }
    }
}

using System;
using System.Linq;
using System.Globalization;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>All spatial values are explicit maker inputs, never published defaults.</summary>
    internal sealed class CharacterTestTacticalBehindTarget
    {
        public bool Maintain { get; set; }
        public float HalfAngleDegrees { get; set; }
        public float ResumeHalfAngleDegrees { get; set; }
        public float DistanceTolerance { get; set; }
        public float ResumeDistanceTolerance { get; set; }
        // Maker supplied angular hysteresis around the opposite-side tie; not a product direction option.
        public float DirectionSwitchHysteresisDegrees { get; set; }
        internal void Validate(CharacterTestRuntimeAiLabMovementStrategy strategy,
            CharacterTestRuntimeAiLabMovementTargetKind target, int side)
        {
            bool finite = !float.IsNaN(HalfAngleDegrees) && !float.IsInfinity(HalfAngleDegrees)
                && !float.IsNaN(ResumeHalfAngleDegrees) && !float.IsInfinity(ResumeHalfAngleDegrees)
                && !float.IsNaN(DistanceTolerance) && !float.IsInfinity(DistanceTolerance)
                && !float.IsNaN(ResumeDistanceTolerance) && !float.IsInfinity(ResumeDistanceTolerance)
                && !float.IsNaN(DirectionSwitchHysteresisDegrees) && !float.IsInfinity(DirectionSwitchHysteresisDegrees);
            if (!finite || strategy != CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget
                || target != CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget
                || side != 0 || HalfAngleDegrees <= 0 || HalfAngleDegrees >= 90
                || ResumeHalfAngleDegrees < HalfAngleDegrees || ResumeHalfAngleDegrees >= 90
                || DistanceTolerance < 0 || ResumeDistanceTolerance < DistanceTolerance
                || DirectionSwitchHysteresisDegrees < 0 || DirectionSwitchHysteresisDegrees >= 90)
                throw new ArgumentException("BehindTargetInvalid: current-target shortest orbit, rear region, tolerances and direction hysteresis are required.");
        }
    }

    /// <summary>Persisted movement authoring, resolved once into the shared execution table.</summary>
    internal sealed class CharacterTestTacticalMovement
    {
        public CharacterTestRuntimeAiLabMovementStrategy Strategy { get; set; }
        public CharacterTestRuntimeAiLabMovementTargetKind Target { get; set; }
        public int EntityId { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public string DistanceSource { get; set; } = "Manual";
        public string ActionSlotId { get; set; } = string.Empty;
        public float Minimum { get; set; } = 1.5f;
        public float Maximum { get; set; } = 3f;
        // 0 keeps the deterministic automatic side; +1 is left, -1 is right.
        public int Side { get; set; }
        public float RadialWeight { get; set; } = 0.65f;
        // Maker-only extension. Absence preserves every existing orbit and published hash.
        public CharacterTestTacticalBehindTarget BehindTarget { get; set; }
        public bool ShouldSerializeBehindTarget() => BehindTarget != null;

        internal static string Label(CharacterTestRuntimeAiLabMovementStrategy strategy) => strategy switch {
            CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget => "接近目标",
            CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance => "保持距离",
            CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard => "远离目标",
            CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked => "横向移动",
            CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose => "过近后撤",
            CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget => "环绕目标", _ => "未知移动" };

        internal bool UsesDistance => Strategy != CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard
            && Strategy != CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked;
        internal bool UsesBand => Strategy == CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance
            || Strategy == CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget;
        internal bool UsesSide => BehindTarget == null && (Strategy == CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget
            || Strategy == CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked);
        public bool ShouldSerializeUsesDistance() => false;
        public bool ShouldSerializeUsesBand() => false;
        public bool ShouldSerializeUsesSide() => false;

        internal CharacterTestBehaviorExecutionMovement Compile(CharacterTestTacticalAuthoringSource source,
            CharacterTestTacticalCardInputs inputs, float speed, string eventId)
        {
            Require(Enum.IsDefined(typeof(CharacterTestRuntimeAiLabMovementStrategy), Strategy)
                && Enum.IsDefined(typeof(CharacterTestRuntimeAiLabMovementTargetKind), Target), "移动方式或目标无效。");
            Require(EntityId >= 0 && Finite(X) && Finite(Z), "移动目标参数无效。");
            Require(Target == CharacterTestRuntimeAiLabMovementTargetKind.Entity || EntityId == 0, "只有指定单位可以填写单位编号。");
            Require(Target == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition || X == 0f && Z == 0f, "只有指定位置可以填写坐标。");
            Require(Finite(Minimum) && Finite(Maximum) && Minimum >= 0 && Maximum >= Minimum, "距离必须非负，且最大距离不小于最小距离。");
            Require(Side >= -1 && Side <= 1 && (UsesSide || Side == 0), "左右方向只用于横移或环绕。");
            Require(Finite(RadialWeight) && RadialWeight >= 0 && RadialWeight <= 4
                && (Strategy == CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget || RadialWeight == 0.65f), "径向修正只用于环绕，范围为 0～4。");
            Require(Finite(speed) && speed >= 0.1f && speed <= 1f, "速度倍率范围为 0.1～1。");
            Require(DistanceSource == "Manual" || DistanceSource == "ActionBand" || DistanceSource == "ActionOptimal", "未知距离来源。");
            BehindTarget?.Validate(Strategy, Target, Side);
            float min = Minimum, max = Maximum, optimal = 0;
            bool point = DistanceSource == "ActionOptimal";
            if (DistanceSource != "Manual")
            {
                Require(UsesDistance && Target == CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget,
                    "动作距离仅支持以当前战斗目标为目标的距离控制。");
                var slot = source.Slots.FirstOrDefault(s => s?.SlotId == ActionSlotId);
                Require(slot != null && !slot.IsFunctional, "动作距离需要已声明的动作槽。");
                if (inputs == null) return default; // Structural authoring validation does not prepare action data.
                var summary = inputs.SlotRange(ActionSlotId);
                min = (float)summary.MinDistance.Value;
                max = (float)summary.MaxDistance.Value;
                optimal = (float)summary.OptimalDistance.Value;
                if (point && Strategy != CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget)
                { min = Math.Max(min, optimal - 0.05f); max = Math.Min(max, optimal + 0.05f); }
                else if (!point && Strategy == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget) min = max;
            }
            else Require(string.IsNullOrEmpty(ActionSlotId), "手填距离不能同时关联动作槽。");
            var metric = Target == CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget
                ? CharacterTestBehaviorDistanceMetric.HurtboxSurfaceClearance
                : Target == CharacterTestRuntimeAiLabMovementTargetKind.Entity ? CharacterTestBehaviorDistanceMetric.RootPlanarDistance
                : CharacterTestBehaviorDistanceMetric.RootToWorldPointPlanarDistance;
            return new CharacterTestBehaviorExecutionMovement(Strategy, Target, metric, EntityId, X, Z, min, max, speed,
                CharacterTestBehaviorExecutionCompiler.ComputeSideParity(0, eventId),
                point && Strategy == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget, optimal,
                explicitSide: Side, radialWeight: RadialWeight, independentFacing: true, explicitDistance: true, requireLivingTarget: true,
                distanceTolerance: Strategy == CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance && min == max ? 0.1f : 0f,
                behindTarget: BehindTarget == null ? default : new CharacterTestBehaviorBehindTarget(BehindTarget));
        }

        internal string Describe()
        {
            string target = Target == CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget ? "当前目标"
                : Target == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition ? "位置（" + Number(X) + "，" + Number(Z) + "）"
                : EntityId == 0 ? "自动选择的队友" : "单位 " + EntityId;
            string distance = !UsesDistance ? string.Empty : DistanceSource != "Manual"
                ? "，按动作槽 " + ActionSlotId + (DistanceSource == "ActionOptimal" ? " 的最佳距离" : " 的有效距离带")
                : UsesBand ? "，距离带 " + Number(Minimum) + "～" + Number(Maximum) + " 米"
                : "，距离 " + Number(Minimum) + " 米";
            return (BehindTarget == null ? Label(Strategy) : BehindTarget.Maintain ? "绕至并保持目标背后" : "绕至目标背后") + "（" + target + distance + (BehindTarget != null ? "，较短环绕弧" : UsesSide ? Side == 0 ? "，自动方向" : Side > 0 ? "，向左" : "，向右" : "") + "）";
        }
        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool value, string message) { if (!value) throw new ArgumentException("MovementInvalid: " + message); }
    }
}

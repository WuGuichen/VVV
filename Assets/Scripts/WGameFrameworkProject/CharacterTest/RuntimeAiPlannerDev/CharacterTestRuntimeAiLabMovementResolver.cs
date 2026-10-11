using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabMovementTargetSample
    {
        public CharacterTestRuntimeAiLabMovementTargetSample(
            bool exists,
            float distance,
            Vector2 towardTarget,
            Vector2 navigationMove, bool hasTargetOrientation = false, int targetYawMilliDegrees = 0, float rootDistance = 0f,
            int targetEntityId = 0)
        {
            Exists = exists;
            Distance = distance > 0f ? distance : 0f;
            TowardTarget = Vector2.ClampMagnitude(towardTarget, 1f);
            NavigationMove = Vector2.ClampMagnitude(navigationMove, 1f);
            HasTargetOrientation = hasTargetOrientation; TargetYawMilliDegrees = targetYawMilliDegrees;
            RootDistance = rootDistance;
            TargetEntityId = targetEntityId;
            HasFiniteSpatialSample = Finite(distance) && Finite(rootDistance) && rootDistance > 0f
                && Finite(towardTarget.x) && Finite(towardTarget.y) && targetEntityId >= 0;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal bool HasFiniteSpatialSample { get; }
        internal int TargetEntityId { get; }
        public bool HasTargetOrientation { get; }
        public int TargetYawMilliDegrees { get; }
        public float RootDistance { get; }
        public bool Exists { get; }
        public float Distance { get; }
        public Vector2 TowardTarget { get; }
        public Vector2 NavigationMove { get; }
    }

    internal enum CharacterTestBehindTargetPreviewStatus { Unavailable, Moving, Arrived, Holding }
    internal readonly struct CharacterTestBehindTargetPreview
    {
        internal readonly CharacterTestBehindTargetPreviewStatus Status;
        internal readonly Vector2 Direction;
        private CharacterTestBehindTargetPreview(CharacterTestBehindTargetPreviewStatus status, Vector2 direction)
        { Status = status; Direction = direction; }
        // Only an unconditional initial block has a direction before execution. Later blocks,
        // gates and competing controls require execution facts; do not predict them from UI selection.
        internal static bool TrySelectInitialMovement(CharacterTestBehaviorExecutionProgram program,
            out CharacterTestBehaviorExecutionMovement movement)
        {
            movement = default;
            if (program == null || program.ActivationCondition >= 0) return false;
            var timeline = program.Timelines[program.EntryTimeline];
            if (timeline.EntryCondition >= 0) return false;
            int selected = -1;
            for (int i = 0; i < timeline.EventCount; i++)
            {
                int index = program.TimelineEvents[timeline.EventStart + i];
                var evt = program.Events[index];
                if (evt.StartFrame != 0) continue;
                if (evt.Condition >= 0) return false;
                if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget)
                {
                    if (selected >= 0 || !program.Movements[evt.Parameter].BehindTarget.Enabled) return false;
                    selected = evt.Parameter;
                }
                else if (evt.Kind != CharacterTestBehaviorExecutionOperations.TurnToTarget) return false;
            }
            if (selected < 0) return false;
            movement = program.Movements[selected];
            return true;
        }
        internal static CharacterTestBehindTargetPreview Create(in CharacterTestBehaviorExecutionMovement movement,
            in CharacterTestRuntimeAiLabMovementTargetSample sample)
        {
            if (!movement.BehindTarget.Enabled || !CharacterTestBehindTargetResolver.TryRearError(sample, out _)
                || float.IsNaN(sample.Distance) || float.IsInfinity(sample.Distance))
                return new CharacterTestBehindTargetPreview(CharacterTestBehindTargetPreviewStatus.Unavailable, Vector2.zero);
            bool stopped = false; int side = 0;
            Vector2 direction = CharacterTestBehindTargetResolver.Resolve(movement, sample, ref stopped, ref side, out _);
            return new CharacterTestBehindTargetPreview(stopped
                ? movement.BehindTarget.Maintain ? CharacterTestBehindTargetPreviewStatus.Holding : CharacterTestBehindTargetPreviewStatus.Arrived
                : CharacterTestBehindTargetPreviewStatus.Moving, direction);
        }
    }

    internal static class CharacterTestBehindTargetResolver
    {
        internal static bool TryRearError(in CharacterTestRuntimeAiLabMovementTargetSample sample, out int signedError)
        {
            signedError = 0;
            if (!sample.Exists || !sample.HasTargetOrientation || !sample.HasFiniteSpatialSample
                || sample.TowardTarget.sqrMagnitude <= 0.000001f)
                return false;
            int awayYaw = MxFramework.CharacterControl.CharacterKinematicMotionMath.Atan2MilliDegrees(
                (int)System.Math.Round(-sample.TowardTarget.x * 1000000d),
                (int)System.Math.Round(-sample.TowardTarget.y * 1000000d));
            signedError = CharacterTestDeterministicMath.ShortestAngleDelta(sample.TargetYawMilliDegrees + 180000, awayYaw);
            return true;
        }
        internal static bool Arrived(in CharacterTestBehaviorExecutionMovement move,
            in CharacterTestRuntimeAiLabMovementTargetSample sample, bool holding = false)
        {
            var rear = move.BehindTarget;
            float tolerance = holding ? rear.ResumeDistanceTolerance : rear.DistanceTolerance;
            return TryRearError(sample, out int error)
                && System.Math.Abs(error) <= System.Math.Round((holding ? rear.ResumeHalfAngle : rear.HalfAngle) * 1000d)
                && sample.Distance >= System.Math.Max(0, move.Minimum - tolerance) && sample.Distance <= move.Maximum + tolerance;
        }
        internal static Vector2 Resolve(in CharacterTestBehaviorExecutionMovement move,
            in CharacterTestRuntimeAiLabMovementTargetSample sample, ref bool stopped, ref int orbitSide, out int limit)
        {
            limit = 0;
            if (!TryRearError(sample, out int error)) { stopped = false; return Vector2.zero; }
            if (stopped && !Arrived(move, sample, true)) stopped = false;
            if (!stopped && Arrived(move, sample)) stopped = true;
            if (stopped) return Vector2.zero;
            var rear = move.BehindTarget;
            Vector2 toward = sample.TowardTarget.normalized;
            float min = System.Math.Max(0, move.Minimum - rear.DistanceTolerance);
            float max = move.Maximum + rear.DistanceTolerance;
            float radialError = sample.Distance < min ? sample.Distance - min : sample.Distance > max ? sample.Distance - max : 0;
            bool angular = System.Math.Abs(error) > System.Math.Round(rear.HalfAngle * 1000d);
            if (!angular)
            {
                limit = (int)System.Math.Min(int.MaxValue, System.Math.Round(System.Math.Abs((double)radialError) * 1000d));
                return radialError < 0 ? -toward : radialError > 0 ? toward : Vector2.zero;
            }
            int candidate = System.Math.Abs(error) == 180000 ? 1 : error > 0 ? -1 : 1;
            if (orbitSide == 0 || candidate == orbitSide
                || System.Math.Abs(error) < 180000d - rear.DirectionSwitchHysteresis * 1000d)
                orbitSide = candidate;
            int side = orbitSide;
            Vector2 tangent = new Vector2(-toward.y, toward.x) * side;
            // Deterministic tie uses +1. Near the tie retain the prior side to prevent per-tick reversals.
            // Otherwise use the shorter rear-entry arc; never chase a rear point through the target.
            double remaining = ((-error * side - rear.HalfAngle * 1000d) % 360000d + 360000d) % 360000d;
            limit = -1;
            if (remaining < 45000d && sample.RootDistance > 0f)
                limit = (int)System.Math.Min(int.MaxValue, System.Math.Max(1, System.Math.Ceiling(
                    sample.RootDistance * System.Math.Tan(remaining * System.Math.PI / 180000d) * 1000d)));
            return Vector2.ClampMagnitude(tangent + toward * (System.Math.Sign(radialError) * move.RadialWeight), 1f);
        }
    }

    /// <summary>Pure movement-card semantics shared by runtime execution and focused tests.</summary>
    internal static class CharacterTestRuntimeAiLabMovementResolver
    {
        private const float DirectionEpsilon = 0.0001f;
        private const float OrbitRadialCorrectionWeight = 0.65f;

        public static bool IsEligible(
            CharacterTestRuntimeAiLabCompiledLogicStage stage,
            in CharacterTestRuntimeAiLabMovementTargetSample target)
        {
            if (stage == null
                || stage.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                || !target.Exists)
            {
                return false;
            }

            switch (stage.MovementStrategy)
            {
                case CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget:
                    return stage.MoveMinDistance <= 0f || target.Distance > stage.MoveMinDistance;
                case CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance:
                    return !HasConfiguredBand(stage)
                        || target.Distance < stage.MoveMinDistance
                        || target.Distance > stage.MoveMaxDistance;
                case CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose:
                    return ResolveCloseThreshold(stage) <= 0f
                        || target.Distance < ResolveCloseThreshold(stage);
                case CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard:
                case CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked:
                case CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget:
                    return true;
                default:
                    return false;
            }
        }

        public static Vector2 Resolve(
            CharacterTestRuntimeAiLabCompiledLogicStage stage,
            in CharacterTestRuntimeAiLabMovementTargetSample target,
            int entityId)
        {
            if (!IsEligible(stage, target))
                return Vector2.zero;

            Vector2 toward = target.TowardTarget.sqrMagnitude > DirectionEpsilon
                ? target.TowardTarget.normalized
                : target.NavigationMove.normalized;
            Vector2 navigation = target.NavigationMove.sqrMagnitude > DirectionEpsilon
                ? target.NavigationMove.normalized
                : toward;

            switch (stage.MovementStrategy)
            {
                case CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget:
                    return navigation;
                case CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance:
                    if (!HasConfiguredBand(stage) || target.Distance < stage.MoveMinDistance)
                        return -toward;
                    return target.Distance > stage.MoveMaxDistance ? navigation : Vector2.zero;
                case CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose:
                case CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard:
                    return -toward;
                case CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked:
                    return Tangent(toward, ResolveOrbitSide(stage, entityId));
                case CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget:
                    return ResolveOrbit(stage, target.Distance, toward, entityId);
                default:
                    return Vector2.zero;
            }
        }

        private static Vector2 ResolveOrbit(
            CharacterTestRuntimeAiLabCompiledLogicStage stage,
            float distance,
            Vector2 toward,
            int entityId)
        {
            Vector2 tangent = Tangent(toward, ResolveOrbitSide(stage, entityId));
            Vector2 radial = Vector2.zero;
            if (HasConfiguredBand(stage))
            {
                if (distance < stage.MoveMinDistance)
                    radial = -toward;
                else if (distance > stage.MoveMaxDistance)
                    radial = toward;
            }
            return Vector2.ClampMagnitude(tangent + (radial * OrbitRadialCorrectionWeight), 1f);
        }

        private static Vector2 Tangent(Vector2 toward, int side)
        {
            if (toward.sqrMagnitude <= DirectionEpsilon)
                return Vector2.zero;
            Vector2 tangent = new Vector2(-toward.y, toward.x);
            return side < 0 ? -tangent : tangent;
        }

        private static int ResolveOrbitSide(
            CharacterTestRuntimeAiLabCompiledLogicStage stage,
            int entityId)
        {
            unchecked
            {
                int hash = (entityId * 397) ^ (stage.Order * 31);
                string stageId = stage.StageId ?? string.Empty;
                for (int i = 0; i < stageId.Length; i++)
                    hash = (hash * 31) + stageId[i];
                return (hash & 1) == 0 ? 1 : -1;
            }
        }

        private static bool HasConfiguredBand(CharacterTestRuntimeAiLabCompiledLogicStage stage)
        {
            return stage.MoveMinDistance > 0f || stage.MoveMaxDistance > 0f;
        }

        private static float ResolveCloseThreshold(CharacterTestRuntimeAiLabCompiledLogicStage stage)
        {
            return stage.MoveMinDistance > 0f ? stage.MoveMinDistance : stage.MoveMaxDistance;
        }
    }
}

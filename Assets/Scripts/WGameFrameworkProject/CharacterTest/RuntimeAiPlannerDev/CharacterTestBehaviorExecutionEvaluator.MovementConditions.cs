using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestBehaviorExecutionEvaluator
    {
        private static bool EvaluateExecutionPredicate(in CharacterTestBehaviorExecutionPredicate predicate,
            ICharacterTestBehaviorExecutionRuntime runtime)
        {
            var move = predicate.Movement;
            if (predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.MovementTargetMissing)
            {
                // Target existence must not be confused with an unavailable Hurtbox distance sample.
                var targetOnly = new CharacterTestBehaviorExecutionMovement(CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard,
                    move.Target, move.Metric, move.EntityId, move.X, move.Z, 0, 0, 1, 0, requireLivingTarget: move.RequireLivingTarget);
                return !runtime.TrySampleMovement(targetOnly, out var target) || !target.Exists;
            }
            if (predicate.Kind != CharacterTestBehaviorExecutionPredicateKind.MovementArrived) return runtime.Evaluate(predicate);
            if (!runtime.TrySampleMovement(move, out var sample) || !sample.Exists) return false;
            if (move.BehindTarget.Enabled) return CharacterTestBehindTargetResolver.Arrived(move, sample);
            if (move.IsOptimalRange) return Math.Abs(sample.Distance - move.Optimal) <= move.StopTolerance;
            switch (move.Strategy)
            {
                case CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget: return sample.Distance <= move.Minimum;
                case CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose: return sample.Distance >= move.Minimum;
                case CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance:
                    return sample.Distance >= move.EffectiveMinimum && sample.Distance <= move.EffectiveMaximum;
                case CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget: return sample.Distance >= move.Minimum && sample.Distance <= move.Maximum;
                default: return false;
            }
        }
    }
}

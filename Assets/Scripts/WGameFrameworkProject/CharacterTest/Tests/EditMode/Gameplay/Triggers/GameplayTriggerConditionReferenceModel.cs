using System;
using System.Collections.Generic;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.Triggers
{
    /// <summary>
    /// Test-only recursive condition oracle. It evaluates a tree directly and only emits
    /// postfix instructions as input for the production condition program.
    /// </summary>
    internal abstract class GameplayTriggerConditionReferenceNode
    {
        public abstract bool Evaluate(in GameplayTriggerOccurrence occurrence);
        public abstract void EmitPostfix(List<GameplayTriggerConditionInstruction> output);

        public GameplayTriggerConditionProgram Compile()
        {
            var instructions = new List<GameplayTriggerConditionInstruction>();
            EmitPostfix(instructions);
            return new GameplayTriggerConditionProgram(instructions.ToArray());
        }

        public static GameplayTriggerConditionReferenceNode Literal(bool value) =>
            new LiteralNode(value);

        public static GameplayTriggerConditionReferenceNode Leaf(
            GameplayTriggerConditionOperation operation,
            long operand = 0L,
            GameplayTriggerComparison comparison = GameplayTriggerComparison.Equal) =>
            new LeafNode(operation, operand, comparison);

        public static GameplayTriggerConditionReferenceNode Not(
            GameplayTriggerConditionReferenceNode operand) =>
            new NotNode(operand);

        public static GameplayTriggerConditionReferenceNode All(
            GameplayTriggerConditionReferenceNode left,
            GameplayTriggerConditionReferenceNode right) =>
            new BinaryNode(left, right, all: true);

        public static GameplayTriggerConditionReferenceNode Any(
            GameplayTriggerConditionReferenceNode left,
            GameplayTriggerConditionReferenceNode right) =>
            new BinaryNode(left, right, all: false);

        private sealed class LiteralNode : GameplayTriggerConditionReferenceNode
        {
            private readonly bool _value;

            public LiteralNode(bool value)
            {
                _value = value;
            }

            public override bool Evaluate(in GameplayTriggerOccurrence occurrence) => _value;

            public override void EmitPostfix(
                List<GameplayTriggerConditionInstruction> output)
            {
                output.Add(new GameplayTriggerConditionInstruction(
                    _value
                        ? GameplayTriggerConditionOperation.PushTrue
                        : GameplayTriggerConditionOperation.PushFalse));
            }
        }

        private sealed class LeafNode : GameplayTriggerConditionReferenceNode
        {
            private readonly GameplayTriggerConditionOperation _operation;
            private readonly long _operand;
            private readonly GameplayTriggerComparison _comparison;

            public LeafNode(
                GameplayTriggerConditionOperation operation,
                long operand,
                GameplayTriggerComparison comparison)
            {
                if (operation < GameplayTriggerConditionOperation.OccurrenceMask ||
                    operation > GameplayTriggerConditionOperation.Payload2)
                {
                    throw new ArgumentOutOfRangeException(nameof(operation));
                }
                _operation = operation;
                _operand = operand;
                _comparison = comparison;
            }

            public override bool Evaluate(in GameplayTriggerOccurrence occurrence)
            {
                switch (_operation)
                {
                    case GameplayTriggerConditionOperation.OccurrenceMask:
                        return (((long)occurrence.Kind) & _operand) != 0L;
                    case GameplayTriggerConditionOperation.SignalIdEquals:
                        return occurrence.SignalId == _operand;
                    case GameplayTriggerConditionOperation.HasSubject:
                        return occurrence.Subject.IsValid == (_operand != 0L);
                    case GameplayTriggerConditionOperation.HasInstigator:
                        return occurrence.Instigator.IsValid == (_operand != 0L);
                    case GameplayTriggerConditionOperation.PreviousScalar:
                        return Compare(
                            occurrence.PreviousScalar.RawValue,
                            _operand,
                            _comparison);
                    case GameplayTriggerConditionOperation.CurrentScalar:
                        return Compare(
                            occurrence.CurrentScalar.RawValue,
                            _operand,
                            _comparison);
                    case GameplayTriggerConditionOperation.Payload0:
                        return Compare(occurrence.Payload0, _operand, _comparison);
                    case GameplayTriggerConditionOperation.Payload1:
                        return Compare(occurrence.Payload1, _operand, _comparison);
                    case GameplayTriggerConditionOperation.Payload2:
                        return Compare(occurrence.Payload2, _operand, _comparison);
                    default:
                        throw new InvalidOperationException("Unsupported reference leaf.");
                }
            }

            public override void EmitPostfix(
                List<GameplayTriggerConditionInstruction> output)
            {
                output.Add(new GameplayTriggerConditionInstruction(
                    _operation,
                    _operand,
                    _comparison));
            }
        }

        private sealed class NotNode : GameplayTriggerConditionReferenceNode
        {
            private readonly GameplayTriggerConditionReferenceNode _operand;

            public NotNode(GameplayTriggerConditionReferenceNode operand)
            {
                _operand = operand ?? throw new ArgumentNullException(nameof(operand));
            }

            public override bool Evaluate(in GameplayTriggerOccurrence occurrence) =>
                !_operand.Evaluate(occurrence);

            public override void EmitPostfix(
                List<GameplayTriggerConditionInstruction> output)
            {
                _operand.EmitPostfix(output);
                output.Add(new GameplayTriggerConditionInstruction(
                    GameplayTriggerConditionOperation.Not));
            }
        }

        private sealed class BinaryNode : GameplayTriggerConditionReferenceNode
        {
            private readonly GameplayTriggerConditionReferenceNode _left;
            private readonly GameplayTriggerConditionReferenceNode _right;
            private readonly bool _all;

            public BinaryNode(
                GameplayTriggerConditionReferenceNode left,
                GameplayTriggerConditionReferenceNode right,
                bool all)
            {
                _left = left ?? throw new ArgumentNullException(nameof(left));
                _right = right ?? throw new ArgumentNullException(nameof(right));
                _all = all;
            }

            public override bool Evaluate(in GameplayTriggerOccurrence occurrence)
            {
                bool left = _left.Evaluate(occurrence);
                bool right = _right.Evaluate(occurrence);
                return _all ? left && right : left || right;
            }

            public override void EmitPostfix(
                List<GameplayTriggerConditionInstruction> output)
            {
                _left.EmitPostfix(output);
                _right.EmitPostfix(output);
                output.Add(new GameplayTriggerConditionInstruction(
                    _all
                        ? GameplayTriggerConditionOperation.All
                        : GameplayTriggerConditionOperation.Any));
            }
        }

        private static bool Compare(
            long left,
            long right,
            GameplayTriggerComparison comparison)
        {
            switch (comparison)
            {
                case GameplayTriggerComparison.Equal: return left == right;
                case GameplayTriggerComparison.NotEqual: return left != right;
                case GameplayTriggerComparison.Less: return left < right;
                case GameplayTriggerComparison.LessOrEqual: return left <= right;
                case GameplayTriggerComparison.Greater: return left > right;
                case GameplayTriggerComparison.GreaterOrEqual: return left >= right;
                default: throw new InvalidOperationException("Unsupported reference comparison.");
            }
        }
    }
}

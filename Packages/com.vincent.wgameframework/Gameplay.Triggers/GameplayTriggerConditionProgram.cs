using System;

namespace MxFramework.Gameplay.Triggers
{
    public enum GameplayTriggerConditionOperation
    {
        PushTrue = 0,
        PushFalse = 1,
        OccurrenceMask = 2,
        SignalIdEquals = 3,
        HasSubject = 4,
        HasInstigator = 5,
        PreviousScalar = 6,
        CurrentScalar = 7,
        Payload0 = 8,
        Payload1 = 9,
        Payload2 = 10,
        Not = 11,
        All = 12,
        Any = 13
    }

    public enum GameplayTriggerComparison
    {
        Equal = 0,
        NotEqual = 1,
        Less = 2,
        LessOrEqual = 3,
        Greater = 4,
        GreaterOrEqual = 5
    }

    public readonly struct GameplayTriggerConditionInstruction
    {
        public GameplayTriggerConditionInstruction(GameplayTriggerConditionOperation operation, long operand = 0L, GameplayTriggerComparison comparison = GameplayTriggerComparison.Equal)
        {
            Operation = operation;
            Operand = operand;
            Comparison = comparison;
        }

        public GameplayTriggerConditionOperation Operation { get; }
        public long Operand { get; }
        public GameplayTriggerComparison Comparison { get; }
    }

    /// <summary>Validated postfix condition program.</summary>
    public sealed class GameplayTriggerConditionProgram
    {
        private static readonly GameplayTriggerConditionInstruction[] AlwaysInstructions =
        {
            new GameplayTriggerConditionInstruction(GameplayTriggerConditionOperation.PushTrue)
        };

        private readonly GameplayTriggerConditionInstruction[] _instructions;

        public GameplayTriggerConditionProgram(GameplayTriggerConditionInstruction[] instructions)
        {
            if (instructions == null || instructions.Length == 0) throw new ArgumentException("Condition program requires at least one instruction.", nameof(instructions));
            _instructions = new GameplayTriggerConditionInstruction[instructions.Length];
            Array.Copy(instructions, _instructions, instructions.Length);
            MaxStackDepth = Validate(_instructions);
        }

        public static GameplayTriggerConditionProgram Always { get; } = new GameplayTriggerConditionProgram(AlwaysInstructions);
        public int InstructionCount => _instructions.Length;
        public int MaxStackDepth { get; }

        /// <remarks>NoAlloc. Internal catalog hash/compiler read view.</remarks>
        internal GameplayTriggerConditionInstruction GetInstruction(int index) => _instructions[index];

        /// <remarks>NoAlloc when the caller-owned stack is large enough.</remarks>
        public bool Evaluate(in GameplayTriggerOccurrence occurrence, bool[] stack)
        {
            if (stack == null) throw new ArgumentNullException(nameof(stack));
            if (stack.Length < MaxStackDepth) throw new ArgumentException("Condition stack capacity is too small.", nameof(stack));

            int count = 0;
            for (int i = 0; i < _instructions.Length; i++)
            {
                GameplayTriggerConditionInstruction instruction = _instructions[i];
                switch (instruction.Operation)
                {
                    case GameplayTriggerConditionOperation.PushTrue:
                        stack[count++] = true;
                        break;
                    case GameplayTriggerConditionOperation.PushFalse:
                        stack[count++] = false;
                        break;
                    case GameplayTriggerConditionOperation.OccurrenceMask:
                        stack[count++] = (((long)occurrence.Kind) & instruction.Operand) != 0L;
                        break;
                    case GameplayTriggerConditionOperation.SignalIdEquals:
                        stack[count++] = occurrence.SignalId == instruction.Operand;
                        break;
                    case GameplayTriggerConditionOperation.HasSubject:
                        stack[count++] = occurrence.Subject.IsValid == (instruction.Operand != 0L);
                        break;
                    case GameplayTriggerConditionOperation.HasInstigator:
                        stack[count++] = occurrence.Instigator.IsValid == (instruction.Operand != 0L);
                        break;
                    case GameplayTriggerConditionOperation.PreviousScalar:
                        stack[count++] = Compare(occurrence.PreviousScalar.RawValue, instruction.Operand, instruction.Comparison);
                        break;
                    case GameplayTriggerConditionOperation.CurrentScalar:
                        stack[count++] = Compare(occurrence.CurrentScalar.RawValue, instruction.Operand, instruction.Comparison);
                        break;
                    case GameplayTriggerConditionOperation.Payload0:
                        stack[count++] = Compare(occurrence.Payload0, instruction.Operand, instruction.Comparison);
                        break;
                    case GameplayTriggerConditionOperation.Payload1:
                        stack[count++] = Compare(occurrence.Payload1, instruction.Operand, instruction.Comparison);
                        break;
                    case GameplayTriggerConditionOperation.Payload2:
                        stack[count++] = Compare(occurrence.Payload2, instruction.Operand, instruction.Comparison);
                        break;
                    case GameplayTriggerConditionOperation.Not:
                        stack[count - 1] = !stack[count - 1];
                        break;
                    case GameplayTriggerConditionOperation.All:
                    {
                        bool right = stack[--count];
                        stack[count - 1] = stack[count - 1] && right;
                        break;
                    }
                    case GameplayTriggerConditionOperation.Any:
                    {
                        bool right = stack[--count];
                        stack[count - 1] = stack[count - 1] || right;
                        break;
                    }
                    default:
                        throw new InvalidOperationException("Condition program contains an unsupported operation.");
                }
            }

            return stack[0];
        }

        private static int Validate(GameplayTriggerConditionInstruction[] instructions)
        {
            int depth = 0;
            int maxDepth = 0;
            for (int i = 0; i < instructions.Length; i++)
            {
                GameplayTriggerConditionOperation operation = instructions[i].Operation;
                if (operation >= GameplayTriggerConditionOperation.PushTrue && operation <= GameplayTriggerConditionOperation.Payload2)
                {
                    depth++;
                    if (depth > maxDepth) maxDepth = depth;
                    continue;
                }

                if (operation == GameplayTriggerConditionOperation.Not)
                {
                    if (depth < 1) throw new ArgumentException("Condition Not requires one operand.", nameof(instructions));
                    continue;
                }

                if (operation == GameplayTriggerConditionOperation.All || operation == GameplayTriggerConditionOperation.Any)
                {
                    if (depth < 2) throw new ArgumentException("Condition binary operation requires two operands.", nameof(instructions));
                    depth--;
                    continue;
                }

                throw new ArgumentException("Condition program contains an unsupported operation.", nameof(instructions));
            }

            if (depth != 1) throw new ArgumentException("Condition program must leave exactly one value on the stack.", nameof(instructions));
            return maxDepth;
        }

        private static bool Compare(long left, long right, GameplayTriggerComparison comparison)
        {
            switch (comparison)
            {
                case GameplayTriggerComparison.Equal: return left == right;
                case GameplayTriggerComparison.NotEqual: return left != right;
                case GameplayTriggerComparison.Less: return left < right;
                case GameplayTriggerComparison.LessOrEqual: return left <= right;
                case GameplayTriggerComparison.Greater: return left > right;
                case GameplayTriggerComparison.GreaterOrEqual: return left >= right;
                default: throw new InvalidOperationException("Unsupported trigger comparison.");
            }
        }
    }
}

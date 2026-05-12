using System;
using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public sealed class RuntimeCommandBuffer
    {
        private readonly List<RuntimeCommand> _pending = new List<RuntimeCommand>();
        private readonly IRuntimeCommandValidator _validator;
        private long _nextSequence;

        public RuntimeCommandBuffer()
            : this(null, RuntimeFrame.Zero)
        {
        }

        public RuntimeCommandBuffer(IRuntimeCommandValidator validator)
            : this(validator, RuntimeFrame.Zero)
        {
        }

        public RuntimeCommandBuffer(IRuntimeCommandValidator validator, RuntimeFrame startFrame)
        {
            _validator = validator;
            CurrentFrame = startFrame;
        }

        public RuntimeFrame CurrentFrame { get; private set; }
        public int PendingCount => _pending.Count;

        public RuntimeCommandValidationResult Enqueue(RuntimeCommand command)
        {
            if (command.Frame < CurrentFrame)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.LateCommand,
                    command,
                    CurrentFrame,
                    "Runtime command frame is earlier than the current command buffer frame."));
            }

            if (command.CommandId < 0)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidCommandId,
                    command,
                    CurrentFrame,
                    "Runtime command id cannot be negative."));
            }

            if (_validator != null)
            {
                RuntimeCommandValidationResult validation = _validator.Validate(command);
                if (!validation.Success)
                {
                    return validation;
                }
            }

            RuntimeCommand accepted = command.WithSequence(_nextSequence);
            _nextSequence++;
            _pending.Add(accepted);
            return RuntimeCommandValidationResult.Accepted(accepted);
        }

        public IReadOnlyList<RuntimeCommand> DrainForFrame(RuntimeFrame frame)
        {
            if (frame < CurrentFrame)
            {
                throw new InvalidOperationException("Runtime command buffer cannot drain an earlier frame. CurrentFrame=" + CurrentFrame + ", Frame=" + frame + ".");
            }

            var drained = new List<RuntimeCommand>();
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                RuntimeCommand command = _pending[i];
                if (command.Frame <= frame)
                {
                    _pending.RemoveAt(i);
                    drained.Add(command);
                }
            }

            drained.Sort(CompareCommands);
            CurrentFrame = frame.Next();
            return drained;
        }

        public void Clear()
        {
            _pending.Clear();
        }

        private static int CompareCommands(RuntimeCommand left, RuntimeCommand right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            if (frame != 0)
            {
                return frame;
            }

            int source = left.SourceId.CompareTo(right.SourceId);
            if (source != 0)
            {
                return source;
            }

            int command = left.CommandId.CompareTo(right.CommandId);
            if (command != 0)
            {
                return command;
            }

            int target = left.TargetId.CompareTo(right.TargetId);
            if (target != 0)
            {
                return target;
            }

            return left.Sequence.CompareTo(right.Sequence);
        }
    }
}

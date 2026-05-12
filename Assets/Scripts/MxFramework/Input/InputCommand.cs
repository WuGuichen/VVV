using System;
using UnityEngine;

namespace MxFramework.Input
{
    public enum InputIntent
    {
        Unknown = 0,
        Jump = 10,
        AttackPrimary = 20,
        AttackSecondary = 21,
        Interact = 30,
        Dodge = 40,
        Sprint = 50,
        Pause = 60,
        Submit = 70,
        Cancel = 71,
        Click = 80,
        RightClick = 81,
        ExitVehicle = 90,
        TakeShot = 100,
        ExitPhotoMode = 101,
        SkipCutscene = 110,
        ContinueCutscene = 111,
        ToggleConsole = 120,
        Restart = 130,
        DebugPrimary = 140,
        DebugSecondary = 141,
        DebugCycle = 142,
        DebugStep = 143,
        ToggleHud = 150,
        AudioPrimary = 160,
        AudioSecondary = 161
    }

    public enum InputCommandPhase
    {
        Performed = 0,
        Pressed = 1,
        Released = 2,
        Canceled = 3
    }

    public readonly struct InputCommand : IEquatable<InputCommand>
    {
        public InputCommand(
            long frame,
            int sourceId,
            InputIntent intent,
            InputCommandPhase phase = InputCommandPhase.Pressed,
            Vector2 value = default,
            int targetId = 0,
            string traceId = "",
            long sequence = 0L)
        {
            if (frame < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Input command frame cannot be negative.");
            }

            Frame = frame;
            SourceId = sourceId;
            Intent = intent;
            Phase = phase;
            Value = value;
            TargetId = targetId;
            TraceId = traceId ?? string.Empty;
            Sequence = sequence;
        }

        public long Frame { get; }
        public int SourceId { get; }
        public InputIntent Intent { get; }
        public InputCommandPhase Phase { get; }
        public Vector2 Value { get; }
        public int TargetId { get; }
        public string TraceId { get; }
        public long Sequence { get; }

        public InputCommand WithSequence(long sequence)
        {
            return new InputCommand(Frame, SourceId, Intent, Phase, Value, TargetId, TraceId, sequence);
        }

        public bool Equals(InputCommand other)
        {
            return Frame == other.Frame
                && SourceId == other.SourceId
                && Intent == other.Intent
                && Phase == other.Phase
                && Value == other.Value
                && TargetId == other.TargetId
                && TraceId == other.TraceId
                && Sequence == other.Sequence;
        }

        public override bool Equals(object obj)
        {
            return obj is InputCommand other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Frame.GetHashCode();
                hashCode = (hashCode * 397) ^ SourceId;
                hashCode = (hashCode * 397) ^ (int)Intent;
                hashCode = (hashCode * 397) ^ (int)Phase;
                hashCode = (hashCode * 397) ^ Value.GetHashCode();
                hashCode = (hashCode * 397) ^ TargetId;
                hashCode = (hashCode * 397) ^ TraceId.GetHashCode();
                hashCode = (hashCode * 397) ^ Sequence.GetHashCode();
                return hashCode;
            }
        }
    }
}

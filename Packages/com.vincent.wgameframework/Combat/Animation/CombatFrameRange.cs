using System;

namespace MxFramework.Combat.Animation
{
    public readonly struct CombatFrameRange : IEquatable<CombatFrameRange>
    {
        public static readonly CombatFrameRange Empty = new CombatFrameRange(0, -1, allowEmpty: true);

        public CombatFrameRange(int startFrame, int endFrame)
            : this(startFrame, endFrame, allowEmpty: false)
        {
        }

        private CombatFrameRange(int startFrame, int endFrame, bool allowEmpty)
        {
            if (startFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startFrame), "Frame range start cannot be negative.");
            }

            if (!allowEmpty && endFrame < startFrame)
            {
                throw new ArgumentOutOfRangeException(nameof(endFrame), "Frame range end cannot be before start.");
            }

            StartFrame = startFrame;
            EndFrame = endFrame;
        }

        public int StartFrame { get; }

        public int EndFrame { get; }

        public bool IsEmpty => EndFrame < StartFrame;

        public bool Contains(int localFrame)
        {
            return !IsEmpty && localFrame >= StartFrame && localFrame <= EndFrame;
        }

        public void ValidateWithin(int totalFrames, string name)
        {
            if (totalFrames <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalFrames), "Total frames must be positive.");
            }

            if (IsEmpty)
            {
                return;
            }

            if (EndFrame >= totalFrames)
            {
                throw new ArgumentOutOfRangeException(name, "Frame range must be within action total frames.");
            }
        }

        public bool Equals(CombatFrameRange other)
        {
            return StartFrame == other.StartFrame && EndFrame == other.EndFrame;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatFrameRange other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StartFrame * 397) ^ EndFrame;
            }
        }
    }
}

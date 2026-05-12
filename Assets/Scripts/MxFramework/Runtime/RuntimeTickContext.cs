using System;

namespace MxFramework.Runtime
{
    public readonly struct RuntimeTickContext
    {
        public RuntimeTickContext(long frameIndex, double deltaTime, double elapsedTime, RuntimeTickStage stage)
        {
            if (frameIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex), "Frame index cannot be negative.");
            }

            if (deltaTime < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
            }

            if (elapsedTime < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTime), "Elapsed time cannot be negative.");
            }

            FrameIndex = frameIndex;
            DeltaTime = deltaTime;
            ElapsedTime = elapsedTime;
            Stage = stage;
        }

        public long FrameIndex { get; }
        public double DeltaTime { get; }
        public double ElapsedTime { get; }
        public RuntimeTickStage Stage { get; }

        public RuntimeTickContext WithStage(RuntimeTickStage stage)
        {
            return new RuntimeTickContext(FrameIndex, DeltaTime, ElapsedTime, stage);
        }
    }
}

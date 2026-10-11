namespace MxFramework.Runtime
{
    public interface IDeterministicRandom
    {
        int NextInt(int minInclusive, int maxExclusive);
        float NextFloat01();
        bool Chance(float probability);
        void Reset(uint seed);
        RuntimeRandomState CaptureState();
        void RestoreState(RuntimeRandomState state);
    }
}

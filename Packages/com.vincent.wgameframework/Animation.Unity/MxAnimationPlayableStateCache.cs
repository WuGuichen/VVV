using MxFramework.Animation;
using MxFramework.Resources;

namespace MxFramework.Animation.Unity
{
    internal sealed class MxAnimationPlayableStateCache
    {
        public int CacheHitCount { get; private set; }
        public int CacheMissCount { get; private set; }

        public void RecordHit()
        {
            CacheHitCount++;
        }

        public void RecordMiss()
        {
            CacheMissCount++;
        }

        public MxAnimationBackendCacheDiagnostic CreateDiagnostic(
            int residentClipCount,
            int cachedPlayableCount,
            int activePlayableCount,
            ResourceDebugSnapshot resources)
        {
            return new MxAnimationBackendCacheDiagnostic(
                CacheHitCount,
                CacheMissCount,
                residentClipCount,
                cachedPlayableCount,
                activePlayableCount,
                resources != null ? resources.LoadedCount : 0,
                resources != null ? resources.TotalRefCount : 0);
        }
    }
}

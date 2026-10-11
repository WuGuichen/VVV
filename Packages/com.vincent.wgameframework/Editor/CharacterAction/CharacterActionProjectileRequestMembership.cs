using System;
using System.Collections.Generic;

namespace MxFramework.Editor.CharacterAction
{
    /// <summary>
    /// Read-only classification of gameplay request IDs backed by the loaded projectile request store.
    /// </summary>
    internal static class CharacterActionProjectileRequestMembership
    {
        private static readonly HashSet<string> RequestIds = new HashSet<string>(StringComparer.Ordinal);
        private static bool _loaded;

        /// <summary>LowFreqAlloc. Refreshes membership from an already-loaded authoring store.</summary>
        public static void Refresh(CharacterActionProjectileAuthoringStore store)
        {
            RequestIds.Clear();
            if (store != null)
            {
                for (int i = 0; i < store.Records.Count; i++)
                {
                    CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record = store.Records[i];
                    if (CharacterActionProjectileRequestEligibility.IsCompiledReferenceable(record))
                        RequestIds.Add(record.RequestId);
                }
            }

            _loaded = true;
        }

        /// <summary>LowFreqAlloc on first use; subsequent membership checks do not allocate.</summary>
        public static bool Contains(string requestId)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(requestId) && RequestIds.Contains(requestId);
        }

        public static bool Contains(CharacterActionProjectileAuthoringStore store, string requestId)
        {
            if (store != null)
                Refresh(store);
            return Contains(requestId);
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;
            try
            {
                Refresh(CharacterActionProjectileAuthoringStore.LoadDefault());
            }
            catch (Exception)
            {
                RequestIds.Clear();
                _loaded = true;
            }
        }
    }
}

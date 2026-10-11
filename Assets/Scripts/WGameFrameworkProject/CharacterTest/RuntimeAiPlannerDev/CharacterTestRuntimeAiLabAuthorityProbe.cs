using System.Collections.Generic;
using System.Globalization;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Captures the minimal authority proof used by Runtime AI Lab replay validation.
    /// </summary>
    /// <remarks>LowFreqAlloc. This probe is only called after explicit Lab timeline operations.</remarks>
    internal static class CharacterTestRuntimeAiLabAuthorityProbe
    {
        public static CharacterTestRuntimeAiLabAuthoritySample Capture(
            GameSlice slice,
            CharacterTestNetworkSnapshotParticipant snapshotParticipant)
        {
            if (slice == null || snapshotParticipant == null)
                return default;

            RuntimeFrame frame = slice.CurrentFrame;
            return new CharacterTestRuntimeAiLabAuthoritySample(
                frame,
                snapshotParticipant.ComputeStateHash(frame));
        }

        public static CharacterTestRuntimeAiLabFrameSyncReport Compare(
            CharacterTestRuntimeAiLabAuthoritySample expected,
            CharacterTestRuntimeAiLabAuthoritySample actual)
        {
            var rows = new List<CharacterTestRuntimeAiLabFrameSyncMismatch>(2);
            if (expected.Frame != actual.Frame)
            {
                rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                    "authority",
                    0,
                    "runtimeFrame",
                    expected.Frame.Value.ToString(CultureInfo.InvariantCulture),
                    actual.Frame.Value.ToString(CultureInfo.InvariantCulture)));
            }
            if (expected.StateHash != actual.StateHash)
            {
                rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                    "authority",
                    0,
                    "stateHash",
                    expected.StateHash.ToString("X16", CultureInfo.InvariantCulture),
                    actual.StateHash.ToString("X16", CultureInfo.InvariantCulture)));
            }

            return new CharacterTestRuntimeAiLabFrameSyncReport(rows);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabAuthoritySample
    {
        public CharacterTestRuntimeAiLabAuthoritySample(RuntimeFrame frame, long stateHash)
        {
            Frame = frame;
            StateHash = stateHash;
        }

        public RuntimeFrame Frame { get; }
        public long StateHash { get; }
    }
}

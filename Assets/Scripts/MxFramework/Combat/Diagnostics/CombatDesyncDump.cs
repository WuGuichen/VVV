using MxFramework.Combat.Core;

namespace MxFramework.Combat.Diagnostics
{
    public readonly struct CombatDesyncDump
    {
        public CombatDesyncDump(CombatFrame frame, CombatDebugSnapshot expected, CombatDebugSnapshot actual)
        {
            Frame = frame;
            Expected = expected;
            Actual = actual;
        }

        public CombatFrame Frame { get; }

        public CombatDebugSnapshot Expected { get; }

        public CombatDebugSnapshot Actual { get; }

        public bool HasMismatch
        {
            get
            {
                if (Expected == null || Actual == null)
                {
                    return Expected != Actual;
                }

                return !Expected.FrameHash.Equals(Actual.FrameHash);
            }
        }

        public string Summary
        {
            get
            {
                string expectedHash = Expected == null ? "(null)" : Expected.FrameHash.ToString();
                string actualHash = Actual == null ? "(null)" : Actual.FrameHash.ToString();
                return $"frame={Frame.Value} expected={expectedHash} actual={actualHash}";
            }
        }
    }
}

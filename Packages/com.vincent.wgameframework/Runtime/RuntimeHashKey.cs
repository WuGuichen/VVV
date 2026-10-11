using System;

namespace MxFramework.Runtime
{
    /// <summary>Temporary migration-only path bridge. Runtime state code must use integer fields and scopes.</summary>
    public readonly struct RuntimeHashKey
    {
        internal readonly struct Part
        {
            internal readonly string Text;
            internal readonly int Index;
            internal readonly bool Numeric;
            internal Part(string text) { Text = text; Index = 0; Numeric = false; }
            internal Part(int index) { Text = null; Index = index; Numeric = true; }
        }
        private readonly Part _p0, _p1, _p2, _p3, _p4, _p5, _p6, _p7, _p8, _p9, _p10, _p11;
        internal readonly int Count;
        internal readonly int Length;

        public RuntimeHashKey(string prefix)
        {
            this = default;
            _p0 = new Part(prefix ?? string.Empty);
            Count = 1;
            Length = prefix?.Length ?? 0;
        }
        private RuntimeHashKey(in RuntimeHashKey previous, Part part, int length)
        {
            this = previous;
            if (previous.Count >= 12) throw new ArgumentException("Runtime hash field path exceeds twelve fragments.");
            switch (previous.Count)
            {
                case 0: _p0 = part; break; case 1: _p1 = part; break; case 2: _p2 = part; break;
                case 3: _p3 = part; break; case 4: _p4 = part; break; case 5: _p5 = part; break;
                case 6: _p6 = part; break; case 7: _p7 = part; break; case 8: _p8 = part; break;
                case 9: _p9 = part; break; case 10: _p10 = part; break; case 11: _p11 = part; break;
            }
            Count = previous.Count + 1;
            Length = checked(previous.Length + length);
        }
        public static RuntimeHashKey operator +(RuntimeHashKey key, string suffix) =>
            string.IsNullOrEmpty(suffix) ? key : new RuntimeHashKey(key, new Part(suffix), suffix.Length);
        public static RuntimeHashKey operator +(RuntimeHashKey key, int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            int digits = 1; for (int n = index; n >= 10; n /= 10) digits++;
            return new RuntimeHashKey(key, new Part(index), digits);
        }
        internal Part GetPart(int index) => index switch
        {
            0 => _p0, 1 => _p1, 2 => _p2, 3 => _p3, 4 => _p4, 5 => _p5,
            6 => _p6, 7 => _p7, 8 => _p8, 9 => _p9, 10 => _p10, 11 => _p11,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }
}

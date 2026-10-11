using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public static class AuthoringSchemaVersions
    {
        public static readonly IReadOnlyList<string> Supported = new[] { "1.0" };

        public static bool IsSupported(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return false;
            for (int i = 0; i < Supported.Count; i++)
            {
                if (string.Equals(Supported[i], version)) return true;
            }
            return false;
        }

        public static int Compare(string a, string b)
        {
            int[] av = ParseSemVer(a);
            int[] bv = ParseSemVer(b);
            int len = av.Length > bv.Length ? av.Length : bv.Length;
            for (int i = 0; i < len; i++)
            {
                int x = i < av.Length ? av[i] : 0;
                int y = i < bv.Length ? bv[i] : 0;
                if (x != y) return x - y;
            }
            return 0;
        }

        public static bool IsHigherThanLatest(string version)
        {
            if (string.IsNullOrWhiteSpace(version) || Supported.Count == 0) return false;
            string latest = Supported[0];
            for (int i = 1; i < Supported.Count; i++)
            {
                if (Compare(Supported[i], latest) > 0) latest = Supported[i];
            }
            return Compare(version, latest) > 0;
        }

        private static int[] ParseSemVer(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return new[] { 0 };
            var parts = new List<int>();
            string[] segments = version.Split('.');
            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];
                int n = 0;
                int idx = 0;
                while (idx < seg.Length && seg[idx] >= '0' && seg[idx] <= '9')
                {
                    n = n * 10 + (seg[idx] - '0');
                    idx++;
                }
                parts.Add(n);
            }
            return parts.ToArray();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.NoEngineTests.Determinism
{
    // Test-only observation. Uses the existing production diff; never alters gameplay or hashes.
    internal static class FrameTrace
    {
        public static RuntimeSnapshotValue Value(string field, object value) =>
            new RuntimeSnapshotValue(field, Convert.ToString(value, CultureInfo.InvariantCulture));

        public static void Equal(long frame, IReadOnlyList<RuntimeSnapshotValue> expected,
            IReadOnlyList<RuntimeSnapshotValue> actual, string inputs = "")
        {
            RuntimeChangeSet changes = RuntimeSnapshotDiff.Compare(expected, actual);
            if (!changes.HasChanges) return;
            RuntimeChange first = changes.Changes[0];
            var details = new StringBuilder();
            for (int i = 0; i < Math.Min(changes.Count, 12); i++)
            {
                RuntimeChange change = changes.Changes[i];
                details.Append("\n").Append(change.Key).Append(": ").Append(change.BeforeValue).Append(" -> ").Append(change.AfterValue);
            }
            Assert.Fail("first divergence frame=" + frame + " field=" + first.Key +
                " expected=" + first.BeforeValue + " actual=" + first.AfterValue +
                " changedFields=" + changes.Count + " inputs=" + inputs + details);
        }
    }
}

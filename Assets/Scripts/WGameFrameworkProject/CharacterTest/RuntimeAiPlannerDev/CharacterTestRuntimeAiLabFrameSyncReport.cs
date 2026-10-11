using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncMismatch
    {
        public CharacterTestRuntimeAiLabFrameSyncMismatch(
            string layer,
            int entityId,
            string field,
            string expected,
            string actual)
        {
            Layer = layer ?? string.Empty;
            EntityId = entityId;
            Field = field ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
        }

        public string Layer { get; }
        public int EntityId { get; }
        public string Field { get; }
        public string Expected { get; }
        public string Actual { get; }
    }

    /// <summary>
    /// Read-only comparison report for Runtime AI Lab frame synchronization checks.
    /// </summary>
    internal sealed class CharacterTestRuntimeAiLabFrameSyncReport
    {
        public CharacterTestRuntimeAiLabFrameSyncReport(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMismatch> mismatchRows)
        {
            MismatchRows = mismatchRows ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncMismatch>();
            Passed = MismatchRows.Count == 0;
            SummaryText = BuildSummary(MismatchRows);
        }

        public bool Passed { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMismatch> MismatchRows { get; }
        public string SummaryText { get; }

        private static string BuildSummary(IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            int count = rows != null ? rows.Count : 0;
            if (count <= 0)
                return "Frame sync OK";

            const int maxRows = 8;
            int visibleRows = Math.Min(count, maxRows);
            var builder = new StringBuilder(256);
            builder.Append("Frame sync mismatch:");
            for (int i = 0; i < visibleRows; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncMismatch row = rows[i];
                builder.Append(" ")
                    .Append(row.Layer)
                    .Append("/entity ")
                    .Append(row.EntityId.ToString(CultureInfo.InvariantCulture))
                    .Append("/")
                    .Append(row.Field)
                    .Append(" expected=")
                    .Append(row.Expected)
                    .Append(" actual=")
                    .Append(row.Actual);
                if (i < visibleRows - 1)
                    builder.Append(";");
            }

            if (count > visibleRows)
            {
                builder.Append(" (+")
                    .Append((count - visibleRows).ToString(CultureInfo.InvariantCulture))
                    .Append(" more)");
            }

            return builder.ToString();
        }
    }
}

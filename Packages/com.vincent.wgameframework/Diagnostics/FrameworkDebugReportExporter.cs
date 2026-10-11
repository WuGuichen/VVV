using System;
using System.Text;

namespace MxFramework.Diagnostics
{
    public static class FrameworkDebugReportExporter
    {
        public static string ExportText(FrameworkDebugSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var builder = new StringBuilder();
            builder.Append("source: ").Append(snapshot.SourceName).Append('\n');
            builder.Append("mode: ").Append(snapshot.Mode).Append('\n');
            builder.Append("sections:\n");
            for (int i = 0; i < snapshot.Sections.Count; i++)
            {
                FrameworkDebugSection section = snapshot.Sections[i];
                builder.Append("- ").Append(section.Title).Append('\n');
                if (!string.IsNullOrEmpty(section.Body))
                    builder.Append(section.Body).Append('\n');
            }

            return builder.ToString();
        }
    }
}

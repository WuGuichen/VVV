using System.Text;

namespace MxFramework.Combat.Authoring
{
    public static class CombatAuthoringExportReport
    {
        public static string BuildValidationText(CombatAuthoringReport report)
        {
            if (report == null)
            {
                return "Combat Authoring Validation Report\nNo validation report.";
            }

            var builder = new StringBuilder(512);
            builder.AppendLine("Combat Authoring Validation Report");
            builder.AppendLine("IssueCount: " + report.IssueCount);
            builder.AppendLine("HasErrors: " + report.HasErrors);
            for (int i = 0; i < report.IssueCount; i++)
            {
                CombatAuthoringIssue issue = report.GetIssue(i);
                builder.Append(i + 1);
                builder.Append(". [");
                builder.Append(issue.Severity);
                builder.Append("] ");
                builder.Append(issue.SourceAsset);
                builder.Append(" / ");
                builder.Append(issue.Section);
                builder.Append(" / track ");
                builder.Append(issue.TrackId);
                builder.Append(" / ");
                builder.Append(issue.Field);
                builder.Append(" / frame ");
                builder.AppendLine(FormatRange(issue.FrameRange));
                builder.Append("   Message: ");
                builder.AppendLine(issue.Message);
                builder.Append("   Fix: ");
                builder.AppendLine(issue.SuggestedFix);
                builder.Append("   QuickAction: ");
                builder.AppendLine(issue.QuickAction.ToString());
            }

            if (report.IssueCount == 0)
            {
                builder.AppendLine("No issues.");
            }

            return builder.ToString();
        }

        private static string FormatRange(CombatAuthoringFrameRange range)
        {
            return range.IsEmpty ? "empty" : range.StartFrame + "-" + range.EndFrame;
        }
    }
}

using MxFramework.Diagnostics;
using NUnit.Framework;

namespace MxFramework.Tests.Diagnostics
{
    public class FrameworkDebugReportTests
    {
        [Test]
        public void ExportText_IncludesSourceModeAndSections()
        {
            var snapshot = new FrameworkDebugSnapshot(
                "Sandbox",
                FrameworkDebugMode.Runtime,
                new[] { new FrameworkDebugSection("Buffs", "count: 1") });

            string text = FrameworkDebugReportExporter.ExportText(snapshot);

            StringAssert.Contains("source: Sandbox", text);
            StringAssert.Contains("mode: Runtime", text);
            StringAssert.Contains("- Buffs", text);
            StringAssert.Contains("count: 1", text);
        }
    }
}

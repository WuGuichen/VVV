using System;
using MxFramework.Authoring.FairyGuiArtSlices;
using MxFramework.UI.FairyGui;

namespace MxFramework.NoEngineTests.FairyGUI.Manifest
{
    internal static class FairyGuiReviewReportBuilder
    {
        public static string Generate(
            MxFairyGuiManifestGenerationBatchResult manifestResult,
            FairyGuiArtSliceValidationResult artSliceResult)
        {
            string manifestReport = MxFairyGuiManifestGenerator.GenerateReviewReport(manifestResult);
            string artSliceSummary = FairyGuiArtSliceCatalogTool.GenerateMarkdownSummary(artSliceResult);
            if (string.IsNullOrEmpty(artSliceSummary))
                return manifestReport;

            if (string.IsNullOrEmpty(manifestReport))
                return artSliceSummary;

            if (manifestReport.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                return manifestReport + Environment.NewLine + artSliceSummary;

            return manifestReport + Environment.NewLine + Environment.NewLine + artSliceSummary;
        }
    }
}

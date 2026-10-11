using System;
using MxFramework.Authoring.FairyGuiArtSlices;
using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.NoEngineTests.FairyGUI.Manifest
{
    internal static class FairyGuiReviewReportBuilderTests
    {
        public static void RunAll()
        {
            Generate_IsDeterministicAndAppendsArtSliceSummaryAfterManifest();
            Generate_ReturnsManifestReportWhenArtSliceResultIsMissing();
        }

        private static void Generate_IsDeterministicAndAppendsArtSliceSummaryAfterManifest()
        {
            MxFairyGuiManifestGenerationBatchResult manifestResult = CreateManifestResult();
            FairyGuiArtSliceValidationResult artSliceResult = CreateArtSliceResult();
            string first = FairyGuiReviewReportBuilder.Generate(manifestResult, artSliceResult);
            string second = FairyGuiReviewReportBuilder.Generate(manifestResult, artSliceResult);

            Require(string.Equals(first, second, StringComparison.Ordinal), "review report builder should be deterministic for the same inputs.");
            int manifestIndex = first.IndexOf("FairyGUI Manifest Review Report", StringComparison.Ordinal);
            int artSliceIndex = first.IndexOf("## FairyGUI Art Slice Summary", StringComparison.Ordinal);
            Require(manifestIndex >= 0, "combined review report should include manifest report.");
            Require(artSliceIndex > manifestIndex, "combined review report should append art slice summary after manifest report.");
            Require(first.Contains("Package: `MxSmokeHud`", StringComparison.Ordinal), "combined review report should include art slice package id.");
            Require(first.Contains("- Images: 1", StringComparison.Ordinal), "combined review report should include art slice image count.");
            Require(first.Contains("- Direct image usages: 1", StringComparison.Ordinal), "combined review report should include art slice usage count.");
        }

        private static void Generate_ReturnsManifestReportWhenArtSliceResultIsMissing()
        {
            MxFairyGuiManifestGenerationBatchResult manifestResult = CreateManifestResult();
            string expected = MxFairyGuiManifestGenerator.GenerateReviewReport(manifestResult);
            string actual = FairyGuiReviewReportBuilder.Generate(manifestResult, null);

            Require(string.Equals(actual, expected, StringComparison.Ordinal), "review report builder should preserve manifest-only reports when art slice result is missing.");
        }

        private static MxFairyGuiManifestGenerationBatchResult CreateManifestResult()
        {
            var manifest = new MxFairyGuiManifest
            {
                Packages = new[]
                {
                    new MxFairyGuiManifestPackage
                    {
                        PackageId = "MxSmokeHud",
                        PackageName = "MxSmokeHud",
                        PackageSourcePath = "FGUIProject/assets/MxSmokeHud/package.xml",
                        PackageBytesPath = "Assets/Bundles/FGUI/MxSmokeHud/MxSmokeHud_fui.bytes",
                        PackageBytes = new ResourceKey("ui.fairygui.smoke.fui", MxFairyGuiManifestResourceTypeIds.PackageBytes)
                    }
                },
                Views = new[]
                {
                    new MxFairyGuiManifestView
                    {
                        ViewId = new MxUiViewId("ui.smoke.hud"),
                        PackageId = "MxSmokeHud",
                        ComponentName = "SmokePanel",
                        NamedControls = new[]
                        {
                            new MxFairyGuiNamedControl("title", "GTextField")
                        }
                    }
                }
            };
            var item = new MxFairyGuiManifestGenerationBatchItem(
                "smoke-hud",
                new MxFairyGuiManifestGenerationSpec
                {
                    PackageId = "MxSmokeHud",
                    ViewId = "ui.smoke.hud"
                },
                "Generated/SmokeHudManifest.cs");
            var generation = new MxFairyGuiManifestGenerationResult(
                manifest,
                "// generated",
                Array.Empty<MxFairyGuiManifestDiagnostic>());
            var itemResult = new MxFairyGuiManifestGenerationBatchItemResult(
                item,
                generation,
                generatedOutputValidation: null);

            return new MxFairyGuiManifestGenerationBatchResult(
                new[] { itemResult },
                Array.Empty<MxFairyGuiManifestDiagnostic>());
        }

        private static FairyGuiArtSliceValidationResult CreateArtSliceResult()
        {
            return new FairyGuiArtSliceValidationResult(
                "MxSmokeHud",
                "FGUIProject/assets/MxSmokeHud",
                new[]
                {
                    new FairyGuiPackageImageResource(
                        "smoke1",
                        "button.png",
                        "Images/button.png",
                        "FGUIProject/assets/MxSmokeHud/package.xml")
                },
                new[]
                {
                    new FairyGuiArtSliceUsagePoint(
                        "SmokePanel",
                        "FGUIProject/assets/MxSmokeHud/SmokePanel.xml",
                        "button",
                        "n1",
                        "smoke1",
                        "Images/button.png",
                        new FairyGuiPoint(0, 0),
                        new FairyGuiSize(100, 50),
                        preserveAspect: false,
                        Array.Empty<FairyGuiRelation>(),
                        new FairyGuiGearDisplay(string.Empty, Array.Empty<string>()))
                },
                Array.Empty<FairyGuiArtSliceDiagnostic>());
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}

using System;
using System.IO;
using MxFramework.Authoring.FairyGuiArtSlices;

namespace MxFramework.NoEngineTests.FairyGUI.Manifest
{
    internal static class FairyGuiArtSliceCatalogTests
    {
        public static void RunAll(string repositoryRoot)
        {
            CharacterTestArtSliceCatalog_ValidatesCurrentPackage(repositoryRoot);
            CharacterTestArtSliceCatalog_ReportIsDeterministic(repositoryRoot);
            CharacterTestArtSliceCatalog_ReportsPackageImageUncataloged(repositoryRoot);
            CharacterTestArtSliceCatalog_ReportsExpectedSourcePathDrift(repositoryRoot);
            CharacterTestArtSliceCatalog_ReportsMissingSource(repositoryRoot);
            CharacterTestArtSliceCatalog_ReportsMissingResourceId(repositoryRoot);
            CharacterTestArtSliceCatalog_ReportsPreserveAspectMismatch(repositoryRoot);
            CharacterTestArtSliceCatalog_ReportsGroupSizeMismatch(repositoryRoot);
        }

        private static void CharacterTestArtSliceCatalog_ValidatesCurrentPackage(string repositoryRoot)
        {
            FairyGuiArtSliceValidationResult result = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(repositoryRoot);
            Require(result.Success, "CharacterTest art slice catalog should validate without errors.\n" + FairyGuiArtSliceCatalogTool.GenerateMarkdownReport(result));
            Require(result.PackageImages.Count == 51, "CharacterTest art slice inventory should include current package images.");
            Require(result.UsagePoints.Count > 0, "CharacterTest art slice scanner should find direct image usages.");
        }

        private static void CharacterTestArtSliceCatalog_ReportIsDeterministic(string repositoryRoot)
        {
            FairyGuiArtSliceValidationResult first = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(repositoryRoot);
            FairyGuiArtSliceValidationResult second = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(repositoryRoot);
            string firstMarkdown = FairyGuiArtSliceCatalogTool.GenerateMarkdownReport(first);
            string secondMarkdown = FairyGuiArtSliceCatalogTool.GenerateMarkdownReport(second);
            string firstJson = FairyGuiArtSliceCatalogTool.GenerateJsonReport(first);
            string secondJson = FairyGuiArtSliceCatalogTool.GenerateJsonReport(second);

            Require(string.Equals(firstMarkdown, secondMarkdown, StringComparison.Ordinal), "CharacterTest art slice markdown report should be deterministic.");
            Require(string.Equals(firstJson, secondJson, StringComparison.Ordinal), "CharacterTest art slice JSON report should be deterministic.");
            Require(firstMarkdown.Contains("FairyGUI Art Slice Report", StringComparison.Ordinal), "markdown report should include a stable title.");
            Require(firstJson.Contains("\"schemaVersion\": \"mx.uiArtSliceReport.v1\"", StringComparison.Ordinal), "JSON report should include stable schema version.");
        }

        private static void CharacterTestArtSliceCatalog_ReportsPackageImageUncataloged(string repositoryRoot)
        {
            using (TempPackage temp = TempPackage.Create(repositoryRoot))
            {
                string catalogPath = Path.Combine(temp.PackageRoot, "ui-art-slices.json");
                string text = File.ReadAllText(catalogPath);
                text = text.Replace("\"fguiResourceId\": \"cthud1n\"", "\"fguiResourceId\": \"cthud_missing_coverage\"");
                File.WriteAllText(catalogPath, text);

                FairyGuiArtSliceValidationResult result = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(temp.Root);
                Require(ContainsDiagnostic(result, "UIART001"), "uncataloged package image should report UIART001.");
            }
        }

        private static void CharacterTestArtSliceCatalog_ReportsExpectedSourcePathDrift(string repositoryRoot)
        {
            using (TempPackage temp = TempPackage.Create(repositoryRoot))
            {
                string catalogPath = Path.Combine(temp.PackageRoot, "ui-art-slices.json");
                string text = File.ReadAllText(catalogPath);
                text = text.Replace(
                    "\"expectedSourcePath\": \"Images/menu/menu_button_up_420x64.png\"",
                    "\"expectedSourcePath\": \"Images/menu/menu_button_old_420x64.png\"");
                File.WriteAllText(catalogPath, text);

                FairyGuiArtSliceValidationResult result = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(temp.Root);
                Require(ContainsDiagnostic(result, "UIART002"), "expectedSourcePath drift should report UIART002.");
            }
        }

        private static void CharacterTestArtSliceCatalog_ReportsMissingSource(string repositoryRoot)
        {
            using (TempPackage temp = TempPackage.Create(repositoryRoot))
            {
                File.Delete(Path.Combine(temp.PackageRoot, "Images", "menu", "menu_button_up_420x64.png"));
                FairyGuiArtSliceValidationResult result = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(temp.Root);
                Require(ContainsDiagnostic(result, "UIART003"), "missing source PNG should report UIART003.");
            }
        }

        private static void CharacterTestArtSliceCatalog_ReportsMissingResourceId(string repositoryRoot)
        {
            using (TempPackage temp = TempPackage.Create(repositoryRoot))
            {
                string catalogPath = Path.Combine(temp.PackageRoot, "ui-art-slices.json");
                string text = File.ReadAllText(catalogPath);
                text = text.Replace("\"fguiResourceId\": \"cthud1n\"", "\"fguiResourceId\": \"cthud_missing\"");
                File.WriteAllText(catalogPath, text);

                FairyGuiArtSliceValidationResult result = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(temp.Root);
                Require(ContainsDiagnostic(result, "UIART004"), "missing resource id should report UIART004.");
            }
        }

        private static void CharacterTestArtSliceCatalog_ReportsPreserveAspectMismatch(string repositoryRoot)
        {
            using (TempPackage temp = TempPackage.Create(repositoryRoot))
            {
                string componentPath = Path.Combine(temp.PackageRoot, "CombatHudPanel.xml");
                string text = File.ReadAllText(componentPath);
                text = text.Replace(
                    "name=\"lockCrosshair\" src=\"cthud9\" fileName=\"Images/hud/crosshair_diamond_48x48.png\" xy=\"616,316\" size=\"48,48\" aspect=\"true\"",
                    "name=\"lockCrosshair\" src=\"cthud9\" fileName=\"Images/hud/crosshair_diamond_48x48.png\" xy=\"616,316\" size=\"48,48\"");
                File.WriteAllText(componentPath, text);

                FairyGuiArtSliceValidationResult result = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(temp.Root);
                Require(ContainsDiagnostic(result, "UIART102"), "preserveAspect mismatch should report UIART102.");
            }
        }

        private static void CharacterTestArtSliceCatalog_ReportsGroupSizeMismatch(string repositoryRoot)
        {
            using (TempPackage temp = TempPackage.Create(repositoryRoot))
            {
                File.Copy(
                    Path.Combine(temp.PackageRoot, "Images", "icons", "health_amber_64.png"),
                    Path.Combine(temp.PackageRoot, "Images", "menu", "menu_button_over_420x64.png"),
                    overwrite: true);

                FairyGuiArtSliceValidationResult result = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(temp.Root);
                Require(ContainsDiagnostic(result, "UIART104"), "group pixel size mismatch should report UIART104.");
            }
        }

        private static bool ContainsDiagnostic(FairyGuiArtSliceValidationResult result, string code)
        {
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                if (string.Equals(result.Diagnostics[i].Code, code, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class TempPackage : IDisposable
        {
            private TempPackage(string root, string packageRoot)
            {
                Root = root;
                PackageRoot = packageRoot;
            }

            public string Root { get; }
            public string PackageRoot { get; }

            public static TempPackage Create(string repositoryRoot)
            {
                string root = Path.Combine(Path.GetTempPath(), "MxFairyGuiArtSliceTests", Guid.NewGuid().ToString("N"));
                string source = Path.Combine(repositoryRoot, "FGUIProject", "assets", "MxCharacterTestHud");
                string destination = Path.Combine(root, "FGUIProject", "assets", "MxCharacterTestHud");
                CopyDirectory(source, destination);
                return new TempPackage(root, destination);
            }

            public void Dispose()
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }

            private static void CopyDirectory(string source, string destination)
            {
                Directory.CreateDirectory(destination);
                string[] files = Directory.GetFiles(source);
                for (int i = 0; i < files.Length; i++)
                {
                    string target = Path.Combine(destination, Path.GetFileName(files[i]));
                    File.Copy(files[i], target, overwrite: true);
                }

                string[] directories = Directory.GetDirectories(source);
                for (int i = 0; i < directories.Length; i++)
                {
                    CopyDirectory(directories[i], Path.Combine(destination, Path.GetFileName(directories[i])));
                }
            }
        }
    }
}

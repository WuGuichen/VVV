using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    public sealed class GlobalAssetBundleBuilderWorkbench : EditorWindow
    {
        private const string MenuPath = "MxFramework/Resources/Open Global AssetBundle Builder";

        private GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan _plan;
        private Label _statusLabel;
        private Label _profilePathLabel;
        private Label _buildTargetLabel;
        private Label _summaryLabel;
        private VisualElement _artifactRows;
        private VisualElement _diagnosticRows;
        private string _lastStatus = "Ready.";
        private bool _lastStatusIsError;

        [MenuItem(MenuPath, priority = 129)]
        public static void Open()
        {
            GlobalAssetBundleBuilderWorkbench window = GetWindow<GlobalAssetBundleBuilderWorkbench>();
            window.titleContent = new GUIContent("Global AB Builder");
            window.minSize = new Vector2(780f, 520f);
            window.Show();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            root.Add(CreateHeader());
            root.Add(CreateCommandBar());

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.marginTop = 10;
            root.Add(scroll);

            scroll.Add(CreateStatusPanel());
            scroll.Add(CreateBuildPlanPanel());
            scroll.Add(CreateArtifactsPanel());
            scroll.Add(CreateDiagnosticsPanel());

            RefreshState("Profile refreshed.", false);
        }

        private VisualElement CreateHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            var title = new Label("Global AssetBundle Builder");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 18;
            title.style.flexGrow = 1;
            header.Add(title);

            var badge = new Label("Editor Workbench");
            badge.style.paddingLeft = 8;
            badge.style.paddingRight = 8;
            badge.style.paddingTop = 3;
            badge.style.paddingBottom = 3;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.style.backgroundColor = new Color(0.18f, 0.32f, 0.52f);
            badge.style.color = Color.white;
            header.Add(badge);

            return header;
        }

        private VisualElement CreateCommandBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.marginBottom = 4;

            bar.Add(CreateCommandButton("Refresh / Validate Profile", RefreshCommand));
            bar.Add(CreateCommandButton("Build Global Player Resource Catalog", BuildCommand));
            bar.Add(CreateCommandButton("Copy Report", CopyReportCommand));
            bar.Add(CreateCommandButton("Open Profile", OpenProfileCommand));
            return bar;
        }

        private VisualElement CreateStatusPanel()
        {
            VisualElement panel = CreatePanel("Build Status");
            _statusLabel = CreateWrappedLabel(string.Empty);
            panel.Add(_statusLabel);
            return panel;
        }

        private VisualElement CreateBuildPlanPanel()
        {
            VisualElement panel = CreatePanel("Build Plan Summary");

            _profilePathLabel = CreateWrappedLabel(string.Empty);
            _buildTargetLabel = CreateWrappedLabel(string.Empty);
            _summaryLabel = CreateWrappedLabel(string.Empty);

            panel.Add(_profilePathLabel);
            panel.Add(_buildTargetLabel);
            panel.Add(_summaryLabel);
            return panel;
        }

        private VisualElement CreateArtifactsPanel()
        {
            VisualElement panel = CreatePanel("Generated Artifacts");
            _artifactRows = new VisualElement();
            _artifactRows.Add(CreateArtifactHeader());
            panel.Add(_artifactRows);
            return panel;
        }

        private VisualElement CreateDiagnosticsPanel()
        {
            VisualElement panel = CreatePanel("Diagnostics");
            _diagnosticRows = new VisualElement();
            _diagnosticRows.Add(CreateDiagnosticHeader());
            panel.Add(_diagnosticRows);
            return panel;
        }

        private void RefreshCommand()
        {
            RefreshState("Profile validated.", false);
        }

        private void BuildCommand()
        {
            GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan plan =
                GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            _plan = plan;
            if (plan.HasErrors)
            {
                RefreshViews();
                SetStatus("Build blocked. Fix validation errors before generating Player artifacts.", true);
                return;
            }

            try
            {
                GlobalPlayerResourceBuildProfileBuilder.Build(plan);
                RefreshState("Build succeeded. Generated artifacts were refreshed for "
                    + EditorUserBuildSettings.activeBuildTarget + ".", false);
            }
            catch (Exception ex)
            {
                RefreshState("Build failed: " + ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void CopyReportCommand()
        {
            if (_plan == null)
                RefreshState("Profile validated before copying report.", false);

            EditorGUIUtility.systemCopyBuffer = CreateWorkbenchReport();
            SetStatus("Report copied to clipboard.", false);
        }

        private void OpenProfileCommand()
        {
            OpenAssetOrReveal(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
        }

        private void RefreshState(string status, bool isError)
        {
            _plan = GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            RefreshViews();
            SetStatus(status, isError);
        }

        private void RefreshViews()
        {
            _profilePathLabel.text = "Profile: " + GlobalPlayerResourceBuildProfileBuilder.ProfilePath
                + " (" + (File.Exists(GlobalPlayerResourceBuildProfileBuilder.ProfilePath) ? "exists" : "missing") + ")";
            _buildTargetLabel.text = "Active build target: " + EditorUserBuildSettings.activeBuildTarget;
            _summaryLabel.text = CreateSummaryText(_plan);

            _artifactRows.Clear();
            _artifactRows.Add(CreateArtifactHeader());
            IReadOnlyList<ArtifactInfo> artifacts = CreateArtifacts();
            for (int i = 0; i < artifacts.Count; i++)
                _artifactRows.Add(CreateArtifactRow(artifacts[i]));

            _diagnosticRows.Clear();
            _diagnosticRows.Add(CreateDiagnosticHeader());
            IReadOnlyList<GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildIssue> issues =
                _plan?.Report?.Issues ?? Array.Empty<GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildIssue>();
            if (issues.Count == 0)
            {
                _diagnosticRows.Add(CreateEmptyRow("No diagnostics."));
            }
            else
            {
                for (int i = 0; i < issues.Count; i++)
                    _diagnosticRows.Add(CreateDiagnosticRow(issues[i]));
            }
        }

        private void SetStatus(string text, bool isError)
        {
            _lastStatus = text ?? string.Empty;
            _lastStatusIsError = isError;
            if (_statusLabel == null)
                return;

            _statusLabel.text = _lastStatus;
            _statusLabel.style.color = isError ? new Color(0.95f, 0.32f, 0.28f) : new Color(0.4f, 0.82f, 0.45f);
        }

        private static string CreateSummaryText(GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan plan)
        {
            if (plan == null || plan.Report == null)
                return "No build plan.";

            CountIssues(plan.Report, out int errors, out int warnings);
            return "profileId: " + plan.Report.ProfileId
                + " | catalogId: " + plan.Report.CatalogId
                + " | packageId: " + plan.Report.PackageId
                + " | entries: " + plan.Entries.Count
                + " | bundles: " + plan.Bundles.Count
                + " | errors: " + errors
                + " | warnings: " + warnings;
        }

        private string CreateWorkbenchReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Global AssetBundle Builder Workbench");
            builder.AppendLine("Status: " + _lastStatus + (_lastStatusIsError ? " (error)" : string.Empty));
            builder.AppendLine("Profile: " + GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            builder.AppendLine("Active build target: " + EditorUserBuildSettings.activeBuildTarget);
            builder.AppendLine(CreateSummaryText(_plan));
            builder.AppendLine();
            builder.AppendLine("Artifacts:");

            IReadOnlyList<ArtifactInfo> artifacts = CreateArtifacts();
            for (int i = 0; i < artifacts.Count; i++)
                builder.AppendLine("- " + artifacts[i].Name + ": " + (artifacts[i].Exists ? "exists" : "missing") + " | " + artifacts[i].Path);

            builder.AppendLine();
            builder.AppendLine("Diagnostics:");
            builder.AppendLine(GlobalPlayerResourceBuildProfileBuilder.CreateReportText(_plan?.Report));
            return builder.ToString();
        }

        private static void CountIssues(
            GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildReport report,
            out int errors,
            out int warnings)
        {
            errors = 0;
            warnings = 0;
            if (report == null)
                return;

            for (int i = 0; i < report.Issues.Count; i++)
            {
                string severity = report.Issues[i].Severity;
                if (string.Equals(severity, "Error", StringComparison.Ordinal))
                    errors++;
                else if (string.Equals(severity, "Warning", StringComparison.Ordinal))
                    warnings++;
            }
        }

        private static IReadOnlyList<ArtifactInfo> CreateArtifacts()
        {
            string bundleOutputPath = GlobalPlayerResourceBuildProfileBuilder.BundleRootPath
                + "/" + EditorUserBuildSettings.activeBuildTarget;
            return new[]
            {
                new ArtifactInfo("Runtime catalog", GlobalPlayerResourceBuildProfileBuilder.CatalogPath, false),
                new ArtifactInfo("Preload groups", GlobalPlayerResourceBuildProfileBuilder.PreloadGroupsPath, false),
                new ArtifactInfo("Bundle dependencies", GlobalPlayerResourceBuildProfileBuilder.BundleDependenciesPath, false),
                new ArtifactInfo("Build report", GlobalPlayerResourceBuildProfileBuilder.BuildReportPath, false),
                new ArtifactInfo("Bundle output folder", bundleOutputPath, true)
            };
        }

        private static VisualElement CreateArtifactHeader()
        {
            var row = CreateRow(true);
            row.Add(CreateCell("Artifact", 18, true));
            row.Add(CreateCell("State", 10, true));
            row.Add(CreateCell("Path", 52, true));
            row.Add(CreateFixedCell("Actions", 160, true));
            return row;
        }

        private static VisualElement CreateArtifactRow(ArtifactInfo artifact)
        {
            var row = CreateRow(false);
            row.Add(CreateCell(artifact.Name, 18, false));
            row.Add(CreateCell(artifact.Exists ? "Exists" : "Missing", 10, false));
            row.Add(CreateCell(artifact.Path, 52, false));

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.width = 160;

            Button ping = CreateMiniButton("Ping", () => PingAsset(artifact.Path));
            ping.SetEnabled(artifact.Exists && !artifact.IsFolder);
            actions.Add(ping);

            Button open = CreateMiniButton(artifact.IsFolder ? "Open" : "Open", () => OpenAssetOrReveal(artifact.Path));
            open.SetEnabled(artifact.Exists);
            actions.Add(open);

            row.Add(actions);
            return row;
        }

        private static VisualElement CreateDiagnosticHeader()
        {
            var row = CreateRow(true);
            row.Add(CreateCell("Severity", 10, true));
            row.Add(CreateCell("Code", 18, true));
            row.Add(CreateCell("Message", 38, true));
            row.Add(CreateCell("Path", 20, true));
            row.Add(CreateCell("Resource Key", 14, true));
            return row;
        }

        private static VisualElement CreateDiagnosticRow(GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildIssue issue)
        {
            var row = CreateRow(false);
            row.Add(CreateCell(issue.Severity, 10, false));
            row.Add(CreateCell(issue.Code, 18, false));
            row.Add(CreateCell(issue.Message, 38, false));
            row.Add(CreateCell(issue.SourcePath, 20, false));
            row.Add(CreateCell(issue.ResourceKey, 14, false));
            return row;
        }

        private static VisualElement CreateEmptyRow(string text)
        {
            var row = CreateRow(false);
            var label = CreateWrappedLabel(text);
            label.style.flexGrow = 1;
            row.Add(label);
            return row;
        }

        private static VisualElement CreatePanel(string titleText)
        {
            var panel = new VisualElement();
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 4;
            panel.style.borderTopColor = new Color(0.22f, 0.22f, 0.22f);
            panel.style.borderRightColor = new Color(0.22f, 0.22f, 0.22f);
            panel.style.borderBottomColor = new Color(0.22f, 0.22f, 0.22f);
            panel.style.borderLeftColor = new Color(0.23f, 0.51f, 0.96f);
            panel.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f);
            panel.style.borderTopRightRadius = 6;
            panel.style.borderBottomRightRadius = 6;
            panel.style.paddingLeft = 12;
            panel.style.paddingRight = 12;
            panel.style.paddingTop = 10;
            panel.style.paddingBottom = 10;
            panel.style.marginBottom = 8;

            var title = new Label(titleText);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 12;
            title.style.color = new Color(0.9f, 0.9f, 0.9f);
            title.style.marginBottom = 8;
            panel.Add(title);
            return panel;
        }

        private static Button CreateCommandButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.height = 26;
            button.style.marginRight = 6;
            button.style.marginBottom = 6;
            return button;
        }

        private static Button CreateMiniButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.width = 70;
            button.style.height = 22;
            button.style.marginRight = 4;
            button.style.paddingLeft = 2;
            button.style.paddingRight = 2;
            return button;
        }

        private static VisualElement CreateRow(bool isHeader)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.minHeight = 26;
            row.style.alignItems = Align.Center;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;
            if (isHeader)
            {
                row.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
                row.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f);
                row.style.borderBottomWidth = 2;
            }

            return row;
        }

        private static Label CreateCell(string text, float percentWidth, bool isHeader)
        {
            var label = CreateWrappedLabel(text);
            label.style.width = Length.Percent(percentWidth);
            if (isHeader)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.color = new Color(0.95f, 0.95f, 0.95f);
            }

            return label;
        }

        private static Label CreateFixedCell(string text, int width, bool isHeader)
        {
            var label = CreateWrappedLabel(text);
            label.style.width = width;
            if (isHeader)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.color = new Color(0.95f, 0.95f, 0.95f);
            }

            return label;
        }

        private static Label CreateWrappedLabel(string text)
        {
            var label = new Label(text ?? string.Empty);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.paddingLeft = 4;
            label.style.paddingRight = 4;
            label.style.fontSize = 11;
            label.style.color = new Color(0.82f, 0.82f, 0.82f);
            return label;
        }

        private static void PingAsset(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null)
                return;

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        private static void OpenAssetOrReveal(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (Directory.Exists(path))
            {
                EditorUtility.RevealInFinder(path);
                return;
            }

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset);
                return;
            }

            if (File.Exists(path))
                EditorUtility.RevealInFinder(path);
        }

        private sealed class ArtifactInfo
        {
            public ArtifactInfo(string name, string path, bool isFolder)
            {
                Name = name;
                Path = path;
                IsFolder = isFolder;
                Exists = isFolder ? Directory.Exists(path) : File.Exists(path);
            }

            public string Name { get; }
            public string Path { get; }
            public bool IsFolder { get; }
            public bool Exists { get; }
        }
    }
}

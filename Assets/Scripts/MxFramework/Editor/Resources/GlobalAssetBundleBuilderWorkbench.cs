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
        private VisualElement _statusBanner;
        private VisualElement _statsRow;
        private VisualElement _profilePathRow;
        private VisualElement _profileStateRow;
        private VisualElement _buildTargetRow;
        private VisualElement _artifactRows;
        private VisualElement _diagnosticRows;
        private string _lastStatus = "Ready.";
        private bool _lastStatusIsError;

        [MenuItem(MenuPath, priority = 129)]
        public static void Open()
        {
            GlobalAssetBundleBuilderWorkbench window = GetWindow<GlobalAssetBundleBuilderWorkbench>();
            window.titleContent = new GUIContent("Global AB Builder");
            window.minSize = new Vector2(800f, 540f);
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
            scroll.contentContainer.style.flexGrow = 1;
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
            header.style.flexShrink = 0;
            header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f);

            Image icon = CreateIcon("d_BuildSettings.Editor", 32);
            if (icon == null)
                icon = CreateIcon("d_UnityEditor.SceneAsset Icon", 32);
            if (icon != null)
                header.Add(icon);

            var textContainer = new VisualElement();
            textContainer.style.flexDirection = FlexDirection.Column;
            textContainer.style.flexGrow = 1;
            textContainer.style.marginLeft = icon != null ? 8 : 0;

            var title = new Label("Global AssetBundle Builder");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 16;
            title.style.color = new Color(0.92f, 0.92f, 0.92f);
            textContainer.Add(title);

            var subtitle = new Label("Validate profile, build player resource catalog, and inspect generated artifacts.");
            subtitle.style.color = new Color(0.6f, 0.6f, 0.6f);
            subtitle.style.fontSize = 11;
            subtitle.style.marginTop = 2;
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            textContainer.Add(subtitle);
            header.Add(textContainer);

            var badge = new Label("Workbench");
            badge.style.paddingLeft = 10;
            badge.style.paddingRight = 10;
            badge.style.paddingTop = 4;
            badge.style.paddingBottom = 4;
            badge.style.borderTopLeftRadius = 10;
            badge.style.borderTopRightRadius = 10;
            badge.style.borderBottomLeftRadius = 10;
            badge.style.borderBottomRightRadius = 10;
            badge.style.backgroundColor = new Color(0.18f, 0.32f, 0.52f);
            badge.style.color = Color.white;
            badge.style.fontSize = 10;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.alignSelf = Align.FlexStart;
            header.Add(badge);

            return header;
        }

        private VisualElement CreateCommandBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.alignItems = Align.Center;
            bar.style.marginTop = 10;
            bar.style.marginBottom = 4;
            bar.style.flexShrink = 0;
            bar.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
            bar.style.paddingLeft = 8;
            bar.style.paddingRight = 8;
            bar.style.paddingTop = 6;
            bar.style.paddingBottom = 6;
            bar.style.borderTopLeftRadius = 4;
            bar.style.borderTopRightRadius = 4;
            bar.style.borderBottomLeftRadius = 4;
            bar.style.borderBottomRightRadius = 4;

            bar.Add(CreateStyledButton("Refresh / Validate", "d_Refresh", RefreshCommand));
            bar.Add(CreateStyledButton(
                "Build Catalog",
                "d_SaveAs",
                BuildCommand,
                customNormalBg: new Color(0.15f, 0.38f, 0.62f)));
            bar.Add(CreateToolbarSeparator());
            bar.Add(CreateStyledButton("Copy Report", "d_Copy", CopyReportCommand));
            bar.Add(CreateStyledButton("Open Profile", "d_TextAsset Icon", OpenProfileCommand));
            return bar;
        }

        private VisualElement CreateStatusPanel()
        {
            VisualElement panel = CreatePanel("Build Status", PanelAccent.Status);
            _statusBanner = CreateAlertBanner("Status", _lastStatus, _lastStatusIsError);
            panel.Add(_statusBanner);
            return panel;
        }

        private VisualElement CreateBuildPlanPanel()
        {
            VisualElement panel = CreatePanel("Build Plan", PanelAccent.Plan);

            _statsRow = new VisualElement();
            _statsRow.style.flexDirection = FlexDirection.Row;
            _statsRow.style.flexWrap = Wrap.Wrap;
            _statsRow.style.marginBottom = 10;
            panel.Add(_statsRow);

            _profilePathRow = CreateKeyValueRow("Profile", string.Empty);
            _profileStateRow = CreateKeyValueRow("Profile State", string.Empty);
            _buildTargetRow = CreateKeyValueRow("Build Target", string.Empty);

            panel.Add(_profilePathRow);
            panel.Add(_profileStateRow);
            panel.Add(_buildTargetRow);
            return panel;
        }

        private VisualElement CreateArtifactsPanel()
        {
            VisualElement panel = CreatePanel("Generated Artifacts", PanelAccent.Artifacts);
            _artifactRows = new VisualElement();
            _artifactRows.Add(CreateArtifactHeader());
            panel.Add(_artifactRows);
            return panel;
        }

        private VisualElement CreateDiagnosticsPanel()
        {
            VisualElement panel = CreatePanel("Diagnostics", PanelAccent.Diagnostics);
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
            ShowNotification(new GUIContent("Report copied"));
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
            bool profileExists = File.Exists(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            Label profilePathValue = _profilePathRow.Q<Label>("kv-value");
            profilePathValue.text = GlobalPlayerResourceBuildProfileBuilder.ProfilePath;

            Label profileStateValue = _profileStateRow.Q<Label>("kv-value");
            profileStateValue.text = profileExists ? "Exists" : "Missing";
            profileStateValue.style.color = profileExists
                ? new Color(0.45f, 0.85f, 0.5f)
                : new Color(0.95f, 0.45f, 0.35f);

            _buildTargetRow.Q<Label>("kv-value").text = EditorUserBuildSettings.activeBuildTarget.ToString();

            RefreshStatBadges(_plan);

            _artifactRows.Clear();
            _artifactRows.Add(CreateArtifactHeader());
            IReadOnlyList<ArtifactInfo> artifacts = CreateArtifacts();
            for (int i = 0; i < artifacts.Count; i++)
                _artifactRows.Add(CreateArtifactRow(artifacts[i], i % 2 == 1));

            _diagnosticRows.Clear();
            _diagnosticRows.Add(CreateDiagnosticHeader());
            IReadOnlyList<GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildIssue> issues =
                _plan?.Report?.Issues ?? (IReadOnlyList<GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildIssue>)Array.Empty<GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildIssue>();
            if (issues.Count == 0)
            {
                _diagnosticRows.Add(CreateEmptyRow("No diagnostics. Profile looks clean."));
            }
            else
            {
                for (int i = 0; i < issues.Count; i++)
                    _diagnosticRows.Add(CreateDiagnosticRow(issues[i], i % 2 == 1));
            }
        }

        private void RefreshStatBadges(GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan plan)
        {
            _statsRow.Clear();
            if (plan == null || plan.Report == null)
            {
                _statsRow.Add(CreateStatBadge("Plan", "Unavailable", new Color(0.45f, 0.45f, 0.45f)));
                return;
            }

            CountIssues(plan.Report, out int errors, out int warnings);
            _statsRow.Add(CreateStatBadge("Entries", plan.Entries.Count.ToString(), new Color(0.23f, 0.51f, 0.96f)));
            _statsRow.Add(CreateStatBadge("Bundles", plan.Bundles.Count.ToString(), new Color(0.23f, 0.51f, 0.96f)));
            _statsRow.Add(CreateStatBadge("Errors", errors.ToString(), errors > 0
                ? new Color(0.96f, 0.32f, 0.28f)
                : new Color(0.35f, 0.75f, 0.4f)));
            _statsRow.Add(CreateStatBadge("Warnings", warnings.ToString(), warnings > 0
                ? new Color(0.95f, 0.72f, 0.28f)
                : new Color(0.35f, 0.75f, 0.4f)));
            _statsRow.Add(CreateStatBadge("Catalog", plan.Report.CatalogId, new Color(0.55f, 0.55f, 0.55f)));
            _statsRow.Add(CreateStatBadge("Package", plan.Report.PackageId, new Color(0.55f, 0.55f, 0.55f)));
        }

        private void SetStatus(string text, bool isError)
        {
            _lastStatus = text ?? string.Empty;
            _lastStatusIsError = isError;
            if (_statusBanner == null || _statusBanner.parent == null)
                return;

            VisualElement parent = _statusBanner.parent;
            int index = parent.IndexOf(_statusBanner);
            parent.Remove(_statusBanner);
            _statusBanner = CreateAlertBanner(isError ? "Build Blocked" : "Ready", _lastStatus, isError);
            parent.Insert(index, _statusBanner);
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
            var row = CreateTableRow(true, false);
            row.Add(CreateCell("Artifact", 20, true));
            row.Add(CreateCell("State", 12, true));
            row.Add(CreateCell("Path", 48, true));
            row.Add(CreateFixedCell("Actions", 160, true));
            return row;
        }

        private static VisualElement CreateArtifactRow(ArtifactInfo artifact, bool zebra)
        {
            var row = CreateTableRow(false, zebra);
            row.Add(CreateCell(artifact.Name, 20, false));
            row.Add(CreateStateBadge(artifact.Exists));
            row.Add(CreateCell(artifact.Path, 48, false));

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.width = 160;
            actions.style.alignItems = Align.Center;

            Button ping = CreateMiniButton("Ping", "d_ViewToolMove", () => PingAsset(artifact.Path));
            ping.SetEnabled(artifact.Exists && !artifact.IsFolder);
            actions.Add(ping);

            string openIcon = artifact.IsFolder ? "d_FolderOpened Icon" : "d_TextAsset Icon";
            Button open = CreateMiniButton(artifact.IsFolder ? "Open" : "Reveal", openIcon, () => OpenAssetOrReveal(artifact.Path));
            open.SetEnabled(artifact.Exists);
            actions.Add(open);

            row.Add(actions);
            return row;
        }

        private static VisualElement CreateDiagnosticHeader()
        {
            var row = CreateTableRow(true, false);
            row.Add(CreateCell("Severity", 10, true));
            row.Add(CreateCell("Code", 16, true));
            row.Add(CreateCell("Message", 40, true));
            row.Add(CreateCell("Path", 20, true));
            row.Add(CreateCell("Resource Key", 14, true));
            return row;
        }

        private static VisualElement CreateDiagnosticRow(
            GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildIssue issue,
            bool zebra)
        {
            var row = CreateTableRow(false, zebra);
            row.Add(CreateSeverityBadge(issue.Severity));
            row.Add(CreateCell(issue.Code, 16, false));
            row.Add(CreateCell(issue.Message, 40, false));
            row.Add(CreateCell(issue.SourcePath, 20, false));
            row.Add(CreateCell(issue.ResourceKey, 14, false));
            return row;
        }

        private static VisualElement CreateEmptyRow(string text)
        {
            var row = CreateTableRow(false, false);
            var label = CreateWrappedLabel(text);
            label.style.flexGrow = 1;
            label.style.color = new Color(0.55f, 0.55f, 0.55f);
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            row.Add(label);
            return row;
        }

        private static VisualElement CreateStatBadge(string title, string value, Color accent)
        {
            var badge = new VisualElement();
            badge.style.minWidth = 88;
            badge.style.marginRight = 8;
            badge.style.marginBottom = 6;
            badge.style.paddingLeft = 10;
            badge.style.paddingRight = 10;
            badge.style.paddingTop = 8;
            badge.style.paddingBottom = 8;
            badge.style.borderTopLeftRadius = 6;
            badge.style.borderTopRightRadius = 6;
            badge.style.borderBottomLeftRadius = 6;
            badge.style.borderBottomRightRadius = 6;
            badge.style.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.12f);
            badge.style.borderTopWidth = 1;
            badge.style.borderRightWidth = 1;
            badge.style.borderBottomWidth = 1;
            badge.style.borderLeftWidth = 1;
            badge.style.borderTopColor = new Color(accent.r, accent.g, accent.b, 0.35f);
            badge.style.borderRightColor = badge.style.borderTopColor;
            badge.style.borderBottomColor = badge.style.borderTopColor;
            badge.style.borderLeftColor = badge.style.borderTopColor;

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 10;
            titleLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            badge.Add(titleLabel);

            var valueLabel = new Label(value ?? string.Empty);
            valueLabel.style.fontSize = 13;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = accent;
            valueLabel.style.marginTop = 2;
            valueLabel.style.whiteSpace = WhiteSpace.Normal;
            badge.Add(valueLabel);
            return badge;
        }

        private static VisualElement CreateStateBadge(bool exists)
        {
            var container = new VisualElement();
            container.style.width = Length.Percent(12);
            container.style.paddingLeft = 4;
            container.style.paddingRight = 4;
            container.style.justifyContent = Justify.Center;

            var pill = new Label(exists ? "Exists" : "Missing");
            pill.style.alignSelf = Align.FlexStart;
            pill.style.paddingLeft = 8;
            pill.style.paddingRight = 8;
            pill.style.paddingTop = 2;
            pill.style.paddingBottom = 2;
            pill.style.borderTopLeftRadius = 8;
            pill.style.borderTopRightRadius = 8;
            pill.style.borderBottomLeftRadius = 8;
            pill.style.borderBottomRightRadius = 8;
            pill.style.fontSize = 10;
            pill.style.unityFontStyleAndWeight = FontStyle.Bold;
            if (exists)
            {
                pill.style.backgroundColor = new Color(0.3f, 0.69f, 0.31f, 0.18f);
                pill.style.color = new Color(0.45f, 0.85f, 0.5f);
            }
            else
            {
                pill.style.backgroundColor = new Color(0.96f, 0.32f, 0.28f, 0.15f);
                pill.style.color = new Color(0.95f, 0.45f, 0.35f);
            }

            container.Add(pill);
            return container;
        }

        private static VisualElement CreateSeverityBadge(string severity)
        {
            var container = new VisualElement();
            container.style.width = Length.Percent(10);
            container.style.paddingLeft = 4;
            container.style.paddingRight = 4;

            Color accent = new Color(0.55f, 0.55f, 0.55f);
            if (string.Equals(severity, "Error", StringComparison.Ordinal))
                accent = new Color(0.96f, 0.32f, 0.28f);
            else if (string.Equals(severity, "Warning", StringComparison.Ordinal))
                accent = new Color(0.95f, 0.72f, 0.28f);

            var pill = new Label(severity ?? string.Empty);
            pill.style.alignSelf = Align.FlexStart;
            pill.style.paddingLeft = 8;
            pill.style.paddingRight = 8;
            pill.style.paddingTop = 2;
            pill.style.paddingBottom = 2;
            pill.style.borderTopLeftRadius = 8;
            pill.style.borderTopRightRadius = 8;
            pill.style.borderBottomLeftRadius = 8;
            pill.style.borderBottomRightRadius = 8;
            pill.style.fontSize = 10;
            pill.style.unityFontStyleAndWeight = FontStyle.Bold;
            pill.style.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.15f);
            pill.style.color = accent;
            container.Add(pill);
            return container;
        }

        private static VisualElement CreateKeyValueRow(string key, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 6;
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;

            var keyLabel = new Label(key);
            keyLabel.name = "kv-key";
            keyLabel.style.width = 108;
            keyLabel.style.flexShrink = 0;
            keyLabel.style.color = new Color(0.55f, 0.55f, 0.55f);
            keyLabel.style.fontSize = 11;
            row.Add(keyLabel);

            var valueLabel = new Label(value ?? string.Empty);
            valueLabel.name = "kv-value";
            valueLabel.style.flexGrow = 1;
            valueLabel.style.whiteSpace = WhiteSpace.Normal;
            valueLabel.style.color = new Color(0.88f, 0.88f, 0.88f);
            valueLabel.style.fontSize = 11;
            row.Add(valueLabel);
            return row;
        }

        private enum PanelAccent
        {
            Status,
            Plan,
            Artifacts,
            Diagnostics
        }

        private static VisualElement CreatePanel(string titleText, PanelAccent accent)
        {
            var panel = new VisualElement();
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 4;
            panel.style.borderTopColor = new Color(0.22f, 0.22f, 0.22f);
            panel.style.borderRightColor = new Color(0.22f, 0.22f, 0.22f);
            panel.style.borderBottomColor = new Color(0.22f, 0.22f, 0.22f);
            panel.style.borderLeftColor = GetPanelAccentColor(accent);
            panel.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f);
            panel.style.borderTopRightRadius = 6;
            panel.style.borderBottomRightRadius = 6;
            panel.style.paddingLeft = 12;
            panel.style.paddingRight = 12;
            panel.style.paddingTop = 10;
            panel.style.paddingBottom = 10;
            panel.style.marginBottom = 8;

            var titleContainer = new VisualElement();
            titleContainer.style.flexDirection = FlexDirection.Row;
            titleContainer.style.alignItems = Align.Center;
            titleContainer.style.borderBottomWidth = 1;
            titleContainer.style.borderBottomColor = new Color(0.22f, 0.22f, 0.22f);
            titleContainer.style.paddingBottom = 6;
            titleContainer.style.marginBottom = 8;

            Image icon = CreateIcon(GetPanelIconName(accent), 14);
            if (icon != null)
                titleContainer.Add(icon);

            var title = new Label(titleText);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 12;
            title.style.color = new Color(0.9f, 0.9f, 0.9f);
            title.style.marginLeft = icon != null ? 2 : 0;
            titleContainer.Add(title);
            panel.Add(titleContainer);
            return panel;
        }

        private static Color GetPanelAccentColor(PanelAccent accent)
        {
            switch (accent)
            {
                case PanelAccent.Status:
                    return new Color(0.3f, 0.69f, 0.31f);
                case PanelAccent.Plan:
                    return new Color(0.23f, 0.51f, 0.96f);
                case PanelAccent.Artifacts:
                    return new Color(0.61f, 0.34f, 0.82f);
                case PanelAccent.Diagnostics:
                    return new Color(0.96f, 0.55f, 0.21f);
                default:
                    return new Color(0.23f, 0.51f, 0.96f);
            }
        }

        private static string GetPanelIconName(PanelAccent accent)
        {
            switch (accent)
            {
                case PanelAccent.Status:
                    return "d_Checkmark";
                case PanelAccent.Plan:
                    return "d_CustomTool";
                case PanelAccent.Artifacts:
                    return "d_Folder Icon";
                case PanelAccent.Diagnostics:
                    return "d_console.warnicon.sml";
                default:
                    return "d_Settings";
            }
        }

        private static VisualElement CreateAlertBanner(string title, string content, bool isError)
        {
            var banner = new VisualElement();
            banner.style.paddingLeft = 12;
            banner.style.paddingRight = 12;
            banner.style.paddingTop = 10;
            banner.style.paddingBottom = 10;
            banner.style.borderLeftWidth = 4;
            banner.style.borderTopRightRadius = 6;
            banner.style.borderBottomRightRadius = 6;

            Color leftBarColor = isError ? new Color(0.96f, 0.26f, 0.21f) : new Color(0.3f, 0.69f, 0.31f);
            Color bgColor = isError ? new Color(0.96f, 0.26f, 0.21f, 0.08f) : new Color(0.3f, 0.69f, 0.31f, 0.08f);
            banner.style.borderLeftColor = leftBarColor;
            banner.style.backgroundColor = bgColor;

            var titleContainer = new VisualElement();
            titleContainer.style.flexDirection = FlexDirection.Row;
            titleContainer.style.alignItems = Align.Center;

            Image icon = CreateIcon(isError ? "d_console.erroricon.sml" : "d_Checkmark", 14);
            if (icon != null)
                titleContainer.Add(icon);

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12;
            titleLabel.style.color = leftBarColor;
            titleLabel.style.marginLeft = icon != null ? 2 : 0;
            titleContainer.Add(titleLabel);
            banner.Add(titleContainer);

            var textLabel = new Label(content ?? string.Empty);
            textLabel.AddToClassList("status-content");
            textLabel.style.whiteSpace = WhiteSpace.Normal;
            textLabel.style.marginTop = 6;
            textLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
            textLabel.style.fontSize = 11;
            banner.Add(textLabel);
            return banner;
        }

        private static VisualElement CreateToolbarSeparator()
        {
            var separator = new VisualElement();
            separator.style.width = 1;
            separator.style.height = 18;
            separator.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f);
            separator.style.marginRight = 8;
            separator.style.marginLeft = 4;
            return separator;
        }

        private static Button CreateStyledButton(string text, string iconName, Action onClick, Color? customNormalBg = null)
        {
            var button = new Button(onClick);
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.marginRight = 6;
            button.style.marginBottom = 4;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.paddingTop = 5;
            button.style.paddingBottom = 5;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.borderTopWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;

            Color defBg = customNormalBg ?? new Color(0.24f, 0.24f, 0.24f);
            Color hvrBg = customNormalBg != null
                ? new Color(customNormalBg.Value.r * 1.12f, customNormalBg.Value.g * 1.12f, customNormalBg.Value.b * 1.12f)
                : new Color(0.28f, 0.28f, 0.28f);
            Color borderCol = new Color(0.18f, 0.18f, 0.18f);

            button.style.backgroundColor = defBg;
            button.style.borderTopColor = borderCol;
            button.style.borderRightColor = borderCol;
            button.style.borderBottomColor = borderCol;
            button.style.borderLeftColor = borderCol;

            if (!string.IsNullOrEmpty(iconName))
            {
                Image icon = CreateIcon(iconName, 14);
                if (icon != null)
                    button.Add(icon);
            }

            var label = new Label(text);
            label.style.marginLeft = string.IsNullOrEmpty(iconName) ? 0 : 4;
            label.style.color = new Color(0.88f, 0.88f, 0.88f);
            button.Add(label);

            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                button.style.backgroundColor = hvrBg;
                button.style.borderTopColor = new Color(0.38f, 0.38f, 0.38f);
                button.style.borderRightColor = new Color(0.38f, 0.38f, 0.38f);
                button.style.borderBottomColor = new Color(0.38f, 0.38f, 0.38f);
                button.style.borderLeftColor = new Color(0.38f, 0.38f, 0.38f);
            });

            button.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                button.style.backgroundColor = defBg;
                button.style.borderTopColor = borderCol;
                button.style.borderRightColor = borderCol;
                button.style.borderBottomColor = borderCol;
                button.style.borderLeftColor = borderCol;
            });

            return button;
        }

        private static Button CreateMiniButton(string text, string iconName, Action action)
        {
            var button = new Button(action);
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.width = 74;
            button.style.height = 22;
            button.style.marginRight = 4;
            button.style.paddingLeft = 4;
            button.style.paddingRight = 4;
            button.style.borderTopLeftRadius = 3;
            button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = 3;
            button.style.borderBottomRightRadius = 3;

            if (!string.IsNullOrEmpty(iconName))
            {
                Image icon = CreateIcon(iconName, 12);
                if (icon != null)
                    button.Add(icon);
            }

            var label = new Label(text);
            label.style.fontSize = 10;
            label.style.marginLeft = 2;
            button.Add(label);
            return button;
        }

        private static VisualElement CreateTableRow(bool isHeader, bool zebra)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.minHeight = 28;
            row.style.alignItems = Align.Center;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;

            if (isHeader)
            {
                row.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
                row.style.borderBottomColor = new Color(0.26f, 0.26f, 0.26f);
                row.style.borderBottomWidth = 2;
            }
            else if (zebra)
            {
                row.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
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

        private static Image CreateIcon(string iconName, float size)
        {
            Texture2D tex = FindBuiltInTexture(iconName);
            if (tex == null)
                return null;

            var icon = new Image();
            icon.image = tex;
            icon.style.width = size;
            icon.style.height = size;
            icon.style.marginRight = 4;
            icon.style.alignSelf = Align.Center;
            return icon;
        }

        private static Texture2D FindBuiltInTexture(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            Texture2D tex = EditorGUIUtility.FindTexture(name);
            if (tex != null)
                return tex;

            if (name.StartsWith("d_", StringComparison.Ordinal))
                return EditorGUIUtility.FindTexture(name.Substring(2));

            return EditorGUIUtility.FindTexture("d_" + name);
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

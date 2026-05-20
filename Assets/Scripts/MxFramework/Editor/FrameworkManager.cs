using System.Collections.Generic;
using MxFramework.Config;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    public sealed partial class FrameworkManager : EditorWindow
    {
        private readonly List<VisualElement> _moduleRows = new List<VisualElement>();
        private IReadOnlyList<IConfigEditorSource> _configSources;
        private IReadOnlyList<ConfigIssueView> _configIssues;
        private ConfigHealthReport _configHealth;
        private ConfigChangeReport _configChangeReport;
        private EditorDisplayMode _displayMode;
        private AuthoringPage _authoringPage;
        private ConfigWorkspacePage _configWorkspacePage;
        private VisualElement _contentRoot;
        private VisualElement _configDetailsRoot;
        private Label _validationLabel;
        private Label _runtimeLabel;
        private Label _configHealthLabel;
        private Label _configChangeLabel;
        private Label _configValidationLabel;
        private VisualElement _configFieldInspectorRoot;
        private TextField _configIssuePreview;
        private TextField _configTemplatePreview;
        private PopupField<string> _configIssueFilterPopup;
        private string _selectedConfigSourceName;
        private string _selectedConfigFieldName;
        private string _lastReport;

        [MenuItem("MxFramework/Framework Manager")]
        public static void Open()
        {
            FrameworkManager window = GetWindow<FrameworkManager>();
            window.titleContent = new GUIContent("MxFramework");
            window.minSize = new Vector2(640f, 420f);
            window.Show();
        }

        public static void OpenConfigWorkbench()
        {
            FrameworkManager window = GetWindow<FrameworkManager>();
            window.titleContent = new GUIContent("MxFramework");
            window.minSize = new Vector2(640f, 420f);
            window._displayMode = EditorDisplayMode.Authoring;
            window._authoringPage = AuthoringPage.Config;
            window.Show();
            window.RebuildContent();
        }

        public void CreateGUI()
        {
            _moduleRows.Clear();
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            root.Add(CreateHeader());
            root.Add(CreateToolbar());

            _contentRoot = new ScrollView(ScrollViewMode.Vertical);
            _contentRoot.style.flexGrow = 1;
            _contentRoot.contentContainer.style.flexGrow = 1;
            _contentRoot.style.minHeight = 0;
            _contentRoot.style.marginTop = 10;
            root.Add(_contentRoot);

            RebuildContent();
        }

        private void RebuildContent()
        {
            if (_contentRoot == null)
                return;

            _contentRoot.Clear();
            if (_displayMode == EditorDisplayMode.Runtime)
            {
                _contentRoot.Add(CreateRuntimePanel());
                RefreshRuntimeView();
                return;
            }

            var content = new VisualElement();
            content.style.flexDirection = FlexDirection.Row;
            content.style.flexGrow = 1;

            var authoring = new VisualElement();
            authoring.style.flexDirection = FlexDirection.Column;
            authoring.style.flexGrow = 1;

            authoring.Add(CreateAuthoringTabs());
            if (_authoringPage == AuthoringPage.Config)
            {
                authoring.Add(CreateConfigPanel());
            }
            else if (_authoringPage == AuthoringPage.Tools)
            {
                authoring.Add(CreateToolsPanel());
            }
            else
            {
                content.Add(CreateModulePanel());
                content.Add(CreateValidationPanel());
                authoring.Add(content);
            }

            _contentRoot.Add(authoring);

            if (_authoringPage == AuthoringPage.Config)
                RefreshConfigView();
            else if (_authoringPage == AuthoringPage.Modules)
                RefreshValidation();
        }

        private void SetDisplayMode(EditorDisplayMode mode)
        {
            if (_displayMode == mode)
                return;

            _displayMode = mode;
            RebuildContent();
        }

        private void SetAuthoringPage(AuthoringPage page)
        {
            if (_authoringPage == page)
                return;

            _authoringPage = page;
            RebuildContent();
        }

        private enum EditorDisplayMode
        {
            Authoring,
            Runtime
        }

        private enum AuthoringPage
        {
            Modules,
            Config,
            Tools
        }

        private enum ConfigWorkspacePage
        {
            Overview,
            Fields,
            Rows,
            References
        }
    }
}

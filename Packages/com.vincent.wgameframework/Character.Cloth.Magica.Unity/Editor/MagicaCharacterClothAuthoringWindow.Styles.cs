using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    public sealed partial class MagicaCharacterClothAuthoringWindow
    {
        private GUIStyle _panelStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _selectedCardStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _cardMetaStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _chipStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _diagnosticHeaderStyle;
        private GUIStyle _toolbarTitleStyle;
        private GUIStyle _objectRowStyle;
        private GUIStyle _selectedObjectRowStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _selectedTabStyle;
        private GUIStyle _valueBadgeStyle;
        private GUIStyle _appliedBadgeStyle;
        private string _clothSearch = string.Empty;
        private int _workspaceMode;
        private int _clothParameterCategory;
        private bool _diagnosticsExpanded = true;
        private bool _basicExpanded = true;
        private bool _shapeExpanded = true;
        private bool _transformExpanded = true;
        private bool _advancedClothExpanded;
        private double _nextReceiptReadTime;
        private bool _cachedReceiptReady;

        private void EnsureStyles()
        {
            if (_panelStyle != null) return;
            _panelStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 9, 9), margin = new RectOffset(4, 4, 2, 2) };
            _cardStyle = new GUIStyle("Button") { alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true, padding = new RectOffset(12, 10, 7, 7), fixedHeight = 58f };
            _selectedCardStyle = new GUIStyle(_cardStyle);
            _cardTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _cardMetaStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            _sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _chipStyle = new GUIStyle(EditorStyles.miniButton) { fixedHeight = 22f, padding = new RectOffset(8, 8, 2, 2) };
            _primaryButtonStyle = new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold, fixedHeight = 23f, padding = new RectOffset(10, 10, 2, 2) };
            _diagnosticHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader) { fontStyle = FontStyle.Bold };
            _toolbarTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            _objectRowStyle = new GUIStyle("Button") { alignment = TextAnchor.MiddleLeft, richText = true, fixedHeight = 42f, padding = new RectOffset(10, 8, 4, 4) };
            _selectedObjectRowStyle = new GUIStyle(_objectRowStyle);
            _tabStyle = new GUIStyle(EditorStyles.toolbarButton) { fixedHeight = 28f, alignment = TextAnchor.MiddleCenter };
            _selectedTabStyle = new GUIStyle(_tabStyle) { fontStyle = FontStyle.Bold };
            _valueBadgeStyle = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft, fixedHeight = 20f, padding = new RectOffset(6, 6, 1, 1) };
            _appliedBadgeStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
        }

        private static IDisposable BackgroundColor(Color color) => new GuiColorScope(color);
        private static IDisposable ContentColor(Color color) => new GuiContentColorScope(color);

        private sealed class GuiColorScope : IDisposable
        {
            private readonly Color _previous;
            internal GuiColorScope(Color color) { _previous = GUI.backgroundColor; GUI.backgroundColor = color; }
            public void Dispose() => GUI.backgroundColor = _previous;
        }

        private sealed class GuiContentColorScope : IDisposable
        {
            private readonly Color _previous;
            internal GuiContentColorScope(Color color) { _previous = GUI.contentColor; GUI.contentColor = color; }
            public void Dispose() => GUI.contentColor = _previous;
        }

        private void StatusChip(string label, bool ready, float width)
        {
            using (BackgroundColor(ready ? new Color(0.32f, 0.58f, 0.36f) : new Color(0.45f, 0.45f, 0.45f)))
                GUILayout.Label((ready ? "●  " : "○  ") + label, _chipStyle, GUILayout.Width(width));
        }

        private bool WorkspaceTab(string label, int mode)
        {
            bool selected = _workspaceMode == mode;
            using (BackgroundColor(selected ? new Color(0.22f, 0.46f, 0.72f) : GUI.backgroundColor))
                if (GUILayout.Button(label, selected ? _selectedTabStyle : _tabStyle))
                    _workspaceMode = mode;
            return selected;
        }

        private bool ParameterTab(string label, int category)
        {
            bool selected = _clothParameterCategory == category;
            using (BackgroundColor(selected ? new Color(0.20f, 0.48f, 0.76f) : GUI.backgroundColor))
                if (GUILayout.Button(label, selected ? _selectedTabStyle : _tabStyle, GUILayout.Width(112f)))
                    _clothParameterCategory = category;
            return selected;
        }

        private static string Leaf(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "未选择";
            string normalized = path.Replace('\\', '/').TrimEnd('/');
            int slash = normalized.LastIndexOf('/');
            return slash >= 0 ? normalized.Substring(slash + 1) : normalized;
        }

        private bool HasCollider(string id)
        {
            MagicaCharacterClothAuthoringCollider[] colliders = SelectedCloth()?.colliders ?? Array.Empty<MagicaCharacterClothAuthoringCollider>();
            for (int i = 0; i < colliders.Length; i++)
                if (string.Equals(colliders[i]?.id, id, StringComparison.Ordinal)) return true;
            return false;
        }

        private bool ReadReceiptReady()
        {
            if (EditorApplication.timeSinceStartup < _nextReceiptReadTime) return _cachedReceiptReady;
            _nextReceiptReadTime = EditorApplication.timeSinceStartup + 1d;
            if (string.IsNullOrWhiteSpace(_session?.packageId)) { _cachedReceiptReady = false; return false; }
            string path = Path.GetFullPath(
                "Assets/MxFrameworkGenerated/CharacterPackages/" + _session.packageId
                + "/package_cache/unity_editor_import_receipt.json");
            if (!File.Exists(path)) { _cachedReceiptReady = false; return false; }
            try { _cachedReceiptReady = (bool?)JObject.Parse(File.ReadAllText(path))["ready"] == true; }
            catch (Newtonsoft.Json.JsonException) { _cachedReceiptReady = false; }
            return _cachedReceiptReady;
        }

        private void InvalidateReceiptCache()
        {
            _nextReceiptReadTime = 0d;
            _cachedReceiptReady = false;
        }
    }
}

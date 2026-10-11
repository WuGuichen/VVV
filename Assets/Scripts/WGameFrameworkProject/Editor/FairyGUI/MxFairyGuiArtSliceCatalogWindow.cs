using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    internal sealed class MxFairyGuiArtSliceCatalogWindow : EditorWindow
    {
        private const string MenuPath = "MxFramework/FairyGUI/UI Art Slice Catalog";
        private const string PackageId = "MxCharacterTestHud";
        private const string PackageRootPath = "FGUIProject/assets/MxCharacterTestHud";
        private const string CatalogPath = "FGUIProject/assets/MxCharacterTestHud/ui-art-slices.json";
        private const string ManifestTestProjectPath = "Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj";
        private const string DefaultReportPath = "Temp/fairygui-art-slices.editor-report.md";
        private const string ExternalEditorUrl = "http://127.0.0.1:4873/Tools/MxFramework.FairyGuiArtSlices/web/?library=MxUiFoundation";
        private const string UnavailableStatus =
            "Unavailable: FGUIProject catalog is not present in this consumer project.";

        private static readonly string[] CategoryOptions =
        {
            "PanelFrame",
            "ButtonState",
            "Icon",
            "BarTrack",
            "BarFill",
            "Divider",
            "Badge",
            "InputBackground",
            "Decoration",
            "Ignored"
        };

        private readonly List<MxFairyGuiArtSliceEditorSliceRecord> _slices = new List<MxFairyGuiArtSliceEditorSliceRecord>();
        private readonly Dictionary<string, Texture2D> _previewCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private JObject _catalogRoot;
        private string _selectedSliceId = string.Empty;
        private string _filter = string.Empty;
        private string _status = string.Empty;
        private string _editDisplayName = string.Empty;
        private string _editDescription = string.Empty;
        private string _editCategory = string.Empty;
        private string _editTags = string.Empty;
        private string _editGroupId = string.Empty;
        private string _editRoleInGroup = string.Empty;
        private bool _editRequireAlphaChannel;
        private bool _editRequireNonOpaqueAlpha;
        private bool _editRequireSamePixelSize;
        private bool _editReplaceTogether;
        private int _editMinPixelWidth;
        private int _editMinPixelHeight;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<MxFairyGuiArtSliceCatalogWindow>();
            window.titleContent = new GUIContent("UI Art Slices");
            window.minSize = new Vector2(900f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            _status = "UI art resources are edited in the external EditorHub page.";
        }

        private void OnDisable()
        {
            ClearPreviewCache();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "UI Art Resource Library editing has moved to the external EditorHub tool. This Unity window is only a thin entry/debug fallback.",
                MessageType.Info);

            if (GUILayout.Button("Open External UI Art Resource Library", GUILayout.Height(32f)))
                Application.OpenURL(ExternalEditorUrl);

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Generate MxCharacterTestHud Audit Report", GUILayout.Height(28f)))
                GenerateReport();

            EditorGUILayout.Space(8f);
            DrawStatusBar();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                Reload();

            if (GUILayout.Button("Generate Report", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                GenerateReport();

            if (GUILayout.Button("Open External Editor", EditorStyles.toolbarButton, GUILayout.Width(140f)))
                Application.OpenURL(ExternalEditorUrl);

            if (GUILayout.Button("Open Catalog", EditorStyles.toolbarButton, GUILayout.Width(95f)))
                OpenFile(CatalogAbsolutePath);

            if (GUILayout.Button("Reveal Package", EditorStyles.toolbarButton, GUILayout.Width(105f)))
                EditorUtility.RevealInFinder(PackageRootAbsolutePath);

            GUILayout.Space(8f);
            GUILayout.Label("Filter", GUILayout.Width(36f));
            _filter = GUILayout.TextField(_filter, EditorStyles.toolbarTextField, GUILayout.MinWidth(160f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(PackageId, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSliceList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label("Slices", EditorStyles.boldLabel);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUI.skin.box);
            for (int i = 0; i < _slices.Count; i++)
            {
                MxFairyGuiArtSliceEditorSliceRecord slice = _slices[i];
                if (!MatchesFilter(slice))
                    continue;

                GUIStyle style = string.Equals(slice.SliceId, _selectedSliceId, StringComparison.Ordinal)
                    ? EditorStyles.helpBox
                    : GUI.skin.button;
                string label = string.IsNullOrWhiteSpace(slice.DisplayName)
                    ? slice.SliceId
                    : slice.DisplayName;
                if (GUILayout.Button(slice.Category + " | " + label, style))
                    SelectSlice(slice.SliceId);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical();
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            MxFairyGuiArtSliceEditorSliceRecord selected = FindSelectedSlice();
            if (selected == null)
            {
                EditorGUILayout.HelpBox("Select a slice to inspect semantics, usage, preview, and replacement constraints.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            DrawSliceHeader(selected);
            EditorGUILayout.Space(6f);
            DrawPreview(selected);
            EditorGUILayout.Space(6f);
            DrawSemanticEditor(selected);
            EditorGUILayout.Space(6f);
            DrawUsageList(selected);
            EditorGUILayout.Space(6f);
            DrawReplacementControls(selected);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSliceHeader(MxFairyGuiArtSliceEditorSliceRecord selected)
        {
            GUILayout.Label(selected.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Slice Id", selected.SliceId);
            EditorGUILayout.LabelField("Resource Id", selected.ResourceId);
            EditorGUILayout.LabelField("Package Source", selected.SourcePath);
            EditorGUILayout.LabelField("Expected Source", selected.ExpectedSourcePath);
            if (!string.Equals(selected.SourcePath, selected.ExpectedSourcePath, StringComparison.Ordinal))
                EditorGUILayout.HelpBox("expectedSourcePath differs from package.xml. package.xml remains the resource authority.", MessageType.Warning);

            if (!File.Exists(ResolvePackagePath(selected.SourcePath)))
                EditorGUILayout.HelpBox("Source PNG is missing: " + selected.SourcePath, MessageType.Error);
        }

        private void DrawPreview(MxFairyGuiArtSliceEditorSliceRecord selected)
        {
            Texture2D texture = LoadPreview(selected.SourcePath);
            if (texture == null)
            {
                EditorGUILayout.HelpBox("Preview unavailable. The source may be outside Unity import scope or the PNG may be missing.", MessageType.Warning);
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(180f, 180f, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, alphaBlend: true);
            EditorGUILayout.LabelField("Pixel Size", texture.width.ToString(CultureInfo.InvariantCulture) + " x " + texture.height.ToString(CultureInfo.InvariantCulture));
        }

        private void DrawSemanticEditor(MxFairyGuiArtSliceEditorSliceRecord selected)
        {
            GUILayout.Label("Catalog Semantics", EditorStyles.boldLabel);
            int categoryIndex = IndexOfCategory(_editCategory);
            int nextCategoryIndex = EditorGUILayout.Popup("Category", categoryIndex, CategoryOptions);
            if (nextCategoryIndex >= 0 && nextCategoryIndex < CategoryOptions.Length)
                _editCategory = CategoryOptions[nextCategoryIndex];

            _editDisplayName = EditorGUILayout.TextField("Display Name", _editDisplayName);
            EditorGUILayout.LabelField("Description");
            _editDescription = EditorGUILayout.TextArea(_editDescription, GUILayout.MinHeight(50f));
            _editTags = EditorGUILayout.TextField("Tags", _editTags);
            _editGroupId = EditorGUILayout.TextField("Group Id", _editGroupId);
            _editRoleInGroup = EditorGUILayout.TextField("Role In Group", _editRoleInGroup);

            GUILayout.Label("Replacement Constraints", EditorStyles.boldLabel);
            _editRequireAlphaChannel = EditorGUILayout.Toggle("Require Alpha Channel", _editRequireAlphaChannel);
            _editRequireNonOpaqueAlpha = EditorGUILayout.Toggle("Require Non-Opaque Alpha", _editRequireNonOpaqueAlpha);
            _editRequireSamePixelSize = EditorGUILayout.Toggle("Require Same Pixel Size", _editRequireSamePixelSize);
            _editReplaceTogether = EditorGUILayout.Toggle("Replace Together", _editReplaceTogether);
            _editMinPixelWidth = EditorGUILayout.IntField("Min Pixel Width", _editMinPixelWidth);
            _editMinPixelHeight = EditorGUILayout.IntField("Min Pixel Height", _editMinPixelHeight);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Catalog Edits", GUILayout.Width(150f)))
                SaveSelectedCatalogEdits(selected);

            if (GUILayout.Button("Discard And Reload", GUILayout.Width(140f)))
                Reload();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawUsageList(MxFairyGuiArtSliceEditorSliceRecord selected)
        {
            GUILayout.Label("Direct XML Usages", EditorStyles.boldLabel);
            if (selected.Usages.Count == 0)
            {
                EditorGUILayout.HelpBox("No direct <image src> usage found for this resource in current package component XML.", MessageType.Info);
                return;
            }

            for (int i = 0; i < selected.Usages.Count; i++)
            {
                MxFairyGuiArtSliceEditorUsageRecord usage = selected.Usages[i];
                EditorGUILayout.LabelField(usage.ComponentPath, usage.ControlName + " [" + usage.NodeId + "] " + usage.Size);
            }
        }

        private void DrawReplacementControls(MxFairyGuiArtSliceEditorSliceRecord selected)
        {
            GUILayout.Label("Low-Risk PNG Replacement", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Replacement keeps the existing package path and resource id. It does not edit package.xml, component XML, atlas, or *_fui.bytes. Republish through FairyGUI Editor after replacement.", MessageType.Info);
            if (!string.IsNullOrWhiteSpace(selected.GroupId) || selected.ReplaceTogether)
                EditorGUILayout.HelpBox("This slice belongs to a group or is marked replaceTogether. Review sibling states before replacing only this PNG.", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Replace PNG...", GUILayout.Width(130f)))
                ReplaceSelectedPng(selected);

            if (GUILayout.Button("Reveal PNG", GUILayout.Width(100f)))
                EditorUtility.RevealInFinder(ResolvePackagePath(selected.SourcePath));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(_status, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void Reload()
        {
            ClearPreviewCache();
            _slices.Clear();
            _catalogRoot = null;

            if (!Directory.Exists(PackageRootAbsolutePath)
                || !File.Exists(CatalogAbsolutePath))
            {
                _status = UnavailableStatus;
                return;
            }

            try
            {
                MxFairyGuiArtSliceEditorDocument document = MxFairyGuiArtSliceCatalogEditorService.LoadDocument(
                    RepositoryRoot,
                    PackageRootPath,
                    CatalogPath);
                _catalogRoot = document.CatalogRoot;
                for (int i = 0; i < document.Slices.Count; i++)
                    _slices.Add(document.Slices[i]);

                if (_slices.Count > 0 && string.IsNullOrWhiteSpace(_selectedSliceId))
                    SelectSlice(_slices[0].SliceId);
                else
                    LoadEditFields(FindSelectedSlice());

                _status = "Loaded " + _slices.Count.ToString(CultureInfo.InvariantCulture) + " slices from " + CatalogPath + ".";
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is System.Xml.XmlException)
            {
                _status = "Failed to load catalog: " + ex.Message;
            }
        }

        private void SelectSlice(string sliceId)
        {
            _selectedSliceId = sliceId ?? string.Empty;
            LoadEditFields(FindSelectedSlice());
        }

        private void LoadEditFields(MxFairyGuiArtSliceEditorSliceRecord record)
        {
            if (record == null)
                return;

            _editDisplayName = record.DisplayName;
            _editDescription = record.Description;
            _editCategory = string.IsNullOrWhiteSpace(record.Category) ? CategoryOptions[0] : record.Category;
            _editTags = record.Tags;
            _editGroupId = record.GroupId;
            _editRoleInGroup = record.RoleInGroup;
            _editRequireAlphaChannel = record.RequireAlphaChannel;
            _editRequireNonOpaqueAlpha = record.RequireNonOpaqueAlpha;
            _editRequireSamePixelSize = record.RequireSamePixelSize;
            _editReplaceTogether = record.ReplaceTogether;
            _editMinPixelWidth = record.MinPixelWidth;
            _editMinPixelHeight = record.MinPixelHeight;
        }

        private void SaveSelectedCatalogEdits(MxFairyGuiArtSliceEditorSliceRecord selected)
        {
            if (selected == null || selected.Json == null)
                return;

            MxFairyGuiArtSliceEditorOperationPlan plan =
                MxFairyGuiArtSliceCatalogEditorOperationPlanner.PlanSaveCatalogSemantics(CatalogPath, selected);
            if (ShowBlockedPlan("Save Catalog blocked", plan))
                return;

            selected.Json["category"] = _editCategory ?? string.Empty;
            selected.Json["displayName"] = _editDisplayName ?? string.Empty;
            selected.Json["description"] = _editDescription ?? string.Empty;
            selected.Json["tags"] = MxFairyGuiArtSliceCatalogEditorService.ToTagArray(_editTags);
            selected.Json["groupId"] = _editGroupId ?? string.Empty;
            selected.Json["roleInGroup"] = _editRoleInGroup ?? string.Empty;
            JObject replacement = selected.Json["replacement"] as JObject;
            if (replacement == null)
            {
                replacement = new JObject();
                selected.Json["replacement"] = replacement;
            }

            replacement["requireAlphaChannel"] = _editRequireAlphaChannel;
            replacement["requireNonOpaqueAlpha"] = _editRequireNonOpaqueAlpha;
            replacement["requireSamePixelSize"] = _editRequireSamePixelSize;
            replacement["replaceTogether"] = _editReplaceTogether;
            replacement["minPixelWidth"] = Math.Max(0, _editMinPixelWidth);
            replacement["minPixelHeight"] = Math.Max(0, _editMinPixelHeight);

            MxFairyGuiArtSliceCatalogEditorService.SaveCatalog(CatalogAbsolutePath, _catalogRoot);
            AssetDatabase.Refresh();
            _status = "Saved catalog edits to " + CatalogPath + ". " + plan.Summary;
            Reload();
        }

        private void GenerateReport()
        {
            MxFairyGuiArtSliceEditorReportResult result = MxFairyGuiArtSliceCatalogEditorService.GenerateReport(
                RepositoryRoot,
                ManifestTestProjectPath,
                DefaultReportPath);
            _status = result.Message;
            if (result.Success)
            {
                OpenFile(result.ReportPath);
            }
        }

        private void ReplaceSelectedPng(MxFairyGuiArtSliceEditorSliceRecord selected)
        {
            string targetPath = ResolvePackagePath(selected.SourcePath);
            string newPath = EditorUtility.OpenFilePanel("Select replacement PNG", string.Empty, "png");
            if (string.IsNullOrWhiteSpace(newPath))
                return;

            MxFairyGuiArtSliceEditorOperationPlan plan =
                MxFairyGuiArtSliceCatalogEditorOperationPlanner.PlanReplaceExistingPng(
                    PackageRootAbsolutePath,
                    selected,
                    targetPath,
                    newPath);
            if (ShowBlockedPlan("Replacement blocked", plan))
                return;

            if (!EditorUtility.DisplayDialog("Replace PNG", plan.ToDisplayText() + Environment.NewLine + Environment.NewLine + "Continue?", "Replace", "Cancel"))
                return;

            try
            {
                MxFairyGuiArtSliceCatalogEditorService.ReplaceFileAtomically(newPath, targetPath);
                ClearPreviewCache();
                AssetDatabase.Refresh();
                _status = "Replaced " + selected.SourcePath + ". Republish the FairyGUI package before runtime validation. " + plan.Summary;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                EditorUtility.DisplayDialog("Replacement failed", ex.Message, "OK");
                _status = "Replacement failed: " + ex.Message;
            }
        }

        private Texture2D LoadPreview(string sourcePath)
        {
            string absolutePath = ResolvePackagePath(sourcePath);
            Texture2D texture;
            if (_previewCache.TryGetValue(absolutePath, out texture))
                return texture;

            if (!File.Exists(absolutePath))
                return null;

            byte[] bytes = File.ReadAllBytes(absolutePath);
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            if (!texture.LoadImage(bytes))
            {
                DestroyImmediate(texture);
                return null;
            }

            _previewCache[absolutePath] = texture;
            return texture;
        }

        private void ClearPreviewCache()
        {
            foreach (KeyValuePair<string, Texture2D> pair in _previewCache)
            {
                if (pair.Value != null)
                    DestroyImmediate(pair.Value);
            }

            _previewCache.Clear();
        }

        private MxFairyGuiArtSliceEditorSliceRecord FindSelectedSlice()
        {
            for (int i = 0; i < _slices.Count; i++)
            {
                if (string.Equals(_slices[i].SliceId, _selectedSliceId, StringComparison.Ordinal))
                    return _slices[i];
            }

            return null;
        }

        private bool ShowBlockedPlan(string title, MxFairyGuiArtSliceEditorOperationPlan plan)
        {
            if (!plan.Blocked)
                return false;

            EditorUtility.DisplayDialog(title, plan.ToDisplayText(), "OK");
            _status = title + ": " + plan.Summary;
            return true;
        }

        private bool MatchesFilter(MxFairyGuiArtSliceEditorSliceRecord slice)
        {
            if (slice == null)
                return false;

            if (string.IsNullOrWhiteSpace(_filter))
                return true;

            string filter = _filter.Trim();
            return Contains(slice.SliceId, filter)
                || Contains(slice.ResourceId, filter)
                || Contains(slice.DisplayName, filter)
                || Contains(slice.Category, filter)
                || Contains(slice.SourcePath, filter);
        }

        private static bool Contains(string value, string filter)
        {
            return (value ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int IndexOfCategory(string category)
        {
            for (int i = 0; i < CategoryOptions.Length; i++)
            {
                if (string.Equals(CategoryOptions[i], category, StringComparison.Ordinal))
                    return i;
            }

            return 0;
        }

        private static void OpenFile(string path)
        {
            if (File.Exists(path))
                EditorUtility.OpenWithDefaultApp(path);
            else
                EditorUtility.DisplayDialog("File missing", path, "OK");
        }

        private static string ResolvePackagePath(string packageRelativePath)
        {
            return MxFairyGuiArtSliceCatalogEditorService.ResolvePackagePath(PackageRootAbsolutePath, packageRelativePath);
        }

        private static string RepositoryRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string PackageRootAbsolutePath => MxFairyGuiArtSliceCatalogEditorService.GetPackageRootAbsolutePath(RepositoryRoot, PackageRootPath);
        private static string CatalogAbsolutePath => MxFairyGuiArtSliceCatalogEditorService.GetCatalogAbsolutePath(RepositoryRoot, CatalogPath);
    }
}

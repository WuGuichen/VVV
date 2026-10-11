using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed class CharacterActionVfxSelectionOption
    {
        internal ResourceKey ResourceKey;
        internal string DisplayName = string.Empty;
        internal string Description = string.Empty;
        internal string AssetPath = string.Empty;
        internal string Role = string.Empty;
        internal string Theme = string.Empty;
        internal string Anchor = string.Empty;
        internal string Lifecycle = string.Empty;
        internal string Maturity = string.Empty;
        internal string Review = string.Empty;
        internal Texture2D Thumbnail;
        internal bool IsCandidate;
        internal bool IsUnboundCandidate;
        internal bool HasContractWarning;
        internal bool Selectable = true;
        internal string Diagnostic = string.Empty;
        internal VfxCompositionBakeReceipt Receipt;

        internal string StatusLabel => IsUnboundCandidate ? "草稿候选" : IsCandidate ? "本机 Candidate" : Maturity;

        internal bool Matches(string query, string role, string theme)
        {
            if (!string.IsNullOrWhiteSpace(role)
                && !string.Equals(role, "全部", StringComparison.Ordinal)
                && !string.Equals(Role, role, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrWhiteSpace(theme)
                && !string.Equals(theme, "全部", StringComparison.Ordinal)
                && !string.Equals(Theme, theme, StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrWhiteSpace(query))
                return true;

            string[] terms = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string haystack = string.Join("|", new[]
            {
                DisplayName,
                Description,
                ResourceKey.Id,
                Role,
                Theme,
                Anchor,
                Lifecycle,
                StatusLabel
            });
            for (int i = 0; i < terms.Length; i++)
                if (haystack.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            return true;
        }
    }

    /// <summary>Editor-only, AllocByDesign projection used by CharacterAction VFX pickers.</summary>
    internal sealed class CharacterActionVfxSelectionCatalog : IDisposable
    {
        private readonly VfxLibraryWorkspace _workspace;
        internal readonly List<CharacterActionVfxSelectionOption> Options =
            new List<CharacterActionVfxSelectionOption>();

        private CharacterActionVfxSelectionCatalog(VfxLibraryWorkspace workspace)
        {
            _workspace = workspace;
        }

        internal static CharacterActionVfxSelectionCatalog Load(string candidateBindingResourceKey = null)
        {
            VfxLibraryWorkspace workspace = VfxLibraryWorkspace.Load(
                VfxAuthoringResourceLibraryWindow.CatalogPath,
                VfxAuthoringResourceLibraryWindow.ThumbnailRoot);
            var catalog = new CharacterActionVfxSelectionCatalog(workspace);
            for (int i = 0; i < workspace.Effects.Count; i++)
            {
                VfxLibraryEffect effect = workspace.Effects[i];
                JObject keySource = effect.Source?["resourceKey"] as JObject;
                ResourceKey key = new ResourceKey(
                    keySource?.Value<string>("id") ?? effect.EffectId,
                    keySource?.Value<string>("typeId") ?? ResourceTypeIds.GameObject,
                    keySource?.Value<string>("variant") ?? string.Empty,
                    keySource?.Value<string>("packageId") ?? string.Empty);
                var option = new CharacterActionVfxSelectionOption
                {
                    ResourceKey = key,
                    DisplayName = string.IsNullOrWhiteSpace(effect.DisplayName) ? effect.EffectId : effect.DisplayName,
                    Description = effect.Description,
                    AssetPath = effect.AssetPath,
                    Role = effect.PrimaryRole,
                    Theme = effect.PrimaryTheme,
                    Anchor = effect.AnchorSummary,
                    Lifecycle = effect.LifecycleSummary,
                    Maturity = effect.VisualMaturity,
                    Review = effect.ReviewStatus,
                    Thumbnail = effect.Thumbnail,
                    Diagnostic = string.IsNullOrWhiteSpace(effect.AssetPath) ? "未找到正式 Prefab。" : string.Empty,
                    Selectable = !string.IsNullOrWhiteSpace(effect.AssetPath)
                };
                catalog.Options.Add(option);
            }

            catalog.Options.Sort(CompareOptions);
            return catalog;
        }

        internal CharacterActionVfxSelectionOption FindCurrent(string serializedResourceKey)
        {
            if (!TryParseResourceKey(serializedResourceKey, out ResourceKey key))
                return null;
            return Options.FirstOrDefault(value => !value.IsCandidate && value.ResourceKey == key);
        }

        public void Dispose()
        {
            _workspace?.Dispose();
        }

        internal static bool TryParseResourceKey(string value, out ResourceKey key)
        {
            key = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string text = value.Trim();
            int packageIndex = text.LastIndexOf('@');
            string package = packageIndex >= 0 ? text.Substring(packageIndex + 1) : string.Empty;
            if (packageIndex >= 0)
                text = text.Substring(0, packageIndex);
            int variantIndex = text.LastIndexOf('#');
            string variant = variantIndex >= 0 ? text.Substring(variantIndex + 1) : string.Empty;
            if (variantIndex >= 0)
                text = text.Substring(0, variantIndex);
            int typeIndex = text.LastIndexOf(':');
            string type = typeIndex >= 0 ? text.Substring(typeIndex + 1) : ResourceTypeIds.GameObject;
            string id = typeIndex >= 0 ? text.Substring(0, typeIndex) : text;
            key = new ResourceKey(id, type, variant, package);
            return key.IsValid;
        }

        private void AppendCandidates(
            IReadOnlyDictionary<string, CharacterActionVfxSelectionOption> published,
            bool hasCandidateBinding,
            ResourceKey candidateBinding)
        {
            if (!Directory.Exists(VfxCompositionCandidateBaker.ReceiptRoot))
                return;

            string[] paths = Directory.GetFiles(VfxCompositionCandidateBaker.ReceiptRoot, "*.json");
            Array.Sort(paths, StringComparer.Ordinal);
            for (int i = 0; i < paths.Length; i++)
            {
                VfxCompositionBakeReceipt receipt;
                try
                {
                    JObject raw = JObject.Parse(File.ReadAllText(paths[i]));
                    string receiptHash = raw.Value<string>("receiptHash") ?? string.Empty;
                    if (!VfxPublishedBindingEditorService.TryReadAndValidateReceipt(paths[i], receiptHash, out receipt))
                        continue;
                }
                catch
                {
                    continue;
                }

                bool isBound = TryParseResourceKey(receipt.ConsumerResourceKey, out ResourceKey key);
                if (!isBound)
                {
                    if (!hasCandidateBinding)
                        continue;
                    key = candidateBinding;
                }
                if (string.IsNullOrWhiteSpace(receipt.OutputPrefabPath))
                    continue;

                published.TryGetValue(key.Id, out CharacterActionVfxSelectionOption basis);
                VfxConsumerContract contract = VfxConsumerContractResolver.Find(key);
                VfxConsumerCompatibilityReport compatibility = isBound || contract == null
                    ? null
                    : VfxConsumerCompatibilityValidator.Validate(contract, receipt);
                VfxConsumerCompatibilityReport artifact = isBound
                    ? null
                    : VfxConsumerCompatibilityValidator.ValidateCandidateArtifact(receipt);
                bool contractCurrent = contract != null && (isBound
                    ? string.Equals(contract.ContractHash, receipt.ConsumerContractHash, StringComparison.OrdinalIgnoreCase)
                    : artifact.Compatible);
                Texture2D thumbnail = null;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(receipt.OutputPrefabPath);
                if (prefab != null)
                    thumbnail = AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab);
                var option = new CharacterActionVfxSelectionOption
                {
                    ResourceKey = key,
                    DisplayName = isBound
                        ? (basis?.DisplayName ?? key.Id) + " · Candidate"
                        : receipt.DraftId + " · 草稿候选",
                    Description = isBound
                        ? "由 VFX 创作工作台烘焙的本机候选。"
                        : "由 VFX 创作工作台烘焙、尚未绑定技能槽位的草稿候选。",
                    AssetPath = receipt.OutputPrefabPath,
                    Role = basis?.Role ?? ContractRole(key),
                    Theme = basis?.Theme ?? "generic",
                    Anchor = basis?.Anchor ?? contract?.Anchor ?? "-",
                    Lifecycle = basis?.Lifecycle ?? contract?.LoopMode ?? "-",
                    Maturity = "Candidate",
                    Review = "Local",
                    Thumbnail = thumbnail ?? basis?.Thumbnail,
                    IsCandidate = true,
                    IsUnboundCandidate = !isBound,
                    HasContractWarning = !isBound && compatibility?.Compatible == false,
                    Selectable = contractCurrent,
                    Diagnostic = !isBound
                        ? contractCurrent
                            ? compatibility?.Compatible == false
                                ? "可作为本机预览覆盖，但不满足当前技能发布合同：\n" + compatibility.Summary
                                : "可绑定到当前技能槽位；确认后将作为本机试玩覆盖。"
                            : artifact?.Summary ?? "当前技能槽位没有可用的 VFX 消费合同。"
                        : contractCurrent
                            ? "有效的本机试玩候选。"
                            : "技能合同已变化，请在 VFX 工作台重新烘焙该 Candidate。",
                    Receipt = receipt
                };
                Options.Add(option);
            }
        }

        private static string ContractRole(ResourceKey key)
        {
            string id = key.Id ?? string.Empty;
            if (id.EndsWith(".charge", StringComparison.OrdinalIgnoreCase)) return "Charge";
            if (id.EndsWith(".flight", StringComparison.OrdinalIgnoreCase)) return "Flight";
            if (id.EndsWith(".impact", StringComparison.OrdinalIgnoreCase)) return "Impact";
            return "Other";
        }

        private static int CompareOptions(CharacterActionVfxSelectionOption left, CharacterActionVfxSelectionOption right)
        {
            int result = string.Compare(left.ResourceKey.Id, right.ResourceKey.Id, StringComparison.Ordinal);
            if (result != 0) return result;
            if (left.IsCandidate == right.IsCandidate) return 0;
            return left.IsCandidate ? -1 : 1;
        }
    }

    internal sealed class CharacterActionVfxPickerWindow : EditorWindow
    {
        private const float PickerWidth = 1060f;
        private const float PickerHeight = 680f;
        private const float RowHeight = 92f;

        private readonly List<CharacterActionVfxSelectionOption> _visible =
            new List<CharacterActionVfxSelectionOption>();
        private CharacterActionVfxSelectionCatalog _catalog;
        private Action<CharacterActionVfxSelectionOption> _onSelected;
        private Func<CharacterActionVfxSelectionOption, string> _contractDiagnostic;
        private Func<CharacterActionVfxSelectionOption, string> _selectionBlockDiagnostic;
        private string _currentKey = string.Empty;
        private string _expectedRole = string.Empty;
        private ToolbarSearchField _search;
        private DropdownField _role;
        private DropdownField _theme;
        private ListView _list;
        private Label _title;
        private Label _details;
        private Label _diagnostic;
        private Button _use;
        private Button _openWorkbench;
        private Button _ping;
        private VfxLibraryPreviewSession _preview;
        private IMGUIContainer _previewCanvas;

        internal static void ShowPicker(
            Rect activatorScreenRect,
            string currentResourceKey,
            string expectedRole,
            Action<CharacterActionVfxSelectionOption> onSelected,
            Func<CharacterActionVfxSelectionOption, string> contractDiagnostic = null,
            Func<CharacterActionVfxSelectionOption, string> selectionBlockDiagnostic = null)
        {
            var window = CreateInstance<CharacterActionVfxPickerWindow>();
            window._currentKey = currentResourceKey ?? string.Empty;
            window._expectedRole = expectedRole ?? string.Empty;
            window._onSelected = onSelected;
            window._contractDiagnostic = contractDiagnostic;
            window._selectionBlockDiagnostic = selectionBlockDiagnostic;
            window.titleContent = new GUIContent("选择 VFX");
            float x = Mathf.Max(0f, activatorScreenRect.xMax - PickerWidth);
            float y = Mathf.Max(0f, activatorScreenRect.yMax + 4f);
            window.position = new Rect(x, y, PickerWidth, PickerHeight);
            window.minSize = new Vector2(880f, 560f);
            window.ShowAuxWindow();
        }

        public void CreateGUI()
        {
            BuildUi();
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _preview?.Dispose();
            _preview = null;
            _catalog?.Dispose();
            _catalog = null;
        }

        private void BuildUi()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;
            root.style.backgroundColor = new Color(0.075f, 0.09f, 0.105f);

            _catalog = CharacterActionVfxSelectionCatalog.Load(_currentKey);
            var header = new Label("选择技能 VFX");
            header.style.fontSize = 17f;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 8f;
            root.Add(header);

            var body = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1f } };
            root.Add(body);

            VisualElement browser = BuildBrowser();
            browser.style.width = 430f;
            browser.style.marginRight = 10f;
            body.Add(browser);
            body.Add(BuildPreviewPanel());

            RefreshFilters();
            SelectInitial();
        }

        private VisualElement BuildBrowser()
        {
            var browser = new VisualElement();
            _search = new ToolbarSearchField();
            _search.tooltip = "搜索名称、ResourceKey、职责、主题或挂点";
            _search.RegisterValueChangedCallback(_ => RefreshVisible());
            browser.Add(_search);

            var filters = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginTop = 6f, marginBottom = 6f }
            };
            _role = new DropdownField("职责");
            _role.style.flexGrow = 1f;
            _role.RegisterValueChangedCallback(_ => RefreshVisible());
            filters.Add(_role);
            _theme = new DropdownField("主题");
            _theme.style.flexGrow = 1f;
            _theme.style.marginLeft = 6f;
            _theme.RegisterValueChangedCallback(_ => RefreshVisible());
            filters.Add(_theme);
            browser.Add(filters);

            _list = new ListView
            {
                itemsSource = _visible,
                fixedItemHeight = RowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow
            };
            _list.style.flexGrow = 1f;
            _list.selectionChanged += _ => RefreshSelection();
            _list.itemsChosen += _ => CommitSelection();
            browser.Add(_list);
            return browser;
        }

        private VisualElement BuildPreviewPanel()
        {
            var panel = new VisualElement { style = { flexGrow = 1f, minWidth = 0f } };
            _title = new Label("请选择 VFX");
            _title.style.fontSize = 15f;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(_title);

            var previewHost = new VisualElement();
            previewHost.style.flexGrow = 1f;
            previewHost.style.minHeight = 330f;
            previewHost.style.marginTop = 6f;
            previewHost.style.marginBottom = 6f;
            previewHost.style.borderTopWidth = 1f;
            previewHost.style.borderBottomWidth = 1f;
            previewHost.style.borderLeftWidth = 1f;
            previewHost.style.borderRightWidth = 1f;
            Color border = new Color(0.18f, 0.25f, 0.29f);
            previewHost.style.borderTopColor = border;
            previewHost.style.borderBottomColor = border;
            previewHost.style.borderLeftColor = border;
            previewHost.style.borderRightColor = border;
            _preview = new VfxLibraryPreviewSession();
            _previewCanvas = new IMGUIContainer(() =>
            {
                Rect previewRect = previewHost.contentRect;
                if (previewRect.width > 1f && previewRect.height > 1f)
                    _preview.Draw(previewRect);
            });
            previewHost.Add(_previewCanvas);
            panel.Add(previewHost);

            var playback = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            playback.Add(new Button(() => _preview.Play()) { text = "▶ 播放" });
            playback.Add(new Button(() => _preview.Pause()) { text = "Ⅱ 暂停" });
            playback.Add(new Button(() => _preview.Restart()) { text = "↻ 重播" });
            panel.Add(playback);

            _details = new Label();
            _details.style.whiteSpace = WhiteSpace.Normal;
            _details.style.marginTop = 8f;
            panel.Add(_details);
            _diagnostic = new Label();
            _diagnostic.style.whiteSpace = WhiteSpace.Normal;
            _diagnostic.style.marginTop = 4f;
            panel.Add(_diagnostic);

            var actions = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 10f }
            };
            _openWorkbench = new Button(OpenWorkbench) { text = "在 VFX 工作台打开" };
            actions.Add(_openWorkbench);
            _ping = new Button(PingPrefab) { text = "定位 Prefab" };
            _ping.style.marginLeft = 6f;
            actions.Add(_ping);
            actions.Add(new Button(Close) { text = "取消", style = { marginLeft = 6f } });
            _use = new Button(CommitSelection) { text = "使用此 VFX" };
            _use.style.marginLeft = 6f;
            _use.style.minWidth = 120f;
            actions.Add(_use);
            panel.Add(actions);
            return panel;
        }

        private void RefreshFilters()
        {
            List<string> roles = new List<string> { "全部" };
            roles.AddRange(_catalog.Options.Select(value => value.Role).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().OrderBy(value => value));
            _role.choices = roles;
            _role.SetValueWithoutNotify("全部");
            List<string> themes = new List<string> { "全部" };
            themes.AddRange(_catalog.Options.Select(value => value.Theme).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().OrderBy(value => value));
            _theme.choices = themes;
            _theme.SetValueWithoutNotify("全部");
            RefreshVisible();
        }

        private void RefreshVisible()
        {
            _visible.Clear();
            for (int i = 0; i < _catalog.Options.Count; i++)
            {
                CharacterActionVfxSelectionOption option = _catalog.Options[i];
                if (option.Matches(_search?.value, _role?.value, _theme?.value))
                    _visible.Add(option);
            }
            _visible.Sort(CompareRecommendations);
            _list?.Rebuild();
            if (_visible.Count > 0 && (_list.selectedIndex < 0 || _list.selectedIndex >= _visible.Count))
                _list.selectedIndex = 0;
            RefreshSelection();
        }

        private void SelectInitial()
        {
            CharacterActionVfxSelectionOption current = _catalog.FindCurrent(_currentKey);
            int index = current == null ? -1 : _visible.IndexOf(current);
            _list.selectedIndex = index >= 0 ? index : (_visible.Count > 0 ? 0 : -1);
            RefreshSelection();
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 6f, paddingRight = 6f }
            };
            var image = new Image { name = "thumbnail", scaleMode = ScaleMode.ScaleAndCrop };
            image.style.width = 76f;
            image.style.height = 76f;
            image.style.marginRight = 8f;
            row.Add(image);
            var copy = new VisualElement { style = { flexGrow = 1f, minWidth = 0f } };
            var name = new Label { name = "name" };
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            copy.Add(name);
            copy.Add(new Label { name = "semantic" });
            var status = new Label { name = "status" };
            status.style.color = new Color(0.45f, 0.78f, 0.98f);
            copy.Add(status);
            row.Add(copy);
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            CharacterActionVfxSelectionOption option = _visible[index];
            row.Q<Image>("thumbnail").image = option.Thumbnail;
            row.Q<Label>("name").text = option.DisplayName;
            row.Q<Label>("semantic").text = VfxLibraryLabels.Display(option.Role) + " · "
                + VfxLibraryLabels.Display(option.Theme) + " · " + option.Anchor;
            row.Q<Label>("status").text = option.StatusLabel + (option.Selectable ? string.Empty : " · 不可使用");
            if (option.HasContractWarning)
                row.Q<Label>("status").text = option.StatusLabel + " · 仅预览";
            bool contractCompatible = IsContractCompatible(option, _contractDiagnostic);
            string selectionBlock = _selectionBlockDiagnostic?.Invoke(option) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(selectionBlock))
                row.Q<Label>("status").text = option.StatusLabel + " · 不可使用";
            else if (!contractCompatible)
                row.Q<Label>("status").text = option.StatusLabel + " · 语义建议不匹配";
            row.style.opacity = IsSelectionUsable(option, _selectionBlockDiagnostic) ? 1f : 0.62f;
        }

        private CharacterActionVfxSelectionOption SelectedOption()
        {
            int index = _list?.selectedIndex ?? -1;
            return index >= 0 && index < _visible.Count ? _visible[index] : null;
        }

        private void RefreshSelection()
        {
            CharacterActionVfxSelectionOption option = SelectedOption();
            if (option == null)
            {
                _title.text = "没有匹配的 VFX";
                _details.text = string.Empty;
                _diagnostic.text = string.Empty;
                _use.SetEnabled(false);
                return;
            }

            _title.text = option.DisplayName;
            _details.text = option.ResourceKey + "\n职责 " + VfxLibraryLabels.Display(option.Role)
                + " · 主题 " + VfxLibraryLabels.Display(option.Theme)
                + " · " + option.Anchor + "\n生命周期 " + option.Lifecycle
                + " · " + option.StatusLabel + " · " + option.Review
                + "\n实际 Prefab：" + option.AssetPath;
            string contractDiagnostic = _contractDiagnostic?.Invoke(option) ?? string.Empty;
            string selectionBlockDiagnostic = _selectionBlockDiagnostic?.Invoke(option) ?? string.Empty;
            bool contractCompatible = string.IsNullOrWhiteSpace(contractDiagnostic);
            bool roleRecommended = IsRoleRecommended(option, _expectedRole);
            if (!string.IsNullOrWhiteSpace(selectionBlockDiagnostic))
                _diagnostic.text = "⚠ 当前资源暂不可用于此槽位。\n"
                    + selectionBlockDiagnostic + "\n" + option.Diagnostic;
            else if (!contractCompatible)
                _diagnostic.text = "⚠ 语义建议不匹配，仅供创作参考，仍可选择。\n"
                    + contractDiagnostic + "\n" + option.Diagnostic;
            else if (roleRecommended)
                _diagnostic.text = "✓ 语义建议匹配\n" + option.Diagnostic;
            else
                _diagnostic.text = "⚠ 推荐职责为 "
                    + VfxLibraryLabels.Display(_expectedRole)
                    + "，当前资源职责为 "
                    + VfxLibraryLabels.Display(option.Role)
                    + "。职责仅用于推荐，仍可使用。\n"
                    + option.Diagnostic;
            _diagnostic.style.color = option.Selectable
                && string.IsNullOrWhiteSpace(selectionBlockDiagnostic)
                && contractCompatible
                && roleRecommended
                ? new Color(0.48f, 0.83f, 0.42f)
                : new Color(0.95f, 0.65f, 0.24f);
            _use.SetEnabled(IsSelectionUsable(option, _selectionBlockDiagnostic));
            _openWorkbench.SetEnabled(true);
            _ping.SetEnabled(!string.IsNullOrWhiteSpace(option.AssetPath));
            _preview.Select(option.AssetPath);
            _preview.Play();
        }

        internal static bool IsContractCompatible(
            CharacterActionVfxSelectionOption option,
            Func<CharacterActionVfxSelectionOption, string> contractDiagnostic)
        {
            return option != null
                && (contractDiagnostic == null || string.IsNullOrWhiteSpace(contractDiagnostic(option)));
        }

        internal static bool IsRoleRecommended(CharacterActionVfxSelectionOption option, string expectedRole)
        {
            return option != null
                && (string.IsNullOrWhiteSpace(expectedRole)
                    || string.Equals(option.Role, expectedRole, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(option.Role, "Other", StringComparison.OrdinalIgnoreCase));
        }

        internal static bool IsSelectionUsable(
            CharacterActionVfxSelectionOption option,
            Func<CharacterActionVfxSelectionOption, string> selectionBlockDiagnostic)
        {
            return option != null
                && option.Selectable
                && IsContractCompatible(option, selectionBlockDiagnostic);
        }

        private int CompareRecommendations(
            CharacterActionVfxSelectionOption left,
            CharacterActionVfxSelectionOption right)
        {
            bool leftRecommended = IsRoleRecommended(left, _expectedRole)
                && IsContractCompatible(left, _contractDiagnostic);
            bool rightRecommended = IsRoleRecommended(right, _expectedRole)
                && IsContractCompatible(right, _contractDiagnostic);
            if (leftRecommended != rightRecommended)
                return leftRecommended ? -1 : 1;

            return string.Compare(
                left?.DisplayName,
                right?.DisplayName,
                StringComparison.OrdinalIgnoreCase);
        }

        private void CommitSelection()
        {
            CharacterActionVfxSelectionOption option = SelectedOption();
            if (option == null || !_use.enabledSelf)
                return;

            if (option.IsCandidate)
            {
                VfxConsumerContract contract = VfxConsumerContractResolver.Find(option.ResourceKey);
                string message = string.Empty;
                bool allowContractWarning = option.IsUnboundCandidate && option.HasContractWarning;
                if (contract == null)
                {
                    EditorUtility.DisplayDialog("无法启用 Candidate", "找不到对应的技能消费合同。", "确定");
                    return;
                }
                if (option.IsUnboundCandidate)
                {
                    try
                    {
                        VfxConsumerContractVariantCompiler.BindReceipt(option.Receipt, contract);
                        option.IsUnboundCandidate = false;
                    }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                    {
                        EditorUtility.DisplayDialog("无法绑定草稿候选", exception.Message, "确定");
                        return;
                    }
                }
                bool overrideSet = allowContractWarning
                    ? VfxPublishedBindingEditorService.SetDraftPlaytestOverride(contract, option.Receipt, out message)
                    : VfxPublishedBindingEditorService.SetPlaytestOverride(contract, option.Receipt, out message);
                if (!overrideSet)
                {
                    EditorUtility.DisplayDialog("无法启用 Candidate", message, "确定");
                    return;
                }
            }
            else
            {
                VfxPublishedBindingEditorService.ClearPlaytestOverride(option.ResourceKey, out _);
            }

            _onSelected?.Invoke(option);
            Close();
        }

        private void OpenWorkbench()
        {
            VfxAuthoringResourceLibraryWindow.Open();
        }

        private void PingPrefab()
        {
            CharacterActionVfxSelectionOption option = SelectedOption();
            UnityEngine.Object asset = option == null ? null : AssetDatabase.LoadMainAssetAtPath(option.AssetPath);
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void OnEditorUpdate()
        {
            _previewCanvas?.MarkDirtyRepaint();
        }
    }
}

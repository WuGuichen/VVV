using System;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestFairyGuiRuntimeAiLabBehaviorCardLibraryBinder
    {
        private int VisibleCardCount = 4;
        private const float AssemblyCardWidth = 300f;
        private const float AssemblyCardHeight = 252f;
        private const float AssemblyCardGap = 12f;
        private const float AssemblyCardStartX = 20f;
        private const float AssemblyCardY = 140f;
        private readonly CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler _commands;
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;
        private readonly Action _closeAssembly;
        private readonly Action<CharacterTestRuntimeAiLabBehaviorAssetCardViewModel>
            _assemblySelectionChanged;
        private string[] _stableCardIds = new string[4];
        private bool[] _cardAvailable = new bool[4];
        private bool[] _cardInstalled = new bool[4];
        private bool[] _cardUpdateAvailable = new bool[4];
        private Fgui.GComponent _root;
        private CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel _viewModel;
        private int _projectedCardCount;
        private bool _addToBehaviorGroup;
        private CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind? _assemblyResponsibilityFilter;
        private int _assemblyPageStart;
        private string _assemblySelectedStableCardId = string.Empty;
        private int[] _assemblyIndices = new int[16];
        private readonly int[] _assemblyResponsibilityCounts = new int[6];

        private readonly Action<Fgui.GObject, string> _bindTooltip;

        private static readonly string[] AssemblyResponsibilityButtonNames =
        {
            "assemblyResponsibilityAll",
            "assemblyResponsibilityTargeting",
            "assemblyResponsibilityEngage",
            "assemblyResponsibilityDamage",
            "assemblyResponsibilityFallback",
            "assemblyResponsibilityOther"
        };

        private static readonly string[] AssemblyResponsibilityLabels =
            { "全部", "索敌", "接战", "输出", "兜底", "其他" };

        internal CharacterTestFairyGuiRuntimeAiLabBehaviorCardLibraryBinder(
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler commands,
            IMxUiCommandSink commandSink,
            MxUiViewId viewId,
            Action closeAssembly = null,
            Action<CharacterTestRuntimeAiLabBehaviorAssetCardViewModel>
                assemblySelectionChanged = null,
            Action<Fgui.GObject, string> bindTooltip = null)
        {
            _commands = commands;
            _commandSink = commandSink;
            _viewId = viewId;
            _closeAssembly = closeAssembly;
            _assemblySelectionChanged = assemblySelectionChanged;
            _bindTooltip = bindTooltip;
        }

        internal void SetResponsibilityFilter(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind? responsibility)
        {
            if (_assemblyResponsibilityFilter == responsibility)
                return;
            _assemblyResponsibilityFilter = responsibility;
            _assemblyPageStart = 0;
            _assemblySelectedStableCardId = string.Empty;
            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation(
                "library-filter=" + FormatResponsibilityFilter(responsibility));
        }

        internal void Bind(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel viewModel,
            bool visible,
            bool addToBehaviorGroup = false)
        {
            _addToBehaviorGroup = addToBehaviorGroup;
            if (!ReferenceEquals(_root, root))
            {
                _root = root;
                ConfigureHandlers();
            }
            if (_root == null)
                return;
            _root.visible = visible;
            if (!visible)
                return;

            viewModel ??= new CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel();
            _viewModel = viewModel;
            ApplyRootLayout(_addToBehaviorGroup);
            _projectedCardCount = BuildProjection(viewModel, _addToBehaviorGroup);
            if (_addToBehaviorGroup)
                NormalizeAssemblySelection(viewModel);
            SetText("libraryTitle", _addToBehaviorGroup ? "添加行为卡" : viewModel.HeaderText);
            SetText("librarySummary", viewModel.SummaryText);
            SetText(
                "libraryEmpty",
                _addToBehaviorGroup && _projectedCardCount == 0
                    ? "当前职责下没有匹配的行为卡。"
                    : viewModel.EmptyText);
            Fgui.GTextInput query = _root.GetChild("libraryQuery")?.asTextInput;
            if (query != null && !string.Equals(query.text, viewModel.Query, StringComparison.Ordinal))
                query.text = viewModel.Query ?? string.Empty;
            if (query != null && _addToBehaviorGroup)
                query.promptText = "搜索行为";
            Fgui.GButton filter = _root.GetChild("btnLibraryFilter")?.asButton;
            if (filter != null)
                filter.title = FormatFilter(viewModel.Filter);

            int first = _addToBehaviorGroup
                ? Math.Min(_assemblyPageStart, Math.Max(0, _projectedCardCount - 1))
                : Math.Max(0, viewModel.FirstVisibleIndex);
            for (int slot = 0; slot < VisibleCardCount; slot++)
            {
                int index = first + slot;
                int sourceIndex = _addToBehaviorGroup && index < _projectedCardCount
                    ? _assemblyIndices[index]
                    : index;
                CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card =
                    viewModel.Cards != null && index < _projectedCardCount
                        && sourceIndex >= 0 && sourceIndex < viewModel.ActiveCount
                        ? viewModel.Cards[sourceIndex]
                        : null;
                bool focused = _addToBehaviorGroup
                    ? card != null && string.Equals(
                        card.StableCardId,
                        _assemblySelectedStableCardId,
                        StringComparison.Ordinal)
                    : viewModel.FocusedIndex == sourceIndex;
                BindCard(slot, sourceIndex, card, focused);
            }

            SetButtonEnabled("btnLibraryPrevious", first > 0);
            SetButtonEnabled(
                "btnLibraryNext",
                first + VisibleCardCount < _projectedCardCount);
            if (_addToBehaviorGroup)
            {
                UpdateAssemblyResponsibilityButtons();
                _assemblySelectionChanged?.Invoke(ResolveSelectedAssemblyCard(viewModel));
            }
        }

        internal Action<string> ExternalQueryChanged { get; set; }
        internal Action<int> ExternalPageChanged { get; set; }
        internal Func<string, bool> ExternalOpenCard { get; set; }
        internal Action<string> ExternalSelectCard { get; set; }

        // Data-driven repetitions use the published card component; no alternative UI template.
        internal void SetVisibleCardCount(Fgui.GComponent root, int count)
        {
            count = Math.Max(1, Math.Min(36, count));
            var prototype = root?.GetChild(CardName(0))?.asCom;
            if (prototype == null) return;
            bool changed = count != VisibleCardCount;
            for (int i = 0; i < count; i++)
            {
                if (root.GetChild(CardName(i)) != null) continue;
                var card = Fgui.UIPackage.CreateObjectFromURL(prototype.resourceURL)?.asCom;
                if (card == null) throw new InvalidOperationException("Published behavior card template is unavailable.");
                card.name = CardName(i); root.AddChild(card); changed = true;
            }
            for (int i = count; root.GetChild(CardName(i)) is Fgui.GObject unused; i++)
                unused.visible = false;
            VisibleCardCount = count;
            Array.Resize(ref _stableCardIds, count); Array.Resize(ref _cardAvailable, count);
            Array.Resize(ref _cardInstalled, count); Array.Resize(ref _cardUpdateAvailable, count);
            if (changed && ReferenceEquals(root, _root)) ConfigureHandlers();
        }

        private void ConfigureHandlers()
        {
            if (_root == null)
                return;
            Fgui.GTextInput query = _root.GetChild("libraryQuery")?.asTextInput;
            if (query != null)
                query.onChanged.Set(() => { if (ExternalQueryChanged != null) ExternalQueryChanged(query.text); else _commands?.SetLibraryQuery(query.text); });
            Fgui.GButton filter = _root.GetChild("btnLibraryFilter")?.asButton;
            if (filter != null)
                filter.onClick.Set(() => _commands?.CycleLibraryFilter());
            Fgui.GButton previous = _root.GetChild("btnLibraryPrevious")?.asButton;
            if (previous != null)
                previous.onClick.Set(() => MovePage(-VisibleCardCount));
            Fgui.GButton next = _root.GetChild("btnLibraryNext")?.asButton;
            if (next != null)
                next.onClick.Set(() => MovePage(VisibleCardCount));
            for (int i = 0; i < VisibleCardCount; i++)
            {
                int slot = i;
                Fgui.GComponent card = _root.GetChild(CardName(i))?.asCom;
                if (card == null)
                    continue;
                card.onClick.Set(context =>
                {
                    context.StopPropagation();
                    if (_addToBehaviorGroup)
                        SelectAssemblyCard(slot);
                    else if (ExternalSelectCard != null)
                    {
                        string key = _stableCardIds[slot];
                        if (!string.IsNullOrEmpty(key)) ExternalSelectCard(key);
                    }
                    else
                        OpenCard(slot);
                });
                Fgui.GButton action = card.GetChild("btnAction")?.asButton;
                if (action != null)
                {
                    action.visible = true;
                    action.onClick.Set(context =>
                    {
                        context.StopPropagation();
                        OpenCard(slot);
                    });
                }
                Fgui.GButton secondary = card.GetChild("btnSecondaryAction")?.asButton;
                if (secondary != null)
                    secondary.onClick.Set(context =>
                    {
                        context.StopPropagation();
                        UpgradeCard(slot);
                    });
            }
        }

        private void OpenCard(int slot)
        {
            if (slot < 0 || slot >= _stableCardIds.Length)
                return;
            string stableCardId = _stableCardIds[slot];
            if (string.IsNullOrEmpty(stableCardId) || !_cardAvailable[slot])
                return;
            if (ExternalOpenCard != null) { ExternalOpenCard(stableCardId); return; }
            if (_addToBehaviorGroup)
            {
                if (!_cardInstalled[slot])
                    _commands?.TryAddProjectCardToCurrentBehaviorGroup(stableCardId);
                return;
            }
            _commands?.FocusLibraryCard(slot, _projectedCardCount);
            _commandSink?.Enqueue(new MxUiCommand(
                _viewId,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.OpenLibraryCard,
                new CharacterTestRuntimeAiLabBehaviorCardElementCommand(stableCardId)));
        }

        private void UpgradeCard(int slot)
        {
            if (slot < 0 || slot >= _stableCardIds.Length || !_cardUpdateAvailable[slot])
                return;
            _commands?.TryUpgradeProjectCardReference(_stableCardIds[slot]);
        }

        private void BindCard(
            int slot,
            int absoluteIndex,
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card,
            bool focused)
        {
            Fgui.GComponent visual = _root.GetChild(CardName(slot))?.asCom;
            if (visual == null)
                return;
            bool visible = card != null;
            visual.visible = visible;
            visual.touchable = visible;
            _stableCardIds[slot] = visible ? card.StableCardId : string.Empty;
            _cardAvailable[slot] = visible && card.Available;
            _cardInstalled[slot] = visible && (card.Installed || card.InCurrentBehaviorGroup);
            _cardUpdateAvailable[slot] = visible && card.HasNewerRevision;
            if (!visible)
                return;

            SetText(visual, "title", card.Title);
            SetText(visual, "cost", card.CostText);
            SetText(visual, "state", card.StatusText);
            SetText(visual, "tags", card.TagsText);
            SetText(
                visual,
                "description",
                _addToBehaviorGroup ? card.ResponsibilitySummaryText : card.Description);
            SetText(visual, "slot", card.RevisionText);
            string problemText = card.HasValidationProblems
                ? card.ValidationStatusText
                : card.BlockReasonText;
            SetText(visual, "blockReason", problemText);
            Color32 iconAccent = card.RuntimeActive
                ? new Color32(91, 224, 177, 255)
                : card.CurrentEdit
                    ? new Color32(65, 190, 232, 255)
                    : card.Installed
                        ? new Color32(72, 184, 142, 255)
                        : card.Available
                            ? new Color32(72, 174, 230, 255)
                            : new Color32(108, 123, 130, 255);
            CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.Bind(
                visual,
                card.IconKey,
                _addToBehaviorGroup ? 22f : 10f,
                _addToBehaviorGroup ? 24f : 14f,
                _addToBehaviorGroup ? 72f : 48f,
                iconAccent,
                true);
            ApplyIconCardLayout(visual, _addToBehaviorGroup);
            SetText(visual, "inlineFeedback", card.RuntimeActive ? "当前运行" : card.CurrentEdit ? "当前编辑" : string.Empty);
            SetObjectVisible(visual, "description", string.IsNullOrEmpty(problemText));
            SetObjectVisible(visual, "blockReason", !string.IsNullOrEmpty(problemText));
            _bindTooltip?.Invoke(
                visual,
                card.Title + "\n" + card.StatusText
                + (string.IsNullOrWhiteSpace(problemText) ? string.Empty : "\n" + problemText));
            visual.alpha = card.Available ? 1f : 0.64f;
            Fgui.GGraph stripe = visual.GetChild("stateStripe")?.asGraph;
            if (stripe != null)
            {
                stripe.visible = !_addToBehaviorGroup;
                stripe.color = card.RuntimeActive
                    ? new Color32(56, 222, 194, 255)
                    : card.CurrentEdit
                        ? new Color32(38, 184, 232, 255)
                        : card.Installed
                            ? new Color32(72, 184, 142, 255)
                            : !card.Available
                                ? new Color32(205, 86, 76, 255)
                                : new Color32(63, 127, 140, 255);
            }
            Fgui.GGraph background = visual.GetChild("background")?.asGraph;
            if (background != null)
            {
                if (_addToBehaviorGroup)
                {
                    Color border = focused
                        ? new Color32(255, 195, 74, 255)
                        : card.InCurrentBehaviorGroup
                            ? new Color32(55, 226, 166, 255)
                            : new Color32(32, 167, 208, 255);
                    Color fill = focused
                        ? new Color32(19, 55, 62, 255)
                        : card.InCurrentBehaviorGroup
                            ? new Color32(8, 48, 48, 255)
                            : new Color32(6, 24, 34, 255);
                    background.DrawRect(
                        background.width,
                        background.height,
                        focused ? 4 : 2,
                        border,
                        fill);
                }
                else
                {
                    background.color = focused
                        ? new Color32(13, 52, 67, 255)
                        : card.CurrentEdit
                            ? new Color32(10, 43, 58, 255)
                            : new Color32(12, 27, 37, 255);
                }
            }
            Fgui.GButton action = visual.GetChild("btnAction")?.asButton;
            if (action != null)
            {
                bool actionEnabled = card.Available && (!_addToBehaviorGroup || !card.InCurrentBehaviorGroup);
                action.visible = true;
                action.title = _addToBehaviorGroup
                    ? card.InCurrentBehaviorGroup ? "已添加" : "添加"
                    : "编辑";
                action.enabled = actionEnabled;
                action.touchable = actionEnabled;
                action.grayed = !actionEnabled;
            }
            Fgui.GButton secondary = visual.GetChild("btnSecondaryAction")?.asButton;
            if (secondary != null)
            {
                secondary.visible = !_addToBehaviorGroup && card.HasNewerRevision;
                secondary.title = "升级组引用";
                secondary.enabled = card.HasNewerRevision;
                secondary.grayed = !card.HasNewerRevision;
            }
            BindCardDeleteButton(visual, card, _addToBehaviorGroup);
        }

        private void BindCardDeleteButton(
            Fgui.GComponent visual,
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card,
            bool assemblyMode)
        {
            if (visual == null)
                return;

            Fgui.GButton delete = visual.GetChild("btnDeleteCard")?.asButton;
            bool canDelete = assemblyMode
                && card != null
                && !string.IsNullOrWhiteSpace(card.StableCardId)
                && !card.StableCardId.StartsWith("draft:", StringComparison.Ordinal);

            if (!canDelete)
            {
                if (delete != null)
                    delete.visible = false;
                return;
            }

            if (delete == null)
            {
                delete = Fgui.UIPackage.CreateObject(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    "IconCompactDangerButton")?.asButton;
                if (delete == null)
                {
                    delete = Fgui.UIPackage.CreateObject(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                        "CompactNeutralButton")?.asButton;
                }
                delete ??= new Fgui.GButton();
                delete.name = "btnDeleteCard";
                visual.AddChild(delete);
            }

            delete.visible = true;
            delete.relations?.ClearAll();
            delete.SetXY(visual.width - 48f, 16f);
            delete.SetSize(36f, 36f);
            string iconUrl = Fgui.UIPackage.GetItemURL(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "action_remove_32x32");
            delete.icon = iconUrl;
            delete.title = string.IsNullOrEmpty(iconUrl) ? "×" : string.Empty;
            Fgui.GLoader icon = delete.GetChild("icon")?.asLoader;
            if (icon != null)
            {
                icon.visible = !string.IsNullOrEmpty(iconUrl);
                icon.SetSize(22f, 22f);
                icon.color = new Color32(255, 102, 112, 255);
            }
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.CenterButtonContent(delete, 0f);
            delete.touchable = true;
            delete.grayed = false;
            _bindTooltip?.Invoke(delete, "删除该项目行为卡（从工程中销毁）");
            delete.onTouchBegin.Set(context => context.StopPropagation());
            delete.onClick.Set(context =>
            {
                context.StopPropagation();
                if (!delete.touchable)
                    return;
                _commandSink?.Enqueue(new MxUiCommand(
                    _viewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteProjectCard,
                    card.StableCardId));
            });
            delete.parent?.SetChildIndex(delete, delete.parent.numChildren - 1);
        }

        private static void ApplyIconCardLayout(Fgui.GComponent visual, bool assemblyMode)
        {
            if (visual == null)
                return;
            SetGeometry(visual.GetChild("title"), 68f, 7f, assemblyMode ? 300f : 152f, 22f);
            SetGeometry(visual.GetChild("description"), 68f, 48f, 232f, 18f);
            SetGeometry(visual.GetChild("blockReason"), 68f, 48f, 232f, 18f);
            SetGeometry(visual.GetChild("state"), 68f, 31f, 82f, 18f);
            SetGeometry(visual.GetChild("slot"), 154f, 31f, 66f, 18f);
            SetGeometry(visual.GetChild("tags"), 224f, 31f, 76f, 18f);
            SetObjectVisible(visual, "inlineFeedback", !assemblyMode);
            SetObjectVisible(visual, "cost", !assemblyMode);
            SetObjectVisible(visual, "state", !assemblyMode);
            SetObjectVisible(visual, "slot", !assemblyMode);
            SetObjectVisible(visual, "tags", !assemblyMode);
            if (!assemblyMode)
                return;
            SetObjectVisible(visual, "state", true);
            SetGeometry(visual.GetChild("state"), 24f, 126f, 252f, 28f);
            SetGeometry(visual.GetChild("title"), 106f, 28f, 136f, 32f);
            SetGeometry(visual.GetChild("description"), 106f, 64f, 170f, 48f);
            SetGeometry(visual.GetChild("blockReason"), 106f, 64f, 170f, 48f);
            SetGeometry(visual.GetChild("btnAction"), 24f, 176f, 252f, 54f);
            SetGeometry(visual.GetChild("btnSecondaryAction"), 24f, 176f, 252f, 54f);
            Fgui.GTextField title = visual.GetChild("title")?.asTextField;
            if (title != null)
                title.textFormat.size = 23;
            Fgui.GTextField description = visual.GetChild("description")?.asTextField;
            if (description != null)
            {
                description.textFormat.size = 16;
                description.singleLine = false;
            }
            Fgui.GButton action = visual.GetChild("btnAction")?.asButton;
            if (action != null)
                action.titleFontSize = 22;
        }

        private void ApplyRootLayout(bool assemblyMode)
        {
            if (_root == null)
                return;
            SetObjectVisible(_root, "librarySummary", !assemblyMode);
            for (int i = 0; i < AssemblyResponsibilityButtonNames.Length; i++)
                SetObjectVisible(_root, AssemblyResponsibilityButtonNames[i], assemblyMode);
            SetObjectVisible(_root, "assemblyClose", assemblyMode);
            if (!assemblyMode)
                return;

            BindAssemblyChrome();
            float containerWidth = _root.width > 100f ? _root.width : 1232f;
            float containerHeight = _root.height > 100f ? _root.height : 530f;
            Fgui.GObject background = _root.GetChild("background");
            if (background != null)
                SetGeometry(background, 0f, 0f, containerWidth, containerHeight);

            SetGeometry(_root.GetChild("libraryTitle"), 20f, 14f, 340f, 42f);
            Fgui.GTextField title = _root.GetChild("libraryTitle")?.asTextField;
            if (title != null)
                title.textFormat.size = 30;

            float queryWidth = 240f;
            float closeWidth = 64f;
            float rightPadding = 16f;
            float closeX = containerWidth - closeWidth - rightPadding;
            float queryX = closeX - queryWidth - 16f;

            SetGeometry(_root.GetChild("libraryQuery"), queryX, 14f, queryWidth, 48f);
            Fgui.GTextInput query = _root.GetChild("libraryQuery")?.asTextInput;
            if (query != null)
            {
                query.textFormat.size = 18;
                query.color = new Color32(213, 229, 234, 255);
                query.sortingOrder = 3;
            }
            Fgui.GGraph querySurface = _root.GetChild("assemblyQuerySurface")?.asGraph;
            if (querySurface == null)
            {
                querySurface = new Fgui.GGraph
                {
                    name = "assemblyQuerySurface",
                    touchable = false,
                    sortingOrder = 2
                };
                _root.AddChild(querySurface);
            }
            SetGeometry(querySurface, queryX, 14f, queryWidth, 48f);
            querySurface.DrawRect(
                querySurface.width,
                querySurface.height,
                1,
                new Color32(64, 92, 104, 255),
                new Color32(8, 24, 33, 255));
            SetObjectVisible(_root, "btnLibraryFilter", false);
            SetGeometry(_root.GetChild("btnLibraryPrevious"), containerWidth - 90f, 76f, 34f, 38f);
            SetGeometry(_root.GetChild("btnLibraryNext"), containerWidth - 50f, 76f, 34f, 38f);
            for (int slot = 0; slot < VisibleCardCount; slot++)
            {
                Fgui.GObject card = _root.GetChild(CardName(slot));
                SetGeometry(
                    card,
                    AssemblyCardStartX + slot * (AssemblyCardWidth + AssemblyCardGap),
                    AssemblyCardY,
                    AssemblyCardWidth,
                    AssemblyCardHeight);
            }

            SetGeometry(_root.GetChild("libraryEmpty"), 360f, 220f, 580f, 48f);
        }

        private int BuildProjection(
            CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel viewModel,
            bool assemblyMode)
        {
            int count = Math.Min(viewModel.ActiveCount, viewModel.Cards?.Length ?? 0);
            if (!assemblyMode)
                return count;
            if (_assemblyIndices.Length < count)
                Array.Resize(ref _assemblyIndices, Math.Max(count, _assemblyIndices.Length * 2));
            int projected = 0;
            Array.Clear(_assemblyResponsibilityCounts, 0, _assemblyResponsibilityCounts.Length);
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card = viewModel.Cards[i];
                _assemblyResponsibilityCounts[0]++;
                for (int responsibilityIndex = 1; responsibilityIndex < 6; responsibilityIndex++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility =
                        ResponsibilityForButtonIndex(responsibilityIndex);
                    if (CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.Contains(
                            card?.CompatibleResponsibilities,
                            responsibility))
                    {
                        _assemblyResponsibilityCounts[responsibilityIndex]++;
                    }
                }
                if (!_assemblyResponsibilityFilter.HasValue
                    || CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.Contains(
                        card?.CompatibleResponsibilities,
                        _assemblyResponsibilityFilter.Value))
                {
                    _assemblyIndices[projected++] = i;
                }
            }
            int lastPage = Math.Max(0, ((projected - 1) / VisibleCardCount) * VisibleCardCount);
            _assemblyPageStart = Math.Min(_assemblyPageStart, lastPage);
            return projected;
        }

        private void MovePage(int delta)
        {
            if (ExternalPageChanged != null) { ExternalPageChanged(delta); return; }
            if (!_addToBehaviorGroup)
            {
                _commands?.MoveLibraryPage(
                    delta < 0 ? -1 : 1,
                    VisibleCardCount,
                    _projectedCardCount);
                return;
            }
            _assemblyPageStart = Math.Max(
                0,
                Math.Min(
                    _assemblyPageStart + delta,
                    Math.Max(
                        0,
                        ((_projectedCardCount - 1) / VisibleCardCount)
                        * VisibleCardCount)));
            _assemblySelectedStableCardId = string.Empty;
            Bind(_root, _viewModel, true, true);
        }

        internal void ClearAssemblySelection()
        {
            _assemblySelectedStableCardId = string.Empty;
        }

        private void NormalizeAssemblySelection(
            CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel viewModel)
        {
            if (_projectedCardCount <= 0
                || string.IsNullOrEmpty(_assemblySelectedStableCardId))
            {
                _assemblySelectedStableCardId = string.Empty;
                return;
            }
            int pageEnd = Math.Min(
                _projectedCardCount,
                _assemblyPageStart + VisibleCardCount);
            for (int i = _assemblyPageStart; i < pageEnd; i++)
            {
                int sourceIndex = _assemblyIndices[i];
                CharacterTestRuntimeAiLabBehaviorAssetCardViewModel candidate =
                    sourceIndex >= 0 && sourceIndex < viewModel.ActiveCount
                        ? viewModel.Cards[sourceIndex]
                        : null;
                if (candidate != null
                    && string.Equals(
                        candidate.StableCardId,
                        _assemblySelectedStableCardId,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }
            // Opening the assembly drawer is a navigation-only action. Do not implicitly select
            // the first projected card because selection binds card details and may take over the
            // shared Save surface. Details appear only after an explicit card click.
            _assemblySelectedStableCardId = string.Empty;
        }

        private CharacterTestRuntimeAiLabBehaviorAssetCardViewModel
            ResolveSelectedAssemblyCard(
                CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel viewModel)
        {
            for (int i = 0; i < _projectedCardCount; i++)
            {
                int sourceIndex = _assemblyIndices[i];
                CharacterTestRuntimeAiLabBehaviorAssetCardViewModel candidate =
                    sourceIndex >= 0 && sourceIndex < viewModel.ActiveCount
                        ? viewModel.Cards[sourceIndex]
                        : null;
                if (candidate != null
                    && string.Equals(
                        candidate.StableCardId,
                        _assemblySelectedStableCardId,
                        StringComparison.Ordinal))
                    return candidate;
            }
            return null;
        }

        private void BindAssemblyChrome()
        {
            for (int i = 0; i < AssemblyResponsibilityButtonNames.Length; i++)
            {
                int index = i;
                Fgui.GButton button = _root.GetChild(
                    AssemblyResponsibilityButtonNames[i])?.asButton;
                if (button == null)
                    continue;
                SetGeometry(button, 20f + i * 120f, 76f, 112f, 44f);
                button.titleFontSize = 18;
                button.visible = true;
                button.onClick.Set(() => SelectAssemblyResponsibility(index));
            }
            Fgui.GButton close = _root.GetChild("assemblyClose")?.asButton;
            if (close != null)
            {
                float containerWidth = _root.width > 100f ? _root.width : 1232f;
                SetGeometry(close, containerWidth - 64f - 16f, 14f, 64f, 48f);
                close.title = "×";
                close.titleFontSize = 30;
                close.visible = true;
                close.onClick.Set(() => _closeAssembly?.Invoke());
            }
        }

        private void SelectAssemblyResponsibility(int buttonIndex)
        {
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind? responsibility =
                buttonIndex == 0
                    ? null
                    : ResponsibilityForButtonIndex(buttonIndex);
            if (_assemblyResponsibilityFilter == responsibility)
                return;
            _assemblyResponsibilityFilter = responsibility;
            _assemblyPageStart = 0;
            _assemblySelectedStableCardId = string.Empty;
            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation(
                "library-filter=" + FormatResponsibilityFilter(responsibility)
                + ";result-count=" + _assemblyResponsibilityCounts[buttonIndex]);
            Bind(_root, _viewModel, true, true);
        }

        private void SelectAssemblyCard(int slot)
        {
            if (slot < 0 || slot >= _stableCardIds.Length)
                return;
            string stableCardId = _stableCardIds[slot];
            if (string.IsNullOrEmpty(stableCardId))
                return;
            _assemblySelectedStableCardId = stableCardId;
            Bind(_root, _viewModel, true, true);
        }

        private void UpdateAssemblyResponsibilityButtons()
        {
            for (int i = 0; i < AssemblyResponsibilityButtonNames.Length; i++)
            {
                Fgui.GButton button = _root.GetChild(
                    AssemblyResponsibilityButtonNames[i])?.asButton;
                if (button == null)
                    continue;
                bool selected = i == 0
                    ? !_assemblyResponsibilityFilter.HasValue
                    : _assemblyResponsibilityFilter == ResponsibilityForButtonIndex(i);
                button.title = AssemblyResponsibilityLabels[i]
                    + " " + _assemblyResponsibilityCounts[i];
                Fgui.GGraph surface = button.GetChild("assemblySurface")?.asGraph;
                if (surface == null)
                {
                    surface = new Fgui.GGraph
                    {
                        name = "assemblySurface",
                        touchable = false
                    };
                    button.AddChild(surface);
                }
                surface.SetSize(button.width, button.height);
                surface.SetXY(0f, 0f);
                surface.DrawRect(
                    surface.width,
                    surface.height,
                    selected ? 2 : 1,
                    selected
                        ? new Color32(55, 226, 166, 255)
                        : new Color32(61, 92, 104, 255),
                    selected
                        ? new Color32(14, 105, 83, 255)
                        : new Color32(9, 28, 38, 255));
                Fgui.GTextField label = button.GetChild("title")?.asTextField;
                if (label != null)
                {
                    label.color = selected
                        ? new Color32(230, 255, 248, 255)
                        : new Color32(202, 219, 225, 255);
                    label.sortingOrder = 4;
                }
            }
        }

        private static CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind
            ResponsibilityForButtonIndex(int buttonIndex)
        {
            switch (buttonIndex)
            {
                case 1:
                    return CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting;
                case 2:
                    return CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure;
                case 3:
                    return CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage;
                case 4:
                    return CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback;
                default:
                    return CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other;
            }
        }

        private static string FormatResponsibilityFilter(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind? responsibility)
        {
            return responsibility.HasValue
                ? responsibility.Value.ToString()
                : "All";
        }

        private static void SetGeometry(
            Fgui.GObject target,
            float x,
            float y,
            float width,
            float height)
        {
            if (target == null)
                return;
            target.SetSize(width, height);
            target.SetXY(x, y);
        }

        private static string FormatFilter(CharacterTestRuntimeAiLabBehaviorCardLibraryFilter filter)
        {
            switch (filter)
            {
                case CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.Installed:
                    return "已装配";
                case CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.UnlockedAvailable:
                    return "可用";
                case CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.UnavailableOrIncompatible:
                    return "需处理";
                default:
                    return "全部";
            }
        }

        private static string CardName(int index) => "libraryCard" + (index + 1);

        private void SetText(string childName, string value) => SetText(_root, childName, value);

        private static void SetText(Fgui.GComponent root, string childName, string value)
        {
            Fgui.GTextField text = root?.GetChild(childName)?.asTextField;
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private static void SetObjectVisible(Fgui.GComponent root, string childName, bool visible)
        {
            Fgui.GObject child = root?.GetChild(childName);
            if (child != null)
                child.visible = visible;
        }

        private void SetButtonEnabled(string childName, bool enabled)
        {
            Fgui.GButton button = _root?.GetChild(childName)?.asButton;
            if (button == null)
                return;
            button.enabled = enabled;
            button.grayed = !enabled;
        }
    }
}

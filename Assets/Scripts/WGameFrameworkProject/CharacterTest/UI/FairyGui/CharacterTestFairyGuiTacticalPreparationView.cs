using System;
using System.Linq;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using MxFramework.Items;
using MxFramework.UI.FairyGui;
using MxFramework.Game.CharacterTest.TacticalCard;
using UnityEngine;
using UnityEngine.InputSystem;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Binds the published components; the preparation service owns persistent edits.</summary>
    internal sealed partial class CharacterTestFairyGuiTacticalPreparationView : IDisposable
    {
        private enum PopupKind { Closed, Actions, Teams, Presets, Profiles, ReplacePreset, NamePreset, Details, More, AcquireActions, AcquireMechanismTrials }
        private CharacterTestFairyGuiCardTooltip _tooltips;
        private static readonly MxFairyGuiTooltipOptions TooltipOptions = new(
            MxFairyGuiTooltipPlacement.Above, maximumTextWidth: 560f, allowPin: false);
        private sealed class CardContext
        {
            internal string Entity;
            internal CharacterTacticalCardReference Reference;
        }
        private Fgui.GComponent _root, _header, _drawerHeader, _topBar, _popup;
        private Fgui.GList _deckList, _libraryList, _characterList, _popupList;
        private CharacterTestFairyGuiCharacterImageBinding _characterImages;
        private TacticalBattlePreparationController _controller;
        private TacticalBattlePreparationService _preparation;
        private CharacterTacticalPlayerService _player;
        private InventoryOwnerId _inventoryOwner;
        private string _inventoryIssue = "";
        private CharacterTacticalRoleBindingQueue _roleBindings;
        private long _actionRoleRevision, _actionPlayerRevision, _actionPrepRevision, _actionCatalogRevision;
        private long _revision = -1, _catalogRevision = -1, _playerRevision = -1;
        private long _summaryRevision = -1;
        private long _summaryStatusRevision = -1;
        private readonly CharacterTestCardSummaryPreparation _summaries = new();
        private CharacterTestUiTextProviderSignature _textSignature;
        private double _frameSeconds;
        private Vector2 _size, _actionAnchor;
        private string _selection = "", _search = "", _localIssue = "", _lastIssue = "";
        private TacticalPreparationDrawerMode _drawer = (TacticalPreparationDrawerMode)(-1);
        private bool _top, _dirty, _busy, _presetCreated, _canStart;
        private PopupKind _popupKind;
        private string _popupEntity = "", _pendingPreset = "";
        private ulong _popupCard;
        private CharacterTacticalCardReference _popupReference;
        private string _popupSlot = "";
        private int _actionPickerGeneration;
        private int _generation;
        private readonly Dictionary<Fgui.GComponent, CharacterTestFairyGuiTacticalDescriptionHints> _cardHints = new();
        private readonly Dictionary<(string Card, int Revision, string Hash, ulong Instance, int Character, string Action,
            CharacterTestUiTextProviderSignature Text, double Frame, long Summary, object Registry), CharacterTestCardUiPreviewData> _presentationCache = new();

        internal void Bind(Fgui.GComponent root, TacticalBattlePreparationController controller,
            TacticalBattlePreparationService preparation, CharacterTacticalPlayerService player, InventoryOwnerId inventoryOwner = default, CharacterTacticalRoleBindingQueue roleBindings = null)
        {
            if (root == null || controller == null || preparation == null || player == null) return;
            if (_root != root || !ReferenceEquals(_controller, controller)
                || !ReferenceEquals(_preparation, preparation) || !ReferenceEquals(_player, player) || !_inventoryOwner.Equals(inventoryOwner)
                || !ReferenceEquals(_roleBindings, roleBindings))
            {
                Dispose(); _tooltips = new CharacterTestFairyGuiCardTooltip(true); _root = root; _controller = controller; _preparation = preparation; _player = player; _inventoryOwner = inventoryOwner; _roleBindings = roleBindings;
                controller.BindPreparationSources(preparation, player, inventoryOwner, roleBindings);
                _header = Required(root, "entityHeader").asCom; _drawerHeader = Required(root, "drawerHeader").asCom;
                _topBar = Required(root, "topBar").asCom; _popup = Required(root, "popupPanel").asCom;
                _deckList = Required(root, "deckList").asList; _libraryList = Required(root, "libraryList").asList;
                _characterList = Required(root, "characterList").asList; _popupList = Required(root, "popupList").asList;
                ConfigureList(_deckList, true); ConfigureList(_libraryList, true);
                ConfigureList(_characterList, false); ConfigureList(_popupList, false); SetupControls();
            }
            _inventoryIssue = string.Empty;
            if (player.HasCharacterItemProgress)
                player.TryReadOwnedCharacterItems(inventoryOwner, out _, out _inventoryIssue);
            _summaries.Tick();
            foreach (var hints in _cardHints.Values) hints.UpdatePointerHold();
            UpdateDrawerPointerRegions();
            TickCardMaterials();
            int generation = _generation;
            UpdateTemplatePreview();
            // Placement can publish a new session and synchronously dispose/rebind this view.
            if (generation != _generation) return;
            HandlePopupKeys();
            if (_popupKind == PopupKind.AcquireActions && !_busy && controller.SelectedEntityId != _popupEntity) ClosePopup();
            if (_popupKind == PopupKind.AcquireMechanismTrials && !_busy && !MechanismTrialOwnerMatches()) ClosePopup();
            if (_popupKind == PopupKind.Actions && (controller.SelectedEntityId != _popupEntity
                || preparation.Find(_popupEntity)?.Loadout.Cards.Any(card => card.InstanceId == _popupCard) != true)) ClosePopup();
            string issue = CurrentIssue(); Vector2 size = new Vector2(root.width, root.height);
            var context = controller.GetComponent<GameManager>()?.RuntimeContext;
            var provider = context?.LocalizationRuntime?.TextProvider ?? (MxFramework.UI.IMxUiTextProvider)MxFramework.UI.MxUiNullTextProvider.Instance;
            var textSignature = CharacterTestUiTextProviderSignature.Create(provider);
            double frameSeconds = context?.SessionBuilder?.ResolveSimulationDeltaTime()
                ?? 1d / Math.Max(1, context?.Profile?.TargetFrameRate ?? CharacterTestDeterministicUnits.DefaultTickRate);
            if (_playerRevision == player.Revision && _revision == preparation.Revision && _catalogRevision == player.CatalogRevision && _selection == controller.SelectedEntityId
                && _drawer == controller.DrawerMode && _top == controller.TopExpanded && _size == size && _canStart == controller.CanStartBattle
                && _lastIssue == issue && _dirty == (preparation.HasUnsavedChanges || player.HasUnsavedChanges) && _textSignature == textSignature
                && _frameSeconds == frameSeconds && _summaryRevision == CharacterTestTacticalPreparationCatalog.Revision
                && _summaryStatusRevision == _summaries.Revision) return;
            if (_catalogRevision != player.CatalogRevision || _textSignature != textSignature || _frameSeconds != frameSeconds
                || _summaryRevision != CharacterTestTacticalPreparationCatalog.Revision) _presentationCache.Clear();
            _playerRevision = player.Revision; _revision = preparation.Revision; _catalogRevision = player.CatalogRevision; _selection = controller.SelectedEntityId;
            _textSignature = textSignature; _frameSeconds = frameSeconds; _summaryRevision = CharacterTestTacticalPreparationCatalog.Revision;
            _summaryStatusRevision = _summaries.Revision;
            _drawer = controller.DrawerMode; _top = controller.TopExpanded; _size = size;
            _lastIssue = issue; _dirty = preparation.HasUnsavedChanges || player.HasUnsavedChanges; _canStart = controller.CanStartBattle; Render();
        }
        private static Fgui.GObject Required(Fgui.GComponent parent, string name) => parent.GetChild(name)
            ?? throw new InvalidOperationException("Published preparation control missing: " + name);
        private static void ConfigureList(Fgui.GList list, bool cards)
        {
            list.itemRenderer = (index, item) => { };
            list.layout = cards ? Fgui.ListLayoutType.FlowHorizontal : Fgui.ListLayoutType.SingleColumn;
            list.autoResizeItem = !cards; list.columnGap = 16; list.lineGap = cards ? 20 : 8;
            list.align = cards ? Fgui.AlignType.Center : Fgui.AlignType.Left; list.SetVirtual();
        }
        private void SetupControls()
        {
            Required(_root, "background").touchable = false;
            Required(_root, "background").visible = false;
            Required(_root, "toast").touchable = false; Required(_root, "toastBackground").touchable = false;
            Button(_root, "leftHandle", _controller.ToggleCharacterDrawer); Button(_root, "topHandle", _controller.ToggleTop);
            Button(_drawerHeader, "collapse", _controller.CollapseDrawer);
            Button(_header, "deselect", () => { ClosePopup(); _controller.Select(""); });
            Button(_header, "team", ShowTeamPopup); Button(_header, "control", TogglePlayerControl);
            Button(_header, "rotateLeft", () => _controller.RotateSelected(-15f));
            Button(_header, "rotateRight", () => _controller.RotateSelected(15f)); Button(_header, "addCard", _controller.OpenCardDrawer);
            Button(_topBar, "preset", ShowPresets); Button(_topBar, "saveAs", () => ShowPresetName(""));
            Button(_topBar, "update", UpdatePreset); Button(_root, "start", () => {
                if (!_busy && _controller.CanStartBattle) { _localIssue = ""; _controller.StartBattle(); _revision = -1; } });
            Button(_topBar, "collapse", _controller.ToggleTop);
            Button(_topBar, "refreshCatalog", () => { ClosePopup(); _localIssue = ""; _controller.RefreshPublishedCatalog(); _revision = -1; });
            SetupPreparationThemeControls();
            Button(_popup, "close", ClosePopup); Button(_popup, "cancel", ClosePopup);
            Required(_root, "popupDismiss").onClick.Set(ctx => { ctx.StopPropagation(); ClosePopup(); });
            HoldDrawer(Required(_root, "libraryPanel")); HoldDrawer(_drawerHeader); HoldDrawer(_characterList);
            HoldDrawer(_libraryList); HoldDrawer(Required(_root, "search"));
            _topBar.onRollOver.Set(() => _controller.HoldTop(true)); _topBar.onRollOut.Set(() => _controller.HoldTop(false));
            var search = Required(_root, "search").asTextInput; search.promptText = "搜索行为卡";
            search.onChanged.Set(() => { _search = search.text ?? ""; _revision = -1; });
            _popupList.sortingOrder = 102; _popup.sortingOrder = 101; Required(_root, "popupDismiss").sortingOrder = 100;
            Required(_root, "toastBackground").sortingOrder = 120; Required(_root, "toast").sortingOrder = 121; ClosePopup();
        }
        private void HoldDrawer(Fgui.GObject control)
        {
            control.onRollOver.Set(() => _controller.HoldDrawer(true)); control.onRollOut.Set(() => _controller.HoldDrawer(false));
        }
        private static void StyleButton(Fgui.GButton button)
        {
            if (button == null) throw new InvalidOperationException("Preparation controls require published GButton components.");
            var states = button.GetController("button");
            states.onChanged.Set(() => button.titleColor = states.selectedPage == "up"
                ? new Color32(28, 49, 77, 255) : new Color32(18, 77, 87, 255));
            button.titleColor = new Color32(28, 49, 77, 255);
            foreach (string name in new[] { "overOverlay", "downOverlay" })
                if (button.GetChild(name) is Fgui.GObject overlay) overlay.alpha = 0;
            var title = button.GetTextField();
            if (title != null) { var format = title.textFormat; format.bold = true; title.textFormat = format; }
        }
        private static Fgui.GButton Button(Fgui.GComponent parent, string name, Action action)
        {
            var button = Required(parent, name).asButton; StyleButton(button);
            button.onClick.Set(ctx => { ctx.StopPropagation(); action(); }); return button;
        }
        private static void RowButton(Fgui.GButton row, Action action)
        {
            StyleButton(row); row.onClick.Set(ctx => { ctx.StopPropagation(); action(); });
        }
        private static void Label(Fgui.GComponent parent, string name, string value)
        {
            var field = Required(parent, name).asTextField; field.text = value ?? ""; field.touchable = false;
        }
        private void Render()
        {
            RenderFrostedLayout();
        }
        private void RenderEntity(TacticalBattleEntitySetup entity)
        {
            RenderFrostedEntity(entity);
        }
        private void RenderDeck(TacticalBattleEntitySetup entity)
        {
            if (entity == null) { _deckList.numItems = 0; return; }
            var cards = entity.Loadout.Cards.ToArray();
            _deckList.itemRenderer = (index, item) =>
            {
                if (index >= cards.Length) return;
                var reference = cards[index]; var tile = item.asCom; var card = Required(tile, "content").asCom; var definition = _player.Catalog.ExactCard(reference);
                BindCardFace(card, definition, reference); FitCardTile(tile, card, _deckScale); item.draggable = false; Required(card, "add").visible = false;
                var remove = Button(card, "remove", () => RemoveCard(entity.EntityId, reference.InstanceId)); remove.visible = false; remove.tooltips = "移除这张卡";
                var inline = Required(card.GetChild("face").asCom, "behaviorHints").asList;
                bool hasInlineAction = inline.visible && inline.GetChildren().Any(row => row.asCom.GetChild("action")?.visible == true);
                var choose = Button(card, "action", () => ShowActions(entity.EntityId, reference, definition?.ActionSlotPath, Required(card, "action")));
                choose.visible = false;
                choose.touchable = choose.visible; choose.enabled = choose.visible;
                choose.title = "装配动作  ▴"; choose.titleFontSize = 28; remove.titleFontSize = 28;
                Box(choose, 42, 476, 310, 48); Box(remove, 346, 10, 36, 36);
                choose.onClick.Clear();
                card.onRollOver.Set(() => remove.visible = !entity.CharacterItemInstanceId.HasValue);
                card.onRollOut.Set(() => remove.visible = false);
                card.onClick.Set(ctx => { ctx.StopPropagation();
                    if (definition?.HasActionSlot == true && !hasInlineAction)
                        ShowActions(entity.EntityId, reference, definition.ActionSlotPath, card.GetChild("face"));
                });
            };
            _deckList.numItems = cards.Length; _deckList.RefreshVirtualList();
        }
        private void BindCardFace(Fgui.GComponent item, CharacterTacticalCardDefinition definition, CharacterTacticalCardReference reference)
        {
            var face = Required(item, "face").asCom;
            var display = definition == null ? default : TacticalCardDisplayData.FromDefinition(new TacticalActionCardDefinition(
                definition.CardId, string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.CardId : definition.DisplayName,
                definition.Description, definition.MotionCost, definition.CardId, definition.Revision,
                definition.ContentHash, reference.InstanceId, definition.IconKey));
            item.data = new CardContext { Entity = _controller.SelectedEntityId, Reference = reference };
            CharacterTestFairyGuiPlayerCardPresentation.Initialize(face);
            item.SetSize(CharacterTestFairyGuiPlayerCardPresentation.FrameWidth, CharacterTestFairyGuiPlayerCardPresentation.FrameHeight);
            face.SetXY(CharacterTestFairyGuiPlayerCardPresentation.FramePadding, CharacterTestFairyGuiPlayerCardPresentation.FramePadding);
            face.SetPivot(0.5f, 0.5f);
            var data = PresentationData(definition, reference, display);
            CharacterTestFairyGuiPlayerCardPresentation.Bind(face, data, 34);
            if (!_cardHints.TryGetValue(face, out var hints))
            {
                face.onRollOver.Set(() => CharacterTestFairyGuiPlayerCardPresentation.SetHovered(face, true));
                face.onRollOut.Set(() => CharacterTestFairyGuiPlayerCardPresentation.SetHovered(face, false));
                hints = new CharacterTestFairyGuiTacticalDescriptionHints(face, null, (path, anchor) => {
                    var current = item.data as CardContext;
                    if (current?.Reference.InstanceId > 0 && _controller.SelectedEntityId == current.Entity
                        && _preparation.Find(current.Entity)?.Loadout.Cards.Any(card => card.InstanceId == current.Reference.InstanceId) == true)
                        ShowActions(current.Entity, current.Reference, path, anchor);
                }, _root);
                _cardHints.Add(face, hints);
            }
            ApplyCardTheme(face, data);
            hints.Bind(data, _controller.GetComponent<GameManager>()?.RuntimeContext?.LocalizationRuntime?.TextProvider, 34);
            CharacterTestFairyGuiPlayerCardPresentation.SetHovered(face, hints.CardHovered);
            hints.RefreshPlacement();

        }
        // Read-only source projection. It never prepares or charges a command, or invents action metrics.
        private CharacterTestCardUiPreviewData PresentationData(CharacterTacticalCardDefinition definition,
            CharacterTacticalCardReference reference, TacticalCardDisplayData display)
        {
            var data = new CharacterTestCardUiPreviewData { Id = reference.CardId, Name = display.IsAvailable ? display.Name : "失效卡牌",
                Cost = display.IsAvailable ? display.Cost : -1, IconKey = display.IconKey, Description = display.Description,
                Token = $"{reference.CardId}@{reference.Revision}:{reference.ContentHash}/{reference.InstanceId}",
                Actions = Array.Empty<CharacterTestTacticalActionLocation>() };
            var source = definition?.PresentationSource;
            if (source == null) return data;
            var document = CharacterTestTacticalCardPresentationProjection.Document(reference.CardId, data.Name, source);
            var entity = _preparation.Find(_controller.SelectedEntityId);
            var binding = entity?.Loadout.Bindings.Find(value => value.CardInstanceId == reference.InstanceId);
            var action = binding == null ? null : _player.Catalog.Action(binding.TacticalActionId);
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            if (action != null)
                foreach (string path in definition.Slots) names.Add((source.IsLegacy ? "preview-legacy/" : "") + path, action.DisplayName);
            var context = _controller.GetComponent<GameManager>()?.RuntimeContext;
            var provider = context?.LocalizationRuntime?.TextProvider ?? (MxFramework.UI.IMxUiTextProvider)MxFramework.UI.MxUiNullTextProvider.Instance;
            double frameSeconds = context?.SessionBuilder?.ResolveSimulationDeltaTime()
                ?? 1d / Math.Max(1, context?.Profile?.TargetFrameRate ?? CharacterTestDeterministicUnits.DefaultTickRate);
            var key = (reference.CardId, reference.Revision, reference.ContentHash, reference.InstanceId, entity?.CharacterId ?? 0,
                binding?.TacticalActionId ?? "", CharacterTestUiTextProviderSignature.Create(provider), frameSeconds,
                CharacterTestTacticalPreparationCatalog.Revision, (object)context?.Slice?.CharacterRuntime?.ActionRuntime?.Registry);
            if (_presentationCache.TryGetValue(key, out var cached)) return cached;
            data.Actions = definition.Slots.Select(path => new CharacterTestTacticalActionLocation { Path = path,
                Label = path, ActionName = action?.DisplayName ?? "未装配", ActionId = action == null ? ""
                    : CharacterTestRuntimeAiPlannerDevSceneController.AuthoringActionKey(action.ActionId) }).ToArray();
            var notes = new List<string>();
            var inputs = CharacterTestTacticalCardPresentationProjection.Prepare(source, document, data.Actions,
                entity?.CharacterId ?? 0, notes,
                () => CharacterTestTacticalPreparationCatalog.Role(entity?.CharacterId ?? 0, context?.Slice?.CharacterRuntime?.ActionRuntime),
                null, out string preparationIssue, _summaries.Prepare);
            CharacterTestTacticalCardPresentationProjection.Project(source, document, data, inputs,
                preparationIssue, names, provider, frameSeconds, notes);
            var selected = action == null ? null : CharacterTestActionBrowserCatalog.Get().Actions.FirstOrDefault(value => value.ActionId == action.ActionId);
            var definitions = document.Definitions.ToDictionary(value => value.DefinitionId, StringComparer.Ordinal);
            int actionSlots = CharacterTestTacticalCompositionEdits.Nodes(document.Root).Where(value => value.Kind == "Reference")
                .Sum(value => definitions[value.DefinitionId].Source.Slots.Count(slot => !slot.IsFunctional));
            data.HasSwordArtwork = actionSlots == 1 && selected?.Category == CharacterActionCategory.BasicAttack
                && selected.Issues.Length == 0 && selected.WeaponTypes.Contains("sword");
            // Position/layout changes must not prepare the same static authored metrics repeatedly.
            // Keep failures uncached so a newly available resource can be retried.
            if (data.Prepared)
            {
                if (_presentationCache.Count >= 256) _presentationCache.Clear();
                _presentationCache[key] = data;
            }
            return data;
        }
        private void RenderDrawer(TacticalBattleEntitySetup selected, float width, float height)
        {
            RenderFrostedDrawer(selected, width, height);
        }
        private void RenderCharacters()
        {
            RenderFrostedCharacters();
        }
        private int RenderLibrary(TacticalBattleEntitySetup entity)
        {
            if (entity.CharacterItemInstanceId.HasValue) { _libraryList.numItems = 0; return 0; }
            var cards = (_player.Read(entity.CharacterStableId)?.Cards ?? new List<CharacterTacticalCardReference>())
                .Select(reference => _player.Catalog.ExactCard(reference)).Where(card => card != null)
                .GroupBy(card => card.CardId + ":" + card.Revision + ":" + card.ContentHash, StringComparer.Ordinal).Select(group => group.First())
                .Where(card => string.IsNullOrWhiteSpace(_search) || (card.DisplayName ?? card.CardId).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(card => new CharacterTacticalCardReference { CardId = card.CardId, Revision = card.Revision, ContentHash = card.ContentHash }).ToArray();
            _libraryList.itemRenderer = (index, item) =>
            {
                var reference = cards[index]; var tile = item.asCom; var card = Required(tile, "content").asCom; var definition = _player.Catalog.ExactCard(reference); BindCardFace(card, definition, reference); FitCardTile(tile, card, _libraryScale); tile.height = 594f * _libraryScale;
                Required(card, "remove").visible = Required(card, "action").visible = false; item.draggable = false;
                var add = Button(card, "add", () => AddCard(entity.EntityId, reference)); add.visible = true; add.title = "+"; add.titleFontSize = 38; Box(add, 142, 542, 110, 48);
                add.enabled = definition != null && ((definition.PointMove == null && definition.PositionRecall == null) || _preparation.Working.PlayerControlledEntityId == entity.EntityId);
                _tooltips.Bind(add, add.enabled ? "添加到该实体的出战卡组。" : "手动选点卡只能添加给玩家控制实体。", TooltipOptions);
                card.onClick.Set(ctx => ctx.StopPropagation());
            };
            _libraryList.numItems = cards.Length; _libraryList.RefreshVirtualList(); return cards.Length;
        }
        private void RenderTop(float width)
        {
            RenderFrostedTop(width);
        }
        private void AddCard(string entity, CharacterTacticalCardReference reference) => SetResult(_preparation.AddCard(entity, reference, out _, out string issue), issue);
        private void RemoveCard(string entity, ulong instance) => SetResult(_preparation.RemoveCard(entity, instance, out string issue), issue);
        private void TogglePlayerControl()
        {
            string entity = _preparation.Working.PlayerControlledEntityId == _controller.SelectedEntityId ? "" : _controller.SelectedEntityId;
            SetResult(_preparation.SetPlayerControlledEntity(entity, out string issue), issue);
        }
        private void SetResult(bool success, string issue)
        {
            _localIssue = success ? "" : issue; _revision = -1; if (success) _controller.ClearIssue();
            if (!success && _popupKind != PopupKind.Closed) Label(_popup, "issue", issue);
        }
        private string CurrentIssue() => !string.IsNullOrWhiteSpace(_localIssue) ? _localIssue : !string.IsNullOrWhiteSpace(_controller.LastIssue) ? _controller.LastIssue
            : !string.IsNullOrWhiteSpace(_inventoryIssue) ? _inventoryIssue
            : !string.IsNullOrWhiteSpace(_player.SaveIssue) ? _player.SaveIssue
            : !string.IsNullOrWhiteSpace(_preparation.SaveIssue) ? _preparation.SaveIssue : _controller.ConfigurationIssue;
        private void OpenPopup(PopupKind kind, string title, string context)
        {
            if (_busy) return;
            _popupKind = kind; _controller.HoldPopup(true); SetPopupBusy(false);
            _popup.visible = _popupList.visible = Required(_root, "popupDismiss").visible = true;
            Label(_popup, "title", title); Label(_popup, "context", context); Label(_popup, "issue", "");
            Required(_popup, "context").SetSize(_popup.width - 48, 52);
            Required(_popup, "input").visible = Required(_popup, "confirm").visible = false;
            Required(_popup, "cancel").visible = true; Required(_popup, "cancel").asButton.title = "取消"; _popupList.numItems = 0; LayoutPopup();
        }
        private void LayoutPopup()
        {
            if (_popupKind == PopupKind.Closed) return;
            if (_popupKind == PopupKind.AcquireActions || _popupKind == PopupKind.AcquireMechanismTrials) { LayoutAcquireActions(); return; }
            if (_popupKind == PopupKind.Presets || _popupKind == PopupKind.More || _popupKind == PopupKind.Teams || _popupKind == PopupKind.Actions)
            { LayoutChoicePopup(); return; }
            Required(_popup, "context").visible = true;
            float s = _uiScale;
            float width = Mathf.Min(520f, (_root.width - 48f) / s);
            float height = _popupKind == PopupKind.More ? 316f : _popupKind == PopupKind.Teams ? 392f : 568f;
            height = Mathf.Min(height, (_root.height - 48f) / s);
            float x = (_root.width - width * s) * .5f, y = (_root.height - height * s) * .5f;
            if (_popupKind == PopupKind.Actions)
            { x = _actionAnchor.x; y = _actionAnchor.y - height * s - 12f * s; }
            else if (_popupKind == PopupKind.Presets || _popupKind == PopupKind.More)
            {
                var anchor = Required(_topBar, _popupKind == PopupKind.More ? "more" : "preset");
                var position = _root.GlobalToLocal(anchor.LocalToGlobal(new Vector2(0, anchor.height)));
                x = position.x; y = position.y + 8 * s;
            }
            x = Mathf.Clamp(x, 16 * s, _root.width - width * s - 16 * s);
            y = Mathf.Clamp(y, 16 * s, _root.height - height * s - 16 * s);
            _popup.SetScale(s, s); Box(_popup, x, y, width, height);
            Box(Required(_popup, "background"), 0, 0, width, height);
            Box(Required(_popup, "backgroundImage"), -6, -6, width+12, height+12);
            Box(Required(_popup, "close"), width - 64, 18, 40, 36);
            Box(Required(_popup, "title"), 24, 20, width - 104, 36);
            Box(Required(_popup, "context"), 24, 68, width - 48, _popupKind == PopupKind.Details ? height - 220 : 58);
            Box(Required(_popup, "input"), 24, 130, width - 48, 40);
            Box(Required(_popup, "issue"), 24, height - 136, width - 48, 58);
            Box(Required(_popup, "confirm"), 24, height - 64, (width - 60) / 2, 40);
            Box(Required(_popup, "cancel"), width / 2 + 6, height - 64, (width - 60) / 2, 40);
            float listTop = _popupKind == PopupKind.More ? 72 : _popupKind == PopupKind.Actions ? 180 : 134;
            _popupList.SetScale(s, s); Box(_popupList, x + 24*s, y + listTop*s, width - 48, Mathf.Max(64, height - listTop - 140));
            Required(_root, "popupDismiss").SetSize(_root.width, _root.height);
        }
        private void ShowActions(string entityId, CharacterTacticalCardReference reference, string path, Fgui.GObject anchor)
        {
            var entity = _preparation.Find(entityId);
            var current = entity?.Loadout.Cards.Find(value => value.InstanceId == reference.InstanceId);
            var exact = _player.Catalog.ExactCard(current);
            if (_busy || _controller.SelectedEntityId != entityId || current == null || exact == null
                || current.CardId != reference.CardId || current.Revision != reference.Revision || current.ContentHash != reference.ContentHash
                || (entity.CharacterItemInstanceId.HasValue ? !exact.Slots.Contains(path)
                    : !string.Equals(path, exact.ActionSlotPath, StringComparison.Ordinal))) return;
            _popupEntity = entityId; _popupCard = reference.InstanceId; _popupSlot = path;
            _actionRoleRevision = entity.CharacterItemRevision; _actionPlayerRevision = _player.Revision;
            _actionPrepRevision = _preparation.Revision; _actionCatalogRevision = _player.CatalogRevision;
            _actionPickerGeneration++;
            _popupReference = new CharacterTacticalCardReference { InstanceId = reference.InstanceId, CardId = reference.CardId,
                Revision = reference.Revision, ContentHash = reference.ContentHash };
            _actionAnchor = _root.GlobalToLocal(anchor.LocalToGlobal(Vector2.zero));
            OpenPopup(PopupKind.Actions, entity.CharacterItemInstanceId.HasValue ? "已学动作" : "选择动作",
                entity.CharacterItemInstanceId.HasValue ? "选择已学动作；命令执行后仅修改该角色实例的此卡槽。" : "仅修改这张卡；相同定义的其他卡不受影响。");
            var input = Required(_popup, "input").asTextInput; input.visible = true; input.editable = true; input.promptText = "搜索可用动作"; input.text = "";
            input.onChanged.Set(() => RenderActions(input.text)); RenderActions("");
        }
        private void RenderActions(string search)
        {
            var entity = _preparation.Find(_popupEntity); var owned = entity == null ? null
                : entity.CharacterItemInstanceId.HasValue ? entity.Loadout : _player.Read(entity.CharacterStableId);
            var actions = (owned?.ActionIds?.ToArray() ?? Array.Empty<string>()).Select(id => _player.Catalog.Action(id))
                .Where(action => action != null && action.CharacterStableIds.Contains(entity.CharacterStableId)
                    && (string.IsNullOrWhiteSpace(search) || (action.DisplayName ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            string current = entity?.Loadout.Bindings.Find(value => value.CardInstanceId == _popupCard && value.SlotPath == _popupSlot)?.TacticalActionId;
            int pickerGeneration = _actionPickerGeneration;
            bool instanceMode = entity?.CharacterItemInstanceId.HasValue == true;
            _popupList.itemRenderer = (index, item) =>
            {
                var action = index < actions.Length ? actions[index] : null;
                var row = item.asButton;
                row.title = action == null ? "解除此槽装配" : (action.Id == current ? "✓  " : "") + action.DisplayName;
                RowButton(row, () => {
                    if (_popupKind != PopupKind.Actions || _busy || pickerGeneration != _actionPickerGeneration) return;
                    if (instanceMode)
                    {
                        _busy = true; var sourceRoot = _root; var sourcePlayer = _player; var sourceQueue = _roleBindings; int generation = _generation;
                        bool accepted = _controller.QueueRoleBinding(_popupEntity,
                            new ItemInstanceId(entity.CharacterItemInstanceId.Value), _inventoryOwner, _popupCard,
                            _popupSlot, action?.Id, _actionRoleRevision, _actionPlayerRevision, _actionPrepRevision, _actionCatalogRevision,
                            (success, issue) => {
                                if (generation != _generation || !ReferenceEquals(_root, sourceRoot) || !ReferenceEquals(_player, sourcePlayer)
                                    || !ReferenceEquals(_roleBindings, sourceQueue)) return;
                                _busy = false; SetResult(success, issue);
                                if (success) ClosePopup();
                            }, out string enqueueIssue);
                        if (!accepted) { _busy = false; SetResult(false, enqueueIssue); }
                        return;
                    }
                    var live = _preparation.Find(_popupEntity)?.Loadout.Cards.Find(value => value.InstanceId == _popupCard);
                    var exact = _player.Catalog.ExactCard(live);
                    if (_controller.SelectedEntityId != _popupEntity || live == null || exact == null
                        || live.CardId != _popupReference.CardId || live.Revision != _popupReference.Revision
                        || live.ContentHash != _popupReference.ContentHash || exact.ActionSlotPath != _popupSlot)
                    { SetResult(false, "卡实例、版本或动作槽已变化，请关闭后重新选择。"); return; }
                    bool success = _preparation.BindAction(_popupEntity, _popupCard, action.Id, out string issue);
                    SetResult(success, issue); if (success) ClosePopup();
                });
                row.enabled = !instanceMode || _controller.CanEditRoleBindings && entity.InventoryOwnerId == _inventoryOwner.Value;
            };
            _popupList.numItems = actions.Length + (instanceMode ? 1 : 0);
            Label(_popup, "issue", instanceMode && !_controller.CanEditRoleBindings ? "权威装配入口尚未准备就绪。"
                : actions.Length == 0 ? "没有匹配的已学动作。" : ""); LayoutPopup();
        }

        private void ShowTeamPopup()
        {
            var entity = _preparation.Find(_controller.SelectedEntityId); if (_busy || entity == null) return;
            OpenPopup(PopupKind.Teams, "选择阵营", entity.DisplayName);
            _popupList.itemRenderer = (index, item) => { int team = index + 1; item.asButton.title = (entity.TeamId == team ? "✓  " : "") + "阵营 " + (team == 1 ? "A" : "B");
                RowButton(item.asButton, () => { if (_controller.SetSelectedTeam(team)) ClosePopup(); else Label(_popup, "issue", _controller.LastIssue); }); };
            _popupList.numItems = 2; LayoutPopup();
        }
        private void ShowPresets()
        {
            if (_busy) return; OpenPopup(PopupKind.Presets, "预设与进度", "预设只切换当前进度的整备；其他进度拥有独立卡组与整备。"); var presets = _preparation.Presets.ToArray();
            _popupList.itemRenderer = (index, item) => {
                if (index == 0) { item.asButton.title = "选择其他进度…"; RowButton(item.asButton, ShowProfiles); return; }
                var preset = presets[index - 1]; item.asButton.title = (_preparation.ActivePresetId == preset.PresetId ? "✓  " : "") + preset.Name;
                RowButton(item.asButton, () => RequestPresetLoad(preset.PresetId)); };
            _popupList.numItems = presets.Length + 1; Label(_popup, "issue", presets.Length == 0 ? "尚无命名预设，可另存当前配置。" : ""); LayoutPopup();
        }
        private void ShowProfiles()
        {
            if (_busy) return;
            OpenPopup(PopupKind.Profiles, "选择进度", "切换会保存当前整备，恢复所选进度的持有卡与整备；不会恢复战斗中途。");
            var profiles = CharacterTacticalServices.Profiles;
            _popupList.itemRenderer = (index, item) => {
                var profile = profiles[index]; bool active = profile.ProfileId == CharacterTacticalServices.ActiveProfileId;
                string stamp = DateTime.TryParse(profile.CreatedAtUtc, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var created) ? created.ToLocalTime().ToString("MM-dd HH:mm") : "";
                item.asButton.title = (active ? "✓ 当前 · " : "") + "进度 " + (index + 1) + " · " + stamp + " · " + profile.ProfileId.Substring(0, Math.Min(8, profile.ProfileId.Length));
                RowButton(item.asButton, () => {
                    if (_busy || active) return;
                    var flow = _controller.GetComponent<TacticalCardSliceCoordinator>();
                    string issue = "进度流程尚未准备就绪。";
                    if (flow != null && flow.RequestLoadProfile(profile.ProfileId, out issue)) ClosePopup();
                    else Label(_popup, "issue", issue);
                });
                item.asButton.enabled = !active;
            };
            _popupList.numItems = profiles.Length;
        }
        private void RequestPresetLoad(string presetId)
        {
            if (_busy) return; if (!_preparation.WorkingDiffersFromActivePreset) { LoadPreset(presetId); return; }
            OpenPopup(PopupKind.ReplacePreset, "切换预设", "当前工作配置尚未更新到预设。选择保存或直接切换。");
            string[] labels = { "另存后切换", "直接切换", "返回" };
            _popupList.itemRenderer = (index, item) => { item.asButton.title = labels[index]; RowButton(item.asButton, () => { if (index == 0) ShowPresetName(presetId); else if (index == 1) LoadPreset(presetId); else ClosePopup(); }); }; _popupList.numItems = 3;
        }
        private void ShowPresetName(string switchAfter)
        {
            if (_busy) return; _pendingPreset = switchAfter; _presetCreated = false;
            OpenPopup(PopupKind.NamePreset, "另存为预设", "输入名称。写入失败时保留修改，可重试保存。"); _popupList.visible = false;
            var input = Required(_popup, "input").asTextInput; input.visible = true; input.editable = true; input.promptText = "预设名称"; input.onChanged.Clear(); input.text = "";
            var confirm = Button(_popup, "confirm", SaveNamedPreset); confirm.visible = true; confirm.title = "保存";
        }
        private async void SaveNamedPreset()
        {
            if (_busy || _root == null) return; string name = Required(_popup, "input").asTextInput.text?.Trim() ?? "";
            if (!_presetCreated && !_preparation.SaveAsPreset(name, out _, out string issue)) { Label(_popup, "issue", issue); return; }
            _presetCreated = true; _busy = true; _revision = -1; int generation = _generation; var service = _preparation;
            Required(_popup, "input").asTextInput.editable = false; SetPopupBusy(true);
            try
            {
                bool saved = await service.FlushAsync(); if (_root == null || generation != _generation) return;
                if (!saved) { Label(_popup, "issue", service.SaveIssue); Required(_popup, "confirm").asButton.title = "重试保存"; return; }
                if (_pendingPreset.Length > 0)
                {
                    if (!service.LoadPreset(_pendingPreset, out string loadIssue)) { Label(_popup, "issue", loadIssue); return; }
                    _controller.RefreshLoadedPreset();
                    saved = await service.FlushAsync(); if (_root == null || generation != _generation) return;
                    if (!saved) { Label(_popup, "issue", service.SaveIssue); return; }
                    _controller.Select("");
                }
                ClosePopup(true);
            }
            catch (Exception error) { if (_root != null && generation == _generation) Label(_popup, "issue", "保存失败：" + error.Message); }
            finally { if (generation == _generation) { _busy = false; SetPopupBusy(false); _revision = -1; } }
        }
        private async void UpdatePreset()
        {
            if (_busy) return; var service = _preparation;
            if (service.WorkingDiffersFromActivePreset && !service.UpdateActivePreset(out string issue)) { SetResult(false, issue); return; }
            _busy = true; _revision = -1; int generation = _generation;
            try { bool saved = await service.FlushAsync(); if (generation == _generation) SetResult(saved, service.SaveIssue); }
            finally { if (generation == _generation) { _busy = false; _revision = -1; } }
        }
        private async void LoadPreset(string presetId)
        {
            if (_busy) return; var service = _preparation; bool loaded = service.LoadPreset(presetId, out string issue); SetResult(loaded, issue); if (!loaded) return;
            _controller.RefreshLoadedPreset();
            _busy = true; _revision = -1; int generation = _generation; SetPopupBusy(true);
            try { bool saved = await service.FlushAsync(); if (generation != _generation || _root == null) return; SetResult(saved, service.SaveIssue); if (saved) { _controller.Select(""); ClosePopup(true); } }
            finally { if (generation == _generation) { _busy = false; SetPopupBusy(false); _revision = -1; } }
        }
        private void SetPopupBusy(bool busy)
        {
            if (_root == null) return; Required(_popup, "confirm").enabled = Required(_popup, "cancel").enabled = Required(_popup, "close").enabled = !busy; _popupList.touchable = !busy;
        }
        private void HandlePopupKeys()
        {
            if (_popupKind == PopupKind.Closed || _busy) return; var keyboard = Keyboard.current; if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) ClosePopup(); else if (_popupKind == PopupKind.NamePreset && keyboard.enterKey.wasPressedThisFrame) SaveNamedPreset();
        }
        private void ClosePopup() => ClosePopup(false);
        private void ClosePopup(bool completed)
        {
            if (_busy && !completed) return; _popupKind = PopupKind.Closed; _controller?.HoldPopup(false);
            if (_root != null) { _popup.visible = _popupList.visible = Required(_root, "popupDismiss").visible = false; }
        }
        public void Dispose()
        {
            _generation++;
            DisposeCardMaterials();
            CancelTemplatePlacement();
            _controller?.SetUiOcclusion(0, 0, 0, 0);
            _controller?.BindPreparationSources(null, null);
            _characterImages?.Dispose(); _characterImages = null;
            _tooltips?.Dispose(); _tooltips = null;
            _summaries.Dispose();
            foreach (var hints in _cardHints.Values) hints.Dispose();
            _cardHints.Clear();
            _presentationCache.Clear();
            _roleBindings?.Cancel("整备界面已关闭或重绑。"); _roleBindings = null;
            _busy = false; _inventoryOwner = default; _inventoryIssue = "";
            _controller?.HoldPopup(false); _root = null; _controller = null; _preparation = null; _player = null;
            _popupKind = PopupKind.Closed; _revision = _summaryRevision = -1; _lastIssue = _localIssue = _search = _selection = ""; _busy = _presetCreated = false;
        }
    }
}

using System;
using MxFramework.Items;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using MxFramework.Game.CharacterTest.TacticalCard;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalPreparationView
    {
        private float _uiScale = 1f, _drawerWidth, _topHeight, _deckScale = 0.5f, _libraryScale = 0.5f;
        private int _placementCharacter;
        private ItemInstanceId _placementItem;
        private InventoryOwnerId _placementOwner;
        private long _placementPreparationRevision, _placementPlayerRevision;
        private bool _templateDragging;
        private float _suppressCharacterClickUntil;

        private static void Box(Fgui.GObject item, float x, float y, float width, float height)
        {
            item.SetXY(x, y); item.SetSize(width, height);
            // Small iconless controls must not retain the wide button's title inset.
            var button = item.asButton;
            if (button != null && string.IsNullOrEmpty(button.icon))
            {
                var title = button.GetTextField();
                if (title != null)
                {
                    title.SetXY(4, 0); title.SetSize(Mathf.Max(1, width - 8), height);
                    title.autoSize = Fgui.AutoSizeType.Shrink;
                    title.align = Fgui.AlignType.Center; title.verticalAlign = Fgui.VertAlignType.Middle;
                }
            }
        }

        private void SetupPreparationThemeControls()
        {
            Button(_drawerHeader, "pin", () => { _controller.ToggleDrawerPin(); _revision = -1; });
            Button(_drawerHeader, "back", _controller.ToggleCharacterDrawer);
            Button(_topBar, "pin", () => { _controller.ToggleTopPin(); _revision = -1; });
            Button(_topBar, "more", ShowMore).tooltips = "选中角色后，选择“移除选中角色…”并确认。";
            Button(_root, "deckAddCard", _controller.OpenCardDrawer);
            Button(_header, "removeEntity", ShowRemoveEntity);
            Required(_topBar, "refreshCatalog").visible = false;
            Required(_header, "addCard").visible = false;
            _deckList.layout = Fgui.ListLayoutType.SingleRow;
            Required(_root, "placementRing").touchable = false;
            var ghost = Required(_root, "placementPreview").asCom;
            ghost.touchable = false;
            foreach (var child in ghost.GetChildren()) child.touchable = false;
            Required(_root, "placementRing").sortingOrder = 125;
            ghost.sortingOrder = 126;
            foreach (string name in new[] { "deckSurface", "librarySurface", "entitySurface" })
                Required(_root, name).touchable = true;
            HoldDrawer(Required(_root, "librarySurface"));
            ConfigureEdgeHandle(Required(_root, "leftHandle").asButton, false);
            ConfigureEdgeHandle(Required(_root, "topHandle").asButton, true);
        }

        private void ConfigureEdgeHandle(Fgui.GButton button, bool top)
        {
            button.opaque = true;
            button.onClick.Clear();
            button.onRollOver.Set(() =>
            {
                if (top) { if (!_controller.TopExpanded) _controller.ToggleTop(); _controller.HoldTop(true); }
                else _controller.RevealDrawerFromPointer();
            });
            button.tooltips = top ? "预设与工具" : "角色库";
            Action draw = () =>
            {
                bool over = button.GetController("button").selectedPage != "up";
                float thickness = (over ? 14f : 5f) * _uiScale;
                foreach (string name in new[] { "base", "overBase", "downBase" })
                {
                    Fgui.GObject face = button.GetChild(name);
                    if (face != null) Box(face, 0, 0, top ? button.width : thickness, top ? thickness : button.height);
                }
                button.title = over ? (top ? "⌄" : "›") : string.Empty;
                var title = button.GetTextField();
                if (title != null) Box(title, 0, 0, top ? button.width : thickness, top ? thickness : button.height);
            };
            button.GetController("button").onChanged.Set(() => draw());
            draw();
        }

        private void UpdateDrawerPointerRegions()
        {
            if (_root == null || _popupKind != PopupKind.Closed || _controller.TemplateDragActive) return;
            if (_cardHints.Values.Any(hint => hint.HoldingWithAlt)) { _controller.HoldDrawer(true); return; }
            var point = Fgui.Stage.inst.touchPosition;
            bool drawer = PointerInside(Required(_root, "librarySurface"), point)
                || PointerInside(Required(_root, "leftHandle"), point);
            bool top = PointerInside(_topBar, point) || PointerInside(Required(_root, "topHandle"), point);
            _controller.HoldDrawer(drawer); _controller.HoldTop(top);
        }

        private void RenderFrostedLayout()
        {
            float width = _root.width, height = _root.height;
            _uiScale = Mathf.Clamp(Mathf.Min(width / 1920f, height / 1080f), 0.65f, 1.5f);
            float s = _uiScale, gap = 16f * s;
            bool drawerOpen = _controller.DrawerMode != TacticalPreparationDrawerMode.Collapsed;
            _drawerWidth = (_controller.DrawerMode == TacticalPreparationDrawerMode.Cards ? 480f : 424f) * s;
            _topHeight = _controller.TopExpanded ? 96f * s : 0f;
            var entity = _preparation.Find(_controller.SelectedEntityId);
            bool selected = entity != null;
            float top = Mathf.Max(64f * s, _topHeight + 28f * s);
            float inspectorWidth = 408f * s;
            var deck = Required(_root, "deckPanel");
            float availableLeft = drawerOpen ? _drawerWidth + gap * 2f : gap;
            float deckAvailableWidth = width - availableLeft - gap - 300f * s - gap;
            float deckWidth = Mathf.Min(1160f * s, deckAvailableWidth);
            float deckX = availableLeft + (deckAvailableWidth - deckWidth) * .5f;
            float deckHeight = (_controller.DrawerMode == TacticalPreparationDrawerMode.Cards ? 356f : 452f) * s;
            Box(deck, deckX, height - deckHeight - 32f*s, deckWidth, deckHeight);
            deck.visible = false;
            var deckSkin = Required(_root, "deckSurface");
            deckSkin.SetScale(s, s); Box(deckSkin, deck.x - 20*s, deck.y - 20*s, deck.width/s + 40, deck.height/s + 40); deckSkin.visible = selected;
            var inspector = Required(_root, "entitySurface");
            Box(inspector, width - inspectorWidth - gap, top, inspectorWidth, 416f * s); inspector.visible = selected;
            _header.visible = selected; _header.SetScale(s, s);
            Box(_header, inspector.x + gap, inspector.y + 16f * s, 376f, 384f);
            var portraitHost = Required(_root, "entityPortrait").asCom;
            var portrait = Required(portraitHost, "icon").asLoader;
            portraitHost.touchable = false;
            foreach (var child in portraitHost.GetChildren()) if (child != portrait) child.visible = false;
            portraitHost.visible = selected; Box(portraitHost, inspector.x + gap, inspector.y + 88f*s, 140f*s, 172f*s);
            Box(portrait, 0, 0, portraitHost.width, portraitHost.height);
            if (selected) BindConfiguredImage(portrait, entity.CharacterId, true);
            inspector.SetScale(s, s); Box(inspector, inspector.x - 20*s, inspector.y - 20*s, inspectorWidth/s + 40, 456);
            var deckTitle = Required(_root, "deckTitle"); deckTitle.visible = selected; deckTitle.SetScale(s, s);
            Box(deckTitle, deck.x + 24f*s, deck.y + 18f * s, 300f, 38f);
            Label(_root, "deckTitle", "出战卡组 · " + (entity?.Loadout.Cards.Count ?? 0));
            var add = Required(_root, "deckAddCard"); add.visible = selected && !entity.CharacterItemInstanceId.HasValue; add.SetScale(s, s);
            Box(add, deck.x + deck.width - 238f * s, deck.y + 12f * s, 214f, 46f);
            _deckScale = (deck.height - 96f * s) / 548f;
            _libraryScale = ((_drawerWidth - 68f * s) * 0.5f) / CharacterTestFairyGuiPlayerCardPresentation.FrameWidth;
            _deckList.defaultItemSize = CardTileSize(_deckScale);
            _libraryList.defaultItemSize = new Vector2(394f * _libraryScale, 594f * _libraryScale);
            _deckList.columnGap = Mathf.RoundToInt(16f * s); _libraryList.columnGap = Mathf.RoundToInt(12f * s);
            _deckList.visible = selected;
            Box(_deckList, deck.x + gap, deck.y + 72f * s, deck.width - gap * 2f, deck.height - 88f * s);
            var empty = Required(_root, "emptyHint"); empty.visible = selected && entity.Loadout.Cards.Count == 0;
            empty.SetScale(s, s); Box(empty, deck.x + 24f * s, deck.y + 86f * s, (deck.width - 48f * s) / s, 72f);
            Label(_root, "emptyHint", "暂无行为卡\n点击“添加行为卡”，为当前角色配置出战卡组。");
            if (selected) RenderEntity(entity);
            RenderDeck(entity); RenderDrawer(entity, _drawerWidth, height); RenderTop(width);
            _controller.SetUiOcclusion(drawerOpen ? (_drawerWidth + gap) / width : 0f,
                selected ? (inspectorWidth + gap) / width : 0f, selected ? (deck.height + gap) / height : 0f, _topHeight / height);
            var toast = Required(_root, "toast"); var toastBg = Required(_root, "toastBackground");
            toast.visible = toastBg.visible = !string.IsNullOrWhiteSpace(_lastIssue);
            float toastWidth = Mathf.Min(720f * s, width - (drawerOpen ? _drawerWidth : 0) - (selected ? inspectorWidth : 0) - 80f * s);
            toastBg.SetScale(s, s); toast.SetScale(s, s);
            Box(toastBg, (width - toastWidth) * 0.5f, _topHeight + 20f * s, toastWidth / s, 54f);
            Box(toast, toastBg.x + 16f * s, toastBg.y + 8f * s, toastWidth / s - 32f, 38f);
            Label(_root, "toast", _lastIssue);
            LayoutPopup(); foreach (var hints in _cardHints.Values) hints.RefreshPlacement();
        }

        private static void SetTextSize(Fgui.GTextField text, int size)
        { var format = text.textFormat; format.size = size; format.bold = true; text.textFormat = format; }

        private static Vector2 CardTileSize(float scale) => new Vector2(
            CharacterTestFairyGuiPlayerCardPresentation.FrameWidth * scale,
            548f * scale);
        private static void FitCardTile(Fgui.GComponent tile, Fgui.GComponent content, float scale)
        { content.SetScale(scale, scale); content.SetXY(0, 0); var size = CardTileSize(scale); tile.SetSize(size.x, size.y); }

        private void BindConfiguredImage(Fgui.GLoader target, int characterId, bool standing)
        {
            var definition = CharacterTestPlayableCharacterCatalog.Discover().FirstOrDefault(d => d.CharacterId == characterId);
            var images = definition.PresentationImages;
            string key = images?.ResolveResourceKey(standing);
            if (standing && string.IsNullOrWhiteSpace(key)) key = images?.ResolveResourceKey(false);
            _characterImages ??= new CharacterTestFairyGuiCharacterImageBinding(
                _controller.GetComponent<GameManager>()?.RuntimeContext?.Slice?.Resources?.ResourceManager);
            _characterImages.Bind(target, key);
        }

        private void RenderFrostedEntity(TacticalBattleEntitySetup entity)
        {
            Label(_header, "name", entity.DisplayName);
            var name = Required(_header, "name").asTextField;
            name.autoSize = Fgui.AutoSizeType.Shrink;
            Box(name, 12, 6, 312, 50); SetTextSize(name, 30);
            Box(Required(_header, "deselect"), 332, 8, 40, 40);
            Required(_header, "deselect").asButton.title = "×";
            Box(Required(_header, "team"), 160, 80, 212, 60);
            Box(Required(_header, "control"), 160, 158, 212, 60);
            Required(_header, "team").asButton.title = "阵营  " + (entity.TeamId == 1 ? "A" : "B") + "  ▾";
            Required(_header, "control").asButton.title = _preparation.Working.PlayerControlledEntityId == entity.EntityId ? "控制：玩家  ▾" : "控制：AI  ▾";
            Box(Required(_header, "pose"), 12, 270, 360, 32);
            Label(_header, "pose", "朝向  " + entity.YawDegrees.ToString("0") + "°");
            Required(_header, "pose").asTextField.align = Fgui.AlignType.Center;
            Box(Required(_header, "rotateLeft"), 12, 320, 168, 52);
            Box(Required(_header, "rotateRight"), 204, 320, 168, 52);
            Required(_header, "removeEntity").visible = false;
            Required(_header, "addCard").visible = false;
        }

        private void RenderFrostedDrawer(TacticalBattleEntitySetup selected, float width, float height)
        {
            float s = _uiScale, x = 24f * s, y = Mathf.Max(64f*s, _topHeight + 28f*s);
            bool characters = _controller.DrawerMode == TacticalPreparationDrawerMode.Characters;
            bool cards = _controller.DrawerMode == TacticalPreparationDrawerMode.Cards && selected != null;
            bool open = characters || cards;
            var proxy = Required(_root, "libraryPanel"); Box(proxy, x, y, width, height - y - 40f * s); proxy.visible = false;
            var skin = Required(_root, "librarySurface"); skin.SetScale(s, s); Box(skin, proxy.x - 20*s, proxy.y - 20*s, proxy.width/s + 40, proxy.height/s + 40); skin.visible = open;
            _drawerHeader.visible = open; _drawerHeader.SetScale(s, s); Box(_drawerHeader, x + 24f * s, y + 22f * s, width / s - 48, 92);
            float w = _drawerHeader.width;
            Box(Required(_drawerHeader, "collapse"), w - 36, 0, 36, 36);
            Box(Required(_drawerHeader, "pin"), w - 80, 0, 36, 36);
            Required(_drawerHeader, "collapse").asButton.title = "‹";
            Required(_drawerHeader, "pin").asButton.title = _controller.DrawerPinned ? "●" : "○";
            Required(_drawerHeader, "pin").tooltips = _controller.DrawerPinned ? "已固定：点击取消固定" : "固定抽屉";
            var back = Required(_drawerHeader, "back"); back.visible = cards; Box(back, 0, 0, 32, 36);
            Box(Required(_drawerHeader, "title"), cards ? 40 : 0, 0, w - (cards ? 130 : 88), 36);
            Label(_drawerHeader, "title", characters ? (_player.HasCharacterItemProgress ? "持有角色" : "角色库") : "行为卡库");
            SetTextSize(Required(_drawerHeader, "title").asTextField, 34);
            Box(Required(_drawerHeader, "hint"), 0, 58, w, 28);
            Label(_drawerHeader, "hint", characters ? (_player.HasCharacterItemProgress
                ? "选择已有实例 · 点击或拖拽放置" : "模板整备 · 点击或拖拽放置") : "添加至 " + selected?.DisplayName);
            var search = Required(_root, "search"); search.visible = cards; search.SetScale(s, s);
            Box(search, x + 24f * s, y + 116f * s, width / s - 48f, 48f);
            var searchSkin = Required(_root, "searchBackground"); searchSkin.visible = cards;
            Box(searchSkin, search.x - 6*s, search.y - 2*s, search.width*s + 12*s, search.height*s + 4*s);
            _characterList.visible = characters;
            Box(_characterList, x + 24f * s, y + 118f * s, width - 48f * s, proxy.height - 142f * s);
            _characterList.defaultItemSize = new Vector2(_characterList.width, 112f * s);
            _libraryList.visible = cards; Box(_libraryList, x + 24f * s, y + 184f * s, width - 48f * s, proxy.height - 208f * s);
            var handle = Required(_root, "leftHandle").asButton; handle.visible = !open;
            Box(handle, 0, height * 0.46f, 24f * s, 78f * s); ConfigureEdgeHandle(handle, false);
            if (characters) RenderCharacters(); int count = cards ? RenderLibrary(selected) : 0;
            var empty = Required(_root, "libraryEmpty"); empty.visible = (cards && count == 0) || (characters && _player.HasCharacterItemProgress && _characterList.numItems == 0); empty.SetScale(s, s);
            Box(empty, x + 24f * s, y + 166f * s, width / s - 48, 100);
            Label(_root, "libraryEmpty", characters && _player.HasCharacterItemProgress
                ? (string.IsNullOrWhiteSpace(_inventoryIssue) ? "当前库存归属下没有持有角色。" : _inventoryIssue)
                : selected?.CharacterItemInstanceId.HasValue == true ? "卡实例授予入口尚未接入；当前卡组读取角色已有配置。"
                : string.IsNullOrWhiteSpace(_search) ? "尚无可添加的已发布行为卡。" : "没有匹配的行为卡。\n请尝试其他名称。");
        }

        private void RenderFrostedCharacters()
        {
            if (_player.HasCharacterItemProgress) { RenderOwnedCharacterItems(); return; }
            var definitions = CharacterTestPlayableCharacterCatalog.Discover().Where(d => d.TacticalDefaults != null).ToArray();
            _characterList.itemRenderer = (index, item) =>
            {
                var definition = definitions[index]; var row = item.asButton; row.title = definition.DisplayName;
                row.SetSize(_characterList.width, 112f * _uiScale);
                RowButton(row, () => { if (!_templateDragging && Time.unscaledTime >= _suppressCharacterClickUntil) BeginTemplatePlacement(definition, false); });
                var icon = row.GetChild("icon").asLoader; icon.color = Color.white; icon.url = null;
                BindConfiguredImage(icon, definition.CharacterId, false);
                Box(icon, 12f * _uiScale, 10f * _uiScale, 116f * _uiScale, 92f * _uiScale); icon.fill = Fgui.FillType.Scale;
                var title = row.GetTextField(); Box(title, 144f * _uiScale, 12f * _uiScale, row.width - 158f * _uiScale, row.height);
                title.autoSize = Fgui.AutoSizeType.None; title.singleLine = false; title.align = Fgui.AlignType.Left; title.verticalAlign = Fgui.VertAlignType.Middle;
                var format = title.textFormat; format.size = Mathf.RoundToInt(23f * _uiScale); format.bold = true; title.textFormat = format;
                row.tooltips = definition.DisplayName + "\n拖到战场放置；点击后选择落点；Esc 取消。";
                row.draggable = true;
                row.onDragStart.Set(context => { context.PreventDefault(); Fgui.Stage.inst.CancelClick(context.inputEvent.touchId); BeginTemplatePlacement(definition, true); });
                row.onDragEnd.Clear();
            };
            _characterList.numItems = definitions.Length;
        }

        private void RenderOwnedCharacterItems()
        {
            if (!_player.TryReadOwnedCharacterItems(_inventoryOwner, out var characters, out _inventoryIssue))
            { _characterList.numItems = 0; return; }
            var definitions = CharacterTestPlayableCharacterCatalog.Discover().Where(value => value.TacticalDefaults != null).ToArray();
            var player = _player; var preparation = _preparation; var owner = _inventoryOwner;
            int generation = _generation; long playerRevision = player.Revision, preparationRevision = preparation.Revision;
            bool CurrentSource() => generation == _generation && ReferenceEquals(player, _player)
                && ReferenceEquals(preparation, _preparation) && owner.Equals(_inventoryOwner)
                && player.Revision == playerRevision && preparation.Revision == preparationRevision;
            _characterList.itemRenderer = (index, item) =>
            {
                var character = characters[index]; var row = item.asButton;
                var definition = definitions.FirstOrDefault(value => value.TacticalDefaults.CharacterStableId == character.CharacterStableId);
                bool duplicateName = characters.Count(value => value.DisplayName == character.DisplayName) > 1;
                row.data = character.ItemInstanceId;
                row.title = character.DisplayName + (duplicateName ? " · #" + character.ItemInstanceId : "");
                row.SetSize(_characterList.width, 112f * _uiScale);
                RowButton(row, () => {
                    if (CurrentSource() && !_templateDragging && Time.unscaledTime >= _suppressCharacterClickUntil)
                        BeginCharacterItemPlacement(character, definition, false);
                });
                var icon = row.GetChild("icon").asLoader; icon.color = Color.white; icon.url = null;
                if (definition.CharacterId != 0) BindConfiguredImage(icon, definition.CharacterId, false);
                Box(icon, 12f * _uiScale, 10f * _uiScale, 116f * _uiScale, 92f * _uiScale); icon.fill = Fgui.FillType.Scale;
                var title = row.GetTextField(); Box(title, 144f * _uiScale, 12f * _uiScale, row.width - 158f * _uiScale, row.height);
                title.autoSize = Fgui.AutoSizeType.None; title.singleLine = false; title.align = Fgui.AlignType.Left;
                title.verticalAlign = Fgui.VertAlignType.Middle;
                var format = title.textFormat; format.size = Mathf.RoundToInt(23f * _uiScale); format.bold = true; title.textFormat = format;
                row.enabled = definition.CharacterId != 0;
                row.tooltips = row.enabled ? character.DisplayName + "\n已出战时点击选中；否则点击选落点或拖到战场。" : "角色模板当前不可用。";
                row.draggable = row.enabled;
                row.onDragStart.Set(context => { context.PreventDefault(); Fgui.Stage.inst.CancelClick(context.inputEvent.touchId);
                    if (CurrentSource()) BeginCharacterItemPlacement(character, definition, true); });
                row.onDragEnd.Clear();
            };
            _characterList.numItems = characters.Length;
        }

        private void BeginCharacterItemPlacement(CharacterTacticalCharacterItemState character,
            CharacterTestCharacterDefinition definition, bool dragging)
        {
            var id = new ItemInstanceId(character.ItemInstanceId);
            if (!_player.TryReadCharacterItemLoadout(id, _inventoryOwner, out _, out _, out string issue))
            { SetResult(false, issue); return; }
            if (_controller.FocusCharacterItem(id, _inventoryOwner)) return;
            if (definition.CharacterId == 0) { SetResult(false, "角色模板当前不可用。"); return; }
            BeginTemplatePlacement(definition, dragging);
            _placementItem = id; _placementOwner = _inventoryOwner;
            _placementPreparationRevision = _preparation.Revision; _placementPlayerRevision = _player.Revision;
        }

        private void RenderFrostedTop(float width)
        {
            float s = _uiScale, w = (width - 24f * s) / s;
            _topBar.visible = _controller.TopExpanded; _topBar.SetScale(s, s); Box(_topBar, 12f * s, 0, w, 96);
            Box(Required(_topBar, "background"), 0, 0, w, 96);
            Box(Required(_topBar, "backgroundImage"), -20, -20, w+40, 136);
            Box(Required(_topBar, "title"), 24, 18, 234, 52);
            SetTextSize(Required(_topBar, "title").asTextField, 36);
            float controlsX = w - 848;
            Box(Required(_topBar, "status"), 280, 22, Mathf.Max(150, controlsX - 300), 44);
            SetTextSize(Required(_topBar, "status").asTextField, 17);
            Required(_topBar, "status").asTextField.autoSize = Fgui.AutoSizeType.Shrink;
            Box(Required(_topBar, "preset"), controlsX, 18, 290, 52);
            Box(Required(_topBar, "update"), controlsX + 304, 18, 164, 52);
            Box(Required(_topBar, "saveAs"), controlsX + 482, 18, 146, 52);
            Box(Required(_topBar, "more"), controlsX + 646, 18, 52, 52);
            Box(Required(_topBar, "pin"), controlsX + 714, 18, 46, 52);
            Box(Required(_topBar, "collapse"), controlsX + 772, 18, 52, 52);
            Required(_topBar, "collapse").asButton.title = "⌃";
            Required(_topBar, "pin").asButton.title = _controller.TopPinned ? "●" : "○";
            Required(_topBar, "pin").tooltips = _controller.TopPinned ? "已固定工具栏" : "固定工具栏";
            var start = Required(_root, "start").asButton;
            start.visible = true;
            start.SetScale(s, s);
            Box(start, width - 324f * s, _root.height - 100f * s, 300, 76);
            start.titleFontSize = 30;
            Required(_topBar, "refreshCatalog").visible = false;
            var active = _preparation.Presets.FirstOrDefault(v => v.PresetId == _preparation.ActivePresetId);
            Required(_topBar, "preset").asButton.title = (active?.Name ?? "工作配置") + "  ▾";
            Label(_topBar, "status", _busy ? "正在处理…" : !string.IsNullOrWhiteSpace(_preparation.SaveIssue) || !string.IsNullOrWhiteSpace(_player.SaveIssue) ? "保存失败，可重试"
                : _preparation.HasUnsavedChanges || _player.HasUnsavedChanges ? "正在保存进度…" : _preparation.WorkingDiffersFromActivePreset ? "已保存 · 预设待更新" : "✓ 已保存");
            Required(_topBar, "update").enabled = !_busy && active != null && (_preparation.WorkingDiffersFromActivePreset || _preparation.HasUnsavedChanges);
            Required(_topBar, "saveAs").enabled = !_busy;
            start.enabled = !_busy && _controller.CanStartBattle && string.IsNullOrEmpty(_controller.ConfigurationIssue);
            _tooltips.Bind(start, _controller.ConfigurationIssue,
                new MxFramework.UI.FairyGui.MxFairyGuiTooltipOptions(MxFramework.UI.FairyGui.MxFairyGuiTooltipPlacement.Above,
                    maximumTextWidth: 440, allowPin: false));
            var handle = Required(_root, "topHandle").asButton; handle.visible = !_topBar.visible;
            Box(handle, (width - 96f * s) * .5f, 0, 96f * s, 24f * s); ConfigureEdgeHandle(handle, true);
        }

        private void LayoutChoicePopup()
        {
            float s = _uiScale, width = _popupKind == PopupKind.Actions ? 420 : 360;
            float listTop = _popupKind == PopupKind.Actions ? 122 : 68;
            bool issue = !string.IsNullOrWhiteSpace(Required(_popup, "issue").text);
            float listHeight = Mathf.Clamp(_popupList.numItems * 64f, 64, 288);
            float height = listTop + listHeight + (issue ? 70 : 24);
            var anchor = _popupKind == PopupKind.Teams ? Required(_header, "team")
                : Required(_topBar, _popupKind == PopupKind.More ? "more" : "preset");
            Vector2 point = _popupKind == PopupKind.Actions ? _actionAnchor - new Vector2(0, (height+8)*s)
                : _root.GlobalToLocal(anchor.LocalToGlobal(new Vector2(0, anchor.height))) + new Vector2(0, 8*s);
            float x = Mathf.Clamp(point.x, 24*s, _root.width - (width+24)*s);
            float y = Mathf.Clamp(point.y, 24*s, _root.height - (height+24)*s);
            _popup.SetScale(s,s); Box(_popup, x,y,width,height);
            Box(Required(_popup, "backgroundImage"), -6,-6,width+12,height+12);
            Box(Required(_popup, "title"), 24,18,width-88,36);
            SetTextSize(Required(_popup, "title").asTextField, 24);
            Box(Required(_popup, "close"), width-60,18,36,36);
            Required(_popup, "context").visible = false;
            Required(_popup, "cancel").visible = false;
            Box(Required(_popup, "input"),24,66,width-48,40);
            Box(Required(_popup, "issue"),24,height-66,width-48,48);
            _popupList.SetScale(s,s); Box(_popupList,x+24*s,y+listTop*s,width-48,listHeight);
            Required(_root, "popupDismiss").SetSize(_root.width,_root.height);
        }

        private void ShowMore()
        {
            if (_busy) return;
            OpenPopup(PopupKind.More, "更多", "");
            int normalRows = _preparation.Find(_controller.SelectedEntityId) != null ? 4 : 3;
            bool retrySave = _player.HasUnsavedChanges || _preparation.HasUnsavedChanges
                || !string.IsNullOrWhiteSpace(_player.SaveIssue) || !string.IsNullOrWhiteSpace(_preparation.SaveIssue);
            _popupList.itemRenderer = (index, item) =>
            {
                if (retrySave && index == normalRows) { item.asButton.title = "重试保存进度"; RowButton(item.asButton, RetryProgressSave); item.enabled = true; return; }
                if (index == 3) { item.asButton.title = "移除选中角色…"; item.tooltips = "确认后移除该角色及其本次出战卡组。"; RowButton(item.asButton, ShowRemoveEntity); item.enabled = true; return; }
                if (index == 2)
                {
                    item.asButton.title = "领取机制试用卡…"; RowButton(item.asButton, ShowAcquireMechanismTrials);
                    item.enabled = !_player.HasCharacterItemProgress && _preparation.Find(_controller.SelectedEntityId) != null;
                    item.tooltips = item.enabled ? "向选中角色的当前进度领取三张已发布试用卡" : "请先选中战场角色";
                    return;
                }
                if (index == 1)
                {
                    item.asButton.title = "测试获取动作…"; RowButton(item.asButton, ShowAcquireActions);
                    item.enabled = !_player.HasCharacterItemProgress && _preparation.Find(_controller.SelectedEntityId) != null;
                    item.tooltips = item.enabled ? "向当前进度的角色持有清单添加动作" : "请先选中战场角色";
                    return;
                }
                item.asButton.title = "刷新卡库";
                RowButton(item.asButton, () => { ClosePopup(); _controller.RefreshPublishedCatalog(); _revision = -1; });
                item.asButton.enabled = !_busy && _controller.CanRefreshPublishedCatalog;
            };
            _popupList.numItems = normalRows + (retrySave ? 1 : 0); LayoutPopup();
        }

        private async void RetryProgressSave()
        {
            if (_busy) return;
            int generation = _generation; var player = _player; var preparation = _preparation;
            _busy = true;
            try
            {
                bool saved = await player.FlushAsync();
                if (saved) saved = await preparation.FlushAsync();
                if (generation != _generation || _root == null) return;
                SetResult(saved, !string.IsNullOrWhiteSpace(player.SaveIssue) ? player.SaveIssue : preparation.SaveIssue);
                if (saved) ClosePopup(true);
            }
            finally { if (generation == _generation) { _busy = false; _revision = -1; } }
        }

        private void ShowRemoveEntity()
        {
            var selected = _preparation.Find(_controller.SelectedEntityId); if (selected == null) return;
            string id = selected.EntityId;
            OpenPopup(PopupKind.Details, "移除角色？", selected.DisplayName + "\n此角色及其本次出战卡组将从整备中移除。");
            _popupList.visible = false;
            var confirm = Button(_popup, "confirm", () => { if (_controller.SelectedEntityId == id) _controller.RemoveSelected(); ClosePopup(); });
            confirm.visible = true; confirm.title = "移除角色";
        }

        private void BeginTemplatePlacement(CharacterTestCharacterDefinition definition, bool dragging)
        {
            _placementItem = default; _placementOwner = default;
            _placementCharacter = definition.CharacterId; _templateDragging = dragging;
            _controller.TemplateDragActive = true; _controller.HoldDrawer(true);
            var ghost = Required(_root, "placementPreview").asButton;
            BindConfiguredImage(ghost.GetChild("icon").asLoader, definition.CharacterId, false);
        }

        private bool PointerInside(Fgui.GObject item, Vector2 point)
        {
            if (item == null || !item.visible) return false;
            Vector2 local = item.GlobalToLocal(point);
            return local.x >= 0 && local.y >= 0 && local.x < item.width && local.y < item.height;
        }

        private void UpdateTemplatePreview()
        {
            if (_root == null) return;
            var ghost = Required(_root, "placementPreview").asButton;
            var ring = Required(_root, "placementRing").asGraph;
            if (_placementCharacter == 0) { ghost.visible = ring.visible = false; return; }
            var mouse = Mouse.current;
            if (mouse == null || !Application.isFocused || Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            { CancelTemplatePlacement(); return; }
            Vector2 screen = mouse.position.ReadValue(), point = new Vector2(screen.x, Screen.height - screen.y);
            bool overUi = PointerInside(Required(_root, "librarySurface"), point) || PointerInside(Required(_root, "deckSurface"), point)
                || PointerInside(Required(_root, "entitySurface"), point) || PointerInside(_topBar, point)
                || PointerInside(Required(_root, "start"), point);
            bool valid = !overUi && screen.x >= 0 && screen.x < Screen.width && screen.y >= 0 && screen.y < Screen.height
                && _controller.CanPlaceTemplate(screen);
            CharacterTestFairyGuiCursor.SetPlacement(this, _templateDragging, valid);
            ghost.visible = ring.visible = true;
            Vector2 localPoint = _root.GlobalToLocal(point);
            ghost.SetScale(_uiScale, _uiScale); Box(ghost, Mathf.Min(localPoint.x + 24, _root.width - 224 * _uiScale),
                Mathf.Clamp(localPoint.y - 64 * _uiScale, 0, _root.height - 64 * _uiScale), 210, 58);
            ghost.title = valid ? (_templateDragging ? "松开放置" : "点击放置 · Esc 取消") : "此处不可放置";
            var title = ghost.GetTextField(); Box(title, 56, 0, 150, 58); title.align = Fgui.AlignType.Left;
            var icon = ghost.GetChild("icon"); Box(icon, 8, 7, 44, 44);
            ring.SetXY(localPoint.x - 36 * _uiScale, localPoint.y - 14 * _uiScale);
            ring.DrawEllipse(72 * _uiScale, 28 * _uiScale, valid ? new Color(0.1f, .8f, .65f, .32f) : new Color(.9f, .25f, .2f, .32f));
            bool release = _templateDragging ? mouse.leftButton.wasReleasedThisFrame : mouse.leftButton.wasPressedThisFrame && !overUi;
            if (!release) return;
            int character = _placementCharacter; var item = _placementItem; var owner = _placementOwner;
            long preparationRevision = _placementPreparationRevision, playerRevision = _placementPlayerRevision;
            CancelTemplatePlacement(); _suppressCharacterClickUntil = Time.unscaledTime + .2f;
            if (valid)
            {
                if (item.IsValid) _controller.AddCharacterItemAtScreen(item, owner, screen, preparationRevision, playerRevision);
                else _controller.AddTemplateAtScreen(character, screen);
            }
        }

        private void CancelTemplatePlacement()
        {
            CharacterTestFairyGuiCursor.ClearPlacement(this);
            _placementCharacter = 0; _placementItem = default; _placementOwner = default; _templateDragging = false;
            if (_controller != null) { _controller.TemplateDragActive = false; _controller.HoldDrawer(false); }
            if (_root != null && !_root.isDisposed && _root.displayObject?.gameObject != null) { Required(_root, "placementPreview").visible = false; Required(_root, "placementRing").visible = false; }
        }
    }
}


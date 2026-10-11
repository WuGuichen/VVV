using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest.TacticalCard;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Binds only Composer-authored controls; mutations go through the shared inventory service.</summary>
    internal sealed class CharacterTestFairyGuiTacticalInventoryView : IDisposable
    {
        private Fgui.GComponent _root;
        private CharacterTacticalPlayerService _service;
        private CharacterTestCharacterDefinition[] _characters = Array.Empty<CharacterTestCharacterDefinition>();
        private int _characterIndex, _cardPage;
        private string _selectedCard, _hoveredCardId, _message = string.Empty;
        private ulong _selectedInstanceId;
        private string _searchText = string.Empty;
        private bool _dirty = true;
        private Action _confirmation;
        private string _confirmationText;
        private Fgui.GComponent _cardsHost;
        private Fgui.GComponent _deckHost;
        private Fgui.GComponent _actionPickerHost;
        private Fgui.GList _composerDeckList;
        private Fgui.GList _composerLibraryList;
        private Fgui.GComponent _composerTooltip;
        private bool _composerMode;
        private float _deckScrollY;
        private float _libraryScrollY;

        // Action picker modal state
        private bool _pickerOpen = false;
        private string _pickerTargetCardId;
        private ulong _pickerTargetInstanceId;
        private string _pickerTargetSlot;

        private const int CardPageSize = 4;
        private static readonly Dictionary<int, (string distance, string sector)> s_metricsCache = new Dictionary<int, (string distance, string sector)>();

        internal void Bind(Fgui.GComponent root, CharacterTacticalPlayerService service)
        {
            if (root == null || service == null) return;
            bool composerMode = root.GetChild("deckList")?.asList != null
                && root.GetChild("libraryList")?.asList != null;
            if (!composerMode && root.GetChild("tactical_background") == null) return;
            if (_service != service)
            {
                if (_service != null) _service.Changed -= Invalidate;
                _service = service; _service.Changed += Invalidate;
                _characters = CharacterTestPlayableCharacterCatalog.Discover().ToArray();
                _dirty = true;
            }
            if (_root != root)
            {
                _root = root;
                _cardsHost = null;
                _deckHost = null;
                _actionPickerHost = null;
                _composerDeckList = null;
                _composerLibraryList = null;
                _composerTooltip = null;
                _dirty = true;
            }
            _composerMode = composerMode;
            _root.alpha = 1f;
            _root.grayed = false;

            if (_composerMode)
            {
                EnsureComposerHosts();
                if (_dirty) RenderComposer();
                return;
            }

            EnsureHosts();

            // A legacy binder may refresh its projection; it never controls this inventory presentation.
            for (int i = 0; i < root.numChildren; i++)
            {
                var child = root.GetChildAt(i);
                if (!child.name.StartsWith("tactical_", StringComparison.Ordinal) && child.name != "btnClose") child.visible = false;
            }
            if (!_dirty) return;
            Render();
        }

        private void EnsureComposerHosts()
        {
            _composerDeckList = _root.GetChild("deckList")?.asList;
            _composerLibraryList = _root.GetChild("libraryList")?.asList;
            ConfigureComposerCardList(_composerDeckList);
            ConfigureComposerCardList(_composerLibraryList);
            if (_actionPickerHost == null || _actionPickerHost.parent != _root)
            {
                _actionPickerHost = new Fgui.GComponent
                {
                    name = "tactical_action_picker_host",
                    touchable = true,
                    sortingOrder = 200
                };
                _root.AddChild(_actionPickerHost);
            }
        }

        private static void ConfigureComposerCardList(Fgui.GList list)
        {
            if (list == null) return;
            list.layout = Fgui.ListLayoutType.FlowHorizontal;
            list.autoResizeItem = false;
            list.columnGap = 16;
            list.lineGap = 16;
            if (!list.isVirtual) list.SetVirtual();
        }

        private void RenderComposer()
        {
            _dirty = false;
            if (_characters.Length == 0) return;
            _characterIndex = Mathf.Clamp(_characterIndex, 0, _characters.Length - 1);
            string character = _characters[_characterIndex].TacticalDefaults?.CharacterStableId;
            CharacterTacticalDefaults owned = _service.Read(character);
            CharacterTacticalCardReference[] library = _service.Catalog.Cards
                .GroupBy(card => card.CardId, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(card => card.Revision).First())
                .Where(card => string.IsNullOrWhiteSpace(_searchText)
                    || Name(card.DisplayName, card.CardId).IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(card => new CharacterTacticalCardReference
                {
                    CardId = card.CardId,
                    Revision = card.Revision,
                    ContentHash = card.ContentHash
                })
                .ToArray();

            var search = _root.GetChild("search")?.asTextInput;
            if (search != null)
            {
                search.promptText = "搜索行为卡";
                if (search.text != _searchText) search.text = _searchText;
                search.onChanged.Set(() =>
                {
                    if (_searchText == search.text) return;
                    _searchText = search.text;
                    Invalidate();
                });
            }

            var empty = _root.GetChild("emptyHint");
            if (empty != null) empty.visible = owned == null || owned.Cards.Count == 0;
            var toast = _root.GetChild("toast")?.asTextField;
            if (toast != null)
            {
                toast.text = _message ?? string.Empty;
                toast.visible = !string.IsNullOrWhiteSpace(_message);
            }
            BindComposerDeck(character, owned);
            BindComposerLibrary(character, library);

            if (_pickerOpen)
            {
                _actionPickerHost.visible = true;
                RenderActionPicker(character, owned, null);
            }
            else
            {
                _actionPickerHost.RemoveChildren(0, -1, true);
                _actionPickerHost.visible = false;
            }
        }

        private void BindComposerDeck(string character, CharacterTacticalDefaults owned)
        {
            if (_composerDeckList == null) return;
            IReadOnlyList<CharacterTacticalCardReference> entries = owned != null
                ? owned.Cards
                : Array.Empty<CharacterTacticalCardReference>();
            _composerDeckList.itemRenderer = (index, item) =>
            {
                if (index < 0 || index >= entries.Count || item?.asCom == null) return;
                CharacterTacticalCardReference entry = entries[index];
                CharacterTacticalCardDefinition definition = _service.Catalog.Card(entry);
                BindComposerCard(item.asCom, definition, deck: true);
                ulong instanceId = entry.InstanceId;
                bool hasActionSlot = definition?.HasActionSlot == true;
                string actionSlotPath = definition?.ActionSlotPath ?? string.Empty;
                CharacterTacticalSlotBinding binding = !hasActionSlot ? null : owned.Bindings.Find(value =>
                    value.CardInstanceId == instanceId && value.SlotPath == actionSlotPath);
                CharacterTacticalActionDefinition action = binding == null ? null : _service.Catalog.Action(binding.TacticalActionId);
                var footer = item.asCom.GetChild("footer")?.asTextField;
                if (footer != null)
                {
                    footer.visible = true;
                    footer.text = !hasActionSlot
                        ? "无动作槽"
                        : action != null
                            ? Name(action.DisplayName, action.Id) + " ▼"
                            : "选择动作 ▼";
                    footer.touchable = false;
                    var footerHit = EnsureCardHitTarget(item.asCom, "footer_hit", footer);
                    footerHit.visible = hasActionSlot;
                    footerHit.touchable = hasActionSlot;
                    footerHit.onClick.Set(context =>
                    {
                        if (!hasActionSlot) return;
                        context.StopPropagation();
                        OpenActionPicker(instanceId, entry.CardId, actionSlotPath);
                    });
                }
                var add = item.asCom.GetChild("add"); if (add != null) add.visible = false;
                var addHit = item.asCom.GetChild("add_hit"); if (addHit != null) addHit.visible = false;
                var remove = item.asCom.GetChild("remove");
                var drag = item.asCom.GetChild("dragHandle");
                if (drag != null) drag.visible = false;
                if (remove != null)
                {
                    remove.visible = false; remove.touchable = false;
                    var removeHit = EnsureCardHitTarget(item.asCom, "remove_hit", remove);
                    removeHit.visible = false;
                    removeHit.onClick.Set(context =>
                    {
                        context.StopPropagation();
                        _service.RemoveDeckEntry(character, instanceId, out _message);
                        Invalidate();
                    });
                }
                item.draggable = false;
                item.dragBounds = null;
                item.onDragStart.Set((Fgui.EventCallback1)null);
                item.onDragEnd.Set((Fgui.EventCallback1)null);
                item.onRollOver.Set(() =>
                {
                    if (remove != null) remove.visible = true;
                    var removeHit = item.asCom.GetChild("remove_hit"); if (removeHit != null) removeHit.visible = true;
                    if (action != null) ShowComposerTooltip(item.asCom, action);
                });
                item.onRollOut.Set(() =>
                {
                    if (remove != null) remove.visible = false;
                    var removeHit = item.asCom.GetChild("remove_hit"); if (removeHit != null) removeHit.visible = false;
                    HideComposerTooltip(item.asCom);
                });
            };
            _composerDeckList.numItems = entries.Count;
        }

        private void BindComposerLibrary(string character, CharacterTacticalCardReference[] references)
        {
            if (_composerLibraryList == null) return;
            _composerLibraryList.itemRenderer = (index, item) =>
            {
                if (index < 0 || index >= references.Length || item?.asCom == null) return;
                CharacterTacticalCardReference reference = references[index];
                BindComposerCard(item.asCom, _service.Catalog.Card(reference), deck: false);
                var footer = item.asCom.GetChild("footer"); if (footer != null) footer.visible = true;
                var footerHit = item.asCom.GetChild("footer_hit"); if (footerHit != null) footerHit.visible = false;
                var remove = item.asCom.GetChild("remove"); if (remove != null) remove.visible = false;
                var removeHit = item.asCom.GetChild("remove_hit"); if (removeHit != null) removeHit.visible = false;
                var drag = item.asCom.GetChild("dragHandle"); if (drag != null) drag.visible = false;
                var add = item.asCom.GetChild("add");
                if (add != null)
                {
                    add.visible = false; add.touchable = false;
                    var addHit = EnsureCardHitTarget(item.asCom, "add_hit", add);
                    addHit.visible = false;
                    addHit.onClick.Set(context =>
                    {
                        context.StopPropagation();
                        _service.AddDeckEntry(character, reference, out _, out _message);
                        Invalidate();
                    });
                }
                item.draggable = false;
                item.dragBounds = null;
                item.onDragStart.Set((Fgui.EventCallback1)null);
                item.onDragEnd.Set((Fgui.EventCallback1)null);
                item.onRollOver.Set(() => { if (add != null) { add.visible = true; if (footer != null) footer.visible = false; } });
                item.onRollOver.Add(() => { var hit = item.asCom.GetChild("add_hit"); if (hit != null) hit.visible = true; });
                item.onRollOut.Set(() =>
                {
                    if (add != null) { add.visible = false; if (footer != null) footer.visible = true; }
                    var hit = item.asCom.GetChild("add_hit"); if (hit != null) hit.visible = false;
                });
            };
            _composerLibraryList.numItems = references.Length;
        }

        private void ShowComposerTooltip(Fgui.GComponent card, CharacterTacticalActionDefinition action)
        {
            if (_composerTooltip != null)
            {
                _composerTooltip.RemoveFromParent();
                _composerTooltip.Dispose();
            }
            _composerTooltip = BuildActionTooltip(action);
            _composerTooltip.sortingOrder = 180;
            Vector2 position = _root.GlobalToLocal(card.LocalToGlobal(new Vector2(card.width + 10f, card.height - _composerTooltip.height)));
            if (position.x + _composerTooltip.width > _root.width - 16f)
                position.x = Mathf.Max(16f, position.x - card.width - _composerTooltip.width - 20f);
            position.y = Mathf.Clamp(position.y, 16f, _root.height - _composerTooltip.height - 16f);
            _composerTooltip.SetXY(position.x, position.y);
            _root.AddChild(_composerTooltip);
            _composerTooltip.onRollOut.Set(() => { if (!IsPointerOver(card)) HideComposerTooltip(card); });
        }

        private void HideComposerTooltip(Fgui.GComponent card)
        {
            if (_composerTooltip == null || IsPointerOver(_composerTooltip) || IsPointerOver(card)) return;
            _composerTooltip.RemoveFromParent();
            _composerTooltip.Dispose();
            _composerTooltip = null;
        }

        private static void BindComposerCard(Fgui.GComponent card, CharacterTacticalCardDefinition definition, bool deck)
        {
            var background = card.GetChild("background");
            if (background != null) background.touchable = false;
            var icon = card.GetChild("icon");
            if (icon != null) icon.touchable = false;
            var title = card.GetChild("title")?.asTextField;
            if (title != null)
            {
                title.touchable = false;
                title.text = FormatCardTitle(Name(definition?.DisplayName, definition?.CardId));
            }
            var description = card.GetChild("description")?.asTextField;
            if (description != null)
            {
                description.touchable = false;
                description.text = ShortCardDescription(definition?.Description);
            }
            var footer = card.GetChild("footer")?.asTextField;
            if (footer != null) footer.text = definition?.HasActionSlot == true
                ? "有动作槽"
                : "无动作槽";
        }

        private static Fgui.GComponent EnsureCardHitTarget(
            Fgui.GComponent card,
            string name,
            Fgui.GObject source)
        {
            var hit = card.GetChild(name)?.asCom;
            if (hit == null)
            {
                hit = new Fgui.GComponent
                {
                    name = name,
                    opaque = true,
                    touchable = true,
                    sortingOrder = 250
                };
                card.AddChild(hit);
            }
            hit.SetXY(source.x, source.y);
            hit.SetSize(source.width, source.height);
            return hit;
        }

        private void EnsureHosts()
        {
            if (_root == null) return;
            if (_cardsHost == null || _cardsHost.parent != _root)
            {
                _cardsHost = _root.GetChild("tactical_cards_host")?.asCom;
                if (_cardsHost == null)
                {
                    _cardsHost = new Fgui.GComponent { name = "tactical_cards_host", touchable = true, sortingOrder = 10 };
                    _root.AddChild(_cardsHost);
                }
            }
            _cardsHost.sortingOrder = 110;
            if (_deckHost == null || _deckHost.parent != _root)
            {
                _deckHost = _root.GetChild("tactical_deck_host")?.asCom;
                if (_deckHost == null)
                {
                    _deckHost = new Fgui.GComponent { name = "tactical_deck_host", touchable = true, sortingOrder = 12 };
                    _root.AddChild(_deckHost);
                }
            }
            _deckHost.sortingOrder = 120;
            if (_actionPickerHost == null || _actionPickerHost.parent != _root)
            {
                _actionPickerHost = _root.GetChild("tactical_action_picker_host")?.asCom;
                if (_actionPickerHost == null)
                {
                    _actionPickerHost = new Fgui.GComponent { name = "tactical_action_picker_host", touchable = true, sortingOrder = 100 };
                    _root.AddChild(_actionPickerHost);
                }
            }
            _actionPickerHost.sortingOrder = 200;
            var close = _root.GetChild("btnClose");
            if (close != null) close.sortingOrder = 300;
            var tt = _root.GetChild("tactical_tooltip_host");
            if (tt != null) tt.visible = false;
        }

        private void Invalidate() { _dirty = true; }

        private void Render()
        {
            _dirty = false;
            EnsureHosts();

            for (int i = 0; i < _root.numChildren; i++)
            {
                var c = _root.GetChildAt(i);
                if (c.name.StartsWith("tactical_", StringComparison.Ordinal)) c.visible = true;
            }

            // Completely hide legacy left column (tactical actions list)
            Visible("background", false);
            Visible("actions_title", false);
            Visible("actions_previous", false);
            Visible("actions_page", false);
            Visible("actions_next", false);
            for (int r = 0; r < 12; r++) Row("actions", r, false);

            // Completely hide legacy 3rd column (slots list)
            Visible("slots_title", false);
            Visible("slots_previous", false);
            Visible("slots_page", false);
            Visible("slots_next", false);
            for (int r = 0; r < 12; r++) Row("slots", r, false);

            // Hide legacy 2nd column raw text rows
            for (int r = 0; r < 12; r++) Row("cards", r, false);
            Visible("cards_title", false);

            // Hide legacy tooltip host if present
            var tt = _root.GetChild("tactical_tooltip_host");
            if (tt != null) tt.visible = false;

            if (_characters.Length == 0) { Text("status", "没有已导入角色。"); return; }
            _characterIndex = Math.Max(0, Math.Min(_characterIndex, _characters.Length - 1));
            var character = _characters[_characterIndex];
            string key = character.TacticalDefaults?.CharacterStableId;
            var owned = _service.Read(key);

            foreach (string id in new[] { "character", "previous_character", "next_character", "save", "reset", "status",
                "cards_previous", "cards_page", "cards_next", "actions_title", "slots_title" }) Visible(id, false);
            var cardRefs = _service.Catalog.Cards.GroupBy(card => card.CardId, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(card => card.Revision).First())
                .Where(card => string.IsNullOrWhiteSpace(_searchText)
                    || (card.DisplayName ?? card.CardId).IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(card => new CharacterTacticalCardReference { CardId = card.CardId,
                    Revision = card.Revision, ContentHash = card.ContentHash }).ToArray();
            RenderMinimalDeck(key, owned);
            RenderMinimalLibrary(key, cardRefs);

            // 5. Action Picker Modal
            if (_pickerOpen)
            {
                _actionPickerHost.visible = true;
                RenderActionPicker(key, owned, null);
            }
            else
            {
                _actionPickerHost.RemoveChildren(0, -1, true);
                _actionPickerHost.visible = false;
            }

            foreach (string id in new[] { "background", "text", "yes", "no" }) Visible("confirm_" + id, false);
        }

        private void RenderMinimalDeck(string character, CharacterTacticalDefaults owned)
        {
            _deckHost.RemoveChildren(0, -1, true);
            float width = _root.width * 0.49f, height = _root.height - 56f;
            _deckHost.SetXY(24f, 28f); _deckHost.SetSize(width - 36f, height);
            var panel = new Fgui.GComponent { name = "deck_panel", touchable = true, opaque = true };
            panel.SetSize(_deckHost.width, _deckHost.height);
            AddRoundedRect(panel, "panel", 0, 0, panel.width, panel.height,
                new Color32(5, 13, 20, 235), new Color32(31, 72, 94, 220), 1f, 12f, 0);
            _deckHost.AddChild(panel);
            if (owned == null || owned.Cards.Count == 0)
            {
                var empty = CreateText("empty", "从右侧添加行为卡", 16, false, new Color32(100, 125, 145, 220), Fgui.AlignType.Center);
                empty.SetXY(20f, panel.height * 0.45f); empty.SetSize(panel.width - 40f, 30f); empty.sortingOrder = 10;
                panel.AddChild(empty); return;
            }
            const float cardW = 220f, cardH = 330f, gap = 16f;
            int columns = Math.Max(1, Mathf.FloorToInt((panel.width - 32f + gap) / (cardW + gap)));
            int rows = Mathf.CeilToInt(owned.Cards.Count / (float)columns);
            float contentHeight = 16f + rows * (cardH + gap);
            _deckScrollY = Mathf.Clamp(_deckScrollY, 0f, Mathf.Max(0f, contentHeight - panel.height));
            var viewport = CreateScrollViewport("deck_viewport", panel.width, panel.height, _deckScrollY,
                value => { _deckScrollY = value; Invalidate(); });
            viewport.sortingOrder = 10;
            panel.AddChild(viewport);
            for (int i = 0; i < owned.Cards.Count; i++)
            {
                CharacterTacticalCardReference entry = owned.Cards[i];
                CharacterTacticalCardDefinition definition = _service.Catalog.Card(entry);
                float x = 16f + (i % columns) * (cardW + gap);
                float logicalY = 16f + (i / columns) * (cardH + gap);
                float y = logicalY - _deckScrollY;
                if (y + cardH < 0f || y > viewport.height) continue;
                var card = BuildMinimalCard(definition, cardW, cardH, true);
                card.name = "deck_" + entry.InstanceId;
                card.SetXY(x, y); viewport.AddChild(card);
                ulong capturedInstance = entry.InstanceId;
                card.draggable = false;

                var remove = CreateText("remove", "×", 18, true, new Color32(255, 120, 135, 255), Fgui.AlignType.Center);
                remove.SetXY(cardW - 34f, 8f); remove.SetSize(26f, 26f); remove.touchable = true; remove.visible = false;
                remove.onClick.Set(ctx => { ctx.StopPropagation(); _service.RemoveDeckEntry(character, capturedInstance, out _message); Invalidate(); });
                card.AddChild(remove);

                string[] slots = definition?.Slots ?? Array.Empty<string>();
                CharacterTacticalSlotBinding binding = slots.Length == 0 ? null : owned.Bindings.Find(value =>
                    value.CardInstanceId == capturedInstance && value.SlotPath == slots[0]);
                CharacterTacticalActionDefinition action = binding == null ? null : _service.Catalog.Action(binding.TacticalActionId);
                var footer = card.GetChild("footer")?.asTextField;
                if (footer != null) footer.text = slots.Length == 0 ? "自动解析" : Name(action?.DisplayName, "选择动作") + " ▼";
                if (footer != null && slots.Length > 0)
                {
                    footer.touchable = true;
                    string targetSlot = slots[0];
                    footer.onClick.Set(ctx => { ctx.StopPropagation(); OpenActionPicker(capturedInstance, entry.CardId, targetSlot); });
                }

                Fgui.GComponent tooltip = action == null ? null : BuildActionTooltip(action);
                if (tooltip != null) { tooltip.SetXY(x + cardW + 8f, y + cardH - tooltip.height); tooltip.visible = false; tooltip.sortingOrder = 80; viewport.AddChild(tooltip); }
                card.onRollOver.Add(() => { remove.visible = true; if (tooltip != null) tooltip.visible = true; });
                card.onRollOut.Add(() => { remove.visible = false; if (tooltip != null && !IsPointerOver(tooltip)) tooltip.visible = false; });
                if (tooltip != null) tooltip.onRollOut.Add(() => { if (!IsPointerOver(card)) tooltip.visible = false; });
            }
        }

        private void RenderMinimalLibrary(string character, CharacterTacticalCardReference[] references)
        {
            _cardsHost.RemoveChildren(0, -1, true);
            float startX = _root.width * 0.5f + 8f, width = _root.width - startX - 24f, height = _root.height - 56f;
            _cardsHost.SetXY(startX, 28f); _cardsHost.SetSize(width, height);
            var panel = new Fgui.GComponent { name = "library_panel", touchable = true, opaque = true };
            panel.SetSize(width, height);
            AddRoundedRect(panel, "panel", 0, 0, width, height,
                new Color32(5, 13, 20, 235), new Color32(31, 72, 94, 220), 1f, 12f, 0);
            _cardsHost.AddChild(panel);

            var search = new Fgui.GTextInput { name = "search" };
            search.promptText = "搜索行为卡"; search.text = _searchText;
            Fgui.TextFormat searchFormat = search.textFormat; searchFormat.size = 15;
            searchFormat.color = new Color32(220, 232, 240, 255); search.textFormat = searchFormat;
            search.SetXY(width - 450f, 16f); search.SetSize(270f, 38f);
            search.sortingOrder = 20;
            search.onChanged.Add(() => { if (_searchText != search.text) { _searchText = search.text; Invalidate(); } });
            panel.AddChild(search);

            const float cardW = 220f, cardH = 330f, gap = 16f;
            int columns = Math.Max(1, Mathf.FloorToInt((width - 32f + gap) / (cardW + gap)));
            float viewportY = 66f;
            float viewportHeight = Mathf.Max(1f, height - viewportY);
            int rows = Mathf.CeilToInt(references.Length / (float)columns);
            float contentHeight = 6f + rows * (cardH + gap);
            _libraryScrollY = Mathf.Clamp(_libraryScrollY, 0f, Mathf.Max(0f, contentHeight - viewportHeight));
            var viewport = CreateScrollViewport("library_viewport", width, viewportHeight, _libraryScrollY,
                value => { _libraryScrollY = value; Invalidate(); });
            viewport.SetXY(0f, viewportY);
            viewport.sortingOrder = 10;
            panel.AddChild(viewport);
            for (int i = 0; i < references.Length; i++)
            {
                CharacterTacticalCardReference reference = references[i];
                CharacterTacticalCardDefinition definition = _service.Catalog.Card(reference);
                float x = 16f + (i % columns) * (cardW + gap);
                float y = 6f + (i / columns) * (cardH + gap) - _libraryScrollY;
                if (y + cardH < 0f || y > viewport.height) continue;
                var card = BuildMinimalCard(definition, cardW, cardH, false); card.SetXY(x, y); viewport.AddChild(card);
                var add = CreateText("add", "＋ 添加一张", 14, true, new Color32(5, 22, 28, 255), Fgui.AlignType.Center);
                add.SetXY(0f, cardH - 42f); add.SetSize(cardW, 42f); add.touchable = true; add.visible = false;
                add.onClick.Set(ctx => { ctx.StopPropagation(); _service.AddDeckEntry(character, reference, out _, out _message); Invalidate(); });
                card.AddChild(add);
                card.onRollOver.Add(() => add.visible = true);
                card.onRollOut.Add(() => add.visible = false);
            }
        }

        private static Fgui.GComponent CreateScrollViewport(
            string name,
            float width,
            float height,
            float scrollY,
            Action<float> changed)
        {
            var viewport = new Fgui.GComponent { name = name, touchable = true, opaque = true };
            viewport.SetSize(width, height);
            viewport.rootContainer.clipRect = new Rect(0f, 0f, width, height);
            viewport.displayObject.onMouseWheel.Set(context =>
            {
                float delta = context?.inputEvent?.mouseWheelDelta ?? 0f;
                if (Mathf.Approximately(delta, 0f)) return;
                changed?.Invoke(Mathf.Max(0f, scrollY + (delta < 0f ? 180f : -180f)));
                context.StopPropagation();
            });
            return viewport;
        }

        private Fgui.GComponent BuildMinimalCard(CharacterTacticalCardDefinition definition, float width, float height, bool deck)
        {
            var card = new Fgui.GComponent { touchable = true, opaque = true };
            card.SetSize(width, height);
            AddRoundedRect(card, "card", 0, 0, width, height, new Color32(12, 27, 38, 255),
                new Color32(38, 76, 98, 255), 1.2f, 12f, 0);
            BindHoverState(card, "card", new Color32(12, 27, 38, 255), new Color32(38, 76, 98, 255),
                new Color32(15, 34, 47, 255), new Color32(68, 196, 168, 255));
            var icon = new Fgui.GGraph { name = "icon", touchable = false };
            icon.SetXY((width - 72f) * 0.5f, 24f); icon.SetSize(72f, 72f);
            icon.DrawEllipse(72f, 72f, new Color32(17, 34, 47, 255)); card.AddChild(icon);
            var glyph = CreateText("glyph", "×", 32, true, new Color32(68, 196, 168, 255), Fgui.AlignType.Center);
            glyph.SetXY((width - 56f) * 0.5f, 39f); glyph.SetSize(56f, 40f); card.AddChild(glyph);
            var title = CreateText("title", Name(definition?.DisplayName, definition?.CardId), 19, true, Color.white, Fgui.AlignType.Left);
            title.SetXY(18f, 116f); title.SetSize(width - 36f, 28f); title.autoSize = Fgui.AutoSizeType.Shrink; card.AddChild(title);
            var body = CreateText("body", ShortCardDescription(definition?.Description),
                13, false, new Color32(203, 213, 225, 255), Fgui.AlignType.Left);
            body.SetXY(18f, 154f); body.SetSize(width - 36f, 54f); body.singleLine = false; card.AddChild(body);
            string footerText = definition?.Slots?.Length > 0 ? definition.Slots.Length + " 个动作槽" : "自动解析";
            var footer = CreateText("footer", footerText, 13, true, new Color32(133, 237, 214, 255), Fgui.AlignType.Center);
            footer.SetXY(14f, height - 54f); footer.SetSize(width - 28f, 34f); card.AddChild(footer);
            return card;
        }

        private static string ShortCardDescription(string description)
        {
            string value = string.IsNullOrWhiteSpace(description) ? "按正式行为源执行。" : description.Trim();
            const int maximumCharacters = 40;
            return value.Length <= maximumCharacters ? value : value.Substring(0, maximumCharacters - 1) + "…";
        }

        private static string FormatCardTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title) || title.Length <= 7 || title.IndexOf('\n') >= 0)
                return title;
            return title.Substring(0, 7) + "\n" + title.Substring(7);
        }

        private Fgui.GComponent BuildActionTooltip(CharacterTacticalActionDefinition action)
        {
            var metrics = GetActionMetrics(action.ActionId);
            var tooltip = new Fgui.GComponent { name = "action_tooltip", touchable = true, opaque = true };
            tooltip.SetSize(190f, 120f);
            AddRoundedRect(tooltip, "tip", 0, 0, 190f, 120f, new Color32(16, 30, 41, 255),
                new Color32(68, 196, 168, 255), 1.5f, 10f, 0);
            var title = CreateText("title", action.DisplayName, 13, true, new Color32(133, 237, 214, 255), Fgui.AlignType.Left);
            title.SetXY(14f, 12f); title.SetSize(162f, 24f); tooltip.AddChild(title);
            var values = CreateText("values", metrics.distance + "\n" + metrics.sector, 14, false, Color.white, Fgui.AlignType.Left);
            values.SetXY(14f, 50f); values.SetSize(162f, 54f); tooltip.AddChild(values); return tooltip;
        }

        private void RenderTacticalCards(string key, CharacterTacticalDefaults owned, CharacterTacticalAcquisitionPool pool, CharacterTacticalCardReference[] cardRefs)
        {
            _cardsHost.RemoveChildren(0, -1, true);
            _cardsHost.SetXY(0f, 0f);
            _cardsHost.SetSize(_root.width, _root.height);

            float cardW = 280f;
            float cardH = 340f;
            float cardY = 90f;
            float spacingX = 36f;

            int remaining = cardRefs.Length - _cardPage * CardPageSize;
            int cardsOnThisPage = Mathf.Max(1, Mathf.Min(CardPageSize, remaining));
            float totalW = cardsOnThisPage * cardW + Mathf.Max(0, cardsOnThisPage - 1) * spacingX;
            float startX = Mathf.Max(40f, (_root.width - totalW) * 0.5f);

            for (int i = 0; i < CardPageSize; i++)
            {
                int cardIndex = _cardPage * CardPageSize + i;
                if (cardIndex >= cardRefs.Length) break;

                var reference = cardRefs[cardIndex];
                var card = _service.Catalog.Card(reference);
                bool has = owned?.Cards.Any(c => c.CardId == reference.CardId) == true;
                bool inAcquisition = pool?.Cards.Any(candidate => candidate.CardId == reference.CardId
                    && candidate.Revision == reference.Revision && candidate.ContentHash == reference.ContentHash) == true;
                bool isSelected = string.Equals(_selectedCard, reference.CardId, StringComparison.Ordinal);

                // Resolve slots and bindings
                var slots = card?.Slots ?? Array.Empty<string>();

                CharacterTacticalSlotBinding binding = null;
                string slotName = "action";
                if (slots.Length > 0)
                {
                    slotName = slots[0];
                    binding = owned?.Bindings.Find(b => b.CardId == reference.CardId && b.SlotPath == slotName)
                        ?? (slots.Length == 1 ? owned?.Bindings.Find(b => b.CardId == reference.CardId && b.SlotPath == "action") : null);
                }

                bool isSpecializedBound = binding != null;
                var boundActionDef = isSpecializedBound ? _service.Catalog.Action(binding.TacticalActionId) : null;
                string boundActionName = isSpecializedBound ? Name(boundActionDef?.DisplayName, binding.TacticalActionId) : null;
                var metrics = isSpecializedBound ? GetActionMetrics(boundActionDef?.ActionId ?? 0) : default;

                float cardX = startX + i * (cardW + spacingX);

                // 1. Main Tactical Card (Ultra-clean 280x340, centered layout)
                var cardCom = new Fgui.GComponent { name = "card_" + i, touchable = true, opaque = true, sortingOrder = 10 };
                cardCom.SetXY(cardX, cardY);
                cardCom.SetSize(cardW, cardH);
                _cardsHost.AddChild(cardCom);

                string capturedCardId = reference.CardId;
                string capturedSlotName = slotName;
                cardCom.onClick.Set(() => {
                    _selectedCard = capturedCardId;
                    Invalidate();
                });

                // Card Frame & Fill
                Color32 borderColor = isSelected ? new Color32(68, 196, 168, 255) : new Color32(35, 69, 87, 255);
                float stroke = isSelected ? 2f : 1.5f;
                Color32 fillColor = new Color32(11, 22, 32, 255);
                AddRoundedRect(cardCom, "card_bg", 0, 0, cardW, cardH, fillColor, borderColor, stroke, 16f, order: 0);

                cardCom.onRollOver.Set(() => {
                    _hoveredCardId = capturedCardId;
                    if (!isSelected)
                    {
                        var b = cardCom.GetChild("card_bg_border")?.asGraph;
                        if (b != null) b.color = new Color32(52, 98, 122, 255);
                    }
                });
                cardCom.onRollOut.Set(() => {
                    if (_hoveredCardId == capturedCardId) _hoveredCardId = null;
                    if (!isSelected)
                    {
                        var b = cardCom.GetChild("card_bg_border")?.asGraph;
                        if (b != null) b.color = borderColor;
                    }
                });

                // Top-Left Circle Icon Badge (diameter 44px, scaled vector mechanism icon)
                DrawTopLeftBadge(cardCom, 44f, 44f, 22f, isSpecializedBound, reference.CardId);

                // Top-Right Deck Status Badge
                float badgeW = 74f; float badgeH = 24f;
                float badgeX = cardW - badgeW - 14f; float badgeY = 14f;
                var deckBadge = new Fgui.GComponent { name = "deckBadge", touchable = true, opaque = true, sortingOrder = 15 };
                deckBadge.SetXY(badgeX, badgeY);
                deckBadge.SetSize(badgeW, badgeH);
                if (has)
                {
                    AddRoundedRect(deckBadge, "db_bg", 0, 0, badgeW, badgeH, new Color32(6, 46, 36, 240), new Color32(16, 185, 129, 255), 1.2f, 12f, order: 1);
                    var dbText = CreateText("db_txt", "✓ 出战中", 12, true, new Color32(52, 211, 153, 255), Fgui.AlignType.Center);
                    dbText.SetXY(0, 3f);
                    dbText.SetSize(badgeW, 18f);
                    deckBadge.AddChild(dbText);
                    BindHoverState(deckBadge, "db_bg", new Color32(6, 46, 36, 240), new Color32(16, 185, 129, 255),
                        new Color32(12, 70, 55, 255), new Color32(52, 211, 153, 255));
                    deckBadge.onClick.Set(ctx => {
                        ctx.StopPropagation();
                        _service.TryRemoveCard(key, reference.CardId, out _message);
                        _message = "已将“" + Name(card?.DisplayName, reference.CardId) + "”移出出战牌库。";
                        Invalidate();
                    });
                }
                else
                {
                    AddRoundedRect(deckBadge, "db_bg", 0, 0, badgeW, badgeH, new Color32(20, 30, 42, 240), new Color32(55, 75, 95, 255), 1f, 12f, order: 1);
                    var dbText = CreateText("db_txt", "＋ 未出战", 12, false, new Color32(148, 163, 184, 255), Fgui.AlignType.Center);
                    dbText.SetXY(0, 3f);
                    dbText.SetSize(badgeW, 18f);
                    deckBadge.AddChild(dbText);
                    BindHoverState(deckBadge, "db_bg", new Color32(20, 30, 42, 240), new Color32(55, 75, 95, 255),
                        new Color32(32, 48, 66, 255), new Color32(96, 165, 250, 255));
                    deckBadge.onClick.Set(ctx => {
                        ctx.StopPropagation();
                        if (owned?.Cards.Count >= 4)
                        {
                            _message = "出战卡组最多支持 4 张卡牌，请先移出一张再装入。";
                            Invalidate();
                            return;
                        }
                        if (inAcquisition)
                        {
                            if (!_service.TryGrantCard(key, reference, out _message)) { Invalidate(); return; }
                        }
                        else
                        {
                            if (!_service.TryGrantCardForTesting(key, reference, out _message)) { Invalidate(); return; }
                            _message = "已将“" + Name(card?.DisplayName, reference.CardId) + "”加入出战牌库。";
                        }
                        _selectedCard = reference.CardId;
                        Invalidate();
                    });
                }
                cardCom.AddChild(deckBadge);

                // Card Title (Bold white, 19px, left aligned, below circle)
                float titleY = 82f;
                string titleText = string.Equals(reference.CardId, "tactical.bound-action", StringComparison.Ordinal)
                    ? "接近并执行动作"
                    : Name(card?.DisplayName, reference.CardId);
                var title = CreateText("title", titleText, 19, true, new Color32(255, 255, 255, 255), Fgui.AlignType.Left);
                title.autoSize = Fgui.AutoSizeType.Shrink;
                title.SetXY(20f, titleY);
                title.SetSize(cardW - 40f, 28f);
                cardCom.AddChild(title);

                // Natural Language Description
                float descY = 120f;
                string descText = slots.Length > 0
                    ? "以 1 倍速移动并转向目标，释放"
                    : "固有战术机制，按机动策略自动生效。";
                var desc = CreateText("desc", descText, 13, false, new Color32(203, 213, 225, 255), Fgui.AlignType.Left);
                desc.SetXY(20f, descY);
                desc.SetSize(cardW - 40f, 22f);
                cardCom.AddChild(desc);

                // Slot Pill & Suffix
                if (slots.Length > 0)
                {
                    float pillX = 20f;
                    float pillY = 150f;
                    float pillH = 30f;
                    float pillW = isSpecializedBound
                        ? Mathf.Clamp((boundActionName?.Length ?? 0) * 13f + 42f, 110f, cardW - 46f)
                        : 138f;

                    var slotPill = new Fgui.GComponent { name = "slotPill", touchable = true, opaque = true, sortingOrder = 15 };
                    slotPill.SetXY(pillX, pillY);
                    slotPill.SetSize(pillW, pillH);

                    string targetCardIdForSlot = reference.CardId;
                    string targetSlotForSlot = slotName;

                    Color32 spBorder = isSpecializedBound ? new Color32(68, 196, 168, 255) : new Color32(68, 196, 168, 200);
                    Color32 spFill = isSpecializedBound ? new Color32(22, 50, 52, 240) : new Color32(24, 38, 48, 240);
                    Color32 spHoverBorder = new Color32(145, 255, 230, 255);
                    Color32 spHoverFill = new Color32(35, 80, 84, 255);

                    if (isSpecializedBound)
                    {
                        AddRoundedRect(slotPill, "sp_bg", 0, 0, pillW, pillH, spFill, spBorder, 1.2f, 6f, order: 1);
                        var pillText = CreateText("sp_txt", $"{boundActionName} ▾", 13, true, new Color32(133, 237, 214, 255), Fgui.AlignType.Center);
                        pillText.autoSize = Fgui.AutoSizeType.Shrink;
                        pillText.SetXY(8f, 5f);
                        pillText.SetSize(pillW - 16f, 20f);
                        slotPill.AddChild(pillText);
                    }
                    else
                    {
                        AddRoundedRect(slotPill, "sp_bg", 0, 0, pillW, pillH, spFill, spBorder, 1.2f, 6f, order: 1);
                        var pillText = CreateText("sp_txt", "✛ 选择战术动作 ▾", 13, true, new Color32(133, 237, 214, 255), Fgui.AlignType.Center);
                        pillText.autoSize = Fgui.AutoSizeType.Shrink;
                        pillText.SetXY(8f, 5f);
                        pillText.SetSize(pillW - 16f, 20f);
                        slotPill.AddChild(pillText);
                    }

                    BindHoverState(slotPill, "sp_bg", spFill, spBorder, spHoverFill, spHoverBorder);

                    slotPill.onClick.Set(ctx => {
                        ctx.StopPropagation();
                        OpenActionPicker(targetCardIdForSlot, targetSlotForSlot);
                    });
                    cardCom.AddChild(slotPill);

                    // Suffix period "。"
                    var suffix = CreateText("suffix", "。", 14, false, new Color32(203, 213, 225, 255), Fgui.AlignType.Left);
                    suffix.SetXY(pillX + pillW + 3f, pillY + 3f);
                    suffix.SetSize(20f, 24f);
                    cardCom.AddChild(suffix);

                    // Quick Specs inside Card
                    if (isSpecializedBound)
                    {
                        var specsTxt = CreateText("specs_txt", $"{metrics.distance}   |   {metrics.sector}", 12, false, new Color32(68, 196, 168, 220), Fgui.AlignType.Left);
                        specsTxt.SetXY(20f, 190f);
                        specsTxt.SetSize(cardW - 40f, 20f);
                        cardCom.AddChild(specsTxt);
                    }

                    // Explicit bottom button: [ ⚙ 更换动作 ] or [ ✛ 装配动作 ]
                    float cfgBtnW = cardW - 40f;
                    float cfgBtnH = 34f;
                    var cfgBtn = new Fgui.GComponent { name = "cfgBtn", touchable = true, opaque = true, sortingOrder = 15 };
                    cfgBtn.SetXY(20f, 286f);
                    cfgBtn.SetSize(cfgBtnW, cfgBtnH);
                    AddRoundedRect(cfgBtn, "cb_bg", 0, 0, cfgBtnW, cfgBtnH, new Color32(18, 36, 46, 230), new Color32(45, 80, 100, 220), 1.2f, 6f, order: 1);
                    var cbTxt = CreateText("cb_txt", isSpecializedBound ? "⚙ 更换动作" : "✛ 装配动作", 13, true, new Color32(148, 220, 205, 255), Fgui.AlignType.Center);
                    cbTxt.SetXY(0, 7f);
                    cbTxt.SetSize(cfgBtnW, 20f);
                    cfgBtn.AddChild(cbTxt);
                    BindHoverState(cfgBtn, "cb_bg", new Color32(18, 36, 46, 230), new Color32(45, 80, 100, 220),
                        new Color32(28, 56, 70, 255), new Color32(68, 196, 168, 255));
                    cfgBtn.onClick.Set(ctx => {
                        ctx.StopPropagation();
                        OpenActionPicker(targetCardIdForSlot, targetSlotForSlot);
                    });
                    cardCom.AddChild(cfgBtn);
                }

                // 2. Slay the Spire (STS) Companion Tooltip Sub-Card (Overlay popout on hover)
                if (isSpecializedBound)
                {
                    float ttW = 190f;
                    float ttH = 115f;
                    float bridgeW = 16f;

                    // If card is on the far right, popout to the left so it never clips off screen
                    bool popLeft = cardX + cardW + ttW + bridgeW > _root.width - 20f;
                    float ttX = popLeft ? (cardX - ttW - bridgeW) : (cardX + cardW);
                    float ttY = cardY + 60f;

                    var ttCom = new Fgui.GComponent { name = "sts_tooltip_" + i, touchable = true, opaque = true, sortingOrder = 100 };
                    ttCom.SetXY(ttX, ttY);
                    ttCom.SetSize(ttW + bridgeW, ttH);
                    ttCom.visible = false; // Non-permanent: only pops out on hover

                    Color32 ttBorder = new Color32(68, 196, 168, 255);
                    Color32 ttFill = new Color32(16, 30, 41, 255);
                    Color32 ttHoverBorder = new Color32(145, 255, 230, 255);
                    Color32 ttHoverFill = new Color32(26, 50, 68, 255);

                    float rectOffsetX = popLeft ? 0f : bridgeW;
                    AddRoundedRect(ttCom, "tt_bg", rectOffsetX, 0, ttW, ttH, ttFill, ttBorder, 1.5f, 12f, order: 1);
                    BindHoverState(ttCom, "tt_bg", ttFill, ttBorder, ttHoverFill, ttHoverBorder);

                    // Header: {boundActionName}
                    var ttHead = CreateText("tt_head", boundActionName, 14, true, new Color32(133, 237, 214, 255), Fgui.AlignType.Left);
                    ttHead.autoSize = Fgui.AutoSizeType.Shrink;
                    ttHead.SetXY(rectOffsetX + 14f, 10f);
                    ttHead.SetSize(ttW - 28f, 22f);
                    ttCom.AddChild(ttHead);

                    // Divider: 1px subtle line
                    var ttDiv = new Fgui.GGraph { name = "tt_div", touchable = false, sortingOrder = 5 };
                    ttDiv.SetXY(rectOffsetX + 14f, 36f);
                    ttDiv.SetSize(ttW - 28f, 1f);
                    ttDiv.DrawRect(ttW - 28f, 1f, 0, Color.clear, new Color32(27, 52, 68, 255));
                    ttCom.AddChild(ttDiv);

                    // Distance stat (≤ 2.2m)
                    var ttDist = CreateText("tt_dist", metrics.distance, 15, true, new Color32(248, 250, 252, 255), Fgui.AlignType.Left);
                    ttDist.SetXY(rectOffsetX + 14f, 46f);
                    ttDist.SetSize(ttW - 28f, 24f);
                    ttCom.AddChild(ttDist);

                    // Angle stat (前方 ±25°)
                    var ttSec = CreateText("tt_sec", metrics.sector, 14, true, new Color32(133, 237, 214, 255), Fgui.AlignType.Left);
                    ttSec.SetXY(rectOffsetX + 14f, 74f);
                    ttSec.SetSize(ttW - 28f, 24f);
                    ttCom.AddChild(ttSec);

                    // Click tooltip to open action picker as well
                    ttCom.onClick.Set(ctx => {
                        ctx.StopPropagation();
                        OpenActionPicker(capturedCardId, capturedSlotName);
                    });

                    _cardsHost.AddChild(ttCom);

                    // Hover show/hide lifecycle (transient tooltip)
                    var localCard = cardCom;
                    var localTt = ttCom;
                    Action checkTooltipHide = () => {
                        if (!IsPointerOver(localCard) && !IsPointerOver(localTt))
                        {
                            localTt.visible = false;
                        }
                    };

                    cardCom.onRollOver.Add(() => {
                        localTt.visible = true;
                    });
                    cardCom.onRollOut.Add(() => {
                        checkTooltipHide();
                    });

                    ttCom.onRollOver.Add(() => {
                        localTt.visible = true;
                    });
                    ttCom.onRollOut.Add(() => {
                        checkTooltipHide();
                    });
                }
            }
        }

        private void RenderDeckTray(string key, CharacterTacticalDefaults owned, CharacterTacticalAcquisitionPool pool)
        {
            _deckHost.RemoveChildren(0, -1, true);
            _deckHost.SetXY(0f, 0f);
            _deckHost.SetSize(_root.width, _root.height);

            float trayW = 1360f;
            float trayH = 230f;
            float trayX = Mathf.Max(40f, (_root.width - trayW) * 0.5f);
            float trayY = 500f;

            var trayCom = new Fgui.GComponent { name = "deck_tray", touchable = true, opaque = true, sortingOrder = 12 };
            trayCom.SetXY(trayX, trayY);
            trayCom.SetSize(trayW, trayH);
            _deckHost.AddChild(trayCom);

            // Tray Background
            Color32 trayFill = new Color32(9, 17, 24, 235);
            Color32 trayBorder = new Color32(32, 58, 76, 220);
            AddRoundedRect(trayCom, "tray_bg", 0, 0, trayW, trayH, trayFill, trayBorder, 1.5f, 16f, order: 0);

            // Tray Header
            int equippedCount = owned?.Cards.Count ?? 0;
            var trayHeader = CreateText("tray_header", $"【当前出战卡组】  (已出战: {equippedCount} / 4 张 · 战斗中按机动策略循环调度)", 16, true, new Color32(148, 205, 224, 255), Fgui.AlignType.Left);
            trayHeader.SetXY(30f, 16f);
            trayHeader.SetSize(700f, 26f);
            trayCom.AddChild(trayHeader);

            // 4 Slots
            int slotCount = 4;
            float slotW = 300f;
            float slotH = 158f;
            float slotSpacing = 26f;
            float totalSlotsW = slotCount * slotW + (slotCount - 1) * slotSpacing;
            float slotsStartX = (trayW - totalSlotsW) * 0.5f;
            float slotY = 54f;

            for (int s = 0; s < slotCount; s++)
            {
                float slotX = slotsStartX + s * (slotW + slotSpacing);
                var slotCom = new Fgui.GComponent { name = "slot_" + s, touchable = true, opaque = true, sortingOrder = 15 };
                slotCom.SetXY(slotX, slotY);
                slotCom.SetSize(slotW, slotH);
                trayCom.AddChild(slotCom);

                if (owned != null && s < owned.Cards.Count)
                {
                    // Equipped Card Slot
                    var cardRef = owned.Cards[s];
                    var cardDef = _service.Catalog.Card(cardRef);
                    string cardTitle = string.Equals(cardRef.CardId, "tactical.bound-action", StringComparison.Ordinal)
                        ? "接近并执行动作"
                        : Name(cardDef?.DisplayName, cardRef.CardId);

                    // Only published `binding` slots are player-configurable. Functional resolvers have no saved binding.
                    var playerSlots = cardDef?.Slots ?? Array.Empty<string>();
                    var binding = playerSlots.Length == 0 ? null
                        : owned.Bindings.Find(b => b.CardId == cardRef.CardId && b.SlotPath == playerSlots[0]);
                    bool isBound = binding != null;
                    var boundActionDef = isBound ? _service.Catalog.Action(binding.TacticalActionId) : null;
                    string boundActionName = isBound ? Name(boundActionDef?.DisplayName, binding.TacticalActionId) : null;
                    var metrics = isBound ? GetActionMetrics(boundActionDef?.ActionId ?? 0) : default;

                    // Slot frame
                    Color32 sFill = new Color32(15, 28, 38, 240);
                    Color32 sBorder = new Color32(42, 78, 98, 220);
                    Color32 sHoverFill = new Color32(22, 42, 56, 255);
                    Color32 sHoverBorder = new Color32(68, 196, 168, 255);
                    AddRoundedRect(slotCom, "s_bg", 0, 0, slotW, slotH, sFill, sBorder, 1.2f, 10f, order: 1);
                    BindHoverState(slotCom, "s_bg", sFill, sBorder, sHoverFill, sHoverBorder);

                    string capturedCardId = cardRef.CardId;
                    slotCom.onClick.Set(() => {
                        _selectedCard = capturedCardId;
                        Invalidate();
                    });

                    // Sequence Badge [ 01 ]
                    var seqTxt = CreateText("seq", $"0{s + 1}", 13, true, new Color32(68, 196, 168, 255), Fgui.AlignType.Left);
                    seqTxt.SetXY(14f, 12f);
                    seqTxt.SetSize(30f, 20f);
                    slotCom.AddChild(seqTxt);

                    // Card Name
                    var nameTxt = CreateText("name", cardTitle, 15, true, Color.white, Fgui.AlignType.Left);
                    nameTxt.autoSize = Fgui.AutoSizeType.Shrink;
                    nameTxt.SetXY(46f, 12f);
                    nameTxt.SetSize(slotW - 128f, 22f);
                    slotCom.AddChild(nameTxt);

                    // [ ✕ 移出 ] Unequip button
                    float unbW = 62f;
                    float unbH = 24f;
                    var unequipBtn = new Fgui.GComponent { name = "unb_btn", touchable = true, opaque = true, sortingOrder = 20 };
                    unequipBtn.SetXY(slotW - unbW - 12f, 11f);
                    unequipBtn.SetSize(unbW, unbH);
                    AddRoundedRect(unequipBtn, "unb_bg", 0, 0, unbW, unbH, new Color32(46, 22, 28, 240), new Color32(180, 50, 65, 230), 1f, 4f, order: 1);
                    var unbTxt = CreateText("unb_txt", "✕ 移出", 11, true, new Color32(254, 202, 202, 255), Fgui.AlignType.Center);
                    unbTxt.SetXY(0, 4f);
                    unbTxt.SetSize(unbW, 16f);
                    unequipBtn.AddChild(unbTxt);
                    BindHoverState(unequipBtn, "unb_bg", new Color32(46, 22, 28, 240), new Color32(180, 50, 65, 230),
                        new Color32(75, 30, 38, 255), new Color32(239, 68, 68, 255));
                    unequipBtn.onClick.Set(ctx => {
                        ctx.StopPropagation();
                        _service.TryRemoveCard(key, capturedCardId, out _message);
                        _message = $"已将“{cardTitle}”移出出战卡组。";
                        Invalidate();
                    });
                    slotCom.AddChild(unequipBtn);

                    // Divider line
                    var div = new Fgui.GGraph { name = "s_div", touchable = false, sortingOrder = 3 };
                    div.SetXY(12f, 40f);
                    div.SetSize(slotW - 24f, 1f);
                    div.DrawRect(slotW - 24f, 1f, 0, Color.clear, new Color32(32, 54, 70, 200));
                    slotCom.AddChild(div);

                    if (playerSlots.Length > 0)
                    {
                        // Bound Action Pill
                        float apW = slotW - 28f;
                        float apH = 32f;
                        var actPill = new Fgui.GComponent { name = "act_pill", touchable = true, opaque = true, sortingOrder = 20 };
                        actPill.SetXY(14f, 50f);
                        actPill.SetSize(apW, apH);
                        Color32 apFill = isBound ? new Color32(18, 42, 46, 240) : new Color32(20, 32, 42, 240);
                        Color32 apBorder = isBound ? new Color32(68, 196, 168, 255) : new Color32(50, 75, 95, 220);
                        Color32 apHFill = isBound ? new Color32(28, 64, 70, 255) : new Color32(30, 48, 64, 255);
                        Color32 apHBorder = isBound ? new Color32(145, 255, 230, 255) : new Color32(80, 140, 180, 255);
                        AddRoundedRect(actPill, "ap_bg", 0, 0, apW, apH, apFill, apBorder, 1.2f, 6f, order: 1);
                        BindHoverState(actPill, "ap_bg", apFill, apBorder, apHFill, apHBorder);
                        string actLabel = isBound ? $"{boundActionName} ▾" : "✛ 装配动作 ▾";
                        Color32 actColor = isBound ? new Color32(133, 237, 214, 255) : new Color32(148, 163, 184, 255);
                        var actTxt = CreateText("ap_txt", actLabel, 13, true, actColor, Fgui.AlignType.Center);
                        actTxt.autoSize = Fgui.AutoSizeType.Shrink;
                        actTxt.SetXY(8f, 6f);
                        actTxt.SetSize(apW - 16f, 20f);
                        actPill.AddChild(actTxt);
                        string targetPlayerSlot = playerSlots[0];
                        actPill.onClick.Set(ctx => { ctx.StopPropagation(); OpenActionPicker(capturedCardId, targetPlayerSlot); });
                        slotCom.AddChild(actPill);

                        string detail = isBound ? $"{metrics.distance}    {metrics.sector}" : "尚未装配战术招式";
                        var metricsTxt = CreateText("m_txt", detail, 12, false,
                            isBound ? new Color32(68, 196, 168, 230) : new Color32(148, 163, 184, 200), Fgui.AlignType.Left);
                        metricsTxt.SetXY(16f, 92f);
                        metricsTxt.SetSize(slotW - 32f, 20f);
                        slotCom.AddChild(metricsTxt);
                    }
                    else
                    {
                        var resolver = CreateText("functional_resolver", "无玩家装配槽 · 按正式行为源自动解析", 12, false,
                            new Color32(133, 190, 210, 230), Fgui.AlignType.Left);
                        resolver.SetXY(16f, 58f);
                        resolver.SetSize(slotW - 32f, 42f);
                        slotCom.AddChild(resolver);
                    }

                    // Tactical mode hint
                    var modeTxt = CreateText("mode_txt", "移动并自动索敌释放", 11, false, new Color32(120, 145, 165, 200), Fgui.AlignType.Left);
                    modeTxt.SetXY(16f, 122f);
                    modeTxt.SetSize(slotW - 32f, 18f);
                    slotCom.AddChild(modeTxt);
                }
                else
                {
                    // Empty Slot
                    Color32 esFill = new Color32(11, 20, 28, 180);
                    Color32 esBorder = new Color32(26, 46, 62, 200);
                    Color32 esHFill = new Color32(16, 28, 40, 220);
                    Color32 esHBorder = new Color32(40, 75, 100, 255);
                    AddRoundedRect(slotCom, "es_bg", 0, 0, slotW, slotH, esFill, esBorder, 1f, 10f, order: 1);
                    BindHoverState(slotCom, "es_bg", esFill, esBorder, esHFill, esHBorder);

                    // Sequence Badge
                    var seqTxt = CreateText("seq", $"0{s + 1}", 13, false, new Color32(90, 115, 135, 255), Fgui.AlignType.Left);
                    seqTxt.SetXY(14f, 12f);
                    seqTxt.SetSize(30f, 20f);
                    slotCom.AddChild(seqTxt);

                    // Empty label
                    var empLbl = CreateText("emp_lbl", "[ 空出战槽位 ]", 13, false, new Color32(100, 125, 145, 255), Fgui.AlignType.Left);
                    empLbl.SetXY(46f, 12f);
                    empLbl.SetSize(slotW - 60f, 20f);
                    slotCom.AddChild(empLbl);

                    // Center Guide Icon / Text
                    var guideTxt = CreateText("guide_txt", "+ 从上方牌库点击“＋未出战”装入", 12, false, new Color32(85, 115, 135, 255), Fgui.AlignType.Center);
                    guideTxt.SetXY(10f, 68f);
                    guideTxt.SetSize(slotW - 20f, 24f);
                    slotCom.AddChild(guideTxt);

                    var guideSub = CreateText("guide_sub", "最多构筑 4 张出战组合", 11, false, new Color32(65, 88, 105, 220), Fgui.AlignType.Center);
                    guideSub.SetXY(10f, 96f);
                    guideSub.SetSize(slotW - 20f, 20f);
                    slotCom.AddChild(guideSub);

                    slotCom.onClick.Set(() => {
                        _message = "请在上方牌库中点击“＋ 未出战”即可将卡牌装入出战卡组。";
                        Invalidate();
                    });
                }
            }
        }

        private void OpenActionPicker(string targetCardId, string targetSlot)
        {
            _pickerTargetInstanceId = 0;
            _pickerTargetCardId = targetCardId;
            _pickerTargetSlot = targetSlot;
            _pickerOpen = true;
            _message = "正在为卡牌选择战术招式装配… 点击目标招式完成装配。";
            Invalidate();
        }

        private void OpenActionPicker(ulong instanceId, string targetCardId, string targetSlot)
        {
            _pickerTargetInstanceId = instanceId;
            _pickerTargetCardId = targetCardId;
            _pickerTargetSlot = targetSlot;
            _pickerOpen = true;
            _message = string.Empty;
            Invalidate();
        }

        private void RenderActionPicker(string key, CharacterTacticalDefaults owned, CharacterTacticalAcquisitionPool pool)
        {
            _actionPickerHost.RemoveChildren(0, -1, true);
            _actionPickerHost.SetXY(0, 0);
            _actionPickerHost.SetSize(_root.width, _root.height);

            // Fullscreen backdrop to focus user attention
            var backdrop = new Fgui.GGraph { name = "picker_backdrop", touchable = true, sortingOrder = 0 };
            backdrop.SetXY(0, 0);
            backdrop.SetSize(_root.width, _root.height);
            backdrop.DrawRect(_root.width, _root.height, 0, Color.clear, new Color32(4, 8, 14, 215));
            backdrop.onClick.Set(() => {
                _pickerOpen = false;
                _message = string.Empty;
                Invalidate();
            });
            _actionPickerHost.AddChild(backdrop);

            // Action list query
            var actionIds = (owned?.ActionIds ?? new List<string>()).Distinct().ToArray();
            int maxVisibleRows = 5;
            int rowCount = Math.Min(maxVisibleRows, actionIds.Length);

            var existingBinding = _pickerTargetInstanceId > 0
                ? owned?.Bindings.Find(b => b.CardInstanceId == _pickerTargetInstanceId && b.SlotPath == _pickerTargetSlot)
                : owned?.Bindings.Find(b => b.CardId == _pickerTargetCardId && b.SlotPath == _pickerTargetSlot);

            // Modal Dialog
            float modalW = 860f;
            float modalH = actionIds.Length == 0 ? 280f : Mathf.Clamp(125f + rowCount * 84f + 25f, 240f, 580f);
            float modalX = Mathf.Max(40f, (_root.width - modalW) * 0.5f);
            float modalY = Mathf.Max(40f, (_root.height - modalH) * 0.5f);

            var modal = new Fgui.GComponent { name = "picker_modal", touchable = true, opaque = true, sortingOrder = 10 };
            modal.SetXY(modalX, modalY);
            modal.SetSize(modalW, modalH);
            _actionPickerHost.AddChild(modal);

            AddRoundedRect(modal, "modal_bg", 0, 0, modalW, modalH, new Color32(13, 23, 33, 255), new Color32(68, 196, 168, 255), 2f, 16f, order: 0);

            // Header
            var mTitle = CreateText("m_title", "【选择要装配的战术招式】", 22, true, new Color32(245, 250, 255, 255), Fgui.AlignType.Left);
            mTitle.SetXY(35f, 22f);
            mTitle.SetSize(420f, 30f);
            modal.AddChild(mTitle);

            var targetCard = _service.Catalog.Card(new CharacterTacticalCardReference { CardId = _pickerTargetCardId });
            string targetCardName = string.Equals(_pickerTargetCardId, "tactical.bound-action", StringComparison.Ordinal)
                ? "接近并执行动作"
                : Name(targetCard?.DisplayName, _pickerTargetCardId);
            var mHint = CreateText("m_hint", $"目标卡牌：《{targetCardName}》", 13, false, new Color32(133, 237, 214, 255), Fgui.AlignType.Left);
            mHint.SetXY(35f, 54f);
            mHint.SetSize(450f, 22f);
            modal.AddChild(mHint);

            // Unbind Button (if already bound)
            if (existingBinding != null)
            {
                var unbindModalBtn = new Fgui.GComponent { name = "unbindModalBtn", touchable = true, opaque = true, sortingOrder = 15 };
                unbindModalBtn.SetXY(modalW - 225f, 22f);
                unbindModalBtn.SetSize(100f, 34f);
                AddRoundedRect(unbindModalBtn, "umb_bg", 0, 0, 100f, 34f, new Color32(50, 24, 30, 240), new Color32(190, 50, 65, 240), 1f, 6f, order: 1);
                var umbText = CreateText("umb_txt", "解除装配", 13, false, new Color32(254, 202, 202, 255), Fgui.AlignType.Center);
                umbText.SetXY(0, 6f);
                umbText.SetSize(100f, 22f);
                unbindModalBtn.AddChild(umbText);
                BindHoverState(unbindModalBtn, "umb_bg", new Color32(50, 24, 30, 240), new Color32(190, 50, 65, 240),
                    new Color32(80, 32, 40, 255), new Color32(239, 68, 68, 255));
                unbindModalBtn.onClick.Set(ctx => {
                    ctx.StopPropagation();
                    if (_pickerTargetInstanceId > 0) _service.TryUnbind(key, _pickerTargetInstanceId, _pickerTargetSlot, out _message);
                    else _service.TryUnbind(key, _pickerTargetCardId, _pickerTargetSlot, out _message);
                    _message = $"已解除《{targetCardName}》的动作装配。";
                    _pickerOpen = false;
                    Invalidate();
                });
                modal.AddChild(unbindModalBtn);
            }

            // Close button
            var closeBtn = new Fgui.GComponent { name = "closeBtn", touchable = true, opaque = true, sortingOrder = 15 };
            closeBtn.SetXY(modalW - 110f, 22f);
            closeBtn.SetSize(75f, 34f);
            AddRoundedRect(closeBtn, "cb_bg", 0, 0, 75f, 34f, new Color32(32, 44, 58, 220), new Color32(70, 95, 120, 220), 1f, 6f, order: 1);
            var cbText = CreateText("cb_txt", "✕ 关闭", 13, false, Color.white, Fgui.AlignType.Center);
            cbText.SetXY(0, 6f);
            cbText.SetSize(75f, 22f);
            closeBtn.AddChild(cbText);
            BindHoverState(closeBtn, "cb_bg", new Color32(32, 44, 58, 220), new Color32(70, 95, 120, 220),
                new Color32(48, 65, 85, 255), new Color32(130, 165, 200, 255));
            closeBtn.onClick.Set(ctx => {
                ctx.StopPropagation();
                _pickerOpen = false;
                _message = string.Empty;
                Invalidate();
            });
            modal.AddChild(closeBtn);

            // Divider
            var div = new Fgui.GGraph { name = "m_div", touchable = false, sortingOrder = 2 };
            div.SetXY(30f, 86f);
            div.SetSize(modalW - 60f, 1f);
            div.DrawRect(modalW - 60f, 1f, 0, Color.clear, new Color32(36, 52, 70, 200));
            modal.AddChild(div);

            if (actionIds.Length == 0)
            {
                var empty = CreateText("empty_act", "当前角色暂无可装配的战术动作。", 15, false, Color.gray, Fgui.AlignType.Center);
                empty.SetXY(30f, 150f);
                empty.SetSize(modalW - 60f, 30f);
                modal.AddChild(empty);
                return;
            }

            for (int r = 0; r < rowCount; r++)
            {
                string actId = actionIds[r];
                var actDef = _service.Catalog.Action(actId);
                bool has = _service.Catalog.ContainsAction(owned?.ActionIds, actId);
                var metrics = GetActionMetrics(actDef?.ActionId ?? 0);
                string actDisplayName = Name(actDef?.DisplayName, actId);

                float rowY = 100f + r * 82f;
                float rowW = modalW - 70f;
                float rowH = 72f;

                var rowCom = new Fgui.GComponent { name = "row_" + r, touchable = true, opaque = true, sortingOrder = 15 };
                rowCom.SetXY(35f, rowY);
                rowCom.SetSize(rowW, rowH);
                modal.AddChild(rowCom);

                AddRoundedRect(rowCom, "r_bg", 0, 0, rowW, rowH, new Color32(16, 28, 38, 240), new Color32(35, 65, 85, 200), 1.2f, 8f, order: 1);
                BindHoverState(rowCom, "r_bg", new Color32(16, 28, 38, 240), new Color32(35, 65, 85, 200),
                    new Color32(22, 42, 58, 240), new Color32(68, 196, 168, 255));

                var rName = CreateText("r_name", actDisplayName, 17, true, new Color32(245, 250, 255, 255), Fgui.AlignType.Left);
                rName.SetXY(20f, 12f);
                rName.SetSize(rowW - 200f, 24f);
                rowCom.AddChild(rName);

                var rMetrics = CreateText("r_metrics", $"{metrics.distance}    {metrics.sector}", 14, false, new Color32(68, 196, 168, 255), Fgui.AlignType.Left);
                rMetrics.SetXY(20f, 40f);
                rMetrics.SetSize(rowW - 200f, 20f);
                rowCom.AddChild(rMetrics);

                // Equip Button
                float ebW = 120f;
                float ebH = 38f;
                var equipBtn = new Fgui.GComponent { name = "eq_btn", touchable = true, opaque = true, sortingOrder = 20 };
                equipBtn.SetXY(rowW - ebW - 20f, (rowH - ebH) * 0.5f);
                equipBtn.SetSize(ebW, ebH);

                if (has)
                {
                    AddRoundedRect(equipBtn, "eb_bg", 0, 0, ebW, ebH, new Color32(22, 50, 52, 240), new Color32(68, 196, 168, 255), 1.5f, 6f, order: 1);
                    var ebText = CreateText("eb_txt", "装配此动作", 13, true, new Color32(133, 237, 214, 255), Fgui.AlignType.Center);
                    ebText.SetXY(0, 9f);
                    ebText.SetSize(ebW, 20f);
                    equipBtn.AddChild(ebText);
                    BindHoverState(equipBtn, "eb_bg", new Color32(22, 50, 52, 240), new Color32(68, 196, 168, 255),
                        new Color32(34, 78, 80, 255), new Color32(145, 255, 230, 255));
                }
                else
                {
                    AddRoundedRect(equipBtn, "eb_bg", 0, 0, ebW, ebH, new Color32(14, 43, 58, 240), new Color32(2, 132, 199, 255), 1.5f, 6f, order: 1);
                    var ebText = CreateText("eb_txt", "获取并装配", 13, true, new Color32(224, 242, 254, 255), Fgui.AlignType.Center);
                    ebText.SetXY(0, 9f);
                    ebText.SetSize(ebW, 20f);
                    equipBtn.AddChild(ebText);
                    BindHoverState(equipBtn, "eb_bg", new Color32(14, 43, 58, 240), new Color32(2, 132, 199, 255),
                        new Color32(24, 65, 88, 255), new Color32(56, 189, 248, 255));
                }

                Action doEquip = () => {
                    if (!has)
                    {
                        if (!_service.TryGrantAction(key, actId, out _message)) { Invalidate(); return; }
                    }
                    bool bound = _pickerTargetInstanceId > 0
                        ? _service.TryBind(key, _pickerTargetInstanceId, _pickerTargetSlot, actId, out _message)
                        : _service.TryBind(key, _pickerTargetCardId, _pickerTargetSlot, actId, out _message);
                    if (bound)
                    {
                        _message = $"已将动作“{actDisplayName}”装配至《{targetCardName}》。";
                    }
                    _pickerOpen = false;
                    Invalidate();
                };

                equipBtn.onClick.Set(ctx => {
                    ctx.StopPropagation();
                    doEquip();
                });
                rowCom.onClick.Set(ctx => {
                    ctx.StopPropagation();
                    doEquip();
                });
                rowCom.AddChild(equipBtn);
            }
        }

        private static string ResolveCardIconKey(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.FallbackIconKey;
            string lower = cardId.ToLowerInvariant();
            if (lower.Contains("strike") || lower.Contains("attack")) return "primary-attack";
            if (lower.Contains("approach") || lower.Contains("move")) return "continuous-approach";
            if (lower.Contains("dodge")) return "reactive-dodge";
            if (lower.Contains("distance") || lower.Contains("retreat")) return "keep-distance";
            if (lower.Contains("orbit")) return "orbit-target";
            if (lower.Contains("strafe")) return "strafe-when-blocked";
            return CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.TargetNearestIconKey;
        }

        private static void DrawVectorLine(Fgui.GComponent parent, float x1, float y1, float x2, float y2, float thickness, Color32 color, int order = 10)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            var line = new Fgui.GGraph { name = "vec_line", touchable = false, sortingOrder = order };
            line.SetPivot(0f, 0.5f, true);
            line.SetXY(x1, y1);
            line.SetSize(len, thickness);
            line.DrawRect(len, thickness, 0, Color.clear, color);
            line.rotation = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            parent.AddChild(line);
        }

        private static void DrawVectorCircle(Fgui.GComponent parent, float cx, float cy, float radius, Color32 color, int order = 10)
        {
            var circle = new Fgui.GGraph { name = "vec_circle", touchable = false, sortingOrder = order };
            circle.SetXY(cx - radius, cy - radius);
            circle.SetSize(radius * 2f, radius * 2f);
            circle.DrawEllipse(radius * 2f, radius * 2f, color);
            parent.AddChild(circle);
        }

        private static void DrawTopLeftBadge(Fgui.GComponent parent, float cx, float cy, float radius, bool isSpecializedBound, string cardId)
        {
            // Circle background & border (diameter ~56px)
            Color32 circleFill = new Color32(19, 34, 45, 255); // #13222d
            Color32 circleBorder = new Color32(39, 64, 82, 255); // #274052
            AddRoundedRect(parent, "icon_circle", cx - radius, cy - radius, radius * 2f, radius * 2f, circleFill, circleBorder, 1.5f, radius, order: 2);

            // Vector Mechanism / Action Icon inside circle (#44c4a8)
            Color32 accent = new Color32(68, 196, 168, 255);
            float s = radius / 28f;

            // Rotation Arc (curved path in upper-left quadrant)
            DrawVectorLine(parent, cx - 14f * s, cy - 4f * s, cx - 9f * s, cy - 12f * s, 2f * s, accent, order: 10);
            DrawVectorLine(parent, cx - 9f * s, cy - 12f * s, cx + 2f * s, cy - 15f * s, 2f * s, accent, order: 10);
            DrawVectorLine(parent, cx + 2f * s, cy - 15f * s, cx + 11f * s, cy - 10f * s, 2f * s, accent, order: 10);

            // Arrowhead at top-right of arc
            DrawVectorLine(parent, cx + 6f * s, cy - 13f * s, cx + 11f * s, cy - 10f * s, 2f * s, accent, order: 10);
            DrawVectorLine(parent, cx + 7f * s, cy - 5f * s, cx + 11f * s, cy - 10f * s, 2f * s, accent, order: 10);

            // Dot at 8 o'clock
            DrawVectorCircle(parent, cx - 10f * s, cy + 2f * s, 2.5f * s, accent, order: 10);

            // Diagonal slash / sword mechanism
            DrawVectorLine(parent, cx + 11f * s, cy - 7f * s, cx - 3f * s, cy + 7f * s, 2.5f * s, accent, order: 10);
            // Diamond guard
            DrawVectorLine(parent, cx - 4f * s, cy + 3f * s, cx + 1f * s, cy + 8f * s, 2f * s, accent, order: 10);
            // Hilt
            DrawVectorLine(parent, cx - 3f * s, cy + 7f * s, cx - 9f * s, cy + 13f * s, 2.5f * s, accent, order: 10);
        }

        private static void AddRoundedRect(Fgui.GComponent parent, string name, float x, float y, float w, float h,
            Color32 fill, Color32 border, float stroke, float radius, int order = 0)
        {
            float[] outerRadii = new float[] { radius, radius, radius, radius };
            if (stroke > 0 && border.a > 0)
            {
                var borderGraph = new Fgui.GGraph { name = name + "_border", touchable = false, sortingOrder = order };
                borderGraph.SetXY(x, y);
                borderGraph.SetSize(w, h);
                borderGraph.DrawRoundRect(w, h, border, outerRadii);
                parent.AddChild(borderGraph);
            }
            float innerR = Mathf.Max(0f, radius - stroke);
            float[] innerRadii = new float[] { innerR, innerR, innerR, innerR };
            var fillGraph = new Fgui.GGraph { name = name + "_fill", touchable = false, sortingOrder = order + 1 };
            fillGraph.SetXY(x + stroke, y + stroke);
            fillGraph.SetSize(w - stroke * 2, h - stroke * 2);
            fillGraph.DrawRoundRect(w - stroke * 2, h - stroke * 2, fill, innerRadii);
            parent.AddChild(fillGraph);
        }

        private static void BindHoverState(Fgui.GComponent com, string rectPrefix, Color32 normalFill, Color32 normalBorder, Color32 hoverFill, Color32 hoverBorder)
        {
            com.onRollOver.Add(() => {
                var b = com.GetChild(rectPrefix + "_border")?.asGraph;
                var f = com.GetChild(rectPrefix + "_fill")?.asGraph;
                if (b != null) b.color = hoverBorder;
                if (f != null) f.color = hoverFill;
            });
            com.onRollOut.Add(() => {
                var b = com.GetChild(rectPrefix + "_border")?.asGraph;
                var f = com.GetChild(rectPrefix + "_fill")?.asGraph;
                if (b != null) b.color = normalBorder;
                if (f != null) f.color = normalFill;
            });
        }

        private static bool IsPointerOver(Fgui.GComponent com)
        {
            if (com == null || !com.visible) return false;
            var hit = Fgui.Stage.inst.HitTest(Fgui.Stage.inst.touchPosition, false);
            if (hit == null) return false;
            return hit == com.displayObject || (com.displayObject is Fgui.Container c && c.IsAncestorOf(hit));
        }

        private static Fgui.GTextField CreateText(string name, string text, int fontSize, bool bold, Color32 color, Fgui.AlignType align, bool singleLine = true, int sortingOrder = 30)
        {
            var field = new Fgui.GTextField
            {
                name = name,
                text = text ?? string.Empty,
                align = align,
                verticalAlign = Fgui.VertAlignType.Middle,
                singleLine = singleLine,
                autoSize = Fgui.AutoSizeType.None,
                touchable = false,
                sortingOrder = sortingOrder
            };
            Fgui.TextFormat format = field.textFormat;
            format.size = Mathf.RoundToInt(fontSize * 1.5f);
            format.bold = bold;
            format.color = color;
            field.textFormat = format;
            return field;
        }

        private static (string distance, string sector) GetActionMetrics(int actionId)
        {
            if (s_metricsCache.TryGetValue(actionId, out var cached)) return cached;
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MxFramework/CharacterActionTacticalMetrics"));
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, $"*-action-{actionId}.bin");
                    if (files.Length > 0)
                    {
                        var summary = CharacterActionTacticalSummaryBinary.ReadFile(files[0]);
                        if (summary != null)
                        {
                            string distStr;
                            double? minD = summary.MinDistance;
                            double? maxD = summary.MaxDistance;
                            if (maxD.HasValue)
                            {
                                if (!minD.HasValue || minD.Value <= 0.2)
                                    distStr = $"≤ {maxD.Value:F1}m";
                                else
                                    distStr = $"{minD.Value:F1}m ~ {maxD.Value:F1}m";
                            }
                            else
                            {
                                distStr = "≤ 2.0m";
                            }

                            float deg = summary.Sector.ToleranceMilliDegrees / 1000f;
                            string sectorStr = deg >= 180f ? "全周 360°" : $"前方 ±{Mathf.RoundToInt(deg)}°";
                            var result = (distStr, sectorStr);
                            s_metricsCache[actionId] = result;
                            return result;
                        }
                    }
                }
            }
            catch { /* fallback */ }

            (string d, string s) fb = actionId switch
            {
                301 => ("≤ 2.2m", "前方 ±25°"),
                324 => ("1.2m ~ 3.5m", "前方 ±20°"),
                455 => ("0.5m ~ 2.6m", "前方 ±35°"),
                950003 => ("≤ 1.8m", "前方 ±30°"),
                _ => ("≤ 2.0m", "前方 ±30°")
            };
            s_metricsCache[actionId] = fb;
            return fb;
        }

        private async void Reset()
        {
            var manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (manager == null) { _message = "当前玩法会话不可用，未清档。"; Invalidate(); return; }
            if (!await manager.ResetTacticalProgressAsync()) { _message = _service.SaveIssue; Invalidate(); }
        }

        private void SelectCharacter(int index)
        {
            _characterIndex = index;
            _selectedCard = null;
            _cardPage = 0;
            _pickerOpen = false;
            _confirmation = null;
            Invalidate();
        }

        private void Confirm(string text, Action action) { _confirmationText = text; _confirmation = action; Invalidate(); }

        private void Page(string section, int count, ref int page, Action<int> set)
        {
            int pageSize = string.Equals(section, "cards", StringComparison.Ordinal) ? CardPageSize : 7;
            int pages = Math.Max(1, (count + pageSize - 1) / pageSize);
            page = Math.Min(page, pages - 1);
            int current = page;
            Text(section + "_page", (page + 1) + " / " + pages);
            Button(section + "_previous", "上一页", page > 0, () => { set(current - 1); Invalidate(); });
            Button(section + "_next", "下一页", page + 1 < pages, () => { set(current + 1); Invalidate(); });
        }

        private bool Row(string section, int row, bool visible)
        {
            foreach (string part in new[] { "text", "icon", "use", "remove" }) Visible(section + "_" + row + "_" + part, visible);
            return visible;
        }

        private void Visible(string id, bool value)
        {
            var child = _root.GetChild("tactical_" + id);
            if (child != null) child.visible = value;
        }

        private void Text(string id, string text)
        {
            var field = _root.GetChild("tactical_" + id)?.asTextField;
            if (field != null) field.text = text ?? string.Empty;
        }

        private void Button(string id, string title, bool enabled, Action action)
        {
            var button = _root.GetChild("tactical_" + id)?.asButton;
            if (button == null) return;
            button.title = id.StartsWith("confirm_", StringComparison.Ordinal) ? title : string.Empty;
            button.tooltips = title;
            button.grayed = !enabled;
            button.touchable = enabled;
            button.onClick.Set(() => { if (enabled) action(); });
        }

        private static string Name(string title, string id)
        {
            if (!string.IsNullOrWhiteSpace(title)) return title;
            if (string.Equals(id, "tactical.bound-action", StringComparison.Ordinal)) return "《常规移动接敌出招》";
            return id + "（名称待完善）";
        }

        public void Dispose()
        {
            if (_service != null) _service.Changed -= Invalidate;
            if (_cardsHost != null) { _cardsHost.Dispose(); _cardsHost = null; }
            if (_deckHost != null) { _deckHost.Dispose(); _deckHost = null; }
            if (_actionPickerHost != null) { _actionPickerHost.Dispose(); _actionPickerHost = null; }
            if (_composerTooltip != null) { _composerTooltip.Dispose(); _composerTooltip = null; }
            _root = null;
            _service = null;
        }
    }
}

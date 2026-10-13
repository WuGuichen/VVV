using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest.TacticalCard;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiAiLabCardTrialSurface
    {
        private enum SetupSection { Entry, Positions, Deck }
        private SetupSection _setupSection;
        private int _selectedRole, _selectedCard, _selectedSlot = -1;
        private CharacterTestRuntimeAiLabCardTrialConfiguration _editingSnapshot;
        private Fgui.GList _roles, _summaryRows, _offers, _accessories;
        private Fgui.GTextInput _offerSearch, _positionX, _positionZ, _positionFacing;
        private readonly List<(string Card, CharacterTacticalCardQuality Quality)> _mainOffers = new();
        private readonly List<CharacterTacticalAccessoryDefinition> _accessoryOffers = new();
        private readonly List<Fgui.GComponent> _placementMarkers = new();
        private readonly List<Fgui.GComponent> _entryMarkers = new();
        private List<CharacterTestRuntimeAiLabCardTrialSelection> EditingDeck => _session.Draft.DeckAt(_selectedRole);
        private int EditingCharacter => _session.Draft.CharacterAt(_selectedRole);

        private void BindExperiment()
        {
            BindAccessoryArtwork();
            if (!_session.Draft.UseExplicitDecks && _session.Draft.OpponentDeck.Count == 0)
                _session.Draft.OpponentDeck.Add(_session.Draft.OpponentCard?.Clone() ?? _session.Content.DefaultActionCard(_session.Draft.OpponentCharacterId));
            _session.Draft.UseExplicitDecks = true;
            _roles = Required("participants").asList; _roles.itemRenderer = RenderParticipant; _roles.SetVirtual();
            _summaryRows = Required("entrySummary").asList; _summaryRows.itemRenderer = RenderParticipantSummary; _summaryRows.SetVirtual();
            _offers = Required("mainOffers").asList; _offers.itemRenderer = RenderMainOffer; _offers.SetVirtual();
            _accessories = Required("accessoryOffers").asList; _accessories.itemRenderer = RenderAccessoryOffer; _accessories.SetVirtual();
            _offerSearch = Required("offerSearch").asTextInput;
            _offerSearch.onChanged.Set(RefreshMainOffers);
            _positionX = Required("positionX").asTextInput; _positionZ = Required("positionZ").asTextInput;
            _positionFacing = Required("positionFacing").asTextInput;
            _positionX.onChanged.Set(() => ReadPlacement()); _positionZ.onChanged.Set(() => ReadPlacement()); _positionFacing.onChanged.Set(() => ReadPlacement());
            Button("openPositions", () => OpenSetupSection(SetupSection.Positions));
            Button("openDecks", () => OpenSetupSection(SetupSection.Deck));
            Button("saveSetup", () => FinishSetup(true)); Button("cancelSetup", () => FinishSetup(false));
            Button("retryEntry", () => _session.Start(true));
            Button("behaviorEditor", () => _session.ReturnToObservation());
            Button("editBehavior", () => _session.ReturnToObservation());
            Button("characterChoice", () => OpenPicker(_selectedRole == 0 ? TrialPickerMode.Player : TrialPickerMode.Opponent));
            Button("addParticipant", () => {
                var role = new CharacterTestRuntimeAiLabTrialParticipant { CharacterId = EditingCharacter };
                role.Cards.Add(_session.Content.DefaultActionCard(role.CharacterId));
                _session.Draft.AdditionalParticipants.Add(role); _selectedRole = _session.Draft.ParticipantCount - 1;
                RefreshConfiguration(); RefreshSetupSection();
            });
            Button("removeParticipant", () => {
                if (_selectedRole < 2) return;
                _session.Draft.AdditionalParticipants.RemoveAt(_selectedRole - 2); _selectedRole = 0;
                RefreshConfiguration(); RefreshSetupSection();
            });
            Button("pauseTrial", () => { if (_session.IsTrialPaused) _session.ResumeTrial(); else _session.PauseTrial(); RefreshPage(); });
            Button("endTrial", () => _session.EndTrial());
            _configuration.itemRenderer = RenderAssembledCard;
            RefreshMainOffers();
        }

        private void OpenSetupSection(SetupSection section)
        {
            _editingSnapshot = _session.Draft.Clone(); _setupSection = section;
            _selectedRole = Math.Min(_selectedRole, _session.Draft.ParticipantCount - 1); _selectedCard = 0; _selectedSlot = -1;
            RefreshConfiguration(); RefreshPage();
        }

        private void FinishSetup(bool save)
        {
            if (save && (!ReadNumbers() || !ReadPlacement())) { RefreshReadout(); return; }
            if (!save) _session.RestoreDraft(_editingSnapshot);
            _editingSnapshot = null; _setupSection = SetupSection.Entry;
            _selectedRole = Math.Min(_selectedRole, _session.Draft.ParticipantCount - 1);
            _binding = true;
            _momentum.text = _session.Draft.InitialMomentum.ToString(CultureInfo.InvariantCulture);
            _seed.text = _session.Draft.Seed.ToString(CultureInfo.InvariantCulture);
            _cost.text = _session.Draft.PreparationCost.ToString(CultureInfo.InvariantCulture);
            _binding = false;
            RefreshConfiguration(); RefreshPage();
        }

        private void BackToEntry()
        { if (_setupSection == SetupSection.Entry) _session.ReturnToObservation(); else FinishSetup(false); }

        private void RefreshSetupSection()
        {
            bool setup = _session.Page == CharacterTestAiLabCardTrialPage.Configuration && _pickerMode == TrialPickerMode.None;
            if (!setup || _setupSection != SetupSection.Deck) CancelAccessoryDrag();
            SetVisible(_configurationControls, false);
            SetVisible(Controls(CharacterTestAiLabCardTrialControlContract.EntryControls), false);
            SetVisible(Controls(CharacterTestAiLabCardTrialControlContract.PositionControls), false);
            SetVisible(Controls(CharacterTestAiLabCardTrialControlContract.DeckControls), false);
            Required("setupBackdrop").visible = _session.Page == CharacterTestAiLabCardTrialPage.Configuration || _pickerMode != TrialPickerMode.None;
            if (setup)
            {
                SetVisible(Controls(_setupSection == SetupSection.Entry ? CharacterTestAiLabCardTrialControlContract.EntryControls
                    : _setupSection == SetupSection.Positions ? CharacterTestAiLabCardTrialControlContract.PositionControls
                    : CharacterTestAiLabCardTrialControlContract.DeckControls), true);
                _feedback.visible = _conditions.visible = true;
                _title.text = _setupSection == SetupSection.Entry ? "实验台" : _setupSection == SetupSection.Positions ? "角色与站位" : "卡组装配";
                _subtitle.text = _setupSection == SetupSection.Entry ? "配置角色与卡组，然后开始一次实验。"
                    : _setupSection == SetupSection.Positions ? "选择角色，调整位置与朝向。" : "加入主卡，为具体牌装入配件。";
                _resume.visible = _setupSection == SetupSection.Entry && _session.OwnsSession;
                Required("retryEntry").asButton.enabled = _session.CanReset;
                _roles.SetXY(_setupSection == SetupSection.Positions ? 48 : 48, _setupSection == SetupSection.Positions ? 188 : 154);
                _roles.SetSize(_setupSection == SetupSection.Positions ? 300 : 1788, _setupSection == SetupSection.Positions ? 600 : 112);
                _roles.layout = _setupSection == SetupSection.Positions ? Fgui.ListLayoutType.SingleColumn : Fgui.ListLayoutType.SingleRow;
                _roles.numItems = _session.Draft.ParticipantCount; _roles.RefreshVirtualList();
                Required("removeParticipant").asButton.enabled = _selectedRole >= 2;
                Required("characterChoice").asButton.title = _session.Content.Character(EditingCharacter).DisplayName;
                _summaryRows.numItems = _session.Draft.ParticipantCount; _summaryRows.RefreshVirtualList();
                if (_setupSection == SetupSection.Positions) { WritePlacementFields(); RenderPlacements(Required("positionCanvas").asCom, _placementMarkers, true); }
                if (_setupSection == SetupSection.Entry) RenderPlacements(Required("entryDiagram").asCom, _entryMarkers, false);
            }
            bool observation = _session.Page == CharacterTestAiLabCardTrialPage.Observation && _pickerMode == TrialPickerMode.None;
            Required("pauseTrial").visible = Required("endTrial").visible = observation;
            Required("pauseTrial").asButton.title = _session.IsTrialPaused ? "继续实验" : "暂停实验";
        }

        private void RenderParticipant(int index, Fgui.GObject item) => RenderParticipantRow(index, item.asCom, false);
        private void RenderParticipantSummary(int index, Fgui.GObject item) => RenderParticipantRow(index, item.asCom, true);
        private void RenderParticipantRow(int index, Fgui.GComponent row, bool summary)
        {
            row.SetSize(summary ? 840 : 292, summary ? 132 : 108);
            row.GetChild("name").touchable = row.GetChild("detail").touchable = false;
            row.GetChild("name").width = row.GetChild("detail").width = summary ? 804 : 264;
            row.GetChild("select").SetSize(row.width, row.height);
            string side = index == 0 ? "我方" : "对方";
            string name = _session.Content.Character(_session.Draft.CharacterAt(index)).DisplayName;
            row.GetChild("name").text = $"{index + 1:00} · {name} / {side}";
            var position = _session.Draft.PositionAt(index); var deck = _session.Draft.DeckAt(index);
            int parts = 0; foreach (var card in deck) foreach (var kind in card.AccessorySlots) if (kind != 0) parts++;
            row.GetChild("detail").text = summary ? $"{deck.Count} 张主卡 · {parts} 个配件 · X {position.x:0.##} · Z {position.z:0.##} · 朝向 {_session.Draft.FacingAt(index):0.##}°"
                : $"{deck.Count} 张主卡";
            var button = RowButton(row, "select", () => { _selectedRole = index; _selectedCard = 0; _selectedSlot = -1; RefreshConfiguration(); RefreshSetupSection(); });
            button.touchable = !summary; row.touchable = !summary;
            row.alpha = summary || index == _selectedRole ? 1 : .72f;
        }

        private void WritePlacementFields()
        {
            _binding = true;
            var position = _session.Draft.PositionAt(_selectedRole);
            _positionX.text = position.x.ToString("0.###", CultureInfo.InvariantCulture);
            _positionZ.text = position.z.ToString("0.###", CultureInfo.InvariantCulture);
            _positionFacing.text = _session.Draft.FacingAt(_selectedRole).ToString("0.###", CultureInfo.InvariantCulture);
            _binding = false;
        }

        private bool ReadPlacement()
        {
            if (_binding || _setupSection != SetupSection.Positions) return true;
            if (!Finite(_positionX.text, out float x) || !Finite(_positionZ.text, out float z) || !Finite(_positionFacing.text, out float yaw))
            { _configurationIssue = "位置与朝向须为有效有限数值。"; RefreshReadout(); return false; }
            var position = _session.Draft.PositionAt(_selectedRole); position.x = x; position.z = z;
            _session.Draft.SetPlacement(_selectedRole, position, (yaw % 360 + 360) % 360);
            _configurationIssue = ""; RenderPlacements(Required("positionCanvas").asCom, _placementMarkers, true); return true;
        }
        private static bool Finite(string text, out float value) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !float.IsNaN(value) && !float.IsInfinity(value);

        private void RenderPlacements(Fgui.GComponent canvas, List<Fgui.GComponent> markers, bool editable)
        {
            float targetWidth = editable ? 1030 : 760, targetHeight = editable ? 620 : 424;
            canvas.SetSize(1000, 600); canvas.SetScale(targetWidth / 1000, targetHeight / 600);
            float extent = 2;
            for (int i = 0; i < _session.Draft.ParticipantCount; i++)
            { var point = _session.Draft.PositionAt(i); extent = Mathf.Max(extent, Mathf.Abs(point.x) * 1.3f, Mathf.Abs(point.z) * 1.3f); }
            while (markers.Count < _session.Draft.ParticipantCount)
            {
                var marker = Fgui.UIPackage.CreateObject("MxCharacterTestRuntimeAiLabUi", "RuntimeAiLabTrialMarker").asCom;
                canvas.AddChild(marker); markers.Add(marker);
            }
            for (int i = 0; i < markers.Count; i++)
            {
                int role = i; var marker = markers[i]; marker.visible = role < _session.Draft.ParticipantCount;
                if (!marker.visible) continue;
                var position = _session.Draft.PositionAt(role);
                marker.SetXY(500 + position.x / extent * 450 - 32, 300 - position.z / extent * 250 - 32);
                marker.GetChild("select").asButton.title = (role + 1).ToString("00");
                marker.GetChild("select").asButton.titleColor = role == 0 ? new Color32(24, 92, 116, 255) : new Color32(166, 73, 64, 255);
                marker.GetChild("direction").asTextField.color = marker.GetChild("select").asButton.titleColor;
                marker.GetChild("direction").rotation = _session.Draft.FacingAt(role);
                marker.touchable = editable; marker.draggable = editable;
                marker.alpha = !editable || role == _selectedRole ? 1 : .75f;
                marker.onClick.Set(context => { context.StopPropagation(); if (!editable) return; _selectedRole = role; RefreshConfiguration(); RefreshSetupSection(); });
                marker.onDragEnd.Set(context => {
                    var point = _session.Draft.PositionAt(role);
                    point.x = (marker.x + 32 - 500) / 450 * extent; point.z = (300 - marker.y - 32) / 250 * extent;
                    _selectedRole = role; _session.Draft.SetPlacement(role, point, _session.Draft.FacingAt(role));
                    WritePlacementFields(); RenderPlacements(canvas, markers, editable);
                });
            }
        }

        private void RefreshMainOffers()
        {
            if (_offers == null) return;
            _mainOffers.Clear(); string query = _offerSearch.text ?? "";
            foreach (var card in _session.Content.Catalog.Cards)
            {
                if (card.InteractionPreparation != null || card.PresentationSource == null || !card.PresentationSource.TryValidatePlayerExecution(out _)) continue;
                if (query.Length > 0 && card.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (var quality in new[] { CharacterTacticalCardQuality.Silver, CharacterTacticalCardQuality.Gold, CharacterTacticalCardQuality.Rainbow }) _mainOffers.Add((card.CardId, quality));
            }
            _offers.numItems = _mainOffers.Count; _offers.RefreshVirtualList();
            _accessoryOffers.Clear(); _accessoryOffers.AddRange(CharacterTestRuntimeAiLabFixedAssembly.AvailableDefinitions());
            _accessories.numItems = _accessoryOffers.Count; _accessories.RefreshVirtualList();
            Required("accessoriesTitle").text = $"配件 · {_accessoryOffers.Count} 种 · 横向滚动选择，点击或拖入槽位";
        }

        private CharacterTestRuntimeAiLabCardTrialSelection OfferSelection(int index)
        {
            var offer = _mainOffers[index]; var selection = PreviewSelection(offer.Card, EditingCharacter);
            selection.FixedQuality = offer.Quality;
            selection.FixedAssembly = _selectedRole == 0 && _session.Content.Catalog.Card(offer.Card)?.HasActionSlot == true;
            return selection;
        }
        private void RenderMainOffer(int index, Fgui.GObject item)
        {
            var row = item.asCom; var selection = OfferSelection(index);
            BindPreviewFace(row.GetChild("face").asCom, selection, EditingCharacter, _session.Draft.PreparationCost, 164);
            _skin.SetPreviewQuality(row.GetChild("face").asCom, (int)selection.FixedQuality - 1);
            Action add = () => { EditingDeck.Add(selection.Clone()); _selectedCard = EditingDeck.Count - 1; _selectedSlot = -1; RefreshConfiguration(); };
            row.GetChild("face").onClick.Set(context => { context.StopPropagation(); add(); });
            RowButton(row, "select", add).title = QualityName(selection.FixedQuality) + " · " + CharacterTestRuntimeAiLabFixedAssembly.Slots(selection.FixedQuality) + " 槽 · 加入";
        }

        private void RenderAccessoryOffer(int index, Fgui.GObject item)
        {
            var definition = _accessoryOffers[index]; var kind = definition.Execution.Kind; var row = item.asCom;
            var display = CharacterTestTacticalAccessoryDisplay.From(definition);
            ValidateControls(row, CharacterTestAiLabCardTrialControlContract.AccessoryOfferControls);
            // Keep the published 276x116 row: a compact icon/name header leaves the full width for fee and effect text.
            var icon = row.GetChild("icon"); icon.SetXY(10, 2); icon.SetSize(36, 36);
            var name = row.GetChild("name").asTextField; name.SetXY(56, 2); name.SetSize(208, 34);
            var detail = row.GetChild("detail").asTextField; detail.SetXY(10, 40); detail.SetSize(256, 74);
            var nameFormat = name.textFormat; nameFormat.size = 20; name.textFormat = nameFormat;
            var detailFormat = detail.textFormat; detailFormat.size = 17; detail.textFormat = detailFormat;
            name.verticalAlign = detail.verticalAlign = Fgui.VertAlignType.Top;
            BindAccessoryOfferArtwork(row, display);
            row.GetChild("name").touchable = row.GetChild("detail").touchable = false;
            row.GetChild("name").text = display.SlotText;
            row.GetChild("detail").text = display.FeeText + "\n" + display.Description;
            row.GetChild("name").asTextField.singleLine = true;
            row.GetChild("detail").asTextField.singleLine = false;
            row.GetChild("name").asTextField.autoSize = row.GetChild("detail").asTextField.autoSize = Fgui.AutoSizeType.None;
            var button = RowButton(row, "select", () => EquipAccessory(kind));
            button.title = ""; button.touchable = true; button.enabled = true; button.draggable = true;
            button.tooltips = display.Name + "\n" + display.FeeText + "\n" + display.Description;
            button.onDragStart.Set(context => {
                context.PreventDefault();
                StartAccessoryDrag(button, display, context.inputEvent.touchId);
            });
        }

        private void EquipAccessory(TacticalAccessoryCombatKind kind, int cardIndex = -1, int slot = -1)
        {
            if (cardIndex < 0) cardIndex = _selectedCard;
            if (cardIndex < 0 || cardIndex >= EditingDeck.Count) { _session.SetIssue("请先选中一张具体牌。"); return; }
            var card = EditingDeck[cardIndex];
            if (slot < 0) slot = _selectedSlot;
            if (!CharacterTestRuntimeAiLabFixedAssembly.TryEquip(card, kind, slot, out string issue)) { _session.SetIssue(issue); return; }
            _selectedCard = cardIndex; _selectedSlot = -1;
            _session.SetIssue(""); RefreshConfiguration();
        }
        private static string QualityName(CharacterTacticalCardQuality quality) => quality == CharacterTacticalCardQuality.Silver ? "银" : quality == CharacterTacticalCardQuality.Gold ? "金" : "彩";
        private static string EffectText(TacticalAccessoryCombatKind kind) => CharacterTestRuntimeAiLabFixedAssembly.Display(kind).Description;

        private void RenderAssembledCard(int index, Fgui.GObject item)
        {
            var row = item.asCom; var card = EditingDeck[index]; bool selected = index == _selectedCard;
            ValidateControls(row, CharacterTestAiLabCardTrialControlContract.RowControls);
            var face = row.GetChild("face").asCom;
            BindPreviewFace(face, card, EditingCharacter, _session.Draft.PreparationCost, 184, selected);
            _skin.SetPreviewQuality(face, (int)card.FixedQuality - 1);
            _skin.SetAccessoryGraphicLayout(face);
            row.GetChild("selectionFrame").visible = selected;
            row.GetChild("slotLabel").text = CardName(card.CardId) + $" · 牌 {index + 1:00}";
            face.onClick.Set(context => { context.StopPropagation(); _selectedCard = index; _selectedSlot = -1; RefreshConfiguration(); });
            RowButton(row, "chooseAction", () => OpenPicker(TrialPickerMode.Action, index)).title = _session.Content.Catalog.Action(card.TacticalActionId)?.DisplayName ?? "选择动作";
            RowButton(row, "replaceCard", () => OpenPicker(TrialPickerMode.ReplaceCard, index));
            RowButton(row, "copyCard", () => { EditingDeck.Insert(index + 1, card.Clone()); _selectedCard = index + 1; _selectedSlot = -1; RefreshConfiguration(); });
            RowButton(row, "remove", () => { EditingDeck.RemoveAt(index); _selectedCard = Math.Min(_selectedCard, EditingDeck.Count - 1); _selectedSlot = -1; RefreshConfiguration(); });
            RowButton(row, "quality", () => {
                var quality = (CharacterTacticalCardQuality)((int)card.FixedQuality % 3 + 1);
                for (int i = CharacterTestRuntimeAiLabFixedAssembly.Slots(quality); i < 3; i++) if (card.AccessorySlots[i] != 0)
                { _session.SetIssue("请先移除超出新品质槽数的配件。"); return; }
                card.FixedQuality = quality;
                _selectedSlot = -1;
                if (_selectedRole == 0 && _session.Content.Catalog.Card(card.CardId)?.HasActionSlot == true) card.FixedAssembly = true;
                RefreshConfiguration();
            }).title = QualityName(card.FixedQuality) + " ▾";
            string effects = "";
            for (int i = 0; i < 3; i++)
            {
                int slot = i; var kind = card.AccessorySlots[i]; if (kind != 0) { effects += CharacterTestRuntimeAiLabFixedAssembly.Name(kind) + "：" + EffectText(kind) + "\n"; }
                BindConfigurationAccessory(row, card, index, i, selected);
                var remove = RowButton(row, "removeSlot" + (i + 1), () => {
                    card.AccessorySlots[slot] = 0;
                    if (!card.AccessorySlots.Any(value => value != 0) && (_selectedRole > 0 || _session.Content.Catalog.Card(card.CardId)?.HasActionSlot != true)) card.FixedAssembly = false;
                    _selectedCard = index; _selectedSlot = -1; _session.SetIssue(""); RefreshConfiguration();
                });
                remove.title = "移除配件";
                remove.visible = selected && _selectedSlot == i && i < CharacterTestRuntimeAiLabFixedAssembly.Slots(card.FixedQuality) && kind != 0;
            }
            int cost = PreviewFace(card, EditingCharacter, _session.Draft.PreparationCost).Cost;
            int accessoryCost = CharacterTestRuntimeAiLabFixedAssembly.ConfirmationCost(card);
            row.GetChild("assemblyCost").text = CharacterTestTacticalAccessoryDisplay.CostBreakdown(cost, accessoryCost);
            row.GetChild("assemblyEffects").text = effects.Length == 0 ? "仅执行主卡行为。" : effects.TrimEnd();
            RefreshAssemblyTarget();
        }

        private void RefreshAssemblyTarget()
        {
            Required("assemblyTarget").text = _selectedCard < 0 || _selectedCard >= EditingDeck.Count ? "先选择左侧一张具体牌。"
                : $"装入牌 {_selectedCard + 1:00}" + (_selectedSlot >= 0 ? $" · 槽 {_selectedSlot + 1}（已有配件时替换）" : " · 下一个空槽；满槽请先选择替换位置");
        }
        private void DisposePlacementMarkers()
        {
            CancelAccessoryDrag();
            Fgui.DragDropManager.inst.dragAgent.onDragEnd.Remove(OnAccessoryDragEnd);
            foreach (var marker in _placementMarkers) marker.Dispose(); foreach (var marker in _entryMarkers) marker.Dispose();
            _placementMarkers.Clear(); _entryMarkers.Clear(); _accessoryOffers.Clear();
        }
    }
}

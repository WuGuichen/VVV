using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MxFramework.Game.CharacterTest.TacticalCard;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiAiLabCardTrialSurface
    {
        private enum TrialPickerMode { None, Player, Opponent, AddCard, ReplaceCard, Action }
        private sealed class TrialChoice
        {
            internal string Id, Name, Detail, Kind;
        }
        private TrialPickerMode _pickerMode;
        private int _pickerIndex;
        private string _pickerFilter = "全部";
        private TrialChoice _selectedChoice;
        private readonly List<TrialChoice> _allChoices = new(), _visibleChoices = new();
        private string[] _cardChoices;
        private Fgui.GList _choiceList, _cardChoiceList;
        private Fgui.GComponent _pickerCardPreview;
        private Fgui.GButton _pickerPortraitPreview;
        private Fgui.GTextInput _pickerSearch;
        private Fgui.GTextField _pickerTitle, _pickerDetailTitle, _pickerDetail;
        private Fgui.GButton _pickerApply;

        private void BindPicker()
        {
            _choiceList = Required("pickerChoices").asList;
            _cardChoiceList = Required("pickerCards").asList;
            _pickerCardPreview = Required("pickerCardPreview").asCom;
            _pickerPortraitPreview = Required("pickerPortraitPreview").asButton;
            _pickerPortraitPreview.touchable = false;
            _pickerSearch = Required("pickerSearch").asTextInput;
            _pickerSearch.promptText = "搜索名称或效果";
            _pickerTitle = Required("pickerTitle").asTextField;
            _pickerDetailTitle = Required("pickerDetailTitle").asTextField;
            _pickerDetail = Required("pickerDetail").asTextField;
            _pickerDetailTitle.autoSize = _pickerDetail.autoSize = Fgui.AutoSizeType.Shrink;
            _pickerApply = Button("pickerApply", ApplyPickerChoice);
            Button("pickerCancel", ClosePicker);
            Button("pickerAll", () => FilterPicker("全部"));
            Button("pickerActions", () => FilterPicker("行动卡"));
            Button("pickerPreparations", () => FilterPicker("交互准备卡"));
            Button("pickerTools", () => FilterPicker("测试工具卡"));
            _pickerSearch.onChanged.Set(RefreshPicker);
            _choiceList.itemRenderer = RenderChoice;
            _choiceList.SetVirtual();
            _cardChoiceList.itemRenderer = RenderCardChoice; _cardChoiceList.SetVirtual();
            _cardChoices = new[] { CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId }
                .Concat(TacticalInteractionTrialCards.Ids.Where(id => {
                    TacticalInteractionTrialCards.TryCreate(id, out var fallback);
                    var kind = ((InteractionPreparationPayload)fallback.Payload).Effect;
                    return !_session.Content.Catalog.Cards.Any(card => card.InteractionPreparation?.Effect == kind);
                }))
                .Concat(_session.Content.Catalog.Cards.Where(card => card.PresentationSource != null
                    && card.PresentationSource.TryValidatePlayerExecution(out _)).Select(card => card.CardId))
                .Distinct(StringComparer.Ordinal).ToArray();
        }

        private void OpenPicker(TrialPickerMode mode, int index = -1)
        {
            _pickerMode = mode; _pickerIndex = index; _pickerFilter = "全部"; _selectedChoice = null;
            _allChoices.Clear();
            if (mode == TrialPickerMode.Player || mode == TrialPickerMode.Opponent)
            {
                foreach (var character in _session.Content.Characters)
                    _allChoices.Add(new TrialChoice { Id = character.CharacterId.ToString(CultureInfo.InvariantCulture),
                        Name = character.DisplayName, Detail = "已导入并配置战术内容的角色", Kind = "角色" });
                int current = mode == TrialPickerMode.Player ? _session.Draft.PlayerCharacterId : _session.Draft.OpponentCharacterId;
                _selectedChoice = _allChoices.Find(item => item.Id == current.ToString(CultureInfo.InvariantCulture));
                _pickerTitle.text = mode == TrialPickerMode.Player ? "选择玩家角色" : "选择对手角色";
            }
            else if (mode == TrialPickerMode.Action)
            {
                foreach (var action in _session.Content.EligibleActions(_session.Draft.PlayerCharacterId))
                    _allChoices.Add(new TrialChoice { Id = action.Id, Name = action.DisplayName,
                        Detail = "当前玩家角色可用的正式动作；仅装配到选中的这张卡。", Kind = "动作" });
                _selectedChoice = _allChoices.Find(item => item.Id == _session.Draft.InitialHand[index].TacticalActionId);
                if (_selectedChoice == null && _allChoices.Count == 1) _selectedChoice = _allChoices[0];
                _pickerTitle.text = "选择装配动作 · " + CardName(_session.Draft.InitialHand[index].CardId);
            }
            else
            {
                foreach (string id in _cardChoices)
                    _allChoices.Add(new TrialChoice { Id = id, Name = CardName(id), Detail = CardDescription(id), Kind = CardKind(id) });
                if (mode == TrialPickerMode.ReplaceCard) _selectedChoice = _allChoices.Find(item => item.Id == _session.Draft.InitialHand[index].CardId);
                _pickerTitle.text = mode == TrialPickerMode.AddCard ? "添加初始手牌" : "替换第 " + (index + 1) + " 张手牌";
            }
            _pickerSearch.text = "";
            RefreshPage(); RefreshPicker();
        }

        private void FilterPicker(string kind)
        { _pickerFilter = kind; RefreshPicker(); }

        private void RefreshPicker()
        {
            if (_pickerMode == TrialPickerMode.None || _root == null) return;
            _visibleChoices.Clear();
            string search = _pickerSearch.text ?? "";
            foreach (var item in _allChoices)
                if ((_pickerFilter == "全部" || item.Kind == _pickerFilter)
                    && (search.Length == 0 || item.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                        || item.Detail.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)) _visibleChoices.Add(item);
            if (_selectedChoice != null && !_visibleChoices.Contains(_selectedChoice)) _selectedChoice = null;
            bool cards = _pickerMode == TrialPickerMode.AddCard || _pickerMode == TrialPickerMode.ReplaceCard;
            if (cards) _pickerTitle.text = (_pickerMode == TrialPickerMode.AddCard ? "添加初始手牌" : "替换第 " + (_pickerIndex + 1) + " 张手牌")
                + " · " + _pickerFilter + " · " + _visibleChoices.Count + " 张";
            foreach (string name in new[] { "pickerAll", "pickerActions", "pickerPreparations", "pickerTools" }) Required(name).visible = cards;
            _choiceList.visible = !cards; _cardChoiceList.visible = cards;
            _choiceList.numItems = cards ? 0 : _visibleChoices.Count;
            _cardChoiceList.numItems = cards ? _visibleChoices.Count : 0;
            _choiceList.RefreshVirtualList();
            _cardChoiceList.RefreshVirtualList();
            RefreshPickerDetail();
        }

        private void RenderChoice(int index, Fgui.GObject item)
        {
            var row = item.asCom; var choice = _visibleChoices[index];
            ValidateControls(row, CharacterTestAiLabCardTrialControlContract.ChoiceControls);
            row.GetChild("name").touchable = row.GetChild("detail").touchable = false;
            row.GetChild("name").text = (ReferenceEquals(choice, _selectedChoice) ? "✓ " : "") + choice.Name;
            row.GetChild("name").asTextField.autoSize = Fgui.AutoSizeType.Shrink;
            row.GetChild("detail").text = choice.Kind + ((_pickerMode == TrialPickerMode.AddCard || _pickerMode == TrialPickerMode.ReplaceCard)
                ? " · 动势 " + CardCost(choice.Id) : " · " + choice.Detail);
            row.GetChild("detail").asTextField.autoSize = Fgui.AutoSizeType.Shrink;
            var portrait = row.GetChild("portrait").asCom;
            bool character = _pickerMode == TrialPickerMode.Player || _pickerMode == TrialPickerMode.Opponent;
            portrait.visible = character; portrait.touchable = false;
            if (character) BindPortrait(portrait, int.Parse(choice.Id, CultureInfo.InvariantCulture));
            row.GetChild("name").x = row.GetChild("detail").x = character ? 126 : 22;
            row.GetChild("name").width = row.GetChild("detail").width = character ? 752 : 856;
            RowButton(row, "select", () => { _selectedChoice = choice; _choiceList.RefreshVirtualList(); RefreshPickerDetail(); });
        }

        private void RenderCardChoice(int index, Fgui.GObject item)
        {
            var row = item.asCom; var choice = _visibleChoices[index];
            ValidateControls(row, CharacterTestAiLabCardTrialControlContract.CardChoiceControls);
            var face = row.GetChild("face").asCom;
            BindPreviewFace(face, PreviewSelection(choice.Id), _session.Draft.PlayerCharacterId, _session.Draft.PreparationCost, 280,
                ReferenceEquals(choice, _selectedChoice));
            Action select = () => { _selectedChoice = choice; _cardChoiceList.RefreshVirtualList(); RefreshPickerDetail(); };
            face.onClick.Set(context => { context.StopPropagation(); select(); });
            RowButton(row, "select", select).title = ReferenceEquals(choice, _selectedChoice) ? "已选中" : "选择这张卡";
        }

        private void RefreshPickerDetail()
        {
            _pickerApply.enabled = _selectedChoice != null;
            _pickerApply.title = _pickerMode == TrialPickerMode.Action ? "绑定动作" : _pickerMode == TrialPickerMode.AddCard ? "加入手牌" : "确认选择";
            _pickerDetailTitle.text = _selectedChoice?.Name ?? (_visibleChoices.Count == 0 ? "没有可用候选" : "选择左侧一项");
            _pickerDetail.text = _selectedChoice?.Detail ?? (_visibleChoices.Count == 0 ? "检查角色动作目录，或调整搜索条件。" : "确认前不会修改当前配置。" );
            bool cards = _pickerMode == TrialPickerMode.AddCard || _pickerMode == TrialPickerMode.ReplaceCard;
            bool character = _pickerMode == TrialPickerMode.Player || _pickerMode == TrialPickerMode.Opponent;
            _pickerCardPreview.visible = cards && _selectedChoice != null;
            _pickerPortraitPreview.visible = character && _selectedChoice != null;
            _pickerDetail.SetXY(cards || character ? 1450 : 1020, 416);
            _pickerDetail.SetSize(cards || character ? 394 : 824, 480);
            if (cards && _selectedChoice != null)
            {
                BindPreviewFace(_pickerCardPreview, PreviewSelection(_selectedChoice.Id), _session.Draft.PlayerCharacterId,
                    _session.Draft.PreparationCost, 340);
                var selection = PreviewSelection(_selectedChoice.Id);
                var action = _session.Content.Catalog.Action(selection.TacticalActionId);
                _pickerDetail.text = _session.Content.Catalog.Card(selection.CardId)?.HasActionSlot != true ? "这张卡无需绑定动作。"
                    : action != null ? "装配动作：" + action.DisplayName + "\n\n确认选卡后自动装配；可在配置页更换动作。"
                    : "当前玩家角色没有可装配动作，请更换角色。";
            }
            if (character && _selectedChoice != null) BindPortrait(_pickerPortraitPreview, int.Parse(_selectedChoice.Id, CultureInfo.InvariantCulture));
        }

        private void ApplyPickerChoice()
        {
            if (_selectedChoice == null) return;
            var draft = _session.Draft;
            if (_pickerMode == TrialPickerMode.Player || _pickerMode == TrialPickerMode.Opponent)
            {
                int character = int.Parse(_selectedChoice.Id, CultureInfo.InvariantCulture);
                if (_pickerMode == TrialPickerMode.Player)
                {
                    draft.PlayerCharacterId = character;
                    foreach (var item in draft.InitialHand)
                        item.TacticalActionId = _session.Content.ResolveActionBinding(item.CardId, character, item.TacticalActionId);
                }
                else draft.OpponentCharacterId = character;
            }
            else if (_pickerMode == TrialPickerMode.Action) draft.InitialHand[_pickerIndex].TacticalActionId = _selectedChoice.Id;
            else
            {
                var item = PreviewSelection(_selectedChoice.Id);
                if (_pickerMode == TrialPickerMode.AddCard) draft.InitialHand.Add(item);
                else draft.InitialHand[_pickerIndex] = item;
            }
            _session.SetIssue("");
            ClosePicker(); RefreshConfiguration();
        }

        private void ClosePicker()
        { _pickerMode = TrialPickerMode.None; _selectedChoice = null; RefreshPage(); }
    }
}

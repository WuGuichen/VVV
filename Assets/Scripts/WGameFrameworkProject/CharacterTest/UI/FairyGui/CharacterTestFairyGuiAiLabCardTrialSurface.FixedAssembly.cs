using System;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiAiLabCardTrialSurface
    {
        private int _assemblySlot;
        private void BuildAssemblyChoices(TrialPickerMode mode, int index)
        {
            var item = EditingDeck[index];
            if (mode == TrialPickerMode.Assembly)
            {
                _pickerTitle.text = "动作与固定配件 · " + CardName(item.CardId);
                _allChoices.Add(new TrialChoice { Id = "action", Name = "更换装配动作", Kind = "动作", Detail = "保留配件，选择这张主卡的动作。" });
                _allChoices.Add(new TrialChoice { Id = "bare", Name = "使用普通主卡", Kind = "装配", Detail = "取消这张测试牌的配件装配。" });
                foreach (var quality in new[] { CharacterTacticalCardQuality.Silver, CharacterTacticalCardQuality.Gold, CharacterTacticalCardQuality.Rainbow })
                    _allChoices.Add(new TrialChoice { Id = "quality." + (int)quality,
                        Name = "测试主卡品质：" + (quality == CharacterTacticalCardQuality.Silver ? "银" : quality == CharacterTacticalCardQuality.Gold ? "金" : "彩")
                            + " · " + CharacterTestRuntimeAiLabFixedAssembly.Slots(quality) + "槽", Kind = "装配",
                        Detail = "只改变本次试验的槽数；不改变正式卡品质或存档。配件确认费按当前配置显示。" });
                if (item.FixedAssembly) for (int slot = 0; slot < CharacterTestRuntimeAiLabFixedAssembly.Slots(item.FixedQuality); slot++)
                    _allChoices.Add(new TrialChoice { Id = "slot." + slot, Name = "配件槽 " + (slot + 1) + "：" + CharacterTestRuntimeAiLabFixedAssembly.Name(item.AccessorySlots[slot]),
                        Kind = "配件", Detail = "选择或清空此槽。配件随主卡统一打出；效果按每张配件的条件或完整回合规则存续。" });
            }
            else
            {
                _pickerTitle.text = "配件槽 " + (_assemblySlot + 1) + " · " + CardName(item.CardId);
                _allChoices.Add(new TrialChoice { Id = "0", Name = "空槽", Kind = "配件", Detail = "允许空槽；不会增加确认费用。" });
                foreach (var definition in CharacterTestRuntimeAiLabFixedAssembly.AvailableDefinitions()) {
                    var display = CharacterTestTacticalAccessoryDisplay.From(definition);
                    _allChoices.Add(new TrialChoice { Id = ((int)display.Kind).ToString(), Name = display.Name, Kind = "配件",
                        Detail = display.FeeText + "；" + display.RuleText });
                }
            }
        }
        private bool ApplyAssemblyChoice()
        {
            var item = EditingDeck[_pickerIndex]; string id = _selectedChoice.Id;
            if (_pickerMode == TrialPickerMode.Accessory)
            {
                var kind = (TacticalAccessoryCombatKind)int.Parse(id);
                for (int i = 0; kind != 0 && i < item.AccessorySlots.Length; i++) if (i != _assemblySlot && item.AccessorySlots[i] == kind)
                { _pickerDetail.text = "同名重复装配合同尚未确定；当前测试不支持重复。"; return true; }
                item.AccessorySlots[_assemblySlot] = kind;
            }
            else if (id == "action") { OpenPicker(TrialPickerMode.Action, _pickerIndex); return true; }
            else if (id.StartsWith("slot.", StringComparison.Ordinal))
            { _assemblySlot = int.Parse(id.Substring(5)); OpenPicker(TrialPickerMode.Accessory, _pickerIndex); return true; }
            else if (id == "bare") { item.FixedAssembly = false; Array.Clear(item.AccessorySlots, 0, item.AccessorySlots.Length); }
            else
            {
                var quality = (CharacterTacticalCardQuality)int.Parse(id.Substring(8));
                for (int slot = CharacterTestRuntimeAiLabFixedAssembly.Slots(quality); slot < item.AccessorySlots.Length; slot++)
                    if (item.AccessorySlots[slot] != 0) { _pickerDetail.text = "请先清空超出新槽数的配件。"; return true; }
                item.FixedAssembly = true; item.FixedQuality = quality;
                _session.SetIssue(""); RefreshConfiguration(); OpenPicker(TrialPickerMode.Assembly, _pickerIndex); return true;
            }
            _session.SetIssue(""); ClosePicker(); RefreshConfiguration(); return true;
        }
    }
}

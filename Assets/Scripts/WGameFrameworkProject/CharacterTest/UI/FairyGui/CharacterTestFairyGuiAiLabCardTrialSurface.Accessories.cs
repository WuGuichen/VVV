using System;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiAiLabCardTrialSurface
    {
        private readonly Fgui.NTexture[] _accessoryTextures = new Fgui.NTexture[4];
        private Fgui.GComponent _accessoryIconLibrary;
        private bool _accessoryDragActive;

        private void BindAccessoryArtwork()
        {
            Fgui.DragDropManager.inst.dragAgent.onDragEnd.Remove(OnAccessoryDragEnd);
            Fgui.DragDropManager.inst.dragAgent.onDragEnd.Add(OnAccessoryDragEnd);
            _accessoryIconLibrary = Required("accessoryIconLibrary").asCom;
            ValidateControls(_accessoryIconLibrary, CharacterTestAiLabCardTrialControlContract.AccessoryArtworkControls);
            _accessoryIconLibrary.visible = false;
            for (int i = 1; i <= 3; i++)
            {
                var image = _accessoryIconLibrary.GetChild(CharacterTestFairyGuiTacticalAccessoryPresentation.ArtworkControl((TacticalAccessoryCombatKind)i)).asImage;
                _accessoryTextures[i] = image.texture
                    ?? throw new InvalidOperationException("Published tactical accessory artwork is missing.");
            }
        }

        private Fgui.NTexture AccessoryTexture(TacticalAccessoryCombatKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= _accessoryTextures.Length) return null;
            return _accessoryTextures[index];
        }

        private void BindAccessoryOfferArtwork(Fgui.GComponent row, CharacterTestTacticalAccessoryDisplay display)
        {
            var icon = row.GetChild("icon").asImage;
            icon.texture = AccessoryTexture(display.Kind); icon.visible = icon.texture != null; icon.touchable = false;
            var fallback = row.GetChild("accessoryFallback")?.asCom;
            if (fallback == null && icon.texture == null)
            {
                fallback = Fgui.UIPackage.CreateObject("MxCharacterTestRuntimeAiLabUi", "RuntimeAiLabTrialHandAccessorySlot").asCom;
                fallback.name = "accessoryFallback"; row.AddChild(fallback);
            }
            if (fallback == null) return;
            CharacterTestFairyGuiTacticalAccessoryPresentation.Bind(fallback, icon.texture, CharacterTacticalCardQuality.Silver,
                false, false, display.SlotText);
            fallback.SetXY(icon.x, icon.y); fallback.SetScale(icon.width / fallback.width, icon.height / fallback.height);
            fallback.visible = icon.texture == null;
        }

        private void StartAccessoryDrag(Fgui.GButton button, CharacterTestTacticalAccessoryDisplay display, int touchId)
        {
            var manager = Fgui.DragDropManager.inst;
            if (manager.dragging) return;
            string control = CharacterTestFairyGuiTacticalAccessoryPresentation.ArtworkControl(display.Kind);
            string url = control == null ? null : _accessoryIconLibrary.GetChild(control)?.resourceURL;
            bool fallback = string.IsNullOrEmpty(url);
            if (fallback) url = Fgui.UIPackage.GetItemURL("MxCharacterTestRuntimeAiLabUi", "RuntimeAiLabTrialHandAccessorySlot");
            if (string.IsNullOrEmpty(url)) throw new InvalidOperationException("Published accessory drag marker is missing.");
            var agent = manager.dragAgent;
            agent.url = null; agent.texture = null; agent.autoSize = false; agent.fill = Fgui.FillType.Scale;
            agent.SetSize(48, 48);
            manager.StartDrag(button, url, display.Kind, touchId);
            if (fallback) CharacterTestFairyGuiTacticalAccessoryPresentation.Bind(agent.component, null,
                CharacterTacticalCardQuality.Silver, false, false, display.SlotText);
            _accessoryDragActive = true;
        }

        private void OnAccessoryDragEnd(Fgui.EventContext context) => ReleaseAccessoryDragPreview();
        private void ReleaseAccessoryDragPreview()
        {
            if (!_accessoryDragActive) return;
            var agent = Fgui.DragDropManager.inst.dragAgent;
            agent.url = null; agent.texture = null; _accessoryDragActive = false;
        }
        private void CancelAccessoryDrag()
        {
            if (_accessoryDragActive) Fgui.DragDropManager.inst.Cancel();
            ReleaseAccessoryDragPreview();
        }

        private void BindConfigurationAccessory(Fgui.GComponent row, CharacterTestRuntimeAiLabCardTrialSelection card,
            int cardIndex, int slotIndex, bool selected)
        {
            var slot = row.GetChild("slot" + (slotIndex + 1)).asCom;
            var kind = card.AccessorySlots[slotIndex];
            var display = CharacterTestRuntimeAiLabFixedAssembly.Display(kind);
            slot.visible = slotIndex < CharacterTestRuntimeAiLabFixedAssembly.Slots(card.FixedQuality);
            CharacterTestFairyGuiTacticalAccessoryPresentation.Bind(slot, AccessoryTexture(kind), card.FixedQuality,
                selected && _selectedSlot == slotIndex, true, display.SlotText);
            slot.onClick.Set(context => { context.StopPropagation(); _selectedCard = cardIndex; _selectedSlot = slotIndex; RefreshConfiguration(); });
            slot.onDrop.Set(context => { if (context.data is TacticalAccessoryCombatKind dropped) { context.StopPropagation(); EquipAccessory(dropped, cardIndex, slotIndex); } });
            _tooltips.Bind(slot, kind == 0 ? "空配件槽 · 从右侧选择配件或拖到此处。"
                : display.Name + "\n" + display.Description + "\n" + display.FeeText + " · 点击选择此槽，可替换或移除。",
                new MxFairyGuiTooltipOptions(minimumTextWidth: 240, maximumTextWidth: 420, allowPin: false));
        }

        private void BindHandAccessories(Fgui.GComponent row, TacticalFixedAssemblyManifest assembly)
        {
            for (int i = 0; i < 3; i++)
            {
                var slot = row.GetChild("slot" + (i + 1)).asCom;
                slot.visible = assembly != null && i < assembly.SlotCount;
                var display = CharacterTestTacticalAccessoryDisplay.Unresolved(0);
                if (assembly != null) foreach (var part in assembly.Accessories) if (part.Slot == i) { display = CharacterTestTacticalAccessoryDisplay.From(part); break; }
                CharacterTestFairyGuiTacticalAccessoryPresentation.Bind(slot, AccessoryTexture(display.Kind),
                    assembly?.Quality ?? CharacterTacticalCardQuality.Silver, false, false, display.SlotText);
                _tooltips.Bind(slot, display.Name + "\n" + display.FeeText + "\n" + display.Description,
                    new MxFairyGuiTooltipOptions(minimumTextWidth: 240, maximumTextWidth: 420, allowPin: false));
            }
        }
    }
}

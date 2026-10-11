namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalPreparationView
    {
        private readonly CharacterTestFairyGuiTacticalCardSkin _cardSkin = new();
        private void ApplyCardTheme(global::FairyGUI.GComponent face, CharacterTestCardUiPreviewData data) => _cardSkin.Bind(face,data);
        private int ResolvePreviewQuality(string cardId) => _cardSkin.ResolveQuality(cardId);
        private void TickCardMaterials() => _cardSkin.Tick();
        private void DisposeCardMaterials() => _cardSkin.Dispose();
    }
}

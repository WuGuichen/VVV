namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Read-only ground picking shared by manual input and transient validation previews.</summary>
    internal interface ITacticalPointSelectionPort
    {
        bool TryGetPointOrigin(out TacticalWorldPoint origin);
        TacticalInputStatus TryPickGround(TacticalWorldPoint rayOrigin, TacticalWorldPoint rayDirection, out TacticalWorldPoint point);
    }
}

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestComponentWorld
    {
        private readonly CharacterTestComponentStore<CharacterTestGroundPointTractionState> _groundPointTraction =
            new CharacterTestComponentStore<CharacterTestGroundPointTractionState>();
        internal CharacterTestGroundPointTractionState GetGroundPointTraction(CharacterTestCharacterEntityId id)
        { _groundPointTraction.TryGet(id, out var state); return state; }
        internal void SetGroundPointTraction(CharacterTestCharacterEntityId id, in CharacterTestGroundPointTractionState state) =>
            _groundPointTraction.Set(id, state);
    }
}
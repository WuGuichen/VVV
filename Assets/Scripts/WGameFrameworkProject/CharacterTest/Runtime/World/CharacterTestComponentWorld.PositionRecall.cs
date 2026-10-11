namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestComponentWorld
    {
        private readonly CharacterTestComponentStore<CharacterTestPositionRecallState> _positionRecall = new CharacterTestComponentStore<CharacterTestPositionRecallState>();
        internal CharacterTestPositionRecallState GetPositionRecall(CharacterTestCharacterEntityId id)
        { _positionRecall.TryGet(id, out var state); return state; }
        internal void SetPositionRecall(CharacterTestCharacterEntityId id, in CharacterTestPositionRecallState state) => _positionRecall.Set(id, state);
    }
}

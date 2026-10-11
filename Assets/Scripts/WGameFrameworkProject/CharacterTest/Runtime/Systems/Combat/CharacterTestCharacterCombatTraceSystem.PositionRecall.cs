namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        internal void ResetPositionRecallWitnesses(int entityId)
        {
            _bodyHitVolumeCacheRemovals.Clear();
            foreach (var pair in _bodyHitVolumePreviousCenters) if (pair.Key.EntityId == entityId) _bodyHitVolumeCacheRemovals.Add(pair.Key);
            for (int i = 0; i < _bodyHitVolumeCacheRemovals.Count; i++) _bodyHitVolumePreviousCenters.Remove(_bodyHitVolumeCacheRemovals[i]);
            _bodyHitVolumeCacheRemovals.Clear();
        }
    }
}

using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterRuntimeWorld
    {
        internal System.Func<int, bool> ThirdDirectProjectileRequest { get; set; }
    }
    public sealed partial class CharacterTestCharacterEntity
    {
        private CharacterTestCharacterRuntimeWorld _thirdWorld;
        internal void BindThirdWorld(CharacterTestCharacterRuntimeWorld world) => _thirdWorld = world;
        internal TacticalThirdBatchAttack CurrentThirdAttack => _thirdWorld?.DoubleEdgeState.CurrentThirdAttack(GameplayEntityId) ?? default;
        internal bool SuppressesThirdMotion(in TacticalThirdBatchAttack source, TacticalThirdBatchMotionChannel channel)
            => _thirdWorld?.DoubleEdgeState.SuppressesThirdMotion(GameplayEntityId, source, channel) == true;
        internal bool SuppressesThirdActiveMotion => SuppressesThirdMotion(CurrentThirdAttack, TacticalThirdBatchMotionChannel.ActiveTranslation);
        internal void FreezeThirdTrace(int window, long instance, long frame, ulong canonicalNode = 0)
        {
            var state = _thirdWorld?.DoubleEdgeState;
            if (state == null || state.Stage == 0) return;
            if (!state.BindThirdSource(2, window, 0, state.ThirdAttack(GameplayEntityId, instance), frame, canonicalNode))
                throw new System.InvalidOperationException("third-batch:TraceBirthRejected");
        }
    }
}

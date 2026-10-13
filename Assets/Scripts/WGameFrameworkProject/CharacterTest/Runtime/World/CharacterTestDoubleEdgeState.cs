using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterRuntimeWorld
    {
        // A stage projection, deliberately independent of the world's surviving actor enumeration.
        internal TacticalDoubleEdgeBattleState DoubleEdgeState { get; set; } = new TacticalDoubleEdgeBattleState();
    }

    public sealed partial class CharacterTestCharacterEntity
    {
        private CharacterActionInstance _doubleEdgeAction;
        private long _doubleEdgeActionInstance;
        private TacticalDirectAttackStamp _doubleEdgeSource;
        private TacticalDoubleEdgeBattleState _doubleEdgeAuthority;
        internal void BindDoubleEdgeAction(CharacterActionInstance action, in TacticalDirectAttackStamp source, TacticalDoubleEdgeBattleState authority = null)
        { _doubleEdgeAction = action; _doubleEdgeActionInstance = action?.InstanceId ?? 0; _doubleEdgeSource = source; _doubleEdgeAuthority = authority; }
        internal TacticalDirectAttackStamp FreezeDoubleEdgeTrace(int window, long actionInstance, long frame)
        {
            var source = CaptureDoubleEdgeSource(actionInstance);
            bool bound = source.IsValid && _doubleEdgeAuthority?.BindTrace(window, source, frame) == true;
            FreezeThirdTrace(window, actionInstance, frame, bound ? TacticalDoubleEdgeBattleState.ProducerNode(2, window, 0, source.ActionRevision) : 0);
            return bound ? source : default;
        }
        /// <remarks>NoAlloc. The actual acknowledged object may be pooled; its frozen instance must still match.</remarks>
        internal TacticalDirectAttackStamp CaptureDoubleEdgeSource(long actionInstance)
            => _doubleEdgeSource.IsValid && _doubleEdgeSource.Commit.Actor == GameplayEntityId
                && _doubleEdgeAction != null && _doubleEdgeAction.InstanceId == _doubleEdgeActionInstance
                && _doubleEdgeActionInstance == actionInstance ? _doubleEdgeSource : default;
    }
}

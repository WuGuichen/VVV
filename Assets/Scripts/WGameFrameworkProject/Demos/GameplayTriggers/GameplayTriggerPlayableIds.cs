using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;

namespace MxFramework.Demo.GameplayTriggers
{
    internal static class GameplayTriggerPlayableIds
    {
        internal const int CommandEnvelopeType = 7360001;
        internal const int CommandEnvelopeSchema = 1;
        internal const int CommandSource = 7360010;

        internal const int MoveCommand = 7360101;
        internal const int CaptureCommand = 7360102;
        internal const int RestoreCommand = 7360103;
        internal const int ResetCommand = 7360104;

        internal const int CheckpointSourceDefinition = 7361001;
        internal const int DoorSourceDefinition = 7361002;
        internal const int TrapSourceDefinition = 7361003;
        internal const int CheckpointCondition = 7361101;
        internal const int DoorCondition = 7361102;
        internal const int TrapCondition = 7361103;
        internal const int CheckpointDefinition = 7361201;
        internal const int DoorDefinition = 7361202;
        internal const int TrapDefinition = 7361203;
        internal const int CheckpointRoute = 7361301;
        internal const int DoorRoute = 7361302;
        internal const int TrapRoute = 7361303;
        internal const int CheckpointBinding = 7361401;
        internal const int DoorBinding = 7361402;
        internal const int TrapBinding = 7361403;
        internal const int CheckpointSourceBinding = 7361501;
        internal const int DoorSourceBinding = 7361502;
        internal const int TrapSourceBinding = 7361503;
        internal const int CombatProvider = 7361601;
        internal const int SetEnabledActionKind = 7362001;
        internal const int ExplicitDoorTargetSlot = 1;

        internal const int StoryGraph = 7363001;
        internal const int StoryWaitingBeat = 7363101;
        internal const int StoryOpenBeat = 7363102;
        internal const int StoryOpenStep = 7363201;
        internal const int StoryOpenTrigger = 7363301;
        internal const int StoryFactNamespace = 7363001;
        internal const int StoryDoorOpenFact = 7363401;

        internal const int MoveSpeedAttribute = 7364001;
        internal const int SlowedStatus = 7364101;
        internal const int TrapCounterBuff = 1736401;
        internal const int TrapSlowBuff = 1736402;
        internal const int TrapBuffSource = 7364301;

        internal const int PlayerCombatEntity = 7365001;
        internal const int PlayerBody = 7365101;
        internal const int PlayerCollider = 7365201;
        internal const int DoorCombatEntity = 7365002;
        internal const int DoorBody = 7365102;
        internal const int DoorCollider = 7365202;
        internal const int BoundsCombatEntityStart = 7365003;
        internal const int BoundsBodyStart = 7365103;
        internal const int BoundsColliderStart = 7365203;
        internal const int CheckpointQuery = 7365301;
        internal const int DoorQuery = 7365302;
        internal const int TrapQuery = 7365303;

        internal static readonly GameplayTriggerActionKindId SetEnabledAction =
            new GameplayTriggerActionKindId(SetEnabledActionKind);
        internal static readonly GameplayTriggerSourceProviderId CombatSourceProvider =
            new GameplayTriggerSourceProviderId(CombatProvider);
    }
}

using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestChainLightningVfxEvent
    {
        public CharacterTestChainLightningVfxEvent(
            RuntimeFrame frame,
            int sourceEntityId,
            int targetEntityId,
            int segmentIndex,
            int actionInstanceId,
            int localFrame,
            FixVector3 origin,
            FixVector3 direction,
            FixVector3 endpoint,
            int lengthMillimeters,
            bool hit)
        {
            Frame = frame;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            SegmentIndex = segmentIndex < 0 ? 0 : segmentIndex;
            ActionInstanceId = actionInstanceId;
            LocalFrame = localFrame;
            Origin = origin;
            Direction = direction;
            Endpoint = endpoint;
            LengthMillimeters = lengthMillimeters < 0 ? 0 : lengthMillimeters;
            Hit = hit;
        }

        public RuntimeFrame Frame { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public int SegmentIndex { get; }
        public int ActionInstanceId { get; }
        public int LocalFrame { get; }
        public FixVector3 Origin { get; }
        public FixVector3 Direction { get; }
        public FixVector3 Endpoint { get; }
        public int LengthMillimeters { get; }
        public bool Hit { get; }
    }
}

using System;
using MxFramework.Combat.Physics;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterRuntimeModule
    {
        // Optional presentation observation only: never feeds back into authority.
        internal event Action<CharacterTestCharacterRuntimeWorld,CharacterTestCharacterMotionSystem,RuntimeFrame> PresentationMotionObserved;
        internal event Action<CombatPhysicalContactEvent> PhysicalContactObserved
        { add => _physicalAudioSystem.ContactObserved+=value; remove => _physicalAudioSystem.ContactObserved-=value; }
        private void PublishPresentationMotion(RuntimeFrame frame) => PresentationMotionObserved?.Invoke(_world,_motionSystem,frame);
    }
}

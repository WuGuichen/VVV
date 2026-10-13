using System;
using MxFramework.Combat.Physics;
using MxFramework.CharacterApplication;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterRuntimeModule
    {
        // Optional presentation observation only: never feeds back into authority.
        internal event Action<CharacterTestCharacterRuntimeWorld,CharacterTestCharacterMotionSystem,RuntimeFrame> PresentationMotionObserved;
        internal event Action<CharacterTestCharacterRuntimeWorld,CharacterTestCharacterMotionSystem,RuntimeFrame> PhysicalContactMotionObserved;
        internal event Action<CombatPhysicalContactEvent> PhysicalContactObserved
        { add => _physicalAudioSystem.ContactObserved+=value; remove => _physicalAudioSystem.ContactObserved-=value; }
        /// <remarks>NoAlloc. Shared presentation identity query; does not own or generate physical contacts.</remarks>
        internal bool TryGetPhysicalContactPartKind(int entityId, int contactPointId, int bodyPartId,
            out CharacterBodyPartKind kind) => _physicalAudioSystem.TryGetPhysicalContactPartKind(
                entityId, contactPointId, bodyPartId, out kind);
        /// <remarks>NoAlloc. Normalizes the existing physical namespace once for all presentation consumers.</remarks>
        internal bool TryResolvePhysicalContactPresentation(in CombatPhysicalContactEvent contact,
            out CharacterTestCharacterEntity subject, out CharacterBodyPartKind kind)
        {
            int id = CharacterTestCombatEntityLookup.ResolveCharacterEntityId(contact.SourceEntityId);
            subject = id > 0 ? _world.FindEntity(id) : null;
            kind = CharacterBodyPartKind.Custom;
            return subject != null && TryGetPhysicalContactPartKind(id, contact.ContactPointId, contact.BodyPartId, out kind);
        }
        private void PublishPresentationMotion(RuntimeFrame frame) => PresentationMotionObserved?.Invoke(_world,_motionSystem,frame);
        private void PublishPhysicalContactMotion(RuntimeFrame frame) => PhysicalContactMotionObserved?.Invoke(_world,_motionSystem,frame);
    }
}

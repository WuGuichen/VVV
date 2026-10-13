using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestPhysicalAudioSystem
    {
        /// <summary>Shared, read-only presentation semantics for the existing contact publisher.</summary>
        /// <remarks>NoAlloc. Cache lifetime is the existing spawn/refresh/remove registration lifetime.</remarks>
        internal bool TryGetPhysicalContactPartKind(int entityId, int contactPointId, int bodyPartId,
            out CharacterBodyPartKind kind)
        {
            kind = CharacterBodyPartKind.Custom;
            if (!_contactRegistrations.TryGetValue(entityId, out var registration)) return false;
            for (int i = 0; i < registration.Bindings.Length; i++)
            {
                var config = registration.Bindings[i].Config;
                if (config == null || config.ContactPointId != contactPointId || config.BodyPartId != bodyPartId) continue;
                kind = registration.PartKinds[i]; return true;
            }
            return false;
        }
        /// <remarks>NoAlloc. Used at registration; enum semantic is distinct from authored numeric part identity.</remarks>
        internal static CharacterBodyPartKind ResolveRegisteredContactPartKind(
            CharacterBodyPartContactPointConfig contact, CharacterBodyPartConfig[] parts,
            CharacterImportedGeometryBinding geometry = null)
        {
            if (contact == null || parts == null) return CharacterBodyPartKind.Custom;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part == null || part.PartId != contact.BodyPartId
                    || !string.Equals(part.PartSetId, contact.PartSetId, System.StringComparison.Ordinal)) continue;
                if (part.PartKind != CharacterBodyPartKind.Custom) return part.PartKind;
                // Legacy imported Bone rows use the existing abstract-foot socket role and parent-part relation.
                var sockets = geometry?.Sockets;
                if (sockets != null)
                    for (int socketIndex = 0; socketIndex < sockets.Length; socketIndex++)
                    {
                        var socket = sockets[socketIndex];
                        if (socket == null || !int.TryParse(socket.ParentPartId, out int parentPart)
                            || parentPart != part.PartId) continue;
                        if (socket.AbstractBoneId == CharacterAbstractBoneIds.LeftFoot
                            || socket.AbstractBoneId == CharacterAbstractBoneIds.RightFoot)
                            return CharacterBodyPartKind.Foot;
                    }
                return CharacterBodyPartKind.Custom;
            }
            return CharacterBodyPartKind.Custom;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestPhysicalAudioSystem
    {
        private void BeginBodyPartContactDebug(RuntimeFrame frame)
        {
            _bodyPartContactDebugFrame = frame;
            _bodyPartContactDebugCount = 0;
            _bodyPartContactDebugEntryIndices.Clear();
        }

        private void AppendBodyPartContactDebugEntry(
            CharacterTestCharacterEntity entity,
            in CombatBodyPartContactPointSample sample,
            CharacterTestBodyPartContactMotionResolution motion)
        {
            if (_bodyPartContactDebugCount >= _bodyPartContactDebugEntries.Length)
                return;

            CombatBodyPartContactPointDefinition definition = sample.Definition;
            FixVector3 center = TransformContactPointLocalOffset(
                sample.BodyPosition,
                sample.Forward,
                sample.Right,
                sample.Up,
                definition.LocalOffset);
            int index = _bodyPartContactDebugCount;
            _bodyPartContactDebugEntries[index] =
                CharacterTestBodyPartContactDebugEntry.CreateProbe(
                    _bodyPartContactDebugFrame,
                    entity == null ? 0 : entity.Id.Value,
                    sample.SourceEntityId.Value,
                    definition.ContactPointId,
                    definition.BodyPartId,
                    definition.SourceSlotId,
                    definition.SourceOrder,
                    definition.ActivationMode,
                    motion.MotionMode,
                    motion.MotionSocketId,
                    motion.State,
                    motion.ClipKey,
                    motion.LocalFrame,
                    definition.LayerMask,
                    center,
                    definition.Radius,
                    IsBodyPartContactSampleActive(sample),
                    sample.RelativeSpeedMillimetersPerSecond,
                    sample.NormalSpeedMillimetersPerSecond,
                    sample.TangentialSpeedMillimetersPerSecond,
                    sample.Pressure,
                    sample.Intensity);
            _bodyPartContactDebugEntryIndices[new BodyPartContactDebugKey(
                sample.SourceEntityId.Value,
                definition.ContactPointId,
                definition.BodyPartId)] = index;
            _bodyPartContactDebugCount++;
        }

        private void UpdateBodyPartContactDebugEntry(
            in CombatPhysicalContactEvent contactEvent,
            CharacterTestCharacterEntity entity)
        {
            int index = FindBodyPartContactDebugEntryIndex(contactEvent);
            if (index < 0)
                return;

            bool contactAllowed = contactEvent.Phase != CombatPhysicalContactPhase.Ended
                && IsBodyPartContactAllowed(entity, CreateBodyPartContactHit(contactEvent));
            _bodyPartContactDebugEntries[index] =
                _bodyPartContactDebugEntries[index].WithContact(contactEvent, contactAllowed);
        }

        private void MarkBodyPartContactDebugAudioQueued(in CombatPhysicalContactEvent contactEvent)
        {
            int index = FindBodyPartContactDebugEntryIndex(contactEvent);
            if (index < 0)
                return;

            _bodyPartContactDebugEntries[index] =
                _bodyPartContactDebugEntries[index].WithAudioQueued();
        }

        private void UpdateBodyPartContactDebugDynamics(
            in CombatPhysicalContactEvent contactEvent,
            in PhysicalAudioContactDynamics dynamics)
        {
            int index = FindBodyPartContactDebugEntryIndex(contactEvent);
            if (index < 0)
                return;

            _bodyPartContactDebugEntries[index] =
                _bodyPartContactDebugEntries[index].WithDynamics(dynamics);
        }

        private int FindBodyPartContactDebugEntryIndex(in CombatPhysicalContactEvent contactEvent)
        {
            return _bodyPartContactDebugEntryIndices.TryGetValue(
                new BodyPartContactDebugKey(
                    contactEvent.SourceEntityId.Value,
                    contactEvent.ContactPointId,
                    contactEvent.BodyPartId),
                out int index)
                ? index
                : -1;
        }

        private static BodyPartContactHit CreateBodyPartContactHit(in CombatPhysicalContactEvent contactEvent)
        {
            return new BodyPartContactHit(
                contactEvent.TargetBodyId,
                contactEvent.TargetColliderId,
                contactEvent.TargetShapeKind,
                contactEvent.Point,
                contactEvent.Normal,
                contactEvent.SurfaceMaterial,
                contactEvent.Normal.Y >= MinimumStandableNormalY,
                contactEvent.ContactPointId,
                contactEvent.BodyPartId,
                contactEvent.ActivationMode);
        }
    }
}

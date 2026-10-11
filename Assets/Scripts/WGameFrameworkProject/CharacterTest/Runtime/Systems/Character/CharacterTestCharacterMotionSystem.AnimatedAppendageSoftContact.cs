using System;
using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestAnimatedAppendageLeafWitness
    {
        internal CharacterTestAnimatedAppendageLeafWitness(
            int ownerEntityId,
            int logicalColliderId,
            string logicalColliderLabel,
            int combatColliderId,
            int partId,
            string partLabel,
            CombatPhysicsShape shape,
            FixVector3 ownerRootWorld,
            RuntimeFrame frame,
            int hurtboxRevision,
            int topologyRevision,
            int bindingRevision,
            string bindingContentHash,
            int skinMillimeters,
            int pressureScaleMilli)
        {
            OwnerEntityId = ownerEntityId;
            LogicalColliderId = logicalColliderId;
            LogicalColliderLabel = logicalColliderLabel ?? string.Empty;
            CombatColliderId = combatColliderId;
            PartId = partId;
            PartLabel = partLabel ?? string.Empty;
            Shape = shape;
            OwnerRootWorld = ownerRootWorld;
            Frame = frame;
            HurtboxRevision = hurtboxRevision;
            TopologyRevision = topologyRevision;
            BindingRevision = bindingRevision;
            BindingContentHash = bindingContentHash ?? string.Empty;
            SkinMillimeters = skinMillimeters;
            PressureScaleMilli = pressureScaleMilli;
        }

        internal int OwnerEntityId { get; }
        internal int LogicalColliderId { get; }
        internal string LogicalColliderLabel { get; }
        internal int CombatColliderId { get; }
        internal int PartId { get; }
        internal string PartLabel { get; }
        internal CombatPhysicsShape Shape { get; }
        internal FixVector3 OwnerRootWorld { get; }
        internal RuntimeFrame Frame { get; }
        internal int HurtboxRevision { get; }
        internal int TopologyRevision { get; }
        internal int BindingRevision { get; }
        internal string BindingContentHash { get; }
        internal int SkinMillimeters { get; }
        internal int PressureScaleMilli { get; }
    }

    public sealed partial class CharacterTestCharacterMotionSystem
    {
        private const int AnimatedAppendageMaximumSourceOwners = 8;
        private const int AnimatedAppendageMaximumLeavesPerOwner = 4;
        private const int AnimatedAppendageMaximumLeafWitnesses =
            AnimatedAppendageMaximumSourceOwners * AnimatedAppendageMaximumLeavesPerOwner;

        private readonly CharacterTestAnimatedAppendageLeafWitness[] _animatedAppendagePreviousWitnesses =
            new CharacterTestAnimatedAppendageLeafWitness[AnimatedAppendageMaximumLeafWitnesses];
        private readonly CharacterTestAnimatedAppendageLeafWitness[] _animatedAppendageCurrentWitnesses =
            new CharacterTestAnimatedAppendageLeafWitness[AnimatedAppendageMaximumLeafWitnesses];
        private int _animatedAppendagePreviousWitnessCount;
        private int _animatedAppendageCurrentWitnessCount;
        private bool _animatedAppendagePreviousReady;

        internal CharacterTestAnimatedAppendageSoftContactDiagnostics
            CaptureAnimatedAppendageSoftContactDiagnostics()
        {
            return _contactResolver.CaptureAnimatedAppendageSoftContactDiagnostics();
        }

        internal bool TryGetAnimatedAppendageSoftContactPhase(
            int index,
            out CharacterTestAnimatedAppendageSoftContactPhaseFact fact)
        {
            return _contactResolver.TryGetAnimatedAppendageSoftContactPhase(index, out fact);
        }

        private bool TryCaptureAnimatedAppendagePreviousWitnesses(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            RuntimeFrame currentFrame)
        {
            _animatedAppendagePreviousWitnessCount = 0;
            Array.Clear(
                _animatedAppendagePreviousWitnesses,
                0,
                _animatedAppendagePreviousWitnesses.Length);
            return TryCaptureAnimatedAppendageWitnesses(
                entities,
                currentFrame,
                requireCurrentFrame: false,
                _animatedAppendagePreviousWitnesses,
                out _animatedAppendagePreviousWitnessCount);
        }

        internal void ResolveAnimatedAppendageSoftContact(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int tickRate,
            RuntimeFrame frame)
        {
            _animatedAppendageCurrentWitnessCount = 0;
            Array.Clear(
                _animatedAppendageCurrentWitnesses,
                0,
                _animatedAppendageCurrentWitnesses.Length);
            bool currentReady = _physicsRuntime != null
                && TryCaptureAnimatedAppendageWitnesses(
                    entities,
                    frame,
                    requireCurrentFrame: true,
                    _animatedAppendageCurrentWitnesses,
                    out _animatedAppendageCurrentWitnessCount);

            _contactResolver.ResolveAnimatedAppendageSoftContacts(
                entities,
                _physicsRuntime,
                _animatedAppendagePreviousWitnesses,
                _animatedAppendagePreviousWitnessCount,
                _animatedAppendagePreviousReady,
                _animatedAppendageCurrentWitnesses,
                _animatedAppendageCurrentWitnessCount,
                currentReady,
                _movementProfile.RunSpeedMillimetersPerSecond,
                _movementProfile.AccelerationMillimetersPerSecondSquared,
                tickRate);
            if (_physicsRuntime != null)
                _contactResolver.ApplyCharacterContactState(entities);

            _animatedAppendagePreviousReady = false;
            _animatedAppendagePreviousWitnessCount = 0;
            _animatedAppendageCurrentWitnessCount = 0;
        }

        private bool TryCaptureAnimatedAppendageWitnesses(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            RuntimeFrame expectedFrame,
            bool requireCurrentFrame,
            CharacterTestAnimatedAppendageLeafWitness[] destination,
            out int destinationCount)
        {
            destinationCount = 0;
            if (entities == null || destination == null)
                return false;

            int sourceOwnerCount = 0;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
            {
                CharacterTestCharacterEntity entity = entities[entityIndex];
                if (entity == null || entity.Id.Value <= 0)
                    continue;

                CharacterImportedGeometryBinding binding;
                try
                {
                    binding = CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity)?.Geometry;
                }
                catch (Exception)
                {
                    return false;
                }

                CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding[] sources =
                    binding?.AnimatedHurtboxSources;
                if (sources == null || sources.Length == 0)
                    continue;
                if (sources.Length > AnimatedAppendageMaximumLeavesPerOwner
                    || ++sourceOwnerCount > AnimatedAppendageMaximumSourceOwners
                    || binding.BodyPresenceMode != CharacterBodyPresenceMode.Authored
                    || binding.BodyPresenceRevision <= 0
                    || string.IsNullOrEmpty(binding.BodyPresenceContentHash))
                {
                    return false;
                }

                if (!_hurtboxSystem.TryGetDamageGeometrySnapshot(
                        entity.Id.Value,
                        out CharacterTestDamageGeometrySnapshot snapshot)
                    || snapshot == null
                    || !snapshot.IsCommitted
                    || snapshot.EntityId != entity.Id.Value)
                {
                    if (requireCurrentFrame)
                        return false;
                    continue;
                }

                if (requireCurrentFrame && !snapshot.Frame.Equals(expectedFrame))
                    return false;
                if (!requireCurrentFrame && snapshot.Frame.Value >= expectedFrame.Value)
                    continue;

                CharacterBodyColliderRuntimeBinding[] colliders = binding.BodyColliders;
                for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
                {
                    CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding source = sources[sourceIndex];
                    if (source == null || !source.Enabled)
                        continue;
                    if (source.SkinMillimeters < 0
                        || source.SkinMillimeters > 10000
                        || source.PressureScaleMilli < 0
                        || source.PressureScaleMilli > 100000
                        || !TryFindAuthoredColliderOrdinal(
                            colliders,
                            source.ColliderId,
                            out int colliderOrdinal,
                            out CharacterBodyColliderRuntimeBinding authoredCollider))
                    {
                        return false;
                    }

                    if (!snapshot.TryGetCollider(
                            source.ColliderId,
                            out CharacterTestDamageGeometryColliderMetadata metadata))
                    {
                        // An authored leaf omitted by the committed topology (for example Severed)
                        // is inactive, not an identity failure.
                        continue;
                    }

                    int expectedCombatColliderId = checked(
                        CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart
                        + colliderOrdinal);
                    if (metadata.Layer != CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer
                        || metadata.CombatColliderId.Value != expectedCombatColliderId
                        || metadata.LogicalColliderId != source.ColliderId
                        || metadata.PartId != authoredCollider.PartId
                        || !IsSupportedAnimatedAppendageShape(metadata.LocalShape.Kind)
                        || ContainsAnimatedAppendageWitness(
                            destination,
                            destinationCount,
                            entity.Id.Value,
                            metadata.LogicalColliderId,
                            metadata.CombatColliderId.Value,
                            metadata.LocalShape.Kind))
                    {
                        return false;
                    }

                    if (destinationCount >= destination.Length)
                        return false;
                    destination[destinationCount++] = new CharacterTestAnimatedAppendageLeafWitness(
                        entity.Id.Value,
                        metadata.LogicalColliderId,
                        metadata.LogicalColliderLabel,
                        metadata.CombatColliderId.Value,
                        metadata.PartId,
                        metadata.PartLabel,
                        metadata.LocalShape,
                        new FixVector3(
                            CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.XMillimeters),
                            CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.YMillimeters),
                            CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.ZMillimeters)),
                        snapshot.Frame,
                        snapshot.HurtboxRevision,
                        snapshot.TopologyRevision,
                        binding.BodyPresenceRevision,
                        binding.BodyPresenceContentHash,
                        source.SkinMillimeters,
                        source.PressureScaleMilli);
                }
            }

            SortAnimatedAppendageWitnesses(destination, destinationCount);
            return true;
        }

        private static bool TryFindAuthoredColliderOrdinal(
            CharacterBodyColliderRuntimeBinding[] colliders,
            int logicalColliderId,
            out int ordinal,
            out CharacterBodyColliderRuntimeBinding collider)
        {
            ordinal = -1;
            collider = null;
            if (colliders == null || logicalColliderId <= 0)
                return false;
            for (int i = 0; i < colliders.Length; i++)
            {
                CharacterBodyColliderRuntimeBinding candidate = colliders[i];
                if (candidate == null || candidate.ColliderId != logicalColliderId)
                {
                    continue;
                }
                if (ordinal >= 0)
                    return false;
                ordinal = i;
                collider = candidate;
            }
            return ordinal >= 0 && collider.PartId > 0;
        }

        private static bool ContainsAnimatedAppendageWitness(
            CharacterTestAnimatedAppendageLeafWitness[] witnesses,
            int count,
            int ownerEntityId,
            int logicalColliderId,
            int combatColliderId,
            CombatPhysicsShapeKind shapeKind)
        {
            for (int i = 0; i < count; i++)
            {
                CharacterTestAnimatedAppendageLeafWitness value = witnesses[i];
                if (value.OwnerEntityId == ownerEntityId
                    && value.CombatColliderId == combatColliderId
                    && value.Shape.Kind == shapeKind
                    && value.LogicalColliderId == logicalColliderId)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsSupportedAnimatedAppendageShape(CombatPhysicsShapeKind kind)
        {
            return kind == CombatPhysicsShapeKind.Sphere
                || kind == CombatPhysicsShapeKind.Capsule
                || kind == CombatPhysicsShapeKind.Obb;
        }

        private static void SortAnimatedAppendageWitnesses(
            CharacterTestAnimatedAppendageLeafWitness[] witnesses,
            int count)
        {
            for (int i = 1; i < count; i++)
            {
                CharacterTestAnimatedAppendageLeafWitness value = witnesses[i];
                int j = i - 1;
                while (j >= 0 && CompareAnimatedAppendageWitness(witnesses[j], value) > 0)
                {
                    witnesses[j + 1] = witnesses[j];
                    j--;
                }
                witnesses[j + 1] = value;
            }
        }

        private static int CompareAnimatedAppendageWitness(
            CharacterTestAnimatedAppendageLeafWitness left,
            CharacterTestAnimatedAppendageLeafWitness right)
        {
            int result = left.OwnerEntityId.CompareTo(right.OwnerEntityId);
            if (result != 0) return result;
            result = left.LogicalColliderId.CompareTo(right.LogicalColliderId);
            if (result != 0) return result;
            result = left.CombatColliderId.CompareTo(right.CombatColliderId);
            if (result != 0) return result;
            return left.Shape.Kind.CompareTo(right.Shape.Kind);
        }

        private void ResetAnimatedAppendageSoftContact()
        {
            Array.Clear(_animatedAppendagePreviousWitnesses, 0, _animatedAppendagePreviousWitnesses.Length);
            Array.Clear(_animatedAppendageCurrentWitnesses, 0, _animatedAppendageCurrentWitnesses.Length);
            _animatedAppendagePreviousWitnessCount = 0;
            _animatedAppendageCurrentWitnessCount = 0;
            _animatedAppendagePreviousReady = false;
            _contactResolver.SuppressAnimatedAppendagePresentationPhases();
        }
    }
}

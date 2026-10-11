using System;
using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestBodyPresenceSyncDiagnostics
    {
        internal CharacterTestBodyPresenceSyncDiagnostics(
            bool lastSyncSucceeded,
            int projectionCount,
            int projectionCapacity,
            int projectedLeafCount,
            int projectedLeafCapacity,
            int registeredEntityCount)
        {
            LastSyncSucceeded = lastSyncSucceeded;
            ProjectionCount = projectionCount;
            ProjectionCapacity = projectionCapacity;
            ProjectedLeafCount = projectedLeafCount;
            ProjectedLeafCapacity = projectedLeafCapacity;
            RegisteredEntityCount = registeredEntityCount;
        }

        internal bool LastSyncSucceeded { get; }
        internal int ProjectionCount { get; }
        internal int ProjectionCapacity { get; }
        internal int ProjectedLeafCount { get; }
        internal int ProjectedLeafCapacity { get; }
        internal int RegisteredEntityCount { get; }
    }

    /// <summary>Projects compiled ModelRoot Body Presence bindings into derived Combat Physics compounds.</summary>
    internal sealed class CharacterTestCharacterBodyPresenceSystem
    {
        private readonly Projection[] _projections =
            new Projection[CharacterTestCharacterPhysicsRuntime.BodyPresenceMaximumActiveCharacters];
        private readonly CombatPhysicsCompoundLeaf[] _projectedLeaves =
            new CombatPhysicsCompoundLeaf[CharacterTestCharacterPhysicsRuntime.BodyPresenceMaximumActiveLeaves];
        private readonly int[] _registeredEntityIds =
            new int[CharacterTestCharacterPhysicsRuntime.BodyPresenceMaximumActiveCharacters];
        private int _projectionCount;
        private int _projectedLeafCount;
        private int _registeredEntityCount;
        private bool _lastSyncSucceeded;

        /// <remarks>NoAlloc. Projects counters from the existing fixed Presence buffers.</remarks>
        internal CharacterTestBodyPresenceSyncDiagnostics CaptureDiagnostics()
        {
            return new CharacterTestBodyPresenceSyncDiagnostics(
                _lastSyncSucceeded,
                _projectionCount,
                _projections.Length,
                _projectedLeafCount,
                _projectedLeaves.Length,
                _registeredEntityCount);
        }

        /// <remarks>NoAlloc after package loading and the fixed buffers have been initialized.</remarks>
        internal bool SyncAll(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterPhysicsRuntime physics)
        {
            return SyncAll(entities, physics, null);
        }

        /// <remarks>
        /// The optional binding list is a deterministic test/import seam. When supplied, its entries
        /// correspond one-to-one with the entity list and bypass package lookup.
        /// </remarks>
        internal bool SyncAll(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterPhysicsRuntime physics,
            IReadOnlyList<CharacterImportedGeometryBinding> bindings)
        {
            _lastSyncSucceeded = false;
            if (physics?.World == null
                || physics.StaticObstacleLayerMask.ContainsLayer(
                    CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer)
                || physics.MotionObstacleLayerMask.ContainsLayer(
                    CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer)
                || physics.DynamicCharacterObstacleLayerMask.ContainsLayer(
                    CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer)
                || physics.DynamicMovableObjectObstacleLayerMask.ContainsLayer(
                    CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer))
            {
                Clear(physics);
                return false;
            }

            bool prepared;
            try
            {
                prepared = TryPrepare(entities, physics, bindings);
            }
            catch (ArgumentException)
            {
                Clear(physics);
                return false;
            }
            catch (InvalidOperationException)
            {
                Clear(physics);
                return false;
            }
            catch (OverflowException)
            {
                Clear(physics);
                return false;
            }

            if (!prepared)
            {
                Clear(physics);
                return false;
            }

            RemoveStaleEntities(entities, physics);
            try
            {
                for (int i = 0; i < _projectionCount; i++)
                {
                    if (!SyncProjection(_projections[i], physics))
                    {
                        Clear(physics);
                        return false;
                    }
                }
            }
            catch (ArgumentException)
            {
                physics.World.CancelCompoundColliderUpdate();
                Clear(physics);
                return false;
            }
            catch (InvalidOperationException)
            {
                physics.World.CancelCompoundColliderUpdate();
                Clear(physics);
                return false;
            }
            catch (OverflowException)
            {
                physics.World.CancelCompoundColliderUpdate();
                Clear(physics);
                return false;
            }

            _lastSyncSucceeded = true;
            return true;
        }

        internal bool RebuildAll(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterPhysicsRuntime physics)
        {
            return RebuildAll(entities, physics, null);
        }

        internal bool RebuildAll(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterPhysicsRuntime physics,
            IReadOnlyList<CharacterImportedGeometryBinding> bindings)
        {
            Clear(physics);
            return SyncAll(entities, physics, bindings);
        }

        internal void RemoveEntity(
            int entityId,
            CharacterTestCharacterPhysicsRuntime physics)
        {
            RemoveOwnedBody(entityId, physics);
            physics?.ForgetBodyPresenceColliderSync(entityId);
            UnregisterEntity(entityId);
        }

        internal void Clear(CharacterTestCharacterPhysicsRuntime physics)
        {
            for (int i = _registeredEntityCount - 1; i >= 0; i--)
                RemoveOwnedBody(_registeredEntityIds[i], physics);

            if (physics != null)
                physics.ClearBodyPresenceColliderSyncStates();
            Array.Clear(_registeredEntityIds, 0, _registeredEntityIds.Length);
            _registeredEntityCount = 0;
            _projectionCount = 0;
            _projectedLeafCount = 0;
            _lastSyncSucceeded = false;
        }

        private bool TryPrepare(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterPhysicsRuntime physics,
            IReadOnlyList<CharacterImportedGeometryBinding> bindings)
        {
            _projectionCount = 0;
            _projectedLeafCount = 0;
            int entityCount = entities?.Count ?? 0;
            if (entityCount > CharacterTestCharacterPhysicsRuntime.BodyPresenceMaximumActiveCharacters
                || (bindings != null && bindings.Count != entityCount))
            {
                return false;
            }

            for (int i = 0; i < entityCount; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null
                    || !physics.TryGetBodyPresenceBodyId(entity, out CombatBodyId bodyId)
                    || ContainsEntityBefore(entities, i, entity.Id.Value))
                {
                    return false;
                }

                CharacterImportedGeometryBinding binding = bindings == null
                    ? CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity)?.Geometry
                    : bindings[i];
                BindingState bindingState = ValidateBinding(binding, out int activeLeafCount);
                if (bindingState == BindingState.Invalid)
                    return false;

                bool hasPreviousWitness = physics.TryGetBodyPresenceSyncWitness(
                    entity.Id.Value,
                    out int previousRevision,
                    out string previousHash,
                    out _);
                if (bindingState == BindingState.Authored
                    && hasPreviousWitness
                    && (binding.BodyPresenceRevision < previousRevision
                        || (binding.BodyPresenceRevision == previousRevision
                            && !string.Equals(
                                binding.BodyPresenceContentHash,
                                previousHash,
                                StringComparison.Ordinal))))
                {
                    return false;
                }

                if ((bindingState == BindingState.Authored || hasPreviousWitness)
                    && physics.World.TryGetBody(bodyId, out CombatPhysicsBody occupiedBody))
                {
                    CombatEntityId expectedEntityId = physics.GetEntityId(entity);
                    if (!hasPreviousWitness || !occupiedBody.EntityId.Equals(expectedEntityId))
                        return false;
                }

                int leafStart = _projectedLeafCount;
                if (bindingState == BindingState.Authored)
                {
                    CharacterBodyPresenceRuntimeBinding[] volumes = binding.BodyPresenceVolumes;
                    for (int volumeOrdinal = 0; volumeOrdinal < volumes.Length; volumeOrdinal++)
                    {
                        CharacterBodyPresenceRuntimeBinding volume = volumes[volumeOrdinal];
                        if (!volume.Enabled)
                            continue;
                        if (_projectedLeafCount >= _projectedLeaves.Length
                            || !CharacterTestCharacterPhysicsRuntime.TryGetBodyPresenceColliderId(
                                volumeOrdinal,
                                out CombatColliderId colliderId)
                            || !TryCreateLocalShape(
                                volume,
                                entity.Transform.YawMilliDegrees,
                                out CombatPhysicsShape shape))
                        {
                            return false;
                        }

                        _projectedLeaves[_projectedLeafCount++] =
                            new CombatPhysicsCompoundLeaf(
                                colliderId,
                                CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer,
                                shape);
                    }
                }

                if (_projectedLeafCount - leafStart != activeLeafCount)
                    return false;

                _projections[_projectionCount++] = new Projection(
                    entity,
                    bodyId,
                    bindingState,
                    binding,
                    leafStart,
                    activeLeafCount);
            }

            return _projectedLeafCount
                <= CharacterTestCharacterPhysicsRuntime.BodyPresenceMaximumActiveLeaves;
        }

        private bool SyncProjection(
            Projection projection,
            CharacterTestCharacterPhysicsRuntime physics)
        {
            CharacterTestCharacterEntity entity = projection.Entity;
            int entityId = entity.Id.Value;
            if (projection.BindingState == BindingState.Missing)
            {
                RemoveEntity(entityId, physics);
                return true;
            }

            CharacterImportedGeometryBinding binding = projection.Binding;
            CharacterTestCharacterTransformComponent root = entity.Transform;
            if (physics.IsBodyPresenceColliderSynced(
                    entityId,
                    root.XMillimeters,
                    root.YMillimeters,
                    root.ZMillimeters,
                    root.YawMilliDegrees,
                    binding.BodyPresenceRevision,
                    binding.BodyPresenceContentHash,
                    projection.ActiveLeafCount))
            {
                if (projection.ActiveLeafCount == 0)
                {
                    if (!physics.World.TryGetBody(projection.BodyId, out _))
                        return true;
                }
                else if (physics.World.TryGetBody(
                             projection.BodyId,
                             out CombatPhysicsBody body)
                         && body.EntityId.Equals(physics.GetEntityId(entity)))
                {
                    return true;
                }
            }

            if (projection.ActiveLeafCount == 0)
            {
                RemoveOwnedBody(entityId, physics);
                RegisterEntity(entityId);
                physics.MarkBodyPresenceColliderSynced(
                    entityId,
                    root.XMillimeters,
                    root.YMillimeters,
                    root.ZMillimeters,
                    root.YawMilliDegrees,
                    binding.BodyPresenceRevision,
                    binding.BodyPresenceContentHash,
                    activeLeafCount: 0);
                return true;
            }

            FixVector3 rootPosition = new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(root.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(root.YMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(root.ZMillimeters));
            physics.World.UpsertBody(new CombatPhysicsBody(
                physics.GetEntityId(entity),
                projection.BodyId,
                rootPosition));
            physics.World.BeginCompoundColliderUpdate(
                projection.BodyId,
                projection.ActiveLeafCount);
            for (int i = 0; i < projection.ActiveLeafCount; i++)
                physics.World.UpsertCompoundCollider(_projectedLeaves[projection.LeafStart + i]);
            physics.World.EndCompoundColliderUpdate();

            RegisterEntity(entityId);
            physics.MarkBodyPresenceColliderSynced(
                entityId,
                root.XMillimeters,
                root.YMillimeters,
                root.ZMillimeters,
                root.YawMilliDegrees,
                binding.BodyPresenceRevision,
                binding.BodyPresenceContentHash,
                projection.ActiveLeafCount);
            return true;
        }

        private void RemoveStaleEntities(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterPhysicsRuntime physics)
        {
            int index = 0;
            while (index < _registeredEntityCount)
            {
                int entityId = _registeredEntityIds[index];
                if (ContainsEntity(entities, entityId))
                {
                    index++;
                    continue;
                }

                RemoveEntity(entityId, physics);
            }
        }

        private void RemoveOwnedBody(
            int entityId,
            CharacterTestCharacterPhysicsRuntime physics)
        {
            if (physics?.World == null
                || !physics.TryGetBodyPresenceBodyId(entityId, out CombatBodyId bodyId)
                || !physics.World.TryGetBody(bodyId, out CombatPhysicsBody body))
            {
                return;
            }

            var expectedEntityId = new CombatEntityId(
                checked(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entityId));
            if (body.EntityId.Equals(expectedEntityId))
                physics.World.RemoveBody(bodyId);
        }

        private void RegisterEntity(int entityId)
        {
            for (int i = 0; i < _registeredEntityCount; i++)
            {
                if (_registeredEntityIds[i] == entityId)
                    return;
            }

            if (_registeredEntityCount >= _registeredEntityIds.Length)
                throw new InvalidOperationException("Body Presence registration capacity was exceeded.");
            _registeredEntityIds[_registeredEntityCount++] = entityId;
        }

        private void UnregisterEntity(int entityId)
        {
            for (int i = 0; i < _registeredEntityCount; i++)
            {
                if (_registeredEntityIds[i] != entityId)
                    continue;
                for (int j = i + 1; j < _registeredEntityCount; j++)
                    _registeredEntityIds[j - 1] = _registeredEntityIds[j];
                _registeredEntityIds[--_registeredEntityCount] = 0;
                return;
            }
        }

        private static BindingState ValidateBinding(
            CharacterImportedGeometryBinding binding,
            out int activeLeafCount)
        {
            activeLeafCount = 0;
            if (binding == null)
                return BindingState.Missing;

            bool legacy = string.IsNullOrEmpty(binding.Format)
                || string.Equals(binding.Format, "mx.characterGeometryBinding.v1", StringComparison.Ordinal)
                || string.Equals(binding.Format, "mx.characterGeometryBinding.v2", StringComparison.Ordinal)
                || string.Equals(binding.Format, "mx.characterGeometryBinding.v3", StringComparison.Ordinal);
            if (legacy)
            {
                return binding.BodyPresenceMode == CharacterBodyPresenceMode.CompatibilityHardRoot
                    && (binding.BodyPresenceVolumes == null || binding.BodyPresenceVolumes.Length == 0)
                        ? BindingState.Missing
                        : BindingState.Invalid;
            }

            bool isV4 = string.Equals(
                    binding.Format,
                    "mx.characterGeometryBinding.v4",
                    StringComparison.Ordinal)
                && string.Equals(binding.SchemaVersion, "4.0", StringComparison.Ordinal);
            bool isV5 = string.Equals(
                    binding.Format,
                    "mx.characterGeometryBinding.v5",
                    StringComparison.Ordinal)
                && string.Equals(binding.SchemaVersion, "5.0", StringComparison.Ordinal);
            bool isV6 = string.Equals(
                    binding.Format,
                    "mx.characterGeometryBinding.v6",
                    StringComparison.Ordinal)
                && string.Equals(binding.SchemaVersion, "6.0", StringComparison.Ordinal);
            bool isV7 = string.Equals(
                    binding.Format,
                    "mx.characterGeometryBinding.v7",
                    StringComparison.Ordinal)
                && string.Equals(binding.SchemaVersion, "7.0", StringComparison.Ordinal);
            bool isV8 = string.Equals(binding.Format, "mx.characterGeometryBinding.v8", StringComparison.Ordinal)
                && string.Equals(binding.SchemaVersion, "8.0", StringComparison.Ordinal);
            if (!isV4 && !isV5 && !isV6 && !isV7 && !isV8)
                return BindingState.Invalid;

            if ((isV5 || isV6 || isV8)
                && binding.BodyPresenceMode == CharacterBodyPresenceMode.CompatibilityHardRoot)
            {
                return binding.BodyPresenceRevision == 0
                    && string.IsNullOrEmpty(binding.BodyPresenceContentHash)
                    && (binding.BodyPresenceVolumes == null
                        || binding.BodyPresenceVolumes.Length == 0)
                            ? BindingState.Missing
                            : BindingState.Invalid;
            }

            if (binding.BodyPresenceMode != CharacterBodyPresenceMode.Authored
                || binding.BodyPresenceRevision < 1
                || !IsSha256(binding.BodyPresenceContentHash)
                || binding.BodyPresenceVolumes == null
                || binding.BodyPresenceVolumes.Length
                    > CharacterTestCharacterPhysicsRuntime.BodyPresenceColliderCapacity)
            {
                return BindingState.Invalid;
            }

            string previousVolumeId = string.Empty;
            for (int i = 0; i < binding.BodyPresenceVolumes.Length; i++)
            {
                CharacterBodyPresenceRuntimeBinding volume = binding.BodyPresenceVolumes[i];
                if (!IsValidVolume(volume)
                    || (i > 0
                        && string.CompareOrdinal(previousVolumeId, volume.VolumeId) >= 0))
                {
                    return BindingState.Invalid;
                }

                previousVolumeId = volume.VolumeId;
                if (volume.Enabled)
                    activeLeafCount++;
            }

            return BindingState.Authored;
        }

        private static bool IsValidVolume(CharacterBodyPresenceRuntimeBinding volume)
        {
            if (volume == null
                || !IsStableVolumeId(volume.VolumeId)
                || volume.ReferenceFrame != CharacterBodyPresenceReferenceFrame.ModelRoot
                || !string.Equals(volume.LocalPose.ParentKind, "ModelRoot", StringComparison.Ordinal)
                || !string.IsNullOrEmpty(volume.LocalPose.ParentPath)
                || !IsFinite(volume.LocalPose.Position.X)
                || !IsFinite(volume.LocalPose.Position.Y)
                || !IsFinite(volume.LocalPose.Position.Z)
                || !IsFinite(volume.LocalPose.Rotation.X)
                || !IsFinite(volume.LocalPose.Rotation.Y)
                || !IsFinite(volume.LocalPose.Rotation.Z)
                || !IsFinite(volume.LocalPose.Rotation.W)
                || !IsFinite(volume.LocalPose.Scale.X)
                || !IsFinite(volume.LocalPose.Scale.Y)
                || !IsFinite(volume.LocalPose.Scale.Z)
                || !IsFinite(volume.Radius)
                || !IsFinite(volume.Height)
                || !IsFinite(volume.Size.X)
                || !IsFinite(volume.Size.Y)
                || !IsFinite(volume.Size.Z))
            {
                return false;
            }

            CharacterRuntimeVector3 position = volume.LocalPose.Position;
            CharacterRuntimeQuaternion rotation = volume.LocalPose.Rotation;
            CharacterRuntimeVector3 scale = volume.LocalPose.Scale;
            double normSquared = (double)rotation.X * rotation.X
                + (double)rotation.Y * rotation.Y
                + (double)rotation.Z * rotation.Z
                + (double)rotation.W * rotation.W;
            bool validPose = Math.Abs(position.X) <= 1000f
                && Math.Abs(position.Y) <= 1000f
                && Math.Abs(position.Z) <= 1000f
                && Math.Abs(normSquared - 1d) <= 0.001d
                && Math.Abs(scale.X - 1f) <= 0.0001f
                && Math.Abs(scale.Y - 1f) <= 0.0001f
                && Math.Abs(scale.Z - 1f) <= 0.0001f;
            bool boundedDimensions = volume.Radius >= 0f && volume.Radius <= 1000f
                && volume.Height >= 0f && volume.Height <= 1000f
                && volume.Size.X >= 0f && volume.Size.X <= 1000f
                && volume.Size.Y >= 0f && volume.Size.Y <= 1000f
                && volume.Size.Z >= 0f && volume.Size.Z <= 1000f;
            bool validDimensions = volume.Shape switch
            {
                CharacterBodyPresenceShape.Sphere => volume.Radius > 0f,
                CharacterBodyPresenceShape.Capsule =>
                    volume.Radius > 0f && volume.Height >= 2f * volume.Radius,
                CharacterBodyPresenceShape.Box =>
                    volume.Size.X > 0f && volume.Size.Y > 0f && volume.Size.Z > 0f,
                _ => false
            };
            return validPose
                && boundedDimensions
                && validDimensions
                && volume.SkinMillimeters >= 0
                && volume.SkinMillimeters <= 10000
                && volume.PressureScaleMilli >= 0
                && volume.PressureScaleMilli <= 100000;
        }

        private static bool TryCreateLocalShape(
            CharacterBodyPresenceRuntimeBinding volume,
            int rootYawMilliDegrees,
            out CombatPhysicsShape shape)
        {
            shape = default;
            FixVector3 center = RotateYaw(
                ToFixVector(volume.LocalPose.Position),
                rootYawMilliDegrees);
            Fix64 skin = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                volume.SkinMillimeters);
            switch (volume.Shape)
            {
                case CharacterBodyPresenceShape.Sphere:
                {
                    Fix64 radius = ToFixMeters(volume.Radius) + skin;
                    if (radius <= Fix64.Zero)
                        return false;
                    shape = CombatPhysicsShape.Sphere(center, radius);
                    return true;
                }
                case CharacterBodyPresenceShape.Capsule:
                {
                    if (!TryCreateAxes(
                            volume.LocalPose.Rotation,
                            rootYawMilliDegrees,
                            out _,
                            out FixVector3 axisY,
                            out _))
                    {
                        return false;
                    }

                    Fix64 radius = ToFixMeters(volume.Radius);
                    Fix64 height = ToFixMeters(volume.Height);
                    Fix64 halfSegment = height / Fix64.FromInt(2) - radius;
                    if (radius <= Fix64.Zero || halfSegment < Fix64.Zero)
                        return false;
                    shape = CombatPhysicsShape.Capsule(
                        center - axisY * halfSegment,
                        center + axisY * halfSegment,
                        radius + skin);
                    return true;
                }
                case CharacterBodyPresenceShape.Box:
                {
                    if (!TryCreateAxes(
                            volume.LocalPose.Rotation,
                            rootYawMilliDegrees,
                            out FixVector3 axisX,
                            out FixVector3 axisY,
                            out FixVector3 axisZ))
                    {
                        return false;
                    }

                    Fix64 two = Fix64.FromInt(2);
                    FixVector3 halfExtents = new FixVector3(
                        ToFixMeters(volume.Size.X) / two + skin,
                        ToFixMeters(volume.Size.Y) / two + skin,
                        ToFixMeters(volume.Size.Z) / two + skin);
                    if (halfExtents.X <= Fix64.Zero
                        || halfExtents.Y <= Fix64.Zero
                        || halfExtents.Z <= Fix64.Zero)
                    {
                        return false;
                    }

                    shape = CombatPhysicsShape.Obb(
                        center,
                        halfExtents,
                        axisX,
                        axisY,
                        axisZ);
                    return true;
                }
                default:
                    return false;
            }
        }

        private static bool TryCreateAxes(
            CharacterRuntimeQuaternion rotation,
            int rootYawMilliDegrees,
            out FixVector3 axisX,
            out FixVector3 axisY,
            out FixVector3 axisZ)
        {
            double x = rotation.X;
            double y = rotation.Y;
            double z = rotation.Z;
            double w = rotation.W;
            FixVector3 rawX = new FixVector3(
                ToFixUnit(1d - 2d * (y * y + z * z)),
                ToFixUnit(2d * (x * y + z * w)),
                ToFixUnit(2d * (x * z - y * w)));
            FixVector3 rawY = new FixVector3(
                ToFixUnit(2d * (x * y - z * w)),
                ToFixUnit(1d - 2d * (x * x + z * z)),
                ToFixUnit(2d * (y * z + x * w)));
            if (!rawX.TryNormalize(out axisX))
            {
                axisY = FixVector3.Zero;
                axisZ = FixVector3.Zero;
                return false;
            }

            FixVector3 orthogonalY = rawY - axisX * axisX.Dot(rawY);
            if (!orthogonalY.TryNormalize(out axisY)
                || !Cross(axisX, axisY).TryNormalize(out axisZ)
                || !Cross(axisZ, axisX).TryNormalize(out axisY))
            {
                axisX = FixVector3.Zero;
                axisY = FixVector3.Zero;
                axisZ = FixVector3.Zero;
                return false;
            }

            axisX = RotateYaw(axisX, rootYawMilliDegrees);
            axisY = RotateYaw(axisY, rootYawMilliDegrees);
            axisZ = RotateYaw(axisZ, rootYawMilliDegrees);
            return axisX.TryNormalize(out axisX)
                && axisY.TryNormalize(out axisY)
                && axisZ.TryNormalize(out axisZ);
        }

        private static FixVector3 RotateYaw(FixVector3 value, int yawMilliDegrees)
        {
            CharacterTestDeterministicMath.SinCosMilliDegrees(
                yawMilliDegrees,
                out int sinScaled,
                out int cosScaled);
            Fix64 sin = Fix64.FromRaw(
                (long)sinScaled * Fix64.Scale / CharacterTestDeterministicMath.TrigScale);
            Fix64 cos = Fix64.FromRaw(
                (long)cosScaled * Fix64.Scale / CharacterTestDeterministicMath.TrigScale);
            return new FixVector3(
                value.X * cos + value.Z * sin,
                value.Y,
                -value.X * sin + value.Z * cos);
        }

        private static FixVector3 Cross(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                left.Y * right.Z - left.Z * right.Y,
                left.Z * right.X - left.X * right.Z,
                left.X * right.Y - left.Y * right.X);
        }

        private static FixVector3 ToFixVector(CharacterRuntimeVector3 value)
        {
            return new FixVector3(
                ToFixMeters(value.X),
                ToFixMeters(value.Y),
                ToFixMeters(value.Z));
        }

        private static Fix64 ToFixMeters(float value)
        {
            return CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                CharacterTestDeterministicUnits.MetersToMillimeters(value));
        }

        private static Fix64 ToFixUnit(double value)
        {
            return Fix64.FromRaw(checked((long)Math.Round(
                value * Fix64.Scale,
                MidpointRounding.AwayFromZero)));
        }

        private static bool ContainsEntityBefore(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int exclusiveEnd,
            int entityId)
        {
            for (int i = 0; i < exclusiveEnd; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null && entity.Id.Value == entityId)
                    return true;
            }
            return false;
        }

        private static bool ContainsEntity(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int entityId)
        {
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null && entity.Id.Value == entityId)
                    return true;
            }
            return false;
        }

        private static bool IsSha256(string value)
        {
            const string prefix = "sha256:";
            if (value == null || value.Length != prefix.Length + 64
                || !value.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            for (int i = prefix.Length; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return false;
            }
            return true;
        }

        private static bool IsStableVolumeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 96)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLower(c) && !char.IsDigit(c) && c != '.' && c != '_' && c != '-')
                    return false;
            }
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private enum BindingState
        {
            Missing = 0,
            Authored = 1,
            Invalid = 2
        }

        private readonly struct Projection
        {
            public Projection(
                CharacterTestCharacterEntity entity,
                CombatBodyId bodyId,
                BindingState bindingState,
                CharacterImportedGeometryBinding binding,
                int leafStart,
                int activeLeafCount)
            {
                Entity = entity;
                BodyId = bodyId;
                BindingState = bindingState;
                Binding = binding;
                LeafStart = leafStart;
                ActiveLeafCount = activeLeafCount;
            }

            public CharacterTestCharacterEntity Entity { get; }
            public CombatBodyId BodyId { get; }
            public BindingState BindingState { get; }
            public CharacterImportedGeometryBinding Binding { get; }
            public int LeafStart { get; }
            public int ActiveLeafCount { get; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Resources;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Projects package-local baked hurtboxes into deterministic Combat Physics.</summary>
    internal sealed partial class CharacterTestCharacterHurtboxSystem
    {
        private readonly Dictionary<int, PackageState> _preparedEntityPackages = new Dictionary<int, PackageState>();
        private readonly Dictionary<int, int> _registeredColliderCounts = new Dictionary<int, int>();
        private readonly Dictionary<int, EntityPoseState> _entityPoseStates = new Dictionary<int, EntityPoseState>();
        private readonly Dictionary<int, CharacterTestDamageGeometrySnapshot> _geometrySnapshots =
            new Dictionary<int, CharacterTestDamageGeometrySnapshot>();
        private readonly Dictionary<int, CharacterTestDamageZoneSnapshot> _zoneSnapshots =
            new Dictionary<int, CharacterTestDamageZoneSnapshot>();
        private readonly Dictionary<int, CharacterImportedGeometryBinding> _runtimeGeometryBindings =
            new Dictionary<int, CharacterImportedGeometryBinding>();
        private readonly Dictionary<int, int> _hurtboxRevisionCounters = new Dictionary<int, int>();
        private readonly HashSet<string> _reportedIssues = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<int> _staleEntityIds = new List<int>(32);

        private CharacterActionConfigRegistry _actionPoseRegistry;
        internal void SetActionPoseRegistry(CharacterActionConfigRegistry registry) => _actionPoseRegistry = registry;

        public void Clear()
        {
            Clear(null);
        }

        internal void Clear(CharacterTestCharacterPhysicsRuntime physics)
        {
            RemoveAllPhysicsBodies(physics);
            InvalidateAllGeometry();
            _preparedEntityPackages.Clear();
            _registeredColliderCounts.Clear();
            _entityPoseStates.Clear();
            _geometrySnapshots.Clear();
            _zoneSnapshots.Clear();
            _runtimeGeometryBindings.Clear();
            _hurtboxRevisionCounters.Clear();
            _reportedIssues.Clear();
            _referenceDummies.Clear();
        }

        public void SyncAll(IReadOnlyList<CharacterTestCharacterEntity> entities, CharacterTestCharacterPhysicsRuntime physics, RuntimeFrame frame)
        {
            if (entities == null || physics?.World == null)
            {
                Clear(physics);
                return;
            }

            SweepStaleEntities(entities, physics);
            for (int i = 0; i < entities.Count; i++) Sync(entities[i], physics, frame);

            // A later actor can advance the shared physics revision. Seal every
            // successful view to the final revision without enumerating storage.
            int finalWorldRevision = physics.World.Revision;
            int finalTopologyRevision = physics.World.TopologyRevision;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null
                    && _geometrySnapshots.TryGetValue(entity.Id.Value, out CharacterTestDamageGeometrySnapshot snapshot))
                {
                    snapshot.SealWorldRevision(frame, finalWorldRevision, finalTopologyRevision);
                    if (_zoneSnapshots.TryGetValue(entity.Id.Value, out CharacterTestDamageZoneSnapshot zoneSnapshot))
                        zoneSnapshot.SealWorldRevision(frame, finalWorldRevision);
                }
            }
        }

        internal void RemoveEntity(int entityId, CharacterTestCharacterPhysicsRuntime physics)
        {
            RemoveEntityState(entityId, physics);
        }

        internal bool TryGetDamageGeometrySnapshot(
            int entityId,
            out CharacterTestDamageGeometrySnapshot snapshot)
        {
            return _geometrySnapshots.TryGetValue(entityId, out snapshot)
                && snapshot != null
                && snapshot.IsValid;
        }

        /// <summary>
        /// Returns the current typed geometry state, including a committed
        /// fail-closed state with zero active explicit leaves.
        /// </summary>
        internal bool TryGetDamageGeometrySnapshotState(
            int entityId,
            out CharacterTestDamageGeometrySnapshot snapshot)
        {
            return _geometrySnapshots.TryGetValue(entityId, out snapshot)
                && snapshot != null
                && snapshot.IsCommitted;
        }

        internal int GetHurtboxRevision(int entityId)
        {
            return _geometrySnapshots.TryGetValue(entityId, out CharacterTestDamageGeometrySnapshot snapshot)
                && snapshot != null
                && snapshot.IsValid
                    ? snapshot.HurtboxRevision
                    : 0;
        }

        internal bool TryGetDamageZoneSnapshot(
            int entityId,
            out CharacterTestDamageZoneSnapshot snapshot)
        {
            return _zoneSnapshots.TryGetValue(entityId, out snapshot)
                && snapshot != null
                && snapshot.IsValid;
        }

        /// <summary>NoAlloc: returns the sealed compiler binding used by this entity.</summary>
        internal bool TryGetRuntimeGeometryBinding(
            int entityId,
            out CharacterImportedGeometryBinding binding)
        {
            return _runtimeGeometryBindings.TryGetValue(entityId, out binding)
                && binding != null;
        }

        private CharacterTestDamageGeometrySnapshot BeginGeometryWrite(
            CharacterTestCharacterEntity entity,
            CombatEntityId entityId,
            int expectedColliderCount)
        {
            if (!_geometrySnapshots.TryGetValue(entity.Id.Value, out CharacterTestDamageGeometrySnapshot snapshot))
            {
                snapshot = new CharacterTestDamageGeometrySnapshot(expectedColliderCount);
                _geometrySnapshots.Add(entity.Id.Value, snapshot);
            }

            snapshot.BeginWrite(entity.Id.Value, entity.GameplayEntityId, entityId);
            return snapshot;
        }

        private int NextHurtboxRevision(int entityId)
        {
            _hurtboxRevisionCounters.TryGetValue(entityId, out int revision);
            unchecked
            {
                revision++;
            }
            if (revision <= 0)
                revision = 1;
            _hurtboxRevisionCounters[entityId] = revision;
            return revision;
        }

        private static void InvalidateGeometry(CharacterTestDamageGeometrySnapshot snapshot)
        {
            snapshot?.Invalidate();
        }

        private void InvalidateAllGeometry()
        {
            foreach (CharacterTestDamageGeometrySnapshot snapshot in _geometrySnapshots.Values)
                snapshot?.Invalidate();
            foreach (CharacterTestDamageZoneSnapshot snapshot in _zoneSnapshots.Values)
                snapshot?.Invalidate();
        }

        private void SweepStaleEntities(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestCharacterPhysicsRuntime physics)
        {
            _staleEntityIds.Clear();
            foreach (int entityId in _geometrySnapshots.Keys)
            {
                if (!ContainsEntity(entities, entityId))
                    _staleEntityIds.Add(entityId);
            }

            _staleEntityIds.Sort();
            for (int i = 0; i < _staleEntityIds.Count; i++)
                RemoveEntityState(_staleEntityIds[i], physics);
        }

        private static bool ContainsEntity(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int entityId)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null && entity.Id.Value == entityId)
                    return true;
            }

            return false;
        }

        private void RemoveAllPhysicsBodies(CharacterTestCharacterPhysicsRuntime physics)
        {
            if (physics?.World == null)
                return;

            foreach (int entityId in _geometrySnapshots.Keys)
            {
                physics.World.RemoveBody(GetHurtboxBodyId(entityId));
            }

            foreach (int entityId in _registeredColliderCounts.Keys)
            {
                physics.World.RemoveBody(GetHurtboxBodyId(entityId));
            }
        }

        private void RemoveEntityState(
            int entityId,
            CharacterTestCharacterPhysicsRuntime physics)
        {
            if (physics?.World != null)
                physics.World.RemoveBody(GetHurtboxBodyId(entityId));

            if (_geometrySnapshots.TryGetValue(entityId, out CharacterTestDamageGeometrySnapshot snapshot))
            {
                snapshot?.Invalidate();
                _geometrySnapshots.Remove(entityId);
            }
            if (_zoneSnapshots.TryGetValue(entityId, out CharacterTestDamageZoneSnapshot zoneSnapshot))
            {
                zoneSnapshot?.Invalidate();
                _zoneSnapshots.Remove(entityId);
            }

            _registeredColliderCounts.Remove(entityId);
            _preparedEntityPackages.Remove(entityId);
            _entityPoseStates.Remove(entityId);
            _hurtboxRevisionCounters.Remove(entityId);
            _runtimeGeometryBindings.Remove(entityId);
            _referenceDummies.Remove(entityId);
        }

        private static CombatBodyId GetHurtboxBodyId(int entityId)
        {
            return new CombatBodyId(
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxBodyIdStart + entityId);
        }

        private void Sync(CharacterTestCharacterEntity entity, CharacterTestCharacterPhysicsRuntime physics, RuntimeFrame frame)
        {
            if (entity == null)
                return;

            CombatBodyId bodyId = physics.GetHurtboxBodyId(entity);
            try
            {
                SyncCore(entity, physics, frame, bodyId);
            }
            catch
            {
                physics.World.RemoveBody(bodyId);
                if (_geometrySnapshots.TryGetValue(entity.Id.Value, out CharacterTestDamageGeometrySnapshot geometry))
                    geometry.Invalidate();
                throw;
            }
        }

        private void SyncCore(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterPhysicsRuntime physics,
            RuntimeFrame frame,
            CombatBodyId bodyId)
        {
            // Invalidate before package/catalog I/O so an exception or malformed
            // source cannot leave the previous frame exposed as current.
            if (_geometrySnapshots.TryGetValue(entity.Id.Value, out CharacterTestDamageGeometrySnapshot previousGeometry))
                previousGeometry.Invalidate();
            if (_zoneSnapshots.TryGetValue(entity.Id.Value, out CharacterTestDamageZoneSnapshot previousZones))
                previousZones.Invalidate();
            if (_referenceDummies.Count != 0 && _referenceDummies.TryGetValue(entity.Id.Value, out CharacterActionReferenceDummy referenceDummy))
            {
                SyncReferenceDummy(entity, physics, frame, bodyId, referenceDummy);
                return;
            }
            PackageState state = GetPackageState(entity);
            CharacterImportedGeometryBinding importedGeometry = state.Package?.Geometry;
            if (importedGeometry != null)
                _runtimeGeometryBindings[entity.Id.Value] = importedGeometry;
            else
                _runtimeGeometryBindings.Remove(entity.Id.Value);
            CombatEntityId entityId = physics.GetEntityId(entity);
            // Preserve the package ordinal for the existing hit-zone resolver. The
            // read-only geometry view performs its own stable logical-id ordering.
            CharacterBodyColliderRuntimeBinding[] bindings =
                state.Package?.Geometry?.BodyColliders ?? Array.Empty<CharacterBodyColliderRuntimeBinding>();
            CharacterTestDamageGeometrySnapshot geometry = BeginGeometryWrite(entity, entityId, bindings.Length > 0 ? bindings.Length : 1);

            if (bindings.Length == 0)
            {
                physics.World.RemoveCompound(bodyId);
                SyncCompatibilityHurtbox(entity, physics, bodyId, entityId, geometry);
                _registeredColliderCounts[entity.Id.Value] = 1;
                geometry.SortCollidersByStableLogicalId();
                geometry.Commit(
                    frame,
                    physics.World.Revision,
                    NextHurtboxRevision(entity.Id.Value),
                    physics.World.TopologyRevision);
                BuildZoneSnapshot(entity.Id.Value, geometry, state.Package?.Geometry?.DamageZones);
                return;
            }

            if (!TryResolveActiveLeafCount(
                    entity.BodyPartState,
                    state.HitZoneMetadata,
                    bindings.Length,
                    out int activeLeafCount))
            {
                physics.World.RemoveBody(bodyId);
                _registeredColliderCounts[entity.Id.Value] = 0;
                geometry.CommitUnavailable(
                    frame,
                    physics.World.Revision,
                    NextHurtboxRevision(entity.Id.Value),
                    physics.World.TopologyRevision,
                    CharacterTestDamageGeometryStatus.InvalidBodyPartBinding);
                BuildZoneSnapshot(entity.Id.Value, geometry, state.Package?.Geometry?.DamageZones);
                ReportOnce(
                    state.Key + "|body-part-filter|" + entity.Id.Value,
                    "CHARACTER_HURTBOX_BODY_PART_BINDING_INVALID package=" + state.Key
                        + " entity=" + entity.Id.Value);
                return;
            }

            FixVector3 rootPosition = new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.YMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.ZMillimeters));

            if (activeLeafCount == 0)
            {
                physics.World.UpsertBody(new CombatPhysicsBody(entityId, bodyId, rootPosition));
                var emptyCompatibilityColliderId = new CombatColliderId(
                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart);
                if (physics.World.TryGetAabbCollider(bodyId, emptyCompatibilityColliderId, out _))
                    physics.World.RemoveCollider(bodyId, emptyCompatibilityColliderId);

                bool emptyCompoundCompleted = false;
                physics.World.BeginCompoundColliderUpdate(bodyId, bindings.Length);
                try
                {
                    // EndUpdate treats every previously registered leaf omitted from
                    // this active write set as an explicit removal and rebuilds the
                    // compound to a stable zero-leaf topology.
                    physics.World.EndCompoundColliderUpdate();
                    emptyCompoundCompleted = true;
                }
                finally
                {
                    if (!emptyCompoundCompleted)
                        physics.World.CancelCompoundColliderUpdate();
                }

                _registeredColliderCounts[entity.Id.Value] = 0;
                geometry.CommitUnavailable(
                    frame,
                    physics.World.Revision,
                    NextHurtboxRevision(entity.Id.Value),
                    physics.World.TopologyRevision,
                    CharacterTestDamageGeometryStatus.ExplicitLeavesInactive);
                BuildZoneSnapshot(entity.Id.Value, geometry, state.Package?.Geometry?.DamageZones);
                return;
            }

            if (state.Catalog == null)
            {
                physics.World.RemoveBody(bodyId);
                InvalidateGeometry(geometry);
                ReportOnce(state.Key + "|catalog", "CHARACTER_HURTBOX_CATALOG_MISSING package=" + state.Key + " entity=" + entity.Id.Value);
                return;
            }

            if (!TryResolvePoseSources(entity, state, frame, out int poseSourceCount))
            {
                physics.World.RemoveBody(bodyId);
                InvalidateGeometry(geometry);
                ReportOnce(state.Key + "|clip", "CHARACTER_HURTBOX_CLIP_MISSING package=" + state.Key + " entity=" + entity.Id.Value + " action=" + entity.Animation.ActionKey);
                return;
            }

            EntityPoseState entityPoseState = GetEntityPoseState(entity.Id.Value, bindings.Length);
            entityPoseState.BeginTransitionIfNeeded(
                entity.Animation.ActionActive,
                entity.Animation.AuthorityBindingId,
                entity.Action.ActiveActionId,
                frame.Value,
                Math.Max(
                    CharacterTestDeterministicUnits.SecondsToFrames(entity.Animation.ActionTransitionSeconds),
                    entity.Action.AnimationTransitionFrames));

            physics.World.UpsertBody(new CombatPhysicsBody(entityId, bodyId, rootPosition));
            var compatibilityColliderId = new CombatColliderId(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart);
            if (physics.World.TryGetAabbCollider(bodyId, compatibilityColliderId, out _))
                physics.World.RemoveCollider(bodyId, compatibilityColliderId);
            CharacterTestDeterministicMath.SinCosMilliDegrees(entity.Transform.YawMilliDegrees, out int sinScaled, out int cosScaled);
            Fix64 sin = TrigToFix64(sinScaled);
            Fix64 cos = TrigToFix64(cosScaled);

            bool compoundCompleted = false;
            physics.World.BeginCompoundColliderUpdate(bodyId, bindings.Length);
            try
            {
                for (int i = 0; i < bindings.Length; i++)
                {
                    CharacterTestBodyPartFilterStatus filterStatus = entity.BodyPartState != null
                        ? entity.BodyPartState.GetBodyPartFilterStatus(state.HitZoneMetadata[i].PartId)
                        : CharacterTestBodyPartFilterStatus.Untracked;
                    if (filterStatus == CharacterTestBodyPartFilterStatus.Severed)
                    {
                        // Keeping the original ordinal while omitting the leaf lets
                        // EndUpdate remove the old collider without reindexing any
                        // remaining CombatColliderId.
                        continue;
                    }

                    CharacterBodyColliderRuntimeBinding binding = bindings[i];
                    if (!TryResolvePose(
                            state.Catalog,
                            binding.ColliderId,
                            state.PoseSources,
                            poseSourceCount,
                            entity,
                            state.Package?.Geometry?.AuthorityPoseRig,
                            state.AuthorityPoseCatalog,
                            frame,
                            out CharacterHurtboxBakePose pose))
                    {
                        physics.World.RemoveBody(bodyId);
                        physics.World.CancelCompoundColliderUpdate();
                        PoseSource primary = state.PoseSources[0];
                        ReportOnce(state.Key + "|" + primary.ClipKey + "|" + binding.ColliderId + "|" + primary.LocalFrame,
                            "CHARACTER_HURTBOX_FRAME_MISSING package=" + state.Key + " clip=" + primary.ClipKey + " collider=" + binding.ColliderId + " frame=" + primary.LocalFrame);
                        return;
                    }
                    pose = entityPoseState.ApplyTransitionAndStore(i, pose, frame.Value);
                    CombatColliderId combatColliderId = new CombatColliderId(
                        CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart + i);
                    if (!TryUpsertCompoundPose(
                        physics.World,
                        combatColliderId,
                        pose,
                        sin,
                        cos,
                        binding.Priority,
                        out CombatPhysicsShape localShape))
                    {
                        physics.World.RemoveBody(bodyId);
                        physics.World.CancelCompoundColliderUpdate();
                        InvalidateGeometry(geometry);
                        ReportOnce(
                            state.Key + "|shape|" + binding.ColliderId,
                            "CHARACTER_HURTBOX_SHAPE_UNSUPPORTED package=" + state.Key + " entity=" + entity.Id.Value + " collider=" + binding.ColliderId);
                        return;
                    }

                    GetWorldAabb(localShape, rootPosition, out FixVector3 worldMin, out FixVector3 worldMax);
                    geometry.AddCollider(
                        binding.ColliderId,
                        binding.ColliderLabel,
                        combatColliderId,
                        state.HitZoneMetadata[i].PartId,
                        state.HitZoneMetadata[i].PartLabel,
                        state.HitZoneMetadata[i].HitZoneId,
                        state.HitZoneMetadata[i].HitZoneLabel,
                        binding.Priority,
                        layer: CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer,
                        localShape,
                        worldMin,
                        worldMax);
                }
                physics.World.EndCompoundColliderUpdate();
                compoundCompleted = true;
                _registeredColliderCounts[entity.Id.Value] = activeLeafCount;
                geometry.SortCollidersByStableLogicalId();
                geometry.Commit(
                    frame,
                    physics.World.Revision,
                    NextHurtboxRevision(entity.Id.Value),
                    physics.World.TopologyRevision);
                BuildZoneSnapshot(entity.Id.Value, geometry, state.Package?.Geometry?.DamageZones);
            }
            finally
            {
                if (!compoundCompleted)
                {
                    physics.World.CancelCompoundColliderUpdate();
                    InvalidateGeometry(geometry);
                }
            }
        }

        private static bool TryResolveActiveLeafCount(
            CharacterTestBodyPartStateComponent bodyPartState,
            CharacterTestHurtboxHitZoneMetadata[] metadata,
            int bindingCount,
            out int activeLeafCount)
        {
            activeLeafCount = 0;
            if (bindingCount <= 0 || metadata == null || metadata.Length != bindingCount)
                return false;

            for (int i = 0; i < bindingCount; i++)
            {
                CharacterTestBodyPartFilterStatus status = bodyPartState != null
                    ? bodyPartState.GetBodyPartFilterStatus(metadata[i].PartId)
                    : CharacterTestBodyPartFilterStatus.Untracked;
                if (status == CharacterTestBodyPartFilterStatus.InvalidBinding)
                    return false;
                if (status == CharacterTestBodyPartFilterStatus.Severed)
                    continue;

                activeLeafCount++;
            }

            return true;
        }

        private void BuildZoneSnapshot(
            int entityId,
            CharacterTestDamageGeometrySnapshot geometry,
            CharacterDamageZoneRuntimeBinding[] bindings)
        {
            if (!_zoneSnapshots.TryGetValue(entityId, out CharacterTestDamageZoneSnapshot snapshot))
            {
                snapshot = new CharacterTestDamageZoneSnapshot();
                _zoneSnapshots.Add(entityId, snapshot);
            }
            snapshot.Build(geometry, bindings);
        }

        private static void SyncCompatibilityHurtbox(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterPhysicsRuntime physics,
            CombatBodyId bodyId,
            CombatEntityId entityId,
            CharacterTestDamageGeometrySnapshot geometry)
        {
            FixVector3 halfExtents = physics.GetCharacterHalfExtents(entity.ColliderPosture);
            FixVector3 center = new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.YMillimeters) + physics.GetCharacterCenterOffsetY(entity.ColliderPosture),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.ZMillimeters));
            physics.World.UpsertBody(new CombatPhysicsBody(entityId, bodyId, center));
            physics.World.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart),
                layer: CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer,
                -halfExtents,
                halfExtents));
            CombatColliderId combatColliderId = new CombatColliderId(
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart);
            CombatPhysicsShape localShape = CombatPhysicsShape.Aabb(-halfExtents, halfExtents);
            geometry.AddCollider(
                CharacterTestAuthoredContentIds.D400_Id35,
                "__compatibility__",
                combatColliderId,
                CharacterTestHitZoneDefaults.BodyPartId,
                string.Empty,
                CharacterTestHitZoneDefaults.HitZoneId,
                string.Empty,
                priority: 0,
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer,
                localShape,
                center - halfExtents,
                center + halfExtents);
        }

        private EntityPoseState GetEntityPoseState(int entityId, int colliderCount)
        {
            if (_entityPoseStates.TryGetValue(entityId, out EntityPoseState state)
                && state.ColliderCount == colliderCount)
            {
                return state;
            }

            state = new EntityPoseState(colliderCount);
            _entityPoseStates[entityId] = state;
            return state;
        }

        /// <summary>Configuration/restore boundary: prepares immutable clip/track references for the entity.</summary>
        internal void PrepareEntity(CharacterTestCharacterEntity entity)
        {
            if (entity != null) _preparedEntityPackages[entity.Id.Value] = PreparePackageState(entity);
        }

        private PackageState GetPackageState(CharacterTestCharacterEntity entity)
        {
            if (_preparedEntityPackages.TryGetValue(entity.Id.Value, out PackageState state)) return state;
            state = PreparePackageState(entity);
            _preparedEntityPackages[entity.Id.Value] = state;
            return state;
        }

        private PackageState PreparePackageState(CharacterTestCharacterEntity entity)
        {
            string key = (entity.Identity.GeneratedPackageRoot ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity);
            CharacterHurtboxBakeCatalog catalog = null;
            CharacterSocketBakeCatalog authorityPoseCatalog = null;
            if (package != null && !string.IsNullOrWhiteSpace(package.RootPath))
            {
                string path = Path.Combine(package.RootPath, CharacterHurtboxBakeCatalogJson.DefaultRelativePath);
                if (File.Exists(path))
                {
                    string indexJson = File.ReadAllText(path);
                    if (CharacterHurtboxBakeCatalogBinary.IsV2Index(indexJson))
                    {
                        string binaryRelative = CharacterHurtboxBakeCatalogBinary.ReadBinaryRelativePath(indexJson);
                        string binaryPath = Path.Combine(package.RootPath, binaryRelative.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(binaryPath))
                            catalog = CharacterHurtboxBakeCatalogBinary.Load(indexJson, File.ReadAllBytes(binaryPath), out _);
                    }
                    else
                    {
                        catalog = CharacterHurtboxBakeCatalogJson.Load(indexJson, out _);
                    }
                }

                CharacterTestDefaultBakedWeaponTraceSource.Instance.TryGetPackageSocketCatalog(
                    entity.Identity.GeneratedPackageRoot,
                    out authorityPoseCatalog);
            }
            CharacterRuntimeLocomotionDefinition locomotion = default;
            CharacterRuntimeAnimationResources.TryCreateLocomotionDefinition(entity.Identity.AnimationBinding, NullRuntimeLogger.Instance, out locomotion);
            var state = new PackageState(
                key,
                package,
                catalog,
                authorityPoseCatalog,
                locomotion);
            return state;
        }

        private bool TryResolvePoseSources(CharacterTestCharacterEntity entity, PackageState state, RuntimeFrame frame, out int sourceCount)
        {
            sourceCount = 0;
            CharacterActionConfig action = null;
            _actionPoseRegistry?.TryGetAction(entity.Action.ActiveActionId, out action);
            var animation = CharacterTestCharacterAnimationSyncSystem.ResolveOrdinaryGuardRecoveryPresentation(
                action, entity.Action.LocalFrame, entity.Animation);
            if (animation.ActionActive)
            {
                PreparedPoseClip actionClip = null;
                if (entity.Animation.AuthorityBindingId > 0)
                    state.ActionClips.TryGetValue(entity.Animation.AuthorityBindingId, out actionClip);
                else if (!string.IsNullOrEmpty(entity.Animation.ActionKey))
                    state.ActionClipsByKey.TryGetValue(entity.Animation.ActionKey, out actionClip);

                if (actionClip != null)
                {
                    state.PoseSources[0] = new PoseSource(actionClip, (int)Math.Max(0, CharacterTestCharacterAnimationSyncSystem.ResolveOrdinaryGuardClipFrame(action, entity.Action.LocalFrame)), 1000, loop: false);
                    sourceCount = 1;
                    return true;
                }
            }

            int worldFrame = frame.Value <= 0 ? 0 : (int)Math.Min(int.MaxValue, frame.Value);
            if (state.Locomotion.BlendKind == CharacterAnimationBlendKind.Blend1D && state.Locomotion.Blend1D != null)
                return ResolveBlend1D(state, entity.Animation.LocomotionSpeed01, worldFrame, state.PoseSources, out sourceCount);
            if (state.Locomotion.BlendKind == CharacterAnimationBlendKind.Blend2D && state.Locomotion.Blend2D != null)
                return ResolveBlend2D(state, entity.Animation.LocomotionBlend, worldFrame, out sourceCount);
            return false;
        }

        private static bool ResolveBlend1D(PackageState state, float value01, int frame, PoseSource[] sources, out int sourceCount)
        {
            sourceCount = 0;
            MxAnimationBlend1DDefinition blend = state.Locomotion.Blend1D;
            IReadOnlyList<MxAnimationBlend1DPoint> points = blend.Points;
            if (points.Count == 0) return false;
            int value = (int)Math.Round(Math.Max(0f, Math.Min(1f, value01)) * blend.ParameterScale);
            if (value <= points[0].Threshold) { sources[0] = new PoseSource(state.Blend1DClips[0], frame, 1000, loop: true); sourceCount = 1; return true; }
            int last = points.Count - 1;
            if (value >= points[last].Threshold) { sources[0] = new PoseSource(state.Blend1DClips[last], frame, 1000, loop: true); sourceCount = 1; return true; }
            for (int i = 0; i < last; i++)
            {
                if (value < points[i].Threshold || value > points[i + 1].Threshold) continue;
                int span = Math.Max(1, points[i + 1].Threshold - points[i].Threshold);
                int upperWeight = (value - points[i].Threshold) * 1000 / span;
                sources[0] = new PoseSource(state.Blend1DClips[i], frame, 1000 - upperWeight, loop: true);
                sources[1] = new PoseSource(state.Blend1DClips[i + 1], frame, upperWeight, loop: true);
                sourceCount = upperWeight > 0 ? 2 : 1;
                return true;
            }
            return false;
        }

        private static bool ResolveBlend2D(PackageState state, UnityEngine.Vector2 value, int frame, out int sourceCount)
        {
            sourceCount = 0;
            MxAnimationBlend2DDefinition blend = state.Locomotion.Blend2D;
            int x = (int)Math.Round(value.x * blend.ParameterXScale);
            int y = (int)Math.Round(value.y * blend.ParameterYScale);
            MxAnimationBlend2DCalculator.EvaluatePrepared(blend, x, y, state.Blend2DBuffer);
            IReadOnlyList<MxAnimationBlend2DWeight> weights = state.Blend2DBuffer.Weights;
            int remainingWeight = 1000;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i].Weight <= 0f) continue;
                int weightMilli = (int)Math.Round(weights[i].Weight * 1000f);
                if (weightMilli <= 0) continue;
                weightMilli = Math.Min(remainingWeight, weightMilli);
                state.PoseSources[sourceCount++] = new PoseSource(state.Blend2DClips[PointKey(weights[i].X, weights[i].Y)], frame, weightMilli, weights[i].Loop);
                remainingWeight -= weightMilli;
            }
            if (sourceCount == 0) return false;
            if (remainingWeight != 0)
            {
                PoseSource last = state.PoseSources[sourceCount - 1];
                state.PoseSources[sourceCount - 1] = new PoseSource(last.Clip, last.LocalFrame, last.WeightMilli + remainingWeight, last.Loop);
            }
            return sourceCount > 0;
        }

        private static bool TryResolvePose(
            CharacterHurtboxBakeCatalog catalog,
            int colliderId,
            PoseSource[] sources,
            int sourceCount,
            CharacterTestCharacterEntity entity,
            CharacterAuthorityPoseRigRuntimeBinding rig,
            CharacterSocketBakeCatalog authorityPoseCatalog,
            RuntimeFrame frame,
            out CharacterHurtboxBakePose pose)
        {
            if (sourceCount <= 0 || !TryGetLoopedPose(catalog, sources[0], colliderId, out CharacterHurtboxBakePose firstPose)) { pose = default; return false; }
            firstPose = ApplyAuthorityPoseAttachment(
                firstPose,
                colliderId,
                sources[0],
                entity,
                rig,
                authorityPoseCatalog,
                frame);
            if (sourceCount == 1) { pose = firstPose; return true; }
            long ax = 0, ay = 0, az = 0, bx = 0, by = 0, bz = 0, ex = 0, ey = 0, ez = 0;
            long xx = 0, xy = 0, xz = 0, yx = 0, yy = 0, yz = 0, zx = 0, zy = 0, zz = 0, radius = 0;
            for (int i = 0; i < sourceCount; i++)
            {
                if (!TryGetLoopedPose(catalog, sources[i], colliderId, out CharacterHurtboxBakePose sample) || sample.ShapeKind != firstPose.ShapeKind) { pose = default; return false; }
                sample = ApplyAuthorityPoseAttachment(
                    sample,
                    colliderId,
                    sources[i],
                    entity,
                    rig,
                    authorityPoseCatalog,
                    frame);
                int w = sources[i].WeightMilli;
                ax += sample.PointA.X * w; ay += sample.PointA.Y * w; az += sample.PointA.Z * w;
                bx += sample.PointB.X * w; by += sample.PointB.Y * w; bz += sample.PointB.Z * w;
                ex += sample.HalfExtents.X * w; ey += sample.HalfExtents.Y * w; ez += sample.HalfExtents.Z * w;
                xx += sample.AxisX.X * w; xy += sample.AxisX.Y * w; xz += sample.AxisX.Z * w;
                yx += sample.AxisY.X * w; yy += sample.AxisY.Y * w; yz += sample.AxisY.Z * w;
                zx += sample.AxisZ.X * w; zy += sample.AxisZ.Y * w; zz += sample.AxisZ.Z * w;
                radius += sample.Radius * w;
            }
            pose = new CharacterHurtboxBakePose(
                firstPose.ShapeKind,
                new CharacterHurtboxBakeVector3(ax / 1000, ay / 1000, az / 1000),
                new CharacterHurtboxBakeVector3(bx / 1000, by / 1000, bz / 1000),
                new CharacterHurtboxBakeVector3(ex / 1000, ey / 1000, ez / 1000),
                new CharacterHurtboxBakeVector3(xx / 1000, xy / 1000, xz / 1000),
                new CharacterHurtboxBakeVector3(yx / 1000, yy / 1000, yz / 1000),
                new CharacterHurtboxBakeVector3(zx / 1000, zy / 1000, zz / 1000),
                radius / 1000);
            return true;
        }

        private static CharacterHurtboxBakePose ApplyAuthorityPoseAttachment(
            CharacterHurtboxBakePose pose,
            int colliderId,
            PoseSource source,
            CharacterTestCharacterEntity entity,
            CharacterAuthorityPoseRigRuntimeBinding rig,
            CharacterSocketBakeCatalog catalog,
            RuntimeFrame frame)
        {
            if (!CharacterTestAuthorityPoseConsumerMath.TryResolveAttachmentDelta(
                    entity,
                    rig,
                    source.Clip.Socket,
                    CharacterAuthorityPoseRigAttachmentKind.Collider,
                    colliderId,
                    source.LocalFrame,
                    source.Loop,
                    frame,
                    out CharacterTestAuthorityPoseAttachmentDelta delta))
            {
                return pose;
            }

            CharacterHurtboxBakeVector3 pointA = ToBakedPosition(
                delta.TransformPoint(ToPosition(pose.PointA)));
            CharacterHurtboxBakeVector3 pointB = pose.PointB;
            CharacterHurtboxBakeVector3 axisX = pose.AxisX;
            CharacterHurtboxBakeVector3 axisY = pose.AxisY;
            CharacterHurtboxBakeVector3 axisZ = pose.AxisZ;
            if (pose.ShapeKind == CharacterHurtboxBakeShapeKind.Capsule)
            {
                pointB = ToBakedPosition(delta.TransformPoint(ToPosition(pose.PointB)));
            }
            else if (pose.ShapeKind == CharacterHurtboxBakeShapeKind.Obb)
            {
                axisX = ToBakedAxis(delta.TransformAxis(ToAxis(pose.AxisX)));
                axisY = ToBakedAxis(delta.TransformAxis(ToAxis(pose.AxisY)));
                axisZ = ToBakedAxis(delta.TransformAxis(ToAxis(pose.AxisZ)));
            }

            return new CharacterHurtboxBakePose(
                pose.ShapeKind,
                pointA,
                pointB,
                pose.HalfExtents,
                axisX,
                axisY,
                axisZ,
                pose.Radius);
        }

        private static bool TryGetLoopedPose(CharacterHurtboxBakeCatalog catalog, PoseSource source, int colliderId, out CharacterHurtboxBakePose pose)
        {
            if (source.Clip == null || source.Clip.Hurtbox == null || !source.Clip.Hurtbox.TryGetTrack(colliderId, out CharacterHurtboxBakeTrack track) || track.FrameCount <= 0) { pose = default; return false; }
            if (!source.Loop && source.LocalFrame >= track.FrameCount) { pose = default; return false; }
            int localFrame = source.Loop ? source.LocalFrame % track.FrameCount : source.LocalFrame;
            return track.TryGetPose(localFrame, out pose);
        }

        private static bool TryUpsertCompoundPose(
            CombatPhysicsWorld world,
            CombatColliderId colliderId,
            CharacterHurtboxBakePose pose,
            Fix64 sin,
            Fix64 cos,
            int priority,
            out CombatPhysicsShape shape)
        {
            FixVector3 a = Rotate(ToPosition(pose.PointA), sin, cos);
            FixVector3 b = Rotate(ToPosition(pose.PointB), sin, cos);
            Fix64 radius = ToMillimetres(pose.Radius);
            switch (pose.ShapeKind)
            {
                case CharacterHurtboxBakeShapeKind.Sphere:
                    shape = CombatPhysicsShape.Sphere(a, radius);
                    world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(colliderId, CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer, shape, priority: priority));
                    return true;
                case CharacterHurtboxBakeShapeKind.Capsule:
                    shape = CombatPhysicsShape.Capsule(a, b, radius);
                    world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(colliderId, CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer, shape, priority: priority));
                    return true;
                case CharacterHurtboxBakeShapeKind.Obb:
                    shape = CombatPhysicsShape.Obb(
                        a,
                        ToPosition(pose.HalfExtents),
                        Rotate(ToAxis(pose.AxisX), sin, cos),
                        Rotate(ToAxis(pose.AxisY), sin, cos),
                        Rotate(ToAxis(pose.AxisZ), sin, cos));
                    world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(colliderId, CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer, shape, priority: priority));
                    return true;
                default:
                    shape = default;
                    return false;
            }
        }

        private static void GetWorldAabb(
            CombatPhysicsShape shape,
            FixVector3 bodyPosition,
            out FixVector3 min,
            out FixVector3 max)
        {
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Aabb:
                    min = bodyPosition + shape.Min;
                    max = bodyPosition + shape.Max;
                    return;
                case CombatPhysicsShapeKind.Sphere:
                    FixVector3 sphereRadius = new FixVector3(shape.Radius, shape.Radius, shape.Radius);
                    min = bodyPosition + shape.Center - sphereRadius;
                    max = bodyPosition + shape.Center + sphereRadius;
                    return;
                case CombatPhysicsShapeKind.Capsule:
                    FixVector3 capsuleRadius = new FixVector3(shape.Radius, shape.Radius, shape.Radius);
                    min = bodyPosition + Min(shape.PointA, shape.PointB) - capsuleRadius;
                    max = bodyPosition + Max(shape.PointA, shape.PointB) + capsuleRadius;
                    return;
                case CombatPhysicsShapeKind.Obb:
                    Fix64 extentX = Abs(shape.AxisX.X) * shape.HalfExtents.X
                        + Abs(shape.AxisY.X) * shape.HalfExtents.Y
                        + Abs(shape.AxisZ.X) * shape.HalfExtents.Z;
                    Fix64 extentY = Abs(shape.AxisX.Y) * shape.HalfExtents.X
                        + Abs(shape.AxisY.Y) * shape.HalfExtents.Y
                        + Abs(shape.AxisZ.Y) * shape.HalfExtents.Z;
                    Fix64 extentZ = Abs(shape.AxisX.Z) * shape.HalfExtents.X
                        + Abs(shape.AxisY.Z) * shape.HalfExtents.Y
                        + Abs(shape.AxisZ.Z) * shape.HalfExtents.Z;
                    FixVector3 center = bodyPosition + shape.Center;
                    FixVector3 extent = new FixVector3(extentX, extentY, extentZ);
                    min = center - extent;
                    max = center + extent;
                    return;
                default:
                    min = FixVector3.Zero;
                    max = FixVector3.Zero;
                    return;
            }
        }

        private static FixVector3 Min(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Min(left.X, right.X),
                Fix64.Min(left.Y, right.Y),
                Fix64.Min(left.Z, right.Z));
        }

        private static FixVector3 Max(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Max(left.X, right.X),
                Fix64.Max(left.Y, right.Y),
                Fix64.Max(left.Z, right.Z));
        }

        private static Fix64 Abs(Fix64 value)
        {
            return value < Fix64.Zero ? -value : value;
        }

        private static FixVector3 Rotate(FixVector3 value, Fix64 sin, Fix64 cos) => new FixVector3(value.X * cos + value.Z * sin, value.Y, -value.X * sin + value.Z * cos);
        internal static Fix64 TrigToFix64(int value) => Fix64.FromRaw((long)value * Fix64.Scale / CharacterTestDeterministicMath.TrigScale);
        private static FixVector3 ToPosition(CharacterHurtboxBakeVector3 value) => new FixVector3(ToMillimetres(value.X), ToMillimetres(value.Y), ToMillimetres(value.Z));
        private static FixVector3 ToAxis(CharacterHurtboxBakeVector3 value) => new FixVector3(ToAxis(value.X), ToAxis(value.Y), ToAxis(value.Z));
        private static CharacterHurtboxBakeVector3 ToBakedPosition(FixVector3 value) =>
            new CharacterHurtboxBakeVector3(
                value.X.RawValue * CharacterHurtboxBakePose.MillimetresPerMetre / Fix64.Scale,
                value.Y.RawValue * CharacterHurtboxBakePose.MillimetresPerMetre / Fix64.Scale,
                value.Z.RawValue * CharacterHurtboxBakePose.MillimetresPerMetre / Fix64.Scale);
        private static CharacterHurtboxBakeVector3 ToBakedAxis(FixVector3 value) =>
            new CharacterHurtboxBakeVector3(
                value.X.RawValue * CharacterHurtboxBakePose.AxisQuantizationScale / Fix64.Scale,
                value.Y.RawValue * CharacterHurtboxBakePose.AxisQuantizationScale / Fix64.Scale,
                value.Z.RawValue * CharacterHurtboxBakePose.AxisQuantizationScale / Fix64.Scale);
        private static Fix64 ToMillimetres(long value) => Fix64.FromRaw(value * Fix64.Scale / CharacterHurtboxBakePose.MillimetresPerMetre);
        private static Fix64 ToAxis(long value) => Fix64.FromRaw(value * Fix64.Scale / CharacterHurtboxBakePose.AxisQuantizationScale);
        private static CharacterHurtboxBakePose LerpPose(CharacterHurtboxBakePose from, CharacterHurtboxBakePose to, int weightMilli)
        {
            if (from.ShapeKind != to.ShapeKind) return to;
            return new CharacterHurtboxBakePose(
                to.ShapeKind,
                LerpVector(from.PointA, to.PointA, weightMilli),
                LerpVector(from.PointB, to.PointB, weightMilli),
                LerpVector(from.HalfExtents, to.HalfExtents, weightMilli),
                LerpVector(from.AxisX, to.AxisX, weightMilli),
                LerpVector(from.AxisY, to.AxisY, weightMilli),
                LerpVector(from.AxisZ, to.AxisZ, weightMilli),
                LerpLong(from.Radius, to.Radius, weightMilli));
        }

        private static CharacterHurtboxBakeVector3 LerpVector(CharacterHurtboxBakeVector3 from, CharacterHurtboxBakeVector3 to, int weightMilli) =>
            new CharacterHurtboxBakeVector3(
                LerpLong(from.X, to.X, weightMilli),
                LerpLong(from.Y, to.Y, weightMilli),
                LerpLong(from.Z, to.Z, weightMilli));

        private static long LerpLong(long from, long to, int weightMilli) => from + ((to - from) * weightMilli / 1000);
        private void ReportOnce(string key, string message) { if (_reportedIssues.Add(key)) UnityEngine.Debug.LogError(message); }

        private static CharacterTestHurtboxHitZoneMetadata[] ResolveHitZoneMetadata(
            CharacterImportedPackage package)
        {
            CharacterBodyColliderRuntimeBinding[] bindings =
                package?.Geometry?.BodyColliders ?? Array.Empty<CharacterBodyColliderRuntimeBinding>();
            if (bindings.Length == 0)
                return Array.Empty<CharacterTestHurtboxHitZoneMetadata>();

            CharacterImportedConfigSet configs = package?.Configs;
            CharacterConfig character = configs?.Characters != null && configs.Characters.Length > 0
                ? configs.Characters[0]
                : null;
            CharacterBodyProfileConfig bodyProfile = character == null
                ? null
                : configs.FindBodyProfile(character.BodyProfileId);
            var metadata = new CharacterTestHurtboxHitZoneMetadata[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                CharacterBodyColliderRuntimeBinding binding = bindings[i];
                BodyPartHitZoneResolveResult result = BodyPartHitZoneResolver.Resolve(
                    bodyProfile,
                    configs?.BodyParts ?? Array.Empty<CharacterBodyPartConfig>(),
                    binding.HitZoneId);
                if (!result.IsSuccess)
                {
                    metadata[i] = new CharacterTestHurtboxHitZoneMetadata(
                        CharacterTestHitZoneDefaults.HitZoneId,
                        binding.HitZoneLabel,
                        CharacterTestHitZoneDefaults.BodyPartId,
                        binding.PartLabel);
                    continue;
                }

                metadata[i] = new CharacterTestHurtboxHitZoneMetadata(
                    result.HitZoneId,
                    binding.HitZoneLabel,
                    result.PartId <= 0 ? binding.PartId : result.PartId,
                    binding.PartLabel);
            }

            return metadata;
        }

        private readonly struct CharacterTestHurtboxHitZoneMetadata
        {
            public CharacterTestHurtboxHitZoneMetadata(
                int hitZoneId,
                string hitZoneLabel,
                int partId,
                string partLabel)
            {
                HitZoneId = hitZoneId;
                HitZoneLabel = hitZoneLabel ?? string.Empty;
                PartId = partId;
                PartLabel = partLabel ?? string.Empty;
            }

            public int HitZoneId { get; }
            public string HitZoneLabel { get; }
            public int PartId { get; }
            public string PartLabel { get; }
        }

        private sealed class EntityPoseState
        {
            private readonly CharacterHurtboxBakePose[] _lastPoses;
            private readonly CharacterHurtboxBakePose[] _transitionFromPoses;
            private int _bindingId;
            private CharacterActionId _actionId;
            private bool _actionActive;
            private bool _stateInitialized;
            private long _transitionStartFrame;
            private int _transitionFrames;
            private bool _hasPoses;

            public EntityPoseState(int colliderCount)
            {
                ColliderCount = Math.Max(0, colliderCount);
                _lastPoses = new CharacterHurtboxBakePose[ColliderCount];
                _transitionFromPoses = new CharacterHurtboxBakePose[ColliderCount];
            }

            public int ColliderCount { get; }

            public void BeginTransitionIfNeeded(
                bool actionActive,
                int bindingId,
                CharacterActionId actionId,
                long frame,
                int transitionFrames)
            {
                if (_stateInitialized
                    && _actionActive == actionActive
                    && _bindingId == bindingId
                    && _actionId == actionId)
                {
                    return;
                }
                if (_hasPoses && transitionFrames > 0)
                {
                    Array.Copy(_lastPoses, _transitionFromPoses, ColliderCount);
                    _transitionStartFrame = Math.Max(0L, frame);
                    _transitionFrames = transitionFrames;
                }
                else
                {
                    _transitionFrames = 0;
                }
                _actionActive = actionActive;
                _bindingId = bindingId;
                _actionId = actionId;
                _stateInitialized = true;
            }

            public CharacterHurtboxBakePose ApplyTransitionAndStore(int colliderIndex, CharacterHurtboxBakePose current, long frame)
            {
                CharacterHurtboxBakePose resolved = current;
                if (_transitionFrames > 0)
                {
                    long elapsed = Math.Max(0L, frame - _transitionStartFrame);
                    int weightMilli = elapsed >= _transitionFrames
                        ? 1000
                        : (int)(elapsed * 1000L / _transitionFrames);
                    resolved = LerpPose(_transitionFromPoses[colliderIndex], current, weightMilli);
                    if (weightMilli >= 1000) _transitionFrames = 0;
                }

                _lastPoses[colliderIndex] = resolved;
                _hasPoses = true;
                return resolved;
            }
        }

        private static long PointKey(int x, int y) => ((long)x << 32) | (uint)y;

        private sealed class PreparedPoseClip
        {
            public PreparedPoseClip(CharacterPreparedHurtboxClip hurtbox, CharacterPreparedSocketClip socket) { Hurtbox = hurtbox; Socket = socket; }
            public CharacterPreparedHurtboxClip Hurtbox { get; }
            public CharacterPreparedSocketClip Socket { get; }
        }

        private readonly struct PoseSource
        {
            public PoseSource(PreparedPoseClip clip, int localFrame, int weightMilli, bool loop) { Clip = clip; LocalFrame = Math.Max(0, localFrame); WeightMilli = Math.Max(0, Math.Min(1000, weightMilli)); Loop = loop; }
            public PreparedPoseClip Clip { get; }
            public string ClipKey => Clip?.Hurtbox?.SourceClipLabel ?? string.Empty;
            public int LocalFrame { get; }
            public int WeightMilli { get; }
            public bool Loop { get; }
            public bool IsValid => Clip != null;
        }

        private sealed class PackageState
        {
            public PackageState(
                string key,
                CharacterImportedPackage package,
                CharacterHurtboxBakeCatalog catalog,
                CharacterSocketBakeCatalog authorityPoseCatalog,
                CharacterRuntimeLocomotionDefinition locomotion)
            {
                Key = key;
                Package = package;
                Catalog = catalog;
                AuthorityPoseCatalog = authorityPoseCatalog;
                Locomotion = locomotion;
                Blend2DBuffer = new MxAnimationBlend2DEvaluationBuffer();
                PoseSources = new PoseSource[Math.Max(2, locomotion.Blend2D?.Points.Count ?? 0)];
                HitZoneMetadata = ResolveHitZoneMetadata(package);
                var prepared = new Dictionary<string, PreparedPoseClip>(StringComparer.Ordinal);
                var actions = locomotion.AnimationSet?.Actions;
                for (int i = 0; actions != null && i < actions.Count; i++)
                {
                    MxAnimationActionBinding binding = actions[i];
                    if (binding != null)
                    {
                        PreparedPoseClip poseClip = Prepare(prepared, binding.Clip.Id);
                        if (binding.BindingId > 0 && !ActionClips.ContainsKey(binding.BindingId))
                            ActionClips.Add(binding.BindingId, poseClip);
                        if (!string.IsNullOrWhiteSpace(binding.ActionKey) && !ActionClipsByKey.ContainsKey(binding.ActionKey))
                            ActionClipsByKey.Add(binding.ActionKey, poseClip);
                    }
                }
                var points1D = locomotion.Blend1D?.Points;
                Blend1DClips = new PreparedPoseClip[points1D?.Count ?? 0];
                for (int i = 0; points1D != null && i < points1D.Count; i++)
                    Blend1DClips[i] = Prepare(prepared, points1D[i].ClipKey.Id);
                var points2D = locomotion.Blend2D?.Points;
                for (int i = 0; points2D != null && i < points2D.Count; i++)
                    Blend2DClips.Add(PointKey(points2D[i].X, points2D[i].Y), Prepare(prepared, points2D[i].ClipKey.Id));
            }

            private PreparedPoseClip Prepare(Dictionary<string, PreparedPoseClip> prepared, string resourceLabel)
            {
                if (string.IsNullOrWhiteSpace(resourceLabel)) return null;
                if (!prepared.TryGetValue(resourceLabel, out PreparedPoseClip clip))
                {
                    clip = new PreparedPoseClip(Catalog?.PrepareClip(resourceLabel), AuthorityPoseCatalog?.PrepareClip(resourceLabel));
                    prepared.Add(resourceLabel, clip);
                }
                return clip;
            }

            public Dictionary<int, PreparedPoseClip> ActionClips { get; } = new Dictionary<int, PreparedPoseClip>();
            public Dictionary<string, PreparedPoseClip> ActionClipsByKey { get; } = new Dictionary<string, PreparedPoseClip>(StringComparer.Ordinal);
            public PreparedPoseClip[] Blend1DClips { get; }
            public Dictionary<long, PreparedPoseClip> Blend2DClips { get; } = new Dictionary<long, PreparedPoseClip>();

            public string Key { get; }
            public CharacterImportedPackage Package { get; }
            public CharacterHurtboxBakeCatalog Catalog { get; }
            public CharacterSocketBakeCatalog AuthorityPoseCatalog { get; }
            public CharacterRuntimeLocomotionDefinition Locomotion { get; }
            public MxAnimationBlend2DEvaluationBuffer Blend2DBuffer { get; }
            public PoseSource[] PoseSources { get; }
            public CharacterTestHurtboxHitZoneMetadata[] HitZoneMetadata { get; }
        }
    }
}

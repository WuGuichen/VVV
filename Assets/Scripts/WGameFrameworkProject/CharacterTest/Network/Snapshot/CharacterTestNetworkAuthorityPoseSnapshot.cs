using System;
using System.Collections.Generic;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Formal schema-32 future-affecting terrain IK authority for one entity.</summary>
    /// <remarks>LowFreqAlloc: snapshot, rollback, replay, and late-join paths only.</remarks>
    public sealed class CharacterTestNetworkAuthorityPoseSnapshot
    {
        public int EntityId { get; set; }
        public bool IsConfigured { get; set; }
        public long LastPublishedRevision { get; set; }
        public int DefinitionRevision { get; set; }
        public string DefinitionContentHash { get; set; } = string.Empty;
        public int BaseSourceRevision { get; set; }
        public string BaseSourceContentHash { get; set; } = string.Empty;
        public CharacterTestNetworkAuthorityPoseLimbSnapshot[] Limbs { get; set; } =
            Array.Empty<CharacterTestNetworkAuthorityPoseLimbSnapshot>();

        internal void NormalizeCanonical()
        {
            DefinitionContentHash ??= string.Empty;
            BaseSourceContentHash ??= string.Empty;
            Limbs ??= Array.Empty<CharacterTestNetworkAuthorityPoseLimbSnapshot>();
            for (int i = 0; i < Limbs.Length; i++)
                Limbs[i]?.NormalizeCanonical();
            Array.Sort(Limbs, CompareLimbs);
        }

        internal CharacterTestNetworkAuthorityPoseSnapshot CloneCanonical()
        {
            var clone = new CharacterTestNetworkAuthorityPoseSnapshot
            {
                EntityId = EntityId,
                IsConfigured = IsConfigured,
                LastPublishedRevision = LastPublishedRevision,
                DefinitionRevision = DefinitionRevision,
                DefinitionContentHash = DefinitionContentHash ?? string.Empty,
                BaseSourceRevision = BaseSourceRevision,
                BaseSourceContentHash = BaseSourceContentHash ?? string.Empty,
                Limbs = CharacterTestNetworkAuthorityPoseLimbSnapshot.CloneArray(Limbs)
            };
            clone.NormalizeCanonical();
            return clone;
        }

        private static int CompareLimbs(
            CharacterTestNetworkAuthorityPoseLimbSnapshot left,
            CharacterTestNetworkAuthorityPoseLimbSnapshot right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            int compare = left.ProbeId.CompareTo(right.ProbeId);
            return compare != 0 ? compare : left.Ordinal.CompareTo(right.Ordinal);
        }
    }

    /// <summary>Schema-32 history that can affect a later terrain IK result.</summary>
    public sealed class CharacterTestNetworkAuthorityPoseLimbSnapshot
    {
        public int Ordinal { get; set; }
        public int ProbeId { get; set; }
        public int IkBindingId { get; set; }
        public int PartStateId { get; set; }
        public long LastValidFrame { get; set; }
        public int ConsecutiveMissCount { get; set; }
        public long LastValidTargetXRaw { get; set; }
        public long LastValidTargetYRaw { get; set; }
        public long LastValidTargetZRaw { get; set; }
        public long LastValidNormalXRaw { get; set; }
        public long LastValidNormalYRaw { get; set; }
        public long LastValidNormalZRaw { get; set; }
        public int SurfaceEntityId { get; set; }
        public int SurfaceBodyId { get; set; }
        public int SurfaceColliderId { get; set; }
        public int SurfaceLeafId { get; set; }
        public int SurfaceShapeKind { get; set; }
        public int SurfaceMaterialKind { get; set; }
        public long SurfaceFrictionRaw { get; set; }
        public long SurfaceElasticityRaw { get; set; }
        public long SurfaceFixedBounceHeightRaw { get; set; }
        public long CurrentWeightRaw { get; set; }

        internal void NormalizeCanonical()
        {
        }

        internal static CharacterTestNetworkAuthorityPoseLimbSnapshot[] CloneArray(
            CharacterTestNetworkAuthorityPoseLimbSnapshot[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestNetworkAuthorityPoseLimbSnapshot>();
            var result = new CharacterTestNetworkAuthorityPoseLimbSnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CharacterTestNetworkAuthorityPoseLimbSnapshot value = source[i];
                if (value == null) continue;
                result[i] = new CharacterTestNetworkAuthorityPoseLimbSnapshot
                {
                    Ordinal = value.Ordinal,
                    ProbeId = value.ProbeId,
                    IkBindingId = value.IkBindingId,
                    PartStateId = value.PartStateId,
                    LastValidFrame = value.LastValidFrame,
                    ConsecutiveMissCount = value.ConsecutiveMissCount,
                    LastValidTargetXRaw = value.LastValidTargetXRaw,
                    LastValidTargetYRaw = value.LastValidTargetYRaw,
                    LastValidTargetZRaw = value.LastValidTargetZRaw,
                    LastValidNormalXRaw = value.LastValidNormalXRaw,
                    LastValidNormalYRaw = value.LastValidNormalYRaw,
                    LastValidNormalZRaw = value.LastValidNormalZRaw,
                    SurfaceEntityId = value.SurfaceEntityId,
                    SurfaceBodyId = value.SurfaceBodyId,
                    SurfaceColliderId = value.SurfaceColliderId,
                    SurfaceLeafId = value.SurfaceLeafId,
                    SurfaceShapeKind = value.SurfaceShapeKind,
                    SurfaceMaterialKind = value.SurfaceMaterialKind,
                    SurfaceFrictionRaw = value.SurfaceFrictionRaw,
                    SurfaceElasticityRaw = value.SurfaceElasticityRaw,
                    SurfaceFixedBounceHeightRaw = value.SurfaceFixedBounceHeightRaw,
                    CurrentWeightRaw = value.CurrentWeightRaw
                };
            }
            return result;
        }
    }

    internal readonly struct CharacterTestAuthorityPoseNetworkRestorePlan
    {
        internal CharacterTestAuthorityPoseNetworkRestorePlan(
            CharacterTestTerrainIkStateComponent component,
            CharacterTestTerrainIkLimbState[] limbs,
            long lastPublishedRevision,
            bool legacyDefaults)
        {
            Component = component;
            Limbs = limbs;
            LastPublishedRevision = lastPublishedRevision;
            LegacyDefaults = legacyDefaults;
        }

        internal CharacterTestTerrainIkStateComponent Component { get; }
        internal CharacterTestTerrainIkLimbState[] Limbs { get; }
        internal long LastPublishedRevision { get; }
        internal bool LegacyDefaults { get; }

        internal void Commit()
        {
            if (LegacyDefaults)
                Component.ResetLegacyNetworkState();
            else
                Component.CommitNetworkRestore(Limbs, LastPublishedRevision);
        }
    }

    internal static class CharacterTestNetworkAuthorityPoseSnapshotCodec
    {
        internal static CharacterTestNetworkAuthorityPoseSnapshot[] Capture(
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            int count = entities?.Count ?? 0;
            if (count == 0)
                return Array.Empty<CharacterTestNetworkAuthorityPoseSnapshot>();
            var result = new CharacterTestNetworkAuthorityPoseSnapshot[count];
            for (int i = 0; i < count; i++)
                result[i] = entities[i].TerrainIkState.CaptureNetworkSnapshot();
            Array.Sort(result, CompareEntities);
            return result;
        }

        internal static CharacterTestNetworkAuthorityPoseSnapshot[] NormalizeCanonicalArray(
            CharacterTestNetworkAuthorityPoseSnapshot[] source)
        {
            source ??= Array.Empty<CharacterTestNetworkAuthorityPoseSnapshot>();
            for (int i = 0; i < source.Length; i++)
                source[i]?.NormalizeCanonical();
            Array.Sort(source, CompareEntities);
            return source;
        }

        internal static CharacterTestNetworkAuthorityPoseSnapshot[] CloneCanonicalArray(
            CharacterTestNetworkAuthorityPoseSnapshot[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestNetworkAuthorityPoseSnapshot>();
            var result = new CharacterTestNetworkAuthorityPoseSnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = source[i]?.CloneCanonical();
            return NormalizeCanonicalArray(result);
        }

        internal static bool TryValidateDocumentShape(
            long snapshotFrame,
            CharacterTestNetworkCharacterSnapshot[] characters,
            CharacterTestNetworkAuthorityPoseSnapshot[] snapshots,
            out string failure)
        {
            failure = string.Empty;
            characters ??= Array.Empty<CharacterTestNetworkCharacterSnapshot>();
            snapshots ??= Array.Empty<CharacterTestNetworkAuthorityPoseSnapshot>();
            if (characters.Length != snapshots.Length)
            {
                failure = "CharacterTest authority-pose snapshot entity count does not match characters.";
                return false;
            }

            int previousEntityId = 0;
            for (int i = 0; i < snapshots.Length; i++)
            {
                CharacterTestNetworkCharacterSnapshot character = characters[i];
                CharacterTestNetworkAuthorityPoseSnapshot snapshot = snapshots[i];
                if (character == null
                    || snapshot == null
                    || character.EntityId <= previousEntityId
                    || snapshot.EntityId != character.EntityId
                    || !TryValidateSnapshot(snapshotFrame, snapshot))
                {
                    failure = "CharacterTest authority-pose snapshot source/history shape is invalid.";
                    return false;
                }
                previousEntityId = character.EntityId;
            }
            return true;
        }

        internal static bool TryPrepareCurrentAuthority(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestNetworkAuthorityPoseSnapshot[] snapshots,
            bool legacyDefaults,
            out CharacterTestAuthorityPoseNetworkRestorePlan[] plans,
            out string failure)
        {
            failure = string.Empty;
            int count = entities?.Count ?? 0;
            snapshots ??= Array.Empty<CharacterTestNetworkAuthorityPoseSnapshot>();
            if (!legacyDefaults && snapshots.Length != count)
            {
                plans = Array.Empty<CharacterTestAuthorityPoseNetworkRestorePlan>();
                failure = "CharacterTest authority-pose snapshot does not cover the restored entity set.";
                return false;
            }

            plans = new CharacterTestAuthorityPoseNetworkRestorePlan[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                CharacterTestTerrainIkStateComponent component = entity?.TerrainIkState;
                if (component == null)
                {
                    plans = Array.Empty<CharacterTestAuthorityPoseNetworkRestorePlan>();
                    failure = "CharacterTest restored entity is missing terrain IK authority.";
                    return false;
                }
                if (legacyDefaults)
                {
                    plans[i] = component.PrepareLegacyNetworkDefaults();
                    continue;
                }
                if (snapshots[i] == null
                    || snapshots[i].EntityId != entity.Id.Value
                    || !component.TryPrepareNetworkRestore(
                        snapshots[i],
                        out plans[i],
                        out failure))
                {
                    plans = Array.Empty<CharacterTestAuthorityPoseNetworkRestorePlan>();
                    if (string.IsNullOrEmpty(failure))
                        failure = "CharacterTest authority-pose snapshot entity order mismatch.";
                    return false;
                }
            }
            return true;
        }

        internal static void Commit(CharacterTestAuthorityPoseNetworkRestorePlan[] plans)
        {
            plans ??= Array.Empty<CharacterTestAuthorityPoseNetworkRestorePlan>();
            for (int i = 0; i < plans.Length; i++)
                plans[i].Commit();
        }

        internal static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int ownedEntityId = 0,
            bool excludeOwnedEntity = false)
        {
            int count = entities?.Count ?? 0;
            int included = 0;
            for (int i = 0; i < count; i++)
            {
                bool isOwned = entities[i] != null && entities[i].Id.Value == ownedEntityId;
                if (ownedEntityId <= 0 || isOwned != excludeOwnedEntity)
                    included++;
            }

            accumulator.BeginArray(NetworkAuthorityPoseSnapshotHashFields.Entities, included);
            int outputIndex = 0;
            for (int i = 0; i < count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                bool isOwned = entity != null && entity.Id.Value == ownedEntityId;
                if (ownedEntityId > 0 && isOwned == excludeOwnedEntity)
                    continue;
                accumulator.BeginElement(outputIndex);
                accumulator.BeginObject(NetworkAuthorityPoseSnapshotHashFields.EntityState,
                    RuntimeHashSchemaIds.NetworkSnapshotAuthorityPose, 2, entity?.TerrainIkState != null);
                if (entity?.TerrainIkState != null)
                    entity.TerrainIkState.ContributeNetworkStateHash(accumulator);
                accumulator.EndObject();
                accumulator.EndElement();
                outputIndex++;
            }
            accumulator.EndArray();
        }


        private static bool TryValidateSnapshot(
            long snapshotFrame,
            CharacterTestNetworkAuthorityPoseSnapshot snapshot)
        {
            if (snapshot.EntityId <= 0 || snapshot.LastPublishedRevision < 0L)
                return false;
            CharacterTestNetworkAuthorityPoseLimbSnapshot[] limbs = snapshot.Limbs
                ?? Array.Empty<CharacterTestNetworkAuthorityPoseLimbSnapshot>();
            if (!snapshot.IsConfigured)
            {
                return snapshot.LastPublishedRevision == 0L
                    && snapshot.DefinitionRevision == 0
                    && string.IsNullOrEmpty(snapshot.DefinitionContentHash)
                    && snapshot.BaseSourceRevision == 0
                    && string.IsNullOrEmpty(snapshot.BaseSourceContentHash)
                    && limbs.Length == 0;
            }
            if (snapshot.DefinitionRevision <= 0
                || string.IsNullOrWhiteSpace(snapshot.DefinitionContentHash)
                || snapshot.BaseSourceRevision <= 0
                || string.IsNullOrWhiteSpace(snapshot.BaseSourceContentHash)
                || limbs.Length == 0
                || limbs.Length > CharacterTestTerrainIkStateComponent.MaximumLimbCount)
            {
                return false;
            }

            int previousProbeId = 0;
            for (int i = 0; i < limbs.Length; i++)
            {
                CharacterTestNetworkAuthorityPoseLimbSnapshot value = limbs[i];
                if (value == null
                    || value.Ordinal < 0
                    || value.ProbeId <= 0
                    || (i > 0 && previousProbeId >= value.ProbeId)
                    || value.IkBindingId <= 0
                    || value.LastValidFrame < -1L
                    || value.LastValidFrame > snapshotFrame
                    || value.ConsecutiveMissCount < 0
                    || value.CurrentWeightRaw < Fix64.Zero.RawValue
                    || value.CurrentWeightRaw > Fix64.One.RawValue
                    || !ValidateSurface(value))
                {
                    return false;
                }
                previousProbeId = value.ProbeId;
            }
            return true;
        }

        private static bool ValidateSurface(CharacterTestNetworkAuthorityPoseLimbSnapshot value)
        {
            if (value.LastValidFrame < 0L)
            {
                return value.LastValidTargetXRaw == 0L
                    && value.LastValidTargetYRaw == 0L
                    && value.LastValidTargetZRaw == 0L
                    && value.LastValidNormalXRaw == 0L
                    && value.LastValidNormalYRaw == 0L
                    && value.LastValidNormalZRaw == 0L
                    && value.SurfaceEntityId == 0
                    && value.SurfaceBodyId == 0
                    && value.SurfaceColliderId == 0
                    && value.SurfaceLeafId == 0
                    && value.SurfaceShapeKind == 0
                    && value.SurfaceMaterialKind == (int)CombatSurfaceMaterialKind.Default
                    && value.SurfaceFrictionRaw == 0L
                    && value.SurfaceElasticityRaw == 0L
                    && value.SurfaceFixedBounceHeightRaw == 0L
                    && value.CurrentWeightRaw == 0L;
            }

            bool normalPresent = value.LastValidNormalXRaw != 0L
                || value.LastValidNormalYRaw != 0L
                || value.LastValidNormalZRaw != 0L;
            return normalPresent
                && value.SurfaceEntityId > 0
                && value.SurfaceBodyId > 0
                && value.SurfaceColliderId > 0
                && value.SurfaceLeafId >= 0
                && Enum.IsDefined(typeof(CombatPhysicsShapeKind), value.SurfaceShapeKind)
                && Enum.IsDefined(typeof(CombatSurfaceMaterialKind), value.SurfaceMaterialKind)
                && value.SurfaceFrictionRaw >= 0L
                && value.SurfaceElasticityRaw >= Fix64.Zero.RawValue
                && value.SurfaceElasticityRaw <= Fix64.One.RawValue
                && value.SurfaceFixedBounceHeightRaw >= 0L;
        }

        private static int CompareEntities(
            CharacterTestNetworkAuthorityPoseSnapshot left,
            CharacterTestNetworkAuthorityPoseSnapshot right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            return left.EntityId.CompareTo(right.EntityId);
        }
    }

    internal sealed partial class CharacterTestTerrainIkStateComponent
    {
        internal CharacterTestNetworkAuthorityPoseSnapshot CaptureNetworkSnapshot()
        {
            var limbs = new CharacterTestNetworkAuthorityPoseLimbSnapshot[LimbCount];
            for (int i = 0; i < LimbCount; i++)
                limbs[i] = CaptureLimb(_limbs[i]);
            var snapshot = new CharacterTestNetworkAuthorityPoseSnapshot
            {
                EntityId = OwnerEntityId,
                IsConfigured = IsConfigured,
                LastPublishedRevision = LastPublishedRevision,
                DefinitionRevision = DefinitionRevision,
                DefinitionContentHash = DefinitionContentHash,
                BaseSourceRevision = BaseSourceRevision,
                BaseSourceContentHash = BaseSourceContentHash,
                Limbs = limbs
            };
            snapshot.NormalizeCanonical();
            return snapshot;
        }

        internal bool TryPrepareNetworkRestore(
            CharacterTestNetworkAuthorityPoseSnapshot snapshot,
            out CharacterTestAuthorityPoseNetworkRestorePlan plan,
            out string failure)
        {
            plan = default;
            failure = string.Empty;
            if (snapshot == null
                || snapshot.EntityId != OwnerEntityId
                || snapshot.IsConfigured != IsConfigured
                || snapshot.DefinitionRevision != DefinitionRevision
                || !string.Equals(
                    snapshot.DefinitionContentHash,
                    DefinitionContentHash,
                    StringComparison.Ordinal)
                || snapshot.BaseSourceRevision != BaseSourceRevision
                || !string.Equals(
                    snapshot.BaseSourceContentHash,
                    BaseSourceContentHash,
                    StringComparison.Ordinal)
                || snapshot.LastPublishedRevision < 0L)
            {
                failure = "CharacterTest authority-pose snapshot definition/base source identity mismatch.";
                return false;
            }

            CharacterTestNetworkAuthorityPoseLimbSnapshot[] source = snapshot.Limbs
                ?? Array.Empty<CharacterTestNetworkAuthorityPoseLimbSnapshot>();
            if (source.Length != LimbCount)
            {
                failure = "CharacterTest authority-pose snapshot limb count mismatch.";
                return false;
            }
            var restored = new CharacterTestTerrainIkLimbState[LimbCount];
            for (int i = 0; i < LimbCount; i++)
            {
                CharacterTestTerrainIkLimbState current = _limbs[i];
                CharacterTestNetworkAuthorityPoseLimbSnapshot value = source[i];
                if (value == null
                    || current.Ordinal != value.Ordinal
                    || current.ProbeId != value.ProbeId
                    || current.IkBindingId != value.IkBindingId
                    || current.PartStateId != value.PartStateId
                    || value.ConsecutiveMissCount > current.MissCountCap
                    || !TryRestoreLimb(value, current, out restored[i]))
                {
                    failure = "CharacterTest authority-pose snapshot limb identity/state mismatch.";
                    return false;
                }
            }
            plan = new CharacterTestAuthorityPoseNetworkRestorePlan(
                this,
                restored,
                snapshot.LastPublishedRevision,
                legacyDefaults: false);
            return true;
        }

        internal CharacterTestAuthorityPoseNetworkRestorePlan PrepareLegacyNetworkDefaults()
        {
            return new CharacterTestAuthorityPoseNetworkRestorePlan(
                this,
                Array.Empty<CharacterTestTerrainIkLimbState>(),
                lastPublishedRevision: 0L,
                legacyDefaults: true);
        }

        internal void CommitNetworkRestore(
            CharacterTestTerrainIkLimbState[] limbs,
            long lastPublishedRevision)
        {
            for (int i = 0; i < LimbCount; i++)
                _limbs[i] = limbs[i];
            RestorePublishedRevision(lastPublishedRevision);
        }

        internal void ContributeNetworkStateHash(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddInt(NetworkAuthorityPoseEntityEntryHashFields.EntityId, OwnerEntityId);
            accumulator.AddInt(NetworkAuthorityPoseEntityEntryHashFields.Configured, IsConfigured ? 1 : 0);
            accumulator.AddLong(NetworkAuthorityPoseEntityEntryHashFields.LastPublishedRevision, LastPublishedRevision);
            accumulator.AddInt(NetworkAuthorityPoseEntityEntryHashFields.DefinitionRevision, DefinitionRevision);
            DefinitionDigest.Contribute(accumulator, NetworkAuthorityPoseEntityEntryHashFields.DefinitionContentHash);
            accumulator.AddInt(NetworkAuthorityPoseEntityEntryHashFields.BaseSourceRevision, BaseSourceRevision);
            BaseSourceDigest.Contribute(accumulator, NetworkAuthorityPoseEntityEntryHashFields.BaseSourceContentHash);

            accumulator.BeginArray(NetworkAuthorityPoseEntityEntryHashFields.Limbs, LimbCount);
            for (int i = 0; i < LimbCount; i++)
            {
                accumulator.BeginElement(i);
                ContributeLimbHash(accumulator, _limbs[i]);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }


        private static CharacterTestNetworkAuthorityPoseLimbSnapshot CaptureLimb(
            in CharacterTestTerrainIkLimbState state)
        {
            return new CharacterTestNetworkAuthorityPoseLimbSnapshot
            {
                Ordinal = state.Ordinal,
                ProbeId = state.ProbeId,
                IkBindingId = state.IkBindingId,
                PartStateId = state.PartStateId,
                LastValidFrame = state.LastValidFrame,
                ConsecutiveMissCount = state.ConsecutiveMissCount,
                LastValidTargetXRaw = state.LastValidTargetWorld.X.RawValue,
                LastValidTargetYRaw = state.LastValidTargetWorld.Y.RawValue,
                LastValidTargetZRaw = state.LastValidTargetWorld.Z.RawValue,
                LastValidNormalXRaw = state.LastValidNormalWorld.X.RawValue,
                LastValidNormalYRaw = state.LastValidNormalWorld.Y.RawValue,
                LastValidNormalZRaw = state.LastValidNormalWorld.Z.RawValue,
                SurfaceEntityId = state.SurfaceEntityId,
                SurfaceBodyId = state.SurfaceBodyId,
                SurfaceColliderId = state.SurfaceColliderId,
                SurfaceLeafId = state.SurfaceLeafId,
                SurfaceShapeKind = (int)state.SurfaceShapeKind,
                SurfaceMaterialKind = (int)state.SurfaceMaterial.Kind,
                SurfaceFrictionRaw = state.SurfaceMaterial.Friction.RawValue,
                SurfaceElasticityRaw = state.SurfaceMaterial.Elasticity.RawValue,
                SurfaceFixedBounceHeightRaw = state.SurfaceMaterial.FixedBounceHeight.RawValue,
                CurrentWeightRaw = state.CurrentWeight.RawValue
            };
        }

        private static bool TryRestoreLimb(
            CharacterTestNetworkAuthorityPoseLimbSnapshot source,
            in CharacterTestTerrainIkLimbState identity,
            out CharacterTestTerrainIkLimbState state)
        {
            state = identity;
            try
            {
                state.CurrentValid = false;
                state.CurrentFailure = CharacterTestAuthorityPoseFailure.SourceStale;
                state.LastValidFrame = source.LastValidFrame;
                state.ConsecutiveMissCount = source.ConsecutiveMissCount;
                state.LastValidTargetWorld = RawVector(
                    source.LastValidTargetXRaw,
                    source.LastValidTargetYRaw,
                    source.LastValidTargetZRaw);
                state.LastValidNormalWorld = RawVector(
                    source.LastValidNormalXRaw,
                    source.LastValidNormalYRaw,
                    source.LastValidNormalZRaw);
                state.SurfaceEntityId = source.SurfaceEntityId;
                state.SurfaceBodyId = source.SurfaceBodyId;
                state.SurfaceColliderId = source.SurfaceColliderId;
                state.SurfaceLeafId = source.SurfaceLeafId;
                state.SurfaceShapeKind = (CombatPhysicsShapeKind)source.SurfaceShapeKind;
                state.SurfaceMaterial = new CombatSurfaceMaterial(
                    (CombatSurfaceMaterialKind)source.SurfaceMaterialKind,
                    Fix64.FromRaw(source.SurfaceFrictionRaw),
                    Fix64.FromRaw(source.SurfaceElasticityRaw),
                    Fix64.FromRaw(source.SurfaceFixedBounceHeightRaw));
                state.CurrentWeight = Fix64.FromRaw(source.CurrentWeightRaw);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                state = default;
                return false;
            }
        }

        private static FixVector3 RawVector(long x, long y, long z)
        {
            return new FixVector3(Fix64.FromRaw(x), Fix64.FromRaw(y), Fix64.FromRaw(z));
        }

        private static void ContributeLimbHash(
            RuntimeHashAccumulator accumulator,
            in CharacterTestTerrainIkLimbState state)
        {
            accumulator.AddInt(NetworkAuthorityPoseLimbEntryHashFields.Ordinal, state.Ordinal);
            accumulator.AddInt(NetworkAuthorityPoseLimbEntryHashFields.ProbeId, state.ProbeId);
            accumulator.AddInt(NetworkAuthorityPoseLimbEntryHashFields.IkBindingId, state.IkBindingId);
            accumulator.AddInt(NetworkAuthorityPoseLimbEntryHashFields.PartStateId, state.PartStateId);
            accumulator.AddLong(NetworkAuthorityPoseLimbEntryHashFields.LastValidFrame, state.LastValidFrame);
            accumulator.AddInt(NetworkAuthorityPoseLimbEntryHashFields.ConsecutiveMissCount, state.ConsecutiveMissCount);
            accumulator.AddLong(NetworkAuthorityPoseLimbEntryHashFields.LastValidTargetX, state.LastValidTargetWorld.X.RawValue);
            accumulator.AddLong(NetworkAuthorityPoseLimbEntryHashFields.LastValidTargetY, state.LastValidTargetWorld.Y.RawValue);
            accumulator.AddLong(NetworkAuthorityPoseLimbEntryHashFields.LastValidTargetZ, state.LastValidTargetWorld.Z.RawValue);
            accumulator.AddLong(NetworkAuthorityPoseLimbEntryHashFields.LastValidNormalX, state.LastValidNormalWorld.X.RawValue);
            accumulator.AddLong(NetworkAuthorityPoseLimbEntryHashFields.LastValidNormalY, state.LastValidNormalWorld.Y.RawValue);
            accumulator.AddLong(NetworkAuthorityPoseLimbEntryHashFields.LastValidNormalZ, state.LastValidNormalWorld.Z.RawValue);
            accumulator.AddInt(NetworkAuthorityPoseLimbEntryHashFields.SurfaceEntityId, state.SurfaceEntityId);
            accumulator.AddInt(NetworkAuthorityPoseLimbEntryHashFields.SurfaceBodyId, state.SurfaceBodyId);
            accumulator.AddInt(NetworkAuthorityPoseLimbEntryHashFields.SurfaceColliderId, state.SurfaceColliderId);
            accumulator.AddInt(NetworkAuthorityPoseLimbEntryHashFields.SurfaceLeafId, state.SurfaceLeafId);
            accumulator.AddInt(NetworkAuthorityPoseLimbEntryHashFields.SurfaceShapeKind, (int)state.SurfaceShapeKind);
            accumulator.AddInt(NetworkAuthorityPoseLimbEntryHashFields.SurfaceMaterialKind, (int)state.SurfaceMaterial.Kind);
            accumulator.AddLong(NetworkAuthorityPoseLimbEntryHashFields.SurfaceFriction, state.SurfaceMaterial.Friction.RawValue);
            accumulator.AddLong(NetworkAuthorityPoseLimbEntryHashFields.SurfaceElasticity, state.SurfaceMaterial.Elasticity.RawValue);
            accumulator.AddLong(NetworkAuthorityPoseLimbEntryHashFields.SurfaceFixedBounceHeight, state.SurfaceMaterial.FixedBounceHeight.RawValue);
            accumulator.AddLong(NetworkAuthorityPoseLimbEntryHashFields.CurrentWeight, state.CurrentWeight.RawValue);
        }


    }

    internal static class NetworkAuthorityPoseSnapshotHashFields
    {
        public const int Entities = 1;
        public const int EntityState = 2;
    }

    internal static class NetworkAuthorityPoseEntityEntryHashFields
    {
        public const int EntityId = 1;
        public const int Configured = 2;
        public const int LastPublishedRevision = 3;
        public const int DefinitionRevision = 4;
        public const int DefinitionContentHash = 5;
        public const int BaseSourceRevision = 6;
        public const int BaseSourceContentHash = 7;
        public const int Limbs = 8;
    }

    internal static class NetworkAuthorityPoseLimbEntryHashFields
    {
        public const int Ordinal = 1;
        public const int ProbeId = 2;
        public const int IkBindingId = 3;
        public const int PartStateId = 4;
        public const int LastValidFrame = 5;
        public const int ConsecutiveMissCount = 6;
        public const int LastValidTargetX = 7;
        public const int LastValidTargetY = 8;
        public const int LastValidTargetZ = 9;
        public const int LastValidNormalX = 10;
        public const int LastValidNormalY = 11;
        public const int LastValidNormalZ = 12;
        public const int SurfaceEntityId = 13;
        public const int SurfaceBodyId = 14;
        public const int SurfaceColliderId = 15;
        public const int SurfaceLeafId = 16;
        public const int SurfaceShapeKind = 17;
        public const int SurfaceMaterialKind = 18;
        public const int SurfaceFriction = 19;
        public const int SurfaceElasticity = 20;
        public const int SurfaceFixedBounceHeight = 21;
        public const int CurrentWeight = 22;
    }
}

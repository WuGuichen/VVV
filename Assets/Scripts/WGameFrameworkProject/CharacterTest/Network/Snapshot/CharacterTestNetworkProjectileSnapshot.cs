using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestNetworkProjectileSnapshotCodec
    {
        public static CharacterTestNetworkProjectileRuntimeSnapshot Capture(
            CharacterTestCharacterCombatTraceSystem combatTrace)
        {
            return CharacterTestNetworkProjectileRuntimeSnapshot.FromState(
                combatTrace?.CreateProjectileStateSnapshot());
        }

        public static NetworkSnapshotApplyResult Apply(
            CharacterTestCharacterCombatTraceSystem combatTrace,
            CharacterTestNetworkProjectileRuntimeSnapshot projectiles)
        {
            if (combatTrace == null)
            {
                if (projectiles == null || projectiles.ActiveCount == 0)
                    return NetworkSnapshotApplyResult.Succeeded();

                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest projectile snapshot cannot be applied because combat trace system is missing.");
            }

            CombatProjectileRuntimeState state;
            try
            {
                state = projectiles?.ToState();
            }
            catch (Exception exception)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest projectile snapshot parse failed: " + exception.Message);
            }

            if (!combatTrace.TryApplyProjectileStateSnapshot(state, out string failure))
                return NetworkSnapshotApplyResult.Failed("CharacterTest projectile snapshot apply failed: " + failure);

            return NetworkSnapshotApplyResult.Succeeded();
        }

        public static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CombatProjectileRuntimeState state,
            int sourceEntityIdFilter = 0)
        {
            AddStateHash(
                accumulator,
                state,
                sourceEntityIdFilter,
                excludeSourceEntity: false);
        }

        public static void AddStateHashExcludingSource(
            RuntimeHashAccumulator accumulator,
            CombatProjectileRuntimeState state,
            int excludedSourceEntityId)
        {
            AddStateHash(
                accumulator,
                state,
                Math.Max(0, excludedSourceEntityId),
                excludeSourceEntity: true);
        }

        private static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CombatProjectileRuntimeState state,
            int sourceEntityIdFilter,
            bool excludeSourceEntity)
        {
            accumulator.AddInt(
                NetworkProjectileSnapshotHashFields.Schema,
                state != null ? state.SchemaVersion : CombatProjectileRuntimeState.CurrentSchemaVersion);
            accumulator.AddLong(NetworkProjectileSnapshotHashFields.DefinitionHash, state != null ? state.DefinitionHash : 0L);
            if (sourceEntityIdFilter <= 0)
                accumulator.AddInt(NetworkProjectileSnapshotHashFields.NextProjectileId, state != null ? state.NextProjectileId : 1);

            IReadOnlyList<CombatProjectileSnapshot> projectiles = state != null
                ? state.ActiveProjectiles
                : null;
            int count = projectiles != null ? projectiles.Count : 0;
            if (sourceEntityIdFilter <= 0)
            {
                accumulator.BeginArray(NetworkProjectileSnapshotHashFields.Projectiles, count);
                for (int i = 0; i < count; i++)
                {
                    accumulator.BeginElement(i);
                    AddSnapshotHash(accumulator, i, projectiles[i]);
                    accumulator.EndElement();
                }
                accumulator.EndArray();
                return;
            }

            int includedCount = 0;
            for (int i = 0; i < count; i++)
            {
                bool sourceMatches = projectiles[i].SourceEntityId.Value == sourceEntityIdFilter;
                if ((!excludeSourceEntity && sourceMatches) || (excludeSourceEntity && !sourceMatches))
                    includedCount++;
            }

            accumulator.BeginArray(NetworkProjectileSnapshotHashFields.Projectiles, includedCount);
            int outputIndex = 0;
            for (int i = 0; i < count; i++)
            {
                bool sourceMatches = projectiles[i].SourceEntityId.Value == sourceEntityIdFilter;
                if ((!excludeSourceEntity && !sourceMatches) || (excludeSourceEntity && sourceMatches))
                    continue;

                accumulator.BeginElement(outputIndex);
                AddSnapshotHash(accumulator, outputIndex, projectiles[i]);
                accumulator.EndElement();
                outputIndex++;
            }
            accumulator.EndArray();
        }

        private static void AddSnapshotHash(
            RuntimeHashAccumulator accumulator,
            int index,
            CombatProjectileSnapshot projectile)
        {
            accumulator.AddInt(NetworkProjectileEntryHashFields.Index, index);
            accumulator.AddInt(NetworkProjectileEntryHashFields.ProjectileId, projectile.ProjectileId);
            accumulator.AddInt(NetworkProjectileEntryHashFields.SpawnSequence, projectile.SpawnSequence);
            accumulator.AddInt(NetworkProjectileEntryHashFields.DefinitionId, projectile.DefinitionId);
            accumulator.AddDigest(NetworkProjectileEntryHashFields.DefinitionStableId, projectile.DefinitionLabelDigest);
            accumulator.AddInt(NetworkProjectileEntryHashFields.SourceEntityId, projectile.SourceEntityId.Value);
            accumulator.AddInt(NetworkProjectileEntryHashFields.ActionId, projectile.ActionId);
            accumulator.AddInt(NetworkProjectileEntryHashFields.ActionInstanceId, projectile.ActionInstanceId);
            accumulator.AddInt(NetworkProjectileEntryHashFields.TraceId, projectile.TraceId);
            accumulator.AddInt(NetworkProjectileEntryHashFields.LocalFrame, projectile.LocalFrame);
            accumulator.AddLong(NetworkProjectileEntryHashFields.SpawnFrame, projectile.SpawnFrame.Value);
            accumulator.AddLong(NetworkProjectileEntryHashFields.LastFrame, projectile.LastFrame.Value);
            accumulator.AddInt(NetworkProjectileEntryHashFields.AgeFrames, projectile.AgeFrames);
            accumulator.AddLong(NetworkProjectileEntryHashFields.PositionX, projectile.Position.X.RawValue);
            accumulator.AddLong(NetworkProjectileEntryHashFields.PositionY, projectile.Position.Y.RawValue);
            accumulator.AddLong(NetworkProjectileEntryHashFields.PositionZ, projectile.Position.Z.RawValue);
            accumulator.AddLong(NetworkProjectileEntryHashFields.DirectionX, projectile.Direction.X.RawValue);
            accumulator.AddLong(NetworkProjectileEntryHashFields.DirectionY, projectile.Direction.Y.RawValue);
            accumulator.AddLong(NetworkProjectileEntryHashFields.DirectionZ, projectile.Direction.Z.RawValue);
            accumulator.AddLong(NetworkProjectileEntryHashFields.OriginX, projectile.Origin.X.RawValue);
            accumulator.AddLong(NetworkProjectileEntryHashFields.OriginY, projectile.Origin.Y.RawValue);
            accumulator.AddLong(NetworkProjectileEntryHashFields.OriginZ, projectile.Origin.Z.RawValue);
            accumulator.AddInt(NetworkProjectileEntryHashFields.HasTargetPoint, projectile.HasTargetPoint ? 1 : 0);
            accumulator.AddLong(NetworkProjectileEntryHashFields.TargetPointX, projectile.TargetPoint.X.RawValue);
            accumulator.AddLong(NetworkProjectileEntryHashFields.TargetPointY, projectile.TargetPoint.Y.RawValue);
            accumulator.AddLong(NetworkProjectileEntryHashFields.TargetPointZ, projectile.TargetPoint.Z.RawValue);
            accumulator.AddInt(NetworkProjectileEntryHashFields.EffectiveLifetimeFrames, projectile.EffectiveLifetimeFrames);
            accumulator.AddInt(NetworkProjectileEntryHashFields.HasTrackingTarget, projectile.HasTrackingTarget ? 1 : 0);
            accumulator.AddInt(NetworkProjectileEntryHashFields.TrackingTargetEntityId, projectile.TrackingTargetEntityId.Value);

            int impactCount = projectile.AcceptedImpacts != null ? projectile.AcceptedImpacts.Count : 0;
            accumulator.BeginArray(NetworkProjectileEntryHashFields.AcceptedImpacts, impactCount);
            for (int i = 0; i < impactCount; i++)
            {
                accumulator.BeginElement(i);
                CombatProjectileHitRecord hit = projectile.AcceptedImpacts[i];
                accumulator.AddInt(NetworkProjectileAcceptedImpactEntryHashFields.Index, i);
                accumulator.AddInt(NetworkProjectileAcceptedImpactEntryHashFields.TargetEntityId, hit.TargetEntityId.Value);
                accumulator.AddLong(NetworkProjectileAcceptedImpactEntryHashFields.Frame, hit.Frame.Value);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }




    }

    public sealed class CharacterTestNetworkProjectileRuntimeSnapshot
    {
        public int SchemaVersion { get; set; }
        public string ModuleId { get; set; }
        public long DefinitionHash { get; set; }
        public int NextProjectileId { get; set; }
        public CharacterTestNetworkProjectileSnapshot[] ActiveProjectiles { get; set; }
        public int ActiveCount => ActiveProjectiles != null ? ActiveProjectiles.Length : 0;

        public static CharacterTestNetworkProjectileRuntimeSnapshot FromState(CombatProjectileRuntimeState state)
        {
            if (state == null)
                return null;

            return new CharacterTestNetworkProjectileRuntimeSnapshot
            {
                SchemaVersion = state.SchemaVersion,
                ModuleId = state.ModuleId,
                DefinitionHash = state.DefinitionHash,
                NextProjectileId = state.NextProjectileId,
                ActiveProjectiles = CharacterTestNetworkProjectileSnapshot.FromSnapshots(state.ActiveProjectiles)
            };
        }

        public CharacterTestNetworkProjectileRuntimeSnapshot CloneForRollback()
        {
            return new CharacterTestNetworkProjectileRuntimeSnapshot
            {
                SchemaVersion = SchemaVersion,
                ModuleId = ModuleId,
                DefinitionHash = DefinitionHash,
                NextProjectileId = NextProjectileId,
                ActiveProjectiles = CharacterTestNetworkProjectileSnapshot.CloneArray(ActiveProjectiles)
            };
        }

        public CombatProjectileRuntimeState ToState()
        {
            return new CombatProjectileRuntimeState(
                SchemaVersion,
                string.IsNullOrWhiteSpace(ModuleId)
                    ? CombatProjectileRuntimeModule.DefaultModuleId
                    : ModuleId,
                DefinitionHash,
                NextProjectileId <= 0 ? 1 : NextProjectileId,
                CharacterTestNetworkProjectileSnapshot.ToSnapshots(ActiveProjectiles));
        }
    }

    public sealed class CharacterTestNetworkProjectileSnapshot
    {
        public int ProjectileId { get; set; }
        public int SpawnSequence { get; set; }
        public int DefinitionId { get; set; }
        public string DefinitionStableId { get; set; }
        public RuntimeHashDigest DefinitionLabelDigest { get; set; }
        public int SourceEntityId { get; set; }
        public int ActionId { get; set; }
        public int ActionInstanceId { get; set; }
        public int TraceId { get; set; }
        public int LocalFrame { get; set; }
        public int SpawnFrame { get; set; }
        public int LastFrame { get; set; }
        public int AgeFrames { get; set; }
        public long PositionX { get; set; }
        public long PositionY { get; set; }
        public long PositionZ { get; set; }
        public long DirectionX { get; set; }
        public long DirectionY { get; set; }
        public long DirectionZ { get; set; }
        public long OriginX { get; set; }
        public long OriginY { get; set; }
        public long OriginZ { get; set; }
        public bool HasTargetPoint { get; set; }
        public long TargetPointX { get; set; }
        public long TargetPointY { get; set; }
        public long TargetPointZ { get; set; }
        public int EffectiveLifetimeFrames { get; set; }
        public bool HasTrackingTarget { get; set; }
        public int TrackingTargetEntityId { get; set; }
        public CharacterTestNetworkProjectileHitRecord[] AcceptedImpacts { get; set; }

        public static CharacterTestNetworkProjectileSnapshot[] FromSnapshots(
            IReadOnlyList<CombatProjectileSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
                return Array.Empty<CharacterTestNetworkProjectileSnapshot>();

            var result = new CharacterTestNetworkProjectileSnapshot[snapshots.Count];
            for (int i = 0; i < snapshots.Count; i++)
                result[i] = FromSnapshot(snapshots[i]);
            return result;
        }

        public static List<CombatProjectileSnapshot> ToSnapshots(
            CharacterTestNetworkProjectileSnapshot[] snapshots)
        {
            var result = new List<CombatProjectileSnapshot>();
            if (snapshots == null || snapshots.Length == 0)
                return result;

            for (int i = 0; i < snapshots.Length; i++)
            {
                if (snapshots[i] != null)
                    result.Add(snapshots[i].ToSnapshot());
            }

            return result;
        }

        public static CharacterTestNetworkProjectileSnapshot[] CloneArray(
            CharacterTestNetworkProjectileSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
                return Array.Empty<CharacterTestNetworkProjectileSnapshot>();

            var result = new CharacterTestNetworkProjectileSnapshot[snapshots.Length];
            for (int i = 0; i < snapshots.Length; i++)
                result[i] = snapshots[i]?.CloneForRollback();
            return result;
        }

        public CharacterTestNetworkProjectileSnapshot CloneForRollback()
        {
            var clone = (CharacterTestNetworkProjectileSnapshot)MemberwiseClone();
            clone.AcceptedImpacts = CharacterTestNetworkProjectileHitRecord.CloneArray(AcceptedImpacts);
            return clone;
        }

        private static CharacterTestNetworkProjectileSnapshot FromSnapshot(
            CombatProjectileSnapshot snapshot)
        {
            return new CharacterTestNetworkProjectileSnapshot
            {
                ProjectileId = snapshot.ProjectileId,
                SpawnSequence = snapshot.SpawnSequence,
                DefinitionId = snapshot.DefinitionId,
                DefinitionStableId = snapshot.DefinitionStableId,
                DefinitionLabelDigest = snapshot.DefinitionLabelDigest,
                SourceEntityId = snapshot.SourceEntityId.Value,
                ActionId = snapshot.ActionId,
                ActionInstanceId = snapshot.ActionInstanceId,
                TraceId = snapshot.TraceId,
                LocalFrame = snapshot.LocalFrame,
                SpawnFrame = snapshot.SpawnFrame.Value,
                LastFrame = snapshot.LastFrame.Value,
                AgeFrames = snapshot.AgeFrames,
                PositionX = snapshot.Position.X.RawValue,
                PositionY = snapshot.Position.Y.RawValue,
                PositionZ = snapshot.Position.Z.RawValue,
                DirectionX = snapshot.Direction.X.RawValue,
                DirectionY = snapshot.Direction.Y.RawValue,
                DirectionZ = snapshot.Direction.Z.RawValue,
                OriginX = snapshot.Origin.X.RawValue,
                OriginY = snapshot.Origin.Y.RawValue,
                OriginZ = snapshot.Origin.Z.RawValue,
                HasTargetPoint = snapshot.HasTargetPoint,
                TargetPointX = snapshot.TargetPoint.X.RawValue,
                TargetPointY = snapshot.TargetPoint.Y.RawValue,
                TargetPointZ = snapshot.TargetPoint.Z.RawValue,
                EffectiveLifetimeFrames = snapshot.EffectiveLifetimeFrames,
                HasTrackingTarget = snapshot.HasTrackingTarget,
                TrackingTargetEntityId = snapshot.TrackingTargetEntityId.Value,
                AcceptedImpacts = CharacterTestNetworkProjectileHitRecord.FromRecords(snapshot.AcceptedImpacts)
            };
        }

        private CombatProjectileSnapshot ToSnapshot()
        {
            return new CombatProjectileSnapshot(
                ProjectileId,
                SpawnSequence,
                DefinitionId,
                DefinitionStableId,
                new CombatEntityId(SourceEntityId),
                ActionId,
                ActionInstanceId,
                TraceId,
                LocalFrame,
                new CombatFrame(SpawnFrame),
                new CombatFrame(LastFrame),
                AgeFrames,
                new FixVector3(
                    Fix64.FromRaw(PositionX),
                    Fix64.FromRaw(PositionY),
                    Fix64.FromRaw(PositionZ)),
                new FixVector3(
                    Fix64.FromRaw(DirectionX),
                    Fix64.FromRaw(DirectionY),
                    Fix64.FromRaw(DirectionZ)),
                new FixVector3(
                    Fix64.FromRaw(OriginX),
                    Fix64.FromRaw(OriginY),
                    Fix64.FromRaw(OriginZ)),
                HasTargetPoint,
                new FixVector3(
                    Fix64.FromRaw(TargetPointX),
                    Fix64.FromRaw(TargetPointY),
                    Fix64.FromRaw(TargetPointZ)),
                CharacterTestNetworkProjectileHitRecord.ToRecords(AcceptedImpacts),
                EffectiveLifetimeFrames,
                HasTrackingTarget,
                new CombatEntityId(TrackingTargetEntityId),
                definitionLabelDigest: DefinitionLabelDigest);
        }
    }

    public sealed class CharacterTestNetworkProjectileHitRecord
    {
        public int TargetEntityId { get; set; }
        public int Frame { get; set; }

        public static CharacterTestNetworkProjectileHitRecord[] FromRecords(
            IReadOnlyList<CombatProjectileHitRecord> records)
        {
            if (records == null || records.Count == 0)
                return Array.Empty<CharacterTestNetworkProjectileHitRecord>();

            var result = new CharacterTestNetworkProjectileHitRecord[records.Count];
            for (int i = 0; i < records.Count; i++)
            {
                result[i] = new CharacterTestNetworkProjectileHitRecord
                {
                    TargetEntityId = records[i].TargetEntityId.Value,
                    Frame = records[i].Frame.Value
                };
            }

            return result;
        }

        public static List<CombatProjectileHitRecord> ToRecords(
            CharacterTestNetworkProjectileHitRecord[] records)
        {
            var result = new List<CombatProjectileHitRecord>();
            if (records == null || records.Length == 0)
                return result;

            for (int i = 0; i < records.Length; i++)
            {
                CharacterTestNetworkProjectileHitRecord record = records[i];
                if (record == null || record.TargetEntityId <= 0)
                    continue;

                result.Add(new CombatProjectileHitRecord(
                    new CombatEntityId(record.TargetEntityId),
                    new CombatFrame(record.Frame)));
            }

            return result;
        }

        public static CharacterTestNetworkProjectileHitRecord[] CloneArray(
            CharacterTestNetworkProjectileHitRecord[] records)
        {
            if (records == null || records.Length == 0)
                return Array.Empty<CharacterTestNetworkProjectileHitRecord>();

            var result = new CharacterTestNetworkProjectileHitRecord[records.Length];
            for (int i = 0; i < records.Length; i++)
            {
                CharacterTestNetworkProjectileHitRecord record = records[i];
                result[i] = record == null
                    ? null
                    : new CharacterTestNetworkProjectileHitRecord
                    {
                        TargetEntityId = record.TargetEntityId,
                        Frame = record.Frame
                    };
            }

            return result;
        }
    }

    internal static class NetworkProjectileSnapshotHashFields
    {
        public const int Schema = 1;
        public const int DefinitionHash = 2;
        public const int NextProjectileId = 3;
        public const int Projectiles = 4;
    }

    internal static class NetworkProjectileEntryHashFields
    {
        public const int Index = 1;
        public const int ProjectileId = 2;
        public const int SpawnSequence = 3;
        public const int DefinitionId = 4;
        public const int DefinitionStableId = 5;
        public const int SourceEntityId = 6;
        public const int ActionId = 7;
        public const int ActionInstanceId = 8;
        public const int TraceId = 9;
        public const int LocalFrame = 10;
        public const int SpawnFrame = 11;
        public const int LastFrame = 12;
        public const int AgeFrames = 13;
        public const int PositionX = 14;
        public const int PositionY = 15;
        public const int PositionZ = 16;
        public const int DirectionX = 17;
        public const int DirectionY = 18;
        public const int DirectionZ = 19;
        public const int OriginX = 20;
        public const int OriginY = 21;
        public const int OriginZ = 22;
        public const int HasTargetPoint = 23;
        public const int TargetPointX = 24;
        public const int TargetPointY = 25;
        public const int TargetPointZ = 26;
        public const int EffectiveLifetimeFrames = 27;
        public const int HasTrackingTarget = 28;
        public const int TrackingTargetEntityId = 29;
        public const int AcceptedImpacts = 30;
    }

    internal static class NetworkProjectileAcceptedImpactEntryHashFields
    {
        public const int Index = 1;
        public const int TargetEntityId = 2;
        public const int Frame = 3;
    }
}

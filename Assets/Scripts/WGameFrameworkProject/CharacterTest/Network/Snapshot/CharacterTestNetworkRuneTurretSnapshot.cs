using System;
using System.Collections.Generic;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestNetworkRuneTurretSnapshotCodec
    {
        public static CharacterTestNetworkRuneTurretRuntimeSnapshot Capture(
            CharacterTestCharacterCombatTraceSystem combatTrace)
        {
            return CharacterTestNetworkRuneTurretRuntimeSnapshot.FromState(
                combatTrace?.CreateRuneTurretStateSnapshot());
        }

        public static NetworkSnapshotApplyResult Apply(
            CharacterTestCharacterCombatTraceSystem combatTrace,
            CharacterTestNetworkRuneTurretRuntimeSnapshot runeTurrets)
        {
            if (combatTrace == null)
            {
                if (runeTurrets == null || runeTurrets.ActiveCount == 0)
                    return NetworkSnapshotApplyResult.Succeeded();

                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest rune turret snapshot cannot be applied because combat trace system is missing.");
            }

            CharacterTestRuneTurretRuntimeState state;
            try
            {
                state = runeTurrets?.ToState();
            }
            catch (Exception exception)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest rune turret snapshot parse failed: " + exception.Message);
            }

            if (!combatTrace.TryApplyRuneTurretStateSnapshot(state, out string failure))
                return NetworkSnapshotApplyResult.Failed("CharacterTest rune turret snapshot apply failed: " + failure);

            return NetworkSnapshotApplyResult.Succeeded();
        }

        public static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestRuneTurretRuntimeState state,
            int casterEntityIdFilter = 0)
        {
            AddStateHash(accumulator, state, casterEntityIdFilter, excludeCasterEntity: false);
        }

        public static void AddStateHashExcludingCaster(
            RuntimeHashAccumulator accumulator,
            CharacterTestRuneTurretRuntimeState state,
            int excludedCasterEntityId)
        {
            AddStateHash(
                accumulator,
                state,
                Math.Max(0, excludedCasterEntityId),
                excludeCasterEntity: true);
        }

        private static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestRuneTurretRuntimeState state,
            int casterEntityIdFilter,
            bool excludeCasterEntity)
        {
            accumulator.BeginObject(
                NetworkRootStateFieldIds.RuneTurretState,
                RuntimeHashSchemaIds.NetworkSnapshotRuneTurret,
                state != null ? state.SchemaVersion : CharacterTestRuneTurretRuntimeState.CurrentSchemaVersion,
                state != null);
            if (state == null)
            {
                accumulator.EndObject();
                return;
            }
            accumulator.AddInt(
                NetworkRuneTurretSnapshotHashFields.Schema,
                state.SchemaVersion);
            accumulator.AddInt(NetworkRuneTurretSnapshotHashFields.ModuleId, state.ModuleId);
            accumulator.AddInt(NetworkRuneTurretSnapshotHashFields.NextRuntimeId, state.NextRuntimeId);
            IReadOnlyList<CharacterTestRuneTurretSnapshot> turrets = state.ActiveTurrets;
            int count = turrets != null ? turrets.Count : 0;
            if (casterEntityIdFilter <= 0)
            {
                accumulator.BeginArray(NetworkRuneTurretSnapshotHashFields.Turrets, count);
                for (int i = 0; i < count; i++)
                {
                    accumulator.BeginElement(i);
                    AddSnapshotHash(accumulator, i, turrets[i]);
                    accumulator.EndElement();
                }
                accumulator.EndArray();
                accumulator.EndObject();
                return;
            }

            int includedCount = 0;
            for (int i = 0; i < count; i++)
            {
                bool casterMatches = turrets[i].CasterEntityId == casterEntityIdFilter;
                if ((!excludeCasterEntity && casterMatches) || (excludeCasterEntity && !casterMatches))
                    includedCount++;
            }

            accumulator.BeginArray(NetworkRuneTurretSnapshotHashFields.Turrets, includedCount);
            int outputIndex = 0;
            for (int i = 0; i < count; i++)
            {
                bool casterMatches = turrets[i].CasterEntityId == casterEntityIdFilter;
                if ((!excludeCasterEntity && !casterMatches) || (excludeCasterEntity && casterMatches))
                    continue;

                accumulator.BeginElement(outputIndex);
                AddSnapshotHash(accumulator, outputIndex, turrets[i]);
                accumulator.EndElement();
                outputIndex++;
            }
            accumulator.EndArray();
            accumulator.EndObject();
        }

        private static void AddSnapshotHash(
            RuntimeHashAccumulator accumulator,
            int index,
            CharacterTestRuneTurretSnapshot turret)
        {
            accumulator.AddInt(NetworkRuneTurretEntryHashFields.Index, index);
            accumulator.AddInt(NetworkRuneTurretEntryHashFields.RuntimeId, turret.RuntimeId);
            accumulator.AddInt(NetworkRuneTurretEntryHashFields.CasterEntityId, turret.CasterEntityId);
            accumulator.AddInt(NetworkRuneTurretEntryHashFields.SourceCombatEntityId, turret.SourceCombatEntityId);
            accumulator.AddInt(NetworkRuneTurretEntryHashFields.ActionInstanceId, turret.ActionInstanceId);
            accumulator.AddInt(NetworkRuneTurretEntryHashFields.SourceOrder, turret.SourceOrder);
            accumulator.AddInt(NetworkRuneTurretEntryHashFields.LocalFrame, turret.LocalFrame);
            accumulator.AddLong(NetworkRuneTurretEntryHashFields.CreatedFrame, turret.CreatedFrame);
            accumulator.AddLong(NetworkRuneTurretEntryHashFields.ActivateFrame, turret.ActivateFrame);
            accumulator.AddLong(NetworkRuneTurretEntryHashFields.ExpireFrame, turret.ExpireFrame);
            accumulator.AddLong(NetworkRuneTurretEntryHashFields.NextThinkFrame, turret.NextThinkFrame);
            accumulator.AddInt(NetworkRuneTurretEntryHashFields.ShotsFired, turret.ShotsFired);
            accumulator.AddLong(NetworkRuneTurretEntryHashFields.CenterXRaw, turret.CenterXRaw);
            accumulator.AddLong(NetworkRuneTurretEntryHashFields.CenterYRaw, turret.CenterYRaw);
            accumulator.AddLong(NetworkRuneTurretEntryHashFields.CenterZRaw, turret.CenterZRaw);
            accumulator.AddLong(NetworkRuneTurretEntryHashFields.ForwardXRaw, turret.ForwardXRaw);
            accumulator.AddLong(NetworkRuneTurretEntryHashFields.ForwardYRaw, turret.ForwardYRaw);
            accumulator.AddLong(NetworkRuneTurretEntryHashFields.ForwardZRaw, turret.ForwardZRaw);
            accumulator.AddInt(NetworkRuneTurretEntryHashFields.TraceId, turret.TraceId);
        }




    }

    public sealed class CharacterTestNetworkRuneTurretRuntimeSnapshot
    {
        public int SchemaVersion { get; set; }
        public int ModuleId { get; set; }
        public int NextRuntimeId { get; set; }
        public CharacterTestNetworkRuneTurretSnapshot[] ActiveTurrets { get; set; }
        public int ActiveCount => ActiveTurrets != null ? ActiveTurrets.Length : 0;

        internal static CharacterTestNetworkRuneTurretRuntimeSnapshot FromState(
            CharacterTestRuneTurretRuntimeState state)
        {
            if (state == null)
                return null;

            return new CharacterTestNetworkRuneTurretRuntimeSnapshot
            {
                SchemaVersion = state.SchemaVersion,
                ModuleId = state.ModuleId,
                NextRuntimeId = state.NextRuntimeId,
                ActiveTurrets = CharacterTestNetworkRuneTurretSnapshot.FromSnapshots(state.ActiveTurrets)
            };
        }

        public CharacterTestNetworkRuneTurretRuntimeSnapshot CloneForRollback()
        {
            return new CharacterTestNetworkRuneTurretRuntimeSnapshot
            {
                SchemaVersion = SchemaVersion,
                ModuleId = ModuleId,
                NextRuntimeId = NextRuntimeId,
                ActiveTurrets = CharacterTestNetworkRuneTurretSnapshot.CloneArray(ActiveTurrets)
            };
        }

        internal CharacterTestRuneTurretRuntimeState ToState()
        {
            return new CharacterTestRuneTurretRuntimeState(
                SchemaVersion,
                ModuleId,
                NextRuntimeId,
                CharacterTestNetworkRuneTurretSnapshot.ToSnapshots(ActiveTurrets));
        }
    }

    public sealed class CharacterTestNetworkRuneTurretSnapshot
    {
        public int RuntimeId { get; set; }
        public int CasterEntityId { get; set; }
        public int SourceCombatEntityId { get; set; }
        public int ActionInstanceId { get; set; }
        public int SourceOrder { get; set; }
        public int LocalFrame { get; set; }
        public long CreatedFrame { get; set; }
        public long ActivateFrame { get; set; }
        public long ExpireFrame { get; set; }
        public long NextThinkFrame { get; set; }
        public int ShotsFired { get; set; }
        public long CenterXRaw { get; set; }
        public long CenterYRaw { get; set; }
        public long CenterZRaw { get; set; }
        public long ForwardXRaw { get; set; }
        public long ForwardYRaw { get; set; }
        public long ForwardZRaw { get; set; }
        public int TraceId { get; set; }

        internal static CharacterTestNetworkRuneTurretSnapshot[] FromSnapshots(
            IReadOnlyList<CharacterTestRuneTurretSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
                return Array.Empty<CharacterTestNetworkRuneTurretSnapshot>();

            var result = new CharacterTestNetworkRuneTurretSnapshot[snapshots.Count];
            for (int i = 0; i < snapshots.Count; i++)
                result[i] = FromSnapshot(snapshots[i]);
            return result;
        }

        internal static List<CharacterTestRuneTurretSnapshot> ToSnapshots(
            CharacterTestNetworkRuneTurretSnapshot[] snapshots)
        {
            var result = new List<CharacterTestRuneTurretSnapshot>();
            if (snapshots == null || snapshots.Length == 0)
                return result;

            for (int i = 0; i < snapshots.Length; i++)
            {
                CharacterTestNetworkRuneTurretSnapshot snapshot = snapshots[i];
                if (snapshot == null || snapshot.RuntimeId <= 0 || snapshot.CasterEntityId <= 0)
                    continue;

                result.Add(snapshot.ToSnapshot());
            }

            return result;
        }

        public static CharacterTestNetworkRuneTurretSnapshot[] CloneArray(
            CharacterTestNetworkRuneTurretSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
                return Array.Empty<CharacterTestNetworkRuneTurretSnapshot>();

            var result = new CharacterTestNetworkRuneTurretSnapshot[snapshots.Length];
            for (int i = 0; i < snapshots.Length; i++)
                result[i] = snapshots[i]?.CloneForRollback();
            return result;
        }

        public CharacterTestNetworkRuneTurretSnapshot CloneForRollback()
        {
            return (CharacterTestNetworkRuneTurretSnapshot)MemberwiseClone();
        }

        private static CharacterTestNetworkRuneTurretSnapshot FromSnapshot(
            CharacterTestRuneTurretSnapshot snapshot)
        {
            return new CharacterTestNetworkRuneTurretSnapshot
            {
                RuntimeId = snapshot.RuntimeId,
                CasterEntityId = snapshot.CasterEntityId,
                SourceCombatEntityId = snapshot.SourceCombatEntityId,
                ActionInstanceId = snapshot.ActionInstanceId,
                SourceOrder = snapshot.SourceOrder,
                LocalFrame = snapshot.LocalFrame,
                CreatedFrame = snapshot.CreatedFrame,
                ActivateFrame = snapshot.ActivateFrame,
                ExpireFrame = snapshot.ExpireFrame,
                NextThinkFrame = snapshot.NextThinkFrame,
                ShotsFired = snapshot.ShotsFired,
                CenterXRaw = snapshot.CenterXRaw,
                CenterYRaw = snapshot.CenterYRaw,
                CenterZRaw = snapshot.CenterZRaw,
                ForwardXRaw = snapshot.ForwardXRaw,
                ForwardYRaw = snapshot.ForwardYRaw,
                ForwardZRaw = snapshot.ForwardZRaw,
                TraceId = snapshot.TraceId
            };
        }

        private CharacterTestRuneTurretSnapshot ToSnapshot()
        {
            return new CharacterTestRuneTurretSnapshot(
                RuntimeId,
                CasterEntityId,
                SourceCombatEntityId,
                ActionInstanceId,
                SourceOrder,
                LocalFrame,
                CreatedFrame,
                ActivateFrame,
                ExpireFrame,
                NextThinkFrame,
                ShotsFired,
                CenterXRaw,
                CenterYRaw,
                CenterZRaw,
                ForwardXRaw,
                ForwardYRaw,
                ForwardZRaw,
                TraceId);
        }
    }

    internal static class NetworkRuneTurretSnapshotHashFields
    {
        public const int Schema = 1;
        public const int NextRuntimeId = 2;
        public const int Turrets = 3;
        public const int ModuleId = 4;
    }

    internal static class NetworkRuneTurretEntryHashFields
    {
        public const int Index = 1;
        public const int RuntimeId = 2;
        public const int CasterEntityId = 3;
        public const int SourceCombatEntityId = 4;
        public const int ActionInstanceId = 5;
        public const int SourceOrder = 6;
        public const int LocalFrame = 7;
        public const int CreatedFrame = 8;
        public const int ActivateFrame = 9;
        public const int ExpireFrame = 10;
        public const int NextThinkFrame = 11;
        public const int ShotsFired = 12;
        public const int CenterXRaw = 13;
        public const int CenterYRaw = 14;
        public const int CenterZRaw = 15;
        public const int ForwardXRaw = 16;
        public const int ForwardYRaw = 17;
        public const int ForwardZRaw = 18;
        public const int TraceId = 19;
    }
}

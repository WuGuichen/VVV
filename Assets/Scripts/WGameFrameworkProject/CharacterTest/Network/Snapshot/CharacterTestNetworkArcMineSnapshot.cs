using System;
using System.Collections.Generic;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestNetworkArcMineSnapshotCodec
    {
        public static CharacterTestNetworkArcMineRuntimeSnapshot Capture(
            CharacterTestCharacterCombatTraceSystem combatTrace)
        {
            return CharacterTestNetworkArcMineRuntimeSnapshot.FromState(
                combatTrace?.CreateArcMineStateSnapshot());
        }

        public static NetworkSnapshotApplyResult Apply(
            CharacterTestCharacterCombatTraceSystem combatTrace,
            CharacterTestNetworkArcMineRuntimeSnapshot arcMines)
        {
            if (combatTrace == null)
            {
                if (arcMines == null || arcMines.ActiveCount == 0)
                    return NetworkSnapshotApplyResult.Succeeded();

                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest arc mine snapshot cannot be applied because combat trace system is missing.");
            }

            CharacterTestArcMineRuntimeState state;
            try
            {
                state = arcMines?.ToState();
            }
            catch (Exception exception)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest arc mine snapshot parse failed: " + exception.Message);
            }

            if (!combatTrace.TryApplyArcMineStateSnapshot(state, out string failure))
                return NetworkSnapshotApplyResult.Failed("CharacterTest arc mine snapshot apply failed: " + failure);

            return NetworkSnapshotApplyResult.Succeeded();
        }

        public static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestArcMineRuntimeState state,
            int casterEntityIdFilter = 0)
        {
            AddStateHash(accumulator, state, casterEntityIdFilter, excludeCasterEntity: false);
        }

        public static void AddStateHashExcludingCaster(
            RuntimeHashAccumulator accumulator,
            CharacterTestArcMineRuntimeState state,
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
            CharacterTestArcMineRuntimeState state,
            int casterEntityIdFilter,
            bool excludeCasterEntity)
        {
            accumulator.BeginObject(
                NetworkRootStateFieldIds.ArcMineState,
                RuntimeHashSchemaIds.NetworkSnapshotArcMine,
                state != null ? state.SchemaVersion : CharacterTestArcMineRuntimeState.CurrentSchemaVersion,
                state != null);
            if (state == null)
            {
                accumulator.EndObject();
                return;
            }
            accumulator.AddInt(
                NetworkArcMineSnapshotHashFields.Schema,
                state.SchemaVersion);
            accumulator.AddInt(NetworkArcMineSnapshotHashFields.ModuleId, state.ModuleId);
            accumulator.AddInt(NetworkArcMineSnapshotHashFields.NextRuntimeId, state.NextRuntimeId);
            IReadOnlyList<CharacterTestArcMineSnapshot> mines = state.ActiveMines;
            int count = mines != null ? mines.Count : 0;
            if (casterEntityIdFilter <= 0)
            {
                accumulator.BeginArray(NetworkArcMineSnapshotHashFields.Mines, count);
                for (int i = 0; i < count; i++)
                {
                    accumulator.BeginElement(i);
                    AddSnapshotHash(accumulator, i, mines[i]);
                    accumulator.EndElement();
                }
                accumulator.EndArray();
                accumulator.EndObject();
                return;
            }

            int includedCount = 0;
            for (int i = 0; i < count; i++)
            {
                bool casterMatches = mines[i].CasterEntityId == casterEntityIdFilter;
                if ((!excludeCasterEntity && casterMatches) || (excludeCasterEntity && !casterMatches))
                    includedCount++;
            }

            accumulator.BeginArray(NetworkArcMineSnapshotHashFields.Mines, includedCount);
            int outputIndex = 0;
            for (int i = 0; i < count; i++)
            {
                bool casterMatches = mines[i].CasterEntityId == casterEntityIdFilter;
                if ((!excludeCasterEntity && !casterMatches) || (excludeCasterEntity && casterMatches))
                    continue;

                accumulator.BeginElement(outputIndex);
                AddSnapshotHash(accumulator, outputIndex, mines[i]);
                accumulator.EndElement();
                outputIndex++;
            }
            accumulator.EndArray();
            accumulator.EndObject();
        }

        private static void AddSnapshotHash(
            RuntimeHashAccumulator accumulator,
            int index,
            CharacterTestArcMineSnapshot mine)
        {
            accumulator.AddInt(NetworkArcMineEntryHashFields.Index, index);
            accumulator.AddInt(NetworkArcMineEntryHashFields.RuntimeId, mine.RuntimeId);
            accumulator.AddInt(NetworkArcMineEntryHashFields.CasterEntityId, mine.CasterEntityId);
            accumulator.AddInt(NetworkArcMineEntryHashFields.SourceCombatEntityId, mine.SourceCombatEntityId);
            accumulator.AddInt(NetworkArcMineEntryHashFields.ActionInstanceId, mine.ActionInstanceId);
            accumulator.AddInt(NetworkArcMineEntryHashFields.SourceOrder, mine.SourceOrder);
            accumulator.AddInt(NetworkArcMineEntryHashFields.LocalFrame, mine.LocalFrame);
            accumulator.AddLong(NetworkArcMineEntryHashFields.CreatedFrame, mine.CreatedFrame);
            accumulator.AddLong(NetworkArcMineEntryHashFields.ArmedFrame, mine.ArmedFrame);
            accumulator.AddLong(NetworkArcMineEntryHashFields.ExpireFrame, mine.ExpireFrame);
            accumulator.AddLong(NetworkArcMineEntryHashFields.FuseStartedFrame, mine.FuseStartedFrame);
            accumulator.AddLong(NetworkArcMineEntryHashFields.DetonateFrame, mine.DetonateFrame);
            accumulator.AddInt(NetworkArcMineEntryHashFields.TriggerTargetEntityId, mine.TriggerTargetEntityId);
            accumulator.AddLong(NetworkArcMineEntryHashFields.CenterXRaw, mine.CenterXRaw);
            accumulator.AddLong(NetworkArcMineEntryHashFields.CenterYRaw, mine.CenterYRaw);
            accumulator.AddLong(NetworkArcMineEntryHashFields.CenterZRaw, mine.CenterZRaw);
            accumulator.AddLong(NetworkArcMineEntryHashFields.ForwardXRaw, mine.ForwardXRaw);
            accumulator.AddLong(NetworkArcMineEntryHashFields.ForwardYRaw, mine.ForwardYRaw);
            accumulator.AddLong(NetworkArcMineEntryHashFields.ForwardZRaw, mine.ForwardZRaw);
            accumulator.AddInt(NetworkArcMineEntryHashFields.TraceId, mine.TraceId);
        }




    }

    public sealed class CharacterTestNetworkArcMineRuntimeSnapshot
    {
        public int SchemaVersion { get; set; }
        public int ModuleId { get; set; }
        public int NextRuntimeId { get; set; }
        public CharacterTestNetworkArcMineSnapshot[] ActiveMines { get; set; }
        public int ActiveCount => ActiveMines != null ? ActiveMines.Length : 0;

        internal static CharacterTestNetworkArcMineRuntimeSnapshot FromState(
            CharacterTestArcMineRuntimeState state)
        {
            if (state == null)
                return null;

            return new CharacterTestNetworkArcMineRuntimeSnapshot
            {
                SchemaVersion = state.SchemaVersion,
                ModuleId = state.ModuleId,
                NextRuntimeId = state.NextRuntimeId,
                ActiveMines = CharacterTestNetworkArcMineSnapshot.FromSnapshots(state.ActiveMines)
            };
        }

        public CharacterTestNetworkArcMineRuntimeSnapshot CloneForRollback()
        {
            return new CharacterTestNetworkArcMineRuntimeSnapshot
            {
                SchemaVersion = SchemaVersion,
                ModuleId = ModuleId,
                NextRuntimeId = NextRuntimeId,
                ActiveMines = CharacterTestNetworkArcMineSnapshot.CloneArray(ActiveMines)
            };
        }

        internal CharacterTestArcMineRuntimeState ToState()
        {
            return new CharacterTestArcMineRuntimeState(
                SchemaVersion,
                ModuleId,
                NextRuntimeId,
                CharacterTestNetworkArcMineSnapshot.ToSnapshots(ActiveMines));
        }
    }

    public sealed class CharacterTestNetworkArcMineSnapshot
    {
        public int RuntimeId { get; set; }
        public int CasterEntityId { get; set; }
        public int SourceCombatEntityId { get; set; }
        public int ActionInstanceId { get; set; }
        public int SourceOrder { get; set; }
        public int LocalFrame { get; set; }
        public long CreatedFrame { get; set; }
        public long ArmedFrame { get; set; }
        public long ExpireFrame { get; set; }
        public long FuseStartedFrame { get; set; }
        public long DetonateFrame { get; set; }
        public int TriggerTargetEntityId { get; set; }
        public long CenterXRaw { get; set; }
        public long CenterYRaw { get; set; }
        public long CenterZRaw { get; set; }
        public long ForwardXRaw { get; set; }
        public long ForwardYRaw { get; set; }
        public long ForwardZRaw { get; set; }
        public int TraceId { get; set; }

        internal static CharacterTestNetworkArcMineSnapshot[] FromSnapshots(
            IReadOnlyList<CharacterTestArcMineSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
                return Array.Empty<CharacterTestNetworkArcMineSnapshot>();

            var result = new CharacterTestNetworkArcMineSnapshot[snapshots.Count];
            for (int i = 0; i < snapshots.Count; i++)
                result[i] = FromSnapshot(snapshots[i]);
            return result;
        }

        internal static List<CharacterTestArcMineSnapshot> ToSnapshots(
            CharacterTestNetworkArcMineSnapshot[] snapshots)
        {
            var result = new List<CharacterTestArcMineSnapshot>();
            if (snapshots == null || snapshots.Length == 0)
                return result;

            for (int i = 0; i < snapshots.Length; i++)
            {
                CharacterTestNetworkArcMineSnapshot snapshot = snapshots[i];
                if (snapshot == null || snapshot.RuntimeId <= 0 || snapshot.CasterEntityId <= 0)
                    continue;

                result.Add(snapshot.ToSnapshot());
            }

            return result;
        }

        public static CharacterTestNetworkArcMineSnapshot[] CloneArray(
            CharacterTestNetworkArcMineSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
                return Array.Empty<CharacterTestNetworkArcMineSnapshot>();

            var result = new CharacterTestNetworkArcMineSnapshot[snapshots.Length];
            for (int i = 0; i < snapshots.Length; i++)
                result[i] = snapshots[i]?.CloneForRollback();
            return result;
        }

        public CharacterTestNetworkArcMineSnapshot CloneForRollback()
        {
            return (CharacterTestNetworkArcMineSnapshot)MemberwiseClone();
        }

        private static CharacterTestNetworkArcMineSnapshot FromSnapshot(
            CharacterTestArcMineSnapshot snapshot)
        {
            return new CharacterTestNetworkArcMineSnapshot
            {
                RuntimeId = snapshot.RuntimeId,
                CasterEntityId = snapshot.CasterEntityId,
                SourceCombatEntityId = snapshot.SourceCombatEntityId,
                ActionInstanceId = snapshot.ActionInstanceId,
                SourceOrder = snapshot.SourceOrder,
                LocalFrame = snapshot.LocalFrame,
                CreatedFrame = snapshot.CreatedFrame,
                ArmedFrame = snapshot.ArmedFrame,
                ExpireFrame = snapshot.ExpireFrame,
                FuseStartedFrame = snapshot.FuseStartedFrame,
                DetonateFrame = snapshot.DetonateFrame,
                TriggerTargetEntityId = snapshot.TriggerTargetEntityId,
                CenterXRaw = snapshot.CenterXRaw,
                CenterYRaw = snapshot.CenterYRaw,
                CenterZRaw = snapshot.CenterZRaw,
                ForwardXRaw = snapshot.ForwardXRaw,
                ForwardYRaw = snapshot.ForwardYRaw,
                ForwardZRaw = snapshot.ForwardZRaw,
                TraceId = snapshot.TraceId
            };
        }

        private CharacterTestArcMineSnapshot ToSnapshot()
        {
            return new CharacterTestArcMineSnapshot(
                RuntimeId,
                CasterEntityId,
                SourceCombatEntityId,
                ActionInstanceId,
                SourceOrder,
                LocalFrame,
                CreatedFrame,
                ArmedFrame,
                ExpireFrame,
                FuseStartedFrame,
                DetonateFrame,
                TriggerTargetEntityId,
                CenterXRaw,
                CenterYRaw,
                CenterZRaw,
                ForwardXRaw,
                ForwardYRaw,
                ForwardZRaw,
                TraceId);
        }
    }

    internal static class NetworkArcMineSnapshotHashFields
    {
        public const int Schema = 1;
        public const int NextRuntimeId = 2;
        public const int Mines = 3;
        public const int ModuleId = 4;
    }

    internal static class NetworkArcMineEntryHashFields
    {
        public const int Index = 1;
        public const int RuntimeId = 2;
        public const int CasterEntityId = 3;
        public const int SourceCombatEntityId = 4;
        public const int ActionInstanceId = 5;
        public const int SourceOrder = 6;
        public const int LocalFrame = 7;
        public const int CreatedFrame = 8;
        public const int ArmedFrame = 9;
        public const int ExpireFrame = 10;
        public const int FuseStartedFrame = 11;
        public const int DetonateFrame = 12;
        public const int TriggerTargetEntityId = 13;
        public const int CenterXRaw = 14;
        public const int CenterYRaw = 15;
        public const int CenterZRaw = 16;
        public const int ForwardXRaw = 17;
        public const int ForwardYRaw = 18;
        public const int ForwardZRaw = 19;
        public const int TraceId = 20;
    }
}

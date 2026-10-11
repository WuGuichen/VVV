using System;
using System.Collections.Generic;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestNetworkHookPullSnapshotCodec
    {
        public static CharacterTestNetworkHookPullRuntimeSnapshot Capture(
            CharacterTestCharacterCombatTraceSystem combatTrace)
        {
            return CharacterTestNetworkHookPullRuntimeSnapshot.FromState(
                combatTrace?.CreateHookPullStateSnapshot());
        }

        public static NetworkSnapshotApplyResult Apply(
            CharacterTestCharacterCombatTraceSystem combatTrace,
            CharacterTestNetworkHookPullRuntimeSnapshot hookPulls)
        {
            if (combatTrace == null)
            {
                if (hookPulls == null || hookPulls.ActiveCount == 0)
                    return NetworkSnapshotApplyResult.Succeeded();

                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest hook pull snapshot cannot be applied because combat trace system is missing.");
            }

            CharacterTestHookPullRuntimeState state;
            try
            {
                state = hookPulls?.ToState();
            }
            catch (Exception exception)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest hook pull snapshot parse failed: " + exception.Message);
            }

            if (!combatTrace.TryApplyHookPullStateSnapshot(state, out string failure))
                return NetworkSnapshotApplyResult.Failed("CharacterTest hook pull snapshot apply failed: " + failure);

            return NetworkSnapshotApplyResult.Succeeded();
        }

        public static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestHookPullRuntimeState state,
            int casterEntityIdFilter = 0)
        {
            AddStateHash(
                accumulator,
                state,
                casterEntityIdFilter,
                excludeCasterEntity: false);
        }

        public static void AddStateHashExcludingCaster(
            RuntimeHashAccumulator accumulator,
            CharacterTestHookPullRuntimeState state,
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
            CharacterTestHookPullRuntimeState state,
            int casterEntityIdFilter,
            bool excludeCasterEntity)
        {
            accumulator.AddInt(
                NetworkHookPullSnapshotHashFields.Schema,
                state != null ? state.SchemaVersion : CharacterTestHookPullRuntimeState.CurrentSchemaVersion);
            IReadOnlyList<CharacterTestHookPullSnapshot> pulls = state != null ? state.ActivePulls : null;
            int count = pulls != null ? pulls.Count : 0;
            if (casterEntityIdFilter <= 0)
            {
                accumulator.BeginArray(NetworkHookPullSnapshotHashFields.Pulls, count);
                for (int i = 0; i < count; i++)
                {
                    accumulator.BeginElement(i);
                    AddSnapshotHash(accumulator, i, pulls[i]);
                    accumulator.EndElement();
                }
                accumulator.EndArray();
                return;
            }

            int includedCount = 0;
            for (int i = 0; i < count; i++)
            {
                bool casterMatches = pulls[i].CasterEntityId == casterEntityIdFilter;
                if ((!excludeCasterEntity && casterMatches) || (excludeCasterEntity && !casterMatches))
                    includedCount++;
            }

            accumulator.BeginArray(NetworkHookPullSnapshotHashFields.Pulls, includedCount);
            int outputIndex = 0;
            for (int i = 0; i < count; i++)
            {
                bool casterMatches = pulls[i].CasterEntityId == casterEntityIdFilter;
                if ((!excludeCasterEntity && !casterMatches) || (excludeCasterEntity && casterMatches))
                    continue;

                accumulator.BeginElement(outputIndex);
                AddSnapshotHash(accumulator, outputIndex, pulls[i]);
                accumulator.EndElement();
                outputIndex++;
            }
            accumulator.EndArray();
        }

        private static void AddSnapshotHash(
            RuntimeHashAccumulator accumulator,
            int index,
            CharacterTestHookPullSnapshot pull)
        {
            accumulator.AddInt(NetworkHookPullEntryHashFields.Index, index);
            accumulator.AddInt(NetworkHookPullEntryHashFields.CasterEntityId, pull.CasterEntityId);
            accumulator.AddInt(NetworkHookPullEntryHashFields.TargetEntityId, pull.TargetEntityId);
            accumulator.AddLong(NetworkHookPullEntryHashFields.StartFrame, pull.StartFrame);
            accumulator.AddLong(NetworkHookPullEntryHashFields.EndFrame, pull.EndFrame);
            accumulator.AddInt(NetworkHookPullEntryHashFields.ActionInstanceId, pull.ActionInstanceId);
            accumulator.AddInt(NetworkHookPullEntryHashFields.ProjectileId, pull.ProjectileId);
            accumulator.AddInt(NetworkHookPullEntryHashFields.StableEventId, pull.StableEventId);
            accumulator.AddInt(NetworkHookPullEntryHashFields.StopDistanceMillimeters, pull.StopDistanceMillimeters);
            accumulator.AddInt(NetworkHookPullEntryHashFields.AnchorForwardOffsetMillimeters, pull.AnchorForwardOffsetMillimeters);
            accumulator.AddInt(NetworkHookPullEntryHashFields.AccelerationMillimetersPerSecondSquared, pull.AccelerationMillimetersPerSecondSquared);
            accumulator.AddInt(NetworkHookPullEntryHashFields.TerminalSpeedMillimetersPerSecond, pull.TerminalSpeedMillimetersPerSecond);
        }




    }

    public sealed class CharacterTestNetworkHookPullRuntimeSnapshot
    {
        public int SchemaVersion { get; set; }
        public string ModuleId { get; set; }
        public CharacterTestNetworkHookPullSnapshot[] ActivePulls { get; set; }
        public int ActiveCount => ActivePulls != null ? ActivePulls.Length : 0;

        internal static CharacterTestNetworkHookPullRuntimeSnapshot FromState(
            CharacterTestHookPullRuntimeState state)
        {
            if (state == null)
                return null;

            return new CharacterTestNetworkHookPullRuntimeSnapshot
            {
                SchemaVersion = state.SchemaVersion,
                ModuleId = state.ModuleId,
                ActivePulls = CharacterTestNetworkHookPullSnapshot.FromSnapshots(state.ActivePulls)
            };
        }

        public CharacterTestNetworkHookPullRuntimeSnapshot CloneForRollback()
        {
            return new CharacterTestNetworkHookPullRuntimeSnapshot
            {
                SchemaVersion = SchemaVersion,
                ModuleId = ModuleId,
                ActivePulls = CharacterTestNetworkHookPullSnapshot.CloneArray(ActivePulls)
            };
        }

        internal CharacterTestHookPullRuntimeState ToState()
        {
            return new CharacterTestHookPullRuntimeState(
                SchemaVersion,
                string.IsNullOrWhiteSpace(ModuleId)
                    ? CharacterTestHookPullRuntimeState.DefaultModuleId
                    : ModuleId,
                CharacterTestNetworkHookPullSnapshot.ToSnapshots(ActivePulls));
        }
    }

    public sealed class CharacterTestNetworkHookPullSnapshot
    {
        public int CasterEntityId { get; set; }
        public int TargetEntityId { get; set; }
        public long StartFrame { get; set; }
        public long EndFrame { get; set; }
        public int ActionInstanceId { get; set; }
        public int ProjectileId { get; set; }
        public int StableEventId { get; set; }
        public int StopDistanceMillimeters { get; set; }
        public int AnchorForwardOffsetMillimeters { get; set; }
        public int AccelerationMillimetersPerSecondSquared { get; set; }
        public int TerminalSpeedMillimetersPerSecond { get; set; }

        internal static CharacterTestNetworkHookPullSnapshot[] FromSnapshots(
            IReadOnlyList<CharacterTestHookPullSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
                return Array.Empty<CharacterTestNetworkHookPullSnapshot>();

            var result = new CharacterTestNetworkHookPullSnapshot[snapshots.Count];
            for (int i = 0; i < snapshots.Count; i++)
                result[i] = FromSnapshot(snapshots[i]);
            return result;
        }

        internal static List<CharacterTestHookPullSnapshot> ToSnapshots(
            CharacterTestNetworkHookPullSnapshot[] snapshots)
        {
            var result = new List<CharacterTestHookPullSnapshot>();
            if (snapshots == null || snapshots.Length == 0)
                return result;

            for (int i = 0; i < snapshots.Length; i++)
            {
                CharacterTestNetworkHookPullSnapshot snapshot = snapshots[i];
                if (snapshot == null || snapshot.CasterEntityId <= 0 || snapshot.TargetEntityId <= 0)
                    continue;

                result.Add(snapshot.ToSnapshot());
            }

            return result;
        }

        public static CharacterTestNetworkHookPullSnapshot[] CloneArray(
            CharacterTestNetworkHookPullSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
                return Array.Empty<CharacterTestNetworkHookPullSnapshot>();

            var result = new CharacterTestNetworkHookPullSnapshot[snapshots.Length];
            for (int i = 0; i < snapshots.Length; i++)
                result[i] = snapshots[i]?.CloneForRollback();
            return result;
        }

        public CharacterTestNetworkHookPullSnapshot CloneForRollback()
        {
            return (CharacterTestNetworkHookPullSnapshot)MemberwiseClone();
        }

        private static CharacterTestNetworkHookPullSnapshot FromSnapshot(
            CharacterTestHookPullSnapshot snapshot)
        {
            return new CharacterTestNetworkHookPullSnapshot
            {
                CasterEntityId = snapshot.CasterEntityId,
                TargetEntityId = snapshot.TargetEntityId,
                StartFrame = snapshot.StartFrame,
                EndFrame = snapshot.EndFrame,
                ActionInstanceId = snapshot.ActionInstanceId,
                ProjectileId = snapshot.ProjectileId,
                StableEventId = snapshot.StableEventId,
                StopDistanceMillimeters = snapshot.StopDistanceMillimeters,
                AnchorForwardOffsetMillimeters = snapshot.AnchorForwardOffsetMillimeters,
                AccelerationMillimetersPerSecondSquared =
                    snapshot.AccelerationMillimetersPerSecondSquared,
                TerminalSpeedMillimetersPerSecond = snapshot.TerminalSpeedMillimetersPerSecond
            };
        }

        private CharacterTestHookPullSnapshot ToSnapshot()
        {
            return new CharacterTestHookPullSnapshot(
                CasterEntityId,
                TargetEntityId,
                StartFrame,
                EndFrame,
                ActionInstanceId,
                ProjectileId,
                StableEventId,
                StopDistanceMillimeters,
                AnchorForwardOffsetMillimeters,
                AccelerationMillimetersPerSecondSquared,
                TerminalSpeedMillimetersPerSecond);
        }
    }

    internal static class NetworkHookPullSnapshotHashFields
    {
        public const int Schema = 1;
        public const int Pulls = 2;
    }

    internal static class NetworkHookPullEntryHashFields
    {
        public const int Index = 1;
        public const int CasterEntityId = 2;
        public const int TargetEntityId = 3;
        public const int StartFrame = 4;
        public const int EndFrame = 5;
        public const int ActionInstanceId = 6;
        public const int ProjectileId = 7;
        public const int StableEventId = 8;
        public const int StopDistanceMillimeters = 9;
        public const int AnchorForwardOffsetMillimeters = 10;
        public const int AccelerationMillimetersPerSecondSquared = 11;
        public const int TerminalSpeedMillimetersPerSecond = 12;
    }
}

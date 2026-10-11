using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Shields;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestNetworkAreaConfinementSnapshotCodec
    {
        public static CharacterTestNetworkAreaConfinementRuntimeSnapshot Capture(
            CharacterTestCharacterCombatTraceSystem combatTrace)
        {
            return CharacterTestNetworkAreaConfinementRuntimeSnapshot.FromState(
                combatTrace?.CreateAreaConfinementStateSnapshot());
        }

        public static NetworkSnapshotApplyResult Apply(
            CharacterTestCharacterCombatTraceSystem combatTrace,
            CharacterTestNetworkAreaConfinementRuntimeSnapshot areaConfinement)
        {
            if (combatTrace == null)
            {
                if (areaConfinement == null || areaConfinement.TotalCount == 0)
                    return NetworkSnapshotApplyResult.Succeeded();

                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest Area Confinement snapshot cannot be applied because combat trace system is missing.");
            }

            CharacterTestAreaConfinementRuntimeState state;
            try
            {
                state = areaConfinement?.ToState();
            }
            catch (Exception exception)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest Area Confinement snapshot parse failed: " + exception.Message);
            }

            if (!combatTrace.TryApplyAreaConfinementStateSnapshot(state, out string failure))
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest Area Confinement snapshot apply failed: " + failure);
            }

            return NetworkSnapshotApplyResult.Succeeded();
        }

        public static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestAreaConfinementRuntimeState state,
            int casterEntityIdFilter = 0)
        {
            AddStateHash(accumulator, state, Math.Max(0, casterEntityIdFilter), excludeCasterEntity: false);
        }

        public static void AddStateHashExcludingCaster(
            RuntimeHashAccumulator accumulator,
            CharacterTestAreaConfinementRuntimeState state,
            int excludedCasterEntityId)
        {
            AddStateHash(accumulator, state, Math.Max(0, excludedCasterEntityId), excludeCasterEntity: true);
        }

        private static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestAreaConfinementRuntimeState state,
            int casterEntityIdFilter,
            bool excludeCasterEntity)
        {
            accumulator.BeginObject(
                NetworkRootStateFieldIds.AreaConfinementState,
                RuntimeHashSchemaIds.NetworkSnapshotAreaConfinement,
                state != null ? state.SchemaVersion : CharacterTestAreaConfinementRuntimeState.CurrentSchemaVersion,
                state != null);
            if (state == null)
            {
                accumulator.EndObject();
                return;
            }
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F1,
                state.SchemaVersion);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F2,
                state.NextPendingChannelId);
            AddPendingHash(
                accumulator,
                state.PendingChannels,
                casterEntityIdFilter,
                excludeCasterEntity);
            AddSourceHash(
                accumulator,
                state.SourceRuntime,
                casterEntityIdFilter,
                excludeCasterEntity);
            accumulator.EndObject();
        }

        private static void AddPendingHash(
            RuntimeHashAccumulator accumulator,
            IReadOnlyList<CharacterTestAreaConfinementPendingChannelSnapshot> channels,
            int casterEntityIdFilter,
            bool excludeCasterEntity)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F85, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            int count = channels != null ? channels.Count : 0;
            if (casterEntityIdFilter <= 0)
            {
                accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F3, count);
                for (int i = 0; i < count; i++)
                    AddPendingSnapshotHash(accumulator, i, channels[i]);
                { accumulator.EndObject(); return; }
            }

            int matched = 0;
            for (int i = 0; i < count; i++)
            {
                bool casterMatches = channels[i].CasterEntityId == casterEntityIdFilter;
                if ((!excludeCasterEntity && casterMatches) || (excludeCasterEntity && !casterMatches))
                    matched++;
            }

            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F3, matched);
            int index = 0;
            for (int i = 0; i < count; i++)
            {
                bool casterMatches = channels[i].CasterEntityId == casterEntityIdFilter;
                if ((!excludeCasterEntity && !casterMatches) || (excludeCasterEntity && casterMatches))
                    continue;

                AddPendingSnapshotHash(accumulator, index, channels[i]);
                index++;
            }
            accumulator.EndObject();
        }

        private static void AddSourceHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestMechanicSourceRuntimeState sourceState,
            int casterEntityIdFilter,
            bool excludeCasterEntity)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F86, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F4,
                sourceState != null
                    ? sourceState.SchemaVersion
                    : CharacterTestMechanicSourceRuntimeState.CurrentSchemaVersion);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F5,
                sourceState != null ? sourceState.NextRuntimeSourceId : 1);
            IReadOnlyList<CharacterTestMechanicSourceSnapshot> sources =
                sourceState != null ? sourceState.ActiveSources : null;
            int count = sources != null ? sources.Count : 0;
            if (casterEntityIdFilter <= 0)
            {
                accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F6, count);
                for (int i = 0; i < count; i++)
                    AddSourceSnapshotHash(accumulator, i, sources[i]);
                { accumulator.EndObject(); return; }
            }

            int matched = 0;
            for (int i = 0; i < count; i++)
            {
                bool casterMatches = sources[i].SourceCombatEntityId == casterEntityIdFilter;
                if ((!excludeCasterEntity && casterMatches) || (excludeCasterEntity && !casterMatches))
                    matched++;
            }

            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F6, matched);
            int index = 0;
            for (int i = 0; i < count; i++)
            {
                bool casterMatches = sources[i].SourceCombatEntityId == casterEntityIdFilter;
                if ((!excludeCasterEntity && !casterMatches) || (excludeCasterEntity && casterMatches))
                    continue;

                AddSourceSnapshotHash(accumulator, index, sources[i]);
                index++;
            }
            accumulator.EndObject();
        }

        private static void AddPendingSnapshotHash(
            RuntimeHashAccumulator accumulator,
            int index,
            CharacterTestAreaConfinementPendingChannelSnapshot channel)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F87, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F7, index);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F8, channel.PendingChannelId);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F9, channel.CasterEntityId);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F10,
                channel.SourceGameplayEntityIndex);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F11,
                channel.SourceGameplayEntityGeneration);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F12, channel.SourceCombatEntityId);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F13, channel.CenterXRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F14, channel.CenterYRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F15, channel.CenterZRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F16, channel.ForwardXRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F17, channel.ForwardYRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F18, channel.ForwardZRaw);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F19, (int)channel.LandingMode);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F20, channel.StartFrame);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F21, channel.ActivationFrame);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F22, channel.LocalFrame);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F23, channel.SourceOrder);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F24, channel.TraceId);
            accumulator.EndObject();
        }

        private static void AddSourceSnapshotHash(
            RuntimeHashAccumulator accumulator,
            int index,
            CharacterTestMechanicSourceSnapshot source)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F88, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F25, index);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F26, source.RuntimeSourceId);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F27, source.SourceDefinitionId);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F28,
                source.SourceGameplayEntityIndex);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F29,
                source.SourceGameplayEntityGeneration);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F30, source.SourceCombatEntityId);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F31, source.CenterXRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F32, source.CenterYRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F33, source.CenterZRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F34, source.StartFrame);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F35, source.EndFrame);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F36, source.TraceId);
            accumulator.EndObject();
        }
    }

    public sealed class CharacterTestNetworkAreaConfinementRuntimeSnapshot
    {
        public int SchemaVersion { get; set; }
        public int ModuleId { get; set; }
        public int NextPendingChannelId { get; set; }
        public CharacterTestNetworkAreaConfinementPendingChannelSnapshot[] PendingChannels { get; set; }
        public CharacterTestNetworkMechanicSourceRuntimeSnapshot SourceRuntime { get; set; }
        public int PendingCount => PendingChannels != null ? PendingChannels.Length : 0;
        public int ActiveZoneCount => SourceRuntime != null ? SourceRuntime.ActiveCount : 0;
        public int TotalCount => PendingCount + ActiveZoneCount;

        internal static CharacterTestNetworkAreaConfinementRuntimeSnapshot FromState(
            CharacterTestAreaConfinementRuntimeState state)
        {
            if (state == null)
                return null;

            return new CharacterTestNetworkAreaConfinementRuntimeSnapshot
            {
                SchemaVersion = state.SchemaVersion,
                ModuleId = state.ModuleId,
                NextPendingChannelId = state.NextPendingChannelId,
                PendingChannels =
                    CharacterTestNetworkAreaConfinementPendingChannelSnapshot.FromSnapshots(
                        state.PendingChannels),
                SourceRuntime = CharacterTestNetworkMechanicSourceRuntimeSnapshot.FromState(
                    state.SourceRuntime)
            };
        }

        public CharacterTestNetworkAreaConfinementRuntimeSnapshot CloneForRollback()
        {
            return new CharacterTestNetworkAreaConfinementRuntimeSnapshot
            {
                SchemaVersion = SchemaVersion,
                ModuleId = ModuleId,
                NextPendingChannelId = NextPendingChannelId,
                PendingChannels =
                    CharacterTestNetworkAreaConfinementPendingChannelSnapshot.CloneArray(PendingChannels),
                SourceRuntime = SourceRuntime?.CloneForRollback()
            };
        }

        internal CharacterTestAreaConfinementRuntimeState ToState()
        {
            return new CharacterTestAreaConfinementRuntimeState(
                SchemaVersion,
                ModuleId <= 0
                    ? CharacterTestAreaConfinementRuntimeState.DefaultModuleId
                    : ModuleId,
                NextPendingChannelId,
                CharacterTestNetworkAreaConfinementPendingChannelSnapshot.ToSnapshots(PendingChannels),
                SourceRuntime?.ToState());
        }
    }

    public sealed class CharacterTestNetworkAreaConfinementPendingChannelSnapshot
    {
        public int PendingChannelId { get; set; }
        public int CasterEntityId { get; set; }
        public int SourceGameplayEntityIndex { get; set; }
        public int SourceGameplayEntityGeneration { get; set; }
        public int SourceCombatEntityId { get; set; }
        public long CenterXRaw { get; set; }
        public long CenterYRaw { get; set; }
        public long CenterZRaw { get; set; }
        public long ForwardXRaw { get; set; }
        public long ForwardYRaw { get; set; }
        public long ForwardZRaw { get; set; }
        public CharacterTestAreaConfinementLandingMode LandingMode { get; set; }
        public long StartFrame { get; set; }
        public long ActivationFrame { get; set; }
        public int LocalFrame { get; set; }
        public int SourceOrder { get; set; }
        public int TraceId { get; set; }

        internal static CharacterTestNetworkAreaConfinementPendingChannelSnapshot[] FromSnapshots(
            IReadOnlyList<CharacterTestAreaConfinementPendingChannelSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
                return Array.Empty<CharacterTestNetworkAreaConfinementPendingChannelSnapshot>();

            var result = new CharacterTestNetworkAreaConfinementPendingChannelSnapshot[snapshots.Count];
            for (int i = 0; i < snapshots.Count; i++)
                result[i] = FromSnapshot(snapshots[i]);
            return result;
        }

        internal static List<CharacterTestAreaConfinementPendingChannelSnapshot> ToSnapshots(
            CharacterTestNetworkAreaConfinementPendingChannelSnapshot[] snapshots)
        {
            var result = new List<CharacterTestAreaConfinementPendingChannelSnapshot>();
            if (snapshots == null || snapshots.Length == 0)
                return result;

            for (int i = 0; i < snapshots.Length; i++)
            {
                CharacterTestNetworkAreaConfinementPendingChannelSnapshot snapshot = snapshots[i];
                if (snapshot == null
                    || snapshot.PendingChannelId <= 0
                    || snapshot.CasterEntityId <= 0)
                {
                    continue;
                }

                result.Add(snapshot.ToSnapshot());
            }

            return result;
        }

        public static CharacterTestNetworkAreaConfinementPendingChannelSnapshot[] CloneArray(
            CharacterTestNetworkAreaConfinementPendingChannelSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
                return Array.Empty<CharacterTestNetworkAreaConfinementPendingChannelSnapshot>();

            var result = new CharacterTestNetworkAreaConfinementPendingChannelSnapshot[snapshots.Length];
            for (int i = 0; i < snapshots.Length; i++)
                result[i] = snapshots[i]?.CloneForRollback();
            return result;
        }

        public CharacterTestNetworkAreaConfinementPendingChannelSnapshot CloneForRollback()
        {
            return (CharacterTestNetworkAreaConfinementPendingChannelSnapshot)MemberwiseClone();
        }

        private static CharacterTestNetworkAreaConfinementPendingChannelSnapshot FromSnapshot(
            CharacterTestAreaConfinementPendingChannelSnapshot snapshot)
        {
            return new CharacterTestNetworkAreaConfinementPendingChannelSnapshot
            {
                PendingChannelId = snapshot.PendingChannelId,
                CasterEntityId = snapshot.CasterEntityId,
                SourceGameplayEntityIndex = snapshot.SourceGameplayEntityIndex,
                SourceGameplayEntityGeneration = snapshot.SourceGameplayEntityGeneration,
                SourceCombatEntityId = snapshot.SourceCombatEntityId,
                CenterXRaw = snapshot.CenterXRaw,
                CenterYRaw = snapshot.CenterYRaw,
                CenterZRaw = snapshot.CenterZRaw,
                ForwardXRaw = snapshot.ForwardXRaw,
                ForwardYRaw = snapshot.ForwardYRaw,
                ForwardZRaw = snapshot.ForwardZRaw,
                LandingMode = snapshot.LandingMode,
                StartFrame = snapshot.StartFrame,
                ActivationFrame = snapshot.ActivationFrame,
                LocalFrame = snapshot.LocalFrame,
                SourceOrder = snapshot.SourceOrder,
                TraceId = snapshot.TraceId
            };
        }

        private CharacterTestAreaConfinementPendingChannelSnapshot ToSnapshot()
        {
            return new CharacterTestAreaConfinementPendingChannelSnapshot(
                PendingChannelId,
                CasterEntityId,
                SourceGameplayEntityIndex,
                SourceGameplayEntityGeneration,
                SourceCombatEntityId,
                CenterXRaw,
                CenterYRaw,
                CenterZRaw,
                ForwardXRaw,
                ForwardYRaw,
                ForwardZRaw,
                LandingMode,
                StartFrame,
                ActivationFrame,
                LocalFrame,
                SourceOrder,
                TraceId);
        }
    }

    public sealed class CharacterTestNetworkMechanicSourceRuntimeSnapshot
    {
        public int SchemaVersion { get; set; }
        public int ModuleId { get; set; }
        public int NextRuntimeSourceId { get; set; }
        public CharacterTestNetworkMechanicSourceSnapshot[] ActiveSources { get; set; }
        public int ActiveCount => ActiveSources != null ? ActiveSources.Length : 0;

        internal static CharacterTestNetworkMechanicSourceRuntimeSnapshot FromState(
            CharacterTestMechanicSourceRuntimeState state)
        {
            if (state == null)
                return null;

            return new CharacterTestNetworkMechanicSourceRuntimeSnapshot
            {
                SchemaVersion = state.SchemaVersion,
                ModuleId = state.ModuleId,
                NextRuntimeSourceId = state.NextRuntimeSourceId,
                ActiveSources = CharacterTestNetworkMechanicSourceSnapshot.FromSnapshots(state.ActiveSources)
            };
        }

        public CharacterTestNetworkMechanicSourceRuntimeSnapshot CloneForRollback()
        {
            return new CharacterTestNetworkMechanicSourceRuntimeSnapshot
            {
                SchemaVersion = SchemaVersion,
                ModuleId = ModuleId,
                NextRuntimeSourceId = NextRuntimeSourceId,
                ActiveSources = CharacterTestNetworkMechanicSourceSnapshot.CloneArray(ActiveSources)
            };
        }

        internal CharacterTestMechanicSourceRuntimeState ToState()
        {
            return new CharacterTestMechanicSourceRuntimeState(
                SchemaVersion,
                ModuleId <= 0
                    ? CharacterTestAreaConfinementRuntimeState.SourceRuntimeModuleId
                    : ModuleId,
                NextRuntimeSourceId,
                CharacterTestNetworkMechanicSourceSnapshot.ToSnapshots(ActiveSources));
        }
    }

    public sealed class CharacterTestNetworkMechanicSourceSnapshot
    {
        public int RuntimeSourceId { get; set; }
        public int SourceDefinitionId { get; set; }
        public int SourceGameplayEntityIndex { get; set; }
        public int SourceGameplayEntityGeneration { get; set; }
        public int SourceCombatEntityId { get; set; }
        public long CenterXRaw { get; set; }
        public long CenterYRaw { get; set; }
        public long CenterZRaw { get; set; }
        public long StartFrame { get; set; }
        public long EndFrame { get; set; }
        public int TraceId { get; set; }

        internal static CharacterTestNetworkMechanicSourceSnapshot[] FromSnapshots(
            IReadOnlyList<CharacterTestMechanicSourceSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
                return Array.Empty<CharacterTestNetworkMechanicSourceSnapshot>();

            var result = new CharacterTestNetworkMechanicSourceSnapshot[snapshots.Count];
            for (int i = 0; i < snapshots.Count; i++)
                result[i] = FromSnapshot(snapshots[i]);
            return result;
        }

        internal static List<CharacterTestMechanicSourceSnapshot> ToSnapshots(
            CharacterTestNetworkMechanicSourceSnapshot[] snapshots)
        {
            var result = new List<CharacterTestMechanicSourceSnapshot>();
            if (snapshots == null || snapshots.Length == 0)
                return result;

            for (int i = 0; i < snapshots.Length; i++)
            {
                CharacterTestNetworkMechanicSourceSnapshot snapshot = snapshots[i];
                if (snapshot == null
                    || snapshot.RuntimeSourceId <= 0
                    || snapshot.SourceDefinitionId <= 0)
                {
                    continue;
                }

                result.Add(snapshot.ToSnapshot());
            }

            return result;
        }

        public static CharacterTestNetworkMechanicSourceSnapshot[] CloneArray(
            CharacterTestNetworkMechanicSourceSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
                return Array.Empty<CharacterTestNetworkMechanicSourceSnapshot>();

            var result = new CharacterTestNetworkMechanicSourceSnapshot[snapshots.Length];
            for (int i = 0; i < snapshots.Length; i++)
                result[i] = snapshots[i]?.CloneForRollback();
            return result;
        }

        public CharacterTestNetworkMechanicSourceSnapshot CloneForRollback()
        {
            return (CharacterTestNetworkMechanicSourceSnapshot)MemberwiseClone();
        }

        private static CharacterTestNetworkMechanicSourceSnapshot FromSnapshot(
            CharacterTestMechanicSourceSnapshot snapshot)
        {
            return new CharacterTestNetworkMechanicSourceSnapshot
            {
                RuntimeSourceId = snapshot.RuntimeSourceId,
                SourceDefinitionId = snapshot.SourceDefinitionId,
                SourceGameplayEntityIndex = snapshot.SourceGameplayEntityIndex,
                SourceGameplayEntityGeneration = snapshot.SourceGameplayEntityGeneration,
                SourceCombatEntityId = snapshot.SourceCombatEntityId,
                CenterXRaw = snapshot.CenterXRaw,
                CenterYRaw = snapshot.CenterYRaw,
                CenterZRaw = snapshot.CenterZRaw,
                StartFrame = snapshot.StartFrame,
                EndFrame = snapshot.EndFrame,
                TraceId = snapshot.TraceId
            };
        }

        private CharacterTestMechanicSourceSnapshot ToSnapshot()
        {
            return new CharacterTestMechanicSourceSnapshot(
                RuntimeSourceId,
                SourceDefinitionId,
                SourceGameplayEntityIndex,
                SourceGameplayEntityGeneration,
                SourceCombatEntityId,
                CenterXRaw,
                CenterYRaw,
                CenterZRaw,
                StartFrame,
                EndFrame,
                TraceId);
        }
    }

    internal static class CharacterTestNetworkPoisonBottlePoolSnapshotCodec
    {
        public static CharacterTestNetworkPoisonBottlePoolRuntimeSnapshot Capture(
            CharacterTestCharacterCombatTraceSystem combatTrace)
        {
            return CharacterTestNetworkPoisonBottlePoolRuntimeSnapshot.FromState(
                combatTrace?.CreatePoisonBottlePoolStateSnapshot());
        }

        public static NetworkSnapshotApplyResult Apply(
            CharacterTestCharacterCombatTraceSystem combatTrace,
            CharacterTestNetworkPoisonBottlePoolRuntimeSnapshot poisonBottlePools)
        {
            if (combatTrace == null)
            {
                if (poisonBottlePools == null || poisonBottlePools.ActivePoolCount == 0)
                    return NetworkSnapshotApplyResult.Succeeded();

                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest Poison Bottle pool snapshot cannot be applied because combat trace system is missing.");
            }

            CharacterTestMechanicSourceRuntimeState state;
            try
            {
                state = poisonBottlePools?.ToState();
            }
            catch (Exception exception)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest Poison Bottle pool snapshot parse failed: " + exception.Message);
            }

            if (!combatTrace.TryApplyPoisonBottlePoolStateSnapshot(state, out string failure))
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest Poison Bottle pool snapshot apply failed: " + failure);
            }

            return NetworkSnapshotApplyResult.Succeeded();
        }

        public static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestMechanicSourceRuntimeState state,
            int casterEntityIdFilter = 0)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F83, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            AddStateHash(accumulator, state, Math.Max(0, casterEntityIdFilter), excludeCasterEntity: false);
            accumulator.EndObject();
        }

        public static void AddStateHashExcludingCaster(
            RuntimeHashAccumulator accumulator,
            CharacterTestMechanicSourceRuntimeState state,
            int excludedCasterEntityId)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F84, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            AddStateHash(accumulator, state, Math.Max(0, excludedCasterEntityId), excludeCasterEntity: true);
            accumulator.EndObject();
        }

        private static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestMechanicSourceRuntimeState state,
            int casterEntityIdFilter,
            bool excludeCasterEntity)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F83, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F37,
                state != null
                    ? state.SchemaVersion
                    : CharacterTestMechanicSourceRuntimeState.CurrentSchemaVersion);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F38,
                state != null ? state.ModuleId : CharacterTestPoisonBottlePoolRuntime.SourceRuntimeModuleId);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F39,
                state != null ? state.NextRuntimeSourceId : 1);

            IReadOnlyList<CharacterTestMechanicSourceSnapshot> sources =
                state != null ? state.ActiveSources : null;
            int count = sources != null ? sources.Count : 0;
            if (casterEntityIdFilter <= 0)
            {
                accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F40, count);
                for (int i = 0; i < count; i++)
                    AddSourceSnapshotHash(accumulator, i, sources[i]);
                { accumulator.EndObject(); return; }
            }

            int matched = 0;
            for (int i = 0; i < count; i++)
            {
                bool casterMatches = sources[i].SourceCombatEntityId == casterEntityIdFilter;
                if ((!excludeCasterEntity && casterMatches) || (excludeCasterEntity && !casterMatches))
                    matched++;
            }

            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F40, matched);
            int index = 0;
            for (int i = 0; i < count; i++)
            {
                bool casterMatches = sources[i].SourceCombatEntityId == casterEntityIdFilter;
                if ((!excludeCasterEntity && !casterMatches) || (excludeCasterEntity && casterMatches))
                    continue;

                AddSourceSnapshotHash(accumulator, index, sources[i]);
                index++;
            }
            accumulator.EndObject();
        }

        private static void AddSourceSnapshotHash(
            RuntimeHashAccumulator accumulator,
            int index,
            CharacterTestMechanicSourceSnapshot source)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F88, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F41, index);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F42, source.RuntimeSourceId);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F43, source.SourceDefinitionId);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F44,
                source.SourceGameplayEntityIndex);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F45,
                source.SourceGameplayEntityGeneration);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F46, source.SourceCombatEntityId);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F47, source.CenterXRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F48, source.CenterYRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F49, source.CenterZRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F50, source.StartFrame);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F51, source.EndFrame);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F52, source.TraceId);
            accumulator.EndObject();
        }
    }

    public sealed class CharacterTestNetworkPoisonBottlePoolRuntimeSnapshot
    {
        public CharacterTestNetworkMechanicSourceRuntimeSnapshot SourceRuntime { get; set; }
        public int ActivePoolCount => SourceRuntime != null ? SourceRuntime.ActiveCount : 0;

        internal static CharacterTestNetworkPoisonBottlePoolRuntimeSnapshot FromState(
            CharacterTestMechanicSourceRuntimeState state)
        {
            if (state == null)
                return null;

            return new CharacterTestNetworkPoisonBottlePoolRuntimeSnapshot
            {
                SourceRuntime = CharacterTestNetworkMechanicSourceRuntimeSnapshot.FromState(state)
            };
        }

        public CharacterTestNetworkPoisonBottlePoolRuntimeSnapshot CloneForRollback()
        {
            return new CharacterTestNetworkPoisonBottlePoolRuntimeSnapshot
            {
                SourceRuntime = SourceRuntime?.CloneForRollback()
            };
        }

        internal CharacterTestMechanicSourceRuntimeState ToState()
        {
            if (SourceRuntime == null)
                return null;

            return new CharacterTestMechanicSourceRuntimeState(
                SourceRuntime.SchemaVersion,
                SourceRuntime.ModuleId <= 0
                    ? CharacterTestPoisonBottlePoolRuntime.SourceRuntimeModuleId
                    : SourceRuntime.ModuleId,
                SourceRuntime.NextRuntimeSourceId,
                CharacterTestNetworkMechanicSourceSnapshot.ToSnapshots(SourceRuntime.ActiveSources));
        }
    }

    internal static class CharacterTestNetworkPhysicalShieldSnapshotCodec
    {
        public static CharacterTestNetworkPhysicalShieldRuntimeSnapshot Capture(
            CharacterTestCharacterCombatTraceSystem combatTrace)
        {
            return CharacterTestNetworkPhysicalShieldRuntimeSnapshot.FromState(
                combatTrace?.CreatePhysicalShieldStateSnapshot());
        }

        public static NetworkSnapshotApplyResult Apply(
            CharacterTestCharacterCombatTraceSystem combatTrace,
            CharacterTestNetworkPhysicalShieldRuntimeSnapshot physicalShields)
        {
            if (combatTrace == null)
            {
                if (physicalShields == null || physicalShields.TotalCount == 0)
                    return NetworkSnapshotApplyResult.Succeeded();

                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest Physical Shield snapshot cannot be applied because combat trace system is missing.");
            }

            CharacterTestPhysicalShieldRuntimeState state;
            try
            {
                state = physicalShields?.ToState();
            }
            catch (Exception exception)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest Physical Shield snapshot parse failed: " + exception.Message);
            }

            if (!combatTrace.TryApplyPhysicalShieldStateSnapshot(state, out string failure))
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest Physical Shield snapshot apply failed: " + failure);
            }

            return NetworkSnapshotApplyResult.Succeeded();
        }

        public static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestPhysicalShieldRuntimeState state,
            int casterEntityIdFilter = 0)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F83, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            AddStateHash(accumulator, state, Math.Max(0, casterEntityIdFilter), excludeCasterEntity: false);
            accumulator.EndObject();
        }

        public static void AddStateHashExcludingCaster(
            RuntimeHashAccumulator accumulator,
            CharacterTestPhysicalShieldRuntimeState state,
            int excludedCasterEntityId)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F84, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            AddStateHash(accumulator, state, Math.Max(0, excludedCasterEntityId), excludeCasterEntity: true);
            accumulator.EndObject();
        }

        private static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestPhysicalShieldRuntimeState state,
            int casterEntityIdFilter,
            bool excludeCasterEntity)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F83, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F53,
                state != null
                    ? state.SchemaVersion
                    : CharacterTestPhysicalShieldRuntimeState.CurrentSchemaVersion);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F54,
                state != null ? state.ModuleId : CharacterTestPhysicalShieldRuntimeState.ModuleIdValue);

            AddActiveShieldHash(
                accumulator,
                state != null ? state.ActiveShields : null,
                casterEntityIdFilter,
                excludeCasterEntity);
            AddShieldRuntimeHash(
                accumulator,
                state != null ? state.ShieldRuntime : null,
                casterEntityIdFilter,
                excludeCasterEntity);
            accumulator.EndObject();
        }

        private static void AddActiveShieldHash(
            RuntimeHashAccumulator accumulator,
            IReadOnlyList<CharacterTestPhysicalShieldActiveSnapshot> activeShields,
            int casterEntityIdFilter,
            bool excludeCasterEntity)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F89, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            int count = activeShields != null ? activeShields.Count : 0;
            if (casterEntityIdFilter <= 0)
            {
                accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F55, count);
                for (int i = 0; i < count; i++)
                    AddActiveShieldSnapshotHash(accumulator, i, activeShields[i]);
                { accumulator.EndObject(); return; }
            }

            int matched = 0;
            for (int i = 0; i < count; i++)
            {
                bool ownerMatches = activeShields[i].OwnerId == casterEntityIdFilter;
                if ((!excludeCasterEntity && ownerMatches) || (excludeCasterEntity && !ownerMatches))
                    matched++;
            }

            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F55, matched);
            int index = 0;
            for (int i = 0; i < count; i++)
            {
                bool ownerMatches = activeShields[i].OwnerId == casterEntityIdFilter;
                if ((!excludeCasterEntity && !ownerMatches) || (excludeCasterEntity && ownerMatches))
                    continue;

                AddActiveShieldSnapshotHash(accumulator, index, activeShields[i]);
                index++;
            }
            accumulator.EndObject();
        }

        private static void AddShieldRuntimeHash(
            RuntimeHashAccumulator accumulator,
            CombatShieldRuntimeState state,
            int casterEntityIdFilter,
            bool excludeCasterEntity)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F90, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F56,
                state != null ? state.SchemaVersion : CombatShieldRuntimeState.CurrentSchemaVersion);
            accumulator.AddInt(
                CharacterTestNetworkAreaConfinementSnapshotHashFields.F57,
                state != null ? state.ModuleIdentity : CharacterTestPhysicalShieldRuntimeState.ShieldRuntimeModuleIdentity);

            IReadOnlyList<CombatShieldPotencyComponent> shields = state != null ? state.Shields : null;
            int count = shields != null ? shields.Count : 0;
            if (casterEntityIdFilter <= 0)
            {
                accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F58, count);
                for (int i = 0; i < count; i++)
                    AddShieldPotencyHash(accumulator, i, shields[i]);
                { accumulator.EndObject(); return; }
            }

            int matched = 0;
            for (int i = 0; i < count; i++)
            {
                bool ownerMatches = shields[i].OwnerEntityId.Value == casterEntityIdFilter;
                if ((!excludeCasterEntity && ownerMatches) || (excludeCasterEntity && !ownerMatches))
                    matched++;
            }

            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F58, matched);
            int index = 0;
            for (int i = 0; i < count; i++)
            {
                bool ownerMatches = shields[i].OwnerEntityId.Value == casterEntityIdFilter;
                if ((!excludeCasterEntity && !ownerMatches) || (excludeCasterEntity && ownerMatches))
                    continue;

                AddShieldPotencyHash(accumulator, index, shields[i]);
                index++;
            }
            accumulator.EndObject();
        }

        private static void AddActiveShieldSnapshotHash(
            RuntimeHashAccumulator accumulator,
            int index,
            CharacterTestPhysicalShieldActiveSnapshot active)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F91, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F59, index);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F60, active.OwnerId);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F61, active.ShieldEntityId);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F62, active.BodyId);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F63, active.CenterXRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F64, active.CenterYRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F65, active.CenterZRaw);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F66, active.EndFrame);
            accumulator.EndObject();
        }

        private static void AddShieldPotencyHash(
            RuntimeHashAccumulator accumulator,
            int index,
            CombatShieldPotencyComponent shield)
        {
            accumulator.BeginObject(CharacterTestNetworkAreaConfinementSnapshotHashFields.F92, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F67, index);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F68, shield.ShieldEntityId.Value);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F69, shield.OwnerEntityId.Value);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F70, shield.BodyId.Value);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F71, shield.ColliderId.Value);
            AddPotencyHash(accumulator, CharacterTestNetworkAreaConfinementSnapshotHashFields.F93, shield.BasePotency);
            AddPotencyHash(accumulator, CharacterTestNetworkAreaConfinementSnapshotHashFields.F94, shield.CurrentPotency);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F72, shield.BaseCohesionLimit);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F73, shield.CurrentCohesionLimit);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F74, shield.RecoveryRate);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F75, shield.RecoveryDelayFrames);
            accumulator.AddLong(CharacterTestNetworkAreaConfinementSnapshotHashFields.F76, shield.LastImpactFrame);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F77, (int)shield.CurrentBand);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F78, shield.IsBroken ? 1 : 0);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F79, shield.ShieldPressure);
            accumulator.EndObject();
        }

        private static void AddPotencyHash(
            RuntimeHashAccumulator accumulator,
            int key,
            CombatShieldPotencyTriplet potency)
        {
            accumulator.BeginObject(key, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaId, CharacterTestNetworkAreaConfinementSnapshotHashFields.SchemaVersion, true);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F80, potency.Motion);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F81, potency.Cohesion);
            accumulator.AddInt(CharacterTestNetworkAreaConfinementSnapshotHashFields.F82, potency.Scatter);
            accumulator.EndObject();
        }
    }

    public sealed class CharacterTestNetworkPhysicalShieldRuntimeSnapshot
    {
        public int SchemaVersion { get; set; }
        public int ModuleId { get; set; }
        public CharacterTestNetworkPhysicalShieldActiveSnapshot[] ActiveShields { get; set; }
        public CharacterTestNetworkCombatShieldRuntimeSnapshot ShieldRuntime { get; set; }
        public int ActiveShieldCount => ActiveShields != null ? ActiveShields.Length : 0;
        public int ShieldRuntimeCount => ShieldRuntime != null ? ShieldRuntime.ShieldCount : 0;
        public int TotalCount => ActiveShieldCount + ShieldRuntimeCount;

        internal static CharacterTestNetworkPhysicalShieldRuntimeSnapshot FromState(
            CharacterTestPhysicalShieldRuntimeState state)
        {
            if (state == null)
                return null;

            return new CharacterTestNetworkPhysicalShieldRuntimeSnapshot
            {
                SchemaVersion = state.SchemaVersion,
                ModuleId = state.ModuleId,
                ActiveShields = CharacterTestNetworkPhysicalShieldActiveSnapshot.FromSnapshots(
                    state.ActiveShields),
                ShieldRuntime = CharacterTestNetworkCombatShieldRuntimeSnapshot.FromState(
                    state.ShieldRuntime)
            };
        }

        public CharacterTestNetworkPhysicalShieldRuntimeSnapshot CloneForRollback()
        {
            return new CharacterTestNetworkPhysicalShieldRuntimeSnapshot
            {
                SchemaVersion = SchemaVersion,
                ModuleId = ModuleId,
                ActiveShields = CharacterTestNetworkPhysicalShieldActiveSnapshot.CloneArray(ActiveShields),
                ShieldRuntime = ShieldRuntime?.CloneForRollback()
            };
        }

        internal CharacterTestPhysicalShieldRuntimeState ToState()
        {
            return new CharacterTestPhysicalShieldRuntimeState(
                SchemaVersion <= 0
                    ? CharacterTestPhysicalShieldRuntimeState.CurrentSchemaVersion
                    : SchemaVersion,
                ModuleId <= 0
                    ? CharacterTestPhysicalShieldRuntimeState.ModuleIdValue
                    : ModuleId,
                CharacterTestNetworkPhysicalShieldActiveSnapshot.ToSnapshots(ActiveShields),
                ShieldRuntime != null ? ShieldRuntime.ToState() : null);
        }
    }

    public sealed class CharacterTestNetworkPhysicalShieldActiveSnapshot
    {
        public int OwnerId { get; set; }
        public int ShieldEntityId { get; set; }
        public int BodyId { get; set; }
        public long CenterXRaw { get; set; }
        public long CenterYRaw { get; set; }
        public long CenterZRaw { get; set; }
        public long EndFrame { get; set; }

        internal static CharacterTestNetworkPhysicalShieldActiveSnapshot[] FromSnapshots(
            IReadOnlyList<CharacterTestPhysicalShieldActiveSnapshot> snapshots)
        {
            int count = snapshots != null ? snapshots.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestNetworkPhysicalShieldActiveSnapshot>();

            var result = new CharacterTestNetworkPhysicalShieldActiveSnapshot[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestPhysicalShieldActiveSnapshot snapshot = snapshots[i];
                result[i] = new CharacterTestNetworkPhysicalShieldActiveSnapshot
                {
                    OwnerId = snapshot.OwnerId,
                    ShieldEntityId = snapshot.ShieldEntityId,
                    BodyId = snapshot.BodyId,
                    CenterXRaw = snapshot.CenterXRaw,
                    CenterYRaw = snapshot.CenterYRaw,
                    CenterZRaw = snapshot.CenterZRaw,
                    EndFrame = snapshot.EndFrame
                };
            }

            return result;
        }

        internal static CharacterTestPhysicalShieldActiveSnapshot[] ToSnapshots(
            IReadOnlyList<CharacterTestNetworkPhysicalShieldActiveSnapshot> snapshots)
        {
            int count = snapshots != null ? snapshots.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestPhysicalShieldActiveSnapshot>();

            var result = new CharacterTestPhysicalShieldActiveSnapshot[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestNetworkPhysicalShieldActiveSnapshot snapshot = snapshots[i];
                result[i] = snapshot != null
                    ? snapshot.ToSnapshot()
                    : default;
            }

            return result;
        }

        internal static CharacterTestNetworkPhysicalShieldActiveSnapshot[] CloneArray(
            IReadOnlyList<CharacterTestNetworkPhysicalShieldActiveSnapshot> snapshots)
        {
            int count = snapshots != null ? snapshots.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestNetworkPhysicalShieldActiveSnapshot>();

            var result = new CharacterTestNetworkPhysicalShieldActiveSnapshot[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestNetworkPhysicalShieldActiveSnapshot snapshot = snapshots[i];
                result[i] = snapshot != null
                    ? snapshot.Clone()
                    : null;
            }

            return result;
        }

        public CharacterTestNetworkPhysicalShieldActiveSnapshot Clone()
        {
            return new CharacterTestNetworkPhysicalShieldActiveSnapshot
            {
                OwnerId = OwnerId,
                ShieldEntityId = ShieldEntityId,
                BodyId = BodyId,
                CenterXRaw = CenterXRaw,
                CenterYRaw = CenterYRaw,
                CenterZRaw = CenterZRaw,
                EndFrame = EndFrame
            };
        }

        private CharacterTestPhysicalShieldActiveSnapshot ToSnapshot()
        {
            return new CharacterTestPhysicalShieldActiveSnapshot(
                OwnerId,
                ShieldEntityId,
                BodyId,
                CenterXRaw,
                CenterYRaw,
                CenterZRaw,
                EndFrame);
        }
    }

    public sealed class CharacterTestNetworkCombatShieldRuntimeSnapshot
    {
        public int SchemaVersion { get; set; }
        public int ModuleId { get; set; }
        public CharacterTestNetworkCombatShieldPotencySnapshot[] Shields { get; set; }
        public int ShieldCount => Shields != null ? Shields.Length : 0;

        internal static CharacterTestNetworkCombatShieldRuntimeSnapshot FromState(
            CombatShieldRuntimeState state)
        {
            if (state == null)
                return null;

            return new CharacterTestNetworkCombatShieldRuntimeSnapshot
            {
                SchemaVersion = state.SchemaVersion,
                ModuleId = state.ModuleIdentity,
                Shields = CharacterTestNetworkCombatShieldPotencySnapshot.FromComponents(state.Shields)
            };
        }

        public CharacterTestNetworkCombatShieldRuntimeSnapshot CloneForRollback()
        {
            return new CharacterTestNetworkCombatShieldRuntimeSnapshot
            {
                SchemaVersion = SchemaVersion,
                ModuleId = ModuleId,
                Shields = CharacterTestNetworkCombatShieldPotencySnapshot.CloneArray(Shields)
            };
        }

        internal CombatShieldRuntimeState ToState()
        {
            return new CombatShieldRuntimeState(
                SchemaVersion,
                CharacterTestPhysicalShieldRuntimeState.ShieldRuntimeModuleId,
                CharacterTestNetworkCombatShieldPotencySnapshot.ToComponents(Shields), ModuleId);
        }
    }

    public sealed class CharacterTestNetworkCombatShieldPotencySnapshot
    {
        public int ShieldEntityId { get; set; }
        public int OwnerEntityId { get; set; }
        public int BodyId { get; set; }
        public int ColliderId { get; set; }
        public int BaseMotion { get; set; }
        public int BaseCohesion { get; set; }
        public int BaseScatter { get; set; }
        public int CurrentMotion { get; set; }
        public int CurrentCohesion { get; set; }
        public int CurrentScatter { get; set; }
        public int BaseCohesionLimit { get; set; }
        public int CurrentCohesionLimit { get; set; }
        public int RecoveryRate { get; set; }
        public int RecoveryDelayFrames { get; set; }
        public long LastImpactFrame { get; set; }
        public int CurrentBand { get; set; }
        public bool IsBroken { get; set; }

        internal static CharacterTestNetworkCombatShieldPotencySnapshot[] FromComponents(
            IReadOnlyList<CombatShieldPotencyComponent> components)
        {
            int count = components != null ? components.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestNetworkCombatShieldPotencySnapshot>();

            var result = new CharacterTestNetworkCombatShieldPotencySnapshot[count];
            for (int i = 0; i < count; i++)
                result[i] = FromComponent(components[i]);
            return result;
        }

        internal static CombatShieldPotencyComponent[] ToComponents(
            IReadOnlyList<CharacterTestNetworkCombatShieldPotencySnapshot> snapshots)
        {
            int count = snapshots != null ? snapshots.Count : 0;
            if (count == 0)
                return Array.Empty<CombatShieldPotencyComponent>();

            var result = new CombatShieldPotencyComponent[count];
            for (int i = 0; i < count; i++)
                result[i] = snapshots[i] != null ? snapshots[i].ToComponent() : default;
            return result;
        }

        internal static CharacterTestNetworkCombatShieldPotencySnapshot[] CloneArray(
            IReadOnlyList<CharacterTestNetworkCombatShieldPotencySnapshot> snapshots)
        {
            int count = snapshots != null ? snapshots.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestNetworkCombatShieldPotencySnapshot>();

            var result = new CharacterTestNetworkCombatShieldPotencySnapshot[count];
            for (int i = 0; i < count; i++)
                result[i] = snapshots[i]?.Clone();
            return result;
        }

        private static CharacterTestNetworkCombatShieldPotencySnapshot FromComponent(
            CombatShieldPotencyComponent component)
        {
            return new CharacterTestNetworkCombatShieldPotencySnapshot
            {
                ShieldEntityId = component.ShieldEntityId.Value,
                OwnerEntityId = component.OwnerEntityId.Value,
                BodyId = component.BodyId.Value,
                ColliderId = component.ColliderId.Value,
                BaseMotion = component.BasePotency.Motion,
                BaseCohesion = component.BasePotency.Cohesion,
                BaseScatter = component.BasePotency.Scatter,
                CurrentMotion = component.CurrentPotency.Motion,
                CurrentCohesion = component.CurrentPotency.Cohesion,
                CurrentScatter = component.CurrentPotency.Scatter,
                BaseCohesionLimit = component.BaseCohesionLimit,
                CurrentCohesionLimit = component.CurrentCohesionLimit,
                RecoveryRate = component.RecoveryRate,
                RecoveryDelayFrames = component.RecoveryDelayFrames,
                LastImpactFrame = component.LastImpactFrame,
                CurrentBand = (int)component.CurrentBand,
                IsBroken = component.IsBroken
            };
        }

        public CharacterTestNetworkCombatShieldPotencySnapshot Clone()
        {
            return new CharacterTestNetworkCombatShieldPotencySnapshot
            {
                ShieldEntityId = ShieldEntityId,
                OwnerEntityId = OwnerEntityId,
                BodyId = BodyId,
                ColliderId = ColliderId,
                BaseMotion = BaseMotion,
                BaseCohesion = BaseCohesion,
                BaseScatter = BaseScatter,
                CurrentMotion = CurrentMotion,
                CurrentCohesion = CurrentCohesion,
                CurrentScatter = CurrentScatter,
                BaseCohesionLimit = BaseCohesionLimit,
                CurrentCohesionLimit = CurrentCohesionLimit,
                RecoveryRate = RecoveryRate,
                RecoveryDelayFrames = RecoveryDelayFrames,
                LastImpactFrame = LastImpactFrame,
                CurrentBand = CurrentBand,
                IsBroken = IsBroken
            };
        }

        private CombatShieldPotencyComponent ToComponent()
        {
            return new CombatShieldPotencyComponent(
                new CombatEntityId(ShieldEntityId),
                new CombatEntityId(OwnerEntityId),
                new CombatBodyId(BodyId),
                new CombatColliderId(ColliderId),
                new CombatShieldPotencyTriplet(BaseMotion, BaseCohesion, BaseScatter),
                new CombatShieldPotencyTriplet(CurrentMotion, CurrentCohesion, CurrentScatter),
                BaseCohesionLimit,
                CurrentCohesionLimit,
                RecoveryRate,
                RecoveryDelayFrames,
                LastImpactFrame,
                (CombatShieldPressureBand)CurrentBand,
                IsBroken);
        }
    }
}

namespace MxFramework.Game.CharacterTest
{
    // Frozen v3 protocol numbers. Retired entries remain reserved.
    internal static class CharacterTestNetworkAreaConfinementSnapshotHashFields
    {
        internal const int SchemaId = 10002;
        internal const int SchemaVersion = 1;
        internal const int F1 = 1; // areaConfinement.schema
        internal const int F2 = 2; // areaConfinement.nextPendingChannelId
        internal const int F3 = 3; // areaConfinement.pending.count
        internal const int F4 = 4; // areaConfinement.source.schema
        internal const int F5 = 5; // areaConfinement.source.nextRuntimeSourceId
        internal const int F6 = 6; // areaConfinement.source.count
        internal const int F7 = 7; // areaConfinement.pending.index
        internal const int F8 = 8; // areaConfinement.pending.id
        internal const int F9 = 9; // areaConfinement.pending.casterEntityId
        internal const int F10 = 10; // areaConfinement.pending.sourceGameplayEntityIndex
        internal const int F11 = 11; // areaConfinement.pending.sourceGameplayEntityGeneration
        internal const int F12 = 12; // areaConfinement.pending.sourceCombatEntityId
        internal const int F13 = 13; // areaConfinement.pending.centerXRaw
        internal const int F14 = 14; // areaConfinement.pending.centerYRaw
        internal const int F15 = 15; // areaConfinement.pending.centerZRaw
        internal const int F16 = 16; // areaConfinement.pending.forwardXRaw
        internal const int F17 = 17; // areaConfinement.pending.forwardYRaw
        internal const int F18 = 18; // areaConfinement.pending.forwardZRaw
        internal const int F19 = 19; // areaConfinement.pending.landingMode
        internal const int F20 = 20; // areaConfinement.pending.startFrame
        internal const int F21 = 21; // areaConfinement.pending.activationFrame
        internal const int F22 = 22; // areaConfinement.pending.localFrame
        internal const int F23 = 23; // areaConfinement.pending.sourceOrder
        internal const int F24 = 24; // areaConfinement.pending.traceId
        internal const int F25 = 25; // areaConfinement.source.index
        internal const int F26 = 26; // areaConfinement.source.runtimeSourceId
        internal const int F27 = 27; // areaConfinement.source.definitionId
        internal const int F28 = 28; // areaConfinement.source.gameplayEntityIndex
        internal const int F29 = 29; // areaConfinement.source.gameplayEntityGeneration
        internal const int F30 = 30; // areaConfinement.source.combatEntityId
        internal const int F31 = 31; // areaConfinement.source.centerXRaw
        internal const int F32 = 32; // areaConfinement.source.centerYRaw
        internal const int F33 = 33; // areaConfinement.source.centerZRaw
        internal const int F34 = 34; // areaConfinement.source.startFrame
        internal const int F35 = 35; // areaConfinement.source.endFrame
        internal const int F36 = 36; // areaConfinement.source.traceId
        internal const int F37 = 37; // poisonBottlePool.source.schema
        internal const int F38 = 38; // poisonBottlePool.source.moduleId
        internal const int F39 = 39; // poisonBottlePool.source.nextRuntimeSourceId
        internal const int F40 = 40; // poisonBottlePool.source.count
        internal const int F41 = 41; // poisonBottlePool.source.index
        internal const int F42 = 42; // poisonBottlePool.source.runtimeSourceId
        internal const int F43 = 43; // poisonBottlePool.source.definitionId
        internal const int F44 = 44; // poisonBottlePool.source.gameplayEntityIndex
        internal const int F45 = 45; // poisonBottlePool.source.gameplayEntityGeneration
        internal const int F46 = 46; // poisonBottlePool.source.combatEntityId
        internal const int F47 = 47; // poisonBottlePool.source.centerXRaw
        internal const int F48 = 48; // poisonBottlePool.source.centerYRaw
        internal const int F49 = 49; // poisonBottlePool.source.centerZRaw
        internal const int F50 = 50; // poisonBottlePool.source.startFrame
        internal const int F51 = 51; // poisonBottlePool.source.endFrame
        internal const int F52 = 52; // poisonBottlePool.source.traceId
        internal const int F53 = 53; // physicalShield.schema
        internal const int F54 = 54; // physicalShield.moduleId
        internal const int F55 = 55; // physicalShield.active.count
        internal const int F56 = 56; // physicalShield.shieldRuntime.schema
        internal const int F57 = 57; // physicalShield.shieldRuntime.moduleId
        internal const int F58 = 58; // physicalShield.shieldRuntime.count
        internal const int F59 = 59; // physicalShield.active.index
        internal const int F60 = 60; // physicalShield.active.ownerId
        internal const int F61 = 61; // physicalShield.active.shieldEntityId
        internal const int F62 = 62; // physicalShield.active.bodyId
        internal const int F63 = 63; // physicalShield.active.centerXRaw
        internal const int F64 = 64; // physicalShield.active.centerYRaw
        internal const int F65 = 65; // physicalShield.active.centerZRaw
        internal const int F66 = 66; // physicalShield.active.endFrame
        internal const int F67 = 67; // physicalShield.shield.index
        internal const int F68 = 68; // physicalShield.shield.shieldEntityId
        internal const int F69 = 69; // physicalShield.shield.ownerEntityId
        internal const int F70 = 70; // physicalShield.shield.bodyId
        internal const int F71 = 71; // physicalShield.shield.colliderId
        internal const int F72 = 72; // physicalShield.shield.baseCohesionLimit
        internal const int F73 = 73; // physicalShield.shield.currentCohesionLimit
        internal const int F74 = 74; // physicalShield.shield.recoveryRate
        internal const int F75 = 75; // physicalShield.shield.recoveryDelayFrames
        internal const int F76 = 76; // physicalShield.shield.lastImpactFrame
        internal const int F77 = 77; // physicalShield.shield.currentBand
        internal const int F78 = 78; // physicalShield.shield.isBroken
        internal const int F79 = 79; // physicalShield.shield.pressure
        internal const int F80 = 80; // .motion
        internal const int F81 = 81; // .cohesion
        internal const int F82 = 82; // .scatter
        internal const int F83 = 83; // $scope.AddStateHash
        internal const int F84 = 84; // $scope.AddStateHashExcludingCaster
        internal const int F85 = 85; // $scope.AddPendingHash
        internal const int F86 = 86; // $scope.AddSourceHash
        internal const int F87 = 87; // $scope.AddPendingSnapshotHash
        internal const int F88 = 88; // $scope.AddSourceSnapshotHash
        internal const int F89 = 89; // $scope.AddActiveShieldHash
        internal const int F90 = 90; // $scope.AddShieldRuntimeHash
        internal const int F91 = 91; // $scope.AddActiveShieldSnapshotHash
        internal const int F92 = 92; // $scope.AddShieldPotencyHash
        internal const int F93 = 93; // physicalShield.shield.basePotency
        internal const int F94 = 94; // physicalShield.shield.currentPotency
    }
}

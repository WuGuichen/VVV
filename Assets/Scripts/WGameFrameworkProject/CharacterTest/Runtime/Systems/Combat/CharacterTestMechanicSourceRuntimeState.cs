using System;
using System.Collections.Generic;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestMechanicSourceRuntimeState
    {
        public const int CurrentSchemaVersion = 1;
        public const int DefaultModuleId = 1100;

        public CharacterTestMechanicSourceRuntimeState(
            int schemaVersion,
            int moduleId,
            int nextRuntimeSourceId,
            IReadOnlyList<CharacterTestMechanicSourceSnapshot> activeSources,
            bool borrow = false)
        {
            SchemaVersion = schemaVersion <= 0 ? CurrentSchemaVersion : schemaVersion;
            ModuleId = moduleId <= 0 ? DefaultModuleId : moduleId;
            NextRuntimeSourceId = nextRuntimeSourceId <= 0 ? 1 : nextRuntimeSourceId;
            ActiveSources = borrow ? activeSources : Copy(activeSources);
        }

        public int SchemaVersion { get; }
        public int ModuleId { get; internal set; }
        public int NextRuntimeSourceId { get; internal set; }
        public IReadOnlyList<CharacterTestMechanicSourceSnapshot> ActiveSources { get; }
        public int ActiveCount => ActiveSources.Count;

        private static CharacterTestMechanicSourceSnapshot[] Copy(
            IReadOnlyList<CharacterTestMechanicSourceSnapshot> activeSources)
        {
            if (activeSources == null || activeSources.Count == 0)
                return Array.Empty<CharacterTestMechanicSourceSnapshot>();

            var copy = new CharacterTestMechanicSourceSnapshot[activeSources.Count];
            for (int i = 0; i < activeSources.Count; i++)
                copy[i] = activeSources[i];
            return copy;
        }
    }

    internal readonly struct CharacterTestMechanicSourceSnapshot
    {
        public CharacterTestMechanicSourceSnapshot(
            int runtimeSourceId,
            int sourceDefinitionId,
            int sourceGameplayEntityIndex,
            int sourceGameplayEntityGeneration,
            int sourceCombatEntityId,
            long centerXRaw,
            long centerYRaw,
            long centerZRaw,
            long startFrame,
            long endFrame,
            int traceId)
        {
            RuntimeSourceId = runtimeSourceId <= 0 ? 1 : runtimeSourceId;
            SourceDefinitionId = sourceDefinitionId < 0 ? 0 : sourceDefinitionId;
            SourceGameplayEntityIndex = sourceGameplayEntityIndex < 0 ? 0 : sourceGameplayEntityIndex;
            SourceGameplayEntityGeneration = sourceGameplayEntityGeneration < 0 ? 0 : sourceGameplayEntityGeneration;
            SourceCombatEntityId = sourceCombatEntityId < 0 ? 0 : sourceCombatEntityId;
            CenterXRaw = centerXRaw;
            CenterYRaw = centerYRaw;
            CenterZRaw = centerZRaw;
            StartFrame = startFrame < 0L ? 0L : startFrame;
            EndFrame = endFrame < 0L ? 0L : endFrame;
            TraceId = traceId;
        }

        public int RuntimeSourceId { get; }
        public int SourceDefinitionId { get; }
        public int SourceGameplayEntityIndex { get; }
        public int SourceGameplayEntityGeneration { get; }
        public int SourceCombatEntityId { get; }
        public long CenterXRaw { get; }
        public long CenterYRaw { get; }
        public long CenterZRaw { get; }
        public long StartFrame { get; }
        public long EndFrame { get; }
        public int TraceId { get; }

        public FixVector3 Center => new FixVector3(
            Fix64.FromRaw(CenterXRaw),
            Fix64.FromRaw(CenterYRaw),
            Fix64.FromRaw(CenterZRaw));

        public static CharacterTestMechanicSourceSnapshot FromVector(
            int runtimeSourceId,
            int sourceDefinitionId,
            int sourceGameplayEntityIndex,
            int sourceGameplayEntityGeneration,
            int sourceCombatEntityId,
            FixVector3 center,
            long startFrame,
            long endFrame,
            int traceId)
        {
            return new CharacterTestMechanicSourceSnapshot(
                runtimeSourceId,
                sourceDefinitionId,
                sourceGameplayEntityIndex,
                sourceGameplayEntityGeneration,
                sourceCombatEntityId,
                center.X.RawValue,
                center.Y.RawValue,
                center.Z.RawValue,
                startFrame,
                endFrame,
                traceId);
        }
    }
}

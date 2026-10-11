using System;
using System.Collections.Generic;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestArcMineRuntimeState
    {
        public const int CurrentSchemaVersion = 3;
        public const int DefaultModuleId = 1108;
        public const string DefaultModuleLabel = "CharacterTest.ArcMine";

        public CharacterTestArcMineRuntimeState(
            int schemaVersion,
            int moduleId,
            int nextRuntimeId,
            IReadOnlyList<CharacterTestArcMineSnapshot> activeMines,
            bool borrow = false)
        {
            SchemaVersion = schemaVersion;
            ModuleId = moduleId;
            NextRuntimeId = nextRuntimeId;
            ActiveMines = borrow ? activeMines : Copy(activeMines);
        }

        public int SchemaVersion { get; }
        public int ModuleId { get; }
        public int NextRuntimeId { get; internal set; }
        public IReadOnlyList<CharacterTestArcMineSnapshot> ActiveMines { get; }
        public int ActiveCount => ActiveMines.Count;

        private static CharacterTestArcMineSnapshot[] Copy(
            IReadOnlyList<CharacterTestArcMineSnapshot> activeMines)
        {
            if (activeMines == null || activeMines.Count == 0)
                return Array.Empty<CharacterTestArcMineSnapshot>();

            var copy = new CharacterTestArcMineSnapshot[activeMines.Count];
            for (int i = 0; i < activeMines.Count; i++)
                copy[i] = activeMines[i];
            return copy;
        }
    }

    internal readonly struct CharacterTestArcMineSnapshot
    {
        public CharacterTestArcMineSnapshot(
            int runtimeId,
            int casterEntityId,
            int sourceCombatEntityId,
            int actionInstanceId,
            int sourceOrder,
            int localFrame,
            long createdFrame,
            long armedFrame,
            long expireFrame,
            long fuseStartedFrame,
            long detonateFrame,
            int triggerTargetEntityId,
            long centerXRaw,
            long centerYRaw,
            long centerZRaw,
            long forwardXRaw,
            long forwardYRaw,
            long forwardZRaw,
            int traceId)
        {
            RuntimeId = runtimeId <= 0 ? 1 : runtimeId;
            CasterEntityId = casterEntityId < 0 ? 0 : casterEntityId;
            SourceCombatEntityId = sourceCombatEntityId < 0 ? 0 : sourceCombatEntityId;
            ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
            SourceOrder = sourceOrder < 0 ? 0 : sourceOrder;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
            CreatedFrame = createdFrame < 0L ? 0L : createdFrame;
            ArmedFrame = armedFrame < 0L ? 0L : armedFrame;
            ExpireFrame = expireFrame < 0L ? 0L : expireFrame;
            FuseStartedFrame = fuseStartedFrame < 0L ? 0L : fuseStartedFrame;
            DetonateFrame = detonateFrame < 0L ? 0L : detonateFrame;
            TriggerTargetEntityId = triggerTargetEntityId < 0 ? 0 : triggerTargetEntityId;
            CenterXRaw = centerXRaw;
            CenterYRaw = centerYRaw;
            CenterZRaw = centerZRaw;
            ForwardXRaw = forwardXRaw;
            ForwardYRaw = forwardYRaw;
            ForwardZRaw = forwardZRaw;
            TraceId = traceId < 0 ? 0 : traceId;
        }

        public int RuntimeId { get; }
        public int CasterEntityId { get; }
        public int SourceCombatEntityId { get; }
        public int ActionInstanceId { get; }
        public int SourceOrder { get; }
        public int LocalFrame { get; }
        public long CreatedFrame { get; }
        public long ArmedFrame { get; }
        public long ExpireFrame { get; }
        public long FuseStartedFrame { get; }
        public long DetonateFrame { get; }
        public int TriggerTargetEntityId { get; }
        public long CenterXRaw { get; }
        public long CenterYRaw { get; }
        public long CenterZRaw { get; }
        public long ForwardXRaw { get; }
        public long ForwardYRaw { get; }
        public long ForwardZRaw { get; }
        public int TraceId { get; }

        public FixVector3 Center => new FixVector3(
            Fix64.FromRaw(CenterXRaw),
            Fix64.FromRaw(CenterYRaw),
            Fix64.FromRaw(CenterZRaw));

        public FixVector3 Forward => new FixVector3(
            Fix64.FromRaw(ForwardXRaw),
            Fix64.FromRaw(ForwardYRaw),
            Fix64.FromRaw(ForwardZRaw));

        public static CharacterTestArcMineSnapshot FromVectors(
            int runtimeId,
            int casterEntityId,
            int sourceCombatEntityId,
            int actionInstanceId,
            int sourceOrder,
            int localFrame,
            long createdFrame,
            long armedFrame,
            long expireFrame,
            long fuseStartedFrame,
            long detonateFrame,
            int triggerTargetEntityId,
            FixVector3 center,
            FixVector3 forward,
            int traceId)
        {
            return new CharacterTestArcMineSnapshot(
                runtimeId,
                casterEntityId,
                sourceCombatEntityId,
                actionInstanceId,
                sourceOrder,
                localFrame,
                createdFrame,
                armedFrame,
                expireFrame,
                fuseStartedFrame,
                detonateFrame,
                triggerTargetEntityId,
                center.X.RawValue,
                center.Y.RawValue,
                center.Z.RawValue,
                forward.X.RawValue,
                forward.Y.RawValue,
                forward.Z.RawValue,
                traceId);
        }
    }
}

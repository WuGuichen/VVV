using System;
using System.Collections.Generic;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuneTurretRuntimeState
    {
        public const int CurrentSchemaVersion = 2;
        public const int DefaultModuleId = 1109;
        public const string DefaultModuleLabel = "CharacterTest.RuneTurret";

        public CharacterTestRuneTurretRuntimeState(
            int schemaVersion,
            int moduleId,
            int nextRuntimeId,
            IReadOnlyList<CharacterTestRuneTurretSnapshot> activeTurrets,
            bool borrow = false)
        {
            SchemaVersion = schemaVersion;
            ModuleId = moduleId;
            NextRuntimeId = nextRuntimeId;
            ActiveTurrets = borrow ? activeTurrets : Copy(activeTurrets);
        }

        public int SchemaVersion { get; }
        public int ModuleId { get; }
        public int NextRuntimeId { get; internal set; }
        public IReadOnlyList<CharacterTestRuneTurretSnapshot> ActiveTurrets { get; }
        public int ActiveCount => ActiveTurrets.Count;

        private static CharacterTestRuneTurretSnapshot[] Copy(
            IReadOnlyList<CharacterTestRuneTurretSnapshot> activeTurrets)
        {
            if (activeTurrets == null || activeTurrets.Count == 0)
                return Array.Empty<CharacterTestRuneTurretSnapshot>();

            var copy = new CharacterTestRuneTurretSnapshot[activeTurrets.Count];
            for (int i = 0; i < activeTurrets.Count; i++)
                copy[i] = activeTurrets[i];
            return copy;
        }
    }

    internal readonly struct CharacterTestRuneTurretSnapshot
    {
        public CharacterTestRuneTurretSnapshot(
            int runtimeId,
            int casterEntityId,
            int sourceCombatEntityId,
            int actionInstanceId,
            int sourceOrder,
            int localFrame,
            long createdFrame,
            long activateFrame,
            long expireFrame,
            long nextThinkFrame,
            int shotsFired,
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
            ActivateFrame = activateFrame < 0L ? 0L : activateFrame;
            ExpireFrame = expireFrame < 0L ? 0L : expireFrame;
            NextThinkFrame = nextThinkFrame < 0L ? 0L : nextThinkFrame;
            ShotsFired = shotsFired < 0 ? 0 : shotsFired;
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
        public long ActivateFrame { get; }
        public long ExpireFrame { get; }
        public long NextThinkFrame { get; }
        public int ShotsFired { get; }
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

        public static CharacterTestRuneTurretSnapshot FromVectors(
            int runtimeId,
            int casterEntityId,
            int sourceCombatEntityId,
            int actionInstanceId,
            int sourceOrder,
            int localFrame,
            long createdFrame,
            long activateFrame,
            long expireFrame,
            long nextThinkFrame,
            int shotsFired,
            FixVector3 center,
            FixVector3 forward,
            int traceId)
        {
            return new CharacterTestRuneTurretSnapshot(
                runtimeId,
                casterEntityId,
                sourceCombatEntityId,
                actionInstanceId,
                sourceOrder,
                localFrame,
                createdFrame,
                activateFrame,
                expireFrame,
                nextThinkFrame,
                shotsFired,
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

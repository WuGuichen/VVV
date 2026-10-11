using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestHookPullRuntimeState
    {
        public const int CurrentSchemaVersion = 1;
        public const string DefaultModuleId = "CharacterTest.HookPull";

        public CharacterTestHookPullRuntimeState(
            int schemaVersion,
            string moduleId,
            IReadOnlyList<CharacterTestHookPullSnapshot> activePulls,
            bool borrow = false)
        {
            SchemaVersion = schemaVersion <= 0 ? CurrentSchemaVersion : schemaVersion;
            ModuleId = string.IsNullOrWhiteSpace(moduleId) ? DefaultModuleId : moduleId;
            ActivePulls = borrow ? activePulls : Copy(activePulls);
        }

        public int SchemaVersion { get; }
        public string ModuleId { get; }
        public IReadOnlyList<CharacterTestHookPullSnapshot> ActivePulls { get; }
        public int ActiveCount => ActivePulls.Count;

        private static CharacterTestHookPullSnapshot[] Copy(
            IReadOnlyList<CharacterTestHookPullSnapshot> activePulls)
        {
            if (activePulls == null || activePulls.Count == 0)
                return Array.Empty<CharacterTestHookPullSnapshot>();

            var copy = new CharacterTestHookPullSnapshot[activePulls.Count];
            for (int i = 0; i < activePulls.Count; i++)
                copy[i] = activePulls[i];
            return copy;
        }
    }

    internal readonly struct CharacterTestHookPullSnapshot
    {
        public CharacterTestHookPullSnapshot(
            int casterEntityId,
            int targetEntityId,
            long startFrame,
            long endFrame,
            int actionInstanceId,
            int projectileId,
            int stableEventId,
            int stopDistanceMillimeters,
            int anchorForwardOffsetMillimeters,
            int accelerationMillimetersPerSecondSquared,
            int terminalSpeedMillimetersPerSecond)
        {
            CasterEntityId = casterEntityId < 0 ? 0 : casterEntityId;
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            StartFrame = startFrame < 0L ? 0L : startFrame;
            EndFrame = endFrame < 0L ? 0L : endFrame;
            ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
            ProjectileId = projectileId < 0 ? 0 : projectileId;
            StableEventId = stableEventId < 0 ? 0 : stableEventId;
            StopDistanceMillimeters = stopDistanceMillimeters < 0 ? 0 : stopDistanceMillimeters;
            AnchorForwardOffsetMillimeters =
                anchorForwardOffsetMillimeters < 0 ? 0 : anchorForwardOffsetMillimeters;
            AccelerationMillimetersPerSecondSquared =
                accelerationMillimetersPerSecondSquared < 0 ? 0 : accelerationMillimetersPerSecondSquared;
            TerminalSpeedMillimetersPerSecond =
                terminalSpeedMillimetersPerSecond < 0 ? 0 : terminalSpeedMillimetersPerSecond;
        }

        public int CasterEntityId { get; }
        public int TargetEntityId { get; }
        public long StartFrame { get; }
        public long EndFrame { get; }
        public int ActionInstanceId { get; }
        public int ProjectileId { get; }
        public int StableEventId { get; }
        public int StopDistanceMillimeters { get; }
        public int AnchorForwardOffsetMillimeters { get; }
        public int AccelerationMillimetersPerSecondSquared { get; }
        public int TerminalSpeedMillimetersPerSecond { get; }
    }
}

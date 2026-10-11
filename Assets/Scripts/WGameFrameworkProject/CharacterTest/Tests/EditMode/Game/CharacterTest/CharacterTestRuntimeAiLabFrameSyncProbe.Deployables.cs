using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Shields;
using MxFramework.Combat.Projectiles;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiLabFrameSyncProbe
    {
        private static int ToProbeEntityId(long id)
        {
            if (id > int.MaxValue)
                return int.MaxValue;
            if (id < int.MinValue)
                return int.MinValue;
            return (int)id;
        }

        private static void CompareArcMine(
            CharacterTestRuntimeAiLabFrameSyncArcMineSample expected,
            CharacterTestRuntimeAiLabFrameSyncArcMineSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "arcMine", 0, "runtimeAvailable", expected.RuntimeAvailable, actual.RuntimeAvailable);
            AddMismatch(rows, "arcMine", 0, "schemaVersion", expected.SchemaVersion, actual.SchemaVersion);
            AddMismatch(rows, "arcMine", 0, "moduleId", expected.ModuleId, actual.ModuleId);
            AddMismatch(rows, "arcMine", 0, "nextRuntimeId", expected.NextRuntimeId, actual.NextRuntimeId);
            AddMismatch(rows, "arcMine", 0, "activeMineCount", expected.ActiveMineCount, actual.ActiveMineCount);

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample> expectedMines =
                expected.ActiveMines;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample> actualMines =
                actual.ActiveMines;
            int expectedCount = expectedMines != null ? expectedMines.Count : 0;
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample left = expectedMines[i];
                if (!TryFindArcMine(
                    actualMines,
                    left.RuntimeId,
                    out CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "arcMine",
                        left.RuntimeId,
                        "mine.missing",
                        "present",
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.casterEntityId", left.CasterEntityId, right.CasterEntityId);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.sourceCombatEntityId", left.SourceCombatEntityId, right.SourceCombatEntityId);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.actionInstanceId", left.ActionInstanceId, right.ActionInstanceId);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.sourceOrder", left.SourceOrder, right.SourceOrder);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.localFrame", left.LocalFrame, right.LocalFrame);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.createdFrame", left.CreatedFrame, right.CreatedFrame);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.armedFrame", left.ArmedFrame, right.ArmedFrame);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.expireFrame", left.ExpireFrame, right.ExpireFrame);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.fuseStartedFrame", left.FuseStartedFrame, right.FuseStartedFrame);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.detonateFrame", left.DetonateFrame, right.DetonateFrame);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.triggerTargetEntityId", left.TriggerTargetEntityId, right.TriggerTargetEntityId);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.centerXRaw", left.CenterXRaw, right.CenterXRaw);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.centerYRaw", left.CenterYRaw, right.CenterYRaw);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.centerZRaw", left.CenterZRaw, right.CenterZRaw);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.forwardXRaw", left.ForwardXRaw, right.ForwardXRaw);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.forwardYRaw", left.ForwardYRaw, right.ForwardYRaw);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.forwardZRaw", left.ForwardZRaw, right.ForwardZRaw);
                AddMismatch(rows, "arcMine", left.RuntimeId, "mine.traceId", left.TraceId, right.TraceId);
            }
        }

        private static bool TryFindArcMine(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample> samples,
            int runtimeId,
            out CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.RuntimeId == runtimeId)
                    return true;
            }

            sample = default;
            return false;
        }

        private static void CompareRuneTurret(
            CharacterTestRuntimeAiLabFrameSyncRuneTurretSample expected,
            CharacterTestRuntimeAiLabFrameSyncRuneTurretSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "runeTurret", 0, "runtimeAvailable", expected.RuntimeAvailable, actual.RuntimeAvailable);
            AddMismatch(rows, "runeTurret", 0, "schemaVersion", expected.SchemaVersion, actual.SchemaVersion);
            AddMismatch(rows, "runeTurret", 0, "moduleId", expected.ModuleId, actual.ModuleId);
            AddMismatch(rows, "runeTurret", 0, "nextRuntimeId", expected.NextRuntimeId, actual.NextRuntimeId);
            AddMismatch(rows, "runeTurret", 0, "activeTurretCount", expected.ActiveTurretCount, actual.ActiveTurretCount);

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample> expectedTurrets =
                expected.ActiveTurrets;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample> actualTurrets =
                actual.ActiveTurrets;
            int expectedCount = expectedTurrets != null ? expectedTurrets.Count : 0;
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample left = expectedTurrets[i];
                if (!TryFindRuneTurret(
                    actualTurrets,
                    left.RuntimeId,
                    out CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "runeTurret",
                        left.RuntimeId,
                        "turret.missing",
                        "present",
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.casterEntityId", left.CasterEntityId, right.CasterEntityId);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.sourceCombatEntityId", left.SourceCombatEntityId, right.SourceCombatEntityId);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.actionInstanceId", left.ActionInstanceId, right.ActionInstanceId);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.sourceOrder", left.SourceOrder, right.SourceOrder);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.localFrame", left.LocalFrame, right.LocalFrame);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.createdFrame", left.CreatedFrame, right.CreatedFrame);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.activateFrame", left.ActivateFrame, right.ActivateFrame);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.expireFrame", left.ExpireFrame, right.ExpireFrame);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.nextThinkFrame", left.NextThinkFrame, right.NextThinkFrame);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.shotsFired", left.ShotsFired, right.ShotsFired);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.centerXRaw", left.CenterXRaw, right.CenterXRaw);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.centerYRaw", left.CenterYRaw, right.CenterYRaw);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.centerZRaw", left.CenterZRaw, right.CenterZRaw);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.forwardXRaw", left.ForwardXRaw, right.ForwardXRaw);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.forwardYRaw", left.ForwardYRaw, right.ForwardYRaw);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.forwardZRaw", left.ForwardZRaw, right.ForwardZRaw);
                AddMismatch(rows, "runeTurret", left.RuntimeId, "turret.traceId", left.TraceId, right.TraceId);
            }
        }

        private static bool TryFindRuneTurret(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample> samples,
            int runtimeId,
            out CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.RuntimeId == runtimeId)
                    return true;
            }

            sample = default;
            return false;
        }

        private static void CompareAreaConfinement(
            CharacterTestRuntimeAiLabFrameSyncAreaConfinementSample expected,
            CharacterTestRuntimeAiLabFrameSyncAreaConfinementSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "areaConfinement", 0, "runtimeAvailable", expected.RuntimeAvailable, actual.RuntimeAvailable);
            AddMismatch(rows, "areaConfinement", 0, "schemaVersion", expected.SchemaVersion, actual.SchemaVersion);
            AddMismatch(rows, "areaConfinement", 0, "moduleId", expected.ModuleId, actual.ModuleId);
            AddMismatch(rows, "areaConfinement", 0, "nextPendingChannelId", expected.NextPendingChannelId, actual.NextPendingChannelId);
            AddMismatch(rows, "areaConfinement", 0, "pending.count", expected.PendingChannelCount, actual.PendingChannelCount);
            AddMismatch(rows, "areaConfinement", 0, "sourceRuntimeAvailable", expected.SourceRuntimeAvailable, actual.SourceRuntimeAvailable);
            AddMismatch(rows, "areaConfinement", 0, "sourceSchemaVersion", expected.SourceSchemaVersion, actual.SourceSchemaVersion);
            AddMismatch(rows, "areaConfinement", 0, "sourceModuleId", expected.SourceModuleId, actual.SourceModuleId);
            AddMismatch(rows, "areaConfinement", 0, "source.nextRuntimeSourceId", expected.SourceNextRuntimeSourceId, actual.SourceNextRuntimeSourceId);
            AddMismatch(rows, "areaConfinement", 0, "source.count", expected.ActiveSourceCount, actual.ActiveSourceCount);

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample> expectedPending =
                expected.PendingChannels;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample> actualPending =
                actual.PendingChannels;
            int pendingCount = expectedPending != null ? expectedPending.Count : 0;
            for (int i = 0; i < pendingCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample left = expectedPending[i];
                if (!TryFindAreaConfinementPending(
                    actualPending,
                    left.PendingChannelId,
                    out CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "areaConfinement",
                        left.PendingChannelId,
                        "pending.missing",
                        "present",
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.casterEntityId", left.CasterEntityId, right.CasterEntityId);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.sourceGameplayEntityIndex", left.SourceGameplayEntityIndex, right.SourceGameplayEntityIndex);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.sourceGameplayEntityGeneration", left.SourceGameplayEntityGeneration, right.SourceGameplayEntityGeneration);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.sourceCombatEntityId", left.SourceCombatEntityId, right.SourceCombatEntityId);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.centerXRaw", left.CenterXRaw, right.CenterXRaw);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.centerYRaw", left.CenterYRaw, right.CenterYRaw);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.centerZRaw", left.CenterZRaw, right.CenterZRaw);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.forwardXRaw", left.ForwardXRaw, right.ForwardXRaw);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.forwardYRaw", left.ForwardYRaw, right.ForwardYRaw);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.forwardZRaw", left.ForwardZRaw, right.ForwardZRaw);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.landingMode", left.LandingMode, right.LandingMode);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.startFrame", left.StartFrame, right.StartFrame);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.activationFrame", left.ActivationFrame, right.ActivationFrame);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.localFrame", left.LocalFrame, right.LocalFrame);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.sourceOrder", left.SourceOrder, right.SourceOrder);
                AddMismatch(rows, "areaConfinement", left.PendingChannelId, "pending.traceId", left.TraceId, right.TraceId);
            }

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample> expectedSources =
                expected.ActiveSources;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample> actualSources =
                actual.ActiveSources;
            int sourceCount = expectedSources != null ? expectedSources.Count : 0;
            for (int i = 0; i < sourceCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample left = expectedSources[i];
                if (!TryFindMechanicSource(
                    actualSources,
                    left.RuntimeSourceId,
                    out CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "areaConfinement",
                        left.RuntimeSourceId,
                        "source.missing",
                        "present",
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "areaConfinement", left.RuntimeSourceId, "source.definitionId", left.SourceDefinitionId, right.SourceDefinitionId);
                AddMismatch(rows, "areaConfinement", left.RuntimeSourceId, "source.gameplayEntityIndex", left.SourceGameplayEntityIndex, right.SourceGameplayEntityIndex);
                AddMismatch(rows, "areaConfinement", left.RuntimeSourceId, "source.gameplayEntityGeneration", left.SourceGameplayEntityGeneration, right.SourceGameplayEntityGeneration);
                AddMismatch(rows, "areaConfinement", left.RuntimeSourceId, "source.combatEntityId", left.SourceCombatEntityId, right.SourceCombatEntityId);
                AddMismatch(rows, "areaConfinement", left.RuntimeSourceId, "source.centerXRaw", left.CenterXRaw, right.CenterXRaw);
                AddMismatch(rows, "areaConfinement", left.RuntimeSourceId, "source.centerYRaw", left.CenterYRaw, right.CenterYRaw);
                AddMismatch(rows, "areaConfinement", left.RuntimeSourceId, "source.centerZRaw", left.CenterZRaw, right.CenterZRaw);
                AddMismatch(rows, "areaConfinement", left.RuntimeSourceId, "source.startFrame", left.StartFrame, right.StartFrame);
                AddMismatch(rows, "areaConfinement", left.RuntimeSourceId, "source.endFrame", left.EndFrame, right.EndFrame);
                AddMismatch(rows, "areaConfinement", left.RuntimeSourceId, "source.traceId", left.TraceId, right.TraceId);
            }
        }

        private static bool TryFindAreaConfinementPending(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample> samples,
            int pendingChannelId,
            out CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.PendingChannelId == pendingChannelId)
                    return true;
            }

            sample = default;
            return false;
        }

        private static void CompareHookPull(
            CharacterTestRuntimeAiLabFrameSyncHookPullSample expected,
            CharacterTestRuntimeAiLabFrameSyncHookPullSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "hookPull", 0, "runtimeAvailable", expected.RuntimeAvailable, actual.RuntimeAvailable);
            AddMismatch(rows, "hookPull", 0, "schemaVersion", expected.SchemaVersion, actual.SchemaVersion);
            AddMismatch(rows, "hookPull", 0, "moduleId", expected.ModuleId, actual.ModuleId);
            AddMismatch(rows, "hookPull", 0, "activeCount", expected.ActivePullCount, actual.ActivePullCount);

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample> expectedPulls =
                expected.ActivePulls;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample> actualPulls =
                actual.ActivePulls;
            int expectedCount = expectedPulls != null ? expectedPulls.Count : 0;
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample left = expectedPulls[i];
                if (!TryFindHookPull(
                    actualPulls,
                    left,
                    out CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "hookPull",
                        left.TargetEntityId,
                        "pull.missing",
                        BuildHookPullKey(left),
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "hookPull", left.TargetEntityId, "pull.casterEntityId", left.CasterEntityId, right.CasterEntityId);
                AddMismatch(rows, "hookPull", left.TargetEntityId, "pull.targetEntityId", left.TargetEntityId, right.TargetEntityId);
                AddMismatch(rows, "hookPull", left.TargetEntityId, "pull.startFrame", left.StartFrame, right.StartFrame);
                AddMismatch(rows, "hookPull", left.TargetEntityId, "pull.endFrame", left.EndFrame, right.EndFrame);
                AddMismatch(rows, "hookPull", left.TargetEntityId, "pull.actionInstanceId", left.ActionInstanceId, right.ActionInstanceId);
                AddMismatch(rows, "hookPull", left.TargetEntityId, "pull.projectileId", left.ProjectileId, right.ProjectileId);
                AddMismatch(rows, "hookPull", left.TargetEntityId, "pull.stableEventId", left.StableEventId, right.StableEventId);
                AddMismatch(rows, "hookPull", left.TargetEntityId, "pull.stopDistanceMillimeters", left.StopDistanceMillimeters, right.StopDistanceMillimeters);
                AddMismatch(rows, "hookPull", left.TargetEntityId, "pull.anchorForwardOffsetMillimeters", left.AnchorForwardOffsetMillimeters, right.AnchorForwardOffsetMillimeters);
                AddMismatch(rows, "hookPull", left.TargetEntityId, "pull.accelerationMillimetersPerSecondSquared", left.AccelerationMillimetersPerSecondSquared, right.AccelerationMillimetersPerSecondSquared);
                AddMismatch(rows, "hookPull", left.TargetEntityId, "pull.terminalSpeedMillimetersPerSecond", left.TerminalSpeedMillimetersPerSecond, right.TerminalSpeedMillimetersPerSecond);
            }
        }

        private static bool TryFindHookPull(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample> samples,
            CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample expected,
            out CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.CasterEntityId == expected.CasterEntityId
                    && sample.TargetEntityId == expected.TargetEntityId
                    && sample.ActionInstanceId == expected.ActionInstanceId
                    && sample.ProjectileId == expected.ProjectileId
                    && sample.StableEventId == expected.StableEventId)
                {
                    return true;
                }
            }

            sample = default;
            return false;
        }

        private static string BuildHookPullKey(
            CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample sample)
        {
            return string.Concat(
                sample.CasterEntityId.ToString(CultureInfo.InvariantCulture),
                ":",
                sample.TargetEntityId.ToString(CultureInfo.InvariantCulture),
                ":",
                sample.ActionInstanceId.ToString(CultureInfo.InvariantCulture),
                ":",
                sample.ProjectileId.ToString(CultureInfo.InvariantCulture),
                ":",
                sample.StableEventId.ToString(CultureInfo.InvariantCulture));
        }
    }
}
namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncArcMineSample
    {
        public CharacterTestRuntimeAiLabFrameSyncArcMineSample(
            bool runtimeAvailable,
            int schemaVersion,
            int moduleId,
            int nextRuntimeId,
            CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample[] activeMines)
        {
            RuntimeAvailable = runtimeAvailable;
            SchemaVersion = schemaVersion;
            ModuleId = moduleId;
            NextRuntimeId = nextRuntimeId;
            ActiveMines = activeMines ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample>();
        }

        public bool RuntimeAvailable { get; }
        public int SchemaVersion { get; }
        public int ModuleId { get; }
        public int NextRuntimeId { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample> ActiveMines { get; }
        public int ActiveMineCount => ActiveMines != null ? ActiveMines.Count : 0;

        public static CharacterTestRuntimeAiLabFrameSyncArcMineSample Capture(
            CharacterTestArcMineRuntimeState state)
        {
            return new CharacterTestRuntimeAiLabFrameSyncArcMineSample(
                state != null,
                state != null ? state.SchemaVersion : 0,
                state != null ? state.ModuleId : 0,
                state != null ? state.NextRuntimeId : 0,
                CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample.Capture(state));
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample
    {
        public CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample(
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
            RuntimeId = runtimeId;
            CasterEntityId = casterEntityId;
            SourceCombatEntityId = sourceCombatEntityId;
            ActionInstanceId = actionInstanceId;
            SourceOrder = sourceOrder;
            LocalFrame = localFrame;
            CreatedFrame = createdFrame;
            ArmedFrame = armedFrame;
            ExpireFrame = expireFrame;
            FuseStartedFrame = fuseStartedFrame;
            DetonateFrame = detonateFrame;
            TriggerTargetEntityId = triggerTargetEntityId;
            CenterXRaw = centerXRaw;
            CenterYRaw = centerYRaw;
            CenterZRaw = centerZRaw;
            ForwardXRaw = forwardXRaw;
            ForwardYRaw = forwardYRaw;
            ForwardZRaw = forwardZRaw;
            TraceId = traceId;
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

        public static CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample[] Capture(
            CharacterTestArcMineRuntimeState state)
        {
            IReadOnlyList<CharacterTestArcMineSnapshot> activeMines = state != null
                ? state.ActiveMines
                : null;
            int count = activeMines != null ? activeMines.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample>();

            var samples = new CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestArcMineSnapshot mine = activeMines[i];
                samples[i] = new CharacterTestRuntimeAiLabFrameSyncArcMineActiveSample(
                    mine.RuntimeId,
                    mine.CasterEntityId,
                    mine.SourceCombatEntityId,
                    mine.ActionInstanceId,
                    mine.SourceOrder,
                    mine.LocalFrame,
                    mine.CreatedFrame,
                    mine.ArmedFrame,
                    mine.ExpireFrame,
                    mine.FuseStartedFrame,
                    mine.DetonateFrame,
                    mine.TriggerTargetEntityId,
                    mine.CenterXRaw,
                    mine.CenterYRaw,
                    mine.CenterZRaw,
                    mine.ForwardXRaw,
                    mine.ForwardYRaw,
                    mine.ForwardZRaw,
                    mine.TraceId);
            }

            return samples;
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncRuneTurretSample
    {
        public CharacterTestRuntimeAiLabFrameSyncRuneTurretSample(
            bool runtimeAvailable,
            int schemaVersion,
            int moduleId,
            int nextRuntimeId,
            CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample[] activeTurrets)
        {
            RuntimeAvailable = runtimeAvailable;
            SchemaVersion = schemaVersion;
            ModuleId = moduleId;
            NextRuntimeId = nextRuntimeId;
            ActiveTurrets = activeTurrets ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample>();
        }

        public bool RuntimeAvailable { get; }
        public int SchemaVersion { get; }
        public int ModuleId { get; }
        public int NextRuntimeId { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample> ActiveTurrets { get; }
        public int ActiveTurretCount => ActiveTurrets != null ? ActiveTurrets.Count : 0;

        public static CharacterTestRuntimeAiLabFrameSyncRuneTurretSample Capture(
            CharacterTestRuneTurretRuntimeState state)
        {
            return new CharacterTestRuntimeAiLabFrameSyncRuneTurretSample(
                state != null,
                state != null ? state.SchemaVersion : 0,
                state != null ? state.ModuleId : 0,
                state != null ? state.NextRuntimeId : 0,
                CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample.Capture(state));
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample
    {
        public CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample(
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
            RuntimeId = runtimeId;
            CasterEntityId = casterEntityId;
            SourceCombatEntityId = sourceCombatEntityId;
            ActionInstanceId = actionInstanceId;
            SourceOrder = sourceOrder;
            LocalFrame = localFrame;
            CreatedFrame = createdFrame;
            ActivateFrame = activateFrame;
            ExpireFrame = expireFrame;
            NextThinkFrame = nextThinkFrame;
            ShotsFired = shotsFired;
            CenterXRaw = centerXRaw;
            CenterYRaw = centerYRaw;
            CenterZRaw = centerZRaw;
            ForwardXRaw = forwardXRaw;
            ForwardYRaw = forwardYRaw;
            ForwardZRaw = forwardZRaw;
            TraceId = traceId;
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

        public static CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample[] Capture(
            CharacterTestRuneTurretRuntimeState state)
        {
            IReadOnlyList<CharacterTestRuneTurretSnapshot> activeTurrets = state != null
                ? state.ActiveTurrets
                : null;
            int count = activeTurrets != null ? activeTurrets.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample>();

            var samples = new CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuneTurretSnapshot turret = activeTurrets[i];
                samples[i] = new CharacterTestRuntimeAiLabFrameSyncRuneTurretActiveSample(
                    turret.RuntimeId,
                    turret.CasterEntityId,
                    turret.SourceCombatEntityId,
                    turret.ActionInstanceId,
                    turret.SourceOrder,
                    turret.LocalFrame,
                    turret.CreatedFrame,
                    turret.ActivateFrame,
                    turret.ExpireFrame,
                    turret.NextThinkFrame,
                    turret.ShotsFired,
                    turret.CenterXRaw,
                    turret.CenterYRaw,
                    turret.CenterZRaw,
                    turret.ForwardXRaw,
                    turret.ForwardYRaw,
                    turret.ForwardZRaw,
                    turret.TraceId);
            }

            return samples;
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncHookPullSample
    {
        public CharacterTestRuntimeAiLabFrameSyncHookPullSample(
            bool runtimeAvailable,
            int schemaVersion,
            string moduleId,
            CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample[] activePulls)
        {
            RuntimeAvailable = runtimeAvailable;
            SchemaVersion = schemaVersion;
            ModuleId = moduleId ?? string.Empty;
            ActivePulls = activePulls ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample>();
        }

        public bool RuntimeAvailable { get; }
        public int SchemaVersion { get; }
        public string ModuleId { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample> ActivePulls { get; }
        public int ActivePullCount => ActivePulls != null ? ActivePulls.Count : 0;

        public static CharacterTestRuntimeAiLabFrameSyncHookPullSample Capture(
            CharacterTestHookPullRuntimeState state)
        {
            return new CharacterTestRuntimeAiLabFrameSyncHookPullSample(
                state != null,
                state != null ? state.SchemaVersion : 0,
                state != null ? state.ModuleId : string.Empty,
                CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample.Capture(state));
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample
    {
        public CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample(
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
            CasterEntityId = casterEntityId;
            TargetEntityId = targetEntityId;
            StartFrame = startFrame;
            EndFrame = endFrame;
            ActionInstanceId = actionInstanceId;
            ProjectileId = projectileId;
            StableEventId = stableEventId;
            StopDistanceMillimeters = stopDistanceMillimeters;
            AnchorForwardOffsetMillimeters = anchorForwardOffsetMillimeters;
            AccelerationMillimetersPerSecondSquared = accelerationMillimetersPerSecondSquared;
            TerminalSpeedMillimetersPerSecond = terminalSpeedMillimetersPerSecond;
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

        public static CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample[] Capture(
            CharacterTestHookPullRuntimeState state)
        {
            IReadOnlyList<CharacterTestHookPullSnapshot> activePulls = state != null
                ? state.ActivePulls
                : null;
            int count = activePulls != null ? activePulls.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample>();

            var samples = new CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestHookPullSnapshot pull = activePulls[i];
                samples[i] = new CharacterTestRuntimeAiLabFrameSyncHookPullActiveSample(
                    pull.CasterEntityId,
                    pull.TargetEntityId,
                    pull.StartFrame,
                    pull.EndFrame,
                    pull.ActionInstanceId,
                    pull.ProjectileId,
                    pull.StableEventId,
                    pull.StopDistanceMillimeters,
                    pull.AnchorForwardOffsetMillimeters,
                    pull.AccelerationMillimetersPerSecondSquared,
                    pull.TerminalSpeedMillimetersPerSecond);
            }

            return samples;
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncAreaConfinementSample
    {
        public CharacterTestRuntimeAiLabFrameSyncAreaConfinementSample(
            bool runtimeAvailable,
            int schemaVersion,
            int moduleId,
            int nextPendingChannelId,
            CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample[] pendingChannels,
            bool sourceRuntimeAvailable,
            int sourceSchemaVersion,
            int sourceModuleId,
            int sourceNextRuntimeSourceId,
            CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample[] activeSources)
        {
            RuntimeAvailable = runtimeAvailable;
            SchemaVersion = schemaVersion;
            ModuleId = moduleId;
            NextPendingChannelId = nextPendingChannelId;
            PendingChannels = pendingChannels
                ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample>();
            SourceRuntimeAvailable = sourceRuntimeAvailable;
            SourceSchemaVersion = sourceSchemaVersion;
            SourceModuleId = sourceModuleId;
            SourceNextRuntimeSourceId = sourceNextRuntimeSourceId;
            ActiveSources = activeSources ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample>();
        }

        public bool RuntimeAvailable { get; }
        public int SchemaVersion { get; }
        public int ModuleId { get; }
        public int NextPendingChannelId { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample> PendingChannels { get; }
        public int PendingChannelCount => PendingChannels != null ? PendingChannels.Count : 0;
        public bool SourceRuntimeAvailable { get; }
        public int SourceSchemaVersion { get; }
        public int SourceModuleId { get; }
        public int SourceNextRuntimeSourceId { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample> ActiveSources { get; }
        public int ActiveSourceCount => ActiveSources != null ? ActiveSources.Count : 0;

        public static CharacterTestRuntimeAiLabFrameSyncAreaConfinementSample Capture(
            CharacterTestAreaConfinementRuntimeState state)
        {
            CharacterTestMechanicSourceRuntimeState source = state != null
                ? state.SourceRuntime
                : null;
            return new CharacterTestRuntimeAiLabFrameSyncAreaConfinementSample(
                state != null,
                state != null ? state.SchemaVersion : 0,
                state != null ? state.ModuleId : 0,
                state != null ? state.NextPendingChannelId : 0,
                CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample.Capture(state),
                source != null,
                source != null ? source.SchemaVersion : 0,
                source != null ? source.ModuleId : 0,
                source != null ? source.NextRuntimeSourceId : 0,
                CharacterTestRuntimeAiLabFrameSyncMechanicSourceSample.Capture(source));
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample
    {
        public CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample(
            int pendingChannelId,
            int casterEntityId,
            int sourceGameplayEntityIndex,
            int sourceGameplayEntityGeneration,
            int sourceCombatEntityId,
            long centerXRaw,
            long centerYRaw,
            long centerZRaw,
            long forwardXRaw,
            long forwardYRaw,
            long forwardZRaw,
            int landingMode,
            long startFrame,
            long activationFrame,
            int localFrame,
            int sourceOrder,
            int traceId)
        {
            PendingChannelId = pendingChannelId;
            CasterEntityId = casterEntityId;
            SourceGameplayEntityIndex = sourceGameplayEntityIndex;
            SourceGameplayEntityGeneration = sourceGameplayEntityGeneration;
            SourceCombatEntityId = sourceCombatEntityId;
            CenterXRaw = centerXRaw;
            CenterYRaw = centerYRaw;
            CenterZRaw = centerZRaw;
            ForwardXRaw = forwardXRaw;
            ForwardYRaw = forwardYRaw;
            ForwardZRaw = forwardZRaw;
            LandingMode = landingMode;
            StartFrame = startFrame;
            ActivationFrame = activationFrame;
            LocalFrame = localFrame;
            SourceOrder = sourceOrder;
            TraceId = traceId;
        }

        public int PendingChannelId { get; }
        public int CasterEntityId { get; }
        public int SourceGameplayEntityIndex { get; }
        public int SourceGameplayEntityGeneration { get; }
        public int SourceCombatEntityId { get; }
        public long CenterXRaw { get; }
        public long CenterYRaw { get; }
        public long CenterZRaw { get; }
        public long ForwardXRaw { get; }
        public long ForwardYRaw { get; }
        public long ForwardZRaw { get; }
        public int LandingMode { get; }
        public long StartFrame { get; }
        public long ActivationFrame { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public int TraceId { get; }

        public static CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample[] Capture(
            CharacterTestAreaConfinementRuntimeState state)
        {
            IReadOnlyList<CharacterTestAreaConfinementPendingChannelSnapshot> pendingChannels =
                state != null ? state.PendingChannels : null;
            int count = pendingChannels != null ? pendingChannels.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample>();

            var samples = new CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestAreaConfinementPendingChannelSnapshot channel = pendingChannels[i];
                samples[i] = new CharacterTestRuntimeAiLabFrameSyncAreaConfinementPendingSample(
                    channel.PendingChannelId,
                    channel.CasterEntityId,
                    channel.SourceGameplayEntityIndex,
                    channel.SourceGameplayEntityGeneration,
                    channel.SourceCombatEntityId,
                    channel.CenterXRaw,
                    channel.CenterYRaw,
                    channel.CenterZRaw,
                    channel.ForwardXRaw,
                    channel.ForwardYRaw,
                    channel.ForwardZRaw,
                    (int)channel.LandingMode,
                    channel.StartFrame,
                    channel.ActivationFrame,
                    channel.LocalFrame,
                    channel.SourceOrder,
                    channel.TraceId);
            }

            return samples;
        }
    }
}

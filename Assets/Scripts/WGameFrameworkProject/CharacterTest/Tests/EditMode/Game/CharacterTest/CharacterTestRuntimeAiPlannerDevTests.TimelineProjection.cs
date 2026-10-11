using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevTests
    {

        private static void AssertPanelChildDeclared(string panelText, string childName)
        {
            StringAssert.Contains("name=\"" + childName + "\"", panelText);
        }

        private sealed class RuntimeAiLabHeadlessFrameRecord
        {
            public RuntimeAiLabHeadlessFrameRecord(
                int frame,
                double elapsedSeconds,
                RuntimeCommand[] commands,
                RuntimeCustomState checkpoint,
                CharacterTestRuntimeAiLabFrameSyncSample sample,
                CharacterTestRuntimeAiLabAuthoritySample authority)
            {
                Frame = frame;
                ElapsedSeconds = elapsedSeconds;
                Commands = commands ?? Array.Empty<RuntimeCommand>();
                Checkpoint = checkpoint;
                Sample = sample;
                Authority = authority;
            }

            public int Frame { get; }
            public double ElapsedSeconds { get; }
            public RuntimeCommand[] Commands { get; }
            public RuntimeCustomState Checkpoint { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
            public CharacterTestRuntimeAiLabAuthoritySample Authority { get; }
        }

        private sealed class FrameSyncCombatReplayResult
        {
            public FrameSyncCombatReplayResult(
                int targetEntityId,
                int hitRecordIndex,
                IReadOnlyList<FrameSyncCombatFrameRecord> records)
            {
                TargetEntityId = targetEntityId;
                HitRecordIndex = hitRecordIndex;
                Records = records ?? Array.Empty<FrameSyncCombatFrameRecord>();
            }

            public int TargetEntityId { get; }
            public int HitRecordIndex { get; }
            public IReadOnlyList<FrameSyncCombatFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncCombatFrameRecord
        {
            public FrameSyncCombatFrameRecord(
                int frame,
                CharacterTestRuntimeAiLabFrameSyncSample sample)
            {
                Frame = frame;
                Sample = sample;
            }

            public int Frame { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
        }

        private sealed class FrameSyncChainLightningReplayResult
        {
            public FrameSyncChainLightningReplayResult(
                int chainRecordIndex,
                IReadOnlyList<FrameSyncChainLightningFrameRecord> records)
            {
                ChainRecordIndex = chainRecordIndex;
                Records = records ?? Array.Empty<FrameSyncChainLightningFrameRecord>();
            }

            public int ChainRecordIndex { get; }
            public IReadOnlyList<FrameSyncChainLightningFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncChainLightningFrameRecord
        {
            public FrameSyncChainLightningFrameRecord(
                int frame,
                CharacterTestRuntimeAiLabFrameSyncSample sample,
                IReadOnlyList<int> chainTargetEntityIds)
            {
                Frame = frame;
                Sample = sample;
                ChainTargetEntityIds = chainTargetEntityIds ?? Array.Empty<int>();
            }

            public int Frame { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
            public IReadOnlyList<int> ChainTargetEntityIds { get; }
        }

        private sealed class FrameSyncLaserPathReplayResult
        {
            public FrameSyncLaserPathReplayResult(
                int laserPathRecordIndex,
                IReadOnlyList<FrameSyncLaserPathFrameRecord> records)
            {
                LaserPathRecordIndex = laserPathRecordIndex;
                Records = records ?? Array.Empty<FrameSyncLaserPathFrameRecord>();
            }

            public int LaserPathRecordIndex { get; }
            public IReadOnlyList<FrameSyncLaserPathFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncLaserPathFrameRecord
        {
            public FrameSyncLaserPathFrameRecord(
                int frame,
                CharacterTestNetworkCharacterSnapshot characterSnapshot,
                CombatProjectileRuntimeState projectileState,
                CharacterTestRuntimeAiLabFrameSyncSample sample)
            {
                Frame = frame;
                CharacterSnapshot = characterSnapshot;
                ProjectileState = projectileState;
                Sample = sample;
            }

            public int Frame { get; }
            public CharacterTestNetworkCharacterSnapshot CharacterSnapshot { get; }
            public CombatProjectileRuntimeState ProjectileState { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
        }

        private sealed class FrameSyncGuardAuraReplayResult
        {
            public FrameSyncGuardAuraReplayResult(
                int checkpointRecordIndex,
                int firstAppliedRecordIndex,
                IReadOnlyList<FrameSyncGuardAuraFrameRecord> records)
            {
                CheckpointRecordIndex = checkpointRecordIndex;
                FirstAppliedRecordIndex = firstAppliedRecordIndex;
                Records = records ?? Array.Empty<FrameSyncGuardAuraFrameRecord>();
            }

            public int CheckpointRecordIndex { get; }
            public int FirstAppliedRecordIndex { get; }
            public IReadOnlyList<FrameSyncGuardAuraFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncGuardAuraFrameRecord
        {
            public FrameSyncGuardAuraFrameRecord(
                int frame,
                CharacterTestNetworkCharacterSnapshot characterSnapshot,
                CharacterTestRuntimeAiLabFrameSyncSample sample)
            {
                Frame = frame;
                CharacterSnapshot = characterSnapshot;
                Sample = sample;
            }

            public int Frame { get; }
            public CharacterTestNetworkCharacterSnapshot CharacterSnapshot { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
        }

        private sealed class FrameSyncPhysicalShieldReplayResult
        {
            public FrameSyncPhysicalShieldReplayResult(
                int checkpointRecordIndex,
                int firstActiveShieldRecordIndex,
                IReadOnlyList<FrameSyncPhysicalShieldFrameRecord> records)
            {
                CheckpointRecordIndex = checkpointRecordIndex;
                FirstActiveShieldRecordIndex = firstActiveShieldRecordIndex;
                Records = records ?? Array.Empty<FrameSyncPhysicalShieldFrameRecord>();
            }

            public int CheckpointRecordIndex { get; }
            public int FirstActiveShieldRecordIndex { get; }
            public IReadOnlyList<FrameSyncPhysicalShieldFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncPhysicalShieldFrameRecord
        {
            public FrameSyncPhysicalShieldFrameRecord(
                int frame,
                CharacterTestNetworkCharacterSnapshot characterSnapshot,
                CharacterTestPhysicalShieldRuntimeState physicalShieldState,
                CharacterTestRuntimeAiLabFrameSyncSample sample,
                long physicalShieldHash)
            {
                Frame = frame;
                CharacterSnapshot = characterSnapshot;
                PhysicalShieldState = physicalShieldState;
                Sample = sample;
                PhysicalShieldHash = physicalShieldHash;
            }

            public int Frame { get; }
            public CharacterTestNetworkCharacterSnapshot CharacterSnapshot { get; }
            public CharacterTestPhysicalShieldRuntimeState PhysicalShieldState { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
            public long PhysicalShieldHash { get; }
        }

        private sealed class FrameSyncProjectileReplayResult
        {
            public FrameSyncProjectileReplayResult(
                int firstActiveRecordIndex,
                IReadOnlyList<FrameSyncProjectileFrameRecord> records,
                int firstAcceptedImpactRecordIndex = -1)
            {
                FirstActiveRecordIndex = firstActiveRecordIndex;
                Records = records ?? Array.Empty<FrameSyncProjectileFrameRecord>();
                FirstAcceptedImpactRecordIndex = firstAcceptedImpactRecordIndex;
            }

            public int FirstActiveRecordIndex { get; }
            public int FirstAcceptedImpactRecordIndex { get; }
            public IReadOnlyList<FrameSyncProjectileFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncProjectileFrameRecord
        {
            public FrameSyncProjectileFrameRecord(
                int frame,
                CombatProjectileRuntimeState projectileState,
                CharacterTestRuntimeAiLabFrameSyncSample sample)
            {
                Frame = frame;
                ProjectileState = projectileState;
                Sample = sample;
            }

            public int Frame { get; }
            public CombatProjectileRuntimeState ProjectileState { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
        }

        private sealed class FrameSyncStunBoltReplayResult
        {
            public FrameSyncStunBoltReplayResult(
                int targetEntityId,
                int firstActiveRecordIndex,
                int checkpointRecordIndex,
                int firstStunnedRecordIndex,
                IReadOnlyList<FrameSyncStunBoltFrameRecord> records)
            {
                TargetEntityId = targetEntityId;
                FirstActiveRecordIndex = firstActiveRecordIndex;
                CheckpointRecordIndex = checkpointRecordIndex;
                FirstStunnedRecordIndex = firstStunnedRecordIndex;
                Records = records ?? Array.Empty<FrameSyncStunBoltFrameRecord>();
            }

            public int TargetEntityId { get; }
            public int FirstActiveRecordIndex { get; }
            public int CheckpointRecordIndex { get; }
            public int FirstStunnedRecordIndex { get; }
            public IReadOnlyList<FrameSyncStunBoltFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncStunBoltFrameRecord
        {
            public FrameSyncStunBoltFrameRecord(
                int frame,
                CombatProjectileRuntimeState projectileState,
                CharacterTestRuntimeAiLabFrameSyncSample sample,
                CharacterTestCharacterTransformComponent targetTransform,
                bool targetHasStunBuff,
                bool targetHasStunnedStatus)
            {
                Frame = frame;
                ProjectileState = projectileState;
                Sample = sample;
                TargetTransform = targetTransform;
                TargetHasStunBuff = targetHasStunBuff;
                TargetHasStunnedStatus = targetHasStunnedStatus;
            }

            public int Frame { get; }
            public CombatProjectileRuntimeState ProjectileState { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
            public CharacterTestCharacterTransformComponent TargetTransform { get; }
            public bool TargetHasStunBuff { get; }
            public bool TargetHasStunnedStatus { get; }
        }

        private sealed class FrameSyncMeteorBarrageRunnerReplayResult
        {
            public FrameSyncMeteorBarrageRunnerReplayResult(
                IReadOnlyList<FrameSyncMeteorBarrageRunnerFrameRecord> records)
            {
                Records = records ?? Array.Empty<FrameSyncMeteorBarrageRunnerFrameRecord>();
            }

            public IReadOnlyList<FrameSyncMeteorBarrageRunnerFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncMeteorBarrageRunnerFrameRecord
        {
            public FrameSyncMeteorBarrageRunnerFrameRecord(
                int frame,
                CharacterTestNetworkCharacterSnapshot characterSnapshot,
                CombatProjectileRuntimeState projectileState,
                CharacterTestRuntimeAiLabFrameSyncSample sample)
            {
                Frame = frame;
                CharacterSnapshot = characterSnapshot;
                ProjectileState = projectileState;
                Sample = sample;
            }

            public int Frame { get; }
            public CharacterTestNetworkCharacterSnapshot CharacterSnapshot { get; }
            public CombatProjectileRuntimeState ProjectileState { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
        }

        private sealed class FrameSyncHookChainReplayResult
        {
            public FrameSyncHookChainReplayResult(
                int firstActiveRecordIndex,
                int firstHookPullRecordIndex,
                IReadOnlyList<FrameSyncHookChainFrameRecord> records)
            {
                FirstActiveRecordIndex = firstActiveRecordIndex;
                FirstHookPullRecordIndex = firstHookPullRecordIndex;
                Records = records ?? Array.Empty<FrameSyncHookChainFrameRecord>();
            }

            public int FirstActiveRecordIndex { get; }
            public int FirstHookPullRecordIndex { get; }
            public IReadOnlyList<FrameSyncHookChainFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncHookChainFrameRecord
        {
            public FrameSyncHookChainFrameRecord(
                int frame,
                CombatProjectileRuntimeState projectileState,
                CharacterTestHookPullRuntimeState hookPullState,
                CharacterTestRuntimeAiLabFrameSyncSample sample,
                CharacterTestCharacterTransformComponent targetTransform,
                long hookPullHash)
            {
                Frame = frame;
                ProjectileState = projectileState;
                HookPullState = hookPullState;
                Sample = sample;
                TargetTransform = targetTransform;
                HookPullHash = hookPullHash;
            }

            public int Frame { get; }
            public CombatProjectileRuntimeState ProjectileState { get; }
            public CharacterTestHookPullRuntimeState HookPullState { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
            public CharacterTestCharacterTransformComponent TargetTransform { get; }
            public long HookPullHash { get; }
        }

        private sealed class FrameSyncArcMineReplayResult
        {
            public FrameSyncArcMineReplayResult(
                int firstCarrierRecordIndex,
                int firstDropRecordIndex,
                int firstActiveMineRecordIndex,
                IReadOnlyList<FrameSyncArcMineFrameRecord> records)
            {
                FirstCarrierRecordIndex = firstCarrierRecordIndex;
                FirstDropRecordIndex = firstDropRecordIndex;
                FirstActiveMineRecordIndex = firstActiveMineRecordIndex;
                Records = records ?? Array.Empty<FrameSyncArcMineFrameRecord>();
            }

            public int FirstCarrierRecordIndex { get; }
            public int FirstDropRecordIndex { get; }
            public int FirstActiveMineRecordIndex { get; }
            public IReadOnlyList<FrameSyncArcMineFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncArcMineFrameRecord
        {
            public FrameSyncArcMineFrameRecord(
                int frame,
                CombatProjectileRuntimeState projectileState,
                CharacterTestArcMineRuntimeState arcMineState,
                CharacterTestRuntimeAiLabFrameSyncSample sample,
                CharacterTestCharacterTransformComponent targetTransform,
                long arcMineHash)
            {
                Frame = frame;
                ProjectileState = projectileState;
                ArcMineState = arcMineState;
                Sample = sample;
                TargetTransform = targetTransform;
                ArcMineHash = arcMineHash;
            }

            public int Frame { get; }
            public CombatProjectileRuntimeState ProjectileState { get; }
            public CharacterTestArcMineRuntimeState ArcMineState { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
            public CharacterTestCharacterTransformComponent TargetTransform { get; }
            public long ArcMineHash { get; }
        }

        private sealed class FrameSyncRuneTurretReplayResult
        {
            public FrameSyncRuneTurretReplayResult(
                int firstDeployCarrierRecordIndex,
                int firstActiveTurretRecordIndex,
                int firstShotProjectileRecordIndex,
                IReadOnlyList<FrameSyncRuneTurretFrameRecord> records)
            {
                FirstDeployCarrierRecordIndex = firstDeployCarrierRecordIndex;
                FirstActiveTurretRecordIndex = firstActiveTurretRecordIndex;
                FirstShotProjectileRecordIndex = firstShotProjectileRecordIndex;
                Records = records ?? Array.Empty<FrameSyncRuneTurretFrameRecord>();
            }

            public int FirstDeployCarrierRecordIndex { get; }
            public int FirstActiveTurretRecordIndex { get; }
            public int FirstShotProjectileRecordIndex { get; }
            public IReadOnlyList<FrameSyncRuneTurretFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncRuneTurretFrameRecord
        {
            public FrameSyncRuneTurretFrameRecord(
                int frame,
                CombatProjectileRuntimeState projectileState,
                CharacterTestRuneTurretRuntimeState runeTurretState,
                CharacterTestRuntimeAiLabFrameSyncSample sample,
                CharacterTestCharacterTransformComponent targetTransform,
                long runeTurretHash)
            {
                Frame = frame;
                ProjectileState = projectileState;
                RuneTurretState = runeTurretState;
                Sample = sample;
                TargetTransform = targetTransform;
                RuneTurretHash = runeTurretHash;
            }

            public int Frame { get; }
            public CombatProjectileRuntimeState ProjectileState { get; }
            public CharacterTestRuneTurretRuntimeState RuneTurretState { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
            public CharacterTestCharacterTransformComponent TargetTransform { get; }
            public long RuneTurretHash { get; }
        }

        private sealed class FrameSyncAreaConfinementReplayResult
        {
            public FrameSyncAreaConfinementReplayResult(
                int firstPendingChannelRecordIndex,
                int firstActiveZoneRecordIndex,
                int firstRootedTargetRecordIndex,
                IReadOnlyList<FrameSyncAreaConfinementFrameRecord> records)
            {
                FirstPendingChannelRecordIndex = firstPendingChannelRecordIndex;
                FirstActiveZoneRecordIndex = firstActiveZoneRecordIndex;
                FirstRootedTargetRecordIndex = firstRootedTargetRecordIndex;
                Records = records ?? Array.Empty<FrameSyncAreaConfinementFrameRecord>();
            }

            public int FirstPendingChannelRecordIndex { get; }
            public int FirstActiveZoneRecordIndex { get; }
            public int FirstRootedTargetRecordIndex { get; }
            public IReadOnlyList<FrameSyncAreaConfinementFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncAreaConfinementFrameRecord
        {
            public FrameSyncAreaConfinementFrameRecord(
                int frame,
                CharacterTestNetworkCharacterSnapshot characterSnapshot,
                CharacterTestNetworkCharacterSnapshot targetCharacterSnapshot,
                CharacterTestAreaConfinementRuntimeState areaConfinementState,
                CharacterTestRuntimeAiLabFrameSyncSample sample,
                int targetEntityId,
                bool hasTargetBuffs,
                GameplayComponentBuffSetComponent targetBuffs,
                bool hasTargetStatuses,
                GameplayStatusComponent targetStatuses,
                long areaConfinementHash)
            {
                Frame = frame;
                CharacterSnapshot = characterSnapshot;
                TargetCharacterSnapshot = targetCharacterSnapshot;
                AreaConfinementState = areaConfinementState;
                Sample = sample;
                TargetEntityId = targetEntityId;
                HasTargetBuffs = hasTargetBuffs;
                TargetBuffs = targetBuffs;
                HasTargetStatuses = hasTargetStatuses;
                TargetStatuses = targetStatuses;
                AreaConfinementHash = areaConfinementHash;
            }

            public int Frame { get; }
            public CharacterTestNetworkCharacterSnapshot CharacterSnapshot { get; }
            public CharacterTestNetworkCharacterSnapshot TargetCharacterSnapshot { get; }
            public CharacterTestAreaConfinementRuntimeState AreaConfinementState { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
            public int TargetEntityId { get; }
            public bool HasTargetBuffs { get; }
            public GameplayComponentBuffSetComponent TargetBuffs { get; }
            public bool HasTargetStatuses { get; }
            public GameplayStatusComponent TargetStatuses { get; }
            public long AreaConfinementHash { get; }
        }

        private sealed class FrameSyncPoisonBottlePoolReplayResult
        {
            public FrameSyncPoisonBottlePoolReplayResult(
                int firstProjectileRecordIndex,
                int firstActivePoolRecordIndex,
                int firstPoisonTickRecordIndex,
                IReadOnlyList<FrameSyncPoisonBottlePoolFrameRecord> records)
            {
                FirstProjectileRecordIndex = firstProjectileRecordIndex;
                FirstActivePoolRecordIndex = firstActivePoolRecordIndex;
                FirstPoisonTickRecordIndex = firstPoisonTickRecordIndex;
                Records = records ?? Array.Empty<FrameSyncPoisonBottlePoolFrameRecord>();
            }

            public int FirstProjectileRecordIndex { get; }
            public int FirstActivePoolRecordIndex { get; }
            public int FirstPoisonTickRecordIndex { get; }
            public IReadOnlyList<FrameSyncPoisonBottlePoolFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncPoisonBottlePoolFrameRecord
        {
            public FrameSyncPoisonBottlePoolFrameRecord(
                int frame,
                CombatProjectileRuntimeState projectileState,
                CharacterTestMechanicSourceRuntimeState poisonBottlePoolState,
                CharacterTestRuntimeAiLabFrameSyncSample sample,
                CharacterTestNetworkCharacterSnapshot attackerSnapshot,
                CharacterTestNetworkCharacterSnapshot targetSnapshot,
                int targetEntityId,
                bool hasTargetBuffs,
                GameplayComponentBuffSetComponent targetBuffs,
                bool hasTargetStatuses,
                GameplayStatusComponent targetStatuses,
                long poisonBottlePoolHash)
            {
                Frame = frame;
                ProjectileState = projectileState;
                PoisonBottlePoolState = poisonBottlePoolState;
                Sample = sample;
                AttackerSnapshot = attackerSnapshot;
                TargetSnapshot = targetSnapshot;
                TargetEntityId = targetEntityId;
                HasTargetBuffs = hasTargetBuffs;
                TargetBuffs = targetBuffs;
                HasTargetStatuses = hasTargetStatuses;
                TargetStatuses = targetStatuses;
                PoisonBottlePoolHash = poisonBottlePoolHash;
            }

            public int Frame { get; }
            public CombatProjectileRuntimeState ProjectileState { get; }
            public CharacterTestMechanicSourceRuntimeState PoisonBottlePoolState { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
            public CharacterTestNetworkCharacterSnapshot AttackerSnapshot { get; }
            public CharacterTestNetworkCharacterSnapshot TargetSnapshot { get; }
            public int TargetEntityId { get; }
            public bool HasTargetBuffs { get; }
            public GameplayComponentBuffSetComponent TargetBuffs { get; }
            public bool HasTargetStatuses { get; }
            public GameplayStatusComponent TargetStatuses { get; }
            public long PoisonBottlePoolHash { get; }
        }

        [Test]
        public void RuntimeAiLabTimelineEventIndex_CapsFrameAndEmitsDeterministicOverflow()
        {
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(10, 4);
            var events = new List<CharacterTestRuntimeEvent>();
            for (int i = 0; i < 100; i++)
            {
                events.Add(new CharacterTestRuntimeEvent(
                    new RuntimeFrame(10),
                    CharacterTestRuntimeEventType.CommandAccepted,
                    commandId: 1,
                    sourceId: i + 1,
                    targetId: 0));
            }

            index.CaptureFrame(10, null, events, null, null);

            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineEventIndex.MaxEventsPerFrame, index.GetCount(10));
            CharacterTestRuntimeAiLabTimelineEventSample overflow = index.GetSample(10, 63);
            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineEventKind.Warning, overflow.Kind);
            Assert.AreEqual("TimelineOverflow", overflow.Category);
            Assert.AreEqual(37, overflow.Value);
        }

        [Test]
        public void RuntimeAiLabTimelineProjection_BuildsFiveTracksAndOpenActionSpanFromTypedEvent()
        {
            CharacterTestRuntimeAiPlannerSnapshot snapshot = CreateEmptyRuntimeAiPlannerSnapshot();
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(8);
            var identity = new CharacterTestRuntimeAiLabTimelineSessionIdentity(1, 10, 8);
            timeline.BeginSession(identity);
            timeline.AddRecord(10, 0f, 0d, snapshot, null, null, 10L);
            timeline.AddRecord(11, 0f, 0d, snapshot, null, null, 11L);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(identity);
            var actionEvents = new CharacterTestCharacterActionTimelineEventBuffer(8);
            actionEvents.Add(new CharacterTestCharacterActionTimelineEvent(
                10,
                1,
                CharacterActionRunnerEventKind.ActionStarted,
                77L,
                MxFramework.Tests.CharacterActionLegacyFixtureIds.From("light_attack_1"),
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0039),
                string.Empty));
            index.CaptureFrame(10, null, null, null, actionEvents);
            index.CaptureFrame(11, null, null, null, null);

            var projector = new CharacterTestRuntimeAiLabTimelineProjector();
            CharacterTestRuntimeAiLabTimelineProjection projection = projector.Build(
                timeline,
                index,
                8,
                1,
                new CharacterTestRuntimeAiLabTimelineViewport(10, 17, -1, 1200f, true));

            Assert.AreEqual(5, projection.Tracks.Count);
            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineValidity.Valid, projection.Validity);
            Assert.IsFalse(projection.Tracks[0].Available);
            Assert.AreEqual(0, projection.Tracks[0].Items.Count);
            Assert.IsTrue(projection.Tracks[2].Items[0].IsOpen);
            Assert.AreEqual("light_attack_1", projection.Tracks[2].Items[0].Label);
            Assert.AreEqual(11, projection.Tracks[2].Items[0].EndFrame);
            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineItemProvenance.TypedEvent, projection.Tracks[2].Items[0].Provenance);
            Assert.AreEqual(0, projection.Tracks[4].Items.Count);
        }

        [Test]
        public void RuntimeAiLabExecutionIdentity_ParsesGeneratedBehaviorEntry()
        {
            Assert.IsTrue(CharacterTestRuntimeAiLabExecutionIdentity.TryParseRuntimeEntryId(
                "Approach target [entry.approach]",
                out string entryId));
            Assert.AreEqual("entry.approach", entryId);
            Assert.IsFalse(CharacterTestRuntimeAiLabExecutionIdentity.TryParseRuntimeEntryId(
                "Approach target",
                out entryId));
            Assert.AreEqual(string.Empty, entryId);
        }

        [Test]
        public void RuntimeAiLabTimelineProjection_DecisionCarriesBehaviorIdentityAndLiveMarker()
        {
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = CreatePreviewAgent(
                selectedActionName: "Approach target [entry.approach]");
            var snapshot = new CharacterTestRuntimeAiPlannerSnapshot(
                available: true,
                profileId: "behavior-identity",
                agents: new[] { agent },
                validationReport: "ok");
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var identity = new CharacterTestRuntimeAiLabTimelineSessionIdentity(1, 30, 4);
            timeline.BeginSession(identity);
            timeline.AddRecord(30, 0f, 0d, snapshot, null, null, 30L);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(identity);
            index.CaptureFrame(30, null, null, null, null);

            CharacterTestRuntimeAiLabTimelineProjection projection =
                new CharacterTestRuntimeAiLabTimelineProjector().Build(
                    timeline,
                    index,
                    4,
                    agent.EntityId,
                    new CharacterTestRuntimeAiLabTimelineViewport(30, 33, -1, 1200f, true));

            CharacterTestRuntimeAiLabTimelineItem decision = projection.Tracks[1].Items[0];
            Assert.AreEqual("entry.approach", decision.BehaviorEntryId);
            Assert.IsTrue(decision.IsOpen);
            StringAssert.StartsWith("▶ ", decision.Label);
            StringAssert.Contains("行为条目 entry.approach", decision.Tooltip);
        }

        [Test]
        public void RuntimeAiLabTimelineProjection_IsStableAndReadOnly()
        {
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = CreatePreviewAgent(
                selectedActionName: "Approach target [entry.approach]");
            var snapshot = new CharacterTestRuntimeAiPlannerSnapshot(
                available: true,
                profileId: "stable-read-only",
                agents: new[] { agent },
                validationReport: "ok");
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var identity = new CharacterTestRuntimeAiLabTimelineSessionIdentity(1, 20, 4);
            timeline.BeginSession(identity);
            timeline.AddRecord(20, 0f, 0d, snapshot, null, null, 20L);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(identity);
            index.CaptureFrame(20, null, null, null, null);
            var projector = new CharacterTestRuntimeAiLabTimelineProjector();
            var viewport = new CharacterTestRuntimeAiLabTimelineViewport(20, 23, -1, 1200f, true);

            CharacterTestRuntimeAiLabTimelineProjection first = projector.Build(timeline, index, 4, agent.EntityId, viewport);
            CharacterTestRuntimeAiLabTimelineProjection second = projector.Build(timeline, index, 4, agent.EntityId, viewport);

            Assert.AreEqual(first.ContentRevision, second.ContentRevision);
            Assert.AreEqual(first.Tracks[1].Items[0].StableKey, second.Tracks[1].Items[0].StableKey);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<CharacterTestRuntimeAiLabTimelineTrackProjection>)first.Tracks).Add(first.Tracks[0]));
        }

        [Test]
        public void RuntimeAiLabTimelineProjection_ClusterRetainsStableMembers()
        {
            CharacterTestRuntimeAiPlannerSnapshot snapshot = CreateEmptyRuntimeAiPlannerSnapshot();
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(900);
            var identity = new CharacterTestRuntimeAiLabTimelineSessionIdentity(1, 10, 900);
            timeline.BeginSession(identity);
            timeline.AddRecord(10, 0f, 0d, snapshot, null, null, 10L);
            timeline.AddRecord(11, 0f, 0d, snapshot, null, null, 11L);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(identity);
            index.CaptureFrame(10, null, new[]
            {
                new CharacterTestRuntimeEvent(
                    new RuntimeFrame(10),
                    CharacterTestRuntimeEventType.CommandAccepted,
                    commandId: 101,
                    sourceId: 1,
                    targetId: 2)
            }, null, null);
            index.CaptureFrame(11, null, new[]
            {
                new CharacterTestRuntimeEvent(
                    new RuntimeFrame(11),
                    CharacterTestRuntimeEventType.CommandAccepted,
                    commandId: 102,
                    sourceId: 2,
                    targetId: 1)
            }, null, null);

            CharacterTestRuntimeAiLabTimelineProjection projection =
                new CharacterTestRuntimeAiLabTimelineProjector().Build(
                    timeline,
                    index,
                    900,
                    0,
                    new CharacterTestRuntimeAiLabTimelineViewport(10, 909, -1, 1200f, true));

            CharacterTestRuntimeAiLabTimelineItem cluster = projection.Tracks[2].Items[0];
            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineItemKind.Cluster, cluster.ItemKind);
            Assert.AreEqual(2, cluster.Members.Count);
            Assert.AreNotEqual(cluster.Members[0].StableKey, cluster.Members[1].StableKey);
        }

        [Test]
        public void RuntimeAiLabTimeline_SelectItemDoesNotSeekOrMoveSelectedFrame()
        {
            var host = new GameObject("Runtime AI Lab Timeline Selection Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                CharacterTestRuntimeAiPlannerSnapshot snapshot = CreateEmptyRuntimeAiPlannerSnapshot();
                MethodInfo beginSession = typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetMethod(
                    "BeginVisibleTimelineSession",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(beginSession);
                beginSession.Invoke(controller, new object[] { 10 });
                controller.Timeline.AddRecord(10, 0f, 0d, snapshot, null, null, 10L);
                controller.Timeline.AddRecord(11, 0f, 0d, snapshot, null, null, 11L);
                FieldInfo eventIndexField = typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField(
                    "_timelineEventIndex",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(eventIndexField);
                var eventIndex = (CharacterTestRuntimeAiLabTimelineEventIndex)eventIndexField.GetValue(controller);
                var actionEvents = new CharacterTestCharacterActionTimelineEventBuffer(4);
                actionEvents.Add(new CharacterTestCharacterActionTimelineEvent(
                    10,
                    1,
                    CharacterActionRunnerEventKind.ActionStarted,
                    77L,
                    MxFramework.Tests.CharacterActionLegacyFixtureIds.From("light_attack_1"),
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0039),
                    string.Empty));
                eventIndex.CaptureFrame(10, null, null, null, actionEvents);
                eventIndex.CaptureFrame(11, null, null, null, null);
                controller.Timeline.SelectFrame(11);
                CharacterTestRuntimeAiLabTimelineItem baseline =
                    controller.BuildTimelineProjection().Tracks[2].Items[0];

                Assert.IsTrue(controller.SelectTimelineItem(baseline.StableKey));

                Assert.AreEqual(11, controller.Timeline.SelectedFrame);
                Assert.IsTrue(controller.TryGetSelectedTimelineItem(out CharacterTestRuntimeAiLabTimelineItem selected));
                Assert.AreEqual(baseline.StableKey, selected.StableKey);
                Assert.AreEqual(baseline.StableKey, controller.SelectedTimelineVisualStableKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabTimeline_ResolvesExactBehaviorTransitionLocator()
        {
            var host = new GameObject("Runtime AI Lab Transition Locator Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                CharacterTestRuntimeAiPlannerSnapshot snapshot = CreateEmptyRuntimeAiPlannerSnapshot();
                MethodInfo beginSession = typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetMethod(
                    "BeginVisibleTimelineSession",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(beginSession);
                beginSession.Invoke(controller, new object[] { 10 });
                controller.Timeline.AddRecord(10, 0f, 0d, snapshot, null, null, 10L);
                FieldInfo eventIndexField = typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField(
                    "_timelineEventIndex",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(eventIndexField);
                var eventIndex = (CharacterTestRuntimeAiLabTimelineEventIndex)eventIndexField.GetValue(controller);
                var logicEvents = new CharacterTestRuntimeAiLabLogicEventBuffer();
                logicEvents.Add(new CharacterTestRuntimeAiLabLogicEvent(
                    10,
                    CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted,
                    "approach",
                    "threat",
                    "threat.to-dodge",
                    entityId: 7,
                    behaviorEntryId: "entry.dodge",
                    programSemanticHash: 42L));
                eventIndex.CaptureFrame(10, null, null, null, null, logicEvents);

                Assert.IsTrue(controller.TryResolveBehaviorTransitionTimelineLocator(
                    "threat.to-dodge",
                    10,
                    out CharacterTestRuntimeAiLabTimelineItemLocator locator));
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionCommitted,
                    locator.EventKind);
                Assert.AreEqual("threat.to-dodge", locator.SourceTraceId);
                Assert.AreEqual(10, locator.StartFrame);
                Assert.IsFalse(controller.TryResolveBehaviorTransitionTimelineLocator(
                    "other.transition",
                    10,
                    out _));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabTimeline_NearestFrameUsesEarlierTieAndContentRevisionOnlyForContent()
        {
            CharacterTestRuntimeAiPlannerSnapshot snapshot = CreateEmptyRuntimeAiPlannerSnapshot();
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(8);
            long initialRevision = timeline.ContentRevision;
            timeline.AddRecord(30, 0f, 0d, snapshot, null, null, 30L);
            timeline.AddRecord(34, 0f, 0d, snapshot, null, null, 34L);
            long contentRevision = timeline.ContentRevision;

            Assert.IsTrue(timeline.TryResolveNearestFrame(32, out int resolved, out bool clamped));
            Assert.AreEqual(30, resolved);
            Assert.IsTrue(clamped);
            Assert.IsTrue(timeline.SelectFrame(30));
            Assert.AreEqual(contentRevision, timeline.ContentRevision);
            Assert.Greater(contentRevision, initialRevision);
        }

        [Test]
        public void RuntimeAiLabTimeline_FrameLimitClampsAndCyclesOnlyInSetup()
        {
            var host = new GameObject("Runtime AI Lab Frame Limit Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();

                Assert.AreEqual(900, controller.FrameLimit);
                Assert.IsTrue(controller.TrySetFrameLimit(0));
                Assert.AreEqual(1, controller.FrameLimit);
                Assert.IsTrue(controller.CycleFrameLimitPreset());
                Assert.AreEqual(300, controller.FrameLimit);
                Assert.IsTrue(controller.TrySetFrameLimit(9999));
                Assert.AreEqual(1800, controller.FrameLimit);
                Assert.IsTrue(controller.CycleFrameLimitPreset());
                Assert.AreEqual(300, controller.FrameLimit);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabTimeline_FrameZeroSupportsLoopAndEditBranchMarkers()
        {
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline();
            CharacterTestRuntimeAiPlannerSnapshot snapshot = CreateEmptyRuntimeAiPlannerSnapshot();
            timeline.AddRecord(0, 0f, 0d, snapshot, null, null, 0L);
            timeline.AddRecord(1, 0f, 0d, snapshot, null, null, 1L);

            Assert.IsTrue(timeline.SelectFrame(0));
            Assert.IsTrue(timeline.SetLoopStartToSelection());
            Assert.IsTrue(timeline.SelectFrame(1));
            Assert.IsTrue(timeline.SetLoopEndToSelection());
            Assert.IsTrue(timeline.HasLoopRange);
            Assert.AreEqual(0, timeline.LoopStartFrame);

            Assert.IsTrue(timeline.SelectFrame(0));
            timeline.MarkEditBranchStart();
            timeline.AddRecord(1, 1f, 1d / 60d, snapshot, null, null, 2L);
            Assert.AreEqual(2, timeline.Count);
            Assert.Greater(
                timeline.Records[1].RecordGeneration,
                timeline.Records[0].RecordGeneration);
            Assert.AreEqual(2L, timeline.Records[1].StateHash);
        }

        [Test]
        public void RuntimeAiLabTimeline_ScrubCoalescesToLatestPendingFrame()
        {
            var host = new GameObject("Runtime AI Lab Scrub Coalescing Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.Timeline.BeginSession(
                    new CharacterTestRuntimeAiLabTimelineSessionIdentity(1, 10, 3));
                for (int frame = 10; frame <= 12; frame++)
                {
                    controller.Timeline.AddRecord(
                        frame,
                        frame,
                        frame / 60d,
                        CreateSnapshot("scrub-" + frame),
                        Array.Empty<RuntimeCommand>(),
                        authorityCheckpoint: null,
                        stateHash: frame);
                }

                typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetField("_seekInProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(controller, true);

                Assert.IsTrue(controller.BeginTimelineScrub(10));
                Assert.IsTrue(controller.UpdateTimelineScrub(12));
                Assert.IsTrue(controller.EndTimelineScrub(11));

                FieldInfo pendingFrame = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetField("_timelineScrubDesiredFrame", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo hasPending = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetField("_hasTimelineScrubDesiredFrame", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(pendingFrame);
                Assert.NotNull(hasPending);
                Assert.AreEqual(11, pendingFrame.GetValue(controller));
                Assert.IsTrue((bool)hasPending.GetValue(controller));

                controller.CancelTimelineScrub();
                Assert.IsFalse((bool)hasPending.GetValue(controller));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabTimeline_PlayheadAndLabelStayInsideRulerEdges()
        {
            Assert.AreEqual(227f,
                CharacterTestRuntimeAiLabTimelineGeometry.ClampPlayheadX(120f));
            Assert.AreEqual(1897f,
                CharacterTestRuntimeAiLabTimelineGeometry.ClampPlayheadX(2000f));
            Assert.AreEqual(224f,
                CharacterTestRuntimeAiLabTimelineGeometry.ClampLabelX(123f, 52f));
            Assert.AreEqual(1848f,
                CharacterTestRuntimeAiLabTimelineGeometry.ClampLabelX(2000f, 52f));
        }

        [Test]
        public void RuntimeAiLabTimeline_ZoomChangesHorizontalDensityWithoutChangingEventHeight()
        {
            const float width = 1732f;
            RuntimeFrameTimelineVisualMetrics detailed =
                CharacterTestRuntimeAiLabTimelineGeometry.ResolveVisualMetrics(
                    new CharacterTestRuntimeAiLabTimelineViewport(0, 119, 0, width, false),
                    width);
            RuntimeFrameTimelineVisualMetrics compact =
                CharacterTestRuntimeAiLabTimelineGeometry.ResolveVisualMetrics(
                    new CharacterTestRuntimeAiLabTimelineViewport(0, 299, 1, width, false),
                    width);
            RuntimeFrameTimelineVisualMetrics dense =
                CharacterTestRuntimeAiLabTimelineGeometry.ResolveVisualMetrics(
                    new CharacterTestRuntimeAiLabTimelineViewport(0, 599, 2, width, false),
                    width);

            Assert.AreEqual(
                RuntimeFrameTimelineVisualDensity.Detailed,
                detailed.Density);
            Assert.AreEqual(
                RuntimeFrameTimelineVisualDensity.Compact,
                compact.Density);
            Assert.AreEqual(
                RuntimeFrameTimelineVisualDensity.Dense,
                dense.Density);
            Assert.AreEqual(detailed.MarkerSize, compact.MarkerSize);
            Assert.AreEqual(compact.MarkerSize, dense.MarkerSize);
            Assert.AreEqual(detailed.MarkerGlyphSize, compact.MarkerGlyphSize);
            Assert.AreEqual(compact.MarkerGlyphSize, dense.MarkerGlyphSize);
            Assert.AreEqual(detailed.SpanHeight, compact.SpanHeight);
            Assert.AreEqual(compact.SpanHeight, dense.SpanHeight);
            Assert.AreEqual(detailed.LabelFontSize, compact.LabelFontSize);
            Assert.AreEqual(compact.LabelFontSize, dense.LabelFontSize);
            Assert.AreEqual(48f, detailed.SpanHeight);
            Assert.AreEqual(18, detailed.LabelFontSize);
            Assert.IsTrue(detailed.ShowsLabels);
            Assert.IsTrue(compact.ShowsLabels);
            Assert.IsFalse(dense.ShowsLabels);
        }

        [Test]
        public void RuntimeAiLabTimeline_CausalSummaryUsesReadableScale()
        {
            Assert.AreEqual(
                464f,
                CharacterTestRuntimeAiLabTimelineGeometry.CausalCardWidth);
            Assert.AreEqual(
                88f,
                CharacterTestRuntimeAiLabTimelineGeometry.CausalCardHeight);
            Assert.AreEqual(
                52f,
                CharacterTestRuntimeAiLabTimelineGeometry.CausalIconSize);
            Assert.AreEqual(
                18,
                CharacterTestRuntimeAiLabTimelineGeometry.CausalTitleFontSize);
            Assert.AreEqual(
                16,
                CharacterTestRuntimeAiLabTimelineGeometry.CausalValueFontSize);
        }

        [TestCase(900, 1, 2)]
        [TestCase(900, -1, 4)]
        [TestCase(90, -1, 0)]
        [TestCase(1800, 1, 3)]
        public void RuntimeAiLabTimeline_ZoomStepUsesAdjacentVisibleSpan(
            int currentSpan,
            int direction,
            int expectedZoomLevel)
        {
            Assert.IsTrue(
                CharacterTestRuntimeAiPlannerDevSceneController.TryResolveTimelineZoomLevel(
                    currentSpan,
                    direction,
                    out int zoomLevel));
            Assert.AreEqual(expectedZoomLevel, zoomLevel);
        }

        [TestCase(90, 1)]
        [TestCase(1800, -1)]
        [TestCase(600, 0)]
        public void RuntimeAiLabTimeline_ZoomStepStopsAtVisibleBounds(
            int currentSpan,
            int direction)
        {
            Assert.IsFalse(
                CharacterTestRuntimeAiPlannerDevSceneController.TryResolveTimelineZoomLevel(
                    currentSpan,
                    direction,
                    out int zoomLevel));
            Assert.AreEqual(-1, zoomLevel);
        }

        [Test]
        public void RuntimeAiLabTimeline_AdjacentSpanGeometryDoesNotOverlap()
        {
            const float width = 1732f;
            var viewport = new CharacterTestRuntimeAiLabTimelineViewport(
                0,
                599,
                2,
                width,
                false);

            CharacterTestRuntimeAiLabTimelineGeometry.ResolveSpan(
                100,
                119,
                viewport,
                width,
                out float firstX,
                out float firstWidth);
            CharacterTestRuntimeAiLabTimelineGeometry.ResolveSpan(
                120,
                139,
                viewport,
                width,
                out float secondX,
                out float secondWidth);

            Assert.AreEqual(firstX + firstWidth, secondX, 0.001f);
            Assert.Greater(firstWidth, 0f);
            Assert.Greater(secondWidth, 0f);
            Assert.Less(firstWidth, 128f,
                "A short interval must not be expanded to the old fixed 128 px text-chip width.");
        }

        [Test]
        public void RuntimeAiLabTimeline_RightOverdragMapsToFutureSimulationFrame()
        {
            var viewport = new CharacterTestRuntimeAiLabTimelineViewport(
                startFrame: 10,
                endFrame: 20,
                zoomLevel: 0,
                pixelWidth: 100f,
                followLatest: true);
            var domain = new CharacterTestRuntimeAiLabTimelineDomain(
                sessionStartFrame: 1,
                retainedStartFrame: 5,
                latestFrame: 20,
                selectedFrame: 20,
                frameLimit: 100,
                retainedRecordCount: 16);

            Assert.AreEqual(
                25,
                CharacterTestRuntimeAiLabTimelineViewportInputBinder.ResolveScrubFrame(
                    viewport,
                    domain,
                    localX: 318f,
                    rulerWidth: 100f));
            Assert.AreEqual(
                domain.SessionEndFrame,
                CharacterTestRuntimeAiLabTimelineViewportInputBinder.ResolveScrubFrame(
                    viewport,
                    domain,
                    localX: 10000f,
                    rulerWidth: 100f));
            Assert.AreEqual(
                domain.RetainedStartFrame,
                CharacterTestRuntimeAiLabTimelineViewportInputBinder.ResolveScrubFrame(
                    viewport,
                    domain,
                    localX: -1000f,
                    rulerWidth: 100f));
        }

        [Test]
        public void RuntimeAiLabTimeline_ActivePointerKeepsScrubHitSurfaceEnabledAcrossRefresh()
        {
            Assert.IsTrue(
                CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineBinder
                    .ShouldKeepScrubEnabled(false, false, true),
                "A ViewModel refresh during pointer-down must not disable the hit surface before scrub-end.");
            Assert.IsFalse(
                CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineBinder
                    .ShouldKeepScrubEnabled(false, false, false));
        }

        [Test]
        public void RuntimeAiLabTimeline_ActivePointerKeepsPreviewVisibleAcrossLayoutRefresh()
        {
            Assert.IsTrue(
                CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineBinder
                    .ShouldKeepPreview(false, true),
                "A synchronous ViewModel refresh must not reset the orange preview playhead to the ruler origin.");
            Assert.IsFalse(
                CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineBinder
                    .ShouldKeepPreview(false, false));
        }

        [Test]
        public void RuntimeAiLabTimeline_EventInspectorPrioritizesSemanticSummaryOverRawIdentity()
        {
            const string sourceTraceId = "a45dbdaa-5123-459f-8909-4054880aec51";
            var item = new CharacterTestRuntimeAiLabTimelineItem(
                stableKey: "perception:1:188:95:2:0",
                trackKind: CharacterTestRuntimeAiLabTimelineTrackKind.Perception,
                itemKind: CharacterTestRuntimeAiLabTimelineItemKind.Span,
                provenance: CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                startFrame: 95,
                endFrame: 169,
                isOpen: false,
                agentEntityId: 1,
                relatedEntityId: 2,
                category: "Perception.Target",
                severity: CharacterTestRuntimeAiLabTimelineSeverity.Info,
                label: "最近敌人",
                tooltip: "目标 2 · 距离 4.2 → 3.8m · 可见 · 视线畅通",
                count: 1,
                sourceTraceId: sourceTraceId,
                sourceOrder: 0,
                sessionGeneration: 1,
                recordGeneration: 188,
                authorityHash: 0x70681D1E334A89F5L,
                hasPerceptionSnapshot: true,
                perceptionStartDistance: 4.2f,
                perceptionEndDistance: 3.8f,
                perceptionTargetExists: true,
                perceptionTargetVisible: true,
                perceptionLineOfSightBlocked: false,
                perceptionInAttackRange: true);
            CharacterTestRuntimeAiLabTimelineDetailViewModel detail =
                CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, null);

            Assert.IsNotNull(detail);
            Assert.AreEqual("最近敌人", detail.Title);
            Assert.AreEqual("感知 · 区间", detail.MetaText);
            Assert.AreEqual("观测区间  95  →  169    ·    共 75 帧", detail.FrameText);
            Assert.AreEqual("目标  目标 2", detail.DetailLine1);
            Assert.AreEqual("距离  4.2 → 3.8 m", detail.DetailLine2);
            Assert.AreEqual("状态  可见 · 视线畅通 · 攻击范围内", detail.DetailLine3);
            Assert.AreEqual("涉及对象  智能体 1  →  目标 2", detail.ParticipantsText);
            foreach (string text in detail.EnumerateVisibleText())
            {
                Assert.IsFalse(
                    CharacterTestRuntimeAiLabTimelineDetailProjector.ContainsOpaqueIdentifier(text),
                    text);
                StringAssert.DoesNotContain(sourceTraceId, text);
                StringAssert.DoesNotContain("70681D1E334A89F5", text);
                StringAssert.DoesNotContain("记录 188", text);
            }
            Assert.AreEqual(
                668f,
                CharacterTestRuntimeAiLabTimelineGeometry.ResolveDetailHeight(item));
            Assert.AreEqual(
                668f,
                CharacterTestRuntimeAiLabTimelineGeometry.ResolveDetailHeight(null));
            var exactStack = new CharacterTestRuntimeAiLabTimelineItem(
                stableKey: "cluster:10",
                trackKind: CharacterTestRuntimeAiLabTimelineTrackKind.Decision,
                itemKind: CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack,
                provenance: CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                startFrame: 10,
                endFrame: 10,
                isOpen: false,
                agentEntityId: 1,
                relatedEntityId: 0,
                category: "Decision",
                severity: CharacterTestRuntimeAiLabTimelineSeverity.Info,
                label: "事件聚合",
                tooltip: string.Empty,
                count: 4,
                sourceTraceId: string.Empty,
                sourceOrder: 0);
            Assert.AreEqual(
                668f,
                CharacterTestRuntimeAiLabTimelineGeometry.ResolveDetailHeight(exactStack));
            Assert.AreEqual(
                5,
                CharacterTestFairyGuiRuntimeAiLabTimelineDetailBinder.VisibleMemberCount);
            var densityAggregate = new CharacterTestRuntimeAiLabTimelineItem(
                stableKey: "density:10:20",
                trackKind: CharacterTestRuntimeAiLabTimelineTrackKind.Decision,
                itemKind: CharacterTestRuntimeAiLabTimelineItemKind.ZoomDensityAggregate,
                provenance: CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                startFrame: 10,
                endFrame: 20,
                isOpen: false,
                agentEntityId: 1,
                relatedEntityId: 0,
                category: "Decision",
                severity: CharacterTestRuntimeAiLabTimelineSeverity.Info,
                label: "事件密度",
                tooltip: string.Empty,
                count: 8,
                sourceTraceId: string.Empty,
                sourceOrder: 0);
            Assert.AreEqual(
                668f,
                CharacterTestRuntimeAiLabTimelineGeometry.ResolveDetailHeight(
                    densityAggregate));
        }

        [Test]
        public void RuntimeAiLabTimeline_EventInspectorDistinguishesUnobservedFromCompleted()
        {
            const string behaviorId = "e7cba7bf-c8f7-4b4a-b008-9089765c743d";
            const string stageId = "019fb4ce-841b-7254-bdd7-149c0372fd57";
            var item = new CharacterTestRuntimeAiLabTimelineItem(
                stableKey: "runtime-effect:1:4:3:0",
                trackKind: CharacterTestRuntimeAiLabTimelineTrackKind.Result,
                itemKind: CharacterTestRuntimeAiLabTimelineItemKind.Span,
                provenance: CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                startFrame: 3,
                endFrame: 94,
                isOpen: false,
                agentEntityId: 1,
                relatedEntityId: 0,
                category: "RuntimeEffect",
                severity: CharacterTestRuntimeAiLabTimelineSeverity.Warning,
                label: "已入队 · 尚未执行",
                tooltip: "控制意图：已入队 · 动作状态：尚未观测到开始、完成或失败",
                count: 1,
                sourceTraceId: "planner.action.930020",
                sourceOrder: 0,
                behaviorEntryId: behaviorId,
                behaviorStepId: stageId,
                sessionGeneration: 1,
                recordGeneration: 4,
                authorityHash: unchecked((long)0xB3EB1234567B8DUL),
                commandEnqueueKind: CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued,
                runtimeEffectKind:
                    CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved,
                fallbackActionKind: CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack);
            CharacterTestRuntimeAiLabTimelineDetailViewModel detail =
                CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, null);

            Assert.IsNotNull(detail);
            Assert.AreEqual("结果 · 区间 · 需确认", detail.MetaText);
            Assert.AreEqual("观测区间  3  →  94    ·    共 92 帧", detail.FrameText);
            Assert.AreEqual("行为  基础攻击", detail.DetailLine1);
            Assert.AreEqual("阶段  基础攻击", detail.DetailLine2);
            Assert.AreEqual("结果  已入队 · 尚未执行", detail.DetailLine3);
            foreach (string text in detail.EnumerateVisibleText())
            {
                Assert.IsFalse(
                    CharacterTestRuntimeAiLabTimelineDetailProjector.ContainsOpaqueIdentifier(text),
                    text);
                StringAssert.DoesNotContain(behaviorId, text);
                StringAssert.DoesNotContain(stageId, text);
                StringAssert.DoesNotContain("B3EB1234567B8D", text);
                StringAssert.DoesNotContain("planner.action.930020", text);
            }
        }

        [Test]
        public void RuntimeAiLabTimeline_EventInspectorResolvesDraftBehaviorAndStageNames()
        {
            var host = new GameObject("Runtime AI Lab Semantic Detail Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                CharacterTestRuntimeAiLabBehaviorGroupDraft group =
                    controller.DraftWorkspace.Groups.Values.First(
                        candidate => candidate.Entries.Count > 0);
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[0];
                Assert.IsTrue(
                    controller.DraftWorkspace.Cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card));
                Assert.Greater(card.LogicPayload.Stages.Count, 0);
                CharacterTestRuntimeAiLabLogicStage stage = card.LogicPayload.Stages[0];
                card.DisplayName = "持续接敌";
                stage.Title = "接近目标";
                var item = new CharacterTestRuntimeAiLabTimelineItem(
                    stableKey: "semantic-resolution",
                    trackKind: CharacterTestRuntimeAiLabTimelineTrackKind.Decision,
                    itemKind: CharacterTestRuntimeAiLabTimelineItemKind.Span,
                    provenance:
                        CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                    startFrame: 10,
                    endFrame: 14,
                    isOpen: false,
                    agentEntityId: 1,
                    relatedEntityId: 2,
                    category: "Decision",
                    severity: CharacterTestRuntimeAiLabTimelineSeverity.Success,
                    label: "行为已提交",
                    tooltip: string.Empty,
                    count: 1,
                    sourceTraceId: Guid.NewGuid().ToString(),
                    sourceOrder: 0,
                    behaviorEntryId: entry.RuntimeEntry.EntryId,
                    behaviorStepId: stage.StageId,
                    plannerLogicKind:
                        CharacterTestRuntimeAiLabPlannerLogicCommitKind.Committed,
                    commandEnqueueKind:
                        CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued);

                CharacterTestRuntimeAiLabTimelineDetailViewModel detail =
                    CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, controller);

                Assert.AreEqual("行为  持续接敌", detail.DetailLine1);
                Assert.AreEqual("阶段  接近目标", detail.DetailLine2);
                Assert.AreEqual("决策  已入队 · 尚未执行", detail.DetailLine3);
                foreach (string text in detail.EnumerateVisibleText())
                {
                    Assert.IsFalse(
                        CharacterTestRuntimeAiLabTimelineDetailProjector
                            .ContainsOpaqueIdentifier(text),
                        text);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabTimeline_EventInspectorActionUsesTypedExecutionState()
        {
            var item = new CharacterTestRuntimeAiLabTimelineItem(
                stableKey: "action:running",
                trackKind: CharacterTestRuntimeAiLabTimelineTrackKind.Action,
                itemKind: CharacterTestRuntimeAiLabTimelineItemKind.Span,
                provenance: CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                startFrame: 42,
                endFrame: 48,
                isOpen: true,
                agentEntityId: 1,
                relatedEntityId: 2,
                category: "Action",
                severity: CharacterTestRuntimeAiLabTimelineSeverity.Info,
                label: "正在接近目标",
                tooltip: "internal.action.state.running",
                count: 1,
                sourceTraceId: Guid.NewGuid().ToString(),
                sourceOrder: 0,
                behaviorEntryId: Guid.NewGuid().ToString(),
                behaviorStepId: Guid.NewGuid().ToString(),
                runtimeEffectKind:
                    CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Running,
                fallbackActionKind: CharacterTestRuntimeAiPlannerActionKind.MoveToTarget);

            CharacterTestRuntimeAiLabTimelineDetailViewModel detail =
                CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, null);

            Assert.AreEqual("行为  接近目标", detail.DetailLine1);
            Assert.AreEqual("动作  接近目标", detail.DetailLine2);
            Assert.AreEqual("执行  动作执行中", detail.DetailLine3);
            foreach (string text in detail.EnumerateVisibleText())
            {
                Assert.IsFalse(
                    CharacterTestRuntimeAiLabTimelineDetailProjector.ContainsOpaqueIdentifier(text),
                    text);
            }
        }

        [Test]
        public void RuntimeAiLabTimeline_EventInspectorUnknownReasonUsesSafeLocalizedFallback()
        {
            const string unknownReason = "ERR_0xDEADBEEF";
            var item = new CharacterTestRuntimeAiLabTimelineItem(
                stableKey: "warning:unknown",
                trackKind: CharacterTestRuntimeAiLabTimelineTrackKind.Warnings,
                itemKind: CharacterTestRuntimeAiLabTimelineItemKind.Marker,
                provenance: CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                startFrame: 28,
                endFrame: 28,
                isOpen: false,
                agentEntityId: 1,
                relatedEntityId: 0,
                category: "Warning",
                severity: CharacterTestRuntimeAiLabTimelineSeverity.Warning,
                label: "执行未完成",
                tooltip: unknownReason,
                count: 1,
                sourceTraceId: Guid.NewGuid().ToString(),
                sourceOrder: 0,
                reasonCode: unknownReason);

            CharacterTestRuntimeAiLabTimelineDetailViewModel detail =
                CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, null);

            Assert.AreEqual("建议  具体原因暂不可读", detail.DetailLine3);
            foreach (string text in detail.EnumerateVisibleText())
            {
                StringAssert.DoesNotContain(unknownReason, text);
                Assert.IsFalse(
                    CharacterTestRuntimeAiLabTimelineDetailProjector.ContainsOpaqueIdentifier(text),
                    text);
            }
        }

        private static CharacterTestRuntimeAiPlannerSnapshot CreateEmptyRuntimeAiPlannerSnapshot()
        {
            return new CharacterTestRuntimeAiPlannerSnapshot(
                available: true,
                profileId: "test",
                agents: Array.Empty<CharacterTestRuntimeAiPlannerAgentSnapshot>(),
                validationReport: string.Empty);
        }

        private static CharacterTestRuntimeAiPlannerAgentSnapshot CreateRecoveryFingerprintAgent(
            int selectedActionId,
            string selectedActionName)
        {
            return new CharacterTestRuntimeAiPlannerAgentSnapshot(
                entityId: 1,
                targetEntityId: 2,
                activeStatePath: "Combat/Engage",
                targetExists: true,
                targetInAttackRange: false,
                targetDistance: 4.5f,
                movePathAvailable: true,
                selectedUtilityScore: 0.75f,
                selectedActionCost: 0.25f,
                selectedActionId: selectedActionId,
                selectedActionName: selectedActionName,
                selectedActionKind: CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                lastCommand: "SetControlIntent",
                suppressedReason: string.Empty,
                rejectReason: string.Empty,
                transitionFromStateId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0006,
                transitionToStateId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0003,
                targetVisible: true,
                movementSpeedScale: 0.8f,
                hasScenePreviewGeometry: true,
                scenePreviewSourceCenterX: 1f,
                scenePreviewTargetCenterX: 5f,
                runtimeAiPlannerProfileSlot: 1,
                runtimeAiBehaviorLoadoutId: 3,
                runtimeAiBehaviorVariantId: 2,
                runtimeAiBehaviorCatalogHash: 9988L,
                runtimeAiBehaviorProfileSlot: 1,
                selectedAbilitySlot: 2,
                targetAttackActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("attack.basic"),
                targetAttackSequence: 4,
                targetAttackImpactFrames: 6);
        }

        private sealed class FrameSyncTornadoReplayResult
        {
            public FrameSyncTornadoReplayResult(
                int firstActiveRecordIndex,
                int firstDamageTickRecordIndex,
                IReadOnlyList<FrameSyncTornadoFrameRecord> records)
            {
                FirstActiveRecordIndex = firstActiveRecordIndex;
                FirstDamageTickRecordIndex = firstDamageTickRecordIndex;
                Records = records ?? Array.Empty<FrameSyncTornadoFrameRecord>();
            }

            public int FirstActiveRecordIndex { get; }
            public int FirstDamageTickRecordIndex { get; }
            public IReadOnlyList<FrameSyncTornadoFrameRecord> Records { get; }
        }

        private readonly struct FrameSyncTornadoFrameRecord
        {
            public FrameSyncTornadoFrameRecord(
                int frame,
                CombatProjectileRuntimeState projectileState,
                CharacterTestRuntimeAiLabFrameSyncSample sample,
                CharacterTestCharacterTransformComponent targetTransform,
                int tornadoDamageTickTargetCount,
                int interactionLastFrameCount,
                long interactionLastFrameHash)
            {
                Frame = frame;
                ProjectileState = projectileState;
                Sample = sample;
                TargetTransform = targetTransform;
                TornadoDamageTickTargetCount = tornadoDamageTickTargetCount;
                InteractionLastFrameCount = interactionLastFrameCount;
                InteractionLastFrameHash = interactionLastFrameHash;
            }

            public int Frame { get; }
            public CombatProjectileRuntimeState ProjectileState { get; }
            public CharacterTestRuntimeAiLabFrameSyncSample Sample { get; }
            public CharacterTestCharacterTransformComponent TargetTransform { get; }
            public int TornadoDamageTickTargetCount { get; }
            public int InteractionLastFrameCount { get; }
            public long InteractionLastFrameHash { get; }
        }
    }
}

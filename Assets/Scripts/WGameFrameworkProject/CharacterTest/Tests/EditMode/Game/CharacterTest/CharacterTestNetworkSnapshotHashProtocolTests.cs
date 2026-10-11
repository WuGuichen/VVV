using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestNetworkSnapshotHashProtocolTests
    {
        [Test]
        public void EmptyStatePresence_IsDistinctFromNullForEachOwnedCodec()
        {
            long nullArc = Hash(hash => CharacterTestNetworkArcMineSnapshotCodec.AddStateHash(hash, null));
            long emptyArc = Hash(hash => CharacterTestNetworkArcMineSnapshotCodec.AddStateHash(
                hash,
                new CharacterTestArcMineRuntimeState(
                    CharacterTestArcMineRuntimeState.CurrentSchemaVersion,
                    CharacterTestArcMineRuntimeState.DefaultModuleId,
                    1,
                    Array.Empty<CharacterTestArcMineSnapshot>())));
            long nullRune = Hash(hash => CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHash(hash, null));
            long emptyRune = Hash(hash => CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHash(
                hash,
                new CharacterTestRuneTurretRuntimeState(
                    CharacterTestRuneTurretRuntimeState.CurrentSchemaVersion,
                    CharacterTestRuneTurretRuntimeState.DefaultModuleId,
                    1,
                    Array.Empty<CharacterTestRuneTurretSnapshot>())));
            long nullArea = Hash(hash => CharacterTestNetworkAreaConfinementSnapshotCodec.AddStateHash(hash, null));
            long emptyArea = Hash(hash => CharacterTestNetworkAreaConfinementSnapshotCodec.AddStateHash(
                hash,
                new CharacterTestAreaConfinementRuntimeState(
                    CharacterTestAreaConfinementRuntimeState.CurrentSchemaVersion,
                    CharacterTestAreaConfinementRuntimeState.DefaultModuleId,
                    1,
                    Array.Empty<CharacterTestAreaConfinementPendingChannelSnapshot>(),
                    sourceRuntime: null)));

            Assert.AreNotEqual(nullArc, emptyArc);
            Assert.AreNotEqual(nullRune, emptyRune);
            Assert.AreNotEqual(nullArea, emptyArea);
        }

        [Test]
        public void ArcMineAndRuneTurretUseDistinctRootSchemasAndModuleIdsAffectHash()
        {
            CharacterTestArcMineRuntimeState arc = new CharacterTestArcMineRuntimeState(
                CharacterTestArcMineRuntimeState.CurrentSchemaVersion,
                CharacterTestArcMineRuntimeState.DefaultModuleId,
                1,
                Array.Empty<CharacterTestArcMineSnapshot>());
            CharacterTestRuneTurretRuntimeState rune = new CharacterTestRuneTurretRuntimeState(
                CharacterTestRuneTurretRuntimeState.CurrentSchemaVersion,
                CharacterTestRuneTurretRuntimeState.DefaultModuleId,
                1,
                Array.Empty<CharacterTestRuneTurretSnapshot>());

            long arcHash = Hash(hash => CharacterTestNetworkArcMineSnapshotCodec.AddStateHash(hash, arc));
            long runeHash = Hash(hash => CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHash(hash, rune));
            long changedArcHash = Hash(hash => CharacterTestNetworkArcMineSnapshotCodec.AddStateHash(
                hash,
                new CharacterTestArcMineRuntimeState(
                    arc.SchemaVersion,
                    arc.ModuleId + 1,
                    arc.NextRuntimeId,
                    Array.Empty<CharacterTestArcMineSnapshot>())));

            Assert.AreNotEqual(arcHash, runeHash);
            Assert.AreNotEqual(arcHash, changedArcHash);
        }

        [Test]
        public void OwnedAndNonOwnedFilteringRemainDistinctForArcMineAndRuneTurret()
        {
            CharacterTestArcMineSnapshot arcMine = ArcMine(1);
            CharacterTestArcMineRuntimeState arcState = new CharacterTestArcMineRuntimeState(
                CharacterTestArcMineRuntimeState.CurrentSchemaVersion,
                CharacterTestArcMineRuntimeState.DefaultModuleId,
                3,
                new[] { arcMine, ArcMine(2) });
            CharacterTestRuneTurretRuntimeState runeState = new CharacterTestRuneTurretRuntimeState(
                CharacterTestRuneTurretRuntimeState.CurrentSchemaVersion,
                CharacterTestRuneTurretRuntimeState.DefaultModuleId,
                3,
                new[] { RuneTurret(1), RuneTurret(2) });

            long arcOwned = Hash(hash => CharacterTestNetworkArcMineSnapshotCodec.AddStateHash(hash, arcState, 1));
            long arcNonOwned = Hash(hash => CharacterTestNetworkArcMineSnapshotCodec.AddStateHashExcludingCaster(hash, arcState, 1));
            long runeOwned = Hash(hash => CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHash(hash, runeState, 1));
            long runeNonOwned = Hash(hash => CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHashExcludingCaster(hash, runeState, 1));

            Assert.AreNotEqual(arcOwned, arcNonOwned);
            Assert.AreNotEqual(runeOwned, runeNonOwned);
        }

        private static long Hash(Action<RuntimeHashAccumulator> write)
        {
            var accumulator = new RuntimeHashAccumulator(
                RuntimeHashSchemaIds.NetworkSnapshot,
                RuntimeHashSchemaIds.Version1);
            write(accumulator);
            return accumulator.ToHash();
        }

        private static CharacterTestArcMineSnapshot ArcMine(int casterEntityId)
        {
            return new CharacterTestArcMineSnapshot(
                runtimeId: casterEntityId,
                casterEntityId: casterEntityId,
                sourceCombatEntityId: 100 + casterEntityId,
                actionInstanceId: 200 + casterEntityId,
                sourceOrder: casterEntityId,
                localFrame: 4,
                createdFrame: 10L,
                armedFrame: 11L,
                expireFrame: 60L,
                fuseStartedFrame: 12L,
                detonateFrame: 30L,
                triggerTargetEntityId: 0,
                centerXRaw: 1000L * casterEntityId,
                centerYRaw: 0L,
                centerZRaw: 2000L,
                forwardXRaw: 0L,
                forwardYRaw: 0L,
                forwardZRaw: 1000000L,
                traceId: 9000 + casterEntityId);
        }

        private static CharacterTestRuneTurretSnapshot RuneTurret(int casterEntityId)
        {
            return new CharacterTestRuneTurretSnapshot(
                runtimeId: casterEntityId,
                casterEntityId: casterEntityId,
                sourceCombatEntityId: 200 + casterEntityId,
                actionInstanceId: 300 + casterEntityId,
                sourceOrder: casterEntityId,
                localFrame: 4,
                createdFrame: 10L,
                activateFrame: 12L,
                expireFrame: 60L,
                nextThinkFrame: 20L,
                shotsFired: 1,
                centerXRaw: 1000L * casterEntityId,
                centerYRaw: 0L,
                centerZRaw: 2000L,
                forwardXRaw: 0L,
                forwardYRaw: 0L,
                forwardZRaw: 1000000L,
                traceId: 9100 + casterEntityId);
        }
    }
}

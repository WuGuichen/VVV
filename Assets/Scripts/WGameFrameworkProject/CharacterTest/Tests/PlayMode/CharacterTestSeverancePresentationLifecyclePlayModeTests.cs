using System;
using System.Collections;
using System.IO;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    public sealed class CharacterTestSeverancePresentationLifecyclePlayModeTests
    {
        private const int EntityId = 1405;
        private const string TailPartStateId = "partstate.000003.tail";

        [UnityTest]
        public IEnumerator ReleaseAndRespawn_UseCurrentSeveredStateWithoutLifecycleLeakOrReplay()
        {
            CharacterTestBodyPartStateComponent restored = CreateRestoredSeveredTailState();
            CharacterTestRuntimeCharacterView first = CreateView("first");
            GameObject firstInstance = first.Instance;

            CharacterTestSeverancePresentationSyncResult attach =
                first.SyncSeverancePresentation(restored);
            Assert.AreEqual(0, attach.ConsumedAuthorityEventCount);
            Assert.AreEqual(1, attach.CurrentStateProjectionCount);
            Assert.AreEqual(
                CharacterTestSeverancePresentationStatus.RuntimeSliceNoStableResource,
                attach.Status);

            first.Release();
            Assert.IsNull(first.Instance);
            Assert.IsFalse(first.TryGetSeverancePresentationSnapshot(CharacterTestAuthoredContentIds.D400_Id43, out _));
            yield return null;
            Assert.IsTrue(firstInstance == null,
                "PlayMode Release must complete Unity's deferred destruction without a leaked view.");

            CharacterTestRuntimeCharacterView respawned = CreateView("respawned");
            GameObject respawnedInstance = respawned.Instance;
            CharacterTestSeverancePresentationSyncResult respawn =
                respawned.SyncSeverancePresentation(restored);
            Assert.AreEqual(0, respawn.ConsumedAuthorityEventCount,
                "Respawn projects restored state and must not replay a one-shot event.");
            Assert.AreEqual(1, respawn.CurrentStateProjectionCount);
            Assert.IsTrue(respawned.TryGetSeverancePresentationSnapshot(
                CharacterTestAuthoredContentIds.D400_Id43,
                out CharacterTestSeverancePresentationSnapshot snapshot));
            Assert.AreEqual(0, snapshot.DetachedViewCount);
            Assert.AreEqual(0, snapshot.SpawnedVfxCount);
            Assert.AreEqual(0, snapshot.PlayedSfxCount);

            respawned.Release();
            yield return null;
            Assert.IsTrue(respawnedInstance == null);
            Assert.IsNull(GameObject.Find("lcc-m3-t5-view-first"));
            Assert.IsNull(GameObject.Find("lcc-m3-t5-view-respawned"));
        }

        private static CharacterTestRuntimeCharacterView CreateView(string suffix)
        {
            var instance = new GameObject("lcc-m3-t5-view-" + suffix);
            instance.AddComponent<MeshRenderer>();
            return new CharacterTestRuntimeCharacterView(
                new CharacterTestCharacterEntityId(EntityId),
                instance,
                characterHandle: null,
                resourceManager: null,
                locomotionController: null);
        }

        private static CharacterTestBodyPartStateComponent CreateRestoredSeveredTailState()
        {
            CharacterImportedGeometryBinding geometry = LoadDragon14Geometry();
            var state = new CharacterTestBodyPartStateComponent(EntityId);
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                state.Reinitialize(geometry));
            Assert.IsTrue(state.TryResolveDefinitionIndexByPartStateId(
                CharacterTestAuthoredContentIds.D400_Id43,
                out int definitionIndex));
            CharacterBodyPartDamageRuntimeBinding definition =
                geometry.BodyPartDamageDefinitions[definitionIndex];

            Assert.IsTrue(state.TryBeginFrame(0, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageAcceptedDamage(new CharacterTestBodyPartAcceptedDamageFact(
                    authorityFrame: 0,
                    sourceEntityId: 1,
                    targetEntityId: EntityId,
                    actionInstanceId: 1,
                    policyEventId: 1,
                    traceId: 1,
                    localFrame: 1,
                    definitionIndex,
                    finalDamage: definition.MaxIntegrity)));
            Assert.IsTrue(state.CommitFrame().Succeeded);

            Assert.IsTrue(state.TryBeginFrame(1, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageSever(new CharacterTestBodyPartSeverFact(
                    authorityFrame: 1,
                    sourceEntityId: 1,
                    targetEntityId: EntityId,
                    definitionIndex,
                    definition.SeverRequiredTag,
                    correlationId: 1405001L)));
            Assert.IsTrue(state.CommitFrame().Succeeded);

            Assert.IsTrue(state.TryBeginFrame(2, out _));
            Assert.IsTrue(state.CommitFrame().Succeeded);
            Assert.AreEqual(0, state.TransitionCount,
                "The lifecycle test starts from restored/current state, not a live one-shot transition.");
            return state;
        }

        private static CharacterImportedGeometryBinding LoadDragon14Geometry()
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            for (int i = 0; i < registry.Definitions.Count; i++)
            {
                CharacterTestCharacterDefinition definition = registry.Definitions[i];
                if (!definition.GeneratedPackageRoot.Replace('\\', '/').EndsWith(
                        "/000003",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(
                    Path.GetFullPath(definition.GeneratedPackageRoot));
                Assert.NotNull(package?.Geometry);
                return package.Geometry;
            }

            Assert.Fail("Dragon14 was not discovered as a playable generated package.");
            return null;
        }
    }
}

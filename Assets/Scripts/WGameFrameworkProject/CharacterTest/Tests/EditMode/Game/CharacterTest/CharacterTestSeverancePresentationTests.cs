using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestSeverancePresentationTests
    {
        private const int EntityId = 7;
        private const int PartStateId = 1200110; // Synthetic tail part state.
        private const string SeverTag = "action.test.tail.sever";
        private const string PresentationEventId = "event.test.tail.severed";

        [Test]
        public void TypedSeveredAuthorityEvent_IsConsumedExactlyOnceWithMissingResourceFallback()
        {
            CharacterTestBodyPartStateComponent state = CreateSeveredState(clearTransitions: false);
            CharacterTestRuntimeCharacterView view = CreateView("live-event");
            try
            {
                CharacterTestSeverancePresentationSyncResult first =
                    view.SyncSeverancePresentation(state);
                Assert.AreEqual(
                    CharacterTestSeverancePresentationStatus.RuntimeSliceNoStableResource,
                    first.Status);
                Assert.AreEqual(1, first.ConsumedAuthorityEventCount);
                Assert.AreEqual(0, first.DuplicateAuthorityEventCount);
                Assert.AreEqual(0, first.CurrentStateProjectionCount,
                    "The live transition is consumed before current-state projection.");

                CharacterTestSeverancePresentationSyncResult duplicate =
                    view.SyncSeverancePresentation(state);
                Assert.AreEqual(0, duplicate.ConsumedAuthorityEventCount);
                Assert.AreEqual(1, duplicate.DuplicateAuthorityEventCount);
                Assert.IsTrue(view.TryGetSeverancePresentationSnapshot(
                    PartStateId,
                    out CharacterTestSeverancePresentationSnapshot snapshot));
                Assert.AreEqual(EntityId, snapshot.EntityId);
                Assert.AreEqual(PartStateId, snapshot.PartStateId);
                Assert.IsTrue(snapshot.AuthoritySevered);
                Assert.AreEqual(2L, snapshot.AuthorityRevision);
                Assert.AreEqual(PresentationEventId, snapshot.PresentationEventId);
                Assert.AreEqual(1, snapshot.ConsumedAuthorityEventCount);
                Assert.AreEqual(1, snapshot.DuplicateAuthorityEventCount);
                Assert.AreEqual(0, snapshot.SpawnedVfxCount);
                Assert.AreEqual(0, snapshot.PlayedSfxCount);
                Assert.AreEqual(0, snapshot.DetachedViewCount);
                Assert.AreEqual(1, view.CreateAnimationDiagnostics().EnabledRendererCount,
                    "The whole character renderer remains visible in Runtime Slice fallback.");
            }
            finally
            {
                view.Release();
            }
        }

        [Test]
        public void LateAttachAndRestore_ProjectCurrentSeveredStateWithoutReplayingOneShot()
        {
            CharacterTestBodyPartStateComponent restored = CreateSeveredState(clearTransitions: true);
            CharacterTestRuntimeCharacterView view = CreateView("late-attach");
            try
            {
                CharacterTestSeverancePresentationSyncResult attach =
                    view.SyncSeverancePresentation(restored);
                Assert.AreEqual(
                    CharacterTestSeverancePresentationStatus.RuntimeSliceNoStableResource,
                    attach.Status);
                Assert.AreEqual(0, attach.ConsumedAuthorityEventCount);
                Assert.AreEqual(0, attach.DuplicateAuthorityEventCount);
                Assert.AreEqual(1, attach.CurrentStateProjectionCount);

                CharacterTestSeverancePresentationSyncResult restore =
                    view.SyncSeverancePresentation(restored);
                Assert.AreEqual(0, restore.ConsumedAuthorityEventCount);
                Assert.AreEqual(0, restore.CurrentStateProjectionCount);
                Assert.IsTrue(view.TryGetSeverancePresentationSnapshot(
                    PartStateId,
                    out CharacterTestSeverancePresentationSnapshot snapshot));
                Assert.AreEqual(0, snapshot.ConsumedAuthorityEventCount);
                Assert.AreEqual(1, snapshot.CurrentStateProjectionCount);
                Assert.AreEqual(0, snapshot.SpawnedVfxCount);
                Assert.AreEqual(0, snapshot.PlayedSfxCount);
            }
            finally
            {
                view.Release();
            }
        }

        [Test]
        public void ReleaseAndRespawn_ResetPresentationStateWithoutLeakingDetachedObjects()
        {
            CharacterTestBodyPartStateComponent restored = CreateSeveredState(clearTransitions: true);
            CharacterTestRuntimeCharacterView released = CreateView("released");
            released.SyncSeverancePresentation(restored);
            GameObject releasedInstance = released.Instance;
            released.Release();

            Assert.IsNull(released.Instance);
            Assert.IsTrue(releasedInstance == null,
                "EditMode Release must destroy the previous runtime instance immediately.");
            Assert.IsFalse(released.TryGetSeverancePresentationSnapshot(PartStateId, out _));
            Assert.AreEqual(
                CharacterTestSeverancePresentationStatus.Released,
                released.SyncSeverancePresentation(restored).Status);

            CharacterTestRuntimeCharacterView respawned = CreateView("respawned");
            try
            {
                CharacterTestSeverancePresentationSyncResult result =
                    respawned.SyncSeverancePresentation(restored);
                Assert.AreEqual(0, result.ConsumedAuthorityEventCount);
                Assert.AreEqual(1, result.CurrentStateProjectionCount);
                Assert.IsTrue(respawned.TryGetSeverancePresentationSnapshot(
                    PartStateId,
                    out CharacterTestSeverancePresentationSnapshot snapshot));
                Assert.AreEqual(0, snapshot.DetachedViewCount);
                Assert.AreEqual(0, snapshot.SpawnedVfxCount);
                Assert.AreEqual(0, snapshot.PlayedSfxCount);
            }
            finally
            {
                respawned.Release();
            }
        }

        [Test]
        public void OverlayCard_SeparatesAuthorityTopologyActionAndPresentationFallback()
        {
            var presentation = new CharacterTestSeverancePresentationSnapshot(
                EntityId,
                PartStateId,
                authorityRevision: 2L,
                authoritySevered: true,
                PresentationEventId,
                CharacterTestSeverancePresentationStatus.RuntimeSliceNoStableResource,
                consumedAuthorityEventCount: 1,
                duplicateAuthorityEventCount: 1,
                currentStateProjectionCount: 0);
            var snapshot = new CharacterTestSeverancePresentationDebugSnapshot(
                EntityId,
                PartStateId,
                CharacterTestBodyPartStructureState.Severed,
                authorityRevision: 2L,
                CharacterTestBodyPartDependencyStatus.Severed,
                expectedMemberHurtboxCount: 3,
                activeMemberHurtboxCount: 0,
                topologyRevision: 12,
                hasViewProjection: true,
                presentation);

            CharacterTestDeveloperDebugCard card =
                CharacterTestSeverancePresentationDebug.CreateCard(snapshot);
            Assert.AreEqual(CharacterTestDeveloperDebugCardLayout.Entity, card.Layout);
            StringAssert.Contains("Severance Runtime Slice", card.Title);
            StringAssert.Contains("Severed", FindValue(card, "Authority"));
            StringAssert.Contains("0/3", FindValue(card, "Hurtbox Topology"));
            Assert.AreEqual("Severed", FindValue(card, "Action Dependency"));
            Assert.AreEqual(
                "RuntimeSliceNoStableResource",
                FindValue(card, "Presentation"));
            StringAssert.Contains("VFX=0 SFX=0 limbs=0", FindValue(card, "Emitted Presentation"));
            StringAssert.Contains("whole renderer unchanged", FindValue(card, "Safety Boundary"));
        }

        private static CharacterTestRuntimeCharacterView CreateView(string suffix)
        {
            var instance = new GameObject("severance-presentation-" + suffix);
            instance.AddComponent<MeshRenderer>();
            return new CharacterTestRuntimeCharacterView(
                new CharacterTestCharacterEntityId(EntityId),
                instance,
                characterHandle: null,
                resourceManager: null,
                locomotionController: null);
        }

        private static CharacterTestBodyPartStateComponent CreateSeveredState(bool clearTransitions)
        {
            var state = new CharacterTestBodyPartStateComponent(EntityId);
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                state.Reinitialize(CreateBinding()));

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
                    definitionIndex: 0,
                    finalDamage: 10)));
            Assert.IsTrue(state.CommitFrame().Succeeded);

            Assert.IsTrue(state.TryBeginFrame(1, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageSever(new CharacterTestBodyPartSeverFact(
                    authorityFrame: 1,
                    sourceEntityId: 1,
                    targetEntityId: EntityId,
                    definitionIndex: 0,
                    severTag: SeverTag,
                    correlationId: 1001L)));
            Assert.IsTrue(state.CommitFrame().Succeeded);

            if (clearTransitions)
            {
                Assert.IsTrue(state.TryBeginFrame(2, out _));
                Assert.IsTrue(state.CommitFrame().Succeeded);
                Assert.AreEqual(0, state.TransitionCount);
            }

            return state;
        }

        private static CharacterImportedGeometryBinding CreateBinding()
        {
            var definition = new CharacterBodyPartDamageRuntimeBinding(
                definitionIndex: 0,
                partStateId: PartStateId,
                memberPartIds: new[] { 1200111 },
                damageZoneId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id154,
                woundCapacity: 0,
                openDamageScaleMilli: 0,
                focusConsumeTag: string.Empty,
                maxIntegrity: 10,
                integrityDamageScaleMilli: 1_000,
                breakReactionId: "reaction.tail.break",
                authoredPriorityDeltaOpen: 0,
                authoredPriorityDeltaBroken: 10,
                severable: true,
                severRequiredTag: SeverTag,
                presentationEventId: PresentationEventId);
            return new CharacterImportedGeometryBinding(
                format: "mx.characterGeometryBinding.v5",
                schemaVersion: "5.0",
                packageId: "test.severance-presentation",
                bodyProfile: null,
                bodyColliders: Array.Empty<CharacterBodyColliderRuntimeBinding>(),
                hitZoneBindings: Array.Empty<CharacterHitZoneRuntimeBinding>(),
                damageZones: Array.Empty<CharacterDamageZoneRuntimeBinding>(),
                sockets: Array.Empty<CharacterSocketRuntimeBinding>(),
                weaponAttachments: Array.Empty<CharacterWeaponAttachmentRuntimeBinding>(),
                weaponTraces: Array.Empty<CharacterWeaponTraceRuntimeBinding>(),
                bodyPartDamageDefinitions: new[] { definition },
                bodyPartDamagePartLookup: new[]
                {
                    new CharacterBodyPartDamageLookupRuntimeBinding(401, 0, "tail")
                },
                bodyPartDamageFormat: "mx.characterBodyPartDamage.v1",
                bodyPartDamageSchemaVersion: "1.0",
                bodyPartDamageRevision: 2,
                bodyPartDamageContentHash: "sha256:test-severance-presentation",
                bodyPartDamageMode: CharacterBodyPartDamageMode.Authored);
        }

        private static string FindValue(CharacterTestDeveloperDebugCard card, string name)
        {
            for (int i = 0; i < card.Lines.Count; i++)
            {
                if (string.Equals(card.Lines[i].Name, name, StringComparison.Ordinal))
                    return card.Lines[i].Value;
            }

            Assert.Fail("Missing debug line: " + name);
            return string.Empty;
        }
    }
}

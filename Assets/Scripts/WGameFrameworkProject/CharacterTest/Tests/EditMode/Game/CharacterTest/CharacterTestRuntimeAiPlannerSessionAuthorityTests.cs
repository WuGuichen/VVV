using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    [TestFixture]
    public sealed class CharacterTestRuntimeAiPlannerSessionAuthorityTests
    {
        [Test]
        public void ValidateAndLock_PinsExactPublishedRevisionAndHashes()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                7,
                Publish("card.alpha", 1, 1),
                Publish("card.alpha", 2, 2));
            CharacterTestRuntimeAiPlannerSessionAuthorityResult result = Lock(
                catalog,
                CreateUnlocked("card.alpha"),
                new[] { "card.alpha" });

            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            Assert.AreEqual(7, result.SessionLock.CatalogRevision);
            Assert.AreEqual(catalog.CatalogHash, result.SessionLock.CatalogHash);
            Assert.AreEqual(2, result.SessionLock.Cards[0].Revision);
            Assert.AreEqual(catalog.Cards[1].ContentHash, result.SessionLock.Cards[0].ContentHash);
            Assert.AreNotEqual(0L, result.SessionLock.AuthorityHash);
        }

        [Test]
        public void ValidateAndLock_RejectsUnauthorizedCharacter()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                1,
                Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerSessionAuthorityResult result = Lock(
                catalog,
                CreateUnlocked("card.alpha"),
                new[] { "card.alpha" },
                characterStableId: "character.other");

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "SESSION_DECK_CHARACTER_NOT_OWNED");
        }

        [Test]
        public void ValidateAndLock_RejectsRequesterContextMismatch()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                1,
                Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerSessionDeckRequest request =
                new CharacterTestRuntimeAiPlannerSessionDeckRequest(
                    "peer.attacker",
                    "character.hero",
                    1,
                    catalog.CatalogId,
                     catalog.Revision,
                     catalog.CatalogNumericId,
                     catalog.NumericIdentityHash,
                     catalog.CatalogHash,
                    new[] { catalog.Cards[0].NumericId },
                    new[] { "card.alpha" });

            CharacterTestRuntimeAiPlannerSessionAuthorityResult result =
                CharacterTestRuntimeAiPlannerSessionAuthority.ValidateAndLock(
                    request,
                    CreateContext(catalog, CreateUnlocked("card.alpha")));

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "SESSION_DECK_REQUESTER_MISMATCH");
        }

        [Test]
        public void ValidateAndLock_RejectsLockedCard()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                1,
                Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerSessionAuthorityResult result = Lock(
                catalog,
                CreateUnlocked(),
                new[] { "card.alpha" });

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "SESSION_DECK_CARD_NOT_UNLOCKED");
        }

        [TestCase("id")]
        [TestCase("revision")]
        [TestCase("hash")]
        public void ValidateAndLock_RejectsCatalogMismatch(string mismatch)
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                3,
                Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerSessionDeckRequest request = CreateRequest(
                catalog,
                "character.hero",
                new[] { "card.alpha" },
                mismatch == "id" ? "wrong.catalog" : catalog.CatalogId,
                mismatch == "revision" ? catalog.Revision + 1 : catalog.Revision,
                mismatch == "hash" ? catalog.CatalogHash + 1 : catalog.CatalogHash);
            CharacterTestRuntimeAiPlannerSessionAuthorityResult result =
                CharacterTestRuntimeAiPlannerSessionAuthority.ValidateAndLock(
                    request,
                    CreateContext(catalog, CreateUnlocked("card.alpha")));

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "SESSION_DECK_CATALOG_" + mismatch.ToUpperInvariant() + "_MISMATCH");
        }

        [Test]
        public void ValidateAndLock_RejectsDuplicateOverSlotAndOverCostThroughDeckRules()
        {
            var cards = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard[7];
            var ids = new string[7];
            for (int i = 0; i < cards.Length; i++)
            {
                ids[i] = "card." + (char)('a' + i);
                cards[i] = Publish(ids[i], 1, 2);
            }
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(1, cards);
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked = CreateUnlocked(ids);

            CharacterTestRuntimeAiPlannerSessionAuthorityResult duplicate = Lock(
                catalog,
                unlocked,
                new[] { ids[0], ids[0] });
            CharacterTestRuntimeAiPlannerSessionAuthorityResult limits = Lock(catalog, unlocked, ids);

            Assert.IsFalse(duplicate.Success);
            AssertDiagnostic(duplicate.Diagnostics, "INSTALLED_DECK_CARD_DUPLICATE");
            Assert.IsFalse(limits.Success);
            AssertDiagnostic(limits.Diagnostics, "INSTALLED_DECK_SLOT_LIMIT_EXCEEDED");
            AssertDiagnostic(limits.Diagnostics, "INSTALLED_DECK_COST_LIMIT_EXCEEDED");
        }

        [Test]
        public void ValidateAndLock_PreservesFallbackOnlyWarningForLegalEmptyDeck()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                1,
                Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerSessionAuthorityResult result = Lock(
                catalog,
                CreateUnlocked(),
                Array.Empty<string>());

            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            Assert.AreEqual(0, result.SessionLock.Cards.Count);
            AssertDiagnostic(result.Diagnostics, "INSTALLED_DECK_FALLBACK_ONLY");
        }

        [Test]
        public void AuthorityHash_IsStableForEquivalentCardOrder()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                1,
                Publish("card.alpha", 1, 1),
                Publish("card.beta", 1, 2));
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked =
                CreateUnlocked("card.alpha", "card.beta");
            CharacterTestRuntimeAiPlannerSessionAuthorityResult first = Lock(
                catalog,
                unlocked,
                new[] { "card.beta", "card.alpha" });
            CharacterTestRuntimeAiPlannerSessionAuthorityResult second = Lock(
                catalog,
                unlocked,
                new[] { "card.alpha", "card.beta" });

            Assert.IsTrue(first.Success, Describe(first.Diagnostics));
            Assert.IsTrue(second.Success, Describe(second.Diagnostics));
            Assert.AreEqual(first.SessionLock.DeckHash, second.SessionLock.DeckHash);
            Assert.AreEqual(first.SessionLock.AuthorityHash, second.SessionLock.AuthorityHash);
        }

        [Test]
        public void Participant_SecondLockIsRejectedWithoutReplacingAuthorityState()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                1,
                Publish("card.alpha", 1, 1),
                Publish("card.beta", 1, 1));
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked =
                CreateUnlocked("card.alpha", "card.beta");
            var participant = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(catalog, 1);
            CharacterTestRuntimeAiPlannerSessionAuthorityContext context = CreateContext(catalog, unlocked);
            CharacterTestRuntimeAiPlannerSessionAuthorityResult first = participant.TryLock(
                CreateRequest(catalog, "character.hero", new[] { "card.alpha" }),
                context);
            long originalHash = participant.SessionLock.AuthorityHash;
            CharacterTestRuntimeAiPlannerSessionAuthorityResult second = participant.TryLock(
                CreateRequest(catalog, "character.hero", new[] { "card.beta" }),
                context);

            Assert.IsTrue(first.Success, Describe(first.Diagnostics));
            Assert.IsFalse(second.Success);
            AssertDiagnostic(second.Diagnostics, "SESSION_DECK_SELECTION_LOCKED");
            Assert.AreEqual(originalHash, participant.SessionLock.AuthorityHash);
            Assert.AreEqual("card.alpha", participant.SessionLock.Cards[0].StableCardId);
        }

        [Test]
        public void Participant_LocksDifferentOwnedCharactersAndNormalizesRosterHash()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                1,
                Publish("card.alpha", 1, 1),
                Publish("card.beta", 1, 1));
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked =
                CreateUnlocked("card.alpha", "card.beta");
            var context = new CharacterTestRuntimeAiPlannerSessionAuthorityContext(
                "peer.local",
                new[] { "character.hero", "character.companion" },
                unlocked,
                catalog,
                1);
            var firstOrder = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(catalog, 1);
            var secondOrder = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(catalog, 1);

            Assert.IsTrue(firstOrder.TryLock(
                CreateRequest(catalog, "character.hero", new[] { "card.alpha" }),
                context).Success);
            Assert.IsTrue(firstOrder.TryLock(
                CreateRequest(catalog, "character.companion", new[] { "card.beta" }),
                context).Success);
            Assert.IsTrue(secondOrder.TryLock(
                CreateRequest(catalog, "character.companion", new[] { "card.beta" }),
                context).Success);
            Assert.IsTrue(secondOrder.TryLock(
                CreateRequest(catalog, "character.hero", new[] { "card.alpha" }),
                context).Success);

            Assert.AreEqual(2, firstOrder.SessionLocks.Count);
            Assert.AreEqual("character.companion", firstOrder.SessionLocks[0].CharacterStableId);
            Assert.AreEqual("character.hero", firstOrder.SessionLocks[1].CharacterStableId);
            Assert.AreEqual(firstOrder.AuthorityHash, secondOrder.AuthorityHash);
            Assert.IsTrue(firstOrder.TryGetSessionLock(
                "character.hero",
                out CharacterTestRuntimeAiPlannerSessionDeckLock hero));
            Assert.AreEqual("card.alpha", hero.Cards[0].StableCardId);
        }

        [Test]
        public void Participant_MultiCharacterSnapshotRoundTripRestoresExactRoster()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                2,
                Publish("card.alpha", 1, 1),
                Publish("card.beta", 1, 1));
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked =
                CreateUnlocked("card.alpha", "card.beta");
            var context = new CharacterTestRuntimeAiPlannerSessionAuthorityContext(
                "peer.local",
                new[] { "character.hero", "character.companion" },
                unlocked,
                catalog,
                1);
            var source = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(catalog, 1);
            Assert.IsTrue(source.TryLock(
                CreateRequest(catalog, "character.hero", new[] { "card.alpha" }),
                context).Success);
            Assert.IsTrue(source.TryLock(
                CreateRequest(catalog, "character.companion", new[] { "card.beta" }),
                context).Success);
            var restored = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(catalog, 1);

            NetworkSnapshotApplyResult apply = restored.ApplySnapshot(
                new RuntimeFrame(12),
                source.CaptureSnapshot(new RuntimeFrame(12)));

            Assert.IsTrue(apply.Success, apply.Message);
            Assert.AreEqual(2, restored.SessionLocks.Count);
            Assert.AreEqual(source.AuthorityHash, restored.AuthorityHash);
        }

        [Test]
        public void Participant_SnapshotRejectsCharacterIdsThatCollideAfterNormalization()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                2,
                Publish("card.alpha", 1, 1),
                Publish("card.beta", 1, 1));
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked =
                CreateUnlocked("card.alpha", "card.beta");
            var context = new CharacterTestRuntimeAiPlannerSessionAuthorityContext(
                "peer.local",
                new[] { "character.hero", "character.hero2" },
                unlocked,
                catalog,
                1);
            var source = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(catalog, 1);
            Assert.IsTrue(source.TryLock(
                CreateRequest(catalog, "character.hero", new[] { "card.alpha" }),
                context).Success);
            Assert.IsTrue(source.TryLock(
                CreateRequest(catalog, "character.hero2", new[] { "card.beta" }),
                context).Success);
            RuntimeCustomState snapshot = source.CaptureSnapshot(RuntimeFrame.Zero);
            JObject document = JObject.Parse(snapshot.PayloadJson);
            ((JArray)document["locks"])[1]["characterStableId"] = "character.hero ";
            var malformed = new RuntimeCustomState(
                snapshot.NumericTypeId,
                snapshot.TypeId,
                snapshot.SchemaVersion,
                document.ToString(Newtonsoft.Json.Formatting.None));
            var restored = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(catalog, 1);

            NetworkSnapshotApplyResult apply = restored.ApplySnapshot(RuntimeFrame.Zero, malformed);

            Assert.IsFalse(apply.Success);
            StringAssert.Contains("uniquely normalized", apply.Message);
            Assert.AreEqual(0, restored.SessionLocks.Count);
        }

        [Test]
        public void Participant_SnapshotRoundTripRestoresPinnedIdentity()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                2,
                Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked = CreateUnlocked("card.alpha");
            var source = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(catalog, 1);
            Assert.IsTrue(source.TryLock(
                CreateRequest(catalog, "character.hero", new[] { "card.alpha" }),
                CreateContext(catalog, unlocked)).Success);
            RuntimeCustomState snapshot = source.CaptureSnapshot(new RuntimeFrame(12));
            var restored = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(catalog, 1);

            NetworkSnapshotApplyResult apply = restored.ApplySnapshot(new RuntimeFrame(12), snapshot);

            Assert.IsTrue(apply.Success, apply.Message);
            Assert.IsTrue(restored.IsLocked);
            Assert.AreEqual(source.SessionLock.AuthorityHash, restored.SessionLock.AuthorityHash);
            Assert.AreEqual(source.SessionLock.Cards[0].Revision, restored.SessionLock.Cards[0].Revision);
        }

        [Test]
        public void Participant_TamperedSnapshotIsRejectedAtomically()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                1,
                Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked = CreateUnlocked("card.alpha");
            var participant = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(catalog, 1);
            Assert.IsTrue(participant.TryLock(
                CreateRequest(catalog, "character.hero", new[] { "card.alpha" }),
                CreateContext(catalog, unlocked)).Success);
            long originalHash = participant.SessionLock.AuthorityHash;
            RuntimeCustomState snapshot = participant.CaptureSnapshot(new RuntimeFrame(5));
            JObject document = JObject.Parse(snapshot.PayloadJson);
            document["authorityHash"] = originalHash + 1;
            var tampered = new RuntimeCustomState(
                snapshot.NumericTypeId,
                snapshot.TypeId,
                snapshot.SchemaVersion,
                document.ToString(Newtonsoft.Json.Formatting.None));

            NetworkSnapshotApplyResult apply = participant.ApplySnapshot(new RuntimeFrame(5), tampered);

            Assert.IsFalse(apply.Success);
            Assert.AreEqual(originalHash, participant.SessionLock.AuthorityHash);
        }

        [Test]
        public void Participant_CatalogMismatchSnapshotIsRejected()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = Publish("card.alpha", 1, 1);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog firstCatalog = CreateCatalog(1, card);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog secondCatalog = CreateCatalog(2, card);
            var source = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(firstCatalog, 1);
            Assert.IsTrue(source.TryLock(
                CreateRequest(firstCatalog, "character.hero", new[] { "card.alpha" }),
                CreateContext(firstCatalog, CreateUnlocked("card.alpha"))).Success);
            var target = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(secondCatalog, 1);

            NetworkSnapshotApplyResult apply = target.ApplySnapshot(
                RuntimeFrame.Zero,
                source.CaptureSnapshot(RuntimeFrame.Zero));

            Assert.IsFalse(apply.Success);
            Assert.IsFalse(target.IsLocked);
        }

        [Test]
        public void Participant_EmptySnapshotCannotClearLockedSelection()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                1,
                Publish("card.alpha", 1, 1));
            var locked = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(catalog, 1);
            Assert.IsTrue(locked.TryLock(
                CreateRequest(catalog, "character.hero", new[] { "card.alpha" }),
                CreateContext(catalog, CreateUnlocked("card.alpha"))).Success);
            long originalHash = locked.SessionLock.AuthorityHash;
            var empty = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(catalog, 1);

            NetworkSnapshotApplyResult apply = locked.ApplySnapshot(
                RuntimeFrame.Zero,
                empty.CaptureSnapshot(RuntimeFrame.Zero));

            Assert.IsFalse(apply.Success);
            Assert.AreEqual(originalHash, locked.SessionLock.AuthorityHash);
        }

        [Test]
        public void Participant_RollbackRestoresImmutableLockedState()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                1,
                Publish("card.alpha", 1, 1));
            var participant = new CharacterTestRuntimeAiPlannerSessionAuthorityParticipant(catalog, 1);
            Assert.IsTrue(participant.TryLock(
                CreateRequest(catalog, "character.hero", new[] { "card.alpha" }),
                CreateContext(catalog, CreateUnlocked("card.alpha"))).Success);
            object rollback = participant.CaptureRollbackState(new RuntimeFrame(3));
            long authorityHash = participant.SessionLock.AuthorityHash;

            bool restored = participant.RestoreRollbackState(
                new RuntimeFrame(3),
                rollback,
                out string failure);

            Assert.IsTrue(restored, failure);
            Assert.AreEqual(authorityHash, participant.SessionLock.AuthorityHash);
        }

        [Test]
        public void WireDecode_UsesTransportSenderInsteadOfClientSuppliedRequester()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                1,
                Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerSessionDeckRequest source =
                new CharacterTestRuntimeAiPlannerSessionDeckRequest(
                    "peer.spoofed",
                    "character.hero",
                    1,
                    catalog.CatalogId,
                     catalog.Revision,
                     catalog.CatalogNumericId,
                     catalog.NumericIdentityHash,
                     catalog.CatalogHash,
                    new[] { catalog.Cards[0].NumericId },
                    new[] { "card.alpha" });
            NetworkApplicationControlRequestPacket packet =
                CharacterTestRuntimeAiPlannerSessionAuthorityWire.CreateRequestPacket(9L, source);

            bool decoded = CharacterTestRuntimeAiPlannerSessionAuthorityWire.TryDecodeRequest(
                "peer.authoritative-envelope",
                packet,
                out CharacterTestRuntimeAiPlannerSessionDeckRequest request,
                out string failure);

            Assert.IsTrue(decoded, failure);
            Assert.AreEqual("peer.authoritative-envelope", request.RequesterPeerId);
            StringAssert.DoesNotContain("peer.spoofed", packet.PayloadJson);
        }

        [Test]
        public void WireDecode_RejectsOversizedAndUnknownPayloadMembers()
        {
            var oversized = new NetworkApplicationControlRequestPacket(
                CharacterTestRuntimeAiPlannerSessionAuthorityWire.RequestTypeId,
                CharacterTestRuntimeAiPlannerSessionAuthorityWire.SchemaVersion,
                10L,
                new string('x', CharacterTestRuntimeAiPlannerSessionAuthorityWire.MaximumPayloadCharacters + 1));
            bool oversizedDecoded = CharacterTestRuntimeAiPlannerSessionAuthorityWire.TryDecodeRequest(
                "peer.authoritative",
                oversized,
                out _,
                out string oversizedFailure);

            var unknownMember = new NetworkApplicationControlRequestPacket(
                CharacterTestRuntimeAiPlannerSessionAuthorityWire.RequestTypeId,
                CharacterTestRuntimeAiPlannerSessionAuthorityWire.SchemaVersion,
                11L,
                "{\"characterStableId\":\"character.hero\","
                + "\"expectedCatalogId\":\"catalog\","
                + "\"expectedCatalogRevision\":1,"
                + "\"expectedCatalogHash\":1,"
                + "\"stableCardIds\":[],"
                + "\"unexpected\":true}");
            bool unknownDecoded = CharacterTestRuntimeAiPlannerSessionAuthorityWire.TryDecodeRequest(
                "peer.authoritative",
                unknownMember,
                out _,
                out string unknownFailure);

            Assert.IsFalse(oversizedDecoded);
            StringAssert.Contains("size limit", oversizedFailure);
            Assert.IsFalse(unknownDecoded);
            StringAssert.Contains("invalid", unknownFailure);
        }

        [Test]
        public void NetworkRunner_TypedRequestLocksServerAndReplicatesExactClientSnapshot()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                4,
                Publish("card.alpha", 1, 1),
                Publish("card.beta", 1, 1));
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked =
                CreateUnlocked("card.alpha", "card.beta");
            var transport = new CodecLoopbackNetworkTransport(new JsonNetworkMessageCodec());
            using (var runner = new CharacterTestNetworkSessionRunner(
                transport,
                transport,
                config: NetworkSessionConfig.Default,
                createServerViews: false,
                disposeTransports: false))
            {
                NetworkApplicationControlResponsePacket observed = null;
                var configuration = new CharacterTestRuntimeAiPlannerSessionAuthorityConfiguration(
                    catalog,
                    1,
                    peerId => new CharacterTestRuntimeAiPlannerSessionAuthorityContext(
                        peerId,
                        new[] { "character.hero", "character.companion" },
                        unlocked,
                        catalog,
                        1),
                    response => observed = response);
                CharacterTestRuntimeAiPlannerSessionAuthorityComposition.Configure(
                    runner,
                    configuration);
                CharacterTestRuntimeAiPlannerSessionAuthorityParticipant serverParticipant =
                    runner.ServerSessionAuthorityParticipant;
                CharacterTestRuntimeAiPlannerSessionAuthorityParticipant clientParticipant =
                    runner.ClientSessionAuthorityParticipant;
                ((ICharacterTestRuntimeAiPlannerSessionAuthorityClient)runner)
                    .SubmitRuntimeAiPlannerSessionDeckRequest(
                    42L,
                    new CharacterTestRuntimeAiPlannerSessionDeckRequest(
                        runner.ClientPeerId,
                        "character.hero",
                        1,
                        catalog.CatalogId,
                         catalog.Revision,
                         catalog.CatalogNumericId,
                         catalog.NumericIdentityHash,
                         catalog.CatalogHash,
                        new[] { catalog.Cards[0].NumericId },
                        new[] { "card.alpha" }));

                runner.Tick(1d / 60d);

                ((ICharacterTestRuntimeAiPlannerSessionAuthorityClient)runner)
                    .SubmitRuntimeAiPlannerSessionDeckRequest(
                    43L,
                    new CharacterTestRuntimeAiPlannerSessionDeckRequest(
                        runner.ClientPeerId,
                        "character.companion",
                        2,
                        catalog.CatalogId,
                         catalog.Revision,
                         catalog.CatalogNumericId,
                         catalog.NumericIdentityHash,
                         catalog.CatalogHash,
                        new[] { catalog.Cards[1].NumericId },
                        new[] { "card.beta" }));
                runner.Tick(1d / 60d);

                Assert.IsNotNull(observed);
                Assert.IsTrue(observed.Accepted, observed.Reason);
                Assert.AreEqual(43L, observed.RequestId);
                Assert.IsNotNull(runner.ServerSessionDeckLock);
                Assert.IsNotNull(runner.ClientSessionDeckLock);
                Assert.AreEqual(2, serverParticipant.SessionLocks.Count);
                Assert.AreEqual(2, clientParticipant.SessionLocks.Count);
                Assert.AreEqual(
                    serverParticipant.AuthorityHash,
                    clientParticipant.AuthorityHash);
                Assert.AreEqual(
                    runner.ServerSessionDeckLock.Cards[0].Revision,
                    runner.ClientSessionDeckLock.Cards[0].Revision);

                CharacterTestReplayPlaybackStatus recording = runner.BeginReplayRecording();
                Assert.AreEqual("RECORDING", recording.ResultCode);
                runner.Tick(1d / 60d);
                CharacterTestReplayCapture capture = runner.CreateReplayCapture();
                Assert.IsTrue(capture.HasSessionAuthorityBootstrap);
                Assert.AreEqual(serverParticipant.AuthorityHash, capture.SessionAuthorityHash);
                Assert.AreEqual(
                    serverParticipant.CaptureSnapshot(runner.ServerSlice.CurrentFrame).PayloadJson,
                    capture.SessionAuthorityBootstrapSnapshot.PayloadJson);
                RuntimeReplayPlaybackResult playback = CharacterTestReplayPlayback.Play(capture);
                Assert.IsTrue(playback.Success, playback.FailureMessage);

                JObject tamperedDocument = JObject.Parse(
                    capture.SessionAuthorityBootstrapSnapshot.PayloadJson);
                ((JArray)tamperedDocument["locks"])[0]["catalogHash"] = catalog.CatalogHash + 1L;
                var tamperedBootstrap = new RuntimeCustomState(
                    capture.SessionAuthorityBootstrapSnapshot.NumericTypeId,
                    capture.SessionAuthorityBootstrapSnapshot.TypeId,
                    capture.SessionAuthorityBootstrapSnapshot.SchemaVersion,
                    tamperedDocument.ToString(Newtonsoft.Json.Formatting.None));
                var tamperedCapture = new CharacterTestReplayCapture(
                    capture.RuntimeReplaySnapshot,
                    capture.BootstrapSnapshot,
                    capture.TickRate,
                    capture.StateSyncPolicy,
                    capture.AuthoritativeCommands,
                    capture.ReplaySliceFactory,
                    tamperedBootstrap,
                    capture.SessionAuthorityHash,
                    capture.SessionAuthorityParticipantFactory);
                RuntimeReplayPlaybackResult tamperedPlayback =
                    CharacterTestReplayPlayback.Play(tamperedCapture);
                Assert.IsFalse(tamperedPlayback.Success);
                Assert.AreEqual(
                    RuntimeReplayPlaybackFailureCode.DriverException,
                    tamperedPlayback.FailureCode);
                StringAssert.Contains(
                    "session authority bootstrap",
                    tamperedPlayback.FailureMessage);
            }
            transport.Clear();
        }

        [Test]
        public void DirectClientRollbackCoordinatorRestoresRegisteredAuthorityParticipant()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                3,
                Publish("card.alpha", 1, 1),
                Publish("card.beta", 1, 1));
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked =
                CreateUnlocked("card.alpha", "card.beta");
            var transport = new LoopbackNetworkTransport(
                LoopbackNetworkTransportOptions.ReliableImmediate);
            var configuration = new CharacterTestRuntimeAiPlannerSessionAuthorityConfiguration(
                catalog,
                1,
                peerId => new CharacterTestRuntimeAiPlannerSessionAuthorityContext(
                    peerId,
                    new[] { "character.hero", "character.companion" },
                    unlocked,
                    catalog,
                    1));
            using (var client = new CharacterTestUnityTransportClientRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: NetworkSessionConfig.Default,
                sessionId: "character-test-session-authority-rollback",
                serverPeerId: "server",
                clientPeerId: "client-authority",
                createClientViews: false,
                disposeTransport: false))
            {
                CharacterTestRuntimeAiPlannerSessionAuthorityComposition.Configure(client, configuration);
                CharacterTestRuntimeAiPlannerSessionAuthorityParticipant participant =
                    client.SessionAuthorityParticipant;
                CharacterTestRuntimeAiPlannerSessionAuthorityContext context =
                    configuration.ServerContextProvider("client-authority");
                Assert.IsTrue(participant.TryLock(
                    CreateRequest(
                        catalog,
                        "character.hero",
                        new[] { "card.alpha" },
                        requesterPeerId: "client-authority"),
                    context).Success);

                FieldInfo coordinatorField = typeof(CharacterTestUnityTransportClientRunner).GetField(
                    "_rollbackCoordinator",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(coordinatorField);
                var coordinator = (NetworkPredictionRollbackCoordinator)coordinatorField.GetValue(client);
                coordinator.RecordFrame(RuntimeFrame.Zero);
                Assert.IsTrue(participant.TryLock(
                    CreateRequest(
                        catalog,
                        "character.companion",
                        new[] { "card.beta" },
                        requesterPeerId: "client-authority"),
                    context).Success);
                Assert.AreEqual(2, participant.SessionLocks.Count);

                NetworkPredictionRollbackResult rollback = coordinator.ReconcileFromFrame(
                    RuntimeFrame.Zero,
                    new RuntimeFrame(1),
                    AcceptRollbackCommand,
                    ReplayRollbackFrame);

                Assert.IsTrue(rollback.Success, rollback.Message);
                Assert.IsTrue(rollback.RolledBack);
                Assert.AreEqual(1, participant.SessionLocks.Count);
                Assert.IsTrue(participant.TryGetSessionLock("character.hero", out _));
                Assert.IsFalse(participant.TryGetSessionLock("character.companion", out _));
            }
            transport.Clear();
        }

        [Test]
        public void DirectEndpointRunners_ComposedAuthorityReplicatesAcceptedSelection()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                5,
                Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked = CreateUnlocked("card.alpha");
            var transport = new LoopbackNetworkTransport(
                LoopbackNetworkTransportOptions.ReliableImmediate);
            NetworkApplicationControlResponsePacket observed = null;
            var configuration = new CharacterTestRuntimeAiPlannerSessionAuthorityConfiguration(
                catalog,
                1,
                peerId => new CharacterTestRuntimeAiPlannerSessionAuthorityContext(
                    peerId,
                    new[] { "character.hero" },
                    unlocked,
                    catalog,
                    1),
                response => observed = response);
            using (var server = new CharacterTestUnityTransportServerRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: NetworkSessionConfig.Default,
                sessionId: "character-test-session-authority-direct",
                serverPeerId: "server",
                createServerViews: false,
                disposeTransport: false))
            using (var client = new CharacterTestUnityTransportClientRunner(
                transport,
                NullRuntimeLogger.Instance,
                config: NetworkSessionConfig.Default,
                sessionId: "character-test-session-authority-direct",
                serverPeerId: "server",
                clientPeerId: "client-authority",
                createClientViews: false,
                disposeTransport: false))
            {
                CharacterTestRuntimeAiPlannerSessionAuthorityComposition.Configure(server, configuration);
                CharacterTestRuntimeAiPlannerSessionAuthorityComposition.Configure(client, configuration);
                for (int i = 0; i < 24; i++)
                {
                    server.Tick(1d / 60d);
                    client.Tick(1d / 60d);
                }
                Assert.AreEqual(NetworkSessionStatus.Connected, client.Session.Status);

                ((ICharacterTestRuntimeAiPlannerSessionAuthorityClient)client)
                    .SubmitRuntimeAiPlannerSessionDeckRequest(
                    51L,
                    new CharacterTestRuntimeAiPlannerSessionDeckRequest(
                        "spoofed-client-id",
                        "character.hero",
                        1,
                        catalog.CatalogId,
                         catalog.Revision,
                         catalog.CatalogNumericId,
                         catalog.NumericIdentityHash,
                         catalog.CatalogHash,
                        new[] { catalog.Cards[0].NumericId },
                        new[] { "card.alpha" }));
                for (int i = 0; i < 4; i++)
                {
                    server.Tick(1d / 60d);
                    client.Tick(1d / 60d);
                }

                Assert.IsNotNull(observed);
                Assert.IsTrue(observed.Accepted, observed.Reason);
                Assert.AreEqual(1, server.SessionAuthorityParticipant.SessionLocks.Count, "server lock count");
                Assert.AreEqual(1, client.SessionAuthorityParticipant.SessionLocks.Count, "client lock count");
                Assert.IsTrue(server.SessionAuthorityParticipant.TryGetSessionLock(
                    "character.hero",
                    out CharacterTestRuntimeAiPlannerSessionDeckLock serverLock),
                    "server character lock missing");
                Assert.IsTrue(client.SessionAuthorityParticipant.TryGetSessionLock(
                    "character.hero",
                    out CharacterTestRuntimeAiPlannerSessionDeckLock clientLock),
                    "client character lock missing");
                Assert.AreEqual(serverLock.AuthorityHash, clientLock.AuthorityHash);
                Assert.AreEqual(
                    server.SessionAuthorityParticipant.AuthorityHash,
                    client.SessionAuthorityParticipant.AuthorityHash);
            }
            transport.Clear();
        }

        private static CharacterTestRuntimeAiPlannerSessionAuthorityResult Lock(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked,
            IEnumerable<string> stableCardIds,
            string characterStableId = "character.hero")
        {
            return CharacterTestRuntimeAiPlannerSessionAuthority.ValidateAndLock(
                CreateRequest(catalog, characterStableId, stableCardIds),
                CreateContext(catalog, unlocked));
        }

        private static bool AcceptRollbackCommand(RuntimeCommand command, out string failure)
        {
            failure = string.Empty;
            return true;
        }

        private static bool ReplayRollbackFrame(RuntimeFrame frame, out string failure)
        {
            failure = string.Empty;
            return true;
        }

        private static CharacterTestRuntimeAiPlannerSessionDeckRequest CreateRequest(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            string characterStableId,
            IEnumerable<string> stableCardIds,
            string catalogId = null,
            int? catalogRevision = null,
            long? catalogHash = null,
            string requesterPeerId = "peer.local",
            int characterNumericId = 1)
        {
            return new CharacterTestRuntimeAiPlannerSessionDeckRequest(
                requesterPeerId,
                characterStableId,
                characterNumericId,
                catalogId ?? catalog.CatalogId,
                 catalogRevision ?? catalog.Revision,
                 catalog.CatalogNumericId,
                 catalog.NumericIdentityHash,
                 catalogHash ?? catalog.CatalogHash,
                stableCardIds.Select(id => catalog.Cards.First(card => card.StableCardId == id).NumericId),
                stableCardIds);
        }

        private static CharacterTestRuntimeAiPlannerSessionAuthorityContext CreateContext(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked)
        {
            return new CharacterTestRuntimeAiPlannerSessionAuthorityContext(
                "peer.local",
                new[] { "character.hero" },
                unlocked,
                catalog,
                1);
        }

        private static CharacterTestRuntimeAiPlannerUnlockedCardCollection CreateUnlocked(
            params string[] stableCardIds)
        {
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult result =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(stableCardIds);
            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            return result.Collection;
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCard Publish(
            string stableCardId,
            int revision,
            int cost)
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture();
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                cardDraftId: null,
                publishedStableId: stableCardId,
                displayName: stableCardId,
                category: "Test",
                payload: payload);
            draft.LogicPayload = CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(payload);
            var request = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                stableCardId,
                revision,
                1,
                1,
                new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                    stableCardId,
                    "ui." + stableCardId,
                    cost,
                    "Test card",
                    "Test use",
                    "Test risk"));
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult result =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    request,
                    CreateReferences());
            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            return result.Card;
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog CreateCatalog(
            int revision,
            params CharacterTestRuntimeAiPlannerPublishedBehaviorCard[] cards)
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult result =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    "character-test.behavior-cards",
                    revision,
                    cards,
                    CreateReferences());
            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            return result.Catalog;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog CreateReferences()
        {
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            return new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateRegistry(),
                actionRuntime.Registry);
        }

        private static void AssertDiagnostic(
            IReadOnlyList<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic> diagnostics,
            string code)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                    return;
            }
            Assert.Fail("Expected diagnostic " + code + ". Actual: " + Describe(diagnostics));
        }

        private static string Describe(
            IReadOnlyList<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic> diagnostics)
        {
            var values = new List<string>();
            for (int i = 0; i < diagnostics.Count; i++)
                values.Add(diagnostics[i].Code + " " + diagnostics[i].Path + " " + diagnostics[i].Message);
            return string.Join(" | ", values);
        }

        private static string Describe(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            var values = new List<string>();
            for (int i = 0; i < diagnostics.Count; i++)
                values.Add(diagnostics[i].Code + " " + diagnostics[i].Path + " " + diagnostics[i].Message);
            return string.Join(" | ", values);
        }

        private static string Describe(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            var values = new List<string>();
            for (int i = 0; i < diagnostics.Count; i++)
                values.Add(diagnostics[i].Code + " " + diagnostics[i].Path + " " + diagnostics[i].Message);
            return string.Join(" | ", values);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.UI;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiPlannerInstalledDeckTests
    {
        [Test]
        public void ValidateAndCreate_NormalizesOrderAndPinsLatestCompatibleRevisions()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.bravo", 1, 2, 1, 1),
                Publish("card.alpha", 1, 3, 1, 2),
                Publish("card.alpha", 2, 3, 2, 2));

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult first = Build(
                catalog,
                "character.grunt",
                new[] { "card.bravo", "card.alpha" },
                runtimeContractVersion: 1);
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult second = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha", "card.bravo" },
                runtimeContractVersion: 1);

            Assert.IsTrue(first.Success, Describe(first.Diagnostics));
            Assert.IsTrue(second.Success, Describe(second.Diagnostics));
            Assert.AreEqual("card.alpha", first.Deck.Cards[0].StableCardId);
            Assert.AreEqual(1, first.Deck.Cards[0].Revision);
            Assert.AreEqual("card.bravo", first.Deck.Cards[1].StableCardId);
            Assert.AreEqual(5, first.Deck.TotalCost);
            Assert.AreEqual(first.Deck.DeckHash, second.Deck.DeckHash);
        }

        [Test]
        public void ValidateAndCreate_RejectsSlotLimitWithoutPartialDeck()
        {
            var cards = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard[7];
            var ids = new string[7];
            for (int i = 0; i < cards.Length; i++)
            {
                ids[i] = "card." + i;
                cards[i] = Publish(ids[i], 1, 1);
            }

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult result = Build(
                CreateCatalog(cards),
                "character.grunt",
                ids);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Deck);
            AssertDiagnostic(result.Diagnostics, "INSTALLED_DECK_SLOT_LIMIT_EXCEEDED");
        }

        [Test]
        public void ValidateAndCreate_RejectsCostLimitWithoutPartialDeck()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 6),
                Publish("card.bravo", 1, 5));

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult result = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha", "card.bravo" });

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Deck);
            AssertDiagnostic(result.Diagnostics, "INSTALLED_DECK_COST_LIMIT_EXCEEDED");
            AssertDiagnosticSeverity(
                result.Diagnostics,
                "INSTALLED_DECK_COST_LIMIT_EXCEEDED",
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error);
        }

        [Test]
        public void ValidateAndCreate_RejectsDuplicateMissingAndIncompatibleCards()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1, 2, 2));

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult duplicate = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha", "  card.alpha  " },
                runtimeContractVersion: 2);
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult missing = Build(
                catalog,
                "character.grunt",
                new[] { "card.missing" },
                runtimeContractVersion: 2);
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult incompatible = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" },
                runtimeContractVersion: 1);

            AssertDiagnostic(duplicate.Diagnostics, "INSTALLED_DECK_CARD_DUPLICATE");
            AssertDiagnostic(missing.Diagnostics, "INSTALLED_DECK_CARD_UNAVAILABLE");
            AssertDiagnostic(incompatible.Diagnostics, "INSTALLED_DECK_CARD_UNAVAILABLE");
            Assert.IsNull(duplicate.Deck);
            Assert.IsNull(missing.Deck);
            Assert.IsNull(incompatible.Deck);
        }

        [Test]
        public void ValidateAndCreate_RejectsMissingCharacterAndBlankCardId()
        {
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult result = Build(
                CreateCatalog(Publish("card.alpha", 1, 1)),
                "   ",
                new[] { "   " });

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Deck);
            AssertDiagnostic(result.Diagnostics, "INSTALLED_DECK_CHARACTER_ID_MISSING");
            AssertDiagnostic(result.Diagnostics, "INSTALLED_DECK_CARD_ID_MISSING");
        }

        [Test]
        public void ValidateAndCreate_AllowsExactLimitsAndIncludesCharacterInHash()
        {
            var cards = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard[6];
            var ids = new string[6];
            for (int i = 0; i < cards.Length; i++)
            {
                ids[i] = "card." + i;
                cards[i] = Publish(ids[i], 1, i < 4 ? 2 : 1);
            }
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(cards);

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult first = Build(catalog, "character.alpha", ids);
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult second = Build(catalog, "character.bravo", ids);

            Assert.IsTrue(first.Success, Describe(first.Diagnostics));
            Assert.IsTrue(second.Success, Describe(second.Diagnostics));
            Assert.AreEqual(CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumSlots, first.Deck.SlotCount);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumCost, first.Deck.TotalCost);
            Assert.AreNotEqual(first.Deck.DeckHash, second.Deck.DeckHash);
        }

        [Test]
        public void ValidateAndCreate_AllowsEmptyDeckWithFreeSystemFallbacks()
        {
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult result = Build(
                CreateCatalog(Publish("card.catalog-anchor", 1, 1)),
                "character.grunt",
                Array.Empty<string>());

            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            Assert.AreEqual(0, result.Deck.SlotCount);
            Assert.AreEqual(0, result.Deck.TotalCost);
            Assert.AreEqual(2, result.Deck.Fallbacks.Count);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerInstalledDeckFallbackKind.TargetNearestEnemy,
                result.Deck.Fallbacks[0].Kind);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerInstalledDeckFallbackKind.Idle, result.Deck.Fallbacks[1].Kind);
            Assert.IsFalse(result.Deck.Fallbacks[0].ConsumesSlot);
            Assert.AreEqual(0, result.Deck.Fallbacks[0].Cost);
            AssertDiagnostic(result.Diagnostics, "INSTALLED_DECK_FALLBACK_ONLY");
            AssertDiagnosticSeverity(
                result.Diagnostics,
                "INSTALLED_DECK_FALLBACK_ONLY",
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning);
        }

        [Test]
        public void CapabilityDiagnostics_AreWarningsAndDoNotBlockLegalDeck()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish(
                    "card.support-dependent",
                    1,
                    2,
                    requiredCapabilities: new[]
                    {
                        CharacterTestRuntimeAiPlannerBehaviorCapability.Support
                    }));
            var diagnosticsInput = new CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput(
                characterCapabilities: new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting
                },
                providedDeckCapabilities: new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Engage,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Damage
                });

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult result = Build(
                catalog,
                "character.grunt",
                new[] { "card.support-dependent" },
                diagnosticsInput: diagnosticsInput);

            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            Assert.IsNotNull(result.Deck);
            AssertDiagnostic(result.Diagnostics, "INSTALLED_DECK_DISENGAGE_MISSING");
            AssertDiagnostic(result.Diagnostics, "INSTALLED_DECK_SURVIVAL_MISSING");
            AssertDiagnostic(result.Diagnostics, "INSTALLED_DECK_CHARACTER_CAPABILITY_MISMATCH");
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning,
                    result.Diagnostics[i].Severity);
            }
        }

        [Test]
        public void CapabilityDiagnostics_CanRepresentAllBasicCoverageWithoutWarnings()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish(
                    "card.complete",
                    1,
                    2,
                    requiredCapabilities: new[]
                    {
                        CharacterTestRuntimeAiPlannerBehaviorCapability.Support
                    }));
            var diagnosticsInput = new CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput(
                characterCapabilities: new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Support
                },
                providedDeckCapabilities: new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Engage,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Damage,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Kite,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Defense
                });

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult result = Build(
                catalog,
                "character.grunt",
                new[] { "card.complete" },
                diagnosticsInput: diagnosticsInput);

            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            Assert.IsEmpty(result.Diagnostics);
        }

        [Test]
        public void SingleCardEdits_AreAtomicAndDoNotMutateSource()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 6),
                Publish("card.bravo", 1, 4),
                Publish("card.charlie", 1, 1));
            CharacterTestRuntimeAiPlannerInstalledDeck source = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha", "card.bravo" }).Deck;

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult failedInstall =
                CharacterTestRuntimeAiPlannerInstalledDeckRules.TryInstall(source, "card.charlie", catalog);
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult successfulRemove =
                CharacterTestRuntimeAiPlannerInstalledDeckRules.TryRemove(source, "card.bravo", catalog);

            Assert.IsFalse(failedInstall.Success);
            Assert.IsNull(failedInstall.Deck);
            Assert.AreEqual(2, source.Cards.Count);
            Assert.AreEqual(10, source.TotalCost);
            Assert.IsTrue(successfulRemove.Success, Describe(successfulRemove.Diagnostics));
            Assert.AreEqual(1, successfulRemove.Deck.Cards.Count);
            Assert.AreEqual("card.alpha", successfulRemove.Deck.Cards[0].StableCardId);
            Assert.AreEqual(2, source.Cards.Count);
        }

        [Test]
        public void SingleCardEdits_InstallAndMissingRemoveHaveExplicitResults()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 2),
                Publish("card.bravo", 1, 3));
            CharacterTestRuntimeAiPlannerInstalledDeck source = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" }).Deck;

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult installed =
                CharacterTestRuntimeAiPlannerInstalledDeckRules.TryInstall(source, "card.bravo", catalog);
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult missingRemove =
                CharacterTestRuntimeAiPlannerInstalledDeckRules.TryRemove(source, "card.bravo", catalog);

            Assert.IsTrue(installed.Success, Describe(installed.Diagnostics));
            Assert.AreEqual(2, installed.Deck.SlotCount);
            Assert.AreEqual(5, installed.Deck.TotalCost);
            Assert.IsFalse(missingRemove.Success);
            Assert.IsNull(missingRemove.Deck);
            AssertDiagnostic(missingRemove.Diagnostics, "INSTALLED_DECK_CARD_NOT_INSTALLED");
            Assert.AreEqual(1, source.SlotCount);
        }

        [Test]
        public void SingleCardEdits_RejectDifferentCatalogRevision()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = Publish("card.alpha", 1, 1);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog first = CreateCatalog(1, card);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog second = CreateCatalog(2, card);
            CharacterTestRuntimeAiPlannerInstalledDeck source = Build(
                first,
                "character.grunt",
                new[] { "card.alpha" }).Deck;

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult result =
                CharacterTestRuntimeAiPlannerInstalledDeckRules.TryRemove(source, "card.alpha", second);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Deck);
            AssertDiagnostic(result.Diagnostics, "INSTALLED_DECK_CATALOG_MISMATCH");
        }

        [Test]
        public void ExpandRecommendedPreset_UsesLoadoutVariantIdentityAndCreatesOrdinaryDeck()
        {
            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog loadouts =
                CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault();
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog cards = CreateCatalog(
                Publish("card.alpha", 1, 2),
                Publish("card.bravo", 1, 3));
            var preset = new CharacterTestRuntimeAiPlannerInstalledDeckPresetDefinition(
                loadouts.Loadouts[0].LoadoutId,
                loadouts.Loadouts[0].Variants[0].VariantId,
                new[] { "card.bravo", "card.alpha" });

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult result =
                CharacterTestRuntimeAiPlannerInstalledDeckRules.ExpandRecommendedPreset(
                    "character.grunt",
                    1,
                    preset,
                    loadouts,
                    cards,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion);

            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            Assert.AreEqual(2, result.Deck.Cards.Count);
            Assert.AreEqual("card.alpha", result.Deck.Cards[0].StableCardId);
            Assert.AreEqual(5, result.Deck.TotalCost);
        }

        [Test]
        public void ExpandRecommendedPreset_RejectsUnknownVariantWithoutPartialDeck()
        {
            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog loadouts =
                CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault();
            var preset = new CharacterTestRuntimeAiPlannerInstalledDeckPresetDefinition(
                loadouts.Loadouts[0].LoadoutId,
                999,
                new[] { "card.alpha" });

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult result =
                CharacterTestRuntimeAiPlannerInstalledDeckRules.ExpandRecommendedPreset(
                    "character.grunt",
                    1,
                    preset,
                    loadouts,
                    CreateCatalog(Publish("card.alpha", 1, 1)),
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Deck);
            AssertDiagnostic(result.Diagnostics, "INSTALLED_DECK_PRESET_VARIANT_UNKNOWN");
        }

        [Test]
        public void InstalledDeck_CollectionsDoNotExposeMutableBackingArrays()
        {
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult result = Build(
                CreateCatalog(Publish("card.alpha", 1, 1)),
                "character.grunt",
                new[] { "card.alpha" });

            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            Assert.IsFalse(result.Deck.Cards is CharacterTestRuntimeAiPlannerInstalledDeckCard[]);
            Assert.IsFalse(result.Deck.Fallbacks is CharacterTestRuntimeAiPlannerInstalledDeckFallback[]);
        }

        [Test]
        public void InstalledDeck_CannotBypassValidatedCreationBoundary()
        {
            ConstructorInfo[] constructors = typeof(CharacterTestRuntimeAiPlannerInstalledDeck)
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.IsNotEmpty(constructors);
            for (int i = 0; i < constructors.Length; i++)
                Assert.IsTrue(constructors[i].IsPrivate, constructors[i].ToString());
        }

        [Test]
        public void InstalledDeck_FallbackIdentityAffectsDeckHash()
        {
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult result = Build(
                CreateCatalog(Publish("card.alpha", 1, 1)),
                "character.grunt",
                new[] { "card.alpha" });
            var alteredFallbacks = new[]
            {
                new CharacterTestRuntimeAiPlannerInstalledDeckFallback(
                    CharacterTestRuntimeAiPlannerInstalledDeckFallbackKind.TargetNearestEnemy,
                    "system.runtime-ai-planner.fallback.changed-targeting"),
                new CharacterTestRuntimeAiPlannerInstalledDeckFallback(
                    CharacterTestRuntimeAiPlannerInstalledDeckFallbackKind.Idle,
                    "system.runtime-ai-planner.fallback.idle")
            };
            long alteredFallbackHash =
                CharacterTestRuntimeAiPlannerInstalledDeckFallbacks.ComputeContractHash(alteredFallbacks);
            long rebuiltWithCurrentFallbacks = CharacterTestRuntimeAiPlannerInstalledDeck.ComputeHash(
                result.Deck.CharacterStableId,
                result.Deck.CharacterNumericId,
                CreateCatalog(Publish("card.alpha", 1, 1)),
                result.Deck.RuntimeContractVersion,
                result.Deck.Cards,
                result.Deck.TotalCost,
                CharacterTestRuntimeAiPlannerInstalledDeckFallbacks.ContractHash);
            long rebuiltWithAlteredFallbacks = CharacterTestRuntimeAiPlannerInstalledDeck.ComputeHash(
                result.Deck.CharacterStableId,
                result.Deck.CharacterNumericId,
                CreateCatalog(Publish("card.alpha", 1, 1)),
                result.Deck.RuntimeContractVersion,
                result.Deck.Cards,
                result.Deck.TotalCost,
                alteredFallbackHash);

            Assert.AreNotEqual(CharacterTestRuntimeAiPlannerInstalledDeckFallbacks.ContractHash, alteredFallbackHash);
            Assert.AreEqual(result.Deck.DeckHash, rebuiltWithCurrentFallbacks);
            Assert.AreNotEqual(result.Deck.DeckHash, rebuiltWithAlteredFallbacks);
        }

        [Test]
        public void PlayerDeckWorkbench_ProjectsLatestCardsAndAppliesOneClickEdits()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 2),
                Publish("card.alpha", 2, 2),
                Publish("card.bravo", 1, 3));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult empty = Build(
                catalog,
                "character.grunt",
                Array.Empty<string>());
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha", "card.bravo" });

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    empty.Deck);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult bravo =
                created.Workbench.TryInstall("card.bravo");
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult alpha =
                created.Workbench.TryInstall("card.alpha");
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult removed =
                created.Workbench.TryRemove("card.bravo");

            Assert.IsTrue(created.Success, Describe(created.Diagnostics));
            AssertDiagnostic(created.View.Diagnostics, "INSTALLED_DECK_FALLBACK_ONLY");
            Assert.AreEqual(2, created.View.Cards.Count, "Only the latest compatible revision should be projected per stable card id.");
            Assert.AreEqual(2, created.View.Cards[0].Revision);
            Assert.IsTrue(bravo.Success, Describe(bravo.Diagnostics));
            Assert.IsTrue(alpha.Success, Describe(alpha.Diagnostics));
            Assert.AreEqual("card.alpha", alpha.View.Cards[0].StableCardId);
            Assert.IsTrue(alpha.View.Cards[0].IsInstalled);
            Assert.AreEqual("card.bravo", alpha.View.Cards[1].StableCardId);
            Assert.IsTrue(alpha.View.Cards[1].IsInstalled);
            Assert.AreEqual(2, alpha.View.SlotCount);
            Assert.AreEqual(5, alpha.View.TotalCost);
            Assert.IsTrue(removed.Success, Describe(removed.Diagnostics));
            Assert.AreEqual(1, removed.View.SlotCount);
            Assert.IsFalse(removed.View.Cards[1].IsInstalled);
            Assert.IsTrue(removed.View.Cards[1].CanInstall);
        }

        [Test]
        public void PlayerDeckWorkbench_RejectsLockedCardAndPreservesDeck()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 2),
                Publish("card.locked", 1, 2));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck);
            long sourceHash = created.Workbench.Deck.DeckHash;

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result =
                created.Workbench.TryInstall("card.locked");

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "PLAYER_DECK_CARD_NOT_UNLOCKED");
            Assert.AreEqual(sourceHash, created.Workbench.Deck.DeckHash);
            Assert.AreEqual(1, created.Workbench.Deck.SlotCount);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.NotUnlocked,
                result.View.Cards[1].BlockReason);
        }

        [Test]
        public void PlayerDeckWorkbench_HardRuleFailureIsAtomicAndVisibleInProjection()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 8),
                Publish("card.bravo", 1, 3));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha", "card.bravo" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck);
            long sourceHash = created.Workbench.Deck.DeckHash;

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result =
                created.Workbench.TryInstall("card.bravo");

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "INSTALLED_DECK_COST_LIMIT_EXCEEDED");
            Assert.AreEqual(sourceHash, created.Workbench.Deck.DeckHash);
            Assert.AreEqual(1, result.View.SlotCount);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.CostLimit,
                result.View.Cards[1].BlockReason);
        }

        [Test]
        public void PlayerDeckWorkbench_SessionLockDisablesEveryEdit()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                Array.Empty<string>());
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck);

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult locked =
                created.Workbench.LockSession();
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result =
                created.Workbench.TryInstall("card.alpha");

            Assert.IsTrue(locked.View.IsSessionLocked);
            Assert.IsFalse(locked.View.Cards[0].CanInstall);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.SessionLocked,
                locked.View.Cards[0].BlockReason);
            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "PLAYER_DECK_SESSION_LOCKED");
            Assert.AreEqual(0, created.Workbench.Deck.SlotCount);
        }

        [Test]
        public void PlayerDeckWorkbench_PresetRejectsLockedContentWithoutPartialReplacement()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1),
                Publish("card.locked", 1, 1));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog loadouts =
                CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault();
            var preset = new CharacterTestRuntimeAiPlannerInstalledDeckPresetDefinition(
                loadouts.Loadouts[0].LoadoutId,
                loadouts.Loadouts[0].Variants[0].VariantId,
                new[] { "card.locked" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck);
            long sourceHash = created.Workbench.Deck.DeckHash;

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreviewResult result =
                created.Workbench.TryPreviewPreset(preset, loadouts);

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "PLAYER_DECK_PRESET_CARD_NOT_UNLOCKED");
            Assert.AreEqual(sourceHash, created.Workbench.Deck.DeckHash);
            Assert.AreEqual("card.alpha", created.Workbench.Deck.Cards[0].StableCardId);
        }

        [Test]
        public void PlayerDeckWorkbench_PresetPreviewDoesNotMutateAndProjectsStableDiff()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1),
                Publish("card.bravo", 1, 2),
                Publish("card.charlie", 1, 3));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha", "card.bravo" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha", "card.bravo", "card.charlie" });
            var persistence = new TestPersistenceSink(true);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck,
                    null,
                    persistence);
            long sourceHash = created.Workbench.Deck.DeckHash;

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreviewResult result =
                created.Workbench.TryPreviewPreset(
                    CreatePreset("card.bravo", "card.charlie"),
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault());

            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            Assert.AreEqual(sourceHash, created.Workbench.Deck.DeckHash);
            Assert.AreEqual(0, persistence.CallCount);
            Assert.AreEqual("card.charlie", result.Preview.Added[0].StableCardId);
            Assert.AreEqual("card.bravo", result.Preview.Retained[0].StableCardId);
            Assert.AreEqual("card.alpha", result.Preview.Removed[0].StableCardId);
            Assert.IsTrue(result.Preview.CanConfirm);
        }

        [Test]
        public void PlayerDeckWorkbench_PresetPreviewCannotBypassAuthorityConstructionBoundary()
        {
            ConstructorInfo[] constructors = typeof(CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview)
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.IsNotEmpty(constructors);
            for (int i = 0; i < constructors.Length; i++)
                Assert.IsTrue(constructors[i].IsPrivate, constructors[i].ToString());
        }

        [Test]
        public void PlayerDeckWorkbench_ForgedPresetCandidateIsDefensivelyRejected()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1),
                Publish("card.bravo", 1, 2));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult foreignCandidate = Build(
                catalog,
                "character.other",
                new[] { "card.bravo" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha", "card.bravo" });
            var persistence = new TestPersistenceSink(true);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck,
                    null,
                    persistence);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview validPreview =
                created.Workbench.TryPreviewPreset(
                    CreatePreset("card.bravo"),
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault()).Preview;
            Type previewType = typeof(CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview);
            ConstructorInfo constructor = previewType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)[0];
            object ownerToken = previewType.GetField(
                "_ownerToken",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(validPreview);
            long sourceEditEpoch = (long)previewType.GetProperty(
                "SourceEditEpoch",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(validPreview);
            var emptyDiff = Array.Empty<CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetDiffEntry>();
            var forged = (CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview)constructor.Invoke(
                new object[]
                {
                    ownerToken,
                    sourceEditEpoch,
                    validPreview.CharacterStableId,
                    validPreview.CatalogHash,
                    validPreview.RuntimeContractVersion,
                    new[] { "card.alpha" },
                    validPreview.SourceReferenceFingerprint,
                    validPreview.SourceDeckHash,
                    validPreview.SourceResolved,
                    validPreview.SourceKnownCost,
                    validPreview.SourceBudgetComplete,
                    foreignCandidate.Deck,
                    Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                    emptyDiff,
                    emptyDiff,
                    emptyDiff,
                    true
                });

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result =
                created.Workbench.TryConfirmPreset(forged);

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "PLAYER_DECK_PRESET_CANDIDATE_INVALID");
            Assert.AreEqual("character.grunt", created.Workbench.Deck.CharacterStableId);
            Assert.AreEqual(initial.Deck.DeckHash, created.Workbench.Deck.DeckHash);
            Assert.AreEqual(0, persistence.CallCount);
        }

        [Test]
        public void PlayerDeckWorkbench_NoChangePresetCannotConfirm()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck);

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview preview =
                created.Workbench.TryPreviewPreset(
                    CreatePreset("card.alpha"),
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault()).Preview;
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult confirmed =
                created.Workbench.TryConfirmPreset(preview);

            Assert.IsFalse(preview.CanConfirm);
            Assert.IsFalse(confirmed.Success);
            AssertDiagnostic(confirmed.Diagnostics, "PLAYER_DECK_PRESET_NO_CHANGES");
        }

        [Test]
        public void PlayerDeckWorkbench_PresetConfirmAndUndoAreAtomicAndOneShot()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1),
                Publish("card.bravo", 1, 2));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha", "card.bravo" });
            var persistence = new TestPersistenceSink(true);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck,
                    null,
                    persistence);
            long sourceHash = created.Workbench.Deck.DeckHash;
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview preview =
                created.Workbench.TryPreviewPreset(
                    CreatePreset("card.bravo"),
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault()).Preview;

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult confirmed =
                created.Workbench.TryConfirmPreset(preview);
            long candidateHash = created.Workbench.Deck.DeckHash;
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult undone =
                created.Workbench.TryUndoLastPreset();
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult secondUndo =
                created.Workbench.TryUndoLastPreset();

            Assert.IsTrue(confirmed.Success, Describe(confirmed.Diagnostics));
            Assert.AreNotEqual(sourceHash, candidateHash);
            Assert.IsTrue(undone.Success, Describe(undone.Diagnostics));
            Assert.AreEqual(sourceHash, created.Workbench.Deck.DeckHash);
            Assert.AreEqual(2, persistence.CallCount);
            Assert.IsFalse(secondUndo.Success);
            AssertDiagnostic(secondUndo.Diagnostics, "PLAYER_DECK_PRESET_UNDO_UNAVAILABLE");
        }

        [Test]
        public void PlayerDeckWorkbench_PresetPreviewBecomesStaleAfterSuccessfulEdit()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1),
                Publish("card.bravo", 1, 2),
                Publish("card.charlie", 1, 1));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha", "card.bravo", "card.charlie" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview preview =
                created.Workbench.TryPreviewPreset(
                    CreatePreset("card.bravo"),
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault()).Preview;

            Assert.IsTrue(created.Workbench.TryInstall("card.charlie").Success);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult confirmed =
                created.Workbench.TryConfirmPreset(preview);

            Assert.IsFalse(confirmed.Success);
            AssertDiagnostic(confirmed.Diagnostics, "PLAYER_DECK_PRESET_PREVIEW_STALE");
            Assert.AreEqual(2, created.Workbench.Deck.SlotCount);
        }

        [Test]
        public void PlayerDeckWorkbench_PresetConfirmPersistenceFailureIsRetriable()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1),
                Publish("card.bravo", 1, 2));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha", "card.bravo" });
            var persistence = new TestPersistenceSink(false);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck,
                    null,
                    persistence);
            long sourceHash = created.Workbench.Deck.DeckHash;
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview preview =
                created.Workbench.TryPreviewPreset(
                    CreatePreset("card.bravo"),
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault()).Preview;

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult rejected =
                created.Workbench.TryConfirmPreset(preview);
            persistence.Success = true;
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult retried =
                created.Workbench.TryConfirmPreset(preview);

            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(sourceHash, rejected.View.DeckHash);
            Assert.IsTrue(retried.Success, Describe(retried.Diagnostics));
            Assert.AreEqual("card.bravo", created.Workbench.Deck.Cards[0].StableCardId);
        }

        [Test]
        public void PlayerDeckWorkbench_PresetUndoPersistenceFailureKeepsCheckpointForRetry()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1),
                Publish("card.bravo", 1, 2));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha", "card.bravo" });
            var persistence = new TestPersistenceSink(true);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck,
                    null,
                    persistence);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview preview =
                created.Workbench.TryPreviewPreset(
                    CreatePreset("card.bravo"),
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault()).Preview;
            Assert.IsTrue(created.Workbench.TryConfirmPreset(preview).Success);
            long candidateHash = created.Workbench.Deck.DeckHash;

            persistence.Success = false;
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult rejected =
                created.Workbench.TryUndoLastPreset();

            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(candidateHash, created.Workbench.View.DeckHash);
            Assert.IsTrue(created.Workbench.CanUndoLastPreset);

            persistence.Success = true;
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult retried =
                created.Workbench.TryUndoLastPreset();

            Assert.IsTrue(retried.Success, Describe(retried.Diagnostics));
            Assert.AreEqual("card.alpha", created.Workbench.Deck.Cards[0].StableCardId);
        }

        [Test]
        public void PlayerDeckWorkbench_PresetPreviewCannotCrossWorkbenchOwnership()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1),
                Publish("card.bravo", 1, 2));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha", "card.bravo" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult first =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult second =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview preview =
                first.Workbench.TryPreviewPreset(
                    CreatePreset("card.bravo"),
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault()).Preview;

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result =
                second.Workbench.TryConfirmPreset(preview);

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "PLAYER_DECK_PRESET_PREVIEW_STALE");
            Assert.AreEqual("card.alpha", second.Workbench.Deck.Cards[0].StableCardId);
        }

        [Test]
        public void PlayerDeckWorkbench_PresetUndoSurvivesRejectedEditButNotSuccessfulEditOrLock()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1),
                Publish("card.bravo", 1, 2),
                Publish("card.charlie", 1, 1),
                Publish("card.locked", 1, 1));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha", "card.bravo", "card.charlie" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult edited =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview preview =
                edited.Workbench.TryPreviewPreset(
                    CreatePreset("card.bravo"),
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault()).Preview;
            Assert.IsTrue(edited.Workbench.TryConfirmPreset(preview).Success);

            Assert.IsFalse(edited.Workbench.TryInstall("card.locked").Success);
            Assert.IsTrue(edited.Workbench.CanUndoLastPreset);
            Assert.IsTrue(edited.Workbench.TryInstall("card.charlie").Success);
            Assert.IsFalse(edited.Workbench.CanUndoLastPreset);

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult locked =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview lockedPreview =
                locked.Workbench.TryPreviewPreset(
                    CreatePreset("card.bravo"),
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault()).Preview;
            Assert.IsTrue(locked.Workbench.TryConfirmPreset(lockedPreview).Success);
            Assert.IsTrue(locked.Workbench.CanUndoLastPreset);
            locked.Workbench.LockSession();
            Assert.IsFalse(locked.Workbench.CanUndoLastPreset);
        }

        [Test]
        public void PlayerDeckWorkbench_PresetUndoRestoresUnresolvedSourceReferences()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha", "card.retired" });
            var persisted = new CharacterTestRuntimeAiPlannerPersistedDeck(
                "character.grunt",
                new[] { "card.retired" },
                1);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    persisted,
                    runtimeContractVersion: 1);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview preview =
                created.Workbench.TryPreviewPreset(
                    CreatePreset("card.alpha"),
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault()).Preview;

            Assert.IsFalse(preview.SourceBudgetComplete);
            Assert.IsFalse(preview.DeltaKnown);
            Assert.IsFalse(preview.Removed[0].CostKnown);
            Assert.IsTrue(created.Workbench.TryConfirmPreset(preview).Success);
            Assert.IsTrue(created.Workbench.TryUndoLastPreset().Success);
            Assert.IsNull(created.Workbench.Deck);
            Assert.AreEqual("card.retired", created.Workbench.PersistedDeck.StableCardIds[0]);
            Assert.IsFalse(created.Workbench.View.IsResolved);
        }

        [Test]
        public void PlayerDeckWorkbench_FailedCommandKeepsDeckDiagnosticsSeparate()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.locked", 1, 1));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult empty = Build(
                catalog,
                "character.grunt",
                Array.Empty<string>());
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(Array.Empty<string>());
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    empty.Deck);

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult rejected =
                created.Workbench.TryInstall("card.locked");
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult locked =
                created.Workbench.LockSession();

            Assert.IsFalse(rejected.Success);
            AssertDiagnostic(rejected.View.DeckDiagnostics, "INSTALLED_DECK_FALLBACK_ONLY");
            AssertDiagnostic(rejected.View.LastCommandDiagnostics, "PLAYER_DECK_CARD_NOT_UNLOCKED");
            AssertDiagnostic(rejected.Diagnostics, "PLAYER_DECK_CARD_NOT_UNLOCKED");
            AssertDiagnostic(locked.View.DeckDiagnostics, "INSTALLED_DECK_FALLBACK_ONLY");
            Assert.AreEqual(0, locked.View.LastCommandDiagnostics.Count);
        }

        [Test]
        public void PlayerDeckWorkbench_ProjectsOwnedUnavailableAndIncompatibleReferences()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.anchor", 1, 1),
                Publish("card.incompatible", 1, 2, minimumRuntimeVersion: 2, maximumRuntimeVersion: 2));
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.incompatible", "card.retired" });
            var persisted = new CharacterTestRuntimeAiPlannerPersistedDeck(
                "character.grunt",
                new[] { "card.incompatible", "card.retired" },
                1);

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    persisted,
                    runtimeContractVersion: 1);
            CharacterTestRuntimeAiPlannerPlayerDeckCardView incompatible =
                FindCardView(created.View.Cards, "card.incompatible");
            CharacterTestRuntimeAiPlannerPlayerDeckCardView retired =
                FindCardView(created.View.Cards, "card.retired");

            Assert.IsTrue(created.Success, Describe(created.Diagnostics));
            Assert.IsFalse(created.View.IsResolved);
            Assert.IsTrue(created.View.RequiresReplacement);
            Assert.IsNull(created.Workbench.Deck);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Incompatible, incompatible.Availability);
            Assert.IsTrue(incompatible.IsUnlocked);
            Assert.IsTrue(incompatible.IsInstalled);
            Assert.IsTrue(incompatible.CanReplace);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Unavailable, retired.Availability);
            Assert.IsTrue(retired.IsUnlocked);
            Assert.IsTrue(retired.IsInstalled);
            Assert.IsTrue(retired.CanRemove);
            Assert.IsTrue(retired.CanReplace);
            AssertDiagnostic(created.View.DeckDiagnostics, "PLAYER_DECK_CARD_INCOMPATIBLE");
            AssertDiagnostic(created.View.DeckDiagnostics, "PLAYER_DECK_CARD_UNAVAILABLE");
            AssertDiagnostic(created.View.DeckDiagnostics, "PLAYER_DECK_REQUIRES_REPLACEMENT");
        }

        [Test]
        public void PlayerDeckWorkbench_ReplacesUnavailableReferenceAndResolvesDeck()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.anchor", 1, 1),
                Publish("card.replacement", 1, 3));
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.replacement", "card.retired" });
            var persisted = new CharacterTestRuntimeAiPlannerPersistedDeck(
                "character.grunt",
                new[] { "card.retired" },
                1);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    persisted,
                    runtimeContractVersion: 1);

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult replaced =
                created.Workbench.TryReplace("card.retired", "card.replacement");

            Assert.IsTrue(replaced.Success, Describe(replaced.Diagnostics));
            Assert.IsTrue(replaced.View.IsResolved);
            Assert.IsFalse(replaced.View.RequiresReplacement);
            Assert.IsNotNull(created.Workbench.Deck);
            Assert.AreEqual(1, created.Workbench.Deck.Cards.Count);
            Assert.AreEqual("card.replacement", created.Workbench.Deck.Cards[0].StableCardId);
            Assert.IsTrue(FindCardView(replaced.View.Cards, "card.retired").IsUnlocked);
            Assert.IsFalse(FindCardView(replaced.View.Cards, "card.retired").IsInstalled);
            Assert.IsTrue(FindCardView(replaced.View.Cards, "card.replacement").IsInstalled);
            Assert.AreEqual(0, replaced.View.LastCommandDiagnostics.Count);
        }

        [Test]
        public void PlayerDeckWorkbench_AllowsIncrementalRepairOfMultipleUnavailableReferences()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.replacement-a", 1, 2),
                Publish("card.replacement-b", 1, 3));
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[]
                    {
                        "card.replacement-a",
                        "card.replacement-b",
                        "card.retired-a",
                        "card.retired-b"
                    });
            var persisted = new CharacterTestRuntimeAiPlannerPersistedDeck(
                "character.grunt",
                new[] { "card.retired-a", "card.retired-b" },
                1);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    persisted,
                    runtimeContractVersion: 1);

            Assert.IsTrue(
                FindCardView(created.View.Cards, "card.replacement-a").CanUseAsReplacement);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult first =
                created.Workbench.TryReplace("card.retired-a", "card.replacement-a");

            Assert.IsTrue(first.Success, Describe(first.Diagnostics));
            Assert.IsFalse(first.View.IsResolved);
            Assert.IsNull(created.Workbench.Deck);
            Assert.AreEqual("card.replacement-a", created.Workbench.PersistedDeck.StableCardIds[0]);
            Assert.AreEqual("card.retired-b", created.Workbench.PersistedDeck.StableCardIds[1]);

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult second =
                created.Workbench.TryReplace("card.retired-b", "card.replacement-b");

            Assert.IsTrue(second.Success, Describe(second.Diagnostics));
            Assert.IsTrue(second.View.IsResolved);
            Assert.IsNotNull(created.Workbench.Deck);
            Assert.AreEqual("card.replacement-a", created.Workbench.PersistedDeck.StableCardIds[0]);
            Assert.AreEqual("card.replacement-b", created.Workbench.PersistedDeck.StableCardIds[1]);
            Assert.AreEqual(2, created.Workbench.Deck.Cards.Count);
        }

        [Test]
        public void PlayerDeckWorkbench_PersistenceFailurePreservesDeckAndHash()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 2));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult empty = Build(
                catalog,
                "character.grunt",
                Array.Empty<string>());
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(new[] { "card.alpha" });
            var persistence = new TestPersistenceSink(false);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    empty.Deck,
                    null,
                    persistence);
            long sourceHash = created.Workbench.Deck.DeckHash;

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result =
                created.Workbench.TryInstall("card.alpha");

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "TEST_PERSISTENCE_REJECTED");
            Assert.AreEqual(sourceHash, created.Workbench.Deck.DeckHash);
            Assert.AreEqual(0, created.Workbench.Deck.SlotCount);
            Assert.AreEqual(0, created.Workbench.PersistedDeck.StableCardIds.Count);
            Assert.AreEqual("card.alpha", persistence.LastDeck.StableCardIds[0]);
        }

        [Test]
        public void PlayerDeckWorkbench_PersistsNormalizedCandidateBeforeCommitting()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 2),
                Publish("card.bravo", 1, 3));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult initial = Build(
                catalog,
                "character.grunt",
                new[] { "card.bravo" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha", "card.bravo" });
            var persistence = new TestPersistenceSink(true);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    initial.Deck,
                    null,
                    persistence);

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result =
                created.Workbench.TryInstall("card.alpha");

            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            Assert.AreEqual(1, persistence.CallCount);
            Assert.AreEqual("card.alpha", persistence.LastDeck.StableCardIds[0]);
            Assert.AreEqual("card.bravo", persistence.LastDeck.StableCardIds[1]);
            Assert.AreEqual(persistence.LastDeck.StableCardIds, created.Workbench.PersistedDeck.StableCardIds);
        }

        [Test]
        public void LocalPlayerDeckService_RoundtripRestoresInstalledCards()
        {
            string directory = Path.Combine(Path.GetTempPath(), "mx-player-deck-" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(directory, "deck.json");
            try
            {
                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                        filePath,
                        1001,
                        out CharacterTestRuntimeAiPlannerLocalPlayerDeckService first,
                        out string firstError),
                    firstError);
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult install =
                    first.Workbench.TryInstall("bc_3qz33zjzk4");

                Assert.IsTrue(install.Success, Describe(install.Diagnostics));
                Assert.IsTrue(File.Exists(filePath));
                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                        filePath,
                        1001,
                        out CharacterTestRuntimeAiPlannerLocalPlayerDeckService restored,
                        out string restoredError),
                    restoredError);
                Assert.AreEqual(string.Empty, restored.LoadWarning);
                Assert.AreEqual(1, restored.Workbench.Deck.SlotCount);
                Assert.AreEqual(
                    "bc_3qz33zjzk4",
                    restored.Workbench.Deck.Cards[0].StableCardId);
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult secondInstall =
                    restored.Workbench.TryInstall("bc_g6a5fw319a");
                Assert.IsTrue(secondInstall.Success, Describe(secondInstall.Diagnostics));
                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                        filePath,
                        1001,
                        out CharacterTestRuntimeAiPlannerLocalPlayerDeckService restoredAgain,
                        out string restoredAgainError),
                    restoredAgainError);
                Assert.AreEqual(2, restoredAgain.Workbench.Deck.SlotCount);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void LocalPlayerDeckService_CorruptSaveKeepsSafeDefaultAndOriginalFile()
        {
            string directory = Path.Combine(Path.GetTempPath(), "mx-player-deck-" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(directory, "deck.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(filePath, "{corrupt");
            try
            {
                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                        filePath,
                        1001,
                        out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                        out string error),
                    error);

                Assert.IsNotEmpty(service.LoadWarning);
                Assert.AreEqual(0, service.Workbench.Deck.SlotCount);
                AssertDiagnostic(
                    service.Workbench.View.DeckDiagnostics,
                    "PLAYER_DECK_PERSISTENCE_RESTORE_FAILED");
                Assert.IsTrue(File.Exists(service.RecoveryBackupPath));
                Assert.AreEqual("{corrupt", File.ReadAllText(service.RecoveryBackupPath));
                Assert.AreEqual("{corrupt", File.ReadAllText(filePath));
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult install =
                    service.Workbench.TryInstall("bc_3qz33zjzk4");
                Assert.IsTrue(install.Success, Describe(install.Diagnostics));
                Assert.AreEqual("{corrupt", File.ReadAllText(service.RecoveryBackupPath));
                Assert.AreNotEqual("{corrupt", File.ReadAllText(filePath));
                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                        filePath,
                        1001,
                        out CharacterTestRuntimeAiPlannerLocalPlayerDeckService restored,
                        out string restoredError),
                    restoredError);
                Assert.AreEqual(string.Empty, restored.LoadWarning);
                Assert.AreEqual(1, restored.Workbench.Deck.SlotCount);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void LocalPlayerDeckService_IncompatibleLocalCatalogFallsBackUntilNextValidSave()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "mx-player-deck-stale-catalog-" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(directory, "deck.json");
            string catalogPath = Path.Combine(directory, "deck.published-behavior-cards.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(catalogPath, "{incompatible-local-catalog");
            try
            {
                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                        filePath,
                        1001,
                        out CharacterTestRuntimeAiPlannerLocalPlayerDeckService recovered,
                        out string error),
                    error);
                StringAssert.Contains("已忽略", recovered.LoadWarning);
                AssertDiagnostic(
                    recovered.Workbench.View.DeckDiagnostics,
                    "PLAYER_DECK_CATALOG_RECOVERY");
                Assert.AreEqual("{incompatible-local-catalog", File.ReadAllText(catalogPath));

                CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult install =
                    recovered.Workbench.TryInstall("bc_3qz33zjzk4");
                Assert.IsTrue(install.Success, Describe(install.Diagnostics));
                Assert.AreNotEqual("{incompatible-local-catalog", File.ReadAllText(catalogPath));

                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                        filePath,
                        1001,
                        out CharacterTestRuntimeAiPlannerLocalPlayerDeckService restored,
                        out string restoredError),
                    restoredError);
                Assert.AreEqual(string.Empty, restored.LoadWarning);
                Assert.AreEqual(1, restored.Workbench.Deck.SlotCount);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void LocalPlayerDeckService_FailedRuntimeInstallDoesNotCommitSessionLock()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "mx-player-deck-install-failure-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                        Path.Combine(directory, "deck.json"),
                        1001,
                        out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                        out string createError),
                    createError);

                bool installed = service.TryLockAndInstallForSession(
                    null,
                    Array.Empty<CharacterTestCharacterEntity>(),
                    out string error);

                Assert.IsFalse(installed);
                StringAssert.Contains("command module", error);
                Assert.IsNull(service.SessionLock);
                Assert.IsFalse(service.Workbench.View.IsSessionLocked);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SessionBuilder_PlayerDeckTargetsUsePlayerTeamAndStableCharacterOrder()
        {
            var startupSpecs = new[]
            {
                new CharacterTestCharacterSpawnSpec(
                    characterId: 1000,
                    teamId: 2,
                    x: 0f,
                    y: 1f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled),
                new CharacterTestCharacterSpawnSpec(
                    characterId: 3003,
                    teamId: 1,
                    x: 0f,
                    y: 1f,
                    z: 4f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled)
                    .WithRuntimeAiPlannerProfileSlot(4),
                new CharacterTestCharacterSpawnSpec(
                    characterId: 2002,
                    teamId: 1,
                    x: 0f,
                    y: 1f,
                    z: 8f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled)
                    .WithRuntimeAiPlannerProfileSlot(3),
                new CharacterTestCharacterSpawnSpec(
                    characterId: 3003,
                    teamId: 1,
                    x: 0f,
                    y: 1f,
                    z: 12f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled)
            };

            CharacterTestRuntimeAiPlannerPlayerDeckTarget[] targets =
                CharacterTestRuntimeSessionBuilder.ResolvePlayerBehaviorDeckTargets(
                startupSpecs,
                arenaMatchConfig: null,
                entities: Array.Empty<CharacterTestCharacterEntity>());

            Assert.AreEqual(2, targets.Length);
            Assert.AreEqual(2002, targets[0].CharacterId);
            Assert.AreEqual(3, targets[0].PreferredProfileSlot);
            Assert.AreEqual(3003, targets[1].CharacterId);
            Assert.AreEqual(4, targets[1].PreferredProfileSlot);
        }

        [Test]
        public void SessionBuilder_PlayerDeckTargetUsesEnabledArenaSlotBeforeEntitiesExist()
        {
            CharacterTestArenaMatchConfig arena = CharacterTestArenaMatchConfig.CreateDefault();
            arena.Team1Slots[0].CharacterId = 3003;

            CharacterTestRuntimeAiPlannerPlayerDeckTarget[] targets =
                CharacterTestRuntimeSessionBuilder.ResolvePlayerBehaviorDeckTargets(
                startupSpecs: null,
                arenaMatchConfig: arena,
                entities: Array.Empty<CharacterTestCharacterEntity>());

            Assert.AreEqual(1, targets.Length);
            Assert.AreEqual(3003, targets[0].CharacterId);
            Assert.AreEqual(CharacterTestArenaMatchConfig.Team1Id, targets[0].TeamId);
        }

        [Test]
        public void SessionBuilder_MissingPlayerDeckTargetWarnsOnlyWhenLabRequiresTheLayer()
        {
            Assert.IsFalse(
                CharacterTestRuntimeSessionBuilder.ShouldWarnWhenPlayerBehaviorDeckTargetsAreMissing(
                    allowLockedLabPresentation: false));
            Assert.IsTrue(
                CharacterTestRuntimeSessionBuilder.ShouldWarnWhenPlayerBehaviorDeckTargetsAreMissing(
                    allowLockedLabPresentation: true));
        }

        [Test]
        public void SessionBuilder_RuntimeAiLabNeverFallsBackToLegacyLoadoutPanel()
        {
            Assert.IsFalse(CharacterTestRuntimeSessionBuilder.ShouldEnableRuntimeAiBehaviorLoadoutPanel(
                runtimeAiLab: true,
                hasPlayerBehaviorDeckService: false));
            Assert.IsTrue(CharacterTestRuntimeSessionBuilder.ShouldEnableRuntimeAiBehaviorLoadoutPanel(
                runtimeAiLab: true,
                hasPlayerBehaviorDeckService: true));
            Assert.IsTrue(CharacterTestRuntimeSessionBuilder.ShouldEnableRuntimeAiBehaviorLoadoutPanel(
                runtimeAiLab: false,
                hasPlayerBehaviorDeckService: false));
        }

        [Test]
        public void SessionBuilder_PlayerDeckProfileReservationDoesNotReuseEnemySpawnSlot()
        {
            var sourceSpecs = new[]
            {
                new CharacterTestCharacterSpawnSpec(
                    characterId: 1001,
                    teamId: CharacterTestArenaMatchConfig.Team1Id,
                    x: 0f,
                    y: 1f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled,
                    runtimeAiBehaviorSelection: new CharacterTestRuntimeAiBehaviorSelection(
                        10, 20, 30, 1)),
                new CharacterTestCharacterSpawnSpec(
                    characterId: 1001,
                    teamId: CharacterTestArenaMatchConfig.Team2Id,
                    x: 0f,
                    y: 1f,
                    z: 4f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled,
                    runtimeAiBehaviorSelection: new CharacterTestRuntimeAiBehaviorSelection(
                        11, 21, 31, 1))
            };
            CharacterTestRuntimeAiPlannerProfileLoadResult load =
                CharacterTestRuntimeAiPlannerProfileLoadResult.Loaded(null, "test", null);
            CharacterTestRuntimeAiPlannerProfile behaviorProfile =
                CharacterTestRuntimeAiPlannerProfileSource.CreateDefault();
            var behaviorBinding = new CharacterTestRuntimeAiPlannerProfileBinding(
                1,
                "behavior-loadout:test",
                behaviorProfile,
                new CharacterTestRuntimeAiPlannerValidationReport(null));

            CharacterTestRuntimeAiPlannerProfileBinding[] bindings =
                CharacterTestRuntimeSessionBuilder.ReservePlayerBehaviorDeckProfileSlots(
                    sourceSpecs,
                    load,
                    Array.Empty<CharacterTestRuntimeAiPlannerProfileBinding>(),
                    out CharacterTestCharacterSpawnSpec[] assigned,
                    new[] { behaviorBinding });

            Assert.AreEqual(2, assigned[0].RuntimeAiPlannerProfileSlot);
            Assert.AreEqual(2, assigned[0].RuntimeAiBehaviorSelection.ProfileSlot);
            Assert.AreEqual(1, assigned[1].RuntimeAiPlannerProfileSlot);
            Assert.AreEqual(1, assigned[1].RuntimeAiBehaviorSelection.ProfileSlot);
            Assert.AreEqual(1, bindings.Length);
            Assert.AreEqual(2, bindings[0].Slot);
            Assert.AreSame(behaviorProfile, bindings[0].Profile);
            Assert.AreEqual("behavior-loadout:test", bindings[0].SourcePath);
        }

        [Test]
        public void SessionBuilder_PlayerDeckProfileReservationKeepsDistinctSameCharacterLoadoutsIndependent()
        {
            var sourceSpecs = new[]
            {
                new CharacterTestCharacterSpawnSpec(
                    characterId: 1001,
                    teamId: CharacterTestArenaMatchConfig.Team1Id,
                    x: -1f,
                    y: 1f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled,
                    runtimeAiBehaviorSelection: new CharacterTestRuntimeAiBehaviorSelection(
                        10, 20, 30, 1)),
                new CharacterTestCharacterSpawnSpec(
                    characterId: 1001,
                    teamId: CharacterTestArenaMatchConfig.Team1Id,
                    x: 1f,
                    y: 1f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.AiControlled,
                    runtimeAiBehaviorSelection: new CharacterTestRuntimeAiBehaviorSelection(
                        11, 21, 31, 2))
            };
            CharacterTestRuntimeAiPlannerProfileLoadResult load =
                CharacterTestRuntimeAiPlannerProfileLoadResult.Loaded(null, "test", null);
            CharacterTestRuntimeAiPlannerProfile firstProfile =
                CharacterTestRuntimeAiPlannerProfileSource.CreateDefault();
            CharacterTestRuntimeAiPlannerProfile secondProfile =
                CharacterTestRuntimeAiPlannerProfileSource.CreateDefault();
            var behaviorBindings = new[]
            {
                new CharacterTestRuntimeAiPlannerProfileBinding(
                    1,
                    "behavior-loadout:first",
                    firstProfile,
                    new CharacterTestRuntimeAiPlannerValidationReport(null)),
                new CharacterTestRuntimeAiPlannerProfileBinding(
                    2,
                    "behavior-loadout:second",
                    secondProfile,
                    new CharacterTestRuntimeAiPlannerValidationReport(null))
            };

            CharacterTestRuntimeAiPlannerProfileBinding[] bindings =
                CharacterTestRuntimeSessionBuilder.ReservePlayerBehaviorDeckProfileSlots(
                    sourceSpecs,
                    load,
                    Array.Empty<CharacterTestRuntimeAiPlannerProfileBinding>(),
                    out CharacterTestCharacterSpawnSpec[] assigned,
                    behaviorBindings);

            Assert.AreEqual(3, assigned[0].RuntimeAiPlannerProfileSlot);
            Assert.AreEqual(3, assigned[0].RuntimeAiBehaviorSelection.ProfileSlot);
            Assert.AreEqual(10, assigned[0].RuntimeAiBehaviorSelection.LoadoutId);
            Assert.AreEqual(4, assigned[1].RuntimeAiPlannerProfileSlot);
            Assert.AreEqual(4, assigned[1].RuntimeAiBehaviorSelection.ProfileSlot);
            Assert.AreEqual(11, assigned[1].RuntimeAiBehaviorSelection.LoadoutId);
            Assert.AreEqual(2, bindings.Length);
            Assert.AreEqual(3, bindings[0].Slot);
            Assert.AreSame(firstProfile, bindings[0].Profile);
            Assert.AreEqual(4, bindings[1].Slot);
            Assert.AreSame(secondProfile, bindings[1].Profile);
        }

        [Test]
        public void LocalPlayerDeckService_SelectsAndPersistsIndependentCharacterDecks()
        {
            string directory = Path.Combine(Path.GetTempPath(), "mx-player-deck-roster-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.IsTrue(CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    Path.Combine(directory, "deck.json"),
                    new[]
                    {
                        new CharacterTestRuntimeAiPlannerPlayerDeckTarget(2002, 2),
                        new CharacterTestRuntimeAiPlannerPlayerDeckTarget(1001, 1)
                    },
                    out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                    out string error), error);
                Assert.AreEqual(1001, service.SelectedCharacterId);
                Assert.IsTrue(service.Workbench.TryInstall(
                    "bc_3qz33zjzk4").Success);
                Assert.IsTrue(service.TrySelectCharacter(1, out error), error);
                Assert.AreEqual(2002, service.SelectedCharacterId);
                Assert.AreEqual(0, service.Workbench.Deck.Cards.Count);
                Assert.IsTrue(service.TrySelectCharacter(0, out error), error);
                Assert.AreEqual(1, service.Workbench.Deck.Cards.Count);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void LocalPlayerDeckService_MultipleCharacterDecksSurviveDiskRoundtrip()
        {
            string directory = Path.Combine(Path.GetTempPath(), "mx-player-deck-roster-roundtrip-" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(directory, "deck.json");
            Directory.CreateDirectory(directory);
            var targets = new[]
            {
                new CharacterTestRuntimeAiPlannerPlayerDeckTarget(2002, 2, displayName: "守卫"),
                new CharacterTestRuntimeAiPlannerPlayerDeckTarget(1001, 1, displayName: "先锋")
            };
            try
            {
                Assert.IsTrue(CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    filePath,
                    targets,
                    out CharacterTestRuntimeAiPlannerLocalPlayerDeckService first,
                    out string error), error);
                Assert.IsTrue(first.Workbench.TryInstall(
                    "bc_3qz33zjzk4").Success);
                Assert.IsTrue(first.TrySelectCharacter(1, out error), error);
                Assert.IsTrue(first.Workbench.TryInstall(
                    "bc_g6a5fw319a").Success);

                Assert.IsTrue(CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    filePath,
                    targets,
                    out CharacterTestRuntimeAiPlannerLocalPlayerDeckService restored,
                    out error), error);
                Assert.AreEqual("先锋", restored.SelectedCharacterDisplayName);
                Assert.AreEqual(
                    "bc_3qz33zjzk4",
                    restored.Workbench.Deck.Cards[0].StableCardId);
                Assert.IsTrue(restored.TrySelectCharacter(1, out error), error);
                Assert.AreEqual("守卫", restored.SelectedCharacterDisplayName);
                Assert.AreEqual(
                    "bc_g6a5fw319a",
                    restored.Workbench.Deck.Cards[0].StableCardId);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void BehaviorDeckPanel_PreviewsConfirmsAndUndoesRecommendedPreset()
        {
            string directory = Path.Combine(Path.GetTempPath(), "mx-player-deck-preset-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.IsTrue(CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    Path.Combine(directory, "deck.json"),
                    1001,
                out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                out string error), error);
                var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(service);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                    new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                    {
                        StableCardId = "bc_3qz33zjzk4",
                        Action = CharacterTestFairyGuiBehaviorDeckCardAction.Install,
                        Origin = CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                        ActionRole = CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary
                    }));
                Assert.IsTrue(sink.OperationFeedback.Visible);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviewRecommendedPreset,
                    null));

                Assert.IsNotNull(sink.PresetPreview);
                Assert.AreEqual(1, service.Workbench.Deck.SlotCount);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ConfirmRecommendedPreset,
                    null));

                Assert.AreEqual(2, service.Workbench.Deck.SlotCount);
                Assert.AreEqual(0, service.Workbench.Deck.TotalCost);
                Assert.IsFalse(sink.OperationFeedback.Visible);
                Assert.IsNull(sink.PresetPreview);
                Assert.AreEqual(
                    "bc_3qz33zjzk4",
                    service.Workbench.Deck.Cards[0].StableCardId);
                Assert.AreEqual(
                    "bc_g6a5fw319a",
                    service.Workbench.Deck.Cards[1].StableCardId);
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.UndoRecommendedPreset,
                    null));

                Assert.AreEqual(1, service.Workbench.Deck.SlotCount);
                Assert.AreEqual(
                    "bc_3qz33zjzk4",
                    service.Workbench.Deck.Cards[0].StableCardId);
                Assert.IsFalse(service.Workbench.CanUndoLastPreset);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void BehaviorDeckPanel_PresetPreviewIsModalAndCancelDoesNotMutateDeck()
        {
            string directory = Path.Combine(Path.GetTempPath(), "mx-player-deck-preset-modal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.IsTrue(CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    Path.Combine(directory, "deck.json"),
                    1001,
                    out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                    out string error), error);
                int continueCount = 0;
                var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(
                    service,
                    continueToSetup: () =>
                    {
                        continueCount++;
                        return true;
                    });
                long sourceHash = service.Workbench.Deck.DeckHash;

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviewRecommendedPreset,
                    null));
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ContinueToSetup,
                    null));

                Assert.IsNotNull(sink.PresetPreview);
                Assert.AreEqual(0, continueCount);
                Assert.AreEqual(sourceHash, service.Workbench.Deck.DeckHash);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CancelRecommendedPreset,
                    null));

                Assert.IsNull(sink.PresetPreview);
                Assert.AreEqual(sourceHash, service.Workbench.Deck.DeckHash);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void BehaviorDeckPanel_LockAfterPreviewRejectsConfirmAndSelectsEnabledExitFocus()
        {
            string directory = Path.Combine(Path.GetTempPath(), "mx-player-deck-preset-lock-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.IsTrue(CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    Path.Combine(directory, "deck.json"),
                    1001,
                    out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                    out string error), error);
                var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(
                    service,
                    continueToSetup: () => true);
                long sourceHash = service.Workbench.Deck.DeckHash;

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviewRecommendedPreset,
                    null));
                service.Workbench.LockSession();
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ConfirmRecommendedPreset,
                    null));

                Assert.IsNull(sink.PresetPreview);
                Assert.AreEqual(sourceHash, service.Workbench.Deck.DeckHash);
                Assert.AreEqual(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ContinueToSetup,
                    sink.PresetExitFocusTargetId);
                Assert.IsFalse(sink.CanPreviewRecommendedPreset);
                Assert.IsTrue(sink.CanContinueToSetup);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void BehaviorDeckPanel_ContinueToSetupUsesSingleShellCommand()
        {
            int continueCount = 0;
            var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(
                (CharacterTestRuntimeAiPlannerPlayerDeckWorkbench)null,
                continueToSetup: () =>
                {
                    continueCount++;
                    return true;
                });

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ContinueToSetup,
                null));

            Assert.AreEqual(1, continueCount);
            Assert.AreEqual("已进入实验设置", sink.LastStatusMessage);
            Assert.IsTrue(sink.CanContinueToSetup);
        }

        [Test]
        public void BehaviorDeckPanel_RetainedSessionUsesReturnToDebugSemantics()
        {
            int continueCount = 0;
            var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(
                (CharacterTestRuntimeAiPlannerPlayerDeckWorkbench)null,
                continueToSetup: () =>
                {
                    continueCount++;
                    return true;
                },
                hasActiveRuntimeAiLabSession: () => true);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ContinueToSetup,
                null));

            Assert.AreEqual(1, continueCount);
            Assert.AreEqual("已返回调试", sink.LastStatusMessage);
            Assert.IsTrue(sink.HasActiveRuntimeAiLabSession);
        }

        [Test]
        public void LocalPlayerDeckService_PresentationTargetIgnoresEnemyAndEntityOrder()
        {
            string directory = Path.Combine(Path.GetTempPath(), "mx-player-deck-presentation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.IsTrue(CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    Path.Combine(directory, "deck.json"),
                    1001,
                    out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                    out string error), error);
                CharacterTestCharacterEntity enemy = CreateCharacterEntity(
                    1, 1001, CharacterTestCharacterControlOwner.AiControlled, teamId: 2);
                CharacterTestCharacterEntity allyLater = CreateCharacterEntity(
                    9, 1001, CharacterTestCharacterControlOwner.AiControlled, teamId: 1);
                CharacterTestCharacterEntity allyObserved = CreateCharacterEntity(
                    4, 1001, CharacterTestCharacterControlOwner.AiControlled, teamId: 1);

                Assert.IsTrue(service.TryResolvePresentationEntityId(
                    new[] { enemy, allyLater, allyObserved }, out int first));
                Assert.IsTrue(service.TryResolvePresentationEntityId(
                    new[] { allyObserved, allyLater, enemy }, out int reversed));
                Assert.AreEqual(4, first);
                Assert.AreEqual(first, reversed);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void BehaviorDeckPanel_ProjectsWorkbenchAndAppliesOneClickInstallRemove()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 2),
                Publish("card.bravo", 1, 3));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult empty = Build(
                catalog,
                "character.grunt",
                Array.Empty<string>());
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha", "card.bravo" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    empty.Deck);
            var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(created.Workbench);

            CharacterTestFairyGuiBehaviorDeckPanelViewModel initial =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.View,
                    sink.PageIndex,
                    sink.ReplacementSourceStableCardId,
                    sink.LastStatusMessage);
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = "card.alpha",
                    Action = CharacterTestFairyGuiBehaviorDeckCardAction.Install
                }));
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = "card.alpha",
                    Action = CharacterTestFairyGuiBehaviorDeckCardAction.Install
                }));
            string duplicateStatus = sink.LastStatusMessage;
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = "card.internal-missing",
                    Action = CharacterTestFairyGuiBehaviorDeckCardAction.Install
                }));
            string missingStatus = sink.LastStatusMessage;
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = "card.alpha",
                    Action = CharacterTestFairyGuiBehaviorDeckCardAction.Remove
                }));
            created.Workbench.LockSession();
            CharacterTestFairyGuiBehaviorDeckPanelViewModel locked =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.Workbench.View,
                    sink.PageIndex,
                    sink.ReplacementSourceStableCardId,
                    sink.LastStatusMessage);

            Assert.IsTrue(created.Success, Describe(created.Diagnostics));
            Assert.AreEqual("行为卡组", initial.Title);
            Assert.AreEqual(CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCardCount, initial.LibraryCards.Count);
            Assert.AreEqual(CharacterTestFairyGuiBehaviorDeckPanelIds.DeckCardCount, initial.DeckCards.Count);
            StringAssert.Contains("警告", initial.DiagnosticsText);
            Assert.AreEqual(0, created.Workbench.View.SlotCount);
            Assert.AreEqual("已移除行为卡", sink.LastStatusMessage);
            Assert.AreEqual("这张行为卡已经在卡组中", duplicateStatus);
            StringAssert.DoesNotContain("card.alpha", duplicateStatus);
            Assert.AreEqual("找不到这张行为卡，请刷新卡库后重试", missingStatus);
            StringAssert.DoesNotContain("card.internal-missing", missingStatus);
            Assert.AreEqual("战斗会话已锁定", locked.LockStatusText);
            Assert.IsFalse(locked.LibraryCards[0].ActionEnabled);
            Assert.AreEqual("安装", locked.LibraryCards[0].ActionText);
            Assert.IsNotEmpty(locked.LibraryCards[0].BlockReasonText);
        }

        [Test]
        public void BehaviorDeckPanel_RuntimeViewContractHasUniqueCommandIds()
        {
            MxUiViewContract contract =
                CharacterTestFairyGuiBehaviorDeckPanelHost.CreateRuntimeViewContractForTests();
            var commandIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < contract.Commands.Count; i++)
            {
                Assert.IsTrue(
                    commandIds.Add(contract.Commands[i].CommandId),
                    "Duplicate runtime command id: " + contract.Commands[i].CommandId);
            }

            Assert.IsTrue(
                commandIds.Contains(
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ToggleInstallSelection));
        }

        [Test]
        public void BehaviorDeckPanel_AddModeUsesNumericCostsAndExitsAfterInstall()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 3),
                Publish("card.locked", 1, 2));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult empty = Build(
                catalog,
                "character.grunt",
                Array.Empty<string>());
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    empty.Deck);
            var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(created.Workbench);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ToggleInstallSelection,
                null));
            CharacterTestFairyGuiBehaviorDeckPanelViewModel selecting =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.Workbench.View,
                    sink.PageIndex,
                    sink.ReplacementSourceStableCardId,
                    sink.LastStatusMessage,
                    installSelectionActive: sink.InstallSelectionActive);

            Assert.IsTrue(sink.InstallSelectionActive);
            Assert.IsTrue(selecting.InstallSelectionActive);
            Assert.IsTrue(selecting.ShowAddCard);
            Assert.AreEqual("0/6", selecting.BudgetText);
            Assert.AreEqual("0/10", selecting.CostBudgetText);
            Assert.AreEqual("3", FindUiCard(selecting.LibraryCards, "card.alpha").CostText);
            Assert.AreEqual(
                CharacterTestFairyGuiBehaviorDeckCardState.Available,
                FindUiCard(selecting.LibraryCards, "card.alpha").State);
            Assert.AreEqual(
                CharacterTestFairyGuiBehaviorDeckCardState.Locked,
                FindUiCard(selecting.LibraryCards, "card.locked").State);
            Assert.IsNotEmpty(FindUiCard(selecting.LibraryCards, "card.locked").BlockReasonText);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = "card.alpha",
                    Action = CharacterTestFairyGuiBehaviorDeckCardAction.Install,
                    Origin = CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                    ActionRole = CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary,
                    SlotIndex = -1
                }));

            Assert.IsFalse(sink.InstallSelectionActive);
            Assert.AreEqual(1, created.Workbench.View.SlotCount);
            CharacterTestFairyGuiBehaviorDeckPanelViewModel installed =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.Workbench.View,
                    sink.PageIndex,
                    sink.ReplacementSourceStableCardId,
                    sink.LastStatusMessage,
                    installSelectionActive: sink.InstallSelectionActive);
            Assert.AreEqual("1/6", installed.BudgetText);
            Assert.AreEqual("3/10", installed.CostBudgetText);
            Assert.AreEqual(
                CharacterTestFairyGuiBehaviorDeckCardState.Installed,
                installed.DeckCards[0].State);

            created.Workbench.LockSession();
            CharacterTestFairyGuiBehaviorDeckPanelViewModel locked =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.Workbench.View,
                    sink.PageIndex,
                    sink.ReplacementSourceStableCardId,
                    sink.LastStatusMessage,
                    installSelectionActive: true);
            Assert.IsFalse(locked.ShowAddCard);
        }

        [Test]
        public void BehaviorDeckPanel_InstallFeedbackTracksAuthoritativeDeltaAndExpiresOnce()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 3));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult empty = Build(
                catalog,
                "character.grunt",
                Array.Empty<string>());
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    empty.Deck);
            double now = 100d;
            var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(
                created.Workbench,
                unscaledTimeProvider: () => now);
            var payload = new CharacterTestFairyGuiBehaviorDeckCardActionPayload
            {
                StableCardId = "card.alpha",
                Action = CharacterTestFairyGuiBehaviorDeckCardAction.Install,
                Origin = CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                ActionRole = CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary,
                SlotIndex = -1
            };

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                payload));

            CharacterTestBehaviorDeckOperationFeedback feedback = sink.OperationFeedback;
            Assert.IsTrue(feedback.Visible);
            Assert.AreEqual(CharacterTestBehaviorDeckOperationOutcome.Success, feedback.Outcome);
            Assert.AreEqual(0, feedback.BeforeSlotCount);
            Assert.AreEqual(1, feedback.AfterSlotCount);
            Assert.AreEqual(0, feedback.BeforeCost);
            Assert.AreEqual(3, feedback.AfterCost);
            Assert.AreEqual(0, feedback.InstalledSlotIndex);
            Assert.AreEqual(CharacterTestFairyGuiBehaviorDeckCardOrigin.Library, sink.FocusIntent.Origin);
            Assert.AreEqual(CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary, sink.FocusIntent.ActionRole);
            Assert.AreEqual("card.alpha", sink.FocusIntent.StableCardId);

            CharacterTestFairyGuiBehaviorDeckPanelViewModel visible =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.Workbench.View,
                    sink.PageIndex,
                    sink.ReplacementSourceStableCardId,
                    sink.LastStatusMessage,
                    feedback: sink.OperationFeedback,
                    focusIntent: sink.FocusIntent,
                    feedbackVersion: sink.FeedbackVersion);
            string visibleSignature = CharacterTestFairyGuiBehaviorDeckPanelViewModelSignature.Create(visible);
            Assert.IsTrue(visible.FeedbackVisible);
            Assert.IsTrue(visible.BudgetDeltaVisible);
            Assert.AreEqual(CharacterTestFairyGuiBehaviorDeckFeedbackTone.Success, visible.BudgetDeltaTone);
            Assert.IsTrue(FindUiCard(visible.LibraryCards, "card.alpha").IsOperationTarget);
            Assert.IsTrue(visible.DeckCards[0].IsOperationTarget);
            StringAssert.Contains("1", visible.DeckCards[0].SlotText);

            long visibleVersion = sink.FeedbackVersion;
            now = feedback.ExpiresAtUnscaledSeconds;
            Assert.IsTrue(sink.ReconcileOperationFeedback());
            Assert.AreEqual(visibleVersion + 1, sink.FeedbackVersion);
            Assert.IsEmpty(sink.LastStatusMessage);
            CharacterTestFairyGuiBehaviorDeckPanelViewModel expired =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.Workbench.View,
                    sink.PageIndex,
                    sink.ReplacementSourceStableCardId,
                    sink.LastStatusMessage,
                    feedback: sink.OperationFeedback,
                    focusIntent: sink.FocusIntent,
                    feedbackVersion: sink.FeedbackVersion);
            Assert.IsFalse(expired.FeedbackVisible);
            Assert.AreNotEqual(
                visibleSignature,
                CharacterTestFairyGuiBehaviorDeckPanelViewModelSignature.Create(expired));
            long expiredVersion = sink.FeedbackVersion;
            Assert.IsFalse(sink.ReconcileOperationFeedback());
            Assert.AreEqual(expiredVersion, sink.FeedbackVersion);
        }

        [Test]
        public void BehaviorDeckPanel_FeedbackClearsWhenAuthoritativeViewIdentityChanges()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 3));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult empty = Build(
                catalog,
                "character.grunt",
                Array.Empty<string>());
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    empty.Deck);
            var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(
                created.Workbench,
                unscaledTimeProvider: () => 10d);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = "card.alpha",
                    Action = CharacterTestFairyGuiBehaviorDeckCardAction.Install,
                    Origin = CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                    ActionRole = CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary
                }));

            Assert.IsTrue(sink.OperationFeedback.Visible);
            long visibleVersion = sink.FeedbackVersion;
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView feedbackView = sink.View;
            Assert.IsFalse(sink.ReconcileOperationFeedback());
            Assert.AreSame(feedbackView, sink.View);

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult locked =
                created.Workbench.LockSession();
            Assert.IsTrue(locked.Success);
            Assert.AreNotSame(feedbackView, sink.View);
            Assert.IsTrue(sink.ReconcileOperationFeedback());
            Assert.IsFalse(sink.OperationFeedback.Visible);
            Assert.AreEqual(visibleVersion + 1, sink.FeedbackVersion);
            Assert.IsEmpty(sink.LastStatusMessage);
            Assert.IsFalse(sink.ReconcileOperationFeedback());
        }

        [Test]
        public void BehaviorDeckPanel_CloseClearsTransientFeedbackAndFocusIntent()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 2));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult empty = Build(
                catalog,
                "character.grunt",
                Array.Empty<string>());
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    empty.Deck);
            bool closed = false;
            var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(
                created.Workbench,
                closePanel: () => closed = true,
                unscaledTimeProvider: () => 10d);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = "card.alpha",
                    Action = CharacterTestFairyGuiBehaviorDeckCardAction.Install,
                    Origin = CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                    ActionRole = CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary
                }));
            Assert.IsTrue(sink.OperationFeedback.Visible);
            Assert.Greater(sink.FocusIntent.Sequence, 0);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.Close,
                null));

            Assert.IsTrue(closed);
            Assert.IsFalse(sink.OperationFeedback.Visible);
            Assert.AreEqual(0, sink.FocusIntent.Sequence);
        }

        [Test]
        public void BehaviorDeckPanel_DiagnosticsPrioritizeSeverityAndPreservePeerOrder()
        {
            var diagnostics = new[]
            {
                new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Info,
                    "INSTALLED_DECK_DAMAGE_MISSING",
                    "$.info",
                    "info"),
                new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning,
                    "INSTALLED_DECK_FALLBACK_ONLY",
                    "$.warning.first",
                    "warning-first"),
                new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                    "PLAYER_DECK_CARD_UNAVAILABLE",
                    "$.error",
                    "error"),
                new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning,
                    "INSTALLED_DECK_TARGETING_MISSING",
                    "$.warning.second",
                    "warning-second")
            };

            string text = CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder
                .FormatDiagnosticsForTests(diagnostics);

            int errorIndex = text.IndexOf("错误：", StringComparison.Ordinal);
            int firstWarningIndex = text.IndexOf("当前卡组仅依靠", StringComparison.Ordinal);
            int secondWarningIndex = text.IndexOf("当前卡组缺少", StringComparison.Ordinal);
            Assert.GreaterOrEqual(errorIndex, 0);
            Assert.Greater(firstWarningIndex, errorIndex);
            Assert.Greater(secondWarningIndex, firstWarningIndex);
            StringAssert.Contains("另有 1 项", text);
            StringAssert.DoesNotContain("提示：", text);
        }

        [Test]
        public void BehaviorDeckPanel_BinderFocusIntentCoversInstallRemoveFailureAndOffPage()
        {
            var libraryPrimary = new CharacterTestFairyGuiBehaviorDeckCardViewModel
            {
                StableCardId = "card.alpha",
                Visible = true,
                Action = CharacterTestFairyGuiBehaviorDeckCardAction.Remove,
                ActionEnabled = true,
                SecondaryAction = CharacterTestFairyGuiBehaviorDeckCardAction.BeginReplacement,
                SecondaryActionVisible = true,
                SecondaryActionEnabled = true
            };
            var deckPrimary = new CharacterTestFairyGuiBehaviorDeckCardViewModel
            {
                StableCardId = "card.alpha",
                Visible = true,
                Action = CharacterTestFairyGuiBehaviorDeckCardAction.Remove,
                ActionEnabled = true
            };
            var viewModel = new CharacterTestFairyGuiBehaviorDeckPanelViewModel
            {
                FocusRequestSequence = 1,
                FocusOrigin = CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                FocusStableCardId = "card.alpha",
                FocusActionRole = CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary,
                FocusAction = CharacterTestFairyGuiBehaviorDeckCardAction.Install
            };
            viewModel.SetLibraryCards(new[] { libraryPrimary });
            viewModel.SetDeckCards(new[] { deckPrimary });

            Assert.AreEqual(
                "libraryCard0/btnAction",
                CharacterTestFairyGuiBehaviorDeckPanelBinder.ResolveFocusIntentPathForTests(viewModel));

            viewModel.FocusOrigin = CharacterTestFairyGuiBehaviorDeckCardOrigin.InstalledSlot;
            viewModel.FocusAction = CharacterTestFairyGuiBehaviorDeckCardAction.Remove;
            Assert.AreEqual(
                "libraryCard0/btnAction",
                CharacterTestFairyGuiBehaviorDeckPanelBinder.ResolveFocusIntentPathForTests(viewModel));

            viewModel.FocusOrigin = CharacterTestFairyGuiBehaviorDeckCardOrigin.Library;
            viewModel.FocusAction = CharacterTestFairyGuiBehaviorDeckCardAction.BeginReplacement;
            viewModel.FocusActionRole = CharacterTestFairyGuiBehaviorDeckCardActionRole.Secondary;
            Assert.AreEqual(
                "libraryCard0/btnAction",
                CharacterTestFairyGuiBehaviorDeckPanelBinder.ResolveFocusIntentPathForTests(viewModel));

            viewModel.FocusAction = CharacterTestFairyGuiBehaviorDeckCardAction.Remove;
            Assert.AreEqual(
                "libraryCard0/btnSecondaryAction",
                CharacterTestFairyGuiBehaviorDeckPanelBinder.ResolveFocusIntentPathForTests(viewModel));

            viewModel.FocusStableCardId = "card.off-page";
            Assert.AreEqual(
                CharacterTestFairyGuiBehaviorDeckPanelIds.DeckHeader,
                CharacterTestFairyGuiBehaviorDeckPanelBinder.ResolveFocusIntentPathForTests(viewModel));
        }

        [Test]
        public void BehaviorDeckPanel_RejectedCommandKeepsPersistentDiagnosticsSeparateFromFeedback()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.locked", 1, 2));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult empty = Build(
                catalog,
                "character.grunt",
                Array.Empty<string>());
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    Array.Empty<string>());
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    empty.Deck);
            double now = 20d;
            var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(
                created.Workbench,
                unscaledTimeProvider: () => now);
            CharacterTestFairyGuiBehaviorDeckPanelViewModel blocked =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.View,
                    0,
                    string.Empty,
                    string.Empty);
            CharacterTestFairyGuiBehaviorDeckCardViewModel blockedCard =
                FindUiCard(blocked.LibraryCards, "card.locked");

            Assert.IsFalse(blockedCard.ActionEnabled);
            Assert.IsNotEmpty(blockedCard.BlockReasonText);
            Assert.IsEmpty(blockedCard.InlineFeedbackText);
            Assert.IsFalse(blocked.FeedbackVisible);
            AssertDiagnostic(created.View.DeckDiagnostics, "INSTALLED_DECK_FALLBACK_ONLY");

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = "card.locked",
                    Action = CharacterTestFairyGuiBehaviorDeckCardAction.Install,
                    Origin = CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                    ActionRole = CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary
                }));

            Assert.IsFalse(sink.OperationFeedback.Outcome == CharacterTestBehaviorDeckOperationOutcome.Success);
            Assert.AreEqual("PLAYER_DECK_CARD_NOT_UNLOCKED", sink.OperationFeedback.DiagnosticCode);
            AssertDiagnostic(created.Workbench.View.DeckDiagnostics, "INSTALLED_DECK_FALLBACK_ONLY");
            CharacterTestFairyGuiBehaviorDeckPanelViewModel rejected =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.Workbench.View,
                    0,
                    string.Empty,
                    sink.LastStatusMessage,
                    feedback: sink.OperationFeedback,
                    focusIntent: sink.FocusIntent,
                    feedbackVersion: sink.FeedbackVersion);
            CharacterTestFairyGuiBehaviorDeckCardViewModel rejectedCard =
                FindUiCard(rejected.LibraryCards, "card.locked");
            Assert.IsTrue(rejected.FeedbackVisible);
            Assert.AreEqual(CharacterTestFairyGuiBehaviorDeckFeedbackTone.Error, rejected.BudgetDeltaTone);
            Assert.IsNotEmpty(rejectedCard.BlockReasonText);
            Assert.IsNotEmpty(rejectedCard.InlineFeedbackText);
            StringAssert.Contains("警告", rejected.DiagnosticsText);
        }

        [Test]
        public void BehaviorDeckPanel_RemoveFeedbackReturnsCardToLibraryAndPreservesOriginIntent()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 3));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult installed = Build(
                catalog,
                "character.grunt",
                new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.alpha" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    installed.Deck);
            var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(
                created.Workbench,
                unscaledTimeProvider: () => 10d);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = "card.alpha",
                    Action = CharacterTestFairyGuiBehaviorDeckCardAction.Remove,
                    Origin = CharacterTestFairyGuiBehaviorDeckCardOrigin.InstalledSlot,
                    ActionRole = CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary,
                    SlotIndex = 0
                }));

            Assert.AreEqual(CharacterTestBehaviorDeckOperationOutcome.Success, sink.OperationFeedback.Outcome);
            Assert.AreEqual(1, sink.OperationFeedback.BeforeSlotCount);
            Assert.AreEqual(0, sink.OperationFeedback.AfterSlotCount);
            Assert.AreEqual(3, sink.OperationFeedback.BeforeCost);
            Assert.AreEqual(0, sink.OperationFeedback.AfterCost);
            Assert.AreEqual(-1, sink.OperationFeedback.InstalledSlotIndex);
            Assert.AreEqual(CharacterTestFairyGuiBehaviorDeckCardOrigin.InstalledSlot, sink.FocusIntent.Origin);
            Assert.AreEqual(0, created.Workbench.View.SlotCount);

            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.Workbench.View,
                    0,
                    string.Empty,
                    sink.LastStatusMessage,
                    feedback: sink.OperationFeedback,
                    focusIntent: sink.FocusIntent,
                    feedbackVersion: sink.FeedbackVersion);
            Assert.IsTrue(FindUiCard(viewModel.LibraryCards, "card.alpha").IsOperationTarget);
            Assert.AreEqual(CharacterTestFairyGuiBehaviorDeckCardState.Empty, viewModel.DeckCards[0].State);
        }

        [Test]
        public void BehaviorDeckPanel_CapabilityMismatchDiagnosticHidesStableCardId()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish(
                    "card.internal-support-dependent",
                    1,
                    2,
                    requiredCapabilities: new[]
                    {
                        CharacterTestRuntimeAiPlannerBehaviorCapability.Support
                    }));
            var diagnosticsInput = new CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput(
                characterCapabilities: new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting
                },
                providedDeckCapabilities: new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Engage,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Damage,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Kite,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Defense
                });
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult deck = Build(
                catalog,
                "character.grunt",
                new[] { "card.internal-support-dependent" },
                diagnosticsInput: diagnosticsInput);
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.internal-support-dependent" });
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    deck.Deck,
                    diagnosticsInput);

            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.View,
                    0,
                    string.Empty,
                    string.Empty);

            Assert.IsTrue(created.Success, Describe(created.Diagnostics));
            StringAssert.Contains("角色缺少", viewModel.DiagnosticsText);
            StringAssert.DoesNotContain("card.internal-support-dependent", viewModel.DiagnosticsText);
        }

        [Test]
        public void BehaviorDeckPanel_PaginationStopsAtLastPage()
        {
            var published = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard[9];
            var unlockedIds = new string[published.Length];
            for (int i = 0; i < published.Length; i++)
            {
                string stableCardId = "card.page-" + i;
                published[i] = Publish(stableCardId, 1, 1);
                unlockedIds[i] = stableCardId;
            }
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(published);
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult empty = Build(
                catalog,
                "character.grunt",
                Array.Empty<string>());
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(unlockedIds);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    empty.Deck);
            var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(created.Workbench);
            var next = new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.NextPage,
                null);

            sink.Enqueue(next);
            sink.Enqueue(next);
            sink.Enqueue(next);

            Assert.AreEqual(1, sink.PageIndex);
            CharacterTestFairyGuiBehaviorDeckPanelViewModel page =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.View,
                    sink.PageIndex,
                    sink.ReplacementSourceStableCardId,
                    sink.LastStatusMessage);
            Assert.AreEqual("2 / 2", page.PageStatusText);
            Assert.IsFalse(page.CanNextPage);
        }

        [Test]
        public void BehaviorDeckPanel_ReplacesUnavailableInstalledReference()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.replacement", 1, 2));
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.replacement", "card.retired" });
            var persisted = new CharacterTestRuntimeAiPlannerPersistedDeck(
                "character.grunt",
                new[] { "card.retired" },
                1);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    persisted,
                    runtimeContractVersion: 1);
            var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(created.Workbench);
            CharacterTestFairyGuiBehaviorDeckPanelViewModel initial =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.View,
                    0,
                    string.Empty,
                    string.Empty);

            Assert.AreEqual(
                CharacterTestFairyGuiBehaviorDeckCardAction.Remove,
                FindUiCard(initial.LibraryCards, "card.retired").Action);
            Assert.AreEqual(
                CharacterTestFairyGuiBehaviorDeckCardAction.BeginReplacement,
                FindUiCard(initial.LibraryCards, "card.retired").SecondaryAction);
            Assert.AreEqual("未知行为卡", FindUiCard(initial.LibraryCards, "card.retired").Title);
            Assert.AreEqual("未知行为卡", initial.DeckCards[0].Title);
            StringAssert.DoesNotContain("card.retired", initial.DiagnosticsText);
            Assert.AreEqual(
                CharacterTestFairyGuiBehaviorDeckCardAction.Remove,
                initial.DeckCards[0].Action);
            Assert.AreEqual(
                CharacterTestFairyGuiBehaviorDeckCardAction.BeginReplacement,
                initial.DeckCards[0].SecondaryAction);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = "card.retired",
                    Action = CharacterTestFairyGuiBehaviorDeckCardAction.BeginReplacement
                }));
            Assert.AreEqual("card.retired", sink.ReplacementSourceStableCardId);
            CharacterTestFairyGuiBehaviorDeckPanelViewModel choosing =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.Workbench.View,
                    0,
                    sink.ReplacementSourceStableCardId,
                    sink.LastStatusMessage);
            Assert.AreEqual(
                CharacterTestFairyGuiBehaviorDeckCardAction.CancelReplacement,
                FindUiCard(choosing.LibraryCards, "card.retired").Action);
            Assert.AreEqual(
                CharacterTestFairyGuiBehaviorDeckCardAction.Remove,
                FindUiCard(choosing.LibraryCards, "card.retired").SecondaryAction);
            Assert.AreEqual(
                CharacterTestFairyGuiBehaviorDeckCardAction.ChooseReplacement,
                FindUiCard(choosing.LibraryCards, "card.replacement").Action);
            Assert.AreEqual(
                CharacterTestFairyGuiBehaviorDeckCardAction.CancelReplacement,
                choosing.DeckCards[0].Action);
            Assert.AreEqual(
                CharacterTestFairyGuiBehaviorDeckCardAction.Remove,
                choosing.DeckCards[0].SecondaryAction);
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = "card.replacement",
                    Action = CharacterTestFairyGuiBehaviorDeckCardAction.ChooseReplacement
                }));

            Assert.AreEqual(string.Empty, sink.ReplacementSourceStableCardId);
            Assert.AreEqual("已替换行为卡", sink.LastStatusMessage);
            Assert.IsTrue(created.Workbench.View.IsResolved);
            Assert.AreEqual("card.replacement", created.Workbench.Deck.Cards[0].StableCardId);
        }

        [Test]
        public void BehaviorDeckPanel_UnavailableDeckSlotCanBeRemovedWithoutReplacement()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.anchor", 1, 1));
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.retired" });
            var persisted = new CharacterTestRuntimeAiPlannerPersistedDeck(
                "character.grunt",
                new[] { "card.retired" },
                1);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    persisted,
                    runtimeContractVersion: 1);
            var sink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(created.Workbench);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = "card.retired",
                    Action = CharacterTestFairyGuiBehaviorDeckCardAction.Remove
                }));

            Assert.AreEqual("已移除行为卡", sink.LastStatusMessage);
            Assert.AreEqual(string.Empty, sink.ReplacementSourceStableCardId);
            Assert.IsTrue(created.Workbench.View.IsResolved);
            Assert.AreEqual(0, created.Workbench.Deck.SlotCount);
        }

        [Test]
        public void BehaviorDeckPanel_ReplacementCandidatePreviewsCostLimit()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.anchor", 1, 9),
                Publish("card.expensive-replacement", 1, 2));
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                    new[] { "card.anchor", "card.expensive-replacement", "card.retired" });
            var persisted = new CharacterTestRuntimeAiPlannerPersistedDeck(
                "character.grunt",
                new[] { "card.anchor", "card.retired" },
                1);
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult created =
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                    catalog,
                    unlocked.Collection,
                    persisted,
                    runtimeContractVersion: 1);

            CharacterTestFairyGuiBehaviorDeckPanelViewModel choosing =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    created.View,
                    0,
                    "card.retired",
                    string.Empty);
            CharacterTestFairyGuiBehaviorDeckCardViewModel replacement =
                FindUiCard(choosing.LibraryCards, "card.expensive-replacement");

            Assert.IsFalse(replacement.ActionEnabled);
            Assert.AreEqual(CharacterTestFairyGuiBehaviorDeckCardAction.None, replacement.Action);
            Assert.AreEqual("费用不足", replacement.ActionText);
            Assert.AreEqual(9, created.View.KnownCost);
        }

        private static CharacterTestRuntimeAiPlannerInstalledDeckBuildResult Build(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            string characterStableId,
            IEnumerable<string> stableCardIds,
            int runtimeContractVersion = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput = null)
        {
            return CharacterTestRuntimeAiPlannerInstalledDeckRules.ValidateAndCreate(
                new CharacterTestRuntimeAiPlannerInstalledDeckCandidate(characterStableId, stableCardIds, 1),
                catalog,
                runtimeContractVersion,
                diagnosticsInput);
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCard Publish(
            string stableCardId,
            int revision,
            int cost,
            int minimumRuntimeVersion = 1,
            int maximumRuntimeVersion =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorCapability> requiredCapabilities = null)
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
            CharacterTestRuntimeAiLabBehaviorRecipeV5Templates.ConfigureNativeCard(
                draft.LogicPayload,
                stableCardId);
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                stableCardId,
                "ui." + stableCardId,
                cost,
                "Test card",
                "Test use",
                "Test risk",
                requiredCapabilities: requiredCapabilities);
            var request = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                stableCardId,
                revision,
                minimumRuntimeVersion,
                maximumRuntimeVersion,
                metadata);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult result =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    request,
                    CreateReferences());
            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            return result.Card;
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog CreateCatalog(
            params CharacterTestRuntimeAiPlannerPublishedBehaviorCard[] cards)
        {
            return CreateCatalog(1, cards);
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
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            string code)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                    return;
            }
            Assert.Fail("Expected diagnostic " + code + ". Actual: " + Describe(diagnostics));
        }

        private static void AssertDiagnosticSeverity(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            string code,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity expected)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (!string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                    continue;
                Assert.AreEqual(expected, diagnostics[i].Severity);
                return;
            }
            Assert.Fail("Expected diagnostic " + code + ". Actual: " + Describe(diagnostics));
        }

        private static CharacterTestRuntimeAiPlannerInstalledDeckPresetDefinition CreatePreset(
            params string[] stableCardIds)
        {
            return new CharacterTestRuntimeAiPlannerInstalledDeckPresetDefinition(
                1,
                1,
                stableCardIds);
        }

        private static CharacterTestRuntimeAiPlannerPlayerDeckCardView FindCardView(
            IReadOnlyList<CharacterTestRuntimeAiPlannerPlayerDeckCardView> cards,
            string stableCardId)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (string.Equals(cards[i].StableCardId, stableCardId, StringComparison.Ordinal))
                    return cards[i];
            }
            Assert.Fail("Expected player deck card view " + stableCardId + ".");
            return null;
        }

        private static CharacterTestFairyGuiBehaviorDeckCardViewModel FindUiCard(
            IReadOnlyList<CharacterTestFairyGuiBehaviorDeckCardViewModel> cards,
            string stableCardId)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (string.Equals(cards[i].StableCardId, stableCardId, StringComparison.Ordinal))
                    return cards[i];
            }
            Assert.Fail("Expected behavior deck UI card " + stableCardId + ".");
            return null;
        }

        private sealed class TestPersistenceSink : ICharacterTestRuntimeAiPlannerPlayerDeckPersistenceSink
        {
            public TestPersistenceSink(bool success)
            {
                Success = success;
            }

            public bool Success { get; set; }
            public int CallCount { get; private set; }
            public CharacterTestRuntimeAiPlannerPersistedDeck LastDeck { get; private set; }

            public bool TryPersist(
                CharacterTestRuntimeAiPlannerPersistedDeck deck,
                out CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic diagnostic)
            {
                CallCount++;
                LastDeck = deck;
                diagnostic = Success
                    ? null
                    : new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                        CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                        "TEST_PERSISTENCE_REJECTED",
                        "$.persistence",
                        "Test persistence rejected the candidate.");
                return Success;
            }
        }

        private static CharacterTestCharacterEntity CreateCharacterEntity(
            int entityId,
            int characterId,
            CharacterTestCharacterControlOwner owner,
            int teamId = 1)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(entityId),
                new CharacterTestCharacterIdentityComponent(characterId, "Entity " + entityId, default, default),
                new CharacterTestCharacterTeamComponent(teamId, teamId),
                new CharacterTestCharacterTransformComponent(0f, 0f, 0f),
                new CharacterTestCharacterEquipmentComponent(useDefaultLoadout: false),
                new CharacterTestCharacterControlComponent(owner),
                new GameplayEntityId(entityId, 1));
        }

        private static string Describe(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            var lines = new List<string>();
            for (int i = 0; i < diagnostics.Count; i++)
            {
                lines.Add(
                    diagnostics[i].Severity
                    + " "
                    + diagnostics[i].Code
                    + " "
                    + diagnostics[i].Path
                    + " "
                    + diagnostics[i].Message);
            }
            return string.Join(" | ", lines);
        }

        private static string Describe(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            var lines = new List<string>();
            for (int i = 0; i < diagnostics.Count; i++)
                lines.Add(diagnostics[i].Code + " " + diagnostics[i].Path + " " + diagnostics[i].Message);
            return string.Join(" | ", lines);
        }
    }
}

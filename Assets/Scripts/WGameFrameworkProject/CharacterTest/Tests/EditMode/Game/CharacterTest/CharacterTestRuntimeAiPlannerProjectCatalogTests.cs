using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiPlannerProjectCatalogTests
    {
        [Test]
        public void FoundationCatalog_ContainsOnlyApproachAttackAndIdleStableCards()
        {
            CharacterTestRuntimeAiPlannerProjectCatalogSeedResult seed =
                CharacterTestRuntimeAiPlannerProjectCatalogSeed.Create();
            Assert.That(seed.Success, Is.True, seed.Diagnostic);

            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < seed.Cards.Cards.Count; i++)
                stableIds.Add(seed.Cards.Cards[i].StableCardId);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog
                        .FoundationApproachAndAttack,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle
                },
                stableIds);
        }

        [Test]
        public void FoundationCatalog_LatestCardsHaveAuthoritativeNamesAndLogic()
        {
            CharacterTestRuntimeAiPlannerProjectCatalogSeedResult seed =
                CharacterTestRuntimeAiPlannerProjectCatalogSeed.Create();
            Assert.That(seed.Success, Is.True, seed.Diagnostic);
            int contract =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                    .CurrentRuntimeContractVersion;

            Assert.That(seed.Cards.TryResolveLatestCompatible(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog
                    .FoundationApproachAndAttack,
                contract,
                out CharacterTestRuntimeAiPlannerPublishedBehaviorCard approach), Is.True);
            Assert.That(approach.Metadata.DisplayName, Is.EqualTo("靠近并普攻"));
            Assert.That(CountEvents(approach.CloneLogic()), Is.EqualTo(10));

            Assert.That(seed.Cards.TryResolveLatestCompatible(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                contract,
                out CharacterTestRuntimeAiPlannerPublishedBehaviorCard idle), Is.True);
            Assert.That(idle.Metadata.DisplayName, Is.EqualTo("发呆"));
            Assert.That(CountEvents(idle.CloneLogic()), Is.Zero);
        }

        [Test]
        public void BuiltInDefinitionLibrary_ExposesExactlyTwoFoundationCards()
        {
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog =
                CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn();
            Assert.That(catalog.Definitions.Count, Is.EqualTo(2));
            Assert.That(catalog.Definitions[0].BehaviorId, Is.EqualTo(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog
                    .FoundationApproachAndAttack));
            Assert.That(catalog.Definitions[0].DisplayName, Is.EqualTo("靠近并普攻"));
            Assert.That(catalog.Definitions[1].BehaviorId, Is.EqualTo(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle));
            Assert.That(catalog.Definitions[1].DisplayName, Is.EqualTo("发呆"));
        }

        [Test]
        public void DefaultProfile_IsFilledByExactlyTwoFoundationCards()
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            Assert.That(profile.Entries.Count, Is.EqualTo(2));
            Assert.That(profile.Entries[0].BehaviorId, Is.EqualTo(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog
                    .FoundationApproachAndAttack));
            Assert.That(profile.Entries[1].BehaviorId, Is.EqualTo(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle));
        }

        [Test]
        public void DefaultProfile_RemainsWaitOnlyUntilFoundationRuntimeIsImplemented()
        {
            CharacterTestRuntimeAiPlannerBehaviorCompileResult result =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog
                        .CreateDefaultProfile());
            Assert.That(result.Success, Is.True, result.Report?.ToText());
            Assert.That(result.Profile.Actions.Count, Is.EqualTo(2));
            for (int i = 0; i < result.Profile.Actions.Count; i++)
            {
                Assert.That(
                    result.Profile.Actions[i].Kind,
                    Is.EqualTo(CharacterTestRuntimeAiPlannerActionKind.Wait));
            }
        }

        [Test]
        public void LatestProjectGroup_PinsOnlyTheTwoFoundationCards()
        {
            CharacterTestRuntimeAiPlannerProjectCatalogSeedResult seed =
                CharacterTestRuntimeAiPlannerProjectCatalogSeed.Create();
            Assert.That(seed.Success, Is.True, seed.Diagnostic);
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision[] groups =
                seed.Groups.GetLatestRevisions();
            Assert.That(groups.Length, Is.GreaterThan(0));
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision latest = groups[0];
            Assert.That(latest.Cards.Count, Is.EqualTo(2));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog
                        .FoundationApproachAndAttack,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle
                },
                new[]
                {
                    latest.Cards[0].StableCardId,
                    latest.Cards[1].StableCardId
                });
        }

        [Test]
        public void LegacyBehaviorIds_AreMigrationAliasesAndNeverLibraryEntries()
        {
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog =
                CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn();
            Assert.That(catalog.TryGetDefinition(
                "Movement.ApproachTarget",
                out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition approach), Is.True);
            Assert.That(approach.BehaviorId, Is.EqualTo(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog
                    .FoundationApproachAndAttack));
            Assert.That(catalog.TryGetDefinition(
                "Fallback.WaitOrHold",
                out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition idle), Is.True);
            Assert.That(idle.BehaviorId, Is.EqualTo(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle));

            for (int i = 0; i < catalog.Definitions.Count; i++)
            {
                Assert.That(catalog.Definitions[i].BehaviorId, Does.Not.Contain("."));
            }
        }

        private static int CountEvents(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic)
        {
            int count = 0;
            for (int i = 0; logic?.Timelines != null && i < logic.Timelines.Count; i++)
                count += logic.Timelines[i]?.Events.Count ?? 0;
            return count;
        }
    }
}

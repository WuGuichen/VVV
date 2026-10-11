using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.Tests
{
    [TestFixture]
    public sealed class CharacterTestRuntimeAiPlannerBehaviorResponsibilityTests
    {
        [Test]
        public void MissingResponsibilities_AreWarningsAndDoNotBlockValidation()
        {
            var entries = new List<CharacterTestRuntimeAiPlannerBehaviorModuleEntry>();

            Assert.IsTrue(CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.TryValidateAssignments(
                entries,
                CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn(),
                out string message), message);

            CharacterTestRuntimeAiPlannerBehaviorResponsibilityReport[] reports =
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.BuildReports(
                    entries,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            Assert.AreEqual(5, reports.Length);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerBehaviorResponsibilityState.Missing, reports[0].State);
            Assert.IsFalse(reports[0].BlocksRuntime);
        }

        [Test]
        public void LegacyEnsureContract_NeverAddsOrReclassifiesEntries()
        {
            var entries = new List<CharacterTestRuntimeAiPlannerBehaviorModuleEntry>
            {
                Entry("fallback-a", CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback, 10),
                Entry("fallback-b", CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback, 20)
            };

            Assert.IsFalse(CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.EnsureContract(
                entries,
                CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn()));
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback,
                entries[0].ResponsibilityKinds[0]);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback,
                entries[1].ResponsibilityKinds[0]);
        }

        [Test]
        public void BehaviorGroupV4_RoundTripsMultiResponsibilitiesInCanonicalOrder()
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile = CreateProfile();
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry added = Entry(
                "approach-second",
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage,
                30);
            added.SetResponsibilities(new[]
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage
            });
            profile.Entries.Add(added);

            string json = CharacterTestRuntimeAiPlannerBehaviorGroupJson.ToJson(profile);
            StringAssert.Contains("behavior-group.v4", json);
            StringAssert.Contains("responsibilityKinds", json);
            StringAssert.DoesNotContain("slotKind", json);
            Assert.IsTrue(CharacterTestRuntimeAiPlannerBehaviorGroupJson.TryLoad(
                json,
                CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn(),
                out CharacterTestRuntimeAiPlannerBehaviorCompositionProfile loaded,
                out CharacterTestRuntimeAiPlannerValidationReport report),
                report.Diagnostics.Count > 0 ? report.Diagnostics[0].Message : string.Empty);
            Assert.AreEqual(3, loaded.Entries.Count);
            Assert.AreEqual("approach-second", loaded.Entries[2].EntryId);
            CollectionAssert.AreEqual(new[]
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage
            }, loaded.Entries[2].ResponsibilityKinds);
        }

        [Test]
        public void ResponsibilityNormalization_MakesOtherExclusiveAndIgnoresOrdering()
        {
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] normalized =
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.Normalize(new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other,
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage,
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure,
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage
                });
            CollectionAssert.AreEqual(new[]
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage
            }, normalized);
            CollectionAssert.AreEqual(new[]
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
            }, CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.Normalize(
                Array.Empty<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>()));
        }

        [Test]
        public void CapabilityProjection_UsesEveryExplicitResponsibilityAndOtherOnlyAsFallback()
        {
            CollectionAssert.AreEqual(new[]
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage
            }, CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.ResolveCompatibleResponsibilities(
                new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Control,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Damage,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Control
                }));

            CollectionAssert.AreEqual(new[]
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
            }, CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.ResolveCompatibleResponsibilities(
                new[] { CharacterTestRuntimeAiPlannerBehaviorCapability.Defense }));
        }

        [Test]
        public void ResponsibilityChange_ChangesContentHashButNotCompiledRuntimeProfile()
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile engage = CreateProfile();
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile other = engage.Clone();
            other.Entries[1].SetResponsibilities(new[]
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
            });

            CharacterTestRuntimeAiPlannerBehaviorCompileResult engageCompile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(engage);
            CharacterTestRuntimeAiPlannerBehaviorCompileResult otherCompile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(other);
            Assert.IsTrue(engageCompile.Success, FirstDiagnostic(engageCompile.Report));
            Assert.IsTrue(otherCompile.Success, FirstDiagnostic(otherCompile.Report));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerProfileJson.ToJson(engageCompile.Profile),
                CharacterTestRuntimeAiPlannerProfileJson.ToJson(otherCompile.Profile));

            var engageGroup = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                "responsibility.hash.test",
                1,
                "Responsibility hash test",
                string.Empty,
                new string[0],
                0,
                engage,
                new CharacterTestRuntimeAiPlannerBehaviorGroupCardReference[0]);
            var otherGroup = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                "responsibility.hash.test",
                1,
                "Responsibility hash test",
                string.Empty,
                new string[0],
                0,
                other,
                new CharacterTestRuntimeAiPlannerBehaviorGroupCardReference[0]);
            Assert.AreNotEqual(engageGroup.ContentHash, otherGroup.ContentHash);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCompositionProfile CreateProfile()
        {
            var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
                "responsibility.test",
                CharacterTestAiTargetSelectionMode.NearestOpposingTeam,
                1.5f,
                CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault(),
                CharacterTestRuntimeAiPlannerMovementSettings.CreateDefault());
            profile.Entries.Add(Entry(
                "targeting",
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting,
                0));
            profile.Entries.Add(Entry(
                "approach",
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure,
                10));
            return profile;
        }

        private static CharacterTestRuntimeAiLabDraftWorkspace CreateWorkspace(
            out CharacterTestRuntimeAiPlannerProjectCatalogSeedResult seed)
        {
            seed = CharacterTestRuntimeAiPlannerProjectCatalogSeed.Create();
            Assert.IsTrue(seed.Success, seed.Diagnostic);
            var state = new CharacterTestRuntimeAiPlannerProjectCatalogState(
                seed.Cards,
                seed.Groups,
                "behavior_card_catalog.json",
                "behavior_group_catalog.json",
                "cards-hash",
                "groups-hash",
                string.Empty);
            return CharacterTestRuntimeAiLabDraftWorkspace.Import(
                CharacterTestArenaMatchConfig.CreateProjectBacked(state));
        }

        private static CharacterTestRuntimeAiPlannerBehaviorResponsibilityReport FindReport(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityReport[] reports,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind kind)
        {
            for (int i = 0; i < reports.Length; i++)
            {
                if (reports[i].ResponsibilityKind == kind)
                    return reports[i];
            }
            Assert.Fail("Responsibility report was not found: " + kind);
            return default;
        }

        private static int FindEntry(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind kind)
        {
            for (int i = 0; i < group.Entries.Count; i++)
            {
                if (group.Entries[i].ResponsibilityKinds[0] == kind)
                    return i;
            }
            return -1;
        }

        private static int FindEntry(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            string entryId)
        {
            for (int i = 0; i < group.Entries.Count; i++)
            {
                if (string.Equals(group.Entries[i].EntryId, entryId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleEntry Entry(
            string entryId,
            string behaviorId,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility,
            int order)
        {
            var entry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(behaviorId, true, order)
            {
                EntryId = entryId,
            };
            entry.SetResponsibilities(new[] { responsibility });
            return entry;
        }

        private static string FirstDiagnostic(CharacterTestRuntimeAiPlannerValidationReport report)
        {
            return report != null && report.Diagnostics.Count > 0
                ? report.Diagnostics[0].Message
                : string.Empty;
        }
    }
}

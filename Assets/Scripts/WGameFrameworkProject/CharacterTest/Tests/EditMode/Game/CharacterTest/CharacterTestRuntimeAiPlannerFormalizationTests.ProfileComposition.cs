using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Physics;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.UI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerFormalizationTests
    {
        [Test]
        public void RuntimeAiPlannerProfileValidator_ReportsFormalProfileErrors()
        {
            var profile = new CharacterTestRuntimeAiPlannerProfile(
                states: new[]
                {
                    new CharacterTestRuntimeAiPlannerStateDefinition(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0034),
                    new CharacterTestRuntimeAiPlannerStateDefinition(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0034, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060),
                    new CharacterTestRuntimeAiPlannerStateDefinition(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0023),
                    new CharacterTestRuntimeAiPlannerStateDefinition(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0023)
                },
                transitions: new[]
                {
                    new CharacterTestRuntimeAiPlannerTransitionDefinition(
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060,
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0052,
                        1,
                        CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                            CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                            true))
                },
                actions: new[]
                {
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        1,
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060,
                        (CharacterTestRuntimeAiPlannerActionKind)999,
                        float.NaN,
                        1f),
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        1,
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0052,
                        CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot,
                        1f,
                        float.PositiveInfinity)
                });

            CharacterTestRuntimeAiPlannerValidationReport report =
                CharacterTestRuntimeAiPlannerProfileValidator.Validate(profile);

            Assert.IsTrue(report.HasErrors);
            AssertHasDiagnostic(report, "state.id.duplicate");
            AssertHasDiagnostic(report, "state.parent.cycle");
            AssertHasDiagnostic(report, "transition.target.missing");
            AssertHasDiagnostic(report, "action.score.invalid");
            AssertHasDiagnostic(report, "action.cost.invalid");
            AssertHasDiagnostic(report, "action.output.invalid");
            AssertHasDiagnostic(report, "action.output.slot.missing");
            AssertHasDiagnostic(report, "action.state.missing");
        }

        [Test]
        public void RuntimeAiPlannerProfileJson_DefaultTemplateRoundTripsAndValidates()
        {
            CharacterTestRuntimeAiPlannerProfile expected =
                CharacterTestRuntimeAiPlannerProfileSource.CreateDefault();
            string json = CharacterTestRuntimeAiPlannerProfileJson.ToJson(expected);

            bool loaded = CharacterTestRuntimeAiPlannerProfileJson.TryLoad(
                json,
                out CharacterTestRuntimeAiPlannerProfile actual,
                out CharacterTestRuntimeAiPlannerValidationReport report);

            Assert.IsTrue(loaded, report.ToText());
            Assert.IsFalse(report.HasErrors, report.ToText());
            Assert.AreEqual(expected.ProfileId, actual.ProfileId);
            Assert.AreEqual(expected.States.Count, actual.States.Count);
            Assert.AreEqual(expected.Actions.Count, actual.Actions.Count);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerProfileSource.DefaultMoveToTargetActionId,
                actual.Actions[0].ActionId);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultVisionRange,
                actual.PerceptionSettings.VisionRange);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultSpeedScale,
                actual.MovementSettings.SpeedScale);
            StringAssert.Contains(
                "mx.character-test.runtime-ai-planner.profile.v3",
                json);
        }

        [Test]
        public void RuntimeAiPlannerBehaviorCompositionJson_RoundTripsAndValidates()
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile expected =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            EnableModule(
                expected,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                true);
            string json = CharacterTestRuntimeAiPlannerBehaviorCompositionJson.ToJson(expected);

            bool loaded = CharacterTestRuntimeAiPlannerBehaviorCompositionJson.TryLoad(
                json,
                out CharacterTestRuntimeAiPlannerBehaviorCompositionProfile actual,
                out CharacterTestRuntimeAiPlannerValidationReport report);

            Assert.IsTrue(loaded, report.ToText());
            Assert.AreEqual(expected.CompositionId, actual.CompositionId);
            Assert.AreEqual(expected.Modules.Count, actual.Modules.Count);
            StringAssert.Contains(
                "mx.character-test.runtime-ai-planner.composition.v1",
                json);
            StringAssert.Contains(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                json);
        }

        [Test]
        public void RuntimeAiPlannerBehaviorDefinitionCatalogJson_LoadsLargeCatalogAndCompilesOnlyGroupEntries()
        {
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog source =
                CreateLargeBehaviorDefinitionCatalog(1000);
            string json = CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalogJson.ToJson(source);

            bool loaded = CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalogJson.TryLoad(
                json,
                out CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog,
                out CharacterTestRuntimeAiPlannerValidationReport report);
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile group =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(group, catalog);

            Assert.IsTrue(loaded, report.ToText());
            Assert.AreEqual(source.Definitions.Count, catalog.Definitions.Count);
            Assert.AreEqual(source.CatalogHash, catalog.CatalogHash);
            Assert.IsTrue(compile.Success, compile.Report.ToText());
            Assert.AreEqual(group.Entries.Count, compile.ModuleSummaries.Count);
            Assert.Less(compile.Profile.Actions.Count, catalog.Definitions.Count);
            StringAssert.Contains(
                "mx.character-test.runtime-ai-planner.behavior-definition-catalog.v1",
                json);
        }

        [Test]
        public void RuntimeAiPlannerBehaviorGroupJson_RoundTripsEntriesAndDisabledBlocksCandidate()
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile group =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            EnableModule(
                group,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                false);
            string json = CharacterTestRuntimeAiPlannerBehaviorGroupJson.ToJson(group);

            bool loaded = CharacterTestRuntimeAiPlannerBehaviorGroupJson.TryLoad(
                json,
                CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn(),
                out CharacterTestRuntimeAiPlannerBehaviorCompositionProfile actual,
                out CharacterTestRuntimeAiPlannerValidationReport report);
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(actual);

            Assert.IsTrue(loaded, report.ToText());
            Assert.AreEqual(group.Entries.Count, actual.Entries.Count);
            Assert.IsTrue(FindBehaviorEntry(
                actual,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack).Disabled);
            Assert.IsTrue(compile.Success, compile.Report.ToText());
            AssertActionKindMissing(compile.Profile, CharacterTestRuntimeAiPlannerActionKind.Retreat);
            AssertSummaryContains(compile, "disabled condition blocked");
            StringAssert.Contains(
                "mx.character-test.runtime-ai-planner.behavior-group.v4",
                json);
        }

        [Test]
        public void RuntimeAiPlannerBehaviorGroupJson_V2MigratesAssignmentsOneToOneWithoutAddingCards()
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile source =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            JObject legacyDocument = JObject.Parse(
                CharacterTestRuntimeAiPlannerBehaviorGroupJson.ToJson(source));
            legacyDocument["schema"] = "mx.character-test.runtime-ai-planner.behavior-group.v2";
            foreach (JObject entry in legacyDocument["entries"] as JArray ?? new JArray())
            {
                string value = (string)entry["responsibilityKinds"]?[0] ?? "Other";
                entry.Remove("responsibilityKinds");
                entry["slotKind"] = string.Equals(value, "Other", StringComparison.Ordinal)
                    ? "Flexible"
                    : value;
            }
            string legacyJson = legacyDocument.ToString();

            Assert.IsTrue(CharacterTestRuntimeAiPlannerBehaviorGroupJson.TryLoad(
                legacyJson,
                CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn(),
                out CharacterTestRuntimeAiPlannerBehaviorCompositionProfile migrated,
                out CharacterTestRuntimeAiPlannerValidationReport report),
                report.ToText());
            Assert.AreEqual(source.Entries.Count, migrated.Entries.Count);
            for (int i = 0; i < source.Entries.Count; i++)
            {
                Assert.AreEqual(source.Entries[i].EntryId, migrated.Entries[i].EntryId);
                Assert.AreEqual(source.Entries[i].BehaviorId, migrated.Entries[i].BehaviorId);
                Assert.AreEqual(source.Entries[i].ResponsibilityKinds[0],
                    migrated.Entries[i].ResponsibilityKinds[0]);
            }
        }

        [Test]
        public void RuntimeAiPlannerBehaviorGroup_RejectsDuplicateEntryIds()
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile group =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry copy = group.Entries[0].Clone();
            copy.Order += 1;
            group.Entries.Add(copy);

            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(group);

            Assert.IsFalse(compile.Success);
            AssertHasDiagnostic(compile.Report, "composition.entry.duplicate");
        }

        [Test]
        public void RuntimeAiPlannerBehaviorComposition_MissingTargetingResponsibilityDoesNotBlockCompile()
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile composition =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            EnableModule(
                composition,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                false);

            CharacterTestRuntimeAiPlannerBehaviorCompileResult result =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(composition);

            Assert.IsTrue(result.Success, result.Report.ToText());
        }

        [Test]
        public void RuntimeAiPlannerBehaviorLoadoutCatalog_CompilesSelectionAndHashesParameters()
        {
            CharacterTestRuntimeAiPlannerProfile runtimeProfile =
                CharacterTestRuntimeAiPlannerProfileSource.CreateDefault();
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtime =
                CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime.CreateDefault(
                    runtimeProfile,
                    null,
                    NullRuntimeLogger.Instance);

            Assert.AreEqual(1, runtime.Entries.Count);
            Assert.AreEqual(
                runtime.Catalog.Loadouts[0].Variants[0].BehaviorGroup.GroupId,
                runtime.Catalog.Loadouts[0].Variants[0].BehaviorGroupId);
            Assert.IsTrue(runtime.TryResolveSelection(
                1,
                1,
                out CharacterTestRuntimeAiBehaviorSelection selection,
                out string message), message);
            Assert.IsTrue(selection.HasSelection);
            Assert.AreEqual(runtime.Catalog.CatalogHash, selection.CatalogHash);
            Assert.Greater(selection.ProfileSlot, 0);
            Assert.IsTrue(runtime.TryValidateSelection(selection, out message), message);

            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile changed =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile(runtimeProfile);
            SetModuleFloat(
                changed,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey,
                321f);
            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog changedCatalog =
                CreateSingleVariantLoadoutCatalog(changed);

            Assert.AreNotEqual(runtime.Catalog.CatalogHash, changedCatalog.CatalogHash);
        }

        [Test]
        public void RuntimeAiPlannerBehaviorLoadoutCatalog_RegisteredSessionProfileAliasRemainsValid()
        {
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtime =
                CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime.CreateDefault(
                    CharacterTestRuntimeAiPlannerProfileSource.CreateDefault(),
                    null,
                    NullRuntimeLogger.Instance);
            Assert.IsTrue(runtime.TryResolveSelection(
                1,
                1,
                out CharacterTestRuntimeAiBehaviorSelection canonical,
                out string message), message);
            var reserved = new CharacterTestRuntimeAiBehaviorSelection(
                canonical.LoadoutId,
                canonical.VariantId,
                canonical.CatalogHash,
                canonical.ProfileSlot + 1);

            Assert.IsFalse(runtime.TryValidateSelection(reserved, out _));
            Assert.IsTrue(
                runtime.TryRegisterSessionProfileSlotAlias(reserved, out message),
                message);
            Assert.IsTrue(runtime.TryValidateSelection(reserved, out message), message);
        }

    }
}

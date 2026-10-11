using System;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionAuthoringProblemsTests
    {
        [Test]
        public void ProblemsModel_MergesDeduplicatesAndSortsStructuredDiagnostics()
        {
            var local = new CharacterActionValidationDiagnostic(
                "character_actions.json",
                "actions[light_attack].animationTrack.events[anim.hit].animationActionKey",
                "ACT_LOCAL",
                CharacterActionDiagnosticSeverity.Warning,
                "local warning");
            CharacterActionAuthoringProblemsModel model = CharacterActionAuthoringProblemsModel.Create(
                new[]
                {
                    new CharacterActionJsonDiagnostic(
                        "character_action_sets.json",
                        "$.actionSets[0].commandBindings[0].actionId",
                        "ACT_IMPORT",
                        CharacterActionDiagnosticSeverity.Error,
                        "import error"),
                },
                new[] { local },
                new[]
                {
                    local,
                    new CharacterActionValidationDiagnostic(
                        "character_actions.json",
                        "actions[light_attack].combatTrack.events[hit.0].combatActionId",
                        "ACT_CROSS",
                        CharacterActionDiagnosticSeverity.Warning,
                        "cross warning"),
                },
                new[]
                {
                    new CharacterActionReferenceDiscoveryDiagnostic(
                        CharacterActionReferenceDiscoverySeverity.Info,
                        "ACT_SOURCE",
                        "ResourceCatalog",
                        "provider unavailable"),
                });

            Assert.AreEqual(4, model.Items.Count);
            Assert.AreEqual(1, model.ErrorCount);
            Assert.AreEqual(2, model.WarningCount);
            Assert.AreEqual(1, model.InfoCount);
            Assert.AreEqual(CharacterActionAuthoringProblemOrigin.Import, model.Items[0].Origin);
            Assert.AreEqual(CharacterActionAuthoringProblemOrigin.LocalValidation, model.Items[1].Origin);
            Assert.AreEqual(CharacterActionAuthoringProblemOrigin.CrossModuleValidation, model.Items[2].Origin);
            Assert.AreEqual(CharacterActionAuthoringProblemOrigin.ReferenceDiscovery, model.Items[3].Origin);
            Assert.AreEqual(4, model.Project().Items.Length);
        }

        [TestCase("actionSets[set.main].commandBindings[3].actionId", CharacterActionAuthoringProblemTargetKind.CommandBinding, "set.main", "commandBindings", 3, "actionId")]
        [TestCase("$.actionSets[0].abilityBindings[2].requiredTags[1]", CharacterActionAuthoringProblemTargetKind.AbilityBinding, "0", "abilityBindings", 2, "requiredTags[1]")]
        [TestCase("actionSets[set.main].reactionBindings[1].actionId", CharacterActionAuthoringProblemTargetKind.LegacyReactionBinding, "set.main", "reactionBindings", 1, "actionId")]
        public void LocationParser_MapsActionSetBindingPaths(
            string path,
            object target,
            string objectKey,
            string section,
            int index,
            string fieldPath)
        {
            CharacterActionAuthoringProblemLocation location =
                CharacterActionAuthoringProblemLocationParser.Parse("sets.json", path);

            Assert.AreEqual(CharacterActionAuthoringProblemWorkspace.Mappings, location.Workspace);
            Assert.AreEqual(target, location.TargetKind);
            Assert.AreEqual(objectKey, location.ObjectKey);
            Assert.AreEqual(section, location.Section);
            Assert.AreEqual(index, location.ItemIndex);
            Assert.AreEqual(fieldPath, location.FieldPath);
        }

        [Test]
        public void LocationParser_MapsStableTimelineEventAndActionFallback()
        {
            CharacterActionAuthoringProblemLocation timeline = CharacterActionAuthoringProblemLocationParser.Parse(
                "actions.json",
                "actions[light_attack].combatTrack.events[hit.primary].hitEvent.damageProfileId");
            CharacterActionAuthoringProblemLocation action = CharacterActionAuthoringProblemLocationParser.Parse(
                "actions.json",
                "actions[light_attack].durationFrames");

            Assert.AreEqual(CharacterActionAuthoringProblemTargetKind.TimelineItem, timeline.TargetKind);
            Assert.AreEqual("light_attack", timeline.ObjectKey);
            Assert.AreEqual("combatTrack.events", timeline.Section);
            Assert.AreEqual("hit.primary", timeline.ItemKey);
            Assert.AreEqual(-1, timeline.ItemIndex);
            Assert.AreEqual("hitEvent.damageProfileId", timeline.FieldPath);
            Assert.AreEqual(CharacterActionAuthoringProblemTargetKind.Action, action.TargetKind);
            Assert.AreEqual("durationFrames", action.FieldPath);
        }

        [Test]
        public void LocationParser_MapsReactionProfileAndFallsBackToSource()
        {
            CharacterActionAuthoringProblemLocation reaction = CharacterActionAuthoringProblemLocationParser.Parse(
                "reaction_profiles.json",
                "reactionProfiles[reaction.default].rules[2].actionId");
            CharacterActionAuthoringProblemLocation fallback = CharacterActionAuthoringProblemLocationParser.Parse(
                "unknown.json",
                "$.metadata.owner");

            Assert.AreEqual(CharacterActionAuthoringProblemTargetKind.ReactionProfile, reaction.TargetKind);
            Assert.AreEqual("reaction.default", reaction.ObjectKey);
            Assert.AreEqual("rules[2].actionId", reaction.FieldPath);
            Assert.AreEqual(CharacterActionAuthoringProblemTargetKind.Source, fallback.TargetKind);
            Assert.AreEqual("unknown.json", fallback.SourcePath);
        }

        [Test]
        public void ProblemsProjection_FiltersByCurrentActionSeverityAndQuery()
        {
            CharacterActionAuthoringProblemsModel model = CharacterActionAuthoringProblemsModel.Create(
                null,
                new[]
                {
                    new CharacterActionValidationDiagnostic(
                        "actions.json",
                        "actions[light_attack].durationFrames",
                        "ACT_DURATION",
                        CharacterActionDiagnosticSeverity.Error,
                        "duration is missing"),
                    new CharacterActionValidationDiagnostic(
                        "actions.json",
                        "actions[jump].durationFrames",
                        "ACT_JUMP",
                        CharacterActionDiagnosticSeverity.Warning,
                        "duration fallback"),
                },
                null,
                null);

            CharacterActionAuthoringProblemsProjection projection = model.Project(
                new CharacterActionAuthoringProblemFilter(
                    CharacterActionAuthoringProblemScope.CurrentAction,
                    CharacterActionAuthoringProblemSeverityFilter.Error,
                    "duration",
                    currentActionId: "light_attack"));

            Assert.AreEqual(1, projection.Items.Length);
            Assert.AreEqual("ACT_DURATION", projection.Items[0].Code);
            Assert.AreEqual(1, projection.TotalErrorCount);
            Assert.AreEqual(1, projection.TotalWarningCount);
        }

        [Test]
        public void SearchCatalog_PreservesUnknownCurrentAndSearchesAllTokens()
        {
            var catalog = new CharacterActionAuthoringSearchCatalog(new[]
            {
                new CharacterActionAuthoringSearchCandidate(
                    CharacterActionAuthoringSearchCategory.Action,
                    "light_attack",
                    "轻攻击",
                    "近战 Combat",
                    "Common"),
                new CharacterActionAuthoringSearchCandidate(
                    CharacterActionAuthoringSearchCategory.Action,
                    "heavy_attack",
                    "重攻击",
                    "近战 Combat",
                    "Common"),
            });

            CharacterActionAuthoringSearchProjection projection = catalog.Project(
                CharacterActionAuthoringSearchCategory.Action,
                "轻 combat",
                "legacy.unknown");

            Assert.IsTrue(projection.HasUnknownCurrentValue);
            Assert.AreEqual(2, projection.Candidates.Length);
            Assert.AreEqual("legacy.unknown", projection.Candidates[0].Value);
            Assert.IsFalse(projection.Candidates[0].IsKnown);
            Assert.IsTrue(projection.Candidates[0].IsCurrent);
            Assert.AreEqual("light_attack", projection.Candidates[1].Value);
        }

        [Test]
        public void SearchCatalog_KnownCurrentStaysVisibleWhenQueryDoesNotMatch()
        {
            var catalog = new CharacterActionAuthoringSearchCatalog(new[]
            {
                new CharacterActionAuthoringSearchCandidate(
                    CharacterActionAuthoringSearchCategory.Intent,
                    "Jump",
                    "跳跃"),
                new CharacterActionAuthoringSearchCandidate(
                    CharacterActionAuthoringSearchCategory.Intent,
                    "Dodge",
                    "闪避"),
            });

            CharacterActionAuthoringSearchProjection projection = catalog.Project(
                CharacterActionAuthoringSearchCategory.Intent,
                "闪避",
                "Jump");

            Assert.IsFalse(projection.HasUnknownCurrentValue);
            Assert.AreEqual(2, projection.Candidates.Length);
            Assert.AreEqual("Jump", projection.Candidates[0].Value);
            Assert.IsTrue(projection.Candidates[0].IsCurrent);
            Assert.AreEqual("Dodge", projection.Candidates[1].Value);
        }

        [Test]
        public void SearchCatalog_MapsReferenceDiscoveryAndDeduplicatesValues()
        {
            var discovery = new CharacterActionReferenceDiscoveryResult(
                CharacterActionAuthoringReferenceContext.CreateDefault(),
                new[]
                {
                    new CharacterActionReferenceDiscoveryEntry(
                        CharacterActionReferenceDiscoveryCategory.CharacterStableId,
                        "char.hero",
                        CharacterActionReferenceDiscoverySourceKind.CharacterApplicationConfig,
                        "package-a"),
                    new CharacterActionReferenceDiscoveryEntry(
                        CharacterActionReferenceDiscoveryCategory.CharacterStableId,
                        "char.hero",
                        CharacterActionReferenceDiscoverySourceKind.ManualFallback,
                        "manual"),
                    new CharacterActionReferenceDiscoveryEntry(
                        CharacterActionReferenceDiscoveryCategory.EquipmentTag,
                        "weapon.light",
                        CharacterActionReferenceDiscoverySourceKind.CharacterApplicationConfig,
                        "package-a"),
                },
                Array.Empty<CharacterActionReferenceDiscoveryDiagnostic>());

            CharacterActionAuthoringSearchCatalog catalog =
                CharacterActionAuthoringSearchCatalog.CreateFromReferenceDiscovery(discovery);
            CharacterActionAuthoringSearchProjection characters = catalog.Project(
                CharacterActionAuthoringSearchCategory.Character,
                string.Empty,
                string.Empty);
            CharacterActionAuthoringSearchProjection tags = catalog.Project(
                CharacterActionAuthoringSearchCategory.Tag,
                "light",
                string.Empty);

            Assert.AreEqual(1, characters.Candidates.Length);
            Assert.AreEqual("char.hero", characters.Candidates[0].Value);
            Assert.AreEqual(1, tags.Candidates.Length);
            Assert.AreEqual("weapon.light", tags.Candidates[0].Value);
        }

        [Test]
        public void SearchCatalogBuilder_AggregatesEveryProfileActionLibraryWithSourceLabels()
        {
            const string profileDirectory =
                "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test";
            const string actionSetDirectory =
                "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test";
            const string characterLibraryDirectory =
                "Assets/MxFrameworkGenerated/CharacterActions/Libraries/codex_flow_test";
            const string commonLibraryDirectory =
                "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common";
            Assert.IsTrue(File.Exists(Path.Combine(profileDirectory, CharacterActionJsonImporter.DefaultProfilesSourcePath)));

            CharacterActionAuthoringDocumentSession session = CharacterActionAuthoringDocumentSession.Load(
                new CharacterActionAuthoringSourceEntry(profileDirectory, "codex_flow_test"),
                new[] { new CharacterActionAuthoringSourceEntry(actionSetDirectory, "codex_flow_test") },
                new[]
                {
                    new CharacterActionAuthoringSourceEntry(characterLibraryDirectory, "codex_flow_test"),
                    new CharacterActionAuthoringSourceEntry(commonLibraryDirectory, "Common"),
                });
            CharacterActionAuthoringSearchCatalog catalog =
                CharacterActionAuthoringSearchCatalogBuilder.BuildActions(session, null);

            Assert.AreEqual(53, catalog.Candidates.Count);
            bool hasCharacterLibrary = false;
            bool hasCommonLibrary = false;
            for (int i = 0; i < catalog.Candidates.Count; i++)
            {
                hasCharacterLibrary |= string.Equals(
                    catalog.Candidates[i].SourceName,
                    "codex_flow_test",
                    StringComparison.Ordinal);
                hasCommonLibrary |= string.Equals(
                    catalog.Candidates[i].SourceName,
                    "Common",
                    StringComparison.Ordinal);
            }
            Assert.IsTrue(hasCharacterLibrary);
            Assert.IsTrue(hasCommonLibrary);
        }

        [Test]
        public void SearchCatalogBuilder_ProvidesIntentReactionAbilityAndTagCandidates()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            model.ActionSet.AbilityBindings = new[]
            {
                new CharacterAbilityActionBindingJsonDto
                {
                    AbilityId = 900001,
                    ActionId = 301,
                    RequiredTags = new[] { "weapon.light" },
                    ForbiddenTags = new[] { "status.stunned" },
                },
            };

            CharacterActionAuthoringSearchProjection intents =
                CharacterActionAuthoringSearchCatalogBuilder.BuildIntents(null, model).Project(
                    CharacterActionAuthoringSearchCategory.Intent,
                    "Jump",
                    string.Empty);
            CharacterActionAuthoringSearchProjection reactions =
                CharacterActionAuthoringSearchCatalogBuilder.BuildReactionProfiles(null, model).Project(
                    CharacterActionAuthoringSearchCategory.ReactionProfile,
                    "reaction",
                    string.Empty);
            CharacterActionAuthoringSearchProjection abilities =
                CharacterActionAuthoringSearchCatalogBuilder.BuildAbilities(null, model, new[] { 900002 }).Project(
                    CharacterActionAuthoringSearchCategory.Ability,
                    string.Empty,
                    string.Empty);
            CharacterActionAuthoringSearchProjection tags =
                CharacterActionAuthoringSearchCatalogBuilder.BuildTags(null, model).Project(
                    CharacterActionAuthoringSearchCategory.Tag,
                    "status",
                    string.Empty);

            Assert.GreaterOrEqual(intents.Candidates.Length, 1);
            Assert.GreaterOrEqual(reactions.Candidates.Length, 1);
            Assert.AreEqual(1, abilities.Candidates.Length);
            Assert.AreEqual("900002", abilities.Candidates[0].Value);
            CharacterActionAuthoringSearchProjection legacyAbility =
                CharacterActionAuthoringSearchCatalogBuilder.BuildAbilities(null, model, new[] { 900002 }).Project(
                    CharacterActionAuthoringSearchCategory.Ability,
                    string.Empty,
                    "900001");
            Assert.IsTrue(legacyAbility.HasUnknownCurrentValue);
            Assert.AreEqual("900001", legacyAbility.Candidates[0].Value);
            Assert.AreEqual(1, tags.Candidates.Length);
            Assert.AreEqual("status.stunned", tags.Candidates[0].Value);
        }

        [Test]
        public void BindingNavigation_ResolvesNumericActionSetIndexFromDiagnosticSource()
        {
            CharacterActionAuthoringDocumentSession session = LoadCodexFlowSession();
            CharacterActionAuthoringActionSetBundleDocument bundle = session.ActionSetBundles[0];
            CharacterActionAuthoringProblemLocation location =
                CharacterActionAuthoringProblemLocationParser.Parse(
                    bundle.Source.ActionSetsPath,
                    "$.actionSets[1].abilityBindings[1].requiredTags[0]");

            bool created = CharacterActionBindingProblemNavigation.TryCreate(
                location,
                session,
                session.CurrentActionSet,
                out CharacterActionBindingProblemNavigationPlan plan);

            Assert.IsTrue(created);
            Assert.AreEqual("codex_flow_test.sword_empty", plan.ActionSetStableId);
            Assert.AreEqual(CharacterActionAuthoringProblemTargetKind.AbilityBinding, plan.TargetKind);
            Assert.AreEqual(1, plan.OriginalIndex);
            Assert.AreEqual(
                "character-action-ability-binding-1",
                plan.InspectorCardElementName);
            Assert.AreEqual(
                "character-action-ability-binding-1-requiredTags",
                plan.FieldElementName);
        }

        [Test]
        public void BindingNavigation_DoesNotTreatNumericDocumentIndexAsStableId()
        {
            CharacterActionAuthoringDocumentSession session = LoadCodexFlowSession();
            CharacterActionAuthoringProblemLocation location =
                CharacterActionAuthoringProblemLocationParser.Parse(
                    "Assets/Unknown/character_action_sets.json",
                    "$.actionSets[0].commandBindings[0].actionId");

            Assert.IsFalse(CharacterActionBindingProblemNavigation.TryCreate(
                location,
                session,
                session.CurrentActionSet,
                out _));
            Assert.IsFalse(CharacterActionBindingProblemNavigation.TryResolveActionSetStableId(
                "0",
                string.Empty,
                null,
                session.CurrentActionSet,
                out _,
                out _));
        }

        [Test]
        public void BindingNavigation_StableIdAndNestedFieldProduceStableInspectorName()
        {
            CharacterActionAuthoringDocumentSession session = LoadCodexFlowSession();
            CharacterActionAuthoringProblemLocation location =
                CharacterActionAuthoringProblemLocationParser.Parse(
                    session.ActionSetBundles[0].Source.ActionSetsPath,
                    "actionSets[codex_flow_test.empty_empty].commandBindings[0].actionId.value");

            Assert.IsTrue(CharacterActionBindingProblemNavigation.TryCreate(
                location,
                session,
                session.CurrentActionSet,
                out CharacterActionBindingProblemNavigationPlan plan));
            Assert.AreEqual("codex_flow_test.empty_empty", plan.ActionSetStableId);
            Assert.AreEqual(
                "character-action-command-binding-0-actionId",
                plan.FieldElementName);
        }

        [Test]
        public void BindingTableProjection_ReportsWhetherFilteredRowsContainOriginalIndex()
        {
            CharacterActionBindingJsonDto[] bindings =
            {
                new CharacterActionBindingJsonDto { IntentId = CharacterTestBulkIds38.Id790029, ActionId = 300 },
                new CharacterActionBindingJsonDto { IntentId = CharacterTestBulkIds38.Id790030, ActionId = 301 },
            };
            var knownActions = new System.Collections.Generic.HashSet<int>
            {
                300,
                301,
            };
            System.Collections.Generic.List<CharacterActionBindingTableRow> filtered =
                CharacterActionBindingTableProjection.BuildCommandRows(
                    bindings,
                    "set.main",
                    knownActions,
                    "Jump",
                    CharacterActionBindingTableSort.TargetAction);

            Assert.IsTrue(CharacterActionBindingTableProjection.ContainsOriginalIndex(filtered, 0));
            Assert.IsFalse(CharacterActionBindingTableProjection.ContainsOriginalIndex(filtered, 1));
        }

        private static CharacterActionAuthoringDocumentSession LoadCodexFlowSession()
        {
            const string profileDirectory =
                "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test";
            const string actionSetDirectory =
                "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test";
            const string characterLibraryDirectory =
                "Assets/MxFrameworkGenerated/CharacterActions/Libraries/codex_flow_test";
            const string commonLibraryDirectory =
                "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common";
            return CharacterActionAuthoringDocumentSession.Load(
                new CharacterActionAuthoringSourceEntry(profileDirectory, "codex_flow_test"),
                new[] { new CharacterActionAuthoringSourceEntry(actionSetDirectory, "codex_flow_test") },
                new[]
                {
                    new CharacterActionAuthoringSourceEntry(characterLibraryDirectory, "codex_flow_test"),
                    new CharacterActionAuthoringSourceEntry(commonLibraryDirectory, "Common"),
                });
        }
    }
}

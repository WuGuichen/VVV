using System;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionProfileOverviewProjectionTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(
                "Temp",
                "CharacterActionProfileOverviewProjectionTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_root) && Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        [Test]
        public void Build_ProjectsUniqueDirectActionsLibrariesAndAvailableActionSets()
        {
            CharacterActionAuthoringDocumentSession session = CreateSession();

            CharacterActionProfileOverviewProjection projection =
                CharacterActionProfileOverviewProjection.Build(session);

            Assert.AreEqual("profile.test", projection.Profile.StableId);
            Assert.AreEqual(1, projection.ActionSets.Length);
            Assert.AreEqual("set.equipped", projection.SelectedActionSet.ActionSet.StableId);
            Assert.AreEqual(4, projection.SelectedActionSet.ReferencedActionIds.Length);
            Assert.AreEqual(1, projection.SelectedActionSet.MissingActionCount);
            Assert.AreEqual(1, projection.SelectedActionSet.CommandBindingCount);
            Assert.AreEqual(1, projection.SelectedActionSet.AbilityBindingCount);
            Assert.IsTrue(projection.SelectedActionSet.HasReactionProfile);
            Assert.IsTrue(projection.SelectedActionSet.HasDefaultAction);

            Assert.AreEqual(1, projection.AvailableActionSets.Length);
            Assert.AreEqual("set.available", projection.AvailableActionSets[0].StableId);
            Assert.AreEqual(2, projection.Libraries.Length);
            Assert.AreEqual("Role", projection.Libraries[0].Library.LibraryId);
            Assert.AreEqual(3, projection.Libraries[0].ActionCount);
            Assert.AreEqual(3, projection.Libraries[0].DirectReferences.Length);
            Assert.IsTrue(projection.Libraries[0].IsSelected);
            Assert.AreEqual("Common", projection.Libraries[1].Library.LibraryId);
            Assert.AreEqual(2, projection.Libraries[1].DirectReferences.Length);
        }

        [Test]
        public void Build_ReportsMissingReactionProfileAndDefaultActionReminderState()
        {
            CharacterActionAuthoringDocumentSession session = CreateSession();
            CharacterActionSetJsonDto actionSet = session.CurrentActionSet;
            actionSet.DefaultActionId = 0;
            actionSet.ReactionProfileId = "reaction.missing";

            CharacterActionProfileActionSetSummary summary =
                CharacterActionProfileOverviewProjection.Build(session).SelectedActionSet;

            Assert.IsFalse(summary.HasDefaultAction);
            Assert.IsFalse(summary.HasReactionProfile);
            Assert.IsFalse(summary.IsHealthy);
        }

        private CharacterActionAuthoringDocumentSession CreateSession()
        {
            string profileDirectory = Path.Combine(_root, "Profiles", "test");
            string actionSetDirectory = Path.Combine(_root, "ActionSets", "test");
            string roleLibraryDirectory = Path.Combine(_root, "Libraries", "Role");
            string commonLibraryDirectory = Path.Combine(_root, "Libraries", "Common");
            Directory.CreateDirectory(profileDirectory);
            Directory.CreateDirectory(actionSetDirectory);
            Directory.CreateDirectory(roleLibraryDirectory);
            Directory.CreateDirectory(commonLibraryDirectory);

            WriteJson(
                Path.Combine(profileDirectory, "character_action_profile.json"),
                new CharacterActionProfilesJsonDocument
                {
                    SchemaVersion = "0.1.0",
                    Profiles = new[]
                    {
                        new CharacterActionProfileJsonDto
                        {
                            Id = 1,
                            StableId = "profile.test",
                            DisplayName = "Test Profile",
                            ActionSetIds = new[] { "set.equipped" },
                            ActionLibraryIds = new[] { "Role", "Common" },
                        },
                    },
                });
            WriteJson(
                Path.Combine(actionSetDirectory, "character_action_sets.json"),
                new CharacterActionSetsJsonDocument
                {
                    SchemaVersion = "0.1.0",
                    ActionSets = new[]
                    {
                        new CharacterActionSetJsonDto
                        {
                            StableId = "set.equipped",
                            DisplayName = "剑 / 空手",
                            CharacterStableId = "char.test",
                            EquipmentStateStableId = "equipment.test",
                            DefaultActionId = 101,
                            ReactionProfileId = "reaction.test",
                            CommandBindings = new[]
                            {
                                new CharacterActionBindingJsonDto
                                {
                                    IntentId = CharacterTestBulkIds38.Id790054,
                                    ActionId = 101,
                                },
                            },
                            AbilityBindings = new[]
                            {
                                new CharacterAbilityActionBindingJsonDto
                                {
                                    AbilityId = 1001,
                                    ActionId = 102,
                                },
                            },
                        },
                        new CharacterActionSetJsonDto
                        {
                            StableId = "set.available",
                            DisplayName = "空手 / 空手",
                            CharacterStableId = "char.test",
                        },
                    },
                });
            WriteJson(
                Path.Combine(actionSetDirectory, "character_reaction_profiles.json"),
                new CharacterReactionProfilesJsonDocument
                {
                    SchemaVersion = "0.1.0",
                    ReactionProfiles = new[]
                    {
                        new CharacterReactionProfileJsonDto
                        {
                            StableId = "reaction.test",
                            DefaultActionId = 201,
                            Rules = new[]
                            {
                                new CharacterReactionRuleJsonDto
                                {
                                    Trigger = "Hit",
                                    ActionId = 201,
                                },
                                new CharacterReactionRuleJsonDto
                                {
                                    Trigger = "Death",
                                    ActionId = 999,
                                },
                            },
                        },
                    },
                });
            WriteJson(
                Path.Combine(roleLibraryDirectory, "character_actions.json"),
                CreateActions(101, 102, 103));
            WriteJson(
                Path.Combine(commonLibraryDirectory, "character_actions.json"),
                CreateActions(201));

            return CharacterActionAuthoringDocumentSession.Load(
                new CharacterActionAuthoringSourceEntry(profileDirectory, "test"),
                new[] { new CharacterActionAuthoringSourceEntry(actionSetDirectory, "test") },
                new[]
                {
                    new CharacterActionAuthoringSourceEntry(roleLibraryDirectory, "Role"),
                    new CharacterActionAuthoringSourceEntry(commonLibraryDirectory, "Common"),
                });
        }

        private static CharacterActionsJsonDocument CreateActions(params int[] actionIds)
        {
            var actions = new CharacterActionJsonDto[actionIds.Length];
            for (int i = 0; i < actionIds.Length; i++)
                actions[i] = new CharacterActionJsonDto { ActionId = actionIds[i] };
            return new CharacterActionsJsonDocument
            {
                SchemaVersion = CharacterActionJsonImporter.CurrentActionLibrarySchemaVersion,
                Actions = actions
            };
        }

        private static void WriteJson(string path, object value)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(value, Formatting.Indented));
        }
    }
}

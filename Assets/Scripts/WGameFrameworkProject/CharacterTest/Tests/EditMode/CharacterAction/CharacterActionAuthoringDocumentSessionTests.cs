using System;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionAuthoringDocumentSessionTests
    {
        [Test]
        public void SaveScope_PreparedProfileBundleAndLibraryPreserveFreshJsonAndUnknownFields()
        {
            using (var graph = new TemporaryProjectionGraph())
            {
                foreach (string path in new[] { graph.ProfileSource.ProfilePath, graph.ActionSetSource.ActionSetsPath,
                    graph.ActionSetSource.ReactionProfilesPath, graph.ActionSetSource.LandingProfilesPath,
                    graph.CommonLibrarySource.ActionsPath })
                {
                    JObject root = JObject.Parse(File.ReadAllText(path));
                    root["customMetadata"] = "preserve";
                    File.WriteAllText(path, root.ToString());
                }
                var session = CharacterActionAuthoringDocumentSession.Load(graph.ProfileSource,
                    new[] { graph.ActionSetSource }, new[] { graph.CommonLibrarySource });
                session.ProfileDocument.Document.Profiles[0].DisplayName = "Edited profile";
                session.ActionSetBundles[0].ActionSetsDocument.ActionSets[0].DisplayName = "Edited set";
                session.ActionLibraries[0].Document.Actions = new[]
                {
                    new CharacterActionJsonDto { ActionId = 1, DisplayName = "Edited action" },
                };
                foreach (CharacterActionAuthoringDocumentState state in session.DocumentStates)
                {
                    CharacterActionAuthoringPreparedDocumentWrite[] expected = state.PrepareWrites();
                    using (CharacterActionAuthoringDocumentState.BeginDirtyReadScope())
                    {
                        Assert.IsTrue(state.IsDirty);
                        CharacterActionAuthoringPreparedDocumentWrite[] actual = state.PrepareWrites();
                        Assert.AreEqual(expected.Length, actual.Length);
                        for (int i = 0; i < actual.Length; i++)
                        {
                            Assert.AreEqual(expected[i].Path, actual[i].Path);
                            Assert.IsTrue(JToken.DeepEquals(JToken.Parse(expected[i].Contents), JToken.Parse(actual[i].Contents)));
                            Assert.AreEqual("preserve", JObject.Parse(actual[i].Contents)["customMetadata"].Value<string>());
                        }
                        state.AcceptPreparedWrites(actual);
                        Assert.IsFalse(state.IsDirty);
                    }
                }
            }
        }

        private const string ProfileDirectory =
            "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test";
        private const string ActionSetDirectory =
            "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test";
        private const string CharacterLibraryDirectory =
            "Assets/MxFrameworkGenerated/CharacterActions/Libraries/codex_flow_test";
        private const string CommonLibraryDirectory =
            "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common";

        [Test]
        public void SourceCatalog_MatchingPublishedBundle_IsNotASecondDocument()
        {
            using (var graph = new TemporaryProjectionGraph())
            {
                var sources = CreateProjectionPair(graph, out string generatedRoot, out string publishedRoot);
                var warnings = new System.Collections.Generic.List<string>();
                var selected = CharacterActionAuthoringSourceCatalog.SelectActionSetAuthoringSources(
                    sources, warnings.Add, generatedRoot, publishedRoot);
                CollectionAssert.AreEqual(new[] { sources[0] }, selected);
                Assert.IsEmpty(warnings);
                var session = CharacterActionAuthoringDocumentSession.Load(graph.ProfileSource, selected,
                    new[] { graph.CommonLibrarySource });
                var validation = session.Validate(CharacterActionAuthoringReferenceContext.CreateDefault());
                Assert.IsTrue(validation.ImportResult.Success, validation.DiagnosticsText);
                Assert.AreEqual(1, session.ActionSetBundles.Count);
            }
        }

        [Test]
        public void SourceCatalog_PublishedOnly_StillAvailable()
        {
            using (var graph = new TemporaryProjectionGraph())
            {
                var sources = CreateProjectionPair(graph, out string generatedRoot, out string publishedRoot);
                var selected = CharacterActionAuthoringSourceCatalog.SelectActionSetAuthoringSources(
                    new[] { sources[1] }, Assert.Fail, generatedRoot, publishedRoot);
                CollectionAssert.AreEqual(new[] { sources[1] }, selected);
            }
        }

        [TestCase("character_action_sets.json")]
        [TestCase("character_reaction_profiles.json")]
        [TestCase("character_landing_profiles.json")]
        public void SourceCatalog_ChangedPublishedBundle_ReportsBothPaths(string fileName)
        {
            using (var graph = new TemporaryProjectionGraph())
            {
                var sources = CreateProjectionPair(graph, out string generatedRoot, out string publishedRoot);
                string path = Path.Combine(sources[1].DirectoryPath, fileName);
                JObject changed = JObject.Parse(File.ReadAllText(path));
                changed["schemaVersion"] = "changed.projection";
                File.WriteAllText(path, changed.ToString());
                var warnings = new System.Collections.Generic.List<string>();
                var selected = CharacterActionAuthoringSourceCatalog.SelectActionSetAuthoringSources(
                    sources, warnings.Add, generatedRoot, publishedRoot);
                CollectionAssert.AreEqual(new[] { sources[0] }, selected);
                Assert.AreEqual(1, warnings.Count);
                StringAssert.Contains(sources[0].DirectoryPath, warnings[0]);
                StringAssert.Contains(sources[1].DirectoryPath, warnings[0]);
                Assert.AreEqual("changed.projection", (string)JObject.Parse(File.ReadAllText(path))["schemaVersion"]);
            }
        }

        [Test]
        public void SourceCatalog_IndependentDuplicateIds_StillFailImport()
        {
            using (var graph = new TemporaryProjectionGraph())
            {
                var sources = CreateProjectionPair(graph, out string generatedRoot, out string publishedRoot);
                string independentDirectory = Path.Combine(publishedRoot, "ActionSets", "different_character");
                CopyDirectoryFiles(sources[1].DirectoryPath, independentDirectory);
                var independent = new CharacterActionAuthoringSourceEntry(independentDirectory, "independent");
                var selected = CharacterActionAuthoringSourceCatalog.SelectActionSetAuthoringSources(
                    new[] { sources[0], sources[1], independent }, Assert.Fail, generatedRoot, publishedRoot);
                CollectionAssert.AreEqual(new[] { sources[0], independent }, selected);
                var session = CharacterActionAuthoringDocumentSession.Load(graph.ProfileSource, selected,
                    new[] { graph.CommonLibrarySource });
                var validation = session.Validate(CharacterActionAuthoringReferenceContext.CreateDefault());
                Assert.IsFalse(validation.ImportResult.Success);
                StringAssert.Contains("ACT_JSON_DUPLICATE", validation.DiagnosticsText);
            }
        }

        private static CharacterActionAuthoringSourceEntry[] CreateProjectionPair(
            TemporaryProjectionGraph graph, out string generatedRoot, out string publishedRoot)
        {
            generatedRoot = Path.Combine(graph.Root, "Generated");
            publishedRoot = Path.Combine(graph.Root, "Published");
            string generatedDirectory = Path.Combine(generatedRoot, "ActionSets", "codex_flow_test");
            string publishedDirectory = Path.Combine(publishedRoot, "ActionSets", "codex_flow_test");
            CopyDirectoryFiles(graph.ActionSetSource.DirectoryPath, generatedDirectory);
            CopyDirectoryFiles(graph.ActionSetSource.DirectoryPath, publishedDirectory);
            var generated = new CharacterActionAuthoringSourceEntry(generatedDirectory, "generated");
            var published = new CharacterActionAuthoringSourceEntry(publishedDirectory, "published");
            return new[] { generated, published };
        }

        private sealed class TemporaryProjectionGraph : IDisposable
        {
            internal TemporaryProjectionGraph()
            {
                Root = Path.Combine("Temp", "CharacterActionProjectionTests", Guid.NewGuid().ToString("N"));
                ProfileSource = new CharacterActionAuthoringSourceEntry(Path.Combine(Root, "Profiles"), "profile");
                ActionSetSource = new CharacterActionAuthoringSourceEntry(Path.Combine(Root, "Seed"), "set");
                CommonLibrarySource = new CharacterActionAuthoringSourceEntry(Path.Combine(Root, "Libraries", "Common"), "Common");
                Directory.CreateDirectory(ProfileSource.DirectoryPath);
                Directory.CreateDirectory(ActionSetSource.DirectoryPath);
                Directory.CreateDirectory(CommonLibrarySource.DirectoryPath);
                File.WriteAllText(ProfileSource.ProfilePath, Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    schemaVersion = "0.6.0",
                    profiles = new[] { new { id = 1, stableId = "projection.profile", displayName = "Projection",
                        actionSetIds = new[] { "projection.set" }, actionLibraryIds = new[] { "Common" },
                        defaultActionSetId = "projection.set" } },
                }));
                File.WriteAllText(ActionSetSource.ActionSetsPath, Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    schemaVersion = "0.6.0",
                    actionSets = new[] { new { id = 1, stableId = "projection.set", displayName = "Projection",
                        characterStableId = "", equipmentStateStableId = "", movementProfileId = 1,
                        reactionProfileId = "", defaultActionId = 0, defaultActionSlotMask = 415 } },
                }));
                File.WriteAllText(ActionSetSource.ReactionProfilesPath, "{\"schemaVersion\":\"0.6.0\",\"reactionProfiles\":[]}");
                File.WriteAllText(ActionSetSource.LandingProfilesPath, "{\"schemaVersion\":\"0.6.0\",\"landingProfiles\":[]}");
                File.WriteAllText(CommonLibrarySource.ActionsPath, Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    schemaVersion = CharacterActionJsonImporter.CurrentActionLibrarySchemaVersion,
                    actions = Array.Empty<object>(),
                }));
            }
            internal string Root { get; }
            internal CharacterActionAuthoringSourceEntry ProfileSource { get; }
            internal CharacterActionAuthoringSourceEntry ActionSetSource { get; }
            internal CharacterActionAuthoringSourceEntry CommonLibrarySource { get; }
            public void Dispose()
            {
                if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
            }
        }

        [Test]
        public void Load_CodexProfile_LoadsCompleteReferencedDocumentGraph()
        {
            CharacterActionAuthoringDocumentSession session = LoadCodexSession();

            Assert.AreEqual(1, session.ProfileDocument.Document.Profiles.Length);
            Assert.AreEqual(1, session.ActionSetBundles.Count);
            Assert.AreEqual(2, session.ActionSetBundles[0].ActionSetsDocument.ActionSets.Length);
            Assert.AreEqual(2, session.ActionSetBundles[0].ReactionProfilesDocument.ReactionProfiles.Length);
            Assert.AreEqual(2, session.ActionLibraries.Count);
            Assert.AreEqual(CountReferencedActions(session), session.ProfileActions.Count);
            Assert.AreEqual(0, session.UnresolvedActionSetIds.Length);
            Assert.AreEqual(0, session.UnresolvedActionLibraryIds.Length);
            Assert.AreEqual("codex_flow_test.default", session.Selection.ProfileStableId);
            Assert.AreEqual("codex_flow_test.empty_empty", session.Selection.ActionSetStableId);
            Assert.AreEqual("Common", session.Selection.ActionLibraryId);
            Assert.NotNull(session.CurrentAction);
            Assert.NotNull(session.CurrentReactionProfile);

            for (int i = 0; i < session.DocumentStates.Count; i++)
            {
                Assert.IsFalse(session.DocumentStates[i].IsDirty, session.DocumentStates[i].Key.ToString());
                Assert.AreEqual(0, session.DocumentStates[i].Revision);
            }
        }

        [Test]
        public void StableSelection_DoesNotModifyProfileMembershipOrDirtyDocuments()
        {
            CharacterActionAuthoringDocumentSession session = LoadCodexSession();
            JToken profileBefore = JToken.Parse(session.ProfileDocument.Json);

            Assert.IsTrue(session.SelectActionSet("codex_flow_test.sword_empty"));
            Assert.IsTrue(session.SelectActionLibrary("Common"));
            int commonActionId = session.CurrentActionLibrary.Document.Actions[0].ActionId;
            Assert.IsTrue(session.SelectAction(commonActionId));
            session.SelectTimelineItem("event.selection.only");

            Assert.AreEqual("codex_flow_test.sword_empty", session.Selection.ActionSetStableId);
            Assert.AreEqual("Common", session.Selection.ActionLibraryId);
            Assert.AreEqual(commonActionId, session.Selection.ActionId);
            Assert.IsTrue(JToken.DeepEquals(profileBefore, JToken.Parse(session.ProfileDocument.Json)));
            for (int i = 0; i < session.DocumentStates.Count; i++)
            {
                Assert.IsFalse(session.DocumentStates[i].IsDirty, session.DocumentStates[i].Key.ToString());
                Assert.AreEqual(0, session.DocumentStates[i].Revision);
            }
        }

        [Test]
        public void ProfileComposition_AddsCatalogDocumentsWithoutReloadOrImplicitMembership()
        {
            using (var graph = new TemporaryDocumentGraph())
            {
                string extraActionSetDirectory = Path.Combine(graph.Root, "ActionSets", "catalog_extra");
                string extraLibraryDirectory = Path.Combine(graph.Root, "Libraries", "CatalogExtra");
                CopyDirectoryFiles(ActionSetDirectory, extraActionSetDirectory);
                CopyDirectoryFiles(CommonLibraryDirectory, extraLibraryDirectory);

                var extraActionSetSource = new CharacterActionAuthoringSourceEntry(
                    extraActionSetDirectory,
                    "catalog_extra");
                JObject actionSetRoot = JObject.Parse(File.ReadAllText(extraActionSetSource.ActionSetsPath));
                JObject extraActionSet = (JObject)actionSetRoot["actionSets"]?[0]?.DeepClone();
                Assert.NotNull(extraActionSet);
                extraActionSet["stableId"] = "catalog.extra.set";
                actionSetRoot["actionSets"] = new JArray(extraActionSet);
                File.WriteAllText(extraActionSetSource.ActionSetsPath, actionSetRoot.ToString());

                var extraLibrarySource = new CharacterActionAuthoringSourceEntry(
                    extraLibraryDirectory,
                    "CatalogExtra");
                JObject libraryRoot = JObject.Parse(File.ReadAllText(extraLibrarySource.ActionsPath));
                JObject extraAction = (JObject)libraryRoot["actions"]?[0]?.DeepClone();
                Assert.NotNull(extraAction);
                extraAction["actionId"] = 999001;
                extraAction.Remove("stableId");
                libraryRoot["actions"] = new JArray(extraAction);
                File.WriteAllText(extraLibrarySource.ActionsPath, libraryRoot.ToString());

                CharacterActionAuthoringDocumentSession session =
                    CharacterActionAuthoringDocumentSession.Load(
                        graph.ProfileSource,
                        new[] { graph.ActionSetSource, extraActionSetSource },
                        new[]
                        {
                            graph.CharacterLibrarySource,
                            graph.CommonLibrarySource,
                            extraLibrarySource,
                        });
                int originalProfileActionCount = session.ProfileActions.Count;

                Assert.AreEqual(2, session.ActionSetBundles.Count);
                Assert.AreEqual(3, session.ActionLibraries.Count);
                Assert.AreEqual(CountReferencedActions(session), session.ProfileActions.Count);
                CollectionAssert.DoesNotContain(session.CurrentProfile.ActionSetIds, "catalog.extra.set");
                CollectionAssert.DoesNotContain(session.CurrentProfile.ActionLibraryIds, "CatalogExtra");

                session.CurrentProfile.ActionSetIds = new[]
                {
                    "codex_flow_test.empty_empty",
                    "codex_flow_test.sword_empty",
                    "catalog.extra.set",
                };
                session.CurrentProfile.ActionLibraryIds = new[]
                {
                    "codex_flow_test",
                    "Common",
                    "CatalogExtra",
                };
                session.MarkCurrentProfileChanged(session.CurrentProfile);

                Assert.IsTrue(session.SelectActionSet("catalog.extra.set"));
                Assert.IsTrue(session.SelectActionLibrary("CatalogExtra"));
                Assert.AreEqual(originalProfileActionCount + 1, session.ProfileActions.Count);
                Assert.AreEqual(999001, session.CurrentAction.ActionId);
                Assert.IsTrue(session.ProfileDocument.State.IsDirty);
                Assert.IsFalse(session.FindActionSetBundle("catalog.extra.set").State.IsDirty);
                Assert.IsFalse(session.CurrentActionLibrary.State.IsDirty);
            }
        }

        [Test]
        public void StructuralLibraryEdit_RebuildsProfileIndexBeforeSelectingNewAction()
        {
            CharacterActionAuthoringDocumentSession session = LoadCodexSession();
            CharacterActionAuthoringActionLibraryDocument library = session.CurrentActionLibrary;
            CharacterActionJsonDto[] original = library.Document.Actions;
            int originalProfileActionCount = session.ProfileActions.Count;
            var expanded = new CharacterActionJsonDto[original.Length + 1];
            Array.Copy(original, expanded, original.Length);
            expanded[expanded.Length - 1] = new CharacterActionJsonDto
            {
                ActionId = 999002,
                DisplayName = "Session New Action",
            };
            library.Document.Actions = expanded;

            Assert.IsTrue(session.SelectAction(999002));
            session.MarkCurrentActionChanged();

            Assert.AreEqual(originalProfileActionCount + 1, session.ProfileActions.Count);
            Assert.AreSame(expanded[expanded.Length - 1], session.CurrentAction);
            Assert.IsTrue(library.State.IsDirty);
            Assert.AreEqual(1, library.State.Revision);
        }

        [Test]
        public void Validate_UsesCompositeActionSetAndLibraryGraph()
        {
            CharacterActionAuthoringDocumentSession session = LoadCodexSession();

            CharacterActionAuthoringExportResult result = session.Validate(
                CharacterActionAuthoringReferenceContext.CreateDefault(),
                buildWorkstationSnapshots: false);

            Assert.IsTrue(result.ImportResult.Success, result.DiagnosticsText);
            Assert.AreEqual(
                CharacterActionJsonImporter.CurrentActionLibrarySchemaVersion,
                (string)JObject.Parse(result.ActionsJson)["schemaVersion"]);
            Assert.IsTrue(result.Registry.TryGetActionSet("codex_flow_test.empty_empty", out _));
            Assert.IsTrue(result.Registry.TryGetActionSet("codex_flow_test.sword_empty", out _));
            Assert.Greater(session.ProfileActions.Count, 0);
            for (int i = 0; i < session.ProfileActions.Count; i++)
            {
                CharacterActionJsonDto action = session.ProfileActions[i].Action;
                Assert.IsTrue(
                    result.Registry.TryGetAction(new CharacterActionId(action.ActionId), out _),
                    "Missing merged action #" + action.ActionId + ".");
            }
        }

        [Test]
        public void Save_EditSecondActionSet_PreservesSiblingAndProfileReferences()
        {
            string root = Path.Combine(
                "Temp",
                "CharacterActionDocumentSessionTests",
                Guid.NewGuid().ToString("N"));
            string profileDirectory = Path.Combine(root, "Profiles", "codex_flow_test");
            string actionSetDirectory = Path.Combine(root, "ActionSets", "codex_flow_test");
            string characterLibraryDirectory = Path.Combine(root, "Libraries", "codex_flow_test");
            string commonLibraryDirectory = Path.Combine(root, "Libraries", "Common");

            try
            {
                CopyDirectoryFiles(ProfileDirectory, profileDirectory);
                CopyDirectoryFiles(ActionSetDirectory, actionSetDirectory);
                CopyDirectoryFiles(CharacterLibraryDirectory, characterLibraryDirectory);
                CopyDirectoryFiles(CommonLibraryDirectory, commonLibraryDirectory);

                var profileSource = new CharacterActionAuthoringSourceEntry(
                    profileDirectory,
                    "codex_flow_test");
                var actionSetSource = new CharacterActionAuthoringSourceEntry(
                    actionSetDirectory,
                    "codex_flow_test");
                var characterLibrarySource = new CharacterActionAuthoringSourceEntry(
                    characterLibraryDirectory,
                    "codex_flow_test");
                var commonLibrarySource = new CharacterActionAuthoringSourceEntry(
                    commonLibraryDirectory,
                    "Common");

                JToken originalProfile = JToken.Parse(File.ReadAllText(profileSource.ProfilePath));
                JToken originalActionSets = JToken.Parse(File.ReadAllText(actionSetSource.ActionSetsPath));
                JToken originalSibling = originalActionSets["actionSets"]?[0]?.DeepClone();

                CharacterActionAuthoringDocumentSession session =
                    CharacterActionAuthoringDocumentSession.Load(
                        profileSource,
                        new[] { actionSetSource },
                        new[] { characterLibrarySource, commonLibrarySource });
                Assert.IsTrue(session.SelectActionSet("codex_flow_test.sword_empty"));
                CharacterActionAuthoringActionSetBundleDocument bundle =
                    session.FindActionSetBundle(session.Selection.ActionSetStableId);
                Assert.NotNull(bundle);

                session.CurrentActionSet.DisplayName = "剑 / 空手（文档会话测试）";
                session.MarkCurrentActionSetChanged();

                Assert.IsTrue(bundle.State.IsDirty);
                Assert.AreEqual(1, bundle.State.Revision);
                string[] savedPaths = session.SaveAllDirty();

                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        actionSetSource.ActionSetsPath,
                        actionSetSource.ReactionProfilesPath,
                        actionSetSource.LandingProfilesPath,
                    },
                    savedPaths);
                Assert.IsFalse(bundle.State.IsDirty);
                JToken savedActionSets = JToken.Parse(File.ReadAllText(actionSetSource.ActionSetsPath));
                Assert.IsTrue(JToken.DeepEquals(originalSibling, savedActionSets["actionSets"]?[0]));
                Assert.AreEqual(
                    "剑 / 空手（文档会话测试）",
                    (string)savedActionSets["actionSets"]?[1]?["displayName"]);
                Assert.IsTrue(JToken.DeepEquals(
                    originalProfile["profiles"]?[0]?["actionSetIds"],
                    JToken.Parse(File.ReadAllText(profileSource.ProfilePath))["profiles"]?[0]?["actionSetIds"]));
                Assert.AreEqual(
                    (string)originalActionSets["schemaVersion"],
                    (string)savedActionSets["schemaVersion"]);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void DirtyDrafts_RestoreAllDirtyDocumentsAndStableSelection()
        {
            using (var graph = new TemporaryDocumentGraph())
            {
                CharacterActionAuthoringDocumentSession edited = graph.Load();
                int originalProfileActionCount = edited.ProfileActions.Count;
                JToken untouchedActionSet = JToken.Parse(
                    edited.ActionSetBundles[0].ActionSetsJson)["actionSets"]?[0]?.DeepClone();

                edited.CurrentProfile.DisplayName = "Draft Profile";
                edited.MarkChanged(edited.ProfileDocument.State.Key);

                Assert.IsTrue(edited.SelectActionSet("codex_flow_test.sword_empty"));
                edited.CurrentActionSet.DisplayName = "Draft ActionSet";
                edited.MarkCurrentActionSetChanged();

                CharacterActionAuthoringActionLibraryDocument common = edited.ActionLibraries[1];
                int commonActionId = common.Document.Actions[0].ActionId;
                Assert.IsTrue(edited.SelectAction(commonActionId));
                edited.CurrentAction.DisplayName = "Draft Common Action";
                edited.MarkCurrentActionChanged();
                edited.SelectTimelineItem("draft.timeline.item");

                string payload = edited.SerializeDirtyDrafts();
                JObject payloadRoot = JObject.Parse(payload);
                JArray entries = (JArray)payloadRoot["documents"];
                Assert.AreEqual(3, entries.Count);
                for (int i = 0; i < entries.Count; i++)
                {
                    Assert.AreEqual(64, ((string)entries[i]["baselineSha256"])?.Length);
                    Assert.IsNotEmpty((string)entries[i]["sourcePath"]);
                    Assert.NotNull(entries[i]["kind"]);
                    Assert.IsInstanceOf<JObject>(entries[i]["content"]);
                }

                CharacterActionAuthoringDocumentSession restored = graph.Load();
                Assert.IsTrue(restored.RestoreDirtyDrafts(payload, out string[] conflicts));
                Assert.AreEqual(0, conflicts.Length);

                Assert.AreEqual("codex_flow_test.default", restored.Selection.ProfileStableId);
                Assert.AreEqual("codex_flow_test.sword_empty", restored.Selection.ActionSetStableId);
                Assert.AreEqual("Common", restored.Selection.ActionLibraryId);
                Assert.AreEqual(commonActionId, restored.Selection.ActionId);
                Assert.AreEqual("draft.timeline.item", restored.Selection.TimelineItemStableId);
                Assert.AreEqual("Draft Profile", restored.CurrentProfile.DisplayName);
                Assert.AreEqual("Draft ActionSet", restored.CurrentActionSet.DisplayName);
                Assert.AreEqual("Draft Common Action", restored.CurrentAction.DisplayName);
                Assert.AreEqual(originalProfileActionCount, restored.ProfileActions.Count);

                int dirtyCount = 0;
                for (int i = 0; i < restored.DocumentStates.Count; i++)
                {
                    if (restored.DocumentStates[i].IsDirty)
                    {
                        dirtyCount++;
                        Assert.Greater(restored.DocumentStates[i].Revision, 0);
                    }
                }
                Assert.AreEqual(3, dirtyCount);
                Assert.IsTrue(JToken.DeepEquals(
                    untouchedActionSet,
                    JToken.Parse(restored.ActionSetBundles[0].ActionSetsJson)["actionSets"]?[0]));
            }
        }

        [Test]
        public void DirtyDrafts_SourceBaselineChanged_SkipsOnlyConflict()
        {
            using (var graph = new TemporaryDocumentGraph())
            {
                CharacterActionAuthoringDocumentSession edited = graph.Load();
                CharacterActionAuthoringSourceEntry editedActionLibrarySource =
                    edited.CurrentActionLibrary.Source;
                int actionId = edited.CurrentAction.ActionId;
                edited.CurrentProfile.DisplayName = "Restorable Profile Draft";
                edited.MarkChanged(edited.ProfileDocument.State.Key);
                edited.CurrentAction.DisplayName = "Pending Draft";
                edited.MarkCurrentActionChanged();
                string payload = edited.SerializeDirtyDrafts();

                JObject external = JObject.Parse(File.ReadAllText(editedActionLibrarySource.ActionsPath));
                JArray externalActions = (JArray)external["actions"];
                externalActions[0]["displayName"] = "External Change";
                File.WriteAllText(
                    editedActionLibrarySource.ActionsPath,
                    external.ToString());

                CharacterActionAuthoringDocumentSession restored = graph.Load();
                Assert.IsTrue(restored.RestoreDirtyDrafts(payload, out string[] conflicts));
                CollectionAssert.AreEqual(
                    new[] { editedActionLibrarySource.ActionsPath.Replace('\\', '/') },
                    conflicts);
                Assert.IsTrue(restored.SelectAction(actionId));
                Assert.AreEqual("Restorable Profile Draft", restored.CurrentProfile.DisplayName);
                Assert.IsTrue(restored.ProfileDocument.State.IsDirty);
                Assert.AreEqual("External Change", restored.CurrentAction.DisplayName);
                Assert.IsFalse(restored.CurrentActionLibrary.State.IsDirty);
                Assert.AreEqual(0, restored.CurrentActionLibrary.State.Revision);
            }
        }

        [Test]
        public void DirtyDrafts_EmptyOrMalformedPayload_IsSafeAndReported()
        {
            CharacterActionAuthoringDocumentSession session = LoadCodexSession();

            Assert.IsTrue(session.RestoreDirtyDrafts(string.Empty, out string[] emptyConflicts));
            Assert.AreEqual(0, emptyConflicts.Length);
            Assert.IsFalse(session.RestoreDirtyDrafts("{bad", out string[] malformedConflicts));
            Assert.AreEqual(0, malformedConflicts.Length);
            Assert.IsFalse(session.RestoreDirtyDrafts("{}", out string[] incompleteConflicts));
            Assert.AreEqual(0, incompleteConflicts.Length);
            Assert.IsFalse(session.RestoreDirtyDrafts(
                "{\"version\":\"not-a-number\",\"selection\":{},\"documents\":[]}",
                out string[] invalidTypeConflicts));
            Assert.AreEqual(0, invalidTypeConflicts.Length);

            JObject invalidSelection = JObject.Parse(session.SerializeDirtyDrafts());
            invalidSelection["selection"]["actionId"] = new JArray("not-a-string");
            Assert.IsFalse(session.RestoreDirtyDrafts(
                invalidSelection.ToString(),
                out string[] invalidSelectionConflicts));
            Assert.AreEqual(0, invalidSelectionConflicts.Length);

            JObject invalidContent = JObject.Parse(session.SerializeDirtyDrafts());
            invalidContent["documents"] = new JArray
            {
                new JObject
                {
                    ["kind"] = (int)CharacterActionAuthoringDocumentKind.Profile,
                    ["sourcePath"] = session.ProfileDocument.State.Key.SourcePath,
                    ["baselineSha256"] = session.ProfileDocument.State.BaselineSha256,
                    ["revision"] = 1,
                    ["content"] = new JObject(),
                },
            };
            Assert.IsFalse(session.RestoreDirtyDrafts(
                invalidContent.ToString(),
                out string[] invalidContentConflicts));
            Assert.AreEqual(0, invalidContentConflicts.Length);

            for (int i = 0; i < session.DocumentStates.Count; i++)
            {
                Assert.IsFalse(session.DocumentStates[i].IsDirty);
                Assert.AreEqual(0, session.DocumentStates[i].Revision);
            }
        }

        private static CharacterActionAuthoringDocumentSession LoadCodexSession()
        {
            var profile = new CharacterActionAuthoringSourceEntry(ProfileDirectory, "codex_flow_test");
            var actionSet = new CharacterActionAuthoringSourceEntry(ActionSetDirectory, "codex_flow_test");
            var characterLibrary = new CharacterActionAuthoringSourceEntry(
                CharacterLibraryDirectory,
                "codex_flow_test");
            var commonLibrary = new CharacterActionAuthoringSourceEntry(CommonLibraryDirectory, "Common");
            return CharacterActionAuthoringDocumentSession.Load(
                profile,
                new[] { actionSet },
                new[] { characterLibrary, commonLibrary });
        }

        private static int CountReferencedActions(CharacterActionAuthoringDocumentSession session)
        {
            int count = 0;
            string[] referenced = session.CurrentProfile?.ActionLibraryIds ?? Array.Empty<string>();
            for (int i = 0; i < session.ActionLibraries.Count; i++)
            {
                CharacterActionAuthoringActionLibraryDocument library = session.ActionLibraries[i];
                for (int j = 0; j < referenced.Length; j++)
                {
                    if (!string.Equals(library.LibraryId, referenced[j], StringComparison.Ordinal))
                        continue;
                    count += library.Document.Actions?.Length ?? 0;
                    break;
                }
            }

            return count;
        }

        private static void CopyDirectoryFiles(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            string[] files = Directory.GetFiles(sourceDirectory, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                File.Copy(
                    files[i],
                    Path.Combine(destinationDirectory, Path.GetFileName(files[i])),
                    overwrite: true);
            }
        }

        private sealed class TemporaryDocumentGraph : IDisposable
        {
            internal TemporaryDocumentGraph()
            {
                Root = Path.Combine(
                    "Temp",
                    "CharacterActionDocumentSessionDraftTests",
                    Guid.NewGuid().ToString("N"));
                string profileDirectory = Path.Combine(Root, "Profiles", "codex_flow_test");
                string actionSetDirectory = Path.Combine(Root, "ActionSets", "codex_flow_test");
                string characterLibraryDirectory = Path.Combine(Root, "Libraries", "codex_flow_test");
                string commonLibraryDirectory = Path.Combine(Root, "Libraries", "Common");
                CopyDirectoryFiles(ProfileDirectory, profileDirectory);
                CopyDirectoryFiles(ActionSetDirectory, actionSetDirectory);
                CopyDirectoryFiles(CharacterLibraryDirectory, characterLibraryDirectory);
                CopyDirectoryFiles(CommonLibraryDirectory, commonLibraryDirectory);
                ProfileSource = new CharacterActionAuthoringSourceEntry(
                    profileDirectory,
                    "codex_flow_test");
                ActionSetSource = new CharacterActionAuthoringSourceEntry(
                    actionSetDirectory,
                    "codex_flow_test");
                CharacterLibrarySource = new CharacterActionAuthoringSourceEntry(
                    characterLibraryDirectory,
                    "codex_flow_test");
                CommonLibrarySource = new CharacterActionAuthoringSourceEntry(
                    commonLibraryDirectory,
                    "Common");
            }

            internal string Root { get; }
            internal CharacterActionAuthoringSourceEntry ProfileSource { get; }
            internal CharacterActionAuthoringSourceEntry ActionSetSource { get; }
            internal CharacterActionAuthoringSourceEntry CharacterLibrarySource { get; }
            internal CharacterActionAuthoringSourceEntry CommonLibrarySource { get; }

            internal CharacterActionAuthoringDocumentSession Load()
            {
                return CharacterActionAuthoringDocumentSession.Load(
                    ProfileSource,
                    new[] { ActionSetSource },
                    new[] { CharacterLibrarySource, CommonLibrarySource });
            }

            public void Dispose()
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
        }
    }
}

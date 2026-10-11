using System;
using System.IO;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionAuthoringReactionBundleScopeTests
    {
        [Test]
        public void ReactionPicker_UsesOnlyCurrentActionSetBundle()
        {
            using (var graph = new TemporaryReactionBundleGraph())
            {
                CharacterActionAuthoringDocumentSession session = graph.Load();

                Assert.IsTrue(session.SelectActionSet("set.a"));
                CharacterActionAuthoringSearchCatalog bundleACatalog =
                    CharacterActionAuthoringSearchCatalogBuilder.BuildReactionProfiles(session, null);
                AssertReactionCatalog(bundleACatalog, "Bundle A Shared", "reaction.only_a", "reaction.only_b");

                Assert.IsTrue(session.SelectActionSet("set.b"));
                CharacterActionAuthoringSearchCatalog bundleBCatalog =
                    CharacterActionAuthoringSearchCatalogBuilder.BuildReactionProfiles(session, null);
                AssertReactionCatalog(bundleBCatalog, "Bundle B Shared", "reaction.only_b", "reaction.only_a");
            }
        }

        [Test]
        public void ReactionLookupAndRename_EditCurrentBundleDtoAndDirtyOnlyItsDocument()
        {
            using (var graph = new TemporaryReactionBundleGraph())
            {
                CharacterActionAuthoringDocumentSession session = graph.Load();
                Assert.IsTrue(session.SelectActionSet("set.b"));
                CharacterActionAuthoringActionSetBundleDocument bundleA = session.ActionSetBundles[0];
                CharacterActionAuthoringActionSetBundleDocument bundleB = session.ActionSetBundles[1];
                CharacterReactionProfileJsonDto reactionA =
                    bundleA.ReactionProfilesDocument.ReactionProfiles[0];
                CharacterReactionProfileJsonDto reactionB =
                    bundleB.ReactionProfilesDocument.ReactionProfiles[0];
                CharacterActionAuthoringModel model = CreateAggregatedModel(session);
                CharacterActionAuthoringWindow window =
                    ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
                try
                {
                    SetField(window, "_documentSession", session);
                    SetField(window, "_model", model);

                    CharacterReactionProfileJsonDto selected =
                        (CharacterReactionProfileJsonDto)Invoke(
                            window,
                            "FindReactionProfile",
                            "reaction.shared");
                    Assert.AreSame(reactionB, selected);
                    Assert.AreNotSame(reactionA, selected);

                    Invoke(
                        window,
                        "ApplyActionSetDocumentChange",
                        new Action(() => Invoke(
                            window,
                            "RenameReactionProfile",
                            model.ActionSet,
                            selected,
                            "reaction.bundle_b.renamed")),
                        EnumValue(window, "ApplyActionSetDocumentChange", 1, "Content"));

                    Assert.AreEqual("reaction.shared", reactionA.StableId);
                    Assert.AreEqual("reaction.bundle_b.renamed", reactionB.StableId);
                    Assert.AreEqual("reaction.bundle_b.renamed", model.ActionSet.ReactionProfileId);
                    Assert.IsFalse(bundleA.State.IsDirty);
                    Assert.IsTrue(bundleB.State.IsDirty);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(window);
                }
            }
        }

        [Test]
        public void ReactionProblemNavigation_UsesSourcePathAndReactionIdToSelectOwnerActionSet()
        {
            using (var graph = new TemporaryReactionBundleGraph())
            {
                CharacterActionAuthoringDocumentSession session = graph.Load();
                Assert.AreEqual("set.a", session.Selection.ActionSetStableId);
                CharacterActionAuthoringProblemLocation location =
                    CharacterActionAuthoringProblemLocationParser.Parse(
                        graph.BundleB.ReactionProfilesPath,
                        "reactionProfiles[reaction.shared].rules[0].actionId");

                bool resolved = CharacterActionAuthoringWindow.TryResolveReactionProblemActionSet(
                    location,
                    session,
                    session.CurrentActionSet,
                    out string actionSetStableId);

                Assert.IsTrue(resolved);
                Assert.AreEqual("set.b", actionSetStableId);
            }
        }

        [Test]
        public void AbilityPicker_UsesOnlyEffectiveAbilitiesAndPreservesUnknownCurrentValue()
        {
            using (var graph = new TemporaryReactionBundleGraph())
            {
                CharacterActionAuthoringDocumentSession session = graph.Load();
                Assert.IsTrue(session.SelectActionSet("set.b"));
                CharacterActionAuthoringSearchCatalog catalog =
                    CharacterActionAuthoringSearchCatalogBuilder.BuildAbilities(
                        session,
                        CreateAggregatedModel(session),
                        new[] { 333 });

                Assert.AreEqual(1, catalog.Candidates.Count);
                Assert.AreEqual("333", catalog.Candidates[0].Value);
                CharacterActionAuthoringSearchProjection projection = catalog.Project(
                    CharacterActionAuthoringSearchCategory.Ability,
                    string.Empty,
                    "222");
                Assert.IsTrue(projection.HasUnknownCurrentValue);
                Assert.AreEqual("222", projection.Candidates[0].Value);
                Assert.IsFalse(projection.Candidates[0].IsKnown);
            }
        }

        private static void AssertReactionCatalog(
            CharacterActionAuthoringSearchCatalog catalog,
            string expectedSharedDisplayName,
            string expectedUniqueId,
            string forbiddenUniqueId)
        {
            Assert.AreEqual(2, catalog.Candidates.Count);
            bool foundShared = false;
            bool foundExpectedUnique = false;
            for (int i = 0; i < catalog.Candidates.Count; i++)
            {
                CharacterActionAuthoringSearchCandidate candidate = catalog.Candidates[i];
                if (string.Equals(candidate.Value, "reaction.shared", StringComparison.Ordinal))
                {
                    foundShared = true;
                    Assert.AreEqual(expectedSharedDisplayName, candidate.DisplayName);
                }
                foundExpectedUnique |= string.Equals(
                    candidate.Value,
                    expectedUniqueId,
                    StringComparison.Ordinal);
                Assert.AreNotEqual(forbiddenUniqueId, candidate.Value);
            }
            Assert.IsTrue(foundShared);
            Assert.IsTrue(foundExpectedUnique);
        }

        private static CharacterActionAuthoringModel CreateAggregatedModel(
            CharacterActionAuthoringDocumentSession session)
        {
            var model = new CharacterActionAuthoringModel
            {
                Profile = session.CurrentProfile,
                ActionSet = session.CurrentActionSet,
                ReferenceContext = CharacterActionAuthoringReferenceContext.CreateDefault(),
            };
            for (int i = 0; i < session.ActionSetBundles.Count; i++)
            {
                model.ReactionProfiles.AddRange(
                    session.ActionSetBundles[i].ReactionProfilesDocument.ReactionProfiles);
            }
            return model;
        }

        private static object Invoke(object instance, string methodName, params object[] arguments)
        {
            MethodInfo[] methods = instance.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                if (!string.Equals(methods[i].Name, methodName, StringComparison.Ordinal)
                    || methods[i].GetParameters().Length != arguments.Length)
                {
                    continue;
                }
                return methods[i].Invoke(instance, arguments);
            }
            Assert.Fail("Missing method: " + methodName);
            return null;
        }

        private static object EnumValue(
            object instance,
            string methodName,
            int parameterIndex,
            string value)
        {
            MethodInfo[] methods = instance.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                if (!string.Equals(methods[i].Name, methodName, StringComparison.Ordinal))
                    continue;
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (parameterIndex >= 0
                    && parameterIndex < parameters.Length
                    && parameters[parameterIndex].ParameterType.IsEnum)
                {
                    return Enum.Parse(parameters[parameterIndex].ParameterType, value);
                }
            }
            Assert.Fail("Missing enum parameter: " + methodName);
            return null;
        }

        private static void SetField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            field.SetValue(instance, value);
        }

        private sealed class TemporaryReactionBundleGraph : IDisposable
        {
            internal TemporaryReactionBundleGraph()
            {
                Root = Path.Combine(
                    "Temp",
                    "CharacterActionReactionBundleScopeTests",
                    Guid.NewGuid().ToString("N"));
                string profileDirectory = Path.Combine(Root, "Profile");
                string bundleADirectory = Path.Combine(Root, "BundleA");
                string bundleBDirectory = Path.Combine(Root, "BundleB");
                Directory.CreateDirectory(profileDirectory);
                Directory.CreateDirectory(bundleADirectory);
                Directory.CreateDirectory(bundleBDirectory);

                Profile = new CharacterActionAuthoringSourceEntry(profileDirectory, "Profile");
                BundleA = new CharacterActionAuthoringSourceEntry(bundleADirectory, "Bundle A");
                BundleB = new CharacterActionAuthoringSourceEntry(bundleBDirectory, "Bundle B");

                Write(Profile.ProfilePath, new CharacterActionProfilesJsonDocument
                {
                    SchemaVersion = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                    Profiles = new[]
                    {
                        new CharacterActionProfileJsonDto
                        {
                            Id = 1,
                            StableId = "profile.test",
                            DisplayName = "Test",
                            ActionSetIds = new[] { "set.a", "set.b" },
                            ActionLibraryIds = Array.Empty<string>(),
                        },
                    },
                });
                WriteBundle(
                    BundleA,
                    "set.a",
                    111,
                    "Bundle A Shared",
                    "reaction.only_a");
                WriteBundle(
                    BundleB,
                    "set.b",
                    222,
                    "Bundle B Shared",
                    "reaction.only_b");
            }

            internal string Root { get; }
            internal CharacterActionAuthoringSourceEntry Profile { get; }
            internal CharacterActionAuthoringSourceEntry BundleA { get; }
            internal CharacterActionAuthoringSourceEntry BundleB { get; }

            internal CharacterActionAuthoringDocumentSession Load()
            {
                return CharacterActionAuthoringDocumentSession.Load(
                    Profile,
                    new[] { BundleA, BundleB },
                    Array.Empty<CharacterActionAuthoringSourceEntry>());
            }

            public void Dispose()
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }

            private static void WriteBundle(
                CharacterActionAuthoringSourceEntry source,
                string actionSetId,
                int abilityId,
                string sharedReactionDisplayName,
                string uniqueReactionId)
            {
                Write(source.ActionSetsPath, new CharacterActionSetsJsonDocument
                {
                    SchemaVersion = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                    ActionSets = new[]
                    {
                        new CharacterActionSetJsonDto
                        {
                            Id = abilityId,
                            StableId = actionSetId,
                            DisplayName = actionSetId,
                            ReactionProfileId = "reaction.shared",
                            AbilityBindings = new[]
                            {
                                new CharacterAbilityActionBindingJsonDto
                                {
                                    AbilityId = abilityId,
                                    ActionId = 901001,
                                },
                            },
                        },
                    },
                });
                Write(source.ReactionProfilesPath, new CharacterReactionProfilesJsonDocument
                {
                    SchemaVersion = CharacterActionJsonImporter.CurrentProjectionSchemaVersion,
                    ReactionProfiles = new[]
                    {
                        new CharacterReactionProfileJsonDto
                        {
                            StableId = "reaction.shared",
                            DisplayName = sharedReactionDisplayName,
                        },
                        new CharacterReactionProfileJsonDto
                        {
                            StableId = uniqueReactionId,
                            DisplayName = uniqueReactionId,
                        },
                    },
                });
            }

            private static void Write(string path, object document)
            {
                File.WriteAllText(
                    path,
                    JsonConvert.SerializeObject(
                        document,
                        CharacterActionAuthoringJsonService.JsonSettings));
            }
        }
    }
}

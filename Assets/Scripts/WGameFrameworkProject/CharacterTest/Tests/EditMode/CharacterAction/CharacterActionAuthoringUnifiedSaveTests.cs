using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using MxFramework.Editor.CharacterAction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionAuthoringUnifiedSaveTests
    {
        [Test]
        public void SaveScope_ReusesPreparedContentAndInvalidatesAfterAcceptEditOrRestore()
        {
            int reads = 0;
            string json = "{\"value\":1}";
            CharacterActionAuthoringDocumentState state = null;
            state = new CharacterActionAuthoringDocumentState(
                new CharacterActionAuthoringDocumentKey(CharacterActionAuthoringDocumentKind.ActionLibrary, "library.json"),
                () => { reads++; return json; },
                () => new[] { new CharacterActionAuthoringPreparedDocumentWrite("library.json", state.SerializeCurrentState()) },
                writes => json = writes[0].Contents);
            reads = 0;
            json = "{\"value\":2}";
            using (CharacterActionAuthoringDocumentState.BeginDirtyReadScope())
            {
                Assert.IsTrue(state.IsDirty);
                CharacterActionAuthoringPreparedDocumentWrite[] writes = state.PrepareWrites();
                Assert.AreEqual(json, writes[0].Contents);
                Assert.AreEqual(1, reads);
                state.AcceptPreparedWrites(writes);
                Assert.IsFalse(state.IsDirty);
                Assert.AreEqual(json, state.SerializeCurrentState());
                Assert.AreEqual(2, reads);
                json = "{\"value\":3}";
                state.MarkChanged();
                Assert.IsTrue(state.IsDirty);
                Assert.AreEqual(json, state.PrepareWrites()[0].Contents);
                Assert.AreEqual(3, reads);
                json = "{\"value\":2}";
                state.RestoreRevision(1);
                Assert.IsFalse(state.IsDirty);
                Assert.AreEqual(4, reads);
            }
            json = "{\"value\":4}";
            Assert.IsTrue(state.IsDirty, "Direct edits outside the save scope must remain detectable.");
            Assert.AreEqual(5, reads);
        }

        [Test]
        public void SaveScope_CollectsSupportedDirtyDocumentsOnceInExistingSaveOrder()
        {
            var kinds = new[] { CharacterActionAuthoringDocumentKind.Profile, CharacterActionAuthoringDocumentKind.ActionSetBundle,
                CharacterActionAuthoringDocumentKind.ActionLibrary, CharacterActionAuthoringDocumentKind.ActionLibrary,
                CharacterActionAuthoringDocumentKind.AbilityEffectTimeline, CharacterActionAuthoringDocumentKind.Profile };
            var paths = new[] { "profile", "set", "z-library", "a-library", "ignored", "clean" };
            var json = new string[kinds.Length];
            var reads = new int[kinds.Length];
            var states = new List<CharacterActionAuthoringDocumentState>();
            for (int i = 0; i < kinds.Length; i++)
            {
                int index = i;
                json[index] = "{\"value\":1}";
                states.Add(new CharacterActionAuthoringDocumentState(
                    new CharacterActionAuthoringDocumentKey(kinds[index], paths[index]),
                    () => { reads[index]++; return json[index]; },
                    () => Array.Empty<CharacterActionAuthoringPreparedDocumentWrite>(), _ => { }));
                reads[index] = 0;
                if (i < kinds.Length - 1) json[index] = "{\"value\":2}";
            }
            using (CharacterActionAuthoringDocumentState.BeginDirtyReadScope())
            {
                List<CharacterActionAuthoringDocumentState> dirty = CharacterActionAuthoringWindow.CollectDirtySaveStates(states);
                CollectionAssert.AreEqual(new[] { states[3], states[2], states[1], states[0] }, dirty);
                CharacterActionAuthoringWindow.CollectDirtySaveStates(states);
                CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 0, 1 }, reads);
            }
        }

        [Test]
        public void SaveScope_FailedWriteKeepsCachedDraftDirty()
        {
            string path = Path.Combine(Path.GetTempPath(), "MxSaveScope-" + Guid.NewGuid().ToString("N") + ".json");
            string json = "{\"value\":1}";
            int reads = 0;
            CharacterActionAuthoringDocumentState state = null;
            state = new CharacterActionAuthoringDocumentState(
                new CharacterActionAuthoringDocumentKey(CharacterActionAuthoringDocumentKind.ActionLibrary, path),
                () => { reads++; return json; },
                () => new[] { new CharacterActionAuthoringPreparedDocumentWrite(path, state.SerializeCurrentState()) }, _ => { });
            json = "{\"value\":2}";
            reads = 0;
            using (CharacterActionAuthoringDocumentState.BeginDirtyReadScope())
            {
                Assert.IsTrue(state.IsDirty);
                var writes = state.PrepareWrites();
                var plan = new CharacterActionAuthoringUnifiedSavePlan((_, __) => throw new IOException("simulated write failure"));
                plan.AddPreparedFiles(CharacterActionAuthoringUnifiedSaveStage.ActionLibrary, path, writes,
                    () => state.AcceptPreparedWrites(writes));
                Assert.IsFalse(plan.TryExecute(out _));
                Assert.IsTrue(state.IsDirty);
                Assert.AreEqual(json, state.SerializeCurrentState());
                Assert.AreEqual(1, reads);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CommonProjection_DeletionRemovesBaselineActionsAndPreservesSourceOnlyActions(bool deleteAll)
        {
            const string source = "{\"schemaVersion\":\"0.3.0\",\"customMetadata\":\"keep\",\"actions\":[{\"actionId\":1,\"displayName\":\"A\"},{\"actionId\":2,\"displayName\":\"B\",\"customField\":\"keep\"},{\"actionId\":3,\"displayName\":\"Source only\"}]}";
            const string baseline = "{\"schemaVersion\":\"0.5.0\",\"actions\":[{\"actionId\":1,\"displayName\":\"A\"},{\"actionId\":2,\"displayName\":\"B\"}]}";
            string current = deleteAll
                ? "{\"schemaVersion\":\"0.5.0\",\"actions\":[]}"
                : "{\"schemaVersion\":\"0.5.0\",\"actions\":[{\"actionId\":2,\"displayName\":\"B\"}]}";

            string merged = CharacterActionCommonSourceProjection.MergeChangedActions(source, baseline, current);
            JObject result = JObject.Parse(merged);
            JArray actions = (JArray)result["actions"];
            Assert.AreEqual(deleteAll ? 1 : 2, actions.Count);
            Assert.AreEqual(3, actions.Last["actionId"].Value<int>());
            Assert.AreEqual("keep", result["customMetadata"].Value<string>());
            if (!deleteAll)
                Assert.AreEqual("keep", actions[0]["customField"].Value<string>());
            Assert.IsTrue(JToken.DeepEquals(result, JObject.Parse(
                CharacterActionCommonSourceProjection.MergeChangedActions(merged, current, current))));
        }

        [Test]
        public void SavePlan_OneCommonWindowEditReopensFromBothFilesWithoutChangingSiblings()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CommonReopen-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string sourcePath = Path.Combine(directory, "a-source.json");
            string projectionPath = Path.Combine(directory, "z-projection.json");
            const string source = "{\"schemaVersion\":\"0.3.0\",\"actions\":[{\"actionId\":458,\"combatTrack\":{\"events\":[{\"kind\":\"HitTraceWindow\",\"frame\":12,\"hitEvent\":{\"damage\":{\"baseDamage\":6,\"impactPower\":34}}},{\"kind\":\"HitTraceWindow\",\"frame\":20,\"hitEvent\":{\"hitSameTargetPolicy\":\"AllowRepeat\",\"damage\":{\"baseDamage\":6,\"impactPower\":34}}},{\"kind\":\"HitTraceWindow\",\"frame\":26,\"hitEvent\":{\"damage\":{\"baseDamage\":6,\"impactPower\":34}}}]}}]}";
            string baselineProjection = source.Replace("0.3.0", "0.5.0");
            File.WriteAllText(sourcePath, source);
            File.WriteAllText(projectionPath, baselineProjection);
            bool dirty = true;
            try
            {
                JObject edited = JObject.Parse(baselineProjection);
                edited["actions"][0]["combatTrack"]["events"][1]["hitEvent"]["damage"]["baseDamage"] = 7;
                string sourceCandidate = CharacterActionCommonSourceProjection.MergeChangedActions(
                    source, baselineProjection, edited.ToString());
                var plan = new CharacterActionAuthoringUnifiedSavePlan();
                plan.AddPreparedFiles(CharacterActionAuthoringUnifiedSaveStage.ActionLibrary, sourcePath,
                    new[]
                    {
                        new CharacterActionAuthoringPreparedDocumentWrite(sourcePath, sourceCandidate, FileHash(sourcePath)),
                        new CharacterActionAuthoringPreparedDocumentWrite(projectionPath, edited.ToString(), FileHash(projectionPath)),
                    }, () => dirty = false);
                Assert.IsTrue(plan.TryExecute(out CharacterActionAuthoringUnifiedSaveFailure failure), failure.Message);
                Assert.IsFalse(dirty);
                foreach (string path in new[] { sourcePath, projectionPath })
                {
                    JArray windows = (JArray)JObject.Parse(File.ReadAllText(path))["actions"][0]["combatTrack"]["events"];
                    Assert.AreEqual(6, windows[0]["hitEvent"]["damage"]["baseDamage"].Value<int>());
                    Assert.AreEqual(7, windows[1]["hitEvent"]["damage"]["baseDamage"].Value<int>());
                    Assert.AreEqual(6, windows[2]["hitEvent"]["damage"]["baseDamage"].Value<int>());
                    Assert.AreEqual(34, windows[1]["hitEvent"]["damage"]["impactPower"].Value<int>());
                    Assert.AreEqual("AllowRepeat", windows[1]["hitEvent"]["hitSameTargetPolicy"].Value<string>());
                }
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void SavePlan_CommonPairFailureRestoresBothFilesAndKeepsDraft()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CommonPair-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string source = Path.Combine(directory, "a-source.json");
            string projection = Path.Combine(directory, "z-projection.json");
            File.WriteAllText(source, "source-before");
            File.WriteAllText(projection, "projection-before");
            bool dirty = true;
            try
            {
                var plan = new CharacterActionAuthoringUnifiedSavePlan((path, contents) =>
                {
                    File.WriteAllText(path, contents);
                    if (Path.GetFileName(path) == "z-projection.json") throw new IOException("second write failed");
                });
                plan.AddPreparedFiles(CharacterActionAuthoringUnifiedSaveStage.ActionLibrary, source,
                    new[]
                    {
                        new CharacterActionAuthoringPreparedDocumentWrite(source, "source-after", FileHash(source)),
                        new CharacterActionAuthoringPreparedDocumentWrite(projection, "projection-after", FileHash(projection)),
                    }, () => dirty = false);

                Assert.IsFalse(plan.TryExecute(out _));
                Assert.AreEqual("source-before", File.ReadAllText(source));
                Assert.AreEqual("projection-before", File.ReadAllText(projection));
                Assert.IsTrue(dirty);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void SavePlan_ExternalCommonChangeRejectsBeforeAnyWrite()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CommonConflict-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string source = Path.Combine(directory, "source.json");
            File.WriteAllText(source, "source-before");
            string expectedHash = FileHash(source);
            File.WriteAllText(source, "external-change");
            bool dirty = true;
            try
            {
                var plan = new CharacterActionAuthoringUnifiedSavePlan();
                plan.AddPreparedFiles(CharacterActionAuthoringUnifiedSaveStage.ActionLibrary, source,
                    new[] { new CharacterActionAuthoringPreparedDocumentWrite(source, "draft", expectedHash) },
                    () => dirty = false);
                Assert.IsFalse(plan.TryExecute(out _));
                Assert.AreEqual("external-change", File.ReadAllText(source));
                Assert.IsTrue(dirty);
            }
            finally { Directory.Delete(directory, true); }
        }

        private static string FileHash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return Convert.ToBase64String(sha.ComputeHash(stream));
        }

        [Test]
        public void SavePlan_ExecutesStrictStageAndStablePathOrder()
        {
            var calls = new List<string>();
            var plan = new CharacterActionAuthoringUnifiedSavePlan(
                (path, contents) => calls.Add("write:" + path));

            plan.AddOperation(
                CharacterActionAuthoringUnifiedSaveStage.Profile,
                "profile/z.json",
                () => calls.Add("profile"));
            plan.AddOperation(
                CharacterActionAuthoringUnifiedSaveStage.ActionLibrary,
                "library/z.json",
                () => calls.Add("library:z"));
            plan.AddOperation(
                CharacterActionAuthoringUnifiedSaveStage.ActionLibrary,
                "library/a.json",
                () => calls.Add("library:a"));
            plan.AddOperation(
                CharacterActionAuthoringUnifiedSaveStage.ActionSetBundle,
                "action-set/a.json",
                () => calls.Add("action-set"));
            plan.AddPreparedFiles(
                CharacterActionAuthoringUnifiedSaveStage.MechanicTuning,
                "mechanic-primary",
                new[]
                {
                    new CharacterActionAuthoringPreparedDocumentWrite("mechanic/z.json", "z"),
                    new CharacterActionAuthoringPreparedDocumentWrite("mechanic/a.json", "a"),
                },
                () => calls.Add("mechanic-primary:accept"));
            plan.AddPreparedFiles(
                CharacterActionAuthoringUnifiedSaveStage.MechanicTuning,
                "mechanic",
                new[] { new CharacterActionAuthoringPreparedDocumentWrite("mechanic/a.json", "m") },
                () => calls.Add("mechanic:accept"));
            plan.AddPreparedFiles(
                CharacterActionAuthoringUnifiedSaveStage.AbilityEffectTimeline,
                "ability",
                new[] { new CharacterActionAuthoringPreparedDocumentWrite("ability/a.json", "t") },
                () => calls.Add("ability:accept"));

            Assert.That(plan.TryExecute(out CharacterActionAuthoringUnifiedSaveFailure failure), Is.True, failure.Message);
            CollectionAssert.AreEqual(
                new[]
                {
                    "library:a",
                    "library:z",
                    "action-set",
                    "write:mechanic/a.json",
                    "write:mechanic/a.json",
                    "write:mechanic/z.json",
                    "profile",
                    "write:ability/a.json",
                    "mechanic:accept",
                    "mechanic-primary:accept",
                    "ability:accept",
                },
                calls);
        }

        [Test]
        public void SavePlan_PathWriteFailure_ReportsExactPathAndKeepsBatchDirty()
        {
            var written = new List<string>();
            bool dirty = true;
            var plan = new CharacterActionAuthoringUnifiedSavePlan((path, contents) =>
            {
                written.Add(path);
                if (string.Equals(path, "mechanic/b.json", StringComparison.Ordinal))
                    throw new IOException("simulated path failure");
            });
            plan.AddPreparedFiles(
                CharacterActionAuthoringUnifiedSaveStage.MechanicTuning,
                "mechanic",
                new[]
                {
                    new CharacterActionAuthoringPreparedDocumentWrite("mechanic/a.json", "a"),
                    new CharacterActionAuthoringPreparedDocumentWrite("mechanic/b.json", "b"),
                },
                () => dirty = false);

            Assert.That(plan.TryExecute(out CharacterActionAuthoringUnifiedSaveFailure failure), Is.False);
            Assert.That(failure.SourcePath, Is.EqualTo("mechanic/b.json"));
            Assert.That(failure.Message, Does.Contain("simulated path failure"));
            Assert.That(dirty, Is.True, "The source is only accepted after every path in its batch succeeds.");
            CollectionAssert.AreEqual(new[] { "mechanic/a.json", "mechanic/b.json" }, written);
        }

        [Test]
        public void MergeAbilityEffectTimelines_PreservesProjectileAndMechanicFields()
        {
            const string baseJson = "{\"schemaVersion\":\"1\",\"projectileDefinitions\":[{\"stableId\":\"projectile.kept\"}],\"characterTestMechanicTunings\":[{\"stableId\":\"mechanic.kept\"}],\"effectTimelines\":[{\"timelineId\":\"old\"}]}";
            var timelines = new JArray(
                new JObject
                {
                    ["timelineId"] = "new",
                    ["durationFrames"] = 30,
                });

            string merged = CharacterActionAuthoringUnifiedSaveJson.MergeAbilityEffectTimelines(
                baseJson,
                timelines);
            JObject root = JObject.Parse(merged);

            Assert.That(root["projectileDefinitions"]?[0]?["stableId"]?.Value<string>(), Is.EqualTo("projectile.kept"));
            Assert.That(root["characterTestMechanicTunings"]?[0]?["stableId"]?.Value<string>(), Is.EqualTo("mechanic.kept"));
            Assert.That(root["effectTimelines"]?[0]?["timelineId"]?.Value<string>(), Is.EqualTo("new"));
        }

        [Test]
        public void StagingArea_LaterAbilityPreparationReadsPreparedProjectileJson()
        {
            string directory = Path.Combine(Path.GetTempPath(), "MxFramework-UnifiedSaveTests-" + Guid.NewGuid().ToString("N"));
            string sourcePath = Path.Combine(directory, "ability_authoring.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(sourcePath, "{\"schemaVersion\":\"1\",\"projectileDefinitions\":[]}");
            try
            {
                using (var staging = new CharacterActionAuthoringUnifiedSaveStagingArea())
                {
                    string projectileTemp = staging.StageFile("projectile", sourcePath, "ability_authoring.json");
                    File.WriteAllText(
                        projectileTemp,
                        "{\"schemaVersion\":\"1\",\"projectileDefinitions\":[{\"stableId\":\"prepared.projectile\"}]}");
                    staging.CaptureFile(projectileTemp, sourcePath);

                    string abilityBase = staging.ReadCurrentContents(sourcePath);
                    string merged = CharacterActionAuthoringUnifiedSaveJson.MergeAbilityEffectTimelines(
                        abilityBase,
                        new JArray(new JObject { ["timelineId"] = "timeline.prepared" }));
                    JObject root = JObject.Parse(merged);
                    Assert.That(root["projectileDefinitions"]?[0]?["stableId"]?.Value<string>(), Is.EqualTo("prepared.projectile"));
                    Assert.That(root["effectTimelines"]?[0]?["timelineId"]?.Value<string>(), Is.EqualTo("timeline.prepared"));
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void DocumentSaveAs_WritesSelectedDirectoryAndLeavesOriginalUntouched()
        {
            string root = Path.Combine(Path.GetTempPath(), "MxFramework-SaveAsTests-" + Guid.NewGuid().ToString("N"));
            string originalDirectory = Path.Combine(root, "original");
            string targetDirectory = Path.Combine(root, "selected-target");
            Directory.CreateDirectory(originalDirectory);
            string originalProfile = Path.Combine(originalDirectory, "character_action_profile.json");
            string originalReactions = Path.Combine(originalDirectory, "character_reaction_profiles.json");
            File.WriteAllText(originalProfile, "original-profile");
            File.WriteAllText(originalReactions, "original-reactions");
            try
            {
                string[] written = CharacterActionAuthoringDocumentSaveAs.CopyToDirectory(
                    new[]
                    {
                        new CharacterActionAuthoringPreparedDocumentWrite(originalProfile, "copied-profile"),
                        new CharacterActionAuthoringPreparedDocumentWrite(originalReactions, "copied-reactions"),
                    },
                    targetDirectory);

                Assert.That(File.ReadAllText(originalProfile), Is.EqualTo("original-profile"));
                Assert.That(File.ReadAllText(originalReactions), Is.EqualTo("original-reactions"));
                Assert.That(File.ReadAllText(Path.Combine(targetDirectory, "character_action_profile.json")), Is.EqualTo("copied-profile"));
                Assert.That(File.ReadAllText(Path.Combine(targetDirectory, "character_reaction_profiles.json")), Is.EqualTo("copied-reactions"));
                Assert.That(written, Has.Length.EqualTo(2));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }
        [Test]
        public void ResourceDraftSessionPayload_RoundTripsSourcePathBaselineAndContent()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "MxFramework-ResourceDraftSessionTests-" + Guid.NewGuid().ToString("N"));
            string sourcePath = Path.Combine(directory, "ability_authoring.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(sourcePath, "{\"schemaVersion\":\"1\"}");
            try
            {
                string baseline = CharacterActionAuthoringResourceDraftSessionPayload.ComputeSourceBaselineHash(sourcePath);
                var entry = new CharacterActionAuthoringResourceDraftSessionEntry(
                    CharacterActionAuthoringResourceDraftKind.Projectile,
                    sourcePath,
                    baseline,
                    new JObject
                    {
                        ["stableId"] = "projectile.session-test",
                        ["speedPerFrame"] = 2.5f,
                    });

                string payload = CharacterActionAuthoringResourceDraftSessionPayload.Serialize(new[] { entry });

                Assert.That(
                    CharacterActionAuthoringResourceDraftSessionPayload.TryDeserialize(
                        payload,
                        out CharacterActionAuthoringResourceDraftSessionEntry[] restored),
                    Is.True);
                Assert.That(restored, Has.Length.EqualTo(1));
                Assert.That(restored[0].Kind, Is.EqualTo(CharacterActionAuthoringResourceDraftKind.Projectile));
                Assert.That(restored[0].SourcePath, Is.EqualTo(sourcePath.Replace('\\', '/')));
                Assert.That(restored[0].BaselineSha256, Is.EqualTo(baseline));
                Assert.That(restored[0].Content["stableId"]?.Value<string>(), Is.EqualTo("projectile.session-test"));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void ResourceDraftRestore_SourceChanged_RetainsConflictWithoutRestoringDraft()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "MxFramework-ResourceDraftConflictTests-" + Guid.NewGuid().ToString("N"));
            string sourcePath = Path.Combine(directory, "ability_authoring.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(sourcePath, "{\"schemaVersion\":\"1\",\"revision\":1}");
            string baseline = CharacterActionAuthoringResourceDraftSessionPayload.ComputeSourceBaselineHash(sourcePath);
            string payload = CharacterActionAuthoringResourceDraftSessionPayload.Serialize(
                new[]
                {
                    new CharacterActionAuthoringResourceDraftSessionEntry(
                        CharacterActionAuthoringResourceDraftKind.AbilityEffectTimeline,
                        sourcePath,
                        baseline,
                        new JArray(new JObject { ["timelineId"] = "timeline.conflict" })),
                });
            File.WriteAllText(sourcePath, "{\"schemaVersion\":\"1\",\"revision\":2}");

            CharacterActionAuthoringWindow window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
            try
            {
                SetPrivateField(window, "_abilityEffectTimelineDirty", true);
                InvokePrivate(window, "RestoreResourceDraftsFromSession", payload);

                Assert.That(GetPrivateField<bool>(window, "_abilityEffectTimelineDirty"), Is.False,
                    "A conflicting timeline draft must not be restored as the active dirty draft.");
                string[] conflicts = GetPrivateField<string[]>(window, "_documentSessionDraftConflictPaths");
                CollectionAssert.Contains(conflicts, sourcePath.Replace('\\', '/'));
                string recovery = GetPrivateField<string>(window, "_conflictedDocumentSessionDraftsJson");
                Assert.That(recovery, Is.Not.Empty);
                Assert.That(
                    CharacterActionAuthoringDraftRecoveryEnvelope.TryDeserialize(
                        recovery,
                        out CharacterActionAuthoringDraftRecoveryEnvelope envelope),
                    Is.True);
                Assert.That(envelope.ResourceDraftsJson, Is.EqualTo(payload));
                CollectionAssert.Contains(envelope.ResourceConflictPaths, sourcePath.Replace('\\', '/'));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void AbilityTimelineDraft_FirstEditCapturesBaselineBeforeSourceChanges()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "MxFramework-ResourceDraftBaselineTests-" + Guid.NewGuid().ToString("N"));
            string sourcePath = Path.Combine(directory, "ability_authoring.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(sourcePath, "{\"revision\":1}");
            string expectedBaseline = CharacterActionAuthoringResourceDraftSessionPayload.ComputeSourceBaselineHash(sourcePath);

            CharacterActionAuthoringWindow window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
            try
            {
                SetPrivateField(window, "_abilityAuthoringJsonPath", sourcePath);
                InvokePrivate(window, "MarkAbilityEffectTimelineDraftDirty");
                File.WriteAllText(sourcePath, "{\"revision\":2}");
                InvokePrivate(window, "MarkAbilityEffectTimelineDraftDirty");

                var hashes = GetPrivateField<Dictionary<string, string>>(
                    window,
                    "_resourceDraftBaselineHashes");
                string identity = CharacterActionAuthoringResourceDraftSessionEntry.CreateIdentity(
                    CharacterActionAuthoringResourceDraftKind.AbilityEffectTimeline,
                    sourcePath);
                Assert.That(hashes[identity], Is.EqualTo(expectedBaseline),
                    "Later session saves must retain the baseline captured by the first edit.");
                Assert.That(
                    CharacterActionAuthoringResourceDraftSessionPayload.ComputeSourceBaselineHash(sourcePath),
                    Is.Not.EqualTo(expectedBaseline));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void RecoveryEnvelope_MalformedPayload_RemainsExportable()
        {
            const string malformedPayload = "{not-json";
            string recovery = CharacterActionAuthoringDraftRecoveryEnvelope.Serialize(
                string.Empty,
                malformedPayload,
                Array.Empty<string>(),
                new[] { "$resource-drafts" });

            Assert.That(
                CharacterActionAuthoringDraftRecoveryEnvelope.TryDeserialize(
                    recovery,
                    out CharacterActionAuthoringDraftRecoveryEnvelope restored),
                Is.True);
            Assert.That(restored.ResourceDraftsJson, Is.EqualTo(malformedPayload));
            CollectionAssert.AreEqual(new[] { "$resource-drafts" }, restored.ResourceConflictPaths);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, arguments);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }
            return true;
        }
    }
}

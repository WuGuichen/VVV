using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Projectiles;
using MxFramework.Editor.Animation;
using MxFramework.Editor.CharacterAction;
using MxFramework.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionAuthoringEditorTests
    {
        [TestCase("char.000001", "000001")]
        [TestCase("char.combat_girl_sword", "combat_girl_sword")]
        [TestCase("", "")]
        [TestCase("invalid", "")]
        public void ResolveCharacterPackageId_UsesStableCharacterPrefix(
            string characterStableId,
            string expectedPackageId)
        {
            Assert.AreEqual(
                expectedPackageId,
                CharacterActionAuthoringWindow.ResolveCharacterPackageId(characterStableId));
        }

        [SetUp]
        public void SetUp()
        {
            DestroyOpenAuthoringWindows();
            SessionState.SetString(
                "MxFramework.CharacterActionAuthoringWindow.Model",
                string.Empty);
            SessionState.SetString(
                "MxFramework.CharacterActionAuthoringWindow.ResourceDrafts",
                string.Empty);
            SessionState.SetString(
                "MxFramework.CharacterActionAuthoringWindow.DocumentDrafts",
                string.Empty);
            SessionState.SetString(
                "MxFramework.CharacterActionAuthoringWindow.ConflictedDocumentDrafts",
                string.Empty);
            SessionState.SetString(
                "MxFramework.CharacterActionAuthoringWindow.DocumentDraftConflictPaths",
                string.Empty);
            SessionState.SetBool(
                "MxFramework.CharacterActionAuthoringWindow.ProfileDirty",
                false);
            SessionState.SetBool(
                "MxFramework.CharacterActionAuthoringWindow.ActionSetDirty",
                false);
            SessionState.SetBool(
                "MxFramework.CharacterActionAuthoringWindow.ActionLibraryDirty",
                false);
            SessionState.SetString(
                "MxFramework.CharacterActionAuthoringWindow.ProfileSource",
                "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test");
            SessionState.SetString(
                "MxFramework.CharacterActionAuthoringWindow.ActionSetSource",
                "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test");
            SessionState.SetString(
                "MxFramework.CharacterActionAuthoringWindow.ActionLibrarySource",
                "CharacterActionSource/Libraries/Common");
            SessionState.SetString(
                "MxFramework.CharacterActionAuthoringWindow.EditorMode",
                "CharacterAction");
            SessionState.SetString(
                "MxFramework.CharacterActionAuthoringWindow.Tab",
                "Profile");
        }

        [Test]
        public void CharacterActionAuthoringWindow_ToggleFocusShortcutUsesSingleCKey()
        {
            const string shortcutId = "MxFramework/Character Action Authoring/Toggle Focus";
            CollectionAssert.Contains(
                ShortcutManager.instance.GetAvailableShortcutIds(),
                shortcutId);

            MethodInfo shortcutMethod = typeof(CharacterActionAuthoringWindow).GetMethod(
                "ToggleFocusShortcut",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(shortcutMethod);

            bool foundShortcut = false;
            bool foundDefaultC = false;
            IList<CustomAttributeData> attributes = shortcutMethod.GetCustomAttributesData();
            for (int i = 0; i < attributes.Count; i++)
            {
                CustomAttributeData attribute = attributes[i];
                if (attribute.AttributeType != typeof(ShortcutAttribute))
                    continue;

                foundShortcut = true;
                IList<CustomAttributeTypedArgument> arguments = attribute.ConstructorArguments;
                for (int j = 0; j < arguments.Count; j++)
                {
                    CustomAttributeTypedArgument argument = arguments[j];
                    if (argument.ArgumentType == typeof(KeyCode)
                        && Convert.ToInt32(argument.Value) == (int)KeyCode.C)
                    {
                        foundDefaultC = true;
                    }
                }
            }

            Assert.IsTrue(foundShortcut);
            Assert.IsTrue(foundDefaultC);
        }

        [UnityTest]
        public IEnumerator CharacterActionAuthoringWindow_ToggleFocusReturnsToGameView()
        {
            MethodInfo shortcutMethod = typeof(CharacterActionAuthoringWindow).GetMethod(
                "ToggleFocusShortcut",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(shortcutMethod);

            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            Assert.IsNotNull(gameViewType);
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            gameView.Show();
            gameView.Focus();
            yield return null;

            shortcutMethod.Invoke(null, null);
            yield return null;

            var authoringWindow = EditorWindow.focusedWindow as CharacterActionAuthoringWindow;
            Assert.IsNotNull(authoringWindow);

            shortcutMethod.Invoke(null, null);
            yield return null;

            Assert.AreSame(gameView, EditorWindow.focusedWindow);

            shortcutMethod.Invoke(null, null);
            yield return null;

            var restoredAuthoringWindow = EditorWindow.focusedWindow as CharacterActionAuthoringWindow;
            Assert.IsNotNull(restoredAuthoringWindow);
            Assert.AreSame(authoringWindow, restoredAuthoringWindow);
            restoredAuthoringWindow.Close();
        }

        [Test]
        public void EditorWindowShortcutTray_FirstItemUsesBottomRightCorner()
        {
            var bottomRight = new Vector2(1300f, 1000f);
            var collapsedSize = new Vector2(180f, 28f);
            Rect collapsed = MxFramework.Editor.EditorWindowShortcutTray.BuildPosition(
                bottomRight,
                0f,
                collapsedSize);

            Assert.AreEqual(bottomRight.x, collapsed.xMax);
            Assert.AreEqual(bottomRight.y, collapsed.yMax);
            Assert.AreEqual(collapsedSize.x, collapsed.width);
            Assert.AreEqual(collapsedSize.y, collapsed.height);
        }

        [Test]
        public void M7_ProjectileRequestClassificationUsesLoadedStoreMembership()
        {
            CharacterActionProjectileAuthoringStore store = CharacterActionProjectileAuthoringStore.LoadDefault();
            CharacterActionProjectileRequestMembership.Refresh(store);

            Assert.IsTrue(CharacterActionProjectileRequestMembership.Contains("character_test.arc_mine.deploy"));
            Assert.IsTrue(CharacterActionProjectileRequestMembership.Contains("character_test.rune_turret.deploy"));
            Assert.IsTrue(CharacterActionProjectileRequestMembership.Contains("character_test.fireball.projectile"));
            Assert.IsFalse(CharacterActionProjectileRequestMembership.Contains("gameplay.character_test.ordinary_request"));
        }

        [Test]
        public void CharacterActionExecution_FirstPartySourcesUseSingleGateway()
        {
            string root = Directory.GetCurrentDirectory();
            string[] sourceRoots =
            {
                "Packages/com.vincent.wgameframework/CharacterAction",
                "Packages/com.vincent.wgameframework/CharacterControl",
                "Assets/Scripts/WGameFrameworkProject/CharacterTest",
                "Packages/com.vincent.wgameframework/Editor/CharacterAction",
            };
            var directReferences = new List<string>();
            var runnerType = new Regex(@"\bCharacterActionRunner\b", RegexOptions.CultureInvariant);
            for (int rootIndex = 0; rootIndex < sourceRoots.Length; rootIndex++)
            {
                string sourceRoot = Path.Combine(root, sourceRoots[rootIndex]);
                string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string file = files[fileIndex];
                    if (file.IndexOf(
                            "CharacterActionExecutionGateway.cs",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }
                    if (file.EndsWith(
                            Path.DirectorySeparatorChar + "CharacterActionRunner.cs",
                            StringComparison.OrdinalIgnoreCase)
                        || file.EndsWith(
                            Path.DirectorySeparatorChar + "CharacterActionRunnerStateTypes.cs",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (file.IndexOf(
                            Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    if (runnerType.IsMatch(File.ReadAllText(file)))
                        directReferences.Add(file.Substring(root.Length + 1));
                }
            }

            Assert.IsEmpty(
                directReferences,
                "First-party code must not reference CharacterActionRunner outside the gateway: "
                + string.Join(", ", directReferences));
        }

        [Test]
        public void CharacterActionRunner_ExecutionSurfaceIsInternalAndOnlyGatewayConstructsIt()
        {
            Type runnerType = typeof(CharacterActionRunner);
            Assert.IsFalse(runnerType.IsPublic || runnerType.IsNestedPublic);
            Assert.IsFalse(typeof(CharacterActionRunnerOperationResult).IsPublic);
            Assert.IsFalse(typeof(CharacterActionRunnerBufferedOperationResult).IsPublic);
            string[] executionMethods =
            {
                "Start",
                "RequestCancel",
                "RequestInterrupt",
                "CompleteExternal",
                "ReplaceForLifecycle",
                "TerminateForLifecycle",
            };
            MethodInfo[] publicMethods = runnerType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int nameIndex = 0; nameIndex < executionMethods.Length; nameIndex++)
            {
                for (int methodIndex = 0; methodIndex < publicMethods.Length; methodIndex++)
                {
                    Assert.AreNotEqual(
                        executionMethods[nameIndex],
                        publicMethods[methodIndex].Name,
                        "Runner execution method must not be public: " + executionMethods[nameIndex]);
                }
            }

            string root = Directory.GetCurrentDirectory();
            string sourceRoot = Path.Combine(root, "Packages/com.vincent.wgameframework");
            string gatewayPath = Path.Combine(
                sourceRoot,
                "CharacterAction",
                "CharacterActionExecutionGateway.cs");
            gatewayPath = Path.GetFullPath(gatewayPath);
            var unauthorizedConstruction = new List<string>();
            var construction = new Regex(
                @"new\s+CharacterActionRunner\s*\(",
                RegexOptions.CultureInvariant);
            string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string file = Path.GetFullPath(files[i]);
                if (file.IndexOf(
                        Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase) >= 0
                    || string.Equals(file, gatewayPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (construction.IsMatch(File.ReadAllText(file)))
                    unauthorizedConstruction.Add(file.Substring(root.Length + 1));
            }

            Assert.IsEmpty(
                unauthorizedConstruction,
                "Only CharacterActionExecutionGateway may construct CharacterActionRunner: "
                + string.Join(", ", unauthorizedConstruction));
        }

        [Test]
        public void CharacterAction_BreakingMigrationLeavesNoGenericCancelOrRawClearSurface()
        {
            string root = Directory.GetCurrentDirectory();
            string sourceRoot = Path.Combine(root, "Packages/com.vincent.wgameframework");
            string[] forbidden =
            {
                "CharacterActionKind." + "Cancel",
                "CharacterActionRequest." + "Cancel",
                "CharacterInputActionBinding." + "Cancel",
                "ForceCancel" + "Active",
                "ClearEntity" + "Action",
                "TryCancel" + "ActiveAction",
                "TryStart" + "ForcedReaction",
                "CombatCohesion" + "BreakAdapter",
            };
            var violations = new List<string>();
            string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (file.IndexOf(
                        Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
                string source = File.ReadAllText(file);
                for (int forbiddenIndex = 0; forbiddenIndex < forbidden.Length; forbiddenIndex++)
                {
                    if (source.IndexOf(forbidden[forbiddenIndex], StringComparison.Ordinal) >= 0)
                        violations.Add(file.Substring(root.Length + 1) + ":" + forbidden[forbiddenIndex]);
                }
            }

            Assert.IsEmpty(
                violations,
                "Removed CharacterAction surfaces must not reappear: " + string.Join(", ", violations));
        }

        [Test]
        public void CharacterActionController_DoesNotOwnCombatOrGameplayExecutionFacilities()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Packages/com.vincent.wgameframework/CharacterControl/CharacterActionController.cs"));

            StringAssert.DoesNotContain("CombatAction" + "Runner", source);
            StringAssert.DoesNotContain("RuntimeCommand" + "Buffer", source);
            StringAssert.DoesNotContain("GameplayComponentAbility" + "RequestStore", source);
        }

        [Test]
        public void RuntimeAiPlannerOverlay_DoesNotGenerateStandaloneCharacterActionCancel()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge.Actions.cs"));

            StringAssert.DoesNotContain(
                "CharacterTestRuntimeAiLabRuntimeIntentKind.CancelCharacterAction",
                source);
        }

        [Test]
        public void RuntimeAiPlannerOverlay_MutableStateGroupsHaveSingleOwners()
        {
            string directory = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev");
            string responsibilitiesPath = Path.Combine(
                directory,
                "CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.Responsibilities.cs");
            string responsibilities = File.ReadAllText(responsibilitiesPath);
            StringAssert.Contains("class BindingSwitchCoordinator", responsibilities);
            StringAssert.Contains("class EntryActionHandshake", responsibilities);
            StringAssert.Contains("class LogicFactRuntime", responsibilities);
            StringAssert.Contains("class DecisionTraceState", responsibilities);

            var duplicateBackingField = new Regex(
                @"private\s+(?:int|bool|long|ulong|string|CharacterTestRuntimeAiLabCommittedStateTrace|CharacterTestRuntimeAiLabCommandEnqueueKind)\s+_(?:activeBindingIndex|pendingBindingIndex|commandResultBindingIndex|bindingReleasePending|commandPending|commandRejected|actionObserved|actionStartedThisFrame|adoptEntryStagePending|entryTransitionPreviousInstanceId|factReadMask|cardArbitrationFactReadMask|logicConditionFactReadMask|previousCommitted|commandEnqueueKind|commandReasonCode)\s*(?:=|;)",
                RegexOptions.CultureInvariant);
            var violations = new List<string>();
            string[] files = Directory.GetFiles(
                directory,
                "CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay*.cs",
                SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                if (string.Equals(files[i], responsibilitiesPath, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (duplicateBackingField.IsMatch(File.ReadAllText(files[i])))
                    violations.Add(Path.GetFileName(files[i]));
            }

            Assert.IsEmpty(
                violations,
                "Overlay mutable state must remain owned by its responsibility object: "
                + string.Join(", ", violations));
        }

        [Test]
        public void M7_CharacterActionWindowPartialsContainNoProjectileWriterOwnership()
        {
            string sourceDirectory = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Packages/com.vincent.wgameframework/Editor/CharacterAction");
            string[] files = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);
            string source = string.Empty;
            string projectileSource = string.Empty;
            for (int i = 0; i < files.Length; i++)
            {
                source += File.ReadAllText(files[i]);
                if (Path.GetFileName(files[i]).IndexOf("Projectile", StringComparison.OrdinalIgnoreCase) >= 0)
                    projectileSource += File.ReadAllText(files[i]);
            }

            string[] forbidden =
            {
                "CreateFromTemplate",
                "UpdateDefinitions(",
                "BuildGeneratedBindingsRoot",
                "BuildRuntimeCatalogProjection(",
                "ApplyProjectileAuthoringChange",
                "SavePendingProjectile",
                "RestorePendingProjectile",
                "StorePendingProjectileDraft",
                "CharacterActionProjectileGizmoEdit",
                "CharacterActionProjectileEditContext",
                "CharacterActionProjectileAuthoringDraft",
                "SessionPendingProjectileDraftsKey",
                "_pendingProjectileDrafts",
                "CharacterActionProjectileRequestCreateWindow",
                "Create a projectile request from a template",
                "syncs generated JSON",
                "CharacterActionProjectileManagerIndex",
                "CharacterActionProjectileManagerItem",
                "CharacterActionProjectileReferenceCatalog",
                "AddDirtySourceLabel(labels, \"Projectile\")",
                "CharacterActionAuthoringUnifiedSaveStage.Projectile",
                "EditModeText",
            };
            for (int i = 0; i < forbidden.Length; i++)
                StringAssert.DoesNotContain(forbidden[i], source, forbidden[i]);
            StringAssert.DoesNotContain("WriteJson(", projectileSource, "WriteJson(");
        }

        [TestCase("character_test.fireball.projectile", 1)]
        [TestCase("character_test.arc_mine.deploy", 2)]
        [TestCase("character_test.rune_turret.deploy", 2)]
        public void M7_ProjectileGameplayInspectorShowsReadOnlySummaryAndKeepsSpecialTuning(
            string requestId,
            int expectedProjectileSections)
        {
            CharacterActionAuthoringWindow window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
            try
            {
                CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
                typeof(CharacterActionAuthoringWindow)
                    .GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(window, model);
                var parent = new VisualElement();
                var evt = new GameplayTrackEventJsonDto
                {
                    Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                    RequestId = CharacterActionAuthoringIds.Resolve(requestId, 408),
                };
                MethodInfo method = typeof(CharacterActionAuthoringWindow).GetMethod(
                    "AddGameplayPayloadFields",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(method);
                method.Invoke(window, new object[] { parent, model.Actions[0], evt, CharacterActionTrackEventKind.SendGameplayRequest, false });

                VisualElement summary = parent.Q("projectile-request-readonly-summary");
                Assert.NotNull(summary);
                Assert.NotNull(summary.Q<Button>("open-projectile-authoring"));
                Assert.AreEqual(0, summary.Query<TextField>().ToList().Count);
                Assert.AreEqual(0, summary.Query<FloatField>().ToList().Count);
                Assert.AreEqual(0, summary.Query<IntegerField>().ToList().Count);
                Assert.AreEqual(
                    expectedProjectileSections,
                    parent.Query<VisualElement>(className: "ca-projectile-inspector").ToList().Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ProjectileGameplayEventInspector_UsesCompactPrimaryLayoutAndCollapsedAdvancedDetails()
        {
            CharacterActionAuthoringWindow window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
            try
            {
                CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
                typeof(CharacterActionAuthoringWindow)
                    .GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(window, model);
                CharacterActionJsonDto action = model.Actions[0];
                action.GameplayTrack = new GameplayTrackJsonDto
                {
                    Events = new[]
                    {
                        new GameplayTrackEventJsonDto
                        {
                            Frame = 25,
                            Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                            RequestId = CharacterTestBulkIds38.Id790020,
                            StableEventId = CharacterTestBulkIds38.Id790021,
                        },
                    },
                };

                var parent = new VisualElement();
                MethodInfo method = typeof(CharacterActionAuthoringWindow).GetMethod(
                    "BuildGameplayEventInspector",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(method);
                method.Invoke(window, new object[] { parent, action, 0, false });

                VisualElement editor = parent.Q("projectile-request-event-editor");
                Assert.NotNull(editor);
                Assert.AreEqual("Ready", editor.Q<Label>("projectile-health-badge")?.text);
                StringAssert.Contains("mainHand", editor.Q<Label>("projectile-compact-summary")?.text);
                StringAssert.Contains("CasterForward", editor.Q<Label>("projectile-compact-summary")?.text);
                Assert.AreEqual("编辑投射物配置", editor.Q<Button>("open-projectile-authoring")?.text);
                Foldout advanced = editor.Q<Foldout>("projectile-event-advanced");
                Assert.NotNull(advanced);
                Assert.IsFalse(advanced.value);
                Assert.IsNull(parent.Q("projectile-request-readonly-summary"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void M7_GameplayRequestSelectionChangesOnlyRequestIdAndLeavesProjectileArtifactsUntouched()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            var pathsList = new List<string>(Directory.GetFiles(
                    Path.Combine(projectRoot, "Tools/MxFramework.Authoring/samples/ability-character-test/config/projectiles"),
                    "*.json",
                    SearchOption.TopDirectoryOnly));
            string[] artifactNames =
            {
                "projectile_definitions.json", "projectile_definitions.bytes",
                "projectile_spawn_profiles.json", "projectile_spawn_profiles.bytes",
                "projectile_presentation_bindings.json", "projectile_presentation_bindings.bytes",
            };
            string[] artifactRoots =
            {
                "Assets/MxFrameworkGenerated/Combat/Projectiles",
                "Assets/StreamingAssets/MxFramework/CharacterTest/RuntimeCatalogs/Combat/Projectiles",
            };
            for (int rootIndex = 0; rootIndex < artifactRoots.Length; rootIndex++)
                for (int artifactIndex = 0; artifactIndex < artifactNames.Length; artifactIndex++)
                    pathsList.Add(Path.Combine(projectRoot, artifactRoots[rootIndex], artifactNames[artifactIndex]));
            string[] paths = pathsList
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            byte[][] before = paths.Select(File.ReadAllBytes).ToArray();
            var evt = new GameplayTrackEventJsonDto { RequestId = CharacterTestBulkIds38.Id790022 };

            CharacterActionAuthoringWindow window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
            try
            {
                CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
                typeof(CharacterActionAuthoringWindow)
                    .GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(window, model);
                MethodInfo method = typeof(CharacterActionAuthoringWindow).GetMethod(
                    "ApplyGameplayRequestValue",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(method);
                method.Invoke(window, new object[]
                {
                    new Action<string>(value => evt.RequestId = CharacterActionAuthoringIds.Resolve(value, 408)),
                    "character_test.arc_mine.deploy",
                    false,
                });
                Assert.AreEqual(CharacterTestNumericIds.Assert_character_test_arc_mine_deploy, evt.RequestId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }

            for (int i = 0; i < paths.Length; i++)
                CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]), paths[i]);
        }

        [Test]
        public void M7_ResourceSessionPayloadIgnoresProjectileEntryAndRestoresOtherKinds()
        {
            CharacterActionAuthoringWindow window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
            try
            {
                string missingSource = "Temp/M7/session-source-does-not-exist.json";
                string baseline = CharacterActionAuthoringResourceDraftSessionPayload.ComputeSourceBaselineHash(missingSource);
                string payload = CharacterActionAuthoringResourceDraftSessionPayload.Serialize(new[]
                {
                    new CharacterActionAuthoringResourceDraftSessionEntry(
                        CharacterActionAuthoringResourceDraftKind.Projectile,
                        missingSource,
                        baseline,
                        JObject.Parse("{\"stableId\":\"legacy.projectile\",\"speedPerFrame\":999}")),
                    new CharacterActionAuthoringResourceDraftSessionEntry(
                        CharacterActionAuthoringResourceDraftKind.AbilityEffectTimeline,
                        missingSource,
                        baseline,
                        JArray.Parse("[{\"timelineId\":\"m7.timeline\",\"durationFrames\":10,\"events\":[]}]")),
                    new CharacterActionAuthoringResourceDraftSessionEntry(
                        CharacterActionAuthoringResourceDraftKind.MechanicTuning,
                        missingSource,
                        baseline,
                        JArray.Parse("[{\"stableId\":\"mechanic_tuning.character_test.arc_mine\",\"abilityStableId\":\"ability.character_test.arc_mine\",\"sourceStableId\":\"mechanic.character_test.arc_mine.trap\",\"deployRangeMeters\":9}]")),
                });
                MethodInfo restore = typeof(CharacterActionAuthoringWindow).GetMethod(
                    "RestoreResourceDraftsFromSession",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(restore);
                Assert.DoesNotThrow(() => restore.Invoke(window, new object[] { payload }));
                FieldInfo timelines = typeof(CharacterActionAuthoringWindow).GetField(
                    "_abilityEffectTimelines",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo dirty = typeof(CharacterActionAuthoringWindow).GetField(
                    "_abilityEffectTimelineDirty",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo conflicts = typeof(CharacterActionAuthoringWindow).GetField(
                    "_resourceDraftConflictPaths",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo mechanicDrafts = typeof(CharacterActionAuthoringWindow).GetField(
                    "_pendingMechanicTuningDrafts",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(timelines?.GetValue(window));
                Assert.IsTrue((bool)dirty.GetValue(window));
                var restoredMechanics = mechanicDrafts?.GetValue(window) as System.Collections.IDictionary;
                Assert.NotNull(restoredMechanics);
                Assert.AreEqual(1, restoredMechanics.Count);
                Assert.IsEmpty((string[])conflicts.GetValue(window));
                MethodInfo serialize = typeof(CharacterActionAuthoringWindow).GetMethod(
                    "SerializeResourceDraftsForSession",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                string roundTrip = (string)serialize.Invoke(window, null);
                StringAssert.DoesNotContain("legacy.projectile", roundTrip);
                StringAssert.Contains("m7.timeline", roundTrip);
                StringAssert.Contains("mechanic_tuning.character_test.arc_mine", roundTrip);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DefaultModel_ExportsValidJumpLightAttackJson()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            model.Actions[0].CancelRules = new[]
            {
                new CharacterCancelRuleJsonDto
                {
                    StartFrame = 5,
                    EndFrame = 7,
                    RequestTypeMask = (int)CharacterActionRequestType.Any,
                    ActionSlotMask = (int)CharacterCancelInputMask.Any,
                },
            };

            CharacterActionAuthoringExportResult result = CharacterActionAuthoringJsonService.Export(model);

            Assert.IsTrue(result.Success, result.DiagnosticsText);
            Assert.IsTrue(result.ImportResult.Success, result.DiagnosticsText);
            Assert.IsTrue(result.ValidationReport.Success, result.DiagnosticsText);
            Assert.IsTrue(result.Registry.TryGetActionSet("codex_flow_test.default", out _));
            Assert.AreEqual("codex_flow_test.default", model.Profile.StableId);
            Assert.AreEqual("codex_flow_test.default", model.ActionSet.StableId);
            Assert.AreEqual("reaction.codex_flow_test.default", model.ActionSet.ReactionProfileId);
            Assert.IsTrue(result.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(300), out _));
            Assert.IsTrue(result.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(301), out _));
            StringAssert.DoesNotContain("character_test.default", result.ActionSetsJson);
            StringAssert.DoesNotContain("reaction.character_test.katana.default", result.ActionSetsJson);
            StringAssert.Contains("\"intentId\": \"Jump\"", result.ActionSetsJson);
            StringAssert.Contains("\"intentId\": \"LightAttack\"", result.ActionSetsJson);
            StringAssert.Contains("\"formulaId\": \"ct.soft_defense_v1\"", result.ActionsJson);
            StringAssert.Contains("\"sourceAttributeScales\"", result.ActionsJson);
            StringAssert.Contains("\"baseDamage\": 6", result.ActionsJson);
            StringAssert.Contains("\"impactPower\": 34", result.ActionsJson);
            StringAssert.Contains("\"requestTypeMask\": 0", result.ActionsJson);
            StringAssert.Contains("\"actionSlotMask\": 0", result.ActionsJson);
            StringAssert.DoesNotContain("\"targetActionId\"", result.ActionsJson);
            StringAssert.DoesNotContain("\"sourceKind\": \"Command\"", result.ActionsJson);
            StringAssert.DoesNotContain("\"allow\": true", result.ActionsJson);
            StringAssert.DoesNotContain("\"damageProfileId\": \"damage.light_slash\"", result.ActionsJson);
            Assert.GreaterOrEqual(result.WorkstationSnapshots.Length, 2);
        }

        [Test]
        public void Export_WithMissingCommandAction_BlocksAndReportsDiagnostic()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            model.ActionSet.CommandBindings[0].ActionId = 999999;

            CharacterActionAuthoringExportResult result = CharacterActionAuthoringJsonService.Export(model);

            Assert.IsFalse(result.Success);
            StringAssert.Contains(CharacterActionDiagnosticCodes.MissingActionConfig, result.DiagnosticsText);
        }

        [Test]
        public void Export_AbilityBindings_RoundTripsAndValidatesActionReferences()
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
            model.ReferenceContext.AddEquipmentTag("status.stunned");

            CharacterActionAuthoringExportResult result = CharacterActionAuthoringJsonService.Export(model);

            Assert.IsTrue(result.Success, result.DiagnosticsText);
            StringAssert.Contains("\"abilityId\": 900001", result.ActionSetsJson);
            StringAssert.Contains("\"actionId\": 301", result.ActionSetsJson);

            CharacterActionAuthoringImportResult import =
                CharacterActionAuthoringJsonService.Import(result.ActionSetsJson, result.ActionsJson);
            Assert.IsTrue(import.Success, import.Validation.DiagnosticsText);
            Assert.AreEqual(1, import.Model.ActionSet.AbilityBindings.Length);
            Assert.AreEqual(900001, import.Model.ActionSet.AbilityBindings[0].AbilityId);
            Assert.AreEqual(301, import.Model.ActionSet.AbilityBindings[0].ActionId);

            import.Model.ActionSet.AbilityBindings[0].ActionId = 999999;
            CharacterActionAuthoringExportResult invalid = CharacterActionAuthoringJsonService.Export(import.Model);
            Assert.IsFalse(invalid.Success);
            StringAssert.Contains(CharacterActionDiagnosticCodes.MissingActionConfig, invalid.DiagnosticsText);
        }

        [Test]
        public void SourceCatalog_LoadsReactionProfilesWithActionSetBeforeSavingActionLibrary()
        {
            string root = Path.Combine(
                "Temp",
                "CharacterActionSourceCatalogTests",
                Guid.NewGuid().ToString("N"));
            string actionSetDirectory = Path.Combine(root, "ActionSets", "codex_flow_test");
            string actionLibraryDirectory = Path.Combine(root, "Libraries", "Common");
            Directory.CreateDirectory(actionSetDirectory);
            Directory.CreateDirectory(actionLibraryDirectory);

            try
            {
                CharacterActionAuthoringExportResult seed =
                    CharacterActionAuthoringJsonService.Export(CharacterActionAuthoringModel.CreateDefault());
                Assert.IsTrue(seed.Success, seed.DiagnosticsText);
                File.WriteAllText(
                    Path.Combine(actionSetDirectory, CharacterActionJsonImporter.DefaultActionSetsSourcePath),
                    seed.ActionSetsJson);
                File.WriteAllText(
                    Path.Combine(actionSetDirectory, CharacterActionJsonImporter.DefaultReactionProfilesSourcePath),
                    seed.ReactionProfilesJson);

                var entry = new CharacterActionAuthoringSourceEntry(actionSetDirectory, "codex_flow_test");
                CharacterActionAuthoringImportResult loaded = CharacterActionAuthoringSourceCatalog.LoadActionSet(entry);
                Assert.IsTrue(loaded.Success, loaded.Validation.DiagnosticsText);
                Assert.AreEqual("reaction.codex_flow_test.default", loaded.Model.ActionSet.ReactionProfileId);
                Assert.AreEqual(1, loaded.Model.ReactionProfiles.Count);

                CharacterActionAuthoringModel saveModel = loaded.Model;
                saveModel.Actions.AddRange(CharacterActionAuthoringModel.CreateDefault().Actions);
                saveModel.ReferenceContext = CharacterActionAuthoringReferenceContext.CreateFromModel(saveModel);
                CharacterActionAuthoringExportResult saved = CharacterActionAuthoringSourceCatalog.SaveActionLibrary(
                    actionLibraryDirectory,
                    saveModel,
                    saveModel.ReferenceContext);

                Assert.IsTrue(saved.Success, saved.DiagnosticsText);
                StringAssert.DoesNotContain(CharacterActionDiagnosticCodes.MissingReactionProfile, saved.DiagnosticsText);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void Import_EditExport_RoundTripsPhaseMotionAndAnimationFields()
        {
            CharacterActionAuthoringExportResult seed =
                CharacterActionAuthoringJsonService.Export(CharacterActionAuthoringModel.CreateDefault());
            CharacterActionAuthoringImportResult import =
                CharacterActionAuthoringJsonService.Import(seed.ActionSetsJson, seed.ActionsJson);
            Assert.IsTrue(import.Success, import.Validation.DiagnosticsText);

            CharacterActionJsonDto jump = import.Model.Actions[0];
            jump.DurationFrames = 9;
            jump.Phases[2].EndFrame = 8;
            jump.MotionTrack.Events[0].Y = 12f;
            jump.AnimationTrack.Events[0].AnimationActionKey = "anim.edited_jump";
            import.Model.ReferenceContext.AddAnimationActionKey("anim.edited_jump");

            CharacterActionAuthoringExportResult result =
                CharacterActionAuthoringJsonService.Export(import.Model);

            Assert.IsTrue(result.Success, result.DiagnosticsText);
            Assert.IsTrue(result.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(300), out CharacterActionConfig exported));
            Assert.AreEqual(9, exported.DurationFrames);
            Assert.AreEqual(8, exported.Phases[2].EndFrame);
            Assert.AreEqual(12f, exported.MotionTrack.Events[0].Y);
            Assert.AreEqual("anim.edited_jump", exported.AnimationTrack.Events[0].AnimationActionKey);
        }

        [Test]
        public void Export_ControlScaleMotionEvent_RoundTripsVisibleScaleFields()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            CharacterActionJsonDto lightAttack = model.Actions[1];
            lightAttack.MotionTrack = lightAttack.MotionTrack ?? new MotionTrackJsonDto();
            lightAttack.MotionTrack.Events = new[]
            {
                new MotionTrackEventJsonDto
                {
                    Frame = 0,
                    EndFrame = 3,
                    Kind = CharacterActionTrackEventKind.SetControlScale.ToString(),
                    MovementControlScale = 0.25f,
                    RotationControlScale = 0.5f,
                    StableEventId = CharacterTestBulkIds38.Id790023,
                },
            };

            CharacterActionAuthoringExportResult result = CharacterActionAuthoringJsonService.Export(model);

            Assert.IsTrue(result.Success, result.DiagnosticsText);
            StringAssert.Contains("\"movementControlScale\": 0.25", result.ActionsJson);
            StringAssert.Contains("\"rotationControlScale\": 0.5", result.ActionsJson);
            Assert.IsTrue(result.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(301), out CharacterActionConfig exported));
            Assert.AreEqual(CharacterActionTrackEventKind.SetControlScale, exported.MotionTrack.Events[0].Kind);
            Assert.AreEqual(0.25f, exported.MotionTrack.Events[0].MovementControlScale, 0.0001f);
            Assert.AreEqual(0.5f, exported.MotionTrack.Events[0].RotationControlScale, 0.0001f);
        }

        [Test]
        public void Export_CollisionProfileMotionEvent_OmitsIrrelevantBlankMovementMode()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            CharacterActionJsonDto action = model.Actions[1];
            action.MotionTrack = new MotionTrackJsonDto
            {
                Events = new[]
                {
                    new MotionTrackEventJsonDto
                    {
                        Frame = 0,
                        EndFrame = 1,
                        Kind = CharacterActionTrackEventKind.SetCollisionProfile.ToString(),
                        CollisionProfileId = CharacterTestBulkIds38.Id790024,
                        CollisionRestorePolicy = CharacterActionCollisionProfileRestorePolicy.None.ToString(),
                        StableEventId = CharacterTestBulkIds38.Id790025,
                    },
                },
            };

            CharacterActionAuthoringExportResult result = CharacterActionAuthoringJsonService.Export(model);

            Assert.IsTrue(result.Success, result.DiagnosticsText);
            StringAssert.DoesNotContain("\"movementMode\": \"\"", result.ActionsJson);
            Assert.IsTrue(result.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(action.ActionId), out CharacterActionConfig exported));
            Assert.AreEqual(CharacterActionTrackEventKind.SetCollisionProfile, exported.MotionTrack.Events[0].Kind);
            Assert.AreEqual(CharacterActionAuthoringIds.Resolve("corpse", 402, allocate: false), exported.MotionTrack.Events[0].CollisionProfileId);
        }

        [Test]
        public void Export_InlineDamagePayload_RoundTripsOnCombatAndGameplayEvents()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            CharacterActionJsonDto action = model.Actions[1];
            action.CombatTrack = new CombatTrackJsonDto
            {
                Events = new[]
                {
                    new CombatTrackEventJsonDto
                    {
                        Frame = 3,
                        EndFrame = 5,
                        Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                        EquipmentSlotId = CharacterTestBulkIds38.Id790007,
                        StableEventId = CharacterTestBulkIds38.Id790005,
                        HitEvent = new CharacterActionHitEventJsonDto
                        {
                            Damage = new CharacterActionHitDamageJsonDto
                            {
                                DamageTypeId = CharacterTestBulkIds38.Id790006,
                                FormulaId = CharacterTestBulkIds38.Id790026,
                                BaseDamage = 6,
                                SourceAttributeScales = new[]
                                {
                                    new CharacterActionDamageAttributeScaleJsonDto { AttributeId = 2, Scale = 0.65f },
                                },
                                ImpactPower = 12,
                                Tags = CharacterActionDamageIdentityAuthoring.ExistingTags(new[] { "melee", "weapon", "scatter" }),
                                AnalysisCategory = CharacterTestBulkIds38.Id790012,
                            },
                        },
                    },
                },
            };
            action.GameplayTrack = new GameplayTrackJsonDto
            {
                Events = new[]
                {
                    new GameplayTrackEventJsonDto
                    {
                        Frame = 7,
                        Kind = CharacterActionTrackEventKind.ApplyGameplayEffect.ToString(),
                        TargetPolicyId = CharacterTestBulkIds38.Id790008,
                        StableEventId = CharacterTestBulkIds38.Id790009,
                        HitEvent = new CharacterActionHitEventJsonDto
                        {
                            Damage = new CharacterActionHitDamageJsonDto
                            {
                                DamageTypeId = CharacterTestBulkIds38.Id790010,
                                FormulaId = CharacterTestBulkIds38.Id790027,
                                BaseDamage = 6,
                                ImpactPower = 12,
                                Tags = CharacterActionDamageIdentityAuthoring.ExistingTags(new[] { "area", "shockwave", "scatter" }),
                                AnalysisCategory = CharacterTestBulkIds38.Id790028,
                            },
                        },
                    },
                },
            };

            CharacterActionAuthoringExportResult result = CharacterActionAuthoringJsonService.Export(model);

            Assert.IsTrue(result.Success, result.DiagnosticsText);
            StringAssert.Contains("\"damageTypeId\": \"slash\"", result.ActionsJson);
            StringAssert.Contains("\"formulaId\": \"ct.soft_defense_v1\"", result.ActionsJson);
            StringAssert.Contains("\"baseDamage\": 6", result.ActionsJson);
            StringAssert.Contains("\"impactPower\": 12", result.ActionsJson);
            StringAssert.DoesNotContain("\"scatterDamage\"", result.ActionsJson);
            StringAssert.DoesNotContain("\"damageProfileId\"", result.ActionsJson);
            Assert.IsTrue(result.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(301), out CharacterActionConfig exported));
            Assert.AreEqual(CharacterTestNumericIds.DamageSlash, exported.CombatTrack.Events[0].DamagePayload.DamageTypeId);
            Assert.AreEqual(0.65f, exported.CombatTrack.Events[0].DamagePayload.SourceAttributeScales[0].Scale, 0.0001f);
            Assert.AreEqual(CharacterTestNumericIds.DamageForce, exported.GameplayTrack.Events[0].DamagePayload.DamageTypeId);
            Assert.AreEqual(12, exported.GameplayTrack.Events[0].DamagePayload.ImpactPower);
        }

        [Test]
        public void DefaultLightAttack_UsesCurrentDamageTuningBaseline()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            CharacterActionJsonDto lightAttack = model.Actions.Find(
                action => action.ActionId == 301);
            Assert.NotNull(lightAttack);
            lightAttack.CombatTrack = new CombatTrackJsonDto
            {
                Events = new[]
                {
                    new CombatTrackEventJsonDto
                    {
                        Frame = 3,
                        EndFrame = 5,
                        Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                        EquipmentSlotId = CharacterTestBulkIds38.Id790007,
                        StableEventId = CharacterTestBulkIds38.Id790005,
                        HitEvent = new CharacterActionHitEventJsonDto
                        {
                            Damage = new CharacterActionHitDamageJsonDto
                            {
                                DamageTypeId = CharacterActionAuthoringIds.Resolve(CharacterActionDamageAuthoringText.DefaultDamageTypeId, 409, allocate: false),
                                FormulaId = DamageFormulaIds.CharacterTestSoftDefenseV1,
                                BaseDamage = CharacterActionDamageAuthoringText.BaselineLightAttackBaseDamage,
                                SourceAttributeScales = CharacterActionHitEventAuthoringModel.BuildAttackScaleArray(
                                    CharacterActionDamageAuthoringText.BaselineLightAttackAttackScale),
                                ImpactPower = CharacterActionDamageAuthoringText.BaselineLightAttackImpactPower,
                                Tags = CharacterActionDamageIdentityAuthoring.ExistingTags(new[] { "melee", "weapon", "light", "scatter" }),
                                AnalysisCategory = CharacterTestBulkIds38.Id790012,
                            },
                        },
                    },
                },
            };
            CombatTrackEventJsonDto hitStart = lightAttack.CombatTrack.Events[0];
            CharacterActionHitDamageJsonDto damage = hitStart.HitEvent.Damage;

            Assert.AreEqual(CharacterActionAuthoringIds.Resolve(CharacterActionDamageAuthoringText.DefaultDamageTypeId, 409, allocate: false), damage.DamageTypeId);
            Assert.AreEqual(DamageFormulaIds.CharacterTestSoftDefenseV1, damage.FormulaId);
            Assert.AreEqual(CharacterActionDamageAuthoringText.BaselineLightAttackBaseDamage, damage.BaseDamage);
            Assert.AreEqual(
                CharacterActionDamageAuthoringText.BaselineLightAttackAttackScale,
                CharacterActionHitEventAuthoringModel.ResolveAttackScale(damage.SourceAttributeScales),
                0.0001f);
            Assert.AreEqual(CharacterActionDamageAuthoringText.BaselineLightAttackImpactPower, damage.ImpactPower);

            CharacterActionAuthoringExportResult result = CharacterActionAuthoringJsonService.Export(model);

            Assert.IsTrue(result.Success, result.DiagnosticsText);
            Assert.IsTrue(result.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(301), out CharacterActionConfig exported));
            Assert.AreEqual(CharacterActionAuthoringIds.Resolve(CharacterActionDamageAuthoringText.DefaultDamageTypeId, 409, allocate: false), exported.CombatTrack.Events[0].DamagePayload.DamageTypeId);
            Assert.AreEqual(DamageFormulaIds.CharacterTestSoftDefenseV1, exported.CombatTrack.Events[0].DamagePayload.FormulaId);
            Assert.AreEqual(CharacterActionDamageAuthoringText.BaselineLightAttackBaseDamage, exported.CombatTrack.Events[0].DamagePayload.BaseDamage);
            Assert.AreEqual(
                CharacterActionDamageAuthoringText.BaselineLightAttackAttackScale,
                exported.CombatTrack.Events[0].DamagePayload.SourceAttributeScales[0].Scale,
                0.0001f);
            Assert.AreEqual(CharacterActionDamageAuthoringText.BaselineLightAttackImpactPower, exported.CombatTrack.Events[0].DamagePayload.ImpactPower);
        }

        [Test]
        public void HitEventAuthoringModel_NormalizesOnHitBuffs()
        {
            var model = new CharacterActionHitEventAuthoringModel
            {
                OnHitBuffs = new[]
                {
                    new CharacterActionHitBuffJsonDto { BuffId = -1 },
                    new CharacterActionHitBuffJsonDto { BuffId = 601011 },
                    new CharacterActionHitBuffJsonDto { BuffId = 601010 },
                    new CharacterActionHitBuffJsonDto { BuffId = 601010 },
                },
            };

            CharacterActionHitEventAuthoringModel normalized = model.Clone();

            Assert.AreEqual(2, normalized.OnHitBuffs.Length);
            Assert.AreEqual(601010, normalized.OnHitBuffs[0].BuffId);
            Assert.AreEqual(601011, normalized.OnHitBuffs[1].BuffId);
        }

        [Test]
        public void HitEventAuthoringModel_PreservesAttackScaleInHitEventDamage()
        {
            var evt = new CombatTrackEventJsonDto
            {
                StableEventId = CharacterTestBulkIds38.Id790005,
                HitEvent = new CharacterActionHitEventJsonDto
                {
                    StableEventId = CharacterTestBulkIds38.Id790005,
                    Damage = new CharacterActionHitDamageJsonDto
                    {
                        DamageTypeId = CharacterTestBulkIds38.Id790006,
                        BaseDamage = 6,
                        SourceAttributeScales = CharacterActionHitEventAuthoringModel.BuildAttackScaleArray(0.65f),
                        ImpactPower = 34,
                    },
                },
            };

            CharacterActionHitEventAuthoringModel model = CharacterActionHitEventAuthoringModel.FromCombatTrackEvent(evt);
            model.ApplyToCombatTrackEvent(evt);

            Assert.AreEqual(0.65f, model.GetAttackScale(), 0.0001f);
            Assert.AreEqual(CharacterActionDamageAuthoringText.DefaultDamageFormulaId, evt.HitEvent.Damage.FormulaId);
            Assert.AreEqual(0.65f, evt.HitEvent.Damage.SourceAttributeScales[0].Scale, 0.0001f);
        }

        [Test]
        public void Export_OnHitBuffs_RoundTripsOnCombatAndGameplayEvents()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            CharacterActionJsonDto action = model.Actions[1];
            action.CombatTrack = new CombatTrackJsonDto
            {
                Events = new[]
                {
                    new CombatTrackEventJsonDto
                    {
                        Frame = 3,
                        EndFrame = 5,
                        Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                        EquipmentSlotId = CharacterTestBulkIds38.Id790007,
                        StableEventId = CharacterTestBulkIds38.Id790005,
                        HitEvent = new CharacterActionHitEventJsonDto
                        {
                            Damage = new CharacterActionHitDamageJsonDto
                            {
                                DamageTypeId = CharacterTestBulkIds38.Id790006,
                                BaseDamage = 6,
                                ImpactPower = 12,
                            },
                            OnHitBuffs = new[]
                            {
                                new CharacterActionHitBuffJsonDto { BuffId = 601009 },
                            },
                        },
                    },
                },
            };
            action.GameplayTrack = new GameplayTrackJsonDto
            {
                Events = new[]
                {
                    new GameplayTrackEventJsonDto
                    {
                        Frame = 7,
                        Kind = CharacterActionTrackEventKind.ApplyGameplayEffect.ToString(),
                        TargetPolicyId = CharacterTestBulkIds38.Id790008,
                        StableEventId = CharacterTestBulkIds38.Id790009,
                        HitEvent = new CharacterActionHitEventJsonDto
                        {
                            Damage = new CharacterActionHitDamageJsonDto
                            {
                                DamageTypeId = CharacterTestBulkIds38.Id790010,
                                BaseDamage = 8,
                                ImpactPower = 4,
                            },
                            OnHitBuffs = new[]
                            {
                                new CharacterActionHitBuffJsonDto
                                {
                                    BuffId = 601010,
                                },
                            },
                        },
                    },
                },
            };

            CharacterActionAuthoringExportResult result = CharacterActionAuthoringJsonService.Export(model);

            Assert.IsTrue(result.Success, result.DiagnosticsText);
            StringAssert.Contains("\"onHitBuffs\"", result.ActionsJson);
            Assert.IsTrue(result.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(301), out CharacterActionConfig exported));
            Assert.AreEqual(1, exported.CombatTrack.Events[0].HitEvent.OnHitBuffCount);
            Assert.AreEqual(601009, exported.CombatTrack.Events[0].HitEvent.OnHitBuffs[0].BuffId);
            Assert.AreEqual(1, exported.GameplayTrack.Events[0].HitEvent.OnHitBuffCount);
            Assert.AreEqual(601010, exported.GameplayTrack.Events[0].HitEvent.OnHitBuffs[0].BuffId);
        }

        [Test]
        public void OnHitBuffCatalog_BuildsSummaryFromGeneratedBuffJson()
        {
            const string json = @"
{
  ""definitions"": [
    {
      ""buffId"": 601009,
      ""stableId"": ""buff.character_test.on_hit.defense_break"",
      ""displayName"": ""CharacterTest On-Hit Defense Break"",
      ""defaultDurationFrames"": 300,
      ""maxStackCount"": 1,
      ""applyStackCount"": 1,
      ""stackPolicy"": ""RefreshDuration"",
      ""isPermanent"": false,
      ""effects"": [
        {
          ""effectId"": 2,
          ""kind"": ""Status"",
          ""statusId"": 9001
        },
        {
          ""effectId"": 1,
          ""kind"": ""AttributeModifier"",
          ""attributeId"": 3,
          ""addValue"": -3
        }
      ]
    }
  ]
}";

            List<CharacterActionOnHitBuffChoice> choices =
                CharacterActionOnHitBuffCatalog.BuildChoicesFromJson(json, 0);

            Assert.AreEqual(1, choices.Count);
            Assert.AreEqual(601009, choices[0].BuffId);
            Assert.IsTrue(choices[0].IsKnown);
            Assert.AreEqual("601009 - CharacterTest On-Hit Defense Break", choices[0].Label);
            Assert.AreEqual(
                "300f | RefreshDuration | stack +1/max 1 | Defense(3) -3, Status(9001)",
                choices[0].Summary);
        }

        [Test]
        public void OnHitBuffCatalog_PreservesUnknownCurrentAndIgnoresMalformedJson()
        {
            List<CharacterActionOnHitBuffChoice> unknown =
                CharacterActionOnHitBuffCatalog.BuildChoicesFromJson(@"{ ""definitions"": [] }", 601099);

            Assert.AreEqual(1, unknown.Count);
            Assert.AreEqual(601099, unknown[0].BuffId);
            Assert.IsFalse(unknown[0].IsKnown);
            Assert.AreEqual("601099 [unknown]", unknown[0].Label);
            Assert.AreEqual(string.Empty, unknown[0].Summary);

            List<CharacterActionOnHitBuffChoice> malformed =
                CharacterActionOnHitBuffCatalog.BuildChoicesFromJson("{ not json", 0);

            Assert.AreEqual(0, malformed.Count);
        }

        [Test]
        public void MotionSummary_MarksPendingTestAndShowsOnlyRelevantMotionPayload()
        {
            var lockEvent = new MotionTrackEventJsonDto
            {
                Frame = 0,
                Kind = CharacterActionTrackEventKind.LockMovement.ToString(),
                MovementMode = CharacterMovementMode.Airborne.ToString(),
                X = 1f,
                Y = 2f,
                Z = 3f,
            };
            var impulseEvent = new MotionTrackEventJsonDto
            {
                Frame = 0,
                Kind = CharacterActionTrackEventKind.ApplyImpulse.ToString(),
                MovementMode = CharacterMovementMode.Airborne.ToString(),
                X = 1f,
                Y = 2f,
                Z = 3f,
            };

            string lockSummary = CharacterActionAuthoringText.MotionSummary(lockEvent);
            string impulseSummary = CharacterActionAuthoringText.MotionSummary(impulseEvent);

            Assert.IsFalse(string.IsNullOrWhiteSpace(lockSummary));
            StringAssert.Contains("移动模式=空中", lockSummary);
            Assert.IsFalse(lockSummary.Contains("A="), lockSummary);
            Assert.IsFalse(lockSummary.Contains("mainHand"), lockSummary);
            Assert.IsFalse(string.IsNullOrWhiteSpace(impulseSummary));
            StringAssert.Contains("冲量=(1, 2, 3)", impulseSummary);
        }

        [Test]
        public void DamageAuthoringText_UsesReadableDamageNatureAndPreservesUnknownValues()
        {
            List<(string Value, string Label)> choices =
                CharacterActionDamageAuthoringText.BuildDamageTypeChoices("legacy_arcane");

            StringAssert.Contains("(slash)", FindDamageChoiceLabel(choices, "slash"));
            StringAssert.Contains("(blunt)", FindDamageChoiceLabel(choices, "blunt"));
            StringAssert.Contains("(force)", FindDamageChoiceLabel(choices, "force"));
            StringAssert.Contains("(fire)", FindDamageChoiceLabel(choices, "fire"));
            StringAssert.Contains("(light)", FindDamageChoiceLabel(choices, "light"));
            StringAssert.Contains("(toxic)", FindDamageChoiceLabel(choices, "toxic"));
            StringAssert.Contains("(wind)", FindDamageChoiceLabel(choices, "wind"));
            StringAssert.Contains("(control)", FindDamageChoiceLabel(choices, "control"));
            StringAssert.Contains("legacy_arcane", FindDamageChoiceLabel(choices, "legacy_arcane"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(CharacterActionDamageAuthoringText.FormatDamageTagLabel("scatter")));
            StringAssert.Contains("HP 6", CharacterActionDamageAuthoringText.DamageTuningBaselineHelpText);
            StringAssert.Contains("Attack 20 x0.65", CharacterActionDamageAuthoringText.DamageTuningBaselineHelpText);
            StringAssert.Contains("19", CharacterActionDamageAuthoringText.DamageTuningBaselineHelpText);
            StringAssert.Contains("34", CharacterActionDamageAuthoringText.DamageTuningBaselineHelpText);
            StringAssert.Contains("3", CharacterActionDamageAuthoringText.DamageTuningBaselineHelpText);
            StringAssert.Contains("6", CharacterActionDamageAuthoringText.BaseDamageFieldLabel);
            StringAssert.Contains("x0.65", CharacterActionDamageAuthoringText.AttackScaleFieldLabel);
            StringAssert.Contains("34", CharacterActionDamageAuthoringText.ImpactPowerFieldLabel);
        }

        [Test]
        public void CombatSummary_FormatsInlineDamageWithReadableNatureAndValues()
        {
            var evt = new CombatTrackEventJsonDto
            {
                HitEvent = new CharacterActionHitEventJsonDto
                {
                    Damage = new CharacterActionHitDamageJsonDto
                    {
                        DamageTypeId = CharacterTestBulkIds38.Id790006,
                        BaseDamage = 6,
                        SourceAttributeScales = CharacterActionHitEventAuthoringModel.BuildAttackScaleArray(0.65f),
                        ImpactPower = 34,
                    },
                },
            };

            string summary = CharacterActionAuthoringText.DamageSummary(evt);

            Assert.AreEqual("伤害=斩击 / HP 6 + 攻击 x0.65 / 冲击 34", summary);
            Assert.IsFalse(summary.Contains("slash"), summary);
        }

        [Test]
        public void CombatSummary_DistinguishesHitVolumeFromWeaponTrace()
        {
            var hitVolumeStart = new CombatTrackEventJsonDto
            {
                Frame = 6,
                EndFrame = 20,
                Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                StableEventId = CharacterTestBulkIds38.Id790011,
                HitEvent = new CharacterActionHitEventJsonDto
                {
                    Damage = new CharacterActionHitDamageJsonDto
                    {
                        DamageTypeId = CharacterTestBulkIds38.Id790006,
                        BaseDamage = 12,
                        ImpactPower = 18,
                    },
                },
                HitVolume = new CharacterActionHitVolumeJsonDto
                {
                    ShapeKind = CharacterActionHitVolumeShapeKind.Capsule.ToString(),
                    LocalPointA = new CharacterActionHitVolumePointJsonDto { Y = 0.8f, Z = 0.6f },
                    LocalPointB = new CharacterActionHitVolumePointJsonDto { Y = 0.8f, Z = 1.8f },
                    Radius = 0.65f,
                },
            };
            var weaponTraceStart = new CombatTrackEventJsonDto
            {
                Frame = 3,
                EndFrame = 6,
                Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                EquipmentSlotId = CharacterTestBulkIds38.Id790007,
                StableEventId = CharacterTestBulkIds38.Id790005,
                HitEvent = new CharacterActionHitEventJsonDto
                {
                    Damage = new CharacterActionHitDamageJsonDto
                    {
                        DamageTypeId = CharacterTestBulkIds38.Id790006,
                        BaseDamage = 6,
                        ImpactPower = 34,
                        Tags = CharacterActionDamageIdentityAuthoring.ExistingTags(new[] { "melee", "weapon", "light" }),
                        AnalysisCategory = CharacterTestBulkIds38.Id790012,
                    },
                },
            };
            string volumeSummary = CharacterActionAuthoringText.HitTraceWindowSummary(hitVolumeStart);
            string weaponSummary = CharacterActionAuthoringText.HitTraceWindowSummary(weaponTraceStart);
            string volumeEventSummary = CharacterActionAuthoringText.CombatSummary(hitVolumeStart);

            StringAssert.Contains("碰撞框判定", volumeSummary);
            StringAssert.Contains("碰撞框判定", volumeSummary);
            StringAssert.Contains("A=(0, 0.8, 0.6)", volumeSummary);
            Assert.IsFalse(volumeSummary.Contains("mainHand"), volumeSummary);
            StringAssert.Contains("碰撞框判定", volumeEventSummary);
            Assert.IsFalse(volumeEventSummary.Contains("mainHand"), volumeEventSummary);
            Assert.IsFalse(string.IsNullOrWhiteSpace(weaponSummary));
            StringAssert.Contains("mainHand", weaponSummary);
        }

        [Test]
        public void CombatSummary_LabelsCasterBodyHitVolume()
        {
            var hitVolumeStart = new CombatTrackEventJsonDto
            {
                Frame = 6,
                EndFrame = 20,
                Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                StableEventId = CharacterTestBulkIds38.Id790011,
                HitEvent = new CharacterActionHitEventJsonDto
                {
                    Damage = new CharacterActionHitDamageJsonDto
                    {
                        DamageTypeId = CharacterTestBulkIds38.Id790006,
                        BaseDamage = 12,
                        ImpactPower = 18,
                    },
                },
                HitVolume = new CharacterActionHitVolumeJsonDto
                {
                    SourceKind = CharacterActionHitVolumeSourceKind.CasterBody.ToString(),
                },
            };
            string volumeSummary = CharacterActionAuthoringText.HitTraceWindowSummary(hitVolumeStart);
            string volumeEventSummary = CharacterActionAuthoringText.CombatSummary(hitVolumeStart);

            StringAssert.Contains("身体撞击判定", volumeSummary);
            StringAssert.Contains("身体撞击判定", volumeSummary);
            StringAssert.Contains("身体撞击判定", volumeSummary);
            Assert.IsFalse(volumeSummary.Contains("A="), volumeSummary);
            Assert.IsFalse(volumeSummary.Contains("A="), volumeSummary);
            StringAssert.Contains("身体撞击判定", volumeEventSummary);
            Assert.IsFalse(volumeEventSummary.Contains("A="), volumeEventSummary);
        }

        [Test]
        public void RootMotionDraftFromAnimation_PrefersAnimationEventAtFrame()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.AnimationTrack.Events = new[]
            {
                new AnimationTrackEventJsonDto
                {
                    Frame = 0,
                    EndFrame = 5,
                    Kind = CharacterActionTrackEventKind.CrossFadeAnimation.ToString(),
                    AnimationActionKey = "anim.first",
                    StableEventId = CharacterTestBulkIds38.Id790013,
                },
                new AnimationTrackEventJsonDto
                {
                    Frame = 10,
                    EndFrame = 15,
                    Kind = CharacterActionTrackEventKind.CrossFadeAnimation.ToString(),
                    AnimationClipResourceKey = "clip.second",
                    StableEventId = CharacterTestBulkIds38.Id790014,
                },
            };

            RootMotionTrackJsonDto rootMotion =
                CharacterActionAuthoringModel.CreateRootMotionDraftFromAnimation(action, preferredFrame: 12);

            Assert.AreEqual(CharacterTestBulkIds38.Id790014, rootMotion.SourceAnimationEventId);
            Assert.AreEqual("clip.second", rootMotion.SourceAnimationClipResourceKey);
        }

        [Test]
        public void RootMotionMerge_ReplacesOnlyTargetRange()
        {
            RootMotionFrameJsonDto[] merged = CharacterActionAuthoringModel.MergeRootMotionFrames(
                new[]
                {
                    new RootMotionFrameJsonDto { Frame = 0, X = 1f },
                    new RootMotionFrameJsonDto { Frame = 10, X = 10f },
                    new RootMotionFrameJsonDto { Frame = 11, X = 11f },
                    new RootMotionFrameJsonDto { Frame = 20, X = 20f },
                },
                new[]
                {
                    new RootMotionFrameJsonDto { Frame = 10, X = 100f },
                    new RootMotionFrameJsonDto { Frame = 12, X = 120f },
                },
                10,
                12);

            Assert.AreEqual(4, merged.Length);
            Assert.AreEqual(1f, FindRootMotionFrame(merged, 0).X);
            Assert.AreEqual(100f, FindRootMotionFrame(merged, 10).X);
            Assert.IsNull(FindRootMotionFrame(merged, 11));
            Assert.AreEqual(120f, FindRootMotionFrame(merged, 12).X);
            Assert.AreEqual(20f, FindRootMotionFrame(merged, 20).X);
        }

        [Test]
        public void RootMotionScale_AppliesOnlyTargetRange()
        {
            RootMotionFrameJsonDto[] scaled = CharacterActionAuthoringModel.ScaleRootMotionFrames(
                new[]
                {
                    new RootMotionFrameJsonDto { Frame = 0, X = 1f, Y = 2f, Z = 3f },
                    new RootMotionFrameJsonDto { Frame = 5, X = 2f, Y = 4f, Z = 6f },
                    new RootMotionFrameJsonDto { Frame = 9, X = 3f, Y = 6f, Z = 9f },
                },
                1,
                8,
                0.5f,
                1f,
                2f);

            Assert.AreEqual(1f, FindRootMotionFrame(scaled, 0).X);
            Assert.AreEqual(3f, FindRootMotionFrame(scaled, 0).Z);
            Assert.AreEqual(1f, FindRootMotionFrame(scaled, 5).X);
            Assert.AreEqual(0f, FindRootMotionFrame(scaled, 0).Y);
            Assert.AreEqual(0f, FindRootMotionFrame(scaled, 5).Y);
            Assert.AreEqual(0f, FindRootMotionFrame(scaled, 9).Y);
            Assert.AreEqual(12f, FindRootMotionFrame(scaled, 5).Z);
            Assert.AreEqual(3f, FindRootMotionFrame(scaled, 9).X);
            Assert.AreEqual(9f, FindRootMotionFrame(scaled, 9).Z);
        }

        [Test]
        public void ReferenceContext_CollectsAnimationRegistryAndCharacterApplicationChoices()
        {
            CharacterActionAuthoringReferenceContext context = CharacterActionAuthoringReferenceContext.CreateDefault();
            MxAnimationClipRegistryAsset asset = ScriptableObject.CreateInstance<MxAnimationClipRegistryAsset>();
            try
            {
                asset.Bindings = new[]
                {
                    new MxAnimationClipRegistryBindingEntry
                    {
                        ActionKey = "anim.asset.light",
                        ClipId = "clip.light",
                    },
                };

                context.MergeFromAnimationClipRegistry(asset);
                context.MergeFromCombatActionSet(new CombatActionSetConfig(
                    new CombatActionSetId(800001),
                    "combat.set",
                    new[]
                    {
                        new CombatActionEntry(
                            "light_attack",
                            new CharacterCombatActionId(400001),
                            "trace.light",
                            "anim.combat.light"),
                    }));
                context.Normalize();

                Assert.IsTrue(context.ContainsAnimationActionKey("set", new MxFramework.CharacterAction.CharacterActionId(301), "anim.asset.light"));
                Assert.IsTrue(context.ContainsAnimationActionKey("set", new MxFramework.CharacterAction.CharacterActionId(301), "anim.combat.light"));
                Assert.IsTrue(context.ContainsCombatActionId("set", new MxFramework.CharacterAction.CharacterActionId(301), CharacterTestBulkIds.Id790005));
                Assert.IsTrue(context.ContainsEquipmentSlotId(CharacterTestBulkIds.Id790006));
                Assert.IsTrue(context.ContainsEquipmentSlotId(CharacterTestBulkIds.Id790007));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ReferenceDiscovery_CollectsProviderBackedAnimationCombatAndResourceChoices()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            MxAnimationClipRegistryAsset asset = ScriptableObject.CreateInstance<MxAnimationClipRegistryAsset>();
            try
            {
                asset.Bindings = new[]
                {
                    new MxAnimationClipRegistryBindingEntry
                    {
                        ActionKey = "anim.discovery.light",
                        ClipId = "clip.discovery.light",
                    },
                };

                var catalog = new ResourceCatalog(
                    "test.catalog",
                    "test.package",
                    new[]
                    {
                        new ResourceCatalogEntry("vfx.discovery.light", ResourceTypeIds.GameObject, "test", "vfx/light"),
                        new ResourceCatalogEntry("sfx.discovery.light", ResourceTypeIds.AudioClip, "test", "sfx/light"),
                    });

                CharacterActionReferenceDiscoveryResult result = CharacterActionReferenceDiscoveryService.Discover(
                    model,
                    new CharacterActionReferenceDiscoveryOptions
                    {
                        IncludeAssetDatabase = false,
                        AnimationRegistryAssets = new[] { asset },
                        CombatActionSets = new[]
                        {
                            new CombatActionSetConfig(
                                new CombatActionSetId(800101),
                                "combat.discovery",
                                new[]
                                {
                                    new CombatActionEntry(
                                        "light_attack",
                                        new CharacterCombatActionId(400101),
                                        "trace.discovery",
                                        "anim.combat.discovery"),
                                }),
                        },
                        ResourceCatalogs = new[] { catalog },
                        EquipmentSchemas = new[]
                        {
                            new EquipmentSchemaConfig(
                                new EquipmentSchemaId(910001),
                                "schema.discovery",
                                new[]
                                {
                                    new EquipmentSlotEntry(
                                        CharacterTestBulkIds.Id790008,
                                        EquipmentSlotKind.Custom,
                                        "Tool Slot",
                                        Array.Empty<string>()),
                                },
                                Array.Empty<EquipmentExclusiveGroupEntry>(),
                                Array.Empty<EquipmentStateId>()),
                        },
                    });

                Assert.IsTrue(result.ReferenceContext.ContainsAnimationActionKey("set", CharacterTestActionIds.Action_action, "anim.discovery.light"));
                Assert.IsTrue(result.ReferenceContext.ContainsAnimationActionKey("set", CharacterTestActionIds.Action_action, "anim.combat.discovery"));
                Assert.IsTrue(result.ReferenceContext.ContainsCombatActionId("set", CharacterTestActionIds.Action_action, CharacterTestBulkIds36.Id790001));
                Assert.IsTrue(result.ReferenceContext.ContainsPresentationResource("set", CharacterTestActionIds.Action_action, CharacterActionResourceDependencyKind.VfxResource, "vfx.discovery.light"));
                Assert.IsTrue(result.ReferenceContext.ContainsPresentationResource("set", CharacterTestActionIds.Action_action, CharacterActionResourceDependencyKind.AudioCue, "sfx.discovery.light"));
                Assert.IsTrue(result.ReferenceContext.ContainsEquipmentSlotId(CharacterTestBulkIds.Id790008));
                Assert.IsFalse(ContainsDiscoveryDiagnostic(result, CharacterActionReferenceDiscoveryService.AnimationRegistryUnavailableCode));
                Assert.IsFalse(ContainsDiscoveryDiagnostic(result, CharacterActionReferenceDiscoveryService.CharacterApplicationConfigUnavailableCode));
                Assert.IsFalse(ContainsDiscoveryDiagnostic(result, CharacterActionReferenceDiscoveryService.ResourceCatalogUnavailableCode));
                StringAssert.Contains("ResourceCatalog test.catalog", result.SummaryText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ReferenceDiscovery_WhenProvidersUnavailable_ReportsDiagnosticsAndKeepsManualFallback()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            model.ReferenceContext.AddAnimationActionKey("manual.anim.only");

            CharacterActionReferenceDiscoveryResult result = CharacterActionReferenceDiscoveryService.Discover(
                model,
                new CharacterActionReferenceDiscoveryOptions
                {
                    IncludeAssetDatabase = false,
                });

            Assert.IsTrue(result.ReferenceContext.ContainsAnimationActionKey("set", CharacterTestActionIds.Action_action, "manual.anim.only"));
            Assert.IsTrue(ContainsDiscoveryDiagnostic(result, CharacterActionReferenceDiscoveryService.AnimationRegistryUnavailableCode));
            Assert.IsTrue(ContainsDiscoveryDiagnostic(result, CharacterActionReferenceDiscoveryService.CharacterApplicationConfigUnavailableCode));
            Assert.IsTrue(ContainsDiscoveryDiagnostic(result, CharacterActionReferenceDiscoveryService.ResourceCatalogUnavailableCode));
            StringAssert.Contains("Manual fallback", result.SummaryText);
        }

        [Test]
        public void Export_WithMissingAnimationPickerReference_BlocksAsCrossModuleDiagnostic()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            model.ReferenceContext.AnimationActionKeys.Remove("anim.basic_jump");

            CharacterActionAuthoringExportResult result = CharacterActionAuthoringJsonService.Export(model);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("[Cross-module Reference]", result.DiagnosticsText);
            StringAssert.Contains(CharacterActionDiagnosticCodes.AnimationActionMissing, result.DiagnosticsText);
        }

        [Test]
        public void Export_WithMissingCombatAndPresentationReferences_BlocksAsCrossModuleDiagnostics()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            CharacterActionJsonDto lightAttack = model.Actions[1];
            lightAttack.CombatTrack = new CombatTrackJsonDto
            {
                CombatActionId = CharacterTestBulkIds38.Id790015,
            };
            lightAttack.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Frame = 1,
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.missing",
                        StableEventId = CharacterTestBulkIds38.Id790016,
                    },
                },
            };

            CharacterActionAuthoringExportResult result = CharacterActionAuthoringJsonService.Export(model);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("[Cross-module Reference]", result.DiagnosticsText);
            StringAssert.Contains(CharacterActionDiagnosticCodes.CombatActionMissing, result.DiagnosticsText);
            StringAssert.Contains(CharacterActionDiagnosticCodes.PresentationResourceMissing, result.DiagnosticsText);
        }

        [Test]
        public void EditorWindow_CreateGui_BuildsMenuBackedAuthoringSurface()
        {
            CharacterActionAuthoringWindow window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
            try
            {
                window.CreateGUI();

                Assert.NotNull(window.rootVisualElement.Q(className: "ca-chrome"));
                Assert.NotNull(window.rootVisualElement.Q(className: "ca-content-frame"));
                Assert.NotNull(window.rootVisualElement.Q(className: "ca-content-host"));
                Assert.NotNull(window.rootVisualElement.Q(className: "ca-footer"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void EditorWindow_OpenForAbilityBinding_FocusesWithoutAutoAddingMissingBinding()
        {
            DestroyOpenAuthoringWindows();
            CharacterActionAuthoringWindow window = null;
            try
            {
                CharacterActionAuthoringOpenResult result = CharacterActionAuthoringWindow.OpenForAbilityBinding(
                    new CharacterActionAuthoringOpenRequest
                    {
                        RequestId = "test-open-ability",
                        Package = "Tools/MxFramework.Authoring/samples/character-codex-flow-test",
                        AbilityId = 900001,
                        ProfileSourceDirectory = "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test",
                        ActionSetSourceDirectory = "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test",
                        ActionLibrarySourceDirectory = "CharacterActionSource/Libraries/Common",
                        ActionSetStableId = "codex_flow_test.sword_empty",
                        Mode = "focusOnly",
                    });

                Assert.AreEqual("opened", result.Status);
                window = FindOpenAuthoringWindow();
                Assert.NotNull(window);
                Label title = window.rootVisualElement.Q<Label>("focused-ability-binding-title");
                Assert.NotNull(title);
                StringAssert.Contains("abilityId 900001", title.text);
                List<Button> buttons = window.rootVisualElement.Query<Button>().ToList();
                Assert.IsTrue(buttons.Exists(button => button.text.IndexOf("abilityId", StringComparison.Ordinal) >= 0));
                Assert.IsNull(window.rootVisualElement.Q(name: "focused-ability-binding"));
                CharacterActionAuthoringDocumentSession session =
                    GetPrivateField<CharacterActionAuthoringDocumentSession>(window, "_documentSession");
                Assert.NotNull(session);
                Assert.AreEqual("codex_flow_test.sword_empty", session.Selection.ActionSetStableId);
                Assert.AreEqual("Common", session.Selection.ActionLibraryId);
                Assert.AreEqual(
                    CountActions("CharacterActionSource/Libraries/Common/character_actions.json"),
                    session.ProfileActions.Count);
            }
            finally
            {
                DestroyOpenAuthoringWindows();
            }
        }

        [Test]
        public void OpenRequestBridge_ParsesStructuredActionSelectionAndWritesObservableStatus()
        {
            string requestId = "test-action-status-" + Guid.NewGuid().ToString("N");
            string statusPath = Path.Combine(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                "Temp",
                "MxFrameworkAuthoring",
                "CharacterAction",
                "status",
                requestId + ".json");
            try
            {
                string json = new JObject
                {
                    ["requestId"] = requestId,
                    ["package"] = "Authoring/CharacterPackages/character-combat-girl-sword",
                    ["profileSourceDirectory"] = "Assets/MxFrameworkGenerated/CharacterActions/Profiles/combat_girl_sword",
                    ["actionSetSourceDirectory"] = "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/combat_girl_sword",
                    ["actionLibrarySourceDirectory"] = "CharacterActionSource/Libraries/Common",
                    ["profileId"] = "combat_girl_sword.default",
                    ["actionSetStableId"] = "combat_girl_sword.sword_empty",
                    ["actionLibraryId"] = "Common",
                    ["actionId"] = 301,
                    ["sourceHash"] = ComputeSha256("CharacterActionSource/Libraries/Common/character_actions.json"),
                    ["mode"] = "readOnly",
                }.ToString(Formatting.None);

                Assert.IsTrue(
                    CharacterActionAuthoringOpenRequestBridge.TryReadRequestJson(
                        json,
                        out CharacterActionAuthoringOpenRequest request,
                        out string error),
                    error);
                Assert.AreEqual("combat_girl_sword.default", request.ProfileId);
                Assert.AreEqual("combat_girl_sword.sword_empty", request.ActionSetStableId);
                Assert.AreEqual("Common", request.ActionLibraryId);
                Assert.AreEqual(CharacterTestActionIds.Action_light_attack, request.ActionId);
                Assert.AreEqual("readOnly", request.Mode);
                StringAssert.StartsWith("sha256:", request.SourceHash);

                CharacterActionAuthoringOpenRequestBridge.WriteStatus(
                    new CharacterActionAuthoringOpenResult
                    {
                        RequestId = requestId,
                        Status = "saved",
                        Message = "saved to formal source",
                        ActionId = 301,
                    });

                Assert.IsTrue(File.Exists(statusPath));
                JObject status = JObject.Parse(File.ReadAllText(statusPath));
                Assert.AreEqual(requestId, (string)status["requestId"]);
                Assert.AreEqual("saved", (string)status["status"]);
                Assert.AreEqual("light_attack", (string)status["actionId"]);
                Assert.IsFalse(string.IsNullOrWhiteSpace((string)status["updatedUtc"]));
            }
            finally
            {
                if (File.Exists(statusPath))
                    File.Delete(statusPath);
            }
        }

        [Test]
        public void EditorWindow_OpenForSelection_ExactlySelectsCommonActionAndBlocksMutation()
        {
            DestroyOpenAuthoringWindows();
            ResetOpenRequestSessionState();
            try
            {
                CharacterActionAuthoringOpenRequest request = CreateCombatGirlActionOpenRequest(
                    "test-open-common-action",
                    "CharacterActionSource/Libraries/Common",
                    "Common",
                    301,
                    "readOnly");

                CharacterActionAuthoringOpenResult result =
                    CharacterActionAuthoringWindow.OpenForSelection(request);

                Assert.AreEqual("opened", result.Status, result.Message);
                CharacterActionAuthoringWindow window = FindOpenAuthoringWindow();
                Assert.NotNull(window);
                CharacterActionAuthoringDocumentSession session =
                    GetPrivateField<CharacterActionAuthoringDocumentSession>(window, "_documentSession");
                Assert.AreEqual("combat_girl_sword.default", session.Selection.ProfileStableId);
                Assert.AreEqual("combat_girl_sword.sword_empty", session.Selection.ActionSetStableId);
                Assert.AreEqual("Common", session.Selection.ActionLibraryId);
                Assert.AreEqual(301, session.Selection.ActionId);
                Assert.AreEqual(301, session.CurrentAction.ActionId);
                Assert.AreEqual(
                    "CharacterActionSource/Libraries/Common",
                    session.CurrentActionLibrary.Source.DirectoryPath);
                Assert.IsTrue(GetPrivateField<bool>(window, "_openRequestReadOnly"));

                string displayName = session.CurrentAction.DisplayName;
                int readOnlyNotices = 0;
                SetPrivateField(window, "_readOnlySourceNoticeOverride", new Action(() => readOnlyNotices++));
                InvokePrivate(
                    window,
                    "ApplyFieldChange",
                    new Action(() => session.CurrentAction.DisplayName = "must-not-change"),
                    true);

                Assert.AreEqual(1, readOnlyNotices);
                Assert.AreEqual(displayName, session.CurrentAction.DisplayName);
                Assert.IsFalse(session.CurrentActionLibrary.State.IsDirty);
            }
            finally
            {
                DestroyOpenAuthoringWindows();
            }
        }

        [Test]
        public void EditorWindow_OpenForSelection_RejectsStaleSourceHashBeforeLoading()
        {
            DestroyOpenAuthoringWindows();
            try
            {
                CharacterActionAuthoringOpenRequest request = CreateCombatGirlActionOpenRequest(
                    "test-open-stale-hash",
                    "CharacterActionSource/Libraries/Common",
                    "Common",
                    301,
                    "readOnly");
                request.SourceHash = "sha256:" + new string('0', 64);

                CharacterActionAuthoringOpenResult result =
                    CharacterActionAuthoringWindow.OpenForSelection(request);

                Assert.AreEqual("failed", result.Status);
                StringAssert.Contains("changed on disk", result.Message);
            }
            finally
            {
                DestroyOpenAuthoringWindows();
            }
        }

        [Test]
        public void EditorWindow_EditableCharacterAction_DetectsHashConflictBeforeSave()
        {
            DestroyOpenAuthoringWindows();
            ResetOpenRequestSessionState();
            const string sourcePath =
                "Authoring/CharacterPackages/character-combat-girl-sword/config/character_actions.json";
            string sourceBefore = File.ReadAllText(sourcePath);
            try
            {
                CharacterActionAuthoringOpenRequest request = CreateCombatGirlActionOpenRequest(
                    "test-edit-character-action-conflict",
                    "Authoring/CharacterPackages/character-combat-girl-sword/config",
                    "combat_girl_sword",
                    950002,
                    "edit");
                CharacterActionAuthoringOpenResult result =
                    CharacterActionAuthoringWindow.OpenForSelection(request);
                Assert.AreEqual("opened", result.Status, result.Message);

                CharacterActionAuthoringWindow window = FindOpenAuthoringWindow();
                CharacterActionAuthoringDocumentSession session =
                    GetPrivateField<CharacterActionAuthoringDocumentSession>(window, "_documentSession");
                Assert.AreEqual("combat_girl_sword", session.Selection.ActionLibraryId);
                Assert.AreEqual(950002, session.Selection.ActionId);
                session.CurrentAction.DisplayName += " unsaved";
                session.MarkCurrentActionChanged();
                SetPrivateField(window, "_openRequestSourceHash", "sha256:" + new string('0', 64));

                object[] arguments = { null };
                bool valid = (bool)typeof(CharacterActionAuthoringWindow)
                    .GetMethod(
                        "ValidateOpenRequestSourceHashBeforeSave",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(window, arguments);

                Assert.IsFalse(valid);
                StringAssert.Contains("changed on disk", (string)arguments[0]);
                Assert.AreEqual(sourceBefore, File.ReadAllText(sourcePath));
                Assert.IsTrue(session.CurrentActionLibrary.State.IsDirty);
            }
            finally
            {
                Assert.AreEqual(sourceBefore, File.ReadAllText(sourcePath));
                DestroyOpenAuthoringWindows();
            }
        }

        [Test]
        public void EditorWindow_OpenForAbilityBinding_SameProfilePreservesDraftAndDifferentSourceIsBlocked()
        {
            DestroyOpenAuthoringWindows();
            try
            {
                CharacterActionAuthoringOpenRequest request = CreateCodexAbilityOpenRequest(
                    "test-open-draft-initial",
                    "codex_flow_test.empty_empty",
                    "Assets/MxFrameworkGenerated/CharacterActions/Libraries/codex_flow_test");
                Assert.AreEqual(
                    "opened",
                    CharacterActionAuthoringWindow.OpenForAbilityBinding(request).Status);

                CharacterActionAuthoringWindow window = FindOpenAuthoringWindow();
                CharacterActionAuthoringDocumentSession session =
                    GetPrivateField<CharacterActionAuthoringDocumentSession>(window, "_documentSession");
                CharacterActionSetJsonDto editedActionSet = session.CurrentActionSet;
                editedActionSet.DisplayName = "Unsaved source-switch regression";
                session.MarkCurrentActionSetChanged();

                CharacterActionAuthoringOpenResult sameProfileResult =
                    CharacterActionAuthoringWindow.OpenForAbilityBinding(
                        CreateCodexAbilityOpenRequest(
                            "test-open-draft-same-profile",
                            "codex_flow_test.sword_empty",
                            "CharacterActionSource/Libraries/Common"));

                Assert.AreEqual("opened", sameProfileResult.Status);
                Assert.AreSame(
                    session,
                    GetPrivateField<CharacterActionAuthoringDocumentSession>(window, "_documentSession"));
                Assert.AreEqual("Unsaved source-switch regression", editedActionSet.DisplayName);
                Assert.IsTrue(session.FindActionSetBundle("codex_flow_test.empty_empty").State.IsDirty);
                Assert.AreEqual("codex_flow_test.sword_empty", session.Selection.ActionSetStableId);
                Assert.AreEqual("Common", session.Selection.ActionLibraryId);

                CharacterActionAuthoringOpenRequest differentSource = CreateCodexAbilityOpenRequest(
                    "test-open-draft-different-source",
                    "missing.action.set",
                    "Assets/DoesNotExist/CharacterActions/Libraries/Missing");
                differentSource.ProfileSourceDirectory =
                    "Assets/DoesNotExist/CharacterActions/Profiles/Missing";
                differentSource.ActionSetSourceDirectory =
                    "Assets/DoesNotExist/CharacterActions/ActionSets/Missing";
                CharacterActionAuthoringOpenResult blocked =
                    CharacterActionAuthoringWindow.OpenForAbilityBinding(differentSource);

                Assert.AreEqual("failed", blocked.Status);
                Assert.AreSame(
                    session,
                    GetPrivateField<CharacterActionAuthoringDocumentSession>(window, "_documentSession"));
                Assert.AreEqual("Unsaved source-switch regression", editedActionSet.DisplayName);
                Assert.IsTrue(session.FindActionSetBundle("codex_flow_test.empty_empty").State.IsDirty);
                Assert.AreEqual(
                    "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test",
                    GetPrivateField<string>(window, "_profileSourceDirectory"));
            }
            finally
            {
                DestroyOpenAuthoringWindows();
            }
        }

        [Test]
        public void EditorWindow_LoadInvalidProfileSource_PreservesDirtySessionAndPendingDraftMetadata()
        {
            DestroyOpenAuthoringWindows();
            string root = Path.Combine(
                "Temp",
                "CharacterActionInvalidProfileSourceTests",
                Guid.NewGuid().ToString("N"));
            try
            {
                Assert.AreEqual(
                    "opened",
                    CharacterActionAuthoringWindow.OpenForAbilityBinding(
                        CreateCodexAbilityOpenRequest(
                            "test-invalid-profile-source",
                            "codex_flow_test.empty_empty",
                            "Assets/MxFrameworkGenerated/CharacterActions/Libraries/codex_flow_test")).Status);
                CharacterActionAuthoringWindow window = FindOpenAuthoringWindow();
                CharacterActionAuthoringDocumentSession session =
                    GetPrivateField<CharacterActionAuthoringDocumentSession>(window, "_documentSession");
                session.CurrentActionSet.DisplayName = "Unsaved invalid-source regression";
                session.MarkCurrentActionSetChanged();
                SetPrivateField(window, "_pendingDocumentSessionDraftsJson", "pending-draft-sentinel");

                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "character_action_profile.json"), "{ invalid json");
                var invalidSource = new CharacterActionAuthoringSourceEntry(root, "invalid");
                CharacterActionAuthoringSourceEntry[] original =
                    GetPrivateField<CharacterActionAuthoringSourceEntry[]>(window, "_profileSources");
                var sources = new CharacterActionAuthoringSourceEntry[original.Length + 1];
                Array.Copy(original, sources, original.Length);
                sources[sources.Length - 1] = invalidSource;
                SetPrivateField(window, "_profileSources", sources);

                bool loaded = (bool)InvokePrivate(
                    window,
                    "LoadProfileSource",
                    sources.Length - 1,
                    true,
                    false,
                    false);

                Assert.IsFalse(loaded);
                Assert.AreSame(
                    session,
                    GetPrivateField<CharacterActionAuthoringDocumentSession>(window, "_documentSession"));
                Assert.AreEqual("Unsaved invalid-source regression", session.CurrentActionSet.DisplayName);
                Assert.NotNull(session.CurrentActionSet);
                Assert.IsTrue(session.FindActionSetBundle(session.CurrentActionSet.StableId).State.IsDirty);
                Assert.AreEqual(
                    "pending-draft-sentinel",
                    GetPrivateField<string>(window, "_pendingDocumentSessionDraftsJson"));
            }
            finally
            {
                DestroyOpenAuthoringWindows();
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void EditorWindow_SelectDifferentProfile_CancelPreservesSessionSelectionAndDrafts()
        {
            string root = Path.Combine(
                "Temp",
                "CharacterActionProfileSwitchTests",
                Guid.NewGuid().ToString("N"));
            CharacterActionAuthoringWindow window = null;
            try
            {
                string profileDirectory = Path.Combine(root, "Profiles", "codex_flow_test");
                string actionSetDirectory = Path.Combine(root, "ActionSets", "codex_flow_test");
                string characterLibraryDirectory = Path.Combine(root, "Libraries", "codex_flow_test");
                string commonLibraryDirectory = Path.Combine(root, "Libraries", "Common");
                CopyJsonDirectory(
                    "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test",
                    profileDirectory);
                CopyJsonDirectory(
                    "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test",
                    actionSetDirectory);
                CopyJsonDirectory(
                    "Assets/MxFrameworkGenerated/CharacterActions/Libraries/codex_flow_test",
                    characterLibraryDirectory);
                CopyJsonDirectory(
                    "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common",
                    commonLibraryDirectory);

                var profileSource = new CharacterActionAuthoringSourceEntry(
                    profileDirectory,
                    "codex_flow_test");
                JObject profileRoot = JObject.Parse(File.ReadAllText(profileSource.ProfilePath));
                JObject alternate = (JObject)profileRoot["profiles"]?[0]?.DeepClone();
                Assert.NotNull(alternate);
                alternate["stableId"] = "codex_flow_test.alternate";
                alternate["displayName"] = "Alternate Profile";
                ((JArray)profileRoot["profiles"]).Add(alternate);
                File.WriteAllText(profileSource.ProfilePath, profileRoot.ToString());

                var actionSetSource = new CharacterActionAuthoringSourceEntry(
                    actionSetDirectory,
                    "codex_flow_test");
                var characterLibrarySource = new CharacterActionAuthoringSourceEntry(
                    characterLibraryDirectory,
                    "codex_flow_test");
                var commonLibrarySource = new CharacterActionAuthoringSourceEntry(
                    commonLibraryDirectory,
                    "Common");
                CharacterActionAuthoringDocumentSession session =
                    CharacterActionAuthoringDocumentSession.Load(
                        profileSource,
                        new[] { actionSetSource },
                        new[] { characterLibrarySource, commonLibrarySource },
                        "codex_flow_test.default");

                window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
                SetPrivateField(window, "_actionSetSources", new[] { actionSetSource });
                SetPrivateField(
                    window,
                    "_actionLibrarySources",
                    new[] { characterLibrarySource, commonLibrarySource });
                InvokePrivate(window, "AdoptDocumentSession", session);
                session.CurrentActionSet.DisplayName = "Unsaved profile-switch regression";
                session.MarkCurrentActionSetChanged();
                SetPrivateField(window, "_pendingDocumentSessionDraftsJson", "profile-switch-sentinel");
                int confirmationCount = 0;
                SetPrivateField(
                    window,
                    "_documentGraphSwitchConfirmationOverride",
                    new Func<bool>(() =>
                    {
                        confirmationCount++;
                        return false;
                    }));

                bool selected = (bool)InvokePrivate(
                    window,
                    "SelectDocumentProfile",
                    "codex_flow_test.alternate");

                Assert.IsFalse(selected);
                Assert.AreEqual(1, confirmationCount);
                Assert.AreSame(
                    session,
                    GetPrivateField<CharacterActionAuthoringDocumentSession>(window, "_documentSession"));
                Assert.AreEqual("codex_flow_test.default", session.Selection.ProfileStableId);
                Assert.AreEqual("Unsaved profile-switch regression", session.CurrentActionSet.DisplayName);
                Assert.IsTrue(session.FindActionSetBundle(session.CurrentActionSet.StableId).State.IsDirty);
                Assert.AreEqual(
                    "profile-switch-sentinel",
                    GetPrivateField<string>(window, "_pendingDocumentSessionDraftsJson"));
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void ProjectileAuthoringStore_LoadsAuthoringAndGeneratedProjectiles()
        {
            WithProjectileAuthoringFixture((options, _) =>
            {
                CharacterActionProjectileAuthoringStore store =
                    CharacterActionProjectileAuthoringStore.Load(options, "char.codex_flow_test");

                Assert.AreEqual(5, store.Records.Count);
                Assert.IsTrue(store.TryGetByStableId(
                    "character_test.fireball.projectile",
                    out CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord fireball));
                Assert.AreEqual(91001, fireball.DefinitionId);
                Assert.AreEqual("Constant", fireball.SpeedProfileKind);
                Assert.AreEqual(0.35f, fireball.SpeedPerFrame, 0.0001f);
                Assert.AreEqual(0.25f, fireball.Radius, 0.0001f);
                Assert.AreEqual("fire", fireball.DamageTypeId);
                Assert.AreEqual(10, fireball.BaseDamage);
                Assert.AreEqual(8, fireball.ImpactPower);
                Assert.AreEqual(25, fireball.LaunchPower);
                Assert.AreEqual("vfx.magic.fireball:GameObject@mxframework.samples", fireball.FlightPrefabResource);
                Assert.IsTrue(fireball.GeneratedInSync);
                Assert.IsTrue(fireball.IsLegacyV1PresentationBinding);
                Assert.AreEqual(
                    "compatibility / legacy v1",
                    CharacterActionProjectileInspectorViewModel.Create(fireball).SyncBadgeText);
                StringAssert.Contains("character_test.fireball.projectile", fireball.ChoiceLabel);
                StringAssert.Contains("Direct hit", fireball.ChoiceLabel);
                StringAssert.Contains("31.5m", fireball.ChoiceLabel);
                Assert.IsTrue(store.TryGetByStableId(
                    "character_test.fireball_bezier.projectile",
                    out CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord bezier));
                Assert.AreEqual("BezierToTarget", bezier.MotionKind);
                Assert.AreEqual(3f, bezier.BezierControlOffsetAY, 0.0001f);
                Assert.AreEqual(-4f, bezier.BezierControlOffsetBZ, 0.0001f);
            });
        }

        [Test]
        public void M7_ProjectilePresentationBindingSemanticComparisonNormalizesV2AndLegacyV1()
        {
            JObject sourceV2 = CreatePresentationBindingV2Fixture();
            JObject reorderedV2 = (JObject)sourceV2.DeepClone();
            reorderedV2["slots"] = new JObject(
                ((JObject)reorderedV2["slots"]).Properties().Reverse());

            CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData source =
                CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.FromJson(
                    sourceV2,
                    "source.json",
                    true);
            CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData generated =
                CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.FromJson(
                    reorderedV2,
                    "generated.json",
                    false);

            Assert.IsTrue(
                CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.ValueEquals(
                    source,
                    generated));

            var legacyV1 = new JObject
            {
                ["projectileDefinitionId"] = 91001,
                ["prefabResource"] = CreatePresentationResource("vfx.flight", "GameObject", "blue", "sample"),
            };
            var canonicalV2 = new JObject
            {
                ["projectileDefinitionId"] = 91001,
                ["viewNamePrefix"] = "ProjectileVFX",
                ["slots"] = new JObject
                {
                    ["flightBody"] = CreatePresentationSlot(
                        "vfx.flight", "GameObject", "blue", "sample", "ProjectileForward", 0, 0),
                },
                ["exitPolicy"] = CreateDefaultPresentationExitPolicy(),
            };
            canonicalV2["slots"]["flightBody"]["localOffset"] = new JObject
            {
                ["x"] = 0,
                ["y"] = 0,
                ["z"] = 0,
            };

            Assert.IsTrue(
                CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.ValueEquals(
                    CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.FromJson(
                        legacyV1,
                        "legacy.json",
                        true),
                    CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.FromJson(
                        canonicalV2,
                        "generated.json",
                        false)),
                "Legacy v1 prefabResource must normalize to the canonical v2 flightBody/default-exit meaning.");
        }

        [Test]
        public void M7_ProjectilePresentationBindingSemanticComparisonDetectsEveryV2FieldFamily()
        {
            JObject baselineJson = CreatePresentationBindingV2Fixture();
            CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData baseline =
                CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.FromJson(
                    baselineJson,
                    "source.json",
                    true);

            string[] slotNames = { "spawn", "flightBody", "trail", "directImpact", "areaImpact", "blockedImpact", "expire" };
            for (int i = 0; i < slotNames.Length; i++)
            {
                string slotName = slotNames[i];
                AssertPresentationBindingMutationIsDifferent(
                    baseline,
                    baselineJson,
                    root => root["slots"][slotName]["resource"]["id"] = "changed." + slotName,
                    "resource id: " + slotName);
            }

            AssertPresentationBindingMutationIsDifferent(baseline, baselineJson, root => root["slots"]["spawn"]["resource"]["typeId"] = "Material", "resource type");
            AssertPresentationBindingMutationIsDifferent(baseline, baselineJson, root => root["slots"]["spawn"]["resource"]["variant"] = "changed", "resource variant");
            AssertPresentationBindingMutationIsDifferent(baseline, baselineJson, root => root["slots"]["spawn"]["resource"]["packageId"] = "changed", "resource package");
            AssertPresentationBindingMutationIsDifferent(baseline, baselineJson, root => root["slots"]["spawn"]["orientation"] = "WorldUp", "orientation");
            AssertPresentationBindingMutationIsDifferent(baseline, baselineJson, root => root["slots"]["spawn"]["localOffset"]["x"] = 9f, "local offset");
            AssertPresentationBindingMutationIsDifferent(baseline, baselineJson, root => root["slots"]["spawn"]["lifetimeFramesOverride"] = 99, "lifetime override");
            AssertPresentationBindingMutationIsDifferent(baseline, baselineJson, root => root["slots"]["spawn"]["vfxDefinitionId"] = 999, "vfx definition id");
            AssertPresentationBindingMutationIsDifferent(baseline, baselineJson, root => root["viewNamePrefix"] = "Changed", "view name prefix");
            AssertPresentationBindingMutationIsDifferent(baseline, baselineJson, root => root["exitPolicy"]["acceptedImpact"] = "Dissipate", "accepted exit");
            AssertPresentationBindingMutationIsDifferent(baseline, baselineJson, root => root["exitPolicy"]["blockedImpact"] = "Complete", "blocked exit");
            AssertPresentationBindingMutationIsDifferent(baseline, baselineJson, root => root["exitPolicy"]["expiration"] = "Dissipate", "expiration exit");
            AssertPresentationBindingMutationIsDifferent(baseline, baselineJson, root => root["exitPolicy"]["externalRemoval"] = "Complete", "external removal exit");
            AssertPresentationBindingMutationIsDifferent(baseline, baselineJson, root => root["exitPolicy"]["runtimeReset"] = "Complete", "runtime reset exit");
        }

        [Test]
        public void M7_ProjectilePresentationBindingTreatsSourceVfxDefinitionIdAsCompilerDerived()
        {
            JObject sourceJson = CreatePresentationBindingV2Fixture();
            ((JObject)sourceJson["slots"]["areaImpact"]).Remove("vfxDefinitionId");
            JObject generatedJson = (JObject)CreatePresentationBindingV2Fixture().DeepClone();

            CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData source =
                CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.FromJson(
                    sourceJson, "source.json", true);
            CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData generated =
                CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.FromJson(
                    generatedJson, "generated.json", false);
            Assert.IsTrue(
                CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.ValueEquals(
                    source, generated));

            JObject changedGeneratedJson = (JObject)generatedJson.DeepClone();
            changedGeneratedJson["slots"]["areaImpact"]["vfxDefinitionId"] = 99999;
            CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData changedGenerated =
                CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.FromJson(
                    changedGeneratedJson, "generated-2.json", false);
            Assert.IsFalse(
                CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.ValueEquals(
                    generated, changedGenerated),
                "Generated-style projections must compare their resolved compact VFX ids strictly.");
        }

        private static void AssertPresentationBindingMutationIsDifferent(
            CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData baseline,
            JObject baselineJson,
            Action<JObject> mutate,
            string message)
        {
            JObject changedJson = (JObject)baselineJson.DeepClone();
            mutate(changedJson);
            CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData changed =
                CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.FromJson(
                    changedJson,
                    "generated.json",
                    false);
            Assert.IsFalse(
                CharacterActionProjectileAuthoringStore.CharacterActionProjectilePresentationBindingData.ValueEquals(
                    baseline,
                    changed),
                message);
        }

        private static JObject CreatePresentationBindingV2Fixture()
        {
            string[] names = { "spawn", "flightBody", "trail", "directImpact", "areaImpact", "blockedImpact", "expire" };
            string[] orientations = { "ProjectileForward", "ProjectileForward", "ProjectileForward", "SurfaceNormal", "ImpactNormalOrWorldUp", "SurfaceNormal", "ProjectileForward" };
            var slots = new JObject();
            for (int i = 0; i < names.Length; i++)
            {
                slots[names[i]] = CreatePresentationSlot(
                    "vfx." + names[i],
                    "GameObject",
                    i == 0 ? "blue" : string.Empty,
                    "sample",
                    orientations[i],
                    i + 1,
                    i >= 3 || i == 0 || i == 6 ? 12000 + i : 0);
            }

            return new JObject
            {
                ["projectileDefinitionId"] = 91001,
                ["viewNamePrefix"] = "ProjectileVFX",
                ["slots"] = slots,
                ["exitPolicy"] = CreateDefaultPresentationExitPolicy(),
            };
        }

        private static JObject CreatePresentationSlot(
            string id,
            string typeId,
            string variant,
            string packageId,
            string orientation,
            int lifetimeFramesOverride,
            int vfxDefinitionId)
        {
            return new JObject
            {
                ["resource"] = CreatePresentationResource(id, typeId, variant, packageId),
                ["orientation"] = orientation,
                ["localOffset"] = new JObject { ["x"] = 0.25f, ["y"] = 0.5f, ["z"] = 0.75f },
                ["lifetimeFramesOverride"] = lifetimeFramesOverride,
                ["vfxDefinitionId"] = vfxDefinitionId,
            };
        }

        private static JObject CreatePresentationResource(string id, string typeId, string variant, string packageId)
        {
            return new JObject
            {
                ["id"] = id,
                ["typeId"] = typeId,
                ["variant"] = variant,
                ["packageId"] = packageId,
            };
        }

        private static JObject CreateDefaultPresentationExitPolicy()
        {
            return new JObject
            {
                ["acceptedImpact"] = "Complete",
                ["blockedImpact"] = "Dissipate",
                ["expiration"] = "Complete",
                ["externalRemoval"] = "Dissipate",
                ["runtimeReset"] = "Immediate",
            };
        }

        [Test]
        public void GameplayRequestPickerCatalog_SearchesMeteorTokens()
        {
            var items = new List<CharacterActionGameplayRequestPickerItem>
            {
                new CharacterActionGameplayRequestPickerItem(
                    "character_test.meteor_barrage.projectile.2",
                    "Meteor Barrage 2",
                    "character_test.meteor_barrage.projectile.2 | Area | 42m | fire",
                    "meteor barrage projectile 2 fire",
                    "Projectiles / Meteor",
                    CharacterActionGameplayRequestPickerItemKind.Projectile,
                    isFavorite: false,
                    isRecent: false),
                new CharacterActionGameplayRequestPickerItem(
                    "character_test.fireball.projectile",
                    "Fireball",
                    "character_test.fireball.projectile | Direct | 31.5m | Fire",
                    "fireball projectile fire",
                    "Projectiles / Fire",
                    CharacterActionGameplayRequestPickerItemKind.Projectile,
                    isFavorite: false,
                    isRecent: false),
            };

            List<CharacterActionGameplayRequestPickerSection> sections =
                CharacterActionGameplayRequestPickerCatalog.BuildSections(items, "meteor 2");

            Assert.AreEqual(1, sections.Count);
            Assert.AreEqual("Search Results", sections[0].Title);
            Assert.AreEqual(1, sections[0].Items.Count);
            Assert.AreEqual(CharacterTestNumericIds.Assert_character_test_meteor_barrage_projectile_2, sections[0].Items[0].RequestId);
        }

        [Test]
        public void GameplayRequestPickerCatalog_GroupsFavoritesRecentCurrentAndGameplayRequests()
        {
            WithProjectileAuthoringFixture((options, _) =>
            {
                CharacterActionProjectileAuthoringStore store =
                    CharacterActionProjectileAuthoringStore.Load(options);
                CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
                CharacterActionJsonDto action = model.Actions[0];
                action.GameplayTrack = new GameplayTrackJsonDto
                {
                    Events = new[]
                    {
                        new GameplayTrackEventJsonDto
                        {
                            Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                            RequestId = CharacterTestBulkIds38.Id790017,
                        },
                    },
                };
                var favorites = new HashSet<string>(StringComparer.Ordinal)
                {
                    "character_test.fireball.projectile",
                };
                var recent = new HashSet<string>(StringComparer.Ordinal)
                {
                    "character_test.fireball_burst.projectile",
                };

                List<CharacterActionGameplayRequestPickerItem> items =
                    CharacterActionGameplayRequestPickerCatalog.BuildItems(
                        "gameplay.unknown",
                        store,
                        model,
                        favorites,
                        recent);
                List<CharacterActionGameplayRequestPickerSection> sections =
                    CharacterActionGameplayRequestPickerCatalog.BuildSections(items, string.Empty);

                AssertSectionContains(sections, "Current / Unknown", "gameplay.unknown");
                AssertSectionContains(sections, "Favorites", "character_test.fireball.projectile");
                AssertSectionContains(sections, "Recent", "character_test.fireball_burst.projectile");
                AssertSectionContains(sections, "Gameplay Requests", "gameplay.custom");
                AssertSectionContains(sections, "Projectiles / 火焰", "character_test.fireball_bezier.projectile");
            });
        }

        [Test]
        public void M7_ProjectilePickerAndMembershipExcludeUncompiledRequestsButPreserveCurrentUnknown()
        {
            WithProjectileAuthoringFixture((options, _) =>
            {
                CharacterActionProjectileAuthoringStore store = CharacterActionProjectileAuthoringStore.Load(options);
                const string sourceOnly = "character_test.poison_bottle.projectile";

                CharacterActionProjectileRequestMembership.Refresh(store);
                Assert.IsTrue(CharacterActionProjectileRequestMembership.Contains("character_test.fireball.projectile"));
                Assert.IsFalse(CharacterActionProjectileRequestMembership.Contains(sourceOnly));

                List<CharacterActionGameplayRequestPickerItem> items =
                    CharacterActionGameplayRequestPickerCatalog.BuildItems(
                        sourceOnly,
                        store,
                        CharacterActionAuthoringModel.CreateDefault(),
                        new HashSet<string>(StringComparer.Ordinal),
                        new HashSet<string>(StringComparer.Ordinal));
                CharacterActionGameplayRequestPickerItem current = items.FirstOrDefault(item => item.RequestId == sourceOnly);
                Assert.NotNull(current);
                Assert.AreEqual(CharacterActionGameplayRequestPickerItemKind.CurrentUnknown, current.Kind);
                Assert.IsFalse(items.Any(item => item.RequestId == sourceOnly
                    && item.Kind == CharacterActionGameplayRequestPickerItemKind.Projectile));
            });
        }

        [Test]
        public void M7_ProjectileConsumerIndexCountsActionAndAbilityTimelineReferences()
        {
            WithProjectileAuthoringFixture((options, _) =>
            {
                CharacterActionProjectileAuthoringStore store = CharacterActionProjectileAuthoringStore.Load(options);
                CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
                model.Actions.Clear();
                model.Actions.Add(new CharacterActionJsonDto
                {
                    ActionId = 999001,
                    GameplayTrack = new GameplayTrackJsonDto
                    {
                        Events = new[]
                        {
                            new GameplayTrackEventJsonDto
                            {
                                Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                                RequestId = CharacterTestBulkIds38.Id790018,
                                StableEventId = CharacterTestBulkIds38.Id790019,
                                Frame = 4,
                            },
                        },
                    },
                });
                JObject abilityRoot = JObject.Parse(@"{
  ""effectTimelines"": [
    {
      ""timelineId"": ""m7.timeline"",
      ""events"": [
        { ""eventKind"": ""SpawnProjectile"", ""requestId"": ""character_test.fireball.projectile"", ""eventId"": ""m7.ability.projectile"", ""frame"": 7 }
      ]
    }
  ]
}");

                CharacterActionProjectileConsumerIndex index = CharacterActionProjectileConsumerIndex.Build(
                    store, model, abilityRoot, "actions.json", "ability.json");
                Assert.IsTrue(index.TryGet("character_test.fireball.projectile", out CharacterActionProjectileConsumerIndex.Entry entry));
                Assert.AreEqual(2, entry.UsageCount);
                Assert.AreEqual(1, entry.Usages.Count(value => value.Kind == CharacterActionProjectileConsumerKind.ActionGameplayRequest));
                Assert.AreEqual(1, entry.Usages.Count(value => value.Kind == CharacterActionProjectileConsumerKind.AbilityEffectTimeline));
                StringAssert.Contains("actions[0]", entry.Usages[0].SourcePath);
                StringAssert.Contains("effectTimelines[0]", entry.Usages[1].SourcePath);
            });
        }

        [Test]
        public void ProjectileInspectorViewModel_FormatsHumanSummaryAndPolicyFields()
        {
            Assert.Pass("Covered by ProjectileInspectorViewModel_FormatsCurrentSummaryAndBallisticFields.");

            WithProjectileAuthoringFixture((options, _) =>
            {
                CharacterActionProjectileAuthoringStore store =
                    CharacterActionProjectileAuthoringStore.Load(options);

                Assert.IsTrue(store.TryGetByStableId(
                    "character_test.fireball.projectile",
                    out CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord fireball));
                CharacterActionProjectileInspectorViewModel fireballView =
                    CharacterActionProjectileInspectorViewModel.Create(fireball);
                Assert.IsFalse(string.IsNullOrWhiteSpace(fireballView.Summary));
                Assert.IsFalse(string.IsNullOrWhiteSpace(fireballView.TypeLabel));
                Assert.IsFalse(string.IsNullOrWhiteSpace(fireballView.SpeedProfileLabel));
                Assert.AreEqual(31.5f, fireballView.RangeMeters, 0.0001f);
                Assert.IsFalse(fireballView.IsAreaPolicy);
                Assert.IsFalse(fireballView.IsPiercing);
                Assert.IsFalse(string.IsNullOrWhiteSpace(fireballView.SyncBadgeText));

                Assert.IsTrue(store.TryGetByStableId(
                    "character_test.fireball_burst.projectile",
                    out CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord burst));
                CharacterActionProjectileInspectorViewModel burstView =
                    CharacterActionProjectileInspectorViewModel.Create(burst);
                Assert.IsFalse(string.IsNullOrWhiteSpace(burstView.TypeLabel));
                Assert.IsTrue(burstView.IsAreaPolicy);

                Assert.IsTrue(store.TryGetByStableId(
                    "character_test.flame_lance.projectile",
                    out CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord lance));
                CharacterActionProjectileInspectorViewModel lanceView =
                    CharacterActionProjectileInspectorViewModel.Create(lance);
                Assert.IsFalse(string.IsNullOrWhiteSpace(lanceView.TypeLabel));
                Assert.IsTrue(lanceView.IsPiercing);
            });
        }

        [Test]
        public void ProjectileInspectorViewModel_FormatsCurrentSummaryAndBallisticFields()
        {
            WithProjectileAuthoringFixture((options, _) =>
            {
                CharacterActionProjectileAuthoringStore store =
                    CharacterActionProjectileAuthoringStore.Load(options);

                Assert.IsTrue(store.TryGetByStableId(
                    "character_test.fireball.projectile",
                    out CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord fireball));
                CharacterActionProjectileInspectorViewModel fireballView =
                    CharacterActionProjectileInspectorViewModel.Create(fireball);
                StringAssert.Contains("Projectile:", fireballView.Summary);
                Assert.AreEqual("Direct hit", fireballView.TypeLabel);
                Assert.AreEqual("Constant", fireballView.SpeedProfileLabel);
                Assert.AreEqual(31.5f, fireballView.RangeMeters, 0.0001f);
                Assert.IsFalse(fireballView.IsAreaPolicy);
                Assert.IsFalse(fireballView.IsPiercing);
                Assert.AreEqual("synced", fireballView.SyncBadgeText);
                StringAssert.Contains("HP 10", fireballView.DamageLabel);
                StringAssert.Contains("8", fireballView.DamageLabel);
                StringAssert.Contains("25", fireballView.DamageLabel);
                StringAssert.Contains(fireballView.DamageLabel, fireballView.Summary);
                Assert.IsFalse(fireballView.Summary.Contains("fire / damage"), fireballView.Summary);

                Assert.IsTrue(store.TryGetByStableId(
                    "character_test.fireball_burst.projectile",
                    out CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord burst));
                CharacterActionProjectileInspectorViewModel burstView =
                    CharacterActionProjectileInspectorViewModel.Create(burst);
                Assert.AreEqual("Area pulse", burstView.TypeLabel);
                Assert.IsTrue(burstView.IsAreaPolicy);

                Assert.IsTrue(store.TryGetByStableId(
                    "character_test.flame_lance.projectile",
                    out CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord lance));
                CharacterActionProjectileInspectorViewModel lanceView =
                    CharacterActionProjectileInspectorViewModel.Create(lance);
                Assert.AreEqual("Piercing x3", lanceView.TypeLabel);
                Assert.IsTrue(lanceView.IsPiercing);

                Assert.IsTrue(store.TryGetByStableId(
                    "character_test.poison_bottle.projectile",
                    out CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord poisonBottle));
                CharacterActionProjectileInspectorViewModel poisonBottleView =
                    CharacterActionProjectileInspectorViewModel.Create(poisonBottle);
                Assert.IsTrue(poisonBottleView.IsBallisticMotion);
                Assert.AreEqual("15m", poisonBottleView.RangeLabel);
                Assert.AreEqual(15f, poisonBottleView.BallisticMaxRangeMeters, 0.01f);
                StringAssert.Contains("max initial speed", poisonBottleView.Summary);
                StringAssert.Contains("ballistic max 15m", poisonBottleView.Summary);
                Assert.IsFalse(poisonBottleView.Summary.Contains("aim max"));
                StringAssert.Contains("auto range-optimal", poisonBottleView.Summary);
                Assert.IsFalse(poisonBottleView.Summary.Contains("24m"));
            });
        }

        [Test]
        public void MechanicTuningAuthoringStore_LoadsArcMineAndAppliesPendingDraft()
        {
            WithMechanicTuningAuthoringFixture((options, paths) =>
            {
                CharacterActionMechanicTuningAuthoringStore store =
                    CharacterActionMechanicTuningAuthoringStore.Load(options);

                Assert.IsTrue(store.TryGetArcMine(
                    out CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringRecord arcMine));
                Assert.IsTrue(arcMine.HasSource);
                Assert.AreEqual("mechanic_tuning.character_test.arc_mine", arcMine.StableId);
                Assert.AreEqual("ability.character_test.arc_mine", arcMine.AbilityStableId);
                Assert.AreEqual("mechanic.character_test.arc_mine.trap", arcMine.SourceStableId);
                Assert.AreEqual(8, arcMine.DeployRangeMeters);
                Assert.AreEqual(5, arcMine.FallbackForwardMeters);
                Assert.AreEqual(60, arcMine.FuseDelayFrames);
                Assert.AreEqual(180, arcMine.DurationFrames);
                Assert.AreEqual(125, arcMine.TriggerRadiusCentimeters);
                Assert.AreEqual(225, arcMine.BlastRadiusCentimeters);
                Assert.AreEqual(8, arcMine.MaxBlastTargets);
                Assert.AreEqual(12041, arcMine.DeployVfxDefinitionId);
                Assert.AreEqual(12043, arcMine.FuseVfxDefinitionId);
                Assert.AreEqual(12042, arcMine.TriggerVfxDefinitionId);
                Assert.AreEqual(paths.SourcePath, arcMine.SourcePath);

                string sourceBefore = File.ReadAllText(paths.SourcePath);
                CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft pending =
                    arcMine.CreateDraft();
                pending.FallbackForwardMeters = 7;
                pending.TriggerRadiusCentimeters = 150;
                var pendingDrafts =
                    new Dictionary<string, CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft>(
                        StringComparer.Ordinal)
                    {
                        [pending.StableId] = pending,
                    };

                CharacterActionMechanicTuningAuthoringStore pendingStore =
                    store.WithDraftOverrides(pendingDrafts);

                Assert.IsTrue(pendingStore.TryGetArcMine(
                    out CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringRecord edited));
                Assert.AreEqual(7, edited.FallbackForwardMeters);
                Assert.AreEqual(150, edited.TriggerRadiusCentimeters);
                Assert.AreEqual(sourceBefore, File.ReadAllText(paths.SourcePath));
            });
        }

        [Test]
        public void MechanicTuningAuthoringStore_LoadsRuneTurretAndAppliesPendingDraft()
        {
            WithMechanicTuningAuthoringFixture((options, paths) =>
            {
                CharacterActionMechanicTuningAuthoringStore store =
                    CharacterActionMechanicTuningAuthoringStore.Load(options);

                Assert.IsTrue(store.TryGetRuneTurret(
                    out CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringRecord runeTurret));
                Assert.IsTrue(runeTurret.HasSource);
                Assert.AreEqual("mechanic_tuning.character_test.rune_turret", runeTurret.StableId);
                Assert.AreEqual("ability.character_test.rune_turret", runeTurret.AbilityStableId);
                Assert.AreEqual("mechanic.character_test.rune_turret.turret", runeTurret.SourceStableId);
                Assert.AreEqual(8, runeTurret.DeployRangeMeters);
                Assert.AreEqual(5, runeTurret.FallbackForwardMeters);
                Assert.AreEqual(18, runeTurret.WarmupFrames);
                Assert.AreEqual(360, runeTurret.DurationFrames);
                Assert.AreEqual(1000, runeTurret.ScanRadiusCentimeters);
                Assert.AreEqual(12, runeTurret.ScanIntervalFrames);
                Assert.AreEqual(45, runeTurret.FireIntervalFrames);
                Assert.AreEqual(8, runeTurret.MaxShots);
                Assert.AreEqual(100, runeTurret.MuzzleHeightCentimeters);
                Assert.AreEqual(12045, runeTurret.DeployVfxDefinitionId);
                Assert.AreEqual(12046, runeTurret.IdleVfxDefinitionId);
                Assert.AreEqual(12047, runeTurret.FireVfxDefinitionId);
                Assert.AreEqual(12050, runeTurret.ExpireVfxDefinitionId);

                string sourceBefore = File.ReadAllText(paths.SourcePath);
                CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft pending =
                    runeTurret.CreateDraft();
                pending.FireIntervalFrames = 30;
                pending.MaxShots = 5;
                var pendingDrafts =
                    new Dictionary<string, CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft>(
                        StringComparer.Ordinal)
                    {
                        [pending.StableId] = pending,
                    };

                CharacterActionMechanicTuningAuthoringStore pendingStore =
                    store.WithDraftOverrides(pendingDrafts);

                Assert.IsTrue(pendingStore.TryGetRuneTurret(
                    out CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringRecord edited));
                Assert.AreEqual(30, edited.FireIntervalFrames);
                Assert.AreEqual(5, edited.MaxShots);
                Assert.AreEqual(sourceBefore, File.ReadAllText(paths.SourcePath));
            });
        }

        [Test]
        public void MechanicTuningAuthoringStore_UpdateWritesSourceGeneratedAndMirrorJson()
        {
            WithMechanicTuningAuthoringFixture((options, paths) =>
            {
                CharacterActionMechanicTuningAuthoringStore store =
                    CharacterActionMechanicTuningAuthoringStore.Load(options);
                Assert.IsTrue(store.TryGetArcMine(
                    out CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringRecord arcMine));
                CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft draft =
                    arcMine.CreateDraft();
                draft.DeployRangeMeters = 9;
                draft.FallbackForwardMeters = 6;
                draft.FuseDelayFrames = 12;
                draft.DurationFrames = 210;
                draft.TriggerRadiusCentimeters = 150;
                draft.BlastRadiusCentimeters = 275;
                draft.MaxBlastTargets = 6;
                draft.DeployVfxDefinitionId = 13001;
                draft.FuseVfxDefinitionId = 13003;
                draft.TriggerVfxDefinitionId = 13002;

                CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringEditResult result =
                    store.UpdateTunings(new[] { draft });

                Assert.IsTrue(result.Success, result.Message);
                JObject sourceRoot = JObject.Parse(File.ReadAllText(paths.SourcePath));
                JObject sourceTuning = FindMechanicTuningObject(
                    (JArray)sourceRoot["characterTestMechanicTunings"],
                    "mechanic_tuning.character_test.arc_mine");
                Assert.AreEqual(9, sourceTuning["deployRangeMeters"].Value<int>());
                Assert.AreEqual(6, sourceTuning["fallbackForwardMeters"].Value<int>());
                Assert.IsNull(sourceTuning["armDelayFrames"]);
                Assert.AreEqual(12, sourceTuning["fuseDelayFrames"].Value<int>());
                Assert.AreEqual(210, sourceTuning["durationFrames"].Value<int>());
                Assert.AreEqual(150, sourceTuning["triggerRadiusCentimeters"].Value<int>());
                Assert.AreEqual(275, sourceTuning["blastRadiusCentimeters"].Value<int>());
                Assert.AreEqual(6, sourceTuning["maxBlastTargets"].Value<int>());
                Assert.AreEqual(13001, sourceTuning["deployVfxDefinitionId"].Value<int>());
                Assert.AreEqual(13003, sourceTuning["fuseVfxDefinitionId"].Value<int>());
                Assert.AreEqual(13002, sourceTuning["triggerVfxDefinitionId"].Value<int>());

                JObject generatedRoot = JObject.Parse(File.ReadAllText(paths.GeneratedMechanicTuningsPath));
                JObject generatedTuning = FindMechanicTuningObject(
                    (JArray)generatedRoot["tunings"],
                    "mechanic_tuning.character_test.arc_mine");
                Assert.AreEqual(210, generatedTuning["durationFrames"].Value<int>());
                Assert.AreEqual(275, generatedTuning["blastRadiusCentimeters"].Value<int>());
                Assert.AreEqual(13003, generatedTuning["fuseVfxDefinitionId"].Value<int>());

                JObject mirrorRoot = JObject.Parse(File.ReadAllText(paths.PlayerMirrorMechanicTuningsPath));
                JObject mirrorTuning = FindMechanicTuningObject(
                    (JArray)mirrorRoot["tunings"],
                    "mechanic_tuning.character_test.arc_mine");
                Assert.AreEqual(210, mirrorTuning["durationFrames"].Value<int>());
                Assert.AreEqual(275, mirrorTuning["blastRadiusCentimeters"].Value<int>());
                Assert.AreEqual(13003, mirrorTuning["fuseVfxDefinitionId"].Value<int>());

                CharacterActionMechanicTuningAuthoringStore reloaded =
                    CharacterActionMechanicTuningAuthoringStore.Load(options);
                Assert.IsTrue(reloaded.TryGetArcMine(
                    out CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringRecord saved));
                Assert.AreEqual(6, saved.FallbackForwardMeters);
                Assert.AreEqual(210, saved.DurationFrames);
                Assert.AreEqual(275, saved.BlastRadiusCentimeters);
                Assert.AreEqual(13003, saved.FuseVfxDefinitionId);
            });
        }

        [Test]
        public void VfxDefinitionPickerCatalog_LoadsReadableArcMineChoices()
        {
            string root = Path.Combine(
                "Temp",
                "CharacterActionVfxDefinitionPickerTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string generatedPath = Path.Combine(root, "vfx_definitions.json");
            File.WriteAllText(generatedPath, ArcMineVfxDefinitionsJson());

            try
            {
                CharacterActionVfxDefinitionCatalog catalog =
                    CharacterActionVfxDefinitionCatalog.Load(generatedPath, string.Empty);

                Assert.IsTrue(catalog.HasDefinitions);
                Assert.IsTrue(catalog.TryGet(12043, out CharacterActionVfxDefinitionChoice fuse));
                Assert.IsFalse(string.IsNullOrWhiteSpace(fuse.DisplayName));
                StringAssert.Contains("12043", fuse.Label);
                StringAssert.Contains("vfx.magic.arc_mine.fuse", fuse.Label);
                StringAssert.Contains("stable id: vfx.magic.arc_mine.fuse", fuse.Detail);
                StringAssert.Contains("60f", fuse.Detail);
                StringAssert.Contains("vfx.magic.arc_mine.fuse.v1", fuse.Detail);

                List<CharacterActionVfxDefinitionChoice> filtered =
                    catalog.BuildChoices(12043, allowNone: true, query: "arc");
                Assert.AreEqual(1, CountDefinitionId(filtered, 12043));

                List<CharacterActionVfxDefinitionChoice> noneChoices =
                    catalog.BuildChoices(0, allowNone: true, query: string.Empty);
                Assert.AreEqual(0, noneChoices[0].DefinitionId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(noneChoices[0].Label));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void VfxDefinitionPickerCatalog_FallsBackToAuthoringAndPreservesUnknownId()
        {
            string root = Path.Combine(
                "Temp",
                "CharacterActionVfxDefinitionPickerTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string abilityAuthoringPath = Path.Combine(root, "ability_authoring.json");
            File.WriteAllText(abilityAuthoringPath, ArcMineVfxAuthoringJson());

            try
            {
                CharacterActionVfxDefinitionCatalog catalog =
                    CharacterActionVfxDefinitionCatalog.Load(
                        Path.Combine(root, "missing_vfx_definitions.json"),
                        abilityAuthoringPath);

                Assert.IsTrue(catalog.HasDefinitions);
                Assert.IsTrue(catalog.TryGet(12041, out CharacterActionVfxDefinitionChoice deploy));
                Assert.IsFalse(string.IsNullOrWhiteSpace(deploy.DisplayName));

                List<CharacterActionVfxDefinitionChoice> unknown =
                    catalog.BuildChoices(99999, allowNone: false, query: string.Empty);
                Assert.AreEqual(99999, unknown[0].DefinitionId);
                Assert.IsTrue(unknown[0].IsUnknown);
                Assert.AreEqual("未知 VFX #99999", unknown[0].Label);
                Assert.IsFalse(string.IsNullOrWhiteSpace(unknown[0].Detail));

                List<CharacterActionVfxDefinitionChoice> byId =
                    catalog.BuildChoices(12043, allowNone: true, query: "12043");
                Assert.AreEqual(1, CountDefinitionId(byId, 12043));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void VfxDefinitionPickerUi_DisplaysReadableLabelAndNoneChoice()
        {
            string root = Path.Combine(
                "Temp",
                "CharacterActionVfxDefinitionPickerTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string generatedPath = Path.Combine(root, "vfx_definitions.json");
            File.WriteAllText(generatedPath, ArcMineVfxDefinitionsJson());

            try
            {
                CharacterActionVfxDefinitionCatalog catalog =
                    CharacterActionVfxDefinitionCatalog.Load(generatedPath, string.Empty);
                var fieldRoot = new VisualElement();
                int selected = -1;

                CharacterActionVfxDefinitionPickerUi.AddField(
                    fieldRoot,
                    "VFX",
                    12043,
                    allowNone: true,
                    catalog,
                    value => selected = value,
                    "婵炴潙顑堥惁?tooltip");

                PopupField<int> popup = fieldRoot.Q<PopupField<int>>();
                Assert.IsNotNull(popup);
                Assert.AreEqual(12043, popup.value);
                string selectedLabel = popup.formatSelectedValueCallback(popup.value);
                StringAssert.Contains("12043", selectedLabel);
                StringAssert.Contains("vfx.magic.arc_mine.fuse", selectedLabel);
                Assert.AreEqual(-1, selected);

                var noneRoot = new VisualElement();
                CharacterActionVfxDefinitionPickerUi.AddField(
                    noneRoot,
                    "VFX",
                    0,
                    allowNone: true,
                    catalog,
                    value => selected = value,
                    "婵炴潙顑堥惁?tooltip");
                PopupField<int> nonePopup = noneRoot.Q<PopupField<int>>();
                Assert.IsNotNull(nonePopup);
                Assert.AreEqual(0, nonePopup.value);
                Assert.IsFalse(string.IsNullOrWhiteSpace(nonePopup.formatSelectedValueCallback(nonePopup.value)));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void MechanicTuningAuthoringStore_InvalidValueAndMissingSourceDoNotWrite()
        {
            WithMechanicTuningAuthoringFixture((options, paths) =>
            {
                CharacterActionMechanicTuningAuthoringStore store =
                    CharacterActionMechanicTuningAuthoringStore.Load(options);
                Assert.IsTrue(store.TryGetArcMine(
                    out CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringRecord arcMine));
                CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft invalid =
                    arcMine.CreateDraft();
                invalid.DurationFrames = 0;
                string sourceBefore = File.ReadAllText(paths.SourcePath);
                string generatedBefore = File.ReadAllText(paths.GeneratedMechanicTuningsPath);
                string mirrorBefore = File.ReadAllText(paths.PlayerMirrorMechanicTuningsPath);

                CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringEditResult result =
                    store.UpdateTunings(new[] { invalid });

                Assert.IsFalse(result.Success);
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.Message));
                Assert.AreEqual(sourceBefore, File.ReadAllText(paths.SourcePath));
                Assert.AreEqual(generatedBefore, File.ReadAllText(paths.GeneratedMechanicTuningsPath));
                Assert.AreEqual(mirrorBefore, File.ReadAllText(paths.PlayerMirrorMechanicTuningsPath));

                JObject sourceRoot = JObject.Parse(sourceBefore);
                JArray tunings = (JArray)sourceRoot["characterTestMechanicTunings"];
                for (int i = tunings.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(
                        tunings[i]?["stableId"]?.Value<string>(),
                        "mechanic_tuning.character_test.arc_mine",
                        StringComparison.Ordinal))
                    {
                        tunings.RemoveAt(i);
                    }
                }

                File.WriteAllText(paths.SourcePath, sourceRoot.ToString(Formatting.Indented) + Environment.NewLine);
                CharacterActionMechanicTuningAuthoringStore missingStore =
                    CharacterActionMechanicTuningAuthoringStore.Load(options);
                Assert.IsFalse(missingStore.TryGetArcMine(out _));
                Assert.IsTrue(missingStore.Diagnostics.Count > 0);
                CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringEditResult missingResult =
                    missingStore.UpdateTunings(new[] { arcMine.CreateDraft() });
                Assert.IsFalse(missingResult.Success);
                Assert.IsFalse(string.IsNullOrWhiteSpace(missingResult.Message));
            });
        }

        [Test]
        public void CharacterActionHistorySnapshot_PreservesPendingMechanicTuningDrafts()
        {
            WithMechanicTuningAuthoringFixture((options, _) =>
            {
                CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
                var pending =
                    new Dictionary<string, CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft>(
                        StringComparer.Ordinal)
                    {
                        ["mechanic_tuning.character_test.arc_mine"] =
                            new CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft
                            {
                                StableId = "mechanic_tuning.character_test.arc_mine",
                                AbilityStableId = "ability.character_test.arc_mine",
                                SourceStableId = "mechanic.character_test.arc_mine.trap",
                                DeployRangeMeters = 8,
                                FallbackForwardMeters = 7,
                                FuseDelayFrames = 60,
                                DurationFrames = 210,
                                TriggerRadiusCentimeters = 150,
                                BlastRadiusCentimeters = 225,
                                MaxBlastTargets = 8,
                                DeployVfxDefinitionId = 12041,
                                FuseVfxDefinitionId = 12043,
                                TriggerVfxDefinitionId = 12042,
                                HasSource = true,
                            },
                    };

                CharacterActionAuthoringHistorySnapshot snapshot =
                    CharacterActionAuthoringHistorySnapshot.Capture(
                        model,
                        mechanicTuningOptions: options,
                        pendingMechanicTuningDrafts: pending);
                pending["mechanic_tuning.character_test.arc_mine"].DurationFrames = 999;

                CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft[] restored =
                    snapshot.CreatePendingMechanicTuningDraftClones();
                Assert.AreEqual(1, restored.Length);
                Assert.AreEqual("mechanic_tuning.character_test.arc_mine", restored[0].StableId);
                Assert.AreEqual(7, restored[0].FallbackForwardMeters);
                Assert.AreEqual(210, restored[0].DurationFrames);
                Assert.AreEqual(150, restored[0].TriggerRadiusCentimeters);
                Assert.AreEqual(12043, restored[0].FuseVfxDefinitionId);
            });
        }
        private static bool ContainsDiscoveryDiagnostic(
            CharacterActionReferenceDiscoveryResult result,
            string code)
        {
            CharacterActionReferenceDiscoveryDiagnostic[] diagnostics = result.Diagnostics;
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Code == code)
                    return true;
            }

            return false;
        }

        private static CharacterActionAuthoringWindow FindOpenAuthoringWindow()
        {
            CharacterActionAuthoringWindow[] windows = UnityEngine.Resources.FindObjectsOfTypeAll<CharacterActionAuthoringWindow>();
            return windows.Length == 0 ? null : windows[windows.Length - 1];
        }

        private static CharacterActionAuthoringOpenRequest CreateCodexAbilityOpenRequest(
            string requestId,
            string actionSetStableId,
            string actionLibrarySourceDirectory)
        {
            return new CharacterActionAuthoringOpenRequest
            {
                RequestId = requestId,
                Package = "Tools/MxFramework.Authoring/samples/character-codex-flow-test",
                AbilityId = 900001,
                ProfileSourceDirectory =
                    "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test",
                ActionSetSourceDirectory =
                    "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test",
                ActionLibrarySourceDirectory = actionLibrarySourceDirectory,
                ActionSetStableId = actionSetStableId,
                Mode = "focusOnly",
            };
        }

        private static CharacterActionAuthoringOpenRequest CreateCombatGirlActionOpenRequest(
            string requestId,
            string actionLibrarySourceDirectory,
            string actionLibraryId,
            int actionId,
            string mode)
        {
            string sourcePath = Path.Combine(
                actionLibrarySourceDirectory,
                CharacterActionJsonImporter.DefaultActionsSourcePath);
            return new CharacterActionAuthoringOpenRequest
            {
                RequestId = requestId,
                Package = "Authoring/CharacterPackages/character-combat-girl-sword",
                ProfileSourceDirectory =
                    "Assets/MxFrameworkGenerated/CharacterActions/Profiles/combat_girl_sword",
                ActionSetSourceDirectory =
                    "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/combat_girl_sword",
                ActionLibrarySourceDirectory = actionLibrarySourceDirectory,
                ProfileId = "combat_girl_sword.default",
                ActionSetStableId = "combat_girl_sword.sword_empty",
                ActionLibraryId = actionLibraryId,
                ActionId = actionId,
                SourceHash = ComputeSha256(sourcePath),
                Mode = mode,
            };
        }

        private static int CountActions(string path)
        {
            JObject root = JObject.Parse(File.ReadAllText(path));
            return (root["actions"] as JArray)?.Count ?? 0;
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                return "sha256:" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, "Missing private field: " + fieldName);
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, "Missing private field: " + fieldName);
            field.SetValue(target, value);
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, "Missing private method: " + methodName);
            return method.Invoke(target, arguments);
        }

        private static void CopyJsonDirectory(string sourceDirectory, string destinationDirectory)
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

        private static void DestroyOpenAuthoringWindows()
        {
            CharacterActionAuthoringWindow[] windows = UnityEngine.Resources.FindObjectsOfTypeAll<CharacterActionAuthoringWindow>();
            for (int i = 0; i < windows.Length; i++)
                UnityEngine.Object.DestroyImmediate(windows[i]);
        }

        private static void ResetOpenRequestSessionState()
        {
            const string prefix = "MxFramework.CharacterActionAuthoringWindow.";
            SessionState.SetString(prefix + "Model", string.Empty);
            SessionState.SetString(prefix + "ResourceDrafts", string.Empty);
            SessionState.SetString(prefix + "DocumentDrafts", string.Empty);
            SessionState.SetString(prefix + "ConflictedDocumentDrafts", string.Empty);
            SessionState.SetString(prefix + "DocumentDraftConflictPaths", string.Empty);
            SessionState.SetBool(prefix + "ProfileDirty", false);
            SessionState.SetBool(prefix + "ActionSetDirty", false);
            SessionState.SetBool(prefix + "ActionLibraryDirty", false);
        }

        private static RootMotionFrameJsonDto FindRootMotionFrame(RootMotionFrameJsonDto[] frames, int frame)
        {
            for (int i = 0; frames != null && i < frames.Length; i++)
            {
                if (frames[i]?.Frame == frame)
                    return frames[i];
            }

            return null;
        }

        private static void WithProjectileAuthoringFixture(
            Action<CharacterActionProjectileAuthoringStoreOptions, ProjectileAuthoringFixturePaths> test)
        {
            string root = Path.Combine(
                "Temp",
                "CharacterActionProjectileAuthoringStoreTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var paths = new ProjectileAuthoringFixturePaths(
                Path.Combine(root, "ability_authoring.json"),
                Path.Combine(root, "projectiles"),
                Path.Combine(root, "projectile_definitions.json"),
                Path.Combine(root, "projectile_spawn_profiles.json"),
                Path.Combine(root, "projectile_presentation_bindings.json"),
                Path.Combine(root, "character_packages"),
                Path.Combine(root, "generated_character_packages"));
            JObject sourceRoot = JObject.Parse(ProjectileAuthoringSourceJson());
            WriteProjectileRequestSourceFixture(sourceRoot, paths.ProjectileSourceDirectoryPath);
            sourceRoot.Remove("projectileDefinitions");
            sourceRoot.Remove("projectileSpawnProfiles");
            sourceRoot.Remove("projectilePresentationBindings");
            File.WriteAllText(paths.SourcePath, sourceRoot.ToString(Formatting.Indented) + Environment.NewLine);
            File.WriteAllText(paths.GeneratedDefinitionsPath, ProjectileGeneratedDefinitionsJson());
            File.WriteAllText(paths.GeneratedSpawnProfilesPath, ProjectileGeneratedSpawnProfilesJson());
            File.WriteAllText(paths.GeneratedBindingsPath, ProjectileGeneratedBindingsJson());
            WriteProjectileCharacterPackageFixture(paths.CharacterPackageSourceRoot);

            try
            {
                var options = new CharacterActionProjectileAuthoringStoreOptions(
                    paths.SourcePath,
                    paths.GeneratedDefinitionsPath,
                    paths.GeneratedSpawnProfilesPath,
                    paths.GeneratedBindingsPath,
                    projectileSourceDirectoryPath: paths.ProjectileSourceDirectoryPath,
                    refreshAssetDatabase: false);
                test(options, paths);
            }
            finally
            {
                if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            }
        }

        private static void WithMechanicTuningAuthoringFixture(
            Action<CharacterActionMechanicTuningAuthoringStoreOptions, MechanicTuningAuthoringFixturePaths> test)
        {
            string root = Path.Combine(
                "Temp",
                "CharacterActionMechanicTuningAuthoringStoreTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var paths = new MechanicTuningAuthoringFixturePaths(
                Path.Combine(root, "ability_authoring.json"),
                Path.Combine(root, "character_test_mechanic_tunings.json"),
                Path.Combine(root, "RuntimeCatalogs", "Abilities", "character_test_mechanic_tunings.json"));
            JObject sourceRoot = JObject.Parse(MechanicTuningAuthoringSourceJson());
            JObject generatedRoot = new JObject
            {
                ["schemaVersion"] = "1",
                ["packageId"] = sourceRoot["packageId"]?.Value<string>() ?? string.Empty,
                ["tunings"] = (JArray)((JArray)sourceRoot["characterTestMechanicTunings"]).DeepClone(),
            };
            File.WriteAllText(paths.SourcePath, sourceRoot.ToString(Formatting.Indented) + Environment.NewLine);
            File.WriteAllText(paths.GeneratedMechanicTuningsPath, generatedRoot.ToString(Formatting.Indented) + Environment.NewLine);
            Directory.CreateDirectory(Path.GetDirectoryName(paths.PlayerMirrorMechanicTuningsPath));
            File.WriteAllText(paths.PlayerMirrorMechanicTuningsPath, generatedRoot.ToString(Formatting.Indented) + Environment.NewLine);

            try
            {
                var options = new CharacterActionMechanicTuningAuthoringStoreOptions(
                    paths.SourcePath,
                    paths.GeneratedMechanicTuningsPath,
                    paths.PlayerMirrorMechanicTuningsPath,
                    refreshAssetDatabase: false);
                test(options, paths);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        private static void WriteProjectileRequestSourceFixture(JObject sourceRoot, string directory)
        {
            Directory.CreateDirectory(directory);
            JArray definitions = sourceRoot?["projectileDefinitions"] as JArray;
            JArray spawnProfiles = sourceRoot?["projectileSpawnProfiles"] as JArray;
            JArray bindings = sourceRoot?["projectilePresentationBindings"] as JArray;
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                if (!(definitions[i] is JObject definition))
                    continue;
                string stableId = definition["stableId"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(stableId))
                    continue;
                JObject spawnProfile = FindSpawnProfileObject(spawnProfiles, stableId);
                JObject binding = FindBindingObject(bindings, definition["definitionId"]?.Value<int>() ?? 0);
                var root = new JObject
                {
                    ["schemaVersion"] = "1",
                    ["stableId"] = stableId,
                    ["definition"] = (JObject)definition.DeepClone(),
                };
                if (spawnProfile != null)
                    root["spawnProfile"] = (JObject)spawnProfile.DeepClone();
                if (binding != null)
                    root["presentationBinding"] = (JObject)binding.DeepClone();
                File.WriteAllText(
                    Path.Combine(directory, stableId + ".json"),
                    root.ToString(Formatting.Indented) + Environment.NewLine);
            }
        }

        private static JObject ReadProjectileRequestSource(
            ProjectileAuthoringFixturePaths paths,
            string stableId)
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(
                paths.ProjectileSourceDirectoryPath,
                stableId + ".json")));
        }

        private static JObject FindMechanicTuningObject(JArray tunings, string stableId)
        {
            for (int i = 0; tunings != null && i < tunings.Count; i++)
            {
                if (tunings[i] is JObject tuning
                    && string.Equals(tuning["stableId"]?.Value<string>(), stableId, StringComparison.Ordinal))
                {
                    return tuning;
                }
            }

            Assert.Fail("Missing mechanic tuning: " + stableId);
            return null;
        }

        private static bool ContainsText(IReadOnlyList<string> values, string text)
        {
            for (int i = 0; values != null && i < values.Count; i++)
            {
                if (!string.IsNullOrEmpty(values[i])
                    && values[i].IndexOf(text, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void WriteProjectileCharacterPackageFixture(string root)
        {
            string packageRoot = Path.Combine(root, "character-codex-flow-test");
            string configRoot = Path.Combine(packageRoot, "config");
            string geometryRoot = Path.Combine(packageRoot, "geometry");
            Directory.CreateDirectory(configRoot);
            Directory.CreateDirectory(geometryRoot);
            File.WriteAllText(
                Path.Combine(configRoot, "character_application.json"),
                @"{ ""schemaVersion"": ""1.0"", ""characterStableId"": ""char.codex_flow_test"" }");
            File.WriteAllText(
                Path.Combine(geometryRoot, "sockets.json"),
                @"{
  ""schemaVersion"": ""1.0"",
  ""sockets"": [
    {
      ""socketId"": ""mainHand"",
      ""usage"": ""Weapon"",
      ""handedness"": ""Right"",
      ""sideTag"": ""Right"",
      ""bonePath"": ""Armature/Hips/Spine/RightHand"",
      ""locatorPath"": ""locators/right_hand_weapon""
    },
    {
      ""socketId"": ""offHand"",
      ""usage"": ""Weapon"",
      ""handedness"": ""Left"",
      ""sideTag"": ""Left"",
      ""bonePath"": ""Armature/Hips/Spine/LeftHand"",
      ""locatorPath"": ""locators/left_hand_weapon""
    },
    {
      ""socketId"": ""back"",
      ""usage"": ""Attachment"",
      ""sideTag"": ""Back"",
      ""bonePath"": ""Armature/Hips/Spine"",
      ""locatorPath"": ""locators/back_socket""
    },
    {
      ""socketId"": ""headVfx"",
      ""usage"": ""VFX"",
      ""sideTag"": ""Center"",
      ""bonePath"": ""Armature/Hips/Spine/Head"",
      ""locatorPath"": ""locators/head_vfx""
    }
  ]
}");
        }

        private static void AssertChoiceExists(CharacterActionPreviewBindingChoice[] choices, string value)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(FindChoiceLabel(choices, value)), "Missing choice: " + value);
        }

        private static void AssertSectionContains(
            IReadOnlyList<CharacterActionGameplayRequestPickerSection> sections,
            string sectionTitle,
            string requestId)
        {
            for (int i = 0; sections != null && i < sections.Count; i++)
            {
                CharacterActionGameplayRequestPickerSection section = sections[i];
                if (!string.Equals(section?.Title, sectionTitle, StringComparison.Ordinal))
                    continue;
                for (int j = 0; j < section.Items.Count; j++)
                {
                    if (string.Equals(section.Items[j]?.RequestId, requestId, StringComparison.Ordinal))
                        return;
                }
            }

            Assert.Fail("Missing request '" + requestId + "' in section '" + sectionTitle + "'.");
        }

        private static string FindChoiceLabel(CharacterActionPreviewBindingChoice[] choices, string value)
        {
            for (int i = 0; choices != null && i < choices.Length; i++)
            {
                if (choices[i].Value == value)
                    return choices[i].Label;
            }

            return string.Empty;
        }

        private static string FindDamageChoiceLabel(List<(string Value, string Label)> choices, string value)
        {
            for (int i = 0; choices != null && i < choices.Count; i++)
            {
                if (string.Equals(choices[i].Value, value, StringComparison.Ordinal))
                    return choices[i].Label;
            }

            return string.Empty;
        }

        private static JObject FindDefinitionObject(JArray definitions, string stableId)
        {
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                if (definitions[i] is JObject fields
                    && fields["stableId"]?.Value<string>() == stableId)
                {
                    return fields;
                }
            }

            Assert.Fail("Missing projectile definition: " + stableId);
            return null;
        }

        private static JObject FindSpawnProfileObject(JArray profiles, string stableId)
        {
            for (int i = 0; profiles != null && i < profiles.Count; i++)
            {
                if (profiles[i] is JObject fields
                    && fields["stableId"]?.Value<string>() == stableId)
                {
                    return fields;
                }
            }

            Assert.Fail("Missing projectile spawn profile: " + stableId);
            return null;
        }

        private static void AssertJsonArrayContains(JArray values, string expected)
        {
            for (int i = 0; values != null && i < values.Count; i++)
            {
                if (string.Equals(values[i]?.Value<string>(), expected, StringComparison.Ordinal))
                    return;
            }

            Assert.Fail("Missing JSON array value: " + expected);
        }

        private static int CountDefinitionId(
            IReadOnlyList<CharacterActionVfxDefinitionChoice> choices,
            int definitionId)
        {
            int count = 0;
            for (int i = 0; choices != null && i < choices.Count; i++)
            {
                if (choices[i] != null && choices[i].DefinitionId == definitionId)
                    count++;
            }

            return count;
        }

        private static JObject FindBindingObject(JArray bindings, int definitionId)
        {
            for (int i = 0; bindings != null && i < bindings.Count; i++)
            {
                if (bindings[i] is JObject fields
                    && fields["projectileDefinitionId"]?.Value<int>() == definitionId)
                {
                    return fields;
                }
            }

            Assert.Fail("Missing projectile binding: " + definitionId);
            return null;
        }

        private static string ArcMineVfxDefinitionsJson()
        {
            return @"{
  ""schemaVersion"": ""1"",
  ""packageId"": ""character_test.fireball"",
  ""definitions"": [
    {
      ""definitionId"": 12041,
      ""stableId"": ""vfx.magic.arc_mine.deploy"",
      ""prefabResource"": {
        ""id"": ""vfx.magic.arc_mine.deploy"",
        ""typeId"": ""GameObject"",
        ""variant"": """",
        ""packageId"": ""mxframework.samples""
      },
      ""defaultLifetimeFrames"": 30,
      ""definitionHash"": ""vfx.magic.arc_mine.deploy.v2""
    },
    {
      ""definitionId"": 12043,
      ""stableId"": ""vfx.magic.arc_mine.fuse"",
      ""prefabResource"": {
        ""id"": ""vfx.magic.arc_mine.fuse"",
        ""typeId"": ""GameObject"",
        ""variant"": """",
        ""packageId"": ""mxframework.samples""
      },
      ""defaultLifetimeFrames"": 60,
      ""definitionHash"": ""vfx.magic.arc_mine.fuse.v1""
    },
    {
      ""definitionId"": 12042,
      ""stableId"": ""vfx.magic.arc_mine.trigger"",
      ""prefabResource"": {
        ""id"": ""vfx.magic.arc_mine.trigger"",
        ""typeId"": ""GameObject"",
        ""variant"": """",
        ""packageId"": ""mxframework.samples""
      },
      ""defaultLifetimeFrames"": 24,
      ""definitionHash"": ""vfx.magic.arc_mine.trigger.v2""
    }
  ]
}";
        }

        private static string ArcMineVfxAuthoringJson()
        {
            return @"{
  ""schemaVersion"": ""1"",
  ""packageId"": ""character_test.fireball"",
  ""vfxDefinitions"": [
    {
      ""definitionId"": 12041,
      ""stableId"": ""vfx.magic.arc_mine.deploy"",
      ""prefabResource"": {
        ""id"": ""vfx.magic.arc_mine.deploy"",
        ""typeId"": ""GameObject"",
        ""variant"": """",
        ""packageId"": ""mxframework.samples""
      },
      ""defaultLifetimeFrames"": 30,
      ""definitionHash"": ""vfx.magic.arc_mine.deploy.v2""
    },
    {
      ""definitionId"": 12043,
      ""stableId"": ""vfx.magic.arc_mine.fuse"",
      ""prefabResource"": {
        ""id"": ""vfx.magic.arc_mine.fuse"",
        ""typeId"": ""GameObject"",
        ""variant"": """",
        ""packageId"": ""mxframework.samples""
      },
      ""defaultLifetimeFrames"": 60,
      ""definitionHash"": ""vfx.magic.arc_mine.fuse.v1""
    }
  ]
}";
        }

        private static string MechanicTuningAuthoringSourceJson()
        {
            return @"{
  ""schemaVersion"": ""1"",
  ""packageId"": ""character_test.fireball"",
  ""characterTestMechanicTunings"": [
    {
      ""stableId"": ""mechanic_tuning.character_test.arc_mine"",
      ""abilityStableId"": ""ability.character_test.arc_mine"",
      ""sourceStableId"": ""mechanic.character_test.arc_mine.trap"",
      ""deployRangeMeters"": 8,
      ""fallbackForwardMeters"": 5,
      ""fuseDelayFrames"": 60,
      ""durationFrames"": 180,
      ""triggerRadiusCentimeters"": 125,
      ""blastRadiusCentimeters"": 225,
      ""maxBlastTargets"": 8,
      ""deployVfxDefinitionId"": 12041,
      ""fuseVfxDefinitionId"": 12043,
      ""triggerVfxDefinitionId"": 12042
    },
    {
      ""stableId"": ""mechanic_tuning.character_test.rune_turret"",
      ""abilityStableId"": ""ability.character_test.rune_turret"",
      ""sourceStableId"": ""mechanic.character_test.rune_turret.turret"",
      ""deployRangeMeters"": 8,
      ""fallbackForwardMeters"": 5,
      ""warmupFrames"": 18,
      ""durationFrames"": 360,
      ""scanRadiusCentimeters"": 1000,
      ""scanIntervalFrames"": 12,
      ""fireIntervalFrames"": 45,
      ""maxShots"": 8,
      ""muzzleHeightCentimeters"": 100,
      ""deployVfxDefinitionId"": 12045,
      ""idleVfxDefinitionId"": 12046,
      ""fireVfxDefinitionId"": 12047,
      ""expireVfxDefinitionId"": 12050
    }
  ]
}";
        }

        private static string ProjectileAuthoringSourceJson()
        {
            return @"{
  ""schemaVersion"": ""1"",
  ""knownSocketIds"": [ ""mainHand"" ],
  ""projectileDefinitions"": [
    {
      ""definitionId"": 91001,
      ""stableId"": ""character_test.fireball.projectile"",
      ""motion"": { ""speedPerFrame"": 0.35, ""lifetimeFrames"": 90 },
      ""collision"": {
        ""radius"": 0.25,
        ""stopOnFirstAcceptedImpact"": true,
        ""stopOnBlockedImpact"": true
      },
      ""impact"": {
        ""hitEvent"": {
          ""targetTypes"": [ ""HostileCharacter"" ],
          ""stableEventId"": ""character_test.fireball.projectile.impact"",
          ""damage"": {
            ""damageTypeId"": ""fire"",
            ""baseDamage"": 10,
            ""impactPower"": 8,
            ""launchPower"": 25,
            ""tags"": [ ""Projectile"" ],
            ""analysisCategory"": ""FireballImpact""
          }
        }
      }
    },
    {
      ""definitionId"": 91002,
      ""stableId"": ""character_test.fireball_burst.projectile"",
      ""motion"": { ""speedPerFrame"": 0.35, ""lifetimeFrames"": 90 },
      ""collision"": {
        ""radius"": 0.25,
        ""stopOnFirstAcceptedImpact"": true,
        ""stopOnBlockedImpact"": true
      },
      ""impact"": {
        ""hitEvent"": {
          ""targetTypes"": [ ""HostileCharacter"" ],
          ""stableEventId"": ""character_test.fireball_burst.projectile.impact"",
          ""damage"": {
            ""damageTypeId"": ""fire"",
            ""baseDamage"": 8,
            ""impactPower"": 6,
            ""tags"": [ ""Projectile"", ""Area"" ],
            ""analysisCategory"": ""FireballBurstImpact""
          }
        },
        ""policyKind"": ""AreaPulseOnImpact"",
        ""areaRadius"": 2,
        ""triggerOnExpire"": true,
        ""areaHitEvent"": {
          ""targetTypes"": [ ""HostileCharacter"" ],
          ""stableEventId"": ""character_test.fireball_burst.projectile.impact"",
          ""maxAcceptedHits"": 1,
          ""hitSameTargetPolicy"": ""AllowRepeat""
        }
      }
    },
    {
      ""definitionId"": 91003,
      ""stableId"": ""character_test.flame_lance.projectile"",
      ""motion"": { ""speedPerFrame"": 0.55, ""lifetimeFrames"": 80 },
      ""collision"": {
        ""radius"": 0.15,
        ""stopOnFirstAcceptedImpact"": false,
        ""stopOnBlockedImpact"": true,
        ""maxAcceptedImpacts"": 3,
        ""hitSameTargetPolicy"": ""OncePerProjectile""
      },
      ""impact"": {
        ""hitEvent"": {
          ""targetTypes"": [ ""HostileCharacter"" ],
          ""stableEventId"": ""character_test.flame_lance.projectile.impact"",
          ""maxAcceptedHits"": 3,
          ""hitSameTargetPolicy"": ""OncePerEvent"",
          ""damage"": {
            ""damageTypeId"": ""fire"",
            ""baseDamage"": 7,
            ""impactPower"": 5,
            ""tags"": [ ""Projectile"", ""Pierce"" ],
            ""analysisCategory"": ""FlameLanceImpact""
          }
        }
      }
    },
    {
      ""definitionId"": 91009,
      ""stableId"": ""character_test.fireball_bezier.projectile"",
      ""motion"": {
        ""kind"": ""BezierToTarget"",
        ""speedPerFrame"": 0.35,
        ""lifetimeFrames"": 90,
        ""bezierControlOffsetA"": { ""x"": 0, ""y"": 3, ""z"": 6 },
        ""bezierControlOffsetB"": { ""x"": 0, ""y"": 2, ""z"": -4 }
      },
      ""collision"": {
        ""radius"": 0.25,
        ""stopOnFirstAcceptedImpact"": true,
        ""stopOnBlockedImpact"": true
      },
      ""impact"": {
        ""hitEvent"": {
          ""targetTypes"": [ ""HostileCharacter"" ],
          ""stableEventId"": ""character_test.fireball_bezier.projectile.impact"",
          ""damage"": {
            ""damageTypeId"": ""fire"",
            ""baseDamage"": 10,
            ""impactPower"": 8,
            ""tags"": [ ""Projectile"", ""Bezier"" ],
            ""analysisCategory"": ""FireballBezierImpact""
          }
        }
      }
    },
    {
      ""definitionId"": 91015,
      ""stableId"": ""character_test.poison_bottle.projectile"",
      ""motion"": {
        ""kind"": ""BallisticArcToTarget"",
        ""speedPerFrame"": 0.3535533906,
        ""lifetimeFrames"": 90,
        ""gravityPerFrameSquared"": 0.008333333,
        ""maxLaunchAngleDegrees"": 0,
        ""maxLaunchAngleTangent"": 0
      },
      ""collision"": {
        ""radius"": 0.2,
        ""stopOnFirstAcceptedImpact"": true,
        ""stopOnBlockedImpact"": true,
        ""hitSameTargetPolicy"": ""OncePerProjectile""
      },
      ""impact"": {
        ""hitEvent"": {
          ""targetTypes"": [ ""HostileCharacter"" ],
          ""stableEventId"": ""character_test.poison_bottle.projectile.shatter"",
          ""damage"": {
            ""damageTypeId"": ""toxic"",
            ""baseDamage"": 0,
            ""impactPower"": 0,
            ""tags"": [ ""Projectile"", ""Poison"" ],
            ""analysisCategory"": ""PoisonBottleShatter""
          }
        }
      }
    }
  ],
  ""projectileSpawnProfiles"": [
    {
      ""stableId"": ""character_test.fireball.projectile"",
      ""projectileStableId"": ""character_test.fireball.projectile"",
      ""origin"": {
        ""kind"": ""Socket"",
        ""socketId"": ""mainHand"",
        ""localOffset"": { ""x"": 0, ""y"": 0, ""z"": 0 },
        ""fallbackRootOffset"": { ""x"": 0, ""y"": 1.15, ""z"": 0.65 }
      },
      ""aim"": { ""kind"": ""CasterForwardRange"" }
    },
    {
      ""stableId"": ""character_test.fireball_burst.projectile"",
      ""projectileStableId"": ""character_test.fireball_burst.projectile"",
      ""origin"": {
        ""kind"": ""Socket"",
        ""socketId"": ""mainHand"",
        ""localOffset"": { ""x"": 0, ""y"": 0, ""z"": 0 },
        ""fallbackRootOffset"": { ""x"": 0, ""y"": 1.15, ""z"": 0.65 }
      },
      ""aim"": { ""kind"": ""CasterForwardRange"" }
    },
    {
      ""stableId"": ""character_test.flame_lance.projectile"",
      ""projectileStableId"": ""character_test.flame_lance.projectile"",
      ""origin"": {
        ""kind"": ""Socket"",
        ""socketId"": ""mainHand"",
        ""localOffset"": { ""x"": 0, ""y"": 0, ""z"": 0 },
        ""fallbackRootOffset"": { ""x"": 0, ""y"": 1.15, ""z"": 0.65 }
      },
      ""aim"": { ""kind"": ""CasterForwardRange"" }
    },
    {
      ""stableId"": ""character_test.fireball_bezier.projectile"",
      ""projectileStableId"": ""character_test.fireball_bezier.projectile"",
      ""origin"": {
        ""kind"": ""Socket"",
        ""socketId"": ""mainHand"",
        ""localOffset"": { ""x"": 0, ""y"": 0, ""z"": 0 },
        ""fallbackRootOffset"": { ""x"": 0, ""y"": 1.15, ""z"": 0.65 }
      },
      ""aim"": { ""kind"": ""CasterForwardRange"" }
    },
    {
      ""stableId"": ""character_test.poison_bottle.projectile"",
      ""projectileStableId"": ""character_test.poison_bottle.projectile"",
      ""origin"": {
        ""kind"": ""Socket"",
        ""socketId"": ""mainHand"",
        ""localOffset"": { ""x"": 0, ""y"": 0, ""z"": 0 },
        ""fallbackRootOffset"": { ""x"": 0, ""y"": 1.15, ""z"": 0.65 }
      },
      ""aim"": {
        ""kind"": ""LockedTargetOrCasterForwardRange"",
        ""fallbackKind"": ""CasterForwardRange"",
        ""maxRange"": 0
      }
    }
  ],
  ""projectilePresentationBindings"": [
    {
      ""projectileDefinitionId"": 91001,
      ""prefabResource"": { ""id"": ""vfx.magic.fireball"", ""typeId"": ""GameObject"", ""variant"": """", ""packageId"": ""mxframework.samples"" },
      ""viewNamePrefix"": ""ProjectileVFX""
    },
    {
      ""projectileDefinitionId"": 91002,
      ""prefabResource"": { ""id"": ""vfx.magic.fireball_burst.flight"", ""typeId"": ""GameObject"", ""variant"": """", ""packageId"": ""mxframework.samples"" },
      ""viewNamePrefix"": ""ProjectileVFX""
    },
    {
      ""projectileDefinitionId"": 91003,
      ""prefabResource"": { ""id"": ""vfx.magic.flame_lance.flight"", ""typeId"": ""GameObject"", ""variant"": """", ""packageId"": ""mxframework.samples"" },
      ""viewNamePrefix"": ""ProjectileVFX""
    },
    {
      ""projectileDefinitionId"": 91009,
      ""prefabResource"": { ""id"": ""vfx.magic.fireball"", ""typeId"": ""GameObject"", ""variant"": """", ""packageId"": ""mxframework.samples"" },
      ""viewNamePrefix"": ""ProjectileVFX""
    },
    {
      ""projectileDefinitionId"": 91015,
      ""prefabResource"": { ""id"": ""vfx.magic.poison_bottle.flight"", ""typeId"": ""GameObject"", ""variant"": """", ""packageId"": ""mxframework.samples"" },
      ""viewNamePrefix"": ""ProjectileVFX""
    }
  ]
}";
        }

        private static string ProjectileGeneratedDefinitionsJson()
        {
            return @"{
  ""schemaVersion"": ""1"",
  ""packageId"": ""character_test.fireball"",
  ""definitions"": [
    {
      ""definitionId"": 91001,
      ""stableId"": ""character_test.fireball.projectile"",
      ""motion"": { ""speedPerFrame"": 0.35, ""lifetimeFrames"": 90 },
      ""collision"": {
        ""radius"": 0.25,
        ""layerMask"": 256,
        ""stopOnFirstAcceptedImpact"": true,
        ""stopOnBlockedImpact"": true,
        ""maxAcceptedImpacts"": 1,
        ""hitSameTargetPolicy"": ""AllowRepeat""
      },
      ""impact"": {
        ""hitEvent"": {
          ""targetTypes"": [ ""HostileCharacter"" ],
          ""environmentHitPolicy"": ""None"",
          ""stableEventId"": ""character_test.fireball.projectile.impact"",
          ""maxAcceptedHits"": 1,
          ""hitSameTargetPolicy"": ""AllowRepeat"",
          ""damage"": {
            ""damageTypeId"": ""fire"",
            ""baseDamage"": 10,
            ""impactPower"": 8,
            ""launchPower"": 25,
            ""tags"": [ ""Projectile"" ],
            ""analysisCategory"": ""FireballImpact""
          }
        },
        ""policyKind"": ""DirectHit"",
        ""areaRadius"": 0,
        ""areaLayerMask"": 0,
        ""triggerOnExpire"": false
      }
    },
    {
      ""definitionId"": 91002,
      ""stableId"": ""character_test.fireball_burst.projectile"",
      ""motion"": { ""speedPerFrame"": 0.35, ""lifetimeFrames"": 90 },
      ""collision"": {
        ""radius"": 0.25,
        ""layerMask"": 256,
        ""stopOnFirstAcceptedImpact"": true,
        ""stopOnBlockedImpact"": true,
        ""maxAcceptedImpacts"": 1,
        ""hitSameTargetPolicy"": ""AllowRepeat""
      },
      ""impact"": {
        ""hitEvent"": {
          ""targetTypes"": [ ""HostileCharacter"" ],
          ""environmentHitPolicy"": ""None"",
          ""stableEventId"": ""character_test.fireball_burst.projectile.impact"",
          ""maxAcceptedHits"": 1,
          ""hitSameTargetPolicy"": ""AllowRepeat"",
          ""damage"": {
            ""damageTypeId"": ""fire"",
            ""baseDamage"": 8,
            ""impactPower"": 6,
            ""tags"": [ ""Area"", ""Projectile"" ],
            ""analysisCategory"": ""FireballBurstImpact""
          }
        },
        ""policyKind"": ""AreaPulseOnImpact"",
        ""areaRadius"": 2,
        ""areaLayerMask"": 256,
        ""triggerOnExpire"": true,
        ""areaHitEvent"": {
          ""targetTypes"": [ ""HostileCharacter"" ],
          ""environmentHitPolicy"": ""None"",
          ""stableEventId"": ""character_test.fireball_burst.projectile.impact"",
          ""maxAcceptedHits"": 1,
          ""hitSameTargetPolicy"": ""AllowRepeat""
        }
      }
    },
    {
      ""definitionId"": 91003,
      ""stableId"": ""character_test.flame_lance.projectile"",
      ""motion"": { ""speedPerFrame"": 0.55, ""lifetimeFrames"": 80 },
      ""collision"": {
        ""radius"": 0.15,
        ""layerMask"": 256,
        ""stopOnFirstAcceptedImpact"": false,
        ""stopOnBlockedImpact"": true,
        ""maxAcceptedImpacts"": 3,
        ""hitSameTargetPolicy"": ""OncePerProjectile""
      },
      ""impact"": {
        ""hitEvent"": {
          ""targetTypes"": [ ""HostileCharacter"" ],
          ""environmentHitPolicy"": ""None"",
          ""stableEventId"": ""character_test.flame_lance.projectile.impact"",
          ""maxAcceptedHits"": 3,
          ""hitSameTargetPolicy"": ""OncePerEvent"",
          ""damage"": {
            ""damageTypeId"": ""fire"",
            ""baseDamage"": 7,
            ""impactPower"": 5,
            ""tags"": [ ""Pierce"", ""Projectile"" ],
            ""analysisCategory"": ""FlameLanceImpact""
          }
        },
        ""policyKind"": ""DirectHit"",
        ""areaRadius"": 0,
        ""areaLayerMask"": 0,
        ""triggerOnExpire"": false
      }
    },
    {
      ""definitionId"": 91009,
      ""stableId"": ""character_test.fireball_bezier.projectile"",
      ""motion"": {
        ""kind"": ""BezierToTarget"",
        ""speedPerFrame"": 0.35,
        ""lifetimeFrames"": 90,
        ""arcHeight"": 0,
        ""speedProfile"": { ""kind"": ""Constant"" },
        ""bezierControlOffsetA"": { ""x"": 0, ""y"": 3, ""z"": 6 },
        ""bezierControlOffsetB"": { ""x"": 0, ""y"": 2, ""z"": -4 }
      },
      ""collision"": {
        ""radius"": 0.25,
        ""layerMask"": 256,
        ""stopOnFirstAcceptedImpact"": true,
        ""stopOnBlockedImpact"": true,
        ""maxAcceptedImpacts"": 1,
        ""hitSameTargetPolicy"": ""AllowRepeat""
      },
      ""impact"": {
        ""hitEvent"": {
          ""targetTypes"": [ ""HostileCharacter"" ],
          ""environmentHitPolicy"": ""None"",
          ""stableEventId"": ""character_test.fireball_bezier.projectile.impact"",
          ""maxAcceptedHits"": 1,
          ""hitSameTargetPolicy"": ""AllowRepeat"",
          ""damage"": {
            ""damageTypeId"": ""fire"",
            ""baseDamage"": 10,
            ""impactPower"": 8,
            ""tags"": [ ""Bezier"", ""Projectile"" ],
            ""analysisCategory"": ""FireballBezierImpact""
          }
        },
        ""policyKind"": ""DirectHit"",
        ""areaRadius"": 0,
        ""areaLayerMask"": 0,
        ""triggerOnExpire"": false
      }
    }
  ]
}";
        }

        private static string ProjectileGeneratedBindingsJson()
        {
            return @"{
  ""schemaVersion"": ""1"",
  ""packageId"": ""character_test.fireball"",
  ""bindings"": [
    {
      ""projectileDefinitionId"": 91001,
      ""prefabResource"": { ""id"": ""vfx.magic.fireball"", ""typeId"": ""GameObject"", ""variant"": """", ""packageId"": ""mxframework.samples"" },
      ""viewNamePrefix"": ""ProjectileVFX""
    },
    {
      ""projectileDefinitionId"": 91002,
      ""prefabResource"": { ""id"": ""vfx.magic.fireball_burst.flight"", ""typeId"": ""GameObject"", ""variant"": """", ""packageId"": ""mxframework.samples"" },
      ""viewNamePrefix"": ""ProjectileVFX""
    },
    {
      ""projectileDefinitionId"": 91003,
      ""prefabResource"": { ""id"": ""vfx.magic.flame_lance.flight"", ""typeId"": ""GameObject"", ""variant"": """", ""packageId"": ""mxframework.samples"" },
      ""viewNamePrefix"": ""ProjectileVFX""
    },
    {
      ""projectileDefinitionId"": 91009,
      ""prefabResource"": { ""id"": ""vfx.magic.fireball"", ""typeId"": ""GameObject"", ""variant"": """", ""packageId"": ""mxframework.samples"" },
      ""viewNamePrefix"": ""ProjectileVFX""
    }
  ]
}";
        }

        private static string ProjectileGeneratedSpawnProfilesJson()
        {
            return @"{
  ""schemaVersion"": ""1"",
  ""packageId"": ""character_test.fireball"",
  ""profiles"": [
    {
      ""stableId"": ""character_test.fireball.projectile"",
      ""projectileStableId"": ""character_test.fireball.projectile"",
      ""origin"": {
        ""kind"": ""Socket"",
        ""socketId"": ""mainHand"",
        ""localOffset"": { ""x"": 0, ""y"": 0, ""z"": 0 },
        ""fallbackRootOffset"": { ""x"": 0, ""y"": 1.15, ""z"": 0.65 }
      },
      ""aim"": { ""kind"": ""CasterForwardRange"" }
    },
    {
      ""stableId"": ""character_test.fireball_burst.projectile"",
      ""projectileStableId"": ""character_test.fireball_burst.projectile"",
      ""origin"": {
        ""kind"": ""Socket"",
        ""socketId"": ""mainHand"",
        ""localOffset"": { ""x"": 0, ""y"": 0, ""z"": 0 },
        ""fallbackRootOffset"": { ""x"": 0, ""y"": 1.15, ""z"": 0.65 }
      },
      ""aim"": { ""kind"": ""CasterForwardRange"" }
    },
    {
      ""stableId"": ""character_test.flame_lance.projectile"",
      ""projectileStableId"": ""character_test.flame_lance.projectile"",
      ""origin"": {
        ""kind"": ""Socket"",
        ""socketId"": ""mainHand"",
        ""localOffset"": { ""x"": 0, ""y"": 0, ""z"": 0 },
        ""fallbackRootOffset"": { ""x"": 0, ""y"": 1.15, ""z"": 0.65 }
      },
      ""aim"": { ""kind"": ""CasterForwardRange"" }
    },
    {
      ""stableId"": ""character_test.fireball_bezier.projectile"",
      ""projectileStableId"": ""character_test.fireball_bezier.projectile"",
      ""origin"": {
        ""kind"": ""Socket"",
        ""socketId"": ""mainHand"",
        ""localOffset"": { ""x"": 0, ""y"": 0, ""z"": 0 },
        ""fallbackRootOffset"": { ""x"": 0, ""y"": 1.15, ""z"": 0.65 }
      },
      ""aim"": { ""kind"": ""CasterForwardRange"" }
    }
  ]
}";
        }

        private readonly struct ProjectileAuthoringFixturePaths
        {
            public ProjectileAuthoringFixturePaths(
                string sourcePath,
                string projectileSourceDirectoryPath,
                string generatedDefinitionsPath,
                string generatedSpawnProfilesPath,
                string generatedBindingsPath,
                string characterPackageSourceRoot,
                string generatedCharacterPackageRoot)
            {
                SourcePath = sourcePath;
                ProjectileSourceDirectoryPath = projectileSourceDirectoryPath;
                GeneratedDefinitionsPath = generatedDefinitionsPath;
                GeneratedSpawnProfilesPath = generatedSpawnProfilesPath;
                GeneratedBindingsPath = generatedBindingsPath;
                CharacterPackageSourceRoot = characterPackageSourceRoot;
                GeneratedCharacterPackageRoot = generatedCharacterPackageRoot;
            }

            public string SourcePath { get; }
            public string ProjectileSourceDirectoryPath { get; }
            public string GeneratedDefinitionsPath { get; }
            public string GeneratedSpawnProfilesPath { get; }
            public string GeneratedBindingsPath { get; }
            public string CharacterPackageSourceRoot { get; }
            public string GeneratedCharacterPackageRoot { get; }
        }

        private readonly struct MechanicTuningAuthoringFixturePaths
        {
            public MechanicTuningAuthoringFixturePaths(
                string sourcePath,
                string generatedMechanicTuningsPath,
                string playerMirrorMechanicTuningsPath)
            {
                SourcePath = sourcePath;
                GeneratedMechanicTuningsPath = generatedMechanicTuningsPath;
                PlayerMirrorMechanicTuningsPath = playerMirrorMechanicTuningsPath;
            }

            public string SourcePath { get; }
            public string GeneratedMechanicTuningsPath { get; }
            public string PlayerMirrorMechanicTuningsPath { get; }
        }
    }
}

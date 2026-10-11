using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Config;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.ConfigBridge;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest.Guidance
{
    public sealed class CharacterTestGuidanceAuthoringTests
    {
        private const string DefaultSourceHash =
            "7f19a6ced8982231b3590fa45b302df127522e722cda278dbcd5626e30f2dd3f";

        [Test]
        public void SixStepReferenceDocument_RoundTripsAndProducesStableGoldenHash()
        {
            CharacterTestGuidanceAuthoringDocument source =
                CharacterTestGuidanceAuthoringAdapter.CreateSixStepReferenceDocument();
            var envelope = new MxLevelAuthoringDocument
            {
                LevelId = "character_test_scene",
                ScenePath = "Assets/Scenes/CharacterTestScene.unity"
            };
            envelope = CharacterTestGuidanceAuthoringAdapter.Upsert(envelope, source);
            string json = MxLevelAuthoringJson.SaveToJson(envelope);
            MxLevelAuthoringDocument reloaded = MxLevelAuthoringJson.LoadFromJson(json);

            Assert.That(
                CharacterTestGuidanceAuthoringAdapter.TryExtract(
                    reloaded,
                    out CharacterTestGuidanceAuthoringDocument roundTrip,
                    out CharacterTestGuidanceRuntimeDiagnostic diagnostic),
                Is.True,
                diagnostic.Message);
            Assert.That(
                CharacterTestGuidanceCompiler.TryCompile(
                    roundTrip,
                    out CharacterTestGuidancePlan plan,
                    out CharacterTestGuidanceAuthoringReport report),
                Is.True,
                "Errors=" + report.ErrorCount);
            Assert.That(plan.StepCount, Is.EqualTo(6));
            Assert.That(plan.SourceHash, Is.EqualTo(DefaultSourceHash));
            Assert.That(plan.SourceHash, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void EmptyDocumentAndBlankStep_StartAsExplicitUnconfiguredDraft()
        {
            CharacterTestGuidanceAuthoringDocument document =
                CharacterTestGuidanceAuthoringAdapter.CreateEmptyDocument();

            Assert.That(document.Enabled, Is.False);
            Assert.That(document.Steps, Is.Empty);
            Assert.That(
                CharacterTestGuidanceCompiler.TryCompile(
                    document,
                    out CharacterTestGuidancePlan emptyPlan,
                    out CharacterTestGuidanceAuthoringReport emptyReport),
                Is.True,
                "Errors=" + emptyReport.ErrorCount);
            Assert.That(emptyPlan.Enabled, Is.False);
            Assert.That(emptyPlan.StepCount, Is.Zero);

            CharacterTestGuidanceStepAuthoring step =
                CharacterTestGuidanceAuthoringAdapter.CreateBlankStep("guidance-step");
            document.Steps.Add(step);

            Assert.That(step.Enabled, Is.True);
            Assert.That(step.InputActionId, Is.Empty);
            Assert.That(step.AutoAdvanceSeconds, Is.EqualTo(2d));
            Assert.That(step.Completion.TypeId, Is.Empty);
            Assert.That(step.Completion.SchemaVersion, Is.EqualTo(1));
            Assert.That(step.Completion.Payload, Is.Empty);
            IReadOnlyList<CharacterTestGuidanceTextAuthoring> localizedTexts =
                LocalizedTexts(step);
            for (int i = 0; i < localizedTexts.Count; i++)
            {
                Assert.That(localizedTexts[i].Fallback, Is.Empty);
                Assert.That(
                    CharacterTestGuidanceAuthoringAdapter.IsManagedTextKey(localizedTexts[i].Key),
                    Is.True,
                    localizedTexts[i].Key);
            }

            CharacterTestGuidanceAuthoringReport report =
                CharacterTestGuidanceAuthoringValidator.Validate(document);
            Assert.That(report.Issues, Has.Some.Matches<CharacterTestGuidanceAuthoringIssue>(
                issue => issue.Code == "MissingInputActionId"));
            Assert.That(report.Issues.Count(issue => issue.Code == "MissingLocalizedTextFallback"),
                Is.EqualTo(6));
            Assert.That(report.Issues, Has.Some.Matches<CharacterTestGuidanceAuthoringIssue>(
                issue => issue.Code == "MissingCompletionType"));
            Assert.That(report.Issues, Has.None.Matches<CharacterTestGuidanceAuthoringIssue>(
                issue => issue.Code == "UnknownCompletionType"));
            Assert.That(
                () => CharacterTestGuidanceAuthoringAdapter.Upsert(
                    new MxLevelAuthoringDocument(),
                    null),
                Throws.ArgumentNullException);
        }

        [Test]
        public void PresetCatalog_CreatesSixAuthorFacingIntentsWithoutSharingPayloads()
        {
            Assert.That(CharacterTestGuidanceStepPresetCatalog.Presets.Count, Is.EqualTo(6));
            string[] expectedNames =
            {
                "基础移动与停步",
                "跳跃与落地",
                "切换相机模式",
                "锁定目标",
                "取消锁定",
                "普通攻击命中"
            };
            for (int i = 0; i < CharacterTestGuidanceStepPresetCatalog.Presets.Count; i++)
            {
                CharacterTestGuidanceStepPreset preset =
                    CharacterTestGuidanceStepPresetCatalog.Presets[i];
                CharacterTestGuidanceStepAuthoring step =
                    CharacterTestGuidanceAuthoringAdapter.CreateStepFromPreset(
                        preset.PresetId,
                        preset.StableIdSeed);

                Assert.That(preset.DisplayName, Is.EqualTo(expectedNames[i]));
                Assert.That(step.Content.Title.Fallback, Is.EqualTo(expectedNames[i]));
                Assert.That(step.InputActionId, Is.EqualTo(preset.InputActionId));
                Assert.That(step.AutoAdvanceSeconds, Is.EqualTo(preset.AutoAdvanceSeconds));
                Assert.That(step.Completion.TypeId, Is.EqualTo(preset.CompletionTypeId));
                Assert.That(step.Completion.SchemaVersion, Is.EqualTo(1));
            }

            CharacterTestGuidanceStepAuthoring locked =
                CharacterTestGuidanceAuthoringAdapter.CreateStepFromPreset(
                    CharacterTestGuidanceStepPresetCatalog.TargetLock,
                    "lock-one");
            CharacterTestGuidanceStepAuthoring unlocked =
                CharacterTestGuidanceAuthoringAdapter.CreateStepFromPreset(
                    CharacterTestGuidanceStepPresetCatalog.TargetUnlock,
                    "unlock-one");
            Assert.That(locked.Completion.TypeId, Is.EqualTo(
                CharacterTestGuidanceCompletionRegistry.TargetLockState));
            Assert.That(unlocked.Completion.TypeId, Is.EqualTo(
                CharacterTestGuidanceCompletionRegistry.TargetLockState));
            Assert.That(locked.Completion.Payload.Value<bool>("expectedLocked"), Is.True);
            Assert.That(unlocked.Completion.Payload.Value<bool>("expectedLocked"), Is.False);
            locked.Completion.Payload["expectedLocked"] = false;
            Assert.That(unlocked.Completion.Payload.Value<bool>("expectedLocked"), Is.False,
                "Preset payloads must be independent clones.");
        }

        [Test]
        public void ChangeCompletionType_PreservesAuthorFieldsAndResetsOnlyTypedDefinition()
        {
            CharacterTestGuidanceStepAuthoring step =
                CharacterTestGuidanceAuthoringAdapter.CreateStepFromPreset(
                    CharacterTestGuidanceStepPresetCatalog.TargetUnlock,
                    "custom-step");
            step.Enabled = false;
            step.InputActionId = "Gameplay/Interact";
            step.AutoAdvanceSeconds = 4.25d;
            step.Content.Title.Fallback = "作者标题";
            string key = step.Content.Title.Key;

            CharacterTestGuidanceAuthoringAdapter.ChangeCompletionType(
                step,
                CharacterTestGuidanceCompletionRegistry.BasicAttackDamage);

            Assert.That(step.StableId, Is.EqualTo("custom-step"));
            Assert.That(step.Enabled, Is.False);
            Assert.That(step.InputActionId, Is.EqualTo("Gameplay/Interact"));
            Assert.That(step.AutoAdvanceSeconds, Is.EqualTo(4.25d));
            Assert.That(step.Content.Title.Fallback, Is.EqualTo("作者标题"));
            Assert.That(step.Content.Title.Key, Is.EqualTo(key));
            Assert.That(step.Completion.TypeId, Is.EqualTo(
                CharacterTestGuidanceCompletionRegistry.BasicAttackDamage));
            Assert.That(step.Completion.SchemaVersion, Is.EqualTo(1));
            Assert.That(step.Completion.Payload.Properties().Select(property => property.Name),
                Is.EquivalentTo(new[] { "actionId", "minimumFinalDamage" }));
            Assert.That(step.Completion.Payload.Value<int>("actionId"),
                Is.EqualTo(CharacterTestCharacterActionConfigSource.LightAttackActionId.Value));
            Assert.That(step.Completion.Payload.Value<int>("minimumFinalDamage"), Is.EqualTo(1));
            Assert.That(
                () => CharacterTestGuidanceAuthoringAdapter.ChangeCompletionType(
                    step,
                    "RuntimeEventByReflection"),
                Throws.ArgumentException);
        }

        [Test]
        public void Compiler_PreservesArrayOrderAndHashChangesWhenStepsMove()
        {
            CharacterTestGuidanceAuthoringDocument document =
                CharacterTestGuidanceAuthoringAdapter.CreateSixStepReferenceDocument();
            Assert.That(
                CharacterTestGuidanceCompiler.TryCompile(document, out CharacterTestGuidancePlan first, out _),
                Is.True);
            CharacterTestGuidanceStepAuthoring moved = document.Steps[0];
            document.Steps.RemoveAt(0);
            document.Steps.Insert(1, moved);

            Assert.That(
                CharacterTestGuidanceCompiler.TryCompile(document, out CharacterTestGuidancePlan second, out _),
                Is.True);
            Assert.That(second.Steps[0].StableId, Is.EqualTo("jump-and-land"));
            Assert.That(second.SourceHash, Is.Not.EqualTo(first.SourceHash));
        }

        [Test]
        public void Validator_RejectsDuplicateIdUnknownTypeAndEmptyEnabledSequence()
        {
            CharacterTestGuidanceAuthoringDocument document =
                CharacterTestGuidanceAuthoringAdapter.CreateSixStepReferenceDocument();
            document.Steps[1].StableId = document.Steps[0].StableId;
            document.Steps[2].Completion.TypeId = "RuntimeEventByReflection";
            document.Steps[3].AutoAdvanceSeconds = double.MaxValue;
            for (int i = 0; i < document.Steps.Count; i++)
                document.Steps[i].Enabled = false;

            CharacterTestGuidanceAuthoringReport report =
                CharacterTestGuidanceAuthoringValidator.Validate(document);
            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues, Has.Some.Matches<CharacterTestGuidanceAuthoringIssue>(
                issue => issue.Code == "DuplicateStableId"));
            Assert.That(report.Issues, Has.Some.Matches<CharacterTestGuidanceAuthoringIssue>(
                issue => issue.Code == "UnknownCompletionType"));
            Assert.That(report.Issues, Has.Some.Matches<CharacterTestGuidanceAuthoringIssue>(
                issue => issue.Code == "InvalidAutoAdvanceSeconds"));
            Assert.That(report.Issues, Has.Some.Matches<CharacterTestGuidanceAuthoringIssue>(
                issue => issue.Code == "EmptyEnabledSequence"));
        }

        [Test]
        public void Validator_RejectsNonManagedTextKeyAndInvalidStableId()
        {
            CharacterTestGuidanceAuthoringDocument document =
                CharacterTestGuidanceAuthoringAdapter.CreateSixStepReferenceDocument();
            document.Steps[0].StableId = "Bad ID";
            document.Steps[0].Content.Title.Key = "ui.custom.title";

            CharacterTestGuidanceAuthoringReport report =
                CharacterTestGuidanceAuthoringValidator.Validate(document);

            Assert.That(report.Issues, Has.Some.Matches<CharacterTestGuidanceAuthoringIssue>(
                issue => issue.Code == "InvalidStableId"));
            Assert.That(report.Issues, Has.Some.Matches<CharacterTestGuidanceAuthoringIssue>(
                issue => issue.Code == "UnexpectedLocalizedTextKey"));
        }

        [Test]
        public void FormalLocalization_ContainsEveryReferencePresetTextAndEnglishTranslation()
        {
            string path = ToFullPath(CharacterTestGuidanceAuthoringAdapter.DefaultLocalizationPath);
            LocalizationTsvLoadResult localization =
                LocalizationTsvLoader.LoadFromText(File.ReadAllText(path));
            Assert.That(localization.HasErrors, Is.False);

            CharacterTestGuidanceAuthoringDocument document =
                CharacterTestGuidanceAuthoringAdapter.CreateSixStepReferenceDocument();
            for (int i = 0; i < document.Steps.Count; i++)
            {
                CharacterTestGuidanceContentAuthoring content = document.Steps[i].Content;
                AssertLocalized(content.Title);
                AssertLocalized(content.ActionInstruction);
                AssertLocalized(content.Focus);
                AssertLocalized(content.WaitingStatus);
                AssertLocalized(content.CompletedStatus);
                AssertLocalized(content.SuccessNotice);
            }

            void AssertLocalized(CharacterTestGuidanceTextAuthoring text)
            {
                Assert.That(localization.Provider.TryGetText(
                    new LocalizedTextKey(text.Key),
                    new LocaleId(CharacterTestGuidanceAuthoringAdapter.SourceLocaleId),
                    out string sourceSnapshot), Is.True, text.Key);
                Assert.That(sourceSnapshot, Is.EqualTo(text.Fallback), text.Key);
                Assert.That(localization.Provider.TryGetText(
                    new LocalizedTextKey(text.Key),
                    new LocaleId(CharacterTestGuidanceAuthoringAdapter.TranslationLocaleId),
                    out string english), Is.True, text.Key);
                Assert.That(english, Is.Not.Empty, text.Key);
            }
        }

        [Test]
        public void GuidanceTextProvider_UsesAuthoredSourceAndFormalEnglishTranslation()
        {
            string path = ToFullPath(CharacterTestGuidanceAuthoringAdapter.DefaultLocalizationPath);
            LocalizationTsvLoadResult localization =
                LocalizationTsvLoader.LoadFromText(File.ReadAllText(path));
            var inner = new MxConfigUiTextProvider(
                localization.Provider,
                new MxUiLocaleId(CharacterTestGuidanceAuthoringAdapter.SourceLocaleId));
            var provider = new CharacterTestGuidanceTextProvider(inner);
            MxUiLocalizedTextRequest request = new MxUiLocalizedTextRequest(
                new MxUiTextKey("ui.character_test.guidance.basic_movement.title"),
                "作者刚修改的简中源文案");

            Assert.That(provider.TryGetText(request, out string source), Is.True);
            Assert.That(source, Is.EqualTo("作者刚修改的简中源文案"));

            inner.SetLocale(new MxUiLocaleId(CharacterTestGuidanceAuthoringAdapter.TranslationLocaleId));
            Assert.That(provider.Revision, Is.EqualTo(inner.Revision));
            Assert.That(provider.TryGetText(request, out string english), Is.True);
            Assert.That(english, Is.EqualTo("Basic Movement and Stop"));
        }

        [Test]
        public void DisabledRoot_AllowsZeroEnabledStepsAndCacheReportsNormalDisable()
        {
            CharacterTestGuidanceAuthoringDocument document =
                CharacterTestGuidanceAuthoringAdapter.CreateSixStepReferenceDocument();
            document.Enabled = false;
            for (int i = 0; i < document.Steps.Count; i++)
                document.Steps[i].Enabled = false;

            Assert.That(
                CharacterTestGuidanceCompiler.TryCompile(document, out CharacterTestGuidancePlan plan, out _),
                Is.True);
            var cache = new CharacterTestGuidanceSceneCache();
            cache.Configure(plan);

            Assert.That(cache.TryCompilePlan(out _, out CharacterTestGuidanceRuntimeDiagnostic diagnostic), Is.False);
            Assert.That(diagnostic.Code, Is.EqualTo(CharacterTestGuidanceRuntimeDiagnosticCode.AuthorDisabled));
            Assert.That(diagnostic.IsError, Is.False);
        }

        [Test]
        public void SceneCache_FailsClosedWhenCanonicalJsonIsTampered()
        {
            CharacterTestGuidanceAuthoringDocument document =
                CharacterTestGuidanceAuthoringAdapter.CreateSixStepReferenceDocument();
            Assert.That(
                CharacterTestGuidanceCompiler.TryCompile(document, out CharacterTestGuidancePlan plan, out _),
                Is.True);
            var cache = new CharacterTestGuidanceSceneCache();
            cache.Configure(plan);
            FieldInfo field = typeof(CharacterTestGuidanceSceneCache).GetField(
                "_canonicalGuidanceModuleJson",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            JObject tampered = JObject.Parse(cache.CanonicalGuidanceModuleJson);
            tampered["payload"]["steps"][0]["stableId"] = "tampered";
            field.SetValue(cache, tampered.ToString(Newtonsoft.Json.Formatting.None));

            Assert.That(cache.TryCompilePlan(out _, out CharacterTestGuidanceRuntimeDiagnostic diagnostic), Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(CharacterTestGuidanceRuntimeDiagnosticCode.CacheSourceHashMismatch));
        }

        [Test]
        public void FormalCharacterTestScene_CacheMatchesGuidanceModule()
        {
            string fullPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                CharacterTestGuidanceAuthoringAdapter.DefaultDocumentPath));
            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));
            Assert.That(
                CharacterTestGuidanceAuthoringAdapter.TryExtract(
                    envelope,
                    out CharacterTestGuidanceAuthoringDocument document,
                    out CharacterTestGuidanceRuntimeDiagnostic extractionDiagnostic),
                Is.True,
                extractionDiagnostic.Message);
            Assert.That(
                CharacterTestGuidanceCompiler.TryCompile(
                    document,
                    out CharacterTestGuidancePlan authoredPlan,
                    out CharacterTestGuidanceAuthoringReport report),
                Is.True,
                "Errors=" + report.ErrorCount);

            Scene scene = SceneManager.GetSceneByPath(
                CharacterTestGuidanceAuthoringAdapter.DefaultScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(
                    CharacterTestGuidanceAuthoringAdapter.DefaultScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                GameManager manager = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length && manager == null; i++)
                    manager = roots[i].GetComponentInChildren<GameManager>(true);
                Assert.That(manager, Is.Not.Null, "CharacterTest Scene has no GameManager.");
                Assert.That(
                    manager.GuidanceSceneCache.TryCompilePlan(
                        out CharacterTestGuidancePlan cachedPlan,
                        out CharacterTestGuidanceRuntimeDiagnostic cacheDiagnostic),
                    Is.True,
                    cacheDiagnostic.Message);
                Assert.That(cachedPlan.StepCount, Is.EqualTo(6));
                Assert.That(cachedPlan.SourceHash, Is.EqualTo(authoredPlan.SourceHash));
                Assert.That(cachedPlan.CompletionCatalogHash, Is.EqualTo(authoredPlan.CompletionCatalogHash));
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void FormalCharacterTestScene_FreshnessPreflightAcceptsSavedPayloadAndCache()
        {
            CharacterTestGuidanceSceneCacheFreshnessResult result =
                CharacterTestGuidanceSceneCacheFreshness.ValidateFormalScene();

            Assert.That(result.Success, Is.True, result.Code + ": " + result.Message);
            Assert.That(
                result.AuthoredSourceHash,
                Is.EqualTo(result.CacheSourceHash).And.Not.Empty);
        }

        [Test]
        public void PlayModeFreshness_DoesNotTreatUnrelatedSceneDirtyFlagAsGuidanceFailure()
        {
            Assert.That(
                CharacterTestGuidanceSceneCacheFreshness.ShouldRejectDirtyScene(
                    sceneIsDirty: true,
                    requireSavedScene: false),
                Is.False);
            Assert.That(
                CharacterTestGuidanceSceneCacheFreshness.ShouldRejectDirtyScene(
                    sceneIsDirty: true,
                    requireSavedScene: true),
                Is.True);
        }

        [Test]
        public void RuntimePlan_RepeatedActionStepsKeepIndependentAuthoredPrompts()
        {
            CharacterTestGuidanceStepAuthoring first =
                CharacterTestGuidanceAuthoringAdapter.CreateStepFromPreset(
                    CharacterTestGuidanceStepPresetCatalog.BasicMovement,
                    "move-first");
            CharacterTestGuidanceStepAuthoring second =
                CharacterTestGuidanceAuthoringAdapter.CreateStepFromPreset(
                    CharacterTestGuidanceStepPresetCatalog.BasicMovement,
                    "move-second");
            first.Content.ActionInstruction.Fallback = "第一段移动";
            second.Content.ActionInstruction.Fallback = "第二段移动";
            var document = new CharacterTestGuidanceAuthoringDocument
            {
                Enabled = true,
                Steps = new List<CharacterTestGuidanceStepAuthoring> { first, second }
            };
            Assert.That(
                CharacterTestGuidanceCompiler.TryCompile(document, out CharacterTestGuidancePlan plan, out _),
                Is.True);

            var coordinator = new CharacterTestGuidanceSequenceCoordinator(plan);
            var mapper = new CharacterTestGuidanceSnapshotMapper(plan);
            mapper.SetStepActionPrompts(new[]
            {
                new MxUiLocalizedTextRequest(new MxUiTextKey("guidance.move.first.binding"), "按 W：第一段移动"),
                new MxUiLocalizedTextRequest(new MxUiTextKey("guidance.move.second.binding"), "按 W：第二段移动")
            });
            var observation = new CharacterTestGuidanceObservation(
                true,
                7,
                0,
                0,
                0,
                true,
                CharacterTestCameraMode.ThirdPersonControlled,
                false);
            var events = Array.Empty<CharacterTestRuntimeEvent>();
            var damageRows = Array.Empty<DamageTelemetryRow>();
            var context = new CharacterTestGuidanceObservationContext(
                events,
                damageRows,
                in observation);
            coordinator.Prime(in context);

            Assert.That(
                mapper.CreateCurrentCard(coordinator, in observation).ActionLine.Body.FallbackText,
                Is.EqualTo("按 W：第一段移动"));
            Assert.That(coordinator.TrySelectNext(in context), Is.True);
            MxUiGuidanceCardSnapshot next = mapper.CreateCurrentCard(coordinator, in observation);
            Assert.That(next.ActionLine.Body.FallbackText, Is.EqualTo("按 W：第二段移动"));
        }

        [Test]
        public void EditorProjection_BuildsThreePanesWithoutDirtyingDocument()
        {
            var module = new CharacterTestGuidanceLevelEditorModule();
            module.OnEnable();

            VisualElement root = module.CreateContent(null);
            Assert.That(root.Q("character-test-guidance-step-list-pane"), Is.Not.Null);
            Assert.That(root.Q("character-test-guidance-step-editor"), Is.Not.Null);
            Assert.That(root.Q("character-test-guidance-semantic-preview"), Is.Not.Null);
            Assert.That(module.IsDirty, Is.False);
            List<TextField> textFields = root.Query<TextField>().ToList();
            Assert.That(textFields, Has.None.Matches<TextField>(field => field.label == "Key"));
            Assert.That(textFields, Has.Some.Matches<TextField>(field => field.label == "简中源文案"));
            Assert.That(root.Query<Foldout>().ToList(), Has.Some.Matches<Foldout>(
                foldout => foldout.text.Contains("Key 由系统管理")));
            DropdownField completionField =
                root.Q<DropdownField>("character-test-guidance-completion-condition");
            Assert.That(completionField, Is.Not.Null);
            Assert.That(completionField.choices, Is.EqualTo(new[]
            {
                "移动距离并停步",
                "跳跃并落地",
                "相机模式切换",
                "目标锁定状态",
                "普通攻击伤害"
            }));
            Assert.That(root.Q<Button>("character-test-guidance-add-blank-step-list"), Is.Not.Null);
            ToolbarMenu presetMenu =
                root.Q<ToolbarMenu>("character-test-guidance-add-preset-step-list");
            Assert.That(presetMenu, Is.Not.Null);
            string[] menuNames = presetMenu.menu.MenuItems()
                .OfType<DropdownMenuAction>()
                .Select(action => action.name)
                .ToArray();
            Assert.That(menuNames, Is.EqualTo(new[]
            {
                "基础移动与停步",
                "跳跃与落地",
                "切换相机模式",
                "锁定目标",
                "取消锁定",
                "普通攻击命中"
            }));
            for (int i = 0; i < menuNames.Length; i++)
                Assert.That(CharacterTestGuidanceCompletionRegistry.TypeIds.Contains(menuNames[i]), Is.False);

            module.SelectStepForTests("target-lock");
            Assert.That(module.IsDirty, Is.False);

            module.SelectCompletedPreviewForTests();
            Assert.That(module.PreviewStateForTests, Is.EqualTo("Completed"));
            Assert.That(module.IsDirty, Is.False);
            module.SelectEnglishPreviewForTests();
            Assert.That(module.IsDirty, Is.False);
        }

        [Test]
        public void EditorLoad_MissingModuleStartsEmptyDisabledAndClean()
        {
            var module = new CharacterTestGuidanceLevelEditorModule();
            module.SetDocumentPathForTests(
                "Assets/Temp/CharacterTestGuidanceAuthoringEmpty.missing.json");
            module.OnEnable();

            Assert.That(module.AuthoringEditableForTests, Is.True);
            Assert.That(module.DocumentForTests.Enabled, Is.False);
            Assert.That(module.DocumentForTests.Steps, Is.Empty);
            Assert.That(module.SelectedStableIdForTests, Is.Empty);
            Assert.That(module.IsDirty, Is.False);

            VisualElement root = module.CreateContent(null);
            Assert.That(root.Q("character-test-guidance-step-list-pane"), Is.Not.Null);
            Assert.That(root.Q<Button>("character-test-guidance-add-blank-step-list"), Is.Not.Null);
            Assert.That(
                root.Q<ToolbarMenu>("character-test-guidance-add-preset-step-list"),
                Is.Not.Null);
            Assert.That(module.IsDirty, Is.False);
        }

        [Test]
        public void EditorLoad_UnknownModuleSchemaIsReadOnlyAndPreservesPayload()
        {
            string folderPath = "Assets/Temp/CharacterTestGuidanceUnknown_"
                + Guid.NewGuid().ToString("N");
            string documentPath = folderPath + "/level.json";
            Directory.CreateDirectory(ToFullPath(folderPath));
            try
            {
                var envelope = new MxLevelAuthoringDocument
                {
                    LevelId = "guidance-unknown-schema",
                    ScenePath = CharacterTestGuidanceAuthoringAdapter.DefaultScenePath
                };
                envelope.UpsertModulePayload(
                    CharacterTestGuidanceAuthoringAdapter.ModuleId,
                    99,
                    new JObject { ["futureField"] = "preserve-me" },
                    CharacterTestGuidanceAuthoringAdapter.DisplayName);
                File.WriteAllText(
                    ToFullPath(documentPath),
                    MxLevelAuthoringJson.SaveToJson(envelope));
                string original = File.ReadAllText(ToFullPath(documentPath));

                var module = new CharacterTestGuidanceLevelEditorModule();
                module.SetDocumentPathForTests(documentPath);
                module.OnEnable();
                Assert.That(module.AuthoringEditableForTests, Is.False);
                VisualElement root = module.CreateContent(null);
                Assert.That(
                    root.Q("character-test-guidance-read-only-diagnostic"),
                    Is.Not.Null);

                module.MutateForTests("不得覆盖未来数据", document => document.Enabled = true);

                Assert.That(module.IsDirty, Is.False);
                Assert.That(File.ReadAllText(ToFullPath(documentPath)), Is.EqualTo(original));
            }
            finally
            {
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [UnityTest]
        public IEnumerator EditorMutation_UndoRedoRestoresDocumentAndDirtyBaseline()
        {
            Undo.ClearAll();
            var module = new CharacterTestGuidanceLevelEditorModule();
            module.SetDocumentPathForTests(
                "Assets/Temp/CharacterTestGuidanceAuthoringUndo.missing.json");
            module.OnEnable();
            int originalCount = module.DocumentForTests.Steps.Count;

            module.AddBlankStepForTests();
            Assert.That(module.DocumentForTests.Steps.Count, Is.EqualTo(originalCount + 1));
            Assert.That(module.IsDirty, Is.True);
            yield return null;

            Undo.PerformUndo();
            yield return null;
            Assert.That(module.DocumentForTests.Steps.Count, Is.EqualTo(originalCount));
            Assert.That(module.IsDirty, Is.False);

            Undo.PerformRedo();
            yield return null;
            Assert.That(module.DocumentForTests.Steps.Count, Is.EqualTo(originalCount + 1));
            Assert.That(module.IsDirty, Is.True);

            Undo.PerformUndo();
            yield return null;
            Undo.ClearAll();
        }

        [Test]
        public void EditorCommands_PreserveCompletionIdentityAndArrayOrdering()
        {
            var module = new CharacterTestGuidanceLevelEditorModule();
            module.SetDocumentPathForTests(
                "Assets/Temp/CharacterTestGuidanceAuthoringCommands.missing.json");
            module.OnEnable();
            module.AddPresetStepForTests(CharacterTestGuidanceStepPresetCatalog.TargetLock);
            int originalCount = module.DocumentForTests.Steps.Count;

            module.DuplicateSelectedStepForTests();
            Assert.That(module.DocumentForTests.Steps.Count, Is.EqualTo(originalCount + 1));
            CharacterTestGuidanceStepAuthoring copy = module.DocumentForTests.Steps[1];
            Assert.That(copy.Completion.TypeId, Is.EqualTo(CharacterTestGuidanceCompletionRegistry.TargetLockState));
            Assert.That(copy.StableId, Is.Not.EqualTo("target-lock"));
            Assert.That(copy.Content.Title.Key, Is.EqualTo(
                CharacterTestGuidanceAuthoringAdapter.CreateManagedTextKey(copy.StableId, "title")));
            Assert.That(copy.Content.Title.Key, Is.Not.EqualTo(
                module.DocumentForTests.Steps[0].Content.Title.Key));

            string copiedId = copy.StableId;
            module.MoveSelectedStepForTests(-1);
            Assert.That(module.DocumentForTests.Steps[0].StableId, Is.EqualTo(copiedId));
            module.DeleteSelectedStepForTests();
            Assert.That(module.DocumentForTests.Steps.Count, Is.EqualTo(originalCount));
        }

        [Test]
        public void EditorMutation_WhenStableIdChanges_KeepsTheSameStepSelected()
        {
            var module = new CharacterTestGuidanceLevelEditorModule();
            module.SetDocumentPathForTests(
                "Assets/Temp/CharacterTestGuidanceAuthoringSelection.missing.json");
            module.OnEnable();
            module.AddPresetStepForTests(CharacterTestGuidanceStepPresetCatalog.TargetLock);

            module.MutateForTests("修改稳定 ID", document =>
                document.Steps[0].StableId = "target-lock-renamed");

            Assert.That(module.SelectedStableIdForTests, Is.EqualTo("target-lock-renamed"));
            CharacterTestGuidanceStepAuthoring renamed = module.DocumentForTests.Steps[0];
            Assert.That(renamed.Content.Title.Key, Is.EqualTo(
                "ui.character_test.guidance.target_lock_renamed.title"));
        }

        [Test]
        public void EditorCreation_InsertsAfterSelectionOrAppendsWithoutSelection()
        {
            var module = new CharacterTestGuidanceLevelEditorModule();
            module.SetDocumentPathForTests(
                "Assets/Temp/CharacterTestGuidanceAuthoringInsert.missing.json");
            module.OnEnable();

            module.AddPresetStepForTests(CharacterTestGuidanceStepPresetCatalog.BasicMovement);
            module.AddPresetStepForTests(CharacterTestGuidanceStepPresetCatalog.TargetLock);
            Assert.That(module.DocumentForTests.Steps.Select(step => step.StableId),
                Is.EqualTo(new[] { "basic-movement", "target-lock" }));

            module.SelectStepForTests("basic-movement");
            module.AddBlankStepForTests();
            Assert.That(module.DocumentForTests.Steps.Select(step => step.StableId),
                Is.EqualTo(new[] { "basic-movement", "guidance-step", "target-lock" }));
            Assert.That(module.SelectedStableIdForTests, Is.EqualTo("guidance-step"));
            Assert.That(module.DocumentForTests.Enabled, Is.False,
                "Adding a step must not implicitly enable the guidance root.");

            module.SelectStepForTests(string.Empty);
            module.AddBlankStepForTests();
            Assert.That(
                module.DocumentForTests.Steps[module.DocumentForTests.Steps.Count - 1].StableId,
                Is.EqualTo("guidance-step-2"));
            Assert.That(module.SelectedStableIdForTests, Is.EqualTo("guidance-step-2"));
        }

        [UnityTest]
        public IEnumerator EditorCompletionSwitch_UndoRedoRestoresTypePayloadAndAuthorFields()
        {
            Undo.ClearAll();
            var module = new CharacterTestGuidanceLevelEditorModule();
            module.SetDocumentPathForTests(
                "Assets/Temp/CharacterTestGuidanceCompletionUndo.missing.json");
            module.OnEnable();
            module.AddPresetStepForTests(CharacterTestGuidanceStepPresetCatalog.TargetUnlock);
            CharacterTestGuidanceStepAuthoring before = module.DocumentForTests.Steps[0];
            before.InputActionId = "Gameplay/Interact";
            before.AutoAdvanceSeconds = 3.5d;
            before.Content.Title.Fallback = "自定义取消锁定";
            string beforeKey = before.Content.Title.Key;
            module.ResetUndoBaselineForTests();

            module.ChangeSelectedCompletionTypeForTests(
                CharacterTestGuidanceCompletionRegistry.BasicAttackDamage);
            CharacterTestGuidanceStepAuthoring changed = module.DocumentForTests.Steps[0];
            Assert.That(changed.Completion.TypeId, Is.EqualTo(
                CharacterTestGuidanceCompletionRegistry.BasicAttackDamage));
            Assert.That(changed.Completion.Payload.Value<int>("actionId"),
                Is.EqualTo(CharacterTestCharacterActionConfigSource.LightAttackActionId.Value));
            Assert.That(changed.StableId, Is.EqualTo("target-unlock"));
            Assert.That(changed.InputActionId, Is.EqualTo("Gameplay/Interact"));
            Assert.That(changed.AutoAdvanceSeconds, Is.EqualTo(3.5d));
            Assert.That(changed.Content.Title.Fallback, Is.EqualTo("自定义取消锁定"));
            Assert.That(changed.Content.Title.Key, Is.EqualTo(beforeKey));
            Assert.That(module.PreviewStateForTests, Is.EqualTo("Waiting"));
            yield return null;

            Undo.PerformUndo();
            yield return null;
            CharacterTestGuidanceStepAuthoring undone = module.DocumentForTests.Steps[0];
            Assert.That(undone.Completion.TypeId, Is.EqualTo(
                CharacterTestGuidanceCompletionRegistry.TargetLockState));
            Assert.That(undone.Completion.Payload.Value<bool>("expectedLocked"), Is.False);
            Assert.That(undone.InputActionId, Is.EqualTo("Gameplay/Interact"));
            Assert.That(undone.AutoAdvanceSeconds, Is.EqualTo(3.5d));
            Assert.That(undone.Content.Title.Fallback, Is.EqualTo("自定义取消锁定"));

            Undo.PerformRedo();
            yield return null;
            CharacterTestGuidanceStepAuthoring redone = module.DocumentForTests.Steps[0];
            Assert.That(redone.Completion.TypeId, Is.EqualTo(
                CharacterTestGuidanceCompletionRegistry.BasicAttackDamage));
            Assert.That(redone.Completion.Payload.Value<int>("actionId"),
                Is.EqualTo(CharacterTestCharacterActionConfigSource.LightAttackActionId.Value));
            Undo.ClearAll();
        }

        [Test]
        public void EditorLoad_UnknownCompletionTypeRemainsEditableAndCanBeRepaired()
        {
            string folderPath = "Assets/Temp/CharacterTestGuidanceUnknownType_"
                + Guid.NewGuid().ToString("N");
            string documentPath = folderPath + "/level.json";
            Directory.CreateDirectory(ToFullPath(folderPath));
            try
            {
                CharacterTestGuidanceAuthoringDocument document =
                    CharacterTestGuidanceAuthoringAdapter.CreateSixStepReferenceDocument();
                document.Steps[0].Completion.TypeId = "FutureCompletionCondition";
                document.Steps[0].Completion.Payload = new JObject
                {
                    ["futureField"] = "preserve-until-repair"
                };
                File.WriteAllText(
                    ToFullPath(documentPath),
                    MxLevelAuthoringJson.SaveToJson(CreateEnvelopeWithSibling(document)));

                var module = new CharacterTestGuidanceLevelEditorModule();
                module.SetDocumentPathForTests(documentPath);
                module.OnEnable();
                Assert.That(module.AuthoringEditableForTests, Is.True);
                Assert.That(module.DocumentForTests.Steps[0].Completion.TypeId,
                    Is.EqualTo("FutureCompletionCondition"));
                Assert.That(module.DocumentForTests.Steps[0].Completion.Payload.Value<string>("futureField"),
                    Is.EqualTo("preserve-until-repair"));

                VisualElement root = module.CreateContent(null);
                DropdownField completionField =
                    root.Q<DropdownField>("character-test-guidance-completion-condition");
                Assert.That(completionField, Is.Not.Null);
                Assert.That(completionField.choices, Is.EqualTo(new[]
                {
                    "移动距离并停步",
                    "跳跃并落地",
                    "相机模式切换",
                    "目标锁定状态",
                    "普通攻击伤害"
                }));
                Assert.That(completionField.index, Is.EqualTo(-1));
                module.ChangeSelectedCompletionTypeForTests(
                    CharacterTestGuidanceCompletionRegistry.JumpAndLand);

                Assert.That(module.DocumentForTests.Steps[0].Completion.TypeId,
                    Is.EqualTo(CharacterTestGuidanceCompletionRegistry.JumpAndLand));
                Assert.That(module.DocumentForTests.Steps[0].Completion.Payload, Is.Empty);
                Assert.That(module.IsDirty, Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void EditorSave_IncompleteDraftFailsPrepareWithoutWritingDocument(
            bool rootEnabled,
            bool stepEnabled)
        {
            string folderPath = "Assets/Temp/CharacterTestGuidanceDraft_"
                + Guid.NewGuid().ToString("N");
            string documentPath = folderPath + "/level.json";
            Directory.CreateDirectory(ToFullPath(folderPath));
            try
            {
                var module = new CharacterTestGuidanceLevelEditorModule();
                module.SetDocumentPathForTests(documentPath);
                module.OnEnable();
                module.AddBlankStepForTests();
                module.MutateForTests("设置草稿启用状态", document =>
                {
                    document.Enabled = rootEnabled;
                    document.Steps[0].Enabled = stepEnabled;
                });

                IMxLevelEditorSaveParticipant participant = module;
                Assert.That(participant.TryPrepareSave(out string failureMessage), Is.False);
                Assert.That(failureMessage, Does.Contain("校验失败"));
                Assert.That(module.SaveStateForTests, Is.EqualTo("PreCommitFailed"));
                Assert.That(File.Exists(ToFullPath(documentPath)), Is.False);

                CharacterTestGuidanceAuthoringReport report =
                    CharacterTestGuidanceAuthoringValidator.Validate(module.DocumentForTests);
                Assert.That(report.Issues, Has.Some.Matches<CharacterTestGuidanceAuthoringIssue>(
                    issue => issue.Code == "MissingInputActionId"));
                Assert.That(report.Issues, Has.Some.Matches<CharacterTestGuidanceAuthoringIssue>(
                    issue => issue.Code == "MissingLocalizedTextFallback"));
                Assert.That(report.Issues, Has.Some.Matches<CharacterTestGuidanceAuthoringIssue>(
                    issue => issue.Code == "MissingCompletionType"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [Test]
        public void SceneCacheFreshness_UsesOnlyGuidanceModuleHashAndRejectsStaleCache()
        {
            string folderPath = "Assets/Temp/CharacterTestGuidanceFreshness_"
                + Guid.NewGuid().ToString("N");
            string documentPath = folderPath + "/level.json";
            Directory.CreateDirectory(ToFullPath(folderPath));
            try
            {
                CharacterTestGuidanceAuthoringDocument document =
                    CharacterTestGuidanceAuthoringAdapter.CreateSixStepReferenceDocument();
                MxLevelAuthoringDocument envelope = CreateEnvelopeWithSibling(document);
                File.WriteAllText(ToFullPath(documentPath), MxLevelAuthoringJson.SaveToJson(envelope));
                AssetDatabase.ImportAsset(documentPath, ImportAssetOptions.ForceUpdate);
                Assert.That(CharacterTestGuidanceCompiler.TryCompile(
                    document,
                    out CharacterTestGuidancePlan plan,
                    out _), Is.True);
                var cache = new CharacterTestGuidanceSceneCache();
                cache.Configure(plan);

                CharacterTestGuidanceSceneCacheFreshnessResult fresh =
                    CharacterTestGuidanceSceneCacheFreshness.ValidateCache(cache, documentPath);
                Assert.That(fresh.Success, Is.True, fresh.Message);

                envelope.Metadata["unrelated"] = "changed";
                File.WriteAllText(ToFullPath(documentPath), MxLevelAuthoringJson.SaveToJson(envelope));
                CharacterTestGuidanceSceneCacheFreshnessResult unrelatedChange =
                    CharacterTestGuidanceSceneCacheFreshness.ValidateCache(cache, documentPath);
                Assert.That(unrelatedChange.Success, Is.True, unrelatedChange.Message);

                document.Steps[0].Content.Title.Fallback += "（已修改）";
                envelope = CharacterTestGuidanceAuthoringAdapter.Upsert(envelope, document);
                File.WriteAllText(ToFullPath(documentPath), MxLevelAuthoringJson.SaveToJson(envelope));
                CharacterTestGuidanceSceneCacheFreshnessResult stale =
                    CharacterTestGuidanceSceneCacheFreshness.ValidateCache(cache, documentPath);
                Assert.That(stale.Success, Is.False);
                Assert.That(stale.Code, Is.EqualTo(CharacterTestGuidanceSceneCacheFreshness.StaleCode));
                Assert.That(stale.AuthoredSourceHash, Is.Not.EqualTo(stale.CacheSourceHash));
            }
            finally
            {
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [Test]
        public void CoordinatedSave_PreservesSiblingPayloadAndRollsBackDocumentAndCacheOnTailFailure()
        {
            string folderPath = "Assets/Temp/CharacterTestGuidanceTransaction_"
                + Guid.NewGuid().ToString("N");
            string documentPath = folderPath + "/level.json";
            string failingDocumentPath = folderPath + "/tail.txt";
            Directory.CreateDirectory(ToFullPath(folderPath));
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene testScene = default;
            try
            {
                CharacterTestGuidanceAuthoringDocument originalDocument =
                    CharacterTestGuidanceAuthoringAdapter.CreateSixStepReferenceDocument();
                MxLevelAuthoringDocument envelope = CreateEnvelopeWithSibling(originalDocument);
                string originalJson = MxLevelAuthoringJson.SaveToJson(envelope);
                File.WriteAllText(ToFullPath(documentPath), originalJson);
            File.WriteAllText(ToFullPath(failingDocumentPath), "tail-original");
            AssetDatabase.Refresh();

                bool replaceUntitledScene = string.IsNullOrWhiteSpace(previousActiveScene.path);
                testScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    replaceUntitledScene ? NewSceneMode.Single : NewSceneMode.Additive);
                string scenePath = folderPath + "/GuidanceTransactionTest.unity";
                Assert.That(EditorSceneManager.SaveScene(testScene, scenePath), Is.True);
                if (SceneManager.GetActiveScene().handle != testScene.handle)
                    SceneManager.SetActiveScene(testScene);
                Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(testScene.handle));
                var managerObject = new GameObject("CharacterTest GameManager");
                SceneManager.MoveGameObjectToScene(managerObject, testScene);
                managerObject.SetActive(false);
                GameManager manager = managerObject.AddComponent<GameManager>();
                Assert.That(CharacterTestGuidanceCompiler.TryCompile(
                    originalDocument,
                    out CharacterTestGuidancePlan originalPlan,
                    out _), Is.True);
                var originalCache = new CharacterTestGuidanceSceneCache();
                originalCache.Configure(originalPlan);
                manager.SetGuidanceSceneCache(originalCache);
                EditorUtility.SetDirty(manager);
                Assert.That(EditorSceneManager.SaveScene(testScene), Is.True);

                var guidanceModule = new CharacterTestGuidanceLevelEditorModule();
                guidanceModule.SetDocumentPathForTests(documentPath);
                guidanceModule.OnEnable();
                guidanceModule.MutateForTests(
                    "修改标题",
                    document => document.Steps[0].Content.Title.Fallback = "事务内新标题");

                var tailFailure = new GuidanceFailingSaveModule(
                    failingDocumentPath,
                    manager.transform,
                    () =>
                    {
                        MxLevelAuthoringDocument written = MxLevelAuthoringJson.LoadFromJson(
                            File.ReadAllText(ToFullPath(documentPath)));
                        Assert.That(written.TryGetModule("test.sibling", out MxLevelAuthoringModuleDocument sibling), Is.True);
                        Assert.That(sibling.Payload.Value<string>("marker"), Is.EqualTo("keep-me"));
                        throw new IOException("simulated guidance tail failure");
                    });

                bool saved = MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                    new IMxLevelEditorModule[] { guidanceModule, tailFailure },
                    out string message);

                Assert.That(saved, Is.False);
                Assert.That(message, Does.Contain("simulated guidance tail failure"));
                Assert.That(File.ReadAllText(ToFullPath(documentPath)), Is.EqualTo(originalJson));
                Assert.That(manager.GuidanceSceneCache.GuidanceModuleSourceHash, Is.EqualTo(originalPlan.SourceHash));
                Assert.That(guidanceModule.IsDirty, Is.True);
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);
                if (testScene.IsValid() && testScene.isLoaded)
                {
                    if (SceneManager.sceneCount > 1)
                        EditorSceneManager.CloseScene(testScene, true);
                    else
                        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                }
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [UnityTest]
        public IEnumerator FormalCharacterTestScene_ConsumesCacheAndCompletesAuthoredSixStepSequence()
        {
            EditorSceneManager.OpenScene(
                CharacterTestGuidanceAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);
            yield return new EnterPlayMode();

            GameManager manager = null;
            for (int i = 0; i < 120 && manager == null; i++)
            {
                manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                if (manager == null || manager.RuntimeContext?.UiRuntime == null)
                {
                    manager = null;
                    yield return null;
                }
            }

            bool managerFound = manager != null;
            bool cacheCompiled = false;
            bool hostConfigured = false;
            bool hostUsesFirstAuthoredStep = false;
            bool hostNavigationWorks = false;
            bool bindingRevisionReprojectsCurrentStep = false;
            bool localeRevisionReprojectsCurrentStep = false;
            bool sequenceCompleted = false;
            string issue = string.Empty;
            string bindingIssue = string.Empty;
            string localeIssue = string.Empty;
            if (manager != null)
            {
                cacheCompiled = manager.GuidanceSceneCache.TryCompilePlan(
                    out CharacterTestGuidancePlan plan,
                    out CharacterTestGuidanceRuntimeDiagnostic diagnostic);
                issue = diagnostic.Message;
                CharacterTestFairyGuiGuidanceHost host =
                    manager.RuntimeContext?.UiRuntime?.FairyGuiGuidanceHost;
                hostConfigured = host?.IsConfigured == true;
                if (cacheCompiled && host != null)
                {
                    CharacterTestGuidanceSelectionSnapshot snapshot = host.SelectionSnapshot;
                    hostUsesFirstAuthoredStep = snapshot.StepCount == plan.StepCount
                        && snapshot.SelectedStepIndex == 0
                        && string.Equals(
                            snapshot.SelectedStableId,
                            plan.Steps[0].StableId,
                            StringComparison.Ordinal);

                    bool selectedNext = host.SelectNextTest();
                    CharacterTestGuidanceSelectionSnapshot nextSnapshot = host.SelectionSnapshot;
                    bool selectedSecondAuthoredStep = selectedNext
                        && nextSnapshot.SelectedStepIndex == 1
                        && string.Equals(
                            nextSnapshot.SelectedStableId,
                            plan.Steps[1].StableId,
                            StringComparison.Ordinal);
                    bindingRevisionReprojectsCurrentStep =
                        TryVerifyBindingRevisionReprojection(
                            manager,
                            host,
                            plan.Steps[1],
                            in nextSnapshot,
                            out bindingIssue);
                    localeRevisionReprojectsCurrentStep =
                        TryVerifyLocaleRevisionReprojection(
                            manager,
                            host,
                            in nextSnapshot,
                            out localeIssue);

                    bool selectedPrevious = host.SelectPreviousTest();
                    CharacterTestGuidanceSelectionSnapshot previousSnapshot = host.SelectionSnapshot;
                    hostNavigationWorks = selectedSecondAuthoredStep
                        && selectedPrevious
                        && previousSnapshot.SelectedStepIndex == 0
                        && string.Equals(
                            previousSnapshot.SelectedStableId,
                            plan.Steps[0].StableId,
                            StringComparison.Ordinal);
                    sequenceCompleted = TryCompleteFormalSixStepPlan(plan, out issue);
                }
            }

            yield return new ExitPlayMode();

            Assert.That(managerFound, Is.True, "Formal CharacterTest scene did not create GameManager.");
            Assert.That(cacheCompiled, Is.True, issue);
            Assert.That(hostConfigured, Is.True, "Production Guidance Host was not configured from scene cache.");
            Assert.That(hostUsesFirstAuthoredStep, Is.True, "Host selection did not start from authored array order.");
            Assert.That(hostNavigationWorks, Is.True, "Production Host navigation did not follow authored array order.");
            Assert.That(bindingRevisionReprojectsCurrentStep, Is.True, bindingIssue);
            Assert.That(localeRevisionReprojectsCurrentStep, Is.True, localeIssue);
            Assert.That(sequenceCompleted, Is.True, issue);
        }

        private static bool TryVerifyLocaleRevisionReprojection(
            GameManager manager,
            CharacterTestFairyGuiGuidanceHost host,
            in CharacterTestGuidanceSelectionSnapshot selectionBefore,
            out string issue)
        {
            issue = string.Empty;
            CharacterTestLocalizationRuntime localization = manager.RuntimeContext?.LocalizationRuntime;
            if (localization == null)
            {
                issue = "Formal CharacterTest scene did not create LocalizationRuntime.";
                return false;
            }
            object presenter = host?.Presenter;
            if (!TryReadPresentedCard(presenter, out MxUiGuidanceCardSnapshot zhCard))
            {
                issue = "Production Guidance presenter did not expose the zh-CN card state.";
                return false;
            }

            localization.TextProvider.SetLocale(new MxUiLocaleId("en-US"));
            host.TickPresentation(0f);
            CharacterTestGuidanceSelectionSnapshot selectionAfter = host.SelectionSnapshot;
            bool englishProjected = TryReadPresentedCard(
                    presenter,
                    out MxUiGuidanceCardSnapshot enCard)
                && enCard.Title.FallbackText.StartsWith("Test 2/6: ", StringComparison.Ordinal)
                && enCard.ActionLine.Body.FallbackText.Contains("Jump and land back on the ground")
                && enCard.ActionLine.Body.FallbackText.Contains("Previous")
                && enCard.ActionLine.Body.FallbackText.Contains("Next");
            bool selectionPreserved = selectionAfter.SelectedStepIndex == selectionBefore.SelectedStepIndex
                && string.Equals(
                    selectionAfter.SelectedStableId,
                    selectionBefore.SelectedStableId,
                    StringComparison.Ordinal)
                && selectionAfter.Completed == selectionBefore.Completed;

            localization.TextProvider.SetLocale(new MxUiLocaleId("zh-CN"));
            host.TickPresentation(0f);
            bool sourceRestored = TryReadPresentedCard(
                    presenter,
                    out MxUiGuidanceCardSnapshot restored)
                && restored.Title.FallbackText.StartsWith("测试 2/6：", StringComparison.Ordinal)
                && restored.ActionLine.Body.FallbackText.Contains("跳起并落回地面");
            if (!englishProjected || !sourceRestored || !selectionPreserved)
            {
                issue = "Locale revision did not reproject the current Guidance card in place.";
                return false;
            }
            return true;
        }

        private static bool TryVerifyBindingRevisionReprojection(
            GameManager manager,
            CharacterTestFairyGuiGuidanceHost host,
            CharacterTestGuidanceStepDefinition selectedStep,
            in CharacterTestGuidanceSelectionSnapshot selectionBefore,
            out string issue)
        {
            issue = string.Empty;
            if (!(manager.RuntimeContext?.InputProvider is MxFramework.Input.InputService inputService))
            {
                issue = "Formal CharacterTest scene did not use InputService.";
                return false;
            }
            if (!TryReadPresentedActionPrompt(host, out string promptBefore))
            {
                issue = "Production Guidance presenter did not expose the selected card state.";
                return false;
            }

            var action = inputService.Actions?.FindAction(selectedStep.InputActionId, false);
            if (action == null || action.bindings.Count == 0)
            {
                issue = "Authored action has no binding at index 0: " + selectedStep.InputActionId;
                return false;
            }

            const string replacementPath = "<Keyboard>/j";
            string originalPath = action.bindings[0].path;
            long revisionBefore = inputService.BindingDisplay.Revision;
            if (!inputService.TryChangeBindingPath(
                    selectedStep.InputActionId,
                    0,
                    replacementPath,
                    originalPath))
            {
                issue = "Could not change the selected action's runtime binding path.";
                return false;
            }

            host.RefreshFromRuntime();
            CharacterTestGuidanceSelectionSnapshot selectionAfter = host.SelectionSnapshot;
            bool promptChanged = TryReadPresentedActionPrompt(host, out string promptAfter)
                && !string.Equals(promptBefore, promptAfter, StringComparison.Ordinal);
            bool revisionChanged = inputService.BindingDisplay.Revision > revisionBefore;
            bool selectionPreserved = selectionAfter.SelectedStepIndex == selectionBefore.SelectedStepIndex
                && string.Equals(
                    selectionAfter.SelectedStableId,
                    selectionBefore.SelectedStableId,
                    StringComparison.Ordinal)
                && selectionAfter.Completed == selectionBefore.Completed;

            bool restored = inputService.TryChangeBindingPath(
                selectedStep.InputActionId,
                0,
                originalPath,
                replacementPath);
            host.RefreshFromRuntime();
            if (!restored)
            {
                issue = "Runtime binding changed for verification but could not be restored.";
                return false;
            }
            if (!revisionChanged || !promptChanged || !selectionPreserved)
            {
                issue = "Binding revision did not reproject the current card while preserving selection state.";
                return false;
            }

            return true;
        }

        private static bool TryReadPresentedActionPrompt(
            CharacterTestFairyGuiGuidanceHost host,
            out string prompt)
        {
            prompt = string.Empty;
            object presenter = host?.Presenter;
            if (!TryReadPresentedCard(presenter, out MxUiGuidanceCardSnapshot card))
                return false;
            prompt = card.ActionLine.Body.FallbackText;
            return !string.IsNullOrWhiteSpace(prompt);
        }

        private static bool TryReadPresentedCard(
            object presenter,
            out MxUiGuidanceCardSnapshot card)
        {
            FieldInfo stateField = presenter?.GetType().GetField(
                "_state",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (!(stateField?.GetValue(presenter) is MxUiGuidanceState state) || !state.HasCard)
            {
                card = default;
                return false;
            }
            card = state.Card;
            return true;
        }

        private static bool TryCompleteFormalSixStepPlan(
            CharacterTestGuidancePlan plan,
            out string issue)
        {
            issue = string.Empty;
            if (plan == null || plan.StepCount != 6)
            {
                issue = "Formal Guidance plan must contain exactly six enabled steps.";
                return false;
            }

            var events = new List<CharacterTestRuntimeEvent>();
            var damageRows = new List<DamageTelemetryRow>();
            CharacterTestGuidanceObservation observation = RuntimeObservation(
                x: 0,
                grounded: true,
                targetLocked: false);
            CharacterTestGuidanceObservationContext context =
                new CharacterTestGuidanceObservationContext(events, damageRows, in observation);
            var coordinator = new CharacterTestGuidanceSequenceCoordinator(plan);
            coordinator.Prime(in context);

            if (!CurrentStepIs(coordinator, "basic-movement", out issue))
                return false;
            observation = RuntimeObservation(x: 2100, speed: 1000, grounded: true);
            context = new CharacterTestGuidanceObservationContext(events, damageRows, in observation);
            coordinator.TryObserve(in context, out _);
            observation = RuntimeObservation(x: 2100, speed: 0, grounded: true);
            context = new CharacterTestGuidanceObservationContext(events, damageRows, in observation);
            if (!CompleteAndAdvance(coordinator, in context, out issue))
                return false;

            if (!CurrentStepIs(coordinator, "jump-and-land", out issue))
                return false;
            observation = RuntimeObservation(x: 2100, grounded: false);
            context = new CharacterTestGuidanceObservationContext(events, damageRows, in observation);
            coordinator.TryObserve(in context, out _);
            observation = RuntimeObservation(x: 2100, grounded: true);
            context = new CharacterTestGuidanceObservationContext(events, damageRows, in observation);
            if (!CompleteAndAdvance(coordinator, in context, out issue))
                return false;

            if (!CurrentStepIs(coordinator, "camera-mode-switch", out issue))
                return false;
            events.Add(new CharacterTestRuntimeEvent(
                new RuntimeFrame(101),
                CharacterTestRuntimeEventType.CameraModeChanged,
                CharacterTestRuntimeCommandIds.SetCameraMode,
                sourceId: 1,
                targetId: 1));
            context = new CharacterTestGuidanceObservationContext(events, damageRows, in observation);
            if (!CompleteAndAdvance(coordinator, in context, out issue))
                return false;

            if (!CurrentStepIs(coordinator, "target-lock", out issue))
                return false;
            events.Add(new CharacterTestRuntimeEvent(
                new RuntimeFrame(102),
                CharacterTestRuntimeEventType.TargetLockChanged,
                CharacterTestRuntimeCommandIds.SetTargetLock,
                sourceId: 1,
                targetId: 9,
                entityId: 9));
            observation = RuntimeObservation(x: 2100, grounded: true, targetLocked: true);
            context = new CharacterTestGuidanceObservationContext(events, damageRows, in observation);
            if (!CompleteAndAdvance(coordinator, in context, out issue))
                return false;

            if (!CurrentStepIs(coordinator, "target-unlock", out issue))
                return false;
            events.Add(new CharacterTestRuntimeEvent(
                new RuntimeFrame(103),
                CharacterTestRuntimeEventType.TargetLockChanged,
                CharacterTestRuntimeCommandIds.SetTargetLock,
                sourceId: 1,
                targetId: 0,
                entityId: 0));
            observation = RuntimeObservation(x: 2100, grounded: true, targetLocked: false);
            context = new CharacterTestGuidanceObservationContext(events, damageRows, in observation);
            if (!CompleteAndAdvance(coordinator, in context, out issue))
                return false;

            if (!CurrentStepIs(coordinator, "basic-attack-hit", out issue))
                return false;
            damageRows.Add(CreateRuntimeDamageRow(104));
            context = new CharacterTestGuidanceObservationContext(events, damageRows, in observation);
            if (!coordinator.TryObserve(in context, out CharacterTestGuidanceCompletionOutcome finalOutcome)
                || finalOutcome.Kind != CharacterTestGuidanceCompletionOutcomeKind.Completed
                || !coordinator.Completed
                || coordinator.TryAdvance(100f, in context))
            {
                issue = "Final BasicAttackDamage step did not remain completed.";
                return false;
            }

            return coordinator.CreateSelectionSnapshot().SelectedStepIndex == plan.StepCount - 1;
        }

        private static bool CompleteAndAdvance(
            CharacterTestGuidanceSequenceCoordinator coordinator,
            in CharacterTestGuidanceObservationContext context,
            out string issue)
        {
            if (!coordinator.TryObserve(in context, out CharacterTestGuidanceCompletionOutcome outcome)
                || outcome.Kind != CharacterTestGuidanceCompletionOutcomeKind.Completed)
            {
                issue = "Step did not complete: " + coordinator.CurrentStep.StableId;
                return false;
            }
            if (!coordinator.TryAdvance(coordinator.CurrentStep.AutoAdvanceSeconds, in context))
            {
                issue = "Step did not auto-advance: " + coordinator.CurrentStep.StableId;
                return false;
            }
            issue = string.Empty;
            return true;
        }

        private static bool CurrentStepIs(
            CharacterTestGuidanceSequenceCoordinator coordinator,
            string stableId,
            out string issue)
        {
            if (string.Equals(coordinator.CurrentStep.StableId, stableId, StringComparison.Ordinal))
            {
                issue = string.Empty;
                return true;
            }
            issue = "Expected step " + stableId + " but selected "
                + coordinator.CurrentStep.StableId + ".";
            return false;
        }

        private static CharacterTestGuidanceObservation RuntimeObservation(
            int x,
            int speed = 0,
            bool grounded = true,
            bool targetLocked = false)
        {
            return new CharacterTestGuidanceObservation(
                true,
                7,
                x,
                0,
                speed,
                grounded,
                CharacterTestCameraMode.ThirdPersonControlled,
                targetLocked);
        }

        private static DamageTelemetryRow CreateRuntimeDamageRow(long frame)
        {
            return new DamageTelemetryRow(
                frame,
                DamageProfileSourceKind.WeaponTrace,
                new MxFramework.CharacterAction.CharacterActionId(301),
                actionInstanceId: (int)frame,
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0033,
                damageProfileId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0020,
                damageTypeId: CharacterTestAuthoredContentIds.D409_Id1,
                equipmentSlotId: CharacterTestAuthoredContentIds.D405_Id1,
                weaponId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0086,
                sourceEntityId: 7,
                targetEntityId: 9,
                hitZoneId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id55,
                bodyPartId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id10,
                localFrame: 4,
                incomingDamage: 6,
                finalDamage: 6,
                resultKind: HitResolveKind.Damage,
                hpBefore: 100,
                hpAfter: 94);
        }

        private static IReadOnlyList<CharacterTestGuidanceTextAuthoring> LocalizedTexts(
            CharacterTestGuidanceStepAuthoring step)
        {
            return new[]
            {
                step.Content.Title,
                step.Content.ActionInstruction,
                step.Content.Focus,
                step.Content.WaitingStatus,
                step.Content.CompletedStatus,
                step.Content.SuccessNotice
            };
        }

        private static MxLevelAuthoringDocument CreateEnvelopeWithSibling(
            CharacterTestGuidanceAuthoringDocument guidance)
        {
            var envelope = new MxLevelAuthoringDocument
            {
                LevelId = "guidance-test",
                ScenePath = CharacterTestGuidanceAuthoringAdapter.DefaultScenePath
            };
            envelope.UpsertModulePayload(
                "test.sibling",
                1,
                new JObject { ["marker"] = "keep-me" },
                "Sibling Test Module");
            return CharacterTestGuidanceAuthoringAdapter.Upsert(envelope, guidance);
        }

        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return Path.GetFullPath(Path.Combine(projectRoot ?? string.Empty, assetPath));
        }

        private sealed class GuidanceFailingSaveModule :
            IMxLevelEditorModule,
            IMxLevelEditorSceneChangeAwareModule,
            IMxLevelEditorSaveParticipant
        {
            private readonly Action _saveAction;

            internal GuidanceFailingSaveModule(
                string documentPath,
                Transform managedSceneRoot,
                Action saveAction)
            {
                DocumentPath = documentPath;
                ManagedSceneRoot = managedSceneRoot;
                _saveAction = saveAction;
            }

            public string ModuleId => "test.guidance.tail-failure";
            public string DisplayName => "Guidance Tail Failure";
            public string DocumentPath { get; }
            public bool IsDirty => true;
            public string Status => string.Empty;
            public Transform ManagedSceneRoot { get; }
            public void OnEnable() { }
            public void BuildModuleToolbar(MxLevelEditorWindow window, VisualElement toolbar) { }
            public VisualElement CreateContent(MxLevelEditorWindow window) => new VisualElement();
            public void OpenJson() { }
            public void Reload() { }
            public void Save() { }
            public void Validate() { }
            public void ApplyToScene() { }
            public void SelectRootSource() { }
            public void CollectManagedSceneTransforms(List<Transform> output) { }
            public bool NotifySceneTransformChanged(Transform transform) => false;
            public void RefreshUnregisteredSceneObjects(IReadOnlyList<Transform> allManagedTransforms) { }
            public void AcknowledgeManagedSceneSaved() { }

            public bool TryPrepareSave(out string failureMessage)
            {
                failureMessage = string.Empty;
                return true;
            }

            public void SavePreparedChanges()
            {
                _saveAction();
            }

            public void CompleteCoordinatedSave(bool succeeded, string message) { }
        }
    }
}

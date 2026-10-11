using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Physics;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerFormalizationTests
    {
        [Test]
        public void RuntimeAiPlannerEditorModel_DefaultTemplateValidates()
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(CharacterTestRuntimeAiPlannerProfileSource.CreateDefault());

            CharacterTestRuntimeAiPlannerValidationReport report = model.Validate();

            Assert.IsFalse(report.HasErrors, report.ToText());
        }

        [Test]
        public void RuntimeAiPlannerEditorFactCatalog_CoversRuntimeFactsAndTypedOperators()
        {
            Array values = Enum.GetValues(typeof(CharacterTestRuntimeAiPlannerFactKind));
            Assert.AreEqual(values.Length, CharacterTestRuntimeAiPlannerEditorFactCatalog.Count);

            foreach (object value in values)
            {
                CharacterTestRuntimeAiPlannerFactKind fact = (CharacterTestRuntimeAiPlannerFactKind)value;
                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerEditorFactCatalog.TryGet(
                        fact,
                        out CharacterTestRuntimeAiPlannerEditorFactMetadata metadata),
                    fact.ToString());
                Assert.IsNotEmpty(metadata.DisplayName);
                Assert.IsNotEmpty(metadata.Key);
            }

            CharacterTestRuntimeAiPlannerEditorFactMetadata visible =
                CharacterTestRuntimeAiPlannerEditorFactCatalog.Get(
                    CharacterTestRuntimeAiPlannerFactKind.TargetVisible);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerEditorFactValueType.Bool, visible.ValueType);
            Assert.IsTrue(CharacterTestRuntimeAiPlannerEditorFactCatalog.IsOperatorAllowed(
                visible,
                CharacterTestRuntimeAiPlannerConditionOperator.Equals));
            Assert.IsFalse(CharacterTestRuntimeAiPlannerEditorFactCatalog.IsOperatorAllowed(
                visible,
                CharacterTestRuntimeAiPlannerConditionOperator.GreaterOrEqual));

            CharacterTestRuntimeAiPlannerEditorFactMetadata distance =
                CharacterTestRuntimeAiPlannerEditorFactCatalog.Get(
                    CharacterTestRuntimeAiPlannerFactKind.TargetDistance);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerEditorFactValueType.Float, distance.ValueType);
            Assert.IsTrue(CharacterTestRuntimeAiPlannerEditorFactCatalog.IsOperatorAllowed(
                distance,
                CharacterTestRuntimeAiPlannerConditionOperator.GreaterOrEqual));

            CharacterTestRuntimeAiPlannerEditorFactMetadata surface =
                CharacterTestRuntimeAiPlannerEditorFactCatalog.Get(
                    CharacterTestRuntimeAiPlannerFactKind.SelfSurfaceKind);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerEditorFactValueType.SurfaceKind, surface.ValueType);
            Assert.IsFalse(CharacterTestRuntimeAiPlannerEditorFactCatalog.IsOperatorAllowed(
                surface,
                CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual));
        }

        [Test]
        public void RuntimeAiPlannerEditorFactCatalog_CommonTemplatesProduceTypedConditions()
        {
            var condition = new CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft();

            CharacterTestRuntimeAiPlannerEditorFactCatalog.ApplyTemplate(
                condition,
                CharacterTestRuntimeAiPlannerConditionTemplate.TargetRemembered,
                memoryFrames: 12);

            Assert.AreEqual(CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames, condition.Fact);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual, condition.Operator);
            Assert.AreEqual(12f, condition.FloatValue);

            CharacterTestRuntimeAiPlannerEditorFactCatalog.ApplyTemplate(
                condition,
                CharacterTestRuntimeAiPlannerConditionTemplate.ActionNotRunning,
                memoryFrames: 12);

            Assert.AreEqual(CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning, condition.Fact);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerConditionOperator.Equals, condition.Operator);
            Assert.IsFalse(condition.BoolValue);
        }

        [Test]
        public void RuntimeAiPlannerEditorModel_PerceptionAwareMeleeTemplateValidates()
        {
            CharacterTestRuntimeAiPlannerProfile profile =
                CharacterTestRuntimeAiPlannerEditorModel.CreatePerceptionAwareMeleeTemplate();
            CharacterTestRuntimeAiPlannerValidationReport report =
                CharacterTestRuntimeAiPlannerProfileValidator.Validate(profile);

            Assert.IsFalse(report.HasErrors, report.ToText());
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerEditorModel.PerceptionAwareMeleeProfileId,
                profile.ProfileId);
            Assert.That(
                profile.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => HasCondition(action, CharacterTestRuntimeAiPlannerFactKind.TargetVisible)));
            Assert.That(
                profile.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => HasCondition(action, CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked)));
            Assert.That(
                profile.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => HasCondition(action, CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard)));
        }

        [Test]
        public void RuntimeAiPlannerTemplateLibrary_AllTemplatesRoundTripAndValidate()
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerTemplateDescriptor> templates =
                CharacterTestRuntimeAiPlannerTemplateLibrary.Templates;
            Assert.GreaterOrEqual(templates.Count, 9);

            for (int i = 0; i < templates.Count; i++)
            {
                CharacterTestRuntimeAiPlannerProfile source =
                    CharacterTestRuntimeAiPlannerTemplateLibrary.CreateTemplate(templates[i].TemplateId);
                string json = CharacterTestRuntimeAiPlannerProfileJson.ToJson(source);

                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerProfileJson.TryLoad(
                        json,
                        out CharacterTestRuntimeAiPlannerProfile loaded,
                        out CharacterTestRuntimeAiPlannerValidationReport report),
                    report.ToText());
                Assert.AreEqual(source.ProfileId, loaded.ProfileId);
                Assert.IsFalse(report.HasErrors, report.ToText());
            }
        }

        [Test]
        public void RuntimeAiPlannerTemplateLibrary_NewProfileFilesLoadAndValidate()
        {
            string[] paths =
            {
                CharacterTestRuntimeAiPlannerTemplateLibrary.RangedKiterProjectAssetPath,
                CharacterTestRuntimeAiPlannerTemplateLibrary.HazardSkirmisherProjectAssetPath,
                CharacterTestRuntimeAiPlannerTemplateLibrary.SkillCasterProjectAssetPath,
                CharacterTestRuntimeAiPlannerTemplateLibrary.AggressiveBerserkerProjectAssetPath,
                CharacterTestRuntimeAiPlannerTemplateLibrary.DefensiveGuardianProjectAssetPath,
                CharacterTestRuntimeAiPlannerTemplateLibrary.AmbusherProjectAssetPath,
                CharacterTestRuntimeAiPlannerTemplateLibrary.ControllerZonerProjectAssetPath
            };

            for (int i = 0; i < paths.Length; i++)
            {
                string fullPath = Path.Combine(Directory.GetCurrentDirectory(), paths[i]);
                Assert.IsTrue(File.Exists(fullPath), "Missing Runtime AI Planner JSON: " + paths[i]);
                string json = File.ReadAllText(fullPath);

                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerProfileJson.TryLoad(
                        json,
                        out CharacterTestRuntimeAiPlannerProfile profile,
                        out CharacterTestRuntimeAiPlannerValidationReport report),
                    report.ToText());
                Assert.IsFalse(report.HasErrors, report.ToText());
                Assert.IsNotEmpty(profile.States);
                Assert.IsNotEmpty(profile.Actions);
            }
        }

        [Test]
        public void RuntimeAiPlannerTemplateLibrary_NewProfilesUseExpectedOutputs()
        {
            CharacterTestRuntimeAiPlannerProfile ranged =
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateRangedKiterTemplate();
            CharacterTestRuntimeAiPlannerProfile hazard =
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateHazardSkirmisherTemplate();
            CharacterTestRuntimeAiPlannerProfile caster =
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateSkillCasterTemplate();
            CharacterTestRuntimeAiPlannerProfile berserker =
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateAggressiveBerserkerTemplate();
            CharacterTestRuntimeAiPlannerProfile guardian =
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateDefensiveGuardianTemplate();
            CharacterTestRuntimeAiPlannerProfile ambusher =
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateAmbusherTemplate();
            CharacterTestRuntimeAiPlannerProfile controller =
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateControllerZonerTemplate();

            Assert.That(
                ranged.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => action.Kind == CharacterTestRuntimeAiPlannerActionKind.Retreat));
            Assert.That(
                ranged.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => action.Kind == CharacterTestRuntimeAiPlannerActionKind.Strafe));
            Assert.That(
                ranged.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => action.Kind == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot
                        && action.AbilitySlot >= 1
                        && action.AbilitySlot <= 9));
            Assert.That(
                hazard.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => action.Kind == CharacterTestRuntimeAiPlannerActionKind.Retreat
                        && HasCondition(action, CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard)));
            Assert.That(
                caster.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => action.Kind == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot
                        && action.AbilitySlot >= 1
                        && action.AbilitySlot <= 9
                        && HasCondition(action, CharacterTestRuntimeAiPlannerFactKind.TargetVisible)));
            Assert.That(
                berserker.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => action.Kind == CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack));
            Assert.That(
                berserker.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => action.Kind == CharacterTestRuntimeAiPlannerActionKind.MoveToTarget));
            Assert.That(
                guardian.Transitions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerTransitionDefinition>(
                    transition => transition.Condition.Fact
                        == CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject));
            Assert.That(
                guardian.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => action.Kind == CharacterTestRuntimeAiPlannerActionKind.Retreat));
            Assert.That(
                ambusher.Transitions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerTransitionDefinition>(
                    transition => transition.Condition.Fact
                        == CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked));
            Assert.That(
                ambusher.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => action.Kind == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot
                        && action.AbilitySlot == 2));
            Assert.That(
                controller.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => action.Kind == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot
                        && action.AbilitySlot == 3));
            Assert.That(
                controller.Actions,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerActionDefinition>(
                    action => action.Kind == CharacterTestRuntimeAiPlannerActionKind.Retreat));
        }

        [Test]
        public void RuntimeAiPlannerEditorModel_StateOperationsUpdateReferences()
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(CharacterTestRuntimeAiPlannerProfileSource.CreateDefault());
            CharacterTestRuntimeAiPlannerEditorModel.StateDraft state =
                model.AddChildState(CharacterTestRuntimeAiPlannerProfileSource.EngageStateId.ToString(CultureInfo.InvariantCulture));
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition =
                model.AddTransition(CharacterTestRuntimeAiPlannerProfileSource.ChaseStateId.ToString(CultureInfo.InvariantCulture));
            transition.TargetStateId = state.StateId;
            CharacterTestRuntimeAiPlannerEditorModel.ActionDraft action =
                model.AddAction(state.StateId);

            Assert.IsTrue(model.RenameState(state.StateId, "evade"));

            Assert.AreEqual("evade", transition.TargetStateId);
            Assert.AreEqual("evade", action.StateId);

            Assert.IsTrue(model.DeleteState("evade"));

            for (int i = 0; i < model.Transitions.Count; i++)
            {
                Assert.AreNotEqual("evade", model.Transitions[i].SourceStateId);
                Assert.AreNotEqual("evade", model.Transitions[i].TargetStateId);
            }

            CharacterTestRuntimeAiPlannerValidationReport report = model.Validate();
            AssertHasDiagnostic(report, "action.state.missing");
        }

        [Test]
        public void RuntimeAiPlannerEditorModel_CastAbilitySlotRequiresSlotOnlyForSlotActions()
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(new CharacterTestRuntimeAiPlannerProfile(
                initialStateId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060,
                states: new[]
                {
                    new CharacterTestRuntimeAiPlannerStateDefinition(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060)
                },
                actions: new[]
                {
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        1,
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060,
                        CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot,
                        1f,
                        0f)
                }));

            CharacterTestRuntimeAiPlannerValidationReport report = model.Validate();
            AssertHasDiagnostic(report, "action.output.slot.missing");

            model.Actions[0].AbilitySlot = 1;
            report = model.Validate();
            AssertNoDiagnostic(report, "action.output.slot.missing");

            model.Actions[0].Kind = CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack;
            model.Actions[0].AbilitySlot = 0;
            report = model.Validate();
            AssertNoDiagnostic(report, "action.output.slot.missing");
        }

        [Test]
        public void RuntimeAiPlannerWindow_CopyReportWritesNonEmptyClipboard()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();
                EditorGUIUtility.systemCopyBuffer = string.Empty;
                System.Reflection.MethodInfo method =
                    typeof(CharacterTestRuntimeAiPlannerWindow).GetMethod(
                        "CopyReport",
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(method);

                method.Invoke(window, null);

                Assert.IsNotEmpty(EditorGUIUtility.systemCopyBuffer);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_ConditionEditorShowsFactTemplates()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();
                MethodInfo method = typeof(CharacterTestRuntimeAiPlannerWindow).GetMethod(
                    "CreateSmartConditionEditor",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(method);
                var condition = new CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft();
                var editor = (VisualElement)method.Invoke(
                    window,
                    new object[] { condition, (Action)(() => { }) });

                AssertTextPresent(editor, "条件模板");
                AssertTextPresent(editor, "目标可见");
                AssertTextPresent(editor, "目标存在");
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_ConstrainsToolbarAndListRows()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();

                VisualElement toolbar = window.rootVisualElement.Q("runtime-ai-planner-toolbar");
                Assert.NotNull(toolbar);
                Assert.NotNull(window.rootVisualElement.Q("runtime-ai-planner-toolbar-profile-row"));
                Foldout advancedJson =
                    window.rootVisualElement.Q<Foldout>("runtime-ai-planner-toolbar-advanced-json");
                Assert.NotNull(advancedJson);
                Assert.IsFalse(advancedJson.value);
                VisualElement commandRow =
                    window.rootVisualElement.Q("runtime-ai-planner-toolbar-command-row");
                Assert.NotNull(commandRow);
                Assert.AreEqual(Wrap.Wrap, commandRow.style.flexWrap.value);
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.TemplateMenuName));

                MethodInfo method = typeof(CharacterTestRuntimeAiPlannerWindow).GetMethod(
                    "CreateTransitionRow",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(method);
                var row = (Button)method.Invoke(window, new object[] { 0, null });

                Assert.AreEqual(Overflow.Hidden, row.style.overflow.value);
                Assert.AreEqual(WhiteSpace.NoWrap, row.style.whiteSpace.value);
                Assert.AreEqual(TextOverflow.Ellipsis, row.style.textOverflow.value);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_StateTabShowsFlowPreview()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();

                VisualElement preview =
                    window.rootVisualElement.Q(CharacterTestRuntimeAiPlannerFlowPreview.RootName);
                Assert.NotNull(preview);
                AssertTextPresent(preview, "行为流程预览");
                Assert.NotNull(preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.GlobalNodeName));
                Assert.NotNull(preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.ViewportName));
                Assert.Null(preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.ScrollName));
                Assert.NotNull(preview.Q<Button>(CharacterTestRuntimeAiPlannerFlowPreview.ZoomOutButtonName));
                Assert.NotNull(preview.Q<Button>(CharacterTestRuntimeAiPlannerFlowPreview.ZoomInButtonName));
                Assert.NotNull(preview.Q<Button>(CharacterTestRuntimeAiPlannerFlowPreview.ResetZoomButtonName));
                Assert.NotNull(preview.Q<Button>(CharacterTestRuntimeAiPlannerFlowPreview.FitZoomButtonName));
                Assert.Null(preview.Q<Button>(CharacterTestRuntimeAiPlannerFlowPreview.ExpandButtonName));
                Assert.NotNull(window.rootVisualElement.Q<Button>(
                    CharacterTestRuntimeAiPlannerFlowPreview.ExpandButtonName));
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_FlowPreviewZoomControlsChangeScale()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();

                float before = GetWindowField<float>(window, "_flowPreviewZoom");
                Button zoomIn = window.rootVisualElement.Q<Button>(
                    CharacterTestRuntimeAiPlannerFlowPreview.ZoomInButtonName);
                Assert.NotNull(zoomIn);
                PressButton(zoomIn);

                Assert.Greater(GetWindowField<float>(window, "_flowPreviewZoom"), before);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_FlowPreviewExpandButtonOpensDetailWindow()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            CharacterTestRuntimeAiPlannerFlowPreviewWindow detailWindow = null;
            try
            {
                window.CreateGUI();

                Button expand = window.rootVisualElement.Q<Button>(
                    CharacterTestRuntimeAiPlannerFlowPreview.ExpandButtonName);
                Assert.NotNull(expand);
                PressButton(expand);

                CharacterTestRuntimeAiPlannerFlowPreviewWindow[] windows =
                    UnityEngine.Resources.FindObjectsOfTypeAll<CharacterTestRuntimeAiPlannerFlowPreviewWindow>();
                Assert.Greater(windows.Length, 0);
                detailWindow = windows[0];
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom,
                    GetWindowField<float>(detailWindow, "_zoom"));
                Assert.NotNull(detailWindow.rootVisualElement.Q<ScrollView>(
                    CharacterTestRuntimeAiPlannerFlowPreview.ScrollName));
                Assert.NotNull(detailWindow.rootVisualElement.Q<Button>(
                    CharacterTestRuntimeAiPlannerFlowPreview.CenterButtonName));
                Assert.NotNull(detailWindow.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerFlowPreview.GlobalNodeName));
            }
            finally
            {
                if (detailWindow != null)
                    detailWindow.Close();
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_BehaviorGroupEditTabKeepsEditorsAndHidesFlowPreview()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();
                SetWindowField(window, "_activeTab", 4);
                InvokeWindowMethod(window, "RefreshView");

                Assert.Null(window.rootVisualElement.Q(CharacterTestRuntimeAiPlannerFlowPreview.RootName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorGroupGeneratedActionStripName));
                AssertTextPresent(window.rootVisualElement, "行为组编辑");
                AssertTextPresent(window.rootVisualElement, "行为定义");
                AssertTextPresent(window.rootVisualElement, "行为实例");
                AssertTextPresent(window.rootVisualElement, "查看预览");
                AssertTextPresent(window.rootVisualElement, "JSON");
                AssertTextPresent(window.rootVisualElement, "编译覆盖底层 Profile 草稿");
                AssertTextPresent(window.rootVisualElement, "应用到 Play Mode");
                AssertTextAbsent(window.rootVisualElement, "Flow preview");
                AssertTextAbsent(window.rootVisualElement, "组内行为执行结果");
            }
            finally
            {
                window.Close();
            }
        }

    }
}

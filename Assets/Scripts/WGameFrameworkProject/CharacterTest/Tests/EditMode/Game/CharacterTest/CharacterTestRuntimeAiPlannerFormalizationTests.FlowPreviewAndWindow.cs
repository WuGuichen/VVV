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
        public void RuntimeAiPlannerFlowPreview_PerceptionTemplateShowsStateFlow()
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(CharacterTestRuntimeAiPlannerEditorModel.CreatePerceptionAwareMeleeTemplate());

            var preview = new CharacterTestRuntimeAiPlannerFlowPreview(
                model,
                model.Validate(),
                model.InitialStateId,
                -1,
                _ => { },
                _ => { });

            Assert.NotNull(preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.GlobalNodeName));
            AssertTextPresent(preview, "observe");
            AssertTextPresent(preview, "chase");
            AssertTextPresent(preview, "attack");
            AssertTextPresent(preview, "recover");
            AssertLayoutIsClear(preview.CreateDebugLayoutSnapshotForTests(1f, 900f));
        }

        [Test]
        public void RuntimeAiPlannerFlowPreview_OverviewFitsDefaultProfile()
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(CharacterTestRuntimeAiPlannerProfileSource.CreateDefault());
            const float viewportWidth = 640f;

            var preview = new CharacterTestRuntimeAiPlannerFlowPreview(
                model,
                model.Validate(),
                model.InitialStateId,
                -1,
                _ => { },
                _ => { },
                CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview,
                viewportWidth);

            VisualElement canvas = preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.CanvasName);
            Assert.NotNull(canvas);
            Assert.LessOrEqual(canvas.style.width.value.value, viewportWidth);
            Assert.NotNull(preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.GlobalNodeName));
            Assert.NotNull(preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.StateNodeName("root")));
            Assert.NotNull(preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.StateNodeName("engage")));
            Assert.NotNull(preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.StateNodeName("chase")));
            Assert.NotNull(preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.StateNodeName("attack")));
        }

        [Test]
        public void RuntimeAiPlannerFlowPreview_DetailModeKeepsScrollView()
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(CharacterTestRuntimeAiPlannerProfileSource.CreateDefault());

            var preview = new CharacterTestRuntimeAiPlannerFlowPreview(
                model,
                model.Validate(),
                model.InitialStateId,
                -1,
                _ => { },
                _ => { },
                CharacterTestRuntimeAiPlannerFlowPreviewMode.Detail,
                640f,
                1f);

            Assert.NotNull(preview.Q<ScrollView>(CharacterTestRuntimeAiPlannerFlowPreview.ScrollName));
            Assert.Null(preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.ViewportName));
            Assert.NotNull(preview.Q<Button>(CharacterTestRuntimeAiPlannerFlowPreview.CenterButtonName));
        }

        [Test]
        public void RuntimeAiPlannerFlowPreview_DetailFitFillsViewportAndCentersContent()
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(CharacterTestRuntimeAiPlannerProfileSource.CreateDefault());
            const float viewportWidth = 1800f;
            const float viewportHeight = 900f;

            var preview = new CharacterTestRuntimeAiPlannerFlowPreview(
                model,
                model.Validate(),
                model.InitialStateId,
                -1,
                _ => { },
                _ => { },
                CharacterTestRuntimeAiPlannerFlowPreviewMode.Detail,
                viewportWidth,
                CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom,
                null,
                null,
                viewportHeight);

            VisualElement canvas = preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.CanvasName);
            VisualElement global = preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.GlobalNodeName);
            VisualElement chase = preview.Q(
                CharacterTestRuntimeAiPlannerFlowPreview.StateNodeName(
                    CharacterTestRuntimeAiPlannerProfileSource.ChaseStateId.ToString(CultureInfo.InvariantCulture)));

            Assert.NotNull(canvas);
            Assert.NotNull(global);
            Assert.NotNull(chase);
            Assert.GreaterOrEqual(canvas.style.width.value.value, viewportWidth);
            Assert.GreaterOrEqual(canvas.style.height.value.value, viewportHeight);
            Assert.Less(
                chase.style.left.value.value + chase.style.width.value.value,
                viewportWidth);
        }

        [Test]
        public void RuntimeAiPlannerFlowPreview_PerceptionTemplateUsesCompactRowsAndLegend()
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(CharacterTestRuntimeAiPlannerEditorModel.CreatePerceptionAwareMeleeTemplate());

            var preview = new CharacterTestRuntimeAiPlannerFlowPreview(
                model,
                model.Validate(),
                model.InitialStateId,
                -1,
                _ => { },
                _ => { },
                CharacterTestRuntimeAiPlannerFlowPreviewMode.Detail,
                1800f,
                CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom,
                null,
                null,
                900f);

            CharacterTestRuntimeAiPlannerFlowPreview.FlowGraphLayoutDebugSnapshot snapshot =
                preview.CreateDebugLayoutSnapshotForTests(1f, 900f);
            AssertLayoutIsClear(snapshot);
            AssertGlobalLegendNotOccupied(snapshot);

            Rect observe = GetNodeRect(snapshot, "observe");
            Rect chase = GetNodeRect(
                snapshot,
                CharacterTestRuntimeAiPlannerProfileSource.ChaseStateId.ToString(CultureInfo.InvariantCulture));
            Rect attack = GetNodeRect(
                snapshot,
                CharacterTestRuntimeAiPlannerProfileSource.AttackStateId.ToString(CultureInfo.InvariantCulture));
            Rect recover = GetNodeRect(snapshot, "recover");

            Assert.Less(chase.yMin - observe.yMin, 190f);
            Assert.Less(attack.yMin - chase.yMin, 190f);
            Assert.Less(recover.yMin - attack.yMin, 190f);
        }

        [Test]
        public void RuntimeAiPlannerFlowPreview_NewBehaviorTemplatesRenderClearLayouts()
        {
            CharacterTestRuntimeAiPlannerProfile[] profiles =
            {
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateRangedKiterTemplate(),
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateHazardSkirmisherTemplate(),
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateSkillCasterTemplate(),
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateAggressiveBerserkerTemplate(),
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateDefensiveGuardianTemplate(),
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateAmbusherTemplate(),
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateControllerZonerTemplate()
            };

            for (int i = 0; i < profiles.Length; i++)
            {
                var model = new CharacterTestRuntimeAiPlannerEditorModel();
                model.Load(profiles[i]);

                var preview = new CharacterTestRuntimeAiPlannerFlowPreview(
                    model,
                    model.Validate(),
                    model.InitialStateId,
                    -1,
                    _ => { },
                    _ => { },
                    CharacterTestRuntimeAiPlannerFlowPreviewMode.Detail,
                    1800f,
                    CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom,
                    null,
                    null,
                    900f);

                CharacterTestRuntimeAiPlannerFlowPreview.FlowGraphLayoutDebugSnapshot snapshot =
                    preview.CreateDebugLayoutSnapshotForTests(1f, 900f);
                AssertLayoutIsClear(snapshot);
                AssertGlobalLegendNotOccupied(snapshot);
            }
        }

        [Test]
        public void RuntimeAiPlannerFlowPreview_GlobalCoverageWarningsAreVisible()
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(CharacterTestRuntimeAiPlannerEditorModel.CreatePerceptionAwareMeleeTemplate());
            int attackTransitionIndex = FindGlobalTransitionIndex(
                model,
                CharacterTestRuntimeAiPlannerProfileSource.AttackStateId.ToString(CultureInfo.InvariantCulture));

            Assert.IsTrue(CharacterTestRuntimeAiPlannerEditorRuleDiagnostics.TryCreateGlobalCoverageDiagnostic(
                model.Transitions,
                model.Transitions[attackTransitionIndex],
                out CharacterTestRuntimeAiPlannerDiagnostic diagnostic));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerEditorRuleDiagnostics.GlobalRuleMayBeCoveredCode,
                diagnostic.Code);

            var preview = new CharacterTestRuntimeAiPlannerFlowPreview(
                model,
                model.Validate(),
                model.InitialStateId,
                -1,
                _ => { },
                _ => { },
                CharacterTestRuntimeAiPlannerFlowPreviewMode.Detail,
                1800f,
                CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom,
                null,
                null,
                900f);

            Button rule = preview.Q<Button>(
                CharacterTestRuntimeAiPlannerFlowPreview.TransitionEdgeName(attackTransitionIndex));
            Assert.NotNull(rule);
            StringAssert.StartsWith("! 全局", rule.text);
            StringAssert.Contains(
                CharacterTestRuntimeAiPlannerEditorRuleDiagnostics.GlobalRuleMayBeCoveredCode,
                rule.tooltip);
        }

        [Test]
        public void RuntimeAiPlannerFlowPreview_DefaultProfileLayoutHasNoOverlapsOrBlockedEdges()
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(CharacterTestRuntimeAiPlannerProfileSource.CreateDefault());

            var preview = new CharacterTestRuntimeAiPlannerFlowPreview(
                model,
                model.Validate(),
                model.InitialStateId,
                -1,
                _ => { },
                _ => { },
                CharacterTestRuntimeAiPlannerFlowPreviewMode.Detail,
                1800f,
                CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom,
                null,
                null,
                900f);

            AssertLayoutIsClear(preview.CreateDebugLayoutSnapshotForTests(1f, 900f));
        }

        [Test]
        public void RuntimeAiPlannerFlowPreview_SourceTransitionIsNotRenderedAsGlobalRule()
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(CharacterTestRuntimeAiPlannerProfileSource.CreateDefault());
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition =
                model.AddTransition(CharacterTestRuntimeAiPlannerProfileSource.ChaseStateId.ToString(CultureInfo.InvariantCulture));
            transition.TargetStateId = CharacterTestRuntimeAiPlannerProfileSource.AttackStateId.ToString(CultureInfo.InvariantCulture);
            transition.DisplayName = "explicit";
            int transitionIndex = model.Transitions.Count - 1;

            var preview = new CharacterTestRuntimeAiPlannerFlowPreview(
                model,
                model.Validate(),
                model.InitialStateId,
                -1,
                _ => { },
                _ => { },
                CharacterTestRuntimeAiPlannerFlowPreviewMode.Detail,
                1800f,
                CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom,
                null,
                null,
                900f);

            Button label = preview.Q<Button>(
                CharacterTestRuntimeAiPlannerFlowPreview.TransitionEdgeName(transitionIndex));
            Assert.NotNull(label);
            StringAssert.DoesNotStartWith("全局", label.text);

            CharacterTestRuntimeAiPlannerFlowPreview.FlowGraphLayoutDebugSnapshot snapshot =
                preview.CreateDebugLayoutSnapshotForTests(1f, 900f);
            AssertLayoutIsClear(snapshot);
            Assert.AreEqual(
                2,
                CountTransitionEdges(snapshot, transitionIndex),
                "Explicit source transition should render source->rule and rule->target edges.");
        }

        [Test]
        public void RuntimeAiPlannerFlowPreview_MultipleSameTargetRulesDoNotOverlap()
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(CharacterTestRuntimeAiPlannerProfileSource.CreateDefault());
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft first =
                model.AddTransition(string.Empty);
            first.TargetStateId = CharacterTestRuntimeAiPlannerProfileSource.AttackStateId.ToString(CultureInfo.InvariantCulture);
            first.DisplayName = "same-target-a";
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft second =
                model.AddTransition(string.Empty);
            second.TargetStateId = CharacterTestRuntimeAiPlannerProfileSource.AttackStateId.ToString(CultureInfo.InvariantCulture);
            second.DisplayName = "same-target-b";

            var preview = new CharacterTestRuntimeAiPlannerFlowPreview(
                model,
                model.Validate(),
                model.InitialStateId,
                -1,
                _ => { },
                _ => { });

            AssertLayoutIsClear(preview.CreateDebugLayoutSnapshotForTests(1f, 900f));
        }

        [Test]
        public void RuntimeAiPlannerWindow_FlowStateClickSelectsStateInspector()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();

                Button node = window.rootVisualElement.Q<Button>(
                    CharacterTestRuntimeAiPlannerFlowPreview.StateNodeName(
                        CharacterTestRuntimeAiPlannerProfileSource.AttackStateId.ToString(CultureInfo.InvariantCulture)));
                Assert.NotNull(node);
                PressButton(node);

                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerProfileSource.AttackStateId,
                    GetWindowField<string>(window, "_selectedStateId"));
                Assert.AreEqual(-1, GetWindowField<int>(window, "_selectedTransitionIndex"));
                AssertTextPresent(window.rootVisualElement, "状态 Inspector");
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_FlowTransitionClickSelectsTransitionInspector()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();

                Button edge = window.rootVisualElement.Q<Button>(
                    CharacterTestRuntimeAiPlannerFlowPreview.TransitionEdgeName(0));
                Assert.NotNull(edge);
                PressButton(edge);

                Assert.AreEqual(0, GetWindowField<int>(window, "_selectedTransitionIndex"));
                AssertTextPresent(window.rootVisualElement, "切换 Inspector");
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerFlowPreview_MissingTransitionTargetShowsWarningNode()
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(CharacterTestRuntimeAiPlannerProfileSource.CreateDefault());
            Assert.Greater(model.Transitions.Count, 0);
            model.Transitions[0].TargetStateId = "missing_target";

            CharacterTestRuntimeAiPlannerValidationReport report = model.Validate();
            var preview = new CharacterTestRuntimeAiPlannerFlowPreview(
                model,
                report,
                model.InitialStateId,
                -1,
                _ => { },
                _ => { });

            Assert.NotNull(preview.Q(CharacterTestRuntimeAiPlannerFlowPreview.MissingTargetNodeName));
            AssertTextPresent(preview, "缺失目标");
            AssertHasDiagnostic(report, "transition.target.missing");
            AssertLayoutIsClear(preview.CreateDebugLayoutSnapshotForTests(1f, 900f));
        }

        [Test]
        public void RuntimeAiPlannerProfilePicker_ScansDefaultProfilesAndTemplateLabels()
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfilePickerEntry> entries =
                CharacterTestRuntimeAiPlannerProfilePicker.ScanProfilesForTests();

            CharacterTestRuntimeAiPlannerProfilePickerEntry defaultEntry =
                FindProfilePickerEntry(entries, CharacterTestRuntimeAiPlannerProfileJson.DefaultProjectAssetPath);
            Assert.IsTrue(defaultEntry.IsValid, defaultEntry.ReportText);
            Assert.That(defaultEntry.Label, Does.Contain("默认近战"));
            Assert.That(defaultEntry.Label, Does.Contain("default.json"));
            Assert.That(defaultEntry.Label, Does.Contain("通过"));
            Assert.That(defaultEntry.Label, Does.Not.Contain("/"));

            CharacterTestRuntimeAiPlannerProfilePickerEntry kiterEntry =
                FindProfilePickerEntry(entries, CharacterTestRuntimeAiPlannerTemplateLibrary.RangedKiterProjectAssetPath);
            Assert.IsTrue(kiterEntry.IsValid, kiterEntry.ReportText);
            Assert.That(kiterEntry.Label, Does.Contain("远程风筝"));
            Assert.That(kiterEntry.Label, Does.Contain("ranged_kiter.json"));
            Assert.That(kiterEntry.Label, Does.Not.Contain("/"));
        }

        [Test]
        public void RuntimeAiPlannerProfilePicker_ListsInvalidProfileButDisallowsBinding()
        {
            string projectPath = CharacterTestRuntimeAiPlannerTemplateLibrary.ProfileFolderProjectPath
                + "/__bad_profile_picker_test.json";
            string fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), projectPath));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, "{\"schema\":\"bad\"}");

            try
            {
                IReadOnlyList<CharacterTestRuntimeAiPlannerProfilePickerEntry> entries =
                    CharacterTestRuntimeAiPlannerProfilePicker.ScanProfilesForTests();

                CharacterTestRuntimeAiPlannerProfilePickerEntry entry =
                    FindProfilePickerEntry(entries, projectPath);
                Assert.IsFalse(entry.IsValid);
                Assert.IsFalse(entry.CanBind);
                Assert.That(entry.Label, Does.Contain("校验失败"));
                Assert.That(entry.ReportText, Does.Contain("json.schema.unsupported"));
            }
            finally
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);

                string metaPath = fullPath + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_AutoLoadsDefaultProfileWhenFileExists()
        {
            string fullPath = Path.GetFullPath(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    CharacterTestRuntimeAiPlannerProfileJson.DefaultProjectAssetPath));
            Assert.IsTrue(File.Exists(fullPath), fullPath);

            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();
                SetWindowField(window, "_activeTab", 0);
                InvokeWindowMethod(window, "RefreshView");

                AssertTextPresent(window.rootVisualElement, "状态树");
                AssertTextPresent(window.rootVisualElement, "状态");
                AssertTextAbsent(window.rootVisualElement, "请选择或创建 Runtime AI Planner Profile JSON");
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_MissingProfileShowsEmptyActionsWithoutDisabledStateTree()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();
                InvokeWindowMethod(window, "SetProfilePath", "Temp/missing-runtime-ai-planner-profile.json");
                InvokeWindowMethod(window, "TryAutoLoadSelectedProfile");
                InvokeWindowMethod(window, "RefreshView");

                AssertTextPresent(window.rootVisualElement, "Profile JSON");
                AssertTextPresent(window.rootVisualElement, "创建行为模板");
                AssertTextPresent(window.rootVisualElement, "选择 JSON");
                AssertTextPresent(window.rootVisualElement, "加载当前场景 Profile");
                AssertTextAbsent(window.rootVisualElement, "+ root");
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_InvalidJsonShowsDiagnosticAndCopiesReport()
        {
            string tempPath = Path.Combine("Temp", "runtime-ai-planner-bad-profile.json");
            Directory.CreateDirectory("Temp");
            File.WriteAllText(tempPath, "{\"schema\":\"bad\"}");
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();
                InvokeWindowMethod(window, "LoadProfileFromProjectPath", tempPath);

                AssertTextPresent(window.rootVisualElement, "Profile JSON 校验失败");
                AssertTextPresent(window.rootVisualElement, "json.schema.unsupported");

                EditorGUIUtility.systemCopyBuffer = string.Empty;
                InvokeWindowMethod(window, "CopyReport");
                StringAssert.Contains("json.schema.unsupported", EditorGUIUtility.systemCopyBuffer);
            }
            finally
            {
                window.Close();
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_CanCreateWithoutPlayTarget()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();
                Assert.NotNull(window.rootVisualElement);
                Assert.Greater(window.rootVisualElement.childCount, 0);
            }
            finally
            {
                window.Close();
            }
        }

    }
}
